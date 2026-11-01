using System.Collections;
using System.Text.Json.Serialization;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Platform.Windows;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OwnedDirectoryManifest(int Version, SnapshotReference Snapshot, string OwnerId,
    [property: JsonIgnore] IEnumerable<OwnedDirectoryItem> Items) : IDisposable
{
    public const string MetadataDirectoryName = "metadata";
    [JsonRequired] public SnapshotCatalogDescriptor? Catalog { get; init; }
    [JsonIgnore] public string? CatalogDirectory { get; init; }
    [JsonIgnore] internal TemporaryMetadataDirectory? ScratchOwner { get; init; }
    [JsonIgnore] public long ItemCount => Catalog?.Count ?? Items.LongCount();
    public OwnedDirectoryManifest Bind(string directory)
    {
        if (Version != 2 || Catalog is null) throw new WorkspaceException("GeneratedBasisCorrupt", "Retain unsupported generated metadata without migration.");
        string pages = Path.Combine(directory, MetadataDirectoryName);
        return this with { CatalogDirectory = pages, Items = EnumerateItems(pages, Catalog) };
    }
    internal static IEnumerable<OwnedDirectoryItem> EnumerateItems(string directory, SnapshotCatalogDescriptor catalog)
    {
        using var reader = new PagedSnapshotCatalog(directory, catalog);
        foreach (var item in reader.Walk()) yield return new(item.Entry, item.ContentHash);
    }
    public OwnedDirectoryItemIndex OpenIndex(StringComparer? comparer = null) => new(this, comparer ?? StringComparer.OrdinalIgnoreCase);
    public void Dispose() => ScratchOwner?.Dispose();
}

// The index owns a bounded reader, not a materialized dictionary. Native file
// fences remain a separate cost proportional to the complete protected tree.
public sealed class OwnedDirectoryItemIndex : IEnumerable<KeyValuePair<string, OwnedDirectoryItem>>, IDisposable
{
    private readonly PagedSnapshotCatalog catalog;
    private readonly TemporaryMetadataDirectory? scratch;
    private readonly StringComparer comparer;
    internal OwnedDirectoryItemIndex(OwnedDirectoryManifest manifest, StringComparer comparer)
    {
        this.comparer = comparer;
        if (manifest.Catalog is not null && manifest.CatalogDirectory is not null)
        { catalog = new(manifest.CatalogDirectory, manifest.Catalog); return; }
        // Synthetic fixture manifests may supply inline items. Persisted
        // product manifests must Bind before this reader can be reached.
        if (manifest.Items is null) throw new WorkspaceException("GeneratedBasisCorrupt", "The generated metadata catalog is absent.");
        scratch = new();
        try
        {
            var root = manifest.Items.Single(item => item.Entry.Path == "").Entry;
            var descriptor = PagedSnapshotCatalog.Build(scratch.Path, root, manifest.Items.Where(item => item.Entry.Path.Length != 0)
                .Select(item => new DirectoryMetadataRecord(item.Entry, item.Hash)));
            catalog = new(scratch.Path, descriptor);
        }
        catch { try { scratch.Dispose(); } catch (IOException) { } throw; }
    }
    public long Count => catalog.Descriptor.Count;
    public IEnumerable<OwnedDirectoryItem> Values => catalog.Walk().Select(item => new OwnedDirectoryItem(item.Entry, item.ContentHash));
    public IEnumerable<string> Keys => Values.Select(item => item.Entry.Path);
    public OwnedDirectoryItem this[string path] => GetValueOrDefault(path) ?? throw new KeyNotFoundException(path);
    public bool ContainsKey(string path) => GetValueOrDefault(path) is not null;
    public bool TryGetValue(string path, out OwnedDirectoryItem item)
    { item = GetValueOrDefault(path)!; return item is not null; }
    public OwnedDirectoryItem? GetValueOrDefault(string path)
    {
        var item = catalog.Find(path);
        return item is not null && comparer.Equals(path, item.Entry.Path) ? new(item.Entry, item.ContentHash) : null;
    }
    public IEnumerable<OwnedDirectoryItem> EnumerateChildren(string path) => catalog.EnumerateChildren(path).Select(item => new OwnedDirectoryItem(item.Entry, item.ContentHash));
    public void Verify(CancellationToken ct = default) => catalog.Verify(ct);
    public IEnumerator<KeyValuePair<string, OwnedDirectoryItem>> GetEnumerator() => Values.Select(item => KeyValuePair.Create(item.Entry.Path, item)).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public void Dispose() { catalog.Dispose(); scratch?.Dispose(); }
}
