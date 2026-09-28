using System.Security.Cryptography;
using System.Text.Json;
using Splinter.Application.Unity;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;
using Splinter.SourceControl.Git;
using static Splinter.Workflows.Windows.ProductState;

namespace Splinter.Workflows.Windows;

// The product composition owns concrete resources; Core/Runtime remain portable.
// Its facade serializes product operations, and publication also owns the worker
// operation lock used by workspace ensure/release. Journal readers fence direct writers.
internal sealed partial class WindowsValidationProvider(WorkspaceStore store, string project) : IValidationWorkflowProvider
{
    internal static readonly string[] CacheRoots = ["Library", "Temp", "Logs", "UserSettings", "obj", ".vs", ".idea"];
    internal string ProjectId => WorkspaceStore.ProjectKey(project);
    internal string Root => Path.Combine(store.Root, "validation-product", ProjectId);
    internal string Runner => Path.Combine(store.Root, "runners");
    internal UnityWorkspaceProvider Unity => new(new WindowsProcessWorkspaceProvider(Runner));
    internal string[] Managed => [".git", .. Unity.ManagedPaths];
    internal string WorkerPath(string id) => Path.Combine(Root, "workers", WorkspaceStore.Key(id) + ".json");
    internal string ViewPath(string id) => Path.Combine(Root, "views", WorkspaceStore.Key(id), "view.json");
    internal string GenerationPath(string id) => Path.Combine(Root, "generations", WorkspaceStore.Key(id));
    internal string CandidatePath(string id) => Path.Combine(Root, "candidates", WorkspaceStore.Key(id));
    internal string CachePath(string id) => Path.Combine(Root, "caches", WorkspaceStore.Key(id));
    internal string ActivePath(string id) => Path.Combine(Path.GetDirectoryName(ViewPath(id))!, "active.json");
    internal WorkerBinding Worker(string id)
    {
        var binding = Required<WorkerBinding>(WorkerPath(id));
        Require(binding.ProjectId == ProjectId && binding.WorkspaceId == id, "ValidationReceiptMismatch", "Worker binding identity differs from its location.");
        return binding;
    }
    internal ViewBinding View(string id)
    {
        var binding = Required<ViewBinding>(ViewPath(id));
        string cache = WorkspaceStore.Key(ProjectId + ":cache:" + id);
        Require(binding.ProjectId == ProjectId && binding.Cache.ViewId == id && binding.Cache.CacheId == cache &&
            binding.Cache.Snapshot.Provider == "validation-empty-cache" &&
            WorkspaceStore.SamePath(binding.Cache.Snapshot.RootPath, CachePath(cache)) &&
            WorkspaceStore.SamePath(binding.MountPath, Path.Combine(store.Root, "validation-mounts", WorkspaceStore.Key(ProjectId + ":" + id))),
            "ValidationReceiptMismatch", "View/cache binding paths or identity differ from their owned location.");
        return binding;
    }
    internal ViewGeneration Generation(string id)
    {
        var g = Required<ViewGeneration>(Path.Combine(GenerationPath(id), "generation.json"));
        var intent = g.Selection.Intent; var view = View(intent.Cache.ViewId); var worker = Worker(g.WorkerId);
        Require(intent.ProjectId == ProjectId && intent.GenerationId == id && g.Selection.ReceiptId == id &&
            intent.Cache == view.Cache && intent.Owner == view.Owner && intent.Candidate.Intent.Worker.WorkspaceId == worker.WorkspaceId &&
            g.BaseRevision == worker.BaseRevision && g.Workspace.Id == id && g.Workspace.StorageMode == "ntfs-snapshot" &&
            g.Workspace.ManagedPaths.SequenceEqual(Managed) && g.Request.Identity.ExcludedTopLevel.SequenceEqual([".git"]) &&
            g.Request.Identity.MaxJournalBytes == view.Cache.MaxJournalBytes &&
            WorkspaceStore.SamePath(g.Workspace.ProjectPath, project) && WorkspaceStore.SamePath(g.Workspace.Path, view.MountPath) &&
            g.Request.Identity.Id == id && g.Request.Identity.Snapshot == intent.Candidate.Intent.Worker.Snapshot &&
            g.Workspace.Snapshot?.Reference == g.Request.Identity.Snapshot &&
            WorkspaceStore.SamePath(g.Request.DeltaPath, Path.Combine(GenerationPath(id), "source")) &&
            WorkspaceStore.SamePath(g.Request.ControlPath, Path.Combine(GenerationPath(id), "overlay")) &&
            WorkspaceStore.SamePath(g.Request.MountPath, view.MountPath) &&
            g.Request.SnapshotOwnerId == worker.WorkspaceId &&
            WorkspaceStore.SamePath(g.Request.SnapshotReceiptDirectory, Path.Combine(store.ControlPath(worker.WorkspaceId), "snapshot")) &&
            g.Request.ValidationView is { } selected &&
            selected.CacheTopLevel.SequenceEqual(CacheRoots) && selected.CacheIdentity is { } cacheIdentity &&
            cacheIdentity.Id == view.Cache.CacheId && cacheIdentity.Snapshot == view.Cache.Snapshot &&
            cacheIdentity.MaxJournalBytes == view.Cache.MaxJournalBytes && cacheIdentity.ExcludedTopLevel.SequenceEqual(CacheRoots) &&
            WorkspaceStore.SamePath(selected.CandidatePath, CandidatePath(intent.Candidate.Intent.CandidateId)) &&
            WorkspaceStore.SamePath(selected.CacheDeltaPath, CachePath(view.Cache.CacheId)),
            "ValidationReceiptMismatch", "Generation state cannot redirect owned processes, source, snapshot or cache paths.");
        return g;
    }
    internal WorkspaceRecord Workspace(string id)
    {
        var w = store.Get(id);
        Require(WorkspaceStore.SamePath(w.ProjectPath, project) && w.StorageMode == "ntfs-snapshot" && w.Snapshot?.Reference is not null,
            "ValidationWorkerMismatch", "Use this project's existing NTFS snapshot worker.");
        return w;
    }
    internal string Delta(WorkspaceRecord w) => Path.Combine(store.ControlPath(w.Id), "delta");
    internal static OverlayIdentity Identity(WorkspaceRecord w) => new(1, w.Id, w.Snapshot!.Reference!, w.Snapshot.MaxJournalBytes, 1024 * 1024, [".git"]);
    internal VssSnapshotProvider SnapshotProvider(string worker) => new(Path.Combine(store.ControlPath(worker), "snapshot"), worker);
    internal async Task<VssSnapshotTree> Basis(string worker, SnapshotReference snapshot, CancellationToken ct)
    {
        var access = await SnapshotProvider(worker).ResolveReadAccessAsync(snapshot, null, ct);
        var health = await SnapshotProvider(worker).ObserveAsync(snapshot, ct);
        Require(health.Available == true, "ValidationSnapshotUnavailable", "Verify this exact VSS identity before reading source.");
        return new(snapshot, access, () => health);
    }
    internal static long ValidationSequence(SourceChangeView changes, Func<string, bool> absentFromCandidateAndCurrent)
    {
        // Unity's directory monitor creates and deletes GUID probes during each
        // refresh. Ignore only exact probe names absent from BOTH immutable input
        // and current output; a retained/deleted user file can never disappear.
        return changes.Changes.Where(change => !System.Text.RegularExpressions.Regex.IsMatch(change.Path,
                "^(Assets|ProjectSettings)/~UnityDirMonSyncFile~[0-9a-fA-F]{32}~$", System.Text.RegularExpressions.RegexOptions.CultureInvariant) ||
                !absentFromCandidateAndCurrent(change.Path))
            .Select(change => change.Sequence).DefaultIfEmpty(0).Max();
    }
    internal async Task<long> ObserveValidationSequence(ViewGeneration g, CancellationToken ct)
    {
        var changes = await new WindowsSourceChanges(_ => GenerationPath(g.Selection.Intent.GenerationId)).ObserveChangesAsync(g.Workspace, ct);
        using var basis = await Basis(g.WorkerId, g.Request.Identity.Snapshot, ct);
        using var candidate = new CandidateSnapshotTree(g.Request.ValidationView!.CandidatePath, basis);
        return ValidationSequence(changes, path =>
        {
            if (candidate.Stat(path) is not null) return false;
            try { _ = File.GetAttributes(Path.Combine(g.Workspace.Path, path.Replace('/', Path.DirectorySeparatorChar))); return false; }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException) { return true; }
        });
    }
    public Task VerifyOwnerAsync(string projectId, string resourceId, ValidationOwner owner, CancellationToken ct)
    {
        Require(projectId == ProjectId, "ValidationOwnerMismatch", "Project identity mismatch.");
        var worker = Read<WorkerBinding>(WorkerPath(resourceId)); var view = Read<ViewBinding>(ViewPath(resourceId));
        Require(worker is null != (view is null), "ValidationOwnerMismatch", "Exactly one bound resource must identify this owner.");
        Require((worker?.Owner ?? view!.Owner) == owner && (worker?.ProjectId ?? view!.ProjectId) == ProjectId,
            "ValidationOwnerMismatch", "The resource belongs to another project or owner generation.");
        return Task.CompletedTask;
    }
    public async Task<ValidationWorkerObservation> ObserveWorkerAsync(string projectId, string workspaceId, CancellationToken ct)
    {
        var binding = Worker(workspaceId); await VerifyOwnerAsync(projectId, workspaceId, binding.Owner, ct);
        var w = Workspace(workspaceId);
        var app = await Unity.ObserveAsync(w, store.ControlPath(w.Id), ct);
        var observed = await new NtfsSnapshotWorkspaceBackend(Runner).ObserveAsync(w, store.ControlPath(w.Id), ct);
        var resources = Resources(app, observed);
        Quiescent(resources);
        using var basis = await Basis(w.Id, w.Snapshot!.Reference!, ct);
        using var delta = JournalDeltaStore.OpenReadOnly(Delta(w), Identity(w), basis);
        return new(new(w.Id, binding.Owner, basis.Snapshot, delta.Seal()), resources, observed.SnapshotAvailable, "source-only-v1");
    }
    public async Task<CandidatePublicationReceipt?> FindCandidateAsync(CandidatePublicationIntent intent, CancellationToken ct)
    {
        string destination = CandidatePath(intent.CandidateId);
        if (!Directory.Exists(destination) && !File.Exists(destination)) return null;
        var frozen = Read<CandidateExportIntent>(Path.Combine(destination, "candidate-export.json"), true) ??
            Required<CandidateExportIntent>(Path.Combine(destination, "candidate-export.json.new"), true);
        var workspace = Workspace(intent.Worker.WorkspaceId);
        Require(frozen.Request.RequestKey == intent.CandidateId && frozen.SourceSeal == intent.Worker.SourceSeal &&
            frozen.Identity.Id == intent.Worker.WorkspaceId && frozen.Identity.Snapshot == intent.Worker.Snapshot &&
            WorkspaceStore.SamePath(frozen.SourceDirectory, Delta(workspace)) && WorkspaceStore.SamePath(frozen.Destination, destination) &&
            JsonSerializer.Serialize(frozen.Identity) == JsonSerializer.Serialize(Identity(workspace)),
            "ValidationReceiptMismatch", "Publication must reconcile its original frozen worker.");
        using var basis = await Basis(intent.Worker.WorkspaceId, intent.Worker.Snapshot, ct);
        var candidate = JournalDeltaStore.PublishCandidate(frozen.SourceDirectory, frozen.Identity, basis, destination,
            new(1, intent.CandidateId), CacheRoots, frozen.SourceProjection);
        return Receipt(intent, candidate);
    }
    public async Task<CandidatePublicationReceipt> PublishAsync(CandidatePublicationIntent intent, CancellationToken ct)
    {
        var observed = await ObserveWorkerAsync(intent.ProjectId, intent.Worker.WorkspaceId, ct);
        Require(observed.Worker == intent.Worker, "ValidationSourceChanged", "The publication source changed.");
        var w = Workspace(intent.Worker.WorkspaceId); var identity = Identity(w);
        using var basis = await Basis(w.Id, intent.Worker.Snapshot, ct);
        using var fence = JournalDeltaStore.OpenReadOnly(Delta(w), identity, basis);
        Require(fence.Seal() == intent.Worker.SourceSeal, "ValidationSourceChanged", "Source changed before its writer fence.");
        var git = await GitOverlayRegistration.ReadAsync(Path.Combine(store.ControlPath(w.Id), "git-overlay.json"), ct);
        var projection = await GitOverlayRegistration.QualifyCandidateSourceAsync(git, identity, ct);
        var candidate = JournalDeltaStore.PublishCandidate(Delta(w), identity, basis, CandidatePath(intent.CandidateId),
            new(1, intent.CandidateId), CacheRoots, projection);
        Require(fence.Seal() == intent.Worker.SourceSeal, "ValidationSourceChanged", "Frozen source changed during publication.");
        return Receipt(intent, candidate);
    }
    private static CandidatePublicationReceipt Receipt(CandidatePublicationIntent intent, ValidationCandidate candidate)
    {
        Require(candidate.Id == intent.CandidateId && candidate.SourceSeal == intent.Worker.SourceSeal,
            "ValidationReceiptMismatch", "Candidate receipt does not match the frozen publication.");
        return new(intent, candidate.Seal, candidate.Id);
    }
    internal static ValidationResources Resources(ApplicationState app, SnapshotWorkspaceObservation observed) =>
        new(observed.OwnershipVerified && app.Resources?.OwnershipVerified == true &&
            (app.Running || app.Resources is { WorkingSetBytes: 0, PrivateBytes: 0 }) &&
            (observed.ProviderAlive || observed is { WorkingSetBytes: 0, PrivateBytes: 0 }),
            app.Resources?.ActiveProcesses, observed.ProviderAlive ? 1 : 0, observed.OpenHandles, observed.Mounted);
    internal static void Quiescent(ValidationResources r) => Require(r is { OwnershipVerified: true, Applications: 0, Writers: 0, OpenHandles: 0, Mounted: false },
        "ValidationResourcesBusy", "Verify zero owned applications, writers, handles and mounts; unknown is not zero.");
    public async Task<ValidationViewObservation> ObserveViewAsync(string projectId, string viewId, CancellationToken ct)
    {
        var view = View(viewId); await VerifyOwnerAsync(projectId, viewId, view.Owner, ct);
        var active = Read<ActiveGeneration>(ActivePath(viewId));
        if (active is null)
        {
            Require(!Directory.Exists(view.MountPath) && !File.Exists(view.MountPath), "ValidationResourcesBusy", "An unowned view path exists.");
            return new(view.Owner, new(true, 0, 0, 0, false));
        }
        var generation = Generation(active.GenerationId);
        var (app, observed) = await ObserveGeneration(generation, ct);
        return new(view.Owner, Resources(app, observed));
    }
    internal async Task<(ApplicationState App, SnapshotWorkspaceObservation Mount)> ObserveGeneration(ViewGeneration g, CancellationToken ct)
    {
        var app = await Unity.ObserveAsync(g.Workspace, GenerationPath(g.Selection.Intent.GenerationId), ct);
        var observed = await WinFspOverlayClient.ObserveAsync(g.Request, ct);
        bool alive = observed.GetProperty("processAlive").GetBoolean(), mounted = Directory.Exists(g.Workspace.Path);
        bool owned = observed.GetProperty("ownershipVerified").GetBoolean();
        int? handles = alive ? observed.TryGetProperty("state", out var state) && state.TryGetProperty("openHandles", out var count) ? count.GetInt32() : null : owned && !mounted ? 0 : null;
        var health = await SnapshotProvider(g.WorkerId).ObserveAsync(g.Request.Identity.Snapshot, ct);
        // Actual lease observation supplies provider memory when it is alive.
        OverlayMount? current = g.Workspace.Snapshot!.Mount;
        if (alive && observed.TryGetProperty("state", out var liveState))
            current = new(g.Request.Identity.Id, g.Request.MountPath, g.Request.DeltaPath, g.Request.Identity.Snapshot,
                liveState.GetProperty("processId").GetInt32(), liveState.GetProperty("processStartTimeUtcTicks").GetInt64());
        OverlayMountObservation? live = current is { } lease ? await MountProvider(g).ObserveAsync(lease, ct) : null;
        if (live is { Mounted: true, OwnershipVerified: true })
        {
            g.Workspace.Snapshot.Mount = current;
            g.Workspace.Snapshot.Observation = new(health.Available, true, alive, owned, live.OpenHandles, live.WorkingSetBytes, live.PrivateBytes);
        }
        return (app, new(health.Available, mounted, alive, owned, handles,
            live?.WorkingSetBytes ?? (owned && !alive && !mounted ? 0 : null),
            live?.PrivateBytes ?? (owned && !alive && !mounted ? 0 : null)));
    }
    internal WinFspOverlayMountProvider MountProvider(ViewGeneration g) => new(Path.Combine(g.Request.ControlPath, "request.json"), Runner);
    public async Task<ValidationSelectionReceipt?> FindSelectionAsync(ValidationSelectionIntent intent, CancellationToken ct)
    {
        var g = Read<ViewGeneration>(Path.Combine(GenerationPath(intent.GenerationId), "generation.json"));
        if (g is null) return null;
        Require(g.Selection.Intent == intent, "ValidationReceiptMismatch", "Source generation belongs to another selection.");
        // Only an explicit pending select/resume reaches here. Never replace its source.
        Quiescent((await ObserveViewAsync(intent.ProjectId, intent.Cache.ViewId, ct)).Resources);
        using var tree = await OpenArtifact(new("source-generation", intent.GenerationId), ct);
        Save(ActivePath(intent.Cache.ViewId), new ActiveGeneration(1, intent.GenerationId));
        return g.Selection;
    }
    public async Task<ValidationSelectionReceipt> SelectAsync(ValidationSelectionIntent intent, CancellationToken ct)
    {
        var view = View(intent.Cache.ViewId);
        Require(!view.Closed && view.Cache == intent.Cache && view.Owner == intent.Owner, "ValidationCacheMismatch", "Keep the bound open view and cache identity.");
        var worker = Worker(intent.Candidate.Intent.Worker.WorkspaceId);
        Require(worker.CompatibilityKey == view.Cache.CompatibilityKey, "ValidationCacheMismatch", "The Editor/bridge compatibility key differs.");
        Quiescent((await ObserveViewAsync(intent.ProjectId, intent.Cache.ViewId, ct)).Resources);
        using var basis = await Basis(worker.WorkspaceId, intent.Candidate.Intent.Worker.Snapshot, ct);
        var access = await SnapshotProvider(worker.WorkspaceId).ResolveReadAccessAsync(basis.Snapshot, null, ct);
        string control = GenerationPath(intent.GenerationId), delta = Path.Combine(control, "source");
        var identity = new OverlayIdentity(1, intent.GenerationId, basis.Snapshot, intent.Cache.MaxJournalBytes, 1024 * 1024, [".git"]);
        var cacheIdentity = new OverlayIdentity(1, intent.Cache.CacheId, intent.Cache.Snapshot, intent.Cache.MaxJournalBytes, 1024 * 1024, CacheRoots);
        var selection = new ValidationViewSelection(CandidatePath(intent.Candidate.Intent.CandidateId), CachePath(intent.Cache.CacheId), CacheRoots, cacheIdentity);
        using (var initialize = new ValidationViewDeltaStore(delta, identity, basis, selection)) { initialize.Flush(); }
        var request = new OverlayServeRequest(identity, delta, view.MountPath, Path.Combine(control, "overlay"),
            Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)), access,
            Path.Combine(store.ControlPath(worker.WorkspaceId), "snapshot"), worker.WorkspaceId, selection);
        var receipt = new ValidationSelectionReceipt(intent, intent.GenerationId);
        var w = new WorkspaceRecord { Id = intent.GenerationId, RequestKey = intent.OperationId, ProjectPath = project,
            Path = view.MountPath, SourceReference = "validation-source", BaseReference = worker.BaseRevision, BaseRevision = worker.BaseRevision,
            StorageMode = "ntfs-snapshot", Purpose = "validation", ManagedPaths = Managed, Snapshot = new() { Reference = basis.Snapshot, MaxJournalBytes = intent.Cache.MaxJournalBytes } };
        Save(Path.Combine(control, "generation.json"), new ViewGeneration(1, receipt, worker.WorkspaceId, worker.BaseRevision, w, request));
        Save(Path.Combine(request.ControlPath, "request.json"), request);
        Save(ActivePath(intent.Cache.ViewId), new ActiveGeneration(1, intent.GenerationId));
        return receipt;
    }
}
