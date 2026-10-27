using System.Diagnostics;
using Splinter.Core;

namespace Splinter.SourceControl.Git;

internal sealed record GitPreparationInterval(string Stage, long Start, long End, bool Succeeded);
internal sealed record GitPreparationSnapshot(int Version, string AttemptId, DateTimeOffset StartedAt, long Started,
    DateTimeOffset? EndedAt, long? Ended, string Outcome, string? FailureKind, string? FailureCode,
    long StopwatchFrequency, long WarmFiles, long DroppedIntervals, GitPreparationInterval[] Intervals);

// A fixed number of operation intervals, not a per-file log. Warm/refresh overlap.
internal sealed class GitPreparationObservation
{
    private readonly object gate = new();
    private readonly List<GitPreparationInterval> intervals = [];
    private readonly string attemptId = Guid.NewGuid().ToString("N");
    private readonly long started = Stopwatch.GetTimestamp();
    private readonly DateTimeOffset startedAt = DateTimeOffset.UtcNow;
    private DateTimeOffset? endedAt;
    private long? ended;
    private string outcome = "running";
    private string? failureKind, failureCode;
    private int executed;
    private long warmFiles, droppedIntervals;
    internal void WarmedFile() => Interlocked.Increment(ref warmFiles);

    internal async Task Run(Func<Task> operation, Func<GitPreparationSnapshot, Task> publish)
    {
        if (Interlocked.Exchange(ref executed, 1) != 0) throw new InvalidOperationException("An attach observation represents one attempt.");
        // Replace an older successful attempt before any attach effects. If
        // explicit diagnostics cannot publish this marker, do not start attach.
        await publish(Observe());
        try { await operation(); }
        catch (Exception error)
        {
            Complete(error);
            await PublishCompletion();
            throw;
        }
        Complete(null);
        await PublishCompletion();

        async Task PublishCompletion()
        {
            try { await publish(Observe()); }
            // Diagnostics must not replace the operation's success, failure or
            // cancellation. The prior running marker remains incomplete.
            catch (Exception) { }
        }
    }

    private void Complete(Exception? error)
    {
        lock (gate)
        {
            ended = Stopwatch.GetTimestamp(); endedAt = DateTimeOffset.UtcNow;
            outcome = error is null ? "succeeded" : error is OperationCanceledException ? "cancelled" : "failed";
            failureKind = error?.GetType().Name;
            failureCode = (error as WorkspaceException)?.Code;
        }
    }

    internal async Task<T> Measure<T>(string stage, Func<Task<T>> operation)
    {
        long start = Stopwatch.GetTimestamp();
        bool succeeded = false;
        try { T result = await operation(); succeeded = true; return result; }
        finally
        {
            long end = Stopwatch.GetTimestamp();
            lock (gate)
            {
                if (intervals.Count == 16) { intervals.RemoveAt(0); droppedIntervals++; }
                intervals.Add(new(stage, start, end, succeeded));
            }
        }
    }

    internal Task Measure(string stage, Func<Task> operation) => Measure(stage, async () => { await operation(); return true; });
    internal GitPreparationSnapshot Observe()
    {
        lock (gate)
            return new(1, attemptId, startedAt, started, endedAt, ended, outcome, failureKind, failureCode, Stopwatch.Frequency,
                Interlocked.Read(ref warmFiles), droppedIntervals, intervals.OrderBy(i => i.Start).ToArray());
    }
}
