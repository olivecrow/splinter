using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Protocol;
using Splinter.Cli;
using System.Text.Json;

namespace Splinter.Tests;

public sealed partial class FileWorkspaceTests
{
    private static void RequireWindowsApply()
    { if (!OperatingSystem.IsWindows()) Assert.Skip("Real ordinary-user NTFS publication handles."); }
    [Fact]
    public async Task ApplyPreservesUnrelatedOriginalAndPeerThenReleasesIndependentResult()
    {
        RequireWindowsApply(); using var f = new Fixture(new WindowsOriginalFilePublisher()); var ct = TestContext.Current.CancellationToken;
        var a = (await f.Ensure("apply-a", ct)).Workspace; var b = (await f.Ensure("apply-b", ct)).Workspace;
        File.WriteAllText(Path.Combine(a.Path, "data.txt"), "result-a");
        Directory.CreateDirectory(Path.Combine(a.Path, "new")); File.WriteAllText(Path.Combine(a.Path, "new", "created.txt"), "created");
        File.Delete(Path.Combine(a.Path, "Library", "user.txt")); Directory.Delete(Path.Combine(a.Path, "Library"));
        await f.New().CheckpointAsync(a.Id, "result", ct);
        File.WriteAllText(Path.Combine(f.Project, "foreign.txt"), "keep");
        var plan = (await f.New().PlanOriginalAsync(a.Id, ct)).OriginalApplyPlan!;
        Assert.Empty(plan.Conflicts); Assert.Equal(5, plan.Changes.Length);
        var applied = await f.New().ApplyOriginalAsync(a.Id, "explicit-a", plan.Digest, ct);
        Assert.True(applied.Workspace.OriginalApply!.Completed); Assert.Equal("integrated", applied.Workspace.Phase);
        Assert.Equal("result-a", File.ReadAllText(Path.Combine(f.Project, "data.txt")));
        Assert.Equal("created", File.ReadAllText(Path.Combine(f.Project, "new", "created.txt")));
        Assert.False(Directory.Exists(Path.Combine(f.Project, "Library"))); Assert.Equal("keep", File.ReadAllText(Path.Combine(f.Project, "foreign.txt")));
        Assert.Equal("basis", File.ReadAllText(Path.Combine(b.Path, "data.txt")));
        Assert.Empty(Directory.GetFileSystemEntries(f.Project, ".splinter-apply-*", SearchOption.AllDirectories));
        Assert.Equal("released", (await f.New().ReleaseAsync(a.Id, ct)).Workspace.Phase);
        Assert.True(Directory.Exists(b.Path)); Assert.Equal(0, f.Git.Calls);
    }
    [Fact]
    public async Task AChangedOriginalPlanAndPostCheckpointWorkerChangeNeverWrite()
    {
        RequireWindowsApply(); using var f = new Fixture(new WindowsOriginalFilePublisher()); var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure("conflict", ct)).Workspace;
        File.WriteAllText(Path.Combine(w.Path, "data.txt"), "result"); await f.New().CheckpointAsync(w.Id, "result", ct);
        var plan = (await f.New().PlanOriginalAsync(w.Id, ct)).OriginalApplyPlan!;
        File.WriteAllText(Path.Combine(f.Project, "data.txt"), "external");
        Assert.Equal("OriginalApplyPlanChanged", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ApplyOriginalAsync(w.Id, "apply", plan.Digest, ct))).Code);
        Assert.Null(f.Store.Get(w.Id).OriginalApply); Assert.Equal("external", File.ReadAllText(Path.Combine(f.Project, "data.txt")));
        var conflict = (await f.New().PlanOriginalAsync(w.Id, ct)).OriginalApplyPlan!; Assert.Single(conflict.Conflicts);
        Assert.Equal("OriginalApplyConflict", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ApplyOriginalAsync(w.Id, "apply", conflict.Digest, ct))).Code);
        File.WriteAllText(Path.Combine(w.Path, "data.txt"), "later worker");
        Assert.Equal("ResultNotReady", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().PlanOriginalAsync(w.Id, ct))).Code);
        Assert.Equal("external", File.ReadAllText(Path.Combine(f.Project, "data.txt")));
    }
    [Theory]
    [InlineData("stage-created")]
    [InlineData("staged")]
    [InlineData("retired")]
    [InlineData("retired-before-receipt")]
    [InlineData("published")]
    [InlineData("published-before-receipt")]
    [InlineData("retired-removed")]
    [InlineData("completed")]
    public async Task ApplyLostBoundaryResumesOnlySameIntentAndBlocksReclamation(string stop)
    {
        RequireWindowsApply(); bool interrupted = false;
        var publisher = new WindowsOriginalFilePublisher((at, _) => { if (at == stop && !interrupted) { interrupted = true; throw new WorkspaceException("ReplyLost", "Injected publication response loss."); } });
        using var f = new Fixture(publisher); var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure("resume", ct)).Workspace;
        File.WriteAllText(Path.Combine(w.Path, "data.txt"), "result"); await f.New().CheckpointAsync(w.Id, "result", ct);
        var plan = (await f.New().PlanOriginalAsync(w.Id, ct)).OriginalApplyPlan!;
        Assert.Equal("ReplyLost", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ApplyOriginalAsync(w.Id, "fixed", plan.Digest, ct))).Code);
        if (stop != "completed")
        {
            Assert.Equal("OriginalApplyPending", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ReleaseAsync(w.Id, ct))).Code);
            Assert.Equal("OriginalApplyPending", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().DiscardAsync(new(w.Id, "discard", w.RequestDigest!), ct))).Code);
            Assert.Empty(f.Backend.Released);
        }
        Assert.Equal("OriginalApplyRequestConflict", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ApplyOriginalAsync(w.Id, "different", plan.Digest, ct))).Code);
        Assert.True((await f.New().ApplyOriginalAsync(w.Id, "fixed", plan.Digest, ct)).Workspace.OriginalApply!.Completed);
        Assert.Equal("result", File.ReadAllText(Path.Combine(f.Project, "data.txt")));
        File.WriteAllText(Path.Combine(f.Project, "data.txt"), "later original");
        await f.New().ApplyOriginalAsync(w.Id, "fixed", plan.Digest, ct);
        Assert.Equal("later original", File.ReadAllText(Path.Combine(f.Project, "data.txt")));
    }
    [Fact]
    public async Task NativeHandlesBlockExternalAffectedWritesButAllowUnrelatedWrites()
    {
        RequireWindowsApply(); Fixture? fixture = null; bool checkedHandles = false;
        var publisher = new WindowsOriginalFilePublisher((at, _) =>
        {
            if (at != "preflight") return;
            var f = fixture!;
            Assert.Throws<IOException>(() => File.WriteAllText(Path.Combine(f.Project, "data.txt"), "foreign"));
            Assert.Throws<IOException>(() => File.Delete(Path.Combine(f.Project, "data.txt")));
            Assert.Throws<IOException>(() => Directory.Move(f.Project, f.Project + "-moved"));
            File.WriteAllText(Path.Combine(f.Project, "unrelated.txt"), "allowed"); checkedHandles = true;
        });
        using var f = new Fixture(publisher); fixture = f; var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure("handles", ct)).Workspace;
        File.WriteAllText(Path.Combine(w.Path, "data.txt"), "result"); await f.New().CheckpointAsync(w.Id, "result", ct);
        var plan = (await f.New().PlanOriginalAsync(w.Id, ct)).OriginalApplyPlan!;
        await f.New().ApplyOriginalAsync(w.Id, "apply", plan.Digest, ct);
        Assert.True(checkedHandles); Assert.Equal("allowed", File.ReadAllText(Path.Combine(f.Project, "unrelated.txt")));
    }
    [Fact]
    public async Task ForeignCreationInPublicationGapIsPreservedAndBlocksRetry()
    {
        RequireWindowsApply(); Fixture? fixture = null;
        var publisher = new WindowsOriginalFilePublisher((at, _) => { if (at == "retired") File.WriteAllText(Path.Combine(fixture!.Project, "data.txt"), "foreign new original"); });
        using var f = new Fixture(publisher); fixture = f; var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure("gap", ct)).Workspace;
        File.WriteAllText(Path.Combine(w.Path, "data.txt"), "result"); await f.New().CheckpointAsync(w.Id, "result", ct);
        var plan = (await f.New().PlanOriginalAsync(w.Id, ct)).OriginalApplyPlan!;
        Assert.Equal("OriginalApplyNativeFailure", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ApplyOriginalAsync(w.Id, "apply", plan.Digest, ct))).Code);
        Assert.Equal("foreign new original", File.ReadAllText(Path.Combine(f.Project, "data.txt")));
        Assert.Equal("OriginalApplyConflict", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ApplyOriginalAsync(w.Id, "apply", plan.Digest, ct))).Code);
        Assert.Equal(2, Directory.GetFiles(f.Project, ".splinter-apply-*").Length);
    }
}
