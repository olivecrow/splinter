# Durable flush and shutdown throughput

[한국어](../ko/plans/durable-flush-throughput.md)

The user authorized implementing the opportunities found in the flush/shutdown review. Baseline is `663c400`. This slice targets one workspace's write and finalization cost; it is not an A/B scheduling change or a resumption of the canceled Unity shutdown diagnosis. **Disposition: completed without product adoption.** The prepared-frame prototype was removed after the measured large-gain criterion failed; individual flushes and current product behavior remain.

| ID | Scope and acceptance |
| --- | --- |
| DF-01 | Compare fixed small NTFS durable-write patterns serially, including actual flush completion, allocation and byte verification. Measure alternatives before choosing a product I/O change. |
| DF-02 | Reuse bounded, operation-owned RAM preparation during bounded state checkpointing. Preserve exact estimated/output bytes, full logical/physical proof, individual durable ACK, failed-writer poisoning, generation ownership and recovery. Keep a bounded fallback for larger states. |
| DF-03 | Same-input before/after component measurements in ABBA order, including automatic checkpoint and replay/closure. Prefer substantial reductions in the affected workload; do not promote a raw I/O microbenchmark to Unity speed. |
| DF-04 | Focused corruption/interruption/budget regressions and the required locked-restore Release build/test gate. |
| DF-05 | Paired implementation and verification documentation, exact measurements and unrun boundaries. |

Experiments use only new owned temporary files and retained evidence under `artifacts/durable-flush-20261024/`. They do not change an original project, start Unity, alter installed tools/services/device write-cache settings, kill existing processes, or reclaim old evidence. Per-request durability, the 16MiB/4,096-frame limit and existing on-disk formats stay unchanged. Group commit and asynchronous early ACK are not included: the upstream request overlap is not yet established, and changing that ordering is a separate contract. Direct-I/O candidates are not adopted without proving their durability/format/metadata requirements. Failed candidates remain evidence only, not optional product backends.

Whole Unity shutdown, original application, OS power failure, other storage hardware and full-process peak RAM require separate qualification. Component timing must not overlap builds or other probes. The [verification record](../verification/durable-flush-throughput.md) records the measured disposition; these experiments are not pending product work or an automatic rerun request.
