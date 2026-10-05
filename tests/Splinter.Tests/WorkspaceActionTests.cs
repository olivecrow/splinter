using System.Text.Json;
using System.Text.Json.Nodes;
using Splinter.Cli;
using Splinter.Core;
using Splinter.Protocol;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class WorkspaceActionTests
{
    [Theory]
    [InlineData("none", false, false)] [InlineData("none", true, false)]
    [InlineData("none", false, true)] [InlineData("none", true, true)]
    [InlineData("plastic", false, false)] [InlineData("plastic", true, false)]
    [InlineData("plastic", false, true)] [InlineData("plastic", true, true)]
    public void OriginalApplyActionsRetainTargetIntentDigestAndSelectedScope(string source, bool selected, bool pending)
    {
        var w = Record(source, source == "none" ? "ntfs-snapshot" : "plastic-snapshot");
        string[]? paths = selected ? ["Assets/item.txt"] : null;
        var plan = new OriginalApplyPlan(1, w.Id, w.RequestDigest!, "basis", "result", w.ProjectPath, "physical", [], [], "")
            { SelectedPaths = paths, SourceIdentity = source == "plastic" ? "sha256:" + new string('c', 64) : null };
        plan = plan with { Digest = plan.ComputeDigest() };
        if (pending) w.OriginalApply = new() { Plan = plan, IntentKey = "same-intent", Steps = [] };
        var view = new WorkspaceView(w, null, null, "control", "workspace.release") { OriginalApplyPlan = pending ? null : plan };
        var action = Assert.Single(WorkspaceCommands.Result("workspace.status", view, false, "home").NextActions);
        Assert.Equal("workspace.integrate", action.Command);
        var args = Arguments(action.Arguments); Assert.Equal("original", args["target"]!.GetValue<string>());
        Assert.Equal(selected, args.ContainsKey("paths"));
        if (selected) Assert.Equal(paths, args["paths"]!.Deserialize<string[]>());
        if (pending)
        { Assert.Equal("same-intent", args["intentKey"]!.GetValue<string>()); Assert.Equal(plan.Digest, args["expectedPlanDigest"]!.GetValue<string>()); }
        else Assert.True(args["plan"]!.GetValue<bool>());
        Assert.True(new ContractCatalog().Validate("urn:splinter:v1:workspace-integrate-request", JsonSerializer.SerializeToElement(args)).IsValid);
    }

    [Theory]
    [InlineData("git", "git-snapshot")] [InlineData("plastic", "plastic-snapshot")] [InlineData("none", "project-snapshot")]
    public void EnsureActionsRetainTheExactBasisAndRequest(string source, string storage)
    {
        var w = Record(source, storage); w.GeneratedDirectoryId = new string('b', 24);
        var view = new WorkspaceView(w, null, null, "control", "workspace.ensure");
        var next = Assert.Single(WorkspaceCommands.Result("workspace.status", view, false, "home").NextActions);
        Assert.Equal("workspace.ensure", next.Command);
        var args = Arguments(next.Arguments); string basis = storage == "project-snapshot" ? "projectBasis" : "libraryBasis";
        Assert.Equal(w.GeneratedDirectoryId, args[basis]!.GetValue<string>());
        Assert.Equal(w.RequestKey, args["key"]!.GetValue<string>()); Assert.Equal(w.BaseReference, args["baseReference"]!.GetValue<string>());
        Assert.Equal(args.ToJsonString(), Arguments(WorkspaceActions.Ensure(w, "home").Arguments).ToJsonString());
        Assert.True(new ContractCatalog().Validate("urn:splinter:v1:workspace-ensure-request", JsonSerializer.SerializeToElement(args)).IsValid);
        Assert.True(Arguments(WorkspaceActions.Ensure(w, "home", editor: true).Arguments)["editor"]!.GetValue<bool>());
    }
    private static JsonObject Arguments(object args)
    { var node = JsonSerializer.SerializeToNode(args, ApiJson.Options)!.AsObject(); node.Remove("home"); return node; }
    private static WorkspaceRecord Record(string source, string storage) => new()
    {
        Id = new string('a', 24), RequestKey = "same-request", ProjectPath = "project", Path = "worker", SourceReference = "source",
        BaseReference = source == "none" ? "snapshot" : source == "plastic" ? "1@repo@server" : "recorded", SourceKind = source, ApplicationKind = "none",
        StorageMode = storage, RequestDigest = new string('d', 64), Snapshot = new()
    };
}
