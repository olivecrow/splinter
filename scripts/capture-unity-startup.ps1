[CmdletBinding(DefaultParameterSetName = 'Capture')]
param(
    [Parameter(Mandatory, ParameterSetName = 'Capture')][ValidateSet('before-start', 'live')][string]$Stage,
    [Parameter(Mandatory, ParameterSetName = 'Capture')][string]$ControlPath,
    [Parameter(Mandatory, ParameterSetName = 'Capture')][string]$OutputPath,
    [Parameter(ParameterSetName = 'Capture')][string[]]$InputManifest = @(),
    [Parameter(ParameterSetName = 'Capture')][string]$BeforeCapture,
    [Parameter(ParameterSetName = 'Capture')][ValidateRange(1, 10000)][int]$MaxFiles = 2048,
    [Parameter(ParameterSetName = 'Capture')][ValidateRange(1, 256)][int]$MaxFileMiB = 32,
    [Parameter(ParameterSetName = 'Capture')][ValidateRange(1, 1024)][int]$MaxTotalMiB = 128,
    [Parameter(Mandatory, ParameterSetName = 'Capabilities')][switch]$Capabilities
)
$ErrorActionPreference = 'Stop'
if ($Capabilities) {
    [ordered]@{
        kind = 'development-unity-startup-capture'; version = 1
        stages = @('before-start', 'live'); launchesOrStopsEditor = $false
        requires = @('Windows', 'PowerShell 7', 'attached range-journal Unity workspace', 'new evidence output directory')
        captures = @('workspace/input identity', 'bounded Bee graph/input/response/trace files', 'Editor log', 'provider observations', 'UTC/monotonic anchors', 'ordered log phase analysis')
        exclusions = @('atomic whole-project snapshot', 'compile acceptance', 'automatic profiler injection', 'external tool state', 'release/discard')
    } | ConvertTo-Json -Depth 5
    return
}
if (-not ('Splinter.Development.UnityStartup.StartupCapture' -as [type])) {
    Add-Type -Path (Join-Path $PSScriptRoot 'internal/UnityStartupCapture.cs')
}
$options = @{
    stage = $Stage; controlPath = [IO.Path]::GetFullPath($ControlPath)
    outputPath = [IO.Path]::GetFullPath($OutputPath)
    inputManifests = @($InputManifest | ForEach-Object { [IO.Path]::GetFullPath($_) })
    beforeCapture = $(if ($BeforeCapture) { [IO.Path]::GetFullPath($BeforeCapture) } else { $null })
    maxFiles = $MaxFiles; maxFileBytes = [long]$MaxFileMiB * 1MB; maxTotalBytes = [long]$MaxTotalMiB * 1MB
} | ConvertTo-Json -Depth 5
[Splinter.Development.UnityStartup.StartupCapture]::Run($options)
