using System.Text.Json;
using Splinter.Core;
using Splinter.Runtime;
using Splinter.Protocol;
using Splinter.Platform.Windows;

namespace Splinter.Tests;

public sealed partial class SnapshotWorkspaceCoordinatorTests
{
    [Fact]
    public async Task PlainProjectKeepsBasisAcrossReplyLossAndReclaimsOnlyItsTask()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        f.Backend.LoseCaptureReply = true;
        string basisId = new('a', 24);
        Task<WorkspaceView> Ensure() => f.New().EnsureAsync(f.Project, "plain-task", "snapshot", "project-snapshot", false, ct,
            sourceKind: "none", applicationKind: "none", generatedDirectoryId: basisId);
        Assert.Equal("ReplyLost", (await Assert.ThrowsAsync<WorkspaceException>(Ensure)).Code);
        var ready = await Ensure();
        Assert.Equal(1, f.Backend.Captures); Assert.Empty(ready.Workspace.ManagedPaths);
        Assert.Equal("", ready.Workspace.GeneratedDirectory!.RelativePath);
        Assert.Equal(ready.Workspace.Id, (await Ensure()).Workspace.Id);
        Assert.Equal("RequestKeyConflict", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().EnsureAsync(f.Project, "plain-task", "snapshot", "project-snapshot", false, ct,
            sourceKind: "none", applicationKind: "none", generatedDirectoryId: new string('b', 24)))).Code);
        await f.New().CheckpointAsync(ready.Workspace.Id, "independent plain result", ct);
        var store = new WorkspaceStore(f.Home);
        string peerId = WorkspaceStore.Key("plain-peer");
        var peer = new WorkspaceRecord { Id = peerId, RequestKey = "plain-peer", ProjectPath = f.Project, Path = store.WorkPath(peerId),
            BaseReference = "snapshot", SourceReference = "peer", Phase = "ready", StorageMode = "project-snapshot", SourceKind = "none",
            GeneratedDirectoryId = basisId, GeneratedDirectory = ready.Workspace.GeneratedDirectory,
            Snapshot = new() { Reference = ready.Workspace.Snapshot!.Reference, Attached = true, Step = "ready" } };
        store.Save(peer, "fixture-common-project-dependency");
        Assert.Equal("released", (await f.New().ReleaseAsync(ready.Workspace.Id, ct)).Workspace.Phase);
        Assert.Equal("ready", store.Get(peerId).Phase);
    }
    [Fact]
    public async Task PlainProjectNeverFallsBackWithoutExplicitBasisOrSource()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        foreach (string source in new[] { "none", "git", "plastic" })
            await Assert.ThrowsAsync<WorkspaceException>(() => f.New().EnsureAsync(f.Project, "invalid", "snapshot", "project-snapshot", false, ct,
                sourceKind: source, applicationKind: "none"));
        Assert.Equal(0, f.Backend.Captures); Assert.Empty(new WorkspaceStore(f.Home).List(null));
    }
}

public sealed class PlainProjectBasisTests
{
    [Fact]
    public void ProjectContractRequiresFixedNoneBasisAndKeepsLibraryScopeSeparate()
    {
        foreach (bool development in new[] { false, true })
        {
            var catalog = new ContractCatalog(development);
            bool Valid(object value) => catalog.Validate(catalog.Find("workspace.ensure")!.InputSchema, JsonSerializer.SerializeToElement(value)).IsValid;
            Assert.True(Valid(new { project = "source", key = "task", storage = "project-snapshot", source = "none", application = "unity", projectBasis = new string('a', 24), maxJournalBytes = 4096 }));
            Assert.False(Valid(new { project = "source", key = "task", storage = "project-snapshot", source = "none" }));
            Assert.False(Valid(new { project = "source", key = "task", storage = "project-snapshot", source = "git", projectBasis = new string('a', 24) }));
            Assert.False(Valid(new { project = "source", key = "task", storage = "git-snapshot", projectBasis = new string('a', 24) }));
            Assert.False(Valid(new { project = "source", key = "task", storage = "project-snapshot", source = "none", projectBasis = new string('a', 24), libraryBasis = new string('b', 24) }));
            Assert.NotNull(catalog.Find("project.prepare")); Assert.NotNull(catalog.Find("project.restore"));
        }
    }
    [Fact]
    public void SourceAndLibraryUseOneFrozenTreeWithThreePrivateRangeJournals()
    {
        using var d = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        tree.Files["Assets/code"] = new byte[65536]; tree.Files["Library/db"] = new byte[65536];
        for (int i = 0; i < 3; i++)
        {
            using var delta = new JournalDeltaStore(Path.Combine(d.Path, i.ToString()), new(2, "writer-" + i, tree.Snapshot, 1024 * 1024, 65536), tree);
            foreach (string name in new[] { "Assets/code", "Library/db" })
            {
                delta.Write(delta.Stat(name)!.Id, 100, [(byte)(i + 1)]);
                byte[] value = new byte[1]; delta.Read(delta.Stat(name)!.Id, 100, value); Assert.Equal(i + 1, value[0]);
            }
            Assert.Equal(0, delta.Observe().InitialPayloadCopiedBytes); Assert.Equal(0, delta.Observe().ReadCacheBytes);
            Assert.True(delta.Observe().JournalBytes < 65536);
        }
        Assert.Equal(0, tree.Files["Assets/code"][100]); Assert.Equal(0, tree.Files["Library/db"][100]);
    }
    [Fact]
    public void VcsAdministrationRefusalPrecedesAnySourceMove()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows file sharing or native contract.");
        using var d = new TestDirectory(); string source = Path.Combine(d.Path, "source"); Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "keep"), "intact"); Directory.CreateDirectory(Path.Combine(source, ".git"));
        var manager = new ManagedDirectoryBasis(new WorkspaceStore(Path.Combine(d.Path, "home")), ApplicationProjectPolicies.None, projectBasis: true);
        Assert.Equal("VcsMetadataUnsupported", Assert.Throws<WorkspaceException>(() => manager.PrepareAsync(source, "none", "none", TestContext.Current.CancellationToken).GetAwaiter().GetResult()).Code);
        Assert.Equal("intact", File.ReadAllText(Path.Combine(source, "keep")));
        Assert.False(Directory.Exists(Path.Combine(d.Path, "home", "project-bases")));
    }
    [Fact]
    public async Task SharedReferenceCannotSubstituteAnotherTaskRequest()
    {
        using var d = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        var store = new WorkspaceStore(Path.Combine(d.Path, "home"));
        string id = new('a', 24), basisId = new('b', 24), project = Path.Combine(d.Path, "source");
        Directory.CreateDirectory(project);
        string receiptRoot = Path.Combine(store.Root, "project-bases", basisId), root = Path.Combine(receiptRoot, "base");
        var snapshot = new SnapshotReference(basisId, "owned-directory", root, root, DateTimeOffset.UtcNow, true, true, "stopped-original-owned-directory");
        var basis = new GeneratedDirectoryReference(basisId, project, "", snapshot, receiptRoot, basisId, new string('c', 64));
        var w = new WorkspaceRecord { Id = id, RequestKey = "task", ProjectPath = project, Path = store.WorkPath(id), SourceKind = "none", ApplicationKind = "none",
            StorageMode = "project-snapshot", BaseReference = "snapshot", SourceReference = "source", GeneratedDirectoryId = basisId, GeneratedDirectory = basis,
            RequestDigest = new string('d', 24), Phase = "ready", Snapshot = new() { Reference = snapshot, Step = "ready", Attached = true } };
        store.Save(w, "fixed-request");
        string directory = Path.Combine(store.ControlPath(id), "snapshot");
        await ProjectDirectorySnapshotProvider.CaptureAsync(directory, w, ct);
        string path = Path.Combine(directory, "project-reference.json");
        File.WriteAllText(path, File.ReadAllText(path).Replace(new string('d', 24), new string('e', 24)));
        var provider = new ProjectDirectorySnapshotProvider(directory, id);
        Assert.Equal("ProjectBasisMismatch", (await Assert.ThrowsAsync<WorkspaceException>(() => provider.ObserveAsync(snapshot, ct))).Code);
        Assert.True(File.Exists(path)); Assert.False(File.Exists(Path.Combine(directory, "project-released.json")));
    }
}
