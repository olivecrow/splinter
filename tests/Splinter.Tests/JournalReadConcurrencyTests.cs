using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class JournalReadConcurrencyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BlockedBaseReadAllowsMetadataAndMutationsWithoutMixingFileGenerations(bool optimized)
    {
        using var directory = new TestDirectory(); using var tree = new BlockingTree();
        using var store = Open(directory.Path, tree, optimized);
        var file = store.Stat("base")!;
        store.Write(file.Id, 2, [91, 92]);
        byte[] output = new byte[8];
        var read = Run(() => store.Read(file.Id, 0, output));
        try
        {
            Assert.True(tree.Entered.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
            // These operations must finish while the immutable reader remains
            // blocked. In SJL4 the replacement also reclaims the old capsule.
            await Run(() =>
            {
                Assert.Equal(8, store.Get(file.Id).Length);
                Assert.Single(store.List(""));
                store.Write(file.Id, 2, [81, 82]);
                store.SetLength(file.Id, 3);
                store.Rename(file.Id, "renamed", false);
                store.Delete(file.Id);
                store.ReleaseUnlinkedFile(file.Id);
                Assert.Null(store.Stat("base"));
                Assert.Null(store.Stat("renamed"));
                Assert.NotNull(store.ObserveActivity());
                store.Write(store.Create("other", false, 32).Id, 0, [42]);
            }).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Assert.False(read.IsCompleted);
        }
        finally { tree.Continue.Set(); }
        Assert.Equal(8, await read.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Assert.Equal(new byte[] { 0, 1, 91, 92, 4, 5, 6, 7 }, output);
        Assert.Equal(6, store.Observe().Metrics!.BaseReadBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentCloseWaitsForReadAndRejectsNewOperations(bool failRead)
    {
        using var directory = new TestDirectory(); using var tree = new BlockingTree { FailRead = failRead };
        var store = Open(directory.Path, tree, true);
        var id = store.Stat("base")!.Id;
        var read = Run(() => Record.Exception(() => store.Read(id, 0, new byte[8])));
        Task? close = null, secondClose = null;
        try
        {
            Assert.True(tree.Entered.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
            close = Run(store.Dispose);
            Assert.True(SpinWait.SpinUntil(() =>
            {
                try { store.Get(id); return false; }
                catch (ObjectDisposedException) { return true; }
            }, TimeSpan.FromSeconds(10)));
            using var secondStarted = new ManualResetEventSlim();
            secondClose = Task.Factory.StartNew(() => { secondStarted.Set(); store.Dispose(); }, TestContext.Current.CancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            Assert.True(secondStarted.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
            Assert.Throws<ObjectDisposedException>(() => store.Read(id, 0, new byte[1]));
            Assert.Throws<ObjectDisposedException>(store.Flush);
            Assert.Throws<ObjectDisposedException>(() => store.GetJournalBytes());
            Assert.Throws<ObjectDisposedException>(() => store.ObserveActivity());
            Assert.Throws<ObjectDisposedException>(() => store.Observe());
            Assert.False(close.IsCompleted);
            Assert.NotSame(secondClose, await Task.WhenAny(secondClose, Task.Delay(100, TestContext.Current.CancellationToken)));
            // The writer exclusion fence remains until the in-flight reader
            // completes, even if another caller is also trying to close.
            Assert.Throws<IOException>(() => new FileStream(Path.Combine(directory.Path, "delta.journal"),
                FileMode.Open, FileAccess.ReadWrite, FileShare.Read));
        }
        finally { tree.Continue.Set(); }
        var error = await read.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        if (failRead) Assert.IsType<IOException>(error); else Assert.Null(error);
        await Task.WhenAll(close!, secondClose!).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        store.Dispose();
        using var reopened = Open(directory.Path, tree, true);
        Assert.Equal(8, reopened.Stat("base")!.Length);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedConcurrentMutationInvalidatesReadAndStillDrainsOnClose(bool optimized)
    {
        using var directory = new TestDirectory(); using var tree = new BlockingTree();
        var observer = new FailingMutation();
        using var store = new JournalDeltaStore(directory.Path, Identity(tree), tree, observer, optimized);
        var id = store.Stat("base")!.Id;
        var read = Run(() => Record.Exception(() => store.Read(id, 0, new byte[8])));
        try
        {
            Assert.True(tree.Entered.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
            observer.Fail = true;
            await Run(() => Assert.Throws<IOException>(() => store.Write(id, 0, [9])))
                .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        }
        finally { tree.Continue.Set(); }
        Assert.Equal("DeltaReconnectRequired", Assert.IsType<WorkspaceException>(
            await read.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken)).Code);
        Assert.Null(store.ObserveActivity());
    }

    [Fact]
    public async Task ConcurrentBaseReadersRemainSerializedAndFailuresDoNotStrandTheLifetime()
    {
        using var directory = new TestDirectory(); using var tree = new BlockingTree();
        using var store = Open(directory.Path, tree, true);
        var id = store.Stat("base")!.Id;
        byte[] first = new byte[8], second = new byte[8];
        var a = Run(() => store.Read(id, 0, first));
        Task<int>? b = null;
        try
        {
            Assert.True(tree.Entered.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
            b = Run(() => store.Read(id, 0, second));
            Assert.True(SpinWait.SpinUntil(() => store.ObserveActivity()!.Metrics!.ReadRangeIndex.ReadCalls >= 2,
                TimeSpan.FromSeconds(10)));
            await Run(() => Assert.Equal(8, store.Get(id).Length)).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        }
        finally { tree.Continue.Set(); }
        Assert.Equal(new[] { 8, 8 }, await Task.WhenAll(a, b!).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Assert.Equal(first, second);
        Assert.Equal(1, tree.PeakReads);
        Assert.Equal(16, store.Observe().Metrics!.BaseReadBytes);
    }

    [Fact]
    public async Task NestedCandidateReadAllowsMetadataWithoutInvertingStoreLocks()
    {
        using var directory = new TestDirectory(); using var tree = new BlockingTree();
        string source = Path.Combine(directory.Path, "source"), candidatePath = Path.Combine(directory.Path, "candidate");
        using (var writer = Open(source, tree, true)) writer.Write(writer.Stat("base")!.Id, 2, [91, 92]);
        JournalDeltaStore.ExportCandidate(source, Identity(tree), tree, candidatePath, "nested", []);
        using var candidate = new CandidateSnapshotTree(candidatePath, tree);
        using var store = new JournalDeltaStore(Path.Combine(directory.Path, "top"),
            Identity(tree) with { Id = "top", Snapshot = candidate.Snapshot }, candidate);
        var id = store.Stat("base")!.Id;
        byte[] output = new byte[8];
        var read = Run(() => store.Read(id, 0, output));
        try
        {
            Assert.True(tree.Entered.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
            await Run(() =>
            {
                Assert.Equal(8, store.Stat("base")!.Length);
                Assert.Equal(8, candidate.Stat("base")!.Length);
                Assert.Single(store.List(""));
                store.Write(id, 0, [99]);
            }).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Assert.False(read.IsCompleted);
        }
        finally { tree.Continue.Set(); }
        Assert.Equal(8, await read.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Assert.Equal(new byte[] { 0, 1, 91, 92, 4, 5, 6, 7 }, output);
    }

    private static Task Run(Action action) => Task.Run(action, TestContext.Current.CancellationToken);
    private static Task<T> Run<T>(Func<T> action) => Task.Run(action, TestContext.Current.CancellationToken);

    private static OverlayIdentity Identity(BlockingTree tree) => new(2, "read-concurrency", tree.Snapshot, 4 * 1048576, 1048576);
    private static JournalDeltaStore Open(string path, BlockingTree tree, bool optimized) => new(path, Identity(tree), tree, optimized);

    private sealed class FailingMutation : IJournalMutationObserver
    {
        public bool Fail;
        public void Observe(JournalMutationBoundary boundary)
        {
            if (Fail && boundary.Boundary is "commit.after-flush" or "latest.after-root-publication")
                throw new IOException("Injected acknowledged publication interruption.");
        }
    }

    private sealed class BlockingTree : ISnapshotTree, IDisposable
    {
        public SnapshotReference Snapshot { get; } = new("read-base", "fake-memory", "original", "fixed", DateTimeOffset.UnixEpoch, true, true, "fixture-only");
        public ManualResetEventSlim Entered { get; } = new();
        public ManualResetEventSlim Continue { get; } = new();
        public bool FailRead;
        private int activeReads;
        public int PeakReads;
        public void EnsureAvailable() { }
        public SnapshotEntry? Stat(string path) => path switch
        {
            "" => new("", true, 0, 16, 1, 1, 1),
            "base" => new("base", false, 8, 32, 1, 1, 1),
            _ => null
        };
        public IReadOnlyList<SnapshotEntry> List(string path) => path == "" ? [Stat("base")!] : [];
        public int Read(string path, long offset, Span<byte> destination)
        {
            int active = Interlocked.Increment(ref activeReads);
            Interlocked.Exchange(ref PeakReads, Math.Max(PeakReads, active));
            try
            {
                Entered.Set();
                if (!Continue.Wait(TimeSpan.FromSeconds(20))) throw new TimeoutException("Test reader was not released.");
                if (FailRead) throw new IOException("Injected immutable read failure.");
                int count = (int)Math.Min(destination.Length, 8 - offset);
                for (int i = 0; i < count; i++) destination[i] = (byte)(offset + i);
                return count;
            }
            finally { Interlocked.Decrement(ref activeReads); }
        }
        public void Dispose() { Continue.Set(); Entered.Dispose(); Continue.Dispose(); }
    }
}
