# 문서 지도

현재 구현은 아래 기능 문서에서, 축소한 후속 책임은 계획 문서에서 확인한다. 계획은 프로그램 구현·AI 사용 절차·필요 시 지원 검증·구현 제외를 구분한다. 검증 기록은 지정한 입력·후보의 근거이며 현재 코드의 자동 통과 선언이나 미완성 기능의 수가 아니다.

## 현재 계약과 사용법

| 문서 | 소유하는 내용 |
| --- | --- |
| [현재 기반](foundation.md) | 모듈·의존성·명령·빌드와 시험 |
| [개발용 CLI](development-cli.md) | 기본 CLI에서 분리한 ReFS/baseline·diagnostic checkout·저장소 view·spike/probe의 진입점 |
| [한정 버전 AI 시작 안내](agent-start.md) | 개발 CLI의 전제·진입·정확한 경로/같은 작업 재개·명시적 종료·실패의 다음 행동 |
| [최소 AI 사용 스킬](../skills/splinter/SKILL.md) | 저장소 지침 원본·직접/독립 작업 판단·CLI 발견·반환 경로·같은 작업 재개·명시적 종료/실패 처리. 설치/자동 binding은 없음 |
| [VCS 없는 작업공간](vcs-independent-workspaces.md) | 명시적 소스/application 선택·파일 기준/결과·현재 형식·회수와 실제 검증의 구분 |
| [일반 폴더 원본 반영](plain-folder-original-apply.md) | 읽기 전용 계획·고정 대상/intent·조건부 Windows 게시·중단/회수와 비용 경계 |
| [원본 반영 검증](verification/plain-folder-original-apply.md) | Core/fake·실제 일반 권한 NTFS 파일 시험과 제품 A/B 보류·한정 계측 |
| [VCS 독립 검증](verification/vcs-independent-workspaces.md) | 자동 파일/fake·Git 회귀와 실제 A/B·서비스 업데이트·동일 요청 회수 복구의 한정 판정/비용 |
| [일반 폴더 실제 실행 계획](plans/vcs-independent-validation.md) | 고정 fixture·서비스 업데이트/grant의 검토 가능한 계획·한 A/B 묶음과 중단 조건 |
| [NTFS 작업공간](ntfs-workspaces.md) | VSS identity·범위 저널·Git·권한·제어 파일 게시/읽기·복구·결과 보존·회수 |
| [명시적 폐기](workspace-discard.md) | Core/Runtime intent·Git/Windows provider·CLI·서비스 별도 authorization·중단 재개 |
| [Windows 저장소 권한 서비스](storage-privilege-service.md) | 설치 범위·고정 VSS IPC·사용자/경로/소유권·보호된 재시도 상태 |
| [Unity 알파 작업공간](alpha-workspaces.md) | 외부 도구로 편집·검증, 같은 작업 재개·독립 결과·통합과 Editor 수명 |
| [저장소와 자원](storage-and-resources.md) | 선택적 ReFS 기준본·프로세스 그룹·자원 관측 |
| [실행 파일 묶음의 메모리 검증](verification/runner-bundle-memory.md) | 동일 빌드의 디스크 재사용·스트림 준비의 관리 할당·손상/소실 거부·동시 게시와 실제 프로세스 회귀 |
| [저널 정리 크기 회귀](verification/journal-compaction-size.md) | 작은 덮어쓰기의 메타데이터 증가 재현·더 작은 frame 선택·같은 입력의 NTFS 할당 비교 |
| [동일 private 쓰기](verification/duplicate-private-writes.md) | 승인된 private 범위의 중복 payload 억제·메타데이터/flush 유지·NTFS 할당과 작은 WinFsp client 검증 |
| [작은 Unity 동일 쓰기 공간 비교](verification/unity-duplicate-private-writes.md) | 고정 Runtime의 warm 편집/재개·Library/저널 분류·실제 파일 할당·동일 독립 결과와 회수·작은 관측 차이의 한계 |
| [부분 쓰기·버퍼·시작 개선](verification/resource-efficiency-followup.md) | private 덮어쓰기의 일치 앞뒤 재사용·요청 크기 버퍼·시작/소스 검증 분리와 고정 입력·로컬 Unity 자격 |
| [소스 검증](source-verification.md) | 변경 경로·hash cache·status freshness·현재 계측 형식 |
| [불변 후보와 검증 view](validation-view.md) | 개발용 후보 게시·같은 요청 중단 복구·검증된 Git 포인터 분리·고정 view·공통 Library |
| [개발용 소스 view 수명주기](validation-workflow.md) | 개발 CLI의 view·snapshot Library·정지 cache 정리·독립 결과·회수 의존성. Unity 검증은 외부 도구 책임 |
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
| [대표 AI A/B 사용·비용 검증](verification/representative-agent-workflow.md) | 독립 Editor의 오류/수정·동일 작업 재개·보존/통합/release와 실패 작업 discard·단계별 파일 할당/시간·재시도 제한 |
| [한정 M0 납품 판정](verification/m0-delivery.md) | 현재 복구 회귀·기존 native 소스 대조와 재사용 한계·실제 시작 명령 확인·한정 완료와 지원 확대의 구분 |
| [상태 쓰기 진단 검증](verification/state-write-diagnostics.md) | workspace 저장 단계·게시 완료 여부·실제 파일 잠금과 기계용 오류 계약의 자격 |

## 남은 설계와 검증

| 문서 | 용도 |
| --- | --- |
| [제품 요구사항](../plan.html) | 제품이 달성할 목표 |
| [구현 계획](plans/implementation-plan.md) | 프로그램/에이전트 책임·VCS 무관한 제품 요구·다시 확정한 일반 폴더 기본 수명주기와 원본 반영·외부 VCS 호환 처리의 후속 순서·마감한 M0 |
| [현재 범위: 일반 폴더 원본 반영](plans/plain-folder-original-apply.md) | 명시적 대상·기준/결과/현재 원본 비교·조건부 게시·동일 intent/부분 수행 복구·Editor 없는 NTFS A/B 한 묶음과 제외/종료 기준. 구현·일반 권한 파일 검증과 실제 제품 A/B NotRun을 구분 |
| [원본 반영 제품 A/B 준비](plans/plain-folder-original-apply-validation.md) | 새 작은 입력·seed·현재 서비스/등록·보류한 관리자 Apply와 한 실행 묶음 |
| [검증 추적표](plans/validation-matrix.md) | M0와 기존 ID의 실제 증거 상태·후속 책임 분류·보존한 48개 카탈로그 |
| [NTFS 복구·지원](plans/ntfs-recovery-and-support.md) | NR-01/02 준비 복구·NR-03 공통 저널/로컬 kernel/제품 client 경계와 재부팅·전원·공간·파일 관문의 완료 기준 |
| [저장공간 우선 설계](plans/storage-priority-and-editor-modes.md) | SF-02~06·비용 판단과 WF-09~12/LB-01~05의 완료 범위·재검증 기준 |
| [대화별 설치·사용](plans/session-workflow-and-installation.md) | AI의 설치 연결/인계/도구 선택/종료 절차·외부 도구의 최소 경계·[후속 Unity CLI A/B 인계 범위](plans/session-workflow-and-installation.md#후속-범위-unity-cli의-ab-인계)·SW 증거 분류 |
| [저장소 권한 서비스](plans/storage-privilege-service.md) | 반복 UAC 없는 제한 서비스의 UA-01~04 |
| [병렬 비교 방법](plans/parallel-workflow-qualification.md) | C/S/Q 입력·작업·비용 비교를 재사용할 때 |

변경 이력은 Git에서 확인한다. 완료한 단계별 계획을 다시 유지하지 않는다. 후속 성능·지원 판단에 필요한 실측은 verification에 보존하며 README·AGENTS에 복제하지 않는다.

첫 버전의 실행 범위는 M0로 확정하고 NR/SF/SW 전체 미완료를 첫 버전 완료의 분모로 사용하지 않는다. [제품 범위 정리 검증](verification/workspace-product-scope.md)은 삭제한 DAG/Unity 검증 진입점·기본/개발 CLI 분리와 실제 자동 회귀의 경계를 소유한다.
