using Microsoft.Win32.SafeHandles;
using Splinter.Core;
using Splinter.Platform.Windows;

namespace Splinter.Tests;

public sealed class RootTopologyForestProofTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedOrCancelledHashJoinsItsPeerBeforeReleasingForestPins(bool cancel)
    {
        using var f = new Fixture();
        using var peerEntered = new ManualResetEventSlim();
        using var failureReached = new ManualResetEventSlim();
        using var releasePeer = new ManualResetEventSlim();
        using var cancellation = new CancellationTokenSource();
        int calls = 0; string? peerPath = null, failedPath = null;
        var construct = Task.Run(() =>
        {
            using var proof = f.Open(cancellation.Token, path =>
            {
                int call = Interlocked.Increment(ref calls);
                if (call == 1)
                {
                    peerPath = path; peerEntered.Set();
                    // Do not bind the held peer to the operation token: the
                    // cancellation case must prove that its owner joins it.
                    Assert.True(releasePeer.Wait(TimeSpan.FromSeconds(15)));
                }
                else if (call == 2)
                {
                    Wait(peerEntered); failedPath = path;
                    if (cancel) cancellation.Cancel();
                    failureReached.Set();
                    if (!cancel) throw new IOException("Fixed forest content failure.");
                }
            });
        }, TestContext.Current.CancellationToken);
        Exception? error = null;
        try
        {
            Wait(failureReached);
            // This bounded scheduling check is a safety assertion, not a
            // throughput sample or an artificial delay in a cost workload.
            var observation = Task.Delay(100, TestContext.Current.CancellationToken);
            Assert.Same(observation, await Task.WhenAny(construct, observation));
            Assert.False(construct.IsCompleted);
            Assert.NotNull(peerPath); Assert.NotNull(failedPath); Assert.NotEqual(peerPath, failedPath);
            Assert.Throws<IOException>(() => File.WriteAllBytes(peerPath, [2]));
            Assert.Throws<IOException>(() => File.WriteAllBytes(failedPath, [3]));
            Assert.Throws<IOException>(() => Directory.Move(f.Keep, f.Keep + "-moved"));
            Assert.Throws<IOException>(() => Directory.Move(f.StagedDirectory, f.StagedDirectory + "-moved"));
            Assert.Throws<IOException>(() => Directory.Move(f.ReceiptRoot, f.ReceiptRoot + "-moved"));
        }
        finally
        {
            // Even a failed assertion must release and join the task before
            // its fixture can dispose the borrowed root handle.
            releasePeer.Set();
            // Once started, workers must drain even if the test was cancelled.
#pragma warning disable xUnit1051 // The bounded ownership join deliberately ignores test cancellation.
            error = await Record.ExceptionAsync(() => construct.WaitAsync(TimeSpan.FromSeconds(15), CancellationToken.None));
#pragma warning restore xUnit1051
        }
        if (cancel) Assert.IsAssignableFrom<OperationCanceledException>(error);
        else Assert.IsType<IOException>(error);
        Assert.False(f.RootHandle.IsClosed);
        foreach (string path in f.Files)
            using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        Directory.Move(f.Keep, f.Keep + "-released");
        Directory.Move(f.ReceiptRoot, f.ReceiptRoot + "-released");
        Assert.Throws<IOException>(() => Directory.Move(f.OriginalRoot, f.OriginalRoot + "-still-borrowed"));
        f.RootHandle.Dispose();
        Directory.Move(f.OriginalRoot, f.OriginalRoot + "-released");
        f.AllowCleanup();
    }

    private static void Wait(ManualResetEventSlim signal)
        => Assert.True(signal.Wait(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken));

    private sealed class Fixture : IDisposable
    {
        private readonly TestDirectory temporary = new();
        private readonly OwnedDirectoryManifest original, staged;
        private bool cleanup;
        internal string OriginalRoot => Path.Combine(temporary.Path, "original");
        internal string ReceiptRoot => Path.Combine(temporary.Path, "receipts");
        internal string Keep => Path.Combine(OriginalRoot, "keep");
        internal string StagedDirectory => Path.Combine(ReceiptRoot, "restore-topology-stage-00000002");
        internal SafeFileHandle RootHandle { get; }
        internal string[] Files { get; }

        internal Fixture()
        {
            Assert.SkipUnless(OperatingSystem.IsWindows(), "Requires real NTFS identity and handle sharing guards.");
            Directory.CreateDirectory(Keep); Directory.CreateDirectory(Path.Combine(StagedDirectory, "nested"));
            Files = [Path.Combine(Keep, "a"), Path.Combine(Keep, "b"),
                Path.Combine(ReceiptRoot, "restore-topology-stage-00000001"), Path.Combine(StagedDirectory, "nested", "c")];
            foreach (string path in Files) File.WriteAllBytes(path, Enumerable.Range(0, 4096).Select(i => (byte)(i * 17)).ToArray());
            original = Capture(OriginalRoot, "original"); staged = Capture(ReceiptRoot, "staged");
            // Child rename attempts after failure must not be blocked by the
            // caller's borrowed owner handle and mistaken for leaked pins.
            RootHandle = ManagedRestoreFileAccess.DirectoryHandle(OriginalRoot, allowChildMutations: true);
        }

        private static OwnedDirectoryManifest Capture(string root, string id)
            => OwnedDirectorySnapshotTree.Capture(new SnapshotReference(id, OwnedDirectorySnapshotTree.ProviderName,
                root, root, DateTimeOffset.UnixEpoch, true, true, "stopped-original-owned-directory"), id);

        // Keep new internal topology types out of fixture fields and async
        // state machines so the common test assembly remains discoverable
        // against the frozen baseline used by separate cost tests.
        internal IDisposable Open(CancellationToken ct, Action<string> beforeHash)
        {
            var members = new Dictionary<string, ManagedRestoreTopology.Member>(StringComparer.Ordinal);
            foreach (var group in new[] { (Manifest: original, Root: OriginalRoot, Deleted: false),
                (Manifest: staged, Root: ReceiptRoot, Deleted: true) })
            foreach (var item in group.Manifest.Items)
            {
                if (group.Deleted && item.Entry.Path.Length == 0) continue;
                string path = Path.Combine(group.Root, item.Entry.Path.Replace('/', Path.DirectorySeparatorChar));
                members.Add(path, new(path, item, null,
                    RootTopologyDirectoryRestoreTests.ReadNativeIdentity(path, item.Entry.Directory), group.Deleted));
            }
            return new ManagedRestoreTopology.ForestPins(members, OriginalRoot, ReceiptRoot, RootHandle,
                new HashSet<string>(StringComparer.Ordinal), content: true, ct, hashWorkers: 2, beforeHash);
        }

        internal void AllowCleanup() => cleanup = true;
        public void Dispose()
        {
            RootHandle.Dispose();
            if (cleanup) temporary.Dispose();
            else TestContext.Current.TestOutputHelper?.WriteLine("Preserved forest-proof fixture: " + temporary.Path);
        }
    }
}
