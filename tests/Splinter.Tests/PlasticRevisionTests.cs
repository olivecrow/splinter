using System.Security.Cryptography;
using System.Text.Json;
using Splinter.Core;
using Splinter.Protocol;
using Splinter.Runtime;
using Splinter.SourceControl.Files;
using Splinter.Platform.Windows;
using Splinter.Workflows.Windows;

namespace Splinter.Tests;

public sealed class PlasticRevisionTests
{
    [Theory]
    [InlineData("br:/main@repo@server")]
    [InlineData("1@repo;output@server")]
    [InlineData("-1@repo@server")]
    [InlineData("1@repo #other")]
    public void MovingOrCommandShapedInputIsRejected(string spec) => Assert.Throws<WorkspaceException>(() => PlasticRevisionClient.RequireTreeSpec(spec));

    [Fact]
    public void RecordedBinaryXmlUsesTheSameFixedRevisionContract()
    {
        const string xml = """
        <LsResults><LsItems>
        <LsItem><CurrentPath>/</CurrentPath><RepId>42</RepId><RevId>1</RevId><Guid>11111111-1111-1111-1111-111111111111</Guid><Type>dir</Type><Repository>rep:repo@server</Repository></LsItem>
        <LsItem><CurrentPath>/image.png</CurrentPath><RepId>42</RepId><RevId>2</RevId><Type>bin</Type><Size>1</Size><Hash>1B2M2Y8AsgTpgAmY7PhCfg==</Hash><Date>2026-10-04T00:00:00Z</Date></LsItem>
        </LsItems></LsResults>
        """;
        var file = Assert.Single(PlasticRevisionClient.Parse("1@repo@server", xml).Entries);
        Assert.False(file.Directory); Assert.Equal("image.png", file.Path); Assert.Equal("2", file.Revision);
        Assert.Equal("PlasticBasisInvalid", Assert.Throws<WorkspaceException>(() => PlasticRevisionClient.Parse("1@repo@server", xml.Replace("<Type>bin</Type>", "<Type>symlink</Type>"))).Code);
    }
    [Fact]
    public void LocalRootIdentityIgnoresPrivateChildrenButRejectsAnotherRepositoryOrRoot()
    {
        using var f = new Fixture(); string path = System.Security.SecurityElement.Escape(f.Project);
        string xml = $"<LsResults><LsItems><LsItem><Name>.</Name><CurrentPath>{path}</CurrentPath><Type>dir</Type><Status>Controlled</Status><RepId>42</RepId><Repository>rep:repo@server</Repository></LsItem><LsItem><Name>private.txt</Name><Status>Private</Status></LsItem></LsItems></LsResults>";
        PlasticRevisionClient.VerifyProjectListing(f.Project, f.Client.Manifest, xml);
        Assert.Equal("PlasticBasisIdentityMismatch", Assert.Throws<WorkspaceException>(() => PlasticRevisionClient.VerifyProjectListing(f.Project, f.Client.Manifest, xml.Replace("rep:repo@server", "rep:other@server"))).Code);
        Assert.Throws<WorkspaceException>(() => PlasticRevisionClient.VerifyProjectListing(f.Project + "-peer", f.Client.Manifest, xml));
    }

    [Fact]
    public void BinaryRangeProofIsBoundedAndSequentialReadsReuseTheStream()
    {
        using var f = new Fixture(); using var tree = f.Tree(); byte[] target = new byte[4096];
        Assert.Equal(4096, tree.Read("data.bin", 0, target)); Assert.Equal(f.Payload[..4096], target);
        Assert.Equal(f.Payload.Length, tree.DecodedRevisionBytes); Assert.Equal(1, tree.RevisionStreamsStarted);
        tree.Read("data.bin", 65536, target); tree.Read("data.bin", 69632, target);
        Assert.Equal(2, tree.RevisionStreamsStarted); Assert.Equal(f.Payload.Length + 131072, tree.DecodedRevisionBytes);
        Assert.Equal(f.Payload[69632..73728], target);
        Assert.Equal(0, tree.Read("data.bin", long.MaxValue, target));
    }
    [Fact]
    public void EachTreeClosesItsOwnedReadSessionExactlyOnce()
    {
        using var f = new Fixture(); var tree = f.Tree(); tree.Read("data.bin", 0, new byte[4096]);
        Assert.Equal(0, f.Client.ClosedReadSessions); tree.Dispose(); tree.Dispose();
        Assert.Equal(1, f.Client.ClosedReadSessions);
        using (var next = f.Tree()) next.Read("data.bin", 0, new byte[4096]);
        Assert.Equal(2, f.Client.ClosedReadSessions);
    }
    [Fact]
    public void AbsentNestedAdministrationProbesDoNotInvalidateTheRecordedBasis()
    {
        using var f = new Fixture(); using var tree = f.Tree();
        Assert.Null(tree.Stat("Assets/.git")); Assert.Null(tree.Stat("Packages/.plastic"));
        Assert.Throws<DirectoryNotFoundException>(() => tree.List("Assets/.git"));
        Assert.Throws<FileNotFoundException>(() => tree.Read("Assets/.git/config", 0, new byte[1]));
        Assert.Equal(0, tree.RevisionStreamsStarted);
        Assert.Throws<WorkspaceException>(() => tree.Stat("Assets/../data.bin"));
    }
    [Fact]
    public void CorruptionAndLengthMismatchDoNotPublishRequestedBytes()
    {
        using var f = new Fixture(); byte[] target = Enumerable.Repeat((byte)0xcc, 4096).ToArray();
        f.Client.Corrupt = true;
        using (var tree = f.Tree()) Assert.Equal("PlasticBasisInvalid", Assert.Throws<WorkspaceException>(() => tree.Read("data.bin", 0, target)).Code);
        Assert.All(target, b => Assert.Equal(0xcc, b));
        f.Client.Corrupt = false; f.Client.Truncate = true;
        using (var tree = f.Tree()) Assert.Throws<WorkspaceException>(() => tree.Read("data.bin", 0, target));
        Assert.All(target, b => Assert.Equal(0xcc, b));
        f.Client.Truncate = false;
        using var healthy = f.Tree(); healthy.Read("data.bin", 0, target);
        f.Client.Corrupt = true;
        Assert.Throws<WorkspaceException>(() => healthy.Read("data.bin", 0, target)); // Earlier chunk must reopen and validate.
        Assert.Equal(f.Payload[..4096], target);
    }
    [Fact]
    public async Task OwnedManifestSurvivesSourceChangesAndRejectsChangedCaptureOrAnotherOwner()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        var provider = f.Provider(); var basis = await provider.CaptureAsync(new(f.Project, "owner", f.Receipt), ct);
        File.WriteAllText(Path.Combine(f.Project, "data.bin"), "later original");
        Assert.Equal(basis, await provider.CaptureAsync(new(f.Project, "owner", f.Receipt), ct));
        Assert.Equal("SnapshotIdentityMismatch", (await Assert.ThrowsAsync<WorkspaceException>(() => new PlasticRevisionSnapshotProvider(f.Receipt, "peer", client: f.Client).ResolveReadAccessAsync(basis, ct))).Code);
        var original = f.Client.Manifest; f.Client.Manifest = original with { RootRevision = "99" };
        Assert.Equal("PlasticBasisInvalid", (await Assert.ThrowsAsync<WorkspaceException>(() => provider.CaptureAsync(new(f.Project, "owner", f.Receipt), ct))).Code);
        Assert.Equal(original.RootRevision, provider.Required(basis).Manifest.RootRevision);
    }
    [Fact]
    public async Task TreePinsManifestAgainstMutationWithoutRepeatedMetadataReads()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken; var provider = f.Provider();
        var basis = await provider.CaptureAsync(new(f.Project, "owner", f.Receipt), ct);
        using (var tree = provider.OpenTree(basis, await provider.ResolveReadAccessAsync(basis, ct)))
        {
            Assert.Throws<IOException>(() => File.WriteAllText(Path.Combine(f.Receipt, "plastic-basis.json"), "corruption"));
            for (int i = 0; i < 1000; i++) Assert.NotNull(tree.Stat("data.bin"));
            Assert.Equal("SnapshotReadersActive", (await Assert.ThrowsAsync<WorkspaceException>(() => provider.ReleaseAsync(basis, ct))).Code);
        }
        Assert.Equal("released", (await provider.ReleaseAsync(basis, ct)).Disposition);
    }
    [Fact]
    public async Task ReleaseRequiresReaderAbsenceAndReusesTheSameDurableAttemptWithoutServerEffects()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        var provider = f.Provider(); var basis = await provider.CaptureAsync(new(f.Project, "owner", f.Receipt), ct);
        using (provider.AcquireReadLease())
            Assert.Equal("SnapshotReadersActive", (await Assert.ThrowsAsync<WorkspaceException>(() => provider.ReleaseAsync(basis, ct))).Code);
        int calls = f.Client.Lists; var release = await provider.ReleaseAsync(basis, ct);
        Assert.Equal(JsonSerializer.Serialize(release), JsonSerializer.Serialize(await provider.ReleaseAsync(basis, ct, release.AttemptId))); Assert.Equal(calls, f.Client.Lists);
        Assert.False((await provider.ObserveAsync(basis, ct)).Available);
        Assert.Equal("SnapshotIdentityMismatch", (await Assert.ThrowsAsync<WorkspaceException>(() => provider.ReleaseAsync(basis, ct, "different"))).Code);
        Assert.False(Directory.Exists(Path.Combine(f.Receipt, "payload")));
        var catalog = new ContractCatalog();
        using var receipt = JsonDocument.Parse(File.ReadAllText(Path.Combine(f.Receipt, "plastic-basis.json")));
        Assert.True(catalog.Validate("urn:splinter:v1:plastic-project-basis-receipt", receipt.RootElement).IsValid);
        var request = new OverlayServeRequest(new(2, new string('a', 24), basis, 1024 * 1024, 65536),
            Path.Combine(f.Root, "delta"), Path.Combine(f.Root, "mount"), Path.Combine(f.Root, "control"), new string('b', 64),
            new(basis.Id, basis.RootPath, provider.Required(basis).ManifestDigest), f.Receipt, "owner");
        Assert.True(catalog.Validate("urn:splinter:v1:overlay-serve-request", JsonSerializer.SerializeToElement(request, new JsonSerializerOptions(JsonSerializerDefaults.Web))).IsValid);
    }
    [Theory]
    [InlineData("../escape")]
    [InlineData("Library/NUL.txt")]
    [InlineData("a/.plastic/data")]
    [InlineData("a/trailing.")]
    public void UnsupportedRecordedPathsFailAdmission(string path)
    {
        using var f = new Fixture(); f.Client.Manifest = f.Client.Manifest with { Entries = [f.Client.Manifest.Entries[0] with { Path = path }] };
        Assert.Throws<WorkspaceException>(() => f.Tree());
    }
    [Fact]
    public async Task DifferentTreeAndIncompleteMetadataFailBeforePublishingABasis()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        f.Client.Manifest = f.Client.Manifest with { TreeSpec = "2@repo@server" };
        Assert.Equal("PlasticBasisIdentityMismatch", (await Assert.ThrowsAsync<WorkspaceException>(() => f.Provider().CaptureAsync(new(f.Project, "owner", f.Receipt), ct))).Code);
        Assert.False(File.Exists(Path.Combine(f.Receipt, "plastic-basis.json")));
        f.Client.Manifest = f.Client.Manifest with { Entries = [null!] };
        Assert.Equal("PlasticBasisInvalid", Assert.Throws<WorkspaceException>(() => f.Tree()).Code);
        f.Client.Manifest = f.Client.Manifest with { Entries = [new(null!, false, "1", 1, null!, 0)] };
        Assert.Equal("PlasticBasisInvalid", Assert.Throws<WorkspaceException>(() => f.Tree()).Code);
    }
    [Fact]
    public void SchemaKeepsEachSourceStorageAndLibrarySelectionExplicit()
    {
        var catalog = new ContractCatalog();
        bool Valid(object input) => catalog.Validate("urn:splinter:v1:workspace-ensure-request", JsonSerializer.SerializeToElement(input)).IsValid;
        Assert.True(Valid(new { project = "p", key = "a", storage = "plastic-snapshot", source = "plastic", baseReference = "1@repo@server", application = "none" }));
        Assert.False(Valid(new { project = "p", key = "a", storage = "plastic-snapshot", source = "git", baseReference = "1@repo@server" }));
        Assert.False(Valid(new { project = "p", key = "a", storage = "plastic-snapshot", source = "plastic" }));
        Assert.False(Valid(new { project = "p", key = "a", storage = "ntfs-snapshot", source = "none", libraryBasis = new string('a', 24) }));
    }
    [Fact]
    public async Task RecordedFileBasisPreservesIndependentPayloadsAfterOriginalAndServerBecomeUnavailable()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        Directory.CreateDirectory(Path.Combine(f.Project, ".plastic")); File.WriteAllText(Path.Combine(f.Project, ".plastic", "plastic.workspace"), "identity");
        var store = new WorkspaceStore(Path.Combine(f.Root, "home")); string id = WorkspaceStore.Key("recorded-worker");
        var w = new WorkspaceRecord { Id = id, RequestKey = "a", ProjectPath = f.Project, Path = store.WorkPath(id),
            SourceReference = "files:" + id, BaseReference = "1@repo@server", SourceKind = "plastic", ApplicationKind = "none",
            StorageMode = "plastic-snapshot", RequestDigest = "fixed-request", Snapshot = new() };
        string control = store.ControlPath(id); var source = new PlasticWorkspaceProvider(store, new LocalIdentity(f.Project), f.Client);
        w.BaseRevision = await source.PreflightAsync(w, ct);
        var provider = new PlasticRevisionSnapshotProvider(Path.Combine(control, "snapshot"), id, w.BaseReference, f.Client);
        w.Snapshot.Reference = await provider.CaptureAsync(new(f.Project, id, Path.Combine(control, "snapshot")), ct);
        w.Snapshot.Observation = new(true, true, true, true, 0, 0, 0) { SnapshotStorage = await provider.ObserveAsync(w.Snapshot.Reference, ct) };
        await source.RegisterAsync(w, control, ct);
        w.Snapshot.Mount = new(w.Id, w.Path, Path.Combine(control, "delta"), w.Snapshot.Reference, 1, 1);
        // This file mount is a test double, not WinFsp/no-copy native evidence.
        Directory.CreateDirectory(w.Path); File.WriteAllBytes(Path.Combine(w.Path, "data.bin"), f.Payload);
        File.SetLastWriteTimeUtc(Path.Combine(w.Path, "data.bin"), DateTime.FromFileTimeUtc(f.Client.Manifest.Entries[0].Timestamp));
        await source.AttachAsync(w, control, ct); w.BaseRevision = await source.EstablishBaselineAsync(w, control, ct);
        Assert.False(Directory.Exists(Path.Combine(control, "files", "objects")));
        File.WriteAllText(Path.Combine(f.Project, "data.bin"), "later original");
        File.WriteAllText(Path.Combine(w.Path, "data.bin"), "independent result");
        w.ResultRevision = await source.CheckpointAsync(w, "result", ct);
        string reference = await source.PreserveAsync(w, control, ct);
        w.Snapshot.Result = new(w.Snapshot.Reference.Id, w.ResultRevision, w.ResultRevision, reference, new(0, 0, "seal"), true, SnapshotResult.StableJournalProtocol);
        var reclaimed = await provider.ReleaseAsync(w.Snapshot.Reference, ct); Directory.Delete(w.Path, true); Directory.Delete(f.Project, true);
        FileWorkspaceProvider.VerifyIndependentResult(w, control); await source.VerifyResultAsync(w, control, ct);
        w.GeneratedDirectory = new("generated", w.ProjectPath, "Library", w.Snapshot.Reference, f.Receipt, "generated-owner", "hash");
        var backend = new NtfsSnapshotWorkspaceBackend(Path.Combine(store.Root, "runners"));
        Assert.Equal("GeneratedBasisUnsupported", (await Assert.ThrowsAsync<WorkspaceException>(() =>
            new WindowsWorkspaceDiscardProvider(source, backend).ObserveSourceRevisionAsync(w, control, ct))).Code);
        w.Phase = "discarding"; w.Snapshot.Step = "discard-snapshot";
        w.Discard = new() { Intent = new(1, w.Id, "discard", w.RequestDigest!, w.ProjectPath, w.Path, w.SourceReference,
            w.BaseRevision!, w.BaseRevision!, w.Snapshot.Reference, DateTimeOffset.UtcNow), Step = "snapshot", Seal = new(0, 0, "seal") };
        await source.DiscardSourceAsync(w, control, ct);
        var dependencies = new FailingDependency(); var cleanup = new WindowsWorkspaceDiscardProvider(source, backend, dependencies);
        await Assert.ThrowsAsync<WorkspaceException>(() => cleanup.DiscardSnapshotAsync(w, control, ct));
        var retried = await cleanup.DiscardSnapshotAsync(w, control, ct);
        Assert.Equal(reclaimed.AttemptId, retried.AttemptId); Assert.Equal(2, dependencies.Detaches);
        FileWorkspaceProvider.VerifyIndependentResult(w, control);
    }
    private sealed class FailingDependency : IGeneratedDirectoryDependencies
    {
        public int Detaches;
        public Task<GeneratedDirectoryReference> AttachAsync(string id, WorkspaceRecord workspace, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DetachAsync(GeneratedDirectoryReference basis, WorkspaceRecord workspace, CancellationToken ct = default)
        { if (++Detaches == 1) throw new WorkspaceException("GeneratedBasisInUse", "injected reply boundary"); return Task.CompletedTask; }
    }
    private sealed class LocalIdentity(string project) : IPlasticWorkspaceClient
    {
        public Task<string> VersionAsync(CancellationToken ct = default) => Task.FromResult("11.0.16.9915");
        public Task<PlasticWorkspaceIdentity> InspectAsync(string path, CancellationToken ct = default) => Task.FromResult(new PlasticWorkspaceIdentity(project, "11111111-1111-1111-1111-111111111111", "regular", "static"));
        public Task<PlasticWorkspaceLocation[]> ListAsync(CancellationToken ct = default) => Task.FromResult(new[] { new PlasticWorkspaceLocation(project, "11111111-1111-1111-1111-111111111111") });
    }
    private sealed class Fixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "splinter-plastic-revisions-" + Guid.NewGuid().ToString("N"));
        public string Root => root;
        public string Project => Path.Combine(root, "source"); public string Receipt => Path.Combine(root, "basis");
        public byte[] Payload { get; } = new byte[2 * 1024 * 1024]; public FakeClient Client { get; }
        public Fixture()
        {
            RandomNumberGenerator.Fill(Payload); Directory.CreateDirectory(Project); Directory.CreateDirectory(Receipt);
            Client = new(Payload); File.WriteAllBytes(Path.Combine(Project, "data.bin"), Payload);
        }
        public PlasticRevisionSnapshotProvider Provider() => new(Receipt, "owner", "1@repo@server", Client);
        public PlasticRevisionSnapshotTree Tree() => new(new("snapshot", PlasticRevisionSnapshotProvider.ProviderName, Project, Receipt,
            DateTimeOffset.UtcNow, true, true, PlasticRevisionSnapshotProvider.Consistency), Client.Manifest, Client);
        public void Dispose() { Directory.Delete(root, true); }
    }
    private sealed class FakeClient(byte[] payload) : IPlasticRevisionClient
    {
        public bool Corrupt, Truncate; public int Lists, ClosedReadSessions;
        public PlasticRevisionManifest Manifest { get; set; } = new("1@repo@server", "42", "1", "11111111-1111-1111-1111-111111111111",
            [new("data.bin", false, "2", payload.Length, Convert.ToBase64String(MD5.HashData(payload)), DateTime.UtcNow.ToFileTimeUtc())], "rep:repo@server");
        public Task<PlasticRevisionManifest> ListAsync(string treeSpec, CancellationToken ct = default) { Lists++; return Task.FromResult(Manifest); }
        public Task VerifyProjectAsync(string project, PlasticRevisionManifest manifest, CancellationToken ct = default) => Task.CompletedTask;
        public IPlasticRevisionReader CreateReadSession() => new FakeSession(this);
        private sealed class FakeSession(FakeClient owner) : IPlasticRevisionReader, IDisposable
        {
            private bool closed;
            public Stream OpenRevision(string treeSpec, string revision) => owner.OpenRevision(treeSpec, revision);
            public void Dispose() { if (!closed) { closed = true; owner.ClosedReadSessions++; } }
        }
        public Stream OpenRevision(string treeSpec, string revision)
        {
            byte[] bytes = (byte[])payload.Clone(); if (Corrupt) bytes[0] ^= 1;
            return new MemoryStream(bytes, 0, Truncate ? bytes.Length - 1 : bytes.Length, false);
        }
    }
}
