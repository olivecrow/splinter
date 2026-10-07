<a id="스냅샷-지연-회수-검증"></a>
# Deferred snapshot reclamation verification

[한국어](../ko/verification/deferred-snapshot-reclamation.md)

The execution scope is [first-stage deferred reclamation](../ntfs-workspaces.md). The baseline commit is `79f77ab`; the candidate contains in-progress changes on development. The evidence root is `artifacts/storage-foundation/deferred-20260926/`.

The defined deferred-reclamation automated contracts, fakes, actual helper functions, and actual isolated NTFS/Git path were validated. This does not extend to Unity execution or full NR support.

The first standard build's automated tests returned 248 passed, 3 failed, and 1 test excluded at that time skipped out of 252. Two failures were new descriptor retry values outside the existing enum; one was a CLI fixture missing required requestDigest. The descriptor retained existing `idempotent-release`, and the fixture was corrected. The [build after correction](../../artifacts/storage-foundation/deferred-20260926/build-03.log) returned 251 passed, 0 failed, and 1 test excluded at that time skipped out of 252. Locked restore inside the sandbox exited without diagnostics, so the same standard build ran in an ordinary-permission environment.

<a id="실제-ntfsgit-수명주기"></a>
## Actual NTFS/Git lifecycle

The existing owned `isolated-ntfs.vhdx` was reattached and a small new Git fixture created at `V:\sf-deferred-20260926`. Only V:'s VSS limit was set to 10GiB; C:'s 64GiB remained unchanged. Creation/ensure, status queries, and release of two product workspaces used the non-administrator CLI. No Unity Editor ran and no checkpoint validation was fabricated. Source changes were preserved as fixture Git commits before checking release's independent-result collection.

| Item | Result and evidence |
| --- | --- |
| Different start times | Different VSS IDs and original source baselines confirmed in [A](../../artifacts/storage-foundation/deferred-20260926/ensure-a.json) and [B](../../artifacts/storage-foundation/deferred-20260926/ensure-b.json) |
| Reclaim B first | [Response](../../artifacts/storage-foundation/deferred-20260926/release-b-deferred.json): `Accepted`, `terminal=false`, `reclaim-pending`. A's ID is the blocker; B's result and cleanup of Git registration/delta/mount are distinguished from the remaining snapshot |
| Status/list from a new CLI | [status](../../artifacts/storage-foundation/deferred-20260926/status-b-pending.json)/[list](../../artifacts/storage-foundation/deferred-20260926/list-pending.json) retain the same attempt and wait. Queries do not delete or retry |
| A continues working | After B waits, A reads its baseline and edits `result-A`; [current source verification](../../artifacts/storage-foundation/deferred-20260926/status-a-after-b.json) detects the change |
| Explicit reclamation order | [A release](../../artifacts/storage-foundation/deferred-20260926/release-a.json), then [B release retry](../../artifacts/storage-foundation/deferred-20260926/release-b-retry.json), leave both tasks `released`. Owned processes/memory/handles are 0; snapshot/mount/delta are absent |
| Independent results/original | `result-A`/`result-B` read from Git result refs; source HEAD, marker hash, and clean state retained; only the source remains registered as a Git worktree |

The assessment in [live-result.json](../../artifacts/storage-foundation/deferred-20260926/live-result.json) is **PassedDefinedLive**. While B waited, VSS used space for the entire source volume was **7,815,168B**, allocated space **35,012,608B**, and limit **10,737,418,240B**. These are neither costs attributable to B nor bytes returned by deleting B. Workspace attribution is unknown. Final [administrator observations](../../artifacts/storage-foundation/deferred-20260926/probe-probe.json) show 0 snapshots and C:/V: VSS UsedSpace/AllocatedSpace of 0. This does not claim that all actual NTFS file allocation or RAM cache is 0.

<a id="권한-세션과-운영-마감"></a>
## Permission session and operational closeout

After the user noted the inconvenience of repeated UAC prompts, the remaining B blocker, A release, and B retry were handled in **one approved restricted administrator session**. The exact two owned snapshot IDs, helper hash, allowed Release operation, and 15-minute expiry were fixed; ordinary CLI calls remained non-administrator. Including the final probe/detach, [4 requests were handled and the session ended](../../artifacts/storage-foundation/deferred-20260926/release-session-stopped.json). After [VHD detach](../../artifacts/storage-foundation/deferred-20260926/detached.json), the fixture, independent Git results, and receipts were preserved in the VHD and artifacts. This historical execution did not change UAC settings or install the product service. Separate assessment of the installed product service follows [UA verification](storage-privilege-service.md).

The test harness corrected path composition for V: before attachment and PowerShell function/alias name conflicts. These were preparation errors before product snapshot creation; recovery neither deleted other snapshots nor modified existing source. Those error files also remain in artifacts.

<a id="후보와-제한"></a>
## Candidate and limitations

The real-environment candidate's 141 inputs and DLL hashes are in [live-candidate-manifest.json](../../artifacts/storage-foundation/deferred-20260926/live-candidate-manifest.json). After real-environment completion, handling was strengthened to reject a missing helper-evidence `deletionAttempted` field as unknown rather than interpret it as false, and regression coverage was added. This final invalid-input handling is verified by automated tests; it is not marked as another actual VSS run. Final candidate hashes and automated tests are preserved in separate manifest/TRX files.

These results do not retroactively change FailedPreservation from the historical SF run. Overall SF-02 remains Partial, SF-03–06 remain NotRun, and gates for ordinary non-owned backup coexistence, external concurrent deletion, actual reboot/power/space failure, ARM A/B, and SW/UA remain. Automatic sweeping, cascading reclamation of other workspaces, and immediate physical reclamation in arbitrary completion order are not provided.

The final [standard build](../../artifacts/storage-foundation/deferred-20260926/build-04.log) completed locked restore, Release build, and automated tests with **252 passed, 0 failed, and 1 test excluded at that time skipped out of 253**. The [final candidate](../../artifacts/storage-foundation/deferred-20260926/candidate-manifest.json), [TRX](../../artifacts/storage-foundation/deferred-20260926/candidate.trx), and [closeout receipt](../../artifacts/storage-foundation/deferred-20260926/final-closeout.json) record scope and candidate differences. This task made no commit/push.
