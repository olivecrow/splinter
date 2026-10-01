using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using Splinter.Core;
using Splinter.Platform.Windows;

namespace Splinter.Tests;

[CollectionDefinition("Windows inherited-handle observation", DisableParallelization = true)]
public sealed class WindowsInheritedHandleCollection;

// This probe exposes an inheritable process-wide handle. Concurrent test
// launches can keep it open and invalidate the supervisor-only observation.
[Collection("Windows inherited-handle observation")]
public sealed class WindowsProcessTests
{
    [Fact]
    public async Task SupervisorSurvivesClientAndCleansDescendantThatOutlivesRoot()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows process-group integration test.");
        var ct = TestContext.Current.CancellationToken;
        using var d = new TestDirectory();
        string child = Path.Combine(d.Path, "child.ps1"), root = Path.Combine(d.Path, "root.ps1"), sentinelScript = Path.Combine(d.Path, "sentinel.ps1");
        await File.WriteAllTextAsync(child, "$memory = [byte[]]::new(64MB)\n[Array]::Fill[byte]($memory, 7)\n$PID | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'child.pid')\nStart-Sleep -Seconds 90\n", ct);
        await File.WriteAllTextAsync(root, "Start-Process -FilePath (Join-Path $PSHOME 'pwsh.exe') -ArgumentList @('-NoProfile','-File',('" + '"' + "' + (Join-Path $PSScriptRoot 'child.ps1') + '" + '"' + "')) -WindowStyle Hidden\nwhile (-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'exit'))) { Start-Sleep -Milliseconds 100 }\n", ct);
        await File.WriteAllTextAsync(sentinelScript, "[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'shell.path.new'), (Join-Path $PSHOME 'pwsh.exe'))\n[IO.File]::Move((Join-Path $PSScriptRoot 'shell.path.new'), (Join-Path $PSScriptRoot 'shell.path'))\nStart-Sleep -Seconds 90\n", ct);
        // A separate unrelated process must survive cleanup of the owned group.
        using var sentinel = Process.Start(new ProcessStartInfo("pwsh") { UseShellExecute = false, CreateNoWindow = true,
            ArgumentList = { "-NoProfile", "-File", sentinelScript } })!;
        using var callerPipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
        ApplicationLease? lease = null;
        try
        {
            // The ready PowerShell reports its own installation path. External
            // MainModule inspection can be empty on the ARM Windows test host.
            string shellPath = Path.Combine(d.Path, "shell.path");
            for (int attempt = 0; attempt < 200 && !File.Exists(shellPath) && !sentinel.HasExited; attempt++) await Task.Delay(100, ct);
            Assert.True(File.Exists(shellPath), "PowerShell did not publish its executable path.");
            string shell = await File.ReadAllTextAsync(shellPath, ct);
            Assert.True(Path.IsPathFullyQualified(shell) && File.Exists(shell), "PowerShell executable is unavailable.");
            lease = await new WindowsProcessWorkspaceProvider().StartAsync(new(shell,
                ["-NoProfile", "-File", root], d.Path, Guid.NewGuid().ToString("N")), d.Path, ct);
            callerPipe.DisposeLocalCopyOfClientHandle();
            using var reader = new StreamReader(callerPipe);
            // A live supervisor must not keep an unrelated caller's pipe open.
            Assert.Equal("", await reader.ReadToEndAsync(ct).WaitAsync(TimeSpan.FromSeconds(5), ct));
            string pidPath = Path.Combine(d.Path, "child.pid");
            for (int attempt = 0; attempt < 200 && !File.Exists(pidPath); attempt++) await Task.Delay(100, ct);
            Assert.True(File.Exists(pidPath), "Owned child did not start.");
            var reconnected = new WindowsProcessWorkspaceProvider();
            var active = await reconnected.ObserveAsync(lease, ct);
            Assert.True(active.RootRunning); Assert.True(active.ActiveProcesses >= 3); Assert.True(active.PrivateBytes > 64 * 1024 * 1024);
            int childPid = int.Parse((await File.ReadAllTextAsync(pidPath, ct)).Trim());
            using var ownedChild = Process.GetProcessById(childPid);
            await File.WriteAllTextAsync(Path.Combine(d.Path, "exit"), "stop", ct);
            await reconnected.WaitForExitAsync(lease, ct);
            Assert.True(ownedChild.HasExited); Assert.False(sentinel.HasExited);
            Assert.Equal(new ProcessResources(true, false, 0, 0, 0), await reconnected.ObserveAsync(lease, ct));
            using var proof = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(d.Path, "process-exit.json"), ct));
            Assert.Equal(0, proof.RootElement.GetProperty("activeJobProcesses").GetInt32());
        }
        finally
        {
            await File.WriteAllTextAsync(Path.Combine(d.Path, "exit"), "stop", CancellationToken.None);
            if (lease is not null) await new WindowsProcessWorkspaceProvider().WaitForExitAsync(lease, CancellationToken.None);
            if (!sentinel.HasExited) { sentinel.Kill(); await sentinel.WaitForExitAsync(CancellationToken.None); }
        }
    }
}
