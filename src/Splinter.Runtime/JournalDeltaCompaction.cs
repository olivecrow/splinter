using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class JournalDeltaStore
{
    private sealed record LiveRange(Guid NodeId, long Physical, int Length, long Logical);
    private sealed record PayloadSlice(long Physical, long Logical, int Length);
    private sealed record CompactionFrame(Mutation Mutation, byte[] Metadata, PayloadSlice[] Slices, int PayloadLength);
    private sealed record CompactionIntent(int Version, string WorkspaceId, string SnapshotId, string AttemptId,
        DateTimeOffset StartedAt, SnapshotDeltaSeal Before, string StateHash, SnapshotDeltaSeal? Candidate = null);

    private const string CandidateName = "delta.journal.compacting";
    private const string BackupName = "delta.journal.previous";
    private const string IntentName = "compaction-intent.json";

    private sealed class CompactionCostBuilder
    {
        private readonly List<SnapshotCompactionStageCost> stages = [];
        public void Add(string stage, long elapsedTicks, long readBytes = 0, long writtenBytes = 0, int flushCount = 0) =>
            stages.Add(new(stage, elapsedTicks, readBytes, writtenBytes, flushCount));
        public T Measure<T>(string stage, JournalDeltaStore? reader, Func<T> action,
            Func<T, long>? directReadBytes = null, Func<T, long>? writtenBytes = null, int flushCount = 0)
        {
            long beforeRead = reader?.journalReadBytes ?? 0;
            long started = Stopwatch.GetTimestamp();
            T result = action();
            Add(stage, Stopwatch.GetTimestamp() - started,
                (reader?.journalReadBytes ?? 0) - beforeRead + (directReadBytes?.Invoke(result) ?? 0),
                writtenBytes?.Invoke(result) ?? 0, flushCount);
            return result;
        }
        public void Measure(string stage, JournalDeltaStore? reader, Action action,
            long directReadBytes = 0, int flushCount = 0)
        {
            long beforeRead = reader?.journalReadBytes ?? 0;
            long started = Stopwatch.GetTimestamp();
            action();
            Add(stage, Stopwatch.GetTimestamp() - started,
                (reader?.journalReadBytes ?? 0) - beforeRead + directReadBytes, 0, flushCount);
        }
        public SnapshotCompactionCost Build(long totalTicks) => new(Stopwatch.Frequency, totalTicks, [.. stages]);
    }

    public static SnapshotJournalCompaction CompactDirectory(string directory, OverlayIdentity identity, ISnapshotTree snapshot,
        Action<string>? boundary = null, long? maxEstimatedJournalOverlapBytes = null)
    {
        if (maxEstimatedJournalOverlapBytes is <= 0)
            throw new WorkspaceException("InvalidCompactionBudget", "The estimated journal overlap budget must be positive.");
        long totalStarted = Stopwatch.GetTimestamp();
        var cost = new CompactionCostBuilder();
        string current = Path.Combine(directory, "delta.journal"), candidate = Path.Combine(directory, CandidateName);
        string backup = Path.Combine(directory, BackupName), intentPath = Path.Combine(directory, IntentName);
        if (!File.Exists(current)) throw new WorkspaceException("DeltaMissing", "Preserve the workspace: its journal is missing.");
        var intent = ReadIntent(intentPath);
        if (intent is not null && (intent.Version != 1 || intent.WorkspaceId != identity.Id || intent.SnapshotId != identity.Snapshot.Id ||
            string.IsNullOrWhiteSpace(intent.AttemptId)))
            throw new WorkspaceException("CompactionIntentMismatch", "Preserve a compaction intent with another owner or snapshot.");
        if (intent?.Candidate is { } ready && Matches(current, ready) && !File.Exists(candidate))
        {
            if (File.Exists(backup) && !Matches(backup, intent.Before))
                throw new WorkspaceException("CompactionGenerationMismatch", "The retained previous journal differs from its intent.");
            using var verified = new JournalDeltaStore(directory, identity, snapshot);
            if (verified.StateHash() != intent.StateHash)
                throw new WorkspaceException("CompactionStateMismatch", "The published journal does not reconstruct the original state.");
            return Receipt(intent);
        }
        if (intent is not null && !Matches(current, intent.Before))
            throw new WorkspaceException("CompactionGenerationMismatch", "The active journal differs from its compaction intent; preserve every generation.");
        if (File.Exists(backup))
            throw new WorkspaceException("CompactionGenerationMismatch", "An unverified previous journal exists; preserve it.");
        long replayStarted = Stopwatch.GetTimestamp();
        using (var old = new JournalDeltaStore(directory, identity, snapshot))
        {
            cost.Add("source-replay", Stopwatch.GetTimestamp() - replayStarted, old.replayBytes);
            var before = cost.Measure("before-seal", old, old.SealCurrent, directReadBytes: _ => old.journal.Length);
            string stateHash = cost.Measure("before-state-hash", old, old.StateHash);
            if (intent is not null && (before != intent.Before || stateHash != intent.StateHash))
                throw new WorkspaceException("CompactionGenerationMismatch", "The source journal changed since compaction started.");
            List<LiveRange>? live = null;
            long estimated = 0;
            if (intent?.Candidate is null)
            {
                live = cost.Measure("live-range-index", old, old.LiveRanges);
                estimated = cost.Measure("candidate-scan", old, () => old.WalkFrames(live, null));
                if (estimated > identity.MaxJournalBytes)
                    throw new WorkspaceException("DeltaFull", "The compacted generation exceeds this workspace journal budget.");
                long projected = checked(old.journal.Length + estimated);
                if (maxEstimatedJournalOverlapBytes is { } budget && projected > budget)
                    throw new WorkspaceException("CompactionOverlapBudgetExceeded",
                        "The estimated overlap of the old and candidate journals exceeds the requested budget; preserve the old journal and retry this workspace with an explicit budget.",
                        new { beforeJournalBytes = old.journal.Length, candidateEstimatedJournalBytes = estimated,
                            projectedJournalOverlapBytes = projected, maxEstimatedJournalOverlapBytes = budget });
            }
            if (intent is null)
            {
                intent = new(1, identity.Id, identity.Snapshot.Id, Guid.NewGuid().ToString("N"),
                    DateTimeOffset.UtcNow, before, stateHash);
                cost.Measure("intent-publication", null, () => WriteIntent(intentPath, intent), flushCount: 1);
                boundary?.Invoke("intent");
            }
            if (intent.Candidate is null)
            {
                // The unfinished candidate was never published. The authoritative
                // journal and its seal have been verified before replacing it.
                if (File.Exists(candidate)) File.Delete(candidate);
                old.WriteCompactedCandidate(candidate, before, live!, estimated, cost);
                boundary?.Invoke("candidate-flushed");
                long candidateReplayStarted = Stopwatch.GetTimestamp();
                using var check = new JournalDeltaStore(directory, identity, snapshot, CandidateName);
                cost.Add("candidate-replay", Stopwatch.GetTimestamp() - candidateReplayStarted, check.replayBytes);
                if (cost.Measure("candidate-state-hash", check, check.StateHash) != intent.StateHash || check.sequence != old.sequence)
                    throw new WorkspaceException("CompactionStateMismatch", "Candidate replay differs from the active journal; preserve both.");
                intent = intent with { Candidate = cost.Measure("candidate-seal", check, check.SealCurrent,
                    directReadBytes: _ => check.journal.Length) };
                cost.Measure("prepared-intent-publication", null, () => WriteIntent(intentPath, intent), flushCount: 1);
                boundary?.Invoke("prepared");
            }
        }
        if (intent.Candidate is not { } proposed || !Matches(candidate, proposed))
            throw new WorkspaceException("CompactionCandidateMismatch", "The prepared candidate is missing or changed; preserve the original journal.");
        cost.Measure("generation-publication", null, () => File.Replace(candidate, current, backup));
        boundary?.Invoke("published");
        bool publishedMatches = cost.Measure("published-generation-verify", null,
            () => Matches(current, proposed) && Matches(backup, intent.Before),
            directReadBytes: _ => proposed.JournalBytes + intent.Before.JournalBytes);
        if (!publishedMatches)
            throw new WorkspaceException("CompactionGenerationMismatch", "The generation switch is uncertain; preserve all remaining files.");
        return Receipt(intent) with { Cost = cost.Build(Stopwatch.GetTimestamp() - totalStarted) };
    }

    public static void CompleteCompaction(string directory, OverlayIdentity identity)
    {
        string current = Path.Combine(directory, "delta.journal"), backup = Path.Combine(directory, BackupName);
        string intentPath = Path.Combine(directory, IntentName);
        var intent = ReadIntent(intentPath) ?? throw new WorkspaceException("CompactionIntentMissing", "The compaction intent is missing.");
        if (intent.Version != 1 || intent.WorkspaceId != identity.Id || intent.SnapshotId != identity.Snapshot.Id ||
            string.IsNullOrWhiteSpace(intent.AttemptId) || intent.Candidate is not { } candidate ||
            File.Exists(Path.Combine(directory, CandidateName)) ||
            !Matches(current, candidate) || File.Exists(backup) && !Matches(backup, intent.Before))
            throw new WorkspaceException("CompactionGenerationMismatch", "The selected journal or previous generation differs from the intent.");
        if (File.Exists(backup)) File.Delete(backup);
        File.Delete(intentPath);
    }

    private static SnapshotJournalCompaction Receipt(CompactionIntent intent) =>
        new(intent.AttemptId, intent.Before, intent.Candidate!, intent.StartedAt);

    private static CompactionIntent? ReadIntent(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var intent = JsonSerializer.Deserialize<CompactionIntent>(File.ReadAllText(path));
            if (intent is null || intent.Before is null || string.IsNullOrWhiteSpace(intent.StateHash) ||
                !ValidSeal(intent.Before) || intent.Candidate is { } candidate && !ValidSeal(candidate))
                throw new WorkspaceException("CompactionIntentCorrupt", "Preserve the incomplete compaction intent and journals.");
            return intent;
        }
        catch (JsonException) { throw new WorkspaceException("CompactionIntentCorrupt", "Preserve the unreadable compaction intent and journals."); }
    }

    private static bool ValidSeal(SnapshotDeltaSeal seal) => seal.DurableSequence >= 0 && seal.JournalBytes >= 0 &&
        seal.JournalHash is { Length: 64 } hash && hash.All(Uri.IsHexDigit);

    private static void WriteIntent(string path, CompactionIntent intent)
    {
        string temp = path + ".tmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, intent);
            stream.Flush(true);
        }
        AtomicFilePublication.Publish(temp, path);
    }

    private static bool Matches(string path, SnapshotDeltaSeal seal)
    {
        if (!File.Exists(path)) return false;
        // The reconnected provider owns a read/write handle while its verified
        // generation is being checked. This reader must share that handle.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return stream.Length == seal.JournalBytes &&
            Convert.ToHexStringLower(SHA256.HashData(stream)) == seal.JournalHash;
    }

    private SnapshotDeltaSeal SealCurrent()
    {
        journal.Position = 0;
        string hash = Convert.ToHexStringLower(SHA256.HashData(journal));
        return new(sequence, journal.Length, hash);
    }

    private string StateHash()
    {
        using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        void Add(object value)
        {
            byte[] data = JsonSerializer.SerializeToUtf8Bytes(value);
            Span<byte> length = stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(length, data.Length);
            digest.AppendData(length); digest.AppendData(data);
        }
        Add(sequence);
        foreach (string path in paths.Keys.Order(StringComparer.OrdinalIgnoreCase))
        {
            var node = nodes[paths[path]];
            Add(path); Add(node.Entry);
            foreach (var extent in node.Extents)
            {
                Add(new { extent.Start, extent.Length });
                byte[] buffer = ArrayPool<byte>.Shared.Rent(65536);
                try
                {
                    long offset = extent.JournalOffset; int remaining = extent.Length;
                    while (remaining > 0)
                    {
                        int count = Math.Min(buffer.Length, remaining);
                        ReadExactly(offset, buffer.AsSpan(0, count));
                        digest.AppendData(buffer.AsSpan(0, count));
                        offset += count; remaining -= count;
                    }
                }
                finally { ArrayPool<byte>.Shared.Return(buffer); }
            }
        }
        foreach (string path in hidden.Order(StringComparer.OrdinalIgnoreCase)) Add(path);
        foreach (var change in pathChanges.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)) Add(change);
        return Convert.ToHexStringLower(digest.GetHashAndReset());
    }

    private List<LiveRange> LiveRanges()
    {
        var live = new List<LiveRange>();
        foreach (Guid id in paths.Values.Distinct())
        {
            var node = nodes[id];
            if (node.Entry.Deleted) continue;
            live.AddRange(node.Extents.Select(e => new LiveRange(id, e.JournalOffset, e.Length, e.Start)));
        }
        live.Sort((a, b) => a.Physical.CompareTo(b.Physical));
        for (int i = 1; i < live.Count; i++)
            if (live[i - 1].Physical + live[i - 1].Length > live[i].Physical)
                throw new WorkspaceException("DeltaCorrupt", "Live journal payload ranges overlap; preserve the journal.");
        return live;
    }

    private void WriteCompactedCandidate(string path, SnapshotDeltaSeal originalSeal, List<LiveRange> live,
        long estimated, CompactionCostBuilder cost)
    {
        string root = Path.GetPathRoot(Path.GetFullPath(path))!;
        // ConnectAsync also requires 2 GiB after the candidate is written.
        // Keep that mount headroom before switching generations.
        long reserve = 2L * 1024 * 1024 * 1024;
        if (new DriveInfo(root).AvailableFreeSpace < checked(estimated + reserve))
            throw new WorkspaceException("CompactionHeadroomRequired", "Free space cannot hold the compacted generation and safety headroom.");
        using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536))
        {
            long actual = cost.Measure("candidate-write", this, () => WalkFrames(live, output), writtenBytes: bytes => bytes);
            if (actual != estimated || output.Length != actual)
                throw new WorkspaceException("CompactionCandidateMismatch", "Candidate length changed during compaction.");
            cost.Measure("candidate-flush", null, () => output.Flush(true), flushCount: 1);
        }
        if (cost.Measure("after-source-seal", this, SealCurrent, directReadBytes: _ => journal.Length) != originalSeal)
            throw new WorkspaceException("CompactionGenerationMismatch", "The original journal changed during compaction.");
    }

    private long WalkFrames(List<LiveRange> live, FileStream? output)
    {
        long cursor = 0, total = 0;
        int liveIndex = 0;
        CompactionFrame? pending = null;
        void Emit(CompactionFrame? frame)
        {
            if (frame is null) return;
            total = checked(total + 8L + frame.Metadata.Length + frame.PayloadLength + 32);
            if (output is not null) WriteFrame(output, frame);
        }
        static bool Skipped(CompactionFrame frame) => frame.Mutation.Operation is "skip-write" or "skip-range";
        byte[] header = new byte[8];
        while (cursor < journal.Length)
        {
            ReadExactly(cursor, header);
            int metadataLength = BinaryPrimitives.ReadInt32LittleEndian(header);
            int dataLength = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4));
            if (metadataLength is < 1 or > MaxMetadataBytes || dataLength < 0 || dataLength > Identity.MaxTransferBytes)
                throw new WorkspaceException("DeltaCorrupt", "Invalid compaction input frame.");
            long payload = cursor + 8L + metadataLength, next = payload + dataLength + 32;
            if (next > journal.Length) throw new WorkspaceException("DeltaCorrupt", "Incomplete compaction input frame.");
            byte[] metadata = new byte[metadataLength]; ReadExactly(cursor + 8, metadata);
            var mutation = DecodeMutation(metadata, dataLength);
            while (liveIndex < live.Count && live[liveIndex].Physical + live[liveIndex].Length <= payload) liveIndex++;
            var slices = new List<PayloadSlice>();
            for (int i = liveIndex; i < live.Count && live[i].Physical < payload + dataLength; i++)
            {
                var range = live[i];
                long from = Math.Max(range.Physical, payload), to = Math.Min(range.Physical + range.Length, payload + dataLength);
                if (from >= to) continue;
                if (mutation.Operation is not ("write" or "write-compacted") || range.NodeId != mutation.Entry.Id)
                    throw new WorkspaceException("DeltaCorrupt", "A live extent does not belong to its journal write.");
                slices.Add(new(from, range.Logical + from - range.Physical, checked((int)(to - from))));
            }
            CompactionFrame frame = FrameFor(mutation, metadata, payload, dataLength, slices);
            long first = frame.Mutation.FromSequence ?? frame.Mutation.Sequence;
            if (pending is not null && Skipped(pending) && Skipped(frame) &&
                pending.Mutation.Sequence + 1 == first &&
                pending.Mutation.Entry.Path.Equals(frame.Mutation.Entry.Path, StringComparison.Ordinal))
            {
                var merged = frame.Mutation with { Operation = "skip-range",
                    FromSequence = pending.Mutation.FromSequence ?? pending.Mutation.Sequence };
                byte[] encoded = JsonSerializer.SerializeToUtf8Bytes(merged);
                if (encoded.Length <= MaxMetadataBytes)
                {
                    pending = new(merged, encoded, [], 0);
                    cursor = next;
                    continue;
                }
            }
            Emit(pending); pending = frame;
            cursor = next;
        }
        Emit(pending);
        if (cursor != journal.Length) throw new WorkspaceException("DeltaCorrupt", "Compaction input length changed.");
        return total;
    }

    private static CompactionFrame FrameFor(Mutation mutation, byte[] metadata, long payload, int dataLength, List<PayloadSlice> slices)
    {
        if (mutation.Operation is not ("write" or "write-compacted"))
            return new(mutation, metadata, [], dataLength);
        if (slices.Count == 0)
        {
            var skipped = mutation with { Operation = "skip-write", Fragments = null };
            byte[] encodedSkip = JsonSerializer.SerializeToUtf8Bytes(skipped);
            if (encodedSkip.Length <= MaxMetadataBytes) return new(skipped, encodedSkip, [], 0);
            return new(mutation, metadata, [new(payload, mutation.Offset, dataLength)], dataLength);
        }
        if (mutation.Operation == "write" && slices.Count == 1 && slices[0].Physical == payload && slices[0].Length == dataLength)
            return new(mutation, metadata, [new(payload, mutation.Offset, dataLength)], dataLength);
        var fragments = slices.Select(s => new CompactionFragment(s.Logical, s.Length)).ToArray();
        var compacted = mutation with { Operation = "write-compacted", Fragments = fragments };
        byte[] encoded = JsonSerializer.SerializeToUtf8Bytes(compacted);
        if (encoded.Length > MaxMetadataBytes)
            return new(mutation, metadata, [new(payload, mutation.Offset, dataLength)], dataLength);
        return new(compacted, encoded, slices.ToArray(), slices.Sum(s => s.Length));
    }

    private void WriteFrame(FileStream output, CompactionFrame frame)
    {
        Span<byte> header = stackalloc byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(header, frame.Metadata.Length);
        BinaryPrimitives.WriteInt32LittleEndian(header[4..], frame.PayloadLength);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        output.Write(header); output.Write(frame.Metadata);
        hash.AppendData(header); hash.AppendData(frame.Metadata);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(65536);
        try
        {
            foreach (var slice in frame.Slices)
            {
                long at = slice.Physical; int remaining = slice.Length;
                while (remaining > 0)
                {
                    int count = Math.Min(buffer.Length, remaining);
                    ReadExactly(at, buffer.AsSpan(0, count));
                    output.Write(buffer.AsSpan(0, count)); hash.AppendData(buffer.AsSpan(0, count));
                    at += count; remaining -= count;
                }
            }
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
        output.Write(hash.GetHashAndReset());
    }
}
