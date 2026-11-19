# Parallel Unity workspaces and Editor lifetime

Splinter supplies a writable project path and owns the Editor it launches for that task. Agents use their existing Unity tools for refresh, compilation, scene editing, Play Mode and tests. [NTFS workspace lifecycle](ntfs-workspaces.md)

## Default agent workflow

Before creating parallel Unity workspaces, establish that the saved common input compiles with its selected Editor. Use a completed native compilation for that exact source, packages and Library input, or run the native batch check on a verified fixture. Retain the input identity, fresh log and exit code. A compiler diagnostic, Bee postprocessing failure, PackageCache read error or Safe Mode prompt fails the trial. Stop admitting peers and preserve the failing log. [Native compile and readiness checks](development-cli.md#unity-readiness-and-shutdown-timing)

Use a short NTFS home outside the project. Check the resulting package paths with the selected Editor's reader. Pass the same project, home and base and a stable key for each task. Save the returned workspace ID and use its exact `data.workspace.path`. The original and each mounted workspace can produce different compile results. After startup, check each workspace's errors, completed compilation and external readiness before admitting its result. [Agent start](agent-start.md)

## Exact target selection with Unity CLI

Pass the returned project path as the working directory and explicit target. Compare the reported path, PID and process start time with the owned Editor before issuing commands. After a reload, rediscover the connection. An omitted discovery result can indicate missing Pipeline support or restricted process visibility. Preserve request IDs across uncertain replies. [External tool recovery](unity-mcp-response-recovery.md)

## Results and explicit completion

Save changes through the exact Editor. An intermediate `workspace checkpoint` fixes a result under a stable request key while editing continues. Use a reviewed `workspace integrate --target original` plan for original publication. For a running original, provide exact save and import-deferral evidence and verify the affected scene or asset after publication. [Result and original publication](repeated-original-apply.md)

Use `workspace checkpoint --release` to preserve a final result and release the task. `workspace discard` requires separate explicit intent. A shared Library can be restored after each dependent releases its reference and the original Editor has saved and closed normally. [Library restore](git-library-reference.md)

## Execution isolation and normal shutdown

The adapter binds the Editor to its project, executable, PID, start time, token and Windows job. The owned Editor bridge reports execution state and requests normal shutdown through the exact main window. Unsaved scenes, Play Mode, import or compilation can block exit. Preserve the task and inspect the recorded reason if shutdown times out. Release requires verified exit and resource cleanup. [Process and exit receipts](storage-and-resources.md#processes-and-memory)

On a same-task restart Splinter verifies and reuses its unchanged Editor bridge. For an initial launch with an explicit Library graft, it removes ordinary top-level compiled `Library/Bee/*.dag` files in the private view so Unity can rebuild the graph. Nested artifacts, graph inputs and the shared basis are preserved. File-open and compilation diagnostics in the mounted Editor are part of acceptance.

### Shared preferences and external validation

Keep the installed Editor patch and the user's normal preferences, license and consent identity. Unity Editors can share user-wide settings, favorites and licensing helpers. If transitions contend on a shared settings file, coordinate those transitions and verify each completion. Use a fresh nonce-bound scratch fixture for the optional diagnostic in `scripts/test-unity-shared-settings.ps1`. A new modal fails a trial. Cancel the affected owned Editor normally and retain the evidence.

For Play Mode verification, observe entry and advancing frames. On exit, wait for the matching Edit Mode event and settled compilation/import. Before restoring a shared Library, verify release for every dependent workspace. [Unity maze measurement](verification/parallel-workspace-performance.md)
