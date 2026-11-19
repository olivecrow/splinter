# Sharing and restoring a Unity Library

A Git or Plastic workspace can attach a stopped project's warm Unity `Library` as an independent managed basis. The original project keeps its location and writable Library path. Each workspace can update its own generated files. For projects without version control, the [managed project](plain-project-basis.md) includes the Library in its project basis.

## Prepare and attach

1. Save and normally close the exact original Editor. Use a short state home on the same NTFS volume as the original. Close external readers and writers of the directory being prepared.
2. Read `schema library.prepare` and run `library prepare --project <original> --key <stable-key> --home <home>`. Preparation fixes the compatibility key and intent, moves the directory into the owned basis and exposes a writable view at the original Library path. Keep the returned basis ID.
3. Pass `--library-basis <id>` to each corresponding `workspace ensure`. Fix the application, project, home and source revision for the lifetime of each task. The managed original and the workspaces have independent changes.
4. After dependents are released and readers close, save and normally stop the original Editor for `library restore --id <id> --home <home>`. A task release drops only its own reference.

## What preparation verifies

The disposable Library uses a SHA-256-checksummed flat metadata index. Preparation checks ordinary NTFS files, link counts and metadata. Lazy readers check length and last-write time on the opened file and reuse a bounded pool of handles. This detects common external changes. A same-length overwrite with a restored timestamp can evade that check. Keep the basis under Splinter's exclusive management. Managed source bases use full content verification.

For an eligible Git repository, preparation can capture source timestamps for paths whose raw bytes match the fixed Git blob. The source metadata map binds to the Library and Git basis identities. Converted, modified or busy files do not receive a matching timestamp. The original remains editable after preparation.

## Restore and retry

With a verified fast index and zero durable changes in the original Library overlay, restoration publishes the native directory by a single same-volume move. Task-local generated writes do not make the original Library dirty.

For a modified Library, the restore plan binds the stopped journal seal, paths, affected identities, content and file metadata. Splinter stages changed bytes in the private receipt area, retains replaced files there and applies changes to the private basis before publishing the directory. Metadata-only edits and case-only renames are included. Once published, retry verifies the native result before reclaiming the exact stopped delta. A later original edit, damaged staged file or unknown entry preserves the evidence for recovery. Resume the same basis and home. [Managed storage](scalable-storage.md) · [Unity maze measurements](verification/parallel-workspace-performance.md)
