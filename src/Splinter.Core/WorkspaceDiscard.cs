using System.Text.Json.Serialization;

namespace Splinter.Core;

public sealed record WorkspaceDiscardRequest(string Id, string IntentKey, string ExpectedRequestDigest);

// A durable user intent is independent of compile/collection success. The
// provider must compare this frozen owner and source revision on every retry.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorkspaceDiscardIntent(
    [property: JsonRequired] int Version,
    [property: JsonRequired] string WorkspaceId,
    [property: JsonRequired] string IntentKey,
    [property: JsonRequired] string RequestDigest,
    [property: JsonRequired] string ProjectPath,
    [property: JsonRequired] string Path,
    [property: JsonRequired] string SourceReference,
    [property: JsonRequired] string BaseRevision,
    [property: JsonRequired] string SourceRevision,
    [property: JsonRequired] SnapshotReference Snapshot,
    [property: JsonRequired] DateTimeOffset CreatedAt);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class WorkspaceDiscardState
{
    [JsonRequired] public required WorkspaceDiscardIntent Intent { get; init; }
    [JsonRequired] public string Step { get; set; } = "stop";
    public SnapshotDeltaSeal? Seal { get; set; }

    public void RequireMatch(WorkspaceRecord workspace)
    {
        var i = Intent;
        if (i is null || i.Version != 1 || i.Snapshot is null ||
            new[] { i.WorkspaceId, i.RequestDigest, i.ProjectPath, i.Path, i.SourceReference, i.BaseRevision }.Any(string.IsNullOrWhiteSpace) ||
            i.WorkspaceId != workspace.Id ||
            i.RequestDigest != workspace.RequestDigest || i.ProjectPath != workspace.ProjectPath || i.Path != workspace.Path ||
            i.SourceReference != workspace.SourceReference || i.BaseRevision != workspace.BaseRevision ||
            i.Snapshot != workspace.Snapshot?.Reference || string.IsNullOrWhiteSpace(i.SourceRevision) ||
            string.IsNullOrWhiteSpace(i.IntentKey) || i.IntentKey.Length > 128 || i.CreatedAt == default ||
            Step is not ("stop" or "unmount" or "source" or "delta" or "snapshot" or "complete") ||
            Step != "stop" && Seal is null ||
            Seal is { } seal && (seal.DurableSequence < 0 || seal.JournalBytes < 0 || string.IsNullOrWhiteSpace(seal.JournalHash)) ||
            workspace.Snapshot?.Step != (Step == "complete" ? "discarded" : "discard-" + Step) ||
            workspace.Phase is not ("discarding" or "discard-reclaim-pending" or "discarded") ||
            (Step == "complete") != (workspace.Phase == "discarded") ||
            workspace.Phase == "discard-reclaim-pending" && Step != "snapshot")
            throw new WorkspaceException("DiscardIntentMismatch", "Preserve the workspace: discard owner, stage or frozen identity differs.");
    }
}

// No default destructive implementation. Production adapters must explicitly
// implement owner-checked, same-intent retries; release's result gate stays intact.
public interface IWorkspaceDiscardProvider
{
    // Read-only preflight of owned source registration; return its current HEAD.
    Task<string> ObserveSourceRevisionAsync(WorkspaceRecord workspace, string control, CancellationToken ct);
    // Unmounted root only. Remove this intent's registration and task branch,
    // comparing the frozen revision; retain independent results/shared refs/objects.
    // Never repository GC or an implicit discard of previously preserved results.
    Task DiscardSourceAsync(WorkspaceRecord workspace, string control, CancellationToken ct);
    // Verify intent + seal, zero handles/processes and owned file allowlist.
    // Persist deletion intent before partial removal; reconcile a lost response.
    Task DiscardDeltaAsync(WorkspaceRecord workspace, string control, CancellationToken ct);
    // Exact owned snapshot only, after source/delta absence and dependency checks.
    // Use a distinct discard authorization, never claim results were preserved.
    Task<SnapshotReclamation> DiscardSnapshotAsync(WorkspaceRecord workspace, string control, CancellationToken ct);
}
