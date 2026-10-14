using System.Security.Cryptography;
using System.Text.Json;
using Splinter.Core;
using Splinter.SourceControl.Git;

namespace Splinter.Tests;

public sealed partial class SourceFingerprintTests
{
    [Fact]
    public async Task InitialAdmissionReusesCompleteContentAfterRefreshAndAuditStillReadsFresh()
    {
        using var f = await Fixture.Create(1024 * 1024);
        f.Workspace.StorageMode = "git-snapshot";
        SeedSummaries(f);
        var provider = f.Provider();
        // Keep the real attach refresh; the optimization removes the next
        // content pass rather than deferring refresh work to checkpoint.
        // A real mount first creates its untracked .git pointer through the
        // journal. That administration mutation must precede admission.
        f.Tracker.Mark(".git");
        await TestDirectory.Git(f.Workspace.Path, "update-index", "--refresh");
        await provider.BeginInitialContentInspectionAsync(f.Workspace, TestContext.Current.CancellationToken);
        SourceState initial;
        using (new FileStream(f.Path("large.bin"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            initial = await provider.InspectAsync(f.Workspace, TestContext.Current.CancellationToken);
        Assert.False(initial.HasChanges);
        Assert.Equal(0, initial.Inspection!.PayloadBytesHashed);
        Assert.Equal(0, initial.Inspection.GitHashInputBytes);
        Assert.Equal(4, initial.Inspection.FilesReused);
        Assert.Equal(await f.Oracle(), initial.Fingerprint);
        var audit = await f.Provider(true).InspectAsync(f.Workspace, TestContext.Current.CancellationToken);
        Assert.True(audit.Inspection!.PayloadBytesHashed >= 1024 * 1024);
        Assert.Equal(0, audit.Inspection.FilesReused);
        TestContext.Current.TestOutputHelper!.WriteLine(JsonSerializer.Serialize(new { initial = initial.Inspection, audit = audit.Inspection }));
    }

    [Fact]
    public async Task SourceChangedDuringAttachIsReadFreshWhileOtherProofsRemainUsable()
    {
        using var f = await Fixture.Create(1024 * 1024); f.Workspace.StorageMode = "git-snapshot"; SeedSummaries(f);
        f.Tracker.Mark(".git");
        var timestamp = File.GetLastWriteTimeUtc(f.Path("code.cs"));
        File.WriteAllText(f.Path("code.cs"), "return 2;\n");
        File.SetLastWriteTimeUtc(f.Path("code.cs"), timestamp);
        f.Tracker.Mark("code.cs");
        var provider = f.Provider();
        await provider.BeginInitialContentInspectionAsync(f.Workspace, TestContext.Current.CancellationToken);
        SourceState result;
        using (new FileStream(f.Path("large.bin"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            result = await provider.InspectAsync(f.Workspace, TestContext.Current.CancellationToken);
        Assert.Equal(10, result.Inspection!.PayloadBytesHashed);
        Assert.True(result.HasChanges); Assert.Contains("code.cs", result.ChangedPaths);
        Assert.Equal(await f.Oracle(), result.Fingerprint);
    }

    [Theory]
    [InlineData("missing-proof")]
    [InlineData("reconnected")]
    [InlineData("another-command")]
    [InlineData("already-attached")]
    public async Task MissingOrIneligibleInitialProofKeepsFreshReads(string boundary)
    {
        using var f = await Fixture.Create(); f.Workspace.StorageMode = "git-snapshot"; SeedSummaries(f);
        if (boundary == "missing-proof") f.Tracker.InitialSummaries!.Remove("large.bin");
        if (boundary == "reconnected") f.Tracker.InitialSummaries = null;
        if (boundary == "already-attached") f.Workspace.Snapshot!.Attached = true;
        var provider = f.Provider();
        await provider.BeginInitialContentInspectionAsync(f.Workspace, TestContext.Current.CancellationToken);
        if (boundary == "another-command") provider = f.Provider();
        var source = await provider.InspectAsync(f.Workspace, TestContext.Current.CancellationToken);
        Assert.True(source.Inspection!.PayloadBytesHashed >= 8192);
        Assert.Equal(await f.Oracle(), source.Fingerprint);
    }

    [Theory]
    [InlineData("epoch")]
    [InlineData("sequence")]
    [InlineData("length")]
    [InlineData("snapshot")]
    [InlineData("duplicate")]
    [InlineData("missing-files")]
    [InlineData("missing-generation")]
    public async Task InvalidInitialEvidenceCannotPublishManifest(string fault)
    {
        using var f = await Fixture.Create(); f.Workspace.StorageMode = "git-snapshot"; SeedSummaries(f);
        var provider = f.Provider();
        await provider.BeginInitialContentInspectionAsync(f.Workspace, TestContext.Current.CancellationToken);
        if (fault == "epoch") f.Tracker.Epoch = "new-provider";
        if (fault == "sequence") f.Tracker.Mark("code.cs");
        if (fault == "length") f.Tracker.InitialSummaries!["large.bin"] = f.Tracker.InitialSummaries["large.bin"] with { Length = 1 };
        if (fault == "snapshot") f.Tracker.TransformInitial = view => view with { Snapshot = view.Snapshot with { Id = "other" } };
        if (fault == "duplicate") f.Tracker.TransformInitial = view => view with { Files = [view.Files[0], view.Files[0]] };
        if (fault == "missing-files") f.Tracker.TransformInitial = view => view with { Files = null! };
        if (fault == "missing-generation") f.Tracker.TransformInitial = view => view with { Generation = null! };
        var error = await Assert.ThrowsAsync<WorkspaceException>(() => provider.InspectAsync(f.Workspace, TestContext.Current.CancellationToken));
        Assert.Equal(fault is "epoch" or "sequence" ? "SourceChanged" : "SourceObservationInvalid", error.Code);
        Assert.False(File.Exists(f.Tracker.CachePath(f.Workspace)));
        // Failed admission is consumed: another inspection must read fresh.
        f.Tracker.TransformInitial = null;
        Assert.True((await provider.InspectAsync(f.Workspace, TestContext.Current.CancellationToken)).Inspection!.PayloadBytesHashed >= 8192);
    }

    [Fact]
    public async Task UnprovedCleanConversionStillUsesGitWhenRawInitialProofIsAvailable()
    {
        using var f = await Fixture.Create(); f.Workspace.StorageMode = "git-snapshot";
        File.WriteAllText(f.Path(".gitattributes"), "code.cs ident\n");
        await TestDirectory.Git(f.Workspace.Path, "add", ".gitattributes");
        await TestDirectory.Git(f.Workspace.Path, "commit", "-m", "identity conversion boundary");
        f.Workspace.BaseRevision = await TestDirectory.Git(f.Workspace.Path, "rev-parse", "HEAD");
        SeedSummaries(f); var provider = f.Provider();
        await provider.BeginInitialContentInspectionAsync(f.Workspace, TestContext.Current.CancellationToken);
        var initial = await provider.InspectAsync(f.Workspace, TestContext.Current.CancellationToken);
        Assert.Equal(0, initial.Inspection!.PayloadBytesHashed);
        Assert.Equal(new FileInfo(f.Path("code.cs")).Length, initial.Inspection.GitHashInputBytes);
        Assert.Equal(1, initial.Inspection.GitHashProcesses);
        Assert.False(initial.HasChanges);
    }

    private static void SeedSummaries(Fixture f)
    {
        f.Tracker.InitialSummaries = Directory.GetFiles(f.Workspace.Path).Where(path => Path.GetFileName(path) != ".git")
            .ToDictionary(path => Path.GetFileName(path)!, path =>
            {
                byte[] bytes = File.ReadAllBytes(path);
                return new SnapshotContentSummary(Path.GetFileName(path), bytes.Length,
                    Convert.ToHexStringLower(SHA256.HashData(bytes)), bytes.Contains((byte)'\r'), bytes.Contains((byte)0));
            }, StringComparer.Ordinal)!;
    }

    private sealed partial class Tracker : ISourceContentSummaryProvider
    {
        public Dictionary<string, SnapshotContentSummary>? InitialSummaries;
        public Func<SourceContentSummaryView, SourceContentSummaryView>? TransformInitial;
        public Task<SourceContentSummaryView?> ObserveInitialContentAsync(WorkspaceRecord workspace,
            SourceChangeView expected, string[] paths, CancellationToken ct = default)
        {
            if (InitialSummaries is null) return Task.FromResult<SourceContentSummaryView?>(null);
            var view = new SourceContentSummaryView(workspace.Snapshot!.Reference!, expected,
                paths.Where(path => InitialSummaries.ContainsKey(path) &&
                    !expected.Changes.Any(change => SourcePathPolicy.Affects(change.Path, path)))
                    .Select(path => InitialSummaries[path]).ToArray());
            return Task.FromResult<SourceContentSummaryView?>(TransformInitial?.Invoke(view) ?? view);
        }
    }
}

public sealed partial class GitProofReuseTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ContentSummaryRequiresPublishedWholeBlobProofAndEvictsWithIt(bool packed)
    {
        using var f = await Fixture.Create(packed); using var tree = f.Open(entries: 1);
        Assert.Null(tree.GetVerifiedContentSummary("a.bin"));
        tree.ReadObserver = (_, stage) =>
        {
            if (stage is "header" or "verified") Assert.Null(tree.GetVerifiedContentSummary("a.bin"));
        };
        tree.Read("a.bin", 0, new byte[1]);
        tree.ReadObserver = null;
        var summary = Assert.IsType<SnapshotContentSummary>(tree.GetVerifiedContentSummary("a.bin"));
        Assert.Equal(f.A.Length, summary.Length);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(f.A)), summary.Sha256);
        Assert.True(summary.ContainsCarriageReturn); Assert.True(summary.ContainsNul);
        Assert.Equal(summary with { Path = "alias.bin" }, tree.GetVerifiedContentSummary("alias.bin"));
        long decoded = tree.DecodedBlobBytes;
        Assert.Equal(summary, tree.GetVerifiedContentSummary("a.bin"));
        Assert.Equal(decoded, tree.DecodedBlobBytes);
        tree.Read("b.bin", 0, new byte[1]);
        Assert.Null(tree.GetVerifiedContentSummary("a.bin"));
        Assert.NotNull(tree.GetVerifiedContentSummary("b.bin"));
        tree.Dispose();
        Assert.Throws<ObjectDisposedException>(() => tree.GetVerifiedContentSummary("b.bin"));
    }
}
