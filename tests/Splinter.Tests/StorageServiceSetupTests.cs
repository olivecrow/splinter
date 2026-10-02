using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Splinter.Tests;

public sealed class StorageServiceSetupTests
{
    [Theory]
    [InlineData("valid", null)]
    [InlineData("missing-library", "StoragePackageInvalid")]
    [InlineData("duplicate-file", "StoragePackageInvalid")]
    [InlineData("duplicate-grant", "StorageGrantDuplicate")]
    [InlineData("unknown-field", "StorageSetupInputInvalid")]
    [InlineData("missing-identity", "StorageSetupInputInvalid")]
    [InlineData("duplicate-property", "StorageSetupInputInvalid")]
    [InlineData("noncanonical-home", "StorageGrantInvalid")]
    [InlineData("invalid-version-type", "StorageServiceVersionMismatch")]
    public async Task SetupRejectsInvalidInputsBeforeRequestingAnAdministrator(string scenario, string? errorCode)
    {
        if (!OperatingSystem.IsWindows()) { Assert.Skip("Windows storage service installer."); return; }
        using var d = new TestDirectory();
        string package = Path.Combine(d.Path, "package"), source = Path.Combine(d.Path, "source"), home = Path.Combine(d.Path, "home");
        foreach (string path in new[] { package, source, home }) Directory.CreateDirectory(path);
        var files = new JsonArray();
        foreach (string name in new[] { "service-version.json", "Splinter.Core.dll", "Splinter.Runtime.dll", "Splinter.SourceControl.Files.dll", "Splinter.Platform.Windows.exe",
            "Splinter.Platform.Windows.dll", "Splinter.Platform.Windows.deps.json", "Splinter.Platform.Windows.runtimeconfig.json", "winfsp-msil.dll", "vss-snapshot.ps1" })
        {
            // Hash-only setup planning must never execute this fixture payload.
            string path = Path.Combine(package, name);
            File.WriteAllText(path, name == "service-version.json" ? "{\"version\":1,\"protocolVersion\":1}" : "Not executable fixture content.");
            files.Add(new JsonObject { ["name"] = name, ["sha256"] = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))) });
        }
        if (scenario == "missing-library") files.RemoveAt(1);
        if (scenario == "duplicate-file") files.Add(files[0]!.DeepClone());
        var manifest = new JsonObject { ["version"] = 1, ["protocolVersion"] = 1, ["files"] = files };
        if (scenario == "invalid-version-type") manifest["version"] = true;
        File.WriteAllText(Path.Combine(package, "package.json"), manifest.ToJsonString());
        var grant = new JsonObject { ["userSid"] = "S-1-5-21-111-222-333-1001", ["sourcePath"] = source, ["homePath"] = home,
            ["sourceVolume"] = @"\\?\Volume{12345678-1234-1234-1234-123456789abc}\", ["sourceIdentity"] = "12345678:0000000000000001", ["homeIdentity"] = "12345678:0000000000000002" };
        if (scenario == "unknown-field") grant["command"] = "unaccepted";
        if (scenario == "missing-identity") grant.Remove("sourceIdentity");
        if (scenario == "noncanonical-home") grant["homePath"] = Path.Combine(home, "..", "home");
        var grants = new JsonArray(grant);
        if (scenario == "duplicate-grant") grants.Add(grant.DeepClone());
        string grantJson = grants.ToJsonString();
        if (scenario == "duplicate-property") grantJson = grantJson.Insert(2, "\"userSid\":\"another\",");
        string grantPath = Path.Combine(d.Path, "grants.json"); File.WriteAllText(grantPath, grantJson);
        var before = Directory.GetFiles(d.Path, "*", SearchOption.AllDirectories).ToDictionary(p => p, p => File.ReadAllBytes(p));
        var start = new ProcessStartInfo("pwsh") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in new[] { "-NoProfile", "-NonInteractive", "-File", Path.Combine(AppContext.BaseDirectory, "Scripts", "setup-storage-service.ps1"),
            "-PackagePath", package, "-GrantFile", grantPath }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken); timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var output = process.StandardOutput.ReadToEndAsync(timeout.Token); var error = process.StandardError.ReadToEndAsync(timeout.Token);
        try { await process.WaitForExitAsync(timeout.Token); }
        finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(CancellationToken.None); } }
        string stdout = await output, stderr = await error;
        if (errorCode is null)
        {
            Assert.True(process.ExitCode == 0, stderr);
            using var plan = JsonDocument.Parse(stdout);
            Assert.False(plan.RootElement.GetProperty("apply").GetBoolean());
            Assert.Single(plan.RootElement.GetProperty("grants").EnumerateArray());
        }
        else { Assert.NotEqual(0, process.ExitCode); Assert.Contains(errorCode, stderr); Assert.Empty(stdout); }
        Assert.Equal(before.Count, Directory.GetFiles(d.Path, "*", SearchOption.AllDirectories).Length);
        foreach (var (path, bytes) in before) Assert.Equal(bytes, File.ReadAllBytes(path));
    }
}
