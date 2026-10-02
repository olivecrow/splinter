using System.Security.Cryptography;
using System.Text.Json;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;
using Splinter.SourceControl.Git;
using Splinter.Workflows.Windows;

namespace Splinter.Tests;

public sealed partial class SnapshotWorkspaceCoordinatorTests
{
    [Theory]
    [InlineData("complete")][InlineData("journal-removed")][InlineData("directory-removed")]
    [InlineData("unknown-file")][InlineData("changed-journal")][InlineData("unknown-resources")]
    public async Task WindowsDiscardDeletesOnlySealedOwnedDeltaAndResumesPartialRemoval(string variant)
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure(ct)).Workspace; var store = new WorkspaceStore(f.Home);
        string control = store.ControlPath(w.Id), delta = Path.Combine(control, "delta");
        await f.Backend.UnmountAsync(w, control, ct);
        Directory.CreateDirectory(delta);
        var identity = new OverlayIdentity(1, w.Id, w.Snapshot!.Reference!, w.Snapshot.MaxJournalBytes, 1024 * 1024, [".git"]);
        var tree = new SnapshotOverlayTests.FakeSnapshotTree { Snapshot = w.Snapshot.Reference! };
        long sequence;
        using (var journal = new JournalDeltaStore(delta, identity, tree))
        {
            var file = journal.Create("file.txt", false, 32); journal.Write(file.Id, 0, "failed-task"u8);
            sequence = journal.Observe().DurableSequence;
        }
        byte[] bytes = await File.ReadAllBytesAsync(Path.Combine(delta, "delta.journal"), ct);
        var seal = new SnapshotDeltaSeal(sequence, bytes.Length, Convert.ToHexStringLower(SHA256.HashData(bytes)));
        w.Phase = "discarding"; w.Snapshot!.Step = "discard-delta";
        w.Discard = new() { Step = "delta", Seal = seal, Intent = new(1, w.Id, "discard", w.RequestDigest!, w.ProjectPath,
            w.Path, w.SourceReference, w.BaseRevision!, "branch-head", w.Snapshot.Reference!, DateTimeOffset.UtcNow) };
        var registration = new OverlayGitReceipt(w.Id, w.ProjectPath, w.Path, w.SourceReference, w.BaseRevision!, Path.Combine(f.Project, ".git", "worktrees", "a"), "pointer");
        await WindowsProcessWorkspaceProvider.WriteAsync(Path.Combine(control, "git-discard.json"), new GitDiscardReceipt(w.Discard.Intent, registration, true), ct);
        if (variant is "journal-removed" or "directory-removed")
        {
            await WindowsProcessWorkspaceProvider.WriteAsync(Path.Combine(control, "discard-delta-owner.json"), w.Discard, ct);
            if (variant == "journal-removed")
            {
                Assert.Throws<IOException>(() => JournalReclamation.Reclaim(delta, seal, ["identity.json"], Path.Combine(control, "discard-delta-reclaim.json"),
                    boundary => { if (boundary == "journal-removed") throw new IOException("lost reply"); }));
            }
            else JournalReclamation.Reclaim(delta, seal, ["identity.json"], Path.Combine(control, "discard-delta-reclaim.json"));
        }
        if (variant == "unknown-file") await File.WriteAllTextAsync(Path.Combine(delta, "keep.txt"), "unowned", ct);
        if (variant == "changed-journal") await File.WriteAllBytesAsync(Path.Combine(delta, "delta.journal"), [4, 3, 2, 1], ct);
        if (variant == "unknown-resources") f.Backend.UnknownResources = true;
        var adapter = new WindowsWorkspaceDiscardProvider(new GitWorkspaceProvider(), f.Backend);
        if (variant is "unknown-file" or "changed-journal" or "unknown-resources")
        {
            await Assert.ThrowsAsync<WorkspaceException>(() => adapter.DiscardDeltaAsync(w, control, ct));
            Assert.True(File.Exists(Path.Combine(delta, "delta.journal")));
            Assert.False(File.Exists(Path.Combine(control, "discard-delta-reclaim.json")));
        }
        else
        {
            await adapter.DiscardDeltaAsync(w, control, ct); await adapter.DiscardDeltaAsync(w, control, ct);
            Assert.False(Directory.Exists(delta)); Assert.True(File.Exists(Path.Combine(control, "discard-delta-reclaim.json")));
        }
        Assert.True(Directory.Exists(f.Project)); Assert.False(f.Backend.SnapshotReleased);
    }
}
