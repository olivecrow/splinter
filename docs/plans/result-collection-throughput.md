# File result collection throughput

[한국어](../ko/plans/result-collection-throughput.md) · [Verification](../verification/result-collection-throughput.md) · [Lifecycle map](../lifecycle-optimization-map.md#c20)

## Scope and acceptance

2026-10-26 follow-up to the lifecycle review. Optimize C20 for `project-snapshot` / None and recorded Plastic without changing persistent formats or weakening independent results. The source scan used to run in checkpoint, inspection and preservation. Reuse one manifest inside the existing coordinator-owned pre/post-seal interval, then bound independent payload copy/flush/proof work to eight workers. Keep recorded Plastic revision acquisition serial; its reader concurrency is not assumed.

RT-03 uses the actual Windows CLI, NTFS and WinFsp with one editorless task at a time, a fixed 2.125 GiB / 2,112-file source, the same private edit and one common project basis. Freeze the baseline at `9b68a82cd89912e404d8acd6dcf7e48ce513a353`, build both variants with the same SDK, run before/after/after/before, and record creation, edit, checkpoint and separate release. Common prepare/restore and independent input/result proofs are separate. Acceptance: at least 20% and 10 seconds mean checkpoint reduction with identical inputs/results and successful owned reclamation. Approximately 13 GiB fixture/result storage is budgeted; no concurrent build/tests during timing. OS caches are not forcibly cleared.

## Preserved boundaries

- Task lock, normal application stop and zero owned processes precede collection. Every successful collection still needs equal complete journal seals.
- Full baseline and result payloads remain independently preserved, hash-addressed and deduplicated per task. Every new object is durably flushed and proved before publication. Existing/interrupted objects are freshly verified; no verification cache crosses invocations.
- All worker copies/flushes join on failure/cancellation. Fixed collection intent, partial objects and the workspace remain for recovery; a failed collection cannot authorize reclamation.
- Public CLI/schema and stored state/manifest/journal versions stay unchanged. Git collection, writes/ACK, original publication and discard semantics stay unchanged.

## Tracking

| ID | Work / evidence |
| --- | --- |
| RT-01 | Combined file checkpoint/preservation through the existing `ISnapshotCheckpointSourceProvider`; Plastic delegates only after isolation checks. |
| RT-02 | Bounded payload work; stable-seal failure/retry, corrupt pending payload, worker drain and existing result/ownership/apply regressions. |
| RT-03 | Actual matched-input NTFS/WinFsp ABBA comparison and exact owned cleanup. |
| RT-04 | Required locked restore/build/full suite; bilingual contract and lifecycle map/index synchronization. |

## Other reviewed candidates

Git proof prefetch during Library admission needs a separate cache-lifetime experiment: a proof/payload evicted before index consumption can be decoded again. It is not enabled merely by moving work to a background thread. Incremental bounded checkpoint publication and unchanged-subtree restore require separate on-disk recovery designs and failure matrices; this change does not silently replace them. Unity Editor startup/compile/full-lifecycle, a live Plastic server, original application performance, and power-loss qualification are outside RT-03. Existing recorded qualifications are not rerun or inherited.
