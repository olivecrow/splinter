using Splinter.Core;
using Splinter.Runtime;
using Splinter.SourceControl.Files;

namespace Splinter.Tests;

public sealed partial class SnapshotWorkspaceCoordinatorTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InitialEnsureRevalidatesProofButSubsequentEnsureInspectsAgain(bool changed)
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        var source = new ProvedSource();
        var router = new SourceWorkspaceRouter(source, new FileWorkspaceProvider(new(f.Home)));
        var coordinator = new WorkspaceCoordinator(new(f.Home), router, f.App, f.Backend,
            new ProofObserver(() => source.Changed = changed));
        Task<WorkspaceView> Ensure() => coordinator.EnsureAsync(f.Project, "task", "base", "ntfs-snapshot", false, ct);
        if (changed)
        {
            Assert.Equal("SourceChanged", (await Assert.ThrowsAsync<WorkspaceException>(Ensure)).Code);
            Assert.Single(new WorkspaceStore(f.Home).List(f.Project));
            Assert.False(f.Backend.DeltaRemoved); Assert.False(f.Backend.SnapshotReleased);
        }
        else
        {
            var first = await Ensure();
            Assert.Equal("verified", first.SourceVerification);
            Assert.Equal(1, source.InspectCalls); Assert.Equal(1, source.Revalidations);
            var again = await Ensure();
            Assert.Equal(first.Workspace.Id, again.Workspace.Id);
            Assert.Equal(2, source.InspectCalls); Assert.Equal(1, source.Revalidations);
        }
    }

    [Fact]
    public async Task SelectedAdapterWithoutProofRetainsTheOrdinaryResponseInspection()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        var router = new SourceWorkspaceRouter(f.Source, new FileWorkspaceProvider(new(f.Home)));
        var coordinator = new WorkspaceCoordinator(new(f.Home), router, f.App, f.Backend);
        var result = await coordinator.EnsureAsync(f.Project, "task", "base", "ntfs-snapshot", false, ct);
        Assert.Equal("verified", result.SourceVerification); Assert.Equal(2, f.Source.InspectCalls);
    }

    [Fact]
    public async Task EnsureStartingAnEditorKeepsFreshSourceInspectionAfterLaunch()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        var source = new ProvedSource();
        var coordinator = new WorkspaceCoordinator(new(f.Home), source, f.App, f.Backend,
            new ProofObserver(() => source.Dirty = true));
        var result = await coordinator.EnsureAsync(f.Project, "task", "base", "ntfs-snapshot", true, ct);
        Assert.Equal(1, f.App.Starts); Assert.Equal(2, source.InspectCalls);
        Assert.Equal(0, source.Revalidations); Assert.True(result.Source!.HasChanges);
    }

    private sealed class ProvedSource : Source, ISourceInspectionProofProvider
    {
        public bool Changed;
        public int Revalidations;
        public async Task<ProvedSourceInspection> InspectWithProofAsync(WorkspaceRecord w, CancellationToken ct = default)
        {
            var source = await InspectAsync(w, ct);
            return new(source, new Proof(this, source));
        }
        private sealed class Proof(ProvedSource provider, SourceState source) : ISourceInspectionProof
        {
            public Task<SourceState> RevalidateAsync(WorkspaceRecord w, CancellationToken ct = default)
            {
                provider.Revalidations++;
                if (provider.Changed) throw new WorkspaceException("SourceChanged", "Fixture generation changed before response");
                return Task.FromResult(source);
            }
        }
    }
    private sealed class ProofObserver(Action action) : IWorkspacePreparationObserver
    {
        public Task ReachedAsync(string boundary, WorkspaceRecord w, CancellationToken ct)
        { if (boundary == "ready.after-record") action(); return Task.CompletedTask; }
    }
}
