using Splinter.Core;

namespace Splinter.Tests;

public sealed class SnapshotAvailabilityScopeContractTests
{
    [Fact]
    public void UnsupportedAndUnqualifiedTreesRetainTheFreshProviderFallback()
    {
        var tree = new SnapshotOverlayTests.FakeSnapshotTree(); var access = new SnapshotReadAccess(tree.Snapshot.Id, tree.Snapshot.RootPath, "proof");
        Assert.Null(SnapshotAvailabilityScopes.Observe(tree, tree.Snapshot, access));
        using var scope = new FakeScope(tree) { Result = null }; Assert.Null(SnapshotAvailabilityScopes.Observe(scope, tree.Snapshot, access));
    }
    [Fact]
    public void TheExactHeldScopeReturnsBoundObservationAndSeparateNativeBindingCosts()
    {
        var tree = new SnapshotOverlayTests.FakeSnapshotTree(); var access = new SnapshotReadAccess(tree.Snapshot.Id, tree.Snapshot.RootPath, "proof");
        using var scope = new FakeScope(tree) { Result = new(tree.Snapshot.Id, true, null, 0, null) { ReadAccess = access } };
        Assert.Equal(scope.Result, SnapshotAvailabilityScopes.Observe(scope, tree.Snapshot, access));
        Assert.Equal(3, scope.MetadataFenceCount); Assert.Equal(1024, scope.MetadataFenceBytes);
    }
    [Theory]
    [InlineData("snapshot")]
    [InlineData("access")]
    [InlineData("observation")]
    [InlineData("proof")]
    public void ForeignSnapshotAccessObservationAndVerificationIdentityAreRefused(string mismatch)
    {
        var tree = new SnapshotOverlayTests.FakeSnapshotTree(); var access = new SnapshotReadAccess(tree.Snapshot.Id, tree.Snapshot.RootPath, "proof");
        using var scope = new FakeScope(tree) { Result = new(tree.Snapshot.Id, true, null, 0, null) { ReadAccess = access } };
        var expected = mismatch == "snapshot" ? tree.Snapshot with { SourcePath = "another-source" } : tree.Snapshot;
        if (mismatch == "access") access = access with { SnapshotId = "foreign" };
        if (mismatch == "observation") scope.Result = scope.Result! with { Id = "foreign" };
        if (mismatch == "proof") scope.Result = scope.Result! with { ReadAccess = access with { VerificationId = "foreign" } };
        Assert.Equal("SnapshotIdentityMismatch", Assert.Throws<WorkspaceException>(() => SnapshotAvailabilityScopes.Observe(scope, expected, access)).Code);
    }
    [Fact]
    public void MissingObservationRemainsMissingAndDisposedScopesCannotBeReused()
    {
        var tree = new SnapshotOverlayTests.FakeSnapshotTree(); var access = new SnapshotReadAccess(tree.Snapshot.Id, tree.Snapshot.RootPath, "proof");
        var scope = new FakeScope(tree) { Result = new(tree.Snapshot.Id, false, "SnapshotMissing", 0, null) };
        Assert.False(SnapshotAvailabilityScopes.Observe(scope, tree.Snapshot, access)!.Available);
        scope.Dispose(); Assert.Throws<ObjectDisposedException>(() => SnapshotAvailabilityScopes.Observe(scope, tree.Snapshot, access));
    }
    private sealed class FakeScope(SnapshotOverlayTests.FakeSnapshotTree basis) : ISnapshotTree, ISnapshotAvailabilityScope, IDisposable
    {
        private bool disposed;
        public SnapshotObservation? Result { get; set; }
        public SnapshotReference Snapshot => basis.Snapshot;
        public long MetadataFenceCount => 3;
        public long MetadataFenceBytes => 1024;
        public SnapshotObservation? ObserveAvailability(SnapshotReadAccess expectedAccess) { ObjectDisposedException.ThrowIf(disposed, this); return Result; }
        public void EnsureAvailable() => basis.EnsureAvailable();
        public SnapshotEntry? Stat(string path) => basis.Stat(path);
        public IReadOnlyList<SnapshotEntry> List(string path) => basis.List(path);
        public int Read(string path, long offset, Span<byte> buffer) => basis.Read(path, offset, buffer);
        public void Dispose() => disposed = true;
    }
}
