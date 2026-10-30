using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed partial class FileWorkspaceTests
{
    [Theory]
    [InlineData("none")] [InlineData("plastic")]
    public async Task SourceAdaptersKeepTheFixedBasisAndMultipleGenerationsAcrossContinuingInvocations(string source)
    {
        using var f = new Fixture(sourceKind: source); var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure("continuing-adapter", ct)).Workspace;
        string basis = w.BaseRevision!, originalAdministration = source == "plastic"
            ? File.ReadAllText(Path.Combine(f.Project, ".plastic", "plastic.workspace")) : "";
        File.WriteAllText(Path.Combine(w.Path, "data.txt"), "first source");
        var first = (await f.New().CheckpointAsync(w.Id, "first", "first", ct)).ResultGeneration!;
        File.WriteAllText(Path.Combine(w.Path, "data.txt"), "next source");
        Assert.True((await f.New().StatusAsync(w.Id, ct, verifySource: true)).Source!.HasChanges);
        Assert.Equal(first, (await f.New().CheckpointAsync(w.Id, "first", "first", ct)).ResultGeneration);
        var second = await f.New().CheckpointAsync(w.Id, "second", "second", ct);
        Assert.Equal(2, second.ResultGeneration!.Sequence); Assert.NotEqual(first.Revision, second.ResultGeneration.Revision);
        Assert.Equal(w.Path, second.Workspace.Path); Assert.Equal(basis, second.Workspace.BaseRevision);
        Assert.Equal("basis", File.ReadAllText(Path.Combine(f.Project, "data.txt")));
        if (source == "plastic") Assert.Equal(originalAdministration, File.ReadAllText(Path.Combine(f.Project, ".plastic", "plastic.workspace")));
        var released = await f.New().ReleaseAsync(w.Id, ct);
        Assert.Equal("released", released.Workspace.Phase); Assert.True(released.Workspace.Results.Count >= 2);
    }
    [Theory]
    [InlineData("old-version")] [InlineData("missing-results")] [InlineData("null-results")]
    [InlineData("null-generation")] [InlineData("wrong-result-owner")] [InlineData("unknown-result-field")]
    [InlineData("missing-apply-history")] [InlineData("duplicate-history-property")]
    public async Task DamagedOrPriorGenerationStateIsRefusedWithoutReclaimingOrRewritingIt(string damage)
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure("state-refusal", ct)).Workspace;
        await f.New().CheckpointAsync(w.Id, "first", "first", ct);
        string path = Path.Combine(f.Store.ControlPath(w.Id), "workspace.json"); var node = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        switch (damage)
        {
            case "old-version": node["version"] = 2; node.Remove("results"); node.Remove("originalApplies"); break;
            case "missing-results": node.Remove("results"); break;
            case "null-results": node["results"] = null; break;
            case "null-generation": node["results"]![0] = null; break;
            case "wrong-result-owner": node["results"]![0]!["id"] = new string('f', 24); break;
            case "unknown-result-field": node["results"]![0]!["legacy"] = true; break;
            case "missing-apply-history": node.Remove("originalApplies"); break;
        }
        string changed = node.ToJsonString();
        if (damage == "duplicate-history-property") changed = changed[..^1] + ",\"results\":[]}";
        File.WriteAllText(path, changed);
        Assert.Equal(damage == "old-version" ? "StateVersionUnsupported" : "StateCorrupt",
            (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ReleaseAsync(w.Id, ct))).Code);
        Assert.Equal(changed, File.ReadAllText(path)); Assert.True(Directory.Exists(w.Path));
        Assert.Empty(f.Backend.Released); Assert.Empty(f.Backend.Removed);
    }
    [Fact]
    public async Task FinalCaptureReturnsItsRequestedGenerationAndRetriesItAfterReclamation()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure("final-generation", ct)).Workspace;
        File.WriteAllText(Path.Combine(w.Path, "data.txt"), "final source");
        var first = await f.New().CheckpointAndReleaseAsync(w.Id, "final result", "final-key", ct);
        Assert.Equal("released", first.Workspace.Phase); Assert.Equal("final-key", first.ResultGeneration!.RequestKey);
        var retry = await f.New().CheckpointAndReleaseAsync(w.Id, "final result", "final-key", ct);
        Assert.Equal(first.ResultGeneration, retry.ResultGeneration); Assert.Equal(first.Workspace.Results.Count, retry.Workspace.Results.Count);
        Assert.Equal("WorkspaceReleased", (await Assert.ThrowsAsync<WorkspaceException>(() =>
            f.New().CheckpointAndReleaseAsync(w.Id, "another", "new-key", ct))).Code);
        Assert.Equal("SourceCaptureRequestConflict", (await Assert.ThrowsAsync<WorkspaceException>(() =>
            f.New().CheckpointAndReleaseAsync(w.Id, "another", "final-key", ct))).Code);
    }
    [Fact]
    public async Task ReclamationVerifiesOlderRetainedPayloadsAsWellAsTheLatestResult()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure("old-result-protection", ct)).Workspace;
        File.WriteAllText(Path.Combine(w.Path, "data.txt"), "first retained");
        await f.New().CheckpointAsync(w.Id, "first", "first", ct);
        File.WriteAllText(Path.Combine(w.Path, "data.txt"), "latest retained");
        await f.New().CheckpointAsync(w.Id, "latest", "latest", ct);
        string hash = Convert.ToHexStringLower(SHA256.HashData("first retained"u8));
        File.Delete(Path.Combine(f.Store.ControlPath(w.Id), "files", "objects", hash));
        Assert.Equal("FileResultIncomplete", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ReleaseAsync(w.Id, ct))).Code);
        Assert.True(Directory.Exists(w.Path)); Assert.Empty(f.Backend.Removed); Assert.Empty(f.Backend.Released);
        Assert.Equal(2, f.Store.Get(w.Id).Results.Count);
    }
    [Theory]
    [InlineData("stage-created")]
    [InlineData("staged")]
    [InlineData("retired-before-receipt")]
    [InlineData("retired")]
    [InlineData("published-before-receipt")]
    [InlineData("published")]
    [InlineData("retired-removed")]
    [InlineData("completed")]
    public async Task RepeatedPublicationRecoversFixedGenerationAndHistoryAcrossLostResponses(string boundary)
    {
        RequireWindowsApply(); bool armed = false, fired = false;
        var publisher = new WindowsOriginalFilePublisher((at, _) =>
        { if (armed && !fired && at == boundary) { fired = true; throw new WorkspaceException("ReplyLost", "Injected response loss."); } });
        using var f = new Fixture(publisher); var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure("repeat-recovery", ct)).Workspace;
        File.WriteAllText(Path.Combine(w.Path, "data.txt"), "first");
        var first = (await f.New().CheckpointAsync(w.Id, "first", "first", ct)).ResultGeneration!;
        var p1 = (await f.New().PlanOriginalAsync(w.Id, first.Id, null, ct)).OriginalApplyPlan!;
        await f.New().ApplyOriginalAsync(w.Id, first.Id, "apply-first", p1.Digest, null, ct);
        File.WriteAllText(Path.Combine(w.Path, "data.txt"), "second");
        var second = (await f.New().CheckpointAsync(w.Id, "second", "second", ct)).ResultGeneration!;
        var p2 = (await f.New().PlanOriginalAsync(w.Id, second.Id, null, ct)).OriginalApplyPlan!; armed = true;
        Assert.Equal("ReplyLost", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ApplyOriginalAsync(w.Id, second.Id, "apply-second", p2.Digest, null, ct))).Code);
        if (boundary != "completed")
        {
            Assert.Equal("OriginalApplyPending", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().CheckpointAsync(w.Id, "third", "third", ct))).Code);
            Assert.Equal("OriginalApplyPending", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ReleaseAsync(w.Id, ct))).Code);
        }
        var resumed = await f.New().ApplyOriginalAsync(w.Id, second.Id, "apply-second", p2.Digest, null, ct);
        Assert.Equal(2, resumed.Workspace.OriginalApplies.Count); Assert.True(resumed.OriginalApplyReceipt!.Completed);
        Assert.Equal(p1.Digest, resumed.Workspace.OriginalApplies[0].Plan.Digest);
        Assert.Equal(p1.Digest, resumed.Workspace.OriginalApplies[1].Plan.PreviousApplyDigest);
        Assert.Equal("second", File.ReadAllText(Path.Combine(f.Project, "data.txt")));
        File.WriteAllText(Path.Combine(f.Project, "data.txt"), "later original");
        var receipt = await f.New().ApplyOriginalAsync(w.Id, first.Id, "apply-first", p1.Digest, null, ct);
        Assert.Equal("apply-first", receipt.OriginalApplyReceipt!.IntentKey);
        Assert.Equal("later original", File.ReadAllText(Path.Combine(f.Project, "data.txt")));
        Assert.Equal(first.Id, (await f.New().CheckpointAsync(w.Id, "first", "first", ct)).ResultGeneration!.Id);
        Assert.Equal(2, f.Store.Get(w.Id).Results.Count);
    }
    [Fact]
    public async Task ContinuingTaskPublishesFrozenGenerationsAndSelectedHistoryWithoutReplayingOldWrites()
    {
        RequireWindowsApply(); using var f = new Fixture(new WindowsOriginalFilePublisher()); var ct = TestContext.Current.CancellationToken;
        var a = (await f.Ensure("continuing-a", ct)).Workspace;
        var b = (await f.Ensure("continuing-b", ct)).Workspace;
        File.WriteAllText(Path.Combine(a.Path, "data.txt"), "first");
        var first = (await f.New().CheckpointAsync(a.Id, "first result", "capture-1", ct)).Workspace.Results.Single();
        File.WriteAllText(Path.Combine(a.Path, "data.txt"), "not collected yet");
        var plan = (await f.New().PlanOriginalAsync(a.Id, first.Id, null, ct)).OriginalApplyPlan!;
        Assert.Equal(2, plan.Version); Assert.Empty(plan.Conflicts);
        await f.New().ApplyOriginalAsync(a.Id, first.Id, "publish-1", plan.Digest, null, ct);
        Assert.Equal("first", File.ReadAllText(Path.Combine(f.Project, "data.txt")));
        Assert.Equal("not collected yet", File.ReadAllText(Path.Combine(a.Path, "data.txt")));
        Assert.Equal("basis", File.ReadAllText(Path.Combine(b.Path, "data.txt")));
        File.WriteAllText(Path.Combine(a.Path, "data.txt"), "second");
        File.WriteAllText(Path.Combine(a.Path, "selected.txt"), "selected");
        var second = (await f.New().CheckpointAsync(a.Id, "second result", "capture-2", ct)).Workspace.Results.Last();
        Assert.Equal("ResultNotReady", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().PlanOriginalAsync(a.Id, first.Id, null, ct))).Code);
        Assert.Equal("SourceCaptureRequestConflict", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().CheckpointAsync(a.Id, "changed message", "capture-1", ct))).Code);
        var selected = (await f.New().PlanOriginalAsync(a.Id, second.Id, null, ct, ["data.txt"])).OriginalApplyPlan!;
        Assert.Empty(selected.Conflicts);
        await f.New().ApplyOriginalAsync(a.Id, second.Id, "publish-2", selected.Digest, null, ct, ["data.txt"]);
        Assert.Equal("second", File.ReadAllText(Path.Combine(f.Project, "data.txt")));
        Assert.False(File.Exists(Path.Combine(f.Project, "selected.txt")));
        var remainder = (await f.New().PlanOriginalAsync(a.Id, second.Id, null, ct, ["selected.txt"])).OriginalApplyPlan!;
        await f.New().ApplyOriginalAsync(a.Id, second.Id, "publish-selected", remainder.Digest, null, ct, ["selected.txt"]);
        File.WriteAllText(Path.Combine(a.Path, "data.txt"), "basis");
        File.Delete(Path.Combine(a.Path, "selected.txt"));
        var third = (await f.New().CheckpointAsync(a.Id, "revert result", "capture-3", ct)).Workspace.Results.Last();
        var revert = (await f.New().PlanOriginalAsync(a.Id, third.Id, null, ct)).OriginalApplyPlan!;
        Assert.Empty(revert.Conflicts);
        await f.New().ApplyOriginalAsync(a.Id, third.Id, "publish-3", revert.Digest, null, ct);
        Assert.Equal("basis", File.ReadAllText(Path.Combine(f.Project, "data.txt")));
        Assert.False(File.Exists(Path.Combine(f.Project, "selected.txt")));
        File.WriteAllText(Path.Combine(f.Project, "data.txt"), "later original");
        await f.New().ApplyOriginalAsync(a.Id, first.Id, "publish-1", plan.Digest, null, ct);
        Assert.Equal("later original", File.ReadAllText(Path.Combine(f.Project, "data.txt")));
        var same = await f.New().CheckpointAsync(a.Id, "first result", "capture-1", ct);
        Assert.Equal(3, same.Workspace.Results.Count); Assert.Equal(4, same.Workspace.OriginalApplies.Count);
        Assert.Equal(a.Path, same.Workspace.Path); Assert.Empty(f.Backend.Released);
    }

    private sealed partial class Backend : ISourceGenerationBackend
    {
        private readonly Dictionary<string, FrozenDirectoryTree> sourceGenerations = [];
        public Task<SourceCaptureIdentity> CaptureSourceAsync(WorkspaceRecord w, string control, string id, CancellationToken ct = default)
        {
            if (!sourceGenerations.TryGetValue(id, out var tree))
            {
                string capture = Path.Combine(control, "fake-source-captures", id);
                if (!Directory.Exists(capture)) Copy(w.Path, capture);
                sourceGenerations[id] = tree = new(capture, w.Snapshot!.Reference!, new FrozenDirectoryTree(Path.Combine(control, "fake-immutable"), w.Snapshot.Reference!));
            }
            return Task.FromResult(new SourceCaptureIdentity(id, 1, tree.Digest));
        }
        public Task<ISourceCaptureTree> OpenSourceCaptureAsync(WorkspaceRecord w, string control, string id, CancellationToken ct = default) => Task.FromResult<ISourceCaptureTree>(sourceGenerations[id]);
        public Task CompleteSourceCaptureAsync(WorkspaceRecord w, string control, string id, CancellationToken ct = default) => Task.CompletedTask;
    }
    // Test double only: production captures use durable backing aliases and
    // never materialize a tree into RAM or copy a whole source directory.
    private sealed class FrozenDirectoryTree : ISourceCaptureTree
    {
        private readonly Dictionary<string, SnapshotEntry> entries = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, byte[]> payloads = new(StringComparer.OrdinalIgnoreCase);
        public SnapshotReference Snapshot { get; }
        public ISnapshotTree Basis { get; }
        public string Digest { get; }
        public FrozenDirectoryTree(string root, SnapshotReference snapshot, ISnapshotTree? basis = null)
        {
            Snapshot = snapshot; Basis = basis ?? this;
            entries[""] = new("", true, 0, 16, 0, 0, 0);
            foreach (string path in Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(root, path).Replace('\\', '/'); var attributes = File.GetAttributes(path);
                bool directory = (attributes & FileAttributes.Directory) != 0;
                byte[] bytes = directory ? [] : File.ReadAllBytes(path);
                entries.Add(relative, new(relative, directory, bytes.Length, (uint)attributes, File.GetCreationTimeUtc(path).ToFileTimeUtc(),
                    File.GetLastAccessTimeUtc(path).ToFileTimeUtc(), File.GetLastWriteTimeUtc(path).ToFileTimeUtc()));
                if (!directory) payloads.Add(relative, bytes);
            }
            Digest = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(payloads)));
        }
        public void EnsureAvailable() { }
        public SnapshotEntry? Stat(string path) => entries.GetValueOrDefault(path);
        public IReadOnlyList<SnapshotEntry> List(string path) => entries.Values.Where(e => e.Path.Length > 0 &&
            (e.Path.LastIndexOf('/') < 0 ? "" : e.Path[..e.Path.LastIndexOf('/')]) == path).OrderBy(e => e.Path).ToArray();
        public int Read(string path, long offset, Span<byte> output)
        { byte[] bytes = payloads[path]; int n = (int)Math.Min(output.Length, Math.Max(0, bytes.Length - offset)); bytes.AsSpan((int)offset, n).CopyTo(output); return n; }
        public void Dispose() { }
    }
}
