<a id="ntfs-복구지원-경계-검증"></a>
# NTFS recovery and support boundary verification

[한국어](../ko/verification/ntfs-recovery.md)

Baseline date: 2026-10-01. This is the execution record for the [NR plan](../plans/ntfs-recovery-and-support.md). It confirms **partial implementation and automatic verification of NR-01/03/05/07, 58 actual child terminations for the common journal, 19 separate local WinFsp callback/mmap/new-provider connections, and 13 external client boundaries for product VSS/CLI**. Each execution unit is distinguished below by its separate candidate. Remote VSS read/reclaim recovery, rejection of 5 file APIs, and the single workspace using ARM Windows/x64 Unity are records of their respective earlier candidates; they do not complete NR as a whole.

<a id="새-live-vss-후보"></a>
## New live VSS candidate

Later fixes to the collection race, status cost, and common mount access follow the [separate verification record](README.md). The LV and remote Unity figures below are not inherited as real-environment passes for those fixed candidates.

The [LV-01–08 record](ntfs-live-vss-identity.md) verifies separation of capture/live identity from the current read location, and non-administrator Observe/resume for a new connection generation. The new candidate passed 180 automatic tests with 0 failures and 1 test excluded at the time skipped, actual VSS/Git reconnection, 4KiB range writes, original point-in-time isolation, 3 Windows x64 Unity validations, loaded-code after reconnection, an independent result, and final reclaim. It preserved 77 original files and 3 existing snapshots; owned resources are 0. The 139 passes in the next section and the remote ARM tests belong to earlier candidates. ARM, actual reboot, power, and storage failures are NotRun for the new candidate.

<a id="로컬-코드와-자동-시험"></a>
## Local code and automatic tests

The following contracts were connected on top of the existing NI candidate on `development`.

| Boundary | What was verified | Status |
| --- | --- | --- |
| Snapshot observation | `available`/`missing`/`unknown`, omission of unknown booleans, and preservation of path-file substitution as an identity mismatch | Automatic Passed |
| Recovery response | ready/observe/preserve/resume-ensure/resume-release/released with stable reasons and next commands; CLI/schema agreement | Automatic Passed |
| Same identity | ensure/reclaim preparation rejected while ordinary observation is unavailable; no snapshot recreation or delta reclaim; resume on the same basis after observation recovers | fake Passed |
| State read | Incomplete JSON, orphan temporary files, and locked JSON are not treated as new work; files are preserved | Actual file I/O Passed |
| State write | `StateWriteIncomplete` for a locked save destination; previous records and temporary files preserved | Actual file I/O Passed |
| Provider record | The same probe records `snapshot-unknown` when observation is unavailable; schema rejects phase/availability contradictions | Automatic Passed |
| Journal corruption | Even with a matching checksum, invalid JSON, missing entries, path escape, and invalid ranges return `DeltaCorrupt`; corrupt identity and original journal preserved | Actual journal file I/O Passed |
| Journal process termination | A separate child's completed 64KiB write is flushed to an external ACK file before actual forced termination; reconnection at 1/8/32 ACKs and matching second-replay hashes | Actual process / fake immutable tree, 3 cases Passed |
| File attributes during work | reparse/EFS/compressed/sparse create/overwrite/attribute callbacks rejected; existing bytes, attributes, sequence, and journal hash unchanged; ordinary attributes and unchanged sentinel reconnected | Callback/actual journal Passed |
| Final reclaim observation | Only exact release observation from the same boot resolves unknown; another boot, identity, or query failure is preserved; completion also blocked when resumed final reclaim remains unknown | Receipt/fake Passed |
| Full regression | locked restore → Release build → automatic tests; of 140 total, 139 passed, 0 failed, and 1 test excluded at the time skipped; 0 warnings/errors | Passed |

Evidence for the earlier NR-07 candidate is in `final-build.trx` and `source-manifest.json` under `artifacts/ntfs-recovery/nr07-release-observation-20260921/`. The preceding 128-test candidate is preserved separately in `nr01305-20260921/`, and the 112-test candidate in `nr01-20260921/`. `RecoveryContractTests`, `SnapshotWorkspaceCoordinatorTests`, `VssReleaseObservationTests`, `ContractAndCliTests`, `SnapshotOverlayTests`, and `OverlayFileAttributeTests` verify the added contracts. Existing journal/Git/Windows process tests also ran on the same candidate. A save failure caused by a file lock is not evidence of actual volume exhaustion, and direct callback invocation is not a file API test on an actual mount.

Additional process tests are preserved in the development harness and `trials-2/result.json` under `artifacts/ntfs-recovery/nr03-process-20260921/`. They use the same product `JournalDeltaStore` binary, but the basis tree is a deterministic fake. Final ACK sequences 1/8/32 were all retained, and replayed sequences were 1/9/33. This matches the allowed behavior in which the final record completed before receipt of an ACK may survive. Initial payload copy and read cache are 0. The first run stopped with sharing violation 32 immediately after termination; `trials/` and the failure explanation are preserved. The follow-up harness re-observes only sharing violations for up to 5 seconds after confirming owned process termination; the 3 passing cases required 0 such retries. This test is not evidence for WinFsp/VSS, mmap flush, interruption at a particular instruction inside the journal, reboot, or power loss.

The current snapshot probe observes root metadata and source-volume access. There is no evidence yet of revalidating the same VSS ID/provider/volume after reboot. Apart from the [NR-02 preparation boundaries](preparation-recovery.md), common journal boundaries, and local kernel boundaries below, the remaining interruption gates, normal reboot/forced power-off, actual space exhaustion/offline, full ACL/EFS/input-property matrix, and direct reclaim of partial preparation remain incomplete. The 5 API rejections on an actual mount and the single VIVOBOOK Unity workspace test below are separate from that full scope.

<a id="nr-03-공통-저널-경계의-실제-프로세스-종료"></a>
## NR-03 actual process termination at common journal boundaries

`artifacts/ntfs-recovery/nr03-journal-20260930b/` is the final candidate, including the NR-03 common observer/development harness after `108b594` and a sequence-error regression with a valid checksum. `source-and-environment.json` and `final-audit.json` confirm immutable hashes for 229 source files and 5 binaries, an ordinary-privilege caller, 58 actual terminations, and 0 remaining owned children. The Runtime SHA-256 is `e5ca90d81539e575ba992d1044760cc1151a59d8628df2b9ec390d30fead27e1`. The basis is a deterministic fake immutable tree; VSS, WinFsp, and Unity were not run. The sibling fixture remained clean.

Automatic tests and a separate plan execution confirmed that the default plan at the [development entry point](../journal-recovery.md) creates no files. The final `-Execute` matrix is **58 Passed / 0 Failed**. Each trial preserves its request, a signal matching nonce/PID/start time, actual termination observation, acknowledged prefix/final external ACK, original journal before replay, child stdout/stderr, and result. `result.json` summarizes the exact execution list below.

| Operation | Actual terminations | What was verified |
| --- | --- | --- |
| write | 11 | Before append/after header/after metadata/after half the payload/after the full payload/after checksum/before and after flush/after apply/before and after ACK following return |
| rename / replace / truncate / create / delete | 9 each, 45 total | The same boundaries without payload; namespace, length, and content are either fully applied or absent |
| Explicit Flush | 2 | Already acknowledged prefix retained after termination before/after flush |

All 58 cases preserved acknowledged sequence 4. Of these, 34 replayed at sequence 4, and the 24 after durable flush replayed at sequence 5. All 6 cases where the final external ACK was actually published retained the new content. The oracle still permits a complete transaction that has not yet returned to survive; however, in this run's pre-flush cases, the new transaction did not survive. This observation is not a guarantee that all unacknowledged writes disappear.

For write, only an incomplete tail of 33,129 bytes after half the payload, and 65,897 bytes each in the 3 cases after the full payload/checksum/before flush, was removed. In all 58 cases, the original acknowledged prefix was retained, and the post-replay file was an exact prefix of the original at termination. The second replay left namespace/content hashes and the original journal unchanged. Initial payload copy, read cache, and base payload reads are 0; measurement is limited to the synthetic private journal. Preservation of complete frames with checksum/sequence/JSON corruption was verified by existing automatic regressions on the same candidate; a corrupt frame is not truncated as a normal tail.

The final `pwsh ./scripts/build.ps1` completed locked restore and Release build with 0 warnings/errors, and **of 684 total tests, 683 Passed / 0 Failed / 1 test excluded at the time Skipped**. `final-full.trx` and `../nr03-journal-20260930-preflight/sequence-final-build.log` own the evidence; UTF-8 and diff checks passed. All 58 cases in the final matrix required 0 post-termination sharing re-observations. Original records of the preceding 58 successes and 682 automatic passes in `nr03-journal-20260930/` are also preserved. Runtime is the same, but the added direct sequence-corruption regression changed the test binaries/source, so the final matrix ran again in a new directory.

The first full test run was 679 Passed / 1 Failed / 1 Skipped out of 681. In `replace / commit.after-apply`, replay had not started when journal open failed with Win32 sharing violation 32 immediately after confirming exact child termination. The binary hashes and failure from that run are preserved in `../nr03-journal-20260930-preflight/first-full.trx` and `failure.json`. Re-observation of sharing violations 32/33 for up to 5 seconds was added only to the harness post-exit guard/open; access denied, absence, corruption, and replay errors are not retried. The first failure is not presented as a fix for product data corruption or a flush-policy defect.

The verdict is **scoped-complete-with-limitations for the common journal unit**; NR-03 as a whole is **InProgress**. Forced termination at individual partial header/checksum bytes, inside an OS flush call, or during replay tail truncation/flush; actual WinFsp callback response/reconnection; explicit mmap flush; concurrent reader/writer; VSS; reboot; power; and physical storage failures are NotRun for this common candidate. Actual qualification of the separate kernel candidate below, earlier LV/remote runs, or the NR-02 candidate is not automatically inherited by this candidate.

<a id="nr-03-로컬-winfsp-callbackmmap새-provider-연결"></a>
## NR-03 local WinFsp callback, mmap, and new-provider connection

`artifacts/ntfs-recovery/nr03-kernel-20260930/` is the final candidate containing the development observer/harness/schema after common-journal local commit `ab9c94e`. On Windows x64, .NET 10.0.12, and the existing WinFsp installation, product `JournalDeltaStore`/`WinFspOverlayFileSystem` and a deterministic fake immutable tree were connected to an actual mount. Actual VSS server/CLI, Git, Unity, and VIVOBOOK were not run. `source-and-environment.json`, `candidate.json`, and `final-audit.json` confirm identical hashes for 238 source files and 6 managed binaries, and a non-administrator caller. The Windows adapter SHA-256 is `501c232538f54d9479cb1390a5ace80d046b5314d2f8b66f92e2d1b75e74dc43`.

The final full matrix at the [development entry point](../kernel-recovery.md) is **19 Passed / 0 Failed**. Each trial preserves its request/signal/external prefix/ACK, exact termination PID/start time, original journal, reconnection request/ready, normal stopped state, stdout/stderr, and schema-validated result. At the post-effect callback boundary, the provider is terminated before a successful response; this is distinct from an external ACK.

| Operation | Actual terminations | What was verified |
| --- | --- | --- |
| write / rename / replace / truncate / explicit flush | 3 each, 15 total | Before callback/after effect before response/after client ACK; acknowledged prefix and completed new content retained |
| mmap | 4 | Before view flush/before and after file flush callback/external ACK after view/file flush completes; page-content oracle |
| New provider at the same path | Each of the 19 cases above | Replay of the same delta/identity, actual kernel read, new 4KiB beta write/flush, normal unmount |
| Concurrent Read of a different file | 1 case after write effect | A 4KiB read response/hash for beta opened before the gate is published during the gate; Close/reader termination confirmed after provider termination |

All 19 cases preserved the acknowledged prefix, and first/second replay left the original journal unchanged. Complete new content was required after effect/external ACK. In this run's observation before mmap view flush, the new page was absent; before/after the file flush callback, the page remained. An unacknowledged page may survive or be absent; this observation is not expanded into a guarantee. The mmap ACK is published only after both view flush and file `Flush(true)` complete. Initial payload copy and read cache are all 0. `baseReadBytes` for normal basis reads was recorded separately at 4,096–65,536 bytes.

The final `pwsh ./scripts/build.ps1` completed locked restore and Release build with 0 warnings/errors, and **of 695 total tests, 694 Passed / 0 Failed / 1 test excluded at the time Skipped**. `final-full.trx`/`final-build.log` are preserved. Tests include 5 common callback fake operations, 19 request combinations/schema, no path creation by the default plan, and existing regressions. UTF-8 and diff checks also passed. The final 19 cases confirmed 0 new-provider handles/retained contexts, mount absence, and 0 owned children. The final audit compared 41 mount observations and 204 PID/start-time evidence records across smoke/failure/preflight/final runs, and confirmed 0 remaining kernel child actors, all deltas preserved, and a clean sibling fixture.

The initial `nr03-kernel-preflight-20260930/` passed 1 case, then failed at a 15-second deadline while waiting for the reader process to terminate after write effect. At the time, `File.ReadAllBytes` did not distinguish open/read/dispose, so the actual waiting stage and underlying cause are **unknown**. Candidate hashes, signal, prefix, delta, stdout/stderr, `failure.json`, and adjacent `.log` are preserved. Based on code evidence that existing `Cleanup` also acquires the mutation gate for read-only handles, the follow-up harness separated the pre-opened Read response from Close/process termination. Product locking and flush policies did not change. The 19 successes in `nr03-kernel-preflight2-20260930/` are preserved too; the subsequent full build/new matrix including final capability wording is the final evidence above. The first failure is not presented as concurrent completion of new open/close or a fix for product data loss.

The verdict is **scoped-complete-with-limitations for the local kernel unit**; NR-03 as a whole is **InProgress**. Responses from new open/close during the gate, atomic concurrent writes to the same file, interruption/reconnection of actual VSS server/product CLI, interruption inside OS flush, termination during replay, reboot, power, physical storage failures, and ARM/VIVOBOOK are **NotRun** for this candidate. Real-environment qualification from earlier common-journal/LV/Unity/remote candidates is not automatically inherited. If VIVOBOOK testing is needed, inform the user before execution.

<a id="nr-03-제품-vsscli의-클라이언트-경계"></a>
## NR-03 client boundaries for product VSS and CLI

The final PNG candidate's actual full matrix is **13 Passed / 0 Failed**; the product external-client unit is **scoped-complete-with-limitations**. `candidate.json`, `fixture-attributes.json`, `frozen-source.json`, and `final-audit.json` in `artifacts/ntfs-recovery/nr03-product-png-20261001/` confirm immutable hashes for 244 source files and 20 managed binaries. `environment.json` at the same path owns immutable hashes for the installed restricted privilege service and WinFsp files; installation/grants and volume/budgets were not changed. The CLI caller is a non-administrator. Final `pwsh ./scripts/build.ps1` reports **of 699 tests, 698 Passed / 0 Failed / 1 test excluded at the time Skipped**, with 0 warnings/errors; `final-full.trx`/`final-build.log` are preserved.

The [development entry point](../product-write-recovery.md) uses the existing registered clean `T:/ua-source @ f71e4e244ba9b085aa0fc439336e4c60ee3d5d5d`, base `codex/library-20260929`, and product `.local/wl29`. The actual basis is VSS with ordinary product ensure/server/WinFsp/Git composition. It does not use a fake basis or attach an observer to the provider. The 129,189-byte oracle totaling source FBX and explicit binary PNG is test-evidence cost, not product initial copying. The service's capture/release-isolation queries compare the 3 existing snapshot IDs. Direct global VSS queries by an ordinary user are **unknown** due to access denied/initialization failure, and are not shown as 0.

| Operation | Actual terminations | What was verified |
| --- | --- | --- |
| write / rename / replace / truncate / explicit file Flush | 2 each, 10 total | Before client operation/after external ACK; acknowledged prefix and completed content/namespace retained |
| write | 1 | New 4KiB content retained even after termination following Write return and before explicit file Flush |
| mmap | 2 | Page oracle before view Flush/after view/file Flush and external ACK |
| Same-request reconnection and explicit preservation/reclaim | Each of the 13 cases above | Same snapshot/delta/Git registration, new current-read verification/provider, repeated ensure unchanged, new write, independent ref/blob, final resources 0 |

All 13 cases resumed with the same ensure after actual termination of the exact provider token/workspace/snapshot/generation/PID/start time and mount absence. The original journal was retained on first replay and second ensure; second ensure used the same new provider. After a new 4KiB beta write/flush, the independent Git ref/blob/namespace from ordinary `validation preserve-worker` was verified and collected using `stable-journal-v1`. No Unity validation success is awarded. In this run's pre-mmap-view-Flush observation, the new page was absent; after ACK following view/file Flush completion, it remained. An unacknowledged page may either survive completely or be absent; this observation is not expanded into a general guarantee. Product initial payload copy and read cache are 0 in all 13 cases.

Ordinary release confirmed snapshot/mount/provider absence, 0 handles/working set/private memory, mount/Git registration directory/delta absence, and exact owned-process termination in all 13 cases. The final audit compared **21 owned workspaces and 59 provider/client PID/start-time evidence records**, including the failures/partial runs below. It confirmed all independent-result refs preserved, 0 owned children, 3 existing unowned snapshots preserved, unchanged original HEAD/index/tracked hashes/existing refs/worktree from before the first smoke to after the last run, and a clean sibling fixture. Raw journals, current candidate, and failure evidence were not deleted.

<a id="보존한-실패와-재검증-조건"></a>
### Preserved failures and revalidation conditions

- The initial write-before-effect in `nr03-product-smoke-20261001/` failed with a sharing violation in harness `File.ReadAllBytes` after product reconnection. This was an execution-order error: the restricted-environment build had exited 1 without being checked before execution, so the pre-fix candidate ran. The executed DLL/JSON and `first-full.trx` are preserved. ReadWrite/Delete sharing and same-path guard/open re-observation for IPC were added only to measurement; product locking/flush policies are unchanged. The prefix/basis bytes of the same workspace `ceec9769dcfadfac31d7fa35` were verified, and ordinary preserve-worker/release completed independent ref `ee65032673fe7c09d71ae126b8cddd3056f073b9` and full reclaim. The first failure is not changed into a pass.
- The inputactions candidate in `nr03-product-20261001/` completed 3 cases—before write/after ACK/before rename—then returned `StateWriteIncomplete` while saving `captured` state during the next initial ensure. The mutation boundary was **NotRun**. Workspace `0ec42f18abf0c388465584b2`, snapshot `{F9A02240-C144-4982-B01A-525346E10BDD}`, complete tmp JSON, failure response, and status are preserved. Same-request ensure recovered Ready for the same snapshot, and an unedited independent result/release completed. It has the same error code as the earlier NR-02 publication failure, but the underlying HResult/cause remains **unknown**; retry policy was not changed by conjecture.
- `nr03-product-20261001b/` on the same inputactions candidate completed 2 new cases—after rename ACK and before replace. After replace ACK, the original VSS/journal/reconnection oracle passed, but the independent Git blob's raw-byte hash differed and failed. The fixture moved FBX to a path declared `*.inputactions text eol=lf`. `normalization-evidence.json` proves that 105,117 bytes, produced by removing exactly 3 CR bytes from CRLF in 105,120 bytes, match the Git blob hash. The original/canonical Git blob/raw journal/executed DLL/JSON are preserved, and the same result/ref/release completed for workspace `56a39a5c8a16ba8ef897dbb9`. The original is also retained as independent `independent-raw-beta.bin`. This is not presented as a Git declaration-policy change or a fix for a product normalization defect.

The PNG candidate is a different binary/input for which `text: unset`/`binary: set` on `Assets/TutorialInfo/Icons/URP.png` was actually verified. The 5 completed boundaries of earlier candidates were not inherited; all 13 ran anew. NR-03 as a whole is **InProgress**. Interruption inside actual VSS callbacks/OS flush, termination during replay, concurrent writer/read open/close, Unity/ARM, reboot, power, and physical storage failures are **NotRun** for the current candidate. The underlying cause of preparation-stage `StateWriteIncomplete` also remains unknown. VIVOBOOK was not used; inform the user before execution if testing there is needed.

<a id="vivobook-준비"></a>
## VIVOBOOK preparation

The empty `splinter-test-vivobook` in the user-designated remote task is used. The fixture was copied via Git bundle to remote child directory `splinter-unity`. The remote side separately verified expected revision `46e7f4dbdb219e4afa602f3598a3de9ff4070fae`, bundle SHA-256, 77 tracked files, and clean `development` agreement. Original `Assets`/`Packages`/`ProjectSettings`/`.meta` and Git history are preserved; `Library`/`Temp`/`Logs` were not copied. This is initial test-environment preparation, excluded from product workspace-copy measurements.

The environment supplied by the user is ARM Windows 11, 32GB RAM, and 342GB capacity. Capacity is not substituted for actual free space; the remote side measured it. At 12:34 UTC, the system NTFS volume had 336.618309GiB free. .NET SDK 10.0.112 ARM64 ZIP was prepared in project-local `.tools/dotnet` after comparison with the official SHA-512. **The test Editor is the existing x64 Unity 6000.3.22f1**. The user-selected existing installation is retained; no ARM64 Editor is added. This fixture's first canonical x64 Editor import is recorded in the preparation section below, separately from product Unity workspace verification.

Because the recent remote Codex task body was empty when queried from this side, a temporary local transfer server serving only designated files was used. Results were received directly as minimal bool/error-code JSON. Reports containing account names/full paths were not transferred, and the user was not asked to relay or manually copy data. Evidence is stored in `artifacts/remote-vivobook/transfer/vivobook-report-*.json`. This transfer server is not a product feature.

The user-approved 162 current product source files and per-file manifest were sent to the remote side. ZIP SHA-256 is `3d3efb9239d55a18a8a622ba76af1335efc2c4252eaa92915f51dbb83a74005c`; test-source aggregate is `8422f019575a3a2bb48a239f9220f2e3ba51bb34079d9982563dc8f5b0e8ad55`. ARM64 .NET locked restore/Release build/automatic tests report **128 total, 127 passed, 0 failed, and 1 test excluded at the time skipped**. Transfer hashes, source immutability, and final TRX hash were verified. `WindowsProcessTests` verifies the executable by directly receiving `$PSHOME` from PowerShell started through PATH in an atomic file, so it neither fixes an installation path nor depends on external `Process.MainModule` queries.

After direct user approval in the remote task, **installation of WinFsp `2.2.26215 Beta4` Core/user runtime completed**. MSI exit code 0, no reboot required, ARM64 files/signatures, and launcher execution were verified. The separate tests below also confirm actual kernel mount and file I/O. Successful installation and execution tests are separate evidence; existing x64 Unity files/package declarations were retained.

At 12:34 UTC, the same system volume's VSS diff-area had a 19.027344GiB limit, 16.620625GiB used, 16.971958GiB allocated, and 2.406719GiB limit headroom, below capture's 8GiB headroom requirement. The user-approved increase-only plan is in `artifacts/remote-vivobook/vivobook-vss-budget-plan.json`. After approval forwarded by the parent was rejected, direct user input on the remote side passed the approval boundary; **the 13:20 UTC report confirmed the limit changed to 32GiB and 4 existing snapshots were preserved**. No limit reduction, existing-snapshot deletion, automatic reboot, or physical storage exhaustion was performed.

The remote fixture's coordination `0.3.0` and Coplay `10.2.0` declarations/locks agree. Existing Unity MCP provider settings and Python/uv preparation were reused; the canonical x64 Editor's first import, HTTP connection, and coordination preparation completed. The interactive user's Git ownership check succeeded for read-only queries with process settings limited to the exact fixture. Global Git safe-path restrictions were not relaxed and packages were not upgraded. **The product Unity single-workspace normal regression passed in the record below; A/B, reboot, failure recovery on the remote PC, and the remaining file-support boundary tests are still outstanding.**

After usage limits were lifted, the same remote task resumed at 13:05 UTC. Around 13:07 UTC, explicit-host queries failed temporarily, but automatic host discovery confirmed the same running task and new reports. This is not evidence of PC power loss or remote-execution interruption.

The first actual product-workspace attempt stopped in Git preflight with `GitCommandFailed`. Because the product Git adapter removes external `GIT_*` variables, it did not inherit the process-scoped trust setting used for standalone queries. The 13:20 UTC report confirmed an unchanged original index and 0 owned snapshots/mounts. Later, **non-administrator creation succeeded on the same fixture without changing product Git defenses or user-global Git settings**. No new fixture or duplicate smoke was created. The first failure is not counted as a VSS/mount failure.

<a id="vivobook-제품-workspace-읽기와-회수"></a>
## VIVOBOOK product workspace read and reclaim

Ordinary product CLI `workspace ensure --storage ntfs-snapshot` connected actual VSS and WinFsp. `vivobook-report-1789997092011864500.json` records non-administrator runtime ready, successful capture/mount, and 0 initial payload copy/read cache. This stage is separate from installation above or the fake-tree mount test below.

`vivobook-report-1789997324666452000.json` confirms **byte equality for all 77 tracked files**, unchanged payload after reads, read cache 0, and an unchanged original index. Source was not edited and Unity was not run. Ordinary release preserved the unchanged basis commit as an independent result; it did not create validation success.

`vivobook-report-1789997930391652500.json` records **0 owned snapshots/mounts/providers/deltas/handles/memory** after actual reclaim, 4 existing snapshots preserved, restored original Git worktree registration, and independent-result preservation. An intermediate UAC cancellation was resolved. However, ordinary CLI interpreted Win32 21 on the deleted device path as `SnapshotObservationUnknown`, leaving the workspace in `releasing`. Physical resource reclaim and product state-transition failure at that point were preserved separately. The resume test on the fixed candidate below also completed the final state transition.

`vivobook-report-1789998203275918300.json` confirms snapshot/owner agreement in the successful release receipt and the helper's absence observation. The current VSS-list query in the same non-administrator context failed with HRESULT `0x80041014`. The fixed candidate compares the exact helper's absence observation against the current boot time; if the receipt is from an earlier boot, it refreshes observation with a read-only Observe helper. Ordinary unknown remains; this observation-refresh path is allowed only at the final reclaim stage. fake/receipt tests distinguish current boot/another boot/query failure/different owner, snapshot, or provider/reused path/final unknown blocking. These policy tests are not actual reboot tests.

`vivobook-report-1789999527420230800.json` records the fixed candidate's **successful reclaim resume for the same workspace**. Transferred ZIP SHA-256 is `c8b20aa2e13c4cbad89049d5f78ab856c7e989c9c635c055daef03fedd28a250`; test-source aggregate is `78d285261a1a5899f137c39e1193eb3dc0c6e00d1d4523870bdd709c7af7140e`. After remote ZIP/file-hash verification and locked restore/build, 139 of 140 tests passed, 0 failed, and 1 ReFS test skipped, with 0 warnings/errors. The Observe helper did not change the original release receipt; it refreshed only an absence observation matching the owned snapshot/current boot. Subsequent ordinary non-administrator `workspace release/status` both exited 0, phase was `released`, and the previous error was resolved. Workspace JSON was not manually edited and the workspace was not replaced with a new one.

Final observations are 0 owned snapshots/mounts/providers/deltas/processes/handles/working set/private bytes, 4 existing snapshots preserved, and preservation of original HEAD/index/77 files, original Git worktree registration, and independent-result ref/tree. Unity did not run in this test. The read stage used an actual snapshot from the preceding candidate; the fixed candidate continued its durable state to reclaim. This compatible resume evidence is not expanded into full new-capture/Unity/reboot testing of the new candidate.

<a id="vivobook-x64-unity-초기-준비"></a>
## VIVOBOOK x64 Unity initial preparation

`vivobook-report-1790000221236042200.json` records execution of only one existing x64 Unity `6000.3.22f1` as non-administrator, completing the canonical fixture's first import in **179.423 seconds**. It was within the 180-second preparation budget, with 0 compilation-error log lines. Tracked source/HEAD, package manifest/lock, and staged content were retained. Git index stat/cache updates changed its byte hash, so index-byte immutability is not claimed.

The first import's Library has logical size **1,837,943,930 bytes** and **32,041 files**. This is initial preparation cost for an empty remote fixture, not product workspace initial payload-copy/read-hydration measurement. No new VSS or provider, or ARM64 Editor, was installed.

Initially the saved HTTP endpoint was not listening, so MCP initialize failed with `UnityMcpEndpointConnectionFailed`. Later, in `vivobook-report-1790000999923943400.json`, the remote native UI pressed the existing Start Server button, and actual HTTP connection/first coordination status succeeded. The observed agent/claim count was 0, with no active editor-access lease. The user was not asked to operate a button or install a new provider.

`vivobook-report-1790001786937894400.json` reports final Unity preparation success/cleanup success, 0 Console errors/1 warning, and a final preparation check of 653ms. The remote task identified the warning as a 60-second wait warning from before connection, and also observed `Session Active`. Initial import time and this check time are not combined or treated as product workspace performance. Detailed instance/lease data remains on the remote side; only approved minimal results were received. Play/Test and product Unity workspace code execution are separate tests.

<a id="nr-05-실제-mount의-파일-api-거부"></a>
## NR-05 rejection of file APIs on an actual mount

Using the same product candidate's `JournalDeltaStore`/`WinFspOverlayFileSystem`, 5 APIs were tested on an actual WinFsp mount in an ARM64 process. The immutable basis is a deterministic fake tree; VSS and the original Unity project were not used. The local plan is `artifacts/ntfs-recovery/nr05-mount-api-20260921/plan.json`; raw harness/per-API results are in remote `artifacts/nr05-kernel/`.

| Request | Ordinary NTFS positive control | Actual mount |
| --- | --- | --- |
| `CreateFile` named data stream | Success | Rejected |
| `CreateHardLinkW` | Success | Rejected |
| `FSCTL_SET_SPARSE` | Success | Rejected |
| `FSCTL_SET_COMPRESSION` | Success | Rejected |
| `FSCTL_SET_REPARSE_POINT` mount-point buffer | Success | Rejected |

The final minimal report `vivobook-report-1790001786937894400.json` is **5 passed, 0 failed, 0 NotRun, 327ms, cleanup successful**. The remote task verified preservation of file content/metadata/journal before and after rejection, ordinary write/rename/delete behavior, and mount/process/handle reclaim. The initial 107ms run stopped before API testing because of an incorrect journal-read sharing setting in the harness; all 5 were NotRun. The first failure/data are preserved, and only the rerun after a harness-only fix is recorded as passed.

This evidence covers rejection boundaries for unsupported APIs. It is not expanded into original preflight, VSS input properties, EFS, ACL, long paths, all reparse types, or support for ordinary NTFS properties. To remain within the approved detailed-transfer scope, individual Win32 codes and raw paths/logs were left in remote data.

<a id="arm-windows의-제품-unity-단일-작업공간"></a>
## Single product Unity workspace on ARM Windows

An ordinary CLI new `ntfs-snapshot` task is tested with existing x64 Unity `6000.3.22f1`, remote fixture revision `46e7f4dbdb219e4afa602f3598a3de9ff4070fae`, and the `78d28526…` product candidate above. The plan and minimal-report connections are in `artifacts/ntfs-recovery/nr08-vivobook-unity-20260922/plan.json`. Raw CLI/snapshot/process/Git/Unity evidence remains on the remote side.

The first capture request was canceled before starting. `vivobook-report-1790002929162919600.json` confirmed 0 waiting, successful cleanup, and 1 Unity test NotRun. The user also reported that no UAC window was visible, so the previous waiting report is not evidence that a window actually appeared. The same-task retry was confirmed in `vivobook-report-1790003097919600000.json` as **capture 2,895ms, successful, 0 waiting**.

The single task requested this sequence: verify default probe value 13 → change only the returned workspace's probe → verify value 20 in the new generation and validation for the current fingerprint → independent checkpoint → release without original integration. `vivobook-report-1790003606358959700.json` is an intermediate report at checkpointing after 2 validations passed. Final workspace report `vivobook-report-1790004051972464600.json` confirms **2 passed, 0 failed, 0 NotRun, overall success, workspace_released, 0 remaining owned workspace resources, cleanup successful**. Raw execution values/generation/fingerprint/result ref/manifest remain in remote evidence; only approved minimal results were sent to the parent.

`vivobook-report-1790004706952742900.json` is the final report completing both the test and canonical preparation-session cleanup. It confirms 2 validations passed, 0 failed, 0 NotRun, 0 owned workspace resources, successful workspace cleanup, successful normal shutdown of the canonical Editor/MCP session, and 0 waiting. Preservation of the original Editor/MCP was first verified after workspace reclaim, then the session directly started for this preparation was stopped. The intermediate normal-shutdown delay is not retained as a final reclaim failure or counted as a forced power-off test.

| Final timing report | Result |
| --- | --- |
| Task Editor first Ready | 127.085 seconds |
| Default-state validation | 23.878 seconds |
| Modified-state validation | 25.329 seconds |

Each time is a remote per-stage report, not total task time or a start SLA. It is also separate from the canonical first import of 179.423 seconds. The remote side confirmed preservation of the independent result and 4 existing snapshots. The parent's temporary transfer server was also stopped after comparing PID/start time/executable; the record is in `artifacts/remote-vivobook/transfer/server-final-cleanup.json`. **This remote test session is finished; VIVOBOOK does not need to remain powered on for a running test.** The project, Library, results, raw evidence, approved installation, and VSS settings are preserved for follow-up tests.

This test is a normal script regression for one task. It does not confer passes for ARM Windows A/B concurrency, loaded-code/MVID after reboot, failure recovery, or ordinary asset/Play/Test support.

<a id="arm64-실제-winfsp와-합성-입력"></a>
## Actual ARM64 WinFsp and synthetic input

`vivobook-report-1789996442204143400.json` is the **actual WinFsp mount + fake immutable tree** test result. A separate development harness combining deterministic in-memory input with product `JournalDeltaStore`/`WinFspOverlayFileSystem` ran as non-administrator. The Unity fixture and VSS were not used; this is not an alternative path for product snapshot workspace creation.

| Observation | Result |
| --- | --- |
| Execution | ARM64 .NET, non-administrator, 1,370ms, passed |
| Synthetic basis | Full 32,768-byte read matched; basis-tree bytes unchanged |
| Storage contract | Initial payload copy 0, read cache 0, payload created by reads 0 |
| Partial write | 4,096-byte write/flush/read matched; payload increased by 4,096 bytes |
| File operations | New test-file create/rename/delete passed; final total payload 4,131 bytes |
| Errors and reclaim | Callback errors 0; open handles/retained contexts 0 after unmount; process terminated; actual mount absent; owned-provider memory 0 |
| Preservation | Original product source/Unity fixture preserved; owned snapshots 0; VSS quota unchanged |

This is evidence for ARM64 WinFsp basic file I/O. VSS point-in-time isolation, product workspace/Git lifecycle, durability across process interruption, special files/ACL, and Unity execution were not verified in this test. NR items as a whole are not promoted to Passed.

The [WinFsp ARM64 guide](https://winfsp.dev/doc/WinFsp-on-ARM64/) explains ARM64 support, a common installation package, and use by x64 user processes on ARM64 OS. Editor-process architecture and kernel-driver architecture are distinct. This machine's composition is recorded as ARM Windows 11 + x64 Unity; it is not marked verified without execution evidence. The .NET SDK is prepared inside the project using the 10.0.112 ARM64 ZIP and SHA-512 from [Microsoft 10.0 release metadata](https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/10.0/releases.json).
