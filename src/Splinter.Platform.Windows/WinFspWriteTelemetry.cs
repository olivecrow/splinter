using System.Diagnostics;
using Splinter.Runtime;

namespace Splinter.Platform.Windows;

internal sealed record WinFspWriteCallbackSample(long Ticket, long BatchId, Guid FileId, string Path,
    bool PathTruncated, ulong Offset, uint RequestedBytes, uint EffectiveBytes, uint StagedBytes,
    bool Append, bool Constrained, long EnteredTicks, long EnqueuedTicks, long DequeuedTicks,
    long CompletedTicks, long ReturnedTicks, bool Failed, bool ReturnFailed);
internal sealed record WinFspWriteBatchSample(long Id, long FirstTicket, long LastTicket, int Callbacks,
    long GateWaitTicks, long StartedTicks, long CompletedTicks, bool Failed);
internal sealed record WinFspWriteStoreSample(long BatchId, int Chunks, int Bytes, string Reason,
    bool UsesBatch, long StartedTicks, long CompletedTicks, bool Failed, JournalWriteAccounting? Runtime);
internal sealed record WinFspMutationTiming(string Operation, long Calls, long GateWaitTicks, long HeldTicks);
internal sealed record WinFspMixedAdmission(long Groups, long Callbacks, long MutatingCallbacks,
    long GatherTicks, long QueueWaitTicks, long ServiceTicks, long IdleOpens, long IdleOpenServiceTicks,
    long[] CallbackBuckets, long StopwatchFrequency);
internal sealed record WinFspWriteAdmissionObservation(int Version, long StopwatchFrequency, long ObservedTicks,
    long AcceptedCallbacks, long CompletedCallbacks, long ReturnedCallbacks, long FailedCallbacks, long FailedReturns, long RequestedBytes,
    long EffectiveBytes, long SuccessfulBytes, int MaxQueuedCallbacks, long MaxOutstandingCallbacks, long QueueResidenceTicks,
    long CallbackTicks, long BatchCount, long BatchGateWaitTicks, long[] BatchCallbackBuckets, long StoreCalls, long RangeChunks,
    long StagedBytes, long ChunkLimitSplits, long ByteLimitSplits, long DrainCalls, long DrainWaitTicks,
    long RuntimeMeasuredCalls, JournalWriteAccounting RuntimeStoreIntervals,
    long DiscardedCallbackSamples, long DiscardedBatchSamples, long DiscardedStoreSamples,
    WinFspWriteCallbackSample[] RecentCallbacks, WinFspWriteBatchSample[] RecentBatches,
    WinFspWriteStoreSample[] RecentStoreCalls, WinFspMutationTiming[] MutationCallbacks)
{
    public string Scope => "Provider-local monotonic counters; recent samples overwrite at fixed capacity. " +
        "Runtime deltas cover store-call intervals and may include concurrent administrative flushes. " +
        "Requested callback bytes, staged range bytes and stored changed-payload bytes differ. " +
        "Completed ticks mark batch effect publication; returned ticks mark callback return or throw. " +
        "Interrupted waiters can throw after a durable successful effect. " +
        "Dispatcher admission is observed, not the originating application thread or kernel queue.";
}

public sealed partial class WinFspOverlayFileSystem
{
    // No payload retention, filesystem queries, serialization or file I/O occurs
    // here. The fixed rings retain only recent examples; totals never sample.
    internal sealed class WriteTelemetry
    {
        internal const int CallbackCapacity = 32, BatchCapacity = 16, StoreCapacity = 16, MaximumPathCharacters = 256;
        private readonly object gate = new();
        private readonly WinFspWriteCallbackSample?[] callbacks = new WinFspWriteCallbackSample[CallbackCapacity];
        private readonly WinFspWriteBatchSample?[] batches = new WinFspWriteBatchSample[BatchCapacity];
        private readonly WinFspWriteStoreSample?[] stores = new WinFspWriteStoreSample[StoreCapacity];
        private readonly long[] batchBuckets = new long[7]; // 0, 1, 2-3, 4-7, 8-15, 16-63, 64+
        private readonly Dictionary<string, (long Calls, long Wait, long Held)> mutations = new(StringComparer.Ordinal);
        private long accepted, completed, returned, failed, failedReturns, requestedBytes, effectiveBytes, successfulBytes, queueTicks, callbackTicks;
        private long batchCount, batchGateWait, maxOutstanding, storeCalls, chunks, stagedBytes, chunkSplits, byteSplits, drainCalls, drainTicks, measuredCalls;
        private int maxQueued;
        private JournalWriteAccounting runtime;
        private long mixedGroups, mixedCallbacks, mixedMutations, mixedGather, mixedQueue, mixedService;
        private long idleOpens, idleOpenTicks;
        private readonly long[] mixedBuckets = new long[7];
        internal void MixedBatch(int callbacks, int changes, long gather, long queue, long service)
        {
            lock (gate)
            {
                mixedGroups++; mixedCallbacks += callbacks; mixedMutations += changes;
                mixedGather += gather; mixedQueue += queue; mixedService += service;
                mixedBuckets[callbacks switch { 0 => 0, 1 => 1, <= 3 => 2, <= 7 => 3, <= 15 => 4, < 64 => 5, _ => 6 }]++;
            }
        }
        internal WinFspMixedAdmission ObserveMixed()
        { lock (gate) return new(mixedGroups, mixedCallbacks, mixedMutations, mixedGather, mixedQueue, mixedService, idleOpens, idleOpenTicks, (long[])mixedBuckets.Clone(), Stopwatch.Frequency); }
        internal void IdleOpen(long ticks) { lock (gate) { idleOpens++; idleOpenTicks += ticks; } }

        internal void Enqueued(uint length, int queued)
        { lock (gate) { accepted++; requestedBytes += length; maxQueued = Math.Max(maxQueued, queued); maxOutstanding = Math.Max(maxOutstanding, accepted - returned); } }

        private void Completed(PendingWrite request, long at, bool failure)
        {
            string path = request.Path;
            bool truncated = path.Length > MaximumPathCharacters;
            if (truncated) path = path[..MaximumPathCharacters];
            var sample = new WinFspWriteCallbackSample(request.Ticket, request.BatchId, request.Id, path, truncated,
                request.Offset, request.Length, request.EffectiveLength, request.Written, request.Append, request.Constrained,
                request.EnteredTicks, request.EnqueuedTicks, request.DequeuedTicks, at, 0, failure, false);
            lock (gate)
            {
                callbacks[completed % CallbackCapacity] = sample; completed++;
                if (failure) failed++; else successfulBytes += request.Written;
                effectiveBytes += request.EffectiveLength;
                queueTicks += request.DequeuedTicks - request.EnqueuedTicks;
            }
        }

        internal void Returned(PendingWrite request, long at, bool failure)
        {
            lock (gate)
            {
                returned++; if (failure) failedReturns++;
                callbackTicks += at - request.EnteredTicks;
                if (request.Ticket > 0)
                {
                    int slot = (int)((request.Ticket - 1) % CallbackCapacity);
                    if (callbacks[slot] is { } prior && prior.Ticket == request.Ticket)
                        callbacks[slot] = prior with { ReturnedTicks = at, ReturnFailed = failure };
                }
            }
        }

        internal void Batch(long id, PendingWrite[] requests, long wait, long started, long ended, bool failure)
        {
            lock (gate)
            {
                batches[batchCount % BatchCapacity] = new(id, requests[0].Ticket, requests[^1].Ticket,
                    requests.Length, wait, started, ended, failure);
                batchCount++; batchGateWait += wait;
                batchBuckets[requests.Length switch { 0 => 0, 1 => 1, <= 3 => 2, <= 7 => 3, <= 15 => 4, < 64 => 5, _ => 6 }]++;
            }
            foreach (var request in requests) Completed(request, ended, failure);
        }

        internal void Store(long batchId, int count, int bytes, string reason, bool usesBatch,
            long started, long ended, bool failure, JournalWriteAccounting? accounting)
        {
            lock (gate)
            {
                stores[storeCalls % StoreCapacity] = new(batchId, count, bytes, reason, usesBatch, started, ended, failure, accounting);
                storeCalls++; chunks += count; stagedBytes += bytes;
                if (reason == "chunk-limit") chunkSplits++;
                if (reason == "byte-limit") byteSplits++;
                if (accounting is { } delta) { measuredCalls++; runtime += delta; }
            }
        }

        internal void Drain(long ticks) { lock (gate) { drainCalls++; drainTicks += ticks; } }
        internal void Mutation(string operation, long wait, long held)
        {
            lock (gate)
            {
                var prior = mutations.GetValueOrDefault(operation);
                mutations[operation] = (prior.Calls + 1, prior.Wait + wait, prior.Held + held);
            }
        }

        internal WinFspWriteAdmissionObservation Observe()
        {
            lock (gate)
                return new(1, Stopwatch.Frequency, Stopwatch.GetTimestamp(), accepted, completed, returned, failed, failedReturns,
                    requestedBytes, effectiveBytes, successfulBytes, maxQueued, maxOutstanding, queueTicks, callbackTicks,
                    batchCount, batchGateWait, (long[])batchBuckets.Clone(), storeCalls, chunks, stagedBytes, chunkSplits, byteSplits,
                    drainCalls, drainTicks, measuredCalls, runtime,
                    Math.Max(0, completed - CallbackCapacity), Math.Max(0, batchCount - BatchCapacity), Math.Max(0, storeCalls - StoreCapacity),
                    Recent(callbacks, completed), Recent(batches, batchCount), Recent(stores, storeCalls),
                    mutations.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                        .Select(pair => new WinFspMutationTiming(pair.Key, pair.Value.Calls, pair.Value.Wait, pair.Value.Held)).ToArray());
        }

        private static T[] Recent<T>(T?[] ring, long count) where T : class
        {
            int size = (int)Math.Min(count, ring.Length);
            var result = new T[size];
            long first = Math.Max(0, count - ring.Length);
            for (int i = 0; i < size; i++) result[i] = ring[(first + i) % ring.Length]!;
            return result;
        }
    }

    private readonly WriteTelemetry? writeTelemetry;
    internal WinFspWriteAdmissionObservation? ObserveWriteAdmission() => writeTelemetry?.Observe();
    // Internal diagnostic only; no command/wire field or retained authority.
    internal WinFspMixedAdmission? ObserveMixedAdmission() => writeTelemetry?.ObserveMixed();
    private long WriteClock() => writeTelemetry is null ? 0 : Stopwatch.GetTimestamp();

    private MutationLease EnterMutation(string operation)
    {
        long started = WriteClock();
        Monitor.Enter(mutationGate);
        return new(mutationGate, writeTelemetry, operation, started, WriteClock());
    }

    private readonly struct MutationLease(object gate, WriteTelemetry? telemetry, string operation,
        long started, long acquired) : IDisposable
    {
        public void Dispose()
        {
            long ended = telemetry is null ? 0 : Stopwatch.GetTimestamp();
            Monitor.Exit(gate);
            telemetry?.Mutation(operation, acquired - started, ended - acquired);
        }
    }
}
