using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using Splinter.Core;
using Splinter.Runtime;
using static Splinter.Platform.Windows.ManagedRootDeltaDirectoryRestore;
using static Splinter.Platform.Windows.ManagedUnchangedDirectoryRestore;

namespace Splinter.Platform.Windows;

// Bulk, preserving namespace moves precede all range writes and reclamation.
// Existing root-delta v1 plans stay on their original same-directory route.
internal static class ManagedTopologyDirectoryRestore
{
    private sealed record Plan(int Version, GeneratedDirectoryReference Basis, string OriginalPath, string DeltaPath,
        string IdentityHash, SnapshotDeltaSeal Seal, string JournalMode, Dictionary<string, string> Identities,
        DeltaRestoreEntry[] Directories, RestoreFile[] Files, ManagedRestoreTopology.Model Topology);
    private sealed record Receipt(int Version, string PlanHash);
    private sealed record FileReceipt(int Version, string PlanHash, Guid EntryId, string Identity);
    private sealed record Cursor(int Version, string PlanHash, int Completed);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const string Prefix = "restore-topology-";
    private static string Named(string root, string name) => Path.Combine(root, Prefix + name);
    private static string PlanPath(string root) => Named(root, "plan.json");
    private static string FilePath(string root, string kind, Guid id) => Named(root, kind + "-" + id.ToString("N") + ".json");
    private static WorkspaceException Conflict() => ManagedRestoreTopology.Conflict();
    private static string At(string root, string path) => ManagedRestoreTopology.At(root, path);
    private static string Stage(string root, int unit) => ManagedRestoreTopology.Stage(root, unit);
    private static DeltaRestoreEntry[] Entries(Plan plan) => plan.Directories.Concat(plan.Files.Select(f => f.Delta)).OrderBy(e => e.Entry.Path, StringComparer.Ordinal).ToArray();
    private static bool Supported(ManagedDirectoryState state) => state.ApplicationKind == "unity" && state.DirectoryKind == "Library" && state.Basis.RelativePath == "Library";

    internal static bool HasPlan(string root)
    {
        bool exists = File.Exists(PlanPath(root));
        if (!exists)
            foreach (string path in Directory.EnumerateFileSystemEntries(root, Prefix + "*"))
                if (!ManagedRestoreControlFiles.IsPublicationTemporary(path, new HashSet<string>(StringComparer.Ordinal) { PlanPath(root) })) throw Conflict();
        return exists;
    }
    internal static bool Eligible(ManagedDirectoryState state, OverlayServeRequest request, OwnedDirectoryManifest manifest, DeltaRestoreEntry[] entries)
    {
        if (!Supported(state) || JournalMode(request) is null) return false;
        _ = ManagedRestoreTopology.Build(manifest, entries); return true;
    }
    // Fresh selection only: a started topology plan always keeps its protocol.
    // General restore moves every surviving source-backed file once. Topology
    // moves the root once, each live split unit twice, and each deleted unit
    // once. Require fewer moves and independently cap topology control writes
    // at the general route's 128-file completion budget. New files cannot
    // enlarge that budget; new directories cost a cursor and identity receipt.
    // These structural floors do not guarantee a wall-clock improvement.
    internal static bool PreferFresh(OwnedDirectoryManifest manifest, DeltaRestoreEntry[] entries)
    {
        var topology = ManagedRestoreTopology.Build(manifest, entries);
        long generalMoves = entries.LongCount(e => !e.Entry.Directory && e.Entry.BasePath is not null);
        long topologyMoves = 1L + topology.Units.Skip(1).Sum(u => u.FinalPath is null ? 1L : 2L);
        long generalCompletionBudget = generalMoves == 0 ? 0 : 1L + (generalMoves - 1) / ManagedDirectoryRestore.CursorBatchSize;
        long topologyPublications = topology.Operations.LongLength + topology.Operations.LongCount(o => o.Kind == "directory");
        return topologyMoves < generalMoves && topologyPublications <= generalCompletionBudget;
    }
    internal static async Task CreateAsync(ManagedDirectoryState state, OverlayServeRequest request, OwnedDirectoryManifest manifest,
        SnapshotDeltaSeal seal, DeltaRestoreEntry[] entries, OwnedDirectorySnapshotTree basis, JournalDeltaStore journal,
        CancellationToken ct, Action<string>? boundary)
    {
        ValidateManifest(state, manifest); RequireAbsent(state.OriginalPath);
        string rr = state.Basis.ReceiptDirectory;
        if (!Eligible(state, request, manifest, entries) || HasPlan(rr)) throw Conflict();
        var files = BuildFiles(state.Basis.Snapshot.RootPath, manifest, entries, journal, ct);
        var topology = ManagedRestoreTopology.Build(manifest, entries);
        Directory.CreateDirectory(Path.GetDirectoryName(state.OriginalPath)!);
        using var parents = new ManagedRestoreFileAccess(state.Basis.Snapshot.RootPath, state.OriginalPath, Stage(rr, 0));
        using var root = basis.TransferRootToRestore(() => boundary?.Invoke("topology.root-unpinned"), allowChildMutations: true);
        using (var original = new TreePins(state.Basis.Snapshot.RootPath, manifest, root, null, verifyContent: false, ct))
        {
            var plan = new Plan(1, state.Basis, state.OriginalPath, request.DeltaPath, IdentityHash(request.Identity), seal,
                JournalMode(request) ?? throw Conflict(), original.Identities,
                entries.Where(e => e.Entry.Directory).OrderBy(e => e.Entry.Path, StringComparer.Ordinal).ToArray(), files, topology);
            ValidatePlan(state, request, manifest, plan); RequireArtifacts(rr, plan, manifest, initial: true);
            if (journal.Seal() != seal) throw Conflict();
            await WindowsProcessWorkspaceProvider.WriteAsync(PlanPath(rr), plan, ct);
            using var pin = ManagedRestoreFileAccess.Read(PlanPath(rr)); string hash = Hash(pin);
            if (hash != Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(plan, Json)))) throw Conflict();
            await WindowsProcessWorkspaceProvider.WriteAsync(Named(rr, "intent.json"), new Receipt(1, hash), ct);
            boundary?.Invoke("topology.after-plan");
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
        using var parents = new ManagedRestoreFileAccess(state.Basis.Snapshot.RootPath, state.OriginalPath, Stage(state.Basis.ReceiptDirectory, 0));
        using var root = ManagedRestoreFileAccess.DirectoryHandle(move ? state.Basis.Snapshot.RootPath : state.OriginalPath, moving: move, allowChildMutations: true);
        await FinishAsync(state, request, manifest, root, move, ct, boundary);
    }

    private static async Task FinishAsync(ManagedDirectoryState state, OverlayServeRequest request, OwnedDirectoryManifest manifest,
        SafeFileHandle root, bool move, CancellationToken ct, Action<string>? boundary)
    {
        string rr = state.Basis.ReceiptDirectory;
        using var planPin = ManagedRestoreFileAccess.Read(PlanPath(rr));
        var plan = JsonSerializer.Deserialize<Plan>(planPin, Json) ?? throw Conflict(); string planHash = Hash(planPin);
        ValidatePlan(state, request, manifest, plan); RequireArtifacts(rr, plan, manifest, initial: false);
        var pins = new List<FileStream>();
        try
        {
            var intent = ReadReceipt(Named(rr, "intent.json"), planHash, pins);
            var verified = ReadReceipt(Named(rr, "verified.json"), planHash, pins);
            var reclaim = ReadReceipt(Named(rr, "staging-reclaim.json"), planHash, pins);
            bool journalPending = BoundedJournalReclamation.HasIntent(Named(rr, "journal-reclaim.bin"));
            if (intent is null && !move || reclaim is not null && verified is null || journalPending && reclaim is null) throw Conflict();
            using var identity = journalPending ? null : PinIdentity(request, plan.IdentityHash);
            using var journal = journalPending ? null : OpenJournal(request, manifest, plan);
            if (SnapshotInputChecks.FileIdentity(root, directory: true) != plan.Identities[""]) throw Conflict();
            var cursor = await WindowsProcessWorkspaceProvider.ReadAsync<Cursor>(Named(rr, "cursor.json"), ct, waitForPublication: false);
            if (cursor is not null && (cursor.Version != 1 || cursor.PlanHash != planHash || cursor.Completed < 0 || cursor.Completed > plan.Topology.Operations.Length)) throw Conflict();
            if (!move && cursor is null) throw Conflict();
            var newDirectories = new Dictionary<Guid, string>(); var created = new Dictionary<Guid, string>();
            var started = new Dictionary<Guid, string>(); var completed = new Dictionary<Guid, string>();
            foreach (var directory in plan.Directories.Where(d => d.Entry.BasePath is null))
            {
                var receipt = ReadFileReceipt(FilePath(rr, "directory", directory.Entry.Id), planHash, directory.Entry.Id, pins);
                if (receipt is not null) newDirectories.Add(directory.Entry.Id, receipt.Identity);
            }
            var items = manifest.Items.ToDictionary(i => i.Entry.Path, StringComparer.Ordinal);
            var mutable = plan.Files.Where(f => !FixedContent(f.Delta, f.Delta.Entry.BasePath is { } b ? items[b] : null)).ToArray();
            foreach (var file in mutable)
            {
                var e = file.Delta.Entry;
                var creation = ReadFileReceipt(FilePath(rr, "file", e.Id), planHash, e.Id, pins);
                if (file.BasisIdentity is not null && creation is not null) throw Conflict();
                if (creation is not null) created.Add(e.Id, creation.Identity);
                var write = ReadFileReceipt(FilePath(rr, "write", e.Id), planHash, e.Id, pins);
                var done = ReadFileReceipt(FilePath(rr, "complete", e.Id), planHash, e.Id, pins);
                string? expected = file.BasisIdentity ?? creation?.Identity;
                if (write is not null && write.Identity != expected || done is not null && (write is null || done.Identity != expected) || verified is not null && done is null) throw Conflict();
                if (write is not null) started.Add(e.Id, write.Identity);
                if (done is not null) completed.Add(e.Id, done.Identity);
            }
            if (move)
            {
                if (verified is not null || reclaim is not null || journalPending || cursor?.Completed > 0 ||
                    newDirectories.Count + created.Count + started.Count + completed.Count != 0 || Directory.EnumerateFileSystemEntries(rr, Prefix + "stage-*").Any()) throw Conflict();
                using (var original = new TreePins(state.Basis.Snapshot.RootPath, manifest, root, plan.Identities, verifyContent: true, ct))
                {
                    if (intent is null)
                    {
                        using var replay = JournalDeltaStore.OpenReadOnly(request.DeltaPath, request.Identity, new PinnedPlanningTree(manifest));
                        if (replay.Seal() != plan.Seal || !FilesEqual(BuildFiles(state.Basis.Snapshot.RootPath, manifest, replay.DescribeRestore(), replay, ct), plan.Files)) throw Conflict();
                        await WindowsProcessWorkspaceProvider.WriteAsync(Named(rr, "intent.json"), new Receipt(1, planHash), ct);
                        _ = ReadReceipt(Named(rr, "intent.json"), planHash, pins) ?? throw Conflict();
                    }
                }
                if (cursor is null)
                {
                    cursor = new Cursor(1, planHash, 0);
                    await WindowsProcessWorkspaceProvider.WriteAsync(Named(rr, "cursor.json"), cursor, ct);
                }
                RequireAbsent(state.OriginalPath); ct.ThrowIfCancellationRequested();
                boundary?.Invoke("topology.before-root-move"); ManagedRestoreFileAccess.Move(root, state.OriginalPath); boundary?.Invoke("topology.after-root-move");
            }
            RequireAbsent(state.Basis.Snapshot.RootPath);
            int position = cursor?.Completed ?? 0;
            if (position != plan.Topology.Operations.Length && (verified is not null || reclaim is not null || journalPending || created.Count + started.Count + completed.Count != 0)) throw Conflict();
            position = await RestoreTopologyAsync(state, manifest, plan, planHash, root, position, newDirectories, created,
                reclaim is not null, pins, ct, boundary);
            if (position != plan.Topology.Operations.Length) throw Conflict();
            using var rootWriteFence = ManagedRestoreFileAccess.PinDirectoryAgainstDataWrites(state.OriginalPath, root);
            var inventory = ManagedRestoreTopology.Inventory(plan.Topology, position, manifest, Entries(plan), plan.Identities,
                state.OriginalPath, rr, newDirectories, created, reclaim is not null);
            var mutablePaths = mutable.Select(f => At(state.OriginalPath, f.Delta.Entry.Path)).ToHashSet(StringComparer.Ordinal);
            using var forest = new ManagedRestoreTopology.ForestPins(inventory, state.OriginalPath, rr, root, mutablePaths, content: true, ct);
            foreach (var file in mutable)
            {
                var e = file.Delta.Entry; string target = At(state.OriginalPath, e.Path);
                if (file.BasisIdentity is null && !created.ContainsKey(e.Id)) { RequireAbsent(target); continue; }
                using var pin = ManagedRestoreFileAccess.Read(target);
                ManagedRestoreFileAccess.Require(pin, file.BasisIdentity ?? created[e.Id]);
                ValidateMutable(pin, file, items, started.ContainsKey(e.Id), completed.ContainsKey(e.Id));
            }
            boundary?.Invoke("topology.after-forest-proof");
            byte[] buffer = new byte[1024 * 1024];
            foreach (var file in mutable)
            {
                ct.ThrowIfCancellationRequested(); var e = file.Delta.Entry; string target = At(state.OriginalPath, e.Path);
                if (file.BasisIdentity is null && !created.ContainsKey(e.Id))
                {
                    if (journal is null) throw Conflict(); RequireAbsent(target);
                    using var output = ManagedRestoreFileAccess.CreateFile(target, Path.GetDirectoryName(target) == state.OriginalPath ? root : null,
                        () => boundary?.Invoke("topology.file.before-create"));
                    output.Flush(true); boundary?.Invoke("topology.file.after-create-before-receipt");
                    await WindowsProcessWorkspaceProvider.WriteAsync(FilePath(rr, "file", e.Id), new FileReceipt(1, planHash, e.Id, ManagedRestoreFileAccess.Identity(output)), ct);
                    string id = (ReadFileReceipt(FilePath(rr, "file", e.Id), planHash, e.Id, pins) ?? throw Conflict()).Identity;
                    created.Add(e.Id, id); inventory.Add(target, new(target, null, e, id, false));
                    boundary?.Invoke("topology.file.after-create-receipt");
                }
                string expected = file.BasisIdentity ?? created[e.Id];
                if (completed.ContainsKey(e.Id))
                {
                    using var done = ManagedRestoreFileAccess.Read(target); ManagedRestoreFileAccess.Require(done, expected);
                    if (done.Length != e.Length || Hash(done) != file.FinalHash) throw Conflict(); continue;
                }
                if (journal is null) throw Conflict();
                using (var output = ManagedRestoreFileAccess.Write(target, expected, pin => ValidateMutable(pin, file, items, started.ContainsKey(e.Id), complete: false)))
                {
                    if (!started.ContainsKey(e.Id))
                    {
                        await WindowsProcessWorkspaceProvider.WriteAsync(FilePath(rr, "write", e.Id), new FileReceipt(1, planHash, e.Id, expected), ct);
                        started.Add(e.Id, (ReadFileReceipt(FilePath(rr, "write", e.Id), planHash, e.Id, pins) ?? throw Conflict()).Identity);
                        boundary?.Invoke("topology.file.after-write-intent");
                    }
                    if (output.Length != e.Length || Hash(output) != file.FinalHash)
                    {
                        boundary?.Invoke("topology.file.before-ranges-write"); output.SetLength(e.BaseLength); output.SetLength(e.Length);
                        foreach (var range in file.Delta.Ranges)
                            for (int offset = 0; offset < range.Length;)
                            {
                                ct.ThrowIfCancellationRequested(); int count = Math.Min(buffer.Length, range.Length - offset);
                                journal.ReadRestoreRange(e.Id, range.Offset + offset, buffer.AsSpan(0, count), plan.Seal);
                                output.Position = range.Offset + offset; output.Write(buffer, 0, count); offset += count;
                                boundary?.Invoke("topology.file.after-range-write");
                            }
                    }
                    if (output.Length != e.Length || Hash(output) != file.FinalHash) throw Conflict();
                    ManagedRestoreFileAccess.Metadata(output, e.Attributes, e.CreationTime, e.LastAccessTime, e.LastWriteTime);
                    output.Flush(true); boundary?.Invoke("topology.file.after-ranges-flush");
                    await WindowsProcessWorkspaceProvider.WriteAsync(FilePath(rr, "complete", e.Id), new FileReceipt(1, planHash, e.Id, expected), ct);
                    completed.Add(e.Id, (ReadFileReceipt(FilePath(rr, "complete", e.Id), planHash, e.Id, pins) ?? throw Conflict()).Identity);
                }
                boundary?.Invoke("topology.file.after-complete");
            }
            forest.RequireMembership();
            var finalManifest = manifest with { Items = plan.Directories.Select(d => new OwnedDirectoryItem(ToSnapshot(d.Entry), null))
                .Concat(plan.Files.Select(f => new OwnedDirectoryItem(ToSnapshot(f.Delta.Entry), f.FinalHash))).ToArray() };
            var finalIds = Entries(plan).ToDictionary(d => d.Entry.Path, d => d.Entry.BasePath is { } b ? plan.Identities[b] :
                d.Entry.Directory ? newDirectories[d.Entry.Id] : created[d.Entry.Id], StringComparer.Ordinal);
            using var final = new TreePins(state.OriginalPath, finalManifest, root, finalIds, verifyContent: true, ct);
            if (!journalPending) RequireSeal(request, plan.Seal); final.ApplyMetadata();
            if (verified is null)
            {
                await WindowsProcessWorkspaceProvider.WriteAsync(Named(rr, "verified.json"), new Receipt(1, planHash), ct);
                _ = ReadReceipt(Named(rr, "verified.json"), planHash, pins) ?? throw Conflict();
            }
            boundary?.Invoke("topology.after-verified");
            if (reclaim is null)
            {
                await WindowsProcessWorkspaceProvider.WriteAsync(Named(rr, "staging-reclaim.json"), new Receipt(1, planHash), ct);
                _ = ReadReceipt(Named(rr, "staging-reclaim.json"), planHash, pins) ?? throw Conflict();
            }
            boundary?.Invoke("topology.reclaim.intent"); forest.Reclaim(boundary, ct);
            final.RequireMembership(); RequireAbsent(state.Basis.Snapshot.RootPath);
            if (Directory.EnumerateFileSystemEntries(rr, Prefix + "stage-*").Any()) throw Conflict();
            journal?.Dispose();
            void ReclaimBoundary(string stage) { if (stage == "intent") identity?.Dispose(); boundary?.Invoke("topology.journal." + stage); }
            if (plan.JournalMode == "bounded")
                BoundedJournalReclamation.ReclaimDirectory(request.DeltaPath, plan.Seal, ["identity.json"], Named(rr, "journal-reclaim.bin"), ReclaimBoundary,
                    path => new WindowsJournalDirectoryGuard(path, BoundedJournalReclamation.GuardName));
            else LatestJournalReclamation.ReclaimDirectory(request.DeltaPath, plan.Seal, ["identity.json"], Named(rr, "journal-reclaim.bin"), ReclaimBoundary,
                path => new WindowsJournalDirectoryGuard(path));
            boundary?.Invoke("topology.after-reclaim");
        }
        finally { foreach (var pin in pins) pin.Dispose(); }
    }

    private static async Task<int> RestoreTopologyAsync(ManagedDirectoryState state, OwnedDirectoryManifest manifest, Plan plan, string hash,
        SafeFileHandle root, int position, Dictionary<Guid, string> newDirectories, Dictionary<Guid, string> created, bool reclaim,
        List<FileStream> pins, CancellationToken ct, Action<string>? boundary)
    {
        string rr = state.Basis.ReceiptDirectory;
        bool Matches(int step)
        {
            try
            {
                RequireStages(rr, plan.Topology, step, reclaim);
                var members = ManagedRestoreTopology.Inventory(plan.Topology, step, manifest, Entries(plan), plan.Identities, state.OriginalPath, rr, newDirectories, created, reclaim);
                using var proof = new ManagedRestoreTopology.ForestPins(members, state.OriginalPath, rr, root, new HashSet<string>(StringComparer.Ordinal), content: false, ct);
                return true;
            }
            catch (Exception ex) when (ex is WorkspaceException or IOException or UnauthorizedAccessException) { return false; }
        }
        if (!Matches(position))
        {
            if (position == plan.Topology.Operations.Length || !Matches(position + 1)) throw Conflict();
            position++;
            await WindowsProcessWorkspaceProvider.WriteAsync(Named(rr, "cursor.json"), new Cursor(1, hash, position), ct);
        }
        var directoryIds = ManagedRestoreTopology.Inventory(plan.Topology, position, manifest, Entries(plan), plan.Identities,
                state.OriginalPath, rr, newDirectories, created, reclaim).Values
            .Where(m => m.Original?.Entry.Directory ?? m.Added!.Directory).ToDictionary(m => m.Path, m => m.Identity, StringComparer.Ordinal);
        void RequireParents(string target)
        {
            for (string? parent = Path.GetDirectoryName(target); parent is not null &&
                (parent == state.OriginalPath || parent.StartsWith(state.OriginalPath + Path.DirectorySeparatorChar, StringComparison.Ordinal)); parent = Path.GetDirectoryName(parent))
            {
                if (!directoryIds.TryGetValue(parent, out string? expected)) throw Conflict();
                if (parent == state.OriginalPath)
                { if (SnapshotInputChecks.FileIdentity(root, directory: true) != expected) throw Conflict(); }
                else
                {
                    using var pin = ManagedRestoreFileAccess.DirectoryHandle(parent, metadataWrite: false);
                    if (SnapshotInputChecks.FileIdentity(pin, directory: true) != expected) throw Conflict();
                }
            }
        }
        for (; position < plan.Topology.Operations.Length; position++)
        {
            ct.ThrowIfCancellationRequested(); var op = plan.Topology.Operations[position];
            string target = op.Kind == "stage" ? Stage(rr, op.Unit) : At(state.OriginalPath, op.Path);
            if (op.Kind == "directory")
            {
                using var access = new ManagedRestoreFileAccess(root, state.OriginalPath, target);
                RequireParents(target);
                RequireAbsent(target);
                using var directory = ManagedRestoreFileAccess.CreateDirectory(target, Path.GetDirectoryName(target) == state.OriginalPath ? root : null,
                    () => boundary?.Invoke("topology.new-directory-before-create"));
                boundary?.Invoke("topology.new-directory-after-create-before-receipt");
                string identity = SnapshotInputChecks.FileIdentity(directory, directory: true);
                await WindowsProcessWorkspaceProvider.WriteAsync(FilePath(rr, "directory", op.EntryId), new FileReceipt(1, hash, op.EntryId, identity), ct);
                newDirectories.Add(op.EntryId, (ReadFileReceipt(FilePath(rr, "directory", op.EntryId), hash, op.EntryId, pins) ?? throw Conflict()).Identity);
                directoryIds.Add(target, identity);
                boundary?.Invoke("topology.new-directory-after-receipt");
            }
            else
            {
                var unit = plan.Topology.Units[op.Unit];
                string source = op.Kind == "stage" ? At(state.OriginalPath, unit.BasePath) : Stage(rr, op.Unit);
                using var access = new ManagedRestoreFileAccess(root, state.OriginalPath, source, target);
                RequireParents(source); RequireParents(target);
                RequireAbsent(target);
                if (unit.Directory)
                {
                    using var directory = ManagedRestoreFileAccess.DirectoryHandle(source, moving: true);
                    if (SnapshotInputChecks.FileIdentity(directory, directory: true) != plan.Identities[unit.BasePath]) throw Conflict();
                    ManagedRestoreFileAccess.MoveRelative(directory, target, Path.GetDirectoryName(target) == state.OriginalPath ? root : null,
                        () => boundary?.Invoke("topology.before-" + op.Kind));
                    boundary?.Invoke("topology.after-" + op.Kind);
                    foreach (var pair in directoryIds.Where(p => p.Key == source || p.Key.StartsWith(source + Path.DirectorySeparatorChar, StringComparison.Ordinal)).ToArray())
                    { directoryIds.Remove(pair.Key); directoryIds.Add(target + pair.Key[source.Length..], pair.Value); }
                }
                else
                {
                    using var file = ManagedRestoreFileAccess.Read(source, moving: true); ManagedRestoreFileAccess.Require(file, plan.Identities[unit.BasePath]);
                    ManagedRestoreFileAccess.MoveRelative(file.SafeFileHandle, target, Path.GetDirectoryName(target) == state.OriginalPath ? root : null,
                        () => boundary?.Invoke("topology.before-" + op.Kind));
                    boundary?.Invoke("topology.after-" + op.Kind);
                }
            }
            await WindowsProcessWorkspaceProvider.WriteAsync(Named(rr, "cursor.json"), new Cursor(1, hash, position + 1), ct);
            boundary?.Invoke("topology.after-" + (op.Kind == "directory" ? "install" : op.Kind) + "-cursor");
        }
        return position;
    }

    private static void RequireStages(string root, ManagedRestoreTopology.Model model, int completed, bool reclaim)
    {
        var staged = new HashSet<int>();
        foreach (var op in model.Operations.Take(completed))
        { if (op.Kind == "stage") staged.Add(op.Unit); else if (op.Kind == "install") staged.Remove(op.Unit); }
        var expected = staged.Select(i => Stage(root, i)).ToHashSet(StringComparer.Ordinal);
        foreach (string actual in Directory.EnumerateFileSystemEntries(root, Prefix + "stage-*"))
            if (!expected.Remove(actual)) throw Conflict();
        if (expected.Count != 0 && !reclaim) throw Conflict();
        foreach (int index in staged)
            if (expected.Contains(Stage(root, index)) && model.Units[index].FinalPath is not null) throw Conflict();
    }
    private static void ValidatePlan(ManagedDirectoryState state, OverlayServeRequest request, OwnedDirectoryManifest manifest, Plan plan)
    {
        if (plan.Version != 1 || !Supported(state) || plan.Basis != state.Basis || plan.OriginalPath != state.OriginalPath || plan.DeltaPath != request.DeltaPath ||
            plan.IdentityHash != IdentityHash(request.Identity) || plan.Seal is null || plan.Seal.DurableSequence < 0 || plan.JournalMode is not ("latest" or "bounded") ||
            plan.Identities is null || plan.Identities.Count != manifest.Items.Length || plan.Directories is null || plan.Files is null || plan.Topology is null ||
            plan.Topology.Units is null || plan.Topology.Operations is null || plan.Topology.Units.Any(u => u is null) || plan.Topology.Operations.Any(o => o is null) ||
            manifest.Items.Any(i => !plan.Identities.TryGetValue(i.Entry.Path, out string? id) || string.IsNullOrEmpty(id)) ||
            plan.Directories.Any(d => d?.Entry is null || !d.Entry.Directory) || plan.Files.Any(f => f?.Delta?.Entry is null || f.Delta.Entry.Directory || !IsHash(f.FinalHash) || !IsHash(f.RetainedHash))) throw Conflict();
        if (!ManagedRestoreTopology.Equal(ManagedRestoreTopology.Build(manifest, Entries(plan)), plan.Topology)) throw Conflict();
        if (!plan.Files.Select(f => f.Delta.Entry.Path).SequenceEqual(plan.Files.Select(f => f.Delta.Entry.Path).Order(StringComparer.Ordinal))) throw Conflict();
        var items = manifest.Items.ToDictionary(i => i.Entry.Path, StringComparer.Ordinal);
        foreach (var file in plan.Files)
        {
            var entry = file.Delta.Entry;
            if (entry.BasePath is null)
            { if (file.BasisIdentity is not null || file.RetainedHash != Convert.ToHexStringLower(SHA256.HashData(Array.Empty<byte>()))) throw Conflict(); }
            else
            {
                if (file.BasisIdentity != plan.Identities[entry.BasePath]) throw Conflict();
                if (FixedContent(file.Delta, items[entry.BasePath]) && (file.FinalHash != items[entry.BasePath].Hash || file.RetainedHash != file.FinalHash)) throw Conflict();
            }
        }
    }
    private static JournalDeltaStore OpenJournal(OverlayServeRequest request, OwnedDirectoryManifest manifest, Plan plan)
    {
        if (JournalMode(request) != plan.JournalMode) throw Conflict();
        var journal = ManagedRestoreJournalProof.Open(request, manifest);
        try
        {
            if (journal.Seal() != plan.Seal || !EntriesEqual(journal.DescribeRestore().OrderBy(e => e.Entry.Path, StringComparer.Ordinal).ToArray(), Entries(plan))) throw Conflict();
            journal.PrepareRestoreReads(plan.Seal); return journal;
        }
        catch { journal.Dispose(); throw; }
    }
    private static Receipt? ReadReceipt(string path, string hash, List<FileStream> pins)
    {
        if (!Exists(path)) return null; var pin = ManagedRestoreFileAccess.Read(path); pins.Add(pin);
        var receipt = JsonSerializer.Deserialize<Receipt>(pin, Json) ?? throw Conflict();
        if (receipt.Version != 1 || receipt.PlanHash != hash) throw Conflict(); return receipt;
    }
    private static FileReceipt? ReadFileReceipt(string path, string hash, Guid id, List<FileStream> pins)
    {
        if (!Exists(path)) return null; var pin = ManagedRestoreFileAccess.Read(path); pins.Add(pin);
        var receipt = JsonSerializer.Deserialize<FileReceipt>(pin, Json) ?? throw Conflict();
        if (receipt.Version != 1 || receipt.PlanHash != hash || receipt.EntryId != id || string.IsNullOrEmpty(receipt.Identity)) throw Conflict(); return receipt;
    }
    private static void RequireArtifacts(string root, Plan plan, OwnedDirectoryManifest manifest, bool initial)
    {
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        { PlanPath(root), Named(root, "intent.json"), Named(root, "cursor.json"), Named(root, "verified.json"), Named(root, "staging-reclaim.json"), Named(root, "journal-reclaim.bin") };
        if (plan.JournalMode == "bounded") allowed.Add(Named(root, "journal-reclaim.bin.pending"));
        for (int i = 1; i < plan.Topology.Units.Length; i++) allowed.Add(Stage(root, i));
        foreach (var d in plan.Directories.Where(d => d.Entry.BasePath is null)) allowed.Add(FilePath(root, "directory", d.Entry.Id));
        var items = manifest.Items.ToDictionary(i => i.Entry.Path, StringComparer.Ordinal);
        foreach (var f in plan.Files.Where(f => !FixedContent(f.Delta, f.Delta.Entry.BasePath is { } b ? items[b] : null)))
        {
            if (f.BasisIdentity is null) allowed.Add(FilePath(root, "file", f.Delta.Entry.Id));
            allowed.Add(FilePath(root, "write", f.Delta.Entry.Id)); allowed.Add(FilePath(root, "complete", f.Delta.Entry.Id));
        }
        var controls = allowed.Where(p => p.EndsWith(".json", StringComparison.Ordinal)).ToHashSet(StringComparer.Ordinal);
        foreach (string actual in Directory.EnumerateFileSystemEntries(root, Prefix + "*"))
        {
            WorkspaceStore.EnsurePlainPath(actual);
            if (ManagedRestoreControlFiles.IsPublicationTemporary(actual, controls)) continue;
            if (initial || !allowed.Contains(actual) || Directory.Exists(actual) && !Path.GetFileName(actual).StartsWith(Prefix + "stage-", StringComparison.Ordinal)) throw Conflict();
        }
    }
}
