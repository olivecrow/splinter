using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class MetadataBatchTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CommonMetadataBatchContractKeepsOrderAndDurableReplay(bool runtime)
    {
        using var root = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = new OverlayIdentity(2, "metadata-batch", basis.Snapshot, long.MaxValue, 65536);
        using var writer = new JournalDeltaStore(root.Path, identity, basis, optimizeJournal: false);
        Guid id = writer.Create("file", false, 32).Id; long flushes = writer.Observe().Metrics!.FlushCount;
        IWorkspaceMetadataBatch batch = runtime ? writer : new LiteralBatch(); var order = new List<int>();
        batch.ApplyMetadataBatch([() => { order.Add(1); if (runtime) writer.SetLength(id, 5); },
            () => { order.Add(2); if (runtime) writer.SetMetadata(id, 32, 1, 2, 3); }]);
        Assert.Equal(new[] { 1, 2 }, order);
        if (!runtime) return;
        Assert.Equal(flushes + 1, writer.Observe().Metrics!.FlushCount); writer.Dispose();
        using var replay = new JournalDeltaStore(root.Path, identity, basis, optimizeJournal: false);
        Assert.Equal(5, replay.Get(id).Length); Assert.Equal(3, replay.Get(id).LastWriteTime);
    }

    [Fact(Timeout = 30000)]
    public async Task NativeMetadataCallbacksQueueBeforeGateAndShareDurability()
    {
        using var root = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree(); using var observer = new Pause();
        var identity = new OverlayIdentity(2, "metadata-callbacks", basis.Snapshot, long.MaxValue, 65536);
        using var writer = new JournalDeltaStore(root.Path, identity, basis, observer, optimizeJournal: false);
        Guid[] ids = Enumerable.Range(0, 9).Select(i => writer.Create("file-" + i, false, 32).Id).ToArray();
        var fs = new WinFspOverlayFileSystem(writer); long flushes = writer.Observe().Metrics!.FlushCount; observer.Armed = true;
        Task Call(int index) => Task.Factory.StartNew(() =>
        {
            if (index % 2 == 0) Assert.Equal(0, fs.SetFileSize(ids[index], null!, 5, false, out _));
            else Assert.Equal(0, fs.SetBasicInfo(ids[index], null!, 32, 1, 2, 3, 0, out _));
        }, TestContext.Current.CancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        Task first = Call(0); Task[] followers = [];
        try
        {
            Assert.True(observer.Entered.Wait(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken));
            followers = Enumerable.Range(1, 8).Select(Call).ToArray();
            Assert.True(SpinWait.SpinUntil(() => fs.PendingMetadataCount == 8, TimeSpan.FromSeconds(3)));
            Assert.All(followers, task => Assert.False(task.IsCompleted));
        }
        finally { observer.Proceed.Set(); await first; await Task.WhenAll(followers); }
        Assert.Equal(flushes + 2, writer.Observe().Metrics!.FlushCount);
        for (int i = 0; i < ids.Length; i++)
            if (i % 2 == 0) Assert.Equal(5, writer.Get(ids[i]).Length); else Assert.Equal(3, writer.Get(ids[i]).LastWriteTime);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void MetadataBatchFailureNeverExposesUnacknowledgedPrefix(bool latest)
    {
        using var root = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = new OverlayIdentity(2, "metadata-failure", basis.Snapshot, long.MaxValue, 65536);
        using var writer = new JournalDeltaStore(root.Path, identity, basis, optimizeJournal: latest);
        Guid id = writer.Create("file", false, 32).Id;
        Assert.Throws<IOException>(() => writer.ApplyMetadataBatch([() => writer.SetLength(id, 5), () => throw new IOException("injected callback failure")]));
        Assert.Equal("DeltaReconnectRequired", Assert.Throws<WorkspaceException>(() => writer.Get(id)).Code);
    }
    private sealed class LiteralBatch : IWorkspaceMetadataBatch
    { public void ApplyMetadataBatch(IReadOnlyList<Action> mutations) { foreach (var mutation in mutations) mutation(); } }
    private sealed class Pause : IJournalMutationObserver, IDisposable
    {
        public bool Armed;
        public readonly ManualResetEventSlim Entered = new(), Proceed = new();
        public void Observe(JournalMutationBoundary point)
        {
            if (!Armed || point.Boundary != "commit.before-flush") return;
            Armed = false; Entered.Set();
            if (!Proceed.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Release the test mutation.");
        }
        public void Dispose() { Entered.Dispose(); Proceed.Dispose(); }
    }
}
