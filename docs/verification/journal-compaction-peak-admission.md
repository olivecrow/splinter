# SF-02 명시적 정리의 예상 저널 중첩량 예산 검증

실행일: 2026-09-26. 기준은 [PB-01~04 범위](../plans/journal-compaction-peak-admission.md), 작업 시작점은 `development`의 `6d29deb`다. QJ-01~04의 이전 Unity·VSS 결과는 [별도 기록](journal-compaction-qualification-and-cost.md)으로 유지한다.

| 단위 | 이번 판정 |
| --- | --- |
| PB-01 입력·응답 계약 | **Passed, 자동**: 선택적 `maxEstimatedJournalOverlapBytes`의 CLI·입력 schema와 저장된 pending 예산의 응답 schema를 연결했다. 값이 없으면 기존 경로를 유지한다. 0은 명령 경계에서 거부한다. |
| PB-02 후보 전 게이트 | **Passed, 로컬 저널**: 같은 1바이트 범위를 10회 덮어쓴 Windows 로컬 저널에서 이전 길이만 허용하는 예산을 설정했다. `CompactionOverlapBudgetExceeded`가 이전·후보 예상·합계·예산 바이트를 반환했고, 원본 저널 바이트·sequence는 유지했으며 새 후보·intent·previous 파일은 없었다. |
| PB-03 재시도 | **Passed, 자동**: fake coordinator에서 예산 거부 후 `compact-write`/`resume-compaction`과 처음 예산 120B를 재시작 뒤에도 유지했다. 인자를 생략한 재시도에는 같은 예산이 전달돼 다시 거부됐고, 명시적 200B 재시도 후 `ready`로 복귀하며 pending 예산이 지워졌다. 로컬 저널에서는 큰 예산의 동일 작업 재시도로 현재 내용과 sequence를 확인하고 이전 세대를 완료했다. |
| PB-04 전체·실환경 | **자동 Passed, 제품 실환경 NotRun**: `pwsh ./scripts/build.ps1`의 locked restore와 Release 빌드는 경고·오류 0, 자동 시험 268건 중 267 통과·0 실패·선택적 ReFS 1 skip이다. TRX는 [보존 사본](../../artifacts/storage-foundation/pb-20260926/Splinter.Tests_net10.0_x64.trx)에 있다. 이번 변경 후보의 실제 VSS/WinFsp 제품 mount나 Unity Editor 실행은 하지 않았다. |

이 예산은 기존 저널과 후보의 **논리 길이 합계**를 후보 파일 쓰기 전에 비교한다. QJ Unity 기록의 저널 논리 길이 두 개를 적용하면 예상 중첩은 3,588,095,713B다. 당시 실제 두 파일 할당 합계 3,588,100,096B와는 4,383B 차이가 있었으며, 다른 control/Git/runner와 공유 VSS는 이 예산에 포함되지 않는다. 이 값은 이전 QJ 관측에서 계산한 예시이며, 이번 후보로 Unity 정리를 다시 실행해 얻은 결과가 아니다.

거부 뒤 workspace는 명시적 `workspace compact` 재시도가 필요한 정지 상태다. 기본 명령은 예산 없이 기존 정리를 수행할 수 있지만, 거부 후 인자를 생략하면 저장된 예산을 유지한다. 이미 게시된 세대를 복구할 때는 새 예산을 적용하지 않는다. 이 동작은 예상 저널 중첩량을 제어하는 첫 범위이며 전체 작업공간 물리 peak의 보장이나 용량 부담 해소 완료가 아니다. 기존 [SF 실행의 FailedPreservation](storage-foundation.md), SF-02 전체 Partial, SF-03~06 NotRun과 flush 정책 미변경을 유지한다.
