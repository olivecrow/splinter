using System.Security.Cryptography;
using System.Text.Json;
using System.Collections.Concurrent;
using Microsoft.Win32.SafeHandles;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Platform.Windows;

internal static partial class ManagedPagedDirectoryRestore
{
    private static bool SameNamespace(JournalDeltaStore journal, OwnedDirectoryManifest manifest, CancellationToken ct)
    {
        using var expected = manifest.Items.GetEnumerator(); long count = 0;
        foreach (var item in journal.EnumerateRestore())
        {
            ct.ThrowIfCancellationRequested();
            if (!expected.MoveNext()) return false;
            var entry = item.Entry; var original = expected.Current.Entry;
            if (entry.Deleted || entry.BasePath != original.Path || entry.BaseLength != original.Length || item.Ranges.Any() ||
                new SnapshotEntry(entry.Path, entry.Directory, entry.Length, entry.Attributes, entry.CreationTime,
                    entry.LastAccessTime, entry.LastWriteTime, entry.UnixMode) != original) return false;
            count++;
        }
        return !expected.MoveNext() && count == manifest.ItemCount;
    }

    private static async Task FinishUnchanged(ManagedDirectoryState state, OverlayServeRequest request, OwnedDirectoryManifest manifest,
        Plan expectedPlan, SafeFileHandle? initialRoot, CancellationToken ct, Action<string>? boundary)
    {
        string receipt = state.Basis.ReceiptDirectory, planPath = Path.Combine(receipt, PlanName), cursorPath = Path.Combine(receipt, CursorName);
        ManagedUnchangedDirectoryRestore.ValidateManifest(state, manifest);
        using var planPin = ManagedRestoreFileAccess.Read(planPath);
        var plan = JsonSerializer.Deserialize<Plan>(planPin, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? throw Conflict();
        planPin.Position = 0; string hash = Convert.ToHexStringLower(SHA256.HashData(planPin));
        if (plan != expectedPlan || plan.Version != 3 || !plan.Unchanged || !plan.RootMove || plan.Files.Count != 0 || plan.Names.Count != 0 ||
            plan.Seal.DurableSequence != 0 || plan.Basis != state.Basis || state.ApplicationKind != "unity" || state.DirectoryKind != "Library" || state.Basis.RelativePath != "Library" ||
            plan.Catalog != plan.CleanupCatalog || !WorkspaceStore.SamePath(Path.GetDirectoryName(plan.Directory)!, receipt)) throw Conflict();
        using var catalog = new PagedSnapshotCatalog(Path.Combine(plan.Directory, "cleanup"), plan.CleanupCatalog);
        using (var original = manifest.Items.GetEnumerator())
        {
            long count = 0;
            foreach (var item in catalog.Walk())
            {
                ct.ThrowIfCancellationRequested();
                if (!original.MoveNext() || item.Entry != original.Current.Entry || item.ContentHash != original.Current.Hash ||
                    item.Entry.Path.Length != 0 && string.IsNullOrEmpty(item.SourceReference)) throw Conflict();
                count++;
            }
            if (original.MoveNext() || count != manifest.ItemCount || count != catalog.Descriptor.Count) throw Conflict();
        }
        boundary?.Invoke("unchanged.plan-checked");
        var cursor = await WindowsProcessWorkspaceProvider.ReadAsync<Cursor>(cursorPath, ct, waitForPublication: false);
        if (cursor is not null && (cursor.PlanHash != hash || cursor.Completed != 0 || cursor.AfterPath is not null)) throw Conflict();
        bool move = Directory.Exists(state.Basis.Snapshot.RootPath);
        if (move == Directory.Exists(state.OriginalPath) || move && cursor?.FilesVerified == true) throw Conflict();
        string reclaim = Path.Combine(receipt, "paged-restore-reclaim.bin"); bool pending = CurrentJournalReclamation.HasIntent(reclaim);
        if (pending && (move || cursor?.FilesVerified != true)) throw Conflict();
        using var identity = pending ? null : ManagedUnchangedDirectoryRestore.PinIdentity(request, ManagedUnchangedDirectoryRestore.IdentityHash(request.Identity));
        using var journal = pending ? null : ManagedRestoreJournalProof.Open(request, manifest);
        if (journal is not null && (journal.Seal() != plan.Seal || !SameNamespace(journal, manifest, ct))) throw Conflict();
        boundary?.Invoke("unchanged.journal-proved");
        if (cursor is null) await WindowsProcessWorkspaceProvider.WriteAsync(cursorPath, new Cursor(hash, null, 0), ct);
        Directory.CreateDirectory(Path.GetDirectoryName(state.OriginalPath)!);
        using var ancestry = new ManagedRestoreFileAccess(state.Basis.Snapshot.RootPath, state.OriginalPath);
        using var resumedRoot = initialRoot is null ? ManagedRestoreFileAccess.DirectoryHandle(move ? state.Basis.Snapshot.RootPath : state.OriginalPath, moving: move) : null;
        var root = initialRoot ?? resumedRoot!;
        if (SnapshotInputChecks.FileIdentity(root, directory: true) != plan.BasisRootIdentity) throw Conflict();
        if (move)
        {
            // A fresh call transfers its still-proved basis reader. A retry has
            // no inherited physical proof and must hash every exact leaf again.
            if (initialRoot is null)
                using (var before = new UnchangedPins(state.Basis.Snapshot.RootPath, root, catalog, plan, ct, boundary)) { }
            ManagedUnchangedDirectoryRestore.RequireAbsent(state.OriginalPath);
            ManagedUnchangedDirectoryRestore.RequireSeal(request, plan.Seal);
            boundary?.Invoke("paged-root.before-move"); ManagedRestoreFileAccess.Move(root, state.OriginalPath); boundary?.Invoke("paged-root.after-move");
        }
        ManagedUnchangedDirectoryRestore.RequireAbsent(state.Basis.Snapshot.RootPath);
        using var final = new UnchangedPins(state.OriginalPath, root, catalog, plan, ct, boundary);
        boundary?.Invoke("restore.after-proof");
        if (!pending) ManagedUnchangedDirectoryRestore.RequireSeal(request, plan.Seal);
        final.ApplyMetadata(ct, boundary);
        final.RequireMembership(ct);
        boundary?.Invoke("unchanged.final-membership-proved");
        await WindowsProcessWorkspaceProvider.WriteAsync(cursorPath, new Cursor(hash, null, 0, FilesVerified: true), ct);
        boundary?.Invoke("restore.after-verified");
        journal?.Dispose();
        CurrentJournalReclamation.ReclaimDirectory(request.DeltaPath, plan.Seal, ["identity.json"], reclaim,
            stage => { if (stage == "intent") identity?.Dispose(); boundary?.Invoke("restore.reclaim." + stage); },
            directoryGuardFactory: path => new WindowsJournalDirectoryGuard(path, CurrentJournalReclamation.GuardName), filePins: new WindowsJournalFilePins());
        boundary?.Invoke("restore.after-delta-reclaim");
    }

    // Metadata stays in the bounded catalog. Only mandatory native leaf and
    // directory fences scale with the complete protected physical namespace.
    private sealed class UnchangedPins : IDisposable
    {
        private readonly string path;
        private readonly SafeFileHandle root;
        private readonly PagedSnapshotCatalog catalog;
        private readonly Plan plan;
        private readonly SafeFileHandle rootFence;
        private readonly WindowsDirectoryTreeProof proof;
        private readonly List<FileStream?[]> files = [];
        private int fileCount;
        internal UnchangedPins(string path, SafeFileHandle root, PagedSnapshotCatalog catalog, Plan plan, CancellationToken ct, Action<string>? boundary = null)
        {
            this.path = path; this.root = root; this.catalog = catalog; this.plan = plan;
            rootFence = ManagedRestoreFileAccess.PinDirectoryAgainstDataWrites(path, root);
            // Attribute access adds no data-write access and does not change
            // no-write/no-delete sharing. Retain the same directory handles
            // through their final metadata writes instead of reopening them.
            try { proof = new(path, rootFence); }
            catch { rootFence.Dispose(); throw; }
            try
            {
                foreach (var item in catalog.Walk().Where(item => item.Entry.Directory))
                {
                    ct.ThrowIfCancellationRequested(); var pin = proof.PinDirectory(item.Entry.Path);
                    if (SnapshotInputChecks.FileIdentity(pin, directory: true) != (item.Entry.Path.Length == 0 ? plan.BasisRootIdentity : item.SourceReference)) throw Conflict();
                }
                boundary?.Invoke("unchanged.directories-pinned");
                IEnumerable<(DirectoryMetadataRecord Item, FileStream?[] Handles, int Offset)> Work()
                {
                    foreach (var item in CheckedMembership(path, catalog.Walk(), plan.Directory, ct, exact: true).Where(item => !item.Entry.Directory))
                    {
                        ct.ThrowIfCancellationRequested(); int offset = fileCount % ManagedDirectoryRestore.CursorBatchSize;
                        if (offset == 0) files.Add(new FileStream?[ManagedDirectoryRestore.CursorBatchSize]);
                        fileCount++; yield return (item, files[^1], offset);
                    }
                }
                // Demand-driven partitioning overlaps catalog production with
                // native opens/hashes, without per-128-file join barriers or
                // an unbounded metadata queue. Only mandatory fences persist.
                var work = Partitioner.Create(Work(), EnumerablePartitionerOptions.NoBuffering);
                try
                {
                    Parallel.ForEach(work, new ParallelOptions { MaxDegreeOfParallelism = OwnedDirectorySnapshotTree.DefaultWorkers, CancellationToken = ct }, job =>
                    {
                        ct.ThrowIfCancellationRequested(); var item = job.Item;
                        var file = proof.Read(item.Entry.Path); job.Handles[job.Offset] = file;
                        ManagedRestoreFileAccess.Require(file, item.SourceReference ?? throw Conflict());
                        if (file.Length != item.Entry.Length || OwnedDirectorySnapshotTree.Hash(file.SafeFileHandle, ct) != item.ContentHash)
                            throw Conflict("An unchanged restore file changed.");
                    });
                }
                catch (AggregateException error)
                { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.Flatten().InnerExceptions[0]).Throw(); throw; }
                boundary?.Invoke("unchanged.files-proved");
                boundary?.Invoke("unchanged.pinned-membership-proved");
            }
            catch { Dispose(); throw; }
        }
        internal void RequireMembership(CancellationToken ct)
        { if (KnownMembership(path, catalog, plan.Directory, ct) != catalog.Descriptor.Count) throw Conflict("The unchanged restore membership changed."); }
        internal void ApplyMetadata(CancellationToken ct, Action<string>? boundary)
        {
            int at = 0;
            var batch = new List<(FileStream File, SnapshotEntry Entry)>(ManagedDirectoryRestore.CursorBatchSize);
            void Apply()
            {
                OwnedDirectorySnapshotTree.ParallelFiles(batch, pair => ManagedRestoreFileAccess.Metadata(pair.File.SafeFileHandle, pair.Entry), OwnedDirectorySnapshotTree.DefaultWorkers, ct);
                foreach (var _ in batch) boundary?.Invoke("file.after-complete"); batch.Clear();
            }
            foreach (var item in catalog.Walk().Where(item => !item.Entry.Directory))
            {
                ct.ThrowIfCancellationRequested();
                batch.Add((files[at / ManagedDirectoryRestore.CursorBatchSize][at % ManagedDirectoryRestore.CursorBatchSize] ?? throw Conflict(), item.Entry));
                at++; if (batch.Count == ManagedDirectoryRestore.CursorBatchSize) Apply();
            }
            Apply(); if (at != fileCount) throw Conflict();
            boundary?.Invoke("unchanged.file-metadata-applied");
            foreach (var directory in PostOrderDirectories(catalog))
            {
                ct.ThrowIfCancellationRequested();
                ManagedRestoreFileAccess.Metadata(directory.Path.Length == 0 ? root : proof.PinDirectory(directory.Path), directory);
            }
            boundary?.Invoke("unchanged.directory-metadata-applied");
        }
        public void Dispose() { foreach (var batch in files) foreach (var file in batch) file?.Dispose(); files.Clear(); proof.Dispose(); rootFence.Dispose(); }
    }
}
