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

public sealed record SnapshotWorkspaceObservation(bool SnapshotAvailable, bool Mounted, bool ProviderAlive,
    bool OwnershipVerified, int? OpenHandles, long? WorkingSetBytes, long? PrivateBytes,
    OverlayStorageObservation? Storage = null, string? FailureCode = null,
    long? HostFreeBytes = null, long? JournalRemainingBytes = null, SnapshotObservation? SnapshotStorage = null, bool? DeltaPresent = null);
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
