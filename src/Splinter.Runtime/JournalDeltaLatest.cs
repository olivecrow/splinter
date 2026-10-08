using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class JournalDeltaStore
{
    internal const long LatestBlockStride = 2L * 1048576;
    private sealed record LatestBlock(long Id, string Hash);
    private sealed record LatestNode(DeltaEntry Entry, Extent[] Extents, LatestBlock[] Blocks, long ChangeSequence);
    private sealed record LatestBucket(string[] Hidden, SourcePathChange[] Changes);
    private LatestJournalStorage? latest;
    private FileStream? latestWriterLock;
    private FileStream? latestObjectsLock, latestReadBlock;
    private long latestReadBlockId;
    private void CloseLatestRead() { latestReadBlock?.Dispose(); latestReadBlock = null; latestReadBlockId = 0; }
    private readonly Dictionary<long, string> latestBlockHashes = [];
    private readonly Dictionary<int, HashSet<string>> latestHiddenBuckets = [];
    private readonly Dictionary<int, Dictionary<string, long>> latestChangeBuckets = [];
    private readonly HashSet<int> latestDirtyBuckets = [];
    private long CurrentJournalBytes => latest?.Bytes ?? journal.Length;
    public bool JournalOptimizationEnabled => latest is not null;
    public void ReleaseUnlinkedFile(Guid id)
    {
        using (EnterGate(mutation: true))
        {
            // A linked/unknown identity has no storage to reclaim. Keep local
            // writer health checks, without querying an unused immutable basis.
            CheckLocal();
            if (latest is null || !nodes.TryGetValue(id, out var node) || !node.Entry.Deleted) return;
            snapshot.EnsureAvailable();
            // Storage reclamation has no new source mutation or path sequence.
            // Keep the whiteout and last path change, but retire the closed ID.
            CommitLatest(new(sequence, "forget-unlinked", node.Entry), []);
        }
    }
    private static string LatestNodeName(Guid id) => $"node-{id:N}.json";
    private static string LatestBucketName(int id) => $"bucket-{id:x2}.json";
    private int LatestBucketId(string path) => SHA256.HashData(Encoding.UTF8.GetBytes(pathPolicy.CaseSensitive ? path : path.ToUpperInvariant()))[0];
    private void AddHidden(string path)
    {
        hidden.Add(path);
        if (latest is null) return;
        int bucket = LatestBucketId(path);
        if (!latestHiddenBuckets.TryGetValue(bucket, out var set)) latestHiddenBuckets[bucket] = set = new(pathPolicy.Comparer);
        set.Add(path); latestDirtyBuckets.Add(bucket);
    }
    private void RemoveHidden(string path)
    {
        hidden.Remove(path);
        if (latest is null) return;
        int bucket = LatestBucketId(path);
        latestHiddenBuckets.GetValueOrDefault(bucket)?.Remove(path); latestDirtyBuckets.Add(bucket);
    }
    private void SetPathChange(string path, long value)
    {
        pathChanges[path] = value;
        if (latest is null) return;
        int bucket = LatestBucketId(path);
        if (paths.ContainsKey(path))
        {
            if (latestChangeBuckets.GetValueOrDefault(bucket)?.Remove(path) == true) latestDirtyBuckets.Add(bucket);
            return;
        }
        if (!latestChangeBuckets.TryGetValue(bucket, out var changes)) latestChangeBuckets[bucket] = changes = new(pathPolicy.Comparer);
        changes[path] = value; latestDirtyBuckets.Add(bucket);
    }
    private void OpenLatest()
    {
        string path = journal.Name;
        journal.Dispose();
        string writerLock = path + ".writer.lock";
        WorkspaceStore.EnsurePlainPath(writerLock);
        latestWriterLock = new FileStream(writerLock, readOnly ? FileMode.Open : FileMode.OpenOrCreate, readOnly ? FileAccess.Read : FileAccess.ReadWrite,
            FileShare.Read, 1);
        // Delete sharing allows an atomic root replacement; the separate guard
        // owns the writer across the moment when the old root handle is closed.
        journal = new FileStream(path, FileMode.Open, readOnly ? FileAccess.Read : FileAccess.ReadWrite,
            FileShare.Read | FileShare.Delete, 4096, FileOptions.RandomAccess);
        var root = LatestJournalStorage.ReadRoot(path);
        string objects = Path.Combine(Path.GetDirectoryName(path)!, root.Objects);
        WorkspaceStore.EnsurePlainPath(objects);
        latestDirectoryGuard = directoryGuardFactory?.Invoke(objects);
        if (latestDirectoryGuard is not null && !WorkspaceStore.SamePath(latestDirectoryGuard.DirectoryPath, objects))
            throw new WorkspaceException("UnsafePath", "The journal directory proof belongs to another generation.");
        latestObjectsLock = new FileStream(Path.Combine(objects, "owner.lock"), FileMode.Open,
            readOnly ? FileAccess.Read : FileAccess.ReadWrite, FileShare.Read, 1);
        // Recovery may publish this root as a backup and truncate its retired
        // footer. Keep the writer/object guards, but not a read-only-sharing
        // root handle that would block our own exact recovery write.
        journal.Dispose();
        latest = new(path, Identity, readOnly, latestDirectoryGuard);
        latest.FlushObserver = ticks => { Interlocked.Increment(ref flushCount); Interlocked.Add(ref flushTicks, ticks); };
        foreach (string unsafePath in latest.Current.ProjectionUnsafe)
        {
            if (Identity.ExcludedTopLevel?.Contains(unsafePath, pathPolicy.Comparer) != true || !projectionUnsafe.Add(unsafePath))
                throw LatestJournalStorage.Corrupt("A projection guard differs from the overlay identity.");
        }
        // Only reopen the current root after publication/reclamation completes.
        journal = new FileStream(path, FileMode.Open, readOnly ? FileAccess.Read : FileAccess.ReadWrite,
            FileShare.Read | FileShare.Delete, 4096, FileOptions.RandomAccess);
        var blockOwners = new Dictionary<long, Guid>();
        foreach (string name in latest.Names().Order(StringComparer.Ordinal))
        {
            if (name.StartsWith("node-", StringComparison.Ordinal))
            {
                var record = ReadLatestNode(name);
                if (record.Entry is null || record.Extents is null || record.Blocks is null || name != LatestNodeName(record.Entry.Id))
                    throw LatestJournalStorage.Corrupt("A current node record has invalid identity.");
                _ = DecodeMutation(JsonSerializer.SerializeToUtf8Bytes(new Mutation(0, "metadata", record.Entry)), 0);
                if (record.ChangeSequence < 0 || record.ChangeSequence > latest.Current.Sequence || record.Entry.Deleted && record.ChangeSequence != 0)
                    throw LatestJournalStorage.Corrupt("A node change sequence is invalid.");
                if (record.ChangeSequence != 0 && !pathChanges.TryAdd(record.Entry.Path, record.ChangeSequence))
                    throw LatestJournalStorage.Corrupt("A node change sequence is duplicated.");
                RequireAllowed(record.Entry.Path);
                if (record.Entry.BasePath is { } basis) RequireAllowed(basis);
                var node = new Node(record.Entry);
                long end = 0;
                foreach (var extent in record.Extents)
                {
                    extent.Payload.RequireValid(Identity.MaxTransferBytes);
                    long id = extent.JournalOffset / LatestBlockStride;
                    if (extent.Length <= 0 || extent.Start < end || extent.Start > record.Entry.Length - extent.Length ||
                        extent.JournalOffset % LatestBlockStride != 0 || id < 1 || id >= latest.Current.NextBlock ||
                        extent.BlockOffset < 0 || extent.BlockOffset > extent.Payload.DecodedBytes - extent.Length ||
                        record.Entry.Directory) throw LatestJournalStorage.Corrupt("Current private ranges are invalid.");
                    end = extent.Start + extent.Length; node.Extents.Add(extent);
                    if (blockOwners.TryGetValue(id, out Guid owner) && owner != record.Entry.Id)
                        throw LatestJournalStorage.Corrupt("A private block has more than one node owner.");
                    blockOwners[id] = record.Entry.Id;
                }
                if (record.Blocks.Select(b => b.Id).Distinct().Count() != record.Blocks.Length ||
                    !record.Blocks.Select(b => b.Id).Order().SequenceEqual(node.Extents.Select(e => e.JournalOffset / LatestBlockStride).Distinct().Order()))
                    throw LatestJournalStorage.Corrupt("Current block references differ from their node ranges.");
                foreach (var block in record.Blocks)
                {
                    var pieces = node.Extents.Where(e => e.JournalOffset / LatestBlockStride == block.Id).OrderBy(e => e.BlockOffset).ToArray();
                    var descriptor = pieces[0].Payload;
                    int blockEnd = 0;
                    foreach (var piece in pieces)
                    {
                        if (piece.Payload != descriptor || piece.BlockOffset != blockEnd) throw LatestJournalStorage.Corrupt("Current block slices overlap or retain superseded bytes.");
                        blockEnd += piece.Length;
                    }
                    if (blockEnd != descriptor.DecodedBytes) throw LatestJournalStorage.Corrupt("A current block retains unused payload.");
                    if (latest.FileLength(LatestJournalStorage.BlockName(block.Id)) != descriptor.StoredBytes + LatestJournalStorage.BlockHeaderBytes ||
                        latest.FileHash(LatestJournalStorage.BlockName(block.Id)) != block.Hash || latestPageHashes.ContainsKey(block.Id) ||
                        !latestBlockHashes.TryAdd(block.Id, block.Hash))
                        throw LatestJournalStorage.Corrupt("A current private payload is missing or damaged.");
                    latest.ValidateBlock(block.Id, descriptor.StoredBytes);
                    replayBytes += descriptor.StoredBytes;
                }
                if (nodes.TryGetValue(record.Entry.Id, out var prior)) RemovePath(prior.Entry.Path);
                if (!nodes.ContainsKey(record.Entry.Id)) RequireNodeCapacity();
                nodes[record.Entry.Id] = node; extentCount += node.Extents.Count;
                if (extentCount > MaxIndexedExtents) throw LatestJournalStorage.Corrupt("The current range index exceeds its bound.");
                if (!record.Entry.Deleted)
                {
                    if (paths.TryGetValue(record.Entry.Path, out var existing) && existing != record.Entry.Id)
                        throw LatestJournalStorage.Corrupt("Two current nodes have the same path.");
                    IndexPath(record.Entry.Path, record.Entry.Id);
                }
            }
            else if (name.StartsWith("bucket-", StringComparison.Ordinal))
            {
                var bucket = LatestJournalStorage.Decode<LatestBucket>(latest.Read(name), "SJK1", int.MaxValue);
                if (bucket.Hidden is null || bucket.Changes is null) throw LatestJournalStorage.Corrupt("Current path indexes are missing.");
                foreach (string hiddenPath in bucket.Hidden)
                {
                    if (NormalizePath(hiddenPath) != hiddenPath || name != LatestBucketName(LatestBucketId(hiddenPath)) || hidden.Contains(hiddenPath))
                        throw LatestJournalStorage.Corrupt("A current whiteout is invalid.");
                    RequireAllowed(hiddenPath); AddHidden(hiddenPath);
                }
                foreach (var change in bucket.Changes)
                {
                    if (NormalizePath(change.Path) != change.Path || name != LatestBucketName(LatestBucketId(change.Path)) ||
                        change.Sequence < 1 || change.Sequence > latest.Current.Sequence || pathChanges.ContainsKey(change.Path))
                        throw LatestJournalStorage.Corrupt("A current change sequence is invalid.");
                    RequireAllowed(change.Path); SetPathChange(change.Path, change.Sequence);
                }
            }
        }
        if (hidden.Count > MaxHiddenPaths || pathChanges.Count > MaxHiddenPaths ||
            latest.Names().Count(n => n.StartsWith("block-", StringComparison.Ordinal)) != latestBlockHashes.Count ||
            latest.Names().Count(n => n.StartsWith("page-", StringComparison.Ordinal)) != latestPageHashes.Count)
            throw LatestJournalStorage.Corrupt("The current indexes or payload inventory differ.");
        sequence = latest.Current.Sequence;
        payloadBytes = latest.Current.PayloadBytes; logicalPayloadBytes = latest.Current.LogicalBytes;
        long stored = 0, decoded = 0;
        foreach (var node in nodes.Values)
        {
            foreach (var block in node.Extents.GroupBy(e => e.JournalOffset))
            {
                var descriptor = block.First().Payload;
                if (block.Any(e => e.Payload != descriptor)) throw LatestJournalStorage.Corrupt("A private block has inconsistent descriptors.");
                // Validate the entire encoded stream before any bytes are served.
                if (descriptor.Encoded) VisitPayload(block.Key, descriptor, (_, _, _) => { });
                stored += descriptor.StoredBytes; decoded += descriptor.DecodedBytes;
            }
        }
        if (stored != payloadBytes || decoded != logicalPayloadBytes || CurrentJournalBytes > Identity.MaxJournalBytes)
            throw LatestJournalStorage.Corrupt("Current journal payload accounting differs.");
        latestDirtyBuckets.Clear();
    }
    private void ReadLatestExactly(long offset, Span<byte> output)
    {
        long id = offset / LatestBlockStride;
        long position = offset % LatestBlockStride;
        if (!latestBlockHashes.ContainsKey(id) || position > Identity.MaxTransferBytes - output.Length)
            throw LatestJournalStorage.Corrupt("A private read is outside its current block.");
        if (latestReadBlockId != id)
        { CloseLatestRead(); latestReadBlock = latest!.OpenBlock(id); latestReadBlockId = id; }
        position += latest!.BlockOffset(id);
        var input = latestReadBlock!;
        while (!output.IsEmpty)
        {
            int count = RandomAccess.Read(input.SafeFileHandle, output, position);
            if (count == 0) throw LatestJournalStorage.Corrupt("A current private read is short.");
            journalReadBytes += count;
            if (replaying) replayBytes += count;
            position += count; output = output[count..];
        }
    }
    private LatestBucket BucketRecord(int bucket) => new(
        latestHiddenBuckets.GetValueOrDefault(bucket)?.Order(pathPolicy.Comparer).ToArray() ?? [],
        latestChangeBuckets.GetValueOrDefault(bucket)?.OrderBy(p => p.Key, pathPolicy.Comparer)
            .Select(p => new SourcePathChange(p.Key, p.Value)).ToArray() ?? []);
    private void CommitLatest(Mutation mutation, ReadOnlySpan<byte> payload)
    {
        if (!liveCommitOperations.TryGetValue(mutation.Operation, out var cost))
            liveCommitOperations[mutation.Operation] = cost = new();
        long started = Stopwatch.GetTimestamp(); cost.Attempts++;
        bool applied = false;
        try
        {
            if (readOnly) throw new WorkspaceException("CandidateReadOnly", "A published candidate cannot be edited.");
            RequireMutationCapacity(mutation);
            var affected = new HashSet<Guid> { mutation.Entry.Id };
            if (mutation.Operation == "rename" && nodes.TryGetValue(mutation.Entry.Id, out var moved))
            {
                foreach (var node in IndexedSubtree(moved)) affected.Add(node.Entry.Id);
                if (paths.TryGetValue(mutation.Destination!, out Guid replaced)) affected.Add(replaced);
            }
            var beforeBlocks = affected.Where(nodes.ContainsKey).SelectMany(id => nodes[id].Extents)
                .GroupBy(e => e.JournalOffset).ToDictionary(g => g.Key, g => g.First().Payload);
            var previousPaths = affected.Where(nodes.ContainsKey).Select(id => nodes[id].Entry.Path).ToArray();
            var puts = new Dictionary<string, LatestJournalStorage.Put>(StringComparer.Ordinal);
            var pageRemoves = new List<LatestJournalStorage.Remove>();
            long next = latest!.Current.NextBlock;
            (long Address, WorkspaceJournalPayload Payload) AddBlock(ReadOnlySpan<byte> data)
            {
                long id = next++;
                if (next >= long.MaxValue / LatestBlockStride) throw new WorkspaceException("DeltaFull", "The bounded block address space is exhausted.");
                byte[]? encoded = EncodePayload(data);
                ReadOnlySpan<byte> bytes = encoded is null ? data : encoded;
                var descriptor = new WorkspaceJournalPayload(bytes.Length, data.Length);
                string name = LatestJournalStorage.BlockName(id);
                if (latest.FileHash(name) is not null) throw LatestJournalStorage.Corrupt("A new private block already exists.");
                byte[] fileBytes = LatestJournalStorage.EncodeBlock(id, latest.Current.IdentityHash, bytes);
                puts.Add(name, new(name, null, fileBytes)); latestBlockHashes.Add(id, LatestJournalStorage.Hash(fileBytes));
                return (id * LatestBlockStride, descriptor);
            }
            var added = payload.IsEmpty ? (Address: 0L, Payload: new WorkspaceJournalPayload(0, 0)) : AddBlock(payload);
            applied = true;
            if (mutation.Operation == "forget-unlinked")
            { extentCount -= nodes[mutation.Entry.Id].Extents.Count; nodes.Remove(mutation.Entry.Id); }
            else Apply(mutation, added.Address, added.Payload);
            foreach (string previous in previousPaths)
                if (!paths.ContainsKey(previous) && pathChanges.TryGetValue(previous, out long changed)) SetPathChange(previous, changed);
            foreach (Guid id in affected)
                if (nodes.TryGetValue(id, out var current) && !current.Entry.Deleted && pathChanges.TryGetValue(current.Entry.Path, out long changed))
                    SetPathChange(current.Entry.Path, changed);
            // Repack only the remaining private slices of a partially replaced
            // old block. Never read the source or rewrite unrelated file ranges.
            foreach (Guid id in affected)
            {
                if (!nodes.TryGetValue(id, out var node)) continue;
                foreach (var block in node.Extents.Where(e => beforeBlocks.ContainsKey(e.JournalOffset)).GroupBy(e => e.JournalOffset).ToArray())
                {
                    var extents = block.ToArray();
                    if (extents.Sum(e => e.Length) == extents[0].Payload.DecodedBytes) continue;
                    var newSlices = node.Extents.Where(e => added.Address != 0 && e.JournalOffset == added.Address).ToArray();
                    bool combine = newSlices.Length != 0 && extents.Sum(e => e.Length) + newSlices.Sum(e => e.Length) <= Identity.MaxTransferBytes;
                    if (combine) extents = extents.Concat(newSlices).OrderBy(e => e.Start).ToArray();
                    byte[] remaining = new byte[extents.Sum(e => e.Length)]; int cursor = 0;
                    foreach (var extent in extents)
                    {
                        if (extent.JournalOffset == added.Address) payload.Slice(extent.BlockOffset, extent.Length).CopyTo(remaining.AsSpan(cursor));
                        else ReadExtent(extent, 0, remaining.AsSpan(cursor, extent.Length));
                        cursor += extent.Length;
                    }
                    var packed = AddBlock(remaining); cursor = 0;
                    foreach (var extent in extents)
                    {
                        int index = node.Extents.IndexOf(extent);
                        node.Extents[index] = extent with { JournalOffset = packed.Address, Payload = packed.Payload, BlockOffset = cursor };
                        cursor += extent.Length;
                    }
                    if (combine)
                    {
                        puts.Remove(LatestJournalStorage.BlockName(added.Address / LatestBlockStride));
                        latestBlockHashes.Remove(added.Address / LatestBlockStride);
                    }
                }
                int priorExtents = node.Extents.Count;
                var merged = new List<Extent>(priorExtents);
                foreach (var extent in node.Extents)
                {
                    if (merged.Count != 0 && merged[^1] is { } prior && prior.JournalOffset == extent.JournalOffset &&
                        prior.Start + prior.Length == extent.Start && prior.BlockOffset + prior.Length == extent.BlockOffset)
                        merged[^1] = prior with { Length = prior.Length + extent.Length };
                    else merged.Add(extent);
                }
                node.Extents = merged; extentCount += merged.Count - priorExtents;
                string name = LatestNodeName(id);
                byte[] record = WriteLatestNode(node, ref next, puts, pageRemoves);
                puts[name] = new(name, latest.FileHash(name), record);
            }
            foreach (int bucket in latestDirtyBuckets)
            {
                string name = LatestBucketName(bucket);
                puts[name] = new(name, latest.FileHash(name), LatestJournalStorage.Encode(BucketRecord(bucket), "SJK1"));
            }
            var retained = affected.Where(nodes.ContainsKey).SelectMany(id => nodes[id].Extents).Select(e => e.JournalOffset).ToHashSet();
            var removed = beforeBlocks.Where(p => !retained.Contains(p.Key)).ToArray();
            var removes = removed.Select(p => new LatestJournalStorage.Remove(LatestJournalStorage.BlockName(p.Key / LatestBlockStride),
                latestBlockHashes[p.Key / LatestBlockStride])).ToList();
            removes.AddRange(pageRemoves);
            if (mutation.Operation == "forget-unlinked")
            {
                string name = LatestNodeName(mutation.Entry.Id);
                removes.Add(new(name, latest.FileHash(name) ?? throw LatestJournalStorage.Corrupt("A closed node record disappeared.")));
                RetireLatestPages(mutation.Entry.Id, removes);
            }
            long fileBytes = latest.Current.FileBytes, stored = latest.Current.PayloadBytes, decoded = latest.Current.LogicalBytes;
            int fileCount = latest.Current.FileCount;
            foreach (var put in puts.Values)
            {
                fileBytes += put.Bytes.Length - (put.BeforeHash is null ? 0 : latest.FileLength(put.Name));
                if (put.BeforeHash is null) fileCount++;
            }
            foreach (var remove in removes) { fileBytes -= latest.FileLength(remove.Name); fileCount--; }
            foreach (var p in removed) { stored -= p.Value.StoredBytes; decoded -= p.Value.DecodedBytes; }
            foreach (var block in puts.Values.Where(p => p.Name.StartsWith("block-", StringComparison.Ordinal)))
            {
                long id = Convert.ToInt64(block.Name[6..22], 16);
                if (latestPageHashes.ContainsKey(id)) continue;
                stored += block.Bytes.Length - LatestJournalStorage.BlockHeaderBytes;
                decoded += affected.Where(nodes.ContainsKey).SelectMany(n => nodes[n].Extents)
                    .First(e => e.JournalOffset / LatestBlockStride == id).Payload.DecodedBytes;
            }
            var root = latest.Current with { Sequence = sequence, NextBlock = next, FileBytes = fileBytes, FileCount = fileCount,
                PayloadBytes = stored, LogicalBytes = decoded, ProjectionUnsafe = projectionUnsafe.Order(pathPolicy.Comparer).ToArray() };
            if (fileBytes + LatestJournalStorage.Encode(root, "SJL4").Length > Identity.MaxJournalBytes)
                throw new WorkspaceException("DeltaFull", "The current delta exceeds the journal budget; preserve it and reconnect.");
            string path = journal.Name;
            long beforeFlushTicks = flushTicks, beforeFlushCount = flushCount; int requestedBytes = payload.Length;
            CloseLatestRead();
            journal.Dispose();
            try
            {
                latest.Commit(root, [.. puts.Values], [.. removes], boundary => Visit(boundary, mutation.Operation, mutation.Sequence,
                    0, fileBytes, requestedBytes));
            }
            finally { cost.FlushTicks += flushTicks - beforeFlushTicks; cost.FlushCalls += flushCount - beforeFlushCount; }
            journal.Dispose();
            journal = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read | FileShare.Delete, 4096, FileOptions.RandomAccess);
            expectedJournalBytes = journal.Length;
            foreach (var remove in removed) latestBlockHashes.Remove(remove.Key / LatestBlockStride);
            payloadBytes = stored; logicalPayloadBytes = decoded; latestDirtyBuckets.Clear();
            commitCount++; appendedBytes += puts.Values.Sum(p => (long)p.Bytes.Length);
            cost.DurableCommits++; cost.PayloadBytes += payload.Length; cost.JournalBytes += puts.Values.Sum(p => (long)p.Bytes.Length);
            Visit("latest.after-ack-ready", mutation.Operation, sequence, 0, latest.CommittedBytes, payload.Length);
        }
        catch { if (applied) poisoned = true; cost.FailedCalls++; throw; }
        finally { cost.TotalTicks += Stopwatch.GetTimestamp() - started; }
    }
    private long WriteLatestCandidate(string path, string attempt, List<LiveRange> live, bool write)
    {
        string objects = "latest-" + attempt;
        string directory = Path.Combine(Path.GetDirectoryName(path)!, objects);
        var hashes = new Dictionary<long, string>();
        long fileBytes = 0, storedBytes = 0, decodedBytes = 0, next = 1; int fileCount = 0;
        void FileObject(string name, byte[] bytes)
        {
            fileBytes += bytes.Length; fileCount++;
            if (!write) return;
            using var file = new FileStream(Path.Combine(directory, name), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            file.Write(bytes); file.Flush(true);
        }
        if (write)
        {
            WorkspaceStore.EnsurePlainPath(directory);
            if (Directory.Exists(directory))
            {
                // The exact unpublished attempt is owned by the verified
                // compaction intent. No other directory or generation is swept.
                foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
                {
                    WorkspaceStore.EnsurePlainPath(entry);
                    string name = Path.GetFileName(entry);
                    if (!File.Exists(entry) || !((name.StartsWith("node-", StringComparison.Ordinal) ||
                        name.StartsWith("bucket-", StringComparison.Ordinal)) && name.EndsWith(".json", StringComparison.Ordinal) ||
                        (name.StartsWith("block-", StringComparison.Ordinal) || name.StartsWith("page-", StringComparison.Ordinal)) && name.EndsWith(".bin", StringComparison.Ordinal) ||
                        name == "owner.lock" && new FileInfo(entry).Length == 0))
                        throw LatestJournalStorage.Corrupt("An unpublished candidate contains an unowned entry.");
                }
                foreach (string entry in Directory.EnumerateFiles(directory)) File.Delete(entry);
            }
            else Directory.CreateDirectory(directory);
        }
        var byNode = live.GroupBy(r => r.NodeId).ToDictionary(g => g.Key, g => g.OrderBy(r => r.Logical).ToArray());
        foreach (var node in nodes.Values.OrderBy(n => n.Entry.Id))
        {
            var extents = new List<Extent>();
            foreach (var block in (byNode.GetValueOrDefault(node.Entry.Id) ?? []).GroupBy(r => r.Physical))
            {
                var ranges = block.ToArray();
                byte[] data = new byte[ranges.Sum(r => r.Length)]; int cursor = 0;
                foreach (var range in ranges)
                { ReadPayload(range.Physical, range.Payload, range.BlockOffset, data.AsSpan(cursor, range.Length)); cursor += range.Length; }
                byte[]? encoded = EncodePayload(data); byte[] bytes = encoded ?? data;
                var payload = new WorkspaceJournalPayload(bytes.Length, data.Length);
                long id = next++;
                byte[] blockBytes = LatestJournalStorage.EncodeBlock(id, LatestJournalStorage.Hash(JsonSerializer.SerializeToUtf8Bytes(Identity)), bytes);
                hashes.Add(id, LatestJournalStorage.Hash(blockBytes));
                FileObject(LatestJournalStorage.BlockName(id), blockBytes);
                storedBytes += bytes.Length; decodedBytes += data.Length; cursor = 0;
                foreach (var range in ranges)
                { extents.Add(new(range.Logical, range.Length, id * LatestBlockStride, payload, cursor)); cursor += range.Length; }
            }
            extents.Sort((a, b) => a.Start.CompareTo(b.Start));
            var pages = new List<LatestPageReference>();
            int paged = extents.Count / LatestPageExtents * LatestPageExtents;
            for (int start = 0; start < paged; start += LatestPageExtents)
            {
                var page = MakeLatestPage(node.Entry.Id, extents.GetRange(start, LatestPageExtents).ToArray(), hashes);
                long id = next++;
                byte[] bytes = LatestJournalStorage.EncodePage(id,
                    LatestJournalStorage.Hash(JsonSerializer.SerializeToUtf8Bytes(Identity)), LatestJournalStorage.Encode(page, "SJP2"));
                FileObject(LatestJournalStorage.PageName(id), bytes);
                pages.Add(new(id, LatestJournalStorage.Hash(bytes)));
            }
            var tail = extents.Skip(paged).ToArray();
            var record = new LatestPagedNode(node.Entry, [.. pages], tail, LatestBlocks(tail, hashes),
                node.Entry.Deleted ? 0 : pathChanges.GetValueOrDefault(node.Entry.Path));
            FileObject(LatestNodeName(node.Entry.Id), LatestJournalStorage.Encode(record, "SJN3"));
        }
        var hiddenBuckets = hidden.GroupBy(LatestBucketId).ToDictionary(g => g.Key, g => g.Order(pathPolicy.Comparer).ToArray());
        var changeBuckets = pathChanges.Where(p => !paths.ContainsKey(p.Key)).GroupBy(p => LatestBucketId(p.Key)).ToDictionary(g => g.Key,
            g => g.OrderBy(p => p.Key, pathPolicy.Comparer).Select(p => new SourcePathChange(p.Key, p.Value)).ToArray());
        foreach (int bucket in hiddenBuckets.Keys.Concat(changeBuckets.Keys).Distinct().Order())
            FileObject(LatestBucketName(bucket), LatestJournalStorage.Encode(new LatestBucket(
                hiddenBuckets.GetValueOrDefault(bucket) ?? [], changeBuckets.GetValueOrDefault(bucket) ?? []), "SJK1"));
        FileObject("owner.lock", []);
        var root = new LatestJournalStorage.Root(4, 3, LatestJournalStorage.Hash(JsonSerializer.SerializeToUtf8Bytes(Identity)), objects,
            sequence, next, fileBytes, fileCount, storedBytes, decodedBytes, projectionUnsafe.Order(pathPolicy.Comparer).ToArray());
        byte[] rootBytes = LatestJournalStorage.Encode(root, "SJL4");
        if (write)
        {
            using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            file.Write(rootBytes); file.Flush(true);
            using var writerLock = new FileStream(path + ".writer.lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
            // An ON source already owns this guard throughout compaction. Do
            // not open a second writer against our own exclusive handle.
            if (latestWriterLock is null)
            {
                using var currentLock = new FileStream(Path.Combine(Path.GetDirectoryName(path)!, "delta.journal.writer.lock"),
                    FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
            }
        }
        return fileBytes + rootBytes.Length;
    }
    public static SnapshotDeltaSeal SealJournal(string path, long durableSequence, Func<string, IJournalDirectoryGuard>? directoryGuardFactory = null)
    {
        if (LatestJournalStorage.IsLatest(path))
        {
            var seal = LatestJournalStorage.Seal(path, directoryGuardFactory);
            if (seal.DurableSequence != durableSequence) throw new WorkspaceException("DeltaChangedAfterCollection", "Current journal sequence differs from its flush receipt.");
            return seal;
        }
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        long bytes = file.Length;
        string hash = Convert.ToHexStringLower(SHA256.HashData(file));
        if (bytes != file.Length) throw new WorkspaceException("DeltaChangedAfterCollection", "Journal grew during its seal.");
        return new(durableSequence, bytes, hash);
    }
    public static IReadOnlyList<string> JournalStorageFiles(string path) => LatestJournalStorage.Files(path);
}
