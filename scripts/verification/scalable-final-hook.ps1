param([ValidateSet('git','none','plastic')][string]$Kind='none',[string]$TraceSuffix='')
. "$PSScriptRoot/scalable-unity-common.ps1" -Kind $Kind
if($TraceSuffix -and $TraceSuffix -notmatch '^[a-z0-9-]+$'){throw 'Use a distinct safe final-hook observation suffix'}
$w=(Get-Content (Join-Path $evidence 'ensure-a.json') -Raw|ConvertFrom-Json).data.workspace
$last='public static class RiValue { public static int Current => 104; } public sealed class RiData : UnityEngine.ScriptableObject { public int Value; }'
$literal=$last|ConvertTo-Json -Compress
$hook='[UnityEditor.InitializeOnLoad] public static class RiShutdown { public static bool Installed {get;private set;} static RiShutdown() { UnityEditor.EditorApplication.quitting += () => System.IO.File.WriteAllText(System.IO.Path.Combine(UnityEngine.Application.dataPath,"RiData.cs"),'+$literal+'); Installed=true; } }'
$hookLiteral=$hook|ConvertTo-Json -Compress
Eval ('final-install-owned-fixture-quitting-write'+$TraceSuffix) $w.path ('System.IO.Directory.CreateDirectory(System.IO.Path.Combine(UnityEngine.Application.dataPath,"Editor"));System.IO.File.WriteAllText(System.IO.Path.Combine(UnityEngine.Application.dataPath,"Editor/RiShutdown.cs"),'+$hookLiteral+');return new {pid=System.Diagnostics.Process.GetCurrentProcess().Id,wrote=true};')|Out-Null
Compile ('final-fixture-hook-compile'+$TraceSuffix) $w.path 103 'RiShutdown, Assembly-CSharp-Editor' | Out-Null
Eval ('final-fixture-hook-loaded-and-metadata'+$TraceSuffix) $w.path 'var t=System.Type.GetType("RiShutdown, Assembly-CSharp-Editor");if(t==null||!(bool)t.GetProperty("Installed").GetValue(null))throw new System.Exception("Final shutdown hook not installed in the loaded Editor assembly");string folder=System.IO.Path.Combine(UnityEngine.Application.dataPath,"Editor.meta"),script=System.IO.Path.Combine(UnityEngine.Application.dataPath,"Editor/RiShutdown.cs.meta");if(!System.IO.File.Exists(folder)||!System.IO.File.Exists(script))throw new System.Exception("Preserve missing imported final-hook metadata");return new {installed=true,folderMeta=true,scriptMeta=true,mvid=t.Assembly.ManifestModule.ModuleVersionId.ToString(),code=(int)System.Type.GetType("RiValue, Assembly-CSharp").GetProperty("Current").GetValue(null)};'|Out-Null
Write-Output 'Compiled fixture-only shutdown write; original and peer keep running'
