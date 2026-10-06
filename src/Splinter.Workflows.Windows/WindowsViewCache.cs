using Splinter.Core;
using Splinter.Runtime;
using Splinter.Platform.Windows;
using static Splinter.Workflows.Windows.ProductState;

namespace Splinter.Workflows.Windows;

internal sealed partial class WindowsViewProvider
{
    internal static OverlayIdentity CacheIdentity(ViewCacheIdentity cache) =>
        new(2, cache.CacheId, cache.Snapshot, cache.MaxJournalBytes, 1024 * 1024, [], CacheRoots);

    internal string MaintenancePath(string viewId) => Path.Combine(Path.GetDirectoryName(ViewPath(viewId))!, "cache-maintenance.json");
    internal void RequireCacheReady(string viewId) => Require(Read<CacheMaintenance>(MaintenancePath(viewId)) is not { Completed: false },
        "ViewCacheMaintenancePending", "Resume the exact pending compact-cache request before changing or starting this view.");
    internal void RequireNoPendingCacheMaintenance(string? allowedRequestKey)
    {
        string views = Path.Combine(Root, "views");
        WorkspaceStore.EnsurePlainPath(views);
        if (!Directory.Exists(views)) return;
        foreach (string directory in Directory.EnumerateDirectories(views))
        {
            var pending = Read<CacheMaintenance>(Path.Combine(directory, "cache-maintenance.json"));
            Require(pending is not { Completed: false } || pending.RequestKey == allowedRequestKey,
                "ViewCacheMaintenancePending", "Resume the exact pending compact-cache request before creating another product operation.");
        }
    }

    internal async Task<CacheMaintenance> CompactCacheAsync(string viewId, ViewOwner owner, string requestKey, long budget, CancellationToken ct)
    {
        var view = View(viewId);
        await VerifyOwnerAsync(ProjectId, viewId, owner, ct);
        Require(!view.Closed && budget > 0, "ViewCacheMismatch", "Compact an open cache with an explicit positive overlap budget.");
        Quiescent((await ObserveViewAsync(ProjectId, viewId, ct)).Resources);
        using var basis = await Basis(view.CacheWorkerId, view.Cache.Snapshot, ct);
        using var lease = SnapshotProvider(view.CacheWorkerId).AcquireReadLease();
        return CompactCacheFiles(CachePath(view.Cache.CacheId), MaintenancePath(viewId), view.Cache, requestKey, budget, basis);
    }

    // The product operation lock and offline resource observation surround this
    // durable file protocol. Tests inject a fake immutable tree and interruption.
    internal static CacheMaintenance CompactCacheFiles(string directory, string pointer, ViewCacheIdentity cache,
        string requestKey, long budget, ISnapshotTree basis, Action<string>? boundary = null)
    {
        string receiptPath = Path.Combine(Path.GetDirectoryName(pointer)!, "cache-maintenance", WorkspaceStore.Key(requestKey) + ".json");
        var pending = Read<CacheMaintenance>(pointer);
        Require(pending is not { Completed: false } || pending.RequestKey == requestKey,
            "ViewCacheMaintenancePending", "Resume the exact pending cache maintenance request.");
        var saved = Read<CacheMaintenance>(receiptPath);
        if (saved is not null)
        {
            Require(saved.RequestKey == requestKey && saved.Cache == cache && saved.MaxEstimatedJournalOverlapBytes == budget &&
                saved.Completed && saved.Compaction is not null && saved.After is not null,
                "ViewReceiptMismatch", "Keep the original completed cache maintenance receipt.");
            if (pending is { Completed: false }) Save(pointer, saved);
            return saved;
        }
        if (pending?.RequestKey != requestKey) pending = null;
        var identity = CacheIdentity(cache);
        using var check = SourceViewDeltaStore.OpenCacheReadOnly(directory, identity, basis);
        if (pending is null)
        {
            Require(budget > 0, "InvalidCompactionBudget", "A positive overlap budget is required.");
            Require(check.EstimateCompactionJournalOverlapBytes() <= budget, "CompactionOverlapBudgetExceeded",
                "The estimated old and candidate journals exceed this request's overlap budget; the cache is unchanged.");
            pending = new(1, requestKey, cache, budget, check.Seal(), check.ObservePrivateUsage());
            Save(pointer, pending); boundary?.Invoke("maintenance:intent");
        }
        Require(pending.Cache == cache && pending.MaxEstimatedJournalOverlapBytes == budget && !pending.Completed,
            "ViewReceiptMismatch", "Keep the same cache and overlap budget during recovery.");
        if (pending.Compaction is null && !File.Exists(Path.Combine(directory, "compaction-intent.json")))
            Require(check.Seal() == pending.BeforeSeal, "ViewSourceChanged", "The cache changed after maintenance began.");
        check.Dispose();
        var tree = new CacheSnapshotTree(basis, cache.Snapshot, CacheRoots);
        if (pending.Compaction is null)
        {
            var compacted = JournalDeltaStore.CompactDirectory(directory, identity, tree, boundary, budget);
            Require(compacted.Before == pending.BeforeSeal, "ViewSourceChanged", "Cache changed after maintenance began; preserve every generation.");
            long? before = OperatingSystem.IsWindows() ? NtfsSnapshotWorkspaceBackend.Allocation(Path.Combine(directory, "delta.journal.previous")) : null;
            long? after = OperatingSystem.IsWindows() ? NtfsSnapshotWorkspaceBackend.Allocation(Path.Combine(directory, "delta.journal")) : null;
            compacted = compacted with { BeforeAllocatedBytes = before, AfterAllocatedBytes = after,
                PeakTemporaryAllocatedBytes = before is not null && after is not null ? before + after : null };
            using var current = SourceViewDeltaStore.OpenCacheReadOnly(directory, identity, basis);
            Require(current.Seal() == compacted.After && current.ObservePrivateUsage().LivePrivateBytes == pending.Before.LivePrivateBytes,
                "ViewSourceChanged", "Compaction must preserve the selected cache state.");
            pending = pending with { Compaction = compacted, After = current.ObservePrivateUsage() };
            Save(pointer, pending); boundary?.Invoke("maintenance:observed");
        }
        using (var current = SourceViewDeltaStore.OpenCacheReadOnly(directory, identity, basis))
            Require(current.Seal() == pending.Compaction.After, "ViewSourceChanged", "Cache changed before maintenance completed.");
        if (File.Exists(Path.Combine(directory, "compaction-intent.json"))) JournalDeltaStore.CompleteCompaction(directory, identity);
        Require(!File.Exists(Path.Combine(directory, "delta.journal.previous")) && !File.Exists(Path.Combine(directory, "delta.journal.compacting")),
            "ViewReleaseUncertain", "Compaction temporary generations remain; preserve them.");
        boundary?.Invoke("maintenance:reclaimed");
        var completed = pending with { Completed = true };
        Save(receiptPath, completed); boundary?.Invoke("maintenance:receipt");
        Save(pointer, completed);
        return completed;
    }

    public async Task CloseCacheAsync(string projectId, ViewCacheIdentity cache, ViewOwner owner, CancellationToken ct)
    {
        var view = View(cache.ViewId);
        RequireCacheReady(cache.ViewId);
        await VerifyOwnerAsync(projectId, cache.ViewId, owner, ct);
        Require(view.Cache == cache, "ViewCacheMismatch", "Close only the retained cache identity.");
        Quiescent((await ObserveViewAsync(projectId, cache.ViewId, ct)).Resources);
        Save(ViewPath(cache.ViewId), view with { Closed = true });
        string directory = CachePath(cache.CacheId);
        string receipt = Path.Combine(Path.GetDirectoryName(ViewPath(cache.ViewId))!, "cache-reclamation.json");
        Require(!File.Exists(directory), "ViewReleaseUncertain", "A file replaced the owned cache directory.");
        SnapshotDeltaSeal? seal = Read<JournalReclamationIntent>(receipt)?.Seal;
        if (File.Exists(Path.Combine(directory, "delta.journal")))
        {
            using var lease = SnapshotProvider(view.CacheWorkerId).AcquireReadLease();
            using var basis = await Basis(view.CacheWorkerId, cache.Snapshot, ct);
            using var check = SourceViewDeltaStore.OpenCacheReadOnly(directory, CacheIdentity(cache), basis);
            seal = check.Seal();
        }
        Require(!Directory.Exists(directory) || seal is not null, "ViewReleaseUncertain", "A remaining cache needs its verified reclamation seal.");
        if (seal is not null) JournalReclamation.Reclaim(directory, seal, ["identity.json"], receipt);
        Require(!Directory.Exists(directory) && !File.Exists(directory), "ViewReleaseUncertain", "The cache is not positively absent.");
    }
}
