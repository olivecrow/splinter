[CmdletBinding()]
param([Parameter(Mandatory)][string]$RunDirectory, [switch]$SkipReads, [switch]$ReadsOnly, [string[]]$ReadWorkspaces = @('a','b'), [switch]$FunctionsOnly)
$ErrorActionPreference = 'Stop'
$run = [IO.Path]::GetFullPath($RunDirectory)
Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
public static class SnapshotOverlayAllocation {
    [DllImport("kernel32.dll", SetLastError=true)]
    static extern bool GetFileInformationByHandleEx(Microsoft.Win32.SafeHandles.SafeFileHandle file, int infoClass, IntPtr info, uint size);
    public static long Bytes(string path) {
        using var file=System.IO.File.OpenHandle(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite|System.IO.FileShare.Delete);
        IntPtr info=Marshal.AllocHGlobal(24);
        try {
            if(!GetFileInformationByHandleEx(file, 1, info, 24)) throw new Win32Exception();
            return Marshal.ReadInt64(info);
        } finally { Marshal.FreeHGlobal(info); }
    }
}
'@
function Delta-Usage([string]$Name) {
    $root = Join-Path $run "delta-$Name"
    $files = @(Get-ChildItem -LiteralPath $root -File | ForEach-Object {
        [pscustomobject]@{name=$_.Name; logicalBytes=$_.Length; allocatedBytes=[SnapshotOverlayAllocation]::Bytes($_.FullName)}
    })
    $sum=0L; foreach($file in $files) { $sum += [long]$file.allocatedBytes }
    [ordered]@{ files=$files; totalAllocatedBytes=$sum; allocationMethod='GetFileInformationByHandleEx.FileStandardInfo.AllocationSize'; excludesMftRecordAndDirectoryMetadata=$true }
}
function Save-Stage([string]$Name, $Value) { $Value | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath (Join-Path $run "$Name.json") -Encoding utf8 }
function Read-Range([string]$Path, [long]$Offset, [int]$Count) {
    $bytes=New-Object byte[] $Count; $stream=[IO.FileStream]::new($Path,'Open','Read','ReadWrite')
    try { $stream.Position=$Offset; $stream.ReadExactly($bytes) } finally { $stream.Dispose() }
    return ,$bytes
}
function Write-Range([string]$Path, [long]$Offset, [byte[]]$Bytes) {
    $stream=[IO.FileStream]::new($Path,'Open','ReadWrite','ReadWrite')
    try { $stream.Position=$Offset; $stream.Write($Bytes); $stream.Flush($true) } finally { $stream.Dispose() }
}
function Assert-EqualBytes([byte[]]$Expected, [byte[]]$Actual, [string]$Label) {
    if ([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($Expected)) -ne [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($Actual))) { throw "ContentMismatch: $Label" }
}
if ($FunctionsOnly) { return }
$sources = Get-Content -LiteralPath (Join-Path $run 'source-files.json') -Raw | ConvertFrom-Json
$receipt = Get-Content -LiteralPath (Join-Path $run 'snapshot-t0/snapshot.json') -Raw | ConvertFrom-Json
if (-not $SkipReads) {
if (-not $ReadsOnly) {
$originalUsage = @($sources | ForEach-Object { [ordered]@{path=$_.path; logicalBytes=$_.logicalBytes; allocatedBytes=[SnapshotOverlayAllocation]::Bytes($_.path); attributes=$_.attributes; sha256=$_.sha256} })
Save-Stage '01-initial' ([ordered]@{snapshotId=$receipt.snapshot.id; source=$originalUsage; a=(Delta-Usage 'a'); b=(Delta-Usage 'b'); states=@((Get-Content (Join-Path $run 'control-a/state.json') -Raw | ConvertFrom-Json),(Get-Content (Join-Path $run 'control-b/state.json') -Raw | ConvertFrom-Json))})
}
$reads=@(); $beforeA=Delta-Usage 'a'; $beforeB=Delta-Usage 'b'
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
$afterA=Delta-Usage 'a'; $afterB=Delta-Usage 'b'
if($afterA.totalAllocatedBytes -ne $beforeA.totalAllocatedBytes -or $afterB.totalAllocatedBytes -ne $beforeB.totalAllocatedBytes) { throw 'ReadHydrationDetected' }
Save-Stage '02-reads' ([ordered]@{reads=$reads;beforeA=$beforeA;beforeB=$beforeB;afterA=$afterA;afterB=$afterB;applicationReadCacheBytes=0;providerStates=@((Get-Content (Join-Path $run 'control-a/state.json') -Raw | ConvertFrom-Json),(Get-Content (Join-Path $run 'control-b/state.json') -Raw | ConvertFrom-Json))})
}
if ($ReadsOnly) { [ordered]@{completed=@('repeated-full-reads');workspaces=$ReadWorkspaces;runDirectory=$run} | ConvertTo-Json; return }
$writes=@(); $patch=New-Object byte[] 4096; [Security.Cryptography.RandomNumberGenerator]::Fill($patch)
foreach($source in $sources) {
    $fileName=[IO.Path]::GetFileName($source.path); $file=Join-Path (Join-Path $run 'mount-a') $fileName
    $before=Delta-Usage 'a'; $watch=[Diagnostics.Stopwatch]::StartNew(); Write-Range $file 4095 $patch
    $elapsed=$watch.ElapsedMilliseconds; Assert-EqualBytes $patch (Read-Range $file 4095 4096) $fileName
    Assert-EqualBytes (Read-Range $source.path 4095 4096) (Read-Range (Join-Path (Join-Path $run 'mount-b') $fileName) 4095 4096) 'B keeps immutable original'
    $writes += [ordered]@{fileBytes=$source.logicalBytes;writeOffset=4095;writeBytes=4096;elapsedMilliseconds=$elapsed;before=$before;after=(Delta-Usage 'a')}
}
Save-Stage '03-small-writes' ([ordered]@{writes=$writes;patchSha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($patch))})
$mappedFile=Join-Path $run 'mount-a/random-1024.bin'; $mapped=[IO.MemoryMappedFiles.MemoryMappedFile]::CreateFromFile($mappedFile,[IO.FileMode]::Open)
try { $view=$mapped.CreateViewAccessor(1048575,4096); try { $view.WriteArray(0,$patch,0,$patch.Length); $view.Flush() } finally { $view.Dispose() } } finally { $mapped.Dispose() }
Assert-EqualBytes $patch (Read-Range $mappedFile 1048575 4096) 'mmap boundary'
$resized=Join-Path $run 'mount-a/random-16.bin'; $stream=[IO.FileStream]::new($resized,'Open','ReadWrite','None')
try { $stream.SetLength(8192); $stream.SetLength(16384); $stream.Flush($true) } finally { $stream.Dispose() }
Assert-EqualBytes (New-Object byte[] 8192) (Read-Range $resized 8192 8192) 'truncate extension zeros'
$renamed=Join-Path $run 'mount-a/renamed.bin'; [IO.File]::Move($resized,$renamed)
$replacement=Join-Path $run 'mount-a/replacement.tmp'; [IO.File]::WriteAllBytes($replacement,$patch); [IO.File]::Move($replacement,$renamed,$true)
Assert-EqualBytes $patch ([IO.File]::ReadAllBytes($renamed)) 'rename replacement'
Save-Stage '03-filesystem-operations' ([ordered]@{mmapBoundary=$true;truncateExtendZeros=$true;rename=$true;replace=$true;a=(Delta-Usage 'a');b=(Delta-Usage 'b')})
[ordered]@{completed=@('initial','repeated-full-reads','small-range-writes','mmap','truncate','rename','replace');runDirectory=$run} | ConvertTo-Json
