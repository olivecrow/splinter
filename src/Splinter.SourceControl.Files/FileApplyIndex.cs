using System.Buffers.Binary;
using System.Text;
using Splinter.Core;
using Splinter.Runtime;
namespace Splinter.SourceControl.Files;

// Working comparison/selection sets spill to the same bounded immutable-page
// engine as durable catalog indices; they never become whole-project maps.
internal sealed class FileApplyIndex : IDisposable
{
    private readonly PagedMetadataStore pages;
    private readonly string directory;
    private readonly TemporaryMetadataDirectory scratch;
    private readonly PagedPathInterner names;
    internal MetadataTreeRoot Root { get; private set; }
    internal FileApplyIndex(string directory)
    {
        scratch = new(directory); this.directory = scratch.Path;
        try { pages = new(this.directory, true); names = new(pages); Root = pages.Empty; }
        catch { scratch.Dispose(); throw; }
    }
    internal MetadataTreeRoot Build(IEnumerable<FileSourceEntry> entries, CancellationToken ct)
    {
        using var input = new MetadataSpool(directory);
        foreach (var entry in entries)
        { ct.ThrowIfCancellationRequested(); input.Append(new(entry.Path, System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(entry))); }
        names.Import(input.Read().Select(entry => entry.Key), ct);
        IEnumerable<MetadataEntry> Encoded()
        {
            foreach (var entry in input.Read())
            {
                var item = System.Text.Json.JsonSerializer.Deserialize<FileSourceEntry>(entry.Value.Span) ?? throw Invalid();
                if (item.Path != entry.Key) throw Invalid();
                yield return new(Key(item.Path), Encode(item, names, alreadyImported: true));
            }
        }
        return pages.Build(MetadataSort.Order(Encoded(), pagesDirectory, ct), ct);
    }
    private string pagesDirectory => directory;
    internal MetadataTreeRoot BuildPaths(IEnumerable<string> paths, CancellationToken ct = default, bool coalesce = false)
    {
        using var input = new MetadataSpool(directory); long index = 0;
        foreach (string path in paths) input.Append(new((index++).ToString("x16"), Encoding.UTF8.GetBytes(path)));
        names.Import(input.Read().Select(entry => Encoding.UTF8.GetString(entry.Value.Span)), ct);
        IEnumerable<MetadataEntry> Entries()
        {
            foreach (var entry in input.Read())
            { string path = Encoding.UTF8.GetString(entry.Value.Span); yield return new(Key(path), Convert.FromHexString(PagedPathInterner.Reference(path))); }
        }
        IEnumerable<MetadataEntry> Ordered()
        {
            MetadataEntry? prior = null;
            foreach (var entry in MetadataSort.Order(Entries(), directory, ct))
            {
                if (coalesce && prior?.Key == entry.Key)
                { if (!prior.Value.Span.SequenceEqual(entry.Value.Span)) throw Invalid(); continue; }
                prior = entry; yield return entry;
            }
        }
        return pages.Build(Ordered(), ct);
    }
    internal bool ContainsExactPath(MetadataTreeRoot root, string path)
    {
        var value = pages.Open(root).Find(Key(path));
        return value is not null && value.Length == 32 && names.Resolve(Convert.ToHexStringLower(value)) == path;
    }
    internal MetadataTreeRoot AddPath(MetadataTreeRoot root, string path) => pages.Set(root, Key(path), Convert.FromHexString(names.Intern(path)));
    internal void Use(MetadataTreeRoot root) => Root = root;
    internal FileSourceEntry? Find(MetadataTreeRoot root, string path)
    { var bytes = pages.Open(root).Find(Key(path)); return bytes is null ? null : Decode(bytes, names); }
    internal bool Contains(MetadataTreeRoot root, string path) => pages.Open(root).Find(Key(path)) is not null;
    internal void Set(string path, FileSourceEntry? entry = null)
    { Root = pages.Set(Root, Key(path), entry is null ? Convert.FromHexString(names.Intern(path)) : Encode(entry, names)); }
    internal MetadataTreeRoot Set(MetadataTreeRoot root, FileSourceEntry entry) => pages.Set(root, Key(entry.Path), Encode(entry, names));
    internal MetadataTreeRoot Remove(MetadataTreeRoot root, string path) => pages.Remove(root, Key(path));
    internal IEnumerable<FileSourceEntry> Entries(MetadataTreeRoot root)
    { foreach (var entry in pages.Open(root).Enumerate()) yield return Decode(entry.Value.Span, names); }
    internal IEnumerable<(FileSourceEntry? Before, FileSourceEntry? After)> Compare(MetadataTreeRoot before, MetadataTreeRoot after)
    {
        using var left = pages.Open(before).Enumerate().GetEnumerator();
        using var right = pages.Open(after).Enumerate().GetEnumerator();
        bool hasLeft = left.MoveNext(), hasRight = right.MoveNext();
        while (hasLeft || hasRight)
        {
            int order = !hasLeft ? 1 : !hasRight ? -1 : StringComparer.Ordinal.Compare(left.Current.Key, right.Current.Key);
            yield return (order <= 0 ? Decode(left.Current.Value.Span, names) : null,
                order >= 0 ? Decode(right.Current.Value.Span, names) : null);
            if (order <= 0) hasLeft = left.MoveNext();
            if (order >= 0) hasRight = right.MoveNext();
        }
    }
    // Paths originate in this index's decoded before/after records, whose
    // components have already been imported and validated.
    internal MetadataTreeRoot BuildImportedPaths(IEnumerable<string> paths, CancellationToken ct) =>
        pages.Build(MetadataSort.Order(paths.Select(path => new MetadataEntry(Key(path),
            Convert.FromHexString(PagedPathInterner.Reference(path)))), directory, ct), ct);
    internal IEnumerable<string> Paths(MetadataTreeRoot root)
    { foreach (var entry in pages.Open(root).Enumerate()) { if (entry.Value.Length != 32) throw Invalid(); yield return names.Resolve(Convert.ToHexStringLower(entry.Value.Span)); } }
    internal MetadataTreeRoot AddText(MetadataTreeRoot root, string text)
    {
        byte[] data = Encoding.UTF8.GetBytes(text); string key = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(data));
        if (data.Length < 48000) return pages.Set(root, key, data);
        IEnumerable<MetadataEntry> Chunks()
        { for (int offset = 0, index = 0; offset < data.Length; offset += 32768, index++) yield return new(index.ToString("x8"), data.AsMemory(offset, Math.Min(32768, data.Length - offset))); }
        var chunks = pages.Build(Chunks()); return pages.Set(root, key, ReadOnlySpan<byte>.Empty, chunks);
    }
    internal IEnumerable<string> Texts(MetadataTreeRoot root)
    {
        foreach (var entry in pages.Open(root).Enumerate())
        {
            if (entry.Child is null) yield return new UTF8Encoding(false, true).GetString(entry.Value.Span);
            else
            {
                using var output = new MemoryStream(); long index = 0;
                foreach (var chunk in pages.Open(entry.Child).Enumerate())
                { if (chunk.Key != index++.ToString("x8") || chunk.Child is not null || chunk.Value.Length > 32768) throw Invalid(); output.Write(chunk.Value.Span); }
                yield return new UTF8Encoding(false, true).GetString(output.GetBuffer(), 0, checked((int)output.Length));
            }
        }
    }
    internal bool Has(string path) => Contains(Root, path);
    internal MetadataTreeRoot Empty => pages.Empty;
    public void Dispose() { pages.Dispose(); scratch.Dispose(); }
    private static string Key(string path) => PagedPathInterner.LookupIdentity(path);
    internal static byte[] Encode(FileSourceEntry entry, PagedPathInterner names, bool alreadyImported = false)
    {
        byte[] name = Convert.FromHexString(alreadyImported ? PagedPathInterner.Reference(entry.Path) : names.Intern(entry.Path)); byte[] bytes = new byte[checked(53 + (entry.Hash is null ? 0 : 32) + (entry.UnixMode is null ? 0 : 4))];
        bytes[0] = (byte)((entry.Kind == "file" ? 3 : 4) | (entry.UnixMode is null ? 0 : 128));
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(1), entry.Length); BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(9), entry.LastWriteUtcTicks);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(17), entry.Attributes);
        name.CopyTo(bytes, 21); if (entry.Hash is not null) Convert.FromHexString(entry.Hash).CopyTo(bytes, 53);
        if (entry.UnixMode is { } mode) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(bytes.Length - 4), mode);
        return bytes;
    }
    internal static FileSourceEntry Decode(ReadOnlySpan<byte> bytes, PagedPathInterner names)
    {
        if (bytes.Length < 1) throw Invalid();
        bool hasMode = (bytes[0] & 128) != 0; int metadataBytes = bytes.Length - (hasMode ? 4 : 0);
        if (metadataBytes is not (53 or 85) || (bytes[0] & 127) is not (3 or 4)) throw Invalid();
        string path = names.Resolve(Convert.ToHexStringLower(bytes.Slice(21, 32)));
        if (!FileWorkspaceProvider.ValidPath(path)) throw Invalid();
        return new(path, (bytes[0] & 127) == 3 ? "file" : "directory", BinaryPrimitives.ReadInt64LittleEndian(bytes[1..]),
            metadataBytes == 53 ? null : Convert.ToHexStringLower(bytes.Slice(53, 32)),
            BinaryPrimitives.ReadInt64LittleEndian(bytes[9..]), BinaryPrimitives.ReadInt32LittleEndian(bytes[17..]),
            hasMode ? BinaryPrimitives.ReadUInt32LittleEndian(bytes[^4..]) : null);
    }
    private static WorkspaceException Invalid() => new("FileSourceStateInvalid", "Preserve malformed source comparison entries.");
}
