using System.Diagnostics;
using Splinter.Core;
using Splinter.SourceControl.Git;

namespace Splinter.Tests;

public sealed class GitRecordSilenceTests
{
    [Fact]
    public async Task ADataScaledRecordCommandCanRemainSilentBeyondTheProtocolReaderDeadline()
    {
        using var files = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        await GitWorkspaceProvider.Required(files.Path, ["init", "--quiet"], ct);
        var watch = Stopwatch.StartNew();
        // A real Git-owned shell command models status inspecting a clean tree:
        // it legitimately has no output until the whole data operation ends.
        var rows = GitWorkspaceProvider.GitRecords(files.Path,
            ["-c", "alias.scal-silent=!sleep 31; printf 'scal-record\\0'", "scal-silent"], ct).ToArray();
        Assert.Equal(["scal-record"], rows); Assert.True(watch.Elapsed >= TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task CancellationStillDrainsASilentOwnedRecordCommandWithoutReturningPartialRows()
    {
        using var files = new TestDirectory();
        await GitWorkspaceProvider.Required(files.Path, ["init", "--quiet"], TestContext.Current.CancellationToken);
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancel.CancelAfter(TimeSpan.FromMilliseconds(500));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Task.Run(() =>
            GitWorkspaceProvider.GitRecords(files.Path,
                ["-c", "alias.scal-silent=!sleep 60; printf 'unpublished\\0'", "scal-silent"], cancel.Token).ToArray(),
            TestContext.Current.CancellationToken));
    }
}
