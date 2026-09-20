using Splinter.Core;

namespace Splinter.Runtime;

public sealed record WorkspaceView(WorkspaceRecord Workspace, SourceState? Source, ApplicationState? Application,
    string ControlPath, string NextCommand);

public sealed class WorkspaceCoordinator(WorkspaceStore store, ISourceWorkspaceProvider sources, IApplicationWorkspaceProvider applications)
{
    public IReadOnlyList<WorkspaceRecord> List(string? project) => store.List(project is null ? null : WorkspaceStore.CanonicalProject(project));

    public async Task<WorkspaceView> EnsureAsync(string project, string requestKey, string baseReference, string storageMode,
        bool editor, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(requestKey) || requestKey.Length > 128)
            throw new WorkspaceException("InvalidRequestKey", "Use a stable task key between 1 and 128 characters; reuse it across turns.");
        if (storageMode != "git-checkout")
            throw new WorkspaceException("UnsupportedStorageMode", "This internal alpha supports explicit git-checkout only; ReFS CoW is not implemented.");
        project = WorkspaceStore.CanonicalProject(project);
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
                SourceReference = "splinter/" + id, BaseReference = baseReference, ManagedPaths = applications.ManagedPaths
            };
            store.Save(record, "create-intent");
        }
        else if (record.BaseReference != baseReference || record.StorageMode != storageMode)
            throw new WorkspaceException("RequestKeyConflict", "This key already belongs to another request. Reuse its original parameters or choose a new task key.");
        if (record.Phase == "released") return new(record, null, null, store.ControlPath(id), "workspace list");
        try
        {
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
            return await ViewAsync(record, ct);
        }
        catch (Exception ex) { RecordFailure(record, ex); throw; }
    }

    public async Task<WorkspaceView> StatusAsync(string id, CancellationToken ct = default)
    {
        using var gate = await store.LockAsync(id, ct);
        return await ViewAsync(store.Get(id), ct);
    }

    public async Task<WorkspaceView> StartAsync(string id, CancellationToken ct = default)
    {
        using var gate = await store.LockAsync(id, ct);
        var record = store.Get(id);
        RequireLive(record);
        try { await StartCoreAsync(record, ct); return await ViewAsync(record, ct); }
        catch (Exception ex) { RecordFailure(record, ex); throw; }
    }

    private async Task StartCoreAsync(WorkspaceRecord record, CancellationToken ct)
    {
        RequireLive(record);
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
        string fingerprint = current.Source!.Fingerprint;
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
        if (record.ResultRevision is not null && current.Source!.Revision == record.ResultRevision && !current.Source.HasChanges)
            return current;
        if (record.Validation is not { Success: true } validation || validation.Fingerprint != current.Source!.Fingerprint)
            throw new WorkspaceException("ValidationRequired", "Refresh and verify the current source before collecting a result.");
        if (string.IsNullOrWhiteSpace(message)) throw new WorkspaceException("CommitMessageRequired", "A result commit needs a nonempty message.");
        record.Phase = "collecting";
        store.Save(record, "checkpoint-intent");
        try
        {
            record.ResultRevision = await sources.CommitAsync(record, message, ct);
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
            if (Directory.Exists(record.Path)) await applications.RemoveInfrastructureAsync(record, store.ControlPath(id), ct);
            using var repositoryGate = await store.LockAsync(WorkspaceStore.ProjectKey(record.ProjectPath), ct);
            await sources.ReleaseAsync(record, ct);
            record.Phase = "released";
            record.LastError = null;
            store.Save(record, "released");
            return new(record, null, null, store.ControlPath(id), "workspace list");
        }
        catch (Exception ex) { RecordFailure(record, ex); throw; }
    }

    private async Task<WorkspaceView> ViewAsync(WorkspaceRecord record, CancellationToken ct)
    {
        if (record.Phase == "released") return new(record, null, null, store.ControlPath(record.Id), "workspace list");
        if (record.BaseRevision is null) return new(record, null, null, store.ControlPath(record.Id), "workspace ensure");
        var source = await sources.InspectAsync(record, ct);
        var app = await applications.ObserveAsync(record, store.ControlPath(record.Id), ct);
        bool changed = false;
        if (app.Lease is not null && app.Lease != record.Application) { record.Application = app.Lease; changed = true; }
        if (record.PendingValidationId is not null && app.Validation?.RequestId == record.PendingValidationId)
        {
            record.Validation = app.Validation.Fingerprint == record.PendingFingerprint && app.Validation.Fingerprint == source.Fingerprint
                ? app.Validation : app.Validation with { Success = false, Errors = ["Source changed during validation; refresh again."] };
            record.PendingValidationId = null;
            record.PendingFingerprint = null;
            record.Phase = record.Validation.Success ? "validated" : "validation-failed";
            record.LastError = null;
            changed = true;
        }
        if (record.Validation is { Success: true } validation && validation.Fingerprint != source.Fingerprint)
        {
            record.Validation = null;
            record.Phase = "source-changed";
            changed = true;
        }
        if (record.Phase == "editor-starting" && app.Ready) { record.Phase = "active"; changed = true; }
        if (changed) store.Save(record, "observed");
        string next = record.IntegratedRevision is not null ? "workspace release" : record.ResultRevision is not null ? "workspace integrate" :
            record.PendingValidationId is not null ? "workspace status" : record.Validation?.Success == true ? "workspace checkpoint" :
            app.Ready ? "unity refresh" : app.Running ? "workspace status" : "unity start";
        return new(record, source, app, store.ControlPath(record.Id), next + " --id " + record.Id);
    }

    private void RecordFailure(WorkspaceRecord record, Exception ex)
    {
        record.LastError = ex is WorkspaceException failure ? failure.Code + ": " + failure.Message : ex.Message;
        store.Save(record, "operation-failed");
    }
    private static void RequireLive(WorkspaceRecord record)
    {
        if (record.Phase == "released") throw new WorkspaceException("WorkspaceReleased", "This task is released. Use a new request key.");
        if (record.BaseRevision is null) throw new WorkspaceException("WorkspaceNotReady", "Resume workspace ensure with the original request key.");
    }
    private static StringComparison PathComparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
