using System.Text.Json;
using Splinter.Core;
using Splinter.Platform.Windows;

namespace Splinter.Tests;

public sealed class OverlaySnapshotObservationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProjectStorageProofRemainsSeparateWhenTheGeneratedBasisIsMissing(bool generatedMissing)
    {
        var request = Request(); string hash = new('a', 64);
        var library = new SnapshotReference("library", OwnedDirectorySnapshotTree.ProviderName, "library-source", "library-basis",
            DateTimeOffset.UtcNow, true, true, "stopped-original-owned-directory");
        var basis = new GeneratedDirectoryReference("library", "project", "Library", library, "library-receipt", "owner", hash);
        var access = new SnapshotReadAccess(library.Id, library.RootPath, hash);
        request = request with { Identity = request.Identity with { Graft = new("Library", basis) }, GeneratedSnapshot = new(access, basis.ReceiptDirectory, basis.OwnerId) };
        var projectProof = new SnapshotObservation(request.Identity.Snapshot.Id, true, null, 12, null) { ReadAccess = request.ReadAccess };
        var libraryProof = new SnapshotObservation(library.Id, !generatedMissing, generatedMissing ? "SnapshotMissing" : null, 34, null)
            { ReadAccess = generatedMissing ? null : access };
        var projectScope = new ObservedScope(request.Identity.Snapshot, projectProof); var libraryScope = new ObservedScope(library, libraryProof);
        SnapshotObservation? supplied = null;
        var combined = await WinFspOverlayClient.ObserveSnapshotAsync(request, TestContext.Current.CancellationToken,
            projectScope, null, libraryScope, proof => supplied = proof);
        Assert.Equal(projectProof, supplied); Assert.Equal(1, projectScope.Observations); Assert.Equal(1, libraryScope.Observations);
        Assert.Equal(!generatedMissing, combined.Available);
        if (generatedMissing) Assert.Equal("SnapshotMissing", combined.FailureCode);
    }
    private sealed class ObservedScope(SnapshotReference snapshot, SnapshotObservation observation) : ISnapshotTree, ISnapshotAvailabilityScope
    {
        public SnapshotReference Snapshot => snapshot;
        public int Observations { get; private set; }
        public long MetadataFenceCount => 1;
        public long MetadataFenceBytes => 1;
        public SnapshotObservation? ObserveAvailability(SnapshotReadAccess expectedAccess) { Observations++; return observation; }
        public void EnsureAvailable() { }
        public SnapshotEntry? Stat(string path) => null;
        public IReadOnlyList<SnapshotEntry> List(string path) => [];
        public int Read(string path, long offset, Span<byte> destination) => throw new NotSupportedException();
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static OverlayServeRequest Request()
    {
        var snapshot = new SnapshotReference("snapshot", "fake", "source", "basis", DateTimeOffset.UtcNow, true, true, "fixed");
        return new(new(3, "overlay", snapshot, long.MaxValue, 65536), "delta", "mount", "control", "token",
            new(snapshot.Id, snapshot.RootPath, "verified"), "receipt", "owner");
    }
    private static JsonElement State(DateTimeOffset at, int pid = 19, long ticks = 27, string token = "token",
        string id = "overlay", string snapshot = "snapshot") => JsonSerializer.SerializeToElement(new
        { token, id, snapshotId = snapshot, phase = "mounted", processId = pid, processStartTimeUtcTicks = ticks, observedAt = at }, Json);
    private static (JsonElement State, WinFspOverlayClient.ProviderLaunch? Launch, WinFspOverlayClient.ProviderIntent? Intent)
        Controls(JsonElement state) => (state, new("token", 19, 27, "generation"),
            new("token", "overlay", "generation", DateTimeOffset.UnixEpoch));
    private static Task<SnapshotObservation> Available() => Task.FromResult(new SnapshotObservation("snapshot", true, null, 0, null));

    [Fact]
    public async Task SlowCompleteSnapshotProofUsesHeartbeatReadAfterProofWithoutExtendingFreshnessWindow()
    {
        var ct = TestContext.Current.CancellationToken;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var clock = DateTimeOffset.UtcNow.AddMinutes(-2); int proofs = 0, reads = 0;
        var initial = Controls(State(clock));
        async Task<SnapshotObservation> Prove()
        {
            proofs++; entered.SetResult(); await released.Task.WaitAsync(ct);
            return await Available();
        }
        var observing = WinFspOverlayClient.ObserveWithSnapshotProofAsync(Request(), initial, () =>
        {
            Assert.True(released.Task.IsCompleted); reads++;
            return Task.FromResult(Controls(State(clock)));
        }, Prove, (_, _) => true, _ => true);
        await entered.Task.WaitAsync(ct); Assert.False(observing.IsCompleted);
        // Advance the fake heartbeat clock by two minutes while catalog proof
        // is blocked. No wall-clock sleep or relaxed readiness deadline.
        clock = DateTimeOffset.UtcNow; released.SetResult();
        var observed = await observing;
        var ready = await WinFspOverlayClient.AwaitMountedAsync(() => Task.FromResult(observed), 19, 27, TimeSpan.Zero, ct);
        Assert.Equal(clock, ready.GetProperty("state").GetProperty("observedAt").GetDateTimeOffset());
        Assert.Equal(1, proofs); Assert.Equal(1, reads);
    }

    [Theory]
    [InlineData("pid", "OverlayStartUncertain")]
    [InlineData("start", "OverlayStartUncertain")]
    [InlineData("token", "OverlayOwnerMismatch")]
    [InlineData("request", "OverlayOwnerMismatch")]
    [InlineData("snapshot", "OverlayOwnerMismatch")]
    [InlineData("launch", "OverlayStartUncertain")]
    [InlineData("intent", "OverlayStartUncertain")]
    public async Task ChangedAuthorityDuringFullSnapshotProofIsRejected(string change, string code)
    {
        int proofs = 0;
        var initial = Controls(State(DateTimeOffset.UtcNow.AddMinutes(-2)));
        var current = Controls(State(DateTimeOffset.UtcNow,
            pid: change == "pid" ? 20 : 19, ticks: change == "start" ? 28 : 27,
            token: change == "token" ? "foreign" : "token", id: change == "request" ? "foreign" : "overlay",
            snapshot: change == "snapshot" ? "foreign" : "snapshot"));
        if (change == "launch") current.Launch = current.Launch! with { Generation = "another" };
        if (change == "intent") current.Intent = current.Intent! with { VerificationId = "another-proof" };
        var error = await Assert.ThrowsAsync<WorkspaceException>(() => WinFspOverlayClient.ObserveWithSnapshotProofAsync(
            Request(), initial, () => Task.FromResult(current), () => { proofs++; return Available(); }, (_, _) => true, _ => true));
        Assert.Equal(code, error.Code); Assert.Equal(1, proofs);
    }

    [Theory]
    [InlineData(true, true, true, "OverlayStartUncertain")]
    [InlineData(false, false, true, "OverlayStartUncertain")]
    [InlineData(false, true, false, "OverlayStartFailed")]
    public async Task PostProofStaleHeartbeatAbsentMountAndExitedProviderRemainNotReady(bool stale, bool mounted,
        bool alive, string code)
    {
        var initial = Controls(State(DateTimeOffset.UtcNow.AddMinutes(-2))); int aliveCalls = 0;
        var observed = await WinFspOverlayClient.ObserveWithSnapshotProofAsync(Request(), initial,
            () => Task.FromResult(Controls(State(stale ? DateTimeOffset.UtcNow.AddMinutes(-1) : DateTimeOffset.UtcNow))),
            Available, (_, _) => ++aliveCalls <= 2 || alive, _ => mounted);
        var error = await Assert.ThrowsAsync<WorkspaceException>(() => WinFspOverlayClient.AwaitMountedAsync(
            () => Task.FromResult(observed), 19, 27, TimeSpan.Zero, TestContext.Current.CancellationToken));
        Assert.Equal(code, error.Code);
    }

    [Fact]
    public async Task FreshOwnedHeartbeatDoesNotReplaceTheCompleteSnapshotProof()
    {
        int proofs = 0; var current = Controls(State(DateTimeOffset.UtcNow));
        var observed = await WinFspOverlayClient.ObserveWithSnapshotProofAsync(Request(), current, () => Task.FromResult(current),
            () => { proofs++; return Task.FromResult(new SnapshotObservation("snapshot", false, "SnapshotMissing", null, null)); },
            (_, _) => true, _ => true);
        Assert.False(observed.GetProperty("snapshotAvailable").GetBoolean());
        var error = await Assert.ThrowsAsync<WorkspaceException>(() => WinFspOverlayClient.AwaitMountedAsync(
            () => Task.FromResult(observed), 19, 27, TimeSpan.Zero, TestContext.Current.CancellationToken));
        Assert.Equal("OverlayStartUncertain", error.Code); Assert.Equal(1, proofs);
    }
}
