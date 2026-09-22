# 문서 지도

현재 문서는 내부 알파 runtime과 초기 기반을 함께 설명한다. 실행 작업공간은 [알파 작업공간](alpha-workspaces.md), 구조와 계약은 [현재 기반](foundation.md), 목표와 미완료 검증은 계획 문서에서 확인한다.

| 문서 | 읽을 때 |
| --- | --- |
| [NTFS 작업공간](ntfs-workspaces.md) | 일반 CLI의 ntfs-snapshot 생성·권한 경계·재개·독립 결과·회수와 현재 제한을 확인할 때 |
| [NI 통합 검증](verification/ntfs-workspace-integration.md) | 실제 NTFS/Git/Unity·범위 저장·장애 보호 증거, 원본 통합 차단·최종 자원 회수와 긴 경로 제한을 확인할 때 |
| [NTFS workspace 1차 통합 범위](plans/ntfs-workspace-integration.md) | 완료한 NI-01~08의 계약·CLI·복구·결과·회수 범위와 완료 기준을 확인할 때 |
| [NTFS 내부 알파 2차 계획](plans/ntfs-recovery-and-support.md) | NR-01~08 전체의 복구·재부팅·파일 지원 경계, 격리 Windows 시험 자원과 이후 에셋·Play·테스트 순서를 확인할 때 |
| [live VSS 식별과 재개 범위](plans/ntfs-live-vss-identity.md) | 이번 NR-01 중심 작업의 계약·순서·LV-01~08 완료 기준과 후속 장애 시험 경계를 확인할 때 |
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
