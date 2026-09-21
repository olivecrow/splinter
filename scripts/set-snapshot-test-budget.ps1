[CmdletBinding()]
param([switch]$Apply, [string]$EvidenceDirectory = (Join-Path $PSScriptRoot '../artifacts/snapshot-overlay/setup'))
$ErrorActionPreference = 'Stop'
# Increase only. Never shrink a provider limit during cleanup: shrinking may
# discard unrelated recovery points accumulated while the test was running.
$targetBytes = 64L * 1024 * 1024 * 1024
$plan = [ordered]@{
    volume = 'C:'; diffVolume = 'C:'; targetMaxBytes = $targetBytes
    command = 'vssadmin resize shadowstorage /for=C: /on=C: /maxsize=64GB'
    purpose = 'Protect existing recovery points before bounded snapshot-overlay experiments.'
    allocatesFullLimitImmediately = $false; createsSnapshots = $false
    automaticShrinkOrDeletion = $false; applied = $false
}
if (-not $Apply) { $plan | ConvertTo-Json; return }
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
if (-not ([Security.Principal.WindowsPrincipal]::new($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'AdministratorRequired' }
$volume = Get-CimInstance Win32_Volume -Filter "DriveLetter='C:'"
$storage = @(Get-CimInstance Win32_ShadowStorage | Where-Object { $_.Volume.DeviceID -eq $volume.DeviceID -and $_.DiffVolume.DeviceID -eq $volume.DeviceID })
if ($storage.Count -ne 1) { throw 'StorageAssociationMismatch: no configuration was changed.' }
New-Item -ItemType Directory -Force $EvidenceDirectory | Out-Null
$before = @(Get-CimInstance Win32_ShadowCopy | Select-Object -ExpandProperty ID)
$plan.previousMaxBytes = $storage[0].MaxSpace
if ($storage[0].MaxSpace -lt $targetBytes) {
    $output = & vssadmin.exe resize shadowstorage /for=C: /on=C: /maxsize=64GB 2>&1
    $plan.nativeOutput = @($output | ForEach-Object { "$_" })
    if ($LASTEXITCODE -ne 0) { throw "ShadowStorageResizeFailed: $LASTEXITCODE" }
    $plan.applied = $true
}
$after = @(Get-CimInstance Win32_ShadowCopy | Select-Object -ExpandProperty ID)
$plan.existingSnapshotsPreserved = @($before | Where-Object { $_ -notin $after }).Count -eq 0
$plan.currentStorage = Get-CimInstance Win32_ShadowStorage | Where-Object { $_.Volume.DeviceID -eq $volume.DeviceID } | Select-Object UsedSpace, AllocatedSpace, MaxSpace
$plan | ConvertTo-Json -Depth 5 | Tee-Object -FilePath (Join-Path $EvidenceDirectory 'vss-budget.json')
if (-not $plan.existingSnapshotsPreserved) { throw 'ExistingSnapshotMissing: stop captures and preserve evidence.' }
