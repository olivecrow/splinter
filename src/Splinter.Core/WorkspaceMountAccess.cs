namespace Splinter.Core;

// A provider-neutral identity rule for the one filesystem root adapters may
// admit as a mount. It grants no exception to ancestors or entries inside it.
public static class WorkspaceMountAccess
{
    public static string? ObservedRoot(WorkspaceRecord? workspace)
    {
        if (workspace?.Snapshot is not { Reference: { } snapshot, Mount: { } mount,
                Observation: { SnapshotAvailable: true, Mounted: true, ProviderAlive: true, OwnershipVerified: true } } ||
            mount.Id != workspace.Id || mount.Snapshot != snapshot ||
            !Path.IsPathFullyQualified(mount.Path) || !Path.IsPathFullyQualified(workspace.Path)) return null;
        return string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(mount.Path)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(workspace.Path)),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
            ? workspace.Path : null;
    }
}
