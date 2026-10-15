# Product bounded-journal verification

[한국어](../ko/verification/bounded-journal.md)

**2026-10-15: scoped-complete-with-limitations.** This record owns BL-01–05 under the [fixed scope](../plans/bounded-journal.md) and [product contract](../bounded-journal.md). The previous fixed-object experiment is committed as `a8eb778`; its SX timings remain historical. This candidate uses the real mutation/state/recovery engine and a new ordinary Windows-provider default. Bee native backing was an [unimplemented design](../plans/bee-native-storage-design.md) at the BL gate. The later [BN experiment](bee-internal-native.md) was implemented, measured and then retired and removed after failing its performance gate; the bounded product path remains.

## Evidence status

| ID | Evidence | Status |
| --- | --- | --- |
| BL-01 | Mutation metadata, Unix mode, range/resize, directory and replacement rename, whiteouts, open-unlinked IDs, bounded bytes/frames, immutable read continuity, cache and original protection | Passed within stated scope |
| BL-02 | Same-owner initialization/checkpoint recovery, poisoned writer, corruption/unknown preservation, exact reclaim inventory, independent result after teardown, external process termination | Passed within stated scope |
| BL-03 | Provider default and schema; omitted/explicit compaction; exact reclamation; unchanged Library v2 directory plan; actual editorless A/B | Passed within stated scope |
| BL-04 | Matched serial SJL4/product-bounded private-range costs | Passed: one serial component pair |
| BL-05 | Required locked restore, Release build/full regression, paired documentation | Passed within stated scope |

## Safety evidence

The final required `pwsh -NoProfile -File ./scripts/build.ps1` completed locked restore and Release compilation with zero warnings/errors, then **1,691 passed / 0 failed / 24 conditional skips (1,715 total)**. The new focused coverage includes 36 bounded-journal cases, exact reclamation and native mode-transition tests. The final Runtime hash is `a8c03203733f23edbd01437ef4656d02165efcc5fcc987a29edeac7ee5cc2f89`, identical in the tests, probe, CLI and actual A/B runner. An earlier complete run passed 1,686 / 0 / 24 before the added recovery/native-transition corrections.

The final editorless product fixture uses a committed 1MiB file plus a text sentinel. A makes 384 flushed 64KiB range writes, crosses the 16MiB suffix threshold, then renames a newly created file. The observed mutation-appended bytes are 25,320,509B while the remaining suffix is 8,571,804B / 130 frames. B and the original retain their initial data hash. Omitted compaction retains bounded mode; explicit `--optimize-journal false` changes B to append mode.

A and B then use `workspace checkpoint --release`. B writes again after A releases. Both independent Git results survive removal of their mounts/deltas and owned basis references; original HEAD, status and payload hash remain unchanged. Final task IDs are `ca62657d74bfb6f4b3351886` and `8de9b0357ce77381b761c55e`. These operations overlap automated regression and supply correctness evidence, not performance figures. The earlier successful A/B fixture is retained separately.

`product-selftest` externally terminates its exact owned child at five append boundaries and seven automatic-checkpoint publication boundaries. All **12/12** recover the externally observed ACK prefix, allowing only a fully recorded but not externally acknowledged final mutation. Original contents remain unchanged. The final raw record is `artifacts/bl-20261015/process-recovery-03/product-selftest-result.json`.

A failed process run exposed a real recovery defect: read-only `Stat/List` cache nodes entered the checkpoint state hash without being recorded in the journal. Bounded state now tracks recorded identities separately, preserving live lookup handles while reconstructing untouched files from the immutable basis after restart. Seven interruption tests warm 15 base-file lookups first; separate cases cover renamed cached children, metadata promotion and an unlinked immutable file written after replacement. The failed fixture remains preserved. A prior harness-only failure was a sharing violation reading the ACK file just after child exit; the exact read now retries only Windows sharing/lock errors for at most two seconds.

Native append/latest conversion tests hold Windows ancestry/owner pins across publication and verify the Files/Seal → reconnect → Complete sequence. Unknown files, competing generation receipts, corrupted frames, foreign owners, live writers/readers and unsafe path transitions remain refused. Reclamation and unchanged-Library restoration tests exercise the exact bounded inventory and v2 directory plan. These small tests do not establish a new real Unity Library restoration time.

## Component cost (BL-04)

[Exact data and provenance](../../reports/benchmarks/bounded-journal-product-20261015.json) record a single ordered SJL4 → bounded pair after all builds/tests stopped. Both run the same product Runtime and immutable bytes: 20,000 logical objects, 256 hot files, 48MiB seeded private ranges (3,072 writes), then 4,096 deterministic incompressible 4/8/16KiB writes. Runtime's directory guard factory/WinFsp callbacks are outside this component fixture. Original hash, complete logical contents, reopen sequence and independent result after store teardown pass for both.

| Measured phase | SJL4 | Bounded |
| --- | ---: | ---: |
| Initialize and seed private state | 47.310s | 9.504s |
| 4,096 writes, including automatic checkpoints | 53.214s | 11.335s |
| Validate all contents | 0.728s | 0.752s |
| Reopen and validate | 2.973s | 0.625s |
| Export independent result | 0.492s | 0.296s |
| Reclaim owned store | 0.548s | 0.006s |
| Validate independent result after teardown | 0.104s | 0.102s |
| **Measured phase sum** | **105.370s** | **22.619s** |

Writes decreased **78.7%**, and the measured phase sum decreased **78.5%**. Fixture construction, initial input-hash admission, process launch and reporting are outside this sum. This is one run per mode without OS-cache reset/randomized order, not a whole-workspace or Unity result.

| Cost/tradeoff | SJL4 | Bounded |
| --- | ---: | ---: |
| Write ACK median / p95 | 10.843 / 21.345ms | 2.065 / 2.501ms |
| Maximum write ACK | 148.676ms | 1,211.853ms |
| Journal logical bytes after writes, `current-files` | 49.825MiB | 58.258MiB |
| Process peak working set observed through writes | 99.422MiB | 82.125MiB |
| Cumulative managed allocations during writes | 662.876MiB | 300.218MiB |
| Durable flush calls during the 4,096 writes | 4,096 | 4,102 |

Bounded makes five automatic checkpoints in total, three while seeding and two during the measured writes. The two take **2.412s** and record **104,983,951B** of state-prefix frames, in addition to **40,735,128B** of ordinary mutation frames. Prefix counters exclude intent/control writes and do not measure total device writes. The normal final suffix remains 8,406,086B / 845 frames. No extra forced consolidation is added solely for the benchmark.

This saves average write time while introducing occasional approximately 1.2s stalls. Final journal length increases **16.9%**; working set in this fixture decreases **17.4%**, but checkpoint temporarily retains both current private-state generations. Physical allocation and peak whole-store bytes were not measured. The probe's diagnostic `inventory` lists top-level files only, omitting nested SJL4 objects; it is **not** used as a storage total. The table uses the product's `current-files` journal observation, which includes those objects. These distinctions prevent a misleading whole-store or RAM-bound claim.

## Boundaries

The 16MiB/4,096-frame limit applies to the appended suffix. Current-state prefix bytes and one checkpoint's temporary duplication are separate; prefix bytes superseded by later writes may remain until checkpoint. Each mutation retains its own durable flush/ACK. Automatic checkpoint copies only current private ranges and full logical metadata, never immutable original payload to fill gaps.

Component measurements must include initialization, writes/automatic checkpoints, validation, reopening, independent result export and owned cleanup. A normal final bounded suffix is retained; an extra forced final consolidation is not part of ordinary bounded operation. Checkpoint prefix-written bytes exclude control/intent bytes and are reported separately from mutation-appended bytes. Working set, managed allocations, logical file lengths, sampled bytes and actual NTFS allocation are different quantities.

Exception injection and externally terminated owned processes do not qualify OS power loss, kernel crash, hostile concurrent mutation or all filesystem races. Small product file tests do not qualify a new complete Unity workflow, Editor compilation, Play/build, large-project speedup, or universal external-tool compatibility. The original Unity project and Library are not used by this validation. No service, installation, UAC, ACL or external-tool changes are included.
