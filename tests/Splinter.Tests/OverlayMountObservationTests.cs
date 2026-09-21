using System.Diagnostics;
using System.Text.Json;
using Splinter.Core;
using Splinter.Platform.Windows;

namespace Splinter.Tests;

public sealed class OverlayMountObservationTests
{
    [Fact]
    public async Task MissingStateAfterLaunchIntentIsUnknownNotZero()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows process observation.");
        using var d = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        var snapshot = new SnapshotReference("snapshot", "windows-vss-system", d.Path, Path.Combine(d.Path, "base"), DateTimeOffset.UtcNow, true, true, "test");
        var request = new OverlayServeRequest(new(1, "overlay", snapshot, 65536, 4096), Path.Combine(d.Path, "delta"), Path.Combine(d.Path, "mount"), d.Path, "token");
        string requestPath = Path.Combine(d.Path, "request.json"); var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request, json), ct);
        await File.WriteAllTextAsync(Path.Combine(d.Path, "provider-launch-intent.json"),
            JsonSerializer.Serialize(new { request.Token, id = "overlay", generation = "unreconciled", requestedAt = DateTimeOffset.UtcNow }, json), ct);
        var observed = await new WinFspOverlayMountProvider(requestPath, d.Path).ObserveAsync(new("overlay", request.MountPath, request.DeltaPath, snapshot, 0, 0), ct);
        Assert.False(observed.OwnershipVerified); Assert.Null(observed.OpenHandles); Assert.Null(observed.PrivateBytes);
        Assert.Equal("OverlayStartUncertain", observed.FailureCode);
        Assert.Equal("OverlayStartUncertain", (await Assert.ThrowsAsync<WorkspaceException>(() => WinFspOverlayClient.StartAsync(requestPath, d.Path, ct))).Code);
    }

    [Fact]
    public async Task FailedProviderWithoutMemoryFieldsCannotPassReclamationGateWhileAlive()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows process observation.");
        using var directory = new TestDirectory();
        using var process = Process.GetCurrentProcess();
        var snapshot = new SnapshotReference("snapshot", "windows-vss-system", directory.Path,
            Path.Combine(directory.Path, "missing-base"), DateTimeOffset.UtcNow, true, true, "test");
        var identity = new OverlayIdentity(1, "overlay", snapshot, 65536, 4096);
        var request = new OverlayServeRequest(identity, Path.Combine(directory.Path, "delta"),
            Path.Combine(directory.Path, "mount"), directory.Path, "token");
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        string requestPath = Path.Combine(directory.Path, "request.json");
        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request, json), TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "state.json"), JsonSerializer.Serialize(new
        {
            id = identity.Id, request.Token, snapshotId = snapshot.Id, phase = "failed", error = "DeltaCorrupt",
            processId = process.Id, processStartTimeUtcTicks = process.StartTime.ToUniversalTime().Ticks
        }, json), TestContext.Current.CancellationToken);
        var mount = new OverlayMount(identity.Id, request.MountPath, request.DeltaPath, snapshot,
            process.Id, process.StartTime.ToUniversalTime().Ticks);
        var provider = new WinFspOverlayMountProvider(requestPath, directory.Path);
        var observed = await provider.ObserveAsync(mount, TestContext.Current.CancellationToken);
        Assert.False(observed.Mounted);
        Assert.True(observed.PrivateBytes > 0);
        Assert.Equal("DeltaCorrupt", observed.FailureCode);
        var storage = new OverlayStorageObservation(identity.Id, snapshot.Id, 0, 0, 0, 0, 0, 0, 4096, 0, 0);
        var result = new OverlayResultReceipt(identity.Id, snapshot.Id, 0, "result", "hash", true);
        Assert.Equal("OverlayCleanupIncomplete", Assert.Throws<WorkspaceException>(() =>
            OverlayReleaseGate.RequireSafe(storage, result, observed, 0)).Code);
    }

    [Fact]
    public async Task MissingProviderStateIsExplicitAndDoesNotCreateResources()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows process observation.");
        using var directory = new TestDirectory();
        var snapshot = new SnapshotReference("snapshot", "windows-vss-system", directory.Path,
            Path.Combine(directory.Path, "missing-base"), DateTimeOffset.UtcNow, true, true, "test");
        var request = new OverlayServeRequest(new(1, "overlay", snapshot, 65536, 4096),
            Path.Combine(directory.Path, "delta"), Path.Combine(directory.Path, "mount"), directory.Path, "token");
        string requestPath = Path.Combine(directory.Path, "request.json");
        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web)), TestContext.Current.CancellationToken);
        var observed = await new WinFspOverlayMountProvider(requestPath, directory.Path).ObserveAsync(
            new("overlay", request.MountPath, request.DeltaPath, snapshot, 0, 0), TestContext.Current.CancellationToken);
        Assert.Equal("OverlayNotStarted", observed.FailureCode);
        Assert.False(observed.Mounted);
        Assert.Equal(0, observed.PrivateBytes);
        Assert.False(Directory.Exists(request.DeltaPath));
    }
}
