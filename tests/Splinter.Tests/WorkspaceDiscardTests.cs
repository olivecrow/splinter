using System.Text.Json;
using System.Text.Json.Nodes;
using Splinter.Core;
using Splinter.Protocol;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed partial class SnapshotWorkspaceCoordinatorTests
{
    private static WorkspaceDiscardRequest DiscardRequest(WorkspaceRecord w) => new(w.Id, "discard-this-task", w.RequestDigest!);

    [Fact]
    public async Task DiscardPublishesResourceAbsenceBeforeSnapshotAuthority()
    {
        using var f = new Fixture { WithDiscard = true }; var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure(ct)).Workspace; int calls = 0;
        f.Discard.BeforeSnapshot = (_, _) =>
        {
            var durable = new WorkspaceStore(f.Home).Get(w.Id);
            Assert.Equal("snapshot", durable.Discard!.Step);
            Assert.Equal("discard-snapshot", durable.Snapshot!.Step);
            Assert.True(durable.Snapshot.Observation is { DeltaPresent: false, Mounted: false, ProviderAlive: false,
                OwnershipVerified: true, OpenHandles: 0, WorkingSetBytes: 0, PrivateBytes: 0 });
            Assert.True(durable.ReleasedResources is { OwnershipVerified: true, ActiveProcesses: 0, WorkingSetBytes: 0, PrivateBytes: 0 });
            calls++;
        };
        Assert.Equal("discarded", (await f.New().DiscardAsync(DiscardRequest(w), ct)).Workspace.Phase);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task FailedDirtyTaskDiscardsWithoutCheckpointWhilePeerContinues()
    {
        using var f = new Fixture { WithDiscard = true }; var ct = TestContext.Current.CancellationToken;
        var a = (await f.Ensure(ct)).Workspace;
        var peerBackend = new Backend { SnapshotId = "peer" }; var peerSource = new Source(); var peerApp = new App();
        WorkspaceCoordinator Peer() => new(new(f.Home), peerSource, peerApp, snapshots: peerBackend);
        var b = await Peer().EnsureAsync(f.Project, "peer", "base", "ntfs-snapshot", false, ct);
        peerSource.Dirty = true;
        a.Validation = new("failed", "source", false, DateTimeOffset.UtcNow, ["compile error"]);
        a.Phase = "validation-failed"; new WorkspaceStore(f.Home).Save(a, "fixture-error");
        f.Source.Dirty = true; f.Source.InspectionError = "MustNotReadSource"; f.App.Running = true; f.Backend.AcknowledgeWrite();
        var finished = await f.New().DiscardAsync(DiscardRequest(a), ct);
        Assert.Equal("discarded", finished.Workspace.Phase); Assert.Equal("complete", finished.Workspace.Discard!.Step);
        Assert.Null(finished.Workspace.ResultRevision); Assert.Null(finished.Workspace.Snapshot!.Result);
        Assert.Equal(1, f.Discard.SourceRemovals); Assert.True(f.Backend.DeltaRemoved); Assert.True(f.Backend.SnapshotReleased);
        Assert.False(f.App.Running); Assert.Equal(0, finished.Workspace.ReleasedResources!.ActiveProcesses);
        Assert.True((await Peer().StatusAsync(b.Workspace.Id, ct, verifySource: true)).Source!.HasChanges);
        Assert.True(peerBackend.Mounted); Assert.Equal(0, peerApp.Stops);
        Assert.Equal("discarded", (await f.New().DiscardAsync(DiscardRequest(a), ct)).Workspace.Phase);
        Assert.Equal("discarded", (await f.Ensure(ct)).Workspace.Phase); Assert.Equal(1, f.Backend.Captures);
        Assert.Equal(1, f.Discard.SourceRemovals); Assert.Equal(1, f.Discard.SnapshotEffects);
    }

    [Theory]
    [InlineData("stop", "stop")]
    [InlineData("unmount", "unmount")]
    [InlineData("source", "source")]
    [InlineData("delta", "delta")]
    [InlineData("snapshot", "snapshot")]
    public async Task DiscardLostResponsesResumeOnlyTheSameIntent(string boundary, string step)
    {
        using var f = new Fixture { WithDiscard = true }; var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure(ct)).Workspace; var request = DiscardRequest(w);
        if (boundary == "stop") f.App.LoseStopReply = true;
        if (boundary == "unmount") f.Backend.LoseReleaseReply = "unmount";
        if (boundary == "source") f.Discard.LoseSourceReply = true;
        if (boundary == "delta") f.Backend.LoseRemoveReply = true;
        if (boundary == "snapshot") f.Backend.LoseReleaseReply = "snapshot-release";
        Assert.Equal("ReplyLost", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().DiscardAsync(request, ct))).Code);
        var pending = new WorkspaceStore(f.Home).Get(w.Id);
        Assert.Equal(step, pending.Discard!.Step);
        Assert.Equal("resume-discard", (await f.New().StatusAsync(w.Id, ct, verifySource: true)).Recovery!.Disposition);
        Assert.Null((await f.New().StatusAsync(w.Id, ct, verifySource: true)).Source);
        Assert.Equal("DiscardRequestConflict", (await Assert.ThrowsAsync<WorkspaceException>(() =>
            f.New().DiscardAsync(request with { IntentKey = "replacement" }, ct))).Code);
        Assert.Equal("WorkspaceDiscarding", (await Assert.ThrowsAsync<WorkspaceException>(() => f.Ensure(ct))).Code);
        Func<Task>[] forbidden = [() => f.New().StartAsync(w.Id, ct),
            () => f.New().StopAsync(w.Id, ct), () => f.New().CheckpointAsync(w.Id, "no", ct),
            () => f.New().IntegrateAsync(w.Id, "base", ct), () => f.New().ReleaseAsync(w.Id, ct), () => f.New().CompactAsync(w.Id, ct)];
        foreach (var action in forbidden) Assert.Equal("WorkspaceDiscarding", (await Assert.ThrowsAsync<WorkspaceException>(action)).Code);
        Assert.Equal("discarded", (await f.New().DiscardAsync(request, ct)).Workspace.Phase);
        Assert.Equal(1, f.Backend.Captures); Assert.Equal(1, f.Discard.SourceRemovals); Assert.Equal(1, f.Discard.SnapshotEffects);
    }

    [Fact]
    public async Task ProductionWithoutDiscardCompositionCannotRecordIntentOrStopTheEditor()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure(ct)).Workspace; f.App.Running = true;
        Assert.Equal("WorkspaceDiscardUnavailable", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().DiscardAsync(DiscardRequest(w), ct))).Code);
        Assert.Null(new WorkspaceStore(f.Home).Get(w.Id).Discard); Assert.Equal(0, f.App.Stops); Assert.True(f.Backend.Mounted);
        Assert.Equal("DiscardRequestConflict", (await Assert.ThrowsAsync<WorkspaceException>(() =>
            f.New().DiscardAsync(DiscardRequest(w) with { ExpectedRequestDigest = new('f', 24) }, ct))).Code);
    }

    [Fact]
    public async Task NewWritesOrSourceRevisionChangesPreserveTheFrozenIntent()
    {
        using var f = new Fixture { WithDiscard = true }; var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure(ct)).Workspace; var request = DiscardRequest(w); f.Backend.WriteDuringUnmount = true;
        Assert.Equal("DiscardDeltaChanged", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().DiscardAsync(request, ct))).Code);
        Assert.Equal("preserve", (await f.New().StatusAsync(w.Id, ct)).Recovery!.Disposition);
        Assert.False(f.Backend.DeltaRemoved); Assert.Equal(0, f.Discard.SourceRemovals);
        Assert.Equal("requires-user-action", ApiResponse.Failure("workspace.discard", "DiscardDeltaChanged", "changed").RetryDisposition);
        using var g = new Fixture { WithDiscard = true }; var v = (await g.Ensure(ct)).Workspace;
        g.Backend.LoseReleaseReply = "unmount";
        await Assert.ThrowsAsync<WorkspaceException>(() => g.New().DiscardAsync(DiscardRequest(v), ct));
        g.Discard.Revision = "foreign-head";
        Assert.Equal("DiscardSourceChanged", (await Assert.ThrowsAsync<WorkspaceException>(() => g.New().DiscardAsync(DiscardRequest(v), ct))).Code);
        Assert.False(g.Backend.DeltaRemoved); Assert.Equal(0, g.Discard.SourceRemovals);
    }

    [Fact]
    public async Task UnknownResourcesAndLiveChildrenCannotPassTheDiscardGate()
    {
        using var f = new Fixture { WithDiscard = true }; var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure(ct)).Workspace; f.App.Children = 1;
        Assert.Equal("ProcessCleanupIncomplete", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().DiscardAsync(DiscardRequest(w), ct))).Code);
        Assert.True(f.Backend.Mounted); Assert.Equal(0, f.Discard.SourceRemovals);
        f.App.Children = 0; f.Backend.UnknownResources = true;
        Assert.Equal("OverlayCleanupIncomplete", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().DiscardAsync(DiscardRequest(w), ct))).Code);
        Assert.False(f.Backend.DeltaRemoved); Assert.Equal(0, f.Discard.SourceRemovals);
        f.Backend.UnknownResources = false;
        f.Backend.UnknownDelta = true;
        Assert.Equal("OverlayCleanupIncomplete", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().DiscardAsync(DiscardRequest(w), ct))).Code);
        Assert.Equal(0, f.Discard.SnapshotEffects); Assert.False(f.Backend.SnapshotReleased);
        f.Backend.UnknownDelta = false;
        Assert.Equal("discarded", (await f.New().DiscardAsync(DiscardRequest(w), ct)).Workspace.Phase);
    }

    [Fact]
    public async Task SharedSnapshotBlocksDiscardBeforeStoppingEitherTask()
    {
        using var f = new Fixture { WithDiscard = true }; var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure(ct)).Workspace;
        var peerBackend = new Backend(); var peerApp = new App();
        var peer = new WorkspaceCoordinator(new(f.Home), new Source(), peerApp, snapshots: peerBackend);
        await peer.EnsureAsync(f.Project, "dependent", "base", "ntfs-snapshot", false, ct);
        peerApp.Running = true;
        Assert.Equal("SnapshotInUse", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().DiscardAsync(DiscardRequest(w), ct))).Code);
        Assert.Null(new WorkspaceStore(f.Home).Get(w.Id).Discard); Assert.Equal(0, f.App.Stops); Assert.Equal(0, peerApp.Stops);
        Assert.True(peerBackend.Mounted); Assert.False(f.Backend.DeltaRemoved);
    }

    [Fact]
    public async Task PublishedCandidateSourceAndCacheBlockDiscardBeforeAnyStop()
    {
        using var f = new Fixture { WithDiscard = true }; var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure(ct)).Workspace; string project = WorkspaceStore.ProjectKey(f.Project);
        var world = new ValidationWorkflowWorld(); var owner = new ValidationOwner("discard-worker", 1);
        var worker = world.SetWorker(w.Id, owner, w.Snapshot!.Reference!, "candidate-data");
        var viewOwner = new ValidationOwner("discard-validator", 1); world.Owners["discard-view"] = viewOwner;
        var workflow = new ValidationWorkflowCoordinator(new(new WorkspaceStore(f.Home)),
            new ValidationWorkflowFake(Path.Combine(f.Home, "discard-validation"), world));
        var candidate = (await workflow.ExecuteAsync(new(1, "publish", project, "publish-before-discard", owner) { Worker = worker }, ct)).Candidate!;
        await workflow.ExecuteAsync(new(1, "select", project, "select-before-discard", viewOwner)
        { CandidateId = candidate.Intent.CandidateId, Cache = new("discard-view", "discard-cache", w.Snapshot.Reference!, "fake", "source", 4096) }, ct);
        var failure = await Assert.ThrowsAsync<WorkspaceException>(() => f.New().DiscardAsync(DiscardRequest(w), ct));
        Assert.Equal("SnapshotInUse", failure.Code);
        var dependencies = JsonSerializer.SerializeToElement(failure.Details).GetProperty("dependencies").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Contains(dependencies, x => x!.StartsWith("candidate:"));
        Assert.Contains(dependencies, x => x!.StartsWith("source-generation:")); Assert.Contains("cache:discard-view", dependencies);
        Assert.Contains(failure.RecoveryActions!, x => x.Command == "capabilities" && x.RequiresUserAction);
        Assert.Null(new WorkspaceStore(f.Home).Get(w.Id).Discard); Assert.Equal(0, f.App.Stops); Assert.True(f.Backend.Mounted);
    }

    [Fact]
    public async Task DeferredDiscardKeepsReceiptAndRetriesOnlySnapshotReclamation()
    {
        using var f = new Fixture { WithDiscard = true }; var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure(ct)).Workspace; f.Backend.Blockers = () => ["older-snapshot"];
        var pending = await f.New().DiscardAsync(DiscardRequest(w), ct);
        Assert.Equal("discard-reclaim-pending", pending.Workspace.Phase);
        Assert.Equal("deferred-discard-reclamation", pending.Recovery!.Disposition);
        Assert.True(f.Backend.DeltaRemoved); Assert.False(f.Backend.SnapshotReleased);
        Assert.Null(pending.Workspace.Snapshot!.Result); Assert.Equal(1, f.Discard.SourceRemovals);
        f.Backend.Blockers = () => [];
        Assert.Equal("discarded", (await f.New().DiscardAsync(DiscardRequest(w), ct)).Workspace.Phase);
        Assert.Equal(1, f.Discard.SourceRemovals); Assert.Equal(1, f.Backend.Captures);
    }

    [Fact]
    public async Task DiscardSchemaAndDurableIdentityRejectMissingOrChangedScope()
    {
        var catalog = new ContractCatalog(); using var f = new Fixture { WithDiscard = true }; var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure(ct)).Workspace; var request = DiscardRequest(w);
        Assert.True(catalog.Validate("urn:splinter:v1:workspace-discard-request", JsonSerializer.SerializeToElement(request, ApiJson.Options)).IsValid);
        Assert.False(catalog.Validate("urn:splinter:v1:workspace-discard-request", JsonSerializer.SerializeToElement(request with { IntentKey = " " }, ApiJson.Options)).IsValid);
        f.Backend.LoseReleaseReply = "unmount";
        await Assert.ThrowsAsync<WorkspaceException>(() => f.New().DiscardAsync(request, ct));
        var store = new WorkspaceStore(f.Home); var pending = store.Get(w.Id);
        var state = JsonSerializer.SerializeToNode(pending.Discard, ApiJson.Options)!;
        Assert.True(catalog.Validate("urn:splinter:v1:workspace-discard-state", JsonSerializer.SerializeToElement(state)).IsValid);
        var response = ApiResponse.Success("workspace.status", new
        {
            workspace = new { pending.Version, pending.SourceKind, pending.ApplicationKind, pending.Id, pending.RequestKey, pending.ProjectPath, pending.Path, pending.SourceReference,
                pending.BaseReference, pending.Phase, pending.CreatedAt, pending.UpdatedAt, pending.StorageMode, pending.RequestDigest, pending.Snapshot, pending.Discard },
            controlPath = store.ControlPath(w.Id), nextCommand = "workspace.discard", isolation = "private", recovery = new WorkspaceView(pending, null, null, "control", "workspace discard").Recovery
        });
        Assert.True(catalog.Validate("urn:splinter:v1:response", JsonSerializer.SerializeToElement(response, ApiJson.Options)).IsValid);
        state.AsObject().Remove("seal");
        Assert.False(catalog.Validate("urn:splinter:v1:workspace-discard-state", JsonSerializer.SerializeToElement(state)).IsValid);
        string file = Path.Combine(store.ControlPath(w.Id), "workspace.json");
        var node = JsonNode.Parse(File.ReadAllText(file))!;
        node["discard"]!["intent"]!["sourceReference"] = "foreign";
        File.WriteAllText(file, node.ToJsonString());
        Assert.Equal("DiscardIntentMismatch", Assert.Throws<WorkspaceException>(() => store.Get(w.Id)).Code);
        node["discard"]!["intent"]!["sourceReference"] = w.SourceReference;
        node["discard"]!["legacy"] = true;
        File.WriteAllText(file, node.ToJsonString());
        Assert.Equal("StateCorrupt", Assert.Throws<WorkspaceException>(() => store.Get(w.Id)).Code);
        node["discard"]!.AsObject().Remove("legacy");
        node["discard"]!["intent"]!.AsObject().Remove("sourceReference");
        File.WriteAllText(file, node.ToJsonString());
        Assert.Equal("StateCorrupt", Assert.Throws<WorkspaceException>(() => store.Get(w.Id)).Code);
        Assert.False(f.Backend.DeltaRemoved);
    }

    private sealed class DiscardProvider(Backend backend) : IWorkspaceDiscardProvider
    {
        public string Revision = "base";
        public bool LoseSourceReply;
        public int SourceRemovals, SnapshotEffects;
        public Action<WorkspaceRecord, string>? BeforeSnapshot;
        private bool sourceRemoved;
        private readonly string attemptId = Guid.NewGuid().ToString("N");
        public Task<string> ObserveSourceRevisionAsync(WorkspaceRecord w, string control, CancellationToken ct) => Task.FromResult(Revision);
        public Task DiscardSourceAsync(WorkspaceRecord w, string control, CancellationToken ct)
        {
            w.Discard!.RequireMatch(w);
            if (Revision != w.Discard.Intent.SourceRevision) throw new WorkspaceException("DiscardSourceChanged", "Foreign source revision.");
            if (!sourceRemoved) { SourceRemovals++; sourceRemoved = true; }
            if (LoseSourceReply) { LoseSourceReply = false; throw new WorkspaceException("ReplyLost", "after source removal"); }
            return Task.CompletedTask;
        }
        public async Task DiscardDeltaAsync(WorkspaceRecord w, string control, CancellationToken ct)
        {
            w.Discard!.RequireMatch(w);
            if (!sourceRemoved) throw new InvalidOperationException("Discard source first.");
            if (!backend.DeltaRemoved && await backend.SealAsync(w, control, ct) != w.Discard.Seal)
                throw new WorkspaceException("DiscardDeltaChanged", "Changed journal.");
            if (!backend.DeltaRemoved) await backend.RemoveDeltaAsync(w, control, ct);
        }
        public async Task<SnapshotReclamation> DiscardSnapshotAsync(WorkspaceRecord w, string control, CancellationToken ct)
        {
            w.Discard!.RequireMatch(w);
            if (!sourceRemoved || !backend.DeltaRemoved) throw new InvalidOperationException("Discard source and delta first.");
            BeforeSnapshot?.Invoke(w, control);
            if (!backend.SnapshotReleased && backend.Blockers().Length == 0) SnapshotEffects++;
            return (await backend.ReleaseSnapshotAsync(w, control, ct)) with { AttemptId = attemptId };
        }
    }
}
