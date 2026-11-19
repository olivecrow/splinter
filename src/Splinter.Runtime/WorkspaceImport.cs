using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class WorkspaceCoordinator
{
    public async Task<WorkspaceView> ImportAsync(WorkspaceImportRequest request, LiveOriginalEditorPermit? liveTarget = null,
        CancellationToken ct = default)
    {
        if (request.Id == request.From || (request.Plan ? request.IntentKey is not null || request.ExpectedPlanDigest is not null :
            string.IsNullOrWhiteSpace(request.IntentKey) || request.IntentKey.Length > 128 || string.IsNullOrWhiteSpace(request.ExpectedPlanDigest)))
            throw new WorkspaceException("WorkspaceImportRequestInvalid", "Select distinct workspaces and either plan or supply the reviewed digest and intent key.");
        if (sources is not IWorkspaceImportSource imports || sources is not IWorkspaceGenerationSource generations ||
            snapshots is not IWorkspacePublicationTargetProvider targets)
            throw new WorkspaceException("WorkspaceImportUnsupported", "The composition must support fixed generations and pinned workspace publication.");
        // Reciprocal imports take both task locks in the same order. Editors continue editing the mounted trees.
        bool targetFirst = string.CompareOrdinal(request.Id, request.From) < 0;
        using var first = await store.LockAsync(targetFirst ? request.Id : request.From, ct);
        using var second = await store.LockAsync(targetFirst ? request.From : request.Id, ct);
        var source = store.Get(request.From);
        var target = store.Get(request.Id);
        if (!WorkspaceStorage.HasRangeJournal(target.StorageMode) || source.StorageMode != target.StorageMode ||
            !WorkspaceStore.SamePath(source.ProjectPath, target.ProjectPath) || source.ApplicationKind != target.ApplicationKind ||
            source.BaseRevision is null || source.BaseRevision != target.BaseRevision ||
            !source.ManagedPaths.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(target.ManagedPaths) ||
            source.StorageMode == "project-snapshot" && source.GeneratedDirectoryId != target.GeneratedDirectoryId ||
            source.StorageMode == "plastic-snapshot" && source.BaseReference != target.BaseReference)
            throw new WorkspaceException("WorkspaceImportBasisMismatch", "Choose workspaces of the same project, source basis and application scope.");
        var generation = source.Results.SingleOrDefault(r => r.Id == request.Result)
            ?? throw new WorkspaceException("ResultNotReady", "Select a fixed checkpoint belonging to the source workspace.");
        if (request.IntentKey is not null && target.Imports.Find(request.IntentKey, r => r.Publication.IntentKey) is { } receipt)
        {
            var completed = receipt.Publication.Plan;
            if (completed.PeerSource != new PeerSource(source.Id, source.RequestDigest!) || completed.ResultId != generation.Id ||
                completed.Result != generation.Revision || completed.Digest != request.ExpectedPlanDigest ||
                !OriginalApplyPages.MatchesSelection(completed, store.ControlPath(target.Id), request.Paths))
                throw new WorkspaceException("WorkspaceImportRequestConflict", "The destination intent already identifies another import.");
            await generations.VerifyGenerationAsync(source, store.ControlPath(source.Id), generation, ct);
            return (await ViewAsync(target, ct, verifySource: false)) with { ImportReceipt = receipt.Publication };
        }
        RequireMetadataMaintenanceComplete(source);
        RequireMetadataMaintenanceComplete(target);
        RequireLive(target, allowImport: true);
        if (target.CaptureIntent is not null)
            throw new WorkspaceException("SourceCapturePending", "Complete the destination's pending capture before importing.");
        var view = await ViewAsync(target, ct, verifySource: false);
        if (target.Snapshot is not { Attached: true, Step: "ready", Mount: not null,
            Observation: { SnapshotAvailable: true, Mounted: true, OwnershipVerified: true, ProviderAlive: true, FailureCode: null } })
            throw new WorkspaceException("WorkspaceImportTargetUnavailable", "Reconnect the exact owned destination mount before importing.");
        var app = view.Application;
        if (app?.Running == true ? liveTarget is null || app.Lease is null || app.Resources is not { OwnershipVerified: true, RootRunning: true } ||
            liveTarget.ProcessId != app.Lease.ProcessId || liveTarget.StartTimeUtcTicks != app.Lease.StartTimeUtcTicks :
            liveTarget is not null || app?.Resources is not { OwnershipVerified: true, ActiveProcesses: 0 })
            throw new WorkspaceException("WorkspaceImportEditorMismatch", "Verify the stopped destination or supply its exact owned Editor cooperation receipt.");
        using var projectLock = await store.LockAsync(WorkspaceStore.ProjectKey(target.ProjectPath), ct);
        using var mount = targets.OpenPublicationTarget(target);
        var plan = await imports.ImportAsync(source, generation, target, mount.Publisher, request, liveTarget, ct);
        return request.Plan ? view with { ImportPlan = plan } : view with { ImportReceipt = target.Imports.Last().Publication };
    }

    private static void RequireImportComplete(WorkspaceRecord target)
    {
        if (target.PendingImport is not null)
            throw new WorkspaceException("WorkspaceImportPending", "Resume the fixed incoming publication before collecting or reclaiming this workspace.");
    }
}
