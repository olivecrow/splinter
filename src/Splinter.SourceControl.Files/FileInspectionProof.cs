using System.Text.Json;
using System.Security.Cryptography;
using System.Buffers.Binary;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.SourceControl.Files;

public sealed partial class FileWorkspaceProvider
{
    // Only the same editorless ensure invocation consumes this proof. No disk
    // cache, timestamp-only reuse, or reuse after a provider restart is allowed.
    public async Task<ProvedSourceInspection> InspectWithProofAsync(WorkspaceRecord w, CancellationToken ct = default)
    {
        if (sourceChanges is null) return new(await InspectAsync(w, ct), null);
        Owner(w, Control(w)); RequireBasis(w, Control(w)); RequireMount(w);
        string identity = InspectionIdentity(w);
        using var before = await Generation(w, ct);
        using var current = Manifest(w, ScanWorkspace(w, ct));
        var state = Inspect(w, current);
        var metadata = InspectionMetadata(current.EnumerateEntries(), FilePath(Control(w), "inspection-proof"), ct);
        using var after = await Generation(w, ct);
        RequireSameGeneration(before, after);
        if (identity != InspectionIdentity(w)) throw ChangedInspection();
        return new(state, new InspectionProof(this, identity, before with { }, metadata, state));
    }

    private static string InspectionIdentity(WorkspaceRecord w) => JsonSerializer.Serialize(new
    {
        w.Id, w.RequestDigest, w.ProjectPath, w.Path, w.SourceKind, w.StorageMode, w.ApplicationKind,
        w.SourceReference, w.BaseRevision, w.ResultRevision, w.ManagedPaths,
        Snapshot = w.Snapshot?.Reference, Mount = w.Snapshot?.Mount
    }, Json);

    private async Task<SourceChangeView> Generation(WorkspaceRecord w, CancellationToken ct)
    {
        using var view = await sourceChanges!.ObserveChangesAsync(w, ct);
        if (view.WorkspaceId != w.Id || view.SnapshotId != w.Snapshot?.Reference?.Id ||
            string.IsNullOrWhiteSpace(view.Epoch) || view.Sequence < 0 || view.Changes is null)
            throw new WorkspaceException("SourceObservationInvalid", "Retain an incomplete or foreign source generation.");
        long count = 0, sequence = 0; HashSet<string>? inlineNames = view.Index is null ? new(StringComparer.OrdinalIgnoreCase) : null;
        foreach (var change in view.EnumerateChanges())
        {
            if (change is null || string.IsNullOrEmpty(change.Path) || change.Path.Contains('\\') || change.Path.StartsWith('/') ||
                change.Path.Split('/').Any(p => p is ".." or "." or "") || change.Sequence <= 0 || change.Sequence > view.Sequence ||
                inlineNames is not null && !inlineNames.Add(change.Path))
                throw new WorkspaceException("SourceObservationInvalid", "Retain an incomplete or foreign source generation.");
            count++; sequence = Math.Max(sequence, change.Sequence);
        }
        if (count != view.ChangesCount || sequence != view.Sequence) throw new WorkspaceException("SourceObservationInvalid", "Retain mismatched source change membership.");
        return view with { Changes = view.Index is null ? view.Changes.ToArray() : [] };
    }
    private static void RequireSameGeneration(SourceChangeView before, SourceChangeView after)
    {
        if (before.WorkspaceId != after.WorkspaceId || before.SnapshotId != after.SnapshotId || before.Epoch != after.Epoch ||
            before.Sequence != after.Sequence || before.ContentIdentity != after.ContentIdentity ||
            before.Index is null && !before.EnumerateChanges().SequenceEqual(after.EnumerateChanges())) throw ChangedInspection();
    }
    private static WorkspaceException ChangedInspection() => new("SourceChanged", "Source or inspection identity changed; preserve the workspace and inspect its current generation.");

    private sealed record InspectionMetadataProof(string Hash, long Count);
    private static InspectionMetadataProof InspectionMetadata(IEnumerable<FileSourceEntry> entries, string directory, CancellationToken ct)
    {
        // This single-use proof only compares membership and metadata. It does
        // not need a persistent lookup tree or a second path-interner index.
        using var scratch = new TemporaryMetadataDirectory(directory);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var records = entries.Select(entry => new MetadataEntry(PagedPathInterner.LookupIdentity(entry.Path),
            JsonSerializer.SerializeToUtf8Bytes(entry with { Hash = null }, Json)));
        string? previous = null; long count = 0; Span<byte> length = stackalloc byte[4];
        foreach (var entry in MetadataExternalSort.Order(records, scratch.Path, ct))
        {
            ct.ThrowIfCancellationRequested();
            if (previous == entry.Key) throw ChangedInspection();
            previous = entry.Key; hash.AppendData(Convert.FromHexString(entry.Key));
            BinaryPrimitives.WriteInt32LittleEndian(length, entry.Value.Length);
            hash.AppendData(length); hash.AppendData(entry.Value.Span); count = checked(count + 1);
        }
        return new(Convert.ToHexStringLower(hash.GetHashAndReset()), count);
    }

    private sealed class InspectionProof(FileWorkspaceProvider provider, string identity, SourceChangeView generation,
        InspectionMetadataProof metadata, SourceState state) : ISourceInspectionProof
    {
        private int consumed;
        private int disposed;
        public void Dispose() { if (Interlocked.Exchange(ref disposed, 1) == 0) generation.Dispose(); }
        public async Task<SourceState> RevalidateAsync(WorkspaceRecord w, CancellationToken ct = default)
        {
            try
            {
            if (Volatile.Read(ref disposed) != 0 || Interlocked.Exchange(ref consumed, 1) != 0 || identity != InspectionIdentity(w)) throw ChangedInspection();
            Owner(w, provider.Control(w)); RequireBasis(w, provider.Control(w)); RequireMount(w);
            using var before = await provider.Generation(w, ct);
            RequireSameGeneration(generation, before);
            var current = InspectionMetadata(Scan(w.Path, w.ManagedPaths, ct, metadataOnly: true, mount: w.Path),
                FilePath(provider.Control(w), "inspection-proof"), ct);
            if (metadata != current) throw ChangedInspection();
            using var after = await provider.Generation(w, ct);
            RequireSameGeneration(generation, after);
            if (identity != InspectionIdentity(w)) throw ChangedInspection();
            return state;
            }
            finally { Dispose(); }
        }
    }
}
