using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class CaptureReadOptimizationTests
{
    [Fact(Timeout = 10000)]
    public void FrozenReadsReuseBackingWhileLaterWritesRemainIndependent()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Native capture fences.");
        var ct = TestContext.Current.CancellationToken;
        using var temp = new TestDirectory(); var native = new WindowsJournalFilePins(); var pins = new CountingPins(native);
        var basis = new SnapshotOverlayTests.FakeSnapshotTree(); basis.Files["data"] = new byte[131072];
        string control = Path.Combine(temp.Path, "records", new string('a', 24));
        using var journal = new JournalDeltaStore(Path.Combine(control, "delta"), new OverlayIdentity(3, new string('a', 24), basis.Snapshot), basis, sourceFilePins: native);
        var id = journal.Stat("data")!.Id; byte[] bytes = Enumerable.Repeat((byte)42, 65536).ToArray();
        journal.Write(id, 0, bytes); journal.Write(id, 65536, bytes);
        string captureId = new('b', 24), directory = Path.Combine(control, "captures", captureId);
        var captured = journal.CaptureSource(directory, captureId, []); Assert.NotNull(captured.Paged!.Segments);
        using (var tree = new SourceCaptureTree(directory, basis, pins))
        {
            pins.Verifications = 0; byte[] read = new byte[4096];
            for (int pass = 0; pass < 2; pass++)
                for (int offset = 0; offset < 131072; offset += read.Length)
                { ct.ThrowIfCancellationRequested(); Assert.Equal(read.Length, tree.Read("data", offset, read)); Assert.All(read, b => Assert.Equal(42, b)); }
            Assert.Equal(1, pins.Verifications);
            journal.Write(id, 0, "new"u8);
            Assert.Equal(4096, tree.Read("data", 0, read)); Assert.All(read, b => Assert.Equal(42, b));
        }
        SourceCaptureTree.Complete(directory, native); journal.CompleteSourceCaptureRequest(captureId);
    }

    [Fact(Timeout = 10000)]
    public void CachedReadersFenceWritesEvictIdleAndRevalidateReopenedIdentity()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Native capture fences.");
        using var temp = new TestDirectory(); var pins = new CountingPins(new WindowsJournalFilePins());
        var backings = Enumerable.Range(0, 9).Select(i => Backing(temp.Path, pins, i)).ToArray();
        using var readers = new CaptureBackingReaders(temp.Path, pins);
        using (var first = readers.Acquire(backings[0])) { }
        Assert.ThrowsAny<IOException>(() => File.WriteAllBytes(backings[0].Source, [2]));
        Assert.ThrowsAny<IOException>(() => File.Delete(backings[0].Source));
        using (var again = readers.Acquire(backings[0])) { }
        Assert.Equal(1, pins.Verifications);
        foreach (var backing in backings.Skip(1)) { TestContext.Current.CancellationToken.ThrowIfCancellationRequested(); using var entry = readers.Acquire(backing); }
        File.Delete(backings[0].Source); File.WriteAllBytes(backings[0].Source, [2]);
        Assert.Throws<WorkspaceException>(() => readers.Acquire(backings[0]));
        readers.Dispose();
        foreach (var backing in backings) File.WriteAllBytes(backing.Source, [3]);
    }

    [Fact(Timeout = 10000)]
    public async Task BusyReadersAreNotEvictedAndCloseDrainsConcurrentLeases()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Native capture fences.");
        using var temp = new TestDirectory(); var pins = new CountingPins(new WindowsJournalFilePins());
        var backings = Enumerable.Range(0, 9).Select(i => Backing(temp.Path, pins, i)).ToArray();
        using var readers = new CaptureBackingReaders(temp.Path, pins);
        var leases = backings.Select(readers.Acquire).ToArray();
        using (var shared = readers.Acquire(backings[0])) Assert.Same(leases[0].Stream, shared.Stream);
        Assert.Equal(9, pins.Verifications);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var close = Task.Run(() => { started.SetResult(); readers.Dispose(); }, TestContext.Current.CancellationToken);
        try
        {
            await started.Task;
            Assert.False(close.IsCompleted);
            foreach (var lease in leases)
            { byte[] bytes = new byte[1]; Assert.Equal(1, RandomAccess.Read(lease.Stream.SafeFileHandle, bytes, 0)); }
        }
        finally { foreach (var lease in leases) lease.Dispose(); }
        await close.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.Throws<ObjectDisposedException>(() => readers.Acquire(backings[0]));
    }

    private static CapturedBacking Backing(string directory, IJournalFilePins pins, int i)
    {
        string path = Path.Combine(directory, i + ".pin"); File.WriteAllBytes(path, [1]);
        return new(path, Path.GetFileName(path), pins.Identify(path), 1);
    }
    private sealed class CountingPins(IJournalFilePins inner) : IJournalFilePins
    {
        public int Verifications;
        public string Identify(string path) => inner.Identify(path);
        public void Verify(FileStream input, string identity) { Verifications++; inner.Verify(input, identity); }
        public void Pin(string source, string target, string identity) => inner.Pin(source, target, identity);
        public bool DetachBeforeTruncate(string path, long length) => inner.DetachBeforeTruncate(path, length);
    }
}
