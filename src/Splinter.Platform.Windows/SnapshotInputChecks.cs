using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Splinter.Core;

namespace Splinter.Platform.Windows;

// Metadata-only admission check. It never reads or copies file payloads.
public static class SnapshotInputChecks
{
    public static int VerifyTree(string root, CancellationToken ct = default, bool rejectVcsMetadata = false, bool omitPlasticRoot = false)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        int count = 0;
        var pending = new Stack<string>(); pending.Push(root);
        while (pending.TryPop(out string? directory))
        {
            ct.ThrowIfCancellationRequested(); Verify(directory);
            foreach (string path in Directory.EnumerateFileSystemEntries(directory))
            {
                ct.ThrowIfCancellationRequested();
                if (omitPlasticRoot && directory == root && Path.GetFileName(path).Equals(".plastic", StringComparison.OrdinalIgnoreCase))
                { Verify(path); if (!Directory.Exists(path)) throw new WorkspaceException("VcsMetadataUnsupported", "Plastic administration must be a directory."); continue; }
                if (rejectVcsMetadata && Splinter.SourceControl.Files.FileWorkspaceProvider.VcsMetadata.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
                    throw new WorkspaceException("VcsMetadataUnsupported", "The immutable plain-folder tree cannot contain VCS administration.");
                if (!rejectVcsMetadata && directory == root && Path.GetFileName(path).Equals(".git", StringComparison.OrdinalIgnoreCase)) continue;
                if (++count > 250000) throw new WorkspaceException("OverlayMetadataLimit", "Snapshot input exceeds the bounded metadata index.");
                Verify(path);
                if ((File.GetAttributes(path) & FileAttributes.Directory) != 0) pending.Push(path);
            }
        }
        return count;
    }

    public static void Verify(string path)
    {
        FileAttributes attrs = File.GetAttributes(path);
        RejectUnsupportedAttributes((uint)attrs, path);
        string native = path.StartsWith(@"\\?\", StringComparison.Ordinal) ? path : @"\\?\" + Path.GetFullPath(path);
        nint search = FindFirstStreamW(native, 0, out var stream, 0);
        if (search == -1)
        {
            if (Marshal.GetLastWin32Error() != 38) throw Native(path);
        }
        else
        {
            try
            {
                do { if (stream.Name != "::$DATA") throw Unsupported(path, "Alternate data streams are not supported."); }
                while (FindNextStreamW(search, out stream));
                if (Marshal.GetLastWin32Error() != 38) throw Native(path);
            }
            finally { FindClose(search); }
        }
        if ((attrs & FileAttributes.Directory) != 0) return;
        // FILE_READ_ATTRIBUTES and FILE_FLAG_OPEN_REPARSE_POINT avoid payload
        // reads and following an unexpected link during live-source preflight.
        using var handle = CreateFileW(native, 0x80, 7, 0, 3, 0x00200000, 0);
        if (handle.IsInvalid || !GetFileInformationByHandle(handle, out var info)) throw Native(path);
        RejectUnsupportedAttributes(info.Attributes, path);
        if (info.Links != 1)
            throw Unsupported(path, "Hard links and reparse points are not supported.");
    }
    // The same metadata policy applies to admission, immutable reads and live
    // overlay mutations. Check before writing a journal record or truncating.
    public static void RejectUnsupportedAttributes(uint attributes, string? path = null)
    {
        const FileAttributes unsupported = FileAttributes.ReparsePoint | FileAttributes.Encrypted |
            FileAttributes.Compressed | FileAttributes.SparseFile;
        if ((attributes & (uint)unsupported) != 0)
            throw new WorkspaceException("SnapshotFileUnsupported", "Reparse, EFS, compressed and sparse files require separate qualification.", new { path, attributes });
    }
    private static WorkspaceException Unsupported(string path, string message) => new("SnapshotFileUnsupported", message, new { path });
    private static WorkspaceException Native(string path) => new("SnapshotInputProbeFailed", new Win32Exception(Marshal.GetLastWin32Error()).Message, new { path });
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StreamData { public long Size; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 296)] public string Name; }
    // https://learn.microsoft.com/windows/win32/api/fileapi/ns-fileapi-by_handle_file_information
    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        public uint Attributes, CreationLow, CreationHigh, AccessLow, AccessHigh, WriteLow, WriteHigh;
        public uint VolumeSerial, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
    }
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)] private static extern nint FindFirstStreamW(string path, int level, out StreamData data, int flags);
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool FindNextStreamW(nint search, out StreamData data);
    [DllImport("kernel32", SetLastError = true)] private static extern bool FindClose(nint search);
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, nint security, uint disposition, uint flags, nint template);
    [DllImport("kernel32", SetLastError = true)] private static extern bool GetFileInformationByHandle(SafeFileHandle file, out FileInformation info);
}
