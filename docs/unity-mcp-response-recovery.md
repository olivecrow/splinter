# Unity MCP 연결 복구와 실행 결과 보존

`integrations/unity-mcp/`는 Coplay Unity MCP `10.2.0` revision `30d22075093d1d35dfb0091c1c7550e9ad948577`의 로컬 외부 도구 패치다. Splinter workspace 명령·Core/Runtime·제품 capabilities를 확장하지 않는다. Unity 편집·컴파일·테스트와 합격 판단은 계속 에이전트와 MCP/Unity CLI가 담당한다. [실제 자격](verification/vss-and-mcp-remediation.md)과 [고정 범위](plans/vss-and-mcp-remediation.md)를 구분한다.

## 준비와 연결

원본 upstream과 uv/Unity cache를 수정하지 않는다. 새 디렉터리에 선택 소스만 checkout하고 `prepare.py`로 package/server를 생성한다. HEAD와 선택 경로의 미커밋/미추적 변경을 검사하고, 이미 존재하는 출력은 보존·거부한다. `candidate.json`의 upstream revision·패치 파일 hash를 `verify.py`로 확인한다. upstream 라이선스는 준비한 checkout의 `LICENSE`를 따른다.

```powershell
git clone --no-checkout --depth 1 --branch v10.2.0 https://github.com/CoplayDev/unity-mcp.git <upstream>
git -C <upstream> sparse-checkout set MCPForUnity Server/src
git -C <upstream> checkout
python integrations/unity-mcp/prepare.py --upstream <upstream> --output <새-output>
python integrations/unity-mcp/verify.py <output>
```

해당 Editor를 정상 종료하고 `Packages/manifest.json`의 `com.coplaydev.unity-mcp`만 준비한 `<output>/package`의 절대 `file:` 경로로 연결한다. 원래 manifest/lock을 보존하고 Unity가 갱신한 lock을 함께 검토한다. 패키지 이름/버전은 upstream과 같으며 별도 공식 릴리스나 모든 프로젝트의 자동 설치가 아니다. 같은 PC에서 새 A/B로 상속하는 절대 경로는 가능하지만 다른 PC에는 먼저 같은 후보를 준비해야 한다. 현재 검증 PC의 연결은 `.local/unity-mcp-remediation/candidate-02/package`이며 최종 재생성 후보와 파일이 일치한다.

공유 HTTP 서버는 기존 사용자가 없는 것을 확인하고 정상 종료한 뒤, 같은 버전·8080에 재시작한다. 준비한 server의 의존성과 호환되는 기존 Python venv를 사용한다. `server.py <output>/server --transport http --http-url http://127.0.0.1:8080 --project-scoped-tools --pidfile <외부-evidence>/mcp-server.pid`로 실행하며 로그도 프로젝트 Library 밖에 둔다. 이미 점유된 포트에 두 번째 서버를 띄우지 않는다. 시작 wrapper는 패치 파일 hash를 확인한 뒤 준비한 server를 import한다. 검사 실패는 덮어쓰기나 다른 버전 fallback으로 숨기지 않는다.

Codex는 한 HTTP endpoint를 사용하고 각 호출의 `unity_instance`를 현재 discovery의 정확한 프로젝트 hash로 지정한다. 단일 HTTP 서버가 여러 Editor를 구분하므로 Editor별 URL·Stdio 전환이 이 응답 문제의 필수 해결책은 아니다. 반환 project/PID/start를 소유 Editor와 대조하고 reload 후 새 session/discovery를 확인한다. Editor가 연결되지 않았다면 올바른 프로젝트에서 Connect를 명시적으로 수행한다. 모든 프로젝트의 설정을 임의로 재작성하거나 서버 자동 시작을 켜지 않는다.

## Codex 설정 검사

HTTP 항목은 `url = "http://127.0.0.1:8080/mcp"`를 사용한다. Stdio에서 전환할 때 `command`, `args`뿐 아니라 그 항목의 `[mcp_servers.<name>.env]` 하위 표도 제거해야 한다. 현재 Codex streamable HTTP는 이 `env`를 허용하지 않는다. 전역 설정은 모든 대화에 영향을 주므로 원본을 보존하고 대상 항목 외 변경이 없는지 확인한다. 중복 alias를 비활성화할 때도 다른 서버 설정을 유지한다.

설정 변경 뒤 다음 두 **설정 조회 검사**를 완료한다. TOML 문법 파싱만으로는 transport 제약을 검증하지 못한다. 실패하면 새 연결을 진행하지 않고 해당 변경을 바로 고치거나 원본으로 복구한다. 전체 설정/토큰은 출력하지 않는다.

```powershell
codex mcp list --json
codex mcp get unity_mcp_coplay --json
```

이 조회 성공과 실제 Unity 연결은 별도 확인이다. 이미 실행 중인 대화의 tool schema/binding은 즉시 바뀌지 않을 수 있으므로 현재 HTTP 서버의 실제 schema와 연결 상태를 확인한다.

대화창 `/mcp`는 상태 조회이며 실제 앱 설정의 MCP 서버 재시작과 구분한다. [OpenAI 공식 안내](https://learn.chatgpt.com/docs/extend/mcp?surface=cli)는 앱 설정 → MCP servers → Restart를 제시한다. 설치 버전에 이 항목이 없으면 미확인 버튼을 반복 조작하도록 지시하지 않는다. 현재 작업은 확인된 HTTP client로 계속할 수 있으며 기존 대화의 기본 binding 전환은 해당 앱 재연결/재시작 후 별도로 관측한다. OAuth 없는 로컬 서버의 인증 미지원 표시는 로그인 요청으로 해결하지 않는다.

전체 앱을 재실행할 때 에이전트의 실행 세션에서 시작한 외부 Editor/서버도 함께 종료될 수 있다. Editor가 반드시 유지된다고 안내하지 않는다. 작업 상태와 정확한 PID/start를 먼저 확인하고, 종료됐다면 같은 프로젝트·패키지·포트로 독립 실행한 뒤 HTTP 응답과 Editor Connect를 확인한다. 앱 도구의 재연결은 서버가 응답하는 상태에서 수행한다. 현재 대화가 이미 받은 startup 실패와 직접 HTTP client의 성공은 따로 기록하며 전역 설정 재작성이나 Editor 쓰기 재시도로 우회하지 않는다.

## Editor 종료 후 남은 MCP 연결 확인

MCP를 사용하는 병렬 작업의 알려진 Editor 하나가 종료되면 남은 원본/peer Editor의 MCP 연결을 확인한다. `unity stop`, release/discard 과정의 종료, 관리형 기준 전환·복원을 위한 종료와 사용자가 닫았음을 관측한 경우에 적용한다. 종료한 Editor의 연결 해제는 정상이며, 공유 서버 중단은 남은 Editor의 연결 복구가 필요한 별도 상태다. 확인은 필수이고 정상 서버의 재시작은 필수가 아니다. 이 절차는 [Splinter 스킬](../skills/splinter/SKILL.md#check-remaining-mcp-connections-after-an-editor-stops)이 수행하며 제품의 자동 재시작 기능이 아니다.

예정된 종료 전에 계속 사용할 Editor의 project path·PID/start·MCP instance, 사용하는 endpoint/transport·서버 버전·기존 시작 절차·로그 위치를 인계에 남긴다. 실행 중이거나 완료한 중요한 job의 request/job ID와 상세 결과도 보존한다. 인증 토큰과 전체 명령행은 출력하지 않는다. 이미 종료됐다면 기존 인계와 실제 프로세스 관측에서 identity를 확인하며 빈 MCP 목록으로 Editor 종료를 추측하지 않는다.

| 종료 뒤 관측 | 대응 |
| --- | --- |
| 서버가 응답하고 남은 정확한 instance의 읽기 요청도 성공 | 그대로 작업 지속. 서버/Editor를 재시작하지 않음 |
| MCP 서버가 중단됐고 기록한 endpoint의 포트가 비어 있음 | 기존 승인 범위의 시작 절차로 같은 버전·transport·endpoint를 복구하고 알려진 남은 Editor를 재연결 |
| 서버는 정상이나 특정 Editor만 instance 목록에서 누락 | 실제 해당 Editor의 PID/start·초기화 상태를 확인하고 기존 Connect/start 방식으로 그 클라이언트만 재연결 |
| 포트가 다른 프로세스에 점유됐거나 서버/Editor 소유권이 불명확 | 기존 프로세스를 종료하거나 다른 포트로 우회하지 않음. 영향을 받는 MCP 쓰기를 보류하고 정확한 차단 상태를 보고 |

서버가 중단된 경우 준비 단계의 **기존 사용자 없는 서버를 정상 종료**하는 절차를 반복할 필요가 없다. 실제 부재·포트 미점유를 확인한 뒤 승인한 기존 실행을 복구한다. 현재 작업의 서버 재시작/외부 로그 승인은 같은 범위에서 유지되며 이를 없던 승인으로 취급하지 않는다. 서버를 정상 재시작했다는 이유만으로 복구 완료를 선언하지 않는다. 서버 응답 뒤 알려진 각 남은 Editor의 기존 Connect 또는 해당 버전이 제공하는 시작 경로로 연결하고 `mcpforunity://instances`를 다시 읽는다. 서버가 내려가 MCP 도구를 사용할 수 없다면 기존 Unity CLI의 exact-path 경로나 실제 해당 Editor의 Connect를 사용한다. 연결 문제 때문에 새 Editor/workspace를 만들거나 다른 Editor를 종료하지 않는다.

새 discovery의 정확한 instance에 읽기 전용 요청을 보내 반환 project/PID/start를 저장한 workspace/소유 Editor와 대조한다. 큰 시작 시각은 문자열로 받는다. A/B는 per-call target을 사용하고 다른 요청과 세션 기본 대상을 경쟁하지 않는다. 기존 request/job ID를 먼저 조회하며 재연결 자체는 timeout 쓰기·씬 편집·test 재실행의 승인이 아니다. Unity의 import/compile 완료와 실제 도구 readiness도 구분한다.

한 번의 명시적 복구와 도구 timeout에 맞춘 한정 readiness 확인 뒤에도 연결/identity가 확인되지 않으면 반복 재시작하지 않는다. 작업과 기존 요청을 보존하고 영향받은 MCP 작업만 대기 상태로 보고하며 독립적인 파일 작업·승인한 회수는 계속한다. 모든 Editor가 종료됐고 MCP 후속 작업이 없다면 서버를 다시 띄우지 않는다. 남은 알려진 공유 사용자/서버의 보호 확인은 유지한다. 결과와 인계에는 종료한 Editor의 회수 결과, 공유 서버 상태, 각 남은 Editor의 연결 확인/미확인과 다음 행동을 분리해 기록한다.

정상 종료 때 공유 서버까지 내려간 [실제 None 관측](verification/plain-project-basis.md#외부-mcp-종료-결함과-복구)의 대응으로 이 운영 절차를 선택했다. 당시 같은 버전/포트의 서버 복구와 기존 Editor의 Codex 기본 MCP 재연결은 확인했다. 절차 문서화는 외부 MCP 소유권 결함을 수정하거나 무중단 공존을 입증하지 않으며, 새 종료 시험이나 다른 패키지의 일반 자격으로 승계하지 않는다.

## 긴 코드 실행

`execute_code`에 다음 입력을 사용한다. `start`는 Editor SessionState에 request/input을 먼저 기록하고 빠르게 job ID를 반환한다. 다음 Editor update에서 기존 ExecuteCode를 한 번 실행한다. Inspector 갱신에 의존하는 `delayCall`은 사용하지 않는다.

```json
{"action":"start","request_id":"task-step-01","code":"return 1;","compiler":"auto","safety_checks":true,"unity_instance":"<현재-project@hash>"}
```

`get_job`에는 같은 target과 request ID 또는 job ID만 전달한다. 이 조회는 코드를 실행하지 않는다. 실행 중에는 main thread가 바빠 조회가 지연될 수 있으므로 `timeout_seconds`를 필요한 범위에서 지정한다(1~300, 기본 30). 접수 응답이 유실돼도 먼저 request ID로 상태를 읽는다. 같은 request ID·같은 code/compiler/safety input의 `start`는 기존 job을 반환하며 다른 input은 `CodeJobRequestConflict`로 거부한다. 서버의 자동 reload 재전송은 끈다. 동기 `execute`는 유지하지만 긴 임의 쓰기의 안전한 반복 수단으로 사용하지 않는다. 오류의 code/error/hint를 보존하며 null message로 원인을 버리지 않는다.

`data.status`와 내부 `data.result.success`를 함께 확인한다. 조회 자체의 `success=true`는 실행 성공이 아니다. `queued/running` 상태 중 reload되면 `interrupted`이며 자동 재실행하지 않는다. 성공/실패 결과는 같은 Editor 세션의 reload 후 복원한다. job은 세션당 10개이며 자동 퇴출하지 않는다. 결과가 1MiB를 넘으면 `completed_result_unavailable`로 실행 사실을 보존하고 반복을 막는다. 이 패치는 Editor 재시작·OS 장애의 durable queue, 임의 코드 취소·롤백, 같은 Editor에서 두 코드의 CPU 병렬 실행을 제공하지 않는다.

## 테스트 상세 결과

TestJobManager의 기존 최근 10개 job 정책을 유지하면서 완료한 TestRunResult와 immutable summary/item의 JSON 생성자를 연결한다. `run_tests`의 job ID와 Editor identity를 보존하고 `get_test_job(include_details=true)`로 읽는다. 같은 Editor의 domain reload 뒤 같은 job ID로 summary·개별 결과·실패/stack/output을 복원한다. 상세 결과 보존에는 해당 결과 크기의 SessionState 비용이 추가되며 RAM 절감 기능이 아니다. 큰 테스트 묶음·Editor 종료 후 보존은 이번 자격에 포함하지 않았다. 중요한 결과는 외부 evidence에도 보존한다.

복구는 정상 Editor stop 후 원래 manifest/lock과 기존 서버 실행 경로를 되돌리는 명시적 작업이다. 활성 Editor/다른 작업이 참조하는 로컬 후보·서버를 삭제하지 않는다. 변경을 되돌릴 때도 Codex 설정 조회 검사와 실제 연결을 확인한다.

현재 고정 후보의 전역 EditorPrefs에 stale handshake가 남으면 정상 종료의 `StopManagedLocalHttpServer`가 다른 Editor와 공유하는 서버를 포트 추정으로 종료할 수 있다. 응답/job 보존 패치는 이 shutdown 경계를 수정하지 않았다. 종료 뒤에는 위 연결 확인·필요한 복구 절차를 따른다.
