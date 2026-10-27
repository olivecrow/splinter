# Lifecycle bottleneck follow-up

[한국어](../ko/plans/lifecycle-bottlenecks.md) · [Verification](../verification/lifecycle-bottlenecks.md)

## Fixed scope

Reduce an original Library restoration's per-file work when surviving paths and bytes are unchanged. Distinguish the remaining callback/queue/flush and Git startup costs with bounded observations, and retain current Unity startup artifacts for attribution. The input is the current code after RR-01; its historical whole-workflow timings are not a before/after result for this change.

| ID | Implementation / evidence boundary |
| --- | --- |
| BO-01 | Exact final equality permits positive sealed sequences on the existing directory route. Metadata updates and existing leaf deletions use a separate sparse directory plan. General file/range restoration remains for unsupported topology/content and already-started file plans. |
| BO-02 | Same fixed 10,000-file ordinary NTFS fixture, one leaf deletion, full restore through verification/reclamation; frozen baseline and candidate, serial ABBA. No Editor or mounted-provider qualification. |
| BO-03 | Bounded WinFsp admission/batch/store-call and mutation timing, successful ACK after existing durability, explicit drain/failure behavior unchanged. Controlled OFF/ON callback cost comparison is separate from Unity. |
| BO-04 | Bounded Git reader/proof/cache observations and opt-in attach intervals. No increased reader/cache budgets or skipped proofs. |
| BO-05 | Read-only development startup capture with exact task/provider/Editor identity, bounded Bee artifacts/logs and before/live correlation. No Editor launch/stop or automatic profiler injection. |

## Restoration contract

The initial sparse route is Unity Library-specific and accepts latest/bounded journals only. It requires the same directory set, original surviving file paths/lengths, no private ranges or new files, and only metadata patches or deleted original leaf files. Before moving anything, bind the basis, exact seal, original/delta paths, all NTFS identities, final metadata and sorted deletion inventory into the new plan. Root/ancestor fences remain; descendant handles reopen after the root rename and verify complete surviving contents and identities.

Read and hash the plan through the same pinned handle, and compare its final effects with a metadata-only replay of the exact sealed journal. Keep the read-only journal open through final verification and staged-leaf reclamation. This also applies to the unchanged route: a positive sequence alone does not establish final equality.

Move deleted leaves to predetermined names within the pinned existing receipt root, verify exact membership, apply final metadata and publish the plan-bound verified receipt before destroying any staged data. A separate reclamation receipt authorizes missing staged entries during resumed deletion. The final survivor pins remain throughout; a third full scan is unnecessary while their proof remains held. Journal reclamation uses its existing exact inventory and seal. Full basis and post-move content proofs and survivor metadata application remain; this reduces per-survivor moves, receipts and cursor publication, not the required whole-input verification.

An existing general or unchanged-directory plan keeps its route. Conflicting/orphan plans, foreign targets, replacement identities, changed contents or seals are preserved and refused. No older plan is migrated. Current retry semantics still reapply planned metadata; this does not promise to preserve every external metadata-only edit after interruption.

## Diagnostics and remaining decisions

`writeAdmission` and `gitReads` are opt-in provider-local diagnostic observations, not recovery inputs or proof of correctness. Enable `SPLINTER_WRITE_DIAGNOSTICS=1` and/or `SPLINTER_GIT_PREPARATION_DIAGNOSTICS=1` in the launching process; existing providers keep their launch configuration. Detailed observations are null by default. They use monotonic ticks/frequency and cumulative counters; concurrent/nested totals must not be added to lifecycle elapsed time. WinFsp rings are bounded and report overwritten samples. Runtime deltas cover store-call intervals and may include administrative flushes.

Set `SPLINTER_GIT_PREPARATION_DIAGNOSTICS=1` for a diagnostic CLI process to publish `control/git-preparation.json`. Before attach effects, replace the previous record with a new attempt ID and `running` marker; failure to publish that initial marker prevents this explicit diagnostic attempt. Completion records `succeeded`, `failed` or `cancelled`, fixed UTC/monotonic anchors, request identity and bounded overlapping verification/read-tree/warm/refresh/final-head-status/admission intervals. Completion-publication failure preserves the actual operation result and leaves the attempt incomplete. Default operation does not create or update this diagnostic file. Compare attempt/time/request identity; neither a prior record nor an incomplete marker proves the current attach succeeded.

The [startup capture command](../development-cli.md) preserves current evidence before reclamation. Capture overhead, fixture timing and old RR-01 observations are separate. It does not enable Unity's profiler automatically; a controlled profiler cohort remains a subsequent external-agent procedure.

Choose further queue scheduling only if independent callbacks actually accumulate; a synchronous caller cannot issue its next dependent request before the current durable ACK. Choose Git demand-priority read-ahead only if unused eviction/redecode/wait is material. Increasing readers alone reduces the current per-proof window; increasing bytes alone leaves the chunk-entry limit. Unity bridge DLL conversion and small metadata commit savings remain deferred. No early ACK, full-copy cache, stale endpoint/DAG patch, external-tool mutation or retired storage backend is introduced.

## Exit conditions

Meaningful interruption/foreign-data/metadata/identity tests, required build, bounded diagnostic tests and matched ordinary-NTFS restore evidence. Do not present component savings as a new Unity whole-lifecycle saving. Further Unity execution needs fixed source/whole Library/loaded state and the same continuous start/end stopwatch, including normal stop and owned recovery/cleanup. Apply and discard have separate end conditions; neither inherits timing from restoration alone.
