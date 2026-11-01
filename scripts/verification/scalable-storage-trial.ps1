param(
 [Parameter(Mandatory)][ValidateSet('git','none','plastic')][string]$Source,
 [Parameter(Mandatory)][string]$FixtureRoot,
 [Parameter(Mandatory)][string]$CliAssembly,
 [Parameter(Mandatory)][string]$ProbeAssembly,
 [string]$BaseReference,
 [switch]$LargeFile,
 [switch]$QueryPages
)
$ErrorActionPreference='Stop'
$fixture=[IO.Path]::GetFullPath($FixtureRoot);$evidence=Join-Path $fixture 'evidence'
$seedInventory=Join-Path $evidence 'allocation-before.json'
& "$PSScriptRoot/scalable-storage-allocation.ps1" -Roots @((Join-Path $fixture 'p'),(Join-Path $fixture 'h')) -Output $seedInventory
$stages=@('start','bulk','selected');if($QueryPages){$stages+='queries'};if($LargeFile){$stages+='large'};$stages+='final'
foreach($stage in $stages){
 & "$PSScriptRoot/scalable-storage-editorless.ps1" -Stage $stage -Source $Source -FixtureRoot $fixture -CliAssembly $CliAssembly -ProbeAssembly $ProbeAssembly -BaseReference $BaseReference
 if($LASTEXITCODE -ne 0){throw ('Preserve exact '+$Source+'/'+$stage+' request and repair before continuing')}
 & "$PSScriptRoot/scalable-storage-allocation.ps1" -Roots @((Join-Path $fixture 'p'),(Join-Path $fixture 'h')) -Output (Join-Path $evidence ('allocation-after-'+$stage+'.json')) -BaselineInventory ($seedInventory+'.files.jsonl') -ExcludeRoots @((Join-Path $fixture 'h/workspaces'))
}
