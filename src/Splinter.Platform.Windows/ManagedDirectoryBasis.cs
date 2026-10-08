using System.Security.Cryptography;
using System.Text.Json;
using Splinter.Core;
using Splinter.Runtime;
using Splinter.SourceControl.Git;

namespace Splinter.Platform.Windows;

public sealed class ManagedDirectoryState
{
    public int Version { get; set; } = 2;
    public string ApplicationKind { get; set; } = "";
    public string DirectoryKind { get; set; } = "";
    public required string Key { get; set; }
    public required GeneratedDirectoryReference Basis { get; set; }
    public required string OriginalPath { get; set; }
    public required string CompatibilityKey { get; set; }
    public string Phase { get; set; } = "preparing";
    public OverlayMount? Mount { get; set; }
    public long InitialPayloadCopiedBytes { get; set; }
}

// Explicit lifecycle for a moved immutable generated subtree or VCS-free project tree.
// The application owns normal Editor stop/start; this class owns only its mount.
public sealed class ManagedDirectoryBasis(WorkspaceStore store, ApplicationProjectPolicies policies, bool projectBasis = false) : IGeneratedDirectoryDependencies, IProjectSourceAccess
{
    private GeneratedDirectoryDescriptor Descriptor(IApplicationProjectPolicy policy)
    {
        var descriptor = projectBasis ? new GeneratedDirectoryDescriptor("Project", "") : policy.GeneratedDirectory
            ?? throw new WorkspaceException("GeneratedBasisUnsupported", "Compose an application policy with an explicit generated directory.");
        string relativePath = descriptor.RelativePath ?? ""; bool validPath;
        try { validPath = descriptor.RelativePath is not null && JournalDeltaStore.Normalize(relativePath) == relativePath; }
        catch (ArgumentException) { validPath = false; }
        if (string.IsNullOrWhiteSpace(descriptor.Kind) || descriptor.Kind.Length > 64 || descriptor.Kind.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-') || !validPath ||
            !projectBasis && (relativePath.Length == 0 || relativePath.Contains('/') || relativePath.Contains('\\') ||
                Splinter.SourceControl.Files.FileWorkspaceProvider.VcsMetadata.Contains(relativePath, StringComparer.OrdinalIgnoreCase)))
            throw new WorkspaceException("GeneratedBasisUnsupported", "Use one ordinary top-level generated directory with an explicit descriptor identity.");
        return descriptor;
    }
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private string Root(string id) { if (id.Length != 24 || id.Any(c => !char.IsAsciiHexDigit(c))) throw new WorkspaceException("InvalidWorkspaceId", "Use the exact basis id."); return Path.Combine(store.Root, projectBasis ? "project-bases" : "library-bases", id); }
    private string StatePath(string id) => Path.Combine(Root(id), "state.json");
    private string RequestPath(string id) => Path.Combine(Root(id), "overlay", "request.json");
    private Task<FileStream> Gate(string id, CancellationToken ct) => store.LockAsync(WorkspaceStore.Key("generated:" + id), ct);
    public string NormalizeProject(string project)
    {
        string path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(project));
        WorkspaceStore.EnsurePlainPath(projectBasis ? Path.GetDirectoryName(path)! : path);
        return path;
    }
    private static void RequireNoVcs(string project)
    {
        for (string? path = project; path is not null; path = Path.GetDirectoryName(path))
            foreach (string name in Splinter.SourceControl.Files.FileWorkspaceProvider.VcsMetadata)
                if (File.Exists(Path.Combine(path, name)) || Directory.Exists(Path.Combine(path, name)))
                    throw new WorkspaceException("VcsMetadataUnsupported", "Managed project preparation requires a VCS-free source and ancestors.");
        foreach (string path in Directory.EnumerateFileSystemEntries(project, "*", SearchOption.AllDirectories))
        {
            WorkspaceStore.EnsurePlainPath(path);
            if (Splinter.SourceControl.Files.FileWorkspaceProvider.VcsMetadata.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase) ||
                Path.GetFileName(path).StartsWith(".splinter-apply-", StringComparison.OrdinalIgnoreCase))
                throw new WorkspaceException("VcsMetadataUnsupported", "Preserve administration or incomplete publication data outside the managed project basis.");
        }
    }
    public IDisposable Acquire(WorkspaceRecord workspace, bool publishing, CancellationToken ct = default)
    {
        if (!projectBasis || workspace.StorageMode != "project-snapshot" || workspace.SourceKind != "none" || workspace.GeneratedDirectoryId is null)
            throw new WorkspaceException("ProjectBasisMismatch", "An exact VCS-free managed project basis is required.");
        var state = GetAsync(workspace.GeneratedDirectoryId, ct).GetAwaiter().GetResult();
        if (state.ApplicationKind != workspace.ApplicationKind || !WorkspaceStore.SamePath(state.Basis.ProjectPath, workspace.ProjectPath) || workspace.GeneratedDirectory is { } prior && prior != state.Basis)
            throw new WorkspaceException("ProjectBasisMismatch", "Preserve the fixed original project reference.");
        return AcquireOriginal(state, publishing);
    }
    public IDisposable? AcquireOriginal(string project)
    {
        string folder = Path.Combine(store.Root, "project-bases");
        if (!Directory.Exists(folder)) return null;
        var states = Directory.EnumerateDirectories(folder).Select(p => GetAsync(Path.GetFileName(p)).GetAwaiter().GetResult())
            .Where(s => WorkspaceStore.SamePath(s.Basis.ProjectPath, project) && s.Phase != "restored").ToArray();
        if (states.Length == 0) return null;
        if (states.Length != 1) throw new WorkspaceException("ProjectBasisMismatch", "Preserve ambiguous original project ownership.");
        return AcquireOriginal(states[0], false);
    }
    private IDisposable AcquireOriginal(ManagedDirectoryState state, bool publishing)
    {
        if (state.Phase != "ready") throw new WorkspaceException("ProjectBasisNotReady", "Resume the exact original preparation before accessing its view.");
        var lease = new OwnedDirectorySnapshotProvider(state.Basis.ReceiptDirectory, state.Basis.OwnerId).AcquireReadLease();
        IDisposable? editor = null, mount = null;
        try
        {
            var request = WinFspOverlayClient.ReadRequestAsync(RequestPath(state.Basis.Id), default).GetAwaiter().GetResult(); ValidateRequest(state, request);
            var observed = WinFspOverlayClient.ObserveAsync(request, default).GetAwaiter().GetResult();
            if (!observed.GetProperty("ownershipVerified").GetBoolean() || !observed.GetProperty("processAlive").GetBoolean() ||
                observed.GetProperty("phase").GetString() != "mounted" || !Directory.Exists(state.OriginalPath))
                throw new WorkspaceException("ProjectOriginalMountRequired", "Reconnect the exact original managed view; no mutable-source fallback is available.");
            mount = ManagedMountIdentity.Acquire(state.OriginalPath, state.Basis.Id);
            if (publishing) editor = RequireStopped(policies.Get(state.ApplicationKind), state.Basis.ProjectPath, state.OriginalPath);
            return new OriginalLease(lease, editor, mount);
        }
        catch { mount?.Dispose(); editor?.Dispose(); lease.Dispose(); throw; }
    }
    private sealed class OriginalLease(IDisposable? basis, IDisposable? editor, IDisposable mount) : IDisposable
    {
        private IDisposable? basisLease = basis, editorLease = editor, mountLease = mount;
        public void Dispose()
        {
            var application = Interlocked.Exchange(ref editorLease, null);
            var root = Interlocked.Exchange(ref mountLease, null);
            var source = Interlocked.Exchange(ref basisLease, null);
            try { application?.Dispose(); }
            finally { try { root?.Dispose(); } finally { source?.Dispose(); } }
        }
    }
    public async Task<ManagedDirectoryState> GetAsync(string id, CancellationToken ct = default)
    {
        var state = await WindowsProcessWorkspaceProvider.ReadAsync<ManagedDirectoryState>(StatePath(id), ct)
            ?? throw new WorkspaceException("GeneratedBasisMissing", "Retain the missing generated-directory identity.");
        if (state.Version != 2 || string.IsNullOrWhiteSpace(state.ApplicationKind))
            throw new WorkspaceException("GeneratedBasisMismatch", "Preserve an unsupported generated-directory state without migration.");
        var descriptor = Descriptor(policies.Get(state.ApplicationKind));
        var basis = state.Basis;
        if (state.DirectoryKind != descriptor.Kind || basis.Id != id || basis.OwnerId != id || basis.RelativePath != descriptor.RelativePath ||
            !WorkspaceStore.SamePath(basis.ReceiptDirectory, Root(id)) || !WorkspaceStore.SamePath(basis.Snapshot.RootPath, Path.Combine(Root(id), "base")) ||
            !WorkspaceStore.SamePath(state.OriginalPath, Path.Combine(basis.ProjectPath, basis.RelativePath)) || basis.Snapshot.Provider != OwnedDirectorySnapshotTree.ProviderName ||
            basis.Snapshot.Id != id || !basis.Snapshot.Persistent || !basis.Snapshot.Differential || basis.Snapshot.Consistency != "stopped-original-owned-directory" ||
            !WorkspaceStore.SamePath(basis.Snapshot.SourcePath, basis.Snapshot.RootPath) || basis.ManifestHash.Length != 64 || basis.ManifestHash.Any(c => !char.IsAsciiHexDigit(c)) ||
            basis.SourceMetadataHash is { } metadataHash && (projectBasis || state.ApplicationKind != "unity" ||
                metadataHash.Length != 64 || metadataHash.Any(c => !char.IsAsciiHexDigit(c) || char.IsUpper(c))) ||
            string.IsNullOrWhiteSpace(state.Key) || state.Key.Length > 128 || string.IsNullOrWhiteSpace(state.CompatibilityKey) || state.CompatibilityKey.Length > 256 ||
            id != WorkspaceStore.Key(WorkspaceStore.ProjectKey(basis.ProjectPath) + "\n" + descriptor.Kind + "\n" + state.Key) ||
            state.Phase is not ("preparing" or "ready" or "restoring" or "restored"))
            throw new WorkspaceException("GeneratedBasisMismatch", "Preserve an unsupported generated-directory state.");
        return state;
    }
    public async Task<ManagedDirectoryState> PrepareAsync(string project, string key, string applicationKind, CancellationToken ct = default)
    {
        project = NormalizeProject(project);
        var policy = policies.Get(applicationKind); var descriptor = Descriptor(policy);
        string compatibilityKey = policy.CompatibilityKey(project);
        if (string.IsNullOrWhiteSpace(compatibilityKey) || compatibilityKey.Length > 256)
            throw new WorkspaceException("ApplicationPolicyUnsupported", "The application policy must provide an explicit bounded compatibility identity.");
        if (string.IsNullOrWhiteSpace(key) || key.Length > 128) throw new WorkspaceException("InvalidRequestKey", "Use a stable generated-directory key.");
        string id = WorkspaceStore.Key(WorkspaceStore.ProjectKey(project) + "\n" + descriptor.Kind + "\n" + key);
        using var gate = await Gate(id, ct);
        string root = Root(id), original = Path.Combine(project, descriptor.RelativePath), basisPath = Path.Combine(root, "base");
        if (!OperatingSystem.IsWindows() || new DriveInfo(Path.GetPathRoot(project)!).DriveFormat != "NTFS" ||
            !WorkspaceStore.SamePath(Path.GetPathRoot(project)!, Path.GetPathRoot(root)!))
            throw new WorkspaceException("NtfsVolumeRequired", "The generated basis must be renamed on the same existing NTFS volume.");
        if (root.StartsWith(project + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            project.StartsWith(store.Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new WorkspaceException("UnsafeStateRoot", "Generated state and the original project must be separate trees.");
        ManagedDirectoryState state;
        if (File.Exists(StatePath(id)))
        {
            state = await GetAsync(id, ct);
            if (state.ApplicationKind != applicationKind || state.Key != key || state.CompatibilityKey != compatibilityKey || !WorkspaceStore.SamePath(state.Basis.ProjectPath, project))
                throw new WorkspaceException("RequestKeyConflict", "Reuse the original generated-directory parameters.");
            if (state.Phase == "restored") return state;
            if (state.Phase == "restoring") throw new WorkspaceException("GeneratedBasisRestorePending", "Resume the same restoration first.");
        }
        else
        {
            using var editorGuard = RequireStopped(policy, project);
            if (projectBasis) RequireNoVcs(project);
            WorkspaceStore.EnsurePlainPath(original); WorkspaceStore.EnsurePlainPath(root);
            if (!Directory.Exists(original) || Directory.Exists(basisPath))
                throw new WorkspaceException("GeneratedBasisPreparationConflict", "An intact original without a renamed basis is required.");
            string intentPath = Path.Combine(root, "prepare-intent.json");
            var intent = new PreparationIntent(2, id, project, key, original, basisPath, applicationKind, descriptor.Kind, compatibilityKey);
            if (Directory.Exists(root))
            {
                var prior = await WindowsProcessWorkspaceProvider.ReadAsync<PreparationIntent>(intentPath, ct);
                if (prior != intent || Directory.EnumerateFileSystemEntries(root).Any(p => Path.GetFileName(p) is not ("prepare-intent.json" or "manifest.json" or "proof.json" or "readers.lock" or "dependents" or GitSourceMetadata.FileName)) ||
                    Directory.Exists(Path.Combine(root, "dependents")) && Directory.EnumerateFileSystemEntries(Path.Combine(root, "dependents")).Any())
                    throw new WorkspaceException("GeneratedBasisPreparationConflict", "Retain an unowned or uncertain preparation root.");
                foreach (string entry in Directory.EnumerateFileSystemEntries(root)) WorkspaceStore.EnsurePlainPath(entry);
            }
            string metadataPath = Path.Combine(root, GitSourceMetadata.FileName);
            GeneratedDirectoryReference basis;
            if (File.Exists(metadataPath))
            {
                // A failed state publication can leave a complete captured map.
                // Reuse its exact paired Library proof under the same intent;
                // never overwrite an unknown map merely because its name fits.
                if (projectBasis || applicationKind != "unity")
                    throw new WorkspaceException("GeneratedBasisPreparationConflict", "Source metadata has no owner in this preparation.");
                var saved = await WindowsProcessWorkspaceProvider.ReadAsync<OwnedDirectoryManifest>(Path.Combine(root, "manifest.json"), ct)
                    ?? throw new WorkspaceException("GeneratedBasisPreparationConflict", "Preserve source metadata without its Library manifest.");
                var fixedSnapshot = saved.Snapshot;
                if (saved.Version != 1 || saved.OwnerId != id || fixedSnapshot is null || fixedSnapshot.Id != id || fixedSnapshot.Provider != OwnedDirectorySnapshotTree.ProviderName ||
                    fixedSnapshot.SourcePath != basisPath || fixedSnapshot.RootPath != basisPath || !fixedSnapshot.Persistent || !fixedSnapshot.Differential ||
                    fixedSnapshot.Consistency != "stopped-original-owned-directory" || fixedSnapshot.PathPolicy is not null)
                    throw new WorkspaceException("GeneratedBasisPreparationConflict", "Preserve a foreign captured Library/source pair.");
                if (!File.Exists(Path.Combine(root, "proof.json")))
                    throw new WorkspaceException("GeneratedBasisPreparationConflict", "Preserve source metadata without its paired Library proof.");
                string manifestHash = HashFile(Path.Combine(root, "manifest.json"));
                _ = new OwnedDirectorySnapshotProvider(root, id).Read(fixedSnapshot);
                basis = new(id, project, descriptor.RelativePath, fixedSnapshot, root, id, manifestHash, HashFile(metadataPath));
                _ = GitSourceMetadata.Read(basis, project);
            }
            else
            {
                SnapshotInputChecks.VerifyTree(original, ct, false, false);
                Directory.CreateDirectory(root);
                if (!File.Exists(intentPath)) await WindowsProcessWorkspaceProvider.WriteAsync(intentPath, intent, ct);
                var snapshot = new SnapshotReference(id, OwnedDirectorySnapshotTree.ProviderName, basisPath, basisPath, DateTimeOffset.UtcNow, true, true, "stopped-original-owned-directory");
                var manifest = OwnedDirectorySnapshotTree.Capture(snapshot with { RootPath = original }, id) with { Snapshot = snapshot };
                await WindowsProcessWorkspaceProvider.WriteAsync(Path.Combine(root, "manifest.json"), manifest, ct);
                string manifestHash = HashFile(Path.Combine(root, "manifest.json"));
                await WindowsProcessWorkspaceProvider.WriteAsync(Path.Combine(root, "proof.json"), new OwnedDirectoryProof(1, snapshot, id, manifestHash, manifest.Items.Sum(i => i.Entry.Length)), ct);
                basis = new(id, project, descriptor.RelativePath, snapshot, root, id, manifestHash);
                if (!projectBasis && applicationKind == "unity")
                {
                    var metadata = await WindowsGitSourceMetadata.CaptureAsync(project, basis, ct);
                    if (metadata is not null)
                    {
                        await WindowsProcessWorkspaceProvider.WriteAsync(metadataPath, metadata, ct);
                        basis = basis with { SourceMetadataHash = HashFile(metadataPath) };
                        _ = GitSourceMetadata.Read(basis, project);
                    }
                }
            }
            using (var readers = new FileStream(Path.Combine(root, "readers.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)) { if (readers.Length != 0) throw new WorkspaceException("GeneratedBasisPreparationConflict", "Retain an inconsistent reader lock."); readers.Flush(true); }
            Directory.CreateDirectory(Path.Combine(root, "dependents"));
            state = new() { ApplicationKind = applicationKind, DirectoryKind = descriptor.Kind, Key = key, OriginalPath = original, CompatibilityKey = compatibilityKey,
                Basis = basis };
            await WindowsProcessWorkspaceProvider.WriteAsync(StatePath(id), state, ct);
        }
        if (state.Phase == "preparing")
        {
            if (state.Basis.SourceMetadataHash is not null) _ = GitSourceMetadata.Read(state.Basis, project);
            using var editorGuard = RequireStopped(policy, project);
            if (Directory.Exists(original) && Directory.Exists(basisPath)) throw new WorkspaceException("GeneratedBasisPreparationConflict", "Both original and basis exist; retain both.");
            if (!Directory.Exists(basisPath)) { WorkspaceStore.EnsurePlainPath(original); if (projectBasis) editorGuard?.Dispose(); Directory.Move(original, basisPath); }
            // The mount client verifies and fences the renamed basis before
            // creating its delta. Keep that single verification boundary rather
            // than opening/hash-reading every basis file a second time here.
        }
        var request = await WindowsProcessWorkspaceProvider.ReadAsync<OverlayServeRequest>(RequestPath(id), ct);
        if (request is null)
        {
            var access = await new OwnedDirectorySnapshotProvider(root, id).ResolveReadAccessAsync(state.Basis.Snapshot, ct);
            request = new(new(2, id, state.Basis.Snapshot, SnapshotWorkspaceState.DefaultJournalBytes, 1024 * 1024,
                ExcludedTopLevel: projectBasis ? Splinter.SourceControl.Files.FileWorkspaceProvider.VcsMetadata : null,
                DeniedTopLevel: projectBasis ? Splinter.SourceControl.Files.FileWorkspaceProvider.VcsMetadata : null), Path.Combine(root, "delta"), original,
                Path.Combine(root, "overlay"), Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)), access, root, id);
            Directory.CreateDirectory(request.ControlPath);
            await WindowsProcessWorkspaceProvider.WriteAsync(RequestPath(id), request, ct);
        }
        ValidateRequest(state, request);
        state.Mount = await new WinFspOverlayMountProvider(RequestPath(id), Path.Combine(store.Root, "runners")).MountAsync(request.Identity, request.DeltaPath, request.MountPath, ct);
        state.Phase = "ready"; await WindowsProcessWorkspaceProvider.WriteAsync(StatePath(id), state, ct); return state;
    }
    public async Task<GeneratedDirectoryReference> AttachAsync(string id, WorkspaceRecord workspace, CancellationToken ct = default)
    {
        using var gate = await Gate(id, ct); var state = await GetAsync(id, ct);
        if (state.Phase != "ready" || state.ApplicationKind != workspace.ApplicationKind || !WorkspaceStore.SamePath(state.Basis.ProjectPath, workspace.ProjectPath) ||
            workspace.GeneratedDirectoryId != id || workspace.GeneratedDirectory is { } prior && prior != state.Basis)
            throw new WorkspaceException("GeneratedBasisMismatch", "Only the exact live generated basis may be attached to this project/task.");
        if (projectBasis && (workspace.SourceKind != "none" || workspace.StorageMode != "project-snapshot"))
            throw new WorkspaceException("ProjectBasisMismatch", "Use the basis's exact source and application contract.");
        if (state.Basis.SourceMetadataHash is not null) _ = GitSourceMetadata.Read(state.Basis, workspace.ProjectPath);
        var request = await WinFspOverlayClient.ReadRequestAsync(RequestPath(id), ct); ValidateRequest(state, request);
        var observed = await WinFspOverlayClient.ObserveAsync(request, ct);
        if (!observed.GetProperty("ownershipVerified").GetBoolean() || !observed.GetProperty("processAlive").GetBoolean() || observed.GetProperty("phase").GetString() != "mounted")
            throw new WorkspaceException("GeneratedBasisOriginalMountRequired", "Restore the original's exact managed mount before attaching a task.");
        string dependent = Path.Combine(Root(id), "dependents", workspace.Id + ".json");
        var receipt = new GeneratedDependency(workspace.Id, workspace.RequestDigest!, state.Basis);
        var saved = await WindowsProcessWorkspaceProvider.ReadAsync<GeneratedDependency>(dependent, ct);
        if (saved is not null && saved != receipt) throw new WorkspaceException("GeneratedBasisMismatch", "Retain an inconsistent dependent receipt.");
        if (saved is null) await WindowsProcessWorkspaceProvider.WriteAsync(dependent, receipt, ct);
        return state.Basis;
    }
    public async Task DetachAsync(GeneratedDirectoryReference basis, WorkspaceRecord workspace, CancellationToken ct = default)
    {
        using var gate = await Gate(basis.Id, ct); var state = await GetAsync(basis.Id, ct);
        if (state.ApplicationKind != workspace.ApplicationKind || state.Basis != basis)
            throw new WorkspaceException("GeneratedBasisMismatch", "Retain a changed dependent basis or application identity.");
        string control = store.ControlPath(workspace.Id);
        if (Directory.Exists(workspace.Path) || Directory.Exists(Path.Combine(control, "delta")))
            throw new WorkspaceException("GeneratedBasisInUse", "Remove the owned mount and delta before detaching its generated basis.");
        var request = await WinFspOverlayClient.ReadRequestAsync(Path.Combine(control, "overlay", "request.json"), ct);
        var observed = await WinFspOverlayClient.ObserveAsync(request, ct);
        if (!observed.GetProperty("ownershipVerified").GetBoolean() || observed.GetProperty("processAlive").GetBoolean())
            throw new WorkspaceException("GeneratedBasisInUse", "Observe the owned provider absent before detaching.");
        string path = Path.Combine(Root(basis.Id), "dependents", workspace.Id + ".json");
        var receipt = await WindowsProcessWorkspaceProvider.ReadAsync<GeneratedDependency>(path, ct);
        if (receipt is not null && receipt != new GeneratedDependency(workspace.Id, workspace.RequestDigest!, basis))
            throw new WorkspaceException("GeneratedBasisMismatch", "Retain another dependent receipt.");
        if (receipt is not null) File.Delete(path);
    }
    public async Task<ManagedDirectoryState> RestoreAsync(string id, CancellationToken ct = default)
    {
        using var gate = await Gate(id, ct); var state = await GetAsync(id, ct);
        if (state.Phase == "restored") return state;
        if (state.Phase is not ("preparing" or "ready" or "restoring")) throw new WorkspaceException("GeneratedBasisNotReady", "Retain an unknown preparation/restoration stage.");
        if (Directory.EnumerateFileSystemEntries(Path.Combine(Root(id), "dependents")).Any() ||
            store.List(state.Basis.ProjectPath).Any(w => w.GeneratedDirectoryId == id && w.Phase is not ("released" or "discarded") && Directory.Exists(Path.Combine(store.ControlPath(w.Id), "delta"))))
            throw new WorkspaceException("GeneratedBasisInUse", "Retain all active task references before restoring the original.");
        using var editorGuard = RequireStoppedForState(state);
        if (state.Phase == "preparing")
        {
            // Explicitly undo an unpublished transition. No mount/worker was
            // exposed. Move the current original data intact; never reconstruct
            // it from a stale manifest or delete an uncertain launched provider.
            string root = Root(id), overlay = Path.Combine(root, "overlay"), delta = Path.Combine(root, "delta");
            if (state.Mount is not null || File.Exists(Path.Combine(overlay, "provider-launch-intent.json")) || File.Exists(Path.Combine(overlay, "provider-launch-receipt.json")))
                throw new WorkspaceException("GeneratedPreparationUncertain", "Reconcile the exact provider launch before undoing this preparation.");
            using var readerGate = new FileStream(Path.Combine(root, "readers.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            if (Directory.Exists(delta))
            {
                var draftRequest = await WinFspOverlayClient.ReadRequestAsync(RequestPath(id), ct); ValidateRequest(state, draftRequest);
                foreach (string entry in Directory.EnumerateFileSystemEntries(delta))
                {
                    WorkspaceStore.EnsurePlainPath(entry);
                    if (Directory.Exists(entry) || Path.GetFileName(entry) is not ("identity.json" or "delta.journal") || Path.GetFileName(entry) == "delta.journal" && new FileInfo(entry).Length != 0)
                        throw new WorkspaceException("GeneratedPreparationUncertain", "Retain nonempty or unclassified preparation data.");
                }
                var draftIdentity = await WindowsProcessWorkspaceProvider.ReadAsync<OverlayIdentity>(Path.Combine(delta, "identity.json"), ct);
                if (draftIdentity != draftRequest.Identity)
                    throw new WorkspaceException("GeneratedPreparationUncertain", "Retain an unverified preparation delta identity.");
            }
            bool originalPresent = Directory.Exists(state.OriginalPath), basePresent = Directory.Exists(state.Basis.Snapshot.RootPath);
            if (originalPresent == basePresent) throw new WorkspaceException("GeneratedRestoreConflict", "Exactly one intact original or renamed basis is required.");
            WorkspaceStore.EnsurePlainPath(state.OriginalPath); WorkspaceStore.EnsurePlainPath(state.Basis.Snapshot.RootPath);
            await WindowsProcessWorkspaceProvider.WriteAsync(Path.Combine(root, "abort-preparation.json"), new { state.Basis.Id, state.OriginalPath, state.Basis.Snapshot.RootPath }, ct);
            if (projectBasis) editorGuard?.Dispose();
            if (basePresent) Directory.Move(state.Basis.Snapshot.RootPath, state.OriginalPath);
            if (Directory.Exists(delta)) { foreach (string entry in Directory.EnumerateFileSystemEntries(delta)) File.Delete(entry); Directory.Delete(delta, false); }
            state.Phase = "restored"; await WindowsProcessWorkspaceProvider.WriteAsync(StatePath(id), state, ct); return state;
        }
        var request = await WinFspOverlayClient.ReadRequestAsync(RequestPath(id), ct); ValidateRequest(state, request);
        if (projectBasis) editorGuard?.Dispose();
        var priorObservation = await WinFspOverlayClient.ObserveAsync(request, ct);
        if (state.Phase != "restoring" || priorObservation.GetProperty("processAlive").GetBoolean() || !priorObservation.GetProperty("ownershipVerified").GetBoolean())
            await WinFspOverlayClient.StopAsync(request, ct);
        var observed = await WinFspOverlayClient.ObserveAsync(request, ct);
        if (!observed.GetProperty("ownershipVerified").GetBoolean() || observed.GetProperty("processAlive").GetBoolean() ||
            state.Phase == "ready" && Directory.Exists(state.OriginalPath))
            throw new WorkspaceException("GeneratedBasisInUse", "Observe the original provider and mount absent before restoration.");
        using var readers = new FileStream(Path.Combine(Root(id), "readers.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        state.Phase = "restoring"; await WindowsProcessWorkspaceProvider.WriteAsync(StatePath(id), state, ct);
        await ManagedDirectoryRestore.ExecuteAsync(state, request, ct);
        state.Mount = null; state.Phase = "restored"; await WindowsProcessWorkspaceProvider.WriteAsync(StatePath(id), state, ct); return state;
    }
    private IDisposable? RequireStoppedForState(ManagedDirectoryState state)
    {
        if (projectBasis && state.Phase is "ready" or "restoring" && Directory.Exists(state.OriginalPath))
        {
            var mount = ManagedMountIdentity.Acquire(state.OriginalPath, state.Basis.Id);
            try { return new OriginalLease(null, RequireStopped(policies.Get(state.ApplicationKind), state.Basis.ProjectPath, state.OriginalPath), mount); }
            catch { mount.Dispose(); throw; }
        }
        return RequireStopped(policies.Get(state.ApplicationKind), state.Basis.ProjectPath);
    }
    private static IDisposable? RequireStopped(IApplicationProjectPolicy policy, string project, string? verifiedManagedRoot = null)
    {
        try { return policy.AcquireOriginal(project, verifiedManagedRoot: verifiedManagedRoot); }
        catch (IOException) { throw new WorkspaceException("OriginalEditorStopRequired", "Save and normally stop the original application before changing its managed mount."); }
    }
    private static void ValidateRequest(ManagedDirectoryState state, OverlayServeRequest request)
    {
        bool project = state.Basis.RelativePath == "";
        var expected = new OverlayIdentity(2, state.Basis.Id, state.Basis.Snapshot, SnapshotWorkspaceState.DefaultJournalBytes, 1024 * 1024,
            ExcludedTopLevel: project ? Splinter.SourceControl.Files.FileWorkspaceProvider.VcsMetadata : null,
            DeniedTopLevel: project ? Splinter.SourceControl.Files.FileWorkspaceProvider.VcsMetadata : null);
        if (request.Identity.Id != state.Basis.Id || request.Identity.Snapshot != state.Basis.Snapshot ||
            JsonSerializer.Serialize(request.Identity) != JsonSerializer.Serialize(expected) ||
            request.MountPath != state.OriginalPath || request.SnapshotOwnerId != state.Basis.OwnerId ||
            request.SnapshotReceiptDirectory != state.Basis.ReceiptDirectory || request.DeltaPath != Path.Combine(state.Basis.ReceiptDirectory, "delta") ||
            request.ControlPath != Path.Combine(state.Basis.ReceiptDirectory, "overlay") || request.Identity.Graft is not null || request.SourceView is not null)
            throw new WorkspaceException("GeneratedBasisMismatch", "Retain an inconsistent original mount request.");
    }
    internal static string HashFile(string path) { using var f = File.OpenRead(path); return Convert.ToHexStringLower(SHA256.HashData(f)); }
    private sealed record GeneratedDependency(string WorkspaceId, string RequestDigest, GeneratedDirectoryReference Basis);
    private sealed record PreparationIntent(int Version, string Id, string ProjectPath, string Key, string OriginalPath, string BasisPath,
        string ApplicationKind, string DirectoryKind, string CompatibilityKey);
}
