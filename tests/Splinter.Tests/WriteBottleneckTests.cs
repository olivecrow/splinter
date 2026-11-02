using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;
using System.Runtime.InteropServices;

namespace Splinter.Tests;

public sealed class WriteBottleneckTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CommonMixedPortKeepsCallbackOrderAndOneDurableBarrier(bool runtime)
    {
        using var root = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = new OverlayIdentity(2, "mixed-port", basis.Snapshot, long.MaxValue, 65536);
        using var writer = new JournalDeltaStore(root.Path, identity, basis, optimizeJournal: false);
        Guid id = writer.Create("file", false, 32).Id; long flushes = writer.Observe().Metrics!.FlushCount;
        IWorkspaceMutationBatch batch = runtime ? writer : new LiteralMixed(); var order = new List<int>();
        batch.ApplyMutationBatch([() => { order.Add(1); if (runtime) writer.Write(id, 0, [1, 2, 3]); },
            () => { order.Add(2); if (runtime) writer.SetLength(id, 2); },
            () => { order.Add(3); if (runtime) writer.SetMetadata(id, 33, 1, 1, 3); }]);
        Assert.Equal(new[] { 1, 2, 3 }, order);
        if (runtime)
        {
            Assert.Equal(flushes + 1, writer.Observe().Metrics!.FlushCount); writer.Dispose();
            using var replay = new JournalDeltaStore(root.Path, identity, basis, optimizeJournal: false);
            byte[] data = new byte[3]; Assert.Equal(2, replay.Read(id, 0, data)); Assert.Equal(new byte[] { 1, 2, 0 }, data);
            Assert.Equal(33u, replay.Get(id).Attributes);
        }
    }

    [Theory(Timeout = 10000)]
    [InlineData(false)] [InlineData(true)]
    public async Task MixedNativeCallbacksShareFlushAndFailWithoutEarlyAcknowledgment(bool fail)
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows callbacks.");
        using var root = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree(); using var pause = new MixedPause(fail);
        var identity = new OverlayIdentity(3, "mixed-native", basis.Snapshot, long.MaxValue, 65536);
        using var writer = new JournalDeltaStore(root.Path, identity, basis, pause, sourceFilePins: new WindowsJournalFilePins());
        Guid firstId = writer.Create("first", false, 32).Id, writeId = writer.Create("write", false, 32).Id, metaId = writer.Create("meta", false, 32).Id;
        var fs = new WinFspOverlayFileSystem(writer);
        object[] contexts = new object[3]; string[] names = ["first", "write", "meta"]; Guid[] opened = [firstId, writeId, metaId];
        for (int i = 0; i < names.Length; i++) Assert.Equal(0, fs.Open(names[i], 0, 0, out _, out contexts[i], out _, out _));
        long flushes = writer.Observe().Metrics!.FlushCount; pause.Armed = true;
        Task first = Task.Factory.StartNew(() => Assert.Equal(0, fs.SetFileSize(firstId, null!, 5, false, out _)),
            TestContext.Current.CancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        Task[] followers = [];
        try
        {
            Assert.True(pause.Entered.Wait(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
            Task write = Task.Factory.StartNew(() =>
            {
                nint input = Marshal.AllocHGlobal(3);
                try
                {
                    Marshal.Copy(new byte[] { 7, 8, 9 }, 0, input, 3);
                    var error = Record.Exception(() => fs.Write(writeId, null!, input, 0, 3, false, false, out _, out _));
                    if (fail) Assert.IsType<IOException>(error); else Assert.Null(error);
                }
                finally { Marshal.FreeHGlobal(input); }
            }, TestContext.Current.CancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            followers = [write]; Assert.True(SpinWait.SpinUntil(() => fs.PendingWriteCount == 1, TimeSpan.FromSeconds(2)));
            Task meta = Task.Factory.StartNew(() =>
            {
                var error = Record.Exception(() => fs.SetBasicInfo(metaId, null!, 33, 1, 1, 3, 0, out _));
                if (fail) Assert.IsType<IOException>(error); else Assert.Null(error);
            }, TestContext.Current.CancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            followers = [write, meta]; Assert.True(SpinWait.SpinUntil(() => fs.PendingMetadataCount == 1, TimeSpan.FromSeconds(2)));
            Assert.All(followers, t => Assert.False(t.IsCompleted));
        }
        finally { pause.Proceed.Set(); await first; await Task.WhenAll(followers); }
        Assert.Equal(flushes + (fail ? 1 : 2), writer.Observe().Metrics!.FlushCount);
        if (fail) Assert.Equal("DeltaReconnectRequired", Assert.Throws<WorkspaceException>(() => writer.Get(writeId)).Code);
        else
        {
            byte[] bytes = new byte[3]; Assert.Equal(3, writer.Read(writeId, 0, bytes)); Assert.Equal(new byte[] { 7, 8, 9 }, bytes);
            Assert.Equal(33u, writer.Get(metaId).Attributes); Assert.Equal(0, fs.Flush(writeId, null!, out _));
        }
        for (int i = 0; i < opened.Length; i++)
        {
            if (fail) Assert.IsType<WorkspaceException>(Record.Exception(() => fs.Close(opened[i], contexts[i])));
            else fs.Close(opened[i], contexts[i]);
        }
        Assert.Equal(0, fs.OpenHandles); Assert.Equal(0, fs.RetainedFileContexts);
    }

    private sealed class LiteralMixed : IWorkspaceMutationBatch
    { public void ApplyMutationBatch(IReadOnlyList<Action> actions) { foreach (var action in actions) action(); } }
    private sealed class MixedPause(bool fail) : IJournalMutationObserver, IDisposable
    {
        public bool Armed; private int calls; public readonly ManualResetEventSlim Entered = new(), Proceed = new();
        public void Observe(JournalMutationBoundary point)
        {
            if (!Armed || point.Boundary != "commit.before-flush") return;
            int at = Interlocked.Increment(ref calls);
            if (at == 1) { Entered.Set(); if (!Proceed.Wait(TimeSpan.FromSeconds(4))) throw new TimeoutException("Release mixed batch."); }
            else if (fail && at == 2) throw new IOException("Injected shared flush failure.");
        }
        public void Dispose() { Entered.Dispose(); Proceed.Dispose(); }
    }

    [Fact(Timeout = 10000)]
    public async Task QueuedOpenKeepsHandlesPrivateUntilThePriorDurableBatchCompletes()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows callbacks.");
        using var root = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree(); using var pause = new Pause();
        basis.Files["file"] = [1, 2, 3];
        var identity = new OverlayIdentity(3, "queued-open", basis.Snapshot, long.MaxValue, 65536);
        using var writer = new JournalDeltaStore(root.Path, identity, basis, pause, sourceFilePins: new WindowsJournalFilePins());
        Guid id = writer.Stat("file")!.Id; var fs = new WinFspOverlayFileSystem(writer); pause.Armed = true;
        Task first = Task.Factory.StartNew(() => Assert.Equal(0, fs.SetFileSize(id, null!, 5, false, out _)),
            TestContext.Current.CancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        object node = null!, context = null!;
        Task? open = null;
        try
        {
            Assert.True(pause.Entered.Wait(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
            open = Task.Factory.StartNew(() => Assert.Equal(0, fs.Open("file", 0, 0, out node, out context, out _, out _)),
                TestContext.Current.CancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            Assert.True(SpinWait.SpinUntil(() => fs.PendingMetadataCount == 1, TimeSpan.FromSeconds(2)));
            Assert.Equal(0, fs.OpenHandles); Assert.False(open.IsCompleted);
        }
        finally { pause.Proceed.Set(); await first; if (open is not null) await open; }
        Assert.Equal(id, node); Assert.Equal(1, fs.OpenHandles); fs.Close(node, context);
        Assert.Equal(0, fs.OpenHandles); Assert.Equal(0, fs.RetainedFileContexts);
        Assert.NotEqual(0, fs.Open("missing", 0, 0, out _, out _, out _, out _)); Assert.Equal(0, fs.OpenHandles);
    }

    [Fact(Timeout = 10000)]
    public async Task ConcurrentOverwritesQueueAndAcknowledgeOnlyAfterTheirSharedFlush()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows callbacks.");
        using var root = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree(); using var pause = new Pause();
        for (int i = 0; i < 9; i++) basis.Files["file-" + i] = [1, 2, 3];
        var identity = new OverlayIdentity(3, "overwrite-queue", basis.Snapshot, long.MaxValue, 65536);
        Guid[] ids; SnapshotDeltaSeal seal;
        using (var writer = new JournalDeltaStore(root.Path, identity, basis, pause, sourceFilePins: new WindowsJournalFilePins()))
        {
            ids = Enumerable.Range(0, 9).Select(i => writer.Stat("file-" + i)!.Id).ToArray();
            var fs = new WinFspOverlayFileSystem(writer); object[] contexts = new object[9];
            for (int i = 0; i < ids.Length; i++) Assert.Equal(0, fs.Open("file-" + i, 0, 0, out _, out contexts[i], out _, out _));
            long flushes = writer.Observe().Metrics!.FlushCount; pause.Armed = true;
            Task Call(int i) => Task.Factory.StartNew(() => Assert.Equal(0, fs.Overwrite(ids[i], null!, 32, true, 0, out _)),
                TestContext.Current.CancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            Task first = Call(0); Task[] followers = [];
            try
            {
                Assert.True(pause.Entered.Wait(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
                followers = Enumerable.Range(1, 8).Select(Call).ToArray();
                Assert.True(SpinWait.SpinUntil(() => fs.PendingMetadataCount == 8, TimeSpan.FromSeconds(2)));
                Assert.All(followers, t => Assert.False(t.IsCompleted));
            }
            finally { pause.Proceed.Set(); await first; await Task.WhenAll(followers); }
            Assert.Equal(flushes + 2, writer.Observe().Metrics!.FlushCount);
            Assert.All(ids, id => { Assert.Equal(0, writer.Get(id).Length); Assert.Equal(0, writer.Get(id).BaseLength); });
            for (int i = 0; i < ids.Length; i++) fs.Close(ids[i], contexts[i]);
            Assert.Equal(0, fs.RetainedFileContexts);
            seal = writer.Seal();
        }
        using var replay = new JournalDeltaStore(root.Path, identity, basis, sourceFilePins: new WindowsJournalFilePins());
        Assert.Equal(seal, replay.Seal()); Assert.All(ids, id => Assert.Equal(0, replay.Get(id).Length));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SegmentedEmptyAndClippedExtentsRemainExactAfterExtension(bool privateBytes)
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Native index pins.");
        using var root = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree(); basis.Files["file"] = [1, 2, 3, 4];
        var identity = new OverlayIdentity(3, "truncate-unchanged", basis.Snapshot, long.MaxValue, 65536); Guid id;
        using (var writer = new JournalDeltaStore(root.Path, identity, basis, sourceFilePins: new WindowsJournalFilePins()))
        {
            id = writer.Stat("file")!.Id;
            if (privateBytes) writer.Write(id, 0, [5, 6, 7, 8]);
            writer.SetLengthAndMetadata(id, privateBytes ? 2 : 0, 32, 1, 1, 2);
            writer.SetLength(id, 4); byte[] actual = new byte[4]; Assert.Equal(4, writer.Read(id, 0, actual));
            Assert.Equal(privateBytes ? new byte[] { 5, 6, 0, 0 } : new byte[4], actual);
        }
        using var replay = new JournalDeltaStore(root.Path, identity, basis, sourceFilePins: new WindowsJournalFilePins());
        byte[] bytes = new byte[4]; Assert.Equal(4, replay.Read(id, 0, bytes));
        Assert.Equal(privateBytes ? new byte[] { 5, 6, 0, 0 } : new byte[4], bytes);
    }

    [Theory(Timeout = 15000)]
    [InlineData(false)] [InlineData(true)]
    public void ShadowTailBatchKeepsRenameTruncateDeleteAndInterruptedRecovery(bool interrupt)
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Native index pins.");
        using var root = new TestDirectory(); var basis = new SnapshotOverlayTests.FakeSnapshotTree();
        for (int i = 0; i < 72; i++) basis.Files["file-" + i] = [1, 2, 3, 4];
        var identity = new OverlayIdentity(3, "shadow-batches", basis.Snapshot, long.MaxValue, 65536); SnapshotDeltaSeal expected;
        using (var writer = new JournalDeltaStore(root.Path, identity, basis, sourceFilePins: new WindowsJournalFilePins()))
        {
            Guid[] ids = Enumerable.Range(0, 72).Select(i => writer.Stat("file-" + i)!.Id).ToArray(); bool appended = false;
            void Boundary(string point)
            {
                if (point == "metadata.repack-payload-durable" && !appended)
                {
                    appended = true;
                    for (int i = 0; i < 72; i++)
                    { writer.SetLengthAndMetadata(ids[i], 0, 32, 1, 1, 2); writer.Write(ids[i], 0, [(byte)i, 9, 8]); writer.SetMetadata(ids[i], 33, 1, 1, 3); }
                    writer.Rename(ids[0], "renamed", false); writer.SetLength(ids[1], 1); writer.Delete(ids[2]);
                }
                if (interrupt && point == "metadata.repack-before-publication") throw new OperationCanceledException("Injected before durable switch.");
            }
            if (interrupt) Assert.Throws<OperationCanceledException>(() => writer.CollectLiveMetadata(true, TestContext.Current.CancellationToken, Boundary));
            else writer.CollectLiveMetadata(true, TestContext.Current.CancellationToken, Boundary);
            Assert.True(appended); expected = writer.Seal();
            writer.CollectLiveMetadata(true, TestContext.Current.CancellationToken);
            Assert.Equal(expected, writer.Seal()); Assert.Null(writer.Stat("file-0")); Assert.NotNull(writer.Stat("renamed")); Assert.Null(writer.Stat("file-2"));
            byte[] bytes = new byte[3]; Assert.Equal(1, writer.Read(ids[1], 0, bytes)); Assert.Equal((byte)1, bytes[0]);
        }
        using var replay = new JournalDeltaStore(root.Path, identity, basis, sourceFilePins: new WindowsJournalFilePins());
        Assert.Equal(expected, replay.Seal()); Assert.NotNull(replay.Stat("renamed")); Assert.Null(replay.Stat("file-2"));
    }

    private sealed class Pause : IJournalMutationObserver, IDisposable
    {
        public bool Armed; public readonly ManualResetEventSlim Entered = new(), Proceed = new();
        public void Observe(JournalMutationBoundary point)
        {
            if (!Armed || point.Boundary != "commit.before-flush") return; Armed = false; Entered.Set();
            if (!Proceed.Wait(TimeSpan.FromSeconds(4))) throw new TimeoutException("Release the local probe.");
        }
        public void Dispose() { Entered.Dispose(); Proceed.Dispose(); }
    }
}
