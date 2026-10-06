using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class JournalBaseReversionTests
{
    private sealed class TracedTree(SnapshotOverlayTests.FakeSnapshotTree inner) : ISnapshotTree
    {
        public readonly List<(string Path, long Offset)> Reads = [];
        public string? ShortReadPath;
        public SnapshotReference Snapshot => inner.Snapshot;
        public void EnsureAvailable() => inner.EnsureAvailable();
        public SnapshotEntry? Stat(string path) => inner.Stat(path);
        public IReadOnlyList<SnapshotEntry> List(string path) => inner.List(path);
        public int Read(string path, long offset, Span<byte> destination)
        { Reads.Add((path, offset)); return path == ShortReadPath ? 0 : inner.Read(path, offset, destination); }
    }

    [Fact]
    public void FailedComparisonAfterAnotherFileRevertsPreservesAllAcknowledgedPrivateBytes()
    {
        using var root = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree();
        basis.Files["a"] = RandomNumberGenerator.GetBytes(8192); basis.Files["b"] = RandomNumberGenerator.GetBytes(8192);
        var tree = new TracedTree(basis);
        var identity = new OverlayIdentity(2, "ordered-failure", tree.Snapshot, 16777216, 1048576);
        SnapshotDeltaSeal seal;
        using (var store = new JournalDeltaStore(root.Path, identity, tree))
        {
            foreach (string path in new[] { "b", "a" })
            { Guid id = store.Stat(path)!.Id; store.Write(id, 0, new byte[8192]); store.Write(id, 0, basis.Files[path]); }
            seal = store.Seal();
        }
        byte[] journal = File.ReadAllBytes(Path.Combine(root.Path, "delta.journal"));
        tree.Reads.Clear(); tree.ShortReadPath = "b";
        Assert.Equal("SnapshotReadFailed", Assert.Throws<WorkspaceException>(() =>
            JournalDeltaStore.CompactDirectory(root.Path, identity, tree, revertBaseRanges: true)).Code);
        Assert.Equal(new[] { ("a", 0L), ("b", 0L) }, tree.Reads);
        Assert.Equal(journal, File.ReadAllBytes(Path.Combine(root.Path, "delta.journal")));
        Assert.False(File.Exists(Path.Combine(root.Path, "compaction-intent.json")));
        Assert.False(File.Exists(Path.Combine(root.Path, "delta.journal.compacting")));
        tree.ShortReadPath = null;
        using var replay = new JournalDeltaStore(root.Path, identity, tree);
        Assert.Equal(seal, replay.Seal());
        foreach (string path in new[] { "a", "b" })
        { byte[] bytes = new byte[8192]; Assert.Equal(8192, replay.Read(replay.Stat(path)!.Id, 0, bytes)); Assert.Equal(basis.Files[path], bytes); }
    }

    [Fact]
    public void InterleavedAndRenamedFilesCompareInBaseOrderAndReplayInJournalOrder()
    {
        using var root = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree();
        foreach (string path in new[] { "a", "b", "c" }) basis.Files[path] = RandomNumberGenerator.GetBytes(16384);
        var tree = new TracedTree(basis);
        var identity = new OverlayIdentity(2, "ordered-return", tree.Snapshot, 16777216, 1048576);
        var expected = new Dictionary<string, DeltaEntry>(); var bytes = new Dictionary<string, byte[]>();
        SourceChangeView changes;
        using (var store = new JournalDeltaStore(root.Path, identity, tree))
        {
            for (int pass = 0; pass < 3; pass++)
            foreach (int offset in new[] { 12288, 0 })
            foreach (string path in new[] { "c", "b", "a" })
            {
                byte[] payload = basis.Files[path].AsSpan(offset, 4096).ToArray();
                if (pass < 2 || offset == 12288) foreach (ref byte b in payload.AsSpan()) b ^= 0x55;
                store.Write(store.Stat(path)!.Id, offset, payload);
            }
            store.Rename(store.Stat("a")!.Id, "renamed", false);
            foreach (string path in new[] { "a", "b", "c" })
            {
                string current = path == "a" ? "renamed" : path;
                Guid id = store.Stat(current)!.Id; store.SetMetadata(id, 33, 101, 202, 303);
                expected[current] = store.Get(id); bytes[current] = (byte[])basis.Files[path].Clone();
                for (int at = 12288; at < 16384; at++) bytes[current][at] ^= 0x55;
            }
            changes = store.ObserveChanges([]);
        }
        tree.Reads.Clear();
        var receipt = JournalDeltaStore.CompactDirectory(root.Path, identity, tree, revertBaseRanges: true);
        Assert.Equal(12288, receipt.RevertedPayloadBytes);
        Assert.Equal(new[] { ("a", 0L), ("a", 12288L), ("b", 0L), ("b", 12288L), ("c", 0L), ("c", 12288L) }, tree.Reads);
        using (var replay = new JournalDeltaStore(root.Path, identity, tree))
        {
            Assert.Equal(changes.Sequence, replay.ObserveChanges([]).Sequence);
            Assert.Equal(changes.Changes, replay.ObserveChanges([]).Changes);
            foreach (var (path, entry) in expected)
            {
                Assert.Equal(entry, replay.Stat(path)); byte[] actual = new byte[16384];
                Assert.Equal(actual.Length, replay.Read(entry.Id, 0, actual)); Assert.Equal(bytes[path], actual);
            }
        }
        JournalDeltaStore.CompleteCompaction(root.Path, identity);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RevertingContentPreservesEntryIdentityAndChanges(bool enabled)
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        tree.Files["file"] = RandomNumberGenerator.GetBytes(16384);
        var identity = new OverlayIdentity(2, "base-return", tree.Snapshot, 16 * 1024 * 1024, 1048576);
        DeltaEntry expected; SourceChangeView changes;
        using (var store = new JournalDeltaStore(root.Path, identity, tree))
        {
            Guid id = store.Stat("file")!.Id;
            for (int i = 0; i < 8; i++)
            { store.Write(id, 0, new byte[16384]); store.Write(id, 0, tree.Files["file"]); }
            store.Rename(id, "renamed", false); store.SetMetadata(id, 33, 100, 200, 300);
            expected = store.Get(id); changes = store.ObserveChanges([]);
        }
        var receipt = JournalDeltaStore.CompactDirectory(root.Path, identity, tree, revertBaseRanges: enabled);
        Assert.Equal(enabled, receipt.RevertBaseRanges);
        Assert.Equal(enabled ? 16384 : 0, receipt.RevertedPayloadBytes);
        using (var replay = new JournalDeltaStore(root.Path, identity, tree))
        {
            Assert.Equal(expected, replay.Stat("renamed")); Assert.Null(replay.Stat("file"));
            byte[] actual = new byte[16384]; replay.Read(expected.Id, 0, actual); Assert.Equal(tree.Files["file"], actual);
            Assert.Equal(changes.Sequence, replay.ObserveChanges([]).Sequence);
            Assert.Equal(changes.Changes, replay.ObserveChanges([]).Changes);
            Assert.Equal(enabled ? 0 : 16384, replay.ObservePrivateUsage().LivePrivateBytes);
        }
        JournalDeltaStore.CompleteCompaction(root.Path, identity);
    }

    [Fact]
    public void PartialReversionRetainsMixedBytesAndTruncatedTailAndNewIncarnations()
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        byte[] basis = RandomNumberGenerator.GetBytes(16384); tree.Files["file"] = basis; tree.Files["replace"] = basis;
        var identity = new OverlayIdentity(2, "mixed-return", tree.Snapshot, 16 * 1024 * 1024, 1048576);
        byte[] expected = (byte[])basis.Clone(); Array.Fill(expected, (byte)42, 4096, 4096);
        DeltaEntry entry, replacement; SourceChangeView changes;
        using (var store = new JournalDeltaStore(root.Path, identity, tree))
        {
            Guid id = store.Stat("file")!.Id; store.Write(id, 0, new byte[16384]); store.Write(id, 0, expected);
            store.SetLength(id, 12000); store.SetLength(id, 16384); Array.Clear(expected, 12000, 4384);
            store.Write(id, 12288, basis.AsSpan(12288)); basis.AsSpan(12288).CopyTo(expected.AsSpan(12288));
            store.Delete(store.Stat("replace")!.Id); replacement = store.Create("replace", false, 32);
            store.Write(replacement.Id, 0, basis); entry = store.Get(id); replacement = store.Get(replacement.Id);
            changes = store.ObserveChanges([]);
        }
        var receipt = JournalDeltaStore.CompactDirectory(root.Path, identity, tree, revertBaseRanges: true);
        Assert.True(receipt.RevertedPayloadBytes > 0);
        using (var replay = new JournalDeltaStore(root.Path, identity, tree))
        {
            Assert.Equal(entry, replay.Stat("file")); Assert.Equal(replacement, replay.Stat("replace"));
            byte[] actual = new byte[16384]; replay.Read(entry.Id, 0, actual); Assert.Equal(expected, actual);
            replay.Read(replacement.Id, 0, actual); Assert.Equal(basis, actual);
            Assert.Equal(changes.Changes, replay.ObserveChanges([]).Changes);
        }
        JournalDeltaStore.CompleteCompaction(root.Path, identity);
    }

    [Theory]
    [InlineData("intent")]
    [InlineData("candidate-flushed")]
    [InlineData("prepared")]
    [InlineData("published")]
    public void SavedOptionAndExpectedStateSurviveEveryPublicationBoundary(string stage)
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree(); tree.Files["file"] = [65,66,67,68];
        var identity = new OverlayIdentity(2, "resume-return", tree.Snapshot, 1048576, 4096); DeltaEntry expected;
        using (var store = new JournalDeltaStore(root.Path, identity, tree))
        { Guid id = store.Stat("file")!.Id; store.Write(id, 0, [66,66,66,66]); store.Write(id, 0, [67,67,67,67]); store.Write(id, 0, [65,66,67,68]); expected = store.Get(id); }
        Assert.Throws<InvalidOperationException>(() => JournalDeltaStore.CompactDirectory(root.Path, identity, tree,
            boundary: at => { if (at == stage) throw new InvalidOperationException(); }, revertBaseRanges: true));
        Assert.Equal("CompactionIntentMismatch", Assert.Throws<WorkspaceException>(() =>
            JournalDeltaStore.CompactDirectory(root.Path, identity, tree, revertBaseRanges: false)).Code);
        var receipt = JournalDeltaStore.CompactDirectory(root.Path, identity, tree);
        Assert.Equal(4, receipt.RevertedPayloadBytes);
        using (var replay = new JournalDeltaStore(root.Path, identity, tree))
        { Assert.Equal(expected, replay.Stat("file")); byte[] bytes = new byte[4]; replay.Read(expected.Id, 0, bytes); Assert.Equal(tree.Files["file"], bytes); }
        JournalDeltaStore.CompleteCompaction(root.Path, identity);
    }

    [Fact]
    public void PreviousIntentVersionIsPreservedAndRejected()
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree(); tree.Files["file"] = [1,2,3];
        var identity = new OverlayIdentity(2, "old-intent", tree.Snapshot, 1048576, 4096);
        using (var store = new JournalDeltaStore(root.Path, identity, tree)) store.Write(store.Stat("file")!.Id, 0, [1,2,3]);
        Assert.Throws<InvalidOperationException>(() => JournalDeltaStore.CompactDirectory(root.Path, identity, tree,
            boundary: at => { if (at == "intent") throw new InvalidOperationException(); }, revertBaseRanges: true));
        string path = Path.Combine(root.Path, "compaction-intent.json");
        var intent = JsonNode.Parse(File.ReadAllText(path))!; intent["Version"] = 1;
        File.WriteAllText(path, intent.ToJsonString());
        byte[] saved = File.ReadAllBytes(path), journal = File.ReadAllBytes(Path.Combine(root.Path, "delta.journal"));
        Assert.Equal("CompactionIntentMismatch", Assert.Throws<WorkspaceException>(() =>
            JournalDeltaStore.CompactDirectory(root.Path, identity, tree)).Code);
        Assert.Equal(saved, File.ReadAllBytes(path)); Assert.Equal(journal, File.ReadAllBytes(Path.Combine(root.Path, "delta.journal")));
        Assert.False(File.Exists(Path.Combine(root.Path, "delta.journal.compacting")));
    }

    [Fact]
    public void MissingBasePreservesAuthoritativeJournalWithoutCandidate()
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree(); tree.Files["file"] = [1,2,3];
        var identity = new OverlayIdentity(2, "loss-return", tree.Snapshot, 1048576, 4096);
        using (var store = new JournalDeltaStore(root.Path, identity, tree)) store.Write(store.Stat("file")!.Id, 0, [1,2,3]);
        byte[] before = File.ReadAllBytes(Path.Combine(root.Path, "delta.journal")); tree.Available = false;
        Assert.Equal("SnapshotMissing", Assert.Throws<WorkspaceException>(() =>
            JournalDeltaStore.CompactDirectory(root.Path, identity, tree, revertBaseRanges: true)).Code);
        Assert.Equal(before, File.ReadAllBytes(Path.Combine(root.Path, "delta.journal")));
        Assert.False(File.Exists(Path.Combine(root.Path, "delta.journal.compacting")));
    }
}
