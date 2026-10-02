using System.Diagnostics;
using System.Text.Json;
using Splinter.Application.Unity;
using Splinter.Core;

namespace Splinter.Tests;

public sealed class UnityWorkspaceProviderTests
{
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

    [Fact]
    public async Task ProductHeartbeatDoesNotExecuteThrowingProjectCode()
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
            "-splinterToken", "test-token" })
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
        Assert.Equal(new[] { "ready", "compiling", "ready" }, heartbeats.Select(b => b.GetProperty("phase").GetString()));
        Assert.All(heartbeats, b =>
        {
            Assert.Equal(process.Id, b.GetProperty("pid").GetInt32());
            Assert.Equal("test-token", b.GetProperty("token").GetString());
            Assert.False(b.TryGetProperty("validation", out _));
        });
        Assert.True(heartbeats[0].GetProperty("ready").GetBoolean());
        Assert.False(heartbeats[1].GetProperty("ready").GetBoolean());
        Assert.True(heartbeats[2].GetProperty("ready").GetBoolean());
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
