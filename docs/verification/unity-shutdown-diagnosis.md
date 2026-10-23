# Unity shutdown diagnosis

[한국어](../ko/verification/unity-shutdown-diagnosis.md)

**SD-01, 2026-10-23:** the user stopped the controlled workload comparison and requested a small-unit investigation of abnormal shutdown. This is a diagnosis, not a completed Splinter/shared-Editor scaling benchmark. Product code remains `c4fa506`; Unity is `6000.3.22f1` on Windows/NTFS.

## Findings and controls

The long shutdown reproduces in an ordinary NTFS project without a Splinter mount. Two already-started 65,536-method diagnostic sessions exit normally in **444.55s without post-compilation live reflection** and **437.79s with it**. Removing that verification call alone does not remove the delay. Neither number represents ordinary Editor shutdown or a fixed Splinter overhead.

The subsequent small cases each start with the same 1,602 source files, all 42,038 Library files (3,105,800,655 bytes), and the exact loaded baseline DLL hash, MVID and IL fingerprint. Each uses a fresh batch Editor, one source edit, actual compilation/reload and the same asset checks. All code is saved to disk before compilation. The experiment clone excludes MCP and its former shutdown hook; the original project, installed tools and healthy original server are preserved. OS caches are not reset.

| Generated methods | Generated classes | Methods per class | Compile/reload | Normal exit |
| ---: | ---: | ---: | ---: | ---: |
| 0 | 0 | — | 7.23s | 1.64s |
| 1,024 | 1 | 1,024 | 6.93s | 1.81s |
| 4,096 | 1 | 4,096 | 6.33s | 1.67s |
| 16,384 | 1 | 16,384 | 7.23s | 17.34s |
| 16,384 | 64 | 256 | 6.05s | 1.60s |

Distributing the same 16,384 arithmetic methods across 64 classes removes **15.75s** of observed exit time. This isolates code organization as an important input; it does not prove a general complexity law or that any large real project behaves this way. Each configuration has one observation. The compilation intervals stay around 6–7s while shutdown changes substantially. A synthetic method count is therefore not a reliable proxy for useful Editor workload.

During the long `Cleanup mono` region, one thread consumes CPU, principally at JIT-generated managed-code addresses. Instruction-pointer sampling of the 16,384-method single-class case briefly suspends selected threads for **0.01342s total** across the observation. This identifies active managed execution rather than a save-dialog wait, but native symbols alone do not identify the responsible managed caller. Follow-up instrumentation is diagnostic and excluded from the table.

## What was timed at exit

The external fixture controller writes one stop request and waits for the exact Editor process to exit with code 0. Its Editor handler rejects dirty scenes and active/pending Play Mode, then calls `EditorApplication.Exit(0)`. It does **not** call `SaveAssets` or save scenes immediately before exit. This is exit from the checked scene state, not a measurement of an explicit save-and-close workflow. Unity documents that [this API does not ask to save](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/EditorApplication.Exit.html). No measured successful Editor was forcibly terminated.

The original 65,536-method Splinter pilot exceeded its controller's 300s wait; observation continued independently until both processes exited normally, about 553.1s/555.1s after the persisted request. Same-task result preservation/reclamation and exact Library restoration then completed. Timeout/recovery observations and the incomplete pair are not normal comparison statistics. No new 65,536-method workload was launched after the user stopped the comparison.

## Waiting, parallelization and current product behavior

`Cleanup mono` is a Unity shutdown stage, not a Splinter function. It can include managed callbacks as well as runtime teardown. A log message does not prove that all source/cache writes, file handles or mapped executable images have ended. More Splinter worker threads cannot directly parallelize that internal stage. A specific application/package callback could be optimized by its owner once identified; changing thread count before identifying it is unsupported.

Current range-journal checkpoint explicitly awaits `applications.StopAsync` and `RequireStopped` before preserving the result ([coordinator](../../src/Splinter.Runtime/WorkspaceCoordinator.cs)). Collection checks before/after journal seals. Release again requires zero owned application processes, verifies the independent result, unmounts, checks the final seal and observes zero provider handles before deleting the delta ([lifecycle](../../src/Splinter.Runtime/SnapshotWorkspaces.cs), [Windows backend](../../src/Splinter.Platform.Windows/NtfsSnapshotWorkspaceBackend.cs)). The task lock spans the operation. The current `integrate` API also calls stop. An exit timeout preserves resources; it is not permission to skip cleanup.

| Work while an Editor is shutting down | Current boundary |
| --- | --- |
| Continue another independent task; finish/reclaim an already-stopped peer | Possible under its own guards. A/B completion can overlap; shared bases stay pinned until their last user finishes. |
| Compute a Git merge from already-preserved immutable commits | Independent of the live Editor's files. This is external Git work; it does not bypass the current same-workspace `integrate` stop/target guards. |
| Preserve and integrate this Editor's latest source during its own shutdown | Not provided by the current checkpoint path. A separate immutable source-generation contract would be needed. |
| Delete its mount/private journal, release a still-used basis or restore the shared Library early | Not safe under the current lifetime/handle contract. Final reclamation must wait for verified absence and stable seals. |

A possible future design separates **save/fix an explicit independent source result → request normal exit and process that fixed result concurrently → verify exit/final writes and reclaim resources**. It must distinguish the fixed result from any later writes, retain source and Library dependencies, protect the target, and recover the same publication/reclamation intent after interruption. It is not implemented or qualified here. Merely moving existing calls into `Task.WhenAll` would weaken the current guarantees. Ordinary repeated use already avoids shutdown between edits.

The measured Git merge operations in two controlled pilots took **0.27s and 0.25s**. Overlapping just those operations can hide at most that work, not hundreds of seconds. Overlap of source preservation with teardown has not been measured and would require the new lifetime design above.

## Evidence and disposition

The [dataset](../../reports/benchmarks/unity-shutdown-diagnosis-20261023.json) preserves the five case results, baseline identity, normal-exit/offline-code checks, and hashes of local raw evidence under `artifacts/unity-controlled-batch-scaling-20261023/`. These local raw artifacts are not distributed with the report. Exact source/Library restoration is confirmed for the five-case series. Two extra native JIT-table enumeration attempts crashed inside diagnostic instrumentation; they are excluded, their logs retained, and their temporary clone scripts removed before exact source/Library restoration. Event-only instrumentation completed normally but did not resolve the hot addresses. No installed Unity/package code was patched.

Debugger-client connection failures are separate from successful timing observations. The last raw-protocol attempt triggered Unity's Debug Mode attachment dialog in batch mode. Its controller timed out after 1,200s, and computer control was stopped by the user. That attempt has no recorded normal-exit or final restoration receipt. Its closure remains **unverified** and is not covered by the earlier successful restoration. The exact hot managed function remains unknown; no further diagnosis was run to prepare this documentation commit.

The canceled controlled comparison does not establish a fixed additional cost, a proportional slowdown or a general break-even threshold. The earlier BS-01 table and seven-pair conditional estimate remain historical evidence but are removed from the root READMEs' current performance claims. BC-01's current-candidate short-workflow measurement retains its original single-pair qualification. Historical graphs remain removed. New Play/Test/build, GUI speed, storage/RAM, general tool support and a completed workload-scaling qualification are **NotRun**.
