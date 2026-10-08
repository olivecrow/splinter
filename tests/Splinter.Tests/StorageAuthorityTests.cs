using System.Text;
using System.Text.Json;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Protocol;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed partial class StorageAuthorityTests
{
    private const string Sid = "S-1-5-21-111-222-333-1001";
    private static readonly DateTimeOffset Boot = DateTimeOffset.UtcNow.AddHours(-1);
    private static StorageAuthorityRequest Capture(string root)
    {
        string id = Guid.NewGuid().ToString("N");
        string owner = WorkspaceStore.Key("authority-test");
        return new(1, id, "Capture", owner, Path.Combine(root, "source"), Path.Combine(root, "home"), null,
            new(id, owner, null, DateTimeOffset.UtcNow, Boot), null);
    }
    private static StorageAuthorityProcessor Processor(string root, StorageAuthorityRequest request, FakeOperations fake) =>
        new(Path.Combine(root, "protected"), new(1, "fixed-powershell", [new(Sid, request.SourcePath, request.HomePath, "volume", "source-id", "home-id")]), fake);
    private sealed class Lease : IDisposable { public void Dispose() { } }
    private static IDisposable Authorize(StorageGrant _, StorageAuthorityRequest __) => new Lease();

    [Fact]
    public void APathCannotBeRenamedOrTurnedIntoAnotherDirectoryDuringAuthorization()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows file sharing or native contract.");
        using var d = new TestDirectory(); string path = Path.Combine(d.Path, "project"); Directory.CreateDirectory(path);
        string original;
        using (var pinned = new StorageAuthorityPaths())
        {
            original = pinned.Pin(path);
            Assert.Throws<IOException>(() => Directory.Move(path, path + "-old"));
            // Ordinary edits inside the source remain possible during capture.
            File.WriteAllText(Path.Combine(path, "active.txt"), "source remains writable");
        }
        Directory.Move(path, path + "-old"); Directory.CreateDirectory(path);
        using var replacement = new StorageAuthorityPaths(); Assert.NotEqual(original, replacement.Pin(path));
    }

    [Fact]
    public async Task ARejectedReleaseGateNeverWritesPrivilegedAuthorization()
    {
        using var d = new TestDirectory(); var request = Capture(d.Path); var fake = new FakeOperations();
        await Assert.ThrowsAsync<WorkspaceException>(() => Processor(d.Path, request, fake).ProcessAsync(Sid, request,
            (_, _) => throw new WorkspaceException("SnapshotInUse", "retained dependency"), TestContext.Current.CancellationToken));
        Assert.Equal(0, fake.Calls); Assert.False(Directory.Exists(Path.Combine(d.Path, "protected", "owners")));
    }

    [Fact]
    public async Task OversizedOrIncompleteFramesCannotAllocateUnboundedMessages()
    {
        using var oversized = new MemoryStream([255, 255, 255, 127]);
        await Assert.ThrowsAsync<JsonException>(() => StorageAuthorityServer.ReadFrameAsync(oversized, StorageAuthorityProtocol.MaxRequestBytes, TestContext.Current.CancellationToken));
        using var partial = new MemoryStream([10, 0, 0, 0, 1]);
        await Assert.ThrowsAsync<EndOfStreamException>(() => StorageAuthorityServer.ReadFrameAsync(partial, StorageAuthorityProtocol.MaxRequestBytes, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LostReplyAndNewServiceInstanceDoNotRepeatThePrivilegedOperation()
    {
        using var d = new TestDirectory(); var request = Capture(d.Path); var fake = new FakeOperations();
        var contracts = new ContractCatalog();
        Assert.True(contracts.Validate("urn:splinter:v1:storage-authority-request", JsonSerializer.SerializeToElement(request, StorageAuthorityProtocol.Json)).IsValid);
        var first = await Processor(d.Path, request, fake).ProcessAsync(Sid, request, Authorize, TestContext.Current.CancellationToken);
        var second = await Processor(d.Path, request, fake).ProcessAsync(Sid, request, Authorize, TestContext.Current.CancellationToken);
        Assert.True(contracts.Validate("urn:splinter:v1:storage-authority-response", JsonSerializer.SerializeToElement(first, StorageAuthorityProtocol.Json)).IsValid);
        Assert.Equal(1, fake.Captures); Assert.True(JsonElement.DeepEquals(first.Receipts["snapshot.json"], second.Receipts["snapshot.json"]));
        var changed = request with { Verification = request.Verification with { RequestedAt = DateTimeOffset.UtcNow.AddMinutes(1) } };
        var error = await Assert.ThrowsAsync<WorkspaceException>(() => Processor(d.Path, request, fake).ProcessAsync(Sid, changed, Authorize, TestContext.Current.CancellationToken));
        Assert.Equal("StorageRequestConflict", error.Code); Assert.Equal(1, fake.Captures);
    }

    [Fact]
    public async Task CompletePendingResponseResumesPublicationWithoutAnotherCapture()
    {
        using var d = new TestDirectory(); var request = Capture(d.Path); var fake = new FakeOperations();
        await Processor(d.Path, request, fake).ProcessAsync(Sid, request, Authorize, TestContext.Current.CancellationToken);
        string response = Directory.GetFiles(Path.Combine(d.Path, "protected"), "*.response.json", SearchOption.AllDirectories).Single();
        File.Move(response, response + ".pending");
        var resumed = await Processor(d.Path, request, fake).ProcessAsync(Sid, request, Authorize, TestContext.Current.CancellationToken);
        Assert.Null(resumed.ErrorCode); Assert.Equal(1, fake.Captures);
        Assert.True(File.Exists(response)); Assert.False(File.Exists(response + ".pending"));
    }

    [Fact]
    public void IncompleteProtectedPublicationPreservesBothThePreviousAndPendingBytes()
    {
        using var d = new TestDirectory(); string path = Path.Combine(d.Path, "state.json");
        File.WriteAllText(path, "{\"version\":1}"); File.WriteAllText(path + ".pending", "{\"version\":");
        Assert.Equal("StorageStateIncomplete", Assert.Throws<WorkspaceException>(() => StorageAuthorityProcessor.Read<JsonElement>(path)).Code);
        Assert.Equal("{\"version\":1}", File.ReadAllText(path)); Assert.Equal("{\"version\":", File.ReadAllText(path + ".pending"));
    }

    [Fact]
    public async Task InterruptedClientPublicationCannotCommitAPartialCapture()
    {
        using var d = new TestDirectory(); var request = Capture(d.Path); var fake = new FakeOperations();
        var response = await Processor(d.Path, request, fake).ProcessAsync(Sid, request, Authorize, TestContext.Current.CancellationToken);
        response.Receipts.Add("live-observation.json", JsonSerializer.SerializeToElement(new { available = true }));
        var published = new List<string>();
        Task Interrupted(string name, JsonElement _, CancellationToken __)
        { published.Add(name); throw new IOException("Simulated client exit while publishing the live proof."); }
        await Assert.ThrowsAsync<IOException>(() => StorageAuthorityClient.PublishReceiptsAsync(response, Interrupted, TestContext.Current.CancellationToken));
        Assert.DoesNotContain("snapshot.json", published);
        published.Clear();
        Task Complete(string name, JsonElement _, CancellationToken __) { published.Add(name); return Task.CompletedTask; }
        var replay = await Processor(d.Path, request, fake).ProcessAsync(Sid, request, Authorize, TestContext.Current.CancellationToken);
        await StorageAuthorityClient.PublishReceiptsAsync(replay, Complete, TestContext.Current.CancellationToken);
        Assert.Equal("snapshot.json", published[^1]); Assert.Equal(1, fake.Captures);
        published.Clear();
        await StorageAuthorityClient.PublishReceiptsAsync(response with { ErrorCode = "SnapshotOperationIncomplete" }, Complete, TestContext.Current.CancellationToken);
        Assert.DoesNotContain("snapshot.json", published);
    }

    [Fact]
    public void RealWorkspaceRecordAllowsCaptureRecoveryButRejectsChangedIdentity()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Skip("Windows path identity test."); return; }
        using var d = new TestDirectory(); var request = Capture(d.Path);
        Directory.CreateDirectory(request.SourcePath); var store = new WorkspaceStore(request.HomePath);
        var workspace = new WorkspaceRecord { Id = request.OwnerId, RequestKey = "authority-test", ProjectPath = request.SourcePath,
            Path = store.WorkPath(request.OwnerId), SourceReference = "splinter/test", BaseReference = "development",
            StorageMode = "ntfs-snapshot", Snapshot = new() { Step = "capture-intent" } };
        store.Save(workspace, "capture-intent"); Directory.CreateDirectory(Path.Combine(store.ControlPath(workspace.Id), "snapshot"));
        StorageGrant grant;
        using (var paths = new StorageAuthorityPaths()) grant = new(Sid, request.SourcePath, request.HomePath,
            StorageAuthorityPaths.Volume(request.SourcePath), paths.Pin(request.SourcePath), paths.Pin(request.HomePath));
        using (StorageAuthorityPaths.Authorize(grant, request)) { }
        using (StorageAuthorityPaths.Authorize(grant, request with { Operation = "Observe", SnapshotId = Guid.NewGuid().ToString("B") })) { }
        workspace.Phase = "ready"; store.Save(workspace, "fixture-invalid-ready");
        Assert.Equal("StorageOwnerMismatch", Assert.Throws<WorkspaceException>(() => StorageAuthorityPaths.Authorize(grant,
            request with { Operation = "Observe", SnapshotId = Guid.NewGuid().ToString("B") })).Code);
        Directory.Move(request.SourcePath, request.SourcePath + "-old"); Directory.CreateDirectory(request.SourcePath);
        Assert.Equal("StoragePathChanged", Assert.Throws<WorkspaceException>(() => StorageAuthorityPaths.Authorize(grant, request)).Code);
    }

    [Theory]
    [InlineData("account")][InlineData("image")][InlineData("shared-process")]
    public void ScmConfigurationCannotPointTheClientAtAnotherPrivilegeHost(string variant)
    {
        string expected = "\"" + Path.Combine(StorageAuthorityProtocol.InstallPath, "Splinter.Platform.Windows.exe") + "\" storage-service";
        StorageAuthorityScm.RequireConfiguration(0x10, expected, "LocalSystem");
        Assert.Equal("StorageServiceIdentityMismatch", Assert.Throws<WorkspaceException>(() =>
            StorageAuthorityScm.RequireConfiguration(variant == "shared-process" ? 0x20u : 0x10u,
                variant == "image" ? expected + " extra-command" : expected, variant == "account" ? Sid : "LocalSystem")).Code);
    }

    [Theory]
    [InlineData("ready")][InlineData("unknown-resources")][InlineData("remaining-mount")][InlineData("changed-result")][InlineData("active-reader")]
    public void NativeReleaseAuthorizationRequiresThePreservedResultAndZeroOwnedResources(string variant)
    {
        if (!OperatingSystem.IsWindows()) { Assert.Skip("Windows release evidence test."); return; }
        using var d = new TestDirectory(); var capture = Capture(d.Path);
        Directory.CreateDirectory(capture.SourcePath); var store = new WorkspaceStore(capture.HomePath);
        var snapshot = new SnapshotReference(Guid.NewGuid().ToString("B"), "fake", capture.SourcePath, capture.SourcePath,
            DateTimeOffset.UtcNow, true, true, "fixture");
        string revision = new('a', 40), resultRef = "refs/splinter/snapshot-results/" + capture.OwnerId;
        var workspace = new WorkspaceRecord { Id = capture.OwnerId, RequestKey = "authority-test", ProjectPath = capture.SourcePath,
            Path = store.WorkPath(capture.OwnerId), SourceReference = "splinter/test", BaseReference = "development",
            Phase = "releasing", StorageMode = "ntfs-snapshot", ResultRevision = revision,
            ReleasedResources = new(true, false, 0, 0, 0), Snapshot = new() { Step = "release-snapshot", Reference = snapshot,
                Result = new(snapshot.Id, revision, "fingerprint", resultRef, new(0, 0, new('b', 64)), true, SnapshotResult.StableJournalProtocol),
                Observation = new(true, false, false, true, 0, 0, 0) } };
        if (variant == "unknown-resources") workspace.ReleasedResources = new(false, false, 0, 0, 0);
        if (variant == "remaining-mount") Directory.CreateDirectory(workspace.Path);
        store.Save(workspace, "release-snapshot");
        string receipts = Path.Combine(store.ControlPath(workspace.Id), "snapshot"); Directory.CreateDirectory(receipts);
        File.WriteAllText(Path.Combine(receipts, "release-authorized.json"), JsonSerializer.Serialize(new { snapshotId = snapshot.Id, remainingDependents = 0, resultsPreserved = true }));
        string readers = Path.Combine(receipts, "readers.lock"); File.WriteAllText(readers, "");
        Directory.CreateDirectory(Path.Combine(capture.SourcePath, ".git", "objects"));
        foreach (string reference in new[] { resultRef, "refs/heads/" + workspace.SourceReference })
        {
            string path = Path.Combine(capture.SourcePath, ".git", reference.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, variant == "changed-result" ? new('c', 40) : revision);
        }
        StorageGrant grant;
        using (var paths = new StorageAuthorityPaths()) grant = new(Sid, capture.SourcePath, capture.HomePath,
            StorageAuthorityPaths.Volume(capture.SourcePath), paths.Pin(capture.SourcePath), paths.Pin(capture.HomePath));
        var request = capture with { Operation = "Release", SnapshotId = snapshot.Id,
            Release = new(capture.RequestId, capture.OwnerId, snapshot, DateTimeOffset.UtcNow, Boot) };
        using var held = variant == "active-reader" ? new FileStream(readers, FileMode.Open, FileAccess.Read, FileShare.Read) : null;
        if (variant == "ready") { using var guard = StorageAuthorityPaths.Authorize(grant, request); }
        else if (variant == "active-reader") Assert.Throws<IOException>(() => StorageAuthorityPaths.Authorize(grant, request));
        else Assert.Throws<WorkspaceException>(() => StorageAuthorityPaths.Authorize(grant, request));
        Assert.True(File.Exists(Path.Combine(receipts, "release-authorized.json")));
    }

    [Fact]
    public async Task InterruptedOperationRetainsItsIntentAndOnlyThatRequestCanResume()
    {
        using var d = new TestDirectory(); var request = Capture(d.Path); var fake = new FakeOperations { InterruptAfterCapture = true };
        await Assert.ThrowsAsync<OperationCanceledException>(() => Processor(d.Path, request, fake).ProcessAsync(Sid, request, Authorize, TestContext.Current.CancellationToken));
        var next = Capture(d.Path);
        Assert.Equal("StorageRequestPending", (await Assert.ThrowsAsync<WorkspaceException>(() => Processor(d.Path, request, fake).ProcessAsync(Sid, next, Authorize, TestContext.Current.CancellationToken))).Code);
        fake.InterruptAfterCapture = false;
        var resumed = await Processor(d.Path, request, fake).ProcessAsync(Sid, request, Authorize, TestContext.Current.CancellationToken);
        Assert.Null(resumed.ErrorCode); Assert.Equal(1, fake.Captures);
    }

    [Theory]
    [InlineData("user")][InlineData("project")][InlineData("home")][InlineData("snapshot")][InlineData("operation")][InlineData("version")][InlineData("traversal")]
    public async Task ForgedRequestsNeverReachPrivilegedOperations(string variant)
    {
        using var d = new TestDirectory(); var original = Capture(d.Path); var fake = new FakeOperations();
        string peer = Sid; var request = original;
        switch (variant)
        {
            case "user": peer = "S-1-5-21-111-222-333-1002"; break;
            case "project": request = request with { SourcePath = Path.Combine(d.Path, "other") }; break;
            case "home": request = request with { HomePath = Path.Combine(d.Path, "other") }; break;
            case "snapshot": request = request with { Operation = "Observe", SnapshotId = Guid.NewGuid().ToString("B") }; break;
            case "operation": request = request with { Operation = "Execute" }; break;
            case "version": request = request with { Version = 0 }; break;
            case "traversal": request = request with { OwnerId = "../victim" }; break;
        }
        await Assert.ThrowsAsync<WorkspaceException>(() => Processor(d.Path, original, fake).ProcessAsync(peer, request, Authorize, TestContext.Current.CancellationToken));
        Assert.Equal(0, fake.Calls);
    }

    [Theory]
    [InlineData("duplicate")][InlineData("extra")][InlineData("missing")]
    public void JsonCannotSmuggleAnotherCommandOrOmitRequiredFields(string variant)
    {
        using var d = new TestDirectory(); string json = JsonSerializer.Serialize(Capture(d.Path), StorageAuthorityProtocol.Json);
        json = variant switch
        {
            "duplicate" => json.Insert(1, "\"operation\":\"Execute\","),
            "extra" => json.Insert(1, "\"script\":\"dangerous\","),
            _ => "{\"version\":1}"
        };
        Assert.Throws<JsonException>(() => StorageAuthorityProtocol.Decode<StorageAuthorityRequest>(Encoding.UTF8.GetBytes(json)));
        var rejected = new StorageAuthorityResponse(1, "", "", "StorageRequestInvalid", []);
        Assert.True(new ContractCatalog().Validate("urn:splinter:v1:storage-authority-response",
            JsonSerializer.SerializeToElement(rejected, StorageAuthorityProtocol.Json)).IsValid);
    }

    [Fact]
    public async Task FailedReleaseReturnsDeferredEvidenceAndCannotBecomeSuccessOnRetry()
    {
        using var d = new TestDirectory(); var capture = Capture(d.Path); var fake = new FakeOperations();
        var processor = Processor(d.Path, capture, fake);
        var response = await processor.ProcessAsync(Sid, capture, Authorize, TestContext.Current.CancellationToken);
        var snapshot = response.Receipts["snapshot.json"].GetProperty("snapshot").Deserialize<SnapshotReference>(StorageAuthorityProtocol.Json)!;
        string id = Guid.NewGuid().ToString("N");
        var release = capture with { Operation = "Release", RequestId = id, SnapshotId = snapshot.Id,
            Release = new(id, capture.OwnerId, snapshot, DateTimeOffset.UtcNow, Boot) };
        fake.Error = "SnapshotReleaseIsolationRequired";
        var first = await processor.ProcessAsync(Sid, release, Authorize, TestContext.Current.CancellationToken);
        fake.Error = null;
        var retried = await Processor(d.Path, capture, fake).ProcessAsync(Sid, release, Authorize, TestContext.Current.CancellationToken);
        Assert.Equal("SnapshotReleaseIsolationRequired", first.ErrorCode); Assert.Equal(first.ErrorCode, retried.ErrorCode);
        var contracts = new ContractCatalog();
        foreach (var failure in new[] { first, retried })
            Assert.True(contracts.Validate("urn:splinter:v1:storage-authority-response", JsonSerializer.SerializeToElement(failure, StorageAuthorityProtocol.Json)).IsValid);
        Assert.Equal(2, fake.Calls);
    }

    private sealed class FakeOperations : IStorageAuthorityOperations
    {
        internal int Captures, Calls;
        internal bool InterruptAfterCapture;
        internal string? Error;
        public Task<string?> ExecuteAsync(string operation, string receipts, string source, string owner, CancellationToken ct)
        {
            Calls++;
            string path = Path.Combine(receipts, "snapshot.json");
            if (operation == "Capture" && !File.Exists(path))
            {
                Captures++;
                var snapshot = new SnapshotReference(Guid.NewGuid().ToString("B"), "fake", source, source,
                    DateTimeOffset.UtcNow, true, true, "fixture");
                File.WriteAllText(path, JsonSerializer.Serialize(new { ownerId = owner, snapshot }, StorageAuthorityProtocol.Json));
            }
            if (InterruptAfterCapture) throw new OperationCanceledException();
            return Task.FromResult(Error);
        }
    }
}
