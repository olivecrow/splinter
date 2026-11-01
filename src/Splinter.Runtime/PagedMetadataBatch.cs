using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class PagedMetadataStore
{
    // Existing keys keep their deterministic tree shape. Visit the union of
    // their search paths and write each changed ancestor once, instead of
    // rebuilding the root path after every row. Inserts use the disk-spine
    // path; neither a large input nor hostile priorities grow a memory tree.
    internal MetadataTreeRoot SetBatch(MetadataTreeRoot root, IReadOnlyList<MetadataEntry> entries)
    {
        if (entries.Count > 64) throw new ArgumentOutOfRangeException(nameof(entries));
        lock (gate)
        {
            CheckWriter(); CheckRoot(root);
            var ordered = entries.OrderBy(entry => entry.Key, StringComparer.Ordinal).ToArray();
            bool inserts = false;
            for (int index = 0; index < ordered.Length; index++)
            {
                var entry = ordered[index]; ValidateEntry(entry.Key, entry.Value.Length);
                if (index > 0 && ordered[index - 1].Key == entry.Key) throw Invalid("A metadata batch must have unique keys.");
                if (entry.Child is { } child) { CheckRoot(child); if (child.Page is not null) _ = ReadNode(child.Page); }
                inserts |= Open(root).FindEntry(entry.Key) is null;
            }
            if (inserts) return Sequential(root, 0, ordered.Length);
            return new(1, StoreId, Update(root.Page, 0, ordered.Length, 0));

            MetadataTreeRoot Sequential(MetadataTreeRoot prior, int start, int end)
            {
                for (int index = start; index < end; index++)
                { var entry = ordered[index]; prior = Set(prior, entry.Key, entry.Value.Span, entry.Child); }
                return prior;
            }
            MetadataPageReference? Update(MetadataPageReference? page, int start, int end, int depth)
            {
                if (start == end) return page;
                if (depth == 128) return Sequential(new(1, StoreId, page), start, end).Page;
                if (page is null) throw Invalid("An existing metadata batch key disappeared.");
                var node = ReadNode(page); int split = start;
                while (split < end && StringComparer.Ordinal.Compare(ordered[split].Key, node.Key) < 0) split++;
                bool replaced = split < end && ordered[split].Key == node.Key;
                var left = Update(node.Left, start, split, depth + 1);
                var right = Update(node.Right, replaced ? split + 1 : split, end, depth + 1);
                var value = replaced ? ordered[split].Value : node.Value;
                var data = replaced ? ordered[split].Child?.Page : node.Data;
                if (left == node.Left && right == node.Right && data == node.Data && value.Span.SequenceEqual(node.Value)) return page;
                return WriteNode(node with { Left = left, Right = right, Value = value.ToArray(), Data = data });
            }
        }
    }
}
