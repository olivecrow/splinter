# 제품 범위와 명령 발견의 검증

첫 버전의 한정 판정은 [M0 기록](m0-delivery.md), 후속 책임은 [구현 계획](../plans/implementation-plan.md), 현재 명령은 [기반 구조](../foundation.md)와 [개발 CLI](../development-cli.md)를 따른다. 이 문서는 작업공간 관리에 제한한 현재 발견/거부·결과 보존 계약과 자동 회귀의 한도를 소유한다.

## 현재 계약

- 기본 `scripts/splinter.ps1`은 NTFS workspace 생성/재개·관측·범위 저널 정리·checkpoint·integrate·release/discard, 소유 Editor start/stop과 읽기 전용 service status를 제공한다. diagnostic checkout·view·spike/probe는 기본 발견·생성에서 제외한다.
- `scripts/splinter-dev.ps1`은 선택적 저장소 경로와 개발 시험의 별도 진입점이다. `view --input`은 불변 source generation·단일 Library cache·명시적 보존/회수 의존성을 관리한다. 자동 모드·큐·Unity 작업 판단은 제공하지 않는다.
- 자체 refresh·컴파일 요청/콜백·도메인 reload 성공 증거·Editor 포커스 조작·내부 컴파일 성공 gate는 없다. `unity refresh`·`validation`과 view의 refresh/observe-validation/preserve-worker를 호환 실행하지 않는다. 제거한 명령/입력은 상태나 provider 부작용 전에 거부한다.
- checkpoint는 Editor 유무·컴파일 결과와 무관하게 안정된 현재 소스를 독립 결과로 보존한다. NTFS는 정상 stop·소유 자원 0·bridge 제거·전후/unmount 뒤 같은 저널 seal을 요구한다. integrate는 현재 revision/fingerprint와 정확한 독립 결과·원본 target/dirty guard를 확인한다. 컴파일·테스트 성공은 주장하지 않는다.
- 현재 형식의 과거 증거·작업/후보/cache·영수증은 보존한다. 일반 status와 공개 application/workspace 응답은 컴파일 증거를 게시하지 않는다. 저장된 과거 observation receipt는 구조를 검사하지만 새 observation은 실행하지 않는다. 제거된 미완료 요청이나 지원하지 않는 상태는 자동 이관/추측 복구하지 않고 보존·거부한다.
- DAG·AI Runner·daemon/DB·범용 Unity 저작/Play/Test Runner·제품 installer·자동 conversation binding은 구현 범위에서 제외한다. `plan validate`와 plan-request schema도 없다. discard는 구현된 명령이며 `unavailable`에는 installation/binding만 표시한다.

## 자동 회귀와 한도

`ContractAndCliTests`는 기본/개발 command discovery·NTFS 전용 ensure schema·삭제 명령의 부작용 전 거부·현재 응답에서 컴파일 필드 배제를 검사한다. `ValidationProductStateTests`와 `ValidationWorkflowTests`는 삭제 action의 durable request/operation/provider 효과 0을 확인한다.

`WorkspaceCoordinatorTests`·`GitWorkspaceProviderTests`·`GitSnapshotWorkspaceTests`는 내부 컴파일 증거 없이 현재 소스를 수집/통합하고, 원본/dirty guard와 metadata 포함 fingerprint·독립 결과를 확인한다. `SnapshotWorkspaceCoordinatorTests`·`WorkspaceDiscardTests`는 결과 이후의 source/delta 변경, 독립 결과 누락, 소유 자원·cache/snapshot 의존성을 계속 차단한다. 당시 Unity bridge의 Coplay 종료 hook 보호 자격은 과거 후보에 한정한다. 현재 harness는 외부 패키지 부재/내부 API 변경과 무관한 초기화·heartbeat·정상 종료 및 다른 quitting callback 보존을 [별도 검증](external-tool-boundaries.md)으로 확인한다.

필수 실행은 `pwsh ./scripts/build.ps1`의 locked restore·Release 빌드·전체 자동 회귀다. 현재 후보의 실행 결과와 hash는 아래에 기록한다. 이 자동 회귀는 실제 VSS·WinFsp·Unity A/B 또는 외부 MCP/Unity CLI 연결·ARM·재부팅/전원/물리 공간 장애의 통과가 아니다. 기존 M0/WF/LB 기록을 이번 축소 후보의 native 자격으로 승계하지 않는다. 설치/grant·원본 fixture·원격 환경은 이번 변경에 포함하지 않는다.

## 보존한 이전 근거

2026-10-01 `development @ 9ec88e6`의 DAG 제외 후보는 705 Passed, 0 Failed, 당시 제외 시험 1 Skipped였다. [manifest](../../artifacts/workspace-product-scope/20261001-154027-033/automated.json)·[TRX](../../artifacts/workspace-product-scope/20261001-154027-033/automated.trx)는 당시 근거이며 현재 코드의 통과 선언이 아니다. 원시 artifacts·독립 결과 refs·snapshot은 정리 대상으로 삭제하지 않는다.

## 현재 자동 실행 근거

2026-10-02 `development @ 146a402`의 미커밋 축소 후보에서 locked restore·Release 빌드는 경고 0·오류 0, 전체 회귀는 **776 Passed, 0 Failed, 당시 제외 시험 1 Skipped**였다. 이후 오류 문구·공백만 정리하고 최종 Release 재빌드와 CLI 계약 **56 Passed, 0 Failed**를 확인했다. [실행/소스·바이너리 hash manifest](../../artifacts/workspace-product-scope/agent-boundary-20261002-040234/automated.json)·[전체 TRX](../../artifacts/workspace-product-scope/agent-boundary-20261002-040234/full.trx)·[최종 CLI TRX](../../artifacts/workspace-product-scope/agent-boundary-20261002-040234/cli.trx)를 보존한다. 제한 환경의 후속 Git 생성 1건은 실패했고 같은 시험은 일반 권한 환경에서 통과했다. 최초 실패 회귀와 제한 환경 로그도 같은 evidence에 남긴다.
