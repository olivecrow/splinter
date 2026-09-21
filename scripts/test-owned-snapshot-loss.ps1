[CmdletBinding()]
param([Parameter(Mandatory)][string]$RunDirectory,[Parameter(Mandatory)][string]$OwnerId,[switch]$InjectLoss)
$ErrorActionPreference='Stop'
$runRoot=[IO.Path]::GetFullPath($RunDirectory)
$receipt=Get-Content -LiteralPath (Join-Path $runRoot 'snapshot-t1/snapshot.json') -Raw | ConvertFrom-Json
if($receipt.ownerId -ne $OwnerId){throw 'SnapshotOwnerMismatch'}
$proofs=@()
foreach($name in @('c','fault','quota')) {
    $resultRoot=Join-Path $runRoot ('results/' + $name)
    $proof=Get-Content -LiteralPath (Join-Path $resultRoot 'result.json') -Raw | ConvertFrom-Json
    if($proof.snapshotId -ne $receipt.snapshot.id -or -not $proof.independentOfSnapshot){throw 'IndependentResultRequired'}
    foreach($file in $proof.files) {
        $path=[IO.Path]::GetFullPath((Join-Path $resultRoot $file.path))
        if(-not $path.StartsWith($resultRoot + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'UnsafeResultPath'}
        if((Get-FileHash -LiteralPath $path).Hash -ne $file.sha256){throw 'IndependentResultChanged'}
    }
    $state=Get-Content -LiteralPath (Join-Path $runRoot ('control-' + $name + '/state.json')) -Raw | ConvertFrom-Json
    if($state.storage.durableSequence -ne $proof.durableSequence){throw 'ResultSequenceChanged'}
    if($name -ne 'c' -and (Test-Path -LiteralPath (Join-Path $runRoot ('mount-' + $name)))){throw 'FaultWorkspacesMustBeUnmounted'}
    if($name -eq 'c' -and ($proof.files.Count -ne 0 -or $proof.durableSequence -ne 0)){throw 'LossTargetMustBeUnmodifiedView'}
    $proofs+=$proof
}
if(-not $InjectLoss){@{action='plan';snapshotId=$receipt.snapshot.id;ownerId=$OwnerId;resultsVerified=$true;activeLossTarget='unmodified mount-c'} | ConvertTo-Json;return}
$identity=[Security.Principal.WindowsIdentity]::GetCurrent()
if(-not ([Security.Principal.WindowsPrincipal]::new($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){throw 'AdministratorRequired'}
$before=@(Get-CimInstance Win32_ShadowCopy | Select-Object -ExpandProperty ID)
$target=Get-CimInstance Win32_ShadowCopy | Where-Object ID -eq $receipt.snapshot.id
if(-not $target -or $target.ProviderID -ne $receipt.nativeProviderId){throw 'SnapshotProviderMismatch'}
$intent=@{snapshotId=$receipt.snapshot.id;ownerId=$OwnerId;beforeSnapshotIds=$before;independentResults=$proofs;startedAt=[DateTimeOffset]::UtcNow;fault='owned snapshot loss with unmodified reader mounted'}
$intent | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath (Join-Path $runRoot '06-snapshot-loss-intent.json') -Encoding utf8
$target | Remove-CimInstance
$after=@(Get-CimInstance Win32_ShadowCopy | Select-Object -ExpandProperty ID)
if($after -contains $receipt.snapshot.id){throw 'SnapshotLossNotInjected'}
if(@($before | Where-Object {$_ -ne $receipt.snapshot.id -and $after -notcontains $_}).Count -ne 0){throw 'UnrelatedSnapshotDisappeared'}
@{snapshotId=$receipt.snapshot.id;ownerId=$OwnerId;beforeSnapshotIds=$before;afterSnapshotIds=$after;unrelatedSnapshotsPreserved=$true;completedAt=[DateTimeOffset]::UtcNow} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $runRoot '06-snapshot-loss-injected.json') -Encoding utf8
