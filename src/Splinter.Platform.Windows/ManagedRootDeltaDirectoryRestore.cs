using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using Splinter.Core;
using Splinter.Runtime;
using static Splinter.Platform.Windows.ManagedUnchangedDirectoryRestore;

namespace Splinter.Platform.Windows;

// Transfer the directory once, keep unchanged leaves in place, and replay only
// private ranges. This mode deliberately requires the original directory set.
// Deleted/replaced leaves survive in fixed staging until the final whole proof.
internal static class ManagedRootDeltaDirectoryRestore
{
    internal sealed record RestoreFile(DeltaRestoreEntry Delta, string FinalHash, string RetainedHash, string? BasisIdentity);
    private sealed record Plan(int Version, GeneratedDirectoryReference Basis, string OriginalPath, string DeltaPath,
        string IdentityHash, SnapshotDeltaSeal Seal, string JournalMode, Dictionary<string, string> Identities,
        DeltaRestoreEntry[] Directories, RestoreFile[] Files, string[] Deleted);
    private sealed record Receipt(int Version, string PlanHash);
    private sealed record FileReceipt(int Version, string PlanHash, Guid EntryId, string Identity);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const string Prefix = "restore-root-delta-";
    private static string Named(string root, string name) => Path.Combine(root, Prefix + name);
    private static string PlanPath(string root) => Named(root, "plan.json");
    private static string IntentPath(string root) => Named(root, "intent.json");
    private static string VerifiedPath(string root) => Named(root, "verified.json");
    private static string StagingReclaimPath(string root) => Named(root, "staging-reclaim.json");
    private static string JournalReclaimPath(string root) => Named(root, "journal-reclaim.bin");
    private static string StagePath(string root, int index) => Named(root, "leaf-" + index.ToString("D8", System.Globalization.CultureInfo.InvariantCulture) + ".bin");
    private static string FilePath(string root, string kind, Guid id) => Named(root, kind + "-" + id.ToString("N") + ".json");
    private static WorkspaceException Conflict() => new("GeneratedRestoreConflict", "Preserve an inconsistent root-delta restoration.");
    private static string EmptyHash => Convert.ToHexStringLower(SHA256.HashData(Array.Empty<byte>()));
    internal static bool FixedContent(DeltaRestoreEntry delta, OwnedDirectoryItem? original) => original?.Hash is not null &&
        delta.Entry.BasePath == original.Entry.Path && delta.Ranges.Length == 0 &&
        delta.Entry.BaseLength == original.Entry.Length && delta.Entry.Length == original.Entry.Length;

    internal static bool HasPlan(string root)
    {
        bool exists = File.Exists(PlanPath(root));
        if (!exists)
            foreach (string path in Directory.EnumerateFileSystemEntries(root, Prefix + "*"))
                if (!ManagedRestoreControlFiles.IsPublicationTemporary(path, new HashSet<string>(StringComparer.Ordinal) { PlanPath(root) })) throw Conflict();
        return exists;
    }

    internal static bool Eligible(ManagedDirectoryState state, OverlayServeRequest request, OwnedDirectoryManifest manifest,
        DeltaRestoreEntry[] entries) => Supported(state) && JournalMode(request) is not null && EligibleEntries(manifest, entries);

    private static bool Supported(ManagedDirectoryState state) => state.ApplicationKind == "unity" &&
        state.DirectoryKind == "Library" && state.Basis.RelativePath == "Library";

    private static bool EligibleEntries(OwnedDirectoryManifest manifest, DeltaRestoreEntry[] entries)
    {
        var items = manifest.Items.ToDictionary(i => i.Entry.Path, StringComparer.Ordinal);
        var originalPaths = manifest.Items.Select(i => i.Entry.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ids = new HashSet<Guid>();
        var directories = new HashSet<string>(StringComparer.Ordinal);
        foreach (var delta in entries)
        {
            if (delta?.Entry is not { } entry || delta.Ranges is null || entry.Deleted || entry.Length < 0 ||
                entry.BaseLength < 0 || entry.BaseLength > entry.Length || !paths.Add(entry.Path) || !ids.Add(entry.Id) ||
                JournalDeltaStore.Normalize(entry.Path) != entry.Path) return false;
            items.TryGetValue(entry.Path, out var original);
            // A case-only rename is topology work, even on a case-insensitive
            // volume. This first mode keeps original names byte-for-byte.
            if (original is null && originalPaths.Contains(entry.Path)) return false;
            if (entry.Directory)
            {
                if (original?.Entry.Directory != true || entry.BasePath != entry.Path || entry.Length != 0 ||
                    entry.BaseLength != 0 || delta.Ranges.Length != 0) return false;
                directories.Add(entry.Path);
            }
            else if (entry.Path.Length == 0 || original?.Entry.Directory == true ||
                entry.BasePath is not null && (entry.BasePath != entry.Path || original?.Hash is null || entry.BaseLength > original.Entry.Length) ||
                entry.BasePath is null && entry.BaseLength != 0) return false;
            long end = 0;
            foreach (var range in delta.Ranges)
            {
                if (range is null || range.Offset < end || range.Length <= 0 || range.Offset > entry.Length - range.Length) return false;
                end = range.Offset + range.Length;
            }
        }
        if (!directories.SetEquals(manifest.Items.Where(i => i.Entry.Directory).Select(i => i.Entry.Path))) return false;
        return entries.Where(e => e.Entry.Path.Length != 0).All(e => directories.Contains(Parent(e.Entry.Path)));
    }

    internal static async Task CreateAsync(ManagedDirectoryState state, OverlayServeRequest request, OwnedDirectoryManifest manifest,
        SnapshotDeltaSeal seal, DeltaRestoreEntry[] entries, OwnedDirectorySnapshotTree basis, JournalDeltaStore journal,
        CancellationToken ct, Action<string>? boundary)
    {
        ValidateManifest(state, manifest); RequireAbsent(state.OriginalPath);
        string receiptRoot = state.Basis.ReceiptDirectory;
        if (!Eligible(state, request, manifest, entries) || HasPlan(receiptRoot)) throw Conflict();
        var files = BuildFiles(state.Basis.Snapshot.RootPath, manifest, entries, journal, ct);
        Directory.CreateDirectory(Path.GetDirectoryName(state.OriginalPath)!);
        using var parents = new ManagedRestoreFileAccess(state.Basis.Snapshot.RootPath, state.OriginalPath, StagePath(receiptRoot, 0));
        using var root = basis.TransferRootToRestore(() => boundary?.Invoke("root-delta.root-unpinned"));
        Plan plan;
        // The admitted basis still holds every already-hashed leaf handle.
        using (var pins = new TreePins(state.Basis.Snapshot.RootPath, manifest, root, null, verifyContent: false, ct))
        {
            var retained = entries.Where(e => e.Entry.BasePath is not null).Select(e => e.Entry.BasePath!).ToHashSet(StringComparer.Ordinal);
            plan = new(1, state.Basis, state.OriginalPath, request.DeltaPath, IdentityHash(request.Identity), seal,
                JournalMode(request) ?? throw Conflict(), pins.Identities,
                entries.Where(e => e.Entry.Directory).OrderBy(e => e.Entry.Path, StringComparer.Ordinal).ToArray(), files,
                manifest.Items.Where(i => !i.Entry.Directory && !retained.Contains(i.Entry.Path)).Select(i => i.Entry.Path).Order(StringComparer.Ordinal).ToArray());
            ValidatePlan(state, request, manifest, plan);
            RequireArtifacts(receiptRoot, plan, manifest, initial: true);
            if (journal.Seal() != seal) throw Conflict();
            await WindowsProcessWorkspaceProvider.WriteAsync(PlanPath(receiptRoot), plan, ct);
            // An independently durable binding prevents changed retained/final
            // hashes in a saved plan from authorizing a partially applied redo.
            using var planPin = ManagedRestoreFileAccess.Read(PlanPath(receiptRoot));
            string hash = Hash(planPin);
            if (hash != Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(plan, Json)))) throw Conflict();
            await WindowsProcessWorkspaceProvider.WriteAsync(IntentPath(receiptRoot), new Receipt(1, hash), ct);
            boundary?.Invoke("root-delta.after-plan");
        }
        basis.Dispose(); journal.Dispose();
        await FinishAsync(state, request, manifest, root, move: true, ct, boundary);
    }

    internal static async Task ResumeAsync(ManagedDirectoryState state, OverlayServeRequest request, OwnedDirectoryManifest manifest,
        CancellationToken ct, Action<string>? boundary)
    {
        ValidateManifest(state, manifest);
        bool move = Exists(state.Basis.Snapshot.RootPath);
        if (move == Exists(state.OriginalPath)) throw Conflict();
        using var parents = new ManagedRestoreFileAccess(state.Basis.Snapshot.RootPath, state.OriginalPath, StagePath(state.Basis.ReceiptDirectory, 0));
        using var root = ManagedRestoreFileAccess.DirectoryHandle(move ? state.Basis.Snapshot.RootPath : state.OriginalPath, moving: move);
        await FinishAsync(state, request, manifest, root, move, ct, boundary);
    }

    private static async Task FinishAsync(ManagedDirectoryState state, OverlayServeRequest request, OwnedDirectoryManifest manifest,
        SafeFileHandle root, bool move, CancellationToken ct, Action<string>? boundary)
    {
        string receiptRoot = state.Basis.ReceiptDirectory;
        using var planPin = ManagedRestoreFileAccess.Read(PlanPath(receiptRoot));
        var plan = JsonSerializer.Deserialize<Plan>(planPin, Json) ?? throw Conflict();
        string planHash = Hash(planPin);
        ValidatePlan(state, request, manifest, plan); RequireArtifacts(receiptRoot, plan, manifest, initial: false);
        var receiptPins = new List<FileStream>();
        var deletedPins = new FileStream?[plan.Deleted.Length];
        try
        {
            var intent = ReadReceipt(IntentPath(receiptRoot), planHash, receiptPins);
            var verified = ReadReceipt(VerifiedPath(receiptRoot), planHash, receiptPins);
            var reclaim = ReadReceipt(StagingReclaimPath(receiptRoot), planHash, receiptPins);
            bool journalPending = BoundedJournalReclamation.HasIntent(JournalReclaimPath(receiptRoot));
            if (intent is null && !move || reclaim is not null && verified is null || journalPending && reclaim is null) throw Conflict();
            using var identity = journalPending ? null : PinIdentity(request, plan.IdentityHash);
            using var journal = journalPending ? null : OpenJournalEffects(state, request, manifest, plan);
            if (SnapshotInputChecks.FileIdentity(root, directory: true) != plan.Identities[""]) throw Conflict();
            if (move)
            {
                if (verified is not null || reclaim is not null || journalPending || HasEffects(receiptRoot, plan, manifest)) throw Conflict();
                using (var original = new TreePins(state.Basis.Snapshot.RootPath, manifest, root, plan.Identities, verifyContent: true, ct))
                {
                    if (intent is null)
                    {
                        // A crash between plan and binding is recoverable only
                        // while every immutable original byte still exists.
                        using var replay = JournalDeltaStore.OpenReadOnly(request.DeltaPath, request.Identity, new PinnedPlanningTree(manifest));
                        var rebuilt = BuildFiles(state.Basis.Snapshot.RootPath, manifest, replay.DescribeRestore(), replay, ct);
                        if (replay.Seal() != plan.Seal || !FilesEqual(rebuilt, plan.Files)) throw Conflict();
                        await WindowsProcessWorkspaceProvider.WriteAsync(IntentPath(receiptRoot), new Receipt(1, planHash), ct);
                        intent = ReadReceipt(IntentPath(receiptRoot), planHash, receiptPins) ?? throw Conflict();
                    }
                }
                ct.ThrowIfCancellationRequested(); RequireAbsent(state.OriginalPath);
                boundary?.Invoke("root-delta.before-move");
                ManagedRestoreFileAccess.Move(root, state.OriginalPath);
                boundary?.Invoke("root-delta.after-move");
            }
            RequireAbsent(state.Basis.Snapshot.RootPath);
            var items = manifest.Items.ToDictionary(i => i.Entry.Path, StringComparer.Ordinal);
            var mutable = plan.Files.Where(f => !FixedContent(f.Delta, items.GetValueOrDefault(f.Delta.Entry.Path))).ToArray();
            var newFiles = mutable.Where(f => f.BasisIdentity is null).ToDictionary(f => f.Delta.Entry.Path, StringComparer.Ordinal);
            var created = new Dictionary<Guid, FileReceipt>();
            var started = new Dictionary<Guid, FileReceipt>();
            var completed = new Dictionary<Guid, FileReceipt>();
            foreach (var file in mutable)
            {
                var entry = file.Delta.Entry;
                var creation = ReadFileReceipt(FilePath(receiptRoot, "file", entry.Id), planHash, entry.Id, receiptPins);
                if (file.BasisIdentity is not null && creation is not null) throw Conflict();
                if (creation is not null) created.Add(entry.Id, creation);
                var write = ReadFileReceipt(FilePath(receiptRoot, "write", entry.Id), planHash, entry.Id, receiptPins);
                var complete = ReadFileReceipt(FilePath(receiptRoot, "complete", entry.Id), planHash, entry.Id, receiptPins);
                string? expected = file.BasisIdentity ?? creation?.Identity;
                if (write is not null && write.Identity != expected || complete is not null && (write is null || complete.Identity != expected) ||
                    verified is not null && complete is null) throw Conflict();
                if (write is not null) started.Add(entry.Id, write);
                if (complete is not null) completed.Add(entry.Id, complete);
            }
            var deletedAtOriginal = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < plan.Deleted.Length; i++)
            {
                string relative = plan.Deleted[i], originalPath = PathOf(state.OriginalPath, relative), stage = StagePath(receiptRoot, i);
                bool staged = Exists(stage), original = Exists(originalPath);
                bool replacement = newFiles.TryGetValue(relative, out var next) && created.ContainsKey(next.Delta.Entry.Id);
                if (staged)
                {
                    if (original && !replacement) throw Conflict();
                    deletedPins[i] = ManagedRestoreFileAccess.Read(stage, moving: true);
                }
                else if (reclaim is null)
                {
                    if (!original || replacement || verified is not null) throw Conflict();
                    deletedAtOriginal.Add(relative);
                    deletedPins[i] = ManagedRestoreFileAccess.Read(originalPath, moving: true);
                }
                else if (original && !replacement) throw Conflict();
                if (deletedPins[i] is { } pin)
                {
                    ManagedRestoreFileAccess.Require(pin, plan.Identities[relative]);
                    if (pin.Length != items[relative].Entry.Length || Hash(pin) != items[relative].Hash) throw Conflict();
                }
            }
            // Stable leaves and all original directories stay pinned while
            // mutable leaves individually change from proof to exclusive write.
            var stableItems = plan.Directories.Select(d => new OwnedDirectoryItem(ToSnapshot(d.Entry), null))
                .Concat(plan.Files.Where(f => FixedContent(f.Delta, items.GetValueOrDefault(f.Delta.Entry.Path)))
                    .Select(f => new OwnedDirectoryItem(ToSnapshot(f.Delta.Entry), f.FinalHash))).ToArray();
            var stableManifest = manifest with { Items = stableItems };
            var extras = new HashSet<string>(deletedAtOriginal, StringComparer.Ordinal);
            foreach (var file in mutable)
                if (Exists(PathOf(state.OriginalPath, file.Delta.Entry.Path))) extras.Add(file.Delta.Entry.Path);
            using var stable = new TreePins(state.OriginalPath, stableManifest, root, plan.Identities, verifyContent: true, ct, extras);
            // Validate all mutable current states before staging or rewriting
            // any leaf, including the originally private bytes before intent.
            foreach (var file in mutable)
            {
                ct.ThrowIfCancellationRequested(); var entry = file.Delta.Entry;
                string target = PathOf(state.OriginalPath, entry.Path);
                if (file.BasisIdentity is null && !created.ContainsKey(entry.Id))
                {
                    if (Exists(target) && !deletedAtOriginal.Contains(entry.Path)) throw Conflict();
                    continue;
                }
                using var pin = ManagedRestoreFileAccess.Read(target);
                string expected = file.BasisIdentity ?? created[entry.Id].Identity;
                ManagedRestoreFileAccess.Require(pin, expected);
                ValidateMutable(pin, file, items, started.ContainsKey(entry.Id), completed.ContainsKey(entry.Id));
            }
            boundary?.Invoke("root-delta.after-proof");
            for (int i = 0; i < plan.Deleted.Length; i++)
            {
                if (!deletedAtOriginal.Contains(plan.Deleted[i])) continue;
                ct.ThrowIfCancellationRequested(); RequireAbsent(StagePath(receiptRoot, i));
                boundary?.Invoke("root-delta.before-stage");
                ManagedRestoreFileAccess.Move(deletedPins[i]!, StagePath(receiptRoot, i));
                extras.Remove(plan.Deleted[i]);
                boundary?.Invoke("root-delta.after-stage");
            }
            stable.RequireMembership(extras);
            byte[] buffer = new byte[1024 * 1024];
            foreach (var file in mutable)
            {
                ct.ThrowIfCancellationRequested(); var entry = file.Delta.Entry;
                string target = PathOf(state.OriginalPath, entry.Path);
                if (file.BasisIdentity is null && !created.ContainsKey(entry.Id))
                {
                    if (journal is null) throw Conflict();
                    RequireAbsent(target);
                    using var output = ManagedRestoreFileAccess.CreateFile(target, Path.GetDirectoryName(target) == state.OriginalPath ? root : null,
                        () => boundary?.Invoke("root-delta.file.before-create"));
                    output.Flush(true); boundary?.Invoke("root-delta.file.after-create-before-receipt");
                    var receipt = new FileReceipt(1, planHash, entry.Id, ManagedRestoreFileAccess.Identity(output));
                    await WindowsProcessWorkspaceProvider.WriteAsync(FilePath(receiptRoot, "file", entry.Id), receipt, ct);
                    created.Add(entry.Id, ReadFileReceipt(FilePath(receiptRoot, "file", entry.Id), planHash, entry.Id, receiptPins) ?? throw Conflict());
                    extras.Add(entry.Path);
                    boundary?.Invoke("root-delta.file.after-create-receipt");
                }
                string expected = file.BasisIdentity ?? created[entry.Id].Identity;
                if (completed.ContainsKey(entry.Id))
                {
                    using var done = ManagedRestoreFileAccess.Read(target);
                    ManagedRestoreFileAccess.Require(done, expected);
                    if (done.Length != entry.Length || Hash(done) != file.FinalHash) throw Conflict();
                    continue;
                }
                if (journal is null) throw Conflict();
                using (var output = ManagedRestoreFileAccess.Write(target, expected,
                    pin => ValidateMutable(pin, file, items, started.ContainsKey(entry.Id), complete: false)))
                {
                    if (!started.ContainsKey(entry.Id))
                    {
                        await WindowsProcessWorkspaceProvider.WriteAsync(FilePath(receiptRoot, "write", entry.Id), new FileReceipt(1, planHash, entry.Id, expected), ct);
                        started.Add(entry.Id, ReadFileReceipt(FilePath(receiptRoot, "write", entry.Id), planHash, entry.Id, receiptPins) ?? throw Conflict());
                        boundary?.Invoke("root-delta.file.after-write-intent");
                    }
                    bool final = output.Length == entry.Length && Hash(output) == file.FinalHash;
                    if (!final)
                    {
                        boundary?.Invoke("root-delta.file.before-ranges-write");
                        output.SetLength(entry.BaseLength); output.SetLength(entry.Length);
                        foreach (var range in file.Delta.Ranges)
                            for (int offset = 0; offset < range.Length;)
                            {
                                ct.ThrowIfCancellationRequested();
                                int count = Math.Min(buffer.Length, range.Length - offset);
                                journal.ReadRestoreRange(entry.Id, range.Offset + offset, buffer.AsSpan(0, count), plan.Seal);
                                output.Position = range.Offset + offset; output.Write(buffer, 0, count); offset += count;
                                boundary?.Invoke("root-delta.file.after-range-write");
                            }
                    }
                    if (output.Length != entry.Length || Hash(output) != file.FinalHash) throw Conflict();
                    ManagedRestoreFileAccess.Metadata(output, entry.Attributes, entry.CreationTime, entry.LastAccessTime, entry.LastWriteTime);
                    // A prior process may have left the final bytes in the OS
                    // cache before its durable flush. Equality permits skipping
                    // redo, never skipping this completion's durability fence.
                    output.Flush(true); boundary?.Invoke("root-delta.file.after-ranges-flush");
                    await WindowsProcessWorkspaceProvider.WriteAsync(FilePath(receiptRoot, "complete", entry.Id), new FileReceipt(1, planHash, entry.Id, expected), ct);
                    completed.Add(entry.Id, ReadFileReceipt(FilePath(receiptRoot, "complete", entry.Id), planHash, entry.Id, receiptPins) ?? throw Conflict());
                }
                boundary?.Invoke("root-delta.file.after-complete");
            }
            stable.RequireMembership(extras); RequireAbsent(state.Basis.Snapshot.RootPath);
            var finalItems = plan.Directories.Select(d => new OwnedDirectoryItem(ToSnapshot(d.Entry), null))
                .Concat(plan.Files.Select(f => new OwnedDirectoryItem(ToSnapshot(f.Delta.Entry), f.FinalHash))).ToArray();
            var finalIdentities = new Dictionary<string, string>(plan.Identities, StringComparer.Ordinal);
            foreach (var file in newFiles.Values) finalIdentities[file.Delta.Entry.Path] = created[file.Delta.Entry.Id].Identity;
            using var finalPins = new TreePins(state.OriginalPath, manifest with { Items = finalItems }, root, finalIdentities, verifyContent: true, ct);
            if (!journalPending) RequireSeal(request, plan.Seal);
            finalPins.ApplyMetadata();
            if (verified is null)
            {
                await WindowsProcessWorkspaceProvider.WriteAsync(VerifiedPath(receiptRoot), new Receipt(1, planHash), ct);
                _ = ReadReceipt(VerifiedPath(receiptRoot), planHash, receiptPins) ?? throw Conflict();
            }
            boundary?.Invoke("root-delta.after-verified");
            if (reclaim is null)
            {
                await WindowsProcessWorkspaceProvider.WriteAsync(StagingReclaimPath(receiptRoot), new Receipt(1, planHash), ct);
                _ = ReadReceipt(StagingReclaimPath(receiptRoot), planHash, receiptPins) ?? throw Conflict();
            }
            boundary?.Invoke("root-delta.reclaim.intent");
            for (int i = 0; i < deletedPins.Length; i++)
            {
                if (deletedPins[i] is not { } file) continue;
                ct.ThrowIfCancellationRequested(); ManagedRestoreFileAccess.Delete(file); file.Dispose(); deletedPins[i] = null;
                RequireAbsent(StagePath(receiptRoot, i)); boundary?.Invoke("root-delta.reclaim.file");
            }
            finalPins.RequireMembership(); RequireAbsent(state.Basis.Snapshot.RootPath);
            RequireArtifacts(receiptRoot, plan, manifest, initial: false);
            for (int i = 0; i < plan.Deleted.Length; i++) RequireAbsent(StagePath(receiptRoot, i));
            journal?.Dispose();
            void ReclaimBoundary(string stage)
            {
                if (stage == "intent") identity?.Dispose();
                boundary?.Invoke("root-delta.journal." + stage);
            }
            if (plan.JournalMode == "bounded")
                BoundedJournalReclamation.ReclaimDirectory(request.DeltaPath, plan.Seal, ["identity.json"], JournalReclaimPath(receiptRoot),
                    ReclaimBoundary, path => new WindowsJournalDirectoryGuard(path, BoundedJournalReclamation.GuardName));
            else
                LatestJournalReclamation.ReclaimDirectory(request.DeltaPath, plan.Seal, ["identity.json"], JournalReclaimPath(receiptRoot),
                    ReclaimBoundary, path => new WindowsJournalDirectoryGuard(path));
            boundary?.Invoke("root-delta.after-reclaim");
        }
        finally
        {
            foreach (var pin in deletedPins) pin?.Dispose();
            foreach (var pin in receiptPins) pin.Dispose();
        }
    }

    internal static RestoreFile[] BuildFiles(string basisRoot, OwnedDirectoryManifest manifest, DeltaRestoreEntry[] entries,
        JournalDeltaStore journal, CancellationToken ct)
    {
        var items = manifest.Items.ToDictionary(i => i.Entry.Path, StringComparer.Ordinal);
        var files = new List<RestoreFile>(); byte[] buffer = new byte[1024 * 1024];
        foreach (var delta in entries.Where(e => !e.Entry.Directory).OrderBy(e => e.Entry.Path, StringComparer.Ordinal))
        {
            ct.ThrowIfCancellationRequested(); var entry = delta.Entry;
            var original = entry.BasePath is null ? null : items.GetValueOrDefault(entry.BasePath);
            string? identity = null; string retained = EmptyHash;
            if (original is not null)
            {
                using var pin = ManagedRestoreFileAccess.Read(PathOf(basisRoot, original.Entry.Path));
                identity = ManagedRestoreFileAccess.Identity(pin);
                retained = FixedContent(delta, original) ? original.Hash! : RetainedHash(pin, entry.BaseLength, delta.Ranges);
            }
            if (FixedContent(delta, original)) { files.Add(new(delta, original!.Hash!, retained, identity)); continue; }
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            for (long offset = 0; offset < entry.Length;)
            {
                ct.ThrowIfCancellationRequested();
                int count = journal.Read(entry.Id, offset, buffer.AsSpan(0, (int)Math.Min(buffer.Length, entry.Length - offset)));
                if (count == 0) throw Conflict(); hash.AppendData(buffer, 0, count); offset += count;
            }
            files.Add(new(delta, Convert.ToHexStringLower(hash.GetHashAndReset()), retained, identity));
        }
        return files.ToArray();
    }

    private static void ValidatePlan(ManagedDirectoryState state, OverlayServeRequest request, OwnedDirectoryManifest manifest, Plan plan)
    {
        if (plan.Version != 1 || !Supported(state) || plan.Basis != state.Basis || plan.OriginalPath != state.OriginalPath ||
            plan.DeltaPath != request.DeltaPath || plan.IdentityHash != IdentityHash(request.Identity) || plan.Seal is null ||
            plan.Seal.DurableSequence < 0 || plan.JournalMode is not ("latest" or "bounded") || plan.Identities is null ||
            plan.Identities.Count != manifest.Items.Length || plan.Directories is null || plan.Files is null || plan.Deleted is null ||
            manifest.Items.Any(i => !plan.Identities.TryGetValue(i.Entry.Path, out string? id) || string.IsNullOrEmpty(id)) ||
            plan.Files.Any(f => f is null || !IsHash(f.FinalHash) || !IsHash(f.RetainedHash)) ||
            !EligibleEntries(manifest, plan.Directories.Concat(plan.Files.Select(f => f.Delta)).ToArray())) throw Conflict();
        if (plan.Directories.Any(d => !d.Entry.Directory) || plan.Files.Any(f => f.Delta.Entry.Directory) ||
            !plan.Directories.Select(d => d.Entry.Path).SequenceEqual(plan.Directories.Select(d => d.Entry.Path).Order(StringComparer.Ordinal)) ||
            !plan.Files.Select(f => f.Delta.Entry.Path).SequenceEqual(plan.Files.Select(f => f.Delta.Entry.Path).Order(StringComparer.Ordinal))) throw Conflict();
        var items = manifest.Items.ToDictionary(i => i.Entry.Path, StringComparer.Ordinal);
        foreach (var file in plan.Files)
        {
            var entry = file.Delta.Entry;
            if (entry.BasePath is null ? file.BasisIdentity is not null || file.RetainedHash != EmptyHash :
                file.BasisIdentity != plan.Identities[entry.BasePath]) throw Conflict();
            if (FixedContent(file.Delta, items.GetValueOrDefault(entry.Path)) &&
                (file.FinalHash != items[entry.Path].Hash || file.RetainedHash != file.FinalHash)) throw Conflict();
        }
        var retained = plan.Files.Where(f => f.Delta.Entry.BasePath is not null).Select(f => f.Delta.Entry.BasePath!).ToHashSet(StringComparer.Ordinal);
        var deleted = manifest.Items.Where(i => !i.Entry.Directory && !retained.Contains(i.Entry.Path)).Select(i => i.Entry.Path).Order(StringComparer.Ordinal);
        if (!deleted.SequenceEqual(plan.Deleted)) throw Conflict();
    }

    private static JournalDeltaStore OpenJournalEffects(ManagedDirectoryState state, OverlayServeRequest request, OwnedDirectoryManifest manifest, Plan plan)
    {
        if (JournalMode(request) != plan.JournalMode) throw Conflict();
        var journal = ManagedRestoreJournalProof.Open(request, manifest);
        try
        {
            var entries = journal.DescribeRestore().OrderBy(e => e.Entry.Path, StringComparer.Ordinal).ToArray();
            var expected = plan.Directories.Concat(plan.Files.Select(f => f.Delta)).OrderBy(e => e.Entry.Path, StringComparer.Ordinal).ToArray();
            if (journal.Seal() != plan.Seal || !EligibleEntries(manifest, entries) || !EntriesEqual(entries, expected)) throw Conflict();
            journal.PrepareRestoreReads(plan.Seal); return journal;
        }
        catch { journal.Dispose(); throw; }
    }

    internal static void ValidateMutable(FileStream pin, RestoreFile file, Dictionary<string, OwnedDirectoryItem> items, bool started, bool complete)
    {
        var entry = file.Delta.Entry;
        if (complete)
        { if (pin.Length != entry.Length || Hash(pin) != file.FinalHash) throw Conflict(); return; }
        if (!started)
        {
            var original = entry.BasePath is null ? null : items[entry.BasePath];
            if (pin.Length != (original?.Entry.Length ?? 0) || Hash(pin) != (original?.Hash ?? EmptyHash)) throw Conflict();
        }
        // An admitted but incomplete effect may redo its private ranges; every
        // retained byte remains immutable. Truncate/reextend never revives tail.
        if (RetainedHash(pin, entry.BaseLength, file.Delta.Ranges) != file.RetainedHash) throw Conflict();
    }

    private static Receipt? ReadReceipt(string path, string hash, List<FileStream> pins)
    {
        if (!Exists(path)) return null;
        var pin = ManagedRestoreFileAccess.Read(path); pins.Add(pin);
        var receipt = JsonSerializer.Deserialize<Receipt>(pin, Json) ?? throw Conflict();
        if (receipt.Version != 1 || receipt.PlanHash != hash) throw Conflict(); return receipt;
    }
    private static FileReceipt? ReadFileReceipt(string path, string hash, Guid id, List<FileStream> pins)
    {
        if (!Exists(path)) return null;
        var pin = ManagedRestoreFileAccess.Read(path); pins.Add(pin);
        var receipt = JsonSerializer.Deserialize<FileReceipt>(pin, Json) ?? throw Conflict();
        if (receipt.Version != 1 || receipt.PlanHash != hash || receipt.EntryId != id || string.IsNullOrEmpty(receipt.Identity)) throw Conflict();
        return receipt;
    }
    private static bool HasEffects(string root, Plan plan, OwnedDirectoryManifest manifest)
    {
        var controls = AllowedArtifacts(root, plan, manifest).Where(p => p.EndsWith(".json", StringComparison.Ordinal)).ToHashSet(StringComparer.Ordinal);
        return Directory.EnumerateFileSystemEntries(root, Prefix + "*")
            .Any(path => path != PlanPath(root) && path != IntentPath(root) && !ManagedRestoreControlFiles.IsPublicationTemporary(path, controls));
    }
    private static HashSet<string> AllowedArtifacts(string root, Plan plan, OwnedDirectoryManifest manifest)
    {
        var items = manifest.Items.ToDictionary(i => i.Entry.Path, StringComparer.Ordinal);
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { PlanPath(root), IntentPath(root), VerifiedPath(root), StagingReclaimPath(root), JournalReclaimPath(root) };
        if (plan.JournalMode == "bounded") allowed.Add(JournalReclaimPath(root) + ".pending");
        for (int i = 0; i < plan.Deleted.Length; i++) allowed.Add(StagePath(root, i));
        foreach (var file in plan.Files)
        {
            var entry = file.Delta.Entry;
            // Receipts for immutable survivors are never valid evidence.
            if (FixedContent(file.Delta, items.GetValueOrDefault(entry.Path))) continue;
            if (file.BasisIdentity is null) allowed.Add(FilePath(root, "file", entry.Id));
            allowed.Add(FilePath(root, "write", entry.Id)); allowed.Add(FilePath(root, "complete", entry.Id));
        }
        return allowed;
    }
    private static void RequireArtifacts(string root, Plan plan, OwnedDirectoryManifest manifest, bool initial)
    {
        var allowed = AllowedArtifacts(root, plan, manifest);
        var controls = allowed.Where(p => p.EndsWith(".json", StringComparison.Ordinal)).ToHashSet(StringComparer.Ordinal);
        foreach (string path in Directory.EnumerateFileSystemEntries(root, Prefix + "*"))
        {
            WorkspaceStore.EnsurePlainPath(path);
            if (ManagedRestoreControlFiles.IsPublicationTemporary(path, controls)) continue;
            if (initial || !allowed.Contains(path) || Directory.Exists(path)) throw Conflict();
        }
    }
    internal static bool FilesEqual(RestoreFile[] left, RestoreFile[] right) => left.Length == right.Length &&
        left.Zip(right).All(pair => EntryEqual(pair.First.Delta, pair.Second.Delta) && pair.First.FinalHash == pair.Second.FinalHash &&
            pair.First.RetainedHash == pair.Second.RetainedHash && pair.First.BasisIdentity == pair.Second.BasisIdentity);
    internal static bool EntriesEqual(DeltaRestoreEntry[] left, DeltaRestoreEntry[] right) => left.Length == right.Length && left.Zip(right).All(p => EntryEqual(p.First, p.Second));
    private static bool EntryEqual(DeltaRestoreEntry left, DeltaRestoreEntry right) => left.Entry == right.Entry && left.Ranges.SequenceEqual(right.Ranges);
    internal static bool IsHash(string hash) => hash is { Length: 64 } && hash.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    internal static SnapshotEntry ToSnapshot(DeltaEntry entry) => new(entry.Path, entry.Directory, entry.Length, entry.Attributes,
        entry.CreationTime, entry.LastAccessTime, entry.LastWriteTime, entry.UnixMode);
    private static string Parent(string path) { int index = path.LastIndexOf('/'); return index < 0 ? "" : path[..index]; }
    private static string PathOf(string root, string relative)
    {
        if (JournalDeltaStore.Normalize(relative) != relative) throw Conflict();
        string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)); WorkspaceStore.EnsurePlainPath(path); return path;
    }
    internal static string Hash(FileStream file) { file.Position = 0; return Convert.ToHexStringLower(SHA256.HashData(file)); }
    internal static string RetainedHash(FileStream input, long length, DeltaRestoreRange[] ranges)
    {
        if (input.Length < length) throw Conflict();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[65536]; long cursor = 0;
        foreach (var range in ranges.Append(new(length, 0)))
        {
            long end = Math.Min(length, range.Offset); input.Position = cursor;
            while (cursor < end)
            {
                int read = input.Read(buffer, 0, (int)Math.Min(buffer.Length, end - cursor));
                if (read == 0) throw Conflict(); hash.AppendData(buffer, 0, read); cursor += read;
            }
            cursor = Math.Min(length, Math.Max(cursor, range.Offset + range.Length));
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    // Used only for the pre-effect plan/binding publication gap, inside a
    // complete original TreePins proof. Opening another basis root lease would
    // conflict with the caller's already acquired DELETE-capable root handle.
    internal sealed class PinnedPlanningTree : ISnapshotTree
    {
        private readonly Dictionary<string, SnapshotEntry> entries;
        private readonly Dictionary<string, SnapshotEntry[]> children;
        public SnapshotReference Snapshot { get; }
        internal PinnedPlanningTree(OwnedDirectoryManifest manifest)
        {
            Snapshot = manifest.Snapshot;
            entries = manifest.Items.ToDictionary(i => i.Entry.Path, i => i.Entry, StringComparer.OrdinalIgnoreCase);
            children = manifest.Items.Where(i => i.Entry.Path.Length != 0).GroupBy(i => Parent(i.Entry.Path), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Select(i => i.Entry).ToArray(), StringComparer.OrdinalIgnoreCase);
        }
        public void EnsureAvailable() { }
        public SnapshotEntry? Stat(string path) => entries.GetValueOrDefault(JournalDeltaStore.Normalize(path));
        public IReadOnlyList<SnapshotEntry> List(string path) => children.GetValueOrDefault(JournalDeltaStore.Normalize(path)) ?? [];
        public int Read(string path, long offset, Span<byte> destination)
        {
            using var pin = ManagedRestoreFileAccess.Read(PathOf(Snapshot.RootPath, path));
            return RandomAccess.Read(pin.SafeFileHandle, destination, offset);
        }
    }
}
