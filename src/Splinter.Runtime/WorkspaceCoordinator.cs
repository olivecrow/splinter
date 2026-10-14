using Splinter.Core;

namespace Splinter.Runtime;

public sealed record WorkspaceView(WorkspaceRecord Workspace, SourceState? Source, ApplicationState? Application,
    string ControlPath, string Command)
{
    public string NextCommand => Command == "workspace.list" ? "workspace list" :
        Command.Replace('.', ' ') + " --id " + Workspace.Id;
    public SnapshotRecoveryDecision? Recovery => WorkspaceStorage.HasRangeJournal(Workspace.StorageMode) ? SnapshotRecoveryDecision.For(Workspace) : null;
    public string SourceVerification => Source is null ? "not-checked" : "verified";
    public OriginalApplyPlan? OriginalApplyPlan { get; init; }
}

public sealed partial class WorkspaceCoordinator(WorkspaceStore store, ISourceWorkspaceProvider sources, IApplicationWorkspaceProvider applications,
    ISnapshotWorkspaceBackend? snapshots = null,
    IWorkspacePreparationObserver? preparationObserver = null, IWorkspaceDiscardProvider? discardProvider = null)
{
    public IReadOnlyList<WorkspaceRecord> List(string? project) => store.List(project is null ? null : WorkspaceStore.ManagedProjectPath(project));

    public async Task<WorkspaceView> EnsureAsync(string project, string requestKey, string baseReference, string storageMode,
        bool editor, CancellationToken ct = default,
        long maxJournalBytes = SnapshotWorkspaceState.DefaultJournalBytes, string sourceKind = "git", string applicationKind = "unity", string? generatedDirectoryId = null)
    {
        if (sourceKind is not ("git" or "none" or "plastic") || applicationKind is not ("unity" or "none"))
            throw new WorkspaceException("SourceSelectionUnsupported", "Select git, none or plastic and unity or none explicitly; no fallback is available.");
        if ((sourceKind == "none" && storageMode != "project-snapshot" || sourceKind == "plastic" && storageMode != "plastic-snapshot") && (storageMode != "ntfs-snapshot" || baseReference != "snapshot"))
            throw new WorkspaceException("SourceSelectionUnsupported", "Plain folders require an NTFS task and the snapshot basis.");
        if (storageMode == "git-snapshot" && sourceKind != "git")
            throw new WorkspaceException("SourceSelectionUnsupported", "git-snapshot requires a Git task with a fixed recorded commit.");
        if (storageMode == "plastic-snapshot" && (sourceKind != "plastic" || baseReference == "snapshot"))
            throw new WorkspaceException("SourceSelectionUnsupported", "plastic-snapshot requires an explicit recorded changeset and Plastic task.");
        if (storageMode == "project-snapshot" && (sourceKind != "none" || baseReference != "snapshot" || generatedDirectoryId is null))
            throw new WorkspaceException("ProjectBasisRequired", "project-snapshot requires source none, snapshot base and an exact prepared project basis.");
        if (editor && applicationKind == "none")
            throw new WorkspaceException("ApplicationUnsupported", "This ordinary folder has no selected application provider.");
        if (generatedDirectoryId is not null && (storageMode is not ("git-snapshot" or "plastic-snapshot" or "project-snapshot")))
            throw new WorkspaceException("GeneratedBasisUnsupported", "A generated directory reference requires a recorded VCS snapshot.");
        if (string.IsNullOrWhiteSpace(requestKey) || requestKey.Length > 128)
            throw new WorkspaceException("InvalidRequestKey", "Use a stable task key between 1 and 128 characters; reuse it across turns.");
        if (storageMode is not ("git-checkout" or "ntfs-snapshot" or "git-snapshot" or "plastic-snapshot" or "project-snapshot"))
            throw new WorkspaceException("UnsupportedStorageMode", "Choose git-snapshot, plastic-snapshot, project-snapshot, ntfs-snapshot, or explicit git-checkout for a diagnostic workspace.");
        project = storageMode == "project-snapshot" ? WorkspaceStore.ManagedProjectPath(project) : WorkspaceStore.CanonicalProject(project);
        if (!Directory.Exists(project)) throw new WorkspaceException("ProjectPathNotFound", "Project directory does not exist.");
        if (WorkspaceStore.SamePath(store.Root, project) || store.Root.StartsWith(project + Path.DirectorySeparatorChar, PathComparison))
            throw new WorkspaceException("UnsafeStateRoot", "State and workspaces must live outside the project being edited.");
        string id = WorkspaceStore.Key(WorkspaceStore.ProjectKey(project) + "\n" + requestKey);
        using var gate = await store.LockAsync(id, ct);
        var record = store.Find(id);
        if (record is null)
        {
            record = new WorkspaceRecord
            {
                Id = id, RequestKey = requestKey, ProjectPath = project, Path = store.WorkPath(id),
                SourceReference = "splinter/" + id, BaseReference = baseReference, ManagedPaths = applicationKind == "none" ? [] : applications.ManagedPaths,
                StorageMode = storageMode, Purpose = "task",
                SourceKind = sourceKind, ApplicationKind = applicationKind, GeneratedDirectoryId = generatedDirectoryId,
                Snapshot = WorkspaceStorage.HasRangeJournal(storageMode) ? new() { MaxJournalBytes = maxJournalBytes } : null,
                // Preserve the existing empty request slot so current snapshot identities remain unchanged.
                RequestDigest = WorkspaceStore.Key(project + "\n" + requestKey + "\n" + baseReference + "\n" + storageMode + "\n\n" + "task" + "\n" + maxJournalBytes + "\n" + sourceKind + "\n" + applicationKind + (generatedDirectoryId is null ? "" : "\n" + generatedDirectoryId))
            };
            await PreparationPointAsync("request.before-record", record, ct);
            store.Save(record, "create-intent");
            await PreparationPointAsync("request.after-record", record, ct);
        }
        else if (record.GeneratedDirectoryId != generatedDirectoryId || record.SourceKind != sourceKind || record.ApplicationKind != applicationKind || record.BaseReference != baseReference || record.StorageMode != storageMode || record.Purpose != "task" ||
            WorkspaceStorage.HasRangeJournal(storageMode) && record.Snapshot?.MaxJournalBytes != maxJournalBytes)
            throw new WorkspaceException("RequestKeyConflict", "This key already belongs to another request. Reuse its original parameters or choose a new task key.");
        if (record.Phase is "released" or "discarded") return new(record, null, null, store.ControlPath(id), "workspace.list");
        RequireNotDiscarding(record);
        record.OriginalApply?.RequireReclaimable(record);
        try
        {
            if (WorkspaceStorage.HasRangeJournal(storageMode))
            {
                var responseProof = await EnsureSnapshotAsync(record, ct);
                if (editor)
                {
                    await StartCoreAsync(record, ct);
                    // An Editor may legitimately create source .meta files.
                    // Keep the existing fresh (possibly dirty) response here.
                    responseProof = null;
                }
                var view = await ViewAsync(record, ct, inspectionProof: responseProof);
                await PreparationPointAsync("response.before-publish", record, ct);
                return view;
            }
            if (record.BaseRevision is null)
            {
                using var repositoryGate = await store.LockAsync(WorkspaceStore.ProjectKey(project), ct);
                SourceWorkspace created = await sources.CreateAsync(record, ct);
                record.BaseRevision = created.BaseRevision;
                record.Phase = "ready";
                record.LastError = null;
                store.Save(record, "created");
            }
            if (editor) await StartCoreAsync(record, ct);
            return await ViewAsync(record, ct, verifySource: !editor);
        }
        catch (Exception ex)
        {
            RecordFailure(record, ex);
            if (ex is WorkspaceException { Code: "SourceCacheRevalidationRequired" } cacheFailure)
                throw new WorkspaceException(cacheFailure.Code, cacheFailure.Message, cacheFailure.Details,
                    [new("workspace.status", new { id, home = store.Root, auditSource = true })]);
            if (ex is WorkspaceException { Code: "SnapshotPrivilegeRequired", RecoveryActions: not null } failure)
                throw new WorkspaceException(failure.Code, failure.Message, failure.Details,
                    [.. failure.RecoveryActions, WorkspaceActions.Ensure(record, store.Root, editor)]);
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
        // Starting an Editor observes liveness, not project correctness.
        try { await StartCoreAsync(record, ct); return await ViewAsync(record, ct, verifySource: false); }
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
        if (record.ApplicationKind == "none") throw new WorkspaceException("ApplicationUnsupported", "This ordinary folder has no selected application provider.");
        if (WorkspaceStorage.HasRangeJournal(record.StorageMode))
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

    public Task<WorkspaceView> CheckpointAsync(string id, string message, CancellationToken ct = default) =>
        CheckpointAsync(id, message, release: false, ct);

    public Task<WorkspaceView> CheckpointAndReleaseAsync(string id, string message, CancellationToken ct = default) =>
        CheckpointAsync(id, message, release: true, ct);

    private async Task<WorkspaceView> CheckpointAsync(string id, string message, bool release, CancellationToken ct)
    {
        using var gate = await store.LockAsync(id, ct);
        var record = store.Get(id);
        bool rangeJournal = WorkspaceStorage.HasRangeJournal(record.StorageMode);
        if (release)
        {
            RequireNotDiscarding(record);
            record.OriginalApply?.RequireReclaimable(record);
            if (!rangeJournal)
                throw new WorkspaceException("UnsupportedStorageMode", "Checkpoint with release requires a range-journal workspace.");
            if (record.Phase == "released") return new(record, null, null, store.ControlPath(id), "workspace.list");
            // A resumed release uses its durable result and existing recovery
            // guards. A prior invocation never supplies an in-memory seal proof.
            if (RequireSnapshot(record).Step.StartsWith("release-", StringComparison.Ordinal))
                return await ReleaseSnapshotWorkspaceAsync(record, ct);
        }
        RequireLive(record);
        // A first checkpoint is verified by the source adapter after application
        // shutdown. Only an existing result needs source inspection here to
        // decide whether this is the same checkpoint or a new generation.
        bool verifyInitialSource = !rangeJournal || record.ResultRevision is not null;
        var current = await ViewAsync(record, ct, verifyInitialSource);
        if (verifyInitialSource || current.Command is "workspace.ensure" or "workspace.release" or "workspace.compact")
            RequireSource(current);
        if (record.ResultRevision is not null && current.Source!.Revision == record.ResultRevision && !current.Source.HasChanges)
        {
            if (WorkspaceStorage.HasRangeJournal(record.StorageMode) && RequireSnapshot(record).Result is null)
            {
                try
                {
                    await CollectSnapshotResultAsync(record, ct);
                    record.Phase = "collected"; record.LastError = null;
                    store.Save(record, "checkpoint-completed");
                }
                catch (Exception ex) { RecordFailure(record, ex); throw; }
            }
            return release ? await ReleaseSnapshotWorkspaceAsync(record, ct) : current;
        }
        if (record.OriginalApply is not null)
            throw new WorkspaceException("OriginalApplyResultFixed", "Preserve the fixed applied result and later edits; use a new task for another apply generation.");
        if (string.IsNullOrWhiteSpace(message)) throw new WorkspaceException("CommitMessageRequired", "A source checkpoint needs a nonempty message.");
        record.Phase = "collecting";
        store.Save(record, "checkpoint-intent");
        try
        {
            if (WorkspaceStorage.HasRangeJournal(record.StorageMode))
            {
                await applications.StopAsync(record, store.ControlPath(id), ct);
                await RequireStopped(record, ct);
                await applications.RemoveInfrastructureAsync(record, store.ControlPath(id), ct);
            }
            if (rangeJournal && sources is ISnapshotCheckpointSourceProvider checkpointSource && checkpointSource.SupportsCheckpointAndPreserve(record))
                await CheckpointSnapshotResultAsync(record, checkpointSource, message, ct);
            else
            {
                record.ResultRevision = await sources.CheckpointAsync(record, message, ct);
                if (rangeJournal) await CollectSnapshotResultAsync(record, ct);
            }
            record.Phase = "collected";
            record.LastError = null;
            store.Save(record, "checkpoint-completed");
            // Collection already verified and sealed the independent result.
            // Observe resources for the response without another full scan;
            // Source remains null rather than claiming a later current-source
            // verification that this response did not perform.
            if (!release) return await ViewAsync(record, ct, verifySource: !rangeJournal);
        }
        catch (Exception ex) { RecordFailure(record, ex); throw; }
        return await ReleaseSnapshotWorkspaceAsync(record, ct, RequireSnapshot(record).Result!);
    }

    public async Task<WorkspaceView> IntegrateAsync(string id, string target, CancellationToken ct = default)
    {
        using var gate = await store.LockAsync(id, ct);
        var record = store.Get(id);
        if (record.SourceKind is "none" or "plastic")
            throw new WorkspaceException("SourceIntegrationUnsupported", "Plain-folder apply requires its separate plan, explicit intent and reviewed digest; a branch target alone cannot authorize it.");
        RequireLive(record);
        var current = await ViewAsync(record, ct);
        RequireSource(current);
        if (record.ResultRevision is null || current.Source!.HasChanges || current.Source.Revision != record.ResultRevision)
            throw new WorkspaceException("ResultNotReady", "Checkpoint the exact current source before integrating; later changes must be preserved.");
        if (WorkspaceStorage.HasRangeJournal(record.StorageMode))
        {
            var preserved = RequireSnapshot(record).Result;
            if (preserved is not { IndependentOfSnapshot: true, CollectionProtocol: SnapshotResult.StableJournalProtocol } ||
                preserved.Revision != current.Source.Revision || preserved.Fingerprint != current.Source.Fingerprint)
                throw new WorkspaceException("ResultNotReady", "Integrate only the exact independently preserved source.");
        }
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
        record.Phase = record.IntegratedRevision is not null ? "integrated" : record.ResultRevision is not null ? "collected" : "ready";
        store.Save(record, "editor-stopped");
        return await ViewAsync(record, ct);
    }

    public async Task<WorkspaceView> ReleaseAsync(string id, CancellationToken ct = default)
    {
        using var gate = await store.LockAsync(id, ct);
        var record = store.Get(id);
        RequireNotDiscarding(record);
        record.OriginalApply?.RequireReclaimable(record);
        if (record.Phase == "released") return new(record, null, null, store.ControlPath(id), "workspace.list");
        if (WorkspaceStorage.HasRangeJournal(record.StorageMode)) return await ReleaseSnapshotWorkspaceAsync(record, ct);
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
            return new(record, null, null, store.ControlPath(id), "workspace.list");
        }
        catch (Exception ex) { RecordFailure(record, ex); throw; }
    }

    private async Task<WorkspaceView> ViewAsync(WorkspaceRecord record, CancellationToken ct, bool verifySource = true,
        ISourceInspectionProof? inspectionProof = null)
    {
        if (record.Phase is "released" or "discarded") return new(record, null, null, store.ControlPath(record.Id), "workspace.list");
        if (record.Discard is not null)
        {
            record.Snapshot!.Observation = await Snapshots.ObserveAsync(record, store.ControlPath(record.Id), ct);
            return new(record, null, await applications.ObserveAsync(record, store.ControlPath(record.Id), ct),
                store.ControlPath(record.Id), "workspace discard");
        }
        if (WorkspaceStorage.HasRangeJournal(record.StorageMode))
        {
            var state = RequireSnapshot(record);
            state.Observation = await Snapshots.ObserveAsync(record, store.ControlPath(record.Id), ct);
            if (!state.Attached || state.Observation.SnapshotAvailable != true || !state.Observation.Mounted || !state.Observation.ProviderAlive || !state.Observation.OwnershipVerified ||
                state.Step.StartsWith("release-", StringComparison.Ordinal) || state.Step.StartsWith("compact-", StringComparison.Ordinal))
            {
                var unavailableApp = await applications.ObserveAsync(record, store.ControlPath(record.Id), ct);
                return new(record, null, unavailableApp, store.ControlPath(record.Id), state.Step.StartsWith("release-", StringComparison.Ordinal)
                    ? "workspace.release" : state.Step.StartsWith("compact-", StringComparison.Ordinal) ? "workspace.compact" : "workspace.ensure");
            }
        }
        if (record.BaseRevision is null) return new(record, null, null, store.ControlPath(record.Id), "workspace.ensure");
        var app = await applications.ObserveAsync(record, store.ControlPath(record.Id), ct);
        // Ordinary polls observe resources without reading project assets.
        var source = verifySource ? inspectionProof is null
            ? await sources.InspectAsync(record, ct) : await inspectionProof.RevalidateAsync(record, ct) : null;
        bool changed = false;
        if (app.Lease is not null && app.Lease != record.Application) { record.Application = app.Lease; changed = true; }
        if (record.Phase == "editor-starting" && app.Ready) { record.Phase = "active"; changed = true; }
        if (changed) store.Save(record, "observed");
        string next = record.Purpose == "baseline" && !app.Running ? "baseline.seal" :
            record.IntegratedRevision is not null ? "workspace.release" : record.ResultRevision is not null ? record.SourceKind is "none" or "plastic" ? "workspace.release" : "workspace.integrate" :
            app.Running && !app.Ready ? "workspace.status" : "workspace.checkpoint";
        return new(record, source, app, store.ControlPath(record.Id), next);
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
    private static void RequireLive(WorkspaceRecord record, bool allowOriginalApply = false)
    {
        RequireNotDiscarding(record);
        if (!allowOriginalApply) record.OriginalApply?.RequireReclaimable(record);
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
