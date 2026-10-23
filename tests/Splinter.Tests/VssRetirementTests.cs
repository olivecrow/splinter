using System.Text.Json;
using Splinter.Core;
using Splinter.Platform.Windows;
using Splinter.Protocol;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed partial class SnapshotWorkspaceCoordinatorTests
{
    [Theory]
    [InlineData("git")]
    [InlineData("plastic")]
    [InlineData("none")]
    public async Task RetiredStorageCannotCreateOrResumeAndLeavesLegacyEvidenceUnchanged(string source)
    {
        using var f = new Fixture { WithDiscard = true }; var ct = TestContext.Current.CancellationToken;
        var store = new WorkspaceStore(f.Home);
        var error = await Assert.ThrowsAsync<WorkspaceException>(() => f.New().EnsureAsync(f.Project, "legacy", "snapshot",
            "ntfs-snapshot", false, ct, sourceKind: source));
        Assert.Equal("StorageModeRetired", error.Code);
        Assert.Empty(store.List(f.Project)); Assert.Equal(0, f.Backend.Captures);

        string id = WorkspaceStore.Key(WorkspaceStore.ProjectKey(f.Project) + "\nlegacy");
        var legacy = new WorkspaceRecord { Id = id, RequestKey = "legacy", ProjectPath = f.Project, Path = store.WorkPath(id),
            BaseReference = "snapshot", SourceReference = "legacy", SourceKind = source, StorageMode = "ntfs-snapshot",
            Phase = "reclaim-pending", RequestDigest = new string('a', 24), ResultRevision = "retained-independent-result",
            Snapshot = new() { Reference = new("legacy-snapshot", "windows-vss-system", f.Project, "old-location", DateTimeOffset.UtcNow, true, true, "filesystem-crash-consistent-no-application-writer") } };
        store.Save(legacy, "legacy-fixture");
        string control = store.ControlPath(id);
        foreach (string name in new[] { "independent-result.bin", "protected-ledger.json", "delta.journal", "snapshot.json" })
            File.WriteAllText(Path.Combine(control, name), "preserve:" + name);
        var before = Directory.GetFiles(control).ToDictionary(path => Path.GetFileName(path)!, File.ReadAllBytes);

        Assert.Equal("StorageModeRetired", Assert.Throws<WorkspaceException>(() => store.Get(id)).Code);
        Assert.Equal("StorageModeRetired", Assert.Throws<WorkspaceException>(() => store.List(f.Project)).Code);
        foreach (var action in new Func<Task>[] {
            async () => { await f.New().StatusAsync(id, ct); }, async () => { await f.New().StartAsync(id, ct); },
            async () => { await f.New().CheckpointAsync(id, "blocked", ct); }, async () => { await f.New().CompactAsync(id, ct); },
            async () => { await f.New().ReleaseAsync(id, ct); },
            async () => { await f.New().DiscardAsync(new(id, "blocked", legacy.RequestDigest!), ct); } })
            Assert.Equal("StorageModeRetired", (await Assert.ThrowsAsync<WorkspaceException>(action)).Code);
        Assert.Equal(before.Keys.Order(), Directory.GetFiles(control).Select(Path.GetFileName).Order());
        foreach (var (name, bytes) in before) Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(control, name!)));
        Assert.Equal(0, f.App.Starts); Assert.Equal(0, f.App.Stops); Assert.Equal(0, f.Backend.Captures);
        Assert.False(f.Backend.DeltaRemoved); Assert.Equal(0, f.Backend.ReleaseAttempts);
        Assert.Equal("requires-user-action", WorkspaceFailurePolicy.RetryDisposition(error.Code));
    }
}

public sealed class VssRetirementTests
{
    [Fact]
    public async Task CliRejectsRetiredModeBeforeCreatingAHomeOrInspectingTheSource()
    {
        using var d = new TestDirectory(); using var output = new StringWriter();
        string home = Path.Combine(d.Path, "unused-home"), project = Path.Combine(d.Path, "absent-project");
        Assert.NotEqual(0, await Splinter.Cli.Program.RunAsync(["workspace", "ensure", "--project", project,
            "--key", "legacy", "--storage", "ntfs-snapshot", "--home", home], output, TestContext.Current.CancellationToken));
        Assert.Contains("StorageModeRetired", output.ToString());
        Assert.Empty(Directory.GetDirectories(d.Path)); Assert.Empty(Directory.GetFiles(d.Path));
    }

    [Fact]
    public async Task LegacyOverlayIsRefusedBeforeLaunchAndReceiptsArePreserved()
    {
        using var d = new TestDirectory();
        var snapshot = new SnapshotReference("legacy", "windows-vss-system", d.Path, "old-location", DateTimeOffset.UtcNow, true, true, "filesystem-crash-consistent-no-application-writer");
        var request = new OverlayServeRequest(new(2, "legacy", snapshot, 65536, 4096), Path.Combine(d.Path, "delta"),
            Path.Combine(d.Path, "mount"), Path.Combine(d.Path, "control"), "owner", new(snapshot.Id, snapshot.RootPath, "old-proof"), d.Path, "owner");
        string path = Path.Combine(d.Path, "request.json");
        File.WriteAllText(path, JsonSerializer.Serialize(request, ApiJson.Options)); byte[] before = File.ReadAllBytes(path);
        Assert.Equal("SnapshotProviderUnsupported", (await Assert.ThrowsAsync<WorkspaceException>(() => WinFspOverlayClient.ReadRequestAsync(path, TestContext.Current.CancellationToken))).Code);
        Assert.Equal("SnapshotProviderUnsupported", Assert.Throws<WorkspaceException>(() => WorkspaceSnapshotBasis.Provider(snapshot, d.Path, "owner")).Code);
        Assert.Equal("SnapshotProviderUnsupported", Assert.Throws<WorkspaceException>(() => WorkspaceSnapshotBasis.AcquireReadLease(snapshot, d.Path, "owner")).Code);
        Assert.Equal(before, File.ReadAllBytes(path)); Assert.Single(Directory.GetFiles(d.Path)); Assert.Empty(Directory.GetDirectories(d.Path));
        Assert.False(new ContractCatalog().Validate("urn:splinter:v1:overlay-serve-request", JsonSerializer.SerializeToElement(request, ApiJson.Options)).IsValid);
    }

    [Fact]
    public async Task DiscoveryAndBothRequestCatalogsExposeOnlyCurrentProductModes()
    {
        foreach (bool development in new[] { false, true })
        {
            var catalog = new ContractCatalog(development);
            Assert.Null(catalog.Find("storage.service.status"));
            Assert.DoesNotContain(catalog.SchemaIds, id => id.Contains("storage-authority") || id.Contains("storage-service"));
            string schema = development ? "workspace-development-ensure-request" : "workspace-ensure-request";
            Assert.False(catalog.Validate("urn:splinter:v1:" + schema,
                JsonSerializer.SerializeToElement(new { project = "p", key = "k", storage = "ntfs-snapshot" })).IsValid);
            foreach (string mode in new[] { "git-snapshot", "plastic-snapshot", "project-snapshot" })
                Assert.True(catalog.Validate("urn:splinter:v1:" + schema, JsonSerializer.SerializeToElement(new {
                    project = "p", key = "k", storage = mode, source = mode == "git-snapshot" ? "git" : mode == "plastic-snapshot" ? "plastic" : "none",
                    baseReference = mode == "git-snapshot" ? "HEAD" : mode == "plastic-snapshot" ? "1@repo@server" : "snapshot",
                    projectBasis = mode == "project-snapshot" ? new string('a', 24) : null })).IsValid);
        }
        Assert.Equal(2, await Splinter.Platform.Windows.Program.Main(["storage-service"]));
        Assert.Equal(2, await Splinter.Platform.Windows.Program.Main(["storage-grant", "source", "home", "sid"]));
        var assembly = typeof(WorkspaceSnapshotBasis).Assembly;
        Assert.DoesNotContain(assembly.GetTypes(), type => type.Name.StartsWith("Vss") || type.Name.StartsWith("StorageAuthority"));
        Assert.DoesNotContain(assembly.GetManifestResourceNames(), name => name.Contains("Vss", StringComparison.OrdinalIgnoreCase));
    }
}
