namespace Splinter.Core;

// This is an observation of a release attempt, not a promise that a later
// deletion is safe. Only an explicit retry can refresh it and remove the ID.
public sealed record SnapshotReclamation(string SnapshotId, string AttemptId, string Disposition,
    DateTimeOffset ObservedAt, string[] BlockingSnapshotIds, SnapshotSharedStorage? SharedStorage = null)
{
    public void RequireMatch(SnapshotReference snapshot)
    {
        if (SnapshotId != snapshot.Id || string.IsNullOrWhiteSpace(AttemptId) ||
            ObservedAt == default || ObservedAt > DateTimeOffset.UtcNow.AddMinutes(1) ||
            BlockingSnapshotIds is null || BlockingSnapshotIds.Any(string.IsNullOrWhiteSpace) ||
            BlockingSnapshotIds.Contains(snapshot.Id, StringComparer.OrdinalIgnoreCase) ||
            BlockingSnapshotIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() != BlockingSnapshotIds.Length ||
            Disposition is not ("deferred" or "released") ||
            (Disposition == "deferred") != (BlockingSnapshotIds.Length > 0))
            throw new WorkspaceException("SnapshotReclamationInvalid", "Preserve the snapshot: its release observation is invalid.");
        SharedStorage?.RequireValid();
    }
}

// Provider accounting is shared by a source volume. These values must never be
// summed across workspaces or described as the cost of this one snapshot.
public sealed record SnapshotSharedStorage(string VolumeId, DateTimeOffset ObservedAt,
    long? UsedBytes, long? AllocatedBytes, long? LimitBytes, string? FailureCode = null)
{
    public string Scope => "source-volume-shared";
    public string WorkspaceAttribution => "unknown";
    public void RequireValid()
    {
        if (string.IsNullOrWhiteSpace(VolumeId) || ObservedAt == default ||
            ObservedAt > DateTimeOffset.UtcNow.AddMinutes(1) || UsedBytes < 0 || AllocatedBytes < 0 || LimitBytes < 0 ||
            (FailureCode is not null && (UsedBytes is not null || AllocatedBytes is not null || LimitBytes is not null)))
            throw new WorkspaceException("SnapshotReclamationInvalid", "Shared storage accounting is incomplete or invalid; do not infer a workspace cost.");
    }
}
