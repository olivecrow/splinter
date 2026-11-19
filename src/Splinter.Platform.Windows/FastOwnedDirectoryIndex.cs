using System.Security.Cryptography;
using System.Text;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Platform.Windows;

// Compact flat binary metadata index for Unity's disposable generated Library basis.
// Replaces per-page cryptographic treaps with ~15ms in-memory load and O(1) Stat/List,
// protected by a trailing SHA-256 integrity checksum bound to manifest.json.
internal sealed class FastOwnedDirectoryIndex
{
    private static ReadOnlySpan<byte> Magic => "SOI2"u8;
    private const int FormatVersion = 2;
    private const int ChecksumLength = 32;
    private const int HeaderLength = 4 + 4 + 4 + 8;

    private readonly Dictionary<string, SnapshotEntry> entries;
    private readonly Dictionary<string, SnapshotEntry[]> children;

    public long Count => entries.Count;
    public long LogicalBytes { get; }
    public SnapshotEntry Root { get; }

    private FastOwnedDirectoryIndex(Dictionary<string, SnapshotEntry> entries,
        Dictionary<string, SnapshotEntry[]> children, SnapshotEntry root, long logicalBytes)
    {
        this.entries = entries;
        this.children = children;
        Root = root;
        LogicalBytes = logicalBytes;
    }

    public SnapshotEntry? Find(string normalizedPath) =>
        entries.TryGetValue(normalizedPath, out var entry) ? entry : null;

    public SnapshotEntry[] EnumerateChildren(string normalizedPath) =>
        children.TryGetValue(normalizedPath, out var list) ? list : [];

    public IEnumerable<OwnedDirectoryItem> EnumerateItems() =>
        entries.Values.OrderBy(e => e.Path, StringComparer.OrdinalIgnoreCase).Select(e => new OwnedDirectoryItem(e, null));

    public IReadOnlyDictionary<string, SnapshotEntry> AllEntries => entries;

    public static (long Count, long LogicalBytes, string Hash) Write(string path, IReadOnlyList<SnapshotEntry> items)
    {
        var sorted = items.OrderBy(e => e.Path, StringComparer.OrdinalIgnoreCase).ToArray();
        if (sorted.Length == 0 || sorted[0].Path.Length != 0 || !sorted[0].Directory)
            throw new WorkspaceException("GeneratedBasisCorrupt", "Fast index requires a valid root directory entry.");
        long logicalBytes = 0;
        foreach (var entry in sorted)
        {
            ValidateEntry(entry);
            if (!entry.Directory) logicalBytes = checked(logicalBytes + entry.Length);
        }

        byte[] payload;
        using (var buffer = new MemoryStream(sorted.Length * 96))
        using (var writer = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Magic);
            writer.Write(FormatVersion);
            writer.Write(sorted.Length);
            writer.Write(logicalBytes);
            foreach (var entry in sorted)
            {
                writer.Write(entry.Path);
                writer.Write(entry.Directory);
                writer.Write(entry.Length);
                writer.Write(entry.Attributes);
                writer.Write(entry.CreationTime);
                writer.Write(entry.LastAccessTime);
                writer.Write(entry.LastWriteTime);
            }
            writer.Flush();
            payload = buffer.ToArray();
        }

        byte[] digest = SHA256.HashData(payload);
        string hashHex = Convert.ToHexStringLower(digest);
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536))
            {
                stream.Write(payload, 0, payload.Length);
                stream.Write(digest, 0, digest.Length);
                stream.Flush(true);
            }
            AtomicFilePublication.Publish(temp, path);
            return (sorted.Length, logicalBytes, hashHex);
        }
        catch
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch (IOException) { }
            throw;
        }
    }

    public static void VerifyFile(string path, string? expectedHash = null) =>
        _ = ReadVerifiedPayload(path, expectedHash);

    public static SnapshotEntry ReadRoot(string path, string? expectedHash = null)
    {
        try
        {
            byte[] payload = ReadVerifiedPayload(path, expectedHash);
            using var stream = new MemoryStream(payload, writable: false);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);
            var (count, _) = ReadHeader(reader);
            if (count <= 0) throw new WorkspaceException("GeneratedBasisCorrupt", "Empty fast index.");
            var root = ReadEntry(reader);
            if (root.Path.Length != 0 || !root.Directory)
                throw new WorkspaceException("GeneratedBasisCorrupt", "Fast index root is missing.");
            return root;
        }
        catch (WorkspaceException) { throw; }
        catch (Exception ex) when (ex is IOException or EndOfStreamException or ArgumentException)
        {
            throw new WorkspaceException("GeneratedBasisCorrupt", "Damaged fast index: " + ex.Message);
        }
    }

    public static FastOwnedDirectoryIndex Load(string path, string? expectedHash = null)
    {
        try
        {
            byte[] payload = ReadVerifiedPayload(path, expectedHash);
            using var stream = new MemoryStream(payload, writable: false);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);
            var (count, logicalBytes) = ReadHeader(reader);

            var entries = new Dictionary<string, SnapshotEntry>(count, StringComparer.OrdinalIgnoreCase);
            var childLists = new Dictionary<string, List<SnapshotEntry>>(StringComparer.OrdinalIgnoreCase);
            SnapshotEntry? root = null;
            long actualLogicalBytes = 0;

            for (int i = 0; i < count; i++)
            {
                var entry = ReadEntry(reader);
                if (!entries.TryAdd(entry.Path, entry))
                    throw new WorkspaceException("GeneratedBasisCorrupt", $"Duplicate fast index entry '{entry.Path}'.");
                if (entry.Path.Length == 0)
                {
                    root = entry;
                }
                else
                {
                    if (!entry.Directory) actualLogicalBytes = checked(actualLogicalBytes + entry.Length);
                    string parent = Parent(entry.Path);
                    if (!childLists.TryGetValue(parent, out var list))
                        childLists[parent] = list = [];
                    list.Add(entry);
                }
            }
            if (stream.Position != payload.Length)
                throw new WorkspaceException("GeneratedBasisCorrupt", "Fast index contains unexpected trailing bytes.");
            if (root is null || !root.Directory)
                throw new WorkspaceException("GeneratedBasisCorrupt", "Fast index root directory is missing.");
            if (actualLogicalBytes != logicalBytes)
                throw new WorkspaceException("GeneratedBasisCorrupt", "Fast index logical byte total mismatch.");

            var children = new Dictionary<string, SnapshotEntry[]>(childLists.Count, StringComparer.OrdinalIgnoreCase);
            foreach (var (parent, list) in childLists)
            {
                if (!entries.TryGetValue(parent, out var parentEntry) || !parentEntry.Directory)
                    throw new WorkspaceException("GeneratedBasisCorrupt", $"Fast index entry parent '{parent}' is missing or not a directory.");
                var array = list.ToArray();
                Array.Sort(array, (a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Path, b.Path));
                children[parent] = array;
            }
            return new FastOwnedDirectoryIndex(entries, children, root, logicalBytes);
        }
        catch (WorkspaceException) { throw; }
        catch (Exception ex) when (ex is IOException or EndOfStreamException or ArgumentException or OverflowException)
        {
            throw new WorkspaceException("GeneratedBasisCorrupt", "Damaged fast index: " + ex.Message);
        }
    }

    public static IEnumerable<OwnedDirectoryItem> Enumerate(string path, string? expectedHash = null)
    {
        byte[] payload = ReadVerifiedPayload(path, expectedHash);
        using var stream = new MemoryStream(payload, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);
        var (count, _) = ReadHeader(reader);
        for (int i = 0; i < count; i++)
            yield return new OwnedDirectoryItem(ReadEntry(reader), null);
    }

    private static byte[] ReadVerifiedPayload(string path, string? expectedHash)
    {
        byte[] raw;
        try { raw = File.ReadAllBytes(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { throw new WorkspaceException("GeneratedBasisCorrupt", "Cannot read fast index: " + ex.Message); }

        if (raw.Length < HeaderLength + ChecksumLength)
            throw new WorkspaceException("GeneratedBasisCorrupt", "Fast index is truncated.");

        int payloadLength = raw.Length - ChecksumLength;
        ReadOnlySpan<byte> storedDigest = raw.AsSpan(payloadLength, ChecksumLength);
        byte[] computed = SHA256.HashData(raw.AsSpan(0, payloadLength));
        if (!CryptographicOperations.FixedTimeEquals(storedDigest, computed))
            throw new WorkspaceException("GeneratedBasisCorrupt", "Fast index integrity checksum mismatch.");
        if (expectedHash is not null &&
            !string.Equals(Convert.ToHexStringLower(computed), expectedHash, StringComparison.OrdinalIgnoreCase))
            throw new WorkspaceException("GeneratedBasisCorrupt", "Fast index checksum does not match manifest.");

        byte[] payload = new byte[payloadLength];
        Buffer.BlockCopy(raw, 0, payload, 0, payloadLength);
        return payload;
    }

    private static (int Count, long LogicalBytes) ReadHeader(BinaryReader reader)
    {
        byte[] magic = reader.ReadBytes(4);
        if (!magic.AsSpan().SequenceEqual(Magic) || reader.ReadInt32() != FormatVersion)
            throw new WorkspaceException("GeneratedBasisCorrupt", "Unsupported fast index format.");
        int count = reader.ReadInt32();
        long logicalBytes = reader.ReadInt64();
        if (count <= 0 || logicalBytes < 0)
            throw new WorkspaceException("GeneratedBasisCorrupt", "Invalid fast index header.");
        return (count, logicalBytes);
    }

    private static SnapshotEntry ReadEntry(BinaryReader reader)
    {
        var entry = new SnapshotEntry(reader.ReadString(), reader.ReadBoolean(), reader.ReadInt64(), reader.ReadUInt32(),
            reader.ReadInt64(), reader.ReadInt64(), reader.ReadInt64());
        ValidateEntry(entry);
        return entry;
    }

    private static void ValidateEntry(SnapshotEntry entry)
    {
        string normalized = JournalDeltaStore.Normalize(entry.Path);
        if (!string.Equals(normalized, entry.Path, StringComparison.Ordinal))
            throw new WorkspaceException("GeneratedBasisCorrupt", $"Fast index path '{entry.Path}' is not normalized.");
        if (entry.Length < 0 || (entry.Directory && entry.Length != 0))
            throw new WorkspaceException("GeneratedBasisCorrupt", $"Fast index entry '{entry.Path}' has invalid length {entry.Length}.");
        SnapshotInputChecks.RejectUnsupportedAttributes(entry.Attributes, entry.Path);
    }

    private static string Parent(string path)
    {
        int slash = path.LastIndexOf('/');
        return slash < 0 ? "" : path[..slash];
    }
}
