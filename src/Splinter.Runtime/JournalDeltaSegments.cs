using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class JournalDeltaStore
{
    private JournalSegmentStorage? segments;
    private JournalReadOnlyReplayScope? readOnlyReplay;
    private MetadataTreeRoot? segmentRoot;
    private MetadataTreeRoot? adoptionRoot;
    private MetadataTreeRoot? captureRoot;
    private MetadataTreeRoot? packingRoot;
    private long nextDataSegment = -2, dataSegmentBytes, dataPayloadBytes;
    private int segmentFrames;
    private long captureProtectionGeneration;
    private string sourceMutationHash = "";
    private long sourceMutationBytes;
    private long replayedActivePayloadBytes;
    private long segmentAppendLimit = DefaultBoundedLogBytes;
    private FileStream? segmentRead;
    private long segmentReadId = -1;
    private const int RetainedSegmentReaderLimit = 4;
    private List<VerifiedSegmentReader>? retainedSegmentReaders;

    private sealed class VerifiedSegmentReader(JournalSegment descriptor, MetadataTreeRoot root, FileStream input)
    {
        internal readonly JournalSegment Descriptor = descriptor;
        internal readonly FileStream Input = input;
        internal MetadataTreeRoot Root = root;
    }

    private void OpenSegmented(string directory, string identityPath, string journalPath)
    {
        sourceMutationHash = PagedOwnerHash(Identity);
        bool existing = File.Exists(Path.Combine(directory, "segment-state.json")) || File.Exists(Path.Combine(directory, "segment-roll.json"));
        if (!File.Exists(identityPath))
        {
            if (readOnly || File.Exists(journalPath)) throw new WorkspaceException("OverlayIdentityMissing", "Preserve an unidentified journal without migration.");
            using var identityFile = new FileStream(identityPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            JsonSerializer.Serialize(identityFile, Identity); identityFile.Flush(true);
        }
        if (!existing)
        {
            if (readOnly || File.Exists(journalPath)) throw new WorkspaceException("OverlayFormatUnsupported", "Preserve an earlier overlay journal without conversion.");
            InitializePagedIndex(directory, true);
            AddBaseNode(snapshot.Stat("") ?? throw new WorkspaceException("SnapshotMissing", "Snapshot root is unavailable."), "");
            pagedIndex!.Flush();
        }
        try
        {
            segments = new(directory, Identity, existing ? null : FrozenJournalRoots(), sourceFilePins, readOnly: readOnly);
            latestDirectoryGuard = directoryGuardFactory?.Invoke(directory);
            if (latestDirectoryGuard is not null && !WorkspaceStore.SamePath(latestDirectoryGuard.DirectoryPath, directory))
                throw new WorkspaceException("UnsafePath", "The segmented journal ancestry proof belongs to another root.");
            if (existing)
            {
                if (readOnly)
                {
                    readOnlyReplay = JournalReadOnlyReplayScope.Create(segments.State.Roots.Directory,
                        Path.GetDirectoryName(Path.GetFullPath(directory))!, Identity, sourceFilePins);
                    InitializePagedIndex(directory, true, segments.State.Roots, readOnlyReplay.Store);
                }
                else InitializePagedIndex(directory, true, segments.State.Roots);
            }
            // The persisted state and each capture control are roots protecting
            // this store. A replay-derived temporary index is never substituted.
            pagedIndexCaptured = true;
            journal = segments.Active;
            long started = Stopwatch.GetTimestamp(); replaying = true; Replay(); replaying = false;
            segments.SetActivePayloadBytes(replayedActivePayloadBytes);
            segments.ResetActiveHash(); replayTicks = Stopwatch.GetTimestamp() - started;
            expectedJournalBytes = journal.Length;
            if (!readOnly) RestoreCaptureSegmentRoot();
        }
        catch
        {
            segments?.Dispose(); segments = null; latestDirectoryGuard?.Dispose(); latestDirectoryGuard = null;
            if (readOnlyReplay is { } replay) replay.Dispose(); else pagedIndex?.Dispose();
            throw;
        }
    }
    private JournalIndexRoots FrozenJournalRoots() => new(pagedIndexDirectory!, NodeRoot!, PathRoot!, HiddenRoot!, ChangeRoot!,
        pathComponents!.Root, ((PagedSet<Guid>)boundedDurableNodes).Root, segmentRoot!, sequence, payloadBytes, logicalPayloadBytes, extentCount, adoptionRoot,
        sourceMutationHash, sourceMutationBytes, captureRoot, packingRoot, nextDataSegment, dataSegmentBytes, dataPayloadBytes);

    private void EnsureSegmentAppendCapacity(long frameBytes)
    {
        if (segments is null) return;
        if (segmentFrames != 0 && (segmentFrames >= DefaultBoundedLogFrames ||
            journal.Length - JournalSegmentStorage.HeaderBytes > segmentAppendLimit - frameBytes)) RollSegment();
    }
    private void RollSegment()
    {
        if (segments is null || readOnly) throw PagedInvalid();
        FlushPendingWrites();
        long started = Stopwatch.GetTimestamp();
        // The append digest was maintained while accepting each frame. This
        // boundary persists roots and control records; it never reads payload.
        segments.Rotate(sealedSegment =>
        {
            segmentRoot = pagedIndex!.Set(segmentRoot!, sealedSegment.Id.ToString("x16"), EncodeSegment(sealedSegment));
            if (captureSegmentRoot is not null) captureSegmentRoot = pagedIndex.Set(captureSegmentRoot, sealedSegment.Id.ToString("x16"), EncodeSegment(sealedSegment));
            pagedIndex.Flush(); return FrozenJournalRoots();
        });
        journal = segments.Active; expectedJournalBytes = journal.Length; segmentFrames = 0;
        durablyFlushedJournal = journal; durablyFlushedBytes = journal.Length;
        boundedCheckpointCount++; boundedCheckpointWrittenBytes += JournalSegmentStorage.HeaderBytes;
        boundedCheckpointTicks += Stopwatch.GetTimestamp() - started;
    }
    private static byte[] EncodeSegment(JournalSegment value)
    {
        byte[] identity = Encoding.UTF8.GetBytes(value.NativeIdentity ?? "");
        byte[] bytes = new byte[checked(58 + identity.Length)]; BinaryPrimitives.WriteInt64LittleEndian(bytes, value.Id);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(8), value.Length); Convert.FromHexString(value.Hash).CopyTo(bytes, 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(48), checked((ushort)identity.Length));
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(50), value.PayloadBytes); identity.CopyTo(bytes, 58); return bytes;
    }
    private static JournalSegment DecodeSegment(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 58 || BinaryPrimitives.ReadUInt16LittleEndian(bytes[48..]) != bytes.Length - 58) throw PagedInvalid();
        long id = BinaryPrimitives.ReadInt64LittleEndian(bytes), length = BinaryPrimitives.ReadInt64LittleEndian(bytes[8..]);
        if (id == -1 || length < (id >= 0 ? JournalSegmentStorage.HeaderBytes : 1)) throw PagedInvalid();
        long payload = BinaryPrimitives.ReadInt64LittleEndian(bytes[50..]); if (payload < 0 || payload > length) throw PagedInvalid();
        string identity = Encoding.UTF8.GetString(bytes[58..]);
        return new(id, length, Convert.ToHexStringLower(bytes.Slice(16, 32)), identity.Length == 0 ? null : identity, payload);
    }
    internal static JournalSegment ReadCapturedSegment(ReadOnlySpan<byte> bytes) => DecodeSegment(bytes);

    private JournalSourceCapture CaptureSegmentedSource(string destination, string id, string[] managedPaths, bool deferBackingPins)
    {
        long started = Stopwatch.GetTimestamp(), entered = 0; JournalSourceCapture result;
        using (EnterGate(mutation: true))
        {
            entered = Stopwatch.GetTimestamp();
            try
            {
                Check(); if (readOnly || sourceFilePins is null) throw new WorkspaceException("SourceCaptureUnsupported", "Compose durable native capture pins.");
                WorkspaceStore.EnsurePlainPath(destination); string manifest = Path.Combine(destination, "capture.json");
                var frozenRecord = pagedIndex!.Open(captureRoot!).FindEntry(id);
                if (frozenRecord is not null || File.Exists(manifest))
                {
                    result = frozenRecord is null ? SourceCaptureTree.ReadCapture(destination, verifyPagedRoots: false) : LoadFrozenCapture(pagedIndex, frozenRecord);
                    if (result.Id != id || !SourceCaptureTree.SameIdentity(result.Identity, Identity) || !result.ManagedPaths.SequenceEqual(managedPaths))
                        throw new WorkspaceException("SourceCaptureRequestConflict", "Resume the exact frozen capture request.");
                    RepairFrozenCapture(destination, result);
                }
                else
                {
                    if (!CaptureId(id)) throw PagedInvalid();
                    if (Directory.Exists(destination) && Directory.EnumerateFileSystemEntries(destination).Any(path =>
                        !Path.GetFileName(path).StartsWith("readers-", StringComparison.Ordinal) || !path.EndsWith(".lock", StringComparison.Ordinal) || !File.Exists(path) || new FileInfo(path).Length != 0))
                        throw new WorkspaceException("SourceCaptureIncomplete", "Preserve the unfinished capture control.");
                    // Capture completion removes native aliases of these
                    // same files. Retain the original single-reader policy
                    // until every frozen capture has completed.
                    if (retainedSegmentReaders is { Count: > 0 }) CloseSegmentReaders();
                    FlushPendingWrites(); RollSegment();
                    Directory.CreateDirectory(destination); string readerName = "readers-" + Guid.NewGuid().ToString("N") + ".lock";
                    using (var created = new FileStream(Path.Combine(destination, readerName), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read)) created.Flush(true);
                    string readerIdentity = sourceFilePins.Identify(Path.Combine(destination, readerName));
                    var roots = new PagedJournalCapture(pagedIndexDirectory!, NodeRoot!, PathRoot!, HiddenRoot!, ChangeRoot!, pathComponents!.Root, captureSegmentRoot ?? segmentRoot!);
                    result = new(2, id, Identity, sequence, managedPaths.ToArray(), [], [], [], [], "", roots, readerName, readerIdentity);
                    result = result with { Digest = SourceCaptureTree.Digest(result) };
                    var control = StoreFrozenCapture(result);
                    Commit(new(sequence, "capture-freeze", nodes[paths[""]].Entry, FrozenCapture: control), []);
                    Visit("capture.freeze-accepted", "capture", sequence, 0, 0, 0);
                    RepairFrozenCapture(destination, result);
                    Visit("capture.intent-published", "capture", sequence, 0, 0, 0);
                }
            }
            finally { LastSourceCaptureTiming = new(Stopwatch.Frequency, entered - started, Stopwatch.GetTimestamp() - entered); }
        }
        // The durable control protects every fixed root and source segment
        // before this potentially long walk. Restart recovery reads captures
        // before any retirement. No payload or directory walk holds the gate.
        if (deferBackingPins) return result;
        if (File.Exists(Path.Combine(destination, "materialized.json"))) SourceCaptureTree.RequireMaterialized(destination, result);
        else SourceCaptureTree.EnsurePins(destination, result, sourceFilePins!);
        return result;
    }
    private JournalSegment Segment(long id)
    {
        var entry = pagedIndex!.Open(segmentRoot!).Find(id.ToString("x16")) ?? throw PagedInvalid();
        var segment = DecodeSegment(entry); if (segment.Id != id) throw PagedInvalid(); return segment;
    }
    private FileStream SegmentInput(long id)
    {
        if (segments is null || id == segments.ActiveId) return journal;
        if (!readOnly && mutationObserver is null && sourceFilePins is not null && latestDirectoryGuard is not null && captureRoot?.Count == 0)
            return RetainedSegmentInput(id);
        if (segmentReadId != id)
        { segmentRead?.Dispose(); segmentRead = segments.OpenSealed(Segment(id)); segmentReadId = id; }
        return segmentRead!;
    }
    // The owning journal gate serializes use and eviction. These handles
    // deny writes and deletion until their verified bytes leave the cache.
    private FileStream RetainedSegmentInput(long id)
    {
        // A completed capture may leave one legacy handle. Clear it before
        // admitting the bounded native reader set again.
        if (segmentRead is not null) CloseSegmentReaders();
        var readers = retainedSegmentReaders ??= new(RetainedSegmentReaderLimit);
        for (int i = readers.Count - 1; i >= 0; i--)
        {
            var reader = readers[i];
            if (reader.Descriptor.Id != id) continue;
            if (!ReferenceEquals(reader.Root, segmentRoot))
            {
                if (Segment(id) != reader.Descriptor) throw PagedInvalid();
                reader.Root = segmentRoot!;
            }
            if (i != readers.Count - 1)
            { readers.RemoveAt(i); readers.Add(reader); }
            return reader.Input;
        }

        var descriptor = Segment(id);
        if (readers.Count == RetainedSegmentReaderLimit)
        {
            var oldest = readers[0]; readers.RemoveAt(0); oldest.Input.Dispose();
        }
        var input = segments!.OpenSealed(descriptor);
        try
        {
            sourceFilePins!.VerifyPath(input, segments.SegmentPath(id), descriptor.NativeIdentity ?? throw PagedInvalid());
            readers.Add(new(descriptor, segmentRoot!, input));
            return input;
        }
        catch { input.Dispose(); throw; }
    }

    private void CloseSegmentReaders()
    {
        segmentRead?.Dispose(); segmentRead = null; segmentReadId = -1;
        if (retainedSegmentReaders is not { } readers) return;
        foreach (var reader in readers) reader.Input.Dispose();
        readers.Clear();
    }

    private void ReadExtentBytes(Extent extent, long physical, Span<byte> output)
    {
        if (extent.ContentHash is { } hash)
        {
            if (capturedContentReader is null) throw new WorkspaceException("ContentReaderMissing", "Compose the preserved-range reader before opening adopted journal ranges.");
            while (!output.IsEmpty)
            {
                int count = capturedContentReader.Read(hash, extent.RawContent, physical, output);
                if (count <= 0 || count > output.Length) throw PagedInvalid(); physical += count; output = output[count..];
            }
            return;
        }
        if (segments is null || extent.Segment == -1) { ReadExactly(physical, output); return; }
        var input = SegmentInput(extent.Segment);
        while (!output.IsEmpty)
        {
            int count = RandomAccess.Read(input.SafeFileHandle, output, physical);
            if (count <= 0) throw PagedInvalid(); journalReadBytes += count; physical += count; output = output[count..];
        }
    }
    private void RecordAcceptedMutation(Mutation mutation, ReadOnlySpan<byte> checksum, long frameBytes)
    {
        if (segments is null && !segmentShadow || IsAdministrative(mutation.Operation)) return;
        Span<byte> input = stackalloc byte[64]; Convert.FromHexString(sourceMutationHash).CopyTo(input); checksum.CopyTo(input[32..]);
        sourceMutationHash = Convert.ToHexStringLower(SHA256.HashData(input)); sourceMutationBytes = checked(sourceMutationBytes + frameBytes);
    }
    private SnapshotDeltaSeal SegmentedSeal() => new(sequence, sourceMutationBytes, sourceMutationHash);
    public static bool IsSegmentedJournal(string path)
    {
        if (!File.Exists(path)) return false;
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        Span<byte> header = stackalloc byte[4]; return input.Read(header) == 4 && header.SequenceEqual("SJS1"u8);
    }
    internal (SnapshotDeltaSeal Seal, OverlayStorageObservation Storage, double ElapsedMilliseconds) VerifyFlushedSegmentedSeal()
    {
        using (EnterGate())
        {
            if (segments is null || readOnly || sourceFilePins is null || segments.State.Head.NativeIdentity is not { } nativeIdentity)
                throw new WorkspaceException("OverlaySealUnsupported", "A live physical seal requires the owned segmented writer and native pins.");
            // A failed roll may already have closed the old head. Preserve
            // its intent before attempting to flush or inspect that handle.
            segments.RequireNoPendingRoll();
            Flush();
            long started = Stopwatch.GetTimestamp();
            sourceFilePins.VerifyPath(journal, journal.Name, nativeIdentity);
            var seal = InspectSegmentedJournal(journal.Name, sequence, segments.State.Head, sourceFilePins, expectedJournalBytes).Seal;
            if (seal != SegmentedSeal())
                throw new WorkspaceException("DeltaChangedAfterCollection", "The live journal and its physical accepted seal differ; preserve it.");
            double elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var storage = ObserveActivity() ?? throw new WorkspaceException("DeltaReconnectRequired", "Preserve and replay the failed writer before sealing.");
            return (seal, storage, elapsed);
        }
    }

    public static SnapshotDeltaSeal SealSegmentedJournal(string path, long sequence) =>
        InspectSegmentedJournal(path, sequence, null, null, null).Seal;

    internal static (SnapshotDeltaSeal Seal, bool PackingPending) InspectStoppedJournal(string path, long sequence) =>
        InspectSegmentedJournal(path, sequence, null, null, null, inspectPacking: true);

    private static (SnapshotDeltaSeal Seal, bool PackingPending) InspectSegmentedJournal(string path, long sequence,
        JournalSegmentHead? expectedHead, IJournalFilePins? pins, long? expectedLength, bool inspectPacking = false)
    {
        string directory = Path.GetDirectoryName(path)!;
        using var identityInput = File.OpenRead(Path.Combine(directory, "identity.json"));
        var identity = JsonSerializer.Deserialize<OverlayIdentity>(identityInput) ?? throw PagedInvalid();
        using var storage = new JournalSegmentStorage(directory, identity, readOnly: true);
        if (expectedHead is not null && storage.State.Head != expectedHead) throw PagedInvalid();
        bool packingPending = false;
        if (inspectPacking && storage.State.Roots.Packing is { } packing)
        {
            using var index = new PagedMetadataStore(storage.State.Roots.Directory);
            packingPending = index.Open(packing).Find("request") is not null;
        }
        string accepted = storage.State.Roots.SourceMutationHash ?? PagedOwnerHash(identity);
        long bytes = storage.State.Roots.SourceMutationBytes, current = storage.State.Roots.Sequence;
        if (accepted.Length != 64 || accepted.Any(c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f')) || bytes < 0) throw PagedInvalid();
        byte[] header = new byte[FrameHeaderBytes], buffer = new byte[65536], expected = new byte[FrameChecksumBytes], chainBytes = new byte[64];
        var file = storage.Active; file.Position = JournalSegmentStorage.HeaderBytes;
        if (expectedLength is { } length && file.Length != length) throw PagedInvalid();
        if (pins is not null) pins.VerifyPath(file, path, storage.State.Head.NativeIdentity ?? throw PagedInvalid());
        while (file.Position < file.Length)
        {
            if (file.Length - file.Position < FrameHeaderBytes) throw PagedInvalid(); file.ReadExactly(header);
            if (!header.AsSpan(0, 4).SequenceEqual("SJR2"u8)) throw PagedInvalid();
            int metadataBytes = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4));
            var payload = new WorkspaceJournalPayload(BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(8)), BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(12)));
            payload.RequireValid(identity.MaxTransferBytes);
            if (metadataBytes < 1 || metadataBytes > 3 * 6 * 32000 + MaxMetadataBytes ||
                file.Length - file.Position < metadataBytes + (long)payload.StoredBytes + FrameChecksumBytes) throw PagedInvalid();
            byte[] metadata = new byte[metadataBytes]; file.ReadExactly(metadata);
            Mutation mutation;
            try { mutation = ReadCurrentMutation(metadata); }
            catch (JsonException) { throw PagedInvalid(); }
            using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); digest.AppendData(header); digest.AppendData(metadata);
            int remaining = payload.StoredBytes;
            while (remaining > 0) { int count = Math.Min(buffer.Length, remaining); file.ReadExactly(buffer.AsSpan(0, count)); digest.AppendData(buffer.AsSpan(0, count)); remaining -= count; }
            file.ReadExactly(expected); if (!CryptographicOperations.FixedTimeEquals(expected, digest.GetHashAndReset())) throw PagedInvalid();
            bool maintenance = IsAdministrative(mutation.Operation);
            if (mutation.Sequence != (maintenance ? current : checked(current + 1)) || mutation.Operation is not
                ("create" or "write" or "resize" or "metadata" or "delete" or "rename" or "adopt-range" or "forget-unlinked" or "capture-freeze" or "capture-complete" or "packing-state" or "packing-complete" or "packing-preserve" or "relocate-range")) throw PagedInvalid();
            if (!maintenance)
            {
                Convert.FromHexString(accepted).CopyTo(chainBytes, 0); expected.CopyTo(chainBytes, 32);
                accepted = Convert.ToHexStringLower(SHA256.HashData(chainBytes));
                bytes = checked(bytes + FrameHeaderBytes + (long)metadataBytes + payload.StoredBytes + FrameChecksumBytes); current = mutation.Sequence;
            }
            else if (inspectPacking && mutation.Operation is "packing-state" or "packing-complete")
                packingPending = mutation.Operation == "packing-state";
        }
        if (expectedLength is { } fixedLength && file.Length != fixedLength) throw PagedInvalid();
        if (pins is not null) pins.VerifyPath(file, path, storage.State.Head.NativeIdentity ?? throw PagedInvalid());
        if (current != sequence) throw new WorkspaceException("DeltaChangedAfterCollection", "Accepted journal source mutations differ from their fixed sequence.");
        return (new(current, bytes, accepted), packingPending);
    }
    private static bool IsAdministrative(string operation) => operation is "adopt-range" or "forget-unlinked" or "capture-freeze" or "capture-complete" or
        "packing-state" or "packing-complete" or "packing-preserve" or "relocate-range";
}
