using Splinter.Core;
using Splinter.SourceControl.Git;

namespace Splinter.Tests;

public sealed class GitStatusSummaryTests
{
    [Fact]
    public async Task StatusDrainsAllChangedEntriesAndRenameCompanionsWhileKeepingOnlyABoundedPreview()
    {
        using var files = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        await TestDirectory.Git(files.Path, "init", "-b", "development");
        await TestDirectory.Git(files.Path, "config", "user.name", "Fixture"); await TestDirectory.Git(files.Path, "config", "user.email", "fixture@example.invalid");
        File.WriteAllText(Path.Combine(files.Path, "original name.txt"), "unchanged payload");
        await TestDirectory.Git(files.Path, "add", "."); await TestDirectory.Git(files.Path, "commit", "-m", "basis");
        File.Move(Path.Combine(files.Path, "original name.txt"), Path.Combine(files.Path, "renamed name.txt")); await TestDirectory.Git(files.Path, "add", "-A");
        for (int i = 0; i < 512; i++) File.WriteAllText(Path.Combine(files.Path, "untracked-" + i.ToString("D4") + ".txt"), "new");
        string head = await TestDirectory.Git(files.Path, "rev-parse", "HEAD"); byte[] index = File.ReadAllBytes(Path.Combine(files.Path, ".git", "index"));
        var status = GitWorkspaceProvider.ObserveStatus(files.Path, ct);
        Assert.Equal(513, status.Entries); Assert.True(status.Truncated); Assert.True(status.Preview.Length <= 16384);
        Assert.Contains("renamed name.txt", status.Preview); Assert.Contains("original name.txt", status.Preview);
        var drained = GitWorkspaceProvider.ObserveStatus(files.Path, ct, preview: false); Assert.Equal(status.Entries, drained.Entries); Assert.Empty(drained.Preview);
        var w = new WorkspaceRecord { Id = new string('a', 24), RequestKey = "dirty", ProjectPath = files.Path,
            Path = Path.Combine(Path.GetDirectoryName(files.Path)!, "uncreated-" + Guid.NewGuid().ToString("N")), SourceReference = "codex/dirty", BaseReference = "HEAD", StorageMode = "git-snapshot" };
        Assert.Equal("GitDirtyWorktree", (await Assert.ThrowsAsync<WorkspaceException>(() => new GitWorkspaceProvider().PreflightAsync(w, ct))).Code);
        Assert.Equal(index, File.ReadAllBytes(Path.Combine(files.Path, ".git", "index"))); Assert.Equal(head, await TestDirectory.Git(files.Path, "rev-parse", "HEAD")); Assert.False(Directory.Exists(w.Path));
    }
}
