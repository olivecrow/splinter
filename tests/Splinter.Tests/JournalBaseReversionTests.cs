using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class JournalBaseReversionTests
{
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
