using System.Text.Json;
using Splinter.Core;

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
        var before = await Generation(w, ct);
        var entries = ScanWorkspace(w, ct);
        var state = Inspect(w, entries);
        RequireSameGeneration(before, await Generation(w, ct));
        if (identity != InspectionIdentity(w)) throw ChangedInspection();
        return new(state, new InspectionProof(this, identity, before, entries.Select(e => e with { Hash = null }).ToArray(), state));
    }

    private static string InspectionIdentity(WorkspaceRecord w) => JsonSerializer.Serialize(new
    {
        w.Id, w.RequestDigest, w.ProjectPath, w.Path, w.SourceKind, w.StorageMode, w.ApplicationKind,
        w.SourceReference, w.BaseRevision, w.ResultRevision, w.ManagedPaths,
        Snapshot = w.Snapshot?.Reference, Mount = w.Snapshot?.Mount
    }, Json);

    private async Task<SourceChangeView> Generation(WorkspaceRecord w, CancellationToken ct)
    {
        var view = await sourceChanges!.ObserveChangesAsync(w, ct);
        if (view.WorkspaceId != w.Id || view.SnapshotId != w.Snapshot?.Reference?.Id ||
            string.IsNullOrWhiteSpace(view.Epoch) || view.Sequence < 0 || view.Changes is null ||
            view.Changes.Any(c => c is null || string.IsNullOrEmpty(c.Path) || c.Path.Contains('\\') || c.Path.StartsWith('/') ||
                c.Path.Split('/').Any(p => p is ".." or "." or "") || c.Sequence <= 0 || c.Sequence > view.Sequence) ||
            view.Sequence != (view.Changes.Length == 0 ? 0 : view.Changes.Max(c => c.Sequence)) ||
            view.Changes.Select(c => c.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != view.Changes.Length)
            throw new WorkspaceException("SourceObservationInvalid", "Retain an incomplete or foreign source generation.");
        return view with { Changes = view.Changes.ToArray() };
    }
    private static void RequireSameGeneration(SourceChangeView before, SourceChangeView after)
    {
        if (before.WorkspaceId != after.WorkspaceId || before.SnapshotId != after.SnapshotId || before.Epoch != after.Epoch ||
            before.Sequence != after.Sequence || !before.Changes.SequenceEqual(after.Changes)) throw ChangedInspection();
    }
    private static WorkspaceException ChangedInspection() => new("SourceChanged", "Source or inspection identity changed; preserve the workspace and inspect its current generation.");

    private sealed class InspectionProof(FileWorkspaceProvider provider, string identity, SourceChangeView generation,
        FileSourceEntry[] metadata, SourceState state) : ISourceInspectionProof
    {
        private int consumed;
        public async Task<SourceState> RevalidateAsync(WorkspaceRecord w, CancellationToken ct = default)
        {
            if (Interlocked.Exchange(ref consumed, 1) != 0 || identity != InspectionIdentity(w)) throw ChangedInspection();
            Owner(w, provider.Control(w)); RequireBasis(w, provider.Control(w)); RequireMount(w);
            RequireSameGeneration(generation, await provider.Generation(w, ct));
            var current = Scan(w.Path, w.ManagedPaths, ct, metadataOnly: true, mount: w.Path);
            if (!metadata.SequenceEqual(current)) throw ChangedInspection();
            RequireSameGeneration(generation, await provider.Generation(w, ct));
            if (identity != InspectionIdentity(w)) throw ChangedInspection();
            return state;
        }
    }
}
