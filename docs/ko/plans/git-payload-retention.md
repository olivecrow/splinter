# Git payload 보존

[English](../../plans/git-payload-retention.md) · [검증](../verification/git-payload-retention.md)

이 후속은 이미 검증한 Git payload를 더 오래 보존하면 작업공간 하나의 생성 시간이 줄어드는지 시험했다. 실제 전후후전 평균은 **50.661 → 48.550초**로 **2.111초(4.2%)** 줄었고 provider 수명 중 peak working set 평균은 **0.675 → 2.768GiB**로 늘었다. 요청한 큰 시간 절감 기준을 충족하지 못해 후보를 기각했으며 제품 계약은 **256MiB / 4,096개**를 유지한다. [BD-01](../verification/unity-lifecycle-bottleneck-diagnosis.md)은 준비 중 완료된 증명 이후 2,913,936,481바이트를 다시 해석하고 미사용 선행 읽기 2,027,356,160바이트를 퇴출한 것을 관측했다. 이 수치는 실험의 근거였으며 같은 비율의 실제 시간 절감을 입증하지 않았다. 이전 [WC-01–05](../verification/workspace-creation-throughput.md)와 BD-01 측정은 별도 근거로 유지한다.

| ID | 범위와 현재 상태 |
| --- | --- |
| GR-01 | 고정 runtime의 실제 생성 전후후전 비교. ensure 4회 측정과 전체 고정 입력/소유 마감 검사를 통과했다. |
| GR-02 | 후보 용량 연산·필요 시 할당·cache/proof/reader 안전성과 필수 전체 gate. 1,734개 통과, 실패 0개, 조건부 skip 30개. 이 gate가 성능 채택을 입증하지는 않는다. |
| GR-03 | 기각: 평균 생성 시간 2.111초(4.2%) 절감에 비해 provider 메모리가 크게 늘었다. 기존 제품 제한을 유지하고 실험은 근거로 보존한다. |

시험한 후보는 tree별 보존 payload 제한을 **256MiB / 4,096개**에서 **1.5GiB / 32,768개**로 높였다. 바이트만 늘리면 작은 chunk가 항목 수 제한을 계속 받으므로 두 제한을 함께 늘렸다. 필요한 때만 할당하고 남은 용량을 뺄셈으로 비교해 할당 전에 퇴출했다. 생성 시 1.5GiB 예약·구성 가능한 2GiB 모드·새 저장 형식·호환 경로는 추가하지 않았다. 이 실험 제한은 지원하는 제품 옵션이 아니다.

후보는 decoder session 2개, 128MiB 선행 proof window, 별도 4MiB proof 예산, 전체 object 검증, 보존 chunk의 현재 hash, health 검사, 실패 reader의 게시 차단과 close/drain을 유지했다. 최초 warm 읽기와 필수 index refresh는 계속 겹쳐 실행했고 성공·실패·취소 모두 양쪽을 마쳤다. 순서와 LRU 정책은 그대로였다. cache 대기 우회·추가 reader·디스크 cache·전체 입력 복사·refresh/proof 생략·flush/ACK 완화는 추가하지 않았다. 소비 순서/LRU 정책 시험은 실행하지 않았고 이 기록은 자동 후속 실행의 승인이 아니다.

GR-01은 전용 실험 source commit `20967120692904985ea6320ce01f93239ba136ff`의 **소스 1,602개 / 2,804,769,296바이트**, **Library 42,038개 / 3,105,800,655바이트**를 쓴다. 실험용 clone과 과거 Library cache 복사는 측정 밖 준비이며, 제품 준비의 전체 입력 복사 금지는 유지한다. 전체 바이트·수정 시각·속성을 동일하게 비교한다. 각 새 `workspace ensure`를 단조 시계로 재고 공통 Library 준비/복원, 입력 검증, 각 명시적 private 편집/폐기는 별도로 기록한다. 양쪽에 같은 G1 진단과 제한된 관측을 적용한다. 실패하면 시리즈를 멈추고 정확한 task/key/home/intent와 근거를 보존한다.

실험의 1.5GiB는 tree별 보존 chunk 바이트 제한이었으며 프로세스·시스템 전체 메모리 제한이 아니다. proof·index 구조·decoder 프로세스·요청 buffer·일시적/GC 대기 할당 비용이 더해진다. provider 수명 중 peak working set과 private commit은 cache counter와 범위가 다른 관측이며 시스템 전체 peak RAM을 입증하지 않는다. GR-01은 한 컴퓨터에서 버전별 2회이고 OS cache를 비우지 않는다. 과거 Library cache가 이번 fixture 소스에 맞게 warmed 상태라고 주장하지 않는다. GUI 시작·컴파일·Play/Test/build·원본 반영·전원 장애·Unity 전체 수명주기 비교는 이번 실험 밖이다. 해석 바이트 감소만으로 새 시간 이득을 주장하지 않는다.

4개 작업을 모두 명시적으로 폐기했고 소유 provider가 종료됐다. 각 실행 뒤와 최종 일반 NTFS 복원 뒤 전체 소스/Library 바이트·수정 시각·속성과 소스 Git 관리 정보가 일치했다. 공통 Library 준비는 40.585초, 복원은 22.621초이며 ensure 비교 밖에서 각각 한 번 실행했다. sampler의 읽기 오류·실패·상한 초과는 없었다. 후보 소스/시험은 로컬 근거로 보존하고 제품에서 제거했으며 최종 소스/시험은 모두 `bea4acd`와 일치한다. 복원된 솔루션은 경고/오류 0개로 다시 빌드했고 Git cache/reader/선행 읽기/진단 회귀는 **56개 통과 / 실패 0개 / 조건부 skip 1개**였다. 기존 BO 전체 gate는 이 변경 없는 소스의 근거이며 위 1,734개 gate는 제외한 후보의 근거다.
