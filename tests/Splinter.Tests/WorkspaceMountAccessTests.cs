using Splinter.Core;

namespace Splinter.Tests;

public sealed class WorkspaceMountAccessTests
{
    [Theory]
    [InlineData("complete", true)]
    [InlineData("unknown-snapshot", false)]
    [InlineData("missing-snapshot", false)]
    [InlineData("unmounted", false)]
    [InlineData("dead-provider", false)]
    [InlineData("unowned", false)]
    [InlineData("other-id", false)]
    [InlineData("other-snapshot", false)]
    [InlineData("other-path", false)]
    [InlineData("relative-path", false)]
    public void MountAdmissionRequiresTheCompleteIdentityAndObservation(string variant, bool allowed)
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "splinter-mount-contract"));
        var snapshot = new SnapshotReference("snapshot", "fake", root, "immutable", DateTimeOffset.UtcNow, true, true, "fake");
        var w = new WorkspaceRecord { Id = "workspace", RequestKey = "task", ProjectPath = root + "-source", Path = root,
            BaseReference = "base", SourceReference = "result", StorageMode = "future-provider",
            Snapshot = new() { Reference = snapshot, Mount = new("workspace", root, "delta", snapshot, 1, 1),
                Observation = new(true, true, true, true, 0, 0, 0) } };
        var state = w.Snapshot;
        switch (variant)
        {
            case "unknown-snapshot": state.Observation = state.Observation with { SnapshotAvailable = null }; break;
            case "missing-snapshot": state.Observation = state.Observation with { SnapshotAvailable = false }; break;
            case "unmounted": state.Observation = state.Observation with { Mounted = false }; break;
            case "dead-provider": state.Observation = state.Observation with { ProviderAlive = false }; break;
            case "unowned": state.Observation = state.Observation with { OwnershipVerified = false }; break;
            case "other-id": state.Mount = state.Mount with { Id = "other" }; break;
            case "other-snapshot": state.Mount = state.Mount with { Snapshot = snapshot with { Id = "other" } }; break;
            case "other-path": state.Mount = state.Mount with { Path = root + "-other" }; break;
            case "relative-path": state.Mount = state.Mount with { Path = "relative" }; break;
        }
        Assert.Equal(allowed ? root : null, WorkspaceMountAccess.ObservedRoot(w));
    }
}
