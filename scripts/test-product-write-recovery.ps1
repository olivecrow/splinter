[CmdletBinding()]
param([Parameter(Mandatory)][string]$Project,[Parameter(Mandatory)][string]$HomePath,
      [Parameter(Mandatory)][string]$OutputPath,[Parameter(Mandatory)][string]$BaseReference,[string[]]$Cases,[switch]$Execute)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$Project=[IO.Path]::GetFullPath($Project); $HomePath=[IO.Path]::GetFullPath($HomePath); $OutputPath=[IO.Path]::GetFullPath($OutputPath)
$known=@()
foreach ($op in @('write','rename','replace','truncate','flush')) { foreach ($boundary in @('client.before-effect','client.after-ack')) { $known += "${op}:${boundary}" } }
$known += @('write:client.after-write-before-flush','mmap:client.before-view-flush','mmap:client.after-ack')
if (-not $Cases) { $Cases=$known }
if (($Cases | Where-Object { $_ -notin $known }) -or @($Cases | Select-Object -Unique).Count -ne $Cases.Count) { throw 'Unsupported/duplicate case.' }
$config=@{version=1;project=$Project;home=$HomePath;evidencePath=$OutputPath;baseReference=$BaseReference;caseNames=@($Cases);cliPath=(Join-Path $repo 'src/Splinter.Cli/bin/Release/net10.0/splinter.dll')}
if (-not $Execute) { @{stage='development-plan';configuration=$config;totalCases=$Cases.Count;effects='existing registered local source/home/service only; ordinary CLI/VSS/WinFsp/Git; exact owned provider interruption; same-request reconnect; explicit editorless preserve/release; no observer/install/Unity/VIVOBOOK'} | ConvertTo-Json -Depth 6; return }
if (Test-Path -LiteralPath $OutputPath) { throw 'Use new evidence; prior runs are preserved.' }
foreach ($tree in @($Project,$HomePath)) { if ($OutputPath.Equals($tree,[StringComparison]::OrdinalIgnoreCase) -or $OutputPath.StartsWith($tree+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Evidence must be outside source and home.' } }
$tests=Join-Path $repo 'tests/Splinter.Tests/bin/Release/net10.0/Splinter.Tests.dll'
if (-not (Test-Path -LiteralPath $tests) -or -not (Test-Path -LiteralPath $config.cliPath)) { throw 'Run pwsh ./scripts/build.ps1 first.' }
$previous=[Environment]::GetEnvironmentVariable('SPLINTER_PRODUCT_WRITE_RECOVERY_RUN','Process')
try {
    $env:SPLINTER_PRODUCT_WRITE_RECOVERY_RUN=$config | ConvertTo-Json -Depth 6 -Compress
    & dotnet $tests -method 'Splinter.Tests.ProductWriteRecoveryTests.EvidenceRun'
    if ($LASTEXITCODE) { throw 'Product write recovery failed; preserve evidence/workspace/snapshot and inspect the exact recorded task.' }
    Get-Content -LiteralPath (Join-Path $OutputPath 'result.json') -Raw
} finally { [Environment]::SetEnvironmentVariable('SPLINTER_PRODUCT_WRITE_RECOVERY_RUN',$previous,'Process') }
