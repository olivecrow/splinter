<a id="명시적-workspace-폐기"></a>
# Explicit workspace discard

[한국어](ko/workspace-discard.md)

The current ordinary-folder path for `--source none` and v2 state/service packages follows [VCS-free workspaces](vcs-independent-workspaces.md). Git paths and historical native qualification below do not qualify ordinary-folder support.

`workspace discard` explicitly abandons a fully prepared NTFS task worker without successful validation/checkpoint. Core/Runtime connect to actual Git/Windows providers and CLI. Real VSS discard requires a privilege-service package supporting this contract; service updates are separate administrator actions. [Discard verification](verification/workspace-discard.md) covers automated privilege boundaries, local Editor-free VSS A/B, same-intent resumption/duplicates; [representative usage](verification/representative-agent-workflow.md) covers Unity compile failure and normal stop/actual discard of an uncommitted worker. Native forced interruption and general support remain separate unexecuted scope.

```powershell
pwsh ./scripts/splinter.ps1 workspace status --id <id> --home <home>
pwsh ./scripts/splinter.ps1 workspace discard --id <id> --intent-key <stable-discard-key> --expected-request-digest <requestDigest> --home <home>
```

Identify the target through status/list ID, project/path, and `requestDigest`. Retry interruption/delay with the same key/digest/id/home. `Succeeded`/`discarded` means exact owned resources are absent; `Accepted`/`discard-reclaim-pending` waits for a shared VSS blocker after task payload removal. Existing preserving [release](ntfs-workspaces.md#reclamation-and-observations) continues to require independent results/seals.

<a id="입력과-소유권"></a>
## Inputs and ownership

`WorkspaceCoordinator.DiscardAsync` accepts `WorkspaceDiscardRequest(id, intentKey, expectedRequestDigest)`. `intentKey` is the fixed key for explicit discard; `expectedRequestDigest` is the target workspace's existing request digest. Compare both before storing intent inside the workspace lock. Scope is fully prepared NTFS task workers; incomplete preparation, bases, other storage, and ongoing release/compaction are rejected.

Intent fixes workspace ID, project/path, request digest, branch/base, current source revision, and the whole snapshot tuple. It creates no successful validation, checkpoint, or independent result. Previously separately preserved independent-result refs/shared Git objects remain; discarding those results is outside this request's scope.

Inputs follow `urn:splinter:v1:workspace-discard-request`; optional `workspace.discard` fields in durable state/responses follow `urn:splinter:v1:workspace-discard-state`. Missing/damaged data or different owner/stage/snapshot are preserved/rejected. Only the current version is supported; earlier intents are neither automatically migrated nor replaced by new intents.

<a id="단계와-중단-재개"></a>
## Stages and interruption resumption

```text
Check shared dependencies → read-only source ownership check → store intent
stop → fix seal → unmount → source registration/task branch → delta → snapshot → complete
```

Runtime requires application/child ownership and zero processes/memory. It compares the fixed seal before/after unmount; new writes are preserved with `DiscardDeltaChanged`. Request source/delta removal only after confirming zero provider/mount/handles/memory and absent actual mount path. The deletion provider verifies fixed source revision, exact registration/file allowlist, and independent-result protection, publishes partial-deletion intent, then reconciles the same request. Loss of Runtime's next-stage save must not duplicate previous external effects.

During discard, ensure/Editor start, refresh, stop/checkpoint/integrate/release/compact cannot begin another lifecycle. Status returns observations/discard stage without reading source files or inheriting successful validation responses. Only `DiscardAsync` with the same intent key/digest continues remaining stages.

Workspace, candidate, source-generation, or cache dependencies block before stop, returning exact dependencies and next actions to inspect. Recheck immediately before snapshot reclamation. Publish current absent mount/provider/delta and zero owned-resource observations in durable workspace state before requesting the separate service. State-publication failure stops before reclamation request. Other tasks, original Editor, and shared views/caches are not automatically stopped.

`discard-reclaim-pending` waits for snapshot reclamation after source/delta cleanup. Retain reclamation receipts/blockers and retry only the snapshot stage with the same intent. `discarded` is a completion receipt confirming absent exact snapshot/delta/mount/provider and zero owned resources. Unknown is neither 0 nor complete. Conversation/Editor termination, timeout, and disk pressure do not express discard intent.

<a id="실제-provider와-권한-경계"></a>
## Actual providers and privilege boundaries

`WindowsWorkspaceDiscardProvider` composes standard-user Git/delta handling. Git checks the created `splinter/<id>` branch and exact owner lock/registration receipt, publishes `git-discard.json` intent, then removes registration. Fixed-revision compare-and-delete with `--no-deref` removes only the task branch, preserving independent-result refs, shared objects, original, and peers. After a lost response, absent registration/branch completes only the same intent. Recreated/changed branches are preserved/rejected.

Windows delta records exact directory, seal, and identity hash in owner intent and `discard-delta-reclaim.json`. A journal fence excluding writers verifies hash/length, then deletes only `delta.journal` and `identity.json`. Unexpected files or changed identity/journal are rejected; after partial deletion, process only remaining files under the same record. Forced cleanup of ongoing preparation/compaction is unsupported.

VSS receives separate `release.discard` authorization through the existing fixed `Release` IPC operation. The service compares authenticated user, installation scope, protected capture tuple, durable discard state/intent/seal, absent actual Git registration/branch/mount/delta, zero owned resources, shared dependencies, and an exclusive reader fence. It passes protected `discard-authorized.json` to the helper without creating `resultsPreserved=true`. Live identity, exact snapshot ID, existing deletion-isolation guards, and deferred reclamation receipts remain. No compatibility fallback to previous services or automatic elevation is added.

Product completion criteria follow [M0](plans/validation-matrix.md#first-practical-use-version-m0). .NET calls without a separately composed provider are rejected with `WorkspaceDiscardUnavailable` before intent/stop.
