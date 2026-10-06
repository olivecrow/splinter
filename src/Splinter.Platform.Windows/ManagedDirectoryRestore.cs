using System.Security.Cryptography;
using System.Text.Json;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Platform.Windows;

// Restore by moving existing files and replaying private ranges in place.
// A durable plan/cursor protects restart. Unchanged retained bytes are checked
// before redo; unknown files, altered retained bytes and changed journals fail.
internal static class ManagedDirectoryRestore
{
    private sealed record RestoreFile(DeltaRestoreEntry Delta, string FinalHash, string RetainedHash, string? BasisIdentity);
    private sealed record RestorePlan(int Version, GeneratedDirectoryReference Basis, SnapshotDeltaSeal Seal,
        SnapshotEntry[] Directories, RestoreFile[] Files);
    private sealed record RestoreCursor(string PlanHash, int CompletedFiles, bool FilesVerified = false);
    private sealed record FileReceipt(int Version, string PlanHash, Guid EntryId, string Identity);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static async Task ExecuteAsync(ManagedDirectoryState state, OverlayServeRequest request, CancellationToken ct, Action<string>? boundary = null)
    {
        string root = state.Basis.ReceiptDirectory, planPath = Path.Combine(root, "restore-plan.json"), cursorPath = Path.Combine(root, "restore-cursor.json");
        var manifest = new OwnedDirectorySnapshotProvider(root, state.Basis.OwnerId).Read(state.Basis.Snapshot);
        var manifestItems = manifest.Items.ToDictionary(i => i.Entry.Path, StringComparer.OrdinalIgnoreCase);
        RestorePlan? plan = await WindowsProcessWorkspaceProvider.ReadAsync<RestorePlan>(planPath, ct);
        if (plan is not null && plan.Version != 2) throw new WorkspaceException("RestoreSealMismatch", "Preserve an unsupported restore plan without migration.");
        var savedCursor = await WindowsProcessWorkspaceProvider.ReadAsync<RestoreCursor>(cursorPath, ct);
        if (plan is not null && savedCursor is { FilesVerified: true })
        {
            if (plan.Basis != state.Basis || savedCursor.PlanHash != ManagedDirectoryBasis.HashFile(planPath) || savedCursor.CompletedFiles != plan.Files.Length)
                throw new WorkspaceException("GeneratedRestoreConflict", "Retain an inconsistent completed restoration.");
            RejectForeign(state.OriginalPath, plan.Files.Select(f => f.Delta.Entry.Path).ToHashSet(StringComparer.OrdinalIgnoreCase), plan.Directories.Select(d => d.Path).ToHashSet(StringComparer.OrdinalIgnoreCase));
            foreach (var file in plan.Files)
            {
                string target = PathOf(state.OriginalPath, file.Delta.Entry.Path);
                using var access = new ManagedRestoreFileAccess(target);
                using var pin = ManagedRestoreFileAccess.Read(target);
                ManagedRestoreFileAccess.Require(pin, await FileIdentityAsync(root, savedCursor.PlanHash, file, ct));
                if (Hash(pin) != file.FinalHash) throw new WorkspaceException("GeneratedRestoreConflict", "A restored original changed before cleanup.");
            }
            Cleanup(state, request, manifest, plan.Seal, boundary); return;
        }
        JournalDeltaStore journal;
        if (plan is null)
        {
            if (Directory.Exists(state.OriginalPath)) throw new WorkspaceException("GeneratedRestoreConflict", "Preserve a directory that appeared at the original path.");
            using var basis = new OwnedDirectorySnapshotTree(manifest, state.Basis.Snapshot);
            journal = JournalDeltaStore.OpenReadOnly(request.DeltaPath, request.Identity, basis);
            try
            {
                var seal = journal.Seal(); var entries = journal.DescribeRestore();
                var files = new List<RestoreFile>(); byte[] buffer = new byte[1024 * 1024];
                foreach (var entry in entries.Where(e => !e.Entry.Directory).OrderBy(e => e.Entry.Path, StringComparer.OrdinalIgnoreCase))
                {
                    using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); long offset = 0;
                    while (offset < entry.Entry.Length)
                    {
                        ct.ThrowIfCancellationRequested(); int count = journal.Read(entry.Entry.Id, offset, buffer.AsSpan(0, (int)Math.Min(buffer.Length, entry.Entry.Length - offset)));
                        if (count == 0) throw new WorkspaceException("GeneratedRestoreConflict", "A fixed restore file ended early.");
                        hash.AppendData(buffer, 0, count); offset += count;
                    }
                    string retained = EmptyHash; string? basisIdentity = null;
                    if (entry.Entry.BasePath is { } basePath)
                    {
                        using var pin = ManagedRestoreFileAccess.Read(PathOf(state.Basis.Snapshot.RootPath, basePath));
                        basisIdentity = ManagedRestoreFileAccess.Identity(pin);
                        retained = RetainedHash(pin, entry.Entry.BaseLength, entry.Ranges);
                    }
                    files.Add(new(entry, Convert.ToHexStringLower(hash.GetHashAndReset()), retained, basisIdentity));
                }
                var directories = entries.Where(e => e.Entry.Directory).Select(e => new SnapshotEntry(e.Entry.Path, true, 0, e.Entry.Attributes,
                    e.Entry.CreationTime, e.Entry.LastAccessTime, e.Entry.LastWriteTime)).OrderBy(e => e.Path.Length).ToArray();
                if (journal.Seal() != seal) throw new WorkspaceException("RestoreSealMismatch", "The restore journal changed while planning.");
                plan = new(2, state.Basis, seal, directories, files.ToArray());
                await WindowsProcessWorkspaceProvider.WriteAsync(planPath, plan, ct);
            }
            catch { journal.Dispose(); throw; }
        }
        else journal = JournalDeltaStore.OpenReadOnly(request.DeltaPath, request.Identity, new MetadataOnlyRestoreTree(manifest));
        using (journal)
        {
            // Planning has closed the basis reader; restoration now uses only
            // the pinned private journal, even after basis files are moved.
            if (plan.Version != 2 || plan.Basis != state.Basis ||
                JournalDeltaStore.SealJournal(Path.Combine(request.DeltaPath, "delta.journal"), plan.Seal.DurableSequence) != plan.Seal)
                throw new WorkspaceException("RestoreSealMismatch", "Retain a mismatched restore plan/journal.");
            string planHash = ManagedDirectoryBasis.HashFile(planPath);
            journal.PrepareRestoreReads(plan.Seal);
            var cursor = await WindowsProcessWorkspaceProvider.ReadAsync<RestoreCursor>(cursorPath, ct);
            if (cursor is null) { cursor = new(planHash, 0); await WindowsProcessWorkspaceProvider.WriteAsync(cursorPath, cursor, ct); }
            if (cursor.PlanHash != planHash || cursor.CompletedFiles < 0 || cursor.CompletedFiles > plan.Files.Length)
                throw new WorkspaceException("GeneratedRestoreConflict", "Retain an inconsistent restore cursor.");
            var allowedFiles = plan.Files.Select(f => f.Delta.Entry.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var allowedDirectories = plan.Directories.Select(d => d.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (Directory.Exists(state.OriginalPath)) RejectForeign(state.OriginalPath, allowedFiles, allowedDirectories);
            foreach (var directory in plan.Directories) Directory.CreateDirectory(PathOf(state.OriginalPath, directory.Path));
            byte[] buffer = new byte[1024 * 1024];
            for (int i = 0; i < plan.Files.Length; i++)
            {
                ct.ThrowIfCancellationRequested(); var file = plan.Files[i]; var entry = file.Delta.Entry; string target = PathOf(state.OriginalPath, entry.Path);
                string? source = entry.BasePath is { } basisPath ? PathOf(state.Basis.Snapshot.RootPath, basisPath) : null;
                using var access = new ManagedRestoreFileAccess(source is null || !File.Exists(source) ? [target] : [source, target]);
                if (i < cursor.CompletedFiles)
                {
                    using var completed = ManagedRestoreFileAccess.Read(target);
                    ManagedRestoreFileAccess.Require(completed, await FileIdentityAsync(root, planHash, file, ct));
                    if (Hash(completed) != file.FinalHash) throw new WorkspaceException("GeneratedRestoreConflict", "A completed restore file changed.");
                    continue;
                }
                if (entry.BasePath is { } basePath)
                {
                    if (file.BasisIdentity is null) throw new WorkspaceException("GeneratedRestoreConflict", "Preserve a restore plan without its basis file identity.");
                    if (File.Exists(source!))
                    {
                        if (File.Exists(target)) throw new WorkspaceException("GeneratedRestoreConflict", "Both the original basis file and restore target exist.");
                        using var moved = ManagedRestoreFileAccess.Read(source!, moving: true);
                        ManagedRestoreFileAccess.Require(moved, file.BasisIdentity);
                        var original = manifestItems.GetValueOrDefault(basePath);
                        if (original?.Hash is null || Hash(moved) != original.Hash)
                            throw new WorkspaceException("GeneratedBasisChanged", "A remaining basis file changed before its move.");
                        ManagedRestoreFileAccess.Move(moved, target);
                        boundary?.Invoke("file.after-move");
                    }
                }
                else
                {
                    var receipt = await ReadReceiptAsync(root, planHash, file, ct);
                    if (receipt is null)
                    {
                        if (File.Exists(target)) throw new WorkspaceException("GeneratedRestoreConflict", "Preserve a new restore target without its creation receipt.");
                        using var created = new FileStream(target, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
                        created.Flush(true); boundary?.Invoke("file.after-create-before-receipt");
                        receipt = new(1, planHash, entry.Id, ManagedRestoreFileAccess.Identity(created));
                        await WindowsProcessWorkspaceProvider.WriteAsync(ReceiptPath(root, entry.Id), receipt, ct);
                        boundary?.Invoke("file.after-create-receipt");
                    }
                }
                string identity = await FileIdentityAsync(root, planHash, file, ct);
                using var output = ManagedRestoreFileAccess.Write(target, identity, pin =>
                {
                    if (entry.BasePath is not null && RetainedHash(pin, entry.BaseLength, file.Delta.Ranges) != file.RetainedHash)
                        throw new WorkspaceException("GeneratedRestoreConflict", "Retained bytes changed; preserve the original and journal.");
                });
                // A started range effect is redoable from the fixed private
                // journal. Reset only the non-retained tail, never copy a file.
                bool needsWrite = file.Delta.Ranges.Length != 0 || output.Length != entry.Length || entry.BaseLength < entry.Length;
                if (needsWrite && Hash(output) != file.FinalHash)
                {
                    boundary?.Invoke("file.before-ranges-write");
                    output.SetLength(entry.BaseLength); output.SetLength(entry.Length);
                    foreach (var range in file.Delta.Ranges)
                        for (int offset = 0; offset < range.Length;)
                        {
                            int count = Math.Min(buffer.Length, range.Length - offset);
                            journal.ReadRestoreRange(entry.Id, range.Offset + offset, buffer.AsSpan(0, count), plan.Seal);
                            output.Position = range.Offset + offset; output.Write(buffer, 0, count); offset += count;
                        }
                    output.Flush(true);
                    boundary?.Invoke("file.after-ranges-flush");
                }
                if (Hash(output) != file.FinalHash) throw new WorkspaceException("GeneratedRestoreConflict", "Restored range content differs from its fixed result.");
                ManagedRestoreFileAccess.Metadata(output, entry.Attributes, entry.CreationTime, entry.LastAccessTime, entry.LastWriteTime);
                cursor = cursor with { CompletedFiles = i + 1 }; await WindowsProcessWorkspaceProvider.WriteAsync(cursorPath, cursor, ct);
            }
            RejectForeign(state.OriginalPath, allowedFiles, allowedDirectories);
            foreach (var directory in plan.Directories.OrderByDescending(e => e.Path.Length))
                SetMetadata(PathOf(state.OriginalPath, directory.Path), directory.Attributes, directory.CreationTime, directory.LastAccessTime, directory.LastWriteTime);
            cursor = cursor with { FilesVerified = true }; await WindowsProcessWorkspaceProvider.WriteAsync(cursorPath, cursor, ct);
            boundary?.Invoke("restore.after-verified");
        }
        Cleanup(state, request, manifest, plan.Seal, boundary);
    }
    private static void Cleanup(ManagedDirectoryState state, OverlayServeRequest request, OwnedDirectoryManifest manifest, SnapshotDeltaSeal seal, Action<string>? boundary)
    {
        var manifestItems = manifest.Items.ToDictionary(i => i.Entry.Path, StringComparer.OrdinalIgnoreCase);
        // The final ordinary NTFS tree is independent. Only untouched deleted
        // basis entries and this exact journal may now be reclaimed.
        if (Directory.Exists(state.Basis.Snapshot.RootPath))
        {
            foreach (string path in Directory.EnumerateDirectories(state.Basis.Snapshot.RootPath, "*", SearchOption.AllDirectories))
            {
                WorkspaceStore.EnsurePlainPath(path); string relative = Path.GetRelativePath(state.Basis.Snapshot.RootPath, path).Replace('\\', '/');
                if (manifestItems.GetValueOrDefault(relative)?.Entry.Directory != true)
                    throw new WorkspaceException("GeneratedBasisChanged", "Retain an unexpected remaining basis directory.");
            }
            foreach (string path in Directory.EnumerateFiles(state.Basis.Snapshot.RootPath, "*", SearchOption.AllDirectories))
            {
                WorkspaceStore.EnsurePlainPath(path); string relative = Path.GetRelativePath(state.Basis.Snapshot.RootPath, path).Replace('\\', '/');
                var item = manifestItems.GetValueOrDefault(relative);
                if (item?.Hash is null || ManagedDirectoryBasis.HashFile(path) != item.Hash) throw new WorkspaceException("GeneratedBasisChanged", "Retain an unexpected remaining basis file.");
            }
            foreach (string path in Directory.EnumerateFiles(state.Basis.Snapshot.RootPath, "*", SearchOption.AllDirectories))
                File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
            WorkspaceStore.EnsurePlainPath(state.Basis.Snapshot.RootPath); Directory.Delete(state.Basis.Snapshot.RootPath, true);
            boundary?.Invoke("restore.after-basis-reclaim");
        }
        if (!Directory.Exists(request.DeltaPath)) return;
        string journalPath = Path.Combine(request.DeltaPath, "delta.journal"), reclaim = Path.Combine(state.Basis.ReceiptDirectory, "latest-restore-delta-reclaim.json");
        if (File.Exists(reclaim) || File.Exists(journalPath) && LatestJournalReclamation.IsLatest(journalPath))
        {
            LatestJournalReclamation.ReclaimDirectory(request.DeltaPath, seal, ["identity.json"], reclaim);
            boundary?.Invoke("restore.after-delta-reclaim"); return;
        }
        var actual = Directory.EnumerateFileSystemEntries(request.DeltaPath).ToArray();
        if (actual.Any(p => Path.GetFileName(p) is not ("delta.journal" or "identity.json") || Directory.Exists(p))) throw new WorkspaceException("UnownedDeltaFiles", "Retain unexpected original-delta files.");
        foreach (string path in actual) { WorkspaceStore.EnsurePlainPath(path); File.Delete(path); }
        Directory.Delete(request.DeltaPath, false);
        boundary?.Invoke("restore.after-delta-reclaim");
    }
    private static string PathOf(string root, string relative)
    {
        if (JournalDeltaStore.Normalize(relative) != relative) throw new WorkspaceException("GeneratedRestoreConflict", "Invalid restore path.");
        string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)); WorkspaceStore.EnsurePlainPath(path); return path;
    }
    private static string EmptyHash => Convert.ToHexStringLower(SHA256.HashData(Array.Empty<byte>()));
    private static string ReceiptPath(string root, Guid id) => Path.Combine(root, "restore-file-" + id.ToString("N") + ".json");
    private static async Task<FileReceipt?> ReadReceiptAsync(string root, string planHash, RestoreFile file, CancellationToken ct)
    {
        var receipt = await WindowsProcessWorkspaceProvider.ReadAsync<FileReceipt>(ReceiptPath(root, file.Delta.Entry.Id), ct, waitForPublication: false);
        if (receipt is not null && (receipt.Version != 1 || receipt.PlanHash != planHash || receipt.EntryId != file.Delta.Entry.Id || string.IsNullOrEmpty(receipt.Identity)))
            throw new WorkspaceException("GeneratedRestoreConflict", "Preserve an inconsistent restore file receipt.");
        return receipt;
    }
    private static async Task<string> FileIdentityAsync(string root, string planHash, RestoreFile file, CancellationToken ct) => file.BasisIdentity ??
        (await ReadReceiptAsync(root, planHash, file, ct))?.Identity ?? throw new WorkspaceException("GeneratedRestoreConflict", "Preserve a restore target without its file identity.");
    private static string Hash(FileStream file) { file.Position = 0; return Convert.ToHexStringLower(SHA256.HashData(file)); }
    private static string RetainedHash(FileStream input, long length, DeltaRestoreRange[] ranges)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        if (input.Length < length) throw new WorkspaceException("GeneratedRestoreConflict", "Retained bytes are missing.");
        byte[] buffer = new byte[65536]; long cursor = 0;
        foreach (var range in ranges.Append(new(length, 0)))
        {
            long end = Math.Min(length, range.Offset);
            input.Position = cursor;
            while (cursor < end) { int read = input.Read(buffer, 0, (int)Math.Min(buffer.Length, end - cursor)); if (read == 0) throw new EndOfStreamException(); hash.AppendData(buffer, 0, read); cursor += read; }
            cursor = Math.Min(length, Math.Max(cursor, range.Offset + range.Length));
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
    private static void RejectForeign(string root, HashSet<string> files, HashSet<string> directories)
    {
        WorkspaceStore.EnsurePlainPath(root);
        foreach (string path in Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories))
        {
            WorkspaceStore.EnsurePlainPath(path); string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            if (!(Directory.Exists(path) ? directories : files).Contains(relative)) throw new WorkspaceException("GeneratedRestoreConflict", "Retain an unexpected original-path entry.");
        }
    }
    private static void SetMetadata(string path, uint attributes, long created, long accessed, long written)
    {
        if ((attributes & 16) != 0)
        {
            Directory.SetCreationTimeUtc(path, DateTime.FromFileTimeUtc(created)); Directory.SetLastAccessTimeUtc(path, DateTime.FromFileTimeUtc(accessed)); Directory.SetLastWriteTimeUtc(path, DateTime.FromFileTimeUtc(written));
        }
        else
        {
            File.SetCreationTimeUtc(path, DateTime.FromFileTimeUtc(created)); File.SetLastAccessTimeUtc(path, DateTime.FromFileTimeUtc(accessed)); File.SetLastWriteTimeUtc(path, DateTime.FromFileTimeUtc(written));
        }
        File.SetAttributes(path, (FileAttributes)attributes);
    }
    private sealed class MetadataOnlyRestoreTree(OwnedDirectoryManifest manifest) : ISnapshotTree
    {
        public SnapshotReference Snapshot => manifest.Snapshot;
        public void EnsureAvailable() { }
        public SnapshotEntry? Stat(string path) => manifest.Items.FirstOrDefault(i => i.Entry.Path.Equals(path, StringComparison.OrdinalIgnoreCase))?.Entry;
        public IReadOnlyList<SnapshotEntry> List(string path) => throw new WorkspaceException("GeneratedRestoreConflict", "A resumed restore may only use its fixed plan.");
        public int Read(string path, long offset, Span<byte> destination) => throw new WorkspaceException("GeneratedRestoreConflict", "A resumed restore must not read a changing basis.");
    }
}
