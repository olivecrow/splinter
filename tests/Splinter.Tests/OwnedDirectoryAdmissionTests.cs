using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class OwnedDirectoryAdmissionTests
{
    [Fact]
    public void EveryDescendantIncludingEmptyDirectoriesStaysPinnedUntilClose()
    {
        using var f = new Fixture(8, 4096);
        using (var tree = f.Open(2, TestContext.Current.CancellationToken))
        {
            foreach (string relative in new[] { "a", "a/b", "a/b/c", "empty" })
            {
                string path = Path.Combine(f.Root, relative);
                Assert.Throws<IOException>(() => Directory.Move(path, path + "-moved"));
                Assert.NotEqual(0, OwnedDirectoryLifetimeTests.TrySetJunction(path, f.Outside));
            }
            Assert.Throws<IOException>(() => File.WriteAllBytes(f.File(0), [1]));
            byte[] read = new byte[4096]; Assert.Equal(read.Length, tree.Read("a/b/c/f0000", 0, read)); Assert.Equal(f.Payload, read);
        }
        Directory.Delete(Path.Combine(f.Root, "empty")); Directory.Move(Path.Combine(f.Root, "a"), Path.Combine(f.Root, "moved"));
    }

    [Theory]
    [InlineData("content")]
    [InlineData("missing-parent")]
    [InlineData("junction")]
    public void FailedAdmissionPreservesSourceAndReleasesEveryPartialPin(string change)
    {
        using var f = new Fixture(8, 4096);
        string? junction = null;
        if (change == "content") File.WriteAllBytes(f.File(7), Enumerable.Repeat((byte)91, 4096).ToArray());
        else if (change == "missing-parent") f.Manifest = f.Manifest with { Items = f.Manifest.Items.Where(i => i.Entry.Path != "a/b").ToArray() };
        else
        {
            junction = Path.Combine(f.Root, "empty"); OwnedDirectoryLifetimeTests.SetJunction(junction, f.Outside);
        }
        try { Assert.Throws<WorkspaceException>(() => f.Open(4, TestContext.Current.CancellationToken)); }
        finally { if (junction is not null) Directory.Delete(junction); }
        Assert.True(File.Exists(f.File(7)));
        Directory.Move(f.Root, f.Root + "-preserved");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedOrCancelledParallelHashDrainsWorkersBeforeClosingHandles(bool cancel)
    {
        using var f = new Fixture(16, 4096);
        using var entered = new ManualResetEventSlim(); using var failure = new ManualResetEventSlim(); using var resume = new ManualResetEventSlim();
        using var cancellation = new CancellationTokenSource();
        int count = 0;
        var open = Task.Run(() => f.Open(2, cancellation.Token, _ =>
        {
            int index = Interlocked.Increment(ref count);
            if (index == 1) { entered.Set(); Wait(resume); }
            else if (index == 2)
            {
                Wait(entered);
                if (cancel) cancellation.Cancel();
                failure.Set();
                if (!cancel) throw new IOException("injected content admission failure");
            }
        }), TestContext.Current.CancellationToken);
        try
        {
            Wait(failure); Assert.False(open.IsCompleted);
            Assert.Throws<IOException>(() => File.WriteAllBytes(f.File(0), [2]));
            Assert.Throws<IOException>(() => Directory.Move(Path.Combine(f.Root, "empty"), Path.Combine(f.Root, "renamed")));
        }
        finally { resume.Set(); }
        var error = await Record.ExceptionAsync(async () => { using var tree = await open.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken); });
        if (cancel) Assert.IsAssignableFrom<OperationCanceledException>(error); else Assert.IsType<IOException>(error);
        File.WriteAllBytes(f.File(0), [2]); Directory.Move(f.Root, f.Root + "-closed");
    }

    [Fact]
    public void AdmissionCostForOneTwoAndFourHashWorkers()
    {
        string? output = Environment.GetEnvironmentVariable("SPLINTER_OWNED_DIRECTORY_ADMISSION_EVIDENCE");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(output), "Set SPLINTER_OWNED_DIRECTORY_ADMISSION_EVIDENCE to a new JSON path for bounded ordinary-user cost evidence.");
        output = Path.GetFullPath(output!); Assert.False(File.Exists(output), "Preserve prior evidence by choosing a new output path.");
        using var f = new Fixture(512, 131072);
        var samples = new List<object>();
        // Warm one admission, then rotate the order of all candidates. The
        // reference reproduces the previous full-ancestor, serial-hash loop.
        using (f.Open(1, TestContext.Current.CancellationToken)) { }
        for (int round = 0; round < 3; round++)
            foreach (int workers in new[] { 0, 1, 2, 4 }.Skip(round).Concat(new[] { 0, 1, 2, 4 }.Take(round)))
            {
                long allocation = GC.GetTotalAllocatedBytes(); long started = Stopwatch.GetTimestamp();
                if (workers == 0) f.ReferenceAdmission(); else using (f.Open(workers, TestContext.Current.CancellationToken)) { }
                samples.Add(new { round, workers, seconds = Stopwatch.GetElapsedTime(started).TotalSeconds, allocatedBytes = GC.GetTotalAllocatedBytes() - allocation });
            }
        using var target = new FileStream(output!, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(target, new { measuredAt = DateTimeOffset.UtcNow,
            runtimeSha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(typeof(OwnedDirectorySnapshotTree).Assembly.Location))),
            fixtureFiles = 512, fileBytes = 131072, directoryCount = f.Manifest.Items.Count(i => i.Entry.Directory), hashScratchBytesPerWorker = 65536,
            scope = "Small warm NTFS admission only; workers=0 is the previous serial full-ancestor loop. No Unity, mount or end-to-end timing qualification.", samples }, new JsonSerializerOptions { WriteIndented = true });
    }

    private static void Wait(ManualResetEventSlim signal) => Assert.True(signal.Wait(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken));
    private sealed class Fixture : IDisposable
    {
        private readonly TestDirectory temporary = new();
        internal string Root => Path.Combine(temporary.Path, "basis");
        internal string Outside => Path.Combine(temporary.Path, "outside");
        internal byte[] Payload { get; }
        internal OwnedDirectoryManifest Manifest { get; set; }
        private readonly SnapshotReference snapshot;
        internal Fixture(int count, int bytes)
        {
            Assert.SkipUnless(OperatingSystem.IsWindows(), "Requires Windows native namespace and sharing guards.");
            Directory.CreateDirectory(Path.Combine(Root, "a/b/c")); Directory.CreateDirectory(Path.Combine(Root, "empty")); Directory.CreateDirectory(Outside);
            Payload = new byte[bytes]; new Random(749).NextBytes(Payload);
            for (int i = 0; i < count; i++) System.IO.File.WriteAllBytes(File(i), Payload);
            snapshot = new("admission", OwnedDirectorySnapshotTree.ProviderName, Root, Root, DateTimeOffset.UnixEpoch, true, true, "stopped-original-owned-directory");
            Manifest = OwnedDirectorySnapshotTree.Capture(snapshot, "owner");
        }
        internal string File(int index) => Path.Combine(Root, "a/b/c", $"f{index:0000}");
        internal OwnedDirectorySnapshotTree Open(int workers, CancellationToken ct = default, Action<string>? beforeHash = null)
            => new(Manifest, snapshot, true, null, workers, ct, beforeHash);
        internal void ReferenceAdmission()
        {
            using var lease = new WindowsOwnedDirectoryLease(Root);
            var handles = new List<Microsoft.Win32.SafeHandles.SafeFileHandle>();
            try
            {
                foreach (var item in Manifest.Items)
                {
                    string path = Path.Combine(Root, item.Entry.Path.Replace('/', Path.DirectorySeparatorChar)); WorkspaceStore.EnsurePlainPath(path);
                    var attributes = System.IO.File.GetAttributes(path);
                    if (item.Entry.Directory != ((attributes & FileAttributes.Directory) != 0) || (attributes & FileAttributes.ReparsePoint) != 0)
                        throw new WorkspaceException("GeneratedBasisChanged", "Reference admission rejected changed metadata.");
                    if (item.Entry.Directory) continue;
                    var handle = System.IO.File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.RandomAccess); handles.Add(handle);
                    if (item.Entry.Length != RandomAccess.GetLength(handle) || item.Hash != OwnedDirectorySnapshotTree.Hash(handle, TestContext.Current.CancellationToken))
                        throw new WorkspaceException("GeneratedBasisChanged", "Reference admission rejected changed bytes.");
                }
            }
            finally { foreach (var handle in handles) handle.Dispose(); }
        }
        public void Dispose() => temporary.Dispose();
    }
}
