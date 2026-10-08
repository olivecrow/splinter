using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Splinter.Core;
using Splinter.Platform.Windows;
using RestoreFixture = Splinter.Tests.ManagedDirectoryRestoreOptimizationTests.Fixture;

namespace Splinter.Tests;

public sealed class OwnedDirectoryLifetimeTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("journal")]
    [InlineData("restore-directory")]
    public void JournalAndRestoreDirectoryProofsPreventRenameAndReparseWritesUntilClosed(string kind)
    {
        using var f = new Fixture();
        string owner = Path.Combine(f.Root, "owner.lock");
        if (kind == "journal") File.WriteAllBytes(owner, []);
        using (IDisposable pin = kind switch
        {
            "journal" => new WindowsJournalDirectoryGuard(f.Root),
            _ => ManagedRestoreFileAccess.DirectoryHandle(f.Root)
        })
        {
            Assert.Throws<IOException>(() => Directory.Move(f.Root, f.Root + "-moved"));
            Assert.Throws<IOException>(() => Directory.Delete(f.Root));
            foreach (string path in kind == "restore-directory" ? new[] { f.Root } : new[] { f.Root, f.Parent })
            {
                Assert.Throws<IOException>(() => Directory.Move(path, path + "-moved"));
                Assert.NotEqual(0, TrySetJunction(path, f.Directory));
            }
            if (kind == "journal")
            {
                Assert.Throws<IOException>(() => File.Delete(owner));
                AssertPublication(f.Root);
            }
        }
        using (var write = OpenDirectoryForReparse(f.Root)) Assert.False(write.IsInvalid);
        if (kind == "journal") File.Delete(owner);
        Directory.Delete(f.Root); Directory.Move(f.Parent, f.Parent + "-moved");
    }

    [Fact]
    public void EmptyRootAndAncestorsDenyRenameDeleteAndReparseWritingUntilClose()
    {
        using var f = new Fixture();
        using (var tree = f.Open())
        {
            foreach (string path in new[] { f.Root, f.Parent })
            {
                Assert.Throws<IOException>(() => Directory.Move(path, path + "-moved"));
                Assert.NotEqual(0, TrySetJunction(path, f.Directory));
            }
            Assert.Throws<IOException>(() => Directory.Delete(f.Root));
            for (int i = 0; i < 100; i++) { tree.EnsureAvailable(); Assert.Empty(tree.List("")); }
        }
        Directory.Delete(f.Root);
        Directory.Move(f.Parent, f.Parent + "-moved");
    }

    [Fact]
    public void MutableRestoreParentsPermitPublicationAndChildMovesButRejectReparsedEmptyPaths()
    {
        using var f = new Fixture();
        string target = Path.Combine(f.Directory, "target"); Directory.CreateDirectory(target);
        using var pin = new ManagedRestoreFileAccess(Path.Combine(f.Root, "future-file"));
        Assert.Throws<IOException>(() => Directory.Move(f.Root, f.Root + "-moved"));
        Assert.Throws<IOException>(() => Directory.Move(f.Parent, f.Parent + "-moved"));
        AssertPublication(f.Root);
        string child = Path.Combine(f.Root, "child"); Directory.CreateDirectory(child);
        Directory.Move(child, child + "-moved"); Directory.Delete(child + "-moved");
        SetJunction(f.Root, target);
        try
        {
            Assert.Throws<WorkspaceException>(() => ManagedRestoreFileAccess.Read(Path.Combine(f.Root, "file")));
            Assert.Throws<WorkspaceException>(() => ManagedRestoreFileAccess.DirectoryHandle(f.Root));
            using var source = ManagedRestoreFileAccess.DirectoryHandle(target, moving: true);
            Assert.Throws<WorkspaceException>(() => ManagedRestoreFileAccess.Move(source, Path.Combine(f.Root, "moved")));
        }
        finally { pin.Dispose(); Directory.Delete(f.Root); }
    }

    private static void AssertPublication(string directory)
    {
        string first = Path.Combine(directory, "publish.tmp"), destination = Path.Combine(directory, "publish.json"), backup = Path.Combine(directory, "publish.backup");
        File.WriteAllText(first, "first"); File.Move(first, destination);
        File.WriteAllText(first, "second"); File.Replace(first, destination, backup);
        Assert.Equal("second", File.ReadAllText(destination)); Assert.Equal("first", File.ReadAllText(backup));
        File.Delete(destination); File.Delete(backup);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConstructorRejectsRootOrAncestorJunctionAndReleasesPartialPins(bool ancestor)
    {
        using var f = new Fixture();
        string link = ancestor ? f.Parent : f.Root;
        string target = Path.Combine(f.Directory, "target"); Directory.CreateDirectory(target);
        if (ancestor) Directory.Delete(f.Root);
        Directory.Delete(link); Directory.CreateDirectory(link);
        SetJunction(link, target);
        try { Assert.Equal("UnsafePath", Assert.Throws<WorkspaceException>(() => f.Open()).Code); }
        finally { Directory.Delete(link); }
        Directory.Move(target, target + "-moved");
    }

    [Fact]
    public async Task ConcurrentReadsOverlapAndEveryDisposeDrainsBeforeReleasingPins()
    {
        using var f = new Fixture(payload: true);
        using var admitted = new CountdownEvent(2);
        using var resume = new ManualResetEventSlim();
        using var tree = f.Open(() => { admitted.Signal(); Wait(resume); });
        byte[] left = new byte[4096], right = new byte[4096];
        var first = Run(() => tree.Read("file", 0, left));
        var second = Run(() => tree.Read("file", 4096, right));
        Task? close = null, otherClose = null;
        try
        {
            Assert.True(admitted.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
            close = Run(tree.Dispose);
            Assert.True(SpinWait.SpinUntil(() => Record.Exception(tree.EnsureAvailable) is ObjectDisposedException, TimeSpan.FromSeconds(10)));
            using var closeStarted = new ManualResetEventSlim();
            otherClose = Run(() => { closeStarted.Set(); tree.Dispose(); }); Wait(closeStarted);
            Assert.Throws<ObjectDisposedException>(() => tree.Read("file", 0, new byte[1]));
            Assert.False(close.IsCompleted); Assert.False(otherClose.IsCompleted);
            Assert.Throws<IOException>(() => File.WriteAllBytes(f.File, [9]));
            Assert.Throws<IOException>(() => Directory.Move(f.Root, f.Root + "-moved"));
        }
        finally { resume.Set(); }
        Assert.Equal(new[] { 4096, 4096 }, await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        await Task.WhenAll(close!, otherClose!).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(f.Bytes[..4096], left); Assert.Equal(f.Bytes[4096..8192], right);
        File.WriteAllBytes(f.File, [9]); Directory.Move(f.Root, f.Root + "-moved");
    }

    [Fact]
    public async Task ReadFailureRemainsUnknownAndDoesNotStrandCloseOrReleasePinsEarly()
    {
        using var f = new Fixture(payload: true);
        using var tree = f.Open(() => throw new IOException("injected read failure"));
        Assert.Throws<IOException>(() => tree.Read("file", 0, new byte[1]));
        Assert.Equal("SnapshotReadFailed", Assert.Throws<WorkspaceException>(tree.EnsureAvailable).Code);
        Assert.Equal("SnapshotReadFailed", Assert.Throws<WorkspaceException>(() => tree.Stat("file")).Code);
        Assert.Equal("SnapshotReadFailed", Assert.Throws<WorkspaceException>(() => tree.List("")).Code);
        Assert.Equal("SnapshotReadFailed", Assert.Throws<WorkspaceException>(() => tree.Read("file", 0, new byte[1])).Code);
        Assert.Throws<IOException>(() => File.Delete(f.File));
        await Run(tree.Dispose).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        File.Delete(f.File); Directory.Delete(f.Root);
    }

    [Fact]
    public async Task RestoreHandoffDrainsReadsAndRetainsExactRootThroughTreeDisposal()
    {
        using var f = new Fixture(payload: true);
        using var admitted = new ManualResetEventSlim(); using var resume = new ManualResetEventSlim();
        using var tree = f.Open(() => { admitted.Set(); Wait(resume); });
        var read = Run(() => tree.Read("file", 0, new byte[4096])); Wait(admitted);
        var transfer = Run(() => tree.TransferRootToRestore());
        try
        {
            Assert.True(SpinWait.SpinUntil(() => Record.Exception(tree.EnsureAvailable) is WorkspaceException, TimeSpan.FromSeconds(10)));
            Assert.False(transfer.IsCompleted);
            Assert.Throws<IOException>(() => Directory.Move(f.Root, f.Root + "-moved"));
        }
        finally { resume.Set(); }
        Assert.Equal(4096, await read.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        using var root = await transfer.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        tree.EnsureAvailable();
        string identity = SnapshotInputChecks.FileIdentity(root, true);
        tree.Dispose();
        Assert.False(root.IsClosed);
        ManagedRestoreFileAccess.Move(root, f.Root + "-moved");
        Assert.Equal(identity, SnapshotInputChecks.FileIdentity(root, true));
        Assert.Equal(f.Bytes, File.ReadAllBytes(Path.Combine(f.Root + "-moved", "file")));
    }

    [Theory]
    [InlineData("replace")]
    [InlineData("junction")]
    [InlineData("failure")]
    public void EmptyRootHandoffRejectsReplacementOrFailureAndPermanentlyRefusesReaderReuse(string change)
    {
        using var f = new Fixture(); using var tree = f.Open();
        string preserved = f.Root + "-preserved", target = Path.Combine(f.Directory, "target");
        bool linked = false;
        Directory.CreateDirectory(target);
        try
        {
            var error = Record.Exception(() => tree.TransferRootToRestore(() =>
            {
                Assert.Throws<IOException>(() => Directory.Move(f.Parent, f.Parent + "-moved"));
                if (change == "failure") throw new IOException("interrupted root handoff");
                Directory.Move(f.Root, preserved); Directory.CreateDirectory(f.Root);
                if (change == "junction") { SetJunction(f.Root, target); linked = true; }
            }));
            if (change == "failure") Assert.IsType<IOException>(error); else Assert.IsType<WorkspaceException>(error);
            Assert.Equal("SnapshotReadFailed", Assert.Throws<WorkspaceException>(tree.EnsureAvailable).Code);
            Assert.Equal("SnapshotReadFailed", Assert.Throws<WorkspaceException>(() => tree.TransferRootToRestore()).Code);
            if (change != "failure") Assert.True(Directory.Exists(preserved));
        }
        finally { tree.Dispose(); if (linked) Directory.Delete(f.Root); }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task ProductRestoreHandoffKeepsExistingDirectoryPlanRecovery(int count)
    {
        using var f = new RestoreFixture(count, true, unchangedDirectory: true);
        int handoffs = 0;
        await Assert.ThrowsAsync<IOException>(() => f.Restore(stage =>
        {
            if (stage == "directory.root-unpinned") handoffs++;
            if (stage == "directory.after-plan") throw new IOException("stop after handoff and durable plan");
        }));
        Assert.Equal(1, handoffs); Assert.True(Directory.Exists(f.Request.DeltaPath));
        await f.Restore(); f.AssertRestored();
    }

    [Fact]
    public async Task InterruptedProductHandoffPreservesBasisAndJournalAndRetriesWithoutNewFormat()
    {
        using var f = new RestoreFixture(0, true, unchangedDirectory: true);
        await Assert.ThrowsAsync<IOException>(() => f.Restore(stage =>
        { if (stage == "directory.root-unpinned") throw new IOException("stop in handoff"); }));
        Assert.True(Directory.Exists(f.State.Basis.Snapshot.RootPath)); Assert.True(Directory.Exists(f.Request.DeltaPath));
        Assert.False(File.Exists(Path.Combine(f.Root, "restore-directory-plan.json")));
        await f.Restore(); f.AssertRestored();
    }

    [Fact]
    public void AvailabilityCostUsesPinnedLifetimeWithoutARepeatedPathQuery()
    {
        using var f = new Fixture(); using var tree = f.Open();
        const int count = 20000;
        for (int i = 0; i < 100; i++) { tree.EnsureAvailable(); Assert.True(Directory.Exists(f.Root)); }
        long started = Stopwatch.GetTimestamp();
        for (int i = 0; i < count; i++) if (!Directory.Exists(f.Root)) throw new InvalidOperationException();
        double pathSeconds = Stopwatch.GetElapsedTime(started).TotalSeconds;
        started = Stopwatch.GetTimestamp();
        for (int i = 0; i < count; i++) tree.EnsureAvailable();
        double pinnedSeconds = Stopwatch.GetElapsedTime(started).TotalSeconds;
        output.WriteLine($"Owned root availability: {count} calls, Directory.Exists={pathSeconds:F6}s, pinned lifetime={pinnedSeconds:F6}s. Small warm fixture only.");
        Assert.True(pathSeconds > 0); Assert.True(pinnedSeconds > 0);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly TestDirectory directory = new();
        internal string Directory => directory.Path;
        internal string Parent => Path.Combine(directory.Path, "parent");
        internal string Root => Path.Combine(Parent, "basis");
        internal string File => Path.Combine(Root, "file");
        internal byte[] Bytes { get; } = new byte[8192];
        private readonly SnapshotReference snapshot;
        private readonly OwnedDirectoryManifest manifest;
        internal Fixture(bool payload = false)
        {
            Assert.SkipUnless(OperatingSystem.IsWindows(), "Requires Windows directory/file sharing and identity handles.");
            System.IO.Directory.CreateDirectory(Root); new Random(91).NextBytes(Bytes);
            if (payload) System.IO.File.WriteAllBytes(File, Bytes);
            snapshot = new("lifetime", OwnedDirectorySnapshotTree.ProviderName, Root, Root, DateTimeOffset.UnixEpoch, true, true, "stopped-original-owned-directory");
            manifest = OwnedDirectorySnapshotTree.Capture(snapshot, "owner");
        }
        internal OwnedDirectorySnapshotTree Open(Action? readAdmitted = null) => new(manifest, snapshot, true, readAdmitted);
        public void Dispose() => directory.Dispose();
    }

    private static void Wait(ManualResetEventSlim signal) => Assert.True(signal.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
    private static Task Run(Action action) => Task.Run(action, TestContext.Current.CancellationToken);
    private static Task<T> Run<T>(Func<T> action) => Task.Run(action, TestContext.Current.CancellationToken);
    private static SafeFileHandle OpenDirectoryForReparse(string path) => CreateFileW(path, 0x40000000, 7, 0, 3, 0x02200000, 0);
    private static void SetJunction(string path, string target)
        => Assert.Equal(0, TrySetJunction(path, target));
    private static int TrySetJunction(string path, string target)
    {
        byte[] substitute = Encoding.Unicode.GetBytes(@"\??\" + Path.GetFullPath(target)), print = Encoding.Unicode.GetBytes(Path.GetFullPath(target));
        byte[] buffer = new byte[16 + substitute.Length + 2 + print.Length + 2];
        BitConverter.GetBytes(0xA0000003u).CopyTo(buffer, 0);
        BitConverter.GetBytes(checked((ushort)(buffer.Length - 8))).CopyTo(buffer, 4);
        BitConverter.GetBytes(checked((ushort)substitute.Length)).CopyTo(buffer, 10);
        BitConverter.GetBytes(checked((ushort)(substitute.Length + 2))).CopyTo(buffer, 12);
        BitConverter.GetBytes(checked((ushort)print.Length)).CopyTo(buffer, 14);
        substitute.CopyTo(buffer, 16); print.CopyTo(buffer, 18 + substitute.Length);
        using var handle = OpenDirectoryForReparse(path);
        if (handle.IsInvalid) return Marshal.GetLastWin32Error();
        return DeviceIoControl(handle, 0x900A4, buffer, buffer.Length, 0, 0, out _, 0) ? 0 : Marshal.GetLastWin32Error();
    }
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, nint security, uint disposition, uint flags, nint template);
    [DllImport("kernel32", SetLastError = true)]
    private static extern bool DeviceIoControl(SafeFileHandle handle, uint code, byte[] input, int inputSize, nint output, int outputSize, out int returned, nint overlapped);
}
