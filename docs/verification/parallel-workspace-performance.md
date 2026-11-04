# Actual Unity parallel workspace performance

The candidate committed as `1e1bdb1` **does not meet the below-half-public-time target** on this dedicated real Unity input. Mean accounted time is **594.15s current versus 426.15s public**, or **39.4% longer**. All four measured product lifecycles passed the specified compile/scene/Play, result and original-restoration checks. Library restoration is the dominant regression. Earlier editorless results for `11e7dc3` do not qualify this candidate's Unity performance; retrieve that report from Git history.

## Versions, input and method

Measured on Windows on 2026-11-04 (Asia/Seoul). GitHub's default `development` tip was rechecked through the connector at 03:53:25 UTC as [`c4fa5063964e7b8456de4ea7735c042fb9af8930`](https://github.com/olivecrow/splinter/commit/c4fa5063964e7b8456de4ea7735c042fb9af8930). Its [verification index](https://github.com/olivecrow/splinter/blob/c4fa5063964e7b8456de4ea7735c042fb9af8930/docs/verification/README.md) records 1,691 Windows passes and 24 skips. Different test inventories do not establish a performance ratio.

Current source is `1e1bdb19e104e336545607b2fbc701573995f38c`. The frozen Release binaries were built and fully tested before that commit; all ten runtime DLL hashes match both current measurement samples and the final files. No rebuild occurred between gate and measurement. Public uses its separately built matching-format binary. This compares the same user workflow across versions, including their different storage formats, rather than isolating one optimization commit.

The dedicated project uses Unity **6000.3.27f2**, URP **17.3.0**, **75 source files**, and **17,863 genuinely generated Library files totaling 1,568,757,165 bytes**. Its fixed local input commit is `7ee96cd995bcb77b345f0028cdcdd351a7a34417`. Cold import and the user-approved template material upgrade precede the frozen input and are excluded. No retained large fixture or existing game project was resumed.

Each version starts from the same complete source/Library SHA-256 and length maps, clean Git HEAD and ordinary Library. The order is public → current → current → public, with separate source/home identities and two observations per version. Preparation runs once per pair; A/B creation, source reads, Editor startup, code work, scene work, Play and final result preservation/reclamation run concurrently. Normal Editor stops are requested A then B in both versions because the earlier concurrent-stop trial encountered a shared user-global layout file dialog. This is agent scheduling through existing commands, not new product orchestration.

Both owned Editor PIDs/project paths are matched to launch receipts. A compiles values `(11,20)`, B `(10,22)`, with changed assembly MVIDs. They save distinct scene markers/positions, run at least 15 runtime Update frames each, exit Play and recheck code/scene isolation. All expected source hashes are fixed before final stop. Product restoration and lifecycle dispatch gaps are included in accounted time.

Excluded work is fixture copying/schema discovery, initial out-of-band input checks, post-release independent-result checks, and final out-of-band original hash/Git checks. Those checks still execute and must pass. There is no OS-cache reset, concurrent regression or timing trace. The final public fixture was reused only after complete read-only input proof and proof that no product state existed; its measured pipeline body is byte-identical to the original driver. See failure handling below.

The host has 12 logical processors and about 64GiB RAM. An unrelated existing Editor remained running; its activity and other host activity were uncontrolled. Shutdown time varied substantially. Two samples per version are observations, not statistical or general performance qualification.

## Measurements

| Accounted lifecycle | Public | Current |
| --- | ---: | ---: |
| First sample | 376.78s | 588.29s |
| Second sample | 475.52s | 600.02s |
| Arithmetic mean | **426.15s** | **594.15s** |

The half-public-mean threshold is **213.08s**. Current exceeds it by **381.08s**; neither current sample is below that threshold. All completed product samples are included.

| Phase, mean of two | Public | Current |
| --- | ---: | ---: |
| Library preparation | 49.91s | 47.08s |
| Concurrent A/B creation/admission | 49.90s | 27.20s |
| Concurrent complete source reads | 0.17s | 0.17s |
| Two Editors ready | 103.94s | 95.87s |
| Code refresh/compilation | 15.32s | 26.19s |
| Scene save | 2.00s | 2.48s |
| Play | 9.95s | 10.03s |
| Final source observation/proof | 0.99s | 0.94s |
| Sequential normal Editor stop requests | 111.02s | 104.22s |
| Concurrent result preservation/reclamation | 42.11s | 41.85s |
| Original Library restoration | **40.85s** | **238.12s** |

Creation saves 22.69s, but preservation/reclamation wall time is essentially unchanged. Restoration adds **197.27s**, exceeding the net **168.00s** lifecycle increase because other phases offset part of that cost. It accounts for 40.1% of current total time. Public stop samples are 74.87s / 147.16s, current 89.77s / 118.66s; no shutdown sample is substituted or omitted.

## Restoration, CPU and remaining inefficiency

Every final restoration has a zero-sequence original journal and moves the retained root: there are no original private byte ranges to replay. The public binary selects its unchanged-directory route. Current overlay v3 selects paged restoration above 1,024 manifest items before unchanged-directory selection. The bounded planning/index and per-file verification path is therefore paid for this unchanged original Library.

Post-hoc immutable plan and final cursor/verified-file timestamps give these approximate wall intervals. They are not exclusive function timings or a profiler.

| Restoration interval, mean | Public | Current |
| --- | ---: | ---: |
| Start through plan publication | 21.05s | 77.63s |
| Plan through final cursor/verification | 15.83s | 157.89s |
| Remaining completion | 3.97s | 2.60s |

Current planning builds bounded cleanup/path/file catalogs and a sorted file-record spool. Application walks the namespace, looks up and decodes file records, verifies retained directory ownership, checks every exact leaf identity and all bytes, applies metadata, advances durable batch cursors, then proves final membership before reclamation. Full proof remains required. Catalog lookup/decoding, planning, namespace passes and cursor publication remain ordered; only admitted unchanged-file verification batches use up to eight workers. The earlier spool/parent-fence/bounded-worker improvement did not remove the large unchanged-input cost.

| Own CLI CPU, mean | Public | Current |
| --- | ---: | ---: |
| A/B creation sum | 15.48 CPU-s | 16.85 CPU-s |
| A/B preservation/reclamation sum | 16.38 CPU-s | 61.05 CPU-s |
| Library restoration | 41.11 CPU-s | 267.97 CPU-s |

Current restoration averages about 1.13 own-CLI CPU cores over its 238.12s wall time despite the eight-worker leaf-verification cap. More preservation/reclamation CPU produces almost no wall-time gain. Increasing worker counts alone is not an evidenced fix. These counters exclude child processes and providers; Editor counters stop at the final pre-stop snapshot. They do not measure whole-host utilization, peak RSS, kernel/disk saturation or prove a specific wait owner.

The next bounded optimization target is the unchanged paged route: reduce repeated catalog lookup/decoding and ordered planning while retaining complete native identity/hash/membership proof under held ownership scopes. Shared proof must stay within its exact fenced call, and durable cursors, reader protection and recovery cannot be omitted. The current evidence does not isolate the cost of each lookup/flush or establish that the complete pipeline is optimal. See [Library lifetimes](../git-library-reference.md), [current storage](../scalable-storage.md) and [result recovery](../repeated-original-apply.md).

## Correctness, failures and time limits

Every final product sample preserves separate A/B results, checks the complete expected source hash set, proves absent task providers/mounts/deltas and zero owned counters, and restores the complete original source/Library hash map, clean original HEAD and one original Git worktree. Public results are independently read through Git archive bytes. Current results are checked through the complete paged result file catalog against expected hashes; physical backing proof is performed by product capture/reclamation, not an additional external materialization. Final host process inspection found zero processes owned by this experiment. No results were applied to the original.

An initial current trial exposed `SourceMaintenancePending` after normal stop and result capture. `1e1bdb1` resumes the exact accepted stopped journal-maintenance request with matching seals and full backing/read-lease proof before reclamation. Wrong seals remain refused and an old payload reader still defers recovery. The original requests/results were recovered and verified without editing state.

That trial's Library restoration exceeded the **external harness's 90-second response limit** and later completed normally in 370.67s. It remains an overrun, not a passing timing sample. The 90 seconds came from a 45-second estimate and the contributor stop multiplier; it was not a Library restoration deadline in product code. A native reproduction with 1,025 files under deep paths improved restoration from 11.125s to 7.120s while checking every byte/native identity and ancestor protection. Focused ownership/recovery checks passed before fresh measurements. That component gain does not qualify the whole Unity workflow.

Before the fresh block, observed costs fixed capture/reclamation at a 180s estimate / 360s stop, restoration at 240s / 480s, and two sequential normal stops at 120s / 240s. No limit was extended during a measured phase, and no product or individual test timeout was increased. Product normal Editor stop still has its separate two-minute bound. Longer harness budgets are not counted as an optimization.

The first public concurrent-stop trial failed with `EditorStopTimeout`: Unity displayed a sharing violation while saving the user-global current layout. The exact external holder was not identified. Retrying that owned dialog completed a normal exit; same-request recovery, both complete result hash sets and original restoration passed. This trial and recovery are excluded. Fresh measured stops used A then B, without UI intervention; concurrent Editor quit is not qualified.

The final public fixture-only setup hit its predeclared 60s stop during input proof, before Library preparation or any product state existed. Its fully copied input then passed a complete read-only hash/Git check in 6.855s under a new 15s estimate / 30s stop. The same unmutated fixture was rechecked in 8.636s and used without another copy. The failed phase/log is retained; its overrun is not reclassified as a pass. Only excluded setup gained a prepared-input option, with guards for the exact input and empty home; all measured pipeline bytes and limits are identical, with recorded SHA-256 proof.

No in-progress write was force-killed, and no original/task/basis/result/request-key/digest/intent/protection state was edited or deleted to escape a timeout. Earlier interrupted/retained state keeps its own preservation obligations.

## Regression, evidence and limits

The required complete `pwsh ./scripts/build.ps1` gate passed on this source before matched measurement: locked restore, Release compilation with zero warnings/errors, **2,605 discovered = 2,569 passed + 36 conditional skips**, zero failures/errors/timeouts/aborts. All four added packing/restore cases passed. Whole-command time was **618.07s**, within its 660s estimate / 1,320s stop. The default conservative eight-class schedule and individual timeout bounds are unchanged. Frozen runtime hashes connect that gate to both current measurements; no product code changed afterward.

All eight final Editor logs have no C# compiler errors and show normal cleanup. Unity's internal `UnityEditor.Search.SearchDatabase` startup indexing exception appears in five logs across both versions. The specified code/scene/Play probes passed; this is not a clean-Console or Search-subsystem qualification. Player builds, Unity Test Runner suites, large existing games, general Plastic/None native performance, peak RSS/allocation, disk saturation and OS/power-loss behavior remain unqualified. Large-catalog opt-in was disabled; retained 100k/17GiB fixtures were not resumed. See [validation scope](README.md).

Ignored evidence is under `artifacts/actual-unity-ab-20261104/`: `summary.json`, `matched-plan.json`, `restore-attribution.json`, `regression-summary.json`, focused/full-gate logs, `post-measurement-processes.json` and per-case schemas, responses, hashes and CPU records. `matched-published-1` retains stop-failure recovery; `matched-published-3/fixture-timeout-phases.json` retains the excluded setup overrun. The full gate TRX remains under `artifacts/test-results/`. Fixture homes/bases/results and the matching binaries remain under `.local/`; generated reports do not authorize state deletion.

To reproduce with the retained fixed input, use `python artifacts/actual-unity-ab-20261104/measure-unity.py --runtime <absolute-splinter.dll> --name <fresh-name>`, read each binary's schemas and retain returned identities. The prepared-input option is restricted to verified untouched setup with an empty home. Re-estimate fixture setup before a new block, preserve failed evidence and stop safely at the declared boundary. This command does not authorize integration, deletion or blind retries of uncertain writes.
