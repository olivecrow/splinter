# Keep one stable CLI entrypoint for agents; build explicitly with build.ps1.
$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($env:DOTNET_CLI_HOME)) {
    $env:DOTNET_CLI_HOME = Join-Path $repositoryRoot '.local/dotnet'
} elseif (-not [IO.Path]::IsPathFullyQualified($env:DOTNET_CLI_HOME)) {
    $env:DOTNET_CLI_HOME = [IO.Path]::GetFullPath($env:DOTNET_CLI_HOME, $repositoryRoot)
}
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$assembly = Join-Path $PSScriptRoot '../src/Splinter.Cli/bin/Release/net10.0/splinter.dll'
if (-not (Test-Path -LiteralPath $assembly)) {
    throw 'Build Splinter first: pwsh ./scripts/build.ps1'
}
& dotnet $assembly @args
exit $LASTEXITCODE
