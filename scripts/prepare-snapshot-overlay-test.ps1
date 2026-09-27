[CmdletBinding()]
param([Parameter(Mandatory)][ValidateSet('Source','Overlays')][string]$Action,[Parameter(Mandatory)][string]$RunDirectory,[string]$SnapshotReceipt,[string[]]$Names=@('a','b'),[switch]$Unity)
$ErrorActionPreference='Stop'; $root=[IO.Path]::GetFullPath($RunDirectory)
. (Join-Path $PSScriptRoot 'snapshot-overlay-request.ps1')
if($Action -eq 'Source') {
    if(Test-Path -LiteralPath $root){throw 'FreshRunDirectoryRequired'}
    $source=Join-Path $root 'source';New-Item -ItemType Directory -Path $source|Out-Null
    $rows=@();$buffer=New-Object byte[] 1048576
    foreach($mib in @(16,256,1024)) {
        $path=Join-Path $source "random-$mib.bin";$f=[IO.FileStream]::new($path,'CreateNew','Write','None')
        try {for($i=0;$i -lt $mib;$i++){[Security.Cryptography.RandomNumberGenerator]::Fill($buffer);$f.Write($buffer)};$f.Flush($true)}finally{$f.Dispose()}
        $item=Get-Item -LiteralPath $path
        if(($item.Attributes -band ([IO.FileAttributes]::SparseFile -bor [IO.FileAttributes]::Compressed)) -ne 0){throw 'NonSparseUncompressedSourceRequired'}
        $rows+=@{path=$path;logicalBytes=$item.Length;attributes=[string]$item.Attributes;sha256=(Get-FileHash -LiteralPath $path).Hash}
    }
    [IO.File]::WriteAllText((Join-Path $source 'marker.txt'),'t0-original')
    $rows|ConvertTo-Json -Depth 6|Set-Content "$root/source-files.json" -Encoding utf8
    @{sourcePath=$source;next='Explicitly capture source with vss-snapshot.ps1, then prepare Overlays.'}|ConvertTo-Json
}else{
    if(-not $SnapshotReceipt){throw 'SnapshotReceiptRequired'}
    foreach($name in $Names){
        if($name -notmatch '^[a-z][a-z0-9-]{0,30}$'){throw 'InvalidTestName'}
        $control=Join-Path $root "control-$name";if(Test-Path -LiteralPath $control){throw 'FreshControlRequired'}
        $identity=@{version=1;id=('so-'+[IO.Path]::GetFileName($root)+'-'+$name);maxJournalBytes=$(if($Unity){16GB}else{16MB});maxTransferBytes=1048576}
        if($Unity){$identity.excludedTopLevel=@('.git')}
        $request=New-SnapshotOverlayRequest -SnapshotReceipt $SnapshotReceipt -Identity $identity -DeltaPath "$root/delta-$name" -MountPath "$root/mount-$name" -ControlPath $control
        New-Item -ItemType Directory -Path $control|Out-Null
        $request | ConvertTo-Json -Depth 8 | Set-Content "$control/request.json" -Encoding utf8
    }
    @{requests=@($Names|ForEach-Object{"$root/control-$_/request.json"});mounted=$false}|ConvertTo-Json
}
