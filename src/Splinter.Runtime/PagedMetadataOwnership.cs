using Splinter.Core;

namespace Splinter.Runtime;

public sealed record OwnedMetadataFile(string Path, string Identity);

public sealed partial class PagedMetadataStore
{
    public static string ReadIdentity(string directory) => ReadStoreIdentity(directory);
    public static void ValidateCreatedContainer(string path, string storeId, IJournalFilePins pins, bool headerOnly = false)
    {
        WorkspaceStore.EnsurePlainPath(path); long number = ParseContainer(path);
        var owner = ReadCollection<ContainerOwner>(path + ".owner");
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (owner.Version != 1 || owner.StoreId != storeId || owner.Container != number || headerOnly && input.Length > ContainerHeaderBytes)
            throw Invalid("Preserve a foreign or payload-bearing metadata initialization.");
        pins.Verify(input, owner.Identity);
    }
    // The owner holds its outer publication/reclamation lock and excludes
    // readers for the entire validation/deletion operation. This helper never
    // upgrades an equal-byte or header-only file into owned payload.
    public static void ValidateReclaimInventory(string directory, IJournalFilePins filePins, IReadOnlySet<string>? extraOwnedControls = null)
        => ValidateMetadataInventory(directory, filePins, extraOwnedControls, opaqueHeaderGap: null);

    // Validation for preserving one already unreachable, exactly fenced
    // directory as a unit. Permitted opaque entries remain unowned: this is
    // never deletion authorization and cannot feed EnumerateOwnedPayloads.
    // The callback receives an absolute path and may classify only an empty/
    // partial container header or its associated ownership-control attempt.
    public static void ValidatePreservationInventory(string directory, IJournalFilePins filePins,
        Func<string, bool> opaqueHeaderGap, IReadOnlySet<string>? extraOwnedControls = null, IEnumerable<MetadataTreeRoot>? activeRoots = null)
    {
        ArgumentNullException.ThrowIfNull(opaqueHeaderGap);
        ValidateMetadataInventory(directory, filePins, extraOwnedControls, opaqueHeaderGap);
        if (activeRoots is not null)
        {
            using var store = new PagedMetadataStore(directory, false, null, filePins, maintenance: true, containerStart: null);
            using var pending = new TraversalSpine();
            foreach (var root in activeRoots)
            { store.Open(root).Verify(); if (root.Page is { } page) pending.Push(page); }
            // Include entry-owned child trees (extent maps, directory roots,
            // capture/packing control bundles), not just left/right order.
            // ReadNode enforces strictly earlier immutable child addresses;
            // the bounded spine spills deep graphs instead of retaining them.
            while (pending.TryPop(out var reference))
            {
                var node = store.ReadNode(reference);
                if (node.Left is { } left) pending.Push(left);
                if (node.Right is { } right) pending.Push(right);
                if (node.Data is { } data) pending.Push(data);
            }
        }
    }

    private static void ValidateMetadataInventory(string directory, IJournalFilePins filePins,
        IReadOnlySet<string>? extraOwnedControls, Func<string, bool>? opaqueHeaderGap)
    {
        ArgumentNullException.ThrowIfNull(filePins);
        directory = Path.GetFullPath(directory);
        using var store = new PagedMetadataStore(directory, false, null, filePins, maintenance: true, containerStart: null);
        foreach (string path in System.IO.Directory.EnumerateFileSystemEntries(directory))
        {
            WorkspaceStore.EnsurePlainPath(path); string name = Path.GetFileName(path);
            if (System.IO.Directory.Exists(path))
            {
                if (name != "collections") throw Invalid("Preserve an unknown or unfinished metadata directory.");
                foreach (string receipt in System.IO.Directory.EnumerateFileSystemEntries(path))
                {
                    WorkspaceStore.EnsurePlainPath(receipt);
                    if (!File.Exists(receipt) || !Path.GetFileName(receipt).EndsWith(".json", StringComparison.Ordinal))
                        throw Invalid("Preserve an unknown metadata collection receipt.");
                    var value = ReadCollection<CollectionIntent>(receipt);
                    if (value.Version != 2 || value.StoreId != store.StoreId || value.Phase != "completed" ||
                        Path.GetFileName(receipt) != WorkspaceStore.Key(value.RequestKey) + ".json")
                        throw Invalid("Preserve an unsupported metadata collection receipt.");
                }
            }
            else if (opaqueHeaderGap is not null && IsOpaqueHeaderEntry(path) && opaqueHeaderGap(path)) { }
            else if (name is "identity.bin" or "writer.lock") { }
            else if (extraOwnedControls?.Contains(name) == true) { }
            else if (name.StartsWith("pages-", StringComparison.Ordinal) && name.EndsWith(".bin", StringComparison.Ordinal))
            {
                long number = ParseContainer(path);
                using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                store.CheckContainer(input);
                var owner = ReadCollection<ContainerOwner>(path + ".owner");
                if (owner.Version != 1 || owner.StoreId != store.StoreId || owner.Container != number) throw Invalid("Preserve an unowned metadata container.");
                filePins.Verify(input, owner.Identity);
            }
            else if (name.StartsWith("pages-", StringComparison.Ordinal) && name.EndsWith(".bin.owner", StringComparison.Ordinal))
            {
                var owner = ReadCollection<ContainerOwner>(path);
                if (owner.Version != 1 || owner.StoreId != store.StoreId || owner.Container != ParseContainer(path[..^6]))
                    throw Invalid("Preserve an unknown container ownership receipt.");
            }
            else if (name == "locations.json")
            {
                var header = ReadCollection<LocationHeader>(path);
                string data = ExactChild(directory, header.FileName, "locations-", ".bin");
                using var input = new FileStream(data, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                filePins.Verify(input, header.Identity);
            }
            else if (name.StartsWith("locations-", StringComparison.Ordinal) && name.EndsWith(".bin", StringComparison.Ordinal))
            {
                var owner = ReadCollection<MapOwner>(path + ".owner");
                if (owner.Version != 1 || owner.StoreId != store.StoreId) throw Invalid("Preserve a foreign metadata location file.");
                using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                filePins.Verify(input, owner.Identity);
            }
            else if (name.StartsWith("locations-", StringComparison.Ordinal) && name.EndsWith(".bin.owner", StringComparison.Ordinal))
            {
                var owner = ReadCollection<MapOwner>(path);
                if (owner.Version != 1 || owner.StoreId != store.StoreId) throw Invalid("Preserve foreign location ownership.");
            }
            else throw Invalid("Preserve an unknown or unfinished metadata control file.");
        }
    }

    private static bool IsOpaqueHeaderEntry(string path)
    {
        string name = Path.GetFileName(path);
        if (name.Length < 26 || !name.StartsWith("pages-", StringComparison.Ordinal) ||
            !name.AsSpan(22, 4).SequenceEqual(".bin") ||
            !long.TryParse(name.AsSpan(6, 16), System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out long number) || number < 0) return false;
        string suffix = name[26..];
        if (suffix.Length != 0 && suffix != ".owner" &&
            !(suffix.Length == 47 && suffix.StartsWith(".owner.", StringComparison.Ordinal) &&
              suffix.EndsWith(".pending", StringComparison.Ordinal) && Guid.TryParseExact(suffix.AsSpan(7, 32), "N", out _))) return false;
        string container = Path.Combine(Path.GetDirectoryName(path)!, name[..26]);
        WorkspaceStore.EnsurePlainPath(container);
        if (!File.Exists(container)) return false;
        using var input = new FileStream(container, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return input.Length <= ContainerHeaderBytes;
    }

    public static IEnumerable<OwnedMetadataFile> EnumerateOwnedPayloads(string directory, IJournalFilePins filePins, IReadOnlySet<string>? extraOwnedControls = null)
    {
        ValidateReclaimInventory(directory, filePins, extraOwnedControls);
        foreach (string path in System.IO.Directory.EnumerateFiles(directory, "pages-*.bin"))
        {
            var owner = ReadCollection<ContainerOwner>(path + ".owner");
            yield return new(path, owner.Identity);
        }
        foreach (string path in System.IO.Directory.EnumerateFiles(directory, "locations-*.bin"))
        {
            var owner = ReadCollection<MapOwner>(path + ".owner");
            yield return new(path, owner.Identity);
        }
    }
}
