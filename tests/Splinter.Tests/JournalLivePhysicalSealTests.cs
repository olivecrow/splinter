using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class JournalLivePhysicalSealTests
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(5);
    private static readonly OverlayFlushRequest Request = new("fixture-token", "fixture-seal", true, 42, 101);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LiveSealWaitsForPublishedRollAndRefusesFailedIntent(bool fail)
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "The test requires native Windows backing identities.");
        using var fixture = new Fixture();
        var ct = TestContext.Current.CancellationToken;
        SnapshotDeltaSeal before;
        using (var store = fixture.Open())
        {
            var target = store.Create("target", false, 32);
            byte[] bytes = new byte[3072];
            new Random(321).NextBytes(bytes);
            store.Write(target.Id, 0, bytes);
            before = store.Seal();
            fixture.Pins.ArmRoll(fail, ct);
            var roll = Task.Run(() => store.Write(target.Id, bytes.Length, bytes), ct);
            using var sealing = new ManualResetEventSlim();
            Task<OverlayFlushReceipt>? seal = null;
            try
            {
                Assert.True(fixture.Pins.Entered.Wait(Bound, ct));
                Assert.True(File.Exists(fixture.Intent));
                var old = Assert.Throws<WorkspaceException>(() => JournalDeltaStore.SealSegmentedJournal(fixture.Head, before.DurableSequence));
                Assert.Equal("JournalSegmentCorrupt", old.Code);
                Assert.Equal("Recover the pending segmented roll first.", old.Message);
                seal = Task.Factory.StartNew(() =>
                {
                    sealing.Set();
                    return ProcessSeal(store);
                }, ct, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                Assert.True(sealing.Wait(Bound, ct));
                Assert.NotSame(seal, await Task.WhenAny(seal, Task.Delay(50, ct)));
                fixture.Pins.Proceed.Set();
                if (fail)
                    await Assert.ThrowsAsync<IOException>(() => roll.WaitAsync(Bound, ct));
                else
                    await roll.WaitAsync(Bound, ct);
                var receipt = await seal.WaitAsync(Bound, ct);
                Assert.Equal(Request.Token, receipt.Token);
                Assert.Equal(Request.RequestId, receipt.RequestId);
                Assert.Equal(Request.ProcessId, receipt.ProcessId);
                Assert.Equal(Request.ProcessStartTimeUtcTicks, receipt.ProcessStartTimeUtcTicks);
                if (fail)
                {
                    Assert.Equal("JournalSegmentCorrupt", receipt.SealError);
                    Assert.Null(receipt.Seal);
                    Assert.True(File.Exists(fixture.Intent));
                }
                else
                {
                    Assert.Equal(before.DurableSequence + 1, receipt.Seal?.DurableSequence);
                    Assert.Equal(store.Seal(), receipt.Seal);
                    Assert.Equal(receipt.Seal?.JournalBytes, receipt.Storage?.SealJournalBytes);
                    Assert.False(File.Exists(fixture.Intent));
                }
            }
            finally
            {
                fixture.Pins.Proceed.Set();
                try { await roll.WaitAsync(Bound, CancellationToken.None); }
                catch (IOException) when (fail) { }
                finally { if (seal is not null) await seal.WaitAsync(Bound, CancellationToken.None); }
            }
        }
        if (fail)
        {
            using var reopened = fixture.Open();
            Assert.False(File.Exists(fixture.Intent));
            Assert.Equal(before, ProcessSeal(reopened).Seal);
        }
    }

    [Fact]
    public async Task LiveSealWaitsForMetadataCollectionRollAndPreservesTheAcceptedSeal()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "The test requires native Windows backing identities.");
        using var fixture = new Fixture();
        var ct = TestContext.Current.CancellationToken;
        byte[] expected = [6, 7, 8, 9];
        SnapshotDeltaSeal before;
        Guid fileId;
        using (var store = fixture.Open())
        {
            fileId = store.Create("target", false, 32).Id;
            store.Write(fileId, 0, expected);
            before = store.Seal();
            fixture.Pins.ArmRoll(fail: false, ct);
            var maintenance = Task.Run(() => store.CollectLiveMetadata(force: true, ct: ct), ct);
            using var sealing = new ManualResetEventSlim();
            Task<OverlayFlushReceipt>? seal = null;
            try
            {
                Assert.True(fixture.Pins.Entered.Wait(Bound, ct));
                Assert.True(File.Exists(fixture.Intent));
                var old = Assert.Throws<WorkspaceException>(() =>
                    JournalDeltaStore.SealSegmentedJournal(fixture.Head, before.DurableSequence));
                Assert.Equal("JournalSegmentCorrupt", old.Code);
                Assert.Equal("Recover the pending segmented roll first.", old.Message);
                seal = Task.Factory.StartNew(() =>
                {
                    sealing.Set();
                    return ProcessSeal(store);
                }, ct, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                Assert.True(sealing.Wait(Bound, ct));
                Assert.NotSame(seal, await Task.WhenAny(seal, Task.Delay(50, ct)));
                fixture.Pins.Proceed.Set();
                var receipt = await seal.WaitAsync(Bound, ct);
                await maintenance.WaitAsync(Bound, ct);
                Assert.Null(receipt.SealError);
                Assert.Equal(before, receipt.Seal);
                var storage = Assert.IsType<OverlayStorageObservation>(receipt.Storage);
                Assert.Equal(before.DurableSequence, storage.DurableSequence);
                Assert.Equal(before.JournalBytes, storage.SealJournalBytes);
                Assert.Equal(before, store.Seal());
                Assert.False(File.Exists(fixture.Intent));
            }
            finally
            {
                fixture.Pins.Proceed.Set();
                try { await maintenance.WaitAsync(Bound, CancellationToken.None); }
                finally { if (seal is not null) await seal.WaitAsync(Bound, CancellationToken.None); }
            }
        }

        Assert.Equal(before, JournalDeltaStore.SealSegmentedJournal(fixture.Head, before.DurableSequence));
        using var reopened = fixture.Open();
        Assert.Equal(before, ProcessSeal(reopened).Seal);
        byte[] actual = new byte[expected.Length + 1];
        int count = reopened.Read(fileId, 0, actual);
        Assert.Equal(expected.Length, count);
        Assert.Equal(expected, actual[..count]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LiveSealReadsPhysicalHeadAndFrameChecksum(bool checksum)
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "The test requires native Windows backing identities.");
        using var fixture = new Fixture();
        using var store = fixture.Open();
        var target = store.Create("target", false, 32);
        store.Write(target.Id, 0, [3, 4, 5]);
        Assert.Equal(store.Seal(), ProcessSeal(store).Seal);
        fixture.Pins.BeforeWriterProof = file =>
        {
            long at = checksum ? file.Length - 1 : 0;
            Span<byte> value = stackalloc byte[1];
            Assert.Equal(1, RandomAccess.Read(file.SafeFileHandle, value, at));
            value[0] ^= 1;
            RandomAccess.Write(file.SafeFileHandle, value, at);
            RandomAccess.FlushToDisk(file.SafeFileHandle);
        };
        var failed = ProcessSeal(store);
        Assert.Null(failed.Seal);
        Assert.Equal(checksum ? "DeltaCorrupt" : "JournalSegmentCorrupt", failed.SealError);
    }

    [Fact]
    public void LiveSealRejectsByteIdenticalHeadSubstitution()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "The test requires native Windows backing identities.");
        using var fixture = new Fixture();
        using var store = fixture.Open();
        var target = store.Create("target", false, 32);
        store.Write(target.Id, 0, [3, 4, 5]);
        var expected = store.Seal();
        string parked = fixture.Head + ".parked";
        File.Move(fixture.Head, parked);
        using (var old = new FileStream(parked, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (var replacement = File.Create(fixture.Head)) old.CopyTo(replacement);
        Assert.Equal(expected, JournalDeltaStore.SealSegmentedJournal(fixture.Head, expected.DurableSequence));
        var failed = ProcessSeal(store);
        Assert.Null(failed.Seal);
        Assert.Contains(failed.SealError!, new[] { "UnsafePath", "SourceCaptureBackingInvalid" });
    }

    [Fact]
    public void WrongProviderLifetimeCannotRequestASeal()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "The test requires native Windows backing identities.");
        using var fixture = new Fixture();
        using var store = fixture.Open();
        var failed = WinFspOverlayServer.ProcessFlush(store, Request, 42, 102);
        Assert.Equal("OverlayLeaseStale", failed.SealError);
        Assert.Equal(102, failed.ProcessStartTimeUtcTicks);
        Assert.Null(failed.Seal);
        Assert.Null(failed.Storage);
        Assert.Equal(store.Seal(), ProcessSeal(store).Seal);
    }

    private static OverlayFlushReceipt ProcessSeal(JournalDeltaStore store) =>
        WinFspOverlayServer.ProcessFlush(store, Request, 42, 101);

    private sealed class Fixture : IDisposable
    {
        private readonly TestDirectory root = new();
        private readonly EmptyBasis basis = new();
        private string Delta => Path.Combine(root.Path, "delta");
        internal string Head => Path.Combine(Delta, "delta.journal");
        internal string Intent => Path.Combine(Delta, "segment-roll.json");
        internal BlockingPins Pins { get; }

        internal Fixture() => Pins = new(Head, Intent);
        internal JournalDeltaStore Open() =>
            new(Delta, new(3, "live-seal", basis.Snapshot, long.MaxValue, 65536), basis,
                boundedLogBytes: 4096, sourceFilePins: Pins);
        public void Dispose() { Pins.Dispose(); root.Dispose(); }
    }

    private sealed class EmptyBasis : ISnapshotTree
    {
        public SnapshotReference Snapshot { get; } = new("live-seal-basis", "fake", "source", "root", DateTimeOffset.UnixEpoch, true, true, "fixture");
        public void EnsureAvailable() { }
        public SnapshotEntry? Stat(string path) => path == "" ? new("", true, 0, 16, 1, 1, 1) : null;
        public IReadOnlyList<SnapshotEntry> List(string path) => [];
        public int Read(string path, long offset, Span<byte> destination) => throw new InvalidOperationException();
    }

    private sealed class BlockingPins(string head, string intent) : IJournalFilePins, IDisposable
    {
        private readonly WindowsJournalFilePins native = new();
        private CancellationToken cancellation;
        private bool fail;
        private int armed;
        internal readonly ManualResetEventSlim Entered = new();
        internal readonly ManualResetEventSlim Proceed = new();
        internal Action<FileStream>? BeforeWriterProof;

        internal void ArmRoll(bool fail, CancellationToken ct)
        { this.fail = fail; cancellation = ct; Volatile.Write(ref armed, 1); }

        public string Identify(string path)
        {
            if (path == head && File.Exists(intent) && Interlocked.Exchange(ref armed, 0) != 0)
            {
                Entered.Set();
                if (!Proceed.Wait(Bound, cancellation)) throw new TimeoutException("The test did not release the roll.");
                if (fail) throw new IOException("Interrupted after roll intent publication.");
            }
            return native.Identify(path);
        }
        public string IdentifyOpened(FileStream input) => native.IdentifyOpened(input);
        public void Verify(FileStream input, string identity) => native.Verify(input, identity);
        public void VerifyPath(FileStream input, string path, string identity)
        {
            if (input.CanWrite) Interlocked.Exchange(ref BeforeWriterProof, null)?.Invoke(input);
            native.VerifyPath(input, path, identity);
        }
        public void Pin(string source, string target, string identity) => native.Pin(source, target, identity);
        public void Unpin(string path, string identity) => native.Unpin(path, identity);
        public bool DetachBeforeTruncate(string path, long length) => native.DetachBeforeTruncate(path, length);
        public void Dispose() { Entered.Dispose(); Proceed.Dispose(); }
    }
}
