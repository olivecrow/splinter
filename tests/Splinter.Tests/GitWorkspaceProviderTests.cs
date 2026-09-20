using Splinter.Core;
using Splinter.SourceControl.Git;

namespace Splinter.Tests;

public sealed class GitWorkspaceProviderTests
{
    [Fact]
    public async Task Creates_isolated_worktree_and_resumes_idempotently()
    {
        var ct = TestContext.Current.CancellationToken; using var d = new TestDirectory(); string repo = await Repo(d.Path, ct); string wt = System.IO.Path.Combine(d.Path, "worker");
        var w = Record(repo, wt, "splinter/work-1"); var p = new GitWorkspaceProvider();
        var created = await p.CreateAsync(w, ct); w.BaseRevision = created.BaseRevision; await File.WriteAllTextAsync(System.IO.Path.Combine(wt, "worker.txt"), "worker", ct);
        Assert.False(File.Exists(System.IO.Path.Combine(repo, "worker.txt")));
        var resumed = await p.CreateAsync(w, ct); Assert.Equal(created.Path, resumed.Path);
        var state = await p.InspectAsync(w, ct); w.Validation = new("test", state.Fingerprint, true, DateTimeOffset.UtcNow, []); w.ResultRevision = await p.CommitAsync(w, "worker result", ct);
        var unintegrated = await Assert.ThrowsAsync<WorkspaceException>(() => p.ReleaseAsync(w, ct)); Assert.Equal("GitReleaseResultNotIntegrated", unintegrated.Code);
        var integrated = await p.IntegrateAsync(w, "main", ct); w.IntegratedRevision = integrated.Revision; await p.ReleaseAsync(w, ct);
    }

    [Fact]
    public async Task Rejects_dirty_original_and_dirty_release()
    {
        var ct = TestContext.Current.CancellationToken; using var d = new TestDirectory(); string repo = await Repo(d.Path, ct); string wt = System.IO.Path.Combine(d.Path, "worker");
        var w = Record(repo, wt, "splinter/work-2"); var p = new GitWorkspaceProvider();
        await File.WriteAllTextAsync(System.IO.Path.Combine(repo, "dirty.txt"), "dirty", ct);
        var ex = await Assert.ThrowsAsync<WorkspaceException>(() => p.CreateAsync(w, ct)); Assert.Equal("GitDirtyWorktree", ex.Code);
        File.Delete(System.IO.Path.Combine(repo, "dirty.txt")); var created = await p.CreateAsync(w, ct); w.BaseRevision = created.BaseRevision;
        await File.WriteAllTextAsync(System.IO.Path.Combine(wt, "dirty.txt"), "dirty", ct);
        ex = await Assert.ThrowsAsync<WorkspaceException>(() => p.ReleaseAsync(w, ct)); Assert.Equal("GitDirtyWorktree", ex.Code);
    }

    [Fact]
    public async Task Commits_integrates_and_releases()
    {
        var ct = TestContext.Current.CancellationToken; using var d = new TestDirectory(); string repo = await Repo(d.Path, ct); string wt = System.IO.Path.Combine(d.Path, "worker");
        var w = Record(repo, wt, "splinter/work-3"); var p = new GitWorkspaceProvider(); var created = await p.CreateAsync(w, ct); w.BaseRevision = created.BaseRevision;
        await File.WriteAllTextAsync(System.IO.Path.Combine(wt, "result.txt"), "result", ct); var state = await p.InspectAsync(w, ct); w.Validation = new("test", state.Fingerprint, true, DateTimeOffset.UtcNow, []); string result = await p.CommitAsync(w, "result", ct); w.ResultRevision = result;
        var integrated = await p.IntegrateAsync(w, "main", ct); Assert.False(integrated.AlreadyIntegrated);
        Assert.Equal(result, await TestDirectory.Git(repo, "merge-base", result, integrated.Revision));
        w.IntegratedRevision = integrated.Revision; await p.ReleaseAsync(w, ct);
        Assert.False(Directory.Exists(wt));
    }

    [Fact]
    public async Task Deleted_file_fingerprint_survives_stage_commit_and_release()
    {
        var ct = TestContext.Current.CancellationToken; using var d = new TestDirectory(); var repo = await Repo(d.Path, ct); var wt = Path.Combine(d.Path, "deleted"); var w = Record(repo, wt, "splinter/deleted"); var p = new GitWorkspaceProvider(); var made = await p.CreateAsync(w, ct); w.BaseRevision = made.BaseRevision;
        File.Delete(Path.Combine(wt, "file.txt")); var state = await p.InspectAsync(w, ct); w.Validation = new("delete", state.Fingerprint, true, DateTimeOffset.UtcNow, []);
        w.ResultRevision = await p.CommitAsync(w, "remove file", ct); var integrated = await p.IntegrateAsync(w, "main", ct); w.IntegratedRevision = integrated.Revision; await p.ReleaseAsync(w, ct); Assert.False(File.Exists(Path.Combine(repo, "file.txt")));
    }

    [Fact]
    public async Task Commit_rejects_stale_or_missing_validation_without_commit()
    {
        var ct = TestContext.Current.CancellationToken; using var d = new TestDirectory(); var repo = await Repo(d.Path, ct); var wt = Path.Combine(d.Path, "stale"); var w = Record(repo, wt, "splinter/stale"); var p = new GitWorkspaceProvider(); var made = await p.CreateAsync(w, ct); w.BaseRevision = made.BaseRevision; await File.WriteAllTextAsync(Path.Combine(wt, "new.txt"), "new", ct); var before = await p.InspectAsync(w, ct);
        await Assert.ThrowsAsync<WorkspaceException>(() => p.CommitAsync(w, "bad", ct)); Assert.Equal(before.Revision, await TestDirectory.Git(wt, "rev-parse", "HEAD"));
        w.Validation = new("stale", "wrong", true, DateTimeOffset.UtcNow, []); await Assert.ThrowsAsync<WorkspaceException>(() => p.CommitAsync(w, "bad", ct)); Assert.Equal(before.Revision, await TestDirectory.Git(wt, "rev-parse", "HEAD"));
    }

    [Fact]
    public async Task Commit_skips_ignored_caches_and_untracked_managed_bridge()
    {
        var ct = TestContext.Current.CancellationToken;
        using var directory = new TestDirectory();
        string repo = await Repo(directory.Path, ct);
        await File.WriteAllTextAsync(Path.Combine(repo, ".gitignore"), "Library/\nTemp/\nLogs/\nUserSettings/\n", ct);
        await TestDirectory.Git(repo, "add", ".gitignore");
        await TestDirectory.Git(repo, "commit", "-m", "ignore Unity caches");
        var workspace = Record(repo, Path.Combine(directory.Path, "worker"), "splinter/managed");
        workspace.ManagedPaths = ["Library", "Temp", "Logs", "UserSettings", "Assets/__Splinter__"];
        var provider = new GitWorkspaceProvider();
        workspace.BaseRevision = (await provider.CreateAsync(workspace, ct)).BaseRevision;
        Directory.CreateDirectory(Path.Combine(workspace.Path, "Library"));
        Directory.CreateDirectory(Path.Combine(workspace.Path, "Assets", "__Splinter__"));
        await File.WriteAllTextAsync(Path.Combine(workspace.Path, "Library", "cache"), "cache", ct);
        await File.WriteAllTextAsync(Path.Combine(workspace.Path, "Assets", "__Splinter__", "bridge.cs"), "generated", ct);
        await File.WriteAllTextAsync(Path.Combine(workspace.Path, "script.cs"), "source", ct);
        var source = await provider.InspectAsync(workspace, ct);
        workspace.Validation = new("compile", source.Fingerprint, true, DateTimeOffset.UtcNow, []);
        workspace.ResultRevision = await provider.CommitAsync(workspace, "source only", ct);
        Assert.Equal("script.cs", await TestDirectory.Git(workspace.Path, "diff-tree", "--no-commit-id", "--name-only", "-r", workspace.ResultRevision));
        Assert.True(File.Exists(Path.Combine(workspace.Path, "Library", "cache")));
        Assert.False((await provider.InspectAsync(workspace, ct)).HasChanges);
    }

    [Fact]
    public async Task Conflict_preserves_target_and_worker_result()
    {
        var ct = TestContext.Current.CancellationToken; using var d = new TestDirectory(); string repo = await Repo(d.Path, ct); string wt = System.IO.Path.Combine(d.Path, "worker");
        var w = Record(repo, wt, "splinter/work-4"); var p = new GitWorkspaceProvider(); var created = await p.CreateAsync(w, ct); w.BaseRevision = created.BaseRevision;
        await File.WriteAllTextAsync(System.IO.Path.Combine(wt, "file.txt"), "worker", ct); var state = await p.InspectAsync(w, ct); w.Validation = new("test", state.Fingerprint, true, DateTimeOffset.UtcNow, []); w.ResultRevision = await p.CommitAsync(w, "worker", ct);
        await File.WriteAllTextAsync(System.IO.Path.Combine(repo, "file.txt"), "target", ct); await TestDirectory.Git(repo, "add", "file.txt"); await TestDirectory.Git(repo, "commit", "-m", "target");
        var ex = await Assert.ThrowsAsync<WorkspaceException>(() => p.IntegrateAsync(w, "main", ct)); Assert.Equal("IntegrationConflict", ex.Code);
        Assert.Equal("target", await File.ReadAllTextAsync(System.IO.Path.Combine(repo, "file.txt"), ct));
        Assert.Equal("worker", await File.ReadAllTextAsync(System.IO.Path.Combine(wt, "file.txt"), ct));
    }

    [Fact]
    public async Task Release_removes_long_generated_paths_without_changing_user_configuration()
    {
        var ct = TestContext.Current.CancellationToken;
        using var directory = new TestDirectory();
        string repo = await Repo(directory.Path, ct);
        await File.WriteAllTextAsync(Path.Combine(repo, ".gitignore"), "Library/\n", ct);
        await TestDirectory.Git(repo, "add", ".gitignore");
        await TestDirectory.Git(repo, "commit", "-m", "ignore caches");
        await TestDirectory.Git(repo, "config", "core.longpaths", "false");
        var workspace = Record(repo, Path.Combine(directory.Path, "worker"), "splinter/long-paths");
        workspace.ManagedPaths = ["Library"];
        var provider = new GitWorkspaceProvider();
        workspace.BaseRevision = (await provider.CreateAsync(workspace, ct)).BaseRevision;
        string cache = Path.Combine(workspace.Path, "Library", new string('a', 100), new string('b', 100), new string('c', 100));
        Directory.CreateDirectory(cache);
        await File.WriteAllTextAsync(Path.Combine(cache, "compiled.cache"), "generated", ct);

        await provider.ReleaseAsync(workspace, ct);

        Assert.False(Directory.Exists(workspace.Path));
        Assert.Equal("false", await TestDirectory.Git(repo, "config", "core.longpaths"));
    }

    [Fact]
    public async Task Partial_removal_preserves_unknown_or_changed_source_then_resumes_only_collected_files()
    {
        var ct = TestContext.Current.CancellationToken;
        using var directory = new TestDirectory();
        string repo = await Repo(directory.Path, ct);
        var workspace = Record(repo, Path.Combine(directory.Path, "worker"), "splinter/partial-removal");
        workspace.ManagedPaths = ["Library"];
        var provider = new GitWorkspaceProvider();
        workspace.BaseRevision = (await provider.CreateAsync(workspace, ct)).BaseRevision;
        await File.WriteAllTextAsync(Path.Combine(workspace.Path, "result.txt"), "collected", ct);
        var source = await provider.InspectAsync(workspace, ct);
        workspace.Validation = new("compile", source.Fingerprint, true, DateTimeOffset.UtcNow, []);
        workspace.ResultRevision = await provider.CommitAsync(workspace, "result", ct);
        workspace.IntegratedRevision = (await provider.IntegrateAsync(workspace, "main", ct)).Revision;
        await TestDirectory.Git(repo, "worktree", "remove", workspace.Path);

        // Reproduce the filesystem state left when Git unregisters first and
        // then fails during recursive deletion, including a later user edit.
        Directory.CreateDirectory(Path.Combine(workspace.Path, "Library"));
        await File.WriteAllTextAsync(Path.Combine(workspace.Path, "Library", "cache"), "generated", ct);
        await File.WriteAllTextAsync(Path.Combine(workspace.Path, "result.txt"), "later edit", ct);
        var missingIntent = await Assert.ThrowsAsync<WorkspaceException>(() => provider.ReleaseAsync(workspace, ct));
        Assert.Equal("GitWorkspaceMismatch", missingIntent.Code);
        workspace.Phase = "releasing";
        var changed = await Assert.ThrowsAsync<WorkspaceException>(() => provider.ReleaseAsync(workspace, ct));
        Assert.Equal("UncollectedChanges", changed.Code);
        Assert.Equal("later edit", await File.ReadAllTextAsync(Path.Combine(workspace.Path, "result.txt"), ct));
        await File.WriteAllTextAsync(Path.Combine(workspace.Path, "result.txt"), "collected", ct);
        await File.WriteAllTextAsync(Path.Combine(workspace.Path, "unknown.txt"), "unique", ct);
        var unknown = await Assert.ThrowsAsync<WorkspaceException>(() => provider.ReleaseAsync(workspace, ct));
        Assert.Equal("UncollectedChanges", unknown.Code);
        Assert.True(File.Exists(Path.Combine(workspace.Path, "unknown.txt")));
        File.Delete(Path.Combine(workspace.Path, "unknown.txt"));

        await provider.ReleaseAsync(workspace, ct);

        Assert.False(Directory.Exists(workspace.Path));
        Assert.Equal(workspace.ResultRevision, await TestDirectory.Git(repo, "rev-parse", workspace.SourceReference));
    }

    private static WorkspaceRecord Record(string repo, string path, string branch) => new() { Id = Guid.NewGuid().ToString(), RequestKey = Guid.NewGuid().ToString(), ProjectPath = repo, Path = path, SourceReference = branch, BaseReference = "main" };
    private static async Task<string> Repo(string root, CancellationToken ct)
    {
        root = System.IO.Path.Combine(root, "repository"); Directory.CreateDirectory(root);
        await TestDirectory.Git(root, "init", "--initial-branch=main");
        await TestDirectory.Git(root, "config", "user.name", "Splinter fixture"); await TestDirectory.Git(root, "config", "user.email", "fixture@example.invalid");
        await File.WriteAllTextAsync(System.IO.Path.Combine(root, "file.txt"), "base", ct); await TestDirectory.Git(root, "add", "."); await TestDirectory.Git(root, "commit", "-m", "base"); return root;
    }
}
