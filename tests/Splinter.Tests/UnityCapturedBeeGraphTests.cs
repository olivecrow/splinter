using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Splinter.Application.Unity;
using Splinter.Core;

namespace Splinter.Tests;

public sealed class UnityCapturedBeeGraphTests
{
    [Fact]
    public void FirstLaunchInvalidatesOnlyTopLevelCompiledGraphsAndIsIdempotent()
    {
        using var fixture = new TestDirectory(); var workspace = Project(fixture.Path);
        string[] removed = ["Library/Bee/1900b0aE.dag", "Library/Bee/another.DAG"];
        string[] retained = ["Library/Bee/TundraBuildState.state", "Library/Bee/bee_backend.info",
            "Library/Bee/1900b0aE.dag.json", "Library/Bee/1900b0aE.dag.payloads",
            "Library/Bee/1900b0aE.dag_derived", "Library/Bee/1900b0aE.dag_fsmtime",
            "Library/Bee/artifacts/rsp/request.rsp", "Library/Bee/artifacts/nested.dag", "Library/other.dag"];
        string peer = Path.Combine(fixture.Path, "peer");
        foreach (string root in new[] { workspace.Path, workspace.ProjectPath, peer })
            foreach (string relative in removed.Concat(retained)) Put(root, relative);
        var originals = Inventory(workspace.ProjectPath); var peers = Inventory(peer);
        var protectedFiles = retained.Select(p => Describe(Path.Combine(workspace.Path, p))).ToArray();

        UnityWorkspaceProvider.InvalidateCapturedBeeGraphForFirstLaunch(workspace, null, TestContext.Current.CancellationToken);
        UnityWorkspaceProvider.InvalidateCapturedBeeGraphForFirstLaunch(workspace, null, TestContext.Current.CancellationToken);

        foreach (string relative in removed) Assert.False(File.Exists(Path.Combine(workspace.Path, relative)));
        Assert.Equal(protectedFiles, retained.Select(p => Describe(Path.Combine(workspace.Path, p))).ToArray());
        Assert.Equal(originals, Inventory(workspace.ProjectPath)); Assert.Equal(peers, Inventory(peer));
    }

    [Theory]
    [InlineData("checkout")]
    [InlineData("no-reference")]
    [InlineData("whole-project-basis")]
    [InlineData("other-generated-directory")]
    [InlineData("prior-receipt")]
    public void UnqualifiedWorkspacesAndRestartsKeepTheirCompiledGraph(string scenario)
    {
        using var fixture = new TestDirectory(); var workspace = Project(fixture.Path);
        Put(workspace.Path, "Library/Bee/captured.dag");
        ApplicationLease? prior = null;
        switch (scenario)
        {
            case "checkout": workspace.StorageMode = "git-checkout"; break;
            case "no-reference": workspace.GeneratedDirectory = null; break;
            case "whole-project-basis": workspace.StorageMode = "project-snapshot"; workspace.GeneratedDirectory = workspace.GeneratedDirectory! with { RelativePath = "" }; break;
            case "other-generated-directory": workspace.GeneratedDirectory = workspace.GeneratedDirectory! with { RelativePath = "Cache" }; break;
            case "prior-receipt": prior = new(-1, 1, "previous-launch", "owned-editor"); break;
        }
        var before = Inventory(workspace.Path);
        UnityWorkspaceProvider.InvalidateCapturedBeeGraphForFirstLaunch(workspace, prior, TestContext.Current.CancellationToken);
        Assert.Equal(before, Inventory(workspace.Path));
    }

    [Theory]
    [InlineData("id")]
    [InlineData("source")]
    [InlineData("original-target")]
    public void MismatchedLibraryOrOriginalTargetIsRejectedBeforeDeletion(string scenario)
    {
        using var fixture = new TestDirectory(); var workspace = Project(fixture.Path);
        Put(workspace.Path, "Library/Bee/captured.dag"); Put(workspace.ProjectPath, "Library/Bee/captured.dag");
        switch (scenario)
        {
            case "id": workspace.GeneratedDirectoryId = "different"; break;
            case "source": workspace.GeneratedDirectory = workspace.GeneratedDirectory! with { ProjectPath = Path.Combine(fixture.Path, "another-source") }; break;
            case "original-target": workspace.Path = workspace.ProjectPath; break;
        }
        var before = Inventory(fixture.Path);
        Assert.Equal("GeneratedBasisMismatch", Assert.Throws<WorkspaceException>(() =>
            UnityWorkspaceProvider.InvalidateCapturedBeeGraphForFirstLaunch(workspace, null, TestContext.Current.CancellationToken)).Code);
        Assert.Equal(before, Inventory(fixture.Path));
    }

    [Fact]
    public void DirectoryNamedLikeGraphBlocksAllGraphDeletion()
    {
        using var fixture = new TestDirectory(); var workspace = Project(fixture.Path);
        Put(workspace.Path, "Library/Bee/a.dag"); Put(workspace.Path, "Library/Bee/z.dag/retained.txt");
        var before = Inventory(workspace.Path);
        Assert.Equal("UnsafePath", Assert.Throws<WorkspaceException>(() =>
            UnityWorkspaceProvider.InvalidateCapturedBeeGraphForFirstLaunch(workspace, null, TestContext.Current.CancellationToken)).Code);
        Assert.Equal(before, Inventory(workspace.Path));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReparseGraphOrBeeParentBlocksDeletionAndPreservesTarget(bool parent)
    {
        using var fixture = new TestDirectory(); var workspace = Project(fixture.Path);
        string bee = Path.Combine(workspace.Path, "Library", "Bee"), target = Path.Combine(fixture.Path, "foreign");
        Put(workspace.Path, "Library/Bee/a.dag"); Put(target, "foreign.dag");
        string link = parent ? bee : Path.Combine(bee, "z.dag");
        if (parent) Directory.Move(bee, Path.Combine(fixture.Path, "retained-bee"));
        bool linked = false;
        try
        {
            await Link(link, target); linked = true;
            var before = Inventory(target);
            Assert.Equal("UnsafePath", Assert.Throws<WorkspaceException>(() =>
                UnityWorkspaceProvider.InvalidateCapturedBeeGraphForFirstLaunch(workspace, null, TestContext.Current.CancellationToken)).Code);
            Assert.Equal(before, Inventory(target));
            Assert.True(File.Exists(parent ? Path.Combine(fixture.Path, "retained-bee", "a.dag") : Path.Combine(bee, "a.dag")));
        }
        finally { if (linked) Directory.Delete(link, recursive: false); }
    }

    [Fact]
    public void InterruptedDeletionPrefixCanResumeWithoutDiscardingOtherCacheFiles()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Skip("Windows sharing failure exercises interruption after one metadata deletion."); return; }
        using var fixture = new TestDirectory(); var workspace = Project(fixture.Path);
        string first = Put(workspace.Path, "Library/Bee/a.dag"), last = Put(workspace.Path, "Library/Bee/z.dag");
        string state = Put(workspace.Path, "Library/Bee/TundraBuildState.state"); var beforeState = Describe(state);
        using (var hold = new FileStream(last, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.ThrowsAny<IOException>(() => UnityWorkspaceProvider.InvalidateCapturedBeeGraphForFirstLaunch(workspace, null, TestContext.Current.CancellationToken));
            Assert.False(File.Exists(first)); Assert.True(File.Exists(last)); Assert.Equal(beforeState, Describe(state));
        }
        UnityWorkspaceProvider.InvalidateCapturedBeeGraphForFirstLaunch(workspace, null, TestContext.Current.CancellationToken);
        Assert.False(File.Exists(last)); Assert.Equal(beforeState, Describe(state));
    }

    [Fact]
    public async Task UncertainLaunchIntentRefusesBeforeInvalidatingGraphs()
    {
        using var fixture = new TestDirectory(); var workspace = Project(fixture.Path);
        Put(workspace.Path, "Library/Bee/captured.dag");
        string control = Path.Combine(fixture.Path, "control"); Put(control, "launch-intent.json");
        var before = Inventory(workspace.Path);
        Assert.Equal("LaunchUncertain", (await Assert.ThrowsAsync<WorkspaceException>(() =>
            new UnityWorkspaceProvider().StartAsync(workspace, control, TestContext.Current.CancellationToken))).Code);
        Assert.Equal(before, Inventory(workspace.Path));
    }

    [Fact]
    public async Task LiveOwnedLaunchReceiptReturnsWithoutInvalidatingGraphs()
    {
        using var fixture = new TestDirectory(); var workspace = Project(fixture.Path);
        Put(workspace.Path, "Library/Bee/captured.dag");
        string control = Path.Combine(fixture.Path, "control"); Directory.CreateDirectory(control);
        using var process = Process.GetCurrentProcess();
        var lease = new ApplicationLease(process.Id, process.StartTime.ToUniversalTime().Ticks, "fixture", process.MainModule!.FileName);
        await File.WriteAllTextAsync(Path.Combine(control, "launch-receipt.json"),
            JsonSerializer.Serialize(lease, new JsonSerializerOptions(JsonSerializerDefaults.Web)), TestContext.Current.CancellationToken);
        var before = Inventory(workspace.Path);
        Assert.Equal(lease, await new UnityWorkspaceProvider().StartAsync(workspace, control, TestContext.Current.CancellationToken));
        Assert.Equal(before, Inventory(workspace.Path));
    }

    private static WorkspaceRecord Project(string directory)
    {
        string worker = Path.Combine(directory, "worker"), original = Path.Combine(directory, "original");
        foreach (string root in new[] { worker, original })
        {
            Directory.CreateDirectory(Path.Combine(root, "Assets"));
            Put(root, "ProjectSettings/ProjectVersion.txt", "m_EditorVersion: 6000.3.22f1");
        }
        const string basisId = "0123456789abcdef01234567";
        var snapshot = new SnapshotReference("library-fixture", "fake-memory", "original", "fixed", DateTimeOffset.UnixEpoch, true, true, "fixture-only");
        return new() { Id = "bee-fixture", RequestKey = "bee-fixture", Path = worker, ProjectPath = original,
            BaseReference = "fixed", SourceReference = "fixed", StorageMode = "git-snapshot", GeneratedDirectoryId = basisId,
            GeneratedDirectory = new(basisId, original, "Library", snapshot, Path.Combine(directory, "receipt"), basisId, new string('a', 64)) };
    }

    private static string Put(string root, string relative, string? content = null)
    {
        string path = Path.Combine(root, relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content ?? relative); File.SetLastWriteTimeUtc(path, new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc));
        return path;
    }
    private static string Describe(string path) => File.GetLastWriteTimeUtc(path).Ticks + ":" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static string[] Inventory(string root) => Directory.GetFiles(root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal)
        .Select(p => Path.GetRelativePath(root, p) + ":" + Describe(p)).ToArray();
    private static async Task Link(string path, string target)
    {
        if (!OperatingSystem.IsWindows()) { Directory.CreateSymbolicLink(path, target); return; }
        var ct = TestContext.Current.CancellationToken;
        var start = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true, Arguments = $"/d /c mklink /J \"{path}\" \"{target}\"" };
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(ct); var error = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        Assert.True(process.ExitCode == 0, await output + await error);
    }
}
