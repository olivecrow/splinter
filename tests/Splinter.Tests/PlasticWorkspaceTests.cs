using System.Text.Json;
using Splinter.Core;
using Splinter.Runtime;
using Splinter.SourceControl.Files;

namespace Splinter.Tests;

public sealed partial class FileWorkspaceTests
{
    [Fact]
    public async Task PlasticWorkersKeepCapturedBytesAndIndependentResultsWithoutOriginalAdministration()
    {
        using var f = new Fixture(sourceKind: "plastic"); var ct = TestContext.Current.CancellationToken;
        string admin = File.ReadAllText(Path.Combine(f.Project, ".plastic", "plastic.workspace"));
        var a = (await f.Ensure("a", ct)).Workspace; var b = (await f.Ensure("b", ct)).Workspace;
        Assert.False(Directory.Exists(Path.Combine(a.Path, ".plastic")));
        Assert.False(Directory.Exists(Path.Combine(f.Store.ControlPath(a.Id), "files", "objects")));
        File.WriteAllText(Path.Combine(f.Project, "data.txt"), "later original");
        File.WriteAllText(Path.Combine(a.Path, "data.txt"), "A result");
        Assert.Equal("basis", File.ReadAllText(Path.Combine(b.Path, "data.txt")));
        Assert.Equal(a.BaseRevision, (await f.Ensure("a", ct)).Workspace.BaseRevision);
        var kept = await f.New().CheckpointAsync(a.Id, "preserve", ct);
        var catalog = new Splinter.Protocol.ContractCatalog();
        Assert.True(catalog.Validate("urn:splinter:v1:response", JsonSerializer.SerializeToElement(Splinter.Cli.WorkspaceCommands.Result("workspace.checkpoint", kept, false, f.Store.Root), Splinter.Protocol.ApiJson.Options)).IsValid);
        foreach (string kind in new[] { "admission", "snapshot" })
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(f.Store.ControlPath(a.Id), "plastic", kind + ".json")));
            var validation = catalog.Validate(kind == "admission" ? "urn:splinter:v1:plastic-source-admission" : "urn:splinter:v1:plastic-source-receipt", doc.RootElement);
            Assert.True(validation.IsValid, kind);
        }
        string state = File.ReadAllText(Path.Combine(f.Store.ControlPath(a.Id), "workspace.json"));
        Assert.Equal("SourceIntegrationUnsupported", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().IntegrateAsync(a.Id, "original", ct))).Code);
        Assert.Equal(state, File.ReadAllText(Path.Combine(f.Store.ControlPath(a.Id), "workspace.json")));
        await f.New().ReleaseAsync(a.Id, ct);
        Assert.True(Directory.Exists(b.Path)); Assert.False(Directory.Exists(a.Path));
        Assert.Equal("later original", File.ReadAllText(Path.Combine(f.Project, "data.txt")));
        FileWorkspaceProvider.VerifyIndependentResult(f.Store.Get(a.Id), f.Store.ControlPath(a.Id));
        File.WriteAllText(Path.Combine(b.Path, "data.txt"), "dirty B");
        Assert.Equal("discarded", (await f.New().DiscardAsync(new(b.Id, "explicit", b.RequestDigest!), ct)).Workspace.Phase);
        Assert.Equal(admin, File.ReadAllText(Path.Combine(f.Project, ".plastic", "plastic.workspace")));
        f.Plastic.Unavailable = true;
        Directory.Move(f.Project, f.Project + "-unavailable");
        FileWorkspaceProvider.VerifyIndependentResult(f.Store.Get(a.Id), f.Store.ControlPath(a.Id));
        Assert.Equal(0, f.Git.Calls);
    }

    [Theory]
    [InlineData("partial")]
    [InlineData("dynamic")]
    [InlineData("ancestor")]
    [InlineData("home-registration")]
    [InlineData("home-administration")]
    [InlineData("nested-vcs")]
    [InlineData("unknown")]
    public async Task UnsupportedPlasticEnvironmentFailsBeforeCaptureWithoutFallback(string condition)
    {
        using var f = new Fixture(sourceKind: "plastic");
        if (condition == "partial") f.Plastic.Type = "partial";
        if (condition == "dynamic") f.Plastic.Dynamic = "dynamic";
        if (condition == "ancestor") f.Plastic.RootOverride = Path.GetDirectoryName(f.Project);
        if (condition == "home-registration") f.Plastic.AdditionalRoot = f.Store.Root;
        if (condition == "home-administration") Directory.CreateDirectory(Path.Combine(f.Store.Root, ".git"));
        if (condition == "nested-vcs") Directory.CreateDirectory(Path.Combine(f.Project, "Library", ".plastic"));
        if (condition == "unknown") f.Plastic.Unavailable = true;
        await Assert.ThrowsAsync<WorkspaceException>(() => f.Ensure("rejected", TestContext.Current.CancellationToken));
        Assert.Equal(0, f.Backend.Captures); Assert.Equal(0, f.Git.Calls);
        Assert.True(File.Exists(Path.Combine(f.Project, ".plastic", "plastic.workspace")));
    }

    [Fact]
    public async Task PlasticReceiptTamperingAndWorkerAdministrationBlockCollectionAndKeepOwnedResources()
    {
        using var f = new Fixture(sourceKind: "plastic"); var ct = TestContext.Current.CancellationToken;
        var a = (await f.Ensure("a", ct)).Workspace;
        Directory.CreateDirectory(Path.Combine(a.Path, ".plastic"));
        Assert.Equal("PlasticCompatibilityInvalid", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().CheckpointAsync(a.Id, "blocked", ct))).Code);
        Directory.Delete(Path.Combine(a.Path, ".plastic"));
        string receipt = Path.Combine(f.Store.ControlPath(a.Id), "plastic", "snapshot.json");
        File.WriteAllText(receipt, File.ReadAllText(receipt).Replace(f.Plastic.Guid, System.Guid.NewGuid().ToString("D"), StringComparison.Ordinal));
        Assert.Equal("PlasticCompatibilityInvalid", (await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ReleaseAsync(a.Id, ct))).Code);
        Assert.True(Directory.Exists(a.Path)); Assert.Empty(f.Backend.Released);
    }

    [Theory]
    [InlineData("capture")]
    [InlineData("connect")]
    [InlineData("unmount")]
    [InlineData("release")]
    public async Task PlasticLostResponseReusesTheSameAdmissionBasisAndOwnedResources(string boundary)
    {
        using var f = new Fixture(sourceKind: "plastic"); var ct = TestContext.Current.CancellationToken;
        if (boundary is "capture" or "connect") { f.Backend.Fail = boundary; await Assert.ThrowsAsync<WorkspaceException>(() => f.Ensure("a", ct)); }
        var a = (await f.Ensure("a", ct)).Workspace;
        if (boundary is "unmount" or "release") { f.Backend.Fail = boundary; await Assert.ThrowsAsync<WorkspaceException>(() => f.New().ReleaseAsync(a.Id, ct)); }
        Assert.Equal("released", (await f.New().ReleaseAsync(a.Id, ct)).Workspace.Phase);
        Assert.Equal(1, f.Backend.Captures); Assert.Equal(0, f.Git.Calls);
        FileWorkspaceProvider.VerifyIndependentResult(f.Store.Get(a.Id), f.Store.ControlPath(a.Id));
    }
}
