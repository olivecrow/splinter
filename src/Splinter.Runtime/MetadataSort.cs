using Splinter.Core;
namespace Splinter.Runtime;
// Public bounded sorting port shared by provider catalogs and content indices.
public static class MetadataSort
{
    public static IEnumerable<MetadataEntry> Order(IEnumerable<MetadataEntry> entries, string workingDirectory, CancellationToken ct = default)
        => MetadataExternalSort.Order(entries, workingDirectory, ct);
}
