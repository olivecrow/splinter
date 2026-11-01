using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;

namespace Splinter.Runtime;

public static class BoundedParallel
{
    // Results may arrive out of order; callers must own their final ordering.
    // A bounded output queue overlaps enumeration, I/O and consumption. Early
    // disposal/cancellation always joins admitted work before returning.
    public static IEnumerable<TResult> Select<T, TResult>(IEnumerable<T> input, Func<T, CancellationToken, TResult> transform,
        int workers, CancellationToken ct)
    {
        if (workers is < 1 or > 32) throw new ArgumentOutOfRangeException(nameof(workers));
        if (workers == 1)
        { foreach (var item in input) { ct.ThrowIfCancellationRequested(); yield return transform(item, ct); } yield break; }
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var results = new BlockingCollection<TResult>(workers * 2);
        ExceptionDispatchInfo? failure = null;
        IEnumerable<T> GuardInput()
        {
            using var cursor = input.GetEnumerator();
            while (true)
            {
                bool more;
                try { stop.Token.ThrowIfCancellationRequested(); more = cursor.MoveNext(); }
                catch (Exception error)
                { Interlocked.CompareExchange(ref failure, ExceptionDispatchInfo.Capture(error), null); stop.Cancel(); throw; }
                if (!more) yield break;
                yield return cursor.Current;
            }
        }
        var work = Task.Run(() =>
        {
            try
            {
                Parallel.ForEach(Partitioner.Create(GuardInput(), EnumerablePartitionerOptions.NoBuffering),
                    new ParallelOptions { MaxDegreeOfParallelism = workers, CancellationToken = stop.Token },
                    item =>
                    {
                        try { results.Add(transform(item, stop.Token), stop.Token); }
                        catch (Exception error)
                        {
                            Interlocked.CompareExchange(ref failure, ExceptionDispatchInfo.Capture(error), null);
                            stop.Cancel(); throw;
                        }
                    });
            }
            catch (Exception error)
            {
                var actual = error is AggregateException aggregate ? aggregate.Flatten().InnerExceptions[0] : error;
                Interlocked.CompareExchange(ref failure, ExceptionDispatchInfo.Capture(actual), null);
            }
            finally { results.CompleteAdding(); }
        }, CancellationToken.None);
        try
        {
            foreach (var result in results.GetConsumingEnumerable(ct)) yield return result;
            work.GetAwaiter().GetResult(); failure?.Throw(); ct.ThrowIfCancellationRequested();
        }
        finally { stop.Cancel(); work.GetAwaiter().GetResult(); }
    }
}
