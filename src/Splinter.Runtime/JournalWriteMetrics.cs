using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class JournalDeltaStore
{
    private sealed class LiveCommitCounters
    {
        public long Attempts, DurableCommits, FailedCalls, PayloadBytes, JournalBytes;
        public long PrepareTicks, AppendTicks, FlushTicks, ApplyTicks, TotalTicks, FlushCalls;
        public int MaxWaitingMutations;
        public readonly long[] WaitingAtStart = new long[5], WaitingAtEnd = new long[5];
    }

    private readonly Dictionary<string, LiveCommitCounters> liveCommitOperations = [];
    private int waitingMutationCalls;
    private long mutationGateCalls, mutationGateWaitTicks;

    private LiveJournalWriteMetrics ObserveLiveWrites() => new(mutationGateCalls, mutationGateWaitTicks,
        liveCommitOperations.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair =>
        {
            var value = pair.Value;
            return new LiveCommitOperationMetrics(pair.Key, value.Attempts, value.DurableCommits,
                value.FailedCalls, value.PayloadBytes, value.JournalBytes, value.PrepareTicks,
                value.AppendTicks, value.FlushTicks, value.ApplyTicks, value.TotalTicks, value.FlushCalls,
                value.MaxWaitingMutations, (long[])value.WaitingAtStart.Clone(), (long[])value.WaitingAtEnd.Clone());
        }).ToArray());

    private void SampleWaitingMutations(LiveCommitCounters cost, long[] buckets)
    {
        int count = Volatile.Read(ref waitingMutationCalls);
        cost.MaxWaitingMutations = Math.Max(cost.MaxWaitingMutations, count);
        buckets[count switch { 0 => 0, 1 => 1, <= 3 => 2, <= 7 => 3, _ => 4 }]++;
    }
}
