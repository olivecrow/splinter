using System.Diagnostics;
using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class JournalDeltaStore : IWorkspaceWriteBatch
{
    private sealed record PendingCommit(Mutation Mutation, long Start, long Bytes, int PayloadBytes, LiveCommitCounters Cost);
    private List<PendingCommit>? pendingWriteBatch;

    public void WriteBatch(IReadOnlyList<WorkspaceRangeWrite> writes)
    {
        using (EnterGate(mutation: true))
        {
            Check();
            ValidateWriteBatch(writes);
            if (pendingWriteBatch is not null) throw new InvalidOperationException("Nested write batches are unsupported.");
            if (readOnly) throw new WorkspaceException("CandidateReadOnly", "A published candidate cannot be edited.");
            // SJL4 retains its capsule publication protocol. SJR2 (bounded or
            // append) can share a file flush without changing persisted frames.
            if (latest is not null)
            {
                foreach (var write in writes) Write(write.Id, write.Offset, write.Data.Span);
                return;
            }
            pendingWriteBatch = [];
            try
            {
                foreach (var write in writes) Write(write.Id, write.Offset, write.Data.Span);
                FlushPendingWrites();
            }
            catch
            {
                // Private in-memory application is visible only inside this
                // gate. Never expose it after a failed batch, even on read.
                if (pendingWriteBatch.Count != 0) Poison();
                throw;
            }
            finally { pendingWriteBatch = null; }
        }
    }

    internal static void ValidateWriteBatch(IReadOnlyList<WorkspaceRangeWrite> writes)
    {
        ArgumentNullException.ThrowIfNull(writes);
        if (writes.Count > IWorkspaceWriteBatch.MaximumWrites || writes.Sum(w => (long)w.Data.Length) > IWorkspaceWriteBatch.MaximumBytes)
            throw new ArgumentException("The write batch exceeds its bounded request or byte budget.", nameof(writes));
    }

    private void FlushPendingWrites()
    {
        if (pendingWriteBatch is not { Count: > 0 } pending) return;
        var last = pending[^1];
        long before = flushTicks, began = Stopwatch.GetTimestamp();
        try
        {
            foreach (var item in pending)
                Visit("commit.before-flush", item.Mutation.Operation, item.Mutation.Sequence, item.Start, item.Bytes, item.PayloadBytes);
            last.Cost.FlushCalls++; SampleWaitingMutations(last.Cost, last.Cost.WaitingAtStart);
            DurableFlush();
            foreach (var item in pending)
            {
                Visit("commit.after-flush", item.Mutation.Operation, item.Mutation.Sequence, item.Start, item.Bytes, item.PayloadBytes);
                commitCount++; appendedBytes += item.Bytes;
                item.Cost.DurableCommits++; item.Cost.PayloadBytes += item.PayloadBytes; item.Cost.JournalBytes += item.Bytes;
                Visit("commit.after-apply", item.Mutation.Operation, item.Mutation.Sequence, item.Start, item.Bytes, item.PayloadBytes);
            }
            pending.Clear();
        }
        catch { Poison(); throw; }
        finally
        {
            last.Cost.FlushTicks += flushTicks - before;
            last.Cost.TotalTicks += Stopwatch.GetTimestamp() - began;
            SampleWaitingMutations(last.Cost, last.Cost.WaitingAtEnd);
        }
    }
}
