namespace Splinter.Core;

// Storage classification only; provider-specific operations stay in adapters.
public static class WorkspaceStorage
{
    public static bool HasRangeJournal(string mode) => mode is "ntfs-snapshot" or "git-snapshot";
}
