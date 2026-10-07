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

public sealed class WinFspOverlayFileSystem : FileSystemBase
{
    private readonly IWorkspaceDeltaStore store;
    private readonly byte[] security;
    private readonly Action<Exception>? report;
    private readonly IOverlayMutationObserver? mutationObserver;
    private readonly object mutationGate = new();
    private readonly Dictionary<Guid, int> retainedContexts = [];
    private int handles;
    private int contexts;
    private sealed class HandleContext { public int Cleaned; }
    private sealed record DirectoryListingItem(string Name, DeltaEntry Entry);
    public int OpenHandles => Volatile.Read(ref handles);
    public int RetainedFileContexts => Volatile.Read(ref contexts);
    public WinFspOverlayFileSystem(IWorkspaceDeltaStore store, Action<Exception>? report = null,
        IOverlayMutationObserver? mutationObserver = null)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        WindowsSnapshotPolicy.Require(store.Identity.Snapshot);
        this.store = store; this.report = report; this.mutationObserver = mutationObserver;
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
        lock (mutationGate)
        {
            SnapshotInputChecks.RejectUnsupportedAttributes(fileAttributes, fileName);
            if (store.Stat(fileName) is not null)
            {
                fileNode = fileDesc = null!; fileInfo = default; normalizedName = null!;
                return STATUS_OBJECT_NAME_COLLISION;
            }
            var entry = store.Create(fileName, (createOptions & 1) != 0, fileAttributes);
            fileNode = entry.Id; fileDesc = Retain(entry.Id); fileInfo = Info(entry);
            normalizedName = "\\" + entry.Path.Replace('/', '\\'); Interlocked.Increment(ref handles); Interlocked.Increment(ref contexts); return STATUS_SUCCESS;
        }
    }
    public override int Open(string fileName, uint createOptions, uint grantedAccess,
        out object fileNode, out object fileDesc, out FileInfo fileInfo, out string normalizedName)
    {
        lock (mutationGate)
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
        lock (mutationGate)
        {
            var id = (Guid)fileNode; var old = store.Get(id);
            uint attributes = replaceFileAttributes ? fileAttributes : old.Attributes | fileAttributes;
            SnapshotInputChecks.RejectUnsupportedAttributes(attributes, old.Path);
            store.SetLength(id, 0);
            store.SetMetadata(id, attributes,
                old.CreationTime, old.LastAccessTime, DateTime.UtcNow.ToFileTimeUtc());
            fileInfo = Info(id); return STATUS_SUCCESS;
        }
    }
    public override void Cleanup(object fileNode, object fileDesc, string fileName, uint flags)
    {
        try { lock (mutationGate) { if ((flags & CleanupDelete) != 0) store.Delete((Guid)fileNode); } }
        finally { if (Interlocked.Exchange(ref ((HandleContext)fileDesc).Cleaned, 1) == 0) Interlocked.Decrement(ref handles); }
    }
    public override void Close(object fileNode, object fileDesc)
    {
        if (Interlocked.Exchange(ref ((HandleContext)fileDesc).Cleaned, 1) == 0) Interlocked.Decrement(ref handles);
        try
        {
            lock (mutationGate)
            {
                Guid id = (Guid)fileNode;
                if (retainedContexts[id] > 1) retainedContexts[id]--;
                else { retainedContexts.Remove(id); store.ReleaseUnlinkedFile(id); }
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
        lock (mutationGate)
        {
            Guid id = (Guid)fileNode; var entry = store.Get(id); bytesTransferred = 0;
            if (writeToEndOfFile) offset = (ulong)entry.Length;
            if (constrainedIo) length = offset >= (ulong)entry.Length ? 0 : (uint)Math.Min(length, (ulong)entry.Length - offset);
            Visit("callback.before-effect", "write", entry.Path, length, 0);
            byte[] data = ArrayPool<byte>.Shared.Rent((int)Math.Min(Math.Max(length, 1u), (uint)store.Identity.MaxTransferBytes));
            try
            {
                while (bytesTransferred < length)
                {
                    int count = (int)Math.Min(length - bytesTransferred, (uint)store.Identity.MaxTransferBytes);
                    Marshal.Copy(IntPtr.Add(buffer, checked((int)bytesTransferred)), data, 0, count);
                    store.Write(id, checked((long)(offset + bytesTransferred)), data.AsSpan(0, count)); bytesTransferred += (uint)count;
                }
                fileInfo = Info(id);
                Visit("callback.after-effect", "write", entry.Path, length, bytesTransferred);
                return STATUS_SUCCESS;
            }
            finally { ArrayPool<byte>.Shared.Return(data); }
        }
    }
    public override int Flush(object fileNode, object fileDesc, out FileInfo fileInfo)
    {
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
        lock (mutationGate)
        {
            Guid id = (Guid)fileNode; var old = store.Get(id);
            uint updated = attributes == uint.MaxValue ? old.Attributes : attributes;
            SnapshotInputChecks.RejectUnsupportedAttributes(updated, old.Path);
            store.SetMetadata(id, updated,
                creation == 0 ? old.CreationTime : (long)creation, access == 0 ? old.LastAccessTime : (long)access,
                write == 0 ? old.LastWriteTime : (long)write); fileInfo = Info(id); return STATUS_SUCCESS;
        }
    }
    public override int SetFileSize(object fileNode, object fileDesc, ulong newSize, bool setAllocationSize, out FileInfo fileInfo)
    {
        lock (mutationGate)
        {
            Guid id = (Guid)fileNode;
            string path = mutationObserver is not null ? store.Get(id).Path : "";
            if (mutationObserver is not null) Visit("callback.before-effect", "truncate", path, checked((long)newSize), 0);
            if (!setAllocationSize || newSize < (ulong)store.Get(id).Length) store.SetLength(id, checked((long)newSize));
            fileInfo = Info(id);
            if (mutationObserver is not null) Visit("callback.after-effect", "truncate", path, checked((long)newSize), checked((long)fileInfo.FileSize));
            return STATUS_SUCCESS;
        }
    }
    public override int CanDelete(object fileNode, object fileDesc, string fileName)
    {
        var entry = store.Get((Guid)fileNode);
        return entry.Directory && store.List(entry.Path).Count != 0 ? STATUS_DIRECTORY_NOT_EMPTY : STATUS_SUCCESS;
    }
    public override int Rename(object fileNode, object fileDesc, string fileName, string newFileName, bool replaceIfExists)
    {
        lock (mutationGate)
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
    public override bool ReadDirectoryEntry(object fileNode, object fileDesc, string pattern, string marker,
        ref object context, out string fileName, out FileInfo fileInfo)
    {
        if (context is not IEnumerator<DirectoryListingItem> iterator)
        {
            var entry = store.Get((Guid)fileNode);
            var listing = new List<DirectoryListingItem>();
            if (entry.Path.Length != 0)
            {
                string parentPath = Parent(entry.Path);
                listing.Add(new(".", entry));
                listing.Add(new("..", store.Stat(parentPath)!));
            }
            listing.AddRange(store.List(entry.Path).Select(child => new DirectoryListingItem(Name(child.Path), child)));
            iterator = listing.Where(item => marker is null || StringComparer.OrdinalIgnoreCase.Compare(item.Name, marker) > 0)
                .GetEnumerator();
            context = iterator;
        }
        if (iterator.MoveNext()) { fileName = iterator.Current.Name; fileInfo = Info(iterator.Current.Entry); return true; }
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
