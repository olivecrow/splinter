[CmdletBinding()]
param([Parameter(Mandatory)][string]$RunDirectory,[Parameter(Mandatory)][ValidateSet('a','b')][string]$Name)
$ErrorActionPreference='Stop';$root=[IO.Path]::GetFullPath($RunDirectory)
$control=Join-Path $root "control-$Name";$requestPath=Join-Path $control 'request.json'
$request=Get-Content -LiteralPath $requestPath -Raw|ConvertFrom-Json
$gitPath=Join-Path $root "git-$Name.json";$git=Get-Content -LiteralPath $gitPath -Raw|ConvertFrom-Json
$invoke=Join-Path $PSScriptRoot 'invoke-snapshot-spike.ps1';$preservedPath=Join-Path $root "unity-$Name-preserved.json"
function Invoke-Spike([string]$Action,[string]$Revision) {
    $arguments=@{OverlayRequestPath=$requestPath;Action=$Action;ReceiptPath=$gitPath}
    if($Revision){$arguments.Revision=$Revision}
    $response=(& $invoke @arguments)|ConvertFrom-Json
    if($LASTEXITCODE -ne 0 -or $response.status -ne 'Succeeded'){throw ($response|ConvertTo-Json -Depth 8)}
    return $response
}
if(-not (Test-Path -LiteralPath $preservedPath)){
    $saved=Invoke-Spike 'preserve'
    $saved|ConvertTo-Json -Depth 10|Set-Content -LiteralPath $preservedPath -Encoding utf8
}else{$saved=Get-Content -LiteralPath $preservedPath -Raw|ConvertFrom-Json}
$revision=$saved.data.result.revision;$resultRef=$saved.data.result.resultRef
if(-not $saved.data.result.independentOfSnapshot -or (& git -C $git.project rev-parse $resultRef) -ne $revision){throw 'IndependentGitResultRequired'}
# Reuse an existing preservation receipt only when no source edits remain.
# Generated bridge/cache paths are intentionally excluded from Git results.
if(Test-Path -LiteralPath $request.mountPath){
    $changed=& git -C $request.mountPath status --porcelain --untracked-files=all -- . ':(exclude)Assets/__Splinter__' ':(exclude)Assets/__Splinter__.meta'
    if($LASTEXITCODE -ne 0 -or $changed){throw 'SourceChangedAfterPreservation'}
}
$accounting=Join-Path $root "unity-$Name-journal-accounting.json"
& python (Join-Path $PSScriptRoot 'measure-overlay-journal.py') (Join-Path $request.deltaPath 'delta.journal') --output $accounting|Out-Null
if($LASTEXITCODE -ne 0){throw 'JournalAccountingFailed'}
Invoke-Spike 'unmount'|ConvertTo-Json -Depth 12|Set-Content (Join-Path $root "mount-$Name-final-unmount.json") -Encoding utf8
Invoke-Spike 'git-release' $revision|ConvertTo-Json -Depth 8|Set-Content (Join-Path $root "git-$Name-final-release.json") -Encoding utf8
$state=Get-Content (Join-Path $control 'state.json') -Raw|ConvertFrom-Json
$output=Join-Path $root "results/$Name";New-Item -ItemType Directory -Path $output -Force|Out-Null
$receipt=@{version=1;id=$request.identity.id;snapshotId=$request.identity.snapshot.id;durableSequence=$state.storage.durableSequence;independentOfSnapshot=$true;files=@();journalSha256=(Get-FileHash (Join-Path $request.deltaPath 'delta.journal')).Hash;gitResult=@{project=$git.project;resultRef=$resultRef;revision=$revision;registrationPath=$git.gitDirectory};scope='Git source result; generated caches and owned bridge are disposable'}
$receipt|ConvertTo-Json -Depth 8|Set-Content (Join-Path $output 'result.json') -Encoding utf8
$receipt|ConvertTo-Json -Depth 8
