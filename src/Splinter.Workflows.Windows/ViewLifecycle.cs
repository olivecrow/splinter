using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;
using Splinter.SourceControl.Git;
using static Splinter.Workflows.Windows.ProductState;

namespace Splinter.Workflows.Windows;

public sealed class ViewLifecycle(WorkspaceStore store)
{
    public async Task<object> ExecuteAsync(ViewLifecycleRequest input, CancellationToken ct = default)
    {
        Require(input.Version == 1 && input.Owner.Generation > 0 && Name(input.Owner.Id) && Name(input.RequestKey),
            "ViewRequestInvalid", "Use current version and bounded owner/request identities.");
        Require(input.Action is "prepare-worker" or "publish" or "select" or "resume" or "preserve" or "release" or
            "status" or "start" or "stop" or "close-view" or "compact-cache",
            "ViewRequestInvalid", "Use a supported storage view action; project validation belongs to external tools.");
        string project = WorkspaceStore.CanonicalProject(input.Project);
        var provider = new WindowsViewProvider(store, project);
        using var operation = await store.LockAsync(WorkspaceStore.Key("validation-product:" + provider.ProjectId), ct);
        var workflow = new ViewWorkflowCoordinator(new(store), provider);
        if (input.Action != "status")
            provider.RequireNoPendingCacheMaintenance(input.Action == "compact-cache" ? input.RequestKey : null);
        string requestPath = Path.Combine(provider.Root, "requests", WorkspaceStore.Key(input.RequestKey) + ".json");
        var prior = Read<ViewLifecycleRequest>(requestPath);
        Require(prior is null || prior == input, "ViewRequestKeyConflict", "Retry the same product request payload.");
        if (prior is null) Save(requestPath, input);
        ViewWorkflowRequest Request(string op) => new(1, op, provider.ProjectId, input.RequestKey, input.Owner);
        switch (input.Action)
        {
            case "prepare-worker":
            case "publish":
            {
                Require(input.WorkspaceId is not null, "ViewRequestInvalid", "workspaceId is required.");
                using var workerGate = await store.LockAsync(input.WorkspaceId!, ct);
                var w = provider.Workspace(input.WorkspaceId!);
                var binding = Read<WorkerBinding>(provider.WorkerPath(w.Id));
                if (binding is null)
                {
                    Require(w.Phase == "ready" && w.Snapshot!.Attached && w.Snapshot.Observation is { Mounted: true },
                        "ViewWorkerMismatch", "Bind a prepared, attached worker before publication.");
                    binding = new(1, provider.ProjectId, w.Id, input.Owner, provider.Unity.CompatibilityKey(w.Path), w.BaseRevision!);
                    Require(Read<ViewBinding>(provider.ViewPath(w.Id)) is null, "ViewOwnerMismatch", "Resource identity is already a view.");
                    Save(provider.WorkerPath(w.Id), binding);
                }
                await provider.VerifyOwnerAsync(provider.ProjectId, w.Id, input.Owner, ct);
                if (input.Action == "prepare-worker")
                {
                    Require(w.Phase is not ("released" or "releasing" or "reclaim-pending") && !w.Snapshot!.Step.StartsWith("release-", StringComparison.Ordinal),
                        "ViewWorkerMismatch", "A releasing worker cannot be prepared.");
                    var backend = new NtfsSnapshotWorkspaceBackend(provider.Runner); string control = store.ControlPath(w.Id);
                    await provider.Unity.StopAsync(w, control, ct);
                    var observed = await backend.ObserveAsync(w, control, ct);
                    if (observed.Mounted)
                    {
                        await provider.Unity.RemoveInfrastructureAsync(w, control, ct);
                        var seal = await backend.SealAsync(w, control, ct);
                        await backend.UnmountAsync(w, control, ct);
                        Require(await backend.SealAsync(w, control, ct) == seal, "ViewSourceChanged", "Source changed while unmounting.");
                    }
                    w.Snapshot!.Observation = await backend.ObserveAsync(w, control, ct);
                    store.Save(w, "view-worker-prepared");
                    return await provider.ObserveWorkerAsync(provider.ProjectId, w.Id, ct);
                }
                var state = await workflow.RecoverForMutationAsync(provider.ProjectId, ct);
                var frozen = state.Operations.GetValueOrDefault(input.RequestKey)?.Request.Worker;
                var worker = frozen ?? (await provider.ObserveWorkerAsync(provider.ProjectId, w.Id, ct)).Worker;
                Require(worker.WorkspaceId == w.Id && worker.Owner == input.Owner, "ViewOwnerMismatch", "Publication worker differs.");
                return await workflow.ExecuteAsync(Request("publish") with { Worker = worker }, ct);
            }
            case "select":
            {
                Require(Name(input.ViewId) && Name(input.CandidateId), "ViewRequestInvalid", "viewId and candidateId are required.");
                var state = await workflow.RecoverForMutationAsync(provider.ProjectId, ct);
                Require(state.Candidates.TryGetValue(input.CandidateId!, out var candidate) && !candidate.Released,
                    "ViewCandidateNotFound", "Select a retained candidate.");
                var worker = provider.Worker(candidate!.Publication.Intent.Worker.WorkspaceId);
                var view = Read<ViewBinding>(provider.ViewPath(input.ViewId!));
                if (view is null)
                {
                    Require(Read<WorkerBinding>(provider.WorkerPath(input.ViewId!)) is null, "ViewOwnerMismatch", "Resource identity is already a worker.");
                    string cacheId = WorkspaceStore.Key(provider.ProjectId + ":cache:" + input.ViewId);
                    var cache = new ViewCacheIdentity(input.ViewId!, cacheId, candidate.Publication.Intent.Worker.Snapshot,
                        worker.CompatibilityKey, "unity-top-level-v1", input.MaxJournalBytes, SnapshotBacked: true);
                    view = new(1, provider.ProjectId, input.Owner, cache,
                        Path.Combine(store.Root, "validation-mounts", WorkspaceStore.Key(provider.ProjectId + ":" + input.ViewId)), worker.WorkspaceId);
                    Save(provider.ViewPath(input.ViewId!), view);
                }
                Require(view.Owner == input.Owner && !view.Closed && view.Cache.MaxJournalBytes == input.MaxJournalBytes,
                    "ViewCacheMismatch", "Retain the view owner and cache budget; closed views cannot be reused.");
                return await workflow.ExecuteAsync(Request("select") with { CandidateId = input.CandidateId, Cache = view.Cache }, ct);
            }
            case "resume":
                return await workflow.ExecuteAsync(Request(input.Action) with { GenerationId = input.GenerationId }, ct);
            case "preserve":
            case "release":
                return await workflow.ExecuteAsync(Request(input.Action) with { Target = input.Target }, ct);
            case "status":
            {
                var state = await workflow.InspectAsync(provider.ProjectId, ct);
                object? resources = input.ViewId is null ? null : await provider.ObserveViewAsync(provider.ProjectId, input.ViewId, ct);
                object? execution = null;
                if (input.ViewId is { } viewId && Read<ActiveGeneration>(provider.ActivePath(viewId)) is { } active)
                {
                    var generation = provider.Generation(active.GenerationId);
                    var observed = await provider.ObserveGeneration(generation, ct);
                    execution = new { generationId = active.GenerationId, application = observed.App, mount = observed.Mount,
                        controlPath = provider.GenerationPath(active.GenerationId), path = generation.Workspace.Path };
                }
                var maintenance = input.ViewId is null ? null : Read<CacheMaintenance>(provider.MaintenancePath(input.ViewId));
                return new { state, resources, execution, cacheMaintenance = maintenance };
            }
            case "start":
            case "stop":
                return await ControlAsync(input, provider, workflow, ct);
            case "close-view":
            {
                Require(Name(input.ViewId), "ViewRequestInvalid", "viewId is required.");
                var view = provider.View(input.ViewId!);
                await provider.VerifyOwnerAsync(provider.ProjectId, input.ViewId!, input.Owner, ct);
                await workflow.ExecuteAsync(Request("close-view") with { Cache = view.Cache }, ct);
                string cache = provider.CachePath(view.Cache.CacheId);
                return new { viewId = input.ViewId, closed = true, cachePresent = Directory.Exists(cache), mountPresent = Directory.Exists(view.MountPath) };
            }
            case "compact-cache":
            {
                Require(Name(input.ViewId) && input.MaxEstimatedJournalOverlapBytes is > 0,
                    "ViewRequestInvalid", "viewId and a positive maxEstimatedJournalOverlapBytes are required.");
                await workflow.RecoverForMutationAsync(provider.ProjectId, ct);
                return await provider.CompactCacheAsync(input.ViewId!, input.Owner, input.RequestKey, input.MaxEstimatedJournalOverlapBytes!.Value, ct);
            }
            default: throw new WorkspaceException("ViewRequestInvalid", "Unknown view action.");
        }
    }

    private static async Task<object> ControlAsync(ViewLifecycleRequest input, WindowsViewProvider provider,
        ViewWorkflowCoordinator workflow, CancellationToken ct)
    {
        Require(Name(input.GenerationId), "ViewRequestInvalid", "generationId is required.");
        var state = await workflow.InspectAsync(provider.ProjectId, ct);
        Require(state.Generations.TryGetValue(input.GenerationId!, out var retained) && !retained.Released && retained.FrozenSeal is null,
            "ViewGenerationFrozen", "Only a retained, unfrozen generation can run.");
        var selection = retained!.Selection;
        await provider.VerifyOwnerAsync(provider.ProjectId, selection.Intent.Cache.ViewId, input.Owner, ct);
        Require(selection.Intent.Owner == input.Owner && state.Views[selection.Intent.Cache.ViewId].CurrentGenerationId == input.GenerationId,
            "ViewGenerationMismatch", "Control only the current source generation.");
        var g = provider.Generation(input.GenerationId!); string control = provider.GenerationPath(input.GenerationId!);
        var (app, mount) = await provider.ObserveGeneration(g, ct);
        if (input.Action == "start")
        {
            provider.RequireCacheReady(selection.Intent.Cache.ViewId);
            if (app.Running)
            {
                Require(mount.Mounted && mount.OwnershipVerified, "ViewResourcesBusy", "The running Editor has no verified provider.");
                return new { selection, application = app, mount, controlPath = control, path = g.Workspace.Path };
            }
            Require(app.Resources is { OwnershipVerified: true, ActiveProcesses: 0 }, "ViewResourcesBusy", "Previous Editor processes must exit before restart.");
            if (!mount.ProviderAlive)
            {
                var consumed = await WinFspOverlayClient.ConsumedVerificationAsync(g.Request, ct);
                var access = await provider.SnapshotProvider(g.WorkerId).ResolveReadAccessAsync(g.Request.Identity.Snapshot, consumed, ct);
                var view = provider.View(selection.Intent.Cache.ViewId);
                var cacheConsumed = await WinFspOverlayClient.ConsumedCacheVerificationAsync(g.Request, ct);
                var cacheAccess = view.CacheWorkerId == g.WorkerId && view.Cache.Snapshot == g.Request.Identity.Snapshot ? access
                    : await provider.SnapshotProvider(view.CacheWorkerId).ResolveReadAccessAsync(view.Cache.Snapshot, cacheConsumed, ct);
                g = g with { Request = g.Request with { ReadAccess = access, CacheSnapshot = g.Request.CacheSnapshot! with { ReadAccess = cacheAccess } } };
                Save(Path.Combine(control, "generation.json"), g);
                Save(Path.Combine(g.Request.ControlPath, "request.json"), g.Request);
            }
            else Save(Path.Combine(g.Request.ControlPath, "request.json"), g.Request);
            string mountParent = Path.GetDirectoryName(g.Request.MountPath)!;
            WorkspaceStore.EnsurePlainPath(mountParent); Directory.CreateDirectory(mountParent);
            g.Workspace.Snapshot!.Mount = await provider.MountProvider(g).MountAsync(g.Request.Identity, g.Request.DeltaPath, g.Request.MountPath, ct);
            var live = await provider.MountProvider(g).ObserveAsync(g.Workspace.Snapshot.Mount, ct);
            Require(live is { Mounted: true, OwnershipVerified: true }, "ViewResourcesBusy", "View mount is not ready.");
            g.Workspace.Snapshot.Observation = new(true, true, true, true, live.OpenHandles, live.WorkingSetBytes, live.PrivateBytes);
            Save(Path.Combine(control, "generation.json"), g);
            g.Workspace.Application = await provider.Unity.StartAsync(g.Workspace, control, ct);
            Save(Path.Combine(control, "generation.json"), g);
            return new { selection, application = await provider.Unity.ObserveAsync(g.Workspace, control, ct), controlPath = control, path = g.Workspace.Path };
        }
        if (mount.Mounted)
        {
            await provider.Unity.StopAsync(g.Workspace, control, ct);
            await provider.Unity.RemoveInfrastructureAsync(g.Workspace, control, ct);
            await provider.MountProvider(g).FlushAsync(g.Workspace.Snapshot!.Mount!, ct);
            await provider.MountProvider(g).UnmountAsync(g.Workspace.Snapshot.Mount!, ct);
        }
        else await provider.Unity.StopAsync(g.Workspace, control, ct);
        WindowsViewProvider.Quiescent((await provider.ObserveViewAsync(provider.ProjectId, selection.Intent.Cache.ViewId, ct)).Resources);
        using var stopped = await provider.OpenArtifact(new("source-generation", input.GenerationId!), ct);
        return new { selection, stopped = true, seal = stopped.Seal, controlPath = control };
    }
    private static bool Name(string? value) => value is not null && Regex.IsMatch(value, "^[A-Za-z0-9][A-Za-z0-9._:-]{0,127}$", RegexOptions.CultureInvariant);
}
