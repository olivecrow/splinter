using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class JournalDeltaStore
{
    private SnapshotDeltaSeal? restoreReadSeal;
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
    // After all basis readers are stopped, restoration may move the original
    // files. This API reads private payload exclusively, independent of basis.
    public void ReadRestoreRange(Guid id, long offset, Span<byte> destination, SnapshotDeltaSeal seal)
    {
        using (EnterGate())
        {
            ObjectDisposedException.ThrowIf(disposed || closing, this);
            if (!readOnly || restoreReadSeal != seal || CurrentJournalBytes != seal.JournalBytes || sequence != seal.DurableSequence)
                throw new WorkspaceException("RestoreSealMismatch", "Retain a changed restoration journal.");
            var node = nodes[id]; int copied = 0;
            var (first, probes) = FindExtent(node.Extents, offset);
            rangeReadCalls++; rangeReadSearchProbes += probes;
            for (int i = first; i < node.Extents.Count; i++)
            {
                var extent = node.Extents[i]; rangeReadVisitedExtents++;
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
