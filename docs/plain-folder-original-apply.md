<a id="일반-폴더의-명시적-원본-반영"></a>
# Explicit original application for ordinary folders

[한국어](ko/plain-folder-original-apply.md)

Apply an exact independent checkpoint from an NTFS task with `source=none` and `application=none` to its fixed original `ProjectPath`. Current implementation and standard-user file tests provide the contract below. Actual product VSS/WinFsp A/B qualification follows [verification records](verification/plain-folder-original-apply.md) and the [AP traceability table](plans/validation-matrix.md#vcs-free-original-application). Unity application, Plastic environments, importing released results, and export to other targets are not provided.

<a id="계획과-실행"></a>
## Planning and execution

```powershell
pwsh ./scripts/splinter.ps1 schema workspace.integrate
pwsh ./scripts/splinter.ps1 workspace integrate --id <id> --target original --plan --home <home>
pwsh ./scripts/splinter.ps1 workspace integrate --id <id> --target original --intent-key <fixed request key> --expected-plan-digest <plan digest> --home <home>
```

`--plan` writes neither original nor workspace state. Under short task/original locks, it reads current checkpoint, independent payload, and current original, returning owner/request digest, basis/result, fixed-target physical identity, changed paths, content/type/attribute guards, and conflicts in `data.originalApplyPlan`. Original edits/new files outside changed paths remain; a clean whole folder is not required. Plan success is not application success.

Selected-path membership checks each manifest path's ancestors against the selected set. A single ancestor set for retained/external original children guards deleted directories. It does not rescan the entire manifest per selection or traverse the whole original per deleted directory. Rejection of distinct-boundary siblings, nested/missing selections, Unity companions, change order, and plan-digest semantics remain. Original-application plan/resume arguments in `nextActions` retain fixed `target=original`, `paths`, and the same intent/digest for resume.

Execution requires explicit `original`, an intent key, and the exact reviewed digest. Original changes after planning cause `OriginalApplyPlanChanged` before a new intent is published. Initial conflicts/unsupported cases do not write to the original. Workspace changes after checkpoint produce `ResultNotReady`. Existing Git branch targets/default `development` remain; plain-folder options are not applied to Git.

Process ordinary files to modify/create/delete and required directory creation/empty-directory deletion per path. Paths identical between basis/result are untouched. An original matching the basis is an application candidate; matching the result is verified without additional writes; different changes conflict. Rename compares as deletion/creation. Case-only rename, type replacement, unsupported attribute changes, read-only, reparse, hardlinks, compressed/encrypted/sparse files, and alternate data streams are preserved/rejected. Unrelated children of deleted original directories are not removed.

<a id="게시와-중단"></a>
## Publication and interruption

Core `IOriginalApplyProvider`/`IOriginalFilePublisher` define target/plan/intent and publication ports. Runtime supplies owned-workspace locking, exact result/seal, per-original Splinter locks, and reclamation guards. Files compares immutable basis, independent result, and current original. `WindowsOriginalFilePublisher` handles NTFS actual physical identity, parent handles, content reads, and publication. Host remains read-only.

Before publication, hold handles for all existing files/ancestors in the change set and check content, type, and identity. Block external writes/deletion of affected existing files and ancestor replacement while permitting unrelated-path writes. A new name's occupant is always rejected without overwrite. Unresolved `.splinter-apply-` publication files in the original reject capture by a new task, avoiding incorporation into a new basis. Already created peers retain their fixed bases/edits. Move an existing file to its exact same-directory `.old` name, publish verified `.new` to the original name, then reclaim the old file. The original path may briefly be absent between these renames. Interruption retains old content/new payload at those names; externally created files at the original name are not automatically removed. There is no atomicity across the whole path set.

Apply the existing file's owner/group/DACL, DACL protection, and creation time, plus supported result attributes/mtime, to the published file. Preservation of audit SACLs, security labels, EFS, or other special security metadata, and concurrent permission changes, are outside current verification. These file tests do not establish general support for inputs managing security metadata. Access/sharing errors are not bypassed through elevation or attribute changes.

Before original writes, publish a version 1 plan, fixed intent, and per-path receipts in v2 workspace `originalApply`. Resume only with the same id/home, intent, digest, and basis/result. Distinguish stage-creation identity, payload completion, old-file move, publication, and reclamation; recheck completed paths. Continue an incomplete owned stage only when identity and actual payload prefix match. Stages without receipts, changed temporary files, and new original conflicts after interruption are preserved and stop the operation. There is no automatic rollback/reset, different intent, or temporary-file sweep. Damaged/missing/unknown fields are preserved/rejected.

Incomplete intent blocks checkpoint, ensure, Editor lifetime changes, compact, release/discard. The restricted service's separate Release authorization also checks it. After normal completion, reclamation/discard may be explicit; discard does not undo an already applied original. Completion receipts are fixed to exact results. Duplicate completed requests do not overwrite later original edits. Converting subsequent edits into another checkpoint/apply generation is not provided; preserve existing results/later edits and use a new task.

<a id="비용과-자격"></a>
## Costs and qualification

Explicit application stages one changed file, then publishes/reclaims it before moving to the next path. Existing content is retained by rename, without a separate full original backup/clone. Staging reserves only the known payload length and hashes/reads/writes with a 64KiB buffer. Independent-result objects, workspace range journals, and explicit publication files have distinct costs. This is not workspace creation/hydration/copy-up.

Original application requires [ordinary-folder lifecycle](vcs-independent-workspaces.md) prerequisites and **a current restricted-service package including this pending-apply guard**. Installation/update and grants are separate explicit administrator Apply actions. Ordinary CLI neither performs them nor automatically elevates. Actual product qualification is limited to one small Editor-free Windows x64 NTFS A/B set, without extending to Unity/Plastic, large inputs, or OS failures.
