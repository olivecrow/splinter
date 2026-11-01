using System.Text.Json;
using Splinter.Core;

namespace Splinter.Runtime;

// Callers retain each store's publication lock while discovering and freezing
// roots. The freeze is drained and durable before Collect obtains writer.lock:
// no history/catalog cursor can self-block its own exclusive collection fence.
public static class MetadataMaintenance
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static MetadataCollectionResult? Collect(string directory, IEnumerable<MetadataTreeRoot> roots,
        string requestKey, IJournalFilePins pins, CancellationToken ct = default, IEnumerable<MetadataEntry>? protectedExternal = null)
    {
        if (!Directory.Exists(directory)) return null;
        var completed = PagedMetadataStore.CollectionReceipt(directory, requestKey); if (completed is not null) return completed;
        using var scratch = new TemporaryMetadataDirectory(); using var frozen = new MetadataSpool(scratch.Path);
        using var residues = new MetadataSpool(scratch.Path); long number = 0;
        foreach (var root in roots)
        { ct.ThrowIfCancellationRequested(); frozen.Append(new((number++).ToString("x16"), JsonSerializer.SerializeToUtf8Bytes(root, Json))); }
        frozen.FlushDurable();
        if (protectedExternal is not null) foreach (var entry in protectedExternal) { ct.ThrowIfCancellationRequested(); residues.Append(entry); }
        residues.FlushDurable();
        IEnumerable<MetadataTreeRoot> Read()
        { foreach (var entry in frozen.Read()) yield return JsonSerializer.Deserialize<MetadataTreeRoot>(entry.Value.Span, Json) ?? throw Invalid("A frozen maintenance root is empty."); }
        return PagedMetadataStore.Collect(directory, Read(), requestKey, pins, ct: ct, protectedExternal: residues.Read());
    }
    public static IEnumerable<MetadataTreeRoot> Roots(object value)
    { foreach (var root in Walk(JsonSerializer.SerializeToElement(value, Json))) yield return root; }
    public static IEnumerable<MetadataTreeRoot> ReadRoots(string path)
    {
        WorkspaceStore.EnsurePlainPath(path);
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var document = JsonDocument.Parse(input);
        if (!JsonPropertyValidation.HasUniqueProperties(document.RootElement)) throw Invalid("Preserve duplicated maintenance root properties.");
        foreach (var root in Walk(document.RootElement)) yield return root;
    }
    private static IEnumerable<MetadataTreeRoot> Walk(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            if (value.TryGetProperty("storeId", out _) && value.TryGetProperty("page", out _))
            {
                var root = value.Deserialize<MetadataTreeRoot>(Json) ?? throw Invalid("A metadata root is empty.");
                if (root.Version != 1 || root.StoreId is not { Length: 24 } || root.StoreId.Any(c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
                    throw Invalid("Preserve unsupported metadata roots.");
                yield return root; yield break;
            }
            foreach (var property in value.EnumerateObject()) foreach (var root in Walk(property.Value)) yield return root;
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) foreach (var root in Walk(item)) yield return root;
    }
    private static WorkspaceException Invalid(string message) => new("MetadataMaintenanceInvalid", message);
}
