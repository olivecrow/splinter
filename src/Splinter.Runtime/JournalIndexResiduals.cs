using System.Security.Cryptography;
using System.Text.Json;
using Splinter.Core;

namespace Splinter.Runtime;

public sealed record PreservedJournalIndex(string Directory, string OriginalDirectory, string NativeIdentity,
    string InventoryDirectory, MetadataTreeRoot Entries, long Count, long Bytes, string Request, string Reason);

public sealed partial class JournalDeltaStore
{
    private sealed record IndexResidual(int Version, string Owner, string Request, string OriginalDirectory, string Directory,
        string NativeIdentity, string StoreId, string InventoryDirectory, MetadataTreeRoot? Files, MetadataTreeRoot? Opaque,
        long Count, long Bytes, string Phase, string Digest = "");
    private sealed record ResidualEntry(bool Directory, bool Opaque, long Bytes, long Allocated, string Identity, string? Hash);
    private static bool OpaqueIndexHeader(string path, string storeId, IJournalFilePins pins)
    {
        string name = Path.GetFileName(path), file = path;
        if (name.EndsWith(".owner", StringComparison.Ordinal)) file = path[..^6];
        else if (name.EndsWith(".pending", StringComparison.Ordinal))
        {
            int marker = name.IndexOf(".bin.owner.", StringComparison.Ordinal);
            if (marker < 0 || name.Length - marker != 51 || !Guid.TryParseExact(name.AsSpan(marker + 11, 32), "N", out _)) return false;
            file = Path.Combine(Path.GetDirectoryName(path)!, name[..(marker + 4)]);
        }
        string payload = Path.GetFileName(file);
        if (payload.Length != 26 || !payload.StartsWith("pages-", StringComparison.Ordinal) || !payload.EndsWith(".bin", StringComparison.Ordinal) ||
            !long.TryParse(payload.AsSpan(6, 16), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out long number) || number < 0 ||
            !File.Exists(file) || new FileInfo(file).Length > 48) return false;
        if (name.EndsWith(".pending", StringComparison.Ordinal)) return true;
        try { PagedMetadataStore.ValidateCreatedContainer(file, storeId, pins, headerOnly: true); return false; }
        catch (WorkspaceException) { return true; }
        catch (FileNotFoundException) { return true; }
    }
    private static bool HasOpaqueIndexHeader(string directory, string storeId, IJournalFilePins pins)
    {
        foreach (string path in Directory.EnumerateFiles(directory, "pages-*"))
            if (OpaqueIndexHeader(path, storeId, pins)) return true;
        return false;
    }
    private static string ResidualControl(string directory, OverlayIdentity identity, string control)
    {
        string normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        return Path.Combine(control, "journal-index-residuals", WorkspaceStore.Key(PagedOwnerHash(identity) + "\n" + (OperatingSystem.IsWindows() ? normalized.ToUpperInvariant() : normalized)));
    }
    private static IndexResidual ReadIndexResidual(string path, OverlayIdentity? expected = null)
    {
        var value = JournalControlIntegrity.Read<IndexResidual>(path);
        if (value.Version != 1 || value.Digest != JournalControlIntegrity.Digest(value with { Digest = "" }) ||
            expected is not null && value.Owner != PagedOwnerHash(expected) || value.Count < 0 || value.Bytes < 0 ||
            value.Phase is not ("freezing" or "prepared" or "pruning" or "completed") ||
            !WorkspaceStore.SamePath(value.Directory, Path.Combine(Path.GetDirectoryName(path)!, "preserved-index")) ||
            !WorkspaceStore.SamePath(value.InventoryDirectory, Path.Combine(Path.GetDirectoryName(path)!, "inventory"))) throw PagedInvalid();
        return value;
    }
    private static void PublishIndexResidual(string path, IndexResidual value)
    {
        value = value with { Digest = JournalControlIntegrity.Digest(value with { Digest = "" }) };
        string root = Path.GetDirectoryName(path)!, target = Path.Combine(root, "phase-" + value.Phase + ".json");
        if (File.Exists(target)) { if (ReadIndexResidual(target) != value) throw PagedInvalid(); return; }
        // An unpublished/torn control never authorizes a move or deletion.
        // Leave its bytes at their original nonce name; a same-request retry
        // can publish a fresh attempt without adopting that unknown prefix.
        string pending = Path.Combine(root, "phase-" + value.Phase + "." + Guid.NewGuid().ToString("N") + ".pending");
        WorkspaceStore.EnsurePlainPath(target); WorkspaceStore.EnsurePlainPath(pending);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value, JournalControlIntegrity.Json);
        using (var output = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.Read)) { output.Write(bytes); output.Flush(true); }
        File.Move(pending, target);
    }
    private static IndexResidual? LatestIndexResidual(string root, OverlayIdentity? expected = null)
    {
        IndexResidual? latest = null;
        foreach (string phase in new[] { "freezing", "prepared", "pruning", "completed" })
        {
            string path = Path.Combine(root, "phase-" + phase + ".json"); if (!File.Exists(path)) continue;
            var value = ReadIndexResidual(path, expected);
            if (value.Phase != phase || latest is null && phase != "freezing" || latest is not null &&
                (value.Owner != latest.Owner || value.Request != latest.Request || value.OriginalDirectory != latest.OriginalDirectory ||
                 value.Directory != latest.Directory || value.NativeIdentity != latest.NativeIdentity || value.StoreId != latest.StoreId ||
                 latest.Files is not null && (value.Files != latest.Files || value.Opaque != latest.Opaque || value.Count != latest.Count || value.Bytes != latest.Bytes))) throw PagedInvalid();
            latest = value;
        }
        return latest;
    }
    public static IEnumerable<PreservedJournalIndex> EnumeratePreservedJournalIndices(string control, OverlayIdentity? expected = null)
    {
        foreach (var value in EnumerateStoppedResiduals(control, expected)) yield return value;
        string root = Path.Combine(control, "journal-index-residuals"); if (!Directory.Exists(root)) yield break;
        foreach (string directory in Directory.EnumerateDirectories(root))
        {
            var value = LatestIndexResidual(directory, expected); if (value?.Opaque is null) continue;
            yield return new(Directory.Exists(value.Directory) ? value.Directory : value.OriginalDirectory, value.OriginalDirectory, value.NativeIdentity,
                value.InventoryDirectory, value.Opaque, value.Count, value.Bytes, value.Request,
                value.Phase == "completed" ? "unowned-source-index-header-prefix" : "source-index-preservation-pending");
        }
    }
    public static IEnumerable<(string Path, string Request, string Reason)> EnumerateUnpublishedIndexResidualControls(string control, OverlayIdentity? expected = null)
    {
        foreach (var value in EnumerateStoppedResidualAttempts(control, expected, unpublished: true)) yield return value;
        string root = Path.Combine(control, "journal-index-residuals"); if (!Directory.Exists(root)) yield break;
        foreach (string directory in Directory.EnumerateDirectories(root))
        {
            _ = LatestIndexResidual(directory, expected);
            foreach (string file in Directory.EnumerateFiles(directory, "phase-*.pending"))
            { WorkspaceStore.EnsurePlainPath(file); yield return (file, Path.GetFileName(directory), "unpublished-source-index-preservation-control"); }
        }
    }
    public static IEnumerable<(string Path, string Request, string Reason)> EnumeratePendingJournalIndexPreservations(string control, OverlayIdentity? expected = null)
    {
        foreach (var value in EnumerateStoppedResidualAttempts(control, expected, unpublished: false)) yield return value;
        string root = Path.Combine(control, "journal-index-residuals"); if (!Directory.Exists(root)) yield break;
        foreach (string directory in Directory.EnumerateDirectories(root))
        {
            var value = LatestIndexResidual(directory, expected); if (value is null || value.Phase == "completed") continue;
            yield return (Directory.Exists(value.Directory) ? value.Directory : value.OriginalDirectory, value.Request, "source-index-preservation-pending");
        }
    }
    public static IEnumerable<(string Path, string Request, string Reason)> EnumerateUnverifiedJournalIndexFiles(string control, OverlayIdentity expected, IJournalFilePins pins)
    {
        string delta = Path.Combine(control, "delta"); if (!Directory.Exists(delta)) yield break;
        foreach (string directory in Directory.EnumerateDirectories(delta, "index-*"))
        {
            ValidateIndexOwner(directory, expected); string store = PagedMetadataStore.ReadIdentity(directory);
            foreach (string file in Directory.EnumerateFiles(directory, "pages-*.bin"))
            {
                bool invalid = false;
                try { PagedMetadataStore.ValidateCreatedContainer(file, store, pins); }
                catch (WorkspaceException) { invalid = true; }
                catch (FileNotFoundException) { invalid = true; }
                if (invalid) yield return (file, Path.GetFileName(directory), new FileInfo(file).Length <= 48
                    ? "unowned-source-index-header-prefix" : "unverified-source-index-payload");
            }
        }
    }
    private static void RecoverIndexResiduals(string control, string parent, OverlayIdentity identity, IJournalFilePins? pins, Action<string>? boundary = null)
    {
        if (pins is null) return;
        string root = Path.Combine(control, "journal-index-residuals"); if (!Directory.Exists(root)) return;
        foreach (string directory in Directory.EnumerateDirectories(root))
        {
            var value = LatestIndexResidual(directory, identity); if (value is null) continue;
            if (value.Phase == "completed" || !WorkspaceStore.SamePath(Path.GetDirectoryName(value.OriginalDirectory)!, parent)) continue;
            string path = Directory.Exists(value.OriginalDirectory) ? value.OriginalDirectory : value.Directory;
            using var fence = new FileStream(Path.Combine(path, "writer.lock"), FileMode.Open, FileAccess.Read, FileShare.Delete);
            PreserveOpaqueIndex(value.OriginalDirectory, identity, pins, fence, control, boundary);
        }
    }
    private static void PreserveOpaqueIndex(string directory, OverlayIdentity identity, IJournalFilePins pins, FileStream fence, string control, Action<string>? boundary = null)
    {
        string root = ResidualControl(directory, identity, control), receipt = Path.Combine(root, "receipt.json");
        WorkspaceStore.EnsurePlainPath(root); Directory.CreateDirectory(root);
        IndexResidual value;
        if (LatestIndexResidual(root, identity) is { } saved) value = saved;
        else
        {
            ValidateIndexOwner(directory, identity); string store = PagedMetadataStore.ReadIdentity(directory);
            value = new(1, PagedOwnerHash(identity), Path.GetFileName(root), Path.GetFullPath(directory), Path.Combine(root, "preserved-index"),
                pins.IdentifyDirectory(directory), store, Path.Combine(root, "inventory"), null, null, 0, 0, "freezing");
            PublishIndexResidual(receipt, value);
            boundary?.Invoke("index-preserve.intent");
        }
        if (value.Phase == "completed") return;
        if (!WorkspaceStore.SamePath(value.OriginalDirectory, directory) || value.Request != Path.GetFileName(root)) throw PagedInvalid();
        var held = new Dictionary<string, FileStream>(StringComparer.Ordinal);
        IDisposable? namespaceFence = null;
        FileStream? movedFence = null;
        try
        {
            if (value.Phase == "freezing")
            {
                if (pins.IdentifyDirectory(directory) != value.NativeIdentity || PagedMetadataStore.ReadIdentity(directory) != value.StoreId) throw PagedInvalid();
                PagedMetadataStore.ValidatePreservationInventory(directory, pins, path => OpaqueIndexHeader(path, value.StoreId, pins),
                    new HashSet<string>(StringComparer.Ordinal) { "owner.json" }, ActiveIndexRoots(directory, identity));
                WorkspaceStore.EnsurePlainPath(value.InventoryDirectory);
                using var inventory = new PagedMetadataStore(value.InventoryDirectory, true, filePins: pins, initialStoreId: value.Request[..24]);
                long count = 0, bytes = 0; var opaque = inventory.Empty;
                IEnumerable<MetadataEntry> Observed()
                {
                    foreach (var item in ResidualNames(directory, value.StoreId, pins))
                    {
                        string relative = item.Key[2..], path = Path.Combine(directory, relative.Replace('/', Path.DirectorySeparatorChar));
                        ResidualEntry entry;
                        if (item.Key.StartsWith("d/", StringComparison.Ordinal)) entry = new(true, false, 0, 0, pins.IdentifyDirectory(path), null);
                        else
                        {
                            FileStream input;
                            if (relative == "writer.lock") input = fence;
                            else { input = new(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 1); held.Add(relative, input); }
                            string native = pins.IdentifyOpened(input); bool unknown = OpaqueIndexHeader(path, value.StoreId, pins);
                            entry = new(false, unknown, input.Length, pins.AllocatedBytes(input), native, Convert.ToHexStringLower(SHA256.HashData(input)));
                            input.Position = 0;
                            if (unknown) { count++; bytes = checked(bytes + entry.Allocated); }
                        }
                        yield return new(item.Key, JsonSerializer.SerializeToUtf8Bytes(entry, JournalControlIntegrity.Json));
                    }
                }
                var files = inventory.Build(MetadataExternalSort.Order(Observed(), root, default, pins));
                foreach (var item in inventory.Open(files).Enumerate())
                    if (PackRead<ResidualEntry>(item.Value.Span).Opaque) opaque = inventory.Set(opaque, item.Key, item.Value.Span);
                if (count == 0) throw PagedInvalid(); inventory.Flush();
                value = value with { Files = files, Opaque = opaque, Count = count, Bytes = bytes, Phase = "prepared" }; PublishIndexResidual(receipt, value);
                boundary?.Invoke("index-preserve.inventory");
            }
            string current = Directory.Exists(value.Directory) ? value.Directory : value.OriginalDirectory;
            if (pins.IdentifyDirectory(current) != value.NativeIdentity) throw PagedInvalid();
            using var records = new PagedMetadataStore(value.InventoryDirectory);
            ValidateResidualNamespace(current, value, records);
            foreach (var item in records.Open(value.Files ?? throw PagedInvalid()).Enumerate())
            {
                var entry = PackRead<ResidualEntry>(item.Value.Span); string relative = item.Key[2..]; string path = Path.Combine(current, relative.Replace('/', Path.DirectorySeparatorChar));
                if (entry.Directory)
                { if (Directory.Exists(path) && pins.IdentifyDirectory(path) != entry.Identity) throw PagedInvalid(); continue; }
                if (!File.Exists(path)) { if (entry.Opaque || value.Phase is "prepared") throw PagedInvalid(); continue; }
                FileStream input;
                if (relative == "writer.lock") input = fence;
                else if (!held.TryGetValue(relative, out input!)) { input = new(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 1); held.Add(relative, input); }
                pins.Verify(input, entry.Identity); input.Position = 0;
                if (input.Length != entry.Bytes || Convert.ToHexStringLower(SHA256.HashData(input)) != entry.Hash) throw PagedInvalid(); input.Position = 0;
            }
            if (WorkspaceStore.SamePath(current, value.OriginalDirectory))
            {
                namespaceFence = pins.MoveDirectory(current, value.Directory, value.NativeIdentity, () =>
                {
                    foreach (var input in held.Values) input.Dispose(); held.Clear(); fence.Dispose();
                    boundary?.Invoke("index-preserve.descendants-released");
                });
                current = value.Directory;
                // A reader that wins the short descendant-fence gap blocks
                // the NTFS rename, or this exclusive destination admission.
                // Either outcome precedes all known-file deletion.
                movedFence = new(Path.Combine(current, "writer.lock"), FileMode.Open, FileAccess.Read, FileShare.Delete); fence = movedFence;
                boundary?.Invoke("index-preserve.moved");
            }
            else namespaceFence = pins.PinDirectory(current, value.NativeIdentity);
            if (pins.IdentifyDirectory(current) != value.NativeIdentity) throw PagedInvalid();
            // After the one atomic directory move, deny further child-name
            // replacement as well as data writes. Validate all current names
            // and native identities before deleting the first known object.
            foreach (var input in held.Values) input.Dispose(); held.Clear();
            ValidateResidualNamespace(current, value, records);
            foreach (var item in records.Open(value.Files!).Enumerate())
            {
                var entry = PackRead<ResidualEntry>(item.Value.Span); string relative = item.Key[2..], path = Path.Combine(current, relative.Replace('/', Path.DirectorySeparatorChar));
                if (entry.Directory) { if (Directory.Exists(path) && pins.IdentifyDirectory(path) != entry.Identity) throw PagedInvalid(); continue; }
                if (!File.Exists(path)) { if (entry.Opaque || value.Phase == "prepared") throw PagedInvalid(); continue; }
                FileStream input;
                if (relative == "writer.lock") input = fence;
                else { input = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1); held.Add(relative, input); }
                pins.Verify(input, entry.Identity); input.Position = 0;
                pins.VerifyPath(input, path, entry.Identity);
                if (input.Length != entry.Bytes || Convert.ToHexStringLower(SHA256.HashData(input)) != entry.Hash) throw PagedInvalid();
            }
            if (value.Phase == "prepared") { value = value with { Phase = "pruning" }; PublishIndexResidual(receipt, value); }
            foreach (var item in records.Open(value.Files!).Enumerate())
            {
                var entry = PackRead<ResidualEntry>(item.Value.Span); if (entry.Directory || entry.Opaque) continue;
                string relative = item.Key[2..]; if (relative == "writer.lock") continue;
                string path = Path.Combine(current, relative.Replace('/', Path.DirectorySeparatorChar));
                if (held.Remove(relative, out var input)) input.Dispose();
                if (File.Exists(path)) { pins.Unpin(path, entry.Identity); boundary?.Invoke("index-preserve.file"); }
            }
            foreach (var item in records.Open(value.Files!).Enumerate())
            {
                var entry = PackRead<ResidualEntry>(item.Value.Span); if (!entry.Directory) continue;
                string path = Path.Combine(current, item.Key[2..].Replace('/', Path.DirectorySeparatorChar));
                if (Directory.Exists(path)) { if (Directory.EnumerateFileSystemEntries(path).Any()) throw PagedInvalid(); pins.UnpinDirectory(path, entry.Identity); }
            }
            // Keep this zero-byte ownership fence as part of the residual
            // control. Publishing completion cannot create a missing-lock
            // recovery gap, and no payload allocation is retained by it.
            ValidateResidualNamespace(current, value, records);
            boundary?.Invoke("index-preserve.pruned"); ValidateResidualNamespace(current, value, records);
            value = value with { Phase = "completed" }; PublishIndexResidual(receipt, value); boundary?.Invoke("index-preserve.complete");
        }
        finally { foreach (var input in held.Values) input.Dispose(); movedFence?.Dispose(); namespaceFence?.Dispose(); }
    }
    private static IEnumerable<MetadataEntry> ResidualNames(string directory, string store, IJournalFilePins pins)
    {
        foreach (string path in Directory.EnumerateFileSystemEntries(directory))
        {
            WorkspaceStore.EnsurePlainPath(path); string name = Path.GetFileName(path);
            if (Directory.Exists(path))
            {
                if (name != "collections") throw PagedInvalid(); yield return new("d/" + name, ReadOnlyMemory<byte>.Empty);
                foreach (string file in Directory.EnumerateFiles(path))
                { string relative = name + "/" + Path.GetFileName(file); if (!IndexFile(relative)) throw PagedInvalid(); yield return new("f/" + relative, ReadOnlyMemory<byte>.Empty); }
            }
            else
            {
                if (!IndexFile(name) && !OpaqueIndexHeader(path, store, pins)) throw PagedInvalid();
                yield return new("f/" + name, ReadOnlyMemory<byte>.Empty);
            }
        }
    }
    private static IEnumerable<MetadataTreeRoot> ActiveIndexRoots(string directory, OverlayIdentity identity)
    {
        string statePath = Path.Combine(Path.GetDirectoryName(directory)!, "segment-state.json");
        if (!File.Exists(statePath)) yield break;
        var state = JournalControlIntegrity.Read<JournalSegmentState>(statePath);
        if (state.Version != 2 || state.OwnerHash != PagedOwnerHash(identity) || state.Digest != JournalControlIntegrity.Digest(state with { Digest = "" })) throw PagedInvalid();
        var roots = state.Roots; if (!WorkspaceStore.SamePath(roots.Directory, directory)) yield break;
        yield return roots.Nodes; yield return roots.Paths; yield return roots.Hidden; yield return roots.Changes;
        yield return roots.Names; yield return roots.DurableNodes; yield return roots.Segments;
        if (roots.Adoptions is { } adoptions) yield return adoptions;
        if (roots.Captures is { } captures) yield return captures;
        if (roots.Packing is { } packing) yield return packing;
    }
    private static void ValidateResidualNamespace(string directory, IndexResidual value, PagedMetadataStore inventory)
    {
        foreach (string path in Directory.EnumerateFileSystemEntries(directory))
        {
            WorkspaceStore.EnsurePlainPath(path); string name = Path.GetFileName(path);
            if (Directory.Exists(path))
            {
                if (inventory.Open(value.Files!).Find("d/" + name) is null) throw PagedInvalid();
                foreach (string file in Directory.EnumerateFileSystemEntries(path))
                { WorkspaceStore.EnsurePlainPath(file); if (!File.Exists(file) || inventory.Open(value.Files!).Find("f/" + name + "/" + Path.GetFileName(file)) is null) throw PagedInvalid(); }
            }
            else if (inventory.Open(value.Files!).Find("f/" + name) is null) throw PagedInvalid();
        }
    }
}
