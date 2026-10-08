using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class JournalCloseAvailabilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LinkedOrUnknownCloseNeedsNoBasisButActualReclamationDoes(bool optimized)
    {
        using var directory = new TestDirectory();
        var tree = new CountedTree();
        var identity = new OverlayIdentity(2, "close-availability", tree.Snapshot, 16 * 1048576, 1048576);
        using var writer = new JournalDeltaStore(directory.Path, identity, tree, optimizeJournal: optimized);
        Guid linked = writer.Create("linked", false, 32).Id;
        Guid deleted = writer.Create("deleted", false, 32).Id;
        writer.Write(deleted, 0, [1, 2, 3]); writer.Delete(deleted);
        long sequence = writer.Observe().DurableSequence;
        tree.Checks = 0; tree.Unavailable = true;
        writer.ReleaseUnlinkedFile(linked); writer.ReleaseUnlinkedFile(Guid.NewGuid());
        Assert.Equal(0, tree.Checks);
        if (optimized)
            Assert.Equal("SnapshotMissing", Assert.Throws<WorkspaceException>(() => writer.ReleaseUnlinkedFile(deleted)).Code);
        else writer.ReleaseUnlinkedFile(deleted);
        tree.Unavailable = false;
        if (optimized) writer.ReleaseUnlinkedFile(deleted);
        Assert.Equal(sequence, writer.Observe().DurableSequence);
        Assert.Equal(0, writer.Get(linked).Length);
    }

    [Fact]
    public void NoOpCloseStillRejectsPoisonAndDisposedWriters()
    {
        using var directory = new TestDirectory(); var tree = new CountedTree();
        var observer = new Failure();
        var writer = new JournalDeltaStore(directory.Path,
            new(2, "close-health", tree.Snapshot, 16 * 1048576, 1048576), tree, observer);
        try
        {
            Guid id = writer.Create("linked", false, 32).Id;
            observer.Fail = true;
            Assert.Throws<IOException>(() => writer.Write(id, 0, [1]));
            Assert.Equal("DeltaReconnectRequired", Assert.Throws<WorkspaceException>(() => writer.ReleaseUnlinkedFile(id)).Code);
            writer.Dispose();
            Assert.Throws<ObjectDisposedException>(() => writer.ReleaseUnlinkedFile(id));
        }
        finally { writer.Dispose(); }
    }

    private sealed class Failure : IJournalMutationObserver
    {
        public bool Fail;
        public void Observe(JournalMutationBoundary boundary)
        {
            if (Fail && boundary.Boundary == "latest.before-root-publication") throw new IOException("Interrupted publication");
        }
    }
    private sealed class CountedTree : ISnapshotTree
    {
        private readonly SnapshotOverlayTests.FakeSnapshotTree inner = new();
        public int Checks;
        public bool Unavailable;
        public SnapshotReference Snapshot => inner.Snapshot;
        public void EnsureAvailable()
        {
            Checks++;
            if (Unavailable) throw new WorkspaceException("SnapshotMissing", "Test basis unavailable");
            inner.EnsureAvailable();
        }
        public SnapshotEntry? Stat(string path) => inner.Stat(path);
        public IReadOnlyList<SnapshotEntry> List(string path) => inner.List(path);
        public int Read(string path, long offset, Span<byte> output) => inner.Read(path, offset, output);
    }
}
