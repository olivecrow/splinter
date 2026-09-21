[CmdletBinding()]
param(
    [ValidateSet('Plan', 'Probe', 'InstallWinFsp')][string]$Action = 'Plan',
    [string]$Installer = (Join-Path $PSScriptRoot '../.local/winfsp/winfsp-2.2.26215.msi'),
    [string]$EvidenceDirectory = (Join-Path $PSScriptRoot '../artifacts/snapshot-overlay/setup')
)

$ErrorActionPreference = 'Stop'
$installerHash = '2ECB5C89405488A95BBD8A01875E02C48534FD37BBDFD84488F7590464D65944'
$release = 'https://github.com/winfsp/winfsp/releases/tag/v2.2B4'
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$isAdmin = ([Security.Principal.WindowsPrincipal]::new($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
$plan = [ordered]@{
    version = 1; action = $Action; elevated = $isAdmin
    candidate = 'WinFsp 2.2.26215 (2026 Beta4)'; release = $release
    installer = [IO.Path]::GetFullPath($Installer); sha256 = $installerHash
    reason = 'Latest upstream prerelease includes security fixes absent from stable v2.1.'
    changes = @('Install the signed WinFsp Core driver and user-mode runtime only.')
    noAutomaticReboot = $true; changesVssConfiguration = $false
    createsSnapshots = $false; modifiesOriginalProject = $false
}
if ($Action -eq 'Plan') { $plan | ConvertTo-Json -Depth 5; return }
New-Item -ItemType Directory -Force -Path $EvidenceDirectory | Out-Null
$evidence = [IO.Path]::GetFullPath($EvidenceDirectory)
if ($Action -eq 'InstallWinFsp') {
    if (-not $isAdmin) { throw 'AdministratorRequired: run this exact script with UAC approval.' }
    if (Test-Path 'HKLM:/SOFTWARE/WOW6432Node/WinFsp') { throw 'WinFspAlreadyInstalled: inspect the installed version; automatic replacement is disabled.' }
    $actualHash = (Get-FileHash -LiteralPath $plan.installer -Algorithm SHA256).Hash
    if ($actualHash -ne $installerHash) { throw 'InstallerHashMismatch' }
    $signature = Get-AuthenticodeSignature -LiteralPath $plan.installer
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'O=NAVIMATICS LLC,') { throw 'InstallerSignatureInvalid' }
    $log = Join-Path $evidence 'winfsp-install.log'
    $process = Start-Process -FilePath msiexec.exe -ArgumentList @('/i', ('"' + $plan.installer + '"'), 'ADDLOCAL=F.Main,F.User', '/qn', '/norestart', '/L*v', ('"' + $log + '"')) -WindowStyle Hidden -PassThru -Wait
    $plan.installExitCode = $process.ExitCode
    $plan.rebootRequired = $process.ExitCode -eq 3010
    $plan | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $evidence 'installation.json') -Encoding utf8
    if ($process.ExitCode -notin @(0, 3010)) { throw "InstallationFailed: $($process.ExitCode); see $log" }
}
$probe = [ordered]@{ observedAt = [DateTimeOffset]::UtcNow; elevated = $isAdmin; errors = @() }
foreach ($class in @('Win32_OperatingSystem', 'Win32_LogicalDisk', 'Win32_ShadowProvider', 'Win32_ShadowStorage', 'Win32_ShadowCopy')) {
    try {
        $probe[$class] = switch ($class) {
            'Win32_OperatingSystem' { Get-CimInstance $class | Select-Object Caption, Version, BuildNumber, FreePhysicalMemory }
            'Win32_LogicalDisk' { Get-CimInstance $class | Select-Object DeviceID, FileSystem, FreeSpace, Size }
            'Win32_ShadowProvider' { Get-CimInstance $class | Select-Object ID, Name, Type, Version }
            'Win32_ShadowStorage' { Get-CimInstance $class | Select-Object @{Name='Volume';Expression={$_.Volume.DeviceID}}, @{Name='DiffVolume';Expression={$_.DiffVolume.DeviceID}}, UsedSpace, AllocatedSpace, MaxSpace }
            'Win32_ShadowCopy' { Get-CimInstance $class | Select-Object ID, ProviderID, DeviceObject, VolumeName, Persistent, NoAutoRelease, Differential, InstallDate }
        }
    } catch { $probe.errors += [ordered]@{ source = $class; message = $_.Exception.Message } }
}
$probe.winfsp = if (Test-Path 'HKLM:/SOFTWARE/WOW6432Node/WinFsp') { Get-ItemProperty 'HKLM:/SOFTWARE/WOW6432Node/WinFsp' | Select-Object InstallDir } else { $null }
$probe | ConvertTo-Json -Depth 8 | Tee-Object -FilePath (Join-Path $evidence 'probe.json')
