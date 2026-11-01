using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class JournalDeltaStore
{
    internal sealed record StoppedIndexContext(SnapshotDeltaSeal Seal, string Receipt, Action VerifyJournal);
    private sealed record StoppedIndexResidual(int Version, string Owner, string Request, SnapshotDeltaSeal Seal,
        string Receipt, string ReceiptIdentity, long ReceiptBytes, string ReceiptHash, string OriginalDirectory, string Directory, string NativeIdentity,
        string StoreId, string InventoryDirectory, MetadataTreeRoot? Files, MetadataTreeRoot? Controls,
        long Count, long Bytes, string Phase, string Digest = "");
    private sealed record StoppedControlProof(string Identity, long Bytes, string Hash);
    private const string StoppedResidualsName = "journal-stopped-index-residuals";
    private static readonly JsonSerializerOptions StoppedRootJson = new(JsonSerializerDefaults.Web)
    { PropertyNameCaseInsensitive = true, RespectRequiredConstructorParameters = true };
    private static string StoppedResidualRoot(string control, string directory, OverlayIdentity identity) => Path.Combine(control,
        StoppedResidualsName, WorkspaceStore.Key(PagedOwnerHash(identity) + "\n" + Path.GetFullPath(directory).ToUpperInvariant()));

    private static StoppedIndexResidual ReadStoppedResidual(string file, OverlayIdentity? expected = null)
    {
        var proof = JournalControlIntegrity.Read<StoppedIndexResidual>(file);
        if (proof.Version != 1 || proof.Digest != JournalControlIntegrity.Digest(proof with { Digest = "" }) ||
            expected is not null && proof.Owner != PagedOwnerHash(expected) || proof.Count < 0 || proof.Bytes < 0 || proof.ReceiptBytes < 1 ||
            proof.Phase is not ("freezing" or "prepared" or "completed") ||
            !WorkspaceStore.SamePath(proof.Directory, Path.Combine(Path.GetDirectoryName(file)!, "preserved-index")) ||
            !WorkspaceStore.SamePath(proof.InventoryDirectory, Path.Combine(Path.GetDirectoryName(file)!, "inventory"))) throw PagedInvalid();
        return proof;
    }
    private static StoppedIndexResidual? LatestStoppedResidual(string root, OverlayIdentity? expected = null)
    {
        StoppedIndexResidual? prior = null;
        foreach (string phase in new[] { "freezing", "prepared", "completed" })
        {
            string path = Path.Combine(root, phase + ".json"); if (!File.Exists(path)) continue;
            var value = ReadStoppedResidual(path, expected);
            if (value.Phase != phase || prior is null && phase != "freezing" || prior is not null &&
                (value.Owner != prior.Owner || value.Request != prior.Request || value.Seal != prior.Seal ||
                 value.Receipt != prior.Receipt || value.ReceiptIdentity != prior.ReceiptIdentity || value.ReceiptBytes != prior.ReceiptBytes ||
                 value.ReceiptHash != prior.ReceiptHash || value.NativeIdentity != prior.NativeIdentity ||
                 value.OriginalDirectory != prior.OriginalDirectory || value.Directory != prior.Directory || value.StoreId != prior.StoreId ||
                 prior.Files is not null && (value.Files != prior.Files || value.Controls != prior.Controls || value.Count != prior.Count || value.Bytes != prior.Bytes))) throw PagedInvalid();
            prior = value;
        }
        return prior;
    }
    private static void PublishStoppedResidual(string root, StoppedIndexResidual value)
    {
        value = value with { Digest = JournalControlIntegrity.Digest(value with { Digest = "" }) };
        string path = Path.Combine(root, value.Phase + ".json");
        if (File.Exists(path)) { if (ReadStoppedResidual(path) != value) throw PagedInvalid(); return; }
        string pending = path + "." + Guid.NewGuid().ToString("N") + ".pending";
        using (var output = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        { JsonSerializer.Serialize(output, value, JournalControlIntegrity.Json); output.Flush(true); }
        File.Move(pending, path);
    }
    private static IEnumerable<PreservedJournalIndex> EnumerateStoppedResiduals(string control, OverlayIdentity? expected)
    {
        string root = Path.Combine(control, StoppedResidualsName); if (!Directory.Exists(root)) yield break;
        foreach (string directory in Directory.EnumerateDirectories(root))
        {
            var value = LatestStoppedResidual(directory, expected); if (value?.Files is null) continue;
            yield return new(Directory.Exists(value.Directory) ? value.Directory : value.OriginalDirectory, value.OriginalDirectory,
                value.NativeIdentity, value.InventoryDirectory, value.Files, value.Count, value.Bytes, value.Request,
                value.Phase == "completed" ? "stopped-source-index-retained-intact" : "stopped-source-index-preservation-pending");
        }
    }
    private static IEnumerable<(string Path, string Request, string Reason)> EnumerateStoppedResidualAttempts(string control,
        OverlayIdentity? expected, bool unpublished)
    {
        string root = Path.Combine(control, StoppedResidualsName); if (!Directory.Exists(root)) yield break;
        foreach (string directory in Directory.EnumerateDirectories(root))
        {
            var value = LatestStoppedResidual(directory, expected);
            if (unpublished)
            {
                foreach (string file in Directory.EnumerateFiles(directory, "*.pending"))
                { WorkspaceStore.EnsurePlainPath(file); yield return (file, Path.GetFileName(directory), "unpublished-stopped-source-index-control"); }
            }
            else if (value is not null && value.Phase != "completed")
                yield return (Directory.Exists(value.Directory) ? value.Directory : value.OriginalDirectory, value.Request, "stopped-source-index-preservation-pending");
        }
    }
    private static bool HasUnownedStoppedPayload(string directory, string store, IJournalFilePins pins)
    {
        bool found = false;
        foreach (string file in Directory.EnumerateFiles(directory, "pages-*.bin"))
        {
            if (new FileInfo(file).Length <= 48) continue;
            try { PagedMetadataStore.ValidateCreatedContainer(file, store, pins); }
            catch (FileNotFoundException) when (!File.Exists(file + ".owner"))
            { PagedMetadataStore.VerifyUnownedStoppedContainer(file, store); found = true; }
        }
        return found;
    }
    private static void RecoverStoppedIndexResiduals(string control, string delta, OverlayIdentity identity,
        IJournalFilePins pins, StoppedIndexContext context, Action<string>? boundary)
    {
        string parent = Path.Combine(control, StoppedResidualsName); if (!Directory.Exists(parent)) return;
        foreach (string root in Directory.EnumerateDirectories(parent))
        {
            var proof = LatestStoppedResidual(root, identity);
            if (proof is null || proof.Phase == "completed" || !WorkspaceStore.SamePath(Path.GetDirectoryName(proof.OriginalDirectory)!, delta)) continue;
            string current = Directory.Exists(proof.Directory) ? proof.Directory : proof.OriginalDirectory;
            using var fence = new FileStream(Path.Combine(current, "writer.lock"), FileMode.Open, FileAccess.Read, FileShare.Delete);
            PreserveWholeStoppedIndex(proof.OriginalDirectory, identity, pins, fence, control, context, boundary);
        }
    }
    // Only the stopped segmented reclaimer supplies this context, after its
    // complete SJRC2 receipt and final seal have authorized metadata retirement.
    // All index bytes remain intact; this is neither ownership adoption nor a
    // broad relaxation of the normal header-gap/unknown-payload safeguards.
    private static void PreserveWholeStoppedIndex(string directory, OverlayIdentity identity, IJournalFilePins pins,
        FileStream fence, string control, StoppedIndexContext context, Action<string>? boundary)
    {
        string root = StoppedResidualRoot(control, directory, identity); WorkspaceStore.EnsurePlainPath(root); Directory.CreateDirectory(root);
        var saved = LatestStoppedResidual(root, identity);
        StoppedIndexResidual value;
        if (saved is null)
        {
            context.VerifyJournal(); ValidateIndexOwner(directory, identity);
            using var receipt = new FileStream(context.Receipt, FileMode.Open, FileAccess.Read, FileShare.Read);
            value = new(1, PagedOwnerHash(identity), Path.GetFileName(root), context.Seal, Path.GetFullPath(context.Receipt), pins.IdentifyOpened(receipt), receipt.Length, FileHash(receipt),
                Path.GetFullPath(directory), Path.Combine(root, "preserved-index"), pins.IdentifyDirectory(directory), PagedMetadataStore.ReadIdentity(directory),
                Path.Combine(root, "inventory"), null, null, 0, 0, "freezing");
            PublishStoppedResidual(root, value); boundary?.Invoke("stopped-index-preserve.intent");
        }
        else value = saved;
        using (var receipt = new FileStream(context.Receipt, FileMode.Open, FileAccess.Read, FileShare.Read))
            if (value.Seal != context.Seal || !WorkspaceStore.SamePath(value.Receipt, context.Receipt) || value.ReceiptIdentity != pins.IdentifyOpened(receipt) ||
                value.ReceiptBytes != receipt.Length || value.ReceiptHash != FileHash(receipt) || !WorkspaceStore.SamePath(value.OriginalDirectory, directory)) throw PagedInvalid();
        string current = Directory.Exists(value.Directory) ? value.Directory : directory;
        if (pins.IdentifyDirectory(current) != value.NativeIdentity) throw PagedInvalid();
        if (value.Phase == "freezing")
        {
            context.VerifyJournal();
            if (!WorkspaceStore.SamePath(current, directory) || !HasUnownedStoppedPayload(directory, value.StoreId, pins)) throw PagedInvalid();
            // This narrow bug recovery is neither metadata migration nor
            // arbitrary orphan adoption. Different relocation/pending domains
            // require their own authoritative proof and remain untouched.
            if (File.Exists(Path.Combine(directory, "locations.json")) || File.Exists(Path.Combine(directory, "collection.json")) ||
                File.Exists(Path.Combine(Path.GetDirectoryName(directory)!, LiveRepackName)) ||
                File.Exists(Path.Combine(Path.GetDirectoryName(directory)!, "segment-roll.json")) ||
                File.Exists(Path.Combine(Path.GetDirectoryName(directory)!, SegmentCollectionName)))
                throw new WorkspaceException("StoppedIndexPreservationUnsupported", "Preserve this different pending or relocated metadata domain; the narrow stopped-replay recovery cannot authorize its move.");
            using var inventory = new PagedMetadataStore(value.InventoryDirectory, true, filePins: pins, initialStoreId: value.Request[..24]);
            var controls = inventory.Build(MetadataExternalSort.Order(StoppedControlEntries(control, directory, identity, value.StoreId, pins), root, default, pins));
            AuditStoppedFrames(Path.GetDirectoryName(directory)!, directory, value.StoreId, identity, pins, inventory);
            long count = 0, bytes = 0;
            IEnumerable<MetadataEntry> Entries()
            {
                foreach (string child in Directory.EnumerateDirectories(directory))
                {
                    if (Path.GetFileName(child) != "collections") throw PagedInvalid();
                    count++; yield return new("d/collections", PackBytes(new ResidualEntry(true, true, 0, 0, pins.IdentifyDirectory(child), null)));
                }
                foreach (string file in OrderedIndexFiles(directory))
                {
                    string relative = Path.GetRelativePath(directory, file).Replace('\\', '/');
                    if (relative == "writer.lock") continue;
                    var entry = ObserveStoppedEntry(file, pins); count++; bytes = checked(bytes + entry.Allocated);
                    yield return new("f/" + relative, PackBytes(entry));
                }
                count++; yield return new("f/writer.lock", PackBytes(new ResidualEntry(false, true, 0, 0, pins.IdentifyOpened(fence), FileHash(fence))));
            }
            var files = inventory.Build(MetadataExternalSort.Order(Entries(), root, default, pins)); inventory.Flush();
            value = value with { Files = files, Controls = controls, Count = count, Bytes = bytes, Phase = "prepared" };
            PublishStoppedResidual(root, value); boundary?.Invoke("stopped-index-preserve.inventory");
        }
        using var records = new PagedMetadataStore(value.InventoryDirectory, filePins: pins);
        ValidateWholeStoppedIndex(current, value, records, pins, fence);
        IDisposable? moved = null; FileStream? destinationFence = null;
        try
        {
            if (WorkspaceStore.SamePath(current, directory))
            {
                context.VerifyJournal(); ValidateStoppedControls(value, records, pins);
                moved = pins.MoveDirectory(current, value.Directory, value.NativeIdentity, () =>
                { fence.Dispose(); boundary?.Invoke("stopped-index-preserve.descendants-released"); });
                current = value.Directory; boundary?.Invoke("stopped-index-preserve.moved");
                destinationFence = new(Path.Combine(current, "writer.lock"), FileMode.Open, FileAccess.Read, FileShare.Delete); fence = destinationFence;
            }
            else moved = pins.PinDirectory(current, value.NativeIdentity);
            ValidateWholeStoppedIndex(current, value, records, pins, fence);
            if (value.Phase != "completed")
            {
                context.VerifyJournal(); ValidateStoppedControls(value, records, pins);
                value = value with { Phase = "completed" }; PublishStoppedResidual(root, value); boundary?.Invoke("stopped-index-preserve.complete");
            }
        }
        finally { destinationFence?.Dispose(); moved?.Dispose(); }
    }
    private static string FileHash(string path)
    { using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read); return FileHash(input); }
    private static string FileHash(FileStream input)
    { input.Position = 0; string hash = Convert.ToHexStringLower(SHA256.HashData(input)); input.Position = 0; return hash; }
    private static ResidualEntry ObserveStoppedEntry(string path, IJournalFilePins pins)
    {
        WorkspaceStore.EnsurePlainPath(path);
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return new(false, true, input.Length, pins.AllocatedBytes(input), pins.IdentifyOpened(input), FileHash(input));
    }
    private static void ValidateWholeStoppedIndex(string directory, StoppedIndexResidual value, PagedMetadataStore records, IJournalFilePins pins, FileStream fence)
    {
        if (pins.IdentifyDirectory(directory) != value.NativeIdentity || PagedMetadataStore.ReadIdentity(directory) != value.StoreId) throw PagedInvalid();
        long count = 0;
        foreach (string child in Directory.EnumerateDirectories(directory))
        {
            var expectedDirectory = PackRead<ResidualEntry>(records.Open(value.Files!).Find("d/" + Path.GetFileName(child)) ?? throw PagedInvalid());
            if (!expectedDirectory.Directory || expectedDirectory.Identity != pins.IdentifyDirectory(child)) throw PagedInvalid(); count++;
        }
        foreach (string file in OrderedIndexFiles(directory))
        {
            string relative = Path.GetRelativePath(directory, file).Replace('\\', '/');
            if (relative == "writer.lock") continue;
            var entry = records.Open(value.Files!).Find("f/" + relative) ?? throw PagedInvalid();
            if (PackRead<ResidualEntry>(entry) != ObserveStoppedEntry(file, pins)) throw PagedInvalid(); count++;
        }
        var expected = PackRead<ResidualEntry>(records.Open(value.Files!).Find("f/writer.lock") ?? throw PagedInvalid());
        if (fence.Length != 0 || pins.IdentifyOpened(fence) != expected.Identity || expected.Hash != FileHash(fence) || count + 1 != value.Count) throw PagedInvalid();
    }
    private static IEnumerable<string> StoppedControlFiles(string control, string excludedIndex)
    {
        var cursors = new Stack<IEnumerator<string>>(); cursors.Push(Directory.EnumerateFileSystemEntries(control).GetEnumerator());
        try
        {
            while (cursors.Count != 0)
            {
                var cursor = cursors.Peek();
                if (!cursor.MoveNext()) { cursor.Dispose(); cursors.Pop(); continue; }
                string path = cursor.Current; WorkspaceStore.EnsurePlainPath(path);
                string relative = Path.GetRelativePath(control, path).Replace('\\', '/');
                if (Directory.Exists(path))
                {
                    if (WorkspaceStore.SamePath(path, excludedIndex) || relative is StoppedResidualsName or "read-only-replays" or "journal-index-residuals") continue;
                    if (!StoppedControlNamespace(relative)) throw new WorkspaceException("StoppedIndexPreservationUnsupported", "Preserve an unknown stopped-control namespace without reading its payload as metadata.");
                    // Admit each plain directory before enumeration; recursive
                    // EnumerateFiles would follow a junction before checking it.
                    cursors.Push(Directory.EnumerateFileSystemEntries(path).GetEnumerator());
                }
                else if (File.Exists(path) && Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase)) yield return path;
            }
        }
        finally { while (cursors.TryPop(out var cursor)) cursor.Dispose(); }
    }
    private static bool StoppedControlNamespace(string relative)
    {
        string[] names = relative.Split('/');
        if (names[0] == "delta") return names.Length == 1 || names.Length == 2 && IndexName(names[1]) ||
            names.Length == 3 && IndexName(names[1]) && names[2] == "collections";
        if (names[0] is "overlay" or "dependents") return names.Length == 1;
        if (names[0] is "metadata" or "source-metadata-pages") return names.Length == 1 || names.Length == 2 && names[1] == "collections";
        if (names[0] == "captures") return names.Length == 1 || CaptureId(names[1]) &&
            (names.Length == 2 || names.Length == 3 && names[2] == "preserved-ranges" || names.Length == 4 && names[2] == "preserved-ranges" && names[3] == "collections");
        if (names[0] == "journal-packing") return names.Length == 1 || Guid.TryParseExact(names[1], "N", out _) &&
            (names.Length == 2 || names.Length == 3 && IndexName(names[2]) || names.Length == 4 && IndexName(names[2]) && names[3] == "collections");
        if (names[0].StartsWith("restore-index-", StringComparison.Ordinal) && Guid.TryParseExact(names[0].AsSpan(14), "N", out _))
            return names.Length == 1 || names[1] is "records" or "paths" or "cleanup" &&
                (names.Length == 2 || names.Length == 3 && names[2] == "collections");
        return false;
    }
    private static IEnumerable<MetadataEntry> StoppedControlEntries(string control, string index, OverlayIdentity identity, string storeId, IJournalFilePins pins)
    {
        foreach (string file in StoppedControlFiles(control, index))
        {
            using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            // Discover all occurrences rather than interpreting an arbitrary
            // response DTO. Critical state/frame controls are also validated
            // by their typed checksum contracts; duplicate root/page members
            // are rejected by this bounded streaming scanner itself.
            PagedMetadataStore.VerifyStoppedGraph(index, pins, StreamStoppedJsonRoots(input, storeId));
            if (Path.GetFileName(file) == "capture.json")
            {
                var capture = SourceCaptureTree.ReadCapture(Path.GetDirectoryName(file)!, verifyPagedRoots: false);
                if (capture.Paged?.Directory == index)
                {
                    if (!SourceCaptureTree.SameIdentity(capture.Identity, identity)) throw PagedInvalid();
                    SourceCaptureTree.RequireMaterialized(Path.GetDirectoryName(file)!, capture);
                    if (Directory.EnumerateFiles(Path.GetDirectoryName(file)!, "*.pin").Any())
                        throw new WorkspaceException("SourceCapturePending", "Complete the frozen result and its exact backing aliases before moving the source index.");
                }
            }
            yield return new(Path.GetRelativePath(control, file).Replace('\\', '/'), PackBytes(new StoppedControlProof(pins.IdentifyOpened(input), input.Length, FileHash(input))));
        }
    }
    private static void ValidateStoppedControls(StoppedIndexResidual value, PagedMetadataStore records, IJournalFilePins pins)
    {
        string control = Path.GetDirectoryName(Path.GetDirectoryName(value.OriginalDirectory)!)!; long count = 0;
        foreach (string file in StoppedControlFiles(control, value.OriginalDirectory))
        {
            string relative = Path.GetRelativePath(control, file).Replace('\\', '/');
            var expected = PackRead<StoppedControlProof>(records.Open(value.Controls!).Find(relative) ?? throw PagedInvalid());
            using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (expected.Identity != pins.IdentifyOpened(input) || expected.Bytes != input.Length || expected.Hash != FileHash(input)) throw PagedInvalid(); count++;
        }
        if (count != value.Controls!.Count) throw PagedInvalid();
    }
    private static IEnumerable<MetadataTreeRoot> JsonRoots(JsonElement element, string storeId)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            JsonElement id = default; bool hasId = false, hasPage = false;
            foreach (var property in element.EnumerateObject())
            { if (property.Name.Equals("storeId", StringComparison.OrdinalIgnoreCase)) { id = property.Value; hasId = true; }
              if (property.Name.Equals("page", StringComparison.OrdinalIgnoreCase)) hasPage = true; }
            if (hasId && hasPage && id.ValueKind == JsonValueKind.String && id.GetString() == storeId)
            { yield return element.Deserialize<MetadataTreeRoot>(StoppedRootJson) ?? throw PagedInvalid(); yield break; }
            foreach (var property in element.EnumerateObject()) foreach (var root in JsonRoots(property.Value, storeId)) yield return root;
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) foreach (var root in JsonRoots(item, storeId)) yield return root;
    }
    private static void AuditStoppedFrames(string delta, string index, string storeId, OverlayIdentity identity, IJournalFilePins pins, PagedMetadataStore inventory)
    {
        var state = JournalControlIntegrity.Read<JournalSegmentState>(Path.Combine(delta, "segment-state.json"));
        if (state.Version != 2 || state.OwnerHash != PagedOwnerHash(identity) || state.Digest != JournalControlIntegrity.Digest(state with { Digest = "" }) ||
            !WorkspaceStore.SamePath(state.Roots.Directory, index)) throw PagedInvalid();
        var pendingCaptures = inventory.Empty; bool pendingPacking;
        using (var source = PagedMetadataStore.OpenStoppedAudit(index, pins))
        {
            if (state.Roots.Captures is { } captures)
                foreach (var entry in source.Open(captures).Enumerate()) pendingCaptures = inventory.Set(pendingCaptures, entry.Key, []);
            pendingPacking = state.Roots.Packing is { } packing && source.Open(packing).FindEntry("request") is not null;
        }
        IEnumerable<string> Files()
        { foreach (string file in Directory.EnumerateFiles(delta, "segment-*.sjr")) yield return file; yield return Path.Combine(delta, "delta.journal"); }
        foreach (string file in Files())
        {
            using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            byte[] segmentHeader = new byte[JournalSegmentStorage.HeaderBytes]; input.ReadExactly(segmentHeader);
            if (!segmentHeader.AsSpan(0, 4).SequenceEqual("SJS1"u8) ||
                !segmentHeader.AsSpan(12, 32).SequenceEqual(Convert.FromHexString(PagedOwnerHash(identity))) ||
                !SHA256.HashData(segmentHeader.AsSpan(0, 60)).AsSpan().SequenceEqual(segmentHeader.AsSpan(60))) throw PagedInvalid();
            bool active = Path.GetFileName(file) == "delta.journal";
            byte[] header = new byte[FrameHeaderBytes], buffer = new byte[65536], checksum = new byte[FrameChecksumBytes];
            while (input.Position < input.Length)
            {
                if (input.Length - input.Position < FrameHeaderBytes) throw PagedInvalid(); input.ReadExactly(header);
                if (!header.AsSpan(0, 4).SequenceEqual("SJR2"u8)) throw PagedInvalid();
                int bytes = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4));
                var payload = new WorkspaceJournalPayload(BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(8)), BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(12)));
                payload.RequireValid(identity.MaxTransferBytes);
                if (bytes < 1 || bytes > 3 * 6 * 32000 + MaxMetadataBytes || input.Length - input.Position < bytes + (long)payload.StoredBytes + FrameChecksumBytes) throw PagedInvalid();
                byte[] metadata = new byte[bytes]; input.ReadExactly(metadata); _ = ReadCurrentMutation(metadata);
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); hash.AppendData(header); hash.AppendData(metadata);
                int remaining = payload.StoredBytes;
                while (remaining > 0) { int count = Math.Min(buffer.Length, remaining); input.ReadExactly(buffer.AsSpan(0, count)); hash.AppendData(buffer.AsSpan(0, count)); remaining -= count; }
                input.ReadExactly(checksum); if (!CryptographicOperations.FixedTimeEquals(checksum, hash.GetHashAndReset())) throw PagedInvalid();
                using var document = JsonDocument.Parse(metadata); PagedMetadataStore.VerifyStoppedGraph(index, pins, JsonRoots(document.RootElement, storeId));
                var mutation = ReadCurrentMutation(metadata);
                if (active)
                {
                    if (mutation.Operation == "capture-freeze") pendingCaptures = inventory.Set(pendingCaptures, mutation.FrozenCapture!.Id, []);
                    else if (mutation.Operation == "capture-complete") pendingCaptures = inventory.Remove(pendingCaptures, mutation.CaptureId!);
                    else if (mutation.Operation == "packing-state") pendingPacking = true;
                    else if (mutation.Operation == "packing-complete") pendingPacking = false;
                }
                if (mutation.FrozenCapture is { } frozen && WorkspaceStore.SamePath(frozen.Directory, index))
                {
                    using var store = PagedMetadataStore.OpenStoppedAudit(index, pins);
                    var capture = LoadFrozenCapture(store, new(frozen.Id, Encoding.ASCII.GetBytes(frozen.Digest), frozen.Control));
                    using var captureJson = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(capture, JournalControlIntegrity.Json));
                    PagedMetadataStore.VerifyStoppedGraph(index, pins, JsonRoots(captureJson.RootElement, storeId));
                }
            }
        }
        if (pendingCaptures.Count != 0) throw new WorkspaceException("SourceCapturePending", "Complete the exact accepted frozen capture before moving its source index.");
        if (pendingPacking) throw new WorkspaceException("SourceMaintenancePending", "Resume the exact packing request before moving its source index.");
    }
}
