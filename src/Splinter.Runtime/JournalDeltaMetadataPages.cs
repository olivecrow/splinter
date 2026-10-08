using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class JournalDeltaStore
{
    // Reuse unchanged logical pages even when an earlier range is inserted or
    // replaced. Page objects follow private payload in a capsule so retiring
    // metadata never requires copying an unrelated private payload prefix.
    private const int LatestPageExtents = 32;
    private sealed record LatestPageReference(long Id, string Hash);
    private sealed record LatestExtentPage(Guid NodeId, Extent[] Extents, LatestBlock[] Blocks);
    private sealed record LatestPagedNode(DeltaEntry Entry, LatestPageReference[] Pages, Extent[] Extents,
        LatestBlock[] Blocks, long ChangeSequence);
    private sealed record LatestPageState(LatestPageReference Reference, Extent[] Extents);
    private sealed class LatestNodePages
    {
        internal readonly List<LatestPageState> Pages = [];
    }
    private readonly Dictionary<Guid, LatestNodePages> latestNodePages = [];
    private readonly Dictionary<long, string> latestPageHashes = [];

    private static LatestBlock[] LatestBlocks(IEnumerable<Extent> extents, IReadOnlyDictionary<long, string> hashes) =>
        extents.Select(e => e.JournalOffset / LatestBlockStride).Distinct().Order().Select(id => new LatestBlock(id, hashes[id])).ToArray();
    private static LatestExtentPage MakeLatestPage(Guid node, Extent[] extents, IReadOnlyDictionary<long, string> hashes) =>
        new(node, extents, LatestBlocks(extents, hashes));

    private LatestNode ReadLatestNode(string name)
    {
        var header = LatestJournalStorage.Decode<LatestPagedNode>(latest!.Read(name), "SJN3", int.MaxValue);
        if (header.Entry is null || header.Pages is null || header.Extents is null || header.Blocks is null ||
            header.Pages.Length > MaxIndexedExtents || header.Extents.Length >= LatestPageExtents)
            throw LatestJournalStorage.Corrupt("A current paged node has invalid bounds.");
        var state = new LatestNodePages();
        var extents = new List<Extent>(Math.Min(MaxIndexedExtents, header.Extents.Length + header.Pages.Length));
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
            string pageName = LatestJournalStorage.PageName(reference.Id);
            long size = latest.FileLength(pageName);
            if (size <= LatestJournalStorage.BlockHeaderBytes || size > 1048576 + LatestJournalStorage.BlockHeaderBytes)
                throw LatestJournalStorage.Corrupt("A metadata page exceeds its bound.");
            byte[] bytes = latest.Read(pageName);
            if (LatestJournalStorage.Hash(bytes) != reference.Hash)
                throw LatestJournalStorage.Corrupt("A metadata page is damaged.");
            latest.ValidatePage(reference.Id, bytes.Length - LatestJournalStorage.BlockHeaderBytes);
            var page = LatestJournalStorage.Decode<LatestExtentPage>(bytes[LatestJournalStorage.BlockHeaderBytes..], "SJP2", 1048576);
            if (page.NodeId != header.Entry.Id || page.Extents is null || page.Extents.Length is < 1 or > LatestPageExtents || page.Blocks is null)
                throw LatestJournalStorage.Corrupt("A metadata page has another node or invalid range count.");
            AddBlocks(page.Blocks, page.Extents);
            extents.AddRange(page.Extents); state.Pages.Add(new(reference, page.Extents));
            if (extents.Count > MaxIndexedExtents) throw LatestJournalStorage.Corrupt("Metadata pages exceed the extent bound.");
            replayBytes += bytes.Length;
        }
        AddBlocks(header.Blocks, header.Extents);
        if (header.Extents.Length > MaxIndexedExtents - extents.Count) throw LatestJournalStorage.Corrupt("Metadata tail exceeds the extent bound.");
        extents.AddRange(header.Extents);
        if (!latestNodePages.TryAdd(header.Entry.Id, state))
            throw LatestJournalStorage.Corrupt("A paged node appears twice.");
        return new(header.Entry, [.. extents], [.. blocks.Values.OrderBy(b => b.Id)], header.ChangeSequence);
    }

    private byte[] WriteLatestNode(Node node, ref long next,
        Dictionary<string, LatestJournalStorage.Put> puts, List<LatestJournalStorage.Remove> removes)
    {
        if (!latestNodePages.TryGetValue(node.Entry.Id, out var state))
            latestNodePages.Add(node.Entry.Id, state = new());
        var prior = state.Pages.ToDictionary(p => p.Extents[0].Start);
        var pages = new List<LatestPageState>();
        var tail = new List<Extent>(LatestPageExtents);
        var retained = new HashSet<long>();
        long pageNext = next;
        void PublishTail()
        {
            var extents = tail.ToArray();
            var page = MakeLatestPage(node.Entry.Id, extents, latestBlockHashes);
            long id = pageNext++;
            if (pageNext >= long.MaxValue / LatestBlockStride)
                throw new WorkspaceException("DeltaFull", "The bounded page address space is exhausted.");
            string name = LatestJournalStorage.PageName(id);
            if (latest!.FileHash(name) is not null) throw LatestJournalStorage.Corrupt("A new metadata page already exists.");
            byte[] bytes = LatestJournalStorage.EncodePage(id, latest.Current.IdentityHash, LatestJournalStorage.Encode(page, "SJP2"));
            var reference = new LatestPageReference(id, LatestJournalStorage.Hash(bytes));
            puts.Add(name, new(name, null, bytes)); latestPageHashes.Add(id, reference.Hash);
            pages.Add(new(reference, extents)); tail.Clear();
        }
        for (int at = 0; at < node.Extents.Count;)
        {
            if (prior.TryGetValue(node.Extents[at].Start, out var page) && PageMatches(page.Extents, node.Extents, at))
            {
                // Fill a preceding short page before preserving this one. In
                // particular, descending insertions must not create one page
                // reference per tiny write ahead of an unchanged full page.
                if (tail.Count != 0 && tail.Count + page.Extents.Length <= LatestPageExtents)
                {
                    tail.AddRange(page.Extents); at += page.Extents.Length;
                    if (tail.Count == LatestPageExtents) PublishTail();
                    continue;
                }
                if (tail.Count != 0) PublishTail();
                pages.Add(page); retained.Add(page.Reference.Id); at += page.Extents.Length;
            }
            else
            {
                tail.Add(node.Extents[at++]);
                if (tail.Count == LatestPageExtents) PublishTail();
            }
        }
        foreach (var page in state.Pages)
            if (!retained.Contains(page.Reference.Id)) RetireLatestPage(page.Reference, removes);
        state.Pages.Clear(); state.Pages.AddRange(pages); next = pageNext;
        return LatestJournalStorage.Encode(new LatestPagedNode(node.Entry, pages.Select(p => p.Reference).ToArray(),
            tail.ToArray(), LatestBlocks(tail, latestBlockHashes), node.Entry.Deleted ? 0 : pathChanges.GetValueOrDefault(node.Entry.Path)), "SJN3");
    }
    private static bool PageMatches(Extent[] page, List<Extent> extents, int start)
    {
        if (start > extents.Count - page.Length) return false;
        for (int i = 0; i < page.Length; i++) if (page[i] != extents[start + i]) return false;
        return true;
    }
    private void RetireLatestPage(LatestPageReference page, List<LatestJournalStorage.Remove> removes)
    {
        removes.Add(new(LatestJournalStorage.PageName(page.Id), page.Hash));
        latestPageHashes.Remove(page.Id);
    }
    private void RetireLatestPages(Guid node, List<LatestJournalStorage.Remove> removes)
    {
        if (!latestNodePages.Remove(node, out var state)) return;
        foreach (var page in state.Pages) RetireLatestPage(page.Reference, removes);
    }
}
