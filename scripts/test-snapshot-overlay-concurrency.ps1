[CmdletBinding()]
param([Parameter(Mandatory)][string]$RunDirectory)
$ErrorActionPreference='Stop'
$run=[IO.Path]::GetFullPath($RunDirectory)
function Read-Window([string]$Path) {
    $data=New-Object byte[] 4096; $stream=[IO.FileStream]::new($Path,'Open','Read','ReadWrite')
    try { $stream.Position=16384; $stream.ReadExactly($data) } finally { $stream.Dispose() }
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($data))
}
$source=Join-Path $run 'source'; $a=Join-Path $run 'mount-a'; $b=Join-Path $run 'mount-b'
if([IO.File]::ReadAllText((Join-Path $source 'marker.txt')) -ne 't0-original') { throw 'ExpectedFreshSourceState' }
$before=Read-Window (Join-Path $source 'random-256.bin')
$patch=New-Object byte[] 4096; [Security.Cryptography.RandomNumberGenerator]::Fill($patch)
$stream=[IO.FileStream]::new((Join-Path $source 'random-256.bin'),'Open','ReadWrite','ReadWrite')
try { $stream.Position=16384; $stream.Write($patch); $stream.Flush($true) } finally { $stream.Dispose() }
$sourceAfter=Read-Window (Join-Path $source 'random-256.bin')
$aAfter=Read-Window (Join-Path $a 'random-256.bin'); $bAfter=Read-Window (Join-Path $b 'random-256.bin')
if($sourceAfter -eq $before -or $aAfter -ne $before -or $bAfter -ne $before) { throw 'SourceWriteIsolationFailed' }
[IO.File]::Move((Join-Path $source 'marker.txt'),(Join-Path $source 'renamed-source-marker.txt'))
[IO.File]::Delete((Join-Path $source 'random-16.bin'))
if(-not [IO.File]::Exists((Join-Path $b 'random-16.bin')) -or [IO.File]::Exists((Join-Path $b 'renamed-source-marker.txt'))) { throw 'SourceNamespaceIsolationFailed' }
if([IO.File]::ReadAllText((Join-Path $a 'marker.txt')) -ne 't0-original' -or [IO.File]::ReadAllText((Join-Path $b 'marker.txt')) -ne 't0-original') { throw 'SourceRenameChangedSnapshot' }
[IO.File]::WriteAllText((Join-Path $a 'marker.txt'),'a-private')
[IO.File]::WriteAllText((Join-Path $b 'marker.txt'),'b-private')
[IO.File]::WriteAllText((Join-Path $source 'marker.txt'),'t1-original')
if([IO.File]::ReadAllText((Join-Path $a 'marker.txt')) -ne 'a-private' -or [IO.File]::ReadAllText((Join-Path $b 'marker.txt')) -ne 'b-private') { throw 'PrivateWriteIsolationFailed' }
$result=[ordered]@{observedAt=[DateTimeOffset]::UtcNow;snapshotT0=(Get-Content (Join-Path $run 'snapshot-t0/snapshot.json') -Raw | ConvertFrom-Json).snapshot.id;beforeHash=$before;sourceAfterHash=$sourceAfter;aAfterHash=$aAfter;bAfterHash=$bAfter;sourceWritable=$true;originalRenameDeleteIsolated=$true;privateWritesIsolated=$true;sourceMarker='t1-original';aMarker='a-private';bMarker='b-private';newSnapshotRequired=$true}
$result | ConvertTo-Json -Depth 4 | Tee-Object -FilePath (Join-Path $run '04-source-and-private-writes.json')
