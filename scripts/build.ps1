[CmdletBinding()]
param([switch]$NoRestore)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$names = @('DOTNET_CLI_HOME', 'NUGET_PACKAGES', 'DOTNET_CLI_TELEMETRY_OPTOUT', 'DOTNET_GENERATE_ASPNET_CERTIFICATE', 'DOTNET_ADD_GLOBAL_TOOLS_TO_PATH')
$previous = @{}
foreach ($name in $names) { $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
Push-Location $repoRoot
try {
    $env:DOTNET_CLI_HOME = Join-Path $repoRoot '.local/dotnet'
    $env:NUGET_PACKAGES = Join-Path $repoRoot '.local/nuget/packages'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    $env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
    if (-not $NoRestore) {
        & dotnet restore Splinter.slnx --locked-mode --disable-build-servers --nologo
        if ($LASTEXITCODE -ne 0) { throw "Restore failed: $LASTEXITCODE" }
    }
    & dotnet build Splinter.slnx --no-restore --disable-build-servers --nologo --configuration Release
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $LASTEXITCODE" }
    & dotnet test --solution Splinter.slnx --no-build --no-restore --configuration Release --report-trx --results-directory (Join-Path $repoRoot 'artifacts/test-results')
    if ($LASTEXITCODE -ne 0) { throw "Tests failed: $LASTEXITCODE" }
}
finally {
    Pop-Location
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
}
