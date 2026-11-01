param(
    [Parameter(Mandatory)][ValidateSet('seed','start','recover-start','resume-start','prepare-peer','bulk','selected','queries','large','final','observe')][string]$Stage,
    [Parameter(Mandatory)][ValidateSet('git','none','plastic')][string]$Source,
    [Parameter(Mandatory)][string]$FixtureRoot,
    [string]$CliAssembly = (Join-Path $PSScriptRoot '../../src/Splinter.Cli/bin/Release/net10.0/splinter.dll'),
    [string]$ProbeAssembly,
    [string]$ActorAssembly,
    [switch]$ResumeBulkAfterCliLoss,
    [string]$BaseReference,
    [int]$Entries = 100000,
    [int]$LargeGiB = 17,
    [ValidatePattern('^[a-z0-9-]*$')][string]$ResumeSuffix = ''
)
$ErrorActionPreference = 'Stop'
$FixtureRoot = [IO.Path]::GetFullPath($FixtureRoot)
$CliAssembly = [IO.Path]::GetFullPath($CliAssembly)
$sourcePath = Join-Path $FixtureRoot 'p'
$homePath = Join-Path $FixtureRoot 'h'
$evidence = Join-Path $FixtureRoot 'evidence'
$ownerPath = Join-Path $FixtureRoot 'fixture-owner.json'
$utf8 = [Text.UTF8Encoding]::new($false)
$fixed = [DateTime]::SpecifyKind([DateTime]::new(2020,2,3,4,5,6), [DateTimeKind]::Utc)
$roundTime = [DateTime]::SpecifyKind([DateTime]::new(2025,2,3,4,5,6), [DateTimeKind]::Utc)
function Write-Json([string]$Path, $Value) { [IO.File]::WriteAllText($Path, (ConvertTo-Json -InputObject $Value -Depth 30 -Compress), $utf8) }
function Payload([int]$Variant) {
    $bytes = [byte[]]::new(32)
    for ($j = 0; $j -lt $bytes.Length; $j++) { $bytes[$j] = [byte](($Variant * 17 + $j * 7) % 256) }
    return ,$bytes
}
function Relative([int]$Index) { return ('d{0:D2}/f{1:D7}.bin' -f ($Index % $script:directoryCount), $Index) }
function Hash([string]$Path) { return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash }
function Assert-Bytes([string]$Path, [byte[]]$Expected) {
    $actual = [IO.File]::ReadAllBytes($Path)
    if ([Convert]::ToHexString($actual) -cne [Convert]::ToHexString($Expected)) { throw "Unexpected payload at $Path" }
}
function Enter-Stage([string]$Name) {
    $path = Join-Path $evidence ('stage-' + $Name + '.intent.json')
    $stream = [IO.FileStream]::new($path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
    try {
        $bytes = $utf8.GetBytes((@{version=1; stage=$Name; project=$sourcePath; home=$homePath; source=$Source; cliAssembly=$CliAssembly; startedAt=[DateTimeOffset]::UtcNow.ToString('O')} | ConvertTo-Json -Compress))
        $stream.Write($bytes); $stream.Flush($true)
    } finally { $stream.Dispose() }
}
function Read-ObservedJson([string]$Path) {
    for ($attempt=0; ; $attempt++) {
        try {
            $stream=[IO.FileStream]::new($Path,[IO.FileMode]::Open,[IO.FileAccess]::Read,([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
            try { $reader=[IO.StreamReader]::new($stream); try { return ($reader.ReadToEnd() | ConvertFrom-Json -Depth 60) } finally { $reader.Dispose() } } finally { $stream.Dispose() }
        } catch [IO.IOException] { if($attempt -ge 4){throw}; Start-Sleep -Milliseconds 20 }
    }
}
function Provider-Gauges {
    foreach($area in @('records','project-bases','library-bases')) {
        $parent=Join-Path $homePath $area;if(-not [IO.Directory]::Exists($parent)){continue}
        foreach($directory in [IO.Directory]::EnumerateDirectories($parent)) {
            $path=Join-Path $directory 'overlay/state.json';if(-not [IO.File]::Exists($path)){continue}
            $state=Read-ObservedJson $path
            $alive=$false;$native=Get-Process -Id $state.processId -ErrorAction SilentlyContinue
            if($native){try{$alive=$native.StartTime.ToUniversalTime().Ticks -eq $state.processStartTimeUtcTicks}catch [InvalidOperationException]{$alive=$false}finally{$native.Dispose()}}
            [pscustomobject]@{statePath=$path;processId=$state.processId;startTicks=$state.processStartTimeUtcTicks;alive=$alive;phase=$state.phase
                metadataCacheBytes=$state.storage.metadataCacheBytes;peakMetadataCacheBytes=$state.storage.peakMetadataCacheBytes;payloadReadCacheBytes=$state.storage.readCacheBytes
                indexedNodes=$state.storage.indexedNodes;physicalJournalBytes=$state.storage.journalBytes;acceptedSealBytes=$state.storage.sealJournalBytes
                scope='Persisted provider observation, including its last lifetime peak; null means no gauge in that candidate. Not whole resident metadata or process RAM.'}
        }
    }
}
function Assert-TrialDeadline {
    foreach ($deadline in @($env:SPLINTER_VERIFY_DEADLINE_UTC, $env:SPLINTER_VERIFY_STAGE_DEADLINE_UTC)) {
        if ($deadline -and [DateTimeOffset]::UtcNow -ge [DateTimeOffset]::Parse($deadline)) {
            throw 'A fixed trial/stage deadline passed; preserve completed writes and start no later operation.'
        }
    }
}
function Invoke-Cli([string]$Name, [string[]]$Arguments, [switch]$AllowFailure) {
    Assert-TrialDeadline
    $stdout = Join-Path $evidence ($Name + '.json')
    $stderr = Join-Path $evidence ($Name + '.stderr.txt')
    if ([IO.File]::Exists($stdout)) { throw "Preserve existing operation evidence: $stdout; recover its exact request manually." }
    if (-not ('ScalabilityProcessSampler' -as [type])) { Add-Type -Path (Join-Path $PSScriptRoot 'ScalabilityProcessSampler.cs') }
    $sampler=[ScalabilityProcessSampler]::new();$rootState=Join-Path $evidence 'measured-process-roots.json'
    if(Test-Path -LiteralPath $rootState){foreach($prior in ([IO.File]::ReadAllText($rootState)|ConvertFrom-Json)){$sampler.Add([int]$prior.Id,[long]$prior.StartTicks)}}
    # A provider restarted through its saved authenticated request predates
    # this CLI process, so descendant discovery alone cannot include it.
    foreach ($provider in @(Provider-Gauges)) { if ($provider.alive) { $sampler.Add([int]$provider.processId,[long]$provider.startTicks) } }
    $info = [Diagnostics.ProcessStartInfo]::new('dotnet')
    $info.UseShellExecute = $false; $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true
    if ($ProbeAssembly) {
        $info.ArgumentList.Add([IO.Path]::GetFullPath($ProbeAssembly))
        $info.ArgumentList.Add($CliAssembly)
        $info.ArgumentList.Add((Join-Path $evidence ($Name + '.inside-metrics.json')))
    } else { $info.ArgumentList.Add($CliAssembly) }
    foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($info)
    $outFile = [IO.FileStream]::new($stdout, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
    $errFile = [IO.FileStream]::new($stderr, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
    $outCopy = $process.StandardOutput.BaseStream.CopyToAsync($outFile)
    $errCopy = $process.StandardError.BaseStream.CopyToAsync($errFile)
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $peakPrivate = 0L; $peakWorking = 0L; $peakHandles = 0; $samples = 0
    $startTicks = $process.StartTime.ToUniversalTime().Ticks
    $sampler.Add($process.Id,$startTicks)
    $ownedPrivate=0L;$ownedWorking=0L;$ownedHandles=0L;$ownedCount=0;$sampleErrors=[Collections.Generic.List[string]]::new()
    try {
        while (-not $process.WaitForExit(100)) {
            try {
                $process.Refresh(); $samples++
                $peakPrivate = [Math]::Max($peakPrivate, $process.PrivateMemorySize64)
                $peakWorking = [Math]::Max($peakWorking, $process.WorkingSet64)
                $peakHandles = [Math]::Max($peakHandles, $process.HandleCount)
            } catch [InvalidOperationException] { if(-not $process.HasExited){throw} }
            $group=$sampler.Sample();$ownedPrivate=[Math]::Max($ownedPrivate,$group.PrivateBytes);$ownedWorking=[Math]::Max($ownedWorking,$group.WorkingSetBytes);$ownedHandles=[Math]::Max($ownedHandles,$group.Handles);$ownedCount=[Math]::Max($ownedCount,$group.Processes.Length)
            foreach($errorText in $group.Errors){$sampleErrors.Add($errorText)}
        }
        [Threading.Tasks.Task]::WhenAll($outCopy, $errCopy).GetAwaiter().GetResult()
        $watch.Stop()
        Write-Json (Join-Path $evidence ($Name + '.process-metrics.json')) @{
            processId=$process.Id; startTimeUtcTicks=$startTicks; seconds=$watch.Elapsed.TotalSeconds; exitCode=$process.ExitCode
            sampledPeakPrivateBytes=$peakPrivate; sampledPeakWorkingSetBytes=$peakWorking; sampledPeakHandles=$peakHandles; samples=$samples
            sampledPeakOwnedPrivateBytes=$ownedPrivate;sampledPeakOwnedWorkingSetBytes=$ownedWorking;sampledPeakOwnedHandles=$ownedHandles;sampledPeakOwnedProcessCount=$ownedCount;samplingErrors=$sampleErrors.ToArray()
            scope='100 ms sampling: exact CLI plus tracked PID/start-verified persistent providers and descendants. Group sums exclude measurement host; sampled maxima are lower bounds, not an OS peak. Provider metadata-cache gauges are recorded separately.'
        }
        $last=$sampler.Sample();Write-Json $rootState @($sampler.Identities());Write-Json (Join-Path $evidence ($Name+'.owned-processes-after.json')) $last
    } finally { $outFile.Dispose(); $errFile.Dispose(); $process.Dispose() }
    $response = [IO.File]::ReadAllText($stdout) | ConvertFrom-Json -Depth 100
    Write-Json (Join-Path $evidence ($Name+'.provider-cache-gauges.json')) @(Provider-Gauges)
    if ($ProbeAssembly) {
        $timing = Get-Content -LiteralPath (Join-Path $evidence ($Name + '.inside-metrics.json')) -Raw | ConvertFrom-Json
        if ($timing.cancellationRequested -or $timing.deadlineExceeded) { throw "$Name reached its fixed deadline; preserve state and start no later stage." }
    }
    if (-not $AllowFailure -and $response.status -ne 'Succeeded') { throw "$Name failed: $($response.error.code); retain all identity/evidence and recover the same request." }
    return $response
}
function Workspace([string]$Name) {
    return ([IO.File]::ReadAllText((Join-Path $evidence ('ensure-' + $Name + '.json'))) | ConvertFrom-Json -Depth 100).data.workspace
}
function Assert-Peer {
    $peer = Workspace 'b'
    Assert-Bytes (Join-Path $peer.path (Relative 0)) (Payload 0)
    $old = ([IO.File]::ReadAllText((Join-Path $evidence 'ensure-b.json')) | ConvertFrom-Json -Depth 100).data.workspace.snapshot.mount
    $process = Get-Process -Id $old.providerProcessId -ErrorAction Stop
    if ($process.StartTime.ToUniversalTime().Ticks -ne $old.providerStartTimeUtcTicks) { throw 'Peer provider identity changed' }
}
function Capture([string]$Name, $Workspace) {
    return (Invoke-Cli $Name @('workspace','checkpoint','--id',$Workspace.id,'--request-key',$Name,'--message',$Name,'--home',$homePath)).data.resultGeneration.id
}
function Apply([string]$Name, $Workspace, [string]$Result, [string[]]$Paths = @()) {
    $select = [Collections.Generic.List[string]]::new()
    foreach ($path in $Paths) { $select.Add('--path'); $select.Add($path) }
    $plan = (Invoke-Cli ($Name + '-plan') (@('workspace','integrate','--id',$Workspace.id,'--target','original','--result',$Result,'--plan','--home',$homePath) + $select.ToArray())).data.originalApplyPlan
    if (($plan.conflictCount -gt 0) -or ($plan.conflicts.Count -gt 0)) { throw 'Unexpected original publication conflict' }
    Invoke-Cli $Name (@('workspace','integrate','--id',$Workspace.id,'--target','original','--result',$Result,'--intent-key',$Name,'--expected-plan-digest',$plan.digest,'--home',$homePath) + $select.ToArray()) | Out-Null
}
if ($Stage -eq 'seed') {
    if ($Entries -lt 16) { throw 'The fixed edit scenario needs at least 16 entries.' }
    if ([IO.Directory]::Exists($FixtureRoot) -or [IO.File]::Exists($FixtureRoot)) { throw 'Preserve an occupied fixture root.' }
    [IO.Directory]::CreateDirectory($FixtureRoot) | Out-Null
    [IO.Directory]::CreateDirectory($sourcePath) | Out-Null
    [IO.Directory]::CreateDirectory($evidence) | Out-Null
    $script:directoryCount = [Math]::Min(10, [Math]::Max(1, [int][Math]::Floor($Entries / 1000)))
    $fileCount = $Entries - $directoryCount
    Write-Json $ownerPath @{version=1; purpose='SCAL-07 editorless / SCAL-08 fixed-input cost fixture'; source=$Source; project=$sourcePath; home=$homePath; entries=$Entries; files=$fileCount; directories=$directoryCount; payloadVariants=16; bytesPerFile=32; seedTime=$fixed.ToString('O'); phase='seeding'}
    for ($d=0; $d -lt $directoryCount; $d++) { [IO.Directory]::CreateDirectory((Join-Path $sourcePath ('d{0:D2}' -f $d))) | Out-Null }
    for ($i=0; $i -lt $fileCount; $i++) {
        $path = Join-Path $sourcePath (Relative $i)
        [IO.File]::WriteAllBytes($path, (Payload ($i % 16)))
        [IO.File]::SetCreationTimeUtc($path, $fixed); [IO.File]::SetLastAccessTimeUtc($path, $fixed); [IO.File]::SetLastWriteTimeUtc($path, $fixed)
        [IO.File]::SetAttributes($path, [IO.FileAttributes]::Archive)
    }
    foreach ($path in [IO.Directory]::EnumerateDirectories($sourcePath)) {
        [IO.Directory]::SetCreationTimeUtc($path,$fixed); [IO.Directory]::SetLastAccessTimeUtc($path,$fixed); [IO.Directory]::SetLastWriteTimeUtc($path,$fixed)
    }
    if ($Source -eq 'git') {
        $hooks = Join-Path $FixtureRoot 'empty-hooks'; [IO.Directory]::CreateDirectory($hooks) | Out-Null
        & git -C $sourcePath init --initial-branch development | Out-Null
        & git -C $sourcePath config core.autocrlf false
        & git -C $sourcePath config core.hooksPath $hooks
        & git -C $sourcePath add --all
        $oldAuthor = $env:GIT_AUTHOR_DATE; $oldCommitter = $env:GIT_COMMITTER_DATE
        try {
            $env:GIT_AUTHOR_DATE='2000-01-01T00:00:00Z'; $env:GIT_COMMITTER_DATE=$env:GIT_AUTHOR_DATE
            & git -C $sourcePath -c user.name=Splinter-Verification -c user.email=verification@invalid -c commit.gpgsign=false commit -m 'Fixed SCAL editorless input' | Out-Null
            if ($LASTEXITCODE -ne 0) { throw 'The owned Git fixture seed commit failed.' }
        } finally { $env:GIT_AUTHOR_DATE=$oldAuthor; $env:GIT_COMMITTER_DATE=$oldCommitter }
    }
    [IO.Directory]::SetCreationTimeUtc($sourcePath,$fixed); [IO.Directory]::SetLastAccessTimeUtc($sourcePath,$fixed); [IO.Directory]::SetLastWriteTimeUtc($sourcePath,$fixed)
    $owner = [IO.File]::ReadAllText($ownerPath) | ConvertFrom-Json
    $owner.phase='seeded'; Write-Json $ownerPath $owner
    Write-Output 'Seeded an owned fixture. Plastic registration/check-in is a separate explicit fixture-preparation step.'
    exit 0
}
if (-not [IO.File]::Exists($ownerPath)) { throw 'The exact fixture ownership marker is required.' }
$owner = [IO.File]::ReadAllText($ownerPath) | ConvertFrom-Json
if ($owner.version -ne 1 -or $owner.source -ne $Source -or $owner.project -ne $sourcePath -or $owner.home -ne $homePath -or $owner.phase -ne 'seeded') { throw 'Fixture owner/scope mismatch' }
$script:directoryCount = [int]$owner.directories; $fileCount = [int]$owner.files
if ($Stage -eq 'recover-start') {
    Enter-Stage 'recover-start'
    if($Source -ne 'git'){throw 'This recovery is the recorded first Native Git initialization diagnosis only.'}
    $failed=Join-Path $evidence 'ensure-a.json';$retained=Join-Path $evidence 'ensure-a-initialization-failed.json';$recovered=Join-Path $evidence 'ensure-a-recovery-native-init.json'
    $failure=[IO.File]::ReadAllText($failed)|ConvertFrom-Json -Depth 100
    $recovery=[IO.File]::ReadAllText($recovered)|ConvertFrom-Json -Depth 100
    if($failure.error.code -ne 'JournalSegmentCorrupt' -or $recovery.status -ne 'Succeeded' -or $recovery.data.workspace.id -ne '48dbbaff824a9c07c427f5ed' -or $recovery.data.workspace.projectPath -ne $sourcePath -or [IO.File]::Exists($retained)){throw 'Not the exact recorded initialization recovery.'}
    [IO.File]::Copy($failed,$retained,$false);[IO.File]::Copy($recovered,$failed,$true)
    $BaseReference=(& git -C $sourcePath rev-parse HEAD).Trim()
    Invoke-Cli 'ensure-b' @('workspace','ensure','--project',$sourcePath,'--key','scal-b','--source','git','--application','none','--home',$homePath,'--storage','git-snapshot','--base',$BaseReference)|Out-Null
    Assert-Peer
    Write-Json (Join-Path $evidence 'initialization-recovery-diagnosis.json') @{scope='Same task/key/home/snapshot recovery and full functional continuation; excluded from matched performance totals.';failedResponse=$retained;recoveredResponse=$recovered;id=$recovery.data.workspace.id;cli=$CliAssembly}
    exit 0
}
if ($Stage -eq 'resume-start') {
    # Controlled continuation of the three already recorded 100k Native runs.
    # Preserve the failed/lost original response and use exactly the same key,
    # home, basis and fixed revision; never prepare a replacement input.
    $expected = @{git='a60f39138954e6f3259815a1';none='790af50759504bf2be12d8ee';plastic='4a780ba1a45cf042dece1460'}[$Source]
    $record = [IO.File]::ReadAllText((Join-Path $homePath "records/$expected/workspace.json")) | ConvertFrom-Json -Depth 100
    if ($owner.entries -ne 100000 -or $record.id -ne $expected -or $record.projectPath -ne $sourcePath -or $record.phase -ne 'creating') { throw 'Not the exact interrupted 100k start.' }
    $suffix = if ($ResumeSuffix) { '-' + $ResumeSuffix } else { '' }
    Enter-Stage ('resume-start' + $suffix)
    $arguments = @('workspace','ensure','--project',$sourcePath,'--key','scal-a','--source',$Source,'--application','none','--home',$homePath)
    if ($Source -eq 'none') {
        $basis = ([IO.File]::ReadAllText((Join-Path $evidence 'project-prepare.json')) | ConvertFrom-Json -Depth 100).data.basis.basis.id
        $arguments += @('--storage','project-snapshot','--project-basis',$basis)
    } else { $BaseReference = $record.baseReference; $arguments += @('--storage',($Source+'-snapshot'),'--base',$BaseReference) }
    $responseName = 'ensure-a-same-request-resumed' + $suffix
    $resumed = Invoke-Cli $responseName $arguments
    if ($resumed.data.workspace.id -ne $expected) { throw 'Resumed workspace identity changed.' }
    $old = Join-Path $evidence 'ensure-a.json'; $retained = Join-Path $evidence ('ensure-a-before-controlled-resume' + $suffix + '.json')
    [IO.File]::Copy($old,$retained,$false)
    [IO.File]::Copy((Join-Path $evidence ($responseName + '.json')),$old,$true)
    $peerArguments = @('workspace','ensure','--project',$sourcePath,'--key','scal-b','--source',$Source,'--application','none','--home',$homePath)
    $peerArguments += if ($Source -eq 'none') { @('--storage','project-snapshot','--project-basis',$basis) } else { @('--storage',($Source+'-snapshot'),'--base',$BaseReference) }
    Invoke-Cli 'ensure-b' $peerArguments | Out-Null
    Assert-Peer
    Write-Json (Join-Path $evidence ('controlled-start-continuation' + $suffix + '.json')) @{id=$expected;priorResponse=$retained;resumedResponse=($responseName+'.json');scope='Same identity after recorded timeout/client interruption and normal provider restart; functional qualification only, not matched timing.'}
    exit 0
}
if ($Stage -eq 'prepare-peer') {
    if ($owner.entries -ne 100000) { throw 'Parallel peer preparation is only for the three fixed functional inputs.' }
    $peerSuffix = if ($ResumeSuffix) { $ResumeSuffix } else { 'r12' }
    Enter-Stage ('prepare-peer-' + $peerSuffix)
    $arguments = @('workspace','ensure','--project',$sourcePath,'--key','scal-b','--source',$Source,'--application','none','--home',$homePath)
    if ($Source -eq 'none') {
        $basis = ([IO.File]::ReadAllText((Join-Path $evidence 'project-prepare.json')) | ConvertFrom-Json -Depth 100).data.basis.basis.id
        $arguments += @('--storage','project-snapshot','--project-basis',$basis)
    } else {
        $expected = @{git='a60f39138954e6f3259815a1';plastic='4a780ba1a45cf042dece1460'}[$Source]
        $record = [IO.File]::ReadAllText((Join-Path $homePath "records/$expected/workspace.json")) | ConvertFrom-Json -Depth 100
        $arguments += @('--storage',($Source+'-snapshot'),'--base',$record.baseReference)
    }
    # Do not alias ensure-b.json: the already-running start driver will use
    # that name for its same-key revalidation. Both commands target one B.
    Invoke-Cli ('ensure-b-initial-' + $peerSuffix) $arguments | Out-Null
    exit 0
}
if ($Stage -eq 'start') {
    Enter-Stage 'start'
    foreach ($name in @('workspace.ensure','workspace.checkpoint','workspace.integrate','workspace.release')) { Invoke-Cli ('schema-' + $name) @('schema',$name) | Out-Null }
    $capabilities = Invoke-Cli 'capabilities' @('capabilities')
    if ($capabilities.data.commands.id -contains 'workspace.read') { Invoke-Cli 'schema-workspace.read' @('schema','workspace.read') | Out-Null }
    if ($Source -eq 'none') {
        Invoke-Cli 'schema-project.prepare' @('schema','project.prepare') | Out-Null
        Invoke-Cli 'schema-project.restore' @('schema','project.restore') | Out-Null
        $basis = (Invoke-Cli 'project-prepare' @('project','prepare','--project',$sourcePath,'--key','scal-project','--application','none','--home',$homePath)).data.basis.basis.id
    } elseif ($Source -eq 'git') {
        $BaseReference = (& git -C $sourcePath rev-parse HEAD).Trim()
        Write-Json (Join-Path $evidence 'git-original-before.json') @{head=$BaseReference; index=(Hash (Join-Path $sourcePath '.git/index')); branch=(& git -C $sourcePath symbolic-ref HEAD).Trim()}
    } elseif (-not $BaseReference) { throw 'Supply the exact separately prepared Plastic changeset@repository@server.' }
    foreach ($letter in @('a','b')) {
        $arguments = @('workspace','ensure','--project',$sourcePath,'--key',('scal-' + $letter),'--source',$Source,'--application','none','--home',$homePath)
        $arguments += if ($Source -eq 'none') { @('--storage','project-snapshot','--project-basis',$basis) } else { @('--storage',($Source + '-snapshot'),'--base',$BaseReference) }
        Invoke-Cli ('ensure-' + $letter) $arguments | Out-Null
    }
    Assert-Peer; exit 0
}
if ($Stage -eq 'bulk') {
    if ($Source -eq 'git' -and $owner.entries -eq 100000 -and
        ([string]::IsNullOrWhiteSpace($ActorAssembly) -or -not [IO.File]::Exists([IO.Path]::GetFullPath($ActorAssembly)))) {
        throw 'Supply the independently frozen qualified interruption ActorAssembly before the fixed Git bulk stage.'
    }
    if ($ResumeBulkAfterCliLoss) {
        if ($Source -ne 'git' -or $owner.entries -ne 100000) { throw 'Only the owned fixed Git CLI-loss experiment can continue this bulk body.' }
        $stagePath=Join-Path $evidence 'stage-bulk.intent.json'
        $stage=Get-Content -LiteralPath $stagePath -Raw|ConvertFrom-Json
        $completedPath=Join-Path $evidence 'native-cli-loss-complete.json'
        $completed=Get-Content -LiteralPath $completedPath -Raw|ConvertFrom-Json
        $fixedCapture=Get-Content (Join-Path $evidence 'native-cli-loss-fixed.json') -Raw|ConvertFrom-Json -Depth 40
        $aRecord=Get-Content (Join-Path $homePath ('records/'+$completed.workspace+'/workspace.json')) -Raw|ConvertFrom-Json -Depth 80
        if ($stage.stage -ne 'bulk' -or [IO.Path]::GetFullPath($stage.cliAssembly) -ne $CliAssembly -or
            $completed.workspace -ne $fixedCapture.workspace -or $completed.result -ne $fixedCapture.captureId -or
            $completed.captureDigest -ne $fixedCapture.digest -or -not $completed.providerLifetimePreserved -or
            -not $completed.laterWorkspaceEditPreserved -or $aRecord.captureIntent -or $aRecord.phase -ne 'ready' -or
            $aRecord.resultHistory.count -ne 1 -or $aRecord.originalApplyHistory.count -ne 0 -or
            (Test-Path (Join-Path $evidence 'bulk-edit-metrics.json')) -or (Test-Path (Join-Path $evidence 'capture-1.json'))) {
            throw 'Preserve the original bulk request; its exact CLI-loss-only continuation proof does not match.'
        }
        Enter-Stage 'bulk-after-cli-loss-continuation'
    } else { Enter-Stage 'bulk' }
    $a = Workspace 'a'; Assert-Peer
    if($Source -eq 'git' -and $owner.entries -eq 100000 -and -not $ResumeBulkAfterCliLoss){
        & "$PSScriptRoot/scalable-native-cli-interruption.ps1" -FixtureRoot $FixtureRoot -CliAssembly $CliAssembly -ActorAssembly ([IO.Path]::GetFullPath($ActorAssembly))
        if($LASTEXITCODE -ne 0){throw 'Preserve the exact actual CLI interruption request before continuing bulk edits'}
        Assert-Peer
    }
    $editWatch = [Diagnostics.Stopwatch]::StartNew()
    $editWorkers = 1
    if ($owner.entries -eq 100000) {
        if (-not ('ScalabilityBulkWrites' -as [type])) { Add-Type -Path (Join-Path $PSScriptRoot 'ScalabilityBulkWrites.cs') }
        $editWorkers = 8
        if ([ScalabilityBulkWrites]::Run($a.path,$fileCount,$script:directoryCount,$roundTime) -ne $fileCount) { throw 'The functional bulk write did not drain every file.' }
    } else {
        for ($i=0; $i -lt $fileCount; $i++) {
            Assert-TrialDeadline
            $path = Join-Path $a.path (Relative $i)
            [IO.File]::WriteAllBytes($path, (Payload (($i + 1) % 16)))
            [IO.File]::SetLastWriteTimeUtc($path, $roundTime)
        }
    }
    $editWatch.Stop()
    Write-Json (Join-Path $evidence 'bulk-edit-metrics.json') @{seconds=$editWatch.Elapsed.TotalSeconds; files=$fileCount; workers=$editWorkers; bytesWritten=32L*$fileCount; scope='Whole-file 32-byte fixture writes plus fixed modification times, outside CLI command timings. Eight drained workers only for 100k functional inputs; matched 64-entry ABBA retains sequential writes.'}
    [IO.File]::WriteAllBytes((Join-Path $sourcePath 'original-only.bin'), (Payload 8))
    $result = Capture 'capture-1' $a
    Apply 'apply-1' $a $result
    Assert-Bytes (Join-Path $sourcePath (Relative 0)) (Payload 1)
    Assert-Peer; Invoke-Cli 'status-after-bulk' @('workspace','status','--id',$a.id,'--home',$homePath) | Out-Null
    exit 0
}
if ($Stage -eq 'large') {
    if ($Source -ne 'none' -or $LargeGiB -lt 17) { throw 'This actual-file boundary case requires the owned None input and more than the old 16 GiB journal budget.' }
    Enter-Stage 'large'; $a = Workspace 'a'; Assert-Peer
    $path=Join-Path $a.path 'Large.bin'; if(Test-Path -LiteralPath $path){throw 'Preserve an occupied large-file test path.'}
    if(-not ('ScalLargeFile' -as [type])) {
        Add-Type -TypeDefinition @'
using System;using System.Diagnostics;using System.IO;using System.Security.Cryptography;
public static class ScalLargeFile {
 public static object Write(string path,long length) {
  byte[] buffer=new byte[1024*1024];for(int i=0;i<buffer.Length;i++)buffer[i]=(byte)(i*17+3);
  using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
  using var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read,1024*1024,FileOptions.SequentialScan);
  var watch=Stopwatch.StartNew();long at=0;
  while(at<length){BitConverter.TryWriteBytes(buffer.AsSpan(0,8),at);int count=(int)Math.Min(buffer.Length,length-at);output.Write(buffer,0,count);hash.AppendData(buffer,0,count);at+=count;}
  output.Flush(true);return new {length,sha256=Convert.ToHexStringLower(hash.GetHashAndReset()),seconds=watch.Elapsed.TotalSeconds,bufferBytes=buffer.Length};
 }
 public static object ReadBoundary(string path,long expectedLength) {
  using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,4096,FileOptions.RandomAccess);
  if(input.Length!=expectedLength)throw new IOException("The actual large file length changed before capture.");
  long offset=checked(expectedLength-1024*1024);Span<byte> tag=stackalloc byte[8];
  input.Seek(offset,SeekOrigin.Begin);input.ReadExactly(tag);
  long actual=System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(tag);
  if(actual!=offset)throw new IOException("The actual 64-bit last-chunk offset tag differs.");
  input.Seek(expectedLength-1,SeekOrigin.Begin);int last=input.ReadByte();
  int expectedLast=unchecked((byte)(((1024*1024-1)*17)+3));
  if(last!=expectedLast)throw new IOException("The actual last byte differs.");
  return new {length=input.Length,seekOffset=offset,storedOffset=actual,lastByteOffset=expectedLength-1,lastByte=last,expectedLastByte=expectedLast,offsetAbove4GiB=offset>uint.MaxValue,scope="Actual file seeks before capture; numeric length/offset distinguish the large-file boundary from the small harness smoke check, modeled offsets and full-file hashes."};
 }
}
'@
    }
    $written=[ScalLargeFile]::Write($path,([long]$LargeGiB*1024*1024*1024))
    Write-Json (Join-Path $evidence 'large-actual-write.json') $written
    Write-Json (Join-Path $evidence 'large-before-capture-offset-proof.json') ([ScalLargeFile]::ReadBoundary($path,$written.length))
    $statePath=Join-Path $homePath ('records/'+$a.id+'/overlay/state.json')
    $writeFinishedAt=[DateTimeOffset]::UtcNow;$stateDeadline=$writeFinishedAt.AddSeconds(60);$freshState=$null
    do {
        $observed=[IO.File]::ReadAllText($statePath)|ConvertFrom-Json -Depth 60
        if($observed.id -ne $a.id -or $observed.processId -ne $a.snapshot.mount.providerProcessId -or
            $observed.processStartTimeUtcTicks -ne $a.snapshot.mount.providerStartTimeUtcTicks -or
            [IO.Path]::GetFullPath($observed.mountPath) -ne [IO.Path]::GetFullPath($a.path)) {throw 'The large-file provider identity changed before capture; preserve the accepted writes.'}
        $provider=Get-Process -Id $observed.processId -ErrorAction Stop
        try {if($provider.StartTime.ToUniversalTime().Ticks -ne $observed.processStartTimeUtcTicks){throw 'The large-file provider PID was reused.'}}finally{$provider.Dispose()}
        if([DateTimeOffset]::Parse($observed.observedAt) -gt $writeFinishedAt -and
            [long]$observed.storage.payloadBytes -ge [long]$written.length -and [long]$observed.storage.journalBytes -gt 16L*1024*1024*1024){$freshState=$observed;break}
        Start-Sleep -Milliseconds 250
    } while([DateTimeOffset]::UtcNow -lt $stateDeadline)
    if(-not $freshState){throw 'No fresh exact-provider observation proved the actual old journal budget crossing; preserve this large-stage request without recapturing.'}
    Write-Json (Join-Path $evidence 'large-before-capture-provider-proof.json') @{
        observedAt=$freshState.observedAt;writeFinishedAt=$writeFinishedAt.ToString('O');id=$freshState.id;processId=$freshState.processId
        processStartTimeUtcTicks=$freshState.processStartTimeUtcTicks;mountPath=$freshState.mountPath;deltaPath=$freshState.deltaPath
        storage=$freshState.storage;formerJournalBudgetBytes=16L*1024*1024*1024;actualWrittenBytes=$written.length
        scope='Fresh exact PID/start/mount observation after actual durable writer close, before capture; physical file allocation is inventoried separately. No capture or source replay was requested to obtain these gauges.'
    }
    & "$PSScriptRoot/scalable-storage-allocation.ps1" -Roots @($freshState.deltaPath) -Output (Join-Path $evidence 'large-before-capture-allocation.json')
    if($LASTEXITCODE -ne 0){throw 'Preserve the large-stage request after its pre-capture allocation observation failed.'}
    $result=Capture 'capture-large' $a;Apply 'apply-large' $a $result @('Large.bin')
    $original=Join-Path $sourcePath 'Large.bin'
    if((Get-Item -LiteralPath $original).Length -ne $written.length -or (Hash $original).ToLowerInvariant() -ne $written.sha256){throw 'The actual large original payload differs.'}
    $edit=[IO.FileStream]::new($original,[IO.FileMode]::Open,[IO.FileAccess]::Write,[IO.FileShare]::Read);try{$edit.WriteByte(255);$edit.Flush($true)}finally{$edit.Dispose()}
    $plan=([IO.File]::ReadAllText((Join-Path $evidence 'apply-large-plan.json'))|ConvertFrom-Json -Depth 100).data.originalApplyPlan
    Invoke-Cli 'apply-large-completed-retry' @('workspace','integrate','--id',$a.id,'--target','original','--result',$result,'--intent-key','apply-large','--expected-plan-digest',$plan.digest,'--path','Large.bin','--home',$homePath)|Out-Null
    $read=[IO.FileStream]::new($original,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read);try{if($read.ReadByte() -ne 255){throw 'A completed publication retry overwrote the later original edit.'}}finally{$read.Dispose()}
    $read=[IO.FileStream]::new($path,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read);try{if($read.ReadByte() -ne 0){throw 'The workspace followed the mutable original instead of the immutable range.'}}finally{$read.Dispose()}
    Invoke-Cli 'capture-large-completed-retry' @('workspace','checkpoint','--id',$a.id,'--request-key','capture-large','--message','capture-large','--home',$homePath)|Out-Null
    Assert-Peer;exit 0
}
if ($Stage -eq 'selected') {
    Enter-Stage 'selected'
    Assert-TrialDeadline
    $a = Workspace 'a'; Assert-Peer
    [IO.File]::WriteAllBytes((Join-Path $a.path (Relative 0)), (Payload 7))
    [IO.File]::WriteAllBytes((Join-Path $a.path (Relative 1)), (Payload 6))
    [IO.File]::WriteAllBytes((Join-Path $a.path 'created.bin'), (Payload 5))
    [IO.File]::Delete((Join-Path $a.path (Relative 2)))
    [IO.File]::Move((Join-Path $a.path (Relative 3)), (Join-Path $a.path 'renamed.bin'))
    $result = Capture 'capture-2' $a
    Apply 'apply-2-selected' $a $result @((Relative 0),(Relative 1))
    Assert-Bytes (Join-Path $sourcePath (Relative 0)) (Payload 7)
    if ([IO.File]::Exists((Join-Path $sourcePath 'created.bin'))) { throw 'An unselected new path was published.' }
    Apply 'apply-2-remainder' $a $result
    if ([IO.File]::Exists((Join-Path $sourcePath (Relative 2)))) { throw 'The remaining deletion was not published.' }
    Assert-Bytes (Join-Path $sourcePath 'created.bin') (Payload 5)
    Assert-Peer; exit 0
}
if ($Stage -eq 'queries') {
    Enter-Stage 'queries'; $a=Workspace 'a'; Assert-Peer
    $firstResult=([IO.File]::ReadAllText((Join-Path $evidence 'capture-1.json'))|ConvertFrom-Json -Depth 100).data.resultGeneration.id
    $secondResult=([IO.File]::ReadAllText((Join-Path $evidence 'capture-2.json'))|ConvertFrom-Json -Depth 100).data.resultGeneration.id
    $first=(Invoke-Cli 'query-files-first' @('workspace','read','--id',$a.id,'--collection','files','--result',$firstResult,'--page-size','2','--home',$homePath)).data.page
    if($first.count -ne $owner.entries -or $first.entries.Count -ne 2 -or -not $first.nextCursor){throw 'The native result page has an invalid count or cursor.'}
    $next=(Invoke-Cli 'query-files-next' @('workspace','read','--id',$a.id,'--collection','files','--result',$firstResult,'--cursor',$first.nextCursor,'--page-size','2','--home',$homePath)).data.page
    if($next.root -ne $first.root -or $next.entries.Count -ne 2){throw 'The native immutable page cursor changed roots.'}
    $wrong=Invoke-Cli 'query-files-wrong-root-refused' @('workspace','read','--id',$a.id,'--collection','files','--result',$secondResult,'--cursor',$first.nextCursor,'--page-size','2','--home',$homePath) -AllowFailure
    if($wrong.error.code -ne 'CursorInvalid'){throw 'A cursor for a different result root was not refused.'}
    foreach($collection in @('results','applies','applied-paths')){
        $page=(Invoke-Cli ('query-'+$collection) @('workspace','read','--id',$a.id,'--collection',$collection,'--page-size','1','--home',$homePath)).data.page
        if($page.count -lt 2 -or $page.entries.Count -ne 1 -or -not $page.nextCursor){throw ('Native '+$collection+' paging was incomplete.')}
    }
    $selected=([IO.File]::ReadAllText((Join-Path $evidence 'apply-2-selected-plan.json'))|ConvertFrom-Json -Depth 100).data.originalApplyPlan
    foreach($collection in @('plan','selection')){
        $page=(Invoke-Cli ('query-'+$collection) @('workspace','read','--id',$a.id,'--collection',$collection,'--plan-id',$selected.digest,'--page-size','1','--home',$homePath)).data.page
        if(($collection -eq 'selection' -and $page.count -ne 2) -or $page.count -lt 2 -or $page.entries.Count -ne 1 -or -not $page.nextCursor){throw ('Native selected '+$collection+' paging differs.')}
    }
    Invoke-Cli 'query-maintain-all' @('workspace','maintain','--id',$a.id,'--request-key','native-page-maintenance','--scope','all','--home',$homePath)|Out-Null
    Invoke-Cli 'query-maintain-all-retry' @('workspace','maintain','--id',$a.id,'--request-key','native-page-maintenance','--scope','all','--home',$homePath)|Out-Null
    $after=(Invoke-Cli 'query-files-after-maintenance' @('workspace','read','--id',$a.id,'--collection','files','--result',$firstResult,'--cursor',$first.nextCursor,'--page-size','2','--home',$homePath)).data.page
    if($after.root -ne $next.root -or ($after.entries|ConvertTo-Json -Compress -Depth 20) -cne ($next.entries|ConvertTo-Json -Compress -Depth 20)){throw 'Maintenance invalidated an immutable result cursor.'}
    foreach($scope in @('content','content-references','ranges','journal-packing','journal-indices','read-only-replay')){Invoke-Cli ('query-deferred-'+$scope) @('workspace','read','--id',$a.id,'--collection','deferred-reclamation','--store-scope',$scope,'--page-size','2','--home',$homePath)|Out-Null}
    Assert-Peer;exit 0
}
if ($Stage -eq 'final') {
    Enter-Stage 'final'
    Assert-TrialDeadline
    $a = Workspace 'a'; $b = Workspace 'b'; Assert-Peer
    [IO.File]::WriteAllBytes((Join-Path $a.path (Relative 0)), (Payload 0))
    $result = Capture 'capture-3' $a
    Apply 'apply-3' $a $result
    Assert-Bytes (Join-Path $sourcePath (Relative 0)) (Payload 0)
    Invoke-Cli 'release-a' @('workspace','release','--id',$a.id,'--home',$homePath) | Out-Null
    Assert-Peer
    [IO.File]::WriteAllBytes((Join-Path $b.path 'peer-after-a.bin'), (Payload 9))
    Capture 'capture-b' $b | Out-Null
    Invoke-Cli 'release-b' @('workspace','release','--id',$b.id,'--home',$homePath) | Out-Null
    if ([IO.Directory]::Exists($a.path) -or [IO.Directory]::Exists($b.path)) { throw 'An owned workspace survived reclamation.' }
    if ($Source -eq 'none') {
        $basis = ([IO.File]::ReadAllText((Join-Path $evidence 'project-prepare.json')) | ConvertFrom-Json -Depth 100).data.basis.basis.id
        Invoke-Cli 'project-restore' @('project','restore','--id',$basis,'--home',$homePath) | Out-Null
    }
    if ($Source -eq 'git') {
        $before = [IO.File]::ReadAllText((Join-Path $evidence 'git-original-before.json')) | ConvertFrom-Json
        if ((& git -C $sourcePath rev-parse HEAD).Trim() -ne $before.head -or (Hash (Join-Path $sourcePath '.git/index')) -ne $before.index -or (& git -C $sourcePath symbolic-ref HEAD).Trim() -ne $before.branch) { throw 'Original Git administration changed.' }
    }
    Assert-Bytes (Join-Path $sourcePath 'original-only.bin') (Payload 8)
    Write-Json (Join-Path $evidence 'lifecycle-complete.json') @{source=$Source; entries=$owner.entries; a=$a.id; b=$b.id; cliAssembly=$CliAssembly; finishedAt=[DateTimeOffset]::UtcNow.ToString('O'); scope='Editorless lifecycle of the exact selected CLI assembly. Unity process/code/scene validation and native allocation/ABBA evidence are separate.'}
    exit 0
}
if ($Stage -eq 'observe') {
    foreach ($letter in @('a','b')) { $w=Workspace $letter; Invoke-Cli ('observe-' + $letter + '-' + [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()) @('workspace','status','--id',$w.id,'--home',$homePath) | Out-Null }
}
