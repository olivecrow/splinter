using System.Diagnostics;

namespace Splinter.SourceControl.Git;

// Bounded process-local diagnostics, never a persisted content/identity proof.
// Processing and wait ticks sum concurrent requests; they are not elapsed wall time.
public sealed record GitReadObservation(long Timestamp, long StopwatchFrequency, DateTimeOffset ObservedAt,
    long Requests, long RequestedBytes, long FailedRequests, long FullProofDecodedBytes, long ProvedReadDecodedBytes,
    long FullProofProcessingTicks, long ProvedReadProcessingTicks, long ReaderWaitCount, long ReaderWaitTicks,
    long PayloadCacheHits, long PayloadCacheMisses, long CapacityEvictedBytes, long UnusedReadAheadEvictedBytes,
    int FullBlobProofs, int ReaderProcessStarts, int ActiveReaders, int WaitingReaders, int PayloadBytes,
    int PeakPayloadBytes, int PayloadEntries, int ReaderLimit, int PayloadByteLimit, int PayloadEntryLimit, int ProofReadAheadBytes);

public sealed partial class GitObjectSnapshotTree
{
    private readonly bool readDiagnostics;
    public bool ReadDiagnosticsEnabled => readDiagnostics;
    private long readRequests, requestedReadBytes, failedReadRequests, fullProofDecodedBytes, provedReadDecodedBytes;
    private long fullProofProcessingTicks, provedReadProcessingTicks, readerWaitCount, readerWaitTicks;
    private long capacityEvictedBytes, unusedReadAheadEvictedBytes;

    public GitReadObservation? ObserveReads()
    {
        if (!readDiagnostics) return null;
        lock (readGate)
            return new(Stopwatch.GetTimestamp(), Stopwatch.Frequency, DateTimeOffset.UtcNow,
                Interlocked.Read(ref readRequests), Interlocked.Read(ref requestedReadBytes), Interlocked.Read(ref failedReadRequests),
                Interlocked.Read(ref fullProofDecodedBytes), Interlocked.Read(ref provedReadDecodedBytes),
                Interlocked.Read(ref fullProofProcessingTicks), Interlocked.Read(ref provedReadProcessingTicks), readerWaitCount, readerWaitTicks,
                PayloadCacheHits, PayloadCacheMisses, capacityEvictedBytes, unusedReadAheadEvictedBytes,
                FullBlobProofs, ReaderProcessStarts, activeReads, waitingReads, payloadBytes, PeakChunkPayloadBytes,
                payloads.Count, MaxReaderSessions, payloadByteLimit, MaxPayloadEntries, proofReadAheadBytes);
    }
}
