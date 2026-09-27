# 문서 지도

현재 구현은 아래 기능 문서에서, 미구현 목표는 계획 문서에서 확인한다. 검증 기록은 지정한 입력·후보의 근거이며 현재 코드의 자동 통과 선언이 아니다.

## 현재 계약과 사용법

| 문서 | 소유하는 내용 |
| --- | --- |
| [현재 기반](foundation.md) | 모듈·의존성·명령·빌드와 시험 |
| [NTFS 작업공간](ntfs-workspaces.md) | VSS identity·범위 저널·Git·권한·복구·결과 보존·회수 |
| [Unity 알파 작업공간](alpha-workspaces.md) | 여러 차례 편집·검증·재개·통합과 Editor 수명 |
| [저장소와 자원](storage-and-resources.md) | 선택적 ReFS 기준본·프로세스 그룹·자원 관측 |
| [소스 검증](source-verification.md) | 변경 경로·hash cache·status freshness·현재 계측 형식 |
| [불변 후보와 검증 view](validation-view.md) | 개발용 후보 게시·검증된 Git 포인터 분리·고정 view·공통 Library |
| [검증 후보 수명주기](validation-workflow.md) | 제품 연결 전 공통 계약·지속 상태·fake 적합성, 실제 adapter 미연결 |
| [Unity 미리보기](unity-render-qualification.md) | fixture의 요청별 PNG·픽셀·해시·실패/복구 조건 |
| [Snapshot overlay 시험](snapshot-overlay.md) | 개발용 준비·mount·파일 API·장애·결과·회수 스크립트 |
| [Replay probe](replay-profiling.md) | 고정 합성 입력의 replay 비용 |
| [Write probe](write-profiling.md) | 고정 합성 쓰기·flush·대기 비용 |
| [검증 안내](verification/README.md) | 지원 경계, 검증 진입점, 보존한 실측 근거 |

## 남은 설계와 검증

| 문서 | 용도 |
| --- | --- |
| [제품 요구사항](../plan.html) | 제품이 달성할 목표 |
| [구현 계획](plans/implementation-plan.md) | 목표 구조·책임·단계별 작업 |
| [검증 추적표](plans/validation-matrix.md) | 필수 48개 시나리오와 남은 지원 관문 |
| [NTFS 복구·지원](plans/ntfs-recovery-and-support.md) | 중단·재부팅·전원·공간·파일 경계의 완료 기준 |
| [저장공간 우선 설계](plans/storage-priority-and-editor-modes.md) | SF-02~06·단일 Editor 비교 기준·SF-03의 남은 제품 연결 관문 |
| [대화별 설치·사용](plans/session-workflow-and-installation.md) | 설치·binding·명시적 discard의 SW-01~10 |
| [저장소 권한 서비스](plans/storage-privilege-service.md) | 반복 UAC 없는 제한 서비스의 UA-01~04 |
| [병렬 비교 방법](plans/parallel-workflow-qualification.md) | C/S/Q 입력·작업·비용 비교를 재사용할 때 |

변경 이력은 Git에서 확인한다. 완료한 단계별 계획을 다시 유지하지 않는다. 후속 성능·지원 판단에 필요한 실측은 verification에 보존하며 README·AGENTS에 복제하지 않는다.
