using System.Text.Json.Serialization;

namespace Splinter.Core;

// Private journal storage only. Immutable snapshot bytes and shared VSS are not
// charged here; frame bytes include retained mutation metadata and checksums.
public sealed record ValidationCacheUsage(long JournalBytes, long RetainedPayloadBytes,
    long LivePrivateBytes, long ObsoletePayloadBytes, long FrameBytes)
{
    public long LiveStoredPayloadBytes { get; init; }
    public long RetainedLogicalPayloadBytes { get; init; }
}

// Adapter-qualified, bounded private files already excluded from the immutable
// snapshot. This is evidence for candidate projection, never a deletion grant.
public sealed record CandidateManagedFile(string Path, int Length, string ContentHash);
public sealed record CandidateSourceProjection(int Version, string OwnerId, SnapshotReference Snapshot,
    string EvidenceHash, CandidateManagedFile[] Files);

// The first publication freezes the source generation. A retry never selects a
// later worker generation, even after the original response was lost.
public sealed record CandidateExportRequest(int Version, string RequestKey);
public sealed record CandidateExportIntent(int Version, CandidateExportRequest Request,
    string SourceDirectory, string Destination, OverlayIdentity Identity, string[] ForbiddenTopLevel,
    SnapshotDeltaSeal SourceSeal, string StateHash, long JournalBytes, DateTimeOffset StartedAt,
    [property: JsonRequired, JsonIgnore(Condition = JsonIgnoreCondition.Never)] CandidateSourceProjection? SourceProjection);

// A reusable validation input is distinct from final workspace collection.
// Its ranges still depend on this exact snapshot; it is not an independent result.
public sealed record ValidationCandidate(int Version, string Id, OverlayIdentity Identity,
    SnapshotDeltaSeal SourceSeal, SnapshotDeltaSeal Seal, DateTimeOffset CreatedAt,
    long CreationTicks, long StopwatchFrequency,
    [property: JsonRequired, JsonIgnore(Condition = JsonIgnoreCondition.Never)] CandidateSourceProjection? SourceProjection);

public sealed record ValidationViewSelection(string CandidatePath, string CacheDeltaPath,
    string[] CacheTopLevel, OverlayIdentity? CacheIdentity = null,
    [property: JsonRequired] string CacheBasis = "empty");
