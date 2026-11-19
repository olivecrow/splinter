using System.Diagnostics;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Platform.Windows;

public sealed partial class WinFspOverlayFileSystem
{
    internal sealed record CaptureCallbackTiming(long StopwatchFrequency, long AcceptedWriteDrainTicks, long MutationGateWaitTicks, long MutationPauseTicks);
    internal CaptureCallbackTiming? LastSourceCaptureCallbacks { get; private set; }
    internal JournalSourceCapture CaptureSource(string directory, string id, string[] managedPaths)
    {
        long started = Stopwatch.GetTimestamp();
        DrainAcceptedWrites();
        long drained = Stopwatch.GetTimestamp();
        JournalSourceCapture capture;
        lock (mutationGate)
        {
            long entered = Stopwatch.GetTimestamp();
            try
            {
                var journal = store is PartitionedWorkspaceDeltaStore partitioned ? partitioned.SourceStore : store as JournalDeltaStore;
                capture = journal is not null ? journal.CaptureSource(directory, id, managedPaths, deferBackingPins: journal.Identity.Version == 3)
                    : throw new WorkspaceException("SourceCaptureUnsupported", "Only the owned task journal can capture source.");
            }
            finally { LastSourceCaptureCallbacks = new(Stopwatch.Frequency, drained - started, entered - drained, Stopwatch.GetTimestamp() - entered); }
        }
        if (capture.Identity.Version == 3 && !File.Exists(Path.Combine(directory, "materialized.json")))
            SourceCaptureTree.EnsurePins(directory, capture, new WindowsJournalFilePins());
        return capture;
    }
    internal void AdoptCapturedRanges(string control, string id, CancellationToken ct = default)
    {
        DrainAcceptedWrites();
        var journal = store is PartitionedWorkspaceDeltaStore partitioned ? partitioned.SourceStore : store as JournalDeltaStore;
        if (journal is null) throw new WorkspaceException("SourceRangeAdoptionUnsupported", "Require the exact owned task journal.");
        journal.AdoptCapturedRanges(control, id, ct);
    }
}
