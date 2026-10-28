# Git payload 보존 검증

[English](../../verification/git-payload-retention.md) · [고정 범위](../plans/git-payload-retention.md)

이 기록은 GR-01–03을 소유한다. 시험한 후보는 보존 payload 용량과 overflow를 피하는 퇴출 연산을 바꿨고 warm 순서·decoder 동시성·내구성은 유지했다. 평균 생성 시간 **2.111초(4.2%)** 절감과 provider 메모리 증가를 확인한 뒤 기각했으며 제품 계약은 **256MiB / 4,096개**를 유지한다. [기계 판독 보고서](../../../reports/benchmarks/git-payload-retention-20261027.json)가 정확한 관측값을 보존한다. 로컬 원시 근거는 `artifacts/git-retention-followup-20261027/`다. `protocol.json`은 비교 묶음을 고정하고, `candidate-source.json`과 `candidate.patch`는 실험 후보를 연결하며, `before-runtime.json` / `after-runtime.json`은 측정용 고정 runtime을 식별한다.

## GR-01 — 실제 생성 프로토콜

측정 기준은 **256MiB / 4,096개**를 사용한 `bea4acdb95f3ea2a914587c7f5e5293b57940e7d`였다. 실험 후보는 **1.5GiB / 32,768개**를 썼고 **decoder session 2개 / proof 선행 읽기 128MiB**를 유지했다. 이 확대 제한은 지원하는 제품 옵션이 아니다. 이 기준은 이미 BO-01–05를 포함했으므로 BO 이전 비교가 아니다. [BD-01](unity-lifecycle-bottleneck-diagnosis.md)은 반복 해석·미사용 퇴출의 동기를 제공하며, 그때의 GUI 수명주기 391.3053613초는 GR-01의 기준 시간이 아니다. 폐기한 cache 대기 우회는 계속 제외한다.

| 프로토콜 항목 | 고정 경계 |
| --- | --- |
| 입력 | Commit `20967120692904985ea6320ce01f93239ba136ff`; 소스 1,602개 / 2,804,769,296바이트; Library 42,038개 / 3,105,800,655바이트. 전체 입력은 BD-01과 다르다. |
| 준비 | 전용 `.local/gr27-source` 실험 clone과 복사한 과거 Library cache; `.local/gr27-home`이 측정 task를 소유한다. 외부 준비는 시간 밖이며 제품 전체 복사 경로가 아니다. |
| 순서 | 전후후전; 한 번에 Editor 없는 작업공간 하나, 버전별 2회. |
| 측정 경계 | 새 `workspace ensure`를 단조 시계 하나로 측정한다. 공통 Library prepare/restore·전체 입력 검증·각 private 편집/격리 검사·명시적 폐기는 별도 측정한다. 격리 구간은 Git status와 원본 바이트 비교를 포함하므로 순수 편집 지연이 아니다. |
| 정합성 / 마감 | 소스/Library 전체 바이트·수정 시각·속성이 동일하며 private 편집/원본 격리와 정확한 소유 폐기/마감을 검사한다. 파일을 조용히 제외하거나 실패 후 task를 대체하지 않는다. |
| 관측 | 양쪽 G1 활성화, 제한된 상태 sampler, ensure 뒤 조회한 Windows provider 수명 중 peak working set/private commit. |
| 실패 | 시리즈를 멈추고 정확한 key/home/id/intent와 근거를 보존하며 자동 대체·강제 종료하지 않는다. |

새 ensure 호출 4회를 모두 마쳤다. 각 행은 `ensure-cost.json`과 대응하는 G1 준비 구간·소유 provider의 Windows 수명 중 peak working set을 사용한다. Warm과 refresh는 attach 안에서 겹치며 attach 자체는 ensure 안에 있으므로 이 기간들을 더하면 안 된다.

| 실행 | 버전 | Ensure (초) | Warm (초) | Index refresh (초) | Provider peak working set (MiB) |
| --- | --- | ---: | ---: | ---: | ---: |
| 1 | 변경 전 | 51.371 | 21.108 | 24.547 | 710.2 |
| 2 | 후보 | 48.650 | 20.495 | 21.700 | 2,841.5 |
| 3 | 후보 | 48.449 | 20.330 | 21.368 | 2,827.9 |
| 4 | 변경 전 | 49.950 | 21.086 | 23.244 | 671.4 |
| 평균 | 변경 전 → 후보 | **50.661 → 48.550** | 21.097 → 20.412 | 23.896 → 21.534 | **690.8 → 2,834.7** |

정확한 ensure 평균 절감은 **2.11080385초(4.16656%)**였다. Attach 평균은 **25.130 → 22.770초**로 변했다. 버전별 평균에서 완료한 proof 뒤 재해석은 **532,746,522 → 11,628,972바이트(97.8%)**, 미사용 선행 읽기 퇴출은 **442,171,392 → 0바이트**로 줄었다. 모든 실행은 여전히 **전체 blob proof 1,563개 / 최초 proof 해석 2,787,029,670바이트**를 수행했다. 따라서 반복 해석은 크게 줄었지만 생성 시간 절감은 작았다.

Provider 수명 중 peak working set 평균은 **724,361,216 → 2,972,444,672바이트(0.675 → 2.768GiB)**로 약 **4.10배** 늘었다. Peak private commit 평균은 **740,626,432 → 3,006,611,456바이트**였다. Provider 프로세스 하나의 peak이므로 decoder 자식과 나머지 시스템은 제외한다. 보존 cache나 시스템 전체 RAM 제한이 아니다.

4개 작업을 모두 명시적으로 폐기했고 소유 provider가 종료됐다. 각 실행 뒤와 최종 일반 NTFS 복원 뒤 전체 소스/Library 바이트·수정 시각·속성과 소스 Git 관리 정보가 일치했다. 공통 Library 준비는 40.585초, 복원은 22.621초이며 ensure 비교 밖에서 각각 한 번 실행했다. sampler의 읽기 오류·실패·상한 초과는 없었다. 후보 소스/시험은 로컬 근거로 보존하고 제품에서 제거했으며 최종 소스/시험은 모두 `bea4acd`와 일치한다. 복원된 솔루션은 경고/오류 0개로 다시 빌드했고 Git cache/reader/선행 읽기/진단 회귀는 **56개 통과 / 실패 0개 / 조건부 skip 1개**였다. 기존 BO 전체 gate는 이 변경 없는 소스의 근거이며 1,734개 gate는 제외한 후보의 근거다.

## GR-02 — 안전성 및 회귀 근거

실험 후보의 필수 `pwsh -NoProfile -File scripts/build.ps1`은 **1,734개 통과 / 실패 0개 / 조건부 skip 30개 / 전체 1,764개**였다. `build-and-test.txt`와 `candidate-tests.trx`가 해당 결과를 보존하며, 이 결과가 채택을 입증하거나 최종 유지 소스의 검증을 대체하지 않는다. 건너뛴 opt-in/native 사례가 이 gate로 새롭게 자격을 얻지는 않는다.

- 후보의 `GitPayloadCacheTests`는 해당 기본 생성자의 빈 cache와 작은 fixture 할당 상한, 보고한 바이트/항목/reader/window 제한, alias 재사용과 완전한 dispose를 검증했다. 시험에서 1.5GiB 전체를 할당하지 않고 필요 시 생성하는 것을 확인했다.
- 음수·2GiB가 overflow로 감긴 값·상한 초과·최대 정수의 바이트 용량과 잘못된 항목 수를 Git 접근 전에 거부했다. 후보 payload 상한은 2GiB가 아닌 정확히 **1,610,612,736바이트**였다. 퇴출은 할당 전에 유입 바이트와 남은 용량을 비교했다.
- 작은 명시적 예산으로 전체 proof를 남기는 항목 제한 퇴출·chunk 크기에 맞지 않는 바이트 제한·올바른 재해석을 검증했다. 후보 용량을 설정한 cache 바이트 손상도 검사했다. 기존 loose/packed 읽기·전체 proof 실패·proof 퇴출·health/poison·peer 실패·close/drain 범위를 유지했다.

독립 읽기 전용 소스 검토에서 입증된 안전성 결함은 발견하지 못했다. 정수 범위 여유·할당 전 퇴출·같은 예산을 쓰는 진행 중 최초 proof chunk·변경 없는 hash/health/게시/drain 경로·새 경계 시험 범위를 확인했고 빌드나 시험을 실행하지 않았다. 이 검토는 실측과 별도이며 프로세스 전체 메모리 자격을 부여하지 않는다. 안전 계약은 게시 전 전체 object/framing proof·매 cache hit의 현재 chunk hash·정확한 불변 identity와 health 검사·실패 게시 차단·접수한 모든 reader drain을 계속 요구한다. 별도 4MiB proof 예산·필수 index refresh·decoder 2개·128MiB window는 그대로다. 저널 flush와 durable ACK도 그대로다.

## GR-03 — 결정과 한계

**기각하며 256MiB / 4,096개를 유지한다.** 평균 생성 시간 **2.111초(4.2%)** 절감은 요청한 큰 시간 절감 기준에 못 미쳤고 provider 메모리는 크게 늘었다. 후보 gate 통과와 해석 바이트 감소가 채택을 정당화하지 못했다. 선택 가능한 cache 모드가 아닌 실험 근거로 보존하며 최종 회수/소스 검사는 이 성능 결정과 구분한다.

후보 바이트 제한은 tree별 보존 payload를 대상으로 했고 별도 항목 수가 작은 파일의 metadata 증가를 제한했다. 어느 쪽도 프로세스 전체 RAM 상한은 아니었다. proof/index metadata·decoder 자식·요청 buffer·GC 보존·임시 할당은 밖에 남았다. 표본 cache peak와 Windows 프로세스 수명 peak는 서로 다른 구간과 자원을 다루므로 합산하거나 시스템 전체 최대치를 측정했다고 주장하지 않는다. 후보는 시작할 때 큰 payload buffer를 예약하지 않았다. 소비 순서/LRU 정책 변경은 별도 미실측이며 해당 알고리즘 시험이나 자동 후속 실행은 하지 않았다.

한 컴퓨터에서 버전별 표본 2개이며 OS cache를 그대로 둔다. Library 입력은 과거 cache이고 이번 소스에 맞게 warmed 상태를 증명한 cache가 아니다. Editor 시작/컴파일/Play/Test/build·원본 반영·전원 장애·다른 backend·Unity 전체 수명주기 이득은 미자격이다. 이전 WC와 BD 기록은 각각의 입력·측정 경계·결정을 유지하며 이번 실험으로 수치를 대체하거나 해당 절차를 자동 재개하지 않는다.
