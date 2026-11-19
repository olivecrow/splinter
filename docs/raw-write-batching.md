# Raw writes and shared durable flush

The user retired compression on 2026-10-25. New writes, current-state checkpoints and newly materialized candidate/latest-state payloads store raw changed ranges. No production Brotli encoder or compression-selection path remains. Space sharing still comes from the immutable basis and private changed ranges, without initial full copies, read hydration or whole-file copy-up.

Retired encoded payloads are preserved and rejected. Only raw payloads with equal stored/decoded lengths are admitted; no compatibility decoder, decoded cache or automatic migration remains.

## Ordered durability

`IWorkspaceWriteBatch.WriteBatch` accepts at most **64 range writes / 16MiB**. Input buffers remain unchanged and caller-owned until return. Writes apply in order under the store gate; SJR2 appends their checksummed frames and completes one shared `Flush(true)` before success. Pending state is inaccessible to other callers. A failure may leave an unacknowledged prefix, poisons an uncertain writer and requires replay; the batch is not a rollback transaction. A checkpoint boundary first flushes pending frames and retains the existing **16MiB / 4,096-frame** suffix limits, full state proofs, publication intent and recovery.

Single `Write` calls still finish their durable flush before returning. SJL4 keeps its existing capsule publication barriers; it receives raw payloads but does not combine its separate capsule commits. A source/cache view forwards consecutive writes to their owning journal, without reordering cross-partition writes or pretending that two files share one physical flush.

## Windows admission

The optional `IWorkspaceMutationBatch` also combines range and metadata callbacks in one owning journal, with at most 64 callback/chunk units and 16MiB requested staging per shared barrier. A mutating leader gathers when several callbacks are queued or a caller was waiting at the end of the preceding group. With multiple open handles it uses recent service cost to select a nominal 20–250µs cooperative yield window and a target of 2–8 available mutations. A serial writer with other files open proceeds directly to the existing durable barrier. This is not a real-time latency guarantee; it never timer-sleeps or returns success before durability. Supported-store Open takes a direct path only when nonblocking mutation-gate admission proves no queued callback or running leader; otherwise Open/Overwrite retain the common FIFO and post-durability handle retention. Oversized callbacks retain their existing split barriers, and unsupported/composite stores retain their per-store fallback. Failed prefixes, buffer leases, ordered drain and post-flush accounting remain protected. See ST-83–86 for current local evidence and ST-79–82 for historical variable/unfavorable blocks. New mixed-admission/shared-index counters are internal diagnostics, not public command fields or durability receipts.

The SCAL throughput follow-up also exposes the app/OS-neutral `IWorkspaceMetadataBatch` contract for at most 64 synchronous metadata mutations. WinFsp queues concurrent size/time callbacks before the mutation gate and acknowledges them only after the owning journal's shared durable flush. Accepted metadata and write tickets both drain before explicit flush/capture. Failed batches preserve an uncertain prefix and require replay; SJL4 retains individual capsule barriers. See ST-10 validation.

The WinFsp adapter queues concurrent write callbacks **before** its mutation gate. A caller synchronously leads the next FIFO batch; callers waiting during that batch can share the following flush. There is no background writer, collection sleep or early acknowledgement. Native callback buffers stay alive until their callbacks return. Staged payload is capped at 64 chunks / 16MiB of logical bytes per commit; ArrayPool rounding/retention and caller/native buffers are separate costs, not a total RAM cap; oversized callbacks are segmented and all segments finish before the callback succeeds. Append offsets and constrained lengths account for earlier writes in the same batch. Other mutations still serialize through the same gate.

An explicit flush first drains the write tickets already accepted by the adapter. Close, seal and reclamation retain their durability and ownership boundaries. Interrupted waiters retain native-buffer ownership until their queued writes drain. There are no pending writes from a callback that has already returned success. A strictly sequential client sending its next request only after the previous completion offers no cross-request batching opportunity. Multiple chunks of one callback can still share a barrier. Increasing the number of flush threads is not this design.

## Write-path follow-up

An explicit SJR2 flush may reuse a successful durable flush only for the same open journal handle and unchanged journal length, after local integrity checks and draining accepted writes. A new/reconnected or replaced handle must establish its own durability. Mutation and batch flushes still run before success; failed writers remain poisoned. SJL4's explicit flush and capsule publication are unchanged.

Raw current-state sizing uses known payload lengths without reading or allocating payload bytes. Actual checkpoint output, checksums, full state proofs and replay still validate the data. The experimental raw overwrite edge search was removed because reduced reads did not improve total overwrite time. Existing raw comparisons, complete private coverage and the 4KiB saved-edge threshold remain.

## Scope

RW-01–05 verification separates raw-format/reader tests, ordered/failing batch tests, callback tests, fixed-input component cost and native WinFsp evidence. None establishes Unity's actual request overlap, full shutdown/lifecycle gain, hardware power-loss qualification or whole-process peak RAM. No original project, Unity Editor, external tool or device setting is changed by this work.
