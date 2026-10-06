using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class WorkspaceCoordinatorTests
{
    [Fact]
    public async Task RetiredStorageCannotFallBackToSourceCreation()
    {
        using var d = new TestDirectory(); var sources = new FakeSources();
        var runtime = New(d, sources, new FakeApplications());
        var error = await Assert.ThrowsAsync<WorkspaceException>(() => runtime.EnsureAsync("missing-project", "retired", "main", "refs-cow", false,
            TestContext.Current.CancellationToken));
        Assert.Equal("UnsupportedStorageMode", error.Code);
        Assert.Equal(0, sources.CreateCalls);
        Assert.Empty(new WorkspaceStore(Path.Combine(d.Path, "state")).List());
    }

    [Theory]
    [InlineData("refs-cow", "task")]
    [InlineData("git-checkout", "baseline")]
    public async Task RetiredPersistedWorkspaceIsRejectedWithoutChangingItsFiles(string storageMode, string purpose)
    {
        using var d = new TestDirectory(); var sources = new FakeSources(); var apps = new FakeApplications();
        var store = new WorkspaceStore(Path.Combine(d.Path, "state"));
        var runtime = new WorkspaceCoordinator(store, sources, apps);
        var w = (await runtime.EnsureAsync(Project(d), "retired-record", "main", "git-checkout", false,
            TestContext.Current.CancellationToken)).Workspace;
        string retained = Path.Combine(w.Path, "retained-result"); await File.WriteAllTextAsync(retained, "preserve", TestContext.Current.CancellationToken);
        w.StorageMode = storageMode; w.Purpose = purpose;
        store.Save(w, "retired-fixture");
        string recordPath = Path.Combine(store.ControlPath(w.Id), "workspace.json");
        byte[] before = await File.ReadAllBytesAsync(recordPath, TestContext.Current.CancellationToken);
        Assert.Equal("StateCorrupt", (await Assert.ThrowsAsync<WorkspaceException>(() => runtime.ReleaseAsync(w.Id,
            TestContext.Current.CancellationToken))).Code);
        Assert.Equal(before, await File.ReadAllBytesAsync(recordPath, TestContext.Current.CancellationToken));
        Assert.Equal("preserve", await File.ReadAllTextAsync(retained, TestContext.Current.CancellationToken));
        Assert.Equal(0, sources.ReleaseCalls); Assert.Equal(0, apps.RemoveCalls);
    }

    [Fact]
    public async Task ExistingRequestDigestAndResumeIdentityStayUnchanged()
    {
        using var d = new TestDirectory(); var sources = new FakeSources(); var apps = new FakeApplications(); string project = Project(d);
        var w = (await New(d, sources, apps).EnsureAsync(project, "retained-request", "main", "git-checkout", false,
            TestContext.Current.CancellationToken)).Workspace;
        string previousDigest = WorkspaceStore.Key(project + "\nretained-request\nmain\ngit-checkout\n\ntask\n" +
            SnapshotWorkspaceState.DefaultJournalBytes + "\ngit\nunity");
        Assert.Equal(previousDigest, w.RequestDigest);
        var resumed = await New(d, sources, apps).EnsureAsync(project, "retained-request", "main", "git-checkout", false,
            TestContext.Current.CancellationToken);
        Assert.Equal(w.Id, resumed.Workspace.Id); Assert.Equal(previousDigest, resumed.Workspace.RequestDigest);
        Assert.Equal(1, sources.CreateCalls);
    }

    [Theory]
    [InlineData("baselineId", "\"obsolete-basis\"")]
    [InlineData("cloneMetrics", "{}")]
    public async Task RemovedStateValuesCannotBeIgnoredOrRewritten(string field, string value)
    {
        using var d = new TestDirectory(); var store = new WorkspaceStore(Path.Combine(d.Path, "state"));
        var runtime = new WorkspaceCoordinator(store, new FakeSources(), new FakeApplications());
        var w = (await runtime.EnsureAsync(Project(d), "unknown-state", "main", "git-checkout", false,
            TestContext.Current.CancellationToken)).Workspace;
        string path = Path.Combine(store.ControlPath(w.Id), "workspace.json");
        var node = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken))!;
        node[field] = null;
        await File.WriteAllTextAsync(path, node.ToJsonString(), TestContext.Current.CancellationToken);
        Assert.Equal(w.RequestDigest, store.Get(w.Id).RequestDigest);
        node[field] = System.Text.Json.Nodes.JsonNode.Parse(value);
        await File.WriteAllTextAsync(path, node.ToJsonString(), TestContext.Current.CancellationToken);
        byte[] before = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal("StateCorrupt", Assert.Throws<WorkspaceException>(() => store.Get(w.Id)).Code);
        Assert.Equal(before, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StartAndEnsureEditorDoNotVerifyImportingSourceOrStartDuplicateApplications()
    {
        var ct = TestContext.Current.CancellationToken;
        using var d = new TestDirectory(); var sources = new FakeSources(); var apps = new FakeApplications();
        string project = Project(d);
        var initial = await New(d, sources, apps).EnsureAsync(project, "starting", "main", "git-checkout", false, ct);
        int before = sources.InspectCalls; sources.InspectionError = "SourceChanged";
        var started = await New(d, sources, apps).StartAsync(initial.Workspace.Id, ct);
        Assert.True(started.Application!.Running); Assert.Equal("not-checked", started.SourceVerification);
        Assert.Null(started.Source);
        var resumed = await New(d, sources, apps).EnsureAsync(project, "starting", "main", "git-checkout", true, ct);
        Assert.Equal(initial.Workspace.Id, resumed.Workspace.Id); Assert.Equal("not-checked", resumed.SourceVerification);
        await New(d, sources, apps).StartAsync(initial.Workspace.Id, ct);
        Assert.Equal(before, sources.InspectCalls); Assert.Equal(1, apps.StartCalls); Assert.Equal(1, sources.CreateCalls);
        Assert.Equal("SourceChanged", (await Assert.ThrowsAsync<WorkspaceException>(() =>
            New(d, sources, apps).StatusAsync(initial.Workspace.Id, ct, verifySource: true))).Code);
    }

    [Fact]
    public async Task OriginalOperationErrorSurvivesAStateFailureWhileRecordingIt()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var dir = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        var store = new WorkspaceStore(Path.Combine(dir.Path, "state"));
        FileStream? held = null;
        var sources = new FakeSources { BeforeCreate = record =>
        {
            held = new FileStream(Path.Combine(store.ControlPath(record.Id), "workspace.json"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            throw new WorkspaceException("GitCommandFailed", "controlled original failure");
        } };
        var runtime = new WorkspaceCoordinator(store, sources, new FakeApplications());
        string project = Project(dir);
        try
        {
            var error = await Assert.ThrowsAsync<WorkspaceException>(() => runtime.EnsureAsync(project, "diagnostic", "main", "git-checkout", false, ct));
            Assert.Equal("StateWriteIncomplete", error.Code);
            var response = System.Text.Json.JsonSerializer.SerializeToElement(Splinter.Protocol.ApiResponse.Failure("workspace.ensure", error.Code, error.Message, error.Details), Splinter.Protocol.ApiJson.Options);
            Assert.True(new Splinter.Protocol.ContractCatalog().Validate("urn:splinter:v1:response", response).IsValid);
            var details = response.GetProperty("error").GetProperty("details");
            Assert.Equal("GitCommandFailed", details.GetProperty("operationError").GetString());
            var stateFailure = details.GetProperty("details");
            Assert.Equal("operation-failed", stateFailure.GetProperty("action").GetString());
            Assert.Equal("publish-state", stateFailure.GetProperty("diagnostic").GetProperty("stage").GetString());
            Assert.Equal("unknown", stateFailure.GetProperty("diagnostic").GetProperty("statePublication").GetString());
            Assert.Equal(32, stateFailure.GetProperty("diagnostic").GetProperty("nativeErrorCode").GetInt32());
            string id = stateFailure.GetProperty("id").GetString()!;
            Assert.Null(store.Get(id).LastError);
            Assert.Contains("GitCommandFailed", File.ReadAllText(Assert.Single(Directory.GetFiles(store.ControlPath(id), "*.tmp"))));
        }
        finally { held?.Dispose(); }
        sources.BeforeCreate = null;
        var ready = await runtime.EnsureAsync(project, "diagnostic", "main", "git-checkout", false, ct);
        Assert.Equal("ready", ready.Workspace.Phase);
        Assert.Equal("released", (await runtime.ReleaseAsync(ready.Workspace.Id, ct)).Workspace.Phase);
    }

    [Fact]
    public async Task ExternalProjectValidationIsNotRequiredForCheckpointOrIntegration()
    {
        var ct = TestContext.Current.CancellationToken;
        using var d = new TestDirectory(); var sources = new FakeSources(); var apps = new FakeApplications();
        var c = New(d, sources, apps);
        string id = (await c.EnsureAsync(Project(d), "external", "main", "git-checkout", false, ct)).Workspace.Id;
        sources.Fingerprint = "edited";
        int before = sources.InspectCalls;
        for (int i = 0; i < 5; i++) Assert.Null((await c.StatusAsync(id, ct)).Source);
        Assert.Equal(before, sources.InspectCalls);
        var fresh = await c.StatusAsync(id, ct, verifySource: true);
        Assert.Equal("edited", fresh.Source!.Fingerprint);
        var collected = await c.CheckpointAsync(id, "external work", ct);
        Assert.Null(collected.Workspace.Validation);
        Assert.Equal("collected", collected.Workspace.Phase);
        sources.Fingerprint = "later-write"; sources.Revision = "later-revision";
        Assert.Equal("ResultNotReady", (await Assert.ThrowsAsync<WorkspaceException>(() => c.IntegrateAsync(id, "main", ct))).Code);
        sources.Revision = collected.Workspace.ResultRevision!;
        Assert.Equal("integrated", (await c.IntegrateAsync(id, "main", ct)).Workspace.Phase);
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
    public async Task StopKeepsWorkspaceAndReleaseRefusesDirtyOrUnintegratedResultsThenIsIdempotent()
    {
        var ct = TestContext.Current.CancellationToken;
        using var d = new TestDirectory(); var sources = new FakeSources(); var apps = new FakeApplications();
        var c = New(d, sources, apps); var view = await c.EnsureAsync(Project(d), "task", "main", "git-checkout", true, ct);
        var stopped = await c.StopAsync(view.Workspace.Id, ct); Assert.NotEqual("released", stopped.Workspace.Phase);
        await c.StartAsync(view.Workspace.Id, ct); await c.StatusAsync(view.Workspace.Id, ct);
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

    [Fact]
    public async Task PeerStatusAndSameEnsureCompleteWhileAnotherWorkspaceWaitsForNormalStop()
    {
        var ct = TestContext.Current.CancellationToken;
        using var d = new TestDirectory(); var sources = new FakeSources(); var apps = new FakeApplications();
        string project = Project(d);
        var a = (await New(d, sources, apps).EnsureAsync(project, "parallel-a", "main", "git-checkout", false, ct)).Workspace;
        var b = (await New(d, sources, apps).EnsureAsync(project, "parallel-b", "main", "git-checkout", false, ct)).Workspace;
        var stopping = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        apps.BeforeStop = async (workspace, token) =>
        {
            if (workspace.Id != a.Id) return;
            stopping.SetResult(); await finish.Task.WaitAsync(token);
        };
        var release = New(d, sources, apps).ReleaseAsync(a.Id, ct);
        try
        {
            await stopping.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
            var status = await New(d, sources, apps).StatusAsync(b.Id, ct).WaitAsync(TimeSpan.FromSeconds(2), ct);
            var resumed = await New(d, sources, apps).EnsureAsync(project, "parallel-b", "main", "git-checkout", false, ct)
                .WaitAsync(TimeSpan.FromSeconds(2), ct);
            Assert.False(release.IsCompleted);
            Assert.Equal(b.Id, status.Workspace.Id); Assert.Equal(b.RequestDigest, resumed.Workspace.RequestDigest);
            Assert.Equal(b.Path, resumed.Workspace.Path); Assert.Equal(b.BaseRevision, resumed.Workspace.BaseRevision);
            Assert.Equal(2, sources.CreateCalls); Assert.True(Directory.Exists(b.Path));
        }
        finally { finish.TrySetResult(); await release; }
        Assert.Equal("released", (await New(d, sources, apps).StatusAsync(a.Id, ct)).Workspace.Phase);
        Assert.True(Directory.Exists(b.Path));
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

    private static WorkspaceCoordinator New(TestDirectory d, FakeSources s, FakeApplications a) => new(new WorkspaceStore(System.IO.Path.Combine(d.Path, "state")), s, a);

    private sealed class FakeSources : ISourceWorkspaceProvider
    {
        public string ResolvedRevision = "base";
        public Task<string> ResolveRevisionAsync(string p, string r, CancellationToken ct = default) => Task.FromResult(ResolvedRevision);
        public int CreateCalls, CommitCalls, ReleaseCalls, InspectCalls; public string Fingerprint = "initial"; public string Revision = "rev"; public TimeSpan CreateDelay; public bool DeleteAndThrowOnRelease, LeaveDirectoryOnRelease, Unregistered;
        public Action<WorkspaceRecord>? BeforeCreate; public string? InspectionError;
        public async Task<SourceWorkspace> CreateAsync(WorkspaceRecord w, CancellationToken ct = default) { Interlocked.Increment(ref CreateCalls); BeforeCreate?.Invoke(w); await Task.Delay(CreateDelay, ct); Directory.CreateDirectory(w.Path); return new(w.Path, w.SourceReference, ResolvedRevision); }
        public Task<SourceState> InspectAsync(WorkspaceRecord w, CancellationToken ct = default)
        {
            InspectCalls++;
            if (InspectionError is { } error) throw new WorkspaceException(error, "Controlled source inspection failure.");
            return Directory.Exists(w.Path) && !Unregistered
                ? Task.FromResult(new SourceState(Revision, Fingerprint, false, [])) : throw new DirectoryNotFoundException(w.Path);
        }
        public Task<string> CheckpointAsync(WorkspaceRecord w, string m, CancellationToken ct = default) { Interlocked.Increment(ref CommitCalls); Revision = "result"; return Task.FromResult(Revision); }
        public Task<IntegrationResult> IntegrateAsync(WorkspaceRecord w, string t, CancellationToken ct = default) => Task.FromResult(new IntegrationResult("result", false));
        public Task ReleaseAsync(WorkspaceRecord w, CancellationToken ct = default) { Interlocked.Increment(ref ReleaseCalls); if (DeleteAndThrowOnRelease) { DeleteAndThrowOnRelease = false; Unregistered = true; if (!LeaveDirectoryOnRelease) Directory.Delete(w.Path); throw new IOException("simulated interrupted release"); } if (Directory.Exists(w.Path)) Directory.Delete(w.Path, true); return Task.CompletedTask; }
    }
    private sealed class FakeApplications : IApplicationWorkspaceProvider
    {
        public string CompatibilityKey(string project) => "fake-application";

        public string[] ManagedPaths => ["Library", "Temp"]; public bool Running; public int RemoveCalls, StartCalls;
        public bool OwnershipVerified = true; public int SurvivingChildren;
        public Func<WorkspaceRecord, CancellationToken, Task>? BeforeStop;
        public Task<ApplicationLease> StartAsync(WorkspaceRecord w, string p, CancellationToken ct = default) { StartCalls++; Running = true; return Task.FromResult(new ApplicationLease(1, 1, "token", "fake")); }
        public Task<ApplicationState> ObserveAsync(WorkspaceRecord w, string p, CancellationToken ct = default)
        {
            return Task.FromResult(new ApplicationState(Running, Running, Running ? "ready" : "stopped", null, Resources:
                new(OwnershipVerified, Running, (Running ? 1 : 0) + SurvivingChildren, Running || SurvivingChildren != 0 ? 1024 : 0, Running || SurvivingChildren != 0 ? 1024 : 0)));
        }
        public async Task StopAsync(WorkspaceRecord w, string p, CancellationToken ct = default) { if (BeforeStop is not null) await BeforeStop(w, ct); Running = false; }
        public Task RemoveInfrastructureAsync(WorkspaceRecord w, string p, CancellationToken ct = default) { RemoveCalls++; return Task.CompletedTask; }
    }
}
