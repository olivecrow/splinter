<a id="plastic-결과를-원본에-반영하기"></a>
# Applying Plastic results to the original

[한국어](ko/plastic-original-apply.md)

Conditionally publish the current independent result of `plastic-snapshot` to the exact regular/static original. `ntfs-snapshot` Plastic, partial/dynamic workspaces, and reimport of released results are unsupported. Server add/checkout/update/check-in and lock changes are the responsibility of AI and Plastic tools. Shut down the original Editor normally and stop/collect the task Editor through the checkpoint procedure.

```powershell
pwsh scripts/splinter.ps1 schema workspace.integrate
pwsh scripts/splinter.ps1 workspace integrate --id <id> --target original --plan --path Assets/Selected.txt --home <home>
pwsh scripts/splinter.ps1 workspace integrate --id <id> --target original --intent-key <key> --expected-plan-digest <digest> --path Assets/Selected.txt --home <home>
```

`--path` is a repeatable exact relative file/directory path. Omission selects all changes. Plan required new parents and Unity Assets file/folder `.meta` companions together; unchanged companions are also checked to protect original GUIDs. Managed paths, VCS metadata, patterns, duplicate selections, and missing selections are rejected. Review the plan's `selectedPaths`, `sourceIdentity`, changes/conflicts, and digest. Path order is normalized.

Plan path/deletion-guard lookup uses the ancestor set from [common original application](plain-folder-original-apply.md). CLI `nextActions` also retains `target=original` and selected `paths`; resuming incomplete application includes the exact intent/digest. Do not omit resume arguments from a selected plan and change it into full-scope application.

Compare content and relevant attributes across basis/result/current original. Only the basis or an already matching result is allowed; third-party changes, path-case changes, special metadata, and deletion with remaining external children are rejected. Unselected changes remain in the independent result and are not published to the original. Additional edits after fixing a result require a new task.

Plastic revisions do not record the NTFS indexing attribute. Preserve only the existing original's `NotContentIndexed` bit in the published version, continuing to verify content, other attributes, identity, and conditions immediately before publication. Attribute comparison for ordinary-folder bases is unchanged.

At application, check original registration GUID, client version, management-file hashes, and repository/loaded changeset/selector. Hold read handles for `.plastic` workspace/selector/wktree files throughout publication and open the Unity lockfile exclusively. Reject lock/retained observations overlapping selected paths or ancestors/descendants, and unclear observations. Even owned server locks are not ignored without separate handling. Server-lock queries are not remote reservations and provide no distributed atomicity between a new remote lock after the query and local publication.

The current path requires the original's loaded changeset to equal the requested recorded changeset. Local edits and unselected dirty content are allowed, but results are not automatically applied to an original whose loaded changeset changed through check-in/update. Handle this explicitly as the next task appropriate to the changed basis and actual need.

Publication reuses [common original publication](plain-folder-original-apply.md) for content, NTFS identity, parent handles, stage/retired ownership checks, and individual durable progress receipts. Resume after interruption only with the same intent/digest/selection. An incomplete intent blocks release/discard/new collection. A duplicate completed request does not republish. Discard does not undo application to the original. Task results and reclamation are separate decisions.

Library and management data are excluded from application. Final dependent reclamation/restore for `--library-basis` follows the [common Library lifecycle](git-library-reference.md) separately. See [verification](verification/plastic-original-apply.md) for actual qualification and the [plan](plans/plastic-original-apply.md) for scope and exit criteria.
