---
name: splinter
description: Use Splinter to create or resume an isolated workspace when the user requests parallel work or another task makes independent work necessary, and to preserve, integrate, or explicitly discard that task. Choose direct work when isolation is unnecessary. Use the existing CLI and the task's saved identity rather than a runner or automatic conversation binding.
---

# Splinter workspace use

The user reduced SCAL verification on 2026-11-01. Read [the current validation limits](../../docs/verification/README.md) before scheduling large work. The retained 100k fixtures/providers are stopped; do not automatically resume 17GiB/full lifecycles/ABBA/the whole gate. Their obligations remain unqualified. Preserve exact state and distinguish the passed small Unity/focused tests from unproved large-project usability and small-input performance preservation. This instruction does not authorize release/discard of retained fixtures.

Fresh `checkpoint --release` requests collect once between full pre/post seals after normal stop/infrastructure removal. Keep the request key on recovery: a previously fixed generation remains immutable, while closure may collect a newer generation before reclaiming. A standalone release still collects current source. Git scoped-read validation is internal and needs no extra flag; it retains exact reader/ref/receipt guards and full publication validation. See [capture and closure](../../docs/repeated-original-apply.md).

Use [continuing-task results](../../docs/repeated-original-apply.md) and [qualification limits](../../docs/verification/README.md). Every checkpoint needs an explicit stable `--request-key`; retain `data.resultGeneration.id`. Intermediate captures keep the Editor and task usable. All three source modes publish fixed original files through `--target original --result <id>` with reviewed plan/intent/digest; a live original additionally needs the exact external cooperation permit. Save named dirty scenes AND assets, refuse unnamed scenes/Play/uncertain saves, own and release only this request's import/reload deferrals, inspect new dirty state, then safely refresh/reload and verify loaded values. Do not repeat uncertain writes/counter changes blindly. Current candidate formats are task v4, managed basis v3 and product overlay v3. SCAL-01–08 qualification remains in progress; historical RI/MX/GCO evidence is candidate-specific. Preserve/refuse old state and use its matching binary; no migration/reclamation.

This is the source skill shipped in the Splinter repository. It can be read directly; project/global installation and automatic Codex discovery are not implemented. Resolve the product repository and its `scripts/splinter.ps1` from the project instructions or conversation. This source layout keeps the owning documents under `../../docs/` relative to this directory. The [limited-version start guide](../../docs/agent-start.md) owns current prerequisites and entry steps; delivery qualification distinguishes the completed M0 scope from unqualified native/OS failure and installation scenarios.

Splinter owns workspace/storage and owned Editor lifetimes, durable independent results, and safe explicit integration/reclamation/discard. The agent uses existing file/Git tools, MCP, or Unity CLI for project work and judgment. Setup and handoff records, tool/mode selection, conflict resolution, generated-source merging, and game validation are agent procedures. Do not request a Splinter installer/binding framework, queue/automatic mode selector, or duplicate Unity authoring/test API to perform those tasks. The [scope plan](../../docs/plans/implementation-plan.md#agent-procedures-and-exclusions) owns the boundary; current schemas and safety gates remain authoritative.

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

For a VCS-free project without VSS, read [managed project basis](../../docs/plain-project-basis.md) and [qualification limits](../../docs/verification/README.md). Obtain explicit authorization for the source-project managed mount; the earlier Library-only authorization is insufficient. Save and normally stop the original Editor, use `project prepare --application none|unity`, then fix its returned basis id in `workspace ensure --source none --storage project-snapshot --project-basis <id>` with the same application/home. Existing source/Library data is renamed on the same NTFS volume without copying; the original path becomes a writable managed view. Original/A/B writes use separate ranges. Never delete an existing VCS administration directory to obtain None input. There is no VSS service/grant or Library graft on this route. Checkpoint preserves independent source payloads; full/selected original apply requires a fixed result/plan/intent/digest and either a stopped original reservation or the exact live-original external cooperation permit. Legacy VSS None/Unity apply remains unsupported. Task release/discard removes only that task's common-basis reference. Reclaim all dependents and normally stop the original before explicit `project restore`; retain receipts/results and verify ordinary NTFS restoration. Do not mistake persisted `project status` for live mount health.

For a recorded Git project basis, use `--source git --storage git-snapshot` after reading [its contract](../../docs/git-project-basis.md) and qualification. This path reads fixed local commit/tree/blob objects and requires WinFsp, but no VSS service/grant. It requires a clean repository root at the requested commit, uses raw blobs without checkout conversion, and excludes ignored/private source files. An explicitly approved original Library transition can attach a fixed generated basis using `library prepare` and `workspace ensure --library-basis <returned-id>`; read [the lifecycle](../../docs/git-library-reference.md) and [qualification limits](../../docs/verification/README.md) first. Save and normally stop the original Editor only when the user has authorized this transition/restoration. Keep the same project/home/basis id and restore the original ordinary Library after all dependent tasks are reclaimed. Keep storage with the same request identity. Missing/corrupt objects or changed owner refs must be preserved, never hydrated from the live original. Current native qualification is separate from previous VSS/Unity evidence.

For Plastic recorded project content, explicitly select `--source plastic --storage plastic-snapshot --base <changeset@repository@server>` and read [the contract](../../docs/plastic-project-basis.md) and current qualification. Workers stay unregistered and deny administration roots; source and home must meet the same Plastic isolation policy. This route requires ordinary-user cm read access and WinFsp, without VSS service/grant. A fixed Library basis can use the same explicit prepare/id/dependent/restore lifecycle as Git. Retain server/cache and retransmission costs separately from range deltas. For authorized local full/selected original writes, read [Plastic original apply](../../docs/plastic-original-apply.md): save the worker, capture a stable request-keyed result, save/cooperate with the original, review `--target original --result <id> --plan` (repeat `--path` for selection), then retain the same intent/digest/paths. Administration/loaded changeset/selector and affected locks are guarded; unknown or locked is refusal. Server history/registration/locks and managed Library are not modified. Preserve incomplete intents and their owned publication resources. Actual qualification is separate; the legacy VSS Plastic route and released-result import remain unsupported.

Use an absolute CLI path, an existing build, and ordinary Windows privileges:

```powershell
pwsh -File <product-root>/scripts/splinter.ps1 capabilities
pwsh -File <product-root>/scripts/splinter.ps1 schema workspace.ensure
```

`capabilities` and `schema <command>` are the authority for available commands, inputs, effects, and retry policy. Read a command's schema before its first use. `doctor --project <source>` diagnoses prerequisites but is not an execution/ownership/Editor-ready certificate. A missing build or WinFsp installation needs its stated setup action; ordinary workspace commands do not elevate or install a helper automatically. Never elevate the entire CLI, Git, or Unity to bypass that result.

Select `--source git --storage git-snapshot` for supported clean Git input, `--source plastic --storage plastic-snapshot --base <changeset@repository@server>` for recorded Plastic input, or `--source none --storage project-snapshot --project-basis <id>` after explicit [project preparation](../../docs/plain-project-basis.md). No VSS service or grant is needed. Keep application/source/basis and project/key/id/home/digest fixed across handoffs. Plain managed projects support full/selected original publication for application none/unity under the exact mount, Editor, plan/intent/digest guards. Do not initialize VCS, check in source, or bypass errors by changing storage mode. Current candidate task state is v4, managed basis v3 and product overlay v3; legacy task/basis/overlay and VSS state is preserved/rejected under the retirement decision.

Use the absolute source and home, the selected supported input and basis, and a stable key for this task. Do not invent a new home to bypass an existing registration or ownership failure. Supported input and native prerequisites are in [NTFS workspaces](../../docs/ntfs-workspaces.md); current support is a small Windows x64 fixture alpha, not general NTFS/ARM/OS-failure qualification.

The [scalable-storage candidate](../../docs/scalable-storage.md) uses product overlay v3, segmented raw SJR2 under SJS1 control, paged live/capture/result indices and immutable shared content/ranges. Processing pages are at most64KiB; the process-wide accounted metadata cache is32MiB. No project-wide journal/entry/path/manifest quota or `--max-journal-bytes` remains. Actual filesystem capacity/constraints and corruption/ownership guards remain. Accepted source-chain seals are distinct from physical backing/hash/page/inventory proofs; adoption/physical reclamation cannot erase later edits. Explicit development candidate/view formats stay separate. An Editor stop alone does not freeze source: use current checkpoint/seal/unmount guards and the same task.

## Create, use, and resume the same task

```powershell
pwsh -File <product-root>/scripts/splinter.ps1 workspace ensure --project <source> --key <task-key> --base <base-ref> --storage git-snapshot --home <home>
```

Read the JSON response, including `status`, `terminal`, `error`, `retryDisposition`, and `nextActions`. Exit code alone is insufficient. `Accepted` is pending; poll the supplied next action. A failure is not a usable workspace or completed cleanup.

When ensure succeeds, retain these values from the request/response in the task's conversation or handoff:

- Absolute product CLI, source project, home, stable key, base ref, and any explicitly supplied storage/budget options.
- Workspace ID, `data.workspace.path`, `data.controlPath`, request digest, base revision, and snapshot identity when reported.
- Any pending operation's exact input and next action. For discard, retain the intent key and expected request digest.

Use **the returned `data.workspace.path`** as the working directory and absolute file-tool target for editing, Git, and project tools. Do not substitute the source path or assume `cd` changes other tools' roots. The returned path does not grant a tool sandbox permission: report a real access failure and preserve the task rather than falling back to edits in the original.

For a later CLI invocation or continued conversation, use the saved absolute CLI/home and the **same ensure request**. Confirm the returned ID, root, request digest, base revision, and snapshot still match before writing. Do not select a global "last workspace", create a fresh key to escape an error, or silently return to the original.

Use one writer for a task path. Short CLI locks and provider process identity do not constitute an exclusive Codex-conversation lock for arbitrary file edits. If the handoff is ambiguous or another writer may still be active, establish which task may write before editing; automatic session binding and stale-writer exclusion are not provided by this skill.

For independent Git tasks, the agent can overlap two `workspace ensure` calls with different stable keys and the same fixed project/home/base/Library basis. Prepare that Library once before dispatch; use one writer per returned task path. Check each response independently and wait for both to succeed before treating the pair as ready. A failed or lost response preserves that task: inspect its supplied next action and resume the same identity; a timeout alone never authorizes a duplicate write. One failure does not authorize releasing the peer. This is agent orchestration of existing commands, not a product queue or automatic mode. The bounded parallel preparation verification uses small Editor-free A/B; it does not measure full Unity readiness.

Read the current `workspace.compact` schema before selecting compaction options. The new product path uses segmented overlay v3; old append/bounded/latest format and performance documents remain evidence for their matching binaries or explicit development fixtures. Never change mode/version/home/key to bypass pending work. Metadata collection uses `workspace maintain`, not journal compaction. Both preserve independent results, native identity and reader protection.

`workspace status --id <id> --home <home>` observes that task. When current source verification is needed, use `--verify-source`; reserve `--audit-source` for a full audit or damaged verification cache. Do not turn every status poll into a full source scan.

## Read large collections and recover metadata maintenance

Read `schema workspace.read` before paging results/applies/files/applied-paths/plans/selection/conflicts. Retain the exact root/cursor; previews are not the complete write set. A same-intent/digest apply retry may omit paths to recover its fixed selection; omitted paths on a new intent mean full scope. Git captures retain file/range results and a protected immutable basis; explicit branch integration materializes Git results only when needed.

Also inspect `workspace.read` collections `selection`, `maintenance-residuals`, `deferred-reclamation` and `preserved-scratch`. Selection binds `--plan-id`. Deferred scopes are `content|ranges|content-references|journal-packing|journal-indices|read-only-replay`; `content-references` is a store scope, not a collection. Original publication scratch uses `preserved-scratch` with `--store-scope workspace-original`. Journal pages bind preserved artifact ownership to the exact overlay identity; actual lifecycle qualification remains pending. A returned `journal-indices` request key selects its preserved native inventory for paging. Preserve referenced/deferred data and the exact owner/scope/root-bound cursor; these pages never authorize force deletion.

Read `schema workspace.maintain`; use `workspace maintain --id <id> --request-key <key> --scope workspace|shared|content|ranges|all --home <home>`. Keep the exact key/scope from `nextActions` after space/I/O failure. Pending maintenance blocks a different request/new mutation; normal Editor stop is allowed. Completed requests return receipts only. Status observes summaries without collection. Protected unknown residue is retained and queried with `workspace read --collection maintenance-residuals --request-key <key> --store-scope <store>` and a root-bound cursor. Never delete unknowns or change identities to bypass recovery. Native all-target handle costs can grow separately from the metadata cache. See [current storage](../../docs/scalable-storage.md) and qualification; do not claim constant disk/time or general RAM/speed gains.

## Use an Editor only when needed

Read the `unity.start` and `unity.stop` schemas and [Unity workspaces](../../docs/alpha-workspaces.md). Pass the same workspace ID/home; target only its returned project and owned Editor. Observe actual readiness after pending responses. Readiness describes the owned Editor process; it does not certify compile or test success. For refresh, compilation, Play, tests, builds and capture, use the Editor UI or optional installed tools chosen by the agent.

Optional source view/cache is a [development CLI](../../docs/development-cli.md) storage path. This development path accepts only a Git worker without a Library graft; ordinary task Library sharing uses `--library-basis`. Use `scripts/splinter-dev.ps1 schema view.execute` and `view --input <request.json> --home <home>`. Follow [view lifetimes](../../docs/view-workflow.md) and its owner/request identities. Do not hot-switch an Editor or treat a candidate as proof that the current worker is correct. Do not stop the original or a peer Editor to obtain access.

MCP and Unity CLI are optional external tools chosen by the agent from the user's installed tools. Splinter's workspace/storage, immutable bases, Library and unchanged-file references, range changes, independent results, safe original application and owned-resource lifetimes work without either tool. No external tool name, version or private API is a product prerequisite or gate. Installing, downgrading, patching or producing/running modified external packages is outside Splinter's scope.

Read the chosen tool's current help/schema and match its actual absolute project path and PID/start identity to the returned workspace and owned Editor before mutation. Use explicit per-call targets where available. Discovery omissions do not prove an Editor is absent; reload can change sessions/ports. Preserve request/job identities and detailed results, inspect uncertain execution, and never automatically repeat writes or tests on timeout. Existing UH and UP records describe their historical inputs, not required versions or current universal compatibility.

Use the returned project path as the external CLI working directory as well as its explicit per-call target. In RI-07, Unity CLI discovery omitted mounted A/B when run from the product repository despite correct descriptors/listeners; each task directory resolved the exact project/PID. The user-requested Window/Pipeline Stop Server → Start Server recovery retained Editor identities. This is an agent/tool procedure, not an internal Splinter server-control feature.

Splinter does not issue compile requests or maintain a compile-success claim. Use `workspace checkpoint` to preserve stable source, including editorless or failed-compilation work. Independent-result, journal-seal, owned-resource and target/dirty guards remain authoritative. Refresh, compilation, scene/asset editing, Play, tests, builds, capture and result judgment belong to the agent and whichever external tool or Editor UI it chooses.

## Check remaining MCP connections after an Editor stops

Only when an external tool is in use, check known remaining original/peer connections and their project/PID/start identities before further external requests after an Editor stops. Preserve healthy servers and unrelated Editors. An external package shutting down its own shared server is that tool's and the agent's responsibility; Splinter does not manipulate its callbacks, run-state files or server lifecycle. Follow the tool's existing authorized recovery procedure, preserve uncertain requests and report unresolved connections separately from Splinter reclamation. Do not automatically change packages/global settings or generate modified tools for recovery. If no external work remains, no server restart is needed. See [the optional tool boundary](../../docs/unity-mcp-response-recovery.md#checking-remaining-mcp-connections-after-editor-shutdown).

## Match termination to the user's intent

When preservation and reclamation are both authorized and no integration is needed between them, use `workspace checkpoint --id <id> --request-key <capture-key> --message <description> --release --home <home>` for a range-journal workspace. It normally stops its owned Editor and retains the final result while reclaiming resources. Reuse the same invocation only as documented; earlier checkpoints/retries are freshly verified. A separate `unity stop` immediately beforehand is unnecessary unless a stopped-state observation is needed. For final apply-before-release, externally save, normally stop only this task, checkpoint with a fresh key → integrate the explicit result → release. Intermediate checkpoint/publication retains the Editor and returns to editing. See LF-01–05.

| Intent | Operation |
| --- | --- |
| Continue later / keep the current work | Retain the same workspace and identity; stop its Editor only when needed. Conversation/Editor end is not release or discard. |
| Preserve a result without applying it | Use the applicable independent-result path. For a range-journal workspace, use `workspace checkpoint --release` when cleanup is also requested; otherwise retain the checkpoint and release only when requested. |
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
