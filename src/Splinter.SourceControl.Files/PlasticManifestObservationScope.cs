using System.Security.Cryptography;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.SourceControl.Files;

// A read proof, not creation/deletion ownership. Legacy catalog files without
// native owner receipts stay unowned by this scope; no receipt is ever written.
// Every physical binding is write/delete fenced before the full logical proof.
internal sealed class PlasticManifestObservationScope : IDisposable
{
    private sealed class FileFence(string relative, FileStream input, string identity, long bytes, FileFence? next)
    {
        public readonly string Relative = relative, Identity = identity;
        public readonly FileStream Input = input;
        public readonly long Bytes = bytes;
        public readonly FileFence? Next = next;
    }
    private readonly object gate = new();
    private readonly string directory, catalog;
    private readonly SnapshotReference snapshot;
    private readonly SnapshotReadAccess access;
    private readonly IJournalFilePins pins;
    private readonly string directoryIdentity, catalogIdentity, manifestIdentity = "";
    private readonly FileStream manifest = null!;
    private readonly long manifestBytes;
    private IDisposable? directoryFence, catalogFence, collectionFence;
    private string? collectionIdentity;
    private FileFence? files;
    private bool disposed;
    public long FileCount { get; private set; }
    public long Bytes { get; private set; }
    public long HandleCount => checked(FileCount + 3 + (collectionFence is null ? 0 : 1));

    public PlasticManifestObservationScope(string directory, SnapshotReference snapshot, SnapshotReadAccess access,
        PlasticRevisionManifest recorded, IJournalFilePins pins)
    {
        this.directory = Path.GetFullPath(directory); this.snapshot = snapshot; this.access = access; this.pins = pins;
        catalog = Path.Combine(this.directory, "catalog");
        if (!OperatingSystem.IsWindows() || !WorkspaceStore.SamePath(snapshot.RootPath, this.directory) ||
            recorded.CatalogDirectory is null || !WorkspaceStore.SamePath(recorded.CatalogDirectory, catalog) || recorded.Catalog is null)
            throw Invalid("The scope requires the exact existing fixed catalog and native share fences.");
        directoryIdentity = pins.IdentifyDirectory(this.directory); catalogIdentity = pins.IdentifyDirectory(catalog);
        FileStream? openedManifest = null;
        try
        {
            directoryFence = pins.PinDirectory(this.directory, directoryIdentity); catalogFence = pins.PinDirectory(catalog, catalogIdentity);
            string manifestPath = Path.Combine(this.directory, "plastic-basis.json"); WorkspaceStore.EnsurePlainPath(manifestPath);
            openedManifest = new(manifestPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            manifestIdentity = pins.IdentifyOpened(openedManifest); manifest = openedManifest; manifestBytes = manifest.Length; Bytes = manifestBytes; openedManifest = null;
            long owners = 0, pages = 0, nativeControls = 0; Span<byte> header = stackalloc byte[48];
            foreach (var item in Namespace())
            {
                if (item.Directory)
                {
                    collectionIdentity = pins.IdentifyDirectory(item.Path); collectionFence = pins.PinDirectory(item.Path, collectionIdentity); continue;
                }
                var input = new FileStream(item.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
                try
                {
                    string relative = Path.GetRelativePath(catalog, item.Path).Replace('\\', '/');
                    if (relative == "writer.lock" && input.Length != 0) throw Invalid("Preserve an unsupported catalog writer fence.");
                    if (relative == "identity.bin" || relative.StartsWith("pages-", StringComparison.Ordinal) && relative.EndsWith(".bin", StringComparison.Ordinal))
                    {
                        input.ReadExactly(header); input.Position = 0;
                        if (!header[..4].SequenceEqual(PagedMetadataStore.FormatMagic) || Convert.ToHexStringLower(header.Slice(4, 12)) != recorded.Catalog.Children.StoreId ||
                            !SHA256.HashData(header[..16]).AsSpan().SequenceEqual(header[16..])) throw Invalid("Preserve a foreign catalog container.");
                    }
                    files = new(relative, input, pins.IdentifyOpened(input), input.Length, files);
                    FileCount = checked(FileCount + 1); Bytes = checked(Bytes + input.Length);
                    if (relative.StartsWith("pages-", StringComparison.Ordinal) && relative.EndsWith(".bin", StringComparison.Ordinal)) pages++;
                    if (relative.StartsWith("pages-", StringComparison.Ordinal) && relative.EndsWith(".bin.owner", StringComparison.Ordinal)) owners++;
                    if (relative.EndsWith(".owner", StringComparison.Ordinal) || relative == "locations.json") nativeControls++;
                }
                catch { input.Dispose(); throw; }
            }
            if (owners != 0)
            {
                if (owners != pages) throw Invalid("Preserve partial native catalog ownership rather than adopting missing receipts.");
                PagedMetadataStore.ValidateReclaimInventory(catalog, pins);
            }
            else if (nativeControls != 0 || collectionFence is not null) throw Invalid("Preserve an incomplete native catalog ownership/relocation domain.");
            Check();
        }
        catch
        {
            openedManifest?.Dispose();
            for (var file = files; file is not null; file = file.Next) file.Input.Dispose(); files = null;
            // readonly fields assigned only after the exact opened manifest
            // exists; a constructor failure owns every handle it admitted.
            if (manifest is not null) manifest.Dispose();
            collectionFence?.Dispose(); catalogFence?.Dispose(); directoryFence?.Dispose(); throw;
        }
    }
    private static WorkspaceException Invalid(string message) => new("PlasticBasisInvalid", message);
    private IEnumerable<(string Path, bool Directory)> Namespace()
    {
        foreach (string path in System.IO.Directory.EnumerateFileSystemEntries(catalog))
        {
            WorkspaceStore.EnsurePlainPath(path); string name = Path.GetFileName(path);
            if (System.IO.Directory.Exists(path))
            {
                if (name != "collections") throw Invalid("Preserve an unknown or pending catalog namespace.");
                yield return (path, true);
                foreach (string child in System.IO.Directory.EnumerateFileSystemEntries(path))
                {
                    WorkspaceStore.EnsurePlainPath(child);
                    if (!File.Exists(child) || !Path.GetFileName(child).EndsWith(".json", StringComparison.Ordinal)) throw Invalid("Preserve an unknown catalog collection control.");
                    yield return (child, false);
                }
            }
            else
            {
                if (!File.Exists(path) || !KnownFile(name)) throw Invalid("Preserve an unknown or unfinished catalog file.");
                yield return (path, false);
            }
        }
    }
    private static bool KnownFile(string name)
    {
        if (name is "identity.bin" or "writer.lock" or "locations.json") return true;
        string payload = name.EndsWith(".owner", StringComparison.Ordinal) ? name[..^6] : name;
        return payload.Length == 26 && payload.StartsWith("pages-", StringComparison.Ordinal) && payload.EndsWith(".bin", StringComparison.Ordinal) &&
            long.TryParse(payload.AsSpan(6, 16), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out long number) && number >= 0 ||
            payload.StartsWith("locations-", StringComparison.Ordinal) && payload.EndsWith(".bin", StringComparison.Ordinal);
    }
    public void Check()
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this); WorkspaceStore.EnsurePlainPath(directory); WorkspaceStore.EnsurePlainPath(catalog);
            if (pins.IdentifyDirectory(directory) != directoryIdentity || pins.IdentifyDirectory(catalog) != catalogIdentity) throw Invalid("A scoped catalog directory was replaced.");
            pins.VerifyPath(manifest, Path.Combine(directory, "plastic-basis.json"), manifestIdentity);
            if (manifest.Length != manifestBytes) throw Invalid("A write-fenced manifest length changed.");
            foreach (var file in EnumerateFences())
            {
                string path = Path.Combine(catalog, file.Relative.Replace('/', Path.DirectorySeparatorChar));
                pins.VerifyPath(file.Input, path, file.Identity);
                if (file.Input.Length != file.Bytes) throw Invalid("A write-fenced catalog length changed.");
            }
            long count = 0, directories = 0;
            foreach (var item in Namespace()) { if (item.Directory) directories++; else count++; }
            if (count != FileCount || directories != (collectionFence is null ? 0 : 1) ||
                collectionIdentity is not null && pins.IdentifyDirectory(Path.Combine(catalog, "collections")) != collectionIdentity)
                throw Invalid("The scoped catalog namespace changed.");
        }
    }
    private IEnumerable<FileFence> EnumerateFences()
    { for (var file = files; file is not null; file = file.Next) yield return file; }
    public SnapshotObservation Observe(SnapshotReadAccess expected)
    {
        if (expected != access) throw new WorkspaceException("SnapshotIdentityMismatch", "Use the exact held manifest read identity.");
        try { Check(); return new(snapshot.Id, true, null, 0, null) { ReadAccess = access }; }
        catch (WorkspaceException failure) { return new(snapshot.Id, null, failure.Code, null, null); }
        catch (IOException) { return new(snapshot.Id, null, "PlasticBasisInvalid", null, null); }
    }
    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return; disposed = true;
            try { foreach (var file in EnumerateFences()) file.Input.Dispose(); files = null; manifest.Dispose(); }
            finally { collectionFence?.Dispose(); catalogFence?.Dispose(); directoryFence?.Dispose(); collectionFence = catalogFence = directoryFence = null; }
        }
    }
}
