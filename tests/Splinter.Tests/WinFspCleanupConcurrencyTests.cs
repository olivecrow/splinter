using System.Runtime.InteropServices;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class WinFspCleanupConcurrencyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NonDeletingCleanupCanPassAnActiveWriteButDeleteAndCloseStillDrain(bool delete)
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows callback contract.");
        using var root = new TestDirectory();
        var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        using var store = new JournalDeltaStore(root.Path, new(2, "cleanup", tree.Snapshot, 4 * 1024 * 1024, 65536), tree);
        var entry = store.Create("file", false, 32);
        using var observer = new BlockWrite();
        var fs = new WinFspOverlayFileSystem(store, mutationObserver: observer);
        Assert.Equal(0, fs.Open("file", 0, 0, out var node, out var context, out _, out _));
        Assert.Equal(0, fs.Open("file", 0, 0, out var otherNode, out var otherContext, out _, out _));
        IntPtr buffer = Marshal.AllocHGlobal(1);
        Marshal.WriteByte(buffer, 42);
        Task writer = Run(() => Assert.Equal(0, fs.Write(node, context, buffer, 0, 1, false, false, out _, out _)));
        Task? cleanup = null, close = null;
        try
        {
            Assert.True(observer.Entered.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
            using var cleanupStarted = new ManualResetEventSlim();
            cleanup = Run(() =>
            {
                cleanupStarted.Set();
                fs.Cleanup(node, context, "file", delete ? Fsp.FileSystemBase.CleanupDelete : 0);
            });
            Assert.True(cleanupStarted.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
            if (delete)
            {
                // A delete cannot pass a write that has not reached its durable effect.
                Assert.NotSame(cleanup, await Task.WhenAny(cleanup, Task.Delay(100, TestContext.Current.CancellationToken)));
                Assert.Equal(2, fs.OpenHandles);
            }
            else
            {
                await cleanup.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                fs.Cleanup(node, context, "file", 0); // counter remains exactly once
                Assert.Equal(1, fs.OpenHandles);
                Assert.Equal(2, fs.RetainedFileContexts);
                using var closeStarted = new ManualResetEventSlim();
                close = Run(() => { closeStarted.Set(); fs.Close(node, context); });
                Assert.True(closeStarted.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
                Assert.NotSame(close, await Task.WhenAny(close, Task.Delay(100, TestContext.Current.CancellationToken)));
                Assert.Equal(2, fs.RetainedFileContexts);
            }
        }
        finally
        {
            observer.Proceed.Set();
            await writer.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            if (cleanup is not null) await cleanup.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            if (close is not null) await close.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Marshal.FreeHGlobal(buffer);
        }
        if (delete)
        {
            Assert.Null(store.Stat("file"));
            fs.Close(node, context);
        }
        else Assert.NotNull(store.Stat("file"));
        // The second retained context remains readable even after unlink and the
        // first Close, and is released only by its own Close.
        byte[] actual = new byte[1];
        Assert.Equal(1, store.Read(entry.Id, 0, actual));
        Assert.Equal(42, actual[0]);
        Assert.Equal(1, fs.OpenHandles);
        Assert.Equal(1, fs.RetainedFileContexts);
        fs.Close(otherNode, otherContext);
        Assert.Equal(0, fs.OpenHandles);
        Assert.Equal(0, fs.RetainedFileContexts);
    }

    private static Task Run(Action action) => Task.Factory.StartNew(action, TestContext.Current.CancellationToken,
        TaskCreationOptions.LongRunning, TaskScheduler.Default);
    private sealed class BlockWrite : IOverlayMutationObserver, IDisposable
    {
        public ManualResetEventSlim Entered { get; } = new();
        public ManualResetEventSlim Proceed { get; } = new();
        public void Observe(OverlayMutationBoundary boundary)
        {
            if (boundary.Boundary != "callback.before-effect" || boundary.Operation != "write") return;
            Entered.Set();
            if (!Proceed.Wait(TimeSpan.FromSeconds(20))) throw new TimeoutException("Blocked test writer was not released.");
        }
        public void Dispose() { Entered.Dispose(); Proceed.Dispose(); }
    }
}
