# Write-path throughput

[한국어](../ko/plans/write-path-throughput.md)

The user authorized the follow-up optimizations on 2026-10-25 after reviewing the write path at `9594209`. Prioritize substantial elapsed-time reductions, retain raw range storage and durable success, and measure equivalent inputs before adopting further structural complexity.

- WP-01: elide redundant explicit SJR2 flush only when the same open journal and unchanged bytes have already passed a successful durable flush. Reconnect, replacement, failure and accepted queued writes retain their boundaries; SJL4 publication remains unchanged.
- WP-02: estimate raw state-frame size without reading/allocating payload. Actual output, checksums, complete state proofs, candidate/published replay and checkpoint recovery remain.
- WP-03: find matching private overwrite edges from both ends, without reading the changed middle. Preserve complete-private-coverage admission, the exact changed envelope and 4KiB reuse threshold, existing encoded readers and corruption handling.
- WP-04: compare frozen baseline/candidate binaries on fixed synthetic write/flush, checkpoint and overwrite inputs. Include per-stage time, physical flushes, journal-read bytes, managed allocations and complete replay checks. Use these results to judge preparation parallelism and format changes; do not adopt low-value concurrency, schema changes or removed experimental backends merely because possible.
- WP-05: regression tests, mandatory locked Release build/full suite, bilingual current contracts and recorded evidence. No Unity lifecycle, original-project operation, power-loss matrix, device change or automatic commit/push.

The new measurements are bounded components, not a repeat of closed Unity/DF experiments. Existing 16MiB/4,096-frame suffix bounds, format versions, original/peer/independent-result guards and no whole-file copy-up remain.

Outcome: WP-01/02 are adopted. WP-03's edge-search candidate was removed after lower read bytes failed to reduce total overwrite time; it is not an optional product path. [WP verification](../verification/write-path-throughput.md) owns the candidate and final comparisons. Preparation threads and a new metadata/checkpoint format were not introduced: measured small-write CPU work is minor, while full checkpoint publication remains a separate structural cost.
