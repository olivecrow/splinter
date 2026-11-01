param([ValidateSet('git','none','plastic')][string]$Kind='git')
. "$PSScriptRoot/scalable-unity-common.ps1" -Kind $Kind
if($Kind -eq 'git' -and -not(Test-Path -LiteralPath (Join-Path $source '.git'))){
 & git -C $source init --initial-branch development | Out-Null
 & git -C $source config core.autocrlf false
 & git -C $source add --all
 & git -C $source -c user.name=Splinter-Verification -c user.email=verification@invalid -c commit.gpgsign=false commit -m 'Fixed RI Unity input' | Out-Null
 if($LASTEXITCODE -ne 0){throw 'Fixture seed commit failed'}
}
$basisArgs=if($Kind -eq 'none'){@('project','prepare','--project',$source,'--key','scal-project','--application','unity','--home',$homePath)}else{@('library','prepare','--project',$source,'--key','scal-library','--home',$homePath)}
$basis=(Invoke-Ri 'basis-prepare' $basisArgs).data.basis.basis.id
foreach($key in @('a','b')) {
 $ensureArgs=@('workspace','ensure','--project',$source,'--key',('scal-'+$key),'--source',$Kind,'--application','unity','--home',$homePath)
 if($Kind -eq 'none'){$ensureArgs+=@('--storage','project-snapshot','--project-basis',$basis)}
 elseif($Kind -eq 'git'){$ensureArgs+=@('--storage','git-snapshot','--library-basis',$basis,'--base','development')}
 else{$ensureArgs+=@('--storage','plastic-snapshot','--library-basis',$basis,'--base','1@nicks-defense/splinter-scal-unity-20261101@20066124403170@cloud')}
 Invoke-Ri ('ensure-'+$key) $ensureArgs | Out-Null
}
if($Kind -eq 'git') {
 @{head=(& git -C $source rev-parse HEAD).Trim();index=(Get-FileHash (Join-Path $source '.git/index')).Hash;branch=(& git -C $source symbolic-ref HEAD).Trim()}|ConvertTo-Json|Set-Content (Join-Path $evidence 'git-before.json')
}
Write-Output ('Prepared '+$Kind+' original and A/B')
