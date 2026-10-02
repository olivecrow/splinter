using System.Text.Json;
using Splinter.Core;
using Splinter.Platform.Windows;

namespace Splinter.Tests;

public sealed class OverlayLaunchReceiptTests
{
    [Theory]
    [InlineData("missing")]
    [InlineData("owner")]
    [InlineData("token")]
    [InlineData("generation")]
    [InlineData("corrupt")]
    [InlineData("empty-generation")]
    public async Task InvalidLaunchCannotPublishReceiptOrOverwriteExistingState(string mismatch)
    {
        using var d = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        var snapshot = new SnapshotReference("snapshot", "fake", Path.Combine(d.Path, "source"), Path.Combine(d.Path, "base"),
            DateTimeOffset.UtcNow, true, true, "fake");
        var request = new OverlayServeRequest(new(2, "overlay", snapshot, 65536, 4096), Path.Combine(d.Path, "delta"),
            Path.Combine(d.Path, "mount"), d.Path, "token", new(snapshot.Id, snapshot.RootPath, "verified"), Path.Combine(d.Path, "snapshot"), "owner");
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        string requestPath = Path.Combine(d.Path, "request.json"), intent = Path.Combine(d.Path, "provider-launch-intent.json"),
            state = Path.Combine(d.Path, "state.json"), receipt = Path.Combine(d.Path, "provider-launch-receipt.json");
        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request, json), ct);
        const string retained = "existing-generation-evidence";
        await File.WriteAllTextAsync(state, retained, ct); await File.WriteAllTextAsync(receipt, retained, ct);
        if (mismatch != "missing")
            await File.WriteAllTextAsync(intent, mismatch == "corrupt" ? "{" : JsonSerializer.Serialize(new {
                token = mismatch == "token" ? "other" : request.Token,
                id = mismatch == "owner" ? "other" : request.Identity.Id,
                generation = mismatch == "generation" ? "other" : mismatch == "empty-generation" ? "" : "generation",
                requestedAt = DateTimeOffset.UtcNow
            }, json), ct);
        string? priorIntent = File.Exists(intent) ? await File.ReadAllTextAsync(intent, ct) : null;
        Assert.Equal(1, await WinFspOverlayServer.RunAsync(requestPath, mismatch == "empty-generation" ? "" : "generation"));
        Assert.Equal(retained, await File.ReadAllTextAsync(state, ct));
        Assert.Equal(retained, await File.ReadAllTextAsync(receipt, ct));
        Assert.Equal(priorIntent, File.Exists(intent) ? await File.ReadAllTextAsync(intent, ct) : null);
        Assert.False(Directory.Exists(request.DeltaPath)); Assert.False(Directory.Exists(request.MountPath));
    }
}
