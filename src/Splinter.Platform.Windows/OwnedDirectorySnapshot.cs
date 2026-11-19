using System.Security.Cryptography;
using System.Buffers;
using System.IO.Enumeration;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32.SafeHandles;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Platform.Windows;

public sealed record OwnedDirectoryItem(SnapshotEntry Entry, string? Hash);
public sealed record OwnedDirectoryProof(int Version, SnapshotReference Snapshot, string OwnerId, string ManifestHash, long LogicalBytes);

// The original has been renamed into an owned directory, and every existing
// file is fenced against writes/deletion before exposing a reader. Metadata is
// fixed in a manifest. No payload cache or mutable-project fallback exists.
public sealed class OwnedDirectorySnapshotTree : ISnapshotTree, IDisposable
{
    private sealed class LazyHandle(string path, SafeFileHandle handle)
    {
        public readonly string Path = path;
        public readonly SafeFileHandle Handle = handle;
        public int ActiveRefs;
        public bool Evicted;
        public LinkedListNode<LazyHandle>? LruNode;
    }

    private const int MaxLazyHandles = 512;
    internal static int DefaultWorkers => Math.Clamp(Environment.ProcessorCount, 2, 8);
    public const string ProviderName = "owned-directory";
    public SnapshotReference Snapshot { get; }
    // Pinned read-only handles and immutable indexes have no shared position.
    public bool SupportsConcurrentReads => true;
    private readonly OwnedDirectoryItemIndex? items;
    private readonly FastOwnedDirectoryIndex? fastIndex;
    private readonly Dictionary<string, SafeFileHandle> files = new(StringComparer.OrdinalIgnoreCase);
    private readonly object lazyPoolGate = new();
    private readonly Dictionary<string, LazyHandle> lazyHandlePool = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<LazyHandle> lazyHandleLru = [];
    private readonly object gate = new();
    private readonly Action? readAdmitted;
    private WindowsOwnedDirectoryLease? directoryLease;
    private WindowsDirectoryTreeProof? directoryProof;
    private bool closing, disposed, handingOff;
    private int closingInt;
    private int activeReads;
    private Exception? readFailure;
    public OwnedDirectoryManifest Manifest { get; }
    public OwnedDirectorySnapshotTree(OwnedDirectoryManifest manifest, SnapshotReference snapshot, bool verifyContent = true)
        : this(manifest, snapshot, verifyContent, null, DefaultWorkers) { }
    private readonly struct LazyTag;
    internal static OwnedDirectorySnapshotTree OpenLazy(OwnedDirectoryManifest manifest, SnapshotReference snapshot)
        => new(manifest, snapshot, default(LazyTag));
    private OwnedDirectorySnapshotTree(OwnedDirectoryManifest manifest, SnapshotReference snapshot, LazyTag _)
    {
        if (manifest.Version != 2 || manifest.Snapshot != snapshot || snapshot.Provider != ProviderName || !snapshot.Persistent || !snapshot.Differential ||
            snapshot.Consistency != "stopped-original-owned-directory" || manifest.FastIndexPath is null)
            throw new WorkspaceException("GeneratedBasisMismatch", "Preserve an unsupported or changed generated basis.");
        Manifest = manifest; Snapshot = snapshot;
        try
        {
            fastIndex = FastOwnedDirectoryIndex.Load(manifest.FastIndexPath, manifest.FastIndexHash);
            if (OperatingSystem.IsWindows())
                directoryLease = new(snapshot.RootPath);
        }
        catch { Dispose(); throw; }
    }
    internal OwnedDirectorySnapshotTree(OwnedDirectoryManifest manifest, SnapshotReference snapshot, bool verifyContent, Action? readAdmitted,
        int hashWorkers = 2, CancellationToken ct = default, Action<string>? beforeHash = null)
    {
        if (hashWorkers is < 1 or > 8) throw new ArgumentOutOfRangeException(nameof(hashWorkers));
        if (manifest.Version != 2 || manifest.Snapshot != snapshot || snapshot.Provider != ProviderName || !snapshot.Persistent || !snapshot.Differential ||
            snapshot.Consistency != "stopped-original-owned-directory")
            throw new WorkspaceException("GeneratedBasisMismatch", "Preserve an unsupported or changed generated basis.");
        Manifest = manifest; Snapshot = snapshot; this.readAdmitted = readAdmitted;
        items = manifest.OpenIndex(StringComparer.OrdinalIgnoreCase);
        try
        {
            items.Verify(ct);
            if (!items.ContainsKey("")) throw new WorkspaceException("GeneratedBasisCorrupt", "Invalid generated metadata root.");
            if (OperatingSystem.IsWindows())
            {
                directoryLease = new(snapshot.RootPath);
                directoryProof = new(snapshot.RootPath, directoryLease.RootHandle, metadataWrite: false);
                foreach (var item in items.Values.Where(i => i.Entry.Directory))
                { ct.ThrowIfCancellationRequested(); directoryProof.PinDirectory(item.Entry.Path); }
            }
            ParallelFiles(items.Values.Where(i => !i.Entry.Directory), item =>
                {
                    string path = Resolve(item.Entry.Path);
                    if (directoryProof is null && (File.GetAttributes(path) & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
                        throw new WorkspaceException("GeneratedBasisChanged", "Generated basis type or path changed.");
                    var handle = directoryProof?.ReadHandle(item.Entry.Path) ?? File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.RandomAccess);
                    try
                    {
                        if (RandomAccess.GetLength(handle) != item.Entry.Length)
                            throw new WorkspaceException("GeneratedBasisChanged", "Generated content differs from its frozen manifest.");
                        lock (gate) files.Add(item.Entry.Path, handle);
                    }
                    catch { handle.Dispose(); throw; }
                }, hashWorkers, ct);
                // Every file is fenced before hashing starts. Workers only read
                // the already-pinned directory map; ownership publishes once.
            if (directoryProof is null)
                foreach (var item in items.Values.Where(i => i.Entry.Directory))
                    if ((File.GetAttributes(Resolve(item.Entry.Path)) & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != FileAttributes.Directory)
                        throw new WorkspaceException("GeneratedBasisChanged", "Generated basis type or path changed.");
            if (verifyContent) VerifyContents(items.Values, hashWorkers, ct, beforeHash);
        }
        catch { Dispose(); throw; }
    }
    private void VerifyContents(IEnumerable<OwnedDirectoryItem> entries, int workers, CancellationToken ct, Action<string>? beforeHash)
    {
        void Verify(OwnedDirectoryItem item)
        {
            ct.ThrowIfCancellationRequested(); beforeHash?.Invoke(item.Entry.Path);
            if (Hash(files[item.Entry.Path], ct) != item.Hash)
                throw new WorkspaceException("GeneratedBasisChanged", "Generated content differs from its frozen manifest.");
        }
        ParallelFiles(entries.Where(i => !i.Entry.Directory), Verify, workers, ct);
    }
    internal static void ParallelFiles<T>(IEnumerable<T> entries, Action<T> action, int workers, CancellationToken ct)
    {
        try
        {
            if (workers == 1) foreach (var item in entries) { ct.ThrowIfCancellationRequested(); action(item); }
            else Parallel.ForEach(entries, new ParallelOptions { MaxDegreeOfParallelism = workers, CancellationToken = ct }, action);
        }
        // Parallel.ForEach joins every worker before leaving. Constructor
        // failure therefore closes handles only after all admitted hashes end.
        catch (AggregateException ex) { ExceptionDispatchInfo.Capture(ex.Flatten().InnerExceptions[0]).Throw(); throw; }
    }
    public static OwnedDirectoryManifest Capture(SnapshotReference snapshot, string owner, string? metadataDirectory = null) =>
        CaptureCancellable(snapshot, owner, default, metadataDirectory);
    internal static OwnedDirectoryManifest CaptureFastLibrary(SnapshotReference snapshot, string owner, string fastIndexPath, CancellationToken ct)
    {
        string rootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(snapshot.RootPath));
        WorkspaceStore.EnsurePlainPath(rootPath);
        using var lease = OperatingSystem.IsWindows() ? new WindowsOwnedDirectoryLease(rootPath) : null;
        var rootInfo = new DirectoryInfo(rootPath);
        SnapshotInputChecks.RejectUnsupportedAttributes((uint)rootInfo.Attributes, rootPath);
        var entries = new List<SnapshotEntry>(1024)
        {
            new("", true, 0, (uint)rootInfo.Attributes,
                rootInfo.CreationTimeUtc.ToFileTimeUtc(), rootInfo.LastAccessTimeUtc.ToFileTimeUtc(), rootInfo.LastWriteTimeUtc.ToFileTimeUtc())
        };
        int prefixLength = rootPath.Length + 1;
        var enumerable = new FileSystemEnumerable<SnapshotEntry>(rootPath,
            (ref FileSystemEntry entry) =>
            {
                ct.ThrowIfCancellationRequested();
                string fullPath = entry.ToFullPath();
                SnapshotInputChecks.RejectUnsupportedAttributes((uint)entry.Attributes, fullPath);
                string relative = fullPath[prefixLength..].Replace('\\', '/');
                return new SnapshotEntry(relative, entry.IsDirectory, entry.IsDirectory ? 0 : entry.Length, (uint)entry.Attributes,
                    entry.CreationTimeUtc.ToFileTime(), entry.LastAccessTimeUtc.ToFileTime(), entry.LastWriteTimeUtc.ToFileTime());
            },
            new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = false, AttributesToSkip = 0 });
        foreach (var entry in enumerable) entries.Add(entry);

        // Verify no active writer holds a Library file open, reject hardlinks/reparse points,
        // and confirm exact length and write timestamp on the opened handle.
        ParallelFiles(entries.Where(e => !e.Directory), entry =>
        {
            ct.ThrowIfCancellationRequested();
            string fullPath = Path.Combine(rootPath, entry.Path.Replace('/', Path.DirectorySeparatorChar));
            using var handle = File.OpenHandle(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            ValidateFastLibraryFileHandle(handle, entry, duringCapture: true);
        }, DefaultWorkers, ct);

        var (count, logicalBytes, hash) = FastOwnedDirectoryIndex.Write(fastIndexPath, entries);
        return new OwnedDirectoryManifest(2, snapshot, owner, FastOwnedDirectoryIndex.Enumerate(fastIndexPath, hash))
        {
            FastIndex = OwnedDirectoryManifest.FastIndexFileName,
            FastIndexHash = hash,
            FastIndexCount = count,
            FastIndexLogicalBytes = logicalBytes,
            FastIndexPath = fastIndexPath
        };
    }
    internal static OwnedDirectoryManifest CaptureCancellable(SnapshotReference snapshot, string owner, CancellationToken ct, string? metadataDirectory = null)
    {
        WorkspaceStore.EnsurePlainPath(snapshot.RootPath);
        using var lease = OperatingSystem.IsWindows() ? new WindowsOwnedDirectoryLease(snapshot.RootPath) : null;
        using var proof = lease is null ? null : new WindowsDirectoryTreeProof(snapshot.RootPath, lease.RootHandle, metadataWrite: false);
        TemporaryMetadataDirectory? scratchOwner = metadataDirectory is null ? new() : null;
        metadataDirectory ??= scratchOwner!.Path;
        SnapshotEntry Describe(string relative)
        {
            ct.ThrowIfCancellationRequested();
            string path = Path.Combine(snapshot.RootPath, relative.Replace('/', Path.DirectorySeparatorChar));
            if (proof is null) WorkspaceStore.EnsurePlainPath(path); else if (relative.Length != 0) proof.RequireParent(path);
            var attrs = File.GetAttributes(path);
            bool directory = (attrs & FileAttributes.Directory) != 0;
            if (directory) proof?.PinDirectory(relative);
            FileSystemInfo info = directory ? new DirectoryInfo(path) : new FileInfo(path);
            var entry = new SnapshotEntry(relative, directory, directory ? 0 : ((FileInfo)info).Length, (uint)attrs,
                info.CreationTimeUtc.ToFileTimeUtc(), info.LastAccessTimeUtc.ToFileTimeUtc(), info.LastWriteTimeUtc.ToFileTimeUtc());
            return entry;
        }
        var root = Describe("");
        IEnumerable<SnapshotEntry> Walk()
        {
            var pending = new Stack<IEnumerator<string>>(); pending.Push(Directory.EnumerateFileSystemEntries(snapshot.RootPath).GetEnumerator());
            try
            {
                while (pending.TryPeek(out var level))
                {
                    if (!level.MoveNext()) { level.Dispose(); pending.Pop(); continue; }
                    string path = level.Current, relative = Path.GetRelativePath(snapshot.RootPath, path).Replace('\\', '/');
                    var entry = Describe(relative); yield return entry;
                    if (entry.Directory) pending.Push(Directory.EnumerateFileSystemEntries(path).GetEnumerator());
                }
            }
            finally { foreach (var level in pending) level.Dispose(); }
        }
        OwnedDirectoryItem HashEntry(SnapshotEntry entry)
        {
            if (entry.Directory) return new(entry, null);
            string path = Path.Combine(snapshot.RootPath, entry.Path.Replace('/', Path.DirectorySeparatorChar));
            if (proof is null) WorkspaceStore.EnsurePlainPath(path);
            using var handle = proof?.ReadHandle(entry.Path) ?? File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (RandomAccess.GetLength(handle) != entry.Length) throw new WorkspaceException("GeneratedBasisChanged", "Generated content changed during capture.");
            return new(entry, Hash(handle, ct));
        }
        var catalog = PagedSnapshotCatalog.Build(metadataDirectory, root,
            BoundedHash(Walk(), HashEntry, ct).Select(item => new DirectoryMetadataRecord(item.Entry, item.Hash)), ct: ct);
        return new(2, snapshot, owner, OwnedDirectoryManifest.EnumerateItems(metadataDirectory, catalog))
        { Catalog = catalog, CatalogDirectory = metadataDirectory, ScratchOwner = scratchOwner };
    }
    private static IEnumerable<OwnedDirectoryItem> BoundedHash(IEnumerable<SnapshotEntry> source, Func<SnapshotEntry, OwnedDirectoryItem> hash, CancellationToken ct)
    {
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var results = new System.Collections.Concurrent.BlockingCollection<OwnedDirectoryItem>(32);
        Exception? failure = null;
        var worker = Task.Run(() =>
        {
            try { Parallel.ForEach(System.Collections.Concurrent.Partitioner.Create(source, System.Collections.Concurrent.EnumerablePartitionerOptions.NoBuffering),
                new ParallelOptions { MaxDegreeOfParallelism = DefaultWorkers, CancellationToken = cancel.Token },
                entry => results.Add(hash(entry), cancel.Token)); }
            catch (Exception ex) { failure = ex is AggregateException aggregate ? aggregate.Flatten().InnerExceptions[0] : ex; }
            finally { results.CompleteAdding(); }
        }, CancellationToken.None);
        try { foreach (var result in results.GetConsumingEnumerable(ct)) yield return result; }
        finally { cancel.Cancel(); worker.GetAwaiter().GetResult(); }
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
    public static string Hash(SafeFileHandle handle, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); byte[] buffer = ArrayPool<byte>.Shared.Rent(65536); long offset = 0;
        try
        {
            for (int read; (read = RandomAccess.Read(handle, buffer.AsSpan(0, 65536), offset)) != 0; offset += read)
            { ct.ThrowIfCancellationRequested(); hash.AppendData(buffer, 0, read); }
            return Convert.ToHexStringLower(hash.GetHashAndReset());
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }
    private void Check()
    {
        ObjectDisposedException.ThrowIf(closing || disposed, this);
        if (readFailure is not null) throw new WorkspaceException("SnapshotReadFailed", "Preserve the generated basis after a failed read or root handoff.");
        if (handingOff) throw new WorkspaceException("SnapshotReadFailed", "The generated root is being handed to restoration.");
        if (directoryLease is not null) directoryLease.EnsureAvailable();
        else if (!Directory.Exists(Snapshot.RootPath)) throw new WorkspaceException("SnapshotMissing", "Retain the missing generated basis.");
    }
    private void CheckFast()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref closingInt) != 0 || disposed, this);
        if (Volatile.Read(ref readFailure) is not null) throw new WorkspaceException("SnapshotReadFailed", "Preserve the generated basis after a failed read or root handoff.");
        if (directoryLease is not null) directoryLease.EnsureAvailable();
        else if (!Directory.Exists(Snapshot.RootPath)) throw new WorkspaceException("SnapshotMissing", "Retain the missing generated basis.");
    }
    public void EnsureAvailable()
    {
        if (fastIndex is not null) { CheckFast(); return; }
        lock (gate) Check();
    }
    public SnapshotEntry? Stat(string path)
    {
        if (fastIndex is not null) { CheckFast(); return fastIndex.Find(JournalDeltaStore.Normalize(path)); }
        lock (gate) { Check(); return items!.GetValueOrDefault(JournalDeltaStore.Normalize(path))?.Entry; }
    }
    public IReadOnlyList<SnapshotEntry> List(string path)
    {
        if (fastIndex is not null)
        {
            CheckFast(); path = JournalDeltaStore.Normalize(path);
            if (fastIndex.Find(path) is not { Directory: true }) throw new DirectoryNotFoundException(path);
            return fastIndex.EnumerateChildren(path);
        }
        lock (gate)
        {
            Check(); path = JournalDeltaStore.Normalize(path);
            if (items!.GetValueOrDefault(path)?.Entry is not { Directory: true }) throw new DirectoryNotFoundException(path);
            return items.EnumerateChildren(path).Select(i => i.Entry).ToArray();
        }
    }
    public IEnumerable<SnapshotEntry> EnumerateChildren(string path)
    {
        if (fastIndex is not null) { CheckFast(); return fastIndex.EnumerateChildren(JournalDeltaStore.Normalize(path)); }
        lock (gate) { Check(); return items!.EnumerateChildren(JournalDeltaStore.Normalize(path)).Select(item => item.Entry); }
    }
    public int Read(string path, long offset, Span<byte> destination)
    {
        if (fastIndex is not null) return ReadLazy(path, offset, destination);
        SafeFileHandle handle;
        lock (gate) { Check(); handle = files[JournalDeltaStore.Normalize(path)]; activeReads++; }
        try
        {
            readAdmitted?.Invoke();
            int read = RandomAccess.Read(handle, destination, offset);
            lock (gate)
            {
                // An admitted read can finish while Dispose waits, but another
                // concurrent I/O failure invalidates its completion proof.
                if (readFailure is not null) throw new WorkspaceException("SnapshotReadFailed", "A concurrent generated-basis read failed.");
                directoryLease?.EnsureAvailable();
            }
            return read;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or WorkspaceException)
        { lock (gate) readFailure ??= ex; throw; }
        finally { lock (gate) { activeReads--; if (closing || handingOff) Monitor.PulseAll(gate); } }
    }
    private int ReadLazy(string path, long offset, Span<byte> destination)
    {
        string normalized = JournalDeltaStore.Normalize(path);
        CheckFast();
        Interlocked.Increment(ref activeReads);
        try
        {
            CheckFast();
            var pooled = AcquireLazyHandle(normalized);
            try
            {
                int read = RandomAccess.Read(pooled.Handle, destination, offset);
                directoryLease?.EnsureAvailable();
                return read;
            }
            finally { ReleaseLazyHandle(pooled); }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or WorkspaceException)
        { Interlocked.CompareExchange(ref readFailure, ex, null); throw; }
        finally
        {
            if (Interlocked.Decrement(ref activeReads) == 0 && Volatile.Read(ref closingInt) != 0)
                lock (gate) Monitor.PulseAll(gate);
        }
    }
    private LazyHandle AcquireLazyHandle(string normalizedPath)
    {
        LazyHandle? toDispose = null;
        try
        {
            lock (lazyPoolGate)
            {
                if (lazyHandlePool.TryGetValue(normalizedPath, out var existing))
                {
                    lazyHandleLru.Remove(existing.LruNode!);
                    lazyHandleLru.AddLast(existing.LruNode!);
                    existing.ActiveRefs++;
                    return existing;
                }
            }

            var entry = fastIndex!.Find(normalizedPath) ?? throw new FileNotFoundException(normalizedPath);
            if (entry.Directory) throw new IOException("Cannot read a directory.");
            string fullPath = Path.Combine(Snapshot.RootPath, normalizedPath.Replace('/', Path.DirectorySeparatorChar));
            var handle = File.OpenHandle(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.RandomAccess);
            try
            {
                ValidateFastLibraryFileHandle(handle, entry, duringCapture: false);
            }
            catch { handle.Dispose(); throw; }

            lock (lazyPoolGate)
            {
                if (lazyHandlePool.TryGetValue(normalizedPath, out var raceWinner))
                {
                    handle.Dispose();
                    lazyHandleLru.Remove(raceWinner.LruNode!);
                    lazyHandleLru.AddLast(raceWinner.LruNode!);
                    raceWinner.ActiveRefs++;
                    return raceWinner;
                }
                if (lazyHandlePool.Count >= MaxLazyHandles)
                {
                    var victim = lazyHandleLru.First;
                    while (victim is not null && victim.Value.ActiveRefs > 0) victim = victim.Next;
                    if (victim is not null)
                    {
                        lazyHandleLru.Remove(victim);
                        lazyHandlePool.Remove(victim.Value.Path);
                        victim.Value.Evicted = true;
                        toDispose = victim.Value;
                    }
                }
                var created = new LazyHandle(normalizedPath, handle) { ActiveRefs = 1 };
                created.LruNode = lazyHandleLru.AddLast(created);
                lazyHandlePool[normalizedPath] = created;
                return created;
            }
        }
        finally { toDispose?.Handle.Dispose(); }
    }
    private static void ValidateFastLibraryFileHandle(SafeFileHandle handle, SnapshotEntry entry, bool duringCapture)
    {
        if (OperatingSystem.IsWindows())
        {
            if (!GetFileInformationByHandle(handle, out var info))
                throw new WorkspaceException("GeneratedBasisChanged", "Cannot inspect the generated basis file handle.");
            if ((info.Attributes & 0x400) != 0 || (info.Attributes & 0x10) != 0)
                throw new WorkspaceException("SnapshotFileUnsupported", $"Generated basis entry '{entry.Path}' must be an ordinary file.");
            if (info.Links > 1)
                throw new WorkspaceException("SnapshotFileUnsupported", $"Hardlinked file '{entry.Path}' is unsupported in the shared generated basis.");
            long actualLength = ((long)info.SizeHigh << 32) | info.SizeLow;
            long actualWriteTime = ((long)info.WriteHigh << 32) | info.WriteLow;
            if (actualLength != entry.Length || actualWriteTime != entry.LastWriteTime)
                throw new WorkspaceException("GeneratedBasisChanged",
                    duringCapture ? "Generated content changed during capture." : "Generated content differs from its frozen manifest.");
            return;
        }
        if (RandomAccess.GetLength(handle) != entry.Length)
            throw new WorkspaceException("GeneratedBasisChanged",
                duringCapture ? "Generated content changed during capture." : "Generated content differs from its frozen manifest.");
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    { public uint Attributes, CreationLow, CreationHigh, AccessLow, AccessHigh, WriteLow, WriteHigh, VolumeSerial, SizeHigh, SizeLow, Links, IndexHigh, IndexLow; }
    [DllImport("kernel32", SetLastError = true)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle file, out FileInformation info);
    private void ReleaseLazyHandle(LazyHandle entry)
    {
        bool shouldDispose = false;
        lock (lazyPoolGate)
        {
            entry.ActiveRefs--;
            if (entry.Evicted && entry.ActiveRefs == 0) shouldDispose = true;
        }
        if (shouldDispose) entry.Handle.Dispose();
    }
    private string Resolve(string path)
    {
        if (directoryProof is not null) return directoryProof.Resolve(path);
        if (JournalDeltaStore.Normalize(path) != path) throw new WorkspaceException("GeneratedBasisCorrupt", "Invalid manifest path.");
        string result = Path.Combine(Snapshot.RootPath, path.Replace('/', Path.DirectorySeparatorChar)); WorkspaceStore.EnsurePlainPath(result); return result;
    }
    internal SafeFileHandle TransferRootToRestore(Action? rootUnpinned = null, bool allowChildMutations = false)
    {
        lock (gate)
        {
            Check();
            if (directoryLease is null) throw new PlatformNotSupportedException();
            handingOff = true;
            try
            {
                while (activeReads != 0) Monitor.Wait(gate);
                ObjectDisposedException.ThrowIf(closing || disposed, this);
                if (readFailure is not null) throw new WorkspaceException("SnapshotReadFailed", "A generated-basis read failed before restoration.");
                return directoryLease.TransferToRestore(rootUnpinned, allowChildMutations);
            }
            catch (Exception ex) { readFailure ??= ex; throw; }
            finally { handingOff = false; Monitor.PulseAll(gate); }
        }
    }
    internal string RestoreIdentity(string path)
    {
        lock (gate)
        {
            Check();
            return files.TryGetValue(path, out var file) ? SnapshotInputChecks.FileIdentity(file)
                : SnapshotInputChecks.FileIdentity(directoryProof!.PinDirectory(path), directory: true);
        }
    }
    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            closing = true; Volatile.Write(ref closingInt, 1);
            while (Volatile.Read(ref activeReads) != 0 || handingOff) Monitor.Wait(gate);
            if (disposed) return;
            foreach (var file in files.Values) file.Dispose();
            files.Clear();
            lock (lazyPoolGate)
            {
                foreach (var entry in lazyHandlePool.Values) entry.Handle.Dispose();
                lazyHandlePool.Clear();
                lazyHandleLru.Clear();
            }
            directoryProof?.Dispose(); directoryLease?.Dispose(); items?.Dispose(); disposed = true;
            Monitor.PulseAll(gate);
        }
    }
    private static string Parent(string path) { int slash = path.LastIndexOf('/'); return slash < 0 ? "" : path[..slash]; }
}

public sealed class OwnedDirectorySnapshotProvider(string directory, string owner) : ISourceSnapshotProvider
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public OwnedDirectoryManifest Read(SnapshotReference snapshot)
    {
        WorkspaceStore.EnsurePlainPath(directory);
        var manifest = JsonSerializer.Deserialize<OwnedDirectoryManifest>(File.ReadAllText(Path.Combine(directory, "manifest.json")), Json)
            ?? throw new WorkspaceException("GeneratedBasisCorrupt", "Missing generated manifest.");
        manifest = manifest.Bind(directory);
        if (manifest.Snapshot != snapshot || manifest.OwnerId != owner) throw new WorkspaceException("GeneratedBasisMismatch", "Generated owner/reference mismatch.");
        if (ManagedDirectoryBasis.HashFile(Path.Combine(directory, "manifest.json")) != Proof(snapshot).ManifestHash)
            throw new WorkspaceException("GeneratedBasisChanged", "Retain a changed generated manifest.");
        return manifest;
    }
    private OwnedDirectoryProof Proof(SnapshotReference snapshot)
    {
        var proof = JsonSerializer.Deserialize<OwnedDirectoryProof>(File.ReadAllText(Path.Combine(directory, "proof.json")), Json);
        if (proof is null || proof.Version != 1 || proof.Snapshot != snapshot || proof.OwnerId != owner || proof.ManifestHash.Length != 64)
            throw new WorkspaceException("GeneratedBasisMismatch", "Retain a mismatched generated proof.");
        return proof;
    }
    public IDisposable AcquireReadLease() => new ReadLease(directory);
    private sealed class ReadLease : IDisposable
    {
        private readonly List<FileStream> handles = [];
        public ReadLease(string directory)
        {
            try { foreach (string name in new[] { "readers.lock", "manifest.json", "proof.json" }) handles.Add(new(Path.Combine(directory, name), FileMode.Open, FileAccess.Read, FileShare.Read)); }
            catch { Dispose(); throw; }
        }
        public void Dispose() { foreach (var handle in handles) handle.Dispose(); handles.Clear(); }
    }
    public Task<SnapshotReference> CaptureAsync(SnapshotCapture request, CancellationToken ct = default) => throw new WorkspaceException("GeneratedBasisPreparationRequired", "Use the explicit generated-directory preparation lifecycle.");
    public Task<SnapshotObservation> ObserveAsync(SnapshotReference snapshot, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested(); var proof = Proof(snapshot);
        if (!Directory.Exists(snapshot.RootPath)) return Task.FromResult(new SnapshotObservation(snapshot.Id, false, "SnapshotMissing", null, null));
        return Task.FromResult(new SnapshotObservation(snapshot.Id, true, null, proof.LogicalBytes, null) { ReadAccess = new(snapshot.Id, snapshot.RootPath, proof.ManifestHash) });
    }
    public async Task<SnapshotReadAccess> ResolveReadAccessAsync(SnapshotReference snapshot, CancellationToken ct = default) =>
        (await ObserveAsync(snapshot, ct)).ReadAccess ?? throw new WorkspaceException("SnapshotMissing", "The original generated basis is missing.");
    public Task<SnapshotReclamation> ReleaseAsync(SnapshotReference snapshot, CancellationToken ct = default, string? previousAttemptId = null) =>
        throw new WorkspaceException("GeneratedBasisRestoreRequired", "Restore the original through its explicit lifecycle after all dependents are absent.");
}
