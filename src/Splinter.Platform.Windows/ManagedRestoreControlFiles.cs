using Splinter.Runtime;

namespace Splinter.Platform.Windows;

// Atomic control publication can leave its unpublished .GUID.tmp after a
// cancellation/crash. Such a file is never authority, adopted, or reclaimed.
internal static class ManagedRestoreControlFiles
{
    internal static bool IsPublicationTemporary(string path, IReadOnlySet<string> canonical)
    {
        if (!path.EndsWith(".tmp", StringComparison.Ordinal) || path.Length < 37 || path[^37] != '.' ||
            !Guid.TryParseExact(path.AsSpan(path.Length - 36, 32), "N", out _) || !canonical.Contains(path[..^37])) return false;
        WorkspaceStore.EnsurePlainPath(path);
        return (File.GetAttributes(path) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) == 0;
    }
}
