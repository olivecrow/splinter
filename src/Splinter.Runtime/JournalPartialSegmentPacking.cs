using System.Security.Cryptography;
using System.Buffers.Binary;
using System.Text.Json;
using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class JournalDeltaStore
{
    // The accepted request owns a frozen source root before the long scan.
    // Packed raw bytes use negative IDs; SJR2 generations stay consecutive.
    private sealed record SegmentPackingJob(int Version, string Id, string Owner, string Directory, string IndexDirectory,
        MetadataTreeRoot FrozenNodes, MetadataTreeRoot Catalog, MetadataTreeRoot Sources, MetadataTreeRoot Rows,
        long Target, string Stage, string Phase, string? NativeIdentity = null, long Bytes = 0,
        string? Hash = null, string? After = null, string Digest = "", MetadataTreeRoot? AllRows = null,
        MetadataTreeRoot? Candidates = null, string? AfterSource = null);
    private sealed record PackingCandidate(JournalSegment Segment, long LiveBytes);
    private sealed record RangeRelocation(Guid FileId, long Offset, int Length, string Block, int BlockOffset,
        long Source, long SourcePhysical, long Target, long TargetPhysical);
    private sealed record PreservedPacking(int Version, string Owner, string Request, string Path, string Reason, string Digest = "");
    private static string PackKey(long id) => id.ToString("x16");
    private static string PackingIndex(SegmentPackingJob job) => Path.Combine(job.Directory, "index-" + job.Id);
    private static byte[] PackBytes<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, JournalControlIntegrity.Json);
    private static T PackRead<T>(ReadOnlySpan<byte> bytes)
    {
        try
        {
            using var document = JsonDocument.Parse(bytes.ToArray());
            if (!JsonPropertyValidation.HasUniqueProperties(document.RootElement)) throw PagedInvalid();
            return document.RootElement.Deserialize<T>(JournalControlIntegrity.Json) ?? throw PagedInvalid();
        }
        catch (JsonException) { throw PagedInvalid(); }
    }
    private SegmentPackingJob? PendingPacking()
    {
        var entry = pagedIndex!.Open(packingRoot!).FindEntry("request"); if (entry is null) return null;
        var job = ReadPackingJob(pagedIndex, entry);
        if (job.Version != 2 || job.Owner != PagedOwnerHash(Identity) || !Guid.TryParseExact(job.Id, "N", out _) || job.Target > -2 ||
            job.Digest != JournalControlIntegrity.Digest(job with { Digest = "" }) ||
            !WorkspaceStore.SamePath(Path.GetDirectoryName(job.IndexDirectory)!, Path.GetDirectoryName(journal.Name)!) ||
            !WorkspaceStore.SamePath(job.Directory, Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(journal.Name)!)!, "journal-packing", job.Id)) ||
            !WorkspaceStore.SamePath(Path.GetDirectoryName(job.Stage)!, job.Directory) || job.Bytes < 0 ||
            job.Phase is not ("planning" or "allocated" or "owned" or "copied" or "mapped" or "published" or "relocated" or "cleanup")) throw PagedInvalid();
        return job;
    }
    private static SegmentPackingJob ReadPackingJob(PagedMetadataStore index, MetadataEntry entry)
    {
        if (entry.Value.Length != 36 || !entry.Value.Span[..4].SequenceEqual("SPK2"u8) || entry.Child is not { Count: 3 } bundle) throw PagedInvalid();
        var tree = index.Open(bundle); var control = tree.FindEntry("control") ?? throw PagedInvalid();
        var nodes = tree.FindEntry("nodes") ?? throw PagedInvalid(); var catalog = tree.FindEntry("catalog") ?? throw PagedInvalid();
        if (control.Value.Length != 32 || !control.Value.Span.SequenceEqual(entry.Value.Span[4..]) || control.Child is not { } chunks ||
            chunks.Count < 1 || chunks.Count > (JournalControlIntegrity.MaximumBytes + 59999) / 60000 ||
            nodes.Value.Length != 0 || nodes.Child is null || catalog.Value.Length != 0 || catalog.Child is null) throw PagedInvalid();
        using var input = new CaptureControlReader(index, chunks);
        SegmentPackingJob job;
        try
        {
            using var document = JsonDocument.Parse(input);
            if (!JsonPropertyValidation.HasUniqueProperties(document.RootElement)) throw PagedInvalid();
            job = document.RootElement.Deserialize<SegmentPackingJob>(JournalControlIntegrity.Json) ?? throw PagedInvalid();
        }
        catch (JsonException) { throw PagedInvalid(); }
        if (job.Digest != Convert.ToHexStringLower(control.Value.Span) || job.Digest != JournalControlIntegrity.Digest(job with { Digest = "" }) ||
            nodes.Child != job.FrozenNodes || catalog.Child != job.Catalog) throw PagedInvalid();
        return job;
    }
    private void StorePackingJob(SegmentPackingJob job)
    {
        using var output = new CaptureControlWriter(pagedIndex!); JsonSerializer.Serialize(output, job, JournalControlIntegrity.Json);
        var chunks = output.Complete(); var bundle = pagedIndex!.Empty;
        bundle = pagedIndex.Set(bundle, "catalog", [], job.Catalog);
        bundle = pagedIndex.Set(bundle, "control", Convert.FromHexString(job.Digest), chunks);
        bundle = pagedIndex.Set(bundle, "nodes", [], job.FrozenNodes);
        byte[] tag = new byte[36]; "SPK2"u8.CopyTo(tag); Convert.FromHexString(job.Digest).CopyTo(tag, 4);
        packingRoot = pagedIndex.Set(packingRoot!, "request", tag, bundle);
    }
    private void SavePacking(SegmentPackingJob job)
    {
        job = job with { Digest = JournalControlIntegrity.Digest(job with { Digest = "" }) };
        using (EnterGate(mutation: true))
        { Check(); FlushPendingWrites(); Commit(new(sequence, "packing-state", nodes[paths[""]].Entry, Packing: job), []); }
    }
    private void ApplyPackingMutation(Mutation mutation)
    {
        if (segments is null && !segmentShadow || mutation.Sequence != sequence || mutation.Entry != nodes[paths[""]].Entry) throw PagedInvalid();
        if (mutation.Operation == "packing-preserve")
        {
            var proof = PackRead<PreservedPacking>(Convert.FromBase64String(mutation.PreservedPacking!));
            if (proof.Version != 2 || proof.Owner != PagedOwnerHash(Identity) || proof.Digest != JournalControlIntegrity.Digest(proof with { Digest = "" })) throw PagedInvalid();
            using var output = new CaptureControlWriter(pagedIndex!); JsonSerializer.Serialize(output, proof, JournalControlIntegrity.Json);
            byte[] tag = new byte[36]; "SPP2"u8.CopyTo(tag); Convert.FromHexString(proof.Digest).CopyTo(tag, 4);
            packingRoot = pagedIndex!.Set(packingRoot!, "preserved:" + proof.Request + ":" + Path.GetFileName(proof.Path), tag, output.Complete()); return;
        }
        if (mutation.Operation == "packing-complete") { packingRoot = pagedIndex!.Remove(packingRoot!, "request"); return; }
        if (mutation.Operation == "relocate-range")
        {
            var row = mutation.Relocation!;
            if (row.Length <= 0 || row.Length > Identity.MaxTransferBytes || row.Offset < 0 || row.Offset > long.MaxValue - row.Length ||
                row.Source == -1 || row.Target > -2 || row.SourcePhysical < 0 || row.TargetPhysical < 0 || row.BlockOffset < 0 ||
                row.Block.Length != 64 || !row.Block.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f')) throw PagedInvalid();
            if (!nodes.TryGetValue(row.FileId, out var node)) return;
            long prior = node.Extents.LongCount;
            foreach (var extent in node.Extents.From(row.Offset))
            {
                if (extent.Start >= checked(row.Offset + row.Length)) break;
                long from = Math.Max(extent.Start, row.Offset), to = Math.Min(extent.Start + extent.Length, row.Offset + row.Length);
                if (to <= from || extent.ContentHash is not null || extent.Segment != row.Source || LiveBlockToken(extent) != row.Block ||
                    ProofOffset(extent) + from - extent.Start != row.BlockOffset + from - row.Offset ||
                    extent.JournalOffset + extent.BlockOffset + from - extent.Start != row.SourcePhysical + from - row.Offset) continue;
                int skip = checked((int)(from - row.Offset));
                node.Extents.ReplacePaged(new(from, checked((int)(to - from)), row.TargetPhysical, new(row.Length, row.Length), skip,
                    row.Target, OriginBlock: row.Block, OriginOffset: checked(row.BlockOffset + skip)));
            }
            extentCount = checked(extentCount - prior + node.Extents.LongCount); return;
        }
        var job = mutation.Packing!;
        if (job.Version != 2 || job.Digest != JournalControlIntegrity.Digest(job with { Digest = "" }) || job.Owner != PagedOwnerHash(Identity)) throw PagedInvalid();
        if (job.Phase == "planning" && pagedIndex!.Open(packingRoot!).Find("request") is null)
        { if (job.Target != nextDataSegment) throw PagedInvalid(); nextDataSegment = checked(nextDataSegment - 1); }
        else if (job.Phase == "allocated" && pagedIndex!.Open(packingRoot!).FindEntry("request") is { } prior && ReadPackingJob(pagedIndex, prior).Target != job.Target)
        { if (job.Target != nextDataSegment) throw PagedInvalid(); nextDataSegment = checked(nextDataSegment - 1); }
        if (job.Phase == "published")
        {
            var descriptor = new JournalSegment(job.Target, job.Bytes, job.Hash ?? throw PagedInvalid(), job.NativeIdentity, job.Bytes);
            string key = PackKey(job.Target); var existing = pagedIndex!.Open(segmentRoot!).Find(key);
            if (existing is null)
            {
                segmentRoot = pagedIndex.Set(segmentRoot!, key, EncodeSegment(descriptor));
                if (captureSegmentRoot is not null) captureSegmentRoot = pagedIndex.Set(captureSegmentRoot, key, EncodeSegment(descriptor));
                dataSegmentBytes = checked(dataSegmentBytes + job.Bytes); dataPayloadBytes = checked(dataPayloadBytes + job.Bytes);
            }
            else if (!existing.AsSpan().SequenceEqual(EncodeSegment(descriptor))) throw PagedInvalid();
        }
        if (job.FrozenNodes.StoreId != pagedIndex!.StoreId || job.Catalog.StoreId != pagedIndex.StoreId) throw PagedInvalid();
        StorePackingJob(job);
    }
    private void PreservePackingGap(SegmentPackingJob job)
    {
        var proof = new PreservedPacking(2, job.Owner, job.Id, job.Stage, "native-creation-before-identity");
        proof = proof with { Digest = JournalControlIntegrity.Digest(proof with { Digest = "" }) };
        string receipt = job.Stage + ".preserved.json";
        if (File.Exists(receipt))
        { if (JournalControlIntegrity.Read<PreservedPacking>(receipt) != proof) throw PagedInvalid(); }
        else SourceCaptureTree.Publish(receipt, proof);
        using (EnterGate(mutation: true))
        { Check(); FlushPendingWrites(); Commit(new(sequence, "packing-preserve", nodes[paths[""]].Entry, PreservedPacking: Convert.ToBase64String(PackBytes(proof))), []); }
    }
    public static IEnumerable<(string Path, string Request, string Reason)> EnumeratePreservedPacking(string control, OverlayIdentity? expected = null)
    {
        string root = Path.Combine(control, "journal-packing"); if (!Directory.Exists(root)) yield break;
        foreach (string directory in Directory.EnumerateDirectories(root))
            foreach (string file in Directory.EnumerateFiles(directory, "*.preserved.json"))
            {
                var proof = JournalControlIntegrity.Read<PreservedPacking>(file);
                if (proof.Version != 2 || proof.Digest != JournalControlIntegrity.Digest(proof with { Digest = "" }) || expected is not null && proof.Owner != PagedOwnerHash(expected) ||
                    !WorkspaceStore.SamePath(Path.GetDirectoryName(proof.Path)!, directory) || Path.GetFileName(directory) != proof.Request) throw PagedInvalid();
                yield return (proof.Path, proof.Request, proof.Reason);
            }
    }
    public static bool HasPendingSourcePacking(string path)
    {
        if (!IsSegmentedJournal(path)) return false;
        string directory = Path.GetDirectoryName(path)!;
        using var input = File.OpenRead(Path.Combine(directory, "identity.json"));
        var identity = JsonSerializer.Deserialize<OverlayIdentity>(input) ?? throw PagedInvalid();
        using var storage = new JournalSegmentStorage(directory, identity, readOnly: true);
        using var index = new PagedMetadataStore(storage.State.Roots.Directory);
        bool pending = storage.State.Roots.Packing is { } root && index.Open(root).Find("request") is not null;
        var active = storage.Active; active.Position = JournalSegmentStorage.HeaderBytes; byte[] header = new byte[FrameHeaderBytes];
        while (active.Position < active.Length)
        {
            active.ReadExactly(header); int count = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4));
            int payload = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(8));
            if (!header.AsSpan(0, 4).SequenceEqual("SJR2"u8) || count < 1 || count > 3 * 6 * 32000 + MaxMetadataBytes || payload < 0 ||
                active.Length - active.Position < count + (long)payload + FrameChecksumBytes) throw PagedInvalid();
            byte[] metadata = new byte[count]; active.ReadExactly(metadata); var mutation = ReadCurrentMutation(metadata);
            if (mutation.Operation == "packing-state") pending = true;
            else if (mutation.Operation == "packing-complete") pending = false;
            active.Position += (long)payload + FrameChecksumBytes;
        }
        return pending;
    }
    private bool PackPartialSegments(CancellationToken ct, Action<string>? boundary, bool opportunistic = false)
    {
        SegmentPackingJob? job;
        using (EnterGate()) { Check(); job = PendingPacking(); }
        // An existing request always drains its original work. Cost admission
        // applies only to a newly created opportunistic request, never recovery.
        bool costAwarePackedSources = opportunistic && job is null;
        if (job is null)
        {
            using (EnterGate(mutation: true))
            {
                Check(); FlushPendingWrites(); RollSegment();
                string id = Guid.NewGuid().ToString("N"), directory = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(journal.Name)!)!, "journal-packing", id);
                job = new(2, id, PagedOwnerHash(Identity), directory, pagedIndexDirectory!, NodeRoot!, segmentRoot!, pagedIndex!.Empty,
                    pagedIndex.Empty, nextDataSegment, Path.Combine(directory, "data-" + Guid.NewGuid().ToString("N") + ".tmp"), "planning");
                SavePacking(job); boundary?.Invoke("segment-pack.intent");
            }
        }
        if (job.Phase == "cleanup") { FinishPacking(job); return false; }
        if (job.Phase == "planning") job = BuildPackingPlan(job, ct, costAwarePackedSources);
        if (job.Rows.Count == 0) { FinishPacking(job); return false; }
        using var plan = new PagedMetadataStore(PackingIndex(job));
        if (job.Phase == "allocated")
        {
            WorkspaceStore.EnsurePlainPath(job.Stage);
            if (File.Exists(job.Stage))
            {
                PreservePackingGap(job); job = job with { Stage = Path.Combine(job.Directory, "data-" + Guid.NewGuid().ToString("N") + ".tmp") }; SavePacking(job);
            }
            using (var output = new FileStream(job.Stage, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read))
            {
                output.Flush(true); boundary?.Invoke("segment-pack.created-before-identity");
                job = job with { NativeIdentity = sourceFilePins!.IdentifyOpened(output), Phase = "owned" }; SavePacking(job);
            }
            boundary?.Invoke("segment-pack.identity-durable");
        }
        if (job.Phase == "owned")
        {
            using var output = new FileStream(job.Stage, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, 65536);
            sourceFilePins!.Verify(output, job.NativeIdentity!); output.SetLength(0);
            byte[] buffer = new byte[65536]; long copied = 0, sourceId = -1, coveredEnd = -1; FileStream? input = null;
            try
            {
                foreach (var record in plan.Open(job.Rows).Enumerate())
                {
                    ct.ThrowIfCancellationRequested(); var row = PackRead<RangeRelocation>(record.Value.Span);
                    if (row.Source != sourceId)
                    {
                        input?.Dispose(); input = segments!.OpenSealed(DecodeSegment(plan.Open(job.Sources).Find(PackKey(row.Source)) ?? throw PagedInvalid())); sourceId = row.Source; coveredEnd = -1;
                    }
                    long from = Math.Max(row.SourcePhysical, coveredEnd), end = checked(row.SourcePhysical + row.Length);
                    while (from < end)
                    {
                        int count = (int)Math.Min(buffer.Length, end - from), got = RandomAccess.Read(input!.SafeFileHandle, buffer.AsSpan(0, count), from);
                        if (got != count) throw PagedInvalid(); output.Write(buffer.AsSpan(0, count)); copied += count; from += count;
                    }
                    coveredEnd = Math.Max(coveredEnd, end);
                }
                if (copied != job.Bytes) throw PagedInvalid(); output.Flush(true); output.Position = 0;
                job = job with { Phase = "copied", Hash = Convert.ToHexStringLower(SHA256.HashData(output)) };
            }
            finally { input?.Dispose(); }
            SavePacking(job); boundary?.Invoke("segment-pack.payload-durable");
        }
        if (job.Phase == "copied") { job = job with { Phase = "mapped" }; SavePacking(job); boundary?.Invoke("segment-pack.map-durable"); }
        string target = segments!.SegmentPath(job.Target);
        if (job.Phase == "mapped")
        {
            if (!File.Exists(target)) { using (var input = OpenPackedProof(job.Stage, job)) { } File.Move(job.Stage, target); }
            using (var input = OpenPackedProof(target, job)) { }
            job = job with { Phase = "published" }; SavePacking(job); boundary?.Invoke("segment-pack.published");
        }
        using var targetFence = OpenPackedProof(target, job);
        if (job.Phase == "published")
        {
            foreach (var batch in plan.Open(job.Rows).Enumerate(job.After).Chunk(IWorkspaceWriteBatch.MaximumWrites - 1))
            {
                ct.ThrowIfCancellationRequested();
                var next = job with { After = batch[^1].Key, Digest = "" };
                next = next with { Digest = JournalControlIntegrity.Digest(next) };
                CommitStorageBatch(() =>
                {
                    foreach (var record in batch)
                    {
                        var row = PackRead<RangeRelocation>(record.Value.Span);
                        Commit(new(sequence, "relocate-range", nodes[paths[""]].Entry, Relocation: row), []);
                    }
                    // Progress follows all relocation frames in this flush.
                    // A torn prefix replays safely from the previous cursor.
                    Commit(new(sequence, "packing-state", nodes[paths[""]].Entry, Packing: next), []);
                });
                job = next; boundary?.Invoke("segment-pack.relocated");
            }
            job = job with { Phase = "relocated" }; SavePacking(job);
        }
        boundary?.Invoke("segment-pack.relocated-all"); sourceFilePins!.Verify(targetFence, job.NativeIdentity!);
        using (EnterGate(mutation: true)) { Check(); FlushPendingWrites(); RollSegment(); }
        boundary?.Invoke("segment-pack.root-rolled"); CollectDeadSegments(ct, boundary);
        foreach (var source in plan.Open(job.Sources).Enumerate())
            if (File.Exists(segments.SegmentPath(DecodeSegment(source.Value.Span).Id))) return false;
        boundary?.Invoke("segment-pack.alias-retired"); plan.Dispose();
        using (var work = new PagedMetadataStore(PackingIndex(job), true, filePins: sourceFilePins))
        {
            long targetId; using (EnterGate()) targetId = nextDataSegment;
            var next = SelectPackingUnit(job with { Target = targetId, Stage = Path.Combine(job.Directory, "data-" + Guid.NewGuid().ToString("N") + ".tmp"),
                NativeIdentity = null, Hash = null, After = null }, work, ct);
            if (next.Rows.Count != 0) { SavePacking(next); return true; }
        }
        FinishPacking(job); return false;
    }
    private FileStream OpenPackedProof(string path, SegmentPackingJob job)
    {
        WorkspaceStore.EnsurePlainPath(path); var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.RandomAccess);
        try { sourceFilePins!.Verify(input, job.NativeIdentity ?? throw PagedInvalid()); if (input.Length != job.Bytes || Convert.ToHexStringLower(SHA256.HashData(input)) != job.Hash) throw PagedInvalid(); input.Position = 0; return input; }
        catch { input.Dispose(); throw; }
    }
    private SegmentPackingJob BuildPackingPlan(SegmentPackingJob job, CancellationToken ct, bool costAwarePackedSources)
    {
        WorkspaceStore.EnsurePlainPath(job.Directory); Directory.CreateDirectory(job.Directory); using var source = new PagedMetadataStore(job.IndexDirectory);
        string indexDirectory = PackingIndex(job);
        WorkspaceStore.EnsurePlainPath(indexDirectory);
        if (!Directory.Exists(indexDirectory)) Directory.CreateDirectory(indexDirectory);
        string owner = Path.Combine(indexDirectory, "owner.json");
        if (!File.Exists(owner))
        {
            if (Directory.EnumerateFileSystemEntries(indexDirectory).Any(path => Path.GetFileName(path) != "owner.json.pending")) throw PagedInvalid();
            SourceCaptureTree.Publish(owner, new PagedIndexOwner(1, job.Owner));
        }
        else ValidateIndexOwner(indexDirectory, Identity);
        using var work = new PagedMetadataStore(indexDirectory, true, filePins: sourceFilePins);
        IEnumerable<MetadataEntry> Rows()
        {
            foreach (var node in source.Open(job.FrozenNodes).Enumerate())
            {
                if (node.Child is not { } extents || !Guid.TryParseExact(node.Key, "N", out var file)) continue;
                foreach (var item in source.Open(extents).Enumerate())
                {
                    ct.ThrowIfCancellationRequested(); var extent = DecodeExtent(item.Value.ToArray());
                    if (extent.ContentHash is not null || extent.Segment == -1 || extent.Payload.Encoded) continue;
                    var descriptor = DecodeSegment(source.Open(job.Catalog).Find(PackKey(extent.Segment)) ?? throw PagedInvalid());
                    if (descriptor.NativeIdentity is null) throw PagedInvalid(); long physical = checked(extent.JournalOffset + extent.BlockOffset);
                    string token = extent.OriginBlock ?? SourceCaptureTree.BlockIdentity(descriptor.NativeIdentity, extent.JournalOffset, extent.Payload);
                    var row = new RangeRelocation(file, extent.Start, extent.Length, token, ProofOffset(extent), extent.Segment, physical, job.Target, 0);
                    yield return new(PackKey(extent.Segment) + ":" + physical.ToString("x16") + ":" + node.Key + ":" + extent.Start.ToString("x16"), PackBytes(row));
                }
            }
        }
        // Keep the sorted rows in shared-budget binary scratch until source
        // admission is known. Rejected mostly-live packed objects need no
        // persistent row tree, while a spill still uses bounded disk cursors.
        using var ordered = new MetadataSpool(job.Directory);
        var candidates = work.Empty; string? group = null; long union = 0, end = -1;
        void Select()
        {
            if (group is null) return; var descriptor = DecodeSegment(source.Open(job.Catalog).Find(group) ?? throw PagedInvalid());
            long dead = descriptor.Length - union;
            // Normal journals also discard frame overhead on their first
            // packing. Already packed raw objects have no such benefit: keep
            // opportunistic live copying to at most three bytes per dead byte.
            // Explicit force and existing requests keep the complete cleanup.
            bool worthwhile = !costAwarePackedSources || descriptor.Id >= 0 || dead >= descriptor.Length / 4 + (descriptor.Length % 4 == 0 ? 0 : 1);
            if (dead >= 4096 && union > 0 && worthwhile)
                candidates = work.Set(candidates, group, PackBytes(new PackingCandidate(descriptor, union)));
        }
        foreach (var item in MetadataExternalSort.Order(Rows(), job.Directory, ct, sourceFilePins))
        {
            ct.ThrowIfCancellationRequested(); ordered.Append(item);
            var row = PackRead<RangeRelocation>(item.Value.Span); string key = PackKey(row.Source);
            if (key != group) { Select(); group = key; union = 0; end = -1; }
            long stop = checked(row.SourcePhysical + row.Length); union += Math.Max(0, stop - Math.Max(row.SourcePhysical, end)); end = Math.Max(end, stop);
        }
        Select();
        IEnumerable<MetadataEntry> AdmittedRows()
        {
            string? admittedGroup = null; bool admitted = false;
            foreach (var item in ordered.Read())
            {
                ct.ThrowIfCancellationRequested();
                if (item.Key.Length < 17 || item.Key[16] != ':') throw PagedInvalid();
                string key = item.Key[..16];
                if (key != admittedGroup) { admittedGroup = key; admitted = work.Open(candidates).Find(key) is not null; }
                if (admitted) yield return item;
            }
        }
        var all = work.Build(AdmittedRows(), ct); work.Flush();
        job = SelectPackingUnit(job with { AllRows = all, Candidates = candidates }, work, ct); SavePacking(job); return job;
    }
    private SegmentPackingJob SelectPackingUnit(SegmentPackingJob job, PagedMetadataStore work, CancellationToken ct)
    {
        var selected = work.Empty; long total = 0; string? first = null, last = null;
        foreach (var item in work.Open(job.Candidates ?? throw PagedInvalid()).Enumerate(job.AfterSource))
        {
            ct.ThrowIfCancellationRequested(); var candidate = PackRead<PackingCandidate>(item.Value.Span);
            if (candidate.LiveBytes < 1 || candidate.LiveBytes > JournalSegmentStorage.DefaultAppendBytes) throw PagedInvalid();
            if (candidate.LiveBytes > JournalSegmentStorage.DefaultAppendBytes - total) break;
            selected = work.Set(selected, item.Key, EncodeSegment(candidate.Segment)); total += candidate.LiveBytes; first ??= item.Key; last = item.Key;
        }
        long targetAt = 0, basePhysical = 0, baseTarget = 0, end = -1; string? group = null, selectedGroup = null; bool groupSelected = false;
        IEnumerable<MetadataEntry> Mapped()
        {
            if (first is null) yield break;
            foreach (var item in work.Open(job.AllRows ?? throw PagedInvalid()).Enumerate(first))
            {
                ct.ThrowIfCancellationRequested(); var row = PackRead<RangeRelocation>(item.Value.Span); string key = PackKey(row.Source);
                if (StringComparer.Ordinal.Compare(key, last) > 0) break;
                if (key != selectedGroup) { selectedGroup = key; groupSelected = work.Open(selected).Find(key) is not null; }
                if (!groupSelected) continue;
                if (key != group) { group = key; end = -1; }
                if (row.SourcePhysical > end) { basePhysical = row.SourcePhysical; baseTarget = targetAt; }
                long stop = checked(row.SourcePhysical + row.Length); targetAt += Math.Max(0, stop - Math.Max(row.SourcePhysical, end)); end = Math.Max(end, stop);
                yield return item with { Value = PackBytes(row with { Target = job.Target, TargetPhysical = baseTarget + row.SourcePhysical - basePhysical }) };
            }
        }
        var mapped = work.Build(Mapped(), ct); if (targetAt != total) throw PagedInvalid(); work.Flush();
        return job with { Sources = selected, Rows = mapped, Bytes = total, Phase = "allocated", AfterSource = last ?? job.AfterSource };
    }
    private void FinishPacking(SegmentPackingJob job)
    {
        if (job.Phase != "cleanup") { job = job with { Phase = "cleanup" }; SavePacking(job); }
        string pages = PackingIndex(job);
        string control = Path.GetDirectoryName(Path.GetDirectoryName(journal.Name)!)!;
        if (Directory.Exists(pages) || File.Exists(pages + ".reclaim") || File.Exists(pages + ".reclaim.pending") ||
            LatestIndexResidual(ResidualControl(pages, Identity, control), Identity) is { Phase: not "completed" })
            ReclaimPagedIndex(pages, Identity, sourceFilePins, control);
        using (EnterGate(mutation: true)) { Check(); FlushPendingWrites(); Commit(new(sequence, "packing-complete", nodes[paths[""]].Entry), []); RollSegment(); }
        if (!Directory.EnumerateFileSystemEntries(job.Directory).Any()) Directory.Delete(job.Directory);
    }
}
