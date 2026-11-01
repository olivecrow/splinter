[CmdletBinding()]
param(
    [string]$FixtureParent,
    [string]$ProtocolPath = (Join-Path $PSScriptRoot '../../reports/benchmarks/scalable-storage-cost-protocol-20261101.json'),
    [string]$OutputPath = (Join-Path $PSScriptRoot '../../reports/benchmarks/scalable-storage-cost-20261101.json'),
    [string]$CandidateSourceManifest
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$OutputPath = [IO.Path]::GetFullPath($OutputPath)
if ([IO.File]::Exists($OutputPath) -or [IO.Directory]::Exists($OutputPath)) {
    throw "Preserve the existing report: $OutputPath. Supply a different -OutputPath for a later observation."
}
$issues = [Collections.Generic.List[object]]::new()
$binaryIdentities = @{}
function Read-Json([string]$Path) {
    if (![IO.File]::Exists($Path)) { return $null }
    try {
        $text = [IO.File]::ReadAllText($Path)
        if ([string]::IsNullOrWhiteSpace($text)) { return $null }
        return ConvertFrom-Json -InputObject $text -Depth 100 -NoEnumerate
    } catch {
        $issues.Add([ordered]@{ path=$Path; issue='UnreadableOrIncompleteJson'; exception=$_.Exception.GetType().FullName })
        return $null
    }
}
function Maximum($Rows, [string]$Property) {
    $values = @($Rows | ForEach-Object { if ($null -ne $_.$Property) { [double]$_.$Property } })
    if ($values.Count -eq 0) { return $null }
    return ($values | Measure-Object -Maximum).Maximum
}
function Sum-IfComplete($Rows, [string]$Property) {
    $values = @($Rows | ForEach-Object { $_ })
    if ($values.Count -eq 0 -or @($values | Where-Object { $null -eq $_.$Property }).Count -ne 0) { return $null }
    return ($values | Measure-Object -Property $Property -Sum).Sum
}
function File-Proof([string]$Path, [string]$Base) {
    if (![IO.File]::Exists($Path)) { return $null }
    return [ordered]@{ path=[IO.Path]::GetFullPath($Path); relativePath=[IO.Path]::GetRelativePath($Base,$Path).Replace('\','/'); bytes=([IO.FileInfo]$Path).Length; sha256=(Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
}
function Binary-Identity([string]$Cli) {
    if (!$Cli) { return $null }
    $key = [IO.Path]::GetFullPath($Cli).ToUpperInvariant()
    if ($binaryIdentities.ContainsKey($key)) { return $binaryIdentities[$key] }
    $directory = [IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($Cli)); $files = [Collections.Generic.List[object]]::new()
    if ([IO.Directory]::Exists($directory)) {
        foreach ($path in [IO.Directory]::EnumerateFiles($directory,'*',[IO.SearchOption]::AllDirectories) | Sort-Object) {
            if ([IO.Path]::GetExtension($path) -in '.dll','.exe' -or $path.EndsWith('.deps.json') -or $path.EndsWith('.runtimeconfig.json')) { $files.Add((File-Proof $path $directory)) }
        }
    }
    $canonical = ($files | ForEach-Object { $_.relativePath + '|' + $_.sha256 }) -join "`n"
    $identity = [ordered]@{ cliAssembly=[IO.Path]::GetFullPath($Cli); cliPresent=[IO.File]::Exists($Cli); observedAtSummary=[DateTimeOffset]::UtcNow.ToString('O'); binarySetSha256=[Convert]::ToHexStringLower([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($canonical))); files=$files.ToArray(); evidenceLimit='Hashes observe the retained exact CLI/dependency paths at summary time, cached per exact CLI path for this summary invocation. A supplied freeze manifest is needed to bind source files to the executed binaries.' }
    $binaryIdentities[$key] = $identity
    return $identity
}
function Group-Resources($Operations) {
    $metrics = @($Operations | ForEach-Object { $_.process }); $inside = @($Operations | ForEach-Object { $_.inside })
    $providers = @($Operations | ForEach-Object { $_.providers }); $errors = [Collections.Generic.List[object]]::new()
    foreach ($operation in $Operations) {
        foreach ($errorText in @($operation.process.samplingErrors)) { if ($null -ne $errorText) { $errors.Add(@{ operation=$operation.name; source='process-metrics'; error=$errorText }) } }
        foreach ($errorText in @($operation.after.Errors)) { if ($null -ne $errorText) { $errors.Add(@{ operation=$operation.name; source='owned-processes-after'; error=$errorText }) } }
    }
    return [ordered]@{
        sampledOwnedGroupPeakPrivateBytes=(Maximum $metrics 'sampledPeakOwnedPrivateBytes'); sampledOwnedGroupPeakWorkingSetBytes=(Maximum $metrics 'sampledPeakOwnedWorkingSetBytes')
        sampledOwnedGroupPeakHandles=(Maximum $metrics 'sampledPeakOwnedHandles'); sampledOwnedGroupPeakProcesses=(Maximum $metrics 'sampledPeakOwnedProcessCount')
        sampledCliPeakPrivateBytes=(Maximum $metrics 'sampledPeakPrivateBytes'); sampledCliPeakWorkingSetBytes=(Maximum $metrics 'sampledPeakWorkingSetBytes'); sampledCliPeakHandles=(Maximum $metrics 'sampledPeakHandles')
        cliPeakAccountedMetadataCacheBytes=(Maximum $inside 'peakMetadataCacheBytes'); cliLastAccountedMetadataCacheBytes=(Maximum $inside 'metadataCacheBytes')
        providerPeakAccountedMetadataCacheBytes=(Maximum $providers 'peakMetadataCacheBytes'); providerObservedAccountedMetadataCacheBytes=(Maximum $providers 'metadataCacheBytes')
        providerObservedPayloadReadCacheBytes=(Maximum $providers 'payloadReadCacheBytes'); samplingErrors=$errors.ToArray(); samplingErrorCount=$errors.Count
        scope='Maxima of 100ms PID/start-verified group samples during recorded CLI commands, including owned providers/descendants; lower bounds, not absolute OS peaks. Measurement host and unsampled direct edit intervals are excluded. Provider gauge maxima are persisted observations, not simultaneous group sums. Baseline unavailable cache gauges remain null.'
    }
}
function Allocation-Category([string]$Path, [string]$Fixture) {
    $relative = [IO.Path]::GetRelativePath($Fixture,$Path).Replace('\','/').ToLowerInvariant()
    if ($relative.StartsWith('p/.git/')) { return 'original-git-administration-and-objects' }
    if ($relative.StartsWith('p/.plastic/')) { return 'original-plastic-administration' }
    if ($relative.StartsWith('p/')) { return 'current-original-files' }
    if ($relative.Contains('/journal-packing/') -or $relative.EndsWith('.pending') -or $relative.EndsWith('.tmp')) { return 'pending-or-preserved-artifacts-require-ownership-review' }
    if ($relative.StartsWith('h/content-store/')) { return 'retained-immutable-content' }
    if ($relative.StartsWith('h/range-store/')) { return 'retained-immutable-ranges' }
    if ($relative.StartsWith('h/metadata-store/')) { return 'retained-shared-metadata' }
    if ($relative.Contains('/captures/')) { return 'capture-controls-or-retained-backing-require-ownership-review' }
    if ($relative.StartsWith('h/records/')) { return 'retained-workspace-results-receipts-and-metadata' }
    if ($relative.StartsWith('h/project-bases/') -or $relative.StartsWith('h/library-bases/')) { return 'shared-basis-controls-or-required-backing-require-ownership-review' }
    return 'other-owned-home-data-require-ownership-review'
}
function Allocation-Residuals($Allocation, [string]$Fixture, [string]$InitialInventory) {
    if ($null -eq $Allocation -or ![IO.File]::Exists($Allocation.inventory)) { return $null }
    $initial = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    if ([IO.File]::Exists($InitialInventory)) { foreach ($line in [IO.File]::ReadLines($InitialInventory)) { $row=ConvertFrom-Json -InputObject $line; if ($row.identity) { [void]$initial.Add($row.identity) } } }
    $identities = @{}; $parseErrors = 0
    foreach ($line in [IO.File]::ReadLines($Allocation.inventory)) {
        try { $row=ConvertFrom-Json -InputObject $line } catch { $parseErrors++; continue }
        if (!$row.identity) { continue }
        if (!$identities.ContainsKey($row.identity)) { $identities[$row.identity]=@{ allocated=[long]$row.allocated; logical=[long]$row.logical; baseline=$initial.Contains($row.identity); categories=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal); aliases=0 } }
        $item=$identities[$row.identity]; [void]$item.categories.Add((Allocation-Category $row.path $Fixture)); $item.aliases++
    }
    $categories=@{}; $aliased=0; $newAllocated=0L; $newLogical=0L
    foreach ($item in $identities.Values) {
        $category=if ($item.baseline) { 'preexisting-input-identity' } elseif ($item.categories.Count -gt 1) { 'shared-identity-across-path-categories' } else { $item.categories | Select-Object -First 1 }
        if (!$categories.ContainsKey($category)) { $categories[$category]=@{category=$category; identities=0L; allocatedBytes=0L; logicalBytes=0L} }
        $categories[$category].identities++; $categories[$category].allocatedBytes+=$item.allocated; $categories[$category].logicalBytes+=$item.logical
        if ($item.aliases -gt 1) { $aliased++ }; if (!$item.baseline) { $newAllocated+=$item.allocated; $newLogical+=$item.logical }
    }
    return [ordered]@{ uniqueIdentities=$identities.Count; aliasedIdentities=$aliased; newAllocatedBytes=$newAllocated; newLogicalBytes=$newLogical; parseErrors=$parseErrors; categories=@($categories.Values | Sort-Object category); inventory=$Allocation.inventory; scope='Retained file allocation after task reclamation; unique native identities counted once. Path categories describe observed retained data, not proof that it is unreferenced garbage. Required immutable history/protected objects/shared backing, reader-deferred data and unknown preservation must be interpreted using ownership receipts. Measurement-only sets are outside product memory/timings.' }
}
$protocol = Read-Json ([IO.Path]::GetFullPath($ProtocolPath))
if ($null -eq $protocol -or $protocol.version -ne 1 -or $protocol.qualification -ne 'SCAL-08' -or $protocol.input.entries -ne 64) { throw 'The declared SCAL-08 64-entry protocol is required.' }
if ($protocol.orderPerSource.Count -ne 4) { throw 'The declared logical ABBA order must contain four positions.' }
foreach ($source in @('git','none','plastic')) {
    $indices=@($protocol.trialIndices.$source)
    if ($indices.Count -ne 4 -or @($indices | Select-Object -Unique).Count -ne 4 -or @($indices | Where-Object { $null -eq $_ -or $_ -isnot [long] -and $_ -isnot [int] -or $_ -lt 0 }).Count -ne 0) {
        throw ('The protocol must explicitly map four distinct nonnegative physical trial indices for '+$source)
    }
}
if (!$FixtureParent) {
    $pattern=[string]$protocol.input.fixturePattern; $offset=$pattern.LastIndexOf('sscs64',[StringComparison]::Ordinal)
    if ($offset -lt 1) { throw 'Supply -FixtureParent or a protocol with the fixed sscs64 fixture pattern.' }
    $FixtureParent=$pattern.Substring(0,$offset)
}
$phaseCommands = [ordered]@{
    start=@('ensure-a','ensure-b')
    bulk=@('capture-1','apply-1-plan','apply-1')
    selected=@('capture-2','apply-2-selected-plan','apply-2-selected','apply-2-remainder-plan','apply-2-remainder')
    final=@('capture-3','apply-3-plan','apply-3','release-a','capture-b','release-b')
}
$trials=[Collections.Generic.List[object]]::new()
foreach ($source in @('git','none','plastic')) {
    $letter=@{git='g';none='n';plastic='p'}[$source]
    for ($trial=0;$trial -lt 4;$trial++) {
        $physicalIndex=[int]$protocol.trialIndices.$source[$trial]
        $fixture=[IO.Path]::GetFullPath((Join-Path $FixtureParent ('sscs64'+$letter+$physicalIndex))); $evidence=Join-Path $fixture 'evidence'; $missing=[Collections.Generic.List[string]]::new()
        $complete=Read-Json (Join-Path $evidence 'lifecycle-complete.json'); $owner=Read-Json (Join-Path $fixture 'fixture-owner.json'); $operations=[Collections.Generic.List[object]]::new(); $phases=[Collections.Generic.List[object]]::new()
        if ($null -eq $complete) { $missing.Add('lifecycle-complete.json') }
        if ($null -eq $owner) { $missing.Add('fixture-owner.json') }
        $scopeValid=($null -eq $owner -or ($owner.source -eq $source -and $owner.entries -eq 64)) -and ($null -eq $complete -or ($complete.source -eq $source -and $complete.entries -eq 64))
        if (!$scopeValid) { $issues.Add(@{path=$fixture;issue='FixtureScopeDoesNotMatchDeclaredSourceAnd64Entries'}) }
        foreach ($phase in $phaseCommands.Keys) {
            $names=@($phaseCommands[$phase]); if ($source -eq 'none' -and $phase -eq 'start') { $names=@('project-prepare')+$names }; if ($source -eq 'none' -and $phase -eq 'final') { $names+='project-restore' }
            $phaseOps=[Collections.Generic.List[object]]::new(); $phaseMissing=[Collections.Generic.List[string]]::new()
            foreach ($name in $names) {
                $metric=Read-Json (Join-Path $evidence ($name+'.process-metrics.json')); $inside=Read-Json (Join-Path $evidence ($name+'.inside-metrics.json')); $providers=Read-Json (Join-Path $evidence ($name+'.provider-cache-gauges.json')); $after=Read-Json (Join-Path $evidence ($name+'.owned-processes-after.json'))
                if ($null -eq $metric) { $missing.Add($name+'.process-metrics.json'); $phaseMissing.Add($name+'.process-metrics.json') }
                if ($null -eq $inside) { $missing.Add($name+'.inside-metrics.json') }
                foreach ($suffix in @('.provider-cache-gauges.json','.owned-processes-after.json')) {
                    if (![IO.File]::Exists((Join-Path $evidence ($name+$suffix)))) { $missing.Add($name+$suffix) }
                }
                if ($null -ne $metric -and ($null -eq $metric.seconds -or [double]::IsNaN([double]$metric.seconds) -or [double]::IsInfinity([double]$metric.seconds) -or [double]$metric.seconds -lt 0)) {
                    $missing.Add($name+'.valid-duration'); $phaseMissing.Add($name+'.valid-duration')
                }
                if ($null -ne $metric -and $metric.exitCode -ne 0) { $issues.Add(@{path=(Join-Path $evidence ($name+'.process-metrics.json'));issue='NonzeroLifecycleCommandExit';exitCode=$metric.exitCode}) }
                $operation=[ordered]@{name=$name;phase=$phase;process=$metric;inside=$inside;providers=@($providers);after=$after}; $phaseOps.Add($operation);$operations.Add($operation)
            }
            $edit=if ($phase -eq 'bulk') { Read-Json (Join-Path $evidence 'bulk-edit-metrics.json') } else { $null }
            if ($phase -eq 'bulk' -and $null -eq $edit) { $missing.Add('bulk-edit-metrics.json');$phaseMissing.Add('bulk-edit-metrics.json') }
            if ($phase -eq 'bulk' -and $null -ne $edit -and ($null -eq $edit.seconds -or [double]::IsNaN([double]$edit.seconds) -or [double]::IsInfinity([double]$edit.seconds) -or [double]$edit.seconds -lt 0)) {
                $missing.Add('bulk-edit-valid-duration'); $phaseMissing.Add('bulk-edit-valid-duration')
            }
            $seconds=if ($phaseMissing.Count -eq 0) { (Sum-IfComplete @($phaseOps | ForEach-Object { $_.process }) 'seconds') + $(if ($edit) {[double]$edit.seconds} else {0}) } else {$null}
            $allocation=Read-Json (Join-Path $evidence ('allocation-after-'+$phase+'.json'))
            if ($null -eq $allocation) { $missing.Add('allocation-after-'+$phase+'.json') }
            elseif (!$allocation.inventory -or ![IO.File]::Exists($allocation.inventory)) { $missing.Add('allocation-after-'+$phase+'.json.files.jsonl') }
            $phases.Add([ordered]@{phase=$phase;primarySeconds=$seconds;directEditSeconds=if($edit){$edit.seconds}else{$null};missing=$phaseMissing.ToArray();commands=$phaseOps.ToArray();resources=(Group-Resources $phaseOps);allocation=$allocation})
        }
        $excluded=[Collections.Generic.List[object]]::new(); $included=@{};foreach($operation in $operations){$included[$operation.name]=$true}
        if ([IO.Directory]::Exists($evidence)) { foreach($path in [IO.Directory]::EnumerateFiles($evidence,'*.process-metrics.json') | Sort-Object) { $name=[IO.Path]::GetFileName($path).Replace('.process-metrics.json',''); if(!$included.ContainsKey($name)){$excluded.Add(@{name=$name;process=(Read-Json $path);reason='Outside declared primary lifecycle sum'})} } }
        $start=Read-Json (Join-Path $evidence 'stage-start.intent.json');$cli=if($complete){$complete.cliAssembly}elseif($start){$start.cliAssembly}else{$null}
        $before=Read-Json (Join-Path $evidence 'allocation-before.json'); if ($null -eq $before) { $missing.Add('allocation-before.json') }
        if (![IO.File]::Exists((Join-Path $evidence 'allocation-before.json.files.jsonl'))) { $missing.Add('allocation-before.json.files.jsonl') }
        $final=@($phases | Where-Object phase -eq 'final')[0].allocation
        $trials.Add([ordered]@{ source=$source;trial=$trial;logicalPosition=$trial;physicalIndex=$physicalIndex;candidate=$protocol.orderPerSource[$trial];fixture=$fixture;evidence=$evidence;status=if($complete -and $missing.Count -eq 0 -and $scopeValid){'Complete'}else{'InProgress'};lifecycleComplete=$complete;missing=$missing.ToArray();primaryLifecycleSeconds=(Sum-IfComplete $phases 'primarySeconds');phases=$phases.ToArray();resources=(Group-Resources $operations);excludedOperations=$excluded.ToArray();allocationBefore=$before;retainedAfterReclamation=(Allocation-Residuals $final $fixture (Join-Path $evidence 'allocation-before.json.files.jsonl'));executionIdentity=(Binary-Identity $cli) })
    }
}
function Compare-Cost($Selected, [string]$Phase) {
    $values=@($Selected | ForEach-Object { [ordered]@{trial=$_.trial;logicalPosition=$_.logicalPosition;physicalIndex=$_.physicalIndex;fixture=$_.fixture;candidate=$_.candidate;seconds=if($Phase -eq 'lifecycle'){$_.primaryLifecycleSeconds}else{@($_.phases|Where-Object phase -eq $Phase)[0].primarySeconds}} })
    $baseline=@($values|Where-Object candidate -eq 'baseline');$candidate=@($values|Where-Object candidate -eq 'candidate')
    if (@($values|Where-Object {$null -eq $_.seconds}).Count -ne 0) { return [ordered]@{phase=$Phase;status='InProgress';observations=$values;baselineMeanSeconds=$null;baselineMaximumSeconds=$null;thresholdSeconds=$null;sustainedRegression=$null} }
    $mean=($baseline|Measure-Object seconds -Average).Average;$maximum=($baseline|Measure-Object seconds -Maximum).Maximum;$floor=if($Phase -eq 'lifecycle'){5}else{2};$threshold=[Math]::Max($floor,0.1*$mean)
    $regression=@($candidate|Where-Object {$_.seconds -gt $maximum+$threshold}).Count -eq 2
    return [ordered]@{phase=$Phase;status=if($regression){'SustainedRegression'}else{'NoSustainedRegressionObserved'};observations=$values;baselineMeanSeconds=$mean;baselineMaximumSeconds=$maximum;candidateMeanSeconds=($candidate|Measure-Object seconds -Average).Average;thresholdSeconds=$threshold;candidateMustExceedSeconds=$maximum+$threshold;sustainedRegression=$regression;scope='Two observations per version, bounded declared regression check; not a population confidence interval or general speedup claim.'}
}
$comparisons=@(foreach($source in @('git','none','plastic')){
    $selected=@($trials|Where-Object source -eq $source)
    $binaryConsistency=@(foreach($version in @('baseline','candidate')){
        $observations=@($selected|Where-Object {$_.candidate -eq $version -and $_.executionIdentity.cliPresent}|ForEach-Object {$_.executionIdentity.binarySetSha256})
        $distinct=@($observations|Select-Object -Unique)
        if($distinct.Count -gt 1){$issues.Add(@{source=$source;candidate=$version;issue='DifferentRetainedBinaryIdentitiesAcrossMatchedTrials';hashes=$distinct})}
        [ordered]@{candidate=$version;observedTrials=$observations.Count;binarySetHashes=$distinct;sameRetainedBinarySet=if($observations.Count -eq 2){$distinct.Count -eq 1}else{$null}}
    })
    [ordered]@{source=$source;costs=@(foreach($phase in @('lifecycle','start','bulk','selected','final')){Compare-Cost $selected $phase});binaryConsistency=$binaryConsistency}
})
$sourceFiles=[Collections.Generic.List[object]]::new()
foreach($area in @('src','contracts','scripts/verification')){foreach($path in [IO.Directory]::EnumerateFiles((Join-Path $repoRoot $area),'*',[IO.SearchOption]::AllDirectories)|Sort-Object){if($path -match '[\\/](bin|obj)[\\/]'){continue};if([IO.Path]::GetExtension($path) -in '.cs','.csproj','.props','.targets','.json','.ps1'){$sourceFiles.Add((File-Proof $path $repoRoot))}}}
foreach($name in @('Directory.Build.props','Directory.Build.targets','Directory.Packages.props','global.json','Splinter.slnx')) {
    $path=Join-Path $repoRoot $name; if([IO.File]::Exists($path)) { $sourceFiles.Add((File-Proof $path $repoRoot)) }
}
$manifest=if($CandidateSourceManifest){[ordered]@{file=(File-Proof ([IO.Path]::GetFullPath($CandidateSourceManifest)) $repoRoot);document=(Read-Json ([IO.Path]::GetFullPath($CandidateSourceManifest)))}}else{$null}
$allComplete=@($trials|Where-Object status -ne 'Complete').Count -eq 0;$regressions=@($comparisons|ForEach-Object{$_.costs}|Where-Object sustainedRegression -eq $true)
$report=[ordered]@{
    version=1;qualification='SCAL-08';observedAt=[DateTimeOffset]::UtcNow.ToString('O');status=if(!$allComplete){'InProgress'}elseif($regressions.Count -ne 0){'RegressionDetected'}elseif($issues.Count -ne 0){'RequiresReview'}else{'Complete'}
    protocol=[ordered]@{file=(File-Proof ([IO.Path]::GetFullPath($ProtocolPath)) $repoRoot);document=$protocol};trials=$trials.ToArray();completedTrials=@($trials|Where-Object status -eq 'Complete').Count;requiredTrials=12;comparisons=$comparisons;excludedDiagnosis=@($protocol.excludedDiagnosis);issues=$issues.ToArray()
    candidateSource=[ordered]@{freezeManifest=$manifest;summaryObservedSourceHashes=$sourceFiles.ToArray();bindingStatus=if($manifest.file -and $manifest.document){'ProvidedFreezeManifestRequiresComparisonToExecutionIdentity'}else{'SummaryTimeSourceOnlyNotProvenExecutedSource'};scope='Current source hashes are an observation, never inferred from assembly path or claimed as the executed snapshot without a retained freeze manifest.'}
    limitations=@('Only this declared 64-entry Windows editorless ABBA input; no larger-input or Unity lifecycle performance inference.','Time gate excludes schema/capabilities/status and allocation scans; all excluded command timings remain visible.','Group samples are lower bounds with errors retained; direct edit intervals and measurement host are outside sampled CLI groups.','Null old-binary cache gauges remain unavailable, not zero; accounted cache is distinct from resident metadata/private memory.','Native allocation is non-atomic unique file-identity allocation, excluding directory/MFT/system-cache costs and virtual views.','Retained allocation is not automatically a leak; immutable results, protected objects, shared backing, reader-deferred and unknown preserved data require ownership interpretation.','A Complete report describes cost evidence, not completion of SCAL-01 through SCAL-08 or general speed/RAM claims.')
}
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($OutputPath))|Out-Null
$output=[IO.FileStream]::new($OutputPath,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::Read)
$writer=[IO.StreamWriter]::new($output,[Text.UTF8Encoding]::new($false),65536,$true)
try{$writer.Write(($report|ConvertTo-Json -Depth 100));$writer.Flush();$output.Flush($true)}finally{$writer.Dispose();$output.Dispose()}
[pscustomobject]@{output=$OutputPath;status=$report.status;completedTrials=$report.completedTrials;requiredTrials=12;sustainedRegressions=$regressions.Count;issues=$issues.Count}|ConvertTo-Json -Compress
