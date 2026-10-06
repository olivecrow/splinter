using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;
using Splinter.Workflows.Windows;

namespace Splinter.Tests;

public sealed class ViewLifecycleStateTests
{
    [Theory]
    [InlineData("unsupported-action")][InlineData("")]
    public async Task RemovedActionsAreRejectedBeforeWritingAnyState(string action)
    {
        using var dir = new TestDirectory(); string home = Path.Combine(dir.Path, "untouched");
        var product = new ViewLifecycle(new WorkspaceStore(home));
        var before = Directory.GetFileSystemEntries(home, "*", SearchOption.AllDirectories).Order().ToArray();
        var request = new ViewLifecycleRequest(1, action, "missing-project", new("owner", 1), "removed");
        var error = await Assert.ThrowsAsync<WorkspaceException>(() => product.ExecuteAsync(request, TestContext.Current.CancellationToken));
        Assert.Equal("ViewRequestInvalid", error.Code);
        Assert.Equal(before, Directory.GetFileSystemEntries(home, "*", SearchOption.AllDirectories).Order().ToArray());
    }

    [Theory]
    [InlineData("source")][InlineData("control")][InlineData("snapshot-owner")]
    [InlineData("application")][InlineData("managed-paths")][InlineData("cache")]
    [InlineData("cache-owner")][InlineData("cache-proof")][InlineData("cache-partition")]
    public void PersistedGenerationCannotRedirectOwnedResources(string corruption)
    {
        using var dir = new TestDirectory(); var store = new WorkspaceStore(Path.Combine(dir.Path, "home"));
        string project = Path.Combine(dir.Path, "project");
        var provider = new WindowsViewProvider(store, project);
        string workerId = new('a', 24), generation = new('b', 24), viewId = "view";
        var owner = new ViewOwner("owner", 1); var workerOwner = new ViewOwner("worker", 1);
        var snapshot = new SnapshotReference("snapshot", "fake", project, "immutable", DateTimeOffset.UtcNow, true, true, "fixture");
        var worker = new WorkerBinding(1, provider.ProjectId, workerId, workerOwner, "compatibility", new string('c', 40));
        string cacheId = WorkspaceStore.Key(provider.ProjectId + ":cache:" + viewId);
        store.Save(new WorkspaceRecord { Id = workerId, RequestKey = workerId, ProjectPath = project,
            SourceReference = "development", BaseReference = worker.BaseRevision,
            Path = store.WorkPath(workerId), StorageMode = "ntfs-snapshot", Snapshot = new() { Reference = snapshot } }, "fixture");
        var cache = new ViewCacheIdentity(viewId, cacheId, snapshot,
            worker.CompatibilityKey, "unity-top-level-v1", SnapshotWorkspaceState.DefaultJournalBytes);
        var view = new ViewBinding(1, provider.ProjectId, owner, cache, Path.Combine(store.Root, "validation-mounts", WorkspaceStore.Key(provider.ProjectId + ":" + viewId)), workerId);
        ProductState.Save(provider.WorkerPath(workerId), worker); ProductState.Save(provider.ViewPath(viewId), view);
        var seal = new SnapshotDeltaSeal(1, 10, new string('d', 64));
        var publication = new CandidatePublicationReceipt(new("candidate", provider.ProjectId, "candidate", new(workerId, workerOwner, snapshot, seal), "source-only-v1"), seal, "candidate");
        var selection = new ViewSelectionReceipt(new(generation, provider.ProjectId, generation, owner, cache, publication), generation);
        string control = provider.GenerationPath(generation);
        var identity = new OverlayIdentity(2, generation, snapshot, cache.MaxJournalBytes, 1024 * 1024, [".git"]);
        var cacheIdentity = WindowsViewProvider.CacheIdentity(cache);
        var request = new OverlayServeRequest(identity, Path.Combine(control, "source"), view.MountPath, Path.Combine(control, "overlay"), "token",
            new(snapshot.Id, snapshot.RootPath, "proof"), Path.Combine(store.ControlPath(workerId), "snapshot"), workerId,
            new(provider.CandidatePath("candidate"), provider.CachePath(cacheId), WindowsViewProvider.CacheRoots, cacheIdentity, "snapshot"),
            new(new(snapshot.Id, snapshot.RootPath, "proof"), Path.Combine(store.ControlPath(workerId), "snapshot"), workerId));
        var workspace = new WorkspaceRecord { Id = generation, RequestKey = generation, SourceReference = "view-source", BaseReference = worker.BaseRevision,
            ProjectPath = project, Path = view.MountPath, StorageMode = "ntfs-snapshot",
            ManagedPaths = provider.Managed, Snapshot = new() { Reference = snapshot } };
        var g = new ViewGeneration(1, selection, workerId, worker.BaseRevision, workspace, request);
        string path = Path.Combine(control, "generation.json"); ProductState.Save(path, g);
        Assert.Equal(g.Request.DeltaPath, provider.Generation(generation).Request.DeltaPath);
        if (corruption == "source") g = g with { Request = request with { DeltaPath = Path.Combine(dir.Path, "unrelated") } };
        if (corruption == "control") g = g with { Request = request with { ControlPath = Path.Combine(dir.Path, "unrelated") } };
        if (corruption == "snapshot-owner") g = g with { Request = request with { SnapshotOwnerId = new string('e', 24) } };
        if (corruption == "application") workspace.Path = Path.Combine(dir.Path, "unrelated");
        if (corruption == "managed-paths") workspace.ManagedPaths = ["Assets"];
        if (corruption == "cache") g = g with { Request = request with { SourceView = request.SourceView! with { CacheDeltaPath = Path.Combine(dir.Path, "unrelated") } } };
        if (corruption == "cache-owner") g = g with { Request = request with { CacheSnapshot = request.CacheSnapshot! with { OwnerId = "foreign" } } };
        if (corruption == "cache-proof") g = g with { Request = request with { CacheSnapshot = null } };
        if (corruption == "cache-partition") g = g with { Request = request with { SourceView = request.SourceView! with {
            CacheIdentity = cacheIdentity with { IncludedTopLevel = ["Assets"] } } } };
        ProductState.Save(path, g); byte[] before = File.ReadAllBytes(path);
        Assert.Equal("ViewReceiptMismatch", Assert.Throws<WorkspaceException>(() => provider.Generation(generation)).Code);
        Assert.Equal(before, File.ReadAllBytes(path)); Assert.False(Directory.Exists(Path.Combine(dir.Path, "unrelated")));
    }
}
