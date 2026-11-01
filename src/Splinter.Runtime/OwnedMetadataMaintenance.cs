namespace Splinter.Runtime;

public sealed partial class OwnedContentStore
{
    public MetadataCollectionResult? CollectMetadata(string requestKey, CancellationToken ct = default)
    {
        if (!Directory.Exists(Metadata)) return null;
        using var held = Lock(ct);
        var completed = PagedMetadataStore.CollectionReceipt(Metadata, requestKey); if (completed is not null) return completed;
        Catalog catalog;
        using (var pages = new PagedMetadataStore(Metadata)) catalog = Load(pages);
        return MetadataMaintenance.Collect(Metadata, [catalog.Objects, catalog.References, catalog.PreservedScratch], requestKey, pins, ct);
    }
}

public sealed partial class OwnedRangeContentStore
{
    public MetadataCollectionResult? CollectMetadata(string requestKey, CancellationToken ct = default)
    {
        if (!Directory.Exists(Metadata)) return null;
        using var held = Lock(ct);
        var completed = PagedMetadataStore.CollectionReceipt(Metadata, requestKey); if (completed is not null) return completed;
        Catalog catalog;
        using (var pages = new PagedMetadataStore(Metadata)) catalog = Load(pages);
        return MetadataMaintenance.Collect(Metadata, [catalog.Objects, catalog.References, catalog.Backings, catalog.PreservedScratch], requestKey, pins, ct);
    }
}
