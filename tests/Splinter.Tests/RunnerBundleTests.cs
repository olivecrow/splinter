using System.Security.Cryptography;
using Splinter.Core;
using Splinter.Platform.Windows;

namespace Splinter.Tests;

// Error-path handle checks open a source config for writing without changing it.
// Serialize with other launches so their short source read locks cannot interfere.
[Collection("Windows inherited-handle observation")]
public sealed class RunnerBundleTests
{
    private static readonly string[] Names = ["Splinter.Platform.Windows.dll", "Splinter.Platform.Windows.deps.json",
        "Splinter.Platform.Windows.runtimeconfig.json", "Splinter.Core.dll", "Splinter.Runtime.dll", "Splinter.SourceControl.Files.dll", "Splinter.SourceControl.Git.dll", "winfsp-msil.dll"];

    [Fact(Timeout = 5000)]
    public async Task BriefChildReaderDefersTheSameDirectoryPublicationWithoutChangingItsBytes()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Requires native directory sharing semantics.");
        using var files = new TestDirectory(); string pending = Path.Combine(files.Path, "pending"), target = Path.Combine(files.Path, "published");
        Directory.CreateDirectory(pending); string file = Path.Combine(pending, "bundle.bin"); File.WriteAllText(file, "fixed bundle bytes");
        using var held = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
        var publication = Task.Run(() => WindowsProcessWorkspaceProvider.PublishRunnerDirectory(pending, target, TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        try
        {
            await Task.Delay(100, TestContext.Current.CancellationToken);
            Assert.False(publication.IsCompleted); Assert.True(Directory.Exists(pending)); Assert.False(Directory.Exists(target));
        }
        finally { held.Dispose(); await publication.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken); }
        Assert.False(Directory.Exists(pending)); Assert.Equal("fixed bundle bytes", File.ReadAllText(Path.Combine(target, "bundle.bin")));
    }

    [Fact(Timeout = 5000)]
    public void PersistentChildReaderPreservesTheUnpublishedBundleAndAllowsAnExplicitRetry()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Requires native directory sharing semantics.");
        using var files = new TestDirectory(); string pending = Path.Combine(files.Path, "pending"), target = Path.Combine(files.Path, "published");
        Directory.CreateDirectory(pending); string file = Path.Combine(pending, "bundle.bin"); File.WriteAllText(file, "fixed bundle bytes");
        using (var held = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var error = Assert.Throws<IOException>(() => WindowsProcessWorkspaceProvider.PublishRunnerDirectory(pending, target, TestContext.Current.CancellationToken));
            Assert.Equal(5, error.HResult & 0xffff); Assert.False(Directory.Exists(target)); Assert.Equal("fixed bundle bytes", File.ReadAllText(file));
        }
        WindowsProcessWorkspaceProvider.PublishRunnerDirectory(pending, target, TestContext.Current.CancellationToken);
        Assert.Equal("fixed bundle bytes", File.ReadAllText(Path.Combine(target, "bundle.bin")));
    }

    [Fact]
    public void ReuseKeepsTheSameBundleKeyAndDoesNotAllocateWholeBinaryArrays()
    {
        using var d = new TestDirectory();
        string cache = Path.Combine(d.Path, "runners");
        string runner = WindowsProcessWorkspaceProvider.PrepareRunner(cache);
        string source = Path.GetDirectoryName(typeof(WindowsProcessWorkspaceProvider).Assembly.Location)!;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string name in Names) hash.AppendData(SHA256.HashData(File.ReadAllBytes(Path.Combine(source, name))));
        Assert.Equal(Convert.ToHexStringLower(hash.GetHashAndReset()), Path.GetFileName(Path.GetDirectoryName(runner)));
        var files = Directory.GetFiles(Path.GetDirectoryName(runner)!).Order().ToArray();
        long bundleBytes = files.Sum(file => new FileInfo(file).Length);
        var writeTimes = files.Select(File.GetLastWriteTimeUtc).ToArray();
        for (int i = 0; i < 3; i++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            string reused = WindowsProcessWorkspaceProvider.PrepareRunner(cache);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(runner, reused);
            Assert.InRange(allocated, 0, bundleBytes / 2);
        }
        Assert.Single(Directory.GetDirectories(cache));
        Assert.Equal(8, files.Length);
        Assert.Equal(writeTimes, files.Select(File.GetLastWriteTimeUtc).ToArray());
        AssertSourceConfigUnlocked(source);
    }

    [Fact]
    public async Task ConcurrentPreparationsPublishOneCompleteBundle()
    {
        using var d = new TestDirectory();
        string cache = Path.Combine(d.Path, "runners");
        var paths = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(
            () => WindowsProcessWorkspaceProvider.PrepareRunner(cache), TestContext.Current.CancellationToken)));
        Assert.Single(paths.Distinct());
        string root = Assert.Single(Directory.GetDirectories(cache));
        Assert.Equal(Names.Order(), Directory.GetFiles(root).Select(Path.GetFileName).Order());
        Assert.Equal(paths[0], WindowsProcessWorkspaceProvider.PrepareRunner(cache));
    }

    [Theory]
    [InlineData("Splinter.Platform.Windows.dll")]
    [InlineData("Splinter.Platform.Windows.deps.json")]
    [InlineData("Splinter.Platform.Windows.runtimeconfig.json")]
    [InlineData("Splinter.Core.dll")]
    [InlineData("Splinter.Runtime.dll")]
    [InlineData("Splinter.SourceControl.Files.dll")]
    [InlineData("Splinter.SourceControl.Git.dll")]
    [InlineData("winfsp-msil.dll")]
    public void ChangedSameLengthCacheIsRejectedAndPreserved(string name)
    {
        using var d = new TestDirectory();
        string cache = Path.Combine(d.Path, "runners");
        string runner = WindowsProcessWorkspaceProvider.PrepareRunner(cache);
        string file = Path.Combine(Path.GetDirectoryName(runner)!, name);
        byte[] changed = File.ReadAllBytes(file);
        changed[changed.Length / 2] ^= 1;
        File.WriteAllBytes(file, changed);
        Assert.Equal("RunnerChanged", Assert.Throws<WorkspaceException>(
            () => WindowsProcessWorkspaceProvider.PrepareRunner(cache)).Code);
        Assert.Equal(changed, File.ReadAllBytes(file));
        Assert.Single(Directory.GetDirectories(cache));
        AssertSourceConfigUnlocked(Path.GetDirectoryName(typeof(WindowsProcessWorkspaceProvider).Assembly.Location)!);
    }

    [Fact]
    public void MissingCacheFileIsPreservedAsAnErrorAndReleasesSourceHandles()
    {
        using var d = new TestDirectory();
        string cache = Path.Combine(d.Path, "runners");
        string runner = WindowsProcessWorkspaceProvider.PrepareRunner(cache);
        string file = Path.Combine(Path.GetDirectoryName(runner)!, "Splinter.Platform.Windows.runtimeconfig.json");
        File.Delete(file);
        Assert.Throws<FileNotFoundException>(() => WindowsProcessWorkspaceProvider.PrepareRunner(cache));
        Assert.False(File.Exists(file));
        Assert.Single(Directory.GetDirectories(cache));
        AssertSourceConfigUnlocked(Path.GetDirectoryName(typeof(WindowsProcessWorkspaceProvider).Assembly.Location)!);
    }

    private static void AssertSourceConfigUnlocked(string source)
    {
        using var stream = new FileStream(Path.Combine(source, "Splinter.Platform.Windows.runtimeconfig.json"),
            FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
    }
}
