using System.Diagnostics;
using System.Text.Json;
using Splinter.Cli;
using Splinter.Core;
using Splinter.Protocol;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class PreparationInterruptionTests
{
    public static TheoryData<string> Boundaries => new(DevelopmentPreparationProbe.Boundaries.Where(p =>
        !p.StartsWith("git-") && !p.StartsWith("delta.") && !p.StartsWith("provider.")).ToArray());

    [Theory]
    [MemberData(nameof(Boundaries))]
    public async Task KilledClientResumesDurableFakeReceiptsWithoutDuplicateIdentity(string boundary)
    {
        using var dir = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        string project = Path.Combine(dir.Path, "source"), home = Path.Combine(dir.Path, "home"), signal = Path.Combine(dir.Path, "signal.json");
        Directory.CreateDirectory(project);
        var request = new { version = 1, project, home, key = "interrupted", baseReference = "base", boundary, signalPath = signal };
        string input = Path.Combine(dir.Path, "input.json"); await File.WriteAllTextAsync(input, JsonSerializer.Serialize(request), ct);
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string arg in new[] { typeof(PreparationInterruptionTests).Assembly.Location, "-method", "Splinter.Tests.PreparationInterruptionTests.ChildEnsure" }) start.ArgumentList.Add(arg);
        start.Environment["SPLINTER_TEST_PREPARATION_INPUT"] = input;
        using var child = Process.Start(start)!;
        var stdout = child.StandardOutput.ReadToEndAsync(ct); var stderr = child.StandardError.ReadToEndAsync(ct);
        try
        {
            long started = Stopwatch.GetTimestamp();
            while (!File.Exists(signal) && !child.HasExited && Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(25)) await Task.Delay(25, ct);
            Assert.True(File.Exists(signal), child.HasExited ? await stdout + await stderr : "Child did not reach " + boundary);
            var proof = JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync(signal, ct));
            Assert.True(new ContractCatalog().Validate("urn:splinter:v1:preparation-probe-signal", proof).IsValid);
            Assert.Equal(child.Id, proof.GetProperty("processId").GetInt32());
            child.Kill(); await child.WaitForExitAsync(ct); // Actual termination, not an exception caught by RecordFailure.
            var ready = await Task.WhenAll(Ensure(project, home, null, ct), Ensure(project, home, null, ct));
            Assert.Equal(proof.GetProperty("workspaceId").GetString(), ready[0].Workspace.Id);
            Assert.Equal(ready[0].Workspace.Snapshot!.Reference, ready[1].Workspace.Snapshot!.Reference);
            Assert.Equal("ready", ready[1].Workspace.Phase); Assert.Null(ready[1].Workspace.LastError);
            string control = new WorkspaceStore(home).ControlPath(ready[0].Workspace.Id);
            Assert.Single(Directory.GetFiles(control, "capture-created-*"));
            Assert.Single(Directory.GetFiles(control, "registration-created-*"));
            Assert.Equal(new SnapshotRecoveryDecision("ready", "WorkspaceReady", "workspace.status"), ready[1].Recovery);
        }
        finally
        {
            if (!child.HasExited) { child.Kill(); await child.WaitForExitAsync(CancellationToken.None); }
            await stdout; await stderr;
        }
    }

    [Fact]
    public async Task ChildEnsure()
    {
        string? input = Environment.GetEnvironmentVariable("SPLINTER_TEST_PREPARATION_INPUT");
        if (input is null) return; // Subprocess-only fixture entrypoint.
        var ct = TestContext.Current.CancellationToken;
        var r = JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync(input, ct));
        await Ensure(r.GetProperty("project").GetString()!, r.GetProperty("home").GetString()!,
            new DevelopmentPreparationProbe(r.GetProperty("boundary").GetString()!, r.GetProperty("signalPath").GetString()!), ct);
    }

    [Fact]
    public void ProbeCatalogAndSchemaAgreeAndRejectUnknownInputs()
    {
        var contracts = new ContractCatalog();
        var schema = contracts.GetSchema("urn:splinter:v1:preparation-probe-request");
        Assert.Equal(DevelopmentPreparationProbe.Boundaries, schema.GetProperty("properties").GetProperty("boundary").GetProperty("enum").EnumerateArray().Select(e => e.GetString()));
        Assert.False(contracts.Validate("urn:splinter:v1:preparation-probe-request", JsonSerializer.SerializeToElement(new { version = 0 })).IsValid);
    }

    private static Task<WorkspaceView> Ensure(string project, string home, IWorkspacePreparationObserver? observer, CancellationToken ct) =>
        new WorkspaceCoordinator(new(home), new Source(), new App(), snapshots: new Backend(), preparationObserver: observer)
            .EnsureAsync(project, "interrupted", "base", "ntfs-snapshot", false, ct);

    private sealed class Backend : ISnapshotWorkspaceBackend
    {
        public Task<SnapshotReference> CaptureAsync(WorkspaceRecord w, string c, CancellationToken ct = default)
        {
            string path = Path.Combine(c, "capture.json");
            if (!File.Exists(path))
            {
                var reference = new SnapshotReference("snapshot-" + w.Id, "fake", w.ProjectPath, "immutable", DateTimeOffset.UtcNow, true, true, "fake");
                File.WriteAllText(path, JsonSerializer.Serialize(reference));
                File.WriteAllText(Path.Combine(c, "capture-created-" + Guid.NewGuid().ToString("N")), "");
            }
            return Task.FromResult(JsonSerializer.Deserialize<SnapshotReference>(File.ReadAllText(path))!);
        }
        public Task VerifyConnectionAsync(WorkspaceRecord w, string c, CancellationToken ct = default) => Task.CompletedTask;
        public Task<OverlayMount> ConnectAsync(WorkspaceRecord w, string c, CancellationToken ct = default)
        { Directory.CreateDirectory(w.Path); return Task.FromResult(new OverlayMount(w.Id, w.Path, "delta", w.Snapshot!.Reference!, 1, 1)); }
        public Task<SnapshotWorkspaceObservation> ObserveAsync(WorkspaceRecord w, string c, CancellationToken ct = default) =>
            Task.FromResult(new SnapshotWorkspaceObservation(true, Directory.Exists(w.Path), Directory.Exists(w.Path), true, 0, 0, 0));
        public Task<SnapshotDeltaSeal> SealAsync(WorkspaceRecord w, string c, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UnmountAsync(WorkspaceRecord w, string c, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<SnapshotJournalCompaction> CompactAsync(WorkspaceRecord w, string c, CancellationToken ct = default, long? maxEstimatedJournalOverlapBytes = null) => throw new NotSupportedException();
        public Task CompleteCompactionAsync(WorkspaceRecord w, string c, CancellationToken ct = default) => throw new NotSupportedException();
        public Task RemoveDeltaAsync(WorkspaceRecord w, string c, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<SnapshotReclamation> ReleaseSnapshotAsync(WorkspaceRecord w, string c, CancellationToken ct = default) => throw new NotSupportedException();
    }
    private sealed class Source : ISourceWorkspaceProvider, ISnapshotSourceWorkspaceProvider
    {
        public Task<string> PreflightAsync(WorkspaceRecord w, CancellationToken ct = default) => Task.FromResult("base");
        public Task RegisterAsync(WorkspaceRecord w, string c, CancellationToken ct = default)
        { if (Directory.GetFiles(c, "registration-created-*").Length == 0) File.WriteAllText(Path.Combine(c, "registration-created-" + Guid.NewGuid().ToString("N")), ""); return Task.CompletedTask; }
        public Task AttachAsync(WorkspaceRecord w, string c, CancellationToken ct = default) => Task.CompletedTask;
        public Task<SourceState> InspectAsync(WorkspaceRecord w, CancellationToken ct = default) => Task.FromResult(new SourceState("base", "fingerprint", false, []));
        public Task<string> ResolveRevisionAsync(string p, string r, CancellationToken ct = default) => Task.FromResult(r);
        public Task<string> SnapshotKeyAsync(string p, string r, string[] paths, CancellationToken ct = default) => Task.FromResult(r);
        public Task<SourceWorkspace> CreateAsync(WorkspaceRecord w, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string> PreserveAsync(WorkspaceRecord w, string c, CancellationToken ct = default) => throw new NotSupportedException();
        public Task VerifyResultAsync(WorkspaceRecord w, string c, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UnregisterAsync(WorkspaceRecord w, string c, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string> CommitAsync(WorkspaceRecord w, string m, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IntegrationResult> IntegrateAsync(WorkspaceRecord w, string t, CancellationToken ct = default) => throw new NotSupportedException();
        public Task ReleaseAsync(WorkspaceRecord w, CancellationToken ct = default) => throw new NotSupportedException();
    }
    private sealed class App : IApplicationWorkspaceProvider
    {
        public string[] ManagedPaths => []; public string[] CacheInputs => [];
        public string CompatibilityKey(string p) => "fake";
        public Task<ApplicationState> ObserveAsync(WorkspaceRecord w, string c, CancellationToken ct = default) => Task.FromResult(new ApplicationState(false, false, "stopped", Resources: new(true, false, 0, 0, 0)));
        public Task<ApplicationLease> StartAsync(WorkspaceRecord w, string c, CancellationToken ct = default) => throw new NotSupportedException();
        public Task StopAsync(WorkspaceRecord w, string c, CancellationToken ct = default) => throw new NotSupportedException();
        public Task RemoveInfrastructureAsync(WorkspaceRecord w, string c, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
