<a id="일반-폴더의-명시적-원본-반영"></a>
# Explicit original application for ordinary folders

Current task state is v3. [Repeated original application](repeated-original-apply.md) owns live immutable captures, explicit result selection, per-path applied history and external-tool live-Editor cooperation; [RI qualification](verification/repeated-original-apply.md) remains separate from earlier stopped-Editor evidence. Managed basis state remains v2; old task state is preserved/refused without migration.

[한국어](ko/plain-folder-original-apply.md)

Apply an exact independent `project-snapshot` checkpoint with `source=none` to its fixed original ProjectPath. Full/selected publication for application none/unity, original mount identity and explicit Editor cooperation follow the [managed project basis](plain-project-basis.md). The common conditional-publication contract and automated/native file tests below remain. Historical VSS A/B in [AP verification](verification/plain-folder-original-apply.md) does not qualify the new path. Plastic follows its [separate contract](plastic-original-apply.md); released-result import and other export targets are unsupported.

<a id="계획과-실행"></a>
## Planning and execution

```powershell
pwsh ./scripts/splinter.ps1 schema workspace.integrate
pwsh ./scripts/splinter.ps1 workspace integrate --id <id> --target original --result <result-id> --plan --home <home>
pwsh ./scripts/splinter.ps1 workspace integrate --id <id> --target original --result <result-id> --intent-key <fixed request key> --expected-plan-digest <plan digest> --home <home>
```

`--plan` writes neither original nor workspace state. Under short task/original locks, it reads current checkpoint, independent payload, and current original, returning owner/request digest, basis/result, fixed-target physical identity, changed paths, content/type/attribute guards, and conflicts in `data.originalApplyPlan`. Original edits/new files outside changed paths remain; a clean whole folder is not required. Plan success is not application success.

Selected-path membership checks each manifest path's ancestors against the selected set. A single ancestor set for retained/external original children guards deleted directories. It does not rescan the entire manifest per selection or traverse the whole original per deleted directory. Rejection of distinct-boundary siblings, nested/missing selections, Unity companions, change order, and plan-digest semantics remain. Original-application plan/resume arguments in `nextActions` retain fixed `target=original`, `paths`, and the same intent/digest for resume.

Execution requires explicit `original`, an intent key, and the exact reviewed digest. Original changes after planning cause `OriginalApplyPlanChanged` before a new intent is published. Initial conflicts/unsupported cases do not write to the original. Publication reads the explicitly selected latest immutable result; later worker edits are permitted. Git uses this same file-publication entrypoint for `--target original`; its separate branch merge retains clean-target protection.

Process ordinary files to modify/create/delete and required directory creation/empty-directory deletion per path. Paths identical between the per-path applied baseline/result are untouched. Initially that baseline is the fixed basis; each completed selection advances only its paths. An original matching the applied baseline is an application candidate; matching the result is verified without additional writes; different changes conflict. Rename compares as deletion/creation. Case-only rename, type replacement, unsupported attribute changes, read-only, reparse, hardlinks, compressed/encrypted/sparse files, and alternate data streams are preserved/rejected. Unrelated children of deleted original directories are not removed.

<a id="게시와-중단"></a>
## Publication and interruption

Core `IOriginalApplyProvider`/`IOriginalFilePublisher` define target/plan/intent and publication ports. Runtime supplies owned-workspace locking, exact result/seal, per-original Splinter locks, and reclamation guards. Files compares per-path applied history (initially the immutable basis), independent result, and current original. `WindowsOriginalFilePublisher` handles NTFS actual physical identity, parent handles, content reads, and publication. Host remains read-only.

Planning may use Core's optional `IOriginalFileObservationProvider`/`IOriginalFileObservationScope`. The Windows scope holds the exact root, ancestors and managed-reader lease for one plan, rereads each target and releases its handles after observation, then rechecks managed availability and VCS administration before saving the plan. It does not retain observations across commands or make a plan an atomic snapshot of the original. Providers without the optional port keep ordinary per-target observation. Publication independently reacquires and verifies all targets under the existing locks. [ST-49–51](verification/scalable-storage-throughput-followup.md) owns the small verification and timing limits.

Before publication, hold handles for all existing files/ancestors in the change set and check content, type, and identity. Block external writes/deletion of affected existing files and ancestor replacement while permitting unrelated-path writes. A new name's occupant is always rejected without overwrite. Unresolved `.splinter-apply-` publication files in the original reject capture by a new task, avoiding incorporation into a new basis. Already created peers retain their fixed bases/edits. Move an existing file to its exact same-directory `.old` name, publish verified `.new` to the original name, then reclaim the old file. The original path may briefly be absent between these renames. Interruption retains old content/new payload at those names; externally created files at the original name are not automatically removed. There is no atomicity across the whole path set.

Apply the existing file's owner/group/DACL, DACL protection, and creation time, plus supported result attributes/mtime, to the published file. Preservation of audit SACLs, security labels, EFS, or other special security metadata, and concurrent permission changes, are outside current verification. These file tests do not establish general support for inputs managing security metadata. Access/sharing errors are not bypassed through elevation or attribute changes.

Before original writes, publish a version 2 generation plan, fixed intent, and per-path receipts in v3 workspace `originalApply`. Resume only with the same id/home, intent, digest, and basis/result. Distinguish stage-creation identity, payload completion, old-file move, publication, and reclamation; recheck completed paths. Continue an incomplete owned stage only when identity and actual payload prefix match. Stages without receipts, changed temporary files, and new original conflicts after interruption are preserved and stop the operation. There is no automatic rollback/reset, different intent, or temporary-file sweep. Damaged/missing/unknown fields are preserved/rejected.

Incomplete intent blocks checkpoint, ensure, Editor lifetime changes, compact, release/discard. After normal completion, reclamation/discard may be explicit; discard does not undo an already applied original. Completion receipts are fixed to exact results. Duplicate completed requests do not overwrite later original edits. Subsequent edits use a new capture key in the same task; per-path publication history supplies the next applied baseline.

<a id="비용과-자격"></a>
## Costs and qualification

Explicit application stages one changed file, then publishes/reclaims it before moving to the next path. Existing content is retained by rename, without a separate full original backup/clone. Staging reserves only the known payload length and hashes/reads/writes with a 64KiB buffer. Independent-result objects, workspace range journals, and explicit publication files have distinct costs. This is not workspace creation/hydration/copy-up.

Original application follows the preparation, ownership and publication conditions of [ordinary folders](vcs-independent-workspaces.md) and [project basis](plain-project-basis.md). No VSS service/grant is required. Distinguish NP qualification from the historical AP small Editor-free Windows x64 NTFS A/B; do not extend either to large inputs or OS failures.
