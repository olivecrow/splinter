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
        string DeltaPath, string IdentityHash, SnapshotDeltaSeal Seal, Dictionary<string, string> Identities, string JournalMode);
    private sealed record Verified(int Version, string PlanHash);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static string PlanPath(string root) => Path.Combine(root, "restore-directory-plan.json");
    private static string VerifiedPath(string root) => Path.Combine(root, "restore-directory-verified.json");
    private static string ReclaimPath(string root) => Path.Combine(root, "restore-directory-reclaim.bin");
    internal static bool HasPlan(string root)
    {
        bool exists = File.Exists(PlanPath(root));
        if (!exists && (File.Exists(VerifiedPath(root)) || BoundedJournalReclamation.HasIntent(ReclaimPath(root)))) throw Conflict();
        return exists;
    }
    private static WorkspaceException Conflict() => new("GeneratedRestoreConflict", "Preserve an inconsistent unchanged-directory restoration.");
    internal static string IdentityHash(OverlayIdentity identity) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(identity, Json)));

    internal static bool Eligible(ManagedDirectoryState state, OverlayServeRequest request, OwnedDirectoryManifest manifest,
        SnapshotDeltaSeal seal, DeltaRestoreEntry[] entries)
    {
        // Both current-state formats have exact resumable reclamation inventories.
        // A positive sequence is harmless only when the complete final state
        // still equals the fixed manifest. The exact nonzero seal is retained.
        if (state.ApplicationKind != "unity" || state.DirectoryKind != "Library" || state.Basis.RelativePath != "Library" ||
            JournalMode(request) is null ||
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
        using var root = basis.TransferRootToRestore(() => boundary?.Invoke("directory.root-unpinned"));
        Plan plan;
        using (var pins = new TreePins(state.Basis.Snapshot.RootPath, manifest, root, null, verifyContent: false, ct))
        {
            // The caller's fully hashed basis reader still denies file writes
            // and deletion while these identities and the intent are captured.
            plan = new(2, state.Basis, state.OriginalPath, request.DeltaPath, IdentityHash(request.Identity), seal, pins.Identities,
                JournalMode(request) ?? throw Conflict());
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
        ValidatePlan(state, request, manifest, plan);
        bool move = Exists(state.Basis.Snapshot.RootPath);
        if (move == Exists(state.OriginalPath)) throw Conflict();
        string path = move ? state.Basis.Snapshot.RootPath : state.OriginalPath;
        using var parents = new ManagedRestoreFileAccess(state.Basis.Snapshot.RootPath, state.OriginalPath);
        using var root = ManagedRestoreFileAccess.DirectoryHandle(path, moving: move);
        if (move)
        {
            if (File.Exists(VerifiedPath(state.Basis.ReceiptDirectory)) || BoundedJournalReclamation.HasIntent(ReclaimPath(state.Basis.ReceiptDirectory))) throw Conflict();
            if (JournalMode(request) != plan.JournalMode) throw Conflict();
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
        // The parsed intent and its receipt hash must refer to the same pinned
        // bytes, including when the plan was published earlier in this call.
        using var planPin = ManagedRestoreFileAccess.Read(PlanPath(receiptRoot));
        plan = JsonSerializer.Deserialize<Plan>(planPin, Json) ?? throw Conflict();
        planPin.Position = 0; string planHash = Convert.ToHexStringLower(SHA256.HashData(planPin));
        ValidatePlan(state, request, manifest, plan);
        var verified = await WindowsProcessWorkspaceProvider.ReadAsync<Verified>(VerifiedPath(receiptRoot), ct, waitForPublication: false);
        if (verified is not null && (verified.Version != 1 || verified.PlanHash != planHash)) throw Conflict();
        bool reclaimPending = BoundedJournalReclamation.HasIntent(ReclaimPath(receiptRoot));
        if (reclaimPending && verified is null || move && (verified is not null || reclaimPending)) throw Conflict();
        using var identityFence = reclaimPending ? null : PinIdentity(request, plan.IdentityHash);
        if (!reclaimPending && JournalMode(request) != plan.JournalMode) throw Conflict();
        using var journalProof = reclaimPending ? null : OpenJournalEffects(state, request, manifest, plan);
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
        if (verified is null)
        {
            RequireSeal(request, plan.Seal);
            final.ApplyMetadata();
            await WindowsProcessWorkspaceProvider.WriteAsync(VerifiedPath(receiptRoot), new Verified(1, planHash), ct);
            boundary?.Invoke("directory.after-verified");
        }
        else
        {
            if (!reclaimPending) RequireSeal(request, plan.Seal);
            final.ApplyMetadata();
        }
        final.RequireMembership(); RequireAbsent(state.Basis.Snapshot.RootPath);
        ct.ThrowIfCancellationRequested();
        journalProof?.Dispose();
        void ReclaimBoundary(string stage)
        {
            // Transfer metadata protection to the durable exact-file inventory
            // before allowing its conditional deletion.
            if (stage == "intent") identityFence?.Dispose();
            boundary?.Invoke("directory.reclaim." + stage);
        }
        if (plan.JournalMode == "bounded")
            BoundedJournalReclamation.ReclaimDirectory(request.DeltaPath, plan.Seal, ["identity.json"], ReclaimPath(receiptRoot),
                ReclaimBoundary, path => new WindowsJournalDirectoryGuard(path, BoundedJournalReclamation.GuardName));
        else
            LatestJournalReclamation.ReclaimDirectory(request.DeltaPath, plan.Seal, ["identity.json"], ReclaimPath(receiptRoot),
                ReclaimBoundary, path => new WindowsJournalDirectoryGuard(path));
        boundary?.Invoke("directory.after-reclaim");
    }

    private static void ValidatePlan(ManagedDirectoryState state, OverlayServeRequest request, OwnedDirectoryManifest manifest, Plan plan)
    {
        if (plan.Version != 2 || state.ApplicationKind != "unity" || state.DirectoryKind != "Library" || state.Basis.RelativePath != "Library" ||
            plan.JournalMode is not ("latest" or "bounded") || plan.Basis != state.Basis || plan.OriginalPath != state.OriginalPath || plan.DeltaPath != request.DeltaPath ||
            plan.IdentityHash != IdentityHash(request.Identity) || plan.Seal is null || plan.Seal.DurableSequence < 0 || plan.Identities is null ||
            plan.Identities.Count != manifest.Items.Length ||
            manifest.Items.Any(i => !plan.Identities.TryGetValue(i.Entry.Path, out string? identity) || string.IsNullOrEmpty(identity))) throw Conflict();
    }

    private static JournalDeltaStore OpenJournalEffects(ManagedDirectoryState state, OverlayServeRequest request, OwnedDirectoryManifest manifest, Plan plan)
    {
        var journal = ManagedRestoreJournalProof.Open(request, manifest);
        try
        {
            // A valid seal alone cannot authorize ignoring journal effects.
            // Prove the final namespace/content/metadata still equal the basis.
            var entries = journal.DescribeRestore();
            if (journal.Seal() != plan.Seal || !Eligible(state, request, manifest, plan.Seal, entries)) throw Conflict();
            return journal;
        }
        catch { journal.Dispose(); throw; }
    }

    internal static string? JournalMode(OverlayServeRequest request)
    {
        string path = Path.Combine(request.DeltaPath, "delta.journal");
        return LatestJournalReclamation.IsLatest(path) ? "latest" : JournalDeltaStore.IsBoundedJournal(path) ? "bounded" : null;
    }

    internal static void RequireSeal(OverlayServeRequest request, SnapshotDeltaSeal seal)
    {
        if (JournalDeltaStore.SealJournal(Path.Combine(request.DeltaPath, "delta.journal"), seal.DurableSequence,
            path => new WindowsJournalDirectoryGuard(path)) != seal) throw Conflict();
    }
    internal static void ValidateManifest(ManagedDirectoryState state, OwnedDirectoryManifest manifest)
    {
        if (manifest.Version != 1 || manifest.Snapshot != state.Basis.Snapshot || manifest.OwnerId != state.Basis.OwnerId ||
            ManagedDirectoryBasis.HashFile(Path.Combine(state.Basis.ReceiptDirectory, "manifest.json")) != state.Basis.ManifestHash) throw Conflict();
    }
    internal static FileStream PinIdentity(OverlayServeRequest request, string expected)
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
    internal static bool Exists(string path)
    {
        WorkspaceStore.EnsurePlainPath(path);
        try { _ = File.GetAttributes(path); return true; }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
    }
    internal static void RequireAbsent(string path) { if (Exists(path)) throw Conflict(); }

    internal sealed class TreePins : IDisposable
    {
        private readonly string path;
        private readonly OwnedDirectoryManifest manifest;
        private readonly SafeFileHandle root;
        private readonly WindowsDirectoryTreeProof proof;
        private readonly Dictionary<string, SafeFileHandle> directories = new(StringComparer.Ordinal);
        private readonly Dictionary<string, FileStream> files = new(StringComparer.Ordinal);
        internal Dictionary<string, string> Identities { get; } = new(StringComparer.Ordinal);
        internal TreePins(string path, OwnedDirectoryManifest manifest, SafeFileHandle root, Dictionary<string, string>? expected,
            bool verifyContent, CancellationToken ct, IReadOnlySet<string>? extraFiles = null)
        {
            this.path = path; this.manifest = manifest; this.root = root;
            proof = new(path, root);
            try
            {
                foreach (var item in manifest.Items.Where(i => i.Entry.Directory).OrderBy(i => i.Entry.Path.Length))
                {
                    ct.ThrowIfCancellationRequested(); var entry = item.Entry;
                    if (JournalDeltaStore.Normalize(entry.Path) != entry.Path) throw Conflict();
                    var handle = proof.PinDirectory(entry.Path);
                    if (entry.Path.Length != 0) directories.Add(entry.Path, handle);
                    string identity = SnapshotInputChecks.FileIdentity(handle, directory: true);
                    if (expected is not null && (!expected.TryGetValue(entry.Path, out string? saved) || saved != identity)) throw Conflict();
                    Identities.Add(entry.Path, identity);
                }
                var entries = manifest.Items.Where(i => !i.Entry.Directory).ToArray();
                var opened = new FileStream?[entries.Length];
                var identities = new string[entries.Length];
                try
                {
                    OwnedDirectorySnapshotTree.ParallelFiles(Enumerable.Range(0, entries.Length), index =>
                    {
                        var item = entries[index]; var entry = item.Entry;
                        if (JournalDeltaStore.Normalize(entry.Path) != entry.Path) throw Conflict();
                        var file = proof.Read(entry.Path); opened[index] = file;
                        if (file.Length != entry.Length || verifyContent && OwnedDirectorySnapshotTree.Hash(file.SafeFileHandle, ct) != item.Hash) throw Conflict();
                        string identity = SnapshotInputChecks.FileIdentity(file.SafeFileHandle);
                        if (expected is not null && (!expected.TryGetValue(entry.Path, out string? saved) || saved != identity)) throw Conflict();
                        identities[index] = identity;
                    }, OwnedDirectorySnapshotTree.DefaultWorkers, ct);
                    for (int i = 0; i < entries.Length; i++)
                    { files.Add(entries[i].Entry.Path, opened[i]!); Identities.Add(entries[i].Entry.Path, identities[i]); }
                }
                catch { foreach (var file in opened) file?.Dispose(); throw; }
                RequireMembership(extraFiles);
            }
            catch { Dispose(); throw; }
        }
        internal void RequireMembership(IReadOnlySet<string>? extraFiles = null)
        {
            int seen = 1;
            // Enumerate only known pinned directories, never recurse through
            // an unexpected directory/reparse point supplied during a gap.
            foreach (var entry in manifest.Items.Where(i => i.Entry.Directory).Select(i => i.Entry))
                foreach (string child in Directory.EnumerateFileSystemEntries(Path.Combine(path, entry.Path.Replace('/', Path.DirectorySeparatorChar))))
                {
                    string relative = Path.GetRelativePath(path, child).Replace('\\', '/');
                    if (!Identities.ContainsKey(relative) && extraFiles?.Contains(relative) != true) throw Conflict();
                    seen++;
                }
            if (seen != manifest.Items.Length + (extraFiles?.Count ?? 0)) throw Conflict();
        }
        internal void ApplyMetadata()
        {
            OwnedDirectorySnapshotTree.ParallelFiles(manifest.Items.Where(i => !i.Entry.Directory),
                item => ManagedRestoreFileAccess.Metadata(files[item.Entry.Path].SafeFileHandle, item.Entry),
                OwnedDirectorySnapshotTree.DefaultWorkers, CancellationToken.None);
            foreach (var entry in manifest.Items.Where(i => i.Entry.Directory).Select(i => i.Entry).OrderByDescending(e => e.Path.Length))
                ManagedRestoreFileAccess.Metadata(entry.Path.Length == 0 ? root : directories[entry.Path], entry);
        }
        public void Dispose()
        {
            foreach (var file in files.Values) file.Dispose(); files.Clear();
            proof.Dispose(); directories.Clear();
        }
    }
}
