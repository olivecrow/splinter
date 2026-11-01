using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed partial class FileWorkspaceTests
{
    [Fact]
    public async Task WholeWriteSetIsPinnedBeforeBoundedParallelPublicationAndStagingReservation()
    {
        RequireWindowsApply(); OriginalApplyPlan? plan = null; int admitted = 0, active = 0, peak = 0; long staged = 0, peakStaged = 0;
        using var admission = new CountdownEvent(8); using var twoStages = new CountdownEvent(2); int stages = 0;
        var publisher = new WindowsOriginalFilePublisher((point, index) =>
        {
            if (point == "target-locked")
            {
                Interlocked.Increment(ref admitted); int now = Interlocked.Increment(ref active);
                int prior;
                do { prior = Volatile.Read(ref peak); if (prior >= now) break; } while (Interlocked.CompareExchange(ref peak, now, prior) != prior);
                admission.Signal(); Assert.True(admission.Wait(TimeSpan.FromSeconds(20))); Interlocked.Decrement(ref active);
            }
            if (point == "stage-created")
            {
                Assert.Equal(8, Volatile.Read(ref admitted));
                long now = Interlocked.Add(ref staged, plan!.ChangeAt(index).After!.Length);
                long prior;
                do { prior = Interlocked.Read(ref peakStaged); if (prior >= now) break; } while (Interlocked.CompareExchange(ref peakStaged, now, prior) != prior);
                if (Interlocked.Increment(ref stages) <= 2) { twoStages.Signal(); Assert.True(twoStages.Wait(TimeSpan.FromSeconds(5))); }
            }
            if (point == "staged") Interlocked.Add(ref staged, -plan!.ChangeAt(index).After!.Length);
        });
        using var f = new Fixture(publisher); var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure("parallel-original", ct)).Workspace;
        for (int i = 0; i < 8; i++) File.WriteAllText(Path.Combine(w.Path, "created-" + i + ".txt"), "same");
        var result = (await f.New().CheckpointAsync(w.Id, "parallel", "parallel-result", ct)).ResultGeneration!;
        plan = (await f.New().PlanOriginalAsync(w.Id, result.Id, null, ct)).OriginalApplyPlan!;
        Assert.Equal(8, plan.ChangeCount); Assert.Equal(3, plan.Version); Assert.Equal(4, w.Version); Assert.NotNull(plan.ChangesRoot);
        await f.New().ApplyOriginalAsync(w.Id, result.Id, "parallel", plan.Digest, null, ct);
        Assert.InRange(peak, 2, 8); Assert.InRange(peakStaged, 8, 32); Assert.Equal(0, staged);
        var metrics = publisher.LastPublicationMetrics!;
        Assert.Equal(8, metrics.AdmittedTargets); Assert.Equal(0, metrics.RemainingWorkers);
        Assert.InRange(metrics.PeakPublicationWorkers, 1, 8); Assert.True(metrics.PeakNativeHandles >= 8);
        Assert.Equal(4, metrics.MaximumSinglePayloadBytes); Assert.InRange(metrics.PeakReservedPayloadBytes, 8, 4 * StorageResources.IoWorkers);
        for (int i = 0; i < 8; i++) Assert.Equal("same", File.ReadAllText(Path.Combine(f.Project, "created-" + i + ".txt")));
    }
    [Fact]
    public async Task ParallelPublicationFailureDrainsWorkersBeforeReturningAndSameIntentResumes()
    {
        RequireWindowsApply(); int inCallback = 0, armed = 1;
        var publisher = new WindowsOriginalFilePublisher((point, _) =>
        {
            if (point != "staged") return;
            Interlocked.Increment(ref inCallback);
            try
            {
                if (Interlocked.Exchange(ref armed, 0) == 1) throw new WorkspaceException("InjectedParallelFailure", "Preserve the fixed original publication.");
                Thread.Sleep(15);
            }
            finally { Interlocked.Decrement(ref inCallback); }
        });
        using var f = new Fixture(publisher); var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure("parallel-drain", ct)).Workspace;
        for (int i = 0; i < 8; i++) File.WriteAllBytes(Path.Combine(w.Path, "empty-" + i + ".bin"), []);
        var result = (await f.New().CheckpointAsync(w.Id, "parallel", "parallel-result", ct)).ResultGeneration!;
        var plan = (await f.New().PlanOriginalAsync(w.Id, result.Id, null, ct)).OriginalApplyPlan!;
        Assert.Equal(3, plan.Version); Assert.NotNull(plan.ChangesRoot);
        Assert.Equal("InjectedParallelFailure", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ApplyOriginalAsync(w.Id, result.Id, "fixed", plan.Digest, null, ct))).Code);
        Assert.Equal(0, Volatile.Read(ref inCallback)); Assert.False(f.Store.Get(w.Id).OriginalApply!.Completed);
        Assert.Equal(0, publisher.LastPublicationMetrics!.RemainingWorkers);
        Assert.True((await f.New().ApplyOriginalAsync(w.Id, result.Id, "fixed", plan.Digest, null, ct)).Workspace.OriginalApply!.Completed);
        Assert.Empty(Directory.GetFiles(f.Project, ".splinter-apply-*", SearchOption.AllDirectories));
    }
    [Theory]
    [InlineData("stage-created")] [InlineData("staged")]
    [InlineData("retired-before-receipt")] [InlineData("published-before-receipt")]
    public async Task PagedPublicationSpaceFailureDrainsAllWorkersAndResumesTheSameGeneration(string boundary)
    {
        RequireWindowsApply(); int active = 0, armed = 1;
        var publisher = new WindowsOriginalFilePublisher((point, _) =>
        {
            if (point != boundary) return; Interlocked.Increment(ref active);
            try { if (Interlocked.Exchange(ref armed, 0) == 1) throw new IOException("Injected backing-store exhaustion.", unchecked((int)0x80070070)); }
            finally { Interlocked.Decrement(ref active); }
        });
        using var f = new Fixture(publisher); var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure("paged-space-drain", ct)).Workspace;
        File.WriteAllText(Path.Combine(w.Path, "data.txt"), "updated basis file");
        for (int i = 0; i < 12; i++) File.WriteAllBytes(Path.Combine(w.Path, "item-" + i.ToString("D2") + ".bin"), new byte[(i + 1) * 1024]);
        var result = (await f.New().CheckpointAsync(w.Id, "fixed", "space-result", ct)).ResultGeneration!;
        var plan = (await f.New().PlanOriginalAsync(w.Id, result.Id, null, ct)).OriginalApplyPlan!;
        Assert.Equal(3, plan.Version); Assert.Equal(13, plan.ChangeCount);
        long stagingLimit = StorageResources.PublicationStagingBytes(12 * 1024,
            new DriveInfo(Path.GetPathRoot(f.Project)!).AvailableFreeSpace);
        long totalPayload = plan.EnumerateChanges().Sum(change => change.After?.Length ?? 0);
        Assert.Equal(unchecked((int)0x80070070), (await Assert.ThrowsAsync<IOException>(() => f.New().ApplyOriginalAsync(w.Id, result.Id, "space-apply", plan.Digest, null, ct))).HResult);
        Assert.Equal(0, active); Assert.Equal(0, publisher.LastPublicationMetrics!.RemainingWorkers);
        Assert.Equal(12 * 1024, publisher.LastPublicationMetrics.MaximumSinglePayloadBytes);
        Assert.InRange(publisher.LastPublicationMetrics.PeakReservedPayloadBytes, 0, Math.Min(totalPayload, stagingLimit));
        Assert.Equal("OriginalApplyPending", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().CheckpointAsync(w.Id, "later", "later", ct))).Code);
        var resumed = await f.New().ApplyOriginalAsync(w.Id, result.Id, "space-apply", plan.Digest, null, ct);
        Assert.True(resumed.OriginalApplyReceipt!.Completed); Assert.Single(resumed.Workspace.Results); Assert.Single(resumed.Workspace.OriginalApplies);
        Assert.Equal("updated basis file", File.ReadAllText(Path.Combine(f.Project, "data.txt")));
        for (int i = 0; i < 12; i++) Assert.Equal((i + 1) * 1024, new FileInfo(Path.Combine(f.Project, "item-" + i.ToString("D2") + ".bin")).Length);
        File.WriteAllText(Path.Combine(f.Project, "data.txt"), "later original");
        await f.New().ApplyOriginalAsync(w.Id, result.Id, "space-apply", plan.Digest, null, ct);
        Assert.Equal("later original", File.ReadAllText(Path.Combine(f.Project, "data.txt")));
        Assert.Empty(Directory.GetFiles(f.Project, ".splinter-apply-*", SearchOption.AllDirectories));
    }
    [Fact]
    public async Task RetainedLargeStageFinishesBeforeANewTemporaryPayloadStartsAfterFailure()
    {
        RequireWindowsApply(); bool recovering = false, retainedPublished = false; int failed = -1;
        var publisher = new WindowsOriginalFilePublisher((point, index) =>
        {
            if (!recovering && point == "staged" && Interlocked.CompareExchange(ref failed, index, -1) == -1)
                throw new IOException("Injected stage exhaustion.", unchecked((int)0x80070070));
            if (recovering && point == "published" && index == failed) retainedPublished = true;
            if (recovering && point == "stage-created" && index != failed) Assert.True(retainedPublished);
        });
        using var f = new Fixture(publisher); var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure("retained-stage-budget", ct)).Workspace;
        File.WriteAllBytes(Path.Combine(w.Path, "00-large.bin"), new byte[65536]); File.WriteAllBytes(Path.Combine(w.Path, "01-large.bin"), new byte[65536]);
        var result = (await f.New().CheckpointAsync(w.Id, "fixed", "retained-result", ct)).ResultGeneration!;
        var plan = (await f.New().PlanOriginalAsync(w.Id, result.Id, null, ct)).OriginalApplyPlan!;
        await Assert.ThrowsAsync<IOException>(() => f.New().ApplyOriginalAsync(w.Id, result.Id, "retained-apply", plan.Digest, null, ct));
        Assert.InRange(Directory.GetFiles(f.Project, ".splinter-apply-*.new").Length, 1, 2);
        Assert.Equal(0, publisher.LastPublicationMetrics!.RemainingWorkers);
        recovering = true;
        await f.New().ApplyOriginalAsync(w.Id, result.Id, "retained-apply", plan.Digest, null, ct);
        Assert.True(retainedPublished); Assert.InRange(publisher.LastPublicationMetrics!.PeakReservedPayloadBytes, 0, 65536);
        Assert.Empty(Directory.GetFiles(f.Project, ".splinter-apply-*"));
    }
}
