using System.Diagnostics;
using Splinter.Platform.Windows;

namespace Splinter.Tests;

public sealed class VssDiscardGateTests
{
    [Theory]
    [InlineData("valid")][InlineData("missing")][InlineData("other-intent")][InlineData("other-owner")][InlineData("changed-seal")]
    public async Task ActualHelperAcceptsOnlyItsExplicitDiscardGate(string scenario)
    {
        using var d = new TestDirectory(); var ct = TestContext.Current.CancellationToken;
        using var resource = typeof(VssSnapshotProvider).Assembly.GetManifestResourceStream("Splinter.VssSnapshot.ps1")!;
        using var reader = new StreamReader(resource);
        string helper = Path.Combine(d.Path, "helper.ps1"), harness = Path.Combine(d.Path, "gate.ps1");
        await File.WriteAllTextAsync(helper, await reader.ReadToEndAsync(ct), ct);
        await File.WriteAllTextAsync(harness, """
            param($Helper,$Scenario)
            $ErrorActionPreference='Stop'; $tokens=$null; $errors=$null
            $ast=[Management.Automation.Language.Parser]::ParseFile($Helper,[ref]$tokens,[ref]$errors)
            if($errors.Count){throw 'Invalid helper syntax'}
            $gate=$ast.Find({param($n) $n -is [Management.Automation.Language.IfStatementAst] -and $n.Clauses[0].Item1.Extent.Text -eq '$null -ne $releaseRequest.discard'},$true)
            if(-not $gate){throw 'Missing actual discard gate'}
            $receiptRoot=$PSScriptRoot; $OwnerId='owner'; $receipt=@{snapshot=@{id='snapshot'}}
            $discard=@{step='snapshot';intent=@{version=1;workspaceId='owner';snapshot=@{id='snapshot'};intentKey='explicit';requestDigest='digest'};seal=@{durableSequence=1;journalBytes=10;journalHash='hash'}}
            $releaseRequest=@{discard=($discard|ConvertTo-Json -Depth 20|ConvertFrom-Json -AsHashtable)}
            switch($Scenario){
              'other-intent' {$discard.intent.intentKey='other'}
              'other-owner' {$discard.intent.workspaceId='other'}
              'changed-seal' {$discard.seal.journalHash='changed'}
            }
            if($Scenario -ne 'missing'){$discard|ConvertTo-Json -Depth 20|Set-Content -LiteralPath (Join-Path $receiptRoot 'discard-authorized.json') -Encoding utf8}
            $failure=$null;try{Invoke-Expression $gate.Extent.Text}catch{$failure=$_.Exception.Message}
            if($Scenario -eq 'valid'){if($failure){throw $failure}}
            elseif($failure -ne 'StorageDiscardNotAuthorized'){throw "Unexpected failure: $failure"}
            if(Test-Path -LiteralPath (Join-Path $receiptRoot 'release-authorized.json')){throw 'Discard fabricated result preservation'}
            "gate-ok"
            """, ct);
        var start = new ProcessStartInfo("pwsh") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string arg in new[] { "-NoProfile", "-NonInteractive", "-File", harness, helper, scenario }) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(ct); var error = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        Assert.True(process.ExitCode == 0, await output + await error);
    }
}
