# Current Unity parallel workflow comparison

[한국어](../ko/verification/unity-current-comparison.md)

For parallel code writing followed by **one shared Editor's sequential validation**, see [SE-01](unity-shared-editor.md). That separate comparison reuses this Splinter cohort after verifying its source/Library input and unchanged runtime; this RC-01 record continues to describe two independent Editors per method.

RC-01 · 2026-10-14 · product `7630970`. Fresh ordinary Git worktrees in Codex-app A/B task paths and fresh Library-referencing Splinter A/B use the same fixed source and one-line edits. Codex imports new Libraries; Splinter references the existing original Library. The comparison uses the completed Splinter cohort and subsequent Codex repeat; earlier diagnostic attempts are retained and excluded. Both methods were remeasured instead of attaching a new Codex result to the previous UT 875.2s observation.

AMD Ryzen 5 5600X (6 cores / 12 threads), 68,628,553,728 bytes physical memory; Windows 11 Home 10.0.26200; NTFS; Unity 6000.3.22f1.

Source `b09fa58e9d40a560d4fe1af7a213f8ac23e8e3b1`: 1,602 tracked files / 2,804,772,822 bytes. The unchanged original Library contains 42,420 files / 3,141,660,252 bytes. Both methods use raw committed source bytes; command-local `core.autocrlf=input` prevents checkout conversion in the Codex worktrees. The original checkout and global configuration remain unchanged.

## Timing and scheduling

Both methods run independent A/B Editors concurrently. Their creation intervals also overlap; lifecycle preservation/reclamation follows the recorded command sequence. The methods run separately to avoid four-Editor contention. Every task makes its isolated one-line C# edit, explicitly refreshes, verifies compiled readiness and selected assets, then exits normally. This measures scripted workspace/Unity operations, not AI reasoning or feature-development time.

| A/B stage | Codex worktrees | Splinter |
| --- | ---: | ---: |
| Library basis preparation | Not needed | 1m 00.2s |
| Create A + B workspaces | 13.4s | 2m 48.8s |
| Launch both Editors → compiled readiness | 12m 41.7s | 5m 10.5s |
| One-line edits → recompiled readiness | 9.4s | 31.7s |
| Selected asset inspection | 7.6s | 1.2s |
| Normal shutdown of both Editors | 3.6s | 1m 56.8s |
| Preserve results + reclaim workspace files | 23.3s | 3m 19.5s |
| Combine A/B source results | 0.7s | 0.7s |
| Restore original Library | Not needed | 45.5s |
| **Complete workflow** | 13m 39.7s | 15m 35.2s |

Splinter takes 115.5s (14.1%) longer overall, while its initial Editor readiness interval is 451.2s shorter. Codex's total is parallel creation plus the uninterrupted Editor-through-cleanup interval, minus the allocation scan. Its read-only raw-input proof between creation and launch is excluded. Splinter's total is Library preparation through restoration, minus its allocation scan. A/B overlaps count once; small dispatch gaps remain included. Chat creation and Codex app archive are not measured. There is one completed compared cohort per method, without clearing OS caches or randomizing execution order; no causal claim is made about the difference from the earlier 875.2s observation.

## Additional file allocation

| At both tasks' edited-and-compiled readiness | Bytes | GiB |
| --- | ---: | ---: |
| Codex worktrees | 11,300,410,064 | 10.52 |
| Splinter | 153,409,432 | 0.14 |

Splinter uses **98.64% less additional file allocation** at this stage. The read-only Windows `FILE_STANDARD_INFO` scan counts task/control files, private source/Library data, owned Git registration, and shared runner/Library metadata once. Every measured root is complete with no read errors or unknown totals. Moved original Library payload, original source, shared Git objects, and directory/MFT metadata are excluded. This is file allocation at an idle compiled-ready stage, not peak volume consumption. The scan takes 33.0525s for Codex and 9.5116s for Splinter; both are excluded from workflow timing.

## Results, cleanup, and limits

Each individual result remains reachable. A conflict-free Git `merge-tree` / `commit-tree` combines each pair into a separate retained result ref; both produce tree `756e0a926577c9bcff036c823b4816b3b2d5fb19`. The original is not overwritten. Individual compilation, asset checks, exact owned Editor exit, Splinter provider/resource reclamation, and restoration of the original Library as an ordinary NTFS directory were verified. All 12 actual Splinter responses passed the current response-schema validator. Combined-result compilation, Play Mode, Unity tests, game build, and conflict resolution are **NotRun**.

Ordinary worktrees also allow parallel development. For independent tasks at unchanged per-task speed, parallelism saves time when `min(A, B)` exceeds extra preparation, merge, cleanup and resource-contention costs compared with sequential execution. This is a model, not a measured crossover. Longer tasks can amortize preparation, but equal parallelism does not establish that Splinter eventually becomes faster; repeated reads/writes and cleanup costs can grow with the work.

## Excluded diagnostic attempts and residual directories

A raw-EOL stat-check false positive interrupted Codex preservation. The first A result remains reachable; the benchmark index was repaired without content changes and that phase was remeasured. The same owned Codex task paths were then prepared afresh using raw Git inputs and the full workflow measured without interruption. The first cohort and diagnostic waits are excluded from the main comparison. Git returned exit255 after deleting payloads/registrations because both empty root directories were still open in another process (holder not identified); that residual boundary remains explicit. The dataset owns per-phase and observation costs, actual file allocation and per-root completeness, result trees, owned closure, original hashes and response-schema validation. Both Codex owners received an empty list_artifacts response, so app archive was unavailable. Exact owned paths, stopped Editors, all raw file bytes, clean Git status and durable result refs were verified before ordinary git worktree remove. The original, previous worktrees, and previous refs were preserved. No administrator, VSS, service, package, or global-setting changes were made. Existing fixture/dependencies remain; no new optional-tool qualification is claimed.

[JSON](../../reports/benchmarks/unity-current-comparison-20261014.json)
