<a id="작은-unity-재개-비교-실행"></a>
# Small Unity resumption comparison run

[한국어](../ko/verification/unity-replay-comparison.md)

`ru-20260927` [RU-01–04](unity-replay-comparison.md) closed **scoped-complete-with-limitations**. Product baseline `2cfcad07069e48cde64a3fa9cbfd70a1b546db5e`. Six same-journal reconnects/six actual Editor resumptions, independent results/original preservation/all owned resources reclaimed/result reads after VHD detach checked. Same-journal mount median 33.84→23.70 seconds, but total actual Editor-resumption improvement on accumulated inputs unproven.

<a id="입력과-비용-경계"></a>
## Inputs and cost boundaries

Existing isolated NTFS VHD/`V:\sf-compaction-20260926\unity` reused. Original HEAD `58f81b5bc6ff8a940d8a90872bd3fed858a54cc1`, 90 tracked files/19 existing protected refs/no Library. Only new owned snapshot/`V:\ru27` worker-immutable candidate-fixed view-one common cache. No canonical project/large PV/Coordination/Codex worktree.

Compared Core/Runtime from existing `d48b3b0`/improved `2cfcad0`. Surrounding CLI/Git/Unity/Windows assemblies common; hashes confirm only `Splinter.Core.dll`/`Splinter.Runtime.dll` differ among 41 files. Not separate fresh builds of both entire commits.

| Execution bundle | File count | Logical B | Copy seconds | Runtime SHA-256 |
| --- | ---: | ---: | ---: | --- |
| baseline | 41 | 3,137,191 | 1.0750 | `F44D4FFB2FE057EBE3305B3823CCB4A919C1EBE0831B30FA19BCA23D8A70D2FD` |
| current | 41 | 3,140,263 | 0.1786 | `4948624B8F45FB57AA08CF1EF14303C14875D4B19E60658223EE2C1AB91A6B89` |

No full project-Library copying/hydration/journal rewind. Initial worker mount 2.09 seconds/small candidate export 0.86 seconds. Small binary copies/candidate publications do not replace actual Editor-task preparation. Bundles inside measurement-evidence directory, not double-counted with evidence allocation.

No product-code changes; existing RI final build/locked restore/automated **300 passed, 0 failed, 1 test excluded at that time skipped** reused. Before this run 134 product-source hashes matched verification manifest. Not marked as automated tests rerun here.

Candidate `Calculate(3) = 23`, `Target.png.meta` `maxTextureSize = 1024`, separate `CandidateA.txt`/`.meta`. Checks actual loaded code/1024×1024 texture/3 meshes/same candidate files; simple content checks distinct from actual Unity execution.

<a id="첫-준비와-운영-재시도"></a>
## First preparation and operational retries

First `ready` heartbeat **1,495.67 seconds** after Editor-start call. Subsequent explicit refresh accepted but exceeded 55-second collection limit. Same request-ID/fingerprint success arrived **68.87 seconds** from refresh/**1,565.16 seconds** from start; collected without new refresh. First-ready observation not code-asset verification completion.

First preview PNG sky/background only; same Editor second PNG model. Code/texture tests passed both. First-render cause unconfirmed; VR-03 Partial/existing limits retained.

First normal stop returned `EditorStopTimeout` after **32.12 seconds**. Same Editor completed normal quit while logs checked; same stop-API retry **0.80 seconds** confirmed owned application processes/working set/private bytes 0. First-start→retry-return **61.24 seconds** includes inspection/between-call delay. No force; failure response not overwritten as success.

Initial Git preparation called `git-prepare` after mount, rejected `UnsafePath`. Order corrected normal unmount → prepare → remount → attach; rejection/retry evidence retained. Preparation/validation collection/stop retry are operational costs, not removed from repeated samples/hidden by initial-preparation success.

<a id="같은-닫힌-저널의-재연결"></a>
## Same closed-journal reconnects

Normally stopped cache journal **2,269,880,373B/331,093 frames**, source **9,442,536B** fixed. Round 1 baseline/current, 2 current/baseline, 3 baseline/current. Each checks fresh provider PID/start/latest mounted/actual mount, reads candidate files/normally unmounts. All six journals unchanged hashes before/after; I/O errors/open handles/remaining providers 0.

| Implementation | Cache replay seconds, rounds 1/2/3 | Replay median seconds | Mount CLI seconds, rounds 1/2/3 | Mount median seconds |
| --- | --- | ---: | --- | ---: |
| baseline | 29.73 / 32.01 / 36.52 | 32.01 | 31.65 / 33.84 / 38.47 | 33.84 |
| current | 26.17 / 22.22 / 19.89 | 22.22 | 28.08 / 23.70 / 21.44 | 23.70 |

Replay median decreased about 30.6%/mount CLI about 30.0% for this input. Before/after hashes/repeated replay warm OS cache, not emptied. Three samples without fixed CPU/power/JIT/GC states. Not same environment-input matching as historical VR/RI/no general Unity improvement.

All six cache-directory allocation **2,269,884,968B**, source **9,446,008B**, identical. Runtime prefix reuse is not disk savings; managed allocation/file allocation/full-task resumption not interchangeable. Fixed hashes cache `ed73d6ac9ee5a797c10acf96af45423b79f9d4504521f14d0c0b3da448c20dc5`, source `69df69560bf04b8f67fa2d804125ab0d6faadc849801e91673de37007889d7f2`.

Index-application median 27.56→17.61 seconds. Current per-operation medians rename 9.28 seconds (4,032 frames), write 8.17 seconds (195,268 frames), other create/delete/metadata/resize 0.062/0.001/0.048/0.058 seconds. Per-run frame/index ticks sum matches total. Sum of operation medians not overall median; missing baseline operation counters unknown. Remaining costs not explained solely by rename-prefix allocation.

<a id="실제-editor-재개와-파일-할당"></a>
## Actual Editor resumption and file allocation

All three runs/version confirm current-request successful compile, loaded `23`, 1024×1024 texture/3 meshes. Same candidate/cache, but Editor writes accumulate inputs; no previous-journal restoration/separate Library copies. Table actual order. Ready latest heartbeat; verification completion explicit compile response/code-assets; stop includes final reclamation confirmation after timeout.

| Order / implementation | Mount seconds | Editor start→ready seconds | Mount start→ready seconds | Mount start→verification complete seconds | Stop/recheck seconds |
| --- | ---: | ---: | ---: | ---: | ---: |
| 1 / baseline-1 | 30.78 | 126.17 | 157.03 | 211.45 | 41.74 |
| 2 / current-1 | 35.48 | 131.37 | 166.89 | 369.71 | 67.50 |
| 3 / current-2 | 39.56 | 109.28 | 148.87 | 188.17 | 37.45 |
| 4 / baseline-2 | 40.86 | 74.79 | 115.68 | 146.65 | 36.74 |
| 5 / baseline-3 | 46.29 | 71.30 | 117.62 | 148.36 | 36.34 |
| 6 / current-3 | 47.70 | 71.51 | 119.23 | 150.21 | 36.11 |

Baseline/current medians mount 40.86/39.56 seconds, mount-start→ready **117.62/148.87 seconds**, mount-start→verified **148.36/188.17 seconds**, stop-recheck 36.74/37.45 seconds. Sequential runs differ in inputs/large initialization variance, so total Editor-resumption improvement/regression attributable to RI unproven. Same-closed-journal ~30% shortening not applied to total resumption.

Current-1 retains 3 `EditorFocusRequired` failures. Exact owned PID/start/actual returned Unity window compared, activated; fourth refresh accepted. Same Editor/journal/initial-ready records continued; 369.71 seconds includes focus diagnosis/recovery/measurement-script resumption delays. Other five no focus retries.

All six initial stops returned `EditorStopTimeout` after 31.72–32.24 seconds. Waited for natural same-owned-Editor exit/rechecked same stop API, confirming processes/memory 0 each. Seven including cold had no force, but not intervention-free single-call shutdown successes. Final unmount each 1.07–1.71 seconds.

Cache journal cold-end **2,269,880,373B**→after six **2,376,470,766B**, growth **106,590,393B**. Per-run growth actual order 17,873,042 / 18,317,816 / 17,881,115 / 17,519,575 / 17,531,165 / 17,467,680B. Source 9,442,536→9,616,656B. Logical lengths, distinct from allocation.

Current write-index application fixed-input median 8.17 seconds→first restart input 16.15→second 22.36 seconds. Evidence to investigate repeated-Editor records/index costs together; per-path payload sizes alone do not identify CPU bottlenecks. Follow-up capacity/resumption candidate is cumulative write-range index costs; no flush changes/automatic compaction/new product mode here.

<a id="파일-할당-장부"></a>
## File-allocation accounting

Directory-file allocation observed with `FILE_STANDARD_INFO.AllocationSize`. All roots complete across **366** approximately 10-second/stage samples, without guaranteeing instantaneous maxima. Library writes inside cache journal, not added as separate Library costs.

| Time | Cache allocation B | Source allocation B | Owned-workspace file total B |
| --- | ---: | ---: | ---: |
| Cold shutdown | 2,269,884,968 | 9,446,008 | 2,282,061,120 |
| baseline-1 shutdown | 2,287,755,816 | 9,474,680 | 2,299,666,080 |
| current-1 shutdown | 2,306,073,128 | 9,503,352 | 2,318,012,064 |
| current-2 shutdown | 2,323,956,264 | 9,532,024 | 2,335,923,872 |
| baseline-2 shutdown | 2,341,474,856 | 9,560,696 | 2,353,467,040 |
| baseline-3 shutdown | 2,359,005,736 | 9,589,368 | 2,371,026,592 |
| current-3 shutdown / result preservation | 2,376,475,176 | 9,618,040 | 2,388,524,704 |

Highest observed owned-workspace allocation **2,388,524,704B (about 2.389GB)**. At preservation: worker 8,752B, candidate 12,848B, source 9,618,040B, cache 2,376,475,176B, control 2,372,560B, snapshot-control 37,328B. No original Library initially/created later.

Common Git allocation before creation 6,117,288B→after preservation 6,122,568B, **5,280B** growth. Intermediate registration retained 6,130,080B. Independent-result branch remains there; C: separate bundle/verification repository in evidence root. Bundle logical length 6,000,213B/export-fetch 1.19 seconds.

Post-preservation evidence-root sample 23,950,000B. Workspace + Git-growth over baseline + evidence allocation **2,412,479,984B**. Two binary bundles/independent results not added again. Later preserved control-receipt copies/measurement results/document checks enter subsequent observations; not final evidence-directory size/actual global peak.

Isolated V: shared VSS used/allocated before creation 0/0B, during import 27,000,832/100,663,296B, before reclamation 27,131,904/100,663,296B. Volume totals, not attributed/added as workspace allocation. Existing separate C: VSS observed, not these workspace costs. Allocation excludes MFT/directory metadata/OS cache/Unity global caches-logs outside roots/unobserved peaks. Existing VHD backing-file shrinkage/host-disk reclamation rate unverified.

Read-only final closed-journal diagnostics verify all checksums/sequences/byte totals. Performed after comparison; cache analysis about 7 minutes. Start time from process query at seconds precision, distinct from precise wall-clock measurement/not summed into resumption figures.

| Journal | Frames | Logical length B | Live payload B | Superseded payload B | Metadata/frame B |
| --- | ---: | ---: | ---: | ---: | ---: |
| cache | 407,225 | 2,376,470,766 | 1,828,055,921 | 359,787,187 | 188,627,658 |
| source | 2,570 | 9,616,656 | 8,438,867 | 76,681 | 1,101,108 |

Of both live payloads totaling 1,836,494,788B, Library 1,827,981,842B (about 99.54%). Superseded payload not converted to immediately reclaimable physical space/compaction peak reduction. Actual journals reclaimed/final hashes-breakdowns retained.

<a id="결과-보존과-회수"></a>
## Result preservation and reclamation

Independent result **`cd968022c81020ee7975188115de1c8019859062`**, retained `codex/ru27/view-a1`/`refs/splinter/snapshot-results/ru27-view`. Only four intended files; temporary Unity bridge excluded. C: `independent-result.bundle`/`independent-result.git` without external alternates also confirm same revision/code/metadata/added files. Bundle SHA-256 `491fffe36b1e40627287c223d3861345d00656cb608941f7479fb761154468ec`.

Preservation remount 54.21 seconds/result collection 2.50/unmount 1.55/deregistration 1.58/separate bundle export-fetch 1.19 individually recorded. Last long replay part of completion costs; small candidate-publication timing does not represent whole task.

First reclamation audit treated newly created `refs/splinter/snapshot-results/ru27-view` as existing-ref modification/stopped before deletion. Difference only that owned result ref; original 19 refs unchanged/not lost. Test-audit allowlist supplemented to check exact ref/revision separately; all original 19 compared again. Original/product guards unchanged to pass.

Final audit confirms original HEAD/clean/90 raw hashes/19 refs identical. Sixteen provider-exit/seven Editor-exit records including cold verify owned PID/start/no actual mount/open handles-I/O errors 0/application processes-working set-private bytes 0. Three measurement helpers ended; owned paths/no reparse checked before reclaiming only data. Data deletion 0.61 seconds, not full audit/preservation/VSS-release time.

After owned snapshot `{529A8CED-493B-48C4-A2CC-039DFCDEC294}` release, V: snapshots 0/VSS used-allocated 0/0B, existing C: `{78AD7813-DF73-4845-8513-F14CC6E87331}` alive. Evidence of existing-ID preservation on another volume, not general non-owned backup coexistence/external concurrent deletion guarantees. Original/all existing refs/independent-result Git fsck pass again.

After reclamation owned-workspace allocation **0B**, original Library still absent, common Git growth 5,280B. Retained evidence-root **26,439,696B** includes independent results/execution bundles/closed control/measurement materials; later small summary-check outputs excluded. V: free space before creation 16,952,979,456B/after snapshot reclamation 16,952,954,880B, not whole physical reclamation rate.

Finally restricted administrator session detached VHD/ended. `Get-DiskImage.Attached = false`, V: absent/administrator PID exit confirmed. With disk absent, C: independent-repository revision/content/fsck/bundle hash match. Final owned mount/application/provider/Git registration/delta/snapshot all 0; results/measurement evidence/existing VHD file retained.

<a id="판정과-증거"></a>
## Assessment and evidence

| ID | Assessment | Scope/limits |
| --- | --- | --- |
| RU-01 | Passed | Original/hash/refs/small bundles/common surrounding layers checked. RI automated 300/0/1 reuses same-source prior evidence |
| RU-02 | Passed, limits retained | Cold import without force/delayed same-request compile/actual code-assets. First PNG blank/stop-timeout retry retained |
| RU-03 | Passed, bounded observation | Same-journal mount three each/accumulating-input Editor resumption three each/allocation. Total Editor-improvement rate/precise global peak unproven |
| RU-04 | Passed | Independent results/bundle-repository/original-existing refs-other-volume snapshots preserved/owned resources reclaimed/result verified after VHD detach |

Overall SF-03 Partial/VR-03 first-render cause unresolved. Every normal quit required stop recheck; one focus-recovery sample/input accumulation/warm OS cache/few samples remain.

Raw evidence `artifacts/storage-foundation/ru-20260927/`. Main evidence `code-and-scope.json`, `binary-manifests.json`, `protected-source.json`, `cold-observation-complete.json`, `cold-stop-completion.json`, `replay-comparison.json`, `editor-comparison.json`, `space-*.json`, `measurement-summary.json`, `*-final-accounting.json`, `pre-reclamation-audit.json`, `independent-result.json`, `closeout.json`, `final-environment-check.json`. All initial rejection/retry responses/Editor logs/previews/fixed administrator requests-responses/measurement scripts retained. Final summary computed from saved observations by `summarize.py`. Flush policy/default mode/product Coordination connections/large PV out of scope.
