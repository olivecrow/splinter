namespace Splinter.Core;

// No provider-specific API or live-source fallback belongs in this contract.
public sealed record SnapshotReference(string Id, string Provider, string SourcePath,
    string RootPath, DateTimeOffset CapturedAt, bool Persistent, bool Differential,
    string Consistency);
public sealed record SnapshotCapture(string SourcePath, string OwnerId, string ReceiptDirectory);
public sealed record SnapshotObservation(string Id, bool Available, string? FailureCode,
    long? PreservedBytes, long? AllocatedBytes);

public interface ISourceSnapshotProvider
{
    Task<SnapshotReference> CaptureAsync(SnapshotCapture request, CancellationToken cancellationToken = default);
    Task<SnapshotObservation> ObserveAsync(SnapshotReference snapshot, CancellationToken cancellationToken = default);
    Task ReleaseAsync(SnapshotReference snapshot, CancellationToken cancellationToken = default);
}

public sealed record SnapshotEntry(string Path, bool Directory, long Length, uint Attributes,
    long CreationTime, long LastAccessTime, long LastWriteTime);

// Implementations must read one immutable snapshot ID, never the mutable original.
// A missing snapshot fails reads; reconnect must not capture a replacement.
public interface ISnapshotTree
{
    SnapshotReference Snapshot { get; }
    void EnsureAvailable();
    SnapshotEntry? Stat(string relativePath);
    IReadOnlyList<SnapshotEntry> List(string relativeDirectory);
    int Read(string relativePath, long offset, Span<byte> destination);
}

public sealed record OverlayIdentity(int Version, string Id, SnapshotReference Snapshot,
    long MaxJournalBytes, int MaxTransferBytes, string[]? ExcludedTopLevel = null);
public sealed record OverlayStorageObservation(string Id, string SnapshotId, long InitialPayloadCopiedBytes,
    long JournalBytes, long PayloadBytes, long MetadataBytes, long ReadBytes,
    long ReadCacheBytes, int MaxTransferBytes, int IndexedNodes, long DurableSequence);
public sealed record DeltaEntry(Guid Id, string Path, bool Directory, long Length, uint Attributes,
    long CreationTime, long LastAccessTime, long LastWriteTime, string? BasePath, long BaseLength,
    bool Deleted = false);

public interface IWorkspaceDeltaStore : IDisposable
{
    OverlayIdentity Identity { get; }
    DeltaEntry? Stat(string path);
    DeltaEntry Get(Guid id);
    IReadOnlyList<DeltaEntry> List(string directory);
    DeltaEntry Create(string path, bool directory, uint attributes);
    int Read(Guid id, long offset, Span<byte> destination);
    void Write(Guid id, long offset, ReadOnlySpan<byte> data);
    void SetLength(Guid id, long length);
    void SetMetadata(Guid id, uint attributes, long creationTime, long accessTime, long writeTime);
    void Rename(Guid id, string destination, bool replace);
    void Delete(Guid id);
    void Flush();
    OverlayStorageObservation Observe();
}
public sealed record OverlayMount(string Id, string Path, string DeltaPath, SnapshotReference Snapshot,
    int ProviderProcessId, long ProviderStartTimeUtcTicks);
public sealed record OverlayMountObservation(bool Mounted, bool OwnershipVerified,
    int? OpenHandles, long? WorkingSetBytes, long? PrivateBytes, string? FailureCode);

public interface IOverlayMountProvider
{
    Task<OverlayMount> MountAsync(OverlayIdentity identity, string deltaPath, string mountPath,
        CancellationToken cancellationToken = default);
    Task<OverlayMount> ReconnectAsync(OverlayMount mount, CancellationToken cancellationToken = default);
    Task FlushAsync(OverlayMount mount, CancellationToken cancellationToken = default);
    Task<OverlayMountObservation> ObserveAsync(OverlayMount mount, CancellationToken cancellationToken = default);
    Task UnmountAsync(OverlayMount mount, CancellationToken cancellationToken = default);
}

public sealed record OverlayResultReceipt(string Id, string SnapshotId, long DurableSequence,
    string ArtifactPath, string ArtifactHash, bool IndependentOfSnapshot);

// Release orchestration must use observed facts, not elapsed time or caller exit.
public static class OverlayReleaseGate
{
    public static void RequireSafe(OverlayStorageObservation storage, OverlayResultReceipt? result,
        OverlayMountObservation mount, int snapshotDependents)
    {
        if (result is null || !result.IndependentOfSnapshot || result.SnapshotId != storage.SnapshotId ||
            result.Id != storage.Id || result.DurableSequence != storage.DurableSequence ||
            string.IsNullOrWhiteSpace(result.ArtifactHash))
            throw new WorkspaceException("OverlayResultRequired", "Preserve and verify the current result independently of the snapshot before reclamation.");
        if (mount.Mounted || !mount.OwnershipVerified || mount.OpenHandles != 0 ||
            mount.WorkingSetBytes != 0 || mount.PrivateBytes != 0)
            throw new WorkspaceException("OverlayCleanupIncomplete", "Owned mounts, handles and provider memory must be observed absent.");
        if (snapshotDependents != 0)
            throw new WorkspaceException("SnapshotInUse", "Other deltas still depend on the snapshot.");
    }
}
