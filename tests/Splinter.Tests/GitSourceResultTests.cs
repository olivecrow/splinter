using System.Text;
using Splinter.SourceControl.Git;

namespace Splinter.Tests;

public sealed class GitSourceResultTests
{
    [Fact]
    public async Task FrozenResultSurvivesSnapshotLossAndPreservesMutableIndexAndWorkingTree()
    {
        using var dir = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        await TestDirectory.Git(dir.Path, "init", "-b", "development");
        await TestDirectory.Git(dir.Path, "config", "user.name", "Fixture");
        await TestDirectory.Git(dir.Path, "config", "user.email", "fixture@example.invalid");
        await File.WriteAllTextAsync(Path.Combine(dir.Path, "unchanged.txt"), "base\n", ct);
        await File.WriteAllTextAsync(Path.Combine(dir.Path, "deleted.txt"), "delete me\n", ct);
        await TestDirectory.Git(dir.Path, "add", "."); await TestDirectory.Git(dir.Path, "commit", "-m", "base");
        string basis = await TestDirectory.Git(dir.Path, "rev-parse", "HEAD");
        await File.WriteAllTextAsync(Path.Combine(dir.Path, "unchanged.txt"), "mutable later change\n", ct);
        await TestDirectory.Git(dir.Path, "add", "unchanged.txt");
        byte[] index = await File.ReadAllBytesAsync(Path.Combine(dir.Path, ".git", "index"), ct);
        var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        tree.Files["unchanged.txt"] = "base\n"u8.ToArray(); tree.Files["Assets/new.txt"] = "frozen\r\n"u8.ToArray();
        tree.Files["Library/private"] = "excluded-cache"u8.ToArray();
        string operation = new('a', 24);
        var result = await GitSourceResult.PreserveAsync(dir.Path, basis, operation, tree,
            ["Assets", "deleted.txt"], ["Library"], ct);
        Assert.Equal(result, await GitSourceResult.PreserveAsync(dir.Path, basis, operation, tree, ["Assets", "deleted.txt"], ["Library"], ct));
        Assert.Equal(index, await File.ReadAllBytesAsync(Path.Combine(dir.Path, ".git", "index"), ct));
        Assert.Equal("mutable later change\n", await File.ReadAllTextAsync(Path.Combine(dir.Path, "unchanged.txt"), ct));
        tree.Available = false;
        Assert.Equal(result.Hash, await GitSourceResult.VerifyAsync(dir.Path, result.Reference, ct));
        Assert.Equal("Assets/new.txt\nunchanged.txt", (await TestDirectory.Git(dir.Path, "ls-tree", "-r", "--name-only", result.Reference)).Replace("\r\n", "\n"));
        Assert.Equal("base", await TestDirectory.Git(dir.Path, "show", result.Reference + ":unchanged.txt"));
        Assert.Equal("frozen", await TestDirectory.Git(dir.Path, "show", result.Reference + ":Assets/new.txt"));
        Assert.Equal(16, tree.ReadBytes); // two retries read only the new eight-byte file.
    }
}
