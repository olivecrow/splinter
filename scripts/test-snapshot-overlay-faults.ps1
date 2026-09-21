[CmdletBinding()]
param([Parameter(Mandatory)][string]$RunDirectory)
$ErrorActionPreference='Stop'
$faultRoot=[IO.Path]::GetFullPath($RunDirectory)
$snapshot=(Get-Content -LiteralPath (Join-Path $faultRoot 'snapshot-t1/snapshot.json') -Raw | ConvertFrom-Json).snapshot
$invoke=Join-Path $PSScriptRoot 'invoke-snapshot-spike.ps1'
function Call-Overlay([string]$RequestPath,[string]$Action) {
    $output=& $invoke -OverlayRequestPath $RequestPath -Action $Action
    $value=$output | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or $value.status -ne 'Succeeded') { throw ($output -join "`n") }
    return $value.data.result
}
function Make-Overlay([string]$Name,[long]$Limit) {
    $control=Join-Path $faultRoot ('control-' + $Name); $requestPath=Join-Path $control 'request.json'
    if (Test-Path -LiteralPath $requestPath) { throw ('FaultTestAlreadyPrepared: ' + $requestPath) }
    New-Item -ItemType Directory -Path $control | Out-Null
    [ordered]@{identity=[ordered]@{version=1;id=('so-' + $Name);snapshot=$snapshot;maxJournalBytes=$Limit;maxTransferBytes=65536};deltaPath=(Join-Path $faultRoot ('delta-' + $Name));mountPath=(Join-Path $faultRoot ('mount-' + $Name));controlPath=$control;token=[Guid]::NewGuid().ToString('N')} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $requestPath -Encoding utf8
    return $requestPath
}
function Save([string]$Name,$Value) { $Value | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $faultRoot ($Name + '.json')) -Encoding utf8 }
$faultRequest=Make-Overlay 'fault' 16777216
$started=Call-Overlay $faultRequest 'mount'; $request=Get-Content -LiteralPath $faultRequest -Raw | ConvertFrom-Json
$dataPath=Join-Path $request.mountPath 'acknowledged.bin'
$sentinel=[Security.Cryptography.RandomNumberGenerator]::GetBytes(4096)
[IO.File]::WriteAllBytes($dataPath,$sentinel)
$held=[IO.File]::Open($dataPath,'Open','Read','Read')
try {
    @{token=$request.token} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $request.controlPath 'stop.json') -Encoding utf8
    Start-Sleep -Milliseconds 2200
    $heldState=Call-Overlay $faultRequest 'status'
    if (-not $heldState.processAlive -or $heldState.state.openHandles -lt 1) { throw 'UnmountDidNotDeferForOpenHandle' }
} finally { $held.Dispose() }
$unmounted=Call-Overlay $faultRequest 'unmount'
$restarted=Call-Overlay $faultRequest 'mount'
if ([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($dataPath))) -ne [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($sentinel))) { throw 'AcknowledgedDataLostAfterStop' }
Save '06-held-handle-and-reconnect' @{held=$heldState;unmounted=$unmounted;reconnected=$restarted;preservedHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($sentinel))}

# Kill the exact owned provider while one child issues write-through requests.
# Completed acknowledgements are independently recorded outside the mount.
$writerPath=Join-Path $request.controlPath 'writer.ps1'
@'
param([string]$DataPath,[string]$AcknowledgementPath)
$ErrorActionPreference='Stop'; $n=0
try {
    $stream=[IO.FileStream]::new($DataPath,'Create','ReadWrite','Read',65536,[IO.FileOptions]::WriteThrough)
    try {
        while($n -lt 200) {
            $block=New-Object byte[] 65536; [Array]::Fill[byte]($block,[byte]($n % 251))
            $stream.Write($block); $stream.Flush($true); $n++
            [IO.File]::WriteAllText($AcknowledgementPath + '.new',[string]$n)
            [IO.File]::Move($AcknowledgementPath + '.new',$AcknowledgementPath,$true)
            Start-Sleep -Milliseconds 15
        }
    } finally {$stream.Dispose()}
} catch { [IO.File]::WriteAllText($AcknowledgementPath + '.error.txt',$_.Exception.Message) }
'@ | Set-Content -LiteralPath $writerPath -Encoding utf8
$ack=Join-Path $request.controlPath 'ack.txt'; $streamPath=Join-Path $request.mountPath 'stream.bin'
$writer=Start-Process pwsh -WindowStyle Hidden -ArgumentList @('-NoProfile','-File',('"' + $writerPath + '"'),'-DataPath',('"' + $streamPath + '"'),'-AcknowledgementPath',('"' + $ack + '"')) -PassThru
$writerStart=$writer.StartTime.ToUniversalTime().Ticks
$deadline=[DateTimeOffset]::UtcNow.AddSeconds(20)
while(( -not (Test-Path -LiteralPath $ack) -or [int](Get-Content -LiteralPath $ack -Raw) -lt 5) -and [DateTimeOffset]::UtcNow -lt $deadline -and -not $writer.HasExited) { Start-Sleep -Milliseconds 30; $writer.Refresh() }
if (-not (Test-Path -LiteralPath $ack)) { throw 'WriterDidNotAcknowledge' }
$beforeKill=Call-Overlay $faultRequest 'status'
$provider=Get-Process -Id $beforeKill.state.processId
if ($provider.StartTime.ToUniversalTime().Ticks -ne $beforeKill.state.processStartTimeUtcTicks -or $beforeKill.state.token -ne $request.token) { throw 'ProviderOwnershipMismatch' }
Stop-Process -Id $provider.Id
if (-not $writer.WaitForExit(15000)) {
    if ($writer.StartTime.ToUniversalTime().Ticks -ne $writerStart) { throw 'WriterOwnershipMismatch' }
    Stop-Process -Id $writer.Id
    throw 'WriterDidNotReceiveMountLoss'
}
$acknowledged=[int](Get-Content -LiteralPath $ack -Raw)
$afterKill=Call-Overlay $faultRequest 'status'
$resumed=Call-Overlay $faultRequest 'mount'
$stream=[IO.File]::OpenRead($streamPath)
try {
    for($n=0;$n -lt $acknowledged;$n++) {
        $block=New-Object byte[] 65536; $stream.ReadExactly($block)
        if(@($block | Where-Object {$_ -ne [byte]($n % 251)}).Count -ne 0) { throw ('LostAcknowledgedBlock: ' + $n) }
    }
} finally {$stream.Dispose()}
Save '06-provider-kill-during-write' @{before=$beforeKill;after=$afterKill;resumed=$resumed;acknowledgedBlocks=$acknowledged;acknowledgedBytes=($acknowledged*65536L);writerExited=$writer.HasExited;writerError=(Get-Content -LiteralPath ($ack+'.error.txt') -Raw -ErrorAction SilentlyContinue);sameSnapshot=($resumed.state.snapshotId -eq $beforeKill.state.snapshotId)}

$quotaRequest=Make-Overlay 'quota' 65536
$quotaMount=Call-Overlay $quotaRequest 'mount'; $quota=Get-Content -LiteralPath $quotaRequest -Raw | ConvertFrom-Json
$quotaPath=Join-Path $quota.mountPath 'quota.bin'; $errorCode=0; $accepted=0
$stream=[IO.FileStream]::new($quotaPath,'Create','ReadWrite','Read',4096,[IO.FileOptions]::WriteThrough)
try {
    try { for($n=0;$n -lt 100;$n++) { $stream.Write($sentinel); $stream.Flush($true); $accepted++ } }
    catch { $errorCode=$_.Exception.GetBaseException().HResult -band 65535 }
} finally {try{$stream.Dispose()}catch{}}
if ($errorCode -ne 112 -or $accepted -lt 1) { throw ('ExpectedWindowsDiskFull112: code=' + $errorCode + ', accepted=' + $accepted) }
$quotaBefore=Call-Overlay $quotaRequest 'status'; $quotaStop=Call-Overlay $quotaRequest 'unmount'; $quotaResume=Call-Overlay $quotaRequest 'mount'
$stream=[IO.File]::OpenRead($quotaPath)
try {for($n=0;$n -lt $accepted;$n++) {$actual=New-Object byte[] 4096;$stream.ReadExactly($actual);if([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($actual)) -ne [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($sentinel))) {throw 'QuotaLostAcknowledgedData'}}}finally{$stream.Dispose()}
Save '06-quota-disk-full' @{windowsError=$errorCode;acceptedBytes=($accepted*4096L);before=$quotaBefore;unmounted=$quotaStop;resumed=$quotaResume;physicalHostVolumeExhaustionTested=$false;fault='enforced delta volume budget returned real Windows ERROR_DISK_FULL'}
@{completed=@('held-handle-unmount','same-snapshot-reconnect','provider-kill-during-write','quota-disk-full');faultRequest=$faultRequest;quotaRequest=$quotaRequest;resourcesRemainForResultPreservation=$true} | ConvertTo-Json
