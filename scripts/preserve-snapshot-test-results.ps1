[CmdletBinding()]
param([Parameter(Mandatory)][string]$RunDirectory, [Parameter(Mandatory)][ValidateSet('a','b','c','fault','quota')][string]$Name)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($RunDirectory)
$control=Join-Path $root ('control-' + $Name)
$request=Get-Content -LiteralPath (Join-Path $control 'request.json') -Raw | ConvertFrom-Json
$outputRoot=Join-Path $root ('results/' + $Name)
$receiptPath=Join-Path $outputRoot 'result.json'
if(Test-Path -LiteralPath $receiptPath) { Get-Content -LiteralPath $receiptPath -Raw; return }
$before=Get-Content -LiteralPath (Join-Path $control 'state.json') -Raw | ConvertFrom-Json
if($before.phase -ne 'mounted' -or $before.openHandles -ne 0) { throw 'CloseClientsBeforeResultPreservation' }
$paths=switch($Name) {
    'a' {@('marker.txt','random-256.bin','random-1024.bin','renamed.bin')}
    'b' {@('marker.txt','random-16.bin','random-256.bin','random-1024.bin')}
    'c' {@()}
    'fault' {@('acknowledged.bin','stream.bin')}
    'quota' {@('quota.bin')}
}
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$results=@(); $buffer=New-Object byte[] 1048576
foreach($relative in $paths) {
    $source=Join-Path $request.mountPath $relative; $destination=Join-Path $outputRoot $relative
    $reuse=Test-Path -LiteralPath $destination
    $hash=[Security.Cryptography.IncrementalHash]::CreateHash([Security.Cryptography.HashAlgorithmName]::SHA256)
    $input=[IO.FileStream]::new($source,'Open','Read','Read',1048576,[IO.FileOptions]::SequentialScan)
    $output=if($reuse){$null}else{[IO.FileStream]::new($destination,'CreateNew','Write','None',1048576,[IO.FileOptions]::SequentialScan)}
    $watch=[Diagnostics.Stopwatch]::StartNew();$bytes=0L
    try {
        while(($count=$input.Read($buffer,0,$buffer.Length)) -ne 0) { $hash.AppendData($buffer,0,$count);if($output){$output.Write($buffer,0,$count)};$bytes+=$count }
        if($output){$output.Flush($true)}
    } finally {$input.Dispose();if($output){$output.Dispose()}}
    $expected=[Convert]::ToHexString($hash.GetHashAndReset());$hash.Dispose()
    $actual=(Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
    if($actual -ne $expected){throw 'IndependentResultHashMismatch'}
    $results += [ordered]@{path=$relative;bytes=$bytes;sha256=$actual;elapsedMilliseconds=$watch.ElapsedMilliseconds}
    [ordered]@{preserved=$relative;bytes=$bytes;elapsedMilliseconds=$watch.ElapsedMilliseconds} | ConvertTo-Json -Compress
}
$after=Get-Content -LiteralPath (Join-Path $control 'state.json') -Raw | ConvertFrom-Json
if($after.storage.durableSequence -ne $before.storage.durableSequence) { throw 'DeltaChangedDuringResultPreservation' }
$journal=[IO.File]::Open((Join-Path $request.deltaPath 'delta.journal'),'Open','Read','ReadWrite')
try{$journalHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($journal))}finally{$journal.Dispose()}
$receipt=[ordered]@{version=1;id=$request.identity.id;snapshotId=$request.identity.snapshot.id;durableSequence=$before.storage.durableSequence;independentOfSnapshot=$true;files=$results;deletedPaths=$(if($Name -eq 'a'){@('random-16.bin')}else{@()});unchangedSnapshotViewOnly=($Name -eq 'c');journalSha256=$journalHash;preservedAt=[DateTimeOffset]::UtcNow;resultMaterializationIsCountedSeparatelyFromInitialWorkspaceCopy=$true}
$receipt | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath $receiptPath -Encoding utf8
$receipt | ConvertTo-Json -Depth 7
