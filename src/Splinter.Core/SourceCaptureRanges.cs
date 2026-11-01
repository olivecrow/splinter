namespace Splinter.Core;

// A fixed capture owns these byte intervals independently of later edits.
// Backing identities are opaque platform values. Only raw, append/pin-protected
// ranges may expose a physical backing; encoded ranges use the tree's Read.
public sealed record CapturedImmutableRangeBacking(string Path, string FileIdentity, long Offset,
    long MinimumLength, string CaptureId, string CaptureDigest, string? ExpectedBackingHash = null);
public sealed record CapturedPrivateSourceRange(long Offset, int Length, string Identity,
    CapturedImmutableRangeBacking? Backing = null, string? BlockIdentity = null, int BlockOffset = 0,
    string? PreservedHash = null, bool PreservedRaw = false, int PreservedOffset = 0, int PreservedLength = 0);
public sealed record CapturedFileRangeLayout(string? BasisPath, long BasisVisibleLength,
    IEnumerable<CapturedPrivateSourceRange> PrivateRanges, Guid? FileId = null);
public interface ISourceCaptureRanges
{
    SourceCaptureIdentity CaptureIdentity { get; }
    CapturedFileRangeLayout? GetFileRanges(string relativePath);
}

public sealed record SourceRangeAdoption(int Version, SourceCaptureIdentity CaptureIdentity,
    string Directory, MetadataTreeRoot Root, long Count, string Digest);
public sealed record CapturedRangeAdoptionEntry(Guid FileId, long Offset, int Length,
    string BlockIdentity, int BlockOffset, string ChunkHash, int ChunkOffset, bool RawRange, int ChunkLength = 0)
{
    public int ContentLength => ChunkLength == 0 ? Length : ChunkLength;
}
public interface ICapturedContentReader
{
    long GetLength(string hash, bool raw);
    int Read(string hash, bool raw, long offset, Span<byte> destination);
    void ReleaseIdleReaders() { }
}

public interface IRetainedResultMaintenance
{
    Task MaintainRetainedResultsAsync(WorkspaceRecord workspace, string control, CancellationToken cancellationToken = default);
}
