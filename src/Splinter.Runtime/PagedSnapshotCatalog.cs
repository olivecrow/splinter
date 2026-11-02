using System.Text;
using Splinter.Core;

namespace Splinter.Runtime;

public sealed record SnapshotCatalogDescriptor(int Version, SnapshotEntry Root, MetadataTreeRoot Children, long Count, bool CaseSensitive);

// Bulk construction uses bounded sorted runs and one spool per open directory.
// The persistent tree contains component names only. No project-sized path or
// directory dictionary is retained by readers or builders.
public sealed class PagedSnapshotCatalog : IDisposable
{
    private readonly PagedMetadataStore store;
    private readonly DirectoryMetadataTree tree;
    private readonly StringComparison comparison;
    private readonly bool ownsStore;
    public SnapshotCatalogDescriptor Descriptor { get; }

    public PagedSnapshotCatalog(string directory, SnapshotCatalogDescriptor descriptor)
        : this(new PagedMetadataStore(directory), descriptor, true) { }

    // The caller owns this reader's lifetime. Catalog and payload-index lookups
    // can share its bounded page cache and native container fences.
    public PagedSnapshotCatalog(PagedMetadataStore store, SnapshotCatalogDescriptor descriptor)
        : this(store, descriptor, false) { }

    private PagedSnapshotCatalog(PagedMetadataStore store, SnapshotCatalogDescriptor descriptor, bool ownsStore)
    {
        this.store = store; this.ownsStore = ownsStore; Descriptor = descriptor; tree = new(store, descriptor.CaseSensitive);
        comparison = descriptor.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        try
        {
            if (descriptor.Version != 1 || descriptor.Count < 1 || descriptor.Root.Path != "" || !descriptor.Root.Directory)
                throw Invalid("The snapshot catalog descriptor is unsupported.");
            _ = store.Open(descriptor.Children);
        }
        catch { if (ownsStore) store.Dispose(); throw; }
    }

    public static SnapshotCatalogDescriptor Build(string directory, SnapshotEntry root, IEnumerable<DirectoryMetadataRecord> records,
        bool caseSensitive = false, CancellationToken ct = default, SnapshotCatalogDescriptor? previousCatalog = null, IJournalFilePins? filePins = null)
    {
        if (root.Path != "" || !root.Directory) throw Invalid("A snapshot catalog needs one root directory.");
        using var store = new PagedMetadataStore(directory, writable: true, filePins: filePins);
        using var previous = previousCatalog is null ? null : new PagedSnapshotCatalog(directory, previousCatalog);
        if (previousCatalog is not null && previousCatalog.CaseSensitive != caseSensitive)
            throw Invalid("Preserve a catalog whose path policy changed.");
        var tree = new DirectoryMetadataTree(store, caseSensitive);
        var policy = caseSensitive ? WorkspacePathPolicy.Posix : WorkspacePathPolicy.Windows;
        var frames = new Stack<DirectoryFrame>(); long count = 1;
        string? previousKey = null;
        try
        {
            frames.Push(new(store.WorkingDirectory, new(root, null)));
            IEnumerable<MetadataEntry> Encoded()
            {
                foreach (var record in records)
                {
                    ct.ThrowIfCancellationRequested(); string path = record.Entry.Path;
                    if (path.Length == 0 || policy.Normalize(path) != path || record.Children is not null)
                        throw Invalid("Catalog input requires unique ordinary paths and explicit directories.");
                    var entry = tree.Encode(record, component: false);
                    yield return entry with { Key = HierarchyKey(caseSensitive ? path : path.ToUpperInvariant()) };
                }
            }
            foreach (var encoded in MetadataExternalSort.Order(Encoded(), store.WorkingDirectory, ct))
            {
                ct.ThrowIfCancellationRequested();
                if (previousKey is not null && StringComparer.Ordinal.Compare(previousKey, encoded.Key) >= 0)
                    throw Invalid("Snapshot catalog paths collide under the recorded path policy.");
                previousKey = encoded.Key;
                var record = tree.Decode(encoded, component: false); string path = record.Entry.Path;
                string parent = Parent(path);
                while (frames.Peek().Entry.Path != parent)
                {
                    if (frames.Count == 1) throw Invalid("A recorded catalog parent is absent or has different casing.");
                    CompleteFrame();
                }
                if (record.Entry.Directory) frames.Push(new(store.WorkingDirectory, record));
                else frames.Peek().Append(tree.Encode(record with { Entry = record.Entry with { Path = Name(path) } }));
                count = checked(count + 1);
            }
            while (frames.Count > 1) CompleteFrame();
            var rootFrame = frames.Pop();
            using (rootFrame)
            {
                var children = store.Build(MetadataExternalSort.Order(rootFrame.Read(), store.WorkingDirectory, ct), ct,
                    previousCatalog?.Children);
                store.Flush(); rootFrame.Complete();
                return new(1, root, children, count, caseSensitive);
            }

            void CompleteFrame()
            {
                var frame = frames.Pop();
                using (frame)
                {
                    var prior = previous?.Find(frame.Entry.Path);
                    var children = store.Build(MetadataExternalSort.Order(frame.Read(), store.WorkingDirectory, ct), ct,
                        prior?.Entry.Directory == true ? prior.Children : null);
                    var record = frame.Record with { Entry = frame.Entry with { Path = Name(frame.Entry.Path) }, Children = children };
                    frames.Peek().Append(tree.Encode(record)); frame.Complete();
                }
            }
        }
        finally { foreach (var frame in frames) frame.Dispose(); }
    }

    public DirectoryMetadataRecord? Find(string path)
    {
        if (path.Length == 0) return new(Descriptor.Root, null, Descriptor.Children);
        var root = Descriptor.Children; int at = 0; string prefix = "";
        while (at < path.Length)
        {
            int slash = path.IndexOf('/', at); int end = slash < 0 ? path.Length : slash;
            var entry = tree.Find(root, path[at..end]);
            if (entry is null) return null;
            if (slash < 0) return entry with { Entry = entry.Entry with { Path = prefix + entry.Entry.Path } };
            // Casing is evidence: traversing a case alias must not manufacture
            // another stored path spelling.
            if (!entry.Entry.Directory || !path.AsSpan(at, end - at).Equals(entry.Entry.Path, comparison)) return null;
            prefix += entry.Entry.Path + "/"; root = entry.Children!; at = slash + 1;
        }
        return null;
    }

    public IEnumerable<DirectoryMetadataRecord> EnumerateChildren(string path)
    {
        var parent = Find(path);
        if (parent is null || !parent.Entry.Directory) throw new DirectoryNotFoundException(path);
        string prefix = parent.Entry.Path.Length == 0 ? "" : parent.Entry.Path + "/";
        var children = parent.Children!;
        return tree.Enumerate(children).Select(item => item with { Entry = item.Entry with { Path = prefix + item.Entry.Path } });
    }
    public IEnumerable<DirectoryMetadataRecord> Walk(string? afterPath = null)
    {
        var pending = new Stack<(string Prefix, IEnumerator<DirectoryMetadataRecord> Items)>();
        try
        {
            if (afterPath is null)
            {
                yield return new(Descriptor.Root, null, Descriptor.Children);
                pending.Push(("", tree.Enumerate(Descriptor.Children).GetEnumerator()));
            }
            else if (afterPath.Length == 0) pending.Push(("", tree.Enumerate(Descriptor.Children).GetEnumerator()));
            else
            {
                string prefix = ""; var children = Descriptor.Children; int at = 0;
                while (at < afterPath.Length)
                {
                    int slash = afterPath.IndexOf('/', at), end = slash < 0 ? afterPath.Length : slash;
                    string name = afterPath[at..end]; var entry = tree.Find(children, name);
                    if (entry is null || entry.Entry.Path != name) throw Invalid("A catalog cursor must name an exact stored path.");
                    pending.Push((prefix, tree.Enumerate(children, name).GetEnumerator()));
                    prefix += name + "/";
                    if (slash < 0)
                    {
                        if (entry.Entry.Directory) pending.Push((prefix, tree.Enumerate(entry.Children!).GetEnumerator()));
                        break;
                    }
                    if (!entry.Entry.Directory) throw Invalid("A catalog cursor traverses a file.");
                    children = entry.Children!; at = slash + 1;
                }
            }
            while (pending.TryPeek(out var level))
            {
                if (!level.Items.MoveNext()) { level.Items.Dispose(); pending.Pop(); continue; }
                var entry = level.Items.Current; string path = level.Prefix + entry.Entry.Path;
                yield return entry with { Entry = entry.Entry with { Path = path } };
                if (entry.Entry.Directory) pending.Push((path + "/", tree.Enumerate(entry.Children!).GetEnumerator()));
            }
        }
        finally { foreach (var level in pending) level.Items.Dispose(); }
    }
    public void Verify(CancellationToken ct = default)
    {
        long count = 0;
        foreach (var item in Walk()) { ct.ThrowIfCancellationRequested(); count = checked(count + 1); _ = item; }
        if (count != Descriptor.Count) throw Invalid("The catalog membership differs from its descriptor.");
    }
    public void Dispose() { if (ownsStore) store.Dispose(); }
    private static string Parent(string path) { int at = path.LastIndexOf('/'); return at < 0 ? "" : path[..at]; }
    private static string Name(string path) { int at = path.LastIndexOf('/'); return at < 0 ? path : path[(at + 1)..]; }
    // Separators sort before every component, so a directory's whole subtree
    // is contiguous even beside names such as "a.txt" and "a/child".
    private static string HierarchyKey(string path)
    {
        var key = new StringBuilder(path.Length);
        foreach (char c in path)
        {
            if (c == '/') key.Append('\u0001');
            else if (c < '\u0004') { key.Append('\u0003'); key.Append((char)('A' + c)); }
            else key.Append(c);
        }
        return key.ToString();
    }
    private static WorkspaceException Invalid(string message) => new("SnapshotCatalogInvalid", message);

    private sealed class DirectoryFrame : IDisposable
    {
        private readonly string directory;
        private MetadataSpool? writer;
        internal DirectoryMetadataRecord Record { get; }
        internal SnapshotEntry Entry => Record.Entry;
        internal DirectoryFrame(string directory, DirectoryMetadataRecord record)
        { Record = record; this.directory = directory; }
        internal void Append(MetadataEntry entry)
        {
            writer ??= new(directory); writer.Append(entry);
        }
        internal IEnumerable<MetadataEntry> Read()
        {
            if (writer is null) yield break;
            foreach (var entry in writer.Read()) yield return entry;
        }
        internal void Complete() { }
        public void Dispose() => writer?.Dispose();
    }
}
