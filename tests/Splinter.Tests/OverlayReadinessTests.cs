using System.Diagnostics;
using System.Text.Json;
using Splinter.Core;
using Splinter.Platform.Windows;

namespace Splinter.Tests;

public sealed class OverlayReadinessTests
{
    private static JsonElement State(string phase, bool mounted = false, bool alive = true,
        long ticks = 27, DateTimeOffset? at = null) => JsonSerializer.SerializeToElement(new
        {
            phase, processAlive = alive, mountPresent = mounted,
            state = new { phase, processId = 19, processStartTimeUtcTicks = ticks,
                observedAt = at ?? DateTimeOffset.UtcNow, error = "DeltaCorrupt" }
        });

    [Fact]
    public async Task LiveProcessAndReplayAreNotReadyWithoutFreshMountedState()
    {
        int calls = 0;
        Task<JsonElement> Observe() => Task.FromResult(++calls switch
        {
            1 => JsonSerializer.SerializeToElement(new { phase = "starting", processAlive = true, ownershipVerified = true }),
            2 => State("replaying"),
            3 => State("mounted", mounted: false),
            4 => State("mounted", mounted: true, at: DateTimeOffset.UtcNow.AddMinutes(-1)),
            _ => State("mounted", mounted: true)
        });
        var result = await WinFspOverlayClient.AwaitMountedAsync(Observe, 19, 27, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(5, calls); Assert.True(result.GetProperty("mountPresent").GetBoolean());
    }

    [Theory]
    [InlineData("failed", true, 27, "DeltaCorrupt")]
    [InlineData("replaying", false, 27, "OverlayStartFailed")]
    [InlineData("mounted", true, 28, "OverlayStartUncertain")]
    [InlineData("replaying", true, 27, "OverlayStartUncertain")]
    public async Task FailureExitWrongGenerationAndTimeoutRemainExplicit(string phase, bool alive, long ticks, string expected)
    {
        var error = await Assert.ThrowsAsync<WorkspaceException>(() => WinFspOverlayClient.AwaitMountedAsync(
            () => Task.FromResult(State(phase, alive: alive, ticks: ticks)), 19, 27, TimeSpan.Zero, TestContext.Current.CancellationToken));
        Assert.Equal(expected, error.Code);
    }

    [Fact]
    public async Task CancelledWaitDoesNotObserveOrStartAnotherProvider()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel(); int observations = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => WinFspOverlayClient.AwaitMountedAsync(
            () => { observations++; return Task.FromResult(State("replaying")); }, 19, 27, TimeSpan.FromMinutes(2), cancellation.Token));
        Assert.Equal(0, observations);
    }

    [Fact]
    public async Task MatchedLiveLaunchWithPreviousGenerationStateIsStartingAndNotReclaimable()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows process identity.");
        using var d = new TestDirectory(); using var process = Process.GetCurrentProcess();
        var snapshot = new SnapshotReference("snapshot", "windows-vss-system", d.Path, Path.Combine(d.Path, "base"), DateTimeOffset.UtcNow, true, true, "test");
        var request = new OverlayServeRequest(new(1, "overlay", snapshot, 65536, 4096), Path.Combine(d.Path, "delta"), Path.Combine(d.Path, "mount"), d.Path, "token",
            new(snapshot.Id, snapshot.RootPath, "verified"), Path.Combine(d.Path, "receipt"), "owner");
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web); var ct = TestContext.Current.CancellationToken;
        await File.WriteAllTextAsync(Path.Combine(d.Path, "provider-launch-intent.json"), JsonSerializer.Serialize(new { request.Token, id = "overlay", generation = "new" }, json), ct);
        await File.WriteAllTextAsync(Path.Combine(d.Path, "provider-launch-receipt.json"), JsonSerializer.Serialize(new { request.Token, processId = process.Id, startTimeUtcTicks = process.StartTime.ToUniversalTime().Ticks, generation = "new" }, json), ct);
        await File.WriteAllTextAsync(Path.Combine(d.Path, "state.json"), JsonSerializer.Serialize(new { request.Token, id = "overlay", snapshotId = snapshot.Id, phase = "unmounted", processId = process.Id, processStartTimeUtcTicks = process.StartTime.ToUniversalTime().Ticks - 1 }, json), ct);
        var observed = await WinFspOverlayClient.ObserveAsync(request, ct);
        Assert.Equal("starting", observed.GetProperty("phase").GetString());
        Assert.True(observed.GetProperty("processAlive").GetBoolean());
        Assert.True(observed.GetProperty("ownershipVerified").GetBoolean());
        Assert.False(observed.TryGetProperty("state", out _));
    }
}
