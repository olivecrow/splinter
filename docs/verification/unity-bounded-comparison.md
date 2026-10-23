# Current bounded-journal Unity comparison

[한국어](../ko/verification/unity-bounded-comparison.md)

## BC-01 — same-input parallel Editors and one shared Editor

Measured on **2026-10-23 KST**, product commit `c4fa5063964e7b8456de4ea7735c042fb9af8930`. The current bounded default completed the workflow in **537.6s**, versus **82.1s** with parallel code preparation and one sequential shared Editor. Splinter took **230.7s less** than historical [FU-01](unity-finalization-rerun.md), but **455.5s more** than the shared Editor in this small workload. These are differences in the observed run, not a measured scaling law. This is a new measurement of both methods, not reuse of the historical shared result.

Exact values, candidate/source/runtime hashes, stage intervals, result identities and closure evidence are in [the dataset](../../reports/benchmarks/unity-bounded-comparison-20261023.json). Raw evidence is retained locally in `artifacts/unity-bounded-comparison-clean-20261023`; the interrupted diagnostic run is separately retained in `artifacts/unity-bounded-comparison-20261023`.

## Input and timing boundary

- Original fixture: `splinter-unity`, commit `b09fa58e9d40a560d4fe1af7a213f8ac23e8e3b1`, **1,602 tracked files / 2,804,772,822 bytes**. Unity **6000.3.22f1**, Windows 11 Home **10.0.26200**, Ryzen 5 5600X **6 cores / 12 threads**, installed RAM **68,628,553,728 bytes**.
- The new pair starts from byte-identical source and **42,424 Library files / 3,142,485,226 bytes**. Full file hashes prove capture and restoration before the shared run. Compared with FU-01, Library has **6 added, 0 removed and 37 changed files**; FU-01 used 42,418 files / 3,141,789,464 bytes. Historical and current Library inputs are therefore not identical.
- Both methods use the same scripted A/B one-line changes and parallel code preparation. Splinter uses independent `workspace ensure` calls with `git-snapshot`, an explicit shared Library basis and the returned project paths. Actual A/B responses report `journalStorageMode=bounded`, `journalOptimizationEnabled=false`, **16MiB / 4,096-frame** suffix limits. No mode override or product modification was made.
- Each Editor proves baseline values, a new compilation generation and assembly MVID after explicit refresh, isolated expected values, no compiler errors, and the same selected asset/scene checks. Shared validation restores A while applying B in one refresh, then restores and recompiles the original. The fixture's existing external qualification controller performs these operations; this is not a product Unity API or an MCP/Unity CLI qualification.
- Splinter timing runs from Library preparation through Library restoration, including parallel `checkpoint --release` and normal Editor shutdown. Shared timing includes parallel code-worktree creation, the union of candidate edit/preservation intervals, the uninterrupted Editor workflow, source-result merge and worktree removal. Overlapping A/B intervals count once. Dispatch gaps within those intervals are included; AI reasoning/chat startup, build/tests and read-only input/final verification are excluded.

## Stage comparison

Seconds, rounded to one decimal. Historical FU-01 is SJL4; current BC-01 uses bounded SJR2. These are whole-candidate observations, **not the isolated causal effect of changing the journal**.

| Stage | FU-01 Splinter | BC-01 Splinter | FU-01 shared | BC-01 shared |
| --- | ---: | ---: | ---: | ---: |
| Library basis preparation | 62.0 | 61.9 | — | — |
| Parallel A/B workspace creation | 162.5 | 96.7 | 12.6 | 13.4 |
| Initial compiled Editor readiness | 248.8 | 187.2 | 34.9 | 25.3 |
| A/B edits through compilation | 43.3 | 29.3 | Included below | Included below |
| Asset checks / remaining dispatch | 1.8 | 3.0 | Included below | Included below |
| A/B validation, compilation + assets | 45.1 | 32.3 | 27.7 | 26.4 |
| Parallel checkpoint/release, including shutdown | 190.9 | 100.4 | — | — |
| Restore original source and compile | — | — | 12.3 | 10.8 |
| Shared Editor shutdown | Included above | Included above | 2.4 | 2.9 |
| Combine independent source results | 0.4 | 0.3 | 0.4 | 0.3 |
| Restore original Library | 58.7 | 58.8 | — | — |
| Code-worktree reclamation | Included above | Included above | 2.2 | 2.0 |
| Parallel candidate edit/preservation | In edit/completion | In edit/completion | 1.5 | 1.1 |
| **Complete accounted workflow** | **768.3** | **537.6** | **94.0** | **82.1** |

The combined validation row is a subtotal, not an additional stage. Totals include small dispatch gaps. Splinter's workspace creation fell **40.5%**, initial readiness **24.8%**, recompilation **32.2%**, and combined completion **47.4%**. Library preparation/restoration were effectively unchanged. Shared total time also fell **12.7%**, especially startup; this reinforces the need to separate implementation effects from cache/run variation.

Current shared A/B service times are **13.65s / 12.75s**, including assets, with B waiting **13.65s** for A. Splinter's parallel compile-plus-assets interval is **32.35s**, compared with **26.40s** serially. It saves no validation time in this short-work pattern despite removing the Editor queue.

## Conditional crossover

Let `E` be each equally long task's Editor time when run alone in the shared Editor, `r` its duration multiplier during parallel execution, and `H` the extra lifecycle cost outside that work. The model compares `2E` with `H + rE`. Splinter wins only for `r < 2` and `E > H / (2 - r)`.

Removing the observed compilation/asset intervals gives:

`H = (537.600687 - 32.346453) - (82.060419 - 26.397176) = 449.590990s`.

Historical FU-01 gives **656.887147s** from the full-precision dataset. The new estimate is **31.6% lower**, approximately **7m 29.6s** instead of **10m 56.9s**. This includes initial readiness and restoration; it is not only the `ensure` or shutdown command time.

| Assumed parallel multiplier `r` | Historical threshold per task | Current threshold per task | Current shared serial total |
| --- | ---: | ---: | ---: |
| 1.00× | 10.95 min | **7.49 min** | 14.99 min |
| 1.25× | 14.60 min | **9.99 min** | 19.98 min |
| 1.50× | 21.90 min | **14.99 min** | 29.97 min |
| 1.80× | 54.74 min | **37.47 min** | 74.93 min |
| 2.00× or more | No crossover | **No crossover** | — |

The measured short compilation/asset pair took **5.949278s more** in parallel. Its balanced equivalent ratio `2 × 32.346453 / 26.397176 = 2.45` describes this observation only. It does not establish proportional slowdown as workload grows, nor that the added time stays fixed. The dataset's `observedCompilePatternCrossoverExists=false` is conditional on holding that ratio constant in the model, not an empirical finding for longer work. If the same pair repeatedly retained the same 5.949278s disadvantage, that disadvantage would accumulate; repetition was not measured.

For an additive alternative, write the parallel work interval as `E + D(E)`: crossover requires `E > H + D(E)`. If `D(E)` were a fixed 20s per parallel work interval, the conditional threshold would be `449.590990 + 20 = 469.590990s` per task. Only when `D(E) = (r - 1)E` does the proportional table apply. Distinguishing per-refresh fixed cost, workload-proportional cost and mixed behavior requires measurements across operation counts and workload sizes. Preparation/completion is incurred per workspace lifecycle, but its value can itself depend on project size and accumulated changes. The table holds `H` fixed and assumes actual overlap; longer tasks may also increase checkpoint/restoration cost. Unequal tasks require `E_A + E_B > H + r × max(E_A, E_B)`. Longer AI coding time alone does not help because code preparation can be parallel in both methods.

## Interrupted diagnostic and final closure

The earlier diagnostic was not included in the normal workflow total. Its historical PNG-equality assertion failed: current renders have **9,457** foreground pixels instead of **9,529**, while selected asset identities, scene/dependency checks and valid rendering matched. The cause of the pixel difference is unisolated. The revised controller retains exact asset checks, request/hash verification, nonempty rendering and scene restoration, and records pixel/hash differences rather than requiring a historical image byte match. All four images in the final new pair have the same SHA-256, `df94cefbc5f0d2fa6b46b878badb8848bc0221d66130778347349725a47b632d`; representative Splinter/shared PNGs were visually inspected.

During diagnostic cleanup, one Unity Editor showed access denied while saving user preferences at `Editor-5.x/Preferences/Overlays/CanvasesSaveData.asset`. After its peer exited, **Try Again** was selected once; the dialog closed and the Editor exited. The file/ACL/global preferences were not manually changed and Force Quit was not used. The failed `EditorStopTimeout` response was retained, and the same workspace's checkpoint/release was resumed to completion before restoring the original Library. This observation does not prove the cause of the access denial or permanently fix Unity's shared-preference behavior.

The new measured pair completed without that popup intervention, timeout or recovery. Both Splinter Editors have normal exit receipts (exit code **0**, active job processes **0**), retained independent results, absent owned mounts/deltas and exited providers. Before shared validation, the original Library was ordinary NTFS and byte-identical to the input. After shared validation, all **1,602** source files, original HEAD/branch/configuration, preexisting refs/worktrees, baseline compilation and owned Editor/worktree closure were verified. Shared Editor's normal Library cache regeneration is retained.

Both methods preserve the same combined source tree, `756e0a926577c9bcff036c823b4816b3b2d5fb19`, without applying it to the original. `scripts/build.ps1` passed **1,691 tests, 0 failures, 24 skips**. All **17** selected current response objects (**8 runtime / 9 discovery**) passed the current schema test; the initial test-input selector accidentally included an array and was corrected without altering response payloads.

**Limits:** one ordered Splinter-then-shared pair after a diagnostic run, no OS cache clearing or randomized repetitions. Diagnostic initial readiness was approximately **251.6s**, versus **187.2s** in the final cohort; variability is material, and the final run is not a cold-start guarantee. No journal-only causal percentage, general Unity support, long-task crossover, Play Mode, Unity tests, game build, combined-result compilation, storage peak, whole-system RAM or 3–4 Editor scaling is qualified. Historical storage figures keep their original RC-01 boundary.
