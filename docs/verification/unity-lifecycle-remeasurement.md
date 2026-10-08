# Unity lifecycle remeasurement

[한국어](../ko/verification/unity-lifecycle-remeasurement.md)

> **Current timing reference:** the record below preserves its original candidate/stage measurements and then-unexecuted boundaries. The subsequent combined `116fd44` candidate is measured in [UF-01](unity-final-lifecycle.md): **18m 03.0s** complete workflow, **3m 36.1s** Editor readiness, **2m 52.0s** A/B preservation/release and **2m 06.0s** Library restoration. UF does not isolate each optimization or qualify the separate unexecuted scenarios below.

This document owns **UR-01–04**, the real Unity follow-up to [CO-01–08](checkpoint-release-optimization.md). Product source was committed as `38b6803` before measurement. The frozen normal distribution is the tested CO-08 candidate; no product rebuild or instrumentation was inserted during the run. Raw evidence remains in ignored `artifacts/unity-remeasure-20261007/` and `.local/ur7/`.

## Input and procedure — UR-01

One independent A/B cohort uses Unity **6000.3.22f1**, Git source `b09fa58`, `git-snapshot`, default optimized journals and the original Library reference. All **1,602 tracked source files / 2,804,772,822 bytes** match the [earlier comparison](readme-unity-comparison.md). The old and new Library manifests also match every entry, payload hash and recorded attribute/timestamp: **42,420 files, 2,440 directories, 3,141,660,252 bytes**. Only ownership IDs, basis paths and capture time differ.

Two ensure calls run sequentially; two owned Editors initialize and refresh concurrently. A changes `value + 1` to `value + 2`; B changes `value + value` to `value + value + value`. The existing external qualification probe verifies exact project/PID/start identity, fresh noncompiling/nonupdating state, expected code values, request-matched error-free refresh and changed generation/assembly MVID. Selected asset inspection also completes. Installed packages and the historical fixture probe are unchanged; no MCP/Unity CLI installation or modification is performed.

After normal Editor shutdown, A checkpoint/release runs before B checkpoint/release. The original Library is then restored. The historical Codex result trees are reused as independent-result oracles; Codex is not rerun.

## Observed time — UR-02

| Stage | Earlier Splinter, s | Current Splinter, s |
| --- | ---: | ---: |
| Library preparation | 272.9366 | 240.3283 |
| A ensure | 381.1842 | 322.3461 |
| B ensure | 385.2465 | 319.4375 |
| A/B initial compiled readiness | 1,753.4893 | 1,041.8831 |
| A/B edit/refresh to compiled readiness | 554.4164 | 76.9129 |
| A checkpoint | 5,723.6083 | 83.5010 |
| A release | 1,662.4729 | 130.5857 |
| B checkpoint | 2,914.6685 | 81.6405 |
| B release | 1,163.3742 | 125.6494 |
| A/B preservation/release interval | 9,367.0703 | 421.3806 |
| Original Library restoration | 1,473.2436 | 1,404.9756 |

The complete observed interval from Library preparation through restoration is **4,107.9591 seconds (68.47 minutes)**, including asset inspection (2.4186 s), shutdown, orchestration and the retained stop timeout/observation. Final independent closure auditing follows that interval. Exact costs and binary hashes are in the [dataset](../../reports/benchmarks/unity-lifecycle-remeasurement-20261007.json).

![Recorded Unity timing comparison](../images/unity-lifecycle-remeasurement.png)

The previous **2h 36m 7s** preservation interval included overlapping B work, an external B source commit, a retained refusal/recovery and concurrent diagnostics. The new interval is sequential A checkpoint/release, then B checkpoint/release. These are recorded user-visible costs, **not an isolated causal speedup experiment**. Nested source/seal times must not be added to command totals. Setup, asset inspection, Editor shutdown and final Library restoration are separate from preservation/release.

Both normal-stop commands returned `EditorStopTimeout` after about 120 seconds. The same PID/start identities were observed to normal exit after **278.3213 seconds**, without repeating stop, killing or restarting Editors. Preservation then continued on the same tasks. The failed stop responses remain evidence; this run does not establish that the two-minute stop command completes successfully on this input.

## Remaining costs and opportunities — UR-03

| Command | Total, s | Source inspections, count / s | Outer seals, count / s | Delta reclamation interval, s |
| --- | ---: | ---: | ---: | ---: |
| s-a-checkpoint | 83.5010 | 2 / 25.4668 | 2 / 43.9624 | — |
| s-a-release | 130.5857 | 1 / 12.8073 | 3 / 55.2250 | 53.6194 |
| s-b-checkpoint | 81.6405 | 2 / 25.0146 | 2 / 42.7075 | — |
| s-b-release | 125.6494 | 1 / 12.4218 | 3 / 53.6273 | 53.3478 |

The reclamation interval is measured between durable `release-delta` and `release-snapshot` events; it includes its inner seal, separate from the three outer seal measurements.

The source inspections use the new two-inspection checkpoint path and one-inspection release path. Checkpoint hashes only the changed source file; the following inspections reuse all 1,602 content hashes. Source metadata and Git guards still cost time even with zero payload hash input.

External `dotnet-trace` samples the first 60 seconds of each library/workspace CLI, A checkpoint's provider, one initial-read window and one late B release window. An additional A workspace shutdown window overlaps early A checkpoint and is not treated as a pure shutdown profile. Library restoration also has a later window. All profile overhead is included. Samples contain overlapping active and waiting threads: their accumulated residence is neither CPU time nor additive wall time, and bounded windows do not apportion complete commands.

1. **Repeated path validation is a measured checkpoint/release target.** In the late B release window, `WorkspaceStore.EnsurePlainPath` occupies about **41 seconds of accumulated sampled residence**. The call repeatedly checks ancestors across seal construction and reclamation. A scoped parent/root identity proof could share this work. A string-only path cache would weaken substitution/reparse protection; retain pinned identity, membership checks and restart validation. See [path checks](../../src/Splinter.Runtime/WorkspaceStore.cs), [seal construction](../../src/Splinter.Runtime/LatestJournalStorage.cs) and [reclamation](../../src/Splinter.Runtime/LatestJournalReclamation.cs).
2. **Seal/read fusion remains possible, but payload hashing alone is not the whole bottleneck.** Each seal reconstructs and validates capsules, then reads physical files again into the composite digest. Reclamation also repeats file hashing at several boundaries. Fuse passes inside an operation while retaining the exact seal framing, per-frame checksums, before/after/post-unmount boundaries, final head/membership/pending checks and same-intent recovery. Current fences permit delete sharing, so removing predelete validation without conditional file identity protection is unsafe.
3. **Initial loading has a separate read/lock bottleneck.** In a 60.02-second A provider window, `GitObjectSnapshotTree.Read` occupies about **59.91 seconds** and Monitor entry accumulates **433.15 thread-seconds**, chiefly in file-info, read and open callbacks. The code holds the journal gate through base reads; missing/inlined frames prevent identifying the precise Monitor object from the trace alone. The Git reader retains one object's proof vector, so A→B→A reads can reprove all of A; backward ranges can decode the prefix again. A bounded metadata-only proof LRU is the first candidate, followed by bounded reader sessions or a separately designed read-plan/gate change. Keep complete blob proof on misses, chunk checks on rereads, no publication after corruption and no disk hydration. Logical returned `readBytes` do not measure decoded-byte amplification; explicit proof/prefix/restart counters are needed. See [Git reader](../../src/Splinter.SourceControl.Git/GitObjectSnapshotTree.cs) and [journal read gate](../../src/Splinter.Runtime/JournalDeltaStore.cs).
4. **Unchanged original Library restoration still takes the general per-file path.** The original provider records zero durable mutations, yet the current restore builds a full file plan and moves/verifies each retained file with a durable cursor. The code performs several whole-payload traversals even for unchanged files. A proven empty-original-delta path could restore the entire owned directory by a same-volume move. It must preserve exact root/target identity, absent dependents/readers, pre/post-unmount seal, no foreign target, durable intent and crash recovery. This is a code-backed opportunity, not implemented or qualified by this measurement. See [restore](../../src/Splinter.Platform.Windows/ManagedDirectoryRestore.cs) and [basis lifecycle](../../src/Splinter.Platform.Windows/ManagedDirectoryBasis.cs).

No further product optimization is implemented in this measurement task.

A zero heartbeat sequence alone is insufficient for the directory-move path. It needs a fully validated stopped journal and descendant identity/content proof, not just root identity; physical last-access metadata can change without logical mutations. An O(N) verification/metadata pass may remain. Never convert an already-started general restore intent into a new fast path.

For the general restore path, a narrower option is to publish the recoverable progress cursor in bounded batches. Existing recovery already handles a file moved before its cursor advances. Batching 128 completed files would reduce approximately 42,420 cursor publications to 332, at the cost of revalidating up to 127 extra completed files after interruption. New-file ownership receipts, range-data flushes, final verification and changed-retained-byte/identity guards must remain. This is a design estimate of publication count, not a measured speedup; interruption coverage is required before adopting it. The later restore profile observes both repeated path checks and durable flush work.

## Closure and limits — UR-04

Both tasks reached `released` with independent results whose trees exactly match the historical Codex A/B results, each changing only its intended source file. Owned Editor/provider processes, mounts, handles, memory, deltas and Git workspace registrations were reclaimed. All 1,602 original source hashes, HEAD/branch and preexisting refs remain unchanged. The original Library is restored to ordinary NTFS on its original volume; its temporary local Git setting is removed and global configuration is unchanged. No UAC, elevation or service modification occurred.

The frozen response schema accepted 8 of 12 actual CLI responses and rejected the four checkpoint/release responses: final unlinked close already emits `forget-unlinked`, but both metric schemas allowed only six operations and omitted that enum value. After measurement, the two schemas were corrected to admit the seven existing operations. A new real-journal regression covers all seven and still rejects unknown names. All 12 recorded responses, including the two stop failures, pass the corrected schema. The measured binaries remain unchanged; this is a separate contract correction, not performance work.

The previously completed required build recorded **1,203 passed, 0 failed, 19 skipped** for the frozen product candidate. After the separate schema correction, `pwsh ./scripts/build.ps1` records **1,204 passed, 0 failed, 19 skipped**, with no build warnings/errors. The recorded-response validator additionally passes for all 12 responses. Frozen execution evidence and post-measurement contract regression are distinct qualifications. There is one cohort per candidate, no cache clearing, statistical interval or general speed guarantee. **New Unity Play Mode, Unity tests, game build, storage allocation, RAM/whole-disk peak, service/UAC work and a fresh Codex run are NotRun.** The old storage saving remains historical evidence, not a newly measured allocation claim.
