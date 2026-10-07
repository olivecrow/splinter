<a id="sf-02-명시적-저널-공간-정리-검증"></a>
# SF-02 explicit journal-space compaction verification

[한국어](../ko/verification/journal-compaction.md)

Execution date: 2026-09-26, Windows x64/NTFS. Scope is explicit compaction before result collection in the [fixed first-stage plan](../ntfs-workspaces.md). Existing [common-storage execution](storage-foundation.md) retains `FailedPreservation`. Overall SF-02 separates evidence for measurement/compaction subsets from flush-policy/failure scope.

<a id="자동-계약과-중단-재개"></a>
## Automated contracts and interruption resumption

`JournalDeltaStore` replays existing acknowledged frames, then writes only live payload ranges to a new candidate in chunks. Tests compare 16MiB full rewrites/live 4MiB after truncate, `.meta`, rename/delete, changed paths/sequence with fake snapshots, checking journal-allocation decreases on the test volume. Candidate flush/replay/state hash/before-after seals are checked before `File.Replace` switches generations. Interruptions injected at intent recording, candidate flush, prepared receipt, and immediately after publication retry the same identity. Corrupt candidates reject without changing original journal. Verified previous generations are cleaned even while reconnected provider has its journal handle open. Coordinator fakes check single Editor/process/handle observation, independent state of another workspace, and same-attempt return on repeated compact. Full `pwsh ./scripts/build.ps1` locked restore/Release build had 0 warnings/errors. One process-supervisor test overlapping first Unity import timed out; after reclaiming Editor/VHD, full `pwsh ./scripts/build.ps1 -NoRestore` rerun returned **262 passed, 0 failed, 1 test excluded at that time skipped out of 263**.

These are deterministic boundary tests simulating process interruption. Actual forced power loss, physical-space exhaustion, or reboot journal-generation recovery are not passes. Existing per-write `Flush(true)` policy is unchanged.

<a id="격리-ntfsgit-제품-경로"></a>
## Isolated NTFS/Git product route

Existing test-owned 16GiB dynamic NTFS VHDX was reattached using exact image/disk ID/volume path. New 1MiB synthetic Git/small Unity fixtures created under `V:\sf-compaction-20260926`. Original C: VSS settings unchanged. Restricted administrator session fixes two owned IDs/helper hash and only Capture/Observe/Release; ordinary CLI non-administrator. This is neither product privilege-service implementation nor UAC-elimination evidence.

Synthetic workspace `7a660f001fa171305b92d76e` actual WinFsp mount overwrote the same 4KiB region 100 times. Original SHA-256 remained `D0B5218892F200A1CDD8AA5642FEAD94F8173F67CE19B0B74899D9F0FC6861CB`; mount hash changed independently. Initial compact required fresh VSS observation after provider stop and returned `SnapshotPrivilegeRequired`. After same-ID Observe helper, retrying identical command yielded:

| Journal generation | Logical B | NTFS allocation B | Durable sequence |
| --- | ---: | ---: | ---: |
| Before compaction | 453,147 | 454,656 | 102 |
| After compaction | 48,138 | 49,152 | 102 |

Two-generation allocation sum at switching was 503,808B: overlap of two journal files, not actual whole-workspace peak. Logical decrease 405,009B is distinct from allocation decrease 405,504B. New-mount file hash matched pre-compaction work-view hash; original hash unchanged. Previous journal/intent disappeared after reconnect confirmation. Same-request repeat did not alter attempt/seal. Subsequent edit/Git commit `70dc0deebcf65c5f21bb2817c49bc52e7377ba2e` preserved independent result ref, then this owned task snapshot/mount/delta reclaimed. See [compaction response](../../artifacts/storage-foundation/compaction-20260926/compact-1.json), [reconnect comparison](../../artifacts/storage-foundation/compaction-20260926/reconnected-check.json), [release](../../artifacts/storage-foundation/compaction-20260926/release-source-1.json).

Second Unity workspace connected concurrently with another ID/snapshot. After synthetic-task compaction, Unity mount allowed separate file writes/reads absent from original Unity Git source. This shows another task's mount persistence/isolation, not two-Editor concurrent performance/large PV comparison.

<a id="작은-unity-저널과-재개"></a>
## Small Unity journal and resumption

Separate owned workspace `d97e7152babb9e543ea02737` opened a small Unity 6000.3.22f1 fixture on actual WinFsp mount. First-import Editor log records `Project Loading completed in 1183.380 seconds` and actual scene/texture/FBX imports. Package imports continued after loading, so Splinter bridge heartbeat was not issued. Product `unity stop` returned `EditorStopTimeout`. Exact owned PID/start time/Editor executable/workspace path compared before terminating only owned Editor; product state confirmed 0 children/mount handles. This operational intervention is not a normal-shutdown pass.

First `workspace compact` stopped at `SnapshotPrivilegeRequired`; after restricted Observe of owned snapshot, same command retried. First connection after candidate publication returned `OverlayStartUncertain`; status confirmed same snapshot/new-generation provider alive, then identical command completed.

| Unity journal generation | Logical B | NTFS allocation B | Durable sequence |
| --- | ---: | ---: | ---: |
| Before compaction | 2,347,144,214 | 2,347,147,264 | 422,868 |
| After compaction | 1,721,650,294 | 1,721,651,200 | 422,868 |

Logical decrease 625,493,920B; actual journal-allocation decrease 625,496,064B. Two-journal allocation sum during generation switching 4,068,798,464B includes only those two files. This is not Library/Git/control/shared-VSS peak and is not summed with small synthetic-journal savings. After reconnect, previous generation/intent absent; original/other-workspace snapshot IDs remained. See [compaction result](../../artifacts/storage-foundation/compaction-20260926/unity-compact-3.json)/[before-compaction allocation](../../artifacts/storage-foundation/compaction-20260926/unity-allocation-before.json).

Same Editor reopened on compacted mount; `ProjectInfo` `projectLoad=65.454` seconds/same Unity version/original snapshot/task ID confirmed. Restart log `Library/ScriptAssemblies` `FileNotFoundException` occurred on script-compilation path; no bridge heartbeat, `application.ready=false`. Unity script validation/normal shutdown therefore remain failed/incomplete. Same owned PID/start/path compared before terminating Editor; processes/handles 0 confirmed. `Assets/CompactionPeer.txt`/`.meta` read retained after compaction; only these two files committed to owned branch. This is not a Unity-validation-passing product checkpoint.

<a id="마감-장부와-남은-경계"></a>
## Closeout accounting and remaining boundaries

| Gate | Observation/assessment |
| --- | --- |
| Synthetic product-mount compaction/resumption/independent result | Passed: subsequent editing on same snapshot, Git result `70dc0deebcf65c5f21bb2817c49bc52e7377ba2e`, release completed |
| Small Unity actual load/compaction/restart | Partial: first/restart loads, 625,496,064B journal-allocation decrease/same sequence confirmed. Bridge ready/script validation failed |
| Unity independent result/owned resources | Passed (operational intervention): two-asset Git result `9d6335bf6f6b1efbfb6d7924b6ec5dc1ab5f3389`/`stable-journal-v1` ref preserved; owned processes/handles/mount/delta/snapshot 0. Editor terminated after exact owned identity comparison |
| Isolated-volume shutdown | Passed: both tasks `released`; restricted administrator session confirms 0 for two owned VSS IDs, then VHD detached. `V:` absent |

Unity original Git HEAD `58f81b5bc6ff8a940d8a90872bd3fed858a54cc1` retained; result ref `refs/splinter/snapshot-results/d97e7152babb9e543ea02737` points to `9d6335bf6f6b1efbfb6d7924b6ec5dc1ab5f3389`. Evidence: [Unity release response](../../artifacts/storage-foundation/compaction-20260926/unity-release-2.json), [snapshot release](../../artifacts/storage-foundation/compaction-20260926/response-release-unity.json), [VHD detach](../../artifacts/storage-foundation/compaction-20260926/response-finish.json).

VSS Used/Allocated are volume-shared observations, not per-task savings. Total first-import peak including Library/control/Git/independent results was not measured. Direct times/read bytes for journal scan/candidate writing/flush/seal were not separately measured. Provider reconnect replay in status metrics is not total compaction cost. Power failure, non-owned backup coexistence, arbitrary-order immediate snapshot reclamation, 100GB projects, and single-Editor candidates are outside scope. Existing [common-storage execution](storage-foundation.md) `FailedPreservation` remains.
