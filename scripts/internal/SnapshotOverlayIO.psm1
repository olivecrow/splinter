$ErrorActionPreference = 'Stop'
$script:allocationReady = $false

function Initialize-OverlayAllocation {
    if ($script:allocationReady) { return }
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
    $script:allocationReady = $true
}

function Get-OverlayFileAllocationBytes([string]$Path) {
    Initialize-OverlayAllocation
    [SnapshotOverlayAllocation]::Bytes($Path)
}

function Get-OverlayDeltaUsage([string]$RunDirectory, [string]$Name) {
    Initialize-OverlayAllocation
    $root = Join-Path $RunDirectory "delta-$Name"
    $files = @(Get-ChildItem -LiteralPath $root -File | ForEach-Object {
        [pscustomobject]@{name=$_.Name; logicalBytes=$_.Length; allocatedBytes=[SnapshotOverlayAllocation]::Bytes($_.FullName)}
    })
    $sum=0L; foreach($file in $files) { $sum += [long]$file.allocatedBytes }
    [ordered]@{ files=$files; totalAllocatedBytes=$sum; allocationMethod='GetFileInformationByHandleEx.FileStandardInfo.AllocationSize'; excludesMftRecordAndDirectoryMetadata=$true }
}

function Save-OverlayStage([string]$RunDirectory, [string]$Name, $Value) {
    $Value | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath (Join-Path $RunDirectory "$Name.json") -Encoding utf8
}

function Read-OverlayRange([string]$Path, [long]$Offset, [int]$Count) {
    $bytes=New-Object byte[] $Count; $stream=[IO.FileStream]::new($Path,'Open','Read','ReadWrite')
    try { $stream.Position=$Offset; $stream.ReadExactly($bytes) } finally { $stream.Dispose() }
    return ,$bytes
}

function Write-OverlayRange([string]$Path, [long]$Offset, [byte[]]$Bytes) {
    $stream=[IO.FileStream]::new($Path,'Open','ReadWrite','ReadWrite')
    try { $stream.Position=$Offset; $stream.Write($Bytes); $stream.Flush($true) } finally { $stream.Dispose() }
}

function Confirm-OverlayBytes([byte[]]$Expected, [byte[]]$Actual, [string]$Label) {
    if ([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($Expected)) -ne [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($Actual))) { throw "ContentMismatch: $Label" }
}

Export-ModuleMember -Function Get-OverlayFileAllocationBytes, Get-OverlayDeltaUsage, Save-OverlayStage, Read-OverlayRange, Write-OverlayRange, Confirm-OverlayBytes
