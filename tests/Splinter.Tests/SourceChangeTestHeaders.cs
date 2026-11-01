using Splinter.Core;

namespace Splinter.Tests;

internal static class SourceChangeTestHeaders
{
    // Replay/compaction assertions retain only the immutable header. The
    // caller closes the original reader before any exclusive maintenance;
    // this detached value never enumerates an old paged root.
    internal static SourceChangeTestHeader Detach(SourceChangeView observed) =>
        new(observed.Epoch, observed.Sequence, observed.Changes);
}

internal sealed record SourceChangeTestHeader(string Epoch, long Sequence, SourcePathChange[] Changes);
