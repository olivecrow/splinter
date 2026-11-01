using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class JournalDeltaStore
{
    private SnapshotDeltaSeal? restoreReadSeal;
    // A processing-path selector, never a total-input rejection. Both node
    // and per-file range arrays must stay on the small restore path only.
    internal bool RestoreIndexExceeds(long entries)
    {
        using (EnterGate()) { Check(); return (NodeRoot?.Count ?? nodes.Count) > entries || extentCount > entries; }
    }
    public void PrepareRestoreReads(SnapshotDeltaSeal seal)
    {
        using (EnterGate())
        {
            ObjectDisposedException.ThrowIf(disposed || closing, this);
            if (!readOnly || SealCurrent() != seal) throw new WorkspaceException("RestoreSealMismatch", "Use the exact stopped journal seal.");
            restoreReadSeal = seal;
        }
    }
    // Enumerate final metadata only. Payload stays in the sealed journal; no
    // unchanged bytes or complete changed file are materialized here.
    public DeltaRestoreEntry[] DescribeRestore()
    {
        using (EnterGate())
        {
            Check();
            if (!readOnly) throw new WorkspaceException("RestoreRequiresSealedDelta", "Stop the writer and open its fixed journal read-only.");
            void Walk(string path)
            {
                foreach (var entry in List(path)) if (entry.Directory) Walk(entry.Path);
            }
            Walk("");
            return paths.Values.Select(id => nodes[id]).Select(n => new DeltaRestoreEntry(n.Entry,
                n.Extents.Select(e => new DeltaRestoreRange(e.Start, e.Length)).ToArray())).ToArray();
        }
    }
    public IEnumerable<DeltaRestoreCursorEntry> EnumerateRestore()
    {
        using (EnterGate())
        {
            Check();
            if (!readOnly) throw new WorkspaceException("RestoreRequiresSealedDelta", "Stop the writer and open its fixed journal read-only.");
            if (pagedIndex is null) return DescribeRestore().Select(e => new DeltaRestoreCursorEntry(e.Entry, e.Ranges));
            // Recursively merge private and immutable directory cursors. Base
            // metadata is returned directly rather than hydrating every node
            // into the live index as a side effect of describing restoration.
            var index = pagedIndex; var fixedNodes = NodeRoot!; var fixedPaths = PathRoot!;
            var fixedHidden = HiddenRoot!; var fixedNames = pathComponents!.Root;
            var namesIndex = (PagedPathNamespace)paths; var components = pathComponents;
            var root = nodes[paths[""]].Entry;
            IEnumerable<DeltaEntry> Children(DeltaEntry entry) => PagedChildren(entry, index, fixedNodes, fixedPaths,
                fixedHidden, fixedNames, namesIndex, components);
            IEnumerable<DeltaEntry> Entries()
            {
                var pending = new Stack<IEnumerator<DeltaEntry>>(); pending.Push(Children(root).GetEnumerator());
                try
                {
                    while (pending.TryPeek(out var cursor))
                    {
                        if (!cursor.MoveNext()) { cursor.Dispose(); pending.Pop(); continue; }
                        var child = cursor.Current; yield return child;
                        if (child.Directory) pending.Push(Children(child).GetEnumerator());
                    }
                }
                finally { foreach (var cursor in pending) cursor.Dispose(); }
            }
            IEnumerable<DeltaRestoreCursorEntry> Cursor()
            {
                yield return new(root, Array.Empty<DeltaRestoreRange>());
                foreach (var entry in Entries())
                {
                    var node = index.Open(fixedNodes).FindEntry(entry.Id.ToString("N"));
                    var ranges = node?.Child;
                    IEnumerable<DeltaRestoreRange> RangeCursor()
                    {
                        using var replayLease = readOnlyReplay?.Retain();
                        if (ranges is null) yield break;
                        foreach (var item in index.Open(ranges).Enumerate())
                        { var extent = DecodeExtent(item.Value.ToArray()); yield return new(extent.Start, extent.Length); }
                    }
                    yield return new(entry, RangeCursor());
                }
            }
            return ProtectIndexCursor(Cursor());
        }
    }
    // After all basis readers are stopped, restoration may move the original
    // files. This API reads private payload exclusively, independent of basis.
    public void ReadRestoreRange(Guid id, long offset, Span<byte> destination, SnapshotDeltaSeal seal)
    {
        using (EnterGate())
        {
            ObjectDisposedException.ThrowIf(disposed || closing, this);
            if (!readOnly || restoreReadSeal != seal || (segments is not null ? sourceMutationBytes : CurrentJournalBytes) != seal.JournalBytes || sequence != seal.DurableSequence)
                throw new WorkspaceException("RestoreSealMismatch", "Retain a changed restoration journal.");
            var node = nodes[id]; int copied = 0;
            var (_, probes) = pagedIndex is null ? FindExtent(node.Extents, offset) : (0, 0);
            rangeReadCalls++; rangeReadSearchProbes += probes;
            foreach (var extent in node.Extents.From(offset))
            {
                rangeReadVisitedExtents++;
                if (extent.Start > offset + copied) break;
                long end = Math.Min(extent.Start + extent.Length, offset + destination.Length);
                long start = Math.Max(extent.Start, offset + copied);
                if (end <= start) continue;
                int count = checked((int)(end - start));
                ReadExtent(extent, checked((int)(start - extent.Start)), destination.Slice(copied, count)); copied += count;
                if (copied == destination.Length) return;
            }
            if (destination.Length != 0) throw new WorkspaceException("RestoreRangeNotPrivate", "A restore range must never fall back to the changing basis.");
        }
    }
}
