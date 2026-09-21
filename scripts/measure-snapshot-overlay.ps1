[CmdletBinding()]
param([Parameter(Mandatory)][string]$RunDirectory, [Parameter(Mandatory)][string]$Stage, [string]$SourcePath)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'test-snapshot-overlay-io.ps1') -RunDirectory $RunDirectory -FunctionsOnly
$overlays = @()
foreach ($control in Get-ChildItem -LiteralPath $run -Directory -Filter 'control-*') {
    $name = $control.Name.Substring(8)
    $statePath = Join-Path $control.FullName 'state.json'
    if (-not (Test-Path -LiteralPath $statePath)) { continue }
    $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    $processState = $null
    $ownedProcess = Get-Process -Id $state.processId -ErrorAction SilentlyContinue
    if ($ownedProcess -and $ownedProcess.StartTime.ToUniversalTime().Ticks -eq $state.processStartTimeUtcTicks) {
        $processState = [ordered]@{pid=$ownedProcess.Id;workingSetBytes=$ownedProcess.WorkingSet64;privateBytes=$ownedProcess.PrivateMemorySize64;peakWorkingSetBytes=$ownedProcess.PeakWorkingSet64;handles=$ownedProcess.HandleCount}
    }
    $overlays += [ordered]@{name=$name;state=$state;providerProcess=$processState;delta=(Delta-Usage $name)}
}
$source = $null
if ($SourcePath) {
    $watch = [Diagnostics.Stopwatch]::StartNew(); $count=0; $logical=0L; $allocated=0L
    $libraryLogical=0L; $libraryAllocated=0L
    foreach ($file in Get-ChildItem -LiteralPath $SourcePath -File -Recurse -Force) {
        $relative=[IO.Path]::GetRelativePath($SourcePath,$file.FullName)
        if ($relative.StartsWith('.git' + [IO.Path]::DirectorySeparatorChar)) { continue }
        $bytes=[SnapshotOverlayAllocation]::Bytes($file.FullName)
        $count++; $logical += $file.Length; $allocated += $bytes
        if ($relative.StartsWith('Library' + [IO.Path]::DirectorySeparatorChar)) { $libraryLogical += $file.Length; $libraryAllocated += $bytes }
    }
    $source=[ordered]@{path=$SourcePath;files=$count;logicalBytes=$logical;fileDataAllocatedBytes=$allocated;libraryLogicalBytes=$libraryLogical;libraryFileDataAllocatedBytes=$libraryAllocated;measurementMilliseconds=$watch.ElapsedMilliseconds}
}
$memory=Get-CimInstance Win32_PerfFormattedData_PerfOS_Memory | Select-Object AvailableBytes,CacheBytes,SystemCacheResidentBytes,ModifiedPageListBytes
$volumes=@(Get-CimInstance Win32_LogicalDisk | Select-Object DeviceID,FileSystem,FreeSpace,Size)
$report=[ordered]@{stage=$Stage;observedAt=[DateTimeOffset]::UtcNow;overlays=$overlays;source=$source;systemMemory=$memory;volumes=$volumes;fileAllocationExcludesMftAndDirectoryMetadata=$true;systemCacheIsSharedNotOwned=$true}
Save-Stage $Stage $report
$report | ConvertTo-Json -Depth 9
