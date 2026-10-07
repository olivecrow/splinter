<a id="vcs-없는-프로젝트-기준본-검증"></a>
# VCS-free project-basis verification

[한국어](../ko/verification/plain-project-basis.md)

2026-10-05 record for the current candidate's [managed-original contract](../plain-project-basis.md)/[fixed scope](../plans/plain-project-basis.md). Historical VSS/Git/Plastic success does not qualify the new reader. File/Unity A/B and full regressions were checked; final NTFS restoration observations are distinguished below.

| ID | Current evidence | Status |
| --- | --- | --- |
| NP-01 | Three source/Library private-range fakes, explicit None/basis schema, lost capture response/same request/VCS rejection/corrupt task-digest rejection/task-reference protection | Automated Passed |
| NP-02 | Actual file/Unity prepare, reject dependent restore, final NTFS restoration, same-restore completed receipt | Passed; native OS-failure qualification NotRun |
| NP-03 | `project prepare/status/restore`, `project-snapshot`, exact basis/source/application/capabilities/owned-directory/SJR2/task receipts | Passed |
| NP-04 | Original private writes, A/B frozen reads/1-byte separation, full A publication, B continuation during A release, B discard or selected publication, final original preservation | File/Unity Passed |
| NP-05 | Exact original/A/B MCP identity for selected Unity source, resources/importer, scene creation/editing, current-state tests, actual 2.690-second test overlap | Editing/testing Passed; shared-server shutdown defect is a limitation |
| NP-06 | Exact task/common file allocation warm/after A reclamation, separate baseline/independent results, required full regression/actual response schema | Passed; total peak/OS costs unknown |

<a id="공통-복원-보호-회귀"></a>
## Common restore-protection regression

Current common restore protection is separately checked with local NTFS file tests in `ManagedDirectoryRestoreTests`. They preserve target and basis/journal for replacement with another file after new-file range flush, identical-byte replacement, interruption before creation receipt, interruption during move after full overwrite of retained-area file, and replacement after completion. They also check resumption after normal receipt, readonly-file moves/metadata, readonly-attribute preservation when retained bytes change is rejected, external replacement rejection during writes, and rejection of previous restore plans. `RecoveryContractTests` checks consistency of basis-protection error responses/recovery policy. Actual Unity/WinFsp restore/OS failures for this change are NotRun; previous-candidate execution below is not inherited as native passes for current protection changes.

Final `pwsh ./scripts/build.ps1` on 2026-10-05 completed locked restore/Release build with 0 warnings/errors; full 1,005 tests: 1,004 Passed/0 Failed/1 test excluded at that time NotRun. Includes 20 new restore/error-policy/directory-move/replay/Git no-progress-limit regressions. Final TRX remains at `artifacts/code-quality-fixes/full-final.trx`; document UTF-8/diff whitespace checks passed. These file/control-stream/existing full automated regressions do not qualify new Unity/VSS/large/ARM/OS support.

<a id="editor-없는-실제-파일-경계"></a>
## Actual editorless file boundary

`T:/np-native-project-20261005`, home `T:/np-native-home-20261005`, basis `0378ee9322d8ed7e5d6dd18b`, A `07db6ca107068b9ce01ef40f`, B `c866ae90adb0b5679ff72f20`. A fixed 3 MiB file/small text inputs isolated original private text/A/B 1-byte edits. Separate original changes remained after full A application. B wrote again after A reclamation, then B was discarded with the same intent and ordinary NTFS original restored. A byte 11/original text/absence of B's new file confirmed. Evidence: JSON files/`summary.json` in `artifacts/plain-project-basis-20261005/native-files/`.

Initial file plan was rejected before side effects by WinFsp stream-query native 87. Only routes verifying managed mount NamedStreams=false/basis ADS rejection were handled; ordinary NTFS stream guards remained. First A release conflicted with existing peer guard that actually deletes the common snapshot. After correcting the route to release only task-owned references, the same A/id/home was reclaimed. Common payload remained until final restore. Failure/retry evidence remains; no separate new A/B replaced it.

<a id="실제-unity-입력과-도구"></a>
## Actual Unity input and tools

Selected **51 files/157,794 logical bytes** from original fixture `b09fa58e9d40a560d4fe1af7a213f8ac23e8e3b1` were prepared once: SampleScene, Settings, Alpha scripts, Packages/ProjectSettings; full Medieval pack/original Library excluded. This preparation copy is distinct from product-basis copying. Existing C: fixture `.git`/Editor PID 37600/start `639267352637577053` remained. Shared HTTP 8080 server stopped/recovered once due to external shutdown defect below, so this is not uninterrupted-coexistence qualification. C: original management data was not removed.

Input `T:/vcs-none-project-20261005`, home `T:/np-unity-home-20261005`, Unity `6000.3.22f1`, existing Pipeline `0.7.0-exp.1`, fixed Coplay candidate-02. First batch import returned 0/no CS errors. Generated source/Library **1,841,478,483 logical / 1,875,469,680 allocated bytes** moved within the same volume; prepare initial payload copying 0. Preparation verification/hash reads/about 2 minutes of preparation costs exist.

| Target | basis/task | Actual Editor identity | HTTP MCP instance |
| --- | --- | --- | --- |
| Verification original | basis `806908d0bfb0cd58e6f485df` | PID 74044/start `639267765099184522` | `vcs-none-project-20261005@664943a407af9f72` |
| A | `cf9403e4f2d9d43fea984308`, digest `45f9657780dd2c6ca094464b` | PID 54492/start `639267764914133475` | `cf9403e4f2d9d43fea984308@62d76c35f33e2c74` |
| B | `f951c25c9f4b030b2f614a3a`, digest `5e5d7bc206d7b04fc6a4c746` | PID 16956/start `639267765707553341` | `f951c25c9f4b030b2f614a3a@c8a87729089c2b55` |

MCP results in `artifacts/plain-project-basis-20261005/*identity*` retain exact project/PID/start, with large start values stored as strings. A was also checked through current Codex default MCP. HTTP per-call instances avoided competition over A/B session default targets.

After original private writes to `Assets/OriginalNote.txt`/`Library/NPOriginalMarker.txt`, absence was checked in A/B. A imported/read red/Point 8×8 PNG, B blue/Bilinear. Each created a new Scene with Camera/Light and changed/saved cube positions `(1,3,0)`/`(2,3,0)`. Common path `Assets/NPState.txt` retained A 101/B 202. Both code-job start/completion intervals also overlapped. Raw job results `a-edit-result.json`/`b-edit-result.json` remain.

Four selected EditMode tests in each Editor check current file value/exact PID/path/absence of peer-original private files, saved scene position, own texture/importer, and current state during a 3-second coroutine. Both A/B: 4 Passed/0 Failed. First concurrent jobs' actual coroutines ran serially; second selected pair was also serial. In the third bounded pair, B was submitted after observing actual A test start; **2,690ms of actual coroutine overlap** confirmed. All three attempts' jobs/test results/`actual-test-overlap.json` remain. Submission-interval overlap alone does not establish execution overlap.

First read-only Pipeline eval timed out after 5 main-thread seconds. A reload port-change 401 remains. Existing CLI exact-path `editor_focus` advanced initial connection callbacks; MCP identity rechecked. Ports were not fixed; timed-out writes were not automatically repeated. Approved helper only called verification Editor's existing Bridge.StartAsync. Helper/test assemblies were removed after confirming owned paths/hashes, then compilation completion/result collection checked; they are absent from independent results. Unity version/packages/global settings unchanged. Final `codex mcp list --json`/`codex mcp get unity_mcp_coplay --json` exited 0 with valid HTTP 8080 settings.

<a id="원본-반영과-소유-회수"></a>
## Original application and owned reclamation

A checkpoint independent result: 173,547 logical bytes/58 payload objects. After normal shutdown of exact verification-original Editor PID/start, full A plan **8 changes/0 conflicts** published with fixed intent/digest. A release confirmed active process/RAM/open handles 0, absent mount/delta, released task reference. Common basis data remained for B.

After A reclamation, B MCP confirmed value 202/absence of A changes-original private files and additionally edited/saved B cube `(2,5,0)`. B checkpoint: 173,561 logical bytes/58 objects. B full original plan returned **2 conflicts** for A-published `NPState.txt`/meta and was not executed. Only `Assets/NP_B` selected plan **6 changes/0 conflicts** published with same selection/intent/digest. Original value 101/separate original text/Library marker/A files remained. B release confirmed owned resources 0/absent mount/delta. Actual Unity bundle preserved two independent results; explicit discard qualified in actual-file bundle above.

Final original restoration processed **31,362 files** with the same basis/id/home fixed plan/seal/cursor, taking **about 783.9 seconds (13 minutes 4 seconds)**. Verification/plan creation, per-file durable cursor publication, moves, and range restoration incur costs; immediate restoration is not claimed. `unity-final-proof.json`: ordinary NTFS/reparse=false, matching independent-result hashes of 12 applied files, original private text/Library marker/value 101, owned Editor/provider 0, absent task mount/delta/common base/delta, dependents 0. Temporary helper/test assemblies absent from original. Same restore re-request returns completed receipt in `unity-restore-repeat.json`. State/manifest/restore plan/independent results intentionally remain.

<a id="외부-mcp-종료-결함과-복구"></a>
## External MCP shutdown defect and recovery

During verification-original Editor normal shutdown, Coplay `McpEditorShutdownCleanup.OnEditorQuitting` → `StopManagedLocalHttpServer` also stopped shared server PID 79520. Sharing existing global EditorPrefs handshake/falling back to port inference with stale/missing pidfile affected a server this Editor did not start. Evidence remains in `original-editor.log`, `shared-server-recovery.json`, and package source. Splinter owned shutdown did not stop peer Editors. Preserved Git Editor/B remained running but MCP disconnected.

Server recovered by previously approved restart with same version/port/external logs. New 8080 listener PID 37048; global Codex settings unchanged. Server remained after B shutdown with stale handshake removed. Final Codex default MCP read-only identity job succeeded: existing Git original PID 37600/start `639267352637577053`, exact path, compiling/updating=false. Evidence: `preserved-editor-native-mcp-final.json` and `artifacts/vss-mcp-remediation-20261005/np1005-final-instances.json`.

Recovery did not fix package shutdown ownership defect. Therefore it does not qualify uninterrupted shared-server coexistence with arbitrary existing handshakes. Chosen response at that time: [skill connection checks after shutdown](../../skills/splinter/SKILL.md#check-remaining-mcp-connections-after-an-editor-stops) and [needed server recovery/Editor reconnection procedure](../unity-mcp-response-recovery.md#checking-remaining-mcp-connections-after-editor-shutdown). Current external-tool connection/recovery belongs to agents/tools, not Splinter result/reclamation gates. External MCP code fixes are not mandatory product follow-ups, nor expanded into new project-basis functionality/automatic mode/per-server URLs/global-setting changes. This documentation change is not new actual shutdown verification.

<a id="저장-비용과-제한"></a>
## Storage costs and limits

Warm `unity-storage-warm.json` measures exact A/B/common basis/runner **6 roots** once each. Each task allocation **32,677,232 bytes**, each private journal **32,440,320 bytes**. Common root **1,899,048,272 bytes**: moved existing payload **1,875,469,680**, manifests etc. **11,720,672**, original private journal **11,857,920**. Runner **3,019,440 bytes**, two cache bundles with different builds. Moved baseline excluded from totals.

`unity-storage-after-a.json`: reclaimed A root 567,968 bytes, still-collected B 75,467,824 bytes. B delta 74,907,648 allocated bytes includes repeated compilation/testing/editing costs. Each independent-result payload 227,696 allocated bytes. Common root 1,927,884,112 bytes: existing payload 1,875,469,680, original journal 40,697,856, metadata 11,716,576. Distinct from full copying of a new immutable basis. Remaining receipt/manifest/restore-plan/independent-result costs after final reclamation follow final measurements; evidence folder is not summed into product storage costs.

All 6 roots in `unity-storage-final.json` are complete. A/B roots **567,968/580,256 allocated bytes**, each includes independent payload 227,696. Common root retains **33,498,680 bytes** of manifest/restore-plan/receipts without basis payload/original journal. Including shared runner **3,019,440 bytes**, remaining product-management/result-file allocation is **37,666,344 bytes**. Original data returned to original NTFS and is not summed into additional-copy baseline. Record/independent-result preservation is not failed cleanup and is not arbitrarily deleted.

This backend does not call VSS. Ordinary-user system-wide VSS queries are unknown; system VSS 0 is not claimed. Physical instantaneous peak/OS metadata/individual ACL semantics/large/ARM/reboot/power failures/other Unity versions are NotRun. RAM savings are not acceptance criteria, as the user decided.

`full-build-final.log` locked restore/Release build: 0 warnings/errors, **984 Passed/0 Failed/1 Skipped**. Earlier full regression's 8 failures from missing new mode/capability/command sideEffects schemas remain, distinct from current candidate with omissions corrected. `response-schema-validation-complete.json`: 43 actual file/Unity lifecycle responses, including final restore/status/same restore, passed current product `ContractCatalog` response schema. Support assessment is **scoped-complete-with-limitations** for small actual inputs/automated regression; external MCP shutdown limits/unknown/NotRun remain.
