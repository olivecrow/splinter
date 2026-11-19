# Managed project without version control

Use `project-snapshot` for an ordinary folder on NTFS. Project and state home must be separate directories on the same volume. Admission rejects VCS administration in or around the selected project, unsupported NTFS features and overlapping roots. The source must remain at its existing path. Preparing a Unity project requires saving and normally closing its exact Editor.

## Prepare and work

Read the command schemas from the built product CLI. Pass `--application none` for a non-Unity project.

```powershell
pwsh ./scripts/splinter.ps1 project prepare --project <original> --key <basis-key> --application unity --home <home>
pwsh ./scripts/splinter.ps1 workspace ensure --project <original> --key <task-key> --source none --application unity --storage project-snapshot --project-basis <returned-basis-id> --home <home>
```

Preparation moves existing project data on the same NTFS volume, records its manifest and exposes a writable original view. The managed project basis retains full content hashing and verification. The original and each task have separate private changes. Work only through the assigned path. Keep the original installation, intent, task and basis identities for retries.

## Publish and restore

Checkpoint with a stable request key. For original application, select the fixed result, review the plan and pass its intent and expected digest. The publisher binds the original mount and current target identities, checks for conflicts, and retains progress for interrupted operations. A live original Editor requires its own cooperation evidence. [Publication procedure](repeated-original-apply.md)

Release or explicitly discard each task when authorized. Each releases its own references. Save and close the original Editor, wait for dependents and readers to finish, then call `project restore --id <basis-id> --home <home>`. Restoration uses the managed directory's identities and journal, verifies native files and metadata, and reclaims only the known owned state. A damaged or interrupted restore keeps its data and request for recovery.
