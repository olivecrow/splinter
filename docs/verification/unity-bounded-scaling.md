# Unity compilation workload scaling with the bounded journal

[한국어](../ko/verification/unity-bounded-scaling.md)

## BS-01 — measured workload sequence

Measured on **2026-10-23 KST**, product `c4fa5063964e7b8456de4ea7735c042fb9af8930` and fixture `b09fa58e9d40a560d4fe1af7a213f8ac23e8e3b1`, Unity **6000.3.22f1**. This uses the current bounded SJR2 default and the unchanged runtime from [BC-01](unity-bounded-comparison.md). **The extra duration is not established as either a fixed number of seconds or a constant multiplier.** Larger compilation intervals sometimes benefit from parallel execution, but the eight measured intervals save only **142.0s**, against **783.7s** of extra lifecycle operations in this sequence. Complete accounted operations take **1,582.4s / 940.6s**, leaving Splinter **641.8s slower** overall.

[Exact dataset](../../reports/benchmarks/unity-bounded-scaling-20261023.json). Raw commands, source inputs, assemblies, per-request proofs and logs remain in `artifacts/unity-bounded-scaling-20261023`. The input exception and restoration cost below are part of this result; it is not a fresh, strictly identical complete-Library pair.

## Workload and observations

Each task changes its assigned ordinary C# source file and adds **0, 4,096, 16,384 or 65,536 generated methods**. The actual order is `0 → 4096 → 16384 → 65536 → 65536 → 16384 → 4096 → 0`. There is exactly one explicitly requested refresh per task per trial, followed by the same asset/scene/render checks. No artificial wait substitutes for work. This is a synthetic compilation workload, not gameplay, tests, a game build or a representative feature-development benchmark.

| Generated methods per task | Shared A/B seconds, ascending / descending | Splinter A/B seconds, ascending / descending | Mean extra seconds with Splinter |
| --- | ---: | ---: | ---: |
| 0 | 26.0 / 25.9 | 35.6 / 38.6 | +11.2 |
| 4,096 | 27.0 / 27.3 | 34.1 / 37.1 | +8.5 |
| 16,384 | 29.4 / 192.3 | 62.2 / 236.7 | +38.6 |
| 65,536 | 212.6 / 347.6 | 65.7 / 236.0 | -129.3 |

Each cell lists both observed A/B intervals in seconds; a negative extra duration means Splinter saved time. Parallel A/B overlaps count once. Shared A and B are sequential. Small file dispatch and the same asset checks are included; independent DLL/hash proof time is excluded from both methods.

The two 65,536-method Splinter intervals are **65.7 / 236.0s** despite equal method counts. Shared intervals also vary; prior loaded assembly, order and accumulated state are not independently controlled. This defeats a workload-count-only interpretation. Independent tasks keep their previous source; shared validation restores the peer while applying the next task. Thus shared B starts after current A, while independent B starts after its own previous trial. This measures the two continued workflows, including their different state transitions; it does not isolate journal overhead at identical before/after Editor states.

The dataset retains diagnostic fits for `S = E + d`, `S = rE` and `S = c + rE`, using four level means and `E = shared pair / 2`. Here `E` is only a balanced descriptive equivalent: the workers' preceding states are not experimentally matched. Repetition/history variation, only two observations per level and that mismatch prevent interpreting fitted coefficients as causal scaling laws. Neither a universal delay nor a universal multiplier is qualified. Distinguishing those mechanisms would require separately controlled starting states and repetitions; this run does not invent such evidence.

## Controls and proof

- Same machine as BC-01: Windows 11 Home 10.0.26200, Ryzen 5 5600X, 6 cores / 12 threads, 68,628,553,728 installed RAM bytes. The same 237 product/contract file hashes and 32 runtime hashes were verified before execution. Product code, Unity version and packages were not changed.
- Original source: **1,602 files / 2,804,772,822 bytes**, fixed raw Git input. Initial Splinter Library: **42,421 files / 3,144,431,511 bytes**. All remaining **42,420** Library files match before shared execution; the removed five-byte stale external-server PID file is detailed below.
- `git-snapshot`, the same fixed Git base, one explicit Library basis, parallel A/B ensure, and returned workspace paths. Both responses prove `journalStorageMode=bounded` and `journalOptimizationEnabled=false`; suffix limits remain 16MiB / 4,096 frames.
- Every one of the **32 task validations** requires the exact Editor project/PID/start identity, fresh generation and assembly MVID, expected loaded A/B values, no reported compiler errors, asset checks, a valid request-specific render and restored scene state. DLL metadata must contain every generated method name, exactly the requested count, plus the expected generated class. Compiled DLL hashes and copies are retained outside the timer.
- One Splinter sequence followed by one shared sequence; no OS cache clearing or randomized independent cohorts. The original source is restored between methods. Temporary Git configuration is restored. Both methods preserve the same final combined source tree; that combined tree is not recompiled.

## Preparation, completion and break-even

Seconds, rounded to one decimal. These are summed measured operation intervals, not elapsed conversation time. Generated input preparation, read-only proofs, AI/tool waiting and the separate failed preparation/server recovery are excluded. Lifecycle operations are charged once for the eight trials.

| Stage | Splinter | Shared Editor |
| --- | ---: | ---: |
| Preparation and initial readiness | 316.5s | 33.9s |
| Eight A/B validation intervals | 746.2s | 888.1s |
| Result preservation, normal shutdown and source restoration | 114.0s | 15.5s |
| Combined source result | 0.4s | 0.3s |
| Original Library restoration / code-worktree reclamation | 405.4s | 2.8s |
| Complete accounted operations | 1,582.4s | 940.6s |

The general accounting rule is:

```text
net time saved = sum(shared validation interval - parallel validation interval)
                 - extra preparation/completion cost
```

For this sequence: **142.0s − 783.7s ≈ −641.8s** (calculated from unrounded values). It did not break even. The two largest-workload pairs saved **129.3s on average**. If that saving and this lifecycle cost repeated unchanged, the arithmetic threshold would be **7 such pairs**; only two were measured, so this is a conditional repetition count, not a measured long-session crossover or a general task-duration recommendation. Setup is not charged again on every edit, but completion cost can depend on accumulated state and the restoration route.

## Retained interruption and input exception

The first Library preparation failed after **17.5s** because the still-running MCP server held `Library/MCPForUnity/Logs/server-launch-8080.log` open for writing. Its recorded process ancestry led to the prior BC-01 shared Editor (PID 33948). With explicit user approval, only server PID **43056** was terminated; Windows rejected non-forced taskkill, so forced server termination was used. No Unity Editor was forcibly terminated, and packages/global configuration were not changed. The same home `.local/bs23`, key `bs23-library`, basis `b91d15db564e3a2e1c85623a` and saved preparation intent were resumed. The successful preparation took **59.5s**. The log's final bytes were recaptured before the measured run; the failed attempt and approval/recovery wait are excluded from work timing.

After normal A/B release, Library restoration succeeded in **405.4s**, then the predeclared strict whole-Library equality assertion failed. The only content/tree difference was removal of `MCPForUnity/RunState/mcp_http_8080.pid`, **5 bytes containing `43056`**. No other file was added, removed or changed; all source bytes also matched. The stale PID was not recreated. The assertion failure is retained and the subsequent shared run is qualified with this explicit exception.

The recorded general `restore-plan.json` was used. `ManagedUnchangedDirectoryRestore.Eligible` requires zero durable mutations and unchanged entries; the PID deletion disqualifies that fast path. This explains the route distinction from BC-01's 58.8s unchanged-Library restore, **not an isolated causal attribution of every extra second**. The 405.4s remains included in the observed lifecycle total and must not be attributed to code size or treated as a universal cleanup constant.

Both owned Splinter Editors exited normally with exit code 0 and no active job processes; their mounts, providers and deltas were reclaimed. The original Library returned to ordinary NTFS. Shared execution restored and recompiled original source, exited its Editor normally and removed only the verified owned code worktrees. Original HEAD/branch, 1,602 source files, preexisting refs/worktrees and Git configuration were verified. Normal shared-Editor Library regeneration is retained. External MCP server lifetime is separate from these owned Editor/workspace closure claims.

The unchanged product's previously completed build gate is **1,691 passed / 0 failed / 24 skipped**; it was not rerun for documentation and benchmark orchestration changes. All 18 selected response objects (9 new runtime / 9 reused discovery) passed the current response-schema test. New whole-system RAM/physical peak, Play, Test Runner, game builds, 3–4 Editors, other projects/tools/OSes and journal-only causal speedups remain unmeasured. Historical graphs were removed from both root READMEs; historical evidence files remain intact.
