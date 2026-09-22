[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Capture','Observe','Release')][string]$Action,
    [Parameter(Mandatory)][string]$ReceiptDirectory,
    [string]$SourcePath,
    [string]$OwnerId
)
$ErrorActionPreference = 'Stop'
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
if (-not ([Security.Principal.WindowsPrincipal]::new($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'AdministratorRequired' }
$systemProvider = '{B5946137-7B9F-4925-AF80-51ABD60B20D5}'
$receiptRoot = [IO.Path]::GetFullPath($ReceiptDirectory)
New-Item -ItemType Directory -Force -Path $receiptRoot | Out-Null
$receiptPath = Join-Path $receiptRoot 'snapshot.json'
$lock = [IO.File]::Open((Join-Path $receiptRoot 'request.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
function Save-Json([string]$Path, $Value) {
    if (Test-Path -LiteralPath ($Path + '.new')) { throw 'SnapshotObservationIncomplete: preserve the interrupted atomic write.' }
    $bytes = [Text.Encoding]::UTF8.GetBytes(($Value | ConvertTo-Json -Depth 7))
    $stream = [IO.FileStream]::new(($Path + '.new'), 'Create', 'Write', 'None')
    try { $stream.Write($bytes); $stream.Flush($true) } finally { $stream.Dispose() }
    [IO.File]::Move(($Path + '.new'), $Path, $true)
}
function Read-LiveIdentity($Shadow, $Receipt) {
    if ($Receipt.snapshot.rootPath -notmatch '^\\\\\?\\GLOBALROOT\\Device\\HarddiskVolumeShadowCopy[0-9]+(?:\\(.*))?$') { throw 'SnapshotIdentityMismatch' }
    $relativePath = [string]$Matches[1]
    if ($relativePath -and ($relativePath.Contains('/') -or $relativePath.Contains(':') -or
        @($relativePath.Split('\') | Where-Object { -not $_ -or $_ -eq '.' -or $_ -eq '..' -or $_.EndsWith('.') -or $_.EndsWith(' ') }).Count)) { throw 'SnapshotIdentityMismatch' }
    if ($Shadow.ID -ne $Receipt.snapshot.id -or $Shadow.ProviderID -ne $Receipt.nativeProviderId -or
        $Shadow.ProviderID -ne $systemProvider -or -not $Shadow.Persistent -or -not $Shadow.Differential -or -not $Shadow.NoAutoRelease -or
        ([DateTimeOffset]$Shadow.InstallDate) -ne ([DateTimeOffset]$Receipt.snapshot.capturedAt) -or
        $Shadow.VolumeName -notmatch '^\\\\\?\\Volume\{[0-9a-fA-F-]{36}\}\\$' -or
        $Shadow.DeviceObject -notmatch '^\\\\\?\\GLOBALROOT\\Device\\HarddiskVolumeShadowCopy[0-9]+$') { throw 'SnapshotIdentityMismatch' }
    $liveIdentity = [ordered]@{snapshotId=$Shadow.ID; nativeProviderId=$Shadow.ProviderID; sourceVolume=$Shadow.VolumeName;
        sourceRelativePath=$relativePath; capturedAt=([DateTimeOffset]$Shadow.InstallDate); persistent=[bool]$Shadow.Persistent;
        differential=[bool]$Shadow.Differential; noAutoRelease=[bool]$Shadow.NoAutoRelease; provenance='live'}
    $captured = $Receipt.captureIdentity
    $verifiedPath = Join-Path $receiptRoot 'verified-identity.json'
    if ($null -eq $captured -and (Test-Path -LiteralPath $verifiedPath)) {
        $verified = Get-Content -LiteralPath $verifiedPath -Raw | ConvertFrom-Json -AsHashtable
        if ($verified.ownerId -ne $Receipt.ownerId -or $verified.snapshot.id -ne $Receipt.snapshot.id -or
            $verified.snapshot.rootPath -cne $Receipt.snapshot.rootPath -or $verified.snapshot.sourcePath -cne $Receipt.snapshot.sourcePath) { throw 'SnapshotIdentityMismatch' }
        $captured = $verified.identity
    }
    if ($null -ne $captured) {
        foreach ($field in @('snapshotId','nativeProviderId','sourceVolume','sourceRelativePath','persistent','differential','noAutoRelease')) {
            if ($captured[$field] -ne $liveIdentity[$field]) { throw 'SnapshotIdentityMismatch' }
        }
        if (([DateTimeOffset]$captured.capturedAt) -ne $liveIdentity.capturedAt) { throw 'SnapshotIdentityMismatch' }
    } else {
        # Preserve the legacy capture receipt. These values are verified now,
        # from the exact original ID, never inferred from the mutable source.
        $liveIdentity.provenance = 'legacy-revalidated'
        Save-Json $verifiedPath ([ordered]@{ownerId=$Receipt.ownerId; snapshot=$Receipt.snapshot; identity=$liveIdentity})
    }
    return $liveIdentity
}
function Measure-Storage {
    [ordered]@{
        volumes = @(Get-CimInstance Win32_LogicalDisk | Select-Object DeviceID, FileSystem, FreeSpace, Size)
        shadowStorage = @(Get-CimInstance Win32_ShadowStorage | Select-Object @{Name='volume';Expression={$_.Volume.DeviceID}}, @{Name='diffVolume';Expression={$_.DiffVolume.DeviceID}}, UsedSpace, AllocatedSpace, MaxSpace)
    }
}
try {
    if ($Action -eq 'Capture' -and -not (Test-Path -LiteralPath $receiptPath)) {
        if (-not $SourcePath -or -not $OwnerId) { throw 'SourceAndOwnerRequired' }
        $source = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($SourcePath))
        if (-not (Test-Path -LiteralPath $source -PathType Container)) { throw 'SourceMissing' }
        $volumeRoot = [IO.Path]::GetPathRoot($source)
        $volume = Get-CimInstance Win32_Volume | Where-Object { $_.Name -eq $volumeRoot }
        if ($null -eq $volume -or $volume.FileSystem -ne 'NTFS') { throw 'NtfsVolumeRequired' }
        $intentPath = Join-Path $receiptRoot 'capture-intent.json'
        $returnPath = Join-Path $receiptRoot 'capture-return.json'
        if (Test-Path -LiteralPath $intentPath) {
            $intent = Get-Content -LiteralPath $intentPath -Raw | ConvertFrom-Json
            if ($intent.ownerId -ne $OwnerId -or $intent.sourcePath -ne $source) { throw 'SnapshotOwnerMismatch' }
            if (-not (Test-Path -LiteralPath $returnPath)) { throw 'CaptureReconcileRequired: capture intent has no returned ID; preserve and inspect instead of creating another snapshot.' }
            $created = Get-Content -LiteralPath $returnPath -Raw | ConvertFrom-Json
            $previous = @($intent.previousSnapshotIds)
            $elapsed = $created.elapsedMilliseconds
            if ($previous -contains $created.shadowId) { throw 'SnapshotOwnerMismatch' }
        } else {
            $storage = @(Get-CimInstance Win32_ShadowStorage | Where-Object { $_.Volume.DeviceID -eq $volume.DeviceID })
            if ($storage.Count -ne 1 -or ($storage[0].MaxSpace - $storage[0].UsedSpace) -lt 8GB) { throw 'SnapshotHeadroomRequired: retain at least 8 GiB before capturing; no configuration changes were made.' }
            $previous = @(Get-CimInstance Win32_ShadowCopy | Select-Object -ExpandProperty ID)
            Save-Json $intentPath ([ordered]@{ownerId=$OwnerId; sourcePath=$source; previousSnapshotIds=$previous; before=(Measure-Storage); startedAt=[DateTimeOffset]::UtcNow})
            $watch = [Diagnostics.Stopwatch]::StartNew()
            $created = Invoke-CimMethod -ClassName Win32_ShadowCopy -MethodName Create -Arguments @{ Volume=$volumeRoot; Context='ClientAccessible' }
            $elapsed = $watch.ElapsedMilliseconds
            Save-Json $returnPath ([ordered]@{returnValue=$created.ReturnValue; shadowId=$created.ShadowID; elapsedMilliseconds=$elapsed})
        }
        if ($created.ReturnValue -ne 0) { throw "SnapshotCaptureFailed: $($created.ReturnValue)" }
        $shadow = Get-CimInstance Win32_ShadowCopy | Where-Object ID -eq $created.ShadowID
        if ($null -eq $shadow) { throw 'SnapshotMissing: the captured ID is gone; do not recapture this request.' }
        # Write the owned ID before validating so an unsupported result can be
        # reclaimed by exact receipt instead of a dangerous global delete.
        $relative = $source.Substring($volumeRoot.Length)
        $snapshot = [ordered]@{ id=$shadow.ID; provider='windows-vss-system'; sourcePath=$source; rootPath=($shadow.DeviceObject + '\' + $relative); capturedAt=([DateTimeOffset]$shadow.InstallDate); persistent=[bool]$shadow.Persistent; differential=[bool]$shadow.Differential; consistency='filesystem-crash-consistent-no-application-writer' }
        $captureIdentity = [ordered]@{snapshotId=$shadow.ID; nativeProviderId=$shadow.ProviderID; sourceVolume=$shadow.VolumeName;
            sourceRelativePath=$relative; capturedAt=([DateTimeOffset]$shadow.InstallDate); persistent=[bool]$shadow.Persistent;
            differential=[bool]$shadow.Differential; noAutoRelease=[bool]$shadow.NoAutoRelease; provenance='capture'}
        $receipt = [ordered]@{version=2; ownerId=$OwnerId; snapshot=$snapshot; nativeProviderId=$shadow.ProviderID; noAutoRelease=$shadow.NoAutoRelease; captureIdentity=$captureIdentity; initialPayloadCopiedBytes=0; elapsedMilliseconds=$elapsed; beforeSnapshotIds=$previous; after=(Measure-Storage); released=$false}
        Save-Json $receiptPath $receipt
        if ($shadow.ProviderID -ne $systemProvider -or -not $shadow.Differential -or -not $shadow.Persistent -or -not $shadow.NoAutoRelease) { throw 'SnapshotProviderUnsupported: preserve the receipt and release only this owned ID.' }
    }
    if (-not (Test-Path -LiteralPath $receiptPath)) { throw 'SnapshotReceiptRequired' }
    $receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json -AsHashtable
    if ($OwnerId -and $receipt.ownerId -ne $OwnerId) { throw 'SnapshotOwnerMismatch' }
    if (($receipt.version -and $receipt.version -notin @(1,2)) -or ($receipt.version -eq 2 -and $null -eq $receipt.captureIdentity)) { throw 'SnapshotIdentityMismatch' }
    $bootTime = (Get-CimInstance Win32_OperatingSystem -Property LastBootUpTime).LastBootUpTime.ToUniversalTime()
    $verificationPath = Join-Path $receiptRoot 'verification-request.json'
    if (-not (Test-Path -LiteralPath $verificationPath)) {
        Save-Json $verificationPath ([ordered]@{requestId=[Guid]::NewGuid().ToString('N'); ownerId=$receipt.ownerId;
            snapshotId=$receipt.snapshot.id; requestedAt=[DateTimeOffset]::UtcNow; bootTimeUtc=$bootTime})
    }
    $verification = Get-Content -LiteralPath $verificationPath -Raw | ConvertFrom-Json
    if ($verification.ownerId -ne $receipt.ownerId -or ($verification.snapshotId -and $verification.snapshotId -ne $receipt.snapshot.id)) { throw 'SnapshotIdentityMismatch' }
    $shadow = Get-CimInstance Win32_ShadowCopy | Where-Object ID -eq $receipt.snapshot.id
    $liveIdentity = if ($null -ne $shadow) { Read-LiveIdentity $shadow $receipt } else { $null }
    if ($Action -eq 'Release') {
        if (-not $OwnerId) { throw 'SnapshotOwnerRequired' }
        # Experiment adapter: callers must establish result preservation and no
        # dependencies. The gate file contains their explicit reviewed evidence.
        $gatePath = Join-Path $receiptRoot 'release-authorized.json'
        if (-not (Test-Path -LiteralPath $gatePath)) { throw 'SnapshotReleaseGateRequired' }
        $gate = Get-Content -LiteralPath $gatePath -Raw | ConvertFrom-Json
        if ($gate.snapshotId -ne $receipt.snapshot.id -or $gate.remainingDependents -ne 0 -or -not $gate.resultsPreserved) { throw 'SnapshotReleaseGateInvalid' }
        if ($null -ne $shadow) {
            if ($shadow.ProviderID -ne $receipt.nativeProviderId) { throw 'SnapshotProviderMismatch' }
            $shadow | Remove-CimInstance
        }
        if ($null -ne (Get-CimInstance Win32_ShadowCopy | Where-Object ID -eq $receipt.snapshot.id)) { throw 'SnapshotReleaseIncomplete' }
        $receipt.released = $true; $receipt.releasedAt = [DateTimeOffset]::UtcNow; $receipt.afterRelease = Measure-Storage
        Save-Json $receiptPath $receipt
    }
    $shadow = Get-CimInstance Win32_ShadowCopy | Where-Object ID -eq $receipt.snapshot.id
    $available = $null -ne $shadow
    $liveIdentity = if ($available) { Read-LiveIdentity $shadow $receipt } else { $null }
    # Bind an absence proof to this OS boot. The normal caller cannot query VSS
    # without elevation, but can compare the read-only OS boot timestamp.
    $bootTime = (Get-CimInstance Win32_OperatingSystem -Property LastBootUpTime).LastBootUpTime.ToUniversalTime()
    $observation = [ordered]@{ownerId=$receipt.ownerId; snapshot=$receipt.snapshot; available=$available; released=$receipt.released; bootTimeUtc=$bootTime; observedAt=[DateTimeOffset]::UtcNow; storage=(Measure-Storage)}
    Save-Json (Join-Path $receiptRoot 'observation.json') $observation
    Save-Json (Join-Path $receiptRoot 'live-observation.json') ([ordered]@{requestId=$verification.requestId; ownerId=$receipt.ownerId;
        snapshotId=$receipt.snapshot.id; bootTimeUtc=$bootTime; observedAt=[DateTimeOffset]::UtcNow; available=$available;
        failureCode=$null; identity=$liveIdentity; deviceObject=$(if ($available) { $shadow.DeviceObject } else { $null })})
    Save-Json (Join-Path $receiptRoot 'operation-result.json') ([ordered]@{action=$Action; success=$true; completedAt=[DateTimeOffset]::UtcNow})
    $observation | ConvertTo-Json -Depth 7
} catch {
    if ($null -ne $verification -and $null -ne $receipt) {
        $failureCode = if ($_.Exception.Message -eq 'SnapshotIdentityMismatch') { 'SnapshotIdentityMismatch' } else { 'SnapshotObservationUnknown' }
        Save-Json (Join-Path $receiptRoot 'live-observation.json') ([ordered]@{requestId=$verification.requestId; ownerId=$receipt.ownerId;
            snapshotId=$receipt.snapshot.id; bootTimeUtc=$bootTime; observedAt=[DateTimeOffset]::UtcNow; available=$null;
            failureCode=$failureCode; identity=$null; deviceObject=$null})
    }
    Save-Json (Join-Path $receiptRoot 'operation-result.json') ([ordered]@{action=$Action; success=$false; error=$_.Exception.Message; completedAt=[DateTimeOffset]::UtcNow})
    throw
} finally { $lock.Dispose() }
