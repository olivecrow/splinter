using System.Runtime.ExceptionServices;
using Splinter.Core;
using Splinter.Runtime;
using System.Diagnostics;
using FileInfo = Fsp.Interop.FileInfo;

namespace Splinter.Platform.Windows;

public sealed partial class WinFspOverlayFileSystem
{
    private sealed class PendingMetadata(string operation, long started, Func<FileInfo> apply, Action? committed)
    {
        public readonly string Operation = operation;
        public readonly long Started = started;
        public readonly Func<FileInfo> Apply = apply;
        public readonly Action? Committed = committed;
        public FileInfo Info;
        public bool Completed;
        public long Ticket;
        public ExceptionDispatchInfo? Failure;
        public PendingWrite? Write;
        public long Units = 1, Bytes;
        public bool Shared;
    }
    private readonly object metadataQueueGate = new();
    private readonly Queue<PendingMetadata> waitingMetadata = new();
    private bool metadataBatchRunning;
    private long issuedMetadataTicket, completedMetadataTicket;
    internal int PendingMetadataCount { get { lock (metadataQueueGate) return waitingMetadata.Count(item => item.Write is null); } }
    private int PendingMixedWriteCount { get { lock (metadataQueueGate) return waitingMetadata.Count(item => item.Write is not null); } }

    private FileInfo SubmitMetadata(string operation, Func<FileInfo> apply, Action? committed = null)
    {
        var request = new PendingMetadata(operation, WriteClock(), apply, committed);
        lock (metadataQueueGate) { request.Ticket = ++issuedMetadataTicket; waitingMetadata.Enqueue(request); Monitor.PulseAll(metadataQueueGate); }
        return WaitForMetadata(request);
    }
    private void SubmitMixedWrite(PendingWrite write)
    {
        ExceptionDispatchInfo? interruption = null;
        PendingMetadata request = null!;
        request = new("write", WriteClock(), () => { ExecuteWriteBatch([write], request.Shared); return default; }, () =>
        {
            if (request.Shared) CompleteWriteEffect(write);
        }) { Write = write, Bytes = write.Length,
            Units = Math.Max(1, ((long)write.Length + store.Identity.MaxTransferBytes - 1) / store.Identity.MaxTransferBytes) };
        lock (metadataQueueGate)
        {
            request.Ticket = ++issuedMetadataTicket;
            lock (writeQueueGate) { write.Ticket = ++issuedWriteTicket; write.EnqueuedTicks = WriteClock(); }
            waitingMetadata.Enqueue(request);
            Monitor.PulseAll(metadataQueueGate);
            while (true)
            {
                try { writeTelemetry?.Enqueued(write.Length, waitingMetadata.Count(item => item.Write is not null)); break; }
                catch (ThreadInterruptedException error) { interruption ??= ExceptionDispatchInfo.Capture(error); }
            }
        }
        WaitForMetadata(request);
        interruption?.Throw();
    }
    private FileInfo WaitForMetadata(PendingMetadata request)
    {
        ExceptionDispatchInfo? interruption = null;
        while (true)
        {
            try { CompleteMetadata(request); break; }
            catch (ThreadInterruptedException error) when (!Volatile.Read(ref request.Completed))
            { interruption ??= ExceptionDispatchInfo.Capture(error); }
        }
        interruption?.Throw(); request.Failure?.Throw(); return request.Info;
    }
    private void GatherConcurrentMutations()
    {
        if (store is not IWorkspaceMutationBatch || OpenHandles < 2) return;
        static bool Changes(PendingMetadata item) => item.Write is not null || item.Operation is "overwrite" or "set-basic-info" or "set-file-size";
        long until = Stopwatch.GetTimestamp() + Math.Max(1, Stopwatch.Frequency / 10000);
        while (OpenHandles > 1 && Stopwatch.GetTimestamp() < until)
        {
            lock (metadataQueueGate)
            {
                if (!waitingMetadata.TryPeek(out var first) || !Changes(first) || waitingMetadata.Count(Changes) >= 2) return;
            }
            // No timer sleep: synchronous callers retain their input. Yield
            // at most 100us without holding either mutation or journal gates.
            Thread.Yield();
        }
    }
    private void CompleteMetadata(PendingMetadata request)
    {
        while (true)
        {
            lock (metadataQueueGate)
            {
                if (request.Completed) return;
                if (metadataBatchRunning) { Monitor.Wait(metadataQueueGate); continue; }
                metadataBatchRunning = true;
            }
            PendingMetadata[] batch = []; ExceptionDispatchInfo? failure = null;
            PendingWrite[] writes = [];
            long acquired = 0, ended = 0;
            IJournalWriteAccounting? accounting = null; JournalWriteAccounting? before = null;
            try
            {
                GatherConcurrentMutations();
                lock (mutationGate)
                {
                    lock (metadataQueueGate)
                    {
                        int count = 0; long units = 0, bytes = 0;
                        foreach (var next in waitingMetadata)
                        {
                            if (count != 0 && (next.Units > IWorkspaceWriteBatch.MaximumWrites - units ||
                                next.Bytes > IWorkspaceWriteBatch.MaximumBytes - bytes)) break;
                            count++; units += next.Units; bytes += next.Bytes;
                            if (units >= IWorkspaceWriteBatch.MaximumWrites || bytes >= IWorkspaceWriteBatch.MaximumBytes) break;
                        }
                        // Allocate before removing any accepted callback: an
                        // allocation failure must never lose its input lease.
                        batch = new PendingMetadata[count];
                        for (int i = 0; i < count; i++) batch[i] = waitingMetadata.Dequeue();
                        bool shared = store is IWorkspaceMutationBatch && units <= IWorkspaceWriteBatch.MaximumWrites && bytes <= IWorkspaceWriteBatch.MaximumBytes;
                        foreach (var item in batch) item.Shared = shared;
                        writes = batch.Where(item => item.Write is not null).Select(item => item.Write!).ToArray();
                        if (writes.Length != 0)
                        {
                            long batchId; lock (writeQueueGate) batchId = ++nextWriteBatchId;
                            foreach (var write in writes) { write.BatchId = batchId; write.DequeuedTicks = WriteClock(); }
                        }
                    }
                    acquired = WriteClock();
                    if (writeTelemetry is not null && batch.Any(item => item.Write is not null && item.Shared))
                    { accounting = store as IJournalWriteAccounting; before = accounting?.ReadWriteAccounting(); }
                    try
                    {
                        var callbacks = batch.Select(item => (Action)(() => item.Info = item.Apply())).ToArray();
                        if (batch[0].Shared && store is IWorkspaceMutationBatch mixed) mixed.ApplyMutationBatch(callbacks);
                        else if (batch.All(item => item.Write is null) && store is IWorkspaceMetadataBatch writer) writer.ApplyMetadataBatch(callbacks);
                        else foreach (var callback in callbacks) callback();
                        // Retain a successful Create's handle before releasing
                        // mutationGate, but never retain an unacknowledged file.
                        foreach (var item in batch)
                            try { item.Committed?.Invoke(); }
                            catch (Exception error) { item.Failure = ExceptionDispatchInfo.Capture(error); }
                    }
                    finally { ended = WriteClock(); }
                }
            }
            catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
            finally
            {
                try
                {
                    // Charge the outer durability interval once, after its
                    // ACK boundary, rather than charging each staged write's
                    // pre-flush interval or counting the shared flush N times.
                    var deferred = writes.SelectMany(write => write.DeferredStores ?? []).ToArray();
                    for (int i = 0; i < deferred.Length; i++)
                    {
                        var sample = deferred[i];
                        writeTelemetry?.Store(sample.BatchId, sample.Chunks, sample.Bytes, sample.Reason, sample.UsesBatch,
                            sample.Started, ended, failure is not null || batch.Any(item => item.Failure is not null),
                            i == deferred.Length - 1 && before is { } prior ? accounting!.ReadWriteAccounting() - prior : null);
                    }
                    // Each callback includes queue wait and shared durability.
                    foreach (var item in batch.Where(item => item.Write is null))
                        writeTelemetry?.Mutation(item.Operation, acquired - item.Started, ended - acquired);
                    if (writes.Length != 0)
                        writeTelemetry?.Batch(writes[0].BatchId, writes, acquired - writes[0].EnqueuedTicks, acquired, ended, failure is not null);
                }
                finally
                {
                    lock (metadataQueueGate)
                    {
                        // Publish the write watermark before another mixed
                        // leader can complete a later batch. Never regress it.
                        PendingWrite? lastWrite = null;
                        for (int i = batch.Length - 1; i >= 0; i--) if (batch[i].Write is { } found) { lastWrite = found; break; }
                        if (lastWrite is not null)
                            lock (writeQueueGate)
                            {
                                foreach (var item in batch)
                                    if (item.Write is { } write) { write.Failure = item.Failure ?? failure; write.Completed = true; }
                                completedWriteTicket = lastWrite.Ticket; Monitor.PulseAll(writeQueueGate);
                            }
                        foreach (var item in batch) { item.Failure ??= failure; item.Completed = true; }
                        if (batch.Length != 0) completedMetadataTicket = batch[^1].Ticket;
                        metadataBatchRunning = false; Monitor.PulseAll(metadataQueueGate);
                    }
                }
            }
        }
    }
    private void DrainAcceptedMetadata()
    {
        lock (metadataQueueGate)
        {
            long target = issuedMetadataTicket;
            while (completedMetadataTicket < target) Monitor.Wait(metadataQueueGate);
        }
    }
}
