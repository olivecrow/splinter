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
    internal FileApplyIndex(string directory, Action<string>? boundary = null)
    {
        scratch = new(directory); this.directory = scratch.Path;
        try { pages = new(this.directory, true, boundary); names = new(pages); Root = pages.Empty; }
        catch { scratch.Dispose(); throw; }
    }
    internal MetadataTreeRoot Build(IEnumerable<FileSourceEntry> entries, CancellationToken ct)
    {
        using var input = new MetadataSpool(directory);
        foreach (var entry in entries)
        { ct.ThrowIfCancellationRequested(); input.Append(new(entry.Path, EncodeTemporary(entry))); }
        names.Import(input.Read().Select(entry => entry.Key), ct);
        IEnumerable<MetadataEntry> Encoded()
        {
            foreach (var entry in input.Read())
            {
                ct.ThrowIfCancellationRequested(); var item = DecodeTemporary(entry) ?? throw Invalid();
                yield return new(Key(item.Path), Encode(item, names, alreadyImported: true));
            }
        }
        return pages.Build(MetadataSort.Order(Encoded(), pagesDirectory, ct), ct);
    }
    private string pagesDirectory => directory;
    internal MetadataTreeRoot ApplyVersions(MetadataTreeRoot basis, IEnumerable<AppliedFilePath> applied, CancellationToken ct)
    {
        using var input = new MetadataSpool(directory); long count = 0;
        foreach (var item in applied)
        {
            ct.ThrowIfCancellationRequested();
            if (!FileWorkspaceProvider.ValidPath(item.Path)) throw Invalid();
            input.Append(new(item.Path, item.Version is null ? new byte[] { 0 } : EncodeTemporary(Entry(item)))); count++;
        }
        if (count == 0) return basis;
        IEnumerable<AppliedFilePath> Items()
        {
            foreach (var row in input.Read())
            {
                ct.ThrowIfCancellationRequested(); var entry = DecodeTemporary(row);
                yield return new(row.Key, entry is null ? null : new(entry.Kind, entry.Length, entry.Hash, entry.Attributes, entry.LastWriteUtcTicks, entry.UnixMode));
            }
        }
        FileSourceEntry Entry(AppliedFilePath item) => new(item.Path, item.Version!.Kind, item.Version.Length,
            item.Version.Hash, item.Version.ModifiedUtcTicks, item.Version.Attributes, item.Version.UnixMode);
        // A sparse update should not rebuild an otherwise large basis.
        if (count < Math.Max(64, basis.Count / 16))
        {
            foreach (var item in Items()) { ct.ThrowIfCancellationRequested(); basis = item.Version is null ? Remove(basis, item.Path) : Set(basis, Entry(item)); }
            return basis;
        }
        names.Import(input.Read().Select(entry => entry.Key), ct);
        var changes = MetadataSort.Order(Items().Select(item => new MetadataEntry(Key(item.Path),
            item.Version is null ? ReadOnlyMemory<byte>.Empty : Encode(Entry(item), names, alreadyImported: true))), directory, ct);
        IEnumerable<MetadataEntry> Merge()
        {
            using var left = pages.Open(basis).Enumerate().GetEnumerator(); using var right = changes.GetEnumerator();
            bool a = left.MoveNext(), b = right.MoveNext(); string? previous = null;
            while (a || b)
            {
                ct.ThrowIfCancellationRequested();
                int order = !a ? 1 : !b ? -1 : StringComparer.Ordinal.Compare(left.Current.Key, right.Current.Key);
                if (order < 0) { yield return left.Current; a = left.MoveNext(); continue; }
                if (right.Current.Key == previous) throw Invalid(); previous = right.Current.Key;
                if (right.Current.Value.Length != 0) yield return right.Current;
                if (order == 0) a = left.MoveNext(); b = right.MoveNext();
            }
        }
        return pages.Build(Merge(), ct);
    }
    internal MetadataTreeRoot RetainedParents(MetadataTreeRoot original, MetadataTreeRoot before, MetadataTreeRoot after,
        MetadataTreeRoot included, CancellationToken ct)
    {
        IEnumerable<string> Parents()
        {
            using var b = pages.Open(before).Enumerate().GetEnumerator(); using var a = pages.Open(after).Enumerate().GetEnumerator();
            using var selected = pages.Open(included).Enumerate().GetEnumerator();
            bool hasB = b.MoveNext(), hasA = a.MoveNext(), hasSelected = selected.MoveNext();
            bool Has(IEnumerator<MetadataEntry> cursor, ref bool more, string key)
            {
                while (more && StringComparer.Ordinal.Compare(cursor.Current.Key, key) < 0)
                { ct.ThrowIfCancellationRequested(); more = cursor.MoveNext(); }
                return more && cursor.Current.Key == key;
            }
            foreach (var row in pages.Open(original).Enumerate())
            {
                ct.ThrowIfCancellationRequested();
                if (Has(a, ref hasA, row.Key) || !Has(b, ref hasB, row.Key) || !Has(selected, ref hasSelected, row.Key))
                {
                    string path = Decode(row.Value.Span, names).Path;
                    for (int slash; (slash = path.LastIndexOf('/')) >= 0;) { path = path[..slash]; yield return path; }
                }
            }
        }
        return BuildImportedPaths(Parents(), ct, coalesce: true);
    }
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
    internal MetadataTreeRoot BuildImportedPaths(IEnumerable<string> paths, CancellationToken ct, bool coalesce = false)
    {
        IEnumerable<MetadataEntry> Ordered()
        {
            MetadataEntry? previous = null;
            foreach (var entry in MetadataSort.Order(paths.Select(path => new MetadataEntry(Key(path),
                Convert.FromHexString(PagedPathInterner.Reference(path)))), directory, ct))
            {
                if (coalesce && previous?.Key == entry.Key)
                { if (!previous.Value.Span.SequenceEqual(entry.Value.Span)) throw Invalid(); continue; }
                previous = entry; yield return entry;
            }
        }
        return pages.Build(Ordered(), ct);
    }
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
    // Operation-owned scratch only: the spool key is the sole path copy, with
    // its existing length/checksum envelope. Durable index encoding is separate.
    internal static byte[] EncodeTemporary(FileSourceEntry entry)
    {
        RequireTemporary(entry);
        byte[] bytes = new byte[21 + (entry.Hash is null ? 0 : 32) + (entry.UnixMode is null ? 0 : 4)];
        bytes[0] = (byte)((entry.Kind == "file" ? 1 : 2) | (entry.Hash is null ? 0 : 4) | (entry.UnixMode is null ? 0 : 8));
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(1), entry.Length);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(9), entry.LastWriteUtcTicks);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(17), entry.Attributes);
        if (entry.Hash is not null) Convert.FromHexString(entry.Hash).CopyTo(bytes, 21);
        if (entry.UnixMode is { } mode) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(bytes.Length - 4), mode);
        return bytes;
    }
    internal static FileSourceEntry? DecodeTemporary(MetadataEntry record)
    {
        if (record.Child is not null || !FileWorkspaceProvider.ValidPath(record.Key)) throw Invalid();
        var bytes = record.Value.Span;
        if (bytes.Length == 1 && bytes[0] == 0) return null;
        if (bytes.Length < 21 || bytes[0] > 15 || (bytes[0] & 3) is not (1 or 2) ||
            bytes.Length != 21 + ((bytes[0] & 4) != 0 ? 32 : 0) + ((bytes[0] & 8) != 0 ? 4 : 0)) throw Invalid();
        var entry = new FileSourceEntry(record.Key, (bytes[0] & 3) == 1 ? "file" : "directory", BinaryPrimitives.ReadInt64LittleEndian(bytes[1..]),
            (bytes[0] & 4) != 0 ? Convert.ToHexStringLower(bytes.Slice(21, 32)) : null,
            BinaryPrimitives.ReadInt64LittleEndian(bytes[9..]), BinaryPrimitives.ReadInt32LittleEndian(bytes[17..]),
            (bytes[0] & 8) != 0 ? BinaryPrimitives.ReadUInt32LittleEndian(bytes[^4..]) : null);
        RequireTemporary(entry); return entry;
    }
    private static void RequireTemporary(FileSourceEntry entry)
    {
        if (!FileWorkspaceProvider.ValidPath(entry.Path) || entry.Kind is not ("file" or "directory") || entry.Length < 0 ||
            entry.LastWriteUtcTicks < 0 || entry.LastWriteUtcTicks > DateTime.MaxValue.Ticks || entry.UnixMode is > 0xFFF ||
            entry.Kind == "directory" && (entry.Length != 0 || entry.Hash is not null) ||
            ((entry.Attributes & 16) != 0) != (entry.Kind == "directory") ||
            entry.Hash is not null && (entry.Hash.Length != 64 || entry.Hash.Any(c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f')))) throw Invalid();
    }
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
