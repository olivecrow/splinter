namespace Splinter.Core;

// A reusable validation input is distinct from final workspace collection.
// Its ranges still depend on this exact snapshot; it is not an independent result.
public sealed record ValidationCandidate(int Version, string Id, OverlayIdentity Identity,
    SnapshotDeltaSeal SourceSeal, SnapshotDeltaSeal Seal, DateTimeOffset CreatedAt,
    long CreationTicks, long StopwatchFrequency);

public sealed record ValidationViewSelection(string CandidatePath, string CacheDeltaPath,
    string[] CacheTopLevel);
