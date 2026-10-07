<a id="vcs-독립-수명주기-검증"></a>
# VCS-independent lifecycle verification

[한국어](../ko/verification/vcs-independent-workspaces.md)

Scope belongs to the [fixed plan](../plans/implementation-plan.md#closed-preceding-development-unit-vcs-free-basic-lifecycle), current implementation to the [plain-folder contract](../vcs-independent-workspaces.md), and gate status to the [VCS tracking table](../plans/validation-matrix.md#vcs-free-basic-lifecycle). Existing Git-based M0 completion is not inherited by this candidate.

`FileWorkspaceTests` uses the actual file provider and a **fake snapshot backend**. Fixture copies prepare a small synthetic test basis; they do not qualify product NTFS copying/range writes. Tests cover A/B, later original changes, same-key resumption, actual checkpoint-payload content and checking without source/snapshot/mount, dirty discard, retention of existing results, peer protection, lost capture/connect/unmount/delta/snapshot responses, corrupt-payload preservation, seal-change blocking, rejection of VCS management data, absence of Git-error fallback, and rejection of previous state. All plain-folder stages confirm 0 Git-adapter calls.

`ContractAndCliTests` checks plain-folder inputs/schemas and doctor without Git/Unity. Existing Git lifecycle/merge/dirty guards and Windows resource/journal regressions are included in required `scripts/build.ps1`. New `Splinter.SourceControl.Files.dll` is included in the runner bundle/service package. The runner currently has 7 files; actual memory values from previous 6-file candidates are not reused as current-candidate values.

Final `pwsh ./scripts/build.ps1` locked restore/Release had 0 warnings/errors, with **794 passed and 1 test excluded at that time Skipped out of 795**. TRX is `artifacts/vcs-independent/native-fix-final.trx`; build output is `native-fix-build.log`. Earlier 794-test `automated-final.trx`/`verification.json` and initial schema/fixture-failure evidence remain preserved and are not overwritten with final passes. The path defect reproduced during actual reclamation was checked by same-request recovery below and automated regression using actual authority pins.

<a id="실제-ab-한-묶음의-판정"></a>
## Assessment of one actual A/B bundle

The [fixed native plan](../plans/vcs-independent-validation.md) ran with user approval on 2026-10-02. Assessment is **scoped-complete-with-limitations**. Raw evidence is in `artifacts/vcs-independent/`; final summary is `native-execution.json`. Existing Git fixture, Editors, results, and protective state were preserved.

| Boundary | Actual observation/evidence |
| --- | --- |
| Input/permissions | `T:\vcs-none-20261002` without `.git`, home `.local/vcs1002`, `source=none`, `application=unity`, `base=snapshot`, journal budget 16 GiB. Immutable basis fixed including settings/locks created by first import. Three existing grants and 1 new grant applied through approved installation tools. Protected records retained: 1,317 during first update, 1,345 during corrective update; ordinary CLI `callerElevated=false` |
| A/B creation/resumption | A `87465748b51e98555de4313f`, B `968940e262731f6de9410814`, distinct actual VSS/WinFsp mounts. `a-resume-02.json` retains identical A id/digest/basis/snapshot from a new CLI. A/B still read `basis` after original changes to `original-after-capture` |
| Owned Editors/external tools | Unity `6000.3.22f1`, A PID 45772/UTC ticks 639265202652845900, B PID 25072/639265202897372343. Exact-path Unity CLI/Pipeline ports 7801/7802, loaded `basis`, and A `a-ok!` confirmed. A external compilation succeeded; B intentionally failed with `CS1525`. Splinter heartbeat was not compilation-success evidence |
| Results/peer | `a-checkpoint-04.json`: stable-journal collection, normal Editor shutdown, independent payload. `b-after-a-release.json`: after A unmount/delta reclamation, B still responds to external tools at its own path/failed source. Original Editor PID 76076/existing start time and original Pipeline 7800 remain ready |
| Reclamation/discard | Same A release after fix is `released`. B discarded with fixed intent/digest without a successful checkpoint; the earlier A snapshot caused `discard-reclaim-pending`, then the same intent reached `discarded`; duplicate request returns identical state. Both final mounts/deltas/snapshots absent; owned Editor/provider/process/handle/memory 0; ownership verified. A result retained; B has no `files/result.json` |
| Content after reclamation | `independent-payload-after-reclaim.json`: lengths/SHA-256 verified for 49 basis/result file entries and 26 objects. Preserved `basis`/`a-ok!` and modified original read separately. Verification does not use original/snapshot/mount as result inputs |

Initial creation, Editors, and checkpoint ran with service package `package-20261002-01`. A release's `StoragePathRejected` was a defect passing content-object path `objects/<hash>` to Windows authority as a noncanonical path. Only `FileWorkspaceProvider` internal paths were formed with OS separators; authority guards remained. After final automated regression and new package `package-20261002-02` (manifest SHA-256 `57a91e572c7be16e61a3a6f6f2901bfc9c8e3d18c6297ffd26f3f88edfcd2571`) were applied, **the same A release/B discard records resumed** to completion. Initial failure/B deferred-reclamation evidence remains. The full A/B was not newly recreated/repeated with the corrective package; execution of a previous candidate is not marked as a separate pass for the new candidate.

Unity CLI `1.0.0-beta.5` `status`/`pipeline list` omitted A/B, while exact `command --project-path` connected. Temporary Hub registration of both owned paths did not resolve list omission; only added registrations were removed after shutdown. Future handoffs compare target path, port, PID/start time rather than infer absence from lists alone. This scope covers reading/eval/recompile connections, not general MCP, Coplay parallel workers, or full external-tool handoff qualification.

<a id="비용과-남은-제한"></a>
## Costs and remaining limits

Two initial storage observations show full payload copying, journal, and read cache of 0. A marker changed through a **5-byte FileStream write** at offset 61. Existing range storage, individual ACK flush, and seal regressions remain; on Editor shutdown A journal was 13,982,199 bytes and B 13,225,733 bytes. These include Unity Library/management-file writes, not marker costs or full-file copying. Initial Editor-import logs show A 76.715 seconds, B 76.034 seconds; these are not total preparation times.

A independent preservation: logical basis 52,937 bytes, logical result 54,069 bytes, unique actual payload 54,140 bytes/26 objects. File-allocation samples: payload 92,184 bytes, both deltas at collection 28,180,480 bytes, 7 shared runner files 1,057,112 bytes. Final delta allocation is absent; retained control/evidence remains separately. Original Library 45,594,344 bytes is a baseline and is not summed. Measurement used `-SourceKind none` without Git queries; file-result category is part of control and is not double-counted. Shared VSS workspace attribution and total physical peak are **unknown**.

Ordinary editorless `application=none` has automated conformance only; no separate native editorless bundle ran. Plain-folder original-apply, Plastic-environment compatibility, large/ARM/remote/OS, and native forced-interruption matrices are also **NotRun/out of scope**. Existing Git M0 and historical runner-memory values do not establish full support for the new candidate.
