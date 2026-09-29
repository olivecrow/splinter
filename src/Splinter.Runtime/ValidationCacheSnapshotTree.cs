using Splinter.Core;

namespace Splinter.Runtime;

// One immutable cache base, independent of the candidate selected for source.
// Filtering is metadata-only: neither construction nor enumeration materializes
// snapshot payload. The caller owns the supplied snapshot and its read lease.
public sealed class ValidationCacheSnapshotTree : ISnapshotTree
{
    private readonly ISnapshotTree basis;
    private readonly HashSet<string> roots;
    public SnapshotReference Snapshot => basis.Snapshot;

    public ValidationCacheSnapshotTree(ISnapshotTree basis, SnapshotReference expected, string[] roots)
    {
        if (basis.Snapshot != expected)
            throw new WorkspaceException("ValidationCacheMismatch", "The cache requires its original immutable snapshot tuple.");
        if (roots.Length == 0 || roots.Any(p => string.IsNullOrWhiteSpace(p) ||
                JournalDeltaStore.Normalize(p) != p || p.Contains('/')) ||
            roots.Distinct(StringComparer.OrdinalIgnoreCase).Count() != roots.Length)
            throw new WorkspaceException("ValidationCacheMismatch", "Use distinct ordinary cache partition roots.");
        this.basis = basis;
        this.roots = new(roots, StringComparer.OrdinalIgnoreCase);
        basis.EnsureAvailable();
    }

    private bool Includes(string path) => path.Length == 0 || roots.Contains(path.Split('/')[0]);
    public void EnsureAvailable() => basis.EnsureAvailable();
    public SnapshotEntry? Stat(string path)
    {
        path = JournalDeltaStore.Normalize(path); EnsureAvailable();
        return Includes(path) ? basis.Stat(path) : null;
    }
    public IReadOnlyList<SnapshotEntry> List(string path)
    {
        path = JournalDeltaStore.Normalize(path); EnsureAvailable();
        if (!Includes(path)) throw new DirectoryNotFoundException(path);
        return basis.List(path).Where(e => Includes(JournalDeltaStore.Normalize(e.Path))).ToArray();
    }
    public int Read(string path, long offset, Span<byte> destination)
    {
        path = JournalDeltaStore.Normalize(path); EnsureAvailable();
        if (!Includes(path)) throw new FileNotFoundException(path);
        return basis.Read(path, offset, destination);
    }
}
