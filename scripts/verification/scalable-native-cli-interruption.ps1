param(
 [Parameter(Mandatory)][string]$FixtureRoot,
 [Parameter(Mandatory)][string]$CliAssembly,
 [Parameter(Mandatory)][string]$ActorAssembly
)
$ErrorActionPreference='Stop'
$fixture=[IO.Path]::GetFullPath($FixtureRoot)
$evidence=Join-Path $fixture 'evidence'
$owner=Get-Content (Join-Path $fixture 'fixture-owner.json') -Raw|ConvertFrom-Json
if($owner.source -ne 'git' -or $owner.entries -ne 100000 -or $owner.phase -ne 'seeded' -or
 [IO.Path]::GetFullPath($owner.project) -ne [IO.Path]::GetFullPath((Join-Path $fixture 'p'))){throw 'The fixed owned 100000-entry Git fixture is required'}
$homePath=[IO.Path]::GetFullPath($owner.home)
$workspace=(Get-Content (Join-Path $evidence 'ensure-a.json') -Raw|ConvertFrom-Json).data.workspace
$actor=[IO.Path]::GetFullPath($ActorAssembly);$cli=[IO.Path]::GetFullPath($CliAssembly)
if(!(Test-Path -LiteralPath $actor)){throw 'Build and freeze the exact interruption actor before starting this stage'}
function Save([string]$Name,$Value){
 $path=Join-Path $evidence ($Name+'.json')
 $file=[IO.FileStream]::new($path,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::Read)
 try{
  $bytes=[Text.Encoding]::UTF8.GetBytes((ConvertTo-Json -InputObject $Value -Depth 30));$file.Write($bytes);$file.Flush($true)
 }finally{$file.Dispose()}
}
function Invoke-VerifiedCli([string]$Name,[string[]]$Arguments){
 $raw=(& dotnet $cli @Arguments)-join "`n";$value=$raw|ConvertFrom-Json -Depth 100
 Save $Name $value;if($value.status -ne 'Succeeded'){throw ($Name+': '+$value.error.code)};return $value
}
$relative='d00/f0000000.bin';$file=Join-Path $workspace.path $relative
$beforeHash=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()
$mount=$workspace.snapshot.mount
$provider=Get-Process -Id $mount.providerProcessId -ErrorAction Stop
if($provider.StartTime.ToUniversalTime().Ticks -ne $mount.providerStartTimeUtcTicks){throw 'The original provider lease changed'}
$provider.Dispose()
$signal=Join-Path $evidence 'native-cli-loss-signal.json'
$request=[ordered]@{Home=$homePath;Id=$workspace.id;Boundary='source-capture-fixed';Signal=$signal;Nonce=[Guid]::NewGuid().ToString('N');ResultId=$null;Digest=$null;
 CliArguments=@('workspace','checkpoint','--id',$workspace.id,'--request-key','native-cli-loss','--message','native CLI interruption','--home',$homePath)}
Save 'native-cli-loss-request' $request
$start=[Diagnostics.ProcessStartInfo]::new('dotnet');$start.UseShellExecute=$false;$start.CreateNoWindow=$true
$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
foreach($argument in @($actor,'-method','Splinter.Tests.FileWorkspaceTests.GenerationProcessChild')){$start.ArgumentList.Add($argument)}
$start.Environment['SPLINTER_GENERATION_PROCESS_CHILD']=Join-Path $evidence 'native-cli-loss-request.json'
$child=[Diagnostics.Process]::Start($start);$ticks=$child.StartTime.ToUniversalTime().Ticks
$stdout=$child.StandardOutput.ReadToEndAsync();$stderr=$child.StandardError.ReadToEndAsync()
try{
 $watch=[Diagnostics.Stopwatch]::StartNew()
 while(!(Test-Path -LiteralPath $signal)){
  if($child.HasExited){throw ('The actual CLI exited before its fixed-source boundary: '+$stdout.GetAwaiter().GetResult())}
  if($watch.Elapsed.TotalMinutes -gt 30){throw 'The fixed-source signal is unconfirmed; preserve the live request before any further action'}
  Start-Sleep -Milliseconds 250
 }
 $observed=Get-Content $signal -Raw|ConvertFrom-Json
 if($observed.Nonce -ne $request.Nonce -or $observed.Pid -ne $child.Id -or $observed.Start -ne $ticks -or
    $child.StartTime.ToUniversalTime().Ticks -ne $ticks){throw 'The interruption actor identity does not match its durable nonce'}
 $control=Join-Path $homePath ('records/'+$workspace.id)
 $fixed=Get-Content (Join-Path $control 'workspace.json') -Raw|ConvertFrom-Json -Depth 40
 if($fixed.version -ne 4 -or $fixed.captureIntent.requestKey -ne 'native-cli-loss'){throw 'The real CLI did not preserve its task-v4 request'}
 $capture=Get-Content (Join-Path $control ('captures/'+$fixed.captureIntent.id+'/capture.json')) -Raw|ConvertFrom-Json -Depth 30
 Save 'native-cli-loss-fixed' @{workspace=$workspace.id;intent=$fixed.captureIntent;captureId=$capture.id;digest=$capture.digest;sourceSequence=$capture.sequence;
  childPid=$child.Id;childStartTicks=$ticks;providerPid=$mount.providerProcessId;providerStartTicks=$mount.providerStartTimeUtcTicks;
  actor=$actor;actorSha256=(Get-FileHash $actor).Hash.ToLowerInvariant();cliRunAssemblySha256=(Get-FileHash (Join-Path ([IO.Path]::GetDirectoryName($actor)) 'splinter.dll')).Hash.ToLowerInvariant();
  scope='Actual CLI Program.RunAsync and native providers/publishers. The test actor only supplies the durable boundary observer; no fake coordinator/backend is selected.'}
 $child.Kill();$child.WaitForExit()
 Save 'native-cli-loss-killed' @{pid=$child.Id;startTicks=$ticks;nonce=$request.Nonce;exited=$child.HasExited;exitCode=$child.ExitCode}
 # A later edit must not enter the already fixed collection.
 $later=[byte[]]::new(32);for($i=0;$i -lt 32;$i++){$later[$i]=[byte]((7*17+$i*7)%256)}
 [IO.File]::WriteAllBytes($file,$later)
 $result=(Invoke-VerifiedCli 'native-cli-loss-recovered' $request.CliArguments).data.resultGeneration
 if($result.id -ne $capture.id -or $result.captureDigest -ne $capture.digest -or $result.requestKey -ne 'native-cli-loss'){
  throw 'The same actual CLI request did not recover its first fixed result'
 }
 $page=(Invoke-VerifiedCli 'native-cli-loss-fixed-files' @('workspace','read','--id',$workspace.id,'--collection','files','--result',$result.id,'--page-size','2','--home',$homePath)).data.page
 $entry=@($page.entries|Where-Object path -eq $relative)
 if($entry.Count -ne 1 -or $entry[0].hash -ne $beforeHash){throw 'The fixed result contains a later workspace edit'}
 if((Get-FileHash -LiteralPath $file).Hash.ToLowerInvariant() -eq $beforeHash){throw 'The later workspace edit was lost'}
 $retry=(Invoke-VerifiedCli 'native-cli-loss-completed-retry' $request.CliArguments).data.resultGeneration
 if($retry.id -ne $result.id -or $retry.captureDigest -ne $result.captureDigest){throw 'The completed request changed its fixed result'}
 $provider=Get-Process -Id $mount.providerProcessId -ErrorAction Stop
 if($provider.StartTime.ToUniversalTime().Ticks -ne $mount.providerStartTimeUtcTicks){throw 'The live provider lifetime changed during CLI recovery'}
 $provider.Dispose()
 Save 'native-cli-loss-complete' @{workspace=$workspace.id;result=$result.id;captureDigest=$result.captureDigest;providerLifetimePreserved=$true;laterWorkspaceEditPreserved=$true;completedAt=[DateTimeOffset]::UtcNow}
}finally{
 # Never kill an unconfirmed request on timeout. Only the explicitly observed
 # boundary above authorizes this one exact-process interruption.
 [IO.File]::WriteAllText((Join-Path $evidence 'native-cli-loss-stdout.log'),$(if($child.HasExited){$stdout.GetAwaiter().GetResult()}else{'Still live: observe the original request; no automatic termination.'}))
 [IO.File]::WriteAllText((Join-Path $evidence 'native-cli-loss-stderr.log'),$(if($child.HasExited){$stderr.GetAwaiter().GetResult()}else{'Still live: output remains attached to the same actor.'}))
 $child.Dispose()
}
