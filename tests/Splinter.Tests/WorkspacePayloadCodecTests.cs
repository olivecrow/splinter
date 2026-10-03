using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class WorkspacePayloadCodecTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CommonContractRoundtripsEmptySmallMaximumAndIndependentPartialReads(bool runtime)
    {
        IWorkspacePayloadCodec codec = runtime ? new BrotliWorkspacePayloadCodec(65536) : new LiteralFake(65536);
        foreach (int size in new[] { 0, 1, 63, 4096, codec.MaxDecodedBytes })
        {
            byte[] input = Enumerable.Range(0, size).Select(i => (byte)(i * 31 + i / 17)).ToArray();
            byte[] before = input.ToArray();
            byte[] encoded = Enumerable.Repeat((byte)0xcc, codec.GetMaxEncodedBytes(size) + 19).ToArray();
            int length = codec.Encode(input, encoded);
            Assert.InRange(length, 1, codec.GetMaxEncodedBytes(size));
            Assert.Equal(before, input);
            Assert.All(encoded.Skip(length), x => Assert.Equal(0xcc, x));
            byte[] result = Enumerable.Repeat((byte)0xee, size + 13).ToArray();
            Assert.Equal(size, codec.Read(encoded.AsSpan(0, length), 0, result));
            Assert.Equal(input, result[..size]); Assert.All(result.Skip(size), x => Assert.Equal(0xee, x));
            foreach (int offset in new[] { 0, Math.Max(0, size - 3), size, int.MaxValue })
            {
                byte[] part = Enumerable.Repeat((byte)0x77, 7).ToArray();
                int count = codec.Read(encoded.AsSpan(0, length), offset, part);
                int expected = offset >= size ? 0 : Math.Min(7, size - offset);
                Assert.Equal(expected, count);
                if (count > 0) Assert.Equal(input.AsSpan(offset, count).ToArray(), part[..count]);
                Assert.All(part.Skip(count), x => Assert.Equal(0x77, x));
            }
            Assert.Equal(0, codec.Read(encoded.AsSpan(0, length), 0, []));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CommonContractRejectsLimitsCorruptionTruncationAndOverlapWithoutPublishing(bool runtime)
    {
        IWorkspacePayloadCodec codec = runtime ? new BrotliWorkspacePayloadCodec(4096) : new LiteralFake(4096);
        Assert.Throws<WorkspaceException>(() => codec.GetMaxEncodedBytes(-1));
        Assert.Throws<WorkspaceException>(() => codec.GetMaxEncodedBytes(4097));
        byte[] destination = Enumerable.Repeat((byte)0x77, codec.GetMaxEncodedBytes(4096)).ToArray();
        Assert.Throws<WorkspaceException>(() => codec.Encode(new byte[4097], destination));
        Assert.All(destination, x => Assert.Equal(0x77, x));
        Assert.Throws<WorkspaceException>(() => codec.Encode(new byte[4096], destination.AsSpan(0, destination.Length - 1)));
        Assert.Throws<WorkspaceException>(() => codec.Encode(destination.AsSpan(0, 100), destination));
        int length = codec.Encode(new byte[4096], destination);
        byte[] valid = destination[..length];
        var corrupted = valid.ToArray(); corrupted[^1] ^= 1;
        foreach (byte[] block in new[] { corrupted, valid[..^1], valid.Concat(new byte[1]).ToArray() })
        foreach (int offset in new[] { 0, int.MaxValue })
        {
            byte[] output = Enumerable.Repeat((byte)0x99, 17).ToArray();
            Assert.Throws<WorkspaceException>(() => codec.Read(block, offset, output));
            Assert.All(output, x => Assert.Equal(0x99, x));
            Assert.Throws<WorkspaceException>(() => codec.Read(block, offset, []));
        }
        Assert.Throws<WorkspaceException>(() => codec.Read(valid, -1, new byte[1]));
        Assert.Throws<WorkspaceException>(() => codec.Read(valid, 0, valid));
    }

    [Fact]
    public void IncompressibleAndTinyBlocksUseRawWithoutExpandingTheRawRepresentation()
    {
        var pool = new TrackingPool(); var codec = new BrotliWorkspacePayloadCodec(65536, pool);
        var random = new Random(1936);
        foreach (int size in new[] { 0, 1, 64, 4096, 65536 })
        {
            byte[] input = new byte[size]; random.NextBytes(input);
            byte[] encoded = new byte[codec.GetMaxEncodedBytes(size)];
            Assert.Equal(encoded.Length, codec.Encode(input, encoded));
            Assert.Equal(0, encoded[12]);
            int beforeRead = pool.Rents;
            byte[] output = new byte[size]; Assert.Equal(size, codec.Read(encoded, 0, output)); Assert.Equal(input, output);
            Assert.Equal(beforeRead, pool.Rents);
        }
        Assert.Equal(pool.Rents, pool.Returns);
    }

    [Fact]
    public void RepeatedMaximumRangeCompressesAndSmallReadUsesChunkAndStagingInsteadOfFullDecodedScratch()
    {
        var pool = new TrackingPool(); var codec = new BrotliWorkspacePayloadCodec(scratchPool: pool);
        byte[] input = new byte[codec.MaxDecodedBytes]; Array.Fill(input, (byte)19);
        byte[] encoded = new byte[codec.GetMaxEncodedBytes(input.Length)];
        int count = codec.Encode(input, encoded); Assert.True(count < input.Length / 100);
        Assert.Equal(1, encoded[12]);
        Assert.Equal(1, pool.Rents); Assert.Equal(1, pool.Returns);
        pool.MaxRequest = 0;
        byte[] output = new byte[7]; Assert.Equal(7, codec.Read(encoded.AsSpan(0, count), 173, output));
        Assert.All(output, x => Assert.Equal(19, x));
        Assert.Equal(16384 + 7, pool.MaxRequest); Assert.Equal(2, pool.Rents); Assert.Equal(2, pool.Returns);
        pool.MaxRequest = 0;
        Assert.Equal(0, codec.Read(encoded.AsSpan(0, count), int.MaxValue, []));
        Assert.Equal(3, pool.Returns);
        Assert.Equal(16384, pool.MaxRequest);
    }

    [Theory]
    [InlineData("trailing")]
    [InlineData("declared-short")]
    [InlineData("declared-long")]
    [InlineData("truncated-stream")]
    public void ChecksummedButInvalidCompressedStreamsAreRejectedBeforePublishingAndReturnScratch(string damage)
    {
        var pool = new TrackingPool(); var codec = new BrotliWorkspacePayloadCodec(scratchPool: pool);
        byte[] encoded = new byte[codec.GetMaxEncodedBytes(65536)]; int length = codec.Encode(new byte[65536], encoded);
        byte[] bad = encoded[..length];
        if (damage == "trailing")
        {
            int stored = BinaryPrimitives.ReadInt32LittleEndian(bad.AsSpan(8));
            bad = new byte[length + 1]; encoded.AsSpan(0, 16 + stored).CopyTo(bad);
            bad[16 + stored] = 0x55; BinaryPrimitives.WriteInt32LittleEndian(bad.AsSpan(8), stored + 1);
        }
        else if (damage == "truncated-stream")
        {
            int stored = BinaryPrimitives.ReadInt32LittleEndian(bad.AsSpan(8));
            bad = bad[..^1]; BinaryPrimitives.WriteInt32LittleEndian(bad.AsSpan(8), stored - 1);
        }
        else BinaryPrimitives.WriteInt32LittleEndian(bad.AsSpan(4), damage == "declared-short" ? 4096 : 131072);
        Resign(bad);
        byte[] output = Enumerable.Repeat((byte)0x77, 19).ToArray();
        Assert.Equal("PayloadBlockCorrupt", Assert.Throws<WorkspaceException>(() => codec.Read(bad, 0, output)).Code);
        Assert.All(output, x => Assert.Equal(0x77, x));
        Assert.Equal(2, pool.Rents); Assert.Equal(pool.Rents, pool.Returns); Assert.InRange(pool.MaxRequest, 1, codec.MaxDecodedBytes);
    }

    [Theory]
    [InlineData("negative-decoded")]
    [InlineData("oversized-decoded")]
    [InlineData("negative-stored")]
    [InlineData("reserved")]
    [InlineData("unknown-version")]
    [InlineData("unknown-kind")]
    public void InvalidHeadersAreRejectedBeforeRentingEvenForEmptyRead(string damage)
    {
        var pool = new TrackingPool(); var codec = new BrotliWorkspacePayloadCodec(4096, pool);
        byte[] encoded = new byte[codec.GetMaxEncodedBytes(4096)]; int size = codec.Encode(new byte[4096], encoded);
        encoded = encoded[..size];
        if (damage == "negative-decoded") BinaryPrimitives.WriteInt32LittleEndian(encoded.AsSpan(4), -1);
        if (damage == "oversized-decoded") BinaryPrimitives.WriteInt32LittleEndian(encoded.AsSpan(4), int.MaxValue);
        if (damage == "negative-stored") BinaryPrimitives.WriteInt32LittleEndian(encoded.AsSpan(8), -1);
        if (damage == "reserved") encoded[13] = 1;
        if (damage == "unknown-version") encoded[3] = (byte)'2';
        if (damage == "unknown-kind") encoded[12] = 2;
        Resign(encoded);
        Assert.Throws<WorkspaceException>(() => codec.Read(encoded, 0, [])); Assert.Equal(1, pool.Rents); Assert.Equal(pool.Rents, pool.Returns);
    }

    [Fact]
    public async Task OneCodecKeepsConcurrentCallersBuffersAndContentIndependent()
    {
        var codec = new BrotliWorkspacePayloadCodec(65536);
        await Task.WhenAll(Enumerable.Range(0, 8).Select(worker => Task.Run(() =>
        {
            byte[] input = Enumerable.Repeat((byte)(worker + 1), 65536).ToArray();
            byte[] encoded = new byte[codec.GetMaxEncodedBytes(input.Length)], output = new byte[31];
            for (int pass = 0; pass < 16; pass++)
            {
                input[worker * 71] = (byte)(worker + pass + 1);
                int length = codec.Encode(input, encoded);
                Assert.Equal(output.Length, codec.Read(encoded.AsSpan(0, length), worker * 71, output));
                Assert.Equal(input.AsSpan(worker * 71, output.Length).ToArray(), output);
            }
        }, TestContext.Current.CancellationToken)));
    }

    [Fact]
    public void RepeatedReadsDoNotRetainDecodedBlocksOrGrowManagedAllocationWithCumulativeInput()
    {
        var pool = new TrackingPool(); var codec = new BrotliWorkspacePayloadCodec(65536, pool);
        byte[] encoded = new byte[codec.GetMaxEncodedBytes(65536)]; int length = codec.Encode(new byte[65536], encoded);
        byte[] output = new byte[1];
        for (int i = 0; i < 16; i++) codec.Read(encoded.AsSpan(0, length), i, output);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 256; i++) codec.Read(encoded.AsSpan(0, length), i, output);
        Assert.InRange(GC.GetAllocatedBytesForCurrentThread() - before, 0, 32768);
        Assert.Equal(273, pool.Rents); Assert.Equal(pool.Rents, pool.Returns);
    }

    [Theory]
    [InlineData(16381, 7)]
    [InlineData(65531, 31)]
    [InlineData(1048573, 19)]
    [InlineData(1048576, 7)]
    [InlineData(int.MaxValue, 19)]
    public void MaximumChunkedReadsCrossBoundariesPublishOnlyTheRequestedPrefixAndReturnOneLease(int offset, int capacity)
    {
        var pool = new TrackingPool(); var codec = new BrotliWorkspacePayloadCodec(scratchPool: pool);
        byte[] input = Enumerable.Range(0, codec.MaxDecodedBytes).Select(i => (byte)(i / 71)).ToArray();
        byte[] encoded = new byte[codec.GetMaxEncodedBytes(input.Length)]; int stored = codec.Encode(input, encoded);
        Assert.Equal(1, encoded[12]); pool.MaxRequest = 0;
        byte[] output = Enumerable.Repeat((byte)0x77, capacity).ToArray();
        int count = Math.Max(0, offset >= input.Length ? 0 : Math.Min(capacity, input.Length - offset));
        Assert.Equal(count, codec.Read(encoded.AsSpan(0, stored), offset, output));
        if (count > 0) Assert.Equal(input.AsSpan(offset, count).ToArray(), output[..count]);
        Assert.All(output.Skip(count), b => Assert.Equal(0x77, b));
        Assert.Equal(16384 + count, pool.MaxRequest); Assert.Equal(2, pool.Rents); Assert.Equal(pool.Rents, pool.Returns);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(16383)]
    [InlineData(1048576)]
    [InlineData(int.MaxValue)]
    public void RechecksummedDamageAfterRequestedBytesNeverPublishesStagedPrefixOrLeaksScratch(int offset)
    {
        var pool = new TrackingPool(); var codec = new BrotliWorkspacePayloadCodec(scratchPool: pool);
        byte[] encoded = new byte[codec.GetMaxEncodedBytes(codec.MaxDecodedBytes)];
        int length = codec.Encode(new byte[codec.MaxDecodedBytes], encoded);
        byte[] invalid = encoded[..(length + 1)];
        int payload = BinaryPrimitives.ReadInt32LittleEndian(invalid.AsSpan(8));
        invalid[16 + payload] = 0x55;
        BinaryPrimitives.WriteInt32LittleEndian(invalid.AsSpan(8), payload + 1); Resign(invalid);
        byte[] output = Enumerable.Repeat((byte)0x77, 31).ToArray(); pool.MaxRequest = 0;
        Assert.Equal("PayloadBlockCorrupt", Assert.Throws<WorkspaceException>(() => codec.Read(invalid, offset, output)).Code);
        Assert.All(output, b => Assert.Equal(0x77, b));
        Assert.InRange(pool.MaxRequest, 16384, 16384 + 31); Assert.Equal(pool.Rents, pool.Returns);
        Assert.Throws<WorkspaceException>(() => codec.Read(invalid, offset, [])); Assert.Equal(pool.Rents, pool.Returns);
    }

    private static void Resign(byte[] block) => SHA256.HashData(block.AsSpan(0, block.Length - 32), block.AsSpan(block.Length - 32));

    private sealed class TrackingPool : ArrayPool<byte>
    {
        private byte[]? held;
        private bool leased;
        public int Rents, Returns, MaxRequest;
        public override byte[] Rent(int minimumLength)
        {
            Assert.False(leased); leased = true; Rents++; MaxRequest = Math.Max(MaxRequest, minimumLength);
            if (held is null || held.Length < minimumLength) held = new byte[minimumLength];
            return held;
        }
        public override void Return(byte[] array, bool clearArray = false)
        { Assert.True(leased); Assert.Same(held, array); leased = false; Returns++; }
    }

    // A separate literal format establishes the common API's content, bounds
    // and publish-on-success contract independently of Brotli or its header.
    private sealed class LiteralFake(int maximum) : IWorkspacePayloadCodec
    {
        public string CodecId => "test-literal-v1";
        public int MaxDecodedBytes => maximum;
        public int GetMaxEncodedBytes(int decodedBytes)
        {
            if (decodedBytes < 0 || decodedBytes > maximum) throw new WorkspaceException("PayloadBlockLimit", "Bound.");
            return 36 + decodedBytes;
        }
        public int Encode(ReadOnlySpan<byte> decoded, Span<byte> destination)
        {
            int length = GetMaxEncodedBytes(decoded.Length);
            if (destination.Length < length || decoded.Overlaps(destination)) throw new WorkspaceException("InvalidPayloadBuffer", "Buffer.");
            BinaryPrimitives.WriteInt32LittleEndian(destination, decoded.Length); decoded.CopyTo(destination[4..]);
            SHA256.HashData(destination[..(length - 32)], destination.Slice(length - 32, 32)); return length;
        }
        public int Read(ReadOnlySpan<byte> encoded, int offset, Span<byte> destination)
        {
            if (offset < 0 || encoded.Overlaps(destination) || encoded.Length < 36 || encoded.Length > 36 + maximum) throw Bad();
            int length = BinaryPrimitives.ReadInt32LittleEndian(encoded);
            if (length < 0 || length > maximum || encoded.Length != 36 + length) throw Bad();
            Span<byte> hash = stackalloc byte[32]; SHA256.HashData(encoded[..^32], hash);
            if (!hash.SequenceEqual(encoded[^32..])) throw Bad();
            int count = offset >= length ? 0 : Math.Min(destination.Length, length - offset);
            if (count > 0) encoded.Slice(4 + offset, count).CopyTo(destination); return count;
        }
        private static WorkspaceException Bad() => new("PayloadBlockCorrupt", "Invalid literal block.");
    }
}
