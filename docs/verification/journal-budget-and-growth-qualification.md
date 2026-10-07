<a id="sf-02-예산-제품-경로와-반복-저장량-검증"></a>
# SF-02 budget product path and repeated-storage verification

[한국어](../ko/verification/journal-budget-and-growth-qualification.md)

Executed 2026-09-26. Status: **defined BQ-01–05 scope complete, scoped-complete-with-limitations**. Follows [fixed scope](journal-budget-and-growth-qualification.md); evidence preserved in `artifacts/storage-foundation/bq-20260926/`. Product candidate `c56d065`, Windows x64, existing isolated 16GiB NTFS VHD/small Unity `6000.3.22f1` input. Reused/preserved existing originals/results; used new homes `V:\bq26\s`, `V:\bq26\u`/new task IDs. Next storage-saving candidate selected as **SF-03 immutable candidates/fixed validation view/minimal single-Library A/B/A scope**, not yet executed.

| Gate | Current judgment |
| --- | --- |
| BQ-01 | Passed: product locked restore/Release build/automatic 267 passed/0 failed/1 test excluded at the time skipped. 6 read-only ledger tests passed. Synthetic/Unity pre-creation Git-storage baselines/candidate hashes acquired |
| BQ-02 | Passed: actual product mount over-budget/omitted-argument retry rejection, journal hash/sequence/snapshot preservation/no candidate, explicit exact-overlap retry/content/same-snapshot resumption |
| BQ-03 | Passed, operational retries included: first load/compaction/same-snapshot resumption/3 actual edits with values/MVID/current validation/normal stop |
| BQ-04 | Passed, defined logical classification/separate file-allocation ledger: seals match before/after compaction/final collection, regrowth decomposition/concurrent-sample maximum/shared VSS. MFT/global cache/between-sample peak unknown |
| BQ-05 | Passed, preservation-path limits below included: two independent results/original hashes/HEAD/index/non-owned C: snapshot preservation, owned-resource reclaim/VHD detach/restricted administrator exit/one SF-03 candidate selected |

Synthetic workspace ID `e61be1640b78a9922e741b4e`, Unity ID `4ba126abe0feb6b7ea7c76b9`. Administrator session handles only fixed helper hashes/exact disk/volume of these two IDs; non-owned snapshot list/shared VSS series recorded separately.

Measurement reads journals at stopped boundaries after product replay/seal verification. Development Python ledger verifies each frame checksum/full hash; classifies logical range-overwrite/truncate/rename/delete/compaction bytes. Does not replace product replay. Only ASCII paths judged; non-ASCII explicitly rejected. No per-path physical allocation estimate/no assumption that all replaced payload equals actual compaction savings.

Additional normal provider unmount for immediate-post-compaction ledger uses only existing `snapshot-spike` `unmount`, a **development measurement intervention**. Workspace creation/compact/resumption/reclaim verified with product commands; intervention not advertised as new product command/unassisted execution evidence. Additional unmount/ensure/privilege-observation cost distinguished from direct compact cost.

Synthetic fixture lacked Unity validation; product checkpoint rejected `ValidationRequired`, uncollected release `UncollectedChanges`. Committed only changed files to fixture workspace Git branch, then product release preserved independent result. No app-validation success assigned to this path. Unity used one final checkpoint after actual loaded code/current validation.

Completion of bounded BQ execution not interpreted as full SF-02 Passed/large-project capacity goal achieved. Existing FailedPreservation/full SF-02 Partial/SF-03–06 NotRun retained.

<a id="후보와-합성-예산-경로"></a>
## Candidate and synthetic budget path

`candidate-manifest.json` fixes product source/contracts/test hashes; `candidate.trx`/`build.log` preserve locked restore/Release/267 passed/0 failed/1 test excluded at the time skipped. Code changes limited to two read-only development measurement scripts/their regression tests. 6 Python ledger cases in `accounting-tests-final.log`. Product Runtime/Core/CLI unchanged.

`original-baselines.json` fixes HEAD/index/tracked raw hashes for synthetic original `2aa147df53037cc31eaa2acbe04ab7330e66b49d`/small Unity original `58f81b5bc6ff8a940d8a90872bd3fed858a54cc1`. Pre-creation Git common allocation measured for both inputs. No new large assets/original Library.

Synthetic wrote same 4096B at offset 8192 of 1MiB file 100 times. Product `compact` budget 1B/new-CLI omitted-argument retry both rejected `CompactionOverlapBudgetExceeded`. Existing journal 453,147B + expected candidate 5,825B = 458,972B. Before/after rejection journal SHA256 `9411853b2dada69fb9f93c005657857b4fc131ac95c874e44345feaffab22ef1`, sequence 102/snapshot ID unchanged; no candidate/intent/previous generation. Saved budget 1B/`resume-compaction` retained.

Same-task retry with explicit exact 458,972B succeeded. Post-compaction journal 5,825B/live payload 4,177B (model 4,096B/Git pointer 81B)/replaced payload 0B. Pre-compaction replaced payload 405,504B. Stopped-boundary hash/sequence match product compaction seal; mount contents/source verified on same snapshot. Physical ledger journal allocation 458,752B→8,192B/owned additional files 1,232,944B→795,504B. Product receipt allocations at other observation times kept separate, not mixed.

Read synthetic independent result `54d2f05ff69a3b7374dc240a516cd653d176790d` after snapshot reclaim; result 1MiB SHA256 `f1afe330db09bcb82c41720c5dccf4a6fce92ffa599bd227b017e35fb07a8623`. Original HEAD/index/file hashes preserved. Reused same model blob as existing QJ result, so **not a new Git blob materialization-cost test**. After release preserved record/log/Git-result additional allocation 787,960B; reclaimed mount/delta/process/handles/owned snapshot. Detail: `source-budget-preservation.json`, `journal-source-*.json`, `source-closeout.json`.

<a id="작은-unity의-실제-로드와-재개"></a>
## Actual loading and resumption in small Unity

| Stage | `Calculate(3)` | assembly MVID | Editor PID | Validation |
| --- | --- | --- | --- | --- |
| Initial preparation | 13 | `078269ea-7b40-49d7-87f1-69956d6b0403` | 29404 | Ready/actual probe/current source checked |
| Edit 1 | 14 | `b06def67-f6a1-4a99-90df-9a479805c5e1` | 29404 | Current source/validation success |
| Same-snapshot resumption | 14 | `b06def67-f6a1-4a99-90df-9a479805c5e1` | 29056 | New generation/same code verified |
| Edit 2 | 15 | `3935ece1-9c95-4bc5-a24c-87c9804be976` | 29056 | Current source/validation success |
| Edit 3 | 16 | `7336e7c7-8d14-4313-b571-9f048f6a6952` | 29056 | Current source/validation success |

Three constant edits to one `Assets/SplinterSnapshotProbe.cs`. Each `unity-editN-change.json`, `unity-editN-validated.json`, `unity-editN-probe.json` links raw source hash/fingerprint/generation/MVID/PID/actual path. Normal product stops before/after compaction verified owned processes/memory 0.

Initial/resume starts returned `SourceChanged` but owned Editors were running. Observed status/logs without duplicate starts; verified current source after ready. First third-refresh `EditorFocusRequired` accepted/validated on same product-refresh retry without further code changes. This is **execution with operational intervention**, including responses/development measurement unmount/ensure, not fully unassisted.

Saved Unity compaction budget 5,368,709,120B. After privilege Observe, omitted-argument retry succeeded/cleared pending budget. Journal retained sequence 333,222, reduced 2,279,539,161B→1,969,637,634B. Read-only analysis hashes match product compaction seals. Live payload 1,838,626,847B retained; replaced 284,667,979B→0B. Remaining reduction frame/metadata. Current `Library` paths account for 1,829,045,448B (~99.5%) of pre-compaction live payload.

Direct compact-retry CLI 97.086s. Receipt core compaction 55.554s, separate provider reconnect 14.240s/previous-generation cleanup 8.868s. Raw receipt records source replay 17.096s/candidate write 5.338s/candidate replay 11.732s/etc. Additional measuring unmount/ensure/separate Python analysis (37.083s immediately after compaction)/administrator observation/wait not added to direct compact time. Flush policy unchanged.

Initial start→ready sample 1,366.904s; resume start→ready 76.284s, including observation delays from 30-second status polling. First-preparation flush 320,947/cumulative 1,029.603s and resumed-provider epoch flush 32,610/68.938s cover different intervals, not added to wall time. Initial payload duplication/read cache remain 0B. See `unity-timing-summary.json`. First Editor log overwritten on next launch, so preserved CLI times used for preparation; resumed log in `unity-resume-editor.log`.

Additional ensure after final-ledger measurement hit `OverlayStartUncertain` because journal replay 22.564s exceeded startup wait (150×100ms, observation call times separate). Subsequent status `SourceObservationUncertain` also recorded. Verified exact existing provider mounted; retried ensure same ID/home, recovered without new provider/snapshot. Current validation retained. See `unity-reconnect-recovery.json`/CLI responses; reconnect-wait limitation not claimed resolved.

<a id="저장량-분해와-파일-할당"></a>
## Storage decomposition and file allocation

Below are **logical bytes inside the journal**, excluding original-snapshot reads. Path groups use current paths; cumulative groups for paths before rename separately in raw `groups`. Direct `Packages` overlay payload 0B. `Library/PackageCache` counted under `Library`.

| Stopped boundary | Whole journal | Live payload | Replaced/deleted payload | Frame/metadata | Unclassified |
| --- | ---: | ---: | ---: | ---: | ---: |
| Before compaction | 2,279,539,161 | 1,838,626,847 | 284,667,979 | 156,244,335 | 0 |
| Immediately after compaction | 1,969,637,634 | 1,838,626,847 | 0 | 131,010,787 | 0 |
| Final collection after resumption/edits 2/3 | 2,005,159,072 | 1,838,813,568 | 21,381,329 | 144,964,175 | 0 |

Final live payload: `Assets` 14,764B, `Packages` 0B, `ProjectSettings` 957B, `Library` 1,829,225,426B, other 9,572,421B. Post-compaction journal regrowth 35,521,438B = live +186,721B/replaced +21,381,329B/frame+metadata +13,953,388B. Logical values not converted to per-path physical allocation. All replaced payload not presented as guaranteed next-compaction savings.

File allocation below at same sample time is `control-and-delta + runners + (Git common − pre-creation Git common 6,081,264B)`. Journal/candidate/previous are control-total subsets, not readded. No original Library; Library within mount view not counted as separate file copies.

| Stage | Journal file allocation (B) | Owned additional files (B) |
| --- | ---: | ---: |
| First Editor ready | 2,264,924,160 | 2,266,916,024 |
| After stop immediately before compaction | 2,279,604,224 | 2,281,510,960 |
| Observed compaction maximum | Current 1,969,639,424 + previous 2,279,542,784 | **4,251,098,528** |
| After compaction | 1,969,639,424 | 1,971,555,168 |
| Same-snapshot Editor resumption | 1,976,172,544 | 1,977,039,032 |
| After edit 2 validation | 1,989,410,816 | 1,990,286,040 |
| After edit 3 validation | 1,999,437,824 | 2,000,325,336 |
| After final stop | 2,005,204,992 | 2,006,096,736 |
| After independent-result collection | 2,005,159,936 | 2,006,064,896 |
| Preserved records/results after release | 0 | 889,400 |

251 periodic samples (10-second wait after each measurement)/12 named boundaries, 263 file ledgers total, all complete. Maximum is simultaneous sum in `space-unity-0165.json` (13:15:27.500 UTC), not absolute peak between samples. NTFS allocation is point-specific; reserved-allocation differences across stop/unmount not payload savings. Owned additional-file growth from immediately after compaction through final collection 34,509,728B. `unity-space-summary.json` lists all raw sample paths.

Receipt allocation of actual overlapping journal files 4,249,182,208B; summed logical lengths 4,249,176,795B. 5GiB budget checks only latter, not bound including simultaneous control/runner/Git/volume-shared VSS. VSS separately in `response-vss-*.json`: after Unity preparation Used 27,082,752B/Allocated 100,663,296B; after compaction 27,148,288B/100,663,296B; after last owned-snapshot reclaim 0B/0B. Request name `vss-overlap` not used as instantaneous VSS overlap sample because final response occurred after previous-generation reclaim.

Separate measurement evidence at release, 531 files/allocation 2,477,928B, displayed once as measurement overhead/excluded from product sum. Does not fix subsequent document/final-summary costs. NTFS directories/MFT/shared Unity global-cache changes/OS cache/between-sample peak unmeasured. No total physical-storage amount/savings/upper bound/large-project suitability declared.

<a id="결과-보존회수와-다음-선택"></a>
## Result preservation, reclaim, and next selection

Final Unity checkpoint preserved `0a5876455cda92ab569f6c3585047715ae9fa4a5` at `refs/splinter/snapshot-results/4ba126abe0feb6b7ea7c76b9`. Analyzed final sequence 365,826/hash `1718f184735bf1ab4a647a9ac3fc8a41514ad50bdb07360dac1f3e27709f8834` match checkpoint seal. Release added 8 metadata records for owned-bridge removal, resealed at sequence 365,834. Seals not presented as identical.

After snapshot deletion read Git-result probe source 1,533B(SHA256 `d301792f35c8fbfbae355dc729b0a1575bddfeb405513b1d3958dc19ac43f3d6`); verified `value + 13`/final validation fingerprint. Git-normalized result blob/raw original-file hash checks separate. Original HEAD/index/all tracked hashes/clean state retained; mount/Git registration/delta/provider/Editor/children/handles absent. See preservation checks `unity-closeout.json`/`source-closeout.json`.

Final owned V: snapshots 0; existing C: snapshot `{78AD7813-DF73-4845-8513-F14CC6E87331}` retained. Not expanded to general same-volume non-owned-backup coexistence. Normally stopped storage sampler/fixed-owned-ID administrator session; `host-closeout.json` verifies VHD `Attached=false`, no `V:\`, corresponding processes exited. Existing VHD/fixtures/Git independent results/records preserved.

**Next candidate is one minimal SF-03 immutable-candidate/fixed-validation-view/single-Library A/B/A unit.** Final live Library payload 1.829GB greatly exceeds replaced 21.4MB; removing all replaced records still leaves live 1.839GB. Regrowth itself is dominated by replaced/metadata bytes, but larger fixed capacity cost is each task's required Library. One task here does not yet prove actual single-Library savings across candidates.

Follow-up defines A/B/A immutable source candidates/fixed validation path/1 validation Editor/1 Library on existing small fixture; verifies transition/reversion code/assets/`.meta`/cache consistency, original/every result preservation/additional allocation. Includes source editing without worker Editors; original Editor sharing/Coordination product connection/default-mode adoption not automatically decided. Implementation/execution NotRun at this record's candidate-selection stage. Subsequent product small-Library implementation/execution qualification distinguished as [WF/LB verification](product-validation-unelevated.md). Scope not expanded to flush policy/online compaction/large PV resumption/full NR support.
