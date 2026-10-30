using System.Text.Json;
using Splinter.Core;
using Splinter.Cli;
using Splinter.Runtime;
using Splinter.SourceControl.Files;

namespace Splinter.Tests;

// Real file-source adapter with a synthetic snapshot backend. Copying here is
// test fixture setup only; it does not qualify VSS/WinFsp or Editor behavior.
public sealed partial class FileWorkspaceTests
{
    [Fact]
    public async Task OrdinaryFolderResumesKeepsCapturedBasisAndPreservesActualBytesWithoutGit()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        var a = (await f.Ensure("a", ct)).Workspace;
        var b = (await f.Ensure("b", ct)).Workspace;
        Assert.NotEqual(a.Path, b.Path); Assert.NotEqual(a.Id, b.Id);
        Assert.Empty(a.ManagedPaths); Assert.Null(a.Application);
        Assert.False(Directory.Exists(Path.Combine(f.Store.ControlPath(a.Id), "files", "objects")));
        File.WriteAllText(Path.Combine(f.Project, "Library", "user.txt"), "original changed");
        File.WriteAllText(Path.Combine(a.Path, "data.txt"), "worker-a");
        Assert.Equal("basis", File.ReadAllText(Path.Combine(b.Path, "data.txt")));
        var resumed = await f.Ensure("a", ct);
        Assert.Equal(a.Id, resumed.Workspace.Id); Assert.Equal(a.BaseRevision, resumed.Workspace.BaseRevision);
        Assert.Equal(2, f.Backend.Captures);
        Assert.Equal("basis", File.ReadAllText(Path.Combine(a.Path, "Library", "user.txt")));
        var collected = await f.New().CheckpointAsync(a.Id, "independent files", ct);
        var catalog = new Splinter.Protocol.ContractCatalog();
        var response = WorkspaceCommands.Result("workspace.checkpoint", collected, false, f.Store.Root);
        Assert.True(catalog.Validate("urn:splinter:v1:response", JsonSerializer.SerializeToElement(response, Splinter.Protocol.ApiJson.Options)).IsValid);
        Assert.Equal("workspace release --id " + a.Id, collected.NextCommand);
        Assert.Equal(10, collected.Workspace.ResultStorage!.BasisLogicalBytes);
        Assert.Equal(13, collected.Workspace.ResultStorage.ResultLogicalBytes);
        Assert.Equal(13, collected.Workspace.ResultStorage.UniquePayloadBytes);
        Assert.Equal(2, collected.Workspace.ResultStorage.PayloadObjects);
        string control = f.Store.ControlPath(a.Id);
        using var resultManifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(control, "files", "result.json")));
        Assert.True(catalog.Validate("urn:splinter:v1:file-source-manifest", resultManifest.RootElement).IsValid);
        var before = File.ReadAllText(Path.Combine(control, "workspace.json"));
        Assert.Equal("SourceIntegrationUnsupported", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().IntegrateAsync(a.Id, "ignored", ct))).Code);
        Assert.Equal(before, File.ReadAllText(Path.Combine(control, "workspace.json")));
        var released = await f.New().ReleaseAsync(a.Id, ct);
        Assert.Equal("released", released.Workspace.Phase);
        Assert.False(Directory.Exists(a.Path)); Assert.True(Directory.Exists(b.Path));
        Assert.Contains(a.Id, f.Backend.Released); Assert.DoesNotContain(b.Id, f.Backend.Released);
        // Verification must work after both the live original and immutable
        // fake snapshot are unavailable, and after the worker mount is removed.
        Directory.Move(f.Project, f.Project + "-moved");
        FileWorkspaceProvider.VerifyIndependentResult(f.Store.Get(a.Id), control);
        var objects = Directory.GetFiles(Path.Combine(control, "files", "objects"));
        Assert.Contains(objects, p => File.ReadAllText(p) == "basis");
        Assert.Contains(objects, p => File.ReadAllText(p) == "worker-a");
        Assert.Equal(0, f.Git.Calls);
    }

    [Fact]
    public async Task DirtyDiscardRetainsPeerAndPreviousIndependentResults()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        var a = (await f.Ensure("keep", ct)).Workspace;
        var b = (await f.Ensure("discard", ct)).Workspace;
        File.WriteAllText(Path.Combine(b.Path, "data.txt"), "dirty failed work");
        Assert.Equal("discarded", (await f.New().DiscardAsync(new(b.Id, "explicit", b.RequestDigest!), ct)).Workspace.Phase);
        Assert.True(Directory.Exists(a.Path)); Assert.DoesNotContain(a.Id, f.Backend.Released);
        Assert.False(File.Exists(Path.Combine(f.Store.ControlPath(b.Id), "files", "result.json")));
        Assert.Equal("discarded", (await f.New().DiscardAsync(new(b.Id, "explicit", b.RequestDigest!), ct)).Workspace.Phase);
        Assert.Equal("DiscardRequestConflict", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().DiscardAsync(new(b.Id, "different", b.RequestDigest!), ct))).Code);
        var kept = await f.New().CheckpointAsync(a.Id, "keep", ct);
        await f.New().DiscardAsync(new(a.Id, "discard-working-resources", a.RequestDigest!), ct);
        FileWorkspaceProvider.VerifyIndependentResult(f.Store.Get(a.Id), f.Store.ControlPath(a.Id));
        Assert.Equal(kept.Workspace.ResultRevision, f.Store.Get(a.Id).ResultRevision);
        Assert.Equal(0, f.Git.Calls);
    }

    [Theory]
    [InlineData("capture")]
    [InlineData("connect")]
    [InlineData("unmount")]
    [InlineData("remove")]
    [InlineData("release")]
    public async Task LostResponseResumesTheSameIdentity(string boundary)
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        if (boundary is "capture" or "connect")
        {
            f.Backend.Fail = boundary;
            Assert.Equal("ReplyLost", (await Assert.ThrowsAsync<WorkspaceException>(() => f.Ensure("a", ct))).Code);
        }
        var a = (await f.Ensure("a", ct)).Workspace;
        Assert.Equal(1, f.Backend.Captures);
        if (boundary is "unmount" or "remove" or "release")
        {
            f.Backend.Fail = boundary;
            Assert.Equal("ReplyLost", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ReleaseAsync(a.Id, ct))).Code);
        }
        Assert.Equal("released", (await f.New().ReleaseAsync(a.Id, ct)).Workspace.Phase);
        FileWorkspaceProvider.VerifyIndependentResult(f.Store.Get(a.Id), f.Store.ControlPath(a.Id));
        Assert.Equal(0, f.Git.Calls);
    }

    [Fact]
    public async Task CorruptPreservedPayloadBlocksReleaseAndIsNeverRewritten()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure("a", ct)).Workspace;
        await f.New().CheckpointAsync(w.Id, "preserve", ct);
        string payload = Directory.GetFiles(Path.Combine(f.Store.ControlPath(w.Id), "files", "objects")).Single();
        File.WriteAllText(payload, "broken");
        Assert.Equal("FileResultIncomplete", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ReleaseAsync(w.Id, ct))).Code);
        Assert.Equal("broken", File.ReadAllText(payload)); Assert.True(Directory.Exists(w.Path));
        Assert.Empty(f.Backend.Released);
    }

    [Fact]
    public async Task StableSealRejectsWritesOverlappingCollection()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure("a", ct)).Workspace;
        f.Backend.ChangeSeal = true;
        Assert.Equal("DeltaChangedAfterCollection", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().CheckpointAsync(w.Id, "preserve", ct))).Code);
        Assert.Null(f.Store.Get(w.Id).Snapshot!.Result); Assert.True(Directory.Exists(w.Path));
        f.Backend.ChangeSeal = false;
        Assert.Equal("collected", (await f.New().CheckpointAsync(w.Id, "preserve", ct)).Workspace.Phase);
    }

    [Theory]
    [InlineData(".git")]
    [InlineData(".plastic")]
    [InlineData(".svn")]
    [InlineData(".hg")]
    public async Task VcsAdministrationIsRejectedBeforeCapture(string name)
    {
        using var f = new Fixture(); Directory.CreateDirectory(Path.Combine(f.Project, name));
        Assert.Equal("VcsMetadataUnsupported", (await Assert.ThrowsAsync<WorkspaceException>(() => f.Ensure("a", TestContext.Current.CancellationToken))).Code);
        Assert.Equal(0, f.Backend.Captures); Assert.Equal(0, f.Git.Calls);
    }

    [Fact]
    public async Task SelectionIsImmutableAndGitFailureNeverFallsBack()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        var a = (await f.Ensure("a", ct)).Workspace;
        Assert.Equal("RequestKeyConflict", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().EnsureAsync(f.Project, "a", "snapshot", "git-snapshot", false, ct, sourceKind: "git", applicationKind: "none"))).Code);
        Assert.Equal("GitRequiredFailure", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().EnsureAsync(f.Project, "git", "branch", "git-snapshot", false, ct, applicationKind: "none"))).Code);
        Assert.Equal(1, f.Git.Calls); Assert.Equal(1, f.Backend.Captures);
        Assert.Equal("ApplicationUnsupported", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().StartAsync(a.Id, ct))).Code);
        Assert.Null(f.Store.Get(a.Id).Application);
    }

    [Fact]
    public async Task CurrentRecordRequiresSelectionAndRejectsEarlierVersionWithoutMigration()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        var w = (await f.Ensure("a", ct)).Workspace;
        string path = Path.Combine(f.Store.ControlPath(w.Id), "workspace.json"), original = File.ReadAllText(path);
        var d = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(original)!;
        d.Remove("sourceKind"); File.WriteAllText(path, JsonSerializer.Serialize(d));
        Assert.Equal("StateCorrupt", Assert.Throws<WorkspaceException>(() => f.Store.Get(w.Id)).Code);
        File.WriteAllText(path, original.Replace("\"version\": 3", "\"version\": 2", StringComparison.Ordinal));
        Assert.Equal("StateVersionUnsupported", Assert.Throws<WorkspaceException>(() => f.Store.Get(w.Id)).Code);
        Assert.True(Directory.Exists(w.Path)); Assert.Empty(f.Backend.Released);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly TestDirectory directory = new();
        public string Project { get; }
        public WorkspaceStore Store { get; }
        public Backend Backend { get; } = new();
        public ForbiddenGit Git { get; } = new();
        public PlasticClient Plastic { get; }
        public FixtureRevisions Revisions { get; }
        private readonly IOriginalFilePublisher? publisher;
        private readonly string sourceKind;
        public Fixture(IOriginalFilePublisher? publisher = null, string sourceKind = "none")
        {
            Revisions = new(); Backend.Revisions = Revisions;
            this.publisher = publisher;
            this.sourceKind = sourceKind;
            Project = Path.Combine(directory.Path, "source"); Store = new(Path.Combine(directory.Path, "home"));
            Plastic = new(Project);
            Directory.CreateDirectory(Path.Combine(Project, "Library"));
            File.WriteAllText(Path.Combine(Project, "data.txt"), "basis");
            File.WriteAllText(Path.Combine(Project, "Library", "user.txt"), "basis");
            if (sourceKind == "plastic")
            { Directory.CreateDirectory(Path.Combine(Project, ".plastic")); File.WriteAllText(Path.Combine(Project, ".plastic", "plastic.workspace"), Plastic.Guid); }
        }
        public WorkspaceCoordinator New(IWorkspacePreparationObserver? observer = null)
        {
            var files = new FileWorkspaceProvider(Store, publisher, projectAccess: new ProjectAccess(), preparationObserver: observer);
            var sources = new SourceWorkspaceRouter(Git, files, new PlasticWorkspaceProvider(Store, Plastic, Revisions));
            return new(Store, sources, new SelectedApplicationProvider(new NoApplication()),
                snapshots: Backend, discardProvider: new Discard(sources, Backend), preparationObserver: observer);
        }
        public Task<WorkspaceView> Ensure(string key, CancellationToken ct) => New().EnsureAsync(Project, key, sourceKind == "plastic" ? "1@repo@server" : "snapshot", sourceKind == "plastic" ? "plastic-snapshot" : "project-snapshot", false, ct, sourceKind: sourceKind, applicationKind: "none", generatedDirectoryId: sourceKind == "none" ? new string('a', 24) : null);
        public void Dispose() => directory.Dispose();
    }
    private sealed class ProjectAccess : IProjectSourceAccess
    {
        public IDisposable Acquire(WorkspaceRecord w, bool publishing, CancellationToken ct = default) => new Lease();
        private sealed class Lease : IDisposable { public void Dispose() { } }
    }
    private sealed class FixtureRevisions : IPlasticRevisionClient
    {
        public bool NestedVcs;
        private static readonly long Time = DateTime.UtcNow.ToFileTimeUtc();
        public Task<PlasticRevisionManifest> ListAsync(string treeSpec, CancellationToken ct = default) => Task.FromResult(new PlasticRevisionManifest(treeSpec, "42", "1", "11111111-1111-1111-1111-111111111111", [
            new("data.txt", false, "2", 5, Convert.ToBase64String(System.Security.Cryptography.MD5.HashData("basis"u8)), Time),
            new("Library", true, "3", 0, "1B2M2Y8AsgTpgAmY7PhCfg==", Time),
            new(NestedVcs ? "Library/.plastic" : "Library/user.txt", false, "4", 5, Convert.ToBase64String(System.Security.Cryptography.MD5.HashData("basis"u8)), Time)], "rep:repo@server"));
        public Task VerifyProjectAsync(string project, PlasticRevisionManifest manifest, CancellationToken ct = default) => Task.CompletedTask;
        public Stream OpenRevision(string treeSpec, string revision) => new MemoryStream("basis"u8.ToArray());
    }
    private sealed class NoApplication : IApplicationWorkspaceProvider
    {
        public string[] ManagedPaths => throw new InvalidOperationException("No application metadata for ordinary folders");

        public string CompatibilityKey(string p) => throw new InvalidOperationException();
        public Task<ApplicationLease> StartAsync(WorkspaceRecord w, string c, CancellationToken ct = default) => throw new InvalidOperationException();
        public Task<ApplicationState> ObserveAsync(WorkspaceRecord w, string c, CancellationToken ct = default) => throw new InvalidOperationException();
        public Task StopAsync(WorkspaceRecord w, string c, CancellationToken ct = default) => throw new InvalidOperationException();
        public Task RemoveInfrastructureAsync(WorkspaceRecord w, string c, CancellationToken ct = default) => throw new InvalidOperationException();
    }
    private sealed class ForbiddenGit : ISourceWorkspaceProvider, ISnapshotSourceWorkspaceProvider
    {
        public int Calls;
        private Exception Fail() { Calls++; return new WorkspaceException("GitRequiredFailure", "Git adapter unavailable; no fallback."); }
        public Task<string> ResolveRevisionAsync(string p, string r, CancellationToken ct = default) => throw Fail();
        public Task<SourceWorkspace> CreateAsync(WorkspaceRecord w, CancellationToken ct = default) => throw Fail();
        public Task<SourceState> InspectAsync(WorkspaceRecord w, CancellationToken ct = default) => throw Fail();
        public Task<string> CheckpointAsync(WorkspaceRecord w, string m, CancellationToken ct = default) => throw Fail();
        public Task<IntegrationResult> IntegrateAsync(WorkspaceRecord w, string t, CancellationToken ct = default) => throw Fail();
        public Task ReleaseAsync(WorkspaceRecord w, CancellationToken ct = default) => throw Fail();
        public Task<string> PreflightAsync(WorkspaceRecord w, CancellationToken ct = default) => throw Fail();
        public Task RegisterAsync(WorkspaceRecord w, string c, CancellationToken ct = default) => throw Fail();
        public Task AttachAsync(WorkspaceRecord w, string c, CancellationToken ct = default) => throw Fail();
        public Task<string> PreserveAsync(WorkspaceRecord w, string c, CancellationToken ct = default) => throw Fail();
        public Task VerifyResultAsync(WorkspaceRecord w, string c, CancellationToken ct = default) => throw Fail();
        public Task UnregisterAsync(WorkspaceRecord w, string c, CancellationToken ct = default) => throw Fail();
    }
    private sealed class Discard(ISourceDiscardProvider files, Backend backend) : IWorkspaceDiscardProvider
    {
        public Task<string> ObserveSourceRevisionAsync(WorkspaceRecord w, string c, CancellationToken ct) => files.ObserveDiscardRevisionAsync(w, c, ct);
        public Task DiscardSourceAsync(WorkspaceRecord w, string c, CancellationToken ct) => files.DiscardSourceAsync(w, c, ct);
        public async Task DiscardDeltaAsync(WorkspaceRecord w, string c, CancellationToken ct) { await files.VerifyDiscardedAsync(w, c, ct); backend.Removed.Add(w.Id); }
        public Task<SnapshotReclamation> DiscardSnapshotAsync(WorkspaceRecord w, string c, CancellationToken ct) => backend.ReleaseSnapshotAsync(w, c, ct);
    }
    private sealed partial class Backend : ISnapshotWorkspaceBackend
    {
        public FixtureRevisions Revisions = null!;
        public int Captures; public string? Fail; public bool ChangeSeal;
        public int SealCalls;
        public Action<WorkspaceRecord, int>? OnSeal;
        public HashSet<string> Mounted = [], Removed = [], Released = [];
        private readonly Dictionary<string, SnapshotReference> receipts = [];
        private long sequence;
        private void Lost(string boundary) { if (Fail == boundary) { Fail = null; throw new WorkspaceException("ReplyLost", boundary); } }
        public async Task<SnapshotReference> CaptureAsync(WorkspaceRecord w, string c, CancellationToken ct = default)
        {
            if (!receipts.TryGetValue(w.Id, out var r))
            {
                string root = Path.Combine(c, "fake-immutable");
                if (w.SourceKind != "plastic") Copy(w.ProjectPath, root);
                receipts[w.Id] = r = w.SourceKind == "plastic"
                    ? await new PlasticRevisionSnapshotProvider(Path.Combine(c, "snapshot"), w.Id, w.BaseReference, client: Revisions).CaptureAsync(new(w.ProjectPath, w.Id, Path.Combine(c, "snapshot")), ct)
                    : new(w.Id, "fake", w.ProjectPath, root, DateTimeOffset.UtcNow, true, true, "fake"); Captures++;
                if (w.SourceKind == "plastic")
                {
                    var recorded = new PlasticRevisionSnapshotProvider(Path.Combine(c, "snapshot"), w.Id, client: Revisions);
                    using var lease = recorded.AcquireReadLease();
                    using var tree = recorded.OpenTree(r, await recorded.ResolveReadAccessAsync(r, ct));
                    Materialize(tree, "", root);
                }
            }
            if (w.SourceKind == "none") w.GeneratedDirectory = new(w.GeneratedDirectoryId!, w.ProjectPath, "", r, c, w.GeneratedDirectoryId!, new string('a', 64));
            Lost("capture"); return r;
        }
        public Task VerifyConnectionAsync(WorkspaceRecord w, string c, CancellationToken ct = default) => Task.CompletedTask;
        public async Task<OverlayMount> ConnectAsync(WorkspaceRecord w, string c, CancellationToken ct = default)
        {
            if (!Directory.Exists(w.Path))
            {
                if (w.SourceKind == "plastic")
                {
                    var provider = new PlasticRevisionSnapshotProvider(Path.Combine(c, "snapshot"), w.Id, client: Revisions);
                    using var lease = provider.AcquireReadLease();
                    using var tree = provider.OpenTree(w.Snapshot!.Reference!, await provider.ResolveReadAccessAsync(w.Snapshot.Reference!, ct));
                    Materialize(tree, "", w.Path);
                }
                else Copy(Path.Combine(c, "fake-immutable"), w.Path);
            }
            Mounted.Add(w.Id); Lost("connect"); return new OverlayMount(w.Id, w.Path, "fake", w.Snapshot!.Reference!, 1, 1);
        }
        private static void Materialize(ISnapshotTree tree, string relative, string target)
        {
            Directory.CreateDirectory(target);
            foreach (var entry in tree.List(relative))
            {
                string path = Path.Combine(target, entry.Path.Split('/')[^1]);
                if (entry.Directory) Materialize(tree, entry.Path, path);
                else { byte[] bytes = new byte[(int)entry.Length]; Assert.Equal(bytes.Length, tree.Read(entry.Path, 0, bytes)); File.WriteAllBytes(path, bytes); }
                if (entry.Directory) Directory.SetLastWriteTimeUtc(path, DateTime.FromFileTimeUtc(entry.LastWriteTime));
                else File.SetLastWriteTimeUtc(path, DateTime.FromFileTimeUtc(entry.LastWriteTime));
                File.SetAttributes(path, (FileAttributes)entry.Attributes);
            }
        }
        public Task<SnapshotWorkspaceObservation> ObserveAsync(WorkspaceRecord w, string c, CancellationToken ct = default) => Task.FromResult(
            new SnapshotWorkspaceObservation(!Released.Contains(w.Id), Mounted.Contains(w.Id), Mounted.Contains(w.Id), true, 0, 0, 0,
                SnapshotStorage: w.Snapshot?.Reference is { } r ? new SnapshotObservation(r.Id, !Released.Contains(w.Id), null, null, null) { ReadAccess = new(r.Id, r.RootPath, "fake-verified") } : null,
                DeltaPresent: !Removed.Contains(w.Id)));
        public Task<SnapshotDeltaSeal> SealAsync(WorkspaceRecord w, string c, CancellationToken ct = default)
        { OnSeal?.Invoke(w, ++SealCalls); if (ChangeSeal) sequence++; return Task.FromResult(new SnapshotDeltaSeal(sequence, 10, "fake-seal-" + sequence)); }
        public Task UnmountAsync(WorkspaceRecord w, string c, CancellationToken ct = default)
        { if (Directory.Exists(w.Path)) Directory.Delete(w.Path, true); Mounted.Remove(w.Id); Lost("unmount"); return Task.CompletedTask; }
        public Task RemoveDeltaAsync(WorkspaceRecord w, string c, CancellationToken ct = default) { Removed.Add(w.Id); Lost("remove"); return Task.CompletedTask; }
        public Task<SnapshotReclamation> ReleaseSnapshotAsync(WorkspaceRecord w, string c, CancellationToken ct = default)
        { Released.Add(w.Id); if (Directory.Exists(Path.Combine(c, "fake-immutable"))) Directory.Delete(Path.Combine(c, "fake-immutable"), true); Lost("release"); return Task.FromResult(new SnapshotReclamation(w.Snapshot!.Reference!.Id, "fake", "released", DateTimeOffset.UtcNow, [])); }
        public Task<SnapshotJournalCompaction> CompactAsync(WorkspaceRecord w, string c, CancellationToken ct = default, long? maxEstimatedJournalOverlapBytes = null, bool? revertBaseRanges = null) => throw new InvalidOperationException();
        public Task CompleteCompactionAsync(WorkspaceRecord w, string c, CancellationToken ct = default) => throw new InvalidOperationException();
        private static void Copy(string source, string target, bool omitPlastic = false)
        {
            Directory.CreateDirectory(target);
            foreach (string file in Directory.GetFiles(source)) { string dest = Path.Combine(target, Path.GetFileName(file)); File.Copy(file, dest); File.SetLastWriteTimeUtc(dest, File.GetLastWriteTimeUtc(file)); File.SetAttributes(dest, File.GetAttributes(file)); }
            foreach (string dir in Directory.GetDirectories(source)) { if (omitPlastic && Path.GetFileName(dir) == ".plastic") continue; string dest = Path.Combine(target, Path.GetFileName(dir)); Copy(dir, dest); Directory.SetLastWriteTimeUtc(dest, Directory.GetLastWriteTimeUtc(dir)); }
        }
    }
    private sealed class PlasticClient(string project) : IPlasticWorkspaceClient
    {
        public string Guid = System.Guid.NewGuid().ToString("D"), Type = "regular", Dynamic = "static";
        public string? RootOverride, AdditionalRoot; public bool Unavailable;
        private void Check() { if (Unavailable) throw new WorkspaceException("PlasticObservationUnavailable", "unknown inventory"); }
        public Task<string> VersionAsync(CancellationToken ct = default) { Check(); return Task.FromResult("11.0.16.10484"); }
        public Task<PlasticWorkspaceIdentity> InspectAsync(string path, CancellationToken ct = default)
        { Check(); return Task.FromResult(new PlasticWorkspaceIdentity(RootOverride ?? project, Guid, Type, Dynamic)); }
        public Task<PlasticWorkspaceLocation[]> ListAsync(CancellationToken ct = default)
        { Check(); return Task.FromResult(AdditionalRoot is null ? new[] { new PlasticWorkspaceLocation(project, Guid) } : [new(project, Guid), new(AdditionalRoot, System.Guid.NewGuid().ToString("D"))]); }
    }
}
