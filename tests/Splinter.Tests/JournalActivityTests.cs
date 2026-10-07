using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class JournalActivityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WriterAccountingMatchesDiskAfterMutationsAndReconnect(bool optimized)
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = new OverlayIdentity(2, "activity", tree.Snapshot, 4 * 1048576, 1048576);
        SnapshotDeltaSeal seal;
        using (var store = new JournalDeltaStore(root.Path, identity, tree, optimizeJournal: optimized))
        {
            Check(store);
            var id = store.Create("file", false, 32).Id; Check(store);
            store.Write(id, 0, new byte[65536]); Check(store);
            store.Write(id, 32768, [1, 2, 3]); Check(store);
            store.SetLength(id, 17); Check(store);
            store.Rename(id, "renamed", false); Check(store);
            store.Delete(id); Check(store);
            store.ReleaseUnlinkedFile(id); Check(store);
            seal = store.Seal();
        }
        using var resumed = new JournalDeltaStore(root.Path, identity, tree);
        Check(resumed); Assert.Equal(seal, resumed.Seal());

        static void Check(JournalDeltaStore store)
        {
            var actual = store.Observe(); var activity = Assert.IsType<OverlayStorageObservation>(store.ObserveActivity());
            Assert.Equal(actual.JournalBytes, store.GetJournalBytes());
            Assert.Equal(actual.JournalBytes, activity.JournalBytes);
            Assert.Equal(actual.DurableSequence, activity.DurableSequence);
            Assert.Equal(actual.PayloadBytes, activity.PayloadBytes);
            Assert.Equal("current-files", actual.JournalBytesBasis);
            Assert.Equal("writer-accounting", activity.JournalBytesBasis);
        }
    }

    [Fact]
    public void ActivityDoesNotAuditForeignFilesButExplicitObservationAndSealStillDetectThem()
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = new OverlayIdentity(2, "activity", tree.Snapshot, 4 * 1048576, 1048576);
        using var store = new JournalDeltaStore(root.Path, identity, tree);
        store.Write(store.Create("file", false, 32).Id, 0, [1]);
        long owned = store.GetJournalBytes();
        string foreign = Path.Combine(Directory.GetDirectories(root.Path, "latest-*").Single(), "foreign.bin");
        File.WriteAllBytes(foreign, new byte[4096]);
        Assert.Equal(owned, store.GetJournalBytes());
        Assert.Equal(owned, store.ObserveActivity()!.JournalBytes);
        Assert.Equal(owned + 4096, store.Observe().JournalBytes);
        Assert.Equal("DeltaCorrupt", Assert.Throws<WorkspaceException>(() => store.Seal()).Code);
        Assert.True(File.Exists(foreign));
    }

    [Fact]
    public void FailedWriterCannotAdvertiseValidAccountingUntilReconnected()
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = new OverlayIdentity(2, "activity", tree.Snapshot, 4 * 1048576, 1048576);
        using (var store = new JournalDeltaStore(root.Path, identity, tree, new Failure()))
        {
            Assert.Throws<IOException>(() => store.Create("file", false, 32));
            Assert.Equal("DeltaReconnectRequired", Assert.Throws<WorkspaceException>(() => store.GetJournalBytes()).Code);
            Assert.Null(store.ObserveActivity());
        }
        using var resumed = new JournalDeltaStore(root.Path, identity, tree);
        Assert.Equal(resumed.Observe().JournalBytes, resumed.GetJournalBytes());
    }

    private sealed class Failure : IJournalMutationObserver
    {
        public void Observe(JournalMutationBoundary value)
        {
            if (value.Boundary == "latest.after-root-publication") throw new IOException("Interrupted publication.");
        }
    }
}
