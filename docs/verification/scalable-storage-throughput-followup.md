# Scalable-storage throughput follow-up

[한국어](../ko/verification/scalable-storage-throughput-followup.md) · [Original diagnosis](scalable-storage-throughput-diagnosis.md)

Status: ST-01–23 implemented; small correctness checks and the bounded native timing attempt completed. The user's follow-up authorizes all review fixes, tiny per-item checks and a bounded final performance attempt. The mandatory full test gate was cancelled and is **not passed**. Retained 100k fixtures are not resumed; SCAL-03/07/08 remain unqualified.

The subsequent request to fix the diagnosed structural causes is covered by **ST-24–28 below**, with small local validation only. It does not reopen the earlier timing matrix or full gate.

The later authorization for aggressive optimization and demand-driven memory around 4GB is covered by **ST-29–35**. It supersedes the earlier cache/worker policy, not the integrity requirements or large-test pause.

The request to proceed with the remaining candidates is implemented in **ST-36–40** below, with small local checks only.

The subsequent request for same-small-input elapsed time and bottleneck repair is covered by **ST-41–43**. The first ABBA compares `3f7d5b1`/`5dce6e0`; the second compares `5dce6e0`/this repair. They are separate comparison blocks.

The five metadata-duplication review comments are owned by **ST-44–48**. Four compact-record changes are implemented; the multi-entry page redesign is explicitly deferred. This follow-up changes the metadata-store codec family to `SPM2` and does not reopen large verification.


After the metadata chat completed, the requested capture/apply investigation and repairs are covered by **ST-49–51** below. Both candidates include SPM2; these timings are not combined with earlier SPM1 comparisons.

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

## ST-41–43: actual small-input timing and fixed absence waits

ST-41 compares `3f7d5b1` (before ST-36–40) with `5dce6e0`; ST-42 fixes remaining waits and compares `5dce6e0` with the new candidate; ST-43 owns deadlines and focused verification. [Timing report](../../reports/benchmarks/scalable-storage-timing-20261101.json) retains exact source/binary hashes, individual commands, inputs and diagnostic measurements. These are **two separate ABBA blocks**, not one cumulative causal percentage.

Each fresh native None input has **31 files × 32 bytes = 992 bytes, one directory**, with identical paths, bytes, attributes and fixed seed mtimes. Each workflow prepares the shared basis and A/B, performs four explicit captures and four original publications (including selection/remainder), releases A while B continues, releases B and restores ordinary NTFS. All nine runs, including one diagnostic intermediate, have matching basis revisions and final paths/lengths/bytes; final mtimes/generated identities are not asserted identical. The harness checks original-only edits, independent retained results and peer continuity. All A/B records ended `released`, bases `restored`, and post-restore owned process inventories empty; no extra cleanup Stop was needed.

Times below are means of two runs per candidate, in seconds. Preparation sums project prepare and A/B ensure; capture sums four explicit checkpoints; apply sums four plans and publications; reclamation sums A/B release and project restore. **Release includes its own final capture.** Rows measure CLI entry-point time. Whole workflow additionally includes all 27 dispatches, process startup, schema/status, edits and harness overhead, excluding seeding; the four rows do not sum to the whole.

| ST-41: previous optimization | Before `3f7d5b1` | After `5dce6e0` | Saved |
| --- | ---: | ---: | ---: |
| Preparation | 15.17 | 14.90 | 1.8% |
| Capture | 17.51 | 17.09 | 2.4% |
| Apply | 18.63 | 17.64 | 5.3% |
| Reclamation | 22.92 | 20.62 | 10.0% |
| Whole workflow | **92.55** | **88.11** | **4.8%** |

Order `a1/b1/b2/a2`; whole ranges 91.86–93.24s and 88.06–88.16s. This adds small native elapsed-time evidence for ST-36–40, not large-input qualification.

| ST-42: this follow-up | Before `5dce6e0` | After fixes | Saved |
| --- | ---: | ---: | ---: |
| Preparation | 14.64 | 9.45 | 35.4% |
| Capture | 16.66 | 17.16 | −3.0% |
| Apply | 17.62 | 18.39 | −4.4% |
| Reclamation | 19.68 | 17.92 | 8.9% |
| Whole workflow | **86.83** | **81.31** | **6.4%** |

Order `b3/d1/d2/b4`; whole ranges **86.30–87.37s** and **78.44–84.17s**. Capture/apply did not improve in this block. Two samples and storage/cache/scheduling variation do not establish statistical significance; the better `d2` is not substituted for the mean. Control means also differ between blocks. Intermediate `c1` (83.274s) is excluded from both comparisons.

The fixed waits were unrelated to copying payload:

- Initial connection observation paid roughly two seconds for absent launch/state publication. `ObserveForConnectionAsync` acquires Start's exclusive `client.lock` before proving absence. A busy lock uses ordinary observation; unresolved intent retains state grace and uncertain ownership; child-published recovery remains. Focused ordinary/locked absence: **2.011477→0.004310s**.
- Initial `VerifyConnectionAsync` waited one second for its own not-yet-published request. Only this read skips absence grace under the coordinator's existing workspace operation lock. General reads, sharing retries and identity checks remain.
- First stopped-journal reclaim waited one second for its not-yet-published `delta-reclaim.json`. The same operation-lock proof removes only absence grace. Complete stopped-provider/seal/native-inventory proof and same-intent recovery remain before deletion.

Optional `SPLINTER_RESULT_GENERATION_DIAGNOSTICS=1` now attributes release phases without changing their order or making logs recovery inputs. In final `d2`, A's final generation still costs **4.957s** and final metadata maintenance **1.198s**. Inside the generation: freeze 0.696s, result scan 0.477s, range preservation 0.620s, two independent checks together 0.299s, pruning 0.795s and backend completion 0.685s. Nested measurements are not additive to parents. Remaining cost spans native capture and metadata/range processing; no additional proof-elision is adopted. Capture/apply and large inventories still need optimization. The historical tens-of-minutes/hours behavior is **not proven resolved**.

The fixed plan was 55 minutes for this work, a 90s expected/180s stop lifecycle, and start/bulk/selected/final stage expectations 20/15/20/35s, stopped at twice each. Command expectations: prepare 3s, ensure 5s, capture 5s, plan 2s, publication 5s, release 8s, restore 3s, schema/status 1s. The helper cancels cooperatively at the earliest command/stage/trial deadline; the driver also rejects late successful responses and starts no later operation. Exact-owner normal provider Stop remains cleanup. The trial's 16–128-entry restriction is a verification-tool limit, not a product quota. An intentional read-only timeout ended after **2.108s**, with no provider/home created and no next command. All nine lifecycle runs stayed within the original limits.

There were **two test-batch execution-policy misses**: a 20s-estimated/40s-stop batch ran **67.997s**, and a 5s-estimated/10s-stop pilot ran **13.074s**. Timely cancellation was missed; both ended normally. These are recorded procedure failures, not enlarged estimates. Adding launch records did not improve the pilot and was reverted. Attribution instead found repeated real Git setup/basis proof/journal preparation (0.802/1.715/2.409s in one case); first reclaim/retry took 0.665/0.478s. The fixture now uses an exact leased Git read scope with full final `VerifyReadBoundary`; independent cases use bounded parallelism and 10s individual timeouts. Two pilot cases took 6.378s; final ten cases passed in **20.407s** against a 20s estimate/40s stop. This fixture/parallelism change is not product speedup evidence.

Validation union: **43 unique focused checks passed, zero failures/skips**: publication grace, busy/unresolved launcher, snapshot/mount observation and ten stopped-journal protection scenarios. Final product solution build **18.92s**, focused test build 10.78s, helper build 2.93s, all zero warnings/errors. Frozen final product binaries match current product output. No large fixture, actual Git/Plastic/Unity lifecycle, OS-failure matrix, peak RSS/hardware saturation or `build.ps1` full test gate was run. Task v4/basis v3/overlay v3 and CLI contracts remain; SCAL-03/07/08 remain incomplete. Raw records are retained in `artifacts/scalable-storage-timing-20261101/` and the report's exact temporary fixture roots.

## ST-44–48: metadata record duplication review

Starting source: clean `development` at `72601d6d4c9fee80c48dc15452b5caf7e345d84f`. The user authorized reviewing five specific comments, reducing unnecessary duplication and considering formats more efficient than JSON, with tiny local verification only. The visible other repository chats were idle at intake; no existing change or retained fixture was overwritten. CLI query JSON is separate from the already-binary durable bulk data.

| ID | Outcome and retained boundary |
| --- | --- |
| ST-44 | **Deferred, not implemented:** multi-entry sorted-tree pages. The present deterministic immutable treap still writes one key per node. Null links are already omitted by flags; they are not three unconditional 61-byte references. A 64-key / 16-byte-value bulk build writes 64 pages and 8,259 page bytes for 1,280 key/value bytes, excluding the 48-byte container header. Each page has a 40-byte checksum header, 9 bytes of lengths/flags and only its non-null 61-byte references. Packed leaves/internal pages could reduce page count/depth, but must jointly redesign deterministic logical identity, sparse COW splits/merges, rank/select, disk spines, graph relocation, collection/stopped proofs and historical cursors. Preserving the current logical treap while physically packing would instead need authenticated subpage addressing/relocation. Neither design is qualified by this small record change; no claim of reduced tree depth or page count is made. |
| ST-45 | Publication changes retain full Before and encode After/Expected against the cheaper complete same-kind Before/After version when smaller than a full version. Field masks preserve length, full SHA-256, attributes, modification ticks and nullable Unix mode independently. Expected native identity and AlreadyMatches remain independent. Equal content never implies equal metadata. Unknown tags/masks, absent references, truncated fields and trailing bytes fail. Inline/internal change tags are 4/5. |
| ST-46 | `FileApplyIndex.Build`/`ApplyVersions` spool the original path only as the key, with binary type/length/ticks/attributes/hash/Unix mode values and a distinct deletion tag. The existing spool envelope checks lengths and SHA-256; the codec checks flags, exact size, path/type/metadata and nullable fields. Dense merge/sparse updates, interning, path spelling and durable index encoding are unchanged. No JSON reparsing remains in these temporary records. |
| ST-47 | Directory record v2 combines flags, omits zero creation/access times and retains every nonzero value, last-write time, attributes, Unix mode, content/child reference, source reference and path policy. Explicit zero optional times are noncanonical and refused. New exact value bytes produce new logical roots; physical placement still does not enter logical identity. |
| ST-48 | Directory-listing scratch uses original-spelling names only as sort keys, with binary fixed metadata and an overlay-only tag. The immutable path-policy comparer applies to in-memory sorts, every spill run and every merge level. A basis listing's exact parent/path is checked before dropping its redundant full path; snapshot availability is checked again before yielding basis metadata. Fixed node/name/hidden roots, overlay precedence, original casing, hidden/excluded paths and metadata reuse remain. |

**Single current format:** metadata identity/container family is now format 2, `SPM2`. Store opens, identity reads, collection/stopped audits, derived read-only replay and Plastic native admission use one shared magic. Previous `SPM1` stores, including the preceding task-v4 candidate, are preserved/refused before append/collection; finish them with their matching binary. Old directory/change tags are refused. No migration, conversion, fallback, or old-store cleanup is provided. Task v4/basis v3/overlay v3, metadata root schema v1, treap page layout and `SPN1` logical hash algorithm stay unchanged. Exact roots/digests bind the new value bytes; native hashes, complete inventory, ownership, reader fences, same-request recovery and durable root/ACK ordering remain mandatory. Control/CLI JSON is unchanged.

Small byte-cost evidence, **not elapsed-time or large-project speedup**:

| Fixed input / measured unit | Before | After |
| --- | ---: | ---: |
| One publication change, Unicode path, changed After content, Expected differing only in attributes; inline binary value | 215 B | 155 B |
| Directory value, creation/access both zero | old value size | 20 B smaller (16 B times + 4 B combined flags) |
| Directory value, both optional times nonzero | old value size | 4 B smaller |
| Comparison value, 90-component Unicode path / file hash / mode | 12,150 B JSON | 57 B binary, path solely in key |
| Same comparison's complete checksummed spool record, including key | 23,624 B | 11,531 B |
| One fixed listing's value, all three times and Unix mode | 159 B JSON | 41 B binary, name solely in key |

The old publication size uses an exact test-only copy of the prior binary encoder; comparison/listing sizes use the prior JSON record shapes. These comparisons are the same fixed records, not independent end-to-end candidate runs. A no-reference full publication version adds one selector byte; sparse deltas are selected only when smaller than that full representation. The one-key node/header cost remains substantial.

Validation: **113 unique focused checks passed**, zero unresolved failures/skips. New codec/cost suite: **13 passed in 1.721s**, including 216 Before/After/Expected combinations, every truncated prefix of representative records, old tags/magic, absent references, optional times/modes and a forced multi-level spill/merge retaining Unicode case spelling. Existing roots/readers: **37 passed in 2.936s**, with three environmental failures subsequently passed in the native group; collection/corruption/same-request interruption: **28 in 10.111s**; native reader/Plastic admission scopes: **21 in 10.070s**; native publication/conflict/retry/history: **14 in 32.257s**, including those three reruns. The union counts IDs once. Actual Plastic client/server, Git/Unity lifecycle, OS-crash matrix and large elapsed-time/RSS are unqualified.

Initial new-suite execution had eight rename-denied errors in the sandbox's default temporary folder. Workspace-owned scratch passed all twelve then-current cases. The roots group then encountered the workspace ancestor's VCS guard once and sandbox hardlink denial twice; native tests were rerun in fresh normal temporary fixtures without changing product guards. An incorrectly combined class/method filter selected zero tests and was corrected; it contributes no evidence. Raw failed and successful CTRF remain preserved. The first parallel build returned failure without diagnostics in 3.91s; serial build exposed/fixed four test-only nullability/cancellation-analyzer errors in 37.03s. Focused builds then passed in 8.76s and 8.51s; final Release solution build passed in **18.68s**, zero warnings/errors.

Initial bounds were read/coordination about 3 minutes, implementation 5–15 minutes per item and checks at most 30 seconds expected per batch, stopped at twice estimate; implementation was narrowed before attempting the high-risk page rewrite. Builds used a 30s estimate/60s stop, based on the preceding small builds. All executed batches/builds completed below their stop boundaries; no writing provider needed forced interruption. No retained 100k/17GiB fixture, Editor or actual lifecycle was restarted. `build.ps1`'s full gate remains deliberately unrun under the user's small-verification instruction; SCAL-03/07/08 remain incomplete. Evidence: [record/format report](../../reports/benchmarks/metadata-encoding-20261101.json), source hashes and CTRF under `artifacts/metadata-encoding-20261101/`.

## ST-49–51: capture/apply after metadata optimization

Work began only after chat `01a19778-a47d-72f1-8c1f-0008fc633412` completed. Its uncommitted ST-44–48 changes are preserved separately in the baseline patch. Both candidates use `72601d6` plus those SPM2 changes and the same optional diagnostics. This follow-up changes no task/basis/overlay, metadata codec, CLI/wire schema or publication durability contract.

| ID | Cause, repair and retained boundary |
| --- | --- |
| ST-49 | Planning called `WindowsOriginalFilePublisher.Observe` for every target, reacquiring `ManagedDirectoryBasis.AcquireOriginal`, provider snapshot availability proof, root/ancestor handles and VCS checks. This repeated whole-basis work multiplied by the selected-file count. Optional Core `IOriginalFileObservationProvider`/`IOriginalFileObservationScope` holds the exact root/ancestors/managed reader for one plan; it rereads and releases every target and repeats availability/administration validation before `SavePlan`. No cross-command observation reuse or input-sized handle cache. Ordinary publishers keep the old port; actual publication still independently locks and revalidates every target. A 16-file native test counts resolver calls **17→2**, and checks later edits, root replacement fencing, cancellation and new VCS administration. This mechanism is a scaling diagnosis, not a measured large-input saving. |
| ST-50 | `PreserveRangePayloads` wrote and reread a separate private-range spool for every file; baseline-identical payloads never consumed the descriptor list. It now streams into the descriptor builder and drains remaining rows even when the descriptor already exists or the full payload is in the basis. The unnecessary raw-basis lookup is also omitted for basis-identical payloads. Every interval/chunk check, preservation, adoption row, independent result proof and durable root/ACK remains. New, retained and basis-identical payloads each test a valid and invalid final range; rejection preserves the prior generation and publishes no new adoption/generation receipt. Memory stays streaming. |
| ST-51 | Optional existing diagnostics now separate raw preparation, descriptor processing, adoption publication, original target observation, planning and publication. Logs are neither recovery inputs nor success receipts. Two fresh small ABBA blocks distinguish the intermediate and final candidates; they are not pooled. |

The unchanged fixture has **31 files / one directory / 992 bytes / 16 payload variants**, identical seed bytes, attributes and fixed modification times. All eight runs also have identical final paths/lengths/bytes; later edit timestamps and generated IDs differ. Each run uses 27 commands, four explicit captures, four plans/publications, final A/B captures during release and original restoration. No Editor. CLI phase totals below use entry-point time; whole time also includes dispatch, PowerShell and sampling, so these rows are not additive.

| Final block `a3/c1/c2/a4`, seconds | Before mean | Final mean |
| --- | ---: | ---: |
| Preparation | 8.95 | 8.53 |
| Capture | 18.46 | 16.47 |
| Apply, including plans | 18.61 | 16.42 |
| Reclamation/restoration | 21.90 | 18.32 |
| Whole workflow | 86.32 | 77.47 |

Whole ranges are **76.98–95.66s** before and **75.48–79.46s** after. The observed mean is 8.85s/10.3% lower, but large control-run variation also affects unchanged adoption publication/reclamation; **this is not an established causal whole-workflow improvement**. Intermediate `a1/b1/b2/a2` means are 83.52→80.15s, with whole ranges 78.50–88.53s / 78.69–81.60s. Its newly composed descriptors still used the spool. Neither block is compared with historical SPM1 trials; no fastest-run substitution.

The more repeatable attribution is 76 target observations: final mean **1.737→0.340s (80.4% lower)**, versus 1.656→0.357s in the intermediate block. All eight planning calls, including apply's internal replanning, total **5.808→4.045s** in the final block. Range-descriptor processing totals **1.007→0.951s** across six generations: the small fixture does **not** establish a meaningful capture speedup. Final-candidate remaining means across those six generations include alias pruning **3.194s**, backend completion **3.020s**, freeze **2.890s** and adoption publication **1.708s**. These involve current/pending/retained reference inventories, durable metadata and native reader boundaries; they were not removed. Nested measurements are not additive to parents.

Final-source focused verification: **28 passed, zero failures/skips**. Original observation/publication/portable fallback/conflict/same-intent recovery: 15 checks in **9.503s**, expected 12s/stop 24s. Capture/reversion/existing descriptor/cancellation/later edits/reader protection: 13 checks in **8.617s**, expected 15s/stop 30s. Earlier 10-check streaming batch took 18.274s against 10s/20s. The first test build failed only on new-test cancellation analyzer xUnit1051 (35.36s); it was corrected (7.45s). Final incremental test/product build **10.780s**, zero warnings/errors, expected 15s/stop 30s. Frozen final binaries match the current output.

Active-work estimate was 45 minutes after dependency completion; implementation began 14:42:05 UTC. Each lifecycle retained **90s expected / 180s stop**, stages start/bulk/selected/final **20/15/20/35s**, and command estimates prepare/ensure/capture/plan/apply/release/restore/schema-status **3/5/5/2/5/8/3/1s**, stopped at twice each or an earlier enclosing deadline. All **216 commands**, 32 stages and eight workflows completed below their fixed stop boundaries; the largest command ratio was release A at **14.471s / 8s = 1.809×**. No deadline enlargement, forced writer termination or extra Stop was needed. Exact PID/start identities confirm **24 recorded providers absent**, all eight A/B pairs released and originals restored to ordinary directories.

The [report](../../reports/benchmarks/scalable-storage-capture-apply-20261101.json) binds source/binary hashes, both comparison blocks, commands, diagnostics, tests and exact retained fixture roots. Raw artifacts remain in `artifacts/scalable-storage-capture-apply-20261101/`. OS caches were not cleared. No large input, actual Git/Plastic/Unity lifecycle, OS-crash matrix, peak RSS/hardware saturation or `build.ps1` full gate was run. ST-44 multi-entry pages and SCAL-03/07/08 remain incomplete; the historical tens-of-minutes/hours problem is **not proven resolved**.

## ST-52: multi-entry page adoption review

On 2026-11-02 KST the user asked whether the deferred immutable B+ tree proposal should be adopted for elapsed-time optimization, authorizing implementation if justified. **Decision: do not replace the general product metadata tree with this proposal yet.** Multi-entry pages can reduce bulk metadata, but the proposed canonical incremental boundary rule is still unspecified. This is not a rejection of all packed trees or a claim that they cannot be faster. ST-44 remains deferred; SPM2 and the current implementation remain unchanged.

Code review confirmed that `WriteNode` already appends small pages to shared containers. `Build` writes each deterministic treap node once, while `Flush` supplies a root-publication durability boundary. One key/page does **not** mean one physical file or one durable flush/key. In the tiny probe, a 256-key bulk build wrote 256 pages with one explicit page-store durability call. The owner/catalog/provider flushes are outside that probe, not removed by this design. `ReadNodeCore` still reads page header/body separately on a cache miss, so a reduction in physical reads is plausible but must be measured rather than equated with page-count reduction.

A disposable probe used the unchanged product DLLs with **64/256 keys and 16/256-byte values**. It checked bulk/unchanged rebuild, one value update, smallest-key insert/delete, old roots, bulk/update logical identity, reopened listing, Rank and EntryAt. A separate **cost model**, not an implemented B+ store, completed the proposal's missing boundary rule using canonical byte-greedy packing. It uses 512/4,096/16,384-byte caps, a 40-byte checksum header, kind/count, length-delimited leaf entries and child key/address/count/hash records. Forward/reverse/shuffled full builds agree. No nested owned-child entries occur in these fixtures.

| 256 keys, 16-byte values; data-page bytes | Actual current treap | Modeled canonical 4KiB packing |
| --- | ---: | ---: |
| Bulk | 34,243 | 8,844 |
| One same-length value update | 1,694 | 4,332 |
| Smallest-key insert | 1,974 | 8,877 |
| Smallest-key delete | 1,633 | 8,811 |

The modeled bulk reduction is **74.2%**, but one update writes **2.56×** and insertion **4.50×** as much. With 256-byte values, the same 4KiB model writes **4,854→72,429 bytes (14.92×)** for the smallest-key insert. Packing from the beginning makes every subsequent leaf boundary shift. A smaller 512-byte cap does not solve this general boundary rule: 16-byte-value insertion still writes **1,974→11,217 bytes**. These are counterexamples for this simple rule, **not** results for every B+ design or a claim that the supplied proposal had already selected it.

The next design prerequisite is a deterministic, locally maintainable boundary rule: insertion/deletion and size-changing values must not normally repack the entire suffix. Key-derived boundaries or a canonical radix partition are candidates to evaluate, not adopted designs. Separately, logical hashing must retain every ordered key/value/owned-child hash and count; a shape-dependent ordinary B+ root fails the existing order-independence contract. `CopyRootsToCore`, reachability, collection and stopped audit must traverse all entry-owned children as well as branch children, preserve old roots/reader fences and validate relocated addresses and logical identity. No hash weakening, full rehash on every small edit, or omitted recovery proof is accepted as a shortcut.

Adoption would require small comparisons of **bulk bytes, sparse and batched insert/update/delete bytes, cold/warm physical reads, durable-flush count and elapsed time**, including owner/catalog/reclamation costs. The six-generation ST-51 remainder includes inventories, native proof and durable adoption publication; this data-page model does not establish how much of that time packing removes.

The isolated helper built in **4.465s** (expected 10s/stop 20s); the actual data-page probe completed in **0.850s** (3s/6s), and the model in **0.022s** (1s/2s). Review/prototype budgets were 10 minutes each, stopping at twice each; no stop boundary was crossed. Product source hashes (513 files) and final product DLLs still match the preceding candidate. No provider, large fixture, full gate or product format change was started. The [review report](../../reports/benchmarks/metadata-multipage-review-20261102.json) preserves the actual records, model assumptions, source hashes and limitations; disposable artifacts are under `artifacts/multipage-review-20261102/` and `.local/multipage-review-20261102/`.

## ST-53–55: capture reference cleanup and durable adoption metadata

Starting source is `1b33995`, with SPM2/task v4/basis v3/overlay v3 unchanged. The user authorized further capture optimization with the same small input and safe stopping at twice the fixed estimates. Both comparison sides include identical optional diagnostics. Earlier reports and retained large fixtures are unchanged.

| ID | Repair and retained boundary |
| --- | --- |
| ST-53 | `CapturedRangeAdoptionStore.Publish` previously collected even a newly bulk-built immutable flat tree. New `PagedMetadataStore.VerifyCompactFlatTree` takes a read fence excluding writers/GC, fully verifies the ordered root and every page hash/count, verifies native container ownership, then sorts physical references and requires exact contiguous coverage of every container payload byte. Only then may it return without copying pages or writing collection intents/relocation/receipts. The original pages and adoption artifact were already flushed before this check. This is a fresh full proof, not a cached availability decision. Existing readers can coexist; writers remain excluded. |
| ST-53 fallback | Child trees, collection/relocation/receipt state, unknown controls, unreachable/torn tails or incomplete native ownership admission take ordinary collection or preserve/refuse behavior. An existing collection resumes the same request. Reachable corruption is rejected. Unowned header remnants remain unowned and preserved. No pending intent, unknown entry, durability ACK or native/content proof is bypassed. |
| ST-54 | `MetadataMembership` fully drains live, pending-adoption, basis/current and retained-generation references before alias deletion. An exact key/value dictionary uses the shared scratch budget, bounded by the sort-run budget. Contradictory lengths fail before reclamation. Budget pressure uses the existing sorted metadata tree; dictionary capacity is released before its memory charge. No whole-input allocation or input quota is added. The bound live-source reader remains alive through pruning; manifest/adoption readers remain alive while their references are consumed. |
| ST-55 | `WindowsSourceChanges` no longer repeats its just-completed full provider proof at the first empty response. Initial and response-boundary proofs, unique token/request/PID/start/snapshot validation and bound change-reader protection remain. During waiting, full proof remains on a one-second clock. Response polling is 20ms only in the first 100ms, then 100ms as before; this lowers short-response latency without a faster unbounded polling loop. |

The baseline subphase probe attributed six generations to adoption build/flush **0.031s**, immediate adoption collection **1.413s**, live reference observation **2.335s**, required-reference index **0.524s**, whole-alias pruning **0.064s** and raw pruning **0.012s**. The reference-index timer also includes enumerating source/pending/history references; the dictionary does not remove those proofs. `ResultGenerationDiagnostics` remains opt-in and non-authoritative.

Final-source checks: **25 passed, zero failures/skips**. Twelve new checks took **1.925s** (expected 8s/stop 16s): memory/disk/partial-spill/shared-budget denial, complete drain, contradictory values, cancellation, empty/nonempty coverage, reader coexistence/writer exclusion, orphan pages/unknown controls, reachable damage/truncation/native ownership, unowned header preservation and same-request recovery after root sealing or relocation publication. Thirteen existing capture/adoption/reversion/later-edit/reader tests took **7.182s** (12s/24s). Final test/product build **8.47s**, zero warnings/errors (15s/30s); earlier builds were 9.12/11.25/7.16s, all within their original limits. No full `build.ps1` gate was run.

The unchanged editorless None input has **31 files / one directory / 992 bytes / 16 payload variants**. Eight fresh workflows have identical seeds (including fixed times/attributes) and final paths/lengths/content; later edit times and generated identities differ. Each performs 27 commands, four explicit captures, four plans/publications, final A/B captures during release and original restoration. Entry-point phase totals exclude driver/sampling/dispatch overhead and are not additive to whole time.

| Final ABBA block `a3/c1/c2/a4`, seconds | Before mean | Final mean |
| --- | ---: | ---: |
| Preparation | 8.011 | 7.399 |
| Capture | 15.602 | 15.086 |
| Apply, including plans | 15.342 | 18.047 |
| Reclamation/restoration | 16.790 | 18.722 |
| Whole workflow | 72.590 | 76.116 |

**Whole-workflow improvement is not established: its final mean is 3.526s/4.9% slower.** Before runs were 72.541/72.639s; final runs were 69.642/82.590s. Capture's observed mean falls only 0.515s/3.3%. The intermediate `a1/b1/b2/a2` block was 80.423→70.630s, with ranges 72.096–88.750s / 70.548–70.711s. It predates the final polling/capacity/header refinements and is not pooled with the final block or used to replace its less favorable result. OS caches were not cleared; each final candidate has only two samples.

| Nested diagnostics, mean sum across six generations per workflow | Before seconds | Final seconds | Observed reduction |
| --- | ---: | ---: | ---: |
| Adoption maintenance: collection versus complete compact proof | 1.405 | 0.160 | 88.6% |
| Live-source reference observation | 2.366 | 1.519 | 35.8% |
| Required-reference enumeration/index | 0.544 | 0.491 | 9.7% |
| Entire alias cleanup, including the preceding two rows | 3.024 | 1.980 | 34.5% |
| Entire result preservation, including nested work | 12.671 | 11.207 | 11.6% |

The component effect repeats in both final runs: adoption maintenance 0.112/0.208s versus controls 1.388/1.422s; alias cleanup 1.884/2.076s versus 3.020/3.029s. Nested costs cannot be added together. Fresh tree construction/flush remains and is small (0.031→0.066s); full reference enumeration and durable publication are not eliminated.

The slower final run `c2` spent **6.363s** in source publication and **4.676s** in final metadata maintenance, versus **1.858/1.749s** in `c1`. Together those intervals account for about 7.43s of the 12.95s whole-run gap. The intermediate baseline `a2` also slowed in those unchanged paths (4.124/4.502s), with additional basis-preservation delay. This locates variability but does **not** prove its underlying cause or rule out a candidate contribution; no disk/antivirus diagnosis is asserted. Capture backend completion (final mean **3.187s**) and freeze (**2.781s**) also remain. No extra trial was run to select a better whole-time result.

Before execution, active work was estimated at **40 minutes**, investigation/implementation/small verification/recording **8/15/12/5 minutes**, each stopped at twice its estimate. Work began at 16:02:45 UTC. Each workflow retained **90s expected / 180s stop**, start/bulk/selected/final stages **20/15/20/35s**, and prepare/ensure/capture/plan/apply/release/restore/schema-status commands **3/5/5/2/5/8/3/1s**, stopping at twice each or an earlier enclosing deadline. All **216 commands / 32 stages / eight workflows** completed within those fixed limits; the maximum command ratio was baseline `a2` release A, **12.510s / 8s = 1.564×**. No limit increase, forced writer termination or additional Stop was needed. All eight A/B pairs were released and originals restored as ordinary directories; the exact PID/start audit found all **24 recorded providers absent**.

The [capture-maintenance report](../../reports/benchmarks/capture-maintenance-20261102.json) binds the final **516-file** source manifest, frozen binaries, baseline diagnostic patch, both blocks, tests, budgets and retained fixture paths. Raw evidence is under `artifacts/capture-maintenance-20261102/`. No large input, retained 100k/17GiB fixture, actual Git/Plastic/Unity lifecycle, OS-crash matrix, maximum RSS/hardware saturation or full gate was run. ST-44 and SCAL-03/07/08 remain incomplete; the historical tens-of-minutes/hours behavior is not proven resolved.

## ST-56–60: reference-workspace collection

The product's purpose is parallel reference workspaces and explicit original application. Result records support restart/reversal, rather than a full-project archive. This follow-up starts from `1b33995` plus the preserved ST-53–55 working changes; the previous report is unchanged.

| ID | Implementation and boundary |
| --- | --- |
| ST-56 | New file manifest **v4** keeps the full comparison catalog but retains only changed before/after payloads, range-basis dependencies and previous retained content. Direct directory-root comparison skips identical subtrees and avoids two flat work-index builds. The generation fingerprint binds the retention and descriptor logical roots. Native None/Plastic uses this path; Git keeps its protected basis, and providers without native range capture retain full-payload behavior. Earlier v3 manifests are preserved/refused, with no migration. Task v4/basis v3/overlay v3/SPM2 and public command fields remain unchanged. Changed files can still require initial basis payload copies. |
| ST-57 | Result descriptors share one writable page session and durable publication per at most 64 changed descriptors. `Complete` precedes the capture acknowledgement; disposal after failure leaves the last published prefix authoritative. File data, descriptor content hashes and native ownership proofs are retained. |
| ST-58 | Capture and fixed-result retry no longer prune aliases. Existing retained-result maintenance at release performs exact live/pending/history marking and reclamation. This deliberately retains extra aliases until that boundary; the 4MiB metadata-maintenance trigger and pending-request protection remain. |
| ST-59 | Frozen source reads fetch basis bytes only for uncovered gaps, then overlay private slices. Entirely private reads perform zero basis-byte reads. |
| ST-60 | Frozen directory listings preserve verified metadata through sorting, apply overlay precedence and hidden/excluded/path-policy rules, and avoid repeating basis `Stat` for unchanged children. |

The file manifest schema, maintenance readers, generation-history validation, bilingual contracts and lifecycle map were updated. `WorkspaceStore` validates a separate SHA-256 fingerprint's shape; the source provider verifies its exact catalog/retention/descriptor binding against the fixed receipt before accepting or using a result. No integrity check or durable write acknowledgement was removed.

### Minimal verification and observed cost

**20 distinct focused cases passed**, covering unrelated payload omission, basis loss and reversion, schema/v3 refusal and retention tampering, failed descriptor groups, raw peer reuse, private-range draining, live reference reclamation, frozen rename/reconnect, and three publication-interruption boundaries. Earlier failures included two expectations about the moved raw descriptor root and one test that attempted capture reclamation before disposing its reader; the corrected affected cases passed. A zero-match filter invocation is not counted. An additional <=65-byte test verifies the published 64-entry prefix survives a failure with entry 65 pending and the remaining entry can be retried. Product code was unchanged after the completed native comparison. The final build passed; `build.ps1` and the full gate were not run.

The first native candidate exposed a real integration omission: history loading still required fingerprint equal to revision. Capture returned `StateCorrupt` after **2.971s**; the normal Stop client hit the same check after one provider had stopped. No writer was killed or record edited. The corrected history reader allowed the other two authenticated Stops (**0.317/0.232s**) and the same `capture-1` request returned its fixed result in **1.624s**. Diagnostic fixture `b1` remains preserved/stopped and is excluded from completed timing comparisons.

Same 32-entry editorless None input, one completed baseline `a1` and one completed final candidate `b2`:

| CLI phase totals | Before seconds | After seconds |
| --- | ---: | ---: |
| Preparation | 7.341 | 7.522 |
| Capture | 16.498 | 12.769 |
| Plan/application | 17.502 | 16.688 |
| Release/restoration | 26.307 | 14.934 |
| Whole workflow, including harness costs | 85.083 | 69.445 |

Capture decreased **22.6%** and whole workflow **18.4% in this pair only**. Nested result preservation was **13.286→8.257s** across six generations; descriptor work **1.050→0.777s**; result scan **2.448→1.967s**; the first/second independent verifications **1.050/1.042→0.534/0.607s**. Capture pruning's **2.114s** is absent; retained-result maintenance at release was **0.203→0.109s**. New retention selection cost **0.695s**. Release metadata maintenance varied **5.928→2.118s**, so the whole/release difference is not attributed entirely to this patch. These nested intervals must not be added together. Equal seed metadata/content and final contents were checked; both completed A/B pairs were released and their originals restored. All **nine recorded provider lifetimes**, including the failed diagnostic fixture, were absent in the final audit.

The predeclared total estimate was **48min**, with design/implementation/minimal verification/recording **8/25/10/5min**; actual work was below the total estimate. Each trial kept **90s expected / 180s stop**, stages **20/15/20/35s**, and the previous per-command **3/5/5/2/5/8/3/1s** budgets, all stopping at twice the estimate or an earlier enclosing deadline. Focused test groups used **8/16s** and builds **15/30s**. No measured execution crossed its limit; the native candidate stopped on a functional failure, not a timeout. No limit was increased, state rewritten, or provider forcibly terminated.

[Raw report](../../reports/benchmarks/reference-capture-20261102.json) binds current sources, frozen binaries, per-command times, diagnostics, failure/recovery, and provider audit; evidence is under `artifacts/reference-capture-20261102/`. Remaining work: full catalog scanning, repeated independent payload verification, broad selected-apply comparison, global object/reference scans and packing under shared locks, plus write comparison/flush costs. A scope-bound proof or dependency-aware traversal is needed before removing those checks; they were not bypassed. No large/100k/17GiB, actual Unity/Git/Plastic lifecycle, full gate, OS-crash, peak-RAM or hardware-saturation qualification is added.

## ST-61–64: bounded capture readers and reference work

This follow-up starts at `8709f902dd14d188f82f1e8cd170e96a2514a9bb`. The main chat was idle after committing ST-56–60; its changes are not repeated. The two architecture review documents were the only initial untracked files. The product remains a reference workspace for parallel execution and explicit original publication, not a full-project archive.

| ID | Implemented work and boundary |
| --- | --- |
| ST-61 | `CaptureBackingReaders` reuses at most eight sealed-segment handles per frozen paged reader. Each handle is admitted with exact native identity/length and denies writes/deletion until eviction/close. Concurrent reads use positional I/O outside the cache lock; leased handles cannot be evicted. When all eight are busy, additional reads use temporary handles, so eight is a retained-cache bound, not a hard total handle cap. Close rejects new leases and drains active ones. Append/bounded non-segment backing behavior is unchanged. |
| ST-62 | `OwnedRangeContentStore` remembers up to 1,024 verified `(offset,length,hash)` slices per existing fenced backing proof (at most eight backing proofs). This avoids repeated range hashing only while that exact no-write/no-delete handle remains open. Eviction/disposal drops its slice proofs; length is checked on backing-proof hits. First admission, source identity, canonical object verification, shared catalog lock, durable pin intents and ACK remain. This does not transfer an unfenced upstream hash or reuse proofs across a reopened handle. |
| ST-63 | Both independent payload-verification passes stream the authenticated, hash-keyed `RetainedPayloads` tree directly. Its decoder validates rows and its tree already provides one length per hash; sorting/deduplicating it again is redundant. Other payload sources retain the original deduplication path. All actual payload reads/hashes, receipt and publication-boundary checks remain. |
| ST-64 | Logical storage accounting uses `MetadataMembership` to validate/deduplicate references before aggregation, with shared scratch budget and the existing sorted-disk fallback. Small inputs avoid sort/spool. Raw/whole identity separation and contradictory-length rejection remain. Capture cancellation now reaches accounting, total-length traversal and ordinary payload sorting. Historical reference enumeration and full basis/result length walks still exist. |

### Minimal checks, failures and limitations

Final selected suite: **23 passed, zero failures/skips, 9.293s** (expected 8s, stop 16s). A 128KiB capture read twice in 4KiB chunks admitted its backing once; later live writes left the frozen bytes unchanged. This is an observed operation count, not an elapsed-time speedup. Under the previous code each segment read opened/verified again (64 reads in this test, a static count, not a measured before-run). Reading the same raw slice twice recorded one backing and one slice proof; reopening the store established them anew. Other checks cover write/delete exclusion, idle eviction and replacement rejection, busy-reader overflow/close drain, memory/disk/partial-spill/budget denial and cancellation, failed descriptor prefixes, retained results after basis loss, changed/unchanged/private drain, raw peer reuse and existing-reference recovery.

The first default parallel build failed in 3.92s with zero reported compiler errors; the underlying cause is unknown. A verbose serial retry rebuilt unchanged dependencies; it was cancelled, with no product writer/provider running. Exact cancellation elapsed time was not retained, so do not count this attempt as a measured within-deadline build. Scope was reduced to existing dependency outputs: the changed Files project built in 0.85s, and the test project in 7.35s, zero warnings/errors. An intervening test build failed in 6.16s on missing timeout cancellation-token references; those tests were corrected. Runtime was compiled by the interrupted dependency build; SHA-256 of Runtime and Files outputs exactly matched the copies used by the final tests. This is not a successful full solution build.

A filter combining class and method criteria selected zero tests and is not counted. The initial sandbox run had 5/8 failures from denied temporary-file rename or dependent scratch cleanup; moving temporary storage into the repository reduced this to two denied native hardlink creations. The final selected suite ran outside that sandbox against only fresh temporary inputs under `artifacts/capture-read-followup-20261102/tmp`. No existing fixture, Editor, original project, provider lifecycle or large run was used. These permission failures were not repaired by changing product guards.

Predeclared work estimates: investigation 8min, implementation 15min, verification 3min, documentation 4min; stop each at twice the estimate. Builds used 15/30s and selected tests 8/16s. Full `build.ps1`, 100k/17GiB, actual Git/Plastic/Unity lifecycles, hardware utilization, peak memory and end-to-end speedup remain unverified. Evidence and source/binary hashes: [capture read follow-up](../../reports/benchmarks/capture-read-followup-20261102.json).

### Remaining candidates and writing review

- Whole-file verification before/after publication still repeats. Current independent readers close individual native fences; a root hash alone cannot prove those bytes remained fixed across the gap. Keep these checks until a bounded operation-scoped native proof or equivalent immutable-store ownership contract is implemented. Holding input-sized native handle sets is not the chosen workaround.
- Full catalog traversal, cumulative retention and historical accounting remain. Reusing only the newest logical payload set would not prove the physical reference closure of every older descriptor. Persistent aggregate/reference roots require crash-safe updates and generation deletion semantics. The new memory set reduces sorting, not the number of historical rows enumerated.
- Conditional metadata maintenance at 4MiB and backend live-repack/segment recovery remain on capture paths. Deferring new GC could shorten response but moves work and retains space; pending same-request recovery cannot be postponed. No unsupported background collector was added.
- `JournalDeltaStore.cs` raw overwrite comparison around `ChangedSlice`/line 484, commit batching around lines 850–916, and `WinFspWriteBatch.cs:86,176` retain private comparison and existing maximum 64-write/16MiB durable groups. Sequential callbacks cannot share a future ACK safely. The prior raw edge-search experiment was removed without whole-write benefit (`raw-write-batching.md`); it was not reintroduced. New writes already store raw bytes. No per-write ACK, conflict protection or replay integrity was weakened.
- Raw scratch JSON/array pooling, parallel directory scheduling, compact-proof scan fusion and a common persistent result/live root are still candidates, not adopted by this patch. The common-root proposal must resolve full-file SHA-256 after partial edits and reference lifetime before it can eliminate conversion or whole reads. Packed B+ pages remain deferred. These are explicit remaining design/performance questions, not claims that current costs are inevitable.

## ST-65–68: shared catalog proofs, persistent retention and independent composition proofs

This work starts at `8709f90` plus the uncommitted ST-61–64 changes. The main chat was checked before and after implementation and remained idle at that commit. No previous edits were discarded, and no new workspace, Editor or product provider was started. The design treats results as immutable references for parallel work and explicit application, rather than rebuilding an archive on every checkpoint.

| ID | Change and protection boundary |
| --- | --- |
| ST-65 | `PagedMetadataStore.CatalogReadScope` combines complete catalog validation and logical byte totals in one structural traversal. At most 4,096 subtree summaries are reusable under this exact reader's native container fences, plus fixed identity/location-header fences. Strict key order, directory decoding, child membership, logical/page checksums and catalog count remain. Container eviction or any write-sharing read invalidates the proof epoch; missing proofs traverse normally. Shared peer writers are not excluded by a global writer lock. The scope begins after metadata construction and spans publication/accounting only. |
| ST-66 | `BindRetainedPayloads` starts from the previous immutable retained root and path-copies only new before/after/range-basis dependencies. Existing lengths must agree. It no longer enumerates and sorts the entire cumulative history to rebuild that union. The resulting complete retained stream is still independently validated; no persisted auxiliary aggregate or new recovery state was added. |
| ST-67 | Historical accounting first proves that the current retained set covers the older one AND that every older retained file has the same physical descriptor (including whole-to-range transitions). Exact persistent-tree differences skip shared subtrees only after full native-backed graph validation, with at most 4,096 scope-bound compared-root proofs invalidated by the same fence epoch. Different/missing descriptors or non-reference manifests retain ordinary historical enumeration. Every historical header still gets owner, catalog and recorded generation fingerprint checks. Only duplicate accounting contributions are omitted; GC, alias pruning and retention/deletion policy are unchanged. |
| ST-68 | `ResultFileContentStore.VerificationScope` proves the entire composed-file SHA-256 first, then remembers at most 1,024 exact hash/descriptor pairs in that request. A second check independently decodes the descriptor/range tree and reopens EVERY declared basis/chunk through current native identity, length, ownership/reference and ready-state checks. Each dependency is hashed again unless `ImmutableReadProofs` still owns its exact no-write/no-delete backing fence. This cache holds at most 64 backings/4,096 range proofs, never evicts a fence into a stale proof, and uses ordinary hashing beyond the budget. Consequently the second check can reuse the established composition hash without reconstructing and hashing the entire file again. External Git basis composition keeps its full-file path. Whole objects already verified by their owned store do not get an extra outer SHA pass. |

Matching a manifest hash alone is never a payload proof. The independently published receipt, request/root binding, capture boundaries, publication order, durable ACK, original/peer protection and same-request retry remain. A new call/retry starts with fresh in-memory proofs. A retained canonical byte fence does **not** imply that an alias/reference still exists: those current ownership checks run again. No file-manifest/task/basis/overlay/SPM2/wire/CLI version or command changed.

### Minimal validation and observed work

The final changed-area suite passed **23/23** in **10.087s**, with zero failures/skips. The three existing publication-boundary recovery cases passed **3/3** in **4.158s**: **26 distinct selected cases total**. This is neither the whole gate nor a real provider/Unity/Git/Plastic lifecycle.

New checks cover a 31-file catalog's repeated proof/total reuse, fewer visited nodes after one changed leaf, count mismatch, write exclusion and corruption rejection after close; writable-container fallback with no reused proof; insert/change/delete tree differences; physical descriptor/whole-to-range/missing-retention accounting distinctions; whole and raw composed-file proofs; removal of a current ownership reference despite retained byte proofs; and the 65th backing falling back to hashing and rejecting subsequently changed bytes. Existing checks retain private iterator drain, peer raw basis reuse, repeated results after basis loss and descriptor-prefix failure/retry. The three extra recovery tests use the existing non-native-content fixture; native reference capture and pre-basis failure are covered separately by the changed-area suite.

For each small whole/raw composed-file case, two verifications recorded **one composed-file hash, two dependency hashes total, and one composition reuse**. Repeating the exact catalog recorded **zero additional node visits**; the single changed leaf required fewer than the original 31 visits. These are operation counts/assertions, not measured end-to-end speedups.

Initial development failures remain recorded: Runtime first failed compilation on nullable flow after a dictionary lookup, then compiled cleanly; the first 10-test selection had two fixture failures (unflushed test metadata, and an incorrect expectation that unlinking a separate owned hardlink must fail while the canonical object is fenced). The test now checks the actual invariant: ownership removal is rejected on revalidation. No product protection was weakened to fix either fixture. The three recovery tests initially used repository-local temporary folders and were rejected by the existing inherited-VCS guard, before preservation began. Running those three on fresh default system-temp inputs passed; their failed report is preserved.

Final restricted builds used existing dependency outputs: Runtime **5.50s**, Files **3.87s**, tests **7.54s**, zero warnings/errors. The Runtime/Files DLL hashes were compared with the copies used by the tests. Expected/stop budgets were investigation **8/16min**, implementation **20/40min**, selected validation **3/6min**; individual builds **15/30s** and tests **8/16s**. No executing command reached its stop boundary, and no provider needed stopping. No full `build.ps1`, large retained fixture or long performance run was started. Raw reports/source and binary hashes are in [reference proof evidence](../../reports/benchmarks/reference-proof-20261102.json).

### Remaining cost and qualification boundary

The initial source namespace observation, catalog/index construction, first complete validation of previously unseen metadata, first changed-file SHA-256, current ownership checks and all independently required payload dependencies remain. History headers and changed tree paths still cost work; divergent physical histories use the old union path. Cache exhaustion, writer-shared containers and fresh calls can repeat reads. This patch reduces repeated traversal/hash/union work; it does not establish cost independent of input/history, eliminate physical maintenance, or prove large-project latency/RAM. Shared-writer contention, a 100k/17GiB workload, full gate, actual Git/Plastic/Unity, OS interruption and end-to-end timings remain unqualified. The prior stopped large fixtures stay stopped.

## ST-69: same-small-input comparison of ST-65–68

The **work counts decreased, but a consistent capture-time improvement was not demonstrated**. Before is `8709f90` plus ST-61–64; after adds ST-65–68. All six changed Runtime/Files sources from the preceding evidence matched their recorded SHA-256 exactly during baseline reconstruction. Current product sources were unchanged by measurement and match ST-65–68 evidence. Only isolated source copies/binaries were built; no checkout, commit or product build output was replaced.

Every run used a fresh **31-file / 992-byte** synthetic immutable basis, fixed metadata, and six successive two-byte edits at offset 2 of the same file. Actual `JournalDeltaStore`, native content pins and `FileWorkspaceProvider` performed frozen capture, independent preservation/publication and capture finalization. This is an in-process collection comparison, **not** a mounted WinFsp/CLI/provider/Unity lifecycle. Editing, initial fixture preparation and final verification are outside capture time. All ten runs (eight timing, two diagnostic) had equal input digest, six result contents/fingerprints and storage statistics, and all retained results remained independently readable after basis availability was removed.

### Time: uninstrumented binaries, separate ABBA blocks

A is before; B is after. Each sample below sums six captures. Counters and optional generation logging were disabled. Each run is a new process/fresh physical fixture; OS caches and background load were not controlled.

| Block/order | A first | B first | B second | A second | Before mean | After mean | Change |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| 1: A1 B1 B2 A2 | 7.178935s | 6.741428s | 6.819520s | 5.379348s | 6.279142s | 6.780474s | **7.98% longer** |
| 2: A3 B3 B4 A4 | 3.523875s | 3.403257s | 3.489949s | 3.481885s | 3.502880s | 3.446603s | **1.61% shorter**, 0.056277s |

One final additional block was declared after the first block showed substantial order-associated variation; no further repeats were added. The drift's cause was not isolated. Do not combine the two blocks into a general improvement percentage or report only the favorable block. The second block's saving is only about **56ms across six captures**.

### Counts: separate instrumented run per candidate

Identical counter hooks were added only to isolated diagnostic copies; their elapsed times are excluded above. The scope is the same six captures, including all historical results accumulated in that run.

| Measured work | Before | After | Reduction |
| --- | ---: | ---: | ---: |
| Catalog entries examined | 2,635 | 1,696 | 35.64% |
| Retention input rows encoded, before deduplication | 38 | 18 | 52.63% |
| Current/historical physical reference rows entering accounting | 133 | 48 | 63.91% |
| Metadata cursor rows yielded | 3,667 | 2,389 | 34.85% |
| Metadata page decodes after cache miss | 17,696 | 16,670 | 5.80% |
| Instrumented content SHA passes | 230 | 128 | 44.35% |
| Bytes processed by those SHA passes | 15,730 | 13,091 | 16.78% |

Catalog visits count ordinary catalog walking plus actual nodes visited by the specialized proof traversal, excluding the virtual root and reused subtrees. Metadata cursor rows and catalog visits overlap and must not be added. Page decodes are not OS read syscalls. SHA counters cover stream payload hashes, composed-file verification, raw ranges, immutable-proof misses, source scans and private-range preparation; metadata/control/address hashes and separate journal-segment seal hashing are excluded. These are actual counts at the documented sites, not all SHA calls in the process. The after run recorded 75 catalog subtree reuses, 69 fenced byte-proof reuses and 21 composition reuses. Individual hash categories moved between implementations; compare the combined matched categories, not a single helper in isolation.

The reduced hash volume is only **2,639 bytes**, and page decodes fall much less than logical traversal rows. Diagnostic attribution still includes result scanning, raw preparation, descriptor/adoption work and durable publication. This supports investigating remaining work if needed; it does not prove flush, CPU, cache warmup or background activity caused the timing result. Nested diagnostic intervals are retained in the report and are not additive or a third timing comparison.

### Budgets, evidence and limits

Predeclared overall estimate **18min**, stop **36min**: preparation **5/10min**, instrumentation **8/16min**, comparison execution **2/4min**, recording **3/6min**. Individual builds used **15/30s**; fixture preparation **2/4s**, each capture **2/4s**, six-capture sum **6/12s**, final checks **2/4s**. The slowest capture was **2.083215s**; all ten runs finished below each execution stop boundary. Cancellation tokens and boundary checks stop later phases and preserve state on failure; no forced termination or timeout extension occurred. Native runs required sandbox escalation for fresh test hardlinks only. No provider/Editor was created, and existing large fixtures were never resumed.

The small comparison completed successfully. The report writer initially encountered Windows default cp949 decoding and was corrected to explicit UTF-8; measured files were unchanged. No full test gate, actual Git/Plastic/Unity, peak-memory/hardware measurement or large-project speed qualification is added. Measurement introduces no product code/format change. Fixtures, exact source manifests, uninstrumented/diagnostic binary hashes, harness/instrumentation scripts and raw results remain under `artifacts/reference-proof-timing-20261102/`. [Machine-readable evidence](../../reports/benchmarks/reference-proof-timing-20261102.json).


## ST-70–72: capture cost attribution and metadata reader reuse

ST-69 removed only 2,639 instrumented hash bytes in the tiny input. The next isolated measurement identifies much larger remaining work: **3,186 complete ancestor-path walks took 1.993613s of measured work**, **256 durable flushes took 0.793511s**, and **2,507 instrumented file opens took 0.218703s** across six captures. Metadata reader construction ran 430 times, taking 0.616842s inclusive of its path/open work. Page read/decode took 0.619969s inclusive, 0.304777s excluding separately timed children. Encoding/writing nodes took only 0.006729s. This explains why fewer logical visits and tiny hashes were insufficient: repeatedly admitting metadata readers and checking their paths remained substantial, alongside durability.

These are attribution measurements, not an additive wall-time partition. Parent metadata timings overlap path/open timings; exclusive work is summed across threads. File-open/flush instrumentation covers explicit FileStream calls and targeted metadata constructors in isolated Runtime/Files copies, not all native Platform opens or every process syscall. The separate caller-attribution run takes stack traces and is not a performance sample. Instrumentation is absent from product sources and final timing binaries.

### Implemented changes and protection

- **ST-70:** frozen before sources and isolated cost probes split ancestor checks, metadata construction/read/decode/write, file opens, durable flushes and atomic publication. The main chat was idle/unchanged before and after this work; ST-61–69 changes were preserved.
- **ST-71:** `ImmutableMetadataReaders` extends the request-local content verification scope with at most four metadata readers. Identity and existing location headers remain write/delete fenced; the ordinary reader lease and native container validation stay active. Overflow uses a short-lived ordinary reader. Every content/range open still locks its owner and rereads the current catalog, object/reference phase, alias/native identity and range membership. Only immutable page readers are reused. Peer appends remain visible; reclamation cannot replace the held store. No mutable ownership decision is cached.
- **ST-72:** a readonly `PagedMetadataStore` checks `identity.bin` and its entire ancestor chain once before opening anything, removing the immediately preceding duplicate directory walk. Writers still check before directory creation and again before identity access. `PagedSnapshotCatalog` can borrow a caller-owned page reader; file-manifest catalog and payload-index queries now share one reader/cache. Disposing the borrowed catalog does not close the owner; foreign roots still fail.

The reader-only intermediate candidate reduced opens but did **not** demonstrate a time improvement: its diagnostic sum was 3.851095s versus the baseline's 3.629636s. That negative observation is retained. The final candidate additionally removes duplicate ancestor walks and shares the catalog/index reader. Current path/native checks, metadata hash/count validation, same-request recovery, result independence, writer/reader fencing, individual durable ACKs and formats remain. No flush or atomic publication was removed.

### Same tiny input, final candidate

Fresh **31 files / 992 bytes**, six two-byte edits at offset 2 of one file, fixed metadata, actual in-process JournalDeltaStore/native pins/FileWorkspaceProvider. No mounted WinFsp/CLI/provider/Editor lifecycle. Preparation, edits and final verification are outside capture time. The single **uninstrumented ABBA** block, with optional generation diagnostics disabled, measured six-capture sums **A1 3.388032s, B1 3.157480s, B2 3.157714s, A2 3.321417s**. Before/after means are **3.354725→3.157597s**, saving **0.197128s (5.88%)**. This is a modest result for this small input; OS caches/background load were not controlled and no general/large-project speed claim follows.

| Separately instrumented work, six captures | Before | Final |
| --- | ---: | ---: |
| Complete ancestor walks | 3,186 / 1.993613s | 2,492 / 1.538372s |
| Metadata reader constructions, inclusive | 430 / 0.616842s | 280 / 0.216830s |
| Instrumented file opens | 2,507 / 0.218703s | 2,087 / 0.195904s |
| Metadata page read/decode, exclusive | 16,673 / 0.304777s | 16,258 / 0.295833s |
| Durable flushes | 256 / 0.793511s | 256 / 0.785565s |
| Atomic publication | 67 / 0.074369s | 67 / 0.073321s |

The final diagnostic sum is 3.405184s; do not mix diagnostic times into ABBA. Fresh physical identities also affect a few treap-node writes/decodes; those small variations are not claimed as logical write removal. All **eight runs** (four diagnostic and four timing) have equal input digest, six result contents/fingerprints and storage statistics. Every historical result verifies after synthetic basis availability is removed.

### Focused validation, budgets and remaining cost

**22 selected tests passed, zero failed/skipped**: 21 reference-proof/raw-basis tests in 8.367s, plus the reparse-parent test in 0.258s. They cover identity/reclamation fences, peer new-root visibility, four-reader fallback, borrowed-reader lifetime/foreign roots, current ownership removal rejection, raw dependencies and retained content. Runtime/Files copies used by tests match the built DLL hashes. Final restricted builds used existing dependencies: Runtime 5.34s, Files 3.95s, tests 7.17s, zero warnings/errors. Development test-compilation errors (cancellation-token usage and missing type qualification) were corrected. A combined class/method filter selected zero cases and was not counted as a pass; the corrected selections above ran successfully.

Predeclared estimates/stops: overall **32/64min**, state/design **5/10min**, diagnosis **5/10min**, implementation **15/30min**, focused verification **4/8min**, recording **3/6min**; builds **15/30s**, test commands **8/16s**, setup **2/4s**, each capture **2/4s**, six captures **6/12s**, final checks **2/4s**. Slowest capture **0.968464s**; no execution reached its stop boundary. All fresh fixtures and evidence are retained; no forced termination, timeout extension, provider/Editor operation or retained large-fixture resumption occurred.

Remaining measured costs are still mandatory current path/native checks, first unseen metadata, initial catalog/index observation and durable publication. Further savings need a separately proven lifetime or publication boundary; this work does not cache mutable ownership or weaken durability to obtain them. Full `build.ps1`, large inputs, actual Git/Plastic/Unity, OS interruption, contention and memory qualification were not run. Raw sources/probes/harness/results are in `artifacts/capture-cost-20261102/`; [machine-readable evidence](../../reports/benchmarks/capture-cost-20261102.json) pins changed source/test hashes and reports.


## ST-73–74: request-bound path proofs and metadata flush grouping

This continues from the exact ST-70–72 working sources and preserves earlier work. The main chat was idle/unchanged at entry. The optimization admits only a protected request lifetime; it does not treat a remembered path or a mutable ownership catalog as a reusable proof.

**ST-73 — path checks.** Optional Runtime `IRequestPathGuard` / `IJournalFilePins.PinMetadataPaths` uses the existing Windows journal directory-guard implementation with an existing metadata `identity.bin` marker. Native directory handles pin every ancestor against rename; the nondeletable marker keeps the chain nonempty and prevents reparse conversion. Exact path spellings are matched, including beneath case-sensitive NTFS directories. A currently mutable leaf is inspected on every call. Only the already pinned directory or a direct child of a pinned ancestor is covered; other paths take the ordinary full walk. Missing native proof returns null/falls back; an observed reparse or access error during a leaf check is still refused. No new ownership marker is created.

The guard belongs to one of the existing **maximum four** metadata readers in `ImmutableReadProofs`. Content/range owner locks, current catalog/object/reference phase, alias/native identity and range membership are still checked on each open. Reader disposal releases the path proof, and there is no ambient/global path cache. Regular callers and platforms without the optional port keep full path checks. `PagedMetadataStore`'s existing public constructor and wire/CLI capabilities are unchanged.

**ST-74 — flush grouping.** A new metadata container still durably records its native owner before writing header bytes. Its header is made visible using ordinary buffered flush, then header and pages share the required durable flush **before any root acknowledgement**. The header is an exact copy of the independently durable identity and an interrupted owned prefix can be resumed. A successful durable flush records only this open writer's watermark. Another `Flush()` with no changed bytes reuses it while still calling the existing `metadata.pages-durable` acknowledgement boundary; any write marks the stream dirty before bytes are emitted. Flush failure cannot advance the watermark. A reopened writer forces its first flush even if no new bytes were written. Container rotation flushes the old dirty output before switching. Native owner receipts, identity-prefix/checksum flushes, control publication ordering, and individual operation ACKs are not deferred or removed.

### Same-small-input observations

Each new process/fixture used **31 files / 992 bytes**, six two-byte edits of one file, and fixed metadata. Actual in-process journal capture/native pins/file result preservation ran; no mounted WinFsp/CLI/provider/Editor was created. Setup, edits and final verification are outside capture timing. Optional diagnostics and instrumentation were disabled for ABBA.

| Candidate / ABBA samples (six captures each) | Before mean | After mean | Saving |
| --- | ---: | ---: | ---: |
| Initial implementation: A1 3.227898, B1 3.150454, B2 3.223370, A2 3.288069s | 3.257984s | 3.186912s | 0.071072s / 2.18% |
| Final exact-spelling / fallback hardening: A3 3.995403, C1 3.676519, C2 3.679434, A4 3.755306s | **3.875355s** | **3.677976s** | **0.197378s / 5.09%** |

The second block was required by the final path-safety change. Keep candidates/blocks separate; do not pool them or claim the hardening caused the larger percentage. Background/OS-cache effects were not controlled. Both savings are small; these observations do not qualify general or large-project performance.

Separate diagnostic counts/costs across six captures:

| Work | Before | Final |
| --- | ---: | ---: |
| Full ancestor walks | 2,492 / 1.595034s | 2,129 / 1.458398s |
| Durable flushes | 256 / 0.788842s | 240 / 0.735610s |
| Atomic publications | 67 | 67 |
| Added native path-pin scopes | 0 | 18 / 0.015803s |
| Guarded path attempts, including misses | 0 | 916 / 0.021957s |

All **16 removed flushes** in this input are the standalone metadata-container header flushes. No-op same-writer flush reuse is demonstrated by the small test, not claimed as another measured reduction here. Full ancestor walks fall by **363 (14.57%)**; mutable leaf checks remain. Native guard setup itself opens handles and has a measured cost. The FileStream open counter is not a total native syscall count. Inclusive metadata times overlap child path/open times; per-thread sums are not an additive wall-time partition. Final diagnostic elapsed time was **3.550311s**, versus **3.475808s** before, so diagnostic wall time is not a demonstrated gain and is excluded from ABBA.

All **11 runs** (three diagnostic, eight timing) have identical input digest, six result contents/fingerprints and storage statistics. Every retained historical result also verifies with synthetic basis availability removed. Raw intermediate/final results remain separate.

### Protection checks and limits

**20 unique focused cases passed, zero failures/skips:** final batching/reference-proof selection **14 in 1.490s**, plus unchanged related recovery selection **6 in 0.639s**. Initial 13-case results are also retained. Checks cover pinned ancestry/marker rename and deletion refusal, mutable leaf junction rejection, exact-spelling fallback, missing/unsupported proof without creating ownership, fresh ownership removal rejection, old/native roots, partial owned header repair, interrupted page writes and initial native-receipt gaps. Partial-header tests model interruption; they are not OS power-loss qualification. Final Runtime/Files/Windows DLL copies used by tests match the built hashes. Restricted builds used existing dependencies; final Windows/tests builds were 4.44/7.76s with zero warnings/errors.

Predeclared estimate/stop: overall **29/58min**, review **6/12min**, implementation **15/30min**, comparison/verification **5/10min**, recording **3/6min**. Individual builds **15/30s**, tests **8/16s**, setup/final verification **2/4s**, each capture **2/4s**, six captures **6/12s**. The maximum capture was **1.104481s**; no phase/executing command reached its stop threshold. Cancellation/boundary checks preserve state and stop subsequent work. No forced termination or timeout extension occurred.

Mutable namespace branches, first admission outside the guarded set, durable ownership/control receipts, identity initialization and data-before-root publication still cost work. Combining those separate durability boundaries would need a new recovery proof; they remain intact. No format change, full `build.ps1`, large fixture, actual Git/Plastic/Unity, OS interruption or concurrent-system qualification was added. [Evidence](../../reports/benchmarks/capture-batching-20261102.json), with exact source/binary hashes and raw fixtures/scripts under `artifacts/capture-batching-20261102/`.

## ST-75–78: preparation path and node lookup costs

The user requested fixing the actual preparation paths identified by [RECHECK-01/02](scalable-storage-throughput-diagnosis.md). Earlier 120s estimate-based stopping was explicitly corrected to historical half-time limits. Source started at `62c29ed`, preserving the uncommitted diagnosis/report updates. No task/basis/overlay, SPM2, manifest, root/hash or command format changed.

| ID | Change and retained boundary |
| --- | --- |
| ST-75 | Managed None preparation keeps ancestor VCS checks, then combines nested VCS and `.splinter-apply-` refusal with `SnapshotInputChecks` admission. Native ordinary root/ancestor/descendant directory pins replace a separate per-file rootward pass before source move. ADS/hardlink/reparse/type checks remain; ancestor traversal and the combined scan check cancellation. |
| ST-76 | File source scans refresh enumeration metadata before using it. One current leaf observation supplies attributes/length/mtime and the first path check; every ancestor still receives a current attribute query. `Plain` no longer performs Exists probes before that query. Hashing still opens/rereads each file, then compares a fresh combined metadata observation and stream length. No metadata-only hash reuse. |
| ST-77 | `PagedNodeMap` reuses decoded live nodes with 512-entry/estimated 1MiB bounds. Save refreshes the entry/charge; root changes and discarded pending admissions clear it. Frozen-root enumeration decodes its own records. Availability/poison checks precede reads. Cache eligibility requires one writable, non-derived store and unchanged native reader-fence epoch; eviction/append-writer rollover invalidate it, and write-shared reads disable it for that store lifetime. |
| ST-78 | Separate candidate blocks, source/binary hashes, fresh native small input, exact retained-request continuation, focused preservation tests and normal authenticated Stops. No timeout extension, original publication, state editing, migration, release/discard/restore or full gate. |

Final focused checks: **54 unique passed, zero failures/skips**: preparation/cache/admission, journal read concurrency and batch/metadata-reader checks **38 in 3.128s**, file source proof **11 in 5.044s**, existing capture/flush pin checks **5 in 0.455s**. Tests cover cache eviction/rename/write/replay, fixed cursors, poisoned batches and aborted read-only admissions, concurrent base-read generations, reserved names, ADS/hardlinks, cancellation, native handle eviction, derived/write-shared corruption detection and existing response proof consumption/change detection. Initial test-only builds exposed cancellation-analyzer and nullability errors; the repaired Release builds passed with zero warnings/errors. These checks are not OS-crash or whole-suite qualification.

Fresh native None preparation uses **1,000 entries / 999 32-byte files / one directory / 16 variants**, identical seed formulas/times/attributes. Final block order **A4 / D1 / D2 / A5**; each trial normally stops its exact providers and preserves data. A preparation times are **19.217666 / 9.274589 / 8.995229 / 19.804333s**. Before/final means are **19.510999→9.134909s (53.180721% lower)**. Basis preparation means are **2.967092→2.670940s (9.981228% lower)**. No Editor; schemas/input preparation/normal Stops lie outside the CLI entry-point timings. Each small diagnostic command has a fixed 60s cancellation cap; none reached it. OS caches/background load and generated native identities differ, so this is a small matched-shape block, not general throughput qualification.

Two earlier candidates remain separate. A1/B1 gave A **19.593774→10.599584s** while basis **2.998803→3.241405s** did not improve. The subsequent A2/C1/C2/A3 block, before native-epoch hardening, gave A means **19.797007→9.623646s** and basis **2.928511→2.562497s**. Do not pool these blocks or substitute a faster sample.

The retained 100k fixture was explicitly reopened. In the pre-epoch-hardening candidate, the same A reached `ready` in **347.940047s including original restart**, reusing its already completed preflight/baseline; this is **not fresh full A preparation or a causal whole-preparation comparison**. The first B on that retained basis completed its full preparation in **521.000061s**, below half its historical **2,446.228702s** recovered command (**1,223.114351s**). Both A/B had the same basis revision and expected first-file bytes, then all three providers stopped normally. Concurrent small correctness/build activity and earlier fixture/cache history make this a functional elapsed observation, not an isolated percentage for the patch.

The **final epoch-guarded candidate** completed first task C (`scal-c`, `afdd57d034cacd3c4e37b800`) on the same retained 100k basis in **516.449302s**; original restart included **524.913097s**. Both are below the historical B half-time boundary **1,223.114351s**. Full task preflight/baseline/inspection and response proof ran, with C's basis revision equal to A/B and expected source bytes. This is a new task on a previously used basis/original, not fresh basis preparation or a controlled causal comparison with historical recovered B. The final C result is separate from earlier A/B binaries. C and original returned authenticated Stop receipts at **20:10:25 / 20:10:26 +09:00**; final audit found A/B/C/original unmounted, no matching CLI/provider and zero user commits. All data/keys/digests remain; no release/discard/restore.

Full original/basis/workspace scans and first node admissions remain. C's retained index-page file lengths total **1,674,894,227 bytes**, including append/COW history; decoded-node caching reduces lookup work, not this admission write amplification or allocated disk/RSS. No 100k bulk write/capture/plan/apply, 17GiB, actual Git/Plastic/Unity, full gate, OS-failure or peak-RSS qualification is added. The [machine report](../../reports/benchmarks/preparation-bottlenecks-20261102.json) binds final source/binary hashes, all candidate blocks, exact retained paths, 54 passing checks and Stop audits. Raw artifacts and failed/intermediate trials remain under `artifacts/preparation-bottlenecks-20261102/`.

The later explicitly requested [fresh 100k remeasurement (RECHECK-03)](scalable-storage-throughput-diagnosis.md#recheck-03) records new basis/A/B preparation in **43.443820 / 611.553764 / 530.251476s**, all within historical half-time thresholds. Bulk writes stopped at **1,472.0838445s**, returning after drain at **1,472.2870604s** with **98,178 / 99,990 files** complete; capture/plan were NotRun. All three providers normally stopped with state preserved. Its per-file flush/index/maintenance diagnosis and fresh-fixture limits are separate from ST-78's earlier measurements and do not complete the large lifecycle.

<a id="st-7982"></a>

## ST-79–82: mixed callback durability and bounded shadow replay

The user requested fixes for RECHECK-03 with local verification first. Existing preparation changes and retained 100k state remain intact. Task/basis/overlay/SPM2/journal and CLI/wire formats are unchanged; Core gains an optional portable port, not a new advertised CLI capability.

| ID | Final change |
| --- | --- |
| ST-79 | `IWorkspaceMutationBatch` combines synchronous range/metadata effects in one owning journal. WinFsp admits writes, size/time, Overwrite and Open through the same FIFO before its mutation gate. At most 64 callback/chunk units and 16MiB requested staging share an ACK. Oversized callbacks run alone with existing chunk barriers; unsupported/composite stores keep their fallback. With multiple open handles, a mutating leader may yield to a 100µs monotonic deadline to gather a second mutation, without holding mutation/journal gates or sleeping on an OS timer. This is cooperative scheduling, not a real-time latency guarantee. |
| ST-80 | `ExtentList.Truncate` avoids a second node save when its private extent root is unchanged. Entry/base-length/time commits and clipped-tail zero filling remain. Overwrite now uses the existing metadata/index batch instead of individual index edits. |
| ST-81 | Private shadow-tail replay uses the existing bounded node/name/namespace/change-index batches. Full catchup/root comparison, native/ownership proofs and durable rotation still precede publication; private replay publishes no accepted ACK. |
| ST-82 | Allocate a bounded callback array before dequeueing accepted inputs; publish write completion watermarks before the next leader can advance them; retain interrupted callers' native buffers until drain. Diagnostic store samples include the outer shared flush exactly once and preserve failure accounting. Small literal/runtime, failure, cleanup, transfer, packing-recovery and reader/cache tests own the qualification. |

Final Release build: **16.72s, zero warnings/errors**. **106 unique tests passed, zero failed, three opt-in cost tests skipped**: callback/ACK/resize/cleanup/transfer **65 passed + 3 skips in 4.151s**, selected small packing recovery **13 in 14.751s**, cache/native-fence/read-concurrency **28 in 2.792s**. The new tests include active open handles, mixed range/metadata failure before ACK, queued Open retention, truncate/extension, shadow rename/delete and interrupted same-request recovery. Existing interruption, explicit drain and oversized-callback checks also passed. No whole gate or OS-crash qualification is inferred.

Small native probes use a fake fixed basis, pre-admitted indices, 32-byte files/16 variants, the same write/mtime operation and eight ready dedicated workers. Every view normally unmounts and every file plus durable seal is checked on replay. Setup, admission, dispatch, unmount and replay checks are outside write timing.

The **final paired 1,000-file protocol** keeps two new processes idle between 64-file steps (last 40), alternates AB/BA, and permits only one measured writer at a time. Sums of actual write intervals are **7.566090→6.026501s (20.348538% lower)**; flushes **2,829→2,114 (25.273948% fewer)**, with all 1,000 files and eight concurrent application operations confirmed on each side. This is not an uninterrupted CLI/workflow time or a historical 100k improvement.

The final ordinary 1,000-file ABBA is also retained: **8.007170 / 9.445883 / 15.730611 / 14.067202s** gives before/after means **11.037186→12.588247s (14.053047% worse)**, while flushes remain lower. The final 512-file shadow block including synchronous injected edits is **12.459031→15.706208s (26.062840% worse)**. Therefore consistent whole-block elapsed improvement is **not established**. In that same shadow block, subtracting the separately measured, directly nested edit callback gives collection-only means **1.558859→1.041613s (33.181115% lower)**. Newly built shadow index page-file lengths are **15,346,896→1,673,443 bytes (89.095886% lower)** on both pairs; these are retained file lengths, not cumulative writes or allocated disk bytes.

Intermediate evidence stays separate: initial 256-file A/B/C candidates had small elapsed differences without a substantial native flush reduction; a traced block's 60.4% apparent gain was dominated by a slow first baseline and is not representative. A 1ms timer candidate reduced flushes but regressed **14.351234%** and was rejected. The retained 100µs candidate measured **18.119117%** lower time at 256 files; its first 1,000-file block was **53.337436%** lower with baseline **19.616421 vs 7.907237s**, and its repeat was **18.826729%** lower. None is pooled with the final source or the paired protocol; unfavorable observations are not discarded.

The first build failed on a new test's missing `Rename` argument after **81.275736s**, missing the declared **40s** stop. A probe rebuild also exceeded **20s** at **23.31s** and had completed before inspection; no process was killed. Both are procedure misses, preserved rather than erased by later passes. Subsequent commands disabled build-server reuse and narrowed test-only rebuilds where appropriate. A sandbox-only atomic-rename denial, an intermediate diagnostic-record accessibility error and five pre-flush telemetry failures were corrected before final passing checks. These are not product throughput gains.

All **56 new probe fixtures** have no remaining view; final audit finds no matching probe process. Retained 100k/basis/task/journal/request identities are not resumed, edited, released, discarded or restored. No capture/original apply, 17GiB, Git/Plastic/Unity, full gate, peak RSS or general single-writer latency claim is added. [Machine report](../../reports/benchmarks/write-bottlenecks-20261102.json) binds final source/binary hashes, all candidate blocks, paired steps, 106 passes, failures/stop-policy misses and the closeout audit. Raw evidence is under `artifacts/write-bottlenecks-20261102/`.

## Later explicitly requested 100k continuation (RECHECK-04)

The preceding ST-79–82 local verification did not resume the retained fixture. The user subsequently explicitly requested its [2026-11-02–03 continuation](scalable-storage-throughput-diagnosis.md#recheck-04). The same final candidate's **328 source/config and 32 binary hashes match**. A new full pass over all **99,990 files** completed writes in **1,254.7332609s**, capture in **707.5361660s conservatively including failed-attempt/reconnection overhead** (successful capture command **440.5851863s**), and readonly original planning in **464.9044448s**. All are below their separate historical half-time thresholds; the old plan time is an interrupted lower bound.

This reuses prior private ranges/history/preparation and is not fresh ABBA or a causal patch speedup. The initial `SourceCaptureUncertain` came from an experimental direct restart retaining the old workspace PID/start lease; normal exact ensure and the same immutable request recovered it without repeated writes or manual state edits. Reconnect accounting **304.9382314s** is not fresh basis/A preparation. Remaining costs include frequent durable flushes, range-relocation/index amplification, per-file `baseline.Find` catalog reopening in retention selection, and current-original metadata scanning/index construction. Exact counters, stacks, qualifications and identities are in [RECHECK-04](scalable-storage-throughput-diagnosis.md#recheck-04) and its [machine report](../../reports/benchmarks/throughput-resume-st79-20261103.json).

The plan contains **99,990 changes / zero conflicts**; no original publication followed. All three providers/views are absent after authenticated normal Stops, one immutable result remains and no capture/original-apply intent is pending. The managed original remains stopped, not restored. No release/discard/restore, full lifecycle/gate, unique large assets/17GiB, Git/Plastic/Unity or OS-crash qualification is added.
