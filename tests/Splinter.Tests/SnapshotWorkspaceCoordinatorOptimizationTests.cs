using Splinter.Core;
using Splinter.Runtime;
using Splinter.SourceControl.Files;

namespace Splinter.Tests;

public sealed partial class SnapshotWorkspaceCoordinatorTests
{
    [Theory]
    [InlineData("success")]
    [InlineData("overlapping-write")]
    [InlineData("dirty")]
    [InlineData("missing-reference")]
    [InlineData("lost-reply")]
    public async Task AtomicCheckpointPreservationRequiresCleanResultAndStableWholeJournal(string outcome)
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        string id = (await f.Ensure(ct)).Workspace.Id;
        var source = new CheckpointSource { Outcome = outcome };
        if (outcome == "overlapping-write") source.AfterPreserve = f.Backend.AcknowledgeWrite;
        var store = new WorkspaceStore(f.Home);
        var router = new SourceWorkspaceRouter(source, new FileWorkspaceProvider(store));
        var coordinator = new WorkspaceCoordinator(store, router, f.App, f.Backend);
        if (outcome == "success")
        {
            var result = await coordinator.CheckpointAsync(id, "result", ct);
            Assert.Equal("result", result.Workspace.ResultRevision);
            Assert.Equal("source", result.Workspace.Snapshot!.Result!.Fingerprint);
            Assert.Null(result.Source);
        }
        else
        {
            var error = await Assert.ThrowsAsync<WorkspaceException>(() => coordinator.CheckpointAsync(id, "result", ct));
            Assert.Equal(outcome == "overlapping-write" ? "DeltaChangedAfterCollection" : outcome == "lost-reply" ? "ReplyLost" : "UncollectedChanges", error.Code);
            Assert.Null(new WorkspaceStore(f.Home).Get(id).Snapshot!.Result);
            Assert.False(f.Backend.DeltaRemoved); Assert.False(f.Backend.SnapshotReleased);
        }
        Assert.Equal(1, source.Transactions);
        Assert.Equal(0, source.InspectCalls);
    }

    [Fact]
    public async Task LostTransactionReplyRetainsPriorManifestAndSameTaskCanRetry()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        string id = (await f.Ensure(ct)).Workspace.Id;
        var source = new CheckpointSource();
        var coordinator = new WorkspaceCoordinator(new(f.Home), source, f.App, f.Backend);
        var prior = (await coordinator.CheckpointAsync(id, "first", ct)).Workspace.Snapshot!.Result;
        source.Dirty = true; source.Fingerprint = "next"; source.Outcome = "lost-reply";
        Assert.Equal("ReplyLost", (await Assert.ThrowsAsync<WorkspaceException>(() => coordinator.CheckpointAsync(id, "next", ct))).Code);
        Assert.Equal(prior, new WorkspaceStore(f.Home).Get(id).Snapshot!.Result);
        Assert.False(f.Backend.DeltaRemoved);
        source.Outcome = "success";
        var resumed = await coordinator.CheckpointAsync(id, "next", ct);
        Assert.Equal(id, resumed.Workspace.Id); Assert.Equal("next", resumed.Workspace.Snapshot!.Result!.Fingerprint);
    }

    private sealed class CheckpointSource : Source, ISnapshotCheckpointSourceProvider
    {
        public bool SupportsCheckpointAndPreserve(WorkspaceRecord w) => true;
        public string Outcome = "success";
        public int Transactions;
        public Task<SnapshotSourceCheckpoint> CheckpointAndPreserveAsync(WorkspaceRecord w, string control, string message, CancellationToken ct = default)
        {
            Transactions++; AfterPreserve?.Invoke();
            if (Outcome == "lost-reply") throw new WorkspaceException("ReplyLost", "Preserved result reply lost.");
            return Task.FromResult(new SnapshotSourceCheckpoint(new("result", Fingerprint, Outcome == "dirty", []),
                Outcome == "missing-reference" ? "" : "result-ref"));
        }
    }

    [Fact]
    public async Task SourceRouterOnlyExposesTheSelectedProvidersCheckpointCapability()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure(ct)).Workspace;
        var router = new SourceWorkspaceRouter(new CheckpointSource(), new FileWorkspaceProvider(new(f.Home)), f.Source);
        Assert.True(router.SupportsCheckpointAndPreserve(w));
        foreach (string kind in new[] { "none", "plastic" })
        {
            w.SourceKind = kind;
            Assert.False(router.SupportsCheckpointAndPreserve(w));
            Assert.Equal("SnapshotCheckpointUnsupported", (await Assert.ThrowsAsync<WorkspaceException>(() =>
                router.CheckpointAndPreserveAsync(w, "control", "result", ct))).Code);
        }
        w.SourceKind = "unknown";
        Assert.Equal("SourceSelectionUnsupported", Assert.Throws<WorkspaceException>(() => router.SupportsCheckpointAndPreserve(w)).Code);
    }

    [Fact]
    public async Task FirstSnapshotCheckpointDoesNotRepeatSourceInspectionForItsResponse()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        string id = (await f.Ensure(ct)).Workspace.Id;
        f.Source.InspectCalls = 0; f.Source.Dirty = true;
        var result = await f.New().CheckpointAsync(id, "result", ct);
        // The fake checkpoint/preserve adapters do not inspect themselves;
        // this is the coordinator's collection inspection only.
        Assert.Equal(1, f.Source.InspectCalls);
        Assert.Null(result.Source); Assert.Equal("not-checked", result.SourceVerification);
        Assert.Equal(f.Source.Fingerprint, result.Workspace.Snapshot!.Result!.Fingerprint);
        Assert.Equal(SnapshotResult.StableJournalProtocol, result.Workspace.Snapshot.Result.CollectionProtocol);
    }

    [Fact]
    public async Task FirstSnapshotCheckpointStillRequiresAnAvailableOwnedMount()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        string id = (await f.Ensure(ct)).Workspace.Id;
        f.Backend.Available = false; f.Source.InspectCalls = 0;
        Assert.Equal("WorkspaceNotReady", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().CheckpointAsync(id, "result", ct))).Code);
        Assert.Equal(0, f.Source.InspectCalls); Assert.Equal(0, f.Source.PreserveCalls);
        Assert.Null(new WorkspaceStore(f.Home).Get(id).Snapshot!.Result);
    }

    [Theory]
    [InlineData("release-unmount")]
    [InlineData("release-registration")]
    [InlineData("release-delta")]
    [InlineData("release-snapshot")]
    public async Task FirstCheckpointCannotReenterAnInterruptedReleaseWithNoResult(string step)
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        var workspace = (await f.Ensure(ct)).Workspace;
        workspace.Phase = "releasing"; workspace.Snapshot!.Step = step;
        var store = new WorkspaceStore(f.Home); store.Save(workspace, "interrupted-release-fixture");
        f.Source.InspectCalls = 0;
        Assert.Equal("WorkspaceNotReady", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().CheckpointAsync(workspace.Id, "result", ct))).Code);
        var retained = store.Get(workspace.Id);
        Assert.Null(retained.ResultRevision); Assert.Null(retained.Snapshot!.Result);
        Assert.Equal(step, retained.Snapshot.Step); Assert.Equal("releasing", retained.Phase);
        Assert.Equal(0, f.Source.InspectCalls); Assert.Equal(0, f.Source.PreserveCalls);
        Assert.True(f.Backend.Mounted); Assert.False(f.Backend.DeltaRemoved); Assert.False(f.Backend.SnapshotReleased);
    }

    [Fact]
    public async Task ReleaseVerifiesAnUnchangedIndependentResultWithoutPreservingItAgain()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        string id = (await f.Ensure(ct)).Workspace.Id;
        var saved = (await f.New().CheckpointAsync(id, "result", ct)).Workspace.Snapshot!.Result;
        int preserves = f.Source.PreserveCalls; f.Source.InspectCalls = 0;
        var released = await f.New().ReleaseAsync(id, ct);
        Assert.Equal("released", released.Workspace.Phase);
        Assert.Equal(1, f.Source.InspectCalls); Assert.Equal(preserves, f.Source.PreserveCalls);
        Assert.True(f.Source.VerifyCalls > 0); Assert.Equal(saved, released.Workspace.Snapshot!.Result);
    }

    [Theory]
    [InlineData("dirty")]
    [InlineData("revision")]
    [InlineData("fingerprint")]
    [InlineData("result")]
    public async Task UnchangedJournalCannotHideChangedSourceMetadataOrCorruptResult(string change)
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        string id = (await f.Ensure(ct)).Workspace.Id;
        var saved = (await f.New().CheckpointAsync(id, "result", ct)).Workspace.Snapshot!.Result;
        if (change == "dirty") f.Source.Dirty = true;
        if (change == "revision") f.Source.RevisionOverride = "another-head";
        if (change == "fingerprint") f.Source.Fingerprint = "changed-index-or-source-interpretation";
        if (change == "result") f.Source.InvalidResult = true;
        var failure = await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ReleaseAsync(id, ct));
        Assert.Equal(change == "result" ? "ResultMissing" : "UncollectedChanges", failure.Code);
        Assert.False(f.Backend.DeltaRemoved); Assert.False(f.Backend.SnapshotReleased); Assert.True(f.Backend.Mounted);
        Assert.Equal(saved, new WorkspaceStore(f.Home).Get(id).Snapshot!.Result);
    }

    [Fact]
    public async Task SourceInspectionWriteCannotPassIndependentResultReuse()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        string id = (await f.Ensure(ct)).Workspace.Id;
        var saved = (await f.New().CheckpointAsync(id, "result", ct)).Workspace.Snapshot!.Result;
        f.Source.AfterInspect = f.Backend.AcknowledgeWrite;
        Assert.Equal("DeltaChangedAfterCollection", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ReleaseAsync(id, ct))).Code);
        Assert.False(f.Backend.DeltaRemoved); Assert.False(f.Backend.SnapshotReleased);
        Assert.Equal(saved, new WorkspaceStore(f.Home).Get(id).Snapshot!.Result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChangedJournalRetainsFullCollectionAndDirtySourceProtection(bool dirtySource)
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        string id = (await f.Ensure(ct)).Workspace.Id;
        var saved = (await f.New().CheckpointAsync(id, "result", ct)).Workspace.Snapshot!.Result;
        int preserves = f.Source.PreserveCalls;
        f.Backend.AcknowledgeWrite(); f.Source.Dirty = dirtySource;
        if (dirtySource)
        {
            Assert.Equal("UncollectedChanges", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ReleaseAsync(id, ct))).Code);
            Assert.Equal(preserves, f.Source.PreserveCalls);
            Assert.False(f.Backend.DeltaRemoved); Assert.False(f.Backend.SnapshotReleased);
            Assert.Equal(saved, new WorkspaceStore(f.Home).Get(id).Snapshot!.Result);
        }
        else
        {
            var released = await f.New().ReleaseAsync(id, ct);
            Assert.Equal("released", released.Workspace.Phase); Assert.Equal(preserves + 1, f.Source.PreserveCalls);
            Assert.NotEqual(saved!.Seal, released.Workspace.Snapshot!.Result!.Seal);
        }
    }
}
