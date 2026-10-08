using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class JournalPathProofTests
{
    private static void RequireWindows()
    { if (!OperatingSystem.IsWindows()) Assert.Skip("Ordinary-user Windows directory handle proof."); }
    private static OverlayIdentity Identity(ISnapshotTree tree) => new(2, "path-proof", tree.Snapshot, 64L * 1048576, 1048576);
    private static IJournalDirectoryGuard Pin(string path) => new WindowsJournalDirectoryGuard(path);

    [Fact]
    public void ExplicitBoundedJournalPathCostEvidence()
    {
        string? output = Environment.GetEnvironmentVariable("SPLINTER_JOURNAL_PATH_EVIDENCE");
        if (string.IsNullOrWhiteSpace(output)) { Assert.Skip("Set SPLINTER_JOURNAL_PATH_EVIDENCE to a new output file for bounded ordinary-user cost evidence."); return; }
        RequireWindows(); output = Path.GetFullPath(output);
        Assert.False(File.Exists(output), "Keep earlier evidence; use a new explicit output path.");
        const int writes = 512, bytesPerWrite = 4096;
        byte[] payload = new byte[bytesPerWrite]; new Random(28173).NextBytes(payload);
        var results = new List<object>();
        // Balanced ordering; this compares the same current binary, not two
        // releases. Each run owns a fresh tiny fixture and its own output.
        foreach (bool pinned in new[] { false, true, true, false })
        {
            using var fixture = new TestDirectory();
            string delta = Path.Combine(fixture.Path, "one", "two", "three", "four", "five", "six", "delta");
            var tree = new SnapshotOverlayTests.FakeSnapshotTree(); var identity = Identity(tree);
            CountingGuard? proof = null; Guid id; SnapshotDeltaSeal seal;
            long elapsedTicks, leafChecks, appended, flushes, journalBytes;
            using (var store = new JournalDeltaStore(delta, identity, tree,
                directoryGuardFactory: pinned ? path => proof = new CountingGuard(path) : null))
            {
                for (int i = 0; i < 32; i++) store.Write(store.Create("peer-" + i, false, 32).Id, 0, [17, 23, 31]);
                id = store.Create("growing", false, 32).Id;
                long beforeFlushes = store.Observe().Metrics!.FlushCount, beforeBytes = store.Observe().Metrics!.JournalAppendedBytes;
                long beforeLeaves = proof?.LeafChecks ?? 0;
                long began = Stopwatch.GetTimestamp();
                for (int i = 0; i < writes; i++) store.Write(id, i * bytesPerWrite, payload);
                elapsedTicks = Stopwatch.GetTimestamp() - began;
                leafChecks = (proof?.LeafChecks ?? 0) - beforeLeaves;
                var observation = store.Observe();
                flushes = observation.Metrics!.FlushCount - beforeFlushes;
                appended = observation.Metrics.JournalAppendedBytes - beforeBytes;
                journalBytes = store.GetJournalBytes();
                Assert.Equal(writes, flushes); Verify(store, id, payload, writes); seal = store.Seal();
            }
            using (var resumed = JournalDeltaStore.OpenReadOnly(delta, identity, tree))
            { Assert.Equal(seal, resumed.Seal()); Verify(resumed, id, payload, writes); }
            results.Add(new
            {
                mode = pinned ? "pinned-ancestry" : "full-walk", elapsedSeconds = (double)elapsedTicks / Stopwatch.Frequency,
                writes, bytesPerWrite, inputBytes = writes * bytesPerWrite, flushes, appended, journalBytes,
                pinnedAncestors = proof?.AncestorCount, measuredLeafChecks = pinned ? (long?)leafChecks : null,
                baselineAttributeQueryCount = (long?)null, seal, contentVerified = true, peerFilesVerified = 32, reconnectSealVerified = true
            });
        }
        using var destination = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        JsonSerializer.Serialize(destination, new
        {
            measuredAt = DateTimeOffset.UtcNow,
            runtimeSha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(typeof(JournalDeltaStore).Assembly.Location))),
            scope = "Four fresh ordinary NTFS fixtures; current binary full walk/pinned/pinned/full walk; append time only, not Unity or physical I/O.", results
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    private static void Verify(JournalDeltaStore store, Guid id, byte[] payload, int writes)
    {
        byte[] actual = new byte[payload.Length];
        for (int i = 0; i < writes; i++)
        { Assert.Equal(actual.Length, store.Read(id, i * payload.Length, actual)); Assert.Equal(payload, actual); }
        for (int i = 0; i < 32; i++)
        {
            byte[] peer = new byte[3]; Assert.Equal(3, store.Read(store.Stat("peer-" + i)!.Id, 0, peer));
            Assert.Equal(new byte[] { 17, 23, 31 }, peer);
        }
    }

    private sealed class CountingGuard : IJournalDirectoryGuard
    {
        private readonly WindowsJournalDirectoryGuard guard;
        public string DirectoryPath => guard.DirectoryPath;
        internal long LeafChecks;
        internal int AncestorCount { get; }
        internal CountingGuard(string path)
        {
            guard = new(path);
            for (string? ancestor = guard.DirectoryPath; ancestor is not null; ancestor = Path.GetDirectoryName(ancestor)) AncestorCount++;
        }
        public void EnsureFile(string path) { LeafChecks++; guard.EnsureFile(path); }
        public void Dispose() => guard.Dispose();
    }

    [Fact]
    public void WriterPinsAncestryWhilePreservingBytesFlushesAndReconnectSeal()
    {
        RequireWindows(); using var fixture = new TestDirectory();
        string ancestor = Path.Combine(fixture.Path, "ancestor"), delta = Path.Combine(ancestor, "delta");
        var tree = new SnapshotOverlayTests.FakeSnapshotTree(); var identity = Identity(tree);
        byte[] expected = Enumerable.Range(0, 4096).Select(i => (byte)i).ToArray(); Guid id; SnapshotDeltaSeal seal;
        using (var store = new JournalDeltaStore(delta, identity, tree, directoryGuardFactory: Pin))
        {
            id = store.Create("file", false, 32).Id; long flushes = store.Observe().Metrics!.FlushCount;
            for (int i = 0; i < 4; i++) store.Write(id, i * expected.Length, expected);
            Assert.Equal(4, store.Observe().Metrics!.FlushCount - flushes);
            string objects = Directory.GetDirectories(delta, "latest-*").Single();
            Assert.ThrowsAny<IOException>(() => Directory.Move(objects, objects + ".moved"));
            Assert.ThrowsAny<IOException>(() => Directory.Move(ancestor, ancestor + ".moved"));
            byte[] bytes = new byte[4 * expected.Length]; Assert.Equal(bytes.Length, store.Read(id, 0, bytes));
            for (int i = 0; i < 4; i++) Assert.Equal(expected, bytes[(i * expected.Length)..((i + 1) * expected.Length)]);
            seal = store.Seal();
        }
        Directory.Move(ancestor, ancestor + ".moved"); Directory.Move(ancestor + ".moved", ancestor);
        using var resumed = new JournalDeltaStore(delta, identity, tree, directoryGuardFactory: Pin);
        Assert.Equal(seal, resumed.Seal()); Assert.Equal(id, resumed.Stat("file")!.Id);
    }

    [Fact]
    public void ProofRejectsAnotherDirectoryAndCannotBeUsedAfterClose()
    {
        RequireWindows(); using var fixture = new TestDirectory();
        string directory = Path.Combine(fixture.Path, "objects"); Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "owner.lock"), []);
        var proof = new WindowsJournalDirectoryGuard(directory);
        proof.EnsureFile(Path.Combine(directory, "missing.bin"));
        Assert.Equal("UnsafePath", Assert.Throws<WorkspaceException>(() => proof.EnsureFile(Path.Combine(fixture.Path, "foreign.bin"))).Code);
        proof.Dispose(); proof.Dispose();
        Assert.Throws<ObjectDisposedException>(() => proof.EnsureFile(Path.Combine(directory, "missing.bin")));
        Directory.Move(directory, directory + ".moved");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExistingObjectOrAncestorJunctionIsRejectedWithoutLeakingPins(bool ancestor)
    {
        RequireWindows(); using var fixture = new TestDirectory();
        string target = Path.Combine(fixture.Path, "target"), link = Path.Combine(fixture.Path, "link");
        Directory.CreateDirectory(Path.Combine(target, "objects"));
        await Link(link, target);
        try
        {
            Assert.Equal("UnsafePath", Assert.Throws<WorkspaceException>(() => new WindowsJournalDirectoryGuard(
                ancestor ? Path.Combine(link, "objects") : link)).Code);
        }
        finally { Directory.Delete(link); }
        Directory.Move(target, target + ".moved");
    }

    [Fact]
    public async Task InjectedPendingJunctionIsPreservedAndNeverWritesToItsTarget()
    {
        RequireWindows(); using var fixture = new TestDirectory();
        string delta = Path.Combine(fixture.Path, "delta"), outside = Path.Combine(fixture.Path, "outside");
        Directory.CreateDirectory(outside); File.WriteAllText(Path.Combine(outside, "keep.txt"), "keep");
        var tree = new SnapshotOverlayTests.FakeSnapshotTree(); var identity = Identity(tree);
        using var store = new JournalDeltaStore(delta, identity, tree, directoryGuardFactory: Pin);
        string pending = Path.Combine(Directory.GetDirectories(delta, "latest-*").Single(), "transaction.pending");
        await Link(pending, outside);
        try
        {
            Assert.Equal("UnsafePath", Assert.Throws<WorkspaceException>(() => store.Create("file", false, 32)).Code);
            Assert.Equal("DeltaReconnectRequired", Assert.Throws<WorkspaceException>(() => store.Create("other", false, 32)).Code);
            Assert.True((File.GetAttributes(pending) & FileAttributes.ReparsePoint) != 0);
            Assert.Equal("keep", File.ReadAllText(Path.Combine(outside, "keep.txt")));
            Assert.Single(Directory.GetFileSystemEntries(outside));
        }
        finally { Directory.Delete(pending); }
    }

    [Fact]
    public void CorruptReconnectReleasesEveryAcquiredAncestorWithoutChangingEvidence()
    {
        RequireWindows(); using var fixture = new TestDirectory();
        string ancestor = Path.Combine(fixture.Path, "ancestor"), delta = Path.Combine(ancestor, "delta");
        var tree = new SnapshotOverlayTests.FakeSnapshotTree(); var identity = Identity(tree);
        using (var store = new JournalDeltaStore(delta, identity, tree, directoryGuardFactory: Pin))
            store.Write(store.Create("file", false, 32).Id, 0, [1, 2, 3]);
        string root = Path.Combine(delta, "delta.journal"); byte[] damaged = File.ReadAllBytes(root); damaged[96] ^= 1;
        File.WriteAllBytes(root, damaged);
        Assert.Equal("DeltaCorrupt", Assert.Throws<WorkspaceException>(() => new JournalDeltaStore(delta, identity, tree, directoryGuardFactory: Pin)).Code);
        Assert.Equal(damaged, File.ReadAllBytes(root));
        Directory.Move(ancestor, ancestor + ".moved");
    }

    [Fact]
    public void IncorrectGenerationProofIsRejectedAndDisposed()
    {
        RequireWindows(); using var fixture = new TestDirectory();
        string delta = Path.Combine(fixture.Path, "delta"), wrong = Path.Combine(fixture.Path, "wrong"); Directory.CreateDirectory(wrong);
        File.WriteAllBytes(Path.Combine(wrong, "owner.lock"), []);
        var tree = new SnapshotOverlayTests.FakeSnapshotTree(); var identity = Identity(tree);
        Assert.Equal("UnsafePath", Assert.Throws<WorkspaceException>(() => new JournalDeltaStore(delta, identity, tree,
            directoryGuardFactory: _ => Pin(wrong))).Code);
        Directory.Move(wrong, wrong + ".moved");
        using var resumed = new JournalDeltaStore(delta, identity, tree, directoryGuardFactory: Pin);
        resumed.Create("file", false, 32);
    }

    [Theory]
    [InlineData("latest.after-intent-flush")]
    [InlineData("latest.after-root-publication")]
    public void InterruptedPublicationRecoversUnderFreshProofForSameGeneration(string boundary)
    {
        RequireWindows(); using var fixture = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = Identity(tree); var observer = new Interrupt(); Guid id;
        using (var store = new JournalDeltaStore(fixture.Path, identity, tree, observer, directoryGuardFactory: Pin))
        {
            id = store.Create("file", false, 32).Id; observer.Boundary = boundary;
            Assert.Throws<IOException>(() => store.Write(id, 0, [9, 8, 7]));
        }
        using var resumed = new JournalDeltaStore(fixture.Path, identity, tree, directoryGuardFactory: Pin);
        byte[] actual = new byte[3]; Assert.Equal(3, resumed.Read(id, 0, actual)); Assert.Equal(new byte[] { 9, 8, 7 }, actual);
        resumed.Write(id, 3, [6]); resumed.Seal();
    }

    [Fact]
    public async Task ConcurrentCloseKeepsProofUntilInFlightMutationFinishes()
    {
        RequireWindows(); using var fixture = new TestDirectory();
        string ancestor = Path.Combine(fixture.Path, "ancestor"), delta = Path.Combine(ancestor, "delta");
        var tree = new SnapshotOverlayTests.FakeSnapshotTree(); var identity = Identity(tree); using var observer = new Block();
        var store = new JournalDeltaStore(delta, identity, tree, observer, directoryGuardFactory: Pin);
        Guid id = store.Create("file", false, 32).Id; observer.Armed = true;
        var write = Run(() => store.Write(id, 0, [1, 2, 3])); Task? close = null, otherClose = null;
        try
        {
            Assert.True(observer.Entered.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
            close = Run(store.Dispose); otherClose = Run(store.Dispose);
            await Task.Delay(100, TestContext.Current.CancellationToken);
            Assert.False(close.IsCompleted); Assert.False(otherClose.IsCompleted);
            Assert.ThrowsAny<IOException>(() => Directory.Move(ancestor, ancestor + ".moved"));
        }
        finally { observer.Continue.Set(); }
        await write.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        if (close is not null) await close.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        if (otherClose is not null) await otherClose.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        store.Dispose();
        Assert.Throws<ObjectDisposedException>(() => store.Write(id, 3, [4]));
        Directory.Move(ancestor, ancestor + ".moved");
    }

    [Fact]
    public async Task ConcurrentCloseKeepsProofUntilDetachedBaseReadFinishes()
    {
        RequireWindows(); using var fixture = new TestDirectory(); using var tree = new BlockingTree();
        string ancestor = Path.Combine(fixture.Path, "ancestor"), delta = Path.Combine(ancestor, "delta");
        var store = new JournalDeltaStore(delta, Identity(tree), tree, directoryGuardFactory: Pin);
        Guid id = store.Stat("base")!.Id; byte[] actual = new byte[1];
        var read = Run(() => store.Read(id, 0, actual)); Task? close = null, otherClose = null;
        try
        {
            Assert.True(tree.Entered.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
            close = Run(store.Dispose);
            Assert.True(SpinWait.SpinUntil(() =>
            {
                try { store.Get(id); return false; }
                catch (ObjectDisposedException) { return true; }
            }, TimeSpan.FromSeconds(10)));
            otherClose = Run(store.Dispose);
            await Task.Delay(100, TestContext.Current.CancellationToken);
            Assert.False(close.IsCompleted); Assert.False(otherClose.IsCompleted);
            Assert.ThrowsAny<IOException>(() => Directory.Move(ancestor, ancestor + ".moved"));
        }
        finally { tree.Continue.Set(); }
        await read.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        if (close is not null) await close.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        if (otherClose is not null) await otherClose.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        store.Dispose(); Assert.Equal(new byte[] { 42 }, actual);
        Directory.Move(ancestor, ancestor + ".moved");
    }

    private sealed class Interrupt : IJournalMutationObserver
    {
        internal string? Boundary;
        public void Observe(JournalMutationBoundary boundary)
        { if (boundary.Boundary == Boundary) throw new IOException("Interrupted publication with pinned ancestry."); }
    }
    private sealed class Block : IJournalMutationObserver, IDisposable
    {
        internal readonly ManualResetEventSlim Entered = new(), Continue = new();
        internal bool Armed;
        public void Observe(JournalMutationBoundary boundary)
        {
            if (!Armed || boundary.Boundary != "latest.before-intent") return;
            Entered.Set();
            if (!Continue.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException("Mutation was not released.");
        }
        public void Dispose() { Continue.Set(); Entered.Dispose(); Continue.Dispose(); }
    }
    private sealed class BlockingTree : ISnapshotTree, IDisposable
    {
        public SnapshotReference Snapshot { get; } = new("path-proof-read", "fake-memory", "original", "fixed", DateTimeOffset.UnixEpoch, true, true, "fixture-only");
        internal readonly ManualResetEventSlim Entered = new(), Continue = new();
        public void EnsureAvailable() { }
        public SnapshotEntry? Stat(string path) => path switch
        {
            "" => new("", true, 0, 16, 1, 1, 1),
            "base" => new("base", false, 1, 32, 1, 1, 1),
            _ => null
        };
        public IReadOnlyList<SnapshotEntry> List(string path) => path == "" ? [Stat("base")!] : [];
        public int Read(string path, long offset, Span<byte> destination)
        {
            Entered.Set();
            if (!Continue.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException("Read was not released.");
            destination[0] = 42; return 1;
        }
        public void Dispose() { Continue.Set(); Entered.Dispose(); Continue.Dispose(); }
    }
    private static Task Run(Action action) => Task.Factory.StartNew(action, TestContext.Current.CancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    private static async Task Link(string path, string target)
    {
        var ct = TestContext.Current.CancellationToken;
        var start = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true, Arguments = $"/d /c mklink /J \"{path}\" \"{target}\"" };
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(ct); var error = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        Assert.True(process.ExitCode == 0, await output + await error);
    }
}
