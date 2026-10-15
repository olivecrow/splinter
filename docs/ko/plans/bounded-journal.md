# 제품 제한 로그 구현

[English](../../plans/bounded-journal.md)

사용자는 기존 실험 커밋 후 원본·결과 안전성을 유지한 제한 로그의 제품 적용과 원자 게시가 가능한 Bee 구조 검토를 승인했다. 선행 실험 커밋은 `a8eb778`이다. 새 provider 기본값은 [제품 계약](../bounded-journal.md)이 소유하며 [Bee 설계](bee-native-storage-design.md)는 이 범위에서 검토에 한정했다. 후속 [BN 실험](../verification/bee-internal-native.md)은 별도로 구현·측정한 개발 후보이며 제품 활성화가 아니다.

| ID | 범위와 종료 근거 |
| --- | --- |
| BL-01 | 실제 SJR2 mutation/state 구현에 저장된 16MiB/4,096-frame suffix 제한, 개별 durable ACK, 전체 metadata·rename/delete/resize·열린 unlink identity·cache·checkpoint의 원본 payload 읽기 없음. |
| BL-02 | 같은 owner의 초기화/게시 복구, 손상·unknown generation 보존, 손상 거부·writer poison·정확한 inventory·독립 결과 생존. 예외 주입/프로세스 종료를 미실행 OS 장애와 구분. |
| BL-03 | Windows provider 기본값·명시/생략 compact 선택·schema/capability/fake 관측·안전한 회수와 변경 없는 Library 복원. 작은 실제 editorless A/B의 원본 불변·peer 지속·독립 결과·소유 종료. |
| BL-04 | 같은 입력 SJL4/제품 bounded의 직렬 구성 요소 측정. 초기화·자동 checkpoint·검증/재연결/결과/정리 비용 포함. RAM/공간의 구분을 기록하며 전체 Unity 절감 주장은 하지 않음. |
| BL-05 | 필수 locked restore·전체 Release build/test와 영/한 계약·자격 갱신, 과거 보고서 보존. |

작은 새 소유 입력을 사용한다. 기존 Unity Library 이동·Editor 시작·서비스/UAC/ACL·외부 패키지 변경·전체 미로 재실행·native Bee 구현은 하지 않는다. 시간 측정은 build/test와 겹치지 않는다. group commit·RAM 전용 ACK·백그라운드 checkpoint writer·소스 hydration·호환 이관은 포함하지 않는다.

suffix 제한은 checkpoint까지 이전 바이트와 임시 private 상태 중복을 허용한다. 상태 prefix의 이전 바이트가 16MiB로 제한되는 것은 아니다. 빈 범위를 채우려고 원본을 복사하지 않는다. 형식은 스스로 식별하며 unknown/지원하지 않는 이전 기록은 보존·거부한다. 새 기본값은 기존 작업의 형식을 자동 변경하지 않는다. 실제 종료와 한계는 [검증](../verification/bounded-journal.md)에 기록한다.

2026-10-15: scoped-complete-with-limitations. BL-01–05는 [검증 기록](../verification/bounded-journal.md)의 실제 editorless A/B·프로세스 종료 12경계·자동 검사 1,691 통과·동일 구성 요소 한 쌍으로 한정 완료했다. 전체 Unity와 Bee native backing은 이 완료 밖이다.
