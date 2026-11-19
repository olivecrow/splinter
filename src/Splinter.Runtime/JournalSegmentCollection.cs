using System.Text.Json;
using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class JournalDeltaStore
{
    private const string SegmentCollectionName = "segment-reclaim.json";
    private readonly object storageMaintenanceGate = new();
    private MetadataTreeRoot? captureSegmentRoot;
    private long lastSegmentCollectionHead = -8;
    private sealed record SegmentCollection(int Version, string Owner, string Directory, MetadataTreeRoot Dead,
        string OriginalCatalog, MetadataTreeRoot Remaining, long Bytes, long Count, long PayloadBytes,
        long DataBytes = 0, long DataPayload = 0, string Digest = "");
    public void MaintainLiveStorage(bool force = false, CancellationToken ct = default, bool collectSegments = false,
        Action<string>? boundary = null, bool resumeOnly = false)
    {
        if (segments is null || sourceFilePins is null || readOnly) return;
        lock (storageMaintenanceGate)
        {
            try
            {
            // A durable index-copy request owns its frozen catalog. Recover
            // it before another collector can retire catalog rows which have
            // no accepted journal mutation for the copied shadow to replay.
            // A reader-deferred request keeps that ordering on later calls.
            string liveRepack = Path.Combine(Path.GetDirectoryName(journal.Name)!, LiveRepackName);
            if (File.Exists(liveRepack))
            {
                CollectLiveMetadata(force, ct, boundary);
                if (File.Exists(liveRepack)) return;
            }
            string segmentIntent = Path.Combine(Path.GetDirectoryName(journal.Name)!, SegmentCollectionName);
            if (resumeOnly || force || collectSegments || File.Exists(segmentIntent) ||
                segments.ActiveId - lastSegmentCollectionHead >= 8)
            {
                bool hasPacking; using (EnterGate()) hasPacking = PendingPacking() is not null;
                // A pending pack resumes with its replacement-object fence
                // before any old-source retirement can proceed.
                if (hasPacking)
                { while (PackPartialSegments(ct, boundary)) ct.ThrowIfCancellationRequested(); }
                else if (!resumeOnly || File.Exists(segmentIntent))
                {
                    CollectDeadSegments(ct, boundary);
                    if (!resumeOnly && !File.Exists(segmentIntent) &&
                        checked(segments.PayloadBytes + dataPayloadBytes) >= 4096)
                        while (PackPartialSegments(ct, boundary, opportunistic: !force)) ct.ThrowIfCancellationRequested();
                }
                if (!resumeOnly) lastSegmentCollectionHead = segments.ActiveId;
            }
            // A final capture still owns every published maintenance request.
            // Starting a new compaction here would rebuild storage about to be reclaimed.
            if (resumeOnly) return;
            bool pending;
            using (EnterGate()) pending = PendingPacking() is not null;
            if (!pending && !File.Exists(Path.Combine(Path.GetDirectoryName(journal.Name)!, SegmentCollectionName))) CollectLiveMetadata(force, ct, boundary);
            }
            catch
            {
                if (File.Exists(Path.Combine(Path.GetDirectoryName(journal.Name)!, SegmentCollectionName)))
                    using (EnterGate()) Poison();
                throw;
            }
        }
    }
    private void CollectDeadSegments(CancellationToken ct, Action<string>? boundary)
    {
        string delta = Path.GetDirectoryName(journal.Name)!, intentPath = Path.Combine(delta, SegmentCollectionName);
        SegmentCollection? plan = null;
        if (File.Exists(intentPath))
        {
            plan = JournalControlIntegrity.Read<SegmentCollection>(intentPath);
            ValidateSegmentCollection(plan, delta);
        }
        if (plan is null)
        {
            while (true)
            {
                MetadataTreeRoot fixedNodes, catalog; PagedMetadataStore index; long captured; long head;
                using (EnterGate())
                {
                    Check(); index = pagedIndex!; fixedNodes = NodeRoot!; catalog = segmentRoot!; captured = captureProtectionGeneration; head = segments!.ActiveId;
                }
                if (catalog.Count == 0) return;
                var live = index.Empty;
                foreach (var node in index.Open(fixedNodes).Enumerate())
                    if (node.Child is { } extents)
                        foreach (var item in index.Open(extents).Enumerate())
                        {
                            ct.ThrowIfCancellationRequested(); var extent = DecodeExtent(item.Value.ToArray());
                            if (extent.ContentHash is null && extent.Segment != -1 && extent.Segment != head)
                            {
                                string key = extent.Segment.ToString("x16");
                                if (index.Open(live).Find(key) is null) live = index.Set(live, key, []);
                            }
                        }
                string captures = Path.Combine(Path.GetDirectoryName(delta)!, "captures");
                foreach (var capture in EnumerateFrozenCaptures())
                {
                    if (File.Exists(Path.Combine(captures, capture.Id, "materialized.json")) || capture.Paged?.Segments is not { } protectedSegments) continue;
                    using var protectedIndex = new PagedMetadataStore(capture.Paged.Directory);
                    foreach (var item in protectedIndex.Open(protectedSegments).Enumerate())
                        if (index.Open(live).Find(item.Key) is null) live = index.Set(live, item.Key, []);
                }
                if (Directory.Exists(captures))
                    foreach (string directory in Directory.EnumerateDirectories(captures))
                    {
                        ct.ThrowIfCancellationRequested(); if (!File.Exists(Path.Combine(directory, "capture.json")) || File.Exists(Path.Combine(directory, "materialized.json"))) continue;
                        var capture = SourceCaptureTree.ReadCapture(directory, verifyPagedRoots: false);
                        if (capture.Paged?.Segments is not { } protectedSegments) continue;
                        using var protectedIndex = new PagedMetadataStore(capture.Paged.Directory);
                        foreach (var item in protectedIndex.Open(protectedSegments).Enumerate())
                            if (index.Open(live).Find(item.Key) is null) live = index.Set(live, item.Key, []);
                    }
                // A published packed target is a proof resource until old
                // aliases are gone, even when every source file was deleted.
                SegmentPackingJob? packing;
                using (EnterGate()) packing = PendingPacking();
                if (packing is not null)
                {
                    if (packing.Phase is "published" or "relocated") live = index.Set(live, PackKey(packing.Target), []);
                    if (packing.Candidates is { } candidates)
                    {
                        using var jobs = new PagedMetadataStore(PackingIndex(packing));
                        // Future units consume this same fixed plan. Later
                        // writes cannot make their frozen proof disappear.
                        foreach (var candidate in jobs.Open(candidates).Enumerate(packing.AfterSource))
                            live = index.Set(live, candidate.Key, []);
                    }
                }
                var dead = index.Empty; var remaining = catalog; long bytes = 0, payload = 0, count = 0, dataBytes = 0, dataPayload = 0;
                foreach (var item in index.Open(catalog).Enumerate())
                {
                    ct.ThrowIfCancellationRequested(); if (index.Open(live).Find(item.Key) is not null) continue;
                    var segment = DecodeSegment(item.Value.Span);
                    if (segment.Id >= 0) { bytes = checked(bytes + segment.Length); payload = checked(payload + segment.PayloadBytes); count++; }
                    else { dataBytes = checked(dataBytes + segment.Length); dataPayload = checked(dataPayload + segment.PayloadBytes); }
                    dead = index.Set(dead, item.Key, item.Value.Span); remaining = index.Remove(remaining, item.Key);
                }
                if (dead.Count == 0) return;
                index.Flush();
                using (EnterGate(mutation: true))
                {
                    Check();
                    // Newly accepted writes cannot resurrect a retired segment.
                    // A new capture or roll can add protection/catalog entries;
                    // retry their mark outside this boundary before publication.
                    if (captured != captureProtectionGeneration || head != segments!.ActiveId || segmentRoot!.ContentHash != catalog.ContentHash) continue;
                    FlushPendingWrites();
                    plan = new(3, PagedOwnerHash(Identity), pagedIndexDirectory!, dead, catalog.ContentHash, remaining, bytes, count, payload, dataBytes, dataPayload);
                    plan = plan with { Digest = JournalControlIntegrity.Digest(plan with { Digest = "" }) };
                    PublishSegmentCollection(intentPath, plan);
                    captureSegmentRoot = remaining;
                    boundary?.Invoke("segments.reclaim-intent");
                    break;
                }
            }
        }
        using var records = new PagedMetadataStore(plan.Directory);
        RestoreCaptureSegmentRoot(plan);
        // Close retained backing handles before either a fresh or resumed
        // collection reaches its physical unlink boundary.
        using (EnterGate()) CloseSegmentReaders();
        // Logical retirement prevents new captures from claiming old aliases.
        // Physical accounting is reduced only after every exact alias is gone.
        foreach (var item in records.Open(plan.Dead).Enumerate())
        {
            ct.ThrowIfCancellationRequested(); var descriptor = DecodeSegment(item.Value.Span);
            string path = segments!.SegmentPath(descriptor.Id); WorkspaceStore.EnsurePlainPath(path);
            if (!File.Exists(path)) continue;
            if (descriptor.NativeIdentity is null) throw new WorkspaceException("ContentReclaimUnsupported", "Compose native source segment identities before live retirement.");
            try
            {
                using (var input = segments.OpenSealed(descriptor)) sourceFilePins!.Verify(input, descriptor.NativeIdentity);
                sourceFilePins!.Unpin(path, descriptor.NativeIdentity);
            }
            catch (WorkspaceException ex) when (ex.Code == "ImmutableContentReaderBusy") { return; }
            catch (IOException ex) when ((ex.HResult & 0xffff) is 32 or 33) { return; }
            boundary?.Invoke("segments.reclaim-file");
        }
        while (true)
        {
            MetadataTreeRoot current; long head;
            using (EnterGate()) { Check(); current = segmentRoot!; head = segments!.ActiveId; }
            var next = current; long present = 0;
            foreach (var item in records.Open(plan.Dead).Enumerate())
                if (pagedIndex!.Open(next).Find(item.Key) is not null) { next = pagedIndex.Remove(next, item.Key); present++; }
            pagedIndex!.Flush();
            if (present == 0) break; // Published before an interrupted receipt deletion.
            if (present != plan.Dead.Count) throw PagedInvalid();
            using (EnterGate(mutation: true))
            {
                Check(); if (head != segments!.ActiveId || current.ContentHash != segmentRoot!.ContentHash) continue;
                FlushPendingWrites(); dataSegmentBytes = checked(dataSegmentBytes - plan.DataBytes); dataPayloadBytes = checked(dataPayloadBytes - plan.DataPayload);
                segments.Rotate(sealedSegment =>
                {
                    segmentRoot = pagedIndex.Set(next, PackKey(sealedSegment.Id), EncodeSegment(sealedSegment));
                    captureSegmentRoot = null; pagedIndex.Flush(); return FrozenJournalRoots();
                }, plan.Bytes, plan.Count, plan.PayloadBytes);
                journal = segments.Active; expectedJournalBytes = journal.Length; segmentFrames = 0;
                durablyFlushedJournal = journal; durablyFlushedBytes = journal.Length; break;
            }
        }
        using (EnterGate()) captureSegmentRoot = null;
        boundary?.Invoke("segments.reclaim-roots");
        File.Delete(intentPath);
        boundary?.Invoke("segments.reclaim-complete");
    }
    private void ValidateSegmentCollection(SegmentCollection plan, string delta)
    {
        if (plan.Version != 3 || plan.Digest != JournalControlIntegrity.Digest(plan with { Digest = "" }) || plan.Owner != PagedOwnerHash(Identity) ||
            !WorkspaceStore.SamePath(Path.GetDirectoryName(plan.Directory)!, delta) || plan.Bytes < 0 || plan.Count < 0 || plan.PayloadBytes < 0 ||
            plan.DataBytes < 0 || plan.DataPayload < 0 || plan.PayloadBytes > plan.Bytes || plan.DataPayload > plan.DataBytes) throw PagedInvalid();
    }
    private void RestoreCaptureSegmentRoot(SegmentCollection? supplied = null)
    {
        string delta = Path.GetDirectoryName(journal.Name)!, path = Path.Combine(delta, SegmentCollectionName);
        if (supplied is null && !File.Exists(path)) return;
        var plan = supplied ?? JournalControlIntegrity.Read<SegmentCollection>(path); ValidateSegmentCollection(plan, delta);
        using var records = new PagedMetadataStore(plan.Directory);
        while (true)
        {
            MetadataTreeRoot current; using (EnterGate()) current = segmentRoot!;
            var next = current;
            foreach (var item in records.Open(plan.Dead).Enumerate()) next = pagedIndex!.Remove(next, item.Key);
            using (EnterGate())
            { if (segmentRoot!.ContentHash != current.ContentHash) continue; captureSegmentRoot = next; break; }
        }
    }
    private static void PublishSegmentCollection(string path, SegmentCollection plan)
    {
        plan = plan with { Digest = JournalControlIntegrity.Digest(plan with { Digest = "" }) };
        string pending = path + ".pending";
        using (var output = new FileStream(pending, FileMode.Create, FileAccess.Write, FileShare.Read))
        { JsonSerializer.Serialize(output, plan, JournalControlIntegrity.Json); output.Flush(true); }
        AtomicFilePublication.Publish(pending, path);
    }
}
