using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Splinter.Core;

namespace Splinter.Runtime;

public sealed record MetadataCollectionResult(string RequestKey, long ReclaimedBytes, long RetainedBytes, bool AlreadyCompleted,
    long PreservedEntries = 0, string? PreservedDirectory = null, MetadataTreeRoot? PreservedRoot = null);
public sealed record PendingMetadataCollection(string RequestKey, string Phase);

public sealed partial class PagedMetadataStore
{
    private static readonly JsonSerializerOptions CollectionJson = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectRequiredConstructorParameters = true };
    private sealed record ContainerOwner(int Version, string StoreId, long Container, string Identity);
    private sealed record LocationHeader(int Version, string StoreId, string FileName, long Rows, string Digest, string Identity);
    private sealed record CollectionIntent(int Version, string StoreId, string RequestKey, string RootFile, string RootIdentity,
        long MaximumContainer, string Phase, LocationHeader? Previous, LocationHeader? Published = null,
        long ReclaimedBytes = 0, long RetainedBytes = 0, string? Attempt = null, PreservedInventory? Preserved = null,
        ContainerReclaim? Reclaiming = null, string StateHash = "");
    private sealed record ContainerReclaim(long Container, string Identity, long Bytes);
    private sealed record PreservedInventory(string Directory, MetadataTreeRoot Root);

    public static PendingMetadataCollection? PendingCollection(string directory)
    {
        string path = Path.Combine(Path.GetFullPath(directory), "collection.json");
        if (!File.Exists(path)) return null;
        var value = ReadCollection<CollectionIntent>(path);
        if (value.Version != 2 || string.IsNullOrWhiteSpace(value.RequestKey) || value.RequestKey.Length > 128 ||
            value.Phase is not ("copying" or "prepared" or "published" or "cleanup" or "completed")) throw Invalid("Preserve unsupported metadata collection state.");
        if (value.StoreId != ReadStoreIdentity(directory)) throw Invalid("Preserve a foreign pending metadata collection.");
        return new(value.RequestKey, value.Phase);
    }

    public static MetadataCollectionResult? CollectionReceipt(string directory, string requestKey)
    {
        string path = Path.Combine(Path.GetFullPath(directory), "collections", WorkspaceStore.Key(requestKey) + ".json");
        if (!File.Exists(path)) return null;
        var value = ReadCollection<CollectionIntent>(path);
        if (value.Version != 2 || value.StoreId != ReadStoreIdentity(directory) || value.RequestKey != requestKey || value.Phase != "completed")
            throw Invalid("Preserve a mismatched metadata collection receipt.");
        if (value.Preserved is { } preserved)
        {
            string location = ExactChild(directory, preserved.Directory, "collection-preserved-", "");
            using var inventory = new PagedMetadataStore(location); _ = inventory.Open(preserved.Root);
        }
        return CollectionResult(value, true);
    }

    // A scheduling hint, never a total storage quota. It does not reject input
    // and deliberately overestimates debt when a failed attempt left scratch.
    public static long AppendedBytesSinceCollection(string directory)
    {
        if (!System.IO.Directory.Exists(directory)) return 0;
        long bytes = 0;
        foreach (string path in System.IO.Directory.EnumerateFiles(directory, "pages-*.bin")) bytes = checked(bytes + new FileInfo(path).Length);
        string location = Path.Combine(directory, "locations.json");
        if (!File.Exists(location)) return bytes;
        var header = ReadCollection<LocationHeader>(location);
        var owner = ReadCollection<MapOwner>(ExactChild(directory, header.FileName, "locations-", ".bin") + ".owner");
        if (header.Version != 1 || owner.Version != 1 || owner.StoreId != header.StoreId || string.IsNullOrWhiteSpace(owner.RequestKey))
            throw Invalid("Preserve a foreign metadata allocation scheduling hint.");
        string receipt = Path.Combine(directory, "collections", WorkspaceStore.Key(owner.RequestKey) + ".json");
        if (!File.Exists(receipt)) return bytes;
        var completed = ReadCollection<CollectionIntent>(receipt);
        if (completed.Version != 2 || completed.Phase != "completed" || completed.StoreId != header.StoreId || completed.Published?.FileName != header.FileName)
            throw Invalid("Preserve a mismatched metadata collection scheduling hint.");
        return Math.Max(0, bytes - completed.RetainedBytes);
    }

    // Roots are frozen before any page is copied. The owner supplies the full
    // authoritative live set while holding its publication lock. Existing roots
    // keep both their logical identity and physical reference after collection.
    public static MetadataCollectionResult Collect(string directory, IEnumerable<MetadataTreeRoot> roots, string requestKey,
        IJournalFilePins filePins, Action<string>? boundary = null, CancellationToken ct = default, IEnumerable<MetadataEntry>? protectedExternal = null)
    {
        ArgumentNullException.ThrowIfNull(filePins);
        if (string.IsNullOrWhiteSpace(requestKey) || requestKey.Length > 128) throw Invalid("A stable metadata collection request key is required.");
        directory = Path.GetFullPath(directory); WorkspaceStore.EnsurePlainPath(directory);
        string active = Path.Combine(directory, "collection.json"), completedDirectory = Path.Combine(directory, "collections");
        string completed = Path.Combine(completedDirectory, WorkspaceStore.Key(requestKey) + ".json");
        using var exclusive = new FileStream(Path.Combine(directory, "writer.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        if (File.Exists(completed))
        {
            var receipt = ReadCollection<CollectionIntent>(completed);
            using (var current = new PagedMetadataStore(directory, false, boundary, filePins, maintenance: true, containerStart: null))
                if (receipt.Version != 2 || receipt.StoreId != current.StoreId || receipt.RequestKey != requestKey || receipt.Phase != "completed")
                    throw Invalid("Preserve a mismatched metadata collection receipt.");
            return CollectionResult(receipt, true);
        }
        using var source = new PagedMetadataStore(directory, false, boundary, filePins, maintenance: true, containerStart: null);
        if (source.locations is not null)
        {
            string location = ExactChild(directory, source.locations.Header.FileName, "locations-", ".bin");
            using var input = new FileStream(location, FileMode.Open, FileAccess.Read, FileShare.Read);
            filePins.Verify(input, source.locations.Header.Identity);
        }
        CollectionIntent intent;
        if (File.Exists(active))
        {
            intent = ReadCollection<CollectionIntent>(active);
            if (intent.Version != 2 || intent.StoreId != source.StoreId || intent.RequestKey != requestKey ||
                intent.Phase is not ("copying" or "prepared" or "published" or "cleanup" or "completed"))
                throw Invalid("Resume the exact unfinished metadata collection before writing or collecting again.");
        }
        else
        {
            long maximum = -1;
            foreach (string path in System.IO.Directory.EnumerateFiles(directory, "pages-*.bin"))
            {
                long number = ParseContainer(path); maximum = Math.Max(maximum, number);
                using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                try { source.VerifyContainerOwner(input, number); }
                catch (Exception error) when (input.Length <= ContainerHeaderBytes && error is IOException or WorkspaceException) { continue; }
                source.CheckContainer(input);
            }
            string rootName = "collection-roots-" + Guid.NewGuid().ToString("N") + ".bin", rootFile = Path.Combine(directory, rootName);
            string rootIdentity;
            using (var output = new FileStream(rootFile, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read, 65536))
            {
                rootIdentity = filePins.Identify(rootFile);
                // This durable ownership intent precedes every root byte.
                intent = new(2, source.StoreId, requestKey, rootName, rootIdentity, maximum, "copying", source.locations?.Header);
                WriteCollection(active, intent, replace: false);
                foreach (var root in roots)
                {
                    ct.ThrowIfCancellationRequested(); source.CheckRoot(root);
                    output.WriteByte(root.Page is null ? (byte)0 : (byte)1);
                    if (root.Page is not null) { _ = source.ReadNode(root.Page); WriteReference(output, root.Page); }
                }
                output.Flush(true);
            }
            // A separate seal prevents a torn root spool from being accepted.
            WriteCollection(active + ".roots", new RootSeal(1, rootIdentity, HashFile(rootFile)));
            boundary?.Invoke("metadata.collection-roots-durable");
        }
        if (intent.Phase is "cleanup" or "completed")
        { source.Dispose(); return CompleteCollection(directory, active, completed, intent, filePins, boundary); }
        string rootsPath = ExactChild(directory, intent.RootFile, "collection-roots-", ".bin");
        using (var rootInput = new FileStream(rootsPath, FileMode.Open, FileAccess.Read, FileShare.Read)) filePins.Verify(rootInput, intent.RootIdentity);
        if (!File.Exists(active + ".roots"))
        {
            // The request has not accepted a frozen root set until its seal.
            // A torn unsealed spool can be rewritten only through the exact
            // recorded native file, while the publication lock is held again.
            using var output = new FileStream(rootsPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            filePins.Verify(output, intent.RootIdentity); output.SetLength(0);
            foreach (var root in roots)
            {
                ct.ThrowIfCancellationRequested(); source.CheckRoot(root); output.WriteByte(root.Page is null ? (byte)0 : (byte)1);
                if (root.Page is not null) { _ = source.ReadNode(root.Page); WriteReference(output, root.Page); }
            }
            output.Flush(true); WriteCollection(active + ".roots", new RootSeal(1, intent.RootIdentity, HashFile(rootsPath)));
        }
        var seal = ReadCollection<RootSeal>(active + ".roots");
        if (seal.Version != 1 || seal.Identity != intent.RootIdentity || seal.Digest != HashFile(rootsPath))
            throw Invalid("Preserve an incomplete or changed frozen metadata root set.");

        if (intent.Phase == "copying")
        {
            string attempt = Path.Combine(directory, "collection-work-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(attempt);
            WriteCollection(Path.Combine(attempt, "collection-work.owner.json"), new WorkOwner(1, source.StoreId, requestKey));
            File.Copy(Path.Combine(directory, "identity.bin"), Path.Combine(attempt, "identity.bin"));
            string rowsPath = Path.Combine(attempt, "relocations.bin");
            using var rows = new FileStream(rowsPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read | FileShare.Delete, 1, FileOptions.RandomAccess | FileOptions.DeleteOnClose);
            long rowCount = 0;
            MetadataPageReference Lookup(MetadataPageReference reference)
            {
                var canonical = source.ResolveReference(reference);
                return FindLocation(rows, rowCount, canonical) ?? throw Invalid("A live metadata root is absent from its completed copy.");
            }
            using (var destination = new PagedMetadataStore(attempt, true, boundary, filePins, maintenance: true,
                containerStart: checked(intent.MaximumContainer + 1)))
            {
                foreach (var _ in source.CopyRootsToCore(destination, ReadRoots(rootsPath, source.StoreId), attempt, ct, true, (old, next) =>
                {
                    WriteLocation(rows, rowCount++, old, next);
                })) { }
                destination.Flush();
            }
            boundary?.Invoke("metadata.collection-payload-durable");
            IEnumerable<MetadataEntry> Mapping()
            {
                for (long index = 0; index < rowCount; index++)
                {
                    ct.ThrowIfCancellationRequested(); byte[] row = new byte[76]; ReadMapping(rows, checked(index * 76), row);
                    yield return new(AddressKey(row), row);
                }
                foreach (var root in ReadRoots(rootsPath, source.StoreId))
                    if (root.Page is { } original)
                    {
                        byte[] row = LocationRow(original, Lookup(original));
                        yield return new(AddressKey(row), row);
                    }
            }
            string mapName = "locations-" + Guid.NewGuid().ToString("N") + ".bin", mapPath = Path.Combine(directory, mapName);
            string mapIdentity; long count = 0;
            using (var map = new FileStream(mapPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 65536))
            {
                mapIdentity = filePins.Identify(mapPath);
                WriteCollection(mapPath + ".owner", new MapOwner(1, source.StoreId, requestKey, mapIdentity), replace: false);
                byte[]? previous = null;
                foreach (var entry in MetadataExternalSort.Order(Mapping(), attempt, ct, filePins))
                {
                    byte[] row = entry.Value.ToArray();
                    if (previous is not null && row.AsSpan(0, 16).SequenceEqual(previous.AsSpan(0, 16)))
                    { if (!row.AsSpan().SequenceEqual(previous)) throw Invalid("A collection address maps to conflicting contents."); continue; }
                    map.Write(row); previous = row; count = checked(count + 1);
                }
                map.Flush(true);
            }
            var header = new LocationHeader(1, source.StoreId, mapName, count, HashFile(mapPath), mapIdentity);
            long retained = 0;
            foreach (string path in System.IO.Directory.EnumerateFiles(attempt, "pages-*.bin"))
            {
                ct.ThrowIfCancellationRequested();
                retained = checked(retained + new FileInfo(path).Length);
            }
            intent = intent with { Phase = "prepared", Published = header, RetainedBytes = retained, Attempt = Path.GetFileName(attempt) };
            WriteCollection(active, intent);
            boundary?.Invoke("metadata.collection-copy-prepared");
        }
        if (intent.Phase == "prepared")
        {
            string attempt = ExactChild(directory, intent.Attempt ?? "", "collection-work-", "");
            // The intent owns the whole move list through the isolated attempt
            // directory. Moving either receipt or page can be resumed exactly.
            foreach (string ownerPath in System.IO.Directory.EnumerateFiles(attempt, "pages-*.bin.owner"))
            {
                ct.ThrowIfCancellationRequested(); string path = ownerPath[..^6]; long number = ParseContainer(path);
                using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var owner = ReadCollection<ContainerOwner>(path + ".owner"); filePins.Verify(input, owner.Identity);
                    if (owner.Version != 1 || owner.StoreId != source.StoreId || owner.Container != number) throw Invalid("Preserve a foreign copied container.");
                }
                string installed = Path.Combine(directory, Path.GetFileName(path));
                File.Move(ownerPath, installed + ".owner");
                File.Move(path, installed);
                boundary?.Invoke("metadata.collection-container-installed");
            }
            // A crash can move the ownership receipt before its page.
            foreach (string path in System.IO.Directory.EnumerateFiles(attempt, "pages-*.bin"))
            {
                string installed = Path.Combine(directory, Path.GetFileName(path));
                var owner = ReadCollection<ContainerOwner>(installed + ".owner");
                using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) filePins.Verify(input, owner.Identity);
                File.Move(path, installed);
            }
            ValidateCopiedLocations(source, intent, filePins, ct);
            intent = intent with { Phase = "published" };
            WriteCollection(active, intent);
            WriteCollection(Path.Combine(directory, "locations.json"), intent.Published!);
            boundary?.Invoke("metadata.collection-location-published");
        }
        else if (intent.Published is null) throw Invalid("An unfinished collection lost its location publication.");

        // Re-publishing the exact completed location is idempotent after a lost
        // reply between its intent and atomic header publication.
        ValidateCopiedLocations(source, intent, filePins, ct);
        WriteCollection(Path.Combine(directory, "locations.json"), intent.Published!);
        source.Dispose();
        if (intent.Reclaiming is not null) intent = FinishContainerReclaim(directory, active, intent, filePins, boundary);
        foreach (string path in System.IO.Directory.EnumerateFiles(directory, "pages-*.bin"))
        {
            ct.ThrowIfCancellationRequested(); long number = ParseContainer(path);
            if (number > intent.MaximumContainer) continue;
            ContainerOwner owner;
            try { owner = ReadCollection<ContainerOwner>(path + ".owner"); }
            catch (Exception error) when (new FileInfo(path).Length <= ContainerHeaderBytes && error is IOException or WorkspaceException) { continue; }
            long bytes = new FileInfo(path).Length;
            if (owner.Version != 1 || owner.StoreId != intent.StoreId || owner.Container != number)
            { if (bytes <= ContainerHeaderBytes) continue; throw Invalid("Preserve an unowned metadata container."); }
            if (bytes <= ContainerHeaderBytes)
            {
                using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                try { filePins.Verify(input, owner.Identity); } catch (WorkspaceException) { continue; }
            }
            intent = intent with { Reclaiming = new(number, owner.Identity, bytes) }; WriteCollection(active, intent);
            boundary?.Invoke("metadata.collection-reclaim-intent-durable");
            intent = FinishContainerReclaim(directory, active, intent, filePins, boundary);
        }
        if (intent.Previous is { } prior && prior.FileName != intent.Published!.FileName)
        {
            string priorPath = ExactChild(directory, prior.FileName, "locations-", ".bin");
            if (File.Exists(priorPath)) filePins.Unpin(priorPath, prior.Identity);
            if (File.Exists(priorPath + ".owner")) File.Delete(priorPath + ".owner");
        }
        var preserved = PreserveUnknownWork(directory, intent, filePins, ct, protectedExternal);
        intent = intent with { Phase = "cleanup", Preserved = preserved }; WriteCollection(active, intent);
        boundary?.Invoke("metadata.collection-cleanup-durable");
        return CompleteCollection(directory, active, completed, intent, filePins, boundary);
    }

    private sealed record RootSeal(int Version, string Identity, string Digest);
    private sealed record MapOwner(int Version, string StoreId, string RequestKey, string Identity);
    private sealed record WorkOwner(int Version, string StoreId, string RequestKey);
    private static MetadataCollectionResult CollectionResult(CollectionIntent intent, bool repeated) =>
        new(intent.RequestKey, intent.ReclaimedBytes, intent.RetainedBytes, repeated, intent.Preserved?.Root.Count ?? 0,
            intent.Preserved?.Directory, intent.Preserved?.Root);
    private static void ValidateCopiedLocations(PagedMetadataStore source, CollectionIntent intent, IJournalFilePins pins, CancellationToken ct)
    {
        if (intent.Published is null || intent.MaximumContainer < -1) throw Invalid("Preserve an incomplete metadata location publication.");
        using var map = new LocationMap(source.directory, source.StoreId, intent.Published);
        pins.Verify(map.Input, intent.Published.Identity);
        byte[] row = new byte[76];
        for (long index = 0; index < intent.Published.Rows; index++)
        {
            ct.ThrowIfCancellationRequested(); ReadMapping(map.Input, checked(index * 76), row);
            long originalContainer = BinaryPrimitives.ReadInt64LittleEndian(row), originalOffset = BinaryPrimitives.ReadInt64LittleEndian(row.AsSpan(8));
            var target = DecodeReference(row.AsSpan(16)); CheckReference(target);
            if (originalContainer < 0 || originalContainer > intent.MaximumContainer || originalOffset < ContainerHeaderBytes || target.Container <= intent.MaximumContainer)
                throw Invalid("Copied metadata addresses conflict with the frozen reclamation boundary.");
            // Every reachable page, including child maps and published aliases,
            // is reread through native container ownership +checksum proof
            // before any old container becomes eligible for deletion.
            _ = source.ReadNode(target);
        }
    }
    private static CollectionIntent FinishContainerReclaim(string directory, string active, CollectionIntent intent,
        IJournalFilePins pins, Action<string>? boundary)
    {
        var deleting = intent.Reclaiming ?? throw Invalid("A metadata reclaim completion lost its fixed native identity.");
        if (deleting.Container < 0 || deleting.Container > intent.MaximumContainer || deleting.Bytes < 0)
            throw Invalid("Preserve a malformed metadata reclaim intent.");
        string path = Path.Combine(directory, "pages-" + deleting.Container.ToString("x16", CultureInfo.InvariantCulture) + ".bin");
        if (File.Exists(path + ".owner"))
        {
            var owner = ReadCollection<ContainerOwner>(path + ".owner");
            if (owner.Version != 1 || owner.StoreId != intent.StoreId || owner.Container != deleting.Container || owner.Identity != deleting.Identity)
                throw Invalid("Preserve a changed metadata reclaim owner.");
        }
        if (File.Exists(path)) pins.Unpin(path, deleting.Identity);
        boundary?.Invoke("metadata.collection-container-unlinked");
        if (File.Exists(path + ".owner")) File.Delete(path + ".owner");
        intent = intent with { ReclaimedBytes = checked(intent.ReclaimedBytes + deleting.Bytes), Reclaiming = null };
        WriteCollection(active, intent); boundary?.Invoke("metadata.collection-container-reclaimed"); return intent;
    }
    private static MetadataCollectionResult CompleteCollection(string directory, string active, string completed,
        CollectionIntent intent, IJournalFilePins pins, Action<string>? boundary)
    {
        string roots = ExactChild(directory, intent.RootFile, "collection-roots-", ".bin");
        if (File.Exists(roots)) pins.Unpin(roots, intent.RootIdentity);
        string seal = Path.Combine(directory, "collection.json.roots");
        if (File.Exists(seal)) File.Delete(seal);
        boundary?.Invoke("metadata.collection-root-spool-reclaimed");
        intent = intent with { Phase = "completed" }; WriteCollection(active, intent);
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(completed)!);
        AtomicFilePublication.Publish(active, completed);
        boundary?.Invoke("metadata.collection-completed");
        return CollectionResult(intent, false);
    }
    private static PreservedInventory? PreserveUnknownWork(string directory, CollectionIntent intent, IJournalFilePins pins, CancellationToken ct, IEnumerable<MetadataEntry>? protectedExternal)
    {
        using var entries = CleanupUnreferencedWork(directory, intent, pins).Concat(protectedExternal ?? []).GetEnumerator();
        if (!entries.MoveNext()) return null;
        string name = "collection-preserved-" + WorkspaceStore.Key(intent.RequestKey) + "-" + Guid.NewGuid().ToString("N");
        string location = Path.Combine(directory, name); System.IO.Directory.CreateDirectory(location);
        WriteCollection(Path.Combine(location, "preserved.owner.json"), new WorkOwner(1, intent.StoreId, intent.RequestKey));
        IEnumerable<MetadataEntry> Remaining()
        { do { ct.ThrowIfCancellationRequested(); yield return entries.Current; } while (entries.MoveNext()); }
        using var index = new PagedMetadataStore(location, true, filePins: pins);
        var root = index.Build(MetadataExternalSort.Order(Remaining(), location, ct, pins), ct); index.Flush();
        return new(name, root);
    }
    private static IEnumerable<MetadataEntry> CleanupUnreferencedWork(string directory, CollectionIntent intent, IJournalFilePins pins)
    {
        // A failed residual-inventory build is scratch until a completed or
        // cleanup intent publishes its root. Reclaim proven objects and retain
        // unknown creation gaps in the next inventory.
        foreach (string prior in System.IO.Directory.EnumerateDirectories(directory, "collection-preserved-*"))
        {
            if (Path.GetFileName(prior) == intent.Preserved?.Directory) continue;
            bool referenced = false, removed = false; WorkOwner? owner = null;
            try
            {
                owner = ReadCollection<WorkOwner>(Path.Combine(prior, "preserved.owner.json"));
                string receipt = Path.Combine(directory, "collections", WorkspaceStore.Key(owner.RequestKey) + ".json");
                if (File.Exists(receipt)) referenced = ReadCollection<CollectionIntent>(receipt).Preserved?.Directory == Path.GetFileName(prior);
                if (!referenced && owner.Version == 1 && owner.StoreId == intent.StoreId && owner.RequestKey == intent.RequestKey)
                {
                    foreach (string scratch in System.IO.Directory.EnumerateDirectories(prior, "sort-*"))
                        _ = MetadataScratchOwnership.TryReclaimScope(scratch, "metadata-sort", pins);
                    var controls = new HashSet<string>(StringComparer.Ordinal) { "preserved.owner.json" };
                    ValidateReclaimInventory(prior, pins, controls);
                    foreach (var payload in EnumerateOwnedPayloads(prior, pins, controls)) pins.Unpin(payload.Path, payload.Identity);
                    foreach (string path in System.IO.Directory.EnumerateFiles(prior)) File.Delete(path);
                    System.IO.Directory.Delete(prior); removed = true;
                }
            }
            catch (Exception error) when (error is IOException or WorkspaceException) { }
            if (!referenced && !removed) yield return new("inventory/" + Path.GetFileName(prior), "unreferenced-inventory-creation-gap"u8.ToArray());
        }
        foreach (string receipt in System.IO.Directory.EnumerateFiles(directory, "pages-*.bin.owner"))
        {
            string path = receipt[..^6]; if (File.Exists(path)) continue;
            var owner = ReadCollection<ContainerOwner>(receipt);
            if (owner.Version != 1 || owner.StoreId != intent.StoreId || owner.Container != ParseContainer(path))
                throw Invalid("Preserve an unknown orphaned metadata ownership receipt.");
            File.Delete(receipt);
        }
        foreach (string path in System.IO.Directory.EnumerateFiles(directory, "locations-*.bin"))
        {
            if (Path.GetFileName(path) == intent.Published?.FileName) continue;
            bool removed = false, unrelated = false;
            try
            {
                var owner = ReadCollection<MapOwner>(path + ".owner");
                if (owner.Version == 1 && owner.StoreId == intent.StoreId && owner.RequestKey == intent.RequestKey)
                { pins.Unpin(path, owner.Identity); File.Delete(path + ".owner"); removed = true; }
                else unrelated = owner.Version == 1 && owner.StoreId == intent.StoreId;
            }
            catch (Exception error) when (error is IOException or WorkspaceException) { }
            if (!removed && !unrelated) yield return new("location/" + Path.GetFileName(path), "unreferenced-location-creation-gap"u8.ToArray());
        }
        foreach (string attempt in System.IO.Directory.EnumerateDirectories(directory, "collection-work-*"))
        {
            WorkspaceStore.EnsurePlainPath(attempt);
            string ownerPath = Path.Combine(attempt, "collection-work.owner.json");
            WorkOwner? work = null;
            try { if (File.Exists(ownerPath)) work = ReadCollection<WorkOwner>(ownerPath); }
            catch (Exception error) when (error is IOException or WorkspaceException) { }
            if (work is null || work.Version != 1 || work.StoreId != intent.StoreId)
            { yield return new("work/" + Path.GetFileName(attempt), "unknown-work-owner"u8.ToArray()); continue; }
            if (work.RequestKey != intent.RequestKey) continue;
            foreach (string scratch in System.IO.Directory.EnumerateDirectories(attempt))
            {
                string name = Path.GetFileName(scratch);
                if (name.StartsWith("repack-", StringComparison.Ordinal)) _ = MetadataScratchOwnership.TryReclaimScope(scratch, "metadata-repack", pins);
                else if (name.StartsWith("sort-", StringComparison.Ordinal)) _ = MetadataScratchOwnership.TryReclaimScope(scratch, "metadata-sort", pins);
            }
            foreach (string path in System.IO.Directory.EnumerateFiles(attempt, "pages-*.bin"))
            {
                try
                {
                    var owner = ReadCollection<ContainerOwner>(path + ".owner"); long number = ParseContainer(path);
                    if (owner.Version != 1 || owner.StoreId != intent.StoreId || owner.Container != number) continue;
                    pins.Unpin(path, owner.Identity); File.Delete(path + ".owner");
                }
                catch (Exception error) when (error is IOException or WorkspaceException) { }
            }
            bool emptyControls = true;
            foreach (string path in System.IO.Directory.EnumerateFileSystemEntries(attempt))
            {
                WorkspaceStore.EnsurePlainPath(path); string name = Path.GetFileName(path);
                if (System.IO.Directory.Exists(path) || name is not ("identity.bin" or "writer.lock" or "collection-work.owner.json"))
                    emptyControls = false;
            }
            if (emptyControls)
            {
                foreach (string path in System.IO.Directory.EnumerateFiles(attempt)) File.Delete(path);
                System.IO.Directory.Delete(attempt);
            }
            else yield return new("work/" + Path.GetFileName(attempt), "unreferenced-unknown-scratch"u8.ToArray());
        }
        foreach (string path in System.IO.Directory.EnumerateFiles(directory, "pages-*.bin"))
            if (new FileInfo(path).Length <= ContainerHeaderBytes && ParseContainer(path) <= intent.MaximumContainer)
                yield return new("header/" + Path.GetFileName(path), "unregistered-creation-gap"u8.ToArray());
        foreach (string path in System.IO.Directory.EnumerateFiles(directory))
        {
            string name = Path.GetFileName(path);
            if (name.EndsWith(".pending", StringComparison.Ordinal) || name.EndsWith(".tmp", StringComparison.Ordinal))
                yield return new("control/" + name, "unpublished-control-scratch"u8.ToArray());
        }
    }
    private void WriteContainerOwner(FileStream stream, long number)
    {
        string identity = filePins!.Identify(stream.Name); filePins.Verify(stream, identity);
        WriteCollection(stream.Name + ".owner", new ContainerOwner(1, StoreId, number, identity), replace: false);
        boundary?.Invoke("metadata.container-owner-durable");
    }
    private void VerifyContainerOwner(FileStream stream, long number)
    {
        var owner = ReadCollection<ContainerOwner>(stream.Name + ".owner");
        if (owner.Version != 1 || owner.StoreId != StoreId || owner.Container != number) throw Invalid("Preserve a metadata container without its exact owner.");
        filePins!.Verify(stream, owner.Identity);
    }
    private MetadataPageReference ResolveReference(MetadataPageReference reference)
    {
        if (locations is null) return reference;
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var resolved = locations.Find(reference);
            if (resolved is null) return reference;
            CheckReference(resolved);
            if (resolved.ContentHash != reference.ContentHash || resolved.Count != reference.Count || PhysicalOrder(resolved, reference) <= 0 || locations.Find(resolved) is not null)
                throw Invalid("Metadata relocation changed logical identity or formed an alias chain.");
            return resolved;
        }
    }
    private LocationMap? OpenLocations()
    {
        string path = Path.Combine(directory, "locations.json");
        return File.Exists(path) ? new LocationMap(directory, StoreId, ReadCollection<LocationHeader>(path)) : null;
    }
    private sealed class LocationMap : IDisposable
    {
        private readonly FileStream input;
        public FileStream Input => input;
        private readonly string scope = "locations/" + Guid.NewGuid().ToString("N") + "/";
        internal long BlockReadCount { get; private set; }
        public LocationHeader Header { get; }
        public LocationMap(string directory, string storeId, LocationHeader header)
        {
            Header = header;
            if (header.Version != 1 || header.StoreId != storeId || header.Rows < 0 || header.Rows > long.MaxValue / 76 ||
                header.Digest is not { Length: 64 } || header.Digest.Any(c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
                throw Invalid("Preserve unsupported metadata locations.");
            string path = ExactChild(directory, header.FileName, "locations-", ".bin");
            input = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.RandomAccess);
            try
            {
                if (input.Length != checked(header.Rows * 76) || Convert.ToHexStringLower(SHA256.HashData(input)) != header.Digest)
                    throw Invalid("The metadata location index is truncated or changed.");
            }
            catch { input.Dispose(); throw; }
        }
        public MetadataPageReference? Find(MetadataPageReference reference)
        {
            long low = 0, high = Header.Rows; Span<byte> row = stackalloc byte[76];
            while (low < high)
            {
                long middle = low + (high - low) / 2; ReadRow(checked(middle * 76), row);
                long container = BinaryPrimitives.ReadInt64LittleEndian(row), offset = BinaryPrimitives.ReadInt64LittleEndian(row[8..]);
                int order = container != reference.Container ? container.CompareTo(reference.Container) : offset.CompareTo(reference.Offset);
                if (order < 0) low = middle + 1; else if (order > 0) high = middle;
                else return DecodeReference(row[16..]);
            }
            return null;
        }
        private void ReadRow(long offset, Span<byte> destination)
        {
            while (!destination.IsEmpty)
            {
                long blockNumber = offset / MaximumPageBytes, start = blockNumber * MaximumPageBytes;
                string key = scope + blockNumber.ToString("x16", CultureInfo.InvariantCulture);
                if (!PageCache.TryGetBlock(key, out var block))
                {
                    block = new byte[checked((int)Math.Min(MaximumPageBytes, input.Length - start))];
                    ReadAt(input, block, start); BlockReadCount++; PageCache.AddBlock(key, block);
                }
                int within = checked((int)(offset - start)), length = Math.Min(destination.Length, block.Length - within);
                if (length <= 0) throw Invalid("A metadata location row ended early.");
                block.AsSpan(within, length).CopyTo(destination); destination = destination[length..]; offset += length;
            }
        }
        public void Dispose() { PageCache.Remove(scope); input.Dispose(); }
    }
    private static MetadataPageReference? FindLocation(FileStream input, long rows, MetadataPageReference reference)
    {
        long low = 0, high = rows; Span<byte> row = stackalloc byte[76];
        while (low < high)
        {
            long middle = low + (high - low) / 2; ReadMapping(input, checked(middle * 76), row);
            long container = BinaryPrimitives.ReadInt64LittleEndian(row), offset = BinaryPrimitives.ReadInt64LittleEndian(row[8..]);
            int order = container != reference.Container ? container.CompareTo(reference.Container) : offset.CompareTo(reference.Offset);
            if (order < 0) low = middle + 1; else if (order > 0) high = middle;
            else return DecodeReference(row[16..]);
        }
        return null;
    }
    private static byte[] LocationRow(MetadataPageReference old, MetadataPageReference next)
    {
        byte[] bytes = new byte[76]; BinaryPrimitives.WriteInt64LittleEndian(bytes, old.Container);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(8), old.Offset); EncodeReference(next).CopyTo(bytes, 16); return bytes;
    }
    private static string AddressKey(ReadOnlySpan<byte> row) => BinaryPrimitives.ReadInt64LittleEndian(row).ToString("x16", CultureInfo.InvariantCulture)
        + BinaryPrimitives.ReadInt64LittleEndian(row[8..]).ToString("x16", CultureInfo.InvariantCulture);
    private static void WriteLocation(FileStream stream, long index, MetadataPageReference old, MetadataPageReference next)
        => RandomAccess.Write(stream.SafeFileHandle, LocationRow(old, next), checked(index * 76));
    private static IEnumerable<MetadataTreeRoot> ReadRoots(string path, string storeId)
    {
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        while (input.Position < input.Length)
        {
            int kind = input.ReadByte(); if (kind == 0) { yield return new(1, storeId, null); continue; }
            if (kind != 1) throw Invalid("The frozen metadata root spool is malformed.");
            byte[] bytes = new byte[60]; input.ReadExactly(bytes); yield return new(1, storeId, DecodeReference(bytes));
        }
    }
    private static long ParseContainer(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        if (name.Length != 22 || !long.TryParse(name.AsSpan(6), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long value) || value < 0)
            throw Invalid("Preserve an unknown metadata container.");
        return value;
    }
    private static string ExactChild(string directory, string name, string prefix, string suffix)
    {
        if (string.IsNullOrEmpty(name) || Path.GetFileName(name) != name || !name.StartsWith(prefix, StringComparison.Ordinal) || !name.EndsWith(suffix, StringComparison.Ordinal))
            throw Invalid("Preserve an unsupported metadata control path.");
        string path = Path.Combine(directory, name); WorkspaceStore.EnsurePlainPath(path); return path;
    }
    private static string HashFile(string path)
    { using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read); return Convert.ToHexStringLower(SHA256.HashData(input)); }
    private static string ReadStoreIdentity(string directory)
    {
        byte[] header = ReadExactFile(Path.Combine(directory, "identity.bin"), ContainerHeaderBytes);
        if (!header.AsSpan(0, 4).SequenceEqual("SPM1"u8) || !SHA256.HashData(header.AsSpan(0, 16)).AsSpan().SequenceEqual(header.AsSpan(16)))
            throw Invalid("Preserve a corrupt metadata store identity.");
        return Convert.ToHexStringLower(header.AsSpan(4, 12));
    }
    private static T ReadCollection<T>(string path)
    {
        WorkspaceStore.EnsurePlainPath(path);
        try
        {
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var document = JsonDocument.Parse(input);
            if (!JsonPropertyValidation.HasUniqueProperties(document.RootElement)) throw Invalid("Preserve duplicated metadata ownership/control fields.");
            var value = document.RootElement.Deserialize<T>(CollectionJson) ?? throw Invalid("A metadata ownership/control receipt is empty.");
            if (value is CollectionIntent intent && (intent.Version != 2 || intent.StateHash is not { Length: 64 } || intent.StateHash != CollectionStateHash(intent)))
                throw Invalid("Preserve an unsupported or checksum-invalid metadata collection control; use its matching binary without migration.");
            return value;
        }
        catch (JsonException) { throw Invalid("Preserve unsupported or corrupt metadata ownership/control receipts."); }
    }
    private static void WriteCollection<T>(string path, T value, bool replace = true)
    {
        if (value is CollectionIntent intent) value = (T)(object)(intent with { StateHash = CollectionStateHash(intent) });
        WorkspaceStore.EnsurePlainPath(path); string pending = path + "." + Guid.NewGuid().ToString("N") + ".pending";
        using (var output = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        { JsonSerializer.Serialize(output, value, CollectionJson); output.Flush(true); }
        if (replace) AtomicFilePublication.Publish(pending, path); else File.Move(pending, path);
    }
    private static string CollectionStateHash(CollectionIntent value) => Convert.ToHexStringLower(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(value with { StateHash = "" }, CollectionJson)));
}
