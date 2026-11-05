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
