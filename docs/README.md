# 문서 지도

현재 구현은 아래 기능 문서에서, 미구현 목표는 계획 문서에서 확인한다. 검증 기록은 지정한 입력·후보의 근거이며 현재 코드의 자동 통과 선언이 아니다.

## 현재 계약과 사용법

| 문서 | 소유하는 내용 |
| --- | --- |
| [현재 기반](foundation.md) | 모듈·의존성·명령·빌드와 시험 |
| [최소 AI 사용 스킬](../skills/splinter/SKILL.md) | 저장소 지침 원본·직접/독립 작업 판단·CLI 발견·반환 경로·같은 작업 재개·명시적 종료/실패 처리. 설치/자동 binding은 없음 |
| [NTFS 작업공간](ntfs-workspaces.md) | VSS identity·범위 저널·Git·권한·제어 파일 게시/읽기·복구·결과 보존·회수 |
| [명시적 폐기](workspace-discard.md) | Core/Runtime intent·Git/Windows provider·CLI·서비스 별도 authorization·중단 재개 |
| [Windows 저장소 권한 서비스](storage-privilege-service.md) | 설치 범위·고정 VSS IPC·사용자/경로/소유권·보호된 재시도 상태 |
| [Unity 알파 작업공간](alpha-workspaces.md) | 여러 차례 편집·검증·재개·통합과 Editor 수명 |
| [저장소와 자원](storage-and-resources.md) | 선택적 ReFS 기준본·프로세스 그룹·자원 관측 |
| [소스 검증](source-verification.md) | 변경 경로·hash cache·status freshness·현재 계측 형식 |
| [불변 후보와 검증 view](validation-view.md) | 개발용 후보 게시·같은 요청 중단 복구·검증된 Git 포인터 분리·고정 view·공통 Library |
| [검증 후보 수명주기](validation-workflow.md) | 제품 validation CLI·Windows/Git/Unity 조합·snapshot Library·정지 cache 정리·독립 결과·회수 의존성 |
| [Unity 미리보기](unity-render-qualification.md) | fixture의 요청별 PNG·픽셀·해시·실패/복구 조건 |
| [Snapshot overlay 시험](snapshot-overlay.md) | 개발용 준비·mount·파일 API·장애·결과·회수 스크립트 |
| [준비 중단 절차](ntfs-workspaces.md#개발용-준비-중단-시험), [준비 복구 검증](verification/preparation-recovery.md) | 개발용 gate/harness와 같은 요청의 실제 CLI 종료·재개·원본 보존·회수 자격 |
| [Replay probe](replay-profiling.md) | 고정 합성 입력의 replay 비용 |
| [Write probe](write-profiling.md) | 고정 합성 쓰기·flush·대기 비용 |
| [저널 중단 시험](journal-recovery.md) | 합성 불변 기준의 58개 실제 자식 종료·외부 ACK·불완전 tail·재생 관문 |
| [로컬 kernel 복구 시험](kernel-recovery.md) | 실제 WinFsp/fake 기준의 callback·mmap·동시 reader·같은 경로 재연결 개발 관문 |
| [제품 쓰기 복구 시험](product-write-recovery.md) | 일반 VSS/WinFsp/Git CLI의 외부 client 경계·동일 작업 재연결·독립 보존/회수 개발 관문 |
| [검증 안내](verification/README.md) | 지원 경계, 검증 진입점, 보존한 실측 근거 |
| [최소 AI 작업 연결 검증](verification/agent-workflow.md) | source skill 직접 사용·반환 경로 편집·새 CLI 재개·direct/명시적 폐기 한정 실행 |
| [상태 쓰기 진단 검증](verification/state-write-diagnostics.md) | workspace 저장 단계·게시 완료 여부·실제 파일 잠금과 기계용 오류 계약의 자격 |

## 남은 설계와 검증

| 문서 | 용도 |
| --- | --- |
| [제품 요구사항](../plan.html) | 제품이 달성할 목표 |
| [구현 계획](plans/implementation-plan.md) | 첫 실사용 버전 M0·폐기/최소 AI 연결의 실행 순서·제거 경계·검증 종료 규칙 |
| [검증 추적표](plans/validation-matrix.md) | M0의 실제 상태와 장기 48개 지원·확장 카탈로그 |
| [NTFS 복구·지원](plans/ntfs-recovery-and-support.md) | NR-01/02 준비 복구·NR-03 공통 저널/로컬 kernel/제품 client 경계와 재부팅·전원·공간·파일 관문의 완료 기준 |
| [저장공간 우선 설계](plans/storage-priority-and-editor-modes.md) | SF-02~06·비용 판단과 WF-09~12/LB-01~05의 완료 범위·재검증 기준 |
| [대화별 설치·사용](plans/session-workflow-and-installation.md) | 최소 사용 지침과 대표 A/B, 후속 설치/자동 binding의 SW-01~10 |
| [저장소 권한 서비스](plans/storage-privilege-service.md) | 반복 UAC 없는 제한 서비스의 UA-01~04 |
| [병렬 비교 방법](plans/parallel-workflow-qualification.md) | C/S/Q 입력·작업·비용 비교를 재사용할 때 |

변경 이력은 Git에서 확인한다. 완료한 단계별 계획을 다시 유지하지 않는다. 후속 성능·지원 판단에 필요한 실측은 verification에 보존하며 README·AGENTS에 복제하지 않는다.

첫 버전의 실행 범위는 M0로 확정하고 NR/SF/SW 전체 미완료를 첫 버전 완료의 분모로 사용하지 않는다. [제품 범위 정리 검증](verification/workspace-product-scope.md)은 삭제한 DAG 진입점·schema와 실제 자동 회귀의 경계를 소유한다.
