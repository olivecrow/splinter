using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed partial class SnapshotWorkspaceCoordinatorTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IndependentTasksOverlapPreparationAndOneFailureDoesNotBlockItsPeer(bool loseAttachReply)
    {
        using var f = new Fixture();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        var ct = deadline.Token;
        var firstSource = new Source { LoseReply = loseAttachReply ? "attach" : null };
        var secondSource = new Source();
        var firstBackend = new Backend(); var secondBackend = new Backend();
        var firstGate = new MountGate(); var secondGate = new MountGate();
        var first = new WorkspaceCoordinator(new(f.Home), firstSource, new App(), firstBackend, firstGate);
        var second = new WorkspaceCoordinator(new(f.Home), secondSource, new App(), secondBackend, secondGate);
        Task<WorkspaceView> Ensure(WorkspaceCoordinator coordinator, string key) =>
            coordinator.EnsureAsync(f.Project, key, "base", "git-snapshot", false, ct);

        Task<WorkspaceView> a = Ensure(first, "parallel-a");
        Task<WorkspaceView> b = Ensure(second, "parallel-b");
        try
        {
            // Both tasks must reach mount preparation while the other task is
            // still held there; an elapsed-time assertion cannot prove overlap.
            await Task.WhenAll(firstGate.Entered.Task, secondGate.Entered.Task).WaitAsync(ct);
            Assert.False(a.IsCompleted); Assert.False(b.IsCompleted);
        }
        finally { firstGate.Resume.TrySetResult(); secondGate.Resume.TrySetResult(); }

        WorkspaceView peer = await b;
        WorkspaceView result;
        if (loseAttachReply)
        {
            Assert.Equal("ReplyLost", (await Assert.ThrowsAsync<WorkspaceException>(() => a)).Code);
            var saved = new WorkspaceStore(f.Home).List().Single(w => w.RequestKey == "parallel-a");
            Assert.Equal("ready", peer.Workspace.Phase);
            Assert.False(secondBackend.DeltaRemoved); Assert.True(secondBackend.Mounted);
            result = await Ensure(first, "parallel-a");
            Assert.Equal(saved.Id, result.Workspace.Id);
        }
        else result = await a;

        Assert.NotEqual(result.Workspace.Id, peer.Workspace.Id);
        Assert.NotEqual(result.Workspace.Path, peer.Workspace.Path);
        Assert.Equal(1, firstBackend.Captures); Assert.Equal(1, secondBackend.Captures);
        await first.CheckpointAsync(result.Workspace.Id, "first result", ct);
        var released = await first.ReleaseAsync(result.Workspace.Id, ct);
        Assert.Equal("released", released.Workspace.Phase);
        Assert.Equal("ready", new WorkspaceStore(f.Home).Get(peer.Workspace.Id).Phase);
        Assert.True(secondBackend.Mounted); Assert.False(secondBackend.DeltaRemoved);
        Assert.Equal(peer.Workspace.Id, (await Ensure(second, "parallel-b")).Workspace.Id);
        await second.CheckpointAsync(peer.Workspace.Id, "peer result", ct);
        Assert.Equal("released", (await second.ReleaseAsync(peer.Workspace.Id, ct)).Workspace.Phase);
    }

    private sealed class MountGate : IWorkspacePreparationObserver
    {
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Resume = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task ReachedAsync(string boundary, WorkspaceRecord workspace, CancellationToken ct = default)
        {
            if (boundary != "mount.before-effect") return;
            Entered.TrySetResult();
            await Resume.Task.WaitAsync(ct);
        }
    }
}
