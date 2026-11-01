param(
    [Parameter(Mandatory)][ValidateSet('git','none')][string]$Source,
    [Parameter(Mandatory)][string]$FixtureRoot,
    [Parameter(Mandatory)][string]$CliAssembly,
    [Parameter(Mandatory)][string]$ProbeAssembly,
    [ValidateRange(16,128)][int]$Entries = 128,
    [string]$BudgetFile
)
$ErrorActionPreference = 'Stop'
$FixtureRoot = [IO.Path]::GetFullPath($FixtureRoot)
$CliAssembly = [IO.Path]::GetFullPath($CliAssembly)
$ProbeAssembly = [IO.Path]::GetFullPath($ProbeAssembly)
$harness = Join-Path $PSScriptRoot 'scalable-storage-editorless.ps1'
$watch = [Diagnostics.Stopwatch]::new()
$success = $false
$stageTimes = [Collections.Generic.List[object]]::new()
$budgets = if ($BudgetFile) { Get-Content -LiteralPath $BudgetFile -Raw | ConvertFrom-Json } else { $null }
if ($budgets) {
    $values = @($budgets.trialExpectedSeconds, $budgets.defaultCommandExpectedSeconds)
    $values += @('start','bulk','selected','final') | ForEach-Object { $budgets.stageExpectedSeconds.$_ }
    $values += @($budgets.commandExpectedSeconds.PSObject.Properties | ForEach-Object { $_.Value })
    foreach ($value in $values) { if (-not [double]::IsFinite([double]$value) -or [double]$value -le 0) { throw 'Supply finite positive trial, stage and command estimates before seeding.' } }
}
$trialLimit = if ($budgets) { 2 * $budgets.trialExpectedSeconds } else { 140 }
$previousBudget = $env:SPLINTER_VERIFY_BUDGET_PATH
$previousDeadline = $env:SPLINTER_VERIFY_DEADLINE_UTC
$previousStageDeadline = $env:SPLINTER_VERIFY_STAGE_DEADLINE_UTC
if ($budgets) { $env:SPLINTER_VERIFY_BUDGET_PATH = [IO.Path]::GetFullPath($BudgetFile) }
function Read-State([string]$Path) {
    $stream=[IO.FileStream]::new($Path,[IO.FileMode]::Open,[IO.FileAccess]::Read,([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
    try { $reader=[IO.StreamReader]::new($stream); try { return ($reader.ReadToEnd() | ConvertFrom-Json) } finally { $reader.Dispose() } } finally { $stream.Dispose() }
}
try {
    & pwsh -NoProfile -File $harness -Stage seed -Source $Source -FixtureRoot $FixtureRoot -CliAssembly $CliAssembly -Entries $Entries
    if ($LASTEXITCODE -ne 0) { throw 'Owned fixture seeding failed.' }
    $seed = @(Get-ChildItem -LiteralPath (Join-Path $FixtureRoot 'p') -File -Recurse | Where-Object { $_.FullName -notmatch '[\\/]\.git[\\/]' } | Sort-Object FullName | ForEach-Object {
        @{ path=[IO.Path]::GetRelativePath((Join-Path $FixtureRoot 'p'),$_.FullName).Replace('\','/'); bytes=$_.Length; modifiedUtcTicks=$_.LastWriteTimeUtc.Ticks; attributes=[int]$_.Attributes; sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    })
    ConvertTo-Json -InputObject $seed -Depth 5 | Set-Content -LiteralPath (Join-Path $FixtureRoot 'evidence/seed-manifest.json') -Encoding utf8
    $watch.Start()
    $env:SPLINTER_VERIFY_DEADLINE_UTC = [DateTimeOffset]::UtcNow.AddSeconds($trialLimit).ToString('O')
    foreach ($stage in @('start','bulk','selected','final')) {
        if ($watch.Elapsed.TotalSeconds -ge $trialLimit) { throw 'Trial budget exhausted; stop before starting another stage.' }
        if ($budgets) { $env:SPLINTER_VERIFY_STAGE_DEADLINE_UTC = [DateTimeOffset]::UtcNow.AddSeconds(2 * $budgets.stageExpectedSeconds.$stage).ToString('O') }
        $stageClock = [Diagnostics.Stopwatch]::StartNew()
        & pwsh -NoProfile -File $harness -Stage $stage -Source $Source -FixtureRoot $FixtureRoot -CliAssembly $CliAssembly -ProbeAssembly $ProbeAssembly -Entries $Entries *> (Join-Path $FixtureRoot ('evidence/trial-' + $stage + '.log'))
        $stageTimes.Add(@{ stage=$stage; seconds=$stageClock.Elapsed.TotalSeconds; exitCode=$LASTEXITCODE })
        if ($LASTEXITCODE -ne 0) { throw "Trial stage $stage failed; preserve exact requests and stop owned providers." }
        if ($watch.Elapsed.TotalSeconds -ge $trialLimit -or ($budgets -and $stageClock.Elapsed.TotalSeconds -ge 2 * $budgets.stageExpectedSeconds.$stage)) {
            throw 'The fixed deadline elapsed before the stage returned; preserve state and start no later stage.'
        }
    }
    $success = $true
} finally {
    $env:SPLINTER_VERIFY_DEADLINE_UTC = $previousDeadline
    $env:SPLINTER_VERIFY_STAGE_DEADLINE_UTC = $previousStageDeadline
    $env:SPLINTER_VERIFY_BUDGET_PATH = $previousBudget
    $watch.Stop()
    $ownerFile = Join-Path $FixtureRoot 'fixture-owner.json'
    if (Test-Path -LiteralPath $ownerFile) {
        $owner = Get-Content -LiteralPath $ownerFile -Raw | ConvertFrom-Json
        if ($owner.home -ne (Join-Path $FixtureRoot 'h') -or $owner.project -ne (Join-Path $FixtureRoot 'p') -or $owner.source -ne $Source) { throw 'Refuse provider handling for a foreign fixture owner.' }
        $stops = [Collections.Generic.List[object]]::new()
        foreach ($area in @('records','project-bases','library-bases')) {
            $parent = Join-Path $owner.home $area
            if (-not (Test-Path -LiteralPath $parent)) { continue }
            foreach ($entry in Get-ChildItem -LiteralPath $parent -Directory) {
                $control = Join-Path $entry.FullName 'overlay'
                $statePath = Join-Path $control 'state.json'
                if (-not (Test-Path -LiteralPath $statePath)) { continue }
                $state = Read-State $statePath
                $native = Get-Process -Id $state.processId -ErrorAction SilentlyContinue
                if (-not $native) { continue }
                try { $alive = $native.StartTime.ToUniversalTime().Ticks -eq $state.processStartTimeUtcTicks } finally { $native.Dispose() }
                if (-not $alive) { continue }
                if ($success) { $success = $false }
                $requestPath = Join-Path $control 'request.json'
                $request = Get-Content -LiteralPath $requestPath -Raw | ConvertFrom-Json
                $evidencePath = Join-Path $FixtureRoot ('evidence/trial-stop-' + $entry.Name + '.json')
                & dotnet $ProbeAssembly --stop ([IO.Path]::GetDirectoryName($CliAssembly)) $requestPath $request.identity.id $state.processId $state.processStartTimeUtcTicks $evidencePath
                $stops.Add(@{ owner=$request.identity.id; exitCode=$LASTEXITCODE; evidence=$evidencePath })
                if ($LASTEXITCODE -ne 0) { throw 'Normal owned provider stop did not complete; preserve and report this exact state.' }
            }
        }
        @{ source=$Source; entries=$Entries; fixture=$FixtureRoot; cliAssembly=$CliAssembly; completed=$success;
            seconds=$watch.Elapsed.TotalSeconds; trialLimitSeconds=$trialLimit; budgetFile=$BudgetFile; stages=$stageTimes.ToArray(); stops=$stops.ToArray(); observedAt=[DateTimeOffset]::UtcNow.ToString('O') } |
            ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $FixtureRoot 'evidence/throughput-trial.json') -Encoding utf8
    }
}
if (-not $success) { exit 1 }
