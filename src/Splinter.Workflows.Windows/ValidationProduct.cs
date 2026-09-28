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

public sealed class ValidationProduct(WorkspaceStore store)
{
    public async Task<object> ExecuteAsync(ValidationProductRequest input, CancellationToken ct = default)
    {
        Require(input.Version == 1 && input.Owner.Generation > 0 && Name(input.Owner.Id) && Name(input.RequestKey),
            "ValidationRequestInvalid", "Use current version and bounded owner/request identities.");
        string project = WorkspaceStore.CanonicalProject(input.Project);
        var provider = new WindowsValidationProvider(store, project);
        using var operation = await store.LockAsync(WorkspaceStore.Key("validation-product:" + provider.ProjectId), ct);
        var workflow = new ValidationWorkflowCoordinator(new(store), provider);
        string requestPath = Path.Combine(provider.Root, "requests", WorkspaceStore.Key(input.RequestKey) + ".json");
        var prior = Read<ValidationProductRequest>(requestPath);
        Require(prior is null || prior == input, "ValidationRequestKeyConflict", "Retry the same product request payload.");
        if (prior is null) Save(requestPath, input);
        ValidationWorkflowRequest Request(string op) => new(1, op, provider.ProjectId, input.RequestKey, input.Owner);
        switch (input.Action)
        {
            case "prepare-worker":
            case "publish":
            case "preserve-worker":
            {
                Require(input.WorkspaceId is not null, "ValidationRequestInvalid", "workspaceId is required.");
                using var workerGate = await store.LockAsync(input.WorkspaceId!, ct);
                var w = provider.Workspace(input.WorkspaceId!);
                var binding = Read<WorkerBinding>(provider.WorkerPath(w.Id));
                if (binding is null)
                {
                    Require(w.Phase == "ready" && w.Snapshot!.Attached && w.Snapshot.Observation is { Mounted: true },
                        "ValidationWorkerMismatch", "Bind a prepared, attached worker before publication.");
                    binding = new(1, provider.ProjectId, w.Id, input.Owner, provider.Unity.CompatibilityKey(w.Path), w.BaseRevision!);
                    Require(Read<ViewBinding>(provider.ViewPath(w.Id)) is null, "ValidationOwnerMismatch", "Resource identity is already a view.");
                    Save(provider.WorkerPath(w.Id), binding);
                }
                await provider.VerifyOwnerAsync(provider.ProjectId, w.Id, input.Owner, ct);
                if (input.Action == "preserve-worker")
                    return await PreserveWorkerAsync(input, provider, w, ct);
                if (input.Action == "prepare-worker")
                {
                    Require(w.Phase is not ("released" or "releasing" or "reclaim-pending") && !w.Snapshot!.Step.StartsWith("release-", StringComparison.Ordinal),
                        "ValidationWorkerMismatch", "A releasing worker cannot be prepared.");
                    var backend = new NtfsSnapshotWorkspaceBackend(provider.Runner); string control = store.ControlPath(w.Id);
                    await provider.Unity.StopAsync(w, control, ct);
                    var observed = await backend.ObserveAsync(w, control, ct);
                    if (observed.Mounted)
                    {
                        await provider.Unity.RemoveInfrastructureAsync(w, control, ct);
                        var seal = await backend.SealAsync(w, control, ct);
                        await backend.UnmountAsync(w, control, ct);
                        Require(await backend.SealAsync(w, control, ct) == seal, "ValidationSourceChanged", "Source changed while unmounting.");
                    }
                    w.Snapshot!.Observation = await backend.ObserveAsync(w, control, ct);
                    w.PendingValidationId = null; w.PendingFingerprint = null; w.Validation = null;
                    store.Save(w, "validation-worker-prepared");
                    return await provider.ObserveWorkerAsync(provider.ProjectId, w.Id, ct);
                }
                var state = await workflow.RecoverForMutationAsync(provider.ProjectId, ct);
                var frozen = state.Operations.GetValueOrDefault(input.RequestKey)?.Request.Worker;
                var worker = frozen ?? (await provider.ObserveWorkerAsync(provider.ProjectId, w.Id, ct)).Worker;
                Require(worker.WorkspaceId == w.Id && worker.Owner == input.Owner, "ValidationOwnerMismatch", "Publication worker differs.");
                return await workflow.ExecuteAsync(Request("publish") with { Worker = worker }, ct);
            }
            case "select":
            {
                Require(Name(input.ViewId) && Name(input.CandidateId), "ValidationRequestInvalid", "viewId and candidateId are required.");
                var state = await workflow.RecoverForMutationAsync(provider.ProjectId, ct);
                Require(state.Candidates.TryGetValue(input.CandidateId!, out var candidate) && !candidate.Released,
                    "ValidationCandidateNotFound", "Select a retained candidate.");
                var worker = provider.Worker(candidate!.Publication.Intent.Worker.WorkspaceId);
                var view = Read<ViewBinding>(provider.ViewPath(input.ViewId!));
                if (view is null)
                {
                    Require(Read<WorkerBinding>(provider.WorkerPath(input.ViewId!)) is null, "ValidationOwnerMismatch", "Resource identity is already a worker.");
                    string cacheId = WorkspaceStore.Key(provider.ProjectId + ":cache:" + input.ViewId);
                    var label = candidate.Publication.Intent.Worker.Snapshot with { Id = cacheId, Provider = "validation-empty-cache",
                        SourcePath = project, RootPath = provider.CachePath(cacheId), Consistency = "empty-owned-cache" };
                    var cache = new ValidationCacheIdentity(input.ViewId!, cacheId, label, worker.CompatibilityKey, "unity-top-level-v1", input.MaxJournalBytes);
                    view = new(1, provider.ProjectId, input.Owner, cache, Path.Combine(store.Root, "validation-mounts", WorkspaceStore.Key(provider.ProjectId + ":" + input.ViewId)));
                    Save(provider.ViewPath(input.ViewId!), view);
                }
                Require(view.Owner == input.Owner && !view.Closed && view.Cache.MaxJournalBytes == input.MaxJournalBytes,
                    "ValidationCacheMismatch", "Retain the view owner and cache budget; closed views cannot be reused.");
                return await workflow.ExecuteAsync(Request("select") with { CandidateId = input.CandidateId, Cache = view.Cache }, ct);
            }
            case "resume":
            case "observe-validation":
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
                return new { state, resources, execution };
            }
            case "start":
            case "refresh":
            case "stop":
                return await ControlAsync(input, provider, workflow, ct);
            case "close-view":
            {
                Require(Name(input.ViewId), "ValidationRequestInvalid", "viewId is required.");
                var view = provider.View(input.ViewId!);
                await provider.VerifyOwnerAsync(provider.ProjectId, input.ViewId!, input.Owner, ct);
                WindowsValidationProvider.Quiescent((await provider.ObserveViewAsync(provider.ProjectId, input.ViewId!, ct)).Resources);
                var state = await workflow.InspectAsync(provider.ProjectId, ct);
                Require(!state.Generations.Values.Any(g => g.Selection.Intent.Cache.ViewId == input.ViewId && !g.Released) &&
                    !state.Operations.Values.Any(o => o.Response is null), "ValidationDependencyActive", "Preserve and release view generations and finish pending operations first.");
                Save(provider.ViewPath(input.ViewId!), view with { Closed = true });
                string cache = provider.CachePath(view.Cache.CacheId), cacheReceipt = Path.Combine(Path.GetDirectoryName(provider.ViewPath(input.ViewId!))!, "cache-reclamation.json");
                Require(!File.Exists(cache), "ValidationReleaseUncertain", "A file replaced the owned cache directory.");
                SnapshotDeltaSeal? seal = Read<JournalReclamationIntent>(cacheReceipt)?.Seal;
                if (File.Exists(Path.Combine(cache, "delta.journal")))
                {
                    var identity = new OverlayIdentity(1, view.Cache.CacheId, view.Cache.Snapshot, view.Cache.MaxJournalBytes, 1024 * 1024, WindowsValidationProvider.CacheRoots);
                    using (var check = ValidationViewDeltaStore.OpenCacheReadOnly(cache, identity)) seal = check.Seal();
                }
                Require(!Directory.Exists(cache) || seal is not null, "ValidationReleaseUncertain", "A remaining cache needs its verified reclamation seal.");
                if (seal is not null) JournalReclamation.Reclaim(cache, seal, ["identity.json"], cacheReceipt);
                return new { viewId = input.ViewId, closed = true, cachePresent = Directory.Exists(cache), mountPresent = Directory.Exists(view.MountPath) };
            }
            default: throw new WorkspaceException("ValidationRequestInvalid", "Unknown validation product action.");
        }
    }

    private async Task<object> PreserveWorkerAsync(ValidationProductRequest input, WindowsValidationProvider provider, WorkspaceRecord w, CancellationToken ct)
    {
        Require(w.Phase is not ("released" or "releasing" or "reclaim-pending") && !w.Snapshot!.Step.StartsWith("release-", StringComparison.Ordinal),
            "ValidationWorkerMismatch", "Do not change a releasing worker's result.");
        string control = store.ControlPath(w.Id);
        var backend = new NtfsSnapshotWorkspaceBackend(provider.Runner);
        await provider.Unity.StopAsync(w, control, ct);
        var app = await provider.Unity.ObserveAsync(w, control, ct);
        Require(app.Resources is { OwnershipVerified: true, ActiveProcesses: 0, WorkingSetBytes: 0, PrivateBytes: 0 },
            "ValidationResourcesBusy", "Preservation requires the owned Editor and all children to be stopped.");
        await backend.VerifyConnectionAsync(w, control, ct);
        w.Snapshot!.Mount = await backend.ConnectAsync(w, control, ct);
        w.Snapshot.Observation = await backend.ObserveAsync(w, control, ct);
        store.Save(w, "validation-worker-preservation-connected");
        await provider.Unity.RemoveInfrastructureAsync(w, control, ct);
        var before = await backend.SealAsync(w, control, ct);
        string intentPath = Path.Combine(provider.Root, "worker-preservation", WorkspaceStore.Key(input.RequestKey) + ".json");
        var intent = new WorkerPreservation(1, w.Id, input.Owner, before);
        var prior = Read<WorkerPreservation>(intentPath);
        Require(prior is null || prior == intent, "ValidationSourceChanged", "Retry only the same frozen worker preservation; later edits remain separate.");
        if (prior is null) Save(intentPath, intent);
        var git = new GitWorkspaceProvider(baselines: store, sourceChanges: new WindowsSourceChanges(store.ControlPath));
        w.ResultRevision = await git.CommitPreservedSourceAsync(w, "Preserve Splinter worker " + input.RequestKey, ct);
        string reference = await git.PreserveAsync(w, control, ct);
        var source = await git.InspectAsync(w, ct);
        var after = await backend.SealAsync(w, control, ct);
        Require(before == after, "ValidationSourceChanged", "Worker writes overlapped independent result collection; retain the journal.");
        w.Snapshot.Result = new(w.Snapshot.Reference!.Id, w.ResultRevision, source.Fingerprint, reference, after, true, SnapshotResult.StableJournalProtocol);
        w.Validation = null; w.PendingValidationId = null; w.PendingFingerprint = null;
        w.Phase = "collected"; w.LastError = null; store.Save(w, "validation-worker-preserved");
        return new { workspaceId = w.Id, result = w.Snapshot.Result, validationClaimed = false };
    }

    private static async Task<object> ControlAsync(ValidationProductRequest input, WindowsValidationProvider provider,
        ValidationWorkflowCoordinator workflow, CancellationToken ct)
    {
        Require(Name(input.GenerationId), "ValidationRequestInvalid", "generationId is required.");
        var state = await workflow.InspectAsync(provider.ProjectId, ct);
        Require(state.Generations.TryGetValue(input.GenerationId!, out var retained) && !retained.Released && retained.FrozenSeal is null,
            "ValidationGenerationFrozen", "Only a retained, unfrozen generation can run.");
        var selection = retained!.Selection;
        await provider.VerifyOwnerAsync(provider.ProjectId, selection.Intent.Cache.ViewId, input.Owner, ct);
        Require(selection.Intent.Owner == input.Owner && state.Views[selection.Intent.Cache.ViewId].CurrentGenerationId == input.GenerationId,
            "ValidationGenerationMismatch", "Control only the current source generation.");
        var g = provider.Generation(input.GenerationId!); string control = provider.GenerationPath(input.GenerationId!);
        var (app, mount) = await provider.ObserveGeneration(g, ct);
        if (input.Action == "start")
        {
            if (app.Running)
            {
                Require(mount.Mounted && mount.OwnershipVerified, "ValidationResourcesBusy", "The running Editor has no verified provider.");
                return new { selection, application = app, mount, controlPath = control, path = g.Workspace.Path };
            }
            Require(app.Resources is { OwnershipVerified: true, ActiveProcesses: 0 }, "ValidationResourcesBusy", "Previous Editor processes must exit before restart.");
            if (!mount.ProviderAlive)
            {
                var consumed = await WinFspOverlayClient.ConsumedVerificationAsync(g.Request, ct);
                var access = await provider.SnapshotProvider(g.WorkerId).ResolveReadAccessAsync(g.Request.Identity.Snapshot, consumed, ct);
                g = g with { Request = g.Request with { ReadAccess = access } };
                Save(Path.Combine(control, "generation.json"), g);
                Save(Path.Combine(g.Request.ControlPath, "request.json"), g.Request);
            }
            else Save(Path.Combine(g.Request.ControlPath, "request.json"), g.Request);
            string mountParent = Path.GetDirectoryName(g.Request.MountPath)!;
            WorkspaceStore.EnsurePlainPath(mountParent); Directory.CreateDirectory(mountParent);
            g.Workspace.Snapshot!.Mount = await provider.MountProvider(g).MountAsync(g.Request.Identity, g.Request.DeltaPath, g.Request.MountPath, ct);
            var live = await provider.MountProvider(g).ObserveAsync(g.Workspace.Snapshot.Mount, ct);
            Require(live is { Mounted: true, OwnershipVerified: true }, "ValidationResourcesBusy", "View mount is not ready.");
            g.Workspace.Snapshot.Observation = new(true, true, true, true, live.OpenHandles, live.WorkingSetBytes, live.PrivateBytes);
            Save(Path.Combine(control, "generation.json"), g);
            g.Workspace.Application = await provider.Unity.StartAsync(g.Workspace, control, ct);
            Save(Path.Combine(control, "generation.json"), g);
            return new { selection, application = await provider.Unity.ObserveAsync(g.Workspace, control, ct), controlPath = control, path = g.Workspace.Path };
        }
        if (input.Action == "refresh")
        {
            Require(app.Running && app.Ready, "EditorNotReady", "Wait for the selected Editor to become ready before requesting validation.");
            long sequence = await provider.ObserveValidationSequence(g, ct);
            string fingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(g.Selection.ReceiptId + ":" + sequence)));
            string id = WorkspaceStore.Key(input.RequestKey);
            var invocation = new ValidationInvocation(1, id, fingerprint, sequence);
            Save(Path.Combine(control, "validation-invocation.json"), invocation);
            await provider.Unity.RequestValidationAsync(g.Workspace, control, id, fingerprint, ct);
            return new { selection, invocation, controlPath = control };
        }
        if (mount.Mounted)
        {
            var invocation = Read<ValidationInvocation>(Path.Combine(control, "validation-invocation.json"));
            if (app.Validation is { } validation && invocation is not null && validation.RequestId == invocation.RequestId)
            {
                long sequence = await provider.ObserveValidationSequence(g, ct);
                Save(Path.Combine(control, "validation-observed.json"), new ValidationObservation(1, validation, sequence));
            }
            await provider.Unity.StopAsync(g.Workspace, control, ct);
            await provider.Unity.RemoveInfrastructureAsync(g.Workspace, control, ct);
            await provider.MountProvider(g).FlushAsync(g.Workspace.Snapshot!.Mount!, ct);
            await provider.MountProvider(g).UnmountAsync(g.Workspace.Snapshot.Mount!, ct);
        }
        else await provider.Unity.StopAsync(g.Workspace, control, ct);
        WindowsValidationProvider.Quiescent((await provider.ObserveViewAsync(provider.ProjectId, selection.Intent.Cache.ViewId, ct)).Resources);
        using var stopped = await provider.OpenArtifact(new("source-generation", input.GenerationId!), ct);
        return new { selection, stopped = true, seal = stopped.Seal, controlPath = control };
    }
    private static bool Name(string? value) => value is not null && Regex.IsMatch(value, "^[A-Za-z0-9][A-Za-z0-9._:-]{0,127}$", RegexOptions.CultureInvariant);
}
