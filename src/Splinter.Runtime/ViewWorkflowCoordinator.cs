using Splinter.Core;
using static Splinter.Runtime.ViewWorkflowRules;

namespace Splinter.Runtime;

// Durable state drives retries; providers reconcile their own atomic receipts.
public sealed class ViewWorkflowCoordinator(ViewWorkflowStore store,
    IViewWorkflowProvider provider, Action<string>? boundary = null)
{
    public async Task<ViewWorkflowState> InspectAsync(string projectId, CancellationToken ct = default)
    {
        Name(projectId);
        using var gate = await store.LockAsync(projectId, ct);
        return store.Read(projectId);
    }

    // A product mutation may need the original frozen request before Execute.
    // Reconcile only the flushed ledger here; external effects remain in Execute.
    public async Task<ViewWorkflowState> RecoverForMutationAsync(string projectId, CancellationToken ct = default)
    {
        Name(projectId);
        using var gate = await store.LockAsync(projectId, ct);
        return store.Read(projectId, recoverPending: true);
    }

    public async Task<ViewWorkflowResponse> ExecuteAsync(ViewWorkflowRequest request, CancellationToken ct = default)
    {
        Request(request);
        using var gate = await store.LockAsync(request.ProjectId, ct);
        var state = store.Read(request.ProjectId, recoverPending: true);
        state.Operations.TryGetValue(request.RequestKey, out var op);
        Require(op is null || op.Request == request, "ViewRequestKeyConflict", "Retry the original payload for this request key.");
        var (resource, owner) = Authority(state, request);
        Require(owner == request.Owner, "ViewOwnerMismatch", "The resource belongs to another owner generation.");
        await provider.VerifyOwnerAsync(request.ProjectId, resource, owner, ct);
        if (op?.Response is { } completed) return completed with { Replayed = true };
        string[] keys = Resources(state, request);
        var blocking = state.Operations.Values.Where(other => other.Request.RequestKey != request.RequestKey && other.Response is null &&
            Resources(state, other.Request).Intersect(keys, StringComparer.Ordinal).Any()).Select(other => other.Request).ToArray();
        if (blocking.Length != 0)
            throw new WorkspaceException("ViewOperationPending", "Resume the pending operation on this resource before changing its intent.",
                new { blockingRequests = blocking });
        if (op is null)
        {
            // Rejections with no possible external effect do not reserve a key
            // or strand a view behind an impossible pending request.
            await PreflightAsync(state, request, ct);
            op = new(OperationId(request), request, null, null);
            state.Operations.Add(request.RequestKey, op);
            state = store.Save(state);
        }
        boundary?.Invoke(request.Operation + ":intent");
        ViewWorkflowResponse response;
        switch (request.Operation)
        {
            case "publish":
            {
                var intent = new CandidatePublicationIntent(op.Id, request.ProjectId, op.Id, request.Worker!, "source-only-v1");
                var receipt = await provider.FindCandidateAsync(intent, ct);
                if (receipt is null)
                {
                    var observed = await provider.ObserveWorkerAsync(request.ProjectId, request.Worker!.WorkspaceId, ct);
                    Require(observed.Worker == request.Worker, "ViewSourceChanged", "The worker identity or sealed source changed; preserve it.");
                    Quiescent(observed.Resources);
                    Require(observed.SnapshotAvailable == true, "ViewSnapshotUnavailable", "The exact snapshot must be verified available.");
                    Require(observed.ManagedPathPolicy == "source-only-v1", "ViewManagedPathsUnsupported",
                        "The adapter has not qualified source-only publication with its managed paths.");
                    receipt = await provider.PublishAsync(intent, ct);
                }
                Publication(receipt, intent);
                boundary?.Invoke("publish:observed");
                state.Candidates.Add(op.Id, new(receipt, null, false));
                response = new(1, op.Id, request.Operation, false, Candidate: receipt);
                break;
            }
            case "select":
            {
                var candidate = Candidate(state, request.CandidateId!);
                Require(!candidate.Released, "ViewArtifactReleased", "A released candidate cannot be selected.");
                var cache = request.Cache!;
                if (state.Views.TryGetValue(cache.ViewId, out var view))
                    Require(view.Owner == request.Owner && view.Cache == cache && !view.CacheReleased, "ViewCacheMismatch", "Keep the open view owner and complete cache identity.");
                Require(!state.Views.Values.Any(v => v.Cache.CacheId == cache.CacheId && v.Cache.ViewId != cache.ViewId),
                    "ViewCacheMismatch", "The cache is already owned by another view.");
                var intent = new ViewSelectionIntent(op.Id, request.ProjectId, op.Id, request.Owner, cache, candidate.Publication);
                var receipt = await provider.FindSelectionAsync(intent, ct);
                if (receipt is null)
                {
                    await QuiescentView(request.ProjectId, cache.ViewId, request.Owner, ct);
                    receipt = await provider.SelectAsync(intent, ct);
                }
                Selection(receipt, intent);
                boundary?.Invoke("select:observed");
                state.Generations.Add(op.Id, new(receipt, null, null, false));
                state.Views[cache.ViewId] = new(cache, request.Owner, op.Id);
                response = new(1, op.Id, request.Operation, false, Selection: receipt);
                break;
            }
            case "resume":
            {
                var generation = Generation(state, request.GenerationId!);
                Require(!generation.Released && generation.FrozenSeal is null, "ViewGenerationFrozen", "Use a new selection after source preservation; keep the frozen generation.");
                var selection = generation.Selection;
                Require(state.Views[selection.Intent.Cache.ViewId].CurrentGenerationId == request.GenerationId,
                    "ViewGenerationMismatch", "Resume only the currently selected source generation.");
                await QuiescentView(request.ProjectId, selection.Intent.Cache.ViewId, request.Owner, ct);
                var receipt = await provider.FindSelectionAsync(selection.Intent, ct);
                Require(receipt is not null, "ViewSelectionMissing", "Preserve source/cache and recover the original selection receipt.");
                Selection(receipt!, selection.Intent);
                boundary?.Invoke(request.Operation + ":observed");
                response = new(1, op.Id, request.Operation, false, Selection: selection);
                break;
            }
            case "preserve":
            {
                var existing = ResultFor(state, request.Target!);
                if (existing is not null)
                {
                    await provider.VerifyResultAsync(existing, ct);
                    response = new(1, op.Id, request.Operation, false, Result: existing);
                    break;
                }
                if (op.Artifact is null)
                {
                    var artifact = await FreezeAsync(state, request, ct);
                    op = op with { Artifact = artifact };
                    state.Operations[request.RequestKey] = op;
                    state = store.Save(state);
                }
                boundary?.Invoke("preserve:frozen");
                var intent = new ViewPreservationIntent(op.Id, request.ProjectId, op.Artifact!);
                var result = await provider.FindResultAsync(intent, ct) ?? await provider.PreserveAsync(intent, ct);
                Result(result, intent);
                await provider.VerifyResultAsync(result, ct);
                boundary?.Invoke("preserve:observed");
                if (request.Target!.Kind == "candidate")
                    state.Candidates[request.Target.Id] = Candidate(state, request.Target.Id) with { Result = result };
                else state.Generations[request.Target.Id] = Generation(state, request.Target.Id) with { Result = result };
                response = new(1, op.Id, request.Operation, false, Result: result);
                break;
            }
            case "release":
            {
                var result = ResultFor(state, request.Target!);
                Require(result is not null, "ViewResultRequired", "Preserve and verify an independent result before releasing this artifact.");
                await provider.VerifyResultAsync(result!, ct);
                if (request.Target!.Kind == "candidate")
                    Require(!state.Generations.Values.Any(g => !g.Released && g.Selection.Intent.Candidate.Intent.CandidateId == request.Target.Id),
                        "ViewDependencyActive", "Source generations still depend on this candidate; preserve and release them first.");
                else if (!Generation(state, request.Target.Id).Released)
                {
                    var generation = Generation(state, request.Target.Id);
                    await QuiescentView(request.ProjectId, generation.Selection.Intent.Cache.ViewId, request.Owner, ct);
                    // After a lost release response, the provider may report the
                    // artifact absent through idempotent ReleaseAsync. It must
                    // otherwise verify this frozen seal before removing anything.
                }
                var intent = new ViewReleaseIntent(op.Id, request.ProjectId, result!.Intent.Artifact, result);
                bool alreadyReleased = request.Target.Kind == "candidate" ? Candidate(state, request.Target.Id).Released : Generation(state, request.Target.Id).Released;
                if (!alreadyReleased) await provider.ReleaseAsync(intent, ct);
                boundary?.Invoke("release:observed");
                if (request.Target.Kind == "candidate")
                    state.Candidates[request.Target.Id] = Candidate(state, request.Target.Id) with { Released = true };
                else
                {
                    var generation = Generation(state, request.Target.Id);
                    state.Generations[request.Target.Id] = generation with { Released = true };
                    string viewId = generation.Selection.Intent.Cache.ViewId;
                    if (state.Views[viewId].CurrentGenerationId == request.Target.Id)
                        state.Views[viewId] = state.Views[viewId] with { CurrentGenerationId = null };
                }
                response = new(1, op.Id, request.Operation, false, Result: result, Released: request.Target);
                break;
            }
            case "close-view":
            {
                var cache = request.Cache!;
                Require(state.Views.TryGetValue(cache.ViewId, out var view) && view.Cache == cache && view.Owner == request.Owner,
                    "ViewCacheMismatch", "Close only the retained view/cache identity.");
                Require(!state.Generations.Values.Any(g => g.Selection.Intent.Cache.ViewId == cache.ViewId && !g.Released),
                    "ViewDependencyActive", "Preserve and release all source generations before closing their cache.");
                await QuiescentView(request.ProjectId, cache.ViewId, request.Owner, ct);
                if (!view!.CacheReleased) await provider.CloseCacheAsync(request.ProjectId, cache, request.Owner, ct);
                boundary?.Invoke("close-view:observed");
                state.Views[cache.ViewId] = view with { CacheReleased = true, CurrentGenerationId = null };
                response = new(1, op.Id, request.Operation, false, ClosedViewId: cache.ViewId);
                break;
            }
            default: throw new InvalidOperationException("Request was not validated.");
        }
        state.Operations[request.RequestKey] = op with { Response = response };
        store.Save(state);
        boundary?.Invoke(request.Operation + ":completed");
        return response;
    }

    public async Task<string[]> SnapshotDependenciesAsync(string projectId, SnapshotReference snapshot, CancellationToken ct = default)
    {
        Snapshot(snapshot);
        var state = await InspectAsync(projectId, ct);
        return SnapshotDependencies(state, snapshot);
    }

    public static string[] SnapshotDependencies(ViewWorkflowState state, SnapshotReference snapshot)
    {
        Snapshot(snapshot);
        var references = state.Candidates.Values.Select(c => c.Publication.Intent.Worker.Snapshot)
            .Concat(state.Generations.Values.Select(g => g.Selection.Intent.Candidate.Intent.Worker.Snapshot))
            .Concat(state.Operations.Values.Where(o => o.Request.Worker is not null).Select(o => o.Request.Worker!.Snapshot))
            .Concat(state.Views.Values.Where(v => v.Cache.SnapshotBacked).Select(v => v.Cache.Snapshot))
            .Concat(state.Operations.Values.Where(o => o.Request.Cache is { SnapshotBacked: true }).Select(o => o.Request.Cache!.Snapshot));
        Require(!references.Any(s => s.Id == snapshot.Id && s != snapshot), "SnapshotIdentityMismatch",
            "A known snapshot ID has a different tuple; an identity mismatch is not an empty dependency set.");
        return state.Candidates.Where(p => !p.Value.Released && p.Value.Publication.Intent.Worker.Snapshot == snapshot).Select(p => "candidate:" + p.Key)
            .Concat(state.Generations.Where(p => !p.Value.Released && p.Value.Selection.Intent.Candidate.Intent.Worker.Snapshot == snapshot).Select(p => "source-generation:" + p.Key))
            .Concat(state.Operations.Values.Where(o => o.Response is null && o.Request.Worker?.Snapshot == snapshot).Select(o => "operation:" + o.Id))
            .Concat(state.Views.Where(v => !v.Value.CacheReleased && v.Value.Cache.SnapshotBacked && v.Value.Cache.Snapshot == snapshot).Select(v => "cache:" + v.Key))
            .Concat(state.Operations.Values.Where(o => o.Response is null && o.Request.Cache is { SnapshotBacked: true } cache && cache.Snapshot == snapshot).Select(o => "operation:" + o.Id))
            .Order(StringComparer.Ordinal).ToArray();
    }

    private async Task<ViewArtifact> FreezeAsync(ViewWorkflowState state, ViewWorkflowRequest request, CancellationToken ct)
    {
        var target = request.Target!;
        if (target.Kind == "candidate")
        {
            var candidate = Candidate(state, target.Id);
            Require(!candidate.Released, "ViewArtifactReleased", "The candidate is already released.");
            return new(target, request.Owner, candidate.Publication.Intent.Worker.Snapshot, candidate.Publication.CandidateSeal);
        }
        var generation = Generation(state, target.Id);
        Require(!generation.Released, "ViewArtifactReleased", "The source generation is already released.");
        await QuiescentView(request.ProjectId, generation.Selection.Intent.Cache.ViewId, request.Owner, ct);
        var seal = await provider.SealSourceAsync(generation.Selection, ct);
        Seal(seal);
        Require(generation.FrozenSeal is null || generation.FrozenSeal == seal, "ViewSourceChanged", "The frozen source output changed; preserve both states.");
        state.Generations[target.Id] = generation with { FrozenSeal = seal };
        return new(target, request.Owner, generation.Selection.Intent.Candidate.Intent.Worker.Snapshot, seal);
    }
    private async Task PreflightAsync(ViewWorkflowState state, ViewWorkflowRequest request, CancellationToken ct)
    {
        if (request.Worker is { } worker)
        {
            var observed = await provider.ObserveWorkerAsync(request.ProjectId, worker.WorkspaceId, ct);
            Require(observed.Worker == worker, "ViewSourceChanged", "The worker's source or snapshot changed.");
            Quiescent(observed.Resources);
            Require(observed.SnapshotAvailable == true, "ViewSnapshotUnavailable", "Verify the original snapshot before publication.");
            Require(observed.ManagedPathPolicy == "source-only-v1", "ViewManagedPathsUnsupported", "The provider has not qualified managed-path separation.");
        }
        else if (request.Cache is { } cache)
        {
            if (request.Operation == "close-view")
            {
                Require(state.Views.TryGetValue(cache.ViewId, out var retained) && retained.Cache == cache && retained.Owner == request.Owner,
                    "ViewCacheMismatch", "Close only the retained cache identity.");
                Require(!state.Generations.Values.Any(g => g.Selection.Intent.Cache.ViewId == cache.ViewId && !g.Released),
                    "ViewDependencyActive", "Preserve and release all source generations first.");
                await QuiescentView(request.ProjectId, cache.ViewId, request.Owner, ct);
                return;
            }
            var candidate = Candidate(state, request.CandidateId!);
            Require(!candidate.Released, "ViewArtifactReleased", "Keep the released candidate's independent result.");
            Require((!state.Views.TryGetValue(cache.ViewId, out var view) || view.Cache == cache && view.Owner == request.Owner && !view.CacheReleased) &&
                !state.Views.Values.Any(v => v.Cache.CacheId == cache.CacheId && v.Cache.ViewId != cache.ViewId),
                "ViewCacheMismatch", "The complete cache identity and single-view ownership must match.");
            await QuiescentView(request.ProjectId, cache.ViewId, request.Owner, ct);
        }
        else if (request.GenerationId is { } id)
        {
            var generation = Generation(state, id);
            Require(!generation.Released && generation.FrozenSeal is null, "ViewGenerationFrozen", "Preserved source generations cannot be resumed.");
            Require(state.Views[generation.Selection.Intent.Cache.ViewId].CurrentGenerationId == id,
                "ViewGenerationMismatch", "Only the current generation can resume.");
            if (request.Operation == "resume") await QuiescentView(request.ProjectId, generation.Selection.Intent.Cache.ViewId, request.Owner, ct);
        }
        else if (request.Operation == "release")
        {
            Require(ResultFor(state, request.Target!) is not null, "ViewResultRequired", "Preserve an independent result before release.");
            if (request.Target!.Kind == "candidate")
                Require(!state.Generations.Values.Any(g => !g.Released && g.Selection.Intent.Candidate.Intent.CandidateId == request.Target.Id),
                    "ViewDependencyActive", "Retained source generations still depend on the candidate.");
        }
    }
    private async Task QuiescentView(string project, string view, ViewOwner owner, CancellationToken ct)
    {
        var observation = await provider.ObserveViewAsync(project, view, ct);
        Require(observation.Owner == owner, "ViewOwnerMismatch", "The view belongs to another owner generation.");
        Quiescent(observation.Resources);
    }
    private static ViewCandidateState Candidate(ViewWorkflowState state, string id) =>
        state.Candidates.TryGetValue(id, out var value) ? value : throw new WorkspaceException("ViewCandidateNotFound", "No published candidate with this identity.");
    private static ViewGenerationState Generation(ViewWorkflowState state, string id) =>
        state.Generations.TryGetValue(id, out var value) ? value : throw new WorkspaceException("ViewGenerationNotFound", "No selected source generation with this identity.");
    private static ViewIndependentResult? ResultFor(ViewWorkflowState state, ViewTarget target) =>
        target.Kind == "candidate" ? Candidate(state, target.Id).Result : Generation(state, target.Id).Result;
    private static (string Resource, ViewOwner Owner) Authority(ViewWorkflowState state, ViewWorkflowRequest request)
    {
        if (request.Worker is { } worker) return (worker.WorkspaceId, worker.Owner);
        if (request.Cache is { } cache)
            return (cache.ViewId, state.Views.TryGetValue(cache.ViewId, out var view) ? view.Owner : request.Owner);
        if (request.Target is { Kind: "candidate" } target)
        {
            var origin = Candidate(state, target.Id).Publication.Intent.Worker;
            return (origin.WorkspaceId, origin.Owner);
        }
        var intent = Generation(state, request.GenerationId ?? request.Target!.Id).Selection.Intent;
        return (intent.Cache.ViewId, intent.Owner);
    }
    private static string[] Resources(ViewWorkflowState state, ViewWorkflowRequest request)
    {
        if (request.Worker is { } worker) return ["worker:" + worker.WorkspaceId, "candidate:" + OperationId(request)];
        if (request.Cache is { } cache) return request.CandidateId is { } candidate
            ? ["view:" + cache.ViewId, "cache:" + cache.CacheId, "candidate:" + candidate]
            : ["view:" + cache.ViewId, "cache:" + cache.CacheId];
        if (request.Target is { Kind: "candidate" } target) return ["candidate:" + target.Id];
        var selection = Generation(state, request.GenerationId ?? request.Target!.Id).Selection.Intent;
        return ["view:" + selection.Cache.ViewId, "cache:" + selection.Cache.CacheId, "candidate:" + selection.Candidate.Intent.CandidateId];
    }
}
