using System.Diagnostics;
using System.Text.Json;
using Splinter.Protocol;

namespace Splinter.Tests;

public sealed class ProductWriteProbeContractTests
{
    [Fact]
    public void CurrentRequestSchemaAllowsExactlyThirteenClientCases()
    {
        var contracts = new ContractCatalog(); int accepted = 0;
        foreach (string op in new[] { "write", "rename", "replace", "truncate", "flush", "mmap" })
            foreach (string boundary in new[] { "client.before-effect", "client.after-ack", "client.after-write-before-flush", "client.before-view-flush", "callback.after-effect" })
            {
                var input = JsonSerializer.SerializeToElement(new { version = 1, directory = "new", mountPath = "owned", operation = op, boundary,
                    nonce = new string('a', 32), alphaOracle = "alpha.bin", betaOracle = "beta.bin", workspaceId = "owned", snapshotId = "owned-snapshot",
                    providerId = 1, providerStartTimeUtcTicks = 1, providerGeneration = new string('b', 32), providerToken = "owned-token" });
                bool valid = contracts.Validate("urn:splinter:v1:product-write-recovery-probe-request", input).IsValid;
                Assert.Equal(ProductWriteRecoveryTests.Cases.Contains(op + ":" + boundary), valid); if (valid) accepted++;
            }
        Assert.Equal(13, accepted);
    }

    [Fact]
    public async Task DefaultPlanCreatesNoSourceHomeOrEvidence()
    {
        using var dir = new TestDirectory(); string project = Path.Combine(dir.Path, "source"), home = Path.Combine(dir.Path, "home"), output = Path.Combine(dir.Path, "evidence");
        var start = new ProcessStartInfo("pwsh") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string arg in new[] { "-NoProfile", "-File", Path.Combine(AppContext.BaseDirectory, "Scripts", "test-product-write-recovery.ps1"), "-Project", project, "-HomePath", home, "-OutputPath", output, "-BaseReference", "recorded-base" }) start.ArgumentList.Add(arg);
        using var child = Process.Start(start)!; var ct = TestContext.Current.CancellationToken; var stdout = child.StandardOutput.ReadToEndAsync(ct); var stderr = child.StandardError.ReadToEndAsync(ct);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try { await child.WaitForExitAsync(timeout.Token); }
        finally { if (!child.HasExited) { child.Kill(); await child.WaitForExitAsync(CancellationToken.None); } }
        Assert.True(child.ExitCode == 0, await stderr); Assert.False(Directory.Exists(project)); Assert.False(Directory.Exists(home)); Assert.False(Directory.Exists(output));
        var plan = JsonSerializer.Deserialize<JsonElement>(await stdout); Assert.Equal("development-plan", plan.GetProperty("stage").GetString());
        Assert.Equal(13, plan.GetProperty("totalCases").GetInt32()); Assert.Equal(ProductWriteRecoveryTests.Cases, plan.GetProperty("configuration").GetProperty("caseNames").EnumerateArray().Select(e => e.GetString()));
    }
}
