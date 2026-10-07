<a id="vcs-없는-프로젝트-기준본"></a>
# VCS-free project basis

[한국어](ko/plain-project-basis.md)

`project-snapshot` reads a VCS-free project and Library as one immutable basis, storing only original/per-task changed ranges in SJR2. It uses no VSS, Git init, initial checkout, read disk cache, or whole-file copy-up fallback. [Actual qualification](verification/plain-project-basis.md) is limited to current-candidate file tests and small Unity inputs.

This is the user-authorized managed-original access contract. After saving and normally shutting down the original Editor, **move the existing tree without copying on the same NTFS volume** and provide a writable WinFsp mount at the existing project path. The active original path's filesystem is `SplinterSO`; actual basis data remains on the same NTFS volume. Original source/Library writes also enter the original-only range journal. After all tasks are reclaimed, explicit `project restore` restores the original's current content/metadata to an ordinary NTFS folder. No kernel write-before-preservation layer retaining direct NTFS original access is provided.

<a id="입력과-준비"></a>
## Inputs and preparation

Original and home must be separate on the same NTFS volume. `.git`, `.plastic`, `.hg`, `.svn` at the original, ancestors, or descendants, and incomplete publication files are rejected. VCS inputs use their Git/Plastic path; errors do not fall back to None. Existing immutable-directory input restrictions for reparse, ADS, hardlinks, and special metadata remain. Initial validation/hash reads are required and are not reported as zero I/O.

```powershell
pwsh ./scripts/splinter.ps1 schema project.prepare
pwsh ./scripts/splinter.ps1 project prepare --project <original> --key <fixed basis key> --application unity --home <same NTFS home>
pwsh ./scripts/splinter.ps1 schema workspace.ensure
pwsh ./scripts/splinter.ps1 workspace ensure --project <original> --key <task key> --source none --application unity --storage project-snapshot --project-basis <returned basis.id> --home <same home>
```

For ordinary data, use `--application none` in both prepare and ensure. `unity` fixes Unity-version/package compatibility keys and includes Library in the whole-project basis. `--library-basis` and `--project-basis` cannot be combined. Only `base=snapshot` and task purpose are allowed. Workspace state and managed-basis state/preparation intent are v2. Basis state explicitly identifies application/directory kind; earlier states are preserved/rejected without automatic migration. Policy composition follows [extension boundaries](extensibility-boundaries.md).

Prepare for the same project/key/home/application and the same task ensure retain existing identity. Preserve returned `data.basis.basis.id`, `data.workspace.path`, task id/digest, and basis manifest hash in handoff. Preparation failure recovers the same request's preparation state instead of recapturing with a new key. `project status --id <basis id>` reads stored identity/phase; it does not certify current mount health. Task attach and original plan/publication separately verify exact provider, original volume identity, and basis-reader lease. Missing/damaged data and owner mismatches are preserved/rejected.

<a id="작업결과반영"></a>
## Work, results, and application

Original, A, and B read the same immutable tree, but source/Library/Temp writes enter separate private journals. Use file tools, MCP, and Unity CLI at the returned task path. Splinter does not evaluate tool scene/compile/test success. MCP compares exact project/PID/start on every request, without trusting fixed ports or session-default targets after reload.

Checkpoint preserves basis/result manifests and independent payload through the existing file provider. Generated paths such as Unity Library/Temp and the owned bridge are excluded from source results. Ordinary application none treats every file as data. Payload copying for explicit result preservation is separate from initial basis/read/task-change costs.

```powershell
pwsh ./scripts/splinter.ps1 workspace checkpoint --id <task id> --message <description> --home <home>
pwsh ./scripts/splinter.ps1 workspace integrate --id <task id> --target original --plan --home <home>
# Selected application retains the same --path <path> set in both plan and execution.
pwsh ./scripts/splinter.ps1 workspace integrate --id <task id> --target original --intent-key <fixed intent> --expected-plan-digest <reviewed digest> --home <home>
```

`project-snapshot/source=none` allows full/selected original publication for application none/unity. It uses basis/result/current-original three-way comparison, an exact mount lease, original-Editor shutdown lock, fixed intent/digest, and conditional file publication. Staging whole result files is an explicit original-application cost, not a file copy-up fallback. Managed mounts assume initial ADS rejection, NamedStreams=false, and fixed security descriptors, retaining descendant reparse/management-path guards. Legacy `ntfs-snapshot/source=none/application=unity` original application remains unsupported. [Plain-folder original application](plain-folder-original-apply.md) owns detailed file publication/incomplete-intent protection.

<a id="회수와-원본-복원"></a>
## Reclamation and original restoration

Release checks results, seals, zero owned resources, and absent mount/delta before reclaiming **only that task's shared-basis reference**. It does not remove a peer's basis/Editor/journal. Discard requires the existing explicit intent/digest. Other tasks' validation dependencies and incomplete-apply protection remain. `project restore` owns the shared basis payload, so task snapshot-reference release is separate from deleting common data.

```powershell
pwsh ./scripts/splinter.ps1 workspace release --id <task id> --home <home>
pwsh ./scripts/splinter.ps1 schema project.restore
pwsh ./scripts/splinter.ps1 project restore --id <basis id> --home <home>
```

Before final restore, explicitly reclaim all tasks and save/normally shut down the original Editor. Active dependents/readers, incomplete intents, and unexpected original occupancy block restoration. A fixed restore plan/seal/cursor moves existing files and applies only changed ranges. Interrupted operations resume with the same id/home; a repeated successful restore returns its completion receipt. Active mount/provider/delta and moved basis payload are reclaimed; state, manifests, plans, logs, and independent results remain.

Current restore plan is v2. It fixes existing files' NTFS volume/file identities and durably publishes per-file creation receipts for new files before writing ranges. File/parent-path handles remain open during moves/writes; resume/completion also compares the same file identity and content. Replacement files with the same name/bytes are not treated as owned files. Interruption between creation and receipt publication preserves target/basis/journal and rejects rather than guessing ownership. Earlier restore plans are not automatically migrated. Workspace v2 and overlay v2/SJR2 remain.

Measurement counts the common basis root once through `scripts/measure-workspace-storage.ps1 -SourceKind none -SkipOriginalLibrary -ProjectBasisIds <id> -WorkspaceIds <A>,<B>`. Exclude moved existing payload from added storage cost, distinguishing common manifests/original journal, task journals, runner, and independent results. Whole instantaneous peak, OS metadata, and RAM savings are not guaranteed by this contract.
