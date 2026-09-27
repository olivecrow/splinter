using System.Security.Cryptography;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class JournalReplayTests
{
    [Fact]
    public void OlderMetricsKeepUnmeasuredReplayBreakdownUnknown()
    {
        var old = System.Text.Json.JsonSerializer.Deserialize<OverlayIoMetrics>(
            """{"Epoch":"old","LockAcquisitions":0,"LockWaitTicks":0,"FlushCount":0,"FlushTicks":0,"CommitCount":0,"JournalAppendedBytes":0,"ReplayBytes":12,"ReplayTicks":34,"JournalReadBytes":12,"BaseReadBytes":0,"StopwatchFrequency":10000000}""")!;
        Assert.Null(old.ReplayFrames); Assert.Null(old.ReplayReadCalls); Assert.Null(old.ReplayReadTicks);
        Assert.Null(old.ReplayValidationTicks); Assert.Null(old.ReplayIndexTicks); Assert.Null(old.ReplayBufferBytes);
        Assert.Null(old.ReplayIndexOperations); Assert.Null(old.ReplayWriteRangeIndex); Assert.Null(old.LiveJournalWrites);
        Assert.Null(old.ReadRangeIndex);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BoundedReadsSeekToRelevantRangesAndPreserveHolesAndTruncation(bool reconnect)
    {
        using var dir = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var expected = new byte[16384]; new Random(81).NextBytes(expected); tree.Files["file"] = (byte[])expected.Clone();
        var identity = new OverlayIdentity(1, "read-index", tree.Snapshot, 1_000_000, 4096);
        string delta = Path.Combine(dir.Path, "delta");
        var store = new JournalDeltaStore(delta, identity, tree);
        try
        {
            Guid id = store.Stat("file")!.Id;
            for (int i = 0; i < 512; i++)
            {
                byte[] data = [(byte)i, 4, 5, 6, 7, 8, 9, 10];
                store.Write(id, i * 32, data); data.CopyTo(expected, i * 32);
            }
            if (reconnect) { store.Dispose(); store = new JournalDeltaStore(delta, identity, tree); }
            foreach ((int offset, int length) in new[] { (0, 4), (16002, 3), (8198, 38), (16380, 4), (16384, 4), (5, 0) })
            {
                var before = store.Observe().Metrics!.ReadRangeIndex!;
                byte[] actual = new byte[length]; int count = store.Read(id, offset, actual);
                Assert.Equal(expected.Skip(offset).Take(length).ToArray(), actual[..count]);
                var after = store.Observe().Metrics!.ReadRangeIndex!;
                Assert.Equal(1, after.ReadCalls - before.ReadCalls);
                Assert.InRange(after.SearchProbes - before.SearchProbes, 0, 10);
                Assert.InRange(after.VisitedExtents - before.VisitedExtents, 0, 3);
            }
            store.SetLength(id, 8195); store.SetLength(id, 16400);
            byte[] tail = new byte[8224]; Assert.Equal(tail.Length, store.Read(id, 8176, tail));
            byte[] truncated = expected.Skip(8176).Take(19).Concat(new byte[8205]).ToArray();
            Assert.Equal(truncated, tail);
        }
        finally { store.Dispose(); }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(65535)]
    [InlineData(65536)]
    [InlineData(131071)]
    public void BufferedReplayPreservesBytesAndDetectsCorruptionAcrossReadBoundaries(int corruptAt)
    {
        using var dir = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = new OverlayIdentity(1, "replay", tree.Snapshot, 10_000_000, 1_048_576);
        string delta = Path.Combine(dir.Path, "delta"), journal = Path.Combine(delta, "delta.journal");
        byte[] block = new byte[4096]; new Random(17).NextBytes(block);
        using (var store = new JournalDeltaStore(delta, identity, tree))
        {
            var file = store.Create("private", false, 32);
            for (int i = 0; i < (corruptAt < 0 ? 300 : 40); i++) store.Write(file.Id, i * 4096, block);
        }
        byte[] bytes = File.ReadAllBytes(journal);
        if (corruptAt >= 0)
        {
            bytes[corruptAt] ^= 1; File.WriteAllBytes(journal, bytes);
            Assert.Equal("DeltaCorrupt", Assert.Throws<WorkspaceException>(() => new JournalDeltaStore(delta, identity, tree)).Code);
            Assert.Equal(bytes, File.ReadAllBytes(journal)); return;
        }
        using var resumed = new JournalDeltaStore(delta, identity, tree);
        var metrics = resumed.Observe().Metrics!;
        Assert.Equal(bytes.Length, metrics.ReplayBytes); Assert.Equal(301, metrics.ReplayFrames);
        Assert.Equal(65536, metrics.ReplayBufferBytes);
        var indexCosts = metrics.ReplayIndexOperations!;
        Assert.Equal(metrics.ReplayFrames, indexCosts.Sum(x => x.Frames));
        Assert.Equal(metrics.ReplayIndexTicks, indexCosts.Sum(x => x.ApplyTicks));
        Assert.Equal(300, Assert.Single(indexCosts, x => x.Operation == "write").Frames);
        var ranges = metrics.ReplayWriteRangeIndex!;
        Assert.Equal(300, ranges.ReplaceCalls);
        Assert.Equal(Enumerable.Range(0, 300).Sum(i => (long)i), ranges.PriorExtents);
        Assert.Equal(299, ranges.MaxPriorExtents);
        Assert.Equal(299, ranges.VisitedExtents);
        Assert.Equal(0, ranges.CopiedExtents);
        Assert.Equal(ranges.ReplaceCalls, ranges.PriorExtentBuckets.Sum());
        Assert.True(ranges.LocateAndReplaceTicks + ranges.SortTicks <=
            Assert.Single(indexCosts, x => x.Operation == "write").ApplyTicks);
        Assert.InRange(metrics.ReplayReadCalls!.Value, 1, 30);
        Assert.True(metrics.ReplayReadTicks + metrics.ReplayValidationTicks + metrics.ReplayIndexTicks <= metrics.ReplayTicks);
        Assert.Equal(0, metrics.CommitCount); Assert.Equal(0, metrics.FlushCount); Assert.Equal(0, metrics.BaseReadBytes);
        byte[] result = new byte[block.Length]; var id = resumed.Stat("private")!.Id;
        foreach (int offset in new[] { 0, 65536, 131072, 299 * 4096 })
        { resumed.Read(id, offset, result); Assert.Equal(block, result); }
        resumed.Dispose();
        Assert.Equal(SHA256.HashData(bytes), SHA256.HashData(File.ReadAllBytes(journal)));
    }

    [Fact]
    public void OrderedRangeSplicePreservesRandomOverlapsHolesAndReconnectBytes()
    {
        using var dir = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = new OverlayIdentity(1, "range-splice", tree.Snapshot, 1_000_000, 4096);
        string delta = Path.Combine(dir.Path, "delta"), journal = Path.Combine(delta, "delta.journal");
        byte[] expected = new byte[4096]; var random = new Random(27);
        Guid id;
        using (var store = new JournalDeltaStore(delta, identity, tree))
        {
            id = store.Create("private", false, 32).Id;
            store.SetLength(id, expected.Length);
            for (int i = 0; i < 240; i++)
            {
                int size = random.Next(1, 97), offset = random.Next(0, expected.Length - size + 1);
                byte[] bytes = new byte[size]; random.NextBytes(bytes);
                store.Write(id, offset, bytes); bytes.CopyTo(expected, offset);
                if (i % 30 == 29)
                {
                    byte[] actual = new byte[expected.Length];
                    Assert.Equal(actual.Length, store.Read(id, 0, actual));
                    Assert.Equal(expected, actual);
                }
            }
            store.SetLength(id, 2048); Array.Clear(expected, 2048, 2048);
            store.SetLength(id, 4096);
            store.Write(id, 4095, [0x5a]); expected[4095] = 0x5a;
        }
        byte[] before = SHA256.HashData(File.ReadAllBytes(journal));
        using var resumed = new JournalDeltaStore(delta, identity, tree);
        byte[] replayed = new byte[expected.Length];
        Assert.Equal(replayed.Length, resumed.Read(id, 0, replayed));
        Assert.Equal(expected, replayed);
        Assert.Equal(241, resumed.Observe().Metrics!.ReplayWriteRangeIndex!.ReplaceCalls);
        resumed.Dispose();
        Assert.Equal(before, SHA256.HashData(File.ReadAllBytes(journal)));
    }

    [Fact]
    public void ReplayOperationBreakdownIsBoundedDetachedAndExcludesCurrentWrites()
    {
        using var dir = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = new OverlayIdentity(1, "operations", tree.Snapshot, 1_000_000, 4096);
        string delta = Path.Combine(dir.Path, "delta");
        using (var store = new JournalDeltaStore(delta, identity, tree))
        {
            var file = store.Create("first", false, 32);
            store.Write(file.Id, 0, [1, 2]); store.SetLength(file.Id, 8);
            store.SetMetadata(file.Id, 32, 2, 2, 2); store.Rename(file.Id, "last", false); store.Delete(file.Id);
            Assert.Empty(store.Observe().Metrics!.ReplayIndexOperations!);
        }
        using var resumed = new JournalDeltaStore(delta, identity, tree);
        var metrics = resumed.Observe().Metrics!;
        var costs = metrics.ReplayIndexOperations!;
        Assert.Equal(new[] { "create", "delete", "metadata", "rename", "resize", "write" }, costs.Select(x => x.Operation));
        Assert.All(costs, x => { Assert.Equal(1, x.Frames); Assert.True(x.ApplyTicks >= 0); });
        Assert.Equal(metrics.ReplayFrames, costs.Sum(x => x.Frames));
        Assert.Equal(metrics.ReplayIndexTicks, costs.Sum(x => x.ApplyTicks));
        Assert.Equal(1, metrics.ReplayWriteRangeIndex!.ReplaceCalls);
        Assert.Equal(new long[] { 1, 0, 0, 0, 0, 0 }, metrics.ReplayWriteRangeIndex.PriorExtentBuckets);
        costs[0] = new("changed", 99, 99);
        Assert.Equal("create", resumed.Observe().Metrics!.ReplayIndexOperations![0].Operation);
        metrics.ReplayWriteRangeIndex.PriorExtentBuckets[0] = 99;
        Assert.Equal(1, resumed.Observe().Metrics!.ReplayWriteRangeIndex!.PriorExtentBuckets[0]);
        resumed.Create("new-live-write", false, 32);
        Assert.Equal(6, resumed.Observe().Metrics!.ReplayFrames);
    }

    [Fact]
    public void BufferedReadAheadDoesNotAcknowledgeIncompleteFinalFrame()
    {
        using var dir = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = new OverlayIdentity(1, "replay", tree.Snapshot, 10_000_000, 1_048_576);
        string delta = Path.Combine(dir.Path, "delta"), journal = Path.Combine(delta, "delta.journal");
        long acknowledged;
        using (var store = new JournalDeltaStore(delta, identity, tree))
        {
            var file = store.Create("private", false, 32);
            store.Write(file.Id, 0, new byte[130_000]); acknowledged = store.Observe().JournalBytes;
            store.Write(file.Id, 0, [9, 9, 9]);
        }
        using (var file = new FileStream(journal, FileMode.Open, FileAccess.Write)) file.SetLength(file.Length - 17);
        using var resumed = new JournalDeltaStore(delta, identity, tree);
        Assert.Equal(acknowledged, resumed.Observe().JournalBytes);
        Assert.Equal(2, resumed.Observe().Metrics!.ReplayFrames);
        byte[] data = new byte[3]; resumed.Read(resumed.Stat("private")!.Id, 0, data);
        Assert.Equal(new byte[3], data);
    }
}
