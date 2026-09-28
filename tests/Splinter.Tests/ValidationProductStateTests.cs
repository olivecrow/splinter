using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;
using Splinter.Workflows.Windows;

namespace Splinter.Tests;

public sealed class ValidationProductStateTests
{
    [Theory]
    [InlineData("Assets/~UnityDirMonSyncFile~0123456789abcdef0123456789abcdef~", true, 1)]
    [InlineData("ProjectSettings/~UnityDirMonSyncFile~0123456789abcdef0123456789abcdef~", true, 1)]
    [InlineData("Assets/~UnityDirMonSyncFile~0123456789abcdef0123456789abcdef~", false, 2)]
    [InlineData("Assets/sub/~UnityDirMonSyncFile~0123456789abcdef0123456789abcdef~", true, 2)]
    [InlineData("Assets/~UnityDirMonSyncFile~user-file~", true, 2)]
    [InlineData("Assets/user.cs", true, 2)]
    public void ValidationIgnoresOnlyAbsentUnityDirectoryProbes(string path, bool absentFromBoth, long expected)
    {
        var changes = new SourceChangeView("view", "snapshot", "epoch", 2, [new("Assets/source.cs", 1), new(path, 2)]);
        Assert.Equal(expected, WindowsValidationProvider.ValidationSequence(changes, _ => absentFromBoth));
    }

    [Theory]
    [InlineData("source")][InlineData("control")][InlineData("snapshot-owner")]
    [InlineData("application")][InlineData("managed-paths")][InlineData("cache")]
    public void PersistedGenerationCannotRedirectOwnedResources(string corruption)
    {
        using var dir = new TestDirectory(); var store = new WorkspaceStore(Path.Combine(dir.Path, "home"));
        string project = Path.Combine(dir.Path, "project");
        var provider = new WindowsValidationProvider(store, project);
        string workerId = new('a', 24), generation = new('b', 24), viewId = "view";
        var owner = new ValidationOwner("owner", 1); var workerOwner = new ValidationOwner("worker", 1);
        var snapshot = new SnapshotReference("snapshot", "fake", project, "immutable", DateTimeOffset.UtcNow, true, true, "fixture");
        var worker = new WorkerBinding(1, provider.ProjectId, workerId, workerOwner, "compatibility", new string('c', 40));
        string cacheId = WorkspaceStore.Key(provider.ProjectId + ":cache:" + viewId);
        var cache = new ValidationCacheIdentity(viewId, cacheId, snapshot with { Id = cacheId, Provider = "validation-empty-cache", RootPath = provider.CachePath(cacheId) },
            worker.CompatibilityKey, "unity-top-level-v1", SnapshotWorkspaceState.DefaultJournalBytes);
        var view = new ViewBinding(1, provider.ProjectId, owner, cache, Path.Combine(store.Root, "validation-mounts", WorkspaceStore.Key(provider.ProjectId + ":" + viewId)));
        ProductState.Save(provider.WorkerPath(workerId), worker); ProductState.Save(provider.ViewPath(viewId), view);
        var seal = new SnapshotDeltaSeal(1, 10, new string('d', 64));
        var publication = new CandidatePublicationReceipt(new("candidate", provider.ProjectId, "candidate", new(workerId, workerOwner, snapshot, seal), "source-only-v1"), seal, "candidate");
        var selection = new ValidationSelectionReceipt(new(generation, provider.ProjectId, generation, owner, cache, publication), generation);
        string control = provider.GenerationPath(generation);
        var identity = new OverlayIdentity(1, generation, snapshot, cache.MaxJournalBytes, 1024 * 1024, [".git"]);
        var cacheIdentity = new OverlayIdentity(1, cacheId, cache.Snapshot, cache.MaxJournalBytes, 1024 * 1024, WindowsValidationProvider.CacheRoots);
        var request = new OverlayServeRequest(identity, Path.Combine(control, "source"), view.MountPath, Path.Combine(control, "overlay"), "token",
            new(snapshot.Id, snapshot.RootPath, "proof"), Path.Combine(store.ControlPath(workerId), "snapshot"), workerId,
            new(provider.CandidatePath("candidate"), provider.CachePath(cacheId), WindowsValidationProvider.CacheRoots, cacheIdentity));
        var workspace = new WorkspaceRecord { Id = generation, RequestKey = generation, SourceReference = "validation-source", BaseReference = worker.BaseRevision,
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
        if (corruption == "cache") g = g with { Request = request with { ValidationView = request.ValidationView! with { CacheDeltaPath = Path.Combine(dir.Path, "unrelated") } } };
        ProductState.Save(path, g); byte[] before = File.ReadAllBytes(path);
        Assert.Equal("ValidationReceiptMismatch", Assert.Throws<WorkspaceException>(() => provider.Generation(generation)).Code);
        Assert.Equal(before, File.ReadAllBytes(path)); Assert.False(Directory.Exists(Path.Combine(dir.Path, "unrelated")));
    }
}
