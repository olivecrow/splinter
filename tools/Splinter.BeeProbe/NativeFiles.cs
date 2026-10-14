using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.BeeProbe;

internal sealed class NativePin : IDisposable
{
    private readonly List<SafeFileHandle> handles = [];
    private readonly NativeMethods.ByHandleInfo identity;
    public string Path { get; }
    public object Evidence => new { path = Path, volumeSerial = identity.VolumeSerialNumber, fileIndex = identity.Index, nativePath = NativeMethods.FinalPath(handles[^1]) };
    public NativePin(string path)
    {
        Path = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(path));
        var parents = new Stack<string>(); string? current = Path;
        while (current is not null) { parents.Push(current); current = Directory.GetParent(current)?.FullName; }
        try
        {
            while (parents.TryPop(out string? item))
            {
                var h = NativeMethods.Open(item, 0x81, 3, true); handles.Add(h);
                var info = NativeMethods.Info(h);
                if ((info.Attributes & 0x410) != 0x10) throw new IOException("Pin requires an ordinary directory: " + item);
            }
            identity = NativeMethods.Info(handles[^1]);
            NativeMethods.RequireNtfs(handles[^1]); Verify();
        }
        catch { Dispose(); throw; }
    }
    public void Verify()
    {
        using var probe = NativeMethods.Open(Path, 0x81, 3, true);
        var current = NativeMethods.Info(probe);
        if (current.Index != identity.Index || current.VolumeSerialNumber != identity.VolumeSerialNumber ||
            (current.Attributes & 0x410) != 0x10 || !NativeMethods.FinalPath(probe).Equals("\\\\?\\" + Path, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Pinned native directory identity changed.");
    }
    public void Dispose() { for (int i = handles.Count - 1; i >= 0; i--) handles[i].Dispose(); handles.Clear(); }
}

internal static class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)] internal struct ByHandleInfo
    {
        internal uint Attributes, CreationLow, CreationHigh, AccessLow, AccessHigh, WriteLow, WriteHigh,
            VolumeSerialNumber, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
        internal readonly ulong Index => ((ulong)IndexHigh << 32) | IndexLow;
        internal readonly long Length => checked((long)(((ulong)SizeHigh << 32) | SizeLow));
        internal readonly long Creation => checked((long)(((ulong)CreationHigh << 32) | CreationLow));
        internal readonly long Access => checked((long)(((ulong)AccessHigh << 32) | AccessLow));
        internal readonly long Write => checked((long)(((ulong)WriteHigh << 32) | WriteLow));
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out ByHandleInfo info);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool FlushFileBuffers(SafeFileHandle handle);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern uint GetFinalPathNameByHandleW(SafeFileHandle handle, StringBuilder path, uint length, uint flags);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool GetVolumeInformationByHandleW(SafeFileHandle handle, StringBuilder volume, uint volumeLength, out uint serial, out uint maxName, out uint flags, StringBuilder fs, uint fsLength);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern bool MoveFileExW(string source, string destination, uint flags);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern bool CreateDirectoryW(string path, IntPtr security);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern IntPtr LoadLibraryExW(string path, IntPtr file, uint flags);
    [DllImport("kernel32.dll", SetLastError = true)] internal static extern bool FreeLibrary(IntPtr module);
    internal static SafeFileHandle Open(string path, uint access, uint share, bool directory)
    {
        var handle = CreateFileW("\\\\?\\" + System.IO.Path.GetFullPath(path), access, share, IntPtr.Zero, 3,
            0x00200000u | (directory ? 0x02000000u : 0), IntPtr.Zero);
        if (!handle.IsInvalid) return handle;
        int error = Marshal.GetLastWin32Error(); handle.Dispose(); throw new Win32Exception(error, "Cannot open pinned probe path: " + path);
    }
    internal static ByHandleInfo Info(SafeFileHandle handle)
    { if (!GetFileInformationByHandle(handle, out var info)) throw new Win32Exception(Marshal.GetLastWin32Error()); return info; }
    internal static void Flush(SafeFileHandle handle)
    { if (!FlushFileBuffers(handle)) throw new Win32Exception(Marshal.GetLastWin32Error()); }
    internal static string FinalPath(SafeFileHandle handle)
    {
        var buffer = new StringBuilder(32768); uint length = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Capacity, 0);
        if (length == 0 || length >= buffer.Capacity) throw new Win32Exception(Marshal.GetLastWin32Error()); return buffer.ToString();
    }
    internal static void RequireNtfs(SafeFileHandle handle)
    {
        var fs = new StringBuilder(64);
        if (!GetVolumeInformationByHandleW(handle, new StringBuilder(260), 260, out _, out _, out _, fs, 64)) throw new Win32Exception(Marshal.GetLastWin32Error());
        if (!fs.ToString().Equals("NTFS", StringComparison.OrdinalIgnoreCase)) throw new IOException("Probe targets must use NTFS.");
    }
    internal static string Hash(SafeFileHandle handle)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); byte[] buffer = new byte[65536]; long offset = 0;
        for (int count; (count = RandomAccess.Read(handle, buffer, offset)) != 0; offset += count) hash.AppendData(buffer.AsSpan(0, count));
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
}

internal sealed class PinnedBasis : ISnapshotTree, IDisposable
{
    private readonly List<NativePin> directories = [];
    private readonly Dictionary<string, SnapshotEntry> entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (SafeFileHandle Handle, string Hash)> files = new(StringComparer.OrdinalIgnoreCase);
    public SnapshotReference Snapshot { get; }
    public bool SupportsConcurrentReads => true;
    private bool closed;
    public PinnedBasis(string directory, string nonce)
    {
        directory = Path.GetFullPath(directory);
        Snapshot = new("bee-probe-" + nonce, "development-pinned-ntfs", directory, directory, DateTimeOffset.UtcNow, true, true, "fixture-pinned-no-write-no-delete");
        try
        {
            Visit(directory, "");
            if (Stat("Library/Bee") is not { Directory: true } || List("Library/Bee").Count != 0)
                throw new IOException("Fixture requires empty Library/Bee for both modes.");
        }
        catch { Dispose(); throw; }
    }
    private void Visit(string physical, string relative)
    {
        var pin = new NativePin(physical); directories.Add(pin);
        entries.Add(relative, new(relative, true, 0, 16, 1, 1, 1));
        foreach (string child in Directory.EnumerateFileSystemEntries(physical).Order(StringComparer.OrdinalIgnoreCase))
        {
            var attr = File.GetAttributes(child); string path = relative.Length == 0 ? Path.GetFileName(child) : relative + "/" + Path.GetFileName(child);
            if ((attr & FileAttributes.ReparsePoint) != 0) throw new IOException("Fixture reparse entry rejected: " + child);
            if ((attr & FileAttributes.Directory) != 0) { Visit(child, path); continue; }
            var handle = NativeMethods.Open(child, 0x80000000, 1, false);
            try
            {
                var info = NativeMethods.Info(handle);
                if (info.Links != 1 || (info.Attributes & 0x410) != 0) throw new IOException("Fixture requires ordinary single-link files.");
                string hash = NativeMethods.Hash(handle); files.Add(path, (handle, hash));
                entries.Add(path, new(path, false, info.Length, info.Attributes, info.Creation, info.Access, info.Write));
            }
            catch { handle.Dispose(); throw; }
        }
    }
    public void EnsureAvailable() { ObjectDisposedException.ThrowIf(closed, this); }
    public SnapshotEntry? Stat(string path) { EnsureAvailable(); return entries.GetValueOrDefault(JournalDeltaStore.Normalize(path)); }
    public IReadOnlyList<SnapshotEntry> List(string path)
    {
        EnsureAvailable(); path = JournalDeltaStore.Normalize(path); string prefix = path.Length == 0 ? "" : path + "/";
        return entries.Values.Where(e => e.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && e.Path.Length > prefix.Length && !e.Path[prefix.Length..].Contains('/')).ToArray();
    }
    public int Read(string path, long offset, Span<byte> destination)
    { EnsureAvailable(); return RandomAccess.Read(files[JournalDeltaStore.Normalize(path)].Handle, destination, offset); }
    public object Verify()
    {
        EnsureAvailable(); foreach (var pin in directories) pin.Verify();
        foreach (var (path, file) in files) if (NativeMethods.Hash(file.Handle) != file.Hash) throw new IOException("Pinned basis changed: " + path);
        return files.Select(f => new { path = f.Key, sha256 = f.Value.Hash, length = entries[f.Key].Length }).ToArray();
    }
    public void Dispose() { closed = true; foreach (var file in files.Values) file.Handle.Dispose(); for (int i = directories.Count - 1; i >= 0; i--) directories[i].Dispose(); }
}

internal sealed class NativeOutputProof : IDisposable
{
    private readonly List<SafeFileHandle> handles = [];
    private readonly List<NativePin> directories = [];
    public List<object> Files { get; } = [];
    public NativeOutputProof(string root)
    {
        try { Visit(root, ""); } catch { Dispose(); throw; }
    }
    private void Visit(string root, string relative)
    {
        directories.Add(new NativePin(root));
        foreach (string path in Directory.EnumerateFileSystemEntries(root).Order(StringComparer.OrdinalIgnoreCase))
        {
            var attr = File.GetAttributes(path); string name = relative.Length == 0 ? Path.GetFileName(path) : relative + "/" + Path.GetFileName(path);
            if ((attr & FileAttributes.ReparsePoint) != 0) throw new IOException("Native output contains unexpected reparse entry.");
            if ((attr & FileAttributes.Directory) != 0) { Visit(path, name); continue; }
            // FILE_WRITE_DATA rejects a live executable image section. No bytes or
            // metadata are changed. This does not claim detection of arbitrary
            // foreign data mappings with no open file handles.
            var handle = NativeMethods.Open(path, 0x80000000u | 0x40000000u, 0, false); handles.Add(handle);
            NativeMethods.Flush(handle);
            var info = NativeMethods.Info(handle);
            if (info.Links != 1 || (info.Attributes & 0x410) != 0) throw new IOException("Native output file identity unsupported.");
            Files.Add(new { path = name, length = info.Length, sha256 = NativeMethods.Hash(handle), volumeSerial = info.VolumeSerialNumber, fileIndex = info.Index });
        }
    }
    public void Dispose()
    {
        foreach (var handle in handles) handle.Dispose(); handles.Clear();
        for (int i = directories.Count - 1; i >= 0; i--) directories[i].Dispose(); directories.Clear();
    }
}
