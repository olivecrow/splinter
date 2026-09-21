namespace Splinter.Core;

// Provider identities are opaque. The coordinator owns ordering and durable
// decisions; adapters own filesystem/VCS side effects and their receipts.
public sealed class SnapshotWorkspaceState
{
    public const long DefaultJournalBytes = 16L * 1024 * 1024 * 1024;
    public long MaxJournalBytes { get; set; } = DefaultJournalBytes;
    public SnapshotReference? Reference { get; set; }
    public OverlayMount? Mount { get; set; }
    public bool Attached { get; set; }
    public string Step { get; set; } = "requested";
    public SnapshotWorkspaceObservation? Observation { get; set; }
    public SnapshotResult? Result { get; set; }
}

public sealed record SnapshotWorkspaceObservation(bool? SnapshotAvailable, bool Mounted, bool ProviderAlive,
    bool OwnershipVerified, int? OpenHandles, long? WorkingSetBytes, long? PrivateBytes,
    OverlayStorageObservation? Storage = null, string? FailureCode = null,
    long? HostFreeBytes = null, long? JournalRemainingBytes = null, SnapshotObservation? SnapshotStorage = null, bool? DeltaPresent = null)
{
    public string SnapshotAvailability => SnapshotAvailable switch { true => "available", false => "missing", null => "unknown" };
}

// A read-only recovery decision is not permission to remove resources. Every
// resumed operation must re-observe its own identity and reclamation gates.
public sealed record SnapshotRecoveryDecision(string Disposition, string ReasonCode, string NextCommand)
{
    public static SnapshotRecoveryDecision For(WorkspaceRecord workspace)
    {
        var state = workspace.Snapshot;
        if (workspace.Phase == "released") return new("released", "WorkspaceReleased", "workspace.list");
        if (state is null) return new("preserve", "SnapshotStateMissing", "workspace.status");
        var observation = state.Observation;
        string? retainedFailure = workspace.LastError?.Split(':', 2)[0];
        if (retainedFailure is "DeltaChangedAfterCollection" or "StateCorrupt")
            return new("preserve", retainedFailure, "workspace.status");
        if (observation?.FailureCode is "DeltaCorrupt" or "OverlayIdentityMismatch" or "SnapshotIdentityMismatch" or "OverlayStartUncertain")
            return new("preserve", observation.FailureCode, "workspace.status");
        if (state.Step == "release-snapshot" && state.Result is { IndependentOfSnapshot: true } &&
            observation is { DeltaPresent: false, Mounted: false, ProviderAlive: false, OwnershipVerified: true,
                OpenHandles: 0, WorkingSetBytes: 0, PrivateBytes: 0 })
            return new("resume-release", "WorkspaceReleasing", "workspace.release");
        if (state.Reference is not null && observation?.SnapshotAvailable is null)
            return new("observe", observation?.FailureCode ?? "SnapshotObservationUnknown", "workspace.status");
        if (observation is { OwnershipVerified: false })
            return new("preserve", observation.FailureCode ?? "OverlayStartUncertain", "workspace.status");
        if (state.Step.StartsWith("release-", StringComparison.Ordinal))
            return new("resume-release", "WorkspaceReleasing", "workspace.release");
        if (state.Reference is not null && observation?.SnapshotAvailable == false)
            return state.Result is { IndependentOfSnapshot: true }
                ? new("resume-release", "SnapshotMissing", "workspace.release")
                : new("preserve", "SnapshotMissing", "workspace.status");
        if (!state.Attached || observation is not { Mounted: true, ProviderAlive: true })
            return new("resume-ensure", observation?.FailureCode ?? "SnapshotNotReady", "workspace.ensure");
        return new("ready", "WorkspaceReady", "workspace.status");
    }
}
public sealed record SnapshotDeltaSeal(long DurableSequence, long JournalBytes, string JournalHash);
public sealed record SnapshotResult(string SnapshotId, string Revision, string Fingerprint,
    string ResultReference, SnapshotDeltaSeal Seal, bool IndependentOfSnapshot);

public interface ISnapshotWorkspaceBackend
{
    Task<SnapshotReference> CaptureAsync(WorkspaceRecord workspace, string control, CancellationToken ct = default);
    Task<OverlayMount> ConnectAsync(WorkspaceRecord workspace, string control, CancellationToken ct = default);
    Task<SnapshotWorkspaceObservation> ObserveAsync(WorkspaceRecord workspace, string control, CancellationToken ct = default);
    Task<SnapshotDeltaSeal> SealAsync(WorkspaceRecord workspace, string control, CancellationToken ct = default);
    Task UnmountAsync(WorkspaceRecord workspace, string control, CancellationToken ct = default);
    Task RemoveDeltaAsync(WorkspaceRecord workspace, string control, CancellationToken ct = default);
    Task ReleaseSnapshotAsync(WorkspaceRecord workspace, string control, CancellationToken ct = default);
}

public interface ISnapshotSourceWorkspaceProvider
{
    Task<string> PreflightAsync(WorkspaceRecord workspace, CancellationToken ct = default);
    Task RegisterAsync(WorkspaceRecord workspace, string control, CancellationToken ct = default);
    Task AttachAsync(WorkspaceRecord workspace, string control, CancellationToken ct = default);
    Task<string> PreserveAsync(WorkspaceRecord workspace, string control, CancellationToken ct = default);
    Task VerifyResultAsync(WorkspaceRecord workspace, string control, CancellationToken ct = default);
    Task UnregisterAsync(WorkspaceRecord workspace, string control, CancellationToken ct = default);
}
