<a id="windows-저장소-권한-서비스"></a>
# Windows storage-privilege service

[한국어](ko/storage-privilege-service.md)

The current ordinary-folder path for `--source none` and v2 state/service packages follows [VCS-free workspaces](vcs-independent-workspaces.md). Git paths and historical native qualification below do not qualify ordinary-folder support.

`VssSnapshotProvider` sends fixed `Capture`, `Observe`, and `Release` requests from standard-user CLI to the `SplinterStorageAuthority` Windows service. CLI, Git, and Unity retain standard-user privileges. Host remains the read-only in-process composition layer; this service replaces neither Host nor a task-assignment daemon. [UA verification](verification/storage-privilege-service.md) owns current actual installation/repeated-execution qualification.

<a id="설치와-발견"></a>
## Installation and discovery

Service executables live in `%ProgramFiles%/Splinter/StorageAuthority`; ownership, request, and VSS receipts live in `%ProgramData%/SplinterStorageAuthority`. Only SYSTEM/Administrators may modify/read state; standard users have only read/execute access to executables. SCM runs the service as LocalSystem. It executes only installed PowerShell 7 and VSS scripts fixed in the package.

- `pwsh scripts/package-storage-service.ps1 -OutputPath <new package path>` creates a Release package and per-file SHA-256 list, without overwriting existing packages.
- Packaged `Splinter.Platform.Windows.exe storage-grant <absolute project path> <absolute home path> <user SID>` is an installation entry point reading file/volume IDs of existing directories. Save grant objects as a JSON array. It neither creates project/home nor changes permissions.
- `pwsh scripts/setup-storage-service.ps1 -PackagePath <package> -GrantFile <grant array JSON>` defaults to a read-only installation plan. Only administrator execution adding `-Apply` to the reviewed request changes machine configuration.
- `pwsh scripts/splinter.ps1 storage service status` reads SCM state, installation path, and current CLI elevation. It neither creates VSS nor requests elevation.

Initial installation/scope configuration and explicit `-Action Update`/`-Action Remove` are administrator actions. Ordinary workspace commands for already registered user/project/volume/home have no elevation code. Unregistered projects/homes are rejected with `StorageScopeDenied`. Identical path strings do not permit an existing grant after directory replacement or volume change.

Installation planning validates inputs before requesting administrator privileges. Grants are JSON arrays even with one entry; field names, SID, volume/file-ID format, existing canonical directories, and duplicate scopes are checked. Required package executables, DLLs, runtime settings, scripts, hashes, and duplicate filenames are checked. Duplicate JSON properties, unknown fields, and differently cased fields are rejected. Plan validation neither runs package executables nor installs system files. Actual grant file/volume-identity comparison and execution success still require service verification after installation.

Update preserves existing grants/protected state of the same protocol. Removal is rejected if captures or requests remain active/incomplete. Removing service registration does not automatically delete packages, ownership records, or independent results. There is no automatic old-version migration or adoption of existing manual-helper receipts.

<a id="요청-경계"></a>
## Request boundaries

At installation/service startup, path owners must be SYSTEM/Administrators. Check both the path and write permissions inherited by children. Even standard-user `InheritOnly` writes are rejected. Inherit-only `CREATOR OWNER` rules are treated as substituted by the trusted creation token. Existing installation files/configuration also receive individual owner/write-permission checks; unsafe ACLs are not automatically repaired.

IPC uses local named pipe `Splinter.StorageAuthority.v1`. ACL permits registered user SIDs and SYSTEM only, denying Network SID. Registered users may read/write as clients but cannot create server instances. Clients verify SCM LocalSystem account, fixed executable path, independent process configuration, and pipe-server PID. The service impersonates the client to obtain its actual SID, without trusting usernames in request bodies. It creates only the first pipe instance, avoiding sharing the same name with another server.

User project/home/workspace reads use the requester's token. The service rejects reparse/path substitution and compares directory IDs with registered values. Ancestor paths and inspected records remain protected during the operation by handles preventing rename/delete. Ordinary file edits inside the original remain possible.

Requests accept only current version, fixed operation, owner, project, home, snapshot ID, and boot-bound verification/reclamation IDs. There is no arbitrary code, command, executable, or administrator-write destination. Duplicate/unknown JSON fields, missing required fields, and excessive sizes are rejected. `storage-authority-request.schema.json` and `storage-authority-response.schema.json` own inputs/outputs.

<a id="보존과-재시도"></a>
## Preservation and retries

Full request-frame reception is limited to 1 second after connection; authenticated operations have a separate 3-minute limit; response writing has a separate 1-second limit. Incomplete headers/bodies terminate connection without entering resource operations. This is not a rate-limit/security-sandbox contract preventing all service abuse by malicious registered accounts.

The common VSS reclamation lock is an exclusive file handle at protected `%ProgramData%/SplinterStorageAuthority/vss-release.lock`. It can be reacquired when process exit closes the handle; the file is not automatically deleted. It does not depend on another local user's ability to preempt a global mutex. Authorized manual-helper reclamation also requires this installed protected path. Absence, unsafe owner/inherited ACLs, or reparse preserve/reject. Existing snapshot inventories, deletion isolation, and same-request guards remain.

The service links only snapshots it captured to ownership records. A snapshot ID belonging to another workspace or a capture receipt from user files cannot grant privileges. Service receipts are written only to protected directories; ordinary CLI stores the response's fixed receipt names in workspace folders.

Changed input for the same request ID produces `StorageRequestConflict`; another incomplete request produces `StorageRequestPending`. Retry after losing a completed response returns the stored response. Interrupted requests resume with the same intent without creating another captured snapshot. Complete `.pending` JSON in protected state is flushed/published on restart; damaged/incomplete files are preserved/rejected. Unclear native capture results are preserved with `CaptureReconcileRequired`.

Ordinary CLI publishes successful-response verification receipts first and `snapshot.json` last. Nonterminal failure does not update this completion marker. Termination during response storage still retries the existing capture/release request. When Runtime `capture-intent` stage and service ownership receipts remain, read verification can resume even for a capture whose returned ID was not yet published into workspace state.

Explicit workspace discard is a separate `release.discard` contract under the same `Release` operation. The service checks fixed intent/seal, completed Git-discard receipt, actual absent task branch/registration/mount/delta, zero owned resources, shared dependencies, and reader fence. It publishes only protected `discard-authorized.json` without claiming result preservation. Use after explicitly updating the service package to match current CLI; [discard verification](verification/workspace-discard.md) owns actual qualification.

Preserving reclamation passes Runtime `stable-journal-v1` result verification, Git-object integrity, and matching post-unmount seals. The service additionally checks durable `release-snapshot` stage, independent results/fixed Git refs, zero owned processes/memory/handles, absent mount/delta, other workspace/validation dependencies, and exclusive reader fence. Existing VSS live identity, deletion-isolation guards, and `reclaim-pending` remain. Elapsed time alone does not delete snapshots.

An absent/stopped/version-mismatched service requires installation/recovery. There is no automatic `RunAs`, administrator CLI, or fallback bypass through previous helpers. Direct `scripts/vss-snapshot.ps1` use is limited to separately authorized development diagnostics/manual recovery; it is not automatically selected as an alternate privilege mechanism for normal workspaces.
