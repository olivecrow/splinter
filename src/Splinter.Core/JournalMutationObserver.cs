namespace Splinter.Core;

// Explicit development instrumentation. Ordinary stores have no observer.
// A callback must not read/flush the journal: doing so changes the interrupted boundary.
public sealed record JournalMutationBoundary(string Boundary, string Operation, long Sequence,
    long FrameStart, long FrameBytes, int PayloadBytes);

public interface IJournalMutationObserver
{
    void Observe(JournalMutationBoundary boundary);
}
