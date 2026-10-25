using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

// Test-only current-format encoded input. Production has no compression writer.
internal static class EncodedPayloadFixture
{
    internal static int Encode(this IWorkspacePayloadReader reader, ReadOnlySpan<byte> input, Span<byte> output)
    {
        int capacity = reader.GetMaxEncodedBytes(input.Length);
        if (output.Length < capacity || input.Overlaps(output)) throw new WorkspaceException("InvalidPayloadBuffer", "Fixture buffer.");
        if (reader.CodecId == "test-literal-v1")
        {
            BinaryPrimitives.WriteInt32LittleEndian(output, input.Length); input.CopyTo(output[4..]);
            SHA256.HashData(output[..(capacity - 32)], output.Slice(capacity - 32, 32)); return capacity;
        }
        byte[] compressed = new byte[input.Length];
        int count = 0;
        bool encoded = input.Length > 64 && BrotliEncoder.TryCompress(input, compressed, out count, quality: 1, window: 22) && count <= input.Length - 64;
        int stored = encoded ? count : input.Length;
        output[..16].Clear(); "SPB1"u8.CopyTo(output);
        BinaryPrimitives.WriteInt32LittleEndian(output[4..], input.Length);
        BinaryPrimitives.WriteInt32LittleEndian(output[8..], stored); output[12] = encoded ? (byte)1 : (byte)0;
        if (encoded) compressed.AsSpan(0, stored).CopyTo(output[16..]); else input.CopyTo(output[16..]);
        SHA256.HashData(output[..(16 + stored)], output.Slice(16 + stored, 32)); return 48 + stored;
    }
    internal static void CompressExistingJournal(string directory)
    {
        string path = Path.Combine(directory, "delta.journal"); byte[] input = File.ReadAllBytes(path);
        using var output = new MemoryStream(); var reader = new BrotliWorkspacePayloadReader();
        for (int cursor = 0; cursor < input.Length;)
        {
            byte[] header = input.AsSpan(cursor, 16).ToArray();
            int metadata = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4));
            int stored = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(8));
            int decoded = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(12));
            byte[] payload = input.AsSpan(cursor + 16 + metadata, stored).ToArray();
            if (stored == decoded && decoded > 112)
            {
                byte[] encoded = new byte[reader.GetMaxEncodedBytes(decoded)]; int length = reader.Encode(payload, encoded);
                if (length <= decoded - 64) payload = encoded[..length];
            }
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(8), payload.Length);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            hash.AppendData(header); hash.AppendData(input.AsSpan(cursor + 16, metadata)); hash.AppendData(payload);
            output.Write(header); output.Write(input.AsSpan(cursor + 16, metadata)); output.Write(payload); output.Write(hash.GetHashAndReset());
            cursor += 16 + metadata + stored + 32;
        }
        File.WriteAllBytes(path, output.ToArray());
    }
}
