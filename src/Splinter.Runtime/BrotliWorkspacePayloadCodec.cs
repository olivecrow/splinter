using System.Buffers;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using Splinter.Core;

namespace Splinter.Runtime;

// An independent block codec used by the bounded journal payload caller.
public sealed class BrotliWorkspacePayloadCodec : IWorkspacePayloadCodec
{
    public const int MaximumBlockBytes = 1024 * 1024;
    public const int BlockOverheadBytes = 48;
    private const int PrefixBytes = 16, ChecksumBytes = 32, MinimumPayloadSavingBytes = 64;
    private const int DecodeChunkBytes = 16384;
    private readonly ArrayPool<byte> scratchPool;
    public string CodecId => "splinter-range-brotli-v1";
    public int MaxDecodedBytes { get; }

    public BrotliWorkspacePayloadCodec(int maxDecodedBytes = MaximumBlockBytes, ArrayPool<byte>? scratchPool = null)
    {
        if (maxDecodedBytes is < 1 or > MaximumBlockBytes)
            throw new ArgumentOutOfRangeException(nameof(maxDecodedBytes));
        MaxDecodedBytes = maxDecodedBytes;
        this.scratchPool = scratchPool ?? ArrayPool<byte>.Shared;
    }

    public int GetMaxEncodedBytes(int decodedBytes)
    {
        if (decodedBytes < 0 || decodedBytes > MaxDecodedBytes)
            throw new WorkspaceException("PayloadBlockLimit", "One decoded range exceeds the codec bound.");
        return BlockOverheadBytes + decodedBytes;
    }

    public int Encode(ReadOnlySpan<byte> decoded, Span<byte> destination)
    {
        int maximum = GetMaxEncodedBytes(decoded.Length);
        if (destination.Length < maximum)
            throw new WorkspaceException("PayloadBufferTooSmall", "Provide capacity for the bounded raw representation.");
        RequireSeparate(decoded, destination);
        Span<byte> payload = destination.Slice(PrefixBytes, decoded.Length);
        int stored = decoded.Length;
        bool compressed = false;
        if (decoded.Length > MinimumPayloadSavingBytes)
        {
            byte[] scratch = scratchPool.Rent(decoded.Length);
            try
            {
                compressed = BrotliEncoder.TryCompress(decoded, scratch.AsSpan(0, decoded.Length), out stored, quality: 1, window: 22) &&
                    stored <= decoded.Length - MinimumPayloadSavingBytes;
                if (compressed) scratch.AsSpan(0, stored).CopyTo(payload);
            }
            finally { scratchPool.Return(scratch); }
        }
        if (!compressed) { decoded.CopyTo(payload); stored = decoded.Length; }
        return CompleteBlock(destination, decoded.Length, stored, compressed);
    }

    // The journal keeps raw bytes bare. Rent only enough compressed capacity
    // to meet its full-block saving threshold, then allocate the selected result.
    internal byte[]? EncodeJournalPayload(ReadOnlySpan<byte> decoded)
    {
        GetMaxEncodedBytes(decoded.Length);
        int capacity = decoded.Length - BlockOverheadBytes - MinimumPayloadSavingBytes;
        if (capacity <= 0) return null;
        byte[] scratch = scratchPool.Rent(capacity);
        try
        {
            if (!BrotliEncoder.TryCompress(decoded, scratch.AsSpan(0, capacity), out int stored, quality: 1, window: 22))
                return null;
            byte[] result = new byte[BlockOverheadBytes + stored];
            scratch.AsSpan(0, stored).CopyTo(result.AsSpan(PrefixBytes));
            CompleteBlock(result, decoded.Length, stored, true);
            return result;
        }
        finally { scratchPool.Return(scratch); }
    }

    private static int CompleteBlock(Span<byte> destination, int decoded, int stored, bool compressed)
    {
        Span<byte> prefix = destination[..PrefixBytes];
        prefix.Clear(); "SPB1"u8.CopyTo(prefix);
        BinaryPrimitives.WriteInt32LittleEndian(prefix[4..], decoded);
        BinaryPrimitives.WriteInt32LittleEndian(prefix[8..], stored);
        prefix[12] = compressed ? (byte)1 : (byte)0;
        SHA256.HashData(destination[..(PrefixBytes + stored)], destination.Slice(PrefixBytes + stored, ChecksumBytes));
        return BlockOverheadBytes + stored;
    }

    public int Read(ReadOnlySpan<byte> encoded, int offset, Span<byte> destination)
    {
        if (offset < 0) throw new WorkspaceException("InvalidPayloadRange", "A range offset cannot be negative.");
        RequireSeparate(encoded, destination);
        if (encoded.Length < BlockOverheadBytes || encoded.Length > GetMaxEncodedBytes(MaxDecodedBytes)) throw Corrupt();
        if (!encoded[..4].SequenceEqual("SPB1"u8) || encoded[12] > 1)
            throw new WorkspaceException("PayloadBlockUnsupported", "Preserve a block with another codec version or encoding.");
        if (encoded[13] != 0 || encoded[14] != 0 || encoded[15] != 0) throw Corrupt();
        int decoded = BinaryPrimitives.ReadInt32LittleEndian(encoded[4..]);
        int stored = BinaryPrimitives.ReadInt32LittleEndian(encoded[8..]);
        bool compressed = encoded[12] == 1;
        if (decoded < 0 || decoded > MaxDecodedBytes || stored < 0 || stored > decoded ||
            encoded.Length != BlockOverheadBytes + stored ||
            (compressed ? stored == 0 || stored > decoded - MinimumPayloadSavingBytes : stored != decoded)) throw Corrupt();
        Span<byte> checksum = stackalloc byte[ChecksumBytes];
        SHA256.HashData(encoded[..(PrefixBytes + stored)], checksum);
        if (!CryptographicOperations.FixedTimeEquals(checksum, encoded[(PrefixBytes + stored)..])) throw Corrupt();
        int count = offset >= decoded ? 0 : Math.Min(destination.Length, decoded - offset);
        ReadOnlySpan<byte> payload = encoded.Slice(PrefixBytes, stored);
        if (!compressed)
        {
            if (count > 0) payload.Slice(offset, count).CopyTo(destination);
            return count;
        }
        // Small/empty reads still validate the entire stream, but retain only
        // a chunk and the requested bytes until validation has succeeded.
        if (decoded > DecodeChunkBytes && count <= decoded / 2)
            return ReadChunked(payload, decoded, offset, destination, count);
        byte[] scratch = scratchPool.Rent(decoded);
        try
        {
            using var decoder = new BrotliDecoder();
            if (decoder.Decompress(payload, scratch.AsSpan(0, decoded), out int consumed, out int written) != OperationStatus.Done ||
                consumed != stored || written != decoded) throw Corrupt();
            if (count > 0) scratch.AsSpan(offset, count).CopyTo(destination);
            return count;
        }
        finally { scratchPool.Return(scratch); }
    }

    private int ReadChunked(ReadOnlySpan<byte> payload, int decoded, int offset, Span<byte> destination, int count)
    {
        byte[] scratch = scratchPool.Rent(DecodeChunkBytes + count);
        try
        {
            using var decoder = new BrotliDecoder();
            int consumed = 0, written = 0;
            Span<byte> staged = scratch.AsSpan(DecodeChunkBytes, count);
            while (true)
            {
                // One extra output byte detects expansion beyond the declared
                // bound, including a decoder which needs a final empty input.
                Span<byte> chunk = scratch.AsSpan(0, Math.Min(DecodeChunkBytes, decoded - written + 1));
                OperationStatus status = decoder.Decompress(payload[consumed..], chunk, out int used, out int produced);
                consumed += used;
                if (produced > decoded - written) throw Corrupt();
                if (count > 0)
                {
                    int start = Math.Max(written, offset), end = Math.Min(written + produced, offset + count);
                    if (end > start) chunk.Slice(start - written, end - start).CopyTo(staged[(start - offset)..]);
                }
                written += produced;
                if (status == OperationStatus.Done)
                {
                    if (consumed != payload.Length || written != decoded) throw Corrupt();
                    staged.CopyTo(destination);
                    return count;
                }
                if (status != OperationStatus.DestinationTooSmall || (used == 0 && produced == 0)) throw Corrupt();
            }
        }
        finally { scratchPool.Return(scratch); }
    }

    private static void RequireSeparate(ReadOnlySpan<byte> input, Span<byte> output)
    {
        if (input.Overlaps(output))
            throw new WorkspaceException("InvalidPayloadBuffer", "The encoded and decoded buffers must not overlap.");
    }

    private static WorkspaceException Corrupt() =>
        new("PayloadBlockCorrupt", "Preserve an invalid, incomplete or checksum-mismatched payload block.");
}
