using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Platform.Windows;

// Pin ancestors and the exact file for each move/write. Only metadata is
// retained in receipts; restoration still moves files and writes private ranges.
internal sealed class ManagedRestoreFileAccess : IDisposable
{
    private readonly List<SafeFileHandle> parents = [];
    internal ManagedRestoreFileAccess(params string[] files)
    {
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string file in files)
            for (string? parent = Path.GetDirectoryName(Path.GetFullPath(file)); parent is not null; parent = Path.GetDirectoryName(parent)) directories.Add(parent);
        try
        {
            foreach (string path in directories.OrderBy(p => p.Length))
            {
                // Ancestors are already pinned, root first. Open this component
                // without following reparses and validate its handle attributes.
                var handle = OpenHandle(path, 0x80, directory: true);
                parents.Add(handle); _ = SnapshotInputChecks.FileIdentity(handle, directory: true);
            }
        }
        catch { Dispose(); throw; }
    }
    internal static FileStream Read(string path, bool moving = false)
    {
        WorkspaceStore.EnsurePlainPath(path);
        var handle = OpenHandle(path, 0x80000000 | 0x100u | (moving ? 0x10000u : 0u));
        try { _ = SnapshotInputChecks.FileIdentity(handle); return new(handle, FileAccess.Read); }
        catch { handle.Dispose(); throw; }
    }
    internal static FileStream Write(string path, string identity, Action<FileStream> validate)
    {
        using (var pin = Read(path))
        {
            Require(pin, identity);
            var info = File.GetAttributes(path);
            if ((info & FileAttributes.ReadOnly) != 0)
            {
                validate(pin); // Reject changed retained bytes before clearing read-only.
                uint attributes = (uint)(info & ~FileAttributes.ReadOnly);
                SetBasic(pin.SafeFileHandle, new() { Attributes = attributes == 0 ? 128u : attributes });
            }
        }
        var output = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.RandomAccess);
        try { Require(output, identity); validate(output); return output; }
        catch { output.Dispose(); throw; }
    }
    internal static string Identity(FileStream file) => SnapshotInputChecks.FileIdentity(file.SafeFileHandle);
    internal static void Require(FileStream file, string identity)
    {
        if (Identity(file) != identity) throw new WorkspaceException("GeneratedRestoreConflict", "Preserve an unowned or replaced restore target.");
    }
    internal static void Move(FileStream source, string target)
    {
        // FILE_RENAME_INFO uses an absolute, NUL-terminated Windows name.
        // https://learn.microsoft.com/windows/win32/api/winbase/ns-winbase-file_rename_info
        byte[] name = Encoding.Unicode.GetBytes(Path.GetFullPath(target));
        int rootOffset = IntPtr.Size == 8 ? 8 : 4, lengthOffset = rootOffset + IntPtr.Size, nameOffset = lengthOffset + 4;
        int size = nameOffset + name.Length + 2;
        nint buffer = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.WriteInt32(buffer, 0, 0); // ReplaceIfExists = false.
            Marshal.WriteIntPtr(buffer, rootOffset, IntPtr.Zero); Marshal.WriteInt32(buffer, lengthOffset, name.Length);
            Marshal.Copy(name, 0, buffer + nameOffset, name.Length);
            Marshal.WriteInt16(buffer, nameOffset + name.Length, 0);
            Check(SetFileInformationByHandle(source.SafeFileHandle, 3, buffer, (uint)size));
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }
    internal static void Metadata(FileStream file, uint attributes, long created, long accessed, long written) =>
        SetBasic(file.SafeFileHandle, new() { Creation = created, Access = accessed, Write = written, Attributes = attributes });
    private static void SetBasic(SafeFileHandle handle, BasicInfo info)
    {
        nint buffer = Marshal.AllocHGlobal(Marshal.SizeOf<BasicInfo>());
        try { Marshal.StructureToPtr(info, buffer, false); Check(SetFileInformationByHandle(handle, 0, buffer, (uint)Marshal.SizeOf<BasicInfo>())); }
        finally { Marshal.FreeHGlobal(buffer); }
    }
    private static SafeFileHandle OpenHandle(string path, uint access, bool directory = false)
    {
        var handle = CreateFileW(path, access, 1, 0, 3, 0x00200000u | (directory ? 0x02000000u : 0u), 0);
        if (!handle.IsInvalid) return handle;
        int error = Marshal.GetLastWin32Error(); handle.Dispose(); throw Native(error);
    }
    private static void Check(bool success) { if (!success) throw Native(Marshal.GetLastWin32Error()); }
    private static WorkspaceException Native(int error) => new("GeneratedRestoreConflict", "Cannot pin or mutate the owned restore file: " + new Win32Exception(error).Message, new { nativeError = error });
    public void Dispose() { for (int i = parents.Count - 1; i >= 0; i--) parents[i].Dispose(); }
    [StructLayout(LayoutKind.Sequential)] private struct BasicInfo { public long Creation, Access, Write, Change; public uint Attributes; }
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, nint security, uint disposition, uint flags, nint template);
    [DllImport("kernel32", SetLastError = true)] private static extern bool SetFileInformationByHandle(SafeFileHandle handle, int kind, nint buffer, uint size);
}
