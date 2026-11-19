using System.Text;
using Splinter.Core;
using Splinter.Runtime;
namespace Splinter.SourceControl.Files;

public sealed record FileResultPage(string Root, long Count, FileSourceEntry[] Entries, string? NextCursor);
public sealed record AppliedFilePath(string Path, OriginalFileVersion? Version)
{
    internal FileSourceEntry? ToEntry() => Version is null ? null : new(Path, Version.Kind, Version.Length,
        Version.Hash, Version.ModifiedUtcTicks, Version.Attributes, Version.UnixMode);
}
public sealed record AppliedPathsPage(string Root, long Count, AppliedFilePath[] Entries, string? NextCursor);

public sealed partial class FileWorkspaceProvider
{
    public static FileResultPage ReadResultPage(string control, string resultId, string? cursor = null, int pageSize = 128)
    {
        if (pageSize is < 1 or > 1024) throw new WorkspaceException("CursorInvalid", "Choose a processing page size between 1 and 1024.");
        string path = GenerationPath(control, resultId, "result.json");
        var manifest = Read<FileSourceManifest>(path);
        if (manifest.Version != 4 || manifest.Catalog is null || manifest.Identity != "sha256:" + manifest.Catalog.Children.ContentHash)
            throw Error("The immutable source result header is invalid.");
        string directory = FilePath(control, "metadata");
        using var catalog = new PagedSnapshotCatalog(directory, manifest.Catalog);
        string? after = ReadCursor(cursor, manifest.Identity); var entries = new List<FileSourceEntry>(pageSize); bool more = false;
        foreach (var record in catalog.Walk(after))
        {
            if (record.Entry.Path.Length == 0) continue;
            if (entries.Count == pageSize) { more = true; break; }
            entries.Add(new(record.Entry.Path, record.Entry.Directory ? "directory" : "file", record.Entry.Length,
                record.ContentHash, DateTime.FromFileTimeUtc(record.Entry.LastWriteTime).Ticks, (int)record.Entry.Attributes, record.Entry.UnixMode));
        }
        return new(manifest.Identity, manifest.Catalog.Count - 1, entries.ToArray(),
            more ? WriteCursor(manifest.Identity, entries[^1].Path) : null);
    }
    public static AppliedPathsPage ReadAppliedPathsPage(string control, MetadataTreeRoot? root, MetadataTreeRoot? pathNames = null, string? cursor = null, int pageSize = 128)
    {
        if (pageSize is < 1 or > 1024) throw new WorkspaceException("CursorInvalid", "Choose a processing page size between 1 and 1024.");
        if (root is null)
        {
            if (cursor is not null) throw new WorkspaceException("CursorInvalid", "The applied path root changed.");
            return new("sha256:" + new MetadataTreeRoot(1, "", null).ContentHash, 0, [], null);
        }
        string hash = "sha256:" + root.ContentHash; string? after = ReadCursor(cursor, hash); var entries = new List<AppliedFilePath>(pageSize); bool more = false;
        using var pages = new PagedMetadataStore(FilePath(control, "applied-pages"));
        if (pathNames is null) throw Error("The applied path spelling root is missing.");
        var names = new PagedPathInterner(pages, pathNames); string? last = null;
        foreach (var record in pages.Open(root).Enumerate(after))
        {
            if (entries.Count == pageSize) { more = true; break; }
            entries.Add(DecodeApplied(record.Value.Span, names)); last = record.Key;
        }
        return new(hash, root.Count, entries.ToArray(), more ? WriteCursor(hash, last!) : null);
    }
    private static string? ReadCursor(string? cursor, string root)
    {
        if (cursor is null) return null;
        try
        {
            string value = new UTF8Encoding(false, true).GetString(Convert.FromBase64String(cursor)); int split = value.IndexOf('\n');
            if (split < 0 || value[..split] != root || value.Length == split + 1) throw new FormatException();
            return value[(split + 1)..];
        }
        catch (Exception ex) when (ex is FormatException or DecoderFallbackException)
        { throw new WorkspaceException("CursorInvalid", "The cursor is malformed or belongs to another immutable root."); }
    }
    private static string WriteCursor(string root, string after) => Convert.ToBase64String(Encoding.UTF8.GetBytes(root + "\n" + after));
    private static IEnumerable<AppliedFilePath> EnumerateAppliedPaths(string control, MetadataTreeRoot? root, MetadataTreeRoot? pathNames)
    {
        if (root is null) yield break;
        using var pages = new PagedMetadataStore(FilePath(control, "applied-pages"));
        if (pathNames is null) throw Error("The applied path spelling root is missing.");
        var names = new PagedPathInterner(pages, pathNames);
        foreach (var record in pages.Open(root).Enumerate())
        {
            var applied = DecodeApplied(record.Value.Span, names);
            if (record.Key != PagedPathInterner.LookupIdentity(applied.Path)) throw Error("Applied path spelling differs from its index.");
            yield return applied;
        }
    }
    private static AppliedFilePath DecodeApplied(ReadOnlySpan<byte> bytes, PagedPathInterner names)
    {
        if (bytes.Length < 2 || bytes[0] > 1) throw Error("The applied path version is malformed.");
        if (bytes[0] == 0)
        { if (bytes.Length != 33) throw Error("The deletion path reference is invalid."); return new(names.Resolve(Convert.ToHexStringLower(bytes[1..])), null); }
        var entry = FileApplyIndex.Decode(bytes[1..], names); return new(entry.Path, VersionOf(entry));
    }
    private sealed class AppliedPathReader : IDisposable
    {
        private readonly PagedMetadataStore pages;
        private readonly IReadOnlyMetadataTree paths;
        private readonly PagedPathInterner names;
        internal AppliedPathReader(string control, MetadataTreeRoot root, MetadataTreeRoot? pathNames, CancellationToken ct)
        {
            pages = new(FilePath(control, "applied-pages"));
            try
            {
                paths = pages.Open(root);
                names = new(pages, pathNames ?? throw Error("The applied path spelling root is missing."));
                foreach (var row in paths.Enumerate())
                {
                    ct.ThrowIfCancellationRequested();
                    var entry = DecodeApplied(row.Value.Span, names);
                    if (!ValidPath(entry.Path) || PagedPathInterner.LookupIdentity(entry.Path) != row.Key)
                        throw Error("Applied path spelling differs from its index.");
                }
            }
            catch { pages.Dispose(); throw; }
        }
        internal AppliedFilePath? Find(string path) => paths.Find(PagedPathInterner.LookupIdentity(path)) is { } data
            ? DecodeApplied(data, names) : null;
        public void Dispose() => pages.Dispose();
    }
    private void SaveAppliedPaths(WorkspaceRecord w)
        => (w.AppliedPaths, w.AppliedPathNames) = SaveAppliedPaths(Control(w), w.OriginalApply!, w.AppliedPaths, w.AppliedPathNames);

    private (MetadataTreeRoot, MetadataTreeRoot) SaveAppliedPaths(string control, OriginalApplyState completed,
        MetadataTreeRoot? previousPaths, MetadataTreeRoot? previousNames)
    {
        using var pages = new PagedMetadataStore(FilePath(control, "applied-pages"), true, filePins: contentPins);
        var root = previousPaths ?? pages.Empty;
        var names = new PagedPathInterner(pages, previousNames);
        foreach (var change in completed.Plan.EnumerateChanges())
        {
            byte[] data;
            if (change.After is null)
            { byte[] name = Convert.FromHexString(names.Intern(change.Path)); data = new byte[33]; name.CopyTo(data, 1); }
            else
            {
                var version = change.After;
                byte[] entry = FileApplyIndex.Encode(new(change.Path, version.Kind, version.Length, version.Hash, version.ModifiedUtcTicks, version.Attributes, version.UnixMode), names);
                data = new byte[1 + entry.Length]; data[0] = 1; entry.CopyTo(data, 1);
            }
            root = pages.Set(root, PagedPathInterner.LookupIdentity(change.Path), data);
        }
        pages.Flush(); return (root, names.Root);
    }
}
