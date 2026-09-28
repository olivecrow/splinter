[CmdletBinding()]
param([Parameter(Mandatory)][string]$OutputPath)
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$output = [IO.Path]::GetFullPath($OutputPath)
if (Test-Path -LiteralPath $output) { throw 'Choose a new package directory; existing packages are preserved.' }
$env:DOTNET_CLI_HOME = Join-Path $repository '.local/dotnet'
$env:NUGET_PACKAGES = Join-Path $repository '.local/nuget/packages'
& dotnet publish (Join-Path $repository 'src/Splinter.Platform.Windows/Splinter.Platform.Windows.csproj') --no-restore --configuration Release --output $output --nologo --disable-build-servers
if ($LASTEXITCODE -ne 0) { throw 'Storage service publication failed.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'vss-snapshot.ps1') -Destination (Join-Path $output 'vss-snapshot.ps1')
[IO.File]::WriteAllText((Join-Path $output 'service-version.json'), '{"version":1,"protocolVersion":1}', [Text.UTF8Encoding]::new($false))
$files = @(Get-ChildItem -LiteralPath $output -File | Sort-Object Name | ForEach-Object {
    [ordered]@{name=$_.Name; sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
})
if (Get-ChildItem -LiteralPath $output -Directory) { throw 'Review package subdirectories before installing; only an explicit flat manifest is supported.' }
[IO.File]::WriteAllText((Join-Path $output 'package.json'), ([ordered]@{version=1; protocolVersion=1; files=$files} | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
[ordered]@{packagePath=$output; fileCount=$files.Count; manifestHash=(Get-FileHash -LiteralPath (Join-Path $output 'package.json')).Hash.ToLowerInvariant()} | ConvertTo-Json
