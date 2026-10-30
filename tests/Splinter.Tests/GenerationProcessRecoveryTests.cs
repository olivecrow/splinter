using System.Diagnostics;
using System.Text.Json;
using Splinter.Cli;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;
using Splinter.SourceControl.Files;

namespace Splinter.Tests;

public sealed partial class FileWorkspaceTests
{
    private sealed record GenerationProcessRequest(string Home, string Id, string Boundary, string Signal, string Nonce,
        string? ResultId = null, string? Digest = null, string[]? CliArguments = null);
    private sealed class GenerationProcessGate(GenerationProcessRequest request) : IWorkspacePreparationObserver
    {
        public Task ReachedAsync(string boundary, WorkspaceRecord workspace, CancellationToken ct)
        { Reach(boundary); return Task.CompletedTask; }
        public void Reach(string boundary)
        {
            if (boundary != request.Boundary) return;
            string pending = request.Signal + ".pending";
            using (var output = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            { JsonSerializer.Serialize(output, new { request.Nonce, boundary, Pid = Environment.ProcessId, Start = Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks }); output.Flush(true); }
            File.Move(pending, request.Signal); Thread.Sleep(Timeout.Infinite);
        }
    }
    [Fact]
    public async Task GenerationProcessChild()
    {
        string? input = Environment.GetEnvironmentVariable("SPLINTER_GENERATION_PROCESS_CHILD");
        if (input is null) Assert.Skip("Subprocess-only immutable generation actor.");
        var request = JsonSerializer.Deserialize<GenerationProcessRequest>(File.ReadAllText(input))!;
        var gate = new GenerationProcessGate(request); var ct = TestContext.Current.CancellationToken;
        if (request.CliArguments is { } arguments)
        {
            using var output = new StringWriter();
            await Splinter.Cli.Program.RunAsync(arguments, output, ct, gate);
            throw new InvalidOperationException("CLI did not reach its requested durable generation boundary: " + output);
        }
        var store = new WorkspaceStore(request.Home); var backend = new Backend { Revisions = new() }; backend.Mounted.Add(request.Id);
        var publisher = new WindowsOriginalFilePublisher((boundary, _) => gate.Reach("original." + boundary));
        var files = new FileWorkspaceProvider(store, publisher, projectAccess: new ProjectAccess(), preparationObserver: gate);
        var coordinator = new WorkspaceCoordinator(store, new SourceWorkspaceRouter(new ForbiddenGit(), files),
            new SelectedApplicationProvider(new NoApplication()), backend, gate);
        if (request.ResultId is null) await coordinator.CheckpointAsync(request.Id, "process result", "process-capture", ct);
        else await coordinator.ApplyOriginalAsync(request.Id, request.ResultId, "process-apply", request.Digest!, null, ct);
        throw new InvalidOperationException("Generation actor did not reach its requested durable boundary.");
    }
    [Theory]
    [InlineData("source-capture-fixed")]
    [InlineData("source-payloads-preserved")]
    [InlineData("source-result-preserved")]
    [InlineData("source-generation-published")]
    [InlineData("original.published")]
    [InlineData("original.completed")]
    public async Task ForcedGenerationActorExitResumesFrozenResultAndAtomicAppliedHistory(string boundary)
    {
        RequireWindowsApply(); using var f = new Fixture(new WindowsOriginalFilePublisher()); var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure("process-recovery", ct)).Workspace;
        File.WriteAllText(Path.Combine(w.Path, "data.txt"), "frozen");
        WorkspaceResultGeneration? result = null; OriginalApplyPlan? plan = null;
        if (boundary.StartsWith("original.", StringComparison.Ordinal))
        {
            result = (await f.New().CheckpointAsync(w.Id, "process result", "process-capture", ct)).ResultGeneration!;
            plan = (await f.New().PlanOriginalAsync(w.Id, result.Id, null, ct)).OriginalApplyPlan!;
        }
        string control = f.Store.ControlPath(w.Id), input = Path.Combine(control, "process-request.json");
        var request = new GenerationProcessRequest(f.Store.Root, w.Id, boundary, Path.Combine(control, "process-signal.json"), Guid.NewGuid().ToString("N"), result?.Id, plan?.Digest);
        File.WriteAllText(input, JsonSerializer.Serialize(request));
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string arg in new[] { typeof(FileWorkspaceTests).Assembly.Location, "-method", "Splinter.Tests.FileWorkspaceTests.GenerationProcessChild" }) start.ArgumentList.Add(arg);
        start.Environment["SPLINTER_GENERATION_PROCESS_CHILD"] = input;
        using var child = Process.Start(start)!; long ticks = child.StartTime.ToUniversalTime().Ticks;
        var stdout = child.StandardOutput.ReadToEndAsync(ct); var stderr = child.StandardError.ReadToEndAsync(ct);
        try
        {
            long began = Stopwatch.GetTimestamp();
            while (!File.Exists(request.Signal) && !child.HasExited && Stopwatch.GetElapsedTime(began).TotalSeconds < 25) await Task.Delay(20, ct);
            Assert.True(File.Exists(request.Signal), child.HasExited ? await stdout + await stderr : "Missing generation signal.");
            var observed = await WindowsProcessWorkspaceProvider.ReadAsync<JsonElement>(request.Signal, ct);
            Assert.Equal(request.Nonce, observed.GetProperty("Nonce").GetString()); Assert.Equal(child.Id, observed.GetProperty("Pid").GetInt32());
            Assert.Equal(ticks, observed.GetProperty("Start").GetInt64()); Assert.Equal(ticks, child.StartTime.ToUniversalTime().Ticks);
            child.Kill(); await child.WaitForExitAsync(ct);
            File.WriteAllText(Path.Combine(w.Path, "data.txt"), "later worker");
            result ??= (await f.New().CheckpointAsync(w.Id, "process result", "process-capture", ct)).ResultGeneration!;
            plan ??= (await f.New().PlanOriginalAsync(w.Id, result.Id, null, ct)).OriginalApplyPlan!;
            var applied = await f.New().ApplyOriginalAsync(w.Id, result.Id, "process-apply", plan.Digest, null, ct);
            Assert.Equal("frozen", File.ReadAllText(Path.Combine(f.Project, "data.txt")));
            Assert.Equal("later worker", File.ReadAllText(Path.Combine(w.Path, "data.txt")));
            Assert.Single(applied.Workspace.Results); Assert.Single(applied.Workspace.OriginalApplies);
            File.WriteAllText(Path.Combine(f.Project, "data.txt"), "later original");
            await f.New().ApplyOriginalAsync(w.Id, result.Id, "process-apply", plan.Digest, null, ct);
            Assert.Equal("later original", File.ReadAllText(Path.Combine(f.Project, "data.txt")));
        }
        finally
        {
            if (!child.HasExited && child.StartTime.ToUniversalTime().Ticks == ticks) { child.Kill(); await child.WaitForExitAsync(CancellationToken.None); }
            File.WriteAllText(Path.Combine(control, "process-stdout.log"), await stdout); File.WriteAllText(Path.Combine(control, "process-stderr.log"), await stderr);
        }
    }
}
