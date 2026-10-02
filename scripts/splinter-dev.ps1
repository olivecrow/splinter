# Explicit development entrypoint. No automatic build, installation or cleanup.
$ErrorActionPreference = 'Stop'
$taskRepositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($env:DOTNET_CLI_HOME)) {
    $env:DOTNET_CLI_HOME = Join-Path $taskRepositoryRoot '.local/dotnet'
} elseif (-not [IO.Path]::IsPathFullyQualified($env:DOTNET_CLI_HOME)) {
    $env:DOTNET_CLI_HOME = [IO.Path]::GetFullPath($env:DOTNET_CLI_HOME, $taskRepositoryRoot)
}
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$taskAssembly = Join-Path $taskRepositoryRoot 'tools/Splinter.Development/bin/Release/net10.0/splinter-dev.dll'
if (-not (Test-Path -LiteralPath $taskAssembly)) {
    throw 'Build Splinter first: pwsh ./scripts/build.ps1'
}
& dotnet $taskAssembly @args
exit $LASTEXITCODE
