param(
    [Parameter(Mandatory)][string]$EditorPath,
    [Parameter(Mandatory)][string]$ScratchRoot,
    [Parameter(Mandatory)][string]$OutputPath,
    [ValidateRange(1,5)][int]$Cycles = 5,
    [string[]]$ConsentValueNames = @()
)
# Finite diagnostic on fresh, disposable projects only; never accepts an existing project.
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'internal/UnitySettingsIdentity.psm1')
$editor = [IO.Path]::GetFullPath($EditorPath)
$scratch = [IO.Path]::GetFullPath($ScratchRoot)
$output = [IO.Path]::GetFullPath($OutputPath)
if (-not (Test-Path -LiteralPath $editor -PathType Leaf)) { throw 'Installed Editor executable is required.' }
if ((Test-Path -LiteralPath $scratch) -or (Test-Path -LiteralPath $output)) { throw 'Scratch and output paths must both be fresh. Preserve earlier trials.' }
$version = (Get-Item -LiteralPath $editor).VersionInfo.ProductVersion.Split('_')[0]
if ($version -notmatch '^6000\.3\.\d+f\d+$') { throw 'This diagnostic is qualified for the installed Unity 6000.3 Editor only.' }
$estimate = 60 + 24 * $Cycles
$stopAfter = 2 * $estimate
$clock = [Diagnostics.Stopwatch]::StartNew()
$probes = [ordered]@{}
$script:probeSerial = 0
New-Item -ItemType Directory -Path $scratch,$output | Out-Null
function Save-Json($Name, $Value) { [IO.File]::WriteAllText((Join-Path $output $Name), ($Value | ConvertTo-Json -Depth 12)) }
Save-Json 'plan.json' @{ estimateSeconds=$estimate; stopSeconds=$stopAfter; evidence='Single clean Editor reproduction: 30.7s, exit acknowledgement 0.126s'; cycles=$Cycles; scope='Fresh A/B fixtures; unchanged user identity; sequential settings transitions, overlapping real Play; normal exits; no external package DLLs'; cleanupEstimateSeconds=15; cleanupStopSeconds=30 }
function Identity {
    $terms = [ordered]@{}
    if ($ConsentValueNames.Count) {
        $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Software\Unity Technologies\Unity Editor 5.x')
        try {
            foreach ($name in $ConsentValueNames) {
                $value = $key.GetValue($name, $null)
                if ($null -eq $value) { throw "Existing consent value is absent: $name" }
                $bytes = [Text.Encoding]::UTF8.GetBytes(([ordered]@{kind=$key.GetValueKind($name).ToString();value=$value} | ConvertTo-Json -Compress))
                $terms[$name] = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
            }
        } finally { if ($key) { $key.Dispose() } }
    }
    $licenses = [ordered]@{}
    $entitlementXmlBytes = $null
    $licenseRoot = Join-Path $env:LOCALAPPDATA 'Unity/licenses'
    if (Test-Path -LiteralPath $licenseRoot) {
        foreach ($file in Get-ChildItem -LiteralPath $licenseRoot -Recurse -File | Sort-Object FullName) {
            $relative = [IO.Path]::GetRelativePath($licenseRoot,$file.FullName)
            $bytes = Read-UnityObservedBytes $file.FullName
            $licenses[$relative] = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
            if ($relative -eq 'UnityEntitlementLicense.xml') { $entitlementXmlBytes = $bytes }
        }
    }
    if ($null -eq $entitlementXmlBytes) { throw 'Existing named-user entitlement document is required for this diagnostic.' }
    $licenseText = [Text.Encoding]::UTF8.GetString($entitlementXmlBytes).TrimStart([char]0xfeff)
    $entitlement = Get-UnityEntitlementIdentity $licenseText
    return [ordered]@{ userProfile=$env:USERPROFILE; appdata=$env:APPDATA; localAppdata=$env:LOCALAPPDATA; terms=$terms; licenseFiles=$licenses; entitlementIdentity=$entitlement }
}
function Comparable-Identity($Snapshot) {
    $otherFiles = [ordered]@{}
    foreach ($name in $Snapshot.licenseFiles.Keys) {
        if ($name -ne 'UnityEntitlementLicense.xml') { $otherFiles[$name] = $Snapshot.licenseFiles[$name] }
    }
    return ([ordered]@{userProfile=$Snapshot.userProfile;appdata=$Snapshot.appdata;localAppdata=$Snapshot.localAppdata;terms=$Snapshot.terms;otherLicenseFiles=$otherFiles;entitlementIdentity=$Snapshot.entitlementIdentity} | ConvertTo-Json -Depth 10 -Compress)
}
function Call-Probe($Side, $Action, [int]$Limit = 30) {
    if ($clock.Elapsed.TotalSeconds -ge $stopAfter) { throw 'Whole diagnostic stop reached.' }
    $probe = $probes[$Side]
    $script:probeSerial++
    $key = "$Action-$script:probeSerial"
    $target = Join-Path $probe.Root "response-$key.json"
    $temporary = Join-Path $probe.Root 'request.pending'
    [IO.File]::WriteAllText($temporary, (@{id=$key;action=$Action} | ConvertTo-Json -Compress))
    [IO.File]::Move($temporary,(Join-Path $probe.Root 'request.json'))
    $until = [Math]::Min($stopAfter,$clock.Elapsed.TotalSeconds+$Limit)
    $reply = $null
    while ($null -eq $reply) {
        if ($clock.Elapsed.TotalSeconds -ge $until) { throw "Response stop reached: $Side $key" }
        if (Test-Path -LiteralPath $target) {
            $stream = $null; $reader = $null
            try {
                $share = [IO.FileShare]::Read -bor [IO.FileShare]::Delete
                $stream = [IO.FileStream]::new($target,[IO.FileMode]::Open,[IO.FileAccess]::Read,$share)
                $reader = [IO.StreamReader]::new($stream)
                $reply = $reader.ReadToEnd() | ConvertFrom-Json
            } catch {
                $cause = $_.Exception
                while ($cause.InnerException) { $cause = $cause.InnerException }
                if ($cause -isnot [IO.IOException] -or ($cause.HResult -band 0xffff) -notin @(32,33)) { throw }
            } finally {
                if ($reader) { $reader.Dispose() }
                if ($stream) { $stream.Dispose() }
            }
        }
        if ($null -ne $reply) { break }
        if ($probe.Process.HasExited -and -not (Test-Path -LiteralPath $target)) { throw "Editor $Side exited before $key." }
        Start-Sleep -Milliseconds 20
    }
    if ($reply.pid -ne $probe.Process.Id -or $reply.project -ne $probe.Root -or $reply.id -ne $key -or $reply.action -ne $Action -or $reply.error -or $reply.compiling -or $reply.updating) { throw "Invalid owned reply: $Side $key" }
    Save-Json "$Side-$key.json" $reply
    return $reply
}
$before = Identity
Save-Json 'identity-before.json' $before
$proofs = [Collections.Generic.List[object]]::new()
try {
    foreach ($side in @('a','b')) {
        $root = Join-Path $scratch $side
        New-Item -ItemType Directory -Path (Join-Path $root 'Assets/Editor'),(Join-Path $root 'Packages'),(Join-Path $root 'ProjectSettings') | Out-Null
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'internal/UnitySettingsProbe.cs') -Destination (Join-Path $root 'Assets/Editor/UnitySettingsProbe.cs')
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'internal/SettingsProbeFrames.cs') -Destination (Join-Path $root 'Assets/SettingsProbeFrames.cs')
        [IO.File]::WriteAllText((Join-Path $root 'ProjectSettings/ProjectVersion.txt'), "m_EditorVersion: $version" + [Environment]::NewLine)
        [IO.File]::WriteAllText((Join-Path $root 'Packages/manifest.json'), '{"dependencies":{"com.unity.modules.imgui":"1.0.0","com.unity.modules.ui":"1.0.0","com.unity.modules.uielements":"1.0.0"}}')
        $token = [Guid]::NewGuid().ToString('N')
        [IO.File]::WriteAllText((Join-Path $root '.splinter-settings-probe'),$token)
        $launch = [Diagnostics.ProcessStartInfo]::new($editor)
        $launch.UseShellExecute = $false
        $launch.WorkingDirectory = $root
        foreach ($argument in @('-projectPath',$root,'-logFile',(Join-Path $output "$side-editor.log"),'-splinterSettingsProbe',$token)) { $launch.ArgumentList.Add($argument) }
        # No flags/environment overrides for profiles, consent, licensing or settings.
        $process = [Diagnostics.Process]::Start($launch)
        $probes[$side] = @{ Root=$root; Process=$process }
        Save-Json "$side-process.json" @{ pid=$process.Id; command=@($editor)+@($launch.ArgumentList); startUtcTicks=$process.StartTime.ToUniversalTime().Ticks }
        $ready = Call-Probe $side 'setup' 60
        if (-not $ready.searchReady -or $ready.preferences -match 'UnityAutomatedTest') { throw "Unexpected preparation: $side" }
        if ($side -eq 'b' -and $ready.preferences -ne $preferences) { throw 'A/B must share the unchanged normal preferences path.' }
        $preferences = $ready.preferences
    }
    for ($cycle=0; $cycle -lt $Cycles; $cycle++) {
        foreach ($side in $probes.Keys) {
            $prior = Call-Probe $side 'status'
            $reloaded = Call-Probe $side 'reload'
            if ($prior.domain -eq $reloaded.domain) { throw 'Explicit reload did not reload.' }
            $null = Call-Probe $side 'ready'
        }
        foreach ($side in $probes.Keys) {
            $entered = Call-Probe $side 'enter'
            if (-not $entered.playing -or $entered.lastMode -ne 'EnteredPlayMode') { throw 'Play entry not observed.' }
        }
        $framesUntil = [Math]::Min($stopAfter,$clock.Elapsed.TotalSeconds+20)
        do {
            $a = Call-Probe 'a' 'status'; $b = Call-Probe 'b' 'status'
            if (-not $a.playing -or -not $b.playing) { throw 'Both Editors must be playing together.' }
            if ($a.frames -ge 20 -and $b.frames -ge 20) { break }
            if ($clock.Elapsed.TotalSeconds -ge $framesUntil) { throw 'Real Play frame progress stop reached.' }
            Start-Sleep -Milliseconds 50
        } while ($true)
        $exits = [ordered]@{}
        foreach ($side in $probes.Keys) {
            $exited = Call-Probe $side 'exit'
            if ($exited.playing -or $exited.changingPlay -or $exited.lastMode -ne 'EnteredEditMode' -or $exited.exitEventFor -ne $exited.id) { throw 'Request-correlated Edit entry not observed.' }
            $exits[$side] = $exited
        }
        $proofs.Add(@{cycle=$cycle; bothPlaying=$true; frames=@{a=$a.frames;b=$b.frames};exits=$exits})
        Write-Host "Cycle $($cycle+1)/$Cycles passed; frames A=$($a.frames) B=$($b.frames)"
    }
    foreach ($side in $probes.Keys) {
        $null = Call-Probe $side 'quit'
        $left = [Math]::Min(30,$stopAfter-$clock.Elapsed.TotalSeconds)
        if ($left -le 0 -or -not $probes[$side].Process.WaitForExit([int]($left*1000)) -or $probes[$side].Process.ExitCode -ne 0) { throw "Normal exit not confirmed: $side" }
        $log = Get-Content -LiteralPath (Join-Path $output "$side-editor.log") -Raw
        if ($log -match '(?im)opening file failed|could not open file|no valid unity editor license|error CS\d+|\w+Exception:|force quit pressed' -or $log -notmatch 'Cleanup mono' -or $log -notmatch 'Successfully resolved entitlement details') { throw "Editor log failed: $side" }
    }
    $after = Identity
    Save-Json 'identity-after.json' $after
    if ((Comparable-Identity $before) -cne (Comparable-Identity $after)) { throw 'User identity, inspected consent values or license identity changed.' }
    $licenseBytesUnchanged = ($before.licenseFiles | ConvertTo-Json -Compress) -ceq ($after.licenseFiles | ConvertTo-Json -Compress)
    Save-Json 'summary.json' @{ complete=$true; seconds=$clock.Elapsed.TotalSeconds; cycles=$proofs; preferences=$preferences; identityUnchanged=$true; consentValuesChecked=$ConsentValueNames.Count; licenseBytesUnchanged=$licenseBytesUnchanged; normalExits=$true; sourceHash=(Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'internal/UnitySettingsProbe.cs')).Hash; scope='Cooperating fixture Editors only; no performance or uncoordinated-Editor guarantee.' }
} catch {
    Save-Json 'failure.json' @{ error=$_.ToString(); seconds=$clock.Elapsed.TotalSeconds; complete=$false }
    throw
} finally {
    foreach ($side in $probes.Keys) {
        $probe = $probes[$side]
        if (-not $probe.Process.HasExited) {
            [IO.File]::WriteAllText((Join-Path $probe.Root 'cancel-request'),'Normal cancellation of owned diagnostic')
            $stopped = $probe.Process.WaitForExit(30000)
            Save-Json "$side-cleanup.json" @{ safeStopConfirmed=$stopped; pid=$probe.Process.Id; forceKilled=$false }
            if (-not $stopped) { Write-Warning "Safe stop unconfirmed for owned Editor $($probe.Process.Id); no further transitions."; break }
        }
        $probe.Process.Dispose()
    }
}
