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
    private sealed record LiveRange(Guid NodeId, long Physical, int Length, long Logical,
        WorkspaceJournalPayload Payload, int BlockOffset);
    private sealed record PayloadSlice(long Physical, long Logical, int Length,
        WorkspaceJournalPayload Payload, int BlockOffset = 0);
    private sealed record CompactionFrame(Mutation Mutation, byte[] Metadata, PayloadSlice[] Slices, int PayloadLength,
        WorkspaceJournalPayload Payload, byte[]? Encoded = null, CompactionFrame? Fallback = null);
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

    private string StateHash() => StateHash(null);
    private string StateHash(IReadOnlySet<string>? projectedFiles)
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
        foreach (string path in paths.Keys.Order(pathPolicy.Comparer))
        {
            if (projectedFiles?.Contains(path) == true) continue;
            var node = nodes[paths[path]];
            Add(path); Add(node.Entry);
            foreach (var extent in node.Extents)
            {
                Add(new { extent.Start, extent.Length });
                VisitExtent(extent, (_, bytes, _) => digest.AppendData(bytes));
            }
        }
        foreach (string path in hidden.Order(pathPolicy.Comparer))
            if (projectedFiles?.Contains(path) != true) Add(path);
        foreach (var change in pathChanges.OrderBy(x => x.Key, pathPolicy.Comparer)) Add(change);
        return Convert.ToHexStringLower(digest.GetHashAndReset());
    }

    private List<LiveRange> LiveRanges() => LiveRanges(null);
    public ValidationCacheUsage ObservePrivateUsage()
    {
        using (EnterGate())
        {
            Check();
            var ranges = LiveRanges();
            long live = ranges.Sum(range => (long)range.Length);
            // A partially live compressed block remains wholly stored. Do not
            // subtract logical bytes from compressed physical accounting.
            long storedLive = ranges.GroupBy(r => r.Physical).Sum(g => (long)g.First().Payload.StoredBytes);
            return new(journal.Length, payloadBytes, live, payloadBytes - storedLive, journal.Length - payloadBytes)
            { LiveStoredPayloadBytes = storedLive, RetainedLogicalPayloadBytes = logicalPayloadBytes };
        }
    }
    public long EstimateCompactionJournalOverlapBytes()
    {
        using (EnterGate()) { Check(); return checked(journal.Length + WalkFrames(LiveRanges(), null)); }
    }
    private List<LiveRange> LiveRanges(IReadOnlySet<string>? projectedFiles)
    {
        var live = new List<LiveRange>();
        foreach (Guid id in paths.Values.Distinct())
        {
            var node = nodes[id];
            if (node.Entry.Deleted || projectedFiles?.Contains(node.Entry.Path) == true) continue;
            live.AddRange(node.Extents.Select(e => new LiveRange(id, e.JournalOffset, e.Length, e.Start, e.Payload, e.BlockOffset)));
        }
        live.Sort((a, b) => a.Physical != b.Physical ? a.Physical.CompareTo(b.Physical) : a.BlockOffset.CompareTo(b.BlockOffset));
        for (int i = 1; i < live.Count; i++)
            if (live[i - 1].Physical == live[i].Physical && live[i - 1].BlockOffset + live[i - 1].Length > live[i].BlockOffset)
                throw new WorkspaceException("DeltaCorrupt", "Live journal payload ranges overlap; preserve the journal.");
        return live;
    }

    private void WriteCompactedCandidate(string path, SnapshotDeltaSeal originalSeal, List<LiveRange> live,
        long estimated, CompactionCostBuilder cost, IReadOnlySet<string>? projectedFiles = null)
    {
        string root = Path.GetPathRoot(Path.GetFullPath(path))!;
        // ConnectAsync also requires 2 GiB after the candidate is written.
        // Keep that mount headroom before switching generations.
        long reserve = 2L * 1024 * 1024 * 1024;
        if (new DriveInfo(root).AvailableFreeSpace < checked(estimated + reserve))
            throw new WorkspaceException("CompactionHeadroomRequired", "Free space cannot hold the compacted generation and safety headroom.");
        using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536))
        {
            long actual = cost.Measure("candidate-write", this, () => WalkFrames(live, output, projectedFiles), writtenBytes: bytes => bytes);
            if (actual != estimated || output.Length != actual)
                throw new WorkspaceException("CompactionCandidateMismatch", "Candidate length changed during compaction.");
            cost.Measure("candidate-flush", null, () => output.Flush(true), flushCount: 1);
        }
        if (cost.Measure("after-source-seal", this, SealCurrent, directReadBytes: _ => journal.Length) != originalSeal)
            throw new WorkspaceException("CompactionGenerationMismatch", "The original journal changed during compaction.");
    }

    private long WalkFrames(List<LiveRange> live, Stream? output, IReadOnlySet<string>? projectedFiles = null)
    {
        long cursor = 0, total = 0;
        int liveIndex = 0;
        CompactionFrame? pending = null;
        void Emit(CompactionFrame? frame)
        {
            if (frame is null) return;
            frame = frame.Fallback ?? frame;
            total = checked(total + FrameBytes(frame));
            if (output is not null) WriteFrame(output, frame);
        }
        static bool Skipped(CompactionFrame frame) => frame.Mutation.Operation is "skip-write" or "skip-range";
        byte[] header = new byte[FrameHeaderBytes];
        while (cursor < journal.Length)
        {
            ReadExactly(cursor, header);
            var (metadataLength, descriptor) = ReadHeader(header);
            int dataLength = descriptor.DecodedBytes;
            long payload = cursor + FrameHeaderBytes + (long)metadataLength, next = payload + descriptor.StoredBytes + FrameChecksumBytes;
            if (next > journal.Length) throw new WorkspaceException("DeltaCorrupt", "Incomplete compaction input frame.");
            byte[] metadata = new byte[metadataLength]; ReadExactly(cursor + FrameHeaderBytes, metadata);
            var mutation = DecodeMutation(metadata, dataLength);
            if (projectedFiles is not null) ValidateProjectionMutation(mutation, projectedFiles);
            while (liveIndex < live.Count && live[liveIndex].Physical < payload) liveIndex++;
            var slices = new List<PayloadSlice>();
            for (int i = liveIndex; i < live.Count && live[i].Physical == payload; i++)
            {
                var range = live[i];
                if (mutation.Operation is not ("write" or "write-compacted") || range.NodeId != mutation.Entry.Id)
                    throw new WorkspaceException("DeltaCorrupt", "A live extent does not belong to its journal write.");
                descriptor.RequireFragment(range.BlockOffset, range.Length);
                if (range.Payload != descriptor) throw new WorkspaceException("DeltaCorrupt", "Live block identity differs from its frame.");
                slices.Add(new(payload, range.Logical, range.Length, descriptor, range.BlockOffset));
            }
            CompactionFrame frame;
            if (projectedFiles?.Contains(mutation.Entry.Path) == true)
            {
                // Existing skip records retain sequence/path-change evidence,
                // but create no node and carry none of the managed payload.
                var skipped = mutation with { Operation = mutation.Operation == "skip-range" ? "skip-range" : "skip-write",
                    Destination = null, Replace = false, Fragments = null };
                frame = new(skipped, JsonSerializer.SerializeToUtf8Bytes(skipped), [], 0, new(0, 0));
            }
            else frame = FrameFor(mutation, metadata, payload, descriptor, slices);
            long first = frame.Mutation.FromSequence ?? frame.Mutation.Sequence;
            if (pending is not null && Skipped(pending) && Skipped(frame) &&
                pending.Mutation.Sequence + 1 == first &&
                pending.Mutation.Entry.Path.Equals(frame.Mutation.Entry.Path, StringComparison.Ordinal))
            {
                var merged = frame.Mutation with { Operation = "skip-range",
                    FromSequence = pending.Mutation.FromSequence ?? pending.Mutation.Sequence };
                byte[] encoded = JsonSerializer.SerializeToUtf8Bytes(merged);
                if (encoded.Length <= MaxMetadataBytes && encoded.Length + FrameHeaderBytes + FrameChecksumBytes <=
                    FrameBytes(pending.Fallback ?? pending) + FrameBytes(frame.Fallback ?? frame))
                {
                    pending = new(merged, encoded, [], 0, new(0, 0));
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

    private CompactionFrame FrameFor(Mutation mutation, byte[] metadata, long payload, WorkspaceJournalPayload descriptor, List<PayloadSlice> slices)
    {
        // Current SJR2 readers already supply these optional mutation defaults.
        // Explicit compaction may reduce their JSON without touching the stored
        // block or rewriting a saved journal merely because it was opened.
        byte[] reducedMetadata = JsonSerializer.SerializeToUtf8Bytes(mutation);
        if (reducedMetadata.Length < metadata.Length) metadata = reducedMetadata;
        if (mutation.Operation is not ("write" or "write-compacted"))
            return new(mutation, metadata, [], 0, descriptor);
        int dataLength = descriptor.DecodedBytes;
        var original = new CompactionFrame(mutation, metadata, [new(payload, mutation.Offset, dataLength, descriptor)], dataLength, descriptor);
        if (slices.Count == 0)
        {
            var skipped = mutation with { Operation = "skip-write", Fragments = null };
            byte[] encodedSkip = JsonSerializer.SerializeToUtf8Bytes(skipped);
            if (encodedSkip.Length > MaxMetadataBytes) return original;
            // Keep the skip eligible for adjacent range merging. If it remains
            // alone, retain its original frame when the skip metadata costs more.
            return new(skipped, encodedSkip, [], 0, new(0, 0), Fallback:
                encodedSkip.Length > metadata.Length + (long)descriptor.StoredBytes ? original : null);
        }
        if (mutation.Operation == "write" && slices.Count == 1 && slices[0].BlockOffset == 0 && slices[0].Length == dataLength)
            return original;
        var fragments = slices.Select(s => new CompactionFragment(s.Logical, s.Length)).ToArray();
        var compacted = mutation with { Operation = "write-compacted", Fragments = fragments };
        byte[] encoded = JsonSerializer.SerializeToUtf8Bytes(compacted);
        int liveBytes = slices.Sum(s => s.Length);
        // Fragment metadata can outweigh a few overwritten bytes. Retaining the
        // already-owned frame is smaller and still replays the same final state.
        if (encoded.Length > MaxMetadataBytes) return original;
        byte[] decoded = ArrayPool<byte>.Shared.Rent(liveBytes);
        byte[]? compressed;
        try
        {
            VisitSlices(slices, (at, bytes, _) => bytes.CopyTo(decoded.AsSpan(at)));
            compressed = EncodePayload(decoded.AsSpan(0, liveBytes));
        }
        finally { ArrayPool<byte>.Shared.Return(decoded); }
        var nextPayload = new WorkspaceJournalPayload(compressed?.Length ?? liveBytes, liveBytes);
        if (encoded.Length + (long)nextPayload.StoredBytes >= metadata.Length + (long)descriptor.StoredBytes) return original;
        return new(compacted, encoded, slices.ToArray(), liveBytes, nextPayload, compressed);
    }

    private static long FrameBytes(CompactionFrame frame) => FrameHeaderBytes + (long)FrameChecksumBytes + frame.Metadata.Length + frame.Payload.StoredBytes;

    private void VisitSlices(IReadOnlyList<PayloadSlice> slices, BrotliWorkspacePayloadCodec.DecodedChunkVisitor visit)
    {
        if (slices[0].Payload.Encoded)
        {
            int first = 0, outputOffset = 0;
            VisitPayload(slices[0].Physical, slices[0].Payload, (at, bytes, _) =>
            {
                while (first < slices.Count)
                {
                    var slice = slices[first];
                    int start = Math.Max(at, slice.BlockOffset), end = Math.Min(at + bytes.Length, slice.BlockOffset + slice.Length);
                    if (end > start) visit(outputOffset + start - slice.BlockOffset, bytes.Slice(start - at, end - start), default);
                    if (at + bytes.Length < slice.BlockOffset + slice.Length) break;
                    outputOffset += slice.Length; first++;
                }
            });
            return;
        }
        byte[] buffer = ArrayPool<byte>.Shared.Rent(65536);
        try
        {
            int outputOffset = 0;
            foreach (var slice in slices)
            {
                for (int at = 0; at < slice.Length;)
                {
                    int count = Math.Min(buffer.Length, slice.Length - at);
                    ReadPayload(slice.Physical, slice.Payload, slice.BlockOffset + at, buffer.AsSpan(0, count));
                    visit(outputOffset + at, buffer.AsSpan(0, count), default); at += count;
                }
                outputOffset += slice.Length;
            }
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }

    private void WriteFrame(Stream output, CompactionFrame frame)
    {
        Span<byte> header = stackalloc byte[FrameHeaderBytes];
        WriteHeader(header, frame.Metadata.Length, frame.Payload);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        output.Write(header); output.Write(frame.Metadata);
        hash.AppendData(header); hash.AppendData(frame.Metadata);
        if (frame.Encoded is { } encoded)
        { output.Write(encoded); hash.AppendData(encoded); }
        else if (frame.Payload.Encoded)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(65536);
            try
            {
                // Retaining a complete frame preserves its exact encoded bytes.
                long at = frame.Slices[0].Physical; int remaining = frame.Payload.StoredBytes;
                while (remaining > 0)
                {
                    int count = Math.Min(buffer.Length, remaining);
                    ReadExactly(at, buffer.AsSpan(0, count));
                    output.Write(buffer.AsSpan(0, count)); hash.AppendData(buffer.AsSpan(0, count));
                    at += count; remaining -= count;
                }
            }
            finally { ArrayPool<byte>.Shared.Return(buffer); }
        }
        else if (frame.Slices.Length != 0)
            VisitSlices(frame.Slices, (_, bytes, _) => { output.Write(bytes); hash.AppendData(bytes); });
        output.Write(hash.GetHashAndReset());
    }
}
