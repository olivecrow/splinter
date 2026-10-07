<a id="기존-c-회수와-mcp-응답-복구-검증"></a>
# Existing C: reclamation and MCP response-recovery verification

[한국어](../ko/verification/vss-and-mcp-remediation.md)

The [fixed scope](../plans/vss-and-mcp-remediation.md) dated 2026-10-05 closes with limits. Historical C: Plastic A/B reclamation pending resolved. Local external Coplay `10.2.0` patch confirmed asynchronous long-code acceptance/result querying/detailed tests after reload in same Editor session. External patch creation/operation removed from current offering. This record's versions/patches/settings are not current prerequisites; follow [current boundary](../unity-mcp-response-recovery.md). Existing external candidates/installed packages/servers/evidence/independent results preserved.

Raw evidence `artifacts/vss-mcp-remediation-20261005/`. Historical failures/pending remain as then-evidence; follow-up reclamation below updates only current state. No new A/B/Play-build/OS-failure matrix.

MR actual Editor/long-execution/reload qualification below belongs to v1 before authentication added. Current generated-format v2 authentication/transport testing follows [SB-02](security-boundaries.md); actual Editor connection with new tokens/live server transition NotRun. VR reclamation results retained unchanged.

<a id="vr-01-정확한-기존-c-plastic-회수"></a>
## VR-01: exact existing C: Plastic reclamation

| Item | Result/evidence |
| --- | --- |
| Fixed tasks | home `T:\pc1002`, A `c050da8fd660e350f0ef2756`, B `1ac08fe356c9603d0f13bf83`; B intent `plastic-b-discard-20261002` / request digest `51a72495ab9ea5bde42a2ab2` |
| Legacy contract | Same requests with existing `plastic-original-cli-4e262b7` executables. No arbitrary overlay/state migration to current candidate |
| Blocker investigation | Current 6 shadows in `inventory-admin.json` compared with exact `snapshot.id` in `owners-admin.json`. Initial simple ledger-string search also matched `beforeIds`, not accepted as ownership evidence. Historical `{78AD7813-DF73-4845-8513-F14CC6E87331}` already absent |
| Separate approval | User explicitly permitted removal of “3 existing blocker snapshots”/Windows recovery data below. Existing-task cleanup approval distinguished from this non-owned-data approval |
| Approved removal | Oldest first: `{15182498-4A5B-4E95-B85F-FBE2EC57AE15}` (9/28, creator unconfirmed), `{A16A6070-1AAE-445F-8561-08C86B00A115}` (9/29 Windows update/restore point 253), `{0A89C286-0E9C-43EB-87AA-4CD068E98E7E}` (10/2 Windows update/restore point 254). Exact lists/dates/volumes/older shadows checked before each deletion |
| Windows deletion boundary | First unknown shadow removed as exact CIM instance. Restore points 253/254 used dynamically loaded `SRRemoveRestorePoint` from absolute System32 DLL, both returned 0. `approved-removal.json`: success=true, each stage collateral=[] |
| A | `legacy-a-release.json`: Succeeded/terminal=true/**released** |
| B | First `--expected-plan-digest` invalid call retained as InvalidArguments/no changes. After legacy-schema check corrected `--expected-request-digest`; `legacy-b-discard-corrected.json`: Succeeded/terminal=true/**discarded** |
| Final reclamation | `final-vss-admin.json`: no A/B mounts-workspaces/delta paths/two owned snapshots; releasedResources/observations processes-mounts-handles 0. Service Running |
| Independent A result | `legacy-a-independent-proof.json`: identity `sha256:709298955c2c47c5016f63f8d08effbae11ae7f45e5fa0bcbb83e41c9cc17ffb`; all objects/content/lengths match 62 entries/245,515 logical bytes. 28-byte probe retained |
| Other-resource protection | Final only shadow `{1A464F45-487E-4105-AE41-9B41A2671B5D}`/latest DirectX restore point 255 match all initial inventory properties. Original Plastic Editor PID 77676/start `639267294741020073` retained. Installation/service/volume limit/other tasks unchanged |

Three deleted recovery points cannot be undone. Actual lists verify latest DirectX/nontarget preservation, not general harmlessness of exact-ID deletion. Distinguish [official SRRemoveRestorePoint](https://learn.microsoft.com/en-us/windows/win32/api/srrestoreptapi/nf-srrestoreptapi-srremoverestorepoint) sequence/return contract from observations. VSS-isolation guard unchanged; no future automatic deletion/sweep rights for tasks with non-owned snapshots. New Git/Plastic project bases do not use VSS, distinct from historical reclamation.

<a id="mr-01-연결-설정과-준비-실패-복구"></a>
## MR-01: connection settings and preparation-failure recovery

Only Coplay tag `v10.2.0` revision `30d22075093d1d35dfb0091c1c7550e9ad948577`. Two long-path checkout failures in other TestProjects of full upstream clone retained; only required `MCPForUnity`/`Server/src` restored from HEAD/verified. No prepare retry overwriting existing output. Candidate-02 pre-scheduler-fix files/hashes retained; only owned generated package explicitly updated. `final-proof.json`: 854 files identical in used candidate-02/clean regenerated candidate-03. No direct uv-cache/Library-PackageCache edits.

Existing 8080 console group's five PID/start identities compared/no nontarget console members confirmed before normal CTRL_C shutdown (`server-graceful-stop.json`, forcedKill=false). Patched server restarted with same 8080/dependency venv, actual PID 49092/start `639267315887175978`. Logs in evidence folder. First base-Python/PYTHONPATH test failed reading venv Windows `.pth` dependencies, corrected to proper existing-venv call.

Exact Git-original MCP identity/idle/clean checked, normal Exit requested once. Failure/null response followed actual old PID 34200 exit, no repeat. Restricted-shell restart PID 28188 showed license-client exception `0xe0434352`/Package Manager IPC-init failure; user reported closing error popup. No main window/normal Close possible confirmed, then only exact failed-startup PID/start/UPM terminated. Ordinary-user unrestricted-shell reopen PID 58696/start `639267316795898602` confirmed license/UPM init/package compilation. No administrator Editor/original Plastic Editor termination. Test asmdef-meta 33-digit GUID mistake corrected to 32 digits/error costs retained. User clicked Editor/Connect to confirm HTTP. Existing Pipeline 0.7 retained; CLI-list omission/previous 401 not inherited as success.

Changing global Codex Stdio `unity_mcp_coplay` to HTTP, initial script left existing `env` subtable. All conversation submissions then failed `invalid configuration: env is not supported for streamable_http in mcp_servers.unity_mcp_coplay`. This is this task's configuration defect, not unrelated app issue. User recovered via separate agent removing env/telemetry. Initial script is failure-reproduction evidence only, not usage procedure.

After recovery actual `codex mcp list --json`/`codex mcp get unity_mcp_coplay --json` both exit 0. Current URL `http://127.0.0.1:8080/mcp`, duplicate `unityMCP` enabled=false, no command/args/env. `final-proof.json`: other top-level settings/MCP-server entries match originals. Original config preserved privately in ignored `codex-config.before.toml`; full config/tokens absent from document. Config-query checks/actual Editor connection both added to current usage.

Already-loaded default MCP binding in conversation: `native-binding-after-repair.json` **transport=stdio / instance_count=0**, default read_console also rejected no target. Distinct from corrected HTTP discovery/actual calls. User refreshed plugins/skills but binding remained; reported `/mcp` status listing cannot reconnect. Default-tool automatic reconnection not marked successful. [Official OpenAI guidance](https://learn.chatgpt.com/docs/extend/mcp?surface=cli) app settings/MCP servers/Restart path checked by app version.

After Codex restart, default-resource-query failing transport changed to **streamable HTTP / 8080/mcp**. However earlier Git Editor 58696/Plastic Editor 77676/HTTP server 49092 all disappeared; user said Unity not manually closed. No normal-exit markers in prior logs; Codex-process cleanup influence possible, cause unestablished. VR-01 then-preservation of other Editor distinguished from subsequent unexpected exit. Guidance claiming Editor survives app restart was inaccurate.

`restore-processes-after-app-restart.ps1` checked free 8080/no Unity, then local `Win32_Process.Create` reopened same-version Git Editor/server outside Codex execution session. No product functionality/service/scheduled tasks added. New Git Editor PID 37600/start `639267352637577053`, HTTP server PID 79520. Paths in `independent-startup.json`/`server-independent-launch.json`; logs outside Library. Unity project load 15.359 seconds, initial import/license init confirmed. Further app-restart survival test for new launch route NotRun. Git-fixture changes only installed manifest/lock/documentation; Plastic Editor not automatically reopened.

After new server start, `codex mcp list --json`/`codex mcp get unity_mcp_coplay --json` again exit 0; global settings unchanged again. Direct HTTP `after-app-restart-instances-01.json` normal response/0 Editors; user asked to Connect new Editor. Default conversation resource query continued prior HTTP-startup failure, distinct from direct HTTP success.

After user clicked Connect, **Codex default HTTP MCP confirmed working**. `after-user-connect-instances-01.json`: exact `splinter-unity@983ac2308e6c044d`, new bridge session `c666afc8-2731-4a85-ba6b-84cf48e2c151`. Default `set_active_instance` fixed target; read custom-tools/project info/editor state/read_console. Default-tool `debug_request_context` also confirms fixed target/then HTTP route `integrations/unity-mcp/server.py`.

`native-binding-after-user-connect.json` preserves raw default-MCP responses. Default `execute_custom_tool(tool_name=execute_code)` accepted short read-only identity job `811f8ecc364a4856b2a80ac11aea43fa`; same-request-ID result queried. succeeded/internal success=true, exact returned project/PID 37600/start `639267352637577053` matches independently launched original. Loaded patch `splinter-mcp-remediation-v1`, candidate-02 package path, sceneDirty=false. Editor idle/ready_for_tools=true, no compile/import/Play/Test/reload, Console error query 0. Raw `content.text` JSON compared with Python integers to avoid JavaScript structuredContent rounding of 64-bit start. Six prepared patch hashes/two Codex-setting queries pass again. Same Editor identity after previous response shutdown; no further app-restart survival tests. Long execution/tests/reload/A/B not repeated/settings-scenes-assets unchanged.

<a id="mr-02-긴-코드-한-번-실행과-중복-방지"></a>
## MR-02: long-code single execution and duplicate prevention

User later toggled `unity_mcp_coplay` off/on. Read-only global settings confirm enabled=true/HTTP URL, duplicate unityMCP disabled. `native-binding-after-user-coplay-toggle.json`: then default conversation connection still stdio/0; `instances-after-user-coplay-toggle.json`: exact splinter-unity/1 at 8080 HTTP. Toggle alone not considered binding transition. Later app-restart/new-Editor connection follows MR-01.

Every call specifies `splinter-unity@983ac2308e6c044d`. `identity-result.json` checks project/PID/start/actual package path/patch version/selected-test assembly MVID/compileFailed=false/dirty=false.

First scheduler (`EditorApplication.delayCall`) long request acknowledged 0.122 seconds but remained queued. Depended on unfocused Inspector updates; corrected to one `EditorApplication.update`. After compilation reload, same existing job `71f33a22bb324f708c582fd61c25aff6` queried **interrupted**/no automatic execution, then explicitly new request ID reran. First attempt excluded from success counts.

| Check | Actual result |
| --- | --- |
| Long-run acceptance | request `mcp-remediation-1005-long-fixed`, job `13fd5c53fc254cad921b821457dc214a`, ack **0.0385 seconds** |
| Single execution | Code Thread.Sleep 35,014ms, total history 36,433.7ms. Returned project/PID/start match original above |
| Result query | `long-fixed-result.json`: succeeded/internal success=true. Queries after acceptance read-only; request waiting on main-thread execution completion took 15.599 seconds |
| Same input | `long-duplicate-key.json`: same job ID/start-completion times/full result. Exactly one long-code execution history entry |
| Different input | `long-conflict-rejected.json`: CodeJobRequestConflict/no code execution. Error/code retained |
| Reload preservation | `long-after-reload.json`: identical full job. Interrupted jobs not automatically rerun |

<a id="mr-03-상세-테스트-결과와-최종-정리"></a>
## MR-03: detailed test results and final cleanup

Only owned temporary `Assets/SplinterMcpRemediationQualification` created with fixed GUIDs/hashes. All **3 tests passed** in `RemediationTests.cs`: immutable detailed-result roundtrip/invalid-request rejection/no execution for missing jobs. Assembly-filter selection, not whole-game tests.

Job `7885946d44d44be4b310fbd9721f3aed` summary/3 individual results stored; explicitly requested `EditorUtility.RequestScriptReload` once. New HTTP session `66782b40-e569-40c9-a0a0-cc611d7401d7` rediscovered, same Editor PID/start. **Entire job identical** in `selected-tests-before-reload.json`/`selected-tests-after-reload.json`; detailed-result canonical SHA-256 `2d34cfe2c7a586a948a9a05d043abf2724124e61887c5bcfdfd59d4ccb9e1aea`.

`reload-and-clean-proof.json`: 7 test files' current hashes/approved paths checked/removed. After cleanup compile/reload, details reread in `selected-tests-after-cleanup.json`. Final Editor idle, no compile/import/reload/Play/Test, original scene clean. Temporary test assembly removed; patched Coplay remains connected in original Git Editor. `final-proof.json` comparison of 1,602 original tracked files: only installed Packages manifest/lock changed. Later fixture-document current-connection guidance reviewed separately as docs change.

<a id="자동-검사와-판정"></a>
## Automated checks and assessment

- Coplay server argument/target/error/no-retry boundaries: **7 Passed**, `server-tests.txt`.
- Selected Unity EditMode: **3 Passed**, summary/items roundtrip/actual reload results identical.
- Subsequent Codex default HTTP MCP: exact original target/project/PID/start/current loaded patch/read-only code result/idle/Console errors 0 confirmed. Historical Stdio 0/HTTP-startup failures retain then-evidence.
- `pwsh scripts/build.ps1`: **978 Passed / 0 Failed / 1 Skipped**, `product-build.log`/original TRX.
- Six preparation-file hashes/normal generated server-wrapper `--help`/Python syntax pass. Document UTF-8: 115 product/6 fixture; both repositories `git diff --check` passed.

Fixed VR/MR scope **scoped-complete-with-limitations**. Detailed completed-test preservation, actual single long-code execution, Codex default HTTP connection confirmed, not full new parallel-A/B patch qualification. Job recovery after Editor restart/OS failure/arbitrary code cancellation-rollback/huge detailed-result costs/all native MCP-client automatic-schema refresh/other versions-PCs NotRun. Exact cause of unexpected prior Editor/server exits unconfirmed; further app-restart survival test after independent launch NotRun. User excluded prior-exit cause investigation from follow-up scope, so not counted unfinished/investigated further. Shared VSS total costs/workspace attribution/physical peak unknown; existing large/ARM/OS limits remain. No Coplay upstream official release/merge claim.
