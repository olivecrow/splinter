using System.Text.Json;
using System.Text.Json.Serialization;
using Splinter.Core;

namespace Splinter.Runtime;

// Home-owned immutable metadata, shared by continuing tasks. The per-scope
// header is a reuse hint only: new input is still enumerated/hashed and pages
// are reused only after their logical identity matches the newly built node.
public sealed partial class SharedMetadataPool : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    private readonly FileStream lease;
    private readonly string home, hint;
    private readonly IJournalFilePins? filePins;
    public string Directory { get; }
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record Owner([property: JsonRequired] int Version, [property: JsonRequired] string Home,
        [property: JsonRequired] string StoreId);
    private sealed record InitializationIdentity(int Version, string Home, string StoreId, string Identity);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record Reuse([property: JsonRequired] int Version, [property: JsonRequired] SnapshotCatalogDescriptor Catalog,
        [property: JsonRequired] MetadataTreeRoot PayloadIndex, [property: JsonRequired] MetadataTreeRoot PayloadPaths);
    public SharedMetadataPool(string home, string scope, CancellationToken ct = default, IJournalFilePins? filePins = null, Action<string>? boundary = null)
    {
        this.filePins = filePins;
        this.home = Path.GetFullPath(home); var store = new WorkspaceStore(this.home);
        lease = store.LockAsync(WorkspaceStore.Key("shared-metadata-pool"), ct).GetAwaiter().GetResult();
        Directory = Path.Combine(this.home, "metadata-store"); hint = Path.Combine(Directory, "reuse-" + WorkspaceStore.Key(scope) + ".json");
        try
        {
            WorkspaceStore.EnsurePlainPath(Directory);
            string owner = Path.Combine(Directory, "owner.json");
            if (!File.Exists(owner))
            {
                string intent = Path.Combine(this.home, "metadata-store.initialization.json");
                if (!File.Exists(intent) && System.IO.Directory.Exists(Directory)) throw Invalid("Preserve an unowned shared metadata directory.");
                string expectedId = Initialize(intent)[..24];
                System.IO.Directory.CreateDirectory(Directory);
                foreach (string path in System.IO.Directory.EnumerateFileSystemEntries(Directory))
                {
                    WorkspaceStore.EnsurePlainPath(path); string name = Path.GetFileName(path);
                    bool receiptScratch = System.Text.RegularExpressions.Regex.IsMatch(name, "^pages-[a-f0-9]{16}\\.bin\\.owner\\.[a-f0-9]{32}\\.pending$") &&
                        File.Exists(Path.Combine(Directory, name[..26])) && new FileInfo(Path.Combine(Directory, name[..26])).Length <= 48;
                    if (name.StartsWith("pages-", StringComparison.Ordinal) && name.EndsWith(".bin.owner", StringComparison.Ordinal) && !File.Exists(path[..^6]))
                        throw Invalid("Preserve an orphaned metadata creation receipt without its original container.");
                    if (System.IO.Directory.Exists(path) || name is not ("identity.bin" or "identity.bin.pending" or "writer.lock") &&
                        !(name.StartsWith("pages-", StringComparison.Ordinal) && name.EndsWith(".bin", StringComparison.Ordinal) && new FileInfo(path).Length <= 48) &&
                        !(name.StartsWith("pages-", StringComparison.Ordinal) && name.EndsWith(".bin.owner", StringComparison.Ordinal)) &&
                        !receiptScratch &&
                        !(name.StartsWith("owner.json.", StringComparison.Ordinal) && name.EndsWith(".pending", StringComparison.Ordinal)))
                        throw Invalid("Preserve metadata pages or unknown input without a published pool owner.");
                }
                // The ordinary metadata initializer owns/reconciles its torn
                // identity before any result or input pages are written.
                void Observe(string point)
                {
                    if (point is "metadata.identity-published" or "metadata.container-created-before-owner") BindInitializationIdentity(expectedId);
                    boundary?.Invoke(point);
                }
                if (File.Exists(Path.Combine(Directory, "identity.bin"))) BindInitializationIdentity(expectedId);
                using var pages = new PagedMetadataStore(Directory, true, Observe, filePins, initialStoreId: expectedId); pages.Flush();
                if (filePins is not null)
                    foreach (string path in System.IO.Directory.EnumerateFiles(Directory, "pages-*.bin"))
                    {
                        // Only a header-sized object can be a receipt-creation
                        // gap: it cannot contain any published logical page.
                        // Keep it opaque and unowned; ordinary collection will
                        // inventory it rather than adopt/delete it.
                        try { PagedMetadataStore.ValidateCreatedContainer(path, pages.StoreId, filePins, headerOnly: true); }
                        catch (Exception error) when (new FileInfo(path).Length <= 48 && error is IOException or WorkspaceException) { }
                    }
                Write(owner, new Owner(1, this.home, pages.StoreId));
            }
            using var reader = new PagedMetadataStore(Directory);
            var recorded = Read<Owner>(owner);
            if (recorded.Version != 1 || !WorkspaceStore.SamePath(recorded.Home, this.home) || recorded.StoreId != reader.StoreId)
                throw Invalid("Preserve a foreign shared metadata pool.");
            if (filePins is not null && File.Exists(Path.Combine(this.home, "metadata-store.identity.json"))) BindInitializationIdentity(recorded.StoreId);
        }
        catch { lease.Dispose(); throw; }
    }
    public static string Location(string control)
    {
        string? records = Path.GetDirectoryName(Path.GetFullPath(control));
        if (records is null || Path.GetFileName(records) != "records" || Path.GetDirectoryName(records) is not { } home)
            throw Invalid("Metadata must be referenced through an exact workspace record directory.");
        return Path.Combine(home, "metadata-store");
    }
    public Reuse? Previous()
    {
        if (!File.Exists(hint)) return null;
        var previous = Read<Reuse>(hint);
        if (previous.Version != 1) throw Invalid("Preserve an unsupported metadata reuse header.");
        using var pages = new PagedMetadataStore(Directory);
        pages.Open(previous.Catalog.Children); pages.Open(previous.PayloadIndex); pages.Open(previous.PayloadPaths);
        return previous;
    }
    public void Publish(SnapshotCatalogDescriptor catalog, MetadataTreeRoot payloads, MetadataTreeRoot paths)
    { Write(hint, new Reuse(1, catalog, payloads, paths)); }
    public FileStream Anchor(SnapshotCatalogDescriptor catalog, MetadataTreeRoot payloads, MetadataTreeRoot paths)
    {
        string anchors = Path.Combine(Directory, "anchors"); WorkspaceStore.EnsurePlainPath(anchors); System.IO.Directory.CreateDirectory(anchors);
        string path = Path.Combine(anchors, Guid.NewGuid().ToString("N") + ".json");
        var lease = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.DeleteOnClose);
        try { JsonSerializer.Serialize(lease, new Reuse(1, catalog, payloads, paths), Json); lease.Flush(true); return lease; }
        catch { lease.Dispose(); throw; }
    }
    public void Dispose() => lease.Dispose();
    private static T Read<T>(string path)
    {
        WorkspaceStore.EnsurePlainPath(path); using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return JsonSerializer.Deserialize<T>(input, Json) ?? throw Invalid("A metadata pool header is empty.");
    }
    private string Initialize(string path)
    {
        string hash = WorkspaceStore.Key(OperatingSystem.IsWindows() ? home.ToUpperInvariant() : home);
        string prefix = "{\"version\":1,\"homeHash\":\"" + hash + "\",\"nonce\":\"", suffix = "\"}\n";
        byte[] value = System.Text.Encoding.ASCII.GetBytes(prefix + Guid.NewGuid().ToString("N") + suffix);
        using var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
        if (stream.Length > value.Length) throw Invalid("Preserve an oversized metadata initialization intent.");
        int retained = checked((int)stream.Length); byte[] prior = new byte[retained]; stream.ReadExactly(prior);
        for (int index = 0; index < retained; index++)
        {
            if (index >= prefix.Length && index < prefix.Length + 32)
            { if (prior[index] is not (>= (byte)'0' and <= (byte)'9' or >= (byte)'a' and <= (byte)'f')) throw Invalid("Preserve an invalid metadata initialization nonce."); value[index] = prior[index]; }
            else if (prior[index] != value[index]) throw Invalid("Preserve another metadata initialization request.");
        }
        stream.Write(value, retained, value.Length - retained); stream.Flush(true);
        return System.Text.Encoding.ASCII.GetString(value, prefix.Length, 32);
    }
    private void BindInitializationIdentity(string expectedId)
    {
        if (PagedMetadataStore.ReadIdentity(Directory) != expectedId) throw Invalid("Preserve a metadata identity foreign to the fixed home initialization.");
        if (filePins is null) return;
        string path = Path.Combine(home, "metadata-store.identity.json"), identity = Path.Combine(Directory, "identity.bin");
        if (!File.Exists(path)) Write(path, new InitializationIdentity(1, home, expectedId, filePins.Identify(identity)));
        var recorded = Read<InitializationIdentity>(path);
        if (recorded.Version != 1 || !WorkspaceStore.SamePath(recorded.Home, home) || recorded.StoreId != expectedId)
            throw Invalid("Preserve an initialization receipt from another store/home.");
        using var input = new FileStream(identity, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        filePins.Verify(input, recorded.Identity);
    }
    private static void Write<T>(string path, T value)
    {
        WorkspaceStore.EnsurePlainPath(path); string pending = path + "." + Guid.NewGuid().ToString("N") + ".pending";
        using (var output = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { JsonSerializer.Serialize(output, value, Json); output.Flush(true); }
        AtomicFilePublication.Publish(pending, path);
    }
    private static WorkspaceException Invalid(string message) => new("MetadataPoolInvalid", message);
}
