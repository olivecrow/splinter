using System.Reflection;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class JournalWritePathTests
{
    private static OverlayIdentity Identity(ISnapshotTree tree) => new(2, "write-path", tree.Snapshot, 64L * 1048576, 1048576);

    [Theory]
    [InlineData("append")] [InlineData("bounded")] [InlineData("latest")]
    public void ExplicitFlushReusesOnlyThisHandlesSuccessfulDurability(string mode)
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = Identity(tree); Guid id;
        using (var store = new JournalDeltaStore(root.Path, identity, tree, optimizeJournal: mode == "latest",
            boundedLogBytes: mode == "bounded" ? 8192 : null))
        {
            long before = store.Observe().Metrics!.FlushCount;
            store.Flush(); store.Flush();
            Assert.Equal(before + (mode == "latest" ? 2 : 1), store.Observe().Metrics!.FlushCount);
            id = store.Create("file", false, 32).Id;
            before = store.Observe().Metrics!.FlushCount;
            store.Write(id, 0, [1, 2, 3]); store.Flush(); store.Flush();
            Assert.Equal(before + (mode == "latest" ? 3 : 1), store.Observe().Metrics!.FlushCount);
            tree.Available = false; store.Flush(); tree.Available = true;
        }
        using var replay = new JournalDeltaStore(root.Path, identity, tree);
        long resumed = replay.Observe().Metrics!.FlushCount;
        replay.Flush(); replay.Flush();
        Assert.Equal(resumed + (mode == "latest" ? 2 : 1), replay.Observe().Metrics!.FlushCount);
        byte[] data = new byte[3]; replay.Read(id, 0, data); Assert.Equal(new byte[] { 1, 2, 3 }, data);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void StateSizingReadsNoPayloadButOutputStillValidatesAndMatchesItsSize(bool encoded)
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree(); var identity = Identity(tree);
        using (var writer = new JournalDeltaStore(root.Path, identity, tree, boundedLogBytes: 16 * 1048576))
        {
            Guid id = writer.Create("file-é", false, 32).Id;
            writer.Write(id, 0, new byte[65536]); writer.SetMetadata(id, 32, 1, 2, 3);
        }
        if (encoded) EncodedPayloadFixture.CompressExistingJournal(root.Path);
        using var store = new JournalDeltaStore(root.Path, identity, tree);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var live = typeof(JournalDeltaStore).GetMethod("BoundedLiveRanges", flags)!.Invoke(store, null);
        var settings = typeof(JournalDeltaStore).GetField("boundedSettings", flags)!.GetValue(store);
        var write = typeof(JournalDeltaStore).GetMethod("WriteStateFrames", flags)!;
        long reads = store.Observe().Metrics!.JournalReadBytes;
        long size = (long)write.Invoke(store, [live, null, null, settings])!;
        Assert.Equal(reads, store.Observe().Metrics!.JournalReadBytes);
        using var output = new MemoryStream();
        long written = (long)write.Invoke(store, [live, output, null, settings])!;
        Assert.Equal(size, written); Assert.Equal(size, output.Length);
        Assert.True(store.Observe().Metrics!.JournalReadBytes > reads);
        Assert.Equal(0, tree.ReadBytes);
    }

}
