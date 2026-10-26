using Splinter.Core;
using Splinter.SourceControl.Files;

namespace Splinter.Tests;

public sealed partial class FileWorkspaceTests
{
    [Theory]
    [InlineData("none")]
    [InlineData("plastic")]
    public async Task CombinedCollectionKeepsPreviousRevisionUntilCoordinatorProvesStableSeal(string kind)
    {
        using var f = new Fixture(sourceKind: kind); var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure("combined", ct)).Workspace;
        File.WriteAllText(Path.Combine(w.Path, "data.txt"), "changed");
        f.Backend.ChangeSeal = true;
        Assert.Equal("DeltaChangedAfterCollection", (await Assert.ThrowsAsync<WorkspaceException>(
            () => f.New().CheckpointAsync(w.Id, "changed", ct))).Code);
        var failed = f.Store.Get(w.Id);
        Assert.Null(failed.ResultRevision); Assert.Null(failed.Snapshot!.Result);
        Assert.True(Directory.Exists(w.Path)); Assert.DoesNotContain(w.Id, f.Backend.Removed);
        f.Backend.ChangeSeal = false;
        var recovered = (await f.New().CheckpointAsync(w.Id, "changed", ct)).Workspace;
        Assert.True(recovered.Snapshot!.Result!.IndependentOfSnapshot);
        Assert.NotEqual(recovered.BaseRevision, recovered.ResultRevision);
        await f.New().ReleaseAsync(w.Id, ct);
        FileWorkspaceProvider.VerifyIndependentResult(f.Store.Get(w.Id), f.Store.ControlPath(w.Id));
    }

    [Fact]
    public async Task CorruptInterruptedPayloadCannotPublishACombinedResult()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        for (int i = 0; i < 24; i++) File.WriteAllText(Path.Combine(f.Project, $"file{i}"), new string((char)('a' + i), 65536));
        var w = (await f.Ensure("pending", ct)).Workspace;
        string objects = Path.Combine(f.Store.ControlPath(w.Id), "files", "objects"); Directory.CreateDirectory(objects);
        string hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData("basis"u8));
        string pending = Path.Combine(objects, hash + ".pending"); File.WriteAllText(pending, "corrupt");
        Assert.Equal("FileResultIncomplete", (await Assert.ThrowsAsync<WorkspaceException>(
            () => f.New().CheckpointAsync(w.Id, "result", ct))).Code);
        Assert.Null(f.Store.Get(w.Id).Snapshot!.Result);
        Assert.False(File.Exists(Path.Combine(f.Store.ControlPath(w.Id), "files", "result.json")));
        Assert.Equal("corrupt", File.ReadAllText(pending)); Assert.True(Directory.Exists(w.Path));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PayloadFailureAndCancellationDrainWorkersBeforeReturning(bool cancel)
    {
        using var started = new ManualResetEventSlim(); using var finish = new ManualResetEventSlim();
        var ct = TestContext.Current.CancellationToken;
        using var rejected = new ManualResetEventSlim(); using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var entries = Enumerable.Range(0, 64).Select(i => new FileSourceEntry(i.ToString(), "file", 1, "hash", 0, 0)).ToArray();
        int active = 0;
        var work = Task.Run(() => FileWorkspaceProvider.ForEachPayload(entries, cancellation.Token, e =>
        {
            Interlocked.Increment(ref active);
            try
            {
                if (e.Path == "0") { started.Set(); Assert.True(finish.Wait(TimeSpan.FromSeconds(20))); }
                else
                {
                    Assert.True(started.Wait(TimeSpan.FromSeconds(20))); rejected.Set();
                    if (cancel) cancellation.Cancel();
                    else throw new WorkspaceException("PayloadFailure", "Injected read failure");
                }
            }
            finally { Interlocked.Decrement(ref active); }
        }), ct);
        try
        {
            Assert.True(rejected.Wait(TimeSpan.FromSeconds(20), ct)); Assert.False(work.IsCompleted);
        }
        finally { finish.Set(); }
        if (cancel) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => work);
        else Assert.Equal("PayloadFailure", (await Assert.ThrowsAsync<WorkspaceException>(() => work)).Code);
        Assert.Equal(0, active);
    }
}
