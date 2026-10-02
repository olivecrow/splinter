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

Use an absolute CLI path, an existing build, and ordinary Windows privileges:

```powershell
pwsh -File <product-root>/scripts/splinter.ps1 capabilities
pwsh -File <product-root>/scripts/splinter.ps1 schema workspace.ensure
pwsh -File <product-root>/scripts/splinter.ps1 storage service status
```

`capabilities` and `schema <command>` are the authority for available commands, inputs, effects, and retry policy. Read a command's schema before its first use. `doctor --project <source>` diagnoses prerequisites but is not an execution/ownership/Editor-ready certificate. A missing build, service, or project/home grant needs its stated setup action; ordinary workspace commands do not elevate or install a helper automatically. Never elevate the entire CLI, Git, or Unity to bypass that result.

Select `--source git` for supported clean Git input, `--source none` for a registered folder without VCS administration, or `--source plastic` for an exact regular/static Plastic original. Plastic workers are unregistered file workspaces; use a registered short home outside all VCS administration/Plastic registrations. Plastic defaults to snapshot basis and Unity application, excludes and denies administration roots, preserves file results, and rejects original integration. Do not run Plastic checkout/update/check-in or initialize VCS in a worker. There is no failure fallback. `none` defaults to the captured `snapshot` basis and application `none`; add `--application unity` on the first request only for Unity work. Keep source/application selection and basis with project/key/id/home/digest in every handoff. Current workspace state is v2; preserve/reject earlier state without migration. Read the [plain-folder contract](../../docs/vcs-independent-workspaces.md) and its native qualification before use. For editorless plain folders, read the [original-apply contract](../../docs/plain-folder-original-apply.md): request `workspace integrate --target original --plan`, review conflicts/digest, then use an explicit stable `--intent-key` and `--expected-plan-digest` only within authorized original writes. Preserve these fields in handoffs. Pending apply blocks reclamation; do not change its intent or sweep publication files. Completed receipts freeze that result. Unity application and released-result import are unsupported. Actual product qualification is one small editorless Windows x64 NTFS A/B run; the current service must include the pending-apply authorization guard. Keep Unity/Plastic, large-input and OS-failure qualification separate.

Use the registered absolute source and home, the selected supported input and basis, and a stable key for this task. Do not invent a new home to bypass an existing registration or ownership failure. Supported input and native prerequisites are in [NTFS workspaces](../../docs/ntfs-workspaces.md); current support is a small Windows x64 fixture alpha, not general NTFS/ARM/OS-failure qualification.

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

`workspace status --id <id> --home <home>` observes that task. When current source verification is needed, use `--verify-source`; reserve `--audit-source` for a full audit or damaged verification cache. Do not turn every status poll into a full source scan.

## Use an Editor only when needed

Read the `unity.start` and `unity.stop` schemas and [Unity workspaces](../../docs/alpha-workspaces.md). Pass the same workspace ID/home; target only its returned project and owned Editor. Observe actual readiness after pending responses. Readiness describes the owned Editor process; it does not certify compile or test success. Use the chosen MCP or Unity CLI for refresh, compilation, Play, tests, builds, and capture.

Optional shared Library view/cache is a [development CLI](../../docs/development-cli.md) storage path. Follow [view lifetimes](../../docs/validation-workflow.md) and its owner/request identities. Do not hot-switch an Editor or treat a candidate as proof that the current worker is correct. Do not stop the original or a peer Editor to obtain access.

For external MCP/Unity CLI work, explicitly match the tool's selected project to the returned workspace path before mutation. Unity CLI exact-path handoff is [qualified on one small T: Plastic A/B input](../../docs/verification/unity-cli-handoff.md), with discovery omissions and initial eval timeouts retained. The current Coplay worker procedure uses a dedicated Splinter bridge and does not start its own server; MCP handoff remains unqualified. Use the [bounded handoff plan](../../docs/plans/session-workflow-and-installation.md#외부-도구-인계의-한정-범위) for a new tool/input. Do not stop a shared/original server to acquire access or bypass tool permissions.

With the installed Unity CLI, check version/help and the existing Pipeline, then pass the exact returned path to every `command --project-path <workspace>`. Read the selected Editor's command catalog/schema first. Match `data.target.projectPath` exactly (normalized path comparison) and a read-only query's actual project/PID to Splinter's workspace and `data.application.processId` before mutation. In CLI `1.0.0-beta.5`, `status`/`pipeline list` can omit a ready worker while exact-path `command` connects; the status filter is a substring match. Do not treat that omission as absence or install/upgrade packages based on it. Keep port selection with the CLI: reload can change the port while the workspace and PID remain the same.

Use external `recompile` and `recompile_status` only after discovering their current schema; request success is not compile completion. Verify actual loaded code/results and nested success separately. Request large start-time ticks as a C# string (`Ticks.ToString()`) if needed, and never print authentication tokens or full command lines. Preserve timeout evidence. The qualification used one same-target retry of read-only eval after the specific timeout; this is not authorization to repeat writes, scene changes, or compilation automatically. After Splinter's normal stop of A, verify B's actual connection and follow-up work before collecting B. The [Unity procedure](../../docs/alpha-workspaces.md#unity-cli의-정확한-대상-선택) gives the bounded command examples.

Splinter does not issue compile requests or maintain a current compile-success claim. Use `workspace checkpoint` to preserve stable source, including editorless or failed-compilation work. It retains independent-result, journal-seal, resource, and target guards; the agent decides whether external validation is sufficient before integration. Scene/asset editing, Play, tests, builds, capture, and evaluation belong to the chosen external tools and the agent, not a new Splinter command family.

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

An unambiguous existing discard instruction is sufficient authorization; do not ask for it again. Ambiguous "clean up" or multiple possible targets needs clarification. Discard removes only this task's owned payload/resources and leaves a receipt. It does not revert already integrated original changes, erase independent result refs, or run repository GC. Detailed intent/recovery/dependency guards are in [workspace discard](../../docs/workspace-discard.md).

## Handle a failed or incomplete operation

Use structured error codes, retry policy, and the exact supplied next action. Retain evidence and the failed task. Correct request errors explicitly; retry only when the reported condition permits it, keeping the same identity/intent. Do not add compatibility fallbacks, delete control files, change permissions, or start a new task to conceal an error.

`reclaim-pending` means results are preserved but physical snapshot reclamation is still blocked. Only an explicit retry of the same task/home may reclaim. Shared/unknown resources are not zero. Never sweep another workspace's snapshots or report `release`/`discard` as complete while the operation is failed or waiting.

Report the chosen direct/isolated path, retained task identity, actual operation outcome, and any pending action. Qualify the evidence you observed; do not promote fixture-only or unrun Editor/installation scenarios to general support.
