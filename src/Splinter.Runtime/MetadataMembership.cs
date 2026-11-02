using Splinter.Core;

namespace Splinter.Runtime;

// A transient exact set. Fully drain and validate before any reclamation; a
// bounded shared-memory budget falls back to the existing sorted disk tree.
public sealed class MetadataMembership : IDisposable
{
    private readonly Dictionary<string, byte[]> memory = new(StringComparer.Ordinal);
    private readonly ScratchBudget.Lease allocation;
    private TemporaryMetadataDirectory? scratch;
    private PagedMetadataStore? pages;
    private IReadOnlyMetadataTree? tree;
    private bool disposed;
    internal bool DiskBacked => tree is not null;
    public MetadataMembership(IEnumerable<MetadataEntry> entries, string workingDirectory, CancellationToken ct = default)
        : this(entries, workingDirectory, StorageResources.SortRunBytes, StorageResources.Scratch, ct) { }
    internal MetadataMembership(IEnumerable<MetadataEntry> entries, string workingDirectory, long limit, ScratchBudget budget, CancellationToken ct)
    {
        allocation = budget.Open();
        try
        {
            using var input = entries.GetEnumerator(); long bytes = 0;
            while (input.MoveNext())
            {
                ct.ThrowIfCancellationRequested(); var entry = input.Current; Validate(entry);
                if (memory.TryGetValue(entry.Key, out var previous)) { Same(previous, entry.Value.Span); continue; }
                long next = checked(bytes + 512 + entry.Key.Length * 2L + entry.Value.Length);
                if (next <= limit && allocation.TryResize(next)) { memory.Add(entry.Key, entry.Value.ToArray()); bytes = next; continue; }
                scratch = new TemporaryMetadataDirectory(workingDirectory); pages = new PagedMetadataStore(scratch.Path, true);
                IEnumerable<MetadataEntry> Remaining()
                {
                    foreach (var item in memory) yield return new(item.Key, item.Value);
                    yield return entry;
                    while (input.MoveNext()) { ct.ThrowIfCancellationRequested(); Validate(input.Current); yield return input.Current; }
                }
                IEnumerable<MetadataEntry> Unique()
                {
                    MetadataEntry? prior = null;
                    foreach (var item in MetadataSort.Order(Remaining(), scratch.Path, ct))
                    {
                        if (prior?.Key == item.Key) { Same(prior.Value.Span, item.Value.Span); continue; }
                        prior = item; yield return item;
                    }
                }
                tree = pages.Open(pages.Build(Unique(), ct)); memory.Clear(); memory.TrimExcess(); _ = allocation.TryResize(0); break;
            }
        }
        catch { Dispose(); throw; }
    }
    public bool Contains(string key) { ObjectDisposedException.ThrowIf(disposed, this); return tree is null ? memory.ContainsKey(key) : tree.Find(key) is not null; }
    // No ordering contract: membership and aggregation do not require a sort.
    public IEnumerable<MetadataEntry> Enumerate(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (tree is not null)
        {
            foreach (var entry in tree.Enumerate()) { ct.ThrowIfCancellationRequested(); yield return entry; }
        }
        else foreach (var entry in memory)
        { ct.ThrowIfCancellationRequested(); yield return new(entry.Key, entry.Value); }
    }
    private static void Validate(MetadataEntry entry)
    { PagedMetadataStore.ValidateEntry(entry.Key, entry.Value.Length); if (entry.Child is not null) throw new WorkspaceException("MetadataMembershipInvalid", "Transient membership cannot own another tree."); }
    private static void Same(ReadOnlySpan<byte> before, ReadOnlySpan<byte> after)
    { if (!before.SequenceEqual(after)) throw new WorkspaceException("MetadataMembershipInvalid", "One required identity has contradictory values."); }
    public void Dispose() { if (disposed) return; disposed = true; try { pages?.Dispose(); scratch?.Dispose(); } finally { memory.Clear(); memory.TrimExcess(); allocation.Dispose(); } }
}
