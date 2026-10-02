# 병렬 Unity 작업공간과 Editor 수명

제품은 NTFS 작업공간과 그 Editor의 수명을 관리한다. 생성·재개·범위 저장·독립 결과·통합·회수/폐기는 [NTFS 계약](ntfs-workspaces.md)을 따른다. 선택적 ReFS와 Library view는 [개발 CLI](development-cli.md)에 분리돼 있다.

## 에이전트의 기본 흐름

빌드된 제품의 `scripts/splinter.ps1`을 절대 경로로 호출하고 등록된 프로젝트/home·명시적 base·안정된 task key를 사용한다. home은 원본 프로젝트 밖에 두고 모든 작업자가 같은 등록 home을 공유한다.

```text
splinter workspace ensure --project <Unity 저장소> --key <작업 key> --storage ntfs-snapshot --base development --editor --home <등록 home>
splinter workspace status --id <반환 id> --home <등록 home>
```

`data.workspace.path`에서 편집하고 외부 MCP·Unity CLI의 projectRoot도 이 경로와 일치시킨다. `application.running`·`ready`는 실행/준비 관측이며 컴파일·테스트 성공이 아니다. Editor 준비 중 에이전트가 소스 분석·계획을 진행할 수 있다. 같은 key/id/home·기준·request digest를 다음 대화에 보존한다.

제품은 `unity start`와 `unity stop`을 제공한다. refresh·컴파일 분석·씬/에셋 편집·Play·테스트·빌드·캡처는 선택한 외부 도구로 수행한다. `unity refresh`와 자체 컴파일 성공 판정은 제공하지 않는다. Unity CLI의 작은 T: Plastic A/B 인계는 [한정 검증](verification/unity-cli-handoff.md)을 따르며 MCP·다른 입력/버전은 별도로 확인해야 한다.

## Unity CLI의 정확한 대상 선택

설치된 CLI의 version/help와 프로젝트의 기존 Pipeline을 확인한다. Splinter `application.ready`는 소유 Editor의 실행 관측이며 도구 연결·컴파일 합격을 증명하지 않는다. 반환 `data.workspace.path`를 모든 Editor 명령의 `--project-path`에 명시하고, 읽기 전용 catalog 응답의 `data.target.projectPath`와 공개 PID를 실제 workspace/`data.application.processId`에 대조한 뒤 편집한다. `status`의 substring 필터와 목록 누락을 정확한 대상/Editor 부재 증거로 사용하지 않는다.

Unity CLI `1.0.0-beta.5`·Pipeline `0.8.0-exp.1`에서는 목록에 빠진 worker도 다음 경로로 연결했다. 명령 이름/입력은 그 Editor의 catalog/schema에서 읽고 실제 응답의 outer/nested success와 결과를 각각 확인한다.

```powershell
unity --format json command --project-path <반환 workspace 경로> --tag scripts/compile
unity --format json command recompile --project-path <반환 workspace 경로> --caller plugin --skill unity-cli
unity --format json command recompile_status --project-path <반환 workspace 경로> --caller plugin --skill unity-cli
```

`recompile` 요청 성공은 컴파일 완료가 아니다. 해당 작업의 status와 loaded 코드 결과를 외부 도구에서 확인한다. reload 뒤 포트가 바뀔 수 있으므로 포트를 저장해 다른 작업을 선택하지 않고 같은 반환 경로로 재연결한다. eval의 큰 정수 시작 ticks가 필요하면 `ToString()`으로 반환해 JSON 숫자 정밀도 손실을 피한다. 인증 token이나 전체 프로세스 명령행은 출력하지 않는다.

실제 검증의 첫 eval 타임아웃과 읽기 전용 동일 요청의 한 번 재시도는 검증 문서에 보존했다. 쓰기/씬 변경/컴파일 요청을 시간 경과만으로 반복하지 않는다. 연결 실패에서는 정확한 경로·소유 상태·descriptor 준비·Safe Mode·sandbox 관측을 구분하고 작업을 보존한다. 원본 fallback·자동 패키지 교체나 서버 탈취는 하지 않는다. 정상 `unity stop` 뒤 peer의 정확한 경로/PID와 실제 후속 응답을 확인하고, 독립 결과/회수는 Splinter의 기존 checkpoint/release를 사용한다.

## 결과와 명시적 종료

```text
splinter workspace checkpoint --id <id> --message <보존할 결과 설명> --home <home>
splinter workspace integrate --id <id> --target development --home <home>
splinter workspace release --id <id> --home <home>
```

checkpoint는 컴파일 성공 없는 작업도 보존한다. 소유 Editor를 정상 종료하고 자식 자원 0을 확인한 뒤 소유 bridge를 제거한다. Git 수집의 전후 내용 일치와 독립 result ref, 수집·unmount의 저널 seal을 확인한다. 작업 내용의 합격 여부는 에이전트가 판단하며 checkpoint 자체로 컴파일·테스트 성공을 주장하지 않는다. 결과 수집 뒤에는 같은 작업의 새 편집을 시작하지 않는다.

integrate는 정확한 수집 revision과 현재 소스의 일치, 깨끗한 요청 대상 branch, 저장소 잠금을 확인하고 Git merge를 수행한다. 이 명령이 시작한 충돌 merge만 중단하며 결과는 보존한다. 통합 전략·충돌 해결·원본 Editor의 통합 후 검증은 에이전트의 책임이다. 보존만 원하면 integrate 없이 release할 수 있다.

release는 독립 결과를 남긴 뒤 소유 Editor/provider/mount/Git/delta·마지막 소유 snapshot을 회수한다. `reclaim-pending`은 명시적인 같은 작업 재시도가 필요한 물리 회수 대기다. 작업을 버리려면 [명시적 discard](workspace-discard.md)를 사용한다. 대화/Editor 종료·timeout은 반영이나 폐기 승인이 아니다.

## 실행 격리와 정상 종료

Unity 어댑터는 PID·시작 시각·실행 파일·토큰·프로젝트 경로와 Job Object 자식을 확인한다. 소유 `Assets/__Splinter__` 브리지는 liveness·import/compile 중 상태 관측과 정상 종료만 제공하고 프로젝트 함수를 실행하지 않는다. Play·미저장 씬·진행 중 import/compile은 정상 종료를 막는다. 최대 2분의 종료 대기 뒤에도 강제 종료/회수하지 않으며 작업과 로그를 보존한다.

Coplay `10.2.0`의 전역 EditorPrefs 기반 종료 callback이 원본 MCP 서버를 종료하지 않도록 작업 프로세스 안에서 해당 callback만 해제한다. 전역 설정이나 패키지 파일은 수정하지 않는다. worker의 외부 MCP 서버를 시작하는 경로는 아직 자격을 확인하지 않았으며 다른 버전까지 호환성을 확대하지 않는다.

사용자 설정·라이선스·CPU/GPU·외부 서비스는 공유된다. 보안 샌드박스가 아니다. source hash 확인은 `workspace status --verify-source`, 전체 감사는 `--audit-source`다. 일반 status는 파일 내용을 읽지 않고 실행 자원을 관측한다. 실제 한정 실행 근거와 실패·재시도/비용은 [대표 사용](verification/representative-agent-workflow.md)에 보존하며 새 외부 도구 경로의 성공으로 승계하지 않는다.
