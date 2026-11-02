using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class PagedMetadataStore
{
    // Catalog validation and logical byte accounting share one traversal. A
    // memoized subtree remains evidence only while every native page fence is
    // still held by this exact reader. Eviction or write-sharing invalidates it.
    public sealed class CatalogReadScope : IDisposable
    {
        private readonly PagedMetadataStore pages;
        private readonly FileStream identity;
        private readonly FileStream? locationHeader;
        private readonly Dictionary<(MetadataPageReference Page, bool Sensitive), Summary> summaries = [];
        private long epoch;
        private bool disposed;
        internal long VisitedNodes { get; private set; }
        internal long ReusedSubtrees { get; private set; }
        public sealed record Summary(long Count, long Bytes, long Rows, string? First, string? Last);
        private static readonly Summary EmptySummary = new(0, 0, 0, null, null);
        private sealed class Frame(MetadataPageReference page)
        {
            internal readonly MetadataPageReference Page = page;
            internal Node? Node;
            internal long Epoch;
            internal int Stage;
            internal Summary Left = EmptySummary, Right = EmptySummary, Child = EmptySummary;
        }
        public CatalogReadScope(string directory, IJournalFilePins? pins = null)
        {
            WorkspaceStore.EnsurePlainPath(directory);
            identity = new(Path.Combine(directory, "identity.bin"), FileMode.Open, FileAccess.Read, FileShare.Read);
            try
            {
                string locations = Path.Combine(directory, "locations.json");
                if (File.Exists(locations)) locationHeader = new(locations, FileMode.Open, FileAccess.Read, FileShare.Read);
                pages = new(directory, filePins: pins);
            }
            catch { locationHeader?.Dispose(); identity.Dispose(); throw; }
        }
        public Summary Verify(SnapshotCatalogDescriptor descriptor, CancellationToken ct = default)
        {
            ObjectDisposedException.ThrowIf(disposed, this); ct.ThrowIfCancellationRequested();
            if (descriptor.Version != 1 || descriptor.Count < 1 || descriptor.Root.Path != "" || !descriptor.Root.Directory)
                throw Invalid("The snapshot catalog descriptor is unsupported.");
            pages.CheckRoot(descriptor.Children);
            var tree = new DirectoryMetadataTree(pages, descriptor.CaseSensitive);
            var stack = new Stack<Frame>(); Summary completed = EmptySummary;
            if (descriptor.Children.Page is { } root) stack.Push(new(root));
            while (stack.TryPeek(out var frame))
            {
                ct.ThrowIfCancellationRequested();
                if (epoch != pages.readProofEpoch) { summaries.Clear(); epoch = pages.readProofEpoch; }
                var key = (frame.Page, descriptor.CaseSensitive);
                if (frame.Stage == 0)
                {
                    if (summaries.TryGetValue(key, out var cached)) { completed = cached; ReusedSubtrees++; stack.Pop(); continue; }
                    frame.Epoch = pages.readProofEpoch; frame.Node = pages.ReadNode(frame.Page); VisitedNodes++; frame.Stage = 1;
                    if (frame.Node.Left is { } left) { stack.Push(new(left)); continue; }
                    completed = EmptySummary;
                }
                if (frame.Stage == 1)
                {
                    frame.Left = completed; frame.Stage = 2;
                    if (frame.Node!.Right is { } right) { stack.Push(new(right)); continue; }
                    completed = EmptySummary;
                }
                if (frame.Stage == 2)
                {
                    frame.Right = completed; frame.Stage = 3;
                    if (frame.Node!.Data is { } child) { stack.Push(new(child)); continue; }
                    completed = EmptySummary;
                }
                frame.Child = completed; var node = frame.Node!;
                if (frame.Left.Last is not null && StringComparer.Ordinal.Compare(frame.Left.Last, node.Key) >= 0 ||
                    frame.Right.First is not null && StringComparer.Ordinal.Compare(node.Key, frame.Right.First) >= 0)
                    throw Invalid("Catalog keys are duplicated or out of order.");
                var record = tree.Decode(new(node.Key, node.Value, node.Data is null ? null : new(1, pages.StoreId, node.Data)));
                long rows = checked(1 + frame.Left.Rows + frame.Right.Rows);
                if (rows != frame.Page.Count) throw Invalid("Catalog tree membership changed.");
                completed = new(checked(1 + frame.Left.Count + frame.Right.Count + frame.Child.Count),
                    checked(record.Entry.Length + frame.Left.Bytes + frame.Right.Bytes + frame.Child.Bytes), rows,
                    frame.Left.First ?? node.Key, frame.Right.Last ?? node.Key);
                if (frame.Epoch == pages.readProofEpoch)
                {
                    if (epoch != pages.readProofEpoch) { summaries.Clear(); epoch = pages.readProofEpoch; }
                    if (summaries.Count == 4096) summaries.Clear();
                    summaries[key] = completed;
                }
                stack.Pop();
            }
            if (checked(completed.Count + 1) != descriptor.Count) throw Invalid("The catalog membership differs from its descriptor.");
            return completed;
        }
        public void Dispose()
        { if (disposed) return; disposed = true; summaries.Clear(); try { pages.Dispose(); } finally { locationHeader?.Dispose(); identity.Dispose(); } }
    }
}
