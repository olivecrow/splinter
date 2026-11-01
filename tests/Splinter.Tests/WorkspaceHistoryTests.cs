using System.Text.Json.Nodes;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class WorkspaceHistoryTests
{
    [Fact]
    public void LongHistoryAndLargeIndividualValueLeaveOnlyRootsInControlJson()
    {
        using var files = new TestDirectory(); var store = new WorkspaceStore(Path.Combine(files.Path, "home"));
        var workspace = Record(store, files.Path); store.Save(workspace, "create");
        for (int i = 0; i < 1024; i++) Add(workspace, "capture-" + i, i == 1023 ? new string('x', 160000) : "small");
        store.Save(workspace, "collected");
        string json = File.ReadAllText(Path.Combine(store.ControlPath(workspace.Id), "workspace.json"));
        Assert.True(json.Length < 6000); Assert.DoesNotContain(new string('x', 256), json); Assert.DoesNotContain("\"results\"", json);
        var reopened = store.Get(workspace.Id);
        Assert.Equal(1024, reopened.Results.Count); Assert.Equal(160000, reopened.Results.Last().Message.Length);
        Assert.Equal("capture-12", reopened.Results.Find("capture-12", value => value.RequestKey)!.RequestKey);
        Assert.Null(reopened.Results.Find("absent", value => value.RequestKey));
        Assert.Equal(Enumerable.Range(0, 1024).Select(i => "capture-" + i), reopened.Results.Select(value => value.RequestKey));
        Add(reopened, "capture-next", "next"); store.Save(reopened, "next");
        Assert.Equal(1024, workspace.Results.Count); Assert.Equal(1025, store.Get(workspace.Id).Results.Count);
        Assert.Equal("capture-1023", workspace.Results.Last().RequestKey);
    }

    [Fact]
    public void DuplicateRequestAndCorruptRootCannotOverwriteRetainedHistory()
    {
        using var files = new TestDirectory(); var store = new WorkspaceStore(Path.Combine(files.Path, "home"));
        var workspace = Record(store, files.Path); store.Save(workspace, "create"); Add(workspace, "request", "first"); store.Save(workspace, "first");
        var before = workspace.ResultHistory;
        Assert.Throws<WorkspaceException>(() => Add(workspace, "request", "different")); Assert.Equal(before, workspace.ResultHistory);
        string path = Path.Combine(store.ControlPath(workspace.Id), "workspace.json"); var control = JsonNode.Parse(File.ReadAllText(path))!;
        control["resultHistory"]!["count"] = 2; File.WriteAllText(path, control.ToJsonString());
        byte[] retained = File.ReadAllBytes(path); Assert.Throws<WorkspaceException>(() => store.Get(workspace.Id)); Assert.Equal(retained, File.ReadAllBytes(path));
    }

    private static WorkspaceRecord Record(WorkspaceStore store, string project)
    {
        string id = WorkspaceStore.Key("history-test");
        return new() { Id = id, RequestKey = "history-test", ProjectPath = project, Path = store.WorkPath(id),
            SourceReference = "splinter/" + id, BaseReference = "development", BaseRevision = "basis",
            StorageMode = "git-checkout", SourceKind = "git", ApplicationKind = "none", Phase = "ready" };
    }
    private static void Add(WorkspaceRecord workspace, string key, string message)
    {
        string id = WorkspaceStore.Key(workspace.Id + "\nresult\n" + key), revision = "revision-" + key;
        workspace.Results.Add(new(id, workspace.Results.Count + 1, key, message, revision, revision,
            "files:" + workspace.Id + ":" + id, new string('a', 64), workspace.Results.Count, DateTimeOffset.UtcNow, "git-" + key, "git-fingerprint"));
        workspace.ResultRevision = revision;
    }
}
