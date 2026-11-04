# Parallel Unity workspaces and Editor lifetime

Current task state is v4 and managed basis state is v3. [Repeated original application](repeated-original-apply.md) owns live immutable captures, explicit result selection, per-path applied history and external-tool live-Editor cooperation; historical RI qualification remains tied to its earlier candidate. Unsupported state is preserved/refused without migration.

On a same-workspace restart, identical owned bridge resources and their marker are reused after full ownership/content/path validation, preserving asset timestamps. The Editor-only `Splinter.Workspace.Editor` assembly has automatic references disabled. Marker version 2 binds the C# and assembly-definition hashes; changed verified owned resources are updated, while previous markers and unknown/changed files are preserved and refused. Tokens and launch control remain fresh. See startup profile follow-up for the tiny actual Unity assembly/lifetime check; full A/B startup time for this candidate is NotRun.

The product manages NTFS workspaces and their Editor lifetime. Creation, resumption, range storage, independent results, integration, reclamation, and discard follow the [NTFS contract](ntfs-workspaces.md). Library views are separated into the [development CLI](development-cli.md).

<a id="에이전트의-기본-흐름"></a>
## Default agent workflow

Invoke the built product's `scripts/splinter.ps1` by absolute path, using the explicit project/home, explicit base, and stable task key. Home lives outside the original project; all workers share the same home.

```text
splinter workspace ensure --project <Unity repository> --key <task key> --storage git-snapshot --base development --editor --home <home>
splinter workspace status --id <returned id> --home <home>
```

Edit at `data.workspace.path` and make external MCP/Unity CLI projectRoot match this path. `application.running`/`ready` are execution/readiness observations, not compile/test success. The agent may analyze source and plan while the Editor prepares. Preserve the same key/id/home, basis, and request digest for the next conversation.

The product provides `unity start` and `unity stop`. Agents perform refresh, compile analysis, scene/asset editing, Play, tests, builds, and capture through Editor UI or selected external tools. There is no `unity refresh` or internal compile-success decision. Historical small T: Plastic CLI handoff in UH, and small Git C/S MCP editing/current-state tests in UP, are records for specified inputs, not current tool-installation/version requirements.

<a id="unity-cli의-정확한-대상-선택"></a>
## Exact target selection with Unity CLI

MCP and Unity CLI are external tools selected by agents when needed. Splinter requires neither their installation nor a fixed version; work through Editor UI or existing file tools is also possible. Read the selected tool's current help/schema and user project settings before connecting. Do not install, downgrade, patch, or create/operate modified versions of specific packages.

Before editing, compare each external command's actual absolute project path, PID, and start time with `data.workspace.path` and owned-Editor identity. If Unity CLI provides exact project-path selection, pass the returned path on every request. If MCP provides a per-call target, use the exact target from current discovery. Session defaults or substring filters in lists are not exact-target evidence. Missing list entries do not mean the Editor is absent; do not guess or pin ports/sessions after reload.

Verify request acceptance, connection, compilation completion, and test results separately; preserve important request/job IDs and results. Do not repeat writes/tests solely because of timeout or unknown responses. On connection failure, preserve the task and ownership state and investigate through that tool's procedure. Do not fall back to the original, take over a server, or automatically replace packages. Agents also check connections to remaining targets after one Editor exits and before the next external request. See the [external-tool guide](unity-mcp-response-recovery.md) for detailed boundaries.

UH, UP, GL, PPB, and VR/MR preserve historical Unity CLI commands/versions/Pipeline and fixed MCP candidate execution, failures, and retries. These records do not become current required tools or passes for other versions.

<a id="결과와-명시적-종료"></a>
## Results and explicit completion

```text
splinter workspace checkpoint --id <id> --request-key <capture-key> --message <result description to preserve> --home <home>
splinter workspace integrate --id <id> --target original --result <result-id> --plan --home <home>
# Apply with the reviewed intent/digest and any exact live-original permit, then continue editing.
splinter workspace release --id <id> --home <home>
```

Checkpoint preserves immutable source generations while the owned Editor and later editing continue. Each stable request key returns its first result. Payload preservation runs outside the short durable source boundary. It does not claim compile/test success. Final processing saves externally, normally stops only this task, and uses the same capture/publication engine before explicit reclamation; final collection remains between full journal seals.

All three sources use `--target original --result <id>` for reviewed conditional file publication against per-path applied history. A running original requires exact external-tool save/import-deferral cooperation; the agent safely refreshes/reloads and checks loaded values afterward. Git HEAD/index/branch remain unchanged. Explicit branch-target merge separately retains clean-target and repository-lock guards. Conflicts preserve changes. To preserve only, release without integrating.

Release retains the independent result, then reclaims the owned Editor/provider/mount/Git/delta and last owned snapshot. `reclaim-pending` means physical reclamation is waiting for an explicit retry of the same task. To abandon work, use [explicit discard](workspace-discard.md). Conversation/Editor termination and timeouts do not authorize application or discard.

<a id="실행-격리와-정상-종료"></a>
## Execution isolation and normal shutdown

The Unity adapter checks PID, start time, executable, token, project path, and Job Object children. The owned `Assets/__Splinter__` bridge provides only liveness, import/compile-in-progress observations, and normal shutdown; it does not run project functions. Play, unsaved scenes, and ongoing import/compile block normal shutdown. Even after the maximum 2-minute shutdown wait, no forced termination/reclamation occurs; the task and logs are preserved.

Before the first Editor launch of a range-journal workspace with an explicit `Library` graft, Splinter removes only ordinary top-level `Library/Bee/*.dag` compiled graph files from that workspace's private view. The Library ID/project association and separate original/workspace paths must match. All selected files pass path/reparse/type checks before any deletion, followed by a check before each deletion. A partial deletion can resume with the same task. Existing launch receipts skip this step; an intent without a receipt still refuses launch. Ordinary checkouts, non-Library references and whole-project bases do not select it.

The operation preserves `TundraBuildState.state`, `bee_backend.info`, graph JSON/payloads, response files, artifacts and nested files, and leaves the original and peers unchanged. Unity rebuilds its compiled graph through its own frontend; Splinter does not patch Unity, alter external-tool settings or manage tool servers. SA-05 verifies a tiny real Bee backend fixture: a missing compiled graph requests frontend work without executing its old action, and explicit baking succeeds. The fixture does not run the Unity frontend or Editor, establish removal of the observed 60-second ILPP delay, or measure full preparation-time savings.

Splinter does not inspect or remove external-package initialization/shutdown callbacks, or manage servers or package-specific run-state files. Defects where an external tool stops its own shared server belong to that tool/agent procedure. Owned-Editor normal shutdown and result/resource guards remain without requiring external-connection success.

User settings, licenses, CPU/GPU, and external services are shared. This is not a security sandbox. Use `workspace status --verify-source` for source-hash checks and `--audit-source` for a full audit. Ordinary status observes execution resources without reading file contents. Representative usage preserves actual bounded execution evidence, failures/retries, and costs; it does not qualify new external-tool paths.
