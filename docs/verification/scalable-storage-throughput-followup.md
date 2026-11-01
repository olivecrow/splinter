# Scalable-storage throughput follow-up

[한국어](../ko/verification/scalable-storage-throughput-followup.md) · [Original diagnosis](scalable-storage-throughput-diagnosis.md)

Status: ST-01–23 implemented; small correctness checks and the bounded native timing attempt completed. The user's follow-up authorizes all review fixes, tiny per-item checks and a bounded final performance attempt. The mandatory full test gate was cancelled and is **not passed**. Retained 100k fixtures are not resumed; SCAL-03/07/08 remain unqualified.

The subsequent request to fix the diagnosed structural causes is covered by **ST-24–28 below**, with small local validation only. It does not reopen the earlier timing matrix or full gate.

The later authorization for aggressive optimization and demand-driven memory around 4GB is covered by **ST-29–35**. It supersedes the earlier cache/worker policy, not the integrity requirements or large-test pause.

The request to proceed with the remaining candidates is implemented in **ST-36–40** below, with small local checks only.

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

## ST-29–35: resource and concurrency follow-up

The previous SCAL foundation and ST-01–28 were committed together as `1c748c9`; the throughput code depends on that previously uncommitted foundation. This section records the subsequent implementation. The earlier 32MiB/8-worker figures above describe those earlier candidates.

| ID | Implemented change and retained boundary |
| --- | --- |
| ST-29 | New Runtime `StorageResources` uses a process-local target of one eighth of available memory, clamped to 128MiB–4GiB (unknown availability: 512MiB). Five eighths funds metadata cache accounting, one eighth pooled scratch. The observed host has 12 logical CPUs and 68,628,553,728 available bytes: target 4GiB, cache 2.5GiB, scratch 512MiB, 48 retained reader handles per store. Nothing is reserved ahead of demand. Native cacheability, identity, checksum and reader fences remain. |
| ST-30 | Sort runs grow up to 128MiB, spools up to 64MiB, both sharing accounted scratch; merge fan-in grows 8→32. Denied growth spills instead of waiting. Nested sorts retain a 64KiB fallback run (or one larger legal record) outside pooled scratch so exhaustion does not create one file per tiny record. Durable spool flush still forces actual file storage. |
| ST-31 | The bounded transient treap handles inserts and updates, persisting surviving dirty nodes once. Journal batches now include changed-path indices. Up to 64 rows and 128 descent levels retain bounded memory; deeper cases use the original disk-spine path before any batch write. Old roots, read-your-writes, poisoned failures and durable root/ACK publication remain. |
| ST-32 | New `BoundedParallel` overlaps enumeration, hashing and consumption with a bounded completion queue. Slow files no longer hold a whole batch at a barrier. Hash workers follow CPU count (1–32); independent I/O uses twice CPU count (8–32): 12/24 on this host. Plastic's fixed revision reader remains serial. Errors and early disposal cancel and join admitted workers, preserving the original failure. |
| ST-33 | The largest-single-file staging reservation had serialized equal-size files despite multiple workers. Parallel staging now uses worker count, one quarter of the working target and one quarter of observed disk headroom, while always allowing one largest payload for existing recovery. Full-target native locks, per-file space checks, durable progress serialization, topology/companion order and failure drain remain. |
| ST-34 | Directory enumeration retains the immutable metadata it already receives instead of calling `Stat` again for every child. Exact path, availability, overlay precedence, excluded/hidden paths and old-root semantics remain checked. |
| ST-35 | Ordinary-file changed-path inspection replaces concatenation, two tree lookups per row and individual COW insertion with a sorted catalog merge and bulk changed-set construction. Case spelling, addition/deletion/metadata changes and cancellation remain covered. |

The memory numbers are policy budgets, **not a 4GiB RSS or multi-provider aggregate guarantee**. Merge buffers, fallback runs, retained cursors, payload/decoder buffers, native handles and GC overhead require separate headroom. Actual large-project peak memory and hardware utilization are unmeasured. Task v4, basis v3, overlay v3, CLI schemas and capabilities are unchanged; no new public command or dependency is introduced.

Small cost evidence: 64 inserted keys require **342→64 page writes**; the same product **32 files / 64 mutations use 223,302→57,988 additional metadata bytes** versus serial execution (74.0% fewer). The previous ST-26 batch recorded 138,332 bytes; the new batch uses 58.1% fewer in that fixture. A 6,000-row/1KiB-value sort with a 4MiB versus 32MiB run allowance produces **2→0 run files before the last row**, with identical output. A 32-file directory listing makes **zero extra `Stat` calls**. The native publication check requires at least two stages to overlap after every target has been pinned. These are work/byte/concurrency checks, not elapsed-time speedup measurements.

Validation: final resource/structural/batch/spool checks **30 passed in 1.283s**; selected Git inspection and publication checks **5 passed in 45.924s**; file inspection proof/fake Plastic checks **14 passed in 8.220s**. Earlier in this same follow-up, unchanged native cache/reader/content/range checks **31 passed in 6.423s**, and publication/adoption checks **4 passed in 7.850s**. Their union is **81 unique tests, zero failures/skips**; overlapping executions are not added as unique tests. The final Release solution build passed in **10.52s, zero warnings/errors**. The Git case uses 520 small files plus a 1MiB file, not the retained 100k fixture.

Execution plan: 55 minutes total (commit 5, review 10, implementation 30, local checks/documentation 10), with twice-estimate stage stops and 30–60s individual check limits; new tests have 10–15s cancellation-aware timeouts. Initial builds exposed a missing scratch-budget owner field (18.11s) and a missing inspection-token forwarding parameter (6.21s); both were repaired before the final checks. Expanded sort memory-return/disk-write paths also check cancellation per record. The existing spill/cursor test now uses an explicit small 64KiB allowance; large-fixture budget assertions follow the policy but those fixtures were not executed. No running writer/provider needed interruption. The full `build.ps1` test gate was deliberately not restarted under the user's small-validation instruction. No large fixture, native Plastic/Unity lifecycle or new provider was started.

Further review found three substantial remaining candidates. `WindowsOriginalFilePublisher.Commit` still serializes each durable progress update; safely grouping those acknowledgements requires explicit crash/retry boundaries, not removing flushes. `JournalPathComponents` and single-key `PagedMetadataStore.Set` still generate per-key paths, while the persisted format stores one key per node; broader batching or a separately qualified denser representation remains open. Original planning and `PagedMetadataCollection` still traverse complete protected inventories; any delta shortcut must prove the complete source generation and native ownership first. The existing live-index repack threshold is `max(64MiB, active count × 2048)`: the 4MiB post-operation maintenance decision is not that repack threshold. Raising workers alone does not resolve these serialized/whole-scan costs, and the historical hour-long behavior is not yet demonstrated to be gone.

Evidence: [resource report](../../reports/benchmarks/scalable-storage-resource-20261101.json), source manifest and CTRF files under `artifacts/scalable-storage-resource-20261101/`. This follow-up does not qualify SCAL-03/07/08, actual 4GiB pressure, whole-system CPU/disk saturation, large elapsed-time improvement or the full gate.

## ST-36–40: remaining serialization and traversal costs

Starting source: `3f7d5b1`, clean `development`. The user authorized proceeding with the remaining candidates while keeping verification small. No retained fixture, provider or Editor was restarted.

| ID | Implemented change and retained boundary |
| --- | --- |
| ST-36 | Windows `OriginalPublicationCommitQueue` groups up to 64 waiting progress entries. Runtime `OriginalApplyPages.SaveSteps` writes their combined COW root and flushes pages once; one control persist then acknowledges the group. Callers remain blocked at their existing durable boundary. There is no timer or background writer. A persistence failure poisons the queue, fails all waiting callers and admits no later progress. Unknown stage preservation and its replacement nonce share the same serialized group. Original/peer locks, per-file phase order, same-intent recovery and worker drain remain. |
| ST-37 | Journal batches now include component-name and hierarchical namespace indices. Pending names are visible to current operations, up to 64 changes coalesce shared ancestor paths, and paths with 128 or more separators use the existing iterative path. Root export flushes pending pages before exposing the root; old cursors remain fixed. WinFsp Create joins the metadata queue, returning collisions per caller; successful handles are retained under the mutation gate only after the group is durable. Failed groups do not retain unacknowledged handles. |
| ST-38 | Runtime `PagedMetadataReachability` uses an exact operation-local visited set instead of rebuilding the cumulative visited file at every tree level when it fits the shared scratch allowance. It charges 512 bytes per visited page and at most the current sort-run budget (128MiB on this host). Denied growth leaves the initial frontier intact and falls back to the existing disk algorithm. Copy relocation also caches exact address→copied-reference mappings at 256 bytes per entry under that budget, retaining its disk map and fallback. Full reachable-page/native/root validation, copy flush, location publication and ownership-checked deletion remain. |
| ST-39 | Original planning applies dense prior publication history with a sorted merge and one bulk basis build instead of per-path COW updates. Fewer than `max(64, basis count / 16)` history entries keep the sparse route. Deletes and case spelling remain exact; path names are imported/validated, and historical roots remain immutable. |
| ST-40 | The original namespace scan is retained. Its retained-parent computation now advances ordered before/after/selection cursors alongside the original list, instead of three point lookups per row and repeated parent insertions. The final parent set is bulk-built with exact duplicate checks. Foreign children, excluded selection and directory-deletion conflict checks remain. |

Small operation evidence (current serial/disk versus batched/memory paths, not binary A/B timing):

- Nine progress boundaries use **two control persists**, with all callers blocked until their persist completes; an injected second-persist failure acknowledges none of that group. Nine native Create callbacks similarly use **two durable journal flushes**, retain no pre-ACK handles, and release all contexts.
- A 128-row shared metadata graph uses **16→0 depth-wise disk merge passes** and **699→0 relocation-map disk reads** with a 1MiB test allowance; 134 validated memory hits replace those reads. Zero/1KiB allowances select the disk fallback and preserve identical logical roots, shared children and empty/duplicate roots.
- A 128-file basis with 97 applied-history entries uses **704→97 metadata page writes**. Five history entries retain **35→35 writes**, avoiding a full rebuild for sparse changes. Names are imported equally before counting these index writes.
- The final 32-file create/nested rename/delete/replay check records **599,773→389,568 metadata bytes**. Earlier small runs recorded 562,848→385,812 and 553,972→390,894. Each run creates fresh node GUIDs, so tree shape/byte counts vary; this is logical-action equivalence, not identical task identity or a general elapsed-time improvement.

Final validation: **64 unique tests passed, zero failures/skips**. The new 12-case suite passed in **2.334s**; 20 collection corruption/interruption/reader checks in **4.045s**; five original-publication guards in **14.452s**; five space-failure/retained-stage recovery checks in **9.171s**; 20 journal/metadata/cleanup/frozen-cursor checks in **2.485s**; two unknown-stage/historical-progress checks in **2.397s**. Inputs use 9 concurrent callbacks, 32 created files, 32–256 collection rows, a 128-file planning basis, or one path at depths 3/129. Existing small Unicode/capture/replay tests remain component checks. Native Plastic server and Unity lifecycle qualification are not inferred from fake/application-policy cases.

The first space-failure run had four failures in an old assertion that limited staging to one 12KiB file, before the test reached recovery. ST-33 already permitted multiple stages. The assertion now checks the current policy allowance, total plan payload and largest-file metric; the retained-stage test permits one or two existing stages and still proves they finish before fresh staging. The full five-case rerun above passed. These failures remain in the raw report; no product guard was relaxed to satisfy them.

Timing plan: 55 minutes total (review 8, implement 33, validation/documentation 14); preparation/capture/reclamation checks about 5s, publication about 10s and build about 30s, with twice-estimate stop boundaries. The initial solution build passed in 53.38s, below its 60s stop; focused builds took 7.86s/8.72s and the final solution build **8.89s**, all with zero warnings/errors. No step required cancellation or forced termination. `build.ps1`'s full test phase and the large matrix were not run.

The one-key-per-node persisted representation and required whole original/inventory observation remain. This change removes repeated index work and depth-wise passes; it does not claim to eliminate every full scan, to saturate CPU/disk, or to resolve the historical hour-long workflows. Extremely large graphs or concurrent scratch demand can still use the slower disk fallback. The process-local 4GiB working target, versions, wire schemas and CLI capabilities remain unchanged.

Evidence: [remaining-candidate report](../../reports/benchmarks/scalable-storage-remaining-20261101.json) and final source hashes/CTRF under `artifacts/scalable-storage-remaining-20261101/`. No large elapsed-time, peak-RSS, OS-crash, native Plastic/Unity or whole-gate qualification is added.
