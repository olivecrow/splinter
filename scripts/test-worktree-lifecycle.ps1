#Requires -Version 7.2
[CmdletBinding()]
param([Parameter(Mandatory)][string]$Repository)

$ErrorActionPreference = 'Stop'
$productRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$cli = Join-Path $productRoot 'src/Splinter.Cli/bin/Release/net10.0/splinter.dll'
if (-not (Test-Path -LiteralPath $cli -PathType Leaf)) { throw 'Run scripts/build.ps1 first.' }
$runId = [Guid]::NewGuid().ToString('N')
$runRoot = [IO.Path]::GetFullPath((Join-Path $productRoot "artifacts/worktree-smoke/$runId"))
$comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }

function Assert-NoReparse([string]$Path) {
    $current = [IO.Path]::GetFullPath($Path)
    while ($current) {
        if ((Test-Path -LiteralPath $current) -and
            ((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw "Refusing a reparse path: $current"
        }
        $current = [IO.Path]::GetDirectoryName($current)
    }
}

function Invoke-Process([string]$Executable, [string]$Directory, [string[]]$Arguments) {
    $start = [Diagnostics.ProcessStartInfo]::new($Executable)
    $start.WorkingDirectory = $Directory
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($key in @($start.Environment.Keys)) {
        if ($key.StartsWith('GIT_', [StringComparison]::OrdinalIgnoreCase)) { $start.Environment.Remove($key) | Out-Null }
    }
    $start.Environment['GIT_TERMINAL_PROMPT'] = '0'
    $start.Environment['GIT_OPTIONAL_LOCKS'] = '0'
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($start)
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(30000)) {
            $process.Kill($true)
            $process.WaitForExit()
            throw "$Executable exceeded 30 seconds."
        }
        $result = $stdout.GetAwaiter().GetResult()
        $errorText = $stderr.GetAwaiter().GetResult()
        if ($process.ExitCode -ne 0) { throw "$Executable failed ($($process.ExitCode)): $errorText $result" }
        return $result.TrimEnd("`r", "`n")
    }
    finally { $process.Dispose() }
}

function Invoke-Git([string]$Directory, [string[]]$GitArgs) {
    # Suppress hooks only for this smoke run; never edit repository/global configuration.
    Invoke-Process 'git' $Directory (@('-c', "core.hooksPath=$runRoot/empty-hooks", '-C', $Directory) + $GitArgs)
}

function Get-TextHash([string]$Text) {
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($Text)))
}

function Get-SourceState {
    [ordered]@{
        head = Invoke-Git $source @('rev-parse', '--verify', 'HEAD')
        status = Invoke-Git $source @('status', '--porcelain=v1', '-z', '--untracked-files=all')
        stagedHash = Get-TextHash (Invoke-Git $source @('diff', '--cached', '--binary', '--no-ext-diff', '--no-textconv'))
        unstagedHash = Get-TextHash (Invoke-Git $source @('diff', '--binary', '--no-ext-diff', '--no-textconv'))
        readmeHash = (Get-FileHash -LiteralPath (Join-Path $source 'README.md') -Algorithm SHA256).Hash
        worktrees = Invoke-Git $source @('worktree', 'list', '--porcelain', '-z')
    }
}

function Assert-OwnedWorktree([string]$Path) {
    $full = [IO.Path]::GetFullPath($Path)
    if (-not [string]::Equals([IO.Path]::GetDirectoryName($full), $runRoot, $comparison) -or
        [IO.Path]::GetFileName($full) -notin @('a', 'b')) { throw "Unexpected cleanup target: $full" }
    if ([IO.File]::ReadAllText((Join-Path $runRoot 'owner.txt')) -ne $runId) { throw 'Run ownership changed.' }
    Assert-NoReparse $full
    $actual = [IO.Path]::GetFullPath((Invoke-Git $full @('rev-parse', '--show-toplevel')))
    if (-not [string]::Equals($full, $actual, $comparison)) { throw "Worktree root changed: $full" }
    if ((Invoke-Git $full @('rev-parse', 'HEAD')) -ne $before.head) { throw "Worktree contains a new commit: $full" }
}

$source = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $Repository).Path)
$source = [IO.Path]::GetFullPath((Invoke-Git $source @('rev-parse', '--show-toplevel')))
Assert-NoReparse $source
Assert-NoReparse $runRoot
$null = Invoke-Git $source @('ls-files', '--error-unmatch', '--', 'README.md')
$before = Get-SourceState
$null = [IO.Directory]::CreateDirectory((Join-Path $runRoot 'empty-hooks'))
[IO.File]::WriteAllText((Join-Path $runRoot 'owner.txt'), $runId)
$workspaces = @('a', 'b') | ForEach-Object {
    [pscustomobject]@{ path = (Join-Path $runRoot $_); branch = "splinter-smoke/$runId-$_"; created = $false }
}
$report = [ordered]@{
    schemaVersion = '1.0'
    kind = 'git-worktree-smoke'
    runId = $runId
    source = $source
    baselineSha = $before.head
    storageMode = 'git-checkout'
    unityStarted = $false
    startedAt = [DateTime]::UtcNow.ToString('O')
    status = 'Running'
    paths = @($workspaces.path)
    checks = @()
    errors = @()
}
$reportPath = Join-Path $runRoot 'result.json'
$report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $reportPath -Encoding utf8
$changedPath = $null
$originalBytes = $null
$writtenHash = $null
try {
    foreach ($workspace in $workspaces) {
        $null = Invoke-Git $source @('worktree', 'add', '-b', $workspace.branch, $workspace.path, $before.head)
        $workspace.created = $true
        Assert-OwnedWorktree $workspace.path
        $doctorText = Invoke-Process 'dotnet' $productRoot @($cli, 'doctor', '--project', $workspace.path)
        $doctor = $doctorText | ConvertFrom-Json
        if ($doctor.data.repository.details.isLinkedWorktree -ne 'true' -or
            $doctor.data.repository.revision -ne $before.head) { throw 'CLI could not identify the linked worktree.' }
        $doctorText | Set-Content -LiteralPath (Join-Path $runRoot "$([IO.Path]::GetFileName($workspace.path))-doctor.json") -Encoding utf8
    }
    $a = $workspaces[0].path
    $b = $workspaces[1].path
    $aIndex = Invoke-Git $a @('rev-parse', '--path-format=absolute', '--git-path', 'index')
    $bIndex = Invoke-Git $b @('rev-parse', '--path-format=absolute', '--git-path', 'index')
    if ($aIndex -eq $bIndex) { throw 'Worktrees share an index.' }
    $report.checks += 'two-registered-worktrees-with-distinct-indexes'
    $changedPath = Join-Path $a 'README.md'
    $otherPath = Join-Path $b 'README.md'
    $otherHash = (Get-FileHash -LiteralPath $otherPath).Hash
    $originalBytes = [IO.File]::ReadAllBytes($changedPath)
    [IO.File]::AppendAllText($changedPath, "`nSplinter isolated smoke $runId`n")
    $writtenHash = (Get-FileHash -LiteralPath $changedPath).Hash
    if ((Get-FileHash -LiteralPath $otherPath).Hash -ne $otherHash -or
        (Get-FileHash -LiteralPath (Join-Path $source 'README.md')).Hash -ne $before.readmeHash) {
        throw 'A modification escaped its worktree.'
    }
    if (-not (Invoke-Git $a @('status', '--porcelain=v1')) -or
        (Invoke-Git $b @('status', '--porcelain=v1', '--untracked-files=all'))) { throw 'Unexpected worktree change state.' }
    $report.checks += 'a-write-isolated-from-b-and-source'
}
catch { $report.errors += $_.Exception.Message }
finally {
    if ($writtenHash) {
        try {
            Assert-OwnedWorktree $workspaces[0].path
            if ((Get-FileHash -LiteralPath $changedPath).Hash -ne $writtenHash) { throw 'Smoke file changed externally; preserving it.' }
            [IO.File]::WriteAllBytes($changedPath, $originalBytes)
        }
        catch { $report.errors += $_.Exception.Message }
    }
    foreach ($workspace in $workspaces) {
        if (-not $workspace.created) { continue }
        try {
            Assert-OwnedWorktree $workspace.path
            if (Invoke-Git $workspace.path @('status', '--porcelain=v1', '--untracked-files=all', '--ignored')) {
                throw "Worktree has uncollected files; preserving $($workspace.path)."
            }
            $null = Invoke-Git $source @('worktree', 'remove', $workspace.path)
            if ((Invoke-Git $source @('rev-parse', $workspace.branch)) -ne $before.head) { throw 'Branch changed; preserving it.' }
            $null = Invoke-Git $source @('branch', '-d', $workspace.branch)
        }
        catch { $report.errors += $_.Exception.Message }
    }
    try {
        $after = Get-SourceState
        if (($before | ConvertTo-Json -Compress) -ne ($after | ConvertTo-Json -Compress)) {
            throw 'Source state or worktree registrations changed; inspect the report before continuing.'
        }
        $report.checks += 'source-head-index-diff-and-worktree-registry-preserved'
        if (@($workspaces | Where-Object { Test-Path -LiteralPath $_.path }).Count -eq 0) {
            $report.checks += 'owned-worktrees-and-temporary-branches-released'
        }
    }
    catch { $report.errors += $_.Exception.Message }
    $report.finishedAt = [DateTime]::UtcNow.ToString('O')
    $report.status = if ($report.errors.Count -eq 0) { 'Passed' } else { 'Failed' }
    $report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $reportPath -Encoding utf8
}
$report | ConvertTo-Json -Depth 10
if ($report.status -ne 'Passed') { throw "Worktree smoke failed. Evidence: $reportPath" }
