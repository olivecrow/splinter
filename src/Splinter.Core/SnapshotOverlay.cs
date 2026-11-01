using System.Text.Json.Serialization;

namespace Splinter.Core;

// No provider-specific API or live-source fallback belongs in this contract.
public sealed record SnapshotReference(string Id, string Provider, string SourcePath,
    string RootPath, DateTimeOffset CapturedAt, bool Persistent, bool Differential,
    string Consistency,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PathPolicy = null);
public sealed record SnapshotCapture(string SourcePath, string OwnerId, string ReceiptDirectory);
// A verified access location is replaceable; the snapshot and delta identity are not.
public sealed record SnapshotReadAccess(string SnapshotId, string RootPath, string VerificationId)
{
    public void RequireMatch(SnapshotReference snapshot)
    {
        if (SnapshotId != snapshot.Id || string.IsNullOrWhiteSpace(RootPath) || string.IsNullOrWhiteSpace(VerificationId))
            throw new WorkspaceException("SnapshotIdentityMismatch", "Read access must identify the original snapshot and its verification.");
    }
}
public sealed record SnapshotObservation(string Id, bool? Available, string? FailureCode,
    long? PreservedBytes, long? AllocatedBytes)
{
    public string Availability => Available switch { true => "available", false => "missing", null => "unknown" };
    public SnapshotReadAccess? ReadAccess { get; init; }
}

public interface ISourceSnapshotProvider
{
    Task<SnapshotReference> CaptureAsync(SnapshotCapture request, CancellationToken cancellationToken = default);
    Task<SnapshotObservation> ObserveAsync(SnapshotReference snapshot, CancellationToken cancellationToken = default);
    Task<SnapshotReclamation> ReleaseAsync(SnapshotReference snapshot, CancellationToken cancellationToken = default,
        string? previousAttemptId = null);
}

public sealed record SnapshotEntry(string Path, bool Directory, long Length, uint Attributes,
    long CreationTime, long LastAccessTime, long LastWriteTime,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] uint? UnixMode = null);

// Implementations must read one immutable snapshot ID, never the mutable original.
// A missing snapshot fails reads; reconnect must not capture a replacement.
// Metadata and availability queries may overlap a payload read. Owners must
// drain their delta-store reads before disposing the tree or its basis lease.
public interface ISnapshotTree
{
    SnapshotReference Snapshot { get; }
    SnapshotGraft? Graft => null;
    // Opt in only when simultaneous Read calls are safe for every path. The
    // value is fixed for this tree's lifetime; any internal decoder remains
    // synchronized by its provider. Owners still drain reads before disposal.
    bool SupportsConcurrentReads => false;
    void EnsureAvailable();
    // A scoped reader may reuse a fenced availability proof while reading,
    // but must revalidate external state before publishing independent data.
    void VerifyReadBoundary() => EnsureAvailable();
    SnapshotEntry? Stat(string relativePath);
    IReadOnlyList<SnapshotEntry> List(string relativeDirectory);
    // A paged provider binds an immutable directory root at this call. The
    // iterator must not change membership as callbacks consume later pages.
    IEnumerable<SnapshotEntry> EnumerateChildren(string relativeDirectory) => List(relativeDirectory);
    int Read(string relativePath, long offset, Span<byte> destination);
}

// Optional, explicitly held immutable-metadata observation. Returning null
// keeps the ordinary provider's fresh observation. A successful scope owns
// exact write/delete fences plus the snapshot reader/reclamation lease until
// disposal; VerifyReadBoundary still performs the full publication proof.
// Native binding costs are separate from the bounded metadata page cache.
public interface ISnapshotAvailabilityScope
{
    SnapshotObservation? ObserveAvailability(SnapshotReadAccess expectedAccess);
    long MetadataFenceCount { get; }
    long MetadataFenceBytes { get; }
}

public static class SnapshotAvailabilityScopes
{
    public static SnapshotObservation? Observe(ISnapshotTree? tree, SnapshotReference expected, SnapshotReadAccess access)
    {
        if (tree is not ISnapshotAvailabilityScope scope) return null;
        access.RequireMatch(expected);
        if (tree.Snapshot != expected) throw new WorkspaceException("SnapshotIdentityMismatch", "The held observation belongs to another immutable snapshot.");
        var observation = scope.ObserveAvailability(access);
        if (observation is not null && (observation.Id != expected.Id || observation.Available == true && observation.ReadAccess != access))
            throw new WorkspaceException("SnapshotIdentityMismatch", "The held observation cannot replace its fixed read identity.");
        return observation;
    }
}

[System.Text.Json.Serialization.JsonConverter(typeof(OverlayIdentityJsonConverter))]
public sealed record OverlayIdentity(int Version, string Id, SnapshotReference Snapshot,
    [property: JsonIgnore] long MaxJournalBytes = long.MaxValue, int MaxTransferBytes = 1048576, string[]? ExcludedTopLevel = null,
    string[]? IncludedTopLevel = null, string[]? DeniedTopLevel = null, SnapshotGraft? Graft = null);
public sealed record OverlayStorageObservation(string Id, string SnapshotId, long InitialPayloadCopiedBytes,
    long JournalBytes, long PayloadBytes, long MetadataBytes, long ReadBytes,
    long ReadCacheBytes, int MaxTransferBytes, long IndexedNodes, long DurableSequence)
{
    public OverlayIoMetrics? Metrics { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? MetadataCacheBytes { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? PeakMetadataCacheBytes { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? SealJournalBytes { get; init; }
    public bool JournalOptimizationEnabled { get; init; }
    public string JournalBytesBasis { get; init; } = "current-files";
    public string JournalStorageMode { get; init; } = "append";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? BoundedLogBytesLimit { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? BoundedLogFrameLimit { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? BoundedLogAppendedBytes { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? BoundedLogAppendedFrames { get; init; }
}
public sealed record DeltaEntry(Guid Id, string Path, bool Directory, long Length, uint Attributes,
    long CreationTime, long LastAccessTime, long LastWriteTime, string? BasePath, long BaseLength,
    bool Deleted = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] uint? UnixMode = null);

public interface IWorkspaceDeltaStore : IDisposable
{
    OverlayIdentity Identity { get; }
    string? BackingPath => null;
    DeltaEntry? Stat(string path);
    DeltaEntry Get(Guid id);
    IReadOnlyList<DeltaEntry> List(string directory);
    IEnumerable<DeltaEntry> EnumerateChildren(string directory) => List(directory);
    DeltaEntry Create(string path, bool directory, uint attributes);
    int Read(Guid id, long offset, Span<byte> destination);
    void Write(Guid id, long offset, ReadOnlySpan<byte> data);
    void SetLength(Guid id, long length);
    void SetMetadata(Guid id, uint attributes, long creationTime, long accessTime, long writeTime);
    // One file callback's final length and metadata. Preserve truncation and
    // acknowledged durability; a provider may retain separate durable effects.
    void SetLengthAndMetadata(Guid id, long length, uint attributes, long creationTime, long accessTime, long writeTime);
    void SetUnixMode(Guid id, uint? mode) => throw new WorkspaceException("UnixPermissionsUnsupported", "This delta provider cannot change Unix permissions.");
    void Rename(Guid id, string destination, bool replace);
    void Delete(Guid id);
    // The caller has closed every retained context for this identity. A provider
    // may reclaim an unlinked file; live names and other identities stay intact.
    void ReleaseUnlinkedFile(Guid id) { }
    void Flush();
    OverlayStorageObservation Observe();
    // Live accounting is rebuilt on reconnect and maintained by the exclusive
    // writer. It is not a disk audit or a result-preservation proof.
    long GetJournalBytes();
    OverlayStorageObservation? ObserveActivity();
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
