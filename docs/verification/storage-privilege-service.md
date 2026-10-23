<a id="반복-uac-제거-검증"></a>
# Repeated-UAC elimination verification

[한국어](../ko/verification/storage-privilege-service.md)

**Elimination of normal repeated UAC prompts was confirmed for registered Windows x64 project/home paths.** On 2026-09-29, after installing the restricted service, ordinary-user CLI ran actual NTFS/Git A/B creation, reconnect to the same snapshot, independent-result preservation, and reclamation. The UA-01–04 scopes below are Passed; this does not complete whole-product installation, general Unity, ARM, or NR failure support.

<a id="판정과-실행-근거"></a>
## Assessment and execution evidence

Raw evidence is in `artifacts/storage-authority/ua-20260928/`. The [combined result](../../artifacts/storage-authority/ua-20260928/qualification-20260929.json) connects each workspace's absent snapshot/mount/delta, 0 owned processes/memory/handles, external-snapshot preservation, CLI permissions, and service state.

| Unit | Checked scope | Actual evidence |
| --- | --- | --- |
| UA-01 installation/lifetime | Install LocalSystem auto-start service at protected paths; preserve active A/B during same-package update; reject removal with active snapshots, then restart service | `bootstrap-result-02.json`, `management-update-active-01-02.json`, `management-remove-active-01-02.json`, `status-final.json` |
| UA-02 request boundaries | Fixed SCM path/account/PID match pipe peer; reject version/arbitrary operation/unknown fields/unregistered home/another task's snapshot/early reclamation; deny ordinary users executable/state writes and creation of same pipe server | 6 `pipe-boundaries.json` cases, `installed-protection.json`; directory replacement/different SID/JSON boundaries in automated tests below |
| UA-03 repeated use | Ordinary-permission A/B creation; reconnect same key/ID/snapshot after normal A-provider stop; preserve separate edits; keep A as blocker when reclaiming B first; successful explicit B retry after A reclamation | `ensure-a-02.json`, `ensure-b-01.json`, `reconnect-a-01.json`, `preserve-*-01.json`, `release-b-first.json`, `release-a-01.json`, `release-b-retry.json` |
| UA-04 interruption/recovery | Return `StorageServiceUnavailable` when service stopped; resume same task without automatic elevation; disconnect without reading Observe response body, then restart service/retransmit same request; reject invalid version | `reconnect-service-stopped.json`, `lost-observe-drop.json`, `management-restart-01-02.json`, `lost-observe-replay-02.json`, `response-replay-proof.json` |

**All 13 ordinary CLI executions had `callerElevated=false`**. During execution, 50ms `consent.exe` samples found 0 instances; normal provider code has no automatic elevation/helper fallback. Initial installation and approved service-management tests ran in a separate administrator session. That session ended with `finish` in `management-ended-02.json`; final service is discovered as `running` by ordinary CLI. Installed SYSTEM service, rather than this test session, handled normal VSS requests.

Response-loss retransmission SHA-256 `0999868d784ec1500393dc060754728d6be9287f51854378585ebfb40fbbec8d` matches the protected completed-response file before restart. Existing response was reused despite changed service PID. Initial probe retransmission produced `StorageRequestConflict` because PowerShell automatic date conversion changed the input offset. Identical input preserving the original string passed; evidence of the initial invalid request remains.

<a id="결과와-보존"></a>
## Results and preservation

| Task | Workspace ID | Independent Git result | Final state |
| --- | --- | --- | --- |
| A | `ade04e0b2900b1ca585a91b4` | `754f024901f6b283749ee84fb7ab638e5908ada8` | released; owned snapshot/mount/delta/processes/memory/handles 0 |
| B | `21b16811c316fdfe5008655a` | `c0813e817db67b4851502c2186c0934d82a5f72a` | released by explicit retry after reclaim-pending; same resources 0 |

After reclamation, `independent-result-a` and `independent-result-b` were actually read from `refs/splinter/snapshot-results/<id>`. Original HEAD `4a8c6b3150a2c8ef4d18567ad47e1649d74c378a` and clean checkout remained; Git worktree registrations returned to the original only. Existing C: snapshot IDs `{78AD7813-DF73-4845-8513-F14CC6E87331}`, `{15182498-4A5B-4E95-B85F-FBE2EC57AE15}` were present before/after.

No Unity Editor started, so `workspace checkpoint`'s `ValidationRequired` remained. Edits were preserved with existing product `validation preserve-worker`, with `validationClaimed=false`. Unity compilation success was not fabricated to bypass reclamation conditions.

<a id="재현-범위와-남은-한계"></a>
## Reproduction scope and remaining limits

- Installation package: `package-03`, manifest SHA-256 `98195261bdd01cfcb6fe940a52f09301bbbfba62f15078c4b51d5515d646fff8`. Installer SHA-256 `806e3de182a9b84b8c884fdc1d6da21e04d4ae83e53642bd5f4a3dc32c1433c3`. Baseline is an uncommitted candidate after `development @ e75b4a5`; product code was unchanged during execution.
- Only new 16GiB dynamic VHDX `ua-isolated.vhdx` was formatted as T: after confirming its exact new RAW disk identity. A seed snapshot was created only on that volume to expand its diff area to 10GiB, then that seed ID reclaimed. Existing C: settings/other snapshots were unchanged. T:/VHDX remain to preserve fixture/independent Git results, separately from workspace mount/delta reclamation.
- Installation grants cover exact identities `T:\ua-source` + this evidence directory's `home`, sibling `splinter-unity` + `unity-home`. Actual A/B ran only with the first grant. No code/Editor work occurred in the sibling repository. New project/home registration and service update/removal are administrator configuration operations, outside ordinary repeated workspace commands.
- Git rejected ownership of the test source directory created by administrator installation. Exact `safe.directory=T:/ua-source` in separate `git-test-settings/git/config` was applied only through test-process `XDG_CONFIG_HOME`. User-global Git settings/product GIT environment-isolation code were unchanged.
- Actual service stop/restart/completed-response loss ran within the scope above. Forced termination during native VSS capture did not run; interruption/ambiguous-capture preservation is automated-test evidence. Reboot/power/physical-space failures and general-file/Unity/ARM A/B retain existing NR/WF support boundaries.
- The earlier initial UAC request was canceled; the user reported a black screen on the directly connected monitor. After the user returned and explicitly approved installation/tests, retry success was confirmed by administrator process/installation results. UAC/secure-desktop/graphics settings were unchanged; the earlier display issue's root cause remains unconfirmed.

<a id="자동-검증"></a>
## Automated verification

Locked restore, Release build, and **568 passed, 0 failed, 1 skipped test** through `pwsh ./scripts/build.ps1` are evidence for the current product candidate. They include 31 `StorageAuthorityTests`/`StorageAuthorityPipeTests` and 9 `StorageServiceSetupTests`. Tests cover fake capture interruption/response loss/same-request recovery, complete pending-publication resumption/corrupt-state preservation, actual Windows file-replacement blocking/ordinary file writes/temporary pipe SID/ACL, and rejection of read-only installation inputs.

Final build log is `build-setup-preflight.log`; full results are `artifacts/test-results/Splinter.Tests_net10.0_x64.trx`. Earlier `build-final.log`, `authority-final.trx`, and `setup-preflight.trx` remain. Existing file-replacement race test hit its 30-second limit once midway, then passed a separate rerun and final full run without test/deadline changes. This does not establish that its cause was resolved.

Current service contract follows the [feature document](../storage-privilege-service.md); completed scope follows the [UA plan](../plans/storage-privilege-service.md).

<a id="ua-05-idle-path-handles"></a>
## UA-05: idle path-handle lifetime, 2026-10-23

Scope: replace service-lifetime installation/state/PowerShell ancestor pins with startup/request scopes while retaining the original deny-write/delete sharing, reparse/type checks and caller-token project/home authorization. The service retains identities of every ancestor and configuration values, reacquires and revalidates them and protected-directory ACLs for each request, then closes all path/evidence pins before the response. Startup failures, failed admission, operation errors, cancellation and bounded service-error logging also dispose their pins. Protocol, grants, protected receipt formats and snapshot reclamation conditions are unchanged.

The 11 `StorageAuthorityLifetimeTests` cases exercise the actual pipe server with native Windows handles and a private directory fixture, synthetic protected-ACL decisions and fake VSS side effects. They cover idle/startup, incomplete input, successful response and replay, rejected requests, failed/cancelled operations and service cancellation. During an operation, ancestor writes, directory renames and junction conversion remain blocked. Between operations, the fixture ancestor opens with the CBS-equivalent `0x12019F` access and share mode 7 using `OPEN_EXISTING`, with no file mutation. Idle ancestor replacement with unchanged leaf identities, directory/PowerShell/configuration-file replacement, in-place configuration changes, changed ACLs and reparse substitution are rejected. The private ancestor avoids an unrelated process's C: handle masking a regression; actual C: evidence is separate below.

On the installed old service, approved normal stop of exact LocalSystem PID **4968** changed the C: directory access result from **32 (`ERROR_SHARING_VIOLATION`) to 0 (success)**. No child process or unfinished `active.json` request existed at the check. All **1,721 protected ledger files** retained their paths/content hashes; no snapshot/resource deletion, Windows Update installation, DISM/SFC or reboot was executed. This establishes that stopping this service removed the observed root-access conflict, not that Windows Update or component-store repair succeeded. Evidence: `artifacts/storage-authority-idle-20261023/stop-result.json` and its fixed stop script.

The final `pwsh ./scripts/build.ps1` locked restore/Release build passed with **1,702 tests passed, 0 failed, 24 skipped (1,726 total)**, including all 11 new cases. Evidence: `required-build-final.log` and `authority-lifetime-final.trx` in the same directory. The initial run exposed test-only JSON exception-subtype and normal-loop-exit expectations; those assertions were corrected before this full passing run.

The new 16-file service package is prepared under the same evidence directory (`package/`), manifest SHA-256 `b7753ca6040a2c603867b73d636b3339b1fa2db095c1b7ddc7c35d4a27c7303e`. Read-only standard Update preflight rejects the retained grants because T: is not connected (`update-plan.log`). T: was the UA fixture's 16GiB VHDX, subsequently used by other recorded Splinter fixtures; `Get-DiskImage` confirms `Attached=false`. No grant was removed/rewritten, no volume reattached, and no new installation was applied.

The user then requested complete occupation cleanup. After an initially cancelled UAC and explicit retry approval, administrator inspection found **102 owner records, all released; 0 active requests; 0 unreclaimed owners; 0 live owned snapshots; 0 Splinter runtime processes or scheduled tasks**. Standard `setup-storage-service.ps1 -Action Remove -Apply` removed the old service registration, and SCM returned **1060 (service absent)**. The 1,721 ledger files and all three existing non-owned C: snapshots were unchanged. After the test processes exited, native directory access succeeded for **C:, D:, E:, Program Files, ProgramData, the service install/state directories and PowerShell 7** (eight paths, error 0). Evidence: `cleanup-result.json`, `service-remove.log` and `cleanup-summary.json`.

At that cleanup, the non-archive `.local` inventory contained **91 terminal workspace records (88 released, 3 discarded)** and **38 restored project/Library bases**. The two inspected original projects and their Library paths were ordinary directories without reparse points. Results, fixture VHDX, historical receipts and inert installed files remained preserved; the shared WinFsp installation was not removed. No live Splinter occupation was observed within that inventory. The subsequent explicitly authorized installation below supersedes the service-absent state; historical UA-01–04 evidence is not inherited as validation of this candidate.

### Installed-service follow-up, 2026-10-23

The user then authorized installing the fix, testing the actual service and committing after success. After one cancelled UAC and an approved retry, the exact existing VHDX/NTFS volume was temporarily reattached as T:. Standard Install preflight/Apply retained all **seven grants** and historical protected owner files, and all **16 installed file hashes** matched the package above. No new project/home scope was added. The ordinary CLI reported `callerElevated=false`; service PID **12548** ran as LocalSystem/Automatic.

One small editorless task used existing `T:\plain-apply-20261002` / `.local\ap1002`, `source=none`, `application=none`, `ntfs-snapshot`, 16MiB journal budget and key `ua05-installed-idle-20261023`. Workspace **`d6b31c017bd9062ef57223de`**, request digest `e2df5a04116b61cb16937948`, captured snapshot **`{7CD684FE-7FF2-4C29-8388-B86C5DFB9A05}`**. A 58-byte file written only to the returned workspace survived `checkpoint --release` as independent result `sha256:98580480db6a631f67260abf5e187cd17256590203b83a3e2b9d110537caa265`. The original three files/29 bytes retained their hashes. Final task state is `released`, with mount/delta/provider/snapshot absent and owned open handles 0.

The first checkpoint/release preserved the result and stopped the provider, but current strict state validation rejected retired fields in two historical completed peer records (`StateCorrupt`). Their protected released receipts, absent live snapshots/mounts/deltas and independent payload hashes were verified before moving the exact records, unchanged, to `.local/ap1002/archive/ua05-preexisting-records/<id>`. A `42fdead74de65116b2669858` retained 33 files and B `78b313a0f90b0261a72742e9` retained 31 files, with every relative path/content hash equal. The same new task ID/home/checkpoint request then completed reclamation. No legacy fields were migrated or ignored by product code, and no release guard was relaxed. The initial failure and archive manifest remain in the evidence directory.

| Installed-service observation | Paths × samples | Result |
| --- | --- | --- |
| Initial service idle | 9 × 3 | All error 0 |
| After real Capture/Observe, workspace provider alive | 9 × 3 | All error 0 |
| After checkpoint/release, service still running | 9 × 3 | All error 0 |
| After explicit service restart, PID **23076** | 9 × 3 | All error 0 |
| Final idle after T: detach | 8 × 3 | All error 0 |

All **132** native probes used access `0x12019F`, share 7 and `OPEN_EXISTING`, opening/closing only. Paths were C:, D:, E:, Program Files, ProgramData, service install/state, PowerShell 7 and T: while attached. Final service remains **Running/Automatic/LocalSystem**; the exact test VHDX is detached again. Historical protected owner files and the four C: snapshots present at this installation's start were unchanged. Only the new task's T: snapshot was reclaimed. No Splinter provider/workflow process remained; the service itself continued running.

Evidence under `artifacts/storage-authority-idle-20261023/`: `install-plan.json`, `install-apply.json`, `installed-verification.json`, the five `installed-*.json` samples, `native-ensure.json`, `native-checkpoint-release.json`, `native-checkpoint-release-retry.json`, `workspace-finished.json`, `archived-legacy-records.json` and `installed-summary.json`. This qualifies actual idle/normal task completion/service-restart behavior for this small Windows x64 NTFS fixture. OS reboot, forced interruption of native VSS, Windows Update retry and DISM/SFC remain **NotRun**.
