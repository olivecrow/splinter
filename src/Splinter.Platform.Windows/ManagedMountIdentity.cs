using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Splinter.Core;

namespace Splinter.Platform.Windows;

internal static class ManagedMountIdentity
{
    public static IDisposable Acquire(string path, string id)
    {
        var handle = CreateFileW(path, 0x80, 3, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero);
        try
        {
            var fs = new StringBuilder(32);
            uint expected = BitConverter.ToUInt32(SHA256.HashData(Encoding.UTF8.GetBytes(id)), 0);
            if (handle.IsInvalid || !GetVolumeInformationByHandleW(handle, null, 0, out uint serial, out _, out _, fs, fs.Capacity))
                throw new WorkspaceException("ProjectOriginalMountRequired", "Cannot pin the original managed filesystem: " + new Win32Exception(Marshal.GetLastWin32Error()).Message);
            if (fs.ToString() != "SplinterSO" || serial != expected)
                throw new WorkspaceException("ProjectBasisMismatch", "The original path no longer exposes its exact managed project volume.");
            return handle;
        }
        catch { handle.Dispose(); throw; }
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumeInformationByHandleW(SafeFileHandle h, StringBuilder? volume, int capacity, out uint serial, out uint max, out uint flags, StringBuilder fs, int fsCapacity);
}
