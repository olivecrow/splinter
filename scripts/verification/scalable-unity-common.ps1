param([ValidateSet('git','none','plastic')][string]$Kind='git')
$ErrorActionPreference='Stop'
$product=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$root=Join-Path 'C:/Users/olive/AppData/Local/Temp' ('ssu1'+@{git='g';none='n';plastic='p'}[$Kind])
$source=Join-Path $root 'p'; $homePath=Join-Path $root 'h'
$evidence=Join-Path $product ('artifacts/scalable-storage-tests/unity-'+$Kind)
$cli=if($env:SPLINTER_SCAL_CLI){[IO.Path]::GetFullPath($env:SPLINTER_SCAL_CLI)}else{Join-Path $product 'src/Splinter.Cli/bin/Release/net10.0/splinter.dll'}
$unityCli=Join-Path $product '.local/scal-tools-20261101/unity.exe'
$fixture=Get-Content -LiteralPath (Join-Path $root 'fixture-owner.json') -Raw|ConvertFrom-Json
if($fixture.kind -ne $Kind -or [IO.Path]::GetFullPath($fixture.project) -ne [IO.Path]::GetFullPath($source) -or $fixture.purpose -ne 'SCAL-07 fixed small Unity fixture'){throw 'The exact owned Unity fixture is required'}
function Expected-Editor([string]$Project){
 $recovered=Join-Path $evidence 'final-recovery-a-owned-identity.json'
 if(Test-Path -LiteralPath $recovered){$proof=Get-Content $recovered -Raw|ConvertFrom-Json;if([IO.Path]::GetFullPath($proof.project) -eq [IO.Path]::GetFullPath($Project)){return @{pid=[int]$proof.pid;startTicks=[long]$proof.startTicks}}}
 foreach($role in @('original','a','b')){
  $path=Join-Path $evidence ($role+'-initial-identity.json')
  if(Test-Path -LiteralPath $path){$proof=Get-Content $path -Raw|ConvertFrom-Json;if([IO.Path]::GetFullPath($proof.project) -eq [IO.Path]::GetFullPath($Project)){return @{pid=[int]$proof.pid;startTicks=[long]$proof.startTicks}}}
 }
 foreach($role in @('a','b')){
  $path=Join-Path $evidence ('start-'+$role+'.json')
  if(Test-Path -LiteralPath $path){$proof=(Get-Content $path -Raw|ConvertFrom-Json).data;if($proof.workspace.path -and [IO.Path]::GetFullPath($proof.workspace.path) -eq [IO.Path]::GetFullPath($Project)){return @{pid=[int]$proof.application.processId;startTicks=[long]$proof.application.startTimeUtcTicks}}}
 }
 return $null
}
function Request-Intent([string]$Name,[string]$Project,[string]$KindName,[string]$Arguments){
 $path=Join-Path $evidence ($Name+'.attempt.json');$identity=if($Project){Expected-Editor $Project}else{$null}
 $value=@{version=1;name=$Name;project=$Project;kind=$KindName;expectedEditor=$identity;argumentsSha256=[Convert]::ToHexStringLower([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($Arguments)));startedAt=[DateTimeOffset]::UtcNow;missingResponse='Observe the exact request and Editor; do not automatically repeat this write'}
 $file=[IO.FileStream]::new($path,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::Read)
 try{$bytes=[Text.Encoding]::UTF8.GetBytes(($value|ConvertTo-Json -Depth 12 -Compress));$file.Write($bytes);$file.Flush($true)}finally{$file.Dispose()}
}
function Invoke-Unity([string]$Project,[string[]]$Arguments) {
 Push-Location -LiteralPath $Project
 try { return (& $unityCli @Arguments) -join "`n" } finally { Pop-Location }
}
function Invoke-Ri([string]$Name,[string[]]$Arguments) {
 if(Test-Path -LiteralPath (Join-Path $evidence ($Name+'.json'))){throw ('Preserve prior operation '+$Name)}
 Request-Intent $Name '' 'splinter-exact-request' ($Arguments|ConvertTo-Json -Compress)
 $watch=[Diagnostics.Stopwatch]::StartNew(); $raw=(& dotnet $cli @Arguments) -join "`n"; $watch.Stop()
 $target=Join-Path $evidence ($Name+'.json'); if(Test-Path -LiteralPath $target){throw ('Preserve prior operation '+$target)}; [IO.File]::WriteAllText($target,$raw)
 @{name=$Name;seconds=$watch.Elapsed.TotalSeconds}|ConvertTo-Json -Compress|Add-Content (Join-Path $evidence 'times.jsonl')
 $r=$raw|ConvertFrom-Json; if($r.status -notin @('Succeeded','Accepted')){throw ($Name+': '+$r.error.code+' '+$r.error.message)}
 return $r
}
function Eval([string]$Name,[string]$Project,[string]$Code) {
 if(Test-Path -LiteralPath (Join-Path $evidence ($Name+'.json'))){throw ('Preserve prior external operation '+$Name)}
 Request-Intent $Name $Project 'external-eval' $Code
 $projectLiteral=([IO.Path]::GetFullPath($Project)|ConvertTo-Json -Compress)
 $guard='if(!string.Equals(System.IO.Path.GetFullPath(System.IO.Directory.GetParent(UnityEngine.Application.dataPath).FullName),'+$projectLiteral+',System.StringComparison.OrdinalIgnoreCase))throw new System.Exception("Wrong exact project");'
 $expected=Expected-Editor $Project
 if($expected){$guard+='if(System.Diagnostics.Process.GetCurrentProcess().Id!='+$expected.pid+'||System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks!='+$expected.startTicks+'L)throw new System.Exception("Wrong exact owned Editor");'}
 $wrapped=$guard+'var operation=new System.Func<object>(()=>{'+$Code+'});var value=operation();return new {project=System.IO.Directory.GetParent(UnityEngine.Application.dataPath).FullName,pid=System.Diagnostics.Process.GetCurrentProcess().Id,startTicks=System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks.ToString(),value=value};'
 $raw=Invoke-Unity $Project @('command','eval','--caller','plugin','--skill','unity-cli','--project-path',$Project,'--format','json','--timeout','45',$wrapped,'30000')
 $target=Join-Path $evidence ($Name+'.json'); if(Test-Path -LiteralPath $target){throw ('Preserve prior operation '+$target)}; [IO.File]::WriteAllText($target,$raw)
 $r=$raw|ConvertFrom-Json
 if(-not $r.success -or -not $r.data.result.success){throw ($Name+': external eval failed '+$raw.Substring(0,[Math]::Min($raw.Length,1000)))}
 if([IO.Path]::GetFullPath($r.data.target.projectPath) -ne [IO.Path]::GetFullPath($Project)){throw 'External tool selected another project'}
 $proof=$r.data.result.result; $native=Get-Process -Id $proof.pid -ErrorAction Stop
 if([IO.Path]::GetFullPath($proof.project) -ne [IO.Path]::GetFullPath($Project) -or $native.StartTime.ToUniversalTime().Ticks -ne [long]$proof.startTicks){throw 'External response/native Editor identity mismatch'}
 $expected=Expected-Editor $Project
 if($expected -and ($expected.pid -ne $proof.pid -or $expected.startTicks -ne [long]$proof.startTicks)){throw 'External tool did not target the exact initial or owned Editor lease'}
 return $proof.value
}
function Compile([string]$Name,[string]$Project,[int]$ExpectedCode,[string]$RequiredInstalledType='') {
 $path=Join-Path $evidence ($Name+'.json');if(Test-Path -LiteralPath $path){throw ('Preserve prior compilation request '+$Name)}
 if(Test-Path -LiteralPath (Join-Path $evidence ($Name+'.attempt.json'))){throw 'Observe the prior compilation request before another write'}
 $required=if($RequiredInstalledType){$RequiredInstalledType|ConvertTo-Json -Compress}else{'null'}
 $observe=('var t=System.Type.GetType("RiValue, Assembly-CSharp");string requiredName=REQUIRED;var requiredType=requiredName==null?null:System.Type.GetType(requiredName);var installed=requiredType==null?null:requiredType.GetProperty("Installed");return new {compiling=UnityEditor.EditorApplication.isCompiling,compilationFailed=UnityEditor.EditorUtility.scriptCompilationFailed,loadedCode=t==null?-1:(int)t.GetProperty("Current").GetValue(null),mvid=t==null?null:t.Assembly.ManifestModule.ModuleVersionId.ToString(),requiredInstalled=requiredName==null||(installed!=null&&(bool)installed.GetValue(null)),requiredMvid=requiredType==null?null:requiredType.Assembly.ManifestModule.ModuleVersionId.ToString()};').Replace('REQUIRED',$required)
 $before=Eval ($Name+'-before-request') $Project $observe
 if($before.compilationFailed){throw 'The exact Editor reports compilation failure'}
 Request-Intent $Name $Project 'external-compilation-confirmation' ([string]$ExpectedCode)
 if(-not $before.compiling -and $before.loadedCode -eq $ExpectedCode -and $before.requiredInstalled){
  [IO.File]::WriteAllText($path,(@{confirmation='already-loaded-expected-code';requested=$false;proof=$before}|ConvertTo-Json -Depth 8))
  return $before
 }
 # The installed Pipeline does not implement the newer CLI's script-recompile
 # verb. Use its existing eval transport for Unity's documented Editor API.
 # A lost response never authorizes another request; subsequent calls only read.
 $outcome='already-compiling'
 if(-not $before.compiling){
  $outcome='acknowledged'
  try{Eval ($Name+'-request') $Project 'if(UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)throw new System.Exception("Preserve Play state");UnityEditor.AssetDatabase.Refresh();UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();return new {requested=true};'|Out-Null}
  catch{$outcome='unconfirmed-observe-before-any-repeat'}
 }
 [IO.File]::WriteAllText($path,(@{confirmation='read-loaded-code-after-single-request';requestOutcome=$outcome;api='CompilationPipeline.RequestScriptCompilation';documentation='https://docs.unity.com/en-us/engine/6000.3/script-reference/unityeditor/compilation/compilationpipeline/requestscriptcompilation'}|ConvertTo-Json -Depth 5))
 for($attempt=0;$attempt -lt 40;$attempt++) {
  try { $proof=Eval ($Name+'-loaded-'+$attempt) $Project $observe }
  catch { Start-Sleep -Milliseconds 500;continue }
  if($proof.compilationFailed){throw 'The exact Editor reports compilation failure'}
  if(-not $proof.compiling -and $proof.loadedCode -eq $ExpectedCode -and $proof.requiredInstalled){return $proof}
  Start-Sleep -Milliseconds 500
 }
 throw 'Compilation remains unconfirmed; preserve the same Editor and observe before another write'
}
function Stop-ExactWorkspace([string]$Name,$Workspace){
 $expected=Expected-Editor $Workspace.path;if(-not $expected){throw 'Observe the exact owned Editor before stopping'}
 $result=Invoke-Ri $Name @('unity','stop','--id',$Workspace.id,'--home',$homePath)
 $watch=[Diagnostics.Stopwatch]::StartNew()
 while($true){
  $native=Get-Process -Id $expected.pid -ErrorAction SilentlyContinue;$alive=$false
  if($native){try{$alive=$native.StartTime.ToUniversalTime().Ticks -eq $expected.startTicks}finally{$native.Dispose()}}
  if(-not $alive){break};if($watch.Elapsed.TotalSeconds -ge 120){throw 'The exact Editor has not exited; observe before retrying stop or capture'}
  Start-Sleep -Milliseconds 250
 }
 $path=Join-Path $evidence ($Name+'-exact-exit.json');if(Test-Path -LiteralPath $path){throw 'Preserve the exact exit proof'}
 [IO.File]::WriteAllText($path,(@{pid=$expected.pid;startTicks=$expected.startTicks;absent=$true;observedAt=[DateTimeOffset]::UtcNow;stopResponse=$result.status}|ConvertTo-Json -Compress))
}
function Saved([string]$Name,[string]$Project) {
 Eval $Name $Project 'if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode || UnityEditor.EditorApplication.isCompiling) throw new System.Exception("Editor busy"); var saved = new System.Collections.Generic.List<string>(); for (int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++) { var s=UnityEngine.SceneManagement.SceneManager.GetSceneAt(i); if(!s.isDirty) continue; if(string.IsNullOrEmpty(s.path)) throw new System.Exception("Unnamed dirty scene"); if(!UnityEditor.SceneManagement.EditorSceneManager.SaveScene(s)) throw new System.Exception("Scene save failed"); saved.Add(s.path); } var dirtyAssets=new System.Collections.Generic.List<UnityEngine.Object>(); foreach(var obj in UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.Object>()) {var ap=UnityEditor.AssetDatabase.GetAssetPath(obj);if(ap.StartsWith("Assets/")&&UnityEditor.EditorUtility.IsDirty(obj))dirtyAssets.Add(obj);} UnityEditor.AssetDatabase.SaveAssets(); foreach(var obj in dirtyAssets)if(UnityEditor.EditorUtility.IsDirty(obj))throw new System.Exception("Asset remains dirty"); for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++) if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new System.Exception("Scene remains dirty"); return new {project=System.IO.Directory.GetParent(UnityEngine.Application.dataPath).FullName,pid=System.Diagnostics.Process.GetCurrentProcess().Id,startTimeUtcTicks=System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks.ToString(),saved=saved.ToArray(),savedAssets=dirtyAssets.Select(o=>UnityEditor.AssetDatabase.GetAssetPath(o)).ToArray(),savedAt=System.DateTimeOffset.UtcNow};'
}
