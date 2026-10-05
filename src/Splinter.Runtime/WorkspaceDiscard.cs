using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class WorkspaceCoordinator
{
    public async Task<WorkspaceView> DiscardAsync(WorkspaceDiscardRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.IntentKey) || request.IntentKey.Length > 128)
            throw new WorkspaceException("InvalidRequestKey", "Use a stable discard intent key between 1 and 128 characters.");
        using var gate = await store.LockAsync(request.Id, ct);
        var w = store.Get(request.Id);
        w.OriginalApply?.RequireReclaimable(w);
        if (w.RequestDigest is null || request.ExpectedRequestDigest != w.RequestDigest)
            throw new WorkspaceException("DiscardRequestConflict", "Confirm the workspace request digest before discarding this task.");
        if (w.Discard is { } prior && prior.Intent.IntentKey != request.IntentKey)
            throw new WorkspaceException("DiscardRequestConflict", "Resume the original discard intent; do not replace it.");
        if (w.Phase == "discarded") return new(w, null, null, store.ControlPath(w.Id), "workspace list");
        if (!WorkspaceStorage.HasRangeJournal(w.StorageMode) || w.Purpose != "task" || w.Phase == "released")
            throw new WorkspaceException("DiscardUnsupported", "Discard currently requires an attached NTFS task workspace.");
        var provider = discardProvider ?? throw new WorkspaceException("WorkspaceDiscardUnavailable", "A discard provider is not composed; preserve this task.");
        var state = RequireSnapshot(w);
        string control = store.ControlPath(w.Id);
        try
        {
            await RequireNoDiscardDependenciesAsync(w, ct);
            if (w.Discard is null)
            {
                if (!state.Attached || state.Reference is null || w.BaseRevision is null ||
                    state.Step.StartsWith("release-", StringComparison.Ordinal) || state.Step.StartsWith("compact-", StringComparison.Ordinal))
                    throw new WorkspaceException("DiscardUnsupported", "Resume preparation/compaction or release before requesting a new discard intent.");
                state.Observation = await Snapshots.ObserveAsync(w, control, ct);
                RequireSnapshotObservation(w, state.Observation);
                if (state.Observation is not { SnapshotAvailable: true, OwnershipVerified: true })
                    throw new WorkspaceException("DiscardOwnershipUnknown", "Verify the original snapshot and provider owner before discard.");
                string revision = await provider.ObserveSourceRevisionAsync(w, control, ct);
                if (string.IsNullOrWhiteSpace(revision)) throw new WorkspaceException("DiscardOwnershipUnknown", "The owned source revision is unknown.");
                w.Discard = new() { Intent = new(1, w.Id, request.IntentKey, w.RequestDigest, w.ProjectPath, w.Path,
                    w.SourceReference, w.BaseRevision, revision, state.Reference, DateTimeOffset.UtcNow) };
                DiscardStep(w, "stop");
            }
            var discard = w.Discard;
            discard.RequireMatch(w);
            if (discard.Step == "stop")
            {
                await applications.StopAsync(w, control, ct);
                await RequireStopped(w, ct);
                discard.Seal = await Snapshots.SealAsync(w, control, ct);
                DiscardStep(w, "unmount");
            }
            await RequireStopped(w, ct);
            if (discard.Step == "unmount")
            {
                await RequireDiscardSealAsync(w, ct);
                await Snapshots.UnmountAsync(w, control, ct);
                await RequireDiscardSealAsync(w, ct);
                DiscardStep(w, "source");
            }
            state.Observation = await Snapshots.ObserveAsync(w, control, ct);
            RequireDiscardStopped(state.Observation);
            if (Directory.Exists(w.Path) || File.Exists(w.Path))
                throw new WorkspaceException("WorkspaceCleanupIncomplete", "The owned mount root remains; preserve it before discard.");
            if (discard.Step == "source")
            {
                await RequireDiscardSealAsync(w, ct);
                using var repositoryGate = await store.LockAsync(WorkspaceStore.ProjectKey(w.ProjectPath), ct);
                await provider.DiscardSourceAsync(w, control, ct);
                DiscardStep(w, "delta");
            }
            if (discard.Step == "delta")
            {
                // A lost deletion response can leave only part of the journal.
                // The provider reconciles its durable intent instead of resealing it.
                await provider.DiscardDeltaAsync(w, control, ct);
                DiscardStep(w, "snapshot");
            }
            using (await store.LockAsync(WorkspaceStore.Key("snapshot:" + state.Reference!.Id), ct))
            {
                await RequireNoDiscardDependenciesAsync(w, ct);
                state.Observation = await Snapshots.ObserveAsync(w, control, ct);
                RequireDiscardStopped(state.Observation);
                if (state.Observation.DeltaPresent != false || Directory.Exists(w.Path) || File.Exists(w.Path))
                    throw new WorkspaceException("OverlayCleanupIncomplete", "Prove owned delta and mount absence before authorizing snapshot discard.");
                // The separate authority reads durable state, not this object.
                store.Save(w, "discard-snapshot-authorized");
                var reclamation = await provider.DiscardSnapshotAsync(w, control, ct);
                reclamation.RequireMatch(state.Reference);
                state.Observation = await Snapshots.ObserveAsync(w, control, ct);
                RequireDiscardStopped(state.Observation);
                if (state.Observation.DeltaPresent != false || Directory.Exists(w.Path) || File.Exists(w.Path))
                    throw new WorkspaceException("OverlayCleanupIncomplete", "Observe owned delta and mount absence before finishing discard.");
                state.Reclamation = reclamation;
                if (reclamation.Disposition == "deferred")
                {
                    w.Phase = "discard-reclaim-pending"; w.LastError = null;
                    store.Save(w, "discard-reclamation-deferred");
                    return new(w, null, null, control, "workspace discard");
                }
                if (state.Observation.SnapshotAvailable != false)
                    throw new WorkspaceException("SnapshotReleaseIncomplete", "Snapshot absence remains unknown; resume the same discard intent.");
                w.LastError = null;
                DiscardStep(w, "complete");
            }
            return new(w, null, null, control, "workspace list");
        }
        catch (Exception ex) { RecordFailure(w, ex); throw; }
    }

    private void DiscardStep(WorkspaceRecord w, string step)
    {
        w.Discard!.Step = step;
        w.Phase = step == "complete" ? "discarded" : "discarding";
        SnapshotStep(w, step == "complete" ? "discarded" : "discard-" + step);
    }
    private async Task RequireDiscardSealAsync(WorkspaceRecord w, CancellationToken ct)
    {
        if (await Snapshots.SealAsync(w, store.ControlPath(w.Id), ct) != w.Discard!.Seal)
            throw new WorkspaceException("DiscardDeltaChanged", "Writes changed the frozen discard scope; preserve the journal and intent.");
    }
    private static void RequireDiscardStopped(SnapshotWorkspaceObservation observation)
    {
        if (observation is not { Mounted: false, ProviderAlive: false, OwnershipVerified: true, OpenHandles: 0, WorkingSetBytes: 0, PrivateBytes: 0 })
            throw new WorkspaceException("OverlayCleanupIncomplete", "Discard requires verified zero owned provider, mount, handles and memory.");
    }
    private async Task RequireNoDiscardDependenciesAsync(WorkspaceRecord w, CancellationToken ct)
    {
        if (w.Snapshot?.Reference is null) return;
        var dependents = store.List().Where(other => other.Id != w.Id && other.Phase is not ("released" or "discarded") &&
            other.Snapshot?.Step != "release-snapshot" &&
            (other.Snapshot?.Step != "discard-snapshot" || other.Snapshot?.Observation?.DeltaPresent != false) &&
            other.Snapshot?.Reference?.Id == w.Snapshot.Reference.Id).ToArray();
        if (w.StorageMode != "project-snapshot" && dependents.Length != 0)
            throw new WorkspaceException("SnapshotInUse", "Another workspace depends on this snapshot; preserve it.",
                new { dependencies = dependents.Select(other => "workspace:" + other.Id).ToArray() },
                dependents.Select(other => new WorkspaceRecoveryAction("workspace.status", new { id = other.Id, home = store.Root }, true)).ToArray());
        try { await RequireNoValidationDependenciesAsync(w, ct); }
        catch (WorkspaceException ex) when (ex.Code == "SnapshotInUse")
        {
            throw new WorkspaceException(ex.Code, ex.Message, ex.Details,
                [new("capabilities", new { }, true), new("workspace.status", new { id = w.Id, home = store.Root }, true)]);
        }
    }
    private static void RequireNotDiscarding(WorkspaceRecord w)
    {
        if (w.Discard is not null)
            throw new WorkspaceException("WorkspaceDiscarding", "Resume the same discard intent; workspace editing, collection and release are blocked.");
    }
}
