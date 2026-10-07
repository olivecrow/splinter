<a id="실행-자원과-메모리-회수"></a>
# Execution resources and memory reclamation

[한국어](ko/storage-and-resources.md)

This is the common contract for per-task process ownership, executable bundles, memory observations, and normal reclamation. Storage lifetime follows [NTFS workspaces](ntfs-workspaces.md) and the selected snapshot-basis contract.

<a id="프로세스와-메모리"></a>
## Processes and memory

The per-task supervisor in `Splinter.Platform.Windows` holds the Windows Job Object handle. It creates Unity suspended and assigns it to the job **before** resuming it, closing the gap for early child-process creation. Breakaway is prohibited. The supervisor and Editor remain alive after the CLI and conversation end. This is not a Host daemon.

The supervisor does not inherit the caller's standard input/output or other inheritable handles. The CLI's JSON response and output streams finish even while the Editor remains alive. Executables share one small bundle per build hash rather than being copied for every task, allowing the product to be rebuilt while running.

`PrepareRunner` computes the content key from the ordered SHA-256 hashes of the current 8 executables. Read-only source handles are held only during preparation to prevent changes between hashing and copying. A new bundle is copied through a fixed-size buffer before its directory is published. Cache length and content hashes are checked by streaming, so reuse does not load entire executables into memory arrays. All source handles close after success or failure; damaged or missing caches are preserved and rejected. [Runner-bundle verification](verification/runner-bundle-memory.md) records disk reuse and managed allocations in the preparation function. These allocations do not establish Editor or whole-system RAM savings.

Normal stop follows the Unity bridge's rejection of unsaved scenes and Play mode, and its normal shutdown procedure. The supervisor cleans up job members remaining after the Editor itself exits. After confirming the last member has exited, the supervisor also exits. The original Editor, other workers, and shared services already running are outside this job. Unexpected supervisor death activates job kill-on-close, but without shutdown evidence it is not classified as normal reclamation.

`application.resources` reports the number of owned processes, including the supervisor, and observed working set/private bytes. Running values are OS samples at a particular time. Release completes only after verifying `ownershipVerified=true`, `activeProcesses=0`, `workingSetBytes=0`, and `privateBytes=0`, and confirming the actual path is absent after Git reclamation. The result is recorded in `workspace.releasedResources`. Splinter does not forcibly empty OS file caches or standby memory, or claim that whole-system RAM has returned to its pre-execution value.

Current journal compression preparation uses one scratch buffer and an array of selected results. Small/empty compressed reads verify the entire stream through chunks and staging for the requested range. The [ME plan](plans/memory-completion.md) and [direct verification](verification/memory-completion.md) distinguish concurrent pool leases, managed allocations/live heap for the same real input, and separate owned provider/Editor and shared-system costs. Lease return and owned-process shutdown do not reset all OS RAM.

If the execution receipt lacks job ownership, `ProcessOwnershipUnknown` stops automatic reclamation. Together with `ProcessCleanupIncomplete`, paths, results, and processes are preserved while actual ownership is checked. An Editor timeout alone does not delete source or cache data. Successful release requires current `releasedResources` evidence.
