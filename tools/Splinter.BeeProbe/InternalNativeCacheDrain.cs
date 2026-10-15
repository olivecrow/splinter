using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Fsp;
using Microsoft.Win32.SafeHandles;

namespace Splinter.BeeProbe;

// Developer-only lifecycle barrier for the owned probe mount. Cleanup purging
// remains enabled. Notifications address exact linked identities still retained
// by Windows; no filename-specific reopen, counter reset or forced unmount is
// used. A live mapping/image that will not drain preserves the provider.
internal static class InternalNativeCacheDrain
{
    internal const int NotificationBatchBytes = 16 * 1024;

    public static IDisposable PrepareStop(FileSystemHost host, BeeFileSystem fileSystem,
        string mountPath, TimeSpan drainTimeout)
    {
        if (drainTimeout <= TimeSpan.Zero || drainTimeout > TimeSpan.FromSeconds(30))
            throw new ArgumentOutOfRangeException(nameof(drainTimeout));
        if (fileSystem.OpenHandles != 0) throw Blocked(fileSystem, "Clients still own overlay handles.");
        IDisposable? fence = null;
        try
        {
            using (var root = OpenRoot(host, fileSystem, mountPath))
            {
                // Only this verified flush handle may be open when admission
                // closes. A concurrent client open forces preservation/retry.
                fence = fileSystem.TryFenceNewOpens(1) ?? throw Blocked(fileSystem, "A client opened the overlay while preparing its stop.");
                if (!FlushFileBuffers(root)) throw Native("The owned overlay kernel cache did not flush.");
            }
            InvalidateRetainedCaches(host, fileSystem);
            if (!fileSystem.WaitForContextDrain(drainTimeout))
                throw Blocked(fileSystem, "Cached or mapped contexts still require the live overlay provider.");
            return fence;
        }
        catch { fence?.Dispose(); throw; }
    }

    private static void InvalidateRetainedCaches(FileSystemHost host, BeeFileSystem fileSystem)
    {
        RequireSuccess(host.NotifyBegin(1000));
        int status = 0;
        try
        {
            // NotifyBegin fences rename while current linked paths are captured.
            // Deleted identities are never notified via a reused path; only
            // their genuine final Close can satisfy the retained-context gate.
            foreach (byte[] batch in NotificationBatches(fileSystem.RetainedLinkedPaths()))
            {
                status = NativeNotify.Value(host.FileSystemHandle(), batch, (nuint)batch.Length);
                if (status < 0) break;
            }
        }
        finally { int ended = host.NotifyEnd(); if (status >= 0) status = ended; }
        RequireSuccess(status);
    }

    internal static IEnumerable<byte[]> NotificationBatches(IEnumerable<string> paths)
    {
        byte[] batch = new byte[NotificationBatchBytes]; int bytes = 0;
        foreach (string path in paths)
        {
            if (path.Length is 0 or > 32001 || path[0] != '\\' || path.Contains('\0'))
                throw new IOException("The retained path cannot be represented exactly by WinFsp notifications.");
            int recordSize = 12 + path.Length * 2, length = (recordSize + 7) & ~7;
            if (bytes + length > NotificationBatchBytes && bytes != 0)
            { yield return batch.AsSpan(0, bytes).ToArray(); bytes = 0; }
            if (length > NotificationBatchBytes)
            {
                byte[] large = new byte[length]; WriteNotification(large, path, recordSize); yield return large; continue;
            }
            WriteNotification(batch.AsSpan(bytes, length), path, recordSize); bytes += length;
        }
        if (bytes != 0) yield return batch.AsSpan(0, bytes).ToArray();
    }

    private static void WriteNotification(Span<byte> record, string path, int recordSize)
    {
        record.Clear(); BinaryPrimitives.WriteUInt16LittleEndian(record, checked((ushort)recordSize));
        // Filter=None invalidates cache without synthesizing a client change
        // event. Action=Modified uses the documented public notification ABI.
        BinaryPrimitives.WriteUInt32LittleEndian(record[8..], 3);
        MemoryMarshal.AsBytes(path.AsSpan()).CopyTo(record[12..]);
    }

    private static readonly Lazy<Notify> NativeNotify = new(() =>
    {
        string architecture = RuntimeInformation.ProcessArchitecture switch
        { Architecture.X64 => "x64", Architecture.X86 => "x86", Architecture.Arm64 => "a64", _ => throw new PlatformNotSupportedException() };
        nint module = GetModuleHandleW("winfsp-" + architecture + ".dll");
        if (module == 0 || !NativeLibrary.TryGetExport(module, "FspFileSystemNotify", out nint entry))
            throw new IOException("The WinFsp module already loaded by this provider has no public notification entry point.");
        return Marshal.GetDelegateForFunctionPointer<Notify>(entry);
    });
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int Notify(nint fileSystem, [In] byte[] buffer, nuint length);

    private static SafeFileHandle OpenRoot(FileSystemHost host, BeeFileSystem fileSystem, string mountPath)
    {
        if (!ReferenceEquals(host.FileSystem(), fileSystem) ||
            !Path.GetFullPath(host.MountPoint()).Equals(Path.GetFullPath(mountPath), StringComparison.OrdinalIgnoreCase))
            throw new IOException("The cache-drain target is not this exact provider and mounted path.");
        ulong expectedIndex = fileSystem.RootIndexNumber; string expectedVolume = HostVolumeName(host);
        var handle = CreateFileW(mountPath, 0x40000080, 3, 0, 3, 0x02000000, 0);
        try
        {
            var fs = new StringBuilder(32);
            if (handle.IsInvalid || !GetFileInformationByHandle(handle, out var info) ||
                !GetVolumeInformationByHandleW(handle, null, 0, out uint serial, out _, out _, fs, fs.Capacity))
                throw Native("Cannot open the owned mounted root for a kernel flush.");
            var nativePath = new StringBuilder(512);
            uint length = GetFinalPathNameByHandleW(handle, nativePath, (uint)nativePath.Capacity, 2);
            if (length == 0 || length >= nativePath.Capacity) throw Native("Cannot identify the mounted root's native volume.");
            if (fs.ToString() != "SplinterSO" || serial != host.VolumeSerialNumber ||
                !nativePath.ToString().TrimEnd('\\').Equals(expectedVolume.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase) ||
                (info.Attributes & 16) == 0 || ((ulong)info.IndexHigh << 32 | info.IndexLow) != expectedIndex)
                throw new IOException("The root flush handle does not identify the owned volume and root.");
            return handle;
        }
        catch { handle.Dispose(); throw; }
    }

    private static string HostVolumeName(FileSystemHost host)
    {
        // Public winfsp.h FSP_FILE_SYSTEM layout: reserved Version=0, aligned
        // UserContext, then VolumeName[256]. No private managed reflection or
        // separately located DLL is used.
        nint fileSystem = host.FileSystemHandle();
        if (fileSystem == 0 || Marshal.ReadInt16(fileSystem) != 0) throw new IOException("The mounted public WinFsp ABI is not recognized.");
        string name = Marshal.PtrToStringUni(fileSystem + 2 * IntPtr.Size, 256)!;
        int end = name.IndexOf('\0');
        if (end <= 0 || !name.StartsWith("\\Device\\", StringComparison.OrdinalIgnoreCase)) throw new IOException("The mounted native volume name is invalid.");
        return name[..end];
    }
    private static IOException Blocked(BeeFileSystem fileSystem, string message) => new(message +
        " OpenHandles=" + fileSystem.OpenHandles + "; RetainedFileContexts=" + fileSystem.RetainedFileContexts +
        "; Retained=" + JsonSerializer.Serialize(fileSystem.RetainedContextDiagnostics()));
    private static IOException Native(string message) => new(message + " " + new Win32Exception(Marshal.GetLastWin32Error()).Message);
    private static void RequireSuccess(int status)
    { if (status < 0) throw new IOException($"WinFsp cache notification returned 0x{status:x8}."); }

    [StructLayout(LayoutKind.Sequential)]
    private struct Information
    {
        public uint Attributes, CreationLow, CreationHigh, AccessLow, AccessHigh, WriteLow, WriteHigh,
            VolumeSerial, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
    }
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, nint security, uint disposition, uint flags, nint template);
    [DllImport("kernel32", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out Information info);
    [DllImport("kernel32", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlushFileBuffers(SafeFileHandle handle);
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumeInformationByHandleW(SafeFileHandle handle, StringBuilder? volume, int capacity,
        out uint serial, out uint max, out uint flags, StringBuilder fileSystem, int fileSystemCapacity);
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandleW(SafeFileHandle handle, StringBuilder path, uint capacity, uint flags);
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint GetModuleHandleW(string name);
}
