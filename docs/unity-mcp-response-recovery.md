# Unity MCP의 긴 실행과 reload 결과 보존

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
