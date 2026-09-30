[CmdletBinding()]
param([Parameter(Mandatory)][string]$OutputPath, [switch]$Execute)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$OutputPath = [IO.Path]::GetFullPath($OutputPath)
$boundaries = @('commit.before-append', 'commit.after-header', 'commit.after-metadata',
    'commit.after-payload-prefix', 'commit.after-payload', 'commit.after-checksum',
    'commit.before-flush', 'commit.after-flush', 'commit.after-apply', 'caller.before-ack', 'caller.after-ack')
$operations = @('write', 'rename', 'replace', 'truncate', 'create', 'delete')
if (-not $Execute) {
    @{ stage='development-plan'; evidence=$OutputPath; operations=$operations; boundaries=$boundaries;
       explicitFlushBoundaries=@('flush.before','flush.after'); cases=58;
       effects='Execute creates synthetic private journals and terminates only its exact child PID/start; retains evidence; no VSS/WinFsp/Git/Unity or cleanup' } | ConvertTo-Json -Depth 5
    return
}
if (Test-Path -LiteralPath $OutputPath) { throw 'Use a new evidence directory. Existing evidence is preserved.' }
$tests = Join-Path $repo 'tests/Splinter.Tests/bin/Release/net10.0/Splinter.Tests.dll'
if (-not (Test-Path -LiteralPath $tests -PathType Leaf)) { throw 'Run pwsh ./scripts/build.ps1 before Execute.' }
$previous = [Environment]::GetEnvironmentVariable('SPLINTER_JOURNAL_RECOVERY_EVIDENCE', 'Process')
try {
    $env:SPLINTER_JOURNAL_RECOVERY_EVIDENCE = $OutputPath
    & dotnet $tests -method 'Splinter.Tests.JournalInterruptionTests.EvidenceRun'
    if ($LASTEXITCODE -ne 0) { throw "Journal recovery failed: $LASTEXITCODE. Preserve evidence and inspect failed trial." }
    $result = Join-Path $OutputPath 'result.json'
    if (-not (Test-Path -LiteralPath $result -PathType Leaf)) { throw 'Missing completed matrix result.' }
    Get-Content -LiteralPath $result -Raw
}
finally { [Environment]::SetEnvironmentVariable('SPLINTER_JOURNAL_RECOVERY_EVIDENCE', $previous, 'Process') }
