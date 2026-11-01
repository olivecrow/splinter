namespace Splinter.Core;

// Addresses locate immutable pages; ContentHash identifies their logical
// contents independently of their location in a container.
[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataPageReference(
    [property: System.Text.Json.Serialization.JsonRequired] long Container,
    [property: System.Text.Json.Serialization.JsonRequired] long Offset,
    [property: System.Text.Json.Serialization.JsonRequired] int Bytes,
    [property: System.Text.Json.Serialization.JsonRequired] long Count,
    [property: System.Text.Json.Serialization.JsonRequired] string ContentHash);
[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataTreeRoot(
    [property: System.Text.Json.Serialization.JsonRequired] int Version,
    [property: System.Text.Json.Serialization.JsonRequired] string StoreId,
    [property: System.Text.Json.Serialization.JsonRequired, System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.Never)] MetadataPageReference? Page)
{
    [System.Text.Json.Serialization.JsonIgnore] public long Count => Page?.Count ?? 0;
    [System.Text.Json.Serialization.JsonIgnore] public string ContentHash => Page?.ContentHash ?? "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
}
public sealed record MetadataEntry(string Key, ReadOnlyMemory<byte> Value, MetadataTreeRoot? Child = null);
public sealed record MetadataCursor(string RootHash, string AfterKey);
public sealed record MetadataReadPage(MetadataTreeRoot Root, MetadataEntry[] Entries, MetadataCursor? NextCursor);

public interface IReadOnlyMetadataTree
{
    MetadataTreeRoot Root { get; }
    byte[]? Find(string key);
    MetadataEntry? FindEntry(string key);
    MetadataEntry? Floor(string key);
    IEnumerable<MetadataEntry> Enumerate(string? afterKey = null);
    MetadataReadPage ReadPage(MetadataCursor? cursor = null, int pageSize = 128);
    void Verify(CancellationToken cancellationToken = default);
}
