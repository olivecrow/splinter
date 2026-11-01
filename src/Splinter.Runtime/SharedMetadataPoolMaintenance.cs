using System.Text.Json;
using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class SharedMetadataPool
{
    public MetadataCollectionResult Collect(string requestKey, CancellationToken ct = default)
    {
        if (filePins is null) throw Invalid("Compose native file ownership before metadata collection.");
        var completed = PagedMetadataStore.CollectionReceipt(Directory, requestKey); if (completed is not null) return completed;
        string storeId;
        using (var pages = new PagedMetadataStore(Directory)) storeId = pages.StoreId;
        IEnumerable<MetadataTreeRoot> Roots()
        {
            // An accepted producer owns this lease before releasing the pool
            // publication lock. Read anchors first; an anchor disappearing is
            // safe only because its complete fixed header is now discoverable.
            string anchors = Path.Combine(Directory, "anchors");
            if (System.IO.Directory.Exists(anchors))
                foreach (string path in System.IO.Directory.EnumerateFiles(anchors, "*.json"))
                {
                    ct.ThrowIfCancellationRequested(); IEnumerable<MetadataTreeRoot> captured;
                    using var held = OpenOptional(path);
                    if (held is null) continue;
                    using var document = JsonDocument.Parse(held);
                    if (!JsonPropertyValidation.HasUniqueProperties(document.RootElement)) throw Invalid("Preserve duplicated anchor properties.");
                    var value = document.RootElement.Deserialize<Reuse>(Json) ?? throw Invalid("A protected metadata anchor is empty.");
                    if (value.Version != 1) throw Invalid("Preserve an unsupported metadata anchor.");
                    captured = [value.Catalog.Children, value.PayloadIndex, value.PayloadPaths];
                    foreach (var root in captured)
                    { if (root.StoreId != storeId) throw Invalid("A metadata anchor belongs to another store."); yield return root; }
                }
            string records = Path.Combine(home, "records");
            if (System.IO.Directory.Exists(records))
                foreach (string control in System.IO.Directory.EnumerateDirectories(records))
                {
                    ct.ThrowIfCancellationRequested(); WorkspaceStore.EnsurePlainPath(control);
                    string id = Path.GetFileName(control);
                    if (id.Length != 24 || id.Any(c => !char.IsAsciiHexDigit(c))) continue;
                    string registry = Path.Combine(control, "workspace.json");
                    using var record = OpenOptional(registry);
                    if (record is null)
                    {
                        // A producer before its initial task publication cannot
                        // have a fixed file header without an active anchor.
                        if (System.IO.Directory.Exists(Path.Combine(control, "files")))
                            throw Invalid("Preserve source headers without their registered workspace owner.");
                        continue;
                    }
                    using var document = JsonDocument.Parse(record);
                    if (!JsonPropertyValidation.HasUniqueProperties(document.RootElement)) throw Invalid("Preserve duplicated workspace root properties.");
                    if (!document.RootElement.TryGetProperty("version", out var version) || !version.TryGetInt32(out int actualVersion))
                        throw Invalid("Preserve a malformed workspace registry during shared metadata collection.");
                    if (actualVersion != WorkspaceRecord.CurrentVersion) continue; // Previous formats never own this v4 pool.
                    if (document.RootElement.GetProperty("id").GetString() != id ||
                        document.RootElement.GetProperty("requestDigest").ValueKind != JsonValueKind.String)
                        throw Invalid("A shared metadata owner differs from its registered control directory.");
                    foreach (string path in FileHeaders(control))
                    {
                        using var input = OpenOptional(path); if (input is null) continue;
                        using var header = JsonDocument.Parse(input);
                        if (!JsonPropertyValidation.HasUniqueProperties(header.RootElement)) throw Invalid("Preserve duplicated fixed metadata header properties.");
                        if (header.RootElement.TryGetProperty("catalog", out var catalogElement) && header.RootElement.TryGetProperty("payloadIndex", out var payloadElement))
                        {
                            var catalog = catalogElement.Deserialize<SnapshotCatalogDescriptor>(Json) ?? throw Invalid("A retained source catalog root is empty.");
                            var payload = payloadElement.Deserialize<MetadataTreeRoot>(Json) ?? throw Invalid("A retained source payload index is empty.");
                            var paths = header.RootElement.GetProperty("payloadPaths").Deserialize<MetadataTreeRoot>(Json) ?? throw Invalid("A retained source path index is empty.");
                            if (header.RootElement.GetProperty("version").GetInt32() != 4 || header.RootElement.GetProperty("ownerId").GetString() != id ||
                                header.RootElement.GetProperty("requestDigest").GetString() != document.RootElement.GetProperty("requestDigest").GetString() ||
                                catalog.Children.StoreId != storeId || payload.StoreId != storeId || paths.StoreId != storeId ||
                                header.RootElement.GetProperty("identity").GetString() != "sha256:" + catalog.Children.ContentHash)
                                throw Invalid("Preserve a foreign or malformed retained source root.");
                        }
                        foreach (var root in MetadataMaintenance.Roots(header.RootElement))
                            if (root.StoreId == storeId) yield return root;
                    }
                }
            // Reuse hints are deliberately not data authority. Remove only
            // validated optimization controls, after all real roots are frozen.
            foreach (string path in System.IO.Directory.EnumerateFiles(Directory, "reuse-*.json"))
            {
                var value = Read<Reuse>(path);
                if (value.Version != 1 || value.Catalog.Children.StoreId != storeId || value.PayloadIndex.StoreId != storeId || value.PayloadPaths.StoreId != storeId)
                    throw Invalid("Preserve an unknown metadata reuse control.");
                File.Delete(path);
            }
        }
        return MetadataMaintenance.Collect(Directory, Roots(), requestKey, filePins, ct)
            ?? throw Invalid("The owned shared metadata store disappeared.");
    }
    private static FileStream? OpenOptional(string path)
    {
        WorkspaceStore.EnsurePlainPath(path);
        try { return new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete); }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException) { return null; }
    }
    private static IEnumerable<string> FileHeaders(string control)
    {
        string files = Path.Combine(control, "files");
        if (!System.IO.Directory.Exists(files)) yield break;
        foreach (string path in System.IO.Directory.EnumerateFiles(files, "*.json")) yield return path;
        string generations = Path.Combine(files, "generations");
        if (System.IO.Directory.Exists(generations))
            foreach (string directory in System.IO.Directory.EnumerateDirectories(generations))
            {
                WorkspaceStore.EnsurePlainPath(directory);
                foreach (string path in System.IO.Directory.EnumerateFiles(directory, "*.json")) yield return path;
            }
    }
}
