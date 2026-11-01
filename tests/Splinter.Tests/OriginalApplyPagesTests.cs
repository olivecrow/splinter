using System.Security.Cryptography;
using System.Text.Json;
using Splinter.Core;
using Splinter.Runtime;
using Splinter.Platform.Windows;
using System.Text;

namespace Splinter.Tests;

public sealed class OriginalApplyPagesTests
{
    [Fact]
    public void InterruptedReviewControlDoesNotBecomeAuthorityOrBlockTheSameDigestPublication()
    {
        using var files = new TestDirectory(); var plan = OriginalApplyPages.Create(Template(files.Path), [Change("file")], files.Path, TestContext.Current.CancellationToken);
        string directory = Path.Combine(files.Path, "original-plans"); Directory.CreateDirectory(directory);
        string partial = Path.Combine(directory, plan.Digest[7..] + ".json.pending"); File.WriteAllText(partial, "{\"version\":3,");
        OriginalApplyPages.SavePlan(plan, files.Path);
        Assert.Equal(plan.Digest, OriginalApplyPages.ReadPlan(files.Path, plan.Digest).Digest);
        Assert.Equal("{\"version\":3,", File.ReadAllText(partial));
        OriginalApplyPages.SavePlan(plan, files.Path); Assert.Single(Directory.GetFiles(directory, "*.json"));
    }
    [Fact]
    public void LargeSelectionUsesFixedPagesAndBoundedPreviewWithoutRetainingTheInputArray()
    {
        using var files = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        IEnumerable<string> Paths() { for (int i = 0; i < 32769; i++) yield return "selected-" + i.ToString("D8"); }
        var plan = OriginalApplyPages.Create(Template(files.Path), [], files.Path, ct, selection: Paths());
        Assert.Equal(32769, plan.SelectionCount); Assert.NotNull(plan.SelectionRoot); Assert.InRange(plan.SelectedPaths!.Length, 1, 100);
        Assert.True(plan.SelectedPaths.Sum(path => path.Length) <= 4096); Assert.True(JsonSerializer.SerializeToUtf8Bytes(plan).Length < 16384);
        Assert.Equal(32769, plan.EnumerateSelection().LongCount());
        var first = OriginalApplyPages.ReadSelectionPage(files.Path, plan, pageSize: 7); Assert.Equal(7, first.Entries.Length);
        var second = OriginalApplyPages.ReadSelectionPage(files.Path, plan, first.NextCursor, 7); Assert.Equal(7, second.Entries.Length);
        Assert.Empty(first.Entries.Intersect(second.Entries));
        var other = OriginalApplyPages.Create(Template(files.Path), [], files.Path, ct, selection: ["different"]);
        Assert.Throws<WorkspaceException>(() => OriginalApplyPages.ReadSelectionPage(files.Path, other, first.NextCursor, 7));
        Assert.Throws<WorkspaceException>(() => OriginalApplyPages.Create(Template(files.Path), [], files.Path, ct, selection: ["same", "SAME"]));
    }
    [Fact]
    public void PlanAboveFormerMembershipLimitRetainsBoundedControlAndFixedPages()
    {
        using var files = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        var template = Template(files.Path);
        IEnumerable<OriginalApplyChange> Changes()
        {
            for (int i = 0; i < 32769; i++) yield return Change("file-" + i.ToString("D8"));
        }
        var plan = OriginalApplyPages.Create(template, Changes(), files.Path, ct);
        Assert.Equal(32769, plan.ChangeCount); Assert.Empty(plan.Changes);
        using (var summary = JsonDocument.Parse(JsonSerializer.Serialize(plan, new JsonSerializerOptions(JsonSerializerDefaults.Web))))
        { Assert.Equal(32769, summary.RootElement.GetProperty("changeCount").GetInt64()); Assert.Equal(0, summary.RootElement.GetProperty("conflictCount").GetInt64()); }
        Assert.Equal("file-00032768", plan.ChangeAt(32768).Path);
        Assert.Equal(32769, plan.EnumerateChanges().LongCount());
        Assert.True(JsonSerializer.SerializeToUtf8Bytes(plan).Length < 8192);
        OriginalApplyPages.SavePlan(plan, files.Path);
        var retained = OriginalApplyPages.ReadPlan(files.Path, plan.Digest);
        Assert.Equal(plan.Digest, retained.Digest); Assert.Equal(plan.ChangesRoot, retained.ChangesRoot);
        Assert.Throws<WorkspaceException>(() => OriginalApplyPages.Create(template, [Change("same"), Change("SAME")], files.Path, ct));
    }
    [Fact]
    public void SparseProgressReopensAndHistoricalRootsDoNotAdvance()
    {
        using var files = new TestDirectory();
        var plan = OriginalApplyPages.Create(Template(files.Path), [Change("a"), Change("b")], files.Path, TestContext.Current.CancellationToken);
        var state = OriginalApplyPages.Initialize(plan, "intent", files.Path); var original = state.StepsRoot;
        state.SaveStep(0, new() { Phase = "staged", StagedIdentity = "owned-stage", StagedBytes = 1 });
        string json = JsonSerializer.Serialize(state, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var restored = JsonSerializer.Deserialize<OriginalApplyState>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        OriginalApplyPages.Bind(restored, files.Path);
        Assert.Equal("staged", restored.StepAt(0).Phase); Assert.Equal("pending", restored.StepAt(1).Phase);
        Assert.Equal(0, original!.Count); Assert.Equal(1, restored.StepsRoot!.Count);
        restored.RequireMatch(Workspace(files.Path));
        restored.StepsRoot = original; OriginalApplyPages.Bind(restored, files.Path);
        Assert.Equal("pending", restored.StepAt(0).Phase);
    }
    [Fact]
    public void AllConflictsStayQueryableWhileControlKeepsOnlyABoundedPreview()
    {
        using var files = new TestDirectory(); var plan = OriginalApplyPages.Create(Template(files.Path), [], files.Path, TestContext.Current.CancellationToken);
        plan = OriginalApplyPages.WithConflicts(plan, Enumerable.Range(0, 32769).Select(i => "path-" + i + ": diverged"), files.Path);
        Assert.Equal(32769, plan.ConflictCount); Assert.Equal(100, plan.Conflicts.Length);
        Assert.True(JsonSerializer.SerializeToUtf8Bytes(plan).Length < 16384);
        OriginalApplyPages.SavePlan(plan, files.Path); Assert.Equal(plan.Digest, OriginalApplyPages.ReadPlan(files.Path, plan.Digest).Digest);
    }
    [Fact]
    public void LongUnicodePathUsesComponentReferencesRatherThanAnOversizedLogicalPage()
    {
        using var files = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        string path = string.Join('/', Enumerable.Repeat(new string('\uD55C', 200), 160)) + "/file";
        Assert.InRange(path.Length, 1, 32767); Assert.True(Encoding.UTF8.GetByteCount(path) > 65536);
        var plan = OriginalApplyPages.Create(Template(files.Path), [Change(path)], files.Path, ct, selection: [path]);
        Assert.Equal(1, plan.SelectionCount); Assert.Empty(plan.SelectedPaths!);
        Assert.Equal(path, Assert.Single(OriginalApplyPages.ReadSelectionPage(files.Path, plan).Entries));
        Assert.Equal(path, plan.ChangeAt(0).Path); Assert.NotNull(plan.FindChange(path));
        plan = OriginalApplyPages.WithConflicts(plan, [path + ": diverged"], files.Path);
        Assert.Equal(path + ": diverged", Assert.Single(OriginalApplyPages.EnumerateConflicts(files.Path, plan)));
        Assert.True(JsonSerializer.SerializeToUtf8Bytes(plan).Length < 8192);
    }
    [Fact]
    public void PendingForeignStageStillRejectsBeforeAnyOriginalOrProgressMutation()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Ordinary NTFS conditional publication.");
        using var files = new TestDirectory(); string project = Path.Combine(files.Path, "project"), control = Path.Combine(files.Path, "control");
        Directory.CreateDirectory(project); File.WriteAllText(Path.Combine(project, "data.txt"), "basis");
        var publisher = new WindowsOriginalFilePublisher(); var observed = publisher.Observe(project, "data.txt", TestContext.Current.CancellationToken)!;
        var change = Change("data.txt") with { Before = observed.Version, Expected = observed };
        var template = Template(project) with { ProjectIdentity = publisher.RootIdentity(project) };
        var plan = OriginalApplyPages.Create(template, [change], control, TestContext.Current.CancellationToken);
        var state = OriginalApplyPages.Initialize(plan, "fixed", control);
        string intent = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("fixed")))[..24];
        string foreign = Path.Combine(project, ".splinter-apply-" + plan.OwnerId + "-" + intent + "-0.new"); File.WriteAllText(foreign, "foreign"); int saves = 0;
        Assert.Equal("OriginalApplyConflict", Assert.Throws<WorkspaceException>(() => publisher.Publish(state,
            _ => new MemoryStream(new byte[] { 1 }), () => saves++, TestContext.Current.CancellationToken)).Code);
        Assert.Equal(0, saves); Assert.Equal("pending", state.StepAt(0).Phase); Assert.Null(state.StepAt(0).StageNonce);
        Assert.Equal("foreign", File.ReadAllText(foreign)); Assert.Equal("basis", File.ReadAllText(Path.Combine(project, "data.txt")));
    }
    private static OriginalApplyChange Change(string path) => new(path, null, new("file", 1,
        Convert.ToHexStringLower(SHA256.HashData(new byte[] { 1 })), 32, DateTime.FromFileTimeUtc(0).Ticks), null, false);
    private static OriginalApplyPlan Template(string project) => new(3, new string('a', 24), "request", "base", "result", project, "root", [], [], "");
    private static WorkspaceRecord Workspace(string project) => new()
    {
        Id = new string('a', 24), RequestKey = "request", RequestDigest = "request", ProjectPath = project, Path = project,
        SourceReference = "source", BaseReference = "base", BaseRevision = "base", ResultRevision = "result",
        SourceKind = "none", StorageMode = "project-snapshot", ApplicationKind = "none"
    };
}
