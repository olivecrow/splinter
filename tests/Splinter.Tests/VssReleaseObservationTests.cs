using System.Text.Json;
using Splinter.Core;
using Splinter.Platform.Windows;

namespace Splinter.Tests;

public sealed class VssReleaseObservationTests
{
    [Theory]
    [InlineData("valid", false, "SnapshotMissing")]
    [InlineData("legacy", null, "SnapshotObservationUnknown")]
    [InlineData("old-boot", null, "SnapshotObservationUnknown")]
    [InlineData("unknown-boot", null, "SnapshotObservationUnknown")]
    [InlineData("wrong-owner", null, "SnapshotObservationUnknown")]
    [InlineData("wrong-snapshot", null, "SnapshotObservationUnknown")]
    [InlineData("still-present", null, "SnapshotObservationUnknown")]
    [InlineData("not-released", null, "SnapshotVerificationRequired")]
    [InlineData("wrong-provider", null, "SnapshotIdentityMismatch")]
    [InlineData("wrong-receipt-owner", null, "SnapshotIdentityMismatch")]
    public async Task InaccessibleDeviceNeedsExactReleasedIdentityAndCurrentBootProof(string scenario, bool? available, string failure)
    {
        using var d = new TestDirectory();
        var snapshot = Snapshot(d.Path);
        var boot = DateTimeOffset.Parse("2026-09-21T00:00:00Z");
        WriteReceipt(d.Path, snapshot, scenario);
        File.WriteAllText(Path.Combine(d.Path, "observation.json"), JsonSerializer.Serialize(new
        {
            ownerId = scenario == "wrong-owner" ? "someone-else" : "owner",
            snapshot = scenario == "wrong-snapshot" ? snapshot with { Id = "another-id" } : snapshot,
            available = scenario == "still-present",
            released = true,
            bootTimeUtc = scenario == "legacy" ? (DateTimeOffset?)null : scenario == "old-boot" ? boot.AddDays(-1) : boot
        }, Json));
        var provider = new VssSnapshotProvider(d.Path, "owner", s => new(s.Id, null, "SnapshotObservationUnknown", null, null),
            _ => Task.FromResult(scenario == "unknown-boot" ? (DateTimeOffset?)null : boot));
        var observed = await provider.ObserveAsync(snapshot, TestContext.Current.CancellationToken);
        Assert.Equal(available, observed.Available);
        Assert.Equal(failure, observed.FailureCode);
        if (available == false) await provider.ReleaseAsync(snapshot, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ReusedReleasedDevicePathCannotBeAcceptedAsTheOriginalSnapshot()
    {
        using var d = new TestDirectory(); var snapshot = Snapshot(d.Path);
        WriteReceipt(d.Path, snapshot, "valid");
        var provider = new VssSnapshotProvider(d.Path, "owner", s => new(s.Id, true, null, null, null),
            _ => throw new InvalidOperationException("Do not resolve a conflicting live identity from cached proof."));
        var observed = await provider.ObserveAsync(snapshot, TestContext.Current.CancellationToken);
        Assert.Null(observed.Available);
        Assert.Equal("SnapshotIdentityMismatch", observed.FailureCode);
        Assert.Equal("SnapshotIdentityMismatch", (await Assert.ThrowsAsync<WorkspaceException>(() =>
            provider.ReleaseAsync(snapshot, TestContext.Current.CancellationToken))).Code);
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static SnapshotReference Snapshot(string source) => new("owned-id", "windows-vss-system", source,
        Path.Combine(source, "deleted-device"), DateTimeOffset.Parse("2026-09-21T01:00:00Z"), true, true, "test");
    private static void WriteReceipt(string path, SnapshotReference snapshot, string scenario) =>
        File.WriteAllText(Path.Combine(path, "snapshot.json"), JsonSerializer.Serialize(new
        {
            ownerId = scenario == "wrong-receipt-owner" ? "other-owner" : "owner", snapshot,
            nativeProviderId = scenario == "wrong-provider" ? "another-provider" : "{B5946137-7B9F-4925-AF80-51ABD60B20D5}",
            noAutoRelease = true, released = scenario != "not-released"
        }, Json));
}
