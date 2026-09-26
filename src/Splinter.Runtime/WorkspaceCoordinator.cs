using Splinter.Core;

namespace Splinter.Runtime;

public sealed record WorkspaceView(WorkspaceRecord Workspace, SourceState? Source, ApplicationState? Application,
    string ControlPath, string NextCommand)
{
    public SnapshotRecoveryDecision? Recovery => Workspace.StorageMode == "ntfs-snapshot" ? SnapshotRecoveryDecision.For(Workspace) : null;
    public string SourceVerification => Source is null ? "not-checked" : "verified";
    public bool? ValidationCurrent => Source is null ? null : Workspace.Validation is { } validation && validation.Fingerprint == Source.Fingerprint;
}

public sealed partial class WorkspaceCoordinator(WorkspaceStore store, ISourceWorkspaceProvider sources, IApplicationWorkspaceProvider applications,
    IWorkspaceStorageProvider? storage = null, ISnapshotWorkspaceBackend? snapshots = null)
{
    public IReadOnlyList<WorkspaceRecord> List(string? project) => store.List(project is null ? null : WorkspaceStore.CanonicalProject(project));

    public async Task<WorkspaceView> EnsureAsync(string project, string requestKey, string baseReference, string storageMode,
        bool editor, CancellationToken ct = default, string? baselineId = null, string purpose = "task",
        long maxJournalBytes = SnapshotWorkspaceState.DefaultJournalBytes)
    {
        if (string.IsNullOrWhiteSpace(requestKey) || requestKey.Length > 128)
            throw new WorkspaceException("InvalidRequestKey", "Use a stable task key between 1 and 128 characters; reuse it across turns.");
        if (storageMode is not ("git-checkout" or "refs-cow" or "ntfs-snapshot"))
            throw new WorkspaceException("UnsupportedStorageMode", "Choose ntfs-snapshot, refs-cow, or explicit git-checkout for a diagnostic workspace.");
        if (storageMode == "refs-cow" && baselineId is null || storageMode != "refs-cow" && baselineId is not null)
            throw new WorkspaceException("BaselineRequired", "Only refs-cow consumes a sealed baseline.");
        project = WorkspaceStore.CanonicalProject(project);
        if (!Directory.Exists(project)) throw new WorkspaceException("ProjectPathNotFound", "Project directory does not exist.");
        if (WorkspaceStore.SamePath(store.Root, project) || store.Root.StartsWith(project + Path.DirectorySeparatorChar, PathComparison))
            throw new WorkspaceException("UnsafeStateRoot", "State and workspaces must live outside the project being edited.");
        string id = WorkspaceStore.Key(WorkspaceStore.ProjectKey(project) + "\n" + requestKey);
        using var gate = await store.LockAsync(id, ct);
        using var baselineGate = baselineId is null ? null : await store.LockAsync(WorkspaceStore.Key("baseline:" + baselineId), ct);
        var record = store.Find(id);
        if (record is null && baselineId is not null)
        {
            Storage.Probe(store.Root);
            var baseline = store.GetBaseline(baselineId);
            if (!WorkspaceStore.SamePath(project, baseline.ProjectPath) || applications.CompatibilityKey(project) != baseline.CompatibilityKey)
                throw new WorkspaceException("BaselineMismatch", "Baseline belongs to another project or application configuration.");
        }
        if (record is null)
        {
            record = new WorkspaceRecord
            {
                Id = id, RequestKey = requestKey, ProjectPath = project, Path = store.WorkPath(id),
                SourceReference = "splinter/" + id, BaseReference = baseReference, ManagedPaths = applications.ManagedPaths,
                StorageMode = storageMode, BaselineId = baselineId, Purpose = purpose,
                Snapshot = storageMode == "ntfs-snapshot" ? new() { MaxJournalBytes = maxJournalBytes } : null,
                RequestDigest = WorkspaceStore.Key(project + "\n" + requestKey + "\n" + baseReference + "\n" + storageMode + "\n" + baselineId + "\n" + purpose + "\n" + maxJournalBytes)
            };
            store.Save(record, "create-intent");
        }
        else if (record.BaseReference != baseReference || record.StorageMode != storageMode || record.BaselineId != baselineId || record.Purpose != purpose ||
            storageMode == "ntfs-snapshot" && record.Snapshot?.MaxJournalBytes != maxJournalBytes)
            throw new WorkspaceException("RequestKeyConflict", "This key already belongs to another request. Reuse its original parameters or choose a new task key.");
        if (record.Phase == "released") return new(record, null, null, store.ControlPath(id), "workspace list");
        try
        {
            if (storageMode == "ntfs-snapshot")
            {
                await EnsureSnapshotAsync(record, ct);
                if (editor) await StartCoreAsync(record, ct);
                return await ViewAsync(record, ct);
            }
            if (record.BaseRevision is null)
            {
                using var repositoryGate = await store.LockAsync(WorkspaceStore.ProjectKey(project), ct);
                SourceWorkspace created = await sources.CreateAsync(record, ct);
                record.BaseRevision = created.BaseRevision;
                record.CloneMetrics = created.CloneMetrics;
                record.Phase = "ready";
                record.LastError = null;
                store.Save(record, "created");
            }
            if (editor) await StartCoreAsync(record, ct);
            return await ViewAsync(record, ct);
        }
        catch (Exception ex)
        {
            RecordFailure(record, ex);
            if (ex is WorkspaceException { Code: "SourceCacheRevalidationRequired" } cacheFailure)
                throw new WorkspaceException(cacheFailure.Code, cacheFailure.Message, cacheFailure.Details,
                    [new("workspace.status", new { id, home = store.Root, auditSource = true })]);
            if (ex is WorkspaceException { Code: "SnapshotPrivilegeRequired", RecoveryActions: not null } failure)
                throw new WorkspaceException(failure.Code, failure.Message, failure.Details,
                    [.. failure.RecoveryActions, new("workspace.ensure", new { project, key = requestKey, baseReference, storage = storageMode,
                        baseline = baselineId, maxJournalBytes = record.Snapshot?.MaxJournalBytes, editor, home = store.Root })]);
            throw;
        }
    }

    public async Task<WorkspaceView> StatusAsync(string id, CancellationToken ct = default, bool verifySource = false)
    {
        using var gate = await store.LockAsync(id, ct);
        return await ViewAsync(store.Get(id), ct, verifySource);
    }

    public async Task<WorkspaceView> StartAsync(string id, CancellationToken ct = default)
    {
        using var gate = await store.LockAsync(id, ct);
        var record = store.Get(id);
        RequireLive(record);
        try { await StartCoreAsync(record, ct); return await ViewAsync(record, ct); }
        catch (Exception ex)
        {
            RecordFailure(record, ex);
            if (ex is WorkspaceException { Code: "SnapshotPrivilegeRequired", RecoveryActions: not null } failure)
                throw new WorkspaceException(failure.Code, failure.Message, failure.Details,
                    [.. failure.RecoveryActions, new("unity.start", new { id, home = store.Root })]);
            throw;
        }
    }

    private async Task StartCoreAsync(WorkspaceRecord record, CancellationToken ct)
    {
        RequireLive(record);
        if (record.StorageMode == "ntfs-snapshot")
        {
            if (!RequireSnapshot(record).Attached) throw new WorkspaceException("SnapshotNotReady", "Resume workspace ensure before starting Unity.");
            await ConnectSnapshotAsync(record, ct);
        }
        if (record.ResultRevision is not null)
            throw new WorkspaceException("ResultAlreadyCollected", "This workspace has a collected result. Use a new task key for further work.");
        var observed = await applications.ObserveAsync(record, store.ControlPath(record.Id), ct);
        if (observed.Running)
        {
            record.Application = observed.Lease ?? record.Application;
            return;
        }
        record.Phase = "editor-starting";
        store.Save(record, "editor-start-intent");
        record.Application = await applications.StartAsync(record, store.ControlPath(record.Id), ct);
        record.LastError = null;
        store.Save(record, "editor-started");
    }

    public async Task<WorkspaceView> RefreshAsync(string id, CancellationToken ct = default)
    {
        using var gate = await store.LockAsync(id, ct);
        var record = store.Get(id);
        RequireLive(record);
        if (record.ResultRevision is not null) throw new WorkspaceException("ResultAlreadyCollected", "Create a new workspace to edit a collected result.");
        var current = await ViewAsync(record, ct);
        if (current.Application?.Running != true || current.Application.Ready != true)
            throw new WorkspaceException("EditorNotReady", "Start the Editor and poll workspace status until application.ready is true.");
        string fingerprint = RequireSource(current).Fingerprint;
        if (record.PendingValidationId is not null && record.PendingFingerprint != fingerprint)
            throw new WorkspaceException("ValidationInProgress", "Source changed while validation is running. Wait for its result, then refresh again.");
        record.PendingValidationId ??= Guid.NewGuid().ToString("N");
        record.PendingFingerprint = fingerprint;
        record.Validation = null;
        record.Phase = "validating";
        store.Save(record, "validation-intent");
        try
        {
            await applications.RequestValidationAsync(record, store.ControlPath(id), record.PendingValidationId, fingerprint, ct);
            record.LastError = null;
            store.Save(record, "validation-requested");
        }
        catch (Exception ex) { RecordFailure(record, ex); throw; }
        return new(record, current.Source, current.Application, store.ControlPath(id), "workspace status --id " + id);
    }

    public async Task<WorkspaceView> CheckpointAsync(string id, string message, CancellationToken ct = default)
    {
        using var gate = await store.LockAsync(id, ct);
        var record = store.Get(id);
        RequireLive(record);
        var current = await ViewAsync(record, ct);
        RequireSource(current);
        if (record.ResultRevision is not null && current.Source!.Revision == record.ResultRevision && !current.Source.HasChanges)
        {
            if (record.StorageMode == "ntfs-snapshot" && RequireSnapshot(record).Result is not { CollectionProtocol: SnapshotResult.StableJournalProtocol })
            {
                try
                {
                    await CollectSnapshotResultAsync(record, ct);
                    record.Phase = "collected"; record.LastError = null;
                    store.Save(record, "checkpoint-completed");
                }
                catch (Exception ex) { RecordFailure(record, ex); throw; }
            }
            return current;
        }
        if (record.Validation is not { Success: true } validation || validation.Fingerprint != current.Source!.Fingerprint)
            throw new WorkspaceException("ValidationRequired", "Refresh and verify the current source before collecting a result.");
        if (string.IsNullOrWhiteSpace(message)) throw new WorkspaceException("CommitMessageRequired", "A result commit needs a nonempty message.");
        record.Phase = "collecting";
        store.Save(record, "checkpoint-intent");
        try
        {
            if (record.StorageMode == "ntfs-snapshot")
            {
                await applications.StopAsync(record, store.ControlPath(id), ct);
                await RequireStopped(record, ct);
            }
            record.ResultRevision = await sources.CommitAsync(record, message, ct);
            if (record.StorageMode == "ntfs-snapshot")
                await CollectSnapshotResultAsync(record, ct);
            record.Phase = "collected";
            record.LastError = null;
            store.Save(record, "checkpoint-completed");
            return await ViewAsync(record, ct);
        }
        catch (Exception ex) { RecordFailure(record, ex); throw; }
    }

    public async Task<WorkspaceView> IntegrateAsync(string id, string target, CancellationToken ct = default)
    {
        using var gate = await store.LockAsync(id, ct);
        var record = store.Get(id);
        RequireLive(record);
        var current = await ViewAsync(record, ct);
        RequireSource(current);
        if (record.ResultRevision is null || record.Validation is not { Success: true } validation ||
            current.Source!.HasChanges || validation.Fingerprint != current.Source.Fingerprint || current.Source.Revision != record.ResultRevision)
            throw new WorkspaceException("ResultNotReady", "Checkpoint validated source before integrating; later changes must be preserved.");
        record.Phase = "integrating";
        store.Save(record, "integration-intent");
        try
        {
            await applications.StopAsync(record, store.ControlPath(id), ct);
            using var repositoryGate = await store.LockAsync(WorkspaceStore.ProjectKey(record.ProjectPath), ct);
            var integrated = await sources.IntegrateAsync(record, target, ct);
            record.IntegratedRevision = integrated.Revision;
            record.Phase = "integrated";
            record.LastError = null;
            store.Save(record, "integrated");
            return await ViewAsync(record, ct);
        }
        catch (Exception ex) { RecordFailure(record, ex); throw; }
    }

    public async Task<WorkspaceView> StopAsync(string id, CancellationToken ct = default)
    {
        using var gate = await store.LockAsync(id, ct);
        var record = store.Get(id);
        RequireLive(record);
        await applications.StopAsync(record, store.ControlPath(id), ct);
        if (record.PendingValidationId is not null)
        {
            record.PendingValidationId = null;
            record.PendingFingerprint = null;
            record.Validation = null;
        }
        record.Phase = record.IntegratedRevision is not null ? "integrated" : record.ResultRevision is not null ? "collected" : "ready";
        store.Save(record, "editor-stopped");
        return await ViewAsync(record, ct);
    }

    public async Task<WorkspaceView> ReleaseAsync(string id, CancellationToken ct = default)
    {
        using var gate = await store.LockAsync(id, ct);
        var record = store.Get(id);
        if (record.Phase == "released") return new(record, null, null, store.ControlPath(id), "workspace list");
        if (record.StorageMode == "ntfs-snapshot") return await ReleaseSnapshotWorkspaceAsync(record, ct);
        // Git can unregister a worktree before failing to delete every file.
        // Let the source adapter validate and resume that partial removal.
        bool removalAlreadyAttempted = record.Phase == "releasing";
        if (!removalAlreadyAttempted)
        {
            var source = await sources.InspectAsync(record, ct);
            if (source.HasChanges || (record.ResultRevision is not null && record.IntegratedRevision is null))
                throw new WorkspaceException("UncollectedChanges", "Preserve this workspace: commit and integrate its source before release.");
        }
        record.Phase = "releasing";
        store.Save(record, "release-intent");
        try
        {
            await applications.StopAsync(record, store.ControlPath(id), ct);
            var stopped = await applications.ObserveAsync(record, store.ControlPath(id), ct);
            if (stopped.Running || stopped.Resources is not { OwnershipVerified: true, ActiveProcesses: 0, WorkingSetBytes: 0, PrivateBytes: 0 })
                throw new WorkspaceException("ProcessCleanupIncomplete", "Workspace deletion requires verified zero owned processes and memory.");
            record.ReleasedResources = stopped.Resources;
            if (Directory.Exists(record.Path)) await applications.RemoveInfrastructureAsync(record, store.ControlPath(id), ct);
            using var repositoryGate = await store.LockAsync(WorkspaceStore.ProjectKey(record.ProjectPath), ct);
            await sources.ReleaseAsync(record, ct);
            if (Directory.Exists(record.Path) || File.Exists(record.Path))
                throw new WorkspaceException("WorkspaceCleanupIncomplete", "The source provider left workspace files; retry release after inspecting the failure.");
            record.Phase = "released";
            record.LastError = null;
            store.Save(record, "released");
            return new(record, null, null, store.ControlPath(id), "workspace list");
        }
        catch (Exception ex) { RecordFailure(record, ex); throw; }
    }

    private async Task<WorkspaceView> ViewAsync(WorkspaceRecord record, CancellationToken ct, bool verifySource = true)
    {
        if (record.Phase == "released") return new(record, null, null, store.ControlPath(record.Id), "workspace list");
        if (record.StorageMode == "ntfs-snapshot")
        {
            var state = RequireSnapshot(record);
            state.Observation = await Snapshots.ObserveAsync(record, store.ControlPath(record.Id), ct);
            if (!state.Attached || state.Observation.SnapshotAvailable != true || !state.Observation.Mounted || !state.Observation.ProviderAlive || !state.Observation.OwnershipVerified ||
                state.Step.StartsWith("release-", StringComparison.Ordinal) || state.Step.StartsWith("compact-", StringComparison.Ordinal))
            {
                var unavailableApp = await applications.ObserveAsync(record, store.ControlPath(record.Id), ct);
                return new(record, null, unavailableApp, store.ControlPath(record.Id), state.Step.StartsWith("release-", StringComparison.Ordinal)
                    ? "workspace release" : state.Step.StartsWith("compact-", StringComparison.Ordinal) ? "workspace compact" : "workspace ensure");
            }
        }
        if (record.BaseRevision is null) return new(record, null, null, store.ControlPath(record.Id), "workspace ensure");
        var app = await applications.ObserveAsync(record, store.ControlPath(record.Id), ct);
        bool reconcileValidation = record.PendingValidationId is not null && app.Validation?.RequestId == record.PendingValidationId;
        // Poll process/provider state without reading project assets. A newly
        // completed validation still requires exactly one fresh fingerprint.
        var source = verifySource || reconcileValidation ? await sources.InspectAsync(record, ct) : null;
        bool changed = false;
        if (app.Lease is not null && app.Lease != record.Application) { record.Application = app.Lease; changed = true; }
        if (reconcileValidation)
        {
            record.Validation = app.Validation!.Fingerprint == record.PendingFingerprint && app.Validation.Fingerprint == source!.Fingerprint
                ? app.Validation : app.Validation with { Success = false, Errors = ["Source changed during validation; refresh again."] };
            record.PendingValidationId = null;
            record.PendingFingerprint = null;
            record.Phase = record.Validation.Success ? "validated" : "validation-failed";
            record.LastError = null;
            changed = true;
        }
        if (source is not null && record.Validation is { Success: true } validation && validation.Fingerprint != source.Fingerprint)
        {
            record.Validation = null;
            record.Phase = "source-changed";
            changed = true;
        }
        if (record.Phase == "editor-starting" && app.Ready) { record.Phase = "active"; changed = true; }
        if (changed) store.Save(record, "observed");
        string next = record.Purpose == "baseline" && record.Validation?.Success == true ? "baseline seal" :
            record.IntegratedRevision is not null ? "workspace release" : record.ResultRevision is not null ? "workspace integrate" :
            record.PendingValidationId is not null ? "workspace status" : record.Validation?.Success == true ? "workspace checkpoint" :
            app.Ready ? "unity refresh" : app.Running ? "workspace status" : "unity start";
        return new(record, source, app, store.ControlPath(record.Id), next + " --id " + record.Id);
    }

    private void RecordFailure(WorkspaceRecord record, Exception ex)
    {
        record.LastError = ex is WorkspaceException failure ? failure.Code + ": " + failure.Message : ex.Message;
        try { store.Save(record, "operation-failed"); }
        catch (WorkspaceException stateError) when (stateError.Code == "StateWriteIncomplete")
        {
            throw new WorkspaceException(stateError.Code, stateError.Message,
                new { operationError = (ex as WorkspaceException)?.Code ?? ex.GetType().Name, stateError.Details }, stateError.RecoveryActions);
        }
    }
    private static void RequireLive(WorkspaceRecord record)
    {
        if (record.Phase == "released") throw new WorkspaceException("WorkspaceReleased", "This task is released. Use a new request key.");
        if (record.BaseRevision is null) throw new WorkspaceException("WorkspaceNotReady", "Resume workspace ensure with the original request key.");
        if (record.Snapshot?.Step.StartsWith("compact-", StringComparison.Ordinal) == true)
            throw new WorkspaceException("WorkspaceCompacting", "Resume the same workspace compact request before other operations.");
    }
    private static SourceState RequireSource(WorkspaceView view) => view.Source ?? throw new WorkspaceException(
        view.Workspace.Snapshot?.Observation?.FailureCode ?? "WorkspaceNotReady",
        "The fixed source is not available. Preserve this workspace and resume its existing operation.");
    private static StringComparison PathComparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
