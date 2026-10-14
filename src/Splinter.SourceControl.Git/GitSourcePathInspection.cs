using Splinter.Core;

namespace Splinter.SourceControl.Git;

// One inspection only. Reusing paths inside a mounted source requires the
// caller's before/after journal barrier; ordinary paths are never cached.
internal sealed class GitSourcePathInspection
{
    internal const int MaxCachedDirectories = 4096;
    private const int MaxCachedPathCharacters = 1024 * 1024;
    private static readonly StringComparison Comparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    private readonly string? mountRoot;
    private readonly string? mountPrefix;
    private readonly HashSet<string>? directories;
    private readonly Func<string, FileAttributes?> readAttributes;
    private int cachedPathCharacters;

    internal GitSourcePathInspection(string? ownedMountRoot, bool journalBound = false,
        Func<string, FileAttributes?>? readAttributes = null)
    {
        mountRoot = ownedMountRoot is null ? null : Path.TrimEndingDirectorySeparator(Path.GetFullPath(ownedMountRoot));
        mountPrefix = mountRoot is null ? null : mountRoot + Path.DirectorySeparatorChar;
        if (mountRoot is not null && journalBound)
            directories = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        this.readAttributes = readAttributes ?? ReadAttributes;
    }

    internal FileAttributes? Inspect(string path)
    {
        string full = Path.GetFullPath(path);
        FileAttributes? leaf = null;
        for (string? current = full; current is not null; current = Path.GetDirectoryName(current))
        {
            // The observed mount identity permits this root alone to be a
            // reparse point. Its native ancestors still get fresh queries.
            if (string.Equals(current, mountRoot, Comparison)) continue;
            bool inside = directories is not null && current.StartsWith(mountPrefix!, Comparison);
            if (current != full && inside && directories!.Contains(current))
            {
                current = mountRoot;
                continue;
            }
            var attributes = readAttributes(current);
            if (current == full) leaf = attributes;
            if (attributes is { } value && (value & FileAttributes.ReparsePoint) != 0)
                throw new WorkspaceException("GitSymlinkRejected", "Source path contains a reparse point: " + current);
            if (current != full && inside && attributes is { } directoryAttributes &&
                (directoryAttributes & FileAttributes.Directory) != 0 &&
                directories!.Count < MaxCachedDirectories &&
                current.Length <= MaxCachedPathCharacters - cachedPathCharacters && directories.Add(current))
                cachedPathCharacters += current.Length;
        }
        return leaf;
    }

    private static FileAttributes? ReadAttributes(string path)
    {
        try { return File.GetAttributes(path); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }
}
