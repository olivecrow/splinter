<a id="한정-버전-ai-시작-안내"></a>
# AI start guide for the bounded release

[한국어](ko/agent-start.md)

Product entry points are the repository checkout's `scripts/splinter.ps1` and [skill source](../skills/splinter/SKILL.md). Views/probes belong to the [separate development CLI](development-cli.md). See the [glossary](glossary.md) for terminology. Use workspaces only for continuing work that needs independent execution; work directly when the current path is safe.

<a id="저장-경로별-시작점"></a>
## Starting points by storage path

Fix source/application/storage and basis in the initial request, then read that path's documents/schema. Do not transfer service prerequisites or actual execution qualification from one path to another.

| Storage path | Prerequisites and usage | Execution evidence and limitations |
| --- | --- | --- |
| `ntfs-snapshot`, source git/none/plastic | Requires the [NTFS contract](ntfs-workspaces.md), exact service/grants, and WinFsp. Source-specific conditions follow [ordinary folders](vcs-independent-workspaces.md) and [Plastic compatibility](plastic-compatibility.md). | Candidate-specific bounded qualification in [M0](verification/m0-delivery.md), [ordinary folders](verification/vcs-independent-workspaces.md), and [Plastic](verification/plastic-compatibility.md). For Editor-free ordinary-folder original application, check plan/intent and current service guards in the [separate contract](plain-folder-original-apply.md). |
| `git-snapshot`, source git | Uses a fixed commit under the [Git basis](git-project-basis.md), excluding ignored/private source. Requires WinFsp, without VSS service/grant. | Distinguish [reader/Editor-free Git](verification/git-project-basis.md), [cold Unity A/B](verification/git-unity-validation.md), and [original Library/warm Unity](verification/git-library-reference.md). Actual Unity records qualify specified candidates/inputs only, without general large-input, ARM, or OS support. |
| `plastic-snapshot`, source plastic | Uses fixed changeset/revision under the [Plastic basis](plastic-project-basis.md), standard-user cm access, and WinFsp. VSS service/grant is unnecessary. | Check [basis verification](verification/plastic-project-basis.md) and [full/selected original application](plastic-original-apply.md). Original application requires normal original-Editor shutdown and a separate fixed plan/intent, without changing server history. |
| `project-snapshot`, source none | Uses the explicitly authorized managed mount of the original under the [project basis](plain-project-basis.md). Requires WinFsp, without VSS service/grant. | Check file/small-Unity qualification in [NP verification](verification/plain-project-basis.md). Authorization to convert Library alone does not authorize a whole-original mount. |

Git/Plastic original Library reuse requires authorization, normal original-Editor shutdown, and a returned basis id from the [separate Library conversion](git-library-reference.md), fixed in `--library-basis`. `project-snapshot` uses `project prepare --application none|unity` to move source/Library without copying on the same NTFS volume and provide a writable view at the original path. Fix returned basis id in `workspace ensure --source none --storage project-snapshot --project-basis <id>` with the same application/home. After task reclamation and all dependents/readers end, explicitly use `project restore` to restore an ordinary NTFS original.

<a id="ntfsvss-gitunity-경로의-전제와-예시"></a>
## NTFS/VSS Git/Unity prerequisites and example

The prerequisites and creation example below concern `source=git`, `application=unity`, and `storage=ntfs-snapshot`. Other paths use their owning documents in the table above.

| Item | Conditions to check |
| --- | --- |
| Product | Existing development checkout's `scripts/splinter.ps1` and Release build. If absent, the agent prepares it with `pwsh ./scripts/build.ps1`. .NET SDK `10.0.112` compatible patch, Git, and PowerShell 7.2 or later. |
| Original | Clean Git repository root on existing NTFS, with an internal `.git` directory and original HEAD matching requested base. Immediately before capture, Unity changes must be saved and import/compile/Play inactive. |
| Input | Dirty/staged/untracked, submodule/symlink, LFS/filter/working-tree-encoding are rejected. ADS/hardlink/reparse/EFS/compressed/sparse input and individual ACL semantics are unsupported. See [NTFS input boundaries](ntfs-workspaces.md#inputs-and-privilege-boundaries). |
| System | Existing WinFsp and `SplinterStorageAuthority` service supporting the current discard contract. Exact user/original/volume/home identities must already be registered. |
| Home/space | All tasks for the same project use one short NTFS home outside the original. Creation/reconnect require 2 GiB free on the state volume; new capture requires 8 GiB headroom under the existing VSS diff-area maximum. Configuration is not automatically changed. |
| Editor | Current bounded execution covers a small Unity `6000.3.22f1` fixture, existing warm Library, and independent Editors. It does not extend to other inputs/versions, large-input costs, ARM A/B, or OS-failure guarantees. |

Ordinary CLI/Git/Unity run as standard users. If service installation/update or grant changes are needed, prepare the [service installation plan](storage-privilege-service.md#installation-and-discovery); administrator Apply requires separate authorization. General product installation, global PATH, automatic skill discovery/conversation binding are not provided.

<a id="발견과-생성"></a>
## Discovery and creation

Obtain product/project/home/base from project policy or same-task handoff. Fill the strings below with actual authorized values. Do not bypass failure with a new home/key.

```powershell
$cli = '<absolute product checkout path>/scripts/splinter.ps1'
$project = '<absolute registered original path>'
$workspaceHome = '<absolute registered common home path>'
$base = '<base ref matching original HEAD>'
$key = '<fixed key for this task>'

pwsh -NoProfile -File $cli capabilities
pwsh -NoProfile -File $cli schema workspace.ensure
pwsh -NoProfile -File $cli storage service status
pwsh -NoProfile -File $cli doctor --project $project
```

`doctor` is metadata diagnosis; service status observes SCM. Success does not guarantee grants, VSS headroom, Editor readiness, or full execution qualification. Distinguish `doctor.data.readyForExecution=false` and `checksNotRun` from actual execution checks. Git ownership/environment errors require checking the exact original/existing policy, without automatically changing global settings or source.

```powershell
$reply = (& pwsh -NoProfile -File $cli workspace ensure --project $project --key $key --base $base --storage ntfs-snapshot --home $workspaceHome) | ConvertFrom-Json
$reply
```

Read response `status`, `terminal`, `error`, `retryDisposition`, and `nextActions`. `Accepted` means in progress; a failed request is not a usable workspace. After checking ensure Succeeded and actual preparation, use `data.workspace.path` as the edit/Git/tool working directory and absolute file target. Do not substitute the original path. If tool permissions prohibit it, preserve the access failure and task.

Preserve `$cli/$project/$workspaceHome/$base/$key`, ID, returned root/control, `requestDigest`, `baseRevision`, snapshot, and explicit budget options in conversation/handoff. Resume with **the same ensure inputs**, checking identical ID/root/digest/basis/snapshot. There is no per-conversation exclusive lock for arbitrary file edits, so use only one writer per task path.

<a id="편집editor종료"></a>
## Editing, Editor use, and completion

MCP and Unity CLI are optional external tools agents choose as needed from user-installed tools. Splinter storage isolation, bases, Library/unchanged-file references, range changes, independent results, safe original application, and owned-resource lifetime work without them. Specific tool names/versions/internal APIs are not product prerequisites or gates. Agents handle selection, connection, refresh, compilation, Play/Test/build, capture, and result evaluation. Installing, downgrading, modifying, or creating/operating separate modified external tools is outside Splinter's scope. Compare actual project/PID/start with returned workspace/owned Editor. Missing list entries alone do not mean no Editor; read current tool help/schema. Splinter's bridge handles lifetime observations and normal shutdown only. See [target selection](alpha-workspaces.md#exact-target-selection-with-unity-cli).

Read `schema <command>` before using each needed command, and use saved ID/home. Start the Editor through `unity start` only when needed; check actual `application.ready` in status for preparation. `ready` is execution readiness. After editing, use selected external tools for refresh/compilation/tests and let AI evaluate results. Splinter responses supply no compile/test success. Ordinary status does not repeat whole-source checks; distinguish `--verify-source` for current verification and `--audit-source` for damaged-cache audits.

Responses after `unity start` and `ensure --editor` also use execution observations. `sourceVerification=not-checked`; request current source verification separately. Do not confuse a source-check failure during import after startup with execution failure.

When an Editor using external tools exits, the agent checks remaining target connections/identity before the next external request. An external package stopping a shared server and recovery from that problem belong to the tool/agent procedure. Connection failure is not a Splinter result-preservation/reclamation gate; servers, packages, and global settings are not automatically changed. Follow [optional connection checks](unity-mcp-response-recovery.md#checking-remaining-mcp-connections-after-editor-shutdown).

| Explicit intent | Path to use |
| --- | --- |
| Continue later | Keep the same workspace. If needed, normally stop only the Editor with `unity stop --id <id> --home <home>`. |
| Preserve results and reclaim resources | Needed external validation → `workspace checkpoint --id <id> --message <description> --home <home>` → explicit `workspace release`. Original integration is optional. |
| Apply to original and reclaim resources | Checkpoint → `workspace integrate --id <id> --target <branch> --home <home>` to the requested clean current original branch → explicit release. Post-integration original-Editor validation is separate; this authorizes neither push nor deployment. |
| Discard failed/uncommitted work | `workspace discard --id <id> --intent-key <fixed intent> --expected-request-digest <saved digest> --home <home>`. Successful validation/checkpoint is unnecessary; use the exact target and existing discard authorization. |

Checkpoint is the final collection boundary; repeated editing/resumption happens beforehand. Work without an Editor or with failed compilation can also be independently preserved through the same `workspace checkpoint`. It verifies source/journal stability without awarding compile success. Optional Library/view storage paths belong to the [development CLI](development-cli.md). Handle candidate/cache dependencies separately, without cascading deletion. Conversation/Editor termination, timeout, lease expiry, and disk pressure do not imply integration/reclamation/discard intent.

<a id="실패와-완료-판독"></a>
## Reading failures and completion

| Response/condition | Next action |
| --- | --- |
| Verification failure, SourceChanged, dirty original | Preserve changes and fix source/run current verification. Do not stash/reset to erase original changes or discard results. |
| DeltaChangedAfterCollection | Preserve revision/delta/snapshot. After exact owned-Editor shutdown, zero resources/handles, and journal stability, explicitly retry checkpoint/release with the same ID/home. |
| Damaged state, unknown ownership/execution, interrupted effects | Preserve structured error/evidence; inspect provided next actions and same-request recovery conditions. Do not bypass through manually deleting JSON/locks/temporary files or guessed forced termination. |
| reclaim-pending, discard-reclaim-pending | Distinguish logical completion from pending physical reclamation. Only same-task/home release or same discard-intent retries are allowed; no other-snapshot sweeps. |
| released, discarded | Completion confirms absent actual mount/Git/delta/exact snapshot and zero owned resources. Records/logs/independent results remain. Unknown is not 0; new edits use a new task key. |

Delayed Library-lock writes may occur after the first normal stop. External-tool connection/operation failures are handled using that tool's evidence. Preparation, owned-memory, and retry costs favor direct work for short edits. If acknowledged-write loss or original/peer damage is observed on a supported path, stop that path and fix it first. Actual reboot/power/physical-space failures and every native interruption boundary of current binaries are not guaranteed. [Representative usage](verification/representative-agent-workflow.md) owns costs and unconfirmed publication errors; [delivery verification](verification/m0-delivery.md) owns later revalidation conditions.
