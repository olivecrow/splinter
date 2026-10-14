using System.Text.Json;
using Splinter.Cli;
using Splinter.Core;
using Splinter.Protocol;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed partial class SnapshotWorkspaceCoordinatorTests
{
    [Fact]
    public async Task CombinedCheckpointReleaseExtendsItsOwnSealAndKeepsFreshSourceVerification()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        string id = (await f.Ensure(ct)).Workspace.Id;
        var source = new CheckpointSource();
        var coordinator = new WorkspaceCoordinator(new(f.Home), source, f.App, f.Backend);
        f.App.Running = true;
        var result = await coordinator.CheckpointAndReleaseAsync(id, "preserve and release", ct);
        Assert.Equal("released", result.Workspace.Phase);
        Assert.Equal(1, source.Transactions); Assert.Equal(1, source.InspectCalls);
        Assert.Equal(4, f.Backend.SealCalls); Assert.Equal(1, f.App.Stops);
        Assert.Equal(1, f.App.InfrastructureRemovals);
        Assert.True(source.VerifyCalls > 0); Assert.True(f.Backend.DeltaRemoved); Assert.True(f.Backend.SnapshotReleased);
        Assert.Equal("result-ref", result.Workspace.Snapshot!.Result!.ResultReference);
        var response = WorkspaceCommands.Result("workspace.checkpoint", result, false, f.Home);
        Assert.True(new ContractCatalog().Validate("urn:splinter:v1:response",
            JsonSerializer.SerializeToElement(response, ApiJson.Options)).IsValid);
        int seals = f.Backend.SealCalls;
        Assert.Equal("released", (await coordinator.CheckpointAndReleaseAsync(id, "same request", ct)).Workspace.Phase);
        Assert.Equal(seals, f.Backend.SealCalls); Assert.Equal(1, source.Transactions);
        using var output = new StringWriter();
        Assert.Equal(0, await Program.RunAsync(["workspace", "checkpoint", "--id", id, "--message", "same request",
            "--release", "--home", f.Home], output, ct));
        using var envelope = JsonDocument.Parse(output.ToString());
        Assert.Equal("workspace.checkpoint", envelope.RootElement.GetProperty("command").GetString());
        Assert.Equal("released", envelope.RootElement.GetProperty("data").GetProperty("workspace").GetProperty("phase").GetString());
        Assert.True(new ContractCatalog().Validate("urn:splinter:v1:response", envelope.RootElement).IsValid);
    }

    [Theory]
    [InlineData("dirty", "UncollectedChanges")]
    [InlineData("revision", "UncollectedChanges")]
    [InlineData("fingerprint", "UncollectedChanges")]
    [InlineData("journal", "DeltaChangedAfterCollection")]
    [InlineData("journal-during-inspection", "DeltaChangedAfterCollection")]
    [InlineData("result", "ResultMissing")]
    [InlineData("owned-child", "ProcessCleanupIncomplete")]
    public async Task CombinedCheckpointReleaseRejectsChangesAfterTheCheckpointSeal(string change, string code)
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        string id = (await f.Ensure(ct)).Workspace.Id;
        var source = new CheckpointSource();
        var coordinator = new WorkspaceCoordinator(new(f.Home), source, f.App, f.Backend);
        f.Backend.AfterSeal = count =>
        {
            if (count != 2) return;
            if (change == "dirty") source.Dirty = true;
            if (change == "revision") source.RevisionOverride = "external-head";
            if (change == "fingerprint") source.Fingerprint = "changed-interpretation";
            if (change == "journal") f.Backend.AcknowledgeWrite();
            if (change == "journal-during-inspection") source.AfterInspect = f.Backend.AcknowledgeWrite;
            if (change == "result") source.InvalidResult = true;
            if (change == "owned-child") f.App.Children = 1;
        };
        var failure = await Assert.ThrowsAsync<WorkspaceException>(() => coordinator.CheckpointAndReleaseAsync(id, "result", ct));
        Assert.Equal(code, failure.Code);
        Assert.NotNull(new WorkspaceStore(f.Home).Get(id).Snapshot!.Result);
        Assert.False(f.Backend.DeltaRemoved); Assert.False(f.Backend.SnapshotReleased); Assert.True(f.Backend.Mounted);
    }

    [Fact]
    public async Task CombinedCheckpointReleaseStillRejectsWritesDuringUnmount()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        string id = (await f.Ensure(ct)).Workspace.Id;
        f.Backend.WriteDuringUnmount = true;
        Assert.Equal("DeltaChangedAfterCollection", (await Assert.ThrowsAsync<WorkspaceException>(() =>
            f.New().CheckpointAndReleaseAsync(id, "result", ct))).Code);
        Assert.Equal("release-unmount", new WorkspaceStore(f.Home).Get(id).Snapshot!.Step);
        Assert.False(f.Backend.DeltaRemoved); Assert.False(f.Backend.SnapshotReleased);
    }

    [Theory]
    [InlineData("unmount")]
    [InlineData("unregister")]
    [InlineData("delta")]
    [InlineData("snapshot-release")]
    public async Task CombinedCheckpointReleaseResumesDurableReleaseWithoutCheckpointingAgain(string boundary)
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        string id = (await f.Ensure(ct)).Workspace.Id;
        var source = new CheckpointSource();
        WorkspaceCoordinator New() => new(new(f.Home), source, f.App, f.Backend);
        if (boundary == "unregister") source.LoseReply = boundary;
        else if (boundary == "delta") f.Backend.LoseRemoveReply = true;
        else f.Backend.LoseReleaseReply = boundary;
        Assert.Equal("ReplyLost", (await Assert.ThrowsAsync<WorkspaceException>(() => New().CheckpointAndReleaseAsync(id, "result", ct))).Code);
        var preserved = new WorkspaceStore(f.Home).Get(id).Snapshot!.Result;
        Assert.NotNull(preserved);
        var result = await New().CheckpointAndReleaseAsync(id, "same request", ct);
        Assert.Equal("released", result.Workspace.Phase); Assert.Equal(preserved, result.Workspace.Snapshot!.Result);
        Assert.Equal(1, source.Transactions); Assert.True(f.Backend.DeltaRemoved); Assert.True(f.Backend.SnapshotReleased);
    }

    [Fact]
    public async Task CombinedCheckpointReleaseCannotReuseAPreviousInvocationsSeal()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        string id = (await f.Ensure(ct)).Workspace.Id;
        await f.New().CheckpointAsync(id, "prior result", ct);
        int seals = f.Backend.SealCalls, stops = f.App.Stops, removals = f.App.InfrastructureRemovals;
        var result = await f.New().CheckpointAndReleaseAsync(id, "same result", ct);
        Assert.Equal("released", result.Workspace.Phase);
        Assert.Equal(3, f.Backend.SealCalls - seals);
        Assert.Equal(1, f.App.Stops - stops); Assert.Equal(1, f.App.InfrastructureRemovals - removals);
    }

    [Fact]
    public async Task FailedCombinedAdmissionRetriesWithFreshSeals()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        string id = (await f.Ensure(ct)).Workspace.Id;
        var source = new CheckpointSource();
        source.AfterInspect = () => { source.AfterInspect = null; throw new WorkspaceException("ReplyLost", "source reply lost"); };
        WorkspaceCoordinator New() => new(new(f.Home), source, f.App, f.Backend);
        Assert.Equal("ReplyLost", (await Assert.ThrowsAsync<WorkspaceException>(() => New().CheckpointAndReleaseAsync(id, "result", ct))).Code);
        Assert.Equal(2, f.Backend.SealCalls); Assert.True(f.Backend.Mounted);
        var preserved = new WorkspaceStore(f.Home).Get(id).Snapshot!.Result;
        Assert.NotNull(preserved);
        var resumed = await New().CheckpointAndReleaseAsync(id, "retry", ct);
        Assert.Equal("released", resumed.Workspace.Phase); Assert.Equal(preserved, resumed.Workspace.Snapshot!.Result);
        Assert.Equal(5, f.Backend.SealCalls); Assert.Equal(1, source.Transactions);
    }

    [Fact]
    public async Task CombinedCheckpointReleaseKeepsTheSameTaskLockThroughFreshInspection()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        string id = (await f.Ensure(ct)).Workspace.Id;
        var source = new CheckpointSource();
        bool contentionVerified = false;
        source.AfterInspectAsync = async () =>
        {
            using var canceled = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new WorkspaceStore(f.Home).LockAsync(id, canceled.Token));
            contentionVerified = true;
        };
        var coordinator = new WorkspaceCoordinator(new(f.Home), source, f.App, f.Backend);
        Assert.Equal("released", (await coordinator.CheckpointAndReleaseAsync(id, "result", ct)).Workspace.Phase);
        Assert.True(contentionVerified);
    }

    [Fact]
    public async Task CombinedCheckpointReleaseRejectsCheckoutModeBeforeStoppingOrMutatingSource()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        var workspace = (await f.Ensure(ct)).Workspace;
        workspace.StorageMode = "git-checkout";
        new WorkspaceStore(f.Home).Save(workspace, "checkout-fixture");
        Assert.Equal("UnsupportedStorageMode", (await Assert.ThrowsAsync<WorkspaceException>(() =>
            f.New().CheckpointAndReleaseAsync(workspace.Id, "not supported", ct))).Code);
        Assert.Equal(0, f.App.Stops); Assert.Equal(0, f.Source.PreserveCalls); Assert.Equal(0, f.Backend.SealCalls);
        Assert.Null(new WorkspaceStore(f.Home).Get(workspace.Id).ResultRevision);
    }
}

public sealed class CheckpointReleaseContractTests
{
    [Fact]
    public async Task CombinedCheckpointReleaseOptionIsDiscoverableAndBooleanOnly()
    {
        var catalog = new ContractCatalog(); var command = catalog.Find("workspace.checkpoint")!;
        Assert.Contains("[--release]", command.CliUsage);
        foreach (bool release in new[] { false, true })
            Assert.True(catalog.Validate(command.InputSchema,
                JsonSerializer.SerializeToElement(new { id = new string('a', 24), message = "result", release })).IsValid);
        Assert.False(catalog.Validate(command.InputSchema,
            JsonSerializer.SerializeToElement(new { id = new string('a', 24), message = "result", release = "true" })).IsValid);
        using var output = new StringWriter();
        Assert.Equal(0, await Program.RunAsync(["schema", "workspace.checkpoint"], output, TestContext.Current.CancellationToken));
        Assert.Contains("\"release\"", output.ToString());
        using var capabilities = new StringWriter();
        Assert.Equal(0, await Program.RunAsync(["capabilities"], capabilities, TestContext.Current.CancellationToken));
        using var response = JsonDocument.Parse(capabilities.ToString());
        Assert.True(catalog.Validate("urn:splinter:v1:response", response.RootElement).IsValid);
    }
}
