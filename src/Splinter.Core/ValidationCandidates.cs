using System.Text.Json.Serialization;

namespace Splinter.Core;

// Adapter-qualified, bounded private files already excluded from the immutable
// snapshot. This is evidence for candidate projection, never a deletion grant.
public sealed record CandidateManagedFile(string Path, int Length, string ContentHash);
public sealed record CandidateSourceProjection(int Version, string OwnerId, SnapshotReference Snapshot,
    string EvidenceHash, CandidateManagedFile[] Files);

// A reusable validation input is distinct from final workspace collection.
// Its ranges still depend on this exact snapshot; it is not an independent result.
public sealed record ValidationCandidate(int Version, string Id, OverlayIdentity Identity,
    SnapshotDeltaSeal SourceSeal, SnapshotDeltaSeal Seal, DateTimeOffset CreatedAt,
    long CreationTicks, long StopwatchFrequency,
    [property: JsonRequired, JsonIgnore(Condition = JsonIgnoreCondition.Never)] CandidateSourceProjection? SourceProjection);

public sealed record ValidationViewSelection(string CandidatePath, string CacheDeltaPath,
    string[] CacheTopLevel);
