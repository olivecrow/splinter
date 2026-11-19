using System.Buffers;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using Splinter.Core;

namespace Splinter.Runtime;

// High-speed local range/file overlay for workspace-private generated/ephemeral
// subtrees (e.g., Unity's Library, Temp, Logs, UserSettings, obj). Unchanged
// basis bytes are read directly from the immutable basis without copying or
// hydration; modified basis files store only changed ranges; newly created
// ephemeral files use native scratch files without per-write fsync or treaps.
public sealed class EphemeralRangeStore : IDisposable
{
    private readonly record struct RangeExtent(long Start, int Length, long DataOffset);

    private sealed class Node(DeltaEntry entry, bool isPrivateFile)
    {
        public DeltaEntry Entry = entry;
        public bool IsPrivateFile = isPrivateFile;
        public List<RangeExtent>? Extents;
    }

    private sealed class PooledHandle(Guid id, SafeFileHandle handle)
    {
        public readonly Guid Id = id;
        public readonly SafeFileHandle Handle = handle;
        public int ActiveRefs;
        public bool Evicted;
        public LinkedListNode<PooledHandle>? LruNode;
    }

    private const int MaxPooledHandles = 256;
    private static ReadOnlySpan<byte> IndexMagic => "SEP1"u8;

    private readonly string scratchDirectory;
    private readonly string filesDirectory;
    private readonly string rangesPath;
    private readonly string indexPath;
    private readonly OverlayIdentity identity;
    private readonly ISnapshotTree? generatedBasis;
    private readonly string? graftRoot;
    private readonly string[] patterns;
    private readonly WorkspacePathPolicy policy;
    private readonly ReaderWriterLockSlim rw = new(LockRecursionPolicy.SupportsRecursion);

    // Modified or created entries only; read-only basis Stat calls never mutate these.
    private readonly Dictionary<Guid, Node> nodes = [];
    private readonly Dictionary<string, Guid> paths;
    private readonly Dictionary<string, HashSet<string>> modifiedChildren;
    private readonly HashSet<string> hidden;

    // Lightweight cache for currently-open read-only basis handles (no write lock required).
    private readonly ConcurrentDictionary<Guid, DeltaEntry> baseHandleCache = new();

    private readonly object rangesWriteGate = new();
    private SafeFileHandle? rangesHandle;
    private long rangesLength;

    private readonly object poolGate = new();
    private readonly Dictionary<Guid, PooledHandle> handlePool = [];
    private readonly LinkedList<PooledHandle> handleLru = [];

    private long readBytes;
    private long writeBytes;
    private long writeCount;
    private int activeRangeReads;
    private volatile bool disposed;

    public long ReadBytes => Interlocked.Read(ref readBytes);
    public long WriteBytes => Interlocked.Read(ref writeBytes);
    public long WriteCount => Interlocked.Read(ref writeCount);
    public long RangesFileLength { get { lock (rangesWriteGate) return rangesLength; } }
    public string ScratchDirectory => scratchDirectory;

    public EphemeralRangeStore(string scratchDirectory, OverlayIdentity identity, ISnapshotTree? generatedBasis,
        string? graftRoot, string[] patterns)
    {
        this.scratchDirectory = Path.GetFullPath(scratchDirectory);
        filesDirectory = Path.Combine(this.scratchDirectory, "files");
        rangesPath = Path.Combine(this.scratchDirectory, "ranges.dat");
        indexPath = Path.Combine(this.scratchDirectory, "index.bin");
        this.identity = identity;
        this.generatedBasis = generatedBasis;
        policy = WorkspacePathPolicy.For(identity.Snapshot);
        this.graftRoot = string.IsNullOrEmpty(graftRoot) ? null : policy.Normalize(graftRoot);
        this.patterns = patterns;
        paths = new(policy.Comparer);
        modifiedChildren = new(policy.Comparer);
        hidden = new(policy.Comparer);

        Directory.CreateDirectory(this.scratchDirectory);
        if (!TryLoadIndex())
        {
            RequireResetAllowed();
            ResetScratchDirectory();
        }
        Directory.CreateDirectory(filesDirectory);
        rangesHandle = File.OpenHandle(rangesPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, FileOptions.RandomAccess);
        long actualLength = RandomAccess.GetLength(rangesHandle);
        if (rangesLength > actualLength)
        {
            // Index referenced ranges beyond file length (incomplete crash state); reset cleanly.
            rangesHandle.Dispose();
            rangesHandle = null;
            RequireResetAllowed();
            ResetScratchDirectory();
            Directory.CreateDirectory(filesDirectory);
            rangesHandle = File.OpenHandle(rangesPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, FileOptions.RandomAccess);
        }
        else if (rangesLength < actualLength)
        {
            RandomAccess.SetLength(rangesHandle, rangesLength);
        }
    }

    public string Normalize(string rawPath) => policy.Normalize(rawPath);

    public bool MatchesPath(string normalizedPath)
    {
        if (normalizedPath.Length == 0) return false;
        int slash = normalizedPath.IndexOf('/');
        string top = slash < 0 ? normalizedPath : normalizedPath[..slash];
        foreach (string pattern in patterns)
        {
            if (pattern.Length == 0) continue;
            if (pattern[0] == '*')
            {
                if (slash < 0 && normalizedPath.EndsWith(pattern[1..], StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            else if (top.Equals(pattern, policy.Comparison))
            {
                return true;
            }
        }
        return false;
    }

    public bool OwnsId(Guid id)
    {
        if (baseHandleCache.ContainsKey(id)) return true;
        rw.EnterReadLock();
        try { return nodes.ContainsKey(id); }
        finally { rw.ExitReadLock(); }
    }

    public void RetainBasisEntry(DeltaEntry entry)
    {
        if (entry.BasePath is null) return;
        rw.EnterReadLock();
        try
        {
            if (!nodes.ContainsKey(entry.Id))
                baseHandleCache[entry.Id] = entry;
        }
        finally { rw.ExitReadLock(); }
    }

    public DeltaEntry? Stat(string normalizedPath, bool retainForHandle = false)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        rw.EnterReadLock();
        try
        {
            if (paths.TryGetValue(normalizedPath, out var existingId))
                return nodes[existingId].Entry;
            if (hidden.Contains(normalizedPath)) return null;
            var basisEntry = ResolveBasisLocked(normalizedPath);
            if (basisEntry is null) return null;
            if (retainForHandle) baseHandleCache[basisEntry.Id] = basisEntry;
            return basisEntry;
        }
        finally { rw.ExitReadLock(); }
    }

    public DeltaEntry Get(Guid id)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        rw.EnterReadLock();
        try
        {
            if (nodes.TryGetValue(id, out var node)) return node.Entry;
        }
        finally { rw.ExitReadLock(); }
        if (baseHandleCache.TryGetValue(id, out var cached)) return cached;
        throw new FileNotFoundException("Unknown ephemeral open file identity.");
    }

    public IReadOnlyList<DeltaEntry> List(string normalizedDirectory) => EnumerateChildren(normalizedDirectory).ToArray();

    public IEnumerable<DeltaEntry> EnumerateChildren(string normalizedDirectory)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (normalizedDirectory.Length == 0)
        {
            return EnumerateTopLevel();
        }

        string? basisDirectory = null;
        Dictionary<string, DeltaEntry> merged = new(policy.Comparer);
        HashSet<string>? hiddenSnapshot = null;

        rw.EnterReadLock();
        try
        {
            DeltaEntry? dirEntry = null;
            if (paths.TryGetValue(normalizedDirectory, out var dirId))
                dirEntry = nodes[dirId].Entry;
            else if (!hidden.Contains(normalizedDirectory))
                dirEntry = ResolveBasisLocked(normalizedDirectory);

            if (dirEntry is null) throw new DirectoryNotFoundException(normalizedDirectory);
            if (!dirEntry.Directory) throw new IOException("Not a directory.");

            basisDirectory = dirEntry.BasePath;
            if (modifiedChildren.TryGetValue(normalizedDirectory, out var children))
            {
                foreach (string childPath in children)
                {
                    if (paths.TryGetValue(childPath, out var childId))
                        merged[Name(childPath)] = nodes[childId].Entry;
                }
            }
            if (basisDirectory is not null && hidden.Count > 0)
            {
                string prefix = normalizedDirectory + "/";
                foreach (string hiddenPath in hidden)
                {
                    if (hiddenPath.StartsWith(prefix, policy.Comparison) && hiddenPath.IndexOf('/', prefix.Length) < 0)
                        (hiddenSnapshot ??= new(policy.Comparer)).Add(hiddenPath[prefix.Length..]);
                }
            }
        }
        finally { rw.ExitReadLock(); }

        if (basisDirectory is not null && generatedBasis is not null)
        {
            foreach (var item in generatedBasis.EnumerateChildren(basisDirectory))
            {
                string name = Name(item.Path);
                if (merged.ContainsKey(name)) continue;
                if (hiddenSnapshot is not null && hiddenSnapshot.Contains(name)) continue;
                string fullPath = Join(normalizedDirectory, name);
                merged[name] = ToDeltaEntry(item, fullPath);
            }
        }

        var result = merged.Values.ToArray();
        Array.Sort(result, (a, b) => policy.Comparer.Compare(a.Path, b.Path));
        return result;
    }

    public IEnumerable<DeltaEntry> EnumerateTopLevel()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var merged = new Dictionary<string, DeltaEntry>(policy.Comparer);
        bool graftHidden = false;

        rw.EnterReadLock();
        try
        {
            if (modifiedChildren.TryGetValue("", out var topChildren))
            {
                foreach (string childPath in topChildren)
                {
                    if (paths.TryGetValue(childPath, out var childId))
                        merged[childPath] = nodes[childId].Entry;
                }
            }
            graftHidden = graftRoot is not null && hidden.Contains(graftRoot);
        }
        finally { rw.ExitReadLock(); }

        if (graftRoot is not null && generatedBasis is not null && !graftHidden && !merged.ContainsKey(graftRoot))
        {
            var rootEntry = generatedBasis.Stat("");
            if (rootEntry is not null)
                merged[graftRoot] = ToDeltaEntry(rootEntry, graftRoot);
        }

        var result = merged.Values.ToArray();
        Array.Sort(result, (a, b) => policy.Comparer.Compare(a.Path, b.Path));
        return result;
    }

    public DeltaEntry Create(string normalizedPath, bool directory, uint attributes) =>
        TryCreate(normalizedPath, directory, attributes) ?? throw new IOException("Path already exists.");

    public DeltaEntry? TryCreate(string normalizedPath, bool directory, uint attributes) =>
        TryCreate(normalizedPath, directory, attributes, Guid.NewGuid());
    internal DeltaEntry? TryCreate(string normalizedPath, bool directory, uint attributes, Guid id)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        rw.EnterWriteLock();
        try
        {
            if (normalizedPath.Length == 0) throw new IOException("Cannot create root.");
            if (paths.ContainsKey(normalizedPath) || (!hidden.Contains(normalizedPath) && ResolveBasisLocked(normalizedPath) is not null))
                return null;
            RequireParentLocked(normalizedPath);
            long now = DateTime.UtcNow.ToFileTimeUtc();
            uint normalizedAttrs = policy == WorkspacePathPolicy.Windows
                ? directory ? attributes | 16u : attributes & ~16u
                : attributes;
            var entry = new DeltaEntry(id, normalizedPath, directory, 0, normalizedAttrs, now, now, now, null, 0);
            var node = new Node(entry, isPrivateFile: !directory);
            nodes[entry.Id] = node;
            IndexPathLocked(normalizedPath, entry.Id);
            return entry;
        }
        finally { rw.ExitWriteLock(); }
    }

    public int Read(Guid id, long offset, Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
        if (destination.IsEmpty) return 0;

        // Fast path: check read-only unmodified basis handle first.
        DeltaEntry? entry = null;
        bool isPrivateFile = false;
        RangeExtent[]? extentSnapshot = null;
        bool countedRangeRead = false;

        rw.EnterReadLock();
        try
        {
            if (nodes.TryGetValue(id, out var node))
            {
                entry = node.Entry;
                if (entry.Directory) throw new IOException("Cannot read a directory.");
                isPrivateFile = node.IsPrivateFile;
                if (!isPrivateFile && node.Extents is { Count: > 0 } extents)
                {
                    long end = offset + Math.Min(destination.Length, Math.Max(0, entry.Length - offset));
                    int first = FindExtent(extents, offset);
                    if (first < extents.Count && extents[first].Start < end)
                    {
                        int last = first;
                        while (last < extents.Count && extents[last].Start < end) last++;
                        extentSnapshot = new RangeExtent[last - first];
                        for (int i = first; i < last; i++) extentSnapshot[i - first] = extents[i];
                        Interlocked.Increment(ref activeRangeReads);
                        countedRangeRead = true;
                    }
                }
            }
        }
        finally { rw.ExitReadLock(); }

        try
        {
        if (entry is null)
        {
            if (!baseHandleCache.TryGetValue(id, out entry))
                throw new FileNotFoundException("Unknown ephemeral open file identity.");
            if (entry.Directory) throw new IOException("Cannot read a directory.");
        }

        int count = (int)Math.Min(destination.Length, Math.Max(0, entry.Length - offset));
        if (count == 0) return 0;

        if (isPrivateFile)
        {
            int filled = ReadPrivateFile(id, offset, destination[..count]);
            if (filled < count) destination.Slice(filled, count - filled).Clear();
            Interlocked.Add(ref readBytes, count);
            return count;
        }

        if (extentSnapshot is null)
        {
            // Pure basis read (zero private extents overlap this range).
            long baseEnd = Math.Min(entry.BaseLength, offset + count);
            if (entry.BasePath is not null && offset < baseEnd)
            {
                int baseCount = (int)(baseEnd - offset);
                ReadBasisExact(entry.BasePath, offset, destination[..baseCount]);
                if (baseCount < count) destination.Slice(baseCount, count - baseCount).Clear();
            }
            else
            {
                destination[..count].Clear();
            }
            Interlocked.Add(ref readBytes, count);
            return count;
        }

        // Mixed basis + private ranges.
        Span<byte> output = destination[..count];
        output.Clear();
        long cursor = offset, readEnd = offset + count;
        var handle = rangesHandle ?? throw new ObjectDisposedException(nameof(EphemeralRangeStore));
        foreach (var extent in extentSnapshot)
        {
            if (extent.Start >= readEnd) break;
            long from = Math.Max(offset, extent.Start);
            long to = Math.Min(readEnd, extent.Start + extent.Length);
            if (from >= to) continue;
            if (cursor < from)
            {
                long basisTo = Math.Min(from, entry.BaseLength);
                if (entry.BasePath is not null && cursor < basisTo)
                    ReadBasisExact(entry.BasePath, cursor, output.Slice((int)(cursor - offset), (int)(basisTo - cursor)));
            }
            int outputOffset = (int)(from - offset);
            int length = (int)(to - from);
            long physical = extent.DataOffset + (from - extent.Start);
            ReadHandleExact(handle, physical, output.Slice(outputOffset, length));
            cursor = to;
        }
        if (cursor < readEnd)
        {
            long basisTo = Math.Min(readEnd, entry.BaseLength);
            if (entry.BasePath is not null && cursor < basisTo)
                ReadBasisExact(entry.BasePath, cursor, output.Slice((int)(cursor - offset), (int)(basisTo - cursor)));
        }
        Interlocked.Add(ref readBytes, count);
        return count;
        }
        finally
        {
            if (countedRangeRead) Interlocked.Decrement(ref activeRangeReads);
        }
    }

    public void Write(Guid id, long offset, ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
        if (data.IsEmpty) return;

        bool isPrivateFile;
        long end = checked(offset + data.Length);
        rw.EnterWriteLock();
        try
        {
            var node = EnsureWritableFileLocked(id);
            isPrivateFile = node.IsPrivateFile;
            if (!isPrivateFile)
            {
                WriteBasisRangeLocked(node, offset, data);
                node.Entry = node.Entry with
                {
                    Length = Math.Max(node.Entry.Length, end),
                    LastWriteTime = DateTime.UtcNow.ToFileTimeUtc()
                };
            }
            else
            {
                WritePrivateFile(id, offset, data);
                node.Entry = node.Entry with
                {
                    Length = Math.Max(node.Entry.Length, end),
                    LastWriteTime = DateTime.UtcNow.ToFileTimeUtc()
                };
            }
            Interlocked.Add(ref writeBytes, data.Length);
            Interlocked.Increment(ref writeCount);
        }
        finally { rw.ExitWriteLock(); }
    }

    private void WriteBasisRangeLocked(Node node, long offset, ReadOnlySpan<byte> data)
    {
        // Reuse existing ranges.dat space in place when overwriting inside an already
        // allocated extent and no concurrent range read is active.
        if (Volatile.Read(ref activeRangeReads) == 0 && node.Extents is { Count: > 0 } extents)
        {
            int idx = FindExtent(extents, offset);
            if (idx < extents.Count)
            {
                var ext = extents[idx];
                if (offset >= ext.Start && checked(offset + data.Length) <= ext.Start + ext.Length)
                {
                    long physical = checked(ext.DataOffset + (offset - ext.Start));
                    lock (rangesWriteGate)
                    {
                        var handle = rangesHandle ?? throw new ObjectDisposedException(nameof(EphemeralRangeStore));
                        RandomAccess.Write(handle, data, physical);
                    }
                    return;
                }
            }
        }
        long writePos;
        lock (rangesWriteGate)
        {
            var handle = rangesHandle ?? throw new ObjectDisposedException(nameof(EphemeralRangeStore));
            writePos = rangesLength;
            RandomAccess.Write(handle, data, writePos);
            rangesLength = checked(writePos + data.Length);
        }
        node.Extents ??= [];
        SpliceExtent(node.Extents, new RangeExtent(offset, data.Length, writePos));
    }

    public DeltaEntry WriteFile(Guid id, ulong offset, ReadOnlySpan<byte> data, bool writeToEndOfFile, bool constrainedIo, out uint bytesWritten)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        rw.EnterWriteLock();
        try
        {
            var node = EnsureWritableFileLocked(id);
            ulong actualOffset = writeToEndOfFile ? (ulong)node.Entry.Length : offset;
            int effectiveLength;
            if (constrainedIo)
            {
                if (actualOffset >= (ulong)node.Entry.Length) effectiveLength = 0;
                else effectiveLength = (int)Math.Min((ulong)data.Length, (ulong)node.Entry.Length - actualOffset);
            }
            else
            {
                effectiveLength = data.Length;
            }
            if (effectiveLength == 0)
            {
                bytesWritten = 0;
                return node.Entry;
            }
            long writeOffset = checked((long)actualOffset);
            long end = checked(writeOffset + effectiveLength);
            ReadOnlySpan<byte> slice = data[..effectiveLength];
            if (!node.IsPrivateFile)
            {
                WriteBasisRangeLocked(node, writeOffset, slice);
            }
            else
            {
                WritePrivateFile(id, writeOffset, slice);
            }
            node.Entry = node.Entry with
            {
                Length = Math.Max(node.Entry.Length, end),
                LastWriteTime = DateTime.UtcNow.ToFileTimeUtc()
            };
            Interlocked.Add(ref writeBytes, effectiveLength);
            Interlocked.Increment(ref writeCount);
            bytesWritten = (uint)effectiveLength;
            return node.Entry;
        }
        finally { rw.ExitWriteLock(); }
    }

    public void SetLength(Guid id, long length)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        rw.EnterWriteLock();
        try
        {
            var node = EnsureWritableFileLocked(id);
            if (length == node.Entry.Length && (node.IsPrivateFile || length != 0)) return;
            if (length == 0 && !node.IsPrivateFile)
            {
                // Truncating a basis-backed file to 0 means zero shared bytes remain;
                // promote to native private scratch file for zero-fragmentation overwrites.
                node.IsPrivateFile = true;
                node.Extents = null;
                node.Entry = node.Entry with { Length = 0, BaseLength = 0, BasePath = null, LastWriteTime = DateTime.UtcNow.ToFileTimeUtc() };
                SetPrivateFileLength(id, 0);
            }
            else if (node.IsPrivateFile)
            {
                SetPrivateFileLength(id, length);
                node.Entry = node.Entry with { Length = length, LastWriteTime = DateTime.UtcNow.ToFileTimeUtc() };
            }
            else
            {
                node.Entry = node.Entry with
                {
                    Length = length,
                    BaseLength = Math.Min(node.Entry.BaseLength, length),
                    LastWriteTime = DateTime.UtcNow.ToFileTimeUtc()
                };
                if (node.Extents is not null) TruncateExtents(node.Extents, length);
            }
        }
        finally { rw.ExitWriteLock(); }
    }

    public void SetMetadata(Guid id, uint attributes, long creationTime, long accessTime, long writeTime)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        rw.EnterWriteLock();
        try
        {
            var node = EnsureWritableNodeLocked(id);
            var updated = node.Entry with
            {
                Attributes = attributes,
                CreationTime = creationTime,
                LastAccessTime = accessTime,
                LastWriteTime = writeTime
            };
            if (updated != node.Entry) node.Entry = updated;
        }
        finally { rw.ExitWriteLock(); }
    }

    public void SetLengthAndMetadata(Guid id, long length, uint attributes, long creationTime, long accessTime, long writeTime)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        rw.EnterWriteLock();
        try
        {
            var node = EnsureWritableFileLocked(id);
            if (length == 0 && !node.IsPrivateFile)
            {
                node.IsPrivateFile = true;
                node.Extents = null;
                SetPrivateFileLength(id, 0);
                node.Entry = node.Entry with
                {
                    Length = 0,
                    BaseLength = 0,
                    BasePath = null,
                    Attributes = attributes,
                    CreationTime = creationTime,
                    LastAccessTime = accessTime,
                    LastWriteTime = writeTime
                };
            }
            else if (node.IsPrivateFile)
            {
                if (length != node.Entry.Length) SetPrivateFileLength(id, length);
                node.Entry = node.Entry with
                {
                    Length = length,
                    Attributes = attributes,
                    CreationTime = creationTime,
                    LastAccessTime = accessTime,
                    LastWriteTime = writeTime
                };
            }
            else
            {
                node.Entry = node.Entry with
                {
                    Length = length,
                    BaseLength = Math.Min(node.Entry.BaseLength, length),
                    Attributes = attributes,
                    CreationTime = creationTime,
                    LastAccessTime = accessTime,
                    LastWriteTime = writeTime
                };
                if (node.Extents is not null) TruncateExtents(node.Extents, length);
            }
        }
        finally { rw.ExitWriteLock(); }
    }

    public void Rename(Guid id, string destination, bool replace)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        rw.EnterWriteLock();
        try
        {
            var node = EnsureWritableNodeLocked(id);
            if (node.Entry.Path.Length == 0 || node.Entry.Deleted || destination.Length == 0 || policy.IsChild(destination, node.Entry.Path))
                throw new IOException("Invalid rename.");
            RequireParentLocked(destination);
            var existing = LookupEntryLocked(destination);
            if (existing is not null && existing.Id != node.Entry.Id)
            {
                if (!replace || existing.Directory || node.Entry.Directory)
                    throw new IOException("Rename destination exists.");
                if (nodes.TryGetValue(existing.Id, out var existingNode))
                {
                    existingNode.Entry = existingNode.Entry with { Deleted = true };
                    RemovePathLocked(destination);
                }
                else if (baseHandleCache.TryRemove(existing.Id, out var cachedExisting))
                {
                    nodes[existing.Id] = new Node(cachedExisting with { Deleted = true }, isPrivateFile: false);
                }
                hidden.Add(destination);
            }

            string oldPath = node.Entry.Path;
            var affected = CollectSubtreeLocked(node);
            foreach (var moved in affected) RemovePathLocked(moved.Entry.Path);
            foreach (var moved in affected)
            {
                string nextPath = destination + moved.Entry.Path[oldPath.Length..];
                moved.Entry = moved.Entry with { Path = nextPath };
                IndexPathLocked(nextPath, moved.Entry.Id);
            }
            if (node.Entry.Directory)
            {
                string prefix = oldPath + "/";
                var movedHidden = hidden.Where(p => p.StartsWith(prefix, policy.Comparison)).ToArray();
                foreach (string h in movedHidden)
                {
                    hidden.Remove(h);
                    hidden.Add(destination + h[oldPath.Length..]);
                }
            }
            hidden.Add(oldPath);
            hidden.Remove(destination);
        }
        finally { rw.ExitWriteLock(); }
    }

    public void Delete(Guid id)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        rw.EnterWriteLock();
        try
        {
            var node = EnsureWritableNodeLocked(id);
            if (node.Entry.Deleted) return;
            if (node.Entry.Directory && HasAnyChildLocked(node.Entry))
                throw new IOException("Directory is not empty.");
            string path = node.Entry.Path;
            node.Entry = node.Entry with { Deleted = true };
            RemovePathLocked(path);
            hidden.Add(path);
        }
        finally { rw.ExitWriteLock(); }
    }

    public void ReleaseUnlinkedFile(Guid id)
    {
        if (disposed) return;
        baseHandleCache.TryRemove(id, out _);
        bool deletePrivate = false;
        rw.EnterWriteLock();
        try
        {
            if (nodes.TryGetValue(id, out var node) && node.Entry.Deleted)
            {
                deletePrivate = node.IsPrivateFile;
                nodes.Remove(id);
            }
        }
        finally { rw.ExitWriteLock(); }

        if (deletePrivate) DeletePrivateFile(id);
    }

    public void Flush()
    {
        // Ephemeral workspace storage intentionally avoids per-write fsync;
        // persist the lightweight index snapshot on unmount/Dispose.
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        try { SaveIndex(); } catch (IOException) { }
        CloseAllPooledHandles();
        lock (rangesWriteGate)
        {
            rangesHandle?.Dispose();
            rangesHandle = null;
        }
        rw.Dispose();
    }

    private DeltaEntry? LookupEntryLocked(string normalizedPath)
    {
        if (paths.TryGetValue(normalizedPath, out var id)) return nodes[id].Entry;
        if (hidden.Contains(normalizedPath)) return null;
        return ResolveBasisLocked(normalizedPath);
    }

    private DeltaEntry? ResolveBasisLocked(string normalizedPath)
    {
        if (generatedBasis is null || graftRoot is null) return null;
        if (normalizedPath.Equals(graftRoot, policy.Comparison))
        {
            var root = generatedBasis.Stat("");
            return root is null ? null : ToDeltaEntry(root, graftRoot);
        }
        // Walk up to find the nearest parent (supports renamed basis directories).
        string parentPath = Parent(normalizedPath);
        if (parentPath.Length == 0) return null;
        string? baseParent;
        if (paths.TryGetValue(parentPath, out var parentId))
        {
            var p = nodes[parentId].Entry;
            if (!p.Directory || p.BasePath is null) return null;
            baseParent = p.BasePath;
        }
        else if (hidden.Contains(parentPath))
        {
            return null;
        }
        else if (parentPath.Equals(graftRoot, policy.Comparison))
        {
            baseParent = "";
        }
        else if (parentPath.StartsWith(graftRoot + "/", policy.Comparison) && !HasHiddenOrModifiedAncestorLocked(parentPath))
        {
            baseParent = parentPath[(graftRoot.Length + 1)..];
        }
        else
        {
            var resolvedParent = ResolveBasisLocked(parentPath);
            if (resolvedParent is not { Directory: true, BasePath: { } bp }) return null;
            baseParent = bp;
        }
        string basisPath = Join(baseParent, Name(normalizedPath));
        var entry = generatedBasis.Stat(basisPath);
        return entry is null ? null : ToDeltaEntry(entry, normalizedPath);
    }

    private bool HasHiddenOrModifiedAncestorLocked(string path)
    {
        if (hidden.Count == 0 && paths.Count == 0) return false;
        for (int slash = path.IndexOf('/'); slash >= 0; slash = path.IndexOf('/', slash + 1))
        {
            string ancestor = path[..slash];
            if (hidden.Contains(ancestor) || paths.ContainsKey(ancestor)) return true;
        }
        return false;
    }

    private void RequireParentLocked(string normalizedPath)
    {
        string parent = Parent(normalizedPath);
        if (parent.Length == 0) return; // Root directory always exists.
        var entry = LookupEntryLocked(parent);
        if (entry is not { Directory: true }) throw new DirectoryNotFoundException(parent);
    }

    private bool HasAnyChildLocked(DeltaEntry dir)
    {
        if (modifiedChildren.TryGetValue(dir.Path, out var children) && children.Count > 0)
            return true;
        if (dir.BasePath is not null && generatedBasis is not null)
        {
            foreach (var child in generatedBasis.EnumerateChildren(dir.BasePath))
            {
                string childPath = Join(dir.Path, Name(child.Path));
                if (!hidden.Contains(childPath)) return true;
            }
        }
        return false;
    }

    private Node EnsureWritableFileLocked(Guid id)
    {
        var node = EnsureWritableNodeLocked(id);
        if (node.Entry.Directory) throw new IOException("Cannot write or resize a directory.");
        return node;
    }

    private Node EnsureWritableNodeLocked(Guid id)
    {
        if (nodes.TryGetValue(id, out var existing)) return existing;
        if (baseHandleCache.TryRemove(id, out var basisEntry))
        {
            var node = new Node(basisEntry, isPrivateFile: false);
            nodes[id] = node;
            if (!basisEntry.Deleted) IndexPathLocked(basisEntry.Path, id);
            return node;
        }
        throw new FileNotFoundException("Unknown ephemeral open file identity.");
    }

    private List<Node> CollectSubtreeLocked(Node root)
    {
        var result = new List<Node> { root };
        if (!root.Entry.Directory) return result;
        string prefix = root.Entry.Path + "/";
        foreach (var (path, id) in paths)
        {
            if (path.StartsWith(prefix, policy.Comparison)) result.Add(nodes[id]);
        }
        return result;
    }

    private void IndexPathLocked(string path, Guid id)
    {
        if (paths.TryGetValue(path, out var existing))
        {
            if (existing == id) return;
            RemovePathLocked(path);
        }
        paths[path] = id;
        hidden.Remove(path);
        string parent = Parent(path);
        if (!modifiedChildren.TryGetValue(parent, out var set))
            modifiedChildren[parent] = set = new(policy.Comparer);
        set.Add(path);
    }

    private void RemovePathLocked(string path)
    {
        if (!paths.Remove(path)) return;
        string parent = Parent(path);
        if (modifiedChildren.TryGetValue(parent, out var set))
        {
            set.Remove(path);
            if (set.Count == 0) modifiedChildren.Remove(parent);
        }
    }

    private DeltaEntry ToDeltaEntry(SnapshotEntry entry, string mountedPath)
    {
        Guid id = BasisGuid(entry.Path);
        return new DeltaEntry(id, mountedPath, entry.Directory, entry.Length, entry.Attributes,
            entry.CreationTime, entry.LastAccessTime, entry.LastWriteTime, entry.Path, entry.Length, UnixMode: entry.UnixMode);
    }

    private Guid BasisGuid(string basisPath)
    {
        string key = "ephemeral:" + (identity.Graft?.Basis.Id ?? identity.Snapshot.Id) + "/" + policy.IdentityPath(basisPath);
        Span<byte> hash = stackalloc byte[32];
        int byteCount = Encoding.UTF8.GetByteCount(key);
        byte[]? rented = byteCount > 256 ? ArrayPool<byte>.Shared.Rent(byteCount) : null;
        try
        {
            Span<byte> utf8 = rented is null ? stackalloc byte[byteCount] : rented.AsSpan(0, byteCount);
            Encoding.UTF8.GetBytes(key, utf8);
            SHA256.HashData(utf8, hash);
            return new Guid(hash[..16]);
        }
        finally { if (rented is not null) ArrayPool<byte>.Shared.Return(rented); }
    }

    private void ReadBasisExact(string basisPath, long offset, Span<byte> target)
    {
        var basis = generatedBasis ?? throw new WorkspaceException("SnapshotMissing", "Generated basis is missing.");
        int done = 0;
        while (done < target.Length)
        {
            int n = basis.Read(basisPath, offset + done, target[done..]);
            if (n <= 0) throw new WorkspaceException("SnapshotReadFailed", "Generated basis returned unexpected short read.");
            done += n;
        }
    }

    private static void ReadHandleExact(SafeFileHandle handle, long offset, Span<byte> target)
    {
        int done = 0;
        while (done < target.Length)
        {
            int n = RandomAccess.Read(handle, target[done..], offset + done);
            if (n <= 0) throw new IOException("Unexpected EOF reading ephemeral range store.");
            done += n;
        }
    }

    private string PrivateFilePath(Guid id) => Path.Combine(filesDirectory, id.ToString("N") + ".bin");

    private PooledHandle AcquirePrivateHandle(Guid id, bool create)
    {
        PooledHandle? toDispose = null;
        try
        {
            lock (poolGate)
            {
                if (handlePool.TryGetValue(id, out var existing))
                {
                    handleLru.Remove(existing.LruNode!);
                    handleLru.AddLast(existing.LruNode!);
                    existing.ActiveRefs++;
                    return existing;
                }
            }

            string path = PrivateFilePath(id);
            if (!create && !File.Exists(path)) return null!;
            var handle = File.OpenHandle(path, create ? FileMode.OpenOrCreate : FileMode.Open,
                FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete, FileOptions.RandomAccess);

            lock (poolGate)
            {
                if (handlePool.TryGetValue(id, out var raceWinner))
                {
                    handle.Dispose();
                    handleLru.Remove(raceWinner.LruNode!);
                    handleLru.AddLast(raceWinner.LruNode!);
                    raceWinner.ActiveRefs++;
                    return raceWinner;
                }
                if (handlePool.Count >= MaxPooledHandles)
                {
                    var victim = handleLru.First;
                    while (victim is not null && victim.Value.ActiveRefs > 0) victim = victim.Next;
                    if (victim is not null)
                    {
                        handleLru.Remove(victim);
                        handlePool.Remove(victim.Value.Id);
                        victim.Value.Evicted = true;
                        toDispose = victim.Value;
                    }
                }
                var entry = new PooledHandle(id, handle) { ActiveRefs = 1 };
                entry.LruNode = handleLru.AddLast(entry);
                handlePool[id] = entry;
                return entry;
            }
        }
        finally
        {
            toDispose?.Handle.Dispose();
        }
    }

    private void ReleasePrivateHandle(PooledHandle entry)
    {
        bool shouldDispose = false;
        lock (poolGate)
        {
            entry.ActiveRefs--;
            if (entry.Evicted && entry.ActiveRefs == 0) shouldDispose = true;
        }
        if (shouldDispose) entry.Handle.Dispose();
    }

    private int ReadPrivateFile(Guid id, long offset, Span<byte> destination)
    {
        var pooled = AcquirePrivateHandle(id, create: false);
        if (pooled is null) return 0;
        try
        {
            int total = 0;
            while (total < destination.Length)
            {
                int n = RandomAccess.Read(pooled.Handle, destination[total..], offset + total);
                if (n == 0) break;
                total += n;
            }
            return total;
        }
        finally { ReleasePrivateHandle(pooled); }
    }

    private void WritePrivateFile(Guid id, long offset, ReadOnlySpan<byte> data)
    {
        var pooled = AcquirePrivateHandle(id, create: true);
        try { RandomAccess.Write(pooled.Handle, data, offset); }
        finally { ReleasePrivateHandle(pooled); }
    }

    private void SetPrivateFileLength(Guid id, long length)
    {
        var pooled = AcquirePrivateHandle(id, create: length > 0);
        if (pooled is null) return;
        try { RandomAccess.SetLength(pooled.Handle, length); }
        finally { ReleasePrivateHandle(pooled); }
    }

    private void DeletePrivateFile(Guid id)
    {
        PooledHandle? removed = null;
        lock (poolGate)
        {
            if (handlePool.Remove(id, out var entry))
            {
                handleLru.Remove(entry.LruNode!);
                entry.Evicted = true;
                if (entry.ActiveRefs == 0) removed = entry;
            }
        }
        removed?.Handle.Dispose();
        string path = PrivateFilePath(id);
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
    }

    private void CloseAllPooledHandles()
    {
        PooledHandle[] all;
        lock (poolGate)
        {
            all = handlePool.Values.ToArray();
            handlePool.Clear();
            handleLru.Clear();
        }
        foreach (var entry in all) entry.Handle.Dispose();
    }

    private static int FindExtent(List<RangeExtent> extents, long offset)
    {
        int low = 0, high = extents.Count;
        while (low < high)
        {
            int mid = low + (high - low) / 2;
            if (extents[mid].Start + extents[mid].Length <= offset) low = mid + 1;
            else high = mid;
        }
        return low;
    }

    private static void SpliceExtent(List<RangeExtent> extents, RangeExtent incoming)
    {
        int count = extents.Count;
        if (count == 0 || extents[count - 1].Start + extents[count - 1].Length <= incoming.Start)
        {
            extents.Add(incoming);
            return;
        }
        long end = incoming.Start + incoming.Length;
        int first = FindExtent(extents, incoming.Start);
        int after = first;
        while (after < count && extents[after].Start < end) after++;

        RangeExtent? left = null, right = null;
        if (first < after)
        {
            var firstExt = extents[first];
            if (firstExt.Start < incoming.Start)
                left = firstExt with { Length = checked((int)(incoming.Start - firstExt.Start)) };
            var lastExt = extents[after - 1];
            long lastEnd = lastExt.Start + lastExt.Length;
            if (lastEnd > end)
            {
                int skip = checked((int)(end - lastExt.Start));
                right = new RangeExtent(end, lastExt.Length - skip, checked(lastExt.DataOffset + skip));
            }
        }

        extents.RemoveRange(first, after - first);
        int idx = first;
        if (left.HasValue) extents.Insert(idx++, left.Value);
        extents.Insert(idx++, incoming);
        if (right.HasValue) extents.Insert(idx, right.Value);
    }

    private static void TruncateExtents(List<RangeExtent> extents, long length)
    {
        for (int i = extents.Count - 1; i >= 0; i--)
        {
            if (extents[i].Start >= length) extents.RemoveAt(i);
            else if (extents[i].Start + extents[i].Length > length)
            {
                extents[i] = extents[i] with { Length = checked((int)(length - extents[i].Start)) };
                break;
            }
            else break;
        }
    }

    internal void WithNamespaceLock(Action action)
    {
        rw.EnterWriteLock();
        try { action(); } finally { rw.ExitWriteLock(); }
    }

    private void RequireResetAllowed()
    {
        if (File.Exists(Path.Combine(scratchDirectory, PartitionedWorkspaceDeltaStore.MoveIntentName)))
            throw new IOException("Preserve the ephemeral index and the pending cross-partition move.");
    }

    internal void PersistMove(IEnumerable<Guid> files)
    {
        bool flushRanges = false;
        foreach (Guid id in files)
        {
            if (!nodes.TryGetValue(id, out var node) || node.Entry.Directory) continue;
            if (!node.IsPrivateFile) { flushRanges |= node.Extents is { Count: > 0 }; continue; }
            if (node.Entry.Length == 0) continue;
            var file = AcquirePrivateHandle(id, create: false);
            try { RandomAccess.FlushToDisk(file.Handle); } finally { ReleasePrivateHandle(file); }
        }
        if (flushRanges) RandomAccess.FlushToDisk(rangesHandle!);
        SaveIndex(durable: true);
    }

    private void SaveIndex(bool durable = false)
    {
        string temp = indexPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        rw.EnterReadLock();
        try
        {
            using var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536);
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: false);
            writer.Write(IndexMagic);
            writer.Write(1);
            writer.Write(identity.Id);
            writer.Write(identity.Snapshot.Id);
            writer.Write(identity.Graft?.Basis.Id ?? "");
            writer.Write(rangesLength);
            writer.Write(hidden.Count);
            foreach (string h in hidden) writer.Write(h);
            var liveNodes = nodes.Values.Where(n => !n.Entry.Deleted).ToArray();
            writer.Write(liveNodes.Length);
            foreach (var node in liveNodes)
            {
                var e = node.Entry;
                writer.Write(e.Id.ToByteArray());
                writer.Write(e.Path);
                writer.Write(e.Directory);
                writer.Write(e.Length);
                writer.Write(e.Attributes);
                writer.Write(e.CreationTime);
                writer.Write(e.LastAccessTime);
                writer.Write(e.LastWriteTime);
                writer.Write(e.BasePath is not null);
                if (e.BasePath is not null) writer.Write(e.BasePath);
                writer.Write(e.BaseLength);
                writer.Write(e.UnixMode.HasValue);
                if (e.UnixMode.HasValue) writer.Write(e.UnixMode.Value);
                writer.Write(node.IsPrivateFile);
                int extCount = node.Extents?.Count ?? 0;
                writer.Write(extCount);
                if (extCount > 0)
                {
                    foreach (var ext in node.Extents!)
                    {
                        writer.Write(ext.Start);
                        writer.Write(ext.Length);
                        writer.Write(ext.DataOffset);
                    }
                }
            }
            writer.Flush();
            if (durable) stream.Flush(true);
        }
        finally { rw.ExitReadLock(); }
        File.Move(temp, indexPath, overwrite: true);
    }

    private bool TryLoadIndex()
    {
        if (!File.Exists(indexPath)) return false;
        try
        {
            using var stream = new FileStream(indexPath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);
            byte[] magic = reader.ReadBytes(4);
            if (!magic.AsSpan().SequenceEqual(IndexMagic) || reader.ReadInt32() != 1) return false;
            if (reader.ReadString() != identity.Id || reader.ReadString() != identity.Snapshot.Id ||
                reader.ReadString() != (identity.Graft?.Basis.Id ?? "")) return false;
            rangesLength = reader.ReadInt64();
            if (rangesLength < 0) return false;
            int hiddenCount = reader.ReadInt32();
            for (int i = 0; i < hiddenCount; i++) hidden.Add(reader.ReadString());
            int nodeCount = reader.ReadInt32();
            for (int i = 0; i < nodeCount; i++)
            {
                Guid id = new(reader.ReadBytes(16));
                string path = reader.ReadString();
                bool directory = reader.ReadBoolean();
                long length = reader.ReadInt64();
                uint attributes = reader.ReadUInt32();
                long created = reader.ReadInt64();
                long accessed = reader.ReadInt64();
                long written = reader.ReadInt64();
                string? basePath = reader.ReadBoolean() ? reader.ReadString() : null;
                long baseLength = reader.ReadInt64();
                uint? unixMode = reader.ReadBoolean() ? reader.ReadUInt32() : null;
                bool isPrivate = reader.ReadBoolean();
                int extCount = reader.ReadInt32();
                List<RangeExtent>? extents = extCount > 0 ? new List<RangeExtent>(extCount) : null;
                for (int j = 0; j < extCount; j++)
                    extents!.Add(new RangeExtent(reader.ReadInt64(), reader.ReadInt32(), reader.ReadInt64()));
                var entry = new DeltaEntry(id, path, directory, length, attributes, created, accessed, written, basePath, baseLength, false, unixMode);
                var node = new Node(entry, isPrivate) { Extents = extents };
                nodes[id] = node;
                IndexPathLocked(path, id);
            }
            return true;
        }
        catch (Exception ex) when (ex is IOException or EndOfStreamException or ArgumentException)
        {
            nodes.Clear(); paths.Clear(); modifiedChildren.Clear(); hidden.Clear(); rangesLength = 0;
            return false;
        }
    }

    private void ResetScratchDirectory()
    {
        nodes.Clear(); paths.Clear(); modifiedChildren.Clear(); hidden.Clear(); rangesLength = 0;
        if (Directory.Exists(filesDirectory))
            try { Directory.Delete(filesDirectory, recursive: true); } catch (IOException) { }
        try { if (File.Exists(rangesPath)) File.Delete(rangesPath); } catch (IOException) { }
        try { if (File.Exists(indexPath)) File.Delete(indexPath); } catch (IOException) { }
    }

    private static string Parent(string path) { int slash = path.LastIndexOf('/'); return slash < 0 ? "" : path[..slash]; }
    private static string Name(string path) => path[(path.LastIndexOf('/') + 1)..];
    private static string Join(string parent, string name) => parent.Length == 0 ? name : parent + "/" + name;
}

// Routes workspace source paths (Assets, Packages, ProjectSettings, etc.) to
// JournalDeltaStore and disposable generated/ephemeral paths (Library, Temp,
// Logs, UserSettings, obj, IDE project files) to EphemeralRangeStore.
public sealed class PartitionedWorkspaceDeltaStore : IWorkspaceDeltaStore, IWorkspaceWriteBatch, IWorkspaceMetadataBatch, IWorkspaceMutationBatch
{
    private readonly JournalDeltaStore source;
    private readonly EphemeralRangeStore ephemeral;
    private readonly WorkspacePathPolicy policy;
    private readonly ConcurrentDictionary<Guid, (Guid TargetId, bool TargetIsEphemeral)> crossPartitionRedirects = new();
    private readonly ConcurrentDictionary<Guid, byte> redirectTargets = new();

    public PartitionedWorkspaceDeltaStore(JournalDeltaStore source, EphemeralRangeStore ephemeral)
    {
        this.source = source;
        this.ephemeral = ephemeral;
        policy = WorkspacePathPolicy.For(source.Identity.Snapshot);
        if (File.Exists(MoveIntentPath))
        {
            pendingMove = JsonSerializer.Deserialize<MoveIntent>(File.ReadAllBytes(MoveIntentPath))
                ?? throw new IOException("Cannot read the pending cross-partition move.");
            WithMoveLock(() => RecoverMove(pendingMove));
        }
    }

    public JournalDeltaStore SourceStore => source;
    public EphemeralRangeStore EphemeralStore => ephemeral;
    public OverlayIdentity Identity => source.Identity;
    public string? BackingPath => source.BackingPath;

    public string Normalize(string rawPath) => policy.Normalize(rawPath);

    public bool IsEphemeralPath(string rawPath)
    {
        string normalized = policy.Normalize(rawPath);
        return ephemeral.MatchesPath(normalized);
    }

    public bool IsEphemeralId(Guid id) =>
        crossPartitionRedirects.TryGetValue(id, out var redirect) ? redirect.TargetIsEphemeral : ephemeral.OwnsId(id);

    private (Guid Id, bool IsEphemeral) ResolveTarget(Guid id) =>
        crossPartitionRedirects.TryGetValue(id, out var redirect) ? redirect : (id, ephemeral.OwnsId(id));

    public DeltaEntry? Stat(string path)
    {
        string normalized = policy.Normalize(path);
        return ephemeral.MatchesPath(normalized) ? ephemeral.Stat(normalized) : source.Stat(path);
    }

    public DeltaEntry? OpenStat(string path)
    {
        string normalized = policy.Normalize(path);
        return ephemeral.MatchesPath(normalized) ? ephemeral.Stat(normalized, retainForHandle: true) : source.Stat(path);
    }

    public DeltaEntry Get(Guid id)
    {
        var (targetId, isEphemeral) = ResolveTarget(id);
        return isEphemeral ? ephemeral.Get(targetId) : source.Get(targetId);
    }

    public IReadOnlyList<DeltaEntry> List(string directory) => EnumerateChildren(directory).ToArray();

    public IEnumerable<DeltaEntry> EnumerateChildren(string directory)
    {
        string normalized = policy.Normalize(directory);
        if (normalized.Length == 0)
        {
            var merged = new Dictionary<string, DeltaEntry>(policy.Comparer);
            foreach (var entry in source.EnumerateChildren(""))
            {
                if (!ephemeral.MatchesPath(entry.Path))
                    merged[entry.Path] = entry;
            }
            foreach (var entry in ephemeral.EnumerateTopLevel())
                merged[entry.Path] = entry;
            var sorted = merged.Values.ToArray();
            Array.Sort(sorted, (a, b) => policy.Comparer.Compare(a.Path, b.Path));
            return sorted;
        }
        return ephemeral.MatchesPath(normalized) ? ephemeral.EnumerateChildren(normalized) : source.EnumerateChildren(directory);
    }

    public DeltaEntry Create(string path, bool directory, uint attributes)
    {
        string normalized = policy.Normalize(path);
        return ephemeral.MatchesPath(normalized) ? ephemeral.Create(normalized, directory, attributes) : source.Create(path, directory, attributes);
    }

    public int Read(Guid id, long offset, Span<byte> destination)
    {
        var (targetId, isEphemeral) = ResolveTarget(id);
        return isEphemeral ? ephemeral.Read(targetId, offset, destination) : source.Read(targetId, offset, destination);
    }

    public void Write(Guid id, long offset, ReadOnlySpan<byte> data)
    {
        var (targetId, isEphemeral) = ResolveTarget(id);
        if (isEphemeral) ephemeral.Write(targetId, offset, data);
        else source.Write(targetId, offset, data);
    }

    public void SetLength(Guid id, long length)
    {
        var (targetId, isEphemeral) = ResolveTarget(id);
        if (isEphemeral) ephemeral.SetLength(targetId, length);
        else source.SetLength(targetId, length);
    }

    public void SetMetadata(Guid id, uint attributes, long creationTime, long accessTime, long writeTime)
    {
        var (targetId, isEphemeral) = ResolveTarget(id);
        if (isEphemeral) ephemeral.SetMetadata(targetId, attributes, creationTime, accessTime, writeTime);
        else source.SetMetadata(targetId, attributes, creationTime, accessTime, writeTime);
    }

    public void SetLengthAndMetadata(Guid id, long length, uint attributes, long creationTime, long accessTime, long writeTime)
    {
        var (targetId, isEphemeral) = ResolveTarget(id);
        if (isEphemeral) ephemeral.SetLengthAndMetadata(targetId, length, attributes, creationTime, accessTime, writeTime);
        else source.SetLengthAndMetadata(targetId, length, attributes, creationTime, accessTime, writeTime);
    }

    public void SetUnixMode(Guid id, uint? mode)
    {
        var (targetId, isEphemeral) = ResolveTarget(id);
        if (!isEphemeral) source.SetUnixMode(targetId, mode);
    }

    public Action<string>? CrossPartitionBoundary { get; set; }

    public void Rename(Guid id, string destination, bool replace) => Rename(id, destination, replace, null);

    internal void Rename(Guid id, string destination, bool replace, Func<Guid, bool>? retained)
    {
        string normalizedDest = policy.Normalize(destination);
        var (effectiveId, sourceIsEphemeral) = ResolveTarget(id);
        bool destIsEphemeral = ephemeral.MatchesPath(normalizedDest);
        if (id != effectiveId && sourceIsEphemeral == destIsEphemeral && Get(id).Path == normalizedDest) return;
        if (sourceIsEphemeral && destIsEphemeral)
        {
            ephemeral.Rename(effectiveId, normalizedDest, replace);
            return;
        }
        if (!sourceIsEphemeral && !destIsEphemeral)
        {
            source.Rename(effectiveId, destination, replace);
            return;
        }
        WithMoveLock(() =>
        {
            if (pendingMove is not null)
            {
                var previous = pendingMove;
                bool completed = RecoverMove(previous);
                if (completed && previous.SourceId == effectiveId && previous.Destination == normalizedDest && previous.Replace == replace)
                    return;
            }
            MoveAcross(effectiveId, sourceIsEphemeral, normalizedDest, replace, retained);
        });
    }

    internal const string MoveIntentName = "cross-partition-move.json";
    private sealed record MovedEntry(DeltaEntry Before, Guid After, string? Hash);
    private sealed record MoveIntent(string Owner, Guid SourceId, string SourcePath, string Destination, bool Replace,
        string StagePath, Guid StageId, MovedEntry[]? Entries);
    private volatile MoveIntent? pendingMove;
    private string MoveIntentPath => Path.Combine(ephemeral.ScratchDirectory, MoveIntentName);
    internal void RecoverPendingMove()
    {
        if (pendingMove is not null) WithMoveLock(() => { if (pendingMove is { } move) RecoverMove(move); });
    }

    private void WithMoveLock(Action action) => source.WithNamespaceLock(() => ephemeral.WithNamespaceLock(action));

    private void SaveMove(MoveIntent move)
    {
        string path = MoveIntentPath, temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(file, move);
            file.Flush(true);
        }
        AtomicFilePublication.Publish(temp, path);
        pendingMove = move;
    }

    private DeltaEntry? InPartition(string path, bool cache) =>
        cache ? ephemeral.Stat(path, retainForHandle: true) : source.Stat(path);

    private void MoveAcross(Guid id, bool fromCache, string destination, bool replace, Func<Guid, bool>? retained)
    {
        var entry = fromCache ? ephemeral.Get(id) : source.Get(id);
        if (entry.Path.Length == 0 || entry.Deleted || destination.Length == 0 || policy.IsChild(destination, entry.Path))
            throw new IOException("Invalid cross-partition rename.");
        int slash = destination.LastIndexOf('/');
        if (slash > 0 && InPartition(destination[..slash], !fromCache) is not { Directory: true })
            throw new DirectoryNotFoundException(destination[..slash]);
        var existing = InPartition(destination, !fromCache);
        if (existing is not null && (!replace || existing.Directory || entry.Directory))
            throw new IOException("Rename destination exists.");

        Guid stageId = Guid.NewGuid();
        string token = ".splinter-xpart-" + stageId.ToString("N");
        string staging = slash >= 0 ? destination[..(slash + 1)] + token + ".tmp"
            : fromCache ? token + ".tmp" : token + "-" + destination;
        if (InPartition(staging, !fromCache) is not null) throw new IOException("Rename staging name is occupied.");
        var entries = new List<MovedEntry>();
        // Make the input cache namespace recoverable before recording a move. Routine cache I/O
        // never takes a durability barrier. Payload is flushed only for the crossing subtree.
        ephemeral.PersistMove(fromCache ? Walk(entry, true).Select(e => e.Id) : []);
        var move = new MoveIntent(Identity.Id, id, entry.Path, destination, replace, staging, stageId, null);
        SaveMove(move);
        try
        {
            Copy(entry, staging, stageId);
            if (!fromCache) ephemeral.PersistMove(entries.Select(e => e.After));
            move = move with { Entries = entries.ToArray() };
            SaveMove(move);
        }
        catch
        {
            RecoverMove(pendingMove!);
            throw;
        }
        if (fromCache) source.Rename(stageId, destination, replace);
        else { ephemeral.Rename(stageId, destination, replace); ephemeral.PersistMove([]); }
        CrossPartitionBoundary?.Invoke("after-destination-commit");
        FinishMove(move, fromCache, recovering: false, retained);

        void Copy(DeltaEntry old, string path, Guid newId)
        {
            if (fromCache) source.Create(path, old.Directory, old.Attributes, newId);
            else if (ephemeral.TryCreate(path, old.Directory, old.Attributes, newId) is null)
                throw new IOException("Rename staging name is occupied.");
            string? hash = null;
            if (old.Directory)
            {
                var children = fromCache ? ephemeral.EnumerateChildren(old.Path) : source.EnumerateChildren(old.Path);
                foreach (var child in children.ToArray())
                    Copy(InPartition(child.Path, fromCache)!, path + "/" + child.Path[(child.Path.LastIndexOf('/') + 1)..], Guid.NewGuid());
            }
            else hash = Transfer(old, fromCache, newId);
            if (fromCache)
            {
                source.SetMetadata(newId, old.Attributes, old.CreationTime, old.LastAccessTime, old.LastWriteTime);
                if (old.UnixMode.HasValue) source.SetUnixMode(newId, old.UnixMode);
            }
            else ephemeral.SetMetadata(newId, old.Attributes, old.CreationTime, old.LastAccessTime, old.LastWriteTime);
            entries.Add(new(old, newId, hash)); // children before parent for interruption-safe unlink
        }
    }

    private IEnumerable<DeltaEntry> Walk(DeltaEntry entry, bool cache)
    {
        yield return entry;
        if (!entry.Directory) yield break;
        var children = cache ? ephemeral.EnumerateChildren(entry.Path) : source.EnumerateChildren(entry.Path);
        foreach (var child in children)
            foreach (var item in Walk(InPartition(child.Path, cache)!, cache)) yield return item;
    }

    private string Transfer(DeltaEntry old, bool fromCache, Guid? writeTo)
    {
        int chunk = Math.Min(Identity.MaxTransferBytes, 262144);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(chunk);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        try
        {
            for (long offset = 0; offset < old.Length;)
            {
                int count = (int)Math.Min(chunk, old.Length - offset);
                int read = fromCache ? ephemeral.Read(old.Id, offset, buffer.AsSpan(0, count)) :
                    source.Read(old.Id, offset, buffer.AsSpan(0, count));
                if (read <= 0) throw new IOException("Unexpected EOF during cross-partition rename.");
                hash.AppendData(buffer, 0, read);
                if (writeTo is Guid id)
                {
                    if (fromCache) source.Write(id, offset, buffer.AsSpan(0, read));
                    else ephemeral.Write(id, offset, buffer.AsSpan(0, read));
                }
                offset += read;
            }
            return Convert.ToHexStringLower(hash.GetHashAndReset());
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }

    private bool RecoverMove(MoveIntent move)
    {
        bool fromCache = IsEphemeralPath(move.SourcePath);
        if (move.Owner != Identity.Id || fromCache == IsEphemeralPath(move.Destination) ||
            move.StageId == Guid.Empty || move.StagePath == move.Destination)
            throw new IOException("Unrecognized pending cross-partition move.");
        var destination = InPartition(move.Destination, !fromCache);
        if (move.Entries is not null && destination?.Id == move.StageId)
        {
            // The rename already committed. A later edit belongs to this destination.
            // Complete only the source unlink, including any remaining directory children.
            FinishMove(move, fromCache, recovering: true);
            return true;
        }
        var stage = InPartition(move.StagePath, !fromCache);
        if (stage is not null && stage.Id != move.StageId || move.Entries is not null && stage is null)
            throw new IOException("Cross-partition move identities changed. Preserve both paths.");
        if (stage is not null)
        {
            DeleteSubtreeInPartition(stage, !fromCache);
            if (!fromCache) ephemeral.PersistMove([]);
        }
        File.Delete(MoveIntentPath);
        pendingMove = null;
        return false;
    }

    private void FinishMove(MoveIntent move, bool fromCache, bool recovering, Func<Guid, bool>? retained = null)
    {
        var entries = move.Entries!;
        var byId = entries.ToDictionary(item => item.Before.Id);
        if (recovering && InPartition(move.SourcePath, fromCache) is { } remaining)
        {
            foreach (var current in Walk(remaining, fromCache))
                if (!byId.TryGetValue(current.Id, out var recorded) || current.Path != recorded.Before.Path ||
                    current != recorded.Before || !current.Directory && Transfer(current, fromCache, null) != recorded.Hash)
                    throw new IOException("The interrupted rename source changed. Preserve both paths.");
        }
        foreach (var item in entries)
        {
            var current = InPartition(item.Before.Path, fromCache);
            if (current is null) continue;
            if (current.Id != item.Before.Id) throw new IOException("The interrupted rename source was replaced.");
            if (fromCache) ephemeral.Delete(current.Id);
            else { source.Delete(current.Id); source.ReleaseUnlinkedFile(current.Id); }
        }
        if (fromCache)
        {
            ephemeral.PersistMove([]);
            foreach (var item in entries) ephemeral.ReleaseUnlinkedFile(item.Before.Id);
        }
        // Keep every still-open historical alias one lookup from the current node even if an
        // intermediate handle closes. No additional work on normal cache reads or writes.
        foreach (var (alias, target) in crossPartitionRedirects)
            if (byId.TryGetValue(target.TargetId, out var next))
            {
                crossPartitionRedirects[alias] = (next.After, !fromCache);
                redirectTargets[next.After] = 0;
            }
        foreach (var item in entries)
        {
            redirectTargets.TryRemove(item.Before.Id, out _);
            if (retained?.Invoke(item.Before.Id) == false) continue;
            crossPartitionRedirects[item.Before.Id] = (item.After, !fromCache);
            redirectTargets[item.After] = 0;
        }
        File.Delete(MoveIntentPath);
        pendingMove = null;
    }

    private void DeleteSubtreeInPartition(DeltaEntry entry, bool inEphemeral)
    {
        if (entry.Directory)
        {
            var children = inEphemeral ? ephemeral.EnumerateChildren(entry.Path).ToArray() : source.EnumerateChildren(entry.Path).ToArray();
            foreach (var child in children)
            {
                var retainedChild = inEphemeral ? (ephemeral.Stat(child.Path, retainForHandle: true) ?? child) : (source.Stat(child.Path) ?? child);
                DeleteSubtreeInPartition(retainedChild, inEphemeral);
            }
        }
        if (inEphemeral)
        {
            ephemeral.RetainBasisEntry(entry);
            ephemeral.Delete(entry.Id);
            ephemeral.ReleaseUnlinkedFile(entry.Id);
        }
        else
        {
            source.Delete(entry.Id);
            source.ReleaseUnlinkedFile(entry.Id);
        }
    }

    public void Delete(Guid id)
    {
        var (targetId, isEphemeral) = ResolveTarget(id);
        if (isEphemeral) ephemeral.Delete(targetId);
        else source.Delete(targetId);
    }

    internal bool IsMoveHandle(Guid id) => crossPartitionRedirects.ContainsKey(id) || redirectTargets.ContainsKey(id);

    public void ReleaseUnlinkedFile(Guid id) => ReleaseUnlinkedFile(id, null);
    internal void ReleaseUnlinkedFile(Guid id, Func<Guid, bool>? retained)
    {
        if (IsMoveHandle(id))
        {
            if (crossPartitionRedirects.TryRemove(id, out var alias)) id = alias.TargetId;
            if (crossPartitionRedirects.Any(pair => pair.Value.TargetId == id)) return;
            redirectTargets.TryRemove(id, out _);
            if (retained?.Invoke(id) == true) return;
        }
        if (ephemeral.OwnsId(id)) ephemeral.ReleaseUnlinkedFile(id);
        else source.ReleaseUnlinkedFile(id);
    }

    public void Flush()
    {
        ephemeral.Flush();
        source.Flush();
    }

    public long GetJournalBytes() => source.GetJournalBytes();

    public OverlayStorageObservation Observe() => WithEphemeralStats(source.Observe());

    public OverlayStorageObservation? ObserveActivity() =>
        source.ObserveActivity() is { } activity ? WithEphemeralStats(activity) : null;

    private OverlayStorageObservation WithEphemeralStats(OverlayStorageObservation value) =>
        value with { ReadBytes = checked(value.ReadBytes + ephemeral.ReadBytes) };

    public DeltaEntry WriteEphemeral(Guid id, ulong offset, ReadOnlySpan<byte> data, bool writeToEndOfFile, bool constrainedIo, out uint bytesWritten)
    {
        var (targetId, _) = ResolveTarget(id);
        return ephemeral.WriteFile(targetId, offset, data, writeToEndOfFile, constrainedIo, out bytesWritten);
    }

    public DeltaEntry? TryCreateEphemeral(string path, bool directory, uint attributes) =>
        ephemeral.TryCreate(policy.Normalize(path), directory, attributes);

    public DeltaEntry CreateEphemeral(string path, bool directory, uint attributes) =>
        ephemeral.Create(policy.Normalize(path), directory, attributes);

    public DeltaEntry? StatEphemeral(string path, bool retainForHandle = false) =>
        ephemeral.Stat(policy.Normalize(path), retainForHandle);

    public void RetainEphemeralBasis(DeltaEntry entry) => ephemeral.RetainBasisEntry(entry);

    public void RenameEphemeral(Guid id, string destination, bool replace)
    {
        var (targetId, _) = ResolveTarget(id);
        ephemeral.Rename(targetId, policy.Normalize(destination), replace);
    }

    public void WriteBatch(IReadOnlyList<WorkspaceRangeWrite> writes)
    {
        var sourceWrites = new List<WorkspaceRangeWrite>();
        foreach (var write in writes)
        {
            var (targetId, isEphemeral) = ResolveTarget(write.Id);
            if (isEphemeral) ephemeral.Write(targetId, write.Offset, write.Data.Span);
            else sourceWrites.Add(targetId == write.Id ? write : write with { Id = targetId });
        }
        if (sourceWrites.Count > 0) source.WriteBatch(sourceWrites);
    }

    public void ApplyMetadataBatch(IReadOnlyList<Action> mutations) => source.ApplyMetadataBatch(mutations);
    public void ApplyMutationBatch(IReadOnlyList<Action> mutations) => source.ApplyMutationBatch(mutations);

    public void Dispose()
    {
        try { ephemeral.Dispose(); }
        finally { source.Dispose(); }
    }
}
