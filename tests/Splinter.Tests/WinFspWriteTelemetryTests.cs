using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class WinFspWriteTelemetryTests
{
    private static OverlayIdentity Identity(ISnapshotTree tree) => new(2, "write-telemetry", tree.Snapshot, 64L * 1048576, 65536);
    private static WinFspOverlayFileSystem Measured(IWorkspaceDeltaStore store) =>
        new(store, null, null, new WinFspOverlayFileSystem.WriteTelemetry());

    [Fact]
    public void DefaultFilesystemDoesNotCollectDiagnosticSamples()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows callback contract.");
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        using var store = new JournalDeltaStore(root.Path, Identity(tree), tree, boundedLogBytes: 16 * 1048576);
        var id = store.Create("file", false, 32).Id; var fs = new WinFspOverlayFileSystem(store);
        Write(fs, id, [1]); Assert.Null(fs.ObserveWriteAdmission());
    }

    [Fact]
    public void SequentialCallbacksSeparateOriginalStagedAndChangedBytes()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows callback contract.");
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        using var store = new JournalDeltaStore(root.Path, Identity(tree), tree, boundedLogBytes: 16 * 1048576);
        var id = store.Create("file", false, 32).Id; byte[] data = new byte[8192]; store.Write(id, 0, data);
        data[4096] = 1; var fs = Measured(store);
        Write(fs, id, data); Write(fs, id, data, offset: 8192, constrained: true); Write(fs, id, []);
        var actual = Assert.IsType<WinFspWriteAdmissionObservation>(fs.ObserveWriteAdmission());
        Assert.Equal(3, actual.AcceptedCallbacks); Assert.Equal(3, actual.CompletedCallbacks);
        Assert.Equal(16384, actual.RequestedBytes); Assert.Equal(8192, actual.EffectiveBytes);
        Assert.Equal(8192, actual.StagedBytes); Assert.Equal(8192, actual.SuccessfulBytes);
        Assert.Equal(1, actual.RuntimeStoreIntervals.ChangedPayloadBytes);
        Assert.Equal(1, actual.StoreCalls); Assert.Equal(1, actual.RuntimeStoreIntervals.FlushCalls);
        Assert.Equal(1, actual.MaxQueuedCallbacks); Assert.Equal(3, actual.BatchCallbackBuckets[1]);
        Assert.Equal(1, actual.MaxOutstandingCallbacks);
        Assert.True(actual.StopwatchFrequency > 0); Assert.Equal(0, actual.FailedCallbacks);
        byte[] replay = new byte[8192]; store.Read(id, 0, replay); Assert.Equal(data, replay);
    }

    [Fact]
    public void RecentSamplesAndPathsAreBoundedWithoutLosingTotals()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows callback contract.");
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        using var store = new JournalDeltaStore(root.Path, Identity(tree), tree, boundedLogBytes: 16 * 1048576);
        string part = new('a', 100); store.Create(part, true, 16); store.Create(part + "/" + part, true, 16);
        var id = store.Create(part + "/" + part + "/" + part, false, 32).Id;
        var fs = Measured(store);
        for (int i = 0; i < 100; i++) Write(fs, id, [(byte)i]);
        Assert.Equal(0, fs.SetBasicInfo(id, null!, uint.MaxValue, 0, 0, 0, 0, out _));
        var actual = Assert.IsType<WinFspWriteAdmissionObservation>(fs.ObserveWriteAdmission());
        Assert.Equal(100, actual.CompletedCallbacks); Assert.Equal(100, actual.StoreCalls);
        Assert.Equal(WinFspOverlayFileSystem.WriteTelemetry.CallbackCapacity, actual.RecentCallbacks.Length);
        Assert.Equal(100 - actual.RecentCallbacks.Length, actual.DiscardedCallbackSamples);
        Assert.Equal(100 - actual.RecentBatches.Length, actual.DiscardedBatchSamples);
        Assert.Equal(100 - actual.RecentStoreCalls.Length, actual.DiscardedStoreSamples);
        Assert.Equal(69, actual.RecentCallbacks[0].Ticket); Assert.Equal(100, actual.RecentCallbacks[^1].Ticket);
        Assert.All(actual.RecentCallbacks, row => { Assert.True(row.PathTruncated); Assert.Equal(256, row.Path.Length); });
        Assert.Single(actual.MutationCallbacks, row => row.Operation == "set-basic-info" && row.Calls == 1);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(actual, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal(100, json.RootElement.GetProperty("completedCallbacks").GetInt64());
    }

    [Theory]
    [InlineData(65536, 65, "chunk-limit")]
    [InlineData(1048576, 17, "byte-limit")]
    public void LargeCallbackReportsAdapterLimitAndBoundedCheckpointBarriers(int transferBytes, int count, string reason)
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows callback contract.");
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        using var store = new JournalDeltaStore(root.Path, Identity(tree) with { MaxTransferBytes = transferBytes }, tree,
            boundedLogBytes: transferBytes == 65536 ? 2 * 1048576 : 16 * 1048576);
        var id = store.Create("large", false, 32).Id; var fs = Measured(store);
        byte[] expected = new byte[count * transferBytes]; Array.Fill(expected, (byte)19); Write(fs, id, expected);
        var actual = Assert.IsType<WinFspWriteAdmissionObservation>(fs.ObserveWriteAdmission());
        Assert.Equal(1, actual.CompletedCallbacks); Assert.Equal(count, actual.RangeChunks);
        Assert.Equal(2, actual.StoreCalls); Assert.Equal(1, actual.ChunkLimitSplits + actual.ByteLimitSplits);
        Assert.Equal(new[] { reason, "batch-end" }, actual.RecentStoreCalls.Select(row => row.Reason));
        Assert.True(actual.RuntimeStoreIntervals.FlushCalls > actual.StoreCalls);
        Assert.Equal(expected.Length, actual.RuntimeStoreIntervals.ChangedPayloadBytes);
        byte[] got = new byte[expected.Length];
        for (int at = 0; at < got.Length; at += transferBytes) store.Read(id, at, got.AsSpan(at, transferBytes));
        Assert.Equal(expected, got);
    }

    [Fact]
    public void FailedCallbackRecordsFailureWithoutHidingPoison()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows callback contract.");
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var observer = new FailBeforeFlush();
        using var store = new JournalDeltaStore(root.Path, Identity(tree), tree, observer, boundedLogBytes: 16 * 1048576);
        var id = store.Create("file", false, 32).Id; var fs = Measured(store); observer.Armed = true;
        Assert.Throws<IOException>(() => Write(fs, id, [1]));
        var actual = Assert.IsType<WinFspWriteAdmissionObservation>(fs.ObserveWriteAdmission());
        Assert.Equal(1, actual.FailedCallbacks); Assert.Equal(0, actual.SuccessfulBytes);
        Assert.True(Assert.Single(actual.RecentCallbacks).Failed); Assert.True(Assert.Single(actual.RecentStoreCalls).Failed);
        Assert.Equal("DeltaReconnectRequired", Assert.Throws<WorkspaceException>(() => store.Get(id)).Code);
    }

    [Fact]
    public async Task FixedSequentialAndConcurrentTelemetryCostEvidence()
    {
        string? path = Environment.GetEnvironmentVariable("SPLINTER_WRITE_TELEMETRY_COST_OUTPUT");
        if (path is null) Assert.Skip("Set SPLINTER_WRITE_TELEMETRY_COST_OUTPUT for an opt-in bounded callback overhead comparison.");
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows callback contract.");
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var rows = new List<object>(); byte[] payload = Enumerable.Range(0, 112).Select(i => (byte)i).ToArray();
        foreach (int concurrency in new[] { 1, 8 })
        foreach (bool enabled in new[] { false, true, true, false })
        {
            using var store = new JournalDeltaStore(Path.Combine(root.Path, rows.Count.ToString()), Identity(tree), tree, boundedLogBytes: 16 * 1048576);
            var id = store.Create("file", false, 32).Id;
            var fs = new WinFspOverlayFileSystem(store, null, null, enabled ? new WinFspOverlayFileSystem.WriteTelemetry() : null);
            var before = ((IJournalWriteAccounting)store).ReadWriteAccounting();
            using var ready = new CountdownEvent(concurrency); using var start = new ManualResetEventSlim();
            Task[] writers = Enumerable.Range(0, concurrency).Select(worker => Task.Factory.StartNew(() =>
            {
                ready.Signal(); start.Wait(TestContext.Current.CancellationToken);
                for (int i = worker; i < 512; i += concurrency) Write(fs, id, payload, (ulong)(i * payload.Length));
            }, TestContext.Current.CancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();
            bool allReady = ready.Wait(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
            long allocated = GC.GetTotalAllocatedBytes(true); var watch = Stopwatch.StartNew(); start.Set();
            await Task.WhenAll(writers); watch.Stop(); allocated = GC.GetTotalAllocatedBytes(true) - allocated;
            Assert.True(allReady);
            var difference = ((IJournalWriteAccounting)store).ReadWriteAccounting() - before;
            var telemetry = fs.ObserveWriteAdmission();
            if (concurrency == 1) Assert.Equal(512, difference.FlushCalls);
            else Assert.True(difference.FlushCalls < 512);
            Assert.Equal(512 * payload.Length, difference.ChangedPayloadBytes);
            if (enabled) { Assert.Equal(512, telemetry!.CompletedCallbacks); Assert.Equal(difference, telemetry.RuntimeStoreIntervals); }
            else Assert.Null(telemetry);
            byte[] result = new byte[512 * payload.Length]; store.Read(id, 0, result);
            for (int i = 0; i < 512; i++) Assert.Equal(payload, result.AsSpan(i * payload.Length, payload.Length).ToArray());
            rows.Add(new { enabled, concurrency, seconds = watch.Elapsed.TotalSeconds, allocated, difference,
                observationJsonBytes = telemetry is null ? 0 : JsonSerializer.SerializeToUtf8Bytes(telemetry).Length });
        }
        File.WriteAllText(path, JsonSerializer.Serialize(new { scope = "Sequential/concurrent callbacks plus real durable journal; cumulative process allocations, no mount, Unity or full-workflow claim", rows }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void Write(WinFspOverlayFileSystem fs, Guid id, byte[] bytes, ulong offset = 0, bool constrained = false)
    {
        IntPtr buffer = Marshal.AllocHGlobal(Math.Max(1, bytes.Length));
        try
        {
            Marshal.Copy(bytes, 0, buffer, bytes.Length);
            Assert.Equal(0, fs.Write(id, null!, buffer, offset, (uint)bytes.Length, false, constrained, out _, out _));
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }
    private sealed class FailBeforeFlush : IJournalMutationObserver
    {
        public bool Armed;
        public void Observe(JournalMutationBoundary boundary)
        { if (Armed && boundary.Boundary == "commit.before-flush") throw new IOException("Injected flush failure."); }
    }
}
