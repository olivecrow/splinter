using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Platform.Windows;

// Readers and the original Editor have stopped. Complete the changed entries in the private
// basis, then publish the whole directory with one NTFS rename. A published tree is read-only
// to recovery, so even a lost cleanup response cannot overwrite a later original edit.
internal static class ManagedFastLibraryRestore
{
    internal const string PlanFileName = "fast-restore-plan.json";
    private sealed record Item(SnapshotEntry Entry, string Identity, string? Hash, string? Stage = null);
    private sealed record Plan(int Version, string BasisId, string Original, string Basis, string Delta,
        SnapshotDeltaSeal Seal, string StageName, string RootId, Item[] Retire, Item[] Install, Item[] Update);
    private sealed record SavedPlan(Plan Plan, string Hash);

    private sealed class MetadataTree(FastOwnedDirectoryIndex index, SnapshotReference snapshot) : ISnapshotTree
    {
        public SnapshotReference Snapshot => snapshot;
        public void EnsureAvailable() { }
        public SnapshotEntry? Stat(string path) => index.Find(JournalDeltaStore.Normalize(path));
        public IReadOnlyList<SnapshotEntry> List(string path) => index.EnumerateChildren(JournalDeltaStore.Normalize(path));
        public int Read(string path, long offset, Span<byte> destination) =>
            throw new NotSupportedException("Stopped journal verification reads metadata only.");
    }

    public static async Task ExecuteAsync(ManagedDirectoryState state, OverlayServeRequest request,
        OwnedDirectoryManifest manifest, CancellationToken ct, Action<string>? boundary = null)
    {
        ct.ThrowIfCancellationRequested();
        string receipt = state.Basis.ReceiptDirectory, basis = state.Basis.Snapshot.RootPath, original = state.OriginalPath;
        string planPath = Path.Combine(receipt, PlanFileName);
        var saved = await WindowsProcessWorkspaceProvider.ReadAsync<SavedPlan>(planPath, ct, waitForPublication: false);
        bool published = Directory.Exists(original);
        if (published == Directory.Exists(basis) || File.Exists(original))
            throw Conflict("Exactly one ordinary original or private basis directory must exist.");
        var index = FastOwnedDirectoryIndex.Load(manifest.FastIndexPath!, manifest.FastIndexHash);

        JournalDeltaStore OpenMetadata() => JournalDeltaStore.OpenReadOnly(request.DeltaPath, request.Identity,
            new MetadataTree(index, state.Basis.Snapshot), sourceFilePins: new WindowsJournalFilePins());

        if (saved is null)
        {
            if (published && !Directory.Exists(request.DeltaPath)) return;
            SnapshotDeltaSeal seal;
            if (published && PublishedReclaimSeal(receipt) is { } reclaimSeal) seal = reclaimSeal;
            else { using var journal = OpenMetadata(); seal = journal.Seal(); }
            if (seal.DurableSequence == 0)
            {
                if (!published)
                {
                    WorkspaceStore.EnsurePlainPath(original);
                    WorkspaceStore.EnsurePlainPath(basis);
                    Directory.CreateDirectory(Path.GetDirectoryName(original)!);
                    Directory.Move(basis, original);
                    boundary?.Invoke("unchanged.after-root-move");
                }
                // This small journal has no changed source entries. The current reclaimer checks
                // its physical inventory and resumes a partially completed reclamation.
                ManagedDirectoryRestore.ReclaimDelta(state, request, seal, boundary);
                return;
            }
            if (published) throw Conflict("A modified delta remains without its restore plan.");
            using (var tree = OwnedDirectorySnapshotTree.OpenLazy(manifest, state.Basis.Snapshot))
            using (var journal = JournalDeltaStore.OpenReadOnly(request.DeltaPath, request.Identity, tree,
                sourceFilePins: new WindowsJournalFilePins()))
            {
                journal.PrepareRestoreReads(seal);
                var plan = Stage(state, request, index, journal, seal, ct);
                saved = new(plan, PlanHash(plan));
                await WindowsProcessWorkspaceProvider.WriteAsync(planPath, saved, ct);
            }
            boundary?.Invoke("modified.after-plan");
        }
        var p = saved.Plan;
        if (p is null || p.Version != 2 || p.BasisId != state.Basis.Id || saved.Hash != PlanHash(p) ||
            !WorkspaceStore.SamePath(p.Original, original) || !WorkspaceStore.SamePath(p.Basis, basis) ||
            !WorkspaceStore.SamePath(p.Delta, request.DeltaPath) || p.Seal.DurableSequence <= 0 ||
            !p.StageName.StartsWith("restore-stage-", StringComparison.Ordinal) ||
            !Guid.TryParseExact(p.StageName["restore-stage-".Length..], "N", out _))
            throw Conflict("The restore plan no longer matches its fixed request.");

        string stage = Path.Combine(receipt, p.StageName);
        if (!published)
        {
            using (var verify = OpenMetadata())
                if (verify.Seal() != p.Seal) throw Conflict("The stopped Library journal changed after staging.");
            using (var root = ManagedRestoreFileAccess.DirectoryHandle(basis, metadataWrite: false, allowChildMutations: true))
            {
                RequireId(root, true, p.RootId);
                ApplyPrivate(p, stage, ct, boundary);
                VerifyFinal(p, basis, ct);
            }
            using (var root = ManagedRestoreFileAccess.DirectoryHandle(basis, moving: true, allowChildMutations: true))
            {
                RequireId(root, true, p.RootId);
                Directory.CreateDirectory(Path.GetDirectoryName(original)!);
                ManagedRestoreFileAccess.Move(root, original);
            }
            boundary?.Invoke("modified.after-root-move");
        }
        else
        {
            using var root = ManagedRestoreFileAccess.DirectoryHandle(original, metadataWrite: false, allowChildMutations: true);
            RequireId(root, true, p.RootId);
            VerifyFinal(p, original, ct);
        }
        if (Directory.Exists(p.Delta) && PublishedReclaimSeal(receipt) is null)
        {
            using var verify = OpenMetadata();
            if (verify.Seal() != p.Seal) throw Conflict("The stopped Library journal changed before cleanup.");
        }
        foreach (var old in p.Retire)
        {
            ct.ThrowIfCancellationRequested();
            string backup = PathAt(stage, old.Stage!);
            if (!Path.Exists(backup)) continue; // cleanup can resume after individual unlinks
            using var handle = Open(backup, old.Entry.Directory, moving: true);
            RequireId(handle, old.Entry.Directory, old.Identity);
            if (old.Entry.Directory) ManagedRestoreFileAccess.DeleteDirectory(handle);
            else
            {
                using var file = new FileStream(handle, FileAccess.Read);
                RequireBytes(file, old);
                ManagedRestoreFileAccess.Delete(file);
            }
        }
        if (Directory.Exists(stage)) Directory.Delete(stage, recursive: false);
        ManagedDirectoryRestore.ReclaimDelta(state, request, p.Seal, boundary);
        File.Delete(planPath);
    }

    private static Plan Stage(ManagedDirectoryState state, OverlayServeRequest request, FastOwnedDirectoryIndex index,
        JournalDeltaStore journal, SnapshotDeltaSeal seal, CancellationToken ct)
    {
        string basis = state.Basis.Snapshot.RootPath, stageName = "restore-stage-" + Guid.NewGuid().ToString("N");
        string stage = Path.Combine(state.Basis.ReceiptDirectory, stageName);
        Directory.CreateDirectory(stage);
        var kept = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var install = new List<Item>();
        var update = new List<Item>();
        byte[] buffer = new byte[262144];
        // No publication or private-basis mutation occurs while producing this plan.
        foreach (var item in journal.EnumerateRestore())
        {
            ct.ThrowIfCancellationRequested();
            var e = item.Entry;
            var current = index.Find(e.Path);
            var final = new SnapshotEntry(e.Path, e.Directory, e.Length, e.Attributes, e.CreationTime, e.LastAccessTime, e.LastWriteTime);
            bool keep = current?.Directory == e.Directory && (e.Directory ||
                string.Equals(e.BasePath, e.Path, StringComparison.OrdinalIgnoreCase) &&
                !item.Ranges.Any() && e.Length == current.Length && e.BaseLength == current.Length);
            if (keep)
            {
                kept.Add(e.Path);
                if (current!.Attributes != e.Attributes || current.LastWriteTime != e.LastWriteTime ||
                    current.CreationTime != e.CreationTime || current.LastAccessTime != e.LastAccessTime ||
                    Path.GetFileName(current.Path) != Path.GetFileName(e.Path))
                    update.Add(Observe(basis, current) with { Entry = final });
                continue;
            }
            string name = (e.Directory ? "d" : "f") + install.Count.ToString("D6") + (e.Directory ? "" : ".bin");
            string path = Path.Combine(stage, name);
            if (e.Directory)
            {
                using var dir = ManagedRestoreFileAccess.CreateDirectory(path);
                install.Add(new(final, SnapshotInputChecks.FileIdentity(dir, directory: true), null, name));
                continue;
            }
            using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            for (long offset = 0; offset < e.Length;)
            {
                ct.ThrowIfCancellationRequested();
                int read = journal.Read(e.Id, offset, buffer.AsSpan(0, (int)Math.Min(buffer.Length, e.Length - offset)));
                if (read <= 0) throw Conflict("Unexpected end of restored Library file.");
                output.Write(buffer, 0, read);
                hash.AppendData(buffer, 0, read);
                offset += read;
            }
            output.Flush(true);
            install.Add(new(final, ManagedRestoreFileAccess.Identity(output), Convert.ToHexStringLower(hash.GetHashAndReset()), name));
        }
        var retire = new List<Item>();
        foreach (var entry in index.AllEntries.Values.Where(e => e.Path.Length != 0 && !kept.Contains(e.Path)))
        {
            ct.ThrowIfCancellationRequested();
            retire.Add(Observe(basis, entry) with { Stage = $"r{retire.Count:D6}" });
        }
        using var root = ManagedRestoreFileAccess.DirectoryHandle(basis, metadataWrite: false, allowChildMutations: true);
        return new(2, state.Basis.Id, state.OriginalPath, basis, request.DeltaPath, seal, stageName,
            SnapshotInputChecks.FileIdentity(root, true), retire.OrderByDescending(i => i.Entry.Path.Length).ToArray(),
            install.OrderBy(i => i.Entry.Path.Length).ToArray(), update.ToArray());
    }

    private static Item Observe(string root, SnapshotEntry e)
    {
        string path = PathAt(root, e.Path);
        if (e.Directory)
        {
            using var dir = ManagedRestoreFileAccess.DirectoryHandle(path, metadataWrite: false, allowChildMutations: true);
            return new(e, SnapshotInputChecks.FileIdentity(dir, true), null);
        }
        using var file = ManagedRestoreFileAccess.Read(path);
        if (file.Length != e.Length || File.GetLastWriteTimeUtc(file.SafeFileHandle).ToFileTimeUtc() != e.LastWriteTime)
            throw Conflict($"The Library basis changed at '{e.Path}'.");
        return new(e, ManagedRestoreFileAccess.Identity(file), Hash(file));
    }

    private static void ApplyPrivate(Plan p, string stage, CancellationToken ct, Action<string>? boundary)
    {
        var additions = p.Install.ToDictionary(i => i.Entry.Path, StringComparer.OrdinalIgnoreCase);
        // Verify every staged or already installed payload before moving any remaining preimage.
        foreach (var item in p.Install)
        {
            ct.ThrowIfCancellationRequested();
            string staged = PathAt(stage, item.Stage!);
            using var handle = Open(Path.Exists(staged) ? staged : PathAt(p.Basis, item.Entry.Path), item.Entry.Directory, moving: false);
            RequireId(handle, item.Entry.Directory, item.Identity);
            if (!item.Entry.Directory)
            {
                using var file = new FileStream(handle, FileAccess.Read);
                RequireBytes(file, item);
            }
        }
        foreach (var old in p.Retire)
        {
            ct.ThrowIfCancellationRequested();
            string path = PathAt(p.Basis, old.Entry.Path);
            string backup = PathAt(stage, old.Stage!);
            if (Path.Exists(backup))
            {
                using var held = Open(backup, old.Entry.Directory, moving: false);
                RequireId(held, old.Entry.Directory, old.Identity);
                if (!Path.Exists(path)) continue;
                if (!additions.TryGetValue(old.Entry.Path, out var completed))
                    throw Conflict($"A retired Library entry reappeared at '{old.Entry.Path}'.");
                using var installed = Open(path, completed.Entry.Directory, moving: false);
                RequireId(installed, completed.Entry.Directory, completed.Identity);
                continue;
            }
            bool directory = Directory.Exists(path);
            using var handle = Open(path, directory, moving: true);
            string identity = SnapshotInputChecks.FileIdentity(handle, directory);
            if (old.Identity != identity || old.Entry.Directory != directory) throw Conflict($"Restore target '{old.Entry.Path}' changed.");
            if (directory)
            {
                if (Directory.EnumerateFileSystemEntries(path).Any()) throw Conflict($"Unexpected children remain at '{old.Entry.Path}'.");
                ManagedRestoreFileAccess.Move(handle, backup);
            }
            else
            {
                using var file = new FileStream(handle, FileAccess.Read);
                RequireBytes(file, old);
                ManagedRestoreFileAccess.Move(file, backup);
            }
        }
        foreach (var item in p.Install)
        {
            ct.ThrowIfCancellationRequested();
            string target = PathAt(p.Basis, item.Entry.Path), staged = PathAt(stage, item.Stage!);
            if (Path.Exists(staged))
            {
                using var handle = Open(staged, item.Entry.Directory, moving: true);
                RequireId(handle, item.Entry.Directory, item.Identity);
                ManagedRestoreFileAccess.Move(handle, target); // verified again at the final name before publication
            }
            if (!item.Entry.Directory) boundary?.Invoke("modified.after-file-install");
        }
        foreach (var item in p.Update.Concat(p.Install).OrderByDescending(i => i.Entry.Path.Length))
        {
            ct.ThrowIfCancellationRequested();
            var e = item.Entry;
            string path = PathAt(p.Basis, e.Path);
            bool rename = e.Path.Length > 0 && !HasExactLeaf(path);
            using var handle = Open(path, e.Directory, moving: rename);
            RequireId(handle, e.Directory, item.Identity);
            if (rename) ManagedRestoreFileAccess.Move(handle, path);
            ManagedRestoreFileAccess.Metadata(handle, e);
        }
    }

    private static void VerifyFinal(Plan p, string root, CancellationToken ct)
    {
        var additions = p.Install.Select(i => i.Entry.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var old in p.Retire)
            if (!additions.Contains(old.Entry.Path) && Path.Exists(PathAt(root, old.Entry.Path)))
                throw Conflict($"Deleted Library entry '{old.Entry.Path}' reappeared.");
        foreach (var item in p.Install.Concat(p.Update))
        {
            ct.ThrowIfCancellationRequested();
            var e = item.Entry;
            string path = PathAt(root, e.Path);
            using var handle = Open(path, e.Directory, moving: false);
            RequireId(handle, e.Directory, item.Identity);
            if (e.Path.Length != 0 && !HasExactLeaf(path) || File.GetAttributes(handle) != (FileAttributes)e.Attributes ||
                File.GetLastWriteTimeUtc(handle).ToFileTimeUtc() != e.LastWriteTime ||
                File.GetCreationTimeUtc(handle).ToFileTimeUtc() != e.CreationTime)
                throw Conflict($"Restored Library metadata changed at '{e.Path}'.");
            if (!e.Directory)
            {
                using var file = new FileStream(handle, FileAccess.Read);
                RequireBytes(file, item);
            }
        }
    }

    private static SafeFileHandle Open(string path, bool directory, bool moving) => directory
        ? ManagedRestoreFileAccess.DirectoryHandle(path, moving, allowChildMutations: true)
        : ManagedRestoreFileAccess.ReadHandle(path, moving);
    private static void RequireId(SafeFileHandle handle, bool directory, string identity)
    {
        if (SnapshotInputChecks.FileIdentity(handle, directory) != identity) throw Conflict("The owned NTFS entry changed.");
    }
    private static void RequireBytes(FileStream file, Item item)
    {
        if (file.Length != item.Entry.Length || Hash(file) != item.Hash)
            throw Conflict($"The restored or staged bytes differ at '{item.Entry.Path}'.");
    }
    private static bool HasExactLeaf(string path) =>
        Directory.EnumerateFileSystemEntries(Path.GetDirectoryName(path)!, Path.GetFileName(path))
            .Any(actual => Path.GetFileName(actual) == Path.GetFileName(path));
    private static string Hash(Stream stream) => Convert.ToHexStringLower(SHA256.HashData(stream));
    private static SnapshotDeltaSeal? PublishedReclaimSeal(string receipt)
    {
        string bounded = Path.Combine(receipt, "bounded-restore-delta-reclaim.bin");
        string latest = Path.Combine(receipt, "latest-restore-delta-reclaim.json");
        if (File.Exists(bounded + ".segmented")) return SegmentedJournalReclamation.ReadPublishedSeal(bounded + ".segmented");
        if (File.Exists(bounded)) return BoundedJournalReclamation.ReadPublishedSeal(bounded);
        return File.Exists(latest) ? LatestJournalReclamation.ReadPublishedSeal(latest) : null;
    }
    private static string PlanHash(Plan plan) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(plan)));
    private static WorkspaceException Conflict(string message) => new("GeneratedRestoreConflict", message);
    private static string PathAt(string root, string relative)
    {
        string path = Path.Combine(root, JournalDeltaStore.Normalize(relative).Replace('/', Path.DirectorySeparatorChar));
        WorkspaceStore.EnsurePlainPath(path);
        return path;
    }
}
