using System.Diagnostics;
using Splinter.SourceControl.Git;

namespace Splinter.Tests;

public sealed class GitCommandLifetimeTests
{
    [Fact]
    public async Task AStreamingBlobMayRemainSilentBeyondTheControlQueryDeadline()
    {
        using var files = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        using var process = StartHash(files.Path);
        var stdout = process.StandardOutput.ReadToEndAsync(ct); var stderr = process.StandardError.ReadToEndAsync(ct);
        using var lifetime = new GitCommandLifetime(["hash-object", "--stdin"], ct, TimeSpan.FromMilliseconds(1));
        await lifetime.WaitAsync(process, async token =>
        {
            await process.StandardInput.BaseStream.WriteAsync("te"u8.ToArray(), token);
            await Task.Delay(100, token);
            await process.StandardInput.BaseStream.WriteAsync("st content\n"u8.ToArray(), token);
            process.StandardInput.Close();
        }, stdout, stderr);
        Assert.Equal(0, process.ExitCode); Assert.Equal("d670460b4b4aece5915caf5c68d12f560a9fe3e4", (await stdout).Trim()); Assert.Empty(await stderr);
    }

    [Fact]
    public async Task CancellingAStreamingBlobKillsItsOwnedChildAndDrainsTheProducerAndBothPipes()
    {
        using var files = new TestDirectory(); using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var process = StartHash(files.Path); var stdout = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken); var stderr = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        using var lifetime = new GitCommandLifetime(["hash-object", "--stdin"], cancelled.Token, TimeSpan.FromMilliseconds(1));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); bool producerDrained = false;
        var operation = lifetime.WaitAsync(process, async token =>
        {
            try
            {
                await process.StandardInput.BaseStream.WriteAsync("te"u8.ToArray(), token); entered.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            finally { producerDrained = true; }
        }, stdout, stderr);
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
        Assert.True(process.HasExited); Assert.True(producerDrained); Assert.True(stdout.IsCompletedSuccessfully); Assert.True(stderr.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task AFailedProducerCannotLeaveGitWaitingForItsUnfinishedInput()
    {
        using var files = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        using var process = StartHash(files.Path); var stdout = process.StandardOutput.ReadToEndAsync(ct); var stderr = process.StandardError.ReadToEndAsync(ct);
        using var lifetime = new GitCommandLifetime(["hash-object", "--stdin"], ct);
        await Assert.ThrowsAsync<IOException>(() => lifetime.WaitAsync(process, async token =>
        {
            await process.StandardInput.BaseStream.WriteAsync("partial"u8.ToArray(), token);
            throw new IOException("Preserve the fixed source request after a failed payload producer.");
        }, stdout, stderr));
        Assert.True(process.HasExited); Assert.True(stdout.IsCompletedSuccessfully); Assert.True(stderr.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task SmallControlQueriesRetainTheirBoundedDeadline()
    {
        using var lifetime = new GitCommandLifetime(["-c", "core.longpaths=true", "rev-parse", "--git-dir"],
            TestContext.Current.CancellationToken, TimeSpan.FromMilliseconds(20));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Task.Delay(Timeout.InfiniteTimeSpan, lifetime.Token));
    }

    private static Process StartHash(string directory)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var key in start.Environment.Keys.Where(key => key.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase)).ToArray()) start.Environment.Remove(key);
        start.Environment["GIT_TERMINAL_PROMPT"] = "0"; start.ArgumentList.Add("hash-object"); start.ArgumentList.Add("--stdin");
        return Process.Start(start) ?? throw new InvalidOperationException("Git did not start.");
    }
}
