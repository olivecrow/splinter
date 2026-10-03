using System.Diagnostics;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class WorkspaceLockTests
{
    [Fact]
    public async Task MissingLockDirectoryIsUnavailableRatherThanAnotherWorkspaceOwner()
    {
        using var d = new TestDirectory();
        var store = new WorkspaceStore(Path.Combine(d.Path, "state"));
        Directory.Delete(Path.Combine(store.Root, "locks"));
        var elapsed = Stopwatch.StartNew();
        var error = await Assert.ThrowsAsync<WorkspaceException>(() => store.LockAsync(WorkspaceStore.Key("missing"), TestContext.Current.CancellationToken));
        Assert.Equal("StateReadUnavailable", error.Code);
        Assert.Equal("requires-user-action", WorkspaceFailurePolicy.RetryDisposition(error.Code));
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(2), "A missing directory must not enter the five-second contention retry.");
        Assert.False(Directory.Exists(Path.Combine(store.Root, "locks")));
    }

    [Fact]
    public async Task ExistingDirectoryAtLockPathIsUnavailableAndPreserved()
    {
        using var d = new TestDirectory();
        var store = new WorkspaceStore(Path.Combine(d.Path, "state"));
        string key = WorkspaceStore.Key("directory"), path = Path.Combine(store.Root, "locks", key + ".lock");
        Directory.CreateDirectory(path);
        string marker = Path.Combine(path, "preserve"); File.WriteAllText(marker, "owned evidence");
        var error = await Assert.ThrowsAsync<WorkspaceException>(() => store.LockAsync(key, TestContext.Current.CancellationToken));
        Assert.Equal("StateReadUnavailable", error.Code);
        Assert.Equal("owned evidence", File.ReadAllText(marker));
    }

    [Fact]
    public async Task WaitingForSameKeyDoesNotBlockDifferentKeyAndReusesLockAfterOwnerCloses()
    {
        using var d = new TestDirectory();
        var store = new WorkspaceStore(Path.Combine(d.Path, "state"));
        string key = WorkspaceStore.Key("a"), peer = WorkspaceStore.Key("b");
        using var owner = await store.LockAsync(key, TestContext.Current.CancellationToken);
        var waiting = store.LockAsync(key, TestContext.Current.CancellationToken);
        using var independent = await store.LockAsync(peer, TestContext.Current.CancellationToken);
        Assert.False(waiting.IsCompleted);
        owner.Dispose();
        using var acquired = await waiting.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.Equal(owner.Name, acquired.Name);
    }

    [Fact]
    public async Task CancelledWaitPreservesOwnerAndSubsequentSameKeyRequest()
    {
        using var d = new TestDirectory();
        var store = new WorkspaceStore(Path.Combine(d.Path, "state"));
        string key = WorkspaceStore.Key("cancelled");
        using var owner = await store.LockAsync(key, TestContext.Current.CancellationToken);
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var waiting = store.LockAsync(key, cancelled.Token); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        Assert.True(owner.CanWrite);
        owner.Dispose();
        using var acquired = await store.LockAsync(key, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SustainedSharingViolationRemainsBusyAndDoesNotReplaceOwnerFile()
    {
        using var d = new TestDirectory();
        var store = new WorkspaceStore(Path.Combine(d.Path, "state"));
        string key = WorkspaceStore.Key("busy");
        using var owner = await store.LockAsync(key, TestContext.Current.CancellationToken);
        owner.Write([42]); owner.Flush(true);
        var error = await Assert.ThrowsAsync<WorkspaceException>(() => store.LockAsync(key, TestContext.Current.CancellationToken));
        Assert.Equal("WorkspaceBusy", error.Code);
        Assert.Equal("safe-to-retry", WorkspaceFailurePolicy.RetryDisposition(error.Code));
        owner.Position = 0; Assert.Equal(42, owner.ReadByte());
    }
}
