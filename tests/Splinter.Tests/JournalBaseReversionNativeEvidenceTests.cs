using System.Diagnostics;
using System.Text.Json;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

// Explicit evidence against an already-owned recorded product basis. No source
// capture, install, Editor, provider stop, or source-project writes occur here.
public sealed class JournalBaseReversionNativeEvidenceTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public sealed record Input(string OverlayRequest, string Output, string[] Files);
    internal sealed class Basis : IDisposable
    {
        public readonly OverlayServeRequest Request;
        private readonly IDisposable lease;
        public readonly WorkspaceSnapshotBasis.DisposableSnapshotTree Tree;
        public Basis(string path)
        {
            Request = JsonSerializer.Deserialize<OverlayServeRequest>(File.ReadAllText(path),Json)!;
            if (Request.Identity.Snapshot.Provider != "git-objects" || Request.Identity.Graft is not null ||
                Request.ReadAccess is null || Request.Identity.Version != 2)
                throw new InvalidOperationException("This bounded evidence requires an owned recorded Git basis without a graft.");
            lease = WorkspaceSnapshotBasis.AcquireReadLease(Request.Identity.Snapshot, Request.SnapshotReceiptDirectory, Request.SnapshotOwnerId);
            try
            {
                var health = WinFspOverlayClient.ObserveSnapshotAsync(Request).GetAwaiter().GetResult();
                if (health.Available != true) throw new WorkspaceException("SnapshotVerificationRequired", "Preserve the recorded basis.");
                Tree = WorkspaceSnapshotBasis.OpenTree(Request.Identity.Snapshot, Request.ReadAccess, Request.SnapshotReceiptDirectory, Request.SnapshotOwnerId, () => health);
            }
            catch { lease.Dispose(); throw; }
        }
        public void Dispose() { Tree.Dispose(); lease.Dispose(); }
    }
    internal static byte[] Read(ISnapshotTree tree, string path, int length)
    {
        byte[] bytes = new byte[length]; int read = 0;
        while (read < length) { int got = tree.Read(path,read,bytes.AsSpan(read,Math.Min(1048576,length-read))); if (got<=0) throw new IOException("Short immutable read."); read += got; }
        return bytes;
    }
    [Fact]
    public void Benchmark()
    {
        string? config = Environment.GetEnvironmentVariable("SPLINTER_BASE_REVERSION_NATIVE_BENCHMARK");
        if (config is null) return;
        var input = JsonSerializer.Deserialize<Input>(File.ReadAllText(config),Json)!;
        Assert.False(Directory.Exists(input.Output)); Directory.CreateDirectory(input.Output);
        var results = new List<object>();
        for (int repeat = 0; repeat < 3; repeat++)
        foreach (bool enabled in repeat == 1 ? new[] {true,false} : new[] {false,true})
        {
            string directory = Path.Combine(input.Output,$"{repeat}-{enabled}");
            var expected = new Dictionary<string,DeltaEntry>(); var contents = new Dictionary<string,byte[]>();
            var latencies = new List<long>(); long preparationTicks, writeTicks, writeAllocated; OverlayIdentity identity;
            long began = Stopwatch.GetTimestamp();
            using (var basis = new Basis(input.OverlayRequest))
            {
                identity = basis.Request.Identity;
                foreach (string path in input.Files)
                { var entry = basis.Tree.Stat(path)!; Assert.InRange(entry.Length,1048576,16777216); contents[path] = Read(basis.Tree,path,(int)entry.Length); }
                preparationTicks = Stopwatch.GetTimestamp()-began;
                long allocated = GC.GetTotalAllocatedBytes(true); began = Stopwatch.GetTimestamp();
                using var store = new JournalDeltaStore(directory,identity,basis.Tree);
                for (int index=0;index<input.Files.Length;index++)
                {
                    string path = input.Files[index]; int length = index==0 ? 4096 : 1048576;
                    byte[] original = contents[path][..length], b = (byte[])original.Clone(), c = (byte[])original.Clone();
                    for (int i=0;i<length;i++) { b[i]^=0x55; c[i]^=0xAA; }
                    Guid id = store.Stat(path)!.Id;
                    for (int cycle=0;cycle<32;cycle++) foreach (byte[] bytes in new[] {b,c,original})
                    { long at=Stopwatch.GetTimestamp(); store.Write(id,0,bytes); latencies.Add(Stopwatch.GetTimestamp()-at); }
                    // A retained changed half and a baseline half in one write.
                    if (index==input.Files.Length-1)
                    { byte[] mixed=(byte[])original.Clone(); b.AsSpan(length/2).CopyTo(mixed.AsSpan(length/2)); store.Write(id,0,mixed); mixed.CopyTo(contents[path],0); }
                    store.SetMetadata(id,32,101,202,303); expected[path] = store.Get(id);
                }
                writeTicks=Stopwatch.GetTimestamp()-began;writeAllocated=GC.GetTotalAllocatedBytes(true)-allocated;
            }
            long beforeAllocation=JournalBaseReversionEvidenceTests.Allocation(Path.Combine(directory,"delta.journal"));
            began=Stopwatch.GetTimestamp(); long startAllocation=GC.GetTotalAllocatedBytes(true); SnapshotJournalCompaction receipt;
            // A fresh reader, as on the actual stopped-provider compact path:
            // the seed read's blob proof is not retained for base comparison.
            using (var basis=new Basis(input.OverlayRequest)) receipt=JournalDeltaStore.CompactDirectory(directory,identity,basis.Tree,revertBaseRanges:enabled);
            long compactTicks=Stopwatch.GetTimestamp()-began,compactAllocated=GC.GetTotalAllocatedBytes(true)-startAllocation;
            long afterAllocation=JournalBaseReversionEvidenceTests.Allocation(Path.Combine(directory,"delta.journal"));
            began=Stopwatch.GetTimestamp(); ValidationCacheUsage usage;long replayTicks;SnapshotDeltaSeal seal;
            using (var basis=new Basis(input.OverlayRequest))
            using (var replay=new JournalDeltaStore(directory,identity,basis.Tree))
            {
                replayTicks=Stopwatch.GetTimestamp()-began;usage=replay.ObservePrivateUsage();seal=replay.Seal();
                foreach (string path in input.Files)
                {
                    Assert.Equal(expected[path],replay.Stat(path)); byte[] result=new byte[contents[path].Length];int offset=0;
                    while (offset<result.Length) { int count=replay.Read(expected[path].Id,offset,result.AsSpan(offset,Math.Min(1048576,result.Length-offset)));Assert.True(count>0);offset+=count; }
                    Assert.Equal(contents[path],result);
                }
            }
            JournalDeltaStore.CompleteCompaction(directory,identity);
            latencies.Sort();results.Add(new {repeat,enabled,files=input.Files,cycles=32,preparationTicks,writeTicks,compactTicks,replayTicks,
                stopwatchFrequency=Stopwatch.Frequency,writeAllocated,compactAllocated,writeP50=latencies[latencies.Count/2],writeP95=latencies[(int)(latencies.Count*.95)],
                beforeAllocation,afterAllocation,oldNewSampleAllocation=beforeAllocation+afterAllocation,receipt,usage,seal});
        }
        File.WriteAllText(Path.Combine(input.Output,"result.json"),JsonSerializer.Serialize(new {qualification="real-large-Unity-assets/fixed-Git-provider/actual-NTFS-journals; offline-compaction; WinFsp product peer qualified separately",osCache="not-evicted; warm/cold unknown; fixed counterbalanced order",results},Json));
    }
}
