param([ValidateSet('git','none','plastic')][string]$Kind='git',[int]$Round=1,[switch]$Final)
. "$PSScriptRoot/scalable-unity-common.ps1" -Kind $Kind
$w=(Get-Content (Join-Path $evidence 'ensure-a.json') -Raw|ConvertFrom-Json).data.workspace
Saved ('round-'+$Round+'-worker-save') $w.path | Out-Null
if($Final){ Stop-ExactWorkspace ('round-'+$Round+'-worker-stop') $w }
$r=Invoke-Ri ('round-'+$Round+'-capture') @('workspace','checkpoint','--id',$w.id,'--request-key',('round-'+$Round),'--message',('RI result '+$Round),'--home',$homePath)
$r.data.resultGeneration|ConvertTo-Json -Depth 3
