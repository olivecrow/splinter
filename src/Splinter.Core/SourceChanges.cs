namespace Splinter.Core;

// Changes are authoritative only within one immutable snapshot and one live
// provider epoch. A directory change invalidates every descendant as well.
public sealed record SourcePathChange(string Path, long Sequence);
public sealed record SourceChangeView(string WorkspaceId, string SnapshotId, string Epoch,
    long Sequence, SourcePathChange[] Changes);

public interface ISourceChangeProvider
{
    Task<SourceChangeView> ObserveChangesAsync(WorkspaceRecord workspace, CancellationToken ct = default);
    string CachePath(WorkspaceRecord workspace);
}

public sealed record SourceInspectionMetrics(string Mode, long PayloadBytesHashed, int FilesHashed,
    int FilesReused, long ManifestBytes, double ElapsedMilliseconds, string? Epoch = null, long? Sequence = null,
    long GitHashInputBytes = 0);

// Monotonic counters for this provider process only. Replay and current writes
// are separate; retained allocation must be measured by the platform adapter.
public sealed record OverlayIoMetrics(string Epoch, long LockAcquisitions, long LockWaitTicks,
    long FlushCount, long FlushTicks, long CommitCount, long JournalAppendedBytes,
    long ReplayBytes, long ReplayTicks, long JournalReadBytes, long BaseReadBytes, long StopwatchFrequency,
    long? ReplayFrames = null, long? ReplayReadCalls = null, long? ReplayReadTicks = null,
    long? ReplayValidationTicks = null, long? ReplayIndexTicks = null, int? ReplayBufferBytes = null);

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
