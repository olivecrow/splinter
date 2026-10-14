using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class JournalDecodedVisitorCacheTests
{
    [Theory]
    [InlineData(0)] [InlineData(32768)] [InlineData(131072)]
    public void VisitorsAndReadsShareOnlyValidatedDecodedBlocks(long budget)
    {
        var codec = new BrotliWorkspacePayloadCodec(); var cache = new JournalDecodedPayloadCache(budget);
        byte[] expected = Payload(65536), encoded = Encode(codec, expected), output = new byte[65536];
        for (int pass = 0; pass < 3; pass++)
        {
            int visited = 0;
            cache.Visit(codec, encoded, (at, chunk, context) =>
            {
                Assert.Equal(visited, at); Assert.True(chunk.Length <= 16384);
                Assert.Equal(new byte[] { 7, 9 }, context.ToArray());
                chunk.CopyTo(output.AsSpan(at)); visited += chunk.Length;
            }, [7, 9]);
            Assert.Equal(expected.Length, visited); Assert.Equal(expected, output);
            cache.Read(codec, encoded, 0, output); Assert.Equal(expected, output);
        }
        Assert.Equal(budget >= 66048 ? 1 : 6, cache.Decodes);
        Assert.True(cache.AccountedBytes <= budget);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CacheHitStillRejectsCurrentCorruptionBeforeVisiting(bool rechecksum)
    {
        var codec = new BrotliWorkspacePayloadCodec(); var cache = new JournalDecodedPayloadCache();
        byte[] encoded = Encode(codec, Payload(65536));
        cache.Visit(codec, encoded, (_, _, _) => { });
        if (rechecksum)
        {
            BinaryPrimitives.WriteInt32LittleEndian(encoded.AsSpan(4), 65535);
            SHA256.HashData(encoded.AsSpan(0, encoded.Length - 32), encoded.AsSpan(encoded.Length - 32));
        }
        else encoded[16] ^= 1;
        int visits = 0;
        Assert.Throws<WorkspaceException>(() => cache.Visit(codec, encoded, (_, _, _) => visits++));
        Assert.Equal(0, visits); Assert.Equal(0, cache.AccountedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RepeatedIdenticalWritesReuseDecodedRamButStillCommitAndSurviveReopen(bool optimized)
    {
        using var directory = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = new OverlayIdentity(2, "visitor-cache", tree.Snapshot, 8L * 1048576, 65536);
        byte[] expected = Payload(65536);
        using (var store = new JournalDeltaStore(directory.Path, identity, tree, optimizeJournal: optimized))
        {
            Guid id = store.Create("file", false, 32).Id; store.Write(id, 0, expected);
            long beforeSequence = store.Observe().DurableSequence;
            long beforeDecodes = store.DecodedPayloadCache.Decodes;
            for (int pass = 0; pass < 4; pass++) store.Write(id, 0, expected);
            Assert.Equal(beforeSequence + 4, store.Observe().DurableSequence);
            Assert.Equal(beforeDecodes + 1, store.DecodedPayloadCache.Decodes);
            Assert.True(store.DecodedPayloadCache.Hits >= 3);
            // A different generation must not inherit the prior decoded data.
            Array.Fill(expected, (byte)83, 8192, 4096); store.Write(id, 8192, expected.AsSpan(8192, 4096));
            byte[] output = new byte[expected.Length]; store.Read(id, 0, output); Assert.Equal(expected, output);
            store.Seal();
        }
        using var reopened = new JournalDeltaStore(directory.Path, identity, tree, optimizeJournal: optimized);
        byte[] restored = new byte[expected.Length]; reopened.Read(reopened.Stat("file")!.Id, 0, restored);
        Assert.Equal(expected, restored);
    }

    [Fact]
    public void FailedVisitorClearsSharedCacheAndNextVisitStartsFromCurrentBytes()
    {
        var codec = new BrotliWorkspacePayloadCodec(); var cache = new JournalDecodedPayloadCache();
        byte[] encoded = Encode(codec, Payload(65536));
        cache.Read(codec, encoded, 0, new byte[17]);
        Assert.Throws<IOException>(() => cache.Visit(codec, encoded, (_, _, _) => throw new IOException("visitor interrupted")));
        Assert.Equal(0, cache.AccountedBytes); Assert.Equal(0, cache.EntryCount);
        int visited = 0;
        cache.Visit(codec, encoded, (_, bytes, _) => visited += bytes.Length);
        Assert.Equal(65536, visited); Assert.Equal(2, cache.Decodes);
    }

    [Fact]
    public void RepeatedVisitorCostEvidence()
    {
        string? path = Environment.GetEnvironmentVariable("SPLINTER_JOURNAL_VISITOR_COST_OUTPUT");
        if (path is null) Assert.Skip("Cost evidence requires SPLINTER_JOURNAL_VISITOR_COST_OUTPUT.");
        var codec = new BrotliWorkspacePayloadCodec();
        byte[][] expected = Enumerable.Range(0, 8).Select(i => Payload(1048576, i)).ToArray();
        byte[][] encoded = expected.Select(p => Encode(codec, p)).ToArray();
        // Warm both paths separately; measurements use fresh bounded caches.
        foreach (long budget in new[] { 0L, 64L * 1048576 })
            new JournalDecodedPayloadCache(budget).Visit(codec, encoded[0], (_, _, _) => { });
        var rows = new List<object>();
        foreach (long budget in new[] { 0L, 64L * 1048576, 64L * 1048576, 0L })
        {
            var cache = new JournalDecodedPayloadCache(budget);
            long allocated = GC.GetAllocatedBytesForCurrentThread(); var watch = Stopwatch.StartNew();
            for (int pass = 0; pass < 16; pass++)
                for (int i = 0; i < encoded.Length; i++)
                    cache.Visit(codec, encoded[i], (at, chunk, _) => Assert.True(chunk.SequenceEqual(expected[i].AsSpan(at, chunk.Length))));
            watch.Stop(); allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
            rows.Add(new { budgetBytes = budget, elapsedMs = watch.Elapsed.TotalMilliseconds, allocatedBytes = allocated,
                cache.Hits, cache.Misses, cache.Decodes, cache.PayloadBytes, cache.PeakAccountedBytes });
        }
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(file, new { version = 1, scope = "Same compressed blocks; repeated comparison/visitor CPU cost, not Unity or filesystem timing", rows },
            new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
    }

    private static byte[] Payload(int length, int seed = 0) =>
        Enumerable.Range(0, length).Select(i => (byte)((i / 113 + seed * 29) % 251)).ToArray();
    private static byte[] Encode(BrotliWorkspacePayloadCodec codec, byte[] input)
    {
        byte[] output = new byte[codec.GetMaxEncodedBytes(input.Length)]; return output[..codec.Encode(input, output)];
    }
}
