# Write-path throughput verification

[한국어](../ko/verification/write-path-throughput.md)

**WP-01–05, 2026-10-25.** Baseline `9594209607dcf69e1b992419fbf265fda8b32e2b`; final working-tree change implements two optimizations and removes one candidate after measurement. [Scope](../plans/write-path-throughput.md), [current contract](../raw-write-batching.md), [dataset](../../reports/benchmarks/write-path-throughput-20261025.json).

## WP-01 — reuse a proven clean explicit flush

SJR2 records the handle and length only after `Flush(true)` succeeds. An explicit `Flush()` checks local integrity and reuses that result only for the same handle and length. Sequence alone is insufficient: retirement can append without advancing it, and checkpoint publication replaces the stream. New/reconnected/replaced handles must establish their own durability. Mutation/batch flushes, poison after uncertain writes, queued-ticket draining, `flush.before/after` observer boundaries and SJL4 publication/explicit flush remain. A single ordinary synchronous write still flushes before success. This improvement applies when the caller also requests an explicit flush of already durable bytes.

Tests cover fresh and reconnected append/bounded/latest modes, repeated explicit flush, a mutation between flushes, readback and an unavailable basis. Existing ordered/failing batch, interrupted waiter, explicit-flush ticket drain and checkpoint/recovery tests remain; physical-count expectations now exclude the redundant barrier.

## WP-02 — payload-free state size calculation

Current-state output is raw, so the estimator uses decoded lengths without reading/allocating payload. Metadata serialization and bounds remain. Actual output still reads the payload, writes checksums, checks the estimate against actual size and passes complete candidate/published-state proof and recovery checks. Raw and current-format encoded fixtures verify 0 sizing reads, equal estimated/output lengths and actual output reads. No journal format or reader migration changed.

The isolated sizing phase improves substantially and removes an unnecessary payload-sized allocation. This does **not** establish a large overall checkpoint improvement: the final checkpoint-heavy workload improves only **3.555%** below. Full state output and validation remain dominant.

## WP-03 — rejected raw edge search

The candidate searched raw private overwrite edges from both ends, retained the exact contiguous envelope and skipped the changed middle. It passed focused correctness checks. In its before/after/after/before comparison, 256 × 1MiB overwrites took **1.838353 → 1.882652s** on average, while journal reads fell **321,912,832 → 69,206,016 bytes**. Less I/O did not produce an elapsed-time benefit. The implementation and candidate-only tests were removed; its binary and raw measurements remain under the evidence directory. It is not an optional backend or an adopted optimization. Existing raw/encoded comparisons and the 4KiB threshold remain.

## WP-04 — final fixed-input comparison

The same probe binary runs against frozen baseline and final Core/Runtime assemblies. Closed input files, identities and hashes are reused; each run gets its own copy outside the timer. Final order is **before → after → after → before**, two runs each, without overlapping builds/tests/probes. Inputs are a created empty file, a 1MiB fixed-seed file, and a 16MiB live file plus a small append file at the 4,096-frame checkpoint boundary. All use bounded SJR2 with the default 16MiB/4,096-frame limits. Supplied batches are explicit, not a captured Unity request trace.

| Workload | Before mean (s) | After mean (s) | Reduction | Physical flushes before → after |
| --- | ---: | ---: | ---: | ---: |
| 2,048 × 112-byte individual writes, explicit flush after each | 8.114789 | 4.374668 | 46.090% | 4,096 → 2,048 |
| Same writes, batches of 16, explicit flush after each batch | 0.518767 | 0.299169 | 42.331% | 256 → 128 |
| 256 × 1MiB alternating overwrites, batches of 16 | 1.900165 | 1.825239 | 3.943% | 83 → 83 |
| 6,144 × 112-byte appends, batches of 64, two checkpoints | 1.030469 | 0.993840 | 3.555% | 102 → 102 |
| State size calculation, 16 calls | 0.277765 | 0.049482 | 82.186% | 0 → 0 |

Sizing reads fall **275,771,904 → 0 bytes**, and cumulative managed allocations average **335,996,436 → 59,337,904 bytes**. In the checkpoint workload, reads fall **104,790,720 → 69,860,480 bytes** and allocations **148,880,144 → 113,957,860 bytes**. These are timed-operation cumulative allocations, not retained memory or whole-process peak RAM. The overwrite result includes checkpoint sizing but retains the original comparison algorithm; its small improvement is not a new large-speed claim.

Final Runtime SHA-256: `94e646cfad3a8a35654d312bc54f5a33758498dfd1f41f2b14f73426f357f428`. Baseline Runtime: `b9751fe7be66bd428457d0daea51ef68991414ec9eb6445f24c756bd1857222c`. The dataset records the raw files and assembly/source hashes. An independent post-run verifier checks expected lengths and every byte of **56 replayed files** across candidate and final comparisons, and rechecks all frozen input hashes. No base payload reads are allowed by the fake basis.

Remaining costs: final individual-write explicit-flush time is **4.374668s**, of which **4.317677s** is physical flush; preparation is **0.033705s** across all 2,048 writes. Adding preparation threads or a new binary metadata format does not address this measured dominant cost. In the checkpoint workload, checkpoint time is **0.707720s / 0.993840s** overall. Full private-state rewrite/proof/publication is the structural bottleneck; a different incremental checkpoint representation would need separate format/recovery work. Preparation counters include nested checkpoint work, so these counters must not be added together. None of this changes the durability contract or proves general workload speedups.

## WP-05 — regression and evidence limits

`pwsh -NoProfile -File scripts/build.ps1` passed locked restore and Release build with **0 warnings/errors**. The final full suite reports **1,626 total / 1,598 passed / 0 failed / 28 skipped**, summary duration **535.407s**. Logs and TRX are preserved as `final-gate.log` / `final-gate.trx`; the dataset records their hashes. The existing sharing test below also passed this full gate. The final Runtime hash matches the measured binary, and product/test code did not change after the final comparison.

An initial focused run had **453 total / 436 passed / 1 failed / 16 skipped**; the failure was the existing `BriefMetadataReaderWithoutDeleteSharingPublishesTheSameFlushedWrite` sharing/timing test, also recorded in RW. Its cause was not established as a product regression; the final targeted run passed **49/49**, including that test and the then-present edge candidate. Intermediate compilation/fixture-mode mistakes were corrected and their logs retained. Final full-suite evidence, rather than those diagnostic failures, qualifies the adopted code.

Raw evidence lives under `artifacts/write-path-20261025/`: `before1/after1/after2/before2.json` are the rejected-candidate comparison; `final-before1/final-after1/final-after2/final-before2.json` are the adopted-code comparison; `inputs.json`, `probe/`, `baseline/`, `candidate/`, `adopted/`, `verify/`, `replay-verification.json`, build/test logs and TRX files are retained. No Unity lifecycle, original-project application, native WinFsp timing, device change or hardware power-loss test was run. Existing qualification is not automatically extended. No commit or push is part of this work.
