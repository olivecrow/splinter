# Durable flush and shutdown throughput experiments

[한국어](../ko/verification/durable-flush-throughput.md)

**DF-01–05, 2026-10-24: completed without a product optimization being adopted.** The [scope](../plans/durable-flush-throughput.md) follows the user's request to implement the flush/shutdown opportunities while prioritizing large savings. Baseline is `663c400`. A bounded checkpoint RAM prototype was implemented, tested and removed after failing the performance criterion. Current product source/tests match that baseline. The [dataset](../../reports/benchmarks/durable-flush-throughput-20261024.json) retains individual samples, runtime/source hashes and fixed-input evidence. Local raw evidence, frozen runtimes and rejected source are under `artifacts/durable-flush-20261024/`; they are not product backends or new CLI entry points.

## DF-01 — individual write alternatives

Each raw NTFS invocation writes the same 576-byte payload 2,048 times to a new owned temporary file, waits for the selected completion operation after every write, closes and checks every record. Each listed mode has 2 observations in forward/reverse order. Timing includes create/write/completion/close; byte readback is outside timing. The first four-mode probe is retained separately. The extended and data-sync comparisons below use their own baseline observations.

| Comparison | Mode | Mean seconds | Logical file length | Disposition |
| --- | --- | ---: | ---: | --- |
| Extended | Buffered `Flush(true)` | 4.365 | 1,179,648 B | Retain current behavior |
| Extended | Preallocation + `Flush(true)` | 4.347 | 1,179,648 B | No substantial gain |
| Extended | Write-through + `Flush(true)` | 4.360 | 1,179,648 B | No substantial gain |
| Extended | Write-through + managed-buffer drain only | 0.141 | 1,179,648 B | Not adopted; equivalent storage-cache durability was not established |
| Extended | Unbuffered write-through, 4,096-byte slots | 0.116 | 8,388,608 B | Not adopted; 7.11× file length and different representation, no equivalent power-loss qualification |
| Data-sync | Buffered `Flush(true)` | 4.360 | 1,179,648 B | Separate matched baseline |
| Data-sync | `NtFlushBuffersFileEx(FILE_DATA_SYNC_ONLY)` | 4.345 | 1,179,648 B | No substantial gain |

The data-sync flag still synchronizes the underlying storage cache and retains metadata required to retrieve data, while avoiding timestamp updates where possible ([Microsoft contract](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/ntifs/nf-ntifs-ntflushbuffersfileex)). It was evaluated only in the raw probe and was not added to product adapters. The much faster write-through-only completions are not evidence that an explicit flush is redundant: hardware/driver treatment of FUA can differ from a storage-cache flush ([Microsoft explanation](https://devblogs.microsoft.com/oldnewthing/20170510-00/?p=95505/)). Normal byte readback does not test sudden power loss. No drive setting, installed tool, service, original project or existing process was changed.

## DF-02–03 — bounded checkpoint preparation

The prototype retained the sizing pass's serialized metadata and encoded payload, then reused them during state output. Its budget was 64MiB of accounted content/overhead and 65,536 entries per checkpoint; an overflow discarded the prepared frames and used the existing streaming second pass. It preserved the 16MiB/4,096-frame limits, formats, every mutation's flush/ACK, independent logical/physical validation, both candidate/published replays, ownership and poisoned-writer/recovery behavior.

One closed synthetic input contains 20MiB of large private payload plus 16,000 × 112-byte private writes. Each timing process copies this same stopped fixture outside timing, reconnects it, overwrites 10,305 × 112-byte ranges and verifies the complete edited range and durable sequence after reconnect. Every run performs 3 automatic checkpoints, 10,305 commits and 10,314 counted flushes. No immutable source payload is copied into the journal. This models a small-write tail; it is not a recording/replay of Unity's actual callbacks or file distribution.

The pilot changes total write time from **24.463s to 24.224s**, only **0.239s / 1.0%**. Checkpoint time changes from 2.538s to 2.263s. It is not pooled into the final series:

| Final order | Version | Write interval, s | Checkpoint time, s | Cumulative flush time, s |
| --- | --- | ---: | ---: | ---: |
| 1 | Before | 25.409 | 2.540 | 22.620 |
| 2 | RAM candidate | 35.567 | 2.610 | 33.022 |
| 3 | RAM candidate | 39.830 | 2.872 | 37.374 |
| 4 | Before | 24.340 | 2.492 | 21.581 |

Final means are **24.874s before / 37.699s candidate**. Most of the increase is measured inside durable flush; its cause is not isolated as a causal code regression. It nevertheless fails the requested large-gain criterion. Checkpoint time and flush time overlap and must not be added. Cumulative managed allocation falls from 458,898,640 B to 421,494,808 B; this is not peak RAM or sufficient reason to retain a time optimization that failed. Builds/probes did not overlap measured intervals; OS caches were not cleared.

The prototype and its dedicated tests/probes were removed from tracked source. Frozen source/binaries and raw results remain for inspection; no optional RAM or native-I/O mode was added. There is no new product speed claim.

## DF-04–05 — validation and remaining boundary

The experimental candidate's focused bounded-journal, preparation-budget and write-metric suites passed **41 tests, 0 failures, 0 skips** in 11.855s. These include 0/tiny/retained preparation budgets, compressed/raw bytes, rename/metadata, live unlinked identity, per-mutation flush and existing bounded interruption/recovery cases. They qualify those file-backed behaviors, not a general OS-crash boundary. Initial restricted build diagnostics and every probe log are retained.

The final required `pwsh -NoProfile -File scripts/build.ps1` passed locked restore, Release compilation with 0 warnings/errors, and **1,580 passed / 0 failed / 26 conditional skips (1,606 total)** in 555.570s. Final Runtime SHA-256 is `2daec23a795c8a156220fcd1ba4d3b02d0deb26ce777b513ef367b9b1b1eb526`, identical to the frozen baseline. Product source and test changes have been removed; only this decision, measurement and navigation documentation remain. The fixed fixture's complete file lengths and SHA-256 hashes still match after all runs.

Group commit was not implemented: the actual requests waiting before the WinFsp mutation gate are still unmeasured, and a synchronous caller may supply no requests to combine. It requires a separately defined queue/order/visibility/failure contract while keeping each successful response durable. Raising the stored frame limit, early ACK, force termination, premature unmount/Library restoration and the retired native Bee backend were not substituted for the failed candidates. New Unity shutdown/full-lifecycle timing, original application, power-loss/device qualification and whole-process peak RAM are **NotRun**. Existing LU-01 and the unresolved historical debugger diagnosis retain their original qualifications.
