# Actual Unity parallel workspace performance

The current catalog/physical-proof candidate completes the matched short-path Unity A/B workflow in **315.26s versus 324.67s public mean**, a **2.9% reduction**. Library restoration is **24.67s versus 25.45s**, a **3.1% reduction**. **The below-half-public-time target is not met.** Two observations per version and uncontrolled host activity do not establish a general or causal throughput guarantee.

## Changes and small reproduction

Current follows local commit 2c0c8bb. Persisted formats, full byte/native-identity/membership proof, ownership fences, metadata restoration, durable publication and same-request recovery are unchanged.

- Membership merges one complete checked catalog traversal with sorted native component cursors for its open ancestors. It no longer rereads every directory's children or resolves each parent from the catalog root. Unknown directories are never traversed; general restoration still permits missing planned paths, while unchanged restoration requires exact membership.
- Catalog construction consumes that exact membership stream. After the root move, the same checked stream feeds native file verification, removing another standalone catalog pass.
- Up to eight demand-driven workers overlap catalog production with native opens and complete hashes. There is no per-128-file hash barrier or unbounded metadata queue. Ordered chunks retain only the mandatory physical handles; all workers join before success, failure or cancellation can close them. Metadata and a separate final membership proof still precede verified publication/reclamation.
- Every empty hidden-path index is validated, but needs no per-child ancestor digest. Nonempty trees retain their ordinary lookups/backing checks. A proposed private-child-root cache was removed because it could not establish the required reader-fence lifetime.

The fresh small reproduction uses 1,025 generated files plus three fixed files under deep paths, checking every byte/native file/root identity, ancestor-movement refusal and final reclamation. An isolated 2c0c8bb source build and current use identical test source. Order is baseline/current/current/baseline, with no concurrent benchmark or OS-cache reset.

| Restore-only small sample | Previous 2c0c8bb | Current |
| --- | ---: | ---: |
| First | 2.901776s | 1.840096s |
| Second | 1.839234s | 1.587919s |
| Mean | **2.370505s** | **1.714008s** |

This is a **27.7% observed reduction** with substantial variation, not whole-lifecycle qualification. Whole-test-process CPU, including fixture setup, averages 8.16 versus 7.46 CPU-s; it is not restore-only CPU. Internal opt-in phase timing is available with SPLINTER_PAGED_RESTORE_PROFILE=1. The 40-case focused selection passes in 82.633s. Added cases cover three physical-proof/metadata interruptions and unknown native items before, within and after known subtrees; rejected resumed trees also prove all leaf fences were released.

An initial combined diagnostic build/reproduction exceeded its 60s stop by 0.86s, although both ended safely. Its overrun remains recorded. Subsequent build and execution scopes use separate estimates and stops; no product or individual test timeout was increased.

## PackageCache failure and prevention

The earlier long-path experiment exposed a real qualification defect: the 5,120-byte Unity.Collections.LowLevel.ILSupport.dll existed and had the expected SHA-256, but its workspace path was 273 characters. The installed Unity 6000.3.27f2 Mono reader threw DirectoryNotFoundException on that path. The 239-character original-view path and extended-path spelling of the same long file both read the expected bytes. A fresh 5KiB reproduction repeats the failure and successful reads. Modern .NET file access alone did not qualify Unity's reader; [Windows path handling also depends on the selected API and application](https://learn.microsoft.com/en-us/windows/win32/fileio/maximum-file-path-limitation).

Fresh comparison inputs/state use dedicated short C:/sp roots. Before either Editor starts, all **14,621 PackageCache file paths** must remain below 260 characters (observed maximum **252**), and the installed Unity Mono runtime must read/hash all **53 package DLLs per side** against the frozen input. Post-start and final logs must have no PackageCache missing-file errors, opening-file-failed dialog or Force Quit. These checks are identical for public and current. No existing task identity/path was relocated and no package payload or global setting was repaired to mask the failure.

One long-path attempt timed out in A Play verification at its existing 60s stop. B stopped normally; A's managed stop also reached the existing two-minute bound. UI inspection found a separate missing user-global Overlays/CanvasesSaveData.asset dialog. The user selected Force Quit. This is recorded as a forced A exit, not a normal stop or passing timing. Both complete source results and the complete ordinary original were subsequently preserved/verified. Computer Use was released immediately after each inspection.

Two prelaunch short-path attempts were rejected by diagnostic stdout parsing: Mono UTF-8 output was decoded through the host's Korean codepage. Neither launched an Editor. Binary UTF-8 decoding was verified with the exact preflight function, all 53 hashes and the 252-character bound. Both attempts' source results and originals were fully preserved/restored. They remain excluded failures; their recovery does not turn them into timing samples.

## Fixed input and matched method

Measured on Windows on 2026-11-04 (Asia/Seoul). The public baseline remains the frozen c4fa5063964e7b8456de4ea7735c042fb9af8930 binary from the preceding investigation. This report does not assert a later live GitHub-tip check or publication of local commits.

The dedicated input is unchanged: Unity **6000.3.27f2**, URP **17.3.0**, **75 source files**, and **17,863 genuinely generated Library files totaling 1,568,757,165 bytes**. Its local input commit is 7ee96cd995bcb77b345f0028cdcdd351a7a34417. Cold import and the approved template material upgrade precede that input.

Final order is **public/current/current/public**, cases sp-p4, sp-c1, sp-c2, sp-p2. Each has a fresh source/home and complete pre-input SHA-256/length and clean Git proof. The strengthened short-path method is rerun for both versions; prior long-path measurements do not enter these means.

Preparation runs once per pair. A/B creation, complete source reads, Editor readiness, code work, scene work, Play and final preservation/reclamation run concurrently. Normal stop requests run A then B in both versions because Unity shares user-global settings. Each launch PID/project path is matched to its receipt. A compiles values (11,20), B (10,22), with changed assembly MVIDs; each saves its distinct scene marker/position, runs at least 15 actual Update frames, exits Play and rechecks isolation.

Accounted time includes product phases and dispatch gaps. Excluded, mandatory checks are fixture/schema/input proof, package Mono preflight, independent post-release result proof and final out-of-band original hash/Git proof. Both versions use the same modified driver and phase bounds. There is no concurrent regression or timing trace during measurement, no cache reset, and no intervention in any final measured Editor.

The 12-logical-processor, approximately 64GiB host has unrelated Unity activity; it was not stopped or controlled. Fixtures take 110-121s, and normal stop still varies substantially. Fixed order and two observations do not remove these limits.

## Actual Unity measurements

| Accounted lifecycle | Public | Current |
| --- | ---: | ---: |
| First | 343.40s | 316.82s |
| Second | 305.95s | 313.70s |
| Mean | **324.67s** | **315.26s** |

The whole-workflow half target is **162.34s**; restoration's half target is **12.72s**. Neither target is met.

| Phase, mean of two | Public | Current |
| --- | ---: | ---: |
| Library preparation | 36.17s | 24.95s |
| Concurrent A/B creation | 41.55s | 20.70s |
| Complete source reads | 0.37s | 0.37s |
| Two Editors ready | 68.45s | 85.58s |
| Refresh/compilation | 12.26s | 28.57s |
| Scene save | 1.52s | 2.68s |
| Play | 9.09s | 9.13s |
| Final source proof | 0.79s | 0.84s |
| Sequential normal stops | 93.11s | 89.64s |
| Concurrent preservation/reclamation | 35.88s | 28.10s |
| Library restoration | **25.45s** | **24.67s** |

Preparation and creation save 32.07s together, but readiness and compilation add 33.43s. Current Editor readiness plus normal stop alone totals **175.22s**, exceeding the whole-workflow half target. Restoring Library alone cannot meet that target while the other observed costs remain.

Post-hoc plan/cursor timestamps approximate wall intervals, not exclusive function profiling:

| Restore interval, mean | Public | Current |
| --- | ---: | ---: |
| Through plan publication | 13.63s | 10.59s |
| Plan through verified cursor | 9.82s | 13.75s |
| Remaining completion | 2.00s | 0.33s |

| Own CLI CPU, mean | Public | Current |
| --- | ---: | ---: |
| Library preparation | 23.64 CPU-s | 31.09 CPU-s |
| A/B creation sum | 11.60 CPU-s | 12.73 CPU-s |
| Preservation/reclamation sum | 13.82 CPU-s | 32.30 CPU-s |
| Library restoration | 26.47 CPU-s | 45.13 CPU-s |

Current restore averages **1.83 own-CLI cores**. Redundant passes/barriers were removed, but ordered metadata production, decoding/native calls and physical proof remain costs; the post-plan interval remains slower than public. More workers alone has not established a gain, and the full pipeline is not proven optimal. These counters exclude child/provider processes and do not establish whole-host utilization, peak RSS, disk saturation or a particular external wait owner.

## Verification and limits

All four final cases preserve separate A/B results, compare every expected source hash, prove absent owned providers/mounts/deltas and zero owned counters, and restore all original source/Library hashes, clean HEAD, one Git worktree and ordinary NTFS Library. Public independent results are read through Git archive bytes; current uses the complete paged result file catalog while product capture/reclamation proves physical backing. No result is applied to the original.

All eight final Editor logs have zero C# compiler errors, normal cleanup and no PackageCache I/O or forced-quit errors. The independent Unity Search startup indexing exception remains in **five of eight logs** across versions. This is not clean-Console or Search qualification.

The mandatory complete `pwsh ./scripts/build.ps1` gate passes: locked restore, Release build with zero warnings/errors, **2,623 discovered = 2,587 passed + 36 conditional skips**, zero failures/errors/timeouts/aborts. Whole-command time is **632.26s** against a 780s estimate / 1,560s stop. The checked-in eight-class schedule and individual timeout/cancellation bounds are unchanged. All ten rebuilt runtime DLL hashes match both final current Unity samples. Large-catalog opt-in is disabled.

Phase limits remain fixed: fixture90s/180s, prepare45s/90s, A/B creation45s/90s, Editor readiness90s/180s, compilation60s/120s, scene20s/40s, Play30s/60s, normal stops120s/240s, capture180s/360s, restore240s/480s. Package preflight is30s/60s and excluded but mandatory. Recovery has its separately recorded estimates/stops; it never changes an in-flight phase's deadline.

Player builds, Unity Test Runner suites, general Plastic/None performance, large games, peak RSS/allocation, disk saturation and OS/power-loss behavior remain unqualified. Large-catalog opt-in is disabled and retained100k/17GiB fixtures are not resumed. See [validation scope](README.md) and [Library contracts](../git-library-reference.md).

Ignored evidence is under artifacts/catalog-physical-20261104/: paired-small-summary.json, focused-summary.json, short-matched-plan.json, short-summary.json, mono-small-proof.json, final case schemas/responses/hashes/CPU, full-gate logs and commit/process proof. Rejected long/prelaunch cases retain recovery-proof.json and raw logs. Original/home/result/request/basis state remains retained in .local/ or the dedicated C:/sp case roots; ignored status does not authorize deletion. Earlier full A/B reports are available from Git history.
