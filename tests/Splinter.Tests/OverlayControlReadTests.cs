using System.Text.Json;
using Splinter.Core;
using Splinter.Platform.Windows;

namespace Splinter.Tests;

// Real Windows control-file/client integration using synthetic process and
// snapshot records. These tests do not mount WinFsp or qualify VSS/Unity.
[Collection("Windows control-file timing")]
public sealed class OverlayControlReadTests
{
    [Fact(Timeout = 10000)]
    public async Task InitialConnectionProvesAbsenceWithoutOrdinaryPublicationGrace()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows publication grace.");
        using var d = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        var request = Request(d.Path);
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        var initial = await WinFspOverlayClient.ObserveForConnectionAsync(request, ct);
        double initialSeconds = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalSeconds;
        started = System.Diagnostics.Stopwatch.GetTimestamp();
        var ordinary = await WinFspOverlayClient.ObserveAsync(request, ct);
        double ordinarySeconds = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalSeconds;
        Assert.Equal("not-started", initial.GetProperty("phase").GetString());
        Assert.Equal(initial.GetRawText(), ordinary.GetRawText());
        Assert.InRange(initialSeconds, 0, 0.8); Assert.InRange(ordinarySeconds, 1.8, 5);
        Assert.False(Directory.Exists(request.DeltaPath));
        Assert.False(File.Exists(Path.Combine(d.Path, "provider-launch-intent.json")));
        TestContext.Current.TestOutputHelper!.WriteLine($"Initial connection observation: ordinary={ordinarySeconds:F6}s; locked={initialSeconds:F6}s");
    }

    [Fact(Timeout = 10000)]
    public async Task InitialConnectionCannotClaimAbsenceWhileAnotherLauncherOwnsTheLock()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows publication grace.");
        using var d = new TestDirectory();
        using var held = new FileStream(Path.Combine(d.Path, "client.lock"), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        stop.CancelAfter(100);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => WinFspOverlayClient.ObserveForConnectionAsync(Request(d.Path), stop.Token));
        Assert.False(File.Exists(Path.Combine(d.Path, "provider-launch-intent.json")));
    }

    [Fact(Timeout = 10000)]
    public async Task InitialConnectionPreservesAnUnresolvedLaunchIntent()
    {
        using var d = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        var request = Request(d.Path); string path = Path.Combine(d.Path, "provider-launch-intent.json");
        await WindowsProcessWorkspaceProvider.WriteAsync(path, new { request.Token, request.Identity.Id, generation = "unresolved" }, ct);
        byte[] before = await File.ReadAllBytesAsync(path, ct);
        var observed = await WinFspOverlayClient.ObserveForConnectionAsync(request, ct);
        Assert.Equal("launch-uncertain", observed.GetProperty("phase").GetString());
        Assert.False(observed.GetProperty("ownershipVerified").GetBoolean());
        Assert.Equal(before, await File.ReadAllBytesAsync(path, ct));
        Assert.False(Directory.Exists(request.DeltaPath));
    }

    [Fact]
    public async Task FirstStartUnderClientLockDoesNotWaitForNonexistentLaunchRecords()
    {
        using var d = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        // Deliberately invalid proof stops before any native launch/effect.
        var request = Request(d.Path) with { SnapshotReceiptDirectory = null! };
        string path = Path.Combine(d.Path, "request.json");
        await WindowsProcessWorkspaceProvider.WriteAsync(path, request, ct);
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        var error = await Assert.ThrowsAsync<WorkspaceException>(() => WinFspOverlayClient.StartAsync(path, Path.Combine(d.Path, "runners"), ct));
        Assert.Equal("SnapshotVerificationRequired", error.Code);
        Assert.True(System.Diagnostics.Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(1));
        Assert.False(Directory.Exists(request.DeltaPath));
        Assert.False(File.Exists(Path.Combine(d.Path, "provider-launch-intent.json")));
    }

    [Fact]
    public async Task LockedStartStillRefusesUnresolvedLaunchAndPreservesReceipt()
    {
        using var d = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        var request = Request(d.Path) with { SnapshotReceiptDirectory = null! };
        string path = Path.Combine(d.Path, "request.json"), intent = Path.Combine(d.Path, "provider-launch-intent.json");
        await WindowsProcessWorkspaceProvider.WriteAsync(path, request, ct);
        await WindowsProcessWorkspaceProvider.WriteAsync(intent, new { request.Token, request.Identity.Id, generation = "unresolved" }, ct);
        byte[] original = await File.ReadAllBytesAsync(intent, ct);
        var error = await Assert.ThrowsAsync<WorkspaceException>(() => WinFspOverlayClient.StartAsync(path, Path.Combine(d.Path, "runners"), ct));
        Assert.Equal("OverlayStartUncertain", error.Code);
        Assert.Equal(original, await File.ReadAllBytesAsync(intent, ct));
        Assert.False(Directory.Exists(request.DeltaPath));
    }

    [Fact]
    public async Task ChildReceiptPublishedDuringLockedObservationRemainsRecoverableWithoutRelaunch()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows publication grace.");
        using var d = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        var request = Request(d.Path) with { SnapshotReceiptDirectory = null! };
        string path = Path.Combine(d.Path, "request.json"), receipt = Path.Combine(d.Path, "provider-launch-receipt.json");
        await WindowsProcessWorkspaceProvider.WriteAsync(path, request, ct);
        await WindowsProcessWorkspaceProvider.WriteAsync(Path.Combine(d.Path, "provider-launch-intent.json"),
            new { request.Token, request.Identity.Id, generation = "recovering" }, ct);
        var pending = WinFspOverlayClient.StartAsync(path, Path.Combine(d.Path, "runners"), ct);
        await Task.Delay(100, ct);
        // A child can finish its receipt after the client has read its absence.
        await WindowsProcessWorkspaceProvider.WriteAsync(receipt,
            new { request.Token, processId = int.MaxValue, startTimeUtcTicks = 1L, generation = "recovering" }, ct);
        byte[] original = await File.ReadAllBytesAsync(receipt, ct);
        Assert.Equal("OverlayStartUncertain", (await Assert.ThrowsAsync<WorkspaceException>(() => pending)).Code);
        Assert.False(Directory.Exists(request.DeltaPath));
        // Same-request retry observes the matching receipt and reaches basis
        // verification, which this synthetic request deliberately cannot pass.
        Assert.Equal("SnapshotVerificationRequired", (await Assert.ThrowsAsync<WorkspaceException>(() =>
            WinFspOverlayClient.StartAsync(path, Path.Combine(d.Path, "runners"), ct))).Code);
        Assert.Equal(original, await File.ReadAllBytesAsync(receipt, ct));
        Assert.False(Directory.Exists(request.DeltaPath));
    }

    [Fact]
    public async Task BlockedFinalStatePreservesJournalAndAllowsTheSameStopRequest()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows file sharing/process identity.");
        using var d = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        var request = Request(d.Path);
        await Publish(request, ct);
        Directory.CreateDirectory(request.DeltaPath);
        string journal = Path.Combine(request.DeltaPath, "retained-fixture.journal");
        byte[] original = [1, 2, 3, 7, 11]; await File.WriteAllBytesAsync(journal, original, ct);
        string state = Path.Combine(d.Path, "state.json");
        byte[] receipt = await File.ReadAllBytesAsync(state, ct);
        using (var held = new FileStream(state, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var error = await Assert.ThrowsAsync<IOException>(() => WinFspOverlayClient.StopAsync(request, ct));
            Assert.Equal(32, error.HResult & 0xffff);
            Assert.Equal(original, await File.ReadAllBytesAsync(journal, ct));
        }
        var stopped = await WinFspOverlayClient.StopAsync(request, ct);
        Assert.Equal("unmounted", stopped.GetProperty("phase").GetString());
        Assert.False(stopped.GetProperty("processAlive").GetBoolean());
        Assert.False(stopped.GetProperty("mountPresent").GetBoolean());
        Assert.True(stopped.GetProperty("ownershipVerified").GetBoolean());
        Assert.Equal(JsonValueKind.Null, stopped.GetProperty("snapshotAvailable").ValueKind);
        Assert.Equal(original, await File.ReadAllBytesAsync(journal, ct));
        Assert.Equal(receipt, await File.ReadAllBytesAsync(state, ct));
        using var stop = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(d.Path, "stop.json"), ct));
        Assert.Equal(request.Token, stop.RootElement.GetProperty("token").GetString());
        Assert.Equal("unmounted", (await WinFspOverlayClient.StopAsync(request, ct)).GetProperty("phase").GetString());
    }

    [Fact]
    public async Task WrongOwnerReceiptIsNotRetriedAsAReadRace()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows process identity.");
        using var d = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        var request = Request(d.Path);
        await Publish(request, ct);
        string path = Path.Combine(d.Path, "state.json");
        await WindowsProcessWorkspaceProvider.WriteAsync(path, new { token = "foreign", id = request.Identity.Id,
            snapshotId = request.Identity.Snapshot.Id, phase = "unmounted" }, ct);
        byte[] original = await File.ReadAllBytesAsync(path, ct);
        var error = await Assert.ThrowsAsync<WorkspaceException>(() => WinFspOverlayClient.ObserveAsync(request, ct));
        Assert.Equal("OverlayOwnerMismatch", error.Code);
        Assert.Equal(original, await File.ReadAllBytesAsync(path, ct));
        Assert.False(File.Exists(Path.Combine(d.Path, "stop.json")));
    }

    [Fact]
    public async Task MissingReceiptWithUnresolvedLaunchDoesNotBecomeZeroResources()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows process identity.");
        using var d = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        var request = Request(d.Path);
        await WindowsProcessWorkspaceProvider.WriteAsync(Path.Combine(d.Path, "provider-launch-intent.json"),
            new { request.Token, request.Identity.Id, generation = "unresolved" }, ct);
        var observed = await WinFspOverlayClient.ObserveAsync(request, ct);
        Assert.Equal("launch-uncertain", observed.GetProperty("phase").GetString());
        Assert.False(observed.GetProperty("ownershipVerified").GetBoolean());
        Assert.False(observed.TryGetProperty("state", out _));
        Assert.False(observed.TryGetProperty("mountPresent", out _));
        Assert.False(observed.TryGetProperty("snapshotAvailable", out _));
        Assert.Single(Directory.GetFiles(d.Path));
    }

    private static OverlayServeRequest Request(string root)
    {
        var snapshot = new SnapshotReference("snapshot", "git-objects", root, Path.Combine(root, "base"),
            DateTimeOffset.UtcNow, true, true, "synthetic-control-test");
        return new(new(2, "overlay", snapshot, 65536, 4096), Path.Combine(root, "delta"), Path.Combine(root, "mount"),
            root, "owner-token", new(snapshot.Id, snapshot.RootPath, "verified"), Path.Combine(root, "receipt"), "owner");
    }

    private static async Task Publish(OverlayServeRequest request, CancellationToken ct)
    {
        await WindowsProcessWorkspaceProvider.WriteAsync(Path.Combine(request.ControlPath, "provider-launch-intent.json"),
            new { request.Token, request.Identity.Id, generation = "ended" }, ct);
        await WindowsProcessWorkspaceProvider.WriteAsync(Path.Combine(request.ControlPath, "provider-launch-receipt.json"),
            new { request.Token, processId = int.MaxValue, startTimeUtcTicks = 1L, generation = "ended" }, ct);
        await WindowsProcessWorkspaceProvider.WriteAsync(Path.Combine(request.ControlPath, "state.json"),
            new { request.Token, request.Identity.Id, snapshotId = request.Identity.Snapshot.Id, phase = "unmounted",
                processId = int.MaxValue, processStartTimeUtcTicks = 1L, openHandles = 0 }, ct);
    }
}
