namespace Splinter.Core;

// Optional development observation, independent of the filesystem adapter API.
public sealed record OverlayMutationBoundary(string Boundary, string Operation, string Path,
    long RequestedBytes, long CompletedBytes);

public interface IOverlayMutationObserver
{
    void Observe(OverlayMutationBoundary boundary);
}
