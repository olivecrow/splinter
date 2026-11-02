using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Splinter.Core;

namespace Splinter.Runtime;

// Content identities name fixed raw byte ranges, never mutable source paths.
// Canonical backing names are owned hard links to capture-sealed segments.
public sealed partial class OwnedRangeContentStore : IDisposable
{
    private readonly string home, directory;
    private readonly IJournalFilePins pins;
    private readonly Action<string>? boundary;
    private readonly object proofGate = new();
    private sealed record BackingProof(string Key, FileStream Stream)
    {
        internal readonly HashSet<(long Offset, int Length, string Hash)> Ranges = new();
    }
    private readonly LinkedList<BackingProof> proofs = new();
    internal long BackingProofAdmissions { get; private set; }
    internal long RangeProofAdmissions { get; private set; }
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { RespectRequiredConstructorParameters = true, RespectNullableAnnotations = true, UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow };
    private sealed record Catalog(int Version, string StoreId, long Sequence, MetadataTreeRoot Objects, MetadataTreeRoot References, MetadataTreeRoot Backings,
        MetadataTreeRoot PreservedScratch);
    private sealed record Range(string Backing, long Offset, int Length);
    private sealed record Backing(string? Identity, string Hash, long Length, long Objects, string Phase);
    public OwnedRangeContentStore(string home, IJournalFilePins pins, Action<string>? boundary = null)
    { this.home = Path.GetFullPath(home); directory = Path.Combine(this.home, "range-store"); this.pins = pins; this.boundary = boundary; }
    private string Metadata => Path.Combine(directory, "pages");
    public bool HasPublishedCatalog => File.Exists(Path.Combine(directory, "catalog.json"));
    private string BackingPath(string key) => Path.Combine(directory, "backings", key);
    private static string Key(string hash, string id) => hash + "/" + id;
    public sealed record Preservation(string Hash, int Length, CapturedImmutableRangeBacking Source);
    public const int MaximumPreservationBatch = 64;
    public void Preserve(WorkspaceRecord w, string control, string hash, int length, CapturedImmutableRangeBacking source, CancellationToken ct = default)
        => PreserveBatch(w, control, [new(hash, length, source)], ct);
    public void PreserveBatch(WorkspaceRecord w, string control, IReadOnlyList<Preservation> requests, CancellationToken ct = default)
    {
        RequireOwner(w, control);
        if (requests.Count is < 1 or > MaximumPreservationBatch) throw new ArgumentOutOfRangeException(nameof(requests));
        var items = requests.ToArray();
        foreach (var request in items)
        {
            RequireHash(request.Hash); var source = request.Source;
            if (request.Length < 1 || source.Offset < 0 || source.Offset > source.MinimumLength - request.Length || source.ExpectedBackingHash is null ||
                w.CaptureIntent?.Id != source.CaptureId || source.CaptureDigest.Length != 64 || source.FileIdentity.Length == 0 ||
                !WorkspaceStore.SamePath(Path.GetDirectoryName(Path.GetFullPath(source.Path))!, Path.Combine(control, "captures", source.CaptureId)))
                throw Invalid("Only an exact fixed capture's sealed raw segment may be adopted.");
            RequireHash(source.ExpectedBackingHash);
        }
        lock (proofGate)
        {
            var sourceFences = new Dictionary<string, FileStream>(StringComparer.Ordinal);
            try
            {
            // Admit source bytes before taking the shared catalog lock. Exact
            // bounded native fences retain that proof through publication even
            // when the small backing-proof LRU evicts an older handle.
            foreach (var request in items)
            {
                var source = request.Source;
                if (!sourceFences.ContainsKey(source.Path))
                {
                    WorkspaceStore.EnsurePlainPath(source.Path);
                    var fence = new FileStream(source.Path, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.RandomAccess);
                    try { pins.Verify(fence, source.FileIdentity); sourceFences.Add(source.Path, fence); }
                    catch { fence.Dispose(); throw; }
                }
                VerifySource(request);
            }
            using var held = Lock(ct);
            if (AlreadyReferenced(w, items, ct)) return;
            using var pages = new PagedMetadataStore(Metadata, true, filePins: pins); var catalog = Load(pages);
            foreach (var request in items)
            {
            ct.ThrowIfCancellationRequested();
            string hash = request.Hash; int length = request.Length; var source = request.Source;
            var raw = pages.Open(catalog.Objects).Find(hash);
            if (raw is not null)
            {
                var prior = DecodeRange(raw); if (prior.Length != length) throw Invalid("One immutable range cannot have different lengths.");
                using var verified = OpenLocked(pages, catalog, hash, prior, ct);
            }
            else
            {
                string key = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(source.FileIdentity + "\n" + source.ExpectedBackingHash)));
                var backingEntry = pages.Open(catalog.Backings).FindEntry(key);
                var backing = backingEntry is null ? new Backing(source.FileIdentity, source.ExpectedBackingHash!, source.MinimumLength, 0, "pinning") : DecodeBacking(backingEntry.Value.ToArray());
                var members = backingEntry?.Child ?? pages.Empty;
                if (members.Count != backing.Objects) throw Invalid("The sealed backing membership index is incomplete.");
                if (backing.Identity != source.FileIdentity || backing.Hash != source.ExpectedBackingHash || backing.Length != source.MinimumLength || backing.Phase == "reclaiming")
                    throw Invalid("The sealed backing differs from its owned range intent.");
                if (backingEntry is null)
                { catalog = catalog with { Backings = pages.Set(catalog.Backings, key, Encode(backing)) }; pages.Flush(); catalog = Save(catalog); boundary?.Invoke("range-backing-intent"); }
                _ = OperationDiskSpace.Require("pin-immutable-range", BackingPath(key), 0, 0, OperationDiskSpace.ControlBytes(catalog) + Encode(backing).Length);
                Directory.CreateDirectory(Path.Combine(directory, "backings")); pins.Pin(source.Path, BackingPath(key), source.FileIdentity);
                boundary?.Invoke("range-backing-pinned");
                var item = new Range(key, source.Offset, length);
                backing = backing with { Objects = checked(backing.Objects + 1), Phase = "ready" };
                members = pages.Set(members, hash, ReadOnlySpan<byte>.Empty);
                catalog = catalog with { Objects = pages.Set(catalog.Objects, hash, Encode(item)), Backings = pages.Set(catalog.Backings, key, Encode(backing), members) };
            }
            byte[]? reference = pages.Open(catalog.References).Find(Key(hash, w.Id));
            if (reference is not null && Encoding.UTF8.GetString(reference) != w.RequestDigest) throw Invalid("The range reference belongs to another task request.");
            if (reference is null) catalog = catalog with { References = pages.Set(catalog.References, Key(hash, w.Id), Encoding.UTF8.GetBytes(w.RequestDigest!)) };
            }
            // Pin intents above remain independently durable. One bounded
            // object/reference root becomes visible only after all its pages.
            pages.Flush(); boundary?.Invoke("range-object-pages-flushed"); _ = Save(catalog); boundary?.Invoke("range-object-published");
            }
            finally { foreach (var fence in sourceFences.Values) fence.Dispose(); }
        }
        void VerifySource(Preservation request)
        {
            ct.ThrowIfCancellationRequested(); var proof = Proof(request.Source, ct);
            var range = (request.Source.Offset, request.Length, request.Hash);
            if (proof.Ranges.Contains(range)) return;
            if (HashRange(proof.Stream, request.Source.Offset, request.Length, ct) != request.Hash)
                throw Invalid("Captured raw bytes differ from their fixed content identity.");
            RangeProofAdmissions++;
            // The exact backing's no-write/no-delete handle remains held. This
            // bounded memo dies on eviction/disposal, never crosses an open gap.
            if (proof.Ranges.Count < 1024) proof.Ranges.Add(range);
        }
    }
    // The outer operation lock fixes this root throughout the proof. A ready
    // object/reference pair needs no metadata writer, new catalog generation or
    // repeat durable flush. Source and retained backing byte proofs still run.
    private bool AlreadyReferenced(WorkspaceRecord w, IReadOnlyList<Preservation> requests, CancellationToken ct)
    {
        if (!HasPublishedCatalog) return false;
        using var pages = new PagedMetadataStore(Metadata); var catalog = Load(pages);
        var proofs = new Dictionary<string, Stream>(StringComparer.Ordinal);
        try
        {
            foreach (var request in requests)
            {
                ct.ThrowIfCancellationRequested(); string hash = request.Hash; int length = request.Length;
                if (proofs.TryGetValue(hash, out var retained))
                { if (retained.Length != length) throw Invalid("One immutable range cannot have different lengths."); continue; }
                var found = pages.Open(catalog.Objects).Find(hash); if (found is null) return false;
                var range = DecodeRange(found); if (range.Length != length) throw Invalid("One immutable range cannot have different lengths.");
                var reference = pages.Open(catalog.References).Find(Key(hash, w.Id)); if (reference is null) return false;
                if (Encoding.UTF8.GetString(reference) != w.RequestDigest) throw Invalid("The range reference belongs to another task request.");
                var backing = pages.Open(catalog.Backings).FindEntry(range.Backing) ?? throw Invalid("The range backing ownership is missing.");
                if (DecodeBacking(backing.Value.ToArray()).Phase != "ready") return false;
                proofs.Add(hash, OpenLocked(pages, catalog, hash, range, ct));
                boundary?.Invoke("range-existing-proof-admitted");
            }
            return true;
        }
        finally { foreach (var proof in proofs.Values) proof.Dispose(); }
    }
    public bool Contains(WorkspaceRecord w, string control, string hash, int length, CancellationToken ct = default)
    {
        RequireOwner(w, control); RequireHash(hash); if (!File.Exists(Path.Combine(directory, "catalog.json"))) return false;
        using var held = Lock(ct); using var pages = new PagedMetadataStore(Metadata); var catalog = Load(pages);
        var raw = pages.Open(catalog.Objects).Find(hash); if (raw is null) return false;
        var value = DecodeRange(raw); if (value.Length != length) throw Invalid("Immutable range length differs from its identity.");
        byte[]? reference = pages.Open(catalog.References).Find(Key(hash, w.Id));
        return reference is not null && Encoding.UTF8.GetString(reference) == w.RequestDigest;
    }
    public bool TryReferenceExisting(WorkspaceRecord w, string control, string hash, int length, CancellationToken ct = default)
    {
        RequireOwner(w, control); RequireHash(hash);
        if (length < 1) throw Invalid("An immutable raw reference must have a positive length.");
        if (!HasPublishedCatalog) return false;
        using var held = Lock(ct); using var pages = new PagedMetadataStore(Metadata, true, filePins: pins); var catalog = Load(pages);
        var found = pages.Open(catalog.Objects).Find(hash); if (found is null) return false;
        var range = DecodeRange(found); if (range.Length != length) throw Invalid("An existing immutable range has another length.");
        // OpenLocked proves native ownership, complete backing membership and
        // exact chunk bytes before the new durable reference is published.
        using var proof = OpenLocked(pages, catalog, hash, range, ct);
        string key = Key(hash, w.Id); var prior = pages.Open(catalog.References).Find(key);
        if (prior is not null && Encoding.UTF8.GetString(prior) != w.RequestDigest) throw Invalid("The immutable raw reference belongs to another task request.");
        if (prior is null)
        {
            catalog = catalog with { References = pages.Set(catalog.References, key, Encoding.UTF8.GetBytes(w.RequestDigest!)) };
            pages.Flush(); boundary?.Invoke("range-existing-reference-durable"); _ = Save(catalog);
            boundary?.Invoke("range-existing-reference-published");
        }
        return true;
    }
    public Stream Open(WorkspaceRecord w, string control, string hash, int length, CancellationToken ct = default)
        => OpenCore(w, control, hash, length, ct);
    internal Stream OpenVerified(WorkspaceRecord w, string control, string hash, int length, ImmutableReadProofs proofs, CancellationToken ct)
        => OpenCore(w, control, hash, length, ct, proofs);
    public Stream Open(WorkspaceRecord w, string control, string hash, CancellationToken ct = default)
        => OpenCore(w, control, hash, null, ct);
    private Stream OpenCore(WorkspaceRecord w, string control, string hash, int? length, CancellationToken ct, ImmutableReadProofs? readProofs = null)
    {
        RequireOwner(w, control); RequireHash(hash); using var held = Lock(ct, readProofs);
        using var reader = ImmutableReadProofs.OpenMetadata(Metadata, readProofs); var pages = reader.Pages; var catalog = Load(pages, readProofs);
        var raw = pages.Open(catalog.Objects).Find(hash) ?? throw Invalid("The immutable range object is missing."); var range = DecodeRange(raw);
        if (length is not null && range.Length != length) throw Invalid("The immutable range length differs.");
        var reference = pages.Open(catalog.References).Find(Key(hash, w.Id));
        if (reference is null || Encoding.UTF8.GetString(reference) != w.RequestDigest) throw Invalid("The immutable range reference is missing or foreign.");
        return OpenLocked(pages, catalog, hash, range, ct, readProofs);
    }
    public int LengthOf(WorkspaceRecord w, string control, string hash, CancellationToken ct = default)
    {
        RequireOwner(w, control); RequireHash(hash); using var held = Lock(ct); using var pages = new PagedMetadataStore(Metadata); var catalog = Load(pages);
        var range = DecodeRange(pages.Open(catalog.Objects).Find(hash) ?? throw Invalid("The immutable range is missing."));
        var reference = pages.Open(catalog.References).Find(Key(hash, w.Id));
        if (reference is null || Encoding.UTF8.GetString(reference) != w.RequestDigest) throw Invalid("The immutable range reference is missing or foreign.");
        return range.Length;
    }
    private Stream OpenLocked(PagedMetadataStore pages, Catalog catalog, string hash, Range range, CancellationToken ct, ImmutableReadProofs? readProofs = null)
    {
        var backingEntry = pages.Open(catalog.Backings).FindEntry(range.Backing) ?? throw Invalid("The range backing ownership is missing.");
        var backing = DecodeBacking(backingEntry.Value.ToArray());
        if (backingEntry.Child?.Count != backing.Objects || backingEntry.Child is null || pages.Open(backingEntry.Child).Find(hash) is null)
            throw Invalid("The backing membership is incomplete or inconsistent.");
        if (backing.Phase != "ready" || range.Offset > backing.Length - range.Length) throw Invalid("The immutable range is outside its owned backing.");
        string path = BackingPath(range.Backing); ImmutableReadProofs.EnsurePlainPath(path, readProofs);
        var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.RandomAccess);
        try
        {
            pins.Verify(input, backing.Identity!);
            if (input.Length != backing.Length) throw Invalid("The independently retained backing length changed.");
            if (readProofs is null)
            { if (HashRange(input, range.Offset, range.Length, ct) != hash) throw Invalid("The independently retained range changed."); }
            else readProofs.Verify(input, backing.Identity!, range.Offset, range.Length, hash, ct);
            return new Slice(input, range.Offset, range.Length);
        }
        catch { input.Dispose(); throw; }
    }
    private BackingProof Proof(CapturedImmutableRangeBacking source, CancellationToken ct)
    {
        string key = source.FileIdentity + "/" + source.ExpectedBackingHash;
        var found = proofs.First;
        while (found is not null && found.Value.Key != key) found = found.Next;
        if (found is not null)
        {
            if (found.Value.Stream.Length != source.MinimumLength) throw Invalid("The sealed segment length changed.");
            proofs.Remove(found); proofs.AddLast(found); return found.Value;
        }
        WorkspaceStore.EnsurePlainPath(source.Path);
        var input = new FileStream(source.Path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536);
        try
        {
            pins.Verify(input, source.FileIdentity); ct.ThrowIfCancellationRequested();
            if (input.Length != source.MinimumLength || PayloadHash.Sha256(input, ct) != source.ExpectedBackingHash)
                throw Invalid("The sealed segment differs from the capture's durable checksum.");
            BackingProofAdmissions++;
            if (proofs.Count == 8) { proofs.First!.Value.Stream.Dispose(); proofs.RemoveFirst(); }
            var proof = new BackingProof(key, input); proofs.AddLast(proof); return proof;
        }
        catch { input.Dispose(); throw; }
    }
    public void PruneUnreferenced(WorkspaceRecord w, string control, Func<string, bool> required, CancellationToken ct = default)
    {
        RequireOwner(w, control); if (!File.Exists(Path.Combine(directory, "catalog.json"))) return;
        using var candidates = new MetadataSpool(directory);
        using (var held = Lock(ct))
        using (var pages = new PagedMetadataStore(Metadata, true, filePins: pins))
        {
            var catalog = Load(pages);
            foreach (var entry in pages.Open(catalog.Objects).Enumerate())
                if (pages.Open(catalog.References).Find(Key(entry.Key, w.Id)) is not null) candidates.Append(new(entry.Key, ReadOnlyMemory<byte>.Empty));
        }
        foreach (var entry in candidates.Read())
        {
            ct.ThrowIfCancellationRequested(); if (required(entry.Key)) continue;
            using var held = Lock(ct); using var pages = new PagedMetadataStore(Metadata, true, filePins: pins); var catalog = Load(pages);
            byte[]? reference = pages.Open(catalog.References).Find(Key(entry.Key, w.Id)); if (reference is null) continue;
            if (Encoding.UTF8.GetString(reference) != w.RequestDigest) throw Invalid("Preserve a foreign range reference.");
            catalog = catalog with { References = pages.Remove(catalog.References, Key(entry.Key, w.Id)) };
            string prefix = entry.Key + "/";
            if (!pages.Open(catalog.References).Enumerate(prefix).TakeWhile(e => e.Key.StartsWith(prefix, StringComparison.Ordinal)).Any())
            {
                var range = DecodeRange(pages.Open(catalog.Objects).Find(entry.Key) ?? throw Invalid("Range ownership disappeared during reclamation."));
                var backingEntry = pages.Open(catalog.Backings).FindEntry(range.Backing) ?? throw Invalid("Backing ownership disappeared during reclamation.");
                var backing = DecodeBacking(backingEntry.Value.ToArray());
                if (backing.Objects < 1 || backingEntry.Child?.Count != backing.Objects) throw Invalid("The range backing count is inconsistent.");
                catalog = catalog with { Objects = pages.Remove(catalog.Objects, entry.Key), Backings = pages.Set(catalog.Backings, range.Backing, Encode(backing with { Objects = backing.Objects - 1 }), pages.Remove(backingEntry.Child!, entry.Key)) };
            }
            pages.Flush(); _ = Save(catalog);
        }
        ReclaimEmptyBackings(ct);
        CompactClosedBackings(ct);
    }
    public void ReclaimEmptyBackings(CancellationToken ct = default)
    {
        if (!File.Exists(Path.Combine(directory, "catalog.json"))) return;
        using var candidates = new MetadataSpool(directory);
        using (var held = Lock(ct))
        using (var pages = new PagedMetadataStore(Metadata, true, filePins: pins))
        {
            var catalog = Load(pages);
            foreach (var entry in pages.Open(catalog.Backings).Enumerate())
                if (DecodeBacking(entry.Value.ToArray()) is { Objects: 0, Phase: "ready" or "reclaiming" or "packing" or "allocating" }) candidates.Append(entry);
        }
        foreach (var candidate in candidates.Read())
        {
            ct.ThrowIfCancellationRequested(); using var held = Lock(ct); using var pages = new PagedMetadataStore(Metadata, true, filePins: pins); var catalog = Load(pages);
            var backingEntry = pages.Open(catalog.Backings).FindEntry(candidate.Key) ?? throw Invalid("A backing reclaim intent disappeared.");
            var backing = DecodeBacking(backingEntry.Value.ToArray());
            if (backing.Objects != 0) continue;
            string path = BackingPath(candidate.Key);
            if (backing.Phase == "allocating")
            {
                if (File.Exists(path)) catalog = catalog with { PreservedScratch = pages.Set(catalog.PreservedScratch, candidate.Key,
                    Encoding.UTF8.GetBytes("A pack stage was created before its native identity receipt; preserve this unowned file.")) };
                catalog = catalog with { Backings = pages.Remove(catalog.Backings, candidate.Key) }; pages.Flush(); _ = Save(catalog); continue;
            }
            if (backingEntry.Child is { Count: > 0 } || pages.Open(catalog.Objects).Enumerate().Any(entry => DecodeRange(entry.Value.ToArray()).Backing == candidate.Key))
                throw Invalid("A supposedly empty backing still owns immutable ranges.");
            if (backing.Phase != "reclaiming") { backing = backing with { Phase = "reclaiming" }; catalog = catalog with { Backings = pages.Set(catalog.Backings, candidate.Key, Encode(backing)) }; pages.Flush(); catalog = Save(catalog); }
            if (File.Exists(path))
            {
                try { pins.Unpin(path, backing.Identity!); }
                catch (WorkspaceException error) when (error.Code == "ImmutableContentReaderBusy") { continue; }
            }
            boundary?.Invoke("range-backing-reclaimed");
            catalog = catalog with { Backings = pages.Remove(catalog.Backings, candidate.Key) }; pages.Flush(); _ = Save(catalog);
        }
    }
    public void CompactClosedBackings(CancellationToken ct = default)
    {
        if (!File.Exists(Path.Combine(directory, "catalog.json"))) return;
        using var candidates = new MetadataSpool(directory);
        using (var held = Lock(ct))
        using (var pages = new PagedMetadataStore(Metadata, true, filePins: pins))
        {
            var catalog = Load(pages);
            foreach (var entry in pages.Open(catalog.Backings).Enumerate())
            {
                var backing = DecodeBacking(entry.Value.ToArray());
                if (backing.Phase == "ready" && backing.Objects > 0 && pins.LinkCount(BackingPath(entry.Key)) == 1) candidates.Append(new(entry.Key, ReadOnlyMemory<byte>.Empty));
            }
        }
        foreach (var candidate in candidates.Read())
        {
            ct.ThrowIfCancellationRequested(); using var held = Lock(ct); using var pages = new PagedMetadataStore(Metadata, true, filePins: pins); var catalog = Load(pages);
            var oldEntry = pages.Open(catalog.Backings).FindEntry(candidate.Key); if (oldEntry is null) continue;
            var old = DecodeBacking(oldEntry.Value.ToArray());
            if (old.Phase != "ready" || old.Objects == 0 || oldEntry.Child?.Count != old.Objects || pins.LinkCount(BackingPath(candidate.Key)) != 1) continue;
            using var sorted = new MetadataSpool(directory);
            IEnumerable<MetadataEntry> Ranges()
            {
                foreach (var member in pages.Open(oldEntry.Child!).Enumerate())
                {
                    var range = DecodeRange(pages.Open(catalog.Objects).Find(member.Key) ?? throw Invalid("A backing member is missing."));
                    if (range.Backing != candidate.Key || range.Offset > old.Length - range.Length) throw Invalid("A backing membership is inconsistent.");
                    yield return new(range.Offset.ToString("x16", System.Globalization.CultureInfo.InvariantCulture) + "/" + member.Key, Encode(range));
                }
            }
            long covered = 0, unionEnd = 0;
            foreach (var range in MetadataSort.Order(Ranges(), directory, ct))
            {
                var value = DecodeRange(range.Value.ToArray()); long end = checked(value.Offset + value.Length);
                covered = checked(covered + Math.Max(0, end - Math.Max(unionEnd, value.Offset))); unionEnd = Math.Max(unionEnd, end); sorted.Append(range);
            }
            // At most one allocation cluster of per-segment slack is retained;
            // tiny repacks do not create another file/flush for a few header bytes.
            if (old.Length - covered < 4096) continue;
            string sourcePath = BackingPath(candidate.Key); WorkspaceStore.EnsurePlainPath(sourcePath);
            using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.RandomAccess); pins.Verify(source, old.Identity!);
            if (source.Length != old.Length || PayloadHash.Sha256(source, ct) != old.Hash) throw Invalid("A closed backing changed before packing.");
            string newKey = Convert.ToHexStringLower(SHA256.HashData(Guid.NewGuid().ToByteArray())); string target = BackingPath(newKey); WorkspaceStore.EnsurePlainPath(target);
            var objects = catalog.Objects; byte[] buffer = new byte[65536]; long unionStart = 0, targetStart = 0, copiedEnd = 0;
            string identity, hash; long packedLength;
            var allocation = new Backing(null, new string('0', 64), 0, 0, "allocating");
            catalog = catalog with { Backings = pages.Set(catalog.Backings, newKey, Encode(allocation)) }; pages.Flush(); catalog = Save(catalog);
            boundary?.Invoke("range-pack-attempt-owned");
            using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read, 65536))
            {
                boundary?.Invoke("range-pack-created-before-receipt"); identity = pins.IdentifyOpened(output);
                var pending = new Backing(identity, new string('0', 64), 0, 0, "packing");
                catalog = catalog with { Backings = pages.Set(catalog.Backings, newKey, Encode(pending)) }; pages.Flush(); catalog = Save(catalog);
                boundary?.Invoke("range-pack-stage-owned");
                _ = OperationDiskSpace.Require("pack-immutable-ranges", target, covered, pins.AllocatedBytes(output), OperationDiskSpace.ControlBytes(catalog) + Encode(pending).Length);
                foreach (var entry in sorted.Read())
                {
                    ct.ThrowIfCancellationRequested(); var range = DecodeRange(entry.Value.ToArray()); long end = checked(range.Offset + range.Length);
                    if (range.Offset > copiedEnd || output.Length == 0) { unionStart = range.Offset; targetStart = output.Length; copiedEnd = range.Offset; }
                    long packedOffset = checked(targetStart + range.Offset - unionStart);
                    for (long at = Math.Max(copiedEnd, range.Offset); at < end;)
                    {
                        int count = RandomAccess.Read(source.SafeFileHandle, buffer.AsSpan(0, (int)Math.Min(buffer.Length, end - at)), at);
                        if (count <= 0) throw Invalid("The closed backing ended early during packing."); output.Position = output.Length; output.Write(buffer, 0, count); at += count;
                    }
                    copiedEnd = Math.Max(copiedEnd, end); string member = entry.Key[17..];
                    objects = pages.Set(objects, member, Encode(range with { Backing = newKey, Offset = packedOffset }));
                }
                output.Flush(true); packedLength = output.Length;
                boundary?.Invoke("range-pack-payload-durable");
                if (packedLength != covered) throw Invalid("The packed backing length differs from its complete range union.");
                output.Position = 0; hash = Convert.ToHexStringLower(SHA256.HashData(output));
                foreach (var member in pages.Open(oldEntry.Child!).Enumerate())
                {
                    var range = DecodeRange(pages.Open(objects).Find(member.Key) ?? throw Invalid("A packed object disappeared."));
                    if (HashRange(output, range.Offset, range.Length, ct) != member.Key) throw Invalid("A packed object changed its immutable content identity.");
                }
            }
            var packed = new Backing(identity, hash, packedLength, old.Objects, "ready");
            catalog = catalog with
            {
                Objects = objects,
                Backings = pages.Set(pages.Set(catalog.Backings, candidate.Key, Encode(old with { Objects = 0 })), newKey, Encode(packed), oldEntry.Child)
            };
            pages.Flush(); boundary?.Invoke("range-pack-pages-flushed"); _ = Save(catalog); boundary?.Invoke("range-pack-published");
        }
        ReclaimEmptyBackings(ct);
    }
    public (string Directory, MetadataTreeRoot[] Roots)? ReadMetadataRoots(CancellationToken ct = default)
    {
        if (!File.Exists(Path.Combine(directory, "catalog.json"))) return null;
        using var held = Lock(ct); using var pages = new PagedMetadataStore(Metadata); var catalog = Load(pages);
        return (Metadata, [catalog.Objects, catalog.References, catalog.Backings, catalog.PreservedScratch]);
    }
    public sealed record PreservedRangeScratch(string RelativePath, string Reason);
    public sealed record PreservedRangeScratchPage(MetadataTreeRoot? Root, PreservedRangeScratch[] Entries, MetadataCursor? NextCursor);
    public PreservedRangeScratchPage ReadPreservedScratchPage(MetadataCursor? cursor = null, int pageSize = 128, CancellationToken ct = default)
    {
        if (!File.Exists(Path.Combine(directory, "catalog.json")))
        { if (cursor is not null) throw Invalid("The requested scratch cursor has no existing range store."); return new(null, [], null); }
        using var held = Lock(ct); using var pages = new PagedMetadataStore(Metadata); var catalog = Load(pages);
        var page = pages.Open(catalog.PreservedScratch).ReadPage(cursor, pageSize);
        var rows = page.Entries.Select(entry =>
        {
            RequireHash(entry.Key); if (entry.Child is not null) throw Invalid("An unknown pack-stage observation is malformed.");
            return new PreservedRangeScratch("range-store/backings/" + entry.Key, new UTF8Encoding(false, true).GetString(entry.Value.Span));
        }).ToArray();
        return new(page.Root, rows, page.NextCursor);
    }
    public sealed record DeferredRangeRetirement(string RelativePath, string NativeIdentity, long LogicalBytes);
    public sealed record DeferredRangeRetirementPage(MetadataTreeRoot? Root, DeferredRangeRetirement[] Entries, MetadataCursor? NextCursor);
    public DeferredRangeRetirementPage ReadDeferredReclamationPage(MetadataCursor? cursor = null, int pageSize = 128, CancellationToken ct = default)
    {
        if (!File.Exists(Path.Combine(directory, "catalog.json")))
        { if (cursor is not null) throw Invalid("The deferred cursor has no existing range store."); return new(null, [], null); }
        using var held = Lock(ct); using var pages = new PagedMetadataStore(Metadata); var catalog = Load(pages);
        var page = pages.Open(catalog.Backings).ReadPage(cursor, pageSize); var entries = new List<DeferredRangeRetirement>();
        foreach (var entry in page.Entries)
        {
            var value = DecodeBacking(entry.Value.ToArray());
            if (value.Phase == "reclaiming" && value.Objects == 0) entries.Add(new("range-store/backings/" + entry.Key, value.Identity!, value.Length));
        }
        return new(page.Root, entries.ToArray(), page.NextCursor);
    }
    private void RequireOwner(WorkspaceRecord w, string control)
    {
        if (w.Id.Length != 24 || string.IsNullOrWhiteSpace(w.RequestDigest) || !WorkspaceStore.SamePath(control, Path.Combine(home, "records", w.Id)))
            throw Invalid("Require the exact task request and home for immutable ranges.");
    }
    private static void RequireHash(string hash)
    { if (hash.Length != 64 || hash.Any(c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f'))) throw Invalid("Invalid immutable range hash."); }
    private static string HashRange(FileStream file, long offset, int length, CancellationToken ct)
    {
        byte[] buffer = new byte[Math.Min(65536, Math.Max(length, 1))]; using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); int done = 0;
        while (done < length) { ct.ThrowIfCancellationRequested(); int count = RandomAccess.Read(file.SafeFileHandle, buffer.AsSpan(0, Math.Min(buffer.Length, length - done)), offset + done); if (count <= 0) throw Invalid("A retained range ended early."); hash.AppendData(buffer.AsSpan(0, count)); done += count; }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
    private Catalog Load(PagedMetadataStore pages, ImmutableReadProofs? readProofs = null)
    {
        string path = Path.Combine(directory, "catalog.json"); ImmutableReadProofs.EnsurePlainPath(path, readProofs);
        if (!File.Exists(path))
        {
            if (Directory.EnumerateFiles(Metadata, "pages-*.bin").Any(file => new FileInfo(file).Length != 48)) throw Invalid("Preserve orphaned range metadata without its owner root.");
            var initial = new Catalog(3, pages.StoreId, 0, pages.Empty, pages.Empty, pages.Empty, pages.Empty); Publish(initial); return initial;
        }
        Catalog result;
        try
        {
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read); using var document = JsonDocument.Parse(input);
            if (!JsonPropertyValidation.HasUniqueProperties(document.RootElement)) throw Invalid("The range catalog repeats a control field.");
            result = document.RootElement.Deserialize<Catalog>(Json) ?? throw Invalid("The range catalog is missing.");
        }
        catch (JsonException) { throw Invalid("Preserve a malformed range catalog."); }
        if (result.Version != 3 || result.StoreId != pages.StoreId || result.Sequence < 0) throw Invalid("Unsupported or foreign range catalog.");
        _ = pages.Open(result.Objects); _ = pages.Open(result.References); _ = pages.Open(result.Backings); _ = pages.Open(result.PreservedScratch); return result;
    }
    private Catalog Save(Catalog catalog) { var next = catalog with { Sequence = checked(catalog.Sequence + 1) }; Publish(next); return next; }
    private void Publish(Catalog catalog)
    {
        string path = Path.Combine(directory, "catalog.json"), pending = path + "." + Guid.NewGuid().ToString("N") + ".tmp"; WorkspaceStore.EnsurePlainPath(pending);
        using (var output = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { JsonSerializer.Serialize(output, catalog, Json); output.Flush(true); }
        AtomicFilePublication.Publish(pending, path);
    }
    private FileStream Lock(CancellationToken ct, ImmutableReadProofs? readProofs = null)
    {
        ImmutableReadProofs.EnsurePlainPath(directory, readProofs); Directory.CreateDirectory(directory); string path = Path.Combine(directory, "operation.lock"); ImmutableReadProofs.EnsurePlainPath(path, readProofs);
        for (int attempt = 0; ; attempt++)
        {
            ct.ThrowIfCancellationRequested(); try { return new(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException error) when ((uint)error.HResult is 0x80070020 or 0x80070021)
            { if (attempt >= 500) throw new WorkspaceException("RangeStoreBusy", "Retry the same captured request after the range writer completes."); Thread.Sleep(10); }
        }
    }
    private static byte[] Encode(Range value)
    { byte[] data = new byte[45]; data[0] = 1; Convert.FromHexString(value.Backing).CopyTo(data, 1); BinaryPrimitives.WriteInt64LittleEndian(data.AsSpan(33), value.Offset); BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(41), value.Length); return data; }
    private static Range DecodeRange(byte[] data)
    { if (data.Length != 45 || data[0] != 1) throw Invalid("Malformed range descriptor."); var value = new Range(Convert.ToHexStringLower(data.AsSpan(1, 32)), BinaryPrimitives.ReadInt64LittleEndian(data.AsSpan(33)), BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(41))); if (value.Offset < 0 || value.Length < 1 || value.Offset > long.MaxValue - value.Length) throw Invalid("Invalid immutable range bounds."); return value; }
    private static byte[] Encode(Backing value)
    {
        using var memory = new MemoryStream(); using var output = new BinaryWriter(memory, Encoding.UTF8, true); output.Write((byte)2); output.Write(value.Phase switch { "pinning" => (byte)0, "ready" => (byte)1, "reclaiming" => (byte)2, "packing" => (byte)3, "allocating" => (byte)4, _ => throw Invalid("Invalid backing phase.") });
        output.Write(value.Length); output.Write(value.Objects); byte[] identity = value.Identity is null ? [] : Encoding.UTF8.GetBytes(value.Identity); output.Write(identity.Length); output.Write(identity); output.Write(Convert.FromHexString(value.Hash)); return memory.ToArray();
    }
    private static Backing DecodeBacking(byte[] data)
    {
        try
        {
            using var memory = new MemoryStream(data, false); using var input = new BinaryReader(memory, Encoding.UTF8); if (input.ReadByte() != 2) throw Invalid("Unsupported range backing version.");
            string phase = input.ReadByte() switch { 0 => "pinning", 1 => "ready", 2 => "reclaiming", 3 => "packing", 4 => "allocating", _ => throw Invalid("Invalid backing phase.") }; long length = input.ReadInt64(), count = input.ReadInt64(); int bytes = input.ReadInt32();
            if (length < 0 || count < 0 || bytes < 0 || bytes > data.Length - memory.Position - 32 || phase == "allocating" != (bytes == 0)) throw Invalid("Invalid range backing fields.");
            string? identity = bytes == 0 ? null : new UTF8Encoding(false, true).GetString(input.ReadBytes(bytes)); byte[] hash = input.ReadBytes(32); if (hash.Length != 32 || memory.Position != memory.Length ||
                phase == "allocating" && (length != 0 || count != 0 || hash.Any(value => value != 0))) throw Invalid("Truncated range backing identity.");
            return new(identity, Convert.ToHexStringLower(hash), length, count, phase);
        }
        catch (Exception error) when (error is EndOfStreamException or DecoderFallbackException) { throw Invalid("Malformed immutable range backing."); }
    }
    public void Dispose() { lock (proofGate) { foreach (var proof in proofs) proof.Stream.Dispose(); proofs.Clear(); } }
    private sealed class Slice(FileStream input, long start, int length) : Stream
    {
        private long position;
        public override int Read(Span<byte> buffer) { int count = (int)Math.Min(buffer.Length, length - position); if (count <= 0) return 0; int read = RandomAccess.Read(input.SafeFileHandle, buffer[..count], start + position); if (read <= 0) throw Invalid("An immutable range ended early."); position += read; return read; }
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override long Length => length; public override long Position { get => position; set { if (value < 0 || value > length) throw new ArgumentOutOfRangeException(nameof(value)); position = value; } }
        public override bool CanRead => true; public override bool CanSeek => true; public override bool CanWrite => false;
        public override long Seek(long value, SeekOrigin origin) { Position = origin switch { SeekOrigin.Begin => value, SeekOrigin.Current => checked(position + value), SeekOrigin.End => checked(length + value), _ => throw new ArgumentOutOfRangeException(nameof(origin)) }; return position; }
        protected override void Dispose(bool disposing) { if (disposing) input.Dispose(); base.Dispose(disposing); }
        public override void Flush() { } public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
    private static WorkspaceException Invalid(string message) => new("RangeContentInvalid", message);
}
