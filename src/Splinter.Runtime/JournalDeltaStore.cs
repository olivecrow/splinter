using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Splinter.Core;

namespace Splinter.Runtime;

// An experimental range overlay. Each acknowledged transaction is checksummed
// and flushed. Reads only use the snapshot and existing journal extents.
// The journal is bounded; exhaustion preserves existing results, never copy-ups.
public sealed class JournalDeltaStore : IWorkspaceDeltaStore
{
    private sealed record Extent(long Start, int Length, long JournalOffset);
    private sealed class Node(DeltaEntry entry)
    {
        public DeltaEntry Entry = entry;
        public List<Extent> Extents = [];
    }
    private sealed record Mutation(long Sequence, string Operation, DeltaEntry Entry,
        long Offset = 0, string? Destination = null, bool Replace = false);
    private const int MaxMetadataBytes = 65536;
    public const int MaxIndexedNodes = 250000;
    public const int MaxIndexedExtents = 1000000;
    public const int MaxHiddenPaths = 250000;
    private readonly object gate = new();
    private readonly ISnapshotTree snapshot;
    private readonly FileStream journal;
    private readonly Dictionary<Guid, Node> nodes = [];
    private readonly Dictionary<string, Guid> paths = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> hidden = new(StringComparer.OrdinalIgnoreCase);
    private long sequence, payloadBytes, readBytes;
    private int extentCount;
    private bool poisoned, disposed;
    public OverlayIdentity Identity { get; }

    public JournalDeltaStore(string directory, OverlayIdentity identity, ISnapshotTree snapshot)
    {
        if (identity.Version != 1 || identity.MaxJournalBytes < 4096 || identity.MaxTransferBytes is < 4096 or > 1048576)
            throw new ArgumentException("Invalid overlay version or bounded storage limits.");
        if (identity.Snapshot != snapshot.Snapshot || !identity.Snapshot.Persistent || !identity.Snapshot.Differential)
            throw new WorkspaceException("SnapshotIdentityMismatch", "Reconnect requires the exact persistent differential snapshot reference.");
        snapshot.EnsureAvailable();
        this.snapshot = snapshot;
        Identity = identity;
        Directory.CreateDirectory(directory);
        string identityPath = System.IO.Path.Combine(directory, "identity.json");
        // FileShare.Read excludes concurrent writers across provider processes.
        journal = new FileStream(System.IO.Path.Combine(directory, "delta.journal"), FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.RandomAccess);
        try
        {
            if (File.Exists(identityPath))
            {
                OverlayIdentity? saved;
                try { saved = JsonSerializer.Deserialize<OverlayIdentity>(File.ReadAllText(identityPath)); }
                catch (JsonException) { throw new WorkspaceException("DeltaCorrupt", "Delta identity JSON is corrupt; preserve the identity and journal."); }
                if (JsonSerializer.Serialize(saved) != JsonSerializer.Serialize(identity))
                    throw new WorkspaceException("OverlayIdentityMismatch", "Delta identity and limits cannot change on reconnect.");
            }
            else
            {
                if (journal.Length != 0) throw new WorkspaceException("OverlayIdentityMissing", "A nonempty journal has no identity; preserve it for recovery.");
                using var identityFile = new FileStream(identityPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                JsonSerializer.Serialize(identityFile, identity); identityFile.Flush(true);
            }
            AddBaseNode(snapshot.Stat("") ?? throw new WorkspaceException("SnapshotMissing", "Snapshot root is unavailable."), "");
            Replay();
        }
        catch { journal.Dispose(); throw; }
    }

    public DeltaEntry? Stat(string path) { lock (gate) { Check(); return Lookup(Normalize(path))?.Entry; } }
    public DeltaEntry Get(Guid id) { lock (gate) { Check(); return Required(id).Entry; } }
    public IReadOnlyList<DeltaEntry> List(string directory)
    {
        lock (gate)
        {
            Check(); Node parent = Lookup(Normalize(directory)) ?? throw new DirectoryNotFoundException(directory);
            if (!parent.Entry.Directory) throw new IOException("Not a directory.");
            if (parent.Entry.BasePath is { } lower)
                foreach (var item in snapshot.List(lower)) _ = Lookup(Join(parent.Entry.Path, Name(item.Path)));
            return paths.Keys.Where(p => p.Length != 0 && Parent(p).Equals(parent.Entry.Path, StringComparison.OrdinalIgnoreCase))
                .Select(p => nodes[paths[p]].Entry).OrderBy(e => e.Path, StringComparer.OrdinalIgnoreCase).ToArray();
        }
    }

    public DeltaEntry Create(string path, bool directory, uint attributes)
    {
        lock (gate)
        {
            Check(); path = Normalize(path);
            if (path.Length == 0 || Lookup(path) is not null) throw new IOException("Path already exists.");
            RequireParent(path); RequireNodeCapacity();
            long now = DateTime.UtcNow.ToFileTimeUtc();
            var entry = new DeltaEntry(Guid.NewGuid(), path, directory, 0,
                directory ? attributes | 16u : attributes & ~16u, now, now, now, null, 0);
            Commit(new(sequence + 1, "create", entry), []);
            return entry;
        }
    }

    public int Read(Guid id, long offset, Span<byte> destination)
    {
        lock (gate)
        {
            Check(); Node node = RequiredFile(id);
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
            int count = (int)Math.Min(destination.Length, Math.Max(0, node.Entry.Length - offset));
            Span<byte> output = destination[..count]; output.Clear();
            // Only uncovered intervals touch the immutable base. Data already in
            // the delta does not cause a hidden full-file baseline read.
            long cursor = offset, end = offset + count;
            foreach (var extent in node.Extents)
            {
                long from = Math.Max(offset, extent.Start), to = Math.Min(end, extent.Start + extent.Length);
                if (from >= to) continue;
                ReadBase(node, cursor, from, output, offset);
                ReadExactly(extent.JournalOffset + from - extent.Start, output.Slice((int)(from - offset), (int)(to - from)));
                cursor = to;
            }
            ReadBase(node, cursor, end, output, offset);
            readBytes += count; return count;
        }
    }

    public void Write(Guid id, long offset, ReadOnlySpan<byte> data)
    {
        lock (gate)
        {
            Check(); Node node = RequiredFile(id);
            if (offset < 0 || data.Length > Identity.MaxTransferBytes) throw new ArgumentOutOfRangeException(nameof(offset));
            long end = checked(offset + data.Length);
            if (data.IsEmpty) return;
            var updated = node.Entry with { Length = Math.Max(node.Entry.Length, end), LastWriteTime = DateTime.UtcNow.ToFileTimeUtc() };
            Commit(new(sequence + 1, "write", updated, offset), data);
        }
    }

    public void SetLength(Guid id, long length)
    {
        lock (gate)
        {
            Check(); Node node = RequiredFile(id);
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
            if (length == node.Entry.Length) return;
            // Once truncated, original tail bytes must never reappear on extension.
            var updated = node.Entry with { Length = length, BaseLength = Math.Min(node.Entry.BaseLength, length), LastWriteTime = DateTime.UtcNow.ToFileTimeUtc() };
            Commit(new(sequence + 1, "resize", updated), []);
        }
    }

    public void SetMetadata(Guid id, uint attributes, long creationTime, long accessTime, long writeTime)
    {
        lock (gate)
        {
            Check(); var old = Required(id).Entry;
            var entry = old with { Attributes = attributes, CreationTime = creationTime, LastAccessTime = accessTime, LastWriteTime = writeTime };
            if (entry != old) Commit(new(sequence + 1, "metadata", entry), []);
        }
    }

    public void Rename(Guid id, string destination, bool replace)
    {
        lock (gate)
        {
            Check(); Node node = Required(id); destination = Normalize(destination);
            if (node.Entry.Path.Length == 0 || node.Entry.Deleted || destination.Length == 0 || IsChild(destination, node.Entry.Path))
                throw new IOException("Invalid rename.");
            RequireParent(destination);
            var existing = Lookup(destination);
            if (existing is not null && existing != node && (!replace || existing.Entry.Directory || node.Entry.Directory))
                throw new IOException("Rename destination exists.");
            Commit(new(sequence + 1, "rename", node.Entry, Destination: destination, Replace: replace), []);
        }
    }

    public void Delete(Guid id)
    {
        lock (gate)
        {
            Check(); Node node = Required(id);
            if (node.Entry.Deleted) return;
            if (node.Entry.Path.Length == 0 || (node.Entry.Directory && List(node.Entry.Path).Count != 0))
                throw new IOException("Directory is not empty or is the root.");
            Commit(new(sequence + 1, "delete", node.Entry with { Deleted = true }), []);
        }
    }

    public void Flush()
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (poisoned) throw new WorkspaceException("DeltaReconnectRequired", "Replay the preserved journal after a failed append.");
            // Flushing acknowledged private data does not need a surviving base.
            journal.Flush(true);
        }
    }
    public OverlayStorageObservation Observe()
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return new(Identity.Id, Identity.Snapshot.Id, 0, journal.Length, payloadBytes,
                journal.Length - payloadBytes, readBytes, 0, Identity.MaxTransferBytes, nodes.Count, sequence);
        }
    }
    public void Dispose() { lock (gate) { if (disposed) return; disposed = true; journal.Dispose(); } }

    private void ReadBase(Node node, long from, long to, Span<byte> output, long outputOffset)
    {
        to = Math.Min(to, node.Entry.BaseLength);
        if (from >= to || node.Entry.BasePath is not { } path) return;
        while (from < to)
        {
            int count = (int)Math.Min(to - from, Identity.MaxTransferBytes);
            int read = snapshot.Read(path, from, output.Slice((int)(from - outputOffset), count));
            if (read <= 0 || read > count) throw new WorkspaceException("SnapshotReadFailed", "Immutable base returned an unexpected short file.");
            from += read;
        }
    }
    private void ReadExactly(long offset, Span<byte> output)
    {
        while (!output.IsEmpty)
        {
            int count = RandomAccess.Read(journal.SafeFileHandle, output, offset);
            if (count == 0) throw new WorkspaceException("DeltaCorrupt", "An acknowledged delta extent is missing.");
            offset += count; output = output[count..];
        }
    }
    private void Check()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (poisoned) throw new WorkspaceException("DeltaReconnectRequired", "A failed append requires closing and replaying the preserved delta.");
        snapshot.EnsureAvailable();
    }
    private Node Required(Guid id) => nodes.TryGetValue(id, out var node) ? node : throw new FileNotFoundException("Unknown open file identity.");
    private Node RequiredFile(Guid id) { var node = Required(id); return !node.Entry.Directory ? node : throw new IOException("Cannot read or write a directory."); }
    private void RequireParent(string path)
    {
        if (Lookup(Parent(path)) is not { Entry.Directory: true }) throw new DirectoryNotFoundException(Parent(path));
    }
    private void RequireNodeCapacity()
    {
        if (nodes.Count >= MaxIndexedNodes) throw new WorkspaceException("OverlayMetadataLimit", "The bounded metadata index is full; preserve the delta.");
    }
    private Node? Lookup(string path)
    {
        if (paths.TryGetValue(path, out var id)) return nodes[id];
        if (path.Length == 0 || hidden.Contains(path)) return null;
        if (Identity.ExcludedTopLevel?.Contains(path.Split('/')[0], StringComparer.OrdinalIgnoreCase) == true) return null;
        var parent = Lookup(Parent(path));
        if (parent is not { Entry.Directory: true, Entry.BasePath: { } baseParent }) return null;
        var entry = snapshot.Stat(Join(baseParent, Name(path)));
        return entry is null ? null : AddBaseNode(entry, path);
    }
    private Node AddBaseNode(SnapshotEntry entry, string path)
    {
        RequireNodeCapacity();
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(Identity.Snapshot.Id + "/" + entry.Path.ToUpperInvariant()));
        var id = new Guid(digest.AsSpan(0, 16));
        var node = new Node(new(id, path, entry.Directory, entry.Length, entry.Attributes,
            entry.CreationTime, entry.LastAccessTime, entry.LastWriteTime, entry.Path, entry.Length));
        nodes.Add(id, node); paths.Add(path, id); return node;
    }
    public static string Normalize(string path)
    {
        path = path.Replace('\\', '/').TrimStart('/');
        if (path.EndsWith('/') && path.Length != 0) path = path.TrimEnd('/');
        if (path.Length > 32000 || path.Contains(':') || path.Contains('\0') ||
            (path.Length != 0 && path.Split('/').Any(p => p is "." or ".." or "" || p.EndsWith('.') || p.EndsWith(' '))))
            throw new ArgumentException("Only relative ordinary file paths are supported.");
        return path;
    }
    private static string Parent(string path) { int slash = path.LastIndexOf('/'); return slash < 0 ? "" : path[..slash]; }
    private static string Name(string path) => path[(path.LastIndexOf('/') + 1)..];
    private static string Join(string parent, string name) => parent.Length == 0 ? name : parent + "/" + name;
    private static bool IsChild(string child, string parent) => child.StartsWith(parent + "/", StringComparison.OrdinalIgnoreCase);

    private void Commit(Mutation mutation, ReadOnlySpan<byte> payload)
    {
        RequireMutationCapacity(mutation);
        byte[] metadata = JsonSerializer.SerializeToUtf8Bytes(mutation);
        if (metadata.Length > MaxMetadataBytes) throw new WorkspaceException("OverlayMetadataLimit", "Mutation metadata exceeds the transaction limit.");
        long start = journal.Length, required = 8L + metadata.Length + payload.Length + 32;
        if (start > Identity.MaxJournalBytes - required)
            throw new WorkspaceException("DeltaFull", "The delta journal budget is exhausted. Existing data remains intact; no automatic deletion or full-copy fallback.");
        Span<byte> header = stackalloc byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(header, metadata.Length);
        BinaryPrimitives.WriteInt32LittleEndian(header[4..], payload.Length);
        using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        digest.AppendData(header); digest.AppendData(metadata); digest.AppendData(payload);
        try
        {
            journal.Position = start;
            journal.Write(header); journal.Write(metadata); journal.Write(payload); journal.Write(digest.GetHashAndReset());
            journal.Flush(true);
        }
        catch { poisoned = true; throw; }
        Apply(mutation, start + 8 + metadata.Length, payload.Length);
    }
    private void Replay()
    {
        long cursor = 0;
        byte[] header = new byte[8], expectedHash = new byte[32];
        while (cursor < journal.Length)
        {
            if (journal.Length - cursor < 8) break;
            ReadExactly(cursor, header);
            int metadataLength = BinaryPrimitives.ReadInt32LittleEndian(header), dataLength = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4));
            if (metadataLength is < 1 or > MaxMetadataBytes || dataLength < 0 || dataLength > Identity.MaxTransferBytes)
                throw new WorkspaceException("DeltaCorrupt", "Invalid journal frame; preserve evidence without truncation.");
            long frameLength = 8L + metadataLength + dataLength + 32;
            if (journal.Length - cursor < frameLength) break;
            byte[] metadata = new byte[metadataLength]; ReadExactly(cursor + 8, metadata);
            using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            digest.AppendData(header); digest.AppendData(metadata);
            byte[] data = ArrayPool<byte>.Shared.Rent(Math.Max(1, dataLength));
            try { ReadExactly(cursor + 8 + metadataLength, data.AsSpan(0, dataLength)); digest.AppendData(data.AsSpan(0, dataLength)); }
            finally { ArrayPool<byte>.Shared.Return(data); }
            ReadExactly(cursor + frameLength - 32, expectedHash);
            if (!CryptographicOperations.FixedTimeEquals(digest.GetHashAndReset(), expectedHash))
                throw new WorkspaceException("DeltaCorrupt", "Journal checksum mismatch; automatic discard is forbidden.");
            var mutation = DecodeMutation(metadata, dataLength);
            if (mutation.Sequence != sequence + 1) throw new WorkspaceException("DeltaCorrupt", "Journal sequence mismatch.");
            Apply(mutation, cursor + 8 + metadataLength, dataLength); cursor += frameLength;
        }
        // Only an incomplete trailing transaction is unacknowledged. Corrupt
        // complete transactions above fail closed and remain byte-for-byte intact.
        if (cursor != journal.Length) { journal.SetLength(cursor); journal.Flush(true); }
    }
    private static Mutation DecodeMutation(byte[] metadata, int payloadLength)
    {
        Mutation? mutation;
        try { mutation = JsonSerializer.Deserialize<Mutation>(metadata); }
        catch (JsonException) { throw new WorkspaceException("DeltaCorrupt", "Journal metadata JSON is corrupt; preserve the complete frame."); }
        static WorkspaceException Invalid() => new("DeltaCorrupt", "Journal mutation metadata is invalid; preserve the complete frame.");
        if (mutation?.Entry is not { } entry || entry.Id == Guid.Empty || entry.Path is null ||
            entry.Length < 0 || entry.BaseLength < 0 || entry.BaseLength > entry.Length ||
            entry.BasePath is null && entry.BaseLength != 0 || entry.Directory && entry.Length != 0)
            throw Invalid();
        try
        {
            if (Normalize(entry.Path) != entry.Path || entry.BasePath is not null && Normalize(entry.BasePath) != entry.BasePath)
                throw Invalid();
            if (mutation.Operation == "rename" && (mutation.Destination is null ||
                mutation.Destination.Length == 0 || Normalize(mutation.Destination) != mutation.Destination ||
                entry.Path.Length == 0 || IsChild(mutation.Destination, entry.Path))) throw Invalid();
        }
        catch (ArgumentException) { throw Invalid(); }
        if (mutation.Operation == "write")
        {
            if (entry.Directory || mutation.Offset < 0 || payloadLength == 0 ||
                mutation.Offset > entry.Length - payloadLength) throw Invalid();
        }
        else if (payloadLength != 0 || mutation.Operation is not ("create" or "resize" or "metadata" or "delete" or "rename"))
            throw Invalid();
        return mutation;
    }
    private void Apply(Mutation mutation, long payloadOffset, int payloadLength)
    {
        RequireMutationCapacity(mutation);
        if (!nodes.TryGetValue(mutation.Entry.Id, out var node))
        {
            RequireNodeCapacity(); node = new Node(mutation.Entry); nodes.Add(mutation.Entry.Id, node);
        }
        node.Entry = mutation.Entry;
        switch (mutation.Operation)
        {
            case "write":
                extentCount -= node.Extents.Count;
                ReplaceRange(node, new(mutation.Offset, payloadLength, payloadOffset));
                extentCount += node.Extents.Count;
                if (!node.Entry.Deleted) paths[node.Entry.Path] = node.Entry.Id;
                break;
            case "resize":
                extentCount -= node.Extents.Count;
                node.Extents = node.Extents.Where(e => e.Start < node.Entry.Length)
                    .Select(e => e with { Length = (int)Math.Min(e.Length, node.Entry.Length - e.Start) }).ToList();
                extentCount += node.Extents.Count;
                if (!node.Entry.Deleted) paths[node.Entry.Path] = node.Entry.Id;
                break;
            case "create": case "metadata":
                if (!node.Entry.Deleted) paths[node.Entry.Path] = node.Entry.Id;
                break;
            case "delete":
                paths.Remove(node.Entry.Path); hidden.Add(node.Entry.Path); break;
            case "rename":
                string old = node.Entry.Path, destination = mutation.Destination!;
                if (paths.TryGetValue(destination, out var replaced) && replaced != node.Entry.Id)
                { nodes[replaced].Entry = nodes[replaced].Entry with { Deleted = true }; paths.Remove(destination); }
                var affected = paths.Keys.Where(p => p.Equals(old, StringComparison.OrdinalIgnoreCase) || IsChild(p, old)).ToArray();
                foreach (string path in affected)
                {
                    var moved = nodes[paths[path]]; paths.Remove(path);
                    moved.Entry = moved.Entry with { Path = destination + path[old.Length..] };
                    paths[moved.Entry.Path] = moved.Entry.Id;
                }
                // A replayed rename may introduce a node not previously looked up.
                node.Entry = node.Entry with { Path = destination }; paths[destination] = node.Entry.Id;
                foreach (string path in hidden.Where(p => IsChild(p, old)).ToArray())
                { hidden.Remove(path); hidden.Add(destination + path[old.Length..]); }
                hidden.Add(old); break;
            default: throw new WorkspaceException("DeltaCorrupt", "Unknown journal operation.");
        }
        payloadBytes += payloadLength; sequence = mutation.Sequence;
    }
    private void RequireMutationCapacity(Mutation mutation)
    {
        // One replacement can split an old extent into two and add one new
        // extent. Reject conservatively before appending an acknowledged frame.
        if (mutation.Operation == "write" && extentCount > MaxIndexedExtents - 2 ||
            mutation.Operation is "delete" or "rename" && !hidden.Contains(mutation.Entry.Path) && hidden.Count >= MaxHiddenPaths)
            throw new WorkspaceException("OverlayMetadataLimit", "The bounded range or whiteout index is full; preserve the delta.");
    }
    private static void ReplaceRange(Node node, Extent replacement)
    {
        long end = replacement.Start + replacement.Length;
        var next = new List<Extent>(node.Extents.Count + 1);
        foreach (var extent in node.Extents)
        {
            long priorEnd = extent.Start + extent.Length;
            if (priorEnd <= replacement.Start || extent.Start >= end) { next.Add(extent); continue; }
            if (extent.Start < replacement.Start) next.Add(extent with { Length = (int)(replacement.Start - extent.Start) });
            if (priorEnd > end) next.Add(new(end, (int)(priorEnd - end), extent.JournalOffset + end - extent.Start));
        }
        next.Add(replacement); next.Sort((a, b) => a.Start.CompareTo(b.Start)); node.Extents = next;
    }
}
