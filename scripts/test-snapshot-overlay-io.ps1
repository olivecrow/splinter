[CmdletBinding()]
param([Parameter(Mandatory)][string]$RunDirectory, [switch]$SkipReads, [switch]$ReadsOnly, [string[]]$ReadWorkspaces = @('a','b'))
$ErrorActionPreference = 'Stop'
$run = [IO.Path]::GetFullPath($RunDirectory)
Import-Module (Join-Path $PSScriptRoot 'internal/SnapshotOverlayIO.psm1')
$sources = Get-Content -LiteralPath (Join-Path $run 'source-files.json') -Raw | ConvertFrom-Json
$receipt = Get-Content -LiteralPath (Join-Path $run 'snapshot-t0/snapshot.json') -Raw | ConvertFrom-Json
if (-not $SkipReads) {
if (-not $ReadsOnly) {
$originalUsage = @($sources | ForEach-Object { [ordered]@{path=$_.path; logicalBytes=$_.logicalBytes; allocatedBytes=(Get-OverlayFileAllocationBytes $_.path); attributes=$_.attributes; sha256=$_.sha256} })
Save-OverlayStage $run '01-initial' ([ordered]@{snapshotId=$receipt.snapshot.id; source=$originalUsage; a=(Get-OverlayDeltaUsage $run 'a'); b=(Get-OverlayDeltaUsage $run 'b'); states=@((Get-Content (Join-Path $run 'control-a/state.json') -Raw | ConvertFrom-Json),(Get-Content (Join-Path $run 'control-b/state.json') -Raw | ConvertFrom-Json))})
}
$reads=@(); $beforeA=Get-OverlayDeltaUsage $run 'a'; $beforeB=Get-OverlayDeltaUsage $run 'b'
foreach($name in $ReadWorkspaces) {
    foreach($round in @(1,2)) {
        foreach($source in $sources) {
            $file=Join-Path (Join-Path $run "mount-$name") ([IO.Path]::GetFileName($source.path)); $watch=[Diagnostics.Stopwatch]::StartNew()
            $actual=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash
            if($actual -ne $source.sha256) { throw "ReadHashMismatch: $file" }
            $reads += [ordered]@{workspace=$name; round=$round; bytes=$source.logicalBytes; sha256=$actual; elapsedMilliseconds=$watch.ElapsedMilliseconds}
        }
    }
}
$afterA=Get-OverlayDeltaUsage $run 'a'; $afterB=Get-OverlayDeltaUsage $run 'b'
if($afterA.totalAllocatedBytes -ne $beforeA.totalAllocatedBytes -or $afterB.totalAllocatedBytes -ne $beforeB.totalAllocatedBytes) { throw 'ReadHydrationDetected' }
Save-OverlayStage $run '02-reads' ([ordered]@{reads=$reads;beforeA=$beforeA;beforeB=$beforeB;afterA=$afterA;afterB=$afterB;applicationReadCacheBytes=0;providerStates=@((Get-Content (Join-Path $run 'control-a/state.json') -Raw | ConvertFrom-Json),(Get-Content (Join-Path $run 'control-b/state.json') -Raw | ConvertFrom-Json))})
}
if ($ReadsOnly) { [ordered]@{completed=@('repeated-full-reads');workspaces=$ReadWorkspaces;runDirectory=$run} | ConvertTo-Json; return }
$writes=@(); $patch=New-Object byte[] 4096; [Security.Cryptography.RandomNumberGenerator]::Fill($patch)
foreach($source in $sources) {
    $fileName=[IO.Path]::GetFileName($source.path); $file=Join-Path (Join-Path $run 'mount-a') $fileName
    $before=Get-OverlayDeltaUsage $run 'a'; $watch=[Diagnostics.Stopwatch]::StartNew(); Write-OverlayRange $file 4095 $patch
    $elapsed=$watch.ElapsedMilliseconds; Confirm-OverlayBytes $patch (Read-OverlayRange $file 4095 4096) $fileName
    Confirm-OverlayBytes (Read-OverlayRange $source.path 4095 4096) (Read-OverlayRange (Join-Path (Join-Path $run 'mount-b') $fileName) 4095 4096) 'B keeps immutable original'
    $writes += [ordered]@{fileBytes=$source.logicalBytes;writeOffset=4095;writeBytes=4096;elapsedMilliseconds=$elapsed;before=$before;after=(Get-OverlayDeltaUsage $run 'a')}
}
Save-OverlayStage $run '03-small-writes' ([ordered]@{writes=$writes;patchSha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($patch))})
$mappedFile=Join-Path $run 'mount-a/random-1024.bin'; $mapped=[IO.MemoryMappedFiles.MemoryMappedFile]::CreateFromFile($mappedFile,[IO.FileMode]::Open)
try { $view=$mapped.CreateViewAccessor(1048575,4096); try { $view.WriteArray(0,$patch,0,$patch.Length); $view.Flush() } finally { $view.Dispose() } } finally { $mapped.Dispose() }
Confirm-OverlayBytes $patch (Read-OverlayRange $mappedFile 1048575 4096) 'mmap boundary'
$resized=Join-Path $run 'mount-a/random-16.bin'; $stream=[IO.FileStream]::new($resized,'Open','ReadWrite','None')
try { $stream.SetLength(8192); $stream.SetLength(16384); $stream.Flush($true) } finally { $stream.Dispose() }
Confirm-OverlayBytes (New-Object byte[] 8192) (Read-OverlayRange $resized 8192 8192) 'truncate extension zeros'
$renamed=Join-Path $run 'mount-a/renamed.bin'; [IO.File]::Move($resized,$renamed)
$replacement=Join-Path $run 'mount-a/replacement.tmp'; [IO.File]::WriteAllBytes($replacement,$patch); [IO.File]::Move($replacement,$renamed,$true)
Confirm-OverlayBytes $patch ([IO.File]::ReadAllBytes($renamed)) 'rename replacement'
Save-OverlayStage $run '03-filesystem-operations' ([ordered]@{mmapBoundary=$true;truncateExtendZeros=$true;rename=$true;replace=$true;a=(Get-OverlayDeltaUsage $run 'a');b=(Get-OverlayDeltaUsage $run 'b')})
[ordered]@{completed=@('initial','repeated-full-reads','small-range-writes','mmap','truncate','rename','replace');runDirectory=$run} | ConvertTo-Json
