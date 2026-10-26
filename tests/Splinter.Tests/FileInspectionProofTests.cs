using Splinter.Core;
using Splinter.SourceControl.Files;

namespace Splinter.Tests;

public sealed partial class FileWorkspaceTests
{
    [Theory]
    [InlineData("none")]
    [InlineData("plastic")]
    public async Task FileProofReusesPayloadOnlyOnceAndFallbackStillReadsIt(string source)
    {
        using var f = new Fixture(sourceKind: source); var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure("file-proof", ct)).Workspace;
        var tracker = new FileChanges();
        var provider = new FileWorkspaceProvider(f.Store, sourceChanges: tracker);
        var inspection = await provider.InspectWithProofAsync(w, ct);
        Assert.NotNull(inspection.Proof); Assert.False(inspection.Source.HasChanges);
        using (var locked = new FileStream(Path.Combine(w.Path, "data.txt"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.Equal(inspection.Source, await inspection.Proof.RevalidateAsync(w, ct));
            Assert.Equal("SourceChanged", (await Assert.ThrowsAsync<WorkspaceException>(() => inspection.Proof.RevalidateAsync(w, ct))).Code);
            await Assert.ThrowsAnyAsync<IOException>(() => new FileWorkspaceProvider(f.Store).InspectWithProofAsync(w, ct));
        }
        Assert.Equal(4, tracker.Calls);
        Assert.Null((await new FileWorkspaceProvider(f.Store).InspectWithProofAsync(w, ct)).Proof);
    }

    [Theory]
    [InlineData("write")]
    [InlineData("restart")]
    [InlineData("membership")]
    [InlineData("metadata")]
    [InlineData("policy")]
    [InlineData("identity")]
    public async Task FileProofRejectsChangedSourceAndIdentity(string change)
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure("file-proof-change", ct)).Workspace;
        var tracker = new FileChanges(); var provider = new FileWorkspaceProvider(f.Store, sourceChanges: tracker);
        var proof = (await provider.InspectWithProofAsync(w, ct)).Proof!;
        string path = Path.Combine(w.Path, "data.txt"); var time = File.GetLastWriteTimeUtc(path);
        switch (change)
        {
            case "write": File.WriteAllText(path, "other"); File.SetLastWriteTimeUtc(path, time); tracker.Sequence = 1; break;
            case "restart": tracker.Epoch = "restarted"; break;
            case "membership": File.WriteAllText(Path.Combine(w.Path, "added.txt"), "new"); break;
            case "metadata": File.SetLastWriteTimeUtc(path, time.AddSeconds(1)); break;
            case "policy": w.ManagedPaths = ["data.txt"]; break;
            case "identity": w.Path += "-other"; break;
        }
        Assert.Equal("SourceChanged", (await Assert.ThrowsAsync<WorkspaceException>(() => proof.RevalidateAsync(w, ct))).Code);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public async Task FileProofRequiresStableBarrierOnBothSidesOfEachScan(int changedCall)
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure("file-proof-race", ct)).Workspace;
        var tracker = new FileChanges { ChangeAt = changedCall };
        var provider = new FileWorkspaceProvider(f.Store, sourceChanges: tracker);
        async Task Act()
        {
            var proof = (await provider.InspectWithProofAsync(w, ct)).Proof!;
            await proof.RevalidateAsync(w, ct);
        }
        Assert.Equal("SourceChanged", (await Assert.ThrowsAsync<WorkspaceException>(Act)).Code);
    }

    [Fact]
    public async Task FileProofRejectsIncompleteHistoryAndPlasticRegistrationChanges()
    {
        using var f = new Fixture(sourceKind: "plastic"); var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure("plastic-proof", ct)).Workspace;
        var tracker = new FileChanges { Invalid = true };
        var provider = new PlasticWorkspaceProvider(f.Store, f.Plastic, f.Revisions, sourceChanges: tracker);
        Assert.Equal("SourceObservationInvalid", (await Assert.ThrowsAsync<WorkspaceException>(() => provider.InspectWithProofAsync(w, ct))).Code);
        tracker.Invalid = false;
        var proof = (await provider.InspectWithProofAsync(w, ct)).Proof!;
        f.Plastic.AdditionalRoot = w.Path;
        Assert.Equal("PlasticCompatibilityInvalid", (await Assert.ThrowsAsync<WorkspaceException>(() => proof.RevalidateAsync(w, ct))).Code);
    }

    private sealed class FileChanges : ISourceChangeProvider
    {
        public int Calls, ChangeAt;
        public long Sequence;
        public string Epoch = "first";
        public bool Invalid;
        public Task<SourceChangeView> ObserveChangesAsync(WorkspaceRecord w, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested(); if (++Calls == ChangeAt) Sequence++;
            return Task.FromResult(new SourceChangeView(w.Id, w.Snapshot!.Reference!.Id, Epoch,
                Invalid ? 1 : Sequence, Sequence == 0 ? [] : [new("data.txt", Sequence)]));
        }
        public string CachePath(WorkspaceRecord w) => throw new InvalidOperationException("No persisted proof cache.");
    }
}
