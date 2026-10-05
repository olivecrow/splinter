using System.Security.Cryptography;
using System.Text.Json;
using Splinter.Core;
using Splinter.SourceControl.Files;

namespace Splinter.Tests;

public sealed partial class FileWorkspaceTests
{
    [Fact]
    public async Task IndependentResultVerifiesEachSharedObjectOncePerInvocationAndRechecksLaterCorruption()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        for (int i = 0; i < 32; i++) File.WriteAllText(Path.Combine(f.Project, $"same{i}.txt"), "basis");
        var w = (await f.Ensure("shared-objects", ct)).Workspace; await f.New().CheckpointAsync(w.Id, "result", ct);
        int objectOpens = 0; string control = f.Store.ControlPath(w.Id);
        Stream Open(string path)
        { if (Path.GetDirectoryName(path) == Path.Combine(control, "files", "objects")) objectOpens++; return File.OpenRead(path); }
        FileWorkspaceProvider.VerifyIndependentResult(f.Store.Get(w.Id), control, Open);
        Assert.Equal(1, objectOpens); Assert.Equal(1, f.Store.Get(w.Id).ResultStorage!.PayloadObjects);
        File.WriteAllText(Assert.Single(Directory.GetFiles(Path.Combine(control, "files", "objects"))), "corrupt");
        Assert.Equal("FileResultIncomplete", Assert.Throws<WorkspaceException>(() => FileWorkspaceProvider.VerifyIndependentResult(f.Store.Get(w.Id), control, Open)).Code);
        Assert.Equal(2, objectOpens);
    }

    [Fact]
    public async Task IndependentResultRejectsConflictingObjectLengthsBeforeReadingPayloads()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure("object-lengths", ct)).Workspace; await f.New().CheckpointAsync(w.Id, "result", ct);
        w = f.Store.Get(w.Id); string control = f.Store.ControlPath(w.Id);
        string resultPath = Path.Combine(control, "files", "result.json"); byte[] original = File.ReadAllBytes(resultPath);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var result = JsonSerializer.Deserialize<FileSourceManifest>(original, json)!;
        var entries = result.Entries.Select((e, i) => i == 0 ? e with { Length = e.Length + 1 } : e).ToArray();
        string identity = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(entries, json)));
        result = result with { Identity = identity, Entries = entries }; w.ResultRevision = identity;
        var intent = new FileCollectionIntent(w.Id, w.RequestDigest!, w.BaseRevision!, identity); int objectOpens = 0;
        Stream Open(string path)
        {
            if (path == resultPath) return new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(result, json));
            if (Path.GetFileName(path) == "collection.json") return new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(intent, json));
            if (Path.GetDirectoryName(path) == Path.Combine(control, "files", "objects")) objectOpens++;
            return File.OpenRead(path);
        }
        Assert.Equal("FileSourceStateInvalid", Assert.Throws<WorkspaceException>(() => FileWorkspaceProvider.VerifyIndependentResult(w, control, Open)).Code);
        Assert.Equal(0, objectOpens); Assert.Equal(original, File.ReadAllBytes(resultPath));
    }

    [Fact]
    public async Task SelectedParentPathsAndDeletionGuardsPreserveBoundarySiblingsAndForeignChildren()
    {
        using var f = new Fixture(new FakePublisher()); var ct = TestContext.Current.CancellationToken;
        for (int i = 0; i < 20; i++)
        { Directory.CreateDirectory(Path.Combine(f.Project, $"dir{i}")); File.WriteAllText(Path.Combine(f.Project, $"dir{i}", "file"), "basis"); }
        var w = (await f.Ensure("indexed-plan", ct)).Workspace;
        Directory.Delete(Path.Combine(w.Path, "dir1"), true); Directory.Delete(Path.Combine(w.Path, "dir10"), true);
        await f.New().CheckpointAsync(w.Id, "result", ct);
        File.WriteAllText(Path.Combine(f.Project, "dir1", "foreign"), "retain");
        var selected = (await f.New().PlanOriginalAsync(w.Id, ct, ["dir1", "dir1/file"])).OriginalApplyPlan!;
        Assert.Equal(new[] { "dir1", "dir1/file" }, selected.SelectedPaths);
        Assert.Equal(new[] { "dir1/file", "dir1" }, selected.Changes.Select(c => c.Path).ToArray());
        Assert.Single(selected.Conflicts); Assert.StartsWith("dir1:", selected.Conflicts[0]);
        var sibling = (await f.New().PlanOriginalAsync(w.Id, ct, ["dir10"])).OriginalApplyPlan!;
        Assert.Empty(sibling.Conflicts); Assert.All(sibling.Changes, c => Assert.StartsWith("dir10", c.Path));
        Assert.Equal("OriginalApplyRequestInvalid", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().PlanOriginalAsync(w.Id, ct, ["missing"]))).Code);
    }
}
