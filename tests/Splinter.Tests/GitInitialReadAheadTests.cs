using Splinter.Core;
using Splinter.SourceControl.Git;

namespace Splinter.Tests;

public sealed class GitInitialReadAheadTests
{
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task AttachDrainsReadAheadEvenWhenIndexRefreshFails(bool dirty)
    {
        using var f = await Fixture.Create(dirty);
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<object> attach = GitOverlayRegistration.AttachAsync(f.Receipt, TestContext.Current.CancellationToken, false,
            path => { entered.SetResult(path); Assert.True(release.Wait(TimeSpan.FromSeconds(20))); });
        try
        {
            Assert.Equal("nested space/large ü.bin", await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
            Assert.False(attach.IsCompleted);
        }
        finally { release.Set(); }
        if (dirty) Assert.Equal("GitCommandFailed", (await Assert.ThrowsAsync<WorkspaceException>(() => attach)).Code);
        else { await attach; Assert.Equal("", await TestDirectory.Git(f.Mount, "status", "--porcelain")); }
        Assert.False(File.Exists(Path.Combine(f.Receipt.GitDirectory, "index.lock")));
        Assert.Equal(f.Original, await File.ReadAllBytesAsync(f.SourceFile, TestContext.Current.CancellationToken));
        Assert.Equal(dirty ? (byte)42 : f.Original[0], (await File.ReadAllBytesAsync(f.MountFile, TestContext.Current.CancellationToken))[0]);
    }

    [Fact]
    public async Task ReadAheadFailureStillFinishesRefreshAndExistingIndexResumesWithoutReadAhead()
    {
        using var f = await Fixture.Create();
        await Assert.ThrowsAsync<IOException>(() => GitOverlayRegistration.AttachAsync(f.Receipt,
            TestContext.Current.CancellationToken, false, _ => throw new IOException("injected read failure")));
        Assert.DoesNotContain("mtime: 0:0", await TestDirectory.Git(f.Mount, "ls-files", "--debug"));
        Assert.False(File.Exists(Path.Combine(f.Receipt.GitDirectory, "index.lock")));
        await GitOverlayRegistration.AttachAsync(f.Receipt, TestContext.Current.CancellationToken, false,
            _ => Assert.Fail("An existing index must refresh without speculative reads."));
        Assert.Equal("", await TestDirectory.Git(f.Mount, "status", "--porcelain"));
    }

    [Fact]
    public async Task ChangedCandidateSkipsReadAheadButStillReportsItsChanges()
    {
        using var f = await Fixture.Create(dirty: true);
        await GitOverlayRegistration.AttachAsync(f.Receipt, TestContext.Current.CancellationToken, true,
            _ => Assert.Fail("Changed candidates do not need speculative initial reads."));
        Assert.Contains("large", await TestDirectory.Git(f.Mount, "status", "--porcelain"));
        Assert.Equal(f.Original, await File.ReadAllBytesAsync(f.SourceFile, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CancellationJoinsTheAdmittedReadBeforeAttachCompletes()
    {
        using var f = await Fixture.Create();
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken); using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<object> attach = GitOverlayRegistration.AttachAsync(f.Receipt, cancelled.Token, false,
            _ => { entered.SetResult(); Assert.True(release.Wait(TimeSpan.FromSeconds(20))); });
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken); cancelled.Cancel();
            Assert.False(attach.IsCompleted);
        }
        finally { release.Set(); }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => attach);
        Assert.Equal(f.Original, await File.ReadAllBytesAsync(f.SourceFile, TestContext.Current.CancellationToken));
        Assert.Equal(f.Original, await File.ReadAllBytesAsync(f.MountFile, TestContext.Current.CancellationToken));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly TestDirectory directory = new();
        internal string Source => Path.Combine(directory.Path, "source");
        internal string Mount => Path.Combine(directory.Path, "mount");
        internal string SourceFile => Path.Combine(Source, "nested space", "large ü.bin");
        internal string MountFile => Path.Combine(Mount, "nested space", "large ü.bin");
        internal OverlayGitReceipt Receipt = null!;
        internal readonly byte[] Original = new byte[2 * 1024 * 1024];
        internal static async Task<Fixture> Create(bool dirty = false)
        {
            var f = new Fixture(); new Random(123).NextBytes(f.Original); f.Original[0] = 17;
            Directory.CreateDirectory(Path.GetDirectoryName(f.SourceFile)!);
            await TestDirectory.Git(f.Source, "init", "-b", "development");
            await TestDirectory.Git(f.Source, "config", "core.autocrlf", "false");
            await TestDirectory.Git(f.Source, "config", "user.name", "Fixture");
            await TestDirectory.Git(f.Source, "config", "user.email", "fixture@example.invalid");
            await File.WriteAllBytesAsync(f.SourceFile, f.Original);
            await File.WriteAllTextAsync(Path.Combine(f.Source, "small.txt"), "small\n");
            await TestDirectory.Git(f.Source, "add", "."); await TestDirectory.Git(f.Source, "commit", "-m", "basis");
            string revision = await TestDirectory.Git(f.Source, "rev-parse", "HEAD");
            f.Receipt = await GitOverlayRegistration.PrepareAsync(f.Source, f.Mount, "codex/read-ahead", revision,
                "read-ahead", Path.Combine(f.directory.Path, "receipt.json"), TestContext.Current.CancellationToken);
            Directory.CreateDirectory(Path.GetDirectoryName(f.MountFile)!);
            byte[] bytes = f.Original.ToArray(); if (dirty) bytes[0] = 42;
            await File.WriteAllBytesAsync(f.MountFile, bytes);
            await File.WriteAllTextAsync(Path.Combine(f.Mount, "small.txt"), "small\n");
            return f;
        }
        public void Dispose() => directory.Dispose();
    }
}
