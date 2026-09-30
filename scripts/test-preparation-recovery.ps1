[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Project,
    [Parameter(Mandatory)][string]$HomePath,
    [Parameter(Mandatory)][string]$OutputPath,
    [string]$BaseReference,
    [string[]]$Boundaries,
    [switch]$Execute
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$Project = [IO.Path]::GetFullPath($Project)
$HomePath = [IO.Path]::GetFullPath($HomePath)
$OutputPath = [IO.Path]::GetFullPath($OutputPath)
$cli = Join-Path $repo 'src/Splinter.Cli/bin/Release/net10.0/splinter.dll'
$known = @('request.before-record','request.after-record','source.before-effect','source.after-effect','source.after-record',
    'capture.before-effect','capture.after-effect','capture.after-record','registration.before-effect',
    'git-registration.after-effect','git-registration.after-record','registration.after-effect','registration.after-record',
    'delta.before-effect','delta.after-record','provider.before-effect','provider.after-effect','provider.after-record',
    'mount.before-effect','mount.after-effect','mount.after-record','attach.before-effect','attach.after-effect',
    'ready.before-record','ready.after-record','response.before-publish')
if (-not $Boundaries) { $Boundaries = $known }
if ($Boundaries | Where-Object { $_ -notin $known }) { throw 'Unsupported boundary.' }
if (-not $Execute) {
    @{ stage='development-plan'; project=$Project; home=$HomePath; evidence=$OutputPath; boundaries=$Boundaries;
       effects='explicit product CLI interruption; owned VSS/Git/WinFsp preparation, independent result and release; no install/elevation/volume changes' } | ConvertTo-Json -Depth 5
    return
}
if (Test-Path -LiteralPath $OutputPath) { throw 'Use a new evidence directory. Existing runs are never overwritten.' }
foreach ($root in @($Project, (Join-Path $HomePath 'workspaces'))) {
    if ($OutputPath.Equals($root, [StringComparison]::OrdinalIgnoreCase) -or $OutputPath.StartsWith($root + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Evidence cannot be inside source or a workspace.' }
}
if (-not $BaseReference) { throw 'Execute requires an explicit recorded base reference.' }
New-Item -ItemType Directory -Path $OutputPath | Out-Null
# The exception is limited to this explicitly selected fixture and inherited by
# these children only; global/user Git configuration is never modified.
New-Item -ItemType Directory -Path (Join-Path $OutputPath 'git') | Out-Null
$gitConfig = Join-Path $OutputPath 'git/config'
[IO.File]::WriteAllText($gitConfig, "[safe]`n`t directory = $($Project.Replace('\','/'))`n", [Text.UTF8Encoding]::new($false))
function Save([string]$path, $value) { [IO.File]::WriteAllText($path, ($value | ConvertTo-Json -Depth 80), [Text.UTF8Encoding]::new($false)) }
function Git([string[]]$gitArguments) {
    $value = & git.exe -c "safe.directory=$($Project.Replace('\','/'))" -C $Project @gitArguments
    if ($LASTEXITCODE) { throw "Git failed: $gitArguments" }
    return ($value -join "`n")
}
function StartCli([string[]]$cliArguments) {
    $start = [Diagnostics.ProcessStartInfo]::new('dotnet'); $start.UseShellExecute=$false; $start.CreateNoWindow=$true
    $start.RedirectStandardOutput=$true; $start.RedirectStandardError=$true
    $start.Environment['XDG_CONFIG_HOME']=$OutputPath
    $start.Environment['DOTNET_CLI_HOME']=Join-Path $repo '.local/dotnet'
    $start.ArgumentList.Add($cli); foreach ($a in $cliArguments) { $start.ArgumentList.Add($a) }
    $p=[Diagnostics.Process]::Start($start)
    return @{ process=$p; stdout=$p.StandardOutput.ReadToEndAsync(); stderr=$p.StandardError.ReadToEndAsync(); arguments=$cliArguments }
}
function Finish($run, [string]$stem, [bool]$allowFailure=$false) {
    if (-not $run.process.WaitForExit(180000)) { throw 'CLI deadline; preserve this exact task and processes.' }
    $stdout=$run.stdout.GetAwaiter().GetResult(); $stderr=$run.stderr.GetAwaiter().GetResult()
    [IO.File]::WriteAllText($stem+'.stdout.json', $stdout); [IO.File]::WriteAllText($stem+'.stderr.txt', $stderr)
    Save ($stem+'.command.json') @{ arguments=$run.arguments; processId=$run.process.Id; exitCode=$run.process.ExitCode }
    if ($run.process.ExitCode -and -not $allowFailure) { throw "CLI failed; retained response: $stem.stdout.json" }
    return ($stdout | ConvertFrom-Json -Depth 80)
}
function SourceProof {
    $files=@{}
    foreach ($f in (Git @('ls-files')).Split("`n")) { if ($f) { $files[$f]=(Get-FileHash -LiteralPath (Join-Path $Project $f) -Algorithm SHA256).Hash } }
    return @{ head=(Git @('rev-parse','HEAD')); index=(Get-FileHash -LiteralPath (Join-Path $Project '.git/index')).Hash;
        status=(Git @('status','--porcelain')); refs=(Git @('show-ref')); files=$files; worktrees=(Git @('worktree','list','--porcelain')) }
}
$prior=SourceProof; if ($prior.status) { throw 'Use a clean qualified source.' }
Save (Join-Path $OutputPath 'source-before.json') $prior
$service=Finish (StartCli @('storage','service','status')) (Join-Path $OutputPath 'service')
if ($service.data.callerElevated -or $service.data.state -ne 'running') { throw 'Use the existing running authority from an unelevated caller.' }
$candidate=@{}; foreach ($f in Get-ChildItem -LiteralPath (Split-Path $cli) -File | Where-Object { $_.Extension -in '.dll','.json' }) { $candidate[$f.Name]=(Get-FileHash -LiteralPath $f.FullName).Hash }
Save (Join-Path $OutputPath 'candidate.json') $candidate
$results=[Collections.Generic.List[object]]::new()
foreach ($point in $Boundaries) {
    $trial=Join-Path $OutputPath $point; New-Item -ItemType Directory -Path $trial | Out-Null
    $key='nr02-'+[Guid]::NewGuid().ToString('N'); $signal=Join-Path $trial 'signal.json'; $input=Join-Path $trial 'request.json'
    Save $input @{version=1;project=$Project;home=$HomePath;key=$key;baseReference=$BaseReference;boundary=$point;signalPath=$signal}
    $args=@('workspace','ensure','--project',$Project,'--home',$HomePath,'--key',$key,'--base',$BaseReference,'--storage','ntfs-snapshot')
    $probe=StartCli @('preparation-probe','--input',$input); $started=[Diagnostics.Stopwatch]::StartNew()
    while (-not (Test-Path -LiteralPath $signal) -and -not $probe.process.HasExited -and $started.Elapsed.TotalSeconds -lt 180) { Start-Sleep -Milliseconds 50 }
    if (-not (Test-Path -LiteralPath $signal)) { Finish $probe (Join-Path $trial 'unreached') | Out-Null; throw "Boundary not reached: $point" }
    $proof=Get-Content -LiteralPath $signal -Raw | ConvertFrom-Json
    if ($proof.processId -ne $probe.process.Id -or $proof.processStartTimeUtcTicks -ne $probe.process.StartTime.ToUniversalTime().Ticks) { throw 'Probe process identity differs; do not terminate it.' }
    $probe.process.Kill(); $probe.process.WaitForExit()
    [IO.File]::WriteAllText((Join-Path $trial 'interrupted.stdout.txt'), $probe.stdout.GetAwaiter().GetResult())
    [IO.File]::WriteAllText((Join-Path $trial 'interrupted.stderr.txt'), $probe.stderr.GetAwaiter().GetResult())
    $control=Join-Path $HomePath ('records/'+$proof.workspaceId)
    # Preserve owned pre-reconciliation control records, excluding payload/runners.
    foreach ($f in @('workspace.json','git-overlay.json','snapshot/snapshot.json','delta/identity.json','overlay/provider-launch-intent.json','overlay/provider-launch-receipt.json')) {
        $path=Join-Path $control $f
        if (Test-Path -LiteralPath $path) { Copy-Item -LiteralPath $path -Destination (Join-Path $trial ('interrupted-'+$f.Replace('/','-'))) }
    }
    $one=StartCli $args; $two=StartCli $args
    $a=Finish $one (Join-Path $trial 'resume-a') $true; $b=Finish $two (Join-Path $trial 'resume-b') $true
    if ($a.error.code -eq 'WorkspaceBusy') { $a=Finish (StartCli $args) (Join-Path $trial 'resume-a-retry') }
    if ($b.error.code -eq 'WorkspaceBusy') { $b=Finish (StartCli $args) (Join-Path $trial 'resume-b-retry') }
    if ($a.error -or $b.error) { throw 'A retained resume response failed; preserve this task.' }
    $w=$a.data.workspace
    if ($w.id -ne $proof.workspaceId -or $b.data.workspace.id -ne $w.id -or $w.phase -ne 'ready' -or $b.data.workspace.phase -ne 'ready' -or $w.snapshot.reference.id -ne $b.data.workspace.snapshot.reference.id) { throw 'Resume identity/readiness differs.' }
    if ($proof.snapshotId -and $proof.snapshotId -ne $w.snapshot.reference.id) { throw 'Snapshot was recaptured.' }
    if ($w.snapshot.mount.providerProcessId -ne $b.data.workspace.snapshot.mount.providerProcessId) { throw 'Duplicate provider generation.' }
    # The response boundary also discards an ordinary completed response and
    # replays the same input. Other trials already prove same-key concurrent
    # replay; repeating this separate response experiment adds no new boundary.
    $responseLossChecked=$point -eq 'response.before-publish'
    if ($responseLossChecked) {
        $drop=Finish (StartCli $args) (Join-Path $trial 'response-loss'); $drop=$null
        $replay=Finish (StartCli $args) (Join-Path $trial 'response-replay')
        if ($replay.data.workspace.snapshot.reference.id -ne $w.snapshot.reference.id) { throw 'Response replay recaptured.' }
    }
    $conflictArgs=$args.Clone(); $conflictArgs[9]='main'
    $conflict=StartCli $conflictArgs
    $conflict.process.WaitForExit(); $conflictText=$conflict.stdout.GetAwaiter().GetResult()
    [IO.File]::WriteAllText((Join-Path $trial 'conflict.json'),$conflictText)
    if (($conflictText | ConvertFrom-Json).error.code -ne 'RequestKeyConflict') { throw 'Changed request not refused.' }
    $release=Finish (StartCli @('workspace','release','--id',$w.id,'--home',$HomePath)) (Join-Path $trial 'release')
    if ($release.data.workspace.phase -ne 'released' -or (Test-Path -LiteralPath (Join-Path $HomePath ('workspaces/'+$w.id))) -or (Test-Path -LiteralPath (Join-Path $control 'delta'))) { throw 'Owned resources remain; retain this run.' }
    $saved=Get-Content -LiteralPath (Join-Path $control 'workspace.json') -Raw | ConvertFrom-Json -Depth 80
    if ($saved.snapshot.observation.snapshotAvailable -ne $false -or $saved.snapshot.observation.providerAlive -or $saved.snapshot.observation.mounted -or $saved.snapshot.observation.openHandles -ne 0 -or $saved.snapshot.observation.workingSetBytes -ne 0 -or $saved.snapshot.observation.privateBytes -ne 0) { throw 'Unknown/nonzero cleanup observation.' }
    Git @('cat-file','-e',($saved.snapshot.result.revision+'^{tree}')) | Out-Null
    $result=@{boundary=$point;workspaceId=$w.id;snapshotId=$w.snapshot.reference.id;resultReference=$saved.snapshot.result.resultReference;resultRevision=$saved.snapshot.result.revision;phase='released';responseLossChecked=$responseLossChecked;elapsedSeconds=$started.Elapsed.TotalSeconds}
    $results.Add($result); Save (Join-Path $OutputPath 'results.json') @($results.ToArray())
    Write-Output "$point : ready, same identity, concurrent retry, preserved and released"
}
$after=SourceProof; Save (Join-Path $OutputPath 'source-after.json') $after
if ($prior.head -ne $after.head -or $prior.index -ne $after.index -or $after.status -or ($prior.files | ConvertTo-Json -Compress) -ne ($after.files | ConvertTo-Json -Compress)) { throw 'Original source/index changed.' }
foreach ($ref in $prior.refs.Split("`n")) { if ($ref -and $ref -notin $after.refs.Split("`n")) { throw 'Prior result/reference changed.' } }
if ($prior.worktrees -ne $after.worktrees) { throw 'Git registrations remain or another worktree changed.' }
Save (Join-Path $OutputPath 'summary.json') @{boundaries=@($results.ToArray());originalPreserved=$true;priorRefsPreserved=$true;worktreesPreserved=$true;callerElevated=$false;qualification='preparation-command-boundaries-only'}
