using System.Text.Json;
using Splinter.Core;
using Splinter.SourceControl.Git;

namespace Splinter.Tests;

public sealed class GitPreparationObservationTests
{
    [Fact]
    public async Task ConcurrentIntervalsRetainTheirOverlapInsteadOfAddingElapsedTime()
    {
        var observation = new GitPreparationObservation();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task a = observation.Measure("warm", () => release.Task);
        Task b = observation.Measure("refresh", () => release.Task);
        release.SetResult(); await Task.WhenAll(a, b);
        var measured = observation.Observe();
        Assert.Equal(2, measured.Intervals.Length);
        Assert.True(measured.Intervals.Max(i => i.Start) <= measured.Intervals.Min(i => i.End));
        Assert.Equal(0, measured.DroppedIntervals);
    }

    [Fact]
    public async Task FailureRemainsFailureAndBoundedHistoryReportsDroppedIntervals()
    {
        var observation = new GitPreparationObservation();
        var failure = new IOException("expected");
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => observation.Measure("failed", () => Task.FromException(failure))));
        Assert.False(Assert.Single(observation.Observe().Intervals).Succeeded);
        for (int i = 0; i < 18; i++) await observation.Measure("bounded", () => Task.CompletedTask);
        var observed = observation.Observe();
        Assert.Equal(16, observed.Intervals.Length); Assert.Equal(3, observed.DroppedIntervals);
        Assert.True(observed.StopwatchFrequency > 0);
        Assert.All(observed.Intervals, x => Assert.InRange(x.Start, observed.Started, x.End));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LaterFailedOrCancelledAttemptReplacesPriorSuccessBeforeRunning(bool cancel)
    {
        using var directory = new TestDirectory();
        var workspace = Workspace(directory.Path);
        string path = Path.Combine(directory.Path, "git-preparation.json");
        await GitWorkspaceProvider.RunPreparationAttemptAsync(workspace, directory.Path,
            new GitPreparationObservation(), () => Task.CompletedTask);
        var prior = Read(path);
        Assert.Equal("succeeded", prior.Outcome);
        var observation = new GitPreparationObservation();
        Exception failure = cancel ? new OperationCanceledException("expected") : new WorkspaceException("ExpectedFailure", "expected");
        Exception? caught = await Record.ExceptionAsync(() => GitWorkspaceProvider.RunPreparationAttemptAsync(workspace,
            directory.Path, observation, () => observation.Measure("load-receipt", () =>
            {
                var running = Read(path);
                Assert.Equal("running", running.Outcome);
                Assert.NotEqual(prior.AttemptId, running.AttemptId);
                Assert.Null(running.Ended); Assert.Null(running.EndedAt);
                return Task.FromException(failure);
            })));
        Assert.Same(failure, caught);
        var current = Read(path);
        Assert.Equal(observation.Observe().AttemptId, current.AttemptId);
        Assert.Equal(cancel ? "cancelled" : "failed", current.Outcome);
        Assert.Equal(cancel ? null : "ExpectedFailure", current.FailureCode);
        Assert.Equal(failure.GetType().Name, current.FailureKind);
        Assert.True(current.Ended!.Value >= current.Started); Assert.NotNull(current.EndedAt);
        Assert.False(Assert.Single(current.Intervals).Succeeded);
        using var envelope = JsonDocument.Parse(await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        Assert.Equal(workspace.Id, envelope.RootElement.GetProperty("workspaceId").GetString());
        Assert.Equal(workspace.RequestDigest, envelope.RootElement.GetProperty("requestDigest").GetString());
        Assert.Single(Directory.GetFiles(directory.Path));
    }

    [Fact]
    public async Task SuccessRetainsOneAttemptAndTheAttachClockIndependentOfCapture()
    {
        var observation = new GitPreparationObservation();
        var published = new List<GitPreparationSnapshot>();
        await observation.Run(() => observation.Measure("attach", () => Task.CompletedTask), snapshot =>
        { published.Add(snapshot); return Task.CompletedTask; });
        Assert.Equal(2, published.Count);
        var running = published[0]; var success = published[1];
        Assert.Equal("running", running.Outcome); Assert.Equal("succeeded", success.Outcome);
        Assert.Equal(running.AttemptId, success.AttemptId);
        Assert.Equal(running.StartedAt, success.StartedAt); Assert.Equal(running.Started, success.Started);
        Assert.Null(running.Ended); Assert.NotNull(success.Ended); Assert.NotNull(success.EndedAt);
        Assert.Null(success.FailureKind); Assert.Null(success.FailureCode);
        var later = observation.Observe();
        Assert.Equal(success.Ended, later.Ended); Assert.Equal(success.EndedAt, later.EndedAt);
        Assert.Equal(success.Intervals, later.Intervals);
    }

    [Theory]
    [InlineData("succeeded")]
    [InlineData("failed")]
    [InlineData("cancelled")]
    public async Task CompletionPublicationFailurePreservesOperationResultAndRunningMarker(string outcome)
    {
        var observation = new GitPreparationObservation();
        GitPreparationSnapshot? stored = null;
        int publications = 0;
        Exception? expected = outcome == "failed" ? new IOException("operation failure")
            : outcome == "cancelled" ? new OperationCanceledException("operation cancelled") : null;
        Exception? caught = await Record.ExceptionAsync(() => observation.Run(
            () => expected is null ? Task.CompletedTask : Task.FromException(expected), snapshot =>
            {
                if (++publications == 2) throw new IOException("diagnostic publication failed");
                stored = snapshot; return Task.CompletedTask;
            }));
        Assert.Same(expected, caught); Assert.Equal(2, publications);
        Assert.Equal("running", stored!.Outcome); Assert.Null(stored.Ended);
        Assert.Equal(outcome, observation.Observe().Outcome);
    }

    [Fact]
    public async Task StartPublicationFailureDoesNotBeginAttach()
    {
        var observation = new GitPreparationObservation();
        var failure = new IOException("start publication failed");
        bool ran = false;
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => observation.Run(
            () => { ran = true; return Task.CompletedTask; }, _ => Task.FromException(failure))));
        Assert.False(ran);
    }

    [Fact]
    public async Task DisabledObservationDoesNotReadOrReplacePriorDiagnosticFile()
    {
        using var directory = new TestDirectory();
        string path = Path.Combine(directory.Path, "git-preparation.json");
        await File.WriteAllTextAsync(path, "unparsed prior observation", TestContext.Current.CancellationToken);
        var expected = new IOException("operation failure");
        Assert.Same(expected, await Assert.ThrowsAsync<IOException>(() => GitWorkspaceProvider.RunPreparationAttemptAsync(
            Workspace(directory.Path), directory.Path, null, () => Task.FromException(expected))));
        Assert.Equal("unparsed prior observation", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        Assert.Single(Directory.GetFiles(directory.Path));
    }

    private static GitPreparationSnapshot Read(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("preparation").Deserialize<GitPreparationSnapshot>(
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    private static WorkspaceRecord Workspace(string root) => new()
    {
        Id = "fixed-workspace", RequestKey = "fixed-key", RequestDigest = "fixed-request-digest",
        ProjectPath = Path.Combine(root, "original"), Path = Path.Combine(root, "workspace"),
        SourceReference = "refs/heads/workspace", BaseReference = "main", BaseRevision = new string('a', 40)
    };
}
