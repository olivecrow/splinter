using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class JournalDecodedPayloadCacheTests
{
    [Theory]
    [InlineData(0)] [InlineData(131328)]
    public void PartialReadsMatchAndRevalidateCurrentBytesBeforeEveryHit(long budget)
    {
        var codec = new BrotliWorkspacePayloadCodec(); var cache = new JournalDecodedPayloadCache(budget);
        byte[] input = Payload(65536, 17), encoded = Encode(codec, input), output = new byte[97];
        for (int pass = 0; pass < 4; pass++)
        {
            int offset = 16383 + pass;
            Assert.Equal(output.Length, cache.Read(codec, encoded, offset, output));
            Assert.Equal(input.AsSpan(offset, output.Length).ToArray(), output);
        }
        Assert.Equal(budget == 0 ? 4 : 1, cache.Decodes);
        Assert.Equal(budget == 0 ? 0 : 3, cache.Hits);
        // Same address, length and cached checksum: current encoded bytes must
        // still be read and hashed; stale RAM cannot hide corruption.
        encoded[16] ^= 1; Array.Fill(output, (byte)0x77);
        Assert.Equal("PayloadBlockCorrupt", Assert.Throws<WorkspaceException>(() => cache.Read(codec, encoded, 0, output)).Code);
        Assert.All(output, value => Assert.Equal(0x77, value));
        Assert.Equal(0, cache.AccountedBytes); Assert.Equal(0, cache.EntryCount);
    }

    [Fact]
    public void SameBufferWithNewValidContentsCannotReturnPriorDecodedBytes()
    {
        var codec = new BrotliWorkspacePayloadCodec(); var cache = new JournalDecodedPayloadCache();
        byte[] first = Encode(codec, new byte[65536]), second = Encode(codec, Enumerable.Repeat((byte)17, 65536).ToArray());
        byte[] output = new byte[19];
        cache.Read(codec, first, 71, output); Assert.All(output, value => Assert.Equal(0, value));
        cache.Read(codec, second, 71, output); Assert.All(output, value => Assert.Equal(17, value));
        cache.Read(codec, first, 71, output); Assert.All(output, value => Assert.Equal(0, value));
        Assert.Equal(2, cache.Decodes); Assert.Equal(1, cache.Hits);
    }

    [Fact]
    public void LiveCachedReadStillReadsAndRejectsCorruptedJournalPayload()
    {
        using var directory = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        using var store = new JournalDeltaStore(directory.Path, Identity(tree), tree, optimizeJournal: false);
        Guid id = store.Create("file", false, 32).Id; store.Write(id, 0, Payload(65536, 17));
        store.Read(id, 0, new byte[17]); store.Read(id, 0, new byte[17]);
        Assert.Equal(1, store.DecodedPayloadCache.Hits);
        // Inject damage through the owned fixture handle: an external writer
        // would correctly be blocked by the journal's sharing exclusion.
        var journal = (FileStream)typeof(JournalDeltaStore).GetField("journal", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(store)!;
        byte[] bytes = new byte[checked((int)journal.Length)];
        Assert.Equal(bytes.Length, RandomAccess.Read(journal.SafeFileHandle, bytes, 0));
        int createEnd = JournalDeltaStore.FrameHeaderBytes + BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4)) + 32;
        int block = createEnd + JournalDeltaStore.FrameHeaderBytes + BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(createEnd + 4));
        bytes[block + 16] ^= 1;
        RandomAccess.Write(journal.SafeFileHandle, bytes.AsSpan(block + 16, 1), block + 16);
        Assert.Equal("PayloadBlockCorrupt", Assert.Throws<WorkspaceException>(() => store.Read(id, 0, new byte[17])).Code);
        Assert.Equal(0, store.DecodedPayloadCache.AccountedBytes);
    }

    [Fact]
    public void BudgetIncludesMissInFlightAndEvictsLeastRecentlyUsedBeforeAllocation()
    {
        const int size = 16384;
        long charge = size + JournalDecodedPayloadCache.EntryChargeBytes;
        var codec = new BrotliWorkspacePayloadCodec(); var cache = new JournalDecodedPayloadCache(2 * charge);
        byte[][] blocks = Enumerable.Range(0, 3).Select(i => Encode(codec, Payload(size, i + 1))).ToArray();
        byte[] output = new byte[7];
        foreach (int index in new[] { 0, 1, 0, 2, 0, 1 }) cache.Read(codec, blocks[index], 301, output);
        Assert.Equal(4, cache.Decodes); Assert.Equal(2, cache.Hits); Assert.Equal(2, cache.Evictions);
        Assert.Equal(2, cache.EntryCount); Assert.Equal(2 * size, cache.PayloadBytes);
        Assert.Equal(2 * charge, cache.AccountedBytes); Assert.Equal(cache.BudgetBytes, cache.PeakAccountedBytes);
        byte[] oversized = Encode(codec, Payload(65536, 99));
        cache.Read(codec, oversized, 0, output); cache.Read(codec, oversized, 0, output);
        Assert.Equal(6, cache.Decodes); Assert.Equal(2, cache.EntryCount);
        Assert.Equal(cache.BudgetBytes, cache.PeakAccountedBytes);
        cache.Clear(); Assert.Equal(0, cache.AccountedBytes); Assert.Equal(0, cache.PayloadBytes); Assert.Equal(0, cache.EntryCount);
    }

    [Theory]
    [InlineData("reserved")] [InlineData("decoded")] [InlineData("trailing-stream")] [InlineData("format")]
    public void RechecksummedInvalidBlocksNeverPublishPartialOutputOrCacheEntries(string fault)
    {
        var codec = new BrotliWorkspacePayloadCodec(); var cache = new JournalDecodedPayloadCache();
        byte[] encoded = Encode(codec, Payload(65536, 17)), output = new byte[17];
        cache.Read(codec, encoded, 100, output); // A prior valid entry must not bless the modified block.
        switch (fault)
        {
            case "reserved": encoded[13] = 1; break;
            case "decoded": BinaryPrimitives.WriteInt32LittleEndian(encoded.AsSpan(4), 65535); break;
            case "format": encoded[0] ^= 1; break;
            case "trailing-stream":
                int stored = BinaryPrimitives.ReadInt32LittleEndian(encoded.AsSpan(8));
                Array.Resize(ref encoded, encoded.Length + 1);
                encoded[16 + stored] = 0x55; BinaryPrimitives.WriteInt32LittleEndian(encoded.AsSpan(8), stored + 1);
                break;
        }
        SHA256.HashData(encoded.AsSpan(0, encoded.Length - 32), encoded.AsSpan(encoded.Length - 32));
        Array.Fill(output, (byte)0x77);
        var error = Assert.Throws<WorkspaceException>(() => cache.Read(codec, encoded, 0, output));
        Assert.Contains(error.Code, new[] { "PayloadBlockCorrupt", "PayloadBlockUnsupported" });
        Assert.All(output, value => Assert.Equal(0x77, value));
        Assert.Equal(0, cache.EntryCount); Assert.Equal(0, cache.AccountedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void LiveStoreCacheSurvivesMutationWithoutMixingRetiredPayloadAndClearsOnClose(bool optimized)
    {
        using var directory = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = Identity(tree);
        var store = new JournalDeltaStore(directory.Path, identity, tree, optimizeJournal: optimized);
        try
        {
            Guid id = store.Create("file", false, 32).Id;
            byte[] expected = Payload(65536, 17), actual = new byte[65536];
            store.Write(id, 0, expected); store.Read(id, 0, actual); Assert.Equal(expected, actual);
            long encodedReads = store.Observe().Metrics!.JournalReadBytes;
            store.Read(id, 0, actual); Assert.Equal(expected, actual);
            Assert.True(store.DecodedPayloadCache.Hits > 0);
            Assert.True(store.Observe().Metrics!.JournalReadBytes > encodedReads);
            Assert.Equal(0, store.Observe().ReadCacheBytes); // Disk hydration remains absent.
            Array.Fill(expected, (byte)7, 8192, 16384); store.Write(id, 8192, expected.AsSpan(8192, 16384));
            store.Read(id, 0, actual); Assert.Equal(expected, actual);
            store.SetLength(id, 32768); store.SetLength(id, 65536); Array.Clear(expected, 32768, 32768);
            store.Rename(id, "renamed", false); store.Read(id, 0, actual); Assert.Equal(expected, actual);
            store.Delete(id); store.ReleaseUnlinkedFile(id);
            Guid fresh = store.Create("file", false, 32).Id;
            store.Write(fresh, 0, Payload(65536, 99)); store.Read(fresh, 0, actual); Assert.Equal(Payload(65536, 99), actual);
        }
        finally { store.Dispose(); }
        Assert.Equal(0, store.DecodedPayloadCache.AccountedBytes); Assert.Equal(0, store.DecodedPayloadCache.EntryCount);
        using var reopened = new JournalDeltaStore(directory.Path, identity, tree, optimizeJournal: optimized);
        byte[] restored = new byte[65536]; reopened.Read(reopened.Stat("file")!.Id, 0, restored);
        Assert.Equal(Payload(65536, 99), restored);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void FailedMutationClearsRamAndPoisonStillRequiresReconnect(bool optimized)
    {
        using var directory = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var observer = new FailingMutation();
        using var store = new JournalDeltaStore(directory.Path, Identity(tree), tree, observer, optimizeJournal: optimized);
        Guid id = store.Create("file", false, 32).Id; store.Write(id, 0, new byte[65536]); store.Read(id, 0, new byte[17]);
        Assert.True(store.DecodedPayloadCache.PayloadBytes > 0);
        observer.Armed = true;
        Assert.Throws<IOException>(() => store.Write(id, 0, Payload(65536, 1)));
        Assert.Equal(0, store.DecodedPayloadCache.AccountedBytes);
        Assert.Equal("DeltaReconnectRequired", Assert.Throws<WorkspaceException>(() => store.Read(id, 0, new byte[1])).Code);
    }

    [Fact]
    public void RepeatedPartialReadCostEvidence()
    {
        string? outputPath = Environment.GetEnvironmentVariable("SPLINTER_JOURNAL_DECODE_CACHE_COST_OUTPUT");
        if (outputPath is null) Assert.Skip("Cost evidence requires SPLINTER_JOURNAL_DECODE_CACHE_COST_OUTPUT.");
        using var directory = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = Identity(tree) with { MaxTransferBytes = 1048576 };
        // Same on-disk fixture for every budget; construction/replay is outside
        // the measured read interval. Every iteration proves returned bytes.
        byte[][] inputs = Enumerable.Range(0, 8).Select(i => Payload(1048576, i + 1)).ToArray();
        Guid id;
        using (var writer = new JournalDeltaStore(directory.Path, identity, tree, 0L))
        {
            id = writer.Create("file", false, 32).Id;
            for (int i = 0; i < inputs.Length; i++) writer.Write(id, i * 1048576L, inputs[i]);
        }
        var rows = new List<object>();
        foreach (int mib in new[] { 0, 32, 64, 128, 64, 0 })
        {
            using var reader = new JournalDeltaStore(directory.Path, identity, tree, (long)mib * 1024 * 1024);
            byte[] output = new byte[4096]; long readBefore = reader.Observe().Metrics!.JournalReadBytes;
            long allocated = GC.GetAllocatedBytesForCurrentThread(); var time = Stopwatch.StartNew();
            for (int pass = 0; pass < 8; pass++)
                for (int block = 0; block < inputs.Length; block++)
                    for (int slice = 0; slice < 8; slice++)
                    {
                        int offset = slice * 127973;
                        Assert.Equal(output.Length, reader.Read(id, block * 1048576L + offset, output));
                        Assert.True(output.AsSpan().SequenceEqual(inputs[block].AsSpan(offset, output.Length)));
                    }
            time.Stop(); allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
            var cache = reader.DecodedPayloadCache;
            rows.Add(new { budgetMiB = mib, elapsedMs = time.Elapsed.TotalMilliseconds, allocatedBytes = allocated,
                encodedReadBytes = reader.Observe().Metrics!.JournalReadBytes - readBefore,
                cache.PayloadBytes, cache.AccountedBytes, cache.PeakAccountedBytes, cache.EntryCount,
                cache.Hits, cache.Misses, cache.Decodes, cache.Evictions });
        }
        using var file = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(file, new { version = 1, scope = "SJL4 synthetic repeated partial reads; not Unity timing", rows },
            new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
    }

    private static OverlayIdentity Identity(ISnapshotTree tree) => new(2, "decoded-cache", tree.Snapshot, 32 * 1024 * 1024, 65536);
    private static byte[] Payload(int size, int seed)
    {
        byte[] bytes = new byte[size];
        for (int i = 0; i < size; i++) bytes[i] = (byte)((i / 113 + seed * 29) % 251);
        return bytes;
    }
    private static byte[] Encode(BrotliWorkspacePayloadCodec codec, byte[] input)
    {
        byte[] encoded = new byte[codec.GetMaxEncodedBytes(input.Length)];
        return encoded[..codec.Encode(input, encoded)];
    }
    private sealed class FailingMutation : IJournalMutationObserver
    {
        internal bool Armed;
        public void Observe(JournalMutationBoundary boundary)
        {
            if (Armed && boundary.Boundary is "commit.after-flush" or "latest.after-root-publication") throw new IOException("Injected commit failure.");
        }
    }
}
