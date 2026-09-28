[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$HomePath,
    [Parameter(Mandatory)][string]$Project,
    [Parameter(Mandatory)][string]$OutputPath,
    [Parameter(Mandatory)][string]$Label,
    [string]$EvidencePath
)
$ErrorActionPreference = 'Stop'
# Read-only file allocation sampling. Never traverse a workspace mount or count
# its logical view as another physical copy. Volume-wide VSS is not attributable.
if (-not ('SplinterAllocationProbe' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
public static class SplinterAllocationProbe {
    [StructLayout(LayoutKind.Sequential)] struct StandardInfo {
        public long AllocationSize, EndOfFile;
        public uint Links;
        [MarshalAs(UnmanagedType.U1)] public bool DeletePending;
        [MarshalAs(UnmanagedType.U1)] public bool Directory;
    }
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    static extern SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError=true)]
    static extern bool GetFileInformationByHandleEx(SafeFileHandle handle, int kind, out StandardInfo info, uint size);
    public static long Get(string path) {
        using (var handle = CreateFileW(path, 0x80, 7, IntPtr.Zero, 3, 0x00200000, IntPtr.Zero)) {
            if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
            if (!GetFileInformationByHandleEx(handle, 1, out var info, (uint)Marshal.SizeOf<StandardInfo>()))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            return info.AllocationSize;
        }
    }
}
'@
}
$stateRoot = [IO.Path]::GetFullPath($HomePath)
$sourceRoot = [IO.Path]::GetFullPath($Project)
$roots = [Collections.Generic.List[object]]::new()
$roots.Add(@{name='control-and-delta';path=(Join-Path $stateRoot 'records');scope='owned';baselineExcluded=$false})
$roots.Add(@{name='validation-product';path=(Join-Path $stateRoot 'validation-product');scope='owned';baselineExcluded=$false})
$roots.Add(@{name='validation-workflows';path=(Join-Path $stateRoot 'validation-workflows');scope='owned';baselineExcluded=$false})
$roots.Add(@{name='runners';path=(Join-Path $stateRoot 'runners');scope='owned';baselineExcluded=$false})
$gitCommon = (& git -C $sourceRoot rev-parse --path-format=absolute --git-common-dir).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Git common directory observation failed' }
$roots.Add(@{name='git-common';path=$gitCommon;scope='shared-including-results';baselineExcluded=$true})
$roots.Add(@{name='original-library';path=(Join-Path $sourceRoot 'Library');scope='original';baselineExcluded=$true})
if ($EvidencePath) {
    $roots.Add(@{name='verification-evidence';path=[IO.Path]::GetFullPath($EvidencePath);scope='measurement-overhead';baselineExcluded=$false})
}
$rows = foreach ($root in $roots) {
    $errorsSeen = [Collections.Generic.List[string]]::new()
    $fileCount = 0L; $logical = 0L; $allocated = 0L
    $categories = @{}
    if (Test-Path -LiteralPath $root.path) {
        $pending = [Collections.Generic.Stack[string]]::new(); $pending.Push($root.path)
        while ($pending.Count) {
            $directory = $pending.Pop()
            try {
                foreach ($item in Get-ChildItem -LiteralPath $directory -Force -ErrorAction Stop) {
                    if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { $errorsSeen.Add('reparse-skipped: ' + $item.FullName); continue }
                    if ($item.PSIsContainer) { $pending.Push($item.FullName); continue }
                    try {
                        $size = [SplinterAllocationProbe]::Get($item.FullName)
                        $logical += $item.Length; $allocated += $size; $fileCount++
                        $category = switch ($item.Name) {
                            'delta.journal' { 'delta-journal-including-Library-writes' }
                            'delta.journal.compacting' { 'delta-compaction-candidate' }
                            'delta.journal.previous' { 'delta-compaction-previous' }
                            default { if ($item.Name -like 'source-fingerprint.json*') { 'source-manifests' } else { 'other' } }
                        }
                        if (-not $categories.ContainsKey($category)) { $categories[$category] = @{logicalBytes=0L;allocatedBytes=0L;files=0L} }
                        $categories[$category].logicalBytes += $item.Length; $categories[$category].allocatedBytes += $size; $categories[$category].files++
                    } catch { $errorsSeen.Add($item.FullName + ': ' + $_.Exception.Message) }
                }
            } catch { $errorsSeen.Add($directory + ': ' + $_.Exception.Message) }
        }
    }
    [ordered]@{name=$root.name;path=$root.path;scope=$root.scope;baselineExcluded=$root.baselineExcluded;
        files=$fileCount;logicalBytes=$logical;allocatedBytes=if($errorsSeen.Count){$null}else{$allocated};
        observedAllocatedSubtotal=$allocated;complete=($errorsSeen.Count -eq 0);categories=$categories;errors=@($errorsSeen)}
}
$vss = $null; $vssError = $null
try { $vss = @(Get-CimInstance Win32_ShadowStorage -ErrorAction Stop | Select-Object @{n='volume';e={$_.Volume.DeviceID}},@{n='diffVolume';e={$_.DiffVolume.DeviceID}},UsedSpace,AllocatedSpace,MaxSpace) }
catch { $vssError = $_.Exception.Message }
$report = [ordered]@{version=1;label=$Label;atUtc=[DateTimeOffset]::UtcNow;home=$stateRoot;project=$sourceRoot;
    roots=@($rows);vssVolumeObservations=$vss;vssObservationError=$vssError;totalAttributableBytes=$null;
    limitations=@('Subtract each category baseline before comparing additional allocation. Categories subdivide their root; do not add them again.',
        'VSS is volume-wide and may include unrelated original writes. No per-workspace attribution or total savings claim.',
        'File allocations exclude MFT/directory metadata and kernel/OS caches. Stage samples can miss peaks.',
        'Original Library baseline is excluded. Workspace Library writes are already inside the delta journal.',
        'Logs outside these roots and this measurement output require a separate evidence allocation entry.',
        'Optional verification-evidence is measurement overhead, not product workspace allocation; count a shared evidence root only once.')}
$destination = [IO.Path]::GetFullPath($OutputPath)
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $destination -Encoding utf8
[pscustomobject]@{label=$Label;roots=$rows.Count;completeRoots=@($rows | Where-Object complete).Count;vssKnown=($null -eq $vssError);output=$destination} | ConvertTo-Json -Compress
