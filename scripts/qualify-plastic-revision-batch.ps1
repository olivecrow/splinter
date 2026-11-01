param(
    [Parameter(Mandatory)][string]$BasisReceipt,
    [Parameter(Mandatory)][string]$OutputPath
)
$ErrorActionPreference = 'Stop'
$basis = Get-Content -LiteralPath $BasisReceipt -Raw | ConvertFrom-Json
$manifest = $basis.manifest
if ($manifest.version -or !$manifest.entries) { throw 'Use the preserved legacy qualification receipt containing exact revision entries.' }
$items = @($manifest.entries | Where-Object { !$_.directory -and $_.length -gt 0 } | Select-Object -First 64)
if ($items.Count -ne 64 -or ($items | Measure-Object -Property length -Sum).Sum -gt 4MB) { throw 'The bounded qualification fixture must contain 64 small revisions.' }
$sourcePath = Join-Path $PSScriptRoot '../src/Splinter.SourceControl.Files/PlasticRevisionBridge.cs'
$source = [IO.File]::ReadAllText([IO.Path]::GetFullPath($sourcePath))
$command = '$ErrorActionPreference=''Stop''; $source=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String([Console]::ReadLine())); $types=Add-Type -PassThru -TypeDefinition $source; $bridge=$types | Where-Object FullName -eq ''Splinter.SourceControl.Files.PlasticRevisionBridge''; $bridge.GetMethod(''Run'').Invoke($null,@())'
$info = [Diagnostics.ProcessStartInfo]::new('pwsh')
$info.UseShellExecute = $false; $info.CreateNoWindow = $true
$info.RedirectStandardInput = $true; $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true
$info.Environment['SPLINTER_PLASTIC_READER_DIAGNOSTICS']='1'
foreach ($arg in @('-NoProfile','-NonInteractive','-EncodedCommand',[Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command)))) { $info.ArgumentList.Add($arg) }
$process = [Diagnostics.Process]::Start($info)
$errors = $process.StandardError.ReadToEndAsync()
$reader = [IO.BinaryReader]::new($process.StandardOutput.BaseStream)
function Receive-Revision($item) {
    $hash = [Security.Cryptography.IncrementalHash]::CreateHash([Security.Cryptography.HashAlgorithmName]::MD5)
    $sha = [Security.Cryptography.IncrementalHash]::CreateHash([Security.Cryptography.HashAlgorithmName]::SHA256)
    $count = 0L
    try {
        while (($length = $reader.ReadInt32()) -ne 0) {
            if ($length -lt 0 -or $length -gt 65536) { throw "Bridge rejected revision $($item.revision) with $length" }
            $buffer = $reader.ReadBytes($length)
            if ($buffer.Length -ne $length) { throw 'Truncated bridge frame.' }
            $hash.AppendData($buffer); $sha.AppendData($buffer); $count += $buffer.Length
        }
        if ($count -ne $item.length -or [Convert]::ToBase64String($hash.GetHashAndReset()) -cne $item.contentHash) { throw 'Exact revision identity/hash/length proof failed.' }
        return [Convert]::ToHexStringLower($sha.GetHashAndReset())
    } finally { $hash.Dispose(); $sha.Dispose() }
}
try {
    $repository = $manifest.treeSpec.Substring($manifest.treeSpec.IndexOf('@') + 1)
    $process.StandardInput.WriteLine([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($source)))
    $process.StandardInput.WriteLine((@{repository=$repository}|ConvertTo-Json -Compress)); $process.StandardInput.Flush()
    $warm = [Diagnostics.Stopwatch]::StartNew()
    $process.StandardInput.WriteLine([string]$items[0].revision); $process.StandardInput.Flush()
    $null = Receive-Revision $items[0]; $warm.Stop()
    $single = [Diagnostics.Stopwatch]::StartNew(); $singleProofs = @()
    foreach ($item in $items) { $process.StandardInput.WriteLine([string]$item.revision); $process.StandardInput.Flush(); $singleProofs += Receive-Revision $item }
    $single.Stop()
    $batch = @($items | ForEach-Object { @{revision=[string]$_.revision; length=[long]$_.length; contentHash=$_.contentHash} })
    $batched = [Diagnostics.Stopwatch]::StartNew(); $batchProofs = @()
    for ($index=0; $index -lt $items.Count; $index++) {
        if ($index -eq 0) { $line=@{requested=[string]$items[$index].revision;batch=$batch}|ConvertTo-Json -Depth 5 -Compress }
        else { $line=[string]$items[$index].revision }
        $process.StandardInput.WriteLine($line); $process.StandardInput.Flush(); $batchProofs += Receive-Revision $items[$index]
    }
    $batched.Stop()
    if ([string]::Join(',', $singleProofs) -cne [string]::Join(',', $batchProofs)) { throw 'Single and batch independent SHA256 proofs differ.' }
    $report = [ordered]@{version=1; observedAt=[DateTimeOffset]::UtcNow; basisReceipt=[IO.Path]::GetFullPath($BasisReceipt); treeSpec=$manifest.treeSpec; qualifiedClient='11.0.16.10484'; revisions=$items.Count; bytes=($items|Measure-Object length -Sum).Sum; processingMaximumRevisions=64; processingMaximumBytes=4MB; warmupSeconds=$warm.Elapsed.TotalSeconds; singleSeconds=$single.Elapsed.TotalSeconds; batchSeconds=$batched.Elapsed.TotalSeconds; exactMd5AndSha256Equal=$true; bridgeSourceSha256=(Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash.ToLowerInvariant(); scope='Read-only fixed revision protocol qualification; not whole workspace/Unity performance.'}
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($OutputPath))) | Out-Null
    [IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputPath), ($report|ConvertTo-Json -Depth 6))
    $report | ConvertTo-Json -Depth 6
} finally {
    $process.StandardInput.Close()
    if (!$process.WaitForExit(5000)) { $process.Kill($true); $process.WaitForExit() }
    $errorText=$errors.GetAwaiter().GetResult(); $reader.Dispose(); $process.Dispose()
    if ($errorText) { [Console]::Error.WriteLine($errorText) }
}
