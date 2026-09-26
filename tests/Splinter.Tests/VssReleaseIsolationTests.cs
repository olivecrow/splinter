using System.Diagnostics;
using Splinter.Platform.Windows;

namespace Splinter.Tests;

public sealed class VssReleaseIsolationTests
{
    [Theory]
    [InlineData("older", "SnapshotReleaseIsolationRequired", 0)]
    [InlineData("same-time", "SnapshotReleaseIsolationRequired", 0)]
    [InlineData("unknown-age", "SnapshotReleaseIsolationRequired", 0)]
    [InlineData("unknown-volume", "SnapshotReleaseIsolationRequired", 0)]
    [InlineData("query-failure", "InventoryUnavailable", 0)]
    [InlineData("alone", "", 1)]
    [InlineData("younger", "", 1)]
    [InlineData("other-volume", "", 1)]
    [InlineData("collateral", "SnapshotCollateralLoss", 1)]
    [InlineData("storage-unknown", "SnapshotReleaseIsolationRequired", 0)]
    public async Task ActualHelperGuardsDeletionAndAuditsOtherIdentities(string scenario, string expectedError, int removals)
    {
        using var directory = new TestDirectory();
        using var resource = typeof(VssSnapshotProvider).Assembly.GetManifestResourceStream("Splinter.VssSnapshot.ps1")!;
        using var reader = new StreamReader(resource);
        string helper = Path.Combine(directory.Path, "helper.ps1");
        await File.WriteAllTextAsync(helper, await reader.ReadToEndAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        string harness = Path.Combine(directory.Path, "check.ps1");
        await File.WriteAllTextAsync(harness, """
            param($Helper, $Scenario, $ExpectedError, [int]$ExpectedRemovals)
            $ErrorActionPreference='Stop'
            $tokens=$null; $errors=$null
            $ast=[Management.Automation.Language.Parser]::ParseFile($Helper,[ref]$tokens,[ref]$errors)
            if($errors.Count) {throw 'Invalid helper syntax'}
            $definition=$ast.Find({param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq 'Invoke-OwnedSnapshotRemoval'},$false)
            if(-not $definition) {throw 'Missing real release implementation'}
            Invoke-Expression $definition.Extent.Text
            $measurement=$ast.Find({param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq 'Measure-ReleaseStorage'},$false)
            Invoke-Expression $measurement.Extent.Text
            $receiptRoot=$PSScriptRoot
            $releaseRequest=@{requestId='attempt'}; $receipt=@{ownerId='owner';snapshot=@{id='owned'}}; $bootTime=[DateTimeOffset]::UtcNow
            $script:removed=@(); $script:saved=$null
            $owned=[pscustomobject]@{ID='owned';VolumeName='volume-a';InstallDate=[datetime]'2026-09-25T12:00:00Z'}
            $other=[pscustomobject]@{ID='unrelated';VolumeName='volume-a';InstallDate=[datetime]'2026-09-25T11:00:00Z'}
            switch($Scenario) {
                'same-time' {$other.InstallDate=$owned.InstallDate}
                'unknown-age' {$other.InstallDate=$null}
                'unknown-volume' {$other.VolumeName=$null}
                'younger' {$other.InstallDate=$owned.InstallDate.AddHours(1)}
                'other-volume' {$other.VolumeName='volume-b'}
                'collateral' {$other.InstallDate=$owned.InstallDate.AddHours(1)}
            }
            $script:inventory=if($Scenario -eq 'alone') {@($owned)} else {@($owned,$other)}
            function Get-CimInstance { [CmdletBinding()]param($ClassName)
                if($ClassName -eq 'Win32_ShadowStorage') {
                    if($Scenario -eq 'storage-unknown') {throw 'StorageUnavailable'}
                    return [pscustomobject]@{Volume=@{DeviceID='volume-a'};UsedSpace=1024;AllocatedSpace=4096;MaxSpace=8192}
                }
                if($Scenario -eq 'query-failure') {throw 'InventoryUnavailable'}
                $script:inventory
            }
            function Remove-CimInstance { [CmdletBinding()]param([Parameter(ValueFromPipeline)]$InputObject)
                process {
                    $script:removed+=@($InputObject.ID)
                    $script:inventory=if($Scenario -eq 'collateral') {@()} else {@($script:inventory | Where-Object ID -ne $InputObject.ID)}
                }
            }
            function Save-Json($Path,$Value) {$script:saved=$Value}
            $caught=''
            try {Invoke-OwnedSnapshotRemoval $owned} catch {$caught=$_.Exception.Message.Split(':')[0]}
            if($caught -cne $ExpectedError) {throw "Expected '$ExpectedError', got '$caught'"}
            if($script:removed.Count -ne $ExpectedRemovals -or @($script:removed | Where-Object {$_ -ne 'owned'}).Count) {throw 'Wrong deletion target/count'}
            if($ExpectedRemovals -eq 0 -and @($script:inventory | Where-Object ID -eq 'owned').Count -ne 1) {throw 'Owned snapshot was lost'}
            if($Scenario -eq 'collateral' -and @($script:saved.missingOtherIds)[0] -ne 'unrelated') {throw 'Missing collateral evidence'}
            if($ExpectedError -eq 'SnapshotReleaseIsolationRequired' -and ($script:saved.deletionAttempted -or @($script:saved.blockingIds)[0] -ne 'unrelated')) {throw 'Missing preservation evidence'}
            if($ExpectedError -eq 'SnapshotReleaseIsolationRequired') {
                if($script:saved.ownerId -ne 'owner' -or $script:saved.requestId -ne 'attempt') {throw 'Missing release binding'}
                if($Scenario -eq 'storage-unknown') {
                    if($null -ne $script:saved.sharedStorage.usedBytes -or $script:saved.sharedStorage.failureCode -ne 'SnapshotStorageUnknown') {throw 'Unknown was treated as zero'}
                } elseif($script:saved.sharedStorage.usedBytes -ne 1024) {throw 'Shared volume measurement missing'}
            }
            """, TestContext.Current.CancellationToken);
        var start = new ProcessStartInfo("pwsh")
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string arg in new[] { "-NoProfile", "-NonInteractive", "-File", harness, helper, scenario, expectedError, removals.ToString() })
            start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        Assert.True(process.ExitCode == 0, await error + await output);
    }
}
