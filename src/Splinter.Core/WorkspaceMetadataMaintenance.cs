namespace Splinter.Core;

public sealed record WorkspaceMaintenanceStage(string Scope, string Phase, string? Store, string RequestKey,
    long ReclaimedBytes = 0, long RetainedBytes = 0, long PreservedEntries = 0,
    string? PreservedDirectory = null, MetadataTreeRoot? PreservedRoot = null);
public sealed record WorkspaceMaintenanceResult(int Version, string OwnerId, string RequestDigest, string RequestKey,
    string Scope, string Phase, WorkspaceMaintenanceStage[] Stages, string? FailureCode = null);
public interface IWorkspaceMetadataMaintenance
{
    bool SupportsMetadataMaintenance { get; }
    Task<WorkspaceMaintenanceResult> MaintainMetadataAsync(WorkspaceRecord workspace, string control,
        string requestKey, string scope = "all", CancellationToken cancellationToken = default);
    Task<WorkspaceMaintenanceResult?> MaintainMetadataIfNeededAsync(WorkspaceRecord workspace, string control,
        string requestKey, CancellationToken cancellationToken = default);
    WorkspaceMaintenanceResult? ObserveMetadataMaintenance(WorkspaceRecord workspace, string control);
    void RequireMetadataMaintenanceComplete(WorkspaceRecord workspace, string control);
}
