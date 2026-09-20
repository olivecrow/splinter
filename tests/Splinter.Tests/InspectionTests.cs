using Splinter.Application.Unity;
using Splinter.Core;
using Splinter.Host;
using Splinter.Protocol;
using Splinter.SourceControl.Git;

namespace Splinter.Tests;

public sealed class InspectionTests
{
    [Fact]
    public async Task UnbornRepositoryIsDetectedWithoutCreatingACommit()
    {
        using var directory = new TestDirectory();
        await TestDirectory.Git(directory.Path, "init", "--initial-branch=main");
        var result = await new GitRepositoryInspector().InspectAsync(directory.Path, TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Null(result.Revision);
        Assert.Equal("main", result.Details["branch"]);
        Assert.Equal("false", result.Details["isLinkedWorktree"]);
    }

    [Fact]
    public async Task LinkedWorktreeInspectionAndRemovalPreserveOriginalStagedAndUnstagedChanges()
    {
        using var directory = new TestDirectory();
        string repository = System.IO.Path.Combine(directory.Path, "original");
        string worktree = System.IO.Path.Combine(directory.Path, "작업 with spaces");
        Directory.CreateDirectory(repository);
        await TestDirectory.Git(repository, "init", "--initial-branch=main");
        await TestDirectory.Git(repository, "config", "user.name", "Splinter fixture");
        await TestDirectory.Git(repository, "config", "user.email", "fixture@example.invalid");
        await File.WriteAllTextAsync(System.IO.Path.Combine(repository, "file.txt"), "baseline\n", TestContext.Current.CancellationToken);
        await TestDirectory.Git(repository, "add", "--", "file.txt");
        await TestDirectory.Git(repository, "-c", "commit.gpgsign=false", "commit", "-m", "fixture baseline");
        await File.WriteAllTextAsync(System.IO.Path.Combine(repository, "file.txt"), "staged\n", TestContext.Current.CancellationToken);
        await TestDirectory.Git(repository, "add", "--", "file.txt");
        await File.WriteAllTextAsync(System.IO.Path.Combine(repository, "file.txt"), "unstaged\n", TestContext.Current.CancellationToken);
        string stagedBefore = await TestDirectory.Git(repository, "diff", "--cached", "--binary");
        string headBefore = await TestDirectory.Git(repository, "rev-parse", "HEAD");
        await TestDirectory.Git(repository, "worktree", "add", "--detach", worktree, "HEAD");
        try
        {
            var inspector = new GitRepositoryInspector();
            var original = await inspector.InspectAsync(repository, TestContext.Current.CancellationToken);
            var linked = await inspector.InspectAsync(worktree, TestContext.Current.CancellationToken);
            Assert.NotNull(original);
            Assert.NotNull(linked);
            Assert.Equal(original.RepositoryId, linked.RepositoryId);
            Assert.Equal("true", linked.Details["isLinkedWorktree"]);
            Assert.Equal("2", linked.Details["worktreeCount"]);
            Assert.Equal(headBefore, linked.Revision);
            Assert.Contains("baseline", await File.ReadAllTextAsync(System.IO.Path.Combine(worktree, "file.txt"), TestContext.Current.CancellationToken));
        }
        finally { await TestDirectory.Git(repository, "worktree", "remove", worktree); }
        Assert.Equal(headBefore, await TestDirectory.Git(repository, "rev-parse", "HEAD"));
        Assert.Equal(stagedBefore, await TestDirectory.Git(repository, "diff", "--cached", "--binary"));
        Assert.Equal("unstaged\n", await File.ReadAllTextAsync(System.IO.Path.Combine(repository, "file.txt"), TestContext.Current.CancellationToken));
        Assert.False(Directory.Exists(worktree));
    }

    [Fact]
    public void UnityMetadataIsDetectedFromNestedPathWithoutAnEditor()
    {
        using var directory = new TestDirectory();
        foreach (string child in new[] { "Assets", "Packages", "ProjectSettings" })
            Directory.CreateDirectory(System.IO.Path.Combine(directory.Path, child));
        File.WriteAllText(System.IO.Path.Combine(directory.Path, "ProjectSettings", "ProjectVersion.txt"), "m_EditorVersion: 6000.3.22f1\n");
        File.WriteAllText(System.IO.Path.Combine(directory.Path, "Packages", "manifest.json"), """{"dependencies":{"com.unity.test-framework":"1.6.0"}}""");
        var result = new UnityProjectInspector().Inspect(System.IO.Path.Combine(directory.Path, "Assets"));
        Assert.NotNull(result);
        Assert.Equal("6000.3.22f1", result.Version);
        Assert.Equal("1.6.0", result.Details["testFramework"]);
        Assert.Equal("not-run", result.Details["runtimeVerification"]);
    }

    [Fact]
    public async Task HostInspectionAcceptsNonGitNonUnityProviders()
    {
        using var directory = new TestDirectory();
        var gateway = new FoundationGateway(new ContractCatalog(), new FakeRepository(), new FakeApplication());
        var response = await gateway.DoctorAsync(directory.Path, TestContext.Current.CancellationToken);
        Assert.Equal("Succeeded", response.Status);
        Assert.Contains("fictional-vcs", System.Text.Json.JsonSerializer.Serialize(response, ApiJson.Options));
    }

    private sealed class FakeRepository : IRepositoryInspector
    {
        public Task<RepositoryInspection?> InspectAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult<RepositoryInspection?>(new("fictional-vcs", path, "opaque-repository", "revision-one", new Dictionary<string, string?>()));
    }

    private sealed class FakeApplication : IApplicationInspector
    {
        public ApplicationInspection? Inspect(string path) => new("fictional-app", path, "1", new Dictionary<string, string?>());
    }
}
