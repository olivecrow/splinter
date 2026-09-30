using System.Diagnostics;
using System.Text.Json;
using Splinter.Protocol;

namespace Splinter.Tests;

public sealed class KernelProbeContractTests
{
    [Fact]
    public void RequestSchemaDescribesExactlyNineteenSupportedCasesAndCurrentGenerations()
    {
        var catalog = new ContractCatalog(); int valid = 0;
        foreach (string operation in new[] { "write", "rename", "replace", "truncate", "flush", "mmap" })
            foreach (string boundary in new[] { "callback.before-effect", "callback.after-effect", "client.after-ack", "client.before-view-flush", "unknown" })
            {
                var request = JsonSerializer.SerializeToElement(new { version = 1, directory = "new-evidence", mountPath = "new-view", operation, boundary, nonce = new string('a', 32), generation = 1 });
                bool expected = KernelRecoveryTests.CaseNames.Contains(operation + ":" + boundary);
                Assert.Equal(expected, catalog.Validate("urn:splinter:v1:kernel-recovery-probe-request", request).IsValid);
                if (expected) valid++;
            }
        Assert.Equal(19, valid); Assert.Equal(19, KernelRecoveryTests.CaseNames.Length);
        var invalid = JsonSerializer.SerializeToElement(new { version = 1, directory = "new", mountPath = "new", operation = "write", boundary = "callback.before-effect", nonce = new string('a', 32), generation = 0 });
        Assert.False(catalog.Validate("urn:splinter:v1:kernel-recovery-probe-request", invalid).IsValid);
    }

    [Fact]
    public async Task ScriptDefaultPlansWithoutCreatingEvidenceOrMountHome()
    {
        using var dir = new TestDirectory(); string evidence = Path.Combine(dir.Path, "evidence"), mount = Path.Combine(dir.Path, "mount-home");
        var start = new ProcessStartInfo("pwsh") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string arg in new[] { "-NoProfile", "-File", Path.Combine(AppContext.BaseDirectory, "Scripts", "test-kernel-recovery.ps1"), "-OutputPath", evidence, "-MountHome", mount }) start.ArgumentList.Add(arg);
        using var child = Process.Start(start)!; var ct = TestContext.Current.CancellationToken;
        var output = child.StandardOutput.ReadToEndAsync(ct); var error = child.StandardError.ReadToEndAsync(ct);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try { await child.WaitForExitAsync(deadline.Token); }
        finally { if (!child.HasExited) { child.Kill(); await child.WaitForExitAsync(CancellationToken.None); } }
        Assert.True(child.ExitCode == 0, await error); Assert.False(Directory.Exists(evidence)); Assert.False(Directory.Exists(mount));
        var plan = JsonSerializer.Deserialize<JsonElement>(await output);
        Assert.Equal("development-plan", plan.GetProperty("stage").GetString()); Assert.Equal(19, plan.GetProperty("totalCases").GetInt32());
        Assert.Equal(KernelRecoveryTests.CaseNames, plan.GetProperty("configuration").GetProperty("caseNames").EnumerateArray().Select(e => e.GetString()));
    }
}
