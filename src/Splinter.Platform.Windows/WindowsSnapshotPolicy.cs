using Splinter.Core;

namespace Splinter.Platform.Windows;

public static class WindowsSnapshotPolicy
{
    public static void Require(SnapshotReference snapshot)
    {
        if (WorkspacePathPolicy.For(snapshot) != WorkspacePathPolicy.Windows)
            throw new WorkspaceException("PathPolicyUnsupported", "The Windows mount supports its fixed Windows path policy only; preserve other immutable trees.");
    }
    public static void Require(OverlayServeRequest request)
    {
        Require(request.Identity.Snapshot);
        if (request.Identity.Graft is { } graft) Require(graft.Basis.Snapshot);
        if (request.ValidationView?.CacheIdentity is { } cache) Require(cache.Snapshot);
    }
}
