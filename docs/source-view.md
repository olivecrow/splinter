<a id="불변-소스-후보와-고정-view"></a>
# Immutable source candidates and fixed views

[한국어](ko/source-view.md)

Current commands use the [separate development CLI](development-cli.md). They manage storage candidates, source/cache, seals, and reclamation, without internal compile/validation requests.

This is a development extension of `snapshot-spike`, not connected to product `workspace ensure` execution modes or task queues. Current scope is the storage/publication/view contracts below. [Verification records](verification/immutable-validation-view.md#per-gate-assessment) own VC-01–04 inputs/verdicts.

The [source-view lifecycle](view-workflow.md) uses the same storage implementation, connected to Windows/Git/Unity lifetime adapters through development `view`. AI/external tools perform actual Unity editing/compilation/tests. Passing evidence for the development experiments below does not automatically qualify new product candidates.

<a id="저장-구조"></a>
## Storage structure

Core's current `ImmutableCandidate` format is version 3. It uses overlay v2/SJR2 stored/decoded lengths and logical fragments, combining original `OverlayIdentity`, original journal seal, separate candidate journal seal/time, and required `sourceProjection`. Ordinary source candidates explicitly use null projection; candidates with separated management files carry the evidence below. `SourceViewSelection` supplies candidate directory, cache directory, and cache top-level names. Core depends on neither Unity, Git, nor Win32. Earlier candidate formats/missing evidence preserve files and reject, without automatic migration.

Runtime `JournalDeltaStore.PublishCandidate` opens a stopped worker journal read-only, rejecting concurrent writers. Existing compaction algorithms write only valid changed ranges/metadata to a separate directory, verify replay/state hash/seal, then atomically publish `candidate.json` last. Same-request recovery follows the intent contract below. It neither enumerates the whole original tree nor copies original payload. Superseded journal payload is unnecessary for candidates, but metadata and some representation-limited records can remain. Source journal is unchanged. Temporary publication space is worker plus candidate; `.new` journal is only renamed at publication, without another whole copy.

`CandidateSnapshotTree` checks published identity/seal and holds a read-only journal handle during use to block writes/replacement. External candidate changes while no handles are open are rejected by the next connection's seal check. Combined snapshot/candidate identity includes original identity, original/candidate seals, and separation evidence. Reads stop if snapshot disappears. A candidate itself is **not a snapshot-independent result**.

`SourceViewDeltaStore` uses two exclusive-writer journals.

| Storage location | Lifetime and role |
| --- | --- |
| Worker delta | Edit A/B without an Editor. Later worker edits do not change already published candidates. |
| Immutable candidate | Range payload/metadata in a separate directory from worker. Depends on snapshot. |
| Per-generation source delta | Preserves source, .meta, and managed-bridge writes by Unity over the candidate. Never mixed into the next candidate. |
| Shared cache delta | Preserves private writes for `Library`/`Temp`/`Logs`/`UserSettings`/`obj` over explicit `empty` or separately fixed `snapshot` basis. Resumes the same journal/basis across candidates. |

Source, candidate, and cache paths must not overlap. Different partition settings, view ID, original snapshot, or budget prohibit reuse of existing cache. A cache open by another writer prevents the next view from opening. Journal format and per-acknowledged-write flush policy remain. `state.json.sourceView` shows separate source/cache I/O counters and candidate seals. Top-level `storage` totals observe both journals and are not product `stable-journal-v1` result seals.

When Unity renames a new temporary file across partitions, only files with 0 ranges to read from original may transfer. `JournalPrivateFiles` copies only valid private extents without filling holes. Limits are 64MiB valid payload and 8,192 extents; cross-boundary movement of directories/files with remaining original ranges is rejected. `partition-transfer.json` records exact source generation, both paths, file ID, and range hash. Write ranges to a destination-journal temporary file, publish through atomic rename within that journal, then remove the original path. Original file ID/open handles remain. Interruption blocks view I/O; reconnect only the same source generation to complete transfer. Do not change candidates or delete intents/journals to bypass recovery. Provider-epoch `transferCount`, `transferredPrivateBytes`, and last paths are observations, not whole-file copy-up or a new journal format.

If a deleted file ID is recreated in the opposite partition, first clear the previous incarnation's extent list/count. This also applies to zero-length private files, preventing old payload from remaining in range hashes or later extended reads. Live application/replay use the same rule; interruption at intent/staged/published/source-removed boundaries reconnects with the same generation. [Post-review defect fixes](verification/README.md) records automated-regression scope.

<a id="개발용-진입점"></a>
## Development entry points

Run after normally unmounting the prepared worker overlay.

```json
{
  "action": "candidate-export",
  "overlayRequestPath": "C:/trial/worker-overlay.json",
  "candidatePath": "C:/trial/candidates/a",
  "publication": { "version": 1, "requestKey": "candidate-a" }
}
```

Invoke through `scripts/splinter-dev.ps1 snapshot-spike --input <request.json>`. Input schema is `snapshot-spike-request`; result-candidate schema is `immutable-candidate`. `candidate-export` still rejects workers that wrote `.git` or managed caches. Workers with Git pointers need the separate development path below. Independent-result collection uses `GitOverlayRegistration` for Git registration; experimental attach accepts normal index-refresh exit 1 when source differs from baseline. Locks/other errors are not ignored; the default product-attach contract remains.

<a id="등록-증거가-있는-관리-파일-분리"></a>
### Separating management files with registration evidence

After confirming normal Editor stop, provider exit, and absent mount, specify the worker's exact `git-overlay.json` registration receipt. The command publishes/reconciles a candidate for the fixed request without modifying/reclaiming worker pointer, journal, or Git registration.

```json
{
  "action": "candidate-source-export",
  "overlayRequestPath": "C:/trial/worker-overlay.json",
  "candidatePath": "C:/trial/candidates/source-a",
  "receiptPath": "C:/trial/worker-git.json",
  "publication": { "version": 1, "requestKey": "source-candidate-a" }
}
```

Git adapter `QualifyCandidateSourceAsync` compares owner, original/task paths, actual linked-worktree management directory/backreferences/branch HEAD/original common directory, `Splinter overlay <owner>` lock, and exact pointer read-only. Remaining `index/HEAD/gitdir/commondir.lock` files preserve with `CandidateRegistrationBusy`. Later current-original edits are not reverted.

Core `CandidateSourceProjection` version 1 contains owner, whole snapshot tuple, registration-evidence hash, and each file's path/length/content hash. Schema is `urn:splinter:v1:candidate-source-projection`. Runtime accepts only top-level private files already excluded from original snapshot, at most 4 KiB each and 16 files per evidence. Git adapter emits exactly one `.git` file. CLI obtains evidence from actual registration rather than accepting caller-supplied arbitrary separation lists.

Runtime opens worker journal read-only against exclusive writers, then compares current pointer content and full history. Missing current file, directory, or different content produces `CandidateProjectionMismatch`; renames beyond management files or records inheriting original ranges produce `CandidateProjectionHistoryUnsupported`. Management-directory descendants/cache writes retain `CandidateContainsManagedPaths` rejection. Path names alone do not exclude user files.

Verified-file history uses existing `skip-write/skip-range` in new candidates, preserving sequence/changed-path history without moving nodes/payload. Verify replayed state hashes for other source live ranges/renames/deletions and before/after worker seals. Introduce neither full original-tree enumeration/payload reads/read hydration nor new journal-flush policy. New views do not see worker Git pointers and must separately attach their own registration if needed.

Current conformance is [VG tracking](plans/validation-matrix.md#sf-03-management-file-separation-conformance) using temporary actual Git registration/synthetic immutable trees. [WF tracking](plans/validation-matrix.md#sf-03-product-workflow-integration) owns product integration/actual execution status. Existing VC execution does not qualify this candidate format.

<a id="같은-요청의-게시-복구"></a>
### Same-request publication recovery

Both publication actions require `publication.version=1` and fixed `requestKey`. Keys are 1–128 Latin-letter/number/`._:-` characters, beginning with a letter/number. Initial request fixes worker identity, whole snapshot tuple, source seal, projection, prohibited paths, both absolute paths, expected state hash/journal length in `candidate-export.json`. Candidate ID equals the request key. Input/intent schemas are `urn:splinter:v1:candidate-export-request` and `urn:splinter:v1:candidate-export-intent`. Publishing another worker generation later requires a new key/destination.

Exclusively open adjacent `<candidatePath>.publication.lock` to reject concurrent publication processes. The empty lock file remains after exit, but occupancy lasts only while its handle is open. This replaces neither general leases/owner takeover nor actual OS-writer observations. Even on retry, CLI first checks normal stop, absent mount, and current Git registration.

| Remaining state | Explicit retry of the same request |
| --- | --- |
| Complete intent `.new` | Compare request/path/identity, then publish by nonoverwriting move. |
| Prepared identity or partial `delta.journal.new` | Recheck original source seal. Recreate expected journal; append/flush only the remainder after every existing prefix byte matches. |
| Complete `.new`/published journal | Check fixed length/sequence/replay state hash and absent separated files, then publish/create receipt. Do not read or revert later worker edits. |
| Complete `candidate.json.new`/published receipt | Verify intent/identity/source seal/candidate seal, then publish/return identical receipt. Lost responses do not recreate candidates. |
| Existing destination without intent, partial JSON, different request/identity, duplicate/unknown fields/files, changed prefix/damaged journal | Preserve/reject. No overwrite, truncate, deletion, or automatic migration. |

Each metadata file is written create-new in the same directory, flushed, and published by initial nonoverwriting move. JSON readers allow delete sharing. At any partial journal header/metadata/payload/checksum position, only an exact prefix can be continued; journals already published under final names are unchanged. Partial JSON cannot prove the full contract, so it is not automatically rewritten. Even completed candidates are unusable without their immutable snapshot.

Runtime one-shot `ExportCandidate`/`ExportSourceCandidate` use the same publication implementation but continue rejecting existing destinations with `CandidateAlreadyExists`. Intended retries must explicitly use `PublishCandidate` or fixed-request CLI. Candidate directories created without intent are not publication-reconciliation targets and are not automatically adopted, independently of their read contract. Before final receipt, `CandidateSnapshotTree` rejects with `CandidateUnpublished`.

Automated-conformance [VP tracking](plans/validation-matrix.md#sf-03-candidate-publication-recovery-conformance) verifies exception-injected boundaries, partial bytes, and new calls using synthetic immutable trees/actual temporary files. Distinguish this from [WF tracking](plans/validation-matrix.md#sf-03-product-workflow-integration) evidence for actual forced process termination, reboot, power/physical-space failures, VSS/WinFsp/Unity execution, product-resource fencing, and snapshot dependencies.

<a id="후보를-사용하는-소스-view"></a>
### Source views using candidates

Add the following selection to validation `overlay-serve-request`.

```json
{
  "sourceView": {
    "candidatePath": "C:/trial/candidates/a",
    "cacheDeltaPath": "C:/trial/cache",
    "cacheTopLevel": ["Library", "Temp", "Logs", "UserSettings", "obj"],
    "cacheBasis": "empty"
  }
}
```

Whole-request `identity` points to the original snapshot. Each transition uses new `deltaPath`/`controlPath` and retains the same `mountPath`, view ID, and cache path. Preserve previous candidates' source delta/control records. Resume with the same candidate/source delta/identity. Do not delete folders/journals after errors to bypass identity checks.

Transition order is `unity-stop` → zero owned application resources → `unmount` → provider exit/absent mount → next-candidate `mount` → `unity-start` → refresh/actual code-asset checks through selected external tools. Candidate publication uses the stopped worker source seal and `candidate-export` or `candidate-source-export`. There is no root replacement under an open Editor, automatic lease/FIFO, or command merging generated source into workers. Collect source-delta output into independent Git results before reclamation. Release, TTL, and disk pressure do not automatically delete candidates.

Provider publishes `replaying` while opening journals. Mount/reconnect retains the same launch PID/start time, waiting at most 2 minutes for actual mount and a `mounted` observation within 5 seconds. Process startup/replay alone does not return readiness. Exceeded limits/cancellation preserve journal and running provider; the next same-request call waits for that provider. Mismatched records/launch ownership retain uncertainty without duplicate launches. `state.preparation.storeReadyTicks` is internal combined candidate-verification/source-cache-open time, distinct from whole-CLI preparation time.

<a id="시간과-공간의-판정"></a>
## Timing and space evaluation

Candidate `creationTicks / stopwatchFrequency` covers journal preparation/verification through immediately before receipt publication in the call creating the final receipt. It excludes earlier interrupted attempts and waits between calls; completed-request retransmission returns the existing value. Measure whole candidate-publication time separately as wall clock through CLI return. Record snapshot capture, Git preparation, initial mount, existing-worker resumption, candidate publication, first Unity import/ready, repeated stop/remount/replay/restart/ready, validation/result collection/reclamation separately. Small internal-function timings do not replace time until users can work.

Distinguish per-directory logical lengths from `FILE_STANDARD_INFO.AllocationSize`; retain shared VSS as volume observations. Evidence/logs are separate costs; separate first import from repeated transitions. Small Editor-free delta preparation does not generalize to whole product `workspace ensure` or large-project preparation. Initial-source verification, long-journal replay, Unity-version/package/platform changes, general interrupted-lease recovery, and source-return merging need separate verification.

[Source verification/instrumentation](source-verification.md) owns replay-read buffers/stage counters; [VR execution](verification/validation-view-readiness-and-replay.md) and [small-Unity comparison of existing/improved Runtime](verification/unity-replay-comparison.md) own actual support scope/timing. Mount reconnect of the same closed journal differs from Editor restart adding new records. Check initial ready, explicit compile, actual code/asset verification, and normal-stop retry costs together. Mount preparation/compile success/asset-load success do not guarantee first-render success. Check the first preview itself; the first-render initialization issue remains unresolved. Product default mode/general Unity support require separate verification.
