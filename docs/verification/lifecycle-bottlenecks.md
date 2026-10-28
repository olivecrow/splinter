# Lifecycle bottleneck follow-up verification

[한국어](../ko/verification/lifecycle-bottlenecks.md) · [Plan and boundaries](../plans/lifecycle-bottlenecks.md)

## Candidate and scope

Base commit `daa3a1646e44395690f00a282d5b44770ca89066`, Windows x64, 2026-10-27, plus the source hashes recorded in the benchmark report. BO-01–05 implement the bounded opportunities selected after RR-01. RR-01's 428.6s / 102.0s observation is historical input, not this candidate's before/after result.

| ID | Evidence |
| --- | --- |
| BO-01 | Positive-sequence exact equality; metadata-only and deleted original leaf restoration; pinned plan and sealed-journal effect comparison; fixed-root transfer, full content/identity/membership proofs, staged deletion and exact reclamation. Interruption and foreign-data tests accompany both new and resumed operations. |
| BO-02 | Frozen original product and candidate run the identical 10,000 × 1KiB ordinary NTFS fixture with one read-only leaf deleted. Serial before/after/after/before, complete restore call including product verification/reclamation. Fixture construction and independent test assertions are outside the timer. |
| BO-03 | Opt-in bounded callback/batch/store-call observations. Sequential and concurrent OFF/ON/ON/OFF cost fixture retains real durable journal writes and content assertions. No native WinFsp mount or Unity runtime qualification. |
| BO-04 | Git read/proof/cache/wait counters, controlled overlapping attach intervals, failure/drain and attempt identity tests. Existing reader and cache bounds remain. |
| BO-05 | 21 native ordinary-file capture tests passed, including identity, limits, reparse/mutation refusal and before/live correlation. PowerShell capabilities and helper loading passed. Actual Unity startup capture was **NotRun** in this component cohort; the later single-input execution is owned by [BD-01](unity-lifecycle-bottleneck-diagnosis.md). |

## Measured component costs

The [machine-readable report](../../reports/benchmarks/lifecycle-bottlenecks-20261027.json) retains all four runs, identical logical-input hashes, exact assembly/source hashes and both diagnostic cohorts.

| Restore run | Before | After |
| --- | ---: | ---: |
| First observation | 44.772525s | 2.557986s |
| Second observation | 44.868515s | 2.353533s |
| Mean | **44.820520s** | **2.455759s** |

BO-02 observed **42.364760s / 94.5%** less restore time. Each original run moved 9,999 surviving files. Each candidate run moved one root and one deleted leaf, with zero individual survivor moves. All 9,999 final file contents, creation/write times and attributes, exact membership, deleted-leaf absence, removed basis/delta and staged-file absence were independently checked. The input hash additionally fixes access times and directory metadata. Cumulative managed allocation during the restore call fell from 324,601,436 to 183,017,848 bytes; this is not peak RAM.

| BO-03 real durable callback fixture | Diagnostics OFF mean | Diagnostics ON mean |
| --- | ---: | ---: |
| 512 sequential writes | 1.114871s | 1.084679s |
| 512 writes, eight callers | 0.259829s | 0.252060s |

These tiny OFF/ON differences do not establish a speedup. The two-pair cohort found no material time overhead in this fixture; allocations increased by 130,416 and 150,552 bytes respectively. Each write carries 112 changed bytes; sequential calls retain 512 flushes and concurrent calls use the existing batching. This measures the new observation cost, not a newly improved batching algorithm. Native mounted Unity overhead remains unknown.

## Reproducibility and validation

Raw evidence is retained under `artifacts/lifecycle-feasibility-implementation-20261027/` and `artifacts/optimization-feasibility-20261027/`. The baseline was exported with `git archive` before implementation; only the identical benchmark test was added. Product assemblies and logical input hashes distinguish the variants. The tracked benchmark report retains the observations and source hashes.

Run the required gate with `pwsh ./scripts/build.ps1`. The explicit cost tests are skipped by default. Set `SPLINTER_SPARSE_RESTORE_COST` to a fresh JSON path for `SparseDirectoryRestoreCostTests`, and `SPLINTER_WRITE_TELEMETRY_COST_OUTPUT` for `WinFspWriteTelemetryTests.FixedSequentialAndConcurrentTelemetryCostEvidence`. Run cost cohorts serially, without competing test/build processes. Neither command launches Unity.

The first full gate found two general-route reclamation fixtures injecting an orphan receipt before any plan existed; the new routing guard correctly refused it earlier. The fixture now first interrupts a real general restore after verification, then injects the other mode's receipt. It retains the original wrong-journal-mode reclamation assertion and restored-file checks. Separate sandbox Git initialization and junction creation failures were environmental permission failures; their logs remain alongside normal-user reruns. A DLL copy collision from overlapping build/test processes is not counted as a successful validation of the later source.

The final changed-boundary run passed **146/146** restoration/reclamation/Git-attempt tests in 12.866s (`final-fixes-focused.trx`). Git reader/cache focused validation passed 47 with one opt-in skip; write admission validation passed 45 with two opt-in skips. The explicit cost run subsequently executed its write-overhead test successfully. These focused checks do not replace the required full gate.

The final required `pwsh ./scripts/build.ps1` passed locked restore and compilation with zero warnings/errors, then **1,724 passed / 0 failed / 30 conditional skips**, 1,754 total, in 9m 08.601s. Raw final evidence is `final-full-build.log` and `final-full-build.trx` in the lifecycle evidence directory above. Both measured candidate assembly hashes match this final build. Bilingual Markdown links, 50 indexed source hashes, benchmark source hashes, 19 stage IDs, UTF-8 checks and `git diff --check` passed. The code-backed map retains six diagrams in each language.

## Interpretation and remaining cost

The restoration optimization removes surviving-file moves and per-file progress work. It retains full initial and post-move content proofs, identity/membership checks, metadata application and exact journal reclamation. New files, directory-set changes, path moves, length/private-range changes and already-started general plans retain the existing file route. Metadata-only retry behavior remains explicitly bounded by the plan.

Detailed write and Git diagnostics are off by default. Enabling diagnostics is for attribution, not a new fast mode. Callback bytes, effective bytes and changed bytes differ; cumulative waits and concurrent processing ticks are not a whole-workflow stopwatch. Capture preserves bounded existing artifacts and exact process identity; it does not enable a Unity profiler or assert that its caller interval is Editor startup time.

This component cohort ran no new actual Unity lifecycle, original apply, discard, Play/Test/build, external-tool support, OS power-loss or peak-RAM qualification. The subsequent [BD-01 diagnosis](unity-lifecycle-bottleneck-diagnosis.md) used committed `bea4acd` for one actual GUI workspace, normal stop, discard and exact Library restoration; its 391.3s includes diagnostic capture and is not a BO before/after comparison. Its negligible decoder wait rejected a tested cache-bypass candidate; serial durable writes, reload and payload re-decoding remain distinct costs. This report does not claim that a component percentage reduces the previously measured whole-workflow gap by that percentage.
