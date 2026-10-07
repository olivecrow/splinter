<a id="ntfs-준비-단계의-cli-중단-복구"></a>
# CLI interruption recovery during NTFS preparation

[한국어](../ko/verification/preparation-recovery.md)

NR-01/02 `workspace ensure` preparation is **scoped-complete-with-limitations**. At 26 actual CLI-termination boundaries on the same final candidate, identical requests/concurrent calls reached Ready; independent results/normal reclamation and original/existing refs/non-owned snapshots preserved. [Final audit](../../artifacts/ntfs-recovery/nr02-20260930e/final-audit.json) fixes exact per-boundary workspace/snapshot/result identities/assessments. One state-publication failure's unconfirmed cause/explicit recovery record remain as limits below.

Final candidate connects [8 d runs](../../artifacts/ntfs-recovery/nr02-20260930d/results.json)/[18 e runs](../../artifacts/ntfs-recovery/nr02-20260930e/results.json) with identical binary hashes. After 8 completed d boundaries, a state-publication failure stopped harness; e reran remaining 18 starting with that boundary under new requests. Original-request resumption/reclamation of failed task is distinguished from new boundary runs.

<a id="후보와-실행-조건"></a>
## Candidate and execution conditions

- Baseline working tree after `development @ d9825ba`. [Binary hashes](../../artifacts/ntfs-recovery/nr02-20260930d/candidate.json) fix actual executed Release DLLs/JSON.
- [Code/schema/harness/installed-environment manifest](../../artifacts/ntfs-recovery/nr02-20260930d/code-and-environment.json) compared with e identical binary hashes. Implementation/installed privilege-service DLLs/JSON unchanged during execution.
- Product CLI ordinary-user; [service status](../../artifacts/ntfs-recovery/nr02-20260930d/service.stdout.json) confirms `callerElevated=false`, protocol 1, installed `SplinterStorageAuthority=running`. Existing grants/service installation reused.
- Clean NTFS/Git source `T:\ua-source`, base `codex/library-20260929`, revision `f71e4e244ba9b085aa0fc439336e4c60ee3d5d5d`. Home product-repository `.local/wl29`. No Unity Editor start.
- No new source Library/copies. Existing VHD/VSS budget/service/grants/original owner/global Git settings unchanged. Fixture Git-owner difference handled by exact-source exception in child-only `XDG_CONFIG_HOME`.
- [Required build results](../../artifacts/ntfs-recovery/nr02-20260930d/final-full.trx): 615 Passed / 0 Failed / 1 Skipped after locked restore/Release build. New common-preparation tests: 19 actual-child-termination/fake-recovery plus 2 subprocess/schema. Fakes do not count as VSS/WinFsp success.

<a id="실행-방법과-판정"></a>
## Execution method and assessment

Each boundary uses a new key. When development `preparation-probe` publishes a flushed signal at exact boundary, harness compares PID/start time and terminates only that CLI. Provider/privilege service not forcibly terminated. Read each directory's `request.json`, `signal.json`, durable interruption records, and ordinary ensure response together.

Two new ordinary ensure CLIs start concurrently. Lock's 5-second `WorkspaceBusy` reruns same request after earlier preparation finishes; first failure response preserved, not overwritten as success. Both Ready responses must share workspace/snapshot/provider identity. Response boundary repeats same request without using normal completion response. Each boundary checks changed-base repeat returns `RequestKeyConflict`. Ordinary `workspace release` then preserves independent Git result/assesses actual absent resources.

| Preparation boundary | Actual CLI termination point |
| --- | --- |
| Request/source | `request.before-record`, `request.after-record`, `source.before-effect`, `source.after-effect`, `source.after-record` — 5 checked |
| capture | `capture.before-effect`, `capture.after-effect`, `capture.after-record` — 3 checked |
| Git registration | `registration.before-effect`, `git-registration.after-effect`, `git-registration.after-record`, `registration.after-effect`, `registration.after-record` — 5 checked. First publication failure/fresh-key rerun distinguished |
| delta | `delta.before-effect`, `delta.after-record` — 2 checked |
| provider | `provider.before-effect`, `provider.after-effect`, `provider.after-record` — 3 checked. Pre-interruption intent matches resumed provider generation/PID/start |
| mount | `mount.before-effect`, `mount.after-effect`, `mount.after-record` — 3 checked |
| Independent index/Ready | `attach.before-effect`, `attach.after-effect`, `ready.before-record`, `ready.after-record` — 4 checked |
| Response | `response.before-publish` — 1 checked. Repeat after completion-response loss retains identity |

Capture after-effect is after restricted service returns capture receipt but before Runtime saves snapshot ID. This does not terminate inside native VSS calls/helper capture-return publication. Git after-effect follows actual `worktree add --no-checkout` before registration receipt; after-record follows receipt before pointer-only preparation-directory cleanup. Delta-identity publication itself is creation receipt, so no separate after-effect/after-record interval. Index tests before/after attach, not index.lock interruption within Git commands. Ready is before/after state publication without external side effects.

Provider before-effect precedes durable launch intent. Short ambiguous OS-process-creation interval after intent publication retains existing `OverlayStartUncertain` preservation/rejection contract. After-effect is after process creation/before parent receipt. Child verifies matching token/workspace/generation intent and publishes own PID/start receipt; ordinary same ensure continues checking that live provider. Different owner/generation/corrupt records/unknown snapshot do not trigger guessed reruns. Rejection regressions are automated `OverlayMountObservationTests`, `OverlayControlReadTests`, `OverlayReadinessTests`, existing state/identity tests.

`OverlayLaunchReceiptTests` runs 6 cases with actual temporary files: missing/corrupt intent, different owner/token/generation, empty generation. Child launch must reject before native mount, preserve intent/state/receipt bytes, and not create delta/mount. Publishing new failure state before permission verification also disallowed. These automated tests differ from actual normal-provider-start qualification.

<a id="보존과-해석-제한"></a>
## Preservation and interpretation limits

Original HEAD/index hash/tracked-file hashes/dirty state/previous refs/existing worktree registrations compared before/after. New test branches/independent-result refs are preserved outputs. Changes in physical allocation of full ignored Library/per-workspace VSS attribution are not test oracles.

Final audit compares original before d/after e and confirms all identical. Each trial confirms `initialPayloadCopiedBytes=0`, `readCacheBytes=0`, one owned snapshot/list of 3 existing non-owned snapshots. Cases interrupted after provider creation retain intent generation before parent publication/child receipt/Ready-mount PID/start. No replacement capture/provider passes.

Each release requires `released`, absent owned provider/mount/delta/snapshot, handles/working set/private bytes 0. `release-isolation.json` before/after lists/`missingOtherIds` compare non-owned snapshot preservation. Unknown/reclaim-pending are incomplete. Exception injection/manual JSON editing do not substitute.

All 26 boundary independent refs/`stable-journal-v1` results verified; actual runner query 0. Failed/interrupted candidates/additional tasks recovered with original requests also `released` after preserving results. Durable receipts/failed temporary files/result refs are intentional evidence, distinct from absent resources.

Candidate did not run Unity code/asset authoring, Play, ARM A/B, reboot/power/physical-space failures, or other write/flush/result/reclamation interruption boundaries. This does not complete all NR-01/07/NR support.

Product validation view also shares provider launch. Previous actual WF/LB Unity/Library records qualify each fixed candidate, not automatic validation/Library requalification of this candidate. Only execution's `ValidationView=null` preparation boundaries/fake-workflow regressions checked on current candidate.

<a id="보존한-실패와-재검증-조건"></a>
## Preserved failures and requalification conditions

Pre-work `d9825ba` full suite: 587 Passed / 1 Failed / 1 Skipped. `WindowsProcessTests.SupervisorSurvivesClientAndCleansDescendantThatOutlivesRoot` failed access denial deleting temporary `Splinter.Core.dll` in `TestDirectory.Dispose`; standalone same-binary rerun 1 Passed. Cause unconfirmed/test unchanged, distinct from passing final full suite. [Baseline failure/standalone rerun](../../artifacts/ntfs-recovery/scope-20260930/) retained.

Harness-preparation failures from Git-function recursion/safe.directory path formatting/product children removing `GIT_*` settings remain in `nr02-20260930`, `nr02-20260930a`, `nr02-20260930b` evidence. Last failure left only new task record before capture. Exact original-request resumption/independent-result/normal-reclamation responses for task `82034cc66236f1251c2dbe56` in [interrupted-candidate reclamation](../../artifacts/ntfs-recovery/nr02-20260930c/owned-cleanup/). Not counted as boundary Passed.

First implementation candidate actual tests stopped at task 15 to strengthen rejection of failure-record publication before permission checks. [Independent results/reclamation of 15 tasks](../../artifacts/ntfs-recovery/nr02-20260930c/stopped-run-cleanup.json)/first-candidate 609 Passed / 0 Failed / 1 Skipped retained. Fourteen completed boundaries distinguished from normal reclamation of in-progress task. First-candidate partial passes not inherited; all 26 boundaries rerun fresh on final candidate.

At final-candidate d `registration.before-effect`, first new CLI returned `StateWriteIncomplete` while publishing `snapshot-ensured`. `ready` record already remained/failed temporary file preserved. Concurrent CLI, after first `WorkspaceBusy`, reached Ready on same snapshot/provider using same request. Explicit original-request status/ensure/release responses/failed temporary file in [trial](../../artifacts/ntfs-recovery/nr02-20260930d/registration.before-effect/). Lower HResult absent from existing machine-readable response, so sharing/access/space cause unestablished; no guessed Runtime-retry changes. Unconfirmed publication failure not overwritten as normal pass; actual resumption/independent result/reclamation/fresh-key rerun assessed separately.

Changing preparation order/capture-Git identity/delta initialization/provider execution-receipts/schema/locking contracts requires rerunning relevant new-candidate boundaries. Actual passes with previous binary hashes are not automatically inherited.
