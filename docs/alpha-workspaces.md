# Parallel Unity workspaces and Editor lifetime

Current task state is v4 and managed basis state is v3. [Repeated original application](repeated-original-apply.md) owns live immutable captures, explicit result selection, per-path applied history and external-tool live-Editor cooperation; historical RI qualification remains tied to its earlier candidate. Unsupported state is preserved/refused without migration.

On a same-workspace restart, identical owned bridge resources and their marker are reused after full ownership/content/path validation, preserving asset timestamps. The Editor-only `Splinter.Workspace.Editor` assembly has automatic references disabled. Marker version 2 binds the C# and assembly-definition hashes. Changed verified owned resources are updated. Previous markers and unknown or changed files are preserved and refused. Tokens and launch control remain fresh. A small native and mounted comparison observed one bridge compilation on first launch and no Bee actions on an unchanged restart. [Unity timing and scope](verification/parallel-workspace-performance.md)

The product manages NTFS workspaces and their Editor lifetime. Creation, resumption, range storage, independent results, integration, reclamation, and discard follow the [NTFS contract](ntfs-workspaces.md). Library views are separated into the [development CLI](development-cli.md).

<a id="에이전트의-기본-흐름"></a>
## Default agent workflow

Invoke the built product's `scripts/splinter.ps1` by absolute path, using the explicit project/home, explicit base, and stable task key. Home lives outside the original project; all workers share the same home.

```text
splinter workspace ensure --project <Unity repository> --key <task key> --storage git-snapshot --base development --editor --home <home>
splinter workspace status --id <returned id> --home <home>
```

Edit at `data.workspace.path` and make external MCP/Unity CLI projectRoot match this path. `application.running`/`ready` are execution/readiness observations, not compile/test success. The agent may analyze source and plan while the Editor prepares. Preserve the same key/id/home, basis, and request digest for the next conversation.

For another edit or review on the same task, an intermediate `workspace checkpoint` preserves the result and keeps the Editor available. When the task is finished and release is authorized, run `workspace checkpoint --release` with its stable request key. This command normally stops the owned Editor, preserves the independent result and reclaims the workspace. Calling `unity stop` separately first adds a source inspection for its response. A shared prepared Library can serve multiple workspaces until its explicit final restore.

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

The Windows bridge posts one close message to the visible, enabled, ownerless Unity main window on its own Editor thread. Unity handles the message after the managed update returns and can honor quit callbacks and cancellation. Ambiguous windows leave the Editor running. The bridge marks the Editor as exiting when Unity raises its quitting event. A refused request stays handled across script reloads. After resolving the reason, issue a fresh explicit stop. Check the signed native exit code in workspace status after a timeout. [Process exit receipts and recovery](storage-and-resources.md#processes-and-memory)

Before the first Editor launch of a range-journal workspace with an explicit `Library` graft, Splinter removes only ordinary top-level `Library/Bee/*.dag` compiled graph files from that workspace's private view. The Library ID/project association and separate original/workspace paths must match. All selected files pass path/reparse/type checks before any deletion, followed by a check before each deletion. A partial deletion can resume with the same task. Existing launch receipts skip this step; an intent without a receipt still refuses launch. Ordinary checkouts, non-Library references and whole-project bases do not select it.

The operation preserves `TundraBuildState.state`, `bee_backend.info`, graph JSON/payloads, response files, artifacts and nested files, and leaves the original and peers unchanged. Unity rebuilds its compiled graph through its own frontend; Splinter does not patch Unity, alter external-tool settings or manage tool servers. SA-05 verifies a tiny real Bee backend fixture: a missing compiled graph requests frontend work without executing its old action, and explicit baking succeeds. The fixture does not run the Unity frontend or Editor, establish removal of the observed 60-second ILPP delay, or measure full preparation-time savings.

Splinter does not inspect or remove external-package initialization/shutdown callbacks, or manage servers or package-specific run-state files. Defects where an external tool stops its own shared server belong to that tool/agent procedure. Owned-Editor normal shutdown and result/resource guards remain without requiring external-connection success.

User settings, licenses, CPU/GPU, and external services are shared. This is not a security sandbox. Use `workspace status --verify-source` for source-hash checks and `--audit-source` for a full audit. Ordinary status observes execution resources without reading file contents. Representative usage preserves actual bounded execution evidence, failures/retries, and costs; it does not qualify new external-tool paths.

Preserve the installed project Editor patch and the normal user identity. Ordinary product launches inherit the environment and do not select test preferences or reset consent. `-useTestPrefs` selects another shared preference/consent store, rather than a private store per workspace; it can display the Editor Software Terms again. Redirecting `USERPROFILE` can also redirect native license discovery and produce a license error despite a valid normal-user license. Neither is a supported workaround for shared settings. Do not alter/copy acceptance records, reactivate licenses, redirect known folders or delete global settings to make a trial pass. A new startup modal invalidates that trial; cancel only the owned Editor normally and inspect the launch configuration.

### Shared preferences and external validation

Unity 6000.3 saves overlay canvas state in the user-wide Preferences folder. Native serialization can delete/recreate its destination; a peer loading the same file during that interval can fail even though project and Library files are isolated. Splinter does not make those external writes atomic or sandbox user preferences.

For coordinated A/B validation, order Editor startup, refresh/domain reload, Play entry/exit and normal shutdown across the cooperating Editors, awaiting each transition's actual completion. Keep code work, storage operations and steady Play execution independent. This avoids overlapping known settings transitions; it does not cover uncoordinated Editors or manual window/settings activity. Apply the same ordering to both versions when measuring performance; earlier timing samples do not qualify the changed method.

Play entry needs its own `EnteredPlayMode` event and actual frame progress. Play exit needs the request-correlated `EnteredEditMode` event, `isPlaying == false`, `isPlayingOrWillChangePlaymode == false`, and settled import/compilation. Do not require a changed domain ID on exit: Unity can return to Edit mode in the same domain. A separately requested script reload still needs its own domain-change proof. Prepare a fresh fixture's normal project-local Search index in Edit mode before Play; after explicit reload, reacquire its lazily created Search database before awaiting readiness. Do not suppress Search exceptions or disable indexing to pass.

Prefer Unity CLI readiness and popup diagnostics before Computer Use. A missing Pipeline package or restricted process visibility can make `unity status` omit a running Editor; that response proves neither normal exit nor popup absence. Keep the chosen fixture/tool dependencies fixed across performance conditions.

A `Folder not found` dialog for a favorite is separate from consent, licensing and serialized settings-file errors. User-wide favorites may reference folders absent from a fresh trial. A controlled validation fixture can select one of its real folders in its own Project browser before reload/Play, including its serialized local selection before the hidden browser GUI is rebuilt. Preserve the shared favorites file; do not delete favorites or fabricate source folders to dismiss the dialog. This procedure is external validation setup, not a Splinter launch feature.

An installed Editor can launch a version-specific Licensing Client that a peer Editor then uses. If the client belongs to the first Editor's Windows job, stopping that job first can leave the peer blocked by `Connection Lost` even with valid entitlement. Check the actual client owner and dependency; for the observed A-owner/B-dependent case, stop B normally, confirm A remains responsive, then stop A. Do not terminate shared licensing helpers, reset the user environment, activate a license, or accept new terms to make the check pass. This ordered procedure does not qualify arbitrary independent shutdowns while another Editor depends on the same helper.

When both results are to be kept and both tasks released, start B's `workspace checkpoint --release` first. A caller that can confirm B's exact owned process group has exited and A still responds can then start A's `workspace checkpoint --release` while B finishes its storage work. A caller relying on command responses can wait for B's release receipt before checking A. Await both release receipts before restoring the shared Library. [Measured shutdown sequence](verification/parallel-workspace-performance.md#november-19-original-publication-and-workspace-reclamation)

`scripts/test-unity-shared-settings.ps1` is a finite optional diagnostic for the installed Unity 6000.3 Editor. It creates fresh builtin-only A/B projects, repeats overlapping real Play with ordered transitions, checks logs and normal exits, and retains generated output. It refuses existing scratch/output roots and requires no packages, profile overrides, installation or activation. Its C# fixture is nonce-gated and is not a product authoring API. For example:

```powershell
pwsh ./scripts/test-unity-shared-settings.ps1 -EditorPath '<installed Editor>/Unity.exe' -ScratchRoot C:/sp/settings-check -OutputPath ./artifacts/settings-check -Cycles 5
```

Supply `-ConsentValueNames` with existing Unity terms registry value names when requiring before/after consent fingerprints; an omitted list is reported as zero values checked. The diagnostic reads the existing local entitlement XML and records original file hashes without copying its contents. It compares license identity/rights and other license-file hashes while allowing normal issue/update timestamp and outer signature renewal; both Editors must also resolve their entitlement normally. Full license-byte equality is reported separately. It rejects damaged/unsupported identity documents and performs no license verification, activation or modification itself. This diagnostic is not a complete regression gate, a matched product lifecycle benchmark or proof of all external-editor interference cases.

On Windows, choose a short state root before creating a Unity workspace and check the resulting absolute package paths before launching its Editor. Unity 6000.3.27f2's Mono reader rejected an existing, correct 273-character PackageCache DLL path with `DirectoryNotFoundException`; its shorter path and extended-path spelling both read the expected bytes. Successful .NET/Win32 file access does not establish that the Editor's reader supports the same path. Keep path headroom for package contents, prove required DLL reads with the selected Editor runtime and treat package-cache I/O exceptions as validation failures. Preserve an existing task's path/home/id rather than moving its authority state to bypass an error. See [current measurement and limits](verification/parallel-workspace-performance.md).
