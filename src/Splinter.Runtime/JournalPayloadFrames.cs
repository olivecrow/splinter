using System.Buffers;
using System.Buffers.Binary;
using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class JournalDeltaStore
{
    public const int FrameHeaderBytes = 16, FrameChecksumBytes = 32;
    private delegate void PayloadChunkVisitor(int offset, ReadOnlySpan<byte> chunk, ReadOnlySpan<byte> context);

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
        if (metadata < 1 || metadata > MutationMetadataBytes)
            throw new WorkspaceException("DeltaCorrupt", "Invalid journal metadata bound.");
        payload.RequireValid(Identity.MaxTransferBytes);
        return (metadata, payload);
    }

    private void ReadPayload(long physical, WorkspaceJournalPayload payload, int offset, Span<byte> output)
    {
        payload.RequireFragment(offset, output.Length);
        ReadExactly(physical + offset, output);
    }
    private void ReadExtent(Extent extent, int relative, Span<byte> output)
    {
        int offset = checked(extent.BlockOffset + relative);
        extent.Payload.RequireFragment(offset, output.Length);
        ReadExtentBytes(extent, extent.JournalOffset + offset, output);
    }
    private void VisitExtent(Extent extent, PayloadChunkVisitor visit, ReadOnlySpan<byte> context = default)
    {
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
