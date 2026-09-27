[CmdletBinding()]
param([Parameter(Mandatory)][string]$RunDirectory)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'snapshot-overlay-request.ps1')
$root=[IO.Path]::GetFullPath($RunDirectory)
$control=Join-Path $root 'control-compat'; $requestPath=Join-Path $control 'request.json'
if(Test-Path -LiteralPath $requestPath){throw 'CompatibilityExperimentAlreadyPrepared'}
$request=New-SnapshotOverlayRequest -SnapshotReceipt "$root/snapshot-t0/snapshot.json" -Identity @{version=1;id='so-compat';maxJournalBytes=16777216;maxTransferBytes=65536} -DeltaPath "$root/delta-compat" -MountPath "$root/mount-compat" -ControlPath $control
New-Item -ItemType Directory -Path $control | Out-Null
$snapshot=$request.identity.snapshot
$request|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $requestPath -Encoding utf8
$invoke=Join-Path $PSScriptRoot 'invoke-snapshot-spike.ps1'
& $invoke -OverlayRequestPath $requestPath -Action mount | Out-Null
if($LASTEXITCODE -ne 0){throw 'CompatibilityMountFailed'}
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Collections.Concurrent;
public sealed class OverlayNotificationProbe : IDisposable {
    public readonly ConcurrentQueue<string> Events = new();
    private readonly FileSystemWatcher watcher;
    public OverlayNotificationProbe(string path) {
        watcher=new(path); watcher.NotifyFilter=NotifyFilters.FileName|NotifyFilters.Size|NotifyFilters.LastWrite;
        watcher.Created+=(s,e)=>Events.Enqueue("Created:"+e.Name);
        watcher.Changed+=(s,e)=>Events.Enqueue("Changed:"+e.Name);
        watcher.Renamed+=(s,e)=>Events.Enqueue("Renamed:"+e.OldName+":"+e.Name);
        watcher.Deleted+=(s,e)=>Events.Enqueue("Deleted:"+e.Name);
        watcher.Error+=(s,e)=>Events.Enqueue("Error:"+e.GetException().Message);
        watcher.EnableRaisingEvents=true;
    }
    public void Dispose()=>watcher.Dispose();
}
'@
$child=Join-Path $control 'read-child.ps1'
@'
param([string]$Path)
try { $f=[IO.File]::Open($Path,'Open','Read','ReadWrite'); try { $value=$f.ReadByte(); @{value=$value;windowsError=0}|ConvertTo-Json -Compress }finally{$f.Dispose()} }
catch { @{windowsError=($_.Exception.GetBaseException().HResult -band 65535)}|ConvertTo-Json -Compress }
'@|Set-Content -LiteralPath $child -Encoding utf8
$path=Join-Path $request.mountPath 'compatibility-owned.bin'
$renamed=Join-Path $request.mountPath 'compatibility-renamed.bin'
$watcher=[OverlayNotificationProbe]::new($request.mountPath)
try {
    [IO.File]::WriteAllBytes($path,[byte[]]@(77,12,31))
    $childRead=(& pwsh -NoProfile -File $child -Path $path)|ConvertFrom-Json
    if($childRead.value -ne 77){throw 'ChildReadMismatch'}
    $held=[IO.File]::Open($path,'Open','ReadWrite','None')
    try{$share=(& pwsh -NoProfile -File $child -Path $path)|ConvertFrom-Json}finally{$held.Dispose()}
    if($share.windowsError -ne 32){throw 'ShareLockNotEnforced'}
    $held=[IO.File]::Open($path,'Open','ReadWrite','ReadWrite')
    try{$held.Lock(0,1);$range=(& pwsh -NoProfile -File $child -Path $path)|ConvertFrom-Json;$held.Unlock(0,1)}finally{$held.Dispose()}
    if($range.windowsError -ne 33){throw 'ByteRangeLockNotEnforced'}
    [IO.File]::AppendAllText($path,'changed'); [IO.File]::Move($path,$renamed); [IO.File]::Delete($renamed)
    $deadline=[DateTimeOffset]::UtcNow.AddSeconds(10)
    do{$events=$watcher.Events.ToArray();if(($events -match '^Created:').Count -and ($events -match '^Changed:').Count -and ($events -match '^Renamed:').Count -and ($events -match '^Deleted:').Count){break};Start-Sleep -Milliseconds 100}while([DateTimeOffset]::UtcNow -lt $deadline)
    foreach($kind in @('Created','Changed','Renamed','Deleted')){if(-not ($events -match "^${kind}:")){throw "MissingNotification:$kind"}}
}finally{$watcher.Dispose()}
$stopped=(& $invoke -OverlayRequestPath $requestPath -Action unmount)|ConvertFrom-Json
if($LASTEXITCODE -ne 0){throw 'CompatibilityUnmountFailed'}
$state=Get-Content "$control/state.json" -Raw|ConvertFrom-Json
$out=Join-Path $root 'results/compat';New-Item -ItemType Directory -Path $out -Force|Out-Null
@{version=1;id=$request.identity.id;snapshotId=$snapshot.id;durableSequence=$state.storage.durableSequence;independentOfSnapshot=$true;files=@();deletedPaths=@();temporaryTestFilesRemoved=$true;journalSha256=(Get-FileHash "$($request.deltaPath)/delta.journal").Hash}|ConvertTo-Json -Depth 6|Set-Content "$out/result.json" -Encoding utf8
@{childRead=$childRead;shareLock=$share;byteRangeLock=$range;events=$events;unmounted=$stopped;observedAt=[DateTimeOffset]::UtcNow}|ConvertTo-Json -Depth 12|Set-Content "$root/05-notifications-locks-child-io.json" -Encoding utf8
@{passed=$true;events=$events;shareError=$share.windowsError;rangeError=$range.windowsError}|ConvertTo-Json
