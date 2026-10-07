<a id="동일-private-쓰기의-저장량과-복구"></a>
# Storage and recovery of duplicate private writes

[한국어](../ko/verification/duplicate-private-writes.md)

Execution date: 2026-10-02, Windows x64/C: NTFS. This record covers payload reuse when request bytes fully covered by the existing private journal are identical. It uses `metadata` frames and individual durable flushes; Core ports, CLI/schema, and journal version remained unchanged. Ranges referring only to the original, holes, extensions, and different bytes continued writing existing-style payloads at that time. Subsequent prefix/suffix reuse for partial changes follows [separate verification](resource-efficiency-followup.md). See the [current contract](../ntfs-workspaces.md#repeated-writes-to-the-same-private-range) and [measurement meanings](../source-verification.md#live-write-costs).

<a id="동일-입력의-실제-ntfs-할당"></a>
## Actual NTFS allocation for the same input

Runtime binaries from baseline `93080be` and the modified Runtime were fixed. Each wrote the same 64KiB range in one file 32 times in a new owned directory. Request content was fixed, with a synthetic immutable tree that fails on snapshot-payload reads. Journal hash differences due to IDs/times are not treated as content equivalence; current-file/replayed content, metadata, and sequence were compared. Compare [before](../../artifacts/storage-foundation/duplicate-write-20261001-235827-526/before.json) and [after](../../artifacts/storage-foundation/duplicate-write-20261001-235827-526/after.json).

| Observation | Existing Runtime | Modified Runtime |
| --- | ---: | ---: |
| Cumulative requested bytes | 2,097,152 | 2,097,152 |
| Recorded payload B | 2,097,152 | 65,536 |
| Logical journal B | 2,110,307 | 78,784 |
| Actual NTFS journal-file allocation B | 2,113,536 | 81,920 |
| Durable sequence / commit count | 33 / 33 | 33 / 33 |
| Durable flush count | 34 | 34 |
| Additional journal-read B for comparison | 0 | 2,031,616 |

File allocation for this repeated input **decreased by 2,031,616B, about 96.1%**. Requested bytes did not disappear: the first acknowledged payload was reused, with 31 new metadata commits retained. Counts including the final explicit flush are identical. Both runs' current-content hash is `4b640d85ab3ba30fd02c9fc9db4a8928f416322ad27022ea58a65aaee68a4df2`; snapshot reads, initial payload copying, and read cache are 0. File allocation is `FILE_STANDARD_INFO.AllocationSize`; it excludes directory/MFT, VSS, Git, and whole-workspace peak.

<a id="작은-실제-winfsp-client"></a>
## Small actual WinFsp client

The same input ran through 32 `FileStream.Write`/`Flush(true)` calls on a [local mount using the modified Runtime](../../artifacts/storage-foundation/duplicate-write-20261001-235827-526/native-after.json). Client reads and replayed content after unmount, metadata, and sequence matched. Sequence was 33, payload 65,536B, journal 78,784B, and file allocation 81,920B; owned handles/retained contexts were 0, normal mount absence and 0 callback errors were confirmed. Including the client's explicit flushes, the total was 66 flushes. It used only existing WinFsp under ordinary user permissions (`callerElevated=false`).

This is a **bounded write run with actual WinFsp/a fake immutable basis**. VSS, product workspace CLI/Git, Unity, VIVOBOOK, mmap, forced termination, power/reboot, and physical-space failure were not run. Previous Unity/Library results do not qualify this new Runtime. The proportion of identical bytes and comparison-read costs in actual projects remain unmeasured; the 96.1% above is not a Library or whole-product saving rate.

<a id="자동-회귀와-완료-기준"></a>
## Automated regression and completion criteria

Four of 8 `JournalDuplicateWriteTests` failed on existing code. All 8 modified tests passed. They check matching across multiple private extents, partial-range reuse after reconnect, metadata, path sequence, flush, base/hole/extension/byte mismatch, allowing identical writes within a small budget, and rejecting actual new payload. Exception injection after metadata header/flush checks existing-payload preservation and tail/complete-frame recovery respectively. This is not actual process/power termination. [Before-fix TRX](../../artifacts/storage-foundation/duplicate-write-20261001-235827-526/before-tests.trx) and [after-fix TRX](../../artifacts/storage-foundation/duplicate-write-20261001-235827-526/after-tests.trx) are preserved.

Eight cache-compaction tests in the first full regression failed because their input assumed that 32 identical payload writes create obsolete records ([TRX](../../artifacts/storage-foundation/duplicate-write-20261001-235827-526/automated-first.trx), [diagnostics](../../artifacts/storage-foundation/duplicate-write-20261001-235827-526/first-run-failure.json)). Only the fixture's first byte was changed on each write to create actual obsolete payload, retaining final expected content 42, 8 interruption boundaries, same-request recovery, range/budget, and preservation conditions.

The final required `pwsh ./scripts/build.ps1` run completed locked restore, Release build with 0 warnings/errors, and automated tests with **764 passed, 0 failed, and 1 test excluded at that time skipped out of 765**. The [build log](../../artifacts/storage-foundation/duplicate-write-20261001-235827-526/build.log), [final TRX](../../artifacts/storage-foundation/duplicate-write-20261001-235827-526/automated.trx), and [current source/binary hashes and assessment](../../artifacts/storage-foundation/duplicate-write-20261001-235827-526/result.json) are preserved. Current duplicate-write, observer, compaction/resumption, and cache-compaction regressions also passed.

This unit is **scoped-complete-with-limitations**. Completion requires the storage, individual flush, content/metadata, budget, recovery, and existing automated regressions above to pass. Overall SF-02 remains Partial; large/general Unity total savings remain unknown.

The [separate small Unity warm comparison](unity-duplicate-private-writes.md) checks identical editing/resumption, result preservation/release through actual product CLI/VSS/WinFsp/Editor. Its journal-allocation difference immediately before shutdown is small; it inherits neither this synthetic unit's 96.1% nor a general Unity/large-project saving rate.
