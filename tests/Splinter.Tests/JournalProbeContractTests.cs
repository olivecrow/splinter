using System.Diagnostics;
using System.Text.Json;
using Splinter.Protocol;

namespace Splinter.Tests;

public sealed class JournalProbeContractTests
{
    [Fact]
    public void CatalogDescribesExactlyTheExecutableMatrixAndRejectsInvalidCombinations()
    {
        var catalog = new ContractCatalog(); var schema = catalog.GetSchema("urn:splinter:v1:journal-recovery-probe-request");
        Assert.Equal(JournalInterruptionTests.CommitBoundaries.Concat(["flush.before", "flush.after"]),
            schema.GetProperty("properties").GetProperty("boundary").GetProperty("enum").EnumerateArray().Select(e => e.GetString()));
        foreach (string operation in JournalInterruptionTests.Operations.Append("flush"))
            foreach (string boundary in JournalInterruptionTests.CommitBoundaries.Concat(["flush.before", "flush.after", "unknown"]))
            {
                var input = JsonSerializer.SerializeToElement(new { version = 1, directory = "new", operation, boundary, nonce = new string('a', 32) });
                bool supported = operation == "flush" ? boundary is "flush.before" or "flush.after" :
                    JournalInterruptionTests.CommitBoundaries.Contains(boundary) && (operation == "write" || !boundary.Contains("payload", StringComparison.Ordinal));
                Assert.Equal(supported, catalog.Validate("urn:splinter:v1:journal-recovery-probe-request", input).IsValid);
            }
        var versionInput = JsonSerializer.SerializeToNode(new { version = 1, directory = "new", operation = "write",
            boundary = "commit.before-append", nonce = new string('a', 32) })!;
        Assert.True(catalog.Validate("urn:splinter:v1:journal-recovery-probe-request", JsonSerializer.SerializeToElement(versionInput)).IsValid);
        versionInput["version"] = 0;
        Assert.False(catalog.Validate("urn:splinter:v1:journal-recovery-probe-request", JsonSerializer.SerializeToElement(versionInput)).IsValid);
        Assert.Equal(58, JournalInterruptionTests.Cases.Count);
    }

    [Fact]
    public async Task ScriptDefaultOnlyPlansAndNeverCreatesOutput()
    {
        using var dir = new TestDirectory(); string output = Path.Combine(dir.Path, "evidence");
        var start = new ProcessStartInfo("pwsh") { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string arg in new[] { "-NoProfile", "-File", Path.Combine(AppContext.BaseDirectory, "Scripts", "test-journal-recovery.ps1"), "-OutputPath", output }) start.ArgumentList.Add(arg);
        using var child = Process.Start(start)!; var ct = TestContext.Current.CancellationToken;
        var stdout = child.StandardOutput.ReadToEndAsync(ct); var stderr = child.StandardError.ReadToEndAsync(ct);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try { await child.WaitForExitAsync(deadline.Token); }
        finally { if (!child.HasExited) { child.Kill(); await child.WaitForExitAsync(CancellationToken.None); } }
        Assert.True(child.ExitCode == 0, await stderr); Assert.False(Directory.Exists(output));
        var plan = JsonSerializer.Deserialize<JsonElement>(await stdout);
        Assert.Equal("development-plan", plan.GetProperty("stage").GetString());
        Assert.Equal(58, plan.GetProperty("cases").GetInt32());
        Assert.Equal(JournalInterruptionTests.CommitBoundaries, plan.GetProperty("boundaries").EnumerateArray().Select(e => e.GetString()));
    }
}
