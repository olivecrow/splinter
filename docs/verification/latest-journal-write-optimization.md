<a id="현재본-저널-쓰기-경로-최적화-검증"></a>
# Latest-only journal write-path optimization verification

[한국어](../ko/verification/latest-journal-write-optimization.md)

This is the 2026-10-06 user's [explicit scope](../plans/latest-journal-write-optimization.md)/[current contract](../latest-only-journal.md). Current candidate is SJL4/root v4 capsule with SJB1 80B owner header; overlay/workspace v2, candidate v3/SJR2, and compaction intent v3 remain. Previous SJL1/2/3 and SJT2/3 are preserved/rejected, with no automatic migration. Current file/process/fake-basis WinFsp qualification is not expanded to historical Unity/VSS/large/OS-power qualification.

<a id="저장과-복구-경계"></a>
## Storage and recovery boundaries

C payload/metadata are recorded once in one capsule, then published to current root after one `Flush(true)` per mutation. Previous root moves through `File.Replace` to an owned backup without copying. When only current private blocks remain, previous metadata footer is truncated; when unreferenced, deletion follows owned-hash verification. Success returns after replaced payload/metadata reclaim. Flush/ACK from different mutations are not combined.

Removed full-directory/file-length resummation for writer budget checks/ACK observation. Starting from the initially verified actual total, new-capsule publication and exact owned-file reclaim lengths update the total incrementally. Explicit observation/seal/reconnect verifies actual file inventory/content checksum. Metadata accounting is not a payload cache or substitute for source-content verification.

`JournalLatestOnlyTests` verifies B retained on incomplete C, publication/reclaim of complete C, corrupt complete-capsule/unknown-file preservation, rejection of missing current root after publication, and previous-format rejection. Verifies pending length/recovery including 512KiB C once, 1 flush per payload/metadata mutation, partial replacement of multiple private blocks, metadata truncation, and equality of incremental bytes to actual totals after reconnect. `JournalLatestProcessTests` covers 5 termination/reconnect boundaries of exact nonce/PID/start owned children: intent prefix, block prefix, stage complete, root publication, and previous reclaim. Basis is fake immutable; this is not OS power-loss qualification.

Intermediate SJL3 improved time only 7–8% with single payload recording and parallel flush of 4 independent files. The next candidate with only SJL4 single flush also failed to improve the 32-file write median. Stage measurement in the existing benchmark identified whole-file-length summation costs for budget/ACK; incremental accounting was applied. Boundary totals in diagnostic `artifacts/latest-capsule-profile-20261006/` include final fixed-metadata work and are not regular write latency.

<a id="고정-입력과-측정"></a>
## Fixed input and measurement

Uses 32 committed PNGs (47,854,812B) from `splinter-unity @ b09fa58e9d40a560d4fe1af7a213f8ac23e8e3b1`. Profiles: 8 files/2 ranges/6 B→C repetitions (192 writes), 32 files/3 ranges/8 B→C repetitions (1,536 writes); ranges 128KiB, offset 65,536 + range×262,144. Final content does not return to basis bytes. No compact during cycles.

Each runner OFF/ON ran 3 times in OFF/ON→ON/OFF→OFF/ON order. Generations are prior SJL2→intermediate SJL3→SJL4 single flush→final SJL4 incremental accounting; immutable runner hashes/actual inputs preserved for each. Build/test does not overlap measurement. Seed/activation/allocation measurement/final fixed metadata/full SHA verification are outside timed writes. Reconnect includes lease/tree/replay/composite seal. Sums NTFS file data allocation of the full current journal and checks per-cycle ON payload/allocation and final paths/metadata/whole-file hashes/seal. OS cache not cleared; actual OS physical I/O/MFT/whole instantaneous peak/shared attribution unknown.

Medians of 3 runs each. Prior ON is SJL2/SJT2; current OFF is the same input with final runner.

| Item | 8 files/192 writes | 32 files/1,536 writes |
| --- | ---: | ---: |
| Prior ON writes | 3.947s | 32.299s |
| Final ON writes | 2.783s | 22.389s |
| ON time reduction | **29.5%** | **30.7%** |
| Final OFF writes | 0.498s | 3.834s |
| Final ON/OFF ratio | 5.58× | 5.84× |
| ON p50 prior → final | 20.457 → 14.431ms | 20.780 → 14.282ms |
| ON p95 prior → final | 22.483 → 17.431ms | 23.503 → 17.159ms |
| Final OFF file data allocation | 26,214,400B | 203,030,528B |
| Final ON file data allocation | 2,163,040B | 12,976,480B |
| Final ON/OFF allocation reduction | **91.75%** | **93.61%** |
| ON reconnect prior → final | 1.138 → 1.110s | 1.392 → 1.387s |
| Durable flush per timed write | 1 | 1 |

Final ON write ranges across 3 runs: 8 files 2.768–2.793s, 32 files 22.113–22.478s. Final OFF: 0.485–0.555s/3.681–3.864s respectively. Prior 32-file ON includes one slow 42.754s trial, median 32.299s. Version ordering/OS-cache differences mean these are observations for this local input/time.

Final ON file data allocation is constant across all 6/8 cycles; current payload is 2,097,152B/12,582,912B respectively. With 8× writes, total ON time is about 8.05× and p50 14.43/14.28ms. For this input, per-write publication/reclaim cost remains in place of progressively slower final saves due to accumulated repeated history. Two profiles differ in files/ranges/cycles, so this is not proof of complexity for all inputs. Bytes/hash/sequence in all 12 stopped accounting records match product seals; replaced payload is 0 in all 6 ON journals. Original HEAD/status/all refs/32 file hashes match before/after; owned basis released without blocking.

Cumulative managed allocation grew from prior ON 32,335,768B/259,586,160B to final 139,502,608B/1,135,364,408B. Capsule/block validation arrays and metadata work create managed allocation, but individual attribution was not measured separately. Time/file-allocation improvements are not memory savings. These values are cumulative managed allocations; retained heap/total RAM/instantaneous peak remain separately unmeasured.

Required `pwsh ./scripts/build.ps1`: locked restore/Release build 0 warnings/errors; 1,144 total with 1,134 passed/0 failed/10 explicit opt-in skips. Final source/runner hashes and build log/TRX preserved. `git diff --check`, document UTF-8, and Python syntax checks passed.

**scoped-complete-with-limitations**. Write-path optimization/current-state/recovery boundaries verified. Default OFF was initially retained due to ON/OFF multiples; user then judged total 22.389s for 1,536 writes and about 14.6ms average per write acceptable during development and explicitly selected default ON for new tasks. Existing tasks retain saved OFF/ON. This judgment does not mean Unity save/import time was measured.

<a id="새-작업-기본-on"></a>
## Default ON for new tasks

Subsequent requested default applies only to new product journals. Existing empty/nonempty SJR2 OFF and SJL4 ON retain saved selection regardless of initial defaults in reconnect inputs. Omitted nullable compact options retain current selection; only explicit false selects OFF. CLI description/request schema/capabilities/agent entry instructions updated together. Development Library cache and fixed SJR2 write/kernel probes retain separate explicit-OFF contracts.

`JournalDefaultTests` verifies repeated saves/current payload/reconnect seal for new default ON, existing empty/nonempty OFF, 7 initial-intent/identity/objects/root-flush/publication interruption boundaries, preservation of different owner/unknown object/corrupt root, omitted compact options for ON, explicit OFF, and subsequent omission/reconnect. Also corrected ON→ON compact reopening the already owned current writer guard. Candidate export compares preservation using composite source seal; Windows discard tests verify sealed inventory/partial reclaim receipts for each OFF/ON.

Actual WinFsp/fake immutable basis verification on a new journal without separately enabling default ON passed 1/1: 8 files/4 writes, 2-context unlinked reads, last Close, normal unmount/reconnect. `native/accounting.json` matches product composite seal bytes/hash/sequence; current payload 65,536B/replaced payload 0. Creation-interruption tests are explicit file-level observer boundaries, not OS power or actual process termination during creation qualification.

After default-ON changes, required `pwsh ./scripts/build.ps1` locked restore/full Release build had 0 warnings/errors; 1,165 total with 1,155 passed/0 failed/10 explicit opt-in skips. Initial OFF single-file hash/frame/reclaim-receipt assumptions were fixed as representation-specific tests or supplemented with ON composite seals/format-specific receipts, then full regression passed. Final `build.log`/`build.trx`, current source snapshots/hashes, native source/runtime hashes/accounting preserved in separate evidence folders. Existing performance figures were not remeasured.

<a id="근거와-한계"></a>
## Evidence and limitations

Subsequent default-ON verification is separately preserved in `artifacts/latest-default-on-20261006/`. Above 8/32-file time/allocation figures are frozen-runner measurements of the same SJL4 write path before default changes. Default changes do not cause repetition of full performance measurements/existing product A/B.

Final evidence in `artifacts/latest-capsule-indexed-write-20261006/` includes input, runner hashes, source before/after, result/cycle samples, basis release, stopped accounting/seal comparisons, build log/TRX, and native result. SJL2/SJL3 preserved in `artifacts/latest-write-path-20261006/`; SJL4 with single flush only in `artifacts/latest-capsule-write-20261006/`. Historical formats are read only by their frozen runners; no fallback reader added to current product.

Actual WinFsp/fake immutable basis verification of 8 files/4 writes, 2-context unlinked reads/last Close/normal unmount/reconnect passed 1/1 for final candidate. Native stopped accounting/product composite seal bytes/hash/sequence match; current payload 65,536B/replaced payload 0. Original Unity Editor/service/ACL/installation/closed A/B unchanged. Retains new-task default ON/existing saved selection, separate selection/state/candidate/OFF/restore/seal/source/peer/target guards. Original HEAD/status/all refs/32 file hashes compared before/after; only the new basis's owned ref released.

Input is NTFS Runtime range writes of actual committed PNGs. Unity Editor/import/game verification, VSS, large projects/OS power/full RAM improvement are NotRun or unknown; existing qualification not inherited. Atomic publication/owned-hash verification/reclaim and partial private-block rewriting costs remain. Reduced preserved file data allocation is not equivalent to reduced actual device writes.
