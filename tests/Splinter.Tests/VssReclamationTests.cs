using System.Text.Json;
using System.Text.Json.Nodes;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Protocol;

namespace Splinter.Tests;

public sealed class VssReclamationTests
{
    private static readonly DateTimeOffset Boot = VssLiveIdentityTests.Request.BootTimeUtc!.Value;
    private static readonly SnapshotReference Snapshot = VssLiveIdentityTests.Snapshot;

    [Fact]
    public async Task ExternalHelperReplySurvivesRestartAndExplicitRetryUsesANewAttempt()
    {
        using var d = new TestDirectory(); WriteReceipt(d.Path);
        var ct = TestContext.Current.CancellationToken;
        Task Privilege(string action, string source, CancellationToken _) => throw new WorkspaceException("SnapshotPrivilegeRequired", "Run helper");
        await Assert.ThrowsAsync<WorkspaceException>(() => Provider(d.Path, Privilege).ReleaseAsync(Snapshot, ct));
        var request = ReadRequest(d.Path);
        WriteProof(d.Path, Proof(request));
        var deferred = await Provider(d.Path, (_, _, _) => throw new InvalidOperationException("Reuse the helper receipt.")).ReleaseAsync(Snapshot, ct);
        Assert.Equal("deferred", deferred.Disposition); Assert.Equal(request["requestId"]!.GetValue<string>(), deferred.AttemptId);
        Assert.Equal(["older"], deferred.BlockingSnapshotIds);
        var retry = await Assert.ThrowsAsync<WorkspaceException>(() => Provider(d.Path, Privilege).ReleaseAsync(Snapshot, ct, deferred.AttemptId));
        Assert.Equal("SnapshotPrivilegeRequired", retry.Code);
        Assert.NotEqual(deferred.AttemptId, ReadRequest(d.Path)["requestId"]!.GetValue<string>());
        // An interrupted new request reuses its ID, never the older blocker list.
        var currentId = ReadRequest(d.Path)["requestId"]!.GetValue<string>();
        await Assert.ThrowsAsync<WorkspaceException>(() => Provider(d.Path, Privilege).ReleaseAsync(Snapshot, ct, deferred.AttemptId));
        Assert.Equal(currentId, ReadRequest(d.Path)["requestId"]!.GetValue<string>());
        WriteReceipt(d.Path, released: true);
        var absent = new VssSnapshotProvider(d.Path, "owner", s => new(s.Id, false, "SnapshotMissing", null, null), _ => Task.FromResult<DateTimeOffset?>(Boot));
        Assert.Equal("released", (await absent.ReleaseAsync(Snapshot, ct, deferred.AttemptId)).Disposition);
    }

    [Theory]
    [InlineData("owner")][InlineData("snapshot")][InlineData("boot")][InlineData("before-request")]
    [InlineData("attempt")][InlineData("deleted")][InlineData("missing-other")][InlineData("missing-owned")]
    [InlineData("blocker-not-observed")][InlineData("empty-blockers")][InlineData("volume")]
    [InlineData("negative-space")][InlineData("unknown-with-bytes")][InlineData("truncated")][InlineData("missing-deletion-flag")]
    public async Task UntrustedDeferredEvidenceCannotBecomeAnAcceptedRelease(string variant)
    {
        using var d = new TestDirectory(); WriteReceipt(d.Path);
        var provider = Provider(d.Path, (_, _, _) =>
        {
            var proof = Proof(ReadRequest(d.Path));
            switch (variant)
            {
                case "owner": proof["ownerId"] = "other"; break;
                case "snapshot": proof["snapshot"]!["rootPath"] = "other"; break;
                case "boot": proof["bootTimeUtc"] = Boot.AddDays(-1); break;
                case "before-request": proof["observedAt"] = Boot; break;
                case "attempt": proof["requestId"] = "other"; break;
                case "deleted": proof["deletionAttempted"] = true; break;
                case "missing-deletion-flag": proof.AsObject().Remove("deletionAttempted"); break;
                case "missing-other": proof["missingOtherIds"] = new JsonArray("older"); break;
                case "missing-owned": proof["beforeIds"] = new JsonArray("older"); break;
                case "blocker-not-observed": proof["blockingIds"] = new JsonArray("another"); break;
                case "empty-blockers": proof["blockingIds"] = new JsonArray(); break;
                case "volume": proof["sharedStorage"]!["volumeId"] = "other"; break;
                case "negative-space": proof["sharedStorage"]!["usedBytes"] = -1; break;
                case "unknown-with-bytes": proof["sharedStorage"]!["failureCode"] = "SnapshotStorageUnknown"; break;
            }
            if (variant == "truncated") File.WriteAllText(Path.Combine(d.Path, "release-deferred.json"), "{");
            else WriteProof(d.Path, proof);
            throw new WorkspaceException("SnapshotReleaseIsolationRequired", "Blocked");
        });
        var failure = await Assert.ThrowsAsync<WorkspaceException>(() => provider.ReleaseAsync(Snapshot, TestContext.Current.CancellationToken));
        Assert.Equal(variant == "truncated" ? "SnapshotObservationCorrupt" : "SnapshotReclamationInvalid", failure.Code);
        Assert.False(JsonNode.Parse(File.ReadAllText(Path.Combine(d.Path, "snapshot.json")))!["released"]!.GetValue<bool>());
    }

    [Fact]
    public async Task OldBootProofRequiresFreshHelperAndMissingAccountingRemainsUnknown()
    {
        using var d = new TestDirectory(); WriteReceipt(d.Path); var ct = TestContext.Current.CancellationToken;
        var boot = Boot;
        int calls = 0;
        var provider = new VssSnapshotProvider(d.Path, "owner", s => new(s.Id, true, null, null, null), _ => Task.FromResult<DateTimeOffset?>(boot), (_, _, _) =>
        {
            calls++;
            var proof = Proof(ReadRequest(d.Path)); proof["bootTimeUtc"] = boot;
            proof["sharedStorage"] = JsonSerializer.SerializeToNode(new SnapshotSharedStorage(VssLiveIdentityTests.Capture.SourceVolume,
                DateTimeOffset.UtcNow, null, null, null, "SnapshotStorageUnknown"), ApiJson.Options);
            WriteProof(d.Path, proof); throw new WorkspaceException("SnapshotReleaseIsolationRequired", "Blocked");
        });
        var first = await provider.ReleaseAsync(Snapshot, ct);
        Assert.Null(first.SharedStorage!.UsedBytes);
        Assert.Equal("unknown", first.SharedStorage.WorkspaceAttribution);
        boot = boot.AddDays(1);
        var second = await provider.ReleaseAsync(Snapshot, ct);
        Assert.Equal(2, calls); Assert.NotEqual(first.AttemptId, second.AttemptId);
    }

    private static VssSnapshotProvider Provider(string path, Func<string, string, CancellationToken, Task> run) =>
        new(path, "owner", s => new(s.Id, true, null, null, null), _ => Task.FromResult<DateTimeOffset?>(Boot), run);
    private static void WriteReceipt(string path, bool released = false) => File.WriteAllText(Path.Combine(path, "snapshot.json"),
        JsonSerializer.Serialize(new { version = 2, ownerId = "owner", snapshot = Snapshot,
            nativeProviderId = VssSnapshotVerification.SystemProvider, noAutoRelease = true, released,
            captureIdentity = VssLiveIdentityTests.Capture }, ApiJson.Options));
    private static JsonNode ReadRequest(string path) => JsonNode.Parse(File.ReadAllText(Path.Combine(path, "release-request.json")))!;
    private static JsonNode Proof(JsonNode request) => JsonSerializer.SerializeToNode(new
    {
        version = 1, requestId = request["requestId"]!.GetValue<string>(), ownerId = "owner", snapshot = Snapshot,
        bootTimeUtc = Boot, observedAt = DateTimeOffset.UtcNow, disposition = "deferred", deletionAttempted = false,
        beforeIds = new[] { Snapshot.Id, "older" }, blockingIds = new[] { "older" }, missingOtherIds = Array.Empty<string>(),
        sharedStorage = new SnapshotSharedStorage(VssLiveIdentityTests.Capture.SourceVolume, DateTimeOffset.UtcNow, 1024, 4096, 8192)
    }, ApiJson.Options)!;
    private static void WriteProof(string path, JsonNode proof) => File.WriteAllText(Path.Combine(path, "release-deferred.json"), proof.ToJsonString());
}
