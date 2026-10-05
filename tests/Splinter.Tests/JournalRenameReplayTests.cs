using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class JournalRenameReplayTests
{
    [Fact]
    public void ListingsStayExactAcrossReplacementDirectoryMovesDeletionAndReplay()
    {
        using var d = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        tree.Files["a/old"] = [1]; tree.Files["a/nested/lazy"] = [2]; tree.Files["b/replaced"] = [3];
        for (int i = 0; i < 300; i++) tree.Files[$"other{i}/file"] = [4];
        var identity = new OverlayIdentity(2, "listing", tree.Snapshot, 1_000_000, 4096); string delta = Path.Combine(d.Path, "delta");
        void Check(JournalDeltaStore store)
        {
            Assert.Equal(new[] { "b/moved", "b/replaced" }, store.List("b").Select(e => e.Path).ToArray());
            Assert.Equal(new[] { "b/moved/nested" }, store.List("b/moved").Select(e => e.Path).ToArray());
            Assert.Equal(new[] { "b/moved/nested/lazy" }, store.List("b/moved/nested").Select(e => e.Path).ToArray());
            Assert.Equal(new byte[] { 1 }, Read(store, "b/replaced"));
            Assert.Throws<DirectoryNotFoundException>(() => store.List("a"));
        }
        using (var store = new JournalDeltaStore(delta, identity, tree))
        {
            foreach (var entry in store.List("")) _ = store.List(entry.Path); // Warm unrelated paths.
            store.Rename(store.Stat("a/old")!.Id, "b/replaced", true);
            var created = store.Create("a/temporary", false, 32); store.Delete(created.Id);
            store.Rename(store.Stat("a")!.Id, "b/moved", false); Check(store);
        }
        using (var replayed = new JournalDeltaStore(delta, identity, tree)) Check(replayed);
        JournalDeltaStore.CompactDirectory(delta, identity, tree);
        using (var compacted = new JournalDeltaStore(delta, identity, tree)) Check(compacted);
    }
    private static byte[] Read(JournalDeltaStore store, string path)
    { byte[] value = new byte[1]; Assert.Equal(1, store.Read(store.Stat(path)!.Id, 0, value)); return value; }
    [Theory]
    [InlineData("dir", "moved")]
    [InlineData("Dír", "Ünicode")]
    [InlineData("MiXeD", "mixed")]
    public void RenameKeepsBoundarySiblingsLazyChildrenAndWhiteoutsAcrossReplay(string from, string to)
    {
        using var d = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        tree.Files[from + "/loaded"] = [1]; tree.Files[from + "/lazy"] = [2];
        tree.Files[from + "/nested/removed"] = [3]; tree.Files[from + "/nested/kept"] = [4];
        tree.Files[from + "-sibling/untouched"] = [5];
        var identity = new OverlayIdentity(2, "rename", tree.Snapshot, 1_000_000, 4096);
        string delta = Path.Combine(d.Path, "delta"); Guid openFile;
        void Check(JournalDeltaStore store)
        {
            foreach (var (path, expected) in new[] { (to + "/loaded", (byte)9), (to + "/lazy", (byte)2),
                (to + "/nested/kept", (byte)4), (from + "-sibling/untouched", (byte)5) })
            {
                byte[] bytes = new byte[1]; store.Read(store.Stat(path)!.Id, 0, bytes); Assert.Equal(expected, bytes[0]);
            }
            Assert.Null(store.Stat(to + "/nested/removed"));
            if (!from.Equals(to, StringComparison.OrdinalIgnoreCase))
            { Assert.Null(store.Stat(to + "-sibling/untouched")); Assert.Null(store.Stat(from)); }
        }
        using (var store = new JournalDeltaStore(delta, identity, tree))
        {
            openFile = store.Stat(from + "/loaded")!.Id; store.Write(openFile, 0, [9]);
            store.Delete(store.Stat(from + "/nested/removed")!.Id);
            _ = store.Stat(from + "-sibling/untouched");
            store.Rename(store.Stat(from.ToUpperInvariant())!.Id, to, false);
            Check(store); Assert.Equal(openFile, store.Stat(to + "/loaded")!.Id);
        }
        using var replayed = new JournalDeltaStore(delta, identity, tree);
        Check(replayed); Assert.Equal(openFile, replayed.Stat(to + "/loaded")!.Id);
        Assert.Equal(1, Assert.Single(replayed.Observe().Metrics!.ReplayIndexOperations!, x => x.Operation == "rename").Frames);
        Assert.Equal(new byte[] { 1 }, tree.Files[from + "/loaded"]);
        Assert.Equal(new byte[] { 3 }, tree.Files[from + "/nested/removed"]);
    }
}
