# Time and RAM optimization

[한국어](../ko/verification/time-memory-optimization.md)

TM-01–04 implement the follow-up to [UF-01](unity-final-lifecycle.md). UF's full Unity workflow remains the recorded **1,083.0s at `116fd44`**; the new candidate has not been timed with two Unity Editors. The [dataset](../../reports/benchmarks/time-memory-optimization-20261014.json) records small-fixture costs, runtime/source hashes, validation and limitations. The source baseline is `5fab578`; the frozen native comparison runtime is UF's `116fd44`.

## Changes and retained guards

| ID | Change | Limits and protection |
| --- | --- | --- |
| TM-01 | Capture, Library admission and unchanged-directory restore pin descendant directories once, then validate each leaf through its pinned parent. Provider admission hashes with at most two workers. | Native identity/type/hardlink/reparse/final-path checks remain. All workers drain before failure closes handles. Capture and final restore hashing remain serial; a restore handoff closes the old proof and establishes a fresh complete post-move proof. Extra directory handles scale with the number of directories. |
| TM-02 | Each Git tree lazily retains requested verified chunks in a 16MiB, 4,096-entry RAM LRU. | First access still proves the entire blob. Pending chunks share the budget and cannot be reused before proof publication. Retained chunks match exact object/length/SHA; proof eviction retires their payload, and errors/close drain readers and clear caches. Existing 4MiB proof budget, two decoder processes, 256KiB windows and two at-most-1MiB request buffers remain. Immutable retained bytes are not a fresh whole-object corruption audit. |
| TM-03 | Each journal store lazily retains decoded compressed blocks in a 64MiB, 4,096-entry RAM LRU. | The budget includes the in-flight allocation and a 512-byte allowance per entry. Every hit still reads the current encoded bytes and validates format/length/SHA. Full replay/seal/export traversal, one durable flush per mutation and ACK remain. Read error, poison and close clear the cache. Superseded decoded bytes may remain only inside this bounded LRU; physical offsets alone never authorize a hit. |
| TM-04 | The agent dispatches independent `ensure` requests concurrently after preparing the shared Library once. | Different stable keys, identical fixed source/home/base/basis, independent response checks and one writer per task. Fake barriers prove overlap and same-task recovery after one lost attach reply. This reuses existing task locks and commands; it adds no scheduler, queue or automatic mode. |

Cache budgets bound retained managed payload/entries, not total process memory or GC residency. A workspace may have more than one store/tree; these are not whole-workspace caps. `readCacheBytes = 0` continues to describe disk payload cache. No format/schema, disk hydration, whole-file copy-up, service configuration, external Unity tool or flush policy changes were made.

## Bounded costs

Git uses two 4MiB packed blobs and 128 backward/interleaved 4KiB requests. Initial full proofs are reported separately; repeated-read timings include exact byte assertions. Journal tests read 512 partial 4KiB ranges from eight 1MiB blocks. Library admission uses 512 files / 64MiB after a warm admission. No other build or performance probe ran concurrently with the timed probes. OS caches were not cleared; the small samples do not establish general or cold-disk speedups.

| Operation | Before/off, seconds | Current/on, seconds | Observed reduction |
| --- | ---: | ---: | ---: |
| Git repeated backward reads | 7.751726 | 0.757531 | 90.23% |
| Journal repeated partial reads: two-run mean, 0/64MiB | 0.674372 | 0.027294 | 95.95% |
| Full Library admission: three-run mean, previous/two workers | 0.366782 | 0.123999 | 66.19% |

Git initial full proofs took 0.255684s / 0.254837s. Repeated decoded bytes fell from 48,234,496 to 6,029,312 and reader starts from 64 to 8, still at most two concurrent readers. Additional cache payload peaked at 1MiB in this sample. Journal decodes fell from 512 to 8, with identical 1,099,648 encoded bytes read in every run. Decoded payload was 8MiB; the accounting peak including entries was 8,392,704 bytes.

Library means for the new 1/2/4-worker paths were 0.151393s / 0.123999s / 0.109777s.

Journal cache sizes 32/64/128MiB all retain the same 8MiB fixture; this sample does not establish 64MiB as an optimal size. The production default is a bounded initial choice. Library's 1/2/4-worker comparison is warm and small; two workers limit per-provider concurrent I/O while retaining the path-proof improvement. Its scratch buffers total 128KiB, plus directory handles and metadata.

## Native independent A/B

The ordinary-user WinFsp test uses a dedicated clean local Git fixture: 64 source payload files totaling 1MiB, four configuration files, and a 512-file / 32MiB Library. It does not open Unity or operate the user's original project. For each new run, prepare Library; ensure A/B; write distinct files; checkpoint and release A while B remains readable; checkpoint and release B; restore the original Library. Verify independent Git results, every original file hash, unchanged HEAD/clean checkout, successful terminal responses and zero owned resources on both releases.

| Stage, seconds | Before serial | Current serial | Current parallel |
| --- | ---: | ---: | ---: |
| Library prepare | 9.118 | 8.801 | 8.859 |
| Ensure A/B cohort | 58.308 | 57.870 | 32.191 |
| A checkpoint | 18.507 | 18.801 | 19.562 |
| A release | 10.977 | 12.310 | 12.237 |
| B checkpoint | 18.117 | 18.528 | 19.293 |
| B release | 11.024 | 11.815 | 12.233 |
| Library restore | 1.749 | 1.019 | 1.157 |
| Complete workflow | 128.257 | 129.665 | 106.004 |

The new code alone did not reduce this single-pass small workflow: 128.257s versus 129.665s. Overlapping the two current ensure calls reduced their cohort from 57.870s to 32.191s (44.37%); the complete workflow was 106.004s, 17.35% shorter than the baseline. This distinguishes orchestration savings from the isolated repeated-read cache benefits.

Raw fixture setup and final hash assertions are outside the complete-workflow timer. That timer includes CLI dispatch, response persistence and independent-result/peer checks between commands. Ensure cohort timing includes either both sequential calls or dispatch-to-completion of both concurrent calls. Runs occurred in baseline-serial/current-serial/extra-current-serial/current-parallel order, without randomization. The extra run, named `after-parallel-1`, accidentally omitted `--parallel`; its recorded `parallel=false` result (133.270s) is preserved as serial evidence and is not used as a parallel measurement. Only `after-parallel-2 --parallel` supplies the parallel column. Full Unity import, compile, Editor shutdown and whole-process peak RAM are **NotRun** for this candidate.

## Validation and administrative boundary

The required `pwsh -NoProfile -File scripts/build.ps1` passed locked restore, Release build with zero warnings/errors, and **1,531 passed / 0 failed / 23 conditional skips** (1,554 total, 434.028s test duration). The three opt-in cost tests and the final Library cost rerun also passed. All **32 terminal CLI responses** from four native cohorts passed the current response schema. All eight workspace releases report owned resources/handles/mount/provider/delta/basis absent, with independent results retained, only the primary Git worktree registered and no task basis refs left. Every cohort restored the original file hashes and clean HEAD.

Coverage includes cache budgets/eviction, first-proof nonpublication, immutable-byte versus uncached corruption behavior, live encoded corruption after a cache hit, overwrite/resize/rename/unlink/reopen, failed reads and concurrent close, pinned empty/descendant directories, junction replacement refusal, failed/cancelled hash-worker drain, restore handoffs and same-task retry after a lost preparation reply. The initial focused run had one sharing-error category regression; read-only capture/admission now preserves the original `IOException` and native HRESULT, and the final full suite passed with that assertion retained. The failed build/focused logs remain in raw evidence.

A separate elevated **read-only** preflight found three existing snapshots on C:, an active storage-authority service and seven grants; the former T: test volume is absent. The new fixture is not granted for VSS. Existing snapshot/reclamation protections remain, and those snapshots were not deleted or changed. No new C: VSS comparison or service update was performed. A VSS experiment needs an isolated, explicitly granted input/volume and separate qualification; admin availability alone does not accelerate the current Git path.

Reproduce the three opt-in cost probes by setting a **new** output path in `SPLINTER_GIT_PAYLOAD_COST_OUTPUT`, `SPLINTER_JOURNAL_DECODE_CACHE_COST_OUTPUT`, or `SPLINTER_OWNED_DIRECTORY_ADMISSION_EVIDENCE`, then running the corresponding `RepeatedPackedBackwardReadCost`, `RepeatedPartialReadCostEvidence`, or `AdmissionCostForOneTwoAndFourHashWorkers` test on the built Release assembly. Run probes serially. The dataset preserves the native harness path/hash and exact native command evidence; prior evidence is never overwritten.
