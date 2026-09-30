[CmdletBinding()]
param([Parameter(Mandatory)][string]$OutputPath, [string]$MountHome, [string[]]$Cases, [switch]$Execute)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$OutputPath = [IO.Path]::GetFullPath($OutputPath)
if (-not $MountHome) { $MountHome = Join-Path $repo ('.local/nr03-kernel/' + [IO.Path]::GetFileName($OutputPath)) }
$MountHome = [IO.Path]::GetFullPath($MountHome)
$known = @()
foreach ($operation in @('write','rename','replace','truncate','flush')) {
    foreach ($boundary in @('callback.before-effect','callback.after-effect','client.after-ack')) { $known += "${operation}:${boundary}" }
}
$known += @('mmap:client.before-view-flush','mmap:callback.before-effect','mmap:callback.after-effect','mmap:client.after-ack')
if (-not $Cases) { $Cases = $known }
if (($Cases | Where-Object { $_ -notin $known }) -or @($Cases | Select-Object -Unique).Count -ne $Cases.Count) { throw 'Unknown or duplicate case.' }
$configuration = @{ version=1; evidencePath=$OutputPath; mountHome=$MountHome; caseNames=@($Cases) }
if (-not $Execute) {
    @{ stage='development-plan'; configuration=$configuration; totalCases=$Cases.Count;
       effects='real local WinFsp with fake snapshot; exact owned provider/client termination; same-path restart and normal unmount; retains private delta/evidence; no VSS/Git/Unity/install or VIVOBOOK' } | ConvertTo-Json -Depth 5
    return
}
foreach ($path in @($OutputPath,$MountHome)) { if (Test-Path -LiteralPath $path) { throw 'Use new evidence and mount-home paths; existing runs are preserved.' } }
if ($OutputPath.Equals($MountHome,[StringComparison]::OrdinalIgnoreCase) -or $OutputPath.StartsWith($MountHome + '\',[StringComparison]::OrdinalIgnoreCase) -or $MountHome.StartsWith($OutputPath + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Evidence and mount home must be separate trees.' }
$tests = Join-Path $repo 'tests/Splinter.Tests/bin/Release/net10.0/Splinter.Tests.dll'
if (-not (Test-Path -LiteralPath $tests -PathType Leaf)) { throw 'Run pwsh ./scripts/build.ps1 first.' }
$previous = [Environment]::GetEnvironmentVariable('SPLINTER_KERNEL_RECOVERY_RUN','Process')
try {
    $env:SPLINTER_KERNEL_RECOVERY_RUN = $configuration | ConvertTo-Json -Depth 5 -Compress
    & dotnet $tests -method 'Splinter.Tests.KernelRecoveryTests.EvidenceRun'
    if ($LASTEXITCODE -ne 0) { throw "Kernel recovery failed: $LASTEXITCODE. Preserve partial trials and inspect process-output.json." }
    Get-Content -LiteralPath (Join-Path $OutputPath 'result.json') -Raw
}
finally { [Environment]::SetEnvironmentVariable('SPLINTER_KERNEL_RECOVERY_RUN',$previous,'Process') }
