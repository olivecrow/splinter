using System.Buffers.Binary;
using Splinter.Core;

namespace Splinter.Runtime;

public sealed record DirectoryMetadataRecord(SnapshotEntry Entry, string? ContentHash, MetadataTreeRoot? Children = null,
    string? SourceReference = null);

// Each directory is an ordered immutable map of component names, never a flat
// project-wide path list. Child map locations are excluded from content identity.
public sealed class DirectoryMetadataTree(PagedMetadataStore store, bool caseSensitive = false)
{
    private static readonly char[] InvalidNameCharacters = ['/', '\\', '\0', ':'];
    private static readonly char[] InvalidPosixNameCharacters = ['/', '\0'];
    private static readonly System.Text.UTF8Encoding Utf8 = new(false, true);
    public MetadataTreeRoot Empty => store.Empty;

    public MetadataTreeRoot Build(IEnumerable<DirectoryMetadataRecord> entries, CancellationToken ct = default) =>
        store.Build(MetadataExternalSort.Order(entries.Select(entry => Encode(entry)), store.WorkingDirectory, ct), ct);

    public MetadataTreeRoot Set(MetadataTreeRoot directory, DirectoryMetadataRecord entry)
    {
        var encoded = Encode(entry);
        return store.Set(directory, encoded.Key, encoded.Value.Span, encoded.Child);
    }
    public MetadataTreeRoot Remove(MetadataTreeRoot directory, string name)
    { RequireName(name); return store.Remove(directory, Key(name)); }

    public DirectoryMetadataRecord? Find(MetadataTreeRoot directory, string name)
    {
        RequireName(name); var entry = store.Open(directory).FindEntry(Key(name));
        return entry is null ? null : Decode(entry);
    }

    public IEnumerable<DirectoryMetadataRecord> Enumerate(MetadataTreeRoot directory, string? afterName = null)
    {
        if (afterName is not null) RequireName(afterName);
        foreach (var entry in store.Open(directory).Enumerate(afterName is null ? null : Key(afterName))) yield return Decode(entry);
    }

    public IEnumerable<DirectoryMetadataRecord> Walk(MetadataTreeRoot directory)
    {
        var pending = new Stack<(string Prefix, IEnumerator<DirectoryMetadataRecord> Items)>();
        pending.Push(("", Enumerate(directory).GetEnumerator()));
        try
        {
            while (pending.TryPeek(out var level))
            {
                if (!level.Items.MoveNext()) { level.Items.Dispose(); pending.Pop(); continue; }
                var item = level.Items.Current;
                string path = level.Prefix.Length == 0 ? item.Entry.Path : level.Prefix + "/" + item.Entry.Path;
                yield return item with { Entry = item.Entry with { Path = path } };
                if (item.Entry.Directory) pending.Push((path, Enumerate(item.Children!).GetEnumerator()));
            }
        }
        finally { foreach (var item in pending) item.Items.Dispose(); }
    }

    private string Key(string name) => caseSensitive ? name : name.ToUpperInvariant();
    internal MetadataEntry Encode(DirectoryMetadataRecord record, bool component = true)
    {
        var entry = record.Entry; if (component) RequireName(entry.Path);
        WorkspaceUnixPermissions.RequireValid(entry.UnixMode);
        if (entry.Length < 0 || !ValidTime(entry.CreationTime) || !ValidTime(entry.LastAccessTime) || !ValidTime(entry.LastWriteTime) ||
            ((entry.Attributes & 16) != 0) != entry.Directory || entry.Directory && (entry.Length != 0 || record.ContentHash is not null) ||
            !entry.Directory && record.Children is not null || record.Children is { Version: not 1 })
            throw Invalid("A directory record has invalid type, length or children.");
        byte[]? digest = null;
        if (record.ContentHash is not null)
        {
            if (record.ContentHash.Length != 64 || record.ContentHash.Any(c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
                throw Invalid("A directory content hash must be canonical SHA-256.");
            digest = Convert.FromHexString(record.ContentHash);
        }
        byte[] name = Utf8.GetBytes(entry.Path);
        byte[]? reference = record.SourceReference is null ? null : Utf8.GetBytes(record.SourceReference);
        byte[] bytes = new byte[checked(46 + name.Length + (entry.UnixMode is null ? 0 : 4) + (digest is null ? 0 : 32) +
            (reference is null ? 0 : 4 + reference.Length))];
        bytes[0] = 1; bytes[1] = entry.Directory ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(2), entry.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(10), entry.Attributes);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(14), entry.CreationTime);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(22), entry.LastAccessTime);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(30), entry.LastWriteTime);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(38), name.Length);
        bytes[42] = entry.UnixMode is null ? (byte)0 : (byte)1;
        bytes[43] = digest is null ? (byte)0 : (byte)1;
        bytes[44] = caseSensitive ? (byte)1 : (byte)0; // Path policy participates in content identity.
        bytes[45] = reference is null ? (byte)0 : (byte)1;
        name.CopyTo(bytes, 46); int position = 46 + name.Length;
        if (entry.UnixMode is { } mode) { BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(position), mode); position += 4; }
        if (digest is not null) { digest.CopyTo(bytes, position); position += digest.Length; }
        if (reference is not null)
        { BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(position), reference.Length); reference.CopyTo(bytes, position + 4); }
        return new(Key(entry.Path), bytes, record.Children);
    }

    internal DirectoryMetadataRecord Decode(MetadataEntry record, bool component = true)
    {
        var bytes = record.Value.Span;
        if (bytes.Length < 46 || bytes[0] != 1 || bytes[1] > 1 || bytes[42] > 1 || bytes[43] > 1 ||
            bytes[44] != (caseSensitive ? 1 : 0) || bytes[45] > 1)
            throw Invalid("A directory record has an invalid format.");
        int length = BinaryPrimitives.ReadInt32LittleEndian(bytes[38..]);
        long prefix = 46L + length + (bytes[42] == 1 ? 4 : 0) + (bytes[43] == 1 ? 32 : 0);
        if (length < 1 || prefix > bytes.Length || bytes[45] == 0 && prefix != bytes.Length || bytes[45] == 1 && prefix + 4 > bytes.Length)
            throw Invalid("A directory record has invalid field lengths.");
        string name;
        try { name = Utf8.GetString(bytes.Slice(46, length)); }
        catch (System.Text.DecoderFallbackException) { throw Invalid("A directory name has invalid UTF-8."); }
        if (component) RequireName(name);
        if (component && record.Key != Key(name)) throw Invalid("A directory name differs from its index key.");
        int position = 46 + length; uint? mode = bytes[42] == 1 ? BinaryPrimitives.ReadUInt32LittleEndian(bytes[position..]) : null;
        if (mode is not null) position += 4;
        string? hash = bytes[43] == 1 ? Convert.ToHexStringLower(bytes.Slice(position, 32)) : null;
        if (hash is not null) position += 32;
        string? reference = null;
        if (bytes[45] == 1)
        {
            int referenceBytes = BinaryPrimitives.ReadInt32LittleEndian(bytes[position..]); position += 4;
            if (referenceBytes < 0 || (long)position + referenceBytes != bytes.Length) throw Invalid("A directory source reference is truncated.");
            try { reference = Utf8.GetString(bytes.Slice(position, referenceBytes)); }
            catch (System.Text.DecoderFallbackException) { throw Invalid("A directory source reference has invalid UTF-8."); }
        }
        var entry = new SnapshotEntry(name, bytes[1] == 1, BinaryPrimitives.ReadInt64LittleEndian(bytes[2..]),
            BinaryPrimitives.ReadUInt32LittleEndian(bytes[10..]), BinaryPrimitives.ReadInt64LittleEndian(bytes[14..]),
            BinaryPrimitives.ReadInt64LittleEndian(bytes[22..]), BinaryPrimitives.ReadInt64LittleEndian(bytes[30..]), mode);
        WorkspaceUnixPermissions.RequireValid(entry.UnixMode);
        if (entry.Length < 0 || !ValidTime(entry.CreationTime) || !ValidTime(entry.LastAccessTime) || !ValidTime(entry.LastWriteTime) ||
            ((entry.Attributes & 16) != 0) != entry.Directory || entry.Directory && (entry.Length != 0 || hash is not null) ||
            !entry.Directory && record.Child is not null)
            throw Invalid("A directory record has invalid metadata.");
        return new(entry, hash, entry.Directory ? record.Child ?? store.Empty : null, reference);
    }
    private void RequireName(string name)
    {
        if (string.IsNullOrEmpty(name) || name is "." or ".." || name.IndexOfAny(caseSensitive ? InvalidPosixNameCharacters : InvalidNameCharacters) >= 0)
            throw Invalid("A directory record requires one ordinary path component.");
    }
    private static bool ValidTime(long value) => value is >= 0 and <= 2650467743999999999L;
    private static WorkspaceException Invalid(string message) => new("DirectoryMetadataInvalid", message);
}
