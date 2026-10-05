using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Protocol;
using Splinter.Runtime;
using Splinter.SourceControl.Files;
using Splinter.Application.Unity;

namespace Splinter.Tests;

public sealed class PlasticOriginalApplyTests
{
    [Fact]
    public async Task InjectedApplicationOwnsCompanionGuardsAndOriginalReservation()
    {
        var policy = new ProjectPolicy();
        using var f = new Fixture(policies: new(policy)); var ct = TestContext.Current.CancellationToken;
        await f.Prepare(ct); f.Write("Assets/item.txt", "changed"); await f.Collect(ct);
        var plan = await f.Source.PlanOriginalAsync(f.W, ct, ["Assets/item.txt"]);
        Assert.Empty(plan.Conflicts);
        Assert.Contains(plan.Changes, c => c.Path == "Packages/manifest.json" && c.AlreadyMatches);
        Assert.DoesNotContain(plan.Changes, c => c.Path == "Assets/item.txt.meta");
        Assert.Equal(1, policy.Acquired); Assert.Equal(1, policy.Released);
        policy.Refuse = true;
        Assert.Equal("FakeOriginalBusy", (await Assert.ThrowsAsync<WorkspaceException>(() =>
            f.Source.ApplyOriginalAsync(f.W, "apply", plan.Digest, ct, ["Assets/item.txt"]))).Code);
        Assert.Null(f.W.OriginalApply);
        Assert.Equal("basis", File.ReadAllText(Path.Combine(f.Project, "Assets", "item.txt")));
        policy.Refuse = false;
        File.WriteAllText(Path.Combine(f.Project, "Packages", "manifest.json"), "foreign");
        var changed = await f.Source.PlanOriginalAsync(f.W, ct, ["Assets/item.txt"]);
        Assert.Contains(changed.Conflicts, c => c.StartsWith("Packages/manifest.json:", StringComparison.Ordinal));
        Assert.Equal(policy.Acquired, policy.Released);
    }
    [Fact]
    public async Task MissingApplicationPolicyFailsBeforeIntentOrOriginalWrites()
    {
        using var f = new Fixture(policies: ApplicationProjectPolicies.None); var ct = TestContext.Current.CancellationToken;
        await f.Prepare(ct); f.Write("Assets/item.txt", "changed"); await f.Collect(ct);
        Assert.Equal("ApplicationPolicyUnsupported", (await Assert.ThrowsAsync<WorkspaceException>(() =>
            f.Source.PlanOriginalAsync(f.W, ct))).Code);
        Assert.Null(f.W.OriginalApply); Assert.Equal("basis", File.ReadAllText(Path.Combine(f.Project, "Assets", "item.txt")));
    }
    private sealed class ProjectPolicy : IApplicationProjectPolicy
    {
        public bool Refuse;
        public int Acquired, Released;
        public string ApplicationKind => "unity"; // Substitute a policy in the currently admitted slot.
        public GeneratedDirectoryDescriptor? GeneratedDirectory => null;
        public string CompatibilityKey(string project) => "synthetic";
        public IDisposable AcquireOriginal(string project, bool reservePublication = false, string? verifiedManagedRoot = null)
        {
            if (Refuse) throw new WorkspaceException("FakeOriginalBusy", "synthetic original reservation refusal");
            Acquired++; return new Release(this);
        }
        public ApplicationSelection CloseSelection(IEnumerable<string> paths, Func<string, bool> before, Func<string, bool> after)
            => new(paths.Append("Packages/manifest.json").ToArray(), ["Packages/manifest.json"]);
        private sealed class Release(ProjectPolicy policy) : IDisposable { public void Dispose() => policy.Released++; }
    }
    [Fact]
    public async Task SelectedUnityChangesPinMetadataAndPreserveWithheldOriginalAndAdministration()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        await f.Prepare(ct); f.Write("Assets/new/selected.txt", "selected"); f.Write("Assets/new/selected.txt.meta", "selected-guid");
        f.Write("Assets/new/withheld.txt", "withheld"); f.Write("Assets/new/withheld.txt.meta", "withheld-guid"); f.Write("Assets/new.meta", "folder-guid");
        f.Write("Assets/item.txt", "changed"); await f.Collect(ct);
        File.WriteAllText(Path.Combine(f.Project, "unrelated.txt"), "keep");
        string[] paths = ["Assets/new/selected.txt", "Assets/item.txt"];
        byte[] stateBefore = File.ReadAllBytes(Path.Combine(f.Project, ".plastic", "plastic.wktree"));
        var plan = await f.Source.PlanOriginalAsync(f.W, ct, paths);
        Assert.Empty(plan.Conflicts); Assert.Contains(plan.Changes, c => c.Path == "Assets/item.txt.meta" && c.AlreadyMatches);
        Assert.Contains(plan.Changes, c => c.Path == "Assets/new.meta"); Assert.DoesNotContain(plan.Changes, c => c.Path.Contains("withheld"));
        Assert.Null(f.W.OriginalApply); Assert.False(Directory.Exists(Path.Combine(f.Project, "Assets", "new")));
        await f.Source.ApplyOriginalAsync(f.W, "apply", plan.Digest, ct, paths);
        f.W.OriginalApply!.RequireMatch(f.W); Assert.True(f.W.OriginalApply.Completed);
        Assert.Equal("changed", File.ReadAllText(Path.Combine(f.Project, "Assets", "item.txt")));
        Assert.Equal("selected", File.ReadAllText(Path.Combine(f.Project, "Assets", "new", "selected.txt")));
        Assert.False(File.Exists(Path.Combine(f.Project, "Assets", "new", "withheld.txt")));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(f.Project, "unrelated.txt")));
        Assert.Equal(stateBefore, File.ReadAllBytes(Path.Combine(f.Project, ".plastic", "plastic.wktree")));
        Assert.Empty(Directory.GetFileSystemEntries(f.Project, ".splinter-apply-*", SearchOption.AllDirectories));
        Assert.True((await f.Source.ApplyOriginalAsync(f.W, "apply", plan.Digest, ct, paths)).AlreadyIntegrated);
        Assert.Equal("OriginalApplyRequestConflict", (await Assert.ThrowsAsync<WorkspaceException>(() => f.Source.ApplyOriginalAsync(f.W, "apply", plan.Digest, ct))).Code);
    }
    [Fact]
    public async Task MetadataConflictAndHeldOriginalEditorRefuseBeforeIntent()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        await f.Prepare(ct); f.Write("Assets/item.txt", "changed"); await f.Collect(ct);
        File.WriteAllText(Path.Combine(f.Project, "Assets", "item.txt.meta"), "foreign-guid");
        var plan = await f.Source.PlanOriginalAsync(f.W, ct, ["Assets/item.txt"]);
        Assert.Contains(plan.Conflicts, c => c.StartsWith("Assets/item.txt.meta:"));
        Assert.Equal("OriginalApplyConflict", (await Assert.ThrowsAsync<WorkspaceException>(() => f.Source.ApplyOriginalAsync(f.W, "apply", plan.Digest, ct, ["Assets/item.txt"]))).Code);
        Assert.Null(f.W.OriginalApply); Assert.Equal("basis", File.ReadAllText(Path.Combine(f.Project, "Assets", "item.txt")));
        Directory.CreateDirectory(Path.Combine(f.Project, "Temp"));
        using var editor = new FileStream(Path.Combine(f.Project, "Temp", "UnityLockfile"), FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        Assert.Equal("PlasticOriginalUnavailable", (await Assert.ThrowsAsync<WorkspaceException>(() => f.Source.PlanOriginalAsync(f.W, ct))).Code);
    }
    [Fact]
    public async Task RecordedBasisPreservesOriginalNtfsIndexingPolicyWithoutWeakeningOtherMetadata()
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        foreach (string path in new[] { "Assets/item.txt", "Assets/item.txt.meta" })
        { string file = Path.Combine(f.Project, path.Replace('/', Path.DirectorySeparatorChar)); File.SetAttributes(file, File.GetAttributes(file) | FileAttributes.NotContentIndexed); }
        await f.Prepare(ct); f.Write("Assets/item.txt", "changed"); await f.Collect(ct);
        var plan = await f.Source.PlanOriginalAsync(f.W, ct, ["Assets/item.txt"]);
        Assert.Empty(plan.Conflicts); await f.Source.ApplyOriginalAsync(f.W, "apply", plan.Digest, ct, ["Assets/item.txt"]);
        Assert.True((File.GetAttributes(Path.Combine(f.Project, "Assets", "item.txt")) & FileAttributes.NotContentIndexed) != 0);
        Assert.True((File.GetAttributes(Path.Combine(f.Project, "Assets", "item.txt.meta")) & FileAttributes.NotContentIndexed) != 0);
    }
    [Theory]
    [InlineData("lock", "PlasticOriginalLocked")]
    [InlineData("root-lock", "PlasticOriginalLocked")]
    [InlineData("guid", "PlasticOriginalIdentityChanged")]
    [InlineData("changeset", "PlasticOriginalIdentityChanged")]
    [InlineData("unknown", "PlasticObservationUnavailable")]
    public async Task OriginalIdentityAndLocksFailClosed(string mutation, string code)
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        await f.Prepare(ct); f.Write("Assets/item.txt", "changed"); await f.Collect(ct);
        if (mutation == "lock") f.Client.Locks = ["Assets/item.txt"];
        if (mutation == "root-lock") f.Client.Locks = [""];
        if (mutation == "guid") f.Client.Guid = "22222222-2222-2222-2222-222222222222";
        if (mutation == "changeset") f.Client.Changeset = "2";
        if (mutation == "unknown") f.Client.Unknown = true;
        Assert.Equal(code, (await Assert.ThrowsAsync<WorkspaceException>(() => f.Source.PlanOriginalAsync(f.W, ct))).Code);
        Assert.Null(f.W.OriginalApply); Assert.Equal("basis", File.ReadAllText(Path.Combine(f.Project, "Assets", "item.txt")));
    }
    [Fact]
    public async Task InterruptedPlasticPublicationPinsAdminAndResumesSameSelectedIntent()
    {
        bool injected = false, pinned = false;
        using var f = new Fixture((stage, _) =>
        {
            if (stage == "preflight")
            {
                Assert.Throws<IOException>(() => File.WriteAllText(Fixture.ActiveAdmin!, "replace")); pinned = true;
            }
            if (stage == "published-before-receipt" && !injected) { injected = true; throw new WorkspaceException("ReplyLost", "injected"); }
        });
        var ct = TestContext.Current.CancellationToken; await f.Prepare(ct); f.Write("Assets/item.txt", "changed"); await f.Collect(ct);
        var plan = await f.Source.PlanOriginalAsync(f.W, ct, ["Assets/item.txt"]);
        Assert.Equal("ReplyLost", (await Assert.ThrowsAsync<WorkspaceException>(() => f.Source.ApplyOriginalAsync(f.W, "same", plan.Digest, ct, ["Assets/item.txt"]))).Code);
        Assert.True(pinned); Assert.False(f.W.OriginalApply!.Completed);
        Assert.Equal("OriginalApplyPending", Assert.Throws<WorkspaceException>(() => f.W.OriginalApply.RequireReclaimable(f.W)).Code);
        Assert.Equal("OriginalApplyRequestConflict", (await Assert.ThrowsAsync<WorkspaceException>(() => f.Source.ApplyOriginalAsync(f.W, "same", plan.Digest, ct))).Code);
        await f.Source.ApplyOriginalAsync(f.W, "same", plan.Digest, ct, ["Assets/item.txt"]);
        f.W.OriginalApply.RequireReclaimable(f.W); Assert.True(f.W.OriginalApply.Completed);
        Assert.Equal("changed", File.ReadAllText(Path.Combine(f.Project, "Assets", "item.txt")));
    }
    [Theory]
    [InlineData("Library")]
    [InlineData(".PLASTIC/plastic.workspace")]
    [InlineData("../Assets")]
    [InlineData("Assets/*")]
    public async Task UnsafeSelectionNeverPersistsIntent(string path)
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        await f.Prepare(ct); f.Write("Assets/item.txt", "changed"); await f.Collect(ct);
        Assert.Equal("OriginalApplyRequestInvalid", (await Assert.ThrowsAsync<WorkspaceException>(() => f.Source.PlanOriginalAsync(f.W, ct, [path]))).Code);
        Assert.Null(f.W.OriginalApply);
    }
    [Theory]
    [InlineData("Assets/item.txt")]
    [InlineData("Assets/item.txt.meta")]
    public async Task AssetOrMetadataOnlyDeletionCannotPublishAnIncompleteUnityPair(string deleted)
    {
        using var f = new Fixture(); var ct = TestContext.Current.CancellationToken;
        await f.Prepare(ct); File.Delete(Path.Combine(f.W.Path, deleted.Replace('/', Path.DirectorySeparatorChar))); await f.Collect(ct);
        Assert.Equal("OriginalApplyMetadataMissing", (await Assert.ThrowsAsync<WorkspaceException>(() => f.Source.PlanOriginalAsync(f.W, ct))).Code);
        Assert.Null(f.W.OriginalApply); Assert.True(File.Exists(Path.Combine(f.Project, "Assets", "item.txt.meta")));
    }
    [Fact]
    public void MachineStatusErrorsAndMalformedLocksNeverBecomeAnEmptyInventory()
    {
        const string xml = "<StatusOutput><WorkspaceStatus><Status><RepSpec><Server>server</Server><Name>repo</Name></RepSpec><Changeset>1</Changeset></Status></WorkspaceStatus><WkConfigType>Branch</WkConfigType><WkConfigName>/main</WkConfigName></StatusOutput>";
        Assert.Empty(PlasticWorkspaceClient.ParseOriginalState(xml, "").LockedPaths);
        string row = "repo|42|11111111-1111-1111-1111-111111111111|date|main|1|main|1|Retained|user|workspace|/Assets/item.txt";
        Assert.Equal("Assets/item.txt", Assert.Single(PlasticWorkspaceClient.ParseOriginalState(xml, row).LockedPaths));
        Assert.Equal("PlasticObservationUnavailable", Assert.Throws<WorkspaceException>(() => PlasticWorkspaceClient.ParseOriginalState(xml, "Error: authentication failed")).Code);
        Assert.Equal("PlasticObservationUnavailable", Assert.Throws<WorkspaceException>(() => PlasticWorkspaceClient.ParseOriginalState(xml, row.Replace("Retained", "unknown"))).Code);
    }
    [Fact]
    public void OptionalSourceSelectionKeepsExistingPlainPlanDigestAndSchema()
    {
        var p = new OriginalApplyPlan(1, new string('a', 24), "request", "basis", "result", "project", "physical", [], [], "");
        string prior = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        { p.Version, p.OwnerId, p.RequestDigest, p.Basis, p.Result, p.ProjectPath, p.ProjectIdentity, p.Changes, p.Conflicts })));
        Assert.Equal(prior, p.ComputeDigest());
        var catalog = new ContractCatalog();
        Assert.True(catalog.Validate("urn:splinter:v1:original-apply-plan", JsonSerializer.SerializeToElement(p with { Digest = prior, SourceIdentity = prior, SelectedPaths = ["Assets/item.txt"] }, ApiJson.Options)).IsValid);
        Assert.False(catalog.Validate("urn:splinter:v1:workspace-integrate-request", JsonSerializer.SerializeToElement(new { id = p.OwnerId, target = "development", paths = new[] { "Assets/item.txt" } })).IsValid);
    }

    // Disk mount and cm observation are fakes. Only conditional NTFS publication
    // and administration exclusion handles are native in these tests.
    private sealed class Fixture : IDisposable
    {
        private readonly TestDirectory directory = new();
        public static string? ActiveAdmin;
        public string Project { get; }
        public WorkspaceStore Store { get; }
        public WorkspaceRecord W { get; }
        public Client Client { get; } = new();
        public PlasticWorkspaceProvider Source { get; }
        public Fixture(Action<string, int>? boundary = null, ApplicationProjectPolicies? policies = null)
        {
            if (!OperatingSystem.IsWindows()) Assert.Skip("Conditional Windows NTFS publication.");
            Project = Path.Combine(directory.Path, "project"); Directory.CreateDirectory(Path.Combine(Project, ".plastic"));
            foreach (string name in new[] { "plastic.workspace", "plastic.selector", "plastic.wktree" }) File.WriteAllText(Path.Combine(Project, ".plastic", name), "fixed-" + name);
            ActiveAdmin = Path.Combine(Project, ".plastic", "plastic.selector"); Client.Project = Project;
            foreach (var entry in Client.Payloads) { string path = Path.Combine(Project, entry.Key.Replace('/', Path.DirectorySeparatorChar)); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllBytes(path, entry.Value); }
            Store = new(Path.Combine(directory.Path, "home")); string id = WorkspaceStore.Key("plastic-apply");
            W = new() { Id = id, RequestKey = "apply", ProjectPath = Project, Path = Store.WorkPath(id), SourceReference = "files:" + id,
                BaseReference = "1@repo@server", SourceKind = "plastic", ApplicationKind = "unity", ManagedPaths = ["Library", "Temp"], StorageMode = "plastic-snapshot", RequestDigest = "fixed", Snapshot = new() };
            Source = new(Store, Client, Client, boundary is null ? new WindowsOriginalFilePublisher(true) : new WindowsOriginalFilePublisher(boundary, true),
                applicationPolicies: policies ?? UnityProjectPolicy.Policies);
        }
        public async Task Prepare(CancellationToken ct)
        {
            string control = Store.ControlPath(W.Id); W.BaseRevision = await Source.PreflightAsync(W, ct);
            var provider = new PlasticRevisionSnapshotProvider(Path.Combine(control, "snapshot"), W.Id, W.BaseReference, Client);
            W.Snapshot!.Reference = await provider.CaptureAsync(new(Project, W.Id, Path.Combine(control, "snapshot")), ct);
            W.Snapshot.Observation = new(true, true, true, true, 0, 0, 0) { SnapshotStorage = await provider.ObserveAsync(W.Snapshot.Reference, ct) };
            await Source.RegisterAsync(W, control, ct); W.Snapshot.Mount = new(W.Id, W.Path, Path.Combine(control, "delta"), W.Snapshot.Reference, 1, 1);
            foreach (var entry in Client.Payloads) Write(entry.Key, Encoding.UTF8.GetString(entry.Value));
            await Source.AttachAsync(W, control, ct); W.BaseRevision = await Source.EstablishBaselineAsync(W, control, ct);
        }
        public void Write(string path, string content) { string target = Path.Combine(W.Path, path.Replace('/', Path.DirectorySeparatorChar)); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.WriteAllText(target, content); }
        public async Task Collect(CancellationToken ct)
        {
            W.ResultRevision = await Source.CheckpointAsync(W, "result", ct); string reference = await Source.PreserveAsync(W, Store.ControlPath(W.Id), ct);
            W.Snapshot!.Result = new(W.Snapshot.Reference!.Id, W.ResultRevision, W.ResultRevision, reference, new(0, 0, "seal"), true, SnapshotResult.StableJournalProtocol);
        }
        public void Dispose() => directory.Dispose();
    }
    private sealed class Client : IPlasticWorkspaceClient, IPlasticRevisionClient, IPlasticOriginalStateClient
    {
        public string Project = "", Guid = "11111111-1111-1111-1111-111111111111", Changeset = "1";
        public string[] Locks = []; public bool Unknown;
        private readonly long timestamp = DateTime.UtcNow.ToFileTimeUtc();
        public Dictionary<string, byte[]> Payloads { get; } = new() { ["Assets/item.txt"] = Encoding.UTF8.GetBytes("basis"), ["Assets/item.txt.meta"] = Encoding.UTF8.GetBytes("guid"),
            ["Packages/manifest.json"] = Encoding.UTF8.GetBytes("{}"), ["ProjectSettings/ProjectVersion.txt"] = Encoding.UTF8.GetBytes("version") };
        public Task<string> VersionAsync(CancellationToken ct = default) => Task.FromResult("11.0.16.9915");
        public Task<PlasticWorkspaceIdentity> InspectAsync(string path, CancellationToken ct = default) => Task.FromResult(new PlasticWorkspaceIdentity(Project, Guid, "regular", "static"));
        public Task<PlasticWorkspaceLocation[]> ListAsync(CancellationToken ct = default) => Task.FromResult(new[] { new PlasticWorkspaceLocation(Project, Guid) });
        public Task<PlasticOriginalState> OriginalStateAsync(string project, string repository, CancellationToken ct = default) => Unknown ? throw new WorkspaceException("PlasticObservationUnavailable", "injected") : Task.FromResult(new PlasticOriginalState("repo", "server", Changeset, "Branch:/main", Locks));
        public Task<PlasticRevisionManifest> ListAsync(string treeSpec, CancellationToken ct = default) => Task.FromResult(new PlasticRevisionManifest(treeSpec, "42", "1", Guid,
            new[] { "Assets", "Packages", "ProjectSettings" }.Select(p => new PlasticRevisionEntry(p, true, "1", 0, Convert.ToBase64String(new byte[16]), timestamp)).Concat(
                Payloads.Select((p, i) => new PlasticRevisionEntry(p.Key, false, (i + 2).ToString(), p.Value.Length, Convert.ToBase64String(MD5.HashData(p.Value)), timestamp))).ToArray(), "rep:repo@server"));
        public Task VerifyProjectAsync(string project, PlasticRevisionManifest manifest, CancellationToken ct = default) => Task.CompletedTask;
        public Stream OpenRevision(string treeSpec, string revision) => new MemoryStream(Payloads.ElementAt(int.Parse(revision) - 2).Value, false);
    }
}
