using System.Security.Cryptography;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class JournalWriteMetricsTests
{
    [Fact]
    public void LiveCostsSeparateMutationCallsDurableCommitsAndDetachedObservations()
    {
        using var dir = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        using var store = new JournalDeltaStore(dir.Path, new(2, "live-costs", tree.Snapshot, 65536, 4096), tree, optimizeJournal: false);
        var file = store.Create("file", false, 32);
        store.Write(file.Id, 0, [1, 2, 3, 4]); store.SetLength(file.Id, 8);
        store.SetMetadata(file.Id, 32, 1, 1, 2); store.Rename(file.Id, "renamed", false);
        store.Write(file.Id, 0, []); store.SetLength(file.Id, 8);
        var current = store.Get(file.Id);
        store.SetMetadata(file.Id, current.Attributes, current.CreationTime, current.LastAccessTime, current.LastWriteTime);
        Assert.Throws<IOException>(() => store.Create("renamed", false, 32));
        store.Delete(file.Id);
        var metrics = store.Observe().Metrics!; var live = metrics.LiveJournalWrites!;
        Assert.Equal(10, live.MutationGateCalls);
        Assert.Equal(6, live.Operations.Length);
        Assert.Equal(metrics.CommitCount, live.Operations.Sum(x => x.DurableCommits));
        Assert.Equal(metrics.JournalAppendedBytes, live.Operations.Sum(x => x.JournalBytes));
        Assert.Equal(metrics.FlushCount, live.Operations.Sum(x => x.FlushCalls));
        Assert.Equal(metrics.FlushTicks, live.Operations.Sum(x => x.FlushTicks));
        Assert.Equal(4, live.Operations.Sum(x => x.PayloadBytes));
        Assert.All(live.Operations, cost =>
        {
            Assert.Equal(1, cost.Attempts); Assert.Equal(1, cost.DurableCommits); Assert.Equal(0, cost.FailedCalls);
            Assert.Equal(0, cost.MaxWaitingMutations);
            Assert.Equal(new long[] { 1, 0, 0, 0, 0 }, cost.WaitingMutationsAtFlushStart);
            Assert.Equal(new long[] { 1, 0, 0, 0, 0 }, cost.WaitingMutationsAtFlushEnd);
            Assert.True(cost.TotalTicks >= cost.PrepareTicks + cost.AppendTicks + cost.FlushTicks + cost.ApplyTicks);
        });
        live.Operations[0].WaitingMutationsAtFlushStart[0] = 99;
        live.Operations[0].WaitingMutationsAtFlushEnd[0] = 99;
        Assert.Equal(1, store.Observe().Metrics!.LiveJournalWrites!.Operations[0].WaitingMutationsAtFlushStart[0]);
        Assert.Equal(1, store.Observe().Metrics!.LiveJournalWrites!.Operations[0].WaitingMutationsAtFlushEnd[0]);
        store.Flush();
        Assert.Equal(7, store.Observe().Metrics!.FlushCount);
        Assert.Equal(6, store.Observe().Metrics!.LiveJournalWrites!.Operations.Sum(x => x.FlushCalls));
    }

    [Fact]
    public void RejectedAppendHasNoDurableCommitAndReplayStartsWithEmptyLiveCosts()
    {
        using var dir = new TestDirectory(); var tree = new SnapshotOverlayTests.FakeSnapshotTree();
        var identity = new OverlayIdentity(2, "rejected-write", tree.Snapshot, 4096, 4096);
        string journal = Path.Combine(dir.Path, "delta.journal");
        byte[] HashJournal()
        {
            using var input = new FileStream(journal, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return SHA256.HashData(input);
        }
        using (var store = new JournalDeltaStore(dir.Path, identity, tree, optimizeJournal: false))
        {
            var file = store.Create("file", false, 32);
            byte[] before = HashJournal();
            byte[] rejectedPayload = new byte[4096]; new Random(538).NextBytes(rejectedPayload);
            var error = Assert.Throws<WorkspaceException>(() => store.Write(file.Id, 0, rejectedPayload));
            Assert.Equal("DeltaFull", error.Code);
            var rejected = Assert.Single(store.Observe().Metrics!.LiveJournalWrites!.Operations, x => x.Operation == "write");
            Assert.Equal(1, rejected.Attempts); Assert.Equal(1, rejected.FailedCalls);
            Assert.Equal(0, rejected.DurableCommits); Assert.Equal(0, rejected.FlushCalls);
            Assert.Equal(0, rejected.JournalBytes); Assert.Equal(0, rejected.PayloadBytes);
            Assert.All(rejected.WaitingMutationsAtFlushStart, count => Assert.Equal(0, count));
            Assert.Equal(before, HashJournal());
        }
        using var resumed = new JournalDeltaStore(dir.Path, identity, tree, optimizeJournal: false);
        Assert.Equal(0, resumed.Stat("file")!.Length);
        Assert.Equal(1, resumed.Observe().DurableSequence);
        Assert.Empty(resumed.Observe().Metrics!.LiveJournalWrites!.Operations);
        Assert.Equal(0, resumed.Observe().Metrics!.LiveJournalWrites!.MutationGateCalls);
    }
}
