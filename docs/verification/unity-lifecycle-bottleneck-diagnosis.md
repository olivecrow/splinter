# Actual Unity lifecycle bottleneck diagnosis

[한국어](../ko/verification/unity-lifecycle-bottleneck-diagnosis.md) · [Follow-up scope](../plans/lifecycle-bottlenecks.md) · [BO component verification](lifecycle-bottlenecks.md)

**BD-01, 2026-10-27:** one actual GUI Unity workspace completed Library preparation, creation, initial loaded-code readiness, one C# edit and confirmed compilation, asset/render inspection, normal stop, explicit discard and original Library restoration in **391.3053613s**. This is a completed diagnostic with capture/observation overhead included, not a before/after improvement or a shared-Editor comparison.

The [machine-readable report](../../reports/benchmarks/unity-lifecycle-bottleneck-diagnosis-20261027.json) retains the fixed input, measured runtime, stage boundaries, attribution and closure. Local raw evidence is `artifacts/unity-lifecycle-followup-20261027/`; the successful run is `01-before`. Its name denotes the planned comparison order, not a completed second variant. The frozen measured runtime is **`bea4acdb95f3ea2a914587c7f5e5293b57940e7d`**, which already includes BO-01–05. This is not a comparison against pre-BO code.

## Fixed input and stopwatch

- Windows/NTFS, Unity **6000.3.22f1**, product-owned GUI Editor and current bounded journal. One workspace, not two A/B Editors or the sum of their independent times.
- Experiment source commit **`20967120692904985ea6320ce01f93239ba136ff`**: **1,602 source files / 2,804,772,190 bytes** and the whole measured Library: **42,419 files / 3,142,155,813 bytes**. Input and restoration checks cover bytes, modification times and attributes; no PID/cache file is excluded. The full manifest was fixed after the authorized original-server pause, because that server had appended its log after the first freeze. That rejected preflight created no product workspace or Editor.
- Fixture warmup is outside timing. It removes the earlier fixture's private package-callback hook; normal installed external-package callbacks remain. Initial loaded runtime MVID **`030f3712-6d1f-4053-9ac9-990717142fd8`** matches the warmed input, with values `(4, 6, 13)`, ready state and no compilation errors. The one-line change produces `(5, 6, 13)`, a successful identified refresh response and new MVID **`2bf06e82-336b-4b98-8ffe-cc11b8d78c82`**. An unsolicited observation's `success=false` is not a failed refresh response.
- One continuous monotonic clock begins at `library prepare` and ends after `library restore`. No interval within that clock is excluded. Full input/final closure proofs and the separately authorized external-server pause/recovery are outside it. W1/G1 diagnostics, two bounded captures and the server observation remain inside; subtracting their caller durations would not establish clean-run timing because they can perturb caches and overlap other work.
- Workspace identity is `426d9275b891ce7a1c64a942`, request digest `34b4d5fa83e03ed811c1de53`; the owned Editor is PID 21864 with its recorded start identity. Captures, loaded-code observations and stop receipts match that returned project and process. The endpoint is **discard**, not checkpoint/release or original application.

| Measured stage | Seconds |
| --- | ---: |
| Library preparation | 37.6204517 |
| Workspace ensure | 59.6685310 |
| Before-start diagnostic capture | 12.6682934 |
| Start invocation through initial qualified readiness | 162.1235357 |
| Live diagnostic capture | 15.4385013 |
| External-server observation | 1.6110659 |
| Code edit/explicit refresh through compiled readiness | 26.8958510 |
| Asset/scene/render inspection | 1.8176238 |
| Product normal-stop call | 39.0148359 |
| Explicit discard after stop | 10.0517996 |
| Original Library restoration | 22.9976892 |
| **Continuous lifecycle, including remaining dispatch/poll gaps** | **391.3053613** |

`unity start` acceptance took 3.9315346s inside the 162.1235357s readiness interval. It is neither a readiness result nor an additional stage. The table uses controller boundaries; asynchronous provider counter windows differ and can overlap. Exact timestamps are retained in the report and raw receipts.

## Durable writes and callback admission

W1's `01-before/provider-analysis.json` and `.md` distinguish callback, batch, store-call and instrumented journal boundaries. The final mounted observation is after normal Editor exit and before discard. It records **25,798 write callbacks / 25,607 batches**; measured journal durable-flush time is **111.6086225s**, of which **96.7305155s** lies inside observed write-store intervals. Live-operation attribution accounts for **107.7683288s**; the remaining **30 flush calls / 3.8402937s** are consistent with bounded-checkpoint flush sites outside that attribution. These counters overlap: they are not separate lifecycle stages, OS disk-service totals or additive removable seconds. Serialization/hash/replay outside those instrumented scopes remains incompletely attributed.

| Provider phase bracket | Callbacks | Batches | Singleton batches | W1 flush s | All instrumented journal flush s |
| --- | ---: | ---: | ---: | ---: | ---: |
| Startup | 13,998 | 13,884 | 99.388% | 72.381 | 86.454 |
| Normal stop | 10,311 | 10,310 | 99.990% | 21.311 | 21.651 |

At stop, 594 of 597 observable consecutive tail pairs enter after the prior callback returns. This is a biased sample, not all 10,311 calls, but provides little basis for more write workers or an artificial batching delay. Before callback admission, the observations cannot distinguish a synchronous Unity caller from WinFsp/kernel serialization. Existing batching cannot combine a dependent next request before that caller receives its required durable ACK. No early ACK, omitted flush, new journal format or retired Bee storage backend was adopted.

Startup Close/Open waits sum to **41.6159745s / 11.4125562s**, but they overlap across callers. One 3.1822556s publication interval contains 10.5857066 summed Close-wait seconds, directly showing why these totals cannot be added as elapsed savings. The `01-before/open-close-review.md` code review establishes:

- Non-final Close already performs only context bookkeeping. A separate context lock with a pending-final recheck is a narrow possible change, but its frequency and critical-path cost are unmeasured.
- Final linked Close retains local writer-health checks; final unlinked/replaced Close must reclaim only after the last reader. Removing the outer gate can merely move waiting to the journal gate. Skipping the release changes health/lifetime protection.
- Open's namespace lookup and retention must remain atomic against delete/replace. A simple outer-lock removal creates a stale-identity race and still encounters the journal gate in `Stat`.

W1 does not classify these Close cases or retain their paths/stacks. Their totals are not proof that Unity's initializer scope spent that time in Close. No asynchronous close queue, broader lock removal or revived read-during-flush prototype was implemented.

## Git preparation: retention, not decoder wait

The succeeded attach attempt is `738316b3652f4b4fb1785859b48b73ac`. Git attach occupies **33.7527360s inside ensure**. First-index warm takes **25.9653962s** and mandatory index refresh **32.4726862s**, overlapping in a **32.4884104s** window. Refresh ends **6.5230142s** after warm. These intervals must not be added to ensure or to one another.

At the first idle publication after attach, full proof decoded **2,787,029,670 bytes**, already-proved reads decoded **2,913,936,481 bytes**, and **2,027,356,160 bytes** of retained read-ahead were evicted before first use. Completed full proofs number 1,563, matching the source manifest's unique-content count. That count and near-equal content size support payload eviction/re-decoding rather than broad repeated whole proofs; the manifest's unique bytes differ by 2,894, so this is not a per-file identity proof.

`g1-preparation-analysis.json` and `.md` trace the calls and largest files. Warm's first-byte reads perform mandatory whole-blob proofs and put forward data in the same 256MiB/4,096-entry LRU as index demand. Consumed chunks are promoted to MRU, so they can evict still-unread future chunks. A proved payload miss sequentially decodes the blob/prefix again. Small files also consume entry slots. The inferred two-scan reuse distance can approach twice the warm lead, but the sampled byte-difference proxy is not an exact access trace.

Repeated decoding is **51.1% of decoded bytes**, not 51.1% of time. If warm timing and other stages remain fixed, shortening only refresh can save at most the **6.5230142s** tail. A cache policy can also change warm timing, so this is a conditional bound. The entire 32.4884104s window is a loose component ceiling, not an achievable saving while full proof remains.

The separately implemented cache-hit decoder-wait bypass passed **55 focused tests / 1 conditional skip** and a full gate of **1,731 passed / 0 failed / 30 conditional skips**. However, actual preparation had no decoder wait and the whole measured task had only **9 waits / 0.000837s**. It did not meet the requested large-gain criterion and was removed from product. Its source, patch, tests and frozen candidate remain under `rejected-cache-candidate/` and the candidate evidence files. **No actual candidate GUI run occurred; these tests establish safety behavior, not measured speedup.**

At the end of BD-01, a larger cache or consumption-aware retention remained a design candidate only. A concrete future probe is 1.5GiB plus 32,768 entries while preserving two readers and the 128MiB window; bytes alone cannot overcome the 4,096-entry cap. It adds up to 1.25GiB retained payload per provider and requires whole-process/GC peak measurement. Exactly 2GiB requires wider accounting. All input files fit 128MiB; shrinking the window to 64MiB would stop initially retaining 104,267,732 tail bytes from two files. Neither change was implemented during BD-01. The subsequent [GR-01–03](git-payload-retention.md) capacity experiment observed only a small native creation saving and removed its candidate; policy changes remain unmeasured.

## Unity startup and retained captures

U1's `01-before/u1-initial-analysis.json` and `.md` correlate completed before/live capture manifests with the exact workspace/provider and live Editor launch. They contain **376 artifacts / 61,313,054 bytes** before and **382 / 62,428,564 bytes** live. Captures are bounded, per-file observations, not atomic Library snapshots. The live Editor log is a prefix of the final log.

Initial Bee work regenerates an unavailable first-start DAG, runs the frontend, then completes a 32.85s backend with 292 updated/956 evaluated items. The action inventory includes one `Csc` for `Splinter.Workspace.Editor.dll` and 68 ILPP actions. The bridge compiler action spans 8.739s in the trace, while the runtime assembly MVID remains unchanged. A precompiled bridge might avoid its own compilation, but this does not prove removal of ILPP, graph regeneration or reload. The current first-start DAG invalidation was retained.

| Unity-reported scope | Seconds | Boundary |
| --- | ---: | --- |
| Initial refresh | 139.948 | Enclosing startup work |
| Script compilation | 50.711 | Within refresh |
| Main domain reload | 65.122 | Nested startup work |
| LoadAssemblies | 13.902 | Within reload |
| ProcessInitializeOnLoadAttributes | 49.364 | Nested initializer aggregate |

These scopes overlap; neither they nor W1 flush totals are additive. The 49.364s initializer aggregate is the largest unassigned Editor scope, not measured CPU time. Captured buildprogram/Bee traces do not identify its individual classes or give the UTC anchor needed to assign summed Close waits to it.

The observed MCP HTTP auto-start stack runs through delayed Editor updates **after** the main reload/initial refresh summary. It cannot explain that earlier initializer aggregate by itself; other package initialization remains unknown. `StartupConfigRewrite refreshed 9 client config(s)` counts in-memory status transitions, not nine proven writes. A safe metadata/endpoint audit and installed early-return code support no-op checks; no trial-time global client-config write was observed, but no independent pre-run fingerprints existed to prove every historical write absent. Product code neither patches nor manages that external behavior.

Supported next attribution work is a bounded comparison of already captured DAG/inputdata/response-file edges and, if separately scoped, supported external Editor profiling for per-class initializers. Changing stale endpoints or private callbacks, reusing unproven process-bound graphs, or assuming a DLL bridge removes all startup work is not supported by this evidence.

## Closure and qualification

`01-before/closure.json` confirms normal Editor exit code 0, explicit discard, all owned providers exited, source/config/ref/worktree preservation, and exact whole measured Library restoration to ordinary NTFS. Asset inspection confirmed model/texture/scene presence and produced the expected render hash `df94cefbc5f0d2fa6b46b878badb8848bc0221d66130778347349725a47b632d`. This is selected asset/render evidence, not Play/Test/player-build qualification.

After the timed run, the original sibling project returned to clean `development` at **`b09fa58e9d40a560d4fe1af7a213f8ac23e8e3b1`**. The original probe bytes/mtime, other source files, configuration and worktree list were restored; pre-existing refs were preserved and the fixture branch retained. The Library is the exact warmed measured input, **not the pre-warm cache**; the restored original probe can compile on the next Editor use.

The user separately authorized pausing the original MCP server and isolating/restoring exactly its two captured global handshake values for this experiment. Package code and other settings were preserved. After owned workspace closure and ordinary Library restoration, the original captured launch arguments/runtime hashes were checked, the server was restarted, listener/health and preference restoration were verified, and the server naturally updated its PID file. That external recovery is outside the 391.3053613s interval and is not a product server-management capability. The final ordinary Library can subsequently change through normal server activity.

BD-01 establishes a completed diagnostic and identifies remaining costs. It establishes **no before/after improvement, BO whole-lifecycle percentage, shared-Editor comparison, original-apply timing, new Play/Test/build support, general cache benefit, peak RAM/storage reduction or power-loss behavior**. Further optimization requires a concrete safe design and measured critical-path benefit; the large cumulative counters alone do not satisfy that requirement.
