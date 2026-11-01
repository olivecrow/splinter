using Splinter.Core;
using Splinter.SourceControl.Git;

namespace Splinter.Tests;

public sealed class GitAttachmentRecoveryTests
{
    [Fact]
    public async Task ReattachedOwnedGitRegistrationKeepsPrivateEditsAndRejectsIndexLocksOrAChangedTaskHead()
    {
        using var files = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        string project = Path.Combine(files.Path, "source"), control = Path.Combine(files.Path, "control"), mount = Path.Combine(files.Path, "mount");
        Directory.CreateDirectory(project); Directory.CreateDirectory(control);
        await TestDirectory.Git(project, "init", "-b", "development"); await TestDirectory.Git(project, "config", "user.name", "Fixture");
        await TestDirectory.Git(project, "config", "user.email", "fixture@example.invalid"); await TestDirectory.Git(project, "config", "core.autocrlf", "false");
        File.WriteAllText(Path.Combine(project, "file.txt"), "basis\n"); await TestDirectory.Git(project, "add", "."); await TestDirectory.Git(project, "commit", "-m", "basis");
        var w = new WorkspaceRecord { Id = new string('a', 24), RequestKey = "reconnect", ProjectPath = project, Path = mount,
            BaseReference = "HEAD", SourceReference = "codex/continuing", StorageMode = "git-snapshot", Snapshot = new() };
        var provider = new GitWorkspaceProvider(); w.BaseRevision = await provider.PreflightAsync(w, ct);
        w.Snapshot.Reference = new("fixed", "fake", project, "immutable", DateTimeOffset.UtcNow, true, true, "fixture only");
        byte[] originalIndex = File.ReadAllBytes(Path.Combine(project, ".git", "index"));
        await provider.RegisterAsync(w, control, ct); Directory.CreateDirectory(mount); File.WriteAllText(Path.Combine(mount, "file.txt"), "basis\n");
        await provider.AttachAsync(w, control, ct);
        // This models the source-provider proof already performed by Runtime;
        // it is real Git registration evidence, not a WinFsp qualification.
        w.Snapshot.Attached = true; w.Snapshot.Mount = new(w.Id, mount, "fixture", w.Snapshot.Reference, 123, 1);
        w.Snapshot.Observation = new(true, true, true, true, 0, 0, 0);
        File.WriteAllText(Path.Combine(mount, "file.txt"), "private edit\n"); File.WriteAllText(Path.Combine(mount, "new.txt"), "private new file\n");
        string staged = await TestDirectory.Git(mount, "ls-files", "--stage"); await provider.AttachAsync(w, control, ct);
        Assert.Equal(staged, await TestDirectory.Git(mount, "ls-files", "--stage")); Assert.Equal(w.BaseRevision, await TestDirectory.Git(mount, "rev-parse", "HEAD"));
        Assert.Equal("private edit\n", File.ReadAllText(Path.Combine(mount, "file.txt"))); Assert.Equal(originalIndex, File.ReadAllBytes(Path.Combine(project, ".git", "index")));
        var receipt = await GitOverlayRegistration.ReadAsync(Path.Combine(control, "git-overlay.json"), ct);
        string index = Path.Combine(receipt.GitDirectory, "index"), indexLock = index + ".lock"; byte[] before = File.ReadAllBytes(index); File.WriteAllText(indexLock, "preserve lock");
        Assert.Equal("GitCommandFailed", (await Assert.ThrowsAsync<WorkspaceException>(() => provider.AttachAsync(w, control, ct))).Code);
        Assert.Equal("preserve lock", File.ReadAllText(indexLock)); Assert.Equal(before, File.ReadAllBytes(index)); File.Delete(indexLock);
        string tree = await TestDirectory.Git(project, "rev-parse", "HEAD^{tree}");
        string foreign = await TestDirectory.Git(project, "commit-tree", tree, "-p", w.BaseRevision!, "-m", "foreign task head");
        await TestDirectory.Git(project, "update-ref", "refs/heads/" + w.SourceReference, foreign, w.BaseRevision!);
        Assert.Equal("GitOverlayResultChanged", (await Assert.ThrowsAsync<WorkspaceException>(() => provider.AttachAsync(w, control, ct))).Code);
        Assert.Equal(foreign, await TestDirectory.Git(project, "rev-parse", "refs/heads/" + w.SourceReference)); Assert.Equal(before, File.ReadAllBytes(index));
        Assert.Equal(w.BaseRevision, await TestDirectory.Git(project, "rev-parse", "HEAD")); Assert.Equal(originalIndex, File.ReadAllBytes(Path.Combine(project, ".git", "index")));
    }
}
