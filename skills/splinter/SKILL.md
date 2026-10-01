---
name: splinter
description: Use Splinter to create or resume an isolated workspace when the user requests parallel work or another task makes independent work necessary, and to preserve, integrate, or explicitly discard that task. Choose direct work when isolation is unnecessary. Use the existing CLI and the task's saved identity rather than a runner or automatic conversation binding.
---

# Splinter workspace use

This is the source skill shipped in the Splinter repository. It can be read directly; project/global installation and automatic Codex discovery are not implemented. Resolve the product repository and its `scripts/splinter.ps1` from the project instructions or conversation. This source layout keeps the owning documents under `../../docs/` relative to this directory.

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

Use the registered absolute source and home, clean supported Git input, explicit base ref, and a stable key for this task. Do not invent a new home to bypass an existing registration or ownership failure. Supported input and native prerequisites are in [NTFS workspaces](../../docs/ntfs-workspaces.md); current support is a small Windows x64 fixture alpha, not general NTFS/ARM/OS-failure qualification.

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

Read the `unity.start`, `unity.refresh`, and `unity.stop` schemas and [Unity workspaces](../../docs/alpha-workspaces.md). Pass the same workspace ID/home; target only its returned project and owned Editor. Observe actual readiness and current validation evidence after pending responses. Request acceptance is not compile success, and older validation does not certify later edits.

If using the explicit shared validation view/cache instead of an independent Editor, follow [validation workflow](../../docs/validation-workflow.md) and its owner/request identities. Do not hot-switch an Editor or treat a candidate's validation as the current worker's success. Do not stop the original or a peer Editor to obtain access.

## Match termination to the user's intent

| Intent | Operation |
| --- | --- |
| Continue later / keep the current work | Retain the same workspace and identity; stop its Editor only when needed. Conversation/Editor end is not release or discard. |
| Preserve a result without applying it | Use the applicable independent-result path, then explicit `workspace release` if cleanup was requested. |
| Apply the result | Use checkpoint/current validation and explicit integration to the requested target, checking current original/dirty/conflict guards; then release when requested. A commit request alone does not authorize integration or remote push. |
| Throw away this workspace's work | Use explicit `workspace discard` with the exact ID, intent key, expected digest, and home. A successful validation/checkpoint is not required. |

For a validated worker, the checkpoint/integrate/release path is in [Unity workspaces](../../docs/alpha-workspaces.md). For an editorless worker, `validation` action `preserve-worker` can create an independent result without granting compile success; use its schema and [validation workflow](../../docs/validation-workflow.md). Do not call ordinary release before satisfying its result-preservation gate. Candidate/source/cache dependencies must be handled explicitly, not cascaded away.

```powershell
pwsh -File <product-root>/scripts/splinter.ps1 schema workspace.discard
pwsh -File <product-root>/scripts/splinter.ps1 workspace discard --id <id> --intent-key <stable-discard-intent> --expected-request-digest <saved-digest> --home <home>
```

An unambiguous existing discard instruction is sufficient authorization; do not ask for it again. Ambiguous "clean up" or multiple possible targets needs clarification. Discard removes only this task's owned payload/resources and leaves a receipt. It does not revert already integrated original changes, erase independent result refs, or run repository GC. Detailed intent/recovery/dependency guards are in [workspace discard](../../docs/workspace-discard.md).

## Handle a failed or incomplete operation

Use structured error codes, retry policy, and the exact supplied next action. Retain evidence and the failed task. Correct request errors explicitly; retry only when the reported condition permits it, keeping the same identity/intent. Do not add compatibility fallbacks, delete control files, change permissions, or start a new task to conceal an error.

`reclaim-pending` means results are preserved but physical snapshot reclamation is still blocked. Only an explicit retry of the same task/home may reclaim. Shared/unknown resources are not zero. Never sweep another workspace's snapshots or report `release`/`discard` as complete while the operation is failed or waiting.

Report the chosen direct/isolated path, retained task identity, actual operation outcome, and any pending action. Qualify the evidence you observed; do not promote fixture-only or unrun Editor/installation scenarios to general support.
