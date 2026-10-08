using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class JournalDeltaStore
{
    // Full append groups become immutable data objects; the bounded unfinished
    // tail stays in the replaceable node footer. Never place a mutable page in
    // the payload region: replacing it would carry prior payload forward.
    private const int LatestPageExtents = 32;
    private sealed record LatestPageReference(long Id, string Hash);
    private sealed record LatestExtentPage(Guid NodeId, Extent[] Extents, LatestBlock[] Blocks);
    private sealed record LatestPagedNode(DeltaEntry Entry, LatestPageReference[] Pages, Extent[] Extents,
        LatestBlock[] Blocks, long ChangeSequence, bool AppendPagingEnabled);
    private sealed record LatestPageState(LatestPageReference Reference, Extent[] Extents);
    private sealed class LatestNodePages
    {
        internal readonly List<LatestPageState> Pages = [];
        internal bool AppendPagingEnabled = true;
    }
    private readonly Dictionary<Guid, LatestNodePages> latestNodePages = [];
    private readonly Dictionary<long, string> latestPageHashes = [];

    private static LatestBlock[] LatestBlocks(IEnumerable<Extent> extents, IReadOnlyDictionary<long, string> hashes) =>
        extents.Select(e => e.JournalOffset / LatestBlockStride).Distinct().Order().Select(id => new LatestBlock(id, hashes[id])).ToArray();
    private static LatestExtentPage MakeLatestPage(Guid node, Extent[] extents, IReadOnlyDictionary<long, string> hashes) =>
        new(node, extents, LatestBlocks(extents, hashes));

    private LatestNode ReadLatestNode(string name)
    {
        var header = LatestJournalStorage.Decode<LatestPagedNode>(latest!.Read(name), "SJN2", int.MaxValue);
        if (header.Entry is null || header.Pages is null || header.Extents is null || header.Blocks is null ||
            header.Pages.Length > MaxIndexedExtents / LatestPageExtents ||
            (long)header.Pages.Length * LatestPageExtents + header.Extents.Length > MaxIndexedExtents)
            throw LatestJournalStorage.Corrupt("A current paged node has invalid bounds.");
        var state = new LatestNodePages { AppendPagingEnabled = header.AppendPagingEnabled };
        var extents = new List<Extent>(header.Pages.Length * LatestPageExtents + header.Extents.Length);
        var blocks = new Dictionary<long, LatestBlock>();
        void AddBlocks(LatestBlock[] entries, Extent[] ranges)
        {
            if (entries.Any(b => b is null) || ranges.Any(e => e is null) ||
                entries.Select(b => b.Id).Distinct().Count() != entries.Length ||
                !entries.Select(b => b.Id).Order().SequenceEqual(ranges.Select(e => e.JournalOffset / LatestBlockStride).Distinct().Order()))
                throw LatestJournalStorage.Corrupt("A metadata page's block references differ from its ranges.");
            foreach (var block in entries)
            {
                if (blocks.TryGetValue(block.Id, out var prior) && prior != block)
                    throw LatestJournalStorage.Corrupt("Metadata pages disagree about a payload hash.");
                blocks[block.Id] = block;
            }
        }
        foreach (var reference in header.Pages)
        {
            if (reference is null || reference.Id < 1 || reference.Id >= latest.Current.NextBlock ||
                latestBlockHashes.ContainsKey(reference.Id) || !latestPageHashes.TryAdd(reference.Id, reference.Hash))
                throw LatestJournalStorage.Corrupt("A metadata page has a duplicate or invalid owner.");
            string pageName = LatestJournalStorage.BlockName(reference.Id);
            long size = latest.FileLength(pageName);
            if (size <= LatestJournalStorage.BlockHeaderBytes || size > 1048576 + LatestJournalStorage.BlockHeaderBytes)
                throw LatestJournalStorage.Corrupt("A metadata page exceeds its bound.");
            byte[] bytes = latest.Read(pageName);
            if (LatestJournalStorage.Hash(bytes) != reference.Hash)
                throw LatestJournalStorage.Corrupt("A metadata page is damaged.");
            latest.ValidateBlock(reference.Id, bytes.Length - LatestJournalStorage.BlockHeaderBytes);
            var page = LatestJournalStorage.Decode<LatestExtentPage>(bytes[LatestJournalStorage.BlockHeaderBytes..], "SJP1", 1048576);
            if (page.NodeId != header.Entry.Id || page.Extents is null || page.Extents.Length != LatestPageExtents || page.Blocks is null)
                throw LatestJournalStorage.Corrupt("A metadata page has another node or invalid range count.");
            AddBlocks(page.Blocks, page.Extents);
            extents.AddRange(page.Extents); state.Pages.Add(new(reference, page.Extents));
            replayBytes += bytes.Length;
        }
        AddBlocks(header.Blocks, header.Extents);
        extents.AddRange(header.Extents);
        if (!latestNodePages.TryAdd(header.Entry.Id, state))
            throw LatestJournalStorage.Corrupt("A paged node appears twice.");
        return new(header.Entry, [.. extents], [.. blocks.Values.OrderBy(b => b.Id)], header.ChangeSequence);
    }

    private byte[] WriteLatestNode(Node node, bool append, ref long next,
        Dictionary<string, LatestJournalStorage.Put> puts, List<LatestJournalStorage.Remove> removes)
    {
        if (!latestNodePages.TryGetValue(node.Entry.Id, out var state))
            latestNodePages.Add(node.Entry.Id, state = new());
        int prefix = 0;
        while (prefix < state.Pages.Count && PageMatches(state.Pages[prefix].Extents, node.Extents, prefix * LatestPageExtents)) prefix++;
        if (prefix != state.Pages.Count)
        {
            // A page's capsule may also own its final append payload. Retire
            // each original page once, but never create replacement live pages
            // for this node: repeated repaging could accumulate copied payload.
            state.AppendPagingEnabled = false;
            foreach (var page in state.Pages.Skip(prefix)) RetireLatestPage(page.Reference, removes);
            state.Pages.RemoveRange(prefix, state.Pages.Count - prefix);
        }
        int paged = prefix * LatestPageExtents;
        if (append && state.AppendPagingEnabled && node.Extents.Count - paged >= LatestPageExtents)
        {
            var extents = node.Extents.GetRange(paged, LatestPageExtents).ToArray();
            var page = MakeLatestPage(node.Entry.Id, extents, latestBlockHashes);
            long id = next++;
            if (next >= long.MaxValue / LatestBlockStride)
                throw new WorkspaceException("DeltaFull", "The bounded page address space is exhausted.");
            string name = LatestJournalStorage.BlockName(id);
            if (latest!.FileHash(name) is not null) throw LatestJournalStorage.Corrupt("A new metadata page already exists.");
            byte[] bytes = LatestJournalStorage.EncodeBlock(id, latest.Current.IdentityHash, LatestJournalStorage.Encode(page, "SJP1"));
            var reference = new LatestPageReference(id, LatestJournalStorage.Hash(bytes));
            puts.Add(name, new(name, null, bytes)); latestPageHashes.Add(id, reference.Hash);
            state.Pages.Add(new(reference, extents)); paged += LatestPageExtents;
        }
        var tail = node.Extents.Skip(paged).ToArray();
        return LatestJournalStorage.Encode(new LatestPagedNode(node.Entry, state.Pages.Select(p => p.Reference).ToArray(),
            tail, LatestBlocks(tail, latestBlockHashes), node.Entry.Deleted ? 0 : pathChanges.GetValueOrDefault(node.Entry.Path),
            state.AppendPagingEnabled), "SJN2");
    }
    private static bool PageMatches(Extent[] page, List<Extent> extents, int start)
    {
        if (start > extents.Count - page.Length) return false;
        for (int i = 0; i < page.Length; i++) if (page[i] != extents[start + i]) return false;
        return true;
    }
    private void RetireLatestPage(LatestPageReference page, List<LatestJournalStorage.Remove> removes)
    {
        removes.Add(new(LatestJournalStorage.BlockName(page.Id), page.Hash));
        latestPageHashes.Remove(page.Id);
    }
    private void RetireLatestPages(Guid node, List<LatestJournalStorage.Remove> removes)
    {
        if (!latestNodePages.Remove(node, out var state)) return;
        foreach (var page in state.Pages) RetireLatestPage(page.Reference, removes);
    }
}
