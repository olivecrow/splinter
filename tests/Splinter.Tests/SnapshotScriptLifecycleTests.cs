using System.Diagnostics;
using System.Text.Json;

namespace Splinter.Tests;

public sealed class SnapshotScriptLifecycleTests
{
    [Theory]
    [InlineData("")]
    [InlineData("unmount")]
    [InlineData("accounting")]
    public async Task PreservationAccountsOnlyAfterProviderExitAndRetainsResultsOnFailure(string failure)
    {
        using var d = new TestDirectory();
        string mount = Path.Combine(d.Path, "mount-a"), delta = Path.Combine(d.Path, "delta-a"), control = Path.Combine(d.Path, "control-a");
        string registration = Path.Combine(d.Path, "registration");
        foreach (string path in new[] { mount, delta, control, registration }) Directory.CreateDirectory(path);
        byte[] content = [1, 2, 3]; File.WriteAllBytes(Path.Combine(delta, "delta.journal"), content);
        File.WriteAllText(Path.Combine(control, "request.json"), JsonSerializer.Serialize(new
        {
            mountPath = mount, deltaPath = delta, identity = new { id = "owner", snapshot = new { id = "snapshot" } }
        }));
        File.WriteAllText(Path.Combine(control, "state.json"), "{\"storage\":{\"durableSequence\":1}}");
        File.WriteAllText(Path.Combine(d.Path, "git-a.json"), JsonSerializer.Serialize(new { project = d.Path, gitDirectory = registration }));
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Scripts", "preserve-snapshot-overlay-unity.ps1"), Path.Combine(d.Path, "preserve-snapshot-overlay-unity.ps1"));
        // Replace only external command boundaries; no Git registration, Editor,
        // snapshot, WinFsp mount or Python process is created by this fixture.
        File.WriteAllText(Path.Combine(d.Path, "invoke-snapshot-spike.ps1"), """
            param($OverlayRequestPath, $Action, $ReceiptPath, $Revision)
            Add-Content -LiteralPath (Join-Path $global:fixture 'calls.txt') -Value $Action
            $global:LASTEXITCODE = 0
            if ($Action -eq 'unmount') {
                if ($global:failure -eq 'unmount') {
                    $global:LASTEXITCODE = 2
                    '{"status":"Failed"}'; return
                }
                $global:writer.Dispose(); $global:writer = $null
                [IO.Directory]::Delete((Join-Path $global:fixture 'mount-a'))
            }
            if ($Action -eq 'git-release') { [IO.Directory]::Delete((Join-Path $global:fixture 'registration')) }
            '{"status":"Succeeded","data":{"result":{"revision":"revision","resultRef":"result-ref","independentOfSnapshot":true}}}'
            """);
        string driver = Path.Combine(d.Path, "driver.ps1");
        File.WriteAllText(driver, """
            param($Fixture, $Failure)
            $ErrorActionPreference = 'Stop'
            $global:fixture = $Fixture; $global:failure = $Failure
            $global:writer = [IO.FileStream]::new((Join-Path $Fixture 'delta-a/delta.journal'), 'Open', 'ReadWrite', 'Read')
            function git {
                $global:LASTEXITCODE = 0
                if ($args -contains 'rev-parse') { 'revision' }
            }
            function python {
                Add-Content -LiteralPath (Join-Path $Fixture 'calls.txt') -Value 'accounting'
                if ($global:writer) { throw 'JournalStillOpen' }
                $stream = [IO.FileStream]::new($args[1], 'Open', 'Read', 'Read')
                try { if ($stream.ReadByte() -ne 1) { throw 'JournalChanged' } } finally { $stream.Dispose() }
                $global:LASTEXITCODE = 0
                if ($Failure -eq 'accounting') { $global:LASTEXITCODE = 1; return }
                Set-Content -LiteralPath $args[-1] -Value '{}'
            }
            try { & (Join-Path $Fixture 'preserve-snapshot-overlay-unity.ps1') -RunDirectory $Fixture -Name a }
            finally { if ($global:writer) { $global:writer.Dispose() } }
            """);
        var (exit, error) = await RunPowerShell(driver, d.Path, failure);
        string[] expected = failure switch
        {
            "unmount" => ["preserve", "unmount"],
            "accounting" => ["preserve", "unmount", "accounting"],
            _ => ["preserve", "unmount", "accounting", "git-release"]
        };
        Assert.Equal(expected, File.ReadAllLines(Path.Combine(d.Path, "calls.txt")));
        Assert.Equal(content, File.ReadAllBytes(Path.Combine(delta, "delta.journal")));
        Assert.True(File.Exists(Path.Combine(d.Path, "unity-a-preserved.json")));
        if (failure.Length == 0)
        {
            Assert.True(exit == 0, error); Assert.False(Directory.Exists(registration));
            Assert.True(File.Exists(Path.Combine(d.Path, "results/a/result.json")));
        }
        else
        {
            Assert.NotEqual(0, exit); Assert.True(Directory.Exists(registration));
            Assert.False(Directory.Exists(Path.Combine(d.Path, "results")));
            Assert.Equal(failure == "unmount", Directory.Exists(mount));
        }
    }

    [Fact]
    public async Task HelpersUseExplicitPathsWithoutInitializingAllocationOrChangingCallerScope()
    {
        using var d = new TestDirectory();
        string driver = Path.Combine(d.Path, "driver.ps1");
        File.WriteAllText(driver, """
            param($Fixture, $Module)
            $ErrorActionPreference = 'Stop'; $run = 'caller-owned'
            Import-Module $Module
            if ($run -ne 'caller-owned' -or ('SnapshotOverlayAllocation' -as [type])) { throw 'ImportSideEffect' }
            $first = Join-Path $Fixture 'first'; $second = Join-Path $Fixture 'second'
            [IO.Directory]::CreateDirectory($first) | Out-Null
            [IO.Directory]::CreateDirectory($second) | Out-Null
            Save-OverlayStage $first 'stage' @{name='first'}
            Save-OverlayStage $second 'stage' @{name='second'}
            if ((Get-Content (Join-Path $first 'stage.json') -Raw | ConvertFrom-Json).name -ne 'first' -or
                (Get-Content (Join-Path $second 'stage.json') -Raw | ConvertFrom-Json).name -ne 'second') { throw 'WrongStagePath' }
            $file = Join-Path $Fixture 'range.bin'; [IO.File]::WriteAllBytes($file, [byte[]](1,2,3,4))
            Write-OverlayRange $file 1 ([byte[]](5,6))
            Confirm-OverlayBytes ([byte[]](1,5,6,4)) (Read-OverlayRange $file 0 4) 'range'
            """);
        var (exit, error) = await RunPowerShell(driver, d.Path,
            Path.Combine(AppContext.BaseDirectory, "Scripts", "internal", "SnapshotOverlayIO.psm1"));
        Assert.True(exit == 0, error);
    }

    private static async Task<(int Exit, string Error)> RunPowerShell(string driver, params string[] arguments)
    {
        var start = new ProcessStartInfo("pwsh") { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in new[] { "-NoProfile", "-NonInteractive", "-File", driver }.Concat(arguments))
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); }
        finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(CancellationToken.None); } }
        await output;
        return (process.ExitCode, await error);
    }
}
