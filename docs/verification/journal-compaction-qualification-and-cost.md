<a id="sf-02-unity-재개와-작업공간-공간정리-비용-검증"></a>
# SF-02 Unity resumption and workspace storage/compaction cost verification

[한국어](../ko/verification/journal-compaction-qualification-and-cost.md)

Executed 2026-09-26. Scope/judgment follows [QJ-01–04 plan](journal-compaction-qualification-and-cost.md). Used Windows x64, isolated 16GiB NTFS VHD, Unity `6000.3.22f1`, small Unity/1MiB synthetic fixtures through actual VSS/WinFsp/Git product paths. Owned IDs: Unity `7610f94472b806a477ac8b53`, synthetic `19e0131aff5bda4ffad7e911`. Neither result integrated into original branches. Existing [first SF-02 record](journal-compaction.md)/[common-storage FailedPreservation](storage-foundation.md) judgments retained.

| Gate | Judgment in this execution |
| --- | --- |
| QJ-01 Unity loading/file API | **Passed, limited cause attribution**: reproduced/fixed product empty-subdirectory Win32 enumeration defect. Compaction/resumption on same snapshot verified bridge readiness/script compilation without exception. Unity also deleted/recreated directory immediately before first exception, so no single-cause attribution. |
| QJ-02 Additional physical-storage ledger | **Partial**: preserved Unity pre-creation baseline, 222 complete 10-second samples, observed compaction maximum, subsequent work/release, and synthetic stage samples. Synthetic Git common pre-creation allocation not measured, initial increase unknown. Between-sample maxima/NTFS directory/MFT metadata also unknown. |
| QJ-03 Direct cost ledger | **Passed: defined single execution**. Preserved same compaction attempt's original replay/scan/candidate writes/flush/candidate replay/seal/publication/provider reconnect/previous-generation cleanup ticks, read/write bytes, flush counts, CLI elapsed. Per-write flush policy unchanged. |
| QJ-04 Resumption/result/reclaim | **Normal path after resumption Passed; first-load shutdown required operational intervention**. Post-fix verified new code 14/current validation/product `unity stop`/independent result/owned resources 0. Pre-fix first-load product stop timeout recorded as forced termination after owned PID verification. |

<a id="빈-디렉터리와-unity-로드"></a>
## Empty directories and Unity loading

First import before compaction remained bridge `application.ready=false` after `ProjectInfo.projectLoad=1041.348` seconds. Editor `DeleteUnusedAssemblies` threw `FileNotFoundException` for `Library/ScriptAssemblies`. [Original journal path history](../../artifacts/storage-foundation/qj-20260926/script-assemblies-journal.json) shows directory creation/deletion/recreation around error. At same short mount path empty directory existed and `Directory.GetFiles` returned 0, but Win32 `FindFirstFileW(<dir>\*)` returned `ERROR_FILE_NOT_FOUND(2)`. Ordinary NTFS empty comparison folder succeeded. See [pre-fix file API](../../artifacts/storage-foundation/qj-20260926/empty-enumeration.json).

Fixed `WinFspOverlayFileSystem.ReadDirectoryEntry`/single-name lookup to supply actual `.`/`..` information/marker order in subdirectories. No fake files recorded in journal. Reconnected same snapshot with corrected provider; both folders passed `FindFirstFileW` ([post-fix file API](../../artifacts/storage-foundation/qj-20260926/empty-enumeration-after.json)). Automatic callback tests verify empty subdirectories/markers/name lookup/unchanged journal sequence. Actual Win32 comparison/Unity resumption are separate regression evidence.

First-load `unity stop` was `EditorStopTimeout`. Verified PID `44924` start time/Unity executable/workspace path, forcibly terminated only that owned Editor. Product status child processes 0/mount handles 0 ([shutdown response](../../artifacts/storage-foundation/qj-20260926/unity-stop-before-fix.json), [owned shutdown receipt](../../artifacts/storage-foundation/qj-20260926/unity-owned-stop.json)). Post-compaction restart returned `SourceChanged` while starting owned Editor PID `43372`. Once files stable, `workspace status --verify-source` verified no source changes/current fingerprint; same Editor became `ready=true`. Restart log lacks same `FileNotFoundException`. See [restart state](../../artifacts/storage-foundation/qj-20260926/unity-status-restart-3.json)/[loaded code before edit](../../artifacts/storage-foundation/qj-20260926/unity-probe-before-edit.json) `Calculate(3)=13`. One successful resumption does not guarantee all deletion/recreation timings.

On same compacted snapshot changed `Assets/SplinterSnapshotProbe.cs` `value + 10`→`value + 11`. [Validation state](../../artifacts/storage-foundation/qj-20260926/unity-status-validated-1.json): `application.ready=true`, `sourceVerification=verified`, `validationCurrent=true`, `validation.success=true`, 0 errors. [Loaded code](../../artifacts/storage-foundation/qj-20260926/unity-probe-after-edit.json) records 14/new assembly MVID in same Editor PID. Pre-fix 13 not inferred solely from source text. Product [stop](../../artifacts/storage-foundation/qj-20260926/unity-stop-after-fix.json) succeeded; owned processes/memory 0.

<a id="원본을-제외한-추가-물리-공간"></a>
## Additional physical storage excluding the original

Read-only [measuring script](../source-verification.md) adds home control/delta/runner file allocation and original `.git` common increase from baseline. `Library` writes included in journal; mount logical view not added again. Original `Library` 0 fixture excluded. Unity `.git` common pre-creation baseline 6,075,552B. [Storage summary/all samples](../../artifacts/storage-foundation/qj-20260926/space-summary.json) connect logical sizes/actual NTFS allocation per category.

| Unity stage | Owned additional NTFS file allocation B | Journal file allocation B | Explanation |
| --- | ---: | ---: | --- |
| Immediately before creation | 0 | 0 | Original Git baseline held separately |
| New workspace ready | 794,320 | 4,096 | Includes control/runner/Git registration increase |
| After first import/provider unmount | 1,943,365,608 | 1,941,647,360 | Same generation immediately before compaction |
| Observed compaction maximum | **3,590,531,672** | 1,646,452,736(new) + 1,941,647,360(previous) | 10-second sample `unity-166`; two generations 3,588,100,096B/other 2,431,576B |
| Immediately after compaction/reconnect | 1,648,883,736 | 1,646,452,736 | 294,481,872B below preceding value |
| After resumption/validation/result collection | **2,031,857,744** | 2,030,043,136 | Continued Unity writes regrew beyond pre-compaction size |
| After release | 1,799,064 | 0 | Preserved control/runner/Git results; no owned mount/delta |

Before/after journal logical lengths 1,941,645,422B/1,646,450,291B; receipt actual allocation 1,941,647,360B/1,646,452,736B; same `durable sequence=298899` retained ([Unity compaction response](../../artifacts/storage-foundation/qj-20260926/unity-compact-2.json)). Pre-compaction live-provider sample 1,944,134,992B was 769,384B above after unmount. Comparison above matches unmounted sample immediately before compaction. All 222 10-second samples completed enumeration, but this is an **observed maximum**. Directory/MFT metadata/between-sample instantaneous peak/host allocation of dynamic VHD/OS/Unity shared caches excluded.

100 overwrites of same 4KiB range on synthetic mount retained original SHA-256; only task-view hash changed ([input](../../artifacts/storage-foundation/qj-20260926/source-exercise.json)). Journal allocation fell 454,656B→8,192B. However observed synthetic-workspace additional allocation after ready grew from 1,223,856B after edits to **1,478,984B immediately after compaction**. Reconnect runner grew 692,568B→1,393,328B, outweighing small journal savings. Synthetic `.git` common pre-creation baseline absent, initial increase unknown; table uses ready-time Git baseline. Small-fixture journal savings do not substitute for whole large-Unity-workspace savings.

Isolated `V:` VSS Used/Allocated: 0 snapshots 0/0; after two prepared 2,949,120/34,078,720B; after first import 27,754,496/101,187,584B; immediately after Unity compaction 27,836,416/101,187,584B; after last owned release 0/0 ([VSS preparation](../../artifacts/storage-foundation/qj-20260926/response-vss-ready.json), [first import](../../artifacts/storage-foundation/qj-20260926/response-vss-first-import.json), [closure](../../artifacts/storage-foundation/qj-20260926/response-vss-final.json)). Volume-shared time series, not per-task savings or attributed/added to additional allocation above. Other-volume `C:` VSS excluded from ledger.

<a id="단계별-compaction-비용"></a>
## Per-stage compaction cost

Unity attempt `f3907db09b2f4c9d940af9a27bd3a4e9`: `stopwatchFrequency=10,000,000` ticks/s, direct journal compaction `totalTicks=489,805,151`(48.981s), successful product CLI wall time 88.124s. Prior `SnapshotPrivilegeRequired`/Observe wait/first import excluded from 88.124s. Main direct stages below; all-stage ticks/read/write bytes/flush counts in [response](../../artifacts/storage-foundation/qj-20260926/unity-compact-2.json).

| Stage | Seconds | Read B | Written B | Flush count |
| --- | ---: | ---: | ---: | ---: |
| Original replay | 15.331 | 1,941,645,422 | 0 | 0 |
| Candidate scan | 2.808 | 131,438,077 | 0 | 0 |
| Candidate write | 4.927 | 1,661,816,220 | 1,646,450,291 | 0 |
| Candidate flush | 0.441 | 0 | 0 | 1 |
| Candidate replay | 9.946 | 1,646,450,291 | 0 | 0 |
| Candidate seal | 1.856 | 1,646,450,291 | 0 | 0 |
| Publication generation verification | 4.070 | 3,588,095,713 | 0 | 0 |
| Provider reconnect | 12.337 | 1,646,450,291 | 0 | 0 |
| Previous-generation cleanup | 7.802 | 0 | 0 | 0 |

Both intent publications also recorded one `Flush(true)` each. `readBytes` are journal bytes actually read per stage; repeated verification reads not summed as unique data. Post-first-import provider status `commitCount=298885`, `flushCount=298924`, cumulative `flushTicks=8524175572`(852.4s) observes current per-write flush ([first-import status](../../artifacts/storage-foundation/qj-20260926/unity-status-first-ready.json)). Not performance/durability verification of improved policy.

<a id="독립-결과와-회수"></a>
## Independent results and reclaim

Unity checkpoint collected only changed `Assets/SplinterSnapshotProbe.cs` into result commit `ccfa3a99a02c8b5e2991f1477d33e73451007ae4`. Final release manifest `stable-journal-v1` seal: sequence 341656/journal length 2,030,016,783B/SHA-256 `78ea5f32ed51bccf8dc8439a1367c4262fe2ad725f5d1f514ac323fba27c1e9b`. Synthetic result `fdc232a6856461bb277256fa95c2f4f7a1fdb5e9` retained as independent ref/sequence 105. Both original Git trees clean; HEADs retain existing `58f81b5...`/`2aa147d...` ([Unity checkpoint](../../artifacts/storage-foundation/qj-20260926/unity-checkpoint.json), [Unity release](../../artifacts/storage-foundation/qj-20260926/unity-release-2.json), [synthetic release](../../artifacts/storage-foundation/qj-20260926/source-release-2.json)).

Explicitly released older owned synthetic snapshot first, latest Unity snapshot second. Both product states `released`; owned Editors/children/mounts/deltas/VSS IDs 0; `V:` VSS Used/Allocated 0. Restricted administrator session handled only exact two IDs; after [closure receipt](../../artifacts/storage-foundation/qj-20260926/response-finish.json), isolated VHD `Attached=false`/`V:` absent. Not general non-owned-backup coexistence/arbitrary-order immediate reclaim evidence.

Full `pwsh ./scripts/build.ps1` locked restore/Release had 0 warnings/errors; automatic tests **265 total: 264 passed/0 failed/1 test excluded at the time skipped**. Cost-response `response.schema.json` contract verified automatically too. Actual mount/Unity regression verified by separate receipts above. Full SF-02 remains **Partial**, with flush policy/power/reboot/storage failures remaining. Small-Unity-fixture maximum additional storage during compaction greatly exceeded pre-compaction, and final additional storage after continued work also exceeded pre-compaction. Thus large Unity workspace capacity burden not judged resolved.
