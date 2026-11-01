using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.SourceControl.Git;

public sealed record GitSourceMetadataFile(string Path, string ObjectId, long Length,
    long CreationTime, long LastAccessTime, long LastWriteTime);
public sealed record GitSourceMetadataBlob(string Path, string ObjectId, long Length);
public sealed record GitSourceMetadataTree(string Revision, string Tree, SnapshotCatalogDescriptor Catalog, string Directory,
    TemporaryMetadataDirectory Owner) : IDisposable
{
    public IEnumerable<GitSourceMetadataBlob> Files => Records().Where(e => !e.Entry.Directory)
        .Select(e => new GitSourceMetadataBlob(e.Entry.Path, e.SourceReference!, e.Entry.Length));
    public IEnumerable<DirectoryMetadataRecord> Records()
    {
        using var catalog = new PagedSnapshotCatalog(Directory, Catalog);
        foreach (var entry in catalog.Walk().Skip(1)) yield return entry;
    }
    public void Dispose() => Owner.Dispose();
}
public sealed record GitSourceMetadataManifest(int Version, GeneratedDirectoryReference Basis,
    string Revision, string Tree, SnapshotCatalogDescriptor Catalog, long ExaminedBytes,
    long ExaminedFiles, long CaptureTicks, long ClockFrequency, string ContentHash)
{
    [JsonIgnore] public IEnumerable<GitSourceMetadataFile> Files { get; init; } = [];
    [JsonIgnore] public string? CatalogDirectory { get; init; }
    public PagedSnapshotCatalog OpenCatalog() => CatalogDirectory is { } directory
        ? new(directory, Catalog) : throw new WorkspaceException("GitSourceMetadataInvalid", "Bind the exact immutable metadata pages.");
}

// Small control JSON binds component-named binary metadata pages. Original
// content is used only to prove timestamps during explicit preparation.
public static class GitSourceMetadata
{
    public const string FileName = "source-metadata.json", DirectoryName = "source-metadata-pages";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { RespectRequiredConstructorParameters = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

    public static async Task<GitSourceMetadataTree?> CaptureTreeAsync(string project, CancellationToken ct = default)
    {
        string git = Path.Combine(project, ".git");
        if (!Directory.Exists(git)) return null;
        GitObjectSnapshotProvider.Plain(project); GitObjectSnapshotProvider.Plain(git);
        string top = await GitWorkspaceProvider.Required(project, ["rev-parse", "--show-toplevel"], ct);
        string common = await GitWorkspaceProvider.Required(project, ["rev-parse", "--path-format=absolute", "--git-common-dir"], ct);
        if (!SamePath(project, top) || !SamePath(common, git)) throw Invalid("Source metadata requires the ordinary repository root.");
        if (File.Exists(Path.Combine(common, "objects", "info", "alternates")) || File.Exists(Path.Combine(common, "shallow")) ||
            (await GitObjectSnapshotProvider.Command(common, ["config", "--get", "extensions.partialClone"], ct, allowMissing: true)).Length != 0)
            throw Invalid("Source metadata requires complete local Git objects without alternates, shallow history or lazy fetch.");
        string revision = await GitObjectSnapshotProvider.Command(common, ["rev-parse", "--verify", "HEAD^{commit}"], ct);
        string tree = await GitObjectSnapshotProvider.Command(common, ["rev-parse", revision + "^{tree}"], ct);
        RequireObject(revision); RequireObject(tree);
        GitObjectSnapshotTree.VerifyObject(common, revision, "commit"); GitObjectSnapshotTree.VerifyObject(common, tree, "tree");
        var scratch = new TemporaryMetadataDirectory();
        try
        {
            IEnumerable<DirectoryMetadataRecord> Records()
            {
                foreach (string row in GitObjectSnapshotTree.EnumerateListing(common, tree))
                {
                    ct.ThrowIfCancellationRequested(); int tab = row.IndexOf('\t');
                    if (tab < 0) throw Invalid("The recorded Git catalog is malformed.");
                    string[] fields = row[..tab].Split(' ', StringSplitOptions.RemoveEmptyEntries); string path = row[(tab + 1)..];
                    RequirePath(path);
                    if (fields.Length != 4) throw Invalid("The recorded Git catalog is malformed.");
                    bool directory = fields[0] == "040000" && fields[1] == "tree" && fields[3] == "-";
                    if (!directory && (fields[0] is not ("100644" or "100755") || fields[1] != "blob")) continue;
                    RequireObject(fields[2]); long length = 0;
                    if (!directory && (!long.TryParse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture, out length) || length < 0))
                        throw Invalid("A recorded blob length is invalid.");
                    yield return new(new(path, directory, length, (uint)(directory ? FileAttributes.Directory : FileAttributes.Archive), 0, 0, 0), null,
                        SourceReference: directory ? null : fields[2]);
                }
            }
            var catalog = PagedSnapshotCatalog.Build(scratch.Path, new("", true, 0, 16, 0, 0, 0), Records(), ct: ct);
            return new(revision, tree, catalog, scratch.Path, scratch);
        }
        catch (WorkspaceException ex)
        { try { scratch.Dispose(); } catch (IOException) { } throw Invalid(ex.Message); }
        catch { try { scratch.Dispose(); } catch (IOException) { } throw; }
    }

    public static IEnumerable<GitSourceMetadataFile> EnumerateFiles(string directory, SnapshotCatalogDescriptor root)
    {
        using var catalog = new PagedSnapshotCatalog(directory, root);
        foreach (var record in catalog.Walk())
            if (!record.Entry.Directory)
            {
                var e = record.Entry;
                yield return new(e.Path, record.SourceReference ?? throw Invalid("A metadata object identity is absent."), e.Length,
                    e.CreationTime, e.LastAccessTime, e.LastWriteTime);
            }
    }

    public static GitSourceMetadataManifest Read(GeneratedDirectoryReference basis, string project)
    {
        RequireBasis(basis, project); string path = Path.Combine(basis.ReceiptDirectory, FileName);
        GitObjectSnapshotProvider.Plain(path);
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (Convert.ToHexStringLower(SHA256.HashData(input)) != basis.SourceMetadataHash) throw Invalid("The frozen source metadata digest changed.");
        input.Position = 0; GitSourceMetadataManifest value;
        try { value = JsonSerializer.Deserialize<GitSourceMetadataManifest>(input, Json) ?? throw Invalid("The source metadata manifest is empty."); }
        catch (JsonException) { throw Invalid("Preserve the unsupported or corrupt source metadata manifest."); }
        string directory = Path.Combine(basis.ReceiptDirectory, DirectoryName);
        value = value with { CatalogDirectory = directory, Files = EnumerateFiles(directory, value.Catalog) };
        Validate(value, basis with { SourceMetadataHash = null }); return value;
    }

    public static void Validate(GitSourceMetadataManifest value, GeneratedDirectoryReference expectedBasis)
    {
        if (value.Version != 2 || value.Basis is null || value.Basis != expectedBasis || value.Basis.SourceMetadataHash is not null || value.Catalog is null ||
            value.ExaminedFiles < 0 || value.ExaminedBytes < 0 || value.CaptureTicks < 0 || value.ClockFrequency < 1)
            throw Invalid("The source metadata belongs to another Library or capture format.");
        RequireObject(value.Revision); RequireObject(value.Tree);
        if (value.Tree.Length != value.Revision.Length) throw Invalid("Source metadata object formats disagree.");
        try
        {
            using var catalog = value.OpenCatalog(); catalog.Verify(); long files = 0;
            foreach (var entry in value.Files)
            {
                RequirePath(entry.Path); RequireObject(entry.ObjectId);
                if (entry.ObjectId.Length != value.Revision.Length || entry.Length < 0 || !ValidTime(entry.CreationTime) ||
                    !ValidTime(entry.LastAccessTime) || !ValidTime(entry.LastWriteTime)) throw Invalid("Captured source metadata has invalid timestamps or objects.");
                files = checked(files + 1);
            }
            if (files > value.ExaminedFiles) throw Invalid("Captured metadata exceeds the examined membership.");
        }
        catch (WorkspaceException ex) { throw Invalid(ex.Message); }
        if (value.ContentHash is not { Length: 64 } hash || hash.Any(c => !char.IsAsciiHexDigit(c) || char.IsUpper(c)) || hash != ComputeContentHash(value))
            throw Invalid("The captured source metadata content proof changed.");
    }
    public static string ComputeContentHash(GitSourceMetadataManifest value) =>
        Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            value.Version, value.Basis, value.Revision, value.Tree, value.ExaminedBytes, value.ExaminedFiles, value.CaptureTicks, value.ClockFrequency,
            value.Catalog.Root, value.Catalog.Count, value.Catalog.CaseSensitive, metadataHash = value.Catalog.Children.ContentHash
        }, Json)));

    public static void RequireBasis(GeneratedDirectoryReference basis, string project)
    {
        if (basis is null || string.IsNullOrWhiteSpace(basis.ProjectPath) || string.IsNullOrWhiteSpace(basis.ReceiptDirectory) ||
            basis.Snapshot is null || !SamePath(basis.ProjectPath, project) || basis.RelativePath != "Library" || basis.Snapshot.Provider != "owned-directory" ||
            basis.OwnerId != basis.Id || basis.Snapshot.Id != basis.Id || basis.SourceMetadataHash is not { Length: 64 } hash ||
            hash.Any(c => !char.IsAsciiHexDigit(c) || char.IsUpper(c)) || basis.ManifestHash is not { Length: 64 })
            throw Invalid("The exact Library source metadata reference is required.");
    }
    private static bool ValidTime(long value) => value is >= 0 and <= 2650467743999999999L;
    private static void RequirePath(string path)
    {
        try
        {
            if (string.IsNullOrEmpty(path) || WorkspacePathPolicy.Windows.Normalize(path) != path ||
                path.StartsWith(".git/", StringComparison.OrdinalIgnoreCase) || path.Equals(".git", StringComparison.OrdinalIgnoreCase))
                throw Invalid("Only exact relative source file paths may carry metadata.");
        }
        catch (ArgumentException) { throw Invalid("A source metadata path is invalid."); }
    }
    private static void RequireObject(string id)
    {
        if (id is null || id.Length is not (40 or 64) || id.Any(c => !char.IsAsciiHexDigit(c) || char.IsUpper(c))) throw Invalid("A source metadata object ID is invalid.");
    }
    private static bool SamePath(string a, string b) => string.Equals(Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar),
        Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
    private static WorkspaceException Invalid(string message) => new("GitSourceMetadataInvalid", message);
}
