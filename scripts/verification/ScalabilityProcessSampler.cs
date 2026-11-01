using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

// Measurement-only process ancestry. Roots retain native PID/start identities
// between CLI calls, so persistent providers and their VCS readers are counted.
public sealed class ScalabilityProcessSampler
{
    public sealed class Identity { public int Id { get; set; } public long StartTicks { get; set; } }
    public sealed class Row
    {
        public int Id { get; set; } public int Parent { get; set; } public long StartTicks { get; set; }
        public string Name { get; set; } = ""; public long PrivateBytes { get; set; }
        public long WorkingSetBytes { get; set; } public int Handles { get; set; }
    }
    public sealed class Observation
    {
        public Row[] Processes { get; set; } = Array.Empty<Row>();
        public long PrivateBytes { get; set; } public long WorkingSetBytes { get; set; }
        public long Handles { get; set; } public string[] Errors { get; set; } = Array.Empty<string>();
    }
    private readonly Dictionary<int, long> known = new Dictionary<int, long>();
    public void Add(int id, long startTicks) { known[id] = startTicks; }
    public Identity[] Identities()
    {
        var result = new List<Identity>(); foreach (var pair in known) result.Add(new Identity { Id = pair.Key, StartTicks = pair.Value }); return result.ToArray();
    }
    public Observation Sample()
    {
        var parents = Parents(); var alive = new Dictionary<int, Row>(); var remove = new List<int>(); var errors = new List<string>();
        foreach (var pair in known)
        {
            Row row = Read(pair.Key, parents, errors);
            if (row == null || row.StartTicks != pair.Value) remove.Add(pair.Key); else alive.Add(pair.Key, row);
        }
        foreach (int id in remove) known.Remove(id);
        bool changed;
        do
        {
            changed = false;
            foreach (var pair in parents)
            {
                if (alive.ContainsKey(pair.Key) || !alive.TryGetValue(pair.Value, out Row parent)) continue;
                Row row = Read(pair.Key, parents, errors);
                if (row == null || row.StartTicks < parent.StartTicks) continue;
                alive.Add(pair.Key, row); known[row.Id] = row.StartTicks; changed = true;
            }
        } while (changed);
        var result = new Observation(); var rows = new List<Row>();
        foreach (Row row in alive.Values) { rows.Add(row); result.PrivateBytes += row.PrivateBytes; result.WorkingSetBytes += row.WorkingSetBytes; result.Handles += row.Handles; }
        result.Processes = rows.ToArray(); result.Errors = errors.ToArray(); return result;
    }
    private static Row Read(int id, Dictionary<int, int> parents, List<string> errors)
    {
        try
        {
            using (var process = Process.GetProcessById(id))
            {
                process.Refresh();
                return new Row { Id = id, Parent = parents.TryGetValue(id, out int parent) ? parent : 0,
                    Name = process.ProcessName, StartTicks = process.StartTime.ToUniversalTime().Ticks,
                    PrivateBytes = process.PrivateMemorySize64, WorkingSetBytes = process.WorkingSet64, Handles = process.HandleCount };
            }
        }
        catch (ArgumentException) { return null; }
        catch (InvalidOperationException) { return null; }
        catch (Win32Exception error) { errors.Add(id + ":" + error.NativeErrorCode); return null; }
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct ProcessEntry
    {
        public uint Size, Usage, ProcessId; public UIntPtr Heap; public uint Module, Threads, Parent;
        public int Priority; public uint Flags; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Name;
    }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool Process32FirstW(IntPtr snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool Process32NextW(IntPtr snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
    private static Dictionary<int, int> Parents()
    {
        IntPtr snapshot = CreateToolhelp32Snapshot(2, 0); if (snapshot == new IntPtr(-1)) throw new Win32Exception();
        try
        {
            var result = new Dictionary<int, int>(); var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf(typeof(ProcessEntry)) };
            if (Process32FirstW(snapshot, ref entry)) do { result[(int)entry.ProcessId] = (int)entry.Parent; } while (Process32NextW(snapshot, ref entry));
            return result;
        }
        finally { CloseHandle(snapshot); }
    }
}
