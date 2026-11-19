# NTFS workspace operations

## Inputs and privilege boundaries

Use a local NTFS project and a separate state home. WinFsp must be installed. Select a supported [Git](git-project-basis.md), [Plastic](plastic-project-basis.md) or [managed project](plain-project-basis.md) basis. The task's project, home, key, ID, storage and basis identify the work across sessions. Keep the original and its VCS administration accessible to their owner.

## Ordinary CLI flow

Read `schema workspace.ensure`. Create or resume the task and use the returned `data.workspace.path`. `workspace status` observes storage and process ownership. An explicit source verification fixes a separate inspection boundary. Starting Unity is optional. [Agent start](agent-start.md)

Capture stable work with `workspace checkpoint --id <id> --request-key <key> --message <description> --home <home>`. This retains a fixed result and leaves the workspace usable. Review it for [original publication](repeated-original-apply.md) or [peer import](workspace-import.md). When preservation and final release are authorized together, use `checkpoint --release`. Use `workspace discard` only with explicit discard intent and the matching request digest.

## Repeated writes to the same private range

Reads combine immutable basis ranges, private extents and logical zeroes. Private writes update the selected extent and namespace indices. The durable source and disposable generated partitions have different flush and recovery policies. Renaming between them preserves replacement semantics and existing open handles. [Storage formats](scalable-storage.md)

## Development preparation-interruption tests

Use `scripts/test-preparation-recovery.ps1` with a disposable supported fixture and fresh state home. For a real interrupted task, inspect the structured recovery disposition, error details and next action. Retry the same request with the recorded basis. Missing or corrupt state beside an existing mount, unverified ownership, or conflicting original and basis directories require preservation and inspection.

## Reclamation and observations

Release verifies independent result backing, owned process exit, flush and unmount seals, native inventories and removal of task-private resources. Explicit Unity release or discard may allow a short grace period for a root-only directory handle after capture and owned Editor exit. Other open files or subdirectories still block reclamation. Library and project restoration require dependents and readers to close. A stopped task can retain results and basis references. [Resource ownership](storage-and-resources.md) · [Discard](workspace-discard.md)
