using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Fsp;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class RawBatchNativeTests
{
    [Fact]
    public async Task NativeWritersCloseUnmountAndReplayWithFewerFlushes()
    {
        string? reportPath = Environment.GetEnvironmentVariable("SPLINTER_RAW_BATCH_NATIVE_OUTPUT");
        if (reportPath is null) Assert.Skip("Explicit new WinFsp fixture requires SPLINTER_RAW_BATCH_NATIVE_OUTPUT.");
        using var root = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = new OverlayIdentity(2, "raw-batch-native", tree.Snapshot, 64L * 1048576, 65536);
        string delta = Path.Combine(root.Path, "delta"), mount = Path.Combine(root.Path, "view");
        byte[][] expected = Enumerable.Range(0, 8).Select(i => Enumerable.Range(0, 512).Select(j => (byte)(i + j)).ToArray()).ToArray();
        var errors = new ConcurrentBag<Exception>(); object result;
        using (var store = new JournalDeltaStore(delta, identity, tree, boundedLogBytes: 16 * 1048576))
        {
            for (int i = 0; i < 8; i++) store.Create($"file-{i}", false, 32);
            var fs = new WinFspOverlayFileSystem(store, errors.Add); using var host = new FileSystemHost(fs);
            int status = host.MountEx(mount, 8, null, false, 0); Assert.True(status >= 0, "MountEx: 0x" + status.ToString("x8"));
            try
            {
                var before = store.Observe(); using var ready = new CountdownEvent(8); using var start = new ManualResetEventSlim();
                Task[] writers = Enumerable.Range(0, 8).Select(i => Task.Factory.StartNew(() =>
                {
                    using var file = new FileStream(Path.Combine(mount, $"file-{i}"), FileMode.Open, FileAccess.Write,
                        FileShare.Read, 1, FileOptions.WriteThrough);
                    ready.Signal(); start.Wait(TestContext.Current.CancellationToken);
                    for (int pass = 0; pass < 128; pass++) file.Write(expected[i]);
                }, TestContext.Current.CancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();
                bool allReady = ready.Wait(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
                var timer = Stopwatch.StartNew(); start.Set(); await Task.WhenAll(writers); timer.Stop(); Assert.True(allReady);
                Assert.True(SpinWait.SpinUntil(() => fs.RetainedFileContexts == 0, TimeSpan.FromSeconds(10)));
                store.Flush(); var after = store.Observe();
                long commits = after.Metrics!.CommitCount - before.Metrics!.CommitCount, flushes = after.Metrics.FlushCount - before.Metrics.FlushCount;
                Assert.True(commits >= 1024); Assert.True(flushes < commits, $"commits={commits}, flushes={flushes}");
                Assert.Empty(errors); Assert.Equal(0, fs.OpenHandles); Assert.Equal(0, fs.RetainedFileContexts);
                result = new { scope = "Actual WinFsp / fake immutable basis / 8 simultaneous synchronous writers / raw data / normal close-unmount-replay; not Unity timing", seconds = timer.Elapsed.TotalSeconds, commits, flushes, after.PayloadBytes };
            }
            finally { host.Unmount(); }
        }
        using (var replay = new JournalDeltaStore(delta, identity, tree))
        {
            for (int i = 0; i < 8; i++)
            {
                byte[] actual = new byte[512 * 128]; Assert.Equal(actual.Length, replay.Read(replay.Stat($"file-{i}")!.Id, 0, actual));
                for (int pass = 0; pass < 128; pass++) Assert.Equal(expected[i], actual.AsSpan(pass * 512, 512).ToArray());
            }
            Assert.Equal(0, tree.ReadBytes);
        }
        File.WriteAllText(reportPath, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    }
}
