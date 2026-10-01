using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class JournalCompactionSizeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SmallScatteredOverwritesDoNotGrowTheCompactedJournal(bool previouslyCompacted)
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = new OverlayIdentity(1, "size", tree.Snapshot, 1024 * 1024, 8192);
        string directory = Path.Combine(root.Path, "delta");
        byte[] expected = Enumerable.Repeat((byte)1, 8192).ToArray();
        using (var store = new JournalDeltaStore(directory, identity, tree))
        {
            Guid id = store.Create("file", false, 32).Id;
            store.Write(id, 0, expected);
            if (previouslyCompacted)
            {
                Array.Fill(expected, (byte)2, 2048, 4096);
                store.Write(id, 2048, expected.AsSpan(2048, 4096));
            }
        }
        if (previouslyCompacted)
        {
            var first = JournalDeltaStore.CompactDirectory(directory, identity, tree);
            Assert.True(first.After.JournalBytes < first.Before.JournalBytes);
            JournalDeltaStore.CompleteCompaction(directory, identity);
        }
        SourceChangeView changes; DeltaEntry entry; long before, estimate;
        using (var store = new JournalDeltaStore(directory, identity, tree))
        {
            Guid id = store.Stat("file")!.Id;
            for (int offset = 16; offset < 2048; offset += 32)
            {
                expected[offset] = 3; expected[offset + 6144] = 4;
                store.Write(id, offset, [3]); store.Write(id, offset + 6144, [4]);
            }
            entry = store.Stat("file")!; changes = store.ObserveChanges([]);
            before = store.Observe().JournalBytes; estimate = store.EstimateCompactionJournalOverlapBytes();
        }
        var receipt = JournalDeltaStore.CompactDirectory(directory, identity, tree,
            maxEstimatedJournalOverlapBytes: before * 2);
        Assert.True(receipt.After.JournalBytes <= before, $"Compaction grew the journal: {before} -> {receipt.After.JournalBytes}.");
        Assert.Equal(before + receipt.After.JournalBytes, estimate);
        using (var replay = new JournalDeltaStore(directory, identity, tree))
        {
            Assert.Equal(entry, replay.Stat("file"));
            Assert.Equal(changes.Sequence, replay.ObserveChanges([]).Sequence);
            Assert.Equal(changes.Changes, replay.ObserveChanges([]).Changes);
            byte[] actual = new byte[expected.Length];
            Assert.Equal(actual.Length, replay.Read(entry.Id, 0, actual)); Assert.Equal(expected, actual);
        }
        JournalDeltaStore.CompleteCompaction(directory, identity);
        var again = JournalDeltaStore.CompactDirectory(directory, identity, tree);
        Assert.True(again.After.JournalBytes <= receipt.After.JournalBytes);
        JournalDeltaStore.CompleteCompaction(directory, identity);
        Assert.Equal(0, tree.ReadBytes);
    }

    [Fact]
    public void InterleavedObsoleteTinyWritesDoNotGrowTheCompactedJournal()
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = new OverlayIdentity(1, "tiny", tree.Snapshot, 1024 * 1024, 4096);
        string directory = Path.Combine(root.Path, "delta"); SourceChangeView changes;
        using (var store = new JournalDeltaStore(directory, identity, tree))
        {
            Guid a = store.Create("a", false, 32).Id, b = store.Create("b", false, 32).Id;
            for (int i = 0; i < 20; i++) { store.Write(a, 0, [(byte)i]); store.Write(b, 0, [(byte)(i + 1)]); }
            changes = store.ObserveChanges([]);
        }
        var receipt = JournalDeltaStore.CompactDirectory(directory, identity, tree);
        Assert.True(receipt.After.JournalBytes <= receipt.Before.JournalBytes);
        using (var replay = new JournalDeltaStore(directory, identity, tree))
        {
            Assert.Equal(changes.Sequence, replay.ObserveChanges([]).Sequence);
            Assert.Equal(changes.Changes, replay.ObserveChanges([]).Changes);
            byte[] value = new byte[1]; replay.Read(replay.Stat("a")!.Id, 0, value); Assert.Equal(19, value[0]);
            replay.Read(replay.Stat("b")!.Id, 0, value); Assert.Equal(20, value[0]);
        }
        JournalDeltaStore.CompleteCompaction(directory, identity);
        Assert.Equal(0, tree.ReadBytes);
    }
}
