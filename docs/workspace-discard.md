# Explicit workspace discard

Use explicit discard to abandon one fully prepared range-journal task and reclaim its owned resources. Inspect the target ID, path and request digest first.

Git, Plastic and managed project bases retain their reader and original/peer ownership guards. Independently retained results survive task discard.

```powershell
pwsh ./scripts/splinter.ps1 workspace status --id <id> --home <home>
pwsh ./scripts/splinter.ps1 workspace discard --id <id> --intent-key <stable-discard-key> --expected-request-digest <requestDigest> --home <home>
```

Identify the target through status/list ID, project/path, and `requestDigest`. Retry interruption/delay with the same key/digest/id/home. `Succeeded`/`discarded` means exact owned resources are absent; `Accepted`/`discard-reclaim-pending` preserves a provider-deferred owned-reference reclamation after task payload removal. Existing preserving [release](ntfs-workspaces.md#reclamation-and-observations) continues to require independent results/seals.

## Inputs and ownership

`WorkspaceCoordinator.DiscardAsync` accepts `WorkspaceDiscardRequest(id, intentKey, expectedRequestDigest)`. `intentKey` is the fixed key for explicit discard; `expectedRequestDigest` is the target workspace's existing request digest. Compare both before storing intent inside the workspace lock. Scope is fully prepared NTFS task workers; incomplete preparation, bases, other storage, and ongoing release/compaction are rejected.

Intent fixes workspace ID, project/path, request digest, branch/base, current source revision, and the whole snapshot tuple. It creates no successful validation, checkpoint, or independent result. Previously separately preserved independent-result refs/shared Git objects remain; discarding those results is outside this request's scope.

Inputs follow `urn:splinter:v1:workspace-discard-request`; optional `workspace.discard` fields in durable state/responses follow `urn:splinter:v1:workspace-discard-state`. Missing/damaged data or different owner/stage/snapshot are preserved/rejected. Only the current version is supported; earlier intents are neither automatically migrated nor replaced by new intents.

## Stages and interruption resumption

```text
Check shared dependencies → read-only source ownership check → store intent
stop → fix seal → unmount → source registration/task branch → delta → snapshot → complete
```

Runtime requires application/child ownership and zero processes/memory. It compares the fixed seal before/after unmount; new writes are preserved with `DiscardDeltaChanged`. Request source/delta removal only after confirming zero provider/mount/handles/memory and absent actual mount path. The deletion provider verifies fixed source revision, exact registration/file allowlist, and independent-result protection, publishes partial-deletion intent, then reconciles the same request. Loss of Runtime's next-stage save must not duplicate previous external effects.

During discard, ensure/Editor start, refresh, stop/checkpoint/integrate/release/compact cannot begin another lifecycle. Status returns observations/discard stage without reading source files or inheriting successful validation responses. Only `DiscardAsync` with the same intent key/digest continues remaining stages.

Workspace, candidate, source-generation, or cache dependencies block before stop, returning exact dependencies and next actions to inspect. Recheck immediately before snapshot reclamation. Publish current absent mount/provider/delta and zero owned-resource observations in durable workspace state before requesting owned-basis reclamation. State-publication failure stops before reclamation request. Other tasks, original Editor, and shared views/caches are not automatically stopped.

`discard-reclaim-pending` waits for snapshot reclamation after source/delta cleanup. Retain reclamation receipts/blockers and retry only the snapshot stage with the same intent. `discarded` is a completion receipt confirming absent exact snapshot/delta/mount/provider and zero owned resources. Unknown is neither 0 nor complete. Conversation/Editor termination, timeout, and disk pressure do not express discard intent.

## Actual providers and privilege boundaries

`WindowsWorkspaceDiscardProvider` composes standard-user Git/delta handling. Git checks the created `splinter/<id>` branch and exact owner lock/registration receipt, publishes `git-discard.json` intent, then removes registration. Fixed-revision compare-and-delete with `--no-deref` removes only the task branch, preserving independent-result refs, shared objects, original, and peers. After a lost response, absent registration/branch completes only the same intent. Recreated/changed branches are preserved/rejected.

The Windows deletion provider verifies the owned delta directory, seal, identity and inventory. Segmented journals use their recorded ranges, metadata and segment inventory. A writer fence and physical backing checks protect verification and reclamation. After interruption, the same intent reconciles the remaining entries. Unknown or changed files block deletion.

For bounded SJR2 the dispatcher records the bounded inventory, including an outstanding publication and writer fence. The Windows directory proof stays pinned through reclamation. Mixed-mode deletion receipts are rejected.

Basis reclamation verifies the discard intent, fixed seal, remaining dependents and owned-resource absence before dropping the exact reference.

.NET calls without a separately composed provider are rejected with `WorkspaceDiscardUnavailable` before intent/stop.
