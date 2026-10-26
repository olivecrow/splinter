# README Unity time recheck

[한국어](../ko/verification/unity-readme-recheck.md) · [Dataset](../../reports/benchmarks/unity-readme-recheck-20261026.json)

**RR-01, 2026-10-26–27 KST.** User-authorized remeasurement of README BC-01 after committing PR-01–04 as `2dff94d`. Current normal whole workflow: **428.6s Splinter / 102.0s shared**, **326.6s extra**. This remains a time cost for the two one-line edits.

## Historical and current observations

| Observation | Splinter (s) | Shared Editor (s) | Extra Splinter time (s) |
| --- | ---: | ---: | ---: |
| BC-01 historical record | 537.6 | 82.1 | 455.5 |
| RR-01 current normal run | 428.6 | 102.0 | 326.6 |

The historical difference is **109.0s / 20.3% less Splinter time**, with **129.0s / 28.3% less extra time relative to shared**. Historical and current Library differ: no added files, six removed, 33 changed. The shared time rose by 19.9s, largely in code-worktree creation (13.4 → 35.0s); its cause is unisolated. These are observations, not isolated product causality or a general multiplier/crossover.

## Fixed input and method

Windows 11 Home 10.0.26200, Ryzen 5 5600X (6 cores/12 threads), 68,628,553,728 bytes installed RAM, Unity 6000.3.22f1. Source `b09fa58e9d40a560d4fe1af7a213f8ac23e8e3b1`: 1,602 tracked files / 2,804,772,822 bytes. Final fixed Library: 42,418 files / 3,144,468,664 bytes. Complete bytes, per-file mtime and attributes match before the frozen old candidate, current candidate and shared workflow; neither comparison excludes individual files. Inputs/closure hashes are outside timed intervals.

Frozen BC-01 `c4fa506` and the source-verified current `2dff94d` use the same Git source and explicit Library basis. Current assemblies are the final passing PR-04 build; their informational commit predates the final commit, while all recorded source/runtime hashes match. The source gate was 1,618 passed / 0 failed / 28 skipped. No product code was changed for this measurement. Old/current/shared are ordered single observations with warm OS caches and no concurrent build/test load.

The existing external fixture controller verifies exact project/PID/start identity, baseline (4,6,13), explicit refresh, new generation/MVID, isolated A=(5,6), B=(4,9), no compilation/import in progress or compiler errors, selected model/texture/scene identities and actual PNG. Initial loaded MVIDs differ and are retained in the dataset; identical starting files do not imply an identical loaded-assembly experiment. All six inspected PNGs have the same SHA-256; a representative current rendering was visually inspected. Shared validation restores A while applying B, then restores/recompiles the original. The merged result is not recompiled or applied to original HEAD. No Play/Test/Player build or new general MCP/CLI qualification.

## Matched candidate stages

| Same-input stage | c4fa506 rerun (s) | 2dff94d (s) | Saved (s) |
| --- | ---: | ---: | ---: |
| Library preparation | 59.2 | 37.7 | 21.5 |
| Workspace creation | 93.1 | 64.5 | 28.6 |
| Initial Editor readiness | 147.9 | 164.6 | -16.7 |
| Compilation and asset checks | 35.0 | 48.8 | -13.8 |
| Library restoration | 47.5 | 22.5 | 25.0 |

Library preparation + workspace creation + Library restoration: **199.8 → 124.6s**, **75.1s / 37.6% saved**. Initial readiness and validation regressed in this single pair. These measured stages support reduced preparation/restoration cost, not universal compile speed.

Both old Editors returned `EditorStopTimeout` after about 121.9s, subsequently exited normally (last receipt at 164.2s), and were preserved/released under the same IDs/home after exit proof; the resumed completion took 37.6s. Continuous recovered wall time is **689.2s**, including operator gaps. Reconstructing measured stages while excluding 104.3s of gaps/remaining dispatch yields **584.9s**, which is still a recovered workflow, not a clean normal baseline. No normal matched whole-workflow percentage is claimed. Current completion succeeded in one invocation.

## Current whole-workflow accounting

| Current Splinter stage | Elapsed seconds |
| --- | ---: |
| Library basis preparation | 37.7 |
| Parallel A/B workspace creation | 64.5 |
| Initial compiled Editor readiness | 164.6 |
| Parallel edits through compilation | 47.2 |
| Checkpoint/release including normal shutdown | 90.2 |
| Combine source results | 0.3 |
| Restore original Library | 22.5 |
| Complete accounted workflow | 428.6 |

Preparation **266.8s**, validation including assets **48.8s**, finalization/remaining dispatch **113.0s**. A/B overlaps count once; normal exit at 50.5s is already inside checkpoint/release's 90.2s. Asset/dispatch 1.6s and small gaps explain the difference between displayed component rows and the continuous 428.6s total.

Shared: create 35.0s; candidate edit/preserve interval union 2.0s; initial readiness 22.6s; A/B validation including assets 26.0s; original recompile 11.1s; normal shutdown 2.8s; merge 0.3s; worktree removal 2.2s. Total **102.0s**, including dispatch within the continuous Editor interval. Preparation 59.6s / validation 26.0s / finalization 16.4s. AI reasoning, approvals, full input/final checks and earlier diagnostics are excluded from normal operation time.

## Diagnostics and remaining large costs

1. The first harness reused historical `bd23-a/b` keys. Registration failed before any task mount/Editor because preserved result branches existed. Library preparation had attached two references; normal restore refused them, and incomplete preparation cannot currently be discarded/released. After explicit user approval, exact task/basis locks and absence/identity/ref proofs, only these two dependency receipts were backed up and archived. Normal Library restore and full bytes/mtime/attributes verification passed. Failed records and both new basis pins remain in `.local/rr26-old`; historical branches/results are untouched. Recovery took 63.6s including its final proof; this is not product-supported automatic cleanup.
2. The next old-candidate run completed its operations but failed the original-Library equality check: only the five-byte `MCPForUnity/RunState/mcp_http_8080.pid` disappeared. The controller did not target that file; the writer was not instrumented. The changed-Library restoration took **360.0s** and that diagnostic's operation interval was **878.5s**. Read-only diagnostic inspection overlaps it, so it is not a clean baseline. All owned Editors/mounts/workspaces closed, source/config/preexisting refs were preserved. The naturally remaining Library was frozen anew; no server/package/run-state file was manually changed. The final baseline/current/shared start from that exact whole input.
3. Current initial Editor readiness remains **164.6s**, the largest single stage; current compilation plus assets **48.8s** exceeds shared **26.0s**. Whole Editor initialization/import/flush work needs focused attribution before another algorithm claim.
4. Current checkpoint/release still takes **90.2s**: 50.5s until observed normal exit, with subsequent preservation/reclamation inside the same interval. Internal subdivisions were not isolated. The old candidate's 164.2s shutdown also shows variability/timeout recovery matters.
5. `ManagedDirectoryRestore.ExecuteAsync` still selects the general file restoration path when the original Library changes; even a tiny deletion can prevent `ManagedUnchangedDirectoryRestore`. The current general path enumerates/plans files, moves/restores them with durable progress and performs final verification. A safe mostly-unchanged root transfer plus changed-entry restoration is a future candidate requiring recovery/identity proof; this experiment did not implement or measure it on current code. Do not drop seals, durability or progress protection based on this observation.

## Closure and evidence

Final measured old/current tasks are released, exact normal exit receipts have zero active owned processes, task mounts/deltas/providers are absent, and source/config/previous refs/worktrees are preserved. Current and old restore all fixed Library bytes/mtime/attributes before the next cohort. Shared code worktrees are removed and its Editor exited normally; its ordinary Library cache is normally regenerated. Independent results and all three combined trees match `756e0a926577c9bcff036c823b4816b3b2d5fb19`. The two earlier pre-mount failed records and basis pins are retained diagnostic evidence, not reported as released.

Raw evidence and scripts: `artifacts/unity-readme-recheck-20261026/`; final cohorts `baseline`, `current`, `current/shared`; excluded attempts `old`, `before`. Protocol amendments explicitly record both exclusions and the new complete input. The tracked dataset contains runtime/source/evidence hashes, each timing boundary, candidate results, closures and limits. No power-loss, peak memory/storage, long-session crossover or generalized application/tool support claim.

Final document/evidence checks passed: 160 current product-source hashes, both frozen runtime bundles and pinned raw evidence, arithmetic/closure assertions, 686 local Markdown links, matching bilingual numerical tables, and the current restore symbol. Final live inspection found no Unity Editor; the original remained clean at its unchanged HEAD with only its original worktree registered. No additional full gate was rerun for these documentation-only changes.
