# 기존 C: 회수와 MCP 응답 복구 검증

2026-10-05의 [고정 범위](../plans/vss-and-mcp-remediation.md)를 한정 마감한다. 과거 C: Plastic A/B의 회수 보류는 해소됐다. 외부 Coplay `10.2.0`의 로컬 패치에서 긴 코드의 비동기 접수/결과 조회와 같은 Editor 세션의 reload 후 상세 테스트 결과를 확인했다. [현재 사용법](../unity-mcp-response-recovery.md)은 Splinter의 제품 Unity API와 구분한다.

원시 근거는 `artifacts/vss-mcp-remediation-20261005/`다. 예전 실패·pending은 당시 증거로 보존하며 아래 후속 회수로 현재 상태만 갱신한다. 새로운 A/B·Play/build·OS 장애 행렬을 실행하지 않았다.

## VR-01: 정확한 기존 C: Plastic 회수

| 항목 | 결과와 근거 |
| --- | --- |
| 고정 작업 | home `T:\pc1002`, A `c050da8fd660e350f0ef2756`, B `1ac08fe356c9603d0f13bf83`. B intent `plastic-b-discard-20261002` / request digest `51a72495ab9ea5bde42a2ab2` |
| 구형 계약 | 기존 `plastic-original-cli-4e262b7`의 실행 파일로 동일 요청을 수행했다. overlay/상태를 현재 후보로 임의 이관하지 않았다 |
| 차단 조사 | `inventory-admin.json`의 현재 6개 shadow와 `owners-admin.json`의 정확한 `snapshot.id`를 대조했다. 최초 inventory의 단순 문자열 ledger 검색은 `beforeIds`도 잡으므로 소유 증거로 채택하지 않았다. 과거 `{78AD7813-DF73-4845-8513-F14CC6E87331}`은 이미 없었다 |
| 별도 승인 | 사용자가 “기존 차단 snapshot 3개 제거 허용”으로 아래 Windows 복구 데이터 제거를 승인했다. 기존 작업 정리 승인과 이 비소유 데이터 승인을 구분했다 |
| 승인 삭제 | 오래된 순서로 `{15182498-4A5B-4E95-B85F-FBE2EC57AE15}`(9/28, 생성 주체 미확정), `{A16A6070-1AAE-445F-8561-08C86B00A115}`(9/29 Windows 업데이트/복원 지점 253), `{0A89C286-0E9C-43EB-87AA-4CD068E98E7E}`(10/2 Windows 업데이트/복원 지점 254). 각 삭제 전 정확한 목록/date/volume와 더 오래된 shadow를 확인했다 |
| Windows 삭제 경계 | 첫 미확정 shadow는 정확한 CIM 인스턴스를 제거했다. 복원 지점 253/254는 절대 System32 DLL에서 동적으로 읽은 `SRRemoveRestorePoint`를 사용했고 둘 다 반환 0. `approved-removal.json`: success=true, 매 단계 collateral=[] |
| A | `legacy-a-release.json`: Succeeded/terminal=true/**released** |
| B | 잘못된 옵션 `--expected-plan-digest`의 첫 호출은 InvalidArguments/변경 없음으로 보존했다. 구형 schema를 확인해 `--expected-request-digest`로 고친 `legacy-b-discard-corrected.json`: Succeeded/terminal=true/**discarded** |
| 최종 회수 | `final-vss-admin.json`: A/B mount/workspace와 delta 경로 없음, owned snapshot 2개 없음, releasedResources/observation은 프로세스·mount·핸들 0. 서비스는 Running |
| 독립 A 결과 | `legacy-a-independent-proof.json`: result identity `sha256:709298955c2c47c5016f63f8d08effbae11ae7f45e5fa0bcbb83e41c9cc17ffb`, 62개/245,515 logical bytes의 모든 object 내용/길이 일치. probe 28 bytes도 보존 |
| 다른 자원 보호 | 최종 유일한 shadow `{1A464F45-487E-4105-AE41-9B41A2671B5D}`와 최신 DirectX 복원 지점 255가 최초 inventory의 전체 속성과 일치. 원본 Plastic Editor PID 77676/start `639267294741020073` 유지. 설치/서비스/볼륨 한도·타 작업 변경 없음 |

삭제된 세 복구 시점은 되돌릴 수 없다. 최신 DirectX와 대상 외 자원 보존을 실제 목록으로 확인했으며 정확한 ID 삭제가 항상 무해하다는 일반 보장은 만들지 않았다. [공식 SRRemoveRestorePoint](https://learn.microsoft.com/en-us/windows/win32/api/srrestoreptapi/nf-srrestoreptapi-srremoverestorepoint)의 지정 sequence/반환 계약과 실제 관측을 구분한다. VSS 격리 guard는 그대로이며 향후 비소유 snapshot이 있는 작업에 자동 삭제·sweep 권한을 부여하지 않았다. 새 Git/Plastic 프로젝트 기준본은 VSS를 사용하지 않으므로 이 과거 회수와 구분한다.

## MR-01: 연결 설정과 준비 실패 복구

Coplay tag `v10.2.0` revision `30d22075093d1d35dfb0091c1c7550e9ad948577`만 사용했다. 전체 upstream clone의 다른 TestProjects 장경로 2개 checkout 실패를 보존하고, 필요한 `MCPForUnity`/`Server/src`만 HEAD에서 복원해 검증했다. 기존 output을 덮어쓰는 prepare 재시도는 하지 않았다. candidate-02의 scheduler 수정 전 파일/hash를 보존한 뒤 소유 생성 package만 명시적으로 갱신했다. `final-proof.json`: 사용 중 candidate-02와 깨끗하게 재생성한 candidate-03의 854개 파일이 동일하다. uv cache·Library/PackageCache를 직접 수정하지 않았다.

기존 8080의 console process 5개 PID/start를 대조하고 대상 외 console 구성원이 없음을 확인한 CTRL_C로 정상 종료했다(`server-graceful-stop.json`, forcedKill=false). 동일 8080/의존성 venv의 패치 server를 재시작했고 실제 PID 49092/start `639267315887175978`를 기록했다. 로그는 이 evidence 폴더에 둔다. 첫 base Python + PYTHONPATH 시험은 venv의 Windows `.pth` 의존성을 읽지 못해 실패했고 올바른 기존 venv 호출로 고쳤다.

Git 원본 Editor의 정확한 MCP identity/idle/clean을 확인하고 정상 Exit를 한 번 요청했다. 실패/null 응답 뒤 실제 이전 PID 34200의 종료를 확인했으며 요청을 반복하지 않았다. 제한 셸에서 재실행한 PID 28188은 라이선스 클라이언트 예외 `0xe0434352`와 Package Manager IPC 초기화 실패를 보였고, 사용자도 오류 팝업을 닫았다고 알렸다. main window가 없어 정상 Close 요청이 불가능함을 확인한 뒤 이 실패 startup PID/start와 정확한 UPM만 종료했다. 일반 사용자 비제한 셸에서 PID 58696/start `639267316795898602`로 다시 열어 라이선스/UPM 초기화와 패키지 컴파일을 확인했다. 관리자 Editor를 열거나 기존 Plastic Editor를 종료하지 않았다. 시험용 asmdef meta의 33자리 GUID 실수도 32자리로 바로잡고 오류 비용을 보존했다. 사용자가 Editor를 클릭하고 Connect를 눌러 HTTP 연결을 확인했다. 기존 Pipeline 0.7은 유지하며 CLI 목록 누락/선행 401을 성공으로 승계하지 않았다.

전역 Codex의 Stdio `unity_mcp_coplay`를 HTTP로 바꾸면서 초기 편집 스크립트가 기존 `env` 하위 표를 남겼다. 그 결과 모든 대화 제출이 `invalid configuration: env is not supported for streamable_http in mcp_servers.unity_mcp_coplay`로 실패했다. 이는 이 작업의 설정 변경 결함이며 무관한 앱 문제로 분류하지 않는다. 사용자가 별도 에이전트로 env/텔레메트리 항목을 제거해 복구했다. 해당 최초 스크립트는 실패 재현 근거일 뿐 사용 절차가 아니다.

복구 후 이 작업에서도 실제 `codex mcp list --json`와 `codex mcp get unity_mcp_coplay --json`이 모두 exit 0임을 확인했다. 현재 URL은 `http://127.0.0.1:8080/mcp`, 중복 `unityMCP`는 enabled=false이며 command/args/env가 없다. `final-proof.json`은 다른 top-level 설정과 다른 MCP 서버 항목이 원래와 일치함을 기록한다. 원래 설정은 비공개 ignored evidence의 `codex-config.before.toml`로 보존했다. 원문 설정과 토큰을 문서에 싣지 않는다. 설정 조회 검사와 실제 Editor 연결을 모두 현재 사용 절차에 추가했다.

대화에 기존에 로드된 기본 MCP binding은 `native-binding-after-repair.json`에서 **transport=stdio / instance_count=0**이며 기본 read_console도 같은 대상 없음으로 거부됐다. 수정된 HTTP 서버의 discovery/실제 호출 성공과 구분한다. 사용자가 플러그인·스킬 새로고침을 했지만 당시 binding은 유지됐고, `/mcp`의 상태 목록으로 재연결을 수행할 수 없다고 알렸다. 기본 tool의 자동 재연결을 성공했다고 표시하지 않는다. [OpenAI 공식 안내](https://learn.chatgpt.com/docs/extend/mcp?surface=cli)의 앱 설정/MCP servers/Restart 경로는 앱 버전에 따라 확인한다.

Codex 재실행 이후 기본 resource 조회의 실패 transport는 **streamable HTTP / 8080/mcp**로 바뀌었다. 그러나 앞서 실행한 Git Editor 58696, Plastic Editor 77676와 HTTP server 49092가 모두 사라졌고 사용자는 Unity를 직접 끄지 않았다고 알렸다. 종료 전 로그에 정상 종료 표식이 없으며 Codex 실행 프로세스 정리의 영향은 가능성일 뿐 원인이 확정되지 않았다. VR-01의 당시 다른 Editor 보존 관측과 이후 예기치 않은 종료를 구분한다. 앱 재실행에도 Editor가 유지된다고 안내한 것은 부정확했다.

`restore-processes-after-app-restart.ps1`은 8080 미점유와 Unity 부재를 확인한 뒤 로컬 `Win32_Process.Create`로 같은 버전의 Git Editor와 서버를 Codex 실행 세션에서 분리해 다시 열었다. 제품 기능·서비스·예약 작업을 추가하지 않았다. 새 Git Editor는 PID 37600/start `639267352637577053`, HTTP server는 PID 79520이다. `independent-startup.json`과 `server-independent-launch.json`에 경로를 기록했고 로그는 Library 밖에 유지했다. Unity의 프로젝트 로드는 15.359초였으며 초기 import 완료와 라이선스 초기화를 확인했다. 이 새 실행 경로의 추가 앱 재시작 생존 시험은 NotRun이다. Git fixture 변경은 앞서 설치한 manifest/lock과 문서뿐이며 Plastic Editor를 자동으로 다시 열지 않았다.

새 서버 시작 뒤 `codex mcp list --json`와 `codex mcp get unity_mcp_coplay --json`은 다시 exit 0이었다. 전역 설정을 다시 수정하지 않았다. `after-app-restart-instances-01.json`의 직접 HTTP 조회는 정상 응답/Editor 0이었으며 사용자에게 새 Editor에서 Connect를 요청했다. 당시 대화의 기본 MCP resource 조회는 이전 HTTP startup 실패를 계속 반환했으므로 직접 HTTP 성공과 구분했다.

사용자가 Connect를 누른 뒤 **Codex 기본 HTTP MCP도 정상 확인했다**. `after-user-connect-instances-01.json`은 정확한 `splinter-unity@983ac2308e6c044d`와 새 bridge session `c666afc8-2731-4a85-ba6b-84cf48e2c151`을 기록한다. 기본 `set_active_instance`로 해당 대상을 고정한 뒤 custom-tools/project info/editor state와 read_console을 읽었다. 기본 도구의 `debug_request_context`에서도 고정된 대상과 현재 `integrations/unity-mcp/server.py`의 HTTP 실행 경로를 확인했다.

`native-binding-after-user-connect.json`은 기본 MCP의 원시 응답을 보존한다. 기본 `execute_custom_tool(tool_name=execute_code)`로 짧은 read-only identity job `811f8ecc364a4856b2a80ac11aea43fa`를 접수하고 같은 request ID로 결과를 조회했다. succeeded/내부 success=true이며 반환 project와 PID 37600/start `639267352637577053`가 독립 실행한 원본과 정확히 일치한다. 로드된 patch는 `splinter-mcp-remediation-v1`, package 경로는 candidate-02이고 sceneDirty=false다. Editor는 idle/ready_for_tools=true이며 compile/import/Play/Test/reload 중이 아니고 Console error 조회는 0개였다. 64-bit 시작 시각은 JavaScript structuredContent의 반올림을 피하도록 원시 `content.text` JSON을 Python 정수로 대조했다. 준비한 패치 6개 hash와 Codex 설정 조회 두 개도 다시 통과했다. 앞선 응답 종료 뒤에도 동일 Editor identity가 유지됐으며 추가 앱 재시작 생존 시험은 실행하지 않았다. 긴 실행/테스트/reload/A/B를 재실행하거나 설정·씬·에셋을 수정하지 않았다.

## MR-02: 긴 코드 한 번 실행과 중복 방지

사용자가 이후 `unity_mcp_coplay`를 껐다 켰다. 전역 항목은 enabled=true/HTTP URL이고 중복 unityMCP는 disabled임을 읽기 전용으로 확인했다. `native-binding-after-user-coplay-toggle.json`은 당시 대화의 기본 연결이 여전히 stdio/0임을, `instances-after-user-coplay-toggle.json`은 8080 HTTP의 정확한 splinter-unity/1 연결을 기록한다. 토글만으로 당시 binding이 전환됐다고 판정하지 않았다. 이후 앱 재실행 관측과 새 Editor의 연결 상태는 MR-01을 따른다.

각 호출에 `splinter-unity@983ac2308e6c044d`를 지정했다. `identity-result.json`은 project/PID/start·실제 package path·패치 버전·선택 test assembly MVID·compileFailed=false/dirty=false를 확인한다.

첫 scheduler(`EditorApplication.delayCall`)의 긴 요청은 0.122초 접수 후 queued 상태에서 진행되지 않았다. unfocused Editor의 Inspector 갱신에 의존하므로 다음 `EditorApplication.update` 한 번으로 수정했다. 컴파일 reload 뒤 같은 기존 job `71f33a22bb324f708c582fd61c25aff6`이 **interrupted**임을 조회하고 자동 실행이 없음을 확인한 뒤 명시적인 새 request ID로 재실행했다. 첫 시도를 성공 횟수에 넣지 않는다.

| 검증 | 실제 결과 |
| --- | --- |
| 긴 실행 접수 | request `mcp-remediation-1005-long-fixed`, job `13fd5c53fc254cad921b821457dc214a`, ack **0.0385초** |
| 한 번 실행 | 코드의 Thread.Sleep 35,014ms, 전체 history 36,433.7ms. 반환 project/PID/start는 위 원본 Editor와 일치 |
| 결과 조회 | `long-fixed-result.json`: succeeded / 내부 success=true. 접수 후 조회는 read-only이며 main thread 실행 완료를 기다린 요청이 15.599초 걸렸다 |
| 동일 input | `long-duplicate-key.json`: 같은 job ID·시작/완료 시각·결과 전체 일치. 실행 history의 긴 코드는 정확히 1개 |
| 다른 input | `long-conflict-rejected.json`: CodeJobRequestConflict, 코드 실행 없음. error/code가 사라지지 않음 |
| reload 보존 | `long-after-reload.json`: 같은 job 전체 일치. interrupted job은 자동 재실행하지 않음 |

## MR-03: 상세 테스트 결과와 최종 정리

작업 소유의 임시 `Assets/SplinterMcpRemediationQualification`만 고정 GUID/hash로 생성했다. `RemediationTests.cs`의 immutable 상세 결과 roundtrip·잘못된 request 거부·없는 job 비실행 **3개가 모두 통과**했다. assembly filter로 선택했으며 게임 전체 테스트를 실행하지 않았다.

job `7885946d44d44be4b310fbd9721f3aed`의 summary와 개별 3개 결과를 저장하고 `EditorUtility.RequestScriptReload`를 한 번 명시적으로 요청했다. 새 HTTP session `66782b40-e569-40c9-a0a0-cc611d7401d7`을 재발견했으며 Editor PID/start는 같았다. `selected-tests-before-reload.json`과 `selected-tests-after-reload.json`의 **job 전체가 동일**하고 상세 result canonical SHA-256은 `2d34cfe2c7a586a948a9a05d043abf2724124e61887c5bcfdfd59d4ccb9e1aea`다.

`reload-and-clean-proof.json`: 시험용 7개 파일의 현재 hash와 승인 경로를 확인하고 제거했다. 정리 후 compile/reload에서도 `selected-tests-after-cleanup.json`의 상세 결과를 다시 읽었다. 최종 Editor는 idle, compile/import/reload/Play/Test 중이 아니고 원본 scene은 clean이다. 임시 test assembly는 제거됐으며 패치된 Coplay는 계속 연결된 원본 Git Editor에 남는다. `final-proof.json`의 원래 추적 파일 1,602개 대조에서 변경은 설치한 Packages manifest/lock 2개뿐이다. 이후 fixture 문서의 현재 연결 안내 추가는 별도 문서 수정으로 검토한다.

## 자동 검사와 판정

- Coplay server argument/target/error/no-retry 경계: **7 Passed**, `server-tests.txt`.
- 선택 Unity EditMode: **3 Passed**, summary/items roundtrip와 actual reload 결과 동일.
- 후속 Codex 기본 HTTP MCP: 정확한 원본 target·project/PID/start·현재 로드된 패치·read-only code 결과·idle/Console error 0 확인. 과거 Stdio 0/HTTP startup 실패는 당시 근거로 보존한다.
- `pwsh scripts/build.ps1`: **978 Passed / 0 Failed / 1 optional ReFS Skipped**, `product-build.log`와 원래 TRX.
- 준비 파일 6개 hash 검증·정상 생성 server wrapper `--help`·Python 구문이 통과했다. 문서 UTF-8은 제품 115개/fixture 6개, 두 저장소 `git diff --check`도 통과했다.

VR/MR의 고정 범위는 **scoped-complete-with-limitations**다. 완료한 테스트의 상세 보존과 실제 긴 code 요청의 한 번 실행 및 Codex 기본 HTTP 연결을 확인했으며 새로운 병렬 A/B 전부의 패치 자격으로 승계하지 않는다. Editor 재시작 후 job 복구/OS 장애·임의 코드 취소/롤백·거대한 상세 테스트 결과 비용·모든 native MCP client의 자동 schema 갱신·다른 버전/PC는 NotRun이다. 예기치 않은 이전 Editor/서버 종료의 정확한 원인은 미확정이며 독립 실행 뒤의 추가 앱 재시작 생존 시험도 NotRun이다. 사용자가 이전 종료의 원인 조사를 후속 범위에서 제외했으므로 이 기록을 미완료 작업으로 집계하거나 추가 조사하지 않는다. 공유 VSS 전체 비용과 workspace 귀속/물리 peak unknown, 기존 대형/ARM/OS 지원 제한은 그대로다. Coplay upstream 공식 배포/병합을 완료했다고 주장하지 않는다.
