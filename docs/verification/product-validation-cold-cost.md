<a id="제품-validation의-새-cache-반복과-비용"></a>
# Product validation repeated new-cache runs and cost

[한국어](../ko/verification/product-validation-cold-cost.md)

WF-06–08 are **scoped-complete-with-limitations**. Completed A/B/A with two different empty caches using identical final product binaries; verified raw response schemas/actual code/assets/PNGs/independent results/owned-resource reclaim. Each generation normally stops render preparation, restarts same source/cache for final validation: 6 Editor starts per A/B/A. Prior two attempts' `ValidationSourceChanged`/`EditorStopTimeout`/diagnostic recovery preserved separately, not added to two final runs. t1 closeout-check correction/t2 pre-first-Editor preparation retry recorded below. **Full SF-02/03 Partial**; large-project savings/default-method adoption not judged.

<a id="입력과-재현-경계"></a>
## Input and reproduction boundaries

- Applied normal-shutdown wait fix to product `3573bba78d8382f42c2e32a60745274e7599f9fa` working tree. Compared before/after execution hashes for 152 final source/contract/script/regression files/21 binaries. Shutdown wait monotonic, maximum 2 minutes; unsaved-state rejection/cancellation/timeout preservation retained. Source-sequence checks/flush policy unchanged.
- Windows x64 10.0.26200/Ryzen 5 5600X 6 cores/12 threads/~64 GiB RAM/.NET SDK 10.0.112/Unity x64 `6000.3.22f1`. Small fixture basis `58f81b5`, 90 tracked files/payload 6,235,231 bytes/no original Library. Three probe/render files inserted as actual `splinter-unity` `6a54974` bytes into workers before publication.
- A code/texture 23/1024; B 33/2048. A-only file absent from B; each generation's `Validation*.txt` not transferred. Later worker A value 24 does not inherit candidate validation success. Same-candidate-label input hashes match across runs; all four worker VSS IDs differ.
- Normal t1 started in **different empty view/cache** after explicitly closing two prior failed views. t1 worker/snapshot/immutable-candidate creation/publication used pre-fix product, preserving inputs for final-product new-cache repetition. t2 final product created separate home/new worker/snapshot/empty cache. After selection, both normal A/B/A runs use same final binaries; same path/cache per trial, restart after normal stop/mount absence. Cold means no Unity Library, not empty OS/user shared caches.
- Reused existing owned NTFS VHD. Original location/filesystem unchanged; initial duplication/read cache 0 per provider observations. Administrator helper limited to VHD/four fixed worker VSS operations. Ordinary CLI/Git/Unity non-administrator.

Raw evidence: [execution directory](../../artifacts/storage-foundation/wf-cold-20260928/), [final candidate manifest](../../artifacts/storage-foundation/wf-cold-20260928/final-candidate-manifest.json), [full cross-validation](../../artifacts/storage-foundation/wf-cold-20260928/verified-run.json), [cost JSON](../../artifacts/storage-foundation/wf-cold-20260928/cost-summary.json). Preserves previous `candidate-manifest.json`, two prior harness versions/final `run-trial.ps1`, diagnostic/reclaim scripts/raw JSON/PNG/TRX. Fixed-ID/ownership/current-file-dependent execution record: not replayed verbatim/old evidence overwritten.

Preserved files identified by [PNG visual check](../../artifacts/storage-foundation/wf-cold-20260928/preview-visual-review.json)/[post-reclaim evidence-file hashes](../../artifacts/storage-foundation/wf-cold-20260928/evidence-manifest.json).

<a id="두-빈-cache-검증-실행"></a>
## Verification runs with two empty caches

| Run | Preparation start→ready A1 / B / A2 (s) | Validation restart→ready A1 / B / A2 (s) | Observed max owned host allocation (GiB) | Corresponding home after reclaim (GiB) | Adopted run / total including failures/diagnosis (s) |
| --- | --- | --- | ---: | ---: | --- |
| t1 | 949.19 / 75.06 / 71.43 | 59.23 / 61.25 / 59.50 | 2.247 | 0.003 | 2025.28 / 5346.05 |
| t2 | 929.20 / 75.91 / 73.65 | 61.43 / 59.55 / 59.48 | 2.264 | 0.002 | 2170.19 / 2170.19 |

Host total: two C: homes/all execution artifacts/independent results/VHD backing-file increase since test start. Second-run total retains first-run results. Normal t1 starts with new-cache preparation; initial ensure/publish/failure/diagnosis costs retained in total/raw command times. PNG/settings preparation exit/restart/final refresh/preservation/reclaim after ready also included in total. Ready alone not final validation completion.

All six generations verified 3 model meshes/3,001 vertices/2,441 triangles/0 missing scene meshes/original scene restoration. 640×480 PNGs checked request-path hash/file SHA-256/per-pass foreground pixels, selected first valid image. Maximum-two rule retained; no Unity internal first-draw cause established. `inspect-*.json`, `preparation-*.json`, `validated-*.json`, `observe-*-1.json`, generation preservation receipts link actual code/domain/MVID/candidate seal/output seal.

461 checks passed for final two runs, including 169 raw response schemas/146 actual input/subsequent payloads. Overall including prior failures/diagnosis: 245 responses/217 inputs/subsequent payloads/677 total. Original runtime outputs checked; actual Accepted follow-up status called in final runs. Previous WF formatter reserialization not counted as new execution.

- t1: 156 CLI responses/13 Accepted follow-up statuses. Failed-response counts: SnapshotPrivilegeRequired 9, EditorStopTimeout 1, ValidationSourceChanged 1. `SnapshotPrivilegeRequired` is non-administrator CLI handoff to fixed helper privilege boundary.
- t2: 89 CLI responses/9 Accepted follow-up statuses. Failed-response counts: SnapshotPrivilegeRequired 8, InputUnavailable 1. `SnapshotPrivilegeRequired` is non-administrator CLI handoff to fixed helper privilege boundary.

<a id="추가-파일-할당과-측정-한계"></a>
## Additional allocation and measurement limits

| Point | Corresponding home (GiB) | Cache journal therein (GiB) | All evidence/independent results (GiB) | VHD backing increase (GiB) | Internal Git increase (KiB, separate layer) |
| --- | ---: | ---: | ---: | ---: | ---: |
| t1 a1-stopped | 2.131 | 2.119 | 0.039 | 0.000 | 34.61 |
| t1 b-stopped | 2.167 | 2.145 | 0.040 | 0.000 | 39.31 |
| t1 a2-stopped | 2.202 | 2.171 | 0.042 | 0.000 | 44.02 |
| t1 results-preserved | 0.003 | 0.000 | 0.053 | 0.000 | 60.06 |
| t1 released | 0.003 | 0.000 | 0.054 | 0.000 | 34.84 |
| t2 a1-stopped | 2.131 | 2.120 | 0.056 | 0.000 | 25.20 |
| t2 b-stopped | 2.166 | 2.146 | 0.057 | 0.000 | 29.91 |
| t2 a2-stopped | 2.202 | 2.173 | 0.058 | 0.000 | 34.61 |
| t2 results-preserved | 0.002 | 0.000 | 0.070 | 0.000 | 41.09 |
| t2 released | 0.002 | 0.000 | 0.070 | 0.000 | 15.91 |

File allocation observed via Windows `FILE_STANDARD_INFO.AllocationSize`, cadence 5-second wait+measurement time. Worker/candidate/generation source/cache journals/receipts/logs/TEMP/TMP/independent bundles/bare Git/measurement artifacts read from nonoverlapping roots. Actual mounts not traversed, avoiding read hydration. Handles permit delete sharing. Incomplete samples separately recorded in cost JSON; maxima use complete samples. t1's one incomplete sample excluded because heartbeat publication temporary file vanished before open.

- t1: 1046 samples/1045 complete; maximum all-sample gap 5.80s/complete gap 10.30s. File-measurement median 99.81 ms/cumulative wall 112.41s. Editor-body working-set observed maximum 3.071 GiB/product-owned-app-group maximum 4.304 GiB. V: shared VSS observed maximum used/allocated 2,703,360 / 34,209,792 bytes.
- t2: 415/415 complete; maximum all-sample gap 6.07s/complete-sample gap 6.07s. File-measurement median 227.26 ms/cumulative wall 96.78s. Editor-body working-set observed maximum 2.945 GiB/product-owned-app-group maximum 4.232 GiB. V: shared VSS observed maximum used/allocated 2,473,984 / 34,242,560 bytes.

These are **observed file-allocation maxima**, not physical maxima at every instant. MFT/directory metadata/kernel/OS caches/preexisting shared Unity user caches not separately measured. Existing VHD backing baseline 5,104,467,968 bytes not readded as new usage. V: internal Git/VSS not double-counted in backing growth. VSS volume shared, `workspaceAttribution=unknown`; C: non-owned snapshots/shared VSS also in raw records. Small input/two samples do not establish general performance distributions/large savings/C/S/Q superiority.

<a id="첫-시도의-거부와-운영-개입"></a>
## First-attempt rejection and operational intervention

Initial cold import/code/assets/PNG/normal stop proceeded, but after validation-request source sequence 2361, `ProjectSettings/ShaderGraphSettings.asset` save produced pre-stop 2397/post-stop 2405; `observe-validation` returned `ValidationSourceChanged`. An **ordinary settings-file write**, distinct from temporary directory-monitor file, so checks not relaxed. Original/post-diagnostic settings SHA-256 identical, but current contract checks writes after request by sequence.

Preserved first failure/raw control files; explicitly start/refresh/stop same generation. Same pending observe retry succeeded, then independent results preserved/source/cache reclaimed. In second empty-cache attempt, render-before-reload still left ShaderGraph `OnDisable → Save` sequence 2401→2413. Editor also exited normally code 0 but took ~54.05s from `exiting`, yielding `EditorStopTimeout` under old 30s wait. Verified natural exit/retried same stop; independently preserved result/reclaimed source/cache. Second result claims no validation success.

Changed shutdown wait to monotonic maximum 2 minutes/added four automatic regressions. Unsaved rejection/cancellation immediate; timeout not forced termination/reclaim. Final two runs normally close/unmount preparation Editor, restart same generation for final refresh. Render preparation loading ShaderGraph settings not repeated after validation. Previous two failures not added to final-candidate passes; total t1 includes failures/diagnosis/fix-verification wait/new-cache repetition. Extra cost over A/B/A with only 3 Editor starts remains default-mode suitability limitation.

After final t1 product A/B/A/preservation/reclaim, closeout script miscounted two reused-worker temporary branches as preexisting protected refs, returned `OriginalChanged`. Original HEAD/index/90 files identical; missing refs exactly owned branches removed by normal worker release. Preserved `trial-failure.json`; `source-audit-closeout.json` compared pre-first-ensure existing refs/exact owned branches. Completed remaining closeout without product/Editor restart/file-structure repair; check time included in t1 total.

In t2, before first Editor launch, `state.json` read immediately after normal `prepare-worker` unmount once failed `InputUnavailable`. Next state same owned provider `unmounted`/I/O failure 0/no mount/journal preserved. Retried exact preparation request, succeeded, then publication/empty-cache A/B/A. No manual product/input/control-file repair; preserved `preparation-retry.json`, raw failure/retry/subsequent script/cost. Transient-read cause unresolved, not error-free preparation evidence.

Pre-product-call PowerShell `File.Replace` backup-argument error/diagnostic wait-script reading heartbeat before creation recorded in `setup-deviations.json`. Outside-sandbox execution distinguished from administrator CLI. Fixed VSS-helper request times/counts/all CLI state/time/schema checks preserved. Timeout/Editor exit not discard authorization.

<a id="독립-결과와-회수"></a>
## Independent results and reclaim

Preserved **14** source A1/B/A2/candidate A/B/worker A/B results from two normal runs plus **2** source results from prior attempts as independent bundles/bare Git. After VHD detach verified object existence/commit hashes/code/assets/generated files for 16 results. Original HEAD/index/90 tracked files/existing refs/initial non-owned C: snapshots retained.

Each trial verified owned app/provider processes/memory/handles/mounts/Git worktree registrations/source/cache/worker journals/V: snapshots 0/absent. Retained metadata/independent results not deleted, included in allocation table. Closure evidence `verified-after-detach.json`, `response-finish.json`, `session-stopped.json`, `sample-detached.json`, `final-environment.json`. VHD detached, not deleted; administrator helper/measurer exited.

Immediately post-detach two homes total 0.006 GiB/evidence+independent results 0.070 GiB/VHD backing increase from start 0.000 GiB. Whole measurement host allocation observed max 2.264 GiB. Aggregation JSON/documents/final hashes written after measurement are small later outputs, not retroactively counted in closing allocation.

Final `pwsh -NoProfile -File scripts/build.ps1`: locked restore/Release/**515 Passed / 0 Failed / 1 Skipped**. Test lacking retired-at-the-time root remains Skipped. Pre-test automatic baseline/final TRX separately preserved.

Product default modes/general lease recovery/generated-source return merge/queues/Coordination/installation/binding/discard/privilege service/large PV/NR reboot/power/storage/ARM A/B in [remaining plan](../plans/storage-priority-and-editor-modes.md). Existing [WF-01–05](product-validation-workflow.md) cold interruption/recovery records not retroactively changed.

<a id="단계별-cli-시간"></a>
## Per-stage CLI timing

CLI entry→Editor process creation for every start below combines mount/replay/CLI/ownership checks/process preparation, not pure replay. Accepted attempts listed; preceding privilege request/helper/retry included separately in command lists/whole wall time.

| Run/generation | Before preparation Editor creation (s) | Before validation Editor creation (s) |
| --- | ---: | ---: |
| t1 a1 | 3.52 | 8.50 |
| t1 b | 8.81 | 8.89 |
| t1 a2 | 9.30 | 9.19 |
| t2 a1 | 3.55 | 8.89 |
| t2 b | 9.34 | 8.97 |
| t2 a2 | 9.29 | 9.20 |

Observed provider-generation internal store preparation/source/cache replay below. Only generations with state samples recorded; distinguished from whole `start` response/Editor ready time. PIDs are mount providers, not Editors. Repeated same-generation samples not summed twice. Missing generation replay details not estimated; all start times preserved below.

| Run/observation label / provider PID | Store preparation (s) | Source replay (s) | Cache replay (s) |
| --- | ---: | ---: | ---: |
| t1 a1-preparation-started / 59344 | 0.05 | 0.00 | 0.00 |
| t1 b-preparation-started / 71616 | 5.34 | 0.00 | 5.29 |
| t1 a2-started / 35524 | 5.98 | 0.01 | 5.92 |
| t2 a1-preparation-started / 61364 | 0.05 | 0.00 | 0.00 |
| t2 a1-started / 39620 | 5.54 | 0.01 | 5.47 |
| t2 b-preparation-started / 54704 | 5.78 | 0.00 | 5.73 |
| t2 b-started / 73520 | 5.71 | 0.01 | 5.65 |
| t2 a2-preparation-started / 61840 | 5.79 | 0.00 | 5.74 |
| t2 a2-started / 68804 | 6.03 | 0.01 | 5.97 |

Accepted start/refresh times are command responses, distinct from Editor-ready/validation-completion waits. Snapshot/helper/ensure/publish/follow-up status/retries in full command/administrator list in `cost-summary.json`.

| Run/command | Response time (s) |
| --- | ---: |
| t1 select-a1 | 2.11 |
| t1 select-b | 7.09 |
| t1 select-a2 | 8.11 |
| t1 start-a1 | 3.72 |
| t1 start-b | 8.99 |
| t1 start-a2 | 9.51 |
| t1 preparation-stop-a1 | 32.54 |
| t1 preparation-stop-b | 30.48 |
| t1 preparation-stop-a2 | 60.45 |
| t1 validation-start-a1 | 8.70 |
| t1 validation-start-b | 9.09 |
| t1 validation-start-a2 | 9.39 |
| t1 refresh-a1 | 3.39 |
| t1 refresh-b | 2.44 |
| t1 refresh-a2 | 3.13 |
| t1 stop-a1 | 30.29 |
| t1 stop-b | 31.89 |
| t1 stop-a2 | 31.88 |
| t1 preserve-source-a1 | 6.12 |
| t1 preserve-source-b | 6.13 |
| t1 preserve-source-a2 | 6.24 |
| t1 close-view | 13.31 |
| t1 release-worker-a | 2.34 |
| t1 release-worker-b | 2.31 |
| t2 select-a1 | 2.08 |
| t2 select-b | 7.92 |
| t2 select-a2 | 8.10 |
| t2 start-a1 | 3.74 |
| t2 start-b | 9.53 |
| t2 start-a2 | 9.51 |
| t2 preparation-stop-a1 | 33.18 |
| t2 preparation-stop-b | 31.67 |
| t2 preparation-stop-a2 | 30.79 |
| t2 validation-start-a1 | 9.10 |
| t2 validation-start-b | 9.16 |
| t2 validation-start-a2 | 9.39 |
| t2 refresh-a1 | 3.34 |
| t2 refresh-b | 3.05 |
| t2 refresh-a2 | 3.52 |
| t2 stop-a1 | 54.16 |
| t2 stop-b | 39.86 |
| t2 stop-a2 | 30.36 |
| t2 preserve-source-a1 | 6.49 |
| t2 preserve-source-b | 6.17 |
| t2 preserve-source-a2 | 6.34 |
| t2 close-view | 13.64 |
| t2 release-worker-a | 2.32 |
| t2 release-worker-b | 1.55 |
