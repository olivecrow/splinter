using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class PagedMetadataStore
{
    // A bounded transient overlay handles both insertions and updates. Rotations
    // only change private references; write each surviving dirty node once after
    // the whole batch succeeds. Old roots and the on-disk treap format are intact.
    internal MetadataTreeRoot SetBatch(MetadataTreeRoot root, IReadOnlyList<MetadataEntry> entries)
    {
        if (entries.Count > 64) throw new ArgumentOutOfRangeException(nameof(entries));
        lock (gate)
        {
            CheckWriter(); CheckRoot(root);
            var ordered = entries.OrderBy(entry => entry.Key, StringComparer.Ordinal).ToArray();
            for (int index = 0; index < ordered.Length; index++)
            {
                var entry = ordered[index]; ValidateEntry(entry.Key, entry.Value.Length);
                if (index > 0 && ordered[index - 1].Key == entry.Key) throw Invalid("A metadata batch must have unique keys.");
                if (entry.Child is { } child) { CheckRoot(child); if (child.Page is not null) _ = ReadNode(child.Page); }
            }
            var edited = Wrap(root.Page);
            try
            {
                foreach (var entry in ordered) edited = Update(edited, entry, 0, out _);
            }
            catch (BatchDepthLimit)
            {
                // No page has been written yet. Preserve bounded memory even
                // for deliberately pathological priorities using the disk spine.
                return Sequential(root);
            }
            return new(1, StoreId, Persist(edited));

            MetadataTreeRoot Sequential(MetadataTreeRoot prior)
            {
                foreach (var entry in ordered) prior = Set(prior, entry.Key, entry.Value.Span, entry.Child);
                return prior;
            }
            Node Load(EditedNode item)
            {
                if (item.Record is not null) return item.Record;
                var node = item.Record = ReadNode(item.Original!);
                item.Left = Wrap(node.Left); item.Right = Wrap(node.Right); return node;
            }
            EditedNode Update(EditedNode? item, MetadataEntry entry, int depth, out bool changed)
            {
                if (depth == 128) throw new BatchDepthLimit();
                if (item is null)
                { changed = true; return new(null) { Record = new(entry.Key, entry.Value.ToArray(), null, null, entry.Child?.Page), Dirty = true }; }
                var node = Load(item); int order = StringComparer.Ordinal.Compare(entry.Key, node.Key);
                if (order == 0)
                {
                    changed = !entry.Value.Span.SequenceEqual(node.Value) || node.Data != entry.Child?.Page;
                    if (changed) { item.Record = node with { Value = entry.Value.ToArray(), Data = entry.Child?.Page }; item.Dirty = true; }
                    return item;
                }
                var child = Update(order < 0 ? item.Left : item.Right, entry, depth + 1, out changed);
                if (!changed) return item;
                item.Dirty = true;
                if (order < 0) item.Left = child; else item.Right = child;
                if (Priority(Load(child).Key, node.Key) >= 0) return item;
                child.Dirty = true;
                if (order < 0) { item.Left = child.Right; child.Right = item; }
                else { item.Right = child.Left; child.Left = item; }
                return child;
            }
            MetadataPageReference? Persist(EditedNode? item)
            {
                if (item is null) return null;
                if (!item.Dirty) return item.Original;
                return WriteNode(Load(item) with { Left = Persist(item.Left), Right = Persist(item.Right) });
            }
        }
    }
    private static EditedNode? Wrap(MetadataPageReference? page) => page is null ? null : new(page);
    private sealed class EditedNode(MetadataPageReference? original)
    {
        internal readonly MetadataPageReference? Original = original;
        internal Node? Record;
        internal EditedNode? Left, Right;
        internal bool Dirty;
    }
    private sealed class BatchDepthLimit : Exception;
}
