using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Platform.Windows;

// A writer's object directory is flat and immutable in identity. Directory
// namespace pins permit child publication. The existing format marker is pinned
// against deletion so the root and its ancestors cannot become empty reparses.
internal sealed class WindowsJournalDirectoryGuard : IJournalDirectoryGuard
{
    private readonly object gate = new();
    private readonly List<SafeFileHandle> handles = [];
    private SafeFileHandle? owner;
    private bool disposed;
    public string DirectoryPath { get; }

    internal WindowsJournalDirectoryGuard(string directory, string? existingOwnerName = null)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        if (existingOwnerName is not (null or "owner.lock" or BoundedJournalReclamation.GuardName))
            throw new ArgumentException("Require the existing format-owned marker.", nameof(existingOwnerName));
        DirectoryPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        try
        {
            var ancestors = new Stack<string>();
            for (string? path = DirectoryPath; path is not null; path = Path.GetDirectoryName(path)) ancestors.Push(path);
            while (ancestors.TryPop(out string? path))
            {
                // FILE_LIST_DIRECTORY | FILE_READ_ATTRIBUTES activates share
                // exclusion; attributes-only handles do not prevent renames.
                // FILE_SHARE_READ|WRITE / OPEN_REPARSE_POINT / BACKUP_SEMANTICS.
                // No directory entries or payload are read or cached.
                var handle = CreateFileW(path, 0x81, 3, 0, 3, 0x02200000, 0);
                if (handle.IsInvalid)
                {
                    int error = Marshal.GetLastWin32Error(); handle.Dispose();
                    throw new WorkspaceException("JournalPathUnpinned", "Cannot pin journal ancestry: " + new Win32Exception(error).Message,
                        new { path, nativeError = error });
                }
                handles.Add(handle);
                if (!GetFileInformationByHandle(handle, out var info))
                    throw new WorkspaceException("JournalPathUnpinned", "Cannot inspect pinned journal ancestry: " + new Win32Exception(Marshal.GetLastWin32Error()).Message);
                if ((info.Attributes & 0x400) != 0 || (info.Attributes & 0x10) == 0)
                    throw new WorkspaceException("UnsafePath", "Journal ancestry must remain ordinary directories: " + path);
            }
            // Classify only after the full ordinary-directory proof. In
            // particular, a reparse path must retain its UnsafePath refusal.
            if (existingOwnerName is null)
            {
                bool latest = File.Exists(Path.Combine(DirectoryPath, "owner.lock"));
                bool bounded = File.Exists(Path.Combine(DirectoryPath, BoundedJournalReclamation.GuardName));
                if (latest == bounded)
                    throw new WorkspaceException("JournalPathUnpinned", "Require exactly one existing format-owned journal marker; preserve missing or ambiguous ownership.");
                existingOwnerName = bounded ? BoundedJournalReclamation.GuardName : "owner.lock";
            }
            // Existing format-owned file; never create a marker or alter its
            // bytes. Share write with Runtime's owner lock but deny deletion.
            owner = CreateFileW(Path.Combine(DirectoryPath, existingOwnerName), 0x80000000, 3, 0, 3, 0x00200000, 0);
            if (owner.IsInvalid || !GetFileInformationByHandle(owner, out var ownerInfo))
                throw new WorkspaceException("JournalPathUnpinned", "Cannot pin the existing journal owner lock.");
            if ((ownerInfo.Attributes & (0x400 | 0x10)) != 0)
                throw new WorkspaceException("UnsafePath", "The journal owner lock must be an ordinary file.");
            // Reject reparse conversion during construction before all child
            // identities were pinned. From here every ancestor stays nonempty.
            foreach (var handle in handles)
            {
                if (!GetFileInformationByHandle(handle, out var info))
                    throw new WorkspaceException("JournalPathUnpinned", "Cannot inspect pinned journal ancestry.");
                if ((info.Attributes & 0x400) != 0 || (info.Attributes & 0x10) == 0)
                    throw new WorkspaceException("UnsafePath", "Journal ancestry must remain ordinary directories.");
            }
            WindowsOwnedDirectoryLease.RequirePath(handles[^1], DirectoryPath);
            WindowsOwnedDirectoryLease.RequirePath(owner, Path.Combine(DirectoryPath, existingOwnerName));
        }
        catch { Dispose(); throw; }
    }

    public void EnsureFile(string path)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (!WorkspaceStore.SamePath(Path.GetDirectoryName(path) ?? "", DirectoryPath))
                throw new WorkspaceException("UnsafePath", "A journal object escaped its pinned directory.");
            FileAttributes attributes;
            try { attributes = File.GetAttributes(path); }
            catch (FileNotFoundException) { return; }
            catch (DirectoryNotFoundException) { return; }
            if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
                throw new WorkspaceException("UnsafePath", "A journal object must be an ordinary file: " + path);
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            owner?.Dispose();
            for (int i = handles.Count - 1; i >= 0; i--) handles[i].Dispose();
            handles.Clear();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    { public uint Attributes, CreationLow, CreationHigh, AccessLow, AccessHigh, WriteLow, WriteHigh, VolumeSerial, SizeHigh, SizeLow, Links, IndexHigh, IndexLow; }
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, nint security, uint disposition, uint flags, nint template);
    [DllImport("kernel32", SetLastError = true)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle file, out FileInformation info);
}
