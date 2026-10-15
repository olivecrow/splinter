using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32.SafeHandles;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.BeeProbe;

internal sealed record InternalNativeFileObservation(Guid Id, string LogicalPath, bool Deleted,
    string BackingPath, long Length, string Sha256, uint VolumeSerial, ulong FileIndex, bool Promoted);

// Development-only, single-namespace experiment. The journal owns every name and
// identity. Only application-created generated payload uses buffered NTFS files;
// inherited data is never hydrated or copied up. Source publication first imports
// the application's new bytes into the durable journal under the SAME identity.
internal sealed class InternalNativeDeltaStore : IWorkspaceDeltaStore
{
    private const string Subtree = "Library/Bee";
    private const string Policy = "new-generated-buffered-ntfs-v1";
    private const int PublicationLimit = 4096;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    private sealed record Envelope<T>(int Version, T Payload, string Sha256);
    private sealed record Owner(string IdentityHash, string Root, string Subtree, string Policy);
    private sealed record Association(Guid Id, string OwnerHash, uint VolumeSerial, ulong FileIndex);
    private sealed record Promotion(Guid Id, string OwnerHash, long Length, string Sha256);
    private sealed record Intent(string Operation, string OwnerHash, Guid? Id, string? Path,
        uint Attributes = 0, string? Destination = null, bool Replace = false,
        long Length = 0, string? Sha256 = null, Association? Association = null);
    private sealed record Publication(string SourcePath, string DestinationPath, Guid SourceId,
        Guid PublishedId, bool AtomicNamespacePublication);
    private sealed class Backing(Association association, SafeFileHandle handle, string path)
    {
        internal readonly Association Association = association;
        internal readonly SafeFileHandle Handle = handle;
        internal readonly string Path = path;
        internal bool Promoted;
        internal long Length;
    }
    private readonly object gate = new();
    private readonly string root, ownerHash;
    private readonly NativePin rootPin;
    private readonly FileStream writer;
    private readonly Action<string>? interruptionObserver;
    private readonly Dictionary<Guid, Backing> files = [];
    private readonly List<Publication> publications = [];
    private bool disposed, poisoned;
    private long nativeReadBytes, nativeWriteBytes, nativeWriteCalls, nativeFlushes, nativeFlushTicks;
    private long promotions, publicationOverflow;
    private long nativePayloadBytes;
    public JournalDeltaStore Journal { get; }
    public OverlayIdentity Identity => Journal.Identity;
    public object NativeRootIdentity => rootPin.Evidence;
    public bool SupportsSourcePromotion => true;
    public long NativePayloadBytes { get { lock (gate) { Check(); return nativePayloadBytes; } } }
    public int NativeFileCount { get { lock (gate) { Check(); return files.Count; } } }
    public object Metrics { get { lock (gate) return new { nativeReadBytes, nativeWriteBytes, nativeWriteCalls,
        nativeFlushes, nativeFlushSeconds = (double)nativeFlushTicks / Stopwatch.Frequency, promotions,
        publicationOverflow, publications = publications.ToArray(), buffering = "generated payload only; Flush reaches every native file and journal" }; } }

    public InternalNativeDeltaStore(JournalDeltaStore journal, string backingRoot, Action<string>? interruptionObserver = null)
    {
        Journal = journal; this.interruptionObserver = interruptionObserver;
        root = Path.GetFullPath(backingRoot); ownerHash = IdentityHash(Identity);
        Directory.CreateDirectory(root); rootPin = new NativePin(root);
        try
        {
            string writerPath = Path.Combine(root, "writer.lock");
            if (File.Exists(writerPath)) CheckRegular(writerPath);
            writer = new FileStream(writerPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            if (writer.Length != 0) throw new IOException("Unknown native backing writer marker.");
            var expected = new Owner(ownerHash, root, Subtree, Policy);
            string ownerPath = Path.Combine(root, "owner.json");
            RecoverPending<Owner>(ownerPath, saved => saved == expected);
            if (!File.Exists(ownerPath))
            {
                if (Directory.EnumerateFileSystemEntries(root).Any(p => p != writerPath))
                    throw new IOException("Unowned native backing is preserved.");
                WriteDocument(ownerPath, expected);
            }
            if (ReadDocument<Owner>(ownerPath) != expected) throw new IOException("Native backing owner/policy mismatch.");
            RecoverPending<Intent>(IntentPath, ValidIntent, ValidIntentTransition);
            Intent? intent = File.Exists(IntentPath) ? ReadIntent() : null;
            // Only a matching in-flight creation may complete a prepared record.
            foreach (string pending in Directory.EnumerateFiles(root, "*.record.json.pending"))
            {
                if (intent is not { Operation: "create", Id: { } pendingId } || pending != RecordPath(pendingId) + ".pending")
                    throw new IOException("Unknown prepared native association is preserved.");
                RecoverPending<Association>(RecordPath(pendingId), saved => ValidPreparedAssociation(saved, pendingId));
            }
            foreach (string path in Directory.EnumerateFiles(root, "*.record.json"))
            {
                var record = ReadDocument<Association>(path);
                if (path != RecordPath(record.Id) || record.OwnerHash != ownerHash) throw new IOException("Native association identity mismatch.");
                if (intent is { Operation: "reclaim", Id: { } reclaimId } && record.Id == reclaimId && !File.Exists(PayloadPath(record.Id))) continue;
                OpenBacking(record, intent);
            }
            if (intent is not null) RecoverIntent(intent);
            VerifyNativeInventory();
        }
        catch
        {
            foreach (var backing in files.Values) backing.Handle.Dispose();
            writer?.Dispose(); rootPin.Dispose(); throw;
        }
    }

    public DeltaEntry? Stat(string path) { lock (gate) { Check(); return Journal.Stat(path) is { } e ? Decorate(e) : null; } }
    public DeltaEntry Get(Guid id) { lock (gate) { Check(); return Decorate(Journal.Get(id)); } }
    public IReadOnlyList<DeltaEntry> List(string directory) { lock (gate) { Check(); return Journal.List(directory).Select(Decorate).ToArray(); } }
    public DeltaEntry Create(string path, bool directory, uint attributes)
    {
        lock (gate)
        {
            Check(); path = JournalDeltaStore.Normalize(path);
            if (directory || !Generated(path)) return Journal.Create(path, directory, attributes);
            if (Journal.Stat(path) is not null) throw new IOException("Path already exists.");
            // This preflight ensures a failed ordinary mkdir/open cannot leave an
            // intent for a request that was never eligible for publication.
            string parent = path[..path.LastIndexOf('/')];
            if (Journal.Stat(parent) is not { Directory: true }) throw new DirectoryNotFoundException(parent);
            var intent = new Intent("create", ownerHash, null, path, attributes);
            try { WriteIntent(intent); Guid id = CompleteCreate(intent); return Decorate(Journal.Get(id)); }
            catch { poisoned = true; throw; }
        }
    }
    public int Read(Guid id, long offset, Span<byte> destination)
    {
        lock (gate)
        {
            Check(); if (!Native(id, out var backing)) return Journal.Read(id, offset, destination);
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
            Info(backing); int count = RandomAccess.Read(backing.Handle, destination, offset); nativeReadBytes += count; return count;
        }
    }
    public void Write(Guid id, long offset, ReadOnlySpan<byte> data)
    {
        lock (gate)
        {
            Check(); if (!Native(id, out var backing)) { Journal.Write(id, offset, data); return; }
            if (offset < 0 || data.Length > Identity.MaxTransferBytes || offset > long.MaxValue - data.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (data.Length == 0) return;
            long newLength = Math.Max(backing.Length, offset + data.Length); RequireNativeCapacity(backing, newLength);
            var info = Info(backing);
            try { RandomAccess.Write(backing.Handle, data, offset); UpdateLength(backing, newLength); SetTimes(backing.Handle, info.Creation, info.Access, DateTime.UtcNow.ToFileTimeUtc()); nativeWriteBytes += data.Length; nativeWriteCalls++; }
            catch { poisoned = true; throw; }
        }
    }
    public void SetLength(Guid id, long length)
    {
        lock (gate)
        {
            Check(); if (!Native(id, out var backing)) { Journal.SetLength(id, length); return; }
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length)); var info = Info(backing);
            if (length == backing.Length) return; RequireNativeCapacity(backing, length);
            try { RandomAccess.SetLength(backing.Handle, length); UpdateLength(backing, length); SetTimes(backing.Handle, info.Creation, info.Access, DateTime.UtcNow.ToFileTimeUtc()); }
            catch { poisoned = true; throw; }
        }
    }
    public void SetMetadata(Guid id, uint attributes, long creationTime, long accessTime, long writeTime)
    {
        lock (gate)
        {
            Check(); Journal.SetMetadata(id, attributes, creationTime, accessTime, writeTime);
            if (!Native(id, out var backing)) return;
            try { Info(backing); SetTimes(backing.Handle, creationTime, accessTime, writeTime); }
            catch { poisoned = true; throw; }
        }
    }
    public void SetUnixMode(Guid id, uint? mode) { lock (gate) { Check(); Journal.SetUnixMode(id, mode); } }
    public void Rename(Guid id, string destination, bool replace)
    {
        lock (gate)
        {
            Check(); var before = Journal.Get(id); destination = JournalDeltaStore.Normalize(destination);
            if (before.Directory && !Generated(destination) && files.Values.Any(f => !f.Promoted && IsChild(Journal.Get(f.Association.Id).Path, before.Path)))
                throw new WorkspaceException("SnapshotFileUnsupported", "Development native backing cannot promote a directory containing generated payload; move its files explicitly.");
            // Validate every ordinary namespace rejection before promotion writes.
            PreflightRename(before, destination, replace);
            if (Native(id, out var backing) && !Generated(destination))
            {
                try
                {
                    FlushBacking(backing); var info = Info(backing);
                    var intent = new Intent("promote", ownerHash, id, before.Path, Destination: destination,
                        Replace: replace, Length: info.Length, Sha256: NativeMethods.Hash(backing.Handle), Association: backing.Association);
                    WriteIntent(intent); interruptionObserver?.Invoke("promotion.intent-durable"); CompletePromotion(intent);
                }
                catch { poisoned = true; throw; }
            }
            else { Journal.Rename(id, destination, replace); interruptionObserver?.Invoke("rename.namespace-published"); }
            var published = Journal.Stat(destination) ?? throw new IOException("Namespace publication is absent.");
            if (published.Id != id) throw new IOException("Namespace publication changed identity.");
            if (publications.Count < PublicationLimit) publications.Add(new(before.Path, destination, id, published.Id, true));
            else publicationOverflow++;
        }
    }
    public void Delete(Guid id) { lock (gate) { Check(); Journal.Delete(id); } }
    public void ReleaseUnlinkedFile(Guid id)
    {
        lock (gate)
        {
            Check(); if (!files.TryGetValue(id, out var backing)) { Journal.ReleaseUnlinkedFile(id); return; }
            if (!Journal.Get(id).Deleted) return;
            try
            {
                var intent = new Intent("reclaim", ownerHash, id, null, Association: backing.Association);
                WriteIntent(intent); CompleteReclaim(intent);
            }
            catch { poisoned = true; throw; }
        }
    }
    public void Flush()
    {
        lock (gate)
        {
            Check(); rootPin.Verify();
            try { foreach (var backing in files.Values) FlushBacking(backing); Journal.Flush(); }
            catch { poisoned = true; throw; }
        }
    }
    // This contract's value is explicitly journal-only. NativeStatistics supplies
    // separate complete payload/control-file accounting; never label it total.
    public OverlayStorageObservation Observe() { lock (gate) { Check(); return Journal.Observe(); } }
    public OverlayStorageObservation? ObserveActivity() { lock (gate) { Check(); return Journal.ObserveActivity(); } }
    public long GetJournalBytes() { lock (gate) { Check(); return Journal.GetJournalBytes(); } }

    public IReadOnlyList<InternalNativeFileObservation> NativeInventory()
    {
        lock (gate)
        {
            Check(); VerifyNativeInventory();
            return files.Values.OrderBy(f => f.Association.Id).Select(f =>
            {
                var entry = Journal.Get(f.Association.Id); var info = Info(f);
                string hash = NativeMethods.Hash(f.Handle);
                if (f.Promoted && ReadDocument<Promotion>(PromotionPath(entry.Id)) != new Promotion(entry.Id, ownerHash, info.Length, hash))
                    throw new IOException("Retained native promotion proof differs.");
                return new InternalNativeFileObservation(entry.Id, entry.Path, entry.Deleted, f.Path, info.Length,
                    hash, info.VolumeSerialNumber, info.Index, f.Promoted);
            }).ToArray();
        }
    }
    public object NativeStatistics()
    {
        lock (gate)
        {
            Check(); VerifyNativeInventory();
            var inventory = Directory.EnumerateFiles(root).Order(StringComparer.Ordinal).Select(path => new
            { path, length = new FileInfo(path).Length, kind = path.EndsWith(".payload", StringComparison.Ordinal) ? "native-payload" : "native-control" }).ToArray();
            return new { nativePayloadBytes = NativePayloadBytes, nativeFileCount = files.Count,
                nativeControlBytes = inventory.Where(f => f.kind == "native-control").Sum(f => f.length),
                nativeTotalFileBytes = inventory.Sum(f => f.length), inventory, metrics = Metrics,
                nativePayloadByteLimit = Identity.MaxJournalBytes,
                allocatedBytes = (long?)null, peakAllocatedBytes = (long?)null,
                accounting = "all owned backing files including control metadata; logical length, not NTFS allocation" };
        }
    }
    public void VerifyNativeInventory()
    {
        lock (gate)
        {
            Check(); rootPin.Verify();
            if (File.Exists(IntentPath) || File.Exists(IntentPath + ".pending")) throw new IOException("Native operation requires same-owner recovery.");
            var expected = ExpectedInventory(root, files.Values.Select(f => (f.Association.Id, f.Promoted)));
            RequireExactInventory(root, expected);
            if (ReadDocument<Owner>(Path.Combine(root, "owner.json")) != new Owner(ownerHash, root, Subtree, Policy))
                throw new IOException("Native root owner changed.");
            foreach (var backing in files.Values)
            {
                var entry = Journal.Get(backing.Association.Id); Info(backing); ValidateHandle(backing.Handle, backing.Path);
                if (entry.Directory || entry.BasePath is not null || !backing.Promoted && (!Generated(entry.Path) || entry.Length != 0))
                    throw new IOException("Native association escaped its generated namespace or journal representation.");
                if (ReadDocument<Association>(RecordPath(entry.Id)) != backing.Association) throw new IOException("Native association changed.");
                if (backing.Promoted)
                {
                    var promotion = ReadDocument<Promotion>(PromotionPath(entry.Id));
                    if (promotion.Id != entry.Id || promotion.OwnerHash != ownerHash) throw new IOException("Native promotion owner changed.");
                }
            }
        }
    }
    public static object VerifyStoppedInventory(string backingRoot, OverlayIdentity identity,
        IReadOnlyList<InternalNativeFileObservation> priorInventory)
    {
        string fullRoot = Path.GetFullPath(backingRoot), hash = IdentityHash(identity);
        var expectedInventory = ExpectedInventory(fullRoot, priorInventory.Select(f => (f.Id, f.Promoted)));
        RequireExactInventory(fullRoot, expectedInventory);
        if (ReadDocument<Owner>(Path.Combine(fullRoot, "owner.json")) != new Owner(hash, fullRoot, Subtree, Policy))
            throw new IOException("Stopped native backing owner differs.");
        var controlHashes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string path in Directory.EnumerateFiles(fullRoot).Where(p => !p.EndsWith(".payload", StringComparison.Ordinal)))
        {
            CheckRegular(path); controlHashes.Add(Path.GetFileName(path), Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))));
        }
        foreach (var row in priorInventory)
        {
            var association = ReadDocument<Association>(Path.Combine(fullRoot, row.Id.ToString("N") + ".record.json"));
            if (association != new Association(row.Id, hash, row.VolumeSerial, row.FileIndex)) throw new IOException("Stopped native association differs.");
            if (row.Promoted)
            {
                var marker = ReadDocument<Promotion>(Path.Combine(fullRoot, row.Id.ToString("N") + ".promoted.json"));
                if (marker != new Promotion(row.Id, hash, row.Length, row.Sha256)) throw new IOException("Stopped promotion ownership/content differs.");
            }
        }
        using var proof = new NativeOutputProof(fullRoot);
        // NativeOutputProof holds exclusive file handles. Its hash/identity rows
        // are read as JSON to avoid reopening those deliberately exclusive files.
        var proofRows = JsonSerializer.SerializeToElement(proof.Files, Json).EnumerateArray().ToDictionary(
            row => row.GetProperty("path").GetString()!, StringComparer.Ordinal);
        if (!expectedInventory.Select(path => Path.GetFileName(path)!).ToHashSet(StringComparer.Ordinal).SetEquals(proofRows.Keys))
            throw new IOException("Stopped native proof captured an unexpected inventory.");
        foreach (var (name, controlHash) in controlHashes)
            if (proofRows[name].GetProperty("sha256").GetString() != controlHash) throw new IOException("Stopped native control changed before exclusive proof.");
        foreach (var row in priorInventory)
        {
            string name = row.Id.ToString("N") + ".payload";
            var observed = proofRows[name];
            if (row.BackingPath != Path.Combine(fullRoot, name) || observed.GetProperty("length").GetInt64() != row.Length ||
                observed.GetProperty("sha256").GetString() != row.Sha256 || observed.GetProperty("volumeSerial").GetUInt32() != row.VolumeSerial ||
                observed.GetProperty("fileIndex").GetUInt64() != row.FileIndex)
                throw new IOException("Stopped native payload differs from its live proof.");
        }
        RequireExactInventory(fullRoot, expectedInventory);
        return new { passed = true, exactInventory = true, files = proof.Files.ToArray(), nativePayloads = priorInventory.Count };
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return; disposed = true;
            try { foreach (var backing in files.Values) backing.Handle.Dispose(); Journal.Dispose(); }
            finally { writer.Dispose(); rootPin.Dispose(); }
        }
    }

    private bool Native(Guid id, out Backing backing)
    { if (files.TryGetValue(id, out backing!) && !backing.Promoted) return true; return false; }
    private DeltaEntry Decorate(DeltaEntry entry)
    {
        if (!Native(entry.Id, out var backing)) return entry;
        var info = Info(backing);
        return entry with { Length = info.Length, CreationTime = info.Creation, LastAccessTime = info.Access, LastWriteTime = info.Write };
    }
    private static bool Generated(string path) => path.Equals(Subtree, StringComparison.OrdinalIgnoreCase) || path.StartsWith(Subtree + "/", StringComparison.OrdinalIgnoreCase);
    private static bool IsChild(string path, string parent) => path.StartsWith(parent + "/", StringComparison.OrdinalIgnoreCase);
    private void Check() { ObjectDisposedException.ThrowIf(disposed, this); if (poisoned) throw new IOException("Native backing failed; preserve it and reconnect the same owner."); }
    private string IntentPath => Path.Combine(root, "operation.json");
    private string PayloadPath(Guid id) => Path.Combine(root, id.ToString("N") + ".payload");
    private string RecordPath(Guid id) => Path.Combine(root, id.ToString("N") + ".record.json");
    private string PromotionPath(Guid id) => Path.Combine(root, id.ToString("N") + ".promoted.json");
    private void WriteIntent(Intent intent) => WriteDocument(IntentPath, intent);
    private Intent ReadIntent()
    {
        var intent = ReadDocument<Intent>(IntentPath);
        if (!ValidIntent(intent))
            throw new IOException("Unknown native operation is preserved.");
        return intent;
    }
    private bool ValidIntent(Intent intent) => intent.OwnerHash == ownerHash && (intent.Operation switch
    {
        "create" => intent.Path is { } path && Generated(path) && JournalDeltaStore.Normalize(path) == path && intent.Association is null && intent.Destination is null,
        "promote" => intent.Id is { } id && intent.Association is { } association && association.Id == id && association.OwnerHash == ownerHash &&
            intent.Path is { } path && Generated(path) && intent.Destination is { } destination && !Generated(destination) &&
            JournalDeltaStore.Normalize(destination) == destination && intent.Length >= 0 && intent.Sha256 is { Length: 64 },
        "reclaim" => intent.Id is { } id && intent.Association is { } association && association.Id == id && association.OwnerHash == ownerHash,
        _ => false
    });
    private bool ValidIntentTransition(Intent previous, Intent next)
    {
        if (previous == next) return true;
        // Creation is the only operation that replaces a durable intent: the
        // namespace assigns its ID between the first intent and this update.
        // Preserve both documents for any other transition, even if each has a
        // valid envelope and belongs to this same owner.
        return previous is { Operation: "create", Id: null } && next.Id is { } id &&
            previous with { Id = id } == next && Journal.Stat(next.Path!)?.Id == id;
    }
    private bool ValidPreparedAssociation(Association association, Guid id)
    {
        if (association.Id != id || association.OwnerHash != ownerHash) return false;
        using var payload = OpenPayload(PayloadPath(id), false);
        var info = ValidateHandle(payload, PayloadPath(id));
        return info.Length == 0 && info.VolumeSerialNumber == association.VolumeSerial && info.Index == association.FileIndex;
    }
    private void RecoverIntent(Intent intent)
    {
        if (intent.OwnerHash != ownerHash) throw new IOException("Native operation owner mismatch.");
        switch (intent.Operation)
        {
            case "create": CompleteCreate(intent); break;
            case "promote": CompletePromotion(intent); break;
            case "reclaim": CompleteReclaim(intent); break;
            default: throw new IOException("Unknown native operation is preserved.");
        }
    }
    private Guid CompleteCreate(Intent intent)
    {
        if (intent.Path is null || !Generated(intent.Path) || JournalDeltaStore.Normalize(intent.Path) != intent.Path)
            throw new IOException("Invalid native creation intent.");
        var entry = intent.Id is { } known ? Journal.Get(known) : Journal.Stat(intent.Path) ?? Journal.Create(intent.Path, false, intent.Attributes);
        if (entry.Path != intent.Path || entry.Directory || entry.BasePath is not null || entry.Length != 0 || entry.Deleted)
            throw new IOException("Native creation namespace no longer matches.");
        if (intent.Id is null) { intent = intent with { Id = entry.Id }; WriteIntent(intent); }
        interruptionObserver?.Invoke("create.namespace-durable");
        if (!files.ContainsKey(entry.Id))
        {
            string path = PayloadPath(entry.Id);
            SafeFileHandle handle = OpenPayload(path, create: !File.Exists(path));
            try
            {
                var info = ValidateHandle(handle, path);
                if (info.Length != 0) throw new IOException("Unacknowledged new native file contains unknown data.");
                var association = new Association(entry.Id, ownerHash, info.VolumeSerialNumber, info.Index);
                NativeMethods.Flush(handle);
                string record = RecordPath(entry.Id); RecoverPending<Association>(record, saved => saved == association);
                if (File.Exists(record)) { if (ReadDocument<Association>(record) != association) throw new IOException("Created association differs."); }
                else WriteDocument(record, association);
                interruptionObserver?.Invoke("create.association-durable");
                SetTimes(handle, entry.CreationTime, entry.LastAccessTime, entry.LastWriteTime);
                files.Add(entry.Id, new(association, handle, path)); handle = null!;
            }
            finally { handle?.Dispose(); }
        }
        DeleteControl(IntentPath); return entry.Id;
    }
    private void OpenBacking(Association association, Intent? intent)
    {
        string path = PayloadPath(association.Id); SafeFileHandle handle = OpenPayload(path, false);
        try
        {
            var backing = new Backing(association, handle, path); var info = Info(backing, checkLength: false); ValidateHandle(handle, path);
            RequireNativeCapacity(backing, info.Length); UpdateLength(backing, info.Length);
            string promotion = PromotionPath(association.Id);
            RecoverPending<Promotion>(promotion, saved => intent is { Operation: "promote" } && intent.Id == association.Id &&
                saved == new Promotion(association.Id, ownerHash, intent.Length, intent.Sha256!));
            if (File.Exists(promotion))
            {
                var marker = ReadDocument<Promotion>(promotion);
                if (marker.Id != association.Id || marker.OwnerHash != ownerHash) throw new IOException("Promotion marker identity differs.");
                backing.Promoted = true;
            }
            files.Add(association.Id, backing); handle = null!;
        }
        finally { handle?.Dispose(); }
    }
    private void CompletePromotion(Intent intent)
    {
        if (intent.Id is not { } id || intent.Path is null || intent.Destination is null || intent.Sha256 is null || intent.Length < 0 ||
            !files.TryGetValue(id, out var backing) || backing.Association != intent.Association || !Generated(intent.Path) || Generated(intent.Destination))
            throw new IOException("Invalid native promotion intent.");
        var entry = Journal.Get(id);
        if (entry.Deleted || entry.Directory || entry.Path != intent.Path && entry.Path != intent.Destination)
            throw new IOException("Native promotion namespace changed.");
        var info = Info(backing);
        if (info.Length != intent.Length || NativeMethods.Hash(backing.Handle) != intent.Sha256)
            throw new IOException("Flushed native promotion input changed.");
        if (!backing.Promoted)
        {
            Journal.SetLength(id, 0); byte[] buffer = new byte[Identity.MaxTransferBytes];
            for (long offset = 0; offset < intent.Length;)
            {
                int count = RandomAccess.Read(backing.Handle, buffer.AsSpan(0, (int)Math.Min(buffer.Length, intent.Length - offset)), offset);
                if (count == 0) throw new EndOfStreamException();
                Journal.Write(id, offset, buffer.AsSpan(0, count)); offset += count;
            }
            Journal.SetLength(id, intent.Length);
            Journal.SetMetadata(id, entry.Attributes, info.Creation, info.Access, info.Write); Journal.Flush();
            if (HashJournal(id, intent.Length) != intent.Sha256) throw new IOException("Durable source promotion differs.");
            interruptionObserver?.Invoke("promotion.journal-imported");
            var marker = new Promotion(id, ownerHash, intent.Length, intent.Sha256);
            WriteDocument(PromotionPath(id), marker); backing.Promoted = true;
            interruptionObserver?.Invoke("promotion.marker-durable");
        }
        else if (HashJournal(id, intent.Length) != intent.Sha256) throw new IOException("Interrupted source promotion journal changed.");
        if (entry.Path != intent.Destination) Journal.Rename(id, intent.Destination, intent.Replace);
        if (Journal.Stat(intent.Destination)?.Id != id) throw new IOException("Promoted source publication failed.");
        interruptionObserver?.Invoke("promotion.namespace-published");
        promotions++; DeleteControl(IntentPath);
    }
    private void CompleteReclaim(Intent intent)
    {
        if (intent.Id is not { } id || intent.Association is not { } association || association.Id != id || association.OwnerHash != ownerHash)
            throw new IOException("Invalid native reclamation intent.");
        try { if (!Journal.Get(id).Deleted) throw new IOException("Cannot reclaim a linked native identity."); }
        catch (FileNotFoundException) { /* A retry after the durable forget frame. */ }
        if (files.TryGetValue(id, out var backing))
        {
            if (backing.Association != association) throw new IOException("Reclamation association changed.");
            try { Info(backing); interruptionObserver?.Invoke("reclaim.before-payload-delete"); DeleteHandle(backing.Handle); }
            finally { backing.Handle.Dispose(); }
            files.Remove(id); nativePayloadBytes -= backing.Length;
            interruptionObserver?.Invoke("reclaim.after-payload-delete");
        }
        else if (File.Exists(PayloadPath(id)))
        {
            using var handle = OpenPayload(PayloadPath(id), false); var info = ValidateHandle(handle, PayloadPath(id));
            if (info.Index != association.FileIndex || info.VolumeSerialNumber != association.VolumeSerial) throw new IOException("Reclamation target changed.");
            DeleteHandle(handle);
        }
        DeleteControl(RecordPath(id)); DeleteControl(PromotionPath(id)); Journal.ReleaseUnlinkedFile(id); DeleteControl(IntentPath);
    }
    private void PreflightRename(DeltaEntry entry, string destination, bool replace)
    {
        if (entry.Path.Length == 0 || entry.Deleted || destination.Length == 0 || IsChild(destination, entry.Path)) throw new IOException("Invalid rename.");
        if (Identity.DeniedTopLevel?.Contains(destination.Split('/')[0], StringComparer.OrdinalIgnoreCase) == true)
            throw new UnauthorizedAccessException("This administration root is reserved by the workspace isolation policy.");
        int slash = destination.LastIndexOf('/'); string parent = slash < 0 ? "" : destination[..slash];
        if (Journal.Stat(parent) is not { Directory: true }) throw new DirectoryNotFoundException(parent);
        var existing = Journal.Stat(destination);
        if (existing is not null && existing.Id != entry.Id && (!replace || existing.Directory || entry.Directory)) throw new IOException("Rename destination exists.");
    }
    private string HashJournal(Guid id, long length)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); byte[] buffer = new byte[Identity.MaxTransferBytes];
        for (long offset = 0; offset < length;)
        {
            int count = Journal.Read(id, offset, buffer.AsSpan(0, (int)Math.Min(buffer.Length, length - offset)));
            if (count == 0) throw new EndOfStreamException(); hash.AppendData(buffer.AsSpan(0, count)); offset += count;
        }
        if (Journal.Get(id).Length != length) throw new IOException("Promoted journal length differs.");
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
    private void FlushBacking(Backing backing)
    {
        long started = Stopwatch.GetTimestamp();
        try { Info(backing); NativeMethods.Flush(backing.Handle); interruptionObserver?.Invoke("native.after-payload-flush"); }
        finally { nativeFlushes++; nativeFlushTicks += Stopwatch.GetTimestamp() - started; }
    }
    private void RequireNativeCapacity(Backing backing, long newLength)
    {
        long growth = newLength - backing.Length;
        if (growth > 0 && growth > Identity.MaxJournalBytes - nativePayloadBytes)
            throw new WorkspaceException("DeltaFull", "The separate native generated-payload byte budget is full; preserve the workspace.");
    }
    private void UpdateLength(Backing backing, long length) { nativePayloadBytes += length - backing.Length; backing.Length = length; }
    private static NativeMethods.ByHandleInfo Info(Backing backing, bool checkLength = true)
    {
        // The owned read/write/delete handle excludes foreign writes and delete
        // sharing. Its path was verified at admission and cannot be replaced
        // while held; repeat identity/link/attribute checks without resolving the
        // same final path for every virtual I/O callback.
        var info = NativeMethods.Info(backing.Handle);
        if ((info.Attributes & 0x410) != 0 || info.Links != 1 || info.Index != backing.Association.FileIndex || info.VolumeSerialNumber != backing.Association.VolumeSerial ||
            checkLength && info.Length != backing.Length)
            throw new IOException("Native payload identity changed.");
        return info;
    }
    private static NativeMethods.ByHandleInfo ValidateHandle(SafeFileHandle handle, string path)
    {
        var info = NativeMethods.Info(handle);
        if ((info.Attributes & 0x410) != 0 || info.Links != 1 || !NativeMethods.FinalPath(handle).Equals("\\\\?\\" + path, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Native payload requires its pinned ordinary single-link identity.");
        return info;
    }
    private static SafeFileHandle OpenPayload(string path, bool create)
    {
        var handle = CreateFileW("\\\\?\\" + Path.GetFullPath(path), 0xC0010000, 1, IntPtr.Zero, create ? 1u : 3u, 0x00200000, IntPtr.Zero);
        if (!handle.IsInvalid) return handle;
        int error = Marshal.GetLastWin32Error(); handle.Dispose(); throw new Win32Exception(error, "Cannot own native payload: " + path);
    }
    private static void DeleteHandle(SafeFileHandle handle)
    { var info = new Disposition { Delete = true }; if (!SetFileInformationByHandle(handle, 4, ref info, (uint)Marshal.SizeOf<Disposition>())) throw new Win32Exception(Marshal.GetLastWin32Error()); }
    private static void SetTimes(SafeFileHandle handle, long creation, long access, long write)
    { if (!SetFileTime(handle, ref creation, ref access, ref write)) throw new Win32Exception(Marshal.GetLastWin32Error()); }
    private static string IdentityHash(OverlayIdentity identity) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(identity)));
    private static void CheckRegular(string path)
    { using var file = NativeMethods.Open(path, 0x80000000, 1, false); var info = NativeMethods.Info(file); if ((info.Attributes & 0x410) != 0 || info.Links != 1) throw new IOException("Unsupported native control file."); }
    private static T ReadDocument<T>(string path)
    {
        CheckRegular(path); using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > 65536) throw new IOException("Native control file exceeds its bounded schema.");
        var document = JsonSerializer.Deserialize<Envelope<T>>(stream, Json) ?? throw new IOException("Missing native control document.");
        if (document.Version != 1 || document.Payload is null || document.Sha256 != Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(document.Payload, Json))))
            throw new IOException("Unsupported or corrupt native control document.");
        return document.Payload;
    }
    private static void WriteDocument<T>(string path, T value)
    {
        string pending = path + ".pending";
        if (File.Exists(pending)) throw new IOException("Preserve an unfinished native control publication.");
        string hash = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, Json)));
        using (var stream = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { JsonSerializer.Serialize(stream, new Envelope<T>(1, value, hash), Json); stream.Flush(true); }
        File.Move(pending, path, true);
    }
    private static void RecoverPending<T>(string path, Func<T, bool> validate, Func<T, T, bool>? validTransition = null)
    {
        string pending = path + ".pending";
        if (!File.Exists(pending)) return;
        T value = ReadDocument<T>(pending);
        if (!validate(value)) throw new IOException("Prepared native control ownership differs; preserve both generations.");
        if (File.Exists(path))
        {
            T previous = ReadDocument<T>(path);
            if (!validate(previous) || !(validTransition?.Invoke(previous, value) ?? EqualityComparer<T>.Default.Equals(previous, value)))
                throw new IOException("Unsupported native control generation transition; preserve both generations.");
        }
        File.Move(pending, path, true);
    }
    private static void DeleteControl(string path)
    {
        if (!File.Exists(path)) return;
        using var handle = OpenPayload(path, false); ValidateHandle(handle, path); DeleteHandle(handle);
    }
    private static HashSet<string> ExpectedInventory(string directory, IEnumerable<(Guid Id, bool Promoted)> entries)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Path.Combine(directory, "owner.json"), Path.Combine(directory, "writer.lock") };
        foreach (var (id, promoted) in entries)
        {
            string prefix = Path.Combine(directory, id.ToString("N")); names.Add(prefix + ".payload"); names.Add(prefix + ".record.json");
            if (promoted) names.Add(prefix + ".promoted.json");
        }
        return names;
    }
    private static void RequireExactInventory(string directory, HashSet<string> expected)
    {
        var actual = Directory.EnumerateFileSystemEntries(directory).ToArray();
        if (!expected.SetEquals(actual)) throw new IOException("Unknown or missing native backing files are preserved.");
        foreach (string path in actual)
            if ((File.GetAttributes(path) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0) throw new IOException("Unsupported native backing entry.");
    }
    [StructLayout(LayoutKind.Sequential)] private struct Disposition { [MarshalAs(UnmanagedType.Bool)] public bool Delete; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle handle, int informationClass, ref Disposition info, uint size);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetFileTime(SafeFileHandle handle, ref long creation, ref long access, ref long write);
}
