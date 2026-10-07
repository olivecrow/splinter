using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Platform.Windows;

// An unchanged original Library can return in one same-volume rename. The
// durable plan fixes every identity; the post-rename proof gates reclamation.
internal static class ManagedUnchangedDirectoryRestore
{
    private sealed record Plan(int Version, GeneratedDirectoryReference Basis, string OriginalPath,
        string DeltaPath, string IdentityHash, SnapshotDeltaSeal Seal, Dictionary<string, string> Identities);
    private sealed record Verified(int Version, string PlanHash);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static string PlanPath(string root) => Path.Combine(root, "restore-directory-plan.json");
    private static string VerifiedPath(string root) => Path.Combine(root, "restore-directory-verified.json");
    private static string ReclaimPath(string root) => Path.Combine(root, "restore-directory-reclaim.bin");
    internal static bool HasPlan(string root)
    {
        bool exists = File.Exists(PlanPath(root));
        if (!exists && (File.Exists(VerifiedPath(root)) || File.Exists(ReclaimPath(root)))) throw Conflict();
        return exists;
    }
    private static WorkspaceException Conflict() => new("GeneratedRestoreConflict", "Preserve an inconsistent unchanged-directory restoration.");
    private static string IdentityHash(OverlayIdentity identity) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(identity, Json)));

    internal static bool Eligible(ManagedDirectoryState state, OverlayServeRequest request, OwnedDirectoryManifest manifest,
        SnapshotDeltaSeal seal, DeltaRestoreEntry[] entries)
    {
        // SJL4 already has an exact, resumable multi-file reclamation inventory.
        // Other journals and any logical mutation retain the general v2 route.
        if (state.ApplicationKind != "unity" || state.DirectoryKind != "Library" || state.Basis.RelativePath != "Library" ||
            seal.DurableSequence != 0 || !LatestJournalReclamation.IsLatest(Path.Combine(request.DeltaPath, "delta.journal")) ||
            entries.Length != manifest.Items.Length) return false;
        var items = manifest.Items.ToDictionary(i => i.Entry.Path, StringComparer.Ordinal);
        return entries.All(d => items.TryGetValue(d.Entry.Path, out var item) && d.Ranges.Length == 0 && !d.Entry.Deleted &&
            d.Entry.BasePath == item.Entry.Path && d.Entry.BaseLength == item.Entry.Length &&
            new SnapshotEntry(d.Entry.Path, d.Entry.Directory, d.Entry.Length, d.Entry.Attributes,
                d.Entry.CreationTime, d.Entry.LastAccessTime, d.Entry.LastWriteTime, d.Entry.UnixMode) == item.Entry);
    }

    internal static async Task CreateAsync(ManagedDirectoryState state, OverlayServeRequest request, OwnedDirectoryManifest manifest,
        SnapshotDeltaSeal seal, OwnedDirectorySnapshotTree basis, JournalDeltaStore journal, CancellationToken ct, Action<string>? boundary)
    {
        ValidateManifest(state, manifest);
        RequireAbsent(state.OriginalPath);
        Directory.CreateDirectory(Path.GetDirectoryName(state.OriginalPath)!);
        using var parents = new ManagedRestoreFileAccess(state.Basis.Snapshot.RootPath, state.OriginalPath);
        using var root = ManagedRestoreFileAccess.DirectoryHandle(state.Basis.Snapshot.RootPath, moving: true);
        Plan plan;
        using (var pins = new TreePins(state.Basis.Snapshot.RootPath, manifest, root, null, verifyContent: false, ct))
        {
            // The caller's fully hashed basis reader still denies file writes
            // and deletion while these identities and the intent are captured.
            plan = new(1, state.Basis, state.OriginalPath, request.DeltaPath, IdentityHash(request.Identity), seal, pins.Identities);
            if (journal.Seal() != seal) throw Conflict();
            await WindowsProcessWorkspaceProvider.WriteAsync(PlanPath(state.Basis.ReceiptDirectory), plan, ct);
            boundary?.Invoke("directory.after-plan");
        }
        basis.Dispose(); journal.Dispose();
        await MoveAndFinishAsync(state, request, manifest, plan, root, move: true, ct, boundary);
    }

    internal static async Task ResumeAsync(ManagedDirectoryState state, OverlayServeRequest request, OwnedDirectoryManifest manifest,
        CancellationToken ct, Action<string>? boundary)
    {
        ValidateManifest(state, manifest);
        var plan = await WindowsProcessWorkspaceProvider.ReadAsync<Plan>(PlanPath(state.Basis.ReceiptDirectory), ct) ?? throw Conflict();
        if (plan.Version != 1 || plan.Basis != state.Basis || plan.OriginalPath != state.OriginalPath || plan.DeltaPath != request.DeltaPath ||
            plan.IdentityHash != IdentityHash(request.Identity) || plan.Seal.DurableSequence != 0 || plan.Identities is null ||
            plan.Identities.Count != manifest.Items.Length || manifest.Items.Any(i => !plan.Identities.ContainsKey(i.Entry.Path))) throw Conflict();
        bool move = Exists(state.Basis.Snapshot.RootPath);
        if (move == Exists(state.OriginalPath)) throw Conflict();
        string path = move ? state.Basis.Snapshot.RootPath : state.OriginalPath;
        using var parents = new ManagedRestoreFileAccess(state.Basis.Snapshot.RootPath, state.OriginalPath);
        using var root = ManagedRestoreFileAccess.DirectoryHandle(path, moving: move);
        if (move)
        {
            if (File.Exists(VerifiedPath(state.Basis.ReceiptDirectory)) || File.Exists(ReclaimPath(state.Basis.ReceiptDirectory))) throw Conflict();
            RequireSeal(request, plan.Seal);
            // Retry cannot inherit the initial reader's proof or open handles.
            using var pins = new TreePins(path, manifest, root, plan.Identities, verifyContent: true, ct);
        }
        await MoveAndFinishAsync(state, request, manifest, plan, root, move, ct, boundary);
    }

    private static async Task MoveAndFinishAsync(ManagedDirectoryState state, OverlayServeRequest request, OwnedDirectoryManifest manifest,
        Plan plan, SafeFileHandle root, bool move, CancellationToken ct, Action<string>? boundary)
    {
        string receiptRoot = state.Basis.ReceiptDirectory;
        using var identityFence = File.Exists(ReclaimPath(receiptRoot)) ? null : PinIdentity(request, plan.IdentityHash);
        if (SnapshotInputChecks.FileIdentity(root, directory: true) != plan.Identities[""]) throw Conflict();
        if (move)
        {
            ct.ThrowIfCancellationRequested(); RequireAbsent(state.OriginalPath); RequireSeal(request, plan.Seal);
            // NTFS directory rename requires descendant file handles closed.
            // Keep this exact root and both ancestor chains pinned throughout.
            boundary?.Invoke("directory.before-move");
            ManagedRestoreFileAccess.Move(root, state.OriginalPath);
            boundary?.Invoke("directory.after-move");
        }
        RequireAbsent(state.Basis.Snapshot.RootPath);
        using var final = new TreePins(state.OriginalPath, manifest, root, plan.Identities, verifyContent: true, ct);
        boundary?.Invoke("directory.after-proof");
        string planHash = ManagedDirectoryBasis.HashFile(PlanPath(receiptRoot));
        var verified = await WindowsProcessWorkspaceProvider.ReadAsync<Verified>(VerifiedPath(receiptRoot), ct, waitForPublication: false);
        if (verified is not null && (verified.Version != 1 || verified.PlanHash != planHash)) throw Conflict();
        if (verified is null)
        {
            RequireSeal(request, plan.Seal);
            final.ApplyMetadata();
            await WindowsProcessWorkspaceProvider.WriteAsync(VerifiedPath(receiptRoot), new Verified(1, planHash), ct);
            boundary?.Invoke("directory.after-verified");
        }
        else
        {
            if (!File.Exists(ReclaimPath(receiptRoot))) RequireSeal(request, plan.Seal);
            final.ApplyMetadata();
        }
        final.RequireMembership(); RequireAbsent(state.Basis.Snapshot.RootPath);
        ct.ThrowIfCancellationRequested();
        LatestJournalReclamation.ReclaimDirectory(request.DeltaPath, plan.Seal, ["identity.json"], ReclaimPath(receiptRoot),
            stage =>
            {
                // Transfer metadata protection to the durable exact-file
                // inventory before allowing its conditional deletion.
                if (stage == "intent") identityFence?.Dispose();
                boundary?.Invoke("directory.reclaim." + stage);
            });
        boundary?.Invoke("directory.after-reclaim");
    }

    private static void RequireSeal(OverlayServeRequest request, SnapshotDeltaSeal seal)
    {
        if (JournalDeltaStore.SealJournal(Path.Combine(request.DeltaPath, "delta.journal"), seal.DurableSequence) != seal) throw Conflict();
    }
    private static void ValidateManifest(ManagedDirectoryState state, OwnedDirectoryManifest manifest)
    {
        if (manifest.Version != 1 || manifest.Snapshot != state.Basis.Snapshot || manifest.OwnerId != state.Basis.OwnerId ||
            ManagedDirectoryBasis.HashFile(Path.Combine(state.Basis.ReceiptDirectory, "manifest.json")) != state.Basis.ManifestHash) throw Conflict();
    }
    private static FileStream PinIdentity(OverlayServeRequest request, string expected)
    {
        string path = Path.Combine(request.DeltaPath, "identity.json"); WorkspaceStore.EnsurePlainPath(path);
        var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            var identity = JsonSerializer.Deserialize<OverlayIdentity>(file, Json);
            if (identity is null || IdentityHash(identity) != expected) throw Conflict();
            return file;
        }
        catch { file.Dispose(); throw; }
    }
    private static bool Exists(string path)
    {
        WorkspaceStore.EnsurePlainPath(path);
        try { _ = File.GetAttributes(path); return true; }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
    }
    private static void RequireAbsent(string path) { if (Exists(path)) throw Conflict(); }

    private sealed class TreePins : IDisposable
    {
        private readonly string path;
        private readonly OwnedDirectoryManifest manifest;
        private readonly SafeFileHandle root;
        private readonly Dictionary<string, SafeFileHandle> directories = new(StringComparer.Ordinal);
        private readonly Dictionary<string, FileStream> files = new(StringComparer.Ordinal);
        internal Dictionary<string, string> Identities { get; } = new(StringComparer.Ordinal);
        internal TreePins(string path, OwnedDirectoryManifest manifest, SafeFileHandle root, Dictionary<string, string>? expected,
            bool verifyContent, CancellationToken ct)
        {
            this.path = path; this.manifest = manifest; this.root = root;
            try
            {
                foreach (var item in manifest.Items.OrderBy(i => i.Entry.Path.Length))
                {
                    ct.ThrowIfCancellationRequested(); var entry = item.Entry;
                    if (JournalDeltaStore.Normalize(entry.Path) != entry.Path) throw Conflict();
                    string full = Path.Combine(path, entry.Path.Replace('/', Path.DirectorySeparatorChar));
                    SafeFileHandle handle;
                    if (entry.Directory)
                    {
                        handle = entry.Path.Length == 0 ? root : ManagedRestoreFileAccess.DirectoryHandle(full);
                        if (entry.Path.Length != 0) directories.Add(entry.Path, handle);
                    }
                    else
                    {
                        var file = ManagedRestoreFileAccess.Read(full); files.Add(entry.Path, file); handle = file.SafeFileHandle;
                        if (file.Length != entry.Length || verifyContent && OwnedDirectorySnapshotTree.Hash(handle) != item.Hash) throw Conflict();
                    }
                    string identity = SnapshotInputChecks.FileIdentity(handle, entry.Directory);
                    if (expected is not null && (!expected.TryGetValue(entry.Path, out string? saved) || saved != identity)) throw Conflict();
                    Identities.Add(entry.Path, identity);
                }
                RequireMembership();
            }
            catch { Dispose(); throw; }
        }
        internal void RequireMembership()
        {
            int seen = 1;
            // Enumerate only known pinned directories, never recurse through
            // an unexpected directory/reparse point supplied during a gap.
            foreach (var entry in manifest.Items.Where(i => i.Entry.Directory).Select(i => i.Entry))
                foreach (string child in Directory.EnumerateFileSystemEntries(Path.Combine(path, entry.Path.Replace('/', Path.DirectorySeparatorChar))))
                {
                    string relative = Path.GetRelativePath(path, child).Replace('\\', '/');
                    if (!Identities.ContainsKey(relative)) throw Conflict();
                    seen++;
                }
            if (seen != manifest.Items.Length) throw Conflict();
        }
        internal void ApplyMetadata()
        {
            foreach (var entry in manifest.Items.Select(i => i.Entry).OrderByDescending(e => e.Path.Length))
                ManagedRestoreFileAccess.Metadata(entry.Directory ? entry.Path.Length == 0 ? root : directories[entry.Path] : files[entry.Path].SafeFileHandle, entry);
        }
        public void Dispose()
        {
            foreach (var file in files.Values) file.Dispose(); files.Clear();
            foreach (var directory in directories.Values.Reverse()) directory.Dispose(); directories.Clear();
        }
    }
}
