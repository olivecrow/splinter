[CmdletBinding()]
param([Parameter(Mandatory)][string[]]$RunDirectories,[Parameter(Mandatory)][string]$OutputPath)
$ErrorActionPreference='Stop'
$identity=[Security.Principal.WindowsIdentity]::GetCurrent()
if(-not ([Security.Principal.WindowsPrincipal]::new($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){throw 'AdministratorRequired'}
$receipts=@()
foreach($root in $RunDirectories){
    $root=[IO.Path]::GetFullPath($root)
    & (Join-Path $PSScriptRoot 'reclaim-snapshot-overlay-test.ps1') -RunDirectory $root | Out-Null
    $proof=Get-Content (Join-Path $root '06-reclamation.json') -Raw|ConvertFrom-Json
    if(-not $proof.reclaimed -or $proof.remainingDeltaBytes -ne 0 -or -not $proof.resultsPreserved){throw 'ReclamationProofRequired'}
    foreach($directory in Get-ChildItem -LiteralPath $root -Directory -Filter 'snapshot*'){
        $file=Join-Path $directory.FullName 'snapshot.json';if(-not (Test-Path -LiteralPath $file)){continue}
        $receipt=Get-Content -LiteralPath $file -Raw|ConvertFrom-Json
        $receipts+=@{directory=$directory.FullName;owner=$receipt.ownerId;id=$receipt.snapshot.id}
    }
}
$before=@(Get-CimInstance Win32_ShadowCopy|Select-Object -ExpandProperty ID)
$unowned=@($before|Where-Object {$_ -notin $receipts.id})
foreach($receipt in $receipts){
    @{snapshotId=$receipt.id;remainingDependents=0;resultsPreserved=$true;verifiedAt=[DateTimeOffset]::UtcNow}|ConvertTo-Json|Set-Content (Join-Path $receipt.directory 'release-authorized.json') -Encoding utf8
    & (Join-Path $PSScriptRoot 'vss-snapshot.ps1') -Action Release -ReceiptDirectory $receipt.directory -OwnerId $receipt.owner | Out-Null
}
$after=@(Get-CimInstance Win32_ShadowCopy|Select-Object -ExpandProperty ID)
if(@($unowned|Where-Object {$_ -notin $after}).Count){throw 'UnownedSnapshotMissing'}
if(@($receipts.id|Where-Object {$_ -in $after}).Count){throw 'OwnedSnapshotRemains'}
$report=@{observedAt=[DateTimeOffset]::UtcNow;releasedOwnedIds=@($receipts.id);preservedUnownedIds=$unowned;afterSnapshotIds=$after;storage=@(Get-CimInstance Win32_ShadowStorage|Select-Object UsedSpace,AllocatedSpace,MaxSpace);volumes=@(Get-CimInstance Win32_LogicalDisk|Select-Object DeviceID,FileSystem,FreeSpace,Size);automaticBudgetShrink=$false}
$report|ConvertTo-Json -Depth 7|Set-Content -LiteralPath $OutputPath -Encoding utf8
$report|ConvertTo-Json -Depth 7
