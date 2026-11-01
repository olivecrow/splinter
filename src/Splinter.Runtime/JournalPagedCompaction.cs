using System.Diagnostics;
using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class JournalDeltaStore
{
    private long pagedComparedBytes, pagedRevertedBytes;
    private IEnumerable<LiveRange> PagedCompactionRanges(Node node, bool revert)
    {
        foreach (var extent in node.Extents)
        {
            if (!revert || node.Entry.BasePath is null || extent.Start >= node.Entry.BaseLength)
            { yield return new(node.Entry.Id, extent.JournalOffset, extent.Length, extent.Start, extent.Payload, extent.BlockOffset); continue; }
            byte[] privateBytes = new byte[Math.Min(65536, extent.Length)], basisBytes = new byte[privateBytes.Length];
            LiveRange? pending = null;
            for (int at = 0; at < extent.Length;)
            {
                int count = Math.Min(privateBytes.Length, extent.Length - at); ReadExtent(extent, at, privateBytes.AsSpan(0, count));
                int comparable = checked((int)Math.Min(count, Math.Max(0, node.Entry.BaseLength - extent.Start - at))), done = 0;
                while (done < comparable)
                {
                    int n = snapshot.Read(node.Entry.BasePath, extent.Start + at + done, basisBytes.AsSpan(done, comparable - done));
                    if (n <= 0 || n > comparable - done) throw new WorkspaceException("SnapshotReadFailed", "The immutable basis ended before its protected compaction range.");
                    done += n; baseReadBytes += n; pagedComparedBytes += n;
                }
                for (int part = 0; part < count;)
                {
                    int unit = Math.Min(4096, count - part);
                    bool same = part + unit <= comparable && privateBytes.AsSpan(part, unit).SequenceEqual(basisBytes.AsSpan(part, unit));
                    if (same) { pagedRevertedBytes += unit; if (pending is not null) { yield return pending; pending = null; } }
                    else if (pending is null) pending = new(node.Entry.Id, extent.JournalOffset, unit, extent.Start + at + part, extent.Payload, extent.BlockOffset + at + part);
                    else pending = pending with { Length = checked(pending.Length + unit) };
                    part += unit;
                }
                at += count;
            }
            if (pending is not null) yield return pending;
        }
    }

    private static SnapshotJournalCompaction CompactPagedDirectory(string directory, OverlayIdentity identity, ISnapshotTree snapshot,
        Action<string>? boundary, long? overlapBudget, bool? requestedRevert, Func<string, IJournalDirectoryGuard>? guardFactory)
    {
        if (overlapBudget is <= 0) throw new WorkspaceException("InvalidCompactionBudget", "The overlap reservation must be positive.");
        string current = Path.Combine(directory, "delta.journal"), candidate = Path.Combine(directory, CandidateName), previous = Path.Combine(directory, BackupName), intentPath = Path.Combine(directory, IntentName);
        var intent = ReadIntent(intentPath);
        if (intent is not null && (intent.Version != 4 || intent.WorkspaceId != identity.Id || intent.SnapshotId != identity.Snapshot.Id ||
            requestedRevert is { } selected && selected != intent.RevertBaseRanges || intent.Bounded is null))
            throw new WorkspaceException("CompactionIntentMismatch", "Resume the exact owned paged compaction request.");
        long started = Stopwatch.GetTimestamp();
        using var source = new JournalDeltaStore(directory, identity, snapshot, directoryGuardFactory: guardFactory);
        if (source.boundedSettings is null || source.latest is not null) throw PagedInvalid();
        var before = source.SealCurrent();
        if (intent?.Candidate is { } completed && before == completed)
        {
            if (source.BoundedStateHash() != intent.ExpectedStateHash) throw PagedInvalid();
            return PagedCompactionReceipt(intent);
        }
        if (intent is not null && before != intent.Before || File.Exists(previous)) throw new WorkspaceException("CompactionGenerationMismatch", "Preserve the different compaction generation.");
        string sourceHash = source.BoundedStateHash();
        if (intent is not null && sourceHash != intent.StateHash) throw PagedInvalid();
        bool revert = intent?.RevertBaseRanges ?? requestedRevert ?? false;
        var next = intent?.Bounded ?? source.boundedSettings with { Generation = Guid.NewGuid().ToString("N") };
        source.pagedComparedBytes = source.pagedRevertedBytes = 0;
        long estimated = source.WriteStateFrames(null, null, null, next, revert);
        if (overlapBudget is { } budget && checked(before.JournalBytes + estimated) > budget)
            throw new WorkspaceException("CompactionOverlapBudgetExceeded", "Preserve the source and resume with space for both journal generations.");
        if (new DriveInfo(Path.GetPathRoot(Path.GetFullPath(current))!).AvailableFreeSpace < estimated)
            throw new WorkspaceException("CompactionHeadroomRequired", "The backing volume cannot hold this request's journal candidate.");
        if (intent is null)
        {
            intent = new(4, identity.Id, identity.Snapshot.Id, next.Generation, DateTimeOffset.UtcNow, before, sourceHash, sourceHash,
                revert, source.pagedComparedBytes, source.pagedRevertedBytes, Bounded: next);
            WriteIntent(intentPath, intent); boundary?.Invoke("intent");
        }
        if (File.Exists(candidate))
        {
            if (intent.Candidate is { } sealedCandidate)
            { if (SealJournal(candidate, before.DurableSequence) != sealedCandidate) throw PagedInvalid(); }
            else
            {
                using var compare = new BoundedCandidatePrefixVerifier(candidate);
                source.WriteStateFrames(null, compare, null, next, revert); compare.Complete();
            }
            File.Delete(candidate);
        }
        using (var output = new FileStream(candidate, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 65536))
        {
            long actual = source.WriteStateFrames(null, output, null, next, revert);
            if (actual != estimated || output.Length != estimated) throw PagedInvalid(); output.Flush(true);
        }
        boundary?.Invoke("candidate-flushed");
        SnapshotDeltaSeal after; string expectedHash;
        using (var check = new JournalDeltaStore(directory, identity, snapshot, CandidateName, true))
        { after = check.SealCurrent(); expectedHash = check.BoundedStateHash(); }
        intent = intent with { Candidate = after, ExpectedStateHash = expectedHash };
        WriteIntent(intentPath, intent); boundary?.Invoke("candidate");
        if (source.SealCurrent() != before) throw PagedInvalid();
        source.journal.Dispose(); File.Replace(candidate, current, previous); boundary?.Invoke("published");
        return PagedCompactionReceipt(intent) with { Cost = new(Stopwatch.Frequency, Stopwatch.GetTimestamp() - started,
            [new("paged-state-stream", Stopwatch.GetTimestamp() - started, source.journalReadBytes, estimated, 1)]) };
    }
    private static SnapshotJournalCompaction PagedCompactionReceipt(CompactionIntent intent) => new(intent.AttemptId, intent.Before, intent.Candidate!, intent.StartedAt,
        RevertBaseRanges: intent.RevertBaseRanges, BaseComparedBytes: intent.BaseComparedBytes, RevertedPayloadBytes: intent.RevertedPayloadBytes,
        JournalOptimizationEnabled: false);

    private static void CompletePagedCompaction(string directory, OverlayIdentity identity, Action<string>? boundary)
    {
        string intentPath = Path.Combine(directory, IntentName), previous = Path.Combine(directory, BackupName), current = Path.Combine(directory, "delta.journal");
        var intent = ReadIntent(intentPath) ?? throw new WorkspaceException("CompactionIntentMissing", "The owned paged compaction intent is missing.");
        if (intent.Version != 4 || intent.WorkspaceId != identity.Id || intent.SnapshotId != identity.Snapshot.Id || intent.Candidate is not { } after ||
            SealJournal(current, after.DurableSequence) != after || File.Exists(Path.Combine(directory, CandidateName)) ||
            File.Exists(previous) && SealJournal(previous, intent.Before.DurableSequence) != intent.Before) throw PagedInvalid();
        boundary?.Invoke("before-previous-delete"); if (File.Exists(previous)) File.Delete(previous);
        boundary?.Invoke("previous-deleted"); File.Delete(intentPath); boundary?.Invoke("intent-deleted");
    }
}
