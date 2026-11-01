using System.Buffers;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Security.Cryptography;
using System.Text;
using Fsp;
using Splinter.Core;
using FileInfo = Fsp.Interop.FileInfo;
using VolumeInfo = Fsp.Interop.VolumeInfo;

namespace Splinter.Platform.Windows;

public sealed partial class WinFspOverlayFileSystem : FileSystemBase
{
    private readonly IWorkspaceDeltaStore store;
    private readonly byte[] security;
    private readonly Action<Exception>? report;
    private readonly IOverlayMutationObserver? mutationObserver;
    private readonly object mutationGate = new();
    private readonly Dictionary<Guid, int> retainedContexts = [];
    private int handles;
    private int contexts;
    private sealed class HandleContext
    {
        public int Cleaned;
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
        : this(store, report, mutationObserver, null) { }
    internal WinFspOverlayFileSystem(IWorkspaceDeltaStore store, Action<Exception>? report,
        IOverlayMutationObserver? mutationObserver, WriteTelemetry? writeTelemetry)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        WindowsSnapshotPolicy.Require(store.Identity.Snapshot);
        this.store = store; this.report = report; this.mutationObserver = mutationObserver;
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
        host.FlushAndPurgeOnCleanup = true; host.PassQueryDirectoryFileName = true;
        host.FileSystemName = "SplinterSO";
        return STATUS_SUCCESS;
    }
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
        DeltaEntry? created = null; HandleContext? context = null;
        fileInfo = SubmitMetadata("create", () =>
        {
            if (store.Stat(fileName) is not null) return default;
            created = store.Create(fileName, (createOptions & 1) != 0, fileAttributes); return Info(created);
        }, () =>
        {
            if (created is null) return;
            context = Retain(created.Id); Interlocked.Increment(ref handles); Interlocked.Increment(ref contexts);
        });
        if (created is null) return STATUS_OBJECT_NAME_COLLISION;
        fileNode = created.Id; fileDesc = context!; normalizedName = "\\" + created.Path.Replace('/', '\\'); return STATUS_SUCCESS;
    }
    public override int Open(string fileName, uint createOptions, uint grantedAccess,
        out object fileNode, out object fileDesc, out FileInfo fileInfo, out string normalizedName)
    {
        using (EnterMutation("open"))
        {
            fileNode = null!; fileDesc = null!; fileInfo = default; normalizedName = null!;
            var entry = store.Stat(fileName); if (entry is null) return STATUS_OBJECT_NAME_NOT_FOUND;
            fileNode = entry.Id; fileDesc = Retain(entry.Id); fileInfo = Info(entry);
            normalizedName = "\\" + entry.Path.Replace('/', '\\'); Interlocked.Increment(ref handles); Interlocked.Increment(ref contexts); return STATUS_SUCCESS;
        }
    }
    private HandleContext Retain(Guid id)
    { retainedContexts[id] = retainedContexts.GetValueOrDefault(id) + 1; return new(); }
    public override int Overwrite(object fileNode, object fileDesc, uint fileAttributes,
        bool replaceFileAttributes, ulong allocationSize, out FileInfo fileInfo)
    {
        using (EnterMutation("overwrite"))
        {
            var id = (Guid)fileNode; var old = store.Get(id);
            uint attributes = replaceFileAttributes ? fileAttributes : old.Attributes | fileAttributes;
            SnapshotInputChecks.RejectUnsupportedAttributes(attributes, old.Path);
            store.SetLengthAndMetadata(id, 0, attributes,
                old.CreationTime, old.LastAccessTime, DateTime.UtcNow.ToFileTimeUtc());
            fileInfo = Info(id); return STATUS_SUCCESS;
        }
    }
    public override void Cleanup(object fileNode, object fileDesc, string fileName, uint flags)
    {
        // Directory readers end with Cleanup; the file identity remains held
        // until Close serializes unlink reclamation.
        try { if ((flags & CleanupDelete) != 0) { using (EnterMutation("cleanup-delete")) store.Delete((Guid)fileNode); } }
        finally
        {
            try { ((HandleContext)fileDesc).DisposeReaders(); }
            finally { if (Interlocked.Exchange(ref ((HandleContext)fileDesc).Cleaned, 1) == 0) Interlocked.Decrement(ref handles); }
        }
    }
    public override void Close(object fileNode, object fileDesc)
    {
        if (Interlocked.Exchange(ref ((HandleContext)fileDesc).Cleaned, 1) == 0) Interlocked.Decrement(ref handles);
        try
        {
            using (EnterMutation("close"))
            {
                try { ((HandleContext)fileDesc).DisposeReaders(); }
                finally
                {
                    Guid id = (Guid)fileNode;
                    if (retainedContexts[id] > 1) retainedContexts[id]--;
                    else { retainedContexts.Remove(id); store.ReleaseUnlinkedFile(id); }
                }
            }
        }
        finally { Interlocked.Decrement(ref contexts); }
    }
    public override int Read(object fileNode, object fileDesc, IntPtr buffer, ulong offset, uint length, out uint bytesTransferred)
    {
        bytesTransferred = 0; byte[] data = ArrayPool<byte>.Shared.Rent((int)Math.Min(Math.Max(length, 1u), (uint)store.Identity.MaxTransferBytes));
        try
        {
            while (bytesTransferred < length)
            {
                int requested = (int)Math.Min(length - bytesTransferred, (uint)store.Identity.MaxTransferBytes);
                int count = store.Read((Guid)fileNode, checked((long)(offset + bytesTransferred)), data.AsSpan(0, requested));
                if (count == 0) break;
                Marshal.Copy(data, 0, IntPtr.Add(buffer, checked((int)bytesTransferred)), count); bytesTransferred += (uint)count;
            }
            return bytesTransferred == 0 && length != 0 ? STATUS_END_OF_FILE : STATUS_SUCCESS;
        }
        finally { ArrayPool<byte>.Shared.Return(data); }
    }
    public override int Write(object fileNode, object fileDesc, IntPtr buffer, ulong offset, uint length,
        bool writeToEndOfFile, bool constrainedIo, out uint bytesTransferred, out FileInfo fileInfo)
    {
        long entered = WriteClock();
        var request = new PendingWrite((Guid)fileNode, buffer, offset, length, writeToEndOfFile, constrainedIo)
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
        fileInfo = SubmitMetadata("set-basic-info", () =>
        {
            Guid id = (Guid)fileNode; var old = store.Get(id);
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
        fileInfo = SubmitMetadata("set-file-size", () =>
        {
            Guid id = (Guid)fileNode;
            string path = mutationObserver is not null ? store.Get(id).Path : "";
            if (mutationObserver is not null) Visit("callback.before-effect", "truncate", path, checked((long)newSize), 0);
            if (!setAllocationSize || newSize < (ulong)store.Get(id).Length) store.SetLength(id, checked((long)newSize));
            var info = Info(id);
            if (mutationObserver is not null) Visit("callback.after-effect", "truncate", path, checked((long)newSize), checked((long)info.FileSize));
            return info;
        });
        return STATUS_SUCCESS;
    }
    public override int CanDelete(object fileNode, object fileDesc, string fileName)
    {
        var entry = store.Get((Guid)fileNode);
        return entry.Directory && store.EnumerateChildren(entry.Path).Any() ? STATUS_DIRECTORY_NOT_EMPTY : STATUS_SUCCESS;
    }
    public override int Rename(object fileNode, object fileDesc, string fileName, string newFileName, bool replaceIfExists)
    {
        using (EnterMutation("rename"))
        {
            string operation = replaceIfExists ? "replace" : "rename";
            string path = mutationObserver is not null ? store.Get((Guid)fileNode).Path : "";
            Visit("callback.before-effect", operation, path, 0, 0);
            var replaced = replaceIfExists ? store.Stat(newFileName) : null;
            store.Rename((Guid)fileNode, newFileName, replaceIfExists);
            if (replaced is not null && replaced.Id != (Guid)fileNode && !retainedContexts.ContainsKey(replaced.Id))
                store.ReleaseUnlinkedFile(replaced.Id);
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
