using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.SourceControl.Files;

// Content is read at admission/inspection, but only explicit result collection
// stores payloads. This adapter never creates a checkout or invokes a VCS.
public sealed partial class FileWorkspaceProvider(WorkspaceStore store, IOriginalFilePublisher? originalPublisher = null, IPlasticRevisionClient? revisionClient = null,
    IPlasticOriginalGuard? originalGuard = null, IProjectSourceAccess? projectAccess = null,
    ApplicationProjectPolicies? applicationPolicies = null) : ISourceWorkspaceProvider,
    ISnapshotSourceWorkspaceProvider, ISourceDiscardProvider, IOriginalApplyProvider
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public static readonly string[] VcsMetadata = [".git", ".plastic", ".hg", ".svn"];
    private string Control(WorkspaceRecord w) => store.ControlPath(w.Id);
    private static string FilePath(string control, string name) => Path.Combine(control, "files", name.Replace('/', Path.DirectorySeparatorChar));
    private static WorkspaceException Error(string message) => new("FileSourceStateInvalid", message);
    private static void Require(WorkspaceRecord w)
    {
        if (w.SourceKind is not ("none" or "plastic") || (w.StorageMode != "ntfs-snapshot" || w.BaseReference != "snapshot") && !(w.SourceKind == "plastic" && w.StorageMode == "plastic-snapshot") && !(w.SourceKind == "none" && w.StorageMode == "project-snapshot" && w.GeneratedDirectoryId is not null) || w.Purpose != "task")
            throw Error("Only the explicitly selected plain-folder NTFS task is supported.");
    }
    public Task<string> PreflightAsync(WorkspaceRecord w, CancellationToken ct = default)
    {
        if (w.SourceKind != "none") throw Error("Plastic input requires its explicit compatibility adapter.");
        using var lease = w.StorageMode == "project-snapshot" ? (projectAccess ?? throw new WorkspaceException("ProjectBasisUnsupported", "Compose the exact managed project source guard.")).Acquire(w, false, ct) : null;
        return PreflightFilesAsync(w, ct);
    }
    internal static Task<string> PreflightFilesAsync(WorkspaceRecord w, CancellationToken ct = default)
    {
        Require(w);
        for (string? path = w.ProjectPath; path is not null; path = Path.GetDirectoryName(path))
            foreach (string name in VcsMetadata)
            {
                if (w.SourceKind == "plastic" && name == ".plastic" && WorkspaceStore.SamePath(path, w.ProjectPath)) continue;
                if (File.Exists(Path.Combine(path, name)) || Directory.Exists(Path.Combine(path, name)))
                    throw new WorkspaceException("VcsMetadataUnsupported", "The plain-folder path cannot contain or inherit VCS administration.");
            }
        var entries = Scan(w.ProjectPath, w.ManagedPaths, ct, metadataOnly: true, mount: w.StorageMode == "project-snapshot" ? w.ProjectPath : null, omitPlasticRoot: w.SourceKind == "plastic");
        if (entries.Any(e => e.Path.Split('/').Any(p => p.StartsWith(".splinter-apply-", StringComparison.OrdinalIgnoreCase))))
            throw new WorkspaceException("OriginalApplyResourcesPresent", "Preserve unresolved original publication resources; do not capture them as a new task's source.");
        return Task.FromResult("snapshot:" + w.Id);
    }
    public Task RegisterAsync(WorkspaceRecord w, string control, CancellationToken ct = default)
    {
        Require(w);
        if (w.Snapshot?.Reference is null) throw Error("The immutable snapshot identity is missing.");
        if (w.SourceKind == "plastic") PlasticWorkspaceProvider.VerifyReceipt(w, control);
        SaveFixed(FilePath(control, "owner.json"), new FileSourceOwner(w.Id, w.RequestDigest!, w.ProjectPath,
            w.Path, w.SourceReference, w.Snapshot.Reference));
        return Task.CompletedTask;
    }
    private static void Owner(WorkspaceRecord w, string control, Func<string, Stream>? open = null)
    {
        Require(w);
        if (w.SourceKind == "plastic") PlasticWorkspaceProvider.VerifyReceipt(w, control, open);
        var owner = Read<FileSourceOwner>(FilePath(control, "owner.json"), open);
        if (owner.Version != 1 || owner != new FileSourceOwner(w.Id, w.RequestDigest!, w.ProjectPath, w.Path, w.SourceReference, w.Snapshot!.Reference!))
            throw Error("The file source owner differs from this request and snapshot.");
    }
    public Task AttachAsync(WorkspaceRecord w, string control, CancellationToken ct = default)
    { Owner(w, control); RequireMount(w); return Task.CompletedTask; }
    public Task<string> EstablishBaselineAsync(WorkspaceRecord w, string control, CancellationToken ct = default)
    {
        Owner(w, control);
        string path = FilePath(control, "baseline.json");
        FileSourceManifest baseline;
        if (File.Exists(path)) baseline = ReadManifest(w, path);
        else
        {
            string root = ImmutableRoot(w);
            baseline = w.StorageMode == "plastic-snapshot" ? RecordedManifest(w, control, ct) :
                Manifest(w, Scan(root, w.ManagedPaths, ct, omitPlasticRoot: w.SourceKind == "plastic"));
            SaveFixed(path, baseline);
        }
        return Task.FromResult(baseline.Identity);
    }
    private static string ImmutableRoot(WorkspaceRecord w)
    {
        var observation = w.Snapshot?.Observation;
        if (observation?.SnapshotAvailable != true || observation.SnapshotStorage?.ReadAccess is not { } access)
            throw new WorkspaceException("SnapshotVerificationRequired", "Read the exact immutable captured root before computing or preserving the basis.");
        access.RequireMatch(w.Snapshot!.Reference!);
        return access.RootPath;
    }
    public Task<SourceState> InspectAsync(WorkspaceRecord w, CancellationToken ct = default)
    {
        Owner(w, Control(w));
        RequireBasis(w, Control(w));
        RequireMount(w);
        var entries = Scan(w.Path, w.ManagedPaths, ct, mount: w.Path);
        var current = Manifest(w, entries);
        string revision = w.ResultRevision ?? w.BaseRevision!;
        bool changed = current.Identity != revision;
        var basis = ReadManifest(w, FilePath(Control(w), File.Exists(FilePath(Control(w), "result.json")) ? "result.json" : "baseline.json"));
        string[] changedPaths = changed ? Changed(basis.Entries, entries) : [];
        return Task.FromResult(new SourceState(revision, current.Identity, changed, changedPaths));
    }
    public Task<string> CheckpointAsync(WorkspaceRecord w, string message, CancellationToken ct = default)
    {
        Owner(w, Control(w)); RequireMount(w);
        RequireBasis(w, Control(w));
        return Task.FromResult(Manifest(w, Scan(w.Path, w.ManagedPaths, ct, mount: w.Path)).Identity);
    }
    public Task<string> PreserveAsync(WorkspaceRecord w, string control, CancellationToken ct = default)
    {
        Owner(w, control); RequireMount(w);
        var baseline = ReadManifest(w, FilePath(control, "baseline.json"));
        if (baseline.Identity != w.BaseRevision) throw Error("The captured basis differs from the durable task.");
        var result = Manifest(w, Scan(w.Path, w.ManagedPaths, ct, mount: w.Path));
        if (result.Identity != w.ResultRevision) throw new WorkspaceException("UncollectedChanges", "The source changed after checkpoint; retain the journal.");
        SaveFixed(FilePath(control, "collection.json"), new FileCollectionIntent(w.Id, w.RequestDigest!, baseline.Identity, result.Identity));
        if (w.StorageMode == "plastic-snapshot") PreserveRecordedPayloads(w, control, baseline, ct);
        else PreservePayloads(control, baseline, ImmutableRoot(w), ct);
        PreservePayloads(control, result, w.Path, ct, w.Path);
        // Verify that every payload exists before publishing an independent result.
        VerifyPayloads(control, baseline.Entries.Concat(result.Entries), ct);
        SaveFixed(FilePath(control, "result.json"), result);
        VerifyIndependentResult(w, control);
        var payloads = baseline.Entries.Concat(result.Entries).Where(e => e.Kind == "file").DistinctBy(e => e.Hash).ToArray();
        w.ResultStorage = new(baseline.Entries.Sum(e => e.Length), result.Entries.Sum(e => e.Length), payloads.Sum(e => e.Length), payloads.Length);
        return Task.FromResult(ResultReference(w));
    }
    public static string ResultReference(WorkspaceRecord w) => "files:" + w.Id + ":result";
    public Task VerifyResultAsync(WorkspaceRecord w, string control, CancellationToken ct = default)
    {
        var result = w.Snapshot?.Result;
        if (result is not { IndependentOfSnapshot: true } || result.ResultReference != ResultReference(w) ||
            result.SnapshotId != w.Snapshot!.Reference?.Id || result.Revision != w.ResultRevision || result.Fingerprint != w.ResultRevision)
            throw Error("The durable independent result does not match this task.");
        VerifyIndependentResult(w, control);
        return Task.CompletedTask;
    }
    // Also used by the authority, with every evidence/object stream pinned by
    // its impersonated guard. No original, mount or VSS read is required here.
    public static void VerifyIndependentResult(WorkspaceRecord w, string control, Func<string, Stream>? open = null)
    {
        Owner(w, control, open);
        var baseline = ReadManifest(w, FilePath(control, "baseline.json"), open);
        var result = ReadManifest(w, FilePath(control, "result.json"), open);
        var intent = Read<FileCollectionIntent>(FilePath(control, "collection.json"), open);
        if (baseline.Identity != w.BaseRevision || result.Identity != w.ResultRevision ||
            intent != new FileCollectionIntent(w.Id, w.RequestDigest!, baseline.Identity, result.Identity))
            throw Error("The preserved basis/result collection differs from the durable task.");
        VerifyPayloads(control, baseline.Entries.Concat(result.Entries), default, open);
    }
    public Task UnregisterAsync(WorkspaceRecord w, string control, CancellationToken ct = default)
    {
        Owner(w, control);
        if (Directory.Exists(w.Path) || File.Exists(w.Path)) throw Error("The mount must be absent before source release.");
        return VerifyResultAsync(w, control, ct);
    }
    public Task<string> ObserveDiscardRevisionAsync(WorkspaceRecord w, string control, CancellationToken ct = default)
    {
        Owner(w, control); RequireBasis(w, control);
        // Identity of the owned source registration. The journal seal freezes
        // acknowledged edits after normal stop; dirty source needs no checkpoint.
        return Task.FromResult(w.BaseRevision!);
    }
    public Task DiscardSourceAsync(WorkspaceRecord w, string control, CancellationToken ct = default)
    {
        Owner(w, control); w.Discard!.RequireMatch(w);
        if (Directory.Exists(w.Path) || File.Exists(w.Path) || w.Discard.Intent.SourceRevision != w.BaseRevision)
            throw Error("Only this unmounted frozen registration may be discarded.");
        SaveFixed(FilePath(control, "discard.json"), new FileDiscardReceipt(w.Discard.Intent, true));
        return Task.CompletedTask;
    }
    public Task VerifyDiscardedAsync(WorkspaceRecord w, string control, CancellationToken ct = default)
    { VerifyDiscarded(w, control); return Task.CompletedTask; }
    public static void VerifyDiscarded(WorkspaceRecord w, string control, Func<string, Stream>? open = null)
    {
        Owner(w, control, open); w.Discard!.RequireMatch(w);
        if (Read<FileDiscardReceipt>(FilePath(control, "discard.json"), open) != new FileDiscardReceipt(w.Discard.Intent, true) ||
            Directory.Exists(w.Path) || File.Exists(w.Path)) throw Error("The completed file source discard receipt is missing.");
    }
    private static FileSourceManifest Manifest(WorkspaceRecord w, FileSourceEntry[] entries) =>
        new(w.Id, w.RequestDigest!, w.Snapshot!.Reference!.Id, Identity(entries), entries);
    private static void RequireBasis(WorkspaceRecord w, string control)
    {
        if (ReadManifest(w, FilePath(control, "baseline.json")).Identity != w.BaseRevision)
            throw Error("The fixed basis identity differs from the durable task.");
    }
    private static string Identity(FileSourceEntry[] entries)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(entries, Json);
        if (bytes.Length > 16 * 1024 * 1024) throw new WorkspaceException("SourceManifestLimit", "The file manifest exceeds 16 MiB; preserve this task.");
        return "sha256:" + Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
    private static FileSourceManifest ReadManifest(WorkspaceRecord w, string path, Func<string, Stream>? open = null)
    {
        var m = Read<FileSourceManifest>(path, open);
        if (m.Version != 1 || m.OwnerId != w.Id || m.RequestDigest != w.RequestDigest || m.SnapshotId != w.Snapshot!.Reference?.Id ||
            m.Entries is null || m.Entries.Length > 32768 || m.Entries.Any(e => !ValidPath(e.Path) ||
                e.Kind is not ("file" or "directory") || e.Length < 0 || e.Kind == "file" && !ValidHash(e.Hash) ||
                e.Kind == "directory" && (e.Length != 0 || e.Hash is not null)) ||
            m.Entries.Select(e => e.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != m.Entries.Length ||
            m.Identity != Identity(m.Entries)) throw Error("The immutable source manifest is corrupt or belongs to another task.");
        return m;
    }
    private static bool ValidHash(string? hash) => hash is { Length: 64 } && hash.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    internal static bool ValidPath(string path) => !string.IsNullOrEmpty(path) && !Path.IsPathRooted(path) &&
        !path.Contains('\\') && !path.Contains(':') && path.Split('/').All(p => p is not ("" or "." or "..") && !VcsMetadata.Contains(p, StringComparer.OrdinalIgnoreCase));
    private static bool Managed(string path, string[] managed) => managed.Any(p => p.StartsWith('*')
        ? path.EndsWith(p[1..], StringComparison.OrdinalIgnoreCase)
        : path.Equals(p, StringComparison.OrdinalIgnoreCase) || path.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase));
    private static void RequireMount(WorkspaceRecord w)
    {
        if (WorkspaceMountAccess.ObservedRoot(w) is null) throw new WorkspaceException("WorkspaceNotReady", "Observe the exact owned mount before source access.");
        Plain(w.Path, w.Path);
    }
    private static void Plain(string path, string? mount = null)
    {
        for (string? current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
            if ((Directory.Exists(current) || File.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0 &&
                (mount is null || !WorkspaceStore.SamePath(current, mount)))
                throw new WorkspaceException("UnsafePath", "Only the exact observed mount root may be a reparse point.");
    }
    private static FileSourceEntry[] Scan(string root, string[] managed, CancellationToken ct, bool metadataOnly = false, string? mount = null, bool omitPlasticRoot = false)
    {
        var entries = new List<FileSourceEntry>();
        long pathBytes = 0;
        var pending = new Stack<string>(); pending.Push(root);
        while (pending.TryPop(out var directory))
        {
            ct.ThrowIfCancellationRequested(); Plain(directory, mount);
            foreach (string path in Directory.EnumerateFileSystemEntries(directory))
            {
                ct.ThrowIfCancellationRequested();
                string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                if (omitPlasticRoot && relative.Equals(".plastic", StringComparison.OrdinalIgnoreCase))
                { WorkspaceStore.EnsurePlainPath(path); if (!Directory.Exists(path)) throw Error("Plastic administration must be a directory."); continue; }
                if (!ValidPath(relative)) throw new WorkspaceException("VcsMetadataUnsupported", "VCS administration and unsupported paths cannot be ordinary file input.");
                Plain(path, mount);
                if (Managed(relative, managed)) continue;
                FileAttributes attributes = File.GetAttributes(path);
                if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Encrypted | FileAttributes.Compressed | FileAttributes.SparseFile)) != 0)
                    throw new WorkspaceException("SnapshotFileUnsupported", "The plain-folder source supports ordinary files and directories only.");
                bool dir = (attributes & FileAttributes.Directory) != 0;
                long length = dir ? 0 : new FileInfo(path).Length;
                string? hash = null;
                if (!dir && !metadataOnly)
                {
                    using var stream = Open(path, mount); hash = Convert.ToHexStringLower(SHA256.HashData(stream));
                    if (stream.Length != length) throw Error("File changed while inspecting its contents.");
                }
                entries.Add(new(relative, dir ? "directory" : "file", length, hash,
                    File.GetLastWriteTimeUtc(path).Ticks, (int)attributes));
                pathBytes += System.Text.Encoding.UTF8.GetByteCount(relative);
                if (entries.Count > 32768 || pathBytes > 1024 * 1024)
                    throw new WorkspaceException("SourceManifestLimit", "The plain-folder manifest supports at most 32768 entries and 1 MiB of UTF-8 paths.");
                if (dir) pending.Push(path);
            }
        }
        return entries.OrderBy(e => e.Path, StringComparer.Ordinal).ToArray();
    }
    private static string[] Changed(FileSourceEntry[] basis, FileSourceEntry[] current)
    {
        var before = basis.ToDictionary(e => e.Path, StringComparer.Ordinal);
        var after = current.ToDictionary(e => e.Path, StringComparer.Ordinal);
        return before.Keys.Union(after.Keys, StringComparer.Ordinal).Where(path =>
            before.GetValueOrDefault(path) != after.GetValueOrDefault(path)).ToArray();
    }
    private static void PreservePayloads(string control, FileSourceManifest manifest, string root, CancellationToken ct, string? mount = null)
    {
        foreach (var entry in UniquePayloads(manifest.Entries))
        {
            ct.ThrowIfCancellationRequested();
            string objectPath = FilePath(control, "objects/" + entry.Hash);
            WorkspaceStore.EnsurePlainPath(objectPath);
            if (Directory.Exists(objectPath)) throw Error("The content object path has an unexpected type.");
            if (File.Exists(objectPath)) { VerifyObject(objectPath, entry, ct); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(objectPath)!);
            string pending = objectPath + ".pending";
            WorkspaceStore.EnsurePlainPath(pending);
            if (!File.Exists(pending))
            {
                using var input = Open(Path.Combine(root, entry.Path.Replace('/', Path.DirectorySeparatorChar)), mount);
                using var output = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                input.CopyTo(output, 65536); output.Flush(true);
            }
            VerifyObject(pending, entry, ct); File.Move(pending, objectPath);
        }
    }
    private static void VerifyObject(string path, FileSourceEntry entry, CancellationToken ct, Func<string, Stream>? open = null)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            using var stream = (open ?? Open)(path);
            if (stream.Length != entry.Length || Convert.ToHexStringLower(SHA256.HashData(stream)) != entry.Hash)
                throw new WorkspaceException("FileResultIncomplete", "Preserve the missing/corrupt independent payload: " + path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { throw new WorkspaceException("FileResultIncomplete", "The independent payload cannot be read; preserve it: " + path); }
    }
    private static IEnumerable<FileSourceEntry> UniquePayloads(IEnumerable<FileSourceEntry> entries)
    {
        var objects = new Dictionary<string, FileSourceEntry>(StringComparer.Ordinal);
        foreach (var entry in entries.Where(e => e.Kind == "file"))
        {
            if (objects.TryGetValue(entry.Hash!, out var prior))
            {
                if (prior.Length != entry.Length) throw Error("One content object cannot have different declared lengths.");
            }
            else objects.Add(entry.Hash!, entry);
        }
        return objects.Values;
    }
    private static void VerifyPayloads(string control, IEnumerable<FileSourceEntry> entries, CancellationToken ct, Func<string, Stream>? open = null)
    { foreach (var entry in UniquePayloads(entries)) VerifyObject(FilePath(control, "objects/" + entry.Hash), entry, ct, open); }
    private static Stream Open(string path)
    { WorkspaceStore.EnsurePlainPath(path); return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 65536); }
    private static Stream Open(string path, string? mount)
    { Plain(path, mount); return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 65536); }
    internal static T Read<T>(string path, Func<string, Stream>? open = null)
    {
        try { using var stream = (open ?? Open)(path);
            if (stream.Length > 16 * 1024 * 1024) throw Error("Preserve the oversized file source receipt.");
            return JsonSerializer.Deserialize<T>(stream, Json) ?? throw Error("Empty file source receipt."); }
        catch (JsonException) { throw Error("Preserve the malformed file source receipt."); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { throw Error("The file source receipt cannot be read; preserve it."); }
    }
    internal static void SaveFixed<T>(string path, T value)
    {
        WorkspaceStore.EnsurePlainPath(path); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value, Json);
        if (bytes.Length > 16 * 1024 * 1024) throw new WorkspaceException("SourceManifestLimit", "The file source receipt exceeds 16 MiB.");
        if (File.Exists(path))
        { if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes)) throw Error("Preserve the different published file source intent."); return; }
        string pending = path + ".pending"; WorkspaceStore.EnsurePlainPath(pending);
        if (File.Exists(pending))
        { if (!File.ReadAllBytes(pending).AsSpan().SequenceEqual(bytes)) throw Error("Preserve the interrupted file source publication."); }
        else { using var stream = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None); stream.Write(bytes); stream.Flush(true); }
        File.Move(pending, path);
    }
    public Task<string> ResolveRevisionAsync(string project, string reference, CancellationToken ct = default) => throw Error("A file basis requires immutable capture.");
    public Task<string> SnapshotKeyAsync(string project, string revision, string[] paths, CancellationToken ct = default) => throw Error("VCS cache baselines are unavailable for plain folders.");
    public Task<SourceWorkspace> CreateAsync(WorkspaceRecord w, CancellationToken ct = default) => throw Error("No checkout or full-copy fallback is available.");
    public Task<IntegrationResult> IntegrateAsync(WorkspaceRecord w, string target, CancellationToken ct = default) => throw new WorkspaceException("SourceIntegrationUnsupported", "Use the original-apply plan and exact intent/digest contract; branch integration cannot authorize original writes.");
    public Task ReleaseAsync(WorkspaceRecord w, CancellationToken ct = default) => throw Error("Use the sealed NTFS release lifecycle.");
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FileSourceOwner([property: JsonRequired] string OwnerId, [property: JsonRequired] string RequestDigest,
    [property: JsonRequired] string ProjectPath, [property: JsonRequired] string Path, [property: JsonRequired] string SourceReference,
    [property: JsonRequired] SnapshotReference Snapshot)
{ [JsonRequired] public int Version { get; init; } = 1; }
public sealed record FileSourceEntry([property: JsonRequired] string Path, [property: JsonRequired] string Kind,
    [property: JsonRequired] long Length, [property: JsonRequired] string? Hash,
    [property: JsonRequired] long LastWriteUtcTicks, [property: JsonRequired] int Attributes);
public sealed record FileSourceManifest([property: JsonRequired] string OwnerId, [property: JsonRequired] string RequestDigest,
    [property: JsonRequired] string SnapshotId, [property: JsonRequired] string Identity, [property: JsonRequired] FileSourceEntry[] Entries)
{ [JsonRequired] public int Version { get; init; } = 1; }
public sealed record FileCollectionIntent([property: JsonRequired] string OwnerId, [property: JsonRequired] string RequestDigest,
    [property: JsonRequired] string BasisIdentity, [property: JsonRequired] string ResultIdentity);
public sealed record FileDiscardReceipt([property: JsonRequired] WorkspaceDiscardIntent Intent, [property: JsonRequired] bool Completed);
