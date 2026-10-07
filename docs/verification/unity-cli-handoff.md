<a id="unity-cli의-정확한-ab-인계-검증"></a>
# Exact A/B handoff verification with Unity CLI

[한국어](../ko/verification/unity-cli-handoff.md)

The small Windows x64/NTFS Plastic input on 2026-10-03 closed **Unity CLI operations targeting returned paths, B follow-up work after A normal shutdown/reclamation, two independent results, and final snapshot reclamation** as scoped-complete-with-limitations. Existing connection procedures ran without program changes/package replacement. Worker omission in `status`/`pipeline list` and first-eval timeout remain recorded; this does not extend to automatic discovery or general support for other tools/inputs. Execution scope follows the [handoff plan](../plans/session-workflow-and-installation.md#follow-up-scope-unity-cli-ab-handoff); current usage follows [Unity workspaces](../alpha-workspaces.md#exact-target-selection-with-unity-cli).

<a id="고정-입력과-실행-근거"></a>
## Fixed inputs and execution evidence

| Item | Executed scope |
| --- | --- |
| Product | `development @ 57d4439`; program source identical to `4e262b7`. This change covers guidance/verification documents; actual build-file hashes in `artifacts/unity-cli-handoff/product-binaries.json` |
| Tools | Unity `6000.3.22f1`, installed Unity CLI `1.0.0-beta.5`, existing Pipeline `0.8.0-exp.1`. Local version/help/returned command schema checked. No new installation/update |
| Original/home | Existing `T:\plastic-fixture-20261002`/`T:\pt1002`, `source=plastic`, `application=unity`, `base=snapshot`, journal budget 17,179,869,184 bytes. Existing regular/static registration/service; no new grant/source copy |
| A | key `cli-t-a-20261003`, id `78cc98798c232f2872f8a9dd`, digest `6eee03de35a50713f8d4416e`, returned root `T:\pt1002\workspaces\78cc98798c232f2872f8a9dd`, PID 90496 / start ticks `639265517648992204`, Pipeline 7801 |
| B | key `cli-t-b-20261003`, id `bbe46e538e9fe1e10a5d3d7c`, digest `5a20e9c46eee3aaf0af8fcc6`, returned root `T:\pt1002\workspaces\bbe46e538e9fe1e10a5d3d7c`, PID 53928 / start ticks `639265517999023455`, Pipeline 7802 → 7801 |
| Basis | Both task baselines `sha256:8a58a18cac8d41149bafa58e886dcd25b78fd8162ea7cd5b19e9533fef0c9e0b`, 62 files / 245,523 bytes. Includes previous verification marker in T: original; original not edited in this task |
| Original Editor | C: Plastic project PID 68624, start `2026-10-02T21:34:17.425636+09:00`, Pipeline 7800 ready. Not started/stopped/reclaimed |
| Raw evidence | `artifacts/unity-cli-handoff/`: product/tool JSON, per-stage times, schemas, before/after source/registration hashes, selected process identifiers, build/TRX/binary hashes, independent-object verification, read-only administrator VSS/ledger/grant comparisons |

Exact PID/start-tick matches in Splinter launch receipts/heartbeats were checked separately. Large integer ticks returned by Unity CLI eval can lose precision through CLI JSON number processing; those numbers were not used for exact lease comparison. Agents querying this value should return a string, such as C# `Ticks.ToString()`. Authentication tokens, full command lines, and license values are excluded from public evidence.

<a id="실제-인계와-실패의-구분"></a>
## Distinguishing actual handoff from failures

| Gate | Assessment and actual result |
| --- | --- |
| UH-01: exact connection/discovery | Passed with limitations. A/B `command --project-path <returned root>` catalog/target and eval actual project/PID checked. Both `status --project-path` returned STATUS_NO_INSTANCES; ordinary-permission `pipeline list` returned only original. Same limits checked outside sandbox; omission not treated as Editor absence |
| UH-02: editing/loaded results/normal-shutdown coexistence | Passed with limitations. Each `Assets/SplinterCliProbe.cs` `a-one`/`b-one`, A edit `a-two`, follow-up `b-two` on same B PID after A stop/release, external recompile completed/failed=false checked. B value/MVID unchanged around A edit. Original source/management data/registration/Editor/Pipeline connection preserved |
| UH-03: independent results/reclamation | Passed. Two checkpoints/releases, actual object hash/size/final probe bytes checked; mount/provider/delta/owned processes/handles/memory 0 or absent; final two owned VSS snapshots reclaimed, T: snapshots 0. Existing C: pending state not inherited as reclamation success |
| UH-04: existing contracts/costs | Passed with limitations. Required locked restore/Release build 0 warnings/errors; 844 automated tests passed/0 failed/1 test excluded at that time skipped. Current embedded catalog validates 28 product JSON responses. Stage times/range storage/independent-result costs recorded; total physical peak/RAM peak/savings/per-workspace VSS attribution unknown |

`a-catalog-initial.json` returned COMMAND_FAILED before descriptor publication. First eval for each Editor and first eval after A recompile returned `Main thread operation timed out after 5000ms`; **one retry of the same read-only target eval** confirmed loaded values/actual targets. Failures remain and are not marked as first-attempt passes. Internal cause of initial timeout is unconfirmed; this does not qualify automatic retries of general Unity operations/write requests.

A initial probe MVID is `597a3b2d-ee72-4ff7-8617-7b1c4b67fec6`; after editing, `9c839321-1df7-4a6f-95b1-b1ad544484de`. B retained `59385ca1-487b-4fcd-9dc4-5c7755edf644` during A editing/stop and changed to `62d4f4b6-55dc-4c52-b9b3-50e56ab54f4c` after follow-up editing. When B reload changed port from 7802 to already-stopped A's 7801, CLI targeting the returned root connected to the same B PID. Port numbers are not persistent task identities.

<a id="독립-보존보호와-비용"></a>
## Independent preservation, protection, and costs

| Item | Final evidence |
| --- | --- |
| A result | `sha256:d1554d89caf3435549ebe8c85e6a6a6fa87ad850fb57c96a9582ad2162bca54a`, 64 files / 245,661 bytes, 64 unique objects. Includes `a-two` source/meta |
| B result | `sha256:ea3034db302fc699605a80b7b7c222d9bb86ed4842d2f8711f2afaeebcdb4f13`, 64 files / 245,661 bytes, 64 unique objects. Includes `b-two` source/meta |
| Reclamation | A `{4E09CAEF-7771-4962-B99A-7CECC79F800C}`, B `{CBCF82ED-583F-4898-AE8B-B589F3AB4C60}` reclaimed. `18-a-final.json`/`19-b-final.json`: both released, snapshotAvailable/mounted/providerAlive/deltaPresent=false, openHandles/owned activeProcesses/workingSet/privateBytes=0 |
| Existing protection | Before/after C:/T: selected-source/`.plastic` content comparison; 5 existing Plastic registrations identical. Read-only UAC inventory retains hashes of 6 existing C: snapshots/1,418 protected ledger files and 7 exact grants. Preceding C: A reclaim-pending/B discard-reclaim-pending observed only |
| Ordinary-permission stage times | ensure A 30,619ms/B 32,110ms, A stop 58,663ms/checkpoint 10,025ms/release 14,290ms, B checkpoint (including normal stop) 49,842ms/release 20,879ms. Ordinary workspace CLI callerElevated=false. No administrator Apply/installation; only final protection comparison used one read-only UAC session |
| Range storage | Both ensure initialPayloadCopiedBytes/journalBytes/payloadBytes/readCacheBytes=0. Cumulative A journal/payload immediately before reclamation 136,298,076/123,511,979 bytes; B 136,120,355/123,349,610 bytes. Includes Unity Library/management-file writes; not current delta allocation after reclamation |
| Final file allocation | `storage-final.json`: all records in same home 1,747,696 allocated bytes/335 files. Includes previous PC results/records, not costs of this A/B alone. Independent file-result payload within it: 932,176 allocated/736,837 logical bytes, already included in records. Shared runner 7 files 1,139,032 allocated bytes; original T: Library 1,852,496,112 allocated/1,819,103,541 logical bytes, 30,748 files, complete=true is a separate baseline |
| Shared VSS | Final T: used/allocated=0, limit 10,737,418,240 bytes. C: used 27,434,254,336/allocated 27,931,082,752/limit 39,985,348,608 bytes is existing shared space, not attributed to this task |

Actual result verification directly read all objects in `files/baseline.json`/`result.json` to check SHA-256/size after worker paths/snapshots were reclaimed. Original Library, other records, shared runner, independent results/journals are not double-counted. VSS observation failure in ordinary measurement shell is not read as 0 and is distinguished from actual administrator-inventory 0 above. Costs are observations of one bundle, not time SLAs or storage/memory saving rates.

<a id="유지하는-제한과-다음-판단"></a>
## Retained limits and next decisions

This normal path is a **small T: Plastic input using exact returned paths through current CLI/Pipeline**. Automatic-list-discovery fixes/CLI updates, Coplay/MCP connection/shared-server coexistence, separate full Git-fixture handoff, Play/Test/build/capture, original integrate/check-in, large/ARM/OS failures, new installation/binding, and development view did not run. It does not mean Splinter provides an external compilation-success gate.

The two preceding owned C: snapshots remain pending behind existing non-owned VSS blockers. T: reclamation is not marked as resolving them; other tasks' snapshots are not deleted to bypass them. If list discovery is essential for a new tool/version/input, first fix the scope around current failure/minimum required changes. This representative A/B unit and closed M0 are not automatically repeated.
