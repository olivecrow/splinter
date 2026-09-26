# 스냅샷 지연 회수 1차 범위

[저장소 실패 재현](../verification/storage-recovery.md) 다음 실행 단위다. 결과 보존과 VSS 삭제 시점을 분리하고, 오래된 활성/비소유 snapshot 때문에 즉시 회수할 수 없는 작업을 명시한다. 공간이 최우선인 기존 목표와 NTFS 원본 유지·최초 전체 복제/읽기 hydration 금지·범위 변경 저장 조건을 유지한다.

## 포함 범위와 완료 조건

| 항목 | 완료 조건 |
| --- | --- |
| 독립 결과와 회수 대기 | 안정 저널 수집, 독립 Git 결과, 프로세스/메모리/핸들 0과 mount/등록/delta 회수 후에만 `reclaim-pending` 저장. snapshot이 남으면 release 완료를 표시하지 않음 |
| 명시적 재시도 | 같은 `workspace release --id --home`으로 재개. 새 CLI에서도 대기·차단 ID 유지, source 재개/재캡처/타 작업 삭제 없음. 결과와 소유 자원 조건을 재검증 |
| helper 응답 재개 | owner·전체 snapshot reference·현재 boot·요청 ID·관측 시각을 대조. 응답 유실 뒤 같은 시도 결과 회수, 이미 소비한 대기 결과의 재시도는 새 요청. 손상/다른 ID/불완전 증거는 보존 |
| 잔존 공간 관측 | 마지막 helper 관측 시각, 차단 snapshot ID, 해당 source volume의 VSS 사용/할당/한도. workspace별 귀속은 unknown이고 중복 합산 금지. 조회 실패는 0이 아닌 unknown |
| 작은 회귀 | fake A/B에서 B 대기 중 A 계속 사용, A의 명시적 회수 뒤 B의 명시적 재시도, 상태 재시작·unknown·결과 소실·schema 검증. 실제 격리 NTFS/Git fixture에서 같은 대기/재시도 경로와 최종 회수 확인 |

`workspace release`의 대기는 `Accepted`, `terminal=false`, `retryDisposition=requires-user-action`이다. 이는 백그라운드 작업 예약이 아니다. `status/list` 성공은 조회 성공이고 workspace의 `reclaim-pending`을 회수 완료로 바꾸지 않는다. 같은 ID에 대한 release 재시도만 해당 snapshot 삭제를 요청한다.

자동 sweep/daemon/타 작업 연쇄 회수, 비소유 snapshot 강제 삭제, 공유 snapshot 수명주기, 임의 순서 즉시 물리 회수 보장, 저널 압축/flush 정책 변경, 단일 Editor 연결, 마감한 PV 대용량 시험은 포함하지 않는다. 일반 백업과 외부 동시 삭제, 실제 재부팅·전원·공간 고갈 및 ARM A/B의 NR 관문은 그대로 남긴다. 이번 작은 Git fixture를 Unity 실행 검증으로 집계하지 않는다.

현재 계약은 [NTFS 작업공간](../ntfs-workspaces.md#지연-회수와-명시적-재시도), 실행 판정과 증거는 [지연 회수 검증](../verification/deferred-snapshot-reclamation.md)을 따른다. 이 결과로 SF-02 전체 또는 SF-03~06을 승격하지 않는다.
