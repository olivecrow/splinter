using System.Buffers.Binary;
using System.Collections;
using System.Security.Cryptography;
using System.Text;
using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class JournalDeltaStore
{
    private PathComponents? pathComponents;
    private sealed class PathComponents(PagedMetadataStore store)
    {
        private MetadataTreeRoot root = store.Empty;
        private readonly Dictionary<string, byte[]> pending = new(StringComparer.Ordinal);
        private bool batching;
        private (MetadataTreeRoot Root, long Epoch, string Path, byte[] Id)? lastInterned;
        public MetadataTreeRoot Root
        {
            get { if (pending.Count != 0) { Flush(); store.Flush(); } return root; }
            set { Flush(); root = value; }
        }
        public void BeginBatch() { if (batching) throw new InvalidOperationException("Nested name batches are unsupported."); batching = true; }
        public void EndBatch() { pending.Clear(); batching = false; }
        public void Flush()
        { if (pending.Count == 0) return; root = store.SetBatch(root, pending.Select(pair => new MetadataEntry(pair.Key, pair.Value)).ToArray()); pending.Clear(); }
        internal static readonly byte[] Empty = new byte[32];
        private static byte[] Id(ReadOnlySpan<byte> parent, ReadOnlySpan<byte> name)
        {
            using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            digest.AppendData(parent); digest.AppendData(name); return digest.GetHashAndReset();
        }
        public byte[] Intern(string path)
        {
            byte[] parent = Empty;
            if (path.Length == 0) return parent;
            var epoch = store.DecodedNodeCacheEpoch;
            if (pending.Count == 0 && lastInterned is { } last && last.Epoch == epoch &&
                ReferenceEquals(last.Root, root) && last.Path == path) return last.Id;
            int at = 0;
            while (at < path.Length)
            {
                int slash = path.IndexOf('/', at), stop = slash < 0 ? path.Length : slash;
                byte[] name = Encoding.UTF8.GetBytes(path.AsSpan(at, stop - at).ToString()), id = Id(parent, name);
                byte[] data = new byte[33 + name.Length]; data[0] = 1; parent.CopyTo(data, 1); name.CopyTo(data, 33);
                string key = Convert.ToHexStringLower(id); var existing = pending.TryGetValue(key, out var changed) ? changed : store.Open(root).Find(key);
                if (existing is null)
                {
                    if (!batching) root = store.Set(root, key, data);
                    else { if (pending.Count == 64) Flush(); pending[key] = data; }
                }
                else if (!existing.AsSpan().SequenceEqual(data)) throw PagedInvalid();
                parent = id; at = stop + 1;
            }
            // Batch rollback can discard unflushed components. Only remember
            // the spelling proved in this exact, physically fenced name root.
            if (pending.Count == 0 && epoch is { } proof && store.DecodedNodeCacheEpoch == proof)
                lastInterned = (root, proof, path, parent);
            return parent;
        }
        public string Resolve(ReadOnlySpan<byte> id, MetadataTreeRoot? fixedRoot = null)
        {
            if (id.Length != 32) throw PagedInvalid();
            var names = new Stack<string>(); var visited = new HashSet<string>(StringComparer.Ordinal); byte[] current = id.ToArray();
            var tree = store.Open(fixedRoot ?? root);
            while (!current.AsSpan().SequenceEqual(Empty))
            {
                string key = Convert.ToHexStringLower(current);
                if (!visited.Add(key)) throw PagedInvalid();
                byte[] data = fixedRoot is null && pending.TryGetValue(key, out var changed) ? changed : tree.Find(key) ?? throw PagedInvalid();
                if (data.Length <= 33 || data[0] != 1 || !Id(data.AsSpan(1, 32), data.AsSpan(33)).AsSpan().SequenceEqual(current)) throw PagedInvalid();
                string name = Encoding.UTF8.GetString(data.AsSpan(33));
                if (name.Contains('/') || name.Contains('\0') || name.Length == 0) throw PagedInvalid();
                names.Push(name); current = data.AsSpan(1, 32).ToArray();
            }
            return string.Join('/', names);
        }
        public static string Key(string path, WorkspacePathPolicy policy)
        {
            byte[] parent = Empty; int at = 0;
            while (at < path.Length)
            {
                int slash = path.IndexOf('/', at), stop = slash < 0 ? path.Length : slash;
                parent = Id(parent, Encoding.UTF8.GetBytes(policy.IdentityPath(path[at..stop]))); at = stop + 1;
            }
            return "p" + Convert.ToHexStringLower(parent);
        }
    }

    // Namespace records carry only one name, its file identity and a child
    // directory root. Implicit parents let replay index a changed descendant
    // without materializing unchanged ancestor metadata from the basis.
    private sealed class PagedPathNamespace(PagedMetadataStore store, WorkspacePathPolicy policy, Func<Guid, bool> directory) : IDictionary<string, Guid>
    {
        private MetadataTreeRoot root = store.Empty;
        private readonly Dictionary<string, (string Path, Guid Id)> pending = new(policy.Comparer);
        private bool batching;
        private (MetadataTreeRoot Root, long Epoch, string Path, Guid Id)? lastLookup;
        public MetadataTreeRoot Root
        {
            get { if (pending.Count != 0) { Flush(); store.Flush(); } return root; }
            set { Flush(); root = value; }
        }
        public void BeginBatch() { if (batching) throw new InvalidOperationException("Nested namespace batches are unsupported."); batching = true; }
        public void EndBatch() { pending.Clear(); batching = false; }
        public void Flush()
        {
            if (pending.Count == 0) return;
            var top = Decode(store.Open(root).FindEntry("!") ?? throw PagedInvalid());
            var (children, delta) = UpdateChildren(top.Children ?? store.Empty, pending.Values.ToArray(), 0);
            top = top with { Children = children, Count = checked(top.Count + delta) };
            root = store.Set(root, "!", Encode(top), children); pending.Clear();
        }
        private (MetadataTreeRoot Root, long Delta) UpdateChildren(MetadataTreeRoot children, (string Path, Guid Id)[] changes, int at)
        {
            var groups = changes.GroupBy(change =>
            { int slash = change.Path.IndexOf('/', at); return Key(change.Path[at..(slash < 0 ? change.Path.Length : slash)]); }, StringComparer.Ordinal);
            var entries = new List<MetadataEntry>(); long delta = 0;
            foreach (var group in groups)
            {
                var items = group.ToArray(); var first = items[0]; int slash = first.Path.IndexOf('/', at);
                string name = first.Path[at..(slash < 0 ? first.Path.Length : slash)];
                var oldEntry = store.Open(children).FindEntry(group.Key); var old = oldEntry is null ? null : Decode(oldEntry);
                var direct = items.Where(item => item.Path.IndexOf('/', at) < 0).ToArray();
                if (direct.Length > 1) throw PagedInvalid();
                bool replaced = direct.Length == 1;
                var item = old ?? new NamespaceItem(name, Guid.Empty, true, 0, store.Empty);
                long added = 0;
                if (replaced)
                {
                    bool isDirectory = directory(direct[0].Id);
                    if (!isDirectory && item.Count > 1) throw PagedInvalid();
                    added = item.Id == Guid.Empty ? 1 : 0;
                    item = item with { Name = direct[0].Path[at..], Id = direct[0].Id, Directory = isDirectory,
                        Children = isDirectory ? item.Children ?? store.Empty : null };
                }
                var descendants = items.Where(value => value.Path.IndexOf('/', at) >= 0).ToArray();
                if (descendants.Length != 0)
                {
                    if (!item.Directory) throw PagedInvalid();
                    // Case-insensitive spellings can have different UTF-16 lengths.
                    // Each level strips its own first component before recursion.
                    var tails = descendants.Select(value => (Path: value.Path[(value.Path.IndexOf('/', at) + 1)..], value.Id)).ToArray();
                    var next = UpdateChildren(item.Children ?? store.Empty, tails, 0);
                    item = item with { Children = next.Root }; added = checked(added + next.Delta);
                }
                item = item with { Count = checked(item.Count + added) }; delta = checked(delta + added);
                entries.Add(new(group.Key, Encode(item), item.Children));
            }
            return (store.SetBatch(children, entries), delta);
        }
        private sealed record NamespaceItem(string Name, Guid Id, bool Directory, long Count, MetadataTreeRoot? Children);
        public long LongCount => Root.Page is null ? 0 : Decode(store.Open(Root).FindEntry("!")!).Count;
        public int Count => (int)Math.Min(int.MaxValue, LongCount); public bool IsReadOnly => false;
        public ICollection<string> Keys => new CursorCollection<string>(() => this.Select(p => p.Key), () => Count);
        public ICollection<Guid> Values => new CursorCollection<Guid>(() => this.Select(p => p.Value), () => Count);
        private string Key(string name) => policy.IdentityPath(name);
        private static byte[] Encode(NamespaceItem item)
        {
            byte[] name = Encoding.UTF8.GetBytes(item.Name), data = new byte[26 + name.Length]; data[0] = 1;
            data[1] = item.Directory ? (byte)1 : (byte)0; item.Id.TryWriteBytes(data.AsSpan(2, 16));
            BinaryPrimitives.WriteInt64LittleEndian(data.AsSpan(18), item.Count); name.CopyTo(data, 26); return data;
        }
        private static NamespaceItem Decode(MetadataEntry entry)
        {
            var data = entry.Value.Span;
            if (data.Length < 26 || data[0] != 1 || data[1] > 1) throw PagedInvalid();
            string name = Encoding.UTF8.GetString(data[26..]); long count = BinaryPrimitives.ReadInt64LittleEndian(data[18..]);
            if (name.Contains('/') || name.Contains('\0') || count < 0 || data[1] == 0 && entry.Child is not null) throw PagedInvalid();
            return new(name, new Guid(data.Slice(2, 16)), data[1] == 1, count, entry.Child);
        }
        private MetadataEntry? Locate(string path, MetadataTreeRoot root)
        {
            var current = store.Open(root).FindEntry("!");
            if (current is null || path.Length == 0) return current;
            int at = 0;
            while (at < path.Length)
            {
                var parent = Decode(current);
                if (!parent.Directory) return null;
                int slash = path.IndexOf('/', at), stop = slash < 0 ? path.Length : slash;
                current = store.Open(parent.Children ?? store.Empty).FindEntry(Key(path[at..stop]));
                if (current is null) return null; at = stop + 1;
            }
            return current;
        }
        public Guid this[string path]
        {
            get => TryGetValue(path, out Guid id) ? id : throw new KeyNotFoundException();
            set
            {
                if (!batching || path.Length == 0 || path.Count(character => character == '/') >= 128)
                { Flush(); Change(path, value, remove: false); return; }
                if (pending.Count == 64 && !pending.ContainsKey(path)) Flush();
                pending[path] = (path, value);
            }
        }
        private void Change(string path, Guid id, bool remove)
        {
            if (path.Length == 0)
            {
                if (remove) { Root = store.Empty; return; }
                var prior = Root.Page is null ? null : Decode(store.Open(Root).FindEntry("!")!);
                var item = new NamespaceItem("", id, true, prior?.Count ?? 1, prior?.Children ?? store.Empty);
                Root = store.Set(Root, "!", Encode(item), item.Children); return;
            }
            var topEntry = store.Open(Root).FindEntry("!") ?? throw PagedInvalid(); var top = Decode(topEntry);
            var ancestors = new Stack<(string Key, NamespaceItem NamespaceItem)>();
            var children = top.Children ?? store.Empty; int at = 0;
            NamespaceItem? old = null; string finalKey = ""; string finalName = "";
            while (at < path.Length)
            {
                int slash = path.IndexOf('/', at), stop = slash < 0 ? path.Length : slash;
                string name = path[at..stop], key = Key(name); var entry = store.Open(children).FindEntry(key);
                var item = entry is null ? null : Decode(entry);
                if (slash < 0) { old = item; finalKey = key; finalName = name; break; }
                item ??= new(name, Guid.Empty, true, 0, store.Empty);
                if (!item.Directory) throw PagedInvalid();
                ancestors.Push((key, item)); children = item.Children ?? store.Empty; at = stop + 1;
            }
            long delta;
            if (remove)
            { if (old is null) return; delta = -old.Count; children = store.Remove(children, finalKey); }
            else
            {
                bool isDirectory = directory(id); delta = old is null || old.Id == Guid.Empty ? 1 : 0;
                if (!isDirectory && old is { Count: > 1 }) throw PagedInvalid();
                var replacement = new NamespaceItem(finalName, id, isDirectory, checked((old?.Count ?? 0) + delta), isDirectory ? old?.Children ?? store.Empty : null);
                children = store.Set(children, finalKey, Encode(replacement), replacement.Children);
            }
            while (ancestors.TryPop(out var parent))
            {
                var item = parent.NamespaceItem with { Children = children, Count = checked(parent.NamespaceItem.Count + delta) };
                MetadataTreeRoot parentRoot = ancestors.TryPeek(out var grandparent) ? grandparent.NamespaceItem.Children ?? store.Empty : top.Children ?? store.Empty;
                children = store.Set(parentRoot, parent.Key, Encode(item), children);
            }
            top = top with { Children = children, Count = checked(top.Count + delta) };
            Root = store.Set(Root, "!", Encode(top), children);
        }
        public bool TryGetValue(string path, out Guid id)
        {
            if (pending.TryGetValue(path, out var changed)) { id = changed.Id; return id != Guid.Empty; }
            // Security, open and subsequent stat callbacks often ask for the
            // same leaf. Fixed directory cursors still use their own roots.
            var epoch = store.DecodedNodeCacheEpoch;
            if (lastLookup is { } last && last.Epoch == epoch && ReferenceEquals(last.Root, root) && last.Path == path)
            { id = last.Id; return true; }
            if (!TryGetValueAt(path, root, out id)) return false;
            if (epoch is { } proof && store.DecodedNodeCacheEpoch == proof) lastLookup = (root, proof, path, id);
            return true;
        }
        public bool TryGetValueAt(string path, MetadataTreeRoot root, out Guid id)
        { var entry = Locate(path, root); id = entry is null ? Guid.Empty : Decode(entry).Id; return id != Guid.Empty; }
        public bool ContainsKey(string path) => TryGetValue(path, out _);
        public void Add(string path, Guid id) { if (ContainsKey(path)) throw new ArgumentException("Duplicate namespace path."); this[path] = id; }
        public bool Remove(string path) { if (!ContainsKey(path)) return false; Flush(); Change(path, Guid.Empty, true); return true; }
        public void Clear() => Root = store.Empty;
        public IEnumerable<KeyValuePair<string, Guid>> Children(string path, MetadataTreeRoot? fixedRoot = null)
        {
            var entry = Locate(path, fixedRoot ?? Root); if (entry is null) yield break;
            var parent = Decode(entry); if (!parent.Directory) yield break;
            foreach (var child in store.Open(parent.Children ?? store.Empty).Enumerate())
            {
                var item = Decode(child);
                yield return new(Join(path, item.Name), item.Id);
            }
        }
        public IEnumerable<KeyValuePair<string, Guid>> Walk(MetadataTreeRoot? fixedRoot = null)
        {
            var root = fixedRoot ?? Root; var topEntry = store.Open(root).FindEntry("!"); if (topEntry is null) yield break;
            var top = Decode(topEntry); if (top.Id != Guid.Empty) yield return new("", top.Id);
            var pending = new Stack<(string Prefix, IEnumerator<MetadataEntry> Cursor)>();
            pending.Push(("", store.Open(top.Children ?? store.Empty).Enumerate().GetEnumerator()));
            try
            {
                while (pending.TryPeek(out var level))
                {
                    if (!level.Cursor.MoveNext()) { level.Cursor.Dispose(); pending.Pop(); continue; }
                    var item = Decode(level.Cursor.Current); string path = Join(level.Prefix, item.Name);
                    if (item.Id != Guid.Empty) yield return new(path, item.Id);
                    if (item.Directory) pending.Push((path, store.Open(item.Children ?? store.Empty).Enumerate().GetEnumerator()));
                }
            }
            finally { foreach (var level in pending) level.Cursor.Dispose(); }
        }
        public IEnumerator<KeyValuePair<string, Guid>> GetEnumerator() { var root = Root; return Walk(root).GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public void Add(KeyValuePair<string, Guid> item) => Add(item.Key, item.Value);
        public bool Contains(KeyValuePair<string, Guid> item) => TryGetValue(item.Key, out var value) && value == item.Value;
        public void CopyTo(KeyValuePair<string, Guid>[] array, int at) { foreach (var item in this) array[at++] = item; }
        public bool Remove(KeyValuePair<string, Guid> item) => Contains(item) && Remove(item.Key);
    }
}
