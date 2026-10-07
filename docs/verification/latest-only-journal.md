<a id="현재본-저널-on의-검증과-비용"></a>
# Verification and cost of latest-only journal ON

[한국어](../ko/verification/latest-only-journal.md)

This document preserves measurements of the SJL2/SJT2 candidate before write-path optimization. Current format is SJL3/SJT3; [write-path verification](latest-journal-write-optimization.md) owns current qualification/comparison figures. Historical results below are not inherited as passes for the current format.

This is the 2026-10-06 user-request [scope](../plans/latest-only-journal.md)/[current contract](../latest-only-journal.md). Implemented SJL2/root v2, SJT2, and v3 compact intent after stopped compaction optimization in `f67f217`. Historical JR/JP/M0/PV/Unity qualification is not inherited as success for the current representation. Initial default OFF was retained at the time. Current defaults follow [subsequent write-path verification](latest-journal-write-optimization.md).

<a id="실제-파일-반복-변경"></a>
## Repeated changes to actual files

Used 32 committed PNGs (47,854,812B) from fixed actual Unity project `splinter-unity @ b09fa58e9d40a560d4fe1af7a213f8ac23e8e3b1`. Basis is Git raw blobs; original files were not edited. Input, runner hashes, original before/after HEAD/status/all refs/32 SHA-256 hashes, cycle samples, and owned Git basis release are in `artifacts/latest-only-journal-20261006/final2-*`.

- 8 files: 2 ranges of 128KiB per file, B→C interleaved 6 times, 192 writes total.
- 32 files: 3 ranges of 128KiB per file, B→C interleaved 8 times, 1,536 writes total.
- B/C use basis bytes xor 0x55/0xaa. Final content does not return to basis. ON starts after selection on an empty journal; no compact runs between writes.
- Each input OFF/ON ran 3 times, ordered OFF/ON→ON/OFF→OFF/ON. Used Runtime directly on actual NTFS. File seed/hash preparation, selection/initialization, cycle-allocation measurement, final fixed metadata, and whole-file hash verification were excluded from write time.
- Reconnect time covers Git lease/current observation/tree preparation, journal open/replay, and composite seal. Whole-file SHA-256 verification completed separately. OS cache was not cleared, so warm/cold is unknown. Build/test did not overlap benchmarks.

The following are medians of 3 runs each. Allocation is the **sum of NTFS file data allocation** for root/all current object files immediately after the last write cycle, before final fixed metadata. MB is 1,000,000B.

| Input | OFF final allocation | ON final allocation | Reduction | OFF writes | ON writes | OFF reconnect/seal | ON reconnect/seal |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 8 files / 192 writes | 26.018MB | 2.131MB | 91.81% | 0.503s | 4.040s | 1.040s | 1.153s |
| 32 files / 1,536 writes | 203.162MB | 12.715MB | 93.74% | 3.998s | 33.351s | 1.358s | 1.347s |

ON remained constant across all cycles/repetitions at **2,130,600B** for 8 files and **12,714,672B** for 32 files. OFF grew from first-cycle medians 4,390,912B→26,017,792B and 26,148,864B→203,161,600B. All 6 stopped SJL2 journals had obsolete payload 0; current raw payload was 2,097,152B/12,582,912B respectively. After final metadata, journal bytes were 2,105,474B/12,621,544B with 27/131 files. Final whole-file contents, entries, and per-path sequences were compared after reconnect.

Write-time ranges were 8 files OFF 0.491–0.602s/ON 3.992–4.133s, 32 files OFF 3.862–18.887s/ON 32.914–65.879s. Both modes varied greatly in the final 32-file repetition; cause unestablished. Favorable repetitions alone were not excluded. Median per-write P50 was 8 files OFF 2.440ms/ON 20.956ms, 32 files OFF 2.373ms/ON 21.002ms; P95 3.724/23.571ms and 3.604/24.663ms. Growing historical replay burden was removed, but the current representation increases write time through redo, metadata/root flush, file publication, and reclaim per mutation.

Cumulative managed-allocation medians were 8 files OFF 290,104B/ON 32,337,056B and 32 files OFF 2,274,328B/ON 259,621,496B. This is cumulative managed allocation over all writes, not peak heap/RAM. Byte cache/full source copy-up are 0; no ON managed-allocation improvement is claimed. MFT, whole instantaneous storage/memory, OS physical I/O, shared/VSS attribution are unknown.

Initial SJL1 prototype is separately preserved in `run/` and frozen `runner/`. SJL2, checked with repeated new input using the same files/order, changed JSON-base64 pending to raw bytes and reduced duplicate per-file checking/read handles; prototype ON medians decreased 7.750s→4.040s (8 files), 60.985s→33.351s (32 files). Environment variation/format differences prevent a controlled single-cause speedup claim. Prototype is not a current-reader supported format.

Initial SJL2 `final-run/` stopped at rejected metadata `File.Replace` for 32 files. Pending/temporary/journal preserved; `recovery.json` and `basis-release.json` verify redo/seal through writer reconnect with same ID/basis. Failed measurement excluded from the table. Current publication retries use only Windows 32/33/1175, the same flushed temporary file, and at most 1 second, matching provider IPC; access denial/corruption/different ownership are rejected. Separate reader-without-delete-sharing regression verified. Final `final2-run/basis-release.json` is released; original HEAD/status/all refs/32 hashes match before/after.

<a id="복구와-연결-경계"></a>
## Recovery and integration boundaries

| Row | Verified scope | Evidence / qualification |
| --- | --- | --- |
| LJ-01 | Retains only current payload after every ACK; 16 files/12 repetitions; raw/Brotli partial replacement/truncate; metadata/rename/whiteout; OFF/ON transition; candidate/restore ranges | `JournalLatestOnlyTests`/`JournalBaseReversionTests`; fake immutable basis / actual NTFS files |
| LJ-02 | Retains B on incomplete redo; completes C on complete redo; 11 observer boundaries; checksum/metadata corruption rejection; recovery of same source sequence on last unlinked close | `JournalLatestOnlyTests`; injected file boundaries, not OS power-loss qualification |
| LJ-03 | Terminates self-owned test child with exact nonce/PID/start after redo prefix/root publication/previous-object reclaim | `JournalLatestProcessTests`, 3 boundaries; actual local process/fake basis, not WinFsp kill |
| LJ-04 | Actual WinFsp 8 files/4 writes, unlinked-file read with two contexts, last-Close reclaim, normal unmount/reconnect | Final `native-final2/result.json`/same-name `.log`; installed WinFsp/fake immutable basis, not VSS/Git/Unity product A/B |
| LJ-05 | Current/obsolete accounting for 12 stopped journals; whole-object composite SHA/bytes/sequence compared to Runtime seal | `final2-accounting/`; 6 current journals obsolete 0, Brotli content restoration verified by Runtime |
| LJ-06 | Exact-inventory release/partial previous-generation reclaim; writer/wrong-seal/unknown rejection; projection protection aggregation; managed original move/range restoration; schema/CLI/saved ON | Related automatic regression and required `pwsh scripts/build.ps1`; final execution results linked to closure record |

New Unity import/Play/Test/build, original Editor apply, VSS/full product A/B, large, ARM/macOS/Linux, and OS power failures are NotRun. Fixture passes are not generalized to support. Final goal is retaining current state, but default-ON condition permitting no greater than 10% write+reconnect regression was not met. **Storage objective achieved, but default OFF retained.**

<a id="최종-마감"></a>
## Final closure

`pwsh scripts/build.ps1` locked restore/full Release build had 0 warnings/errors; full tests **1,128 total / 1,118 passed / 0 failed / 10 skipped**. `build-final2.log`/`build-final2.trx` are final evidence. 10 explicit external/child/native entrypoints skip in ordinary full execution; selected WinFsp entrypoint separately passed 1/1 in `native-final2.log`. `recheck-tests.log`, first rechecking only related boundaries, passed 148/148.

27 failures in prior `build-final.log`/`build-final-failed.trx` arose from capabilities fixed-schema values, existing error precedence for OFF projection rejection, and private-only seal verification connection for restore after basis-reader closure. Corrected each cause, added ON↔OFF projection protection/current-state restore/interruption/changed pending-temporary preservation regressions, and verified final full success. Failed executions are not added to final qualification.

Time/allocation figures use hash-fixed `final2-runner` binaries. Final full tests own subsequent projection rejection/private-only restore seal/corrupt temporary-prefix guard qualification, distinguished from frozen measurement. Original/peer/existing protection/independent results preserved. This request's implementation/fixed measurement closes within scope; no extra commit/push/default change/Unity/VSS/A/B expansion performed.
