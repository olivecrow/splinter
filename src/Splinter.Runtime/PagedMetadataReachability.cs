using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class PagedMetadataStore
{
    // Exact request-local visited set. Exhaustion keeps the original disk
    // frontier intact and selects the existing bounded merge implementation.
    private string? TryMemoryReachability(string frontier, string scope, int limit, CancellationToken ct)
    {
        using var allocation = StorageResources.Scratch.Open();
        var visited = new Dictionary<(long Container, long Offset), MetadataPageReference>();
        var pending = new Stack<MetadataPageReference>();
        foreach (var root in ReadReferences(frontier))
        {
            pending.Push(root);
            while (pending.TryPop(out var candidate))
            {
                ct.ThrowIfCancellationRequested(); var reference = ResolveReference(candidate); CheckReference(reference);
                var key = (reference.Container, reference.Offset);
                if (visited.TryGetValue(key, out var previous))
                { if (previous != reference) throw Invalid("One reachable metadata address has conflicting identities."); continue; }
                long bytes = checked((visited.Count + 1L) * 512);
                if (bytes > limit || !allocation.TryResize(bytes)) return null;
                var node = ReadNode(reference); visited.Add(key, reference);
                if (node.Left is not null) pending.Push(node.Left);
                if (node.Right is not null) pending.Push(node.Right);
                if (node.Data is not null) pending.Push(node.Data);
            }
        }
        string path = Path.Combine(scope, "visited-memory.bin");
        var ordered = visited.Values.ToArray(); Array.Sort(ordered, PhysicalOrder);
        using (var output = NewGraphSpool(path, false, filePins))
            foreach (var reference in ordered) { ct.ThrowIfCancellationRequested(); WriteReference(output, reference); }
        boundary?.Invoke("metadata.repack.reachability-memory"); return path;
    }
}
