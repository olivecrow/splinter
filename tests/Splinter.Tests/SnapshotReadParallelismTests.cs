using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class SnapshotReadParallelismTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task GraftedReadersOverlapOnlyWhenBothProvidersOptIn(bool sourceConcurrent, bool generatedConcurrent)
    {
        using var directory = new TestDirectory();
        using var source = new GatedTree("source", 10);
        using var generated = new GatedTree("generated", 20);
        var graft = Graft(Expose(source, sourceConcurrent), Expose(generated, generatedConcurrent));
        using var store = Open(directory.Path, graft, true);
        var sourceId = store.Stat("base")!.Id;
        var generatedId = store.Stat("Library/base")!.Id;
        byte[] sourceBytes = new byte[8], generatedBytes = new byte[8];
        var first = Run(() => store.Read(sourceId, 0, sourceBytes));
        Task<int>? second = null;
        try
        {
            Wait(source.Entered);
            second = Run(() => store.Read(generatedId, 0, generatedBytes));
            Assert.True(SpinWait.SpinUntil(() => store.ObserveActivity()!.Metrics!.ReadRangeIndex.ReadCalls == 2,
                TimeSpan.FromSeconds(10)));
            if (sourceConcurrent && generatedConcurrent) Wait(generated.Entered);
            else
            {
                Assert.False(generated.Entered.Wait(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken));
                source.Continue.Set();
                Wait(generated.Entered);
            }
        }
        finally { source.Continue.Set(); generated.Continue.Set(); }
        Assert.Equal(new[] { 8, 8 }, await Task.WhenAll(first, second!).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Assert.Equal(Bytes(10), sourceBytes);
        Assert.Equal(Bytes(20), generatedBytes);
        Assert.Equal(16, store.Observe().Metrics!.BaseReadBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ParallelReadersRetainCapturedPrivateBytesAndEveryCloseDrainsBoth(bool optimized)
    {
        using var directory = new TestDirectory();
        using var source = new GatedTree("source", 10);
        using var generated = new GatedTree("generated", 20);
        var graft = Graft(source, generated);
        using var store = Open(directory.Path, graft, optimized);
        var sourceId = store.Stat("base")!.Id;
        var generatedId = store.Stat("Library/base")!.Id;
        store.Write(sourceId, 2, [91, 92]);
        store.Write(generatedId, 2, [81, 82]);
        byte[] sourceBytes = new byte[8], generatedBytes = new byte[8];
        var first = Run(() => store.Read(sourceId, 0, sourceBytes));
        Task<int>? second = null;
        Task? close = null, otherClose = null;
        try
        {
            Wait(source.Entered);
            second = Run(() => store.Read(generatedId, 0, generatedBytes));
            Wait(generated.Entered);
            store.Write(sourceId, 2, [71, 72]);
            store.SetLength(sourceId, 3);
            store.Rename(sourceId, "renamed", false);
            store.Delete(generatedId);
            store.ReleaseUnlinkedFile(generatedId);
            close = Run(store.Dispose);
            Assert.True(SpinWait.SpinUntil(() =>
            {
                try { store.Get(sourceId); return false; }
                catch (ObjectDisposedException) { return true; }
            }, TimeSpan.FromSeconds(10)));
            using var closeStarted = new ManualResetEventSlim();
            otherClose = Run(() => { closeStarted.Set(); store.Dispose(); });
            Wait(closeStarted);
            Assert.Throws<ObjectDisposedException>(() => store.Read(sourceId, 0, new byte[1]));
            Assert.Throws<IOException>(() => new FileStream(Path.Combine(directory.Path, "delta.journal"),
                FileMode.Open, FileAccess.ReadWrite, FileShare.Read));
            source.Continue.Set();
            Assert.Equal(8, await first.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
            Assert.False(second.IsCompleted);
            Assert.NotSame(close, await Task.WhenAny(close, Task.Delay(100, TestContext.Current.CancellationToken)));
            Assert.False(otherClose.IsCompleted);
        }
        finally { source.Continue.Set(); generated.Continue.Set(); }
        Assert.Equal(8, await second!.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        await Task.WhenAll(close!, otherClose!).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(new byte[] { 10, 11, 91, 92, 14, 15, 16, 17 }, sourceBytes);
        Assert.Equal(new byte[] { 20, 21, 81, 82, 24, 25, 26, 27 }, generatedBytes);
        using var reopened = Open(directory.Path, graft, optimized);
        Assert.Equal(3, reopened.Stat("renamed")!.Length);
        Assert.Null(reopened.Stat("Library/base"));
    }

    [Theory]
    [InlineData("read")]
    [InlineData("availability")]
    [InlineData("poison")]
    public async Task ParallelReadFailuresKeepCompletionChecksAndDoNotStrandClose(string failure)
    {
        using var directory = new TestDirectory();
        using var source = new GatedTree("source", 10);
        using var generated = new GatedTree("generated", 20);
        var graft = Graft(source, generated);
        var observer = new FailingMutation();
        using var store = new JournalDeltaStore(directory.Path, Identity(graft), graft, observer);
        var sourceId = store.Stat("base")!.Id;
        var generatedId = store.Stat("Library/base")!.Id;
        var first = Run(() => Record.Exception(() => store.Read(sourceId, 0, new byte[8])));
        Task<Exception?>? second = null;
        try
        {
            Wait(source.Entered);
            second = Run(() => Record.Exception(() => store.Read(generatedId, 0, new byte[8])));
            Wait(generated.Entered);
            if (failure == "read") source.FailRead = true;
            else if (failure == "availability") generated.Available = false;
            else
            {
                observer.Fail = true;
                Assert.Throws<IOException>(() => store.Write(sourceId, 0, [99]));
            }
        }
        finally { source.Continue.Set(); generated.Continue.Set(); }
        var errors = await Task.WhenAll(first, second!).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        if (failure == "read") { Assert.IsType<IOException>(errors[0]); Assert.Null(errors[1]); }
        else
            Assert.All(errors, error => Assert.Equal(failure == "availability" ? "SnapshotMissing" : "DeltaReconnectRequired",
                Assert.IsType<WorkspaceException>(error).Code));
        await Run(store.Dispose).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task PinnedOwnedDirectoryReadsProgressWhileOtherBasisIsHeldAndKeepWriteExclusion()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Requires Windows owned-directory handles.");
        using var directory = new TestDirectory();
        using var source = new GatedTree("source", 10);
        string root = Path.Combine(directory.Path, "basis"); Directory.CreateDirectory(root);
        byte[] expected = new byte[65536]; new Random(971).NextBytes(expected);
        string path = Path.Combine(root, "base"); File.WriteAllBytes(path, expected);
        var snapshot = new SnapshotReference("native", OwnedDirectorySnapshotTree.ProviderName, root, root,
            DateTimeOffset.UtcNow, true, true, "stopped-original-owned-directory");
        using var generated = new OwnedDirectorySnapshotTree(OwnedDirectorySnapshotTree.Capture(snapshot, "owner"), snapshot);
        var graft = Graft(source, generated);
        using var store = Open(Path.Combine(directory.Path, "delta"), graft, true);
        var sourceId = store.Stat("base")!.Id;
        var generatedId = store.Stat("Library/base")!.Id;
        var heldRead = Run(() => store.Read(sourceId, 0, new byte[8]));
        try
        {
            Wait(source.Entered);
            // Read disjoint offsets through the same pinned handle. A shared
            // file position would corrupt these results under overlap.
            await Task.WhenAll(Enumerable.Range(0, 8).Select(index => Run(() =>
            {
                byte[] bytes = new byte[4096]; int offset = index * 4096;
                Assert.Equal(bytes.Length, store.Read(generatedId, offset, bytes));
                Assert.Equal(expected.AsSpan(offset, bytes.Length).ToArray(), bytes);
            }))).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Assert.False(heldRead.IsCompleted);
            Assert.Throws<IOException>(() => File.WriteAllBytes(path, [9]));
            Assert.Throws<IOException>(() => File.Delete(path));
        }
        finally { source.Continue.Set(); }
        Assert.Equal(8, await heldRead.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Assert.Equal(8 + 8 * 4096, store.Observe().Metrics!.BaseReadBytes);
    }

    private static byte[] Bytes(int start) => Enumerable.Range(start, 8).Select(i => (byte)i).ToArray();
    private static void Wait(ManualResetEventSlim signal) => Assert.True(signal.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
    private static Task Run(Action action) => Task.Run(action, TestContext.Current.CancellationToken);
    private static Task<T> Run<T>(Func<T> action) => Task.Run(action, TestContext.Current.CancellationToken);
    private static ISnapshotTree Expose(GatedTree tree, bool concurrent) => concurrent ? tree : new UnspecifiedConcurrencyTree(tree);
    private static GraftedSnapshotTree Graft(ISnapshotTree source, ISnapshotTree generated) => new(
        new WorkspaceSnapshotBasis.DisposableSnapshotTree(source), new WorkspaceSnapshotBasis.DisposableSnapshotTree(generated),
        new("Library", new("generated", "project", "Library", generated.Snapshot, "receipt", "owner", "manifest")));
    private static OverlayIdentity Identity(ISnapshotTree tree) => new(2, "parallel-bases", tree.Snapshot, 4 * 1048576, 1048576, Graft: tree.Graft);
    private static JournalDeltaStore Open(string path, ISnapshotTree tree, bool optimized) => new(path, Identity(tree), tree, optimized);

    private sealed class UnspecifiedConcurrencyTree(ISnapshotTree inner) : ISnapshotTree
    {
        public SnapshotReference Snapshot => inner.Snapshot;
        public void EnsureAvailable() => inner.EnsureAvailable();
        public SnapshotEntry? Stat(string path) => inner.Stat(path);
        public IReadOnlyList<SnapshotEntry> List(string path) => inner.List(path);
        public int Read(string path, long offset, Span<byte> destination) => inner.Read(path, offset, destination);
    }

    private sealed class FailingMutation : IJournalMutationObserver
    {
        public bool Fail;
        public void Observe(JournalMutationBoundary boundary)
        {
            if (Fail && boundary.Boundary == "latest.after-root-publication") throw new IOException("Injected publication failure.");
        }
    }

    private sealed class GatedTree(string id, byte value) : ISnapshotTree, IDisposable
    {
        public SnapshotReference Snapshot { get; } = new(id, "fake-memory", "original", "fixed", DateTimeOffset.UnixEpoch, true, true, "fixture-only");
        public bool SupportsConcurrentReads => true;
        public ManualResetEventSlim Entered { get; } = new();
        public ManualResetEventSlim Continue { get; } = new();
        public volatile bool Available = true, FailRead;
        public void EnsureAvailable() { if (!Available) throw new WorkspaceException("SnapshotMissing", "Injected missing basis."); }
        public SnapshotEntry? Stat(string path) => path switch
        {
            "" => new("", true, 0, 16, 1, 1, 1),
            "base" => new("base", false, 8, 32, 1, 1, 1),
            _ => null
        };
        public IReadOnlyList<SnapshotEntry> List(string path) => path == "" ? [Stat("base")!] : [];
        public int Read(string path, long offset, Span<byte> destination)
        {
            Entered.Set();
            if (!Continue.Wait(TimeSpan.FromSeconds(20))) throw new TimeoutException("Test reader was not released.");
            if (FailRead) throw new IOException("Injected immutable read failure.");
            int count = (int)Math.Min(destination.Length, 8 - offset);
            for (int i = 0; i < count; i++) destination[i] = (byte)(value + offset + i);
            return count;
        }
        public void Dispose() { Continue.Set(); Entered.Dispose(); Continue.Dispose(); }
    }
}
