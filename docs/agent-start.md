# AI start guide for the current candidate

The scalable-storage candidate uses task v4, managed basis v3 and product overlay v3. [Scalable storage](scalable-storage.md) owns the current representation and pending SCAL-01–08 qualification. [Repeated original application](repeated-original-apply.md) retains immutable captures, explicit results, per-path history and external live-Editor cooperation. Historical RI, MX and GCO evidence remains tied to its recorded task-v3/basis-v2 candidate. Preserve/refuse earlier state and use its matching binary; no automatic migration or reclamation.

Product entry points are the repository checkout's `scripts/splinter.ps1` and [skill source](../skills/splinter/SKILL.md). Views/probes belong to the [separate development CLI](development-cli.md). See the [glossary](glossary.md) for terminology. Use workspaces only for continuing work that needs independent execution; work directly when the current path is safe.

For implementation/performance work, start with the lifecycle optimization map and structured stage/code index. They connect preparation, I/O, Editor work, application/discard and final shared-basis restoration without authorizing a new project experiment.

<a id="저장-경로별-시작점"></a>
## Starting points by storage path

Fix source/application/storage and basis in the initial request, then read that path's documents/schema. Do not transfer input prerequisites or actual execution qualification from one path to another.

| Storage path | Prerequisites and usage | Execution evidence and limitations |
| --- | --- | --- |
| `git-snapshot`, source git | Uses a fixed commit under the [Git basis](git-project-basis.md), excluding ignored/private source. Requires WinFsp, without VSS service/grant. | Distinguish reader/Editor-free Git, cold Unity A/B, and original Library/warm Unity. Actual Unity records qualify specified candidates/inputs only, without general large-input, ARM, or OS support. |
| `plastic-snapshot`, source plastic | Uses fixed changeset/revision under the [Plastic basis](plastic-project-basis.md), standard-user cm access, and WinFsp. VSS service/grant is unnecessary. | Check basis verification and [full/selected original application](plastic-original-apply.md). Original application requires fixed result/plan/intent and either stopped reservation or exact live-original cooperation, without changing server history. |
| `project-snapshot`, source none | Uses the explicitly authorized managed mount of the original under the [project basis](plain-project-basis.md). Requires WinFsp, without VSS service/grant. | Check file/small-Unity qualification in NP verification. Authorization to convert Library alone does not authorize a whole-original mount. |

Git/Plastic original Library reuse requires authorization, normal original-Editor shutdown, and a returned basis id from the [separate Library conversion](git-library-reference.md), fixed in `--library-basis`. `project-snapshot` uses `project prepare --application none|unity` to move source/Library without copying on the same NTFS volume and provide a writable view at the original path. Fix returned basis id in `workspace ensure --source none --storage project-snapshot --project-basis <id>` with the same application/home. After task reclamation and all dependents/readers end, explicitly use `project restore` to restore an ordinary NTFS original.

<a id="ntfsvss-gitunity-경로의-전제와-예시"></a>
## Git-basis/Unity prerequisites and example

The prerequisites and creation example below concern `source=git`, `application=unity`, and `storage=git-snapshot`. Other paths use their owning documents in the table above.

| Item | Conditions to check |
| --- | --- |
| Product | Existing development checkout's `scripts/splinter.ps1` and Release build. If absent, the agent prepares it with `pwsh ./scripts/build.ps1`. .NET SDK `10.0.112` compatible patch, Git, and PowerShell 7.2 or later. |
| Original | Clean Git repository root on existing NTFS, with an internal `.git` directory and original HEAD matching requested base. Immediately before capture, Unity changes must be saved and import/compile/Play inactive. |
| Input | Dirty/staged/untracked, submodule/symlink, LFS/filter/working-tree-encoding are rejected. ADS/hardlink/reparse/EFS/compressed/sparse input and individual ACL semantics are unsupported. See [NTFS input boundaries](ntfs-workspaces.md#inputs-and-privilege-boundaries). |
| System | Existing WinFsp. No VSS service, grant or elevated execution. |
| Home/space | One short NTFS home outside the original for tasks on the same project. Space checks use the actual backing volume and the current publication/recovery requirement; no fixed journal budget or 2 GiB reserve is a total-input gate. |
| Editor | Current bounded execution covers a small Unity `6000.3.22f1` fixture, existing warm Library, and independent Editors. It does not extend to other inputs/versions, large-input costs, ARM A/B, or OS-failure guarantees. |

Ordinary CLI/Git/Unity run as standard users. General product installation, global PATH, automatic skill discovery/conversation binding are not provided. After VSS retirement, legacy state is preserved/rejected; service reinstallation is not a recovery action.

<a id="발견과-생성"></a>
## Discovery and creation

Obtain product/project/home/base from project policy or same-task handoff. Fill the strings below with actual authorized values. Do not bypass failure with a new home/key.

```powershell
$cli = '<absolute product checkout path>/scripts/splinter.ps1'
$project = '<absolute original path>'
$workspaceHome = '<absolute common home path>'
$base = '<base ref matching original HEAD>'
$key = '<fixed key for this task>'

pwsh -NoProfile -File $cli capabilities
pwsh -NoProfile -File $cli schema workspace.ensure
pwsh -NoProfile -File $cli doctor --project $project
```

`doctor` is metadata diagnosis, not Editor readiness or full execution qualification. Distinguish `doctor.data.readyForExecution=false` and `checksNotRun` from actual checks. Check Git ownership/environment failures against the exact original and existing policy without automatically changing global settings or source.

```powershell
$reply = (& pwsh -NoProfile -File $cli workspace ensure --project $project --key $key --base $base --storage git-snapshot --home $workspaceHome) | ConvertFrom-Json
$reply
```

Read response `status`, `terminal`, `error`, `retryDisposition`, and `nextActions`. `Accepted` means in progress; a failed request is not a usable workspace. After checking ensure Succeeded and actual preparation, use `data.workspace.path` as the edit/Git/tool working directory and absolute file target. Do not substitute the original path. If tool permissions prohibit it, preserve the access failure and task.

Preserve `$cli/$project/$workspaceHome/$base/$key`, ID, returned root/control, `requestDigest`, `baseRevision`, snapshot, explicit compaction options, capture/result identities and maintenance request/scope in conversation/handoff. Resume with **the same ensure inputs**, checking identical ID/root/digest/basis/snapshot. There is no per-conversation exclusive lock for arbitrary file edits, so use only one writer per task path.

For independent Git tasks, the agent can overlap two `workspace ensure` calls with different stable keys and the same fixed project/home/base/Library basis. Prepare that Library once before dispatch; use one writer per returned task path. Check each response independently and wait for both to succeed before treating the pair as ready. A failed or lost response preserves that task: inspect its supplied next action and resume the same identity; a timeout alone never authorizes a duplicate write. One failure does not authorize releasing the peer. This is agent orchestration of existing commands, not a product queue or automatic mode. The bounded parallel preparation verification uses small Editor-free A/B; it does not measure full Unity readiness.

<a id="편집editor종료"></a>
## Editing, Editor use, and completion

MCP and Unity CLI are optional external tools agents choose as needed from user-installed tools. Splinter storage isolation, bases, Library/unchanged-file references, range changes, independent results, safe original application, and owned-resource lifetime work without them. Specific tool names/versions/internal APIs are not product prerequisites or gates. Agents handle selection, connection, refresh, compilation, Play/Test/build, capture, and result evaluation. Installing, downgrading, modifying, or creating/operating separate modified external tools is outside Splinter's scope. Compare actual project/PID/start with returned workspace/owned Editor. Missing list entries alone do not mean no Editor; read current tool help/schema. Splinter's bridge handles lifetime observations and normal shutdown only. See [target selection](alpha-workspaces.md#exact-target-selection-with-unity-cli).

Read `schema <command>` before using each needed command, and use saved ID/home. Start the Editor through `unity start` only when needed; check actual `application.ready` in status for preparation. `ready` is execution readiness. After editing, use selected external tools for refresh/compilation/tests and let AI evaluate results. Splinter responses supply no compile/test success. Ordinary status does not repeat whole-source checks; distinguish `--verify-source` for current verification and `--audit-source` for damaged-cache audits.

Responses after `unity start` and `ensure --editor` also use execution observations. `sourceVerification=not-checked`; request current source verification separately. Do not confuse a source-check failure during import after startup with execution failure.

When an Editor using external tools exits, the agent checks remaining target connections/identity before the next external request. An external package stopping a shared server and recovery from that problem belong to the tool/agent procedure. Connection failure is not a Splinter result-preservation/reclamation gate; servers, packages, and global settings are not automatically changed. Follow [optional connection checks](unity-mcp-response-recovery.md#checking-remaining-mcp-connections-after-editor-shutdown).

| Explicit intent | Path to use |
| --- | --- |
| Continue later | Keep the same workspace. If needed, normally stop only the Editor with `unity stop --id <id> --home <home>`. |
| Preserve results and reclaim resources | Needed external validation → `workspace checkpoint --id <id> --request-key <capture-key> --message <description> --release --home <home>` for a range-journal workspace. This explicitly authorizes preservation and reclamation together; original integration is optional. Separate checkpoint and release remain available. |
| Apply and continue the same task | External save → checkpoint with a stable request key → `workspace integrate --target original --result <id>` plan/intent/digest and exact live-original cooperation → safe refresh/reload and loaded-state checks → continue. |
| Final original application and reclamation | External save → normal stop of only this task → same capture/publication engine → explicit release. Original/peers remain running. Explicit Git branch merge is a separate clean-target operation; no push/deployment. |
| Discard failed/uncommitted work | `workspace discard --id <id> --intent-key <fixed intent> --expected-request-digest <saved digest> --home <home>`. Successful validation/checkpoint is unnecessary; use the exact target and existing discard authorization. |

Checkpoint collects immutable generations while editing/resumption continues in the same task and Editor; use a stable request key for each result. Work without an Editor or with failed compilation can also be independently preserved through the same `workspace checkpoint`. It verifies source/journal stability without awarding compile success. Optional Library/view storage paths belong to the [development CLI](development-cli.md). Handle candidate/cache dependencies separately, without cascading deletion. Conversation/Editor termination, timeout, lease expiry, and disk pressure do not imply integration/reclamation/discard intent.

## Paged results and metadata recovery

Read `schema workspace.read` and `schema workspace.maintain`. `status` returns summary/counts; use root-bound pages for histories, files, applied paths, plans, selection and conflicts. Explicit maintenance is `workspace maintain --id <id> --request-key <key> --scope workspace|shared|content|ranges|all --home <home>`. Retain its exact key/scope and `data.maintenance.stages`; space/I/O failure must resume that request before another mutation. Editor stop remains available. Completed historical requests return their receipt. Protected unknown residue has a count/root and `workspace read --collection maintenance-residuals --request-key <key> --store-scope <store>`; never delete it to bypass recovery. See [scalable storage](scalable-storage.md) for cache/worker/handle costs and current qualification.

<a id="실패와-완료-판독"></a>
## Reading failures and completion

| Response/condition | Next action |
| --- | --- |
| Verification failure, SourceChanged, dirty original | Preserve changes and fix source/run current verification. Do not stash/reset to erase original changes or discard results. |
| DeltaChangedAfterCollection | Preserve revision/delta/snapshot. After exact owned-Editor shutdown, zero resources/handles, and journal stability, explicitly retry checkpoint/release with the same ID/home. |
| Damaged state, unknown ownership/execution, interrupted effects | Preserve structured error/evidence; inspect provided next actions and same-request recovery conditions. Do not bypass through manually deleting JSON/locks/temporary files or guessed forced termination. |
| reclaim-pending, discard-reclaim-pending | Distinguish logical completion from pending physical reclamation. Only same-task/home release or same discard-intent retries are allowed; no other-snapshot sweeps. |
| released, discarded | Completion confirms absent actual mount/Git/delta/exact snapshot and zero owned resources. Records/logs/independent results remain. Unknown is not 0; new edits use a new task key. |

Delayed Library-lock writes may occur after the first normal stop. External-tool connection/operation failures are handled using that tool's evidence. Preparation, owned-memory, and retry costs favor direct work for short edits. If acknowledged-write loss or original/peer damage is observed on a supported path, stop that path and fix it first. Actual reboot/power/physical-space failures and every native interruption boundary of current binaries are not guaranteed. Representative usage owns costs and unconfirmed publication errors; delivery verification owns later revalidation conditions.
