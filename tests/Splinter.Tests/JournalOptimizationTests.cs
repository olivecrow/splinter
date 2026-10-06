using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class JournalOptimizationTests
{
    [Fact]
    public void SparseRestoreReadsLocateRangesWithoutScanningEarlierExtentsAndRejectHoles()
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = new OverlayIdentity(2, "restore-search", tree.Snapshot, 1_000_000, 4096); Guid id;
        using (var writer = new JournalDeltaStore(root.Path, identity, tree, optimizeJournal: false))
        {
            id = writer.Create("file", false, 32).Id;
            for (int i = 0; i < 512; i++) writer.Write(id, i * 32, [(byte)i, 7, 8, 9]);
        }
        using var reader = JournalDeltaStore.OpenReadOnly(root.Path, identity, tree); var seal = reader.Seal(); reader.PrepareRestoreReads(seal);
        foreach (int i in new[] { 511, 256, 0 })
        {
            var before = reader.Observe().Metrics!.ReadRangeIndex!; byte[] actual = new byte[4];
            reader.ReadRestoreRange(id, i * 32, actual, seal); Assert.Equal(new byte[] { (byte)i, 7, 8, 9 }, actual);
            var after = reader.Observe().Metrics!.ReadRangeIndex!;
            Assert.InRange(after.SearchProbes - before.SearchProbes, 1, 10); Assert.Equal(1, after.VisitedExtents - before.VisitedExtents);
        }
        Assert.Equal("RestoreRangeNotPrivate", Assert.Throws<WorkspaceException>(() => reader.ReadRestoreRange(id, 4, new byte[4], seal)).Code);
        Assert.Equal("RestoreSealMismatch", Assert.Throws<WorkspaceException>(() => reader.ReadRestoreRange(id, 0, new byte[4], seal with { JournalBytes = seal.JournalBytes + 1 })).Code);
    }

    [Fact]
    public void MaximumCompressedPrivateComparisonReadsOneBlockAndPreservesChangedBytes()
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = new OverlayIdentity(2, "compressed-comparison", tree.Snapshot, 4_000_000, 1024 * 1024);
        using var store = new JournalDeltaStore(root.Path, identity, tree, optimizeJournal: false); var id = store.Create("file", false, 32).Id;
        byte[] bytes = new byte[1024 * 1024]; store.Write(id, 0, bytes); long payload = store.Observe().PayloadBytes;
        long before = store.Observe().Metrics!.JournalReadBytes; store.Write(id, 0, bytes);
        Assert.Equal(payload, store.Observe().PayloadBytes);
        Assert.Equal(payload, store.Observe().Metrics!.JournalReadBytes - before);
        bytes[70000] = 1; bytes[900000] = 2; store.Write(id, 0, bytes);
        byte[] actual = new byte[bytes.Length]; Assert.Equal(actual.Length, store.Read(id, 0, actual)); Assert.Equal(bytes, actual);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void MaximumCompressedBlockAndItsFragmentsSurviveCompactionWithoutChunkRereads(bool fragmented)
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = new OverlayIdentity(2, "stream-compaction", tree.Snapshot, 4_000_000, 1024 * 1024);
        byte[] expected = new byte[1024 * 1024]; long firstPayload, payload, frameCount; Guid id;
        using (var store = new JournalDeltaStore(root.Path, identity, tree, optimizeJournal: false))
        {
            id = store.Create("file", false, 32).Id; store.Write(id, 0, expected); firstPayload = store.Observe().PayloadBytes;
            if (fragmented)
            {
                new Random(527).NextBytes(expected.AsSpan(200000, 600000));
                store.Write(id, 200000, expected.AsSpan(200000, 600000));
            }
            payload = store.Observe().PayloadBytes; frameCount = store.Observe().DurableSequence;
        }
        var receipt = JournalDeltaStore.CompactDirectory(root.Path, identity, tree);
        var costs = receipt.Cost!.Stages;
        // Each extent is streamed once; fragments can share a stored block.
        Assert.Equal(payload + (fragmented ? firstPayload : 0), Assert.Single(costs, c => c.Stage == "before-state-hash").ReadBytes);
        // Frame metadata is scanned once; all slices of a partial encoded frame
        // share one decode, rather than one full decode per fragment/chunk.
        Assert.Equal(receipt.Before.JournalBytes - 32 * frameCount - payload + (fragmented ? firstPayload : 0),
            Assert.Single(costs, c => c.Stage == "candidate-scan").ReadBytes);
        Assert.True(receipt.After.JournalBytes <= receipt.Before.JournalBytes);
        using (var reader = JournalDeltaStore.OpenReadOnly(root.Path, identity, tree))
        {
            byte[] actual = new byte[expected.Length]; Assert.Equal(actual.Length, reader.Read(id, 0, actual)); Assert.Equal(expected, actual);
        }
        JournalDeltaStore.CompleteCompaction(root.Path, identity); Assert.Equal(0, tree.ReadBytes);
    }
}
