<a id="ntfs-스냅샷과-범위-저장-실험"></a>
# NTFS snapshot and range-storage experiment

[한국어](ko/snapshot-overlay.md)

Current commands use the [separate development CLI](development-cli.md). They provide storage-candidate/source-cache/seal/reclamation contracts; AI/external tools own project work and result evaluation.

This is the **independent storage-experiment entry point** contract. The ordinary product backend using the same VSS/WinFsp/journal implementation is separately connected in [NTFS workspaces](ntfs-workspaces.md). [SO records](verification/snapshot-overlay.md) own SO-01–06 fixture evidence in one Windows/NTFS environment; [NI records](verification/ntfs-workspace-integration.md) separately own product NI status. No original move is required.

<a id="구성과-저장-계약"></a>
## Composition and storage contract

Immutable candidates and separate source/cache journals for `candidate-export`, registration-evidence-dependent `candidate-source-export`, and `overlay-serve-request.sourceView` follow the [fixed source-view contract](source-view.md). Publication input requires fixed `publication.requestKey`; interruptions reconcile only through same-request intent/original seal. This development experiment switches candidates at the same path after normal Editor/provider shutdown; it is not advertised as a product workspace mode. [VC records](verification/immutable-validation-view.md) separately own defined A/B/A execution and creation/resumption/switching costs.

Shared VSS helper blocks release with `SnapshotReleaseIsolationRequired` and records `release-isolation.json` if other older/same-time/unknown-time snapshots exist on the same volume. Because [actual failure](verification/storage-foundation.md) lost other snapshots even when deleting only an exact ID, do not delete existing snapshots to bypass blockers. Current guards were verified with [isolated owned snapshots](verification/storage-recovery.md). General unowned-backup coexistence/concurrent external-deletion preservation remains incomplete. New source-change generations/journal I/O counters follow [common source verification and instrumentation](source-verification.md).

| Component | Current responsibility |
| --- | --- |
| Splinter.Core/SnapshotOverlay.cs | Platform-neutral snapshot/tree/delta/mount ports, observations, independent-result/dependency-release conditions. |
| Splinter.Runtime/JournalDeltaStore.cs | SHA-256 append-only range/metadata journal, synchronous flush, range composition, truncate/rename/delete, same-identity replay. |
| VssSnapshotTree.cs | Read only persistent differential system VSS devices; no original fallback; fail on loss. |
| WinFspOverlayFileSystem.cs / WinFspOverlayServer.cs | Windows-path file/directory/mmap I/O, provider process, handle/memory/journal state. |
| WinFspOverlayClient.cs / WinFspOverlayMountProvider.cs | Common mount port, immutable runner, PID/start time/launch generation, same-snapshot/delta reconnect, flush/unmount. |
| GitOverlayRegistration.cs | No-checkout linked worktrees, independent indexes, result commits/refs, exact owned-registration reclamation. |
| OverlayExperimentCommands.cs | Schema validation and Git/Unity/Windows composition for snapshot-spike JSON CLI. |
| scripts/vss-snapshot.ps1 | Administrator capture/observe/owned-ID release helper. VssSnapshotProvider connects product receipts/privilege-resumption boundaries. |

Creation copies no source payload. Reads compose snapshot/existing journal ranges without persistent cache. Partial writes store only those ranges. Acknowledged frames use Flush(true). Replay removes only an incomplete final frame. Complete-frame checksum/sequence/JSON/path-range metadata errors and damaged identity JSON preserve files and fail with DeltaCorrupt. No arbitrary corruption repair, automatic recapture, or whole-copy fallback exists.

The original remains writable during work. VSS ClientAccessible provides writer-free filesystem crash consistency, without capturing unsaved Unity memory. Tasks share Library at the same capture point and journal only actual per-task writes. Snapshot ID/device path remain fixed; reconnect does not substitute a new capture.

<a id="명령과-상태"></a>
## Commands and state

`scripts/invoke-snapshot-spike.ps1 -OverlayRequestPath <persisted-request> -Action <action>` wraps `snapshot-spike --input` JSON CLI. It does not automatically capture/install. Inputs follow contracts/v1/snapshot-spike-request.schema.json and overlay-serve-request.schema.json; provider state follows overlay-observation.schema.json.

| action | Conditions and result |
| --- | --- |
| mount / status / unmount | Same identity/delta/paths. Applications/clients defer reclamation. Check PID/start time/token. |
| git-prepare | Requires clean checkpoint, new branch, -ReceiptPath, -Revision. Register without checkout before mount. |
| git-attach | Attach private pointer/independent index to a mount excluding .git basis reads. Original index is neither copied nor shared. |
| unity-start / unity-status | Exact Unity version, dedicated Job Object, owned bridge. Determine ready from fresh heartbeat. |
| unity-stop | Normal shutdown followed by all Editor/child exits and zero owned-memory observation. |
| preserve | After application exit, preserve Git source-result commit and refs/splinter/snapshot-results/overlay-id. Exclude bridge/generated caches. |
| git-release | After absent mount and matching preserved revision, remove only exact Git registration. Retain result ref/branch. |

control/state.json phases are replaying, mounted, snapshot-missing, snapshot-unknown, unmounted, failed. Running provider/CLI use the same snapshot probe. snapshotAvailability is available/missing/unknown; unknown is not stored as snapshot-missing. CLI returns processAlive, snapshotAvailable, and mountPresent together; unknown booleans are null. Different launch-intent/receipt generations produce OverlayStartUncertain to prevent duplicate launch. A live matching new launch with only older-generation state is observed as starting, without estimating resources as 0. Mount/reconnect waits at most 2 minutes for exact PID/start time, actual mount, and mounted state no older than 5 seconds. Exceeded limits/cancellation preserve the same provider/journal. stop.json/flush.json require the same token. After unmounted, also check actual absent process/path.

WinFsp Cleanup counts closed user handles. Kernel retainedFileContexts may remain until Close and are separately observed. Normal unmount followed by provider exit reclaims its memory too. Open user handles prevent forced unmount.

<a id="unity-경계"></a>
## Unity boundary

Additional selected-model/texture/PNG checks follow the sibling fixture's [preview-inspection contract](unity-render-qualification.md). `inspect` in `qualification-command.json` is explicit fixture code/render inspection. Product provides neither refresh nor acceptance decisions. Read PNG path from `assets.previewPath` in the same request response.

Only .git is excluded from basis reads. Library is neither excluded nor deleted for full regeneration. Before execution, whiteout only private-overlay Library/ilpp.pid and Library/Pipeline/.unity-pipeline-port, because reusing original process addresses could interfere with original compiler helpers. External-package-specific run-state/PID files are unchanged and managed by that tool/agent.

The experiment's owned bridge also handles only execution observations/normal shutdown. Check refresh, compilation, loaded-code/render evidence through external tools and explicit fixture inspect. Initial fixture output/Editor readiness is not reused as acceptance of latest code.

<a id="재현-순서"></a>
## Reproduction sequence

Installation/VSS settings changes require separate authorization. setup-snapshot-overlay.ps1 defaults to Plan; -Action InstallWinFsp installs only WinFsp 2.2.26215 Beta4 Core after official SHA-256/NAVIMATICS signature checks, without automatic reboot. set-snapshot-test-budget.ps1 defaults to Plan; authorized -Apply only increases C: maximum to 64GiB. Check 8GiB diff-area headroom before capture; do not automatically shrink after experiments.

Run in new paths. Historical evidence directories' source/delta/snapshots were reclaimed and cannot serve as rerun inputs.

```powershell
pwsh scripts/build.ps1
$run = Join-Path $PWD 'artifacts/snapshot-overlay/new-run'
pwsh scripts/prepare-snapshot-overlay-test.ps1 -Action Source -RunDirectory $run
# Perform Capture in an authorized administrator PowerShell.
pwsh scripts/vss-snapshot.ps1 -Action Capture -SourcePath "$run/source" -OwnerId so-new-run -ReceiptDirectory "$run/snapshot-t0"
pwsh scripts/prepare-snapshot-overlay-test.ps1 -Action Overlays -RunDirectory $run -SnapshotReceipt "$run/snapshot-t0/snapshot.json"
foreach ($name in 'a','b') {
  pwsh scripts/invoke-snapshot-spike.ps1 -OverlayRequestPath "$run/control-$name/request.json" -Action mount
}
pwsh scripts/test-snapshot-overlay-io.ps1 -RunDirectory $run
pwsh scripts/measure-overlay-small-writes.ps1 -RunDirectory $run
pwsh scripts/test-snapshot-overlay-concurrency.ps1 -RunDirectory $run
pwsh scripts/test-snapshot-overlay-compatibility.ps1 -RunDirectory $run
```

After original edits, capture new snapshot-t1 and prepare C through prepare-snapshot-overlay-test.ps1 -Action Overlays -Names c. Check new marker/namespace in C. test-snapshot-overlay-faults.ps1 creates fault/quota from t1, reproducing retained handles, forced provider termination, and quota 112. preserve-snapshot-test-results.ps1 preserves a/b/c/fault/quota changes as separate files/SHA-256 manifests. Export costs are separate from initial workspace copying. test-overlay-journal-recovery.ps1 injects incomplete/checksum failures into A only after preservation. test-owned-snapshot-loss.ps1 deletes only owned t1 through administrator InjectLoss after independent-preservation verification. Follow each script's param block for exact arguments.

For Unity, capture the actual fixture in a separate run. -Action Overlays -Unity creates A/B requests excluding .git with 16GiB journal limits. For each request execute git-prepare -Branch <new-codex-branch> -Revision <checkpoint-SHA> -ReceiptPath <run/git-a.json> → mount → git-attach → unity-start. Edit original/A/B differently and check target-Editor refresh/new-code execution. Record allocation/provider-OS-cache/volume through measure-snapshot-overlay.ps1 and application Job resources through unity-status.

After fixing results, run unity-stop and preserve-snapshot-overlay-unity.ps1 -RunDirectory <run> -Name a and b. This wrapper connects source preservation → normal unmount/provider exit → stopped-journal accounting → Git-registration reclamation → manifest. Unmount/accounting failure blocks subsequent registration reclamation, preserving independent source results/journal. Normally unmount every synthetic fixture too. reclaim-snapshot-overlay-test.ps1 defaults to read-only verification; -Reclaim deletes only delta after checking result hashes/refs, identical durable sequence, and absent processes/handles/mount/Git registration. Already reclaimed deltas reconcile after interruption only with matching reclaim intent. Final administrator release-snapshot-overlay-test.ps1 -RunDirectories <runs> -OutputPath <report> reclaims only those snapshot IDs and verifies preservation of existing IDs. Explicitly clean random source/runner caches after snapshot reclamation.

<a id="비용과-미검증-범위"></a>
## Costs and unverified scope

Journal limits are maxJournalBytes, 250,000 nodes, 1,000,000 extents, and 250,000 whiteouts. One transaction is at most 1MiB. Retain 32 VSS read handles and 32,768 metadata/negative-cache entries. File-content cache is 0. Use 8 WinFsp callbacks, each with at most a 1MiB buffer. Excess limits produce DeltaFull/OverlayMetadataLimit without automatic deletion/copy-up/compaction. Historical payload from repeated writes also counts as cost.

initialPayloadCopiedBytes, journalBytes, payloadBytes, metadataBytes, readBytes, and readCacheBytes are engine counters. measure-overlay-journal.py aggregates acknowledged writes in Library/Temp/Logs etc. by actual requested paths. Actual file-data allocation is FILE_STANDARD_INFO.AllocationSize, excluding full MFT/directory costs; also consider volume free space and VSS used/allocated space. Shared OS cache is neither per-task permanent copying nor owned-process memory.

ADS, hardlink identity, reparse/EFS/compression/sparse, individual ACL changes, ordinary dirty/LFS checkpoints, power loss, and post-reboot snapshot identity verification are unsupported or unverified. Reparse/EFS/compressed/sparse attributes are rejected before changes in basis reads and create/overwrite/attribute callbacks. Automated callback tests do not prove full actual-mount file-API support. Host physical disk exhaustion was not run; quota verified Windows disk-full 112. Snapshots also include out-of-project writes and existing restore-point costs. This experiment is not a security sandbox.

<a id="개발-요청의-필수-증거"></a>
## Required evidence for development requests

`prepare-snapshot-overlay-test.ps1`, `test-snapshot-overlay-faults.ps1`, and `test-snapshot-overlay-compatibility.ps1` use common generators in `scripts/snapshot-overlay-request.ps1`. Current v2 capture receipts and same-request live observations populate all of `readAccess`, `snapshotReceiptDirectory`, and `snapshotOwnerId`. Mount performs the same VSS identity/current-boot/ownership verification as the product; path existence alone cannot bypass it. If a new observation is needed, first run `vss-snapshot.ps1 -Action Observe` against the exact receipt. Request generation performs no capture, mount, or original changes.

Internal module `scripts/internal/SnapshotOverlayIO.psm1` owns common instrumentation/range I/O. Test/measurement entry points import it and explicitly pass run paths. Module import performs neither type registration, file I/O, nor test execution; Windows helpers are prepared only for allocation queries. No `FunctionsOnly` test import is provided.
