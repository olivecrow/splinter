# Actual Unity parallel workspace timing

On this small URP project, the current A/B run took **528.55s**, versus **441.09s** for the mean of two public A/B runs. That is **19.8% more time**, and misses the below-half target of 220.54s. The current result is one clean observation out of three attempts, not a repeat mean. The other two current attempts both passed Editor validation and then required recovery during workspace release.

## Matched input and accounting

The public GitHub `development` tip was [`c4fa506`](https://github.com/olivecrow/splinter/commit/c4fa5063964e7b8456de4ea7735c042fb9af8930) at the final check on 2026-11-05. The current local runtime was `298f30ed033c3e7251e49ca174bd228ed77117ef`. All ten runtime assembly hashes were checked for every accepted case. The same installed Unity **6000.3.27f2** and URP **17.3.0** used fixture `e99b0462816ec4f108fc31e4e20ea6dbceaf4694`: 75 source files and 20,435 Library files containing 1,599,268,684 bytes. The host was Windows 11/NTFS, Ryzen 5 5600X (6 cores/12 logical processors), with about 64 GiB RAM. Fresh copies were fully audited; this is a warm-input comparison, without a controlled foreground/background system load.

- **Shared Editor baseline:** two independent, editorless Git worktrees; one owned Editor validates A and then B. The two source results are kept in independent Git refs, worktrees are reclaimed, and the original is restored. The final exact byte reset copies the frozen Library and preserves the changed one. It is a benchmark obligation; an ordinary shared-Editor workflow can keep its usable generated Library.
- **Public and current Splinter A/B:** concurrent workspace creation and result collection, two owned Editors, ordered startup and source publication/refresh, separate scene saves, ordered Play transitions with real simultaneous Play, then normal B-to-A shutdown. After B exits, A must still respond. Both runtimes use their own native capture, release and Library restore contracts.
- **Core** includes preparation, Editor work and shutdown, result preservation, resource reclamation and exact Library reset. **With auxiliary checks** adds the selected Editor Mono DLL reads and independent result readback. Fresh fixture construction, identity checks and the final out-of-band original audit are reported separately and excluded from both totals.

| Condition | Clean attempts | Core samples (s) | Core mean or single observation (s) | With auxiliary checks (s) |
| --- | ---: | --- | ---: | ---: |
| Public method, one shared Editor | 2/2 | 261.58, 84.07 | 172.82 | 177.87 |
| Public Splinter A/B | 2/2 | 419.28, 462.89 | 441.09 | 458.64 |
| Current Splinter A/B | 1/3 | 528.55 | 528.55 | 547.26 |

The shared core mean before the test-only exact reset is **154.36s**. The current observation is 3.06 times the strict shared mean, or 3.42 times that pre-reset mean. The slow shared observation is included: B took 130.81s from the pre-write snapshot to verified refresh, with 12.48 root Editor CPU-seconds and a 1.09s Bee invocation. The rest of that interval is not identified. The wide shared range and the single clean current observation limit generalization.

## Where the time went

| Stage (wall seconds) | Shared mean | Public A/B mean | Current A/B observation |
| --- | ---: | ---: | ---: |
| Library preparation | 0.00 | 38.98 | 28.72 |
| Workspace creation and source read | 2.75 | 47.57 | 25.21 |
| Editor startup | 33.90 | 130.65 | 139.10 |
| Edit, compile, scene, Play and source validation | 107.84 | 52.43 | 77.37 |
| Normal Editor stop | 7.44 | 98.05 | 183.78 |
| Result preservation and reclamation | 2.41 | 44.28 | 46.13 |
| Final exact reset or Library restore | 18.46 | 29.10 | 28.19 |
| Selected Editor DLL preflight (auxiliary) | 4.65 | 17.27 | 16.90 |
| Independent result readback (auxiliary) | 0.39 | 0.29 | 1.81 |
| Fresh fixture and full input audit (excluded) | 58.63 | 34.96 | 30.41 |
| Final original file audit (excluded) | 29.55 | 4.55 | 4.72 |

The current preparation and initial source path saves **32.62s**. The largest offset is **85.73s** more normal shutdown time. From stop request to the zero-owned-process exit receipt, the pair takes **163.92s** current versus **83.89s** public; from that receipt to CLI response it takes **18.42s** versus **13.11s**. The two current attempts excluded later for release failures also completed normal stops in **186.10s** and **121.75s**. This places the largest observed cost before closure proof, without identifying a specific Unity save, import or worker wait inside it.

At startup, Unity reports **107.91s** of initial Asset Database refresh across the current pair, versus a public mean of **89.14s**. Time from launch command to native process creation falls from **7.52s** to **4.13s**. Source publication and verified compilation took **50.38s** versus a public mean of **26.24s**; actual Play verification was approximately 23s in both. Library restore improves by **0.91s** and does not explain the lifecycle gap. With the observed Editor costs held fixed, startup, validation and shutdown alone cost **400.24s** in the current run, above the 220.54s target. The next useful profile is the import/refresh and normal shutdown path, including WinFsp request counts and provider/compiler CPU.

Measured root Editor CPU is **602.80 CPU-s** current versus **375.14 CPU-s** public, and measured foreground CLI/tool CPU is **211.88** versus **97.27 CPU-s**. These exclude persistent providers, compiler children, controller work and system-wide CPU. They neither establish CPU saturation nor identify the cause of the extra Editor work.

## Reliability and recovery

Both excluded current attempts passed compile, scene isolation, simultaneous Play, correlated exits and B-to-A normal Editor stops. A released successfully. B published its fixed result before release refused a pending roll (`JournalSegmentCorrupt`, `m10-c2`) or a sharing violation on `segment-state.json` (`InputUnavailable`, `m10-c3`). One exact `final-b` retry per case recovered in **31.30s** and **53.15s** respectively. Both fixed B generations, independent source bytes, owned resource reclamation and full original restoration were verified. These are observed release failures with successful recovery, not clean timing samples or evidence of permanent payload corruption.

A **1.61s** scratch reproduction using the actual runtime publisher and control reader reproduced Windows sharing error 32 in both directions: a read handle that denies delete can block replacement, and an outstanding delete handle can block the current reader. After handles close, the published bytes read correctly. A scratch read with delete sharing still excluded writes. This identifies a possible acquisition window, not the historical writer or a qualified production change. Preserve the full journal seals and ownership proof when addressing this failure.

The probe acknowledges Play exit from its exact `EnteredEditMode` request and settled Edit state; exit can retain the domain ID. Changed code still requires a changed assembly MVID. A local valid Project browser folder avoids the observed missing-favorite dialog without deleting favorites. The owned Editor executable, PID and start time are checked natively; a receipt field is not substituted for the actual process image. The external whole-file audit retains every SHA-256 and inventory check; eight bounded readers reduced the fresh 512-file check from 3.00s to 0.56s and the stopped original's late audit completed in 37.15s within its existing 90s stop.

An earlier A-first shutdown cut the version-specific Licensing Client connection used by B. A small managed A/B reproduction in **158.83s** verified the same channel and that the client belonged to A's Windows job. Normal B exit left that exact A and client alive; then A stopped normally. The final procedure applies this dependency order to both binaries. See the [Unity guide](../alpha-workspaces.md#shared-preferences-and-external-validation) and [Windows job lifetime semantics](https://learn.microsoft.com/en-us/windows/win32/api/winnt/ns-winnt-jobobject_basic_limit_information). The earlier connection recovery required a manual Retry and is excluded. This procedure does not qualify arbitrary independent shutdowns.

## Verification boundary

Every accepted sample matched all 53 package DLLs per Editor with its Mono reader, proved the intended code values, changed assemblies, scene and texture, and observed at least 15 real Play frames with request-specific exit events. A/B snapshots confirmed overlapping Play. The 75 source hashes per result matched across all five accepted cases, and originals matched all source and Library bytes and the logical Git index/HEAD. Normal exit receipts and final case-root process checks show no owned Editor/provider left running. Existing consent fingerprints, entitlement identity and favorites matched; ordinary license byte renewal is recorded separately. No new Editor, profile override, terms acceptance or license activation was used for the measurements.

Product code and binaries were unchanged in this comparison. The preceding full runtime gate remains **2,629 discovered, 2,593 passed, 36 conditional skips, zero failures**, in 673.40s; it was not rerun for the measurement tooling. The earlier per-file handle evacuation was rejected at 4.20 times the root-move component time; that prototype is not used here. This limited input does not qualify cold or large projects, longer work sessions, general CPU/RAM or disk peaks, Unity builds, other VCS modes, uncoordinated Editors, or OS/power-loss recovery. The retained 100k/17GiB fixtures were not resumed.

Local evidence is in `artifacts/unity-triad-20261105/`: `comparison.json`, `phase-breakdown.json`, `plan.json`, `final-resource-proof.json`, individual cases, and the exact same-request recoveries. This supersedes the earlier matched timing for the current comparison; older results with other inputs or transition policies remain separate evidence.

## Optimization candidate disposition

**Cost-aware segment packing is applied** following the explicit maintenance-only selection. The write-admission, extent-batching and separate control-file sharing candidates remain excluded. The completed Unity lifecycle timing above is the current comparison point.

| Candidate | Observed result | Disposition |
| --- | --- | --- |
| Cost-aware normal-segment packing and deferred relocation planning | The earlier 512-range NTFS ABBA probe reduced mean maintenance, stopped reopen/byte verification and reclamation from 2.624s to 2.151s (18.0%). Initial writes through reclamation improved only 6.9%, from 6.015s to 5.599s. Relocations fell from 513 to 1; reopened digests and accepted seals matched. Slower final reclamation is included. That probe also contained the separate control-file sharing fix. | Applied without the control-file sharing change. Current rules are in the [storage guide](../scalable-storage.md). No new standalone or Unity throughput claim. |
| Atomic control-reader delete sharing | Four new native regressions covering this fix and packing admission passed. In-place writes remained excluded. | Preserved with the excluded integration candidate; complete regression was not obtained. |
| Extent mutation batching | Fewer metadata pages, but one matched write-apply sample increased from 4.86ms to 8.97ms. | Rejected; further candidate verification stopped after the synchronous write returned. |
| Write admission changes | The repaired fixture measured only 2.61% serial and 1.54% parallel write-time improvement; full parallel process time increased 3.69%. | Excluded: below the 10% target and slower over the full parallel process. |

The earlier combined candidate gate exceeded **673.40s** and was canceled without a final summary (last progress: 2,563 passed, 36 skipped, zero failures). It remains a time-budget failure, not evidence that a specific change caused the overrun. After maintenance-only reapplication, the incremental build passed in 29.42s with zero warnings/errors; a six-case local selection reached its 20s stop without a summary. Its runner exited, one scratch fixture was preserved and managed disposal remains unconfirmed. The post-integration complete gate is recorded separately below.

An earlier 18-case recovery/capture/scratch selection also reached its 30s stop without a final summary. The extent candidate's test-project build completed in 102.93s, exceeding its 60s stop before the overrun was observed; its subsequent focused tests were not run. These incomplete checks are not passes. Original candidate evidence is under `artifacts/parallel-perf-optimization-20261105/`.

## Corrected write verification

The local probe advertised a 4KiB basis file but capped reads at 32 bytes. Its repaired reader uses the same payload length as `Stat` and returns zero at/beyond EOF. Before timing, three passive handles verify the full 12,288 bytes, suffix reads and EOF. The controller uses frozen baseline/candidate assemblies, with only the Windows adapter differing, and preserves earlier evidence. Serial input is 96 files; parallel input is 256 files with eight writers. Each write carries 4KiB.

| Corrected ABBA condition | Baseline write mean | Candidate write mean | Write reduction | Baseline full process | Candidate full process |
| --- | ---: | ---: | ---: | ---: | ---: |
| Serial with three passive handles | 1.060064s | 1.032346s | 2.61% | 1.711741s | 1.628669s |
| Eight concurrent writers | 1.452956s | 1.430512s | 1.54% | 2.349854s | 2.436506s |

One preflight and eight comparison processes all closed normally, unmounted and reopened with zero callback errors. Checks cover acknowledged 4KiB payload bytes, timestamps and the accepted seal; final written-file length/EOF was not independently asserted. Candidate write admission stopped at the first baseline's write-time budget, with a separate 16s process bound. The full-process regression was observed after closure; the controller did not enforce the baseline's total process duration as an admission deadline. No further write candidate was promoted or measured after exclusion.

Repaired probe/controller and measurements are under `artifacts/parallel-perf-recheck-20261105/admission/`. The independent task preserved its result and released all mounts/providers. Its first release encountered `WorkspaceMaintenancePending`; resuming the exact returned maintenance request completed in 2.65s, and the same checkpoint request finished release in 7.75s. No state was removed to bypass recovery.

The maintenance-only post-integration gate, `pwsh ./scripts/build.ps1`, **passed in 631.17s**, within the existing 673.40s stop: **2,631 total, 2,595 passed, 36 conditional skips, zero failures**, with zero build warnings/errors. The final TRX confirms both new packing cases and all four selected force/recovery/scratch-pressure cases passed; this supplies complete coverage after the canceled local selection. No matching repository test/provider process remained. The gate used the default schedule, without large-catalog opt-in or new Unity lifecycle trials. Logs and the extracted six-case results are in `artifacts/parallel-perf-recheck-20261105/full-gate-summary.json` and `full-gate.log`.

## Follow-up qualification

The native mixed read/write probe confirmed reads overlapped the flush, but the candidate workload was 8.74% slower (1.150s to 1.251s). The controller closed admission at the 2.577s baseline full-process bound. All 512 admitted writes drained and the candidate exited normally in 2.820s with replay verified. The separate fixed-EOF probe reduced barrier time by 0.18% and full probe time by 1.78%. Both candidates were excluded.

The maintenance-only Unity trial `t11-b1` completed Editor validation and both normal Editor exits. A preserved its result, then release failed when the live seal encountered a pending segment roll. B released successfully. A and Library providers stopped normally. The separate same-request recovery verified both final source results, the original Git state and all 20,435 Library files, restored Library to an ordinary NTFS directory, and verified owned-resource absence. The recovery preserved the Unity identity and observed changed serialized license bytes. The failed trial and recovery provide phase and correctness evidence only. The trial controller now uses the live handle count for an authenticated stop and skips image and job queries for exited processes. Exact stop receipts and a short exited-process probe cover those corrections.

The live physical seal fix passed fourteen focused NTFS cases. The metadata collection case verified that the live seal waits on a published roll intent and returns the same accepted sequence, byte count and hash. The selection also verified interrupted-roll refusal and stopped replay. An 8 KiB Git/WinFsp smoke completed checkpoint and release in 12.924s and verified the provider receipt, retained bytes and EOF, and resource absence. The fix also completed the original `t11-b1` A checkpoint during same-request recovery. A separate isolated CLI workspace completed its first checkpoint and release in 30.815s after reconnecting to the updated provider. The live seal receipts matched, the provider, mount and delta were absent, and the retained result catalog matched all ten expected file hashes and lengths.

The earlier full gate stopped at 631.17s with 39 cases outstanding. Its two scratch fixtures remain preserved. The subsequent complete `pwsh ./scripts/build.ps1` gate finished in **674.52s** with **2,602 passed, 36 conditional skips and zero failures** across all 2,638 expected cases. All seven seal cases passed. The build had zero warnings and errors, and the gate exited normally with no remaining owned processes or new test fixture directories. This gate covered the live seal fix before the sealed-reader change.

Evidence is under `artifacts/parallel-total-20261105/` in `durability/`, `t11-b1/`, `t11-b1-recovery/`, `controller-readiness-after-t11-b1.json` and `reliability/`.

The single-Editor diagnostic `t12-d1` completed in 322.515s, with a first-attempt checkpoint and release in 29.979s. The retained source result, original source and Library restoration, and Editor/provider absence were verified. Shutdown completed 10,304 singleton write callbacks with 21.247s in write flushes. Of 1,042 distinct callbacks retained in the bounded rings, 1,039 targeted the Burst cache manifest. The rings overwrite older samples, and the opt-in counters can affect timing. This diagnostic is ineligible for whole-lifecycle comparison. Evidence is under `artifacts/parallel-total-20261105/t12-d1/`.

The isolated sealed-reader candidate retained up to four native read handles and passed all 28 focused cases. The hot-read ABBA probe reduced full-process time by 21.69% and native read bytes from 266.7 MB to 16.9 MB. The single-read probe reduced full-process time by 4.13% while timed reads took 0.845ms longer. The isolated workspace released with result `182d9c4dd99cc12f68ff8ca9` preserved. Probe evidence is under `artifacts/parallel-total-20261105/sealed-reader/`.

## Matched Unity lifecycle

Fresh control `t13-b1` and candidate `t13-c1` ran in that order in a clean window. The frozen control included the packing and live seal fixes. Only `Splinter.Runtime.dll` differed between the arms, and write diagnostics were disabled. The first candidate command stopped before fixture creation because the invocation manifest held the DLL provenance record. The corrected manifest used the frozen 45-file inventory, and the measured candidate used the completed control's core and pipeline times as its stop ceilings.

| Elapsed scope | Historical | Fresh control | Candidate | Reduction vs fresh control | Reduction vs historical |
| --- | ---: | ---: | ---: | ---: | ---: |
| Core lifecycle | 528.55s | 450.02s | 389.87s | 13.36% | 26.24% |
| Pipeline with package and independent result checks | 547.26s | 467.42s | 406.94s | 12.94% | 25.64% |
| Controller including fixture and original audit | | 511.54s | 442.66s | 13.47% | |

Both arms verified the same 75 source files and 20,435 Library files, and independently read back matching per-file result hashes and lengths for A and B. The two Editors passed compile, scene and Play isolation checks and exited normally in B→A order. Original source, Library and Git proof passed, and owned Editors, providers and mounts were absent. Both arms preserved the normal Unity 6000.3.27f2 identity and observed changed serialized license bytes.

Startup fell from 139.10s to 126.72s, refresh from 47.00s to 30.30s, and sequential shutdown from 130.90s to 95.19s. Capture and release took 5.216s longer. Passive native provider read bytes around refresh fell from 11.83 GB to 0.42 GB while logical read bytes rose from 0.33 GB to 0.37 GB. Native counters include other provider I/O. Most of the shutdown difference was in B, and the samples cannot isolate a cause for that difference. The measured pair clears 10% on both clocks. It covers one control-first pair with the same warm Library input. The complete post-integration `pwsh ./scripts/build.ps1` gate passed in **666.76s** with **2,605 passed, 36 conditional skips and zero failures** across 2,641 tests. All three cache cases passed. The build reported zero warnings and errors. After a process-watcher access error, a separate native audit confirmed no owned processes or new test fixtures remained. The active CLI DLLs match their owner builds. The active Runtime SHA-256 differs from the timed binary, and the four integrated candidate source hashes match the timed build's source proof. Gate evidence is under `artifacts/parallel-total-20261105/sealed-reader/full-gate/cooperative-full-gate/`.

Controller and run evidence are under `artifacts/parallel-total-20261105/` in `t13-lifecycle/`, `t13-b1/` and `t13-c1/`.
