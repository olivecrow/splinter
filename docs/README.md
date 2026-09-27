# 문서 지도

현재 문서는 내부 알파 runtime과 초기 기반을 함께 설명한다. 실행 작업공간은 [알파 작업공간](alpha-workspaces.md), 구조와 계약은 [현재 기반](foundation.md), 목표와 미완료 검증은 계획 문서에서 확인한다.

| 문서 | 읽을 때 |
| --- | --- |
| [Offline replay 계측](replay-profiling.md) · [RI 범위](plans/replay-index-cost.md) · [검증](verification/replay-index-cost.md) | 연산별 index 비용과 고정 합성 입력의 시간·관리 메모리 할당·동일 상태를 비교할 때. 실제 Unity·VSS 경로와 구분하는 개발용 도구 |
| [첫 준비·재연결 시간 범위](plans/validation-view-readiness-and-replay.md) · [검증](verification/validation-view-readiness-and-replay.md) | 정상 첫 import/렌더, 같은 저널의 재생 읽기·검증·index 시간, 실제 mount 준비 응답과 저장량 기준을 확인할 때. 이전 VC와 구분하는 작은 후속 범위 |
| [불변 후보·고정 검증 view](validation-view.md) · [VC 범위](plans/immutable-validation-view-first-milestone.md) · [검증](verification/immutable-validation-view.md) | Editor 없는 worker의 범위 후보, 같은 경로·Library의 정상 stop/remount/restart A/B/A, 생성·재개·전환 시간과 저장량을 확인할 때. 작은 범위 마감·첫 import 복구/첫 A 미리보기/긴 replay 제한 유지. 개발용 spike이며 제품 모드·source 반환 병합·큐 연결은 미구현 |
| [SF-02 예산 제품 검증·반복 저장량 범위](plans/journal-budget-and-growth-qualification.md) · [실행 결과](verification/journal-budget-and-growth-qualification.md) | BQ-01~05의 실제 예산 거부·같은 작업 재시도·Unity 수정 3회와 보존/회수, 유효·대체 기록 및 별도 파일 할당 장부, 다음 SF-03 후보 선정 근거를 확인할 때. 정의한 범위 완료·운영 개입/공간 미계측 제한 유지 |
| [반복 UAC 없는 저장소 권한 경계](plans/storage-privilege-service.md) | 최초 설치 승인 후 일반 CLI의 생성·재연결·회수에서 반복 UAC를 없애는 미구현 서비스 범위와 UA-01~04를 확인할 때 |
| [스냅샷 지연 회수 1차](plans/deferred-snapshot-reclamation.md) / [검증](verification/deferred-snapshot-reclamation.md) | 결과 보존과 회수 대기, 같은 workspace의 명시적 재시도, helper 응답 재개와 공유 볼륨 공간 관측 범위를 확인할 때 |
| [SF-02 저널 공간 정리 1차](plans/journal-compaction-first-milestone.md) / [검증](verification/journal-compaction.md) | 명시적 NTFS 저널 정리의 확정 범위, 자동 중단 재개·실제 할당·Unity 제한 회귀와 남은 지원 경계를 확인할 때 |
| [SF-02 Unity·전체 공간 비용 범위](plans/journal-compaction-qualification-and-cost.md) / [검증](verification/journal-compaction-qualification-and-cost.md) | 빈 폴더 WinFsp 열거와 작은 Unity 재개·로드 코드, 추가 공간 peak·단계별 비용·결과 회수의 실제 판정과 제한을 확인할 때 |
| [SF-02 명시적 정리 중첩량 예산](plans/journal-compaction-peak-admission.md) / [검증](verification/journal-compaction-peak-admission.md) | 후보 쓰기 전 예상 저널 중첩량 예산과 거부·같은 작업 재시도 계약, 물리 peak와의 경계를 확인할 때 |
| [공통 저장소 개선 1차](plans/storage-foundation-first-milestone.md) | 확정 범위·완료 조건과 구현 결과, 기존 VSS 보존 실패로 남은 마감 조건을 확인할 때 |
| [변경 기반 소스 검증과 계측](source-verification.md) | 파일 hash/manifest·provider 세대·audit 복구·계수와 읽기 전용 물리 할당 계측 스크립트를 사용할 때 |
| [공통 저장소 실제 검증](verification/storage-foundation.md) | SF-01·SF-02 하위 결과, 읽기/공간 장부·Unity A/B·기존 VSS 소실과 자동 해제 차단의 미검증 경계를 확인할 때 |
| [저장소 실패 재현·보호 검증](verification/storage-recovery.md) | 제어 JSON 게시 충돌 수정, 격리 VSS 연쇄 삭제 재현·비강제 API 대조·실제 삭제 차단과 회수 순서의 제한을 확인할 때 |
| [저장공간 우선 설계와 Editor 방식](plans/storage-priority-and-editor-modes.md) | 용량 1순위·Editor 동시성 2순위, SF-01의 제한된 검증과 SF-02~06의 남은 조건·단일 Editor 후보를 확인할 때 |
| [NTFS 작업공간](ntfs-workspaces.md) | 일반 CLI의 ntfs-snapshot 생성·권한 경계·재개·독립 결과·회수와 현재 제한을 확인할 때 |
| [NI 통합 검증](verification/ntfs-workspace-integration.md) | 실제 NTFS/Git/Unity·범위 저장·장애 보호 증거, 원본 통합 차단·최종 자원 회수와 긴 경로 제한을 확인할 때 |
| [NTFS workspace 1차 통합 범위](plans/ntfs-workspace-integration.md) | 완료한 NI-01~08의 계약·CLI·복구·결과·회수 범위와 완료 기준을 확인할 때 |
| [NTFS 내부 알파 2차 계획](plans/ntfs-recovery-and-support.md) | NR-01~08 전체의 복구·재부팅·파일 지원 경계, 격리 Windows 시험 자원과 이후 에셋·Play·테스트 순서를 확인할 때 |
| [독립 대화 A/B·비용 범위](plans/parallel-workflow-qualification.md) | PV-01/02의 실행 순서, Codex 기본 worktree 직접 비교·실제 모델/텍스처·Library 비용, checkpoint/통합의 현재 제한과 완료 기준을 확인할 때 |
| [독립 대화·Codex worktree 비교 기록](verification/parallel-workflow.md) | 독립 Editor C/S와 Coordination 단일 Editor Q의 실제 Unity 작업·에셋·비용 계측, PV-01의 복구 포함 대표 수명주기 완료와 PV-02 선택 범위 마감 및 실패·입력·표본 제한을 확인할 때 |
| [live VSS 식별과 재개 범위](plans/ntfs-live-vss-identity.md) | 완료한 NR-01 하위 작업의 계약·순서·LV-01~08 완료 기준과 후속 장애 시험 경계를 확인할 때 |
| [LV 식별·재연결 검증](verification/ntfs-live-vss-identity.md) | 새 후보의 자동 시험, 실제 VSS/Git 재연결·범위 저장과 단일 Unity 실행·회수 증거를 확인할 때 |
| [NR 복구 검증 기록](verification/ntfs-recovery.md) | 복구 계약의 자동 증거, VIVOBOOK VSS 회수 재개·실제 mount의 파일 API 거부·x64 Unity 단일 회귀와 남은 장애 조건을 확인할 때 |
| [검토 후 수정 검증](verification/workspace-review-fixes.md) | 수집 중 쓰기 보존·일반 status의 소스 읽기 생략·Git/Unity 공통 mount 접근 규칙과 자동/Git 시험의 범위를 확인할 때 |
| [NTFS snapshot-overlay 실험](snapshot-overlay.md) | VSS·WinFsp·범위 저널의 현재 실험 계약, 실행·재연결과 비용 제한을 확인할 때 |
| [SO 실제 실행 기록](verification/snapshot-overlay.md) | SO-01~06의 현재 상태와 원시 증거, 무효 처리된 측정을 확인할 때 |
| [NTFS 실험 계획과 통합 조건](plans/ntfs-snapshot-overlay-spike.md) | SO-01~06의 의도·완료 조건과 제품 통합 전에 남은 검증을 확인할 때 |
| [현재 기반](foundation.md) | 실제 코드 구조, 구현된 CLI 계약, 브랜치 운영, 빌드·시험 방법과 제한을 확인할 때 |
| [알파 작업공간](alpha-workspaces.md) | Unity 작업공간 편집·검증·통합·회수와 복구 흐름을 사용할 때 |
| [저장소·자원 관리](storage-and-resources.md) | 기준본 예열·ReFS CoW·clone/copy 바이트·프로세스 그룹·메모리 회수 계약을 확인할 때 |
| [자원 검증](verification/storage-and-resources.md) | 실제 Windows/ReFS/Unity 증거와 다음 검증 관문을 확인할 때 |
| [ReFS 시험 자원 정리](verification/storage-cleanup.md) | 시험 디스크·기준본 회수와 보존한 증거, 새 세션의 실제 fixture 경로를 확인할 때 |
| [실제 알파 검증](verification/internal-alpha.md) | Luna low 두 작업자의 GUI Editor 격리·재개·통합·회수 증거와 검증 한계를 확인할 때 |
| [원본 기획서](../plan.html) | 개정 5판의 대화별 사용·설치·명시적 완료/폐기 요구사항, 현재 구현·SO/NI 완료 범위, 후속 검증·구현 우선순위를 확인할 때 |
| [대화별 작업·설치 계획](plans/session-workflow-and-installation.md) | 프로젝트용/전역용 설치·Codex 스킬/플러그인 연결, 필요 시 사용·미사용, 독립 대화 재개·반영/보존/폐기와 SW 관문을 확인할 때 |
| [구현 계획](plans/implementation-plan.md) | 기술 선택, 모듈 책임, CLI·상태 계약, 단계별 구현 작업을 확인할 때 |
| [검증 추적표](plans/validation-matrix.md) | 기획 요구사항의 구현 단계와 필요한 검증 증거를 확인할 때 |

새 모듈이 생기면 현재 계약 문서를 추가하고 이 표에서 연결한다. 계획의 제안과 구현된 동작이 다르면 둘을 함께 갱신하고 상태를 명시한다.

`_change_logs/`에는 실제 기능 변경 이력을 JSONL로 기록한다.
