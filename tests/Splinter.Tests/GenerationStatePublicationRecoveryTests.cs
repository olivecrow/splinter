using System.Text.Json;
using Splinter.Application.Unity;
using Splinter.Cli;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Protocol;

namespace Splinter.Tests;

public sealed partial class FileWorkspaceTests
{
    [Fact]
    public async Task AbnormalEditorStopPreservesTheCheckpointUntilExplicitFinalRecovery()
    {
        RequireWindowsApply();
        var ct = TestContext.Current.CancellationToken;
        const int accessViolation = unchecked((int)0xC0000005);
        var processes = new WindowsProcessWorkspaceProvider();
        var unity = new UnityWorkspaceProvider(processes);
        using var f = new Fixture(new WindowsOriginalFilePublisher(), application: unity, policies: UnityProjectPolicy.Policies);
        string settings = Directory.CreateDirectory(Path.Combine(f.Project, "ProjectSettings")).FullName;
        await File.WriteAllTextAsync(Path.Combine(settings, "ProjectVersion.txt"), "m_EditorVersion: 6000.6.8f1", ct);
        var w = (await f.Ensure("abnormal-stop", ct, "unity")).Workspace;
        await File.WriteAllTextAsync(Path.Combine(w.Path, "data.txt"), "verified bytes", ct);
        var previous = (await f.New().CheckpointAsync(w.Id, "verified", "before-stop", ct)).ResultGeneration!;
        string control = f.Store.ControlPath(w.Id);
        string shell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        string script = Path.Combine(control, "wait-for-stop.ps1");
        await File.WriteAllTextAsync(script, $$"""
            $clock = [Diagnostics.Stopwatch]::StartNew()
            while ($clock.Elapsed.TotalSeconds -lt 25) {
                if (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'stop-request.json')) { exit {{accessViolation}} }
                if (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'finish-test')) { exit 0 }
                Start-Sleep -Milliseconds 50
            }
            exit 24
            """, ct);
        var lease = await processes.StartAsync(new(shell, ["-NoProfile", "-File", script], w.Path,
            Guid.NewGuid().ToString("N")), control, ct);
        try
        {
            var error = await Assert.ThrowsAsync<WorkspaceException>(() =>
                f.New().CheckpointAndReleaseAsync(w.Id, "final", "final-stop", ct));
            Assert.Equal("EditorExitedAbnormally", error.Code);
            Assert.Contains("0xC0000005", error.Message);
            Assert.Equal(accessViolation, JsonSerializer.SerializeToElement(error.Details).GetProperty("rootExitCode").GetInt32());
            Assert.Equal("requires-user-action", WorkspaceFailurePolicy.RetryDisposition(error.Code));
            var stopped = await unity.ObserveAsync(w, control, ct);
            Assert.Equal("exited-abnormally", stopped.Phase);
            Assert.Equal(new ProcessResources(true, false, 0, 0, 0, accessViolation), stopped.Resources);
            Assert.Contains(w.Id, f.Backend.Mounted); Assert.Empty(f.Backend.Removed);
            Assert.Equal(previous.Id, Assert.Single(f.Store.Get(w.Id).Results).Id);
            Assert.Equal("verified bytes", await File.ReadAllTextAsync(Path.Combine(w.Path, "data.txt"), ct));
            string request = await File.ReadAllTextAsync(Path.Combine(control, "stop-request.json"), ct);

            var recovered = await f.New().CheckpointAndReleaseAsync(w.Id, "final", "final-stop", ct);
            Assert.Equal("released", recovered.Workspace.Phase);
            Assert.Equal(accessViolation, recovered.Workspace.ReleasedResources!.RootExitCode);
            Assert.Contains(recovered.Workspace.Results, generation => generation.Id == previous.Id);
            Assert.Equal(request, await File.ReadAllTextAsync(Path.Combine(control, "stop-request.json"), ct));
            var response = WorkspaceCommands.Result("workspace.checkpoint", recovered, false, f.Store.Root);
            var json = JsonSerializer.SerializeToElement(response, ApiJson.Options);
            Assert.True(new ContractCatalog().Validate("urn:splinter:v1:response", json).IsValid, json.GetRawText());
        }
        finally
        {
            await File.WriteAllTextAsync(Path.Combine(control, "finish-test"), "", CancellationToken.None);
            await processes.WaitForExitAsync(lease, CancellationToken.None);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task GenerationStatePublicationFailureRecoversTheFixedResultBeforeOrAfterAtomicStatePublication(bool failEventAppend)
    {
        RequireWindowsApply(); using var f = new Fixture(new WindowsOriginalFilePublisher()); var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure("generation-state-space", ct)).Workspace;
        File.WriteAllText(Path.Combine(w.Path, "data.txt"), "fixed source");
        string control = f.Store.ControlPath(w.Id), target = Path.Combine(control, failEventAppend ? "events.jsonl" : "workspace.json");
        FileStream? held = null; bool armed = true;
        var observer = new GenerationBoundary((point, _) =>
        {
            if (!armed || point != "source-result-preserved") return;
            armed = false; held = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read);
        });
        try
        {
            var error = await Assert.ThrowsAsync<WorkspaceException>(() => f.New(observer).CheckpointAsync(w.Id, "fixed", "state-result", ct));
            Assert.Equal("StateWriteIncomplete", error.Code);
            var details = JsonSerializer.SerializeToElement(error.Details, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (details.TryGetProperty("details", out var nested)) details = nested;
            Assert.Equal(failEventAppend ? "append-event" : "publish-state", details.GetProperty("diagnostic").GetProperty("stage").GetString());
            Assert.Equal(failEventAppend ? "completed" : "unknown", details.GetProperty("diagnostic").GetProperty("statePublication").GetString());
            var saved = f.Store.Get(w.Id); Assert.Equal(4, saved.Version);
            Assert.Equal(failEventAppend ? 1 : 0, saved.Results.Count);
            if (!failEventAppend) Assert.NotNull(saved.CaptureIntent);
            Assert.True(Directory.Exists(w.Path)); Assert.Empty(f.Backend.Removed); Assert.Empty(f.Backend.Released);
        }
        finally { held?.Dispose(); }
        File.WriteAllText(Path.Combine(w.Path, "data.txt"), "later worker edit");
        var recovered = await f.New().CheckpointAsync(w.Id, "fixed", "state-result", ct);
        Assert.Single(recovered.Workspace.Results); Assert.Equal(1, recovered.ResultGeneration!.Sequence);
        var plan = (await f.New().PlanOriginalAsync(w.Id, recovered.ResultGeneration.Id, null, ct)).OriginalApplyPlan!;
        Assert.Equal(3, plan.Version); Assert.NotNull(plan.ChangesRoot);
        await f.New().ApplyOriginalAsync(w.Id, recovered.ResultGeneration.Id, "state-apply", plan.Digest, null, ct);
        Assert.Equal("fixed source", File.ReadAllText(Path.Combine(f.Project, "data.txt")));
        Assert.Equal("later worker edit", File.ReadAllText(Path.Combine(w.Path, "data.txt")));
    }
    [Theory]
    [InlineData("source-payloads-preserved")]
    [InlineData("source-result-manifest-published")]
    [InlineData("source-generation-receipt-published")]
    public async Task GenerationSpaceFailureAtPreservationBoundariesRetainsOneRequestAndSeparatesLaterEditing(string boundary)
    {
        RequireWindowsApply(); using var f = new Fixture(new WindowsOriginalFilePublisher()); var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure("generation-payload-space", ct)).Workspace;
        File.WriteAllText(Path.Combine(w.Path, "data.txt"), "fixed source"); bool armed = true;
        var observer = new GenerationBoundary((point, _) =>
        { if (armed && point == boundary) { armed = false; throw new IOException("Injected preservation exhaustion.", unchecked((int)0x80070070)); } });
        Assert.Equal(unchecked((int)0x80070070), (await Assert.ThrowsAsync<IOException>(() => f.New(observer).CheckpointAsync(w.Id, "fixed", "space-result", ct))).HResult);
        var pending = f.Store.Get(w.Id); Assert.NotNull(pending.CaptureIntent); Assert.Empty(pending.Results);
        Assert.Equal("SourceCapturePending", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().CheckpointAsync(w.Id, "new", "new-key", ct))).Code);
        File.WriteAllText(Path.Combine(w.Path, "data.txt"), "later worker edit");
        var recovered = await f.New().CheckpointAsync(w.Id, "fixed", "space-result", ct);
        Assert.Single(recovered.Workspace.Results); Assert.Equal(pending.CaptureIntent.Id, recovered.ResultGeneration!.Id);
        var plan = (await f.New().PlanOriginalAsync(w.Id, recovered.ResultGeneration.Id, null, ct)).OriginalApplyPlan!;
        await f.New().ApplyOriginalAsync(w.Id, recovered.ResultGeneration.Id, "space-apply", plan.Digest, null, ct);
        Assert.Equal("fixed source", File.ReadAllText(Path.Combine(f.Project, "data.txt")));
        Assert.Equal("later worker edit", File.ReadAllText(Path.Combine(w.Path, "data.txt")));
    }
}
