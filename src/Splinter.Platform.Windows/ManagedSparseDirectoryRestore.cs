using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using Splinter.Core;
using Splinter.Runtime;
using static Splinter.Platform.Windows.ManagedUnchangedDirectoryRestore;

namespace Splinter.Platform.Windows;

// Root transfer for unchanged surviving paths/content, metadata updates and
// deleted leaf files. Deleted data is staged, never destroyed before the final
// independent tree proof. Other namespace/content mutations use the file plan.
internal static class ManagedSparseDirectoryRestore
{
    private sealed record Plan(int Version, GeneratedDirectoryReference Basis, string OriginalPath, string DeltaPath,
        string IdentityHash, SnapshotDeltaSeal Seal, string JournalMode, Dictionary<string, string> Identities,
        SnapshotEntry[] Metadata, string[] Deleted);
    private sealed record Receipt(int Version, string PlanHash);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const string StagePrefix = "restore-sparse-leaf-";
    private static string PlanPath(string root) => Path.Combine(root, "restore-sparse-directory-plan.json");
    private static string VerifiedPath(string root) => Path.Combine(root, "restore-sparse-directory-verified.json");
    private static string StagingReclaimPath(string root) => Path.Combine(root, "restore-sparse-staging-reclaim.json");
    private static string JournalReclaimPath(string root) => Path.Combine(root, "restore-sparse-journal-reclaim.bin");
    private static string StagePath(string root, int index) => Path.Combine(root, StagePrefix + index.ToString("D8", System.Globalization.CultureInfo.InvariantCulture) + ".bin");
    private static WorkspaceException Conflict() => new("GeneratedRestoreConflict", "Preserve an inconsistent sparse-directory restoration.");

    internal static bool HasPlan(string root)
    {
        bool exists = File.Exists(PlanPath(root));
        if (!exists && (File.Exists(VerifiedPath(root)) || File.Exists(StagingReclaimPath(root)) ||
            BoundedJournalReclamation.HasIntent(JournalReclaimPath(root)) ||
            Directory.EnumerateFileSystemEntries(root, StagePrefix + "*").Any())) throw Conflict();
        return exists;
    }

    internal static bool Eligible(ManagedDirectoryState state, OverlayServeRequest request, OwnedDirectoryManifest manifest,
        DeltaRestoreEntry[] entries)
    {
        if (state.ApplicationKind != "unity" || state.DirectoryKind != "Library" || state.Basis.RelativePath != "Library" ||
            JournalMode(request) is null) return false;
        var items = manifest.Items.ToDictionary(i => i.Entry.Path, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var delta in entries)
        {
            var entry = delta.Entry;
            if (!seen.Add(entry.Path) || !items.TryGetValue(entry.Path, out var item) || entry.Deleted ||
                entry.BasePath != item.Entry.Path || entry.Directory != item.Entry.Directory || delta.Ranges.Length != 0 ||
                entry.BaseLength != item.Entry.Length || entry.Length != item.Entry.Length) return false;
        }
        // A deleted directory or a new/moved path requires the general topology
        // plan. Keeping the directory set fixed also preserves empty parents.
        return manifest.Items.Where(i => i.Entry.Directory).All(i => seen.Contains(i.Entry.Path));
    }

    internal static async Task CreateAsync(ManagedDirectoryState state, OverlayServeRequest request, OwnedDirectoryManifest manifest,
        SnapshotDeltaSeal seal, DeltaRestoreEntry[] entries, OwnedDirectorySnapshotTree basis, JournalDeltaStore journal,
        CancellationToken ct, Action<string>? boundary)
    {
        ValidateManifest(state, manifest); RequireAbsent(state.OriginalPath);
        if (!Eligible(state, request, manifest, entries) || HasPlan(state.Basis.ReceiptDirectory)) throw Conflict();
        Directory.CreateDirectory(Path.GetDirectoryName(state.OriginalPath)!);
        var items = manifest.Items.ToDictionary(i => i.Entry.Path, StringComparer.Ordinal);
        var final = entries.Select(d => new SnapshotEntry(d.Entry.Path, d.Entry.Directory, d.Entry.Length, d.Entry.Attributes,
            d.Entry.CreationTime, d.Entry.LastAccessTime, d.Entry.LastWriteTime, d.Entry.UnixMode)).ToArray();
        var paths = final.Select(e => e.Path).ToHashSet(StringComparer.Ordinal);
        using var parents = new ManagedRestoreFileAccess(state.Basis.Snapshot.RootPath, state.OriginalPath, StagePath(state.Basis.ReceiptDirectory, 0));
        using var root = basis.TransferRootToRestore(() => boundary?.Invoke("sparse.root-unpinned"));
        Plan plan;
        using (var pins = new TreePins(state.Basis.Snapshot.RootPath, manifest, root, null, verifyContent: false, ct))
        {
            plan = new(1, state.Basis, state.OriginalPath, request.DeltaPath, IdentityHash(request.Identity), seal,
                JournalMode(request) ?? throw Conflict(), pins.Identities,
                final.Where(e => e != items[e.Path].Entry).OrderBy(e => e.Path, StringComparer.Ordinal).ToArray(),
                manifest.Items.Where(i => !paths.Contains(i.Entry.Path)).Select(i => i.Entry.Path).Order(StringComparer.Ordinal).ToArray());
            RequireStaging(state.Basis.ReceiptDirectory, plan, allowExisting: false);
            if (journal.Seal() != seal) throw Conflict();
            await WindowsProcessWorkspaceProvider.WriteAsync(PlanPath(state.Basis.ReceiptDirectory), plan, ct);
            boundary?.Invoke("sparse.after-plan");
        }
        basis.Dispose(); journal.Dispose();
        await FinishAsync(state, request, manifest, plan, root, move: true, ct, boundary);
    }

    internal static async Task ResumeAsync(ManagedDirectoryState state, OverlayServeRequest request, OwnedDirectoryManifest manifest,
        CancellationToken ct, Action<string>? boundary)
    {
        ValidateManifest(state, manifest);
        var plan = await WindowsProcessWorkspaceProvider.ReadAsync<Plan>(PlanPath(state.Basis.ReceiptDirectory), ct) ?? throw Conflict();
        ValidatePlan(state, request, manifest, plan);
        bool move = Exists(state.Basis.Snapshot.RootPath);
        if (move == Exists(state.OriginalPath)) throw Conflict();
        using var parents = new ManagedRestoreFileAccess(state.Basis.Snapshot.RootPath, state.OriginalPath, StagePath(state.Basis.ReceiptDirectory, 0));
        using var root = ManagedRestoreFileAccess.DirectoryHandle(move ? state.Basis.Snapshot.RootPath : state.OriginalPath, moving: move);
        if (move)
        {
            if (File.Exists(VerifiedPath(state.Basis.ReceiptDirectory)) || File.Exists(StagingReclaimPath(state.Basis.ReceiptDirectory)) ||
                BoundedJournalReclamation.HasIntent(JournalReclaimPath(state.Basis.ReceiptDirectory))) throw Conflict();
            RequireStaging(state.Basis.ReceiptDirectory, plan, allowExisting: false);
            if (JournalMode(request) != plan.JournalMode) throw Conflict();
            RequireSeal(request, plan.Seal);
            using var pins = new TreePins(state.Basis.Snapshot.RootPath, manifest, root, plan.Identities, verifyContent: true, ct);
        }
        await FinishAsync(state, request, manifest, plan, root, move, ct, boundary);
    }

    private static async Task FinishAsync(ManagedDirectoryState state, OverlayServeRequest request, OwnedDirectoryManifest manifest,
        Plan plan, SafeFileHandle root, bool move, CancellationToken ct, Action<string>? boundary)
    {
        // Bind parsed effects and the receipt hash to the same immutable open
        // bytes. A publication/corruption after initial planning is not authority
        // to change the journal's deleted set or final metadata.
        string receiptRoot = state.Basis.ReceiptDirectory;
        using var planPin = ManagedRestoreFileAccess.Read(PlanPath(receiptRoot));
        plan = JsonSerializer.Deserialize<Plan>(planPin, Json) ?? throw Conflict();
        planPin.Position = 0; string planHash = Convert.ToHexStringLower(SHA256.HashData(planPin));
        ValidatePlan(state, request, manifest, plan);
        var verified = await ReadReceiptAsync(VerifiedPath(receiptRoot), planHash, ct);
        var reclaim = await ReadReceiptAsync(StagingReclaimPath(receiptRoot), planHash, ct);
        bool journalPending = BoundedJournalReclamation.HasIntent(JournalReclaimPath(receiptRoot));
        if (reclaim is not null && verified is null || journalPending && reclaim is null) throw Conflict();
        using var identity = journalPending ? null : PinIdentity(request, plan.IdentityHash);
        if (!journalPending)
        {
            if (JournalMode(request) != plan.JournalMode) throw Conflict();
            RequireSeal(request, plan.Seal);
        }
        using var journalProof = journalPending ? null : OpenJournalEffects(state, request, manifest, plan);
        if (SnapshotInputChecks.FileIdentity(root, directory: true) != plan.Identities[""]) throw Conflict();
        if (move)
        {
            if (verified is not null || reclaim is not null || journalPending) throw Conflict();
            ct.ThrowIfCancellationRequested(); RequireAbsent(state.OriginalPath);
            boundary?.Invoke("sparse.before-move");
            ManagedRestoreFileAccess.Move(root, state.OriginalPath);
            boundary?.Invoke("sparse.after-move");
        }
        RequireAbsent(state.Basis.Snapshot.RootPath); RequireStaging(receiptRoot, plan, allowExisting: true);
        var items = manifest.Items.ToDictionary(i => i.Entry.Path, StringComparer.Ordinal);
        var deleted = plan.Deleted.ToHashSet(StringComparer.Ordinal);
        var metadata = plan.Metadata.ToDictionary(e => e.Path, StringComparer.Ordinal);
        var finalManifest = manifest with { Items = manifest.Items.Where(i => !deleted.Contains(i.Entry.Path))
            .Select(i => metadata.TryGetValue(i.Entry.Path, out var entry) ? i with { Entry = entry } : i).ToArray() };
        var atOriginal = new HashSet<string>(StringComparer.Ordinal);
        var existingStages = new bool[plan.Deleted.Length];
        for (int i = 0; i < plan.Deleted.Length; i++)
        {
            bool original = Exists(PathOf(state.OriginalPath, plan.Deleted[i])), staged = Exists(StagePath(receiptRoot, i));
            if (original && staged || !original && !staged && reclaim is null || original && (verified is not null || reclaim is not null)) throw Conflict();
            if (original) atOriginal.Add(plan.Deleted[i]);
            existingStages[i] = staged;
        }
        // The survivor proof is complete after the rename gap and stays pinned
        // through all staging, metadata and reclaim effects; no payload is copied.
        using var final = new TreePins(state.OriginalPath, finalManifest, root, plan.Identities, verifyContent: true, ct, atOriginal);
        var pins = new FileStream?[plan.Deleted.Length];
        try
        {
            // Verify every remaining deleted entry before staging any of them.
            for (int i = 0; i < plan.Deleted.Length; i++)
            {
                ct.ThrowIfCancellationRequested(); string path = plan.Deleted[i];
                if (!atOriginal.Contains(path) && !existingStages[i]) continue;
                var file = ManagedRestoreFileAccess.Read(atOriginal.Contains(path) ? PathOf(state.OriginalPath, path) : StagePath(receiptRoot, i), moving: true);
                pins[i] = file; ManagedRestoreFileAccess.Require(file, plan.Identities[path]);
                if (file.Length != items[path].Entry.Length || OwnedDirectorySnapshotTree.Hash(file.SafeFileHandle, ct) != items[path].Hash) throw Conflict();
            }
            boundary?.Invoke("sparse.after-proof");
            for (int i = 0; i < plan.Deleted.Length; i++)
            {
                if (!atOriginal.Contains(plan.Deleted[i])) continue;
                ct.ThrowIfCancellationRequested(); RequireAbsent(StagePath(receiptRoot, i));
                boundary?.Invoke("sparse.before-stage");
                ManagedRestoreFileAccess.Move(pins[i]!, StagePath(receiptRoot, i));
                boundary?.Invoke("sparse.after-stage");
            }
            final.RequireMembership(); RequireAbsent(state.Basis.Snapshot.RootPath);
            if (!journalPending) RequireSeal(request, plan.Seal);
            final.ApplyMetadata();
            if (verified is null)
                await WindowsProcessWorkspaceProvider.WriteAsync(VerifiedPath(receiptRoot), new Receipt(1, planHash), ct);
            boundary?.Invoke("sparse.after-verified");
            // This receipt authorizes only the exact staged identities/hashes
            // already fixed in the plan. Absence is acceptable only after it.
            if (reclaim is null)
                await WindowsProcessWorkspaceProvider.WriteAsync(StagingReclaimPath(receiptRoot), new Receipt(1, planHash), ct);
            boundary?.Invoke("sparse.reclaim.intent");
            for (int i = 0; i < pins.Length; i++)
            {
                if (pins[i] is not { } file) continue;
                ct.ThrowIfCancellationRequested();
                ManagedRestoreFileAccess.Delete(file); file.Dispose(); pins[i] = null;
                RequireAbsent(StagePath(receiptRoot, i));
                boundary?.Invoke("sparse.reclaim.file");
            }
            final.RequireMembership(); RequireStaging(receiptRoot, plan, allowExisting: false);
            RequireAbsent(state.Basis.Snapshot.RootPath);
            // Retain the exact stopped reader throughout destructive staged
            // reclamation, then hand off to the journal's own fenced inventory.
            journalProof?.Dispose();
            void ReclaimBoundary(string stage)
            {
                if (stage == "intent") identity?.Dispose();
                boundary?.Invoke("sparse.journal." + stage);
            }
            if (plan.JournalMode == "bounded")
                BoundedJournalReclamation.ReclaimDirectory(request.DeltaPath, plan.Seal, ["identity.json"], JournalReclaimPath(receiptRoot),
                    ReclaimBoundary, path => new WindowsJournalDirectoryGuard(path, BoundedJournalReclamation.GuardName));
            else
                LatestJournalReclamation.ReclaimDirectory(request.DeltaPath, plan.Seal, ["identity.json"], JournalReclaimPath(receiptRoot),
                    ReclaimBoundary, path => new WindowsJournalDirectoryGuard(path));
            boundary?.Invoke("sparse.after-reclaim");
        }
        finally { foreach (var file in pins) file?.Dispose(); }
    }

    private static void ValidatePlan(ManagedDirectoryState state, OverlayServeRequest request, OwnedDirectoryManifest manifest, Plan plan)
    {
        if (plan.Version != 1 || state.ApplicationKind != "unity" || state.DirectoryKind != "Library" || state.Basis.RelativePath != "Library" ||
            plan.Basis != state.Basis || plan.OriginalPath != state.OriginalPath || plan.DeltaPath != request.DeltaPath ||
            plan.IdentityHash != IdentityHash(request.Identity) || plan.Seal is null || plan.Seal.DurableSequence < 0 || plan.JournalMode is not ("latest" or "bounded") ||
            plan.Identities is null || plan.Identities.Count != manifest.Items.Length || plan.Metadata is null || plan.Deleted is null ||
            manifest.Items.Any(i => !plan.Identities.TryGetValue(i.Entry.Path, out var identity) || string.IsNullOrEmpty(identity))) throw Conflict();
        var items = manifest.Items.ToDictionary(i => i.Entry.Path, StringComparer.Ordinal);
        var deleted = new HashSet<string>(StringComparer.Ordinal);
        foreach (string path in plan.Deleted)
            if (!deleted.Add(path) || !items.TryGetValue(path, out var item) || item.Entry.Directory) throw Conflict();
        if (!plan.Deleted.SequenceEqual(plan.Deleted.Order(StringComparer.Ordinal))) throw Conflict();
        var patched = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in plan.Metadata)
            if (!patched.Add(entry.Path) || deleted.Contains(entry.Path) || !items.TryGetValue(entry.Path, out var item) ||
                entry.Directory != item.Entry.Directory || entry.Length != item.Entry.Length || JournalDeltaStore.Normalize(entry.Path) != entry.Path) throw Conflict();
    }
    private static async Task<Receipt?> ReadReceiptAsync(string path, string hash, CancellationToken ct)
    {
        var receipt = await WindowsProcessWorkspaceProvider.ReadAsync<Receipt>(path, ct, waitForPublication: false);
        if (receipt is not null && (receipt.Version != 1 || receipt.PlanHash != hash)) throw Conflict();
        return receipt;
    }
    private static JournalDeltaStore OpenJournalEffects(ManagedDirectoryState state, OverlayServeRequest request, OwnedDirectoryManifest manifest, Plan plan)
    {
        // A moved basis is no longer an immutable payload source. Reconstruct
        // only final metadata from the fixed manifest and exact sealed journal.
        var journal = ManagedRestoreJournalProof.Open(request, manifest);
        try
        {
            var entries = journal.DescribeRestore();
            if (journal.Seal() != plan.Seal || !Eligible(state, request, manifest, entries)) throw Conflict();
            var items = manifest.Items.ToDictionary(i => i.Entry.Path, StringComparer.Ordinal);
            var final = entries.Select(d => new SnapshotEntry(d.Entry.Path, d.Entry.Directory, d.Entry.Length, d.Entry.Attributes,
                d.Entry.CreationTime, d.Entry.LastAccessTime, d.Entry.LastWriteTime, d.Entry.UnixMode)).ToArray();
            var paths = final.Select(e => e.Path).ToHashSet(StringComparer.Ordinal);
            var metadata = final.Where(e => e != items[e.Path].Entry).OrderBy(e => e.Path, StringComparer.Ordinal);
            var deleted = manifest.Items.Where(i => !paths.Contains(i.Entry.Path)).Select(i => i.Entry.Path).Order(StringComparer.Ordinal);
            if (!metadata.SequenceEqual(plan.Metadata) || !deleted.SequenceEqual(plan.Deleted)) throw Conflict();
            return journal;
        }
        catch { journal.Dispose(); throw; }
    }
    private static void RequireStaging(string root, Plan plan, bool allowExisting)
    {
        var allowed = Enumerable.Range(0, plan.Deleted.Length).Select(i => StagePath(root, i)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (string path in Directory.EnumerateFileSystemEntries(root, StagePrefix + "*"))
        {
            WorkspaceStore.EnsurePlainPath(path);
            if (!allowExisting || !allowed.Contains(path) || Directory.Exists(path)) throw Conflict();
        }
    }
    private static string PathOf(string root, string relative)
    {
        if (JournalDeltaStore.Normalize(relative) != relative) throw Conflict();
        string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)); WorkspaceStore.EnsurePlainPath(path); return path;
    }
}
