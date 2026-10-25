# Raw write and batch-flush scope

[한국어](../ko/plans/raw-write-batching.md)

The user explicitly requested retirement of write compression and implementation of combined flush on 2026-10-25. This supersedes prior per-mutation physical-flush and compression requirements. Retain per-response durability: no early ACK or periodic write-back. The contract is [raw writes and shared flush](../raw-write-batching.md).

RW-01–05 are complete within this scope; [verification](../verification/raw-write-batching.md) records the final regression gate and measured component/native limits.

- RW-01: remove every production compression writer and selection path; keep current encoded-data readers and corruption/recovery coverage.
- RW-02: bounded ordered SJR2 batch API, shared durability, checkpoint drain, failure poison and reconnect. Preserve single writes and SJL4 publication safety.
- RW-03: collect actual concurrent WinFsp callbacks before serialization; preserve append/constrained semantics, native-buffer lifetime and bounded staging.
- RW-04: fixed raw-input serial/batch component measurement and one new small native WinFsp fixture, with verified bytes, normal unmount and replay. Do not replay old Unity or retirement matrices.
- RW-05: update current English/Korean contracts, capability schema and tests; run the required locked Release build/test gate.

No original apply, Unity lifecycle timing, device settings, new storage backend, CLI batching switch or automatic old-format migration is included. A synchronous single-request caller can retain one flush per request; measured batching benefit must not be promoted to a universal speedup.
