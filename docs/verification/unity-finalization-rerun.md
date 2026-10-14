# Unity workflow with parallel combined completion

[한국어](../ko/verification/unity-finalization-rerun.md)

FU-01 · 2026-10-14 · committed product `07bc2de`. This run applies [LF-01–05](lifecycle-finalization-optimization.md) to the real Unity workload from [RC-01](unity-current-comparison.md), then repeats the [shared-Editor workflow](unity-shared-editor.md) on the same current input. [Dataset](../../reports/benchmarks/unity-finalization-rerun-20261014.json).

## Results

| Complete workflow | Previous measurement | This measurement |
| --- | ---: | ---: |
| Splinter, two independent Editors | 15m 35.2s (RC-01) | **12m 48.3s** |
| One Editor, A/B validation in sequence | 1m 42.5s (SE-01) | **1m 34.0s** |

Splinter's complete workflow is **167.0s shorter (17.9%)** than RC-01. It remains **674.3s longer, or 8.18 times the elapsed time**, than the newly measured shared-Editor workflow for this small one-line change. The new parallel combined completion saves 125.5s against the corresponding historical shutdown/preservation/reclamation interval; it does not remove preparation and Editor initialization costs.

| Splinter A/B stage | RC-01 | FU-01 |
| --- | ---: | ---: |
| Library basis preparation | 60.2s | 62.0s |
| Parallel workspace creation | 168.8s | 162.5s |
| Parallel Editor launch → initial compiled readiness | 310.5s | 248.8s |
| One-line edits → recompiled readiness | 31.7s | 43.3s |
| Selected assets and dispatch | 1.2s | 1.8s |
| Normal Editor exit + preserve/reclaim A/B | 316.3s | **190.9s** |
| Combine independent source results | 0.7s | 0.4s |
| Restore original Library | 45.5s | 58.7s |
| **Total** | **935.2s** | **768.3s** |

The completion row combines RC-01's 116.8s normal shutdown and 199.5s serial checkpoint/release. FU-01 runs **`workspace checkpoint --release` concurrently for A/B**, directly from validated, running Editors. Each command normally stops its owned Editor, preserves the independent result and reclaims its workspace. There is no separate `unity stop`. Both commands succeed on their first invocation, in 188.642s / 190.865s; their combined interval is 190.868s, a **39.7% reduction** from the historical comparable interval. Editor exits are observed 110.661s / 112.696s after dispatch and are already included in that interval. Subtracting the last exit gives 78.173s afterward, but A's preservation can overlap B's shutdown; that remainder is not a separately isolated checkpoint/release benchmark.

Initial readiness is 61.8s shorter. Recompilation is 11.6s longer and Library restore 13.2s longer. Built-in Editor logs record initial script compilation of 127.876s / 127.065s and initialization-attribute processing of 45.587s / 45.648s. Incremental source inspections take about 5.2–5.6s: the first hashes one changed file and reuses 1,601 hashes, and following inspections reuse all 1,602. These are observations, not function-level causal attribution to individual caches.

## Current shared-Editor measurement

| Stage | Seconds |
| --- | ---: |
| Create two code worktrees concurrently | 12.601 |
| Candidate editing and Git preservation, overlapping intervals counted once | 1.493 |
| Launch original Editor → initial readiness | 34.872 |
| A transfer, compilation and selected assets | 13.847 |
| Switch to B, compilation and selected assets | 13.848 |
| Restore original source and recompile baseline | 12.341 |
| Normal Editor exit | 2.448 |
| Combine results | 0.353 |
| Reclaim code worktrees and registrations | 2.166 |
| **Accounted total, including small dispatch gaps** | **93.969** |

Both candidates are ready before validation starts. B waits **13.847s** for A; both validations finish after **27.694s**. Splinter's parallel edit/asset interval is **45.090s** including small dispatch overhead. For this workload, avoiding the shared-Editor queue does not offset per-workspace overhead or make validation faster. Longer Editor operations may have different tradeoffs; no long-operation crossover was measured.

## Input, method and timing boundary

Source is unchanged at `b09fa58e9d40a560d4fe1af7a213f8ac23e8e3b1`: 1,602 tracked files / 2,804,772,822 bytes, Unity 6000.3.22f1. The current Library has **42,418 files / 3,141,789,464 bytes**. Since SE-01's original Editor regenerated its cache, 17 files differ and two files are absent relative to RC-01; there are no added files. Historical Library size was 42,420 files / 3,141,660,252 bytes. The dataset records exact differences and hashes. These historical comparisons therefore include code/scheduling and cache-input changes, not a controlled isolated optimization percentage.

The machine remains Windows 11 Home 10.0.26200 / NTFS, Ryzen 5 5600X (6 cores / 12 threads), 68,628,553,728 bytes RAM. The committed source matches the previously tested frozen LF runtime. Splinter runs first with fresh tasks and its current Library basis; byte-identical original Library restoration is verified before the new shared-Editor run. Both methods use the same raw source and current Library input. Execution order is fixed; OS caches are not cleared.

Each task changes its assigned C# expression, explicitly refreshes and verifies fresh loaded values, assembly MVID/generation and no compile errors. A expects `(5,6,13)`, B `(4,9,13)`. The same existing external qualification probe checks selected asset GUIDs, meshes, texture, scene, dependency hash and rendered-preview hash. The shared Editor restores A while applying B in one refresh, then restores and recompiles `(4,6,13)`. The fresh shared cohort prepares candidates with concurrent scripted Git operations, not new Codex chats or newly timed AI sessions. AI reasoning and app handoff are outside both timing scopes.

Splinter's timer spans Library preparation through final restoration, with 0.153s of measured read-only comparison checks excluded. Source/Library input hashing and final validation are outside that timer. A/B overlap counts once. Shared-Editor time adds worktree creation, active candidate edit/preservation intervals, uninterrupted Editor workflow, source merge and reclamation. Small dispatch gaps remain; totals are not elapsed conversation time. No new allocation scan or process sampling profiler runs; lightweight existing provider observations and built-in Editor logs are retained.

## Closure and limits

Both Splinter tasks finish `released`; independent results match RC-01's result trees. Owned Editors exit normally with code 0, and providers, mounts, deltas, registrations and owned resource counts are absent/zero. Original source hashes, HEAD, branch, existing refs/worktrees and configuration are preserved. The temporary local raw-EOL setting is restored. The original Library returns to ordinary NTFS with all input file bytes unchanged before the shared run.

The shared run restores all 1,602 original source files, recompiles the baseline, exits normally and removes both code worktrees/registrations. Its Editor normally regenerates Library cache bytes; these are not rolled back. Both methods retain independent results and the same conflict-free combined tree, `756e0a926577c9bcff036c823b4816b3b2d5fb19`, without overwriting the original. Thirteen CLI responses (eight runtime operations and five schema reads) pass the current response schema. No service, UAC, package or global-setting changes were required.

One cohort per method is measured. Play Mode, Unity tests, game build, combined-result compilation, new two-Editor Codex worktrees, storage peak, whole-system RAM and long-task crossover are **NotRun**. Earlier storage figures retain their own RC-01 input/version boundary; FU-01 is a time comparison.
