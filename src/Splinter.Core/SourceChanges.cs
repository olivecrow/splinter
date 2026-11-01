using System.Text.Json.Serialization;

namespace Splinter.Core;

// Changes are authoritative only within one immutable snapshot and one live
// provider epoch. A directory change invalidates every descendant as well.
public sealed record SourcePathChange(string Path, long Sequence);
public sealed record SourceChangeIndex(int Version, string Directory, MetadataTreeRoot Changes,
    MetadataTreeRoot Names, string[] ManagedPaths, long Count, bool CaseSensitive = false, string ContentHash = "", MetadataTreeRoot? ContentNodes = null,
    MetadataTreeRoot? ContentCaptures = null);
// Each bound view and its record copies own independent references to the
// same immutable reader. Copying a header never transfers a borrowed owner's
// lifetime; the last disposed reference releases the underlying protection.
public interface ISourceChangeReaderLease : IDisposable
{
    ISourceChangeReaderLease Retain();
    IEnumerable<SourcePathChange> Read(SourceChangeView view);
}

public sealed record SourceChangeView(string WorkspaceId, string SnapshotId, string Epoch,
    long Sequence, SourcePathChange[] Changes) : IDisposable
{
    private ISourceChangeReaderLease? readerLease;
    private int disposed;
    private SourceChangeView(SourceChangeView original)
    {
        original.ThrowIfDisposed();
        WorkspaceId = original.WorkspaceId; SnapshotId = original.SnapshotId; Epoch = original.Epoch;
        Sequence = original.Sequence; Changes = original.Changes; Index = original.Index; ChangesReader = original.ChangesReader;
        ReaderLease = original.ReaderLease;
    }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SourceChangeIndex? Index { get; init; }
    [JsonIgnore]
    public Func<IEnumerable<SourcePathChange>>? ChangesReader { get; init; }
    [JsonIgnore]
    public ISourceChangeReaderLease? ReaderLease
    {
        get => readerLease;
        init
        {
            // Retain before releasing the copied reference, including when a
            // with-expression explicitly rebinds this view to another reader.
            var retained = value?.Retain();
            Interlocked.Exchange(ref readerLease, retained)?.Dispose();
        }
    }
    [JsonIgnore]
    public long ChangesCount => Index?.Count ?? Changes.LongLength;
    [JsonIgnore]
    public string ContentIdentity => Index is null ? "inline" : Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
        System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new { Index.ContentHash, Index.ManagedPaths, Index.Count, Index.CaseSensitive })));
    public IEnumerable<SourcePathChange> EnumerateChanges()
    {
        ThrowIfDisposed();
        return ReaderLease?.Read(this) ?? ChangesReader?.Invoke() ??
            (Index is null ? Changes : throw new WorkspaceException("SourceObservationUnbound", "Bind the protected change-index reader before enumerating paths."));
    }
    public void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0) Interlocked.Exchange(ref readerLease, null)?.Dispose();
    }
}

public interface ISourceChangeProvider
{
    Task<SourceChangeView> ObserveChangesAsync(WorkspaceRecord workspace, CancellationToken ct = default);
    string CachePath(WorkspaceRecord workspace);
}

// Optional immutable-reader evidence, retained only after complete content
// verification. A missing/evicted proof requires the ordinary payload read.
public sealed record SnapshotContentSummary(string Path, long Length, string Sha256,
    bool ContainsCarriageReturn, bool ContainsNul);
public interface ISnapshotContentSummaryProvider
{
    SnapshotContentSummary? GetVerifiedContentSummary(string path);
}
public sealed record SourceContentSummaryView(SnapshotReference Snapshot, SourceChangeView Generation,
    SnapshotContentSummary[] Files);
public interface ISourceContentSummaryProvider
{
    // Only the first live provider generation may supply initial-admission
    // evidence. The caller must bind this request to its same-operation barrier.
    // The returned Generation is owned by the caller and must be disposed after
    // consuming or rejecting the summary. The one exception is the identical
    // expected object, which is borrowed and must remain open for its owner.
    // A with-copy has its own retained lease and is not a borrowed reference.
    Task<SourceContentSummaryView?> ObserveInitialContentAsync(WorkspaceRecord workspace,
        SourceChangeView expected, string[] paths, CancellationToken ct = default);
}

public sealed record SourceInspectionMetrics(string Mode, long PayloadBytesHashed, long FilesHashed,
    long FilesReused, long ManifestBytes, double ElapsedMilliseconds, string? Epoch = null, long? Sequence = null,
    long GitHashInputBytes = 0, [property: JsonRequired] long GitHashProcesses = 0);

// Per-provider bounded-read work, independent of wall-clock and physical I/O.
public sealed record ReadRangeIndexMetrics(long ReadCalls, long SearchProbes, long VisitedExtents);

public sealed record ReplayIndexOperationMetrics(string Operation, long Frames, long ApplyTicks);

// Bounded replay-only distribution of prior extents on each write range replacement.
// Buckets are 0, 1-8, 9-64, 65-512, 513-4096, and above 4096.
public sealed record ReplayWriteRangeIndexMetrics(long ReplaceCalls, long PriorExtents,
    int MaxPriorExtents, long VisitedExtents, long CopiedExtents,
    long LocateAndReplaceTicks, long SortTicks, long[] PriorExtentBuckets);

// Flush waiter buckets are 0, 1, 2-3, 4-7, and 8 or more other mutation calls.
// Waiting calls may later be no-ops or fail admission; they are not batch sizes.
public sealed record LiveCommitOperationMetrics(string Operation, long Attempts, long DurableCommits,
    long FailedCalls, long PayloadBytes, long JournalBytes, long PrepareTicks, long AppendTicks,
    long FlushTicks, long ApplyTicks, long TotalTicks, long FlushCalls, int MaxWaitingMutations,
    long[] WaitingMutationsAtFlushStart, long[] WaitingMutationsAtFlushEnd);
public sealed record LiveJournalWriteMetrics(long MutationGateCalls, long MutationGateWaitTicks,
    LiveCommitOperationMetrics[] Operations);

// Monotonic counters for this provider process only. Replay and current writes
// are separate; retained allocation must be measured by the platform adapter.
public sealed record OverlayIoMetrics(string Epoch, long LockAcquisitions, long LockWaitTicks,
    long FlushCount, long FlushTicks, long CommitCount, long JournalAppendedBytes,
    long ReplayBytes, long ReplayTicks, long JournalReadBytes, long BaseReadBytes, long StopwatchFrequency,
    [property: JsonRequired] long ReplayFrames, [property: JsonRequired] long ReplayReadCalls, [property: JsonRequired] long ReplayReadTicks,
    [property: JsonRequired] long ReplayValidationTicks, [property: JsonRequired] long ReplayIndexTicks, [property: JsonRequired] int ReplayBufferBytes,
    [property: JsonRequired] ReplayIndexOperationMetrics[] ReplayIndexOperations,
    [property: JsonRequired] ReplayWriteRangeIndexMetrics ReplayWriteRangeIndex,
    [property: JsonRequired] LiveJournalWriteMetrics LiveJournalWrites,
    [property: JsonRequired] ReadRangeIndexMetrics ReadRangeIndex);

public static class SourcePathPolicy
{
    public static bool Managed(string path, string[] roots) => roots.Any(root => root.Contains('*')
        ? !path.Contains('/') && System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(root, path, OperatingSystem.IsWindows())
        : path.Equals(root, Comparison) || path.StartsWith(root.TrimEnd('/') + "/", Comparison));

    public static bool Affects(string change, string path) => path.Equals(change, Comparison) ||
        path.StartsWith(change.TrimEnd('/') + "/", Comparison);

    private static StringComparison Comparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
