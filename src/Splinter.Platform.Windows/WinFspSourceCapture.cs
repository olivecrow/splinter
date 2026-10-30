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
        lock (mutationGate)
        {
            long entered = Stopwatch.GetTimestamp();
            try
            {
                return store is JournalDeltaStore journal ? journal.CaptureSource(directory, id, managedPaths)
                    : throw new WorkspaceException("SourceCaptureUnsupported", "Only the owned task journal can capture source.");
            }
            finally { LastSourceCaptureCallbacks = new(Stopwatch.Frequency, drained - started, entered - drained, Stopwatch.GetTimestamp() - entered); }
        }
    }
}
