# Raw writes and shared durable flush

[한국어](ko/raw-write-batching.md)

The user retired compression on 2026-10-25. New writes, current-state checkpoints and newly materialized candidate/latest-state payloads store raw changed ranges. No production Brotli encoder or compression-selection path remains. Space sharing still comes from the immutable basis and private changed ranges, without initial full copies, read hydration or whole-file copy-up.

Existing encoded payloads in the current SJR2/SJL4 representations remain readable and validated by `IWorkspacePayloadReader` / `BrotliWorkspacePayloadReader`. Reconnect does not rewrite them. Compaction may retain an existing encoded frame unchanged when that is the smaller valid representation; it never compresses new data. A rewritten current-state prefix uses raw bytes. This is preservation of current-format data, not support for another version or automatic migration.

## Ordered durability

`IWorkspaceWriteBatch.WriteBatch` accepts at most **64 range writes / 16MiB**. Input buffers remain unchanged and caller-owned until return. Writes apply in order under the store gate; SJR2 appends their checksummed frames and completes one shared `Flush(true)` before success. Pending state is inaccessible to other callers. A failure may leave an unacknowledged prefix, poisons an uncertain writer and requires replay; the batch is not a rollback transaction. A checkpoint boundary first flushes pending frames and retains the existing **16MiB / 4,096-frame** suffix limits, full state proofs, publication intent and recovery.

Single `Write` calls still finish their durable flush before returning. SJL4 keeps its existing capsule publication barriers; it receives raw payloads but does not combine its separate capsule commits. A source/cache view forwards consecutive writes to their owning journal, without reordering cross-partition writes or pretending that two files share one physical flush.

## Windows admission

The optional `IWorkspaceMutationBatch` also combines range and metadata callbacks in one owning journal. WinFsp uses a common FIFO for supported stores, including Open/Overwrite, with at most 64 callback/chunk units and 16MiB requested staging per shared barrier. A mutating leader with multiple open handles may yield to a 100µs monotonic deadline before taking mutation/journal gates; it never timer-sleeps or returns success before durability. Oversized callbacks retain their existing split barriers, and unsupported/composite stores retain their per-store fallback. Failed prefixes, buffer leases, ordered drain and post-flush accounting remain protected. See [ST-79–82](verification/scalable-storage-throughput-followup.md#st-7982) for local qualification and unfavorable timing blocks.

The SCAL throughput follow-up also exposes the app/OS-neutral `IWorkspaceMetadataBatch` contract for at most 64 synchronous metadata mutations. WinFsp queues concurrent size/time callbacks before the mutation gate and acknowledges them only after the owning journal's shared durable flush. Accepted metadata and write tickets both drain before explicit flush/capture. Failed batches preserve an uncertain prefix and require replay; SJL4 retains individual capsule barriers. See [ST-10 validation](verification/scalable-storage-throughput-followup.md).

The WinFsp adapter queues concurrent write callbacks **before** its mutation gate. A caller synchronously leads the next FIFO batch; callers waiting during that batch can share the following flush. There is no background writer, collection sleep or early acknowledgement. Native callback buffers stay alive until their callbacks return. Staged payload is capped at 64 chunks / 16MiB of logical bytes per commit; ArrayPool rounding/retention and caller/native buffers are separate costs, not a total RAM cap; oversized callbacks are segmented and all segments finish before the callback succeeds. Append offsets and constrained lengths account for earlier writes in the same batch. Other mutations still serialize through the same gate.

An explicit flush first drains the write tickets already accepted by the adapter. Close, seal and reclamation retain their durability and ownership boundaries. Interrupted waiters retain native-buffer ownership until their queued writes drain. There are no pending writes from a callback that has already returned success. A strictly sequential client sending its next request only after the previous completion offers no cross-request batching opportunity. Multiple chunks of one callback can still share a barrier. Increasing the number of flush threads is not this design.

## Write-path follow-up

An explicit SJR2 flush may reuse a successful durable flush only for the same open journal handle and unchanged journal length, after local integrity checks and draining accepted writes. A new/reconnected or replaced handle must establish its own durability. Mutation and batch flushes still run before success; failed writers remain poisoned. SJL4's explicit flush and capsule publication are unchanged.

Raw current-state sizing uses known payload lengths without reading or allocating payload bytes. Actual checkpoint output, checksums, full state proofs and replay still validate the data. The experimental raw overwrite edge search was removed because reduced reads did not improve total overwrite time. Existing raw/encoded comparisons, complete private coverage and the 4KiB saved-edge threshold remain.

[WP-01–05 verification](verification/write-path-throughput.md) records the fixed-input before/after costs and remaining checkpoint/flush bottlenecks. No journal format, public method or version changed.

## Scope

[RW-01–05 verification](verification/raw-write-batching.md) separates raw-format/reader tests, ordered/failing batch tests, callback tests, fixed-input component cost and native WinFsp evidence. None establishes Unity's actual request overlap, full shutdown/lifecycle gain, hardware power-loss qualification or whole-process peak RAM. No original project, Unity Editor, external tool or device setting is changed by this work.
