using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Splinter.Core;
using Splinter.Runtime;
using Splinter.Platform.Windows;

namespace Splinter.Tests;

public sealed class WriteBatchTests
{
    private static OverlayIdentity Identity(ISnapshotTree tree) => new(2, "write-batch", tree.Snapshot, 64L * 1048576, 65536);

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OrderedCommonBatchContractKeepsOverlapsAndOtherFiles(bool runtime)
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        using var store = new JournalDeltaStore(root.Path, Identity(tree), tree, boundedLogBytes: 16 * 1048576);
        Guid a = store.Create("a", false, 32).Id, b = store.Create("b", false, 32).Id;
        var fake = new LiteralBatch(); IWorkspaceWriteBatch target = runtime ? store : fake;
        var before = store.Observe();
        target.WriteBatch([new(a, 0, new byte[] { 1, 2, 3 }), new(b, 0, new byte[] { 9 }), new(a, 1, new byte[] { 7, 8 })]);
        byte[] Read(Guid id, int length) { byte[] data = new byte[length]; if (runtime) store.Read(id, 0, data); else fake.Files[id].CopyTo(data, 0); return data; }
        Assert.Equal(new byte[] { 1, 7, 8 }, Read(a, 3)); Assert.Equal(new byte[] { 9 }, Read(b, 1));
        if (runtime)
        {
            Assert.Equal(before.Metrics!.FlushCount + 1, store.Observe().Metrics!.FlushCount);
            Assert.Equal(before.DurableSequence + 3, store.Observe().DurableSequence);
            store.Dispose();
            using var replay = new JournalDeltaStore(root.Path, Identity(tree), tree);
            byte[] data = new byte[3]; replay.Read(a, 0, data); Assert.Equal(new byte[] { 1, 7, 8 }, data);
        }
    }

    [Fact]
    public void EmptyAndOversizedBatchesDoNotWriteAndSingleWritesStayDurable()
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        using var store = new JournalDeltaStore(root.Path, Identity(tree), tree, optimizeJournal: false);
        Guid id = store.Create("a", false, 32).Id; var before = store.Observe(); store.WriteBatch([]);
        Assert.Throws<ArgumentException>(() => store.WriteBatch(Enumerable.Range(0, 65).Select(_ => new WorkspaceRangeWrite(id, 0, new byte[1])).ToArray()));
        Assert.Throws<ArgumentException>(() => store.WriteBatch([new(id, 0, new byte[IWorkspaceWriteBatch.MaximumBytes + 1])]));
        Assert.Equal(before.DurableSequence, store.Observe().DurableSequence);
        Assert.Equal(before.Metrics!.FlushCount, store.Observe().Metrics!.FlushCount);
        store.Write(id, 0, [1]); Assert.Equal(before.Metrics.FlushCount + 1, store.Observe().Metrics!.FlushCount);
    }

    [Fact]
    public void BatchCanCrossBoundedCheckpointsWithoutChangingRecoveryLimits()
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        Guid id; byte[] expected = Enumerable.Range(0, 64 * 512).Select(i => (byte)(i / 512)).ToArray();
        using (var store = new JournalDeltaStore(root.Path, Identity(tree), tree, boundedLogBytes: 8192))
        {
            id = store.Create("a", false, 32).Id;
            store.WriteBatch(Enumerable.Range(0, 64).Select(i => new WorkspaceRangeWrite(id, i * 512, expected.AsMemory(i * 512, 512))).ToArray());
            Assert.Equal(65, store.Observe().DurableSequence);
            Assert.True(store.Observe().Metrics!.FlushCount < 65);
        }
        using var replay = new JournalDeltaStore(root.Path, Identity(tree), tree);
        byte[] actual = new byte[expected.Length]; replay.Read(id, 0, actual); Assert.Equal(expected, actual);
        Assert.Equal(65, replay.Observe().DurableSequence); Assert.Equal(0, tree.ReadBytes);
    }

    [Theory]
    [InlineData("commit.after-header")] [InlineData("commit.before-flush")] [InlineData("commit.after-flush")]
    public void FailedBatchNeverAcknowledgesOrExposesPrivateAppliedState(string boundary)
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var observer = new GateObserver(boundary) { Fail = true };
        Guid id;
        using (var store = new JournalDeltaStore(root.Path, Identity(tree), tree, observer, optimizeJournal: false))
        {
            id = store.Create("a", false, 32).Id; store.Write(id, 0, [1]); observer.Armed = true;
            Assert.Throws<IOException>(() => store.WriteBatch([new(id, 1, new byte[] { 2 }), new(id, 2, new byte[] { 3 })]));
            Assert.Equal("DeltaReconnectRequired", Assert.Throws<WorkspaceException>(() => store.Read(id, 0, new byte[3])).Code);
        }
        using var replay = new JournalDeltaStore(root.Path, Identity(tree), tree, optimizeJournal: false);
        byte[] data = new byte[3]; int count = replay.Read(id, 0, data);
        Assert.Equal(1, data[0]); Assert.Contains(count, new[] { 1, 3 });
        if (count == 3) Assert.Equal(new byte[] { 1, 2, 3 }, data);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ReadersAndDisposeCannotPassAnUnfinishedDurabilityBarrier(bool dispose)
    {
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        using var observer = new GateObserver("commit.before-flush");
        var store = new JournalDeltaStore(root.Path, Identity(tree), tree, observer, optimizeJournal: false);
        Guid id = store.Create("a", false, 32).Id; observer.Armed = true;
        var writer = Run(() => store.WriteBatch([new(id, 0, new byte[] { 1 }), new(id, 1, new byte[] { 2 })]));
        Task? reader = null;
        try
        {
            Assert.True(observer.Entered.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
            reader = Run(() => { if (dispose) store.Dispose(); else { byte[] data = new byte[2]; store.Read(id, 0, data); Assert.Equal(new byte[] { 1, 2 }, data); } });
            Assert.NotSame(reader, await Task.WhenAny(reader, Task.Delay(100, TestContext.Current.CancellationToken)));
            Assert.False(writer.IsCompleted);
        }
        finally { observer.Proceed.Set(); await writer; if (reader is not null) await reader; store.Dispose(); }
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)] [InlineData(false, true)]
    public async Task WindowsCallbacksAccumulateBeforeMutationGateAndShareTheNextFlush(bool explicitFlush, bool interruptFollower)
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows callback contract.");
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        using var observer = new GateObserver("commit.before-flush");
        using var store = new JournalDeltaStore(root.Path, Identity(tree), tree, observer, boundedLogBytes: 16 * 1048576);
        Guid id = store.Create("a", false, 32).Id;
        var fs = new WinFspOverlayFileSystem(store); long before = store.Observe().Metrics!.FlushCount;
        observer.Armed = true;
        Thread? follower = null;
        Task Write(int value) => Run(() =>
        {
            IntPtr buffer = Marshal.AllocHGlobal(1);
            try
            {
                if (value == 1) Volatile.Write(ref follower, Thread.CurrentThread);
                Marshal.WriteByte(buffer, (byte)value);
                var error = Record.Exception(() => { Assert.Equal(0, fs.Write(id, null!, buffer, 0, 1, true, false, out uint written, out _)); Assert.Equal(1u, written); });
                if (interruptFollower && value == 1) Assert.IsType<ThreadInterruptedException>(error); else Assert.Null(error);
            }
            finally { Marshal.FreeHGlobal(buffer); }
        });
        var first = Write(0); Task[] others = []; Task? flush = null;
        try
        {
            Assert.True(observer.Entered.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
            others = Enumerable.Range(1, 8).Select(Write).ToArray();
            Assert.True(SpinWait.SpinUntil(() => fs.PendingWriteCount == 8, TimeSpan.FromSeconds(10)));
            if (interruptFollower)
            {
                Volatile.Read(ref follower)!.Interrupt();
                await Task.Delay(100, TestContext.Current.CancellationToken);
            }
            Assert.All(others, task => Assert.False(task.IsCompleted));
            if (explicitFlush)
            {
                flush = Run(() => { Assert.Equal(0, fs.Flush(id, null!, out var info)); Assert.Equal(9UL, info.FileSize); });
                Assert.NotSame(flush, await Task.WhenAny(flush, Task.Delay(100, TestContext.Current.CancellationToken)));
            }
        }
        finally { observer.Proceed.Set(); await first; await Task.WhenAll(others); if (flush is not null) await flush; }
        Assert.Equal(before + (explicitFlush ? 3 : 2), store.Observe().Metrics!.FlushCount);
        byte[] bytes = new byte[9]; Assert.Equal(9, store.Read(id, 0, bytes)); Assert.Equal(0, bytes[0]);
        Assert.Equal(Enumerable.Range(0, 9).Select(i => (byte)i), bytes.Order());
    }

    [Fact]
    public void SerialVersusBatchCostEvidence()
    {
        string? path = Environment.GetEnvironmentVariable("SPLINTER_RAW_BATCH_COST_OUTPUT");
        if (path is null) Assert.Skip("Set SPLINTER_RAW_BATCH_COST_OUTPUT for fixed-input component timing.");
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var rows = new List<object>(); byte[] payload = Enumerable.Range(0, 112).Select(i => (byte)i).ToArray();
        foreach (int size in new[] { 1, 16, 16, 1 })
        {
            string directory = Path.Combine(root.Path, rows.Count.ToString()); Guid id;
            using (var store = new JournalDeltaStore(directory, Identity(tree), tree, boundedLogBytes: 16 * 1048576))
            {
                id = store.Create("a", false, 32).Id; var before = store.Observe();
                var time = Stopwatch.StartNew();
                for (int i = 0; i < 2048; i += size)
                    store.WriteBatch(Enumerable.Range(i, size).Select(j => new WorkspaceRangeWrite(id, j * 112L, payload)).ToArray());
                time.Stop(); var after = store.Observe();
                rows.Add(new { batchSize = size, seconds = time.Elapsed.TotalSeconds, commits = after.DurableSequence - before.DurableSequence,
                    flushes = after.Metrics!.FlushCount - before.Metrics!.FlushCount, payloadBytes = after.PayloadBytes });
            }
            using var replay = new JournalDeltaStore(directory, Identity(tree), tree);
            byte[] data = new byte[2048 * 112]; Assert.Equal(data.Length, replay.Read(id, 0, data));
            for (int i = 0; i < 2048; i++) Assert.Equal(payload, data.AsSpan(i * 112, 112).ToArray());
        }
        File.WriteAllText(path, JsonSerializer.Serialize(new { scope = "Raw SJR2 fixed input; explicit supplied batches, not observed Unity concurrency or full lifecycle", rows }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static Task Run(Action action) => Task.Factory.StartNew(action, TestContext.Current.CancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    private sealed class LiteralBatch : IWorkspaceWriteBatch
    {
        public Dictionary<Guid, byte[]> Files { get; } = [];
        public void WriteBatch(IReadOnlyList<WorkspaceRangeWrite> writes)
        {
            foreach (var write in writes)
            {
                byte[] bytes = Files.GetValueOrDefault(write.Id) ?? [];
                if (bytes.Length < write.Offset + write.Data.Length) Array.Resize(ref bytes, checked((int)write.Offset + write.Data.Length));
                write.Data.Span.CopyTo(bytes.AsSpan((int)write.Offset)); Files[write.Id] = bytes;
            }
        }
    }
    private sealed class GateObserver(string boundary) : IJournalMutationObserver, IDisposable
    {
        public bool Armed, Fail;
        public ManualResetEventSlim Entered { get; } = new();
        public ManualResetEventSlim Proceed { get; } = new();
        public void Observe(JournalMutationBoundary value)
        {
            if (!Armed || value.Boundary != boundary) return;
            Armed = false; Entered.Set();
            if (Fail) throw new IOException("Injected write batch boundary failure.");
            if (!Proceed.Wait(TimeSpan.FromSeconds(20))) throw new TimeoutException("Release the test batch.");
        }
        public void Dispose() { Entered.Dispose(); Proceed.Dispose(); }
    }
}
