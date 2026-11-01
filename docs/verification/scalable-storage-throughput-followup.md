# Scalable-storage throughput follow-up

[한국어](../ko/verification/scalable-storage-throughput-followup.md) · [Original diagnosis](scalable-storage-throughput-diagnosis.md)

Status: ST-01–23 implemented; small correctness checks and the bounded native timing attempt completed. The user's follow-up authorizes all review fixes, tiny per-item checks and a bounded final performance attempt. The mandatory full test gate was cancelled and is **not passed**. Retained 100k fixtures are not resumed; SCAL-03/07/08 remain unqualified.

The subsequent request to fix the diagnosed structural causes is covered by **ST-24–28 below**, with small local validation only. It does not reopen the earlier timing matrix or full gate.

## Changes

| ID | Change and retained boundary |
| --- | --- |
| ST-01 | Adoption shares a durable batch of at most 64 frames; frozen/canonical comparison, live-range matching and final root/seal remain. |
| ST-02 | Packing batches 63 relocation frames with their matching progress frame; the same intent recovers partial work. |
| ST-03 | Fixed captures reuse unchanged basis hashes or verified content summaries; changed bytes, backing proof and whole boundaries remain checked. |
| ST-04 | Spool cursors buffer 64KiB reads. |
| ST-05 | Spools keep at most 64KiB before spilling; durable flush forces a file; each cursor fixes its own length. |
| ST-06 | None original-plan namespace observation includes apply-resource checks, removing a duplicate scan while retaining root/VCS admission. |
| ST-07 | One-use editorless proofs compare sorted canonical metadata digests with complete enumeration and generation checks. |
| ST-08 | Git attribute batches use both 4,096-path and 256KiB UTF-8 bounds. Interpretation verification remains. |
| ST-09 | Unavailable optional initial-summary evidence stops later calls within the inspection; a valid empty reply does not. |
| ST-10 | Concurrent resize/metadata callbacks share bounded durable batches; success follows flush, uncertain writers fail closed, and capture/flush drains both queues. |
| ST-11 | Raw preservation groups native backings to avoid repeated full proofs when the eight-handle cache cycles. |
| ST-12 | Adoption reuses canonical readers within the exact operation and releases them on exit; no cross-operation proof reuse. |
| ST-13 | Existing payload indices replace repeated full-file payload sorting; full catalog coverage and index membership remain checked. |
| ST-14 | Original-plan before/after records use a sorted merge and bulk direct selection. |
| ST-15 | Git retains an unchanged catalog only after all records, omissions, membership, index and interpretation agree. |
| ST-16 | Adoption checks cancellation between proof chunks and durable batches, retaining fixed requests and durable prefixes. |
| ST-17 | Authenticated Stop cancels maintenance/adoption and drains it before shutdown; foreign tokens cannot cancel. |
| ST-18 | Git path collections retain one sorted spool for repeated and partial cursors, removing repeated sorts and partial-sort residue. |
| ST-19 | At most 64 raw objects/references share publication; source native fences cover the batch and backing pin intents precede links/result roots. |
| ST-20 | Payload hashing uses bounded 1MiB reads and checks cancellation before/after reads; tokens propagate into composed readers. |
| ST-21 | Application closure/companions bulk-build once, coalescing identical entries and rejecting conflicting spellings. |
| ST-22 | Canonical readers acquire native fences under the shared catalog lock and verify bytes outside it; write/delete exclusion and full ownership/content proof remain. |
| ST-23 | Bounded hash reads amortize Git availability/path calls without removing checks or full read boundaries; EOF avoids an unnecessary tree read. |

The task/basis/overlay and wire versions do not change. New helpers are `PayloadHash`, `FileRawRangePreparation`, `WinFspMetadataBatch` and the Core `IWorkspaceMetadataBatch` boundary. No OS-specific API enters Core. A 1MiB hashing buffer per bounded worker and up to 64 source handles per raw batch are working-set bounds, not input quotas or total-process memory claims.

## Evidence and stop log

The baseline is `artifacts/scalable-storage-throughput-20261101/before-source/`, with 616 source/config hashes in `before-source.json`; Unity sources and NuGet configuration were copied separately to make that source buildable. Existing user changes, results, intents and stopped fixtures remain intact.

Focused native checks: initial 21 passed; subsequent nine passed; storage/ownership/stop/sort **32 passed in 5.532s**; metadata/adoption/raw-basis/content **48 passed in 16.494s**; repeated original publication, lost-response recovery, exact Git reader fences and paged inspection **17 passed in 55.445s**. The previously slow 2MiB Git-generation case passed in **18.556s** after ST-20/23. The final telemetry repair passed **11 tests with one opt-in cost skip in 1.166s**. These sets overlap; their sum is not a unique full-suite count. Earlier interrupted attempts and the historical full-suite 464s observation are not matched speedup measurements.

Retained execution faults: initial native tests lacked sandbox capabilities; one filter combination selected zero tests; two test assumptions were corrected to use the actual atomic control writer and a native read-sharing assertion. A simultaneous build/test hit locked DLLs; later builds and tests are serialized. The overlong Git test was interrupted, and exact process absence was confirmed before rebuilding. No product timeout increased and no integrity check was omitted to obtain a pass.

The first native timing pilot used fresh **128-entry Git** input. A preparation took **27.335s**; B reached the fixed **30s** command deadline and returned `Cancelled`. Both exact providers received one normal Stop and reported process/mount absence. `C:/Users/olive/AppData/Local/Temp/sctp-g-b1` is retained, with no later stage run. Subsequent paired inputs are reduced equally to **32 entries**, with the same command limit.

## Native timing results

Fresh original/A/B fixtures use 31 files and one directory, 32 bytes per file (992 logical bytes); the fixed basis matches within each pair. Both candidates use the same cancellation wrapper. These are one before/after observation per comparison, not ABBA, cold-cache or large-input evidence.

| Scope | Before | After | Observed saving |
| --- | ---: | ---: | ---: |
| Git common start/bulk/selected stages; final stage not run | 179.249s | 168.960s | 10.289s / 5.740% |
| None complete lifecycle, final product source | 108.520s | 87.694s | 20.825s / 19.190% |

None includes repeated capture, full/selected original publication, rename/delete/create, preservation of an original-only edit, peer continuation after A release, both releases and managed-original restoration to ordinary NTFS. An earlier after-candidate completed in **104.964s**, only **3.555s / 3.276%** below the same baseline. The later change restores optional telemetry; these observations do not establish that it caused the extra saving. Execution-order/cache/environment variation remains unresolved. Git timing predates the final failure-path poisoning guard and telemetry repair, so it is neither final-binary nor whole-lifecycle qualification. The 32-entry bulk harness uses one writer; native eight-worker throughput is unmeasured.

The Git trial driver originally checked its **140s** budget only before stages. Active stages drained to 179.249s/168.960s before the driver declined finalization and normally stopped exact providers. This budget overshoot is retained as a harness defect. Later None runs propagate the absolute deadline into every CLI call. One initial None baseline attempt failed in the observation reader after a successful A release; its remaining B/managed-original providers were normally stopped once, and its data/intents remain. The observation reader now shares atomic replacement safely with bounded I/O retries. This failed run is excluded from completed comparisons.

## Final gate and remaining limits

`pwsh ./scripts/build.ps1` completed locked restore and the full Release build with zero warnings/errors. Its test phase was cancelled: the last progress snapshot was **1,911 passed / 1 failed / 27 skipped**, not a completed result. The intended **180s** limit was missed while agent context was being compacted; the log spans **423.599s** (10:13:20.747–10:20:24.347 UTC). Ctrl+C was sent immediately when execution resumed, the command exited, and read-only process inspection confirmed all related build/test processes absent. No force kill or source/result/intent deletion occurred. The missed deadline is a verification-orchestration failure, not an acceptable runtime estimate or a full gate pass.

The one observed failure was a regression in optional mutation telemetry: the new metadata queue omitted `set-basic-info`/`set-file-size` counts. The queue now records each callback's wait and shared durable batch interval, including failures, without changing its durable completion boundary. The full solution rebuilt in **24.254s** with zero warnings/errors; the focused **11-pass/1-skip** rerun above confirms the repair. The full suite was not restarted. The final None timing uses this rebuilt candidate.

The prior full-suite artifact took **34m26s**, including a **19m13s** restore case and million-entry cases. Those abnormal costs are not normal development estimates. Final validation retains a short explicit stop boundary; an interrupted gate is never a full pass.

The [machine-readable report](../../reports/benchmarks/scalable-storage-throughput-followup-20261101.json) retains all trials, failed attempts, command metrics, stop receipts, focused results and gate status. Baseline verification matched **616/616** source/config hashes; the final source manifest contains **626** files. No new dependency, format or CLI schema was introduced. SCAL-03/07/08, 17GiB/100k, native Plastic/Unity, OS-crash and whole-suite qualification remain open; no large overall speedup is established.

`scripts/verification/ScalabilityTimedCommand.cs` invokes each selected CLI's existing cancellation-aware entry point. `scalable-storage-throughput-trial.ps1` uses the existing native editorless lifecycle harness and normally stops only its owned providers on failure, retaining all requests/data. It never force-kills a writing process. Native Plastic, Unity, 100k and 17GiB performance qualification remains separate.

## ST-24–28: structural follow-up, 2026-11-01

The diagnosis exchange with chat `01a188aa-d3aa-7221-b3cf-acb7796a7579` established that the retained None input had 99,990 files, only 3,199,680 payload bytes and 16 content variants. Its historical 49–66 minute write/capture/plan intervals were not bulk-copy benchmarks. The pre-FIX-01 cache eviction/collection incident is already historical; this follow-up addresses remaining repeated work in the current source.

| ID | Implemented change and boundary |
| --- | --- |
| ST-24 | Whole-object reference batches share one catalog admission. `ExistingReferenceScope` retains at most 64 canonical/alias fence pairs under the fixed capture's workspace operation, and reuses only successful ownership/content proofs. The next operation proves them again. `FileRawRangePreparation` uses this scope; adoption retains up to 64 content readers only during its existing operation, then releases them. Missing/foreign/damaged references, mismatched lengths and pending recovery retain their checks. |
| ST-25 | Ready raw-range batches share one catalog reader and one retained native range proof per distinct hash, up to 64. Source proofs still run, exact membership/request checks remain, and retries do not rewrite ready catalog state. |
| ST-26 | New Runtime `PagedMetadataBatch.cs` updates existing keys along the union of search paths, writing each changed ancestor once. Journal batches coalesce repeated node updates before durable completion; getters see pending values and failure poisons the writer. Explicit root export flushes deferred pages before exposing them. The batch is at most 64 entries with 128 levels of in-memory descent; insertions/deeper paths use the existing disk-spine implementation. Persisted roots, page format, old readers and replay remain unchanged. |
| ST-27 | `PagedPathInterner.Import` merges sorted components against the existing tree, checks collisions and skips rebuilding when no names were added. Repeated original-plan/selection imports no longer perform the previous per-node `Build(previousRoot)` search. |
| ST-28 | Git's prior file catalog is externally sorted once and merged with the current ordered file list. Per-file catalog lookups and a separate omission-count walk are removed. Catalog membership validation and cached-record validation share one traversal. Complete index/membership/interpretation checks, before/after source generations, native path checks and publication proofs remain. |

Small cost checks: **256 nodes / 64 updates: 573 → 164 page writes** (71.4% fewer). The product journal's identical **32 files / 64 mutations: 223,302 → 138,332 additional metadata bytes** (38.1% fewer). These are operation/byte counts, not elapsed-time improvements. A fixed capture scope handled **576 references to 16 contents with 16 native content proof admissions**; a new scope/operation revalidated them. A 64-row/16-range ready raw batch admitted 16 retained proofs. Repeated 64-path imports kept one initial build across three identical imports, and a new path caused another build.

Final focused checks: **41 passed, 0 failed, 0 skipped in 7.453s**; four selected Git incremental/original-publication/adoption-cancellation checks: **4 passed in 36.427s**. The Git input contains 520 small files plus a 1MiB file; other new tests use 4–256 metadata rows, 16 content variants, or 32 journal files. Failure coverage includes torn page tails, poisoned unacknowledged batches, same-request recovery, damaged/foreign native ownership, old roots, native handle release and later-operation revalidation. These 45 tests are not the repository-wide gate.

Execution budget: 40 minutes total (read 5, implement 25, local validation/documentation 10); twice-estimate stop boundaries and individual checks bounded to 30–60 seconds. The initial build failed after 44.12s solely because a new timeout test omitted its cancellation-token check; it was repaired and the focused build passed in 9.48s. Subsequent solution builds passed, including 28.89s with zero warnings/errors before final checks. No long-running step needed cancellation in this follow-up. Historical full-suite/large-input failures remain recorded above; `build.ps1`'s full-suite stage was deliberately not rerun under the user's small-validation instruction.

The final formatting-only solution rebuild passed in **9.27s**, with zero warnings/errors; no further semantic changes followed the focused tests.

Evidence: [structural report](../../reports/benchmarks/scalable-storage-structural-20261101.json) and `artifacts/scalable-storage-structural-20261101/` preserve selected starting sources, initial working-tree status, final source hashes and CTRF results. Task v4, basis v3, overlay v3, wire contracts and the process-wide 32MiB metadata cache budget remain unchanged. The one-key-per-node format and individual initial admissions still exist; a dense-page format replacement is not claimed. No 100k/17GiB fixture, native Plastic/Unity lifecycle, full gate or large end-to-end timing was run. Consequently, removal of the historical hour-long behavior is **not yet established**.
