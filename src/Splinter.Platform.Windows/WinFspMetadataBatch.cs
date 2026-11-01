using System.Runtime.ExceptionServices;
using Splinter.Core;
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
    }
    private readonly object metadataQueueGate = new();
    private readonly Queue<PendingMetadata> waitingMetadata = new();
    private bool metadataBatchRunning;
    private long issuedMetadataTicket, completedMetadataTicket;
    internal int PendingMetadataCount { get { lock (metadataQueueGate) return waitingMetadata.Count; } }

    private FileInfo SubmitMetadata(string operation, Func<FileInfo> apply, Action? committed = null)
    {
        var request = new PendingMetadata(operation, WriteClock(), apply, committed);
        lock (metadataQueueGate) { request.Ticket = ++issuedMetadataTicket; waitingMetadata.Enqueue(request); }
        ExceptionDispatchInfo? interruption = null;
        while (true)
        {
            try { CompleteMetadata(request); break; }
            catch (ThreadInterruptedException error) when (!Volatile.Read(ref request.Completed))
            { interruption ??= ExceptionDispatchInfo.Capture(error); }
        }
        interruption?.Throw(); request.Failure?.Throw(); return request.Info;
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
            long acquired = 0, ended = 0;
            try
            {
                lock (mutationGate)
                {
                    lock (metadataQueueGate)
                    {
                        batch = new PendingMetadata[Math.Min(waitingMetadata.Count, IWorkspaceWriteBatch.MaximumWrites)];
                        for (int i = 0; i < batch.Length; i++) batch[i] = waitingMetadata.Dequeue();
                    }
                    acquired = WriteClock();
                    try
                    {
                        var callbacks = batch.Select(item => (Action)(() => item.Info = item.Apply())).ToArray();
                        if (store is IWorkspaceMetadataBatch writer) writer.ApplyMetadataBatch(callbacks);
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
                // Each callback includes its queue wait and the shared durable batch interval.
                foreach (var item in batch)
                    writeTelemetry?.Mutation(item.Operation, acquired - item.Started, ended - acquired);
                lock (metadataQueueGate)
                {
                    foreach (var item in batch) { item.Failure ??= failure; item.Completed = true; }
                    if (batch.Length != 0) completedMetadataTicket = batch[^1].Ticket;
                    metadataBatchRunning = false; Monitor.PulseAll(metadataQueueGate);
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
