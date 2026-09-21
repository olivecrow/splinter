using System.Diagnostics;
using System.Text.Json;
using Splinter.Application.Unity;
using Splinter.Core;

namespace Splinter.Tests;

public sealed class UnityWorkspaceProviderTests
{
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
    public async Task HeartbeatHandlesUnityDefaultNestedObjectAndRealValidation(bool completed)
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
        if (completed) Assert.True(state.Validation!.Success);
        else Assert.Null(state.Validation);
    }
}
