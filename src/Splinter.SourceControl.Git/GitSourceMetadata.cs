using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Splinter.Core;

namespace Splinter.SourceControl.Git;

public sealed record GitSourceMetadataFile(string Path, string ObjectId, long Length,
    long CreationTime, long LastAccessTime, long LastWriteTime);
public sealed record GitSourceMetadataBlob(string Path, string ObjectId, long Length);
public sealed record GitSourceMetadataTree(string Revision, string Tree, GitSourceMetadataBlob[] Files);
public sealed record GitSourceMetadataManifest(int Version, GeneratedDirectoryReference Basis,
    string Revision, string Tree, GitSourceMetadataFile[] Files, long ExaminedBytes,
    int ExaminedFiles, long CaptureTicks, long ClockFrequency, string ContentHash);

// A source metadata map is prepared beside its Library, then content-addressed
// by that Library reference. It never substitutes original bytes for Git blobs.
public static class GitSourceMetadata
{
    public const string FileName = "source-metadata.json";
    public const int MaximumFiles = 250000;
    public const int MaximumManifestBytes = 64 * 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { RespectRequiredConstructorParameters = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

    // Catalog only ordinary repository roots. Non-Git preparation has no source
    // metadata optimization; a present but invalid Git directory fails closed.
    public static async Task<GitSourceMetadataTree?> CaptureTreeAsync(string project, CancellationToken ct = default)
    {
        string git = Path.Combine(project, ".git");
        // A linked-worktree .git file is not an ordinary-root capture input.
        // This declines only timestamp reuse; it does not select a source VCS.
        if (!Directory.Exists(git)) return null;
        GitObjectSnapshotProvider.Plain(project); GitObjectSnapshotProvider.Plain(git);
        string top = await GitWorkspaceProvider.Required(project, ["rev-parse", "--show-toplevel"], ct);
        string common = await GitWorkspaceProvider.Required(project, ["rev-parse", "--path-format=absolute", "--git-common-dir"], ct);
        if (!SamePath(project, top) || !SamePath(common, git))
            throw Invalid("Source metadata requires the ordinary repository root.");
        if (File.Exists(Path.Combine(common, "objects", "info", "alternates")) || File.Exists(Path.Combine(common, "shallow")) ||
            (await GitObjectSnapshotProvider.Command(common, ["config", "--get", "extensions.partialClone"], ct, allowMissing: true)).Length != 0)
            throw Invalid("Source metadata requires complete local Git objects without alternates, shallow history or lazy fetch.");
        string revision = await GitObjectSnapshotProvider.Command(common, ["rev-parse", "--verify", "HEAD^{commit}"], ct);
        string tree = await GitObjectSnapshotProvider.Command(common, ["rev-parse", revision + "^{tree}"], ct);
        RequireObject(revision); RequireObject(tree);
        GitObjectSnapshotTree.VerifyObject(common, revision, "commit");
        GitObjectSnapshotTree.VerifyObject(common, tree, "tree");
        string listing = await GitObjectSnapshotProvider.Command(common, ["ls-tree", "-r", "-l", "-z", tree], ct);
        if (listing.Length > MaximumManifestBytes) throw Invalid("Source metadata exceeds its bounded catalog.");
        var files = new List<GitSourceMetadataBlob>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string line in listing.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            int tab = line.IndexOf('\t');
            if (tab < 0) throw Invalid("The recorded Git catalog is malformed.");
            string[] fields = line[..tab].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string path = line[(tab + 1)..];
            RequirePath(path);
            if (!names.Add(path) || names.Count > MaximumFiles) throw Invalid("Source paths collide or exceed the metadata limit.");
            if (fields.Length != 4) throw Invalid("The recorded Git catalog is malformed.");
            // Other input kinds have no eligible original file timestamps. The
            // workspace's ordinary Git admission remains responsible for them.
            if (fields[0] is not ("100644" or "100755") || fields[1] != "blob") continue;
            RequireObject(fields[2]);
            if (!long.TryParse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture, out long length) || length < 0)
                throw Invalid("A recorded blob length is invalid.");
            files.Add(new(path, fields[2], length));
        }
        return new(revision, tree, [.. files]);
    }

    public static IReadOnlyDictionary<string, GitSourceMetadataFile> ValidateFiles(IEnumerable<GitSourceMetadataFile> entries)
    {
        var files = new Dictionary<string, GitSourceMetadataFile>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            if (entry is null) throw Invalid("A captured source metadata entry is missing.");
            RequirePath(entry.Path); RequireObject(entry.ObjectId);
            if (entry.Length < 0 || !ValidTime(entry.CreationTime) || !ValidTime(entry.LastAccessTime) || !ValidTime(entry.LastWriteTime) ||
                !names.Add(entry.Path) || files.Count >= MaximumFiles)
                throw Invalid("Captured source metadata has invalid bounds, timestamps or paths.");
            files.Add(entry.Path, entry);
        }
        return files;
    }

    public static GitSourceMetadataManifest Read(GeneratedDirectoryReference basis, string project)
    {
        RequireBasis(basis, project);
        string path = Path.Combine(basis.ReceiptDirectory, FileName);
        GitObjectSnapshotProvider.Plain(path);
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length is <= 0 or > MaximumManifestBytes) throw Invalid("The source metadata manifest exceeds its bound.");
        byte[] bytes = new byte[checked((int)input.Length)]; input.ReadExactly(bytes);
        if (input.Length != bytes.Length || Convert.ToHexStringLower(SHA256.HashData(bytes)) != basis.SourceMetadataHash)
            throw Invalid("The frozen source metadata digest changed.");
        GitSourceMetadataManifest value;
        try { value = JsonSerializer.Deserialize<GitSourceMetadataManifest>(bytes, Json) ?? throw Invalid("The source metadata manifest is empty."); }
        catch (JsonException) { throw Invalid("Preserve the unsupported or corrupt source metadata manifest."); }
        Validate(value, basis with { SourceMetadataHash = null });
        return value;
    }

    public static void Validate(GitSourceMetadataManifest value, GeneratedDirectoryReference expectedBasis)
    {
        if (value.Version != 1 || value.Basis is null || value.Basis != expectedBasis || value.Basis.SourceMetadataHash is not null || value.Files is null ||
            value.ExaminedFiles < value.Files.Length || value.ExaminedFiles > MaximumFiles || value.ExaminedBytes < 0 ||
            value.CaptureTicks < 0 || value.ClockFrequency < 1)
            throw Invalid("The source metadata belongs to another Library or capture format.");
        RequireObject(value.Revision); RequireObject(value.Tree);
        var files = ValidateFiles(value.Files);
        if (value.Tree.Length != value.Revision.Length || files.Values.Any(e => e.ObjectId.Length != value.Revision.Length))
            throw Invalid("Source metadata object formats disagree.");
        if (value.ContentHash is not { Length: 64 } hash || hash.Any(c => !char.IsAsciiHexDigit(c) || char.IsUpper(c)) ||
            hash != ComputeContentHash(value))
            throw Invalid("The captured source metadata content proof changed.");
    }

    // Publish the proof in the same atomic file as its payload. This detects
    // corruption during an interrupted pre-state preparation without creating
    // another publication boundary; the later state binds the whole file hash.
    public static string ComputeContentHash(GitSourceMetadataManifest value) =>
        Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value with { ContentHash = "" }, Json)));

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
        if (id is null || id.Length is not (40 or 64) || id.Any(c => !char.IsAsciiHexDigit(c) || char.IsUpper(c)))
            throw Invalid("A source metadata object ID is invalid.");
    }
    private static bool SamePath(string a, string b) => string.Equals(Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar),
        Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
    private static WorkspaceException Invalid(string message) => new("GitSourceMetadataInvalid", message);
}
