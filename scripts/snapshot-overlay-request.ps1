# Shared request construction for development fixtures. Mount always rechecks
# this evidence with VssSnapshotProvider; constructing a request grants no access.
function New-SnapshotOverlayRequest {
    param(
        [Parameter(Mandatory)][string]$SnapshotReceipt,
        [Parameter(Mandatory)][hashtable]$Identity,
        [Parameter(Mandatory)][string]$DeltaPath,
        [Parameter(Mandatory)][string]$MountPath,
        [Parameter(Mandatory)][string]$ControlPath
    )
    $receiptPath = [IO.Path]::GetFullPath($SnapshotReceipt)
    $receiptDirectory = [IO.Path]::GetDirectoryName($receiptPath)
    $receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json -AsHashtable
    if ($receipt.version -ne 2 -or $null -eq $receipt.captureIdentity -or $receipt.released -or -not $receipt.ownerId) {
        throw 'SnapshotReceiptInvalid: a current unreleased capture receipt is required.'
    }
    $live = Get-Content -LiteralPath (Join-Path $receiptDirectory 'live-observation.json') -Raw | ConvertFrom-Json -AsHashtable
    $verification = Get-Content -LiteralPath (Join-Path $receiptDirectory 'verification-request.json') -Raw | ConvertFrom-Json -AsHashtable
    if ($live.available -ne $true -or $live.failureCode -or -not $live.requestId -or
        $live.requestId -ne $verification.requestId -or $live.ownerId -ne $receipt.ownerId -or
        $verification.ownerId -ne $receipt.ownerId -or $live.snapshotId -ne $receipt.snapshot.id -or
        $receipt.captureIdentity.snapshotId -ne $receipt.snapshot.id -or
        $live.deviceObject -notmatch '^\\\\\?\\GLOBALROOT\\Device\\HarddiskVolumeShadowCopy[0-9]+$') {
        throw 'SnapshotVerificationRequired: observe the exact snapshot before preparing a mount request.'
    }
    $Identity.snapshot = $receipt.snapshot
    return @{
        identity = $Identity; deltaPath = $DeltaPath; mountPath = $MountPath; controlPath = $ControlPath
        token = [Guid]::NewGuid().ToString('N'); snapshotReceiptDirectory = $receiptDirectory; snapshotOwnerId = $receipt.ownerId
        readAccess = @{snapshotId=$receipt.snapshot.id; rootPath=($live.deviceObject + '\' + $receipt.captureIdentity.sourceRelativePath); verificationId=$live.requestId}
    }
}
