using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Splinter.Core;

namespace Splinter.Platform.Windows;

// Keep the root's name and all ancestors fixed. Readers can use the immutable
// manifest without resolving that path again; actual payload I/O still fails
// normally on a device/read error. The owning tree serializes lease transitions.
internal sealed class WindowsOwnedDirectoryLease : IDisposable
{
    private readonly List<SafeFileHandle> ancestors = [];
    private readonly string path, identity;
    private SafeFileHandle? root;
    private bool transferred;

    internal WindowsOwnedDirectoryLease(string directory)
    {
        path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        try
        {
            var paths = new Stack<string>();
            for (string? current = path; current is not null; current = Path.GetDirectoryName(current)) paths.Push(current);
            while (paths.TryPop(out string? current))
            {
                var handle = Open(current, immutable: current == path);
                if (current == path) root = handle; else ancestors.Add(handle);
                string currentIdentity = Identity(handle);
                if (current == path) identity = currentIdentity;
            }
            if (root is null || identity is null) throw new WorkspaceException("GeneratedBasisChanged", "The generated root was not pinned.");
            // Ancestors permit child publication. Once the immutable root is
            // pinned, each ancestor contains a pinned descendant and cannot
            // become an empty junction. Recheck after acquiring the full chain
            // to reject a reparse installed during initial traversal.
            foreach (var ancestor in ancestors) _ = Identity(ancestor);
            RequirePath(root, path);
        }
        catch { Dispose(); throw; }
    }

    internal void EnsureAvailable()
    {
        if (root is null || root.IsClosed || root.IsInvalid)
            throw new WorkspaceException("SnapshotReadFailed", "The generated root lifetime proof is unavailable.");
    }

    // Restore already owns an independent ancestor lease. Keep ours and all
    // file fences while changing only the root handle's access. The empty-root
    // gap is guarded by the previous volume/file ID, never a path-only check.
    internal SafeFileHandle TransferToRestore(Action? rootUnpinned)
    {
        EnsureAvailable();
        if (transferred) throw new WorkspaceException("GeneratedRestoreConflict", "The generated root was already handed to restoration.");
        root!.Dispose();
        rootUnpinned?.Invoke();
        var moving = ManagedRestoreFileAccess.DirectoryHandle(path, moving: true);
        try
        {
            if (Identity(moving) != identity)
                throw new WorkspaceException("GeneratedRestoreConflict", "Preserve a replaced generated root during restoration handoff.");
            foreach (var ancestor in ancestors) _ = Identity(ancestor);
            RequirePath(moving, path);
            root = moving;
            transferred = true;
            return moving;
        }
        catch { moving.Dispose(); throw; }
    }

    public void Dispose()
    {
        if (!transferred) root?.Dispose();
        for (int i = ancestors.Count - 1; i >= 0; i--) ancestors[i].Dispose();
        ancestors.Clear();
    }

    private static SafeFileHandle Open(string path, bool immutable)
    {
        // FILE_LIST_DIRECTORY | FILE_READ_ATTRIBUTES / FILE_SHARE_READ /
        // OPEN_REPARSE_POINT / BACKUP_SEMANTICS. Attributes-only access does
        // not establish Windows' share-delete exclusion; read-data access is
        // required to keep these names fixed. Ancestors also share WRITE so
        // unrelated children can be renamed; the immutable root does not.
        // No directory entries are read.
        string native = path.StartsWith(@"\\?\", StringComparison.Ordinal) ? path : @"\\?\" + path;
        var handle = CreateFileW(native, 0x81, immutable ? 1u : 3u, 0, 3, 0x02200000, 0);
        if (!handle.IsInvalid) return handle;
        int error = Marshal.GetLastWin32Error(); handle.Dispose();
        throw new WorkspaceException(error is 2 or 3 ? "SnapshotMissing" : "GeneratedBasisChanged",
            "Cannot pin the generated directory: " + new Win32Exception(error).Message, new { path, nativeError = error });
    }
    private static string Identity(SafeFileHandle handle)
    {
        if (!GetFileInformationByHandle(handle, out var info))
            throw new WorkspaceException("GeneratedBasisChanged", "Cannot inspect the generated directory handle.", new { nativeError = Marshal.GetLastWin32Error() });
        if ((info.Attributes & 0x400) != 0 || (info.Attributes & 0x10) == 0)
            throw new WorkspaceException("UnsafePath", "Generated-directory ancestry must remain ordinary directories.");
        return $"{info.VolumeSerial:X8}:{info.IndexHigh:X8}{info.IndexLow:X8}";
    }
    internal static void RequirePath(SafeFileHandle handle, string expected)
    {
        var buffer = new StringBuilder(512);
        uint length = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Capacity, 0);
        if (length >= buffer.Capacity && length < 32768)
        {
            buffer.EnsureCapacity(checked((int)length + 1));
            length = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Capacity, 0);
        }
        if (length == 0 || length >= buffer.Capacity)
            throw new WorkspaceException("UnsafePath", "Cannot verify a pinned handle's final path.");
        string actual = buffer.ToString();
        actual = actual.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase) ? @"\\" + actual[8..] :
            actual.StartsWith(@"\\?\", StringComparison.Ordinal) ? actual[4..] : actual;
        if (!string.Equals(Path.TrimEndingDirectorySeparator(actual), Path.TrimEndingDirectorySeparator(Path.GetFullPath(expected)), StringComparison.OrdinalIgnoreCase))
            throw new WorkspaceException("UnsafePath", "A pinned handle resolved outside its ordinary directory ancestry.");
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    { public uint Attributes, CreationLow, CreationHigh, AccessLow, AccessHigh, WriteLow, WriteHigh, VolumeSerial, SizeHigh, SizeLow, Links, IndexHigh, IndexLow; }
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, nint security, uint disposition, uint flags, nint template);
    [DllImport("kernel32", SetLastError = true)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle file, out FileInformation info);
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandleW(SafeFileHandle file, StringBuilder path, uint length, uint flags);
}
