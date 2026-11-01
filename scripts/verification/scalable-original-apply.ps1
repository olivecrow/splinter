param([ValidateSet('git','none','plastic')][string]$Kind='git',[int]$Round=1,[ValidateSet('begin','publish','finish')][string]$Phase='begin')
. "$PSScriptRoot/scalable-unity-common.ps1" -Kind $Kind
$w=(Get-Content (Join-Path $evidence 'ensure-a.json') -Raw|ConvertFrom-Json).data.workspace
$generation=(Get-Content (Join-Path $evidence ('round-'+$Round+'-capture.json')) -Raw|ConvertFrom-Json).data.resultGeneration
$cooperation='scal-'+$Kind+'-'+$Round; $key='Splinter.SCAL.'+$w.id+'.'+$cooperation
$permitPath=Join-Path $evidence ('round-'+$Round+'-permit.json')
if($Phase -eq 'begin') {
 Saved ('round-'+$Round+'-original-save') $source|Out-Null
 $defer='var auto="KEY.auto";var reload="KEY.reload";if(UnityEditor.SessionState.GetInt(auto,0)!=0||UnityEditor.SessionState.GetInt(reload,0)!=0)throw new System.Exception("Observe existing cooperation before another counter request");UnityEditor.SessionState.SetInt(auto,1);UnityEditor.AssetDatabase.DisallowAutoRefresh();UnityEditor.SessionState.SetInt(auto,2);UnityEditor.SessionState.SetInt(reload,1);UnityEditor.EditorApplication.LockReloadAssemblies();UnityEditor.SessionState.SetInt(reload,2);return new {project=System.IO.Directory.GetParent(UnityEngine.Application.dataPath).FullName,pid=System.Diagnostics.Process.GetCurrentProcess().Id,startTicks=System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks.ToString(),savedAt=System.DateTimeOffset.UtcNow.ToString("O"),autoPhase=2,reloadPhase=2,deferred=true};'
 $guard=Eval ('round-'+$Round+'-defer') $source ($defer.Replace('KEY',$key))
 $process=Get-Process -Id $guard.pid
 if($process.StartTime.ToUniversalTime().Ticks -ne [long]$guard.startTicks){throw 'Exact external/native original identity differs'}
 $now=[DateTimeOffset]::UtcNow
 @{version=1;projectPath=$source;processId=$guard.pid;startTimeUtcTicks=$process.StartTime.ToUniversalTime().Ticks;resultId=$generation.id;cooperationKey=$cooperation;savedAt=$guard.savedAt;observedAt=$now.ToString('O');expiresAt=$now.AddMinutes(5).ToString('O');importsDeferred=$true}|ConvertTo-Json|Set-Content $permitPath -Encoding utf8
 Write-Output 'Saved original and acquired this request import/reload deferral'; exit 0
}
if($Phase -eq 'publish') {
 $plan=(Invoke-Ri ('round-'+$Round+'-plan') @('workspace','integrate','--id',$w.id,'--target','original','--result',$generation.id,'--plan','--live-original',$permitPath,'--home',$homePath)).data.originalApplyPlan
 if($plan.conflicts.Count -ne 0){throw ('Review conflicts: '+($plan.conflicts -join ', '))}
 Invoke-Ri ('round-'+$Round+'-apply') @('workspace','integrate','--id',$w.id,'--target','original','--result',$generation.id,'--intent-key',('round-'+$Round+'-publish'),'--expected-plan-digest',$plan.digest,'--live-original',$permitPath,'--home',$homePath)|Out-Null
 Write-Output ('Published immutable result '+$generation.id); exit 0
}
$finish='var auto="KEY.auto";var reload="KEY.reload";int a=UnityEditor.SessionState.GetInt(auto,0),r=UnityEditor.SessionState.GetInt(reload,0);if((a!=2&&a!=4)||(r!=2&&r!=4))throw new System.Exception("Unknown or unowned counter phase; observe before release");var dirty=new System.Collections.Generic.List<string>();for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++){var s=UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);if(s.isDirty)dirty.Add(s.path);}foreach(var obj in UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.Object>()){var p=UnityEditor.AssetDatabase.GetAssetPath(obj);if(p.StartsWith("Assets/")&&UnityEditor.EditorUtility.IsDirty(obj))dirty.Add(p);}if(dirty.Count==0)UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Base.unity");if(r==2){UnityEditor.SessionState.SetInt(reload,3);UnityEditor.EditorApplication.UnlockReloadAssemblies();UnityEditor.SessionState.SetInt(reload,4);}if(a==2){UnityEditor.SessionState.SetInt(auto,3);UnityEditor.AssetDatabase.AllowAutoRefresh();UnityEditor.SessionState.SetInt(auto,4);}if(dirty.Count>0)return new {synchronization="incomplete-new-dirty-state",autoPhase=4,reloadPhase=4,dirty=dirty.ToArray()};UnityEditor.AssetDatabase.Refresh();return new {synchronization="refresh-requested",autoPhase=4,reloadPhase=4,dirty=dirty.ToArray()};'
$finished=Eval ('round-'+$Round+'-resume-imports') $source ($finish.Replace('KEY',$key));$finished|ConvertTo-Json
if($finished.synchronization -ne 'refresh-requested'){throw 'Preserve new dirty state; no forced refresh/reload or compilation request'}
