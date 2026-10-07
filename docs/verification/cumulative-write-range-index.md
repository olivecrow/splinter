<a id="누적-write-범위-index-검증"></a>
# Cumulative write-range index verification

[한국어](../ko/verification/cumulative-write-range-index.md)

`wi-20260927` closes [WI-01–05 scope](cumulative-write-range-index.md) as **scoped-complete-with-limitations**. Baseline is uninstrumented Runtime at `2cfcad0`; instrumented Runtime retains full-list copying/sorting; final candidate replaces only affected regions of sorted extents. Fixed synthetic inputs, a new small Unity journal, actual Editor, independent results/reclamation checked. Full Editor-resumption improvement/physical Library savings unproven.

<a id="합성-원인-판별과-회귀"></a>
## Synthetic cause isolation and regression

Existing identical-input `append`/`overlap`/`file-renames` journal/state hashes/output verification match baseline/final candidate (`evidence/existing-profile-final-regression.json`). Four added fixed inputs also match checksum/sequence/independent expected-file hash/state-input hashes across all three implementations. New journal lengths: sequential 4,096 writes 5,662,179B; sequential 16,384 22,676,107B; same-range overwrites 22,517,584B; distributed across 128 files 22,714,867B; total 73,570,737B. Each within 32MiB limit.

Each input replayed three times in separate processes with baseline/instrumented/final-candidate order changed each round. Full input hashes read before execution, so warm OS cache/three small samples. Below are each implementation's **medians**; `write` is `replayIndexOperations.write.applyTicks`. Per-sample raw values/binary hashes in `artifacts/storage-foundation/wi-20260927/evidence/final-qualified-*.json`. Preliminary comparison before separating `write-compacted` counters also preserved in separate `qualified-*` files.

| Input | Implementation | Replay seconds | Write seconds | Cumulative managed allocation MiB | Peak working set MiB |
| --- | --- | ---: | ---: | ---: | ---: |
| One file sequential 4,096 | Baseline / instrumented / candidate | 0.545 / 0.565 / 0.061 | 0.480 / 0.497 / 0.003 | 71.5 / 71.5 / 7.4 | 54.4 / 55.3 / 44.2 |
| One file sequential 16,384 | Baseline / instrumented / candidate | 3.032 / 3.011 / 0.196 | 2.892 / 2.873 / 0.011 | 1,053.3 / 1,053.3 / 28.4 | 64.4 / 64.7 / 59.1 |
| Same-range overwrite 16,384 | Baseline / instrumented / candidate | 0.196 / 0.197 / 0.197 | 0.011 / 0.012 / 0.013 | 29.1 / 29.1 / 28.5 | 58.6 / 58.7 / 58.5 |
| Distributed across 128 files 16,384 | Baseline / instrumented / candidate | 0.243 / 0.245 / 0.202 | 0.052 / 0.055 / 0.011 | 37.5 / 37.5 / 28.8 | 60.1 / 59.9 / 59.2 |

Instrumented single-file sequential 16,384 `write` median 2.873 seconds includes full sorting 2.299 seconds/list traversal-copying 0.566 seconds. Sum of previous extent counts 134,209,536/max 16,383; sequential input visited/copied that many. Candidate same distribution visits 16,383/copies 0. Same-frame-count repeated overwrites have short lists/`write` about 0.01 seconds. Costs thus follow full processing of accumulated extents per file, not frame count alone. Uninstrumented/instrumented three-run ranges disclose instrumentation overhead; median differences smaller than sample spread are not improvements/regressions.

Candidate appends at end for append paths; otherwise binary-searches overlap in sorted extents/local replacement. Existing left/right fragments/journal offsets retained; list capacity reserved before mutation. Journal format/frame checksum/sequence/seal/acknowledged-write flush unchanged. Automated tests added arbitrary overlap/hole/truncate/extension/reconnect bytes-hashes, existing journal regression, negative-value rejection/missing historical-counter compatibility in `replayWriteRangeIndex` schema. Final candidate excludes `write-compacted` from ordinary `write` counts. Final standard `pwsh ./scripts/build.ps1`: locked restore/Release 0 warnings/errors, **301 passed, 0 failed, 1 test excluded at that time skipped**; TRX `evidence/standard-build-tests-capability-final.trx` retained.

Four synthetic-input directories logical size/`FILE_STANDARD_INFO` allocation 73,596,387/73,618,960B; baseline/instrumented/final probe bundles each 356,696B allocation, baseline/candidate Unity CLI bundles each 3,220,144B. Preliminary pre-separation candidate probe bundle separate. Cumulative managed allocation is a total over time, not simultaneous working set/disk savings. In particular synthetic 16,384 sequential managed-allocation reduction is not physical Unity Library savings.

<a id="실제-작은-unity와-회수"></a>
## Actual small Unity and reclamation

Actual input uses new `wi27` owned snapshot from existing isolated NTFS VHD small Unity fixture, not previous RU journal. Starting original clean HEAD `58f81b5bc6ff8a940d8a90872bd3fed858a54cc1`, 90 tracked files/21 existing refs/no original Library. Test-volume existing snapshots 0; C: existing snapshots separately protected. Product `snapshot-spike` mount/Git/Unity route verifies changing `Calculate(3)` to 23/texture import limit 1024. Surrounding CLI/Git/Unity/Windows files identical in baseline/current bundles; only Core/Runtime differ. First Git-prepare sandbox denial/authorized same-request retry retained as separate raw evidence.

Hashes confirm **only Core/Runtime DLLs differ** among 41-file baseline/final bundles. Final Runtime SHA-256 `03C9E464FB03E25006A092D55B192EACA846FAA27484C16385DB3519585039B0`. Initial Editor preparation started with DLL just before candidate ordinary-write-counter separation; after normal stop final DLL fixed. Closed-journal comparisons/candidate Editor resumption below all use final DLL. Initial `git-prepare` sandbox denial was not file/storage failure; authorized same request succeeded. Denial/retry separately preserved.

<a id="최초-준비와-같은-저널-비교"></a>
### Initial preparation and same-journal comparison

New-snapshot initial payload copying 0B. Small Unity first Editor start→`ready` **1,180.49 seconds**, normal stop 30.59 seconds. Separate build/automated tests ran during first import, so not isolated performance baseline. Same-PID loaded `Calculate(3)=23` checked. After stop/unmount new journal **2,272,161,094B**, **331,992 frames** sealed with SHA-256 `2A30E9F76E1F5C2FEB1AD9783EA05489CEEBD6D1DA4EAD6664C8D49998E4FA0F`, including 196,118 ordinary `write` frames.

Same closed-journal order: round 1 baseline/candidate, 2 candidate/baseline, 3 baseline/candidate. Each confirms fresh provider PID/start/latest mounted state/actual mount, reads code/texture settings, normally unmounts. All six share input length/hash; I/O errors/open handles/remaining mounts/providers 0.

| Implementation | Mount CLI seconds, rounds 1/2/3 | Median seconds | Replay seconds, rounds 1/2/3 | Median seconds | Write-index seconds, rounds 1/2/3 | Median seconds |
| --- | --- | ---: | --- | ---: | --- | ---: |
| Baseline | 11.67 / 10.87 / 10.96 | 10.96 | 10.56 / 10.03 / 10.07 | 10.07 | 5.73 / 5.75 / 5.59 | 5.73 |
| Final candidate | 5.90 / 5.62 / 5.60 | 5.62 | 4.80 / 4.76 / 4.71 | 4.76 | 0.142 / 0.131 / 0.125 | 0.131 |

For this input mount median decreased about 48.8%/replay median about 52.7%. Final `replayWriteRangeIndex.replaceCalls` equals 196,118 `write` frames; previous-extent-count sum 334,711,337/max 10,805, visited 1,490,638/list moves 113,168,556. Local replacement still moves lists, but eliminates full copying/sorting. Comparison uses warm OS cache from repeated hashes/fixed order/three small samples, not overall Editor-preparation savings.

<a id="실제-editor-재개와-비용-장부"></a>
### Actual Editor resumption and cost accounting

One final-candidate Editor resumption: mount **5.87 seconds**, start→`ready` **63.23 seconds**, mount-start→`ready` **78.38 seconds**, mount-start→current-request successful compile **119.42 seconds**. Loaded code 23, 1024×1024 texture/3 meshes/same PID confirmed. First PNG blank; same Editor second PNG rendered model. Cause unconfirmed. First normal stop returned `EditorStopTimeout` at **31.58 seconds**, but log showed exit proceeding/same PID disappeared without force. Rechecking same stop API: owned processes/working set/private bytes 0, unmount **1.50 seconds**. First-call-start→recheck completion **84.56 seconds** includes observation/retry delay.

Editor resumption increased journal **17,278,621B**, final **2,289,439,715B**, so excluded from six same-input comparisons above. Stage FILE_STANDARD_INFO observations: all six closed-comparison delta allocations identical **2,272,162,344B**. Highest owned delta/control/snapshot sum observed at candidate Editor shutdown/result preservation **2,291,852,832B**. Instantaneous peak/MFT-directory metadata/OS cache/VHD backing-file reduction unmeasured. Initial `before-cold` script omitted delta root; preserved sample/initial delta allocation `unknown`. Corrected subsequent stage samples all 6 roots complete.

Isolated V: shared VSS used/allocated before snapshot **0/0B**, immediately before reclamation **27,197,440/100,663,296B**, after **0/0B**. Volume totals, not added to owned-workspace file allocation. Common Git allocation initially **6,135,360B**, after result collection/deregistration **6,124,208B**; physical costs of only new result ref not separated. Independent-bundle logical length **5,999,972B**. C: verification-evidence root in `space-after-release.json` observed **98,303,776B** file allocation; later check logs/document edits outside sample. Synthetic inputs/execution bundles already included in evidence root, not double-counted.

<a id="독립-결과와-최종-판정"></a>
### Independent results and final assessment

Result revision **`1a840215f3de41a25eeb05a2027b3bd081a1f954`** retained in `codex/wi27/unity`/`refs/splinter/snapshot-results/wi27-unity`. Only two changed files: code/texture `.meta`; temporary Unity bridge excluded. C: bundle/bare repository without external alternates confirms same revision/content/`git fsck`. After collection, original HEAD/90 tracked-file hashes/clean/21 existing refs/no original Library remain. Nine owned-provider exit records, two Editor exits, Git registrations/mount/delta/snapshot 0 checked; only owned paths reclaimed. Existing C: snapshot `{78AD7813-DF73-4845-8513-F14CC6E87331}` retained. After VHD detach, reconfirmed `Get-DiskImage.Attached=false`, V: absent, restricted administrator PID ended, C: result revision/bundle hash/fsck matches.

| ID | Assessment | Limits |
| --- | --- | --- |
| WI-01 | Passed | Missing historical counters unknown; `write-compacted` excluded from ordinary write counts |
| WI-02 | Passed | Fixed synthetic inputs/warm cache/three small samples |
| WI-03 | Passed | One local-range-replacement change; flush/journal format unchanged |
| WI-04 | Passed, bounded observation | Same new closed-journal mounts three times each/one candidate actual Editor; total Editor improvement unproven, first PNG blank/stop recheck |
| WI-05 | Passed, bounded observation | Independent results/original-ref preservation/reclamation/VHD detach confirmed; instantaneous physical peak/some initial delta allocation unknown |

Raw evidence: synthetic comparisons/binary hashes/final TRX in `artifacts/storage-foundation/wi-20260927/evidence/`; same WI root `fixed-replay-comparison.json`, `space-*.json`, `closed-unity-input.json`, `candidate-editor-*.json`, `independent-result.json`, `pre-reclamation-audit.json`, `source-and-result-after-release.json`, `closeout.json`, `final-environment-check.json`, independent bundle/repository. Overall SF-02/large PV/default mode/first-render cause/Library capacity goals are not completed by this result.
