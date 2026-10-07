using System.Text;
using Splinter.Core;
using Splinter.SourceControl.Git;

namespace Splinter.Tests;

public sealed class GitAttributeQueryTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task NulInputMatchesLiteralArgvOracleAcrossUtf8BatchBoundary(bool cached, bool all)
    {
        using var d = new TestDirectory();
        var ct = TestContext.Current.CancellationToken;
        await TestDirectory.Git(d.Path, "init", "-b", "development");
        string attributes = Path.Combine(d.Path, ".gitattributes");
        await File.WriteAllTextAsync(attributes, "*.txt -filter -working-tree-encoding text eol=lf custom=committed\n", ct);
        await TestDirectory.Git(d.Path, "add", ".gitattributes");
        await File.WriteAllTextAsync(attributes, "*.txt filter=unqualified working-tree-encoding=UTF-16 custom=current\n", ct);
        string[] paths = Enumerable.Range(0, 1100)
            .Select(i => $"자료 폴더/{new string('가', 90)} {i:D4}.txt")
            .Concat(OperatingSystem.IsWindows() ? ["-leading.txt", "資料 space.txt"] :
                ["-leading.txt", "資料 space.txt", "quote\"name.txt", "line\nbreak.txt", "tab\tname.txt"])
            .ToArray();
        Assert.True(paths.Sum(path => Encoding.UTF8.GetByteCount(path) + 1) > 256 * 1024);
        var actual = new StringBuilder();
        int outputs = 0;
        await GitWorkspaceProvider.AttributeOutputAsync(d.Path, paths, cached, all,
            output => { outputs++; actual.Append(output); }, ct);
        Assert.Equal(2, outputs);

        var expected = new StringBuilder();
        foreach (string[] batch in paths.Chunk(50))
            expected.Append(await TestDirectory.Git(d.Path,
                ["check-attr", .. cached ? new[] { "--cached" } : [], "-z",
                    .. all ? new[] { "--all" } : new[] { "filter", "working-tree-encoding" }, "--", .. batch]));
        Assert.Equal(expected.ToString(), actual.ToString());
        Assert.Contains(cached ? "committed" : "current", all ? actual.ToString() :
            await QueryAll());

        async Task<string> QueryAll()
        {
            var output = new StringBuilder();
            await GitWorkspaceProvider.AttributeOutputAsync(d.Path, paths.Take(1).ToArray(), cached, true,
                part => output.Append(part), ct);
            return output.ToString();
        }
    }

    [Theory]
    [InlineData(false, "filter=unqualified")]
    [InlineData(true, "filter=unqualified")]
    [InlineData(false, "working-tree-encoding=UTF-16")]
    [InlineData(true, "working-tree-encoding=UTF-16")]
    public async Task CheckpointStillRejectsUnsupportedCurrentOrCachedAttributesBeforeCommit(bool cached, string unsupported)
    {
        using var d = new TestDirectory();
        var ct = TestContext.Current.CancellationToken;
        await TestDirectory.Git(d.Path, "init", "-b", "development");
        await TestDirectory.Git(d.Path, "config", "user.name", "Fixture");
        await TestDirectory.Git(d.Path, "config", "user.email", "fixture@example.invalid");
        const string name = "資料 source.txt";
        string source = Path.Combine(d.Path, name), attributes = Path.Combine(d.Path, ".gitattributes");
        await File.WriteAllTextAsync(source, "original bytes\n", ct);
        await File.WriteAllTextAsync(attributes, "*.txt -filter -working-tree-encoding\n", ct);
        await TestDirectory.Git(d.Path, "add", ".");
        await TestDirectory.Git(d.Path, "commit", "-m", "basis");
        string head = await TestDirectory.Git(d.Path, "rev-parse", "HEAD");
        await File.WriteAllTextAsync(attributes, "*.txt " + unsupported + "\n", ct);
        if (cached)
        {
            await TestDirectory.Git(d.Path, "add", ".gitattributes");
            await File.WriteAllTextAsync(attributes, "*.txt -filter -working-tree-encoding\n", ct);
        }
        string before = await TestDirectory.Git(d.Path, "status", "--porcelain");
        var workspace = new WorkspaceRecord
        {
            Id = "attributes", RequestKey = "attributes", ProjectPath = d.Path, Path = d.Path,
            BaseReference = "development", SourceReference = "development", StorageMode = "git-snapshot", Snapshot = new()
        };
        var error = await Assert.ThrowsAsync<WorkspaceException>(() =>
            new GitWorkspaceProvider().CheckpointAsync(workspace, "must remain uncommitted", ct));
        Assert.Equal("SnapshotFilterUnsupported", error.Code);
        Assert.Equal(head, await TestDirectory.Git(d.Path, "rev-parse", "HEAD"));
        Assert.Equal(before, await TestDirectory.Git(d.Path, "status", "--porcelain"));
        Assert.Equal("original bytes\n", await File.ReadAllTextAsync(source, ct));
    }

    [Fact]
    public async Task EmptyAttributeInputDoesNotLaunchGit()
    {
        bool consumed = false;
        await GitWorkspaceProvider.AttributeOutputAsync(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
            [], false, true, _ => consumed = true, TestContext.Current.CancellationToken);
        Assert.False(consumed);
    }
}
