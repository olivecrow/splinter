using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class WorkspaceCoordinator
{
    public async Task<WorkspaceMaintenanceResult> MaintainMetadataAsync(string id, string requestKey, string scope = "all", CancellationToken ct = default)
    {
        using var held = await store.LockAsync(id, ct); var workspace = store.Get(id);
        if (sources is not IWorkspaceMetadataMaintenance maintenance)
            throw new WorkspaceException("MetadataMaintenanceUnsupported", "The selected source does not provide owned metadata maintenance.");
        return await maintenance.MaintainMetadataAsync(workspace, store.ControlPath(id), requestKey, scope, ct);
    }
    private void RequireMetadataMaintenanceComplete(WorkspaceRecord workspace)
    {
        if (WorkspaceStorage.HasRangeJournal(workspace.StorageMode) && sources is IWorkspaceMetadataMaintenance { SupportsMetadataMaintenance: true } maintenance)
            maintenance.RequireMetadataMaintenanceComplete(workspace, store.ControlPath(workspace.Id));
    }
    private WorkspaceMaintenanceResult? ObserveMetadataMaintenance(WorkspaceRecord workspace) =>
        WorkspaceStorage.HasRangeJournal(workspace.StorageMode) && sources is IWorkspaceMetadataMaintenance { SupportsMetadataMaintenance: true } maintenance
            ? maintenance.ObserveMetadataMaintenance(workspace, store.ControlPath(workspace.Id)) : null;
    private async Task MaintainMetadataCoreAsync(WorkspaceRecord workspace, string requestKey, CancellationToken ct, bool force = false)
    {
        if (WorkspaceStorage.HasRangeJournal(workspace.StorageMode) && sources is IWorkspaceMetadataMaintenance { SupportsMetadataMaintenance: true } maintenance)
            if (force) _ = await maintenance.MaintainMetadataAsync(workspace, store.ControlPath(workspace.Id), requestKey, "all", ct);
            else _ = await maintenance.MaintainMetadataIfNeededAsync(workspace, store.ControlPath(workspace.Id), requestKey, ct);
    }
}
