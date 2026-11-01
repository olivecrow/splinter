param([ValidateSet('git','none','plastic')][string]$Kind='git',[switch]$ResumeObservedCompilation)
. "$PSScriptRoot/scalable-unity-common.ps1" -Kind $Kind
if(-not $ResumeObservedCompilation) {
$code='public static class RiValue { public static int Current => 1; } public sealed class RiData : UnityEngine.ScriptableObject { public int Value; }' | ConvertTo-Json -Compress
Eval 'preparation-create-code' $source ('System.IO.File.WriteAllText(System.IO.Path.Combine(UnityEngine.Application.dataPath,"RiData.cs"),'+$code+');return new {pid=System.Diagnostics.Process.GetCurrentProcess().Id,wrote=true};') | Out-Null
$raw=Invoke-Unity $source @('recompile','--project-path',$source,'--format','json','--timeout','90')
[IO.File]::WriteAllText((Join-Path $evidence 'preparation-compile.json'),$raw)
if(-not($raw|ConvertFrom-Json).success){throw 'Preparation compilation requires observation before any further write'}
} else {
 $proof=Eval 'preparation-compiled-resume-proof' $source 'var t=System.Type.GetType("RiValue, Assembly-CSharp");return new {pid=System.Diagnostics.Process.GetCurrentProcess().Id,compiling=UnityEditor.EditorApplication.isCompiling,compilationFailed=UnityEditor.EditorUtility.scriptCompilationFailed,code=t==null?-1:(int)t.GetProperty("Current").GetValue(null)};'
 if($proof.compiling -or $proof.compilationFailed -or $proof.code -ne 1){throw 'Compilation was not observed complete'}
}
Eval 'preparation-named-scene-and-asset' $source 'var s=UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects);var cube=UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cube);cube.name="RiCube";cube.transform.position=new UnityEngine.Vector3(1,0,0);if(!UnityEditor.SceneManagement.EditorSceneManager.SaveScene(s,"Assets/RI.unity"))throw new System.Exception("Scene save failed");var t=System.Type.GetType("RiData, Assembly-CSharp");var asset=UnityEngine.ScriptableObject.CreateInstance(t);t.GetField("Value").SetValue(asset,1);UnityEditor.AssetDatabase.CreateAsset(asset,"Assets/RI.asset");UnityEditor.AssetDatabase.SaveAssets();return new {project=System.IO.Directory.GetParent(UnityEngine.Application.dataPath).FullName,pid=System.Diagnostics.Process.GetCurrentProcess().Id,scene=s.path,code=(int)System.Type.GetType("RiValue, Assembly-CSharp").GetProperty("Current").GetValue(null)};' | ConvertTo-Json
Saved 'preparation-save' $source | Out-Null
Eval 'preparation-normal-close-request' $source 'UnityEditor.EditorApplication.delayCall += () => UnityEditor.EditorApplication.Exit(0);return new {normalExitRequested=true,pid=System.Diagnostics.Process.GetCurrentProcess().Id};' | ConvertTo-Json
