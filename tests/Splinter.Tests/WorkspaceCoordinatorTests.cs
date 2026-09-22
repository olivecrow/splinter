using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class WorkspaceCoordinatorTests
{
    [Fact]
    public async Task RepeatedStatusDoesNotInspectSourceWhileWaitingOrAfterAResultWasReconciled()
    {
        var ct = TestContext.Current.CancellationToken;
        using var d = new TestDirectory(); var sources = new FakeSources(); var apps = new FakeApplications();
        string id = (await New(d, sources, apps).EnsureAsync(Project(d), "poll", "main", "git-checkout", true, ct)).Workspace.Id;
        await New(d, sources, apps).RefreshAsync(id, ct);
        int before = sources.InspectCalls;
        for (int i = 0; i < 10; i++) Assert.Null((await New(d, sources, apps).StatusAsync(id, ct)).Source);
        Assert.Equal(before, sources.InspectCalls);
        apps.EmitValidation = true;
        var completed = await New(d, sources, apps).StatusAsync(id, ct);
        Assert.True(completed.Workspace.Validation!.Success);
        Assert.Equal("verified", completed.SourceVerification);
        Assert.True(completed.ValidationCurrent);
        Assert.Equal("initial", completed.Source!.Fingerprint);
        Assert.Equal(before + 1, sources.InspectCalls);
        for (int i = 0; i < 10; i++) Assert.Null((await New(d, sources, apps).StatusAsync(id, ct)).Source);
        Assert.Equal(before + 1, sources.InspectCalls);
        sources.Fingerprint = "changed-after-validation";
        var historical = await New(d, sources, apps).StatusAsync(id, ct);
        Assert.Equal("not-checked", historical.SourceVerification);
        Assert.Null(historical.ValidationCurrent);
        Assert.True(historical.Workspace.Validation!.Success);
        Assert.Equal("ValidationRequired", (await Assert.ThrowsAsync<WorkspaceException>(() =>
            New(d, sources, apps).CheckpointAsync(id, "stale result", ct))).Code);
        Assert.Equal(0, sources.CommitCalls);
    }

    [Fact]
    public async Task ExplicitSourceVerificationInvalidatesAnOldSuccess()
    {
        var ct = TestContext.Current.CancellationToken;
        using var d = new TestDirectory(); var sources = new FakeSources(); var apps = new FakeApplications { EmitValidation = true };
        var c = New(d, sources, apps);
        string id = (await c.EnsureAsync(Project(d), "verify", "main", "git-checkout", true, ct)).Workspace.Id;
        await c.RefreshAsync(id, ct); await c.StatusAsync(id, ct);
        sources.Fingerprint = "edited";
        int before = sources.InspectCalls;
        var fresh = await New(d, sources, apps).StatusAsync(id, ct, verifySource: true);
        Assert.Equal(before + 1, sources.InspectCalls);
        Assert.Equal("edited", fresh.Source!.Fingerprint);
        Assert.False(fresh.ValidationCurrent);
        Assert.Null(fresh.Workspace.Validation);
        Assert.Equal("source-changed", fresh.Workspace.Phase);
    }

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
    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public async Task ReleaseCannotDeleteWhenDescendantsRemainOrOwnershipIsUnknown(bool verified, int children)
    {
        var ct = TestContext.Current.CancellationToken; using var d = new TestDirectory();
        var sources = new FakeSources(); var apps = new FakeApplications { OwnershipVerified = verified, SurvivingChildren = children };
        var runtime = New(d, sources, apps);
        var view = await runtime.EnsureAsync(Project(d), "resources", "main", "git-checkout", false, ct);
        var error = await Assert.ThrowsAsync<WorkspaceException>(() => runtime.ReleaseAsync(view.Workspace.Id, ct));
        Assert.Equal("ProcessCleanupIncomplete", error.Code); Assert.Equal(0, sources.ReleaseCalls);
        Assert.True(Directory.Exists(view.Workspace.Path));
        apps.OwnershipVerified = true; apps.SurvivingChildren = 0;
        var released = await runtime.ReleaseAsync(view.Workspace.Id, ct);
        Assert.Equal(new ProcessResources(true, false, 0, 0, 0), released.Workspace.ReleasedResources);
    }

    [Fact]
    public async Task UnsupportedCoWIsRejectedBeforeGitCreationAndNeverFallsBack()
    {
        var ct = TestContext.Current.CancellationToken; using var d = new TestDirectory(); var sources = new FakeSources();
        var store = new WorkspaceStore(Path.Combine(d.Path, "state"));
        var runtime = new WorkspaceCoordinator(store, sources, new FakeApplications(), new FakeStorage { Unsupported = true });
        var error = await Assert.ThrowsAsync<WorkspaceException>(() => runtime.PrepareBaselineAsync(Project(d), "main", ct));
        Assert.Equal("CoWUnsupported", error.Code); Assert.Equal(0, sources.CreateCalls); Assert.Empty(store.List());
    }

    [Fact]
    public async Task BaselineSealsOnlyAfterValidationAndRejectsMutationAndActiveConsumerRemoval()
    {
        var ct = TestContext.Current.CancellationToken; using var d = new TestDirectory(); var sources = new FakeSources { Revision = "base" };
        var apps = new FakeApplications { EmitValidation = true }; var storage = new FakeStorage();
        var store = new WorkspaceStore(Path.Combine(d.Path, "state")); var runtime = new WorkspaceCoordinator(store, sources, apps, storage);
        string project = Project(d);
        var prepared = await runtime.PrepareBaselineAsync(project, "main", ct); string id = prepared.Workspace.Id;
        await Assert.ThrowsAsync<WorkspaceException>(() => runtime.SealBaselineAsync(id, ct));
        string library = Path.Combine(prepared.Workspace.Path, "Library"); Directory.CreateDirectory(library);
        await File.WriteAllTextAsync(Path.Combine(library, "cache"), "warmed once", ct);
        await runtime.RefreshAsync(id, ct); await runtime.StatusAsync(id, ct);
        var baseline = await runtime.SealBaselineAsync(id, ct);
        Assert.False(Directory.Exists(prepared.Workspace.Path)); Assert.Equal(1, storage.CloneCalls);
        Assert.Equal(id, (await runtime.PrepareBaselineAsync(project, "main", ct)).Workspace.Id);
        Assert.Equal(1, sources.CreateCalls);
        var consumer = await runtime.EnsureAsync(project, "consumer", "main", "refs-cow", false, ct, id);
        var busy = await Assert.ThrowsAsync<WorkspaceException>(() => runtime.ReleaseBaselineAsync(id, ct));
        Assert.Equal("BaselineInUse", busy.Code);
        await runtime.ReleaseAsync(consumer.Workspace.Id, ct);
        await File.WriteAllTextAsync(Path.Combine(baseline.Path, "Library", "cache"), "unexpected change", ct);
        var changed = await Assert.ThrowsAsync<WorkspaceException>(() => runtime.ReleaseBaselineAsync(id, ct));
        Assert.Equal("BaselineChanged", changed.Code); Assert.True(Directory.Exists(baseline.Path));
        await File.WriteAllTextAsync(Path.Combine(baseline.Path, "Library", "cache"), "warmed once", ct);
        await runtime.ReleaseBaselineAsync(id, ct); Assert.False(Directory.Exists(baseline.Path));
        Assert.NotEqual(id, (await runtime.PrepareBaselineAsync(project, "main", ct)).Workspace.Id);
    }
    private static WorkspaceCoordinator New(TestDirectory d, FakeSources s, FakeApplications a) => new(new WorkspaceStore(System.IO.Path.Combine(d.Path, "state")), s, a);

    private sealed class FakeSources : ISourceWorkspaceProvider
    {
        public string ResolvedRevision = "base";
        public Task<string> ResolveRevisionAsync(string p, string r, CancellationToken ct = default) => Task.FromResult(ResolvedRevision);
        public Task<string> SnapshotKeyAsync(string p, string r, string[] paths, CancellationToken ct = default) => Task.FromResult("fake-cache");
        public int CreateCalls, CommitCalls, ReleaseCalls, InspectCalls; public string Fingerprint = "initial"; public string Revision = "rev"; public TimeSpan CreateDelay; public bool DeleteAndThrowOnRelease, LeaveDirectoryOnRelease, Unregistered;
        public async Task<SourceWorkspace> CreateAsync(WorkspaceRecord w, CancellationToken ct = default) { Interlocked.Increment(ref CreateCalls); await Task.Delay(CreateDelay, ct); Directory.CreateDirectory(w.Path); return new(w.Path, w.SourceReference, ResolvedRevision); }
        public Task<SourceState> InspectAsync(WorkspaceRecord w, CancellationToken ct = default)
        {
            InspectCalls++;
            return Directory.Exists(w.Path) && !Unregistered
                ? Task.FromResult(new SourceState(Revision, Fingerprint, false, [])) : throw new DirectoryNotFoundException(w.Path);
        }
        public Task<string> CommitAsync(WorkspaceRecord w, string m, CancellationToken ct = default) { Interlocked.Increment(ref CommitCalls); Revision = "result"; return Task.FromResult(Revision); }
        public Task<IntegrationResult> IntegrateAsync(WorkspaceRecord w, string t, CancellationToken ct = default) => Task.FromResult(new IntegrationResult("result", false));
        public Task ReleaseAsync(WorkspaceRecord w, CancellationToken ct = default) { Interlocked.Increment(ref ReleaseCalls); if (DeleteAndThrowOnRelease) { DeleteAndThrowOnRelease = false; Unregistered = true; if (!LeaveDirectoryOnRelease) Directory.Delete(w.Path); throw new IOException("simulated interrupted release"); } if (Directory.Exists(w.Path)) Directory.Delete(w.Path, true); return Task.CompletedTask; }
    }
    private sealed class FakeApplications : IApplicationWorkspaceProvider
    {
        public string CompatibilityKey(string project) => "fake-application";
        public string[] CacheInputs => ["configuration"];
        public string[] ManagedPaths => ["Library", "Temp"]; public bool Running; public bool EmitValidation; public int RemoveCalls;
        public bool OwnershipVerified = true; public int SurvivingChildren;
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
            return Task.FromResult(new ApplicationState(Running, Running, Running ? "ready" : "stopped", null, validation,
                new(OwnershipVerified, Running, (Running ? 1 : 0) + SurvivingChildren, Running || SurvivingChildren != 0 ? 1024 : 0, Running || SurvivingChildren != 0 ? 1024 : 0)));
        }
        public Task RequestValidationAsync(WorkspaceRecord w, string p, string id, string fp, CancellationToken ct = default) { pending = (id, fp); return Task.CompletedTask; }
        public Task StopAsync(WorkspaceRecord w, string p, CancellationToken ct = default) { Running = false; return Task.CompletedTask; }
        public Task RemoveInfrastructureAsync(WorkspaceRecord w, string p, CancellationToken ct = default) { RemoveCalls++; return Task.CompletedTask; }
    }
    private sealed class FakeStorage : IWorkspaceStorageProvider
    {
        public bool Unsupported; public int CloneCalls;
        public StorageProbe Probe(string d) => Unsupported ? throw new WorkspaceException("CoWUnsupported", "fake unsupported volume") : new("fake", "fake", "fake", 4096);
        public Task<CloneStatistics> CloneAsync(string source, string destination, string[] excluded, CancellationToken ct = default)
        {
            CloneCalls++; long bytes = 0; int count = 0; Directory.CreateDirectory(destination);
            foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(source, file); if (excluded.Contains(relative.Split(Path.DirectorySeparatorChar)[0])) continue;
                string target = Path.Combine(destination, relative); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(file, target);
                bytes += new FileInfo(file).Length; count++;
            }
            return Task.FromResult(new CloneStatistics(bytes, 0, bytes, count, 0));
        }
    }
}
