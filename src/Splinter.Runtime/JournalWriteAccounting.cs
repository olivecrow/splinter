namespace Splinter.Runtime;

// Process-local diagnostic counters, not a durability receipt or a disk audit.
// Reading these counters never observes the basis or materializes metric arrays.
internal readonly record struct JournalWriteAccounting(long FlushCalls, long FlushTicks,
    long DurableCommits, long ChangedPayloadBytes)
{
    public static JournalWriteAccounting operator +(JournalWriteAccounting a, JournalWriteAccounting b) =>
        new(a.FlushCalls + b.FlushCalls, a.FlushTicks + b.FlushTicks,
            a.DurableCommits + b.DurableCommits, a.ChangedPayloadBytes + b.ChangedPayloadBytes);
    public static JournalWriteAccounting operator -(JournalWriteAccounting a, JournalWriteAccounting b) =>
        new(a.FlushCalls - b.FlushCalls, a.FlushTicks - b.FlushTicks,
            a.DurableCommits - b.DurableCommits, a.ChangedPayloadBytes - b.ChangedPayloadBytes);
}

internal interface IJournalWriteAccounting
{
    JournalWriteAccounting ReadWriteAccounting();
}

public sealed partial class JournalDeltaStore : IJournalWriteAccounting
{
    JournalWriteAccounting IJournalWriteAccounting.ReadWriteAccounting()
    {
        lock (gate)
        {
            long changed = 0;
            foreach (var cost in liveCommitOperations.Values) changed += cost.PayloadBytes;
            return new(flushCount, flushTicks, commitCount, changed);
        }
    }
}
