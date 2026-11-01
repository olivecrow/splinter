using System.Buffers.Binary;
using System.Collections;
using System.Text;
using Splinter.Core;

namespace Splinter.Runtime;

// Persistent immutable-page indices. Journal ACKs own durability between frozen
// roots; reconnect replays only the current append unit. Enumeration binds its
// starting root, so later mutations cannot change an existing cursor.
public sealed partial class JournalDeltaStore
{
    private PagedMetadataStore? pagedIndex;
    private string? pagedIndexDirectory;
    private bool pagedIndexCaptured;
    private MetadataTreeRoot? NodeRoot => (nodes as PagedNodeMap)?.Root;
    private MetadataTreeRoot? PathRoot => (paths as PagedPathNamespace)?.Root;
    private MetadataTreeRoot? HiddenRoot => (hidden as PagedSet<string>)?.Root;
    private MetadataTreeRoot? ChangeRoot => (pathChanges as PagedMap<string, long>)?.Root;

    public IEnumerable<DeltaEntry> EnumerateChildren(string directory)
    {
        using (EnterGate())
        {
            Check();
            if (pagedIndex is null) return List(directory);
            var parent = Lookup(NormalizePath(directory)) ?? throw new DirectoryNotFoundException(directory);
            if (!parent.Entry.Directory) throw new IOException("Not a directory.");
            return ProtectIndexCursor(PagedChildren(parent.Entry, pagedIndex, NodeRoot!, PathRoot!, HiddenRoot!, pathComponents!.Root,
                (PagedPathNamespace)paths, pathComponents));
        }
    }
    private IEnumerable<DeltaEntry> PagedChildren(DeltaEntry parent, PagedMetadataStore index, MetadataTreeRoot fixedNodes,
        MetadataTreeRoot fixedPaths, MetadataTreeRoot fixedHidden, MetadataTreeRoot fixedNames,
        PagedPathNamespace namesIndex, PathComponents components)
    {
        string path = parent.Path; string? lower = parent.BasePath;
        IEnumerable<MetadataEntry> Names()
        {
            if (lower is not null)
                foreach (var entry in snapshot.EnumerateChildren(lower))
                { string child = Name(entry.Path); yield return new(pathPolicy.IdentityPath(child), Encoding.UTF8.GetBytes(child)); }
            foreach (var child in namesIndex.Children(path, fixedPaths))
            {
                string component = Name(child.Key);
                yield return new(pathPolicy.IdentityPath(component), Encoding.UTF8.GetBytes(component));
            }
        }
        string? previous = null;
        foreach (var item in MetadataExternalSort.Order(Names(), Path.GetTempPath(), default, sourceFilePins))
        {
            if (previous == item.Key) continue; previous = item.Key;
            string child = Join(path, Encoding.UTF8.GetString(item.Value.Span));
            if (Denied(child) || Identity.ExcludedTopLevel?.Contains(child.Split('/')[0], pathPolicy.Comparer) == true ||
                Identity.IncludedTopLevel is { } included && !included.Contains(child.Split('/')[0], pathPolicy.Comparer)) continue;
            if (namesIndex.TryGetValueAt(child, fixedPaths, out Guid childId))
            {
                var node = index.Open(fixedNodes).FindEntry(childId.ToString("N")) ?? throw PagedInvalid();
                yield return DecodeEntry(node.Value.ToArray(), components, fixedNames); continue;
            }
            if (index.Open(fixedHidden).Find(PathComponents.Key(child, pathPolicy)) is not null || lower is null) continue;
            string original = Join(lower, Name(child)); var entry = snapshot.Stat(original);
            if (entry is null) throw new WorkspaceException("SnapshotMissing", "An immutable directory child disappeared.");
            var digest = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(Identity.Snapshot.Id + "/" + pathPolicy.IdentityPath(entry.Path)));
            yield return new(new Guid(digest.AsSpan(0, 16)), child, entry.Directory, entry.Length, entry.Attributes,
                entry.CreationTime, entry.LastAccessTime, entry.LastWriteTime, original, entry.Length, UnixMode: entry.UnixMode);
        }
    }

    private void InitializePagedIndex(string directory, bool usePages, JournalIndexRoots? checkpoint = null, PagedMetadataStore? supplied = null)
    {
        if (!usePages)
        {
            nodes = new Dictionary<Guid, Node>(); paths = new Dictionary<string, Guid>(pathPolicy.Comparer);
            legacyChildren = new(pathPolicy.Comparer);
            hidden = new HashSet<string>(pathPolicy.Comparer); pathChanges = new Dictionary<string, long>(pathPolicy.Comparer);
            return;
        }
        // Never reuse an uncommitted root as an authority. Reconnection replays
        // the authenticated journal into a new index; old capture roots keep
        // their exact store and pages until their owner explicitly reclaims it.
        pagedIndexDirectory = checkpoint?.Directory ?? Path.Combine(directory, "index-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pagedIndexDirectory);
        if (checkpoint is null) SourceCaptureTree.Publish(Path.Combine(pagedIndexDirectory, "owner.json"), new PagedIndexOwner(1, PagedOwnerHash(Identity)));
        pagedIndex = supplied ?? new(pagedIndexDirectory, writable: true, filePins: sourceFilePins);
        legacyChildren = null;
        pathComponents = new(pagedIndex);
        nodes = new PagedNodeMap(pagedIndex, pathComponents);
        boundedDurableNodes = new PagedSet<Guid>(new(pagedIndex, g => "g" + g.ToString("N"), s => Guid.ParseExact(s[1..], "N"), _ => [], _ => 0));
        string PathKey(string value) => PathComponents.Key(value, pathPolicy);
        paths = new PagedPathNamespace(pagedIndex, pathPolicy, id => nodes[id].Entry.Directory);
        hidden = new PagedSet<string>(new(pagedIndex, PathKey, s => s, _ => [], _ => 0, pathComponents));
        pathChanges = new PagedMap<string, long>(pagedIndex, PathKey, s => s[1..], BitConverter.GetBytes,
            b => b.Length == 8 ? BinaryPrimitives.ReadInt64LittleEndian(b) : throw PagedInvalid(), pathComponents);
        segmentRoot = checkpoint?.Segments ?? pagedIndex.Empty;
        adoptionRoot = checkpoint?.Adoptions ?? pagedIndex.Empty;
        captureRoot = checkpoint?.Captures ?? pagedIndex.Empty;
        packingRoot = checkpoint?.Packing ?? pagedIndex.Empty;
        nextDataSegment = checkpoint?.NextDataSegment ?? -2; dataSegmentBytes = checkpoint?.DataSegmentBytes ?? 0; dataPayloadBytes = checkpoint?.DataPayloadBytes ?? 0;
        if (checkpoint is not null)
        {
            ((PagedNodeMap)nodes).Root = checkpoint.Nodes; ((PagedPathNamespace)paths).Root = checkpoint.Paths;
            ((PagedSet<string>)hidden).Root = checkpoint.Hidden; ((PagedMap<string, long>)pathChanges).Root = checkpoint.Changes;
            pathComponents.Root = checkpoint.Names; ((PagedSet<Guid>)boundedDurableNodes).Root = checkpoint.DurableNodes;
            sequence = checkpoint.Sequence; payloadBytes = checkpoint.PayloadBytes; logicalPayloadBytes = checkpoint.DecodedPayloadBytes; extentCount = checkpoint.ExtentCount;
            sourceMutationHash = checkpoint.SourceMutationHash ?? PagedOwnerHash(Identity); sourceMutationBytes = checkpoint.SourceMutationBytes;
            if (sourceMutationHash.Length != 64 || sourceMutationHash.Any(c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f')) || sourceMutationBytes < 0) throw PagedInvalid();
        }
    }
    private static WorkspaceException PagedInvalid() => new("DeltaCorrupt", "A paged journal index record is invalid.");

    private sealed class PagedMap<TKey, TValue>(PagedMetadataStore store, Func<TKey, string> key,
        Func<string, TKey> decodeKey, Func<TValue, byte[]> encode, Func<byte[], TValue> decode, PathComponents? components = null) : IDictionary<TKey, TValue> where TKey : notnull
    {
        public MetadataTreeRoot Root = store.Empty;
        public int Count => (int)Math.Min(int.MaxValue, Root.Count);
        public long LongCount => Root.Count;
        public bool IsReadOnly => false;
        public ICollection<TKey> Keys => new CursorCollection<TKey>(() => this.Select(p => p.Key), () => Count);
        public ICollection<TValue> Values => new CursorCollection<TValue>(() => this.Select(p => p.Value), () => Count);
        public TValue this[TKey k]
        {
            get => TryGetValue(k, out var value) ? value : throw new KeyNotFoundException();
            set => Root = store.Set(Root, key(k), Pack(k, value));
        }
        public bool TryGetValue(TKey k, out TValue value)
        {
            var entry = store.Open(Root).Find(key(k));
            if (entry is null) { value = default!; return false; }
            value = Unpack(entry).Value; return true;
        }
        public bool ContainsKey(TKey k) => store.Open(Root).Find(key(k)) is not null;
        public void Add(TKey k, TValue value) { if (ContainsKey(k)) throw new ArgumentException("Duplicate index key."); this[k] = value; }
        public bool Remove(TKey k) { if (!ContainsKey(k)) return false; Root = store.Remove(Root, key(k)); return true; }
        public bool Remove(TKey k, out TValue value) { if (!TryGetValue(k, out value!)) return false; Root = store.Remove(Root, key(k)); return true; }
        public void Clear() => Root = store.Empty;
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            var fixedRoot = Root;
            foreach (var entry in store.Open(fixedRoot).Enumerate())
            { var pair = Unpack(entry.Value.ToArray()); yield return new(pair.Key, pair.Value); }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);
        public bool Contains(KeyValuePair<TKey, TValue> item) => TryGetValue(item.Key, out var value) && EqualityComparer<TValue>.Default.Equals(value, item.Value);
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int index) { foreach (var p in this) array[index++] = p; }
        public bool Remove(KeyValuePair<TKey, TValue> item) => Contains(item) && Remove(item.Key);
        private byte[] Pack(TKey k, TValue value)
        {
            bool referenced = typeof(TKey) == typeof(string) && components is not null;
            byte[] name = referenced ? components!.Intern((string)(object)k) : Encoding.UTF8.GetBytes(typeof(TKey) == typeof(string) ? (string)(object)k : key(k)), body = encode(value);
            byte[] bytes = new byte[checked(4 + name.Length + body.Length)]; BinaryPrimitives.WriteInt32LittleEndian(bytes, referenced ? -32 : name.Length);
            name.CopyTo(bytes, 4); body.CopyTo(bytes, 4 + name.Length); return bytes;
        }
        private (TKey Key, TValue Value) Unpack(byte[] bytes)
        {
            if (bytes.Length < 4) throw PagedInvalid(); int length = BinaryPrimitives.ReadInt32LittleEndian(bytes);
            bool referenced = length == -32 && components is not null;
            if (referenced) length = 32;
            if (length < 0 || length > bytes.Length - 4) throw PagedInvalid(); string name = referenced ? components!.Resolve(bytes.AsSpan(4, 32)) : Encoding.UTF8.GetString(bytes, 4, length);
            var original = typeof(TKey) == typeof(string) ? (TKey)(object)name : decodeKey(name);
            return (original, decode(bytes.AsSpan(4 + length).ToArray()));
        }
    }
    private sealed class CursorCollection<T>(Func<IEnumerable<T>> cursor, Func<int> count) : ICollection<T>
    {
        public int Count => count(); public bool IsReadOnly => true;
        public bool Contains(T value) => cursor().Contains(value);
        public IEnumerator<T> GetEnumerator() => cursor().GetEnumerator(); IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public void CopyTo(T[] array, int at) { foreach (var item in cursor()) array[at++] = item; }
        public void Add(T _) => throw new NotSupportedException(); public void Clear() => throw new NotSupportedException();
        public bool Remove(T _) => throw new NotSupportedException();
    }
    private sealed class PagedSet<T>(PagedMap<T, byte> map) : ISet<T> where T : notnull
    {
        public MetadataTreeRoot Root { get => map.Root; set => map.Root = value; }
        public int Count => map.Count; public bool IsReadOnly => false;
        public bool Add(T value) { if (map.ContainsKey(value)) return false; map[value] = 0; return true; }
        void ICollection<T>.Add(T item) => Add(item);
        public bool Remove(T item) => map.Remove(item); public bool Contains(T item) => map.ContainsKey(item);
        public void Clear() => map.Clear(); public IEnumerator<T> GetEnumerator() => map.Keys.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator(); public void CopyTo(T[] array, int at) { foreach (var item in this) array[at++] = item; }
        public void UnionWith(IEnumerable<T> other) { foreach (var value in other) Add(value); }
        public void ExceptWith(IEnumerable<T> other) { foreach (var value in other) Remove(value); }
        public void IntersectWith(IEnumerable<T> other) => throw new NotSupportedException();
        public void SymmetricExceptWith(IEnumerable<T> other) => throw new NotSupportedException();
        public bool IsSubsetOf(IEnumerable<T> other) => throw new NotSupportedException();
        public bool IsSupersetOf(IEnumerable<T> other) => other.All(Contains);
        public bool IsProperSupersetOf(IEnumerable<T> other) => throw new NotSupportedException();
        public bool IsProperSubsetOf(IEnumerable<T> other) => throw new NotSupportedException();
        public bool Overlaps(IEnumerable<T> other) => other.Any(Contains);
        public bool SetEquals(IEnumerable<T> other) => throw new NotSupportedException();
    }

    private sealed class PagedNodeMap(PagedMetadataStore store, PathComponents components) : IDictionary<Guid, Node>
    {
        private MetadataTreeRoot root = store.Empty;
        private readonly Dictionary<Guid, MetadataEntry> pending = new();
        private bool batching;
        public MetadataTreeRoot Root
        {
            get
            {
                // A checkpoint/capture may request a root after flushing the
                // store. Never expose pages appended by this deferred flush
                // behind that publication's durability barrier.
                if (pending.Count != 0) { Flush(); store.Flush(); }
                return root;
            }
            set { Flush(); root = value; }
        }
        public void BeginBatch() { if (batching) throw new InvalidOperationException("Nested node batches are unsupported."); batching = true; }
        public void EndBatch() { pending.Clear(); batching = false; }
        public void Flush()
        {
            if (pending.Count == 0) return;
            root = store.SetBatch(root, pending.Values.ToArray()); pending.Clear();
        }
        public int Count => (int)Math.Min(int.MaxValue, Root.Count); public bool IsReadOnly => false;
        public ICollection<Guid> Keys => new CursorCollection<Guid>(() => this.Select(p => p.Key), () => Count);
        public ICollection<Node> Values => new CursorCollection<Node>(() => this.Select(p => p.Value), () => Count);
        private Node Decode(MetadataEntry item)
        {
            var node = new Node(DecodeEntry(item.Value.ToArray(), components));
            node.Attach(store, item.Child ?? store.Empty, () => Save(node)); return node;
        }
        private void Save(Node node)
        {
            var entry = new MetadataEntry(node.Entry.Id.ToString("N"), EncodeEntry(node.Entry, components), node.Extents.Root);
            if (!batching) { root = store.Set(root, entry.Key, entry.Value.Span, entry.Child); return; }
            if (pending.Count == 64 && !pending.ContainsKey(node.Entry.Id)) Flush();
            pending[node.Entry.Id] = entry;
        }
        public Node this[Guid id]
        {
            get => TryGetValue(id, out var node) ? node : throw new KeyNotFoundException();
            set { value.Attach(store, value.Extents, () => Save(value)); Save(value); }
        }
        public bool TryGetValue(Guid id, out Node node)
        {
            var item = pending.TryGetValue(id, out var changed) ? changed : store.Open(root).FindEntry(id.ToString("N"));
            if (item is null) { node = null!; return false; } node = Decode(item); return true;
        }
        public bool ContainsKey(Guid id) => pending.ContainsKey(id) || store.Open(root).Find(id.ToString("N")) is not null;
        public void Add(Guid id, Node node) { if (ContainsKey(id) || id != node.Entry.Id) throw new ArgumentException("Duplicate journal node."); this[id] = node; }
        public bool Remove(Guid id) { if (!ContainsKey(id)) return false; Root = store.Remove(Root, id.ToString("N")); return true; }
        public void Clear() => Root = store.Empty;
        public IEnumerator<KeyValuePair<Guid, Node>> GetEnumerator()
        {
            var fixedRoot = Root;
            foreach (var item in store.Open(fixedRoot).Enumerate()) { var node = Decode(item); yield return new(node.Entry.Id, node); }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public void Add(KeyValuePair<Guid, Node> item) => Add(item.Key, item.Value);
        public bool Contains(KeyValuePair<Guid, Node> item) => TryGetValue(item.Key, out var value) && value == item.Value;
        public void CopyTo(KeyValuePair<Guid, Node>[] array, int at) { foreach (var item in this) array[at++] = item; }
        public bool Remove(KeyValuePair<Guid, Node> item) => Contains(item) && Remove(item.Key);
    }

    private static byte[] EncodeEntry(DeltaEntry entry, PathComponents components)
    {
        using var data = new MemoryStream(); using var output = new BinaryWriter(data, Encoding.UTF8, true);
        output.Write((byte)2); output.Write(entry.Id.ToByteArray()); output.Write(components.Intern(entry.Path)); output.Write(entry.Directory);
        output.Write(entry.Length); output.Write(entry.Attributes); output.Write(entry.CreationTime); output.Write(entry.LastAccessTime); output.Write(entry.LastWriteTime);
        output.Write(entry.BasePath is not null); if (entry.BasePath is not null) output.Write(components.Intern(entry.BasePath));
        output.Write(entry.BaseLength); output.Write(entry.Deleted); output.Write(entry.UnixMode.HasValue); if (entry.UnixMode.HasValue) output.Write(entry.UnixMode.Value);
        return data.ToArray();
    }
    private static DeltaEntry DecodeEntry(byte[] data, PathComponents components, MetadataTreeRoot? fixedNames = null)
    {
        try
        {
            using var input = new BinaryReader(new MemoryStream(data), Encoding.UTF8);
            if (input.ReadByte() != 2) throw PagedInvalid();
            var entry = new DeltaEntry(new Guid(input.ReadBytes(16)), components.Resolve(input.ReadBytes(32), fixedNames), input.ReadBoolean(), input.ReadInt64(), input.ReadUInt32(),
                input.ReadInt64(), input.ReadInt64(), input.ReadInt64(), input.ReadBoolean() ? components.Resolve(input.ReadBytes(32), fixedNames) : null,
                input.ReadInt64(), input.ReadBoolean(), input.ReadBoolean() ? input.ReadUInt32() : null);
            if (input.BaseStream.Position != input.BaseStream.Length) throw PagedInvalid(); return entry;
        }
        catch (Exception ex) when (ex is EndOfStreamException or ArgumentException or IOException) { throw PagedInvalid(); }
    }

    // The compatibility list surface uses rank/select, never a whole range
    // array. Ordered writes key ranges by 64-bit logical file offset.
    private sealed class ExtentList : IList<Extent>, IReadOnlyList<Extent>
    {
        private List<Extent>? memory = [];
        private PagedMetadataStore? store; private MetadataTreeRoot? root; private Action? changed;
        public MetadataTreeRoot? Root => root;
        public long LongCount => store is null ? memory!.Count : root!.Count;
        public int Count => (int)Math.Min(int.MaxValue, LongCount);
        public bool IsReadOnly => false;
        private static string Key(long offset) => offset.ToString("x16", System.Globalization.CultureInfo.InvariantCulture);
        public Extent this[int index]
        {
            get => store is null ? memory![index] : DecodeExtent(store.EntryAt(root!, index).Value.ToArray());
            set
            {
                if (store is null) { memory![index] = value; return; }
                var before = this[index]; root = store.Remove(root!, Key(before.Start)); root = store.Set(root, Key(value.Start), EncodeExtent(value)); changed!();
            }
        }
        public void Attach(PagedMetadataStore target, MetadataTreeRoot fixedRoot, Action callback)
        { store = target; root = fixedRoot; memory = null; changed = callback; }
        public void Attach(PagedMetadataStore target, Action callback)
        {
            if (store == target) { changed = callback; return; }
            var entries = this.Select(e => new MetadataEntry(Key(e.Start), EncodeExtent(e)));
            var next = target.Build(entries); store = target; root = next; memory = null; changed = callback;
        }
        public void Add(Extent item)
        {
            if (store is null) { memory!.Add(item); return; }
            if (store.Open(root!).Find(Key(item.Start)) is not null) throw PagedInvalid();
            root = store.Set(root!, Key(item.Start), EncodeExtent(item)); changed!();
        }
        public void Clear() { if (store is null) memory!.Clear(); else { root = store.Empty; changed!(); } }
        public IEnumerator<Extent> GetEnumerator()
        {
            if (store is null) { foreach (var extent in memory!) yield return extent; }
            else { var fixedRoot = root!; foreach (var item in store.Open(fixedRoot).Enumerate()) yield return DecodeExtent(item.Value.ToArray()); }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public IEnumerable<Extent> From(long offset)
        {
            if (store is null)
            {
                int first = FindExtent(this, offset).Index;
                for (int i = first; i < memory!.Count; i++) yield return memory[i];
                yield break;
            }
            var tree = store.Open(root!); var floor = tree.Floor(Key(offset));
            if (floor is not null)
            { var extent = DecodeExtent(floor.Value.ToArray()); if (extent.Start + extent.Length > offset) yield return extent; }
            foreach (var item in tree.Enumerate(floor?.Key ?? Key(offset))) yield return DecodeExtent(item.Value.ToArray());
        }
        public bool ReplacePaged(Extent incoming)
        {
            if (store is null) return false;
            long end = checked(incoming.Start + incoming.Length);
            foreach (var extent in From(incoming.Start))
            {
                if (extent.Start >= end) break;
                root = store.Remove(root!, Key(extent.Start));
                if (extent.Start < incoming.Start)
                { var prefix = extent with { Length = checked((int)(incoming.Start - extent.Start)) }; root = store.Set(root, Key(prefix.Start), EncodeExtent(prefix)); }
                if (extent.Start + extent.Length > end)
                {
                    int skip = checked((int)(end - extent.Start));
                    var suffix = extent with { Start = end, Length = extent.Length - skip, BlockOffset = checked(extent.BlockOffset + skip),
                        OriginOffset = extent.OriginBlock is null ? 0 : checked(extent.OriginOffset + skip) };
                    root = store.Set(root, Key(suffix.Start), EncodeExtent(suffix));
                }
            }
            root = store.Set(root!, Key(incoming.Start), EncodeExtent(incoming)); changed!(); return true;
        }
        public int IndexOf(Extent item)
        {
            if (store is null) return memory!.IndexOf(item);
            long rank = store.Rank(root!, Key(item.Start)); return rank < root!.Count && this[checked((int)rank)] == item ? checked((int)rank) : -1;
        }
        public bool Contains(Extent item) => store is null ? memory!.Contains(item) :
            store.Open(root!).FindEntry(Key(item.Start)) is { } entry && DecodeExtent(entry.Value.ToArray()) == item;
        public void CopyTo(Extent[] array, int at) { foreach (var item in this) array[at++] = item; }
        public void Insert(int index, Extent item) { if (store is null) memory!.Insert(index, item); else Add(item); }
        public void RemoveAt(int index)
        { if (store is null) memory!.RemoveAt(index); else { var prior = this[index]; root = store.Remove(root!, Key(prior.Start)); changed!(); } }
        public bool Remove(Extent item)
        {
            if (store is not null)
            { if (!Contains(item)) return false; root = store.Remove(root!, Key(item.Start)); changed!(); return true; }
            int index = IndexOf(item); if (index < 0) return false; RemoveAt(index); return true;
        }
        public void EnsureCapacity(int count) { memory?.EnsureCapacity(count); }
        public void Truncate(long length)
        {
            if (store is null) { memory = memory!.Where(e => e.Start < length).Select(e => e with { Length = (int)Math.Min(e.Length, length - e.Start) }).ToList(); return; }
            foreach (var extent in From(length))
            {
                if (extent.Start >= length) root = store.Remove(root!, Key(extent.Start));
                else if (extent.Start + extent.Length > length)
                    root = store.Set(root!, Key(extent.Start), EncodeExtent(extent with { Length = checked((int)(length - extent.Start)) }));
            }
            changed!();
        }
        public void RemoveRange(int at, int count) { for (int i = 0; i < count; i++) RemoveAt(at); }
        public void InsertRange(int at, IEnumerable<Extent> items) { foreach (var item in items) Insert(at++, item); }
        public static implicit operator ExtentList(List<Extent> extents) => new() { memory = extents };
    }
    private static byte[] EncodeExtent(Extent e)
    {
        byte[] data = new byte[45 + (e.ContentHash is null ? 0 : 32) + (e.OriginBlock is null ? 0 : 36)]; data[0] = 3; BinaryPrimitives.WriteInt64LittleEndian(data.AsSpan(1), e.Start);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(9), e.Length); BinaryPrimitives.WriteInt64LittleEndian(data.AsSpan(13), e.JournalOffset);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(21), e.Payload.StoredBytes); BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(25), e.Payload.DecodedBytes);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(29), e.BlockOffset); BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(33),
            (e.Payload.Encoded ? 1 : 0) | (e.ContentHash is not null && !e.RawContent ? 2 : 0) | (e.ContentHash is null ? 0 : 4) | (e.OriginBlock is null ? 0 : 8));
        BinaryPrimitives.WriteInt64LittleEndian(data.AsSpan(37), e.Segment);
        int at = 45;
        if (e.ContentHash is not null) { Convert.FromHexString(e.ContentHash).CopyTo(data, at); at += 32; }
        if (e.OriginBlock is not null) { Convert.FromHexString(e.OriginBlock).CopyTo(data, at); BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(at + 32), e.OriginOffset); }
        return data;
    }
    private static Extent DecodeExtent(byte[] data)
    {
        if (data.Length < 45 || data[0] != 3) throw PagedInvalid();
        int stored = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(21)), decoded = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(25));
        int flags = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(33));
        if (flags is < 0 or > 15 || (flags & 2) != 0 && (flags & 4) == 0 ||
            data.Length != 45 + ((flags & 4) == 0 ? 0 : 32) + ((flags & 8) == 0 ? 0 : 36)) throw PagedInvalid();
        int origin = 45 + ((flags & 4) == 0 ? 0 : 32);
        if ((flags & 8) != 0 && BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(origin + 32)) < 0) throw PagedInvalid();
        return new(BinaryPrimitives.ReadInt64LittleEndian(data.AsSpan(1)), BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(9)),
            BinaryPrimitives.ReadInt64LittleEndian(data.AsSpan(13)), new(stored, decoded), BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(29)),
            BinaryPrimitives.ReadInt64LittleEndian(data.AsSpan(37)), (flags & 4) == 0 ? null : Convert.ToHexStringLower(data.AsSpan(45, 32)), (flags & 2) == 0,
            (flags & 8) == 0 ? null : Convert.ToHexStringLower(data.AsSpan(origin, 32)), (flags & 8) == 0 ? 0 : BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(origin + 32)));
    }
}
