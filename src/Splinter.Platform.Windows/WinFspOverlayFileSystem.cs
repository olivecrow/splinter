using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Security.Cryptography;
using System.Text;
using Fsp;
using Microsoft.Win32.SafeHandles;
using Splinter.Core;
using Splinter.Runtime;
using FileInfo = Fsp.Interop.FileInfo;
using VolumeInfo = Fsp.Interop.VolumeInfo;

namespace Splinter.Platform.Windows;

public sealed partial class WinFspOverlayFileSystem : FileSystemBase
{
    private readonly IWorkspaceDeltaStore store;
    private readonly PartitionedWorkspaceDeltaStore? partitioned;
    private readonly bool enableKernelCaching;
    private readonly byte[] security;
    private readonly Action<Exception>? report;
    private readonly IOverlayMutationObserver? mutationObserver;
    private readonly object mutationGate = new();
    private readonly Dictionary<Guid, int> retainedContexts = [];
    private readonly object ephemeralHandleGate = new();
    private readonly Dictionary<Guid, int> ephemeralRetained = [];
    private readonly ConcurrentDictionary<string, long> dirtyKernelSourcePaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<HandleContext, string> openHandlePaths = new();
    private long dirtyKernelEpoch;
    internal string[] SampleOpenPaths(int max = 16) => openHandlePaths.Values.Take(max).ToArray();
    internal int DirtyKernelSourceCount => dirtyKernelSourcePaths.Count;
    internal bool HasOnlyRootDirectoryHandles()
    {
        if (OpenHandles <= 0) return false;
        var snapshot = openHandlePaths.Values.ToArray();
        return snapshot.Length > 0 && snapshot.All(path => path.Length == 0);
    }
    private int CountOpenHandles(string relativePath)
    {
        int count = 0;
        foreach (string openPath in openHandlePaths.Values)
            if (openPath.Equals(relativePath, StringComparison.OrdinalIgnoreCase)) count++;
        return count;
    }
    private int handles;
    private int contexts;
    private sealed class HandleContext
    {
        public int Cleaned;
        public bool IsEphemeral;
        internal readonly object DirectoryGate = new();
        internal DirectoryScan? DirectoryReader;
        internal void DisposeReaders()
        {
            lock (DirectoryGate)
            {
                var reader = DirectoryReader; DirectoryReader = null;
                reader?.Dispose();
            }
        }
    }
    private sealed record DirectoryListingItem(string Name, DeltaEntry Entry);
    private sealed class DirectoryScan(IEnumerator<DirectoryListingItem> reader, IDisposable? membership,
        string? pattern, string? marker) : IDisposable
    {
        internal readonly string? Pattern = pattern;
        internal string? PreviousName = marker;
        internal DirectoryListingItem? Current;
        internal bool Completed;
        private int disposed;
        internal bool Advance(out DirectoryListingItem? item)
        {
            item = null;
            if (Completed) return false;
            ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
            if (!reader.MoveNext()) { Completed = true; Dispose(); return false; }
            PreviousName = Current?.Name ?? PreviousName;
            item = Current = reader.Current; return true;
        }
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            try { reader.Dispose(); } finally { membership?.Dispose(); }
        }
    }
    // FileSystemBase creates a fresh Context for every native output packet.
    // Its final callback can expose an entry which AddDirInfo cannot fit. The
    // next packet's marker acknowledges the preceding entry, so replay that
    // one lookahead before advancing the handle-owned, fixed-root cursor.
    private sealed class DirectoryPacket(DirectoryScan scan, bool replay)
    {
        internal readonly DirectoryScan Scan = scan;
        private bool first = true;
        internal bool Read(out DirectoryListingItem? item)
        {
            if (first) { first = false; if (replay) { item = Scan.Current; return true; } }
            return Scan.Advance(out item);
        }
    }
    public int OpenHandles => Volatile.Read(ref handles);
    public int RetainedFileContexts => Volatile.Read(ref contexts);
    public WinFspOverlayFileSystem(IWorkspaceDeltaStore store, Action<Exception>? report = null,
        IOverlayMutationObserver? mutationObserver = null)
        : this(store, report, mutationObserver, null, false) { }
    internal WinFspOverlayFileSystem(IWorkspaceDeltaStore store, Action<Exception>? report,
        IOverlayMutationObserver? mutationObserver, WriteTelemetry? writeTelemetry, bool enableKernelCaching = false,
        string? kernelFlushRecoveryPath = null)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        WindowsSnapshotPolicy.Require(store.Identity.Snapshot);
        this.store = store; partitioned = store as PartitionedWorkspaceDeltaStore;
        this.enableKernelCaching = enableKernelCaching;
        this.kernelFlushRecoveryPath = kernelFlushRecoveryPath;
        if (kernelFlushRecoveryPath is not null && File.Exists(kernelFlushRecoveryPath))
        {
            pendingReadOnly = WindowsProcessWorkspaceProvider.ReadAsync<ReadOnlyIntent>(kernelFlushRecoveryPath, default, false)
                .GetAwaiter().GetResult() ?? throw FlushFailure("", "unreadable read-only restore intent");
            RestorePendingReadOnly(mountPath: null);
        }
        this.report = report; this.mutationObserver = mutationObserver;
        this.writeTelemetry = writeTelemetry;
        string sid = WindowsIdentity.GetCurrent().User!.Value;
        var descriptor = new RawSecurityDescriptor($"O:{sid}G:{sid}D:P(A;;FA;;;SY)(A;;FA;;;{sid})");
        security = new byte[descriptor.BinaryLength]; descriptor.GetBinaryForm(security, 0);
    }
    public override int Init(object hostObject)
    {
        var host = (FileSystemHost)hostObject;
        host.SectorSize = 512; host.SectorsPerAllocationUnit = 8;
        host.VolumeCreationTime = (ulong)store.Identity.Snapshot.CapturedAt.UtcDateTime.ToFileTimeUtc();
        host.VolumeSerialNumber = BitConverter.ToUInt32(SHA256.HashData(Encoding.UTF8.GetBytes(store.Identity.Id)), 0);
        host.CaseSensitiveSearch = false; host.CasePreservedNames = true; host.UnicodeOnDisk = true;
        host.PersistentAcls = false; host.ReparsePoints = false; host.NamedStreams = false;
        host.PostCleanupWhenModifiedOnly = false; host.PostDispositionWhenNecessaryOnly = true;
        host.PassQueryDirectoryFileName = true;
        if (enableKernelCaching)
        {
            host.FileInfoTimeout = 10000;
            host.VolumeInfoTimeout = 5000;
            host.DirInfoTimeout = 10000;
            host.SecurityTimeout = 10000;
            host.FlushAndPurgeOnCleanup = false;
        }
        else
        {
            host.FlushAndPurgeOnCleanup = true;
        }
        host.FileSystemName = "SplinterSO";
        return STATUS_SUCCESS;
    }
    private void MarkDirtyKernelSource(string? rawPath)
    {
        if (!enableKernelCaching || string.IsNullOrEmpty(rawPath)) return;
        string normalized = rawPath.TrimStart('\\', '/').Replace('\\', '/');
        if (normalized.Length == 0 || partitioned?.IsEphemeralPath(normalized) == true) return;
        dirtyKernelSourcePaths[normalized] = Interlocked.Increment(ref dirtyKernelEpoch);
    }
    internal void FlushPendingKernelWrites(string mountPath)
    {
        if (!enableKernelCaching) return;
        lock (kernelFlushGate)
        {
            partitioned?.RecoverPendingMove();
            if (pendingReadOnly is not null) RestorePendingReadOnly(mountPath);
            if (dirtyKernelSourcePaths.IsEmpty) return;
            foreach (var (relative, epoch) in dirtyKernelSourcePaths.ToArray())
            {
                var entry = store.Stat(relative);
                if (entry is null || entry.Directory)
                {
                    dirtyKernelSourcePaths.TryRemove(KeyValuePair.Create(relative, epoch));
                    continue;
                }
                string path = Path.Combine(mountPath, relative.Replace('/', Path.DirectorySeparatorChar));
                long? completed;
                using (var data = OpenFlushHandle(path))
                {
                    int error = data.IsInvalid ? Marshal.GetLastWin32Error() : 0;
                    completed = error == 5 && (entry.Attributes & (uint)FileAttributes.ReadOnly) != 0
                        ? FlushReadOnly(path, relative, entry, epoch)
                        : FlushData(data, error, relative, epoch, ownHandles: 1);
                }
                // A failed attribute restoration exits before this removal. Newer epochs and
                // other open writers keep the file dirty for the next capture.
                if (completed.HasValue) dirtyKernelSourcePaths.TryRemove(KeyValuePair.Create(relative, completed.Value));
            }
        }
    }
    private sealed record ReadOnlyIntent(string Owner, Guid Id, string Path);
    private readonly object kernelFlushGate = new();
    private readonly string? kernelFlushRecoveryPath;
    private ReadOnlyIntent? pendingReadOnly;
    private static SafeFileHandle OpenFlushHandle(string path) =>
        CreateFileW(path, 2u, 7u, 0, 3u, 0x02000000u, 0);

    private long? FlushData(SafeFileHandle handle, int error, string relative, long epoch, int ownHandles)
    {
        if (handle.IsInvalid)
        {
            if (error is 2 or 3 && store.Stat(relative) is null) return epoch;
            throw FlushFailure(relative, $"open failed with Win32 error {error}");
        }
        long target = dirtyKernelSourcePaths.GetValueOrDefault(relative, epoch);
        if (!FlushFileBuffers(handle)) throw FlushFailure(relative, $"FlushFileBuffers failed with Win32 error {Marshal.GetLastWin32Error()}");
        return CountOpenHandles(relative) <= ownHandles ? target : null;
    }

    private long? FlushReadOnly(string path, string relative, DeltaEntry entry, long epoch)
    {
        // Keep the same leaf pinned against replacement while the kernel requires write access.
        using var attributes = CreateFileW(path, 0x180u, 3u, 0, 3u, 0x02000000u, 0);
        if (attributes.IsInvalid || store.Stat(relative)?.Id != entry.Id) throw FlushFailure(relative, "cannot pin the read-only file");
        var intent = new ReadOnlyIntent(store.Identity.Id, entry.Id, relative);
        if (kernelFlushRecoveryPath is not null)
            WindowsProcessWorkspaceProvider.WriteAsync(kernelFlushRecoveryPath, intent).GetAwaiter().GetResult();
        pendingReadOnly = intent;
        try
        {
            var writable = File.GetAttributes(attributes) & ~FileAttributes.ReadOnly;
            File.SetAttributes(attributes, writable == 0 ? FileAttributes.Normal : writable);
            using var data = OpenFlushHandle(path);
            int error = data.IsInvalid ? Marshal.GetLastWin32Error() : 0;
            return FlushData(data, error, relative, epoch, ownHandles: 2);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { throw FlushFailure(relative, ex.Message); }
        finally
        {
            RestoreReadOnly(attributes, relative);
            ClearReadOnlyIntent();
        }
    }
    private static WorkspaceException FlushFailure(string relative, string error) =>
        new("OverlayFlushFailed", $"Cannot flush dirty source file '{relative}': {error}.");

    private void RestoreReadOnly(SafeFileHandle attributes, string relative)
    {
        try
        {
            var current = File.GetAttributes(attributes);
            if ((current & FileAttributes.ReadOnly) == 0)
            {
                Visit("callback.before-effect", "restore-kernel-readonly", relative, 0, 0);
                File.SetAttributes(attributes, current | FileAttributes.ReadOnly);
            }
            if ((store.Stat(relative)?.Attributes & (uint)FileAttributes.ReadOnly) != (uint)FileAttributes.ReadOnly)
                throw FlushFailure(relative, "read-only metadata was not restored");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { throw FlushFailure(relative, ex.Message); }
    }
    private void ClearReadOnlyIntent()
    {
        if (kernelFlushRecoveryPath is not null) File.Delete(kernelFlushRecoveryPath);
        pendingReadOnly = null;
    }
    private void RestorePendingReadOnly(string? mountPath)
    {
        var intent = pendingReadOnly!;
        if (intent.Owner != store.Identity.Id) throw FlushFailure(intent.Path, "unrecognized restore intent");
        var entry = store.Get(intent.Id);
        if (!entry.Deleted)
        {
            if (mountPath is null)
                store.SetMetadata(entry.Id, entry.Attributes | (uint)FileAttributes.ReadOnly, entry.CreationTime, entry.LastAccessTime, entry.LastWriteTime);
            else
            {
                using var attrs = CreateFileW(Path.Combine(mountPath, entry.Path), 0x180u, 3u, 0, 3u, 0x02000000u, 0);
                if (attrs.IsInvalid || store.Stat(entry.Path)?.Id != entry.Id) throw FlushFailure(entry.Path, "cannot reacquire the original file");
                RestoreReadOnly(attrs, entry.Path);
            }
            MarkDirtyKernelSource(entry.Path);
        }
        ClearReadOnlyIntent();
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string lpFileName, uint dwDesiredAccess, uint dwShareMode,
        nint lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, nint hTemplateFile);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlushFileBuffers(SafeFileHandle hFile);
    public override int ExceptionHandler(Exception ex)
    {
        report?.Invoke(ex);
        return ex switch
        {
            FileNotFoundException => STATUS_OBJECT_NAME_NOT_FOUND,
            DirectoryNotFoundException => STATUS_OBJECT_PATH_NOT_FOUND,
            UnauthorizedAccessException => STATUS_ACCESS_DENIED,
            ArgumentException => STATUS_OBJECT_NAME_INVALID,
            WorkspaceException { Code: "DeltaFull" or "OverlayMetadataLimit" } => STATUS_DISK_FULL,
            WorkspaceException { Code: "SnapshotMissing" or "SnapshotReadFailed" or "SnapshotObservationUnknown" or "SnapshotIdentityMismatch" } => STATUS_DEVICE_NOT_READY,
            WorkspaceException { Code: "SnapshotFileUnsupported" } => STATUS_NOT_SUPPORTED,
            _ => base.ExceptionHandler(ex)
        };
    }
    private FileInfo Info(Guid id) => Info(store.Get(id));
    private static FileInfo Info(DeltaEntry entry) => new()
    {
        FileAttributes = entry.Attributes, FileSize = (ulong)entry.Length,
        AllocationSize = ((ulong)entry.Length + 4095) / 4096 * 4096,
        CreationTime = (ulong)entry.CreationTime, LastAccessTime = (ulong)entry.LastAccessTime,
        LastWriteTime = (ulong)entry.LastWriteTime, ChangeTime = (ulong)entry.LastWriteTime,
        IndexNumber = BitConverter.ToUInt64(entry.Id.ToByteArray(), 0)
    };
    public override int GetVolumeInfo(out VolumeInfo volumeInfo)
    {
        if (store.Identity.Version >= 3)
        {
            string backing = store.BackingPath ?? throw new WorkspaceException("OverlayBackingUnavailable", "Observe the actual journal backing volume before reporting space.");
            var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(backing))!);
            volumeInfo = new() { TotalSize = checked((ulong)drive.TotalSize), FreeSize = checked((ulong)drive.AvailableFreeSpace) };
            volumeInfo.SetVolumeLabel("Splinter snapshot overlay"); return STATUS_SUCCESS;
        }
        long journalBytes = store.GetJournalBytes();
        volumeInfo = new() { TotalSize = (ulong)store.Identity.MaxJournalBytes,
            FreeSize = (ulong)Math.Max(0, store.Identity.MaxJournalBytes - journalBytes) };
        volumeInfo.SetVolumeLabel("Splinter snapshot overlay"); return STATUS_SUCCESS;
    }
    public override int GetSecurityByName(string fileName, out uint fileAttributes, ref byte[] securityDescriptor)
    {
        fileAttributes = 0;
        var entry = store.Stat(fileName); if (entry is null) return STATUS_OBJECT_NAME_NOT_FOUND;
        fileAttributes = entry.Attributes; if (securityDescriptor is not null) securityDescriptor = security;
        return STATUS_SUCCESS;
    }
    public override int GetSecurity(object fileNode, object fileDesc, ref byte[] securityDescriptor)
    { securityDescriptor = security; return STATUS_SUCCESS; }
    public override int Create(string fileName, uint createOptions, uint grantedAccess, uint fileAttributes,
        byte[] securityDescriptor, ulong allocationSize, out object fileNode, out object fileDesc,
        out FileInfo fileInfo, out string normalizedName)
    {
        fileNode = fileDesc = null!; fileInfo = default; normalizedName = null!;
        SnapshotInputChecks.RejectUnsupportedAttributes(fileAttributes, fileName);
        if (partitioned is not null && partitioned.IsEphemeralPath(fileName))
        {
            DeltaEntry? ephemeralCreated;
            HandleContext ephemeralContext;
            lock (ephemeralHandleGate)
            {
                ephemeralCreated = partitioned.TryCreateEphemeral(fileName, (createOptions & 1) != 0, fileAttributes);
                if (ephemeralCreated is null) return STATUS_OBJECT_NAME_COLLISION;
                ephemeralContext = RetainEphemeralLocked(ephemeralCreated.Id);
            }
            openHandlePaths[ephemeralContext] = ephemeralCreated.Path;
            Interlocked.Increment(ref handles); Interlocked.Increment(ref contexts);
            fileInfo = Info(ephemeralCreated); fileNode = ephemeralCreated.Id; fileDesc = ephemeralContext;
            normalizedName = "\\" + ephemeralCreated.Path.Replace('/', '\\'); return STATUS_SUCCESS;
        }
        DeltaEntry? created = null; HandleContext? context = null;
        fileInfo = SubmitMetadata("create", () =>
        {
            if (store.Stat(fileName) is not null) return default;
            created = store.Create(fileName, (createOptions & 1) != 0, fileAttributes); return Info(created);
        }, () =>
        {
            if (created is null) return;
            context = Retain(created.Id); openHandlePaths[context] = created.Path; Interlocked.Increment(ref handles); Interlocked.Increment(ref contexts);
        });
        if (created is null) return STATUS_OBJECT_NAME_COLLISION;
        if (!created.Directory) MarkDirtyKernelSource(created.Path);
        fileNode = created.Id; fileDesc = context!; normalizedName = "\\" + created.Path.Replace('/', '\\'); return STATUS_SUCCESS;
    }
    public override int Open(string fileName, uint createOptions, uint grantedAccess,
        out object fileNode, out object fileDesc, out FileInfo fileInfo, out string normalizedName)
    {
        fileNode = fileDesc = null!; fileInfo = default; normalizedName = null!;
        if (partitioned is not null && partitioned.IsEphemeralPath(fileName))
        {
            DeltaEntry? ephemeralEntry;
            HandleContext ephemeralContext;
            lock (ephemeralHandleGate)
            {
                ephemeralEntry = partitioned.StatEphemeral(fileName, retainForHandle: true);
                if (ephemeralEntry is null) return STATUS_OBJECT_NAME_NOT_FOUND;
                ephemeralContext = RetainEphemeralLocked(ephemeralEntry.Id);
            }
            openHandlePaths[ephemeralContext] = ephemeralEntry.Path;
            Interlocked.Increment(ref handles); Interlocked.Increment(ref contexts);
            fileInfo = Info(ephemeralEntry); fileNode = ephemeralEntry.Id; fileDesc = ephemeralContext;
            normalizedName = "\\" + ephemeralEntry.Path.Replace('/', '\\'); return STATUS_SUCCESS;
        }
        DeltaEntry? entry = null; HandleContext? context = null;
        if (!TryOpenIdle(fileName, out entry, out context, out fileInfo)) fileInfo = SubmitMetadata("open", () =>
        {
            entry = store.Stat(fileName); return entry is null ? default : Info(entry);
        }, () =>
        {
            if (entry is null) return;
            context = Retain(entry.Id); openHandlePaths[context] = entry.Path; Interlocked.Increment(ref handles); Interlocked.Increment(ref contexts);
        });
        if (entry is null) return STATUS_OBJECT_NAME_NOT_FOUND;
        if (context is not null) openHandlePaths[context] = entry.Path;
        if (!entry.Directory && (grantedAccess & 0x40000006u) != 0) MarkDirtyKernelSource(entry.Path);
        fileNode = entry.Id; fileDesc = context!; normalizedName = "\\" + entry.Path.Replace('/', '\\'); return STATUS_SUCCESS;
    }
    private HandleContext Retain(Guid id)
    { retainedContexts[id] = retainedContexts.GetValueOrDefault(id) + 1; return new(); }
    private HandleContext RetainEphemeralLocked(Guid id)
    {
        ephemeralRetained[id] = ephemeralRetained.GetValueOrDefault(id) + 1;
        return new() { IsEphemeral = true };
    }
    public override int Overwrite(object fileNode, object fileDesc, uint fileAttributes,
        bool replaceFileAttributes, ulong allocationSize, out FileInfo fileInfo)
    {
        Guid id = (Guid)fileNode;
        if (partitioned is not null && partitioned.IsEphemeralId(id))
        {
            var old = partitioned.Get(id);
            uint attributes = replaceFileAttributes ? fileAttributes : old.Attributes | fileAttributes;
            SnapshotInputChecks.RejectUnsupportedAttributes(attributes, old.Path);
            partitioned.SetLengthAndMetadata(id, 0, attributes,
                old.CreationTime, old.LastAccessTime, DateTime.UtcNow.ToFileTimeUtc());
            fileInfo = Info(id);
            return STATUS_SUCCESS;
        }
        string? overwrittenPath = null;
        fileInfo = SubmitMetadata("overwrite", () =>
        {
            var old = store.Get(id); overwrittenPath = old.Path;
            uint attributes = replaceFileAttributes ? fileAttributes : old.Attributes | fileAttributes;
            SnapshotInputChecks.RejectUnsupportedAttributes(attributes, old.Path);
            store.SetLengthAndMetadata(id, 0, attributes,
                old.CreationTime, old.LastAccessTime, DateTime.UtcNow.ToFileTimeUtc());
            return Info(id);
        });
        MarkDirtyKernelSource(overwrittenPath);
        return STATUS_SUCCESS;
    }
    public override void Cleanup(object fileNode, object fileDesc, string fileName, uint flags)
    {
        // Directory readers end with Cleanup; the file identity remains held
        // until Close serializes unlink reclamation.
        var handle = (HandleContext)fileDesc;
        try
        {
            Guid id = (Guid)fileNode;
            if (partitioned is not null && partitioned.IsEphemeralId(id))
            {
                if ((flags & CleanupDelete) != 0) partitioned.Delete(id);
            }
            else
            {
                if ((flags & CleanupDelete) != 0) { using (EnterMutation("cleanup-delete")) store.Delete(id); }
                else if ((flags & (CleanupSetAllocationSize | CleanupSetArchiveBit | CleanupSetLastWriteTime)) != 0)
                    MarkDirtyKernelSource(fileName);
            }
        }
        finally
        {
            try { handle.DisposeReaders(); }
            finally { if (Interlocked.Exchange(ref handle.Cleaned, 1) == 0) { openHandlePaths.TryRemove(handle, out _); Interlocked.Decrement(ref handles); } }
        }
    }
    public override void Close(object fileNode, object fileDesc)
    {
        var handle = (HandleContext)fileDesc;
        if (Interlocked.Exchange(ref handle.Cleaned, 1) == 0) { openHandlePaths.TryRemove(handle, out _); Interlocked.Decrement(ref handles); }
        try
        {
            if (handle.IsEphemeral)
            {
                try { handle.DisposeReaders(); }
                finally { ReleaseEphemeral((Guid)fileNode, closingHandle: true); }
            }
            else
            {
                using (EnterMutation("close"))
                {
                    try { handle.DisposeReaders(); }
                    finally
                    {
                        Guid id = (Guid)fileNode;
                        if (retainedContexts[id] > 1) retainedContexts[id]--;
                        else { retainedContexts.Remove(id); ReleaseClosed(id); }
                    }
                }
            }
        }
        finally { Interlocked.Decrement(ref contexts); }
    }
    private bool IsRetained(Guid id) => retainedContexts.ContainsKey(id) || ephemeralRetained.ContainsKey(id);

    private void ReleaseEphemeral(Guid id, bool closingHandle)
    {
        lock (ephemeralHandleGate)
        {
            if (closingHandle)
            {
                if (ephemeralRetained[id] > 1) { ephemeralRetained[id]--; return; }
                ephemeralRetained.Remove(id);
            }
            else if (ephemeralRetained.ContainsKey(id)) return;

            // Reopening a basis file uses this same gate. Evict its cached identity before
            // the next open registers it. Moved handles also need the source retention map.
            if (!partitioned!.IsMoveHandle(id)) { partitioned.ReleaseUnlinkedFile(id); return; }
        }
        lock (mutationGate)
        lock (ephemeralHandleGate)
            if (!IsRetained(id)) partitioned.ReleaseUnlinkedFile(id, IsRetained);
    }

    private void ReleaseClosed(Guid id)
    {
        if (partitioned?.IsMoveHandle(id) == true)
        {
            lock (ephemeralHandleGate)
                if (!IsRetained(id)) partitioned.ReleaseUnlinkedFile(id, IsRetained);
        }
        else store.ReleaseUnlinkedFile(id);
    }
    public override unsafe int Read(object fileNode, object fileDesc, IntPtr buffer, ulong offset, uint length, out uint bytesTransferred)
    {
        bytesTransferred = 0;
        if (length == 0) return STATUS_SUCCESS;
        Guid id = (Guid)fileNode;
        uint maxTransfer = (uint)store.Identity.MaxTransferBytes;
        byte* ptr = (byte*)buffer;
        while (bytesTransferred < length)
        {
            int requested = (int)Math.Min(length - bytesTransferred, maxTransfer);
            int count = store.Read(id, checked((long)(offset + bytesTransferred)), new Span<byte>(ptr + bytesTransferred, requested));
            if (count == 0) break;
            bytesTransferred += (uint)count;
        }
        return bytesTransferred == 0 ? STATUS_END_OF_FILE : STATUS_SUCCESS;
    }
    public override unsafe int Write(object fileNode, object fileDesc, IntPtr buffer, ulong offset, uint length,
        bool writeToEndOfFile, bool constrainedIo, out uint bytesTransferred, out FileInfo fileInfo)
    {
        Guid id = (Guid)fileNode;
        if (partitioned is not null && partitioned.IsEphemeralId(id))
        {
            var data = length == 0 ? ReadOnlySpan<byte>.Empty : new ReadOnlySpan<byte>((void*)buffer, checked((int)length));
            var updated = partitioned.WriteEphemeral(id, offset, data, writeToEndOfFile, constrainedIo, out bytesTransferred);
            fileInfo = Info(updated);
            return STATUS_SUCCESS;
        }
        long entered = WriteClock();
        var request = new PendingWrite(id, buffer, offset, length, writeToEndOfFile, constrainedIo)
        { EnteredTicks = entered };
        bool succeeded = false;
        try
        {
            SubmitWrite(request);
            bytesTransferred = request.Written; fileInfo = request.Info;
            succeeded = true; return STATUS_SUCCESS;
        }
        finally { writeTelemetry?.Returned(request, WriteClock(), !succeeded); }
    }
    public override int Flush(object fileNode, object fileDesc, out FileInfo fileInfo)
    {
        if (partitioned is not null && fileNode is Guid ephemeralId && partitioned.IsEphemeralId(ephemeralId))
        {
            fileInfo = Info(ephemeralId);
            return STATUS_SUCCESS;
        }
        DrainAcceptedWrites();
        string path = mutationObserver is not null && fileNode is Guid node ? store.Get(node).Path : "";
        Visit("callback.before-effect", "flush", path, 0, 0);
        store.Flush(); fileInfo = fileNode is Guid id ? Info(id) : default;
        Visit("callback.after-effect", "flush", path, 0, 0);
        return STATUS_SUCCESS;
    }
    public override int GetFileInfo(object fileNode, object fileDesc, out FileInfo fileInfo)
    { fileInfo = Info((Guid)fileNode); return STATUS_SUCCESS; }
    public override int SetBasicInfo(object fileNode, object fileDesc, uint attributes, ulong creation,
        ulong access, ulong write, ulong change, out FileInfo fileInfo)
    {
        Guid id = (Guid)fileNode;
        if (partitioned is not null && partitioned.IsEphemeralId(id))
        {
            var old = partitioned.Get(id);
            uint updated = attributes == uint.MaxValue ? old.Attributes : attributes;
            SnapshotInputChecks.RejectUnsupportedAttributes(updated, old.Path);
            partitioned.SetMetadata(id, updated,
                creation == 0 ? old.CreationTime : (long)creation, access == 0 ? old.LastAccessTime : (long)access,
                write == 0 ? old.LastWriteTime : (long)write);
            fileInfo = Info(id);
            return STATUS_SUCCESS;
        }
        fileInfo = SubmitMetadata("set-basic-info", () =>
        {
            var old = store.Get(id);
            uint updated = attributes == uint.MaxValue ? old.Attributes : attributes;
            SnapshotInputChecks.RejectUnsupportedAttributes(updated, old.Path);
            store.SetMetadata(id, updated,
                creation == 0 ? old.CreationTime : (long)creation, access == 0 ? old.LastAccessTime : (long)access,
                write == 0 ? old.LastWriteTime : (long)write); return Info(id);
        });
        return STATUS_SUCCESS;
    }
    public override int SetFileSize(object fileNode, object fileDesc, ulong newSize, bool setAllocationSize, out FileInfo fileInfo)
    {
        Guid id = (Guid)fileNode;
        if (partitioned is not null && partitioned.IsEphemeralId(id))
        {
            if (!setAllocationSize || newSize < (ulong)partitioned.Get(id).Length)
                partitioned.SetLength(id, checked((long)newSize));
            fileInfo = Info(id);
            return STATUS_SUCCESS;
        }
        string? resizedPath = null;
        fileInfo = SubmitMetadata("set-file-size", () =>
        {
            var current = store.Get(id); resizedPath = current.Path;
            string path = mutationObserver is not null ? current.Path : "";
            if (mutationObserver is not null) Visit("callback.before-effect", "truncate", path, checked((long)newSize), 0);
            if (!setAllocationSize || newSize < (ulong)current.Length) store.SetLength(id, checked((long)newSize));
            var info = Info(id);
            if (mutationObserver is not null) Visit("callback.after-effect", "truncate", path, checked((long)newSize), checked((long)info.FileSize));
            return info;
        });
        MarkDirtyKernelSource(resizedPath);
        return STATUS_SUCCESS;
    }
    public override int CanDelete(object fileNode, object fileDesc, string fileName)
    {
        var entry = store.Get((Guid)fileNode);
        return entry.Directory && store.EnumerateChildren(entry.Path).Any() ? STATUS_DIRECTORY_NOT_EMPTY : STATUS_SUCCESS;
    }
    public override int Rename(object fileNode, object fileDesc, string fileName, string newFileName, bool replaceIfExists)
    {
        Guid id = (Guid)fileNode;
        if (partitioned is not null && partitioned.IsEphemeralId(id) && partitioned.IsEphemeralPath(newFileName))
        {
            var replaced = replaceIfExists ? partitioned.StatEphemeral(newFileName) : null;
            partitioned.RenameEphemeral(id, newFileName, replaceIfExists);
            if (replaced is not null && replaced.Id != id)
                ReleaseEphemeral(replaced.Id, closingHandle: false);
            return STATUS_SUCCESS;
        }
        using (EnterMutation("rename"))
        {
            string operation = replaceIfExists ? "replace" : "rename";
            string path = mutationObserver is not null ? store.Get(id).Path : "";
            Visit("callback.before-effect", operation, path, 0, 0);
            var replaced = replaceIfExists ? store.Stat(newFileName) : null;
            if (partitioned is not null && partitioned.IsEphemeralId(id) != partitioned.IsEphemeralPath(newFileName))
            {
                lock (ephemeralHandleGate) partitioned.Rename(id, newFileName, replaceIfExists, IsRetained);
            }
            else store.Rename(id, newFileName, replaceIfExists);
            if (replaced is not null && replaced.Id != id)
            {
                if (partitioned?.IsEphemeralPath(newFileName) == true) ReleaseEphemeral(replaced.Id, closingHandle: false);
                else if (!retainedContexts.ContainsKey(replaced.Id)) ReleaseClosed(replaced.Id);
            }
            if (fileDesc is HandleContext renamedHandle)
                openHandlePaths[renamedHandle] = store.Get(id).Path;
            MarkDirtyKernelSource(newFileName);
            Visit("callback.after-effect", operation, path, 0, 0);
            return STATUS_SUCCESS;
        }
    }
    private void Visit(string boundary, string operation, string path, long requested, long completed)
    { mutationObserver?.Observe(new(boundary, operation, path, requested, completed)); }
    public override int ReadDirectory(object fileNode, object fileDesc, string pattern, string marker,
        IntPtr buffer, uint length, out uint bytesTransferred)
    {
        if (fileDesc is not HandleContext handle)
            return base.ReadDirectory(fileNode, fileDesc, pattern, marker, buffer, length, out bytesTransferred);
        // Entry-level locking permits two packet loops on the same handle to
        // interleave. Keep the entire native packet and its lookahead atomic.
        lock (handle.DirectoryGate)
        {
            try { return base.ReadDirectory(fileNode, fileDesc, pattern, marker, buffer, length, out bytesTransferred); }
            catch { handle.DisposeReaders(); throw; }
        }
    }
    public override bool ReadDirectoryEntry(object fileNode, object fileDesc, string pattern, string marker,
        ref object context, out string fileName, out FileInfo fileInfo)
    {
        if (fileDesc is HandleContext handle)
        {
            lock (handle.DirectoryGate)
                return ReadDirectoryEntryCore(fileNode, handle, pattern, marker, ref context, out fileName, out fileInfo);
        }
        return ReadDirectoryEntryCore(fileNode, null, pattern, marker, ref context, out fileName, out fileInfo);
    }
    private bool ReadDirectoryEntryCore(object fileNode, HandleContext? handle, string pattern, string marker,
        ref object context, out string fileName, out FileInfo fileInfo)
    {
        DirectoryPacket? packet = context as DirectoryPacket;
        if (packet is null)
        {
            var prior = handle?.DirectoryReader;
            // A continuation can omit the search expression. Only an explicit
            // changed expression (or a restart marker) starts another scan.
            bool samePattern = prior is not null && (pattern is null || StringComparer.Ordinal.Equals(prior.Pattern, pattern));
            bool replay = samePattern && !prior!.Completed && prior.Current is not null && marker is not null &&
                StringComparer.OrdinalIgnoreCase.Equals(marker, prior.PreviousName);
            bool end = samePattern && prior!.Completed && marker is not null &&
                StringComparer.OrdinalIgnoreCase.Equals(marker, prior.Current?.Name ?? prior.PreviousName);
            if (replay || end) packet = new(prior!, replay);
            else
            {
            handle?.DisposeReaders();
            var entry = store.Get((Guid)fileNode);
            // Bind the provider's immutable membership before retaining this
            // callback context; consume it without copying a whole directory.
            var children = store.EnumerateChildren(entry.Path);
            DeltaEntry? parent;
            try { parent = entry.Path.Length == 0 ? null : store.Stat(Parent(entry.Path)); }
            catch { (children as IDisposable)?.Dispose(); throw; }
            IEnumerable<DirectoryListingItem> Listing()
            {
                if (entry.Path.Length != 0)
                {
                    yield return new(".", entry);
                    yield return new("..", parent!);
                }
                foreach (var child in children) yield return new(Name(child.Path), child);
            }
            bool AfterMarker(DirectoryListingItem item)
            {
                if (marker is null) return true;
                // Dot entries precede every child, including legal names
                // such as !asset and $data which sort before a period.
                if (entry.Path.Length != 0 && (marker is "." or ".."))
                    return item.Name != "." && (marker == "." || item.Name != "..");
                return StringComparer.OrdinalIgnoreCase.Compare(item.Name, marker) > 0;
            }
            var iterator = Listing().Where(AfterMarker).GetEnumerator();
            var scan = new DirectoryScan(iterator, children as IDisposable, pattern, marker);
            if (handle is not null) handle.DirectoryReader = scan;
            packet = new(scan, false);
            }
            context = packet;
        }
        try
        {
            if (packet.Read(out var item)) { fileName = item!.Name; fileInfo = Info(item.Entry); return true; }
        }
        catch { packet.Scan.Dispose(); if (handle?.DirectoryReader == packet.Scan) handle.DirectoryReader = null; throw; }
        fileName = null!; fileInfo = default; return false;
    }
    public override int GetDirInfoByName(object fileNode, object fileDesc, string fileName, out string normalizedName, out FileInfo fileInfo)
    {
        var parent = store.Get((Guid)fileNode);
        var entry = fileName switch
        {
            "." when parent.Path.Length != 0 => parent,
            ".." when parent.Path.Length != 0 => store.Stat(Parent(parent.Path)),
            _ => store.Stat(parent.Path + "/" + fileName)
        };
        normalizedName = fileName; fileInfo = default;
        if (entry is null) return STATUS_OBJECT_NAME_NOT_FOUND;
        normalizedName = fileName is "." or ".." ? fileName : Name(entry.Path);
        fileInfo = Info(entry); return STATUS_SUCCESS;
    }
    private static string Parent(string path) => path[..Math.Max(0, path.LastIndexOf('/'))];
    private static string Name(string path) => path[(path.LastIndexOf('/') + 1)..];
}
