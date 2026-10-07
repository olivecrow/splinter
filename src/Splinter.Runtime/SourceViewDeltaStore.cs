using Splinter.Core;
using System.Text.Json;

namespace Splinter.Runtime;

// An offline-selected source generation and a separately owned persistent cache.
// The two journals are exclusive writers. A selection change requires disposal
// of the entire mounted view; live handle rebinding is deliberately unsupported.
public sealed class SourceViewDeltaStore : IWorkspaceDeltaStore
{
    private readonly object gate = new();
    private readonly CandidateSnapshotTree candidate;
    private readonly WorkspacePathPolicy policy;
    private readonly JournalDeltaStore source, cache;
    private readonly string[] cacheRoots;
    private readonly Dictionary<Guid, JournalDeltaStore> handles = [];
    private readonly string sourceDirectory, transferPath;
    private readonly Action<string>? transferBoundary;
    private bool transferIncomplete;
    private long transferCount, transferredPrivateBytes;
    private string? lastTransferFrom, lastTransferTo;
    private sealed record TransferIntent(int Version, string SourceDirectory, OverlayIdentity SourceIdentity,
        Guid Id, string From, string Destination, string Staging, bool FromCache, bool Replace, string Hash);
    public OverlayIdentity Identity { get; }
    public ImmutableCandidate Candidate => candidate.Candidate;

    public static JournalDeltaStore OpenCacheReadOnly(string directory, OverlayIdentity identity) =>
        identity.Snapshot.Provider == "validation-empty-cache" && identity.IncludedTopLevel is null
            ? JournalDeltaStore.OpenReadOnly(directory, identity, new EmptyCacheTree(identity.Snapshot))
            : throw new WorkspaceException("ViewCacheMismatch", "An exact snapshot basis is required for this cache.");

    public static JournalDeltaStore OpenCacheReadOnly(string directory, OverlayIdentity identity, ISnapshotTree basis) =>
        JournalDeltaStore.OpenReadOnly(directory, identity,
            new CacheSnapshotTree(basis, identity.Snapshot, identity.IncludedTopLevel ?? []));

    public SourceViewDeltaStore(string sourceDirectory, OverlayIdentity identity, ISnapshotTree basis,
        SourceViewSelection selection, Action<string>? transferBoundary = null, ISnapshotTree? cacheBasis = null)
    {
        policy = WorkspacePathPolicy.For(identity.Snapshot);
        if (selection.CacheTopLevel is not { Length: > 0 } || selection.CacheTopLevel.Any(p =>
                string.IsNullOrWhiteSpace(p) || policy.Normalize(p) != p || p.Contains('/')) ||
            selection.CacheTopLevel.Distinct(policy.Comparer).Count() != selection.CacheTopLevel.Length)
            throw new ArgumentException("Cache partition names must be distinct ordinary top-level names.");
        Identity = identity;
        this.sourceDirectory = Path.GetFullPath(sourceDirectory);
        this.transferBoundary = transferBoundary;
        transferPath = Path.Combine(selection.CacheDeltaPath, "partition-transfer.json");
        string[] directories = new[] { sourceDirectory, selection.CandidatePath, selection.CacheDeltaPath }
            .Select(p => Path.TrimEndingDirectorySeparator(Path.GetFullPath(p))).ToArray();
        for (int i = 0; i < directories.Length; i++)
            for (int j = i + 1; j < directories.Length; j++)
                if (directories[i].Equals(directories[j], OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
                    directories[i].StartsWith(directories[j] + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
                    directories[j].StartsWith(directories[i] + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                    throw new WorkspaceException("UnsafePath", "View source, immutable candidate and cache must be disjoint trees.");
        cacheRoots = selection.CacheTopLevel.Order(policy.Comparer).ToArray();
        if (new[] { "compaction-intent.json", "delta.journal.previous", "delta.journal.compacting" }
            .Any(name => File.Exists(Path.Combine(selection.CacheDeltaPath, name))))
            throw new WorkspaceException("ViewCacheMaintenancePending", "Finish this cache's retained compaction before starting a source view.");
        candidate = new CandidateSnapshotTree(selection.CandidatePath, basis);
        try
        {
            // Partition configuration is in the persisted identity, so it cannot
            // change on reconnect and reclassify previous source as cache.
            var cacheIdentity = selection.CacheIdentity ?? identity with { Id = identity.Id + "/cache", ExcludedTopLevel = cacheRoots,
                Snapshot = identity.Snapshot with { Id = identity.Id + "/cache", Provider = "validation-empty-cache",
                    RootPath = Path.GetFullPath(selection.CacheDeltaPath), Consistency = "empty-owned-cache" } };
            if (cacheIdentity.Snapshot.PathPolicy != identity.Snapshot.PathPolicy)
                throw new WorkspaceException("ViewCacheMismatch", "Cache and source partitions must use the same immutable path policy.");
            var partitions = selection.CacheBasis == "snapshot" ? cacheIdentity.IncludedTopLevel : cacheIdentity.ExcludedTopLevel;
            if (!(partitions ?? []).Order(policy.Comparer).SequenceEqual(cacheRoots, policy.Comparer) ||
                selection.CacheBasis == "snapshot" && cacheIdentity.ExcludedTopLevel is { Length: > 0 })
                throw new WorkspaceException("ViewCacheMismatch", "The retained cache partition must match the selected view.");
            ISnapshotTree cacheTree = selection.CacheBasis switch
            {
                "snapshot" when cacheBasis is not null && cacheIdentity.Snapshot.Provider != "validation-empty-cache" =>
                    new CacheSnapshotTree(cacheBasis, cacheIdentity.Snapshot, cacheRoots),
                "empty" when cacheBasis is null && cacheIdentity.Snapshot.Provider == "validation-empty-cache" =>
                    new EmptyCacheTree(cacheIdentity.Snapshot),
                _ => throw new WorkspaceException("ViewCacheMismatch", "Select an explicit empty cache or supply the exact retained cache snapshot; never replace a missing base.")
            };
            cache = new JournalDeltaStore(selection.CacheDeltaPath, cacheIdentity, cacheTree, optimizeJournal: false);
            try
            {
                source = new JournalDeltaStore(sourceDirectory, identity with {
                    Snapshot = candidate.Snapshot, ExcludedTopLevel = identity.ExcludedTopLevel }, candidate);
                try
                {
                    if (File.Exists(transferPath + ".new"))
                    {
                        if (File.Exists(transferPath)) throw new WorkspaceException("ViewTransferRecoveryRequired", "Two transfer receipts remain; preserve both.");
                        ValidateTransfer(ReadTransfer(transferPath + ".new"));
                        AtomicFilePublication.Publish(transferPath + ".new", transferPath);
                    }
                    if (File.Exists(transferPath)) CompleteTransfer(ReadTransfer(transferPath));
                }
                catch { source.Dispose(); throw; }
            }
            catch { cache.Dispose(); throw; }
        }
        catch { candidate.Dispose(); throw; }
    }

    private bool Cached(string path) => cacheRoots.Contains(policy.Normalize(path).Split('/')[0], policy.Comparer);
    private void CheckTransfer()
    { if (transferIncomplete) throw new WorkspaceException("ViewTransferRecoveryRequired", "Reconnect this exact source generation to finish the preserved private-file transfer."); }
    private JournalDeltaStore ForPath(string path) { CheckTransfer(); return Cached(path) ? cache : source; }
    private JournalDeltaStore ForId(Guid id) { CheckTransfer(); return handles.TryGetValue(id, out var store) ? store : throw new FileNotFoundException("Unknown view handle."); }
    private DeltaEntry Remember(DeltaEntry entry, JournalDeltaStore owner)
    {
        if (handles.TryGetValue(entry.Id, out var known) && known != owner)
            throw new WorkspaceException("ViewHandleCollision", "The two partitions produced the same file identity.");
        handles[entry.Id] = owner; return entry;
    }
    public DeltaEntry? Stat(string path)
    { lock (gate) { var owner = ForPath(path); return owner.Stat(path) is { } entry ? Remember(entry, owner) : null; } }
    public DeltaEntry Get(Guid id) { lock (gate) return ForId(id).Get(id); }
    public IReadOnlyList<DeltaEntry> List(string directory)
    {
        lock (gate)
        {
            CheckTransfer();
            if (policy.Normalize(directory).Length == 0)
                return source.List("").Where(e => !Cached(e.Path)).Select(e => Remember(e, source))
                    .Concat(cache.List("").Where(e => Cached(e.Path)).Select(e => Remember(e, cache)))
                    .OrderBy(e => e.Path, policy.Comparer).ToArray();
            var owner = ForPath(directory);
            return owner.List(directory).Select(e => Remember(e, owner)).ToArray();
        }
    }
    public DeltaEntry Create(string path, bool directory, uint attributes)
    { lock (gate) { var owner = ForPath(path); return Remember(owner.Create(path, directory, attributes), owner); } }
    public int Read(Guid id, long offset, Span<byte> destination) { lock (gate) return ForId(id).Read(id, offset, destination); }
    public void Write(Guid id, long offset, ReadOnlySpan<byte> data) { lock (gate) ForId(id).Write(id, offset, data); }
    public void SetLength(Guid id, long length) { lock (gate) ForId(id).SetLength(id, length); }
    public void SetMetadata(Guid id, uint attributes, long creationTime, long accessTime, long writeTime)
    { lock (gate) ForId(id).SetMetadata(id, attributes, creationTime, accessTime, writeTime); }
    public void SetUnixMode(Guid id, uint? mode) { lock (gate) ForId(id).SetUnixMode(id, mode); }
    public void Rename(Guid id, string destination, bool replace)
    {
        lock (gate)
        {
            var owner = ForId(id);
            if (owner != ForPath(destination))
            {
                destination = policy.Normalize(destination);
                var privateFile = owner.ReadPrivateFile(id);
                var target = ForPath(destination);
                if (target.FindPrivateIdentity(id) is { Deleted: false })
                    throw new WorkspaceException("ViewTransferCollision", "This file identity was already used in the other partition.");
                var existing = target.Stat(destination);
                if (existing is not null && (!replace || existing.Directory)) throw new IOException("Rename destination exists.");
                int slash = destination.LastIndexOf('/');
                string parent = slash < 0 ? "" : destination[..slash];
                if (target.Stat(parent) is not { Directory: true }) throw new DirectoryNotFoundException(parent);
                string staging = (parent.Length == 0 ? "" : parent + "/") + ".splinter-transfer-" + id.ToString("N");
                if (target.Stat(staging) is not null) throw new WorkspaceException("ViewTransferCollision", "Preserve the existing staging path.");
                var intent = new TransferIntent(1, sourceDirectory, source.Identity, id, privateFile.Entry.Path,
                    destination, staging, owner == cache, replace, privateFile.Hash);
                transferIncomplete = true;
                using (var file = new FileStream(transferPath + ".new", FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { JsonSerializer.Serialize(file, intent); file.Flush(true); }
                transferBoundary?.Invoke("intent-flushed");
                AtomicFilePublication.Publish(transferPath + ".new", transferPath);
                transferBoundary?.Invoke("intent");
                CompleteTransfer(intent);
                handles[id] = target;
                return;
            }
            owner.Rename(id, destination, replace);
        }
    }
    public void Delete(Guid id) { lock (gate) ForId(id).Delete(id); }
    public void ReleaseUnlinkedFile(Guid id) { lock (gate) ForId(id).ReleaseUnlinkedFile(id); }
    public void Flush() { lock (gate) { source.Flush(); cache.Flush(); } }
    public SourceChangeView ObserveChanges(string[] managedPaths) => source.ObserveChanges(managedPaths) with
        { WorkspaceId = Identity.Id, SnapshotId = Identity.Snapshot.Id };
    public object ObservePartitions()
    { lock (gate) return new { candidate = Candidate.Id, candidateSeal = Candidate.Seal, transferPending = transferIncomplete,
        transferCount, transferredPrivateBytes, lastTransferFrom, lastTransferTo,
        source = source.ObserveActivity(), cache = cache.ObserveActivity() }; }
    public long GetJournalBytes() { lock (gate) return checked(source.GetJournalBytes() + cache.GetJournalBytes()); }
    public OverlayStorageObservation? ObserveActivity() => ObserveStorage(inspectFiles: false);
    public OverlayStorageObservation Observe() => ObserveStorage(inspectFiles: true)!;
    private OverlayStorageObservation? ObserveStorage(bool inspectFiles)
    {
        lock (gate)
        {
            var a = inspectFiles ? source.Observe() : source.ObserveActivity();
            var b = inspectFiles ? cache.Observe() : cache.ObserveActivity();
            if (a is null || b is null) return null;
            return new(Identity.Id, Identity.Snapshot.Id, 0, a.JournalBytes + b.JournalBytes,
                a.PayloadBytes + b.PayloadBytes, a.MetadataBytes + b.MetadataBytes, a.ReadBytes + b.ReadBytes,
                0, Identity.MaxTransferBytes, a.IndexedNodes + b.IndexedNodes, a.DurableSequence + b.DurableSequence)
                { JournalBytesBasis = inspectFiles ? "current-files" : "writer-accounting" };
        }
    }
    public void Dispose() { lock (gate) { source.Dispose(); cache.Dispose(); candidate.Dispose(); } }

    private static TransferIntent ReadTransfer(string path)
    {
        try { return JsonSerializer.Deserialize<TransferIntent>(File.ReadAllText(path)) ?? throw new JsonException(); }
        catch (JsonException) { throw new WorkspaceException("ViewTransferRecoveryRequired", "Preserve the unreadable transfer receipt and both journals."); }
    }

    private void ValidateTransfer(TransferIntent intent)
    {
        if (intent.Version != 1 || intent.Id == Guid.Empty || intent.Hash is not { Length: 64 } ||
            intent.SourceDirectory != sourceDirectory || JsonSerializer.Serialize(intent.SourceIdentity) != JsonSerializer.Serialize(source.Identity) ||
            Cached(intent.From) != intent.FromCache || Cached(intent.Destination) == intent.FromCache ||
            intent.Staging != (intent.Destination.Contains('/') ? intent.Destination[..(intent.Destination.LastIndexOf('/') + 1)] : "") + ".splinter-transfer-" + intent.Id.ToString("N"))
            throw new WorkspaceException("ViewTransferRecoveryRequired", "Reconnect the exact recorded generation; preserve both journals and receipt.");
    }

    private void CompleteTransfer(TransferIntent intent)
    {
        transferIncomplete = true;
        ValidateTransfer(intent);
        var from = intent.FromCache ? cache : source;
        var to = intent.FromCache ? source : cache;
        var data = from.ReadPrivateFile(intent.Id);
        if (data.Entry.Path != intent.From || data.Hash != intent.Hash)
            throw new WorkspaceException("ViewTransferRecoveryRequired", "Private source ranges changed during transfer; preserve both generations.");
        var existing = to.FindPrivateIdentity(intent.Id);
        if (existing is null || existing.Deleted)
        {
            if (data.Entry.Deleted) throw new WorkspaceException("ViewTransferRecoveryRequired", "A transfer has neither a source nor destination result.");
            to.CreatePrivateTransferTarget(data, intent.Staging);
            existing = to.Get(intent.Id);
        }
        if (existing.Path == intent.Staging && !existing.Deleted)
        {
            to.FillPrivateTransferTarget(data);
            if (to.ReadPrivateFile(intent.Id).Hash != intent.Hash)
                throw new WorkspaceException("ViewTransferRecoveryRequired", "Staged private ranges differ from their source.");
            transferBoundary?.Invoke("staged");
            to.Rename(intent.Id, intent.Destination, intent.Replace);
            transferBoundary?.Invoke("published");
        }
        else if (existing.Path != intent.Destination || existing.Deleted || to.ReadPrivateFile(intent.Id).Hash != intent.Hash)
            throw new WorkspaceException("ViewTransferRecoveryRequired", "The published transfer destination changed; preserve both journals.");
        if (!data.Entry.Deleted) from.Delete(intent.Id);
        transferBoundary?.Invoke("source-removed");
        File.Delete(transferPath);
        transferCount++; transferredPrivateBytes += data.Ranges.Sum(r => (long)r.Bytes.Length);
        lastTransferFrom = intent.From; lastTransferTo = intent.Destination;
        transferIncomplete = false;
    }

    // Cache begins empty, never seeds itself from a mutable or candidate Library.
    // Its base root is stable across candidate changes and contains no payload.
    private sealed class EmptyCacheTree(SnapshotReference identity) : ISnapshotTree
    {
        public SnapshotReference Snapshot => identity;
        public void EnsureAvailable() { }
        public SnapshotEntry? Stat(string path) => path.Length == 0 ? new("", true, 0, 16, 0, 0, 0) : null;
        public IReadOnlyList<SnapshotEntry> List(string path) { EnsureAvailable(); return []; }
        public int Read(string path, long offset, Span<byte> destination) => throw new FileNotFoundException(path);
    }
}
