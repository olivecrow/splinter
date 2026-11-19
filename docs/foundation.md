# Technical architecture

[한국어](ko/foundation.md) · [Documentation map](README.md) · [Validation limits](verification/README.md)

Splinter exposes a writable NTFS/WinFsp project tree from an immutable project basis and task-private changed byte ranges. The original stays editable. Creating a workspace does not copy the whole project, hydrate files on read or copy up whole files. Workspace storage isolation is not an OS security sandbox.

## Components

| Project | Responsibility |
| --- | --- |
| `Splinter.Core` | Source/storage/application/process ports, results, ownership and failure contracts; no Win32/Unity/Git/Codex dependencies |
| `Splinter.Protocol` | Product/development command catalogs, DTOs and embedded JSON Schemas |
| `Splinter.Host` | Read-only discovery and diagnostics |
| `Splinter.Runtime` | State/locks, lifecycle, journals, paged metadata, independent results and resource/recovery policy |
| `Splinter.SourceControl.Git` | Fixed commit/tree/blob readers, owned refs/fences, Git results and explicit branch materialization |
| `Splinter.SourceControl.Files` | Ordinary-folder and Plastic bases, independent file/range results and guarded publication |
| `Splinter.Application.Unity` | Exact-project/version Editor launch, readiness, normal shutdown and owned bridge |
| `Splinter.Platform.Windows` | WinFsp, native path/identity fences, project/Library bases, publication, process ownership and restoration |
| `Splinter.Workflows.Windows` | Development immutable views, shared caches and their resource lifetimes |
| `Splinter.Cli` | Parsing, adapter composition, JSON responses and exit codes |
| `tools/` | Separate development CLI and reusable read/write/bounded-log probes |

Product commands use `scripts/splinter.ps1`; diagnostic views/probes use `scripts/splinter-dev.ps1`. The [agent skill](../skills/splinter/SKILL.md) owns command use and exact-path handoff. There is no installer, automatic conversation binding, task scheduler, AI Runner/DAG or daemon/DB writer.

## Bases and task identity

Source kind (`git|plastic|none`), application kind (`unity|none`), project/key/id/home and opaque basis identity are fixed for a task. Providers never fall back to another source after a failed proof. [Git](git-project-basis.md), [Plastic](plastic-project-basis.md) and [managed ordinary projects](plain-project-basis.md) own their input restrictions. Plastic's private read-only revision adapter requires its recorded client version; it does not register workspaces, check in or pin a server revision.

Managed project and [Library](git-library-reference.md) preparation moves existing data on the same NTFS volume and replaces the original path with a writable view. Preparation and final restoration require the exact original Editor to save and stop normally. Task reclamation releases only its own references; final restore waits for every dependent and reader. Current task/basis/product-overlay versions are **v4/v3/v3**, file-result manifests are **v4**, and metadata uses **SPM2**. Unity's disposable `Library` basis uses a SHA-256-checksummed flat binary metadata index (`fast-index.bin`), hardlink rejection and same-handle length/timestamp validation, lazy basis handle pooling (up to 512 handles per mount), and same-volume O(1) NTFS directory-move restoration when unchanged (after verifying the index and zero journal sequence). Modified Library entries are staged, checked and installed in the private basis before the directory is published. VCS-free `project-snapshot` bases retain full content hashing and verification. Damaged or unsupported state is preserved and rejected.

## Metadata and changed ranges

[Scalable storage](scalable-storage.md) owns binary pages (at most 64KiB), path interning, disk sort/spools, history and range indices, cursor queries and metadata collection. Logical node hashes/counts and accepted-source seals do not replace full native identity/hash/page/inventory proof.

Product overlay v3 uses SJS1 control with raw SJR2 segments for source paths (`Assets`, `Packages`, `ProjectSettings`, etc.). For Git/Plastic workspaces with managed generated/ephemeral paths (such as Unity `Library`, `Temp`, `Logs`, `UserSettings`, `obj`, `.vs`, `.idea`, `*.csproj`, `*.sln`, `*.slnx` when untracked in the source snapshot), `PartitionedWorkspaceDeltaStore` routes those paths to `EphemeralRangeStore` (basis reads without copy-up, raw `ranges.dat` partial edits with in-place reuse of existing extents when no concurrent reads overlap, scratch files with pooled handles, and reconnect index saved on unmount) without per-write fsync or `SPM2` overhead. Cross-partition renames record the staging and file identities, publish with the caller's `replace` flag and carry open handles across successive moves. Reconnect finishes a committed move by unlinking its recorded source. Later destination edits survive. WinFsp mounts enable kernel metadata/file caching (`FileInfoTimeout`/`DirInfoTimeout`/`SecurityTimeout` = 10s, `VolumeInfoTimeout` = 5s, `FlushAndPurgeOnCleanup = false`, 16 worker threads) and explicitly flush modified source paths via `FlushFileBuffers` before `capture-source`, `source-query`, `flush`, and `stop` (propagating flush errors and keeping dirty marks for concurrent writes or still-open handles; memory-mapped views still require explicit view and file flush by the writer per [kernel recovery](kernel-recovery.md)).

Each raw frame retains its header, mutation metadata, payload and SHA-256. The 16 MiB or 4,096-frame rollover bounds a segment. Stored and decoded lengths must match. Development append, bounded and latest-state journals use their respective protocols.

[Raw-write batching](raw-write-batching.md) keeps FIFO order, buffer/handle leases, bounded callback/chunk/staging budgets, full failure/poison handling and drain. Concurrent mutations may share a durable flush, but each receives success only after its accepted data and metadata are durable. Metadata header/page flushes precede root ACK. Flush reuse is limited to the same proven writer/bytes and never crosses reopen.

Request-local metadata/catalog readers and path proofs retain native fences, current ownership/leaf checks, bounded capacity and invalidation. The optional Core metadata/mutation batch and original-observation ports have full-check fallbacks for unsupported stores. Exact-writer append tracking is invalidated on failure. Adaptive callback collection and cost-aware packing do not weaken ACK, proof, reader or recovery boundaries.

`StorageResources` supplies a demand-driven process-local working target up to 4GiB, with bounded metadata cache (up to 2.5GiB), shared sort/spool scratch (up to 512MiB) and worker budgets (up to 32). These are not reserved memory, an OS RSS cap or a combined-provider memory limit. Unsupported/compound stores keep their fallback path; Plastic revision retrieval stays serial. [Resource policy](storage-and-resources.md) owns details.

## Capture, results and publication

A continuing task retains its path, ID, immutable basis and Library across intermediate checkpoints. Capture freezes accepted source roots and protection under the mutation boundary, then reads/hashes and independently preserves content outside it. The same request returns its first fixed result; later task writes remain live.

The receiving workspace owns the plan, conditional file publication and per-source path history for a [workspace import](workspace-import.md). A checkpoint in that workspace includes its local changes and imported content.

Pending capture/control/pin and live-repack recovery run before independent GC. Frozen read-only metadata uses an independently owned scratch address domain; it does not append to the source or copy the whole graph. Active reader leases retain the source, basis, journal backing and scratch until closure. Offline v3 change observation refuses instead of inventing a live root.

`OwnedContentStore`, `OwnedRangeContentStore` and `ResultFileContentStore` preserve immutable whole/chunk/slice data and complete dependencies. Mutable originals cannot substitute for a basis. Full capture/publication validation, independent content proof, historical references and same-request adoption remain required. [Repeated application](repeated-original-apply.md) owns result generations, per-path history and final closure.

Original publication requires an explicit result, plan, intent/digest, complete target lock set and current content/native revalidation. These [publication guards](repeated-original-apply.md) apply across source modes. Git original-file publication preserves HEAD/index/branch; branch materialization is a separate explicit integration. A running original requires exact external Editor-cooperation evidence. No automatic commit/check-in or conflict forcing is provided.

## Recovery and reclamation

State/control publication is atomic and checksummed where specified. Current-version interruption, response loss and restart resume the exact identity/request/intent. Unsupported, corrupt, unknown or unowned records are preserved/refused. Full native handle/path/parent/content and reader protections remain at publication and deletion boundaries.

`workspace maintain` reclaims owned metadata across workspace/shared/content/range scopes using the same pending request before admitting new work. Completed retries return receipts. `workspace read` exposes root-bound pages and protected/deferred residues. Metadata thresholds and forced release maintenance are processing policy, not input quotas.

Library restoration resumes existing intent first. Fast-indexed Unity `Library` bases restore unchanged directories with a single same-volume NTFS rename (after index/sequence verification) and publish modified directories after checking their planned changes. Catalog-indexed bases select unchanged, sparse, root-delta, eligible topology or general per-file restoration. [Library contract](git-library-reference.md), [journal recovery](journal-recovery.md), [product-write recovery](product-write-recovery.md) and [kernel recovery](kernel-recovery.md) own details.

## Application and tool boundaries

Splinter owns returned project identity and its Editor/provider/resource lifetime. Windows process groups track and clean up workspace descendants on root exit while omitting `KILL_ON_JOB_CLOSE` and excluding the machine-shared `Unity.Licensing.Client` process from termination (verifying job membership and image name on the same handle) so parallel Unity Editors can launch and exit independently in any order; explicit `release` and `discard` of Unity task mounts allow unmount after a 750ms grace period when only root-directory CWD handles remain (`compact`, `library restore`, and views still require zero open handles). Agents and user-installed tools own editing, target selection, connection, refresh, compilation, Play/Test/build, capture and acceptance. Match every returned project/PID to the workspace; a missing tool-list entry is not proof of Editor absence. Do not retry writes solely because of a timeout or pin ports across reload.

The supported workflow is create/resume → work → checkpoint → optional explicit publication → release/discard. Release preserves independent results; discard is a separate explicit request. Conversation/Editor exit, lease expiry, timeout and disk pressure do not authorize integration or deletion. [Validation scope](verification/README.md) distinguishes current behavior from pending large-input, whole-lifecycle, RAM and OS-failure qualification.
