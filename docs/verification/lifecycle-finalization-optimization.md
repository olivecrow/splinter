# Finalization and decoded-payload reuse

[한국어](../ko/verification/lifecycle-finalization-optimization.md)

This document owns LF-01–05, the bounded follow-up to [RC-01](unity-current-comparison.md). RC-01 recorded 199.5 seconds for serial A/B checkpoint/release. Its source inspections reused all 1,602 content hashes but still took about nine seconds each; each roughly 36 MB journal contained about 16,500 files. Those observations motivate the changes below; they are not timings of this candidate.

## Scope and preserved boundaries

- **LF-01 — source-path admission:** one attributes lookup replaces separate existence/type/attributes probes. An inspection-local, bounded ancestor cache applies only inside an identity-observed mount with authoritative before/after journal generation checks. Leaf paths and paths outside that mount remain freshly checked. HEAD/index, membership, attributes/config, source-change history, reparse rejection and failed-inspection behavior remain authoritative. No path proof is persisted between commands.
- **LF-02 — capsule reads:** format admission, frame validation and block locations share one open file. Temporary block descriptors (at most 131,072; about 3 MiB of struct data plus collection overhead) are replayed in generation order without reopening each capsule for its index. The budget fallback rereads block locations. Complete/prefix checksums, identity, generations and final inventory still run; no previous seal or cached hash substitutes for current physical bytes. The on-disk format is unchanged.
- **LF-03 — explicit combined completion:** `workspace checkpoint --release` preserves the result and continues into reclamation under one task lock for range-journal workspaces. Only this invocation's successful checkpoint post-seal substitutes for the immediately following release pre-seal. The fresh source inspection, following seal, independent result checks, post-unmount seal and reclamation guards remain. This avoids one full seal, redundant stop/infrastructure removal and the intermediate response observation. It does not integrate results into the original.
- **LF-04 — decoded visitor cache:** compressed payload comparisons, logical hashing and compaction visitors use the existing per-store 64 MiB / 4,096-entry RAM cache, shared with reads. Every hit rereads and validates current encoded bytes. A miss decodes completely before publishing the buffer; small budgets and oversized blocks retain bounded streaming. Failed reads/decodes/visitors clear the cache, poisoned writes and close still drain it, and each mutation retains its durable flush/ACK. The cache budget is not total process RAM.

LF-03 changes no persisted workflow/state format. Existing checkpoints and retries cannot reuse an earlier invocation's in-memory proof. A retry during `release-*` resumes the existing release; `released` is idempotent. `reclaim-pending` still requires the explicit returned recovery action. Changed source/journal, incomplete original apply, view dependencies, foreign ownership and uncertain resources preserve the task. Non-range checkout mode rejects `--release` before mutation.

Checkpoint remains a final collection boundary. Keep the same workspace/Editor through repeated edits and external validation, then choose combined completion only when preservation **and** reclamation are authorized. Use separate checkpoint → integrate → release when original integration is required. Checkpoint already normally stops its owned Editor; an additional `unity stop` immediately before it is unnecessary unless a separate stopped-state observation is needed.

## Validation

`pwsh -NoProfile -File ./scripts/build.ps1` completed with **1,571 passed, 0 failed and 24 conditional skips** (1,595 total; 7m 21.499s), after locked restore and a build with zero warnings/errors. New regressions cover current-byte corruption on cache hits, invalid compressed data before visitor callbacks, cache clearing after visitor failure, descriptor-budget fallback, mixed complete/retained-prefix recovery, source/journal changes between checkpoint and release, unmount-time writes, independent-result loss, foreign/owned process guards and same-intent release recovery. The first suite exposed two new test-harness defects (a nullable callback counter and a hash query before closing the fixture writer); both were corrected before the passing suite.

The candidate is a working-tree implementation on `37cfa01`; frozen CLI SHA-256 is `ed04b022b6a7ff0f696909161f270a5b775c8ce7d85f714ad9873441a0ea6d57`. Baseline product code is `7630970`. The [dataset](../../reports/benchmarks/lifecycle-finalization-optimization-20261014.json) records source/DLL hashes, raw evidence references, observations and boundaries.

### Bounded component costs

| Probe | Before | After | Reduction | Measurement boundary |
| --- | ---: | ---: | ---: | --- |
| LF-01 path inspection | 2,727.34 ms | 707.47 ms | 74.1% | 1,602 files; median of five alternating ordinary-NTFS passes with synthetic mount/journal admission; not full source inspection |
| LF-02 stopped-journal constructor | 400.35 ms | 311.32 ms | 22.2% | Same 512 logical files / 515 physical files / 992,579 bytes; warm median of three passes |
| LF-02 full physical seal | 418.48 ms | 329.81 ms | 21.2% | Same stopped fixture, complete manifest/seal equality; current bytes still checked |
| LF-04 repeated decoded visitors | 148.41 ms | 14.70 ms | 90.1% | Eight compressed 1 MiB blocks visited 16 times each; median of two cache-off/on observations |

LF-01 reduces metadata calls from 76,896 to 19,348 per pass. LF-02 seal managed allocations fall from 50,583,360 to 11,363,216 bytes (77.5%); this is cumulative managed allocation, not resident RAM. Its first JIT observation is retained but excluded from the warm median; earlier sandbox measurements remain diagnostics only. LF-04 retains 8 MiB decoded payload (8,392,704 bytes of cache accounting) and reduces decompressions from 128 to eight, with 120 hits; the existing store budget remains 64 MiB / 4,096 entries. These percentages cannot be applied to a complete checkpoint or Unity workflow.

### LF-05 — small native completion

Three fresh, scoped Git/WinFsp A/B cohorts use identical committed source bytes (68 tracked files, including 64 × 1 KiB payload files) and 128 × 1 KiB Library files. Each task adds its own result text file and changes one private Library byte in all 128 files. The Unity policy is selected, but no Editor runs. Workspace creation is sequential in every cohort; only final completion scheduling differs.

| A/B preservation and reclamation | Elapsed time | Reduction from baseline |
| --- | ---: | ---: |
| Baseline: separate checkpoint/release, A then B | 65.771 s | — |
| Candidate: `checkpoint --release`, A then B | 59.898 s | 8.9% |
| Candidate: `checkpoint --release`, A/B concurrently | 37.740 s | 42.6% |

The timer includes CLI startup and both tasks' preservation/reclamation. Preparation, creation, edits, result-validation queries and Library restoration are excluded; their command timings remain in the dataset. This is one cohort per method, combining code changes and, in the last row, concurrent scheduling. It does not isolate a causal percentage for an individual cache. Concurrency raises individual completion time to 37.730 s / 36.506 s from 30.278 s / 29.614 s while shortening the combined wait. Independent A/B commands are an agent procedure, not a product scheduler.

All six tasks finish `released`. Each independent Git result contains its own edit and excludes its peer's edit; B remains readable after A cleanup in sequential cohorts. Each final observation confirms absent mount/provider/delta and zero owned handles/working set/private bytes. Library restore completes; the original file hashes, HEAD and clean Git state match, with only the original Git worktree registration remaining. All **20 actual CLI responses** pass the current response schema. Fixture data and raw evidence are retained; no unrelated project, process, VSS snapshot or service was modified.

The evidence is limited to automatic contract/corruption/recovery tests, small same-input NTFS capsule costs, decoded-visitor costs and a small Editor-free product A/B. Full Unity initialization/compilation, callback attribution, Play/Test/build, OS crashes, whole-system RAM and large-project scaling are not established by those checks. The user's original Unity project and installed service are unchanged. README's RC-01/SE-01 timings are retained as their recorded observations, not relabeled as measurements of this candidate.

## Remaining architectural work

An arbitrary new task cannot inherit an already-running Editor's project state through this change. Automatic Editor pools/reset/rebase, moving private reads outside their lifetime lock, capsule packing into a new storage format and eliminating release's fresh source check require separate state/lifetime designs. No cache is used to hide current corruption or to treat mtime as content proof. RC-01's approximately 87-second initialization callback region still needs function-level attribution before its cause can be changed.
