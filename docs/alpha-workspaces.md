# 병렬 Unity 작업공간과 Editor 수명

제품은 NTFS 작업공간과 그 Editor의 수명을 관리한다. 생성·재개·범위 저장·독립 결과·통합·회수/폐기는 [NTFS 계약](ntfs-workspaces.md)을 따른다. 선택적 ReFS와 Library view는 [개발 CLI](development-cli.md)에 분리돼 있다.

## 에이전트의 기본 흐름

빌드된 제품의 `scripts/splinter.ps1`을 절대 경로로 호출하고 등록된 프로젝트/home·명시적 base·안정된 task key를 사용한다. home은 원본 프로젝트 밖에 두고 모든 작업자가 같은 등록 home을 공유한다.

```text
splinter workspace ensure --project <Unity 저장소> --key <작업 key> --storage ntfs-snapshot --base development --editor --home <등록 home>
splinter workspace status --id <반환 id> --home <등록 home>
```

`data.workspace.path`에서 편집하고 외부 MCP·Unity CLI의 projectRoot도 이 경로와 일치시킨다. `application.running`·`ready`는 실행/준비 관측이며 컴파일·테스트 성공이 아니다. Editor 준비 중 에이전트가 소스 분석·계획을 진행할 수 있다. 같은 key/id/home·기준·request digest를 다음 대화에 보존한다.

제품은 `unity start`와 `unity stop`을 제공한다. refresh·컴파일 분석·씬/에셋 편집·Play·테스트·빌드·캡처는 선택한 외부 도구로 수행한다. `unity refresh`와 자체 컴파일 성공 판정은 제공하지 않는다. 외부 도구의 실제 A/B 인계 자격은 아직 별도로 확인해야 한다.

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
