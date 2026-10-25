# Raw 쓰기와 묶음 flush 검증

[English](../../verification/raw-write-batching.md)

**RW-01–05, 2026-10-25.** 기준은 `663c400`이며 현재 변경은 사용자가 명시한 압축 폐기와 묶음 flush를 구현한다. [계약](../raw-write-batching.md)·[범위](../plans/raw-write-batching.md)·[데이터](../../../reports/benchmarks/raw-write-batching-20261025.json)를 따른다. 앞선 DF 실험은 별도의 탈락 후보로 유지한다.

## RW-01 — raw payload와 현재 압축 형식 읽기 보존

제품의 `Encode`·`EncodeJournalPayload`·Brotli 압축 선택과 사용하지 않는 압축 compact staging을 제거했다. 새 append/bounded/latest 쓰기와 재구성한 상태/candidate payload는 raw다. 이전 크기 임계값의 압축 가능/불가능 입력과 raw 물리 payload 길이를 시험한다. 현재 형식의 압축 SJR2 fixture는 시험에서만 구성하며 부분 읽기·cache 검증·외부 checksum이 유효한 내부 손상·기존 압축/새 raw 혼합 쓰기·독립 candidate 재읽기를 검증한다. Reader의 전체 block 검증을 유지하고 이전 버전 fallback은 추가하지 않는다.

## RW-02–03 — 순서 보장 묶음과 callback 수집

Runtime/fake 적합성은 겹치는 쓰기의 순서와 별도 identity를 검증한다. Runtime은 64개/16MiB 수용 한도·빈 묶음·단일 쓰기 내구성·bounded checkpoint를 지나는 묶음·불완전 append/flush 전후 주입 실패·읽기/dispose 대기·재연결을 시험한다. Source/cache 시험은 순서에 따른 partition 전달과 replay를 검증한다. 제어된 callback 시험은 첫 flush를 막은 동안 8개 후속 요청을 queue에 모으고, 9개 쓰기가 2회 flush와 정확한 append 내용으로 완료됨을 확인한다. Native 버퍼는 각 callback 반환까지 소유한다. 명시적 flush가 접수 ticket을 기다리는 경우와 중단된 후속 스레드가 drain까지 입력을 유지하는 경우도 시험한다.

조기 ACK·타이머 수집 대기·백그라운드 writer·롤백 약속은 없다. SJL4의 개별 capsule 게시를 유지하면서 raw payload 정책을 적용한다. 한 요청씩 보내는 동기 호출자는 요청 사이를 묶을 기회를 제공하지 않는다. 명시적 flush/close/result/seal/회수 보호는 유지한다. 계측은 공유 물리 flush를 묶음의 마지막 변경에 한 번 귀속하고, 각 변경의 영속 commit 수는 별도로 유지한다.

## RW-04 — 구성 요소와 native 근거

구성 요소의 고정 입력은 **2,048 × 112바이트**이며 매회 내용·append 위치·기본 bounded SJR2가 같다. 모든 결과를 닫고 재연결한 뒤 모든 바이트를 확인한다. 준비/replay 검사는 쓰기 타이머 밖이다. 순차 → 묶음 → 묶음 → 순차로 실행했고 빌드나 probe를 겹치지 않았다. 묶음은 명시적으로 공급했으며 Unity 작업 기록이 아니다.

| 순서 | 묶음당 공급한 쓰기 | 초 | 영속 변경 | 물리 flush |
| --- | ---: | ---: | ---: | ---: |
| 1 | 1 | 4.346879 | 2,048 | 2,048 |
| 2 | 16 | 0.286335 | 2,048 | 128 |
| 3 | 16 | 0.276534 | 2,048 | 128 |
| 4 | 1 | 4.342911 | 2,048 | 2,048 |

평균은 **4.344895 → 0.281435초**, 이 공급 묶음 부하에서 **93.523%** 감소다. 모든 실행의 payload는 229,376바이트다. 압축을 이미 제거한 순차/묶음 경로 비교이며 제품 전체 수명주기 전후 비교나 압축 제거만의 인과 효과 측정이 아니다.

Fake 불변 기준본을 사용하는 실제 로컬 WinFsp mount 하나에서 **8개 동시 동기 writer**가 write-through client handle로 각각 **128 × 512바이트**를 append했다. **1,024개 변경 / 256회 flush**, **524,288바이트 payload**, writer 시작 허용부터 close까지 **0.570576초**를 기록했다. Native 전후 시간 한 쌍으로 주장하지 않는다. 모든 client handle/retained context가 0이 되었고 정상 unmount·replay 뒤 모든 바이트가 일치했다. 소유 임시 fixture는 제거했다. 원본 프로젝트·Editor·설치 도구·저장장치 설정은 변경하지 않았다.

원시 근거는 `artifacts/raw-group-cost-final.{json,log,trx}`와 `artifacts/raw-group-native-final.{json,log,trx}`다. 명시적 flush의 ticket drain과 중단된 대기 스레드의 버퍼 보호를 포함한 최종 Release 빌드로 측정했다. 초기 구현의 측정값은 데이터에 별도로 보존한다. 최종 Runtime SHA-256은 `b9751fe7be66bd428457d0daea51ef68991414ec9eb6445f24c756bd1857222c`이며 다른 assembly와 근거 hash도 데이터에 기록했다. 새 Unity 시작/종료/전체 수명주기·원본 반영·전원 장애·전체 프로세스 peak RAM은 측정하지 않았다.

## RW-05 — 최종 gate

`pwsh -NoProfile -File scripts/build.ps1`의 locked restore와 Release build가 경고/오류 0개로 통과했고, 전체 시험은 **총 1,621개 / 통과 1,593개 / 실패 0개 / 건너뜀 28개**, 시험 요약 시간 **524.459초**였다. 로그와 TRX는 `artifacts/raw-group-final-gate.{log,trx}`에 보존한다. 이후 최종 native와 구성 요소 측정은 빌드나 probe와 겹치지 않게 순서대로 실행해 각각 **시험 1개 / 실패 0개 / 건너뜀 0개**로 통과했다. 이후 코드 변경은 없다.

최초 집중 시험에서는 압축 의존 기대값이 실패했다. 수정하지 않은 `BriefMetadataReaderWithoutDeleteSharingPublishesTheSameFlushedWrite` 시험은 집중 후속에서 한 번 실패하고 최종 전체 gate에서 통과했으며, 그 단발 실패의 원인은 별도로 확정하지 않았다. 최초 전체 gate의 실패 8개는 obsolete payload 크기 기대값으로, 정확한 raw 크기로 수정했다. 진단 로그와 최초 전체 TRX는 통과로 표시하지 않고 보존한다. 건너뛴 opt-in 실험에 새 자격을 부여하지 않는다.
