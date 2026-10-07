<a id="변경-기반-소스-검증과-저장-계측"></a>
# Change-based source verification and storage instrumentation

[한국어](ko/source-verification.md)

The current ordinary-folder path for `--source none` and v2 state/service packages follows [VCS-free workspaces](vcs-independent-workspaces.md). Git paths and historical native qualification below do not qualify ordinary-folder support.

Product Git adapters for `ntfs-snapshot` and `git-snapshot` reuse per-file raw SHA-256 and Git content hashes bound to the same snapshot/provider generation/source-change history. File size, mtime, or Git stat cache alone do not establish content equality. Full-fingerprint path ordering and `source-fingerprint-v1` values remain. The diagnostic backend retains full raw reads.

Explicit whole raw hashing of range journals reuses a pooled 1MiB buffer one file at a time to limit read callbacks. Whole-file SHA-256, cancellation/failure, and before/after generation guards remain; payload is not stored on disk. Small files reuse this buffer too, without payload arrays proportional to file count.

<a id="검증-흐름과-소유-경계"></a>
## Verification flow and ownership boundaries

`unity start` and `workspace ensure --editor` observe execution state only. They do not require current-source checks or compile success after start. Editor-free ensure, explicit `--verify-source`/audits, checkpoint/integrate current-content checks, and result seals remain.

Core `ISourceChangeProvider`/`SourceChangeView` provide workspace ID, snapshot ID, provider epoch, last source sequence, and per-path last-change sequences. Runtime `JournalDeltaStore` derives this set from acknowledged journal records and rebuilds it during replay. Every new provider has a different epoch. Core references no Git/Unity/Win32 APIs.

The Windows adapter uses new request IDs in `overlay/source-query.json` and `source-response.json`. Before/after requests, check exact provider PID/start time/mount/snapshot identity. Historical source generations in periodic `state.json` are not verification barriers. Missing the corresponding response within 15 seconds preserves with `SourceObservationUncertain`.

Git adapter checks in this order:

1. Observe provider source generation, then enumerate current HEAD, managed paths, staged-change list, and all index entries. Do not use worktree `git status` to reread unchanged payload.
2. Check managed-path policy and effective Git attributes/config digest. Raw config is not stored in manifests/instrumentation logs. Unsupported filters/LFS/working-tree-encoding remain rejected.
3. Reuse raw hashes for unchanged files under the same snapshot/epoch/policy; hash the full raw content of changed files only. Directory renames also invalidate descendant paths. Add/delete/`.meta`/truncate/atomic replacement are detected through paths/generations and re-enumeration.
4. Reuse Git content hashes or compute through `git hash-object --stdin-paths` in batches of at most 256 paths. Pass UTF-8 paths using Git C quoting to preserve spaces/non-ASCII names and per-path attributes/EOL transformations. Exactly one valid hash must return per path. Compare with HEAD blob IDs to supplement `changedPaths`/`hasChanges`. Different content is changed even if Git returns clean at the same size/mtime. Changed attributes/config interpretation recomputes Git content hashes separately from raw hashes.
5. Recheck HEAD, path set, staged-change list, all index entries, interpretation digest, and provider generation. Mid-check changes produce `SourceChanged` without updating manifest/current-verification success.

During collection, apply per-path `git add --renormalize` to changed existing files so content hidden by stat cache also enters the index. Recheck raw fingerprint and actual commit afterward. New files/deletions retain the existing `git add -A` path. Collection `stable-journal-v1`, before/after/post-unmount seals, and independent-result verification remain required.

Writes in managed paths such as `Library`, `Temp`, and the owned Unity bridge do not advance source generation. Root-directory date/attribute changes are not source-content changes either. Root rename/delete/resize/payload writes remain rejected by storage contracts. The changed-path index allows at most 250,000 entries, blocking excess before acknowledged append.

<a id="manifest와-복구"></a>
## Manifests and recovery

`records/<id>/source-fingerprint.json` is an internal checksummed version 2 manifest storing exact `SnapshotReference`, workspace ID, managed-policy digest, interpretation digest, epoch/sequence, and per-path raw/Git hashes. It stores no payload copies and has a 64MiB size limit. Flush temporary files before replacement.

- A new invocation of the same CLI reuses it if the live provider generation matches.
- Provider restart requires full-read `rebuild` for the new epoch, without substituting a new original snapshot.
- Missing manifests use full-read `initial`; damaged/older formats produce `SourceCacheRevalidationRequired`. Historical success is not automatically reused.
- `workspace status --audit-source --id <id> --home <home>` reads the full source and rewrites the manifest. First preserve rejected manifests as `.rejected-<id>`. It includes `--verify-source` behavior.
- Unknown snapshot/provider observations, complete-journal corruption, and mismatched before/after source generations block successful publication. Existing rules for incomplete final-frame replay and acknowledged-write preservation remain.

Ordinary status observes owned application/provider without reading source content. Explicit current checks use `--verify-source`; check `sourceVerification=verified` and the returned source fingerprint. This verifies stored content; external tools separately evaluate compile/test success.

<a id="계측의-의미"></a>
## Meaning of measurements

`source.inspection.gitHashProcesses` and replay/range-index/live-write counters in `storage.metrics` are required in the current format. A provider not yet supplying measurements may omit the `metrics` object itself, but missing fields in an existing object are not accepted as an older format. Unobservable OS/shared-storage values remain unknown.

| Location | Measurement | Interpretation boundary |
| --- | --- | --- |
| `source.inspection`, `source-inspections.jsonl` | Mode, raw payload bytes, hashed/reused file counts, manifest bytes, time, epoch/sequence, `gitHashInputBytes`, `gitHashProcesses` | Git bytes are raw logical lengths of files supplied for content hashing; process count is hash-object calls in that inspection. Distinct from all Git commands/I/O/physical disk reads. Initial/audit/resume costs are also recorded; process count is required in current measurement objects. |
| `storage.metrics` | Provider epoch, lock counts/wait ticks, flush counts/ticks, commit/append bytes, replay bytes/ticks/frames/read calls/stage ticks, base/journal read bytes | Cumulative counters of the current provider process. Do not simply subtract different epochs across restart. Convert ticks using `stopwatchFrequency`. |
| `storage-reads.jsonl` | Actual stream bytes/hash time for seal and final checks before delta deletion | Diagnostic record outside delta; it replaces neither seal values nor deletion authorization. |
| `scripts/measure-workspace-storage.ps1` | FILE_STANDARD_INFO file allocation, logical lengths, exact task/shared-root classification, shared-volume free space/VSS observations | Read-only development tool accepting source/application/workspace selection. It does not traverse mounts or sum logical trees as actual copied amounts. |
| `scripts/measure-overlay-journal.py` | Current SJR2 frame checksum/SHA-256/sequence, stored/decoded quantities, referenced stored blocks, current per-path logically valid ranges | Development ledger for inputs verified by product replay/seals. Handles compacted `write-compacted`/`skip-range` too; does not replace Brotli decoding, physical allocation, or product validation. |

```powershell
pwsh ./scripts/measure-workspace-storage.ps1 -HomePath <home> -Project <original> -OutputPath <evidence.json> -Label after-edit
pwsh ./scripts/measure-workspace-storage.ps1 -HomePath <home> -Project <plain-folder> -SourceKind none -ApplicationKind unity -OutputPath <evidence.json> -Label after-checkpoint
./scripts/measure-workspace-storage.ps1 -HomePath <home> -Project <original> -SourceKind plastic -WorkspaceIds <id-A>,<id-B> -SkipOriginalLibrary -OutputPath <new-evidence.json> -Label selected-after-release
python ./scripts/measure-overlay-journal.py <stopped-delta.journal> --output <new-accounting.json>
```

Default source/application are `git`/`unity`. `-SourceKind none` invokes no Git and adds no Git common root. `-ApplicationKind none` adds no Unity Library baseline root. Ordinary-folder `files/objects` is separately classified as `independent-file-result-payload`; it is part of control root and must not be counted again.

`-WorkspaceIds` selects only exact task IDs with current v2 records for the same project. If omitted, all records are shared-home observations, not per-task costs. Runner/cache/Git common remain shared roots. Use `-SkipOriginalLibrary` when not rereading a separate original baseline. Duplicate/overlapping roots and mount/reparse roots are rejected. Internal reparse/hardlinks/read failures produce unknown and observed subtotals rather than complete allocation totals. `productFileAllocationBytes` sums selected-root file allocation excluding original/evidence: an absolute observation before subtracting existing shared baselines, not whole-workspace/VSS attribution. Publish only new outputs, preserving existing files.

Allocation measurements separately categorize current journal, compaction candidates, and previous generations. Optional `-EvidencePath <run-directory>` separately observes verification materials as a `verification-evidence` root with `scope=measurement-overhead`. Do not add it to product-workspace space; count shared evidence once across tasks. Observation occurs before the sample file itself is written, so it is not an absolute upper bound including the final sample file.

On Windows, the journal ledger opens read-only handles with read sharing only, rejecting live writers/deletion. It reads payload one frame at a time for checksum/full hash without original snapshot reads. Incomplete tails, corruption, or sequence mismatch fail without repairing inputs. Existing outputs are not overwritten. Current path comparison verifies ASCII case-insensitivity only and explicitly rejects non-ASCII paths. `unclassifiedBytes=0` means payload/frame byte accounting balances, not zero unmeasured MFT/volume space.

`storedPayloadBytes` is actual payload-frame length; `decodedPayloadBytes` is decoded length including repeated records; `livePrivateBytes` is current logically valid ranges. `liveStoredPayloadBytes` and `groups.retainedPayloadBytes` count once the full storage cost of each raw/Brotli block referenced by even one fragment. Partial overwrite/truncate does not immediately reduce that block's file allocation. `obsoletePayloadBytes` counts wholly unreferenced stored blocks; metadata/range limits mean not all necessarily become compaction savings. `metadataAndFrameBytes` includes 16B SJR2 headers, metadata, and 32B checksums. The ledger checks only SPB1 structure/inner checksums; product readers decode Brotli content.

`groups` describes storage costs of path groups at recording time; `livePayloadByCurrentGroup` describes logically valid ranges under current groups after rename. Do not add the classifications. Compare ledger hash/sequence with product seals at the same boundary. Only current formats are supported, without automatic unsupported-input conversion. Independent expectations, corruption preservation, Windows-writer rejection, and actual file-allocation/duplicate-root/hardlink tests run through `python -m unittest discover -s tests/scripts -p 'test_measure_*.py' -v`. Scope/actual qualification follow the [physical-storage phase's final scope](plans/physical-storage-completion.md).

Runtime replay sequentially reads only journals through a lifetime-bounded 64KiB buffer. It does not omit complete-frame SHA-256/metadata/sequence checks or hydrate payload/original data into separate files. `replayReadCalls`/`replayReadTicks` count actual journal file reads; `replayValidationTicks` covers checksum/decode/sequence checks; `replayIndexTicks` covers in-memory index application; `replayFrames` counts applied frames. `replayTicks` also includes buffer copying/other loop costs, so it differs from the sum of those three stages. File-API reads through OS cache are not physical disk I/O. Write flush and incomplete-tail/complete-corruption preservation contracts remain.

Subtract original-Library/shared-Git baselines at the same stage when comparing added costs. Library writes already enter delta; do not count twice. Inaccessible/reparse paths are unknown with subtotals only. Standard-user inability to read VSS is unknown; obtain administrator volume observations separately. Do not estimate per-task VSS attribution or MFT/OS-cache costs as whole savings percentages. Include logs, temporary files, and result objects in space accounting. The maximum stage sample does not guarantee the actual instantaneous peak.

The existing live journal allocation remains after repeated writes of different content to the same range and after deletion/truncate. [Identical private writes](ntfs-workspaces.md#repeated-writes-to-the-same-private-range) reuse existing payload, while metadata accumulates. `journalReadBytes` includes private-journal reads for equality comparison. This internal comparison is neither user `Read` in `readBytes`/`readRangeIndex` nor snapshot `baseReadBytes`. Explicit journal compaction is implemented for stopped NTFS workspaces before result collection. [Small-Unity resume/space measurements](verification/journal-compaction-qualification-and-cost.md) confirmed allocation reduction immediately after compaction and an observed peak while two generations overlapped. Grouped flush and switching the default execution mode were not implemented. [First common-storage plan](plans/storage-priority-and-editor-modes.md) owns scope/verification level; [verification records](verification/storage-foundation.md) own actual verdicts/VSS-preservation failure; [NTFS workspaces](ntfs-workspaces.md) own current reclamation blockers/support boundaries.

`replayIndexOperations` is an observed per-operation array of `operation`, `frames`, and `applyTicks`, with at most 9 entries. Only `create`, `metadata`, `resize`, `write`, `write-compacted`, `rename`, `delete`, `skip-write`, and `skip-range` are allowed. Totals correspond to `replayFrames` and `replayIndexTicks`, excluding new writes of the current provider. `skip-range` is also one frame, so frame count may differ from durable-sequence increments. An empty current-provider array means no replayed frames. Modifying the returned array does not affect internal counters.

`replayWriteRangeIndex` aggregates ordinary replay `write` frame range-index replacements (`replaceCalls`), cumulative/max prior extent counts (`priorExtents`/`maxPriorExtents`), search visits/list-movement extent counts (`visitedExtents`/`copiedExtents`), range search/replacement time (`locateAndReplaceTicks`), and sorting time (`sortTicks`). Multi-fragment `write-compacted` is excluded here and separately recorded in `replayIndexOperations`. The fixed 6 `priorExtentBuckets` are 0, 1–8, 9–64, 65–512, 513–4096, and 4097 or more. No path names/per-file lists are exposed; current writes of a new provider are excluded. Total `write` cost includes frame handling/other index work and differs from the sum of these times. Current sorted local-replacement implementation has `sortTicks` 0.

Rename index application retains existing path/whiteout traversal, constructing the same descendant-path prefix once per rename for reuse. It adds no range index, journal format, or persistent cache. [Offline probe](replay-profiling.md) distinguishes synthetic costs; results do not directly establish actual Unity preparation time.

Ordinary `Read` binary-searches sorted nonoverlapping extents for the first entry whose end exceeds request offset, stopping traversal at request end. Empty/EOF reads do not search. Base holes and zero-fill after truncate retain their existing contract. `storage.metrics.readRangeIndex` counts current-provider `readCalls`, binary-search comparisons `searchProbes`, and traversed entries `visitedExtents`. Entries inspected for the final stopping condition count as visited; these are neither bytes read nor actual disk I/O. [Post-review defect fixes](verification/README.md) owns content/reconnect/range-search-count verification.

<a id="live-write-비용"></a>
## Live write costs

`liveJournalWrites` measures writes in the current provider. `mutationGateCalls` and `mutationGateWaitTicks` cumulatively count nonnested mutation API calls and gate waits, including no-ops/input rejections. Reads/replay are excluded. `operations` contains at most 6 aggregates: `create/write/resize/metadata/rename/delete`. `attempts` counts commit entry, `durableCommits` records commits after durable flush, and `failedCalls` counts exceptions inside commit. Failures before entering commit, such as preflight rejection inside the gate, are excluded.

Per-operation `prepareTicks/appendTicks/flushTicks/applyTicks/totalTicks` cumulatively measure admission/serialization/checksum, journal append, durable flush, index application, and whole commit call. `payloadBytes/journalBytes` count durable-commit bytes; `flushCalls` counts flush attempts for those commits. Explicit Flush/incomplete-tail recovery flush enter only overall flush counters. The fixed 5 `waitingMutationsAtFlushStart/End` buckets are 0, 1, 2–3, 4–7, and 8 or more other waiting mutation calls; `maxWaitingMutations` is the maximum of these two-time samples. They can include eventual no-op/rejected calls and do not measure OS/WinFsp waits outside the gate. They do not represent groupable transaction counts or achievable savings.

`Write` of identical private content counts as a `metadata` commit. `IJournalMutationObserver.operation/payloadBytes` also report the actual frame's `metadata/0`; WinFsp callback requested/completed bytes retain the original write length. Equality comparison occurs before commit entry within the same mutation gate, outside these commit-stage ticks. Evaluate total call cost with separate elapsed time and additional `journalReadBytes`; reduced frame storage does not translate directly into reduced whole-write time.

Compare observations as differences within the same epoch; modifying arrays does not change internal counters. Per-call wait times can overlap and must not be added to wall clock. Current durable-flush policy remains; development synthetic execution follows [WriteProbe](write-profiling.md).

Windows adapter `WinFspOverlayFileSystem.mutationGate` first serializes whole mutation callbacks. Zero Runtime wait samples do not prove the outer callback/OS queue was empty. Composite append-offset/constrained-I/O/resize checks occur inside this lock, so flush-grouping review must separately address that atomicity boundary.

<a id="계약과-진입점"></a>
## Contracts and entry points

`contracts/v1/source-change-{request,response,view}.schema.json`, `workspace-status-request.schema.json`, `response.schema.json`, `overlay-observation.schema.json`, and `storage-read-measurement.schema.json` define input/output/instrumentation contracts. `capabilities.sourceVerification` describes the current method, audit command, and measurement locations. Implementation is split across `Core/SourceChanges.cs`, `Runtime/JournalDeltaStore.cs`/`JournalWriteMetrics.cs`, `SourceControl.Git/GitSourceFingerprint.cs`, `Platform.Windows/WindowsSourceChanges.cs`, and `NtfsSnapshotWorkspaceBackend.cs`.
