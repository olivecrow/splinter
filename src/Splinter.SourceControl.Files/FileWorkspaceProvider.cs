using System.Security.Cryptography;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Splinter.Core;
using Splinter.Runtime;
using Splinter.SourceControl.Git;

namespace Splinter.SourceControl.Files;

// Content is read at admission/inspection, but only explicit result collection
// stores payloads. This adapter never creates a checkout or invokes a VCS.
public sealed partial class FileWorkspaceProvider(WorkspaceStore store, IOriginalFilePublisher? originalPublisher = null, IPlasticRevisionClient? revisionClient = null,
    IPlasticOriginalGuard? originalGuard = null, IProjectSourceAccess? projectAccess = null,
    ApplicationProjectPolicies? applicationPolicies = null, ISourceChangeProvider? sourceChanges = null,
    IWorkspacePreparationObserver? preparationObserver = null, IJournalFilePins? contentPins = null) : ISourceWorkspaceProvider,
    ISnapshotSourceWorkspaceProvider, ISnapshotCheckpointSourceProvider, ISourceDiscardProvider, IOriginalApplyProvider, IWorkspaceImportSource, ISourceInspectionProofProvider, IWorkspaceGenerationSource, IRetainedResultMaintenance, IWorkspaceMetadataMaintenance
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public static readonly string[] VcsMetadata = [".git", ".plastic", ".hg", ".svn"];
    private string Control(WorkspaceRecord w) => store.ControlPath(w.Id);
    private static string FilePath(string control, string name) => name == "metadata" ? SharedMetadataPool.Location(control)
        : Path.Combine(control, "files", name.Replace('/', Path.DirectorySeparatorChar));
    private static WorkspaceException Error(string message) => new("FileSourceStateInvalid", message);
    private static void Require(WorkspaceRecord w)
    {
        if (w.SourceKind is not ("none" or "plastic" or "git") || !(w.SourceKind == "git" && w.StorageMode == "git-snapshot" && w.Results.Count > 0) && !(w.SourceKind == "plastic" && w.StorageMode == "plastic-snapshot") && !(w.SourceKind == "none" && w.StorageMode == "project-snapshot" && w.GeneratedDirectoryId is not null) || w.Purpose != "task")
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
        PreflightOriginalRoot(w);
        var entries = Scan(w.ProjectPath, w.ManagedPaths, ct, metadataOnly: true, mount: w.StorageMode == "project-snapshot" ? w.ProjectPath : null, omitPlasticRoot: w.SourceKind == "plastic");
        foreach (var entry in entries) RequireNoApplyResources(entry);
        return Task.FromResult("snapshot:" + w.Id);
    }
    private static void PreflightOriginalRoot(WorkspaceRecord w)
    {
        Require(w);
        for (string? path = w.ProjectPath; path is not null; path = Path.GetDirectoryName(path))
            foreach (string name in VcsMetadata)
            {
                if (w.SourceKind == "plastic" && name == ".plastic" && WorkspaceStore.SamePath(path, w.ProjectPath)) continue;
                if (File.Exists(Path.Combine(path, name)) || Directory.Exists(Path.Combine(path, name)))
                    throw new WorkspaceException("VcsMetadataUnsupported", "The plain-folder path cannot contain or inherit VCS administration.");
            }
    }
    private static void RequireNoApplyResources(FileSourceEntry entry)
    {
        if (entry.Path.Split('/').Any(p => p.StartsWith(".splinter-apply-", StringComparison.OrdinalIgnoreCase)))
            throw new WorkspaceException("OriginalApplyResourcesPresent", "Preserve unresolved original publication resources; do not capture them as a new task's source.");
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
        baseline.Dispose();
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
        if (w.Results.Count > 0) return InspectLiveGenerationAsync(w, ct);
        Owner(w, Control(w));
        RequireBasis(w, Control(w));
        RequireMount(w);
        var entries = ScanWorkspace(w, ct);
        return Task.FromResult(Inspect(w, entries, ct));
    }
    private SourceState Inspect(WorkspaceRecord w, IEnumerable<FileSourceEntry> entries, CancellationToken ct)
    { using var current = Manifest(w, entries); return Inspect(w, current, ct); }
    private SourceState Inspect(WorkspaceRecord w, FileSourceManifest current, CancellationToken ct)
    {
        string revision = w.ResultRevision ?? w.BaseRevision!;
        bool changed = current.Identity != revision;
        var basis = ReadManifest(w, FilePath(Control(w), File.Exists(FilePath(Control(w), "result.json")) ? "result.json" : "baseline.json"));
        var changedPaths = changed ? Changed(basis.EnumerateEntries(), current.EnumerateEntries(), Control(w), ct) : (Preview: Array.Empty<string>(), Count: 0L);
        return new SourceState(revision, current.Identity, changed, changedPaths.Preview) { ChangedPathCount = changedPaths.Count };
    }
    public Task<string> CheckpointAsync(WorkspaceRecord w, string message, CancellationToken ct = default)
    {
        Owner(w, Control(w)); RequireMount(w);
        RequireBasis(w, Control(w));
        using var current = Manifest(w, ScanWorkspace(w, ct)); return Task.FromResult(current.Identity);
    }
    public Task<string> PreserveAsync(WorkspaceRecord w, string control, CancellationToken ct = default)
    {
        Owner(w, control); RequireMount(w);
        var baseline = ReadManifest(w, FilePath(control, "baseline.json"));
        if (baseline.Identity != w.BaseRevision) throw Error("The captured basis differs from the durable task.");
        using var result = Manifest(w, ScanWorkspace(w, ct));
        if (result.Identity != w.ResultRevision) throw new WorkspaceException("UncollectedChanges", "The source changed after checkpoint; retain the journal.");
        return Task.FromResult(Preserve(w, control, baseline, result, ct));
    }
    public bool SupportsCheckpointAndPreserve(WorkspaceRecord w) =>
        w.SourceKind == "none" && w.StorageMode == "project-snapshot" || w.SourceKind == "plastic" && w.StorageMode == "plastic-snapshot";

    public Task<SnapshotSourceCheckpoint> CheckpointAndPreserveAsync(WorkspaceRecord w, string control,
        string message, CancellationToken ct = default)
    {
        Owner(w, control); RequireMount(w); RequireBasis(w, control);
        var baseline = ReadManifest(w, FilePath(control, "baseline.json"));
        using var result = Manifest(w, ScanWorkspace(w, ct));
        // The coordinator owns the pre/post seal interval and publishes the
        // revision only after it proves that no acknowledged write overlapped.
        string reference = Preserve(w, control, baseline, result, ct);
        return Task.FromResult(new SnapshotSourceCheckpoint(new(result.Identity, result.Identity, false, []), reference));
    }
    private string Preserve(WorkspaceRecord w, string control, FileSourceManifest baseline,
        FileSourceManifest result, CancellationToken ct)
    {
        SaveFixed(FilePath(control, "collection.json"), new FileCollectionIntent(w.Id, w.RequestDigest!, baseline.Identity, result.Identity));
        if (w.StorageMode == "plastic-snapshot") PreserveRecordedPayloads(w, control, baseline, ct);
        else PreservePayloads(w, control, baseline, ImmutableRoot(w), ct);
        PreservePayloads(w, control, result, w.Path, ct, w.Path);
        // Verify that every payload exists before publishing an independent result.
        VerifyPayloads(control, baseline.EnumeratePayloads(ct).Concat(result.EnumeratePayloads(ct)), ct);
        SaveFixed(FilePath(control, "result.json"), result);
        VerifyIndependentResult(w, control, result.Identity, null);
        w.ResultStorage = Storage(w, baseline, result, new[] { baseline, result }, control);
        PrunePayloadAliases(w, control, baseline, result, ct);
        return ResultReference(w);
    }
    public static string ResultReference(WorkspaceRecord w) => "files:" + w.Id + ":result";
    public Task VerifyResultAsync(WorkspaceRecord w, string control, CancellationToken ct = default)
    {
        if (w.Results.Count > 0) return VerifyGenerationAsync(w, control, w.Results.Last(), ct);
        var result = w.Snapshot?.Result;
        if (result is not { IndependentOfSnapshot: true } || result.ResultReference != ResultReference(w) ||
            result.SnapshotId != w.Snapshot!.Reference?.Id || result.Revision != w.ResultRevision || result.Fingerprint != w.ResultRevision)
            throw Error("The durable independent result does not match this task.");
        using var reader = ContentReader(w, control);
        VerifyIndependentResult(w, control, w.ResultRevision, reader.Reader);
        return Task.CompletedTask;
    }
    // Verify only owned result records and payloads. No original, mount or
    // source-basis read is required here.
    public static void VerifyIndependentResult(WorkspaceRecord w, string control, Func<string, Stream>? open = null)
        => VerifyIndependentResult(w, control, w.ResultRevision, open);

    private static void VerifyIndependentResult(WorkspaceRecord w, string control, string? revision, Func<string, Stream>? open)
    {
        Owner(w, control, open);
        if (w.Results.LastOrDefault() is { } generation)
        {
            if (generation.Revision != revision) throw Error("Select the exact retained result generation.");
            VerifyGenerationCoreAsync(w, control, generation, default, open).GetAwaiter().GetResult(); return;
        }
        var baseline = ReadManifest(w, FilePath(control, "baseline.json"), open);
        var result = ReadManifest(w, FilePath(control, "result.json"), open);
        var intent = Read<FileCollectionIntent>(FilePath(control, "collection.json"), open);
        if (baseline.Identity != w.BaseRevision || result.Identity != revision ||
            intent != new FileCollectionIntent(w.Id, w.RequestDigest!, baseline.Identity, result.Identity))
            throw Error("The preserved basis/result collection differs from the durable task.");
        VerifyPayloads(control, baseline.EnumeratePayloads().Concat(result.EnumeratePayloads()), default, open);
    }
    public async Task UnregisterAsync(WorkspaceRecord w, string control, CancellationToken ct = default,
        Func<CancellationToken, Task>? verifyGeneration = null)
    {
        Owner(w, control);
        if (Directory.Exists(w.Path) || File.Exists(w.Path)) throw Error("The mount must be absent before source release.");
        if (verifyGeneration is null) await VerifyResultAsync(w, control, ct);
        else await verifyGeneration(ct);
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
    private FileSourceManifest Manifest(WorkspaceRecord w, IEnumerable<FileSourceEntry> entries)
    {
        // Input can perform slow native reads and complete blob hashes. Finish
        // it in bounded operation-owned scratch before taking the home-wide
        // metadata writer lock; peers can scan/hash their own input concurrently.
        using var input = new MetadataSpool(FilePath(Control(w), "scratch"));
        foreach (var entry in entries)
        {
            if (!ValidPath(entry.Path) || entry.Kind is not ("file" or "directory") || entry.Length < 0 ||
                entry.Kind == "file" && entry.Hash is not null && !ValidHash(entry.Hash) ||
                entry.Kind == "directory" && (entry.Length != 0 || entry.Hash is not null))
                throw Error("The source membership contains unsupported metadata.");
            input.Append(new(entry.Path, FileApplyIndex.EncodeTemporary(entry)));
        }
        using var pool = new SharedMetadataPool(store.Root, w.ProjectPath + "\n" + w.SourceKind + "\n" + w.ApplicationKind + "\n" + string.Join('\n', w.ManagedPaths), filePins: contentPins);
        string directory = FilePath(Control(w), "metadata");
        var root = new SnapshotEntry("", true, 0, 16, 0, 0, 0);
        IEnumerable<DirectoryMetadataRecord> Records()
        {
            foreach (var row in input.Read())
            {
                var entry = FileApplyIndex.DecodeTemporary(row) ?? throw Error("The source membership contains a deletion marker.");
                yield return new(new(entry.Path, entry.Kind == "directory", entry.Length, (uint)entry.Attributes,
                    0, 0, new DateTime(entry.LastWriteUtcTicks, DateTimeKind.Utc).ToFileTimeUtc(), entry.UnixMode), entry.Hash);
            }
        }
        FileSourceManifest? previous = null;
        string previousPath = w.Results.LastOrDefault() is { } generation ? GenerationPath(Control(w), generation.Id, "result.json") : FilePath(Control(w), "baseline.json");
        if (File.Exists(previousPath)) previous = ReadManifest(w, previousPath);
        var shared = previous is null ? pool.Previous() : null;
        var catalog = PagedSnapshotCatalog.Build(directory, root, Records(), previousCatalog: previous?.Catalog ?? shared?.Catalog, filePins: contentPins);
        var manifest = new FileSourceManifest(w.Id, w.RequestDigest!, w.Snapshot!.Reference!.Id, "sha256:" + catalog.Children.ContentHash, [])
            { Version = 4, Catalog = catalog, Directory = directory };
        using var pages = new PagedMetadataStore(directory, true, filePins: contentPins);
        var payloadNames = new PagedPathInterner(pages, previous?.PayloadPaths ?? shared?.PayloadPaths);
        payloadNames.Import(manifest.EnumerateEntries().Select(entry => entry.Path));
        IEnumerable<MetadataEntry> Payloads()
        {
            foreach (var entry in UniquePayloads(manifest.EnumerateEntries(), Control(w)))
                yield return new(entry.Hash!, FileApplyIndex.Encode(entry, payloadNames, alreadyImported: true));
        }
        var payloadIndex = pages.Build(Payloads(), previousRoot: previous?.PayloadIndex ?? shared?.PayloadIndex); pages.Flush();
        pool.Publish(catalog, payloadIndex, payloadNames.Root);
        return manifest with { PayloadIndex = payloadIndex, PayloadPaths = payloadNames.Root,
            AnchorLease = pool.Anchor(catalog, payloadIndex, payloadNames.Root) };
    }
    private static void RequireBasis(WorkspaceRecord w, string control)
    {
        if (ReadManifest(w, FilePath(control, "baseline.json")).Identity != w.BaseRevision)
            throw Error("The fixed basis identity differs from the durable task.");
    }
    private static FileSourceManifest ReadManifest(WorkspaceRecord w, string path, Func<string, Stream>? open = null,
        PagedMetadataStore.CatalogReadScope? catalogs = null, CancellationToken ct = default)
    {
        var m = Read<FileSourceManifest>(path, open);
        if (m.Version != 4 || m.OwnerId != w.Id || m.RequestDigest != w.RequestDigest || m.SnapshotId != w.Snapshot!.Reference?.Id ||
            m.Catalog is null || m.PayloadIndex is null || m.PayloadPaths is null || m.Entries.Length != 0 || m.Identity != "sha256:" + m.Catalog.Children.ContentHash)
            throw Error("The immutable source manifest is corrupt or belongs to another task.");
        string? directory = Path.GetDirectoryName(path);
        while (directory is not null && !Path.GetFileName(directory).Equals("files", StringComparison.Ordinal)) directory = Path.GetDirectoryName(directory);
        if (directory is null) throw Error("The source manifest path is outside its owned records.");
        m.Directory = FilePath(Path.GetDirectoryName(directory)!, "metadata");
        if (catalogs is not null) catalogs.Verify(m.Catalog, ct);
        else { using var catalog = new PagedSnapshotCatalog(m.Directory, m.Catalog); catalog.Verify(ct); }
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
    private static void Plain(string path, string? mount = null, FileAttributes? leafAttributes = null)
    {
        for (string? current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
        {
            FileAttributes attributes;
            try { attributes = leafAttributes ?? File.GetAttributes(current); }
            catch (FileNotFoundException) { leafAttributes = null; continue; }
            catch (DirectoryNotFoundException) { leafAttributes = null; continue; }
            leafAttributes = null;
            if ((attributes & FileAttributes.ReparsePoint) != 0 && (mount is null || !WorkspaceStore.SamePath(current, mount)))
                throw new WorkspaceException("UnsafePath", "Only the exact observed mount root may be a reparse point.");
        }
    }
    // Plastic's fixed reader keeps a single revision proof/stream. Interleaving
    // files would repeatedly evict it and decode the same revision in full.
    private static IEnumerable<FileSourceEntry> ScanWorkspace(WorkspaceRecord w, CancellationToken ct) =>
        Scan(w.Path, w.ManagedPaths, ct, mount: w.Path, hashWorkers: w.SourceKind == "plastic" ? 1 : StorageResources.HashWorkers);
    private static IEnumerable<FileSourceEntry> Scan(string root, string[] managed, CancellationToken ct, bool metadataOnly = false, string? mount = null, bool omitPlasticRoot = false,
        int hashWorkers = 0, bool omitGitRoot = false)
    {
        // Final immutable catalogs own sorting, so independent hashes can finish
        // out of order without a slow file holding a whole batch at a barrier.
        if (metadataOnly) return Metadata();
        return BoundedParallel.Select(Metadata(), HashEntry, hashWorkers == 0 ? StorageResources.HashWorkers : hashWorkers, ct);
        FileSourceEntry HashEntry(FileSourceEntry entry, CancellationToken token)
        {
            if (entry.Kind != "file") return entry;
            string path = Path.Combine(root, entry.Path.Replace('/', Path.DirectorySeparatorChar));
            using var stream = Open(path, mount); string hash = PayloadHash.Sha256(stream, token);
            var current = new FileInfo(path);
            FileAttributes currentAttributes = current.Attributes;
            if (stream.Length != entry.Length || !current.Exists || current.Length != entry.Length ||
                current.LastWriteTimeUtc.Ticks != entry.LastWriteUtcTicks || (int)currentAttributes != entry.Attributes)
                throw Error("File changed while inspecting its contents.");
            return entry with { Hash = hash };
        }
        IEnumerable<FileSourceEntry> Metadata()
        {
            var pending = new Stack<IEnumerator<FileSystemInfo>>();
            Plain(root, mount); pending.Push(new DirectoryInfo(root).EnumerateFileSystemInfos().GetEnumerator());
            try
            {
                while (pending.TryPeek(out var enumerator))
                {
                    ct.ThrowIfCancellationRequested();
                    if (!enumerator.MoveNext()) { enumerator.Dispose(); pending.Pop(); continue; }
                    FileSystemInfo info = enumerator.Current;
                    string path = info.FullName, relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                    if (omitGitRoot && relative.Equals(".git", StringComparison.OrdinalIgnoreCase)) { Plain(path, mount); continue; }
                    if (omitPlasticRoot && relative.Equals(".plastic", StringComparison.OrdinalIgnoreCase))
                    { Plain(path, mount); if (!Directory.Exists(path)) throw Error("Plastic administration must be a directory."); continue; }
                    if (!ValidPath(relative)) throw new WorkspaceException("VcsMetadataUnsupported", "VCS administration and unsupported paths cannot be ordinary file input.");
                    if (Managed(relative, managed)) continue;
                    // Enumeration metadata can already be stale. Refresh the
                    // current leaf once, then use that one observation for its
                    // type/length/time and the first component of the path walk.
                    info.Refresh();
                    FileAttributes attributes = info.Attributes;
                    if (!info.Exists) throw new FileNotFoundException("Source entry disappeared during inspection.", path);
                    Plain(path, mount, attributes);
                    if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Encrypted | FileAttributes.Compressed | FileAttributes.SparseFile)) != 0)
                        throw new WorkspaceException("SnapshotFileUnsupported", "The plain-folder source supports ordinary files and directories only.");
                    bool dir = (attributes & FileAttributes.Directory) != 0;
                    yield return new(relative, dir ? "directory" : "file", dir ? 0 : ((FileInfo)info).Length, null, info.LastWriteTimeUtc.Ticks, (int)attributes);
                    if (dir) pending.Push(new DirectoryInfo(path).EnumerateFileSystemInfos().GetEnumerator());
                }
            }
            finally { foreach (var enumerator in pending) enumerator.Dispose(); }
        }
    }
    internal static (string[] Preview, long Count) Changed(IEnumerable<FileSourceEntry> basis, IEnumerable<FileSourceEntry> current, string control, CancellationToken ct)
    {
        using var work = new FileApplyIndex(FilePath(control, "inspection-work"));
        var before = work.Build(basis, ct); var after = work.Build(current, ct);
        IEnumerable<string> Differences()
        {
            foreach (var pair in work.Compare(before, after))
            {
                ct.ThrowIfCancellationRequested();
                if (pair.Before != pair.After) yield return (pair.After ?? pair.Before)!.Path;
            }
        }
        var changed = work.BuildImportedPaths(Differences(), ct);
        return (work.Paths(changed).Take(100).ToArray(), changed.Count);
    }
    private void PreservePayloads(WorkspaceRecord w, string control, FileSourceManifest manifest, string root, CancellationToken ct, string? mount = null)
    {
        ForEachPayload(manifest.EnumeratePayloads(ct), ct, entry =>
        {
            ct.ThrowIfCancellationRequested();
            if (contentPins is not null)
            {
                new OwnedContentStore(store.Root, contentPins).Preserve(w, control, entry.Hash!, entry.Length, output =>
                {
                    using var input = Open(Path.Combine(root, entry.Path.Replace('/', Path.DirectorySeparatorChar)), mount);
                    PreserveStream(input, output, entry.Length, ct);
                }, ct);
                return;
            }
            string objectPath = FilePath(control, "objects/" + entry.Hash);
            WorkspaceStore.EnsurePlainPath(objectPath);
            if (Directory.Exists(objectPath)) throw Error("The content object path has an unexpected type.");
            if (File.Exists(objectPath)) { VerifyObject(objectPath, entry, ct); return; }
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
        });
    }
    private static void PreserveStream(Stream input, FileStream output, long length, CancellationToken ct)
    {
        if (output.Length > length) throw Error("Preserve an oversized incomplete content payload.");
        byte[] expected = new byte[65536], prior = new byte[65536]; long position = 0;
        while (position < length)
        {
            ct.ThrowIfCancellationRequested(); int count = input.Read(expected, 0, (int)Math.Min(expected.Length, length - position));
            if (count == 0) throw Error("The fixed payload ended early.");
            if (position < output.Length)
            {
                int retained = (int)Math.Min(count, output.Length - position); output.Position = position; output.ReadExactly(prior.AsSpan(0, retained));
                if (!expected.AsSpan(0, retained).SequenceEqual(prior.AsSpan(0, retained))) throw Error("The interrupted immutable payload changed.");
            }
            output.Position = position; output.Write(expected, 0, count); position += count;
        }
    }
    internal static void ForEachPayload(IEnumerable<FileSourceEntry> entries, CancellationToken ct, Action<FileSourceEntry> action)
    {
        try
        {
            Parallel.ForEach(entries, new ParallelOptions { MaxDegreeOfParallelism = StorageResources.IoWorkers, CancellationToken = ct }, action);
        }
        // Join all active copies/flushes before publishing failure or allowing
        // retry/reclamation. Preserve the product error code, not a TPL wrapper.
        catch (AggregateException ex) { ExceptionDispatchInfo.Capture(ex.Flatten().InnerExceptions[0]).Throw(); throw; }
    }
    private static void VerifyObject(string path, FileSourceEntry entry, CancellationToken ct, Func<string, Stream>? open = null)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            using var stream = (open ?? Open)(path);
            if (stream.Length != entry.Length || PayloadHash.Sha256(stream, ct) != entry.Hash)
                throw new WorkspaceException("FileResultIncomplete", "Preserve the missing/corrupt independent payload: " + path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { throw new WorkspaceException("FileResultIncomplete", "The independent payload cannot be read; preserve it: " + path); }
    }
    private static IEnumerable<FileSourceEntry> UniquePayloads(IEnumerable<FileSourceEntry> entries, string control, CancellationToken ct = default)
    {
        IEnumerable<MetadataEntry> Encoded()
        {
            foreach (var entry in entries)
            {
                ct.ThrowIfCancellationRequested();
                if (entry.Kind != "file") continue;
                byte[] path = System.Text.Encoding.UTF8.GetBytes(entry.Path); byte[] data = new byte[checked(8 + path.Length)];
                System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(data, entry.Length); path.CopyTo(data, 8);
                yield return new((entry.Hash ?? throw Error("An immutable file digest is missing.")) + "/" + entry.Path, data);
            }
        }
        string? previous = null; long length = 0;
        foreach (var entry in MetadataSort.Order(Encoded(), FilePath(control, "scratch"), ct))
        {
            long size = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(entry.Value.Span);
            string hash = entry.Key[..64];
            if (hash == previous)
            { if (length != size) throw Error("One content object cannot have different declared lengths."); continue; }
            previous = hash; length = size;
            string path = System.Text.Encoding.UTF8.GetString(entry.Value.Span[8..]);
            yield return new(path, "file", size, hash, DateTime.FromFileTimeUtc(0).Ticks, 32);
        }
    }
    private SourceResultStorage Storage(WorkspaceRecord w, FileSourceManifest baseline, FileSourceManifest result, IEnumerable<FileSourceManifest> retained, string control, CancellationToken ct = default,
        PagedMetadataStore.CatalogReadScope? catalogs = null)
    {
        IEnumerable<MetadataEntry> Objects()
        {
            foreach (var manifest in retained)
            foreach (var reference in ContentReferences(w, control, manifest, ct))
            {
                byte[] size = new byte[8]; System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(size, reference.Length);
                // Equal hashes in separate physical payload pools remain two
                // retained objects until their references actually share one
                // backing. This is logical required-content accounting; native
                // allocation/overlapping raw ranges are measured separately.
                yield return new((reference.RawRange ? "r" : "w") + reference.Hash, size);
            }
        }
        // The complete reference stream is validated before exposing statistics.
        // Small sets avoid sort/spill; large sets keep the shared-budget disk path.
        using var required = new MetadataMembership(Objects(), FilePath(control, "stats-work"), ct);
        long bytes = 0, count = 0;
        foreach (var item in required.Enumerate(ct))
        {
            long size = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(item.Value.Span);
            bytes = checked(bytes + size); count++;
        }
        long Total(FileSourceManifest manifest)
        {
            if (catalogs is not null) return catalogs.Verify(manifest.Catalog!, ct).Bytes;
            long total = 0;
            foreach (var entry in manifest.EnumerateEntries())
            { ct.ThrowIfCancellationRequested(); total = checked(total + entry.Length); }
            return total;
        }
        return new(Total(baseline), Total(result), bytes, count);
    }
    private static void VerifyPayloads(string control, IEnumerable<FileSourceEntry> entries, CancellationToken ct, Func<string, Stream>? open = null, bool concurrentOpen = false,
        bool uniqueRetainedRoot = false, ResultFileContentStore.VerificationScope? verification = null)
    {
        // RetainedPayloads is an authenticated hash-keyed tree with one length
        // per hash. Its decoder validates every row; sorting it again adds no proof.
        var payloads = uniqueRetainedRoot ? entries : UniquePayloads(entries, control, ct);
        if (verification is not null)
        { ForEachPayload(payloads, ct, entry => verification.Verify(entry.Hash!, entry.Length, ct)); return; }
        // Injected readers can have sequential lifetime/observation contracts.
        if (open is not null && !concurrentOpen)
            foreach (var entry in payloads) VerifyObject(FilePath(control, "objects/" + entry.Hash), entry, ct, open);
        else ForEachPayload(payloads, ct, entry => VerifyObject(FilePath(control, "objects/" + entry.Hash), entry, ct, open));
    }

    private ContentReadScope ContentReader(WorkspaceRecord w, string control, MetadataTreeRoot? contentFiles = null,
        GitRetainedResultBasisScope? retainedGitBasis = null, FileSourceManifest? retainedBasis = null, CancellationToken ct = default)
    {
        if (contentPins is null) return new(null, null, null, null);
        var raw = new OwnedRangeContentStore(store.Root, contentPins);
        GitRetainedResultBasisScope? gitBasis = retainedGitBasis, ownedGitBasis = null;
        FileSourceManifest? basis = retainedBasis, ownedBasis = null;
        FileSourceManifest.ReadScope? basisCatalog = null;
        try
        {
            if (w.SourceKind == "git")
            {
                if (gitBasis is null) gitBasis = ownedGitBasis = GitRetainedResultBasis.Open(w, control, contentPins);
                if (basis is null) basis = ownedBasis = ReadManifest(w, FilePath(control, "generation-basis.json"));
                basisCatalog = basis.OpenReader(contentPins);
            }
            Stream Basis(string hash, long? length)
            { var entry = basisCatalog!.FindPayload(hash, length); return new SnapshotPayloadStream(gitBasis!, entry.Path, entry.Length); }
            var content = new ResultFileContentStore(store.Root, control, w, contentPins, raw, contentFiles, gitBasis is null ? null : Basis);
            Stream Reader(string path)
            {
                if (!WorkspaceStore.SamePath(Path.GetDirectoryName(path)!, FilePath(control, "objects"))) return Open(path);
                string hash = Path.GetFileName(path);
                if (!ValidHash(hash)) throw Error("The independent content alias name is invalid.");
                return content.Open(hash, ct: ct);
            }
            return new(Reader, raw, ownedGitBasis, ownedBasis, gitBasis, basisCatalog, content.BeginVerification());
        }
        catch { raw.Dispose(); ownedGitBasis?.Dispose(); basisCatalog?.Dispose(); ownedBasis?.Dispose(); throw; }
    }
    private sealed class ContentReadScope(Func<string, Stream>? reader, OwnedRangeContentStore? raw,
        GitRetainedResultBasisScope? gitBasis, FileSourceManifest? basis, GitRetainedResultBasisScope? boundaryBasis = null,
        FileSourceManifest.ReadScope? basisCatalog = null, ResultFileContentStore.VerificationScope? verification = null) : IDisposable
    {
        public Func<string, Stream>? Reader => reader;
        public ResultFileContentStore.VerificationScope? Verification => verification;
        public void VerifyBoundary() => (boundaryBasis ?? gitBasis)?.VerifyReadBoundary();
        public void Dispose()
        {
            try { verification?.Dispose(); gitBasis?.Dispose(); }
            finally { try { raw?.Dispose(); } finally { try { basisCatalog?.Dispose(); } finally { basis?.Dispose(); } } }
        }
    }
    private static Stream Open(string path)
    { WorkspaceStore.EnsurePlainPath(path); return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 65536); }
    private static Stream Open(string path, string? mount)
    { Plain(path, mount); return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 65536); }
    internal static T Read<T>(string path, Func<string, Stream>? open = null)
    {
        try { using var stream = (open ?? Open)(path);
            return JsonSerializer.Deserialize<T>(stream, Json) ?? throw Error("Empty file source receipt."); }
        catch (JsonException) { throw Error("Preserve the malformed file source receipt."); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { throw Error("The file source receipt cannot be read; preserve it."); }
    }
    internal static void SaveFixed<T>(string path, T value)
    {
        WorkspaceStore.EnsurePlainPath(path); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path))
        {
            if (value is FileSourceManifest manifest)
            {
                var prior = Read<FileSourceManifest>(path);
                if (prior.Version != manifest.Version || prior.OwnerId != manifest.OwnerId || prior.RequestDigest != manifest.RequestDigest ||
                    prior.SnapshotId != manifest.SnapshotId || prior.Identity != manifest.Identity || prior.Catalog is null || manifest.Catalog is null ||
                    prior.Catalog.Count != manifest.Catalog.Count || prior.Catalog.CaseSensitive != manifest.Catalog.CaseSensitive ||
                    prior.Catalog.Children.ContentHash != manifest.Catalog.Children.ContentHash ||
                    prior.RetainedPayloads?.ContentHash != manifest.RetainedPayloads?.ContentHash ||
                    prior.ContentFiles?.ContentHash != manifest.ContentFiles?.ContentHash)
                    throw Error("Preserve the different published source catalog.");
                using var catalog = new PagedSnapshotCatalog(manifest.Directory!, prior.Catalog); catalog.Verify(); return;
            }
            using var publishedControl = Open(path); using var comparison = new FixedControlComparison(publishedControl);
            JsonSerializer.Serialize(comparison, value, Json);
            if (!comparison.Matches) throw Error("Preserve the different published file source intent."); return;
        }
        // An unpublished/truncated attempt has no root authority. Preserve it
        // and choose a fresh name inside this same fixed request on retry.
        string pending = path + "." + Guid.NewGuid().ToString("N") + ".tmp"; WorkspaceStore.EnsurePlainPath(pending);
        using (var stream = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { JsonSerializer.Serialize(stream, value, Json); stream.Flush(true); }
        AtomicFilePublication.Publish(pending, path);
    }
    private sealed class FixedControlComparison(Stream prior) : Stream
    {
        private readonly byte[] buffer = new byte[65536]; private bool equal = true;
        public bool Matches => equal && prior.Position == prior.Length;
        public override void Write(ReadOnlySpan<byte> value)
        {
            while (!value.IsEmpty)
            {
                int count = Math.Min(value.Length, buffer.Length), read = 0;
                while (read < count) { int next = prior.Read(buffer, read, count - read); if (next == 0) { equal = false; return; } read += next; }
                if (!buffer.AsSpan(0, count).SequenceEqual(value[..count])) equal = false;
                value = value[count..];
            }
        }
        public override void Write(byte[] value, int offset, int count) => Write(value.AsSpan(offset, count));
        public override bool CanRead => false; public override bool CanSeek => false; public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException(); public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { } public override int Read(byte[] value, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long value, SeekOrigin origin) => throw new NotSupportedException(); public override void SetLength(long value) => throw new NotSupportedException();
    }
    public Task<string> ResolveRevisionAsync(string project, string reference, CancellationToken ct = default) => throw Error("A file basis requires immutable capture.");
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
    [property: JsonRequired] long LastWriteUtcTicks, [property: JsonRequired] int Attributes,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] uint? UnixMode = null);
public sealed record FileSourceManifest([property: JsonRequired] string OwnerId, [property: JsonRequired] string RequestDigest,
    [property: JsonRequired] string SnapshotId, [property: JsonRequired] string Identity, FileSourceEntry[] Entries) : IDisposable
{
    [JsonRequired] public int Version { get; init; } = 4;
    [JsonRequired] public MetadataTreeRoot? ContentFiles { get; set; }
    [JsonRequired] public MetadataTreeRoot? RetainedPayloads { get; set; }
    [JsonRequired] public SnapshotCatalogDescriptor? Catalog { get; init; }
    [JsonRequired] public MetadataTreeRoot? PayloadIndex { get; init; }
    [JsonRequired] public MetadataTreeRoot? PayloadPaths { get; init; }
    [JsonIgnore] public FileSourceEntry[] Entries { get; init; } = Entries ?? [];
    [JsonIgnore] public string? Directory { get; set; }
    [JsonIgnore] internal FileStream? AnchorLease { get; init; }
    [JsonIgnore] internal FileStream? RetentionLease { get; set; }
    public void Dispose() { try { AnchorLease?.Dispose(); } finally { RetentionLease?.Dispose(); } }
    internal IEnumerable<FileSourceEntry> EnumerateRetainedPayloads(CancellationToken ct = default, IJournalFilePins? pins = null)
    {
        if (RetainedPayloads is null)
        { foreach (var entry in EnumeratePayloads(ct, pins)) yield return entry; yield break; }
        using var pages = new PagedMetadataStore(Directory!, filePins: pins);
        foreach (var item in pages.Open(RetainedPayloads).Enumerate())
        {
            ct.ThrowIfCancellationRequested();
            if (item.Key.Length != 64 || item.Key.Any(c => !char.IsAsciiHexDigit(c) || char.IsUpper(c)) || item.Value.Length != 8 || item.Child is not null)
                throw new WorkspaceException("FileSourceStateInvalid", "Invalid retained payload identity.");
            long length = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(item.Value.Span);
            if (length < 0) throw new WorkspaceException("FileSourceStateInvalid", "Invalid retained payload length.");
            yield return new(item.Key, "file", length, item.Key, DateTime.FromFileTimeUtc(0).Ticks, 32);
        }
    }
    public IEnumerable<FileSourceEntry> EnumerateEntries()
    {
        if (Catalog is null || Directory is null) throw new WorkspaceException("FileSourceStateInvalid", "Bind the source catalog before enumerating.");
        using var reader = new PagedSnapshotCatalog(Directory, Catalog);
        foreach (var item in reader.Walk())
            if (item.Entry.Path.Length != 0) yield return FromRecord(item);
    }
    public FileSourceEntry? Find(string path)
    {
        if (Catalog is null || Directory is null) throw new WorkspaceException("FileSourceStateInvalid", "Bind the source catalog before lookup.");
        using var reader = new PagedSnapshotCatalog(Directory, Catalog); var item = reader.Find(path); return item is null ? null : FromRecord(item);
    }
    public FileSourceEntry FindPayload(string hash, long? length = null)
    {
        using var reader = OpenReader(); return reader.FindPayload(hash, length);
    }
    internal ReadScope OpenReader(IJournalFilePins? pins = null) => new(this, pins);
    internal IEnumerable<FileSourceEntry> EnumeratePayloads(CancellationToken ct = default, IJournalFilePins? pins = null)
    {
        using var reader = OpenReader(pins);
        foreach (var entry in reader.EnumeratePayloads(ct)) yield return entry;
    }
    // One operation may consult many immutable hashes. Keep its catalog and
    // page readers under the same bounded process cache instead of reopening
    // the store and discarding those readers for every file. Every lookup still
    // proves payload-index membership against the fixed directory catalog.
    internal sealed class ReadScope : IDisposable
    {
        private readonly PagedMetadataStore pages;
        private readonly PagedSnapshotCatalog catalog;
        private readonly IReadOnlyMetadataTree payloads;
        private readonly PagedPathInterner names;
        internal ReadScope(FileSourceManifest manifest, IJournalFilePins? pins)
        {
            if (manifest.PayloadIndex is null || manifest.PayloadPaths is null || manifest.Catalog is null || manifest.Directory is null)
                throw new WorkspaceException("FileSourceStateInvalid", "The source payload index is missing.");
            pages = new(manifest.Directory, filePins: pins); PagedSnapshotCatalog? opened = null;
            try
            {
                catalog = opened = new(pages, manifest.Catalog);
                payloads = pages.Open(manifest.PayloadIndex); names = new(pages, manifest.PayloadPaths);
            }
            catch { opened?.Dispose(); pages.Dispose(); throw; }
        }
        public FileSourceEntry? Find(string path)
        { var record = catalog.Find(path); return record is null ? null : FromRecord(record); }
        public FileSourceEntry FindPayload(string hash, long? length = null)
        {
            var raw = payloads.Find(hash) ?? throw new WorkspaceException("FileSourceStateInvalid", "The immutable payload is absent from its result index.");
            var indexed = FileApplyIndex.Decode(raw, names); var entry = Find(indexed.Path);
            if (entry?.Hash != hash || length is not null && entry.Length != length || indexed.Hash != hash || indexed.Length != entry.Length)
                throw new WorkspaceException("FileSourceStateInvalid", "The payload index differs from its immutable source path.");
            return entry;
        }
        public IEnumerable<FileSourceEntry> EnumeratePayloads(CancellationToken ct)
        {
            // The stored index is an accelerator, not authority for omitting
            // source files. Prove coverage against the fixed catalog on every
            // read scope, without sorting all paths again. Bound the repeated
            // hash memo; eviction only repeats the membership proof.
            var covered = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var record in catalog.Walk())
            {
                ct.ThrowIfCancellationRequested(); if (record.Entry.Directory) continue;
                string hash = record.ContentHash ?? throw new WorkspaceException("FileSourceStateInvalid", "A catalog file has no payload identity.");
                if (covered.TryGetValue(hash, out long length))
                { if (length != record.Entry.Length) throw new WorkspaceException("FileSourceStateInvalid", "One payload has different lengths."); }
                else
                {
                    _ = FindPayload(hash, record.Entry.Length);
                    if (covered.Count == 64) covered.Clear(); covered.Add(hash, record.Entry.Length);
                }
            }
            foreach (var item in payloads.Enumerate())
            {
                ct.ThrowIfCancellationRequested();
                // Reject extra/misdirected index entries as well as omissions.
                yield return FindPayload(item.Key);
            }
        }
        public void Dispose() { try { catalog.Dispose(); } finally { pages.Dispose(); } }
    }
    private static FileSourceEntry FromRecord(DirectoryMetadataRecord record) => new(record.Entry.Path, record.Entry.Directory ? "directory" : "file",
        record.Entry.Length, record.ContentHash, DateTime.FromFileTimeUtc(record.Entry.LastWriteTime).Ticks, (int)record.Entry.Attributes, record.Entry.UnixMode);
}
public sealed record FileCollectionIntent([property: JsonRequired] string OwnerId, [property: JsonRequired] string RequestDigest,
    [property: JsonRequired] string BasisIdentity, [property: JsonRequired] string ResultIdentity);
public sealed record FileDiscardReceipt([property: JsonRequired] WorkspaceDiscardIntent Intent, [property: JsonRequired] bool Completed);
