[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$HomePath,
    [Parameter(Mandatory)][string]$Project,
    [Parameter(Mandatory)][string]$OutputPath,
    [Parameter(Mandatory)][string]$Label,
    [string]$EvidencePath,
    [string[]]$WorkspaceIds,
    [string[]]$GeneratedDirectoryIds,
    [string[]]$ProjectBasisIds,
    [switch]$SkipOriginalLibrary,
    [ValidateSet('git','none','plastic')][string]$SourceKind = 'git',
    [ValidateSet('unity','none')][string]$ApplicationKind = 'unity'
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
        var fullPath = System.IO.Path.GetFullPath(path);
        var nativePath = fullPath.StartsWith(@"\\?\", StringComparison.Ordinal)
            ? fullPath : fullPath.StartsWith(@"\\", StringComparison.Ordinal)
                ? @"\\?\UNC\" + fullPath.Substring(2) : @"\\?\" + fullPath;
        using (var handle = CreateFileW(nativePath, 0x80, 7, IntPtr.Zero, 3, 0x00200000, IntPtr.Zero)) {
            if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
            if (!GetFileInformationByHandleEx(handle, 1, out var info, (uint)Marshal.SizeOf<StandardInfo>()))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            if (info.Links > 1) throw new InvalidOperationException("Hardlinked allocation cannot be attributed by path; preserve unknown.");
            return info.AllocationSize;
        }
    }
}
'@
}
$stateRoot = [IO.Path]::GetFullPath($HomePath)
$sourceRoot = [IO.Path]::GetFullPath($Project)
$destination = [IO.Path]::GetFullPath($OutputPath)
if (Test-Path -LiteralPath $destination) { throw 'Preserve existing measurement output; choose a new file.' }
$roots = [Collections.Generic.List[object]]::new()
if ($WorkspaceIds) {
    if (@($WorkspaceIds | Select-Object -Unique).Count -ne $WorkspaceIds.Count -or @($WorkspaceIds | Where-Object { $_ -cnotmatch '^[a-f0-9]{24}$' }).Count) { throw 'Workspace IDs must be unique exact lowercase task IDs.' }
    foreach ($workspaceId in $WorkspaceIds) {
        $taskRoot = Join-Path $stateRoot ('records/' + $workspaceId)
        $workspaceFile = Join-Path $taskRoot 'workspace.json'
        if (-not (Test-Path -LiteralPath $workspaceFile)) { throw 'Selected workspace record is missing; do not count it as zero.' }
        $saved = Get-Content -LiteralPath $workspaceFile -Raw | ConvertFrom-Json
        if ($saved.version -ne 2 -or $saved.id -cne $workspaceId -or $saved.projectPath -ne $sourceRoot) { throw 'Selected workspace identity/project differs.' }
        $roots.Add(@{name=('workspace-' + $workspaceId);path=$taskRoot;scope='selected-workspace';baselineExcluded=$false})
    }
} else { $roots.Add(@{name='control-and-delta';path=(Join-Path $stateRoot 'records');scope='shared-home';baselineExcluded=$false}) }
foreach ($basisGroup in @(@{ids=$GeneratedDirectoryIds;folder='library-bases';kind='Library';relative='Library'},@{ids=$ProjectBasisIds;folder='project-bases';kind='project';relative=''})) {
    $selectedBasisIds = @($basisGroup.ids | Where-Object { $_ })
    if (-not $selectedBasisIds.Count) { continue }
    if ($basisGroup.kind -eq 'project' -and $SourceKind -ne 'none') { throw 'Managed project allocation requires source none.' }
    if (@($selectedBasisIds | Select-Object -Unique).Count -ne $selectedBasisIds.Count -or @($selectedBasisIds | Where-Object { $_ -cnotmatch '^[a-f0-9]{24}$' }).Count) { throw 'Basis IDs must be unique exact lowercase IDs.' }
    if (-not $SkipOriginalLibrary) { throw 'Skip the logical original Library mount when explicitly measuring its moved basis.' }
    foreach ($basisId in $selectedBasisIds) {
        $basisRoot = Join-Path $stateRoot ($basisGroup.folder + '/' + $basisId)
        $basisState = Join-Path $basisRoot 'state.json'
        if (-not (Test-Path -LiteralPath $basisState)) { throw 'Selected generated-directory state is missing; preserve unknown.' }
        $savedBasis = Get-Content -LiteralPath $basisState -Raw | ConvertFrom-Json
        if ($savedBasis.version -ne 1 -or $savedBasis.basis.id -cne $basisId -or $savedBasis.basis.ownerId -cne $basisId -or $savedBasis.basis.relativePath -cne $basisGroup.relative -or $savedBasis.basis.projectPath -ne $sourceRoot -or $savedBasis.basis.receiptDirectory -ne $basisRoot -or $savedBasis.basis.snapshot.rootPath -ne (Join-Path $basisRoot 'base')) { throw 'Selected basis identity/project/path differs.' }
        $roots.Add(@{name=($basisGroup.kind + '-basis-' + $basisId);path=$basisRoot;scope='shared-including-original-basis';baselineExcluded=$true;movedPayloadPath=(Join-Path $basisRoot 'base');movedCategory=('moved-original-' + $basisGroup.kind + '-payload')})
    }
}
$roots.Add(@{name='validation-product';path=(Join-Path $stateRoot 'validation-product');scope='shared-home';baselineExcluded=$false})
$roots.Add(@{name='validation-workflows';path=(Join-Path $stateRoot 'validation-workflows');scope='shared-home';baselineExcluded=$false})
$roots.Add(@{name='runners';path=(Join-Path $stateRoot 'runners');scope='shared-home-cache';baselineExcluded=$false})
if ($SourceKind -eq 'git') {
    $gitCommon = (& git -C $sourceRoot rev-parse --path-format=absolute --git-common-dir).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Git common directory observation failed' }
    $roots.Add(@{name='git-common';path=$gitCommon;scope='shared-including-results';baselineExcluded=$true})
}
if ($ApplicationKind -eq 'unity' -and -not $SkipOriginalLibrary) {
    $roots.Add(@{name='original-library';path=(Join-Path $sourceRoot 'Library');scope='original';baselineExcluded=$true})
}
if ($EvidencePath) {
    $roots.Add(@{name='verification-evidence';path=[IO.Path]::GetFullPath($EvidencePath);scope='measurement-overhead';baselineExcluded=$false})
}
# Root totals can only be summed when their physical inventories are disjoint.
# Never traverse the logical workspace mounts or quietly duplicate evidence.
for ($left = 0; $left -lt $roots.Count; $left++) {
    for ($right = $left + 1; $right -lt $roots.Count; $right++) {
        $a = [IO.Path]::GetFullPath($roots[$left].path).TrimEnd('\','/')
        $b = [IO.Path]::GetFullPath($roots[$right].path).TrimEnd('\','/')
        if ($a.Equals($b,[StringComparison]::OrdinalIgnoreCase) -or $a.StartsWith($b+'\',[StringComparison]::OrdinalIgnoreCase) -or $b.StartsWith($a+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Overlapping measurement roots would duplicate allocation; choose disjoint roots.' }
    }
}
$rows = foreach ($root in $roots) {
    $errorsSeen = [Collections.Generic.List[string]]::new()
    $fileCount = 0L; $logical = 0L; $allocated = 0L
    $categories = @{}
    if (Test-Path -LiteralPath $root.path) {
        $rootItem = Get-Item -LiteralPath $root.path -Force
        if (-not $rootItem.PSIsContainer -or $rootItem.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Measurement roots must be real directories, never mounts/reparse points.' }
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
                            default {
                                if ($item.FullName -match '[\\/]files[\\/]objects[\\/]') { 'independent-file-result-payload' }
                                elseif ($item.Name -like 'source-fingerprint.json*') { 'source-manifests' }
                                else { 'other' }
                            }
                        }
                        if ($root.movedPayloadPath -and $item.FullName.StartsWith($root.movedPayloadPath + '\', [StringComparison]::OrdinalIgnoreCase)) { $category = $root.movedCategory }
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
$productRows = @($rows | Where-Object { $_.scope -notin @('original','measurement-overhead') })
$completeProduct = @($productRows | Where-Object { -not $_.complete }).Count -eq 0
$productAllocatedBytes = [long]0
if ($completeProduct) {
    foreach ($productRow in $productRows) { $productAllocatedBytes += [long]$productRow.allocatedBytes }
}
$volumes = @(@($stateRoot,$sourceRoot) | ForEach-Object { [IO.Path]::GetPathRoot($_) } | Select-Object -Unique | ForEach-Object { $drive=[IO.DriveInfo]::new($_); @{root=$_;freeBytes=$drive.AvailableFreeSpace;totalBytes=$drive.TotalSize;fileSystem=$drive.DriveFormat;scope='volume-shared';workspaceAttribution='unknown'} })
$report = [ordered]@{version=2;label=$Label;atUtc=[DateTimeOffset]::UtcNow;home=$stateRoot;project=$sourceRoot;sourceKind=$SourceKind;applicationKind=$ApplicationKind;workspaceIds=@($WorkspaceIds);generatedDirectoryIds=@($GeneratedDirectoryIds);projectBasisIds=@($ProjectBasisIds);originalLibrarySampled=($ApplicationKind -eq 'unity' -and -not $SkipOriginalLibrary);
    roots=@($rows);vssVolumeObservations=$vss;vssObservationError=$vssError;totalAttributableBytes=$null;
    volumeObservations=$volumes;productFileAllocationBytes=if($completeProduct){$productAllocatedBytes}else{$null};productFileAllocationComplete=$completeProduct;
    limitations=@('Subtract each category baseline before comparing additional allocation. Categories subdivide their root; do not add them again.',
        'VSS is volume-wide and may include unrelated original writes. No per-workspace attribution or total savings claim.',
        'File allocations exclude MFT/directory metadata and kernel/OS caches. Stage samples can miss peaks.',
        'Original Library baseline is excluded. Workspace Library writes are already inside the delta journal.',
        'An explicitly selected moved Library basis is included once. Subtract moved-original-Library-payload as existing baseline before deriving additional allocation.',
        'Logs outside these roots and this measurement output require a separate evidence allocation entry.',
        'Optional verification-evidence is measurement overhead, not product workspace allocation; count a shared evidence root only once.')}
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
$json = $report | ConvertTo-Json -Depth 12
# No overwrite, even if another observation publishes this path during scanning.
$output = [IO.FileStream]::new($destination,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::Read)
try {
    $bytes=[Text.UTF8Encoding]::new($false).GetBytes($json); $output.Write($bytes); $output.Flush($true)
} finally { $output.Dispose() }
[pscustomobject]@{label=$Label;roots=$rows.Count;completeRoots=@($rows | Where-Object complete).Count;vssKnown=($null -eq $vssError);output=$destination} | ConvertTo-Json -Compress
