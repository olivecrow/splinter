[CmdletBinding()]
param(
    [ValidatePattern('^[A-Z]$')][string]$DriveLetter = 'S',
    [switch]$Create
)

$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$directory = Join-Path $repository '.local/dev-drive'
$image = Join-Path $directory 'splinter.vhdx'
$sizeMiB = 65536
if (Test-Path -LiteralPath ($DriveLetter + ':\')) { throw "Drive $DriveLetter is already in use." }
if (Test-Path -LiteralPath $image) { throw 'The image already exists. Preserve it; this script only creates a new image.' }
for ($ancestor = $directory; $ancestor; $ancestor = [IO.Path]::GetDirectoryName($ancestor)) {
    if ((Test-Path -LiteralPath $ancestor) -and ((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw "Reparse point in setup path: $ancestor"
    }
}
$plan = [ordered]@{
    action = 'create-new-dynamic-dev-drive'
    image = $image
    maximumBytes = [long]$sizeMiB * 1MB
    drive = $DriveLetter + ':'
    fileSystem = 'ReFS'
    requiresAdministrator = $true
    formatsExistingVolume = $false
    automaticStartupMount = $false
}
if (-not $Create) { $plan | ConvertTo-Json; exit 0 }
$principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Creation requires an Administrator PowerShell. Review the plan, then explicitly run with -Create.'
}
$priorDisks = @(Get-Disk | Select-Object -ExpandProperty Number)
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$diskpartFile = Join-Path $directory 'create-new-image.txt'
@"
create vdisk file="$image" maximum=$sizeMiB type=expandable
select vdisk file="$image"
attach vdisk
exit
"@ | Set-Content -LiteralPath $diskpartFile -Encoding ascii
& diskpart.exe /s $diskpartFile | Set-Content -LiteralPath (Join-Path $directory 'diskpart.log') -Encoding utf8
if ($LASTEXITCODE -ne 0) { throw 'DiskPart failed; preserve its log and image for inspection.' }
$diskImage = Get-DiskImage -ImagePath $image -ErrorAction Stop
$disk = @($diskImage | Get-Disk)
if ($disk.Count -ne 1 -or -not $diskImage.Attached -or $priorDisks -contains $disk[0].Number -or $disk[0].PartitionStyle -ne 'RAW') {
    throw 'The new attached image could not be identified as one new empty disk. No formatting was attempted.'
}
# Only the disk resolved from this newly created exact image may be initialized.
$diskNumber = $disk[0].Number
Initialize-Disk -Number $diskNumber -PartitionStyle GPT | Out-Null
$partition = New-Partition -DiskNumber $diskNumber -UseMaximumSize -DriveLetter $DriveLetter
$volume = Format-Volume -Partition $partition -DevDrive -NewFileSystemLabel 'Splinter Dev' -Confirm:$false
if ([IO.DriveInfo]::new($DriveLetter + ':\').DriveFormat -ne 'ReFS') { throw 'Created volume is not ReFS; stop and inspect it.' }
$plan.createdAt = (Get-Date).ToUniversalTime().ToString('O')
$plan.diskNumber = $diskNumber
$plan.volumePath = $volume.Path
$plan | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $directory 'receipt.json') -Encoding utf8
$plan | ConvertTo-Json
