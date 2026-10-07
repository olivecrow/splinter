# Checkpoint and release profiling

[한국어](../ko/verification/checkpoint-release-profiling.md)

This document owns **LP-01–04**, the bounded 2026-10-07 diagnosis requested after the [README Unity comparison](readme-unity-comparison.md). It separates checkpoint, release and their internal costs. [Public measurements](../../reports/benchmarks/checkpoint-release-profile-20261007.json) contain phase aggregates and diagnostic binary identities. No product optimization, new Unity run, failure matrix or support pass is claimed.

The subsequent implementation and normal-binary before/after measurements belong to [CO-01–04](checkpoint-release-optimization.md). The diagnostic numbers below remain unchanged.

## Scope and method

- **LP-01:** Reuse the completed large Unity run's command intervals, source-inspection/seal timers and durable events. No large assets were read again.
- **LP-02:** Run two sequential Editor-free workspaces on a new Git fixture: **64 tracked files / 5,510 B**, a managed Library basis of **256 synthetic files / 65,536 B**, `git-snapshot`, Unity directory policy and default SJL4 optimization. Each changes one source line; the second also changes one byte in 128 Library files, excluded from source results.
- **LP-03:** Instrument ignored copies of Git/Runtime assemblies with Stopwatch scopes for command categories, journal seals and reclamation sections. Accumulate inventory counters in memory and emit them at normal process exit. Both cases use identical diagnostic binaries; original source/distribution stay unchanged.
- **LP-04:** Profile the current fence-membership loop with 256/512/1,024 real one-byte NTFS files, after a 32-file warmup. One pass per size; close and remove probe files afterward.

Diagnostic builds used locked restore and passed with zero warnings/errors; 41 other copied distribution files matched the original. The `Bytes` getter counts files through equivalent explicit enumeration instead of LINQ `Sum`. Timing/logging overhead is included and unquantified: this is diagnostic evidence, not exact unchanged-binary performance. Git strips inherited `GIT_*` variables, so central command scopes were used instead of assuming external Trace2 settings took effect.

Each native command had a 240-second observation bound, without automatic force termination/retry; none reached it. No Unity Editor, import, compilation, Play Mode, tests, game build, VSS or installed-service change was needed. Cases ran once in order without clearing OS caches. The approximately 4.4-second separate microprobe overlapped first-case setup before checkpoint. Provider counters cover process lifetimes, not exclusive command time. Nested spans must not be added to their parent totals.

## Checkpoint: repeated verification dominates

| Tiny case | Command, s | Source inspections | Inspection time, s | Share | Git calls / time, s |
| --- | ---: | ---: | ---: | ---: | ---: |
| One source edit | 41.9986 | 7 | 33.9070 | 80.73% | 190 / 20.6472 |
| Same edit + 128 private Library edits | 41.9940 | 7 | 33.6543 | 80.14% | 190 / 21.9802 |

Git time **overlaps** inspection time: 15.9413 / 17.0960 seconds occurred within inspection windows. Non-Git inspection remainders were 17.9657 / 16.5582 seconds, including mounted-path checks, source barriers and manifest publication; these are not measured lock-wait shares. Only 89 source bytes were hashed per checkpoint.

The seven inspections are initial verified View; Git checkpoint before staging, after staging and after commit; collection inspection; independent-preservation inspection; final verified View. See [coordination](../../src/Splinter.Runtime/WorkspaceCoordinator.cs), [Git checkpoint](../../src/Splinter.SourceControl.Git/GitWorkspaceProvider.cs), [collection](../../src/Splinter.Runtime/SnapshotWorkspaces.cs) and [independent preservation](../../src/Splinter.SourceControl.Git/GitSnapshotWorkspaces.cs).

Hash reuse still executes membership/attributes/configuration and per-path metadata checks. With 64 paths in two 50-path batches, checkpoint recorded **68 sequential attribute commands**: 28 all-attribute, 20 current filter/encoding and 20 cached filter/encoding. The structure is `34 × ceil(N / 50)` for unchanged membership: **1,122** at 1,602 paths. The large count is derived from code, not separately traced.

[`GitSourceFingerprint`](../../src/Splinter.SourceControl.Git/GitSourceFingerprint.cs) uses before/after source barriers and plain-path/file-existence checks even for reused hashes. The [provider loop](../../src/Splinter.Platform.Windows/WinFspOverlayServer.cs) processes source/flush requests and sleeps one second; its separate wait share was not instrumented. These fixed waits and the previously observed slow mounted metadata access compound repeated inspection.

| Preserved Unity interval | Command, s | Inspection count / seconds | Inspection share | Separate seal time, s |
| --- | ---: | ---: | ---: | ---: |
| A checkpoint | 5,723.6083 | 7 / 4,673.1192 | 81.65% | 295.6585 |
| B checkpoint after external source commit | 2,914.6685 | 5 / 2,450.3633 | 84.07% | 61.7758 |

The first large inspection hashes only 143 B / 152 B for A/B; later inspections hash zero bytes and reuse all 1,602 hashes. Large asset hashing does not explain repeated inspections. B already had a commit and skipped changed-file staging/commit; its five-inspection path is not an optimized version of A.

**Final response construction repeats verification.** A's `checkpoint-completed` durable event preceded the response by 756.7852 seconds, including a 750.4229-second final inspection. B's gap was 374.6007 seconds, including 372.2619 seconds of inspection. These are already within the command totals.

## Release: recollection precedes reclamation

| Tiny case | Command, s | Inspections / seconds | Git calls / seconds | SJL4 reclaim core, s |
| --- | ---: | ---: | ---: | ---: |
| One source edit | 20.0513 | 2 / 10.0731 | 69 / 7.2679 | 0.0495 |
| Same edit + 128 private Library edits | 20.5481 | 2 / 9.8088 | 69 / 7.6594 | 0.5111 |

A fresh release after checkpoint takes the available-basis path and **collects the result again**: two source inspections, an ignored-tree scan and two collection seals. It also verifies the result object graph before unmount and during unregister. Tiny release recorded **20 attribute commands**; the structure is `10 × ceil(N / 50)`, or 330 at the earlier input. See [release/collection](../../src/Splinter.Runtime/SnapshotWorkspaces.cs) and [Git preservation/result verification](../../src/Splinter.SourceControl.Git/GitSnapshotWorkspaces.cs).

The ignored scan runs `git ls-files --others --ignored --exclude-standard` before filtering managed paths, so it can traverse Library. Its independent large-run contribution was not measured. Both release inspections reuse all hashes and hash zero content bytes.

| Preserved Unity interval | Command, s | Inspection, s / share | Seal time, s | Release-delta event stage, s |
| --- | ---: | ---: | ---: | ---: |
| A release | 1,662.4729 | 1,041.2644 / 62.63% | 133.3495 | 264.3112 |
| B release | 1,163.3742 | 729.9124 / 62.74% | 95.1730 | 187.6621 |

The first `release-unmount` event occurs 1,356.4047 / 946.3805 seconds into A/B release: most time passes **before unmount starts**. Subsequent event intervals are unmount/post-stop seal 38.4430 / 27.4285 seconds, Git unregister 2.1364 / 1.3227 and final basis release 0.9310 / 0.4277. The delta stage includes its outer seal/state publication and overlaps the seal column; do not add it again.

## Private journal reclamation has a quadratic check

[`LatestJournalReclamation`](../../src/Splinter.Runtime/LatestJournalReclamation.cs) opens file fences while preparing inventory, then checks each inventory file with `fences.Any(f => WorkspaceStore.SamePath(f.Name, file))`. These fences already exist on the fresh path. Each linear scan repeats [SamePath](../../src/Splinter.Runtime/WorkspaceStore.cs) full-path normalization.

| Microprobe files | Exact comparisons | Current loop, ms | Additional fences |
| --- | ---: | ---: | ---: |
| 256 | 32,896 | 53.5754 | 0 |
| 512 | 131,328 | 211.4900 | 0 |
| 1,024 | 524,800 | 449.5459 | 0 |

Counts equal `N × (N + 1) / 2`, proving redundant quadratic work. Approximately 16,259 earlier inventory files put the mechanism at the order of 132 million comparisons. Cache/JIT variation prevents extrapolating durations or assigning the entire historical delta-stage remainder to this loop.

Reclaim also performs an internal seal, per-file hash inventory, two inventory validations and hash-before-delete. For 128 private Library edits, internal seal took 0.1556 seconds, validations 0.1205 / 0.1354, fence membership 0.0133 and hash/delete 0.0508 within the 0.5111-second core. The [Windows outer delta reclaim](../../src/Splinter.Platform.Windows/NtfsSnapshotWorkspaceBackend.cs) adds another seal, ownership observation and durable intent before this core.

## Journal observation adds metadata cost

[`LatestJournalStorage.Bytes`](../../src/Splinter.Runtime/LatestJournalStorage.cs) enumerates files/lengths under [`JournalDeltaStore.Observe`](../../src/Splinter.Runtime/JournalDeltaStore.cs)'s store gate. The provider loop and [volume-info callback](../../src/Splinter.Platform.Windows/WinFspOverlayFileSystem.cs) invoke it repeatedly. Provider-lifetime totals were 733 calls / 177.039 ms versus 992 / 3,897.345 ms; inventory growth differs, so these are not equal-duration comparisons or command-exclusive shares. The earlier 16,259-file probe took 0.8914 seconds for one enumeration. Repeated locked work is established, but its exact earlier command contribution remains unmeasured.

## Conclusions and closure

Checkpoint's primary observed cost is repeated source verification, including the final response inspection. Release repeats collection before reclamation; delta removal adds repeated validation/hash passes and quadratic fence membership. These paths do not require a Unity Editor. Subsequent targets are fewer equivalent inspection/attribute passes, result reuse behind the same generation/seal/content guards, indexed fence membership retaining ownership/path checks, and bounded inventory observation outside important callback work. No guard was removed and no optimization was implemented.

Both tiny tasks reached `released`, retained one-source-file independent results and had zero owned resources with absent mount/delta/Git registration. Tiny Library was restored to ordinary NTFS. Original hashes of 64 source and 256 Library files matched, original HEAD stayed fixed and owned basis refs were absent. Post-run verification also matched 45 original source/configuration hashes and four original Git/Runtime DLL/PDB hashes. The existing Unity project was untouched. Raw scopes/receipts, instrumentation patch and probes remain in ignored `artifacts/lifecycle-profile-20261007/` and `.local/lifecycle-profiler-20261007/`; public data excludes user/proprietary paths, images and tokens.
