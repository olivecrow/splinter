# NTFS project workspaces

Current task state is **v4** and managed basis is **v3**. [Repeated original application](repeated-original-apply.md) owns live capture, immutable result versions, per-path publication history and exact external cooperation for a running original. RI qualification is separate from historical stopped-Editor evidence; older tasks are preserved/refused without migration.

A workspace status observation uses the project snapshot observation already obtained inside its provider-control proof, then retains the existing heartbeat and PID/start/launch/intent refresh. This record is reused only within that call; another observation proves it afresh. Preliminary ownership states that skip the inner snapshot proof still obtain a fresh project observation. Project storage availability remains separate from Library/cache availability, and all payload/admission/publication proofs are unchanged.

The current product combines `git-snapshot`, `plastic-snapshot`, or `project-snapshot` bases with WinFsp range journals. Follow the input/preparation contracts for [Git](git-project-basis.md), [Plastic](plastic-project-basis.md), and [VCS-free projects](plain-project-basis.md). `ntfs-snapshot`, VSS, and the dedicated service are retired; legacy state is preserved/rejected. Common journal, path, result and recovery guards below remain.

When preservation and reclamation are both authorized without intervening integration, use `workspace checkpoint --release`. Normal owned-Editor shutdown, independent results, seals and reclamation guards remain; checkpoint does not claim compilation success. See LF-01–05.

## Inputs and privilege boundaries

Git clean/HEAD/management-registration conditions below apply to `--source git`. Ordinary folders follow the [VCS-independent contract](vcs-independent-workspaces.md); Plastic follows [management-information/registration compatibility](plastic-compatibility.md). They share common NTFS/WinFsp, owned-resource, range-storage, and reclamation guards.

- Targets are existing Windows NTFS, installed WinFsp, and a saved clean Git repository root. The first Unity validation target is separate `splinter-unity`. Original current HEAD must match `--base`, and `.git` must be inside the original.
- Dirty/staged/untracked changes, skip-worktree/assume-unchanged, submodule/symlink, LFS/user filters/working-tree-encoding are rejected. Filters are also checked at checkpoint/result preservation. Do not stash/reset or arbitrarily commit the original.
- Original/immutable-snapshot metadata checks reject ADS, hardlinks, reparse, EFS, compressed/sparse files with `SnapshotFileUnsupported`. `.git` is excluded from the basis tree. This check neither reads nor copies file payload. Individual ACL changes are unsupported; the overlay does not claim to reproduce all original ACL semantics.
- Reparse/EFS/compressed/sparse attributes follow the same rejection rules for lazy basis reads and WinFsp create/overwrite/attribute changes. Overwrite checks before reducing existing length. Rejected callbacks preserve bytes/metadata/journal sequence and return OS `STATUS_NOT_SUPPORTED`. An actual ARM64 mount verified rejection of 5 ADS/hardlink/sparse/compression/mount-point-reparse requests with ordinary-NTFS positive controls. Immutable input is fake; full EFS/ACL/special-FSCTL verification remains unqualified. The old VSS-input matrix is retired. NR records own execution scope.
- Immediately before capture, callers check original Unity is saved and not importing/compiling/Playing. Capture supplies filesystem crash consistency without application writers, excluding unsaved memory. Original editing may continue afterward.
- Use short `--home` paths. NI's deep test path observed an initial-load exception for a 270-character full Unity package DLL path. Script validation/execution passed, but long package paths/general asset loads are not established support. Initial import has no SLA either.

WinFsp directory enumeration provides actual directory information, `.`/`..` entries, and marker-based continuation for nonroot directories. Even empty child directories enumerate through Win32 `FindFirstFileW(<dir>\*)`. These entries are not overlay files/journal records. Small-Unity real-environment regressions compare empty-folder enumeration failure before the fix and success afterward.

<a id="일반-cli-흐름"></a>
## Ordinary CLI flow

The [product development test](product-write-recovery.md) reproducing write interruptions in explicit clean Git fixtures/homes retains ordinary providers and terminates only exact owned providers at external-client boundaries. It compares same ensure, current immutable-basis read proofs, and independent results/release; it neither enables gates automatically during ordinary use nor reclaims failed tasks.

Below, `splinter` means `pwsh ./scripts/splinter.ps1` in the product repository. Build with `pwsh ./scripts/build.ps1`. All workers share one persistent `--home` state path outside the original project.

```text
splinter workspace ensure --project <original Unity repository> --key <unique task key> --base development --storage git-snapshot --home <state path>
splinter workspace status --id <workspace.id> --home <state path>
splinter unity start --id <id> --home <state path>
```

Common WinFsp mount/reconnect waits at most 2 minutes for actual mount and fresh `mounted` state from the exact running provider. `starting`/`replaying` are not readiness; limits/cancellation do not delete providers/journals. Same-request retries continue observing that provider. Journals use a lifetime-bounded 64KiB read buffer with all frame validation retained; new replay-stage counters follow the [measurement contract](source-verification.md#meaning-of-measurements). This does not introduce a Unity execution mode or switch default execution methods.

```text
splinter workspace status --id <id> --home <state path>
splinter workspace compact --id <id> --home <state path>
splinter workspace checkpoint --id <id> --request-key <capture-key> --message <result description> --home <state path>
splinter workspace release --id <id> --home <state path>
```

Use selected external MCP/Unity CLI at the returned workspace path for Editor refresh, compilation, Play/tests, etc. Product bridge provides only liveness/progress/normal shutdown, without running project functions or acceptance decisions. Checkpoint preserves source without compile success as stable independent results. Obtain actual game/code validation evidence through selected tools.

Explicitly run `workspace compact` on one connected NTFS workspace **before the first source-result capture**. Owned Editor/children must have exited, mount open handles must be 0, and snapshot availability/ownership verified. Other same-workspace lifecycle commands are rejected during `compact-*` stages. Stop provider, compare before/after unmount journal seals, then verify/publish/reconnect a new journal generation using the same original snapshot/task ID/Git registration. Recheck source content/seal afterward; the agent validates the project as needed. Retrying the same completed generation returns a read-only result if seals match; new later writes allow another explicit compaction. Do not use `checkpoint` for intermediate compaction.

`--revert-base-ranges true` select [latest-state journal ON](latest-only-journal.md); subsequent writers/reconnects retain only completed current private payload/metadata. Only explicit compaction removes private ranges matching fixed basis. The [bounded default](bounded-journal.md) for new ordinary Windows providers, retained stored choice for existing tasks/omitted options, original/expected structural hashes, metadata/identity preservation, and v3 compaction intent follow the [base-reversion contract](journal-base-reversion.md). OFF's zero basis reads/small-frame fallback do not transfer to ON.

Optional `--max-estimated-journal-overlap-bytes <positive integer>` limits the sum of existing-journal logical length and precomputed candidate-journal logical length. Omission retains existing compaction behavior. Exceeding budget returns `CompactionOverlapBudgetExceeded` with both lengths/sum before new candidate files/generation publication. Provider is then stopped in `resume-compaction`; explicitly retry `workspace compact` with the same ID/home. Retry omission retains stored budget; only explicit values change it. A new budget does not block recovery in already-published `compact-connect`. Success clears pending budget. This option limits only **estimated overlap of journal-file lengths**, not actual NTFS allocation, control/Git/Library, VSS diff area, or whole-workspace physical peak. Also use the [read-only space ledger](source-verification.md#meaning-of-measurements).

Budget checks after provider stop perform existing-journal replay, seals, state hashing, and candidate scan, so even rejection takes read time and Editor shutdown. A partial candidate from an earlier interrupted attempt remains after a lower-budget rejection. Budget checks future candidate writing without automatically reclaiming existing space. Verify original journal, intent, and existing partial-candidate ownership/state, then explicitly retry the same task.

Ordinary OFF SJR2 compaction rewrites live ranges in chunks and combines consecutive invalid writes on the same path into one sequence interval. If range/invalid-write metadata plus payload exceeds the old frame, retain that frame. If individual skip representation is larger, convert only when same-path grouping is smaller. This path does not increase candidate logical length but may retain some invalid payload for a smaller representation. ON reclaims previous payload; ON→OFF conversion/independent candidates use current-state initialization prefixes that may cost more metadata. Preflight budget computation and candidate writing use identical rules per path. Existing size-regression verification is limited to the then-current ordinary SJR2 path.

Replay-compare whiteouts, renames, truncation, `.meta`, and changed paths without whole-file copy-up or reading basis files to fill data. Flush new candidates in the same directory, verify checksum/full replay state, then publish atomically. Retain `delta.journal.previous` until new-generation mount reconnect/seals are verified. Ownership/hash mismatches or corruption in `compaction-intent.json` and candidate/previous generations preserve files and stop with `preserve`. Interruption during preparation/publication/reconnect resumes `resume-compaction` through same-ID/home `workspace compact`. No automatic sweep or connection to other-workspace/snapshot reclamation exists.

`snapshot.compaction` records attempt, before/after sequence/length/SHA-256 seals, before/after NTFS file allocation, and both generations' combined allocation at transition overlap. New-attempt `cost` provides `stopwatchFrequency`, `totalTicks`, and per-stage `elapsedTicks`, `readBytes`, `writtenBytes`, `flushCount`. Stages cover original replay/seal/state hash, candidate scan/write/flush/replay/seal, generation publication/verification, provider reconnect/old-generation cleanup. `totalTicks` times only offline journal compaction; reconnect/previous-generation cleanup are separate stages. Retry merely verifying an already published generation may omit `cost`; omission is not zero cost. Combined allocation/stage samples are not whole-workspace peak including control/identity/Git/Library. Insufficient candidate-writing space, the 2 GiB reconnect minimum, or a computed candidate beyond journal budget preserve the previous generation. VSS usage is a shared-volume observation, not attributed per-task savings. This implementation rewrites ranges; it is not general-purpose payload compression or a changed per-write flush policy. Follow first compaction verification, Unity resume/space costs, and [overlap budget](ntfs-workspaces.md).

Ordinary status observes owned application/provider without reading project content. `--verify-source` responses with `sourceVerification=verified` include current `source`; `not-checked` omits it. Source fingerprint verifies stored content, not compile/test success. Checkpoint/integrate recheck current source/independent results.

Checkpoint retains the running Editor and uses a stable request key to fix source metadata/ranges and durable backing pins. Payload reads, hashing and independent Git/file generation preservation occur outside the mutation gate. Later edits and Editor restarts remain possible in the same task. Results use immutable IDs and versioned receipts/refs, never overwrite a single task result. Final stop/bridge removal, full before/after seals and unmount proof use the same generation engine. Read [repeated original application](repeated-original-apply.md) for per-path applied state, Git working-tree publication without HEAD/index mutation, live-original permits and exact retry semantics. Earlier checkpoint/source-inspection timing records describe their recorded stopped-engine candidates.

Live volume/heartbeat accounting uses the exclusive writer count (`journalBytesBasis=writer-accounting`); explicit/final observations inspect current files. Poisoned writers report unknown storage and retain live ownership/open handles until reconnect. File notifications wake source/flush/stop requests without authorizing them.

SJL4 replay/inventory indexes backup generations and payload paths without omitting checksum, identity or ownership checks. Fresh SJL4 reclamation checks the owned request and stopped byte/sequence observation, then records the expected saved seal in the outer intent. The existing inner reclaimer performs the full seal comparison before creating its validated inventory and deleting files. The outer intent alone permits no deletion; OFF/missing-current-journal paths retain the outer full seal, and interrupted recovery retains its exact intent/inventory/fence guards. Formats and individual durable flush/ACK semantics are unchanged.

For original files use `workspace integrate --target original --result <id>` with the reviewed plan/intent/digest and any live-original permit. Explicit branch-target merge, such as `--target development`, remains separate. Original must be clean on the requested branch. On conflict, abort only the merge started by this command and preserve results. **An NTFS task with preserved independent results can be reclaimed without original integration.** An actual NTFS fixture with uncommitted original README changes verified integration rejection through `GitDirtyWorktree`, preserving original HEAD/index/changes/result ref. Failed validation/uncommitted changes are not automatically deleted. [Alpha workspaces](alpha-workspaces.md) owns common original-integration/Editor procedures.

<a id="지속-상태와-재개"></a>
## Durable state and resumption

Workspace Git commands have a 10-minute limit/caller cancellation. Initial index checks for large assets may also read file content. Even with an index file present, interrupted-attach retries complete stat-cache refresh. After `GitCommandTimeout`, inspect state/remaining locks; product does not automatically delete `index.lock`. This differs from the diagnostic Git inspector's 10-second limit.

```text
<home>/records/<id>/workspace.json         identity, stage, previous evidence, result manifest
<home>/records/<id>/events.jsonl           task intents and completion/failure
<home>/records/<id>/snapshot/              owned project-basis receipts and release authorization
<home>/records/<id>/git-overlay.json       no-checkout registration and private .git pointer
<home>/records/<id>/overlay/               provider requests/execution/flush-stop observations
<home>/records/<id>/delta/                 identity.json, delta.journal; during compaction: compaction-intent.json, delta.journal.compacting/previous
<home>/records/<id>/delta-reclaim.json     deletion intent for exact independent result and journal
<home>/workspaces/<id>/                   work tree supplied by WinFsp
```

Preparation order is store request → source-checkpoint check → capture intent/receipts → no-checkout Git registration → mount → independent-index attach → compare capture content/checkpoint. Failed comparison does not promote Ready. Partial registration compares original path/branch/owner receipts and Git management areas. Same-key retries use the same capture; a missing original capture is not replaced with current original data. CLI exit alone does not stop Editor/provider or reclaim tasks.

The WinFsp directory mount root itself is a reparse point. State store can read records while connected; Git/Unity share access rules through Core `WorkspaceMountAccess.ObservedRoot`. Require snapshot availability, live mount/provider, ownership, and matching workspace ID/snapshot/absolute root rather than a backend name. Original/ancestor paths and ordinary links within work trees remain rejected. This common rule does not establish product support for a new backend.

After provider exit, confirm no application/children remain, then reconnect existing snapshot/delta with the same ensure. Execution intent without verifiable receipt, or mismatched older state/new launch generation, preserves with `OverlayStartUncertain` instead of duplicate launch. Reboot/power-loss recovery is unverified. Automatic discard of tasks unable to finish preparation, such as capture/file-content mismatch, is not provided. Preserve `SnapshotPreparationIncomplete` state/receipts for investigation.

New NTFS tasks publish empty delta identity for verified snapshots before provider launch. Existing identity avoids caller-side journal replay; provider validates the existing journal. Private `overlay-serve` requires exact launch generation and checks token/workspace/generation against durable intent before publishing its PID/start-time/generation receipt. Even if parent CLI exits immediately after process creation, same ensure compares child receipts to observe existing provider. The gap with intent alone and unconfirmed execution remains preserved/blocked, without guessed relaunch.

Reads capture file metadata, private bytes and uncovered immutable-base intervals under the journal gate, then read those base intervals under a separate serialized reader gate. Metadata queries and mutations can proceed during slow base I/O; each read returns one captured file state even across overwrite, truncate, rename or unlink. Private payload access/reclamation stays under the journal gate. Completion rechecks writer poison, journal length and basis availability. Close rejects new operations and every concurrent close caller waits for admitted reads before releasing the journal and ownership fences. No disk payload cache or extra file copy is created. This applies to the ordinary journal path; development source views retain their outer gate. See LO-06–08 verification.

Directory listings reuse immutable child metadata only for an exact base-path match. Private/renamed/deleted entries and inclusion/exclusion/denial policy retain precedence, unexpected paths fall back to exact `Stat`, and the basis is checked again after listing. This removes duplicate child lookups without adding a cache or changing timestamps; LO-10 records the counted NTFS comparison.

<a id="동일한-private-범위의-반복-쓰기"></a>
## Repeated writes to the same private range

Ordinary file rename renames only that node without searching all paths/whiteouts. Directory rename moves known descendants/whiteouts by prefixes including boundary `/`. Replay may contain child files without unchanged parent nodes, so direct-child indexes alone do not replace traversal in that case. Remove all old names before registering new ones, retaining case changes/open file IDs/lazy basis children.

Ordinary reads and sealed-private restoration use the same binary search for the first overlapping extent and traverse only needed ranges. Existing `ReadRangeIndex` includes both call kinds/search probes/visited extents. Restore requires exact seal/private coverage and never substitutes original reads for holes.

If the entire current write range is already acknowledged private-journal coverage with identical bytes, payload is not rewritten. Raw journals compare through buffers at most 64KiB. Existing `metadata` frames record mtime/current path/new sequence, responding after individual `Flush(true)`. This differs from existing empty-write no-op; source-change paths/acknowledged-write boundaries remain.

For partly different bytes in the same private range, store one continuous range from first to last difference. Apply only when removing matching leading/trailing bytes saves at least 4KiB; matching bytes in between remain in that range. Retain full request length, mtime, and path sequence, acknowledging through one existing `write` frame and individual `Flush(true)`. Smaller savings retain the whole request to avoid needless extent splitting.

Original-only ranges, zero-fill holes, length extensions, and first new-file writes store requested payload as before. No snapshot-payload comparison reads/hydration, cross-task/file deduplication, or shared payload cache exist. Even identical content accumulates metadata, so unlimited repetition does not guarantee zero storage. Normal writes may add journal comparison reads. Identical-write verification and partial-overwrite/buffer/startup verification distinguish actual allocation for fixed inputs from whole Unity efficiency.

WinFsp Read/Write callbacks rent buffers sized to the smaller of request length and maximum transfer. Empty requests use a minimum rented buffer, retaining empty-I/O semantics. Large-request chunks, callback serialization, and acknowledgement/flush boundaries remain; buffers return after success/failure. Rented sizes/cumulative managed allocation are distinct from Editor/system RAM.

<a id="개발용-준비-중단-시험"></a>
## Development preparation-interruption tests

`preparation-probe --input <JSON>` adds explicit gates only to NTFS preparation boundaries of ordinary `workspace ensure`. It neither starts Unity Editors nor automatically reclaims resources. Input uses `preparation-probe-request` version 1; output signal uses `preparation-probe-signal`. Capability `developmentPreparationProbe` identifies current entry points/schemas. Signals are flushed/published only at new paths outside original/workspace, linking boundary, workspace/request digest, PID/start time, snapshot/step. An external harness matching PID/start time terminates only CLI. Explicit `.continue` passes the gate; the 5-minute deadline cancels while preserving the existing task.

```powershell
# Default is a read-only execution plan. Actual execution uses a separate new evidence path and -Execute.
pwsh ./scripts/test-preparation-recovery.ps1 -Project <clean NTFS Git fixture> -HomePath <home> -BaseReference <current HEAD ref> -OutputPath <new evidence path>
```

`-Execute` terminates CLI at request recording, source checking, capture, Git registration, delta, provider, mount, index, Ready, and response boundaries, then runs two ordinary ensures with identical inputs. Lock `WorkspaceBusy` is rechecked with the same request after the first call finishes. Tests cover lost-response retry, input-conflict rejection, independent-result preservation, explicit release; failures retain tasks/evidence. Service installation, grants, volumes/VSS budgets, and global Git settings are unchanged. Fixture-owner differences use child-process Git settings limited to the explicit source. Preparation-recovery verification owns actual per-boundary qualification/inapplicable combinations.

NTFS response `data.recovery` returns `disposition`, `reasonCode`, and `nextCommand`. `ready` permits existing next work; `resume-ensure`/`resume-compaction`/`resume-release` resume those stages with the same identity. `observe` rechecks status after observation recovery; `preserve` retains data/receipts until the cause is resolved. The next action for `released` is list. Actual call arguments are in `nextActions`, referring to the same command as `data.nextCommand`. Recovery classification does not authorize resource deletion; each command rechecks ownership/results/reclamation conditions.

Basis/restore protection errors `ProjectBasisMismatch`, `GeneratedBasisChanged`, `GeneratedRestoreConflict`, `RestoreSealMismatch`, `PlasticBasisInvalid`, and `GitBasisReadTimeout` use Core's common preservation policy. Failed responses use `requires-user-action`; durable workspace recovery uses `preserve`/`workspace.status`. Preserve the same basis/id/home, receipts, and journal while checking the cause; do not bypass through changed inputs/new tasks. Invalid fixed Plastic revision input is separately classified as `PlasticBasisRequestInvalid`/`correct-request`.

Journal directory queries sort/return only that folder's children from the per-parent child-identity index. Lazy basis entries, creation/deletion/replacement/directory movement/replay update the same index. Queries do not traverse every other folder's paths. Existing metadata limits, mutation gate, individual flush/ACK, and SJR2 format remain. The index is bounded managed-metadata cost, not a guarantee of actual Unity preparation-time or total-RAM savings.

Snapshot-access failure produces `SnapshotObservationUnknown`. `snapshotAvailability`/`snapshotStorage.availability` are `available`/`missing`/`unknown`; unknown omits existing boolean fields. Ordinary ensure and prereclamation stages preserve until observations recover. Distinguish observed absence from access errors; a path changed to a file preserves with `SnapshotIdentityMismatch`. Only a successful exact-identity observation means missing; read-path loss alone does not prove basis absence. Each provider validates its fixed receipt and reader identity on new connections.

Provider `state.json` also uses verified access observations, recording unobservable state as `snapshot-unknown`. Invalid complete-frame checksum/sequence/JSON/path-range metadata or damaged identity JSON fail with `DeltaCorrupt`, preserving files unchanged. Replay removes only an incomplete final frame under existing contracts, without truncating complete corruption.

Provider control JSON/workspace state fully write/flush a same-directory temporary file, then replace existing files through `File.Replace`. Only initial publication uses nonoverwriting move; readers allow delete sharing. Windows provider IPC retries transient sharing errors 32/33/1175 with the same temporary file for at most 1 second. It neither changes read-only attributes, deletes destination first, nor overwrites content in place. Persistent-failure temporary files remain; internal `state.json` records `errorHResult`, `errorStackTrace`, `publicationPath`, and `publicationTemporaryPath`. External workspace-response schemas/journal flush-seal contracts remain. Follow reproduction/regression evidence.

Windows control-file readers do not decide readability through `File.Exists`; they apply path guards and open with delete sharing. Replacement-time file/directory absence and sharing errors 32/33 reopen the same path for at most 1 second of monotonic time, at intervals no longer than 25ms. Actual absence after deadline returns the existing missing contract; persistent sharing errors remain exceptions. File absence does not prove zero resources; caller launch/owner/mount checks remain necessary. Apply guards every time; do not hide JSON corruption/access denial/reparse/owner mismatches through retries. Optional provider stop/flush/source requests skip absence waiting because the next poll rereads them. IPC-race evidence distinguishes actual Windows-file tests from synthetic-client tests.

Damaged workspace JSON is `StateCorrupt`; missing JSON beside existing control/workspace paths is `StateRecoveryRequired`; unreadable JSON is `StateReadUnavailable`. Do not create a new task with the same key. State-recording failure is `StateWriteIncomplete`, preserving remaining records/temporary files. If even failure recording cannot be saved, details include the original error code. File locks/interrupted temporary files verified this contract; actual disk-exhaustion/power-interruption verification remains.

Short CLI commands hold workspace-ID file locks for their full lifetime. Only confirmed Windows sharing/lock violations wait at 100ms intervals for up to 5 seconds before returning `WorkspaceBusy` (`safe-to-retry`). Missing lock paths, access denial, and other I/O errors immediately reject with `StateReadUnavailable` (`requires-user-action`), without deleting/rewriting lock files/directories. Ordinary query/resume for distinct IDs use independent locks. Project registration/application and shared snapshot/basis protection retain separate locks. This supplies neither per-conversation exclusive arbitrary-edit locking nor parallel service mutations. SP verification owns selected concurrent-progress qualification.

Current `StateWriteIncomplete` details follow `urn:splinter:v1:state-write-failure-details`: `id`, `action`, and `diagnostic`. If failure records also cannot be saved, retain the same contract in `operationError` and nested `details`. Diagnostics supply last-attempt `stage` (prepare-record/validate-target/open-temporary/write-temporary/flush-temporary/close-temporary/publish-state/append-event), `exceptionType`, signed `hResult`, and `statePublication`. Before publication is `not-attempted`; failed publication call leaves replacement `unknown`; event-recording failure after publication is `completed`. Completed is still not command success; preserve the task and observe the same identity through status. Visible new state does not automatically establish success of an earlier operation whose response was lost.

Only confirmed Windows FACILITY_WIN32 HRESULTs supply `nativeErrorDomain=win32`/`nativeErrorCode`. Unconfirmed native errors omit fields without guessing 0. Diagnostics omit original exception messages/stacks/temporary-destination paths. They do not change atomic publication, flush, retries, or temporary-file preservation; validation-project ledger `ValidationStateWriteIncomplete` is outside this workspace contract. Follow diagnostics, actual-file tests, and limitations.

## Reclamation and observations

Incomplete publication, candidates, source generations, and open caches in development [source views/Library caches](view-workflow.md) depend on the immutable basis. Product release/discard blocks them with `SnapshotInUse`, without cascading deletion. Editor-free workers also preserve through product `workspace checkpoint`.

Reclamation order is application/child exit → owned bridge removal → precollection flush/seal → current-source checks and independent results → postcollection seal → manifest → unmount/provider exit → seal recheck → owned registration/delta → owned basis reference. Uncollected changes or other dependents block. A changed postcollection seal preserves with `DeltaChangedAfterCollection`; a lost reply resumes using the same intent and remaining inventory. Do not delete original/peer/shared bases arbitrarily.

After snapshot loss, reclamation is allowed **only when an independent result collected with `stable-journal-v1` exists and Git refs/objects and whole journal seal match**. Do not reread/modify unavailable original trees; compare seals after unmount. Missing independent results or postcollection writes preserve results/journals and fail. Unmount with open handles gives `OverlayStopPending`; close handles, then retry identical release.

Final-closure `workspace.snapshot.result` requires `collectionProtocol=stable-journal-v1`; missing/unsupported values preserve and refuse. Intermediate captures instead publish `results` with immutable IDs, source sequences and capture digests and remain usable for later editing. A lost capture response resumes only its fixed request key; corrupt published records are never silently recollected or migrated. Full source checks, captured-tree/object proofs and final before/after/unmount seals remain separate; see [current generation ownership](repeated-original-apply.md).

`workspace.snapshot.observation` observes snapshot/mount/provider/delta, ownership, handles, working set/private bytes, host free space and journal headroom. Missing values are unknown, not zero. Cumulative provider journal counters are not NTFS allocation, whole peak, or space remaining after reclamation. Report measured allocation separately from independent-result and shared-basis costs.

`failureCode` shows snapshot loss/pressure/provider failure or last provider-I/O failure. `DeltaFull` retains existing data without automatically increasing quota/deleting data. Successful release retains fresh observations of zero application/child/provider/owned-memory/handles and absent mount/delta/snapshot. Cumulative `storage` writes are historical last-run values, not remaining actual space after reclamation. Released queries return final reclamation receipts. This does not empty OS standby/cache RAM.

| Error | Resume action |
| --- | --- |
| `StorageOperationUncertain` / `StorageRequestPending` | Retain protected existing intent and retry the same workspace/request. No bypass through new capture/other-task reclamation. |
| `RequestKeyConflict` | Invoke with stored input or use a new key for a separate task. |
| `SnapshotMissing`, `SnapshotIdentityMismatch` | Do not substitute a new original. A lost snapshot permits release only with matching existing independent result/journal seal. |
| `SnapshotFileUnsupported`, `SnapshotFilterUnsupported` | Keep unsupported inputs as separate verification scope. |
| `SnapshotHeadroomRequired`, `DeltaFull` | Preserve existing results; inspect space/limits/helper observations. No automatic deletion/full copying. |
| `CompactionClientsRemain`, `CompactionProviderNotStopped` | Resolve owned-Editor/process/mount-handle observations, then retry identical `workspace compact`. |
| `CompactionHeadroomRequired` | Obtain free space, then retry compaction with the same ID/home. Do not first delete snapshot/old journal. |
| `CompactionGenerationMismatch`, `CompactionStateMismatch`, `CompactionCandidateMismatch`, `CompactionIntentCorrupt`, `CompactionIntentMismatch` | Core common preservation policy. Failed response uses `requires-user-action` and `workspace.status` with the same ID/absolute home; durable recovery is `preserve`. Retain generations/intents/candidates and investigate. |
| `OverlayClientsRemain`, `ProcessCleanupIncomplete` | Check owned-application/child shutdown, then invoke again. |
| `OverlayStartUncertain`, `LaunchUncertain` | Inspect exact execution intent/PID/start time/generation. No duplicate launch/forced reclamation. |
| `UncollectedChanges`, `UnownedIgnoredFiles`, `DeltaChangedAfterCollection` | Preserve unique changes; check validation/result consistency. |
| `SnapshotResultInvalid` | Do not resume reclamation from earlier collection evidence; preserve remaining resources/records. Recollect only in precollection state where original basis is available. |

<a id="구현과-검증-경계"></a>
## Implementation and verification boundaries

Core `ISnapshotWorkspaceBackend`/`ISnapshotSourceWorkspaceProvider` expose no Windows/Git APIs. Runtime owns stages, locks, result/dependency policy; Git adapter owns registration/index/result refs; Windows adapter owns immutable-reader composition, special-file checks, WinFsp, journal observations/reclamation. CLI composes them; Host remains read-only in-process.

NTFS failure never switches to full checkout/whole-file copy-up. NI records distinguish automated fake/Git/Windows metadata evidence from actual mount/Unity execution. Common recovery responses/state-file preservation in the NR-01–08 plan are partially implemented/automatically verified; actual reboot/space failures/file support-rejection remain incomplete conditions in NR records. Special-file support expansion, general assets/packages, large-project performance, and the whole public product remain later scope. Purchased assets are unnecessary for NI/next NR tests.


Historical VSS read/deletion-order evidence belongs to the decision and original verification records.
<a id="ntfs-snapshot-작업공간"></a>
<a id="입력과-권한-경계"></a>
<a id="살아-있는-vss-식별과-읽기-위치"></a>
<a id="지연-회수와-명시적-재시도"></a>
<a id="회수와-관측"></a>
