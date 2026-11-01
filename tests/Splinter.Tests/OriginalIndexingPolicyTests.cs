using Splinter.Core;
using Splinter.Platform.Windows;

namespace Splinter.Tests;

public sealed partial class FileWorkspaceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AtomicSavedSourcePreservesObservedOriginalIndexingPolicyAndExactIntent(bool indexed)
    {
        RequireWindowsApply(); bool interrupted = false;
        var publisher = new WindowsOriginalFilePublisher((stage, _) =>
        { if (!interrupted && stage == "published-before-receipt") { interrupted = true; throw new WorkspaceException("ReplyLost", "Lost native publication response."); } });
        using var f = new Fixture(publisher); var ct = TestContext.Current.CancellationToken;
        string original = Path.Combine(f.Project, "data.txt");
        File.SetAttributes(original, File.GetAttributes(original) | FileAttributes.NotContentIndexed);
        var w = (await f.Ensure("indexing-policy", ct)).Workspace; string source = Path.Combine(w.Path, "data.txt");
        string fixedBasis = Path.Combine(f.Store.ControlPath(w.Id), "fake-immutable", "data.txt"); var basisAttributes = File.GetAttributes(fixedBasis);
        Assert.True((File.GetAttributes(source) & FileAttributes.NotContentIndexed) != 0);
        string replacement = source + ".save"; File.WriteAllText(replacement, "saved source"); File.SetAttributes(replacement, FileAttributes.Normal); File.Move(replacement, source, true);
        Assert.True((File.GetAttributes(source) & FileAttributes.NotContentIndexed) == 0);
        var result = (await f.New().CheckpointAsync(w.Id, "saved", "saved", ct)).ResultGeneration!;
        SetIndexing(original, indexed);
        OriginalApplyPlan plan;
        using (var untouchedPayload = new FileStream(Path.Combine(f.Project, "Library", "user.txt"), FileMode.Open, FileAccess.Read, FileShare.None))
            plan = (await f.New().PlanOriginalAsync(w.Id, result.Id, null, ct, ["data.txt"])).OriginalApplyPlan!;
        Assert.Empty(plan.Conflicts); var change = Assert.Single(plan.EnumerateChanges());
        Assert.Equal(indexed, (change.Before!.Attributes & (int)FileAttributes.NotContentIndexed) != 0);
        Assert.Equal(indexed, (change.After!.Attributes & (int)FileAttributes.NotContentIndexed) != 0);
        Assert.Equal((int)File.GetAttributes(original), change.Expected!.Version.Attributes);
        Assert.Equal("ReplyLost", (await Assert.ThrowsAsync<WorkspaceException>(() =>
            f.New().ApplyOriginalAsync(w.Id, result.Id, "same-indexing-intent", plan.Digest, null, ct, ["data.txt"]))).Code);
        Assert.True(interrupted);
        var resumed = await f.New().ApplyOriginalAsync(w.Id, result.Id, "same-indexing-intent", plan.Digest, null, ct);
        Assert.True(resumed.OriginalApplyReceipt!.Completed); Assert.Equal("saved source", File.ReadAllText(original));
        Assert.Equal(indexed, (File.GetAttributes(original) & FileAttributes.NotContentIndexed) != 0);
        Assert.Equal(basisAttributes, File.GetAttributes(fixedBasis)); Assert.Equal("basis", File.ReadAllText(fixedBasis));
        File.WriteAllText(original, "later original"); SetIndexing(original, !indexed);
        var retry = await f.New().ApplyOriginalAsync(w.Id, result.Id, "same-indexing-intent", plan.Digest, null, ct);
        Assert.Equal(plan.Digest, retry.OriginalApplyReceipt!.Plan.Digest); Assert.Equal("later original", File.ReadAllText(original));
        Assert.Equal(!indexed, (File.GetAttributes(original) & FileAttributes.NotContentIndexed) != 0);
    }

    [Fact]
    public async Task ATargetIndexingChangeAfterReviewStillChangesThePlanBeforePublication()
    {
        RequireWindowsApply(); using var f = new Fixture(new WindowsOriginalFilePublisher()); var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure("target-policy-race", ct)).Workspace; File.WriteAllText(Path.Combine(w.Path, "data.txt"), "changed");
        var result = (await f.New().CheckpointAsync(w.Id, "changed", "changed", ct)).ResultGeneration!;
        string original = Path.Combine(f.Project, "data.txt"); SetIndexing(original, false);
        var plan = (await f.New().PlanOriginalAsync(w.Id, result.Id, null, ct, ["data.txt"])).OriginalApplyPlan!;
        SetIndexing(original, true);
        Assert.Equal("OriginalApplyPlanChanged", (await Assert.ThrowsAsync<WorkspaceException>(() =>
            f.New().ApplyOriginalAsync(w.Id, result.Id, "same", plan.Digest, null, ct, ["data.txt"]))).Code);
        Assert.Null(f.Store.Get(w.Id).OriginalApply); Assert.Equal("basis", File.ReadAllText(original));
    }

    [Theory]
    [InlineData(FileAttributes.Hidden)]
    [InlineData(FileAttributes.System)]
    [InlineData(FileAttributes.ReadOnly)]
    [InlineData(FileAttributes.Temporary)]
    public async Task IndexingNormalizationDoesNotAdmitOtherSourceMetadataChanges(FileAttributes extra)
    {
        RequireWindowsApply(); using var f = new Fixture(new WindowsOriginalFilePublisher()); var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure("other-metadata", ct)).Workspace; string source = Path.Combine(w.Path, "data.txt");
        File.WriteAllText(source, "changed"); File.SetAttributes(source, FileAttributes.Normal | extra);
        var result = (await f.New().CheckpointAsync(w.Id, "changed", "changed", ct)).ResultGeneration!;
        var plan = (await f.New().PlanOriginalAsync(w.Id, result.Id, null, ct, ["data.txt"])).OriginalApplyPlan!;
        Assert.NotEmpty(plan.Conflicts); Assert.Equal("basis", File.ReadAllText(Path.Combine(f.Project, "data.txt")));
        Assert.Null(f.Store.Get(w.Id).OriginalApply);
    }
    private static void SetIndexing(string path, bool indexed) => File.SetAttributes(path,
        indexed ? File.GetAttributes(path) | FileAttributes.NotContentIndexed : File.GetAttributes(path) & ~FileAttributes.NotContentIndexed);
}
