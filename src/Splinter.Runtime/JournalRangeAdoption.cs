using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class JournalDeltaStore
{
    // Byte verification uses the frozen roots and immutable segment aliases.
    // Only the final compare-and-replace holds the live mutation gate. A later
    // write can split an old extent; its surviving slices still match the same
    // block token and position, while the new bytes remain untouched.
    public void AdoptCapturedRanges(string control, string id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (segments is null || sourceFilePins is null || capturedContentReader is null || readOnly)
            throw new WorkspaceException("SourceRangeAdoptionUnsupported", "Compose the owned live journal and retained-content reader.");
        string directory = Path.Combine(control, "captures", id);
        // The constructor below owns full root validation and the capture
        // reader lease before any frozen bytes or live ranges are touched.
        // Preliminary request/identity routing needs only the fixed header.
        var capture = SourceCaptureTree.ReadCapture(directory, verifyPagedRoots: false);
        if (capture.Id != id || !SourceCaptureTree.SameIdentity(capture.Identity, Identity)) throw PagedInvalid();
        if (File.Exists(Path.Combine(directory, "materialized.json"))) return;
        var identity = new SourceCaptureIdentity(capture.Id, capture.Sequence, capture.Digest);
        var adoption = CapturedRangeAdoptionStore.Read(control, identity);
        byte[] adoptionProof = System.Text.Encoding.UTF8.GetBytes(adoption.Digest);
        using (EnterGate())
        {
            var completed = pagedIndex!.Open(adoptionRoot!).Find(id);
            if (completed is not null)
            { if (!completed.AsSpan().SequenceEqual(adoptionProof)) throw PagedInvalid(); return; }
        }
        using var frozen = new SourceCaptureTree(directory, snapshot, sourceFilePins, capturedContentReader);
        using var contentScope = (capturedContentReader as OwnedCapturedContentReader)?.BeginAdoptionScope();
        using var index = new PagedMetadataStore(capture.Paged!.Directory);
        var names = new PathComponents(index) { Root = capture.Paged.Names };
        byte[] original = new byte[65536], preserved = new byte[65536];
        var accepted = new List<CapturedRangeAdoptionEntry>(IWorkspaceWriteBatch.MaximumWrites);
        void PublishAccepted()
        {
            if (accepted.Count == 0) return;
            CommitStorageBatch(() =>
            {
                foreach (var row in accepted)
                    if (nodes.TryGetValue(row.FileId, out var live) && HasAdoptableRange(live, row))
                        Commit(new(sequence, "adopt-range", live.Entry, Adoption: row), []);
            });
            accepted.Clear();
        }
        foreach (var row in CapturedRangeAdoptionStore.Enumerate(adoption))
        {
            ct.ThrowIfCancellationRequested();
            if (capturedContentReader.GetLength(row.ChunkHash, row.RawRange) != row.ContentLength)
                throw new WorkspaceException("SourceRangeAdoptionMismatch", "The preserved chunk length differs from its declared immutable object.");
            var record = index.Open(capture.Paged.Nodes).FindEntry(row.FileId.ToString("N")) ?? throw PagedInvalid();
            var entry = DecodeEntry(record.Value.ToArray(), names, capture.Paged.Names);
            var extents = index.Open(record.Child ?? index.Empty); var source = extents.Floor(row.Offset.ToString("x16")) ?? throw PagedInvalid();
            var extent = DecodeExtent(source.Value.ToArray());
            if (row.Offset < extent.Start || row.Offset > extent.Start + extent.Length - row.Length ||
                row.BlockOffset != ProofOffset(extent) + row.Offset - extent.Start || CapturedBlockToken(extent, capture.Paged, index) != row.BlockIdentity) throw PagedInvalid();
            for (int at = 0; at < row.Length;)
            {
                ct.ThrowIfCancellationRequested();
                int count = Math.Min(original.Length, row.Length - at), read;
                if (concurrentBaseReads) read = frozen.Read(entry.Path, row.Offset + at, original.AsSpan(0, count));
                else lock (baseReadGate) read = frozen.Read(entry.Path, row.Offset + at, original.AsSpan(0, count));
                if (read != count) throw PagedInvalid(); int done = 0;
                while (done < count)
                { int n = capturedContentReader.Read(row.ChunkHash, row.RawRange, row.ChunkOffset + at + done, preserved.AsSpan(done, count - done)); if (n <= 0 || n > count - done) throw PagedInvalid(); done += n; }
                if (!original.AsSpan(0, count).SequenceEqual(preserved.AsSpan(0, count))) throw new WorkspaceException("SourceRangeAdoptionMismatch", "The preserved chunk differs from its fixed source range.");
                at += count;
            }
            accepted.Add(row);
            if (accepted.Count == IWorkspaceWriteBatch.MaximumWrites) PublishAccepted();
        }
        PublishAccepted();
        ct.ThrowIfCancellationRequested();
        using (EnterGate(mutation: true))
        {
            Check(); FlushPendingWrites(); adoptionRoot = pagedIndex!.Set(adoptionRoot!, id, adoptionProof); RollSegment();
        }
        capturedContentReader.ReleaseIdleReaders();
    }
    private static string CapturedBlockToken(Extent extent, PagedJournalCapture capture, PagedMetadataStore index)
    {
        if (extent.OriginBlock is { } origin) return origin;
        if (extent.ContentHash is { } hash) return SourceCaptureTree.ContentBlockIdentity(hash, extent.RawContent, extent.Payload.DecodedBytes);
        if (capture.Segments is null || extent.Segment == -1) throw PagedInvalid();
        var data = index.Open(capture.Segments).Find(extent.Segment.ToString("x16")) ?? throw PagedInvalid();
        var segment = DecodeSegment(data); if (segment.NativeIdentity is null) throw PagedInvalid();
        return SourceCaptureTree.BlockIdentity(segment.NativeIdentity, extent.JournalOffset, extent.Payload);
    }
    private string LiveBlockToken(Extent extent)
    {
        if (extent.OriginBlock is { } origin) return origin;
        if (extent.ContentHash is { } hash) return SourceCaptureTree.ContentBlockIdentity(hash, extent.RawContent, extent.Payload.DecodedBytes);
        if (extent.Segment == -1 || !segmentShadow && extent.Segment == segments!.ActiveId) return "";
        var segment = Segment(extent.Segment); if (segment.NativeIdentity is null) return "";
        return SourceCaptureTree.BlockIdentity(segment.NativeIdentity, extent.JournalOffset, extent.Payload);
    }
    private bool HasAdoptableRange(Node node, CapturedRangeAdoptionEntry row)
    {
        foreach (var extent in node.Extents.From(row.Offset))
        {
            if (extent.Start >= row.Offset + row.Length) break;
            long from = Math.Max(extent.Start, row.Offset), to = Math.Min(extent.Start + extent.Length, row.Offset + row.Length);
            if (to > from && LiveBlockToken(extent) == row.BlockIdentity &&
                ProofOffset(extent) + from - extent.Start == row.BlockOffset + from - row.Offset) return true;
        }
        return false;
    }
    private void ApplyAdoptedRange(Node node, CapturedRangeAdoptionEntry row)
    {
        long prior = node.Extents.LongCount;
        foreach (var extent in node.Extents.From(row.Offset))
        {
            if (extent.Start >= row.Offset + row.Length) break;
            long from = Math.Max(extent.Start, row.Offset), to = Math.Min(extent.Start + extent.Length, row.Offset + row.Length);
            if (to <= from || LiveBlockToken(extent) != row.BlockIdentity ||
                ProofOffset(extent) + from - extent.Start != row.BlockOffset + from - row.Offset) continue;
            int skip = checked((int)(from - row.Offset)), length = checked((int)(to - from));
            node.Extents.ReplacePaged(new(from, length, 0, new(row.ContentLength, row.ContentLength),
                checked(row.ChunkOffset + skip), ContentHash: row.ChunkHash, RawContent: row.RawRange, OriginBlock: row.BlockIdentity,
                OriginOffset: checked(row.BlockOffset + skip)));
        }
        extentCount = checked(extentCount - prior + node.Extents.LongCount);
    }
    private static int ProofOffset(Extent extent) => extent.OriginBlock is null ? extent.BlockOffset : extent.OriginOffset;
    public IEnumerable<(string Hash, bool Raw)> EnumerateRequiredCapturedContent()
    {
        using (EnterGate())
        {
            Check(); if (pagedIndex is null) return Array.Empty<(string, bool)>(); var fixedNodes = NodeRoot!;
            IEnumerable<(string, bool)> Cursor()
            {
                foreach (var record in pagedIndex.Open(fixedNodes).Enumerate())
                    if (record.Child is { } ranges)
                        foreach (var item in pagedIndex.Open(ranges).Enumerate())
                        { var extent = DecodeExtent(item.Value.ToArray()); if (extent.ContentHash is { } hash) yield return (hash, extent.RawContent); }
            }
            return ProtectIndexCursor(Cursor());
        }
    }
    public static IEnumerable<(string Hash, bool Raw, long Length)> EnumerateRequiredCapturedContent(SourceChangeView view)
    {
        view.ThrowIfDisposed();
        var index = view.Index ?? throw new WorkspaceException("SourceObservationInvalid", "Require a bound current source content root.");
        if (index.ContentNodes is not { } nodes || index.ContentNodes.StoreId != index.Changes.StoreId ||
            !IndexName(Path.GetFileName(index.Directory)) || view.ChangesReader is null && view.ReaderLease is null) throw PagedInvalid();
        WorkspaceStore.EnsurePlainPath(index.Directory);
        using var store = new PagedMetadataStore(index.Directory);
        foreach (var content in RequiredNodeContent(store, nodes)) yield return content;
        if (index.ContentCaptures is not { } captures) throw PagedInvalid();
        foreach (var record in store.Open(captures).Enumerate())
        {
            var capture = LoadFrozenCapture(store, record);
            string delta = Path.GetDirectoryName(index.Directory)!, path = Path.Combine(Path.GetDirectoryName(delta)!, "captures", capture.Id);
            if (File.Exists(Path.Combine(path, "materialized.json"))) continue;
            if (capture.Paged is null || capture.Identity.Id != view.WorkspaceId || capture.Identity.Snapshot.Id != view.SnapshotId ||
                !WorkspaceStore.SamePath(Path.GetDirectoryName(capture.Paged.Directory)!, delta)) throw PagedInvalid();
            ValidateIndexOwner(capture.Paged.Directory, capture.Identity);
            using var reader = new PagedMetadataStore(capture.Paged.Directory);
            foreach (var content in RequiredNodeContent(reader, capture.Paged.Nodes)) yield return content;
        }
        GC.KeepAlive(view); // Retain the bound metadata reader for this root.
    }
    private static IEnumerable<(string Hash, bool Raw, long Length)> RequiredNodeContent(PagedMetadataStore store, MetadataTreeRoot nodes)
    {
        foreach (var record in store.Open(nodes).Enumerate())
            if (record.Child is { } ranges)
                foreach (var item in store.Open(ranges).Enumerate())
                {
                    var extent = DecodeExtent(item.Value.ToArray());
                    if (extent.ContentHash is { } hash) yield return (hash, extent.RawContent, extent.Payload.DecodedBytes);
                }
    }
}
