# Actual Unity parallel workspace timing

## November 19 maze workflow

On Unity 6000.6.8f1, a three-workspace replay reached a playable maze in **19m 50.94s**, down from **31m 52.81s**. The original was clean and all task resources were released in **20m 22.95s**, down from **32m 09.80s**. The historical pre-opened shared FIFO Editor reached those endpoints in 6m 44.87s and 7m 02.89s. The replay used the same 69 source files, 24,246 warm Library files, package versions and previously authored module bytes, delivered at their recorded draft times. It includes its controller interruption and recovery. The local comparison is under `artifacts/maze-20261119/workflow-comparison.md`.

The first workspace launch moved from 221.84s to 73.15s and all three Editors reached verified readiness on their first launch. World applied 32.49s after its module passed, down from 868.87s. Actors applied while Presentation was still starting. All three results survived release. The restored native original loaded the combined game, followed a 121-position A* route through five encounters, recovered health and reached the exit. [Workflow guidance](../../skills/splinter/SKILL.md#schedule-independent-work-and-finish-ready-workspaces)

A matched script edit took 19.75s to verify on native NTFS and 96.55s / 97.65s on repeated mounted runs. With two writers the figures were 26.54s / 25.72s on native NTFS and 121.19s / 136.50s on mounts. Read, write, flush and Unity timings are recorded per workspace. One interrupted provider sample from the full game is unavailable. All three replay workspace Editors exited with a Windows access violation. Their process groups emptied, their releases completed, and the restored native original closed normally. [Timing diagnostics](../development-cli.md#unity-readiness-and-shutdown-timing)

### Windows Editor shutdown

On the same Unity 6000.6.8f1 installation, a single fresh workspace reproduced the crash after compiling, saving and checkpointing one script. The native process returned `0xC0000005` 143.46s after the stop request appeared on disk. This dump and seven earlier dumps placed the main thread inside `EditorApplication.Exit` during native teardown. A Windows UI Automation timer raised an exception in `InstallHook` on another thread, with 218–221 recursive Burst signal-handler frames. Calling the File/Exit menu directly from the managed update also crashed, this time on the main thread immediately after Mono cleanup.

Queuing `WM_CLOSE` to the Editor's own main window allowed the Unity update to return before shutdown. On the same 24,315-file input, the single Editor then exited with code zero in 75.38s. Its supervisor confirmed an empty process group, the package reader reported no I/O errors and the verified checkpoint survived release. The local process observations, stack comparisons and result receipts are under `artifacts/unity-exit-20261119/`. [Shutdown and recovery](../storage-and-resources.md#processes-and-memory)

Two further workspaces shared the same fixed Library and compiled separate scripts returning 101 and 202. Each Editor loaded only its own test type. With both Editors open, B's final checkpoint-and-release closed its root in 113.77s. A then returned 101 from its original PID and closed in 71.35s on its own final request. Both native handles and supervisor receipts reported exit code zero, and both process groups emptied. Reading each intermediate and final result after reclamation confirmed its script hash and Unity metadata. The restored original had a native Library directory, a clean Git worktree at its starting commit and matching hashes for all 24,315 input files. These shutdown checks cover standalone and paired Editors on this Windows installation. The earlier three-Editor maze timings used the previous bridge.

## November 19 original publication and workspace reclamation

On the matched warm URP project, checkpoint through a recompiled live original fell from **42.80s to 36.19s (15.4%)**. The first normal shutdown request through both released workspaces fell from **86.15s to 68.66s (20.3%)**. The control used separate `unity stop` commands before final preservation. The selected sequence starts `workspace checkpoint --release` on the dependent Editor, confirms its process group has exited and the owner still responds, then starts the owner's final command. Each command uses its own stable request key.

| Interval in wall seconds | Control | Selected |
| --- | ---: | ---: |
| First start to readiness, A / B | 59.19 / 66.29 | 59.14 / 64.79 |
| Mid-work checkpoint | 13.97 | 10.71 |
| Plan / read reviewed entries / apply to original | 5.02 / 0.69 / 8.88 | 3.78 / 0.70 / 6.59 |
| Original Editor refresh through verified recompile | 14.02 | 14.09 |
| Checkpoint request through recompiled original | **42.80** | **36.19** |
| First shutdown request through both release receipts | **86.15** | **68.66** |
| A's first start request through both releases, including Editor work | 435.47 | 399.96 |
| Separate shared Library restoration | 171.36 | 138.97 |
| Library preparation through restoration, including independent checks | **679.98** | **597.58** |

The publisher's file-write stage took 0.331s and 0.314s. Within apply, the nested replan went from 2.195s to 1.397s, including generation verification from 0.992s to 0.226s. The direct final command also removes the separate stop response's source inspection. In the selected sequence B's group reached zero processes at 26.50s. The remaining 14.41s of B's work overlapped A's command. Physical delta removal took 1.19s / 0.86s for A / B on the control and 1.12s / 1.50s on the selected runtime.

This was one control-first pair on Windows with Unity 6000.3.27f2, URP 17.3.0 and fresh copies of Git `e99b0462`. Each started with the same 75 source files and 20,435 warm Library files (1,599,268,684 bytes). Two task Editors compiled separate edits, saved separate scenes and overlapped in Play. A third Editor ran the original in batch mode. An external test hook saved its work, deferred imports, and then verified the published script's new values and assembly identity after refresh. All four task first starts produced the same 590-node, 9,056-edge Bee graph. B's internal `BuildProgram` took 5.891s and 5.852s.

Both final 75-file results matched independent hashes and lengths. The original contained the selected A script, with its Git HEAD, branch and index intact. Both runs checked every restored Library path and length, hashed all 20,442 restored files and compared 133 hashes to the closed, mounted original. All three Editors and their owned providers exited. The measured pipeline saved 82.40s (12.1%); preparation alone varied by 13.55s and the original Editor's normal exit by 8.86s. Fixture construction and the final proof outside that pipeline brought total controller time to 736.76s and 631.12s. These measurements cover this warm Git workload with one selected script. The frozen binaries, timings and proofs are under `artifacts/pub-close-20261119/`, with the two accepted cases in `comparison.json`.

### Small reproductions

- On a 75-file Git/Unity original with the Editor closed, the initial implementation refused publication with `InputUnavailable` before recording an intent. The apply command already held `Temp/UnityLockfile` when its own replan tried to open it exclusively again. The control for the timing comparison contains just the reservation repair on top of the first-start binary. A native regression also exercises an externally held reservation, interruption, same-intent recovery and a later original edit.
- Two fresh copies per implementation each exercised an edit and revert of one script. Median CLI plan time fell from 3.02s to 1.99s and apply time from 6.98s to 3.64s. With trial order reversed, edit and revert took 5.18s and 5.20s to apply on the control, and 3.69s and 3.60s on the selected implementation. The `.meta` companion, all 75 original files and the Git state were checked. The selected implementation reuses physically protected generation readers inside the request and looks up the selected file directly. [Original publication](../repeated-original-apply.md)
- Eight interleaved small journal runs replayed a metadata copy while 128 pairs of writes appended and overwrote Burst data. Reusing the active physical reader and skipping an already traversed admission set reduced median operation time from 0.896s to 0.844s. Each run still committed and flushed all 256 writes, verified the closed contents and reclaimed the native backing. A separate real Unity task with 28 source files took 19.86s from normal stop request through release with separate commands and 15.51s with `workspace checkpoint --release`.
- Running the original Editor also changed its Library directory layout. On a 769-file reproduction with a renamed directory and a private range, fresh paged restore took 3.87s and 3.89s before the restore change, then 3.32s and 3.09s after it. Interrupting at file 256 and finishing the same plan took 4.30s and 3.59s respectively. Each run verified all final native file identities and bytes. The focused paged restore and path tests passed 38/38. [Library restoration](../git-library-reference.md)

An earlier full control reached both released results in 78.43s from the first stop request. Its separate shared Library restoration canceled at the 90s stop with 10,880 of 20,442 files recorded. The same frozen control resumed its exact plan in 112.29s. All restored paths and lengths and 133 pre-restore sample hashes matched. This interrupted trial is excluded from the matched timings.

## November 19 Unity first start

The 590-node Bee graph slowed to 14.77s on a small mounted replay under competing disk writes. In the final matched URP pair, the startup change reduced B's graph generation from **7.372s to 6.445s**. B reached Editor readiness 0.15s sooner. Its subsequent Bee build took 23.55s before and 24.63s after, with the same 162 items updated and 587 evaluated. All four real launches produced 590 nodes and 9,056 flattened edges.

| Measured interval | Before | After |
| --- | ---: | ---: |
| A / B graph generation, Unity process log | 6.592s / 7.372s | 5.858s / 6.445s |
| A / B first start to verified readiness | 65.47s / 73.32s | 63.60s / 73.17s |
| Both first starts | 138.79s | 136.77s |
| Normal stops, B then A | 86.84s | 86.35s |
| Result capture and release of both workspaces | 17.12s | 18.42s |
| First stop request through both released | 104.00s | 104.80s |
| A's first start request through both released, including Editor work | 294.95s | 294.19s |
| Original Library restoration | 22.08s | 22.29s |
| Library preparation through restoration, including independent checks | **374.75s** | **373.36s** |

This was one control-first pair on Unity 6000.3.27f2 and URP 17.3.0. The control matched all 45 CLI files of the November 18 candidate. Each arm started from a fresh copy of the same warm `e99b0462` input, with 75 source files and 20,435 Library files. Both Editors compiled distinct edits, saved distinct scenes and overlapped in Play. A remained responsive after B stopped. After release, all 75 result hashes and lengths matched independently for each task. The original Library bytes and Git state were restored and every owned Editor, provider and mount exited. Including creation of the test input and the final audits, the controller took 408.29s before and 420.83s after. Input creation alone took 28.13s and 42.00s. The 1.39s difference in the product pipeline is within the variation observed on this machine.

### Isolating graph generation

- A small three-package Unity project produced 160-node graphs in 0.332s for A and 0.334s for B in the internal `BuildProgram` trace. A stayed open during B's launch. In a separate diagnostic launch of the full URP input, the same trace took 28.74s in A and 6.44s in B. That diagnostic pair was excluded from lifecycle timing after a probe-file rename failed. Both Editors subsequently stopped normally, both results were checked and the original was restored.
- Replaying B's captured 41-assembly, 21-package recipe generated the same 590-node dependency graph and action flags on native NTFS and both mounted runtimes. The frontend process took 0.633s on native NTFS. On the mount it averaged **6.318s before and 5.788s after** over two runs per arm. Each mounted run issued 2,618 process writes and recorded 2,628 provider flushes. The provider kept its durable acknowledgement on every serial write. The new queue spends time waiting for a batch only after callbacks overlap. Metadata relocation also avoids writing a second copy of entries held in its bounded cache. Its separate contribution to first start was not measured.
- A six-writer trial hit its 40s stop and was excluded. After resuming the same workspace, the control frontend took **14.766s** with two paced 4 KiB writers on the same volume. The immediately adjacent runs with those writers off took 13.590s and 9.091s. Cumulative provider flush time was 4.60s in the earlier quiet runs and 12.78s in the two-writer run, with the same 2,628 flushes. Storage latency can reproduce the observed delay. The cause of that individual November 18 B launch remains unconfirmed. The workspace was released normally.

Release build completed with zero warnings and errors. The focused write and metadata suites passed 34 cases with one conditional skip. Two executions of `pwsh ./scripts/build.ps1` failed under the default eight-class schedule. One packing test and, on the second run, the pending-packing-reader reclamation case reached their existing 10s limits. The second failure also caused shared-fixture and runner cleanup errors. The entire packing and reclamation classes passed separately (25/25 and 13/13). All 45 build output files matched the timed candidate. Local timings, traces, regression logs and restoration proofs are under `artifacts/unity-startup-20261119/`, with the matched timings in `comparison.json`.

## November 18 matched Unity lifecycle

On Unity 6000.3.27f2 and URP 17.3.0, the first shutdown request through release of both workspaces took **115.49s before and 76.55s after** the changes. The full pipeline from Library preparation through restoration took **370.56s before and 337.50s after**, including the independent checks. This was one control-first pair on the same machine and warm input.

| Measured interval | Control | Candidate | Difference |
| --- | ---: | ---: | ---: |
| First starts for A and B | 132.29s | 139.44s | 7.15s longer |
| Two source edits and refreshes | 24.95s | 25.86s | 0.91s longer |
| Normal Editor stops, B then A | 92.40s | 61.67s | 30.73s shorter |
| Concurrent result capture and workspace release | 23.09s | 14.86s | 8.23s shorter |
| First stop request through both released | 115.49s | 76.55s | **38.94s shorter (33.72%)** |
| Original Library restoration | 20.45s | 20.45s | Under 0.01s |
| Core lifecycle | 355.07s | 322.65s | 32.42s shorter (9.13%) |
| Pipeline including package and independent result checks | 370.56s | 337.50s | **33.06s shorter (8.92%)** |

The control was built from `c371fd04`, including the earlier transient-flush optimization. Both arms had frozen 45-file runtime inventories. They used fresh copies of Git commit `e99b0462` with 75 source files and 20,435 Library files (1,599,268,684 bytes). In each arm, two Editors compiled distinct edits, saved independent scene states and overlapped in Play. A stayed responsive across B's normal exit. Both arms independently matched the 75 expected result hashes and lengths per task, restored the complete original Library and Git state, and verified that all owned Editors, providers and mounts had exited. The normal Unity identity matched across each run. Serialized license bytes changed in both. Elapsed times use the Windows performance counter. A passive observer bracketed each final release within 0.052s. The core excludes the package and independent result checks. The pipeline excludes fixture construction and the final original audit.

B's normal stop fell from 58.82s to 29.60s. Around that stop, the provider recorded 23,531 versus 11,865 mutation-gate entries, 30.88s versus 0.013s of cumulative gate wait, and 35.40s versus 18.25s in write flushes. Those counters overlap and their sample windows include adjacent activity. A's delta removal took 10.51s before and 0.91s after. B's took 1.39s and 1.21s. Each arm enabled the same generation-stage timers and used passive provider counters with per-callback write tracing disabled.

First-start Bee work was identical across all four URP launches, including one C# bridge compilation, 38 response-file writes and 37 IL post-processing actions per Editor. A's initial asset refresh fell from 50.67s to 47.37s, while B's rose from 56.54s to 67.68s. In B, graph generation with `ScriptCompilationBuildProgram` took 7.31s before and about 15s after. Both graphs had 590 nodes. Executing the Bee build took 23.73s and 23.49s and updated 162 items each time. Unity's enclosing script-compilation interval grew from 35.31s to 47.63s. The November 19 investigation above reproduces this timing range.

### Small reproductions

- With 128 interleaved Burst-style append/overwrite pairs, the NTFS ABBA mean fell from 1.582s to 1.024s. Gate entries fell from 515 to 387. All 256 write flushes and acknowledgements completed in each arm. Reopen verified the physical seal, contents and EOF.
- A lookup and read loop over 38 deep Bee response-file paths took 24.85ms before and 23.42ms after. In a separate small Unity project, native NTFS and both mounted runtimes performed the same first-start actions and no Bee actions on unchanged restart. Mounted first-launch readiness was 15.46s before and 15.58s after. Restart took 10.36s and 10.17s. A final checkpoint starting with the Editor open stopped it and released the workspace in 14.77s. The small control independently verified its base sources and identified one generated PackageManagerSettings file without a pre-release hash. Both full URP arms independently verified every result file.
- In the six-file final-capture reproduction, the original runtime started a new pack in both control runs. The candidate started none. Mean process time through reclamation and proof fell from 3.43s to 1.82s. With an already published pack, both runtimes resumed it, retained the seal and performed 12 native unlink calls. The stopped pending check read about 28.7 kB before and 15.5 kB after. Recovery of that same request then used about 517 kB and 1.0s in both arms. The final reclamation read about 0.53 MB per arm. Native unlink consumed about 0.017s and zero read bytes. These are per-process Windows I/O transfer counts on a small local journal.

The complete `pwsh ./scripts/build.ps1` gate finished in 421.48s with 2,607 passing tests, 36 conditional skips and zero failures. The build reported zero warnings and errors. The 45-file CLI output matched the timed candidate byte for byte. The frozen manifests, small cases, per-stage outputs, independent result proofs and full gate are under `artifacts/unity-perf-20261118/`. The matched cases are `u18-b1` and `u18-c1`. The evidence covers this Git and warm Library workload.

## Earlier Unity comparisons

An earlier small URP A/B run took **528.55s**, versus **441.09s** for the mean of two public A/B runs. It was **19.8% slower** and missed the below-half target of 220.54s. One of three attempts completed cleanly. The other two passed Editor validation and then required recovery during workspace release.

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

### Dependent Editor shutdown

The retained B timestamps isolate the difference inside the native Editor lifetime. The first observed `exiting` heartbeat precedes or accompanies the Unity exit call. Unity's final cleanup log records its own UTC timestamp. The separate Job receipt proves the root exited successfully and no owned processes remained.

| B shutdown stage | Control | Sealed-reader candidate |
| --- | ---: | ---: |
| Before CLI to stop-request publication | 0.474s | 0.477s |
| Request publication to first observed `exiting` | 0.474s | 0.463s |
| First observed `exiting` to final Unity cleanup log | 69.987s | 31.007s |
| Final cleanup log to zero-process Job receipt | 0.315s | 0.319s |
| Job receipt to CLI response | 3.960s | 4.253s |
| Total | 75.209s | 36.518s |

Both B providers recorded 10,301 durable write flushes, 10,306 committed writes, and 1,134,728 changed payload bytes. Bracketed write service time was 44.905s versus 24.962s, including 39.333s versus 20.957s in the durable flush interval. During the early slow interval, control averaged about 7–8ms per barrier against about 2ms in the candidate. Control's cumulative Runtime mutation-gate wait reached 22.501s versus 0.017s in the candidate, with most of the control wait appearing after 34s, as barrier latency returned toward 2ms. Counter brackets extend about one second around the request and Job receipt. Wait counters can overlap each other and active service, so they cannot be added to wall time. The control and candidate each spent about three seconds of the post-receipt interval inspecting source for the CLI response. A's first exiting heartbeat to final log took about 50.5s in both runs.

The historical shutdown logs show normal licensing disconnect and cleanup. They contain no matching popup or shutdown refusal, and no contemporaneous window census was recorded. During the long wait, the B Editor used little CPU while durable writes continued. The single-Editor write diagnostic also found that the Burst manifest writer issued successive writes after their previous callbacks returned, leaving no measured overlap for combining those acknowledged mutations.

A fresh shutdown-only diagnostic used the frozen control, the same warm Library and Unity 6000.3.27f2, and a live A as the licensing peer. Only B repeated the source refresh and Play transition. The B CLI took 60.856s. Its request was published at +0.586s, the first exiting heartbeat at +1.014s, and the Mono cleanup log arrived at +5.674s. The package-manager shutdown log arrived at +53.278s, followed by native root exit at +53.293s and the zero-process Job receipt at +53.584s. Source inspection took 5.519s of the 7.273s after the receipt. Log boundaries use sampled arrival times.

B returned 10,308 singleton write callbacks, with 46.272 cumulative callback-seconds and 24.943s in durable write flushes. The provider endpoints bracket the request and native exit by approximately 0.66s each. The bounded rings retained 1,338 distinct callback samples, including 1,312 Burst manifest writes. Most write activity fell between the Mono cleanup and package-manager log markers. Other Editor work and waiting share that interval, and cumulative callback and flush counters overlap.

An owned live metadata repack intent appeared at +31.010s and was still observed at +53.124s. Before the intent, writes completed at 291.5/s with 2.40ms per durable flush and approximately one mutation-gate entry per flush. During catch-up, throughput fell to 128.6/s with 2.42ms per flush and 2.96 gate entries per flush. Cumulative mutation-gate wait rose by 2.376s in that interval. This reproduces the late contention pattern and associates it with metadata maintenance. The historical control's earlier 7–8ms flush plateau did not recur.

The native window census succeeded on the active desktop in all 348 samples from CLI start to native exit, with a maximum 0.157s sample gap. The B main window disappeared at +1.38s. The census observed no visible dialogs or hung/disabled windows for B, A, or their observed Job members. A remained responsive after B exited. This covers the reproduced stop and the sampled desktop; the earlier pair has only log evidence. Diagnostic records are under `artifacts/parallel-total-20261105/st-b2-resume/`.

The initial controller stopped before launching either Editor after misreading the nested Library ID. The corrected controller resumed the same owned basis. After B completed, the A cleanup CLI reached its cancellation deadline. A had already exited normally, and the zero-process receipt arrived about 116.1s after its CLI began. Both Editors and all providers stopped safely. A separate closeout reopened only the exact task providers, preserved both results, independently verified their contents, and restored the scratch original's source, Git state and Library. See `artifacts/parallel-total-20261105/st-b2-closeout/`.

The [catch-up change](../scalable-storage.md) makes transient metadata pages visible to the replay reader while preserving durable journal ACK and root publication. A matched NTFS ABBA reproduction with 128 interleaved 4-byte mutations reduced mean repack time from 0.997s to 0.746s (25.2%) and its measured in-process scope from 1.254s to 1.005s (19.9%). Every arm matched the accepted, stopped-native and reopened seals, payload and EOF. Temporary page barriers fell from 128 to zero, and all 128 journal ACK barriers remained. Both candidate runs finished inside the first control's repack time. All 16 focused native tests passed. These measurements qualify the repack component; the Unity shutdown diagnostic measured the frozen control. Evidence is under `artifacts/parallel-total-20261105/stop-diagnostic/`.

Controller and run evidence are under `artifacts/parallel-total-20261105/` in `t13-lifecycle/`, `t13-b1/` and `t13-c1/`.
