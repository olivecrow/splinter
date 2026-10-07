using Splinter.Core;
using Splinter.SourceControl.Git;

namespace Splinter.Tests;

public sealed class GitSnapshotCheckpointTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task CheckpointPrunesManagedIgnoredRootsButRetainsUnownedSiblingRejection(bool unowned, bool afterPinWrite)
    {
        using var root = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        string source = Path.Combine(root.Path, "source"), control = Path.Combine(root.Path, "control");
        Directory.CreateDirectory(source); Directory.CreateDirectory(control);
        await TestDirectory.Git(source, "init", "-b", "development");
        await TestDirectory.Git(source, "config", "user.name", "Fixture");
        await TestDirectory.Git(source, "config", "user.email", "fixture@example.invalid");
        await TestDirectory.Git(source, "config", "core.autocrlf", "false");
        const string ignore = "Library/\n[[]cache]/\n*.csproj\n";
        File.WriteAllText(Path.Combine(source, ".gitignore"), ignore); File.WriteAllText(Path.Combine(source, "code.cs"), "before\n");
        await TestDirectory.Git(source, "add", "."); await TestDirectory.Git(source, "commit", "-m", "basis");
        var w = new WorkspaceRecord { Id = "snapshot-checkpoint", RequestKey = "test", ProjectPath = source,
            Path = Path.Combine(root.Path, "worker"), SourceReference = "splinter/test", BaseReference = "development",
            StorageMode = "ntfs-snapshot", ManagedPaths = ["Library/managed", "[cache]", "*.csproj"], Snapshot = new() };
        var tracker = new Tracker(Path.Combine(control, "source.json"));
        var git = new GitWorkspaceProvider(tracker);
        w.BaseRevision = await git.PreflightAsync(w, ct);
        w.Snapshot.Reference = new("fixture", "fake", source, source, DateTimeOffset.UtcNow, true, true, "fixture");
        await git.RegisterAsync(w, control, ct);
        Directory.CreateDirectory(w.Path);
        File.WriteAllText(Path.Combine(w.Path, "code.cs"), "before\n"); File.WriteAllText(Path.Combine(w.Path, ".gitignore"), ignore);
        foreach (string name in new[] { "Library/managed/cache", "[cache]/cache", "generated.csproj" })
        { string path = Path.Combine(w.Path, name); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, "private"); }
        if (unowned) File.WriteAllText(Path.Combine(w.Path, "Library", "unowned"), "retain");
        await git.AttachAsync(w, control, ct);
        File.WriteAllText(Path.Combine(w.Path, "code.cs"), "after\n");
        if (afterPinWrite)
        {
            tracker.OnObserve = count => { if (count == 6) tracker.Sequence++; };
            Assert.Equal("SourceChanged", (await Assert.ThrowsAsync<WorkspaceException>(() => git.CheckpointAndPreserveAsync(w, control, "result", ct))).Code);
            Assert.Null(w.Snapshot.Result);
            Assert.Equal("after\n", File.ReadAllText(Path.Combine(w.Path, "code.cs")));
            tracker.OnObserve = null;
        }
        if (unowned)
        {
            Assert.Equal("UnownedIgnoredFiles", (await Assert.ThrowsAsync<WorkspaceException>(() => git.CheckpointAndPreserveAsync(w, control, "result", ct))).Code);
            Assert.Equal("retain", File.ReadAllText(Path.Combine(w.Path, "Library", "unowned")));
            Assert.Null(w.Snapshot.Result);
        }
        else
        {
            var result = await git.CheckpointAndPreserveAsync(w, control, "result", ct);
            Assert.False(result.Source.HasChanges);
            Assert.Equal("after", await TestDirectory.Git(source, "show", result.ResultReference + ":code.cs"));
            Assert.Equal(result.Source.Revision, await TestDirectory.Git(source, "rev-parse", result.ResultReference));
            Assert.Equal(afterPinWrite ? 4 : 2, File.ReadLines(Path.Combine(control, "source-inspections.jsonl")).Count());
            string paths = await TestDirectory.Git(source, "ls-tree", "-r", "--name-only", result.ResultReference);
            Assert.DoesNotContain("Library", paths); Assert.DoesNotContain("cache", paths); Assert.DoesNotContain("csproj", paths);
            var unchanged = await git.CheckpointAndPreserveAsync(w, control, "same result", ct);
            Assert.Equal(result.Source.Revision, unchanged.Source.Revision);
            Assert.Equal(result.ResultReference, unchanged.ResultReference);
        }
    }

    private sealed class Tracker(string cache) : ISourceChangeProvider
    {
        private int observations;
        public long Sequence = 1;
        public Action<int>? OnObserve;
        public string CachePath(WorkspaceRecord w) => cache;
        public Task<SourceChangeView> ObserveChangesAsync(WorkspaceRecord w, CancellationToken ct = default)
        {
            OnObserve?.Invoke(++observations);
            return Task.FromResult(new SourceChangeView(w.Id, w.Snapshot!.Reference!.Id, "fixture", Sequence, [new("code.cs", Sequence)]));
        }
    }
}
