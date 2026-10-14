using Splinter.Core;
using Splinter.SourceControl.Git;

namespace Splinter.Tests;

public sealed class GitSourcePathInspectionTests
{
    [Fact]
    public void GenerationBoundDirectoryReuseStillChecksEveryLeafAndNativeAncestor()
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "inspection-mount"));
        string nested = Path.Combine(root, "Assets", "Code");
        string nativeParent = Path.GetDirectoryName(root)!;
        var queries = new Dictionary<string, int>();
        string? reparse = null;
        var paths = new GitSourcePathInspection(root, journalBound: true, readAttributes: path =>
        {
            queries[path] = queries.GetValueOrDefault(path) + 1;
            return path == reparse ? FileAttributes.ReparsePoint : path.EndsWith(".cs", StringComparison.Ordinal)
                ? FileAttributes.Normal : FileAttributes.Directory;
        });
        paths.Inspect(Path.Combine(nested, "A.cs"));
        paths.Inspect(Path.Combine(nested, "B.cs"));
        Assert.Equal(1, queries[nested]);
        Assert.Equal(1, queries[Path.Combine(root, "Assets")]);
        Assert.Equal(2, queries[nativeParent]);
        Assert.False(queries.ContainsKey(root));

        reparse = Path.Combine(nested, "B.cs");
        Assert.Equal("GitSymlinkRejected", Assert.Throws<WorkspaceException>(() => paths.Inspect(reparse)).Code);
        reparse = nativeParent;
        Assert.Equal("GitSymlinkRejected", Assert.Throws<WorkspaceException>(() => paths.Inspect(Path.Combine(nested, "C.cs"))).Code);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void ReuseRequiresBothObservedMountAndJournalBarrier(bool mount, bool journal)
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "inspection-mount"));
        string nested = Path.Combine(root, "Assets");
        bool changed = false;
        var paths = new GitSourcePathInspection(mount ? root : null, journal, path =>
            changed && path == nested ? FileAttributes.Directory | FileAttributes.ReparsePoint : FileAttributes.Directory);
        paths.Inspect(Path.Combine(nested, "A")); changed = true;
        Assert.Equal("GitSymlinkRejected", Assert.Throws<WorkspaceException>(() => paths.Inspect(Path.Combine(nested, "B"))).Code);
    }

    [Fact]
    public void BoundedReuseFallsBackToFreshQueriesAndDoesNotPersistAcrossInspections()
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "inspection-mount"));
        var queries = new Dictionary<string, int>();
        FileAttributes? Read(string path)
        {
            queries[path] = queries.GetValueOrDefault(path) + 1;
            return FileAttributes.Directory;
        }
        var paths = new GitSourcePathInspection(root, true, Read);
        for (int i = 0; i <= GitSourcePathInspection.MaxCachedDirectories; i++)
            paths.Inspect(Path.Combine(root, i.ToString(), "A"));
        string retained = Path.Combine(root, "0");
        string excess = Path.Combine(root, GitSourcePathInspection.MaxCachedDirectories.ToString());
        paths.Inspect(Path.Combine(retained, "B")); paths.Inspect(Path.Combine(excess, "B"));
        Assert.Equal(1, queries[retained]); Assert.Equal(2, queries[excess]);
        new GitSourcePathInspection(root, true, Read).Inspect(Path.Combine(retained, "C"));
        Assert.Equal(2, queries[retained]);
    }

    [Fact]
    public void MissingPathsAreAllowedButDoNotHideReparseAncestorsOrAccessErrors()
    {
        using var dir = new TestDirectory();
        var paths = new GitSourcePathInspection(null);
        Assert.Null(paths.Inspect(Path.Combine(dir.Path, "missing", "file")));
        var denied = new GitSourcePathInspection(null, readAttributes: _ => throw new UnauthorizedAccessException());
        Assert.Throws<UnauthorizedAccessException>(() => denied.Inspect(dir.Path));
    }

    [Fact]
    public void ActualNestedJunctionIsRejectedEvenWhenTheRequestedFileIsMissing()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var dir = new TestDirectory();
        string target = Path.Combine(dir.Path, "target"); Directory.CreateDirectory(target);
        string root = Path.Combine(dir.Path, "root"); Directory.CreateDirectory(root);
        string link = Path.Combine(root, "link"); Directory.CreateDirectory(link); OwnedDirectoryLifetimeTests.SetJunction(link, target);
        try
        {
            Assert.Equal("GitSymlinkRejected", Assert.Throws<WorkspaceException>(() =>
                new GitSourcePathInspection(root, true).Inspect(Path.Combine(link, "missing"))).Code);
        }
        finally { Directory.Delete(link); }
    }
}

public sealed partial class SourceFingerprintTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MountScopedPathReuseCannotPublishAcrossDirectoryChangesOrProviderRestart(bool restart)
    {
        using var f = await Fixture.Create(); var ct = TestContext.Current.CancellationToken;
        string directory = f.Path("Assets/Code"); Directory.CreateDirectory(directory);
        for (int i = 0; i < 12; i++)
        { File.WriteAllText(f.Path($"Assets/Code/{i}.cs"), "contents"); f.Tracker.Mark($"Assets/Code/{i}.cs"); }
        var snapshot = f.Workspace.Snapshot!;
        snapshot.Mount = new(f.Workspace.Id, f.Workspace.Path, "fixture-delta", snapshot.Reference!, 1, 1);
        snapshot.Observation = new(true, true, true, true, 0, 0, 0);
        await f.Provider().InspectAsync(f.Workspace, ct);
        byte[] previous = File.ReadAllBytes(f.Tracker.CachePath(f.Workspace));
        f.Tracker.OnObserve = count =>
        {
            if (count != 4) return;
            if (restart) f.Tracker.Epoch = "restarted-provider";
            else { Directory.Move(directory, f.Path("Assets/Moved")); f.Tracker.Mark("Assets/Code"); f.Tracker.Mark("Assets/Moved"); }
        };
        Assert.Equal("SourceChanged", (await Assert.ThrowsAsync<WorkspaceException>(() => f.Provider().InspectAsync(f.Workspace, ct))).Code);
        Assert.Equal(previous, File.ReadAllBytes(f.Tracker.CachePath(f.Workspace)));
    }
}
