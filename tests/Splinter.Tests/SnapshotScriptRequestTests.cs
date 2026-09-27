using System.Diagnostics;
using System.Text.Json;
using Splinter.Protocol;

namespace Splinter.Tests;

public sealed class SnapshotScriptRequestTests
{
    [Theory]
    [InlineData(2, "owner", true)]
    [InlineData(1, "owner", false)]
    [InlineData(0, "owner", false)]
    [InlineData(2, "other-owner", false)]
    public async Task DevelopmentPreparationRequiresCurrentEvidenceAndEmitsTheProductTuple(int version, string liveOwner, bool valid)
    {
        using var d = new TestDirectory();
        string receipt = Path.Combine(d.Path, "snapshot.json"), run = Path.Combine(d.Path, "run");
        File.WriteAllText(receipt, JsonSerializer.Serialize(new { version, ownerId = "owner", snapshot = VssLiveIdentityTests.Snapshot,
            captureIdentity = VssLiveIdentityTests.Capture, released = false }, ApiJson.Options));
        File.WriteAllText(Path.Combine(d.Path, "verification-request.json"), JsonSerializer.Serialize(VssLiveIdentityTests.Request, ApiJson.Options));
        File.WriteAllText(Path.Combine(d.Path, "live-observation.json"), JsonSerializer.Serialize(VssLiveIdentityTests.Live with { OwnerId = liveOwner }, ApiJson.Options));
        byte[] original = File.ReadAllBytes(receipt);
        var start = new ProcessStartInfo("pwsh") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string arg in new[] { "-NoProfile", "-NonInteractive", "-File", Path.Combine(AppContext.BaseDirectory, "Scripts", "prepare-snapshot-overlay-test.ps1"),
            "-Action", "Overlays", "-RunDirectory", run, "-SnapshotReceipt", receipt, "-Names", "a" }) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); }
        finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(CancellationToken.None); } }
        string diagnostic = await error; await output;
        Assert.Equal(original, File.ReadAllBytes(receipt));
        string request = Path.Combine(run, "control-a", "request.json");
        if (!valid) { Assert.NotEqual(0, process.ExitCode); Assert.False(Directory.Exists(Path.GetDirectoryName(request))); return; }
        Assert.True(process.ExitCode == 0, diagnostic);
        using var json = JsonDocument.Parse(File.ReadAllText(request));
        Assert.True(new ContractCatalog().Validate("urn:splinter:v1:overlay-serve-request", json.RootElement).IsValid);
        Assert.Equal(d.Path, json.RootElement.GetProperty("snapshotReceiptDirectory").GetString());
        Assert.Equal("owner", json.RootElement.GetProperty("snapshotOwnerId").GetString());
        Assert.Equal(VssLiveIdentityTests.Live.DeviceObject + "\\source", json.RootElement.GetProperty("readAccess").GetProperty("rootPath").GetString());
        Assert.False(Directory.Exists(Path.Combine(run, "mount-a")));
        Assert.False(Directory.Exists(Path.Combine(run, "delta-a")));
    }
}
