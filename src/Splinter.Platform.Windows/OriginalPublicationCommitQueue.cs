using System.Runtime.ExceptionServices;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Platform.Windows;

// Callers remain at their existing durable boundary until their group is saved.
// One caller leads; no timer, background writer or speculative acknowledgement.
internal sealed class OriginalPublicationCommitQueue(OriginalApplyState state, Action persist, Action<int>? durable = null)
{
    private sealed class Request(long index, OriginalApplyStep step, string? scratch)
    {
        internal readonly long Index = index;
        internal readonly OriginalApplyStep Step = new() { Phase = step.Phase, StagedIdentity = step.StagedIdentity, StagedBytes = step.StagedBytes, StageNonce = step.StageNonce };
        internal readonly string? Scratch = scratch;
        internal bool Complete;
        internal ExceptionDispatchInfo? Failure;
    }
    private readonly object gate = new();
    private readonly Queue<Request> waiting = new();
    private bool running;
    private ExceptionDispatchInfo? poisoned;
    internal int PendingCount { get { lock (gate) return waiting.Count; } }
    internal void Commit(long index, OriginalApplyStep step, string? preservedScratch = null)
    {
        var request = new Request(index, step, preservedScratch);
        lock (gate) { poisoned?.Throw(); waiting.Enqueue(request); }
        ExceptionDispatchInfo? interruption = null;
        while (true)
        {
            try { Complete(request); break; }
            catch (ThreadInterruptedException error) when (!Volatile.Read(ref request.Complete))
            { interruption ??= ExceptionDispatchInfo.Capture(error); }
        }
        interruption?.Throw(); request.Failure?.Throw();
    }
    private void Complete(Request request)
    {
        while (true)
        {
            Request[] batch;
            lock (gate)
            {
                if (request.Complete) return;
                if (running) { Monitor.Wait(gate); continue; }
                running = true; batch = new Request[Math.Min(64, waiting.Count)];
                for (int i = 0; i < batch.Length; i++) batch[i] = waiting.Dequeue();
            }
            ExceptionDispatchInfo? failure = null;
            try
            {
                foreach (var item in batch)
                    if (item.Scratch is { } scratch)
                        state.PreservedScratchRoot = (state.PageProgress ?? throw new InvalidOperationException("Missing scratch ledger."))
                            .PreserveScratch(scratch, null, "native-stage-identity-not-recorded");
                OriginalApplyPages.SaveSteps(state, batch.Select(item => new KeyValuePair<long, OriginalApplyStep>(item.Index, item.Step)).ToArray());
                persist(); durable?.Invoke(batch.Length);
            }
            catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
            finally
            {
                lock (gate)
                {
                    foreach (var item in batch) { item.Failure = failure; item.Complete = true; }
                    if (failure is not null)
                    {
                        poisoned = failure;
                        while (waiting.TryDequeue(out var item)) { item.Failure = failure; item.Complete = true; }
                    }
                    running = false; Monitor.PulseAll(gate);
                }
            }
        }
    }
}
