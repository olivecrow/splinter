using Splinter.Core;

namespace Splinter.Runtime;

// One immutable cache base, independent of the candidate selected for source.
// Filtering is metadata-only: neither construction nor enumeration materializes
// snapshot payload. The caller owns the supplied snapshot and its read lease.
public sealed class CacheSnapshotTree : ISnapshotTree
{
    private readonly ISnapshotTree basis;
    private readonly HashSet<string> roots;
    private readonly WorkspacePathPolicy policy;
    public SnapshotReference Snapshot => basis.Snapshot;

    public CacheSnapshotTree(ISnapshotTree basis, SnapshotReference expected, string[] roots)
    {
        policy = WorkspacePathPolicy.For(expected);
        if (basis.Snapshot != expected)
            throw new WorkspaceException("ViewCacheMismatch", "The cache requires its original immutable snapshot tuple.");
        if (roots.Length == 0 || roots.Any(p => string.IsNullOrWhiteSpace(p) ||
                policy.Normalize(p) != p || p.Contains('/')) ||
            roots.Distinct(policy.Comparer).Count() != roots.Length)
            throw new WorkspaceException("ViewCacheMismatch", "Use distinct ordinary cache partition roots.");
        this.basis = basis;
        this.roots = new(roots, policy.Comparer);
        basis.EnsureAvailable();
    }

    private bool Includes(string path) => path.Length == 0 || roots.Contains(path.Split('/')[0]);
    public void EnsureAvailable() => basis.EnsureAvailable();
    public SnapshotEntry? Stat(string path)
    {
        path = policy.Normalize(path); EnsureAvailable();
        return Includes(path) ? basis.Stat(path) : null;
    }
    public IReadOnlyList<SnapshotEntry> List(string path)
    {
        path = policy.Normalize(path); EnsureAvailable();
        if (!Includes(path)) throw new DirectoryNotFoundException(path);
        return basis.List(path).Where(e => Includes(policy.Normalize(e.Path))).ToArray();
    }
    public int Read(string path, long offset, Span<byte> destination)
    {
        path = policy.Normalize(path); EnsureAvailable();
        if (!Includes(path)) throw new FileNotFoundException(path);
        return basis.Read(path, offset, destination);
    }
}
