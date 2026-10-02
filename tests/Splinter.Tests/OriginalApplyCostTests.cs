using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using Splinter.Core;
using Splinter.Platform.Windows;

namespace Splinter.Tests;

public sealed partial class FileWorkspaceTests
{
    [Theory]
    [InlineData(8)]
    [InlineData(32)]
    public async Task PublicationStreamsPayloadAndSamplesOnlyOneChangedFileStage(int mebibytes)
    {
        RequireWindowsApply(); Fixture? fixture = null; long stageAllocation = -1;
        var publisher = new WindowsOriginalFilePublisher((at, _) =>
        {
            if (at == "staged") stageAllocation = Allocation(Assert.Single(Directory.GetFiles(fixture!.Project, ".splinter-apply-*.new")));
        });
        using var f = new Fixture(publisher); fixture = f; var ct = TestContext.Current.CancellationToken;
        static void Fill(string path, int count, byte value)
        {
            using var stream = File.Create(path); byte[] bytes = new byte[65536]; Array.Fill(bytes, value);
            for (int i = 0; i < count * 16; i++) stream.Write(bytes);
        }
        Fill(Path.Combine(f.Project, "data.txt"), mebibytes, 1);
        var w = (await f.Ensure("cost", ct)).Workspace;
        Fill(Path.Combine(w.Path, "data.txt"), mebibytes, 2);
        await f.New().CheckpointAsync(w.Id, "bounded publication", ct);
        var plan = (await f.New().PlanOriginalAsync(w.Id, ct)).OriginalApplyPlan!;
        var state = new OriginalApplyState { Plan = plan, IntentKey = "cost", Steps = plan.Changes.Select(_ => new OriginalApplyStep()).ToArray() };
        long before = GC.GetAllocatedBytesForCurrentThread();
        publisher.Publish(state, version => File.OpenRead(Path.Combine(f.Store.ControlPath(w.Id), "files", "objects", version.Hash!)), () => { }, ct);
        long managed = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(state.Completed); Assert.Equal((long)mebibytes * 1024 * 1024, stageAllocation);
        Assert.InRange(managed, 0, 4 * 1024 * 1024);
        string evidence = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../artifacts/plain-folder-apply"));
        Directory.CreateDirectory(evidence);
        File.WriteAllText(Path.Combine(evidence, $"cost-{mebibytes}-{w.Id}.json"), JsonSerializer.Serialize(new
        {
            payloadBytes = (long)mebibytes * 1024 * 1024, stageAllocationBytes = stageAllocation,
            publisherManagedAllocatedBytes = managed, retainedPublicationFiles = Directory.GetFiles(f.Project, ".splinter-apply-*").Length,
            scope = "Windows publisher only; ordinary-user NTFS; fake workspace snapshot setup; excludes Runtime persistence, VSS, Editor and whole-process/physical peak"
        }));
    }
    [StructLayout(LayoutKind.Sequential)] private struct AllocationInfo
    { public long AllocationSize, EndOfFile; public uint Links; [MarshalAs(UnmanagedType.U1)] public bool DeletePending, Directory; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetFileInformationByHandleEx(SafeFileHandle handle, int kind, out AllocationInfo info, uint size);
    private static long Allocation(string path)
    {
        using var h = CreateFileW(path, 0x80, 7, IntPtr.Zero, 3, 0x00200000, IntPtr.Zero);
        Assert.False(h.IsInvalid); Assert.True(GetFileInformationByHandleEx(h, 1, out var info, (uint)Marshal.SizeOf<AllocationInfo>()));
        return info.AllocationSize;
    }
}
