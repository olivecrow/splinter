<a id="저장소-실패-원인-재현과-보호-검증"></a>
# Storage-failure reproduction and protection verification

[한국어](../ko/verification/storage-recovery.md)

Execution date: 2026-09-26, Windows x64/NTFS. [Environment record](../../artifacts/storage-foundation/recovery-20260926/environment.json): OS 10.0.26200.0, volsnap 10.0.26100.8875, vssapi 10.0.26100.9278. [Previous day's run](storage-foundation.md) retains `FailedPreservation`. This scope fixes control-JSON publication failure and reproduces VSS cascading deletion in isolation/verifies deletion protection. Unity, large PV comparison, journal compression, and flush-policy changes were not run.

<a id="제어-파일-접근-거부"></a>
## Control-file access denial

Actual `WindowsProcessWorkspaceProvider.WriteAsync` reproduced the same `UnauthorizedAccessException`, `0x80070005`, and remaining complete temporary JSON as existing B's failure. `File.Move(temporary, path, true)` failed even with a normal reader opening the destination using `FileShare.ReadWrite | FileShare.Delete`. Compare [before fix](../../artifacts/storage-foundation/recovery-20260926/ipc-before.json), [replacement API comparison](../../artifacts/storage-foundation/recovery-20260926/ipc-replace.json), and [modified product function](../../artifacts/storage-foundation/recovery-20260926/ipc-after.json).

| Condition | Existing Move(overwrite) | Modified Replace |
| --- | --- | --- |
| No reader | Success | Success |
| Reader permits delete sharing | Access denial 5 | Success; existing reader sees previous JSON/new reader sees new JSON |
| Reader forbids delete sharing | Access denial 5 | Sharing violation 32; previous JSON/complete temporary file preserved |
| Read-only destination | Access denial 5 | Access denial 5; attributes/previous content preserved |
| Only reader handle remains after previous-file deletion | Success | Success |

Runtime `AtomicFilePublication` publishes complete, flushed temporary files in the same directory using `File.Replace` for existing files or non-overwriting `File.Move` for initial publication. Control IPC and workspace/baseline storage use it; VSS PowerShell receipts received the same behavior. No delete-first/copy-to-visible-file bypass, access-attribute changes, or unlimited retries were added. Failed files remain for recovery. Provider failure records add HRESULT, stack, publication path, and temporary path. Journal acknowledged-write/flush/seal contracts are unchanged.

Automated regression checks **100 publications** with open normal readers, preservation/retry after sharing violation, read-only files, actual WorkspaceStore saves, and actual PowerShell `Save-Json`. PowerShell `$null`→empty-string binding discovered in the first test was corrected with `NullString.Value`. The failed [first build](../../artifacts/storage-foundation/recovery-20260926/build-01.log) is also preserved.

This reproduced/fixed conflict between product polling readers and publication method. The handle owner at the previous day's error was not recorded, so **this does not identify the process involved in that single historical incident**. Actual prolonged Unity/WinFsp concurrent execution was not repeated here.

<a id="vss-연쇄-삭제"></a>
## VSS cascading deletion

Only synthetic files were created on `V:` in a new dynamic 16GiB NTFS VHDX. All volumes' VSS lists were confirmed empty initially; before each creation/deletion, any snapshot outside the test-volume GUID/recorded owned IDs caused a stop. No writes were made to C:'s 64GiB limit or original Unity/Git. This volume is for reproduction, not a product requirement to relocate originals or prepare another drive.

| Input/deletion route | Newest first | Oldest first | Middle first |
| --- | --- | --- | --- |
| 4 points × 4MiB, CIM | 0 other IDs lost | 0 | 0 |
| 12 points × 128MiB, CIM | 1 oldest other ID lost | 0 | 1 oldest other ID lost |
| 12 points × 128MiB, direct VSS API, `bForceDelete=false` | 1 oldest other ID lost | 0 | 1 oldest other ID lost |

At each point the same path was rewritten with new random bytes. Remaining snapshot files were directly hashed after deletion and compared with each point's values. Evidence: [small-order tests](../../artifacts/storage-foundation/recovery-20260926/matrix-result.json), [cumulative changes/CIM](../../artifacts/storage-foundation/recovery-20260926/churn-result.json), [non-forced direct API](../../artifacts/storage-foundation/recovery-20260926/nativefalse-result.json). CIM middle-order test stopped before deletion due to a harness index-calculation error, then resumed with the same recorded 12 IDs. The [error record](../../artifacts/storage-foundation/recovery-20260926/churn-harness-error.json) remains.

All four losses of other IDs recorded `volsnap 95`. Observed maximum CIM-test VSS UsedSpace was 6,701,056B, far below its 4GiB limit. This is VSS-reported diff-area usage, not total synthetic-file physical costs or hidden deferred-reclamation measurement. Reaching the capacity limit was not necessary in this reproduction.

Direct API also returned `S_OK`, `deleted=1`, `notDeleted=GUID_NULL` immediately before an older nontarget ID disappeared. [Experimental C++](../../artifacts/storage-foundation/recovery-20260926/delete-lab-snapshot.cpp) rejects deletion outside this test-volume GUID and is not included in the product. Microsoft's [DeleteSnapshots contract](https://learn.microsoft.com/en-us/windows/win32/api/vsbackup/nf-vsbackup-ivssbackupcomponents-deletesnapshots) explains forced deletion/return counts, but preservation of other IDs must be checked separately with before/after lists, as these measurements show. Replacing WMI with direct API or merely disabling force was not a solution.

The established defect is the **product assumption that individual deletion of a later snapshot is safe independent-resource reclamation**. Windows provider behavior removing older copies while processing copies marked for later deletion was reproduced on an isolated volume under the same OS. Exact driver thresholds/full triggering conditions are unconfirmed. The 4-point test's success is not a safe upper bound.

<a id="실제-보호-함수와-남은-제품-제약"></a>
## Actual protection function and remaining product constraints

Actual product helper `Invoke-OwnedSnapshotRemoval`/`Save-Json` were called unchanged for 12 points × 128MiB. Two newest/middle deletion requests returned `SnapshotReleaseIsolationRequired`, `deletionAttempted=false`; all 12 IDs and each point's file hashes remained before/after. Subsequent 12 oldest-first reclamations completed with 0 nontarget losses, 0 surviving-file hash mismatches, 0 volsnap events. [Actual protection test](../../artifacts/storage-foundation/recovery-20260926/guard-result.json) assessment is **PassedDefinedLive**. All sentinels are owned synthetic snapshots created by this test; it did not run concurrently with non-owned snapshots from ordinary backup software.

Protection avoids deleting later snapshots while older/same-time/unknown-time snapshots exist. If an older task remains active, the snapshot of a task finishing later may remain. Existing snapshots/other tasks are not deleted instead to bypass this restriction. `release` must not claim successful snapshot reclamation either.

This does not make development impossible for retaining original location, no full copying, range storage, or verification of changed files only. However, **arbitrary completion order and immediate physical reclamation cannot be guaranteed as the same operation**. Follow-up storage design must separate independent-result preservation from snapshot deletability and observe space retained because of older active tasks/non-owned snapshots. After defining deferred-reclamation states/retry/candidate-specific storage costs, proceed with SF-02 compression/SF-03 Editor-candidate connections. Automatic deferred-reclamation queues/shared-basis lifecycle are outside this implementation.

<a id="후보와-마감"></a>
## Candidate and closeout

Baseline is `development` `30ce3075bb377e968931a6bc53dde77c4065673f`; candidate is uncommitted. The [candidate manifest](../../artifacts/storage-foundation/recovery-20260926/candidate-manifest.json) records 138 implementation inputs/build DLL hashes. The [final build](../../artifacts/storage-foundation/recovery-20260926/build-02.log) completed locked restore, Release build, and full automated tests with **231 passed, 0 failed, and 1 test excluded at that time skipped out of 232**; [TRX](../../artifacts/storage-foundation/recovery-20260926/candidate.trx) is preserved.

Test-resource closeout follows the [final receipt](../../artifacts/storage-foundation/recovery-20260926/final-closeout.json). Of 96 synthetic snapshots, including 4 cascading deletions during reproduction, 0 remain. All 90 surviving-file hash comparisons in the protection test matched. [Final VSS query](../../artifacts/storage-foundation/recovery-20260926/lab-probe.json): C:/V: UsedSpace/AllocatedSpace 0; C: limit 64GiB. [Test VHD detach](../../artifacts/storage-foundation/recovery-20260926/detached.json) completed; VHDX containing synthetic files/raw evidence remains. Original Unity fixture is clean/unchanged. Existing work changes remained; no commit/push occurred. Previous `FailedPreservation`, SF-02 Partial, SF-03–06 NotRun, and NR/SW support boundaries are not promoted by this reproduction.
