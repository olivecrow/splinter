using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class WorkspaceCoordinator
{
    private async Task<WorkspaceView> RequireOriginalResult(WorkspaceRecord w, CancellationToken ct)
    {
        RequireLive(w, allowOriginalApply: true);
        if (w.SourceKind != "none" || w.ApplicationKind != "none" || sources is not IOriginalApplyProvider)
            throw new WorkspaceException("SourceIntegrationUnsupported", "Original apply requires a plain folder and the editorless application mode.");
        var view = await ViewAsync(w, ct);
        RequireSource(view);
        var result = RequireSnapshot(w).Result;
        if (w.ResultRevision is null || view.Source!.HasChanges || view.Source.Revision != w.ResultRevision ||
            result is not { IndependentOfSnapshot: true, CollectionProtocol: SnapshotResult.StableJournalProtocol } ||
            result.Revision != view.Source.Revision || result.Fingerprint != view.Source.Fingerprint)
            throw new WorkspaceException("ResultNotReady", "Apply only the exact independently preserved current source; preserve later workspace changes first.");
        return view;
    }

    public async Task<WorkspaceView> PlanOriginalAsync(string id, CancellationToken ct = default)
    {
        using var gate = await store.LockAsync(id, ct);
        var w = store.Get(id);
        var view = await RequireOriginalResult(w, ct);
        using var projectGate = await store.LockAsync(WorkspaceStore.ProjectKey(w.ProjectPath), ct);
        return view with { OriginalApplyPlan = await ((IOriginalApplyProvider)sources).PlanOriginalAsync(w, ct) };
    }

    public async Task<WorkspaceView> ApplyOriginalAsync(string id, string intentKey, string expectedPlanDigest, CancellationToken ct = default)
    {
        using var gate = await store.LockAsync(id, ct);
        var w = store.Get(id);
        await RequireOriginalResult(w, ct);
        using var projectGate = await store.LockAsync(WorkspaceStore.ProjectKey(w.ProjectPath), ct);
        try
        {
            var result = await ((IOriginalApplyProvider)sources).ApplyOriginalAsync(w, intentKey, expectedPlanDigest, ct);
            w.IntegratedRevision = result.Revision;
            w.Phase = "integrated";
            w.LastError = null;
            store.Save(w, "original-apply-completed");
            return await ViewAsync(w, ct);
        }
        catch (Exception ex) { RecordFailure(w, ex); throw; }
    }
}
