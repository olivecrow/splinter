[CmdletBinding()]
param(
    [ValidateSet('Install','Update','Remove')][string]$Action = 'Install',
    [string]$PackagePath,
    [string]$GrantFile,
    [switch]$Apply
)
$ErrorActionPreference = 'Stop'
$serviceName = 'SplinterStorageAuthority'
$install = Join-Path $env:ProgramFiles 'Splinter/StorageAuthority'
$state = Join-Path $env:ProgramData 'SplinterStorageAuthority'
$powerShell = Join-Path $env:ProgramFiles 'PowerShell/7/pwsh.exe'
$manifest = $null
$grants = @()
function Assert-JsonMembers([Text.Json.JsonElement]$Element) {
    if ($Element.ValueKind -eq 'Object') {
        $names = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach ($property in $Element.EnumerateObject()) {
            if (-not $names.Add($property.Name)) { throw 'StorageSetupInputInvalid: duplicate JSON property.' }
            Assert-JsonMembers $property.Value
        }
    } elseif ($Element.ValueKind -eq 'Array') {
        foreach ($item in $Element.EnumerateArray()) { Assert-JsonMembers $item }
    }
}
function Read-InputJson([string]$Path, [string]$Kind) {
    Assert-PlainPath $Path
    if ((Get-Item -LiteralPath $Path).Length -gt 1MB) { throw 'StorageSetupInputInvalid: oversized input.' }
    $options = [Text.Json.JsonDocumentOptions]::new(); $options.MaxDepth = 16
    $document = [Text.Json.JsonDocument]::Parse([IO.File]::ReadAllText($Path), $options)
    try {
        if ($document.RootElement.ValueKind.ToString() -cne $Kind) { throw "StorageSetupInputInvalid: expected $Kind." }
        Assert-JsonMembers $document.RootElement
        return $document.RootElement.GetRawText()
    } finally { $document.Dispose() }
}
function Assert-Properties($Value, [string[]]$Names) {
    $actual = @($Value.PSObject.Properties.Name)
    if ($Value -isnot [pscustomobject] -or $actual.Count -ne $Names.Count -or @($actual | Where-Object { $Names -cnotcontains $_ }).Count) {
        throw 'StorageSetupInputInvalid: missing, unknown or incorrectly cased fields.'
    }
}
function Assert-CurrentVersion($Value) {
    foreach ($field in @('version','protocolVersion')) {
        if (($Value.$field -isnot [long] -and $Value.$field -isnot [int]) -or $Value.$field -ne 1) { throw 'StorageServiceVersionMismatch' }
    }
}
function Assert-GrantDirectory([string]$Path) {
    if (-not [IO.Path]::IsPathFullyQualified($Path) -or $Path.StartsWith('\\') -or
        [IO.Path]::GetFullPath($Path).TrimEnd([IO.Path]::DirectorySeparatorChar) -cne $Path -or
        $Path.Substring(2).Contains(':') -or @($Path.Split([IO.Path]::DirectorySeparatorChar) | Where-Object { $_.EndsWith('.') -or $_.EndsWith(' ') }).Count -or
        -not (Test-Path -LiteralPath $Path -PathType Container)) {
        throw 'StorageGrantInvalid: use an existing canonical local directory.'
    }
    Assert-PlainPath $Path
}
function Assert-PlainPath([string]$Path) {
    $cursor = [IO.Path]::GetFullPath($Path)
    while ($cursor) {
        if (Test-Path -LiteralPath $cursor) {
            $item = Get-Item -LiteralPath $cursor -Force
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Reparse path rejected: $cursor" }
        }
        $parent = [IO.Directory]::GetParent($cursor)
        $cursor = if ($parent) { $parent.FullName } else { $null }
    }
}
function Assert-ProtectedEntry([string]$Path) {
        $current = Get-Acl -LiteralPath $Path
        if ($current.GetOwner([Security.Principal.SecurityIdentifier]).Value -notin @('S-1-5-18','S-1-5-32-544')) { throw 'Preexisting authority path has an untrusted owner; preserve it.' }
        $writes = [Security.AccessControl.FileSystemRights]::Write -bor [Security.AccessControl.FileSystemRights]::Delete -bor [Security.AccessControl.FileSystemRights]::DeleteSubdirectoriesAndFiles -bor [Security.AccessControl.FileSystemRights]::ChangePermissions -bor [Security.AccessControl.FileSystemRights]::TakeOwnership -bor 0x50000000
        foreach ($rule in $current.GetAccessRules($true,$true,[Security.Principal.SecurityIdentifier])) {
            if ($rule.AccessControlType -eq 'Allow' -and -not ($rule.PropagationFlags -band [Security.AccessControl.PropagationFlags]::InheritOnly) -and ($rule.FileSystemRights -band $writes) -and $rule.IdentityReference.Value -notin @('S-1-5-18','S-1-5-32-544')) { throw 'Preexisting authority path is writable by another principal; preserve it.' }
        }
}
function Set-ProtectedDirectory([string]$Path, [bool]$Readable) {
    Assert-PlainPath $Path
    $acl = [Security.AccessControl.DirectorySecurity]::new()
    $acl.SetAccessRuleProtection($true, $false)
    $admin = [Security.Principal.SecurityIdentifier]::new('S-1-5-32-544')
    $acl.SetOwner($admin)
    foreach ($sid in @('S-1-5-18','S-1-5-32-544')) {
        $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new([Security.Principal.SecurityIdentifier]::new($sid), 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow'))
    }
    if ($Readable) { $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new([Security.Principal.SecurityIdentifier]::new('S-1-5-32-545'), 'ReadAndExecute', 'ContainerInherit,ObjectInherit', 'None', 'Allow')) }
    if (Test-Path -LiteralPath $Path) {
        Assert-ProtectedEntry $Path
    } else {
        # Apply the restrictive ACL at creation, with no user-writable interval.
        [IO.FileSystemAclExtensions]::Create([IO.DirectoryInfo]::new($Path), $acl)
    }
}
function Assert-NoActiveSnapshots {
    $owners = Join-Path $state 'owners'
    if (-not (Test-Path -LiteralPath $owners)) { return }
    foreach ($owner in Get-ChildItem -LiteralPath $owners -Directory) {
        Assert-PlainPath $owner.FullName
        $receipt = Join-Path $owner.FullName 'receipts/snapshot.json'
        if (Test-Path -LiteralPath (Join-Path $owner.FullName 'active.json')) { throw 'StorageRequestPending: resume the retained operation before removal.' }
        if (Test-Path -LiteralPath $receipt) {
            $snapshot = Get-Content -LiteralPath $receipt -Raw | ConvertFrom-Json
            if (-not $snapshot.released) { throw 'StorageSnapshotsActive: preserve and explicitly release owned workspaces before removal.' }
        } elseif (Test-Path -LiteralPath (Join-Path $owner.FullName 'receipts/capture-intent.json')) {
            throw 'StorageCaptureUncertain: reconcile retained capture evidence before removal.'
        }
    }
}
if ($Action -ne 'Remove') {
    if (-not $PackagePath -or -not $GrantFile) { throw 'PackagePath and GrantFile are required.' }
    $PackagePath = [IO.Path]::GetFullPath($PackagePath)
    $GrantFile = [IO.Path]::GetFullPath($GrantFile)
    Assert-PlainPath $PackagePath; Assert-PlainPath $GrantFile
    $manifest = Read-InputJson (Join-Path $PackagePath 'package.json') 'Object' | ConvertFrom-Json
    Assert-Properties $manifest @('version','protocolVersion','files')
    Assert-CurrentVersion $manifest
    $grants = @(Read-InputJson $GrantFile 'Array' | ConvertFrom-Json)
    if ($grants.Count -eq 0) { throw 'An exact user/project/home grant is required.' }
    $grantKeys = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($grant in $grants) {
        $fields = @('userSid','sourcePath','homePath','sourceVolume','sourceIdentity','homeIdentity')
        Assert-Properties $grant $fields
        foreach ($field in $fields) { if ($grant.$field -isnot [string] -or [string]::IsNullOrWhiteSpace($grant.$field)) { throw 'StorageGrantInvalid: nonempty strings are required.' } }
        if ($grant.userSid -cnotmatch '^S-1-5-21-\d+-\d+-\d+-\d+$') { throw 'Only explicitly named user SIDs are supported.' }
        $null = [Security.Principal.SecurityIdentifier]::new($grant.userSid)
        if ($grant.sourceIdentity -cnotmatch '^[0-9A-F]{8}:[0-9A-F]{16}$' -or $grant.homeIdentity -cnotmatch '^[0-9A-F]{8}:[0-9A-F]{16}$' -or
            $grant.sourceVolume -cnotmatch '^\\\\\?\\Volume\{[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}\}\\$') { throw 'StorageGrantInvalid: invalid volume or directory identity.' }
        Assert-GrantDirectory $grant.sourcePath; Assert-GrantDirectory $grant.homePath
        if (-not $grantKeys.Add(($grant.userSid, $grant.sourcePath, $grant.homePath -join "`n"))) { throw 'StorageGrantDuplicate: one grant per user/project/home is required.' }
    }
    $requiredFiles = @('service-version.json','Splinter.Core.dll','Splinter.Runtime.dll','Splinter.Platform.Windows.exe',
        'Splinter.Platform.Windows.dll','Splinter.Platform.Windows.deps.json','Splinter.Platform.Windows.runtimeconfig.json','winfsp-msil.dll','vss-snapshot.ps1')
    $fileNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    if ($manifest.files -isnot [array]) { throw 'StoragePackageInvalid: files must be an array.' }
    foreach ($file in $manifest.files) {
        Assert-Properties $file @('name','sha256')
        if ($file.name -isnot [string] -or $file.name -cnotmatch '^[A-Za-z0-9_][A-Za-z0-9_.-]*[A-Za-z0-9]$' -or
            $file.sha256 -isnot [string] -or $file.sha256 -cnotmatch '^[0-9a-f]{64}$' -or -not $fileNames.Add($file.name)) { throw 'StoragePackageInvalid: invalid or duplicate manifest entry.' }
        $inputFile = Join-Path $PackagePath $file.name; Assert-PlainPath $inputFile
        if ((Get-FileHash -LiteralPath $inputFile -Algorithm SHA256).Hash.ToLowerInvariant() -ne $file.sha256) { throw "Package hash mismatch: $($file.name)" }
    }
    if (@($requiredFiles | Where-Object { $manifest.files.name -cnotcontains $_ }).Count) { throw 'StoragePackageInvalid: incomplete service package.' }
    $version = Read-InputJson (Join-Path $PackagePath 'service-version.json') 'Object' | ConvertFrom-Json
    Assert-Properties $version @('version','protocolVersion')
    Assert-CurrentVersion $version
}
$plan = [ordered]@{ action=$Action; apply=[bool]$Apply; serviceName=$serviceName; account='LocalSystem'; protocolVersion=1;
    installPath=$install; protectedState=$state; grants=$grants; package=$PackagePath;
    runtimeOperations=@('Capture','Observe','Release'); callerElevation='not required after installation'; retainedState='never automatically removed' }
if (-not $Apply) { $plan | ConvertTo-Json -Depth 8; return }
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
if (-not ([Security.Principal.WindowsPrincipal]::new($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'AdministratorRequired: review the plan, then apply this exact installation once.' }
Assert-PlainPath $install; Assert-PlainPath $state; Assert-PlainPath $powerShell
$existing = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($Action -eq 'Install' -and $existing) { throw 'ServiceAlreadyInstalled: use explicit Update to preserve its ledger.' }
if ($Action -in @('Update','Remove') -and -not $existing) { throw 'ServiceNotInstalled' }
if ($Action -eq 'Remove') {
    Stop-Service -Name $serviceName
    (Get-Service -Name $serviceName).WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
    try { Assert-NoActiveSnapshots } catch { Start-Service -Name $serviceName; throw }
    & sc.exe delete $serviceName | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'ServiceRemovalFailed' }
    $plan | ConvertTo-Json -Depth 8; return
}
Set-ProtectedDirectory (Split-Path -Parent $install) $true
Set-ProtectedDirectory $install $true
Set-ProtectedDirectory $state $false
$configPath = Join-Path $state 'configuration.json'
if (Test-Path -LiteralPath $configPath) {
    Assert-PlainPath $configPath; Assert-ProtectedEntry $configPath
    $old = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
    if ($old.version -ne 1) { throw 'StorageServiceVersionMismatch: preserve unsupported protected state.' }
    foreach ($previous in $old.grants) {
        if (-not @($grants | Where-Object { $_.userSid -eq $previous.userSid -and $_.sourcePath -eq $previous.sourcePath -and $_.homePath -eq $previous.homePath -and $_.sourceIdentity -eq $previous.sourceIdentity -and $_.homeIdentity -eq $previous.homeIdentity -and $_.sourceVolume -eq $previous.sourceVolume }).Count) {
            throw 'StorageGrantRemovalRejected: update must retain every existing grant and identity.'
        }
    }
}
foreach ($file in $manifest.files) {
    $destination = Join-Path $install $file.name
    Assert-PlainPath $destination
    if (Test-Path -LiteralPath $destination) { Assert-ProtectedEntry $destination }
}
if ($existing) {
    # Reject an incompatible grant before interrupting an active service.
    # A stop never authorizes snapshot deletion or independent-result cleanup.
    Stop-Service -Name $serviceName
    (Get-Service -Name $serviceName).WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
}
foreach ($file in $manifest.files) {
    $source = Join-Path $PackagePath $file.name; $destination = Join-Path $install $file.name
    Assert-PlainPath $source; Assert-PlainPath $destination
    # Pin each source against modification from hashing through copy. No source
    # script or managed assembly is executed during the privileged copy.
    $inputStream = [IO.FileStream]::new($source, 'Open', 'Read', 'Read')
    try {
        if ([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($inputStream)).ToLowerInvariant() -ne $file.sha256) { throw 'Package changed after planning.' }
        $inputStream.Position = 0
        $temporary = $destination + '.installing'
        if (Test-Path -LiteralPath $temporary) { throw 'Preserve interrupted installation; verify it before retrying.' }
        $outputStream = [IO.FileStream]::new($temporary, 'CreateNew', 'Write', 'None')
        try { $inputStream.CopyTo($outputStream); $outputStream.Flush($true) } finally { $outputStream.Dispose() }
        if ([IO.File]::Exists($destination)) { [IO.File]::Replace($temporary, $destination, [System.Management.Automation.Language.NullString]::Value) }
        else { [IO.File]::Move($temporary, $destination) }
    } finally { $inputStream.Dispose() }
}
$configuration = [ordered]@{version=1; powerShellPath=$powerShell; grants=$grants} | ConvertTo-Json -Depth 8
$pending = $configPath + '.installing'
if (Test-Path -LiteralPath $pending) { throw 'Preserve interrupted configuration.' }
$configStream = [IO.FileStream]::new($pending, 'CreateNew', 'Write', 'None')
try { $bytes = [Text.Encoding]::UTF8.GetBytes($configuration); $configStream.Write($bytes); $configStream.Flush($true) } finally { $configStream.Dispose() }
if ([IO.File]::Exists($configPath)) { [IO.File]::Replace($pending, $configPath, [System.Management.Automation.Language.NullString]::Value) }
else { [IO.File]::Move($pending, $configPath) }
$imagePath = '"' + (Join-Path $install 'Splinter.Platform.Windows.exe') + '" storage-service'
if (-not $existing) {
    New-Service -Name $serviceName -BinaryPathName $imagePath -StartupType Automatic -DisplayName 'Splinter Storage Authority' -Description 'Exact-scope VSS capture, observation and owned reclamation. Does not execute arbitrary caller commands.' | Out-Null
} else {
    & sc.exe config $serviceName 'binPath=' $imagePath 'start=' 'auto' 'obj=' 'LocalSystem' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'ServiceConfigurationFailed' }
}
Start-Service -Name $serviceName
(Get-Service -Name $serviceName).WaitForStatus('Running', [TimeSpan]::FromSeconds(30))
$plan | ConvertTo-Json -Depth 8
