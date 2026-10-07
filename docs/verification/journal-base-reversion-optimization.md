<a id="다수-파일-기준본-복귀-시간-최적화-검증"></a>
# Multi-file basis reversion time optimization verification

[한국어](../ko/verification/journal-base-reversion-optimization.md)

2026-10-06 Windows/NTFS. Compared baseline implementation `25f4fa1` with a subsequent candidate on the same branch `codex/journal-base-reversion-optimization`. Follows the [fixed scope](../plans/journal-base-reversion-optimization.md) and [current contract](../journal-base-reversion.md). Previous [actual A/B and 7-boundary records](journal-base-reversion.md) qualify previous binaries and are not inherited as Unity/WinFsp execution for this candidate. Default OFF is retained.

Evidence is preserved in ignored `artifacts/journal-base-reversion-optimization-20261006/`. `before-runner/` and `before-binaries.json` identify prior binaries; `after-binaries.json` identifies measured optimized binaries. Preserves input/PNG lists, `result.json` for each of 18 samples, `before.trx`/`after.trx`, `analyze.ps1`/`summary.json`, and original hash/ref checks.

<a id="입력과-시간-경계"></a>
## Input and timing boundaries

Fixed `splinter-unity` commit `b09fa58e9d40a560d4fe1af7a213f8ac23e8e3b1`, 32 actual Medieval PNGs with different blobs, totaling **47,854,812 bytes**, 1,155,235–1,862,386 bytes per file. Distinguishes the earlier finding of a 2.8GB whole basis project from the actual 47.85MB assets changed. Synthetic large files were not substituted.

Stores interleaved 128KiB ranges starting at 64KiB with 256KiB spacing. Repeats B→C→base for each range over multiple cycles and adds final writes. Mixed input combines full reversion/half reversion/remaining changes by file. In `small-no-reversion`, all final ranges have different bytes. Payload write counts include final writes and exclude separate file metadata writes.

| Input | Files × ranges × cycles | Payload writes |
| --- | --- | ---: |
| small-mixed | 8×2×6 | 304 |
| many-mixed | 32×3×8 | 2,400 |
| small-no-reversion | 8×2×6 | 304 |

Before/after OFF/ON each ran 3 times, ordered OFF/ON→ON/OFF→OFF/ON. Used independent stopped SJR2 journals on actual NTFS, fixed Git provider/ref/lease, and a new reader for each compaction. Writes include durable flush and metadata/change observation. Compaction includes reader open/compaction/reader close; replay includes new reader open/journal replay/usage/seal observation/close. Initial fixture range preparation, whole-file content verification, and final `CompleteCompaction` are excluded from those timings. Unity Editor/import, live WinFsp load, original Library, and new A/B were not executed.

OS cache was not cleared; cold/warm could not be separated. Some prior-binary measurement intervals overlapped builds/selected regressions. Large variations in before/after write p95 and total time are recorded; write speed changes are not claimed as this optimization's effect. Compaction time, decoding amount, and preservation semantics below are the primary evidence.

<a id="결과"></a>
## Results

Medians of 3 samples compare the same ON feature before/after. Final payload selection, basis-comparison bytes, removed bytes, and final NTFS allocation were identical before/after.

| Input | Previous→optimized compaction | Compaction reduction | Previous→optimized Git decoded bytes | Previous→optimized cumulative managed allocation |
| --- | ---: | ---: | ---: | ---: |
| small-mixed | 13,816.60→2,836.54ms | 79.47% | 30,402,066→13,528,767 | 67,391,696→13,811,264 |
| many-mixed | 82,294.24→8,073.58ms | 90.19% | 222,165,084→70,923,484 | 402,936,352→56,304,880 |
| small-no-reversion | 21,300.23→2,730.16ms | 87.18% | 24,960,382→13,528,767 | 100,284,464→13,818,088 |

Medians of `base-range-compare` itself were 12,463.86→1,377.03ms, 79,215.46→5,353.02ms, and 18,794.57→1,308.28ms respectively. Git decoding is the reader's logical decoding counter, not OS physical I/O. Cumulative managed allocation is a `GC.GetTotalAllocatedBytes` difference, not resident RAM/heap/system peak. Managed-allocation reductions comparing only ON before/after are 79.51%, 86.03%, and 86.22% respectively.

| Input | Optimized OFF/ON final NTFS journal allocation | ON savings | Previous→optimized ON write+compaction+replay median |
| --- | ---: | ---: | ---: |
| small-mixed | 2,273,280 / 1,093,632 bytes | 51.89% | 15,745.46→4,878.68ms |
| many-mixed | 13,918,208 / 7,426,048 bytes | 46.65% | 89,771.57→15,535.11ms |
| small-no-reversion | 2,273,280 / 2,273,280 bytes | 0% | 25,786.66→4,647.98ms |

Total measured time is the median of each sample's write+compaction+replay sum, not a sum of the individual stage medians above. Total optimized `many-mixed` sample time varied over OFF **10,582.43–32,547.17ms** and ON **14,943.59–32,538.56ms**. Its write stage was OFF 6,656.52–28,768.00ms and ON 5,887.39–23,312.00ms. Apart from reduced optimized ON compaction at 7,851.7–8,485.2ms, the cause of write/total variation was not established.

Default-ON conditions are not met. After optimization, small mixed input totals were OFF 3,409.53ms / ON 4,878.68ms; no-reversion input OFF 3,257.44ms / ON 4,647.98ms, about 43% regression. The latter also saves no space. Variation in the whole 32-file comparison is not used to justify a favorable default. The earlier compressed repeated input's 0% allocation improvement is also not converted to a new pass. **Default OFF retained**; this result reduces explicit-ON cost.

The structure does not add a fixed 49% as workload grows. Previously, interleaved private extents reproved the same Git blob, accumulating up to 1 second of waiting on each transition discarding a partial read. Now it compares in BasePath/logical order, restores candidate physical order, and first closes the exact owned stdout to cancel waiting on unread suffixes. Cost depends on journal history scanning, current compared private bytes, full basis-blob proof/range redecoding, and range sorting. These 3 samples do not establish a proportional formula for other inputs differing in changed-file count, live ranges, or blob sizes.

<a id="보존과-자격"></a>
## Preservation and qualification

All 36 samples satisfied identical expectations for full-file streaming SHA-256, entries/IDs, metadata, change lists/sequences. New automatic tests verify interleaved/reversed ranges, rename comparison/candidate replay order, preservation of existing ACK bytes/journal/seal when comparison fails in another file, and transitions/closure of 6 partial readers. Existing corrupt blob/chunk, packed object, ref/lease protection, 30-second no-progress/progressing reads, and recovery from 7 synthetic actual child terminations remain. After 31/31 selected tests passed, failure-without-publication tests were added. Final `pwsh scripts/build.ps1` locked restore/full build had 0 warnings/errors; full tests **1,088 total: 1,079 passed / 0 failed / 9 selected tests skipped**. `build-final.log`/`full-final.trx` preserved; final Runtime/Git DLL hashes match measured optimized binaries.

Original SHA-256 of the 32 PNGs used, HEAD/clean status, and **92 existing refs** remain unchanged. Only basis owner `jr-opt-20261006` ref was released after the same receipt/lease checks; the difference in `ownership-after.json` is empty. Original/Library, Editors, service/ACL/installation, non-owned refs, and Git GC were unchanged. General reader optimization applies to the Git read path; basis reversion remains explicit ON.

Whole-workspace physical peak, shared-storage attribution, provider/Editor RAM, native codec RAM, and OS physical I/O are unknown. New native WinFsp interruption, Unity save/import/Play/Test/build, other providers/OS/full failure matrices are NotRun. Prior final A/B is not repeated or inherited by this candidate.

Subsequent default decision: OFF in this record was the historical candidate's choice. Current SJL4 write path and default ON for new tasks follow the [current contract](../latest-only-journal.md) and [subsequent verification](../verification/latest-journal-write-optimization.md). Historical input, figures, and unqualified boundaries remain preserved.
