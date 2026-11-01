using System.Security.Cryptography;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Runtime;
using Splinter.SourceControl.Files;

namespace Splinter.Tests;

public sealed class PlasticManifestObservationScopeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepeatedHeldObservationReusesAdmissionOnlyWhileTheExactNativeMetadataFencesLive(bool nativeOwners)
    {
        using var fixture = new Fixture(); if (nativeOwners) fixture.Client.NativePins = fixture.Pins;
        var ct = TestContext.Current.CancellationToken; await fixture.Prepare(ct);
        using (var tree = fixture.Provider.OpenTree(fixture.Snapshot!, fixture.Access!, fixture.Pins))
        {
            long admission = fixture.Provider.ManifestValidationCount;
            for (int index = 0; index < 100; index++) Assert.True(SnapshotAvailabilityScopes.Observe(tree, fixture.Snapshot!, fixture.Access!)!.Available);
            Assert.Equal(admission, fixture.Provider.ManifestValidationCount);
            Assert.True(tree.MetadataFenceCount >= 8); Assert.True(tree.MetadataFenceBytes > 0);
            Assert.Equal(nativeOwners, File.Exists(fixture.Page + ".owner")); // No ownership adoption.
            tree.VerifyReadBoundary(); Assert.Equal(admission + 1, fixture.Provider.ManifestValidationCount);
            Assert.True((await fixture.Provider.ObserveAsync(fixture.Snapshot!, ct)).Available);
            Assert.Equal(admission + 2, fixture.Provider.ManifestValidationCount);
            Assert.Equal("SnapshotReadersActive", (await Assert.ThrowsAsync<WorkspaceException>(() => fixture.Provider.ReleaseAsync(fixture.Snapshot!, ct))).Code);
        }
        Assert.Equal("released", (await fixture.Provider.ReleaseAsync(fixture.Snapshot!, ct)).Disposition);
    }
    [Fact]
    public async Task EveryCatalogFileAndTheExactManifestRejectWritesAndSameByteReplacementUntilScopeDisposal()
    {
        using var fixture = new Fixture(); await fixture.Prepare(TestContext.Current.CancellationToken);
        using (var tree = fixture.Provider.OpenTree(fixture.Snapshot!, fixture.Access!, fixture.Pins))
        {
            foreach (string path in Directory.EnumerateFiles(fixture.Catalog).Append(Path.Combine(fixture.Basis, "plastic-basis.json")))
            {
                Assert.ThrowsAny<IOException>(() => { using var write = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete); });
                Assert.ThrowsAny<IOException>(() => File.Move(path, path + ".foreign-original"));
            }
            Assert.True(tree.ObserveAvailability(fixture.Access!)!.Available);
        }
        using var output = new FileStream(fixture.Page, FileMode.Open, FileAccess.Write, FileShare.Read); // Fences really close.
    }
    [Fact]
    public async Task NewPhysicalNamespaceMembersAndForeignReadIdentityCannotReuseTheScopedProof()
    {
        using var fixture = new Fixture(); await fixture.Prepare(TestContext.Current.CancellationToken);
        using var tree = fixture.Provider.OpenTree(fixture.Snapshot!, fixture.Access!, fixture.Pins);
        Assert.Equal("SnapshotIdentityMismatch", Assert.Throws<WorkspaceException>(() => tree.ObserveAvailability(fixture.Access! with { VerificationId = "foreign" })).Code);
        string extra = Path.Combine(fixture.Catalog, "foreign.bin"); File.WriteAllBytes(extra, [3]);
        Assert.NotEqual(true, tree.ObserveAvailability(fixture.Access!)!.Available);
        Assert.Throws<WorkspaceException>(() => tree.VerifyReadBoundary()); File.Delete(extra);
        Assert.True(tree.ObserveAvailability(fixture.Access!)!.Available);
    }
    [Theory]
    [InlineData("unknown")]
    [InlineData("partial")]
    public async Task UnknownOrPartiallyOwnedCatalogsRefuseScopeAdmissionWithoutWritingOrAdoptingReceipts(string kind)
    {
        using var fixture = new Fixture(); await fixture.Prepare(TestContext.Current.CancellationToken);
        string injected;
        if (kind == "unknown") { injected = Path.Combine(fixture.Catalog, "unpublished.pending"); File.WriteAllBytes(injected, [3]); }
        else
        {
            injected = Path.Combine(fixture.Catalog, "pages-0000000000000007.bin.owner");
            File.WriteAllText(injected, "{\"version\":1,\"storeId\":\"" + PagedMetadataStore.ReadIdentity(fixture.Catalog) + "\",\"container\":7,\"identity\":\"foreign\"}");
        }
        string native = fixture.Pins.Identify(injected);
        Assert.True(Record.Exception(() => fixture.Provider.OpenTree(fixture.Snapshot!, fixture.Access!, fixture.Pins)) is IOException or WorkspaceException);
        Assert.Equal(native, fixture.Pins.Identify(injected)); Assert.False(File.Exists(fixture.Page + ".owner"));
        using var writer = new FileStream(fixture.Page, FileMode.Open, FileAccess.Write, FileShare.Read); // Failed scope drained every fence.
    }
    [Fact]
    public async Task OrdinaryObservationAfterScopeClosesStillPerformsFreshCorruptionProof()
    {
        using var fixture = new Fixture(); var ct = TestContext.Current.CancellationToken; await fixture.Prepare(ct);
        using (var tree = fixture.Provider.OpenTree(fixture.Snapshot!, fixture.Access!, fixture.Pins)) Assert.True(tree.ObserveAvailability(fixture.Access!)!.Available);
        using (var output = new FileStream(fixture.Page, FileMode.Open, FileAccess.Write, FileShare.Read))
        { output.Position = 90; output.WriteByte(173); output.Flush(true); }
        Assert.NotEqual(true, (await fixture.Provider.ObserveAsync(fixture.Snapshot!, ct)).Available);
    }
    [Fact]
    public async Task WindowsObservationUsesOnlyTheAlreadyOpenedQualifiedWrapperAndRetainsDefaultFreshFallback()
    {
        using var fixture = new Fixture(); var ct = TestContext.Current.CancellationToken; await fixture.Prepare(ct);
        var request = new OverlayServeRequest(new(3, "scope-test", fixture.Snapshot!), Path.Combine(fixture.Root.Path, "delta"), Path.Combine(fixture.Root.Path, "mount"),
            Path.Combine(fixture.Root.Path, "control"), new string('b', 64), fixture.Access!, fixture.Basis, "owner");
        using (var tree = fixture.Provider.OpenTree(fixture.Snapshot!, fixture.Access!, fixture.Pins))
        using (var wrapper = new WorkspaceSnapshotBasis.DisposableSnapshotTree(tree))
        {
            long count = fixture.Provider.ManifestValidationCount;
            Assert.True((await WinFspOverlayClient.ObserveSnapshotAsync(request, ct, openedSnapshot: wrapper)).Available);
            Assert.Equal(count, fixture.Provider.ManifestValidationCount); Assert.Equal(tree.MetadataFenceCount, wrapper.MetadataFenceCount);
        }
        using (var output = new FileStream(fixture.Page, FileMode.Open, FileAccess.Write, FileShare.Read)) { output.Position = 90; output.WriteByte(173); output.Flush(true); }
        Assert.NotEqual(true, (await WinFspOverlayClient.ObserveSnapshotAsync(request, ct)).Available);
    }
    [Fact]
    public async Task ANewCatalogDirectoryJunctionCannotEnterTheHeldObservationNamespace()
    {
        using var fixture = new Fixture(); using var external = new TestDirectory(); var ct = TestContext.Current.CancellationToken; await fixture.Prepare(ct);
        using var tree = fixture.Provider.OpenTree(fixture.Snapshot!, fixture.Access!, fixture.Pins);
        string link = Path.Combine(fixture.Catalog, "collections");
        var start = new System.Diagnostics.ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true, Arguments = $"/d /c mklink /J \"{link}\" \"{external.Path}\"" };
        using var process = System.Diagnostics.Process.Start(start)!; await process.WaitForExitAsync(ct); Assert.Equal(0, process.ExitCode);
        try { Assert.NotEqual(true, tree.ObserveAvailability(fixture.Access!)!.Available); Assert.Throws<WorkspaceException>(tree.VerifyReadBoundary); }
        finally { Directory.Delete(link); }
    }
    [Fact]
    public async Task FailurePartwayThroughNativeAdmissionClosesAllPartialFencesAndTheReaderLease()
    {
        using var fixture = new Fixture(); var ct = TestContext.Current.CancellationToken; await fixture.Prepare(ct);
        Assert.Throws<IOException>(() => fixture.Provider.OpenTree(fixture.Snapshot!, fixture.Access!, new FailingPins(fixture.Pins)));
        foreach (string file in Directory.EnumerateFiles(fixture.Catalog)) { using var output = new FileStream(file, FileMode.Open, FileAccess.Write, FileShare.Read); }
        Assert.Equal("released", (await fixture.Provider.ReleaseAsync(fixture.Snapshot!, ct)).Disposition);
    }
    [Fact]
    public async Task DisposingTheTreeDrainsAnOwnedRevisionReadBeforeReleasingMetadataFencesAndSession()
    {
        using var fixture = new Fixture(); var ct = TestContext.Current.CancellationToken; await fixture.Prepare(ct);
        var tree = fixture.Provider.OpenTree(fixture.Snapshot!, fixture.Access!, fixture.Pins); fixture.Client.Block = true;
        var read = Task.Run(() => tree.Read("data.bin", 0, new byte[32]), ct);
        Assert.True(fixture.Client.Entered.Wait(TimeSpan.FromSeconds(10), ct));
        var dispose = Task.Run(tree.Dispose, ct);
        Assert.ThrowsAny<IOException>(() => { using var output = new FileStream(fixture.Page, FileMode.Open, FileAccess.Write, FileShare.Read); });
        Assert.False(dispose.IsCompleted); fixture.Client.Continue.Set();
        Assert.Equal(32, await read); await dispose; Assert.Equal(1, fixture.Client.Closed);
        Assert.Equal(0, tree.MetadataFenceCount); Assert.Equal(0, tree.MetadataFenceBytes);
        Assert.Throws<ObjectDisposedException>(() => tree.ObserveAvailability(fixture.Access!));
        using var write = new FileStream(fixture.Page, FileMode.Open, FileAccess.Write, FileShare.Read);
    }
    private sealed class Fixture : IDisposable
    {
        public TestDirectory Root { get; } = new();
        public WindowsJournalFilePins Pins { get; } = new();
        public Client Client { get; } = new();
        public string Basis => Path.Combine(Root.Path, "basis");
        public string Catalog => Path.Combine(Basis, "catalog");
        public string Page => Path.Combine(Catalog, "pages-0000000000000000.bin");
        public PlasticRevisionSnapshotProvider Provider { get; }
        public SnapshotReference? Snapshot { get; private set; }
        public SnapshotReadAccess? Access { get; private set; }
        public Fixture()
        {
            if (!OperatingSystem.IsWindows()) Assert.Skip("Windows native immutable metadata fences.");
            Directory.CreateDirectory(Basis); Client.NativeCatalogDirectory = Catalog; Provider = new(Basis, "owner", "1@repo@server", Client);
        }
        public async Task Prepare(CancellationToken ct)
        {
            Snapshot = await Provider.CaptureAsync(new(Path.Combine(Root.Path, "source"), "owner", Basis), ct);
            Access = await Provider.ResolveReadAccessAsync(Snapshot, ct);
        }
        public void Dispose() { Client.Continue.Set(); Client.Entered.Dispose(); Client.Continue.Dispose(); Root.Dispose(); }
    }
    private sealed class Client : IPlasticRevisionClient
    {
        private static readonly byte[] Payload = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        public readonly ManualResetEventSlim Entered = new(), Continue = new();
        public WindowsJournalFilePins? NativePins;
        public string? NativeCatalogDirectory;
        public bool Block; public int Closed;
        public Task<PlasticRevisionManifest> ListAsync(string treeSpec, CancellationToken ct = default) => Task.FromResult(new PlasticRevisionManifest(treeSpec, "42", "1",
            "11111111-1111-1111-1111-111111111111", [new("data.bin", false, "2", Payload.Length, Convert.ToBase64String(MD5.HashData(Payload)), 0)], "rep:repo@server"));
        public Task VerifyProjectAsync(string project, PlasticRevisionManifest manifest, CancellationToken ct = default) => Task.CompletedTask;
        public async Task<PlasticRevisionManifest> ListAsync(string treeSpec, string directory, CancellationToken ct = default)
        {
            using var manifest = await ListAsync(treeSpec, ct);
            if (NativePins is null || NativeCatalogDirectory is null || !WorkspaceStore.SamePath(directory, NativeCatalogDirectory)) return manifest.WriteCatalog(directory, ct);
            var descriptor = PagedSnapshotCatalog.Build(directory, new("", true, 0, (uint)FileAttributes.Directory, 0, 0, 0),
                manifest.Entries.Select(PlasticRevisionManifest.ToRecord), ct: ct, filePins: NativePins);
            return manifest with { Entries = null!, Catalog = descriptor, CatalogDirectory = directory };
        }
        public Stream OpenRevision(string treeSpec, string revision)
        { if (Block) { Entered.Set(); Continue.Wait(TestContext.Current.CancellationToken); } return new MemoryStream(Payload, false); }
        public IPlasticRevisionReader CreateReadSession() => new Session(this);
        private sealed class Session(Client owner) : IPlasticRevisionReader, IDisposable
        {
            private bool closed;
            public Stream OpenRevision(string treeSpec, string revision) => owner.OpenRevision(treeSpec, revision);
            public void Dispose() { if (!closed) { closed = true; owner.Closed++; } }
        }
    }
    private sealed class FailingPins(WindowsJournalFilePins inner) : IJournalFilePins
    {
        private int opened;
        public string Identify(string path) => inner.Identify(path);
        public string IdentifyOpened(FileStream input) => ++opened == 3 ? throw new IOException("Native admission failure") : inner.IdentifyOpened(input);
        public void Verify(FileStream input, string expected) => inner.Verify(input, expected);
        public void VerifyPath(FileStream input, string path, string expected) => inner.VerifyPath(input, path, expected);
        public void Pin(string source, string destination, string expected) => inner.Pin(source, destination, expected);
        public bool DetachBeforeTruncate(string path, long length) => inner.DetachBeforeTruncate(path, length);
        public string IdentifyDirectory(string path) => inner.IdentifyDirectory(path);
        public IDisposable PinDirectory(string path, string expected) => inner.PinDirectory(path, expected);
    }
}
