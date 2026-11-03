using Splinter.Core;

namespace Splinter.Tests;

public sealed class SourceChangeLeaseConsumerTests : SourceFingerprintFixtureBase
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuccessfulAndFailedSourceInspectionsReleaseTheirViewsBeforeReturning(bool paged)
    {
        using var f = await Fixture.Create(); var ct = TestContext.Current.CancellationToken; var leases = new ChangeLeaseProbe();
        f.Tracker.Paged = paged; f.Tracker.ObservedView = leases.Attach;
        var provider = f.Provider(); await provider.InspectAsync(f.Workspace, ct); Assert.Equal(0, leases.Active);
        f.Tracker.OnObserve = count => { if (count == 4) f.Tracker.Epoch = "changed-during-inspection"; };
        Assert.Equal("SourceChanged", (await Assert.ThrowsAsync<WorkspaceException>(() => provider.InspectAsync(f.Workspace, ct))).Code);
        Assert.Equal(0, leases.Active); GC.KeepAlive(provider);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task AnOperationProofKeepsItsReaderUntilItIsConsumedOrExplicitlyDiscarded(bool paged, bool consume)
    {
        using var f = await Fixture.Create(); var ct = TestContext.Current.CancellationToken; var leases = new ChangeLeaseProbe();
        f.Tracker.Paged = paged; f.Tracker.ObservedView = leases.Attach;
        using var inspected = await f.Provider().InspectWithProofAsync(f.Workspace, ct);
        Assert.NotNull(inspected.Proof); Assert.Equal(1, leases.Active);
        if (consume) Assert.Equal(inspected.Source, await inspected.Proof.RevalidateAsync(f.Workspace, ct));
        else
        {
            inspected.Dispose();
            Assert.Equal("SourceChanged", (await Assert.ThrowsAsync<WorkspaceException>(() => inspected.Proof.RevalidateAsync(f.Workspace, ct))).Code);
        }
        Assert.Equal(0, leases.Active); inspected.Dispose(); Assert.Equal(0, leases.Active); GC.KeepAlive(inspected);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnUnusedInitialAdmissionStoresOnlyAStampAndLeavesNoReaderBehind(bool paged)
    {
        using var f = await Fixture.Create(); var ct = TestContext.Current.CancellationToken; var leases = new ChangeLeaseProbe();
        f.Tracker.Paged = paged; f.Tracker.ObservedView = leases.Attach; SeedSummaries(f); var provider = f.Provider();
        await provider.BeginInitialContentInspectionAsync(f.Workspace, ct); Assert.Equal(0, leases.Active);
        using (new FileStream(f.Path("large.bin"), FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.Equal(0, (await provider.InspectAsync(f.Workspace, ct)).Inspection!.PayloadBytesHashed);
        Assert.Equal(0, leases.Active); GC.KeepAlive(provider);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task InitialSummaryOwnedGenerationClonesCloseOnSuccessAndRejection(bool paged, bool invalid)
    {
        using var f = await Fixture.Create(); var ct = TestContext.Current.CancellationToken; var leases = new ChangeLeaseProbe();
        f.Tracker.Paged = paged; f.Tracker.ObservedView = leases.Attach; SeedSummaries(f); var provider = f.Provider();
        await provider.BeginInitialContentInspectionAsync(f.Workspace, ct); Assert.Equal(0, leases.Active);
        f.Tracker.TransformInitial = summary => summary with
        { Generation = summary.Generation with { Epoch = invalid ? "foreign-summary-generation" : summary.Generation.Epoch } };
        if (invalid)
            Assert.Equal("SourceObservationInvalid", (await Assert.ThrowsAsync<WorkspaceException>(() => provider.InspectAsync(f.Workspace, ct))).Code);
        else
            using (new FileStream(f.Path("large.bin"), FileMode.Open, FileAccess.Read, FileShare.None))
                Assert.Equal(0, (await provider.InspectAsync(f.Workspace, ct)).Inspection!.PayloadBytesHashed);
        Assert.Equal(0, leases.Active); GC.KeepAlive(provider);
    }



    private sealed class ChangeLeaseProbe
    {
        private int active;
        public int Active => Volatile.Read(ref active);
        public SourceChangeView Attach(SourceChangeView view)
        {
            using (view)
            using (var lease = new Token(this, view.EnumerateChanges().ToArray()))
                return view with { ReaderLease = lease };
        }
        private sealed class Token : ISourceChangeReaderLease
        {
            private readonly ChangeLeaseProbe owner;
            private readonly SourcePathChange[] rows;
            private int disposed;
            public Token(ChangeLeaseProbe owner, SourcePathChange[] rows)
            { this.owner = owner; this.rows = rows; Interlocked.Increment(ref owner.active); }
            public ISourceChangeReaderLease Retain()
            { ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this); return new Token(owner, rows); }
            public IEnumerable<SourcePathChange> Read(SourceChangeView view)
            { ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this); return rows; }
            public void Dispose()
            { if (Interlocked.Exchange(ref disposed, 1) == 0) Interlocked.Decrement(ref owner.active); }
        }
    }
}
