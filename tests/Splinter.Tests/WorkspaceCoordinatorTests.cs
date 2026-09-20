using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class WorkspaceCoordinatorTests
{
    [Fact]
    public async Task EnsureIsIdempotentAcrossFreshStoreInstancesAndRejectsParameterConflict()
    {
        var ct = TestContext.Current.CancellationToken;
        using var d = new TestDirectory();
        var sources = new FakeSources(); var apps = new FakeApplications();
        var first = await New(d, sources, apps).EnsureAsync(Project(d), "task", "main", "git-checkout", false, ct);
        var second = await New(d, sources, apps).EnsureAsync(Project(d), "task", "main", "git-checkout", false, ct);
        Assert.Equal(first.Workspace.Id, second.Workspace.Id);
        Assert.Equal(1, sources.CreateCalls);
        var error = await Assert.ThrowsAsync<WorkspaceException>(() => New(d, sources, apps).EnsureAsync(Project(d), "task", "other", "git-checkout", false, ct));
        Assert.Equal("RequestKeyConflict", error.Code);
    }

    [Fact]
    public async Task NonGitNonUnityFakesCanDriveValidationAndChangedFingerprintBlocksCheckpoint()
    {
        var ct = TestContext.Current.CancellationToken;
        using var d = new TestDirectory(); var sources = new FakeSources(); var apps = new FakeApplications();
        var c = New(d, sources, apps); var view = await c.EnsureAsync(Project(d), "task", "main", "git-checkout", true, ct);
        apps.EmitValidation = true;
        var pending = await c.RefreshAsync(view.Workspace.Id, ct);
        sources.Fingerprint = "changed";
        var failed = await c.StatusAsync(view.Workspace.Id, ct);
        Assert.Equal("validation-failed", failed.Workspace.Phase);
        var error = await Assert.ThrowsAsync<WorkspaceException>(() => c.CheckpointAsync(view.Workspace.Id, "result", ct));
        Assert.Equal("ValidationRequired", error.Code);
        Assert.Equal(0, sources.CommitCalls);
        Assert.NotNull(pending.Workspace.PendingValidationId);
    }

    [Fact]
    public async Task AsyncValidationAcceptanceDoesNotMeanSuccessAndMatchingResultCanCheckpoint()
    {
        var ct = TestContext.Current.CancellationToken;
        using var d = new TestDirectory(); var sources = new FakeSources(); var apps = new FakeApplications();
        var c = New(d, sources, apps); var view = await c.EnsureAsync(Project(d), "task", "main", "git-checkout", true, ct);
        apps.EmitValidation = false; await c.RefreshAsync(view.Workspace.Id, ct);
        var before = await c.StatusAsync(view.Workspace.Id, ct);
        Assert.Null(before.Workspace.Validation);
        await Assert.ThrowsAsync<WorkspaceException>(() => c.CheckpointAsync(view.Workspace.Id, "result", ct));
        apps.EmitValidation = true; await c.StatusAsync(view.Workspace.Id, ct);
        var result = await c.CheckpointAsync(view.Workspace.Id, "result", ct);
        Assert.Equal("collected", result.Workspace.Phase);
    }

    [Fact]
    public async Task StopKeepsWorkspaceAndReleaseRefusesDirtyOrUnintegratedResultsThenIsIdempotent()
    {
        var ct = TestContext.Current.CancellationToken;
        using var d = new TestDirectory(); var sources = new FakeSources(); var apps = new FakeApplications();
        var c = New(d, sources, apps); var view = await c.EnsureAsync(Project(d), "task", "main", "git-checkout", true, ct);
        var stopped = await c.StopAsync(view.Workspace.Id, ct); Assert.NotEqual("released", stopped.Workspace.Phase);
        apps.EmitValidation = true; await c.StartAsync(view.Workspace.Id, ct); await c.RefreshAsync(view.Workspace.Id, ct); await c.StatusAsync(view.Workspace.Id, ct);
        await c.CheckpointAsync(view.Workspace.Id, "result", ct);
        var blocked = await Assert.ThrowsAsync<WorkspaceException>(() => c.ReleaseAsync(view.Workspace.Id, ct));
        Assert.Equal("UncollectedChanges", blocked.Code); Assert.Equal(0, sources.ReleaseCalls);
        await c.IntegrateAsync(view.Workspace.Id, "main", ct);
        var released = await c.ReleaseAsync(view.Workspace.Id, ct); var again = await c.ReleaseAsync(view.Workspace.Id, ct);
        Assert.Equal("released", released.Workspace.Phase); Assert.Equal(1, sources.ReleaseCalls); Assert.Equal("released", again.Workspace.Phase);
        Assert.Equal(1, apps.RemoveCalls);
    }

    [Fact]
    public async Task ConcurrentDistinctKeysProceedAndSameKeyCreatesOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        using var d = new TestDirectory(); var sources = new FakeSources { CreateDelay = TimeSpan.FromMilliseconds(40) }; var apps = new FakeApplications();
        var tasks = Enumerable.Range(0, 4).Select(i => New(d, sources, apps).EnsureAsync(Project(d), "k" + i, "main", "git-checkout", false, ct)).ToArray();
        await Task.WhenAll(tasks); Assert.Equal(4, sources.CreateCalls);
        var same = Enumerable.Range(0, 4).Select(_ => New(d, sources, apps).EnsureAsync(Project(d), "same", "main", "git-checkout", false, ct)).ToArray();
        await Task.WhenAll(same); Assert.Equal(5, sources.CreateCalls); Assert.Single(same.Select(t => t.Result.Workspace.Id).Distinct());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Release_resumes_after_source_removed_workspace_before_completion_save(bool leaveDirectory)
    {
        var ct = TestContext.Current.CancellationToken; using var d = new TestDirectory(); var sources = new FakeSources { DeleteAndThrowOnRelease = true, LeaveDirectoryOnRelease = leaveDirectory }; var apps = new FakeApplications(); var c = New(d, sources, apps);
        var view = await c.EnsureAsync(Project(d), "release-recovery", "main", "git-checkout", false, ct);
        await Assert.ThrowsAsync<IOException>(() => c.ReleaseAsync(view.Workspace.Id, ct)); Assert.Equal(1, sources.ReleaseCalls);
        Assert.Equal(leaveDirectory, Directory.Exists(view.Workspace.Path));
        var resumed = await New(d, sources, apps).ReleaseAsync(view.Workspace.Id, ct);
        Assert.Equal("released", resumed.Workspace.Phase); Assert.Equal(2, sources.ReleaseCalls);
        Assert.Equal(leaveDirectory ? 2 : 1, apps.RemoveCalls);
    }

    private static string Project(TestDirectory d) { var p = System.IO.Path.Combine(d.Path, "project"); Directory.CreateDirectory(p); return p; }
    private static WorkspaceCoordinator New(TestDirectory d, FakeSources s, FakeApplications a) => new(new WorkspaceStore(System.IO.Path.Combine(d.Path, "state")), s, a);

    private sealed class FakeSources : ISourceWorkspaceProvider
    {
        public int CreateCalls, CommitCalls, ReleaseCalls; public string Fingerprint = "initial"; public string Revision = "rev"; public TimeSpan CreateDelay; public bool DeleteAndThrowOnRelease, LeaveDirectoryOnRelease, Unregistered;
        public async Task<SourceWorkspace> CreateAsync(WorkspaceRecord w, CancellationToken ct = default) { Interlocked.Increment(ref CreateCalls); await Task.Delay(CreateDelay, ct); Directory.CreateDirectory(w.Path); return new(w.Path, w.SourceReference, "base"); }
        public Task<SourceState> InspectAsync(WorkspaceRecord w, CancellationToken ct = default) => Directory.Exists(w.Path) && !Unregistered
            ? Task.FromResult(new SourceState(Revision, Fingerprint, false, [])) : throw new DirectoryNotFoundException(w.Path);
        public Task<string> CommitAsync(WorkspaceRecord w, string m, CancellationToken ct = default) { Interlocked.Increment(ref CommitCalls); Revision = "result"; return Task.FromResult(Revision); }
        public Task<IntegrationResult> IntegrateAsync(WorkspaceRecord w, string t, CancellationToken ct = default) => Task.FromResult(new IntegrationResult("result", false));
        public Task ReleaseAsync(WorkspaceRecord w, CancellationToken ct = default) { Interlocked.Increment(ref ReleaseCalls); if (DeleteAndThrowOnRelease) { DeleteAndThrowOnRelease = false; Unregistered = true; if (!LeaveDirectoryOnRelease) Directory.Delete(w.Path); throw new IOException("simulated interrupted release"); } return Task.CompletedTask; }
    }
    private sealed class FakeApplications : IApplicationWorkspaceProvider
    {
        public string[] ManagedPaths => ["Library", "Temp"]; public bool Running; public bool EmitValidation; public int RemoveCalls;
        private ApplicationValidation? validation;
        private (string Id, string Fingerprint)? pending;
        public Task<ApplicationLease> StartAsync(WorkspaceRecord w, string p, CancellationToken ct = default) { Running = true; return Task.FromResult(new ApplicationLease(1, 1, "token", "fake")); }
        public Task<ApplicationState> ObserveAsync(WorkspaceRecord w, string p, CancellationToken ct = default)
        {
            if (EmitValidation && pending is { } request)
            {
                validation = new(request.Id, request.Fingerprint, true, DateTimeOffset.UtcNow, []);
                pending = null;
            }
            return Task.FromResult(new ApplicationState(Running, Running, Running ? "ready" : "stopped", null, validation));
        }
        public Task RequestValidationAsync(WorkspaceRecord w, string p, string id, string fp, CancellationToken ct = default) { pending = (id, fp); return Task.CompletedTask; }
        public Task StopAsync(WorkspaceRecord w, string p, CancellationToken ct = default) { Running = false; return Task.CompletedTask; }
        public Task RemoveInfrastructureAsync(WorkspaceRecord w, string p, CancellationToken ct = default) { RemoveCalls++; return Task.CompletedTask; }
    }
}
