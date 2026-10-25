namespace Splinter.Core;

// Validates existing encoded current-format changed ranges. The reader has
// no file, snapshot, journal, VCS or application access. Journal framing,
// durability and logical fragment references belong to its caller.
public interface IWorkspacePayloadReader
{
    string CodecId { get; }
    int MaxDecodedBytes { get; }

    // Includes representation overhead and enough capacity for a raw block.
    int GetMaxEncodedBytes(int decodedBytes);

    // Validate the entire block before publishing bytes, even for EOF/empty
    // reads. A failed read leaves destination unchanged. Offset >= length is
    // EOF. Partial reads may decode one bounded block, never an entire file.
    int Read(ReadOnlySpan<byte> encoded, int offset, Span<byte> destination);
}
