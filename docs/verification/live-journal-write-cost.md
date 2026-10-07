<a id="live-journal-쓰기-비용-검증"></a>
# Live journal-write cost verification

[한국어](../ko/verification/live-journal-write-cost.md)

`jw-20260927` closes [JW-01–04 scope](../write-profiling.md) as **scoped-complete-with-limitations**. Automated/fixed-synthetic 18 runs, new small Unity first import/follow-up edit/closed-journal reconnect/independent result/original preservation/reclamation/result verification after VHD detach completed. Change is instrumentation, retaining individual acknowledged-write durable flushes/journal format/range-index behavior. No follow-up implementation adding concurrent group flush only in Runtime selected.

<a id="계측-계약과-자동-시험"></a>
## Instrumentation contract and automated tests

`liveJournalWrites` counts commit entry/durable commits/failures, payload-journal bytes, prepare/append/flush/index application/full-call time for up to 6 operations `create/write/resize/metadata/rename/delete`. Records nonnested mutation-gate calls/waiting and other waiting-mutation counts at flush start/end. Five histogram bins: 0, 1, 2–3, 4–7, 8+. Observed arrays are copies; historical missing/null unknown. No-ops/pre-commit rejection may count as mutation calls, not commit attempts. Explicit Flush/tail-recovery flush only in total-flush counters.

Standard `pwsh ./scripts/build.ps1`: locked restore/Release 0 warnings/errors, **303 passed, 0 failed, 1 test excluded at that time skipped**. Checks six operations/no-op/prerejection/explicit Flush distinction, returned-array isolation, journal hash/reconnect after budget rejection, both-schema matching/negative histogram rejection. Evidence `evidence/build-instrumentation.log`/`evidence/tests-303-passed.trx`.

<a id="고정-합성-쓰기-18회"></a>
## Eighteen fixed synthetic write runs

Baseline WI-completion commit `091a5fc`; candidate adds live instrumentation. Only Core/Runtime DLLs differ among 9 common WriteProbe files (`evidence/probe-binary-alignment.json`). Common probe DLL SHA-256 `7A2202890048EC8ECDD426592A818C4B1C0042046B58E124B1832CC9AC5B7E55`; DLL/actual Unity CLI-bundle hashes in `evidence/binary-manifest.json`.

| DLL | Baseline SHA-256 | Instrumented SHA-256 |
| --- | --- | --- |
| Core | `1FEE11489294A4EA2892117E6DCEFC4768686D8D99215B1A1D200160381865A5` | `2D13960701DA865FDCFAEFB3125CD701E920B72F3CD83661124DD36EAEA1A9C9` |
| Runtime | `03C9E464FB03E25006A092D55B192EACA846FAA27484C16385DB3519585039B0` | `27369D4DB5B532F7093CAEA47763CF7C6F3DC6FDEDD9CC4345742D25518D76CF` |

Sequential 1,024×4KiB, concurrent 4 workers×256×4KiB, 1,024 metadata-operation profiles ran three times per implementation on fresh journals. Round orders baseline/instrumented, instrumented/baseline, baseline/instrumented. All passed independent expected bytes/hashes/metadata, live output/read-only replay/input-hash preservation around replay. Identical `contentHash` across implementations; differing run IDs/times mean journal hashes only prove individual-input preservation.

| Input | Baseline seconds min / median / max | Instrumented seconds min / median / max | Median cumulative managed allocation B baseline / instrumented | Median peak working set B baseline / instrumented |
| --- | ---: | ---: | ---: | ---: |
| serial-1024 | 2.3823 / 2.3839 / 2.7678 | 2.2635 / 2.4874 / 2.9379 | 1,227,072 / 1,227,472 | 63,868,928 / 64,606,208 |
| parallel-4x256 | 2.1870 / 2.1950 / 2.2684 | 2.1841 / 2.1856 / 2.2135 | 1,295,232 / 1,295,632 | 64,647,168 / 65,703,936 |
| metadata-1024 | 2.1803 / 2.1816 / 2.1937 | 2.1736 / 2.1774 / 2.1860 | 1,106,448 / 1,106,848 | 51,568,640 / 52,678,656 |

Timing starts at loop/thread start after file creation, excluding final explicit Flush/output verification/replay. Working set is process maximum through replay verification, a different interval. Warm OS cache/few samples/overlapping ranges prevent instrumentation speed-improvement/regression conclusions. Cumulative managed allocation is not concurrent memory/disk costs.

Summed three instrumented runs' flush start/end histograms: serial/metadata each `[3075,0,0,0,0]`; parallel both `[140,192,2752,0,0]`; maximum sampled waiters 3. Parallel cumulative mutation-wait median 5.9884 seconds exceeds loop wall-time median 2.1856 seconds. Multiple waiters overlap, cannot sum; waiting calls may later no-op/reject, not immediate batch sizes.

Journal logical lengths match both implementations: serial 4,604,690B, parallel 4,604,225B, metadata 351,460B. All 36 synthetic files logical/allocation **57,369,006/57,416,352B**, within bundle 256MiB limit; each journal under 16MiB. Baseline/instrumented probe allocation 610,648/622,936B; CLI bundles 3,220,144/3,236,528B. `FILE_STANDARD_INFO` observations, not MFT/OS cache/VHD backing space.

<a id="실제-작은-unity"></a>
## Actual small Unity

New `jw27-small-unity` owned snapshot/delta on existing isolated NTFS VHD/small fixture. Starting original clean HEAD `58f81b5bc6ff8a940d8a90872bd3fed858a54cc1`, 90 tracked files/23 existing refs/no original Library. Existing V: snapshots/VSS used-allocated 0/0/0; C: snapshots separately protected. Historical WI 21 refs/journal not reused as new-run baseline.

Development `snapshot-spike` mount/Git/Unity prepared `Calculate(3)=23`, texture import max 1024; subsequent code 24. Not full product-workspace-mode support. Initial payload copying 0B/no full Library copies/hydration. Restricted administrator session only capture/observe/release/detach of existing isolated VHD/one fixed owned snapshot.

<a id="최초-준비후속-수정정상-종료"></a>
### Initial preparation, follow-up edit, and normal shutdown

New-snapshot first Editor start→ready **1,186.16 seconds**. Same PID `59580` first explicit-refresh start→current-fingerprint successful compile **36.72 seconds**. Loaded code 23/1024×1024 texture/3 meshes confirmed. First PNG blank/same Editor second PNG model. Follow-up code-edit start→current-fingerprint successful compile **32.32 seconds**, another assembly MVID confirms code 24/same assets. First PNG after follow-up domain reload also blank. First-render cause unconfirmed; code-24 first PNG not rendering success.

Editor normally exited **29.76 seconds**, no forced shutdown/stop retries. Owned active process/working set/private bytes 0 checked, provider unmounted 1.62 seconds. Closed journal **2,284,196,270B**, **334,185 frames/sequence**, SHA-256 `B8AC380B5964A26215C9EB7D462E58DB01F567E0704C37F37F5FBED9E0B2A0AC`. Initial preparation/follow-up are cumulative inputs, not speed comparison to another implementation.

`live-samples.jsonl`: 282 samples roughly every 5 seconds, measurement-read errors 0. Values below are stage-boundary differences in same provider epoch `4d9d8d8de25f43a1b99603057cc63ea6`. Initial-preparation provider observations 19:41:21.0216–20:01:07.5490(KST); CLI start/heartbeat-ready differ about 0.39/0.02 seconds. Allocation measurement of 6 roots/shared VSS queries/OS background concurrent, so not isolated performance baseline/improvement over historical WI.

| Observed interval | Durable commits | Journal growth B | Prepare seconds | Append seconds | Commit-flush seconds | Index-application seconds | Total-commit seconds |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| First-import observation | 320,482 | 2,263,014,996 | 6.467 | 3.451 | 828.378 | 8.197 | 848.157 |
| First ready→first-validation observation | 1,314 | 6,060,259 | 0.038 | 0.018 | 12.610 | 0.104 | 12.778 |
| First inspection/wait | 33 | 75,049 | 0.001 | 0.0002 | 0.348 | 0.009 | 0.358 |
| Follow-up-edit observation | 1,228 | 6,850,706 | 0.026 | 0.018 | 2.733 | 0.035 | 2.818 |
| Follow-up inspection/shutdown observation | 11,120 | 8,188,208 | 0.111 | 0.010 | 23.850 | 0.040 | 24.055 |

First-import total flushes including explicit Flush: 320,548/828.497 seconds, different from table commit flush. Follow-up provider observations 20:03:04.4872–20:03:56.0952 include delay after actual compile completion 20:03:37.0102. Last row also entire inspection/command-preparation/shutdown, not Editor-quit-only costs. Total commit includes small counter-update/etc. costs outside four stages. Per-call wait times not summed with these.

| First-import operation | Durable commits | Prepare seconds | Append seconds | Flush seconds | Index-application seconds | Total-commit seconds |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| create | 35,301 | 0.613 | 0.016 | 93.277 | 0.099 | 94.185 |
| delete | 557 | 0.018 | 0.0004 | 1.338 | 0.003 | 1.362 |
| metadata | 62,082 | 0.946 | 0.024 | 160.769 | 0.101 | 162.149 |
| rename | 3,966 | 0.114 | 0.002 | 9.071 | 7.197 | 16.406 |
| resize | 33,605 | 0.537 | 0.014 | 86.467 | 0.164 | 87.353 |
| write | 184,971 | 4.240 | 3.394 | 477.456 | 0.633 | 486.703 |

Flush is about 97.7% within first-import commits: **share inside measured commits**, not savable share of overall Editor time. create/metadata/resize flush totals 340.51 seconds, requiring separation of durability costs beyond payload recording. Initial mutation gate 337,410 calls/cumulative wait 2.7798 seconds; full Editor observations cumulative wait 2.8072 seconds. Every actual operation's pre/post-flush waiting histogram only increased zero-waiter bin; internal commit/I/O failures 0.

<a id="닫힌-journal-재연결과-저장량"></a>
### Closed-journal reconnect and storage

Same snapshot/closed-journal reconnect mount **5.77 seconds**, internal replay **4.79 seconds**. New commits 0/334,185 frames, code 24/texture-setting hashes match, normal unmount confirmed. Journal length/hash unchanged before/after reconnect/result collection. No Editor restart here, so not Editor-resumption speed evidence.

Stopped-journal read-only accounting verifies all frame checksums/sequences/hash above. Live payload **1,838,007,144B**, superseded **289,531,653B**, metadata/frames **156,657,473B**, sum matches journal length. Current-path live Library **1,828,459,443B (about 99.5%)**. Logical bytes differ from physical allocation. Not guaranteed all superseded payload reclaimable; no compaction in this execution.

| Stage | Owned delta/control/snapshot file allocation B | Common Git file allocation B |
| --- | ---: | ---: |
| Before snapshot | 20,480 | 6,124,208 |
| Before Editor start | 816,800 | 6,137,000 |
| Sample after first ready | 2,265,472,920 | 6,137,000 |
| Sample after follow-up validation | 2,278,515,184 | 6,137,000 |
| After result preservation/deregistration | **2,286,170,992** | 6,125,848 |
| After owned-resource reclamation | 0 | 6,125,848 |

All stages' 6 roots complete/include initial delta. Sample maximum **about 2.286GB** is not instantaneous peak/MFT-directory metadata/OS cache/global Unity cache/VHD backing-file reduction. First-import Library writes already in delta, not summed separately. Common Git includes all existing results; before/after difference 1,640B not independent physical new-result costs.

Isolated V: shared VSS used/allocated before snapshot **0/0B**, immediately before reclamation **27,164,672/100,663,296B**, after **0/0B**. Volume totals, not added to owned allocation. Independent bundle logical length **5,999,971B**; C: verification-evidence root post-reclamation allocation **81,183,072B**. Includes synthetic inputs/two probe-CLI bundles/independent bundle-bare repository/control evidence, not double-counted. Final check/summary files after last sample excluded.

<a id="독립-결과와-회수"></a>
### Independent results and reclamation

Result revision **`b0f8b865f9d39c69154cc025accd2e789fe69f32`** retained in `codex/jw27/unity`/`refs/splinter/snapshot-results/jw27-unity`. Two changed files `Assets/SplinterSnapshotProbe.cs`/`Assets/SplinterQualification/Target.png.meta`; temporary Unity bridge excluded. Preservation mount 5.78 seconds, result collection 2.32 seconds, unmount 0.66 seconds, deregistration 1.12 seconds, C: bundle creation/separate-repository fetch 0.87 seconds. Observation/accounting/between-command waiting excluded from API times.

C: bundle/bare repository without external alternates confirmed same revision/code 24/texture settings/`git fsck`. Original clean HEAD/90 tracked hashes/23 refs/no original Library rechecked before reclamation/after snapshot removal. Exact PID/start termination of 3 owned providers/1 Editor, open handles/I/O errors 0/no mount-Git registration confirmed. After evidence copying, owned delta/control reclamation 0.18 seconds; owned snapshot/empty receipt paths reclaimed. Existing C: snapshot `{78AD7813-DF73-4845-8513-F14CC6E87331}` retained.

Finally confirmed `Get-DiskImage.Attached=false`/V: absent/restricted administrator PID ended; C: independent-result revision/content/bundle hash/`git fsck` reverified. Original fixture/existing-new refs/existing VHD/historical evidence retained. New bundle SHA-256 `AD22FD06F63913574C9D842E7436DE45B2006BC3AB69A235139D8AC9D43C862D`.

<a id="쓰기-동시성의-경계와-후속-판단"></a>
## Write-concurrency boundary and follow-up assessment

`WinFspOverlayFileSystem` `mutationGate` serializes entire mutating callbacks Create/Overwrite/Cleanup/Write/SetBasicInfo/SetFileSize/Rename. Unity requests already serialized before Runtime differ from parallel synthetic direct-Runtime calls. Current Runtime samples cannot measure adapter/OS waits; 0 Runtime waits do not mean no overall write demand.

Therefore **no follow-up implementation merely grouping Runtime flushes selected**. If separate concurrency design needed, first review atomicity of compound checks for callback append offsets/constrained I/O/resize/follow-up file info, queue-wait placement/request-completion boundaries. Then gates for pre-success durable sequence/failure propagation/reconnect/seal/shutdown/bounded waits. No lock removal/flush-policy change in scope.

<a id="판정과-증거"></a>
## Assessment and evidence

| ID | Assessment | Limits |
| --- | --- | --- |
| JW-01 | Passed | Fixed operation counters/schema/budget rejection |
| JW-02 | Passed | 18 fixed synthetic runs/direct Runtime calls/warm cache/three small samples |
| JW-03 | Passed, bounded observation | First import/current compile-loaded code/follow-up edit/normal stop/same-journal reconnect. First PNG and first post-reload PNG blank, cause unconfirmed |
| JW-04 | Passed, bounded observation | Independent results/original 90 files-23 existing refs-other snapshots preserved/reclaimed/VHD detached. Instantaneous peak/adapter-OS waits unknown; Runtime-only group flush unselected |

Raw root `artifacts/storage-foundation/jw-20260927/`. Synthetic/binary hashes/automated TRX in `evidence/`; Unity stages `live-samples.jsonl`/`state-*.json`/`live-phase-summary.json`, allocation `space-*.json`, shared VSS timestamped `response-*.json`. Overall assessment `qualification-summary.json`; preservation/reclamation `independent-result.json`, `pre-reclamation-audit.json`, `source-and-result-after-release.json`, `closeout.json`, `final-environment-check.json`, independent bundle/repository. Overall SF-02/SF-03 Partial; no expansion to first-render cause/physical Library savings/large PV/default-mode adoption.
