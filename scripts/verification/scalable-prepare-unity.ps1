param([ValidateSet('git','none','plastic')][string]$Kind='git')
$ErrorActionPreference='Stop'
$product=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$root=Join-Path 'C:/Users/olive/AppData/Local/Temp' ('ssu1'+@{git='g';none='n';plastic='p'}[$Kind])
$source=Join-Path $root 'p'
$evidence=Join-Path $product ('artifacts/scalable-storage-tests/unity-'+$Kind)
$cache='C:\Users\olive\Desktop\Development\splinter-unity\Library\PackageCache'
$unity='C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe'
New-Item -ItemType Directory -Force -Path $evidence | Out-Null
$owner=Join-Path $root 'fixture-owner.json'
if((Test-Path -LiteralPath $root) -and -not (Test-Path -LiteralPath $owner)){throw 'Preserve an existing unowned fixture root'}
if(-not (Test-Path -LiteralPath $root)){
 New-Item -ItemType Directory -Path $root | Out-Null
 @{kind=$Kind;project=$source;purpose='SCAL-07 fixed small Unity fixture'}|ConvertTo-Json|Set-Content -LiteralPath $owner -Encoding utf8
}
$existing=Get-Content -LiteralPath $owner -Raw|ConvertFrom-Json
if($existing.project -ne $source -or $existing.kind -ne $Kind){throw 'Fixture owner mismatch'}
if(-not (Test-Path -LiteralPath $source)){
 New-Item -ItemType Directory -Path $source,(Join-Path $source 'Assets'),(Join-Path $source 'Packages'),(Join-Path $source 'ProjectSettings')|Out-Null
 [IO.File]::WriteAllText((Join-Path $source 'ProjectSettings/ProjectVersion.txt'),"m_EditorVersion: 6000.3.22f1`nm_EditorVersionWithRevision: 6000.3.22f1 (1c726e1fb402)`n")
 $packages=[ordered]@{}
 $sourceManifest=Get-Content 'C:\Users\olive\Desktop\Development\splinter-unity\Packages\manifest.json' -Raw|ConvertFrom-Json
 foreach($entry in $sourceManifest.dependencies.PSObject.Properties){ if($entry.Name -like 'com.unity.modules.*'){$packages[$entry.Name]='1.0.0'} }
 foreach($name in @('com.unity.pipeline','com.unity.test-framework','com.unity.nuget.newtonsoft-json','com.unity.nuget.mono-cecil','com.unity.ext.nunit')){
  $folder=Get-ChildItem -LiteralPath $cache -Directory|Where-Object Name -Like ($name+'@*')|Select-Object -First 1
  if($null -eq $folder){throw ('Missing existing cached tool dependency '+$name)}
  $packages[$name]='file:'+($folder.FullName -replace '\\','/')
 }
 @{dependencies=$packages}|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $source 'Packages/manifest.json') -Encoding utf8
 [IO.File]::WriteAllText((Join-Path $source '.gitignore'),"Library/`nTemp/`nObj/`nLogs/`nUserSettings/`n*.csproj`n*.sln*`n")
}
$launch=Join-Path $evidence 'preparation-editor.json'
if(Test-Path -LiteralPath $launch){
 $prior=Get-Content $launch -Raw|ConvertFrom-Json
 $running=Get-Process -Id $prior.pid -ErrorAction SilentlyContinue
 if($null -ne $running -and $running.StartTime.ToUniversalTime().Ticks -eq $prior.startTimeUtcTicks){Write-Output ('Preparation Editor already running '+$prior.pid);exit 0}
}
$log=Join-Path $evidence 'preparation-editor.log'
$process=Start-Process -FilePath $unity -ArgumentList @('-projectPath',('"'+$source+'"'),'-logFile',('"'+$log+'"')) -WindowStyle Hidden -PassThru
@{project=$source;home=(Join-Path $root 'h');root=$root;pid=$process.Id;startTimeUtcTicks=$process.StartTime.ToUniversalTime().Ticks;executable=$unity;log=$log;phase='fixture-preparation'}|ConvertTo-Json|Set-Content -LiteralPath $launch -Encoding utf8
Write-Output ('Started preparation Editor '+$process.Id+' for '+$source)
