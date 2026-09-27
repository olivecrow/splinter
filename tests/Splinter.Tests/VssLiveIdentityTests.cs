using Splinter.Core;
using Splinter.Platform.Windows;
using System.Text.Json;

namespace Splinter.Tests;

public sealed class VssLiveIdentityTests
{
    private static readonly DateTimeOffset Boot = DateTimeOffset.Parse("2026-09-22T00:00:00Z");
    internal static readonly SnapshotReference Snapshot = new("snapshot-id", "windows-vss-system", @"C:\source",
        @"\\?\GLOBALROOT\Device\HarddiskVolumeShadowCopy7\source", Boot.AddHours(1), true, true, "filesystem-crash-consistent-no-application-writer");
    internal static VssCaptureIdentity Capture => new(Snapshot.Id, VssSnapshotVerification.SystemProvider,
        @"\\?\Volume{11111111-2222-3333-4444-555555555555}\", "source", Snapshot.CapturedAt, true, true, true);
    internal static VssVerificationRequest Request => new("verification-1", "owner", Snapshot.Id, Boot.AddHours(2), Boot);
    internal static VssLiveObservation Live => new(Request.RequestId, "owner", Snapshot.Id, Boot, Request.RequestedAt.AddSeconds(1),
        true, null, Capture, @"\\?\GLOBALROOT\Device\HarddiskVolumeShadowCopy19");

    [Fact]
    public void CurrentReadLocationChangesWithoutChangingTheSnapshotReference()
    {
        SnapshotReference? pathObserved = null;
        var result = VssSnapshotVerification.Evaluate(Snapshot, "owner", Capture, Request, Live, Boot,
            s => { pathObserved = s; return new(s.Id, true, null, null, null); });
        Assert.True(result.Available);
        Assert.Equal(@"\\?\GLOBALROOT\Device\HarddiskVolumeShadowCopy19\source", result.ReadAccess!.RootPath);
        result.ReadAccess.RequireMatch(Snapshot);
        Assert.Equal(Snapshot, pathObserved! with { RootPath = Snapshot.RootPath });
        Assert.EndsWith("7\\source", Snapshot.RootPath);
    }

    [Theory]
    [InlineData("id")][InlineData("owner")][InlineData("provider")][InlineData("volume")]
    [InlineData("relative")][InlineData("timestamp")][InlineData("persistent")]
    [InlineData("device")][InlineData("same-path-other-id")]
    public void ConflictingEvidenceCannotAuthorizeReads(string change)
    {
        var live = change switch
        {
            "id" => Live with { SnapshotId = "other" },
            "owner" => Live with { OwnerId = "other" },
            "provider" => Live with { Identity = Capture with { NativeProviderId = "other" } },
            "volume" => Live with { Identity = Capture with { SourceVolume = @"\\?\Volume{aaaaaaaa-2222-3333-4444-555555555555}\" } },
            "relative" => Live with { Identity = Capture with { SourceRelativePath = "..\\outside" } },
            "timestamp" => Live with { Identity = Capture with { CapturedAt = Boot } },
            "persistent" => Live with { Identity = Capture with { Persistent = false } },
            "device" => Live with { DeviceObject = @"C:\source" },
            _ => Live with { SnapshotId = "other", DeviceObject = @"\\?\GLOBALROOT\Device\HarddiskVolumeShadowCopy7" }
        };
        var result = VssSnapshotVerification.Evaluate(Snapshot, "owner", Capture, Request, live, Boot,
            _ => throw new InvalidOperationException("Do not open unverified bytes."));
        Assert.Null(result.Available); Assert.Null(result.ReadAccess);
        Assert.Equal("SnapshotIdentityMismatch", result.FailureCode);
    }

    [Theory]
    [InlineData("old-boot")][InlineData("unknown-boot")][InlineData("request")]
    [InlineData("old-time")][InlineData("no-proof")][InlineData("no-capture")]
    public void StaleOrInsufficientEvidenceNeedsVerification(string change)
    {
        var live = change switch
        {
            "old-boot" => Live with { BootTimeUtc = Boot.AddDays(-1) },
            "request" => Live with { RequestId = "previous" },
            "old-time" => Live with { ObservedAt = Request.RequestedAt.AddSeconds(-1) },
            "no-proof" => null,
            _ => Live
        };
        var result = VssSnapshotVerification.Evaluate(Snapshot, "owner", change == "no-capture" ? null : Capture,
            Request, live, change == "unknown-boot" ? null : Boot, _ => throw new InvalidOperationException());
        Assert.Null(result.Available); Assert.Null(result.ReadAccess);
        Assert.Equal("SnapshotVerificationRequired", result.FailureCode);
    }

    [Theory]
    [InlineData(false, "SnapshotMissing")][InlineData(null, "SnapshotObservationUnknown")]
    public void SuccessfulIdAbsenceAndFailedQueryAreDifferent(bool? available, string code)
    {
        var result = VssSnapshotVerification.Evaluate(Snapshot, "owner", Capture, Request,
            Live with { Available = available, Identity = null, DeviceObject = null, FailureCode = available is null ? code : null },
            Boot, _ => throw new InvalidOperationException());
        Assert.Equal(available, result.Available); Assert.Equal(code, result.FailureCode); Assert.Null(result.ReadAccess);
    }

    [Fact]
    public void MissingDevicePathDoesNotProveVssIdAbsence()
    {
        var result = VssSnapshotVerification.Evaluate(Snapshot, "owner", Capture, Request, Live, Boot,
            s => new(s.Id, false, "SnapshotMissing", null, null));
        Assert.Null(result.Available); Assert.Equal("SnapshotObservationUnknown", result.FailureCode);
    }

    [Fact]
    public async Task ReconnectConsumesANewProofButRetryReusesThePendingProof()
    {
        using var directory = new TestDirectory();
        Write(directory.Path, "snapshot.json", new { version = 2, ownerId = "owner", snapshot = Snapshot,
            nativeProviderId = Capture.NativeProviderId, noAutoRelease = true, released = false, captureIdentity = Capture });
        Write(directory.Path, "verification-request.json", Request);
        Write(directory.Path, "live-observation.json", Live);
        byte[] original = File.ReadAllBytes(Path.Combine(directory.Path, "snapshot.json"));
        int queries = 0;
        var provider = Provider(directory.Path, (action, source, ct) =>
        {
            Assert.Equal("Observe", action); Assert.Equal(Snapshot.SourcePath, source); queries++;
            var request = JsonSerializer.Deserialize<VssVerificationRequest>(File.ReadAllText(Path.Combine(directory.Path, "verification-request.json")), Json)!;
            Assert.NotEqual(Request.RequestId, request.RequestId);
            Write(directory.Path, "live-observation.json", Live with { RequestId = request.RequestId, ObservedAt = request.RequestedAt.AddSeconds(1) });
            return Task.CompletedTask;
        });
        var access = await provider.ResolveReadAccessAsync(Snapshot, Request.RequestId, TestContext.Current.CancellationToken);
        Assert.Equal(access, await provider.ResolveReadAccessAsync(Snapshot, Request.RequestId, TestContext.Current.CancellationToken));
        Assert.Equal(1, queries);
        Assert.Equal(original, File.ReadAllBytes(Path.Combine(directory.Path, "snapshot.json")));
    }

    [Theory]
    [InlineData(0, "SnapshotObservationCorrupt")]
    [InlineData(1, "SnapshotObservationCorrupt")]
    [InlineData(3, "SnapshotObservationCorrupt")]
    [InlineData(2, "SnapshotObservationCorrupt")]
    public async Task UnprovableIdentityPreservesTheOriginalReceipt(int version, string code)
    {
        using var directory = new TestDirectory();
        Write(directory.Path, "snapshot.json", new { version, ownerId = "owner", snapshot = Snapshot,
            nativeProviderId = Capture.NativeProviderId, noAutoRelease = true, released = false });
        byte[] original = File.ReadAllBytes(Path.Combine(directory.Path, "snapshot.json"));
        var provider = Provider(directory.Path, (_, _, _) => throw new InvalidOperationException("Invalid receipts must not invoke a helper."));
        var failure = await Assert.ThrowsAsync<WorkspaceException>(() => provider.ResolveReadAccessAsync(Snapshot, null, TestContext.Current.CancellationToken));
        Assert.Equal(code, failure.Code);
        Assert.Equal(original, File.ReadAllBytes(Path.Combine(directory.Path, "snapshot.json")));
        Assert.Equal("SnapshotObservationCorrupt", (await provider.ObserveAsync(Snapshot, TestContext.Current.CancellationToken)).FailureCode);
        Assert.Equal("SnapshotObservationCorrupt", (await Assert.ThrowsAsync<WorkspaceException>(() => provider.ReleaseAsync(Snapshot, TestContext.Current.CancellationToken))).Code);
    }

    [Theory]
    [InlineData("corrupt", "SnapshotObservationCorrupt")]
    [InlineData("partial", "SnapshotObservationIncomplete")]
    [InlineData("request-temp", "SnapshotObservationIncomplete")]
    [InlineData("failed", "SnapshotObservationUnknown")]
    public async Task BrokenLatestObservationNeverFallsBackToAnOlderSuccess(string fault, string code)
    {
        using var directory = new TestDirectory();
        Write(directory.Path, "snapshot.json", new { version = 2, ownerId = "owner", snapshot = Snapshot,
            nativeProviderId = Capture.NativeProviderId, noAutoRelease = true, released = false, captureIdentity = Capture });
        Write(directory.Path, "verification-request.json", Request);
        Write(directory.Path, "live-observation.json", Live);
        if (fault == "corrupt") File.WriteAllText(Path.Combine(directory.Path, "live-observation.json"), "{");
        if (fault == "partial") File.WriteAllText(Path.Combine(directory.Path, "live-observation.json.new"), "partial");
        if (fault == "request-temp") File.WriteAllText(Path.Combine(directory.Path, "verification-request.json." + Guid.NewGuid().ToString("N") + ".tmp"), "partial");
        if (fault == "failed") Write(directory.Path, "live-observation.json", Live with { Available = null, FailureCode = code, Identity = null });
        var provider = Provider(directory.Path, (_, _, _) => throw new InvalidOperationException("Read-only observation must not invoke a helper."));
        var observation = await provider.ObserveAsync(Snapshot, TestContext.Current.CancellationToken);
        Assert.Null(observation.Available); Assert.Null(observation.ReadAccess); Assert.Equal(code, observation.FailureCode);
        if (fault == "partial") Assert.Equal("partial", File.ReadAllText(Path.Combine(directory.Path, "live-observation.json.new")));
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static void Write<T>(string root, string file, T value) => File.WriteAllText(Path.Combine(root, file), JsonSerializer.Serialize(value, Json));
    private static VssSnapshotProvider Provider(string path, Func<string, string, CancellationToken, Task> helper) =>
        new(path, "owner", s => new(s.Id, true, null, null, null), _ => Task.FromResult<DateTimeOffset?>(Boot), helper);

    [Theory]
    [InlineData(@"..\outside")][InlineData(@"source\..\outside")][InlineData(@"source:stream")]
    [InlineData(@"source\\child")][InlineData(@"source/child")][InlineData(@"source.\child")]
    public void CapturedRelativePathCannotEscapeOrAlias(string relative) =>
        Assert.False(VssSnapshotVerification.TryRelative(@"\\?\GLOBALROOT\Device\HarddiskVolumeShadowCopy7\" + relative, out _));
}
