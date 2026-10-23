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

## Parallel preparation accounting

The root README groups the following sequential stages as 345.8s of preparation. Within workspace creation and Editor startup, A/B overlap and are counted once. Existing raw receipts give:

| Stage | A seconds | B seconds | Accounted elapsed seconds |
| --- | ---: | ---: | ---: |
| Shared Library preparation | — | — | 61.892252 |
| Parallel workspace creation | 96.714418 | 94.930266 | 96.717762 |
| Parallel Editor launch request → initial compiled readiness observation | 187.175045 | 187.175844 | 187.177205 |
| **Preparation total** | — | — | **345.787219** |

Source receipts are `library-prepare-cost.json`, `s-{a,b}-ensure-corrected-cost.json`, `s-ensure-cohort.json`, `s-{a,b}-initial.json` and `s-initial-cohort.json` in the raw evidence directory above. The initial Editor values are also in the dataset's `perTask.splinter`; its `timingSeconds.splinter` contains all three shared intervals. This is clarification of the recorded run, not remeasurement or qualification of a later product commit.

Editor launch requests began at 15:58:54.491834 / 15:58:54.492075 UTC; the controller confirmed readiness at 16:02:01.669712 / 16:02:01.670754 UTC. Their duration sum, 374.350889s, is not the elapsed interval. `wait_all` checks each pending Editor separately for the expected compiled values, exact process/project identity, fresh observation and no compilation/import in progress, polling every 0.3s; the recorded endpoints are readiness observations, not exact internal completion timestamps. The controller waits for both workspace-creation calls before dispatching the two launches. Neither Editor's 187.2s includes the preceding Library/workspace stages. When starts differ, the cohort interval spans first dispatch to last readiness confirmation; `max(A duration, B duration)` alone need not equal that interval.

## Work and finalization accounting

The same raw-receipt audit covers work, normal shutdown, preservation, merge and reclamation. This adds interpretation of the recorded BC-01 run; the original dataset and timing values are unchanged, and no Editor was launched for the audit.

| Splinter interval | A seconds | B seconds | Accounted elapsed seconds | Execution |
| --- | ---: | ---: | ---: | --- |
| Source edit through compilation confirmation | 29.317619 | 29.304856 | 29.323598 | Overlapping compilation |
| Asset checks through completion dispatch | Not individually timed | Not individually timed | 3.022855 | Sequential A → B checks, then dispatch |
| Checkpoint/release, including normal exit and reclamation | 99.143387 | 100.348323 | 100.351896 | Parallel |
| Combine source results | — | — | 0.307658 | Common operation, once |
| Restore original Library | — | — | 58.801262 | Common operation after both releases |

The edit timers begin 0.017222s apart; `s-edit-cohort.json` measures the enclosing interval rather than adding individual durations. In the retained controller, `wait_all` first confirms both compilations, then `inspect` requests run in a sequential loop. The 3.022855s envelope runs from the end of `s-edit-cohort.json` to the start of `s-completion-cohort.json`; it includes request/response polling, saving observations, a brief read-only asset comparison and dispatch. Individual asset-response timestamps or `renderMilliseconds` do not supply whole per-task inspection durations. The published work subtotal remains **32.346453s**; it is not two complete, independently timed compile-and-inspect tasks running in parallel.

`s-{a,b}-checkpoint-release-cost.json` records each combined call; `s-completion-cohort.json` encloses both. A/B process exit was observed **62.180156s / 62.180289s after the common completion start**, including command dispatch and a 0.2s observer interval (`s-normal-exits.json`). These are included milestones inside 100.351896s, not extra phases or precisely timed stop-only calls. Post-exit source collection, sealing and provider reclamation lack separate timers here; subtraction from a polling milestone is not an independent measurement. A's combined call finishes about 1.2s before B's, but the shared Library restore waits for both. Independently rounding B's 100.348323s and the enclosing 100.351896s explains the README's 100.3s / 100.4s.

| Shared-Editor interval | A seconds | B seconds | Accounted elapsed seconds | Execution |
| --- | ---: | ---: | ---: | --- |
| Code-worktree creation | 13.372978 | 13.369908 | 13.373869 | Parallel |
| Candidate edit/preservation envelope | 1.005833 | 0.741130 | 1.063193 | Union of overlapping intervals |
| Initial Editor readiness | — | — | 25.254704 | One Editor |
| Compilation and asset validation | 13.650431 | 12.746716 | 26.397176 | Sequential A → B |
| Restore original source and compile | — | — | 10.836974 | Common operation, once |
| Normal Editor shutdown | — | — | 2.859526 | One Editor |
| Combine source results | — | — | 0.296239 | Common operation, once |
| Code-worktree reclamation | Not individually timed | Not individually timed | 1.975875 | Sequential A → B |

Candidate envelopes come from `shared/{a,b}-candidate.json` start/end timestamps and include the checks around editing/preservation, not just their smaller inner timers. B begins 0.057360s before A; the union is 1.063193s rather than their 1.746963s sum or the larger individual 1.005833s. `shared/validation-cohort.json` measures A then B in one Editor. B's **13.650455s queue wait** overlaps A's service and is already in 26.397176s; adding it again would double-count. The shared controller removes the two code worktrees sequentially under one timer; dividing 1.975875s into two measured values is unsupported.

The README's grouping reconciles as follows, calculated before rounding:

| Group | Splinter seconds | Shared seconds |
| --- | ---: | ---: |
| Preparation | 345.787219 | 39.691766 |
| Work | 32.346453 | 26.397176 |
| Finalization and remaining accounting difference | 159.467014 | 15.971477 |
| **Complete accounted workflow** | **537.600687** | **82.060419** |

For Splinter, the named finalization calls total 159.460816s; the remaining difference is 0.006198s. For shared execution, the named finalization calls total 15.968615s; the difference is 0.002862s. These are reconciliation residuals, not independently timed cleanup functions. The full Splinter envelope is 537.789238s with 0.188551s of aggregate read-only checks excluded; the retained evidence does not split that exclusion between its two checks. Shared totals use the uninterrupted Editor-workflow timer plus external creation/candidate-union/merge/reclamation timers, not both that timer and its subtotals. Actual interval boundaries govern overlap, common costs count once, and included milestones/subtotals are never added again. This accounting audit does not change the recorded overall differences or establish throughput for a later product revision.

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
