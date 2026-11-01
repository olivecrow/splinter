param(
 [Parameter(Mandatory)][ValidateSet('git','none','plastic')][string]$Kind,
 [Parameter(Mandatory)][ValidateSet('setup','launch','round1','round2','round3','reclaim','cleanup')][string]$Stage,
 [Parameter(Mandatory)][string]$CliAssembly
)
$ErrorActionPreference='Stop'
$env:SPLINTER_SCAL_CLI=[IO.Path]::GetFullPath($CliAssembly)
. "$PSScriptRoot/scalable-unity-common.ps1" -Kind $Kind
function Save-Proof([string]$Name,$Value) {
 $path=Join-Path $evidence ($Name+'.json');if(Test-Path -LiteralPath $path){throw ('Preserve '+$path)}
 [IO.File]::WriteAllText($path,($Value|ConvertTo-Json -Depth 40),[Text.UTF8Encoding]::new($false))
}
function W([string]$Letter){return (Get-Content (Join-Path $evidence ('ensure-'+$Letter+'.json')) -Raw|ConvertFrom-Json).data.workspace}
function Identity([string]$Name,[string]$Project){
 return Eval $Name $Project 'return new {project=System.IO.Directory.GetParent(UnityEngine.Application.dataPath).FullName,pid=System.Diagnostics.Process.GetCurrentProcess().Id,startTicks=System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks.ToString(),playing=UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode,compiling=UnityEditor.EditorApplication.isCompiling,compilationFailed=UnityEditor.EditorUtility.scriptCompilationFailed};'
}
function Assert-Identity([string]$Tag,[string]$Project,[string]$Initial){
 $before=Get-Content (Join-Path $evidence ($Initial+'.json')) -Raw|ConvertFrom-Json
 $current=Identity $Tag $Project
 if($before.pid -ne $current.pid -or $before.startTicks -ne $current.startTicks -or $current.playing -or $current.compiling -or $current.compilationFailed){throw 'Exact Editor lifetime or compilation proof changed'}
 return $current
}
function Wait-Editor([string]$Name,[string]$Project){
 for($attempt=0;$attempt -lt 80;$attempt++){
  try{$observed=Identity ($Name+'-read-'+$attempt) $Project}catch{Start-Sleep -Milliseconds 750;continue}
  if($observed.playing -or $observed.compilationFailed){throw 'The exact Editor is playing or reports compilation failure'}
  if(-not $observed.compiling){return $observed};Start-Sleep -Milliseconds 750
 }
 throw 'External Editor connection unconfirmed; preserve exact launch and observe before another launch'
}
function Save-Initial([string]$Tag,[string]$Project){Save-Proof ($Tag+'-initial-identity') (Wait-Editor ($Tag+'-initial') $Project)}
function Keep-Proof([string]$Tag,[switch]$AStopped){
 Assert-Identity ($Tag+'-original-identity') $source 'original-initial-identity'|Out-Null
 Assert-Identity ($Tag+'-b-identity') (W 'b').path 'b-initial-identity'|Out-Null
 if(-not $AStopped){Assert-Identity ($Tag+'-a-identity') (W 'a').path 'a-initial-identity'|Out-Null}
}
if($Stage -eq 'setup'){
 & "$PSScriptRoot/scalable-setup-workspaces.ps1" -Kind $Kind
 if($LASTEXITCODE -ne 0){throw 'Setup failed; retain same basis/task identities'}
 exit 0
}
if($Stage -eq 'launch'){
 foreach($schema in @('unity.start','unity.stop','workspace.checkpoint','workspace.integrate','workspace.release')){Invoke-Ri ('schema-'+$schema) @('schema',$schema)|Out-Null}
 Request-Intent 'original-open' $source 'external-open' '6000.3.22f1'
 $raw=Invoke-Unity $source @('open',$source,'--editor-version','6000.3.22f1','--format','json')
 Save-Proof 'original-open' ($raw|ConvertFrom-Json)
 Save-Initial 'original' $source
 foreach($letter in @('a','b')){
  $workspace=W $letter;Invoke-Ri ('start-'+$letter) @('unity','start','--id',$workspace.id,'--home',$homePath)|Out-Null
  Save-Initial $letter $workspace.path
 }
 $unrelated=Eval 'original-unrelated-unsaved' $source 'var shader=UnityEngine.Shader.Find("Standard");if(shader==null)shader=UnityEngine.Shader.Find("Universal Render Pipeline/Lit");var m=new UnityEngine.Material(shader);UnityEditor.AssetDatabase.CreateAsset(m,"Assets/OriginalOnly.mat");UnityEditor.AssetDatabase.SaveAssets();m.color=UnityEngine.Color.red;UnityEditor.EditorUtility.SetDirty(m);return new {dirty=UnityEditor.EditorUtility.IsDirty(m),r=m.color.r};'
 if(-not $unrelated.dirty){throw 'Unsaved original asset precondition missing'}
 & "$PSScriptRoot/scalable-worker-edit.ps1" -Kind $Kind -Worker b -Value 201 -DirtyOnly
 if($LASTEXITCODE -ne 0){throw 'Peer edit failed'}
 exit 0
}
if($Stage -in @('round1','round2','round3')){
 $round=[int]$Stage.Substring(5);$value=100+$round
 Keep-Proof ('before-'+$Stage)
 & "$PSScriptRoot/scalable-worker-edit.ps1" -Kind $Kind -Worker a -Value $value
 if($LASTEXITCODE -ne 0){throw 'Worker edit/compile failed'}
 if($round -eq 3){& "$PSScriptRoot/scalable-final-hook.ps1" -Kind $Kind;if($LASTEXITCODE -ne 0){throw 'Final hook unconfirmed'}}
 & "$PSScriptRoot/scalable-capture-round.ps1" -Kind $Kind -Round $round -Final:($round -eq 3)
 if($LASTEXITCODE -ne 0){throw 'Fixed capture failed; retain exact request'}
 if($round -eq 2){& "$PSScriptRoot/scalable-conflict-round.ps1" -Kind $Kind}
 else{& "$PSScriptRoot/scalable-original-apply.ps1" -Kind $Kind -Round $round -Phase begin}
 if($LASTEXITCODE -ne 0){throw 'External save/deferral or deliberate conflict proof failed'}
 & "$PSScriptRoot/scalable-original-apply.ps1" -Kind $Kind -Round $round -Phase publish
 if($LASTEXITCODE -ne 0){throw 'Publication failed; preserve the owned deferral and exact intent'}
 & "$PSScriptRoot/scalable-original-apply.ps1" -Kind $Kind -Round $round -Phase finish
 if($LASTEXITCODE -ne 0){throw 'External deferral release unconfirmed; do not repeat counters'}
 $expectedCode=if($round -eq 3){104}else{$value}
 & "$PSScriptRoot/scalable-verify-round.ps1" -Kind $Kind -Round $round -Code $expectedCode -Value $value
 if($LASTEXITCODE -ne 0){throw 'Loaded original or continuing peer verification failed'}
 Keep-Proof ('after-'+$Stage) -AStopped:($round -eq 3)
 if($round -eq 3){
  $initial=Get-Content (Join-Path $evidence 'a-initial-identity.json') -Raw|ConvertFrom-Json
  $native=Get-Process -Id $initial.pid -ErrorAction SilentlyContinue
  if($native -and $native.StartTime.ToUniversalTime().Ticks -eq [long]$initial.startTicks){throw 'Final A Editor is still running'}
 }
 exit 0
}
if($Stage -eq 'reclaim'){
 $a=W 'a';$b=W 'b'
 Invoke-Ri 'release-a' @('workspace','release','--id',$a.id,'--home',$homePath)|Out-Null
 Keep-Proof 'after-a-reclaim' -AStopped
 Eval 'peer-after-a-reclaim-new-edit' $b.path 'new UnityEngine.GameObject("AfterAReclaim");UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());return new {dirty=UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty};'|Out-Null
 Saved 'final-peer-save' $b.path|Out-Null
 Stop-ExactWorkspace 'final-peer-stop' $b
 Invoke-Ri 'final-peer-capture' @('workspace','checkpoint','--id',$b.id,'--request-key','final-peer','--message','SCAL peer preserved','--home',$homePath)|Out-Null
 Invoke-Ri 'release-b' @('workspace','release','--id',$b.id,'--home',$homePath)|Out-Null
 Assert-Identity 'after-all-workspace-reclaim-original-alive' $source 'original-initial-identity'|Out-Null
 if((Test-Path -LiteralPath $a.path) -or (Test-Path -LiteralPath $b.path)){throw 'Workspace path survived reclamation'}
 if($Kind -eq 'git'){
  $before=Get-Content (Join-Path $evidence 'git-before.json') -Raw|ConvertFrom-Json
  if((& git -C $source rev-parse HEAD).Trim() -ne $before.head -or (Get-FileHash (Join-Path $source '.git/index')).Hash -ne $before.index -or (& git -C $source symbolic-ref HEAD).Trim() -ne $before.branch){throw 'Original Git administration changed'}
 }
 Save-Proof 'lifecycle-complete-original-still-running' @{source=$Kind;cli=$cli;a=$a.id;b=$b.id;intermediateCaptures=2;finalCapture=1;finalLoadedCode=104;finalLoadedSceneAndAsset=103;completedAt=[DateTimeOffset]::UtcNow;cleanup='Separate cleanup stage has not run'}
 exit 0
}
if($Stage -eq 'cleanup'){
 if(-not(Test-Path -LiteralPath (Join-Path $evidence 'lifecycle-complete-original-still-running.json'))){throw 'Prove original lifetime through workspace reclamation before cleanup'}
 Assert-Identity 'cleanup-exact-original-before-close' $source 'original-initial-identity'|Out-Null
 Saved 'cleanup-original-save' $source|Out-Null
 Request-Intent 'cleanup-original-normal-close' $source 'external-close' 'graceful-timeout45'
 $raw=Invoke-Unity $source @('close',$source,'--timeout','45','--format','json')
 $close=$raw|ConvertFrom-Json;Save-Proof 'cleanup-original-normal-close' $close
 if(-not $close.success){throw 'Cleanup normal close unconfirmed; no force/restore'}
 $initial=Get-Content (Join-Path $evidence 'original-initial-identity.json') -Raw|ConvertFrom-Json
 if([IO.Path]::GetFullPath($close.data.project) -ne [IO.Path]::GetFullPath($source) -or $close.data.pid -ne $initial.pid -or $close.data.method -ne 'graceful' -or -not $close.data.closed){throw 'Cleanup close response does not prove this exact graceful Editor exit'}
 $native=Get-Process -Id $initial.pid -ErrorAction SilentlyContinue
 if($native -and $native.StartTime.ToUniversalTime().Ticks -eq [long]$initial.startTicks){throw 'Original is still alive; do not restore its basis'}
 $basis=(Get-Content (Join-Path $evidence 'basis-prepare.json') -Raw|ConvertFrom-Json).data.basis.basis.id
 $command=if($Kind -eq 'none'){'project'}else{'library'}
 Invoke-Ri 'cleanup-basis-restore' @($command,'restore','--id',$basis,'--home',$homePath)|Out-Null
 Save-Proof 'cleanup-complete' @{source=$Kind;completedAt=[DateTimeOffset]::UtcNow;stage='Separate test cleanup after required original-Editor-preservation evidence'}
}
