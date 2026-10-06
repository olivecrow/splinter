---
name: splinter
description: Use Splinter to create or resume an isolated workspace when the user requests parallel work or another task makes independent work necessary, and to preserve, integrate, or explicitly discard that task. Choose direct work when isolation is unnecessary. Use the existing CLI and the task's saved identity rather than a runner or automatic conversation binding.
---

# Splinter workspace use

This is the source skill shipped in the Splinter repository. It can be read directly; project/global installation and automatic Codex discovery are not implemented. Resolve the product repository and its `scripts/splinter.ps1` from the project instructions or conversation. This source layout keeps the owning documents under `../../docs/` relative to this directory. The [limited-version start guide](../../docs/agent-start.md) owns current prerequisites and entry steps; [delivery qualification](../../docs/verification/m0-delivery.md) distinguishes the completed M0 scope from unqualified native/OS failure and installation scenarios.

Splinter owns workspace/storage and owned Editor lifetimes, durable independent results, and safe explicit integration/reclamation/discard. The agent uses existing file/Git tools, MCP, or Unity CLI for project work and judgment. Setup and handoff records, tool/mode selection, conflict resolution, generated-source merging, and game validation are agent procedures. Do not request a Splinter installer/binding framework, queue/automatic mode selector, or duplicate Unity authoring/test API to perform those tasks. The [scope plan](../../docs/plans/implementation-plan.md#프로그램과-에이전트의-책임) owns the boundary; current schemas and safety gates remain authoritative.

The product goal is for Splinter itself to provide parallel workspaces whether an external VCS is present or absent. VCS adapters protect Splinter's isolation, original/peer ownership, storage and memory efficiency from shared administration, registrations, locks, and VCS caches. Assess compatibility by actual Splinter workspace behavior and resource costs. Current modes are Git, plain-folder and Plastic compatibility. Read the [Plastic contract](../../docs/plastic-compatibility.md) and its current qualification before use; fake lifecycle evidence is not native support.

## Decide whether to use a workspace

| Evidence | Action |
| --- | --- |
| A saved workspace is already attached to this task | Resume that identity, even if the original Editor becomes available. |
| The user explicitly requests parallel/isolated work | Create a workspace within the registered project/home policy. |
| Another task owns the required project/Editor and this task needs independent work | Use Splinter if the existing project policy permits it; preserve the other task. |
| Independent work is unnecessary and the current path is suitable | Work directly. Create no Splinter snapshot/workspace/Editor and no Codex worktree. |
| Occupancy or ownership cannot be established | Keep it unknown. Check whether a supported isolated path exists; do not infer idle or take over an Editor. |

An open Editor alone does not prove another writer is active. `doctor` and workspace lists do not establish global Editor idleness. Do not create a workspace for every request, nest a Codex worktree inside Splinter, or change an existing task's baseline to follow the latest original HEAD.

## Discover the current contract

For a VCS-free project without VSS, read [managed project basis](../../docs/plain-project-basis.md) and [its qualification](../../docs/verification/plain-project-basis.md). Obtain explicit authorization for the source-project managed mount; the earlier Library-only authorization is insufficient. Save and normally stop the original Editor, use `project prepare --application none|unity`, then fix its returned basis id in `workspace ensure --source none --storage project-snapshot --project-basis <id>` with the same application/home. Existing source/Library data is renamed on the same NTFS volume without copying; the original path becomes a writable managed view. Original/A/B writes use separate ranges. Never delete an existing VCS administration directory to obtain None input. There is no VSS service/grant or Library graft on this route. Checkpoint preserves independent source payloads; full/selected original apply requires the exact stopped original and fixed plan/intent/digest. Legacy VSS None/Unity apply remains unsupported. Task release/discard removes only that task's common-basis reference. Reclaim all dependents and normally stop the original before explicit `project restore`; retain receipts/results and verify ordinary NTFS restoration. Do not mistake persisted `project status` for live mount health.

For a recorded Git project basis, use `--source git --storage git-snapshot` after reading [its contract](../../docs/git-project-basis.md) and [qualification](../../docs/verification/git-project-basis.md). This path reads fixed local commit/tree/blob objects and requires WinFsp, but no VSS service/grant. It requires a clean repository root at the requested commit, uses raw blobs without checkout conversion, and excludes ignored/private source files. An explicitly approved original Library transition can attach a fixed generated basis using `library prepare` and `workspace ensure --library-basis <returned-id>`; read [the lifecycle](../../docs/git-library-reference.md) and its qualification first. Save and normally stop the original Editor only when the user has authorized this transition/restoration. Keep the same project/home/basis id and restore the original ordinary Library after all dependent tasks are reclaimed. Keep storage with the same request identity. Missing/corrupt objects or changed owner refs must be preserved, never hydrated from the live original. Current native qualification is separate from previous VSS/Unity evidence. The registered VSS prerequisites below apply to `ntfs-snapshot`, not this Git route.

For Plastic recorded project content, explicitly select `--source plastic --storage plastic-snapshot --base <changeset@repository@server>` and read [the contract](../../docs/plastic-project-basis.md) and [current qualification](../../docs/verification/plastic-project-basis.md). Workers stay unregistered and deny administration roots; source and home must meet the same Plastic isolation policy. This route requires ordinary-user cm read access and WinFsp, without VSS service/grant. A fixed Library basis can use the same explicit prepare/id/dependent/restore lifecycle as Git. Retain server/cache and retransmission costs separately from range deltas. For authorized local full/selected original writes, read [Plastic original apply](../../docs/plastic-original-apply.md): stop the exact original Editor normally, checkpoint the worker, review `--target original --plan` (repeat `--path` for selection), then retain the same intent/digest/paths. Administration/loaded changeset/selector and affected locks are guarded; unknown or locked is refusal. Server history/registration/locks and managed Library are not modified. Preserve incomplete intents and their owned publication resources. Actual qualification is separate; the legacy VSS Plastic route and released-result import remain unsupported.

Use an absolute CLI path, an existing build, and ordinary Windows privileges:

```powershell
pwsh -File <product-root>/scripts/splinter.ps1 capabilities
pwsh -File <product-root>/scripts/splinter.ps1 schema workspace.ensure
pwsh -File <product-root>/scripts/splinter.ps1 storage service status
```

`capabilities` and `schema <command>` are the authority for available commands, inputs, effects, and retry policy. Read a command's schema before its first use. `doctor --project <source>` diagnoses prerequisites but is not an execution/ownership/Editor-ready certificate. A missing build, service, or project/home grant needs its stated setup action; ordinary workspace commands do not elevate or install a helper automatically. Never elevate the entire CLI, Git, or Unity to bypass that result.

Select `--source git` for supported clean Git input, `--source none` for a registered folder without VCS administration, or `--source plastic` for an exact regular/static Plastic original. Plastic workers are unregistered file workspaces; use a short home outside all VCS administration/Plastic registrations. Plastic defaults to snapshot basis and Unity application, excludes and denies administration roots, and preserves file results. The recorded route's original apply follows the preceding contract; the legacy VSS route rejects integration. Do not run Plastic checkout/update/check-in or initialize VCS in a worker. There is no failure fallback. `none` defaults to the captured `snapshot` basis and application `none`; add `--application unity` on the first request only for Unity work. Keep source/application selection and basis with project/key/id/home/digest in every handoff. Current workspace state is v2; preserve/reject earlier state without migration. Read the [plain-folder contract](../../docs/vcs-independent-workspaces.md) and its native qualification before use. For editorless plain folders, read the [original-apply contract](../../docs/plain-folder-original-apply.md): request `workspace integrate --target original --plan`, review conflicts/digest, then use an explicit stable `--intent-key` and `--expected-plan-digest` only within authorized original writes. Preserve these fields in handoffs. Pending apply blocks reclamation; do not change its intent or sweep publication files. Completed receipts freeze that result. Plain-folder Unity apply and released-result import are unsupported. Actual plain-folder product qualification is one small editorless Windows x64 NTFS A/B run; the current service must include the pending-apply authorization guard. Keep recorded Plastic, large-input and OS-failure qualification separate.

Use the registered absolute source and home, the selected supported input and basis, and a stable key for this task. Do not invent a new home to bypass an existing registration or ownership failure. Supported input and native prerequisites are in [NTFS workspaces](../../docs/ntfs-workspaces.md); current support is a small Windows x64 fixture alpha, not general NTFS/ARM/OS-failure qualification.

The current private journal uses overlay v2, explicit OFF SJR2 and default ON SJL4/root v4 for new journals with raw or bounded Brotli ranges; independent development candidates are v3/SJR2. Preserve and reject earlier formats without migration or a fallback reader. See the [compression contract](../../docs/journal-payload-compression.md), [current-state contract](../../docs/latest-only-journal.md) and their separate qualifications; do not carry the earlier Plastic/Unity qualification into SJL4 or claim whole-project space/RAM savings. An Editor stop alone does not freeze the journal: use the normal checkpoint/seal and unmount guards, retaining a changed-input failure and the same task.

## Create, use, and resume the same task

```powershell
pwsh -File <product-root>/scripts/splinter.ps1 workspace ensure --project <source> --key <task-key> --base <base-ref> --storage ntfs-snapshot --home <registered-home>
```

Read the JSON response, including `status`, `terminal`, `error`, `retryDisposition`, and `nextActions`. Exit code alone is insufficient. `Accepted` is pending; poll the supplied next action. A failure is not a usable workspace or completed cleanup.

When ensure succeeds, retain these values from the request/response in the task's conversation or handoff:

- Absolute product CLI, source project, registered home, stable key, base ref, and any explicitly supplied storage/budget options.
- Workspace ID, `data.workspace.path`, `data.controlPath`, request digest, base revision, and snapshot identity when reported.
- Any pending operation's exact input and next action. For discard, retain the intent key and expected request digest.

Use **the returned `data.workspace.path`** as the working directory and absolute file-tool target for editing, Git, and project tools. Do not substitute the source path or assume `cd` changes other tools' roots. The returned path does not grant a tool sandbox permission: report a real access failure and preserve the task rather than falling back to edits in the original.

For a later CLI invocation or continued conversation, use the saved absolute CLI/home and the **same ensure request**. Confirm the returned ID, root, request digest, base revision, and snapshot still match before writing. Do not select a global "last workspace", create a fresh key to escape an error, or silently return to the original.

Use one writer for a task path. Short CLI locks and provider process identity do not constitute an exclusive Codex-conversation lock for arbitrary file edits. If the handoff is ambiguous or another writer may still be active, establish which task may write before editing; automatic session binding and stale-writer exclusion are not provided by this skill.

Optional `workspace compact --optimize-journal true` (also `--revert-base-ranges true`) selects [current-state ON storage](../../docs/latest-only-journal.md). Subsequent acknowledged writes reclaim superseded private payload/metadata, keeping the immutable basis plus current ranges and one pending redo during publication. New task journals default to ON; existing journals, subsequent writers/reconnects and omitted compact options retain the selected mode, explicit false selects OFF, and conflicting pending options are rejected. Explicit stopped compaction additionally removes fixed-basis-equal ranges; running writes do not compare the basis. Compaction intent is v3 only; preserve old intents. This does not certify Unity preparation.

`workspace status --id <id> --home <home>` observes that task. When current source verification is needed, use `--verify-source`; reserve `--audit-source` for a full audit or damaged verification cache. Do not turn every status poll into a full source scan.

## Use an Editor only when needed

Read the `unity.start` and `unity.stop` schemas and [Unity workspaces](../../docs/alpha-workspaces.md). Pass the same workspace ID/home; target only its returned project and owned Editor. Observe actual readiness after pending responses. Readiness describes the owned Editor process; it does not certify compile or test success. For refresh, compilation, Play, tests, builds and capture, use the Editor UI or optional installed tools chosen by the agent.

Optional shared Library view/cache is a [development CLI](../../docs/development-cli.md) storage path. Follow [view lifetimes](../../docs/validation-workflow.md) and its owner/request identities. Do not hot-switch an Editor or treat a candidate as proof that the current worker is correct. Do not stop the original or a peer Editor to obtain access.

MCP and Unity CLI are optional external tools chosen by the agent from the user's installed tools. Splinter's workspace/storage, immutable bases, Library and unchanged-file references, range changes, independent results, safe original application and owned-resource lifetimes work without either tool. No external tool name, version or private API is a product prerequisite or gate. Installing, downgrading, patching or producing/running modified external packages is outside Splinter's scope.

Read the chosen tool's current help/schema and match its actual absolute project path and PID/start identity to the returned workspace and owned Editor before mutation. Use explicit per-call targets where available. Discovery omissions do not prove an Editor is absent; reload can change sessions/ports. Preserve request/job identities and detailed results, inspect uncertain execution, and never automatically repeat writes or tests on timeout. Existing [UH](../../docs/verification/unity-cli-handoff.md) and [UP](../../docs/verification/unity-parallel-editor-validation.md) records describe their historical inputs, not required versions or current universal compatibility.

Splinter does not issue compile requests or maintain a compile-success claim. Use `workspace checkpoint` to preserve stable source, including editorless or failed-compilation work. Independent-result, journal-seal, owned-resource and target/dirty guards remain authoritative. Refresh, compilation, scene/asset editing, Play, tests, builds, capture and result judgment belong to the agent and whichever external tool or Editor UI it chooses.

## Check remaining MCP connections after an Editor stops

Only when an external tool is in use, check known remaining original/peer connections and their project/PID/start identities before further external requests after an Editor stops. Preserve healthy servers and unrelated Editors. An external package shutting down its own shared server is that tool's and the agent's responsibility; Splinter does not manipulate its callbacks, run-state files or server lifecycle. Follow the tool's existing authorized recovery procedure, preserve uncertain requests and report unresolved connections separately from Splinter reclamation. Do not automatically change packages/global settings or generate modified tools for recovery. If no external work remains, no server restart is needed. See [the optional tool boundary](../../docs/unity-mcp-response-recovery.md#editor-종료-후-남은-mcp-연결-확인).

## Match termination to the user's intent

| Intent | Operation |
| --- | --- |
| Continue later / keep the current work | Retain the same workspace and identity; stop its Editor only when needed. Conversation/Editor end is not release or discard. |
| Preserve a result without applying it | Use the applicable independent-result path, then explicit `workspace release` if cleanup was requested. |
| Apply the result | Use external validation as needed, checkpoint, and explicit integration to the requested target, checking current original/dirty/conflict guards; then release when requested. A commit request alone does not authorize integration or remote push. |
| Throw away this workspace's work | Use explicit `workspace discard` with the exact ID, intent key, expected digest, and home. A successful validation/checkpoint is not required. |

Use the same checkpoint/integrate/release path in [Unity workspaces](../../docs/alpha-workspaces.md) for Editor-backed and editorless workers. Checkpoint creates an independent result without claiming compile success. Do not call ordinary release before satisfying its result-preservation gate. Candidate/source/cache dependencies must be handled explicitly, not cascaded away.

```powershell
pwsh -File <product-root>/scripts/splinter.ps1 schema workspace.discard
pwsh -File <product-root>/scripts/splinter.ps1 workspace discard --id <id> --intent-key <stable-discard-intent> --expected-request-digest <saved-digest> --home <home>
```

An unambiguous existing discard instruction is sufficient authorization; do not ask for it again. An explicit request to clean up the known workspaces/Editors of this task also authorizes their applicable preserve/release/cleanup sequence, including all known A/B targets. Multiple known owned targets do not make that instruction ambiguous. Ask only when intended targets or preserve/discard semantics remain unresolved. A tool refusal does not revoke human authorization: finish preservation and verify a permitted cleanup path; retain ownership/dirty/foreign-resource guards and report a remaining technical or approval-review block accurately. Discard removes only this task's owned payload/resources and leaves a receipt. It does not revert already integrated original changes, erase independent result refs, or run repository GC. Detailed intent/recovery/dependency guards are in [workspace discard](../../docs/workspace-discard.md).

## Handle a failed or incomplete operation

Use structured error codes, retry policy, and the exact supplied next action. Retain evidence and the failed task. Correct request errors explicitly; retry only when the reported condition permits it, keeping the same identity/intent. Do not add compatibility fallbacks, delete control files, change permissions, or start a new task to conceal an error.

`reclaim-pending` means results are preserved but physical snapshot reclamation is still blocked. Only an explicit retry of the same task/home may reclaim. Shared/unknown resources are not zero. Never sweep another workspace's snapshots or report `release`/`discard` as complete while the operation is failed or waiting.

Report the chosen direct/isolated path, retained task identity, actual operation outcome, and any pending action. Qualify the evidence you observed; do not promote fixture-only or unrun Editor/installation scenarios to general support.
