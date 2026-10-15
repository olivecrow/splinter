using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class JournalDeltaStore
{
    public const long DefaultBoundedLogBytes = 16L * 1024 * 1024;
    public const int DefaultBoundedLogFrames = 4096;
    private const string BoundedGuardName = "delta.journal.bounded.lock";
    private const string BoundedInitializationName = "bounded-initialization.json";
    private const string BoundedIntentName = "bounded-checkpoint.json";
    private const string BoundedNextName = "delta.journal.bounded-next";
    private const string BoundedPreviousName = "delta.journal.bounded-previous";
    private sealed record BoundedLogSettings(int Version, string IdentityHash, long ByteLimit, int FrameLimit, string Generation);
    private sealed record BoundedInitialization(int Version, OverlayIdentity Identity, BoundedLogSettings Settings);
    private sealed record BoundedCheckpointIntent(int Version, OverlayIdentity Identity, string Attempt,
        SnapshotDeltaSeal Before, string StateHash, BoundedLogSettings Next, SnapshotDeltaSeal? After = null);
    private static readonly JsonSerializerOptions BoundedJson = new()
    { RespectRequiredConstructorParameters = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    private BoundedLogSettings? boundedSettings;
    // Lookup/List may cache immutable base nodes without recording a mutation.
    // Checkpoint identity must depend only on replayable nodes, not which files
    // happened to be observed in this provider process. The live cache remains
    // intact so existing open IDs continue to work across a checkpoint.
    private readonly HashSet<Guid> boundedDurableNodes = [];
    private FileStream? boundedWriterLock;
    private CompactionIntent? boundedModeTransition;
    private IJournalDirectoryGuard? boundedTransitionDirectoryGuard;
    private long boundedPrefixBytes, boundedCheckpointCount, boundedCheckpointWrittenBytes, boundedCheckpointTicks;
    private int boundedFrames;
    public string JournalStorageMode => boundedSettings is not null ? "bounded" : latest is not null ? "latest" : "append";
    public long BoundedCheckpointCount => boundedCheckpointCount;
    // Private state-prefix bytes only; intent/control bytes are excluded. All
    // checkpoint candidate and intent durable flushes remain in FlushCount.
    public long BoundedCheckpointWrittenBytes => boundedCheckpointWrittenBytes;
    public long BoundedCheckpointTicks => boundedCheckpointTicks;
    public long? BoundedLogByteLimit => boundedSettings?.ByteLimit;
    public long BoundedLogBytes => boundedSettings is null ? 0 : expectedJournalBytes - boundedPrefixBytes;
    public int BoundedLogFrames => boundedFrames;

    private static string BoundedIdentityHash(OverlayIdentity identity) =>
        Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(identity)));
    private static void RequireBoundedSettings(BoundedLogSettings settings, OverlayIdentity identity)
    {
        if (settings.Version != 1 || settings.IdentityHash != BoundedIdentityHash(identity) ||
            settings.ByteLimit < 4096 || settings.ByteLimit > DefaultBoundedLogBytes || settings.ByteLimit > identity.MaxJournalBytes ||
            settings.FrameLimit != DefaultBoundedLogFrames || !Guid.TryParseExact(settings.Generation, "N", out _))
            throw LatestJournalStorage.Corrupt("The bounded journal mode, owner or resource limit differs.");
    }

    // Classification reads and authenticates the complete first frame. It does
    // not turn an incomplete/corrupt mode marker into an append-mode fallback.
    public static bool IsBoundedJournal(string path)
    {
        if (!File.Exists(path)) return false;
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (input.Length < FrameHeaderBytes) return false;
        byte[] header = new byte[FrameHeaderBytes]; input.ReadExactly(header);
        if (!header.AsSpan(0, 4).SequenceEqual("SJR2"u8)) return false;
        int length = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4));
        int stored = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(8));
        int decoded = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(12));
        if (length is < 1 or > MaxMetadataBytes || stored < 0 || decoded < 0)
            throw LatestJournalStorage.Corrupt("The journal's first frame has invalid bounds.");
        if (input.Length < FrameHeaderBytes + (long)length) return false;
        byte[] metadata = new byte[length]; input.ReadExactly(metadata);
        Mutation? mutation;
        try { mutation = JsonSerializer.Deserialize<Mutation>(metadata); }
        catch (JsonException) { throw LatestJournalStorage.Corrupt("The journal's first frame metadata is corrupt."); }
        if (mutation?.Operation != "state-bounded") return false;
        if (stored != 0 || decoded != 0 || mutation.Sequence != 0 || mutation.Bounded is null ||
            input.Length < FrameHeaderBytes + (long)length + FrameChecksumBytes)
            throw LatestJournalStorage.Corrupt("The bounded mode frame is incomplete or invalid.");
        byte[] expected = new byte[FrameChecksumBytes]; input.ReadExactly(expected);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(header); hash.AppendData(metadata);
        if (!CryptographicOperations.FixedTimeEquals(expected, hash.GetHashAndReset()))
            throw LatestJournalStorage.Corrupt("The bounded mode checksum differs.");
        var identity = ReadBoundedJson<OverlayIdentity>(Path.Combine(Path.GetDirectoryName(path)!, "identity.json"));
        RequireBoundedSettings(mutation.Bounded, identity);
        return true;
    }

    private void PrepareBoundedOpen(string directory, string journalName, long? requested)
    {
        if (journalName != "delta.journal") return;
        string path = Path.Combine(directory, journalName), identityPath = Path.Combine(directory, "identity.json");
        string initialization = Path.Combine(directory, BoundedInitializationName), guard = Path.Combine(directory, BoundedGuardName);
        bool initializing = File.Exists(initialization) || File.Exists(initialization + ".pending");
        bool latestInitializing = File.Exists(InitializationPath(directory)) || File.Exists(InitializationPath(directory) + ".pending");
        bool selected = !readOnly && requested is not null && !File.Exists(path) && !File.Exists(identityPath) && !initializing && !latestInitializing;
        if (selected && (requested < 4096 || requested > DefaultBoundedLogBytes))
            throw new ArgumentException("The bounded append byte limit must be between 4096 and 16777216.");
        if (readOnly && initializing) throw LatestJournalStorage.Corrupt("Preserve an unfinished bounded initialization.");
        if (selected || initializing)
        {
            InitializeBounded(directory, selected ? Math.Min(requested!.Value, Identity.MaxJournalBytes) : null);
        }
        bool marked = File.Exists(guard), pending = File.Exists(Path.Combine(directory, BoundedIntentName)) || File.Exists(Path.Combine(directory, BoundedIntentName) + ".pending");
        bool bounded = IsBoundedJournal(path);
        if (!marked && !pending && !bounded) return;
        // Explicit compaction publishes the new format, reconnects its provider,
        // and only then retires the previous generation. Bridge those two writer
        // lifetimes using the old marker until the new format's own writer is
        // fully admitted. A mere leftover marker never authorizes conversion.
        if (marked && !pending && !bounded && File.Exists(Path.Combine(directory, IntentName)))
        {
            PrepareBoundedModeTransition(directory);
            return;
        }
        WorkspaceStore.EnsurePlainPath(guard);
        boundedWriterLock ??= new FileStream(guard, FileMode.Open, readOnly ? FileAccess.Read : FileAccess.ReadWrite, FileShare.Read, 1);
        if (boundedWriterLock.Length != 0) throw LatestJournalStorage.Corrupt("The bounded writer guard changed.");
        PinBoundedDirectory(directory);
        if (pending)
        {
            if (readOnly) throw LatestJournalStorage.Corrupt("Recover the pending bounded checkpoint with its exact writable owner first.");
            RecoverBoundedCheckpoint(directory);
        }
        RequireBoundedIdle(path);
        if (!IsBoundedJournal(path)) throw LatestJournalStorage.Corrupt("A bounded owner has lost its mode frame.");
    }

    private void PrepareBoundedModeTransition(string directory)
    {
        if (readOnly) throw LatestJournalStorage.Corrupt("Reconnect the explicitly converted journal's writer before reading it.");
        string guard = Path.Combine(directory, BoundedGuardName);
        WorkspaceStore.EnsurePlainPath(guard);
        boundedWriterLock = new FileStream(guard, FileMode.Open, FileAccess.ReadWrite, FileShare.Read | FileShare.Delete, 1);
        if (boundedWriterLock.Length != 0) throw LatestJournalStorage.Corrupt("The previous bounded writer guard changed.");
        boundedTransitionDirectoryGuard = directoryGuardFactory?.Invoke(directory);
        if (boundedTransitionDirectoryGuard is not null && !WorkspaceStore.SamePath(boundedTransitionDirectoryGuard.DirectoryPath, directory))
            throw new WorkspaceException("UnsafePath", "The bounded mode-transition directory proof belongs to another root.");
        boundedModeTransition = RequireBoundedModeTransition(directory, directoryGuardFactory, Identity);
    }

    // Static seal/inventory readers also run between publication and provider
    // reconnect. Admit only that exact converted generation; an intent filename
    // by itself must never excuse a lost bounded mode marker.
    private static CompactionIntent RequireBoundedModeTransition(string directory,
        Func<string, IJournalDirectoryGuard>? directoryGuardFactory = null, OverlayIdentity? expectedIdentity = null)
    {
        RequireBoundedIdle(Path.Combine(directory, "delta.journal"));
        var identity = ReadBoundedJson<OverlayIdentity>(Path.Combine(directory, "identity.json"));
        if (expectedIdentity is not null && JsonSerializer.Serialize(identity) != JsonSerializer.Serialize(expectedIdentity))
            throw LatestJournalStorage.Corrupt("The bounded conversion identity changed.");
        var intent = ReadIntent(Path.Combine(directory, IntentName));
        string current = Path.Combine(directory, "delta.journal"), previous = Path.Combine(directory, BackupName);
        if (intent is null || intent.Version != 3 || intent.WorkspaceId != identity.Id || intent.SnapshotId != identity.Snapshot.Id ||
            !Guid.TryParseExact(intent.AttemptId, "N", out _) || intent.Bounded is not null || intent.Candidate is not { } after ||
            after.DurableSequence != intent.Before.DurableSequence ||
            File.Exists(Path.Combine(directory, CandidateName)) || File.Exists(Path.Combine(directory, IntentName) + ".tmp") ||
            !IsBoundedJournal(previous) || BoundedFileSeal(previous, intent.Before.DurableSequence) != intent.Before ||
            IsBoundedJournal(current) || LatestJournalStorage.IsLatest(current) != intent.RevertBaseRanges ||
            (intent.RevertBaseRanges ? LatestJournalStorage.Seal(current, directoryGuardFactory) :
                BoundedFileSeal(current, after.DurableSequence)) != after)
            throw LatestJournalStorage.Corrupt("The bounded mode conversion lacks its exact owner and published/previous generation proof.");
        return intent;
    }

    private void CompleteBoundedModeTransition()
    {
        if (boundedModeTransition is not { } intent) return;
        if (boundedSettings is not null || sequence != intent.Candidate!.DurableSequence ||
            StateHash() != intent.ExpectedStateHash || SealCurrent() != intent.Candidate)
            throw LatestJournalStorage.Corrupt("The reconnected converted writer differs from its fixed state proof.");
        string previous = Path.Combine(Path.GetDirectoryName(journal.Name)!, BackupName);
        if (BoundedFileSeal(previous, intent.Before.DurableSequence) != intent.Before)
            throw LatestJournalStorage.Corrupt("The previous bounded generation changed during writer handoff.");
        // The append file or latest writer guard is already exclusively owned.
        // Release the no-delete directory pin while the old marker's RW fence
        // still blocks competing providers, then delete that exact pinned marker.
        boundedTransitionDirectoryGuard?.Dispose(); boundedTransitionDirectoryGuard = null;
        File.Delete(Path.Combine(Path.GetDirectoryName(journal.Name)!, BoundedGuardName));
        boundedWriterLock!.Dispose(); boundedWriterLock = null;
        boundedModeTransition = null;
    }

    private void InitializeBounded(string directory, long? selectedLimit)
    {
        string intentPath = Path.Combine(directory, BoundedInitializationName);
        string journalPath = Path.Combine(directory, "delta.journal"), identityPath = Path.Combine(directory, "identity.json");
        BoundedInitialization intent;
        if (File.Exists(intentPath) || File.Exists(intentPath + ".pending"))
        {
            if (File.Exists(intentPath) && File.Exists(intentPath + ".pending"))
                throw LatestJournalStorage.Corrupt("Two bounded initialization intents remain.");
            intent = ReadBoundedJson<BoundedInitialization>(File.Exists(intentPath) ? intentPath : intentPath + ".pending");
        }
        else
        {
            if (selectedLimit is null || Directory.EnumerateFileSystemEntries(directory).Any())
                throw LatestJournalStorage.Corrupt("A new bounded journal contains unowned entries.");
            intent = new(1, Identity, new(1, BoundedIdentityHash(Identity), selectedLimit.Value, DefaultBoundedLogFrames, Guid.NewGuid().ToString("N")));
            WriteBoundedNew(intentPath + ".pending", intent);
            Visit("bounded.initialize.intent-flushed", "initialize", 0, 0, 0, 0);
        }
        if (intent.Version != 1 || JsonSerializer.Serialize(intent.Identity) != JsonSerializer.Serialize(Identity))
            throw LatestJournalStorage.Corrupt("The bounded initialization belongs to another identity.");
        RequireBoundedSettings(intent.Settings, Identity);
        if (!File.Exists(intentPath)) File.Move(intentPath + ".pending", intentPath);
        string guard = Path.Combine(directory, BoundedGuardName); WorkspaceStore.EnsurePlainPath(guard);
        boundedWriterLock = new FileStream(guard, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, 1);
        if (boundedWriterLock.Length != 0) throw LatestJournalStorage.Corrupt("The bounded initialization guard changed.");
        PinBoundedDirectory(directory);
        var allowed = new HashSet<string>(StringComparer.Ordinal) { BoundedInitializationName, BoundedGuardName,
            "identity.json", "identity.json.initializing", "delta.journal", "delta.journal.initializing" };
        foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
            if (!allowed.Contains(Path.GetFileName(entry)) || Directory.Exists(entry)) throw LatestJournalStorage.Corrupt("Unowned bounded initialization entry.");
        PublishSeed(identityPath, JsonSerializer.SerializeToUtf8Bytes(Identity));
        Visit("bounded.initialize.identity", "initialize", 0, 0, 0, 0);
        using var seed = new MemoryStream();
        long length = WriteStateFrames([], seed, null, intent.Settings);
        if (length > Identity.MaxJournalBytes) throw new WorkspaceException("DeltaFull", "The bounded seed exceeds the journal budget.");
        PublishSeed(journalPath, seed.ToArray());
        Visit("bounded.initialize.published", "initialize", 0, 0, 0, 0);
        File.Delete(intentPath);
    }

    private void PinBoundedDirectory(string directory)
    {
        latestDirectoryGuard ??= directoryGuardFactory?.Invoke(directory);
        if (latestDirectoryGuard is not null && !WorkspaceStore.SamePath(latestDirectoryGuard.DirectoryPath, directory))
            throw new WorkspaceException("UnsafePath", "The bounded journal directory proof belongs to another generation.");
    }

    private static T ReadBoundedJson<T>(string path)
    {
        WorkspaceStore.EnsurePlainPath(path);
        if (!File.Exists(path) || new FileInfo(path).Length is < 1 or > 65536)
            throw LatestJournalStorage.Corrupt("A bounded intent or identity is missing or exceeds its limit.");
        try { return JsonSerializer.Deserialize<T>(File.ReadAllBytes(path), BoundedJson) ?? throw LatestJournalStorage.Corrupt("A bounded record is null."); }
        catch (JsonException) { throw LatestJournalStorage.Corrupt("A bounded record is incomplete or corrupt; preserve it."); }
    }
    private static void WriteBoundedNew<T>(string path, T value)
    {
        WorkspaceStore.EnsurePlainPath(path);
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        JsonSerializer.Serialize(file, value, BoundedJson); file.Flush(true);
    }
    private void PublishBoundedIntent(string path, BoundedCheckpointIntent intent)
    {
        string pending = path + ".pending";
        WorkspaceStore.EnsurePlainPath(pending);
        using (var file = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        {
            JsonSerializer.Serialize(file, intent, BoundedJson);
            long began = Stopwatch.GetTimestamp();
            try { file.Flush(true); }
            finally { flushCount++; flushTicks += Stopwatch.GetTimestamp() - began; }
        }
        AtomicFilePublication.Publish(pending, path);
    }
    private static SnapshotDeltaSeal BoundedFileSeal(string path, long sequence)
    {
        WorkspaceStore.EnsurePlainPath(path);
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        long length = file.Length; string hash = Convert.ToHexStringLower(SHA256.HashData(file));
        if (file.Length != length) throw LatestJournalStorage.Corrupt("A bounded generation changed while hashing.");
        return new(sequence, length, hash);
    }
    internal static void RequireBoundedIdle(string journalPath)
    {
        if (Path.GetFileName(journalPath) != "delta.journal") return;
        string directory = Path.GetDirectoryName(journalPath)!;
        foreach (string name in new[] { BoundedInitializationName, BoundedInitializationName + ".pending", BoundedIntentName,
            BoundedIntentName + ".pending", BoundedNextName, BoundedPreviousName, "journal-initialization.json",
            "journal-initialization.json.pending" })
            if (Path.Exists(Path.Combine(directory, name))) throw LatestJournalStorage.Corrupt("A bounded checkpoint or unexpected generation remains; preserve it.");
        if (File.Exists(Path.Combine(directory, "delta.journal.writer.lock")) && !File.Exists(Path.Combine(directory, IntentName)))
            throw LatestJournalStorage.Corrupt("An unexpected latest-mode guard accompanies the bounded journal.");
        string guard = Path.Combine(directory, BoundedGuardName); WorkspaceStore.EnsurePlainPath(guard);
        if (!File.Exists(guard) || new FileInfo(guard).Length != 0) throw LatestJournalStorage.Corrupt("The bounded owner guard is missing or changed.");
    }

    private IEnumerable<Node> BoundedNodes() => nodes.Values.Where(node =>
        node.Entry.Path.Length == 0 || boundedDurableNodes.Contains(node.Entry.Id));
    private List<LiveRange> BoundedLiveRanges() => BoundedNodes().SelectMany(node => node.Extents.Select(e =>
        new LiveRange(node.Entry.Id, e.JournalOffset, e.Length, e.Start, e.Payload, e.BlockOffset))).ToList();
    private string BoundedStateHash()
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        void Add(object value)
        {
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value); Span<byte> size = stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(size, bytes.Length); hash.AppendData(size); hash.AppendData(bytes);
        }
        Add(sequence);
        // Include every recorded identity, including deleted-but-open private
        // state. Purely observed base nodes have no journal representation and
        // are recovered lazily from their immutable BasePath after a restart.
        foreach (var node in BoundedNodes().OrderBy(n => n.Entry.Id))
        {
            Add(node.Entry);
            foreach (var extent in node.Extents)
            { Add(new { extent.Start, extent.Length }); VisitExtent(extent, (_, bytes, _) => hash.AppendData(bytes)); }
        }
        foreach (string value in hidden.Order(pathPolicy.Comparer)) Add(value);
        foreach (var pair in pathChanges.OrderBy(p => p.Key, pathPolicy.Comparer)) Add(pair);
        foreach (string value in projectionUnsafe.Order(pathPolicy.Comparer)) Add(value);
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private void EnsureBoundedAppendCapacity(long frameBytes)
    {
        if (boundedSettings is null) return;
        if (frameBytes > boundedSettings.ByteLimit)
            throw new WorkspaceException("DeltaFull", "One mutation exceeds this journal's persisted bounded append limit.");
        if (journal.Length - boundedPrefixBytes <= boundedSettings.ByteLimit - frameBytes &&
            boundedFrames < boundedSettings.FrameLimit && journal.Length <= Identity.MaxJournalBytes - frameBytes) return;
        CheckpointBounded(frameBytes);
    }

    private void CheckpointBounded(long nextFrameBytes)
    {
        long checkpointStarted = Stopwatch.GetTimestamp();
        string directory = Path.GetDirectoryName(journal.Name)!, current = journal.Name;
        string candidate = Path.Combine(directory, BoundedNextName), previous = Path.Combine(directory, BoundedPreviousName);
        string intentPath = Path.Combine(directory, BoundedIntentName);
        RequireBoundedIdle(current);
        var next = boundedSettings! with { Generation = Guid.NewGuid().ToString("N") };
        var live = BoundedLiveRanges();
        long estimated = WriteStateFrames(live, null, null, next);
        if (estimated > Identity.MaxJournalBytes - nextFrameBytes)
            throw new WorkspaceException("DeltaFull", "The current private state and next mutation exceed the journal budget.");
        if (new DriveInfo(Path.GetPathRoot(Path.GetFullPath(current))!).AvailableFreeSpace < checked(estimated + 2L * 1024 * 1024 * 1024))
            throw new WorkspaceException("CompactionHeadroomRequired", "A bounded checkpoint requires space for its private-state generation and 2 GiB headroom.");
        var before = BoundedFileSeal(current, sequence);
        var intent = new BoundedCheckpointIntent(1, Identity, next.Generation, before, BoundedStateHash(), next);
        try
        {
            PublishBoundedIntent(intentPath, intent);
            Visit("bounded.checkpoint.intent", "checkpoint", sequence, 0, 0, 0);
            using (var output = new FileStream(candidate, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 65536))
            {
                long actual = WriteStateFrames(live, output, null, next);
                if (actual != estimated || output.Length != actual) throw LatestJournalStorage.Corrupt("The bounded candidate estimate changed.");
                boundedCheckpointWrittenBytes += actual;
                long began = Stopwatch.GetTimestamp(); try { output.Flush(true); }
                finally { flushCount++; flushTicks += Stopwatch.GetTimestamp() - began; }
            }
            Visit("bounded.checkpoint.candidate-flushed", "checkpoint", sequence, 0, 0, 0);
            using (var check = new JournalDeltaStore(directory, Identity, snapshot, BoundedNextName, true, null))
            {
                if (check.boundedSettings != next || check.sequence != sequence || check.BoundedStateHash() != intent.StateHash)
                    throw LatestJournalStorage.Corrupt("The bounded candidate reconstructs another state.");
                intent = intent with { After = BoundedFileSeal(candidate, sequence) };
            }
            if (BoundedFileSeal(current, sequence) != before) throw LatestJournalStorage.Corrupt("The bounded source changed during checkpoint.");
            PublishBoundedIntent(intentPath, intent);
            Visit("bounded.checkpoint.prepared", "checkpoint", sequence, 0, 0, 0);
            journal.Dispose();
            File.Replace(candidate, current, previous);
            Visit("bounded.checkpoint.published", "checkpoint", sequence, 0, 0, 0);
            journal = new FileStream(current, FileMode.Open, FileAccess.ReadWrite, FileShare.Read | FileShare.Delete, 4096, FileOptions.RandomAccess);
            using (var check = new JournalDeltaStore(directory, Identity, snapshot, "delta.journal", true, null, skipBoundedPreparation: true))
            {
                if (check.boundedSettings != next || check.BoundedStateHash() != intent.StateHash || BoundedFileSeal(current, sequence) != intent.After)
                    throw LatestJournalStorage.Corrupt("The published bounded generation differs.");
                foreach (var node in nodes.Values)
                {
                    if (check.nodes.TryGetValue(node.Entry.Id, out var preserved)) node.Extents = preserved.Extents;
                    else if (node.Extents.Count != 0 || boundedDurableNodes.Contains(node.Entry.Id))
                        throw LatestJournalStorage.Corrupt("A recorded private node disappeared during bounded checkpoint.");
                }
                payloadBytes = check.payloadBytes; logicalPayloadBytes = check.logicalPayloadBytes; extentCount = check.extentCount;
                boundedSettings = next; boundedPrefixBytes = check.boundedPrefixBytes; boundedFrames = 0; hasStatePrefix = true;
                expectedJournalBytes = journal.Length; ClearPayloadCache();
            }
            Visit("bounded.checkpoint.reopened", "checkpoint", sequence, 0, 0, 0);
            if (BoundedFileSeal(previous, sequence) != before) throw LatestJournalStorage.Corrupt("The previous bounded generation changed.");
            File.Delete(previous);
            Visit("bounded.checkpoint.previous-deleted", "checkpoint", sequence, 0, 0, 0);
            File.Delete(intentPath); boundedCheckpointCount++;
            Visit("bounded.checkpoint.completed", "checkpoint", sequence, 0, 0, 0);
        }
        catch { Poison(); throw; }
        finally { boundedCheckpointTicks += Stopwatch.GetTimestamp() - checkpointStarted; }
    }

    private void RecoverBoundedCheckpoint(string directory)
    {
        string intentPath = Path.Combine(directory, BoundedIntentName), pending = intentPath + ".pending";
        string current = Path.Combine(directory, "delta.journal"), candidate = Path.Combine(directory, BoundedNextName), previous = Path.Combine(directory, BoundedPreviousName);
        BoundedCheckpointIntent? published = File.Exists(intentPath) ? ReadBoundedJson<BoundedCheckpointIntent>(intentPath) : null;
        BoundedCheckpointIntent? staged = File.Exists(pending) ? ReadBoundedJson<BoundedCheckpointIntent>(pending) : null;
        if (published is not null && staged is not null && (published.After is not null || staged.After is null || published != (staged with { After = null })))
            throw LatestJournalStorage.Corrupt("Competing bounded checkpoint intents remain.");
        var intent = staged ?? published ?? throw LatestJournalStorage.Corrupt("The bounded checkpoint intent is missing.");
        if (intent.Version != 1 || JsonSerializer.Serialize(intent.Identity) != JsonSerializer.Serialize(Identity) ||
            intent.Attempt != intent.Next.Generation || !ValidSeal(intent.Before) || intent.After is { } seal && !ValidSeal(seal) ||
            intent.After is { } selected && selected.DurableSequence != intent.Before.DurableSequence ||
            intent.StateHash.Length != 64 || !intent.StateHash.All(char.IsAsciiHexDigit))
            throw LatestJournalStorage.Corrupt("The bounded checkpoint intent has another owner or format.");
        RequireBoundedSettings(intent.Next, Identity);
        bool oldCurrent = File.Exists(current) && BoundedFileSeal(current, intent.Before.DurableSequence) == intent.Before;
        bool newCurrent = intent.After is { } after && File.Exists(current) && BoundedFileSeal(current, after.DurableSequence) == after;
        if (oldCurrent)
        {
            if (File.Exists(previous)) throw LatestJournalStorage.Corrupt("An old checkpoint has an unexpected previous generation.");
            using var proof = new JournalDeltaStore(directory, Identity, snapshot, "delta.journal", true, null, skipBoundedPreparation: true);
            if (proof.sequence != intent.Before.DurableSequence || proof.BoundedStateHash() != intent.StateHash || proof.boundedSettings is null ||
                proof.boundedSettings.Generation == intent.Next.Generation ||
                proof.boundedSettings.ByteLimit != intent.Next.ByteLimit || proof.boundedSettings.FrameLimit != intent.Next.FrameLimit)
                throw LatestJournalStorage.Corrupt("The old bounded state differs from its intent.");
            if (File.Exists(candidate))
            {
                WorkspaceStore.EnsurePlainPath(candidate);
                if (intent.After is { } expected && BoundedFileSeal(candidate, expected.DurableSequence) != expected)
                    throw LatestJournalStorage.Corrupt("The prepared bounded candidate changed.");
                // A staging filename alone does not prove ownership. Before a
                // prepared seal exists, authenticate every surviving byte as a
                // prefix of this exact owner's deterministic current-state
                // candidate. This covers interrupted writes while preserving
                // complete corruption and another generation's content.
                if (intent.After is null)
                {
                    using var compare = new BoundedCandidatePrefixVerifier(candidate);
                    proof.WriteStateFrames(proof.BoundedLiveRanges(), compare, null, intent.Next);
                    compare.Complete();
                }
                File.Delete(candidate);
            }
        }
        else if (newCurrent)
        {
            if (File.Exists(candidate)) throw LatestJournalStorage.Corrupt("A published bounded checkpoint still has its candidate.");
            using var proof = new JournalDeltaStore(directory, Identity, snapshot, "delta.journal", true, null, skipBoundedPreparation: true);
            if (proof.sequence != intent.Before.DurableSequence || proof.boundedSettings != intent.Next || proof.BoundedStateHash() != intent.StateHash)
                throw LatestJournalStorage.Corrupt("The published bounded state differs from its intent.");
            if (File.Exists(previous))
            {
                if (BoundedFileSeal(previous, intent.Before.DurableSequence) != intent.Before)
                    throw LatestJournalStorage.Corrupt("The retired bounded generation differs from its intent.");
                File.Delete(previous);
            }
        }
        else throw LatestJournalStorage.Corrupt("Neither bounded checkpoint generation matches; preserve all files.");
        if (File.Exists(pending)) File.Delete(pending);
        if (File.Exists(intentPath)) File.Delete(intentPath);
    }

    private sealed class BoundedCandidatePrefixVerifier(string path) : Stream
    {
        private readonly FileStream input = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        private readonly byte[] scratch = new byte[65536];
        private long expectedBytes;
        public override void Write(ReadOnlySpan<byte> expected)
        {
            expectedBytes = checked(expectedBytes + expected.Length);
            while (!expected.IsEmpty && input.Position < input.Length)
            {
                int count = (int)Math.Min(Math.Min(expected.Length, scratch.Length), input.Length - input.Position);
                input.ReadExactly(scratch.AsSpan(0, count));
                if (!expected[..count].SequenceEqual(scratch.AsSpan(0, count)))
                    throw LatestJournalStorage.Corrupt("An unfinished bounded candidate differs from its owner and exact state prefix.");
                expected = expected[count..];
            }
        }
        public void Complete()
        { if (input.Position != input.Length || input.Length > expectedBytes) throw LatestJournalStorage.Corrupt("An unfinished bounded candidate has unowned trailing bytes."); }
        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => expectedBytes;
        public override long Position { get => expectedBytes; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) input.Dispose(); base.Dispose(disposing); }
    }
}
