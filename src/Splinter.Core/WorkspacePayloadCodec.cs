namespace Splinter.Core;

// A self-contained representation of one bounded changed range. The codec has
// no file, snapshot, journal, VCS or application access. Journal framing,
// durability and logical fragment references belong to its caller.
public interface IWorkspacePayloadCodec
{
    string CodecId { get; }
    int MaxDecodedBytes { get; }

    // Includes representation overhead and enough capacity for a raw block.
    int GetMaxEncodedBytes(int decodedBytes);

    // Caller owns both buffers. Inputs/outputs cannot overlap. Return only the
    // initialized prefix; bytes beyond that prefix remain untouched.
    int Encode(ReadOnlySpan<byte> decoded, Span<byte> destination);

    // Validate the entire block before publishing bytes, even for EOF/empty
    // reads. A failed read leaves destination unchanged. Offset >= length is
    // EOF. Partial reads may decode one bounded block, never an entire file.
    int Read(ReadOnlySpan<byte> encoded, int offset, Span<byte> destination);
}
