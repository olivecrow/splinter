using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.SourceControl.Files;

// Observational lock membership, including ancestors and descendants, is
// proportional on disk rather than a full in-memory lock-list string array.
internal sealed class PlasticLockIndex : IDisposable
{
    private readonly TemporaryMetadataDirectory scratch = new();
    private readonly PagedSnapshotCatalog catalog;
    public bool HasRootLock { get; private set; }
    public PlasticLockIndex(IEnumerable<string> paths, CancellationToken ct = default)
    {
        try
        {
            IEnumerable<MetadataEntry> Entries()
            {
                foreach (string path in paths)
                {
                    ct.ThrowIfCancellationRequested();
                    if (path.Length == 0) { HasRootLock = true; continue; }
                    yield return new(path.ToUpperInvariant(), ReadOnlyMemory<byte>.Empty);
                }
            }
            var descriptor = PagedSnapshotCatalog.Build(scratch.Path, new("", true, 0, 16, 0, 0, 0),
                SnapshotCatalogInput.WithPathMarkers(Entries(), scratch.Path, caseSensitive: true, ct: ct), caseSensitive: true, ct: ct);
            catalog = new(scratch.Path, descriptor);
        }
        catch { scratch.Dispose(); throw; }
    }
    public bool Affects(string path)
    {
        if (HasRootLock) return true;
        path = path.ToUpperInvariant();
        // An inferred directory means a locked descendant. A marker on any
        // ancestor means a locked enclosing path. Both are point lookups.
        if (catalog.Find(path) is not null) return true;
        for (int slash; (slash = path.LastIndexOf('/')) >= 0;)
        { path = path[..slash]; if (catalog.Find(path)?.SourceReference is not null) return true; }
        return false;
    }
    public IEnumerable<string> Paths()
    {
        if (HasRootLock) yield return "";
        foreach (var record in catalog.Walk().Skip(1))
            if (record.SourceReference is not null) yield return record.Entry.Path;
    }
    public void Dispose() { catalog.Dispose(); scratch.Dispose(); }
}
