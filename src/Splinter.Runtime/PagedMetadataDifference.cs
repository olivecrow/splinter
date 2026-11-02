using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class PagedMetadataStore
{
    private readonly Dictionary<MetadataPageReference, long> comparedSubtrees = [];
    private void VerifyComparedSubtree(MetadataPageReference page, CancellationToken ct)
    {
        page = ResolveReference(page);
        if (comparedSubtrees.TryGetValue(page, out long proof) && proof == readProofEpoch) return;
        long start = readProofEpoch;
        using var pending = new TraversalSpine(); pending.Push(page);
        while (pending.TryPop(out var root))
            foreach (var entry in Open(new(1, StoreId, root)).Enumerate())
            { ct.ThrowIfCancellationRequested(); if (entry.Child?.Page is { } child) pending.Push(child); }
        if (start == readProofEpoch)
        { if (comparedSubtrees.Count == 4096) comparedSubtrees.Clear(); comparedSubtrees[page] = start; }
    }
    // Exact persistent-tree difference, including child physical references.
    // Equal hashes at different native addresses are not a skipped subtree.
    // Callers use this for union coverage, never to authorize reclamation.
    public IEnumerable<MetadataEntry> EntriesNotIdentical(MetadataTreeRoot source, MetadataTreeRoot target, CancellationToken ct = default)
    {
        CheckRoot(source); CheckRoot(target);
        using var pending = new TraversalSpine();
        if (source.Page is { } first) pending.Push(first);
        while (pending.TryPop(out var page))
        {
            ct.ThrowIfCancellationRequested(); var node = ReadNode(page);
            var match = target.Page; Node? found = null;
            while (match is not null)
            {
                ct.ThrowIfCancellationRequested(); var candidate = ReadNode(match);
                int order = StringComparer.Ordinal.Compare(node.Key, candidate.Key);
                if (order == 0) { found = candidate; break; }
                match = order < 0 ? candidate.Left : candidate.Right;
            }
            if (match is not null && ResolveReference(page) == ResolveReference(match))
            { VerifyComparedSubtree(page, ct); continue; }
            if (found is null || !node.Value.AsSpan().SequenceEqual(found.Value) || node.Data != found.Data)
                yield return new(node.Key, node.Value, node.Data is null ? null : new(1, StoreId, node.Data));
            else if (node.Data is { } child) VerifyComparedSubtree(child, ct);
            if (node.Right is { } right) pending.Push(right);
            if (node.Left is { } left) pending.Push(left);
        }
    }
}
