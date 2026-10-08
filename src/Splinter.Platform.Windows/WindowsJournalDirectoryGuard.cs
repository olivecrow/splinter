using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Platform.Windows;

// A writer's object directory is flat and immutable in identity. Holding every
// ancestor without write/delete sharing prevents both name replacement and
// reparse conversion; only the leaf needs inspecting on subsequent accesses.
internal sealed class WindowsJournalDirectoryGuard : IJournalDirectoryGuard
{
    private readonly object gate = new();
    private readonly List<SafeFileHandle> handles = [];
    private bool disposed;
    public string DirectoryPath { get; }

    internal WindowsJournalDirectoryGuard(string directory)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        DirectoryPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        try
        {
            var ancestors = new Stack<string>();
            for (string? path = DirectoryPath; path is not null; path = Path.GetDirectoryName(path)) ancestors.Push(path);
            while (ancestors.TryPop(out string? path))
            {
                // FILE_READ_ATTRIBUTES, FILE_SHARE_READ, OPEN_REPARSE_POINT
                // and BACKUP_SEMANTICS. No payload is read or cached.
                var handle = CreateFileW(path, 0x80, 1, 0, 3, 0x02200000, 0);
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
