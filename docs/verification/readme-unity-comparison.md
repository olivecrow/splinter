# README Unity storage and time comparison

[한국어](../ko/verification/readme-unity-comparison.md)

This document owns **RB-01–05**, the 2026-10-07 README comparison of actual Codex app-managed worktrees and the current Splinter implementation with an original Library basis. It adds one bounded measurement set; it does not reopen the historical [Unity comparison](unity-live-comparison.md), SF/PV matrix, or completed storage/memory stages.

The measured edited-ready additional file allocation fell from **11,297,124,336 B to 152,543,856 B (98.65%)**. Splinter's Editor preparation and subsequent code refresh/compilation took longer. [Public numeric evidence](../../reports/benchmarks/unity-workspace-comparison-20261007.json) supplies the README charts; [the renderer](../../scripts/plot-unity-workspace-comparison.py) produces PNG and SVG from that dataset.

## Fixed input and conditions

| Item | Recorded input |
| --- | --- |
| Project | Separate `splinter-unity` Git/Unity fixture; development revision `b09fa58e9d40a560d4fe1af7a213f8ac23e8e3b1` |
| Source | 1,602 tracked files, 2,804,772,822 working-file bytes; includes the approximately 2.8 GB Medieval asset input |
| Unity | 6000.3.22f1, URP 17.3.0; the existing committed qualification probe and installed project packages |
| Host | Windows (reported OS version 10.0.26200), NTFS, AMD Ryzen 5 5600X, 68,628,553,728 B installed RAM |
| Product | HEAD `74518fd453ea9e191c5b5efc1e2ad82a17ffe427`; frozen Release assemblies identified by ten SHA-256 records in the dataset, with no rebuild during measurement |
| Codex | Two real app-managed worktrees created through two explicitly authorized measurement chats; each starts without Library |
| Splinter | Two `git-snapshot` / Git / Unity workspaces at the same commit, using the prepared original Library basis; default SJL4 optimization ON verified in both providers |
| Execution order | One Codex A/B cohort, then one Splinter A/B cohort; two independent Editors within each cohort; OS caches not cleared |

The original Editor was absent at preparation. Only Library was temporarily moved into a managed basis on the same NTFS volume, without copying its existing payload. Source stayed at its original path. This Library reuse is the user-selected comparison condition, not a cold-Library comparison.

Both Splinter workspaces were prepared before the timed Editor cohorts. Their mounts and the original Library view remained present during the Codex cohort, with no Splinter Editors running yet. This setup ordering and its cache/resource effects are part of the single-run condition.

Git's global `core.autocrlf=true` was retained. To use equal raw committed bytes, the 31 line-ending-only differences in each Codex checkout were normalized, and all 1,602 checked-out files were verified against their Git blob identities before Editor launch. The original working-file bytes were preserved. The fixture temporarily used local `core.autocrlf=input` for Splinter's clean/raw input restriction; final cleanup removes that local override.

## Work and evidence criteria

The source task was one changed line per worker, with no new feature, asset edit, Play Mode or game build:

| Worker | File | Original calculation | Changed calculation | Result for input 3 |
| --- | --- | --- | --- | --- |
| A | `Assets/SplinterAlphaA.cs` | `value + 1` | `value + 2` | 4 → 5 |
| B | `Assets/SplinterAlphaB.cs` | `value + value` | `value + value + value` | 6 → 9 |

Both methods performed these same changes and checked compiled values, unchanged peer/original values, and selected existing assets. The 9,367.0703-second preservation/release interval starts after this work and normal Editor exit. It is additional overhead for two small source results, not time spent implementing a large feature. The delays are performance blockers for interactive development on this measured input; storage reduction alone does not establish practical usability.

| ID | Fixed criterion | Result |
| --- | --- | --- |
| RB-01 | Actual Codex worktrees, immutable common input, frozen product binaries, exact returned Splinter paths and owned Editor PID/start identity | Observed; first Splinter ensure rejected the line-ending input before mounting, then the same task inputs were corrected |
| RB-02 | Both independent Editors ready at baseline values A=4, B=6, original=13 | Observed for both methods |
| RB-03 | A changes only its calculation to 5; B changes only its calculation to 9; successful refresh with a new domain generation and assembly MVID, current values and original=13 | Observed in all four Editors; response errors empty |
| RB-04 | Same selected model, texture, scene, and bounded URP preview; actual additional file allocation and stage timing | Observed; three meshes, 3,001 vertices, 2,441 triangles, 2,048×2,048 texture, no missing scene meshes, foreground pixels and restored scene state |
| RB-05 | Normal owned Editor shutdown, independent results, owned Splinter reclamation, original Library restoration and unchanged original source | Closure evidence is recorded with the results and cleanup below; Codex chat-owned worktrees remain retained |

The existing fixture probe publishes fresh state at the exact project path with PID, generation, MVID, compilation/updating flags, and loaded values. Initial readiness requires fresh, non-compiling/non-updating state. Code-change readiness additionally requires the matching successful refresh response and new generation/MVID. The asset observation loads the selected committed resources and inspects one bounded render; it is not qualification of every asset in the pack.

No Play Mode, Test Runner, game build, new tool installation, original source integration, forced Editor termination, or expanded failure matrix was performed. Optional external packages were left unchanged. The probe is external compilation evidence; Splinter checkpoint/release does not claim compilation success.

## Additional file allocation

| Measurement stage | Codex, B | Splinter, B | Interpretation |
| --- | ---: | ---: | --- |
| Edited and compiled, both Editors ready | 11,297,124,336 | 152,543,856 | README comparison: 10.5213 versus 0.1421 GiB; 11,144,580,480 B saved, 98.6497% reduction |
| Editors stopped, before result collection | 11,288,856,928 | 197,453,080 | Separate stage; shutdown changed generated/private files, so this is not the edited-ready numerator |

Allocation uses Windows `FILE_STANDARD_INFO` for the actual files, rather than logical mount sizes. Every selected root completed with no unknown hardlink/reparse/read-error total. Codex includes its two source/Library roots, two observer control roots, and two owned Git registration roots. Splinter includes both task records/private deltas and owned Git registration, plus runner, locks, Library metadata and original delta counted once. The pre-existing moved Library `base` payload is excluded, and the logical workspace mount is not traversed as another physical copy.

Existing original source, original Library payload, shared existing Git objects, unrelated tasks/evidence, global application caches, directory/MFT metadata, OS cache pages, and whole-disk instantaneous peak are outside these numbers. The new `git-snapshot` run used no VSS. Shared-data references and `readCacheBytes=0` do not establish RAM savings or a zero-cost provider.

## Time

The A/B cohort durations run until both workers meet the criterion. Workers launch and refresh concurrently; two Splinter ensure calls were sequential. Editor timing begins with workspaces already created. Costs below are not one interchangeable end-to-end timer.

| Stage | Codex, seconds | Splinter, seconds | Splinter difference |
| --- | ---: | ---: | ---: |
| Editor launch to initial compiled readiness | 887.1241 | 1,753.4893 | +866.3652 (+14m 26s) |
| Code edit / refresh to compiled readiness | 19.0121 | 554.4164 | +535.4043 (+8m 55s) |
| Library basis preparation, once | Not applicable | 272.9366 | Separate setup cost, 4m 33s |
| Two workspace ensure calls | End-to-end app completion unknown | 766.4307 | Separate setup cost, 12m 46s; A 381.1842 / B 385.2465 |
| A checkpoint after Editor shutdown | Comparable whole procedure not measured | 5,723.6083 | Separate preservation cost, 95m 23s; includes all product verification and final response |
| A release | Comparable whole procedure not measured | 1,662.4729 | 27m 42s; overlapped B preservation |
| B parallel checkpoint | Comparable whole procedure not measured | 2,914.6685 | 48m 35s after external source commit; different preparation from A, overlapped A checks/release |
| B release | Comparable whole procedure not measured | 1,163.3742 | 19m 23s |
| A/B preservation and release, recorded sequence | Comparable whole procedure not measured | 9,367.0703 | 2h 36m 7s elapsed wall time, including overlap, recovery and concurrent diagnostics; includes the checkpoint/release calls above |
| Final original Library restoration | Not applicable | 1,473.2436 | Separate restoration cost, 24m 33s |

Codex's app returned queued client IDs; Git index timestamps give only a roughly 46-second checkout proxy. They are not an end-to-end Codex task creation measurement and do not appear in the time bars. Library preparation elapsed overlapped a brief source audit and is not an isolated microbenchmark. Polling/launch/refresh are included in their respective Editor phase; setup, separate asset inspection, result cleanup and chart rendering are outside those bars. The Splinter initial clock continued through the observer repair, against the same already running Editors; neither Editor was ready when observation resumed. These are observed wall intervals, not exact internal Unity event timestamps. Cache/order effects and variance are unknown because there is one cohort per method.

Splinter's initialization passed the observer's initial 20-minute wait bound while its logs continued progressing. The same Editors were observed to readiness with an extended 45-minute bound; no replacement Editors or new cohort were started. One A initialization log region reported approximately 456 seconds in `InitializeOnLoad`; this is an observation, not a diagnosis of the whole slowdown. The later preservation diagnostics below establish separate bottlenecks; they do not apportion the earlier Unity initialization or refresh durations.

## Preservation delay diagnosis

The user requested a diagnosis while A's checkpoint was still running. These bounded read-only probes ran after the plotted Editor phases, against the same stopped tasks. They did not restart Editors, repeat edits, alter product state, or modify the frozen implementation. The dataset records their timings separately from the comparison bars.

Three measured costs compound in the current preservation path:

1. **Full metadata checks repeat despite reused content hashes.** For 1,602 source files, one `InspectSourceAsync` invokes 33 batches each of working-tree and index filter/encoding checks, plus 33 batches of all attributes before and after inspection: **132 sequential `check-attr` commands**, in addition to membership/configuration/path checks. Checkpoint, collection and independent preservation call inspection at several boundaries. Completed A inspections reused all 1,602 file hashes with zero content-hash input yet took 382.1333, 405.2459, 478.3267 and 1,429.7086 seconds. Reuse avoids content hashing; it does not eliminate the remaining metadata work. See [source inspection](../../src/Splinter.SourceControl.Git/GitSourceFingerprint.cs), [Git checkpoint](../../src/Splinter.SourceControl.Git/GitWorkspaceProvider.cs) and [result collection](../../src/Splinter.Runtime/SnapshotWorkspaces.cs).
2. **Queries through the mounted workspace are slow in the observed state.** The same 50 tracked paths produced equal-size Git attribute output: ordinary NTFS took 0.2277 seconds and Splinter A took 12.8337 seconds, about 56 times longer. Index-only filter/encoding queries took 0.2274 versus 12.5749 seconds. A plain revision query took 0.1738 versus 5.8935 seconds, and a single-file attribute query on Splinter took 7.7605 seconds, showing substantial per-command startup/path cost as well as attribute processing. Stat of ten existing files took 0.0025 versus 24.0626 seconds. These are single probes during an active checkpoint, with unknown contention/cache effects; they are not general filesystem benchmarks or a measured breakdown of every Git call.
3. **Storage observations walk the current journal inventory under the common store lock.** [`LatestJournalStorage.Bytes`](../../src/Splinter.Runtime/LatestJournalStorage.cs) enumerates and reads every current journal file's length; [`JournalDeltaStore.Observe`](../../src/Splinter.Runtime/JournalDeltaStore.cs) calls it while holding `EnterGate`. The provider performs observations in its periodic loop, and the filesystem's volume-info callback also invokes `Observe`. A's journal had 16,259 files; the same .NET inventory body, executed outside the provider gate, took 0.8914 seconds for 36,694,054 logical bytes. This provides a concrete source of repeated lock occupancy during metadata queries. Cumulative lock wait is summed over callers and must not be interpreted as elapsed checkpoint time. See [provider loop](../../src/Splinter.Platform.Windows/WinFspOverlayServer.cs) and [volume-info callback](../../src/Splinter.Platform.Windows/WinFspOverlayFileSystem.cs).

The independent-preservation path also runs `git ls-files --others --ignored --exclude-standard` and filters managed paths **after** Git returns the list. Consequently, generated data such as Library can be traversed during this guard. This is an additional code-identified cost; its separate elapsed contribution was not measured.

The diagnosis establishes slow mounted metadata access, repeated verification, and an expensive locked observation path. It does **not** establish the exact function-by-function share of elapsed time, a memory leak, paging as the main cause, a Unity package defect, or the cause of every earlier Editor delay. The source was still progressing, and provider observations reported no I/O failure. The measured implementation was left unchanged. Optimization candidates are to preserve the same before/after ownership/content guards while reducing redundant metadata passes and keeping routine storage observations from repeatedly occupying the callback lock; those changes require a separate implementation and validation task.

Final restoration is a separate cost boundary. [`ManagedDirectoryRestore`](../../src/Splinter.Platform.Windows/ManagedDirectoryRestore.cs) validates fixed file identities/content, moves basis files, verifies the final content/metadata and durably publishes a cursor after each file. The observed cursor reached 40,487 completed files while restoration continued; its separate hashing/move/flush contributions were not profiled. Restoration completed in 1,473.2436 seconds. This is not part of the earlier Editor bars or the A/B preservation/release cohort.

## Failures and recovery boundaries

- The first Splinter A ensure rejected the unsupported Git line-ending condition before mounting. It was corrected using the same key/id/home/base and preserved failure evidence.
- The measurement driver initially interpreted asynchronous `unity start` **Accepted** as failure. Observation resumed against the already owned Editor identities; start was not repeated.
- A Codex observer-file publication failed with Windows error 5 before the pending inspect request was published. The same request was published with the appropriate normal-user atomic replace; no code edit/refresh was repeated. This is harness evidence, not a demonstrated Splinter defect.
- Codex result preservation initially required an empty status listing, although normalized files retained stat flags. Content diff was empty and all 1,602 raw result files matched their preserved commit. B preservation resumed without repeating the edits or Editor stop.
- Both Splinter `unity stop` calls returned **EditorStopTimeout**. The Editors subsequently completed normal shutdown without force termination; both process-exit receipts report exit code 0 and active job processes 0. Their absence and zero open mount handles were observed before result collection. The recorded 741.3975-second stop-cohort interval is an **observation upper bound**, not an exact exit time or a successful initial CLI response. The stale `editor-starting` phase was not used as evidence that an Editor remained alive.
- The original cleanup driver retried B checkpoint while B's parallel checkpoint held its task lock. The CLI returned **WorkspaceBusy** after 5.8595 seconds, and the driver stopped. The busy response is retained; recovery uses the successful parallel checkpoint receipt before invoking regular B release. This is an orchestration conflict and the task lock's expected refusal, not a repeated code edit or a bypass of checkpoint guards.

## Results and cleanup

Codex results are independently retained at revisions `022376235d10ec293fe05cb69aa3dfd4910da0ca` (A) and `3194c94e9fb7500de30801b0ec759188c748295b` (B), under benchmark refs. Both Editors and their 61 recorded child processes exited. The two worktrees remain owned by their measurement chats: cross-task attachment was rejected, so they were not deleted or archived from this chat.

Splinter results `3d4eb870ddb3e469d4feb32c838bf97775fa6af1` (A) and `2bbfe63731eecd83d1af91dd8fc529c28c6f2521` (B) have the same result trees as the corresponding Codex results. Both tasks reached `released`; owned deltas/Git registration, Editors and providers are absent. The original Library is an ordinary NTFS directory again. All 1,602 original tracked files, original HEAD/branch, and pre-existing refs were verified unchanged; the temporary local `core.autocrlf` entry was removed and the global setting retained. Ten frozen binary hashes matched before the required build. These checks are recorded in the public dataset's cleanup section. Raw JSON, process identities, logs, journals, and previews are retained locally under ignored `artifacts/readme-unity-comparison-20261007/` and `.local/rb7/`; proprietary asset paths/images and session tokens are excluded from the published dataset.

After the timed phases and normal Editor exit, A used Splinter's checkpoint to commit its changed source. While A's checks continued, B's one exact source file was committed with external Git and its result tree compared with Codex B. B's regular Splinter checkpoint was subsequently started in parallel with A's remaining checks/release. The original driver's B retry was refused with WorkspaceBusy; after the parallel checkpoint succeeded, regular B release resumed from its recorded result. This overlapped independent preservation steps without editing product state or skipping source/seal/reclamation guards. The cleanup duration includes this recorded recovery and concurrent read-only diagnostic probes; it is not an all-in comparison of identical cleanup procedures. Overlapping individual calls must not be added to the cohort duration again.

After measurement and cleanup, the required `pwsh ./scripts/build.ps1` passed locked restore, a Release build with zero warnings/errors, and 1,134 automated tests; 19 explicitly gated tests were skipped and none failed. This validates the working checkout, including pre-existing unrelated changes, after the frozen measured binaries had been checked. It does not replace the recorded Unity observations or qualify the skipped native runs. Documentation validation checked nine changed pages, 538 local links, matching bilingual numbers/qualifications, and all six PNG/SVG files, with no broken links or sensitive dataset fields found.

The README comparison figures show edited-ready allocation and the two common Editor phases. A third figure shows the separate Splinter setup, preservation/release and final restoration intervals; comparable whole Codex lifecycle times were unavailable. Shutdown remains a separate observation upper bound. This execution does not establish statistical performance, total RAM or storage peak, large-project support, general Unity asset compatibility, or native interruption/OS-failure guarantees.
