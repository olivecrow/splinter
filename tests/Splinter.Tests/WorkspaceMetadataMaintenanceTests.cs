using System.Text.Json;
using System.Text.Json.Nodes;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Protocol;
using Splinter.Runtime;
using Splinter.SourceControl.Files;

namespace Splinter.Tests;

public sealed class WorkspaceMetadataMaintenanceTests
{
    [Fact]
    public async Task WorkspaceCollectionRetainsHistoryAndReviewedPlanArtifactsAndCompletedRetryDoesNotMutate()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Requires native metadata creation identities.");
        using var files = new TestDirectory(); var pins = new WindowsJournalFilePins(); var ct = TestContext.Current.CancellationToken;
        var store = new WorkspaceStore(Path.Combine(files.Path, "home"), pins); var workspace = Record(store, files.Path);
        store.Save(workspace, "fixture");
        for (int index = 0; index < 64; index++) Add(workspace, "capture-" + index);
        store.Save(workspace, "history"); string control = store.ControlPath(workspace.Id);
        var template = new OriginalApplyPlan(3, workspace.Id, workspace.RequestDigest!, "basis", "result", workspace.ProjectPath, "original", [], [], "");
        var plan = OriginalApplyPages.Create(template,
            [new("one.txt", null, new("file", 3, new string('a', 64), 128, DateTime.UnixEpoch.Ticks), null, false)], control, ct, pins);
        OriginalApplyPages.SavePlan(plan, control);
        var provider = new FileWorkspaceProvider(store, contentPins: pins);
        var result = await provider.MaintainMetadataAsync(store.Get(workspace.Id), control, "fixed", "workspace", ct);
        Assert.Equal("completed", result.Phase); Assert.Equal(4, result.Stages.Length); Assert.Null(provider.ObserveMetadataMaintenance(workspace, control));
        Assert.Equal(64, store.Get(workspace.Id).Results.Count);
        Assert.Equal("one.txt", OriginalApplyPages.ReadPlan(control, plan.Digest).ChangeAt(0).Path);
        Assert.True(new ContractCatalog().Validate("urn:splinter:v1:workspace-maintenance-result", JsonSerializer.SerializeToElement(result, ApiJson.Options)).IsValid);
        var snapshot = Directory.EnumerateFiles(control, "*", SearchOption.AllDirectories).ToDictionary(path => path, File.ReadAllBytes);
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(await provider.MaintainMetadataAsync(store.Get(workspace.Id), control, "fixed", "workspace", ct)));
        foreach (var file in snapshot) Assert.Equal(file.Value, File.ReadAllBytes(file.Key));
        Assert.Equal("RequestKeyConflict", (await Assert.ThrowsAsync<WorkspaceException>(() => provider.MaintainMetadataAsync(workspace, control, "fixed", "all", ct))).Code);
    }

    [Fact]
    public async Task ReaderContentionPreservesExactScopeAndKeyAndRecoveryAction()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Requires native metadata reader fences.");
        using var files = new TestDirectory(); var pins = new WindowsJournalFilePins(); var ct = TestContext.Current.CancellationToken;
        var store = new WorkspaceStore(Path.Combine(files.Path, "home"), pins); var workspace = Record(store, files.Path);
        store.Save(workspace, "fixture"); Add(workspace, "capture"); store.Save(workspace, "history");
        var provider = new FileWorkspaceProvider(store, contentPins: pins); string control = store.ControlPath(workspace.Id);
        using (var reader = new PagedMetadataStore(Path.Combine(control, "history-pages")))
        {
            Assert.Equal("WorkspaceMaintenancePending", (await Assert.ThrowsAsync<WorkspaceException>(() => provider.MaintainMetadataAsync(workspace, control, "request", "workspace", ct))).Code);
            var pending = provider.ObserveMetadataMaintenance(workspace, control)!;
            Assert.Equal("request", pending.RequestKey); Assert.Equal("workspace", pending.Scope);
            Assert.Equal("WorkspaceMaintenancePending", (await Assert.ThrowsAsync<WorkspaceException>(() => provider.MaintainMetadataAsync(workspace, control, "different", "workspace", ct))).Code);
            Assert.Equal("WorkspaceMaintenancePending", (await Assert.ThrowsAsync<WorkspaceException>(() => provider.MaintainMetadataAsync(workspace, control, "request", "all", ct))).Code);
            var action = WorkspaceActions.Next(new(workspace, null, null, control, "workspace.ensure") { MetadataMaintenance = pending }, store.Root);
            Assert.Equal("workspace.maintain", action.Command);
            var args = JsonSerializer.SerializeToElement(action.Arguments, ApiJson.Options); Assert.Equal("request", args.GetProperty("requestKey").GetString());
        }
        var completed = await provider.MaintainMetadataAsync(store.Get(workspace.Id), control, "request", "workspace", ct);
        Assert.Equal("completed", completed.Phase); Assert.Single(store.Get(workspace.Id).Results);
    }

    [Fact]
    public async Task TamperedStagePathCannotRedirectCollection()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Requires native metadata reader fences.");
        using var files = new TestDirectory(); var pins = new WindowsJournalFilePins(); var ct = TestContext.Current.CancellationToken;
        var store = new WorkspaceStore(Path.Combine(files.Path, "home"), pins); var workspace = Record(store, files.Path);
        store.Save(workspace, "fixture"); Add(workspace, "capture"); store.Save(workspace, "history");
        var provider = new FileWorkspaceProvider(store, contentPins: pins); string control = store.ControlPath(workspace.Id);
        using (var reader = new PagedMetadataStore(Path.Combine(control, "history-pages")))
            await Assert.ThrowsAsync<WorkspaceException>(() => provider.MaintainMetadataAsync(workspace, control, "request", "workspace", ct));
        string active = Path.Combine(control, "maintenance-active.json"); var json = JsonNode.Parse(File.ReadAllText(active))!;
        json["stages"]![0]!["store"] = files.Path; File.WriteAllText(active, json.ToJsonString()); byte[] retained = File.ReadAllBytes(active);
        Assert.Equal("FileSourceStateInvalid", (await Assert.ThrowsAsync<WorkspaceException>(() => provider.MaintainMetadataAsync(workspace, control, "request", "workspace", ct))).Code);
        Assert.Equal(retained, File.ReadAllBytes(active)); Assert.Single(store.Get(workspace.Id).Results);
    }

    [Fact]
    public async Task UnpublishedPlanControlsAreOpaquePreservedAndPagedWhileCompletedPlansRemainReadable()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Requires native metadata identities.");
        using var files = new TestDirectory(); var pins = new WindowsJournalFilePins(); var ct = TestContext.Current.CancellationToken;
        var store = new WorkspaceStore(Path.Combine(files.Path, "home"), pins); var workspace = Record(store, files.Path);
        store.Save(workspace, "fixture"); string control = store.ControlPath(workspace.Id);
        var template = new OriginalApplyPlan(3, workspace.Id, workspace.RequestDigest!, "basis", "result", workspace.ProjectPath, "original", [], [], "");
        var plan = OriginalApplyPages.Create(template,
            [new("one.txt", null, new("file", 3, new string('a', 64), 128, DateTime.UnixEpoch.Ticks), null, false)], control, ct, pins);
        OriginalApplyPages.SavePlan(plan, control);
        string directory = Path.Combine(control, "original-plans"), prefix = new string('b', 64) + ".json";
        string partial = Path.Combine(directory, prefix + ".pending"), newer = Path.Combine(directory, prefix + "." + Guid.NewGuid().ToString("N") + ".tmp");
        File.WriteAllText(partial, "{\"unpublished\":"); File.WriteAllText(newer, "{\"another\":");
        string partialId = pins.Identify(partial), newerId = pins.Identify(newer);
        var provider = new FileWorkspaceProvider(store, contentPins: pins);
        var result = await provider.MaintainMetadataAsync(workspace, control, "reviews", "workspace", ct);
        Assert.Equal(2, result.Stages.Single(stage => stage.Scope == "workspace-original").PreservedEntries);
        var first = FileWorkspaceProvider.ReadMaintenanceResiduals(workspace, control, "reviews", "workspace-original", pageSize: 1);
        Assert.Single(first.Entries); Assert.NotNull(first.NextCursor);
        var second = FileWorkspaceProvider.ReadMaintenanceResiduals(workspace, control, "reviews", "workspace-original", first.NextCursor, 1);
        Assert.Single(second.Entries); Assert.NotEqual(first.Entries[0].Path, second.Entries[0].Path); Assert.Null(second.NextCursor);
        Assert.Equal(partialId, pins.Identify(partial)); Assert.Equal(newerId, pins.Identify(newer));
        Assert.Equal("one.txt", OriginalApplyPages.ReadPlan(control, plan.Digest).ChangeAt(0).Path);
        Assert.Throws<WorkspaceException>(() => FileWorkspaceProvider.ReadMaintenanceResiduals(workspace, control, "reviews", "workspace-original", new(new string('0', 64), "x"), 1));
    }

    private static WorkspaceRecord Record(WorkspaceStore store, string project)
    {
        string id = WorkspaceStore.Key("metadata-maintenance");
        return new() { Id = id, RequestKey = "metadata-maintenance", RequestDigest = WorkspaceStore.Key("request"), ProjectPath = project,
            Path = store.WorkPath(id), SourceReference = "splinter/" + id, BaseReference = "development", BaseRevision = "basis",
            StorageMode = "git-snapshot", SourceKind = "git", ApplicationKind = "none", ManagedPaths = [], Snapshot = new(), Phase = "ready" };
    }
    private static void Add(WorkspaceRecord workspace, string key)
    {
        string id = WorkspaceStore.Key(workspace.Id + "\nresult\n" + key), revision = "revision-" + key;
        workspace.Results.Add(new(id, workspace.Results.Count + 1, key, "message", revision, revision, "files:" + workspace.Id + ":" + id,
            new string('a', 64), workspace.Results.Count, DateTimeOffset.UtcNow, "git-" + key, "git-fingerprint"));
        workspace.ResultRevision = revision;
    }
}
