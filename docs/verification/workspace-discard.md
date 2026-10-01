# 공통 폐기 수명주기의 검증

소유 계약은 [workspace discard](../workspace-discard.md)다. 실행 범위는 Core/Runtime과 atomic 임시 상태·fake provider이며 실제 Git 등록 제거·WinFsp/VSS·Unity 실행의 지원 증거가 아니다. 제품 CLI의 discard는 미구현이고 M0-04/SW-09/10의 실제 상태는 NotRun이다.

`SnapshotWorkspaceCoordinatorTests`의 폐기 적합성은 다음 관문을 확인한다.

- 실패한 compile·dirty 입력을 checkpoint/source collection 없이 폐기하고, peer 작업과 Editor가 계속 유지됨.
- 앱 stop·unmount·source·delta·snapshot의 응답 유실 뒤 새 coordinator가 동일 intent만 재개. 편집/보존형 release의 교차 실행과 다른 intent는 거부.
- provider 미연결과 잘못된 대상 digest는 intent 저장·정지 전에 거부. 폐기 seal/source revision 변경·손상/누락된 owner 필드는 보존.
- 앱 자식·unknown 핸들/메모리는 0으로 읽지 않음. 공유 workspace·실제 Runtime의 candidate/source generation/cache 기록은 정지 전에 차단과 다음 행동을 반환.
- 지연 snapshot 회수는 완료와 구별하고 이미 처리한 source/delta를 다시 만들지 않음. 입력·state·응답 schema와 구조화된 오류의 보존 정책 확인.

시험은 기존 in-process fake와 각 시험의 새 임시 home을 사용한다. 외부 프로세스 강제 종료나 새 장애 harness는 추가하지 않는다. 실제 worker의 안전한 삭제, 이미 보존한 result ref의 보호, 실제 부분 파일 삭제·회수 authorization은 Windows/Git provider 연결 후보에서 별도로 확인해야 한다. 일반 폐기를 위해 기존 release의 결과 gate를 완화하지 않는다.

## 실행 근거와 판정

2026-10-01 `development @ af94ea6`의 미커밋 후보에서 공통 단위를 **scoped-complete-with-limitations**로 판정했다. `pwsh ./scripts/build.ps1`의 locked restore·Release는 경고 0·오류 0이며 자동 시험은 총 719 중 성공 718·실패 0·선택적 실제 ReFS 미실행 1이다. [고정 소스 hash](../../artifacts/workspace-discard/final-20261001-170615-205/source.json), [실행 manifest](../../artifacts/workspace-discard/final-20261001-170615-205/automated.json), [TRX](../../artifacts/workspace-discard/final-20261001-170615-205/automated.trx), [전체 로그](../../artifacts/workspace-discard/final-20261001-170615-205/build.log)는 로컬 근거다.

새 폐기 사례 13개와 기존 coordinator/Windows process 사례를 합친 [한정 실행 54개](../../artifacts/workspace-discard/final-20261001-165923-391/targeted.trx)도 통과했다. 최초 peer 준비 순서와 시험용 응답의 누락 digest는 fixture를 수정했고 [초기 실패](../../artifacts/workspace-discard/initial-fixture-failures/automated.trx)와 [응답 schema 실패](../../artifacts/workspace-discard/targeted-missing-response-digest.trx)를 보존했다.

기존 process 시험은 병렬 전체 실행에서 [호출자 pipe의 5초 관문 타임아웃](../../artifacts/workspace-discard/pre-reclamation-gate.trx)이 한 번 있었고 한정 실행에서는 통과했다. 상속 가능한 process-wide handle이 다른 시험의 프로세스 생성과 겹치지 않도록 이 시험을 전용 비병렬 collection으로 격리했다. 제품 launch/회수와 5초 assertion은 변경하지 않았다. 실제 타임아웃 때 handle을 소유한 PID는 미확정이며 이 후보의 전체 회귀 통과와 구분한다.

VSS 서비스 설치/업데이트·grant, 실제 workspace 폐기·Unity·VIVOBOOK·전원 장애는 이 공통 단위에 포함하지 않는다. M0-04/SW의 NotRun을 올리거나 기존 실환경 자격을 현재 후보로 승계하지 않는다.
