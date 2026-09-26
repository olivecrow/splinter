using System.Diagnostics;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class SnapshotWorkspaceCoordinatorTests
{
    [Fact]
    public async Task CompactionResumesSameWorkspaceWhilePeerRemainsUsable()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        var peerBackend = new Backend { SnapshotId = "peer" }; var peerSource = new Source(); var peerApp = new App();
        WorkspaceCoordinator Peer() => new(new(f.Home), peerSource, peerApp, snapshots: peerBackend);
        string a = (await Peer().EnsureAsync(f.Project, "peer", "base", "ntfs-snapshot", false, ct)).Workspace.Id;
        string b = (await f.Ensure(ct)).Workspace.Id;
        f.Backend.LoseMountReply = true;
        Assert.Equal("ReplyLost", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().CompactAsync(b, ct))).Code);
        Assert.Equal("compact-connect", new WorkspaceStore(f.Home).Get(b).Snapshot!.Step);
        Assert.Equal("resume-compaction", (await f.New().StatusAsync(b, ct)).Recovery!.Disposition);
        Assert.Equal("WorkspaceCompacting", (await Assert.ThrowsAsync<WorkspaceException>(() => f.Ensure(ct))).Code);
        peerSource.Dirty = true;
        Assert.True((await Peer().StatusAsync(a, ct, verifySource: true)).Source!.HasChanges);
        Assert.True(peerBackend.Mounted);
        var resumed = await f.New().CompactAsync(b, ct, 1);
        Assert.Equal("ready", resumed.Workspace.Snapshot!.Step);
        Assert.NotNull(resumed.Workspace.Snapshot.Compaction);
        Assert.Contains(resumed.Workspace.Snapshot.Compaction.Cost!.Stages, stage => stage.Stage == "provider-reconnect");
        Assert.Contains(resumed.Workspace.Snapshot.Compaction.Cost!.Stages, stage => stage.Stage == "previous-generation-cleanup");
        Assert.Equal(1, f.Backend.CompactCalls); Assert.True(f.Backend.CompletedCompaction);
        Assert.Equal([null], f.Backend.SeenOverlapBudgets);
        Assert.Equal(1, f.Backend.Captures); Assert.True(peerBackend.Mounted);
        await f.New().CompactAsync(b, ct);
        Assert.Equal(1, f.Backend.CompactCalls);
    }

    [Fact]
    public async Task CompactionRetryKeepsBudgetUntilCallerExplicitlyRaisesIt()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        string id = (await f.Ensure(ct)).Workspace.Id;
        f.Backend.MinimumOverlap = 200;
        Assert.Equal("CompactionOverlapBudgetExceeded", (await Assert.ThrowsAsync<WorkspaceException>(() =>
            f.New().CompactAsync(id, ct, 120))).Code);
        var pending = new WorkspaceStore(f.Home).Get(id).Snapshot!;
        Assert.Equal("compact-write", pending.Step);
        Assert.Equal(120, pending.PendingCompactionMaxEstimatedJournalOverlapBytes);
        Assert.Equal("resume-compaction", (await f.New().StatusAsync(id, ct)).Recovery!.Disposition);
        Assert.Equal("CompactionOverlapBudgetExceeded", (await Assert.ThrowsAsync<WorkspaceException>(() =>
            f.New().CompactAsync(id, ct))).Code);
        Assert.Equal([120L, 120L], f.Backend.SeenOverlapBudgets);

        var completed = await f.New().CompactAsync(id, ct, 200);
        Assert.Equal("ready", completed.Workspace.Snapshot!.Step);
        Assert.Null(completed.Workspace.Snapshot.PendingCompactionMaxEstimatedJournalOverlapBytes);
        Assert.Equal([120L, 120L, 200L], f.Backend.SeenOverlapBudgets);
    }

    [Fact]
    public async Task NewerResultWaitsWithoutReleasingOlderWorkspaceAndFreshRetryReclaimsOnlyItsOwnSnapshot()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        var older = new Backend { SnapshotId = "older" }; var olderSource = new Source(); var olderApp = new App();
        WorkspaceCoordinator Older() => new(new(f.Home), olderSource, olderApp, snapshots: older);
        string a = (await Older().EnsureAsync(f.Project, "older", "base", "ntfs-snapshot", false, ct)).Workspace.Id;
        string b = (await f.Ensure(ct)).Workspace.Id;
        f.Backend.Blockers = () => older.Available == true ? ["older"] : [];
        var pending = await f.New().ReleaseAsync(b, ct);
        Assert.Equal("reclaim-pending", pending.Workspace.Phase);
        Assert.Equal("release-snapshot", pending.Workspace.Snapshot!.Step);
        Assert.True(pending.Workspace.Snapshot.Result!.IndependentOfSnapshot);
        Assert.Equal("deferred-reclamation", pending.Recovery!.Disposition);
        Assert.True(f.Backend.DeltaRemoved); Assert.False(f.Backend.Mounted); Assert.False(f.Backend.SnapshotReleased);
        Assert.True(older.Mounted); Assert.True(older.Available); Assert.False(older.DeltaRemoved);
        Assert.Equal("source-volume-shared", pending.Workspace.Snapshot.Reclamation!.SharedStorage!.Scope);
        var saved = new WorkspaceStore(f.Home).Get(b);
        var attempt = saved.Snapshot!.Reclamation!.AttemptId;
        f.Source.InspectionError = "SourceMustNotBeRead";
        Assert.Equal("deferred-reclamation", (await f.New().StatusAsync(b, ct, verifySource: true)).Recovery!.Disposition);
        Assert.Equal(1, f.Backend.ReleaseAttempts);
        Assert.Equal(attempt, new WorkspaceStore(f.Home).List().Single(w => w.Id == b).Snapshot!.Reclamation!.AttemptId);
        Assert.Equal("WorkspaceReleasing", (await Assert.ThrowsAsync<WorkspaceException>(() => f.Ensure(ct))).Code);
        olderSource.Dirty = true;
        Assert.True((await Older().StatusAsync(a, ct, verifySource: true)).Source!.HasChanges);
        olderSource.Dirty = false;
        Assert.Equal("released", (await Older().ReleaseAsync(a, ct)).Workspace.Phase);
        Assert.Equal("reclaim-pending", new WorkspaceStore(f.Home).Get(b).Phase);
        var released = await f.New().ReleaseAsync(b, ct);
        Assert.Equal("released", released.Workspace.Phase);
        Assert.Equal("released", released.Workspace.Snapshot!.Reclamation!.Disposition);
        Assert.Equal(saved.Snapshot.Result, released.Workspace.Snapshot.Result);
        Assert.Equal(2, f.Backend.ReleaseAttempts); Assert.Equal(1, f.Backend.Captures);
        Assert.Equal("released", (await f.New().ReleaseAsync(b, ct)).Workspace.Phase);
        Assert.Equal(2, f.Backend.ReleaseAttempts);
    }

    [Fact]
    public async Task DeferredRetryStillRequiresKnownResourcesAndValidPreservedResult()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        string id = (await f.Ensure(ct)).Workspace.Id;
        f.Backend.Blockers = () => ["older"];
        await f.New().ReleaseAsync(id, ct);
        f.Backend.UnknownResources = true;
        Assert.Equal("OverlayCleanupIncomplete", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ReleaseAsync(id, ct))).Code);
        Assert.Equal(1, f.Backend.ReleaseAttempts);
        f.Backend.UnknownResources = false; f.Source.InvalidResult = true;
        Assert.Equal("ResultMissing", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ReleaseAsync(id, ct))).Code);
        Assert.Equal(1, f.Backend.ReleaseAttempts); Assert.False(f.Backend.SnapshotReleased);
        f.Source.InvalidResult = false;
        Assert.Equal("reclaim-pending", (await f.New().ReleaseAsync(id, ct)).Workspace.Phase);
        Assert.Equal(2, f.Backend.ReleaseAttempts);
    }
    [Fact]
    public async Task SourceCacheFailureOnEnsureReturnsExactAuditWithoutRecapturing()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        var ready = await f.Ensure(ct);
        f.Source.InspectionError = "SourceCacheRevalidationRequired";
        var failure = await Assert.ThrowsAsync<WorkspaceException>(() => f.Ensure(ct));
        var action = Assert.Single(failure.RecoveryActions!);
        Assert.Equal("workspace.status", action.Command);
        var arguments = System.Text.Json.JsonSerializer.SerializeToElement(action.Arguments);
        Assert.Equal(ready.Workspace.Id, arguments.GetProperty("id").GetString());
        Assert.Equal(f.Home, arguments.GetProperty("home").GetString());
        Assert.True(arguments.GetProperty("auditSource").GetBoolean());
        Assert.Equal(1, f.Backend.Captures); Assert.True(f.Backend.Mounted);
    }
    [Fact]
    public async Task VerificationGatePreservesCaptureAndReturnsExactEnsureRetry()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        f.Backend.VerificationRequired = true;
        var failure = await Assert.ThrowsAsync<WorkspaceException>(() => f.Ensure(ct));
        Assert.Equal("SnapshotPrivilegeRequired", failure.Code);
        Assert.Contains(failure.RecoveryActions!, action => action.Command == "workspace.ensure");
        Assert.False(f.Backend.Mounted); Assert.Equal(1, f.Backend.Captures);
        f.Backend.VerificationRequired = false;
        var ready = await Task.WhenAll(f.Ensure(ct), f.Ensure(ct));
        Assert.Equal(ready[0].Workspace.Id, ready[1].Workspace.Id);
        Assert.Equal(1, f.Backend.Captures); Assert.Equal(1, f.Source.Registers);
        f.Backend.VerificationRequired = true;
        var start = await Assert.ThrowsAsync<WorkspaceException>(() => f.New().StartAsync(ready[0].Workspace.Id, ct));
        Assert.Contains(start.RecoveryActions!, action => action.Command == "unity.start");
        Assert.Equal(0, f.App.Starts);
    }
    [Fact]
    public async Task ConcurrentRequestsAndFreshCoordinatorReuseExactlyOneIdentity()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        var views = await Task.WhenAll(f.Ensure(ct), f.Ensure(ct));
        Assert.Equal(views[0].Workspace.Id, views[1].Workspace.Id);
        Assert.Equal(1, f.Backend.Captures); Assert.Equal(1, f.Source.Registers);
        Assert.Equal("ready", views[1].Workspace.Phase);
        var conflict = await Assert.ThrowsAsync<WorkspaceException>(() => f.New().EnsureAsync(f.Project, "task", "changed", "ntfs-snapshot", false, ct));
        Assert.Equal("RequestKeyConflict", conflict.Code);
    }

    [Fact]
    public async Task CaptureResponseLossReconcilesReceiptWithoutRecapture()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        f.Backend.LoseCaptureReply = true;
        await Assert.ThrowsAsync<WorkspaceException>(() => f.Ensure(ct));
        var resumed = await f.Ensure(ct);
        Assert.Equal(1, f.Backend.Captures); Assert.Equal("ready", resumed.Workspace.Phase);
    }

    [Fact]
    public async Task MissingSnapshotNeverRecapturesOrStartsApplication()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        var view = await f.Ensure(ct); f.Backend.Available = false;
        Assert.Equal("SnapshotMissing", (await Assert.ThrowsAsync<WorkspaceException>(() => f.Ensure(ct))).Code);
        var status = await f.New().StatusAsync(view.Workspace.Id, ct);
        Assert.Null(status.Source); Assert.False(status.Workspace.Snapshot!.Observation!.SnapshotAvailable);
        Assert.Equal(1, f.Backend.Captures); Assert.Equal(0, f.App.Starts);
        Assert.Equal("SnapshotMissing", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ReleaseAsync(view.Workspace.Id, ct))).Code);
        Assert.False(f.Backend.DeltaRemoved);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingBaseReleasesOnlyAnUnchangedIndependentResult(bool lateWrite)
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        string id = (await f.Ensure(ct)).Workspace.Id;
        await f.New().StartAsync(id, ct); await f.New().RefreshAsync(id, ct); await f.New().StatusAsync(id, ct);
        await f.New().CheckpointAsync(id, "independent result", ct);
        f.Backend.Available = false; f.Source.LoseReply = "inspect"; f.Backend.WriteDuringUnmount = lateWrite;
        if (lateWrite)
        {
            Assert.Equal("DeltaChangedAfterCollection", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ReleaseAsync(id, ct))).Code);
            Assert.False(f.Backend.DeltaRemoved); Assert.NotNull(new WorkspaceStore(f.Home).Get(id).Snapshot!.Result);
        }
        else Assert.Equal("released", (await f.New().ReleaseAsync(id, ct)).Workspace.Phase);
        Assert.Equal("inspect", f.Source.LoseReply); Assert.Equal(1, f.Backend.Captures);
    }

    [Fact]
    public async Task UnknownSnapshotPreservesIdentityAndDeltaUntilObservationRecovers()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        var first = await f.Ensure(ct);
        f.Backend.Available = null;
        var status = await f.New().StatusAsync(first.Workspace.Id, ct);
        Assert.Null(status.Source);
        Assert.Equal("unknown", status.Workspace.Snapshot!.Observation!.SnapshotAvailability);
        Assert.Equal(new SnapshotRecoveryDecision("observe", "SnapshotObservationUnknown", "workspace.status"), status.Recovery);
        foreach (var operation in new Func<Task<WorkspaceView>>[] { () => f.Ensure(ct), () => f.New().ReleaseAsync(first.Workspace.Id, ct) })
        {
            var failure = await Assert.ThrowsAsync<WorkspaceException>(operation);
            Assert.Equal("SnapshotObservationUnknown", failure.Code);
            Assert.Equal("workspace.status", Assert.Single(failure.RecoveryActions!).Command);
        }
        Assert.False(f.Backend.DeltaRemoved); Assert.False(f.Backend.SnapshotReleased);
        f.Backend.Available = true;
        var resumed = await f.Ensure(ct);
        Assert.Equal(first.Workspace.Snapshot!.Reference, resumed.Workspace.Snapshot!.Reference);
        Assert.Equal(1, f.Backend.Captures); Assert.Equal("ready", resumed.Recovery!.Disposition);
    }

    [Fact]
    public async Task FinalSnapshotReleaseCanRefreshUnknownEvidenceButStillRequiresVerifiedAbsence()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        string id = (await f.Ensure(ct)).Workspace.Id;
        f.Backend.LoseReleaseReply = "snapshot-release";
        Assert.Equal("ReplyLost", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ReleaseAsync(id, ct))).Code);
        Assert.True(f.Backend.DeltaRemoved); Assert.True(f.Backend.SnapshotReleased);
        f.Backend.Available = null;
        Assert.Equal("resume-release", (await f.New().StatusAsync(id, ct)).Recovery!.Disposition);
        f.Backend.KeepReleaseUnknown = true;
        Assert.Equal("SnapshotReleaseIncomplete", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ReleaseAsync(id, ct))).Code);
        Assert.Equal("releasing", new WorkspaceStore(f.Home).Get(id).Phase);
        f.Backend.KeepReleaseUnknown = false;
        Assert.Equal("released", (await f.New().ReleaseAsync(id, ct)).Workspace.Phase);
        Assert.Equal(1, f.Backend.Captures);
    }

    [Fact]
    public async Task ProviderReconnectPreservesBasisButRefusesLiveApplication()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        var view = await f.Ensure(ct); f.Backend.Mounted = false; f.App.Running = true;
        Assert.Equal("OverlayClientsRemain", (await Assert.ThrowsAsync<WorkspaceException>(() => f.Ensure(ct))).Code);
        f.App.Running = false; var resumed = await f.Ensure(ct);
        Assert.Equal(view.Workspace.Snapshot!.Reference, resumed.Workspace.Snapshot!.Reference);
        Assert.True(f.Backend.Mounted); Assert.Equal(1, f.Backend.Captures);
    }

    [Fact]
    public async Task IndependentCheckpointCanReleaseWithoutMergingAndReleasedKeyStaysReleased()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        var view = await f.Ensure(ct); string id = view.Workspace.Id;
        await f.New().StartAsync(id, ct); await f.New().RefreshAsync(id, ct); await f.New().StatusAsync(id, ct);
        f.Source.Dirty = true;
        // A change after validation must be rejected before source collection.
        f.Source.Fingerprint = "changed";
        Assert.Equal("ValidationRequired", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().CheckpointAsync(id, "result", ct))).Code);
        await f.New().RefreshAsync(id, ct); await f.New().StatusAsync(id, ct);
        var collected = await f.New().CheckpointAsync(id, "result", ct);
        Assert.True(collected.Workspace.Snapshot!.Result!.IndependentOfSnapshot);
        var released = await f.New().ReleaseAsync(id, ct);
        Assert.Equal("released", released.Workspace.Phase); Assert.Null(released.Workspace.IntegratedRevision);
        Assert.True(f.Backend.DeltaRemoved); Assert.True(f.Backend.SnapshotReleased);
        Assert.Equal("released", (await f.Ensure(ct)).Workspace.Phase); Assert.Equal(1, f.Backend.Captures);
    }

    [Fact]
    public async Task UncollectedSourceAndSurvivingChildrenPreventDeletion()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        string id = (await f.Ensure(ct)).Workspace.Id; f.Source.Dirty = true;
        Assert.Equal("UncollectedChanges", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ReleaseAsync(id, ct))).Code);
        f.Source.Dirty = false; f.App.Children = 1;
        Assert.Equal("ProcessCleanupIncomplete", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ReleaseAsync(id, ct))).Code);
        Assert.False(f.Backend.DeltaRemoved); Assert.False(f.Backend.SnapshotReleased);
    }

    [Fact]
    public async Task LateWriteAfterCollectionKeepsDeltaAndResult()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        string id = (await f.Ensure(ct)).Workspace.Id; f.Backend.WriteDuringUnmount = true;
        Assert.Equal("DeltaChangedAfterCollection", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ReleaseAsync(id, ct))).Code);
        Assert.False(f.Backend.DeltaRemoved); Assert.False(f.Backend.SnapshotReleased);
        Assert.NotNull(new WorkspaceStore(f.Home).Get(id).Snapshot!.Result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WriteBetweenSourceInspectionAndSealCannotBecomeAnIndependentResult(bool checkpoint)
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        string id = (await f.Ensure(ct)).Workspace.Id;
        if (checkpoint)
        {
            await f.New().StartAsync(id, ct); await f.New().RefreshAsync(id, ct); await f.New().StatusAsync(id, ct);
        }
        f.Source.AfterPreserve = f.Backend.AcknowledgeWrite;
        var failure = await Assert.ThrowsAsync<WorkspaceException>(() => checkpoint
            ? f.New().CheckpointAsync(id, "result", ct) : f.New().ReleaseAsync(id, ct));
        Assert.Equal("DeltaChangedAfterCollection", failure.Code);
        Assert.False(f.Backend.DeltaRemoved); Assert.False(f.Backend.SnapshotReleased);
        Assert.Null(new WorkspaceStore(f.Home).Get(id).Snapshot!.Result);
        Assert.True(f.Backend.Mounted);
        Assert.Equal("preserve", (await f.New().StatusAsync(id, ct)).Recovery!.Disposition);
        f.Source.AfterPreserve = null;
        if (checkpoint)
            Assert.Equal(SnapshotResult.StableJournalProtocol, (await f.New().CheckpointAsync(id, "retry", ct)).Workspace.Snapshot!.Result!.CollectionProtocol);
        else Assert.Equal("released", (await f.New().ReleaseAsync(id, ct)).Workspace.Phase);
    }

    [Fact]
    public async Task InfrastructureWritesFinishBeforeTheStableCollectionInterval()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        string id = (await f.Ensure(ct)).Workspace.Id;
        f.App.AfterRemoveInfrastructure = f.Backend.AcknowledgeWrite;
        var released = await f.New().ReleaseAsync(id, ct);
        Assert.Equal("released", released.Workspace.Phase);
        Assert.Equal(2, released.Workspace.Snapshot!.Result!.Seal.DurableSequence);
    }

    [Theory]
    [InlineData(true, "ready")]
    [InlineData(false, "ready")]
    [InlineData(true, "release-unmount")]
    [InlineData(true, "release-delta")]
    public async Task LegacyCollectionMustBeRevalidatedBeforeAnyReclamation(bool available, string step)
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        string id = (await f.Ensure(ct)).Workspace.Id;
        await f.New().StartAsync(id, ct); await f.New().RefreshAsync(id, ct); await f.New().StatusAsync(id, ct);
        var collected = await f.New().CheckpointAsync(id, "result", ct);
        var state = collected.Workspace.Snapshot!;
        state.Result = state.Result! with { CollectionProtocol = null };
        state.Step = step;
        new WorkspaceStore(f.Home).Save(collected.Workspace, "legacy-fixture");
        f.Backend.Available = available;
        if (available && step == "ready")
        {
            var revalidated = await f.New().CheckpointAsync(id, "legacy retry", ct);
            Assert.Equal(SnapshotResult.StableJournalProtocol, revalidated.Workspace.Snapshot!.Result!.CollectionProtocol);
            Assert.Equal("released", (await f.New().ReleaseAsync(id, ct)).Workspace.Phase);
        }
        else
        {
            Assert.Equal("SnapshotResultRevalidationRequired", (await f.New().StatusAsync(id, ct)).Recovery!.ReasonCode);
            var failure = await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ReleaseAsync(id, ct));
            Assert.Equal("SnapshotResultRevalidationRequired", failure.Code);
            Assert.Equal("requires-user-action", Splinter.Protocol.ApiResponse.Failure("workspace.release", failure.Code, failure.Message).RetryDisposition);
            Assert.False(f.Backend.DeltaRemoved); Assert.False(f.Backend.SnapshotReleased); Assert.True(f.Backend.Mounted);
        }
    }

    [Fact]
    public async Task FailedRecollectionRetainsThePreviouslySavedManifest()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        string id = (await f.Ensure(ct)).Workspace.Id;
        await f.New().StartAsync(id, ct); await f.New().RefreshAsync(id, ct); await f.New().StatusAsync(id, ct);
        var previous = (await f.New().CheckpointAsync(id, "result", ct)).Workspace.Snapshot!.Result;
        f.Source.AfterPreserve = f.Backend.AcknowledgeWrite;
        Assert.Equal("DeltaChangedAfterCollection", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ReleaseAsync(id, ct))).Code);
        Assert.Equal(previous, new WorkspaceStore(f.Home).Get(id).Snapshot!.Result);
        Assert.False(f.Backend.DeltaRemoved); Assert.False(f.Backend.SnapshotReleased);
    }

    [Fact]
    public async Task PartialDeltaRemovalResumesAndMissingResourceEvidenceNeverMeansZero()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        string id = (await f.Ensure(ct)).Workspace.Id; f.Backend.UnknownResources = true;
        Assert.Equal("OverlayCleanupIncomplete", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ReleaseAsync(id, ct))).Code);
        Assert.False(f.Backend.DeltaRemoved); f.Backend.UnknownResources = false; f.Backend.LoseRemoveReply = true;
        await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ReleaseAsync(id, ct));
        Assert.True(f.Backend.DeltaRemoved); Assert.False(f.Backend.SnapshotReleased);
        Assert.Equal("released", (await f.New().ReleaseAsync(id, ct)).Workspace.Phase);
    }

    [Theory]
    [InlineData("register")]
    [InlineData("mount")]
    [InlineData("attach")]
    public async Task PreparationResponseLossResumesTheOriginalCapture(string boundary)
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        f.Source.LoseReply = boundary; f.Backend.LoseMountReply = boundary == "mount";
        await Assert.ThrowsAsync<WorkspaceException>(() => f.Ensure(ct));
        var resumed = await f.Ensure(ct);
        Assert.True(resumed.Workspace.Snapshot!.Attached);
        Assert.Equal(1, f.Backend.Captures); Assert.Equal(1, f.Source.Registers);
    }

    [Fact]
    public async Task CheckpointPreservationResponseLossRepairsTheIndependentManifest()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        string id = (await f.Ensure(ct)).Workspace.Id;
        await f.New().StartAsync(id, ct); await f.New().RefreshAsync(id, ct); await f.New().StatusAsync(id, ct);
        f.Source.LoseReply = "preserve";
        await Assert.ThrowsAsync<WorkspaceException>(() => f.New().CheckpointAsync(id, "result", ct));
        var recovered = await f.New().CheckpointAsync(id, "result", ct);
        Assert.Equal("collected", recovered.Workspace.Phase);
        Assert.True(recovered.Workspace.Snapshot!.Result!.IndependentOfSnapshot);
    }

    [Theory]
    [InlineData("unmount")]
    [InlineData("unregister")]
    [InlineData("snapshot-release")]
    public async Task ReleaseResponseLossResumesWithoutRecreatingTheWorkspace(string boundary)
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        string id = (await f.Ensure(ct)).Workspace.Id;
        f.Source.LoseReply = boundary; f.Backend.LoseReleaseReply = boundary;
        await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ReleaseAsync(id, ct));
        Assert.Equal("released", (await f.New().ReleaseAsync(id, ct)).Workspace.Phase);
        Assert.Equal(1, f.Backend.Captures); Assert.True(f.Backend.DeltaRemoved);
    }

    [Fact]
    public async Task ARemainingSnapshotDependentPreventsSnapshotDeletion()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        var first = await f.Ensure(ct); var store = new WorkspaceStore(f.Home);
        string otherId = WorkspaceStore.Key("other-workspace");
        var other = new WorkspaceRecord { Id = otherId, RequestKey = "other", ProjectPath = f.Project, Path = store.WorkPath(otherId),
            BaseReference = "base", SourceReference = "other", Phase = "ready", StorageMode = "ntfs-snapshot",
            Snapshot = new() { Reference = first.Workspace.Snapshot!.Reference, Attached = true, Step = "ready" } };
        store.Save(other, "fixture-dependency");
        Assert.Equal("SnapshotInUse", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ReleaseAsync(first.Workspace.Id, ct))).Code);
        Assert.False(f.Backend.SnapshotReleased);
        other.Phase = "released"; store.Save(other, "fixture-dependent-finished");
        Assert.Equal("released", (await f.New().ReleaseAsync(first.Workspace.Id, ct)).Workspace.Phase);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly TestDirectory directory = new();
        public string Home => Path.Combine(directory.Path, "home");
        public string Project => Path.Combine(directory.Path, "source");
        public Backend Backend { get; } = new();
        public Source Source { get; } = new();
        public App App { get; } = new();
        public Fixture() { Directory.CreateDirectory(Project); }
        public WorkspaceCoordinator New() => new(new(Home), Source, App, snapshots: Backend);
        public Task<WorkspaceView> Ensure(CancellationToken ct) => New().EnsureAsync(Project, "task", "base", "ntfs-snapshot", false, ct);
        public void Dispose() => directory.Dispose();
    }
    private sealed class Backend : ISnapshotWorkspaceBackend
    {
        public string SnapshotId = "opaque";
        public Func<string[]> Blockers = () => [];
        public int ReleaseAttempts;
        public bool VerificationRequired;
        public Task VerifyConnectionAsync(WorkspaceRecord w, string c, CancellationToken ct = default)
        {
            if (VerificationRequired) throw new WorkspaceException("SnapshotPrivilegeRequired", "Observe the exact snapshot first.",
                recoveryActions: [new("pwsh", new { action = "Observe" }, true)]);
            return Task.CompletedTask;
        }
        public int Captures; public bool LoseCaptureReply, LoseMountReply, Mounted, DeltaRemoved, SnapshotReleased, WriteDuringUnmount, UnknownResources, LoseRemoveReply, KeepReleaseUnknown;
        public int CompactCalls; public bool Compacted, CompletedCompaction;
        public long? MinimumOverlap;
        public List<long?> SeenOverlapBudgets = [];
        public bool? Available = true;
        public string? LoseReleaseReply;
        private void FailOnce(string boundary) { if (LoseReleaseReply == boundary) { LoseReleaseReply = null; throw new WorkspaceException("ReplyLost", boundary); } }
        private SnapshotReference? receipt;
        private long sequence = 1;
        public void AcknowledgeWrite() => sequence++;
        public Task<SnapshotReference> CaptureAsync(WorkspaceRecord w, string c, CancellationToken ct = default)
        {
            if (receipt is null) { Captures++; receipt = new(SnapshotId, "fake", w.ProjectPath, "immutable", DateTimeOffset.UtcNow, true, true, "fake"); }
            if (LoseCaptureReply) { LoseCaptureReply = false; throw new WorkspaceException("ReplyLost", "after capture"); }
            return Task.FromResult(receipt);
        }
        public Task<OverlayMount> ConnectAsync(WorkspaceRecord w, string c, CancellationToken ct = default)
        { Mounted = true; Directory.CreateDirectory(w.Path); if (LoseMountReply) { LoseMountReply = false; throw new WorkspaceException("ReplyLost", "after mount"); }
            return Task.FromResult(new OverlayMount(w.Id, w.Path, "delta", w.Snapshot!.Reference!, 123, 1)); }
        public Task<SnapshotWorkspaceObservation> ObserveAsync(WorkspaceRecord w, string c, CancellationToken ct = default) =>
            Task.FromResult(new SnapshotWorkspaceObservation(Available, Mounted, Mounted, true, UnknownResources ? null : 0,
                UnknownResources ? null : 0, UnknownResources ? null : 0, DeltaPresent: !DeltaRemoved));
        public Task<SnapshotDeltaSeal> SealAsync(WorkspaceRecord w, string c, CancellationToken ct = default) =>
            Task.FromResult(new SnapshotDeltaSeal(sequence, Compacted ? 50 : 100, (Compacted ? "compact" : "hash") + sequence));
        public Task UnmountAsync(WorkspaceRecord w, string c, CancellationToken ct = default)
        { Mounted = false; if (Directory.Exists(w.Path)) Directory.Delete(w.Path); if (WriteDuringUnmount) sequence++; FailOnce("unmount"); return Task.CompletedTask; }
        public Task<SnapshotJournalCompaction> CompactAsync(WorkspaceRecord w, string c, CancellationToken ct = default,
            long? maxEstimatedJournalOverlapBytes = null)
        { CompactCalls++; SeenOverlapBudgets.Add(maxEstimatedJournalOverlapBytes);
          if (MinimumOverlap is { } minimum && maxEstimatedJournalOverlapBytes is { } budget && budget < minimum)
              throw new WorkspaceException("CompactionOverlapBudgetExceeded", "Fake overlap budget exceeded.");
          Compacted = true; return Task.FromResult(new SnapshotJournalCompaction("attempt", new(sequence, 100, "hash" + sequence),
            new(sequence, 50, "compact" + sequence), DateTimeOffset.UtcNow, 4096, 2048, 6144,
            new(Stopwatch.Frequency, 20, [new("candidate-scan", 10, 100, 0, 0)]))); }
        public Task CompleteCompactionAsync(WorkspaceRecord w, string c, CancellationToken ct = default)
        { CompletedCompaction = true; return Task.CompletedTask; }
        public Task RemoveDeltaAsync(WorkspaceRecord w, string c, CancellationToken ct = default)
        { DeltaRemoved = true; if (LoseRemoveReply) { LoseRemoveReply = false; throw new WorkspaceException("ReplyLost", "after removal"); } return Task.CompletedTask; }
        public Task<SnapshotReclamation> ReleaseSnapshotAsync(WorkspaceRecord w, string c, CancellationToken ct = default)
        {
            ReleaseAttempts++; var blockers = Blockers();
            if (blockers.Length > 0) return Task.FromResult(new SnapshotReclamation(w.Snapshot!.Reference!.Id,
                Guid.NewGuid().ToString("N"), "deferred", DateTimeOffset.UtcNow, blockers,
                new("shared-volume", DateTimeOffset.UtcNow, 1024, 4096, 8192)));
            SnapshotReleased = true; Available = KeepReleaseUnknown ? null : false; FailOnce("snapshot-release");
            return Task.FromResult(new SnapshotReclamation(w.Snapshot!.Reference!.Id, Guid.NewGuid().ToString("N"), "released", DateTimeOffset.UtcNow, []));
        }
    }
    private sealed class Source : ISourceWorkspaceProvider, ISnapshotSourceWorkspaceProvider
    {
        public int Registers; public bool Dirty; public string Fingerprint = "source"; private bool registered;
        public string? LoseReply;
        public Action? AfterPreserve;
        private void FailOnce(string boundary) { if (LoseReply == boundary) { LoseReply = null; throw new WorkspaceException("ReplyLost", boundary); } }
        public Task<string> PreflightAsync(WorkspaceRecord w, CancellationToken ct = default) => Task.FromResult("base");
        public Task RegisterAsync(WorkspaceRecord w, string c, CancellationToken ct = default) { if (!registered) { Registers++; registered = true; } FailOnce("register"); return Task.CompletedTask; }
        public Task AttachAsync(WorkspaceRecord w, string c, CancellationToken ct = default) { FailOnce("attach"); return Task.CompletedTask; }
        public Task<string> PreserveAsync(WorkspaceRecord w, string c, CancellationToken ct = default) { FailOnce("preserve"); AfterPreserve?.Invoke(); return Task.FromResult("result-ref"); }
        public bool InvalidResult;
        public Task VerifyResultAsync(WorkspaceRecord w, string c, CancellationToken ct = default)
        { if (InvalidResult) throw new WorkspaceException("ResultMissing", "Preserved result is unavailable."); return Task.CompletedTask; }
        public Task UnregisterAsync(WorkspaceRecord w, string c, CancellationToken ct = default) { FailOnce("unregister"); return Task.CompletedTask; }
        public Task<string> ResolveRevisionAsync(string p, string r, CancellationToken ct = default) => Task.FromResult(r);
        public Task<string> SnapshotKeyAsync(string p, string r, string[] paths, CancellationToken ct = default) => Task.FromResult(r);
        public Task<SourceWorkspace> CreateAsync(WorkspaceRecord w, CancellationToken ct = default) => throw new InvalidOperationException("No clone/checkout fallback");
        public Task<SourceState> InspectAsync(WorkspaceRecord w, CancellationToken ct = default)
        { if (InspectionError is not null) throw new WorkspaceException(InspectionError, "fixture cache failure");
            FailOnce("inspect"); return Task.FromResult(new SourceState(w.ResultRevision ?? "base", Fingerprint, Dirty, [])); }
        public string? InspectionError;
        public Task<string> CommitAsync(WorkspaceRecord w, string message, CancellationToken ct = default) { Dirty = false; return Task.FromResult("result"); }
        public Task<IntegrationResult> IntegrateAsync(WorkspaceRecord w, string target, CancellationToken ct = default) => throw new InvalidOperationException("Do not force integration");
        public Task ReleaseAsync(WorkspaceRecord w, CancellationToken ct = default) => throw new InvalidOperationException("Do not use checkout deletion");
    }
    private sealed class App : IApplicationWorkspaceProvider
    {
        public int Starts, Children; public bool Running; private ApplicationValidation? validation;
        public Action? AfterRemoveInfrastructure;
        public string[] ManagedPaths => []; public string[] CacheInputs => [];
        public string CompatibilityKey(string project) => "fake";
        public Task<ApplicationLease> StartAsync(WorkspaceRecord w, string c, CancellationToken ct = default) { Starts++; Running = true; return Task.FromResult(new ApplicationLease(1, 1, "fake", "fake")); }
        public Task<ApplicationState> ObserveAsync(WorkspaceRecord w, string c, CancellationToken ct = default) => Task.FromResult(new ApplicationState(Running, Running, "fake", Validation: validation,
            Resources: new(true, Running, Children + (Running ? 1 : 0), Children, Children)));
        public Task RequestValidationAsync(WorkspaceRecord w, string c, string id, string fp, CancellationToken ct = default) { validation = new(id, fp, true, DateTimeOffset.UtcNow, []); return Task.CompletedTask; }
        public Task StopAsync(WorkspaceRecord w, string c, CancellationToken ct = default) { Running = false; return Task.CompletedTask; }
        public Task RemoveInfrastructureAsync(WorkspaceRecord w, string c, CancellationToken ct = default) { AfterRemoveInfrastructure?.Invoke(); return Task.CompletedTask; }
    }
}
