# 문서 지도

현재 문서는 내부 알파 runtime과 초기 기반을 함께 설명한다. 실행 작업공간은 [알파 작업공간](alpha-workspaces.md), 구조와 계약은 [현재 기반](foundation.md), 목표와 미완료 검증은 계획 문서에서 확인한다.

| 문서 | 읽을 때 |
| --- | --- |
| [현재 기반](foundation.md) | 실제 코드 구조, 구현된 CLI 계약, 브랜치 운영, 빌드·시험 방법과 제한을 확인할 때 |
| [알파 작업공간](alpha-workspaces.md) | git-checkout 작업공간, Unity bridge, 새 Library와 복구 흐름을 사용할 때 |
| [실제 알파 검증](verification/internal-alpha.md) | Luna low 두 작업자의 GUI Editor 격리·재개·통합·회수 증거와 검증 한계를 확인할 때 |
| [원본 기획서](../plan.html) | 제품 요구사항, 범위, 설계 근거를 확인할 때 |
| [구현 계획](plans/implementation-plan.md) | 기술 선택, 모듈 책임, CLI·상태 계약, 단계별 구현 작업을 확인할 때 |
| [검증 추적표](plans/validation-matrix.md) | 기획 요구사항의 구현 단계와 필요한 검증 증거를 확인할 때 |

새 모듈이 생기면 현재 계약 문서를 추가하고 이 표에서 연결한다. 계획의 제안과 구현된 동작이 다르면 둘을 함께 갱신하고 상태를 명시한다.

`_change_logs/`에는 실제 기능 변경 이력을 JSONL로 기록한다.
