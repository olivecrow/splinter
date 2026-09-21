using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Splinter.Core;

namespace Splinter.Platform.Windows;

public sealed class RefsWorkspaceStorageProvider : IWorkspaceStorageProvider
{
    public StorageProbe Probe(string directory)
    {
        if (!OperatingSystem.IsWindows()) throw new WorkspaceException("CoWUnsupported", "ReFS block cloning requires Windows.");
        WindowsProcessWorkspaceProvider.CheckPath(directory);
        string? existing = Path.GetFullPath(directory);
        while (existing is not null && !Directory.Exists(existing)) existing = Path.GetDirectoryName(existing);
        if (existing is null) throw new WorkspaceException("CoWUnsupported", "No existing parent volume.");
        var mount = new StringBuilder(1024); var volume = new StringBuilder(1024); var fs = new StringBuilder(64);
        if (!GetVolumePathNameW(existing, mount, mount.Capacity) ||
            !GetVolumeInformationW(mount.ToString(), null, 0, out _, out _, out _, fs, fs.Capacity) ||
            !GetVolumeNameForVolumeMountPointW(mount.ToString(), volume, volume.Capacity) ||
            !GetDiskFreeSpaceW(mount.ToString(), out uint sectors, out uint bytes, out _, out _)) throw Native("StorageProbeFailed");
        if (!fs.ToString().Equals("ReFS", StringComparison.OrdinalIgnoreCase))
            throw new WorkspaceException("CoWUnsupported", "The workspace home must be on ReFS. Full-copy fallback is disabled; actual filesystem: " + fs);
        return new("refs-cow", volume.ToString(), fs.ToString(), (long)sectors * bytes);
    }

    public async Task<CloneStatistics> CloneAsync(string source, string destination, string[] excludedTopLevel, CancellationToken ct = default)
    {
        var watch = Stopwatch.StartNew();
        string from = Path.TrimEndingDirectorySeparator(Path.GetFullPath(source));
        string to = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destination));
        if (from.Equals(to, StringComparison.OrdinalIgnoreCase) || from.StartsWith(to + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            to.StartsWith(from + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new WorkspaceException("UnsafePath", "Clone roots must be independent directories.");
        var sourceVolume = Probe(from); var targetVolume = Probe(to);
        if (sourceVolume.Volume != targetVolume.Volume) throw new WorkspaceException("CoWCrossVolume", "Baseline and workspace must share one ReFS volume.");
        // Existing Git metadata may be present, but never overwrite a source file.
        var excluded = new HashSet<string>(excludedTopLevel, StringComparer.OrdinalIgnoreCase);
        var files = new List<(string Path, string Relative)>(); var emptyDirectories = new List<string>();
        var directories = new Stack<string>(); directories.Push(from);
        while (directories.TryPop(out string? directory))
        {
            ct.ThrowIfCancellationRequested();
            foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
            {
                string relative = Path.GetRelativePath(from, entry);
                if (excluded.Contains(relative.Split(Path.DirectorySeparatorChar)[0])) continue;
                WindowsProcessWorkspaceProvider.CheckPath(entry);
                if (Directory.Exists(entry)) { directories.Push(entry); emptyDirectories.Add(relative); }
                else
                {
                    var attributes = File.GetAttributes(entry);
                    if ((attributes & (FileAttributes.Encrypted | FileAttributes.Compressed)) != 0)
                        throw new WorkspaceException("CoWFileUnsupported", "Encrypted or compressed baseline files require separate support: " + relative);
                    RejectStreams(entry);
                    if (File.Exists(Path.Combine(to, relative)) || Directory.Exists(Path.Combine(to, relative)))
                        throw new WorkspaceException("CloneDestinationExists", "Refusing to overwrite an existing clone destination: " + relative);
                    files.Add((entry, relative));
                }
            }
        }
        long logical = 0, cloned = 0, copied = 0;
        Directory.CreateDirectory(to);
        foreach (string relative in emptyDirectories)
        {
            string path = Path.Combine(to, relative); WindowsProcessWorkspaceProvider.CheckPath(path); Directory.CreateDirectory(path);
        }
        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            string target = Path.Combine(to, file.Relative); WindowsProcessWorkspaceProvider.CheckPath(target);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using var sourceStream = new FileStream(file.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var targetStream = new FileStream(target, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            long length = sourceStream.Length;
            // Even aligned files need a final write before cloning to establish
            // valid data length. Copy at most one final cluster, never the prefix.
            long shared = length == 0 ? 0 : (length - 1) / sourceVolume.ClusterBytes * sourceVolume.ClusterBytes;
            // Extending VDL after cloning can zero/privatize the cloned range.
            // Make holes cheap, write the small tail FIRST, then map shared blocks.
            if (!DeviceIoControl(targetStream.SafeFileHandle, 0x000900c4, 0, 0, 0, 0, out _, 0)) throw Native("CoWSparseSetupFailed");
            targetStream.SetLength(length);
            sourceStream.Position = shared; targetStream.Position = shared;
            await sourceStream.CopyToAsync(targetStream, 65536, ct);
            // Drain the managed buffer before the native clone. Per-file disk
            // barriers multiply cache creation cost; these are derived files.
            targetStream.Flush();
            for (long offset = 0; offset < shared;)
            {
                long count = Math.Min(shared - offset, 1024L * 1024 * 1024);
                var data = new DuplicateExtents { FileHandle = sourceStream.SafeFileHandle.DangerousGetHandle(), SourceOffset = offset, TargetOffset = offset, ByteCount = count };
                if (!DeviceIoControl(targetStream.SafeFileHandle, 0x00098344, ref data, Marshal.SizeOf<DuplicateExtents>(), 0, 0, out _, 0))
                    throw Native("CoWCloneFailed"); // Never silently copy a large file.
                offset += count;
            }
            targetStream.Flush();
            targetStream.Dispose();
            File.SetLastWriteTimeUtc(target, File.GetLastWriteTimeUtc(file.Path));
            logical += length; cloned += shared; copied += length - shared;
        }
        return new(logical, cloned, copied, files.Count, watch.ElapsedMilliseconds);
    }
    private static void RejectStreams(string file)
    {
        string nativePath = file.StartsWith("\\\\?\\", StringComparison.Ordinal) ? file : "\\\\?\\" + file;
        nint search = FindFirstStreamW(nativePath, 0, out var data, 0);
        if (search == -1)
        {
            if (Marshal.GetLastWin32Error() == 38) return;
            throw Native("StorageProbeFailed");
        }
        try
        {
            do { if (data.Name != "::$DATA") throw new WorkspaceException("CoWFileUnsupported", "Alternate data streams are unsupported: " + file); }
            while (FindNextStreamW(search, out data));
            if (Marshal.GetLastWin32Error() != 38) throw Native("StorageProbeFailed");
        }
        finally { FindClose(search); }
    }
    private static WorkspaceException Native(string code) => new(code, new Win32Exception(Marshal.GetLastWin32Error()).Message);
    [StructLayout(LayoutKind.Sequential)] private struct DuplicateExtents { public nint FileHandle; public long SourceOffset, TargetOffset, ByteCount; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct StreamData { public long Size; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 296)] public string Name; }
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool GetVolumePathNameW(string path, StringBuilder volume, int length);
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool GetVolumeNameForVolumeMountPointW(string mount, StringBuilder name, int size);
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool GetVolumeInformationW(string root, StringBuilder? volume, int size, out uint serial, out uint maximum, out uint flags, StringBuilder fs, int fsSize);
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool GetDiskFreeSpaceW(string root, out uint sectors, out uint bytes, out uint free, out uint total);
    [DllImport("kernel32", SetLastError = true)] private static extern bool DeviceIoControl(SafeFileHandle handle, uint code, ref DuplicateExtents input, int size, nint output, int outputSize, out int returned, nint overlapped);
    [DllImport("kernel32", SetLastError = true)] private static extern bool DeviceIoControl(SafeFileHandle handle, uint code, nint input, int size, nint output, int outputSize, out int returned, nint overlapped);
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)] private static extern nint FindFirstStreamW(string file, int level, out StreamData data, int flags);
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool FindNextStreamW(nint handle, out StreamData data);
    [DllImport("kernel32", SetLastError = true)] private static extern bool FindClose(nint handle);
}
