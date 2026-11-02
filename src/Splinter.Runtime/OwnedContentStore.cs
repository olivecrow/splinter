using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Splinter.Core;

namespace Splinter.Runtime;

// The canonical name is never an original/workspace file. Durable references
// own private hard-link aliases; immutable historical results keep those links
// after their live workspace is reclaimed.
public sealed partial class OwnedContentStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true, RespectNullableAnnotations = true };
    private static readonly object GateRegistryLock = new();
    private static readonly Dictionary<string, LocalGate> GateRegistry = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    private sealed class LocalGate { internal readonly SemaphoreSlim Semaphore = new(1, 1); internal int Users; }
    private readonly string home, directory;
    private readonly IJournalFilePins pins;
    private readonly Action<string>? boundary;
    public OwnedContentStore(string home, IJournalFilePins pins, Action<string>? boundary = null)
    {
        this.home = Path.GetFullPath(home); directory = Path.Combine(this.home, "content-store"); this.pins = pins; this.boundary = boundary;
        WorkspaceStore.EnsurePlainPath(directory);
    }
    private sealed record Catalog(int Version, string StoreId, long Sequence, MetadataTreeRoot Objects, MetadataTreeRoot References, MetadataTreeRoot PreservedScratch);
    private sealed record ContentObject(int Version, string Hash, long Length, string? Identity, string Phase, string Nonce);
    private sealed record ContentReference(int Version, string WorkspaceId, string RequestDigest, string Identity, string Phase);
    private string Metadata => Path.Combine(directory, "pages");
    private string Payload(string hash, string? nonce = null) => Path.Combine(directory, "objects", hash[..2], hash + (nonce is null ? "" : "-" + nonce + ".pending"));
    private string Alias(string control, string hash) => Path.Combine(control, "files", "objects", hash);
    private void Require(WorkspaceRecord w, string control, string hash, long length, bool inspectPath = true)
    {
        if (hash.Length != 64 || hash.Any(c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f')) || length < 0 ||
            string.IsNullOrWhiteSpace(w.RequestDigest) || w.Id.Length != 24 || w.Id.Any(c => !char.IsAsciiHexDigit(c)) ||
            !WorkspaceStore.SamePath(control, Path.Combine(home, "records", w.Id))) throw Invalid("The content reference has no exact workspace owner.");
        if (inspectPath) WorkspaceStore.EnsurePlainPath(control);
    }
    public void Preserve(WorkspaceRecord w, string control, string hash, long length, Action<FileStream> writePending, CancellationToken ct = default)
    {
        Require(w, control, hash, length);
        string canonical = Payload(hash); ContentObject item; long minimumControlBytes;
        using (var held = Lock(ct))
        using (var pages = new PagedMetadataStore(Metadata, true, filePins: pins))
        {
            var catalog = Load(pages);
            var existing = pages.Open(catalog.Objects).Find(hash);
            if (existing is null)
            {
                WorkspaceStore.EnsurePlainPath(canonical);
                if (Path.Exists(canonical)) throw Invalid("Preserve an unknown occupied canonical content name.");
                item = new(2, hash, length, null, "allocating", Guid.NewGuid().ToString("N"));
                catalog = catalog with { Objects = pages.Set(catalog.Objects, hash, Encode(item)) }; pages.Flush(); catalog = Save(catalog);
            }
            else item = DecodeObject(existing, hash, length);
            if (item.Phase == "reclaiming")
            {
                // A newly fixed capture can make an exact owned immutable
                // object live again while an old reader delays collection.
                // Never infer this from equal bytes at a foreign file name.
                if (Path.Exists(canonical))
                {
                    using var proof = OpenCanonical(canonical, item, ct);
                    if ((File.GetAttributes(canonical) & FileAttributes.ReadOnly) == 0) throw Invalid("The deferred immutable object's protection metadata changed.");
                    item = item with { Phase = "ready" };
                }
                else item = new(2, hash, length, null, "allocating", Guid.NewGuid().ToString("N"));
                catalog = catalog with { Objects = pages.Set(catalog.Objects, hash, Encode(item)) }; pages.Flush(); catalog = Save(catalog);
            }
            minimumControlBytes = OperationDiskSpace.ControlBytes(catalog) + Encode(item).Length;
            if (item.Phase == "allocating")
            {
                string candidate = Payload(hash, item.Nonce); WorkspaceStore.EnsurePlainPath(candidate);
                if (Path.Exists(candidate))
                {
                    // This attempt never durably acquired an identity. Retain
                    // its occupant without adopting or deleting it, then record
                    // a new nonce inside the same content-addressed request.
                    string relative = Path.GetRelativePath(directory, candidate).Replace('\\', '/');
                    var scratch = pages.Set(catalog.PreservedScratch, relative, Encoding.UTF8.GetBytes(w.Id + "\nidentity-not-recorded"));
                    item = item with { Nonce = Guid.NewGuid().ToString("N") };
                    catalog = catalog with { PreservedScratch = scratch, Objects = pages.Set(catalog.Objects, hash, Encode(item)) }; pages.Flush(); catalog = Save(catalog);
                    candidate = Payload(hash, item.Nonce);
                }
                Directory.CreateDirectory(Path.GetDirectoryName(candidate)!);
                using (var output = new FileStream(candidate, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                {
                    boundary?.Invoke("content-object-created-before-receipt");
                    item = item with { Identity = pins.Identify(candidate), Phase = "writing" };
                    var objects = pages.Set(catalog.Objects, hash, Encode(item)); pages.Flush(); Save(catalog with { Objects = objects });
                }
                boundary?.Invoke("content-object-owned");
            }
        }
        string pending = Payload(hash, item.Nonce);
        if (item.Phase == "writing")
        {
            FileStream? output = null;
            for (int attempt = 0; File.Exists(pending); attempt++)
            {
                ct.ThrowIfCancellationRequested();
                try { output = new FileStream(pending, FileMode.Open, FileAccess.ReadWrite, FileShare.Read | FileShare.Delete, 65536); break; }
                catch (FileNotFoundException) { break; }
                catch (IOException ex) when ((uint)ex.HResult is 0x80070020 or 0x80070021)
                {
                    if (attempt >= 500) throw new WorkspaceException("ContentStoreBusy", "Retry the same frozen request after its canonical writer completes.");
                    Thread.Sleep(10);
                }
            }
            using (output)
            {
                if (output is not null)
                {
                    pins.Verify(output, item.Identity!);
                    _ = OperationDiskSpace.Require("preserve-immutable-content", pending, length, pins.AllocatedBytes(output),
                        minimumControlBytes);
                    writePending(output); output.Flush(true);
                    output.Position = 0;
                    if (output.Length != item.Length || PayloadHash.Sha256(output, ct) != item.Hash)
                        throw Invalid("An incomplete immutable payload differs from its fixed content identity.");
                    boundary?.Invoke("content-payload-durable");
                }
                using var held = Lock(ct); using var pages = new PagedMetadataStore(Metadata, true, filePins: pins); var catalog = Load(pages);
                item = DecodeObject(pages.Open(catalog.Objects).Find(hash) ?? throw Invalid("The content object intent disappeared."), hash, length);
                if (item.Phase == "writing")
                {
                    if (File.Exists(pending))
                    {
                        if (Path.Exists(canonical)) throw Invalid("Preserve an occupant of the canonical result name.");
                        if (output is null || pins.Identify(pending) != item.Identity) throw Invalid("The incomplete canonical file has no owned writer.");
                        File.Move(pending, canonical); boundary?.Invoke("content-object-published");
                    }
                    // The active writer denies other writers and permits this
                    // identity observation/rename until its ready root commits.
                    if (pins.Identify(canonical) != item.Identity) throw Invalid("The canonical publication identity changed.");
                    if (output is null) Verify(canonical, item, ct);
                    File.SetAttributes(canonical, File.GetAttributes(canonical) | FileAttributes.ReadOnly);
                    item = item with { Phase = "ready" };
                    var objects = pages.Set(catalog.Objects, hash, Encode(item)); pages.Flush(); Save(catalog with { Objects = objects });
                }
            }
        }
        if (item.Phase != "ready") throw Invalid("Resume the owned content reclaim request before publishing another reference.");
        using (var held = Lock(ct))
        using (var pages = new PagedMetadataStore(Metadata, true, filePins: pins))
        {
            var catalog = Load(pages);
            item = DecodeObject(pages.Open(catalog.Objects).Find(hash) ?? throw Invalid("The canonical object ownership is missing."), hash, length);
            if (item.Phase != "ready") throw Invalid("The canonical object is not in its preserved phase.");
            using var reader = OpenCanonical(canonical, item, ct);
            ProtectReference(w, control, pages, catalog, item);
        }
    }
    // A sealed private range can reuse an already owned whole object without
    // creating another payload backing. Missing/in-progress objects return
    // false; a corrupt owned object is an error, never a raw-store fallback.
    public bool TryReferenceExisting(WorkspaceRecord w, string control, string hash, long length, CancellationToken ct = default)
        => TryReferenceExistingBatch(w, control, [new(hash, length)], ct)[0];

    public sealed record ExistingReference(string Hash, long Length);
    public const int MaximumReferenceBatch = 64;
    public bool[] TryReferenceExistingBatch(WorkspaceRecord w, string control, IReadOnlyList<ExistingReference> requests, CancellationToken ct = default)
        => TryReferenceExistingBatch(w, control, requests, null, ct);

    // Only a fixed capture under its existing workspace operation may retain
    // these proofs. Both canonical and private alias names stay natively fenced;
    // no proof survives the caller's using scope or crosses workspace owners.
    public ExistingReferenceScope BeginExistingReferenceScope(WorkspaceRecord workspace, string control)
        => new(this, workspace, control);

    public sealed class ExistingReferenceScope : IDisposable
    {
        private readonly OwnedContentStore owner;
        private readonly WorkspaceRecord workspace;
        private readonly string control;
        private readonly object gate = new();
        private readonly LinkedList<(string Hash, long Length, FileStream Canonical, FileStream Alias)> retained = new();
        private bool disposed;
        internal ExistingReferenceScope(OwnedContentStore owner, WorkspaceRecord workspace, string control)
        { this.owner = owner; this.workspace = workspace; this.control = control; }
        public bool[] TryReferenceBatch(IReadOnlyList<ExistingReference> requests, CancellationToken ct = default)
        { lock (gate) { ObjectDisposedException.ThrowIf(disposed, this); return owner.TryReferenceExistingBatch(workspace, control, requests, this, ct); } }
        internal bool Contains(string hash, long length)
        {
            var node = retained.First;
            while (node is not null && node.Value.Hash != hash) node = node.Next;
            if (node is null) return false;
            if (node.Value.Length != length) throw Invalid("One immutable object cannot have different lengths.");
            retained.Remove(node); retained.AddLast(node); return true;
        }
        internal void Add(string hash, long length, FileStream canonical, FileStream alias)
        {
            if (retained.Count == MaximumReferenceBatch)
            { var prior = retained.First!.Value; prior.Canonical.Dispose(); prior.Alias.Dispose(); retained.RemoveFirst(); }
            retained.AddLast((hash, length, canonical, alias));
        }
        public void Dispose()
        {
            lock (gate)
            {
                if (disposed) return; disposed = true;
                foreach (var entry in retained) { entry.Canonical.Dispose(); entry.Alias.Dispose(); }
                retained.Clear();
            }
        }
    }

    private bool[] TryReferenceExistingBatch(WorkspaceRecord w, string control, IReadOnlyList<ExistingReference> requests, ExistingReferenceScope? scope, CancellationToken ct)
    {
        if (requests.Count is < 1 or > MaximumReferenceBatch) throw new ArgumentOutOfRangeException(nameof(requests));
        var items = requests.ToArray(); var lengths = new Dictionary<string, long>(StringComparer.Ordinal);
        for (int index = 0; index < items.Length; index++)
        {
            var request = items[index];
            Require(w, control, request.Hash, request.Length, inspectPath: index == 0);
            if (lengths.TryGetValue(request.Hash, out long length) && length != request.Length) throw Invalid("One immutable object cannot have different lengths.");
            lengths[request.Hash] = request.Length;
        }
        ct.ThrowIfCancellationRequested(); var results = new bool[items.Length];
        for (int index = 0; index < items.Length; index++) results[index] = scope?.Contains(items[index].Hash, items[index].Length) == true;
        if (results.All(value => value)) return results;
        if (!File.Exists(Path.Combine(directory, "catalog.json"))) return results;
        using var held = Lock(ct); using var pages = new PagedMetadataStore(Metadata, true, filePins: pins); var catalog = Load(pages);
        var proofs = new List<FileStream>(); var admitted = new Dictionary<string, bool>(StringComparer.Ordinal);
        try
        {
            for (int index = 0; index < items.Length; index++)
            {
                ct.ThrowIfCancellationRequested(); string hash = items[index].Hash; long length = items[index].Length;
                if (results[index]) continue;
                if (admitted.TryGetValue(hash, out bool prior)) { results[index] = prior; continue; }
                admitted.Add(hash, false);
                var found = pages.Open(catalog.Objects).Find(hash); if (found is null) continue;
                var item = DecodeObject(found, hash, length);
                if (item.Phase is not ("ready" or "reclaiming")) continue;
                string canonical = Payload(hash);
                if (item.Phase == "reclaiming" && !Path.Exists(canonical)) continue;
                var proof = OpenCanonical(canonical, item, ct); proofs.Add(proof);
                boundary?.Invoke("content-existing-proof-admitted");
                if ((File.GetAttributes(canonical) & FileAttributes.ReadOnly) == 0) throw Invalid("The immutable object's protection metadata changed.");
                if (item.Phase == "reclaiming")
                {
                    item = item with { Phase = "ready" }; catalog = catalog with { Objects = pages.Set(catalog.Objects, hash, Encode(item)) };
                    pages.Flush(); catalog = Save(catalog);
                }
                catalog = ProtectReference(w, control, pages, catalog, item);
                // Keep both exact native names fenced until all duplicate rows
                // have consumed this proof; the catalog lock cannot fence aliases.
                var alias = new FileStream(Alias(control, hash), FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.RandomAccess);
                proofs.Add(alias); pins.Verify(alias, item.Identity!);
                if (scope is not null) { scope.Add(hash, length, proof, alias); proofs.Remove(proof); proofs.Remove(alias); }
                admitted[hash] = results[index] = true;
            }
            return results;
        }
        finally { foreach (var proof in proofs) proof.Dispose(); }
    }
    private Catalog ProtectReference(WorkspaceRecord w, string control, PagedMetadataStore pages, Catalog catalog, ContentObject item)
    {
        string key = ReferenceKey(item.Hash, w.Id);
        var prior = pages.Open(catalog.References).Find(key);
        var reference = prior is null ? new ContentReference(1, w.Id, w.RequestDigest!, item.Identity!, "pinning") : DecodeReference(prior, w, item.Identity!);
        if (reference.Phase == "unlinking") reference = reference with { Phase = "pinning" };
        if (prior is null || reference.Phase == "pinning")
        {
            catalog = catalog with { References = pages.Set(catalog.References, key, Encode(reference)) };
            pages.Flush(); catalog = Save(catalog); boundary?.Invoke("content-reference-protected");
        }
        string alias = Alias(control, item.Hash); Directory.CreateDirectory(Path.GetDirectoryName(alias)!); pins.Pin(Payload(item.Hash), alias, item.Identity!);
        boundary?.Invoke("content-alias-published");
        if (reference.Phase != "ready")
        {
            reference = reference with { Phase = "ready" }; catalog = catalog with { References = pages.Set(catalog.References, key, Encode(reference)) };
            pages.Flush(); catalog = Save(catalog);
        }
        return catalog;
    }
    public long LengthOf(WorkspaceRecord w, string control, string hash, CancellationToken ct = default)
    {
        Require(w, control, hash, 0); using var held = Lock(ct); using var pages = new PagedMetadataStore(Metadata); var catalog = Load(pages);
        var item = DecodeObject(pages.Open(catalog.Objects).Find(hash) ?? throw Invalid("The immutable content owner is missing."), hash, null);
        var reference = DecodeReference(pages.Open(catalog.References).Find(ReferenceKey(hash, w.Id)) ?? throw Invalid("The immutable content reference is missing."), w, item.Identity!);
        if (item.Phase != "ready" || reference.Phase != "ready") throw Invalid("The immutable object or reference is incomplete."); return item.Length;
    }
    public Stream Open(WorkspaceRecord w, string control, string hash, long length, CancellationToken ct = default)
        => OpenCore(w, control, hash, length, ct);
    internal Stream OpenVerified(WorkspaceRecord w, string control, string hash, long length, ImmutableReadProofs proofs, CancellationToken ct)
        => OpenCore(w, control, hash, length, ct, proofs);
    public Stream Open(WorkspaceRecord w, string control, string hash, CancellationToken ct = default)
        => OpenCore(w, control, hash, null, ct);
    private Stream OpenCore(WorkspaceRecord w, string control, string hash, long? length, CancellationToken ct, ImmutableReadProofs? proofs = null)
    {
        Require(w, control, hash, length ?? 0, inspectPath: false); ImmutableReadProofs.EnsurePlainPath(control, proofs); ContentObject item; FileStream input;
        using (var held = Lock(ct, proofs))
        using (var reader = ImmutableReadProofs.OpenMetadata(Metadata, proofs))
        {
            var pages = reader.Pages;
            var catalog = Load(pages, proofs); item = DecodeObject(pages.Open(catalog.Objects).Find(hash) ?? throw Invalid("The immutable content owner is missing."), hash, length);
            var reference = DecodeReference(pages.Open(catalog.References).Find(ReferenceKey(hash, w.Id)) ?? throw Invalid("The immutable content reference is missing."), w, item.Identity!);
            if (item.Phase != "ready" || reference.Phase != "ready" || pins.Identify(Alias(control, hash)) != item.Identity)
                throw Invalid("The independent content alias is missing, changed or incomplete.");
            input = OpenCanonicalFence(Payload(hash), item, ct, proofs);
        }
        // The exact native handle denies writes/deletion before releasing the
        // shared catalog lock. Other payload readers can now admit concurrently.
        try
        {
            boundary?.Invoke("content-reader-pinned");
            if (proofs is null) VerifyCanonicalBytes(input, item, ct);
            else proofs.Verify(input, item.Identity!, 0, item.Length, item.Hash, ct);
            return input;
        }
        catch { input.Dispose(); throw; }
    }
    // Call only when the owner has durably removed every result/request root
    // requiring this alias. Workspace release alone never calls this method.
    public void ReleaseReference(WorkspaceRecord w, string control, string hash, long length, Func<string, bool> isReferenced, CancellationToken ct = default)
    {
        Require(w, control, hash, length);
        if (isReferenced(hash)) throw new WorkspaceException("ContentStillReferenced", "A workspace, retained result or pending request still requires this immutable content alias."); using var held = Lock(ct); using var pages = new PagedMetadataStore(Metadata, true, filePins: pins);
        var catalog = Load(pages); string key = ReferenceKey(hash, w.Id);
        var raw = pages.Open(catalog.References).Find(key);
        if (raw is null)
        { if (Path.Exists(Alias(control, hash))) throw Invalid("Preserve a content alias without its ownership reference."); return; }
        var item = DecodeObject(pages.Open(catalog.Objects).Find(hash) ?? throw Invalid("The content owner disappeared."), hash, length);
        var reference = DecodeReference(raw, w, item.Identity!);
        if (reference.Phase != "unlinking")
        {
            reference = reference with { Phase = "unlinking" };
            catalog = catalog with { References = pages.Set(catalog.References, key, Encode(reference)) }; pages.Flush(); catalog = Save(catalog);
        }
        string alias = Alias(control, hash);
        if (Path.Exists(alias)) { pins.Unpin(alias, item.Identity!); boundary?.Invoke("content-alias-removed"); }
        catalog = catalog with { References = pages.Remove(catalog.References, key) }; pages.Flush(); catalog = Save(catalog);
    }
    public void PruneUnreferenced(WorkspaceRecord w, string control, Func<string, bool> isReferenced, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(isReferenced);
        using var candidates = new MetadataSpool(directory);
        using (var held = Lock(ct))
        using (var pages = new PagedMetadataStore(Metadata, true, filePins: pins))
        {
            var catalog = Load(pages);
            foreach (var entry in pages.Open(catalog.Objects).Enumerate())
            {
                ct.ThrowIfCancellationRequested(); var item = DecodeObject(entry.Value.ToArray(), entry.Key, null);
                bool resume = item.Phase == "reclaiming";
                if (!resume && pages.Open(catalog.References).Find(ReferenceKey(item.Hash, w.Id)) is null) continue;
                byte[] data = new byte[9]; data[0] = resume ? (byte)1 : (byte)0;
                System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(data.AsSpan(1), item.Length);
                candidates.Append(new(item.Hash, data));
            }
        }
        // No catalog writer or metadata-container fence spans caller root
        // lookups, reader draining, or another short reclamation transaction.
        foreach (var entry in candidates.Read())
        {
            ct.ThrowIfCancellationRequested(); long length = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(entry.Value.Span[1..]);
            if (isReferenced(entry.Key))
            {
                if (entry.Value.Span[0] == 1) throw new WorkspaceException("ContentStillReferenced", "A retained root needs content with a pending reclaim intent; preserve it for reconciliation.");
                continue;
            }
            try
            {
                if (entry.Value.Span[0] == 0) ReleaseReference(w, control, entry.Key, length, isReferenced, ct);
                _ = Reclaim(entry.Key, ct);
            }
            catch (WorkspaceException error) when (error.Code == "ImmutableContentReaderBusy") { }
        }
    }
    public bool Reclaim(string hash, CancellationToken ct = default)
    {
        if (hash.Length != 64 || hash.Any(c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f'))) throw Invalid("Invalid canonical content hash.");
        using var held = Lock(ct); using var pages = new PagedMetadataStore(Metadata, true, filePins: pins); var catalog = Load(pages);
        var raw = pages.Open(catalog.Objects).Find(hash); if (raw is null) return true;
        var item = DecodeObject(raw, hash, null);
        string prefix = hash + "/";
        if (pages.Open(catalog.References).Enumerate(prefix).TakeWhile(e => e.Key.StartsWith(prefix, StringComparison.Ordinal)).Any()) return false;
        string canonical = Payload(hash);
        if (item.Phase is not ("ready" or "reclaiming")) throw Invalid("Recover the exact incomplete content object before reclaiming.");
        if (item.Phase == "ready")
        {
            Verify(canonical, item, ct);
            if (pins.LinkCount(canonical) != 1) return false;
            item = item with { Phase = "reclaiming" };
            catalog = catalog with { Objects = pages.Set(catalog.Objects, hash, Encode(item)) }; pages.Flush(); catalog = Save(catalog);
        }
        if (Path.Exists(canonical))
        {
            if (pins.LinkCount(canonical) != 1) throw Invalid("The unreferenced owned object gained another link.");
            pins.Unpin(canonical, item.Identity!); boundary?.Invoke("content-object-reclaimed");
        }
        catalog = catalog with { Objects = pages.Remove(catalog.Objects, hash) }; pages.Flush(); catalog = Save(catalog); return true;
    }
    public sealed record PreservedContentScratch(string RelativePath, string ObservedByWorkspaceId, string Reason);
    public sealed record PreservedContentScratchPage(MetadataTreeRoot? Root, PreservedContentScratch[] Entries, MetadataCursor? NextCursor);
    public PreservedContentScratchPage ReadPreservedScratchPage(MetadataCursor? cursor = null, int pageSize = 128, CancellationToken ct = default)
    {
        if (!File.Exists(Path.Combine(directory, "catalog.json")))
        { if (cursor is not null) throw Invalid("The requested scratch cursor has no existing content store."); return new(null, [], null); }
        using var held = Lock(ct); using var pages = new PagedMetadataStore(Metadata); var catalog = Load(pages);
        var page = pages.Open(catalog.PreservedScratch).ReadPage(cursor, pageSize);
        var entries = page.Entries.Select(entry =>
        {
            string value = new UTF8Encoding(false, true).GetString(entry.Value.Span); int split = value.IndexOf('\n');
            if (split != 24 || !entry.Key.StartsWith("objects/", StringComparison.Ordinal) || entry.Key.Contains("..", StringComparison.Ordinal) ||
                !entry.Key.EndsWith(".pending", StringComparison.Ordinal) || value[..split].Any(c => !char.IsAsciiHexDigit(c)))
                throw Invalid("A preserved content scratch observation is malformed.");
            return new PreservedContentScratch(entry.Key, value[..split], value[(split + 1)..]);
        }).ToArray();
        return new(page.Root, entries, page.NextCursor);
    }
    public sealed record DeferredContentRetirement(string Hash, string Kind, string? WorkspaceId, string NativeIdentity, long LogicalBytes);
    public sealed record DeferredContentRetirementPage(MetadataTreeRoot? Root, DeferredContentRetirement[] Entries, MetadataCursor? NextCursor);
    public DeferredContentRetirementPage ReadDeferredReclamationPage(bool references = false, MetadataCursor? cursor = null, int pageSize = 128, CancellationToken ct = default)
    {
        if (!File.Exists(Path.Combine(directory, "catalog.json")))
        { if (cursor is not null) throw Invalid("The deferred cursor has no existing content store."); return new(null, [], null); }
        using var held = Lock(ct); using var pages = new PagedMetadataStore(Metadata); var catalog = Load(pages);
        var page = pages.Open(references ? catalog.References : catalog.Objects).ReadPage(cursor, pageSize); var entries = new List<DeferredContentRetirement>();
        foreach (var entry in page.Entries)
        {
            if (!references)
            {
                var item = DecodeObject(entry.Value.ToArray(), entry.Key, null);
                if (item.Phase == "reclaiming") entries.Add(new(item.Hash, "object", null, item.Identity!, item.Length));
            }
            else
            {
                if (entry.Key.Length != 89 || entry.Key[64] != '/' || entry.Key[..64].Any(c => !char.IsAsciiHexDigit(c)) || entry.Key[65..].Any(c => !char.IsAsciiHexDigit(c)))
                    throw Invalid("A deferred alias key is invalid.");
                var item = DecodeObject(pages.Open(catalog.Objects).Find(entry.Key[..64]) ?? throw Invalid("A deferred alias has no object owner."), entry.Key[..64], null);
                using var memory = new MemoryStream(entry.Value.ToArray(), false); using var input = new BinaryReader(memory, Encoding.UTF8);
                if (input.ReadByte() != 1) throw Invalid("A deferred alias version is invalid.");
                byte phase = input.ReadByte(); _ = ReadString(input); string identity = ReadString(input);
                if (memory.Position != memory.Length || identity != item.Identity || phase > 2) throw Invalid("A deferred alias identity is invalid.");
                if (phase == 2) entries.Add(new(item.Hash, "alias", entry.Key[65..], identity, item.Length));
            }
        }
        return new(page.Root, entries.ToArray(), page.NextCursor);
    }
    public (string Directory, MetadataTreeRoot[] Roots)? ReadMetadataRoots(CancellationToken ct = default)
    {
        if (!File.Exists(Path.Combine(directory, "catalog.json"))) return null;
        using var held = Lock(ct); using var pages = new PagedMetadataStore(Metadata); var catalog = Load(pages);
        return (Metadata, [catalog.Objects, catalog.References, catalog.PreservedScratch]);
    }
    private FileStream OpenCanonical(string path, ContentObject item, CancellationToken ct)
    {
        var input = OpenCanonicalFence(path, item, ct);
        try { VerifyCanonicalBytes(input, item, ct); return input; }
        catch { input.Dispose(); throw; }
    }
    private FileStream OpenCanonicalFence(string path, ContentObject item, CancellationToken ct, ImmutableReadProofs? proofs = null)
    {
        ImmutableReadProofs.EnsurePlainPath(path, proofs); var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536);
        try
        {
            pins.Verify(input, item.Identity!); ct.ThrowIfCancellationRequested();
            if (item.Phase == "ready" && (File.GetAttributes(path) & FileAttributes.ReadOnly) == 0)
                throw Invalid("The canonical immutable protection metadata changed.");
            if (input.Length != item.Length) throw Invalid("The immutable content length changed.");
            return input;
        }
        catch { input.Dispose(); throw; }
    }
    private static void VerifyCanonicalBytes(FileStream input, ContentObject item, CancellationToken ct)
    {
        if (PayloadHash.Sha256(input, ct) != item.Hash) throw Invalid("The immutable content bytes changed.");
        input.Position = 0;
    }
    private void Verify(string path, ContentObject item, CancellationToken ct)
    { using var input = OpenCanonical(path, item, ct); }
    private Catalog Load(PagedMetadataStore pages, ImmutableReadProofs? proofs = null)
    {
        string path = Path.Combine(directory, "catalog.json"); ImmutableReadProofs.EnsurePlainPath(path, proofs);
        Catalog? current = File.Exists(path) ? ReadCatalog(path) : null;
        if (current is null)
        {
            // No native payload is created until this empty owner root commits.
            // Unpublished GUID temporaries are observations, never root authority.
            if (Directory.EnumerateFiles(Metadata, "pages-*.bin").Any(p => new FileInfo(p).Length != 48))
                throw Invalid("Preserve orphaned content metadata without its owner catalog.");
            current = new(3, pages.StoreId, 0, pages.Empty, pages.Empty, pages.Empty); SaveInitial(current);
        }
        if (current.Version != 3 || current.StoreId != pages.StoreId || current.Sequence < 0) throw Invalid("The content catalog owner/version differs from its page store.");
        _ = pages.Open(current.Objects); _ = pages.Open(current.References); _ = pages.Open(current.PreservedScratch); return current;
    }
    private static Catalog ReadCatalog(string path)
    {
        try
        {
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (input.Length > 16384) throw Invalid("A content control root exceeds its processing bound.");
            using var document = JsonDocument.Parse(input);
            if (!JsonPropertyValidation.HasUniqueProperties(document.RootElement)) throw Invalid("The content catalog repeats a control field.");
            return document.RootElement.Deserialize<Catalog>(Json) ?? throw Invalid("The content catalog is empty.");
        }
        catch (JsonException) { throw Invalid("Preserve a malformed content catalog."); }
    }
    private Catalog Save(Catalog catalog) { var next = catalog with { Sequence = checked(catalog.Sequence + 1) }; SaveInitial(next); return next; }
    private void SaveInitial(Catalog catalog)
    {
        string path = Path.Combine(directory, "catalog.json"), pending = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        WorkspaceStore.EnsurePlainPath(path); WorkspaceStore.EnsurePlainPath(pending);
        // Like workspace control publication, each attempt has a fresh scratch
        // name. A partial temporary never becomes the next root or blocks a
        // same-request retry, and it is never overwritten/adopted/deleted.
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(catalog, Json);
        using (var output = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            int split = Math.Max(1, bytes.Length / 2); output.Write(bytes.AsSpan(0, split)); boundary?.Invoke("content-catalog-prefix-written");
            output.Write(bytes.AsSpan(split)); output.Flush(true); boundary?.Invoke("content-catalog-durable-before-publish");
        }
        AtomicFilePublication.Publish(pending, path); boundary?.Invoke("content-catalog-published");
    }
    private OperationLease Lock(CancellationToken ct, ImmutableReadProofs? proofs = null)
    {
        ImmutableReadProofs.EnsurePlainPath(directory, proofs); Directory.CreateDirectory(directory); string path = Path.Combine(directory, "operation.lock"); ImmutableReadProofs.EnsurePlainPath(path, proofs);
        LocalGate local;
        lock (GateRegistryLock)
        {
            if (!GateRegistry.TryGetValue(directory, out local!)) GateRegistry.Add(directory, local = new());
            local.Users++;
        }
        bool entered = false;
        try
        {
            local.Semaphore.Wait(ct); entered = true;
            for (int attempt = 0; ; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                try { return new(this, local, new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)); }
                catch (IOException ex) when ((uint)ex.HResult is 0x80070020 or 0x80070021)
                {
                    if (attempt >= 500) throw new WorkspaceException("ContentStoreBusy", "Retry the same frozen request after the active content writer completes.");
                    Thread.Sleep(10);
                }
            }
        }
        catch { ReleaseLocal(local, entered); throw; }
    }
    private void ReleaseLocal(LocalGate local, bool entered)
    {
        if (entered) local.Semaphore.Release();
        lock (GateRegistryLock)
        {
            local.Users--;
            if (local.Users == 0) { GateRegistry.Remove(directory); local.Semaphore.Dispose(); }
        }
    }
    private sealed class OperationLease(OwnedContentStore owner, LocalGate local, FileStream stream) : IDisposable
    {
        private bool disposed;
        public void Dispose() { if (disposed) return; disposed = true; stream.Dispose(); owner.ReleaseLocal(local, true); }
    }
    private static string ReferenceKey(string hash, string id) => hash + "/" + id;
    private static byte[] Encode(ContentObject value)
    {
        using var memory = new MemoryStream(); using var output = new BinaryWriter(memory, Encoding.UTF8, true);
        output.Write((byte)value.Version); output.Write(value.Phase switch { "allocating" => (byte)0, "writing" => (byte)1, "ready" => (byte)2, "reclaiming" => (byte)3, _ => throw Invalid("Invalid content phase.") });
        output.Write(value.Length); output.Write(value.Identity is not null); if (value.Identity is not null) WriteString(output, value.Identity);
        WriteString(output, value.Nonce); return memory.ToArray();
    }
    private static byte[] Encode(ContentReference value)
    {
        using var memory = new MemoryStream(); using var output = new BinaryWriter(memory, Encoding.UTF8, true);
        output.Write((byte)value.Version); output.Write(value.Phase switch { "pinning" => (byte)0, "ready" => (byte)1, "unlinking" => (byte)2, _ => throw Invalid("Invalid content reference phase.") });
        WriteString(output, value.RequestDigest); WriteString(output, value.Identity); return memory.ToArray();
    }
    private static ContentObject DecodeObject(byte[] raw, string hash, long? length)
    {
        try
        {
            using var memory = new MemoryStream(raw, false); using var reader = new BinaryReader(memory, Encoding.UTF8);
            int version = reader.ReadByte(); string phase = reader.ReadByte() switch { 0 => "allocating", 1 => "writing", 2 => "ready", 3 => "reclaiming", _ => throw Invalid("Invalid content phase.") };
            long size = reader.ReadInt64(); string? identity = reader.ReadByte() switch { 0 => null, 1 => ReadString(reader), _ => throw Invalid("Invalid content identity presence.") };
            string nonce = ReadString(reader); var item = new ContentObject(version, hash, size, identity, phase, nonce);
            if (memory.Position != memory.Length) throw Invalid("Trailing content owner bytes.");
            ValidateObject(item, hash, length ?? size); return item;
        }
        catch (Exception ex) when (ex is EndOfStreamException or DecoderFallbackException or ArgumentException)
        { throw Invalid("Malformed content object ownership."); }
    }
    private static void ValidateObject(ContentObject item, string hash, long length)
    {
        if (item.Version != 2 || item.Hash != hash || item.Length != length || item.Length < 0 ||
            item.Nonce.Length != 32 || item.Nonce.Any(c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f')) ||
            (item.Phase == "allocating" ? item.Identity is not null : string.IsNullOrWhiteSpace(item.Identity)) ||
            item.Phase is not ("allocating" or "writing" or "ready" or "reclaiming")) throw Invalid("The content object differs from its immutable identity.");
    }
    private static ContentReference DecodeReference(byte[] raw, WorkspaceRecord w, string identity)
    {
        try
        {
            using var memory = new MemoryStream(raw, false); using var reader = new BinaryReader(memory, Encoding.UTF8);
            int version = reader.ReadByte(); string phase = reader.ReadByte() switch { 0 => "pinning", 1 => "ready", 2 => "unlinking", _ => throw Invalid("Invalid content reference phase.") };
            string digest = ReadString(reader), native = ReadString(reader);
            if (version != 1 || digest != w.RequestDigest || native != identity || memory.Position != memory.Length)
                throw Invalid("The content alias belongs to another request or native object.");
            return new(version, w.Id, digest, native, phase);
        }
        catch (Exception ex) when (ex is EndOfStreamException or DecoderFallbackException or ArgumentException)
        { throw Invalid("Malformed content reference ownership."); }
    }
    private static void WriteString(BinaryWriter writer, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value); if (bytes.Length is < 1 or > 65535) throw Invalid("Invalid content owner field length.");
        writer.Write((ushort)bytes.Length); writer.Write(bytes);
    }
    private static string ReadString(BinaryReader reader)
    {
        int count = reader.ReadUInt16(); if (count < 1 || count > reader.BaseStream.Length - reader.BaseStream.Position) throw Invalid("Truncated content owner field.");
        return new UTF8Encoding(false, true).GetString(reader.ReadBytes(count));
    }
    private static WorkspaceException Invalid(string message) => new("ContentStoreInvalid", message);
}
