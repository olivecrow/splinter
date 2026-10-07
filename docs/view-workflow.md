<a id="개발용-소스-view와-library-cache-수명"></a>
# Development source-view and Library-cache lifetime

[한국어](ko/view-workflow.md)

`view` in the separate [development CLI](development-cli.md) manages only storage lifetime for candidates, source generations, shared Library, and independent results. It provides no internal refresh, compilation, or validation collection/evaluation; external tools and agents validate work. Preserve workers through product `workspace checkpoint`. Storage paths/current formats and existing records are not automatically migrated or deleted.

`pwsh ./scripts/splinter-dev.ps1 view --input <request.json> --home <state-root>` connects immutable candidates from existing NTFS workers to a single source view. Check the command contract through `schema view.execute`. `capabilities.viewWorkflowContract` reports `stage=development-storage-view`, `cliAvailable=true`, and `integratedWorkspaceMode=true`. Actual execution qualification follows the [verification matrix](plans/validation-matrix.md) and [Library/standard-user execution limitations](verification/product-validation-unelevated.md).

`Splinter.Workflows.Windows` composes Runtime coordinator with Windows VSS/WinFsp, Git, and Unity. Core has no platform APIs; Host is read-only. This can optionally accompany existing `workspace`/`unity` commands that use worker Editors directly. Automatic default-mode selection, queues, Coordination, and automatic ownership takeover are not provided.

<a id="실행-입력"></a>
## Execution inputs

Current input requires `version=1`, `action`, absolute project path `project`, `owner:{id,generation}`, and stable `requestKey`. IDs are 1–128-character identifiers using Latin letters, numbers, and `._:-`, not paths. Owner generation is positive and fixed to that worker/view on first explicit use. Retransmit the same request key only with identical complete input. Keep the same `--home`.

```json
{
  "version": 1,
  "action": "prepare-worker",
  "project": "C:/projects/game",
  "owner": {"id": "conversation-a", "generation": 1},
  "requestKey": "prepare-a-1",
  "workspaceId": "<ID returned by workspace ensure>"
}
```

Create the worker with `workspace ensure --storage ntfs-snapshot`. Edit, prepare, and publish source without an Editor. A worker with mutation history in managed caches such as Library is rejected if it cannot satisfy source-only publication qualification. Do not bypass by omitting data or copying whole files.

While `start` is running and awaiting application readiness, it returns `Accepted`, `terminal=false`. Save the status JSON from `nextActions[].arguments.request` to a separate file and observe through `pwsh ./scripts/splinter-dev.ps1 view --input <file> --home <arguments.home>`. Do not overwrite the original request file. Status-ledger results, frozen seals, and responses not yet present remain required null fields.

| action | Additional input | Result and next step |
| --- | --- | --- |
| `prepare-worker` | `workspaceId` | Normal owned-Editor stop, bridge removal, flush/unmount and seal checks; returns worker identity to publish. |
| `publish` | `workspaceId` | Returns a candidate after checking stopped worker's original snapshot, Git registration, source seal, and read-only journal fence. |
| `select` | `viewId`, `candidateId`, optional `maxJournalBytes` | Selects a new source generation in a stopped view. Default budget 16 GiB, immutable within the same view. |
| `start` | `generationId` | Mounts current generation at the same view path and starts the exact Unity version. Application readiness is separately observed. |
| `status` | Optional `viewId` | Workflow ledger and actual application/provider/mount/handle resource observations. |
| `stop` | `generationId` | Normal stop, bridge removal, flush/unmount. Timeout is not completed reclamation. |
| `resume` | `generationId` | Checks current unpreserved generation/stopped-resource receipts. Actual execution uses `start`. |
| `preserve` | `target:{kind,id}` | Freezes exact seal of `candidate` or `source-generation`, preserving an independent Git result. |
| `release` | Same target | Revalidates independent results, then reclaims only that artifact's journal. Rejects candidates with dependent source generations. |
| `close-view` | `viewId` | Reclaims that cache after all source generations are preserved/reclaimed and stopped resources checked. Closed views are not reused. |
| `compact-cache` | `viewId`, positive `maxEstimatedJournalOverlapBytes` | Cleans superseded ranges of a stopped cache. Returns same-request interruption recovery, content equivalence, before/after private-payload and file-allocation receipts. |

A/B/A switching is A stop → B select/start → B stop → new A select/start. Three selections create three independent source generations. Retransmitting earlier A does not revert current selection. After publication, reconnect the same worker through `workspace ensure` for subsequent edits without changing the candidate. View execution does not establish acceptance of the latest worker; Unity-generated source is not automatically merged back.

Running providers on the same VSS share verified read access and OS read fences. If reconnect needs new verification proof while another provider reads, `SnapshotReadersActive` preserves the existing proof. Normally stop/unmount workers/views using that snapshot, then retry the same command. One provider's reconnect does not change another Editor's read evidence; OS releases fences of exited processes.

Windows provider-state publication/read races follow [NTFS control-file readers](ntfs-workspaces.md), with at most 1-second recovery. Persistent sharing errors, corruption, and ownership mismatches fail while preserving state/journals. After resolving the cause, invoke the same owner/request without omitting `prepare-worker` seal, normal-unmount, or ownership conditions.

<a id="후보캐시결과의-소유권"></a>
## Candidate, cache, and result ownership

| Object | Fixed identity |
| --- | --- |
| worker | Project/workspace, owner generation, original VSS tuple, source seal to publish. |
| candidate | ID determined by common workflow operation, original worker/source seal, separate candidate seal, atomic publication intent/receipt. |
| view/cache | View/cache ID, owner generation, first candidate worker's immutable Library snapshot/worker ID, Unity/bridge compatibility, partition/budget. |
| source generation | Selection request ID, candidate, shared cache. Previous output journal is retained. |
| independent result | Frozen artifact/seal, Git result ref, commit-object SHA-256, object set readable without snapshot. |

Only `.git` pointers with registration evidence are separated from candidates. Connect `GitOverlayRegistration.QualifyCandidateSourceAsync` and [same-request publication recovery](source-view.md#same-request-publication-recovery) to the common operation ID. Use the worker command lock together with an actual read-only journal handle. User data other than `.git` is not arbitrarily separated.

Cache is the single writer for `Library/Temp/Logs/UserSettings/obj/.vs/.idea`. Fix the first selection's worker snapshot as cache basis with `SnapshotBacked=true`. Read existing Library directly from that snapshot and store new writes as private ranges. There is no initial whole-Library copying, read hydration, or whole-file copy-up. If original Library is absent, real cold import is required; later original Library creation/changes do not enter the already fixed cache basis.

Switching candidates to B leaves cache based on A's original snapshot. Check both snapshots' owners, read proofs, OS read fences, and availability separately. If either is unclear, do not report a healthy mount. Do not replace a missing basis with a new snapshot or empty cache. Candidates from another VSS can use the same cache only with matching project, Unity/bridge compatibility, partition, budget, and view owner. Development `SourceViewSelection.CacheBasis` explicitly specifies `empty` or `snapshot`; development views pass snapshot and cache-only `IncludedTopLevel` identity.

`compact-cache` runs only after confirming zero owned Editor/provider/handle/mount resources. Budget describes estimated logical overlap of old/new journals, not actual volume peak. Distinguish current valid private ranges, superseded payload, and frame metadata; valid Library is not deleted. [Journal-size selection](ntfs-workspaces.md#ordinary-cli-flow) keeps the original frame when range metadata for small overwrites is larger, so some superseded payload can remain. Immutable-basis payload is neither read nor copied into the new journal. Arbitrary basis-free initialization and automatic cleanup are not provided.

Interruption at compaction intent, candidate flush, publication, observation, old-journal reclamation, or completion receipt resumes with the same request key/cache/budget. Another compaction or view start/select/close during the operation is rejected. Preflight rejection of a small budget does not leave cache locked. Retransmitting a completed response returns the original receipt without recompacting later cache writes. Read progress receipts through `status.cacheMaintenance`. Physical allocation comes from Windows observations of both journal generations; VSS, Git, and temporary files are measured separately.

`GitSourceResult` changes neither original index nor checkout. It enumerates frozen-source logical trees and reads only changed-path payload to create raw Git blob/tree/commit objects, reusing unchanged blobs from original base revision. It runs no filters/checkout transformations, excludes bridge/cache, and verifies `refs/splinter/validation-results/<operation-id>` and object existence. This final independent-result materialization cost is distinct from the prohibition on whole copies during candidate publication.

Preserved source is frozen and cannot run again; use a new select. Artifact release rechecks seals, locks the journal with a handle rejecting writers, stores reclamation intent for exact path/seal/metadata hash, then unlinks only that journal. After closing the lock, remove only known metadata with matching hashes. Windows behavior does not depend on renaming directories containing locked child files. New files, changed metadata, or different seals after interruption are preserved; reclamation intent and independent refs remain. Reclaim cache only through `close-view` and worker snapshot only through existing `workspace release`.

Product `workspace release` stops with `SnapshotInUse` when prepared incomplete intents, candidates, source generations, or open snapshot caches remain. Even after all source generations are reclaimed, cache-basis snapshot cannot be reclaimed until `close-view` receipts/common ledger complete. Do not guess that original VSS disappeared or read shared snapshot costs as 0. Follow dependent-artifact preservation/reclamation → cache close → current worker `workspace checkpoint` → `workspace release`, retaining exact IDs, isolation guards, and `reclaim-pending` rules.

<a id="지속-상태와-재시도"></a>
## Durable state and retries

Common ledger lives at `validation-workflows/<project-hash>/state.json`; actual bindings, generations, candidates, caches, application/WinFsp controls, and receipts live at `validation-product/<project-id>/`. Mounts use separate home paths under `validation-mounts/`. There is no general owner change or automatic old-format migration.

Serialize view commands for the same project; worker preparation/publication also use existing workspace operation locks. Common ledger flushes intent before effects and frozen seals before preservation. Publish same-directory flushed temporary files through `File.Replace` or initial nonoverwriting move. Readers allow delete sharing. Incomplete/unsupported JSON and unknown files are preserved/rejected.

After interruption, retry the same command/key/input. Common coordinator first looks for existing provider receipts; partial publication prefixes, complete journals, and lost receipts are reconciled only against the original source seal. Only a complete next ledger revision in the current format is published on explicit retry; read-only inspect does not automatically publish pending state. `replayed=true` is a historical response, not a new observation of current execution state.

Inputs/outputs use `view-lifecycle-request`/`view-lifecycle-data`; common requests/responses/state use `view-workflow-*` schemas. `ViewWorkflowTests` checks fake conformance/CLI responses; `ImmutableCandidateTests` checks separate snapshot/cache journals; `GitSourceResultTests` checks actual Git objects/original-index preservation; `JournalReclamationTests` checks actual file locks/interrupted reclamation recovery; `SnapshotWorkspaceCoordinatorTests` checks product reclamation dependencies. [Storage-view contract verification](verification/storage-view-contract.md) owns current automated regressions and actual-execution limitations. Reboot, power/physical-space failures, general assets, and large projects remain unqualified; generated-source merging and mode selection are AI procedures.
