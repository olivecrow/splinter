[CmdletBinding()]
param([Parameter(Mandatory)][string]$RunDirectory, [string]$Workspace = 'b')
$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSScriptRoot 'internal/SnapshotOverlayIO.psm1')
$run = [IO.Path]::GetFullPath($RunDirectory)
$sources=Get-Content (Join-Path $run 'source-files.json') -Raw | ConvertFrom-Json
$patch=New-Object byte[] 4096; [Security.Cryptography.RandomNumberGenerator]::Fill($patch)
$observations=@()
foreach($source in $sources) {
    $file=Join-Path (Join-Path $run "mount-$Workspace") ([IO.Path]::GetFileName($source.path))
    if((Get-Item -LiteralPath $file).Length -ne $source.logicalBytes) { throw 'UnexpectedTrialFileLength' }
    $before=Get-OverlayDeltaUsage $run $Workspace; $watch=[Diagnostics.Stopwatch]::StartNew()
    Write-OverlayRange $file 2101247 $patch
    $elapsed=$watch.ElapsedMilliseconds; Confirm-OverlayBytes $patch (Read-OverlayRange $file 2101247 4096) 'small write persisted'
    $after=Get-OverlayDeltaUsage $run $Workspace
    $observations += [pscustomobject]@{fileBytes=$source.logicalBytes;writeOffset=2101247;writeBytes=4096;elapsedMilliseconds=$elapsed;before=$before;after=$after;allocatedIncreaseBytes=($after.totalAllocatedBytes-$before.totalAllocatedBytes);journalIncreaseBytes=(($after.files | Where-Object name -eq 'delta.journal').logicalBytes-($before.files | Where-Object name -eq 'delta.journal').logicalBytes)}
}
Save-OverlayStage $run '03-physical-small-writes' ([ordered]@{workspace=$Workspace;observations=$observations;source='non-sparse uncompressed cryptographic random fixture';measurement='FILE_STANDARD_INFO.AllocationSize; file data allocations only; volume-wide VSS/MFT recorded separately'})
$observations | Select-Object fileBytes,writeBytes,allocatedIncreaseBytes,journalIncreaseBytes,elapsedMilliseconds | ConvertTo-Json
