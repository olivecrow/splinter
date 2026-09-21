# 문서 지도

현재 문서는 내부 알파 runtime과 초기 기반을 함께 설명한다. 실행 작업공간은 [알파 작업공간](alpha-workspaces.md), 구조와 계약은 [현재 기반](foundation.md), 목표와 미완료 검증은 계획 문서에서 확인한다.

| 문서 | 읽을 때 |
| --- | --- |
| [다음 작업: NTFS 스냅샷·변경분 검증](plans/ntfs-snapshot-overlay-spike.md) | 원본의 계속된 수정, 최초 전체 복제 금지, 범위별 쓰기와 SO-01~06의 의도·순서·완료 조건을 확인할 때 |
| [현재 기반](foundation.md) | 실제 코드 구조, 구현된 CLI 계약, 브랜치 운영, 빌드·시험 방법과 제한을 확인할 때 |
| [알파 작업공간](alpha-workspaces.md) | Unity 작업공간 편집·검증·통합·회수와 복구 흐름을 사용할 때 |
| [저장소·자원 관리](storage-and-resources.md) | 기준본 예열·ReFS CoW·clone/copy 바이트·프로세스 그룹·메모리 회수 계약을 확인할 때 |
| [자원 검증](verification/storage-and-resources.md) | 실제 Windows/ReFS/Unity 증거와 다음 검증 관문을 확인할 때 |
| [ReFS 시험 자원 정리](verification/storage-cleanup.md) | 시험 디스크·기준본 회수와 보존한 증거, 새 세션의 실제 fixture 경로를 확인할 때 |
| [실제 알파 검증](verification/internal-alpha.md) | Luna low 두 작업자의 GUI Editor 격리·재개·통합·회수 증거와 검증 한계를 확인할 때 |
| [원본 기획서](../plan.html) | 제품 요구사항, 범위, 설계 근거를 확인할 때 |
| [구현 계획](plans/implementation-plan.md) | 기술 선택, 모듈 책임, CLI·상태 계약, 단계별 구현 작업을 확인할 때 |
| [검증 추적표](plans/validation-matrix.md) | 기획 요구사항의 구현 단계와 필요한 검증 증거를 확인할 때 |

새 모듈이 생기면 현재 계약 문서를 추가하고 이 표에서 연결한다. 계획의 제안과 구현된 동작이 다르면 둘을 함께 갱신하고 상태를 명시한다.

`_change_logs/`에는 실제 기능 변경 이력을 JSONL로 기록한다.
