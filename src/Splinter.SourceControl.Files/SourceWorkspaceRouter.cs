using Splinter.Core;

namespace Splinter.SourceControl.Files;

// Selection is durable. An adapter failure is propagated, never reinterpreted
// as a request for another source mode.
public sealed class SourceWorkspaceRouter(ISourceWorkspaceProvider git, FileWorkspaceProvider files, ISourceWorkspaceProvider? plastic = null) :
    ISourceWorkspaceProvider, ISnapshotSourceWorkspaceProvider, ISourceDiscardProvider, IOriginalApplyProvider, ISnapshotCheckpointSourceProvider, ISourceInspectionProofProvider, IWorkspaceGenerationSource, IWorkspaceGenerationBranchSource, IRetainedResultMaintenance, IWorkspaceMetadataMaintenance
{
    public bool SupportsMetadataMaintenance => files.SupportsMetadataMaintenance;
    public Task<WorkspaceMaintenanceResult> MaintainMetadataAsync(WorkspaceRecord w, string control, string requestKey, string scope = "all", CancellationToken ct = default)
        => files.MaintainMetadataAsync(w, control, requestKey, scope, ct);
    public WorkspaceMaintenanceResult? ObserveMetadataMaintenance(WorkspaceRecord w, string control) => files.ObserveMetadataMaintenance(w, control);
    public Task<WorkspaceMaintenanceResult?> MaintainMetadataIfNeededAsync(WorkspaceRecord w, string control, string requestKey, CancellationToken ct = default)
        => files.MaintainMetadataIfNeededAsync(w, control, requestKey, ct);
    public void RequireMetadataMaintenanceComplete(WorkspaceRecord w, string control) => files.RequireMetadataMaintenanceComplete(w, control);
    public Task MaintainRetainedResultsAsync(WorkspaceRecord w, string control, CancellationToken ct = default) =>
        w.SourceKind == "plastic" && plastic is IRetainedResultMaintenance maintenance ? maintenance.MaintainRetainedResultsAsync(w, control, ct) : files.MaintainRetainedResultsAsync(w, control, ct);
    private ISourceWorkspaceProvider For(WorkspaceRecord w) => w.SourceKind switch
    { "git" => git, "none" => files, "plastic" when plastic is not null => plastic, _ => throw new WorkspaceException("SourceSelectionUnsupported", "Unknown or unavailable durable source kind.") };
    private ISnapshotSourceWorkspaceProvider Snapshot(WorkspaceRecord w) => (ISnapshotSourceWorkspaceProvider)For(w);
    private ISourceDiscardProvider Discard(WorkspaceRecord w) => (ISourceDiscardProvider)For(w);
    public Task<WorkspaceResultGeneration> PreserveGenerationAsync(WorkspaceRecord w, string control,
        WorkspaceCaptureIntent intent, SourceCaptureIdentity capture, ISourceCaptureTree tree, CancellationToken ct = default) =>
        w.SourceKind == "plastic" ? plastic is IWorkspaceGenerationSource provider
            ? provider.PreserveGenerationAsync(w, control, intent, capture, tree, ct)
            : throw new WorkspaceException("SourceCaptureUnsupported", "Retain the Plastic isolation adapter for generation preservation.")
            : files.PreserveGenerationAsync(w, control, intent, capture, tree, ct);
    public Task VerifyGenerationAsync(WorkspaceRecord w, string control, WorkspaceResultGeneration generation, CancellationToken ct = default) =>
        files.VerifyGenerationAsync(w, control, generation, ct);
    public async Task<IntegrationResult> IntegrateGenerationAsync(WorkspaceRecord w, WorkspaceResultGeneration generation, string target, CancellationToken ct = default)
    {
        if (git is not IWorkspaceGenerationBranchSource provider || w.SourceKind != "git")
            throw new WorkspaceException("SourceIntegrationUnsupported", "The selected source has no explicit Git branch integration.");
        return await provider.IntegrateGenerationAsync(w, await files.MaterializeGitGenerationAsync(w, generation, ct), target, ct);
    }
    public Task<string> ResolveRevisionAsync(string p, string r, CancellationToken ct = default) => git.ResolveRevisionAsync(p, r, ct);
    public Task<SourceWorkspace> CreateAsync(WorkspaceRecord w, CancellationToken ct = default) => For(w).CreateAsync(w, ct);
    public Task<SourceState> InspectAsync(WorkspaceRecord w, CancellationToken ct = default) =>
        w.Results.Count > 0 && w.SourceKind != "plastic" ? files.InspectLiveGenerationAsync(w, ct) : For(w).InspectAsync(w, ct);
    public async Task<ProvedSourceInspection> InspectWithProofAsync(WorkspaceRecord w, CancellationToken ct = default) =>
        w.Results.Count > 0 ? new(await InspectAsync(w, ct), null) : For(w) is ISourceInspectionProofProvider provider
            ? await provider.InspectWithProofAsync(w, ct) : new(await For(w).InspectAsync(w, ct), null);
    public Task<string> CheckpointAsync(WorkspaceRecord w, string message, CancellationToken ct = default) => For(w).CheckpointAsync(w, message, ct);
    public bool SupportsCheckpointAndPreserve(WorkspaceRecord w) =>
        For(w) is ISnapshotCheckpointSourceProvider provider && provider.SupportsCheckpointAndPreserve(w);
    public Task<SnapshotSourceCheckpoint> CheckpointAndPreserveAsync(WorkspaceRecord w, string control, string message, CancellationToken ct = default) =>
        For(w) is ISnapshotCheckpointSourceProvider provider && provider.SupportsCheckpointAndPreserve(w)
            ? provider.CheckpointAndPreserveAsync(w, control, message, ct)
            : throw new WorkspaceException("SnapshotCheckpointUnsupported", "The selected source has no combined checkpoint/preservation operation.");
    public Task<IntegrationResult> IntegrateAsync(WorkspaceRecord w, string target, CancellationToken ct = default) => For(w).IntegrateAsync(w, target, ct);
    private IOriginalApplyProvider Original(WorkspaceRecord w) => (w.SourceKind == "git" ? files : For(w)) as IOriginalApplyProvider ??
        throw new WorkspaceException("SourceIntegrationUnsupported", "The selected source has no conditional original publisher.");
    public Task<OriginalApplyPlan> PlanOriginalAsync(WorkspaceRecord w, CancellationToken ct = default, string[]? paths = null) => Original(w).PlanOriginalAsync(w, ct, paths);
    public Task<IntegrationResult> ApplyOriginalAsync(WorkspaceRecord w, string key, string digest, CancellationToken ct = default, string[]? paths = null) => Original(w).ApplyOriginalAsync(w, key, digest, ct, paths);
    public Task ReleaseAsync(WorkspaceRecord w, CancellationToken ct = default) => For(w).ReleaseAsync(w, ct);
    public Task<string> PreflightAsync(WorkspaceRecord w, CancellationToken ct = default) => Snapshot(w).PreflightAsync(w, ct);
    public Task RegisterAsync(WorkspaceRecord w, string c, CancellationToken ct = default) => Snapshot(w).RegisterAsync(w, c, ct);
    public Task AttachAsync(WorkspaceRecord w, string c, CancellationToken ct = default) => Snapshot(w).AttachAsync(w, c, ct);
    public Task<string> EstablishBaselineAsync(WorkspaceRecord w, string c, CancellationToken ct = default) => Snapshot(w).EstablishBaselineAsync(w, c, ct);
    public Task<string> PreserveAsync(WorkspaceRecord w, string c, CancellationToken ct = default) => Snapshot(w).PreserveAsync(w, c, ct);
    public Task VerifyResultAsync(WorkspaceRecord w, string c, CancellationToken ct = default) =>
        w.Results.Count > 0 ? files.VerifyGenerationAsync(w, c, w.Results.Last(), ct) : Snapshot(w).VerifyResultAsync(w, c, ct);
    public Task UnregisterAsync(WorkspaceRecord w, string c, CancellationToken ct = default,
        Func<CancellationToken, Task>? verifyGeneration = null)
    {
        Func<CancellationToken, Task>? validator = verifyGeneration;
        if (validator is null && w.SourceKind == "git" && w.Results.Count > 0)
        {
            var generation = w.Results.Last();
            validator = token => files.VerifyGenerationAsync(w, c, generation, token);
        }
        return Snapshot(w).UnregisterAsync(w, c, ct, validator);
    }
    public Task<string> ObserveDiscardRevisionAsync(WorkspaceRecord w, string c, CancellationToken ct = default) => Discard(w).ObserveDiscardRevisionAsync(w, c, ct);
    public Task DiscardSourceAsync(WorkspaceRecord w, string c, CancellationToken ct = default) => Discard(w).DiscardSourceAsync(w, c, ct);
    public Task VerifyDiscardedAsync(WorkspaceRecord w, string c, CancellationToken ct = default) => Discard(w).VerifyDiscardedAsync(w, c, ct);
}
