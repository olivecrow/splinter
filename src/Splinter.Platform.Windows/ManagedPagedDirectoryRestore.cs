using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Splinter.Core;
using Splinter.Runtime;
using Microsoft.Win32.SafeHandles;

namespace Splinter.Platform.Windows;

// Large managed originals use a frozen directory catalog, GUID-addressed file
// records and range roots. Progress is one small durable cursor. Original bytes
// are moved and private ranges replayed; unchanged payload is never copied.
internal static class ManagedPagedDirectoryRestore
{
    private const string PlanName = "paged-restore-plan.json", CursorName = "paged-restore-cursor.json", PrepareName = "paged-restore-prepare.json";
    private sealed record Preparation(int Version, GeneratedDirectoryReference Basis, SnapshotDeltaSeal Seal, string Directory);
    private sealed record Plan(int Version, GeneratedDirectoryReference Basis, SnapshotDeltaSeal Seal, string Directory,
        SnapshotCatalogDescriptor Catalog, MetadataTreeRoot Files, MetadataTreeRoot Names,
        SnapshotCatalogDescriptor CleanupCatalog, string BasisRootIdentity, bool RootMove);
    private sealed record Cursor(string PlanHash, string? AfterPath, long Completed, bool FilesVerified = false);
    private sealed record FileRecord(DeltaEntry Entry, string FinalHash, string RetainedHash, string Identity, string? Staging, MetadataTreeRoot Ranges);
    internal static bool HasPlan(string root) => File.Exists(Path.Combine(root, PlanName)) || File.Exists(Path.Combine(root, PrepareName));
    private static WorkspaceException Conflict(string message = "Preserve a changed or unsupported paged restore artifact.") => new("GeneratedRestoreConflict", message);
    private static string PathOf(string root, string relative)
    {
        if (JournalDeltaStore.Normalize(relative) != relative) throw Conflict();
        string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)); WorkspaceStore.EnsurePlainPath(path); return path;
    }
    private static string EmptyHash => Convert.ToHexStringLower(SHA256.HashData(Array.Empty<byte>()));
    private static string Hash(FileStream file) { file.Position = 0; return Convert.ToHexStringLower(SHA256.HashData(file)); }

    internal static async Task ExecuteAsync(ManagedDirectoryState state, OverlayServeRequest request, OwnedDirectoryManifest manifest,
        CancellationToken ct, Action<string>? boundary)
    {
        string root = state.Basis.ReceiptDirectory, planPath = Path.Combine(root, PlanName), cursorPath = Path.Combine(root, CursorName), preparePath = Path.Combine(root, PrepareName);
        var plan = await WindowsProcessWorkspaceProvider.ReadAsync<Plan>(planPath, ct, waitForPublication: false);
        var cursor = await WindowsProcessWorkspaceProvider.ReadAsync<Cursor>(cursorPath, ct, waitForPublication: false);
        if (plan is null && cursor is not null) throw Conflict();
        using var originalItems = manifest.OpenIndex(StringComparer.OrdinalIgnoreCase);
        if (plan is null)
        {
            if (Directory.Exists(state.OriginalPath) || File.Exists(state.OriginalPath)) throw Conflict("Preserve the original path that appeared before restoration planning.");
            using var basis = new OwnedDirectorySnapshotTree(manifest, state.Basis.Snapshot);
            using var source = JournalDeltaStore.OpenReadOnly(request.DeltaPath, request.Identity, basis, sourceFilePins: new WindowsJournalFilePins());
            var seal = source.Seal(); var prepare = await WindowsProcessWorkspaceProvider.ReadAsync<Preparation>(preparePath, ct, waitForPublication: false);
            if (prepare is null)
            {
                prepare = new(1, state.Basis, seal, Path.Combine(root, "restore-index-" + Guid.NewGuid().ToString("N")));
                await WindowsProcessWorkspaceProvider.WriteAsync(preparePath, prepare, ct);
            }
            if (prepare.Version != 1 || prepare.Basis != state.Basis || prepare.Seal != seal ||
                !WorkspaceStore.SamePath(Path.GetDirectoryName(prepare.Directory)!, root) || !Path.GetFileName(prepare.Directory).StartsWith("restore-index-", StringComparison.Ordinal)) throw Conflict();
            Directory.CreateDirectory(prepare.Directory);
            using var records = new PagedMetadataStore(Path.Combine(prepare.Directory, "records"), writable: true);
            var names = new RestoreNames(records); MetadataTreeRoot files = records.Empty;
            string basisRootIdentity;
            using (var pin = ManagedRestoreFileAccess.DirectoryHandle(state.Basis.Snapshot.RootPath, metadataWrite: false))
                basisRootIdentity = SnapshotInputChecks.FileIdentity(pin, directory: true);
            IEnumerable<DirectoryMetadataRecord> CleanupEntries()
            {
                foreach (var item in originalItems.Values)
                {
                    ct.ThrowIfCancellationRequested(); if (item.Entry.Path.Length == 0) continue;
                    string identity;
                    if (item.Entry.Directory)
                    {
                        using var pin = ManagedRestoreFileAccess.DirectoryHandle(PathOf(state.Basis.Snapshot.RootPath, item.Entry.Path), metadataWrite: false);
                        identity = SnapshotInputChecks.FileIdentity(pin, directory: true);
                    }
                    else
                    {
                        using var pin = ManagedRestoreFileAccess.Read(PathOf(state.Basis.Snapshot.RootPath, item.Entry.Path));
                        identity = ManagedRestoreFileAccess.Identity(pin);
                    }
                    yield return new(item.Entry, item.Hash, SourceReference: identity);
                }
            }
            var cleanupCatalog = PagedSnapshotCatalog.Build(Path.Combine(prepare.Directory, "cleanup"), originalItems[""].Entry, CleanupEntries(), ct: ct);
            using var cleanupReader = new PagedSnapshotCatalog(Path.Combine(prepare.Directory, "cleanup"), cleanupCatalog);
            SnapshotEntry? rootEntry = null; byte[] buffer = new byte[1048576]; bool rootMove = true;
            IEnumerable<DirectoryMetadataRecord> Entries()
            {
                foreach (var delta in source.EnumerateRestore())
                {
                    ct.ThrowIfCancellationRequested(); var entry = delta.Entry;
                    var metadata = new SnapshotEntry(entry.Path, entry.Directory, entry.Length, entry.Attributes,
                        entry.CreationTime, entry.LastAccessTime, entry.LastWriteTime, entry.UnixMode);
                    if (entry.BasePath is not null && entry.BasePath != entry.Path) rootMove = false;
                    if (entry.Path.Length == 0) { rootEntry = metadata; continue; }
                    if (entry.Directory) { yield return new(metadata, null); continue; }
                    var original = entry.BasePath is null ? null : originalItems.GetValueOrDefault(entry.BasePath);
                    bool unchanged = entry.BasePath is not null && !delta.Ranges.Any() && original?.Hash is not null &&
                        entry.BaseLength == entry.Length && entry.Length == original.Entry.Length;
                    string finalHash, retainedHash = EmptyHash, identity; string? staging = null;
                    if (entry.BasePath is not null)
                    {
                        identity = cleanupReader.Find(entry.BasePath)?.SourceReference ?? throw Conflict();
                        if (unchanged) retainedHash = original!.Hash!;
                        else
                        { using var input = ManagedRestoreFileAccess.Read(PathOf(state.Basis.Snapshot.RootPath, entry.BasePath)); retainedHash = RetainedHash(input, entry.BaseLength, delta.Ranges); }
                    }
                    else
                    {
                        staging = "new-" + Guid.NewGuid().ToString("N") + ".tmp";
                        using var created = new FileStream(Path.Combine(prepare.Directory, staging), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
                        created.Flush(true); identity = ManagedRestoreFileAccess.Identity(created);
                    }
                    if (unchanged) finalHash = original!.Hash!;
                    else
                    {
                        using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); long offset = 0;
                        while (offset < entry.Length)
                        {
                            ct.ThrowIfCancellationRequested(); int count = source.Read(entry.Id, offset, buffer.AsSpan(0, checked((int)Math.Min(buffer.Length, entry.Length - offset))));
                            if (count <= 0) throw Conflict("A fixed restore file ended early."); digest.AppendData(buffer, 0, count); offset += count;
                        }
                        finalHash = Convert.ToHexStringLower(digest.GetHashAndReset());
                    }
                    IEnumerable<MetadataEntry> Ranges()
                    {
                        long end = 0;
                        foreach (var range in delta.Ranges)
                        {
                            if (range.Offset < end || range.Length <= 0 || range.Offset > entry.Length - range.Length) throw Conflict();
                            end = checked(range.Offset + range.Length); byte[] data = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(data, range.Length);
                            yield return new(range.Offset.ToString("x16", System.Globalization.CultureInfo.InvariantCulture), data);
                        }
                    }
                    var ranges = records.Build(Ranges(), ct); var item = new FileRecord(entry, finalHash, retainedHash, identity, staging, ranges);
                    files = records.Set(files, entry.Id.ToString("N"), Encode(item, names), ranges);
                    yield return new(metadata, finalHash, SourceReference: entry.Id.ToString("N"));
                }
            }
            // EnumerateRestore always yields the root first; advance that one
            // item explicitly so catalog construction never needs a root array.
            using var enumerator = Entries().GetEnumerator(); bool hasFirst = enumerator.MoveNext();
            if (rootEntry is null) throw Conflict();
            IEnumerable<DirectoryMetadataRecord> Remaining()
            { if (hasFirst) yield return enumerator.Current; while (enumerator.MoveNext()) yield return enumerator.Current; }
            var catalog = PagedSnapshotCatalog.Build(Path.Combine(prepare.Directory, "paths"), rootEntry, Remaining(), ct: ct);
            using (var finalCatalog = new PagedSnapshotCatalog(Path.Combine(prepare.Directory, "paths"), catalog))
                foreach (var original in cleanupReader.Walk().Where(i => i.Entry.Directory))
                    if (finalCatalog.Find(original.Entry.Path)?.Entry.Directory != true) { rootMove = false; break; }
            records.Flush();
            if (source.Seal() != seal) throw new WorkspaceException("RestoreSealMismatch", "The journal changed during paged restore planning.");
            plan = new(3, state.Basis, seal, prepare.Directory, catalog, files, names.Root, cleanupCatalog, basisRootIdentity, rootMove);
            await WindowsProcessWorkspaceProvider.WriteAsync(planPath, plan, ct); boundary?.Invoke("restore.plan-published");
        }
        if (plan.Version != 3 || plan.Basis != state.Basis || !WorkspaceStore.SamePath(Path.GetDirectoryName(plan.Directory)!, root)) throw Conflict();
        string planHash = ManagedDirectoryBasis.HashFile(planPath);
        using var catalogReader = new PagedSnapshotCatalog(Path.Combine(plan.Directory, "paths"), plan.Catalog);
        using var fileStore = new PagedMetadataStore(Path.Combine(plan.Directory, "records")); var pathNames = new RestoreNames(fileStore) { Root = plan.Names };
        if (cursor is null) { cursor = new(planHash, null, 0); await WindowsProcessWorkspaceProvider.WriteAsync(cursorPath, cursor, ct); }
        if (cursor.PlanHash != planHash || cursor.Completed < 0 || cursor.Completed > plan.Files.Count) throw Conflict();
        using var movedRoot = plan.RootMove ? MoveRootIfNeeded(state, plan, ct, boundary) : null;
        if (plan.RootMove) StageDeletedLeaves(state, plan, catalogReader, fileStore, pathNames, cursor.FilesVerified, movedRoot!, ct, boundary);
        RejectForeign(state.OriginalPath, catalogReader);
        if (!cursor.FilesVerified)
        {
            foreach (var directory in catalogReader.Walk().Where(i => i.Entry.Directory))
            { ct.ThrowIfCancellationRequested(); Directory.CreateDirectory(PathOf(state.OriginalPath, directory.Entry.Path)); }
            using var metadata = new MetadataRestoreTree(manifest);
            using var journal = JournalDeltaStore.OpenReadOnly(request.DeltaPath, request.Identity, metadata, sourceFilePins: new WindowsJournalFilePins());
            if (journal.Seal() != plan.Seal) throw new WorkspaceException("RestoreSealMismatch", "Retain the different restoration journal.");
            journal.PrepareRestoreReads(plan.Seal); byte[] buffer = new byte[1048576];
            long completed = 0;
            foreach (var item in catalogReader.Walk())
            {
                ct.ThrowIfCancellationRequested(); if (item.Entry.Directory) continue;
                if (item.SourceReference is null) throw Conflict();
                var encoded = fileStore.Open(plan.Files).FindEntry(item.SourceReference) ?? throw Conflict();
                var file = Decode(encoded, item.Entry.Path, pathNames, fileStore); var entry = file.Entry;
                string target = PathOf(state.OriginalPath, entry.Path);
                string source = entry.BasePath is { } original ? PathOf(plan.RootMove ? state.OriginalPath : state.Basis.Snapshot.RootPath, original) : Path.Combine(plan.Directory, file.Staging ?? throw Conflict());
                using var access = new ManagedRestoreFileAccess(movedRoot, state.OriginalPath, File.Exists(source) ? [source, target] : [target]);
                if (completed < cursor.Completed)
                {
                    using var verified = ManagedRestoreFileAccess.Read(target); ManagedRestoreFileAccess.Require(verified, file.Identity);
                    if (Hash(verified) != file.FinalHash) throw Conflict("A completed restore file changed."); completed++; continue;
                }
                if (File.Exists(source) && !WorkspaceStore.SamePath(source, target))
                {
                    if (File.Exists(target)) throw Conflict("Both restore source and destination exist.");
                    using var moved = ManagedRestoreFileAccess.Read(source, moving: true); ManagedRestoreFileAccess.Require(moved, file.Identity);
                    string expected = entry.BasePath is { } basePath ? originalItems.GetValueOrDefault(basePath)?.Hash ?? throw Conflict() : EmptyHash;
                    if (Hash(moved) != expected) throw Conflict("The exact restore source changed before moving.");
                    if (movedRoot is null) ManagedRestoreFileAccess.Move(moved, target);
                    else ManagedRestoreFileAccess.MoveRelative(moved.SafeFileHandle, target,
                        WorkspaceStore.SamePath(Path.GetDirectoryName(target)!, state.OriginalPath) ? movedRoot : null);
                    boundary?.Invoke("file.after-move");
                }
                if (entry.BasePath is not null && file.Ranges.Count == 0 && entry.Length == entry.BaseLength && file.FinalHash == file.RetainedHash)
                {
                    using var stable = ManagedRestoreFileAccess.Read(target); ManagedRestoreFileAccess.Require(stable, file.Identity);
                    if (Hash(stable) != file.FinalHash) throw Conflict("An unchanged moved file changed.");
                    ManagedRestoreFileAccess.Metadata(stable, entry.Attributes, entry.CreationTime, entry.LastAccessTime, entry.LastWriteTime);
                    completed++; cursor = cursor with { AfterPath = null, Completed = completed };
                    if (completed % ManagedDirectoryRestore.CursorBatchSize == 0 || completed == plan.Files.Count)
                    { boundary?.Invoke("restore.before-cursor"); await WindowsProcessWorkspaceProvider.WriteAsync(cursorPath, cursor, ct); boundary?.Invoke("restore.after-cursor"); }
                    boundary?.Invoke("file.after-complete"); continue;
                }
                using (var output = ManagedRestoreFileAccess.Write(target, file.Identity, input =>
                {
                    if (entry.BasePath is not null && RetainedHash(input, entry.BaseLength, ReadRanges(fileStore, file.Ranges)) != file.RetainedHash)
                        throw Conflict("Retained bytes changed before private-range replay.");
                }))
                {
                    if (Hash(output) != file.FinalHash)
                    {
                        boundary?.Invoke("file.before-ranges-write"); output.SetLength(entry.BaseLength); output.SetLength(entry.Length);
                        foreach (var range in ReadRanges(fileStore, file.Ranges))
                            for (int at = 0; at < range.Length;)
                            {
                                ct.ThrowIfCancellationRequested(); int count = Math.Min(buffer.Length, range.Length - at);
                                journal.ReadRestoreRange(entry.Id, range.Offset + at, buffer.AsSpan(0, count), plan.Seal);
                                output.Position = range.Offset + at; output.Write(buffer, 0, count); at += count;
                            }
                        output.Flush(true); boundary?.Invoke("file.after-ranges-flush");
                        if (Hash(output) != file.FinalHash) throw Conflict("A restored file differs from the frozen plan.");
                    }
                    ManagedRestoreFileAccess.Metadata(output, entry.Attributes, entry.CreationTime, entry.LastAccessTime, entry.LastWriteTime);
                }
                completed++;
                cursor = cursor with { AfterPath = null, Completed = completed };
                if (completed % ManagedDirectoryRestore.CursorBatchSize == 0 || completed == plan.Files.Count)
                { boundary?.Invoke("restore.before-cursor"); await WindowsProcessWorkspaceProvider.WriteAsync(cursorPath, cursor, ct); boundary?.Invoke("restore.after-cursor"); }
                boundary?.Invoke("file.after-complete");
            }
            if (completed != plan.Files.Count) throw Conflict();
            RejectForeign(state.OriginalPath, catalogReader);
            foreach (var directory in PostOrderDirectories(catalogReader))
            {
                if (movedRoot is not null && directory.Path.Length == 0) ManagedRestoreFileAccess.Metadata(movedRoot, directory);
                else SetDirectoryMetadata(PathOf(state.OriginalPath, directory.Path), directory);
            }
            cursor = cursor with { FilesVerified = true, Completed = completed }; await WindowsProcessWorkspaceProvider.WriteAsync(cursorPath, cursor, ct);
            boundary?.Invoke("restore.after-verified");
        }
        else
        {
            foreach (var item in catalogReader.Walk().Where(i => !i.Entry.Directory))
            {
                ct.ThrowIfCancellationRequested(); var encoded = fileStore.Open(plan.Files).FindEntry(item.SourceReference!) ?? throw Conflict();
                var file = Decode(encoded, item.Entry.Path, pathNames, fileStore);
                using var access = new ManagedRestoreFileAccess(movedRoot, state.OriginalPath, PathOf(state.OriginalPath, file.Entry.Path));
                using var input = ManagedRestoreFileAccess.Read(PathOf(state.OriginalPath, file.Entry.Path));
                ManagedRestoreFileAccess.Require(input, file.Identity); if (Hash(input) != file.FinalHash) throw Conflict();
            }
        }
        if (plan.RootMove) ReclaimStagedLeaves(state, plan, catalogReader, fileStore, pathNames, ct, boundary);
        else CleanupBasis(state, plan, ct, boundary);
        if (Directory.Exists(request.DeltaPath))
            CurrentJournalReclamation.ReclaimDirectory(request.DeltaPath, plan.Seal, ["identity.json"], Path.Combine(root, "paged-restore-reclaim.bin"),
                directoryGuardFactory: path => new WindowsJournalDirectoryGuard(path, CurrentJournalReclamation.GuardName), filePins: new WindowsJournalFilePins());
        boundary?.Invoke("restore.after-delta-reclaim");
    }

    private static SafeFileHandle MoveRootIfNeeded(ManagedDirectoryState state, Plan plan, CancellationToken ct, Action<string>? boundary)
    {
        bool move = Directory.Exists(state.Basis.Snapshot.RootPath), original = Directory.Exists(state.OriginalPath);
        if (move == original) throw Conflict("Exactly one owned root must exist during paged directory movement.");
        Directory.CreateDirectory(Path.GetDirectoryName(state.OriginalPath)!);
        using var parents = new ManagedRestoreFileAccess(state.Basis.Snapshot.RootPath, state.OriginalPath);
        var root = ManagedRestoreFileAccess.DirectoryHandle(move ? state.Basis.Snapshot.RootPath : state.OriginalPath,
            moving: move, allowChildMutations: true);
        try
        {
            if (SnapshotInputChecks.FileIdentity(root, directory: true) != plan.BasisRootIdentity) throw Conflict("The owned restore root was replaced.");
            if (move)
            {
                using (var catalog = new PagedSnapshotCatalog(Path.Combine(plan.Directory, "cleanup"), plan.CleanupCatalog))
                using (var proof = new WindowsDirectoryTreeProof(state.Basis.Snapshot.RootPath, root))
                {
                    var files = new List<FileStream>(); long seen = 1;
                    try
                    {
                        foreach (var item in catalog.Walk())
                        {
                            ct.ThrowIfCancellationRequested(); if (item.Entry.Path.Length == 0) continue;
                            if (item.Entry.Directory)
                            {
                                var pin = proof.PinDirectory(item.Entry.Path);
                                if (SnapshotInputChecks.FileIdentity(pin, directory: true) != item.SourceReference) throw Conflict();
                            }
                            else
                            {
                                var pin = ManagedRestoreFileAccess.Read(PathOf(state.Basis.Snapshot.RootPath, item.Entry.Path), proof: proof); files.Add(pin);
                                ManagedRestoreFileAccess.Require(pin, item.SourceReference ?? throw Conflict());
                                if (Hash(pin) != item.ContentHash) throw Conflict("Basis content changed before the root move.");
                            }
                        }
                        foreach (string path in Directory.EnumerateFileSystemEntries(state.Basis.Snapshot.RootPath, "*", SearchOption.AllDirectories))
                        {
                            WorkspaceStore.EnsurePlainPath(path); var item = catalog.Find(Path.GetRelativePath(state.Basis.Snapshot.RootPath, path).Replace('\\', '/'));
                            if (item is null || item.Entry.Directory != Directory.Exists(path)) throw Conflict(); seen++;
                        }
                        if (seen != catalog.Descriptor.Count) throw Conflict("Basis membership changed before the root move.");
                    }
                    finally { foreach (var pin in files) pin.Dispose(); }
                }
                // NTFS requires descendant handles closed for the directory
                // rename. The exact root and ancestor chains stay pinned.
                boundary?.Invoke("paged-root.before-move"); ManagedRestoreFileAccess.Move(root, state.OriginalPath); boundary?.Invoke("paged-root.after-move");
            }
            WindowsOwnedDirectoryLease.RequirePath(root, state.OriginalPath); return root;
        }
        catch { root.Dispose(); throw; }
    }

    private static string DeletedPath(Plan plan, string path) => Path.Combine(plan.Directory,
        "deleted-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(path))) + ".tmp");
    private static bool RetainedOriginal(DirectoryMetadataRecord? final, string original, PagedMetadataStore files, MetadataTreeRoot root, RestoreNames names)
    {
        if (final is null || final.Entry.Directory || final.SourceReference is null) return false;
        var entry = files.Open(root).FindEntry(final.SourceReference) ?? throw Conflict();
        return Decode(entry, final.Entry.Path, names, files).Entry.BasePath == original;
    }
    private static void StageDeletedLeaves(ManagedDirectoryState state, Plan plan, PagedSnapshotCatalog finalCatalog,
        PagedMetadataStore files, RestoreNames names, bool verified, SafeFileHandle rootPin, CancellationToken ct, Action<string>? boundary)
    {
        using var catalog = new PagedSnapshotCatalog(Path.Combine(plan.Directory, "cleanup"), plan.CleanupCatalog);
        foreach (var original in catalog.Walk().Where(i => !i.Entry.Directory))
        {
            ct.ThrowIfCancellationRequested(); var final = finalCatalog.Find(original.Entry.Path);
            if (RetainedOriginal(final, original.Entry.Path, files, plan.Files, names)) continue;
            string source = PathOf(state.OriginalPath, original.Entry.Path), stage = DeletedPath(plan, original.Entry.Path);
            bool staged = File.Exists(stage), exists = File.Exists(source);
            if (!staged && verified)
            {
                if (exists && (final is null || final.Entry.Directory)) throw Conflict();
                continue;
            }
            if (staged)
            {
                using var pin = ManagedRestoreFileAccess.Read(stage); ManagedRestoreFileAccess.Require(pin, original.SourceReference ?? throw Conflict());
                if (Hash(pin) != original.ContentHash) throw Conflict();
                if (exists)
                {
                    if (final is null || final.Entry.Directory || final.SourceReference is null) throw Conflict();
                    var replacement = Decode(files.Open(plan.Files).FindEntry(final.SourceReference) ?? throw Conflict(), final.Entry.Path, names, files);
                    using var current = ManagedRestoreFileAccess.Read(source); ManagedRestoreFileAccess.Require(current, replacement.Identity);
                }
                continue;
            }
            if (!exists) throw Conflict("A deleted source has neither its original file nor its owned staging file.");
            using var access = new ManagedRestoreFileAccess(rootPin, state.OriginalPath, source, stage);
            using var moved = ManagedRestoreFileAccess.Read(source, moving: true); ManagedRestoreFileAccess.Require(moved, original.SourceReference ?? throw Conflict());
            if (Hash(moved) != original.ContentHash) throw Conflict();
            ManagedRestoreFileAccess.Move(moved, stage); boundary?.Invoke("paged-root.leaf-staged");
        }
    }
    private static void ReclaimStagedLeaves(ManagedDirectoryState state, Plan plan, PagedSnapshotCatalog finalCatalog,
        PagedMetadataStore files, RestoreNames names, CancellationToken ct, Action<string>? boundary)
    {
        using var catalog = new PagedSnapshotCatalog(Path.Combine(plan.Directory, "cleanup"), plan.CleanupCatalog);
        foreach (var original in catalog.Walk().Where(i => !i.Entry.Directory))
        {
            ct.ThrowIfCancellationRequested(); if (RetainedOriginal(finalCatalog.Find(original.Entry.Path), original.Entry.Path, files, plan.Files, names)) continue;
            string stage = DeletedPath(plan, original.Entry.Path); if (!File.Exists(stage)) continue;
            using var access = new ManagedRestoreFileAccess(stage); using var file = ManagedRestoreFileAccess.Read(stage, moving: true);
            ManagedRestoreFileAccess.Require(file, original.SourceReference ?? throw Conflict()); if (Hash(file) != original.ContentHash) throw Conflict();
            ManagedRestoreFileAccess.Delete(file); boundary?.Invoke("paged-root.leaf-reclaimed");
        }
    }

    private static IEnumerable<DeltaRestoreRange> ReadRanges(PagedMetadataStore store, MetadataTreeRoot root)
    {
        long end = 0;
        foreach (var item in store.Open(root).Enumerate())
        {
            if (item.Value.Length != 4 || !long.TryParse(item.Key, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out long offset)) throw Conflict();
            int length = BinaryPrimitives.ReadInt32LittleEndian(item.Value.Span);
            if (offset < end || length <= 0 || offset > long.MaxValue - length) throw Conflict(); end = offset + length; yield return new(offset, length);
        }
    }
    private static string RetainedHash(FileStream input, long length, IEnumerable<DeltaRestoreRange> ranges)
    {
        if (input.Length < length) throw Conflict("Retained source bytes ended early.");
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); byte[] buffer = new byte[65536]; long at = 0;
        foreach (var range in ranges.Append(new(length, 0)))
        {
            long end = Math.Min(length, range.Offset); input.Position = at;
            while (at < end) { int n = input.Read(buffer, 0, checked((int)Math.Min(buffer.Length, end - at))); if (n <= 0) throw Conflict(); hash.AppendData(buffer, 0, n); at += n; }
            at = Math.Min(length, Math.Max(at, checked(range.Offset + range.Length)));
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
    private static void RejectForeign(string root, PagedSnapshotCatalog catalog)
    {
        WorkspaceStore.EnsurePlainPath(root); if (!Directory.Exists(root)) return;
        foreach (string path in Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories))
        {
            WorkspaceStore.EnsurePlainPath(path); string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            var expected = catalog.Find(relative);
            if (expected is null || expected.Entry.Directory != Directory.Exists(path)) throw Conflict("An unowned item appeared in the restored original.");
        }
    }
    private static IEnumerable<SnapshotEntry> PostOrderDirectories(PagedSnapshotCatalog catalog)
    {
        var pending = new Stack<(SnapshotEntry Entry, IEnumerator<DirectoryMetadataRecord> Children)>();
        pending.Push((catalog.Descriptor.Root, catalog.EnumerateChildren("").GetEnumerator()));
        try
        {
            while (pending.TryPeek(out var level))
            {
                if (!level.Children.MoveNext()) { level.Children.Dispose(); pending.Pop(); yield return level.Entry; continue; }
                var item = level.Children.Current; if (item.Entry.Directory) pending.Push((item.Entry, catalog.EnumerateChildren(item.Entry.Path).GetEnumerator()));
            }
        }
        finally { foreach (var level in pending) level.Children.Dispose(); }
    }
    private static void SetDirectoryMetadata(string path, SnapshotEntry entry)
    {
        Directory.SetCreationTimeUtc(path, DateTime.FromFileTimeUtc(entry.CreationTime)); Directory.SetLastAccessTimeUtc(path, DateTime.FromFileTimeUtc(entry.LastAccessTime));
        Directory.SetLastWriteTimeUtc(path, DateTime.FromFileTimeUtc(entry.LastWriteTime)); File.SetAttributes(path, (FileAttributes)(entry.Attributes & ~16u));
    }
    private static void CleanupBasis(ManagedDirectoryState state, Plan plan, CancellationToken ct, Action<string>? boundary)
    {
        string root = state.Basis.Snapshot.RootPath; if (!Directory.Exists(root)) return;
        using var catalog = new PagedSnapshotCatalog(Path.Combine(plan.Directory, "cleanup"), plan.CleanupCatalog);
        using var access = new ManagedRestoreFileAccess(root);
        using var rootPin = ManagedRestoreFileAccess.DirectoryHandle(root, moving: true);
        if (SnapshotInputChecks.FileIdentity(rootPin, directory: true) != plan.BasisRootIdentity) throw Conflict("The owned basis root was replaced.");
        using var proof = new WindowsDirectoryTreeProof(root, rootPin);
        foreach (var item in catalog.Walk().Where(i => i.Entry.Directory && i.Entry.Path.Length != 0))
        {
            ct.ThrowIfCancellationRequested(); if (!Directory.Exists(PathOf(root, item.Entry.Path))) continue;
            var pin = proof.PinDirectory(item.Entry.Path, moving: true);
            if (SnapshotInputChecks.FileIdentity(pin, directory: true) != item.SourceReference) throw Conflict("A remaining basis directory was replaced.");
        }
        var pinnedFiles = new List<FileStream>(); long files = 0, directories = 0;
        try
        {
            foreach (string path in Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested(); WorkspaceStore.EnsurePlainPath(path); string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                var expected = catalog.Find(relative);
                if (expected is null || expected.Entry.Directory != Directory.Exists(path)) throw Conflict("An unowned basis item remains.");
                if (expected.Entry.Directory) { directories++; continue; }
                var pin = ManagedRestoreFileAccess.Read(path, moving: true, proof: proof); pinnedFiles.Add(pin);
                ManagedRestoreFileAccess.Require(pin, expected.SourceReference ?? throw Conflict());
                if (expected.ContentHash is null || Hash(pin) != expected.ContentHash) throw Conflict("A remaining basis file changed."); files++;
            }
            long remainingFiles = 0, remainingDirectories = 0;
            foreach (string path in Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories))
            {
                WorkspaceStore.EnsurePlainPath(path); string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                var expected = catalog.Find(relative); if (expected is null || expected.Entry.Directory != Directory.Exists(path)) throw Conflict();
                if (expected.Entry.Directory) remainingDirectories++; else remainingFiles++;
            }
            if (remainingFiles != files || remainingDirectories != directories) throw Conflict("Basis membership changed while acquiring cleanup fences.");
            foreach (var pin in pinnedFiles) { ct.ThrowIfCancellationRequested(); ManagedRestoreFileAccess.Delete(pin); pin.Dispose(); }
            foreach (var directory in PostOrderDirectories(catalog))
            {
                ct.ThrowIfCancellationRequested(); if (directory.Path.Length == 0) continue;
                string path = PathOf(root, directory.Path); if (!Directory.Exists(path)) continue;
                if (Directory.EnumerateFileSystemEntries(path).Any()) throw Conflict("Preserve a directory with an unowned cleanup child.");
                var pin = proof.PinDirectory(directory.Path, moving: true); ManagedRestoreFileAccess.DeleteDirectory(pin); pin.Dispose();
            }
            if (Directory.EnumerateFileSystemEntries(root).Any()) throw Conflict();
            ManagedRestoreFileAccess.DeleteDirectory(rootPin);
        }
        finally { foreach (var pin in pinnedFiles) pin.Dispose(); }
        boundary?.Invoke("restore.after-basis-reclaim");
    }

    private static byte[] Encode(FileRecord file, RestoreNames names)
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, Encoding.UTF8, true); var entry = file.Entry;
        writer.Write((byte)1); writer.Write(entry.Id.ToByteArray()); writer.Write(entry.Length); writer.Write(entry.Attributes);
        writer.Write(entry.CreationTime); writer.Write(entry.LastAccessTime); writer.Write(entry.LastWriteTime); writer.Write(entry.BaseLength);
        writer.Write(entry.BasePath is not null); if (entry.BasePath is not null) writer.Write(names.Intern(entry.BasePath));
        writer.Write(entry.UnixMode.HasValue); if (entry.UnixMode.HasValue) writer.Write(entry.UnixMode.Value);
        writer.Write(Convert.FromHexString(file.FinalHash)); writer.Write(Convert.FromHexString(file.RetainedHash)); writer.Write(file.Identity);
        writer.Write(file.Staging is not null); if (file.Staging is not null) writer.Write(file.Staging); return stream.ToArray();
    }
    private static FileRecord Decode(MetadataEntry metadata, string path, RestoreNames names, PagedMetadataStore store)
    {
        try
        {
            using var reader = new BinaryReader(new MemoryStream(metadata.Value.ToArray()), Encoding.UTF8);
            if (reader.ReadByte() != 1) throw Conflict(); var id = new Guid(reader.ReadBytes(16)); long length = reader.ReadInt64(); uint attributes = reader.ReadUInt32();
            long created = reader.ReadInt64(), accessed = reader.ReadInt64(), written = reader.ReadInt64(), baseLength = reader.ReadInt64();
            string? basePath = reader.ReadBoolean() ? names.Resolve(reader.ReadBytes(32)) : null; uint? mode = reader.ReadBoolean() ? reader.ReadUInt32() : null;
            string final = Convert.ToHexStringLower(reader.ReadBytes(32)), retained = Convert.ToHexStringLower(reader.ReadBytes(32)), identity = reader.ReadString();
            string? staging = reader.ReadBoolean() ? reader.ReadString() : null;
            if (metadata.Key != id.ToString("N") || reader.BaseStream.Position != reader.BaseStream.Length || length < 0 || baseLength < 0 || final.Length != 64 || retained.Length != 64 ||
                string.IsNullOrEmpty(identity) || staging is not null && (Path.GetFileName(staging) != staging || !staging.StartsWith("new-", StringComparison.Ordinal) || !staging.EndsWith(".tmp", StringComparison.Ordinal))) throw Conflict();
            return new(new(id, path, false, length, attributes, created, accessed, written, basePath, baseLength, UnixMode: mode), final, retained, identity, staging, metadata.Child ?? store.Empty);
        }
        catch (Exception error) when (error is EndOfStreamException or ArgumentException or IOException) { throw Conflict(); }
    }
    private sealed class RestoreNames(PagedMetadataStore store)
    {
        public MetadataTreeRoot Root = store.Empty;
        public byte[] Intern(string path)
        {
            byte[] parent = new byte[32];
            foreach (string component in path.Split('/'))
            {
                byte[] name = Encoding.UTF8.GetBytes(component), data = new byte[32 + name.Length]; parent.CopyTo(data, 0); name.CopyTo(data, 32);
                parent = SHA256.HashData(data); string key = Convert.ToHexStringLower(parent); var existing = store.Open(Root).Find(key);
                if (existing is null) Root = store.Set(Root, key, data); else if (!existing.AsSpan().SequenceEqual(data)) throw Conflict();
            }
            return parent;
        }
        public string Resolve(byte[] id)
        {
            var components = new Stack<string>(); var visited = new HashSet<string>(StringComparer.Ordinal);
            while (!id.AsSpan().SequenceEqual(new byte[32]))
            {
                string key = Convert.ToHexStringLower(id); if (!visited.Add(key)) throw Conflict();
                byte[] data = store.Open(Root).Find(key) ?? throw Conflict();
                if (data.Length <= 32 || !SHA256.HashData(data).AsSpan().SequenceEqual(id)) throw Conflict();
                string name = Encoding.UTF8.GetString(data, 32, data.Length - 32); if (name.Contains('/') || name.Contains('\0')) throw Conflict();
                components.Push(name); id = data.AsSpan(0, 32).ToArray();
            }
            return string.Join('/', components);
        }
    }
    private sealed class MetadataRestoreTree(OwnedDirectoryManifest manifest) : ISnapshotTree, IDisposable
    {
        private readonly OwnedDirectoryItemIndex items = manifest.OpenIndex(StringComparer.OrdinalIgnoreCase);
        public SnapshotReference Snapshot => manifest.Snapshot;
        public void EnsureAvailable() { }
        public SnapshotEntry? Stat(string path) => items.GetValueOrDefault(path)?.Entry;
        public IReadOnlyList<SnapshotEntry> List(string path) => EnumerateChildren(path).ToArray();
        public IEnumerable<SnapshotEntry> EnumerateChildren(string path) => items.EnumerateChildren(path).Select(i => i.Entry);
        public int Read(string path, long offset, Span<byte> output) => throw Conflict("Restore payload must come from the exact stopped journal.");
        public void Dispose() => items.Dispose();
    }
}
