using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Splinter.Core;

namespace Splinter.Platform.Windows;

// This handle belongs to the persistent supervisor, never to the calling CLI.
// No breakaway flags: children inherit membership, including nested Unity jobs.
internal sealed class WindowsProcessGroup : IDisposable
{
    private readonly SafeFileHandle handle;
    private WindowsProcessGroup(SafeFileHandle handle) => this.handle = handle;
    public static WindowsProcessGroup Create(string name)
    {
        var handle = CreateJobObjectW(0, name);
        if (handle.IsInvalid) throw Native("ProcessGroupCreateFailed");
        if (Marshal.GetLastWin32Error() == 183) { handle.Dispose(); throw new WorkspaceException("ProcessGroupExists", "A group with this identity already exists."); }
        var group = new WindowsProcessGroup(handle);
        var limits = new ExtendedLimits { Basic = new BasicLimits { LimitFlags = 0x2000 } }; // KILL_ON_JOB_CLOSE
        if (!SetInformationJobObject(handle, 9, ref limits, Marshal.SizeOf<ExtendedLimits>()))
        { group.Dispose(); throw Native("ProcessGroupLimitsFailed"); }
        return group;
    }
    public static WindowsProcessGroup? Open(string name)
    {
        var handle = OpenJobObjectW(4, false, name); // QUERY only
        if (!handle.IsInvalid) return new(handle);
        int error = Marshal.GetLastWin32Error(); handle.Dispose();
        if (error == 2) return null;
        throw new WorkspaceException("ProcessOwnershipUnknown", new Win32Exception(error).Message);
    }
    public SuspendedProcess Launch(ProcessLaunch launch)
    {
        var startup = new StartupInfo { Size = Marshal.SizeOf<StartupInfo>() };
        var command = new StringBuilder(string.Join(' ', new[] { launch.Executable }.Concat(launch.Arguments).Select(Quote)));
        // Assignment before ResumeThread closes the child-creation race.
        if (!CreateProcessW(launch.Executable, command, 0, 0, false, 0x00000004 | 0x00000400,
                0, launch.WorkingDirectory, ref startup, out var info)) throw Native("EditorLaunchFailed");
        var process = new SuspendedProcess(info);
        if (!AssignProcessToJobObject(handle, process.Handle))
        { process.Dispose(); throw Native("ProcessGroupAssignmentFailed"); }
        return process;
    }
    public static Process StartSupervisor(string executable, string[] arguments, string directory)
    {
        var startup = new StartupInfo { Size = Marshal.SizeOf<StartupInfo>() };
        var command = new StringBuilder(string.Join(' ', new[] { executable }.Concat(arguments).Select(Quote)));
        // Redirecting standard IO alone still lets Process.Start inherit other
        // inheritable pipe handles owned by PowerShell or its caller. Inherit NONE.
        if (!CreateProcessW(null, command, 0, 0, false, 0x08000000 | 0x00000400,
                0, directory, ref startup, out var info)) throw Native("SupervisorLaunchFailed");
        using var processHandle = new SafeFileHandle(info.Process, true);
        using var threadHandle = new SafeFileHandle(info.Thread, true);
        return Process.GetProcessById(info.ProcessId);
    }
    public ProcessResources Observe(ApplicationLease lease)
    {
        var pids = Pids(); long working = 0, privateBytes = 0;
        foreach (int id in pids)
        {
            try
            {
                using var p = Process.GetProcessById(id);
                if (!IsProcessInJob(p.Handle, handle, out bool member)) throw Native("ProcessOwnershipUnknown");
                if (!member || p.HasExited) continue;
                working += p.WorkingSet64; privateBytes += p.PrivateMemorySize64;
            }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }
        }
        int active = ActiveCount;
        return new(true, IdentityAlive(lease.ProcessId, lease.StartTimeUtcTicks), active,
            active == 0 ? 0 : working, active == 0 ? 0 : privateBytes);
    }
    public int ActiveCount
    {
        get
        {
            if (!QueryInformationJobObject(handle, 1, out Accounting accounting, Marshal.SizeOf<Accounting>(), 0)) throw Native("ProcessOwnershipUnknown");
            return checked((int)accounting.ActiveProcesses);
        }
    }
    private int[] Pids()
    {
        for (int capacity = 64; capacity <= 65536; capacity *= 2)
        {
            int size = checked(8 + capacity * IntPtr.Size); nint buffer = Marshal.AllocHGlobal(size);
            try
            {
                if (!QueryInformationJobObject(handle, 3, buffer, size, 0))
                {
                    if (Marshal.GetLastWin32Error() == 234) continue;
                    throw Native("ProcessOwnershipUnknown");
                }
                int count = Marshal.ReadInt32(buffer, 4);
                if (count > capacity) continue;
                return Enumerable.Range(0, count).Select(i => checked((int)Marshal.ReadIntPtr(buffer, 8 + i * IntPtr.Size))).ToArray();
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        throw new WorkspaceException("ProcessOwnershipUnknown", "Process group exceeded the observation bound.");
    }
    public void TerminateRemainder()
    {
        if (!TerminateJobObject(handle, 0)) throw Native("ProcessCleanupFailed");
    }
    public void Dispose() => handle.Dispose();
    internal static bool IdentityAlive(int pid, long ticks)
    {
        try { using var p = Process.GetProcessById(pid); return !p.HasExited && p.StartTime.ToUniversalTime().Ticks == ticks; }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
        catch (Win32Exception ex) { throw new WorkspaceException("ProcessOwnershipUnknown", ex.Message); }
    }
    private static WorkspaceException Native(string code) => new(code, new Win32Exception(Marshal.GetLastWin32Error()).Message);
    // Windows CommandLineToArgvW escaping; no shell is involved.
    private static string Quote(string value)
    {
        var result = new StringBuilder("\""); int slashes = 0;
        foreach (char c in value)
        {
            if (c == '\\') { slashes++; continue; }
            result.Append('\\', c == '"' ? slashes * 2 + 1 : slashes).Append(c); slashes = 0;
        }
        return result.Append('\\', slashes * 2).Append('"').ToString();
    }
    internal sealed class SuspendedProcess : IDisposable
    {
        public SafeFileHandle Handle { get; }
        private readonly SafeFileHandle thread;
        private bool resumed;
        public int Id { get; }
        public SuspendedProcess(ProcessInfo info) { Handle = new(info.Process, true); thread = new(info.Thread, true); Id = info.ProcessId; }
        public void Resume()
        {
            if (ResumeThread(thread) == uint.MaxValue) throw Native("ProcessResumeFailed");
            resumed = true;
        }
        public void Dispose()
        {
            if (!resumed) TerminateProcess(Handle, 1);
            thread.Dispose(); Handle.Dispose();
        }
    }
    [StructLayout(LayoutKind.Sequential)] internal struct ProcessInfo { public nint Process, Thread; public int ProcessId, ThreadId; }
    [StructLayout(LayoutKind.Sequential)] private struct StartupInfo
    {
        public int Size; public nint Reserved, Desktop, Title; public uint X, Y, XSize, YSize, XCount, YCount, Fill, Flags;
        public short Show, ReservedSize; public nint ReservedBytes, Input, Output, Error;
    }
    [StructLayout(LayoutKind.Sequential)] private struct BasicLimits
    {
        public long ProcessTime, JobTime; public uint LimitFlags; public nuint MinWorkingSet, MaxWorkingSet;
        public uint ActiveProcessLimit; public nuint Affinity; public uint Priority, Scheduling;
    }
    [StructLayout(LayoutKind.Sequential)] private struct ExtendedLimits
    {
        public BasicLimits Basic; public ulong ReadCount, WriteCount, OtherCount, ReadBytes, WriteBytes, OtherBytes;
        public nuint ProcessMemory, JobMemory, PeakProcessMemory, PeakJobMemory;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Accounting
    {
        public long User, Kernel, UserPeriod, KernelPeriod; public uint Faults, TotalProcesses, ActiveProcesses, TerminatedProcesses;
    }
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateJobObjectW(nint attributes, string name);
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle OpenJobObjectW(uint access, bool inherit, string name);
    [DllImport("kernel32", SetLastError = true)] private static extern bool SetInformationJobObject(SafeFileHandle job, int type, ref ExtendedLimits data, int size);
    [DllImport("kernel32", SetLastError = true)] private static extern bool QueryInformationJobObject(SafeFileHandle job, int type, out Accounting data, int size, nint returned);
    [DllImport("kernel32", SetLastError = true)] private static extern bool QueryInformationJobObject(SafeFileHandle job, int type, nint data, int size, nint returned);
    [DllImport("kernel32", SetLastError = true)] private static extern bool AssignProcessToJobObject(SafeFileHandle job, SafeFileHandle process);
    [DllImport("kernel32", SetLastError = true)] private static extern bool IsProcessInJob(nint process, SafeFileHandle job, out bool member);
    [DllImport("kernel32", SetLastError = true)] private static extern bool TerminateJobObject(SafeFileHandle job, uint code);
    [DllImport("kernel32", SetLastError = true)] private static extern bool TerminateProcess(SafeFileHandle process, uint code);
    [DllImport("kernel32", SetLastError = true)] private static extern uint ResumeThread(SafeFileHandle thread);
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool CreateProcessW(string? executable,
        StringBuilder command, nint processAttributes, nint threadAttributes, bool inherit, uint flags, nint environment,
        string directory, ref StartupInfo startup, out ProcessInfo process);
}
