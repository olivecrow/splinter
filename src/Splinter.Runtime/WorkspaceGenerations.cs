using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class WorkspaceCoordinator
{
    public async Task<WorkspaceView> CheckpointAndReleaseAsync(string id, string message, string requestKey, CancellationToken ct = default)
    {
        using var gate = await store.LockAsync(id, ct);
        var w = store.Get(id); RequireNotDiscarding(w); w.OriginalApply?.RequireReclaimable(w);
        RequireCaptureRequest(message, requestKey);
        var previous = w.Results.SingleOrDefault(r => r.RequestKey == requestKey);
        if (previous is not null && previous.Message != message)
            throw new WorkspaceException("SourceCaptureRequestConflict", "A capture request key already fixes another message.");
        if (w.Phase == "released")
        {
            if (previous is null) throw new WorkspaceException("WorkspaceReleased", "A released task cannot accept a new capture request.");
            await CaptureGenerationCoreAsync(w, message, requestKey, ct);
            return (await ViewAsync(w, ct, verifySource: false)) with { ResultGeneration = previous };
        }
        if (w.Snapshot?.Step.StartsWith("release-", StringComparison.Ordinal) == true)
        {
            if (previous is null) throw new WorkspaceException("SourceCaptureRequestConflict", "Resume the capture request which started final reclamation.");
            await CaptureGenerationCoreAsync(w, message, requestKey, ct);
            return (await ReleaseSnapshotWorkspaceAsync(w, ct)) with { ResultGeneration = previous };
        }
        RequireLive(w);
        try
        {
            await applications.StopAsync(w, store.ControlPath(id), ct); await RequireStopped(w, ct);
            await applications.RemoveInfrastructureAsync(w, store.ControlPath(id), ct);
            // A retry may return a capture fixed by a prior invocation. Only a
            // newly admitted request in this operation can share these seals.
            bool fresh = previous is null && w.CaptureIntent is null;
            var before = fresh ? await Snapshots.SealAsync(w, store.ControlPath(id), ct) : null;
            var generation = await CaptureGenerationCoreAsync(w, message, requestKey, ct);
            if (before is not null)
            {
                await CompleteGenerationSealAsync(w, generation, before, ct);
                return (await ReleaseSnapshotWorkspaceAsync(w, ct, RequireSnapshot(w).Result!)) with { ResultGeneration = generation };
            }
            return (await ReleaseSnapshotWorkspaceAsync(w, ct)) with { ResultGeneration = generation };
        }
        catch (Exception ex) { RecordFailure(w, ex); throw; }
    }
    public async Task<WorkspaceView> CheckpointAsync(string id, string message, string requestKey, CancellationToken ct = default)
    {
        using var gate = await store.LockAsync(id, ct);
        var w = store.Get(id);
        if (!w.Results.Any(r => r.RequestKey == requestKey)) RequireLive(w);
        try
        {
            var generation = await CaptureGenerationCoreAsync(w, message, requestKey, ct);
            return (await ViewAsync(w, ct, verifySource: false)) with { ResultGeneration = generation };
        }
        catch (Exception ex) { RecordFailure(w, ex); throw; }
    }
    private async Task<WorkspaceResultGeneration> CaptureGenerationCoreAsync(WorkspaceRecord w, string message, string requestKey, CancellationToken ct)
    {
        if (snapshots is not ISourceGenerationBackend backend || sources is not IWorkspaceGenerationSource provider)
            throw new WorkspaceException("SourceCaptureUnsupported", "This composition has no immutable continuing-task capture engine.");
        RequireCaptureRequest(message, requestKey);
        string control = store.ControlPath(w.Id);
        var previous = w.Results.SingleOrDefault(r => r.RequestKey == requestKey);
        if (previous is not null)
        {
            if (previous.Message != message) throw new WorkspaceException("SourceCaptureRequestConflict", "A capture request key already fixes another message.");
            await provider.VerifyGenerationAsync(w, control, previous, ct);
            await backend.CompleteSourceCaptureAsync(w, control, previous.Id, ct);
            return previous;
        }
        var intent = new WorkspaceCaptureIntent(WorkspaceStore.Key(w.Id + "\nresult\n" + requestKey), w.Results.Count + 1L, requestKey, message);
        if (w.CaptureIntent is not null && w.CaptureIntent != intent)
            throw new WorkspaceException("SourceCapturePending", "Resume the exact pending capture before choosing another generation.");
        w.CaptureIntent = intent; w.Phase = "capturing"; w.LastError = null;
        store.Save(w, "source-capture-intent");
        var captured = await backend.CaptureSourceAsync(w, control, intent.Id, ct);
        await PreparationPointAsync("source-capture-fixed", w, ct);
        WorkspaceResultGeneration result;
        using (var tree = await backend.OpenSourceCaptureAsync(w, control, intent.Id, ct))
        {
            result = await provider.PreserveGenerationAsync(w, control, intent, captured, tree, ct);
            tree.VerifyReadBoundary();
        }
        await PreparationPointAsync("source-result-preserved", w, ct);
        w.Results.Add(result); w.ResultRevision = result.Revision; w.CaptureIntent = null;
        RequireSnapshot(w).Result = null;
        w.Phase = "ready"; w.LastError = null;
        store.Save(w, "source-generation-published");
        await PreparationPointAsync("source-generation-published", w, ct);
        await backend.CompleteSourceCaptureAsync(w, control, intent.Id, ct);
        return result;
    }
    private static void RequireCaptureRequest(string message, string requestKey)
    {
        if (string.IsNullOrWhiteSpace(requestKey) || requestKey.Length > 128 || string.IsNullOrWhiteSpace(message))
            throw new WorkspaceException("SourceCaptureRequestInvalid", "Supply a nonempty stable request key (at most 128 characters) and message.");
    }
    private async Task SealGenerationForReleaseAsync(WorkspaceRecord w, CancellationToken ct)
    {
        await RequireStopped(w, ct);
        string control = store.ControlPath(w.Id);
        var before = await Snapshots.SealAsync(w, control, ct);
        var generation = await CaptureGenerationCoreAsync(w, "Final source preservation", "release:" + before.JournalHash, ct);
        await CompleteGenerationSealAsync(w, generation, before, ct);
    }
    private async Task CompleteGenerationSealAsync(WorkspaceRecord w, WorkspaceResultGeneration generation,
        SnapshotDeltaSeal before, CancellationToken ct)
    {
        string control = store.ControlPath(w.Id);
        var after = await Snapshots.SealAsync(w, control, ct);
        if (before != after) throw new WorkspaceException("DeltaChangedAfterCollection", "Writes overlapped final closure; retain every generation and the live journal.", new { before, after, generation.Id, generation.SourceSequence });
        RequireSnapshot(w).Result = new(RequireSnapshot(w).Reference!.Id, generation.Revision, generation.Fingerprint,
            generation.ResultReference, after, true, SnapshotResult.StableJournalProtocol);
        w.Phase = "collected";
        store.Save(w, "final-generation-sealed");
    }
    private async Task VerifyRetainedGenerationsAsync(WorkspaceRecord w, CancellationToken ct)
    {
        if (w.Results.Count == 0) return;
        if (sources is not IWorkspaceGenerationSource provider || snapshots is not ISourceGenerationBackend backend)
            throw new WorkspaceException("SourceCaptureUnsupported", "Retained generations require their exact preservation and capture composition.");
        string control = store.ControlPath(w.Id);
        foreach (var generation in w.Results)
        {
            await provider.VerifyGenerationAsync(w, control, generation, ct);
            await backend.CompleteSourceCaptureAsync(w, control, generation.Id, ct);
        }
    }
}
