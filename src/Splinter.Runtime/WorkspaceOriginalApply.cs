using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class WorkspaceCoordinator
{
    private async Task<WorkspaceView> RequireOriginalResult(WorkspaceRecord w, CancellationToken ct)
    {
        RequireLive(w, allowOriginalApply: true);
        if (!(w.SourceKind == "none" && (w.ApplicationKind == "none" || w.StorageMode == "project-snapshot") || w.SourceKind == "plastic" && w.StorageMode == "plastic-snapshot" ||
            w.SourceKind == "git" && w.StorageMode == "git-snapshot" && w.Results.Count > 0) || sources is not IOriginalApplyProvider)
            throw new WorkspaceException("SourceIntegrationUnsupported", "Select a qualified original-apply source and independently preserved result.");
        if (w.Results.Count > 0)
        {
            await ((IWorkspaceGenerationSource)sources).VerifyGenerationAsync(w, store.ControlPath(w.Id), w.Results.Last(), ct);
            return await ViewAsync(w, ct, verifySource: false);
        }
        var view = await ViewAsync(w, ct);
        RequireSource(view);
        var result = RequireSnapshot(w).Result;
        if (w.ResultRevision is null || view.Source!.HasChanges || view.Source.Revision != w.ResultRevision ||
            result is not { IndependentOfSnapshot: true, CollectionProtocol: SnapshotResult.StableJournalProtocol } ||
            result.Revision != view.Source.Revision || result.Fingerprint != view.Source.Fingerprint)
            throw new WorkspaceException("ResultNotReady", "Apply only the exact independently preserved current source; preserve later workspace changes first.");
        return view;
    }

    public async Task<WorkspaceView> PlanOriginalAsync(string id, CancellationToken ct = default, string[]? paths = null)
    {
        using var gate = await store.LockAsync(id, ct);
        var w = store.Get(id);
        RequireMetadataMaintenanceComplete(w);
        var view = await RequireOriginalResult(w, ct);
        using var projectGate = await store.LockAsync(WorkspaceStore.ProjectKey(w.ProjectPath), ct);
        return view with { OriginalApplyPlan = await ((IOriginalApplyProvider)sources).PlanOriginalAsync(w, ct, paths) };
    }
    public async Task<WorkspaceView> PlanOriginalAsync(string id, string resultId, LiveOriginalEditorPermit? liveOriginal,
        CancellationToken ct = default, string[]? paths = null)
    {
        using var gate = await store.LockAsync(id, ct);
        var w = store.Get(id); RequireMetadataMaintenanceComplete(w); RequireLatestGeneration(w, resultId); RequirePermitResult(resultId, liveOriginal); w.LiveOriginal = liveOriginal;
        var view = await RequireOriginalResult(w, ct);
        using var projectGate = await store.LockAsync(WorkspaceStore.ProjectKey(w.ProjectPath), ct);
        return view with { OriginalApplyPlan = await ((IOriginalApplyProvider)sources).PlanOriginalAsync(w, ct, paths) };
    }
    public async Task<WorkspaceView> ApplyOriginalAsync(string id, string resultId, string intentKey, string expectedPlanDigest,
        LiveOriginalEditorPermit? liveOriginal, CancellationToken ct = default, string[]? paths = null)
    {
        using var gate = await store.LockAsync(id, ct);
        var w = store.Get(id);
        if (w.OriginalApplies.Find(intentKey, a => a.IntentKey) is { } completed)
        {
            completed.RequireMatch(w);
            if (completed.Plan.ResultId != resultId || completed.Plan.Digest != expectedPlanDigest ||
                !OriginalApplyPages.MatchesSelection(completed.Plan, store.ControlPath(id), paths))
                throw new WorkspaceException("OriginalApplyRequestConflict", "A completed intent fixes its result, scope and plan digest.");
            await ((IWorkspaceGenerationSource)sources).VerifyGenerationAsync(w, store.ControlPath(id), w.Results.Single(r => r.Id == resultId), ct);
            return (await ViewAsync(w, ct, verifySource: false)) with { OriginalApplyReceipt = completed };
        }
        RequireMetadataMaintenanceComplete(w);
        RequireLive(w, allowOriginalApply: true);
        RequireLatestGeneration(w, resultId); RequirePermitResult(resultId, liveOriginal); w.LiveOriginal = liveOriginal;
        await RequireOriginalResult(w, ct);
        using var projectGate = await store.LockAsync(WorkspaceStore.ProjectKey(w.ProjectPath), ct);
        try
        {
            var result = await ((IOriginalApplyProvider)sources).ApplyOriginalAsync(w, intentKey, expectedPlanDigest, ct, paths);
            w.IntegratedRevision = result.Revision; w.Phase = "ready"; w.LastError = null;
            store.Save(w, "original-apply-completed");
            await MaintainMetadataCoreAsync(w, "apply:" + WorkspaceStore.Key(expectedPlanDigest), ct);
            return (await ViewAsync(w, ct, verifySource: false)) with { OriginalApplyReceipt = w.OriginalApply };
        }
        catch (Exception ex) { RecordFailure(w, ex); throw; }
    }
    private static void RequireLatestGeneration(WorkspaceRecord w, string resultId)
    {
        if (w.CaptureIntent is not null) throw new WorkspaceException("SourceCapturePending", "Complete the frozen source capture first.");
        if (w.Results.LastOrDefault()?.Id != resultId)
            throw new WorkspaceException("ResultNotReady", "A new original publication must explicitly select the latest immutable result generation.");
        if (w.OriginalApply is { Completed: false } pending && pending.Plan.ResultId != resultId)
            throw new WorkspaceException("OriginalApplyPending", "Resume the exact pending publication.");
    }
    private static void RequirePermitResult(string resultId, LiveOriginalEditorPermit? permit)
    {
        if (permit is not null && permit.ResultId != resultId)
            throw new WorkspaceException("LiveOriginalPermitInvalid", "The original Editor cooperation receipt must bind the selected result.");
    }

    public async Task<WorkspaceView> ApplyOriginalAsync(string id, string intentKey, string expectedPlanDigest, CancellationToken ct = default, string[]? paths = null)
    {
        using var gate = await store.LockAsync(id, ct);
        var w = store.Get(id);
        RequireMetadataMaintenanceComplete(w);
        await RequireOriginalResult(w, ct);
        using var projectGate = await store.LockAsync(WorkspaceStore.ProjectKey(w.ProjectPath), ct);
        try
        {
            var result = await ((IOriginalApplyProvider)sources).ApplyOriginalAsync(w, intentKey, expectedPlanDigest, ct, paths);
            w.IntegratedRevision = result.Revision;
            w.Phase = "integrated";
            w.LastError = null;
            store.Save(w, "original-apply-completed");
            await MaintainMetadataCoreAsync(w, "apply:" + WorkspaceStore.Key(expectedPlanDigest), ct);
            return await ViewAsync(w, ct);
        }
        catch (Exception ex) { RecordFailure(w, ex); throw; }
    }
}
