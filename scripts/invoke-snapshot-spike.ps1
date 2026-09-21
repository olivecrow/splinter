[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OverlayRequestPath,
    [Parameter(Mandatory)][ValidateSet('mount','status','unmount','git-prepare','git-attach','git-release','unity-start','unity-status','unity-refresh','unity-stop','preserve')][string]$Action,
    [string]$ReceiptPath, [string]$Branch, [string]$Revision
)
$ErrorActionPreference = 'Stop'
$overlayPath = [IO.Path]::GetFullPath($OverlayRequestPath)
$request = @{action=$Action; overlayRequestPath=$overlayPath}
if ($ReceiptPath) { $request.receiptPath = [IO.Path]::GetFullPath($ReceiptPath) }
if ($Branch) { $request.branch = $Branch }
if ($Revision) { $request.revision = $Revision }
$inputPath = Join-Path ([IO.Path]::GetDirectoryName($overlayPath)) ('command-' + [Guid]::NewGuid().ToString('N') + '.json')
$request | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $inputPath -Encoding utf8
$cli = Join-Path $PSScriptRoot '../src/Splinter.Cli/bin/Release/net10.0/splinter.dll'
if (-not (Test-Path -LiteralPath $cli)) { throw 'BuildRequired: run pwsh scripts/build.ps1 first.' }
& dotnet $cli snapshot-spike --input $inputPath
exit $LASTEXITCODE
