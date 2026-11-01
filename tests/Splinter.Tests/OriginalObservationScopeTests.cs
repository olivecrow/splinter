using Splinter.Core;
using Splinter.Platform.Windows;

namespace Splinter.Tests;

public sealed class OriginalObservationScopeTests
{
    [Fact(Timeout = 10000)]
    public void ScopeReusesRootProofButReleasesTargetsAndRereadsTheirCurrentBytes()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Native original observation.");
        using var files = new TestDirectory(); string project = Path.Combine(files.Path, "project"); Directory.CreateDirectory(project);
        for (int i = 0; i < 16; i++) File.WriteAllText(Path.Combine(project, "file-" + i), "before");
        int acquisitions = 0; var publisher = new WindowsOriginalFilePublisher(managedRoot: _ => { acquisitions++; return null; });
        var ct = TestContext.Current.CancellationToken;
        var expected = Enumerable.Range(0, 16).Select(i => publisher.Observe(project, "file-" + i, ct)).ToArray();
        string root = publisher.RootIdentity(project); Assert.Equal(17, acquisitions); acquisitions = 0;
        using (var scope = ((IOriginalFileObservationProvider)publisher).OpenObservation(project, ct))
        {
            Assert.Equal(root, scope.ProjectIdentity);
            for (int i = 0; i < 16; i++) Assert.Equal(expected[i], scope.Observe("file-" + i, ct));
            Assert.Equal(1, acquisitions);
            File.WriteAllText(Path.Combine(project, "file-0"), "after");
            Assert.NotEqual(expected[0]!.Version.Hash, scope.Observe("file-0", ct)!.Version.Hash);
            Assert.Null(scope.Observe("absent", ct));
            Assert.ThrowsAny<IOException>(() => Directory.Move(project, Path.Combine(files.Path, "moved")));
            scope.VerifyBoundary(ct); Assert.Equal(2, acquisitions);
        }
        Directory.Move(project, Path.Combine(files.Path, "moved"));
    }

    [Fact(Timeout = 10000)]
    public void ScopeRejectsNewAdministrationAndCancellationWithoutRetainingTargetHandles()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Native original observation.");
        using var files = new TestDirectory(); string project = Path.Combine(files.Path, "project"); Directory.CreateDirectory(project);
        File.WriteAllText(Path.Combine(project, "file"), "original");
        var publisher = new WindowsOriginalFilePublisher();
        using var scope = publisher.OpenObservation(project, TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => scope.Observe("file", cancellation.Token));
        File.WriteAllText(Path.Combine(project, "file"), "later");
        Directory.CreateDirectory(Path.Combine(project, ".git"));
        Assert.Equal("VcsMetadataUnsupported", Assert.Throws<WorkspaceException>(() => scope.VerifyBoundary(TestContext.Current.CancellationToken)).Code);
        Assert.True(Directory.Exists(Path.Combine(project, ".git")));
    }
}

public sealed partial class FileWorkspaceTests
{
    [Theory(Timeout = 10000)]
    [InlineData(false)] [InlineData(true)]
    public async Task PlanningUsesOptionalPortableObservationAndDisposesItOnBoundaryFailure(bool fail)
    {
        var publisher = new ScopedObservationFake(fail); using var f = new Fixture(publisher); var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure("scope", ct)).Workspace;
        File.WriteAllText(Path.Combine(w.Path, "data.txt"), "changed"); await f.New().CheckpointAsync(w.Id, "result", ct);
        byte[] original = File.ReadAllBytes(Path.Combine(f.Project, "data.txt"));
        if (fail) Assert.Equal("ObservationBoundaryChanged", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().PlanOriginalAsync(w.Id, ct))).Code);
        else Assert.NotEmpty((await f.New().PlanOriginalAsync(w.Id, ct)).OriginalApplyPlan!.EnumerateChanges());
        Assert.Equal(1, publisher.Opened); Assert.True(publisher.Observed > 0); Assert.Equal(1, publisher.Verified); Assert.Equal(1, publisher.Disposed);
        Assert.Equal(original, File.ReadAllBytes(Path.Combine(f.Project, "data.txt")));
    }
    private sealed class ScopedObservationFake(bool fail) : IOriginalFilePublisher, IOriginalFileObservationProvider
    {
        private readonly FakePublisher inner = new();
        public int Opened, Observed, Verified, Disposed;
        public string RootIdentity(string project) => throw new InvalidOperationException("Use the scoped root.");
        public OriginalFileObservation? Observe(string project, string relativePath, CancellationToken ct = default) => throw new InvalidOperationException("Use scoped observation.");
        public void Publish(OriginalApplyState state, Func<OriginalFileVersion, Stream> payload, Action persist, CancellationToken ct = default) => inner.Publish(state, payload, persist, ct);
        public IOriginalFileObservationScope OpenObservation(string project, CancellationToken ct = default) { Opened++; return new Scope(this, project); }
        private sealed class Scope(ScopedObservationFake owner, string project) : IOriginalFileObservationScope
        {
            public string ProjectIdentity => owner.inner.RootIdentity(project);
            public OriginalFileObservation? Observe(string path, CancellationToken ct = default) { owner.Observed++; return owner.inner.Observe(project, path, ct); }
            public void VerifyBoundary(CancellationToken ct = default) { owner.Verified++; if (owner.Fails) throw new WorkspaceException("ObservationBoundaryChanged", "Injected boundary failure."); }
            public void Dispose() { owner.Disposed++; }
        }
        private bool Fails => fail;
    }
}
