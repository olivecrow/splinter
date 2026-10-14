using System.Collections.Concurrent;
using Splinter.Core;

namespace Splinter.SourceControl.Git;

public sealed partial class GitWorkspaceProvider
{
    private sealed record InitialContentAdmission(SnapshotReference Snapshot, OverlayMount? Mount,
        string Path, string? Revision, string[] ManagedPaths, SourceChangeView Generation);
    private readonly ConcurrentDictionary<string, InitialContentAdmission> initialContentAdmissions = new(StringComparer.Ordinal);

    // Attach captures this barrier after creating its private administration
    // pointer and completing the required index refresh. Full mutation history
    // still excludes every edited source path from immutable-content reuse.
    // Only this instance's next inspection consumes it; it is never persisted.
    internal async Task BeginInitialContentInspectionAsync(WorkspaceRecord workspace, CancellationToken ct)
    {
        initialContentAdmissions.TryRemove(workspace.Id, out _);
        if (auditSource || workspace.StorageMode != "git-snapshot" || workspace.Snapshot is not { Attached: false, Reference: { } snapshot } ||
            sourceChanges is not ISourceContentSummaryProvider) return;
        var before = await sourceChanges.ObserveChangesAsync(workspace, ct);
        RequireView(workspace, before);
        initialContentAdmissions[workspace.Id] = new(snapshot, workspace.Snapshot.Mount, workspace.Path, workspace.BaseRevision,
            [.. workspace.ManagedPaths], before with { Changes = [.. before.Changes] });
    }

    private async Task<Dictionary<string, (SnapshotContentSummary Summary, string ObjectId)>> InitialContentAsync(
        WorkspaceRecord workspace, InitialContentAdmission? admission, SourceChangeView? current, string revision,
        string[] files, bool hasPrior, CancellationToken ct)
    {
        var result = new Dictionary<string, (SnapshotContentSummary, string)>(StringComparer.Ordinal);
        if (admission is null || auditSource || hasPrior ||
            workspace.Snapshot?.Attached != false || current is null || sourceChanges is not ISourceContentSummaryProvider provider)
            return result;
        if (admission.Snapshot != workspace.Snapshot.Reference || admission.Mount != workspace.Snapshot.Mount ||
            admission.Path != workspace.Path || admission.Revision != workspace.BaseRevision || revision != admission.Revision ||
            !admission.ManagedPaths.SequenceEqual(workspace.ManagedPaths) || !SameGeneration(admission.Generation, current))
            throw Error("SourceChanged", "Initial content admission changed after attach; preserve and inspect the current source.");

        Dictionary<string, (string ObjectId, long Length)>? basis = null;
        foreach (string[] batch in files.Chunk(4096))
        {
            var proof = await provider.ObserveInitialContentAsync(workspace, current, batch, ct);
            if (proof is null) continue; // Unproved/reconnected sources retain full reads.
            if (proof.Snapshot != admission.Snapshot || !SameGeneration(proof.Generation, current) ||
                proof.Files is null || proof.Files.Length > batch.Length)
                throw Error("SourceObservationInvalid", "Initial content evidence has another snapshot or source generation.");
            var requested = batch.ToHashSet(StringComparer.Ordinal);
            foreach (var summary in proof.Files)
            {
                if (summary is null || !requested.Remove(summary.Path) || summary.Length < 0 ||
                    summary.Sha256 is not { Length: 64 } || !summary.Sha256.All(char.IsAsciiHexDigit) ||
                    current.Changes.Any(change => SourcePathPolicy.Affects(change.Path, summary.Path)))
                    throw Error("SourceObservationInvalid", "Initial content evidence does not identify an unchanged requested file.");
                // Git knowledge stays in this adapter. The fixed commit's blob
                // identity is reusable only alongside the provider's complete
                // raw-content proof for this exact snapshot/path/length.
                if (basis is null)
                {
                    basis = new(StringComparer.Ordinal);
                    foreach (string entry in SplitZero(await Required(workspace.Path, ["ls-tree", "-r", "-l", "-z", admission.Revision!], ct)))
                    {
                        string[] split = entry.Split('\t', 2);
                        string[] header = split[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (split.Length != 2 || header.Length != 4 || header[0] is not ("100644" or "100755") ||
                            header[1] != "blob" || header[2].Length is not (40 or 64) || !header[2].All(char.IsAsciiHexDigit) ||
                            !long.TryParse(header[3], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out long length))
                            throw Error("GitBasisObjectInvalid", "The fixed initial source tree has invalid blob metadata.");
                        basis.Add(split[1], (header[2], length));
                    }
                }
                if (!basis.TryGetValue(summary.Path, out var blob) || blob.Length != summary.Length)
                    throw Error("SourceObservationInvalid", "Initial content evidence does not match the fixed source tree.");
                result.Add(summary.Path, (summary, blob.ObjectId));
            }
        }
        return result;
    }

    private static bool SameGeneration(SourceChangeView? left, SourceChangeView? right) =>
        left is not null && right is not null && left.Changes is not null && right.Changes is not null &&
        left.WorkspaceId == right.WorkspaceId && left.SnapshotId == right.SnapshotId && left.Epoch == right.Epoch &&
        left.Sequence == right.Sequence && left.Changes.SequenceEqual(right.Changes);
}
