using System.Collections;
using System.Text.Json;
using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class JournalDeltaStore
{
    private bool segmentShadow, repackWatching, repackBusy;
    private long? replaySegment;
    private MetadataTreeRoot? repackAdmissions;
    private long activeIndexCursors;
    private sealed record LiveRepack(int Version, string Request, JournalIndexRoots Frozen, string Target,
        long FirstSegment, JournalIndexRoots? Copied = null, string Digest = "");
    private sealed record RepackPoint(long Segment, long Bytes, FileStream Input, MetadataTreeRoot Nodes,
        MetadataTreeRoot Names, MetadataTreeRoot Durable, MetadataTreeRoot Admissions, MetadataTreeRoot Segments, JournalIndexRoots Roots);
    private const string LiveRepackName = "live-repack.json";

    private JournalDeltaStore(OverlayIdentity identity, ISnapshotTree basis, JournalIndexRoots roots,
        IJournalFilePins pins, ICapturedContentReader? content)
    {
        Identity = identity; snapshot = basis; pathPolicy = WorkspacePathPolicy.For(identity.Snapshot);
        sourceFilePins = pins; capturedContentReader = content; concurrentBaseReads = basis.SupportsConcurrentReads;
        decodedPayloadCache = new(JournalDecodedPayloadCache.DefaultBudgetBytes); segmentShadow = true;
        InitializePagedIndex(Path.GetDirectoryName(roots.Directory)!, true, roots);
    }
    private static MetadataTreeRoot[] RepackRoots(JournalIndexRoots value) =>
        [value.Nodes, value.Paths, value.Hidden, value.Changes, value.Names, value.DurableNodes, value.Segments, value.Adoptions ?? new(1, value.Nodes.StoreId, null), value.Captures ?? new(1, value.Nodes.StoreId, null), value.Packing ?? new(1, value.Nodes.StoreId, null)];
    private static JournalIndexRoots RelocatedRoots(JournalIndexRoots original, string directory, MetadataTreeRoot[] roots) => original with
    { Directory = directory, Nodes = roots[0], Paths = roots[1], Hidden = roots[2], Changes = roots[3], Names = roots[4], DurableNodes = roots[5], Segments = roots[6], Adoptions = roots[7], Captures = roots[8], Packing = roots[9] };

    private IEnumerable<T> ProtectIndexCursor<T>(IEnumerable<T> cursor)
    {
        var replayLease = readOnlyReplay?.Retain();
        Interlocked.Increment(ref activeIndexCursors); return new ProtectedIndexCursor<T>(this, cursor, replayLease);
    }
    private sealed class ProtectedIndexCursor<T>(JournalDeltaStore owner, IEnumerable<T> cursor, IDisposable? replayLease) : IEnumerable<T>, IDisposable
    {
        private int released, started;
        ~ProtectedIndexCursor() { try { Release(); } catch { /* Finalization cannot fail a process. */ } }
        private void Release()
        {
            if (Interlocked.Exchange(ref released, 1) != 0) return;
            lock (owner.gate) { Interlocked.Decrement(ref owner.activeIndexCursors); Monitor.PulseAll(owner.gate); }
            replayLease?.Dispose();
        }
        public IEnumerator<T> GetEnumerator()
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref released) != 0, this);
            if (Interlocked.Exchange(ref started, 1) != 0) throw new InvalidOperationException("A fixed journal cursor is consumed once.");
            return Read().GetEnumerator();
        }
        private IEnumerable<T> Read()
        { try { foreach (var value in cursor) yield return value; } finally { Release(); GC.SuppressFinalize(this); } }
        public void Dispose() { Release(); GC.SuppressFinalize(this); }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    // Copy and replay run outside the mutation gate. The publication boundary
    // accepts only an already caught-up shadow and persists its fixed roots.
    // A held directory cursor postpones switching and retains its old store.
    public void CollectLiveMetadata(bool force = false, CancellationToken ct = default, Action<string>? boundary = null)
    {
        lock (storageMaintenanceGate) CollectLiveMetadataCore(force, ct, boundary);
    }
    private void CollectLiveMetadataCore(bool force, CancellationToken ct, Action<string>? boundary)
    {
        if (segments is null || sourceFilePins is null || readOnly) return;
        string delta = Path.GetDirectoryName(journal.Name)!, intentPath = Path.Combine(delta, LiveRepackName);
        string observedDirectory; long activeAllowance;
        using (EnterGate())
        {
            // A fixed directory cursor prevents publication. Preparing or
            // replaying the initial shadow again cannot advance that boundary;
            // leave an existing intent intact until its readers drain.
            Check(); if (repackBusy || activeIndexCursors != 0 || PendingPacking() is not null || File.Exists(Path.Combine(delta, SegmentCollectionName))) return; observedDirectory = pagedIndexDirectory!;
            long count = NodeRoot!.Count;
            foreach (long additional in new[] { ChangeRoot!.Count, HiddenRoot!.Count, extentCount })
                count = count > long.MaxValue - additional ? long.MaxValue : count + additional;
            activeAllowance = Math.Min(long.MaxValue / 2048, count) * 2048;
        }
        if (!force && !File.Exists(intentPath))
        {
            long bytes = 0;
            foreach (string file in Directory.EnumerateFiles(observedDirectory, "pages-*.bin")) bytes = checked(bytes + new FileInfo(file).Length);
            if (bytes < Math.Max(64L * 1024 * 1024, activeAllowance)) return;
        }
        LiveRepack plan; bool alreadyPublished = false;
        using (EnterGate(mutation: true))
        {
            Check(); if (repackBusy || activeIndexCursors != 0 || !WorkspaceStore.SamePath(observedDirectory, pagedIndexDirectory!)) return;
            repackBusy = true;
            if (File.Exists(intentPath))
            {
                plan = ReadLiveRepack(intentPath);
                if (WorkspaceStore.SamePath(segments.State.Roots.Directory, plan.Target))
                    alreadyPublished = true;
                else if (!WorkspaceStore.SamePath(plan.Frozen.Directory, pagedIndexDirectory!)) throw PagedInvalid();
            }
            else
            {
                FlushPendingWrites(); RollSegment();
                plan = new(2, Guid.NewGuid().ToString("N"), FrozenJournalRoots(), Path.Combine(delta, "index-" + Guid.NewGuid().ToString("N")), segments.ActiveId);
                PublishLiveRepack(intentPath, plan);
                boundary?.Invoke("metadata.repack-roots-durable");
            }
            repackWatching = true; repackAdmissions ??= pagedIndex!.Empty;
        }
        JournalDeltaStore? shadow = null;
        try
        {
            ct.ThrowIfCancellationRequested();
            if (alreadyPublished) { CleanupRetiredIndices(delta); File.Delete(intentPath); return; }
            if (!WorkspaceStore.SamePath(Path.GetDirectoryName(plan.Target)!, delta) || !IndexName(Path.GetFileName(plan.Target)) ||
                !WorkspaceStore.SamePath(Path.GetDirectoryName(plan.Frozen.Directory)!, delta) || !IndexName(Path.GetFileName(plan.Frozen.Directory)) ||
                plan.Copied is { } saved && !WorkspaceStore.SamePath(saved.Directory, plan.Target)) throw PagedInvalid();
            if (plan.Copied is null)
            {
                if (!Directory.Exists(plan.Target))
                {
                    Directory.CreateDirectory(plan.Target);
                    SourceCaptureTree.Publish(Path.Combine(plan.Target, "owner.json"), new PagedIndexOwner(1, PagedOwnerHash(Identity)));
                }
                else ValidateIndexOwner(plan.Target, Identity);
                using var source = new PagedMetadataStore(plan.Frozen.Directory);
                using var target = new PagedMetadataStore(plan.Target, true, filePins: sourceFilePins);
                // Exactly ten control roots; this is unrelated to input size.
                var copied = source.CopyRootsTo(target, RepackRoots(plan.Frozen), Path.GetTempPath(), ct).ToArray();
                if (copied.Length != 10) throw PagedInvalid(); target.Flush();
                plan = plan with { Copied = RelocatedRoots(plan.Frozen, plan.Target, copied) }; PublishLiveRepack(intentPath, plan);
                boundary?.Invoke("metadata.repack-payload-durable");
            }
            shadow = new(Identity, snapshot, plan.Copied ?? throw PagedInvalid(), sourceFilePins, capturedContentReader);
            long segment = plan.FirstSegment, offset = JournalSegmentStorage.HeaderBytes;
            string? admissionHash = null; var processedAdmissions = shadow.pagedIndex!.Empty;
            while (true)
            {
                ct.ThrowIfCancellationRequested(); RepackPoint point;
                using (EnterGate(mutation: true))
                {
                    Check(); FlushPendingWrites(); pagedIndex!.Flush();
                    point = new(segments.ActiveId, journal.Length,
                        new FileStream(journal.Name, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1, FileOptions.RandomAccess),
                        NodeRoot!, pathComponents!.Root, ((PagedSet<Guid>)boundedDurableNodes).Root, repackAdmissions!, segmentRoot!, FrozenJournalRoots());
                }
                using (point.Input)
                {
                    using var records = new PagedMetadataStore(plan.Frozen.Directory);
                    while (segment < point.Segment)
                    {
                        var encoded = records.Open(point.Segments).Find(segment.ToString("x16")) ?? throw PagedInvalid(); var descriptor = DecodeSegment(encoded);
                        using var input = segments.OpenSealed(descriptor); shadow.ReplayShadow(input, segment, offset, descriptor.Length);
                        shadow.segmentRoot = shadow.pagedIndex!.Set(shadow.segmentRoot!, segment.ToString("x16"), encoded);
                        segment++; offset = JournalSegmentStorage.HeaderBytes;
                    }
                    if (offset < point.Bytes) shadow.ReplayShadow(point.Input, segment, offset, point.Bytes);
                    offset = point.Bytes;
                    var originalNames = new PathComponents(records) { Root = point.Names };
                    foreach (var admission in records.Open(point.Admissions).Enumerate())
                    {
                        if (shadow.pagedIndex!.Open(processedAdmissions).Find(admission.Key) is not null) continue;
                        processedAdmissions = shadow.pagedIndex.Set(processedAdmissions, admission.Key, []);
                        if (records.Open(point.Durable).Find("g" + admission.Key) is not null) continue;
                        var record = records.Open(point.Nodes).FindEntry(admission.Key); if (record is null) continue;
                        var entry = DecodeEntry(record.Value.ToArray(), originalNames, point.Names);
                        if (entry.Deleted || shadow.nodes.ContainsKey(entry.Id)) continue;
                        shadow.nodes.Add(entry.Id, new Node(entry)); shadow.IndexPath(entry.Path, entry.Id);
                    }
                    admissionHash = point.Admissions.ContentHash;
                    if (point.Segments.ContentHash != shadow.segmentRoot!.ContentHash)
                    {
                        // Earlier binaries could finish empty-segment GC
                        // before resuming this same durable copy. Reconcile
                        // only that provably payload/frame-free storage change,
                        // after every accepted/nonsegment logical root agrees.
                        if (!RepackContentMatches(point.Roots, shadow)) throw RepackMismatch();
                        ReconcileRetiredEmptySegments(shadow, records, point.Segments, ct);
                    }
                }
                using (EnterGate(mutation: true))
                {
                    Check(); FlushPendingWrites();
                    if (segments.ActiveId != segment || journal.Length != offset || repackAdmissions!.ContentHash != admissionHash) continue;
                    if (activeIndexCursors != 0) return;
                    if (segmentRoot!.ContentHash != point.Segments.ContentHash) continue;
                    if (!RepackContentMatches(FrozenJournalRoots(), shadow) || segmentRoot.ContentHash != shadow.segmentRoot!.ContentHash) throw RepackMismatch();
                    boundary?.Invoke("metadata.repack-before-publication");
                    var old = pagedIndex;
                    segments.Rotate(sealedSegment =>
                    {
                        shadow.segmentRoot = shadow.pagedIndex!.Set(shadow.segmentRoot!, sealedSegment.Id.ToString("x16"), EncodeSegment(sealedSegment));
                        shadow.pagedIndex.Flush(); return shadow.FrozenJournalRoots();
                    });
                    journal = segments.Active; expectedJournalBytes = journal.Length; segmentFrames = 0;
                    pagedIndex = shadow.pagedIndex; pagedIndexDirectory = shadow.pagedIndexDirectory; nodes = shadow.nodes; paths = shadow.paths;
                    hidden = shadow.hidden; pathChanges = shadow.pathChanges; pathComponents = shadow.pathComponents; boundedDurableNodes = shadow.boundedDurableNodes;
                    segmentRoot = shadow.segmentRoot; adoptionRoot = shadow.adoptionRoot; captureRoot = shadow.captureRoot; packingRoot = shadow.packingRoot; sequence = shadow.sequence;
                    nextDataSegment = shadow.nextDataSegment; dataSegmentBytes = shadow.dataSegmentBytes; dataPayloadBytes = shadow.dataPayloadBytes;
                    payloadBytes = shadow.payloadBytes; logicalPayloadBytes = shadow.logicalPayloadBytes; extentCount = shadow.extentCount;
                    sourceMutationHash = shadow.sourceMutationHash; sourceMutationBytes = shadow.sourceMutationBytes;
                    shadow.pagedIndex = null; old!.Dispose(); repackWatching = false; repackAdmissions = null;
                    durablyFlushedJournal = journal; durablyFlushedBytes = journal.Length;
                    break;
                }
            }
            boundary?.Invoke("metadata.repack-published");
            CleanupRetiredIndices(delta); File.Delete(intentPath);
            boundary?.Invoke("metadata.repack-reclaimed");
        }
        finally
        {
            shadow?.pagedIndex?.Dispose();
            using (EnterGate())
            { repackBusy = false; if (alreadyPublished) { repackWatching = false; repackAdmissions = null; } }
        }
    }
    private static WorkspaceException RepackMismatch() => new("JournalRepackMismatch", "The caught-up immutable index differs from the accepted source and current metadata roots; preserve both stores.");
    private static bool RepackContentMatches(JournalIndexRoots expected, JournalDeltaStore shadow) =>
        expected.SourceMutationHash == shadow.sourceMutationHash && expected.SourceMutationBytes == shadow.sourceMutationBytes && expected.Sequence == shadow.sequence &&
        expected.ExtentCount == shadow.extentCount && expected.PayloadBytes == shadow.payloadBytes && expected.DecodedPayloadBytes == shadow.logicalPayloadBytes &&
        expected.NextDataSegment == shadow.nextDataSegment && expected.DataSegmentBytes == shadow.dataSegmentBytes && expected.DataPayloadBytes == shadow.dataPayloadBytes &&
        expected.Nodes.ContentHash == shadow.NodeRoot!.ContentHash && expected.Paths.ContentHash == shadow.PathRoot!.ContentHash &&
        expected.Hidden.ContentHash == shadow.HiddenRoot!.ContentHash && expected.Changes.ContentHash == shadow.ChangeRoot!.ContentHash &&
        expected.Names.ContentHash == shadow.pathComponents!.Root.ContentHash && expected.Adoptions?.ContentHash == shadow.adoptionRoot!.ContentHash &&
        expected.Captures?.ContentHash == shadow.captureRoot!.ContentHash && expected.Packing?.ContentHash == shadow.packingRoot!.ContentHash &&
        expected.DurableNodes.ContentHash == ((PagedSet<Guid>)shadow.boundedDurableNodes).Root.ContentHash;
    private void ReconcileRetiredEmptySegments(JournalDeltaStore shadow, PagedMetadataStore source, MetadataTreeRoot current, CancellationToken ct)
    {
        var next = shadow.segmentRoot!;
        foreach (var entry in shadow.pagedIndex!.Open(shadow.segmentRoot!).Enumerate())
        {
            ct.ThrowIfCancellationRequested(); var present = source.Open(current).Find(entry.Key);
            if (present is not null) { if (!present.AsSpan().SequenceEqual(entry.Value.Span)) throw RepackMismatch(); continue; }
            var descriptor = DecodeSegment(entry.Value.Span);
            if (descriptor.Id < 0 || entry.Key != descriptor.Id.ToString("x16") || descriptor.Length != JournalSegmentStorage.HeaderBytes || descriptor.PayloadBytes != 0 ||
                descriptor.NativeIdentity is null) throw RepackMismatch();
            next = shadow.pagedIndex.Remove(next, entry.Key);
        }
        // Every remaining/new current binding still receives the existing
        // complete sealed-file native identity/length/hash proof outside Gate.
        // No source catalog, file, ownership receipt or accepted ACK changes.
        foreach (var entry in source.Open(current).Enumerate())
        {
            ct.ThrowIfCancellationRequested(); var copied = shadow.pagedIndex.Open(next).Find(entry.Key);
            if (copied is null || !copied.AsSpan().SequenceEqual(entry.Value.Span)) throw RepackMismatch();
            using var proof = segments!.OpenSealed(DecodeSegment(entry.Value.Span));
        }
        if (next.ContentHash != current.ContentHash) throw RepackMismatch();
        shadow.segmentRoot = next;
    }
    private void ReplayShadow(FileStream input, long segment, long start, long end)
    {
        journal = input; replaySegment = segment; replaying = true;
        // Only the private shadow is updated here. Bound deferred index edits
        // exactly as live batches do; no accepted root or ACK is published by
        // replay. The existing full root comparison and flush precede rotation.
        BeginIndexBatch();
        try { Replay(start, end); FlushIndexBatch(); }
        finally { EndIndexBatch(); replaying = false; replaySegment = null; journal = null!; }
    }
    private static LiveRepack ReadLiveRepack(string path)
    {
        var value = JournalControlIntegrity.Read<LiveRepack>(path);
        if (value.Version != 2 || value.Digest != JournalControlIntegrity.Digest(value with { Digest = "" }) || value.FirstSegment < 0 || !Guid.TryParseExact(value.Request, "N", out _)) throw PagedInvalid();
        return value;
    }
    private static void PublishLiveRepack(string path, LiveRepack plan)
    {
        plan = plan with { Digest = JournalControlIntegrity.Digest(plan with { Digest = "" }) };
        string pending = path + ".pending"; using (var output = new FileStream(pending, FileMode.Create, FileAccess.Write, FileShare.Read))
        { JsonSerializer.Serialize(output, plan, JournalControlIntegrity.Json); output.Flush(true); }
        AtomicFilePublication.Publish(pending, path);
    }
    private void CleanupRetiredIndices(string delta)
    {
        string captures = Path.Combine(Path.GetDirectoryName(delta)!, "captures");
        foreach (string directory in Directory.EnumerateDirectories(delta, "index-*"))
        {
            if (WorkspaceStore.SamePath(directory, pagedIndexDirectory!)) continue;
            if (ObservationReferences(delta, directory)) continue;
            bool protectedRoot = false;
            foreach (var capture in EnumerateFrozenCaptures())
            {
                string capturePath = Path.Combine(captures, capture.Id);
                if (capture.Paged?.Directory == directory && !File.Exists(Path.Combine(capturePath, "materialized.json"))) { protectedRoot = true; break; }
            }
            if (protectedRoot) continue;
            if (Directory.Exists(captures))
                foreach (string capturePath in Directory.EnumerateDirectories(captures))
                {
                    if (!File.Exists(Path.Combine(capturePath, "capture.json"))) continue;
                    var capture = SourceCaptureTree.ReadCapture(capturePath, verifyPagedRoots: false);
                    if (capture.Paged?.Directory == directory && (!File.Exists(Path.Combine(capturePath, "materialized.json")) || Directory.EnumerateFiles(capturePath, "*.pin").Any()))
                    { protectedRoot = true; break; }
                }
            if (protectedRoot) continue;
            try { ReclaimPagedIndex(directory, Identity, sourceFilePins); }
            catch (IOException) { /* A reader lease retains this exact store. */ }
        }
    }
    private static bool ObservationReferences(string delta, string directory)
    {
        string overlay = Path.Combine(Path.GetDirectoryName(delta)!, "overlay");
        foreach (string name in new[] { "source-response.json", "source-query.json" })
        {
            string path = Path.Combine(overlay, name); if (!File.Exists(path)) continue;
            try
            {
                WorkspaceStore.EnsurePlainPath(path); using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                byte[] buffer = new byte[65536]; int held = 0; bool final = false, expectDirectory = false; var state = new JsonReaderState();
                while (!final)
                {
                    int read = input.Read(buffer, held, buffer.Length - held); final = read == 0; held += read;
                    var reader = new Utf8JsonReader(buffer.AsSpan(0, held), final, state);
                    while (reader.Read())
                    {
                        if (expectDirectory && reader.TokenType == JsonTokenType.String && reader.GetString() is { } target && WorkspaceStore.SamePath(target, directory)) return true;
                        expectDirectory = reader.TokenType == JsonTokenType.PropertyName && reader.ValueTextEquals("directory");
                    }
                    int consumed = checked((int)reader.BytesConsumed); state = reader.CurrentState; buffer.AsSpan(consumed, held - consumed).CopyTo(buffer); held -= consumed;
                    if (!final && held == buffer.Length)
                    {
                        if (buffer.Length >= 262144) return true; Array.Resize(ref buffer, buffer.Length * 2);
                    }
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
            { return true; } // An unreadable owned observation cannot authorize GC.
        }
        return false;
    }
}
