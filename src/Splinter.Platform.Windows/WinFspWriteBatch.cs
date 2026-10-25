using System.Buffers;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using Splinter.Core;
using FileInfo = Fsp.Interop.FileInfo;

namespace Splinter.Platform.Windows;

public sealed partial class WinFspOverlayFileSystem
{
    private sealed class PendingWrite(Guid id, IntPtr buffer, ulong offset, uint length, bool append, bool constrained)
    {
        public readonly Guid Id = id;
        public readonly IntPtr Buffer = buffer;
        public readonly ulong Offset = offset;
        public readonly uint Length = length;
        public readonly bool Append = append, Constrained = constrained;
        public uint Written, EffectiveLength;
        public string Path = "";
        public FileInfo Info;
        public long Ticket;
        public bool Completed;
        public ExceptionDispatchInfo? Failure;
    }
    private readonly object writeQueueGate = new();
    private readonly Queue<PendingWrite> waitingWrites = new();
    private bool writeBatchRunning;
    private long issuedWriteTicket, completedWriteTicket;
    internal int PendingWriteCount { get { lock (writeQueueGate) return waitingWrites.Count; } }

    private void SubmitWrite(PendingWrite request)
    {
        // Native input remains owned by its blocked callback until completion.
        // No worker, timeout ACK, sleep window or unbounded payload copy.
        lock (writeQueueGate) { request.Ticket = ++issuedWriteTicket; waitingWrites.Enqueue(request); }
        ExceptionDispatchInfo? interruption = null;
        while (true)
        {
            try { SubmitQueuedWrite(request); break; }
            catch (ThreadInterruptedException error) when (!Volatile.Read(ref request.Completed))
            { interruption ??= ExceptionDispatchInfo.Capture(error); }
        }
        interruption?.Throw();
    }

    private void SubmitQueuedWrite(PendingWrite request)
    {
        ExceptionDispatchInfo? interruption = null;
        while (true)
        {
            lock (writeQueueGate)
            {
                if (request.Completed) { request.Failure?.Throw(); interruption?.Throw(); return; }
                if (writeBatchRunning)
                {
                    // A callback cannot release its native input while another
                    // caller may still consume that queued request.
                    try { Monitor.Wait(writeQueueGate); }
                    catch (ThreadInterruptedException error) { interruption ??= ExceptionDispatchInfo.Capture(error); }
                    continue;
                }
                writeBatchRunning = true;
            }
            PendingWrite[] batch = [];
            ExceptionDispatchInfo? failure = null;
            try
            {
                lock (mutationGate)
                {
                    lock (writeQueueGate)
                    {
                        int count = Math.Min(waitingWrites.Count, IWorkspaceWriteBatch.MaximumWrites);
                        batch = new PendingWrite[count];
                        for (int i = 0; i < count; i++) batch[i] = waitingWrites.Dequeue();
                    }
                    ExecuteWriteBatch(batch);
                }
            }
            catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
            finally
            {
                lock (writeQueueGate)
                {
                    foreach (var item in batch) { item.Failure = failure; item.Completed = true; }
                    if (batch.Length != 0) completedWriteTicket = batch[^1].Ticket;
                    writeBatchRunning = false;
                    Monitor.PulseAll(writeQueueGate);
                }
            }
        }
    }

    private void DrainAcceptedWrites()
    {
        lock (writeQueueGate)
        {
            long through = issuedWriteTicket;
            while (completedWriteTicket < through) Monitor.Wait(writeQueueGate);
        }
    }

    private void ExecuteWriteBatch(PendingWrite[] batch)
    {
        var writes = new List<WorkspaceRangeWrite>();
        var buffers = new List<byte[]>();
        var lengths = new Dictionary<Guid, long>();
        int bytes = 0;
        void ReleaseBuffers()
        {
            foreach (var buffer in buffers) ArrayPool<byte>.Shared.Return(buffer);
            buffers.Clear(); writes.Clear(); bytes = 0;
        }
        void Commit()
        {
            if (writes.Count == 0) return;
            try
            {
                if (store is IWorkspaceWriteBatch writer) writer.WriteBatch(writes);
                else foreach (var write in writes) store.Write(write.Id, write.Offset, write.Data.Span);
            }
            finally { ReleaseBuffers(); }
        }
        try
        {
            foreach (var request in batch)
            {
                var entry = store.Get(request.Id);
                long size = lengths.GetValueOrDefault(request.Id, entry.Length);
                ulong offset = request.Append ? (ulong)size : request.Offset;
                uint length = request.Constrained ? offset >= (ulong)size ? 0 : (uint)Math.Min(request.Length, (ulong)size - offset) : request.Length;
                request.Path = entry.Path; request.EffectiveLength = length;
                Visit("callback.before-effect", "write", entry.Path, length, 0);
                while (request.Written < length)
                {
                    int count = (int)Math.Min(length - request.Written, (uint)store.Identity.MaxTransferBytes);
                    if (writes.Count == IWorkspaceWriteBatch.MaximumWrites || bytes + count > IWorkspaceWriteBatch.MaximumBytes) Commit();
                    byte[] data = ArrayPool<byte>.Shared.Rent(count);
                    buffers.Add(data);
                    Marshal.Copy(IntPtr.Add(request.Buffer, checked((int)request.Written)), data, 0, count);
                    long at = checked((long)(offset + request.Written));
                    writes.Add(new(request.Id, at, data.AsMemory(0, count))); bytes += count;
                    request.Written += (uint)count;
                    lengths[request.Id] = Math.Max(size, checked(at + count));
                }
            }
            Commit();
            foreach (var request in batch)
            {
                request.Info = Info(request.Id);
                Visit("callback.after-effect", "write", request.Path, request.EffectiveLength, request.Written);
            }
        }
        finally { ReleaseBuffers(); }
    }
}
