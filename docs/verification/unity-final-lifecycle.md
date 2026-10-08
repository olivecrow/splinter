# Unity lifecycle comparison after optimization

[한국어](../ko/verification/unity-final-lifecycle.md)

**UF-01** reruns the original [RB A/B workload](readme-unity-comparison.md) on committed candidate `116fd447086a825bf6d5b424ff9a4aad41dd8642`. Preservation and release take **2m 52.0s**, versus the original **2h 36m 07.1s**. Original Library restoration takes **2m 06.0s**. The complete new preparation-to-restoration interval is **18m 03.0s**. [Exact dataset](../../reports/benchmarks/unity-final-lifecycle-20261008.json).

## Input and work

Windows/NTFS, Unity **6000.3.22f1**, Git source `b09fa58e9d40a560d4fe1af7a213f8ac23e8e3b1`: **1,602 tracked files / 2,804,772,822 bytes**. All original source hashes match RB. All **44,860 Library entries / 42,420 files / 3,141,660,252 bytes** match the original RB manifest, including content hashes and metadata. No assets were removed.

A changes `Assets/SplinterAlphaA.cs` from `value + 1` to `value + 2`. B changes `Assets/SplinterAlphaB.cs` from `value + value` to `value + value + value`. Two owned Editors initialize concurrently. Explicit refresh proves the exact project/PID/start identity, fresh idle state, matched successful request, new generation/MVID and A/B values `(5,6)` / `(4,9)`, with original value 13. Selected assets are inspected. Unlike [US-01](unity-startup-remeasurement.md), this run includes the original source edits.

Fresh `git-snapshot` tasks share the prepared original Library and use default SJL4. One frozen normal Release bundle is used throughout; all 45 file hashes are verified before and after. The precommit working-tree build for these unchanged product source hashes recorded **1,498 passed, 0 failed, 20 conditional skips**, with unrelated test edits retained. No external profiler or simultaneous benchmark was run; built-in counters and ten-second provider observations are retained.

## Original versus current

These commands start after editing, compilation and normal Editor exit. Setup and shutdown are excluded; restoration has its own interval.

| Operation | Original RB | Current UF | Observed reduction |
| --- | ---: | ---: | ---: |
| A checkpoint | 1h 35m 23.6s | 0m 37.7s | 99.34% |
| A release | 27m 42.5s | 0m 49.4s | 97.03% |
| B checkpoint | 48m 34.7s | 0m 37.4s | 98.72% |
| B release | 19m 23.4s | 0m 47.6s | 95.91% |
| A/B preservation + release wall time | 2h 36m 07.1s | 2m 52.0s | 98.16% |
| Original Library restoration | 24m 33.2s | 2m 06.0s | 91.45% |
| Preservation/release + restoration | 3h 00m 40.3s | 4m 58.0s | 97.25% |

![Original and current Unity lifecycle times](../images/unity-final-lifecycle.png)

The original 2h 36m preservation interval overlapped B work and included diagnostics, a refused `WorkspaceBusy` call and recovery. UF preserves/releases A, then B sequentially without reproducing that orchestration failure. B's original successful checkpoint is the 2,914.6685s parallel receipt, not the 5.8595s refused request. RB also externally Git-committed B's exact source file before its successful checkpoint (64.3363s, included in overlapping work); UF's checkpoint performs that commit itself. Therefore B's individual checkpoint comparison has this procedure difference. Individual original command times overlap and must not be summed. RB's published wall value is retained despite a 0.95s difference from its recorded UTC endpoints. The combined original row adds its published preservation and restoration durations; current uses the continuous preservation-start to restoration-end interval.

These are observed reductions for one cohort per candidate, without clearing OS caches or randomized repeats. They do not isolate each optimization's causal contribution. The earlier Codex and storage allocation measurements were not repeated.

## Complete current workflow

| Stage | Current elapsed time | Share of full interval |
| --- | ---: | ---: |
| Library basis preparation | 1m 55.9s | 10.7% |
| A workspace creation | 2m 43.5s | 15.1% |
| B workspace creation | 2m 49.3s | 15.6% |
| A/B Editor launch to compiled readiness | 3m 36.1s | 19.9% |
| A/B one-line edits to compiled readiness | 0m 29.9s | 2.8% |
| Selected asset inspection | 0m 02.4s | 0.2% |
| A/B normal Editor shutdown | 1m 27.9s | 8.1% |
| A/B preservation and release | 2m 52.0s | 15.9% |
| Original Library restoration | 2m 06.0s | 11.6% |
| **Prepare through restore** | **18m 03.0s** | **100%** |

Small instrumentation/dispatch gaps are included in the full interval. Editor readiness includes scan/import/compile preparation and observation; it is not standalone compiler CPU time. Both stop commands succeeded within their two-minute limit. Exact PID/start observations confirmed normal exit of both Editors in **1m 27.9s**, with no stop timeout, repeated request or forced termination. Earlier UC/US runs exceeded the limit.

## Closure and limits

Both task result trees exactly equal the historical A/B expected trees, each containing only its intended one-line change. Both tasks are released; owned Editors/providers are absent by PID/start identity, mount/delta/Git registration are gone and independent results remain. All 1,602 original files are byte-verified unchanged; HEAD/branch/preexisting refs and global configuration are unchanged. The temporary fixture-local Git setting is restored. Library is restored as an ordinary directory on its original NTFS volume. All 27 unrelated dirty repository files remain byte-identical.

All **12 actual CLI responses** pass the current response schema; no lifecycle command failed. Raw evidence is retained under ignored `artifacts/unity-final-lifecycle-20261008/` and `.local/uf8/`. The normal restore receipt retains the original Library's seal and identity/content/membership verification. No UAC, service change, external package change, Play Mode, Unity tests, game build, new Codex run, whole-disk peak/RAM measurement or OS-failure matrix was performed. This one source/Library/candidate cohort does not establish general Unity support or arbitrary-project speed.
