using Splinter.Core;
using Splinter.Runtime;
using Splinter.SourceControl.Files;
using Splinter.SourceControl.Git;

namespace Splinter.Tests;

public sealed partial class GitObjectSnapshotTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ScopedReadsKeepRawProofsAndPermitOriginalAndPeerRefWrites(bool packedObjects)
    {
        using var f = await Fixture.Create(4096); var ct = TestContext.Current.CancellationToken;
        if (packedObjects) await TestDirectory.Git(f.Source, "gc", "--prune=now");
        var snapshot = await f.Capture(); var access = await f.Provider.ResolveReadAccessAsync(snapshot, ct);
        byte[] index = File.ReadAllBytes(Path.Combine(f.Source, ".git", "index"));
        using var lease = f.Provider.AcquireReadLease();
        using (var scoped = f.Provider.OpenReadScope(snapshot, access, lease))
        {
            byte[] data = new byte[4096]; Assert.Equal(data.Length, scoped.Read("file.bin", 0, data)); Assert.Equal(f.Data, data);
            File.WriteAllText(Path.Combine(f.Source, "Assets", "nested.txt"), "original remains editable");
            await TestDirectory.Git(f.Source, "update-ref", "refs/splinter/git-bases/peer", f.Revision);
            scoped.VerifyReadBoundary();
            Assert.Equal(index, File.ReadAllBytes(Path.Combine(f.Source, ".git", "index")));
            Assert.Equal(f.Revision, await TestDirectory.Git(f.Source, "rev-parse", "HEAD"));
            Assert.Equal("SnapshotReadersActive", (await Assert.ThrowsAsync<WorkspaceException>(() => f.Provider.ReleaseAsync(snapshot, ct))).Code);
        }
        lease.Dispose();
        Assert.Equal("released", (await f.Provider.ReleaseAsync(snapshot, ct)).Disposition);
        Assert.Equal(f.Revision, await TestDirectory.Git(f.Source, "rev-parse", "refs/splinter/git-bases/peer"));
    }

    [Fact]
    public async Task ScopedLooseRefAndReceiptAreFencedWithoutLeakingFencesAfterDispose()
    {
        using var f = await Fixture.Create(1024); var ct = TestContext.Current.CancellationToken;
        var snapshot = await f.Capture(); var access = await f.Provider.ResolveReadAccessAsync(snapshot, ct);
        using var lease = f.Provider.AcquireReadLease();
        string reference = Path.Combine(f.Source, ".git", "refs", "splinter", "git-bases", "task");
        string receipt = Path.Combine(f.Receipt, "git-basis.json");
        using (var scope = f.Provider.OpenReadScope(snapshot, access, lease))
        {
            if (OperatingSystem.IsWindows())
            {
                Assert.Throws<IOException>(() => File.WriteAllText(reference, new string('0', 40)));
                Assert.Throws<IOException>(() => File.Delete(reference));
                Assert.Throws<IOException>(() => File.WriteAllText(receipt, "{}"));
            }
            Assert.Equal(1024, scope.Read("file.bin", 0, new byte[1024]));
            scope.VerifyReadBoundary();
        }
        using var refWriter = new FileStream(reference, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        using var receiptWriter = new FileStream(receipt, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        Assert.True(refWriter.CanWrite); Assert.True(receiptWriter.CanWrite);
    }

    [Fact]
    public async Task PackedOnlyBasisRefRetainsFullChangedRefDetection()
    {
        using var f = await Fixture.Create(1024); var ct = TestContext.Current.CancellationToken;
        var snapshot = await f.Capture(); var access = await f.Provider.ResolveReadAccessAsync(snapshot, ct);
        await TestDirectory.Git(f.Source, "pack-refs", "--all", "--prune");
        using var lease = f.Provider.AcquireReadLease();
        using var scope = f.Provider.OpenReadScope(snapshot, access, lease);
        Assert.NotNull(scope.Stat("file.bin"));
        await TestDirectory.Git(f.Source, "update-ref", "-d", "refs/splinter/git-bases/task");
        Assert.Equal("GitBasisRefChanged", Assert.Throws<WorkspaceException>(() => scope.Stat("file.bin")).Code);
    }

    [Fact]
    public async Task LostReadLeaseAndWrongLeaseCannotAuthorizeScopedReads()
    {
        using var f = await Fixture.Create(1024); var ct = TestContext.Current.CancellationToken;
        var snapshot = await f.Capture(); var access = await f.Provider.ResolveReadAccessAsync(snapshot, ct);
        using var foreign = new FileStream(Path.Combine(f.Root, "wrong-lease"), FileMode.CreateNew, FileAccess.ReadWrite);
        Assert.Equal("GitBasisReadScopeInvalid", Assert.Throws<WorkspaceException>(() => f.Provider.OpenReadScope(snapshot, access, foreign)).Code);
        using var lease = f.Provider.AcquireReadLease(); using var scope = f.Provider.OpenReadScope(snapshot, access, lease);
        lease.Dispose();
        Assert.Equal("GitBasisReadScopeInvalid", Assert.Throws<WorkspaceException>(() => scope.Read("file.bin", 0, new byte[1])).Code);
    }

    [Fact]
    public async Task ObjectLossAtPublicationBoundaryPoisonsTheScopedProof()
    {
        using var f = await Fixture.Create(1024); var ct = TestContext.Current.CancellationToken;
        var snapshot = await f.Capture(); var access = await f.Provider.ResolveReadAccessAsync(snapshot, ct);
        using var lease = f.Provider.AcquireReadLease(); using var scope = f.Provider.OpenReadScope(snapshot, access, lease);
        Assert.Equal(1024, scope.Read("file.bin", 0, new byte[1024]));
        string commit = Path.Combine(f.Source, ".git", "objects", f.Revision[..2], f.Revision[2..]);
        string saved = commit + ".saved"; File.Move(commit, saved);
        try
        {
            Assert.Equal("GitBasisObjectInvalid", Assert.Throws<WorkspaceException>(() => scope.VerifyReadBoundary()).Code);
            File.Move(saved, commit);
            Assert.Equal("GitBasisObjectInvalid", Assert.Throws<WorkspaceException>(() => scope.Read("file.bin", 0, new byte[1])).Code);
        }
        finally { if (File.Exists(saved)) File.Move(saved, commit); }
    }

    [Fact]
    public async Task MissingObjectAfterScanCannotPublishAGenerationManifestOrReceipt()
    {
        using var f = await Fixture.Create(1024); var ct = TestContext.Current.CancellationToken;
        var snapshot = await f.Capture(); var access = await f.Provider.ResolveReadAccessAsync(snapshot, ct);
        using var lease = f.Provider.AcquireReadLease(); using var scope = f.Provider.OpenReadScope(snapshot, access, lease);
        var store = new WorkspaceStore(Path.Combine(f.Root, "home"));
        string id = new('a', 24), result = new('b', 24);
        var w = new WorkspaceRecord { Id = id, RequestKey = "scope", ProjectPath = f.Source, Path = store.WorkPath(id),
            SourceReference = "development", BaseReference = "development", BaseRevision = f.Revision,
            StorageMode = "git-snapshot", SourceKind = "git", ApplicationKind = "none", RequestDigest = new string('c', 64),
            Snapshot = new() { Reference = snapshot } };
        string commit = Path.Combine(f.Source, ".git", "objects", f.Revision[..2], f.Revision[2..]);
        var observer = new ScopeBoundaryObserver(() => File.Move(commit, commit + ".saved"));
        var provider = new FileWorkspaceProvider(store, preparationObserver: observer);
        using var captured = new ScopedCapture(scope);
        try
        {
            Assert.Equal("GitBasisObjectInvalid", (await Assert.ThrowsAsync<WorkspaceException>(() => provider.PreserveGenerationAsync(w,
                store.ControlPath(id), new(result, 1, "result", "result"), new(result, 1, new string('d', 64)), captured, ct))).Code);
            string directory = Path.Combine(store.ControlPath(id), "files", "generations", result);
            Assert.False(File.Exists(Path.Combine(directory, "result.json")));
            Assert.False(File.Exists(Path.Combine(directory, "generation.json")));
            Assert.Empty(w.Results);
        }
        finally { File.Move(commit + ".saved", commit); }
    }
    private sealed class ScopeBoundaryObserver(Action action) : IWorkspacePreparationObserver
    {
        public Task ReachedAsync(string boundary, WorkspaceRecord workspace, CancellationToken ct = default)
        { if (boundary == "source-payloads-preserved") action(); return Task.CompletedTask; }
    }
    private sealed class ScopedCapture(GitSnapshotReadScope scope) : ISourceCaptureTree
    {
        public ISnapshotTree Basis => scope;
        public SnapshotReference Snapshot => scope.Snapshot;
        public void EnsureAvailable() => scope.EnsureAvailable();
        public void VerifyReadBoundary() => scope.VerifyReadBoundary();
        public SnapshotEntry? Stat(string path) => scope.Stat(path);
        public IReadOnlyList<SnapshotEntry> List(string path) => scope.List(path);
        public int Read(string path, long offset, Span<byte> output) => scope.Read(path, offset, output);
        public void Dispose() { }
    }
}
