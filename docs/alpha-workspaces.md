<a id="병렬-unity-작업공간과-editor-수명"></a>
# Parallel Unity workspaces and Editor lifetime

[한국어](ko/alpha-workspaces.md)

The product manages NTFS workspaces and their Editor lifetime. Creation, resumption, range storage, independent results, integration, reclamation, and discard follow the [NTFS contract](ntfs-workspaces.md). Library views are separated into the [development CLI](development-cli.md).

<a id="에이전트의-기본-흐름"></a>
## Default agent workflow

Invoke the built product's `scripts/splinter.ps1` by absolute path, using the registered project/home, explicit base, and stable task key. Home lives outside the original project; all workers share the same registered home.

```text
splinter workspace ensure --project <Unity repository> --key <task key> --storage ntfs-snapshot --base development --editor --home <registered home>
splinter workspace status --id <returned id> --home <registered home>
```

Edit at `data.workspace.path` and make external MCP/Unity CLI projectRoot match this path. `application.running`/`ready` are execution/readiness observations, not compile/test success. The agent may analyze source and plan while the Editor prepares. Preserve the same key/id/home, basis, and request digest for the next conversation.

The product provides `unity start` and `unity stop`. Agents perform refresh, compile analysis, scene/asset editing, Play, tests, builds, and capture through Editor UI or selected external tools. There is no `unity refresh` or internal compile-success decision. Historical small T: Plastic CLI handoff in [UH](verification/unity-cli-handoff.md), and small Git C/S MCP editing/current-state tests in [UP](verification/unity-parallel-editor-validation.md), are records for specified inputs, not current tool-installation/version requirements.

<a id="unity-cli의-정확한-대상-선택"></a>
## Exact target selection with Unity CLI

MCP and Unity CLI are external tools selected by agents when needed. Splinter requires neither their installation nor a fixed version; work through Editor UI or existing file tools is also possible. Read the selected tool's current help/schema and user project settings before connecting. Do not install, downgrade, patch, or create/operate modified versions of specific packages.

Before editing, compare each external command's actual absolute project path, PID, and start time with `data.workspace.path` and owned-Editor identity. If Unity CLI provides exact project-path selection, pass the returned path on every request. If MCP provides a per-call target, use the exact target from current discovery. Session defaults or substring filters in lists are not exact-target evidence. Missing list entries do not mean the Editor is absent; do not guess or pin ports/sessions after reload.

Verify request acceptance, connection, compilation completion, and test results separately; preserve important request/job IDs and results. Do not repeat writes/tests solely because of timeout or unknown responses. On connection failure, preserve the task and ownership state and investigate through that tool's procedure. Do not fall back to the original, take over a server, or automatically replace packages. Agents also check connections to remaining targets after one Editor exits and before the next external request. See the [external-tool guide](unity-mcp-response-recovery.md) for detailed boundaries.

[UH](verification/unity-cli-handoff.md), [UP](verification/unity-parallel-editor-validation.md), [GL](verification/git-library-reference.md), [PPB](verification/plastic-project-basis.md), and [VR/MR](verification/vss-and-mcp-remediation.md) preserve historical Unity CLI commands/versions/Pipeline and fixed MCP candidate execution, failures, and retries. These records do not become current required tools or passes for other versions.

<a id="결과와-명시적-종료"></a>
## Results and explicit completion

```text
splinter workspace checkpoint --id <id> --message <result description to preserve> --home <home>
splinter workspace integrate --id <id> --target development --home <home>
splinter workspace release --id <id> --home <home>
```

Checkpoint also preserves work without compile success. It normally shuts down the owned Editor, checks zero child resources, then removes the owned bridge. It checks content equivalence before/after Git collection, the independent result ref, and journal seals for collection/unmount. The agent decides work acceptance; checkpoint itself does not claim compile/test success. Do not begin new edits in the same task after result collection.

Integrate checks the exact collected revision against current source, a clean requested target branch, and repository locks, then performs Git merge. It aborts only a conflicting merge started by this command and preserves results. Integration strategy, conflict resolution, and post-integration validation in the original Editor are the agent's responsibility. To preserve only, release without integrating.

Release retains the independent result, then reclaims the owned Editor/provider/mount/Git/delta and last owned snapshot. `reclaim-pending` means physical reclamation is waiting for an explicit retry of the same task. To abandon work, use [explicit discard](workspace-discard.md). Conversation/Editor termination and timeouts do not authorize application or discard.

<a id="실행-격리와-정상-종료"></a>
## Execution isolation and normal shutdown

The Unity adapter checks PID, start time, executable, token, project path, and Job Object children. The owned `Assets/__Splinter__` bridge provides only liveness, import/compile-in-progress observations, and normal shutdown; it does not run project functions. Play, unsaved scenes, and ongoing import/compile block normal shutdown. Even after the maximum 2-minute shutdown wait, no forced termination/reclamation occurs; the task and logs are preserved.

Splinter does not inspect or remove external-package initialization/shutdown callbacks, or manage servers or package-specific run-state files. Defects where an external tool stops its own shared server belong to that tool/agent procedure. Owned-Editor normal shutdown and result/resource guards remain without requiring external-connection success.

User settings, licenses, CPU/GPU, and external services are shared. This is not a security sandbox. Use `workspace status --verify-source` for source-hash checks and `--audit-source` for a full audit. Ordinary status observes execution resources without reading file contents. [Representative usage](verification/representative-agent-workflow.md) preserves actual bounded execution evidence, failures/retries, and costs; it does not qualify new external-tool paths.
