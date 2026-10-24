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
    [InlineData("complete", "append")][InlineData("journal-removed", "append")][InlineData("directory-removed", "append")]
    [InlineData("unknown-file", "append")][InlineData("changed-journal", "append")][InlineData("unknown-resources", "append")]
    [InlineData("complete", "latest")][InlineData("journal-removed", "latest")][InlineData("directory-removed", "latest")]
    [InlineData("unknown-file", "latest")][InlineData("changed-journal", "latest")][InlineData("unknown-resources", "latest")]
    [InlineData("complete", "bounded")][InlineData("journal-removed", "bounded")][InlineData("directory-removed", "bounded")]
    [InlineData("unknown-file", "bounded")][InlineData("changed-journal", "bounded")][InlineData("unknown-resources", "bounded")]
    [InlineData("intent-flushed", "bounded")][InlineData("guard-removed", "bounded")]
    public async Task WindowsDiscardDeletesOnlySealedOwnedDeltaAndResumesPartialRemoval(string variant, string mode)
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure(ct)).Workspace; var store = new WorkspaceStore(f.Home);
        string control = store.ControlPath(w.Id), delta = Path.Combine(control, "delta");
        await f.Backend.UnmountAsync(w, control, ct);
        Directory.CreateDirectory(delta);
        var identity = new OverlayIdentity(2, w.Id, w.Snapshot!.Reference!, w.Snapshot.MaxJournalBytes, 1024 * 1024, [".git"]);
        var tree = new SnapshotOverlayTests.FakeSnapshotTree { Snapshot = w.Snapshot.Reference! };
        SnapshotDeltaSeal seal;
        bool optimized = mode == "latest";
        using (var journal = new JournalDeltaStore(delta, identity, tree, optimizeJournal: optimized, boundedLogBytes: mode == "bounded" ? 8192 : null))
        {
            var file = journal.Create("file.txt", false, 32); journal.Write(file.Id, 0, "failed-task"u8);
            Assert.Equal(optimized, journal.JournalOptimizationEnabled); seal = journal.Seal();
        }
        w.Phase = "discarding"; w.Snapshot!.Step = "discard-delta";
        w.Discard = new() { Step = "delta", Seal = seal, Intent = new(1, w.Id, "discard", w.RequestDigest!, w.ProjectPath,
            w.Path, w.SourceReference, w.BaseRevision!, "branch-head", w.Snapshot.Reference!, DateTimeOffset.UtcNow) };
        var registration = new OverlayGitReceipt(w.Id, w.ProjectPath, w.Path, w.SourceReference, w.BaseRevision!, Path.Combine(f.Project, ".git", "worktrees", "a"), "pointer");
        await WindowsProcessWorkspaceProvider.WriteAsync(Path.Combine(control, "git-discard.json"), new GitDiscardReceipt(w.Discard.Intent, registration, true), ct);
        if (variant is "intent-flushed" or "journal-removed" or "guard-removed" or "directory-removed")
        {
            await WindowsProcessWorkspaceProvider.WriteAsync(Path.Combine(control, "discard-delta-owner.json"), w.Discard, ct);
            if (variant != "directory-removed")
            {
                Assert.Throws<IOException>(() => JournalReclamation.Reclaim(delta, seal, ["identity.json"], Path.Combine(control, "discard-delta-reclaim.json"),
                    boundary => { if (boundary == variant) throw new IOException("lost reply"); }));
            }
            else JournalReclamation.Reclaim(delta, seal, ["identity.json"], Path.Combine(control, "discard-delta-reclaim.json"));
        }
        if (variant == "unknown-file") await File.WriteAllTextAsync(Path.Combine(delta, "keep.txt"), "unowned", ct);
        if (variant == "changed-journal") await File.WriteAllBytesAsync(Path.Combine(delta, "delta.journal"), [4, 3, 2, 1], ct);
        if (variant == "unknown-resources") f.Backend.UnknownResources = true;
        var adapter = new WindowsWorkspaceDiscardProvider(new GitWorkspaceProvider(), f.Backend);
        string reclamationReceipt = Path.Combine(control, "discard-delta-reclaim.json") + (mode == "append" ? "" : "." + mode);
        if (variant is "unknown-file" or "changed-journal" or "unknown-resources")
        {
            await Assert.ThrowsAsync<WorkspaceException>(() => adapter.DiscardDeltaAsync(w, control, ct));
            Assert.True(File.Exists(Path.Combine(delta, "delta.journal")));
            Assert.False(File.Exists(reclamationReceipt));
        }
        else
        {
            await adapter.DiscardDeltaAsync(w, control, ct); await adapter.DiscardDeltaAsync(w, control, ct);
            Assert.False(Directory.Exists(delta)); Assert.True(File.Exists(reclamationReceipt));
        }
        Assert.True(Directory.Exists(f.Project)); Assert.False(f.Backend.SnapshotReleased);
    }
}
