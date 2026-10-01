using System.Text.Json;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Protocol;
using Splinter.Runtime;
using Splinter.SourceControl.Git;

namespace Splinter.Tests;

public sealed partial class StorageAuthorityTests
{
    [Theory]
    [InlineData("ready")][InlineData("missing-intent")][InlineData("other-intent")][InlineData("unknown-resources")]
    [InlineData("delta")][InlineData("branch")][InlineData("peer")][InlineData("reader")]
    public void DiscardAuthorityRequiresExplicitIdentityAndAbsenceWithoutResultClaim(string variant)
    {
        if (!OperatingSystem.IsWindows()) { Assert.Skip("Windows path authorization."); return; }
        using var d = new TestDirectory(); var capture = Capture(d.Path); Directory.CreateDirectory(capture.SourcePath);
        var store = new WorkspaceStore(capture.HomePath); string control = store.ControlPath(capture.OwnerId);
        var snapshot = new SnapshotReference(Guid.NewGuid().ToString("B"), "fake", capture.SourcePath, capture.SourcePath, DateTimeOffset.UtcNow, true, true, "fixture");
        string revision = new('a', 40);
        var w = new WorkspaceRecord { Id = capture.OwnerId, RequestKey = "discard", RequestDigest = new('b', 24), ProjectPath = capture.SourcePath,
            Path = store.WorkPath(capture.OwnerId), SourceReference = "splinter/" + capture.OwnerId, BaseReference = "development", BaseRevision = revision,
            Phase = "discarding", StorageMode = "ntfs-snapshot", ReleasedResources = new(true, false, 0, 0, 0),
            Snapshot = new() { Step = "discard-snapshot", Reference = snapshot, Observation = new(true, false, false, true, 0, 0, 0, DeltaPresent: false) } };
        var discard = new WorkspaceDiscardState { Step = "snapshot", Seal = new(1, 4, new('c', 64)),
            Intent = new(1, w.Id, "explicit-discard", w.RequestDigest, w.ProjectPath, w.Path, w.SourceReference, revision, revision, snapshot, DateTimeOffset.UtcNow) };
        w.Discard = discard;
        if (variant == "missing-intent") w.Discard = null;
        if (variant == "unknown-resources") w.ReleasedResources = new(false, false, 0, 0, 0);
        store.Save(w, "discard-snapshot");
        string receipts = Path.Combine(control, "snapshot"); Directory.CreateDirectory(receipts);
        var gate = variant == "other-intent" ? new WorkspaceDiscardState { Step = "snapshot", Seal = discard.Seal, Intent = discard.Intent with { IntentKey = "other" } } : discard;
        File.WriteAllText(Path.Combine(receipts, "discard-authorized.json"), JsonSerializer.Serialize(gate, StorageAuthorityProtocol.Json));
        File.WriteAllText(Path.Combine(receipts, "readers.lock"), "");
        Directory.CreateDirectory(Path.Combine(w.ProjectPath, ".git"));
        var registration = new OverlayGitReceipt(w.Id, w.ProjectPath, w.Path, w.SourceReference, revision, Path.Combine(w.ProjectPath, ".git", "worktrees", "task"), "pointer");
        File.WriteAllText(Path.Combine(control, "git-discard.json"), JsonSerializer.Serialize(new GitDiscardReceipt(discard.Intent, registration, true), StorageAuthorityProtocol.Json));
        if (variant == "delta") Directory.CreateDirectory(Path.Combine(control, "delta"));
        if (variant == "branch")
        {
            string branch = Path.Combine(w.ProjectPath, ".git", "refs", "heads", "splinter", w.Id);
            Directory.CreateDirectory(Path.GetDirectoryName(branch)!); File.WriteAllText(branch, revision);
        }
        if (variant == "peer")
        {
            var peer = new WorkspaceRecord { Id = new('e', 24), RequestKey = "peer", ProjectPath = w.ProjectPath, Path = store.WorkPath(new('e', 24)),
                SourceReference = "splinter/peer", BaseReference = "development", Phase = "ready", Snapshot = new() { Reference = snapshot } };
            store.Save(peer, "peer");
        }
        StorageGrant grant;
        using (var paths = new StorageAuthorityPaths()) grant = new(Sid, w.ProjectPath, capture.HomePath, StorageAuthorityPaths.Volume(w.ProjectPath), paths.Pin(w.ProjectPath), paths.Pin(capture.HomePath));
        var request = capture with { Operation = "Release", SnapshotId = snapshot.Id,
            Release = new(capture.RequestId, w.Id, snapshot, DateTimeOffset.UtcNow, Boot, discard) };
        Assert.True(new ContractCatalog().Validate("urn:splinter:v1:storage-authority-request", JsonSerializer.SerializeToElement(request, StorageAuthorityProtocol.Json)).IsValid);
        StorageAuthorityProtocol.Validate(request);
        using var reader = variant == "reader" ? new FileStream(Path.Combine(receipts, "readers.lock"), FileMode.Open, FileAccess.Read, FileShare.Read) : null;
        if (variant == "ready") { using var authorized = StorageAuthorityPaths.Authorize(grant, request); }
        else if (variant == "reader") Assert.Throws<IOException>(() => StorageAuthorityPaths.Authorize(grant, request));
        else Assert.Throws<WorkspaceException>(() => StorageAuthorityPaths.Authorize(grant, request));
        Assert.False(File.Exists(Path.Combine(receipts, "release-authorized.json"))); Assert.Null(w.Snapshot.Result);
    }

    [Fact]
    public async Task ProtectedDiscardReplayRetainsAuthorizationAndDoesNotPublishResultsPreserved()
    {
        using var d = new TestDirectory(); var capture = Capture(d.Path); var fake = new FakeOperations(); var processor = Processor(d.Path, capture, fake);
        var output = await processor.ProcessAsync(Sid, capture, Authorize, TestContext.Current.CancellationToken);
        var snapshot = output.Receipts["snapshot.json"].GetProperty("snapshot").Deserialize<SnapshotReference>(StorageAuthorityProtocol.Json)!;
        var discard = new WorkspaceDiscardState { Step = "snapshot", Seal = new(0, 0, new('a', 64)),
            Intent = new(1, capture.OwnerId, "discard", new('b', 24), capture.SourcePath, Path.Combine(capture.HomePath, "workspaces", capture.OwnerId), "task", "base", "head", snapshot, DateTimeOffset.UtcNow) };
        string id = Guid.NewGuid().ToString("N"); var request = capture with { Operation = "Release", RequestId = id, SnapshotId = snapshot.Id,
            Release = new(id, capture.OwnerId, snapshot, DateTimeOffset.UtcNow, Boot, discard) };
        var first = await processor.ProcessAsync(Sid, request, Authorize, TestContext.Current.CancellationToken);
        var replay = await Processor(d.Path, capture, fake).ProcessAsync(Sid, request, Authorize, TestContext.Current.CancellationToken);
        Assert.Null(first.ErrorCode); Assert.Null(replay.ErrorCode); Assert.Equal(2, fake.Calls);
        string receipts = Directory.GetDirectories(Path.Combine(d.Path, "protected", "owners")).Single();
        Assert.True(File.Exists(Path.Combine(receipts, "receipts", "discard-authorized.json")));
        Assert.False(File.Exists(Path.Combine(receipts, "receipts", "release-authorized.json")));
    }
}
