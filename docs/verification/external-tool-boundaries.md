# 선택적 외부 도구 경계 검증

2026-10-06의 변경은 `development @ d97bd3cf9c7aaf7d5657d60271881538953ae8fd`에서 시작한 `codex/remove-external-tool-coupling` 워크트리에 한정한다. 현재 책임은 [외부 도구 안내](../unity-mcp-response-recovery.md)와 [Unity 수명](../alpha-workspaces.md)을 따른다. MCP·Unity CLI의 설치·이름·버전·내부 API·연결 성공은 제품 전제나 gate가 아니다.

| ID | 변경 경계 | 확인과 한도 |
| --- | --- | --- |
| ET-01 | 소유 Editor 초기화·정상 종료 | 실제 `WorkspaceBridge.cs`를 링크한 기존 Unity API stub harness에서 외부 패키지 없음과 shutdown class는 있으나 이전 비공개 함수가 없는 합성 구현을 각각 실행한다. 외부 assembly resolver 호출 0, ready/compiling/ready/exiting heartbeat, 프로젝트/PID/시작 시각/토큰과 다른 quitting callback 보존을 확인한다. 실제 Unity/외부 패키지 실행 자격은 아님 |
| ET-02 | 정상 종료 보호·launch 준비 | 잘못된 토큰·compile/import 중 종료 차단과 Play/미저장 씬의 요청 identity가 있는 거부를 확인한다. 실제 임시 파일 시험은 Unity compiler/Pipeline 주소만 제거하고 MCP/다른 도구 run-state·Library artifact·원본 파일을 보존한다. 기존 정상 종료 대기·timeout·취소 시험과 소유 자원/잠금 guard 유지 |
| ET-03 | 외부 패치 제작/운영 제거 | `integrations/unity-mcp/`의 prepare/verify/server·인증 보정·C# job·전용 시험 소스를 제거한다. 제품/개발 CLI·Unity bridge·scripts에 Coplay/MCPForUnity/PID 패턴·토큰·패치 실행 참조가 없음을 검색한다. README·요구사항·AGENTS·스킬·시작/기반/사용 계획과 소유 문서를 선택적 도구 경계로 정리 |
| ET-04 | 기존 보존·회수 계약 | 필수 locked restore·Release 빌드·전체 자동 회귀로 seal·독립 결과·원본/peer·dirty/target·소유 자원 guard의 회귀를 확인한다. 입력/공개 응답 schema·fake 공통 계약·workspace v2·저널 형식은 변경하지 않음. heartbeat에서 제거한 `coplayShutdownGuard`는 내부 bridge 관측 필드이며 공개 schema나 gate에서 사용하지 않았음 |

## 현재 자동 실행 근거

필수 `pwsh ./scripts/build.ps1`은 locked restore·Release 빌드 **경고 0 / 오류 0**, 전체 **1,069개: 1,060 Passed / 0 Failed / 9 conditional Skipped**로 통과했다. 시험 시간은 약 7분 24초다. skip은 별도 evidence 입력·자식 진입점 등 명시적 실행 조건이 필요한 시험이며 새 native 통과로 합산하지 않는다. 변경한 bridge의 두 합성 경우와 외부 run-state/원본 파일 보존 시험도 Passed다.

최종 로그는 이 워크트리의 `.local/external-tool-build.log`, 원시 결과는 [TRX](../../artifacts/test-results/Splinter.Tests_net10.0_x64.trx)다. 첫 제한 실행의 restore는 상세 진단 없이 exit 1로 종료했고, 같은 워크트리의 일반 사용자 실행 환경에서 위 필수 명령 전체를 다시 완료했다. 설치·관리자 Editor·서비스 변경으로 우회하지 않았다. 의존성/lockfile은 변경하지 않는다. UTF-8·로컬 문서 링크 552개와 `git diff --check`도 통과했다.

## 남은 경계와 보존한 근거

외부 패키지가 자체 quitting callback에서 공유 서버를 종료하는 문제는 해당 도구와 에이전트 절차의 책임이다. Splinter가 callback을 해제하거나 정상 종료를 외부 연결 상태로 차단하지 않는다. 다음 외부 요청 전에 대상과 연결을 확인하고 복구 여부를 판단하는 절차와 Splinter 결과/회수 판정은 분리한다. 다른 원본/peer 프로세스·서비스·snapshot을 종료/삭제해 우회하지 않는다.

실제 Unity A/B·MCP·Unity CLI·서비스 설치/Update·native 행렬은 **NotRun**이다. 이미 실행 중인 Editor/서버·설치된 패키지·전역 Codex 설정은 변경하지 않았다. 새 소스의 자동 회귀를 그 환경의 적용/호환 통과로 읽지 않는다. 저장 격리는 임의 외부 코드의 OS sandbox가 아니며 외부 도구 자체가 초기화/종료 중 실패하는 모든 경우를 보장하지 않는다.

[UH](unity-cli-handoff.md)·[UP](unity-parallel-editor-validation.md)·[VR/MR](vss-and-mcp-remediation.md)·[SB-02](security-boundaries.md)와 [NP의 외부 종료 관측](plain-project-basis.md#외부-mcp-종료-결함과-복구)은 당시 버전·후보·입력의 사실로 보존한다. 과거 callback 보호/고정 패치의 성공은 현재 제품 요구나 통과로 승계하지 않는다. 생성된 외부 수정본·원시 evidence·독립 결과·원본/형제 fixture와 다른 작업의 WIP는 변경하지 않았다. 검증을 위한 원본 프로젝트 반영·push·PR·배포는 수행하지 않았다. 제품 소스의 `development` 통합과 별개의 경계다.
