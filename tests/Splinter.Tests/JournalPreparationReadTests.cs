using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class JournalPreparationReadTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreparationPinsCurrentMetadataAndReleasesEveryHandleBeforePublicationOrFailure(bool interrupt)
    {
        if (!OperatingSystem.IsWindows()) { Assert.Skip("Windows file-sharing proof."); return; }
        using var fixture = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = new OverlayIdentity(2, "preparation-read", tree.Snapshot, 8L * 1048576, 1048576);
        string root = Path.Combine(fixture.Path, "delta.journal"); bool armed = false, checkedProof = false; Guid id;
        var observer = new Observer(boundary =>
        {
            if (!armed || boundary != "latest.after-preparation-proof") return;
            using var read = new FileStream(root, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            Assert.ThrowsAny<IOException>(() => { using var write = new FileStream(root, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete); });
            Assert.ThrowsAny<IOException>(() => File.Move(root, root + ".moved"));
            checkedProof = true;
            if (interrupt) throw new IOException("Stop before the new capsule exists.");
        });
        using (var writer = new JournalDeltaStore(fixture.Path, identity, tree, observer))
        {
            id = writer.Create("file", false, 32).Id; writer.Write(id, 0, [1, 2, 3]);
            long flushes = writer.Observe().Metrics!.FlushCount; armed = true;
            if (interrupt)
            {
                Assert.Throws<IOException>(() => writer.Write(id, 0, [4, 5, 6]));
                Assert.Equal("DeltaReconnectRequired", Assert.Throws<WorkspaceException>(() => writer.GetJournalBytes()).Code);
                using var released = new FileStream(root, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);
            }
            else
            {
                writer.Write(id, 0, [4, 5, 6]);
                Assert.Equal(flushes + 1, writer.Observe().Metrics!.FlushCount);
                Assert.Equal(3, writer.Observe().DurableSequence);
            }
        }
        Assert.True(checkedProof);
        using var resumed = new JournalDeltaStore(fixture.Path, identity, tree);
        byte[] actual = new byte[3]; Assert.Equal(3, resumed.Read(id, 0, actual));
        Assert.Equal(interrupt ? new byte[] { 1, 2, 3 } : new byte[] { 4, 5, 6 }, actual);
        resumed.Write(id, 0, [7]); // recovery also proves there is no leaked preparation pin
        Assert.DoesNotContain(Directory.GetFiles(fixture.Path, "*", SearchOption.AllDirectories), p => p.EndsWith(".pending", StringComparison.Ordinal));
    }

    [Fact]
    public void LargePreparationHasBoundedPinsAndStillPreservesEveryCurrentObject()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Skip("Windows file-sharing proof."); return; }
        using var fixture = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = new OverlayIdentity(2, "bounded-preparation", tree.Snapshot, 8L * 1048576, 1048576);
        using (var writer = new JournalDeltaStore(fixture.Path, identity, tree))
            for (int i = 0; i < 80; i++) writer.Create("file-" + i, false, 32);
        string root = Path.Combine(fixture.Path, "delta.journal");
        var storage = new LatestJournalStorage(root, identity, true);
        var contents = storage.Names().Where(n => n.StartsWith("node-", StringComparison.Ordinal))
            .ToDictionary(n => n, storage.Read, StringComparer.Ordinal);
        var puts = contents.Select(p => new LatestJournalStorage.Put(p.Key, LatestJournalStorage.Hash(p.Value), p.Value)).ToArray();
        string[] physical = LatestJournalStorage.Files(root); int blocked = 0, admitted = 0;
        storage.Commit(storage.Current, puts, [], boundary =>
        {
            if (boundary != "latest.after-preparation-proof") return;
            foreach (string path in physical)
            {
                try { using var write = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete); admitted++; }
                catch (IOException) { blocked++; }
            }
        });
        Assert.InRange(blocked, 1, 64); Assert.True(admitted > 0);
        var reopened = new LatestJournalStorage(root, identity, true);
        foreach (var pair in contents) Assert.Equal(pair.Value, reopened.Read(pair.Key));
        using var reader = JournalDeltaStore.OpenReadOnly(fixture.Path, identity, tree);
        for (int i = 0; i < 80; i++) Assert.NotNull(reader.Stat("file-" + i));
        Assert.Equal(storage.Current.Sequence, reader.Seal().DurableSequence);
    }

    private sealed class Observer(Action<string> action) : IJournalMutationObserver
    { public void Observe(JournalMutationBoundary boundary) => action(boundary.Boundary); }
}
