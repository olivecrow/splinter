using Splinter.Core;

namespace Splinter.Runtime;

// A fixed source tree and a fixed generated subtree have separate identities.
// Every mount/journal persists both; this never reads a mutable source directory.
public sealed class GraftedSnapshotTree : ISnapshotTree
{
    private readonly ISnapshotTree source, generated;
    private readonly string root;
    private readonly WorkspacePathPolicy policy;
    public SnapshotReference Snapshot => source.Snapshot;
    public SnapshotGraft? Graft { get; }
    public GraftedSnapshotTree(ISnapshotTree source, ISnapshotTree generated, SnapshotGraft graft)
    {
        policy = WorkspacePathPolicy.For(source.Snapshot);
        root = policy.Normalize(graft.RelativePath);
        if (generated.Snapshot.PathPolicy != source.Snapshot.PathPolicy || root.Length == 0 || root.Contains('/') || root != graft.Basis.RelativePath || generated.Snapshot != graft.Basis.Snapshot)
            throw new WorkspaceException("GeneratedBasisMismatch", "A graft requires the exact fixed generated subtree.");
        if (source.Stat(root) is not null) throw new WorkspaceException("GeneratedBasisTracked", "The source basis already contains this subtree; preserve both without shadowing it.");
        this.source = source; this.generated = generated; Graft = graft;
        EnsureAvailable();
    }
    public void EnsureAvailable() { source.EnsureAvailable(); generated.EnsureAvailable(); }
    private string? Lower(string path) => path.Equals(root, policy.Comparison) ? "" :
        path.StartsWith(root + "/", policy.Comparison) ? path[(root.Length + 1)..] : null;
    private SnapshotEntry Lift(SnapshotEntry e) => e with { Path = e.Path.Length == 0 ? root : root + "/" + e.Path };
    public SnapshotEntry? Stat(string path)
    {
        path = policy.Normalize(path); EnsureAvailable();
        return Lower(path) is { } lower ? generated.Stat(lower) is { } entry ? Lift(entry) : null : source.Stat(path);
    }
    public IReadOnlyList<SnapshotEntry> List(string path)
    {
        path = policy.Normalize(path); EnsureAvailable();
        if (Lower(path) is { } lower) return generated.List(lower).Select(Lift).ToArray();
        var entries = source.List(path);
        return path.Length == 0 ? [.. entries, Lift(generated.Stat("") ?? throw new WorkspaceException("SnapshotMissing", "Generated root is missing."))] : entries;
    }
    public int Read(string path, long offset, Span<byte> destination)
    {
        path = policy.Normalize(path); EnsureAvailable();
        return Lower(path) is { } lower ? generated.Read(lower, offset, destination) : source.Read(path, offset, destination);
    }
}
