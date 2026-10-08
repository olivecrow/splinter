using Splinter.Core;

namespace Splinter.Tests;

public sealed partial class SourceFingerprintTests
{
    [Fact]
    public async Task OperationProofDoesNotReadSourcePayloadOrRepublishItsManifest()
    {
        using var f = await Fixture.Create(); var ct = TestContext.Current.CancellationToken;
        var inspected = await f.Provider().InspectWithProofAsync(f.Workspace, ct);
        Assert.NotNull(inspected.Proof);
        byte[] manifest = File.ReadAllBytes(f.Tracker.CachePath(f.Workspace));
        string metricsPath = System.IO.Path.Combine(f.Directory.Path, "source-inspections.jsonl");
        byte[] metrics = File.ReadAllBytes(metricsPath);
        using var lockedPayload = File.Open(f.Path("large.bin"), FileMode.Open, FileAccess.Read, FileShare.None);
        var verified = await inspected.Proof.RevalidateAsync(f.Workspace, ct);
        Assert.Equal(inspected.Source, verified);
        Assert.Equal(manifest, File.ReadAllBytes(f.Tracker.CachePath(f.Workspace)));
        Assert.Equal(metrics, File.ReadAllBytes(metricsPath));
        Assert.Equal("SourceChanged", (await Assert.ThrowsAsync<WorkspaceException>(() => inspected.Proof.RevalidateAsync(f.Workspace, ct))).Code);
    }

    [Theory]
    [InlineData("same-size-write")]
    [InlineData("provider-restart")]
    [InlineData("head")]
    [InlineData("index")]
    [InlineData("membership")]
    [InlineData("interpretation")]
    [InlineData("policy")]
    [InlineData("workspace")]
    public async Task OperationProofRejectsChangedContentOrInterpretation(string change)
    {
        using var f = await Fixture.Create(); var ct = TestContext.Current.CancellationToken;
        var inspected = await f.Provider().InspectWithProofAsync(f.Workspace, ct);
        byte[] manifest = File.ReadAllBytes(f.Tracker.CachePath(f.Workspace));
        switch (change)
        {
            case "same-size-write":
                var time = File.GetLastWriteTimeUtc(f.Path("code.cs"));
                File.WriteAllText(f.Path("code.cs"), "return 2;\n");
                File.SetLastWriteTimeUtc(f.Path("code.cs"), time); f.Tracker.Mark("code.cs"); break;
            case "provider-restart": f.Tracker.Epoch = "new-provider"; break;
            case "head": await TestDirectory.Git(f.Workspace.Path, "commit", "--allow-empty", "-m", "changed"); break;
            case "index":
                File.WriteAllText(f.Path("code.cs"), "return 2;\n");
                await TestDirectory.Git(f.Workspace.Path, "add", "code.cs"); break;
            case "membership": File.WriteAllText(f.Path("new.cs"), "new"); break;
            case "interpretation": await TestDirectory.Git(f.Workspace.Path, "config", "core.autocrlf", "true"); break;
            case "policy": f.Workspace.ManagedPaths = ["Library", "large.bin"]; break;
            case "workspace": f.Workspace.Id = "different-workspace"; break;
        }
        Assert.Equal("SourceChanged", (await Assert.ThrowsAsync<WorkspaceException>(() => inspected.Proof!.RevalidateAsync(f.Workspace, ct))).Code);
        Assert.Equal(manifest, File.ReadAllBytes(f.Tracker.CachePath(f.Workspace)));
    }

    [Fact]
    public async Task AuditDoesNotOfferAReusableOperationProof()
    {
        using var f = await Fixture.Create();
        Assert.Null((await f.Provider(audit: true).InspectWithProofAsync(f.Workspace, TestContext.Current.CancellationToken)).Proof);
    }
}
