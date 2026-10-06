using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

// Explicit, bounded developer evidence. Ordinary providers never arm a gate.
public sealed class JournalBaseReversionEvidenceTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly string[] Stages = ["append-flushed", "intent", "candidate-flushed", "prepared", "published", "before-previous-delete", "previous-deleted"];
    private sealed record Request(string Directory, string Stage, string Nonce);
    private static SnapshotOverlayTests.FakeSnapshotTree Tree()
    {
        var tree = new SnapshotOverlayTests.FakeSnapshotTree(); byte[] bytes = new byte[16384]; new Random(61006).NextBytes(bytes); tree.Files["file"] = bytes; return tree;
    }
    private static OverlayIdentity Identity(ISnapshotTree tree) => new(2, "base-reversion-process", tree.Snapshot, 1024L * 1024 * 1024, 1048576);
    private sealed class RecoveryBasis : IDisposable
    {
        private readonly JournalBaseReversionNativeEvidenceTests.Basis? native;
        public ISnapshotTree Tree { get; }
        public OverlayIdentity Identity { get; }
        public string File { get; }
        public byte[] Bytes { get; }
        public RecoveryBasis()
        {
            string? config = Environment.GetEnvironmentVariable("SPLINTER_BASE_REVERSION_NATIVE_RECOVERY");
            if (config is null)
            { var tree=JournalBaseReversionEvidenceTests.Tree();Tree=tree;Identity=JournalBaseReversionEvidenceTests.Identity(tree);File="file";Bytes=tree.Files[File]; }
            else
            {
                var input=JsonSerializer.Deserialize<JournalBaseReversionNativeEvidenceTests.Input>(System.IO.File.ReadAllText(config),Json)!;
                native=new(input.OverlayRequest);Tree=native.Tree;Identity=native.Request.Identity;File=input.Files[0];
                Bytes=JournalBaseReversionNativeEvidenceTests.Read(Tree,File,16384);
            }
        }
        public void Dispose() => native?.Dispose();
    }
    private static void WriteJson(string path, object data)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(stream, data, Json); stream.Flush(true);
    }
    private sealed class AppendGate(Action<string> gate) : IJournalMutationObserver
    {
        public bool Armed;
        public void Observe(JournalMutationBoundary boundary)
        { if (Armed && boundary.Boundary == "commit.after-flush") gate("append-flushed"); }
    }

    [Fact]
    public void Child()
    {
        string? path = Environment.GetEnvironmentVariable("SPLINTER_BASE_REVERSION_CHILD");
        if (path is null) return;
        var request = JsonSerializer.Deserialize<Request>(File.ReadAllText(path), Json)!;
        void Gate(string stage)
        {
            if (stage != request.Stage) return;
            WriteJson(Path.Combine(request.Directory, "signal.json"), new { request.Nonce, stage,
                pid = Environment.ProcessId, startTicks = Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks });
            Thread.Sleep(Timeout.Infinite);
        }
        using var basis = new RecoveryBasis(); var tree = basis.Tree; var identity = basis.Identity; string delta = Path.Combine(request.Directory, "delta");
        var observer = new AppendGate(Gate);
        using (var store = new JournalDeltaStore(delta, identity, tree, observer, optimizeJournal: false))
        {
            Guid id = store.Stat(basis.File)!.Id; store.Write(id, 0, new byte[16384]); store.Write(id, 0, Enumerable.Repeat((byte)42,16384).ToArray());
            WriteJson(Path.Combine(request.Directory, "ack-prefix.json"), new { entry = store.Get(id), seal = store.Seal() });
            observer.Armed = request.Stage == "append-flushed"; store.Write(id, 0, basis.Bytes);
            WriteJson(Path.Combine(request.Directory, "ack-final.json"), new { entry = store.Get(id), seal = store.Seal() });
        }
        var receipt = JournalDeltaStore.CompactDirectory(delta, identity, tree, Gate, revertBaseRanges: true);
        WriteJson(Path.Combine(request.Directory, "compacted.json"), receipt);
        JournalDeltaStore.CompleteCompaction(delta, identity, Gate);
        throw new InvalidOperationException("The requested process boundary was not reached.");
    }

    [Fact]
    public async Task ForcedBoundaryRecovery()
    {
        using var temporary = new TestDirectory();
        string? evidence = Environment.GetEnvironmentVariable("SPLINTER_BASE_REVERSION_RECOVERY_EVIDENCE");
        string root = evidence ?? Path.Combine(temporary.Path, "process-evidence");
        Assert.False(Directory.Exists(root)); Directory.CreateDirectory(root);
        var results = new List<object>();
        foreach (string stage in Stages)
        {
            string directory = Path.Combine(root, stage); Directory.CreateDirectory(directory);
            var request = new Request(directory, stage, Guid.NewGuid().ToString("N"));
            string input = Path.Combine(directory, "request.json"); WriteJson(input, request);
            var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardError = true, RedirectStandardOutput = true };
            start.ArgumentList.Add(typeof(JournalBaseReversionEvidenceTests).Assembly.Location);
            start.ArgumentList.Add("-method"); start.ArgumentList.Add("Splinter.Tests.JournalBaseReversionEvidenceTests.Child");
            start.Environment["SPLINTER_BASE_REVERSION_CHILD"] = input;
            using var child = Process.Start(start)!; long ticks = child.StartTime.ToUniversalTime().Ticks;
            var stdout = child.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken); var stderr = child.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
            try
            {
                string signal = Path.Combine(directory, "signal.json"); long began = Stopwatch.GetTimestamp();
                while (!File.Exists(signal) && !child.HasExited && Stopwatch.GetElapsedTime(began).TotalSeconds < 25) await Task.Delay(20, TestContext.Current.CancellationToken);
                Assert.True(File.Exists(signal), "The exact child did not reach its requested boundary.");
                // A created file can still be open for the durable signal publication.
                JsonElement observed = default;
                for (int i = 0; i < 50; i++)
                {
                    try { observed = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(signal)); break; }
                    catch (IOException ex) when ((ex.HResult & 65535) is 32 or 33) { await Task.Delay(20, TestContext.Current.CancellationToken); }
                }
                Assert.Equal(request.Nonce, observed.GetProperty("nonce").GetString());
                Assert.Equal(child.Id, observed.GetProperty("pid").GetInt32()); Assert.Equal(ticks, observed.GetProperty("startTicks").GetInt64());
                Assert.Equal(ticks, child.StartTime.ToUniversalTime().Ticks); child.Kill(); await child.WaitForExitAsync(TestContext.Current.CancellationToken);
                File.WriteAllText(Path.Combine(directory,"child.stdout.log"), await stdout); File.WriteAllText(Path.Combine(directory,"child.stderr.log"), await stderr);
                using var basis = new RecoveryBasis(); var tree = basis.Tree; var identity = basis.Identity; string delta = Path.Combine(directory, "delta");
                bool ackFinal = File.Exists(Path.Combine(directory, "ack-final.json"));
                var ack = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(directory, ackFinal ? "ack-final.json" : "ack-prefix.json")));
                DeltaEntry entry;
                using (var replay = new JournalDeltaStore(delta, identity, tree, optimizeJournal: false))
                {
                    entry = replay.Stat(basis.File)!; byte[] bytes = new byte[16384]; replay.Read(entry.Id, 0, bytes);
                    Assert.Equal(basis.Bytes, bytes); Assert.Equal(3, replay.Observe().DurableSequence);
                    var expected = ack.GetProperty("entry").Deserialize<DeltaEntry>(Json)!;
                    if (ackFinal) Assert.Equal(expected, entry);
                    else { Assert.Equal(expected.Id, entry.Id); Assert.Equal(expected.Length, entry.Length); }
                }
                var receipt = JournalDeltaStore.CompactDirectory(delta, identity, tree, revertBaseRanges: stage == "append-flushed" ? true : null);
                Assert.True(receipt.RevertBaseRanges || stage == "append-flushed");
                JournalDeltaStore.CompleteCompaction(delta, identity);
                using var final = new JournalDeltaStore(delta, identity, tree, optimizeJournal: false);
                byte[] result = new byte[16384]; final.Read(entry.Id, 0, result); Assert.Equal(basis.Bytes, result); Assert.Equal(entry, final.Get(entry.Id));
                results.Add(new { stage, childPid = child.Id, childStartTicks = ticks, forcedExitCode = child.ExitCode,
                    ackFinal, unacknowledgedCompleteWriteSurvived = !ackFinal, receipt, seal = final.Seal(), sha256 = Convert.ToHexStringLower(SHA256.HashData(result)) });
            }
            finally { if (!child.HasExited && child.StartTime.ToUniversalTime().Ticks == ticks) { child.Kill(); await child.WaitForExitAsync(TestContext.Current.CancellationToken); } }
        }
        WriteJson(Path.Combine(root, "result.json"), new { qualification = Environment.GetEnvironmentVariable("SPLINTER_BASE_REVERSION_NATIVE_RECOVERY") is null
            ? "actual-NTFS-files-and-forced-child-exit; synthetic-immutable-basis; no-power-loss"
            : "actual-NTFS-files-and-forced-child-exit; owned-recorded-large-project-Git-basis; offline-compaction; no-WinFsp-kill/power-loss", passed = results.Count, results });
    }

    [Fact]
    public void Benchmark()
    {
        string? root = Environment.GetEnvironmentVariable("SPLINTER_BASE_REVERSION_BENCHMARK");
        if (root is null) return;
        Assert.False(Directory.Exists(root)); Directory.CreateDirectory(root);
        var results = new List<object>();
        foreach (string profile in new[] { "random-4k", "random-1m", "repeat-1m", "mixed-8x4k" })
        for (int repeat = 0; repeat < 3; repeat++)
        foreach (bool enabled in repeat == 1 ? new[] { true, false } : new[] { false, true })
        {
            string directory = Path.Combine(root, $"{profile}-{repeat}-{enabled}");
            var tree = new SnapshotOverlayTests.FakeSnapshotTree(); int size = profile.Contains("1m") ? 1048576 : 4096;
            int files = profile == "mixed-8x4k" ? 8 : 1; var random = new Random(61006);
            byte[] a = new byte[size], b = new byte[size], c = new byte[size]; random.NextBytes(a); random.NextBytes(b); random.NextBytes(c);
            if (profile.StartsWith("repeat")) { Array.Fill(a,(byte)65); Array.Fill(b,(byte)66); Array.Fill(c,(byte)67); }
            for (int file = 0; file < files; file++) tree.Files[$"file-{file}"] = a;
            var identity = new OverlayIdentity(2, "base-benchmark", tree.Snapshot, 1024L*1024*1024, 1048576);
            var expected = new Dictionary<string, DeltaEntry>(); long writeTicks, writeAllocated;
            long allocated = GC.GetTotalAllocatedBytes(true), began = Stopwatch.GetTimestamp();
            using (var store = new JournalDeltaStore(directory, identity, tree, optimizeJournal: false))
            {
                var ids = Enumerable.Range(0,files).Select(i => store.Stat($"file-{i}")!.Id).ToArray();
                for (int cycle = 0; cycle < 32; cycle++) foreach (var id in ids) { store.Write(id,0,b); store.Write(id,0,c); store.Write(id,0,a); }
                if (profile.StartsWith("mixed")) foreach (var id in ids) store.Write(id,2048,b.AsSpan(2048));
                writeTicks = Stopwatch.GetTimestamp()-began; writeAllocated = GC.GetTotalAllocatedBytes(true)-allocated;
                for (int i = 0; i < files; i++) expected[$"file-{i}"] = store.Get(ids[i]);
            }
            long beforeAllocation = Allocation(Path.Combine(directory,"delta.journal"));
            allocated = GC.GetTotalAllocatedBytes(true); began = Stopwatch.GetTimestamp();
            var receipt = JournalDeltaStore.CompactDirectory(directory, identity, tree, revertBaseRanges: enabled);
            long compactTicks = Stopwatch.GetTimestamp()-began, compactAllocated = GC.GetTotalAllocatedBytes(true)-allocated;
            long afterAllocation = Allocation(Path.Combine(directory,"delta.journal"));
            began = Stopwatch.GetTimestamp(); ValidationCacheUsage usage; SnapshotDeltaSeal seal; OverlayStorageObservation observation;
            using (var replay = new JournalDeltaStore(directory, identity, tree, optimizeJournal: false))
            {
                long replayTicks = Stopwatch.GetTimestamp()-began; usage = replay.ObservePrivateUsage(); seal = replay.Seal();
                foreach (var (path, entry) in expected)
                {
                    Assert.Equal(entry,replay.Stat(path)); byte[] result = new byte[size]; replay.Read(entry.Id,0,result);
                    byte[] wanted = (byte[])a.Clone(); if (profile.StartsWith("mixed")) b.AsSpan(2048).CopyTo(wanted.AsSpan(2048)); Assert.Equal(wanted,result);
                }
                observation = replay.Observe();
                results.Add(new { profile, repeat, enabled, size, files, cycles = 32, writeTicks, compactTicks, replayTicks,
                    stopwatchFrequency = Stopwatch.Frequency, writeAllocated, compactAllocated, managedHeap = GC.GetGCMemoryInfo().HeapSizeBytes,
                    beforeAllocation, afterAllocation, oldNewSampleAllocation = beforeAllocation+afterAllocation, receipt, usage, seal, observation });
            }
            JournalDeltaStore.CompleteCompaction(directory, identity);
        }
        WriteJson(Path.Combine(root,"result.json"), new { osCache = "not-evicted; first profile cold-cache status unknown; later warm possible", order = "OFF/ON, ON/OFF, OFF/ON", results });
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StandardInfo { public long AllocationSize, EndOfFile; public uint Links; [MarshalAs(UnmanagedType.U1)] public bool DeletePending, Directory; }
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle file, int informationClass, out StandardInfo info, uint size);
    internal static long Allocation(string path)
    { using var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite|FileShare.Delete); Assert.True(GetFileInformationByHandleEx(handle,1,out var info,(uint)Marshal.SizeOf<StandardInfo>())); return info.AllocationSize; }
}
