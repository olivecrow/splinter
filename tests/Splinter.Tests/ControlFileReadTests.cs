using System.Diagnostics;
using System.Text.Json;
using Splinter.Core;
using Splinter.Platform.Windows;

namespace Splinter.Tests;

[CollectionDefinition("Windows control-file timing", DisableParallelization = true)]
public sealed class ControlFileTimingCollection;

[Collection("Windows control-file timing")]
public sealed class ControlFileReadTests
{
    [Fact]
    public async Task ConcurrentReplacementNeverLooksMissingOrReturnsPartialState()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows Replace/read race.");
        using var d = new TestDirectory();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(30)); var ct = deadline.Token;
        string path = Path.Combine(d.Path, "state.json");
        await WindowsProcessWorkspaceProvider.WriteAsync(path, Receipt.For(0), ct);
        int finished = 0;
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readers = Enumerable.Range(0, 4).Select(_ => Task.Run(async () =>
        {
            await start.Task;
            int prior = 0;
            do
            {
                var value = await WindowsProcessWorkspaceProvider.ReadAsync<Receipt>(path, ct);
                Assert.NotNull(value);
                Assert.Equal(Receipt.For(value.Sequence), value);
                Assert.InRange(value.Sequence, prior, 500);
                prior = value.Sequence;
            } while (Volatile.Read(ref finished) == 0);
        }, ct)).ToArray();
        start.SetResult();
        try
        {
            for (int i = 1; i <= 500; i++) await WindowsProcessWorkspaceProvider.WriteAsync(path, Receipt.For(i), ct);
        }
        finally
        {
            Volatile.Write(ref finished, 1);
            await Task.WhenAll(readers);
        }
        Assert.Equal(Receipt.For(500), await WindowsProcessWorkspaceProvider.ReadAsync<Receipt>(path, ct));
        Assert.Empty(Directory.GetFiles(d.Path, "*.tmp"));
    }

    [Fact]
    public async Task ReaderWaitsForFirstPublicationInsteadOfReportingTransientAbsence()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows publication grace.");
        using var d = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        string path = Path.Combine(d.Path, "state.json");
        var read = WindowsProcessWorkspaceProvider.ReadAsync<Receipt>(path, ct);
        await Task.Delay(100, ct); Assert.False(read.IsCompleted);
        await WindowsProcessWorkspaceProvider.WriteAsync(path, Receipt.For(1), ct);
        Assert.Equal(Receipt.For(1), await read);
    }

    [Fact]
    public async Task BriefExclusiveReaderIsRetriedWithoutChangingTheReceipt()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows file sharing.");
        using var d = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        string path = Path.Combine(d.Path, "state.json");
        await WindowsProcessWorkspaceProvider.WriteAsync(path, Receipt.For(1), ct);
        Task<Receipt?> read;
        using (var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            read = WindowsProcessWorkspaceProvider.ReadAsync<Receipt>(path, ct);
            await Task.Delay(100, ct); Assert.False(read.IsCompleted);
        }
        Assert.Equal(Receipt.For(1), await read);
        Assert.Single(Directory.GetFiles(d.Path));
    }

    [Fact]
    public async Task PersistentSharingFailureIsBoundedAndTheSameReadCanBeRetried()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows file sharing.");
        using var d = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        string path = Path.Combine(d.Path, "state.json");
        await WindowsProcessWorkspaceProvider.WriteAsync(path, Receipt.For(1), ct);
        byte[] before = await File.ReadAllBytesAsync(path, ct);
        using (var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var elapsed = Stopwatch.StartNew();
            var error = await Assert.ThrowsAsync<IOException>(() => WindowsProcessWorkspaceProvider.ReadAsync<Receipt>(path, ct));
            Assert.Equal(32, error.HResult & 0xffff);
            Assert.InRange(elapsed.Elapsed.TotalSeconds, 0.9, 5);
        }
        Assert.Equal(before, await File.ReadAllBytesAsync(path, ct));
        Assert.Equal(Receipt.For(1), await WindowsProcessWorkspaceProvider.ReadAsync<Receipt>(path, ct));
        Assert.Single(Directory.GetFiles(d.Path));
    }

    [Fact]
    public async Task CancelledReadDoesNotWaitForThePublicationDeadline()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows file sharing.");
        using var d = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        string path = Path.Combine(d.Path, "state.json");
        await WindowsProcessWorkspaceProvider.WriteAsync(path, Receipt.For(1), ct);
        using var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var read = WindowsProcessWorkspaceProvider.ReadAsync<Receipt>(path, cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read.WaitAsync(TimeSpan.FromMilliseconds(500), ct));
    }

    [Fact]
    public async Task TrueAbsenceIsBoundedAndOptionalRequestPollingDoesNotWait()
    {
        using var d = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        string path = Path.Combine(d.Path, "stop.json");
        var poll = WindowsProcessWorkspaceProvider.ReadAsync<Receipt>(path, ct, waitForPublication: false);
        Assert.True(poll.IsCompletedSuccessfully); Assert.Null(await poll);
        Assert.Null(await WindowsProcessWorkspaceProvider.ReadAsync<Receipt>(path, ct).WaitAsync(TimeSpan.FromSeconds(5), ct));
        Assert.Empty(Directory.GetFiles(d.Path));
    }

    [Fact]
    public async Task CompleteCorruptJsonIsRejectedImmediatelyAndPreserved()
    {
        using var d = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        string path = Path.Combine(d.Path, "state.json"); const string corrupt = "{\"sequence\":1,";
        await File.WriteAllTextAsync(path, corrupt, ct);
        await Assert.ThrowsAsync<JsonException>(() => WindowsProcessWorkspaceProvider.ReadAsync<Receipt>(path, ct));
        Assert.Equal(corrupt, await File.ReadAllTextAsync(path, ct));
        Assert.Single(Directory.GetFiles(d.Path));
    }

    [Fact]
    public async Task AccessDeniedIsNotReportedAsMissing()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows directory access denial.");
        using var d = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        // Opening a directory as a file yields ERROR_ACCESS_DENIED without
        // modifying ACLs or depending on the account's administrator status.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => WindowsProcessWorkspaceProvider.ReadAsync<Receipt>(d.Path, ct));
        Assert.True(Directory.Exists(d.Path)); Assert.Empty(Directory.GetFiles(d.Path));
    }

    [Fact]
    public async Task ReparseParentRemainsRejected()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows junction guard.");
        using var d = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        string target = Path.Combine(d.Path, "target"), link = Path.Combine(d.Path, "link");
        Directory.CreateDirectory(target);
        await WindowsProcessWorkspaceProvider.WriteAsync(Path.Combine(target, "state.json"), Receipt.For(1), ct);
        var start = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardError = true, RedirectStandardOutput = true,
            Arguments = $"/d /c mklink /J \"{link}\" \"{target}\"" };
        using var process = Process.Start(start)!;
        await process.WaitForExitAsync(ct); Assert.Equal(0, process.ExitCode);
        try
        {
            var error = await Assert.ThrowsAsync<WorkspaceException>(() => WindowsProcessWorkspaceProvider.ReadAsync<Receipt>(Path.Combine(link, "state.json"), ct));
            Assert.Equal("UnsafePath", error.Code);
            Assert.Equal(Receipt.For(1), await WindowsProcessWorkspaceProvider.ReadAsync<Receipt>(Path.Combine(target, "state.json"), ct));
        }
        finally { Directory.Delete(link); }
    }

    private sealed record Receipt(int Sequence, string Body)
    {
        internal static Receipt For(int sequence) => new(sequence, new string((char)('a' + sequence % 26), 8192));
    }
}
