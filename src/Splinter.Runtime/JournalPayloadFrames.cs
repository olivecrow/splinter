using System.Buffers;
using System.Buffers.Binary;
using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class JournalDeltaStore
{
    public const int FrameHeaderBytes = 16, FrameChecksumBytes = 32;
    private readonly BrotliWorkspacePayloadCodec payloadCodec = new();

    private static void WriteHeader(Span<byte> header, int metadata, WorkspaceJournalPayload payload)
    {
        "SJR2"u8.CopyTo(header);
        BinaryPrimitives.WriteInt32LittleEndian(header[4..], metadata);
        BinaryPrimitives.WriteInt32LittleEndian(header[8..], payload.StoredBytes);
        BinaryPrimitives.WriteInt32LittleEndian(header[12..], payload.DecodedBytes);
    }
    private (int Metadata, WorkspaceJournalPayload Payload) ReadHeader(ReadOnlySpan<byte> header)
    {
        if (!header[..4].SequenceEqual("SJR2"u8))
            throw new WorkspaceException("JournalFormatUnsupported", "Only the current SJR2 journal is supported; preserve older bytes without migration.");
        int metadata = BinaryPrimitives.ReadInt32LittleEndian(header[4..]);
        var payload = new WorkspaceJournalPayload(BinaryPrimitives.ReadInt32LittleEndian(header[8..]),
            BinaryPrimitives.ReadInt32LittleEndian(header[12..]));
        if (metadata is < 1 or > MaxMetadataBytes)
            throw new WorkspaceException("DeltaCorrupt", "Invalid journal metadata bound.");
        payload.RequireValid(Identity.MaxTransferBytes);
        return (metadata, payload);
    }

    // Raw is bare bytes protected by the outer frame checksum. Select the codec
    // only when its full 48-byte representation saves at least 64 bytes, so raw
    // incurs no duplicate checksum/codec overhead and compression cannot expand.
    private byte[]? EncodePayload(ReadOnlySpan<byte> decoded)
    {
        if (decoded.Length < BrotliWorkspacePayloadCodec.BlockOverheadBytes + 64) return null;
        return payloadCodec.EncodeJournalPayload(decoded);
    }
    private void ValidateEncoded(ReadOnlySpan<byte> encoded, WorkspaceJournalPayload payload)
    {
        if (encoded.Length < BrotliWorkspacePayloadCodec.BlockOverheadBytes ||
            BinaryPrimitives.ReadInt32LittleEndian(encoded[4..]) != payload.DecodedBytes)
            throw new WorkspaceException("DeltaCorrupt", "The block and frame decoded lengths differ.");
        payloadCodec.Read(encoded, 0, Span<byte>.Empty);
    }
    private void ReadPayload(long physical, WorkspaceJournalPayload payload, int offset, Span<byte> output)
    {
        payload.RequireFragment(offset, output.Length);
        if (!payload.Encoded) { ReadExactly(physical + offset, output); return; }
        byte[]? encoded = null;
        try
        {
            encoded = ReadEncoded(physical, payload);
            var bytes = encoded.AsSpan(0, payload.StoredBytes);
            if (decodedPayloadCache.Read(payloadCodec, bytes, offset, output) != output.Length)
                throw new WorkspaceException("DeltaCorrupt", "The decoded journal fragment is incomplete.");
        }
        catch { decodedPayloadCache.Clear(); throw; }
        finally { if (encoded is not null) ArrayPool<byte>.Shared.Return(encoded); }
    }
    private void ReadExtent(Extent extent, int relative, Span<byte> output) =>
        ReadPayload(extent.JournalOffset, extent.Payload, extent.BlockOffset + relative, output);

    private byte[] ReadEncoded(long physical, WorkspaceJournalPayload payload)
    {
        byte[] encoded = ArrayPool<byte>.Shared.Rent(payload.StoredBytes);
        try
        {
            var bytes = encoded.AsSpan(0, payload.StoredBytes); ReadExactly(physical, bytes);
            if (bytes.Length < BrotliWorkspacePayloadCodec.BlockOverheadBytes ||
                BinaryPrimitives.ReadInt32LittleEndian(bytes[4..]) != payload.DecodedBytes)
                throw new WorkspaceException("DeltaCorrupt", "The block and frame decoded lengths differ.");
            return encoded;
        }
        catch { ArrayPool<byte>.Shared.Return(encoded); throw; }
    }
    private void VisitPayload(long physical, WorkspaceJournalPayload payload,
        BrotliWorkspacePayloadCodec.DecodedChunkVisitor visit, ReadOnlySpan<byte> context = default)
    {
        byte[]? encoded = null;
        try
        {
            encoded = ReadEncoded(physical, payload);
            decodedPayloadCache.Visit(payloadCodec, encoded.AsSpan(0, payload.StoredBytes), visit, context);
        }
        catch { decodedPayloadCache.Clear(); throw; }
        finally { if (encoded is not null) ArrayPool<byte>.Shared.Return(encoded); }
    }
    private void VisitExtent(Extent extent, BrotliWorkspacePayloadCodec.DecodedChunkVisitor visit,
        ReadOnlySpan<byte> context = default)
    {
        if (extent.Payload.Encoded)
        {
            VisitPayload(extent.JournalOffset, extent.Payload, (at, chunk, input) =>
            {
                int start = Math.Max(at, extent.BlockOffset), end = Math.Min(at + chunk.Length, extent.BlockOffset + extent.Length);
                if (end > start) visit(start - extent.BlockOffset, chunk.Slice(start - at, end - start), input);
            }, context);
            return;
        }
        byte[] buffer = ArrayPool<byte>.Shared.Rent(Math.Min(65536, extent.Length));
        try
        {
            for (int at = 0; at < extent.Length;)
            {
                int count = Math.Min(buffer.Length, extent.Length - at);
                ReadExtent(extent, at, buffer.AsSpan(0, count)); visit(at, buffer.AsSpan(0, count), context); at += count;
            }
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }
}
