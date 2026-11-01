using System.Diagnostics;
using System.Text.Json;
using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class JournalDeltaStore
{
    private sealed record SegmentCompactionIntent(int Version, string Workspace, string Snapshot, string Attempt,
        DateTimeOffset Started, SnapshotDeltaSeal Before, bool Revert, string? Nodes = null, string? Names = null,
        long Extents = 0, long Compared = 0, long Reverted = 0, SnapshotDeltaSeal? After = null, string Digest = "");
    private static SegmentCompactionIntent? ReadSegmentCompaction(string path)
    {
        if (!File.Exists(path)) return null;
        var intent = JournalControlIntegrity.Read<SegmentCompactionIntent>(path);
        if (intent.Version != 6 || intent.Digest != JournalControlIntegrity.Digest(intent with { Digest = "" })) throw PagedInvalid(); return intent;
    }
    private static void PublishSegmentCompaction(string path, SegmentCompactionIntent intent)
    {
        intent = intent with { Digest = JournalControlIntegrity.Digest(intent with { Digest = "" }) }; string pending = path + ".tmp";
        using (var file = new FileStream(pending, FileMode.Create, FileAccess.Write, FileShare.Read))
        { JsonSerializer.Serialize(file, intent, JournalControlIntegrity.Json); file.Flush(true); }
        AtomicFilePublication.Publish(pending, path);
    }
    private static SnapshotJournalCompaction SegmentCompactionReceipt(SegmentCompactionIntent intent) => new(intent.Attempt, intent.Before,
        intent.After ?? throw PagedInvalid(), intent.Started, RevertBaseRanges: intent.Revert, BaseComparedBytes: intent.Compared,
        RevertedPayloadBytes: intent.Reverted, JournalOptimizationEnabled: false);
    private static SnapshotJournalCompaction CompactSegmentedDirectory(string directory, OverlayIdentity identity, ISnapshotTree basis,
        Action<string>? boundary, long? overlapBudget, bool? requestedRevert, Func<string, IJournalDirectoryGuard>? guardFactory)
    {
        if (overlapBudget is <= 0) throw new WorkspaceException("InvalidCompactionBudget", "The overlap reservation must be positive.");
        string path = Path.Combine(directory, IntentName); var intent = ReadSegmentCompaction(path);
        using var source = new JournalDeltaStore(directory, identity, basis, directoryGuardFactory: guardFactory);
        var observed = source.SealCurrent();
        if (intent is not null && (intent.Version != 6 || intent.Workspace != identity.Id || intent.Snapshot != identity.Snapshot.Id ||
            requestedRevert is { } selected && selected != intent.Revert)) throw new WorkspaceException("CompactionIntentMismatch", "Resume the exact segmented maintenance request.");
        if (intent?.After is { } after)
        { if (observed != after) throw PagedInvalid(); return SegmentCompactionReceipt(intent); }
        if (intent?.Nodes is { } nodes && source.segments!.State.Roots.Nodes.ContentHash == nodes &&
            source.segments.State.Roots.Names.ContentHash == intent.Names && source.extentCount == intent.Extents && source.sequence == intent.Before.DurableSequence)
        {
            intent = intent with { After = observed }; PublishSegmentCompaction(path, intent); return SegmentCompactionReceipt(intent);
        }
        if (intent is not null && observed != intent.Before) throw new WorkspaceException("CompactionGenerationMismatch", "Preserve the different segmented journal generation.");
        intent ??= new(6, identity.Id, identity.Snapshot.Id, Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, observed, requestedRevert ?? false);
        PublishSegmentCompaction(path, intent); boundary?.Invoke("intent");
        long compared = 0, reverted = 0;
        if (intent.Revert)
        {
            byte[] original = new byte[65536], changed = new byte[65536];
            foreach (var node in source.nodes.Values)
            {
                if (node.Entry.BasePath is null || node.Entry.Deleted || node.Entry.Directory) continue;
                var ranges = source.pagedIndex!.Empty;
                foreach (var extent in node.Extents)
                {
                    Extent? retained = null;
                    for (int at = 0; at < extent.Length;)
                    {
                        int count = Math.Min(changed.Length, extent.Length - at); source.ReadExtent(extent, at, changed.AsSpan(0, count));
                        int comparable = (int)Math.Min(count, Math.Max(0, node.Entry.BaseLength - extent.Start - at)), done = 0;
                        while (done < comparable)
                        { int read = basis.Read(node.Entry.BasePath, extent.Start + at + done, original.AsSpan(done, comparable - done)); if (read <= 0) throw PagedInvalid(); done += read; compared += read; }
                        for (int part = 0; part < count;)
                        {
                            int unit = Math.Min(4096, count - part);
                            if (part + unit <= comparable && changed.AsSpan(part, unit).SequenceEqual(original.AsSpan(part, unit)))
                            {
                                reverted += unit;
                                if (retained is not null) { ranges = source.pagedIndex.Set(ranges, retained.Start.ToString("x16"), EncodeExtent(retained)); retained = null; }
                            }
                            else if (retained is null) retained = extent with { Start = extent.Start + at + part, Length = unit, BlockOffset = extent.BlockOffset + at + part,
                                OriginOffset = extent.OriginBlock is null ? 0 : checked(extent.OriginOffset + at + part) };
                            else retained = retained with { Length = checked(retained.Length + unit) };
                            part += unit;
                        }
                        at += count;
                    }
                    if (retained is not null) ranges = source.pagedIndex.Set(ranges, retained.Start.ToString("x16"), EncodeExtent(retained));
                }
                long prior = node.Extents.LongCount; node.Extents.Attach(source.pagedIndex, ranges, () => ((PagedNodeMap)source.nodes)[node.Entry.Id] = node);
                ((PagedNodeMap)source.nodes)[node.Entry.Id] = node; source.extentCount = checked(source.extentCount - prior + ranges.Count);
            }
        }
        source.pagedIndex!.Flush();
        intent = intent with { Nodes = source.NodeRoot!.ContentHash, Names = source.pathComponents!.Root.ContentHash,
            Extents = source.extentCount, Compared = compared, Reverted = reverted };
        PublishSegmentCompaction(path, intent); boundary?.Invoke("candidate-flushed");
        source.RollSegment(); boundary?.Invoke("published");
        intent = intent with { After = source.SealCurrent() }; PublishSegmentCompaction(path, intent); boundary?.Invoke("candidate");
        return SegmentCompactionReceipt(intent);
    }
    private static void CompleteSegmentedCompaction(string directory, OverlayIdentity identity, Action<string>? boundary)
    {
        string path = Path.Combine(directory, IntentName); var intent = ReadSegmentCompaction(path) ?? throw PagedInvalid();
        if (intent.Version != 6 || intent.Workspace != identity.Id || intent.Snapshot != identity.Snapshot.Id || intent.After is not { } after ||
            SealSegmentedJournal(Path.Combine(directory, "delta.journal"), after.DurableSequence) != after) throw PagedInvalid();
        boundary?.Invoke("before-previous-delete"); boundary?.Invoke("previous-deleted"); File.Delete(path); boundary?.Invoke("intent-deleted");
    }
}
