using System.Text.Json;
using Splinter.Core;
using Splinter.SourceControl.Git;

namespace Splinter.Tests;

public sealed partial class GitSnapshotWorkspaceTests
{
    [Theory]
    [InlineData("new")][InlineData("unlock")][InlineData("registration")][InlineData("branch")]
    public async Task RealGitDiscardResumesAndKeepsOriginalPeerAndIndependentResult(string boundary)
    {
        using var d = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        string source = Path.Combine(d.Path, "source"), control = Path.Combine(d.Path, "control");
        Directory.CreateDirectory(source); Directory.CreateDirectory(control);
        await TestDirectory.Git(source, "init", "-b", "development");
        await TestDirectory.Git(source, "config", "user.name", "Fixture");
        await TestDirectory.Git(source, "config", "user.email", "fixture@example.invalid");
        await File.WriteAllTextAsync(Path.Combine(source, "file.txt"), "baseline", ct);
        await TestDirectory.Git(source, "add", "."); await TestDirectory.Git(source, "commit", "-m", "baseline");
        string revision = await TestDirectory.Git(source, "rev-parse", "HEAD");
        var w = new WorkspaceRecord { Id = new('a', 24), RequestKey = "discard", RequestDigest = new('b', 24), ProjectPath = source,
            Path = Path.Combine(d.Path, "mount-a"), SourceReference = "splinter/" + new string('a', 24), BaseReference = "development", BaseRevision = revision,
            Phase = "ready", StorageMode = "ntfs-snapshot", Snapshot = new() { Reference = new("snapshot", "fake", source, "immutable", DateTimeOffset.UtcNow, true, true, "fake") } };
        var git = new GitWorkspaceProvider(); await git.RegisterAsync(w, control, ct);
        var registration = await GitOverlayRegistration.ReadAsync(Path.Combine(control, "git-overlay.json"), ct);
        var peer = await GitOverlayRegistration.PrepareAsync(source, Path.Combine(d.Path, "mount-b"), "splinter/b", revision,
            "peer", Path.Combine(d.Path, "peer.json"), ct);
        string preserved = "refs/splinter/snapshot-results/" + w.Id;
        await TestDirectory.Git(source, "update-ref", preserved, revision);
        Assert.Equal(revision, await git.ObserveDiscardRevisionAsync(w, control, ct));
        w.Phase = "discarding"; w.Snapshot.Step = "discard-source";
        w.Discard = new() { Step = "source", Seal = new(0, 0, new('c', 64)),
            Intent = new(1, w.Id, "delete-a", w.RequestDigest, source, w.Path, w.SourceReference, revision, revision, w.Snapshot.Reference!, DateTimeOffset.UtcNow) };
        if (boundary != "new")
        {
            await File.WriteAllTextAsync(Path.Combine(control, "git-discard.json"), JsonSerializer.Serialize(new GitDiscardReceipt(w.Discard.Intent, registration, false), new JsonSerializerOptions(JsonSerializerDefaults.Web)), ct);
            await TestDirectory.Git(source, "worktree", "unlock", w.Path);
            if (boundary is "registration" or "branch") await TestDirectory.Git(source, "worktree", "remove", "--force", w.Path);
            if (boundary == "branch") await TestDirectory.Git(source, "update-ref", "-d", "refs/heads/" + w.SourceReference, revision);
        }
        await File.WriteAllTextAsync(Path.Combine(source, "file.txt"), "original still editing", ct);
        await git.DiscardSourceAsync(w, control, ct); await git.DiscardSourceAsync(w, control, ct);
        Assert.False(Directory.Exists(registration.GitDirectory));
        Assert.Equal("", await TestDirectory.Git(source, "for-each-ref", "--format=%(refname)", "refs/heads/" + w.SourceReference));
        Assert.Equal(revision, await TestDirectory.Git(source, "rev-parse", preserved));
        Assert.Equal(revision, await TestDirectory.Git(source, "rev-parse", "HEAD"));
        Assert.Equal("original still editing", await File.ReadAllTextAsync(Path.Combine(source, "file.txt"), ct));
        await GitOverlayRegistration.VerifyAsync(peer, ct);
        var receipt = JsonSerializer.Deserialize<GitDiscardReceipt>(await File.ReadAllTextAsync(Path.Combine(control, "git-discard.json"), ct), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.True(receipt.Completed);
        var replacement = w.Discard.Intent with { IntentKey = "other" };
        Assert.Equal("DiscardIntentMismatch", (await Assert.ThrowsAsync<WorkspaceException>(() =>
            GitOverlayRegistration.DiscardAsync(registration, replacement, Path.Combine(control, "git-discard.json"), ct))).Code);
        await TestDirectory.Git(source, "update-ref", "refs/heads/" + w.SourceReference, revision);
        Assert.Equal("DiscardSourceChanged", (await Assert.ThrowsAsync<WorkspaceException>(() => git.DiscardSourceAsync(w, control, ct))).Code);
        Assert.Equal(revision, await TestDirectory.Git(source, "rev-parse", "refs/heads/" + w.SourceReference));
    }
}
