# 제품 범위와 명령 발견의 검증

첫 버전의 범위·다음 순서·검증 종료 조건은 [M0 계획](../plans/implementation-plan.md), 실제 사용 흐름의 상태는 [M0 추적표](../plans/validation-matrix.md#첫-실사용-버전-m0)가 소유한다. 이 문서는 작업 그래프를 제외한 현재 발견/거부 계약과 자동 회귀의 경계만 설명한다.

## 현재 계약

- `capabilities`와 commands catalog에는 `plan.validate`가 없다. plan-request schema와 DAG 검사기·예제는 제공하지 않는다. workspace의 생성·같은 작업 재개·명시적 종료를 각 대화가 직접 다룬다.
- `plan validate --input <경로>`는 입력 파일을 열거나 실행 경로로 연결하지 않고 `InvalidArguments`와 exit 2를 반환한다. `schema plan.validate`는 `UnknownCommand`와 exit 9, capability 확인의 다음 행동을 반환한다. 현재 버전에서 이전 명령을 호환 실행하지 않는다.
- `capabilities.unavailable`에는 현재 미구현인 `workspace.discard`, `automatic-conversation-binding`, `product-installation`을 표시한다. 이 목록은 실행 명령 등록이 아니며 AI Runner·Unity 저작/Play/Test API를 향후 제공하겠다는 약속도 아니다.
- Runtime·Windows storage·Git·Unity·Library/cache의 상태와 부작용 계약은 유지한다. OS 자식 프로세스를 소유·감독하는 내부 프로그램과 기존 실패 재현 도구는 제외한 AI 작업 Runner와 다르다.

## 자동 회귀와 제한

`ContractAndCliTests.CapabilitiesOnlyAdvertiseImplementedCommands`, `RemovedTaskGraphEntryPointsReturnStructuredErrors`, `SchemasAreAvailableWithoutAnyHostOrWorkingDirectoryState`는 발견·현재 schema·제외한 진입점의 구조화된 응답을 확인한다. `InvalidInputCannotLeakParserTextIntoStdout`는 제품 `validation --input`에서 손상 JSON과 잘못된 요청을 부작용 전 거부하는 공통 JSON 응답 검사를 유지한다. DAG 자체의 시험은 현재 제품의 검증 대상이 아니다.

필수 실행은 `pwsh ./scripts/build.ps1`의 locked restore·Release·전체 자동 시험이다. 이 변경의 자동 회귀는 실제 VSS·WinFsp·Unity·ARM·재부팅 지원이나 M0 사용 흐름의 Passed를 뜻하지 않는다. 원본/타 작업·승인 쓰기·회수 보호 시험과 기존 각 후보의 제한 근거는 유지한다. 작업 그래프의 과거 hash를 참조하는 원시 evidence와 독립 결과 refs는 역사적 근거로 보존한다.

2026-10-01 Windows x64에서 `development @ 9ec88e6`의 미커밋 변경을 검증했다. locked restore·Release 빌드는 경고 0·오류 0, 자동 시험은 총 706개 중 성공 705·실패 0·선택적 실제 ReFS 미실행 1이다. [소스 hash·실행 manifest](../../artifacts/workspace-product-scope/20261001-154027-033/automated.json)와 [보존한 TRX](../../artifacts/workspace-product-scope/20261001-154027-033/automated.trx)는 로컬 artifacts다. 일반 sandbox의 restore는 상세 진단 없이 실패했고 동일 필수 검증은 sandbox 밖에서 성공했다. 서비스 설치·grant·Unity/VSS 실행·VIVOBOOK 시험은 이 변경에 포함하지 않는다.
