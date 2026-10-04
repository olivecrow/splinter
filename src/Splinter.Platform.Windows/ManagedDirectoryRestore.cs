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
    private sealed record RestoreFile(DeltaRestoreEntry Delta, string FinalHash, string RetainedHash);
    private sealed record RestorePlan(int Version, GeneratedDirectoryReference Basis, SnapshotDeltaSeal Seal,
        SnapshotEntry[] Directories, RestoreFile[] Files);
    private sealed record RestoreCursor(string PlanHash, int CompletedFiles, bool FilesVerified = false);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static async Task ExecuteAsync(ManagedDirectoryState state, OverlayServeRequest request, CancellationToken ct, Action<string>? boundary = null)
    {
        string root = state.Basis.ReceiptDirectory, planPath = Path.Combine(root, "restore-plan.json"), cursorPath = Path.Combine(root, "restore-cursor.json");
        var manifest = new OwnedDirectorySnapshotProvider(root, state.Basis.OwnerId).Read(state.Basis.Snapshot);
        var manifestItems = manifest.Items.ToDictionary(i => i.Entry.Path, StringComparer.OrdinalIgnoreCase);
        RestorePlan? plan = await WindowsProcessWorkspaceProvider.ReadAsync<RestorePlan>(planPath, ct);
        var savedCursor = await WindowsProcessWorkspaceProvider.ReadAsync<RestoreCursor>(cursorPath, ct);
        if (plan is not null && savedCursor is { FilesVerified: true })
        {
            if (plan.Basis != state.Basis || savedCursor.PlanHash != ManagedDirectoryBasis.HashFile(planPath) || savedCursor.CompletedFiles != plan.Files.Length)
                throw new WorkspaceException("GeneratedRestoreConflict", "Retain an inconsistent completed restoration.");
            RejectForeign(state.OriginalPath, plan.Files.Select(f => f.Delta.Entry.Path).ToHashSet(StringComparer.OrdinalIgnoreCase), plan.Directories.Select(d => d.Path).ToHashSet(StringComparer.OrdinalIgnoreCase));
            foreach (var file in plan.Files) if (ManagedDirectoryBasis.HashFile(PathOf(state.OriginalPath, file.Delta.Entry.Path)) != file.FinalHash)
                throw new WorkspaceException("GeneratedRestoreConflict", "A restored original changed before cleanup.");
            Cleanup(state, request, manifest, boundary); return;
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
                    string retained = entry.Entry.BasePath is { } basePath ? RetainedHash(PathOf(state.Basis.Snapshot.RootPath, basePath), entry.Entry.BaseLength, entry.Ranges) : EmptyHash;
                    files.Add(new(entry, Convert.ToHexStringLower(hash.GetHashAndReset()), retained));
                }
                var directories = entries.Where(e => e.Entry.Directory).Select(e => new SnapshotEntry(e.Entry.Path, true, 0, e.Entry.Attributes,
                    e.Entry.CreationTime, e.Entry.LastAccessTime, e.Entry.LastWriteTime)).OrderBy(e => e.Path.Length).ToArray();
                if (journal.Seal() != seal) throw new WorkspaceException("RestoreSealMismatch", "The restore journal changed while planning.");
                plan = new(1, state.Basis, seal, directories, files.ToArray());
                await WindowsProcessWorkspaceProvider.WriteAsync(planPath, plan, ct);
            }
            catch { journal.Dispose(); throw; }
        }
        else journal = JournalDeltaStore.OpenReadOnly(request.DeltaPath, request.Identity, new MetadataOnlyRestoreTree(manifest));
        using (journal)
        {
            if (plan.Version != 1 || plan.Basis != state.Basis || ManagedDirectoryBasis.HashFile(Path.Combine(request.DeltaPath, "delta.journal")) != plan.Seal.JournalHash ||
                new FileInfo(Path.Combine(request.DeltaPath, "delta.journal")).Length != plan.Seal.JournalBytes)
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
                if (i < cursor.CompletedFiles)
                {
                    if (!File.Exists(target) || ManagedDirectoryBasis.HashFile(target) != file.FinalHash) throw new WorkspaceException("GeneratedRestoreConflict", "A completed restore file changed.");
                    continue;
                }
                if (entry.BasePath is { } basePath)
                {
                    string source = PathOf(state.Basis.Snapshot.RootPath, basePath);
                    if (File.Exists(source))
                    {
                        if (File.Exists(target)) throw new WorkspaceException("GeneratedRestoreConflict", "Both the original basis file and restore target exist.");
                        var original = manifestItems.GetValueOrDefault(basePath);
                        if (original?.Hash is null || ManagedDirectoryBasis.HashFile(source) != original.Hash)
                            throw new WorkspaceException("GeneratedBasisChanged", "A remaining basis file changed before its move.");
                        File.Move(source, target, false);
                        boundary?.Invoke("file.after-move");
                    }
                    if (!File.Exists(target) || RetainedHash(target, entry.BaseLength, file.Delta.Ranges) != file.RetainedHash)
                        throw new WorkspaceException("GeneratedRestoreConflict", "Retained bytes changed; preserve the original and journal.");
                }
                else if (!File.Exists(target)) { using var created = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None); created.Flush(true); }
                // A started range effect is redoable from the fixed private
                // journal. Reset only the non-retained tail, never copy a file.
                bool needsWrite = file.Delta.Ranges.Length != 0 || new FileInfo(target).Length != entry.Length || entry.BaseLength < entry.Length;
                if (needsWrite && ManagedDirectoryBasis.HashFile(target) != file.FinalHash)
                {
                    File.SetAttributes(target, File.GetAttributes(target) & ~FileAttributes.ReadOnly);
                    using var output = new FileStream(target, FileMode.Open, FileAccess.Write, FileShare.None, 4096, FileOptions.RandomAccess);
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
                if (ManagedDirectoryBasis.HashFile(target) != file.FinalHash) throw new WorkspaceException("GeneratedRestoreConflict", "Restored range content differs from its fixed result.");
                SetMetadata(target, entry.Attributes, entry.CreationTime, entry.LastAccessTime, entry.LastWriteTime);
                cursor = cursor with { CompletedFiles = i + 1 }; await WindowsProcessWorkspaceProvider.WriteAsync(cursorPath, cursor, ct);
            }
            RejectForeign(state.OriginalPath, allowedFiles, allowedDirectories);
            foreach (var directory in plan.Directories.OrderByDescending(e => e.Path.Length))
                SetMetadata(PathOf(state.OriginalPath, directory.Path), directory.Attributes, directory.CreationTime, directory.LastAccessTime, directory.LastWriteTime);
            cursor = cursor with { FilesVerified = true }; await WindowsProcessWorkspaceProvider.WriteAsync(cursorPath, cursor, ct);
            boundary?.Invoke("restore.after-verified");
        }
        Cleanup(state, request, manifest, boundary);
    }
    private static void Cleanup(ManagedDirectoryState state, OverlayServeRequest request, OwnedDirectoryManifest manifest, Action<string>? boundary)
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
    private static string RetainedHash(string path, long length, DeltaRestoreRange[] ranges)
    {
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read); using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
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
