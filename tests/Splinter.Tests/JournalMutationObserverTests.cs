using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class JournalMutationObserverTests
{
    [Fact]
    public void ExplicitObserverReportsOrderedFramesWithoutChangingDurabilityOrPayload()
    {
        using var dir = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var observer = new RecordingObserver(); var identity = new OverlayIdentity(2, "observer", tree.Snapshot, 100000, 4096);
        using var store = new JournalDeltaStore(dir.Path, identity, tree, observer);
        var file = store.Create("file", false, 32); observer.Events.Clear();
        store.Write(file.Id, 0, new byte[4096]);
        Assert.Equal(JournalInterruptionTests.CommitBoundaries.Take(9), observer.Events.Select(e => e.Boundary));
        Assert.All(observer.Events, e => { Assert.Equal("write", e.Operation); Assert.Equal(2, e.Sequence); Assert.Equal(4096, e.PayloadBytes); });
        Assert.Single(observer.Events.Select(e => (e.FrameStart, e.FrameBytes)).Distinct());
        Assert.Equal(2, store.Observe().Metrics!.FlushCount);
        store.Flush(); Assert.Equal(new[] { "flush.before", "flush.after" }, observer.Events.TakeLast(2).Select(e => e.Boundary));
        Assert.Equal(3, store.Observe().Metrics!.FlushCount);
        Assert.Equal(4096, store.ObservePrivateUsage().RetainedLogicalPayloadBytes);
    }

    [Theory]
    [InlineData("commit.after-payload-prefix", false)]
    [InlineData("commit.after-flush", true)]
    [InlineData("commit.after-apply", true)]
    [InlineData("flush.before", true)]
    public void FailedDevelopmentObserverRequiresReconnectAndPreservesAcknowledgedData(string boundary, bool newWriteSurvives)
    {
        using var dir = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var observer = new RecordingObserver(); var identity = new OverlayIdentity(2, "observer-failure", tree.Snapshot, 100000, 4096);
        using (var store = new JournalDeltaStore(dir.Path, identity, tree, observer))
        {
            var file = store.Create("file", false, 32); store.Write(file.Id, 0, [1, 2, 3, 4]);
            if (boundary == "flush.before") store.Write(file.Id, 4, [5, 6, 7, 8]);
            observer.FailAt = boundary;
            Assert.Throws<IOException>(() => { if (boundary == "flush.before") store.Flush(); else store.Write(file.Id, 4, [5, 6, 7, 8]); });
            Assert.Equal("DeltaReconnectRequired", Assert.Throws<WorkspaceException>(() => store.Write(file.Id, 0, [9])).Code);
        }
        using var resumed = new JournalDeltaStore(dir.Path, identity, tree);
        byte[] result = new byte[8]; int count = resumed.Read(resumed.Stat("file")!.Id, 0, result);
        Assert.Equal(newWriteSurvives ? new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 } : new byte[] { 1, 2, 3, 4 }, result[..count]);
    }

    private sealed class RecordingObserver : IJournalMutationObserver
    {
        public List<JournalMutationBoundary> Events { get; } = [];
        public string? FailAt { get; set; }
        public void Observe(JournalMutationBoundary boundary)
        { Events.Add(boundary); if (boundary.Boundary == FailAt) throw new IOException("Development probe failure."); }
    }
}
