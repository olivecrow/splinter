# Raw writes and shared-flush verification

[한국어](../ko/verification/raw-write-batching.md)

**RW-01–05, 2026-10-25.** Baseline is `663c400`; this working-tree change implements the user's explicit compression retirement and batch-flush request. [Contract](../raw-write-batching.md), [scope](../plans/raw-write-batching.md), [dataset](../../reports/benchmarks/raw-write-batching-20261025.json). The earlier DF experiment remains a separate rejected candidate.

## RW-01 — raw payload and retained current-format reads

Production `Encode`, `EncodeJournalPayload`, Brotli compression selection and unused compressed-compaction staging are removed. New append/bounded/latest writes and reconstructed state/candidate payloads are raw. Tests cover former size thresholds with compressible/incompressible inputs and raw physical payload lengths. Existing current-format encoded SJR2 fixtures are constructed only in tests; they exercise partial reads, cache validation, corruption with an otherwise valid outer checksum, mixed old encoded/new raw writes and independent candidate readback. Readers retain full block checks and do not introduce older-version fallback.

## RW-02–03 — ordered batches and callback admission

Runtime/fake conformance tests cover ordered overlapping writes and separate identities. Runtime tests cover 64-write/16MiB admission bounds, empty batches, single-write durability, batches crossing bounded checkpoints, injected incomplete append/before-flush/after-flush failures, blocked reads/disposal and reconnect. Source/cache tests cover ordered partition routing and replay. A controlled callback test holds the first flush while 8 followers enter the queue, then proves 9 writes complete with 2 flushes and correct append contents. Native buffers remain owned until each callback returns. Additional cases check explicit flush waiting for accepted tickets and an interrupted follower retaining its input until drain.

There is no early ACK, timer collection window, background writer or rollback promise. SJL4 still publishes individual capsules; raw payload policy applies there too. Single-request synchronous clients do not provide cross-request batching opportunities. Explicit flush/close/result/seal/reclamation protections remain. Metrics count the physical shared flush once, attributed to the last mutation in that group; each mutation retains its own durable commit count.

## RW-04 — component and native evidence

The fixed component input is **2,048 × 112 bytes**, the same bytes and append offsets each time, default bounded SJR2. Every result is closed/reconnected and every byte verified. Setup/replay checks are outside the write timer. Runs are serial → batch → batch → serial, with no overlapping builds or probes. Batches are explicitly supplied; this is not a captured Unity workload.

| Order | Supplied writes per batch | Seconds | Durable mutations | Physical flushes |
| --- | ---: | ---: | ---: | ---: |
| 1 | 1 | 4.346879 | 2,048 | 2,048 |
| 2 | 16 | 0.286335 | 2,048 | 128 |
| 3 | 16 | 0.276534 | 2,048 | 128 |
| 4 | 1 | 4.342911 | 2,048 | 2,048 |

Means are **4.344895 → 0.281435s**, **93.523%** lower for this supplied-batch workload. All runs store 229,376 payload bytes. This is a comparison of serial/batch paths with compression already removed, not a complete before/after product lifecycle or an isolated measurement of compression removal.

One real local WinFsp mount over a fake immutable basis uses **8 simultaneous synchronous writers**, each appending **128 × 512 bytes** with write-through client handles. It records **1,024 mutations / 256 flushes**, **524,288 payload bytes**, and **0.570576s** from releasing the writers through their close. No before/after native timing pair is claimed. All client handles/retained contexts reached 0, the mount unmounted normally, and replay matched all bytes. The owned temporary fixture was removed. No original project, Editor, installed tool or storage setting was changed.

Raw evidence: `artifacts/raw-group-cost-final.{json,log,trx}` and `artifacts/raw-group-native-final.{json,log,trx}`. These measurements use the final Release build, including explicit-flush ticket draining and interrupted-waiter buffer protection. Initial implementation measurements are preserved separately in the dataset. Final Runtime SHA-256 is `b9751fe7be66bd428457d0daea51ef68991414ec9eb6445f24c756bd1857222c`; the dataset also records other assembly and evidence hashes. No new Unity startup/shutdown/full-lifecycle timing, original application, power-loss test or whole-process peak RAM measurement was run.

## RW-05 — final gate

`pwsh -NoProfile -File scripts/build.ps1` passed locked restore and Release build with 0 warnings/errors, followed by **1,621 total / 1,593 passed / 0 failed / 28 skipped**, with test summary duration **524.459s**. Logs and TRX are preserved as `artifacts/raw-group-final-gate.{log,trx}`. The optional final native and component measurements then each passed **1 test / 0 failures / 0 skips**, sequentially with no overlapping builds or probes. Code did not change afterward.

Initial focused failures exposed compression-dependent expectations. The unchanged `BriefMetadataReaderWithoutDeleteSharingPublishesTheSameFlushedWrite` test failed once in the focused follow-up and passed the final full gate; that isolated failure's cause was not separately established. The initial full gate's 8 failures were obsolete-payload size assertions, corrected to exact raw sizes. Diagnostic logs and the initial full TRX are preserved rather than reported as passes. Skipped opt-in experiments do not acquire new qualification.
