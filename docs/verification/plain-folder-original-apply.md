<a id="일반-폴더-원본-반영의-한정-검증"></a>
# Bounded verification of plain-folder original apply

[한국어](../ko/verification/plain-folder-original-apply.md)

Scope is owned by the [fixed plan](../plans/plain-folder-original-apply.md); current commands/publication/metadata limitations by the [feature document](../plain-folder-original-apply.md). Program candidate is `development @ fa1efea`. Ordinary-user Core/fake/file/Windows tests are distinguished from actual product VSS/WinFsp execution below. Existing VCS-01–06/M0/Unity qualification is not inherited by this feature.

<a id="현재-판정"></a>
## Current judgment

**Bounded AP-01–06 scope closed as scoped-complete-with-limitations.** Verified automatic contract/actual-file tests and one small Editor-free product A/B set on Windows x64 NTFS. Actual apply is limited to ordinary file modification/creation/deletion, unrelated original-edit retention, B continued edits/conflict rejection, both independent results/reclaim. Directory/concurrent original-writer/interruption receipt/security boundaries retain prior automatic-file qualification, not expanded by this native set.

| Row | Current evidence and limits |
| --- | --- |
| AP-01 | Portable publisher fake, exact result/seal, read-only plan, request/response schemas, invalid CLI combinations rejected before home creation. Successful plan is not successful apply |
| AP-02 | Actual ordinary-user NTFS file modification/creation/deletion, directory creation/empty deletion, unrelated original writes/peer preservation. Rejects writes/deletion of affected files/original ancestor moves, rejects existing native writable mmap before publication, does not overwrite new files created in publication gaps |
| AP-03 | Preserves/rejects original conflicts/plan changes, post-checkpoint edits, ADS, unrelated children of deleted directories, unsupported/corrupt/unknown receipts. Rejects new task capture of unresolved publication files while existing peers continue. No whole-original rollback/recursive deletion |
| AP-04 | Finite injected interruption at stage creation/completion, receipts before/after retirement/publication, reclaim/completion; same-intent resumption. Unknown stage identity/altered prefix/new intent preserved/rejected. Runtime/privilege-service pending-apply reclaim blocking; prevents completion until actual absence of old files with deletion pending due to external readers |
| AP-05 | Passed (small Editor-free product A/B only). Same A resumption, 3 changed paths applied, 91 B writes during A release, B conflict rejected before original writes, independent results/final mount/delta/snapshot/owned-resource absence |
| AP-06 | Passed (fixed measurement/regression only). 8MiB/32MiB publisher stage/managed allocation and product result/journal/runner/process observation/regression/schema/guidance below. Whole physical peak/per-workspace VSS attribution/full CLI/Editor RAM savings unknown/unqualified |

Workspace snapshots in automatic tests above use synthetic backends. Fixture-setup copies are not product clone/copy-up evidence. Publisher uses actual Windows files/handles; sharing-race attempts use separate file opens/renames in the same test process. Observer-injected interruption does not qualify actual CLI/provider forced termination/reboot/power failure. Verifies owner/group, DACL protection, and ACE-content equivalence; audit SACL, special security labels, concurrent permission changes unqualified.

<a id="공간과-메모리"></a>
## Storage and memory

Historical AP-06 evidence is `artifacts/plain-folder-apply/cost-*.json`. A single changed-file stage allocated 8,388,608 / 33,554,432 bytes; 0 temporary files after normal publication. Publisher cumulative managed allocation was 524,488–525,312 bytes in those selected tests. Cost of fixed 64KiB streams/metadata/hash buffers, excluding Runtime state publication/VSS/Editor/whole-process working set/physical peak. No savings ratio combines independent-result basis/result payload with workspace journal.

Current `PublicationStreamsPayloadAndSamplesOnlyOneChangedFileStage` regression records the same measurements in xUnit test output. Confirmed in required-build `artifacts/test-results/Splinter.Tests_net10.0_x64.trx` output for each 8MiB/32MiB case; no separate `cost-<size>-<workspace ID>.json` accumulated per run. JSON above is historical preserved evidence, not current-test input.

Initial sequential EOF extension allocated 9,371,648 / 34,537,472 bytes respectively. Reserving known final stage length once removed additional 983,040 bytes from both samples. Initial failures/observations preserved in `artifacts/plain-folder-apply-preallocation-before.trx`/`.log`. No fixed savings ratio/SLA claimed for other sizes/volumes/large performance.

<a id="실제-제품-ab와-보호-경계"></a>
## Actual product A/B and protection boundaries

Fixed input/authorized scope followed the [execution plan](../plans/plain-folder-original-apply-validation.md). Created from 27-byte seed at `T:\plain-apply-20261002` and `.local\ap1002` with source/application `none`, snapshot basis, 16MiB journal budget. Preserved key/request digest/basis/snapshot/returned paths for A `42fdead74de65116b2669858` / B `78b313a0f90b0261a72742e9`. Initial payload duplication/read cache both 0.

After returning, user authorized execution including UAC. First UAC accidentally canceled, so helper did not start; after explicit reauthorization, package Update/new registration applied. Preserved 4 existing registrations and hashes of 1,352 protected-ledger files, adding only 1 registration. 14 installed files match reviewed manifest; service Running. All 15 ordinary CLI privilege observations have `callerElevated=false`.

First sandbox ensure returned `StateWriteIncomplete`, `publish-state`, publication unknown, Win32 5. Same-ID status observed `requested`/snapshot not captured; resumed same ensure from returned nextAction outside sandbox as ordinary user. Succeeded without new key/capture/state/permission edits; first response/temporary state preserved. This alone does not establish internal access-denial cause/native forced-termination recovery qualification.

A checkpointed modification of `data.txt`, creation of `added.txt`, deletion of `obsolete.txt`. Read-only plan retained state-file hash/original contents; verified 0 conflicts/3 paths. Applied with `ap-a-apply-20261002` and exact plan digest. Retained original `marker-after` and B `marker-before`/`b-result`/`obsolete.txt`. During about 11.1 seconds of A release, B child writer completed 91 writes; additional B edit after reclaim succeeded.

B checkpoint had 1 `data.txt` conflict. Explicit execution rejected with `OriginalApplyConflict`; original content/hash/mtime/attribute manifests identical before/after. Both tasks' independent result/baseline object lengths/hashes rechecked after release. Status/reclaim receipts confirmed actual mount/provider/delta/snapshot absence and owned handles/working set/private bytes 0. Verified reclaim isolation `missingOtherIds=[]` and preservation of 4 existing snapshot IDs. Remaining original publication files 0.

<a id="제품-비용과-증거"></a>
### Product cost and evidence

| Category | Observation and limits |
| --- | --- |
| Ordinary CLI elapsed time | A/B creation about 17.4/17.3s, A apply about 2.46s, A/B release about 11.1/10.9s. One sample including process start/checks/IPC, not SLA |
| Provider memory | Creation-complete working set A 50,774,016/B 50,843,648 bytes; private A 16,220,160/B 15,962,112 bytes. 0 after release. Fixed point observations, not peak/full CLI/Editor memory |
| Independent payload | A result 30/basis+result objects 44 bytes; B result 55/basis+result objects 60 bytes. Object total logical 104/file allocation 144 bytes. Tiny resident-file values are not general large-file allocation ratios |
| Private journal | Final seals A 2,388/B 112,932 bytes. Includes B repeated-write/metadata costs. B journal allocation 131,072 bytes after A release; both final deltas absent |
| Preserved records and runner | Final records allocation 136,400 bytes (includes objects above); same-home runner 7 files/1,110,360 bytes. Reads record/measurement overhead/payload separately without double-counting |
| Original publication temporary storage | 0 publication files after completion. Actual product-stage instantaneous allocation unobserved; distinguished from separate 8/32MiB publisher measurements above |
| Shared VSS | Corresponding T: volume allocation after capture 33,554,432 bytes; used/allocated 0 after last release. Volume observation from service receipts; per-task attribution/full physical peak unknown |

Preserved raw `01~16-*.json`, ordinary privilege/time `*-cost.json`, 24 valid responses in `response-schema-validation.json`, stage `storage-*.json`, `native-qualification.json`, independent results/owned reclaim receipts in `.local\ap1002\records`. Administrator Update distinguished by `service-update-plan.json`, `service-apply-outcome.json`, `ledger-before.json`, and configuration before/after records. No new native failure matrix/Unity/Plastic/large/ARM/OS tests.

Final program-candidate `pwsh ./scripts/build.ps1` locked restore/Release build had 0 warnings/errors; automatic tests **826 Passed / 0 Failed / 1 test excluded at the time Skipped (827 total)**. Evidence preserved in `artifacts/plain-folder-apply/automated.trx`, `artifacts/plain-folder-apply-final-build.log`, `artifacts/plain-folder-apply/candidate.json`. Includes 32 new apply-boundary cases/existing Git/Windows/contract regressions. Verified hashes of service-package 14 explicit files and matching product Core/Runtime/Files/Windows binaries; product code unchanged after native execution.
