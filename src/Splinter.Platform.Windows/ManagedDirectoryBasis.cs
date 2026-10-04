using System.Security.Cryptography;
using System.Text.Json;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Platform.Windows;

public sealed class ManagedDirectoryState
{
    public int Version { get; set; } = 1;
    public required string Key { get; set; }
    public required GeneratedDirectoryReference Basis { get; set; }
    public required string OriginalPath { get; set; }
    public required string CompatibilityKey { get; set; }
    public string Phase { get; set; } = "preparing";
    public OverlayMount? Mount { get; set; }
    public long InitialPayloadCopiedBytes { get; set; }
}

// Explicit lifecycle for a moved, immutable application-generated directory.
// The application owns normal Editor stop/start; this class owns only its mount.
public sealed class ManagedDirectoryBasis(WorkspaceStore store) : IGeneratedDirectoryDependencies
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private string Root(string id) { if (id.Length != 24 || id.Any(c => !char.IsAsciiHexDigit(c))) throw new WorkspaceException("InvalidWorkspaceId", "Use the exact generated-directory id."); return Path.Combine(store.Root, "library-bases", id); }
    private string StatePath(string id) => Path.Combine(Root(id), "state.json");
    private string RequestPath(string id) => Path.Combine(Root(id), "overlay", "request.json");
    private Task<FileStream> Gate(string id, CancellationToken ct) => store.LockAsync(WorkspaceStore.Key("generated:" + id), ct);
    public async Task<ManagedDirectoryState> GetAsync(string id, CancellationToken ct = default)
    {
        var state = await WindowsProcessWorkspaceProvider.ReadAsync<ManagedDirectoryState>(StatePath(id), ct)
            ?? throw new WorkspaceException("GeneratedBasisMissing", "Retain the missing generated-directory identity.");
        var basis = state.Basis;
        if (state.Version != 1 || basis.Id != id || basis.OwnerId != id || basis.RelativePath != "Library" ||
            !WorkspaceStore.SamePath(basis.ReceiptDirectory, Root(id)) || !WorkspaceStore.SamePath(basis.Snapshot.RootPath, Path.Combine(Root(id), "base")) ||
            !WorkspaceStore.SamePath(state.OriginalPath, Path.Combine(basis.ProjectPath, basis.RelativePath)) || basis.Snapshot.Provider != OwnedDirectorySnapshotTree.ProviderName ||
            basis.Snapshot.Id != id || !basis.Snapshot.Persistent || !basis.Snapshot.Differential || basis.Snapshot.Consistency != "stopped-original-owned-directory" ||
            !WorkspaceStore.SamePath(basis.Snapshot.SourcePath, basis.Snapshot.RootPath) || basis.ManifestHash.Length != 64 || basis.ManifestHash.Any(c => !char.IsAsciiHexDigit(c)) ||
            string.IsNullOrWhiteSpace(state.Key) || state.Key.Length > 128 || id != WorkspaceStore.Key(WorkspaceStore.ProjectKey(basis.ProjectPath) + "\nLibrary\n" + state.Key) ||
            state.Phase is not ("preparing" or "ready" or "restoring" or "restored"))
            throw new WorkspaceException("GeneratedBasisMismatch", "Preserve an unsupported generated-directory state.");
        return state;
    }
    public async Task<ManagedDirectoryState> PrepareAsync(string project, string key, string compatibilityKey, CancellationToken ct = default)
    {
        project = WorkspaceStore.CanonicalProject(project);
        if (string.IsNullOrWhiteSpace(key) || key.Length > 128) throw new WorkspaceException("InvalidRequestKey", "Use a stable generated-directory key.");
        string id = WorkspaceStore.Key(WorkspaceStore.ProjectKey(project) + "\nLibrary\n" + key);
        using var gate = await Gate(id, ct);
        string root = Root(id), original = Path.Combine(project, "Library"), basisPath = Path.Combine(root, "base");
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
            if (state.Key != key || state.CompatibilityKey != compatibilityKey || !WorkspaceStore.SamePath(state.Basis.ProjectPath, project))
                throw new WorkspaceException("RequestKeyConflict", "Reuse the original generated-directory parameters.");
            if (state.Phase == "restored") return state;
            if (state.Phase == "restoring") throw new WorkspaceException("GeneratedBasisRestorePending", "Resume the same restoration first.");
        }
        else
        {
            using var editorGuard = RequireStopped(project);
            WorkspaceStore.EnsurePlainPath(original); WorkspaceStore.EnsurePlainPath(root);
            if (!Directory.Exists(original) || Directory.Exists(basisPath))
                throw new WorkspaceException("GeneratedBasisPreparationConflict", "An intact original without a renamed basis is required.");
            string intentPath = Path.Combine(root, "prepare-intent.json");
            var intent = new PreparationIntent(1, id, project, key, original, basisPath, compatibilityKey);
            if (Directory.Exists(root))
            {
                var prior = await WindowsProcessWorkspaceProvider.ReadAsync<PreparationIntent>(intentPath, ct);
                if (prior != intent || Directory.EnumerateFileSystemEntries(root).Any(p => Path.GetFileName(p) is not ("prepare-intent.json" or "manifest.json" or "proof.json" or "readers.lock" or "dependents")) ||
                    Directory.Exists(Path.Combine(root, "dependents")) && Directory.EnumerateFileSystemEntries(Path.Combine(root, "dependents")).Any())
                    throw new WorkspaceException("GeneratedBasisPreparationConflict", "Retain an unowned or uncertain preparation root.");
                foreach (string entry in Directory.EnumerateFileSystemEntries(root)) WorkspaceStore.EnsurePlainPath(entry);
            }
            SnapshotInputChecks.VerifyTree(original, ct, false, false);
            Directory.CreateDirectory(root);
            if (!File.Exists(intentPath)) await WindowsProcessWorkspaceProvider.WriteAsync(intentPath, intent, ct);
            var snapshot = new SnapshotReference(id, OwnedDirectorySnapshotTree.ProviderName, basisPath, basisPath, DateTimeOffset.UtcNow, true, true, "stopped-original-owned-directory");
            var manifest = OwnedDirectorySnapshotTree.Capture(snapshot with { RootPath = original }, id) with { Snapshot = snapshot };
            await WindowsProcessWorkspaceProvider.WriteAsync(Path.Combine(root, "manifest.json"), manifest, ct);
            string manifestHash = HashFile(Path.Combine(root, "manifest.json"));
            await WindowsProcessWorkspaceProvider.WriteAsync(Path.Combine(root, "proof.json"), new OwnedDirectoryProof(1, snapshot, id, manifestHash, manifest.Items.Sum(i => i.Entry.Length)), ct);
            using (var readers = new FileStream(Path.Combine(root, "readers.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)) { if (readers.Length != 0) throw new WorkspaceException("GeneratedBasisPreparationConflict", "Retain an inconsistent reader lock."); readers.Flush(true); }
            Directory.CreateDirectory(Path.Combine(root, "dependents"));
            state = new() { Key = key, OriginalPath = original, CompatibilityKey = compatibilityKey,
                Basis = new(id, project, "Library", snapshot, root, id, manifestHash) };
            await WindowsProcessWorkspaceProvider.WriteAsync(StatePath(id), state, ct);
        }
        if (state.Phase == "preparing")
        {
            using var editorGuard = RequireStopped(project);
            if (Directory.Exists(original) && Directory.Exists(basisPath)) throw new WorkspaceException("GeneratedBasisPreparationConflict", "Both original and basis exist; retain both.");
            if (!Directory.Exists(basisPath)) { WorkspaceStore.EnsurePlainPath(original); Directory.Move(original, basisPath); }
            // The mount client verifies and fences the renamed basis before
            // creating its delta. Keep that single verification boundary rather
            // than opening/hash-reading every basis file a second time here.
        }
        var request = await WindowsProcessWorkspaceProvider.ReadAsync<OverlayServeRequest>(RequestPath(id), ct);
        if (request is null)
        {
            var access = await new OwnedDirectorySnapshotProvider(root, id).ResolveReadAccessAsync(state.Basis.Snapshot, ct);
            request = new(new(2, id, state.Basis.Snapshot, SnapshotWorkspaceState.DefaultJournalBytes, 1024 * 1024), Path.Combine(root, "delta"), original,
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
        if (state.Phase != "ready" || !WorkspaceStore.SamePath(state.Basis.ProjectPath, workspace.ProjectPath) ||
            workspace.GeneratedDirectoryId != id || workspace.GeneratedDirectory is { } prior && prior != state.Basis)
            throw new WorkspaceException("GeneratedBasisMismatch", "Only the exact live generated basis may be attached to this project/task.");
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
        if (state.Basis != basis) throw new WorkspaceException("GeneratedBasisMismatch", "Retain a changed dependent basis.");
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
        using var editorGuard = RequireStopped(state.Basis.ProjectPath);
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
            if (basePresent) Directory.Move(state.Basis.Snapshot.RootPath, state.OriginalPath);
            if (Directory.Exists(delta)) { foreach (string entry in Directory.EnumerateFileSystemEntries(delta)) File.Delete(entry); Directory.Delete(delta, false); }
            state.Phase = "restored"; await WindowsProcessWorkspaceProvider.WriteAsync(StatePath(id), state, ct); return state;
        }
        var request = await WinFspOverlayClient.ReadRequestAsync(RequestPath(id), ct); ValidateRequest(state, request);
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
    private static IDisposable? RequireStopped(string project)
    {
        string path = Path.Combine(project, "Temp", "UnityLockfile");
        if (!File.Exists(path)) return null;
        try { return File.OpenHandle(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException) { throw new WorkspaceException("OriginalEditorStopRequired", "Save and normally stop the original Editor before changing its Library mount."); }
    }
    private static void ValidateRequest(ManagedDirectoryState state, OverlayServeRequest request)
    {
        if (request.Identity.Id != state.Basis.Id || request.Identity.Snapshot != state.Basis.Snapshot ||
            request.MountPath != state.OriginalPath || request.SnapshotOwnerId != state.Basis.OwnerId ||
            request.SnapshotReceiptDirectory != state.Basis.ReceiptDirectory || request.DeltaPath != Path.Combine(state.Basis.ReceiptDirectory, "delta") ||
            request.ControlPath != Path.Combine(state.Basis.ReceiptDirectory, "overlay") || request.Identity.Graft is not null || request.ValidationView is not null)
            throw new WorkspaceException("GeneratedBasisMismatch", "Retain an inconsistent original mount request.");
    }
    internal static string HashFile(string path) { using var f = File.OpenRead(path); return Convert.ToHexStringLower(SHA256.HashData(f)); }
    private sealed record GeneratedDependency(string WorkspaceId, string RequestDigest, GeneratedDirectoryReference Basis);
    private sealed record PreparationIntent(int Version, string Id, string ProjectPath, string Key, string OriginalPath, string BasisPath, string CompatibilityKey);
}
