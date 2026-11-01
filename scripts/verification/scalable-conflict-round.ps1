param([ValidateSet('git','none','plastic')][string]$Kind='none')
. "$PSScriptRoot/scalable-unity-common.ps1" -Kind $Kind
$asset=Join-Path $source 'Assets/RI.asset';$beforeHash=(Get-FileHash $asset -Algorithm SHA256).Hash;$beforeAttributes=[int][IO.File]::GetAttributes($asset)
@{hash=$beforeHash;attributes=$beforeAttributes;path=$asset}|ConvertTo-Json|Set-Content (Join-Path $evidence 'round-2-conflict-before.json')
Eval 'round-2-original-conflicting-edit' $source 'var t=System.Type.GetType("RiData, Assembly-CSharp");var a=UnityEditor.AssetDatabase.LoadAssetAtPath("Assets/RI.asset",t);t.GetField("Value").SetValue(a,999);UnityEditor.EditorUtility.SetDirty(a);return new {dirty=UnityEditor.EditorUtility.IsDirty(a),value=999};'|Out-Null
& "$PSScriptRoot/scalable-original-apply.ps1" -Kind $Kind -Round 2 -Phase begin
if($LASTEXITCODE -ne 0){throw 'Original cooperation failed'}
$w=(Get-Content (Join-Path $evidence 'ensure-a.json') -Raw|ConvertFrom-Json).data.workspace
$result=(Get-Content (Join-Path $evidence 'round-2-capture.json') -Raw|ConvertFrom-Json).data.resultGeneration.id
$permit=Join-Path $evidence 'round-2-permit.json'
$plan=(Invoke-Ri 'round-2-intentional-conflict-plan' @('workspace','integrate','--id',$w.id,'--target','original','--result',$result,'--plan','--live-original',$permit,'--home',$homePath)).data.originalApplyPlan
if($plan.conflicts.Count -lt 1){throw 'Intentional conflict missing'}
$raw=(& dotnet $cli workspace integrate --id $w.id --target original --result $result --intent-key reject-conflict --expected-plan-digest $plan.digest --live-original $permit --home $homePath)-join "`n"
$raw|Set-Content (Join-Path $evidence 'round-2-intentional-conflict-rejected.json')
if(($raw|ConvertFrom-Json).error.code -ne 'OriginalApplyConflict'){throw 'Conflict was not refused'}
Eval 'round-2-conflict-preserved-and-explicitly-resolved' $source 'var t=System.Type.GetType("RiData, Assembly-CSharp");var a=UnityEditor.AssetDatabase.LoadAssetAtPath("Assets/RI.asset",t);int before=(int)t.GetField("Value").GetValue(a);if(before!=999)throw new System.Exception("Conflict overwritten");t.GetField("Value").SetValue(a,101);UnityEditor.EditorUtility.SetDirty(a);UnityEditor.AssetDatabase.SaveAssets();return new {before=before,restored=101,dirty=UnityEditor.EditorUtility.IsDirty(a)};'|Out-Null
if((Get-FileHash $asset -Algorithm SHA256).Hash -ne $beforeHash){throw 'Explicit conflict content restore differs'}
[IO.File]::SetAttributes($asset,[IO.FileAttributes]$(if($beforeAttributes -eq 0){128}else{$beforeAttributes}))
@{beforeAttributes=$beforeAttributes;afterAttributes=[int][IO.File]::GetAttributes($asset);reason='Explicit restore of test-owned conflict content and metadata; product guards remain strict'}|ConvertTo-Json|Set-Content (Join-Path $evidence 'round-2-conflict-restored.json')
Write-Output 'Intentional conflicting publication refused and fixture change explicitly restored'
