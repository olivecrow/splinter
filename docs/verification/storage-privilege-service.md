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
