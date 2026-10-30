using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Splinter.Core;

namespace Splinter.Platform.Windows;

public sealed class WindowsOriginalEditorIdentityVerifier : IOriginalEditorIdentityVerifier
{
    public IDisposable Acquire(string project, int processId, long startTimeUtcTicks)
    {
        Process? process = null;
        try
        {
            process = Process.GetProcessById(processId);
            if (process.HasExited || process.StartTime.ToUniversalTime().Ticks != startTimeUtcTicks ||
                !string.Equals(Path.GetFileName(process.MainModule?.FileName), "Unity.exe", StringComparison.OrdinalIgnoreCase)) throw Invalid();
            var arguments = Arguments(process);
            var selected = arguments.Select((argument, i) => (argument, i)).Where(p => p.argument.Equals("-projectPath", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (selected.Length != 1 || selected[0].i + 1 >= arguments.Length ||
                !Path.IsPathFullyQualified(arguments[selected[0].i + 1]) ||
                !WorkspaceEqual(project, arguments[selected[0].i + 1])) throw Invalid();
            if (process.HasExited || process.StartTime.ToUniversalTime().Ticks != startTimeUtcTicks) throw Invalid();
            return process;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        { process?.Dispose(); throw Invalid(); }
        catch { process?.Dispose(); throw; }
    }
    private static bool WorkspaceEqual(string a, string b) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(a))
        .Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)), StringComparison.OrdinalIgnoreCase);
    private static string[] Arguments(Process process)
    {
        NtQueryInformationProcess(process.SafeHandle, 60, 0, 0, out uint required);
        if (required is < 16 or > 1048576) throw Invalid();
        nint buffer = Marshal.AllocHGlobal((int)required);
        try
        {
            if (NtQueryInformationProcess(process.SafeHandle, 60, buffer, required, out _) != 0) throw Invalid();
            var unicode = Marshal.PtrToStructure<UnicodeString>(buffer);
            if (unicode.Length == 0 || unicode.Length % 2 != 0 || unicode.Buffer < buffer ||
                unicode.Buffer.ToInt64() + unicode.Length > buffer.ToInt64() + required) throw Invalid();
            string command = Marshal.PtrToStringUni(unicode.Buffer, unicode.Length / 2) ?? throw Invalid();
            nint pointers = CommandLineToArgvW(command, out int count);
            if (pointers == 0) throw Invalid();
            try { return Enumerable.Range(0, count).Select(i => Marshal.PtrToStringUni(Marshal.ReadIntPtr(pointers, i * IntPtr.Size)) ?? "").ToArray(); }
            finally { LocalFree(pointers); }
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }
    private static WorkspaceException Invalid() => new("LiveOriginalIdentityMismatch", "Cannot prove the exact original Unity executable, PID, start time and project argument.");
    [StructLayout(LayoutKind.Sequential)] private struct UnicodeString { public ushort Length, MaximumLength; public nint Buffer; }
    [DllImport("ntdll.dll")] private static extern int NtQueryInformationProcess(SafeProcessHandle process, int informationClass, nint information, uint size, out uint required);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern nint CommandLineToArgvW(string commandLine, out int count);
    [DllImport("kernel32.dll")] private static extern nint LocalFree(nint memory);
}
