[CmdletBinding()]
param([Parameter(Mandatory)][string]$RunDirectory,[switch]$Reclaim)
$ErrorActionPreference='Stop';$root=[IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($RunDirectory))
function Assert-OwnedPlain([string]$Path) {
    $absolute=[IO.Path]::GetFullPath($Path)
    if(-not $absolute.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'OutsideExperimentRoot'}
    for($part=$absolute;$part -and $part.Length -ge $root.Length;$part=[IO.Path]::GetDirectoryName($part)){
        if((Test-Path -LiteralPath $part) -and ((Get-Item -LiteralPath $part -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'UnexpectedReparsePath'}
    }
}
$intentPath=Join-Path $root 'reclaim-intent.json'
$prior=if(Test-Path -LiteralPath $intentPath){Get-Content -LiteralPath $intentPath -Raw|ConvertFrom-Json}else{$null}
$verified=@()
foreach($control in Get-ChildItem -LiteralPath $root -Directory -Filter 'control-*') {
    $name=$control.Name.Substring(8);$request=Get-Content (Join-Path $control.FullName 'request.json') -Raw|ConvertFrom-Json
    $state=Get-Content (Join-Path $control.FullName 'state.json') -Raw|ConvertFrom-Json
    $delta=Join-Path $root "delta-$name";Assert-OwnedPlain $delta
    if([IO.Path]::GetFullPath($request.deltaPath) -ne $delta -or $state.id -ne $request.identity.id -or $state.snapshotId -ne $request.identity.snapshot.id -or $state.token -ne $request.token){throw 'OverlayOwnershipMismatch'}
    if(Test-Path -LiteralPath $request.mountPath){throw 'MountRemains'}
    $provider=Get-Process -Id $state.processId -ErrorAction SilentlyContinue
    if($provider -and $provider.StartTime.ToUniversalTime().Ticks -eq $state.processStartTimeUtcTicks){throw 'ProviderRemains'}
    if($state.phase -ne 'unmounted' -or $state.openHandles -ne 0){throw 'NormalUnmountProofRequired'}
    $resultPath=Join-Path $root "results/$name/result.json";Assert-OwnedPlain $resultPath
    $result=Get-Content -LiteralPath $resultPath -Raw|ConvertFrom-Json
    if(-not $result.independentOfSnapshot -or $result.id -ne $state.id -or $result.snapshotId -ne $state.snapshotId -or $result.durableSequence -ne $state.storage.durableSequence){throw 'IndependentCurrentResultRequired'}
    foreach($file in $result.files){$path=Join-Path ([IO.Path]::GetDirectoryName($resultPath)) $file.path;Assert-OwnedPlain $path;if((Get-FileHash -LiteralPath $path).Hash -ne $file.sha256){throw 'ResultHashMismatch'}}
    if($result.gitResult){
        $git=$result.gitResult
        if((& git -C $git.project rev-parse $git.resultRef) -ne $git.revision){throw 'ResultRefMismatch'}
        & git -C $git.project cat-file -e ($git.revision+'^{tree}');if($LASTEXITCODE -ne 0){throw 'ResultTreeMissing'}
        if(Test-Path -LiteralPath $git.registrationPath){throw 'GitRegistrationRemains'}
        $app=(& pwsh (Join-Path $PSScriptRoot 'invoke-snapshot-spike.ps1') -OverlayRequestPath (Join-Path $control.FullName 'request.json') -Action unity-status)|ConvertFrom-Json
        if($LASTEXITCODE -ne 0 -or -not $app.data.result.resources.ownershipVerified -or $app.data.result.resources.activeProcesses -ne 0 -or $app.data.result.resources.workingSetBytes -ne 0 -or $app.data.result.resources.privateBytes -ne 0){throw 'ApplicationResourcesRemain'}
    }
    $bytes=0L
    if(Test-Path -LiteralPath $delta){
        $identity=Get-Content (Join-Path $delta 'identity.json') -Raw|ConvertFrom-Json
        if($identity.id -ne $state.id -or $identity.snapshot.id -ne $state.snapshotId){throw 'DeltaIdentityMismatch'}
        if((Get-FileHash -LiteralPath (Join-Path $delta 'delta.journal')).Hash -ne $result.journalSha256){throw 'DeltaChangedAfterResultPreservation'}
        foreach($file in Get-ChildItem -LiteralPath $delta -Recurse -Force){Assert-OwnedPlain $file.FullName;if(-not $file.PSIsContainer){$bytes+=$file.Length}}
    }elseif(-not @($prior.overlays|Where-Object {$_.id -eq $state.id -and $_.journalSha256 -eq $result.journalSha256}).Count){throw 'MissingDeltaWithoutReclamationIntent'}
    $verified+=@{id=$state.id;snapshotId=$state.snapshotId;deltaPath=$delta;journalSha256=$result.journalSha256;resultPath=$resultPath;logicalBytesBeforeReclaim=$bytes;ownedProcesses=0;ownedMemoryBytes=0;openHandles=0;mountPresent=$false}
}
$report=@{observedAt=[DateTimeOffset]::UtcNow;overlays=$verified;remainingDependents=0;resultsPreserved=$true;reclaimed=[bool]$Reclaim}
if($Reclaim){
    $report|ConvertTo-Json -Depth 9|Set-Content -LiteralPath $intentPath -Encoding utf8
    foreach($entry in $verified){Assert-OwnedPlain $entry.deltaPath;if(Test-Path -LiteralPath $entry.deltaPath){Remove-Item -LiteralPath $entry.deltaPath -Recurse -Force}}
    if(@($verified|Where-Object {Test-Path -LiteralPath $_.deltaPath}).Count){throw 'DeltaReclamationIncomplete'}
    $report.remainingDeltaBytes=0
    $report|ConvertTo-Json -Depth 9|Set-Content (Join-Path $root '06-reclamation.json') -Encoding utf8
}
$report|ConvertTo-Json -Depth 9
