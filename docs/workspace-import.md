# Bring changes from one workspace into another

Use `workspace import` when work in B depends on a change in A. A checkpoints its saved work and can continue editing immediately. B reviews that fixed result and imports all its changes or selected files and directories. A and B must belong to the same Splinter home, original project, fixed source basis and application scope. Git, Plastic and managed ordinary-folder workspaces use the same command.

## Import a dependency

Run these commands from the Splinter checkout. Use the workspace IDs returned by `workspace ensure` for `$producer` and `$consumer`, and their shared state directory for `$workspaceHome`.

```powershell
pwsh ./scripts/splinter.ps1 schema workspace.import

$capture = pwsh ./scripts/splinter.ps1 workspace checkpoint --id $producer --home $workspaceHome `
    --request-key dependency-v1 --message 'Shared movement code' | ConvertFrom-Json
$result = $capture.data.resultGeneration.id

$transfer = @('workspace', 'import', '--id', $consumer, '--from', $producer,
    '--result', $result, '--home', $workspaceHome, '--path', 'Assets/Scripts/Movement.cs')
$review = pwsh ./scripts/splinter.ps1 @transfer --plan | ConvertFrom-Json
$plan = $review.data.importPlan

pwsh ./scripts/splinter.ps1 workspace read --id $consumer --home $workspaceHome `
    --collection plan --plan-id $plan.digest
```

Check the write set and `conflictCount`. Use `--collection conflicts` to read the conflict paths and reasons. After reviewing a conflict-free plan, apply it with a stable destination intent key.

```powershell
pwsh ./scripts/splinter.ps1 @transfer --intent-key take-movement-v1 --expected-plan-digest $plan.digest
```

Omit `--path` for all changes. Repeat it for multiple paths. Selecting a directory includes changed descendants. Unity selections include required asset and folder `.meta` companions. The comparison can include additions, edits, deletions and returns to basis content. Required parent directories are included. Each changed file that needs publication is staged at its full length. Unrelated work in B stays in place.

The destination holds the import receipt and tracks the last imported version of each path from each source workspace. On the next import from A, a path is compared with the version B last took from A, or with the shared basis on its first import. A path already at the requested version can be accepted without rewriting it. If B independently changed the same file to different content, the plan reports a conflict. A stale or conflicting new plan cannot start a publication.

## Continuing work and recovery

An import writes B's private working tree. B can checkpoint the combined work, continue editing, or pass its own result to another workspace. The original project and A keep their own publication histories. Importing a result needs neither a Git commit nor a Plastic check-in.

A result remains usable if A later captures another generation or releases its workspace. After a lost response, inspect B with `workspace status`. Resume `pendingImport` using its source, result, intent key and plan digest. A completed retry returns `data.importReceipt` and preserves B's later edits. Use `workspace read --id <B> --collection imports` for its completed requests. An interrupted multi-file publication can have visible progress. B must finish that intent before checkpointing, compacting, releasing or discarding. If publication reports preserved scratch files, inspect them with `workspace read --id <B> --collection preserved-scratch --store-scope workspace-import --request-key <intent>`.

## Unity Editors

Save the required scenes and assets in A before checkpointing. For a running B Editor, save B, defer imports and obtain the [live Editor cooperation receipt](repeated-original-apply.md#live-unity-original-agent-owned-cooperation) for **B's returned workspace path, owned PID and process start time**, and **A's result ID**. Add `--live-target <receipt.json>` to both the plan and apply commands. Renew the same cooperation identity on recovery. After applying, release B's deferrals, refresh and verify the affected assets through its existing Unity tools. The same external save and dirty-scene rules apply. A stopped destination uses an exclusive publication reservation.
