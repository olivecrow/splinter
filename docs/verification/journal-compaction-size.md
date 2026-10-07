<a id="저널-정리의-크기-증가-회귀"></a>
# Journal-compaction size-growth regression

[한국어](../ko/verification/journal-compaction-size.md)

Execution date: 2026-10-01, Windows x64/C: NTFS. Scope covers metadata growth exceeding removed payload during offline `JournalDeltaStore` compaction. Only frame selection used by existing `workspace compact`/Library `compact-cache` changes. Input, response, journal format, durability, ownership, and interruption-recovery contracts remain. Overall SF-02 is Partial; this does not complete large Unity storage goals.

<a id="재현과-선택-규칙"></a>
## Reproduction and selection rules

- Scattered 1-byte overwrites in an 8KiB write made the list of small live ranges larger than the payload saved. Further edits to an already `write-compacted` frame also reproduced this. Existing code rejected an overlap budget set to twice the previous length because candidate length increased.
- Alternating 1-byte overwrites in two files prevented grouping obsolete writes; individual `skip-write` metadata growth exceeded the 1 byte removed.
- Keep the existing frame when splitting it into ranges is larger or equal. If an individual skip is larger, keep the existing frame but retain opportunities to group adjacent skips on the same path. Accept a group only when it does not exceed the sum of the smaller representations of the two frames. Prescan and actual candidate writing use the same rules.

Existing-frame payload is read from the already-owned journal. No basis-file reading/filling or full-file copy-up occurs. Selection avoids growing the entire journal even if some obsolete payload remains; obsolete=0 is not guaranteed. Candidate checksum, full replay-state hash, sequence, before/after seal checking, and preservation of the previous generation continue to apply.

<a id="같은-입력의-실제-파일-할당"></a>
## Actual file allocation for the same input

Runtime binaries from baseline commit `b526646` and modified binaries were fixed. A journal was written once on a synthetic immutable tree: 8KiB to each of 4 files, followed by 128 1-byte overwrites in two regions. **Byte-identical journals** were copied into separate new owned directories and compacted. Compare [existing-code response](../../artifacts/storage-foundation/compaction-size-20261001-232212-446/before-qualified.json) and [modified-code response](../../artifacts/storage-foundation/compaction-size-20261001-232212-446/after-qualified.json).

| Observation B | Immediately before compaction | After existing-code compaction | After modified-code compaction |
| --- | ---: | ---: | ---: |
| Logical journal length | 238,404 | 252,296 | 238,404 |
| NTFS journal-file allocation | 241,664 | 253,952 | 241,664 |
| Total allocation of two journal files after publication | Not applicable | 495,616 | 483,328 |

For this input, the fix **prevented 12,288B of file-allocation growth** caused by existing compaction. It did not reduce storage from immediately before compaction. Input journal SHA-256 in both runs is `fd6a49b371015dc89c0cc18dbfdabee89a79971cfeac7febe7fa4dbd4d3b4a7f`; durable sequence is 520. The four files' current-content hashes, changed paths, and sequence match; basis-payload reads were 0. Predicted overlap matched the actual sum of both journals' logical lengths. The previous journal and intent were reclaimed after verification.

Allocation is `FILE_STANDARD_INFO.AllocationSize`. It excludes directory/MFT, VSS, Git, Unity, OS cache, and whole-workspace instantaneous peak. The first sandbox run's `File.Replace` access-denial material remains in `before-journal`. The comparison table uses only completed ordinary-user runs in new owned `before-qualified-journal`/`after-qualified-journal`. Administrator installation, grant, VSS, WinFsp mount, Unity, and VIVOBOOK did not run.

<a id="자동-회귀와-완료-경계"></a>
## Automated regression and completion boundary

All three `JournalCompactionSizeTests` cases failed before and passed after the fix. They compare re-compaction of existing/already-compacted frames, overlap budget, predicted/actual length equality, file content/metadata, changed paths/sequence, and 0 basis reads. [Before-fix TRX](../../artifacts/storage-foundation/compaction-size-20261001-232212-446/before-tests.trx) and [after-fix TRX](../../artifacts/storage-foundation/compaction-size-20261001-232212-446/after-tests.trx) are preserved.

Required `pwsh ./scripts/build.ps1` completed locked restore/Release build with 0 warnings/errors; automated tests returned **756 passed, 0 failed, and 1 test excluded at that time skipped out of 757**. The [build log](../../artifacts/storage-foundation/compaction-size-20261001-232212-446/build.log), [same-run TRX](../../artifacts/storage-foundation/compaction-size-20261001-232212-446/automated.trx), and [assessment/current source hashes](../../artifacts/storage-foundation/compaction-size-20261001-232212-446/result.json) are preserved. Existing large-payload removal, same-path skip grouping, resumption at four publication boundaries, corrupt-candidate preservation, and workspace/cache conformance tests also passed. All 63 documents passed UTF-8 checks; all 222 local links in changed documents exist.

This unit is **scoped-complete-with-limitations**. Completion is limited to the synthetic immutable basis, current automated regressions, and actual local journal-file allocation comparison. Actual whole Unity/Library project savings and VSS/WinFsp/power/reboot qualifications are not inherited as passes for the new candidate.
