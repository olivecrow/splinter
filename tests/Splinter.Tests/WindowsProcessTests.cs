using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using Splinter.Core;
using Splinter.Platform.Windows;

namespace Splinter.Tests;

public sealed class WindowsProcessTests
{
    [Fact]
    public async Task SupervisorSurvivesClientAndCleansDescendantThatOutlivesRoot()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows process-group integration test.");
        var ct = TestContext.Current.CancellationToken;
        using var d = new TestDirectory();
        string child = Path.Combine(d.Path, "child.ps1"), root = Path.Combine(d.Path, "root.ps1");
        await File.WriteAllTextAsync(child, "$memory = [byte[]]::new(64MB)\n[Array]::Fill[byte]($memory, 7)\n$PID | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'child.pid')\nStart-Sleep -Seconds 90\n", ct);
        await File.WriteAllTextAsync(root, "Start-Process -FilePath (Get-Process -Id $PID).Path -ArgumentList @('-NoProfile','-File',('" + '"' + "' + (Join-Path $PSScriptRoot 'child.ps1') + '" + '"' + "')) -WindowStyle Hidden\nwhile (-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'exit'))) { Start-Sleep -Milliseconds 100 }\n", ct);
        // A separate unrelated process must survive cleanup of the owned group.
        using var sentinel = Process.Start(new ProcessStartInfo("pwsh") { UseShellExecute = false, CreateNoWindow = true,
            ArgumentList = { "-NoProfile", "-Command", "Start-Sleep -Seconds 90" } })!;
        using var callerPipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
        ApplicationLease? lease = null;
        try
        {
            lease = await new WindowsProcessWorkspaceProvider().StartAsync(new("C:\\Program Files\\PowerShell\\7\\pwsh.exe",
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
