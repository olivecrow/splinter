using System.Diagnostics;
using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class WorkspaceCoordinator
{
    private ISnapshotWorkspaceBackend Snapshots => snapshots ?? throw new WorkspaceException("SnapshotProviderMissing", "Compose the snapshot backend before requesting this storage mode.");
    private ISnapshotSourceWorkspaceProvider SnapshotSources => sources as ISnapshotSourceWorkspaceProvider
        ?? throw new WorkspaceException("SnapshotSourceUnsupported", "This source adapter cannot attach an immutable snapshot.");
    private static SnapshotWorkspaceState RequireSnapshot(WorkspaceRecord w) => w.Snapshot
        ?? throw new WorkspaceException("SnapshotStateMissing", "Preserve this workspace: its snapshot identity is missing.");
    private void SnapshotStep(WorkspaceRecord w, string step)
    { RequireSnapshot(w).Step = step; store.Save(w, step); }

    private async Task EnsureSnapshotAsync(WorkspaceRecord w, CancellationToken ct)
    {
        var state = RequireSnapshot(w);
        if (state.MaxJournalBytes is < 4096 or > 1099511627776) throw new WorkspaceException("InvalidJournalBudget", "Journal budget must be between 4096 bytes and 1 TiB.");
        if (state.Step.StartsWith("release-", StringComparison.Ordinal))
            throw new WorkspaceException("WorkspaceReleasing", "Resume workspace release; do not remount a collected workspace.");
        if (state.Step.StartsWith("compact-", StringComparison.Ordinal))
            throw new WorkspaceException("WorkspaceCompacting", "Resume the same workspace compact request before other operations.");
        string control = store.ControlPath(w.Id);
        if (w.BaseRevision is null)
        {
            using var sourceLock = await store.LockAsync(WorkspaceStore.ProjectKey(w.ProjectPath), ct);
            w.BaseRevision = await SnapshotSources.PreflightAsync(w, ct);
            SnapshotStep(w, "source-checked");
        }
        if (state.Reference is null)
        {
            SnapshotStep(w, "capture-intent");
            state.Reference = await Snapshots.CaptureAsync(w, control, ct);
            SnapshotStep(w, "captured");
        }
        if (!state.Attached)
        {
            using var sourceLock = await store.LockAsync(WorkspaceStore.ProjectKey(w.ProjectPath), ct);
            SnapshotStep(w, "registration-intent");
            await SnapshotSources.RegisterAsync(w, control, ct);
            SnapshotStep(w, "registered");
        }
        await ConnectSnapshotAsync(w, ct);
        if (!state.Attached)
        {
            SnapshotStep(w, "attach-intent");
            await SnapshotSources.AttachAsync(w, control, ct);
            var source = await sources.InspectAsync(w, ct);
            if (source.Revision != w.BaseRevision || source.HasChanges)
                throw new WorkspaceException("SnapshotCheckpointMismatch", "Captured files do not match the clean source checkpoint. Preserve the snapshot and delta.");
            state.Attached = true;
            w.Phase = "ready";
            SnapshotStep(w, "ready");
        }
        w.LastError = null;
        store.Save(w, "snapshot-ensured");
    }

    private async Task ConnectSnapshotAsync(WorkspaceRecord w, CancellationToken ct)
    {
        var state = RequireSnapshot(w);
        if (state.Reference is null || state.Step.StartsWith("release-", StringComparison.Ordinal))
            throw new WorkspaceException("SnapshotNotReady", "Resume ensure or the pending release first.");
        await Snapshots.VerifyConnectionAsync(w, store.ControlPath(w.Id), ct);
        var before = await Snapshots.ObserveAsync(w, store.ControlPath(w.Id), ct);
        state.Observation = before;
        RequireSnapshotObservation(w, before);
        if (before.SnapshotAvailable == false) throw new WorkspaceException("SnapshotMissing", "The original snapshot is unavailable. Preserve the delta; never recapture this key.",
            recoveryActions: [new("workspace.status", new { id = w.Id, home = store.Root }, true)]);
        if (!before.ProviderAlive)
        {
            var app = await applications.ObserveAsync(w, store.ControlPath(w.Id), ct);
            if (app.Running || app.Resources is not { OwnershipVerified: true, ActiveProcesses: 0 })
                throw new WorkspaceException("OverlayClientsRemain", "Stop the owned application before reconnecting its filesystem.");
        }
        state.Mount = await Snapshots.ConnectAsync(w, store.ControlPath(w.Id), ct);
        state.Observation = await Snapshots.ObserveAsync(w, store.ControlPath(w.Id), ct);
        if (state.Observation is not { SnapshotAvailable: true, Mounted: true, ProviderAlive: true, OwnershipVerified: true })
            throw new WorkspaceException("OverlayStartUncertain", "Verify the owned live mount before accessing source files.");
        store.Save(w, "mount-connected");
    }

    private async Task<WorkspaceView> ReleaseSnapshotWorkspaceAsync(WorkspaceRecord w, CancellationToken ct)
    {
        var state = RequireSnapshot(w);
        if (state.Step.StartsWith("compact-", StringComparison.Ordinal))
            throw new WorkspaceException("WorkspaceCompacting", "Resume workspace compact before release.");
        string control = store.ControlPath(w.Id);
        try
        {
            if (!state.Step.StartsWith("release-", StringComparison.Ordinal))
            {
                if (!state.Attached) throw new WorkspaceException("SnapshotPreparationIncomplete", "Preserve incomplete preparation and resume ensure before release.");
                await applications.StopAsync(w, control, ct);
                await RequireStopped(w, ct);
                state.Observation = await Snapshots.ObserveAsync(w, control, ct);
                RequireSnapshotObservation(w, state.Observation);
                if (state.Observation.SnapshotAvailable == false)
                {
                    if (state.Result is not { IndependentOfSnapshot: true })
                        throw new WorkspaceException("SnapshotMissing", "The snapshot is gone and no independent result exists. Preserve the delta.");
                    RequireStableCollection(state);
                    // A collected result can outlive its base. Do not inspect or
                    // mutate an unavailable tree: unmount first, then require
                    // the entire journal to equal the independently saved seal.
                    await SnapshotSources.VerifyResultAsync(w, control, ct);
                }
                else
                {
                    // Infrastructure removal legitimately appends to the journal.
                    // Complete it before the stable source-collection interval.
                    await applications.RemoveInfrastructureAsync(w, control, ct);
                    await CollectSnapshotResultAsync(w, ct);
                }
                w.Phase = "releasing";
                SnapshotStep(w, "release-unmount");
            }
            RequireStableCollection(state);
            await RequireStopped(w, ct);
            await SnapshotSources.VerifyResultAsync(w, control, ct);
            if (state.Step == "release-unmount")
            {
                await Snapshots.UnmountAsync(w, control, ct);
                var seal = await Snapshots.SealAsync(w, control, ct);
                if (seal != state.Result!.Seal)
                    throw new WorkspaceException("DeltaChangedAfterCollection", "Writes followed the collected result. Preserve the journal and reconcile before deletion.");
                SnapshotStep(w, "release-registration");
            }
            state.Observation = await Snapshots.ObserveAsync(w, control, ct);
            // At this final step the delta and Git registration are already
            // reclaimed. Let the exact release adapter refresh privileged
            // absence evidence; unknown still cannot pass the final gate.
            if (state.Step != "release-snapshot") RequireSnapshotObservation(w, state.Observation);
            if (state.Observation is not { Mounted: false, ProviderAlive: false, OwnershipVerified: true, OpenHandles: 0, WorkingSetBytes: 0, PrivateBytes: 0 })
                throw new WorkspaceException("OverlayCleanupIncomplete", "Owned provider, handles, mount and memory must be observed absent.");
            if (state.Step == "release-registration")
            {
                using var repositoryLock = await store.LockAsync(WorkspaceStore.ProjectKey(w.ProjectPath), ct);
                await SnapshotSources.UnregisterAsync(w, control, ct);
                SnapshotStep(w, "release-delta");
            }
            if (state.Step == "release-delta")
            {
                await Snapshots.RemoveDeltaAsync(w, control, ct);
                SnapshotStep(w, "release-snapshot");
            }
            using (await store.LockAsync(WorkspaceStore.Key("snapshot:" + state.Reference!.Id), ct))
            {
                if (store.List().Any(other => other.Id != w.Id && other.Phase != "released" && other.Snapshot?.Step != "release-snapshot" && other.Snapshot?.Reference?.Id == state.Reference.Id))
                    throw new WorkspaceException("SnapshotInUse", "Another workspace still depends on this immutable snapshot.");
                var reclamation = await Snapshots.ReleaseSnapshotAsync(w, control, ct);
                reclamation.RequireMatch(state.Reference);
                state.Observation = await Snapshots.ObserveAsync(w, control, ct);
                if (reclamation.Disposition == "deferred")
                {
                    if (state.Observation is not { DeltaPresent: false, Mounted: false, ProviderAlive: false,
                        OwnershipVerified: true, OpenHandles: 0, WorkingSetBytes: 0, PrivateBytes: 0 } ||
                        Directory.Exists(w.Path) || File.Exists(w.Path))
                        throw new WorkspaceException("OverlayCleanupIncomplete", "Deferred reclamation requires verified workspace resource cleanup.");
                    state.Reclamation = reclamation;
                    w.Phase = "reclaim-pending"; w.LastError = null;
                    store.Save(w, "snapshot-reclamation-deferred");
                    return new(w, null, null, control, "workspace release");
                }
                if (state.Observation is not { SnapshotAvailable: false, DeltaPresent: false, Mounted: false, ProviderAlive: false,
                    OwnershipVerified: true, OpenHandles: 0, WorkingSetBytes: 0, PrivateBytes: 0 })
                    throw new WorkspaceException("SnapshotReleaseIncomplete", "Verified snapshot, delta, mount and provider absence is required after release.");
                state.Observation = state.Observation with { FailureCode = null,
                    SnapshotStorage = state.Observation.SnapshotStorage is { } snapshotStorage ? snapshotStorage with { FailureCode = null } : null };
                if (Directory.Exists(w.Path) || File.Exists(w.Path))
                    throw new WorkspaceException("WorkspaceCleanupIncomplete", "Workspace path remains after unmount.");
                w.Phase = "released"; w.LastError = null;
                state.Reclamation = reclamation;
                SnapshotStep(w, "released");
            }
            return new(w, null, null, control, "workspace list");
        }
        catch (Exception ex)
        {
            RecordFailure(w, ex);
            if (ex is WorkspaceException { Code: "SnapshotPrivilegeRequired", RecoveryActions: not null } failure)
                throw new WorkspaceException(failure.Code, failure.Message, failure.Details,
                    [.. failure.RecoveryActions, new("workspace.release", new { id = w.Id, home = store.Root })]);
            throw;
        }
    }

    private void RequireSnapshotObservation(WorkspaceRecord w, SnapshotWorkspaceObservation observation)
    {
        if (observation.SnapshotAvailable is null)
            throw new WorkspaceException(observation.FailureCode ?? "SnapshotObservationUnknown", "Snapshot availability could not be observed. Preserve the existing identity and delta; restore access before resuming.",
                new { id = w.Id, observation.FailureCode },
                [new("workspace.status", new { id = w.Id, home = store.Root }, true)]);
    }

    private async Task CollectSnapshotResultAsync(WorkspaceRecord w, CancellationToken ct)
    {
        await RequireStopped(w, ct);
        string control = store.ControlPath(w.Id);
        var before = await Snapshots.SealAsync(w, control, ct);
        var source = await sources.InspectAsync(w, ct);
        if (source.HasChanges || w.ResultRevision is not null && source.Revision != w.ResultRevision)
            throw new WorkspaceException("UncollectedChanges", "Preserve and checkpoint the current source before collection.");
        // A clean untouched source may be preserved for cleanup without claiming
        // validation. No manifest is published until the full interval is stable.
        w.ResultRevision ??= source.Revision;
        string reference = await SnapshotSources.PreserveAsync(w, control, ct);
        var seal = await Snapshots.SealAsync(w, control, ct);
        if (seal != before)
            throw new WorkspaceException("DeltaChangedAfterCollection", "Writes overlapped result collection. Preserve the journal and recollect from a stable source before deletion.");
        RequireSnapshot(w).Result = new(RequireSnapshot(w).Reference!.Id, source.Revision, source.Fingerprint, reference, seal, true,
            SnapshotResult.StableJournalProtocol);
    }

    private static void RequireStableCollection(SnapshotWorkspaceState state)
    {
        if (state.Result is not { IndependentOfSnapshot: true, CollectionProtocol: SnapshotResult.StableJournalProtocol })
            throw new WorkspaceException("SnapshotResultRevalidationRequired", "This result predates stable journal collection. Preserve its resources; recollect while the original snapshot and mount are available before starting release.");
    }

    private async Task RequireStopped(WorkspaceRecord w, CancellationToken ct)
    {
        var app = await applications.ObserveAsync(w, store.ControlPath(w.Id), ct);
        if (app.Running || app.Resources is not { OwnershipVerified: true, ActiveProcesses: 0, WorkingSetBytes: 0, PrivateBytes: 0 })
            throw new WorkspaceException("ProcessCleanupIncomplete", "Verified zero owned application processes and memory are required.");
        w.ReleasedResources = app.Resources;
    }

    public async Task<WorkspaceView> CompactAsync(string id, CancellationToken ct = default,
        long? maxEstimatedJournalOverlapBytes = null)
    {
        if (maxEstimatedJournalOverlapBytes is <= 0)
            throw new WorkspaceException("InvalidCompactionBudget", "The estimated journal overlap budget must be positive.");
        using var gate = await store.LockAsync(id, ct);
        var w = store.Get(id);
        if (w.StorageMode != "ntfs-snapshot")
            throw new WorkspaceException("CompactionUnsupported", "Only an NTFS snapshot workspace has a range journal.");
        var state = RequireSnapshot(w);
        if (w.Phase is "released" or "reclaim-pending" || state.Step.StartsWith("release-", StringComparison.Ordinal) ||
            w.ResultRevision is not null || state.Result is not null || !state.Attached)
            throw new WorkspaceException("CompactionUnavailable", "Compact only an attached workspace before final result collection.");
        string control = store.ControlPath(id);
        try
        {
            if (!state.Step.StartsWith("compact-", StringComparison.Ordinal))
            {
                if (w.PendingValidationId is not null)
                    throw new WorkspaceException("ValidationInProgress", "Wait for the active validation before compacting.");
                await RequireStopped(w, ct);
                state.Observation = await Snapshots.ObserveAsync(w, control, ct);
                RequireSnapshotObservation(w, state.Observation);
                if (state.Observation is not { SnapshotAvailable: true, Mounted: true, ProviderAlive: true,
                    OwnershipVerified: true, OpenHandles: 0 })
                    throw new WorkspaceException("CompactionClientsRemain", "Verify the owned provider and close all mount handles before compaction.");
                // A repeated CLI request for the completed generation is a
                // read-only retry. New writes change the seal and allow a later
                // explicit compaction of the same workspace.
                if (state.Compaction is { } completed &&
                    await Snapshots.SealAsync(w, control, ct) == completed.After)
                    return await ViewAsync(w, ct);
                state.PendingCompactionMaxEstimatedJournalOverlapBytes = maxEstimatedJournalOverlapBytes;
                SnapshotStep(w, "compact-unmount");
            }
            else if (state.Step == "compact-unmount" || state.Step == "compact-write")
            {
                if (maxEstimatedJournalOverlapBytes is not null)
                {
                    state.PendingCompactionMaxEstimatedJournalOverlapBytes = maxEstimatedJournalOverlapBytes;
                    SnapshotStep(w, state.Step);
                }
            }
            if (state.Step == "compact-unmount")
            {
                await RequireStopped(w, ct);
                var before = await Snapshots.SealAsync(w, control, ct);
                await Snapshots.UnmountAsync(w, control, ct);
                if (await Snapshots.SealAsync(w, control, ct) != before)
                    throw new WorkspaceException("DeltaChangedAfterCollection", "Journal changed while stopping for compaction; preserve it.");
                state.Observation = await Snapshots.ObserveAsync(w, control, ct);
                if (state.Observation is not { SnapshotAvailable: true, Mounted: false, ProviderAlive: false,
                    OwnershipVerified: true, OpenHandles: 0, WorkingSetBytes: 0, PrivateBytes: 0 })
                    throw new WorkspaceException("CompactionProviderNotStopped", "The provider is not observed stopped; preserve the journal.");
                SnapshotStep(w, "compact-write");
            }
            if (state.Step == "compact-write")
            {
                await Snapshots.VerifyConnectionAsync(w, control, ct);
                state.Compaction = await Snapshots.CompactAsync(w, control, ct,
                    state.PendingCompactionMaxEstimatedJournalOverlapBytes);
                SnapshotStep(w, "compact-connect");
            }
            if (state.Step == "compact-connect")
            {
                long reconnectStarted = Stopwatch.GetTimestamp();
                await ConnectSnapshotAsync(w, ct);
                long reconnectTicks = Stopwatch.GetTimestamp() - reconnectStarted;
                long cleanupStarted = Stopwatch.GetTimestamp();
                await Snapshots.CompleteCompactionAsync(w, control, ct);
                long cleanupTicks = Stopwatch.GetTimestamp() - cleanupStarted;
                if (state.Compaction?.Cost is { } cost)
                    state.Compaction = state.Compaction with { Cost = cost with { Stages = [.. cost.Stages,
                        new("provider-reconnect", reconnectTicks,
                            state.Observation?.Storage?.Metrics?.ReplayBytes ?? 0, 0, 0),
                        new("previous-generation-cleanup", cleanupTicks, 0, 0, 0)] } };
                w.Validation = null; w.PendingValidationId = null; w.PendingFingerprint = null;
                state.PendingCompactionMaxEstimatedJournalOverlapBytes = null;
                w.Phase = "ready"; w.LastError = null;
                SnapshotStep(w, "ready");
            }
            return await ViewAsync(w, ct);
        }
        catch (Exception ex)
        {
            RecordFailure(w, ex);
            if (ex is WorkspaceException failure && WorkspaceFailurePolicy.RequiresPreservation(failure.Code))
                throw new WorkspaceException(failure.Code, failure.Message, failure.Details,
                    [new("workspace.status", new { id, home = store.Root }, true)]);
            throw;
        }
    }
}
