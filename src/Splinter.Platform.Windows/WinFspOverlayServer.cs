using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fsp;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Platform.Windows;

public sealed record OverlayServeRequest(OverlayIdentity Identity, string DeltaPath, string MountPath,
    string ControlPath, string Token, [property: JsonRequired] SnapshotReadAccess ReadAccess,
    [property: JsonRequired] string SnapshotReceiptDirectory, [property: JsonRequired] string SnapshotOwnerId,
    SourceViewSelection? SourceView = null, OverlayCacheSnapshotRead? CacheSnapshot = null, OverlayCacheSnapshotRead? GeneratedSnapshot = null);

public sealed record OverlayCacheSnapshotRead(SnapshotReadAccess ReadAccess, string ReceiptDirectory, string OwnerId);

public static class WinFspOverlayServer
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    public static async Task<int> RunAsync(string requestPath, string generation)
    {
        OverlayServeRequest request = JsonSerializer.Deserialize<OverlayServeRequest>(await File.ReadAllTextAsync(requestPath), Json)
            ?? throw new ArgumentException("Missing overlay request.");
        WindowsSnapshotPolicy.Require(request);
        WindowsProcessWorkspaceProvider.CheckPath(request.ControlPath);
        Directory.CreateDirectory(request.ControlPath);
        string statePath = Path.Combine(request.ControlPath, "state.json");
        int failures = 0;
        string? lastFailureCode = null;
        using var self = Process.GetCurrentProcess();
        bool launchOwned = false;
        try
        {
            // The child publishes its own identity before creating a delta or
            // mounting. A caller dying immediately after CreateProcess cannot
            // strand a live owned provider without a launch receipt.
            var intent = await WindowsProcessWorkspaceProvider.ReadAsync<WinFspOverlayClient.ProviderIntent>(Path.Combine(request.ControlPath, "provider-launch-intent.json"))
                ?? throw new WorkspaceException("OverlayStartUncertain", "A durable launch intent is required.");
            if (intent.Token != request.Token || intent.Id != request.Identity.Id || intent.Generation != generation ||
                string.IsNullOrWhiteSpace(generation) || string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.Identity.Id))
                throw new WorkspaceException("OverlayOwnerMismatch", "Provider generation does not match the exact launch intent.");
            launchOwned = true;
            await WindowsProcessWorkspaceProvider.WriteAsync(Path.Combine(request.ControlPath, "provider-launch-receipt.json"),
                new WinFspOverlayClient.ProviderLaunch(request.Token, self.Id, self.StartTime.ToUniversalTime().Ticks, generation));
            string mountPath = Path.GetFullPath(request.MountPath), deltaPath = Path.GetFullPath(request.DeltaPath);
            foreach (string other in new[] { deltaPath, request.ControlPath, request.Identity.Snapshot.SourcePath })
            {
                string normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(other));
                if (mountPath.Equals(normalized, StringComparison.OrdinalIgnoreCase) ||
                    mountPath.StartsWith(normalized + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                    normalized.StartsWith(mountPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new WorkspaceException("UnsafePath", "Mount, delta, control and mutable source must be separate trees.");
            }
            WindowsProcessWorkspaceProvider.CheckPath(mountPath); WindowsProcessWorkspaceProvider.CheckPath(deltaPath);
            if (Directory.Exists(mountPath) && Directory.EnumerateFileSystemEntries(mountPath).Any())
                throw new WorkspaceException("MountPathNotEmpty", "A mount must never hide an existing tree.");
            // WinFsp creates the mount directory itself. Passing an existing
            // empty directory returns STATUS_OBJECT_NAME_COLLISION.
            if (Directory.Exists(mountPath)) Directory.Delete(mountPath, false);
            using var snapshotReadLease = WorkspaceSnapshotBasis.AcquireReadLease(request.Identity.Snapshot, request.SnapshotReceiptDirectory, request.SnapshotOwnerId);
            WinFspOverlayClient.ValidateCacheSnapshot(request);
            using var cacheReadLease = request.CacheSnapshot is { } cacheRead
                ? WorkspaceSnapshotBasis.AcquireReadLease(request.SourceView!.CacheIdentity!.Snapshot, cacheRead.ReceiptDirectory, cacheRead.OwnerId) : null;
            SnapshotObservation health = await WinFspOverlayClient.ObserveSnapshotAsync(request);
            if (health.Available != true) throw new WorkspaceException(health.FailureCode ?? "SnapshotObservationUnknown", "Snapshot read access is not verified.");
            using var snapshot = WorkspaceSnapshotBasis.OpenTree(request.Identity.Snapshot, request.ReadAccess,
                request.SnapshotReceiptDirectory, request.SnapshotOwnerId, () => Volatile.Read(ref health));
            using var generatedLease = request.Identity.Graft is { } graftLease
                ? WorkspaceSnapshotBasis.AcquireReadLease(graftLease.Basis.Snapshot, graftLease.Basis.ReceiptDirectory, graftLease.Basis.OwnerId) : null;
            using var generatedTree = request.Identity.Graft is { } graftTree
                ? WorkspaceSnapshotBasis.OpenTree(graftTree.Basis.Snapshot, request.GeneratedSnapshot!.ReadAccess, graftTree.Basis.ReceiptDirectory, graftTree.Basis.OwnerId) : null;
            ISnapshotTree basisTree = request.Identity.Graft is { } graft
                ? new GraftedSnapshotTree(snapshot, generatedTree!, graft) : snapshot;
            using var cacheSnapshot = request.CacheSnapshot is { } cacheAccess
                ? WorkspaceSnapshotBasis.OpenTree(request.SourceView!.CacheIdentity!.Snapshot, cacheAccess.ReadAccess, cacheAccess.ReceiptDirectory, cacheAccess.OwnerId, () => Volatile.Read(ref health)) : null;
            if (request.SourceView is { } selection)
            {
                foreach (string path in new[] { selection.CandidatePath, selection.CacheDeltaPath })
                {
                    WindowsProcessWorkspaceProvider.CheckPath(path);
                    foreach (string other in new[] { mountPath, deltaPath, request.ControlPath, request.Identity.Snapshot.SourcePath })
                    {
                        string a = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
                        string b = Path.TrimEndingDirectorySeparator(Path.GetFullPath(other));
                        if (a.Equals(b, StringComparison.OrdinalIgnoreCase) || a.StartsWith(b + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                            b.StartsWith(a + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                            throw new WorkspaceException("UnsafePath", "Candidate, cache, source, mount and control trees must be separate.");
                    }
                }
            }
            await WindowsProcessWorkspaceProvider.WriteAsync(statePath, new
            {
                version = 1, request.Token, request.Identity.Id, phase = "replaying", observedAt = DateTimeOffset.UtcNow,
                snapshotId = request.Identity.Snapshot.Id, mountPath, deltaPath,
                processId = self.Id, processStartTimeUtcTicks = self.StartTime.ToUniversalTime().Ticks
            });
            long replayStarted = Stopwatch.GetTimestamp();
            var sourcePins = new WindowsJournalFilePins();
            string sourceCaptures = Path.Combine(Path.GetDirectoryName(request.ControlPath)!, "captures");
            if (request.Identity.Version < 3) SourceCaptureTree.RecoverPins(sourceCaptures, sourcePins);
            WindowsJournalFilePins.RecoverCopies(Path.GetDirectoryName(request.ControlPath)!);
            using var capturedContent = CapturedResultReaders.Open(request.Identity, deltaPath);
            using IWorkspaceDeltaStore store = request.SourceView is { } selectedView
                ? new SourceViewDeltaStore(deltaPath, request.Identity, snapshot, selectedView, cacheBasis: cacheSnapshot)
                : new JournalDeltaStore(deltaPath, request.Identity, basisTree, directoryGuardFactory: path => new WindowsJournalDirectoryGuard(path),
                    initialContentGeneration: intent.InitialContentGeneration,
                    boundedLogBytes: JournalDeltaStore.DefaultBoundedLogBytes, sourceFilePins: sourcePins, capturedContentReader: capturedContent);
            if (store is JournalDeltaStore recovered)
            { recovered.RecoverSourceCaptures(sourceCaptures); recovered.MaintainLiveStorage(); }
            long storeReadyTicks = Stopwatch.GetTimestamp() - replayStarted;
            var fileSystem = new WinFspOverlayFileSystem(store, ex =>
            {
                int count = Interlocked.Increment(ref failures);
                if (ex is WorkspaceException failure) Volatile.Write(ref lastFailureCode, failure.Code);
                if (count <= 100)
                    lock (Json) File.AppendAllText(Path.Combine(request.ControlPath, "io-errors.jsonl"),
                        JsonSerializer.Serialize(new { at = DateTimeOffset.UtcNow, type = ex.GetType().Name, code = (ex as WorkspaceException)?.Code, ex.Message }, Json) + "\n");
            }, null, Environment.GetEnvironmentVariable("SPLINTER_WRITE_DIAGNOSTICS") == "1"
                ? new WinFspOverlayFileSystem.WriteTelemetry() : null);
            using (var host = new FileSystemHost(fileSystem))
            {
                using var wakeup = new OverlayRequestWakeup(request.ControlPath);
                using var maintenanceCancellation = new CancellationTokenSource();
                Task? maintenance = null; int pendingSegmentCollection = 0; Exception? maintenanceFailure = null;
                void ScheduleMaintenance(bool collectSegments = false)
                {
                    if (maintenanceCancellation.IsCancellationRequested) return;
                    if (store is not JournalDeltaStore maintained || Volatile.Read(ref maintenanceFailure) is not null) return;
                    if (collectSegments) Interlocked.Exchange(ref pendingSegmentCollection, 1);
                    if (maintenance is { IsCompleted: false }) return;
                    maintenance = Task.Run(() =>
                    {
                        try
                        {
                            do { maintained.MaintainLiveStorage(ct: maintenanceCancellation.Token,
                                collectSegments: Interlocked.Exchange(ref pendingSegmentCollection, 0) != 0); }
                            while (Volatile.Read(ref pendingSegmentCollection) != 0);
                        }
                        catch (OperationCanceledException) when (maintenanceCancellation.IsCancellationRequested) { }
                        catch (Exception error)
                        {
                            Volatile.Write(ref maintenanceFailure, error); Interlocked.Increment(ref failures);
                            Volatile.Write(ref lastFailureCode, (error as WorkspaceException)?.Code ?? error.GetType().Name);
                        }
                    });
                }
                long nextObservation = 0, nextStorageMaintenance = Stopwatch.GetTimestamp() + 30 * Stopwatch.Frequency;
                string? lastFlushRequest = null, lastSourceRequest = null, lastCaptureRequest = null, lastCompletionRequest = null;
                int status = host.MountEx(mountPath, 8, null, false, 0);
                if (status < 0) throw new WorkspaceException("OverlayMountFailed", $"WinFsp mount returned 0x{status:x8}.");
                try
                {
                while (true)
                {
                    try
                    {
                        if (Stopwatch.GetTimestamp() >= nextObservation)
                        {
                            self.Refresh();
                            var snapshotObservation = await WinFspOverlayClient.ObserveSnapshotAsync(request,
                                openedSnapshot: snapshot, openedCache: cacheSnapshot, openedGenerated: generatedTree);
                            Volatile.Write(ref health, snapshotObservation);
                            var storage = store.ObserveActivity();
                            if (storage is null) Volatile.Write(ref lastFailureCode, "DeltaReconnectRequired");
                            await WindowsProcessWorkspaceProvider.WriteAsync(statePath, new
                            {
                                version = 1, request.Token, request.Identity.Id,
                                phase = snapshotObservation.Available switch { true => "mounted", false => "snapshot-missing", null => "snapshot-unknown" },
                                snapshotAvailability = snapshotObservation.Availability, snapshotFailureCode = snapshotObservation.FailureCode,
                                observedAt = DateTimeOffset.UtcNow,
                                snapshotId = request.Identity.Snapshot.Id, mountPath, deltaPath,
                                processId = self.Id, processStartTimeUtcTicks = self.StartTime.ToUniversalTime().Ticks,
                                openHandles = fileSystem.OpenHandles, retainedFileContexts = fileSystem.RetainedFileContexts,
                                workingSetBytes = self.WorkingSet64, privateBytes = self.PrivateMemorySize64,
                                ioFailures = Volatile.Read(ref failures), lastFailureCode = Volatile.Read(ref lastFailureCode), storage,
                                sourceView = (store as SourceViewDeltaStore)?.ObservePartitions(),
                                writeAdmission = fileSystem.ObserveWriteAdmission(),
                                gitReads = snapshot.ObserveGitReads(),
                                preparation = new { storeReadyTicks, stopwatchFrequency = Stopwatch.Frequency }
                            });
                            nextObservation = Stopwatch.GetTimestamp() + Stopwatch.Frequency;
                        }
                        var stop = await WindowsProcessWorkspaceProvider.ReadAsync<StopRequest>(Path.Combine(request.ControlPath, "stop.json"), waitForPublication: false);
                        if (stop?.Token == request.Token)
                        {
                            if (fileSystem.OpenHandles == 0)
                            {
                                maintenanceCancellation.Cancel();
                                if (maintenance is not null) await maintenance;
                                try { store.Flush(); }
                                catch (WorkspaceException failed) when (failed.Code == "DeltaReconnectRequired")
                                {
                                    // There are no client handles to abandon.
                                    // Preserve the failed generation and stop
                                    // for exact replay; no sealed result or
                                    // successful flush is claimed here.
                                    Volatile.Write(ref lastFailureCode, failed.Code);
                                }
                                break;
                            }
                            // A normal stop is deferred while clients own handles.
                            // Throwing here would dispose the host and forcibly unmount it.
                            await WindowsProcessWorkspaceProvider.WriteAsync(Path.Combine(request.ControlPath, "stop-blocked.json"),
                                new { request.Token, error = "OverlayHandlesRemain", openHandles = fileSystem.OpenHandles, observedAt = DateTimeOffset.UtcNow });
                        }
                        if (Stopwatch.GetTimestamp() >= nextStorageMaintenance)
                        {
                            ScheduleMaintenance();
                            nextStorageMaintenance = Stopwatch.GetTimestamp() + 30 * Stopwatch.Frequency;
                        }
                        var flush = await WindowsProcessWorkspaceProvider.ReadAsync<OverlayFlushRequest>(Path.Combine(request.ControlPath, "flush.json"), waitForPublication: false);
                        if (flush?.Token == request.Token && flush.RequestId != lastFlushRequest)
                        {
                            var receipt = ProcessFlush(store, flush, self.Id, self.StartTime.ToUniversalTime().Ticks);
                            await WindowsProcessWorkspaceProvider.WriteAsync(Path.Combine(request.ControlPath, "flush-result.json"), receipt);
                            lastFlushRequest = flush.RequestId;
                        }
                        var captureRequest = await WindowsProcessWorkspaceProvider.ReadAsync<CaptureSourceRequest>(Path.Combine(request.ControlPath, "capture-source.json"), waitForPublication: false);
                        if (captureRequest?.Token == request.Token && captureRequest.RequestId != lastCaptureRequest)
                        {
                            JournalSourceCapture? capture = null; string? captureError = null;
                            try
                            {
                                if (captureRequest.Id.Length != 24 || captureRequest.Id.Any(c => !char.IsAsciiHexDigit(c)) || store is not JournalDeltaStore live)
                                    throw new WorkspaceException("SourceCaptureRequestInvalid", "Require an owned task and an exact generation ID.");
                                capture = fileSystem.CaptureSource(Path.Combine(sourceCaptures, captureRequest.Id), captureRequest.Id, captureRequest.ManagedPaths);
                                string costPath = Path.Combine(sourceCaptures, captureRequest.Id, "capture-cost.json");
                                if (!File.Exists(costPath)) SourceCaptureTree.Publish(costPath, new { store = live.LastSourceCaptureTiming, callbacks = fileSystem.LastSourceCaptureCallbacks });
                            }
                            catch (Exception ex) { captureError = (ex as WorkspaceException)?.Code ?? ex.GetType().Name; }
                            await WindowsProcessWorkspaceProvider.WriteAsync(Path.Combine(request.ControlPath, "capture-source-result.json"),
                                new { request.Token, captureRequest.RequestId, captureRequest.Id, captureError, capture?.Digest, capture?.Sequence,
                                    processId = self.Id, processStartTimeUtcTicks = self.StartTime.ToUniversalTime().Ticks });
                            lastCaptureRequest = captureRequest.RequestId;
                        }
                        var completion = await WindowsProcessWorkspaceProvider.ReadAsync<CompleteSourceRequest>(Path.Combine(request.ControlPath, "complete-source.json"), waitForPublication: false);
                        if (completion?.Token == request.Token && completion.RequestId != lastCompletionRequest)
                        {
                            string? completionError = null;
                            try
                            {
                                if (completion.Id.Length != 24 || completion.Id.Any(c => !char.IsAsciiHexDigit(c)))
                                    throw new WorkspaceException("SourceCaptureRequestInvalid", "Require the fixed generation identity.");
                                string control = Path.GetDirectoryName(request.ControlPath)!;
                                await RunStopAwareAsync(request.ControlPath, request.Token, ct => fileSystem.AdoptCapturedRanges(control, completion.Id, ct));
                                SourceCaptureTree.Complete(Path.Combine(sourceCaptures, completion.Id), sourcePins);
                                if (store is JournalDeltaStore completedRequest)
                                {
                                    completedRequest.CompleteSourceCaptureRequest(completion.Id);
                                    if (completion.PrepareForRelease)
                                        await RunStopAwareAsync(request.ControlPath, request.Token, ct => completedRequest.MaintainLiveStorage(ct: ct, resumeOnly: true));
                                    else ScheduleMaintenance(collectSegments: true);
                                }
                            }
                            catch (Exception ex) { completionError = (ex as WorkspaceException)?.Code ?? ex.GetType().Name; }
                            await WindowsProcessWorkspaceProvider.WriteAsync(Path.Combine(request.ControlPath, "complete-source-result.json"),
                                new { request.Token, completion.RequestId, completion.Id, completionError,
                                    processId = self.Id, processStartTimeUtcTicks = self.StartTime.ToUniversalTime().Ticks });
                            lastCompletionRequest = completion.RequestId;
                        }
                        var sourceQuery = await WindowsProcessWorkspaceProvider.ReadAsync<SourceChangeRequest>(Path.Combine(request.ControlPath, "source-query.json"), waitForPublication: false);
                        if (sourceQuery?.Token == request.Token && sourceQuery.RequestId != lastSourceRequest)
                        {
                            SourceChangeView? view = null;
                            SourceContentSummaryView? initialContent = null;
                            string? sourceError = null;
                            try
                            {
                                if ((sourceQuery.ExpectedGeneration is null) != (sourceQuery.ContentPaths is null))
                                    throw new WorkspaceException("SourceObservationInvalid", "Initial evidence requires both paths and a fixed generation.");
                                if (sourceQuery.ExpectedGeneration is { } expected && store is JournalDeltaStore journal)
                                    initialContent = journal.ObserveInitialContent(sourceQuery.ManagedPaths, expected, sourceQuery.ContentPaths!);
                                view = initialContent?.Generation ?? (store is SourceViewDeltaStore selected ? selected.ObserveChanges(sourceQuery.ManagedPaths)
                                    : ((JournalDeltaStore)store).ObserveChanges(sourceQuery.ManagedPaths));
                            }
                            catch (Exception ex) { sourceError = (ex as WorkspaceException)?.Code ?? ex.GetType().Name; }
                            try
                            {
                                await WindowsProcessWorkspaceProvider.WriteAsync(Path.Combine(request.ControlPath, "source-response.json"),
                                    new SourceChangeResponse(request.Token, sourceQuery.RequestId, self.Id,
                                        self.StartTime.ToUniversalTime().Ticks, view, sourceError, initialContent));
                            }
                            finally
                            {
                                // The durable response/query root protects the
                                // handoff until the client binds its own reader.
                                view?.Dispose();
                                if (!ReferenceEquals(initialContent?.Generation, view)) initialContent?.Generation.Dispose();
                            }
                            lastSourceRequest = sourceQuery.RequestId;
                        }
                    }
                    catch (Exception ex)
                    {
                        // A failed flush/observation/publication is not a
                        // successful stop. Keep the provider and
                        // mount alive even when its failure receipt cannot be
                        // published; callers receive no success acknowledgement.
                        Interlocked.Increment(ref failures);
                        Volatile.Write(ref lastFailureCode, (ex as WorkspaceException)?.Code ?? ex.GetType().Name);
                        nextObservation = Stopwatch.GetTimestamp() + Stopwatch.Frequency;
                    }
                    double remaining = Math.Max(0, nextObservation - Stopwatch.GetTimestamp()) / (double)Stopwatch.Frequency;
                    await wakeup.WaitAsync(TimeSpan.FromSeconds(remaining));
                }
                }
                finally
                {
                    maintenanceCancellation.Cancel();
                    if (maintenance is not null) await maintenance;
                }
            }
            await WindowsProcessWorkspaceProvider.WriteAsync(statePath, new
            {
                version = 1, request.Token, request.Identity.Id, phase = "unmounted", observedAt = DateTimeOffset.UtcNow,
                snapshotId = request.Identity.Snapshot.Id, mountPath, deltaPath, processId = self.Id,
                processStartTimeUtcTicks = self.StartTime.ToUniversalTime().Ticks, openHandles = fileSystem.OpenHandles,
                retainedFileContexts = fileSystem.RetainedFileContexts,
                // The external owner must verify process exit and memory zero.
                storage = store.ObserveActivity(), ioFailures = Volatile.Read(ref failures), lastFailureCode = Volatile.Read(ref lastFailureCode)
            });
            return 0;
        }
        catch (Exception ex)
        {
            // An invalid/missing launch intent grants no right to overwrite an
            // existing generation's state, even with an error report.
            if (!launchOwned) return 1;
            await WindowsProcessWorkspaceProvider.WriteAsync(statePath, new
            {
                version = 1, request.Token, request.Identity.Id, phase = "failed", observedAt = DateTimeOffset.UtcNow,
                processId = self.Id, processStartTimeUtcTicks = self.StartTime.ToUniversalTime().Ticks,
                error = ex is WorkspaceException w ? w.Code : ex.GetType().Name, ex.Message,
                errorHResult = $"0x{ex.HResult:x8}", errorStackTrace = ex.StackTrace,
                publicationPath = ex.Data["publicationPath"], publicationTemporaryPath = ex.Data["publicationTemporaryPath"],
                preservedDeltaPath = request.DeltaPath, snapshotId = request.Identity.Snapshot.Id
            });
            return 1;
        }
    }
    internal static async Task RunStopAwareAsync(string control, string token, Action<CancellationToken> action)
    {
        using var operation = new CancellationTokenSource();
        using var finished = new CancellationTokenSource();
        async Task ObserveStop()
        {
            try
            {
                while (true)
                {
                    var stop = await WindowsProcessWorkspaceProvider.ReadAsync<StopRequest>(Path.Combine(control, "stop.json"),
                        finished.Token, waitForPublication: false);
                    if (stop?.Token == token) { operation.Cancel(); return; }
                    await Task.Delay(100, finished.Token);
                }
            }
            catch (OperationCanceledException) when (finished.IsCancellationRequested) { }
            catch { operation.Cancel(); throw; }
        }
        Task observer = ObserveStop();
        try { await Task.Run(() => action(operation.Token)); }
        finally { finished.Cancel(); await observer; }
    }
    internal static OverlayFlushReceipt ProcessFlush(IWorkspaceDeltaStore store, OverlayFlushRequest flush, int processId, long startTicks)
    {
        if (!flush.VerifySeal)
        {
            store.Flush();
            return new(flush.Token, flush.RequestId, DateTimeOffset.UtcNow, store.ObserveActivity(), processId, startTicks);
        }
        try
        {
            if (flush.ProcessId != processId || flush.ProcessStartTimeUtcTicks != startTicks)
                throw new WorkspaceException("OverlayLeaseStale", "The seal request belongs to another provider lifetime.");
            if (store is not JournalDeltaStore journal)
                throw new WorkspaceException("OverlaySealUnsupported", "A live physical seal requires the owned segmented writer.");
            var verified = journal.VerifyFlushedSegmentedSeal();
            return new(flush.Token, flush.RequestId, DateTimeOffset.UtcNow, verified.Storage, processId, startTicks,
                verified.Seal, verified.ElapsedMilliseconds);
        }
        catch (WorkspaceException error)
        {
            return new(flush.Token, flush.RequestId, DateTimeOffset.UtcNow, null, processId, startTicks,
                SealError: error.Code, SealErrorMessage: error.Message);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or ObjectDisposedException)
        {
            return new(flush.Token, flush.RequestId, DateTimeOffset.UtcNow, null, processId, startTicks,
                SealError: "OverlaySealFailed", SealErrorMessage: error.Message);
        }
    }
    private sealed record StopRequest(string Token);
    private sealed record CaptureSourceRequest(string Token, string RequestId, string Id, string[] ManagedPaths);
    private sealed record CompleteSourceRequest(string Token, string RequestId, string Id, bool PrepareForRelease);
}
