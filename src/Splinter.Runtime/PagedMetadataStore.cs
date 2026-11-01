using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Splinter.Core;

namespace Splinter.Runtime;

// One writer, immutable readers. Only the caller's durable root publication
// makes a mutation visible. Unpublished/torn tails never replace an old root.
// Small logical pages share append-only containers; no file per directory.
public sealed partial class PagedMetadataStore : IDisposable
{
    // One current record-codec family. Old stores must be finished with their
    // matching binary; never append new value codecs to an older store.
    public const int FormatVersion = 2;
    public static ReadOnlySpan<byte> FormatMagic => "SPM2"u8;
    public const int MaximumPageBytes = 65536;
    public const long ContainerTargetBytes = 64L * 1024 * 1024;
    public static long CachedMetadataBytes => PageCache.ChargedBytes;
    public static long PeakCachedMetadataBytes => PageCache.PeakChargedBytes;
    public static int MaximumContainerReaders => StorageResources.MetadataReaders;
    private const int ContainerHeaderBytes = 48, PageHeaderBytes = 40, ReferenceBytes = 60, ReferencesPerNode = 3;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private readonly string directory;
    private readonly bool writable;
    private readonly Action<string>? boundary;
    private readonly IJournalFilePins? filePins;
    private readonly object gate = new();
    private readonly FileStream? writerLease;
    private readonly FileStream? readerLease;
    private FileStream? output;
    private readonly Dictionary<long, ContainerReader> readers = [];
    private readonly LinkedList<long> readerRecent = [];
    internal long CacheInvalidationVisits { get; private set; }
    internal long LocationBlockReads => locations?.BlockReadCount ?? 0;
    private sealed record ContainerReader(FileStream Input, bool Cacheable, LinkedListNode<long> Recent) : IDisposable
    { public void Dispose() => Input.Dispose(); }
    private readonly string cacheScope = Guid.NewGuid().ToString("N");
    private long container;
    private bool disposed;
    private LocationMap? locations;
    private PagedMetadataStore? readThrough;
    private long readThroughLastContainer;
    public string StoreId { get; }
    internal string WorkingDirectory => directory;
    public MetadataTreeRoot Empty => new(1, StoreId, null);

    public PagedMetadataStore(string directory, bool writable = false, Action<string>? boundary = null, IJournalFilePins? filePins = null, string? initialStoreId = null)
        : this(directory, writable, boundary, filePins, maintenance: false, containerStart: null, initialStoreId) { }

    private PagedMetadataStore(string directory, bool writable, Action<string>? boundary, IJournalFilePins? filePins,
        bool maintenance, long? containerStart, string? initialStoreId = null)
    {
        this.directory = Path.GetFullPath(directory); this.writable = writable; this.boundary = boundary; this.filePins = filePins;
        WorkspaceStore.EnsurePlainPath(this.directory);
        if (writable) Directory.CreateDirectory(this.directory);
        string identity = Path.Combine(this.directory, "identity.bin");
        WorkspaceStore.EnsurePlainPath(identity);
        try
        {
            if (writable)
            {
                string lease = Path.Combine(this.directory, "writer.lock");
                WorkspaceStore.EnsurePlainPath(lease);
                if (!maintenance) writerLease = new(lease, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
                if (!File.Exists(identity)) InitializeIdentity(identity, initialStoreId);
            }
            else if (!maintenance) readerLease = new(Path.Combine(this.directory, "writer.lock"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            byte[] header = ReadExactFile(identity, ContainerHeaderBytes);
            if (!header.AsSpan(0, 4).SequenceEqual(FormatMagic) ||
                !SHA256.HashData(header.AsSpan(0, 16)).AsSpan().SequenceEqual(header.AsSpan(16)))
                throw Invalid("Preserve an unsupported or corrupt metadata store; use its matching binary.");
            StoreId = Convert.ToHexStringLower(header.AsSpan(4, 12));
            if (initialStoreId is not null && StoreId != initialStoreId) throw Invalid("Preserve a metadata identity foreign to its fixed initialization.");
            locations = OpenLocations();
            if (writable && !maintenance && File.Exists(Path.Combine(this.directory, "collection.json")))
                throw new WorkspaceException("MetadataCollectionPending", "Resume the fixed metadata collection request before writing new metadata.");
            if (writable)
            {
                foreach (string path in Directory.EnumerateFiles(this.directory, "pages-*.bin"))
                {
                    string name = Path.GetFileNameWithoutExtension(path);
                    if (name.Length != 22 || !long.TryParse(name.AsSpan(6), System.Globalization.NumberStyles.HexNumber,
                            System.Globalization.CultureInfo.InvariantCulture, out long number) || number < 0)
                        throw Invalid("Preserve an unknown metadata container.");
                    container = Math.Max(container, number);
                }
                if (containerStart is not null) container = Math.Max(container, containerStart.Value);
                OpenOutput();
            }
        }
        catch { output?.Dispose(); locations?.Dispose(); writerLease?.Dispose(); readerLease?.Dispose(); throw; }
    }

    public IReadOnlyMetadataTree Open(MetadataTreeRoot root)
    {
        CheckRoot(root);
        if (root.Page is not null) _ = ReadNode(root.Page);
        return new Tree(this, root);
    }

    // Rank/select walk subtree counts without materializing preceding entries.
    public MetadataEntry EntryAt(MetadataTreeRoot root, long index)
    {
        CheckRoot(root);
        if (index < 0 || index >= root.Count) throw new ArgumentOutOfRangeException(nameof(index));
        var page = root.Page;
        while (page is not null)
        {
            var node = ReadNode(page); long left = node.Left?.Count ?? 0;
            if (index < left) page = node.Left;
            else if (index > left) { index -= left + 1; page = node.Right; }
            else return new(node.Key, node.Value.ToArray(), node.Data is null ? null : new(1, StoreId, node.Data));
        }
        throw Invalid("A metadata subtree count is inconsistent.");
    }

    public long Rank(MetadataTreeRoot root, string key)
    {
        CheckRoot(root); long rank = 0; var page = root.Page;
        while (page is not null)
        {
            var node = ReadNode(page); int order = StringComparer.Ordinal.Compare(key, node.Key);
            if (order <= 0) { if (order == 0) return checked(rank + (node.Left?.Count ?? 0)); page = node.Left; }
            else { rank = checked(rank + (node.Left?.Count ?? 0) + 1); page = node.Right; }
        }
        return rank;
    }

    public MetadataTreeRoot Set(MetadataTreeRoot root, string key, ReadOnlySpan<byte> value, MetadataTreeRoot? childTree = null)
    {
        lock (gate)
        {
            CheckWriter(); CheckRoot(root); ValidateEntry(key, value.Length);
            if (childTree is not null) { CheckRoot(childTree); if (childTree.Page is not null) _ = ReadNode(childTree.Page); }
            // The search spine is stored on disk, so deliberately constructed
            // key priorities cannot turn a large input into an in-RAM array.
            using var spine = new Spine(this);
            var page = root.Page; Node? found = null;
            while (page is not null)
            {
                Node node = ReadNode(page); int order = StringComparer.Ordinal.Compare(key, node.Key);
                if (order == 0) { found = node; break; }
                spine.Push(node); page = order < 0 ? node.Left : node.Right;
            }
            if (found is not null && value.SequenceEqual(found.Value) && found.Data == childTree?.Page) { spine.Complete(); return root; }
            var updated = WriteNode(new(key, value.ToArray(), found?.Left, found?.Right, childTree?.Page));
            while (spine.TryPop(out var parent))
            {
                Node child = ReadNode(updated);
                bool left = StringComparer.Ordinal.Compare(key, parent.Key) < 0;
                if (Priority(child.Key, parent.Key) < 0)
                {
                    if (left)
                    {
                        var right = WriteNode(parent with { Left = child.Right });
                        updated = WriteNode(child with { Right = right });
                    }
                    else
                    {
                        var prior = WriteNode(parent with { Right = child.Left });
                        updated = WriteNode(child with { Left = prior });
                    }
                }
                else updated = WriteNode(left ? parent with { Left = updated } : parent with { Right = updated });
            }
            spine.Complete(); return new(1, StoreId, updated);
        }
    }

    // Sorted bulk construction writes every node once. A disk spine bounds
    // working memory even for an adversarial monotone Cartesian-tree spine.
    public MetadataTreeRoot Build(IEnumerable<MetadataEntry> entries, CancellationToken ct = default, MetadataTreeRoot? previousRoot = null)
    {
        lock (gate)
        {
            CheckWriter();
            if (previousRoot is not null) CheckRoot(previousRoot);
            using var spine = new Spine(this); string? previous = null;
            foreach (var entry in entries)
            {
                ct.ThrowIfCancellationRequested(); ValidateEntry(entry.Key, entry.Value.Length);
                if (entry.Child is not null) { CheckRoot(entry.Child); if (entry.Child.Page is not null) _ = ReadNode(entry.Child.Page); }
                if (previous is not null && StringComparer.Ordinal.Compare(previous, entry.Key) >= 0)
                    throw Invalid("Metadata input must have unique ordinal-sorted keys.");
                previous = entry.Key; MetadataPageReference? left = null;
                while (spine.TryPeek(out var pending) && Priority(entry.Key, pending.Key) < 0)
                {
                    _ = spine.TryPop(out pending); left = WriteBuiltNode(pending with { Right = left });
                }
                spine.Push(new(entry.Key, entry.Value.ToArray(), left, null, entry.Child?.Page));
            }
            MetadataPageReference? root = null;
            while (spine.TryPop(out var pending)) root = WriteBuiltNode(pending with { Right = root });
            spine.Complete(); return new(1, StoreId, root);

            MetadataPageReference WriteBuiltNode(Node node)
            {
                byte[] hash = LogicalHash(node);
                // Look up the same deterministic Cartesian node in the prior
                // immutable root. Equal logical contents reuse its old physical
                // page, including unchanged subdirectories, without a second
                // project-wide hash index or an unbounded in-memory table.
                var candidate = previousRoot?.Page;
                while (candidate is not null)
                {
                    var prior = ReadNode(candidate); int order = StringComparer.Ordinal.Compare(node.Key, prior.Key);
                    if (order == 0)
                    {
                        if (hash.AsSpan().SequenceEqual(Convert.FromHexString(candidate.ContentHash))) return candidate;
                        break;
                    }
                    candidate = order < 0 ? prior.Left : prior.Right;
                }
                return WriteNode(node, hash);
            }
        }
    }

    public MetadataTreeRoot Remove(MetadataTreeRoot root, string key)
    {
        lock (gate)
        {
            CheckWriter(); CheckRoot(root); using var spine = new Spine(this); var page = root.Page;
            while (page is not null)
            {
                var node = ReadNode(page); int order = StringComparer.Ordinal.Compare(key, node.Key);
                if (order == 0)
                {
                    var updated = Merge(node.Left, node.Right);
                    while (spine.TryPop(out var parent))
                        updated = WriteNode(StringComparer.Ordinal.Compare(key, parent.Key) < 0
                            ? parent with { Left = updated } : parent with { Right = updated });
                    spine.Complete(); return new(1, StoreId, updated);
                }
                spine.Push(node); page = order < 0 ? node.Left : node.Right;
            }
            spine.Complete(); return root;
        }
    }

    private MetadataPageReference? Merge(MetadataPageReference? left, MetadataPageReference? right)
    {
        using var spine = new Spine(this);
        while (left is not null && right is not null)
        {
            var a = ReadNode(left); var b = ReadNode(right);
            if (Priority(a.Key, b.Key) < 0) { spine.Push(a); left = a.Right; }
            else { spine.Push(b, replaceLeft: true); right = b.Left; }
        }
        var result = left ?? right;
        while (spine.TryPop(out var node, out bool replaceLeft))
            result = WriteNode(replaceLeft ? node with { Left = result } : node with { Right = result });
        spine.Complete(); return result;
    }

    // Data must be durable BEFORE a workspace/capture/result root points at it.
    public void Flush()
    {
        lock (gate) { CheckWriter(); output!.Flush(true); boundary?.Invoke("metadata.pages-durable"); }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return; disposed = true;
            CacheInvalidationVisits += PageCache.Remove(cacheScope);
            readerRecent.Clear();
            foreach (var reader in readers.Values) reader.Dispose();
            output?.Dispose(); locations?.Dispose(); writerLease?.Dispose(); readerLease?.Dispose();
        }
    }

    private sealed record Node(string Key, byte[] Value, MetadataPageReference? Left, MetadataPageReference? Right,
        MetadataPageReference? Data = null);

    private MetadataPageReference WriteNode(Node node, byte[]? logicalHash = null)
    {
        byte[] body = Encode(node); byte[] hash = logicalHash ?? LogicalHash(node);
        int bytes = checked(PageHeaderBytes + body.Length);
        if (bytes > MaximumPageBytes) throw Invalid("Split a metadata value into pages before storing it.");
        if (output!.Length > ContainerTargetBytes - bytes)
        {
            CacheInvalidationVisits += PageCache.Remove(cacheScope + "/" + container + "/");
            output.Flush(true); output.Dispose(); output = null; container = checked(container + 1); OpenOutput();
        }
        long offset = output!.Length; output.Position = offset;
        Span<byte> header = stackalloc byte[PageHeaderBytes]; "SPN1"u8.CopyTo(header);
        BinaryPrimitives.WriteInt32LittleEndian(header[4..], body.Length);
        SHA256.HashData(body, header[8..]); output.Write(header); boundary?.Invoke("metadata.page-header-written"); output.Write(body);
        var reference = new MetadataPageReference(container, offset, bytes,
            checked(1 + (node.Left?.Count ?? 0) + (node.Right?.Count ?? 0)), Convert.ToHexStringLower(hash));
        PageCache.Add(CacheKey(reference), node, bytes); return reference;
    }

    private Node ReadNode(MetadataPageReference reference)
    {
        ObjectDisposedException.ThrowIf(disposed, this); CheckReference(reference);
        if (readThrough is not null && reference.Container <= readThroughLastContainer) return readThrough.ReadNode(reference);
        reference = ResolveReference(reference);
        if (PageCache.TryGet(CacheKey(reference), out var cached)) { TouchReader(reference.Container); return cached; }
        // A miss retains the container fence through the read; another reader
        // must not evict/close this handle while RandomAccess uses it.
        lock (gate) return ReadNodeCore(reference);
    }
    private Node ReadNodeCore(MetadataPageReference reference)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        CheckReference(reference);
        if (PageCache.TryGet(CacheKey(reference), out var cached)) { TouchReader(reference.Container); return cached; }
        FileStream input;
        bool cacheable = true;
        lock (gate)
        {
            if (writable && reference.Container == container)
            {
                output!.Flush(); input = output;
            }
            else
            {
                if (!readers.TryGetValue(reference.Container, out var reader))
                {
                    string path = ContainerPath(reference.Container); WorkspaceStore.EnsurePlainPath(path);
                    try { input = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.RandomAccess); }
                    catch (IOException ex) when ((ex.HResult & 0xffff) == 32)
                    {
                        // Retain the exact no-delete handle, but never cache pages
                        // under a share mode which permits mutation. Every reuse
                        // still reads and verifies its current page bytes.
                        input = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1, FileOptions.RandomAccess);
                        cacheable = false;
                    }
                    // Never retain a handle which failed its identity proof: a
                    // later open must not bypass this check through the cache.
                    try { CheckContainer(input); if (filePins is not null) VerifyContainerOwner(input, reference.Container); }
                    catch { input.Dispose(); throw; }
                    if (readers.Count >= MaximumContainerReaders)
                    {
                        long victim = readerRecent.Last!.Value; readerRecent.RemoveLast();
                        var retired = readers[victim]; readers.Remove(victim);
                        CacheInvalidationVisits += PageCache.Remove(cacheScope + "/" + victim + "/"); retired.Dispose();
                    }
                    reader = new(input, cacheable, readerRecent.AddFirst(reference.Container)); readers.Add(reference.Container, reader);
                }
                else { readerRecent.Remove(reader.Recent); readerRecent.AddFirst(reader.Recent); }
                input = reader.Input; cacheable = reader.Cacheable;
            }
        }
        // Write sharing also permits damage to the container prefix. Retain
        // the exact handle, not a cached integrity decision about its bytes.
        if (!cacheable) CheckContainer(input);
        if (reference.Offset > RandomAccess.GetLength(input.SafeFileHandle) - reference.Bytes) throw Invalid("An immutable metadata page is truncated.");
        Span<byte> header = stackalloc byte[PageHeaderBytes]; ReadAt(input, header, reference.Offset);
        if (!header[..4].SequenceEqual("SPN1"u8) || BinaryPrimitives.ReadInt32LittleEndian(header[4..]) != reference.Bytes - PageHeaderBytes)
            throw Invalid("An immutable metadata page has an invalid header.");
        byte[] body = new byte[reference.Bytes - PageHeaderBytes]; ReadAt(input, body, reference.Offset + PageHeaderBytes);
        if (!SHA256.HashData(body).AsSpan().SequenceEqual(header[8..])) throw Invalid("An immutable metadata page checksum changed.");
        Node node = Decode(body);
        if (reference.ContentHash != Convert.ToHexStringLower(LogicalHash(node)) ||
            reference.Count != checked(1 + (node.Left?.Count ?? 0) + (node.Right?.Count ?? 0)))
            throw Invalid("An immutable metadata page differs from its logical root.");
        foreach (var child in new[] { node.Left, node.Right, node.Data })
            if (child is not null && (child.Container > reference.Container || child.Container == reference.Container && child.Offset >= reference.Offset))
                throw Invalid("Metadata pages must reference earlier immutable pages, never a cycle.");
        if (cacheable) PageCache.Add(CacheKey(reference), node, reference.Bytes);
        return node;
    }

    private string CacheKey(MetadataPageReference reference) => cacheScope + "/" + reference.Container + "/" + reference.Offset + "/" + reference.Bytes + "/" + reference.Count + "/" + reference.ContentHash;
    private void TouchReader(long number)
    {
        lock (gate)
            if (readers.TryGetValue(number, out var reader))
            { readerRecent.Remove(reader.Recent); readerRecent.AddFirst(reader.Recent); }
    }
    private static void ReadAt(FileStream input, Span<byte> bytes, long offset)
    {
        while (!bytes.IsEmpty)
        {
            int count = RandomAccess.Read(input.SafeFileHandle, bytes, offset);
            if (count == 0) throw Invalid("An immutable metadata page ended early.");
            bytes = bytes[count..]; offset = checked(offset + count);
        }
    }

    private static byte[] LogicalHash(Node node)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData("SPN1-logical"u8);
        byte[] key = Utf8.GetBytes(node.Key); Span<byte> lengths = stackalloc byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(lengths, key.Length);
        BinaryPrimitives.WriteInt32LittleEndian(lengths[4..], node.Value.Length);
        hash.AppendData(lengths); hash.AppendData(key); hash.AppendData(node.Value);
        Span<byte> count = stackalloc byte[8];
        foreach (var child in new[] { node.Left, node.Right, node.Data })
        {
            BinaryPrimitives.WriteInt64LittleEndian(count, child?.Count ?? 0);
            hash.AppendData(count);
            hash.AppendData(child is null ? new byte[32] : Convert.FromHexString(child.ContentHash));
        }
        return hash.GetHashAndReset();
    }

    private static byte[] Encode(Node node)
    {
        byte[] key = Utf8.GetBytes(node.Key);
        var children = new[] { node.Left, node.Right, node.Data };
        byte flags = 0; int references = 0;
        for (int index = 0; index < children.Length; index++)
            if (children[index] is not null) { flags |= (byte)(1 << index); references++; }
        byte[] body = new byte[checked(9 + key.Length + node.Value.Length + references * (1 + ReferenceBytes))];
        BinaryPrimitives.WriteInt32LittleEndian(body, key.Length);
        BinaryPrimitives.WriteInt32LittleEndian(body.AsSpan(4), node.Value.Length);
        body[8] = flags; key.CopyTo(body, 9); node.Value.CopyTo(body, 9 + key.Length);
        int position = 9 + key.Length + node.Value.Length;
        foreach (var child in children)
            if (child is not null) { WriteReference(body.AsSpan(position, 1 + ReferenceBytes), child); position += 1 + ReferenceBytes; }
        return body;
    }

    private static Node Decode(ReadOnlySpan<byte> body)
    {
        if (body.Length < 9 || body[8] > 7) throw Invalid("A metadata record is truncated or has unknown flags.");
        int keyLength = BinaryPrimitives.ReadInt32LittleEndian(body), valueLength = BinaryPrimitives.ReadInt32LittleEndian(body[4..]);
        int count = System.Numerics.BitOperations.PopCount(body[8]);
        if (keyLength < 1 || valueLength < 0 || (long)keyLength + valueLength + 9 + count * (1 + ReferenceBytes) != body.Length)
            throw Invalid("A metadata record has invalid lengths.");
        string key;
        try { key = Utf8.GetString(body.Slice(9, keyLength)); }
        catch (DecoderFallbackException) { throw Invalid("A metadata key has invalid UTF-8."); }
        int position = 9 + keyLength + valueLength;
        var children = new MetadataPageReference?[ReferencesPerNode];
        for (int index = 0; index < children.Length; index++)
            if ((body[8] & (1 << index)) != 0)
            {
                children[index] = ReadReference(body.Slice(position, 1 + ReferenceBytes));
                if (children[index] is null) throw Invalid("A present metadata reference is null.");
                position += 1 + ReferenceBytes;
            }
        return new(key, body.Slice(9 + keyLength, valueLength).ToArray(), children[0], children[1], children[2]);
    }

    private static void WriteReference(Span<byte> bytes, MetadataPageReference? reference)
    {
        bytes.Clear(); if (reference is null) return; CheckReference(reference); bytes[0] = 1;
        BinaryPrimitives.WriteInt64LittleEndian(bytes[1..], reference.Container);
        BinaryPrimitives.WriteInt64LittleEndian(bytes[9..], reference.Offset);
        BinaryPrimitives.WriteInt32LittleEndian(bytes[17..], reference.Bytes);
        BinaryPrimitives.WriteInt64LittleEndian(bytes[21..], reference.Count);
        Convert.FromHexString(reference.ContentHash).CopyTo(bytes[29..]);
    }

    private static MetadataPageReference? ReadReference(ReadOnlySpan<byte> bytes)
    {
        if (bytes[0] == 0)
        { if (bytes[1..].ContainsAnyExcept((byte)0)) throw Invalid("A null metadata reference contains data."); return null; }
        if (bytes[0] != 1) throw Invalid("A metadata reference has an invalid tag.");
        var reference = new MetadataPageReference(BinaryPrimitives.ReadInt64LittleEndian(bytes[1..]),
            BinaryPrimitives.ReadInt64LittleEndian(bytes[9..]), BinaryPrimitives.ReadInt32LittleEndian(bytes[17..]),
            BinaryPrimitives.ReadInt64LittleEndian(bytes[21..]), Convert.ToHexStringLower(bytes.Slice(29, 32)));
        CheckReference(reference); return reference;
    }

    private static int Priority(string a, string b)
    {
        int order = SHA256.HashData(Utf8.GetBytes(a)).AsSpan().SequenceCompareTo(SHA256.HashData(Utf8.GetBytes(b)));
        return order != 0 ? order : StringComparer.Ordinal.Compare(a, b);
    }

    private void OpenOutput()
    {
        while (true)
        {
            string path = ContainerPath(container); WorkspaceStore.EnsurePlainPath(path);
            try
            {
                bool existed = File.Exists(path);
                output = new(path, existed ? FileMode.Open : FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read, 65536);
                if (!existed) boundary?.Invoke("metadata.container-created-before-owner");
                if (filePins is not null)
                {
                    if (existed)
                    {
                        try { VerifyContainerOwner(output, container); }
                        catch (Exception error) when (output.Length <= ContainerHeaderBytes && error is IOException or WorkspaceException)
                        {
                            // Creation may lose its reply before its ownership
                            // receipt is durable. Preserve that object and use a
                            // fresh container number; never adopt or delete it.
                            output.Dispose(); output = null; container = checked(container + 1); continue;
                        }
                    }
                    else WriteContainerOwner(output, container);
                }
                break;
            }
            catch (IOException error) when ((uint)error.HResult == 0x80070020 && File.Exists(path))
            {
                // An immutable reader may fence the previous append container.
                // Validate it, then append to a new owned container; never ask
                // the reader to drop its proof or open it writable underneath.
                using var prior = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1);
                CheckContainer(prior); container = checked(container + 1);
            }
        }
        if (output.Length < ContainerHeaderBytes)
        {
            byte[] header = ReadExactFile(Path.Combine(directory, "identity.bin"), ContainerHeaderBytes);
            byte[] prior = new byte[checked((int)output.Length)]; output.Position = 0; output.ReadExactly(prior);
            if (!prior.AsSpan().SequenceEqual(header.AsSpan(0, prior.Length))) throw Invalid("Preserve a changed metadata container initialization.");
            output.Write(header, prior.Length, header.Length - prior.Length); output.Flush(true);
        }
        else CheckContainer(output);
        output.Position = output.Length;
    }
    private void CheckContainer(FileStream input)
    {
        if (input.Length < ContainerHeaderBytes) throw Invalid("A metadata container header is truncated.");
        Span<byte> header = stackalloc byte[ContainerHeaderBytes]; input.Position = 0; input.ReadExactly(header);
        if (!header[..4].SequenceEqual(FormatMagic) || StoreId != Convert.ToHexStringLower(header.Slice(4, 12)) ||
            !SHA256.HashData(header[..16]).AsSpan().SequenceEqual(header[16..]))
            throw Invalid("A metadata container belongs to another store or is corrupt.");
    }
    private string ContainerPath(long number) => Path.Combine(directory, "pages-" + number.ToString("x16") + ".bin");
    private void InitializeIdentity(string identity, string? initialStoreId)
    {
        string pending = identity + ".pending"; WorkspaceStore.EnsurePlainPath(pending);
        using (var created = new FileStream(pending, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read))
        {
            if (created.Length > ContainerHeaderBytes) throw Invalid("Preserve an oversized metadata initialization.");
            byte[] value = new byte[ContainerHeaderBytes]; FormatMagic.CopyTo(value);
            if (initialStoreId is null) RandomNumberGenerator.Fill(value.AsSpan(4, 12));
            else
            {
                if (initialStoreId.Length != 24 || initialStoreId.Any(c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
                    throw Invalid("A fixed metadata initialization identity is malformed.");
                Convert.FromHexString(initialStoreId).CopyTo(value, 4);
            }
            int retained = checked((int)created.Length); byte[] prior = new byte[retained]; created.ReadExactly(prior);
            if (!prior.AsSpan(0, Math.Min(4, retained)).SequenceEqual(FormatMagic[..Math.Min(4, retained)]))
                throw Invalid("Preserve an invalid metadata initialization prefix.");
            if (initialStoreId is not null && retained > 4 && !prior.AsSpan(4, Math.Min(12, retained - 4)).SequenceEqual(value.AsSpan(4, Math.Min(12, retained - 4))))
                throw Invalid("Preserve an initialization prefix belonging to another fixed store.");
            prior.AsSpan(0, Math.Min(16, retained)).CopyTo(value);
            SHA256.HashData(value.AsSpan(0, 16), value.AsSpan(16));
            if (!prior.AsSpan().SequenceEqual(value.AsSpan(0, retained))) throw Invalid("Preserve a changed metadata initialization checksum.");
            if (retained < 16)
            {
                created.Write(value, retained, 16 - retained); created.Flush(true); retained = 16;
                boundary?.Invoke("metadata.identity-prefix-durable");
            }
            created.Position = retained; created.Write(value, retained, value.Length - retained); created.Flush(true);
            boundary?.Invoke("metadata.identity-durable");
        }
        File.Move(pending, identity); boundary?.Invoke("metadata.identity-published");
    }
    private void CheckRoot(MetadataTreeRoot root)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (root.Version != 1 || root.StoreId != StoreId) throw Invalid("Open the exact metadata root and store version.");
        if (root.Page is not null) CheckReference(root.Page);
    }
    private void CheckWriter()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!writable) throw Invalid("The metadata store is read-only.");
    }
    private static void CheckReference(MetadataPageReference reference)
    {
        if (reference.Container < 0 || reference.Offset < ContainerHeaderBytes || reference.Bytes is < PageHeaderBytes or > MaximumPageBytes ||
            reference.Count < 1 || reference.ContentHash is not { Length: 64 } || reference.ContentHash.Any(c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
            throw Invalid("A metadata page reference has invalid bounds.");
    }
    private static void ValidateEntry(string key, int valueBytes)
    {
        if (string.IsNullOrEmpty(key) || key.Contains('\0') || (long)Utf8.GetByteCount(key) + valueBytes + 9 + ReferencesPerNode * (1 + ReferenceBytes) + PageHeaderBytes > MaximumPageBytes)
            throw Invalid("A metadata entry must fit one logical page; split large values into referenced pages.");
    }
    private static byte[] ReadExactFile(string path, int bytes)
    {
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length != bytes) throw Invalid("A metadata identity has invalid length.");
        byte[] value = new byte[bytes]; input.ReadExactly(value); return value;
    }
    private static WorkspaceException Invalid(string message) => new("MetadataStoreInvalid", message);

    private sealed class Tree(PagedMetadataStore store, MetadataTreeRoot root) : IReadOnlyMetadataTree
    {
        public MetadataTreeRoot Root => root;
        public byte[]? Find(string key) => FindEntry(key)?.Value.ToArray();
        public MetadataEntry? FindEntry(string key)
        {
            store.CheckRoot(root); var page = root.Page;
            while (page is not null)
            {
                var node = store.ReadNode(page); int order = StringComparer.Ordinal.Compare(key, node.Key);
                if (order == 0) return new(node.Key, node.Value.ToArray(), node.Data is null ? null : new MetadataTreeRoot(1, store.StoreId, node.Data));
                page = order < 0 ? node.Left : node.Right;
            }
            return null;
        }
        public MetadataEntry? Floor(string key)
        {
            store.CheckRoot(root); var page = root.Page; Node? candidate = null;
            while (page is not null)
            {
                var node = store.ReadNode(page); int order = StringComparer.Ordinal.Compare(key, node.Key);
                if (order == 0) { candidate = node; break; }
                if (order > 0) { candidate = node; page = node.Right; } else page = node.Left;
            }
            return candidate is null ? null : new(candidate.Key, candidate.Value.ToArray(),
                candidate.Data is null ? null : new MetadataTreeRoot(1, store.StoreId, candidate.Data));
        }
        public IEnumerable<MetadataEntry> Enumerate(string? afterKey = null)
        {
            store.CheckRoot(root);
            using var pending = new TraversalSpine();
            string? previous = afterKey; long seen = 0; var page = root.Page;
            while (true)
            {
                while (page is not null)
                {
                    var node = store.ReadNode(page);
                    if (afterKey is null || StringComparer.Ordinal.Compare(node.Key, afterKey) > 0)
                    { pending.Push(page); page = node.Left; }
                    else page = node.Right;
                }
                if (!pending.TryPop(out var next))
                {
                    if (afterKey is null && seen != root.Count) throw Invalid("Metadata tree ordering or membership differs from its root.");
                    yield break;
                }
                var candidate = store.ReadNode(next);
                if (previous is not null && StringComparer.Ordinal.Compare(previous, candidate.Key) >= 0)
                    throw Invalid("Metadata tree keys are duplicated or out of order.");
                seen = checked(seen + 1);
                previous = candidate.Key; yield return new(candidate.Key, candidate.Value.ToArray(), candidate.Data is null ? null : new MetadataTreeRoot(1, store.StoreId, candidate.Data));
                page = candidate.Right;
            }
        }
        public MetadataReadPage ReadPage(MetadataCursor? cursor = null, int pageSize = 128)
        {
            if (pageSize is < 1 or > 1024 || cursor is not null &&
                (cursor.RootHash != root.ContentHash || string.IsNullOrEmpty(cursor.AfterKey) || cursor.AfterKey.Contains('\0')))
                throw new WorkspaceException("MetadataCursorInvalid", "Use a bounded page and the cursor of the exact immutable root.");
            using var source = Enumerate(cursor?.AfterKey).GetEnumerator();
            var items = new List<MetadataEntry>(pageSize);
            while (items.Count < pageSize && source.MoveNext()) items.Add(source.Current);
            bool more = source.MoveNext();
            return new(root, items.ToArray(), more ? new(root.ContentHash, items[^1].Key) : null);
        }
        public void Verify(CancellationToken cancellationToken = default)
        {
            // The complete checked cursor proves each visited page checksum/logical
            // hash, subtree count, earlier child address, strict key order,
            // uniqueness and final membership count. Looking up every yielded
            // key again adds O(N log N) work without another data invariant.
            foreach (var _ in Enumerate())
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }

    // Normal traversal retains at most 64 addresses. Deep/adversarial trees
    // spill only addresses to a fresh, private, delete-on-close scratch file;
    // read-only stores and immutable containers are never modified by a cursor.
    private sealed class TraversalSpine : IDisposable
    {
        private readonly List<MetadataPageReference> memory = [];
        private FileStream? spill;
        private long top = -1;
        public void Push(MetadataPageReference page)
        {
            if (memory.Count == 64)
            {
                foreach (var prior in memory) Spill(prior);
                memory.Clear();
            }
            memory.Add(page);
        }
        private void Spill(MetadataPageReference page)
        {
            if (spill is null)
            {
                string path = Path.Combine(Path.GetTempPath(), "splinter-metadata-cursor-" + Guid.NewGuid().ToString("N") + ".tmp");
                WorkspaceStore.EnsurePlainPath(path);
                spill = new(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 4096,
                    FileOptions.DeleteOnClose | FileOptions.RandomAccess);
            }
            Span<byte> record = stackalloc byte[8 + 1 + ReferenceBytes];
            BinaryPrimitives.WriteInt64LittleEndian(record, top); WriteReference(record[8..], page);
            long at = spill.Length; spill.Position = at; spill.Write(record); top = at;
        }
        public bool TryPop(out MetadataPageReference page)
        {
            if (memory.Count > 0) { page = memory[^1]; memory.RemoveAt(memory.Count - 1); return true; }
            if (top < 0) { page = null!; return false; }
            spill!.Position = top; Span<byte> record = stackalloc byte[8 + 1 + ReferenceBytes]; spill.ReadExactly(record);
            long previous = BinaryPrimitives.ReadInt64LittleEndian(record);
            if (previous >= top || previous < -1) throw Invalid("An owned metadata cursor spine is corrupt.");
            top = previous; page = ReadReference(record[8..]) ?? throw Invalid("A metadata cursor contains a null address."); return true;
        }
        public void Dispose() => spill?.Dispose();
    }

    private sealed class Spine : IDisposable
    {
        private FileStream? stream;
        private readonly string path;
        private readonly List<(Node Node, bool ReplaceLeft)> memory = [];
        private long top = -1;
        private Node? peek;
        private bool peekReplaceLeft;
        public Spine(PagedMetadataStore store)
        {
            path = Path.Combine(store.directory, "build-" + Guid.NewGuid().ToString("N") + ".pending");
        }
        public void Push(Node node, bool replaceLeft = false)
        {
            if (memory.Count == 64)
            {
                foreach (var prior in memory) Spill(prior.Node, prior.ReplaceLeft);
                memory.Clear();
            }
            memory.Add((node, replaceLeft));
        }
        private void Spill(Node node, bool replaceLeft)
        {
            if (stream is null)
            {
                WorkspaceStore.EnsurePlainPath(path);
                // This is private construction scratch, never a durable root
                // or reader anchor. Delete the exact opened object on every
                // close, including failure/process teardown; a pathname delete
                // only on successful completion leaves recovery-blocking gaps.
                stream = new(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 65536,
                    FileOptions.SequentialScan | FileOptions.DeleteOnClose);
                stream.Write("SPS1"u8);
            }
            byte[] bytes = Encode(node); long next = stream.Length; stream.Position = next;
            Span<byte> header = stackalloc byte[12]; BinaryPrimitives.WriteInt64LittleEndian(header, top);
            BinaryPrimitives.WriteInt32LittleEndian(header[8..], bytes.Length | (replaceLeft ? 1 << 30 : 0));
            stream.Write(header); stream.Write(bytes); top = next; peek = null;
        }
        public bool TryPeek(out Node node)
        {
            if (memory.Count > 0) { node = memory[^1].Node; return true; }
            if (top < 0) { node = null!; return false; }
            if (peek is null)
            {
                stream!.Position = top; Span<byte> header = stackalloc byte[12]; stream.ReadExactly(header);
                int flags = BinaryPrimitives.ReadInt32LittleEndian(header[8..]);
                int length = flags & ~(1 << 30); peekReplaceLeft = (flags & (1 << 30)) != 0;
                if (length is < 1 or > MaximumPageBytes) throw Invalid("An owned metadata build spine is corrupt.");
                byte[] bytes = new byte[length]; stream.ReadExactly(bytes); peek = Decode(bytes);
            }
            node = peek; return true;
        }
        public bool TryPop(out Node node) => TryPop(out node, out _);
        public bool TryPop(out Node node, out bool replaceLeft)
        {
            replaceLeft = false;
            if (!TryPeek(out node)) return false;
            if (memory.Count > 0) { replaceLeft = memory[^1].ReplaceLeft; memory.RemoveAt(memory.Count - 1); return true; }
            replaceLeft = peekReplaceLeft;
            stream!.Position = top; Span<byte> previous = stackalloc byte[8]; stream.ReadExactly(previous);
            long prior = BinaryPrimitives.ReadInt64LittleEndian(previous);
            if (prior >= top || prior is < -1) throw Invalid("An owned metadata build spine has an invalid predecessor.");
            top = prior; peek = null; return true;
        }
        public void Complete() { }
        public void Dispose() => stream?.Dispose();
    }

    // Process-wide, lazy accounting. Entries are reusable only while their
    // store instance retains an exclusive writer or read-only container fence.
    private static class PageCache
    {
        private static long Budget => StorageResources.MetadataCacheBytes;
        private sealed record Item(string Key, object Value, int Charge, Group Group)
        { internal LinkedListNode<Item>? GroupNode; }
        private sealed class Scope(string name)
        { internal readonly string Name = name; internal readonly LinkedList<Group> Groups = []; }
        private sealed class Group(string prefix, Scope scope)
        {
            internal readonly string Prefix = prefix;
            internal readonly Scope Scope = scope;
            internal readonly LinkedList<Item> Entries = [];
            internal LinkedListNode<Group>? ScopeNode;
        }
        private static readonly object Gate = new();
        private static readonly Dictionary<string, LinkedListNode<Item>> Items = new(StringComparer.Ordinal);
        private static readonly LinkedList<Item> Recent = new();
        private static readonly Dictionary<string, Scope> Scopes = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, Group> Groups = new(StringComparer.Ordinal);
        private static long bytes;
        public static long ChargedBytes { get { lock (Gate) return bytes; } }
        private static long peakBytes;
        public static long PeakChargedBytes { get { lock (Gate) return peakBytes; } }
        public static bool TryGet(string key, out Node node)
        {
            lock (Gate)
            {
                if (!Items.TryGetValue(key, out var found)) { node = null!; return false; }
                Recent.Remove(found); Recent.AddFirst(found); node = (Node)found.Value.Value; return true;
            }
        }
        public static void Add(string key, Node node, int encodedBytes)
        {
            int charge = checked(encodedBytes + key.Length * 2 + node.Key.Length * 2 + node.Value.Length + 768 + IndexCharge);
            AddValue(key, node, charge);
        }
        public static bool TryGetBlock(string key, out byte[] block)
        {
            lock (Gate)
            {
                if (!Items.TryGetValue(key, out var found)) { block = null!; return false; }
                Recent.Remove(found); Recent.AddFirst(found); block = (byte[])found.Value.Value; return true;
            }
        }
        // Includes both linked memberships and conservative per-entry scope/
        // group dictionary/list overhead, including a one-entry partition.
        private const int IndexCharge = 768;
        public static void AddBlock(string key, byte[] block) => AddValue(key, block, checked(block.Length + key.Length * 2 + 256 + IndexCharge));
        private static void AddValue(string key, object value, int charge)
        {
            lock (Gate)
            {
                if (Items.ContainsKey(key)) return;
                while (bytes + charge > Budget && Recent.Last is { } victim)
                    RemoveItem(victim);
                int first = key.IndexOf('/'), second = key.IndexOf('/', first + 1);
                if (first < 1 || second < 0) throw Invalid("An internal metadata cache key has no ownership partition.");
                string name = key[..first], prefix = key[..(second + 1)];
                if (!Scopes.TryGetValue(name, out var scope)) { scope = new(name); Scopes.Add(name, scope); }
                if (!Groups.TryGetValue(prefix, out var group))
                { group = new(prefix, scope); group.ScopeNode = scope.Groups.AddFirst(group); Groups.Add(prefix, group); }
                var valueItem = new Item(key, value, charge, group);
                var item = Recent.AddFirst(valueItem); valueItem.GroupNode = group.Entries.AddFirst(valueItem);
                Items.Add(key, item); bytes += charge;
                peakBytes = Math.Max(peakBytes, bytes);
            }
        }
        private static void RemoveItem(LinkedListNode<Item> item)
        {
            var value = item.Value; var group = value.Group;
            Recent.Remove(item); Items.Remove(value.Key); group.Entries.Remove(value.GroupNode!); bytes -= value.Charge;
            if (group.Entries.Count != 0) return;
            Groups.Remove(group.Prefix); group.Scope.Groups.Remove(group.ScopeNode!);
            if (group.Scope.Groups.Count == 0) Scopes.Remove(group.Scope.Name);
        }
        public static long Remove(string prefix)
        {
            lock (Gate)
            {
                long visits = 0;
                void RemoveGroup(Group group)
                {
                    while (group.Entries.First is { } item)
                    { RemoveItem(Items[item.Value.Key]); visits++; }
                }
                if (prefix.EndsWith('/'))
                { if (Groups.TryGetValue(prefix, out var group)) RemoveGroup(group); }
                else if (Scopes.TryGetValue(prefix, out var scope))
                    while (scope.Groups.First is { } group) RemoveGroup(group.Value);
                return visits;
            }
        }
    }
}
