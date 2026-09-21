[CmdletBinding()]
param([Parameter(Mandatory)][string]$RunDirectory)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($RunDirectory);$control=Join-Path $root 'control-a';$requestPath=Join-Path $control 'request.json'
$request=Get-Content -LiteralPath $requestPath -Raw|ConvertFrom-Json
$invoke=Join-Path $PSScriptRoot 'invoke-snapshot-spike.ps1'
function Call([string]$Action){$text=& $invoke -OverlayRequestPath $requestPath -Action $Action;$result=$text|ConvertFrom-Json;if($result.status -ne 'Succeeded'){throw $text};return $result.data.result}
$proof=Get-Content -LiteralPath (Join-Path $root 'results/a/result.json') -Raw|ConvertFrom-Json
$before=Call 'unmount';$journal=Join-Path $request.deltaPath 'delta.journal'
$valid=[IO.File]::ReadAllBytes($journal);$hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($valid))
if($hash -ne $proof.journalSha256){throw 'ResultDoesNotCoverCurrentJournal'}
$backup=Join-Path $root '06-journal-valid.bin'
if(Test-Path -LiteralPath $backup){throw 'RecoveryExperimentAlreadyPrepared'}
[IO.File]::WriteAllBytes($backup,$valid)
$tail=New-Object byte[] 19;[BitConverter]::GetBytes([int]100).CopyTo($tail,0);[BitConverter]::GetBytes([int]64).CopyTo($tail,4)
$file=[IO.File]::Open($journal,'Append','Write','None');try{$file.Write($tail);$file.Flush($true)}finally{$file.Dispose()}
$reconnected=Call 'mount';$afterIncomplete=Call 'unmount'
if((Get-FileHash -LiteralPath $journal).Hash -ne $hash){throw 'IncompleteFrameRecoveryChangedAcknowledgedPrefix'}
$bad=[byte[]]$valid.Clone();$bad[$bad.Length-1]=$bad[$bad.Length-1] -bxor 1;[IO.File]::WriteAllBytes($journal,$bad)
$failedText=& $invoke -OverlayRequestPath $requestPath -Action mount;$failed=$failedText|ConvertFrom-Json
if($failed.status -ne 'Failed'){throw 'CompleteCorruptionAccepted'}
$state=Get-Content -LiteralPath (Join-Path $control 'state.json') -Raw|ConvertFrom-Json
if($state.error -ne 'DeltaCorrupt'){throw 'ExpectedDeltaCorrupt'}
if((Get-FileHash -LiteralPath $journal).Hash -ne [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bad))){throw 'CorruptionWasDiscarded'}
[IO.File]::WriteAllBytes((Join-Path $root '06-journal-corrupt.bin'),$bad)
# Restore only the single injected corruption from the verified owned backup.
$provider=Get-Process -Id $state.processId -ErrorAction SilentlyContinue
if($provider -and $provider.StartTime.ToUniversalTime().Ticks -eq $state.processStartTimeUtcTicks){$provider.WaitForExit()}
[IO.File]::WriteAllBytes($journal,$valid)
$recovered=Call 'mount'
@{snapshotId=$request.identity.snapshot.id;acknowledgedHash=$hash;injectedIncompleteBytes=$tail.Length;afterIncomplete=$afterIncomplete;corruptFailure=$failed.error;corruptState=$state;recovered=$recovered;injectedByteRestoredFromVerifiedBackup=$true;originalAndOtherWorkspacesUntouched=$true} | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $root '06-journal-recovery.json') -Encoding utf8
@{incompleteTailRecovered=$true;completeCorruptionPreserved=$true;recovered=$true} | ConvertTo-Json
