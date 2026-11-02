# Scalable-storage throughput diagnosis

The user-requested local fixes for RECHECK-03 and their bounded/variable timing evidence are [ST-79–82](scalable-storage-throughput-followup.md#st-7982). The later explicitly requested retained 100k continuation is [RECHECK-04](#recheck-04) below.

Post-commit file-write attribution is [WRITE-COST-01](#write-cost-01): durable-barrier latency, a shared index-persistence timing gap and packing amplification remain distinct costs.

The subsequent user-authorized implementation and bounded verification are tracked in the [throughput follow-up](scalable-storage-throughput-followup.md). The original observations below remain historical evidence.

The user-requested preparation fixes and final guarded-candidate checks are [ST-75–78](scalable-storage-throughput-followup.md#st-7578-preparation-path-and-node-lookup-costs). Keep the earlier failed/recovered timings below separate from those new measurements.

[한국어](../ko/verification/scalable-storage-throughput-diagnosis.md) · [SCAL qualification](scalable-storage.md) · [Machine report](../../reports/benchmarks/scalable-storage-slowpoints-20261101.json)

The actual 100,000-entry tiny-file inputs exposed **unacceptably long preparation, writes, capture and planning for an interactive development workflow**. Removing aggregate limits has not established usable throughput. These are recovered functional runs with concurrent host load and mixed frozen stage binaries, not matched benchmarks or predictions for an ordinary large project.

Status: **Throughput concern open.** The 2026-11-01 sections retain the curtailed historical evidence; they do not authorize automatic resumption. The user explicitly requested the bounded retries and continuation below on 2026-11-02–03. SCAL-03, SCAL-07 and SCAL-08 remain open in the [owning ledger](scalable-storage.md).

<a id="recheck-04"></a>

## 2026-11-02–03 retained 100k continuation after write fixes (RECHECK-04)

The user explicitly requested resuming the preserved 100k work and stopping safely if elapsed time exceeds half the historical record. Final ST-79–82 ran unchanged throughout: **328 source/config hashes and 32 frozen binary hashes match**. The same RECHECK-03 None basis/A/B and request identities were retained. This continuation ran a **new full 99,990-file write pass**, not just its remaining 1,812 files, using the historical eight-worker, 32-byte variant edit and fixed-mtime driver. Existing private ranges/history, indices, startup packing and host/cache conditions differ from the historical/fresh runs; these are observed phase times, **not a controlled causal patch speedup**.

| Phase | Historical seconds | Half-time stop, seconds | Current seconds / outcome |
| --- | ---: | ---: | --- |
| Full 99,990-file writes | 2,944.1676890 | 1,472.0838445 | **1,254.7332609**, all files / 3,199,680 bytes completed and drained; **42.62%** of the historical elapsed time |
| First capture, conservatively including reconnection | 3,869.3736515 | 1,934.68682575 | **707.5361660**, completed; **18.29%** of the historical elapsed time |
| Readonly original plan | **3,945.0931925 interrupted lower bound** | 1,972.54659625 | **464.9044448**, completed; 99,990 changes / zero conflicts |

No measured phase reached its historical half-time deadline. The old plan never completed; do not infer an old completed-plan time or a matched plan speedup. The write clock excludes dispatch/type loading and normal Stops. Capture conservatively includes the initial **0.5130345s** failed command plus the **707.0231315s** active continuation, including original/A reconnection and exact ensure. The successful capture command alone took **440.5851863s**. Diagnosis/user-wait time between stopped attempts is not active command time. Read-only stack observations occur inside the respective measured clocks; no adjustment is subtracted. No competing build/tests ran during timing.

Even including the stopped diagnosis interval and normal Stops, the observed timestamp span from first capture launch **00:04:26.2271448** to successful completion **00:22:16.6873225 +09:00** is **1,070.4601777s (17m 50.46s)**, also below the **1,934.68682575s** deadline. Keep this wall span separate from the active-command total; the outcome does not depend on excluding the diagnosis interval.

Initial original/A/B restarts took **38.6976785s**. The later original/A normal reconnect and product `workspace ensure` took **266.2405488s** (ensure entry point **259.3072815s**); combined reconnect accounting is **304.9382314s**, within the **1,009.3601973s** preparation budget. This reuses completed preparation and is **not new basis/A/B preparation**. RECHECK-03's fresh **43.4438196 / 611.5537642 / 530.2514760s** remain the relevant fresh-preparation observations.

The initial capture failed with safe-to-retry `SourceCaptureUncertain`, not a timeout. The experimental restart helper started new owned provider lifetimes without updating the workspace record's old PID/start lease. Freeze had already durably fixed the capture. All three providers stopped normally; then the same product ensure persisted A's current lease before retrying **the exact `capture-1` request**, without another file-write pass or manual record edits. Result **`13b3a5724ada7f8306edf2b0`**, source sequence **594,504**, capture digest **`2acaf50ba2efeaebb47fb2a034a45093d345f791a282fef4d71a055921acde04`** stayed fixed. This is a harness reconnection defect and recovery overhead, not a failed half-time throughput result.

Remaining measured costs and efficiency candidates:

1. **Durability and metadata amplification remain substantial.** Same-epoch observations from before writes through initial capture/normal Stop add **220,716 flushes / 623.8871018 cumulative flush seconds**, **644,046 commits** and **336,687,984 journal-appended bytes**. This interval includes maintenance and tiny freeze/Stop costs, not exclusive write-only timers or allocated disk bytes. It contains **299,970 user commits** plus **338,643 additional range relocations**. A valid write stack shows `ApplyMutationBatch → CommitStorageBatch → FlushPendingWrites` and paged node persistence. Investigate remaining callback ACK sharing, index path reuse and packing live/reclaimed ratios while retaining durability, GC and native proofs; thread waits/operation timers overlap wall time.
2. **Per-file catalog reopening is code-confirmed.** Capture stack 1 shows `BindRetainedPayloads → baseline.Find → new PagedSnapshotCatalog → ReadNodeCore → EnsurePlainPath`. `FileSourceManifest.Find` opens a reader for each call even though a bounded operation-local `OpenReader` already exists. A fenced reader reused across retention selection is a concrete follow-up candidate; one sample establishes neither exclusive time nor a call count. Capture stack 2 contains idle/awaiting CLI threads and does not attribute asynchronous provider work.
3. **Full current-original observation/index construction remains.** The plan stack shows `Scan.Metadata → CheckedOriginalEntries → FileApplyIndex.Build`. Repeated metadata transitions are candidates for local attribution; whole current-source validation must remain. Capture adoption separately completed **99,990 commits with 1,563 flushes**, rather than one flush per range. Those operation counters do not partition the entire capture clock.

The final plan digest is **`sha256:c417a0b91af205f6528e66a1df5649c328af7a08ec86b16b5ae566ef38871680`**. **No original publication** followed it. A and original received authenticated normal Stop at **00:30:02–06 +09:00** on November 3; B remained normally stopped. The exact fixture process scan and all three view checks found no matching CLI/provider or mount. A/B remain `ready`, A retains one result, and neither has a pending capture/original-apply intent. Task/basis/private journal/result/request evidence remains; the managed original is **stopped, not restored**. No force kill, release, discard or restore occurred.

This is one retained None tiny-file continuation, not fresh ABBA, unique large assets/17GiB, full original apply/release, Git/Plastic/Unity, full gate, peak-RSS or OS-crash qualification. [Machine report](../../reports/benchmarks/throughput-resume-st79-20261103.json) binds identities, budgets, hashes, phase metrics, failure/recovery, counters and closeout; raw evidence is under `C:/Users/olive/AppData/Local/Temp/sscal3n-20261102-remeasure/evidence/resume-st79-20261102` and `artifacts/throughput-resume-st79-20261102/`.

<a id="write-cost-01"></a>

## 2026-11-03 write-cost attribution after the commit (WRITE-COST-01)

The user requested committing the completed work before analyzing file writes. Commit **`c6e0b17`** contains ST-75–82, their focused tests and RECHECK-04. This analysis uses its existing same-epoch counters, **14 saved observations / 13 adjacent intervals**, one provider stack and current code. **No 100k/provider/CLI restart, payload access, new benchmark or product change** was needed. [Machine report](../../reports/benchmarks/write-cost-diagnosis-20261103.json) retains input/sample hashes, all interval calculations, reconciled operation deltas and uncertainty.

The primary measured limit remains durable journal barriers, followed by insufficiently attributed index persistence; packing is substantial work amplification but a small share of directly timed barriers. The interval includes concurrent maintenance plus tiny initial capture/Stop overhead, so it is not a pure exclusive write timer.

| Cost group | Durable commits | Charged flushes | Flush seconds | Journal-appended bytes |
| --- | ---: | ---: | ---: | ---: |
| User resize/write/metadata | 299,970 | 215,200 | 611.6502652 | 117,188,280 |
| Packing progress/relocation/completion | 344,075 | 5,515 | 12.2348547 | 219,498,852 |
| Initial capture freeze | 1 | 1 | 0.0019819 | 852 |
| Total delta | 644,046 | 220,716 | 623.8871018 | 336,687,984 |

**Barrier latency and occupancy.** Cumulative flush time is **49.72%** of the 1,254.7332609s write wall; mean flush duration is **2.826651ms**. User commits per user-charged flush are only **1.393913**. This is an aggregate proxy, not the native callback batch size: shared barriers are charged to their final operation. The 64-unit limit does not promise 64 concurrent synchronous callbacks, and Journal `waitingMutations` excludes the upstream WinFsp FIFO. Its observed maximum of one does **not** establish that only one application worker ran. Native admission histograms were not enabled in this 100k run.

The saved **23:56:54.8445189–23:57:56.8111595 +09:00** window lasted **61.9666406s**, with **4,543 flushes / 54.4972177 flush seconds**: **11.995866ms** per flush and **87.95%** of that window. User throughput fell to **103.700958 commits/s**, versus **346.785536 commits/s** in the fastest saved window, whose average flush was **2.064368ms**. Neither interval added recorded relocations or packing completions. This locates a durable-barrier stall; it does not identify whether the underlying delay came from storage, OS, another process or concurrent index activity. Wait totals across threads must not be added to wall time.

**Index-persistence timing gap.** `Commit` times `Apply` per mutation, but `FlushPendingWrites` calls `FlushIndexBatch` **before** starting its final barrier timer. Deferred node/path/name changes persist via `SetBatch → WriteNode`; those shared persistence costs fall outside the individual `ApplyTicks` and that final barrier interval. The user-operation Apply total **97.9749945s** therefore does not measure the entire index cost. The provider stack directly places `SetBatch/Persist/WriteNode` in this path, with Close/GetFileInfo and other callbacks waiting. `CompleteMetadata` holds its mutation gate through index work and durability. Its gate/queue waits are downstream symptoms; the unpartitioned remainder cannot simply be labeled index time. `WriteNode` also queries the output length before capacity checking and append positioning; its exclusive syscall cost is unmeasured.

**Packing amplification and counter attribution.** Packing accounts for **65.193551%** of journal-appended bytes but **1.961069%** of directly timed flush seconds. It adds **338,643 relocations**, **5,426 progress records** and **six completion records**. Most groups contain up to **63 relocations plus one progress record**, so the shared flush is usually charged to `packing-state`. The relocation-only **83 flushes** are not the packing barrier count; the full packing group has **5,515**. Journal append traffic is **105.225518 times** the 3,199,680 changed payload bytes, excluding metadata page writes and without claiming physical allocation/bandwidth. Actual per-job net reclaimed bytes and copy/index cost remain unrecorded.

`BuildPackingPlan` admits a source when `descriptor.Length - liveUnion >= 4096 && liveUnion > 0`; it has no live/dead fraction or relocation-row cost screen. First journal-to-raw packing can legitimately remove frame overhead. Repeated packed-to-packed copying can instead move almost all live rows for a small dead region. **Arithmetic illustration only:** a 3,199,680-byte packed payload with 4,096 dead bytes (128 of these 32-byte files) can leave **3,195,584 live bytes / 99,862 file ranges** to move, roughly **780.17 live-copy bytes per dead byte**, ignoring header bytes conservatively. This is not an observed job or measured waste in RECHECK-04.

The next efficient local investigation should first measure FIFO/group occupancy, mutation-gate residence and `FlushIndexBatch` time/page bytes separately, including the historical `Parallel.For` driver versus dedicated ready workers. Then target repeated node/path persistence. Finally measure net reclaim versus moved bytes/rows before testing cost-aware packed-object admission/hysteresis. Keep per-callback durable ACK, input/handle leases, poison/drain, fresh native/root/cursor proofs, pending-request recovery, proven-dead collection and final forced cleanup. Increasing timeouts, removing ACKs, enlarging the cache/worker count without evidence, disabling GC or replacing all SPM2 pages is not supported by this diagnosis. No new fix or improvement percentage is claimed.

<a id="recheck-03"></a>

## 2026-11-02 fresh 100k remeasurement after preparation fixes (RECHECK-03)

The user explicitly requested another measurement and a safe stop **at or above half the historical time**, followed by bottleneck analysis. This trial uses the unchanged final ST-75–78 guarded binary: all **327 source/config hashes and 32 binary hashes** match its recorded manifests. A fresh None fixture contains the same **10 directories / 99,990 ordinary 32-byte files / 16 variants / fixed timestamps and attributes**. Input generation took **51.947688s**, outside product timing. Preparation starts with a new basis and new A/B; no earlier completed preflight or baseline is reused for A. The general estimate-times-two rule does not shorten these explicitly requested historical half-time budgets.

| Phase | Historical seconds | Half-time stop, seconds | Current seconds / outcome |
| --- | ---: | ---: | --- |
| Basis preparation | 189.7678915 | 94.88394575 | **43.4438196**, completed |
| A preparation | 2,018.7203946 | 1,009.3601973 | **611.5537642**, completed |
| B preparation | 2,446.2287019 | 1,223.11435095 | **530.2514760**, completed |
| Eight-worker bulk writes | 2,944.1676890 | **1,472.0838445** | **1,472.2870604**, stopped after accepted operations drained; **98,178 / 99,990 files** completed |
| First capture / readonly original plan | Separate historical records | 1,934.68682575 / 1,972.54659625 | **NotRun** after the write deadline |

All three preparation commands returned `Succeeded` within their thresholds, including response proof. Their observed elapsed differences are 77.106865%, 69.705871% and 78.323716% lower respectively. Historical A is a final successful recovery call, and historical candidates/cache/host conditions differ; these differences are **not controlled causal patch speedups**. Historical planning time is an interrupted lower bound, not a completed denominator. B shares the newly prepared basis after A, as in the sequential fixture scenario.

The write driver uses the same eight-worker 32-byte edit and fixed last-write-time operation. At **1,472.0838445s**, it blocks new file admissions, then drains the accepted per-file write/mtime operations. The **0.2032159s** overrun is drain/return time, not an increased budget. **1,812 files remain unwritten**; do not extrapolate this partial run into a completed write time or claim a 50% completed-write improvement. The bulk clock measures the write driver; its type loading, input preparation, schema calls, dispatch and normal Stops are separate. No original-only edit, capture, plan or original publication followed the deadline.

The stopped writer's counters and one valid native provider stack identify these costs:

1. **Frequent durable journal flushes.** `JournalDeltaStore.DurableFlush` calls `journal.Flush(true)`. The final counter, including normal Stop, is **284,804 flushes / 771.5042324s**. Each of the **98,178 resize commits** owns a flush; **98,178 write commits** use **91,784 flushes**, and **98,178 metadata commits** use **93,338 flushes**. Existing bounded batches therefore share relatively few of these file-callback ACKs. The stack shows `FlushToDisk → FlushPendingWrites → WriteBatch`, with other WinFsp callbacks waiting on monitors. This identifies durability latency/admission as a primary measured cost; removing an ACK is not a valid fix.
2. **Index mutation and concurrent maintenance amplification.** Resize `Apply` accumulates **176.5685430s**, versus **37.4178832s** for write and **26.5799147s** for metadata. It includes extent truncation, namespace/path-change and node work; these counters do not isolate each tree method. Another stack thread runs `MaintainLiveStorage → CollectLiveMetadataCore → Replay → Apply → SetPathChange → PagedMetadataStore.Set/ReadNodeCore`. Background packing adds **89,753 range relocations + 1,475 packing-state + 6 completion records = 91,234 internal durable commits**, in addition to **294,534 user-operation commits**. Range relocation itself already shares **23 flushes**, so attributing all extra cost to unbatched relocation flushes would be wrong. The journal appends **172,965,613 bytes** while changed payload is **3,141,696 bytes**; this is recorded journal traffic, not physical disk allocation or payload-bandwidth qualification.
3. **Lock waiting is a symptom, not an additive phase.** Journal gate wait totals **2,705.9978122s** across threads; mutation-gate wait is **301.9938545s**. They overlap other work and one another and cannot be added to wall time. The concurrent maintenance path and extra work are observed, but their exclusive elapsed share and ownership of each waiting callback lock have not been isolated.

The next minimal reproduction should separate actual native callback batch admission/ACK sharing, resize index updates and concurrent packing/shadow replay. Preserve durable completion, current ownership/native proofs and same-request recovery; do not merely enlarge a timeout, drop validation, or disable referenced-data protection. This measurement changes no product code and starts no further large step after the threshold. One later stack tool call exited zero with an empty output and supplies **no stack evidence**; an A response sampling attempt raced its successful exit and never attached. Both attempts remain recorded separately from the valid sample.

Exact fixture: `C:/Users/olive/AppData/Local/Temp/sscal3n-20261102-remeasure`, basis **`a1702646add993f92b08fb4e`**, A **`21d5b629c1f07ce9042dd2ee`** / `scal-a` / digest **`e505ea6676fe69d4bf741c72`**, B **`03e3b4cd9bc29da39605118b`** / `scal-b`. All three providers were authenticated-normal-Stopped at **21:52:48–51 +09:00**; the final PID/start audit confirms no matching CLI/provider or mount. Task/basis/private journals/request identities and evidence remain intact. The managed original is **stopped, not restored to ordinary NTFS**. No force kill, release, discard or restore occurred. Capture/apply/17GiB/Git/Plastic/Unity/full-gate and whole-lifecycle performance remain unqualified. [Machine report](../../reports/benchmarks/throughput-remeasure-20261102.json) binds policy, metrics, source/binary manifests, exact IDs, analysis and Stop audit; raw/public evidence and diagnostic scripts are under `artifacts/throughput-remeasure-20261102/`.

## 2026-11-02 corrected half-time continuation (RECHECK-02)

The user corrected the early stop below: **use half the historical elapsed time, not twice a new estimate.** The same frozen candidate, fixture, basis, A ID/key/digest were resumed; the existing basis was not prepared again. This explicit continuation supersedes the general estimate/tens-of-minutes stopping policy for this retry. **A still did not finish within the historical half-time limit.**

| Phase | Historical observation | Current observation |
| --- | ---: | --- |
| None basis preparation | 189.7678915s, completed | 98.7819962s, completed: 47.945885% shorter; 3.898050s above half |
| A preparation | 2,018.7203946s, last successful recovery call | **1,009.578185s accumulated**, still incomplete; cancelled at the **1,009.3601973s** half-time limit |
| B / writes / capture / original plan | Separate historical evidence | NotRun after A reached its limit |

A's total conservatively includes the earlier 120.0255507s call, 8.6153063s for a failed continuation wrapper attempt including its normal original restart/Stop, and 880.937328s for the final continuation including restart, dispatch and cancellation/drain return. Human discussion/pause time is excluded. The wrapper failure was a PowerShell `Cli`/`Clear-Item` alias collision before A invocation; it was renamed to `Invoke-HalfCli`, with failed evidence retained and no identity changed. The last actual CLI entry point took **875.6331665s**, with its remaining budget **875.6104484s**. No new estimate-based deadline or projected-time early stop was applied. The **0.217988s** accumulated overshoot is cancellation/return overhead, not a raised limit.

Actual stacks and file publication boundaries locate three successive expensive passes in the resumed A preparation:

1. Live-original metadata preflight: `PreflightFilesAsync → Scan.Metadata → Plain`; about **478.245s** from CLI launch to `files/owner.json` publication, including the connection boundary. The original provider reached **100,001 basis nodes including root**.
2. Immutable baseline: `EstablishBaselineAsync → Manifest → PagedSnapshotCatalog.Build`, with per-file `Plain`/hash work; another **175.555s** until `files/baseline.json` publication.
3. Live workspace content: `InspectWithProofAsync → Manifest(ScanWorkspace(...)) → PagedSnapshotCatalog.Build`; **221.941s** from that baseline publication until cancellation return, still incomplete. A had admitted **31,364 nodes**, recorded **2,006,912 base-read bytes**, **0 user commits**, and **449,721,841 bytes of index page files** at normal Stop. Provider samples also show paged node/path-component decoding during file information queries.

These intervals are artifact/stack-backed observations, **not exclusive function timers** or a completed-file percentage. The remaining bottleneck is the repeated original/basis/workspace inventory/content traversal plus per-file ancestor checks and paged metadata admission/lookup. Later result-capture optimizations do not remove these initial preparation paths. Investigate sharing exact current-source/basis proofs and bounded initial node admissions under the existing native/reader/ownership/change fences; do not skip live-source checks or treat a stale basis as current. No product change was made during this measurement.

At **19:10:02 +09:00** A returned `Cancelled`, still `creating`/`attach-intent`. Its persisted earlier mount observation is historical, not proof of continued liveness. Authenticated normal Stop receipts for A and original returned at **19:10:04 / 19:10:05**, both with process/mount absent; the final audit found no matching CLI/provider or either mount. The task, baseline, basis, deltas, request identities and prior evidence remain; no publication, release, discard or project restore occurred. All **32 frozen product file hashes** still match. This resumed comparison and the historical recovered call are not a fresh uninterrupted A benchmark; no completed A speedup, write/capture/plan improvement, full gate or SCAL completion is claimed. The [updated report](../../reports/benchmarks/scalable-storage-recheck-20261102.json) retains RECHECK-01 separately and binds policy, per-command limits, progress, stacks, accounting, failure and final Stop evidence under `artifacts/scalable-storage-recheck-20261102/continuation/`.

## 2026-11-02 initial retry: incorrectly early preparation stop (RECHECK-01)

**The historical write/capture/plan times have not been shown to halve.** Current `62c29ed` completed managed None basis preparation in **98.781996s**, but workspace A remained in its metadata-only source preflight until cooperative cancellation at **120.025551s**. The fixed estimate was 60s per preparation command, with cancellation at 120s. No B preparation, bulk write, capture or original plan/publication followed the deadline. This is an earlier preparation stop, **not** a measured failure of the 50% threshold for those unexecuted later stages.

The user requested a current retry and safe stopping/diagnosis if time did not halve. The original write/capture/plan half-time thresholds are **1,472.0838445 / 1,934.68682575 / 1,972.54659625s**; the last uses an interrupted historical lower bound. This initial attempt incorrectly applied the repository's shorter twice-estimate stop ahead of that task-specific instruction; the correction and continuation above own the current result. Historical Git/Plastic preparation includes recovery and is not compared with this fresh None preparation.

The fresh fixture has the same historical shape: **10 directories, 99,990 regular 32-byte files, 3,199,680 logical bytes and 16 initial variants**. Paths, byte formula, fixed creation/access/write times and Archive attributes follow the retained scenario. Current task v4/basis v3/overlay v3, `SPM2`, result-manifest v4 binaries were frozen after a successful Release build. The previous stopped fixtures use incompatible formats and were not resumed, migrated, edited or deleted. This is one bounded native None attempt; it is not a matched old/new or ABBA product benchmark.

| Current observation | Time / outcome |
| --- | --- |
| Managed `project prepare` | 98.781996s, succeeded; basis `9373cc048772da946046ebd1` |
| `workspace ensure`, A | 120.025551s, `Cancelled`; workspace `fdc0ca2d1c0e48d1e057ebb8`, key `scal-a`, phase `creating`, snapshot step `requested` |
| Preparation wrapper | 225.967358s including schema calls/dispatch/sampling; excludes subsequent normal Stop |
| Bulk writes / capture / original plan | NotRun; no 50% result can be calculated |

Actual CLI/provider stack samples and current code identify the remaining path:

1. `ManagedDirectoryBasis.RequireNoVcs` enumerates every entry and calls `WorkspaceStore.EnsurePlainPath`, repeating `File.GetAttributes` to the volume root for each file. This loop has **no cancellation token/check**; it finished before this attempt's deadline, so no cancellation overrun is attributed to it here. The next full pass, `SnapshotInputChecks.VerifyTree`, checks attributes, alternate streams, native handles and hardlink counts before manifest capture.
2. A's cancelled stage was `FileWorkspaceProvider.PreflightFilesAsync → Scan.Metadata → Plain` through the live managed-original mount. `Plain` repeats `Directory.Exists`/`File.Exists`/attributes up the ancestor chain; `Scan` separately requests attributes, length and mtime. The later capture optimizations do not remove this initial source preflight.
3. Provider samples show `GetSecurityByName → Stat → Lookup/AddBaseNode → PathComponents.Intern → PagedNodeMap.Save`, and path-component resolution on `GetFileInfo`. Merely inspecting untouched basis files admits nodes and updates paged metadata. At normal Stop, **25,459 indexed nodes** had accumulated **357,329,537 bytes across six index page files**. Payload/base-read bytes, user commit count and durable sequence were all **0**. This places substantial work in read admission/indexing before user edits, rather than payload copying. Indexed nodes are not an exact completed-file percentage; page-file lengths are not allocated disk bytes or a necessary minimum.

A read-only 256-file probe on the stopped physical basis called the actual current methods twice in reversed order. `EnsurePlainPath` took **0.174140 / 0.168443s**, making **3,328 ancestor attribute queries per observation** at that deeper basis path. `SnapshotInputChecks.Verify` took **0.049589 / 0.040483s**; one leaf-attribute query took **0.023302 / 0.019957s**. These measurements isolate repeated admission work; they are not exclusive timers for the full preparation, mounted timings or a 100k extrapolation. The provider's only journal flush was normal close (**0.001938s**); these counters do not support flush waiting as the explanation for the cancelled 120s preflight. Lock counters, metadata cache gauges and stack samples are not a wall-time partition or RSS measurement.

Next investigation targets are scope-bound **current-source** ancestor/metadata proofs and bounded unchanged-basis node admission/index construction. Preserve original-change detection, native/reparse/ownership fences and durable publication boundaries; substituting a stale immutable view or dropping checks is not a fix. Cancellation inside `RequireNoVcs` is a separate responsiveness issue. No product code was changed in this retry, and no new large stage was admitted after cancellation.

Execution limits/incidents are retained: total work estimate 25min/stop 50min; input seeding 30s/60s; preparation phase 180s/360s and individual preparation commands 60s/120s. The initial parallel build failed without diagnostic errors in 3.74s; serial Release succeeded with zero warnings/errors in **93.17s**, missing its 30s/60s build stop. This was a procedure failure. Initial test-input seeding safely stopped at **60.004764s / 74,469 files**. A separate 256-file ABBA then verified identical bytes/times/attributes with one open handle and a combined native metadata update (**0.181619→0.101966s mean per block**); only then did the same partial input finish its remaining 25,521 files in **10.227985s**. That is a setup-tool correction, not Splinter throughput improvement; original estimates were not raised.

The exact-owner authenticated normal Stop returned at **18:17:21 +09:00** with `processAlive=false`, `mountExists=false`; the later process audit found no matching provider/CLI. Task, basis, index, request key/digest and all old evidence remain. The new fixture's managed original is **stopped, not restored**; there was no forced kill, release, discard or original publication. No Git/Plastic retry, 17GiB, Unity, full gate or large-performance qualification is added. [Machine report](../../reports/benchmarks/scalable-storage-recheck-20261102.json) binds frozen binary/source hashes, exact fixture/identity, timings, stop receipt and limits; raw scripts, stacks and observations are under `artifacts/scalable-storage-recheck-20261102/`.

## Input and timing boundaries

Each fixed Git/None/Plastic input contains 10 directories and 99,990 regular 32-byte files: 100,000 entries, 3,199,680 aggregate file bytes, and 16 initial payload variants. The basis therefore has only **512 unique logical payload bytes**. This intentionally stresses file metadata, proof and ownership work; it does not qualify mostly unique large assets or payload bandwidth.

All timestamps below are 2026-11-01. Artifact timestamps locate observational intervals, not exclusive function timers. Recovered calls, subphase observations and overlapping process samples must not be added into one lifecycle total. Historical RI/GCO/MX timings do not provide a matched baseline for this candidate.

| ID | Existing observation | Seconds and outcome |
| --- | --- | --- |
| OBS-N1 | None: eight drained workers write all 99,990 files and set fixed modification times | 2,944.167689; 3,199,680 bytes written |
| OBS-N2 | None: first `capture-1` | 3,869.3736515; immutable result `60864f835b308dccc6c2ec96`, fixed source sequence 299,970 |
| OBS-N3 | None: first read-only `integrate --plan` | 3,945.0931925 before explicit cancellation at 15:36:54 +09:00; no completed plan response or original publication |
| OBS-G1 | Git: same B preparation after the record-timer correction | 2,476.9713499; recovered functional preparation succeeded |
| OBS-G2 | Git: final canonical B check | Whole command 766.1348281; its incremental source inspection 760.2953989, 99,990 files reused, zero payload/Git-hash input |
| OBS-G3 | Git: actual fixed-capture CLI-loss recovery | 7,221.1475198; same request/root/digest recovered, independent result and later edit preserved |
| OBS-P1 | Plastic: A same-key preparation including recovery/revalidation | 10,488.1015401; succeeded after revalidating 58,332 previously admitted nodes; not a fresh nominal trial |
| OBS-P2 | Plastic: saved READY through A response | First READY observation 14:37:42 +09:00; process return about 15:20:02 +09:00, roughly 42 minutes. This is not an exclusive timer for metadata revalidation. |

Raw sources: None [writes](C:/Users/olive/AppData/Local/Temp/sscal1n/evidence/bulk-edit-metrics.json), [capture](C:/Users/olive/AppData/Local/Temp/sscal1n/evidence/capture-1.process-metrics.json), [plan](C:/Users/olive/AppData/Local/Temp/sscal1n/evidence/apply-1-plan.process-metrics.json); Git [B preparation](C:/Users/olive/AppData/Local/Temp/sscal1g/evidence/ensure-b-initial-r18-git.process-metrics.json), [canonical command](C:/Users/olive/AppData/Local/Temp/sscal1g/evidence/ensure-b.process-metrics.json), [inspection records](C:/Users/olive/AppData/Local/Temp/sscal1g/h/records/b87030261d7b7141d5bc66f2/source-inspections.jsonl), [recovery](C:/Users/olive/AppData/Local/Temp/sscal1g/evidence/native-cli-loss-recovered-continuation-metrics.json); Plastic [A preparation](C:/Users/olive/AppData/Local/Temp/sscal1p/evidence/ensure-a-same-request-resumed-r19.process-metrics.json) and [slow-phase handoff](C:/Users/olive/AppData/Local/Temp/sscal1p/evidence/slow-phase-handoff-r19.json). These local raw files are machine-specific; the checked-in machine report retains their exact paths.

## Where the long capture intervals occurred

**None, OBS-N2**, +09:00:

| Existing boundary | Time |
| --- | --- |
| CLI process start | 13:26:39.6417783 |
| Fixed capture control | 13:26:40.4788448 |
| Generation basis header | 13:27:46.6939741 |
| Preserved-range/adoption map, 99,990 rows | 13:56:36.4086447 |
| Generation header | 13:57:01.4549226 |
| Workspace result state | 13:57:57.7433953 |
| Source materialized marker | 14:30:06.1286749 |
| Response file | 14:31:09.1516585 |

The measured store mutation pause was 0.0833245 seconds and callback pause 0.0862686 seconds; they overlap. The hour-long capture occurred outside that short freeze. The workspace-state-to-materialization observation spans about 1,928 seconds, but it is not solely a flush timer. See the [None handoff](../../artifacts/scalable-storage-tests/native-none-slowpoint-handoff-20261101.md) and [materialization observation](C:/Users/olive/AppData/Local/Temp/sscal1n/evidence/capture-1-source-materialized-observation.json).

**Git, OBS-G3**, UTC:

| Existing boundary | Time |
| --- | --- |
| Recovery CLI start | 04:25:29.8438305Z |
| Generation basis header | 05:04:34.2288105Z |
| Owned result-sort output observed gone | 06:08:09.2391176Z |
| Result / generation receipt | 06:24:28.1574743Z / 06:24:28.4375879Z |
| Recovery response | 06:25:51.078383Z |
| Wrapper confirms fixed bytes, retry and later edit | 06:26:30.6125413Z |

The sort observation is not an instrumented sort-end boundary. Store/callback pauses were 0.1068701 / 0.1104373 seconds, also overlapping. The actual CLI-loss case passed: one exact actor interruption, the original request/root/digest, independently retained fixed bytes, a completed-request retry and the later workspace edit. A harness alias-binding failure occurred before the recovery call and was retained; the actor or later write was not repeated. [Completion proof](C:/Users/olive/AppData/Local/Temp/sscal1g/evidence/native-cli-loss-complete.json) · [owned-output progress](C:/Users/olive/AppData/Local/Temp/sscal1g/evidence/native-cli-loss-owned-artifact-progress-2.json).

## Confirmed defects already corrected

These corrections have focused qualification in the [integrated report](../../reports/benchmarks/scalable-storage-integrated-20261101.json); they do not prove whole-lifecycle performance preservation.

| ID | Confirmed cause and bounded correction |
| --- | --- |
| FIX-01 | R16-cache: `Dictionary.First` did not provide recency when removed slots were reused; eviction also scanned the global metadata cache. Explicit 16-handle LRU and direct scope/container invalidation replace that work. A held directory cursor now defers live GC before an expensive copy/replay attempt, retaining the final reader guard and same intent. |
| FIX-02 | R19 Plastic: the actual R16 managed stack placed repeated server heartbeats in full manifest/catalog verification. An already admitted immutable reader now holds exact Native identity/path/length and write/delete fences; scoped heartbeats inspect physical bindings, while ordinary observation and full `VerifyReadBoundary` remain. |
| FIX-03 | R18 Git: a clean, data-scaled status walk can produce no output for more than 30 seconds. Its record stream incorrectly inherited the fixed-object blocked-read timer. Only that inappropriate record timer was removed; cancellation, owned-child/stderr drain, record validation and fixed-object reader deadlines remain. |
| FIX-04 | R19 GC recovery: startup segment retirement changed the segment catalog before a pending live repack resumed. The same pending repack now recovers first. Reconciliation permits only proven absent empty normal headers after all other roots/counters/ACK seals match; nonempty removal or other proof mismatch still refuses. An actual killed-child case and the same native request recovered. |

For FIX-02, the same existing 100k snapshot measured full observation 5.5830412 seconds, full scope admission 5.4950772 seconds, **100 scoped observations together 0.9958472 seconds**, full boundary 4.6148969 seconds, and later full observation 5.0921995 seconds. Full-manifest-validation counts were 0 → 1 → 2 → 2 → 3 → 4. Eight fences bound 21,390,437 **logical metadata bytes**, with zero fence handles after disposal. This is one [component probe](../../artifacts/scalable-storage-tests/native-plastic/r19-same-snapshot-scope-component.json), not physical allocation, server throughput, isolated RAM or a whole-lifecycle gain.

## Remaining work found in code and existing counters

| ID | Evidence level and remaining cost |
| --- | --- |
| COST-01 | **Code-confirmed:** first `FileWorkspaceProvider.PreserveGenerationAsync` scans the basis when its header is absent and scans the complete fixed result. `ScanTree` streams and hashes every regular file. Later fixed basis headers avoid that first basis rebuild, but complete result enumeration/hash work remains. The Git artifact intervals locate this path; they do not apportion its CPU or I/O. |
| COST-02 | **Code-confirmed; timing inferred:** `FileInspectionProof.RevalidateAsync` performs a full metadata-only workspace scan and disk-index root comparison for the editorless response. This is a plausible major part of Plastic's READY-to-return interval, not a measured exclusive attribution. Git's zero-payload incremental inspection still took 760.2953989 seconds, demonstrating substantial non-payload cost. |
| COST-03 | **Code-confirmed; exact plan attribution unknown:** original planning verifies the retained generation, builds basis/result/selection indexes, acquires source protection, observes the original namespace and reads selected target versions. The None plan was canceled before publishing a plan; its elapsed time is a lower bound on completion time, not a failed file publication. |
| COST-04 | **Counted and code-confirmed:** range adoption validates frozen bytes against canonical content and durably commits matching live ranges. None recorded 99,990 `adopt-range` commits, 361.542 cumulative operation seconds and 282.786 flush seconds. These counters cannot explain the entire roughly 1,928-second backend observation interval. |
| COST-05 | **Static candidate only:** paged Git inspection uses 256-file summary batches. After restart, the optional initial-content proof may be unavailable, yet a null summary does not stop later batch requests. A full 99,990-file pass could therefore issue about 391 summary RPCs before/alongside fallback reads. This was not counted or timed in the native run, and no capability/negative-result optimization was implemented. |

Code: [generation scan/preservation](../../src/Splinter.SourceControl.Files/FileSourceGenerations.cs), [response proof](../../src/Splinter.SourceControl.Files/FileInspectionProof.cs), [original planning](../../src/Splinter.SourceControl.Files/FileOriginalApply.cs), [range adoption](../../src/Splinter.Runtime/JournalRangeAdoption.cs), [paged Git inspection](../../src/Splinter.SourceControl.Git/GitPagedSourceFingerprint.cs), [optional summary](../../src/Splinter.Runtime/JournalInitialContent.cs).

None's persisted counters also contain 99,990 commits each for write, resize and metadata, 91,627 packing-state commits, 91,571 range relocations, seven completed packing jobs, 583,167 total commits and 573,403 flushes. Operation and flush counters are cumulative and overlap other work; they are not disjoint wall-time components or counts of automatically unnecessary operations.

The 32MiB page-cache peak is not whole metadata memory. During None capture, sampled peaks were 156,463,104 CLI private bytes, 508,272,640 tracked-process private bytes and 401,986 tracked handles; `samplingErrors=[]`. These 100ms samples are lower bounds and include the tracked process group. Native ownership/whole-target protection can scale with file count outside the cache budget.

After None materialization, source-private payload was zero and the journal was 671 bytes while the immutable result remained retained. This establishes adoption/compaction of that source, not zero total-home allocation. No new post-bulk allocation scan was run after the user reduced verification.

## Storage lifetime is a separate cost

Managed None preparation moves the original basis and builds indexes without copying its payload. The original must later become ordinary mutable NTFS again, so it cannot replace independently immutable result storage. The first independent retention can copy each unique basis version into canonical CAS; A/B and later results share those objects. Basis/CAS coexistence is additional lifetime-separation overhead, not proof that all duplication was removed or all those bytes are unavoidable.

Count preexisting basis Native identities separately from new CAS identities and metadata/controls. The 512-byte unique initial payload in this fixture makes it unsuitable for measuring retention of mostly unique large assets; new edited versions can require additional immutable bytes. No phase allocation or percentage saving is inferred from logical payload size.

## Prioritized follow-up questions

These are unimplemented questions, **not new execution authorization**:

1. **Q-01:** Can fixed basis/result metadata and verified canonical identities eliminate duplicate scans or hashes under the same reader lease and generation proof, while retaining full publication-boundary validation?
2. **Q-02:** Can ordered, bounded adoption/packing batches reduce per-row metadata/flush work with exact durable recovery and unchanged accepted-write ACK semantics?
3. **Q-03:** Which full original-observation/index passes are repeated, and can streamed comparisons/independent bounded I/O remove repetition while retaining whole-target/ancestor locks, conditional publication and companion/ownership checks?
4. **Q-04:** Can the optional initial-summary capability be rejected once per exact provider lifetime after restart, rather than once per batch, without treating unavailable proof as verified content?
5. **Q-05:** Which active-container proof/page operations dominate metadata-only work after the qualified cache fixes? A future small targeted attribution must preserve Native replacement detection, immutable-reader fences and full hash proof; no cache-only availability shortcut is justified here.

## User-requested stopping point

The two expensive read-only CLIs were terminated by exact PID/start identity: None plan and Plastic B ensure. Seven owned providers received one authenticated normal Stop each: Git A/B, None A/B/original, Plastic A/B. All reported process/mount absent. Tasks, bases, private deltas, immutable results, request identities and owned evidence remain; normal Stop is not release, discard or managed-original restoration.

Git retained the passed CLI-loss result and later edit; its 100k bulk body was never started. None retained its one capture with zero original applies; the empty `apply-1.json` is an explicit harness barrier, not a CLI response. Plastic A preparation passed and B was canceled after 427 admitted nodes, source ACK sequence zero and no results.

No further 100k phase, selected/publication/query matrix, real 17GiB case, matched ABBA, allocation sweep or latest whole build gate was launched after steering. The three small actual Unity lifecycles and separate cleanup are separate completed evidence; they do not fill these missing large-input or performance gates.

Closeout: [Git](C:/Users/olive/AppData/Local/Temp/sscal1g/evidence/minimal-closeout-normal-stop-r19.json), [None](C:/Users/olive/AppData/Local/Temp/sscal1n/evidence/user-closeout-stopped-preserved.json), [Plastic](C:/Users/olive/AppData/Local/Temp/sscal1p/evidence/minimum-closeout-r19.json). No remaining experiment or fixture deletion is automatically authorized by this document.

