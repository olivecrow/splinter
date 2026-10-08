using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Splinter.Application.Unity;
using Splinter.Core;

namespace Splinter.Tests;

public sealed class UnityInfrastructureReuseTests
{
    [Fact]
    public async Task SameOwnedBridgePreservesFilesAndTimestampsWithoutWriting()
    {
        using var directory = new TestDirectory();
        var workspace = await Project(directory.Path);
        var ct = TestContext.Current.CancellationToken;
        await UnityWorkspaceProvider.PrepareInfrastructureAsync(workspace, ct);
        string infrastructure = Infrastructure(workspace), bridge = Bridge(workspace), marker = Marker(workspace);
        string meta = bridge + ".meta";
        await File.WriteAllTextAsync(meta, "guid: owned-existing-meta", ct);
        string code = await File.ReadAllTextAsync(bridge, ct), receipt = await File.ReadAllTextAsync(marker, ct);
        var timestamp = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        foreach (string path in new[] { bridge, marker, meta }) File.SetLastWriteTimeUtc(path, timestamp);
        Directory.SetLastWriteTimeUtc(infrastructure, timestamp);
        Directory.SetLastWriteTimeUtc(Path.GetDirectoryName(bridge)!, timestamp);
        // Windows share modes make an accidental same-content rewrite fail,
        // independently of filesystem timestamp granularity.
        using var bridgeRead = new FileStream(bridge, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var markerRead = new FileStream(marker, FileMode.Open, FileAccess.Read, FileShare.Read);
        await UnityWorkspaceProvider.PrepareInfrastructureAsync(workspace, ct);
        Assert.Equal(code, await File.ReadAllTextAsync(bridge, ct));
        Assert.Equal(receipt, await File.ReadAllTextAsync(marker, ct));
        Assert.Equal("guid: owned-existing-meta", await File.ReadAllTextAsync(meta, ct));
        foreach (string path in new[] { bridge, marker, meta }) Assert.Equal(timestamp, File.GetLastWriteTimeUtc(path));
        Assert.Equal(timestamp, Directory.GetLastWriteTimeUtc(infrastructure));
        Assert.Equal(timestamp, Directory.GetLastWriteTimeUtc(Path.GetDirectoryName(bridge)!));
    }

    [Fact]
    public async Task VerifiedOwnedDifferentCodeIsUpdatedToEmbeddedBridge()
    {
        using var directory = new TestDirectory();
        var workspace = await Project(directory.Path);
        var ct = TestContext.Current.CancellationToken;
        await UnityWorkspaceProvider.PrepareInfrastructureAsync(workspace, ct);
        string bridge = Bridge(workspace), marker = Marker(workspace);
        byte[] expectedCode = await File.ReadAllBytesAsync(bridge, ct), expectedMarker = await File.ReadAllBytesAsync(marker, ct);
        const string oldCode = "// previously owned bridge\n";
        await File.WriteAllTextAsync(bridge, oldCode, ct);
        await WriteMarker(workspace, workspace.Id, oldCode);
        await UnityWorkspaceProvider.PrepareInfrastructureAsync(workspace, ct);
        Assert.Equal(expectedCode, await File.ReadAllBytesAsync(bridge, ct));
        Assert.Equal(expectedMarker, await File.ReadAllBytesAsync(marker, ct));
    }

    [Theory]
    [InlineData("owner", "ManagedFilesChanged")]
    [InlineData("code", "ManagedFilesChanged")]
    [InlineData("missing-bridge", "ManagedFilesChanged")]
    [InlineData("missing-marker", "ManagedPathConflict")]
    [InlineData("extra-file", "ManagedFilesChanged")]
    [InlineData("extra-directory", "ManagedFilesChanged")]
    public async Task ReuseStillRejectsChangedOwnershipOrInfrastructure(string change, string error)
    {
        using var directory = new TestDirectory();
        var workspace = await Project(directory.Path);
        var ct = TestContext.Current.CancellationToken;
        await UnityWorkspaceProvider.PrepareInfrastructureAsync(workspace, ct);
        string bridge = Bridge(workspace), marker = Marker(workspace), infrastructure = Infrastructure(workspace);
        switch (change)
        {
            case "owner": await WriteMarker(workspace, "another-owner", await File.ReadAllTextAsync(bridge, ct)); break;
            case "code": await File.AppendAllTextAsync(bridge, "// user change", ct); break;
            case "missing-bridge": File.Delete(bridge); break;
            case "missing-marker": File.Delete(marker); break;
            case "extra-file": await File.WriteAllTextAsync(Path.Combine(infrastructure, "user.txt"), "retained", ct); break;
            case "extra-directory": Directory.CreateDirectory(Path.Combine(infrastructure, "User")); break;
        }
        var before = await Files(infrastructure);
        Assert.Equal(error, (await Assert.ThrowsAsync<WorkspaceException>(() =>
            UnityWorkspaceProvider.PrepareInfrastructureAsync(workspace, ct))).Code);
        Assert.Equal(before, await Files(infrastructure));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReuseRejectsInfrastructureAndChildReparsePoints(bool child)
    {
        using var directory = new TestDirectory();
        var workspace = await Project(directory.Path);
        var ct = TestContext.Current.CancellationToken;
        await UnityWorkspaceProvider.PrepareInfrastructureAsync(workspace, ct);
        string link = child ? Path.GetDirectoryName(Bridge(workspace))! : Infrastructure(workspace);
        string target = Path.Combine(directory.Path, "retained-target");
        Directory.Move(link, target);
        bool linked = false;
        try
        {
            await Link(link, target, ct); linked = true;
            var before = await Files(target);
            Assert.Equal("UnsafePath", (await Assert.ThrowsAsync<WorkspaceException>(() =>
                UnityWorkspaceProvider.PrepareInfrastructureAsync(workspace, ct))).Code);
            Assert.Equal(before, await Files(target));
        }
        finally { if (linked) Directory.Delete(link, recursive: false); }
    }

    private static async Task<WorkspaceRecord> Project(string directory)
    {
        string root = Path.Combine(directory, "project");
        Directory.CreateDirectory(Path.Combine(root, "Assets"));
        Directory.CreateDirectory(Path.Combine(root, "ProjectSettings"));
        await File.WriteAllTextAsync(Path.Combine(root, "ProjectSettings", "ProjectVersion.txt"),
            "m_EditorVersion: 6000.3.22f1", TestContext.Current.CancellationToken);
        return new() { Id = "fixture", RequestKey = "fixture", Path = root, ProjectPath = root,
            BaseReference = "fixture", SourceReference = "fixture" };
    }
    private static string Infrastructure(WorkspaceRecord workspace) => Path.Combine(workspace.Path, "Assets", "__Splinter__");
    private static string Bridge(WorkspaceRecord workspace) => Path.Combine(Infrastructure(workspace), "Editor", "WorkspaceBridge.cs");
    private static string Marker(WorkspaceRecord workspace) => Path.Combine(Infrastructure(workspace), "workspace.marker");
    private static Task WriteMarker(WorkspaceRecord workspace, string owner, string code) =>
        File.WriteAllTextAsync(Marker(workspace), JsonSerializer.Serialize(new
        {
            workspaceId = owner, codeHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(code)))
        }), TestContext.Current.CancellationToken);
    private static async Task<string[]> Files(string root)
    {
        var result = new List<string>();
        foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            result.Add(Path.GetRelativePath(root, file) + ":" + Convert.ToHexString(await File.ReadAllBytesAsync(file, TestContext.Current.CancellationToken)));
        return result.ToArray();
    }
    private static async Task Link(string path, string target, CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows()) { Directory.CreateSymbolicLink(path, target); return; }
        var start = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true, Arguments = $"/d /c mklink /J \"{path}\" \"{target}\"" };
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(ct); var error = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        Assert.True(process.ExitCode == 0, await output + await error);
    }
}
