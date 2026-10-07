# 선택적 외부 Unity 도구와 에이전트의 책임

[English](../unity-mcp-response-recovery.md)

MCP·Unity CLI는 사용자가 설치한 도구 중 에이전트가 필요할 때 선택하는 선택적 외부 도구다. 없어도 Splinter의 저장 격리·기준본·Library/미변경 파일 참조·범위 변경·독립 결과·안전한 원본 반영과 소유 자원 수명 기능은 성립한다. 특정 도구 이름·버전·내부 API는 제품 전제나 gate가 아니다. 도구 선택·연결·refresh·컴파일·Play/Test/build·캡처와 결과 판단은 에이전트가 맡는다. 외부 도구 설치·다운그레이드·수정·별도 수정본 생성/운영은 Splinter 범위에서 제외한다.

Splinter의 `Assets/__Splinter__` 브리지는 소유 Editor의 liveness·import/compile 진행 관측·정상 종료만 제공한다. 외부 패키지의 초기화·비공개 shutdown callback·PID 파일·서버·인증·job API를 조회하거나 변경하지 않는다. Editor ready와 외부 도구 연결·프로젝트 합격은 각각 별개의 관측이다.

## 대상 선택과 불확실한 요청

사용자가 설치한 도구의 현재 도움말/schema를 읽고 실제 반환 프로젝트 경로·PID/시작 시각을 Splinter의 workspace/소유 Editor와 대조한다. discovery 누락은 Editor 부재가 아니다. reload 뒤에는 대상/session을 다시 확인한다. request/job ID와 중요한 상세 결과를 보존하고 timeout·unknown 응답 뒤 기존 수행 여부부터 확인한다. 불확실한 쓰기·씬 변경·테스트를 자동 반복하지 않는다.

## Editor 종료 후 남은 MCP 연결 확인

MCP 또는 다른 외부 연결을 실제 사용하는 경우, 에이전트는 알려진 Editor 하나의 종료 후 다음 외부 요청 전에 남은 원본/peer의 실제 연결과 identity를 확인한다. 건강한 공유 서버와 다른 Editor를 유지한다. 외부 패키지가 공유 서버를 종료하거나 client를 끊는 경우 해당 도구의 현재 절차와 기존 승인 범위로 복구한다. 소유권·연결이 불명확하면 영향을 받은 외부 쓰기를 보류하고 작업/요청을 보존한다. 연결 복구를 위해 패키지 수정본·새 workspace·전역 설정 변경을 자동 수행하지 않는다.

외부 연결 복구와 Splinter의 checkpoint/release/discard 결과는 별도로 보고한다. 연결 실패는 안정된 독립 결과 보존이나 소유 자원 회수의 실패 판정이 아니며, 저장소 seal·dirty/target·원본/peer 보호와 정상 종료 guard는 그대로 적용된다. 외부 도구를 사용하지 않으면 서버 연결·복구 절차가 필요하지 않다.

## 과거 패치의 근거

2026-10-05의 Coplay 고정 패치·긴 코드 job·reload 상세 결과·인증 후보의 근거는 [VR/MR](verification/vss-and-mcp-remediation.md)와 [SB-02](verification/security-boundaries.md)에 보존한다. `integrations/unity-mcp/`의 패치 생성·검사·실행 기능은 제공 범위에서 제거했다. 그 기록은 현재 사용 절차·설치 요구·버전 gate·지원 선언이 아니다. 이미 생성하거나 설치한 외부 후보·패키지·서버·evidence·독립 결과는 이 정리로 변경하지 않는다. [현재 결합 제거 검증](verification/external-tool-boundaries.md)은 자동 bridge/file 회귀와 미실행 native 경계를 구분한다.
