using System.Diagnostics;
using System.Text.Json;
using Splinter.Application.Unity;
using Splinter.Core;

namespace Splinter.Tests;

public sealed class UnityWorkspaceProviderTests
{
    [Fact]
    public async Task LaunchPreparationPreservesExternalPackageRunStateAndOriginalFiles()
    {
        var ct = TestContext.Current.CancellationToken;
        using var directory = new TestDirectory();
        string root = Path.Combine(directory.Path, "worker"), original = Path.Combine(directory.Path, "original");
        string[] paths = ["Library/ilpp.pid", "Library/Pipeline/.unity-pipeline-port",
            "Library/MCPForUnity/RunState/mcp_http_8080.pid", "Library/OtherTool/server.pid", "Library/artifact.bin"];
        foreach (string project in new[] { root, original })
        {
            Directory.CreateDirectory(Path.Combine(project, "Assets"));
            Directory.CreateDirectory(Path.Combine(project, "ProjectSettings"));
            await File.WriteAllTextAsync(Path.Combine(project, "ProjectSettings", "ProjectVersion.txt"), "m_EditorVersion: 6000.3.22f1", ct);
            foreach (string relative in paths)
            {
                string path = Path.Combine(project, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllTextAsync(path, relative, ct);
            }
        }
        var workspace = new WorkspaceRecord { Id = "test", RequestKey = "test", Path = root,
            ProjectPath = original, BaseReference = "test", SourceReference = "test", StorageMode = "git-snapshot" };
        UnityWorkspaceProvider.ResetCapturedUnityProcessAddresses(workspace);
        Assert.False(File.Exists(Path.Combine(root, paths[0])));
        Assert.False(File.Exists(Path.Combine(root, paths[1])));
        foreach (string relative in paths.Skip(2))
            Assert.Equal(relative, await File.ReadAllTextAsync(Path.Combine(root, relative), ct));
        foreach (string relative in paths)
            Assert.Equal(relative, await File.ReadAllTextAsync(Path.Combine(original, relative), ct));
    }

    [Fact]
    public async Task NormalShutdownMayDrainBeyondThirtySeconds()
    {
        var clock = new ShutdownClock();
        await UnityWorkspaceProvider.WaitForNormalExitAsync(() => clock.Elapsed < TimeSpan.FromSeconds(54),
            _ => Task.FromResult<string?>(null), clock, clock.Delay, TestContext.Current.CancellationToken);
        Assert.InRange(clock.Elapsed.TotalSeconds, 54, 55);
    }

    [Fact]
    public async Task UnresponsiveEditorRemainsAnExplicitBoundedFailure()
    {
        var clock = new ShutdownClock();
        var failure = await Assert.ThrowsAsync<WorkspaceException>(() => UnityWorkspaceProvider.WaitForNormalExitAsync(
            () => true, _ => Task.FromResult<string?>(null), clock, clock.Delay, TestContext.Current.CancellationToken));
        Assert.Equal("EditorStopTimeout", failure.Code);
        Assert.InRange(clock.Elapsed.TotalSeconds, 120, 121);
    }

    [Fact]
    public async Task UnsavedStateRefusalDoesNotWaitForTheShutdownDeadline()
    {
        var clock = new ShutdownClock();
        var failure = await Assert.ThrowsAsync<WorkspaceException>(() => UnityWorkspaceProvider.WaitForNormalExitAsync(
            () => true, _ => Task.FromResult<string?>("Unsaved scene"), clock, clock.Delay, TestContext.Current.CancellationToken));
        Assert.Equal("EditorStopBlocked", failure.Code);
        Assert.Equal(TimeSpan.Zero, clock.Elapsed);
    }

    [Fact]
    public async Task ShutdownWaitHonorsCallerCancellation()
    {
        var clock = new ShutdownClock();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => UnityWorkspaceProvider.WaitForNormalExitAsync(
            () => true, _ => Task.FromResult<string?>(null), clock, async (duration, token) =>
            {
                await clock.Delay(duration, token);
                cancellation.Cancel();
            }, cancellation.Token));
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(1));
    }

    private sealed class ShutdownClock : TimeProvider
    {
        internal TimeSpan Elapsed { get; private set; }
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Elapsed.Ticks;
        internal Task Delay(TimeSpan duration, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Elapsed += duration;
            return Task.CompletedTask;
        }
    }

    [Theory]
    [InlineData("absent")]
    [InlineData("changed-hook")]
    public async Task ProductBridgeInitializesAndStopsWithoutInspectingExternalTools(string externalTool)
    {
        using var directory = new TestDirectory();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (string argument in new[] { "exec", "--runtimeconfig",
            Path.ChangeExtension(typeof(UnityWorkspaceProviderTests).Assembly.Location, ".runtimeconfig.json"),
            typeof(UnityBridgeHarness.HarnessMarker).Assembly.Location, "-splinterControlPath", directory.Path,
            "-splinterToken", "test-token", "--external-tool", externalTool })
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var error = process.StandardError.ReadToEndAsync(timeout.Token);
        try { await process.WaitForExitAsync(timeout.Token); }
        finally { if (!process.HasExited) { process.Kill(true); await process.WaitForExitAsync(CancellationToken.None); } }
        Assert.True(process.ExitCode == 0, await error);
        using var result = JsonDocument.Parse(await output);
        var root = result.RootElement;
        Assert.Equal(0, root.GetProperty("fixtureCalls").GetInt32());
        Assert.Empty(root.GetProperty("errors").EnumerateArray());
        var heartbeats = root.GetProperty("heartbeats").EnumerateArray().ToArray();
        Assert.Equal(new[] { "ready", "compiling", "ready", "exiting" }, heartbeats.Select(b => b.GetProperty("phase").GetString()));
        Assert.All(heartbeats, b =>
        {
            Assert.Equal(process.Id, b.GetProperty("pid").GetInt32());
            Assert.Equal("test-token", b.GetProperty("token").GetString());
            Assert.False(b.TryGetProperty("validation", out _));
            Assert.False(b.TryGetProperty("coplayShutdownGuard", out _));
            Assert.Equal(directory.Path, b.GetProperty("projectPath").GetString());
            Assert.True(b.GetProperty("startTimeUtcTicks").GetInt64() > 0);
        });
        Assert.True(heartbeats[0].GetProperty("ready").GetBoolean());
        Assert.False(heartbeats[1].GetProperty("ready").GetBoolean());
        Assert.True(heartbeats[2].GetProperty("ready").GetBoolean());
        Assert.False(heartbeats[3].GetProperty("ready").GetBoolean());
        Assert.Equal(0, root.GetProperty("externalLookups").GetInt32());
        Assert.Equal(0, root.GetProperty("quitCallsBeforeStop").GetInt32());
        Assert.Equal(1, root.GetProperty("externalQuitCalls").GetInt32());
        Assert.True(root.GetProperty("exited").GetBoolean());
        Assert.True(root.GetProperty("wrongTokenBlocked").GetBoolean());
        Assert.True(root.GetProperty("compilingBlocked").GetBoolean());
        Assert.True(root.GetProperty("importingBlocked").GetBoolean());
        Assert.Contains("Stop Play", root.GetProperty("playRefusal").GetString());
        Assert.Equal("Unsaved scene: Assets/Unsaved.unity", root.GetProperty("sceneRefusal").GetString());
    }

    [Fact]
    public async Task CompatibilityIgnoresCheckoutLineEndings()
    {
        var ct = TestContext.Current.CancellationToken; using var directory = new TestDirectory();
        string settings = Path.Combine(directory.Path, "ProjectSettings"); Directory.CreateDirectory(settings);
        string version = Path.Combine(settings, "ProjectVersion.txt");
        await File.WriteAllTextAsync(version, "m_EditorVersion: 6000.3.22f1\r\nm_EditorVersionWithRevision: 6000.3.22f1 (id)\r\n", ct);
        var provider = new UnityWorkspaceProvider(); string original = provider.CompatibilityKey(directory.Path);
        await File.WriteAllTextAsync(version, "m_EditorVersion: 6000.3.22f1\nm_EditorVersionWithRevision: 6000.3.22f1 (id)\n", ct);
        Assert.Equal(original, provider.CompatibilityKey(directory.Path));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HeartbeatIgnoresEarlierCompileEvidence(bool completed)
    {
        var ct = TestContext.Current.CancellationToken;
        using var directory = new TestDirectory();
        using var process = Process.GetCurrentProcess();
        var lease = new ApplicationLease(process.Id, process.StartTime.ToUniversalTime().Ticks, "test-token", process.MainModule!.FileName!);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "launch-receipt.json"), JsonSerializer.Serialize(lease, options), ct);
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "heartbeat.json"), JsonSerializer.Serialize(new
        {
            token = lease.Token, pid = lease.ProcessId, startTimeUtcTicks = lease.StartTimeUtcTicks,
            projectPath = directory.Path, phase = "ready", ready = true, observedAt = DateTimeOffset.UtcNow,
            validation = new { requestId = completed ? "request" : "", fingerprint = completed ? "hash" : "",
                success = completed, completedAt = completed ? DateTimeOffset.UtcNow.ToString("O") : "", errors = Array.Empty<string>() }
        }, options), ct);
        var record = new WorkspaceRecord { Id = "test", RequestKey = "test", ProjectPath = directory.Path,
            Path = directory.Path, SourceReference = "test", BaseReference = "test" };
        var state = await new UnityWorkspaceProvider().ObserveAsync(record, directory.Path, ct);
        Assert.True(state.Running);
        Assert.True(state.Ready);
        Assert.False(JsonSerializer.SerializeToElement(state, options).TryGetProperty("validation", out _));
    }
}
