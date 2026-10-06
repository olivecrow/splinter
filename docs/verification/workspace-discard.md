# 명시적 폐기 연결의 검증

소유 계약은 [workspace discard](../workspace-discard.md)다. 이 후보의 실행은 Core/Runtime과 실제 Git·Windows 파일 삭제·VSS authorization·제품 CLI 연결이다. 사용자 승인으로 기존 서비스를 업데이트하고 등록된 로컬 fixture에서 **Editor 없는 dirty A 폐기·같은 요청 재시도·B 후속 쓰기·A/B 자원 회수**를 확인했다. 이 실행 단위는 scoped-complete-with-limitations이며 Unity 컴파일 실패·Editor 종료는 별도 [대표 A/B 실행](representative-agent-workflow.md)에서 확인했다. 현재 M0 자격은 추적표를 따르고 이 Editor 없는 기록으로 승계하지 않는다. native 강제 중단/SW-10 NotRun·SW-09 전체 Partial은 유지한다.

## 현재 provider/CLI 연결 범위

- 실제 임시 Git 저장소의 정확한 task 등록/branch 삭제, unlock·등록 삭제·branch 삭제 이후 동일 intent 재개, 원본 dirty 파일·peer 등록·기존 독립 result ref 보존. 완료 뒤 다시 생긴 branch와 다른 intent는 거부한다.
- 실제 `JournalDeltaStore` 형식의 identity/journal을 sealed 상태로 제거하고 journal/디렉터리의 부분 삭제 후 재시도한다. unknown 자원·예상 밖 파일·변경된 journal은 보존한다. snapshot/자원 관측은 이 시험에서 fake다.
- Windows 실제 경로/파일 identity의 서비스 authorization: 별도 discard intent·zero 자원·branch/delta 부재·공유 peer·독점 reader fence. 결과 manifest와 `resultsPreserved=true` 없이 고정 snapshot 소유권을 확인한다.
- 보호된 fake 서비스 ledger의 같은 요청 응답 재생과 별도 discard gate, 실제 PowerShell helper의 discard authorization 분기, 제품 capabilities/schema/CLI 인수 적합성.

최초 설치·grant/볼륨 변경, Unity 폐기, VIVOBOOK과 OS 장애는 이 범위에 포함하지 않는다. 기존 서비스의 명시적 업데이트 뒤 등록된 로컬 fixture에서 한 묶음의 WinFsp/VSS 폐기 흐름만 실행했다. 새 장애 harness나 전체 지원 행렬을 추가하지 않는다.

## 서비스 패키지 준비 후보의 실행 근거

2026-10-01 `development @ c23ea4c` 이후 미커밋 후보의 `pwsh ./scripts/build.ps1`은 locked restore·Release 경고/오류 0, **744개 중 743 성공·0 실패·당시 제외 시험 미실행 1**이다. 이전 공통 후보의 718 성공을 현재 후보의 근거로 대체하지 않으며, 추가된 25개 경계 사례를 포함한 전체 회귀를 실행했다. [source hash](../../artifacts/workspace-discard/product-20261001-180005-293/source.json)·[manifest](../../artifacts/workspace-discard/product-20261001-180005-293/automated.json)·[TRX](../../artifacts/workspace-discard/product-20261001-180005-293/automated.trx)·[빌드 로그](../../artifacts/workspace-discard/product-20261001-180005-293/build.log)가 고정 근거다. 패키지 작성 후에도 시험한 소스 hash가 일치함을 확인했다.

[서비스 패키지 manifest](../../artifacts/workspace-discard/product-20261001-180005-293/package-01/package.json)의 SHA-256은 `962ceac3aa23b41a8a89987e14e31c258b3120a83a34d19c55e4151e21e292e3`이며 12개 파일 hash를 확인했다. [읽기 전용 Update 계획](../../artifacts/workspace-discard/product-20261001-180005-293/update-plan.json)은 `apply=false`다. 기존 evidence의 세 scope를 새 패키지의 read-only `storage-grant`로 현재 디렉터리/볼륨 identity와 대조했고 새 grant를 추가하지 않았다. Apply는 보호된 실제 설정의 모든 기존 scope 보존을 다시 검사하며 누락/변경이 있으면 거부한다. [패키지/계획 검토 hash](../../artifacts/workspace-discard/product-20261001-180005-293/package-review.json)를 남겼다.

연결 코드의 자동 자격은 **scoped-complete-with-limitations**다. 설치된 서비스 실행 상태만으로 이 후보의 discard 지원을 선언하지 않는다. 승인 전 패키지/계획과 승인 뒤 실제 Update·로컬 실행 근거를 구분하며 새 OS/ARM/대형 입력 행렬은 이 완료 조건에 포함하지 않는다.

## 승인한 서비스 적용

사용자의 서비스 업데이트·로컬 시험 승인 뒤 위 패키지에 `setup-storage-service.ps1 -Action Update -Apply`를 실행했다. [적용 결과](../../artifacts/workspace-discard/product-20261001-180005-293/update-result.json)는 서비스 `Running`, 설치 파일의 패키지 hash 일치, 기존 세 scope 보존을 확인한다. 보호된 owner ledger의 [업데이트 전](../../artifacts/workspace-discard/product-20261001-180005-293/ledger-before-update.json)·[업데이트 후](../../artifacts/workspace-discard/product-20261001-180005-293/ledger-after-update.json) **1,189개 파일 hash가 모두 같다**. [실제 Apply](../../artifacts/workspace-discard/product-20261001-180005-293/update-applied.json)와 기존 `apply=false` 계획을 구분한다.

## 로컬 dirty A/B 폐기와 같은 요청 재개

[최종 실행 manifest](../../artifacts/workspace-discard/product-20261001-180005-293/native-trial.json)는 Windows `10.0.26200` x64·Git `2.45.1.windows.1`, 원본 `T:/ua-source @ f71e4e244ba9b085aa0fc439336e4c60ee3d5d5d`와 기존 등록 home의 한정 실행이다. 새 A `3536bbfc8c0f0c2ba4e581f5`를 먼저, B `b6afaca474b21b07d71d53d1`를 뒤에 캡처했다. 각 worker의 private text 파일을 미커밋 상태로 만들었으며 Unity·validation·checkpoint는 실행하지 않았다.

- [A 재개 완료](../../artifacts/workspace-discard/product-20261001-180005-293/trial-a-discard-resume.json)·[동일 요청 재전송](../../artifacts/workspace-discard/product-20261001-180005-293/trial-a-discard-duplicate.json)은 `Succeeded`/`discarded`다. 재전송은 기존 완료 시각과 VSS attempt ID를 유지한다.
- [A 종료 뒤 B](../../artifacts/workspace-discard/product-20261001-180005-293/trial-b-after-a.json)는 같은 snapshot·provider PID/시작 시각과 기존 편집 hash를 유지했다. 기준 README 읽기와 A 파일의 격리를 확인한 뒤 [B의 후속 36-byte 쓰기](../../artifacts/workspace-discard/product-20261001-180005-293/trial-b-continued.json)·[현재 소스 확인](../../artifacts/workspace-discard/product-20261001-180005-293/trial-b-after-write.json)이 성공했다.
- [B 최초 폐기](../../artifacts/workspace-discard/product-20261001-180005-293/trial-b-discard.json)도 수정 후보에서 한 번에 완료했다. A/B 모두 정확한 mount·Git 등록·task branch·delta·소유 VSS 부재와 앱/provider·핸들·메모리 0을 확인했다. 각 삭제 격리 영수증의 다른 snapshot 소실은 0이다. 결과 보존 gate를 만들지 않았다.
- 원본 HEAD·branch·전체 refs·clean 상태와 tracked 파일 **94개 hash**, 기존 두 released 기록의 **57개 파일 hash**가 유지됐다. 일반 CLI 11회는 모두 비승격이며 50ms consent-process 표본은 0이다. 별도 관리자 Update와 보호된 오류 기록의 읽기 전용 진단은 일반 CLI 호출 수에 포함하지 않는다.

처음 [A 폐기](../../artifacts/workspace-discard/product-20261001-180005-293/trial-a-discard.json)는 `StorageDiscardNotAuthorized`로 실패했다. delta 삭제 직후 최신 `DeltaPresent=false` 관측이 메모리에만 있고 durable 상태에는 이전 `true`가 남아 서비스가 정상 거부했다. [보호된 A 기록](../../artifacts/workspace-discard/product-20261001-180005-293/native-authority-failure/)에는 privileged Release intent 자체가 없다. 실패 뒤의 일반 실패 기록은 최신 관측을 저장했으므로 그 후 읽기 전용 권한 대조만으로 최초 실패를 재현하지 못한다.

`DiscardPublishesResourceAbsenceBeforeSnapshotAuthority`에서 별도 reader가 요청 시점의 durable 상태를 읽어 [수정 전 실패](../../artifacts/workspace-discard/product-20261001-180005-293/regression-before-fix/Splinter.Tests_net10.0_x64.trx)를 재현했다. Runtime이 최신 부재/zero 관측을 `discard-snapshot-authorized`로 게시한 뒤 요청하도록 수정했다. 게시 실패는 privileged 요청 전에 중단한다. 서비스의 검사·IPC 계약·설치 패키지는 변경하거나 재적용하지 않았다. 실패한 A의 ID·digest·intent와 같은 Release request ID로 재개했으며 새 A/snapshot으로 바꾸지 않았다.

수정 뒤 필수 `pwsh ./scripts/build.ps1`은 locked restore·Release 경고/오류 0, **745개 중 744 성공·0 실패·당시 제외 시험 미실행 1**이다. [수정 후보 hash](../../artifacts/workspace-discard/product-20261001-180005-293/source-after-observation-fix.json)·[자동 실행 manifest](../../artifacts/workspace-discard/product-20261001-180005-293/automated-after-observation-fix.json)·[TRX](../../artifacts/workspace-discard/product-20261001-180005-293/automated-after-observation-fix.trx)·[로그](../../artifacts/workspace-discard/product-20261001-180005-293/build-after-observation-fix.log)를 고정했고 native 실행 후에도 코드 hash가 일치했다. 기존 743 성공은 서비스 패키지 준비 후보의 별도 근거다.

이번 자격은 dirty 파일·서비스 거부 후 같은 요청 재개·완료 요청 중복·peer 지속과 실제 회수에 한정한다. 컴파일 실패/Editor stop·native 강제 중단·재부팅·전원·물리 공간·ARM·대형 프로젝트와 전체 비용 자격은 승계하지 않는다. 다음 단위는 최소 AI 사용 지침과 대표 사용 흐름이며 새 장애 행렬을 열지 않는다.

## 공통 수명주기 근거

`SnapshotWorkspaceCoordinatorTests`의 폐기 적합성은 다음 관문을 확인한다.

- 실패한 compile·dirty 입력을 checkpoint/source collection 없이 폐기하고, peer 작업과 Editor가 계속 유지됨.
- 앱 stop·unmount·source·delta·snapshot의 응답 유실 뒤 새 coordinator가 동일 intent만 재개. 편집/보존형 release의 교차 실행과 다른 intent는 거부.
- provider 미연결과 잘못된 대상 digest는 intent 저장·정지 전에 거부. 폐기 seal/source revision 변경·손상/누락된 owner 필드는 보존.
- 앱 자식·unknown 핸들/메모리는 0으로 읽지 않음. 공유 workspace·실제 Runtime의 candidate/source generation/cache 기록은 정지 전에 차단과 다음 행동을 반환.
- 지연 snapshot 회수는 완료와 구별하고 이미 처리한 source/delta를 다시 만들지 않음. 입력·state·응답 schema와 구조화된 오류의 보존 정책 확인.

시험은 기존 in-process fake와 각 시험의 새 임시 home을 사용한다. 외부 프로세스 강제 종료나 새 장애 harness는 추가하지 않는다. 이 공통 실행만으로 실제 worker 삭제·native VSS 회수를 통과로 표시하지 않는다. 후속 provider 후보의 Git/파일·authorization 근거는 위 범위와 구분한다. 일반 폐기를 위해 기존 release의 결과 gate를 완화하지 않는다.

## 실행 근거와 판정

2026-10-01 `development @ af94ea6`의 미커밋 후보에서 공통 단위를 **scoped-complete-with-limitations**로 판정했다. `pwsh ./scripts/build.ps1`의 locked restore·Release는 경고 0·오류 0이며 자동 시험은 총 719 중 성공 718·실패 0·당시 제외 시험 미실행 1이다. [고정 소스 hash](../../artifacts/workspace-discard/final-20261001-170615-205/source.json), [실행 manifest](../../artifacts/workspace-discard/final-20261001-170615-205/automated.json), [TRX](../../artifacts/workspace-discard/final-20261001-170615-205/automated.trx), [전체 로그](../../artifacts/workspace-discard/final-20261001-170615-205/build.log)는 로컬 근거다.

새 폐기 사례 13개와 기존 coordinator/Windows process 사례를 합친 [한정 실행 54개](../../artifacts/workspace-discard/final-20261001-165923-391/targeted.trx)도 통과했다. 최초 peer 준비 순서와 시험용 응답의 누락 digest는 fixture를 수정했고 [초기 실패](../../artifacts/workspace-discard/initial-fixture-failures/automated.trx)와 [응답 schema 실패](../../artifacts/workspace-discard/targeted-missing-response-digest.trx)를 보존했다.

기존 process 시험은 병렬 전체 실행에서 [호출자 pipe의 5초 관문 타임아웃](../../artifacts/workspace-discard/pre-reclamation-gate.trx)이 한 번 있었고 한정 실행에서는 통과했다. 상속 가능한 process-wide handle이 다른 시험의 프로세스 생성과 겹치지 않도록 이 시험을 전용 비병렬 collection으로 격리했다. 제품 launch/회수와 5초 assertion은 변경하지 않았다. 실제 타임아웃 때 handle을 소유한 PID는 미확정이며 이 후보의 전체 회귀 통과와 구분한다.

VSS 서비스 설치/업데이트·grant, 실제 workspace 폐기·Unity·VIVOBOOK·전원 장애는 이 공통 단위에 포함하지 않았다. 이 공통 시험만으로 M0-04/SW의 실제 상태를 승격하지 않았으며, 이후 로컬 실행의 Partial은 위 별도 근거를 따른다.
