# 최초 준비의 live journal 쓰기 비용

2026-09-27 확정. WI-01~05 이후의 SF-02 계측 하위 범위다. WI의 첫 import 종료 시 provider는 commit 331,992회, flush 332,061회·누적 816.35초를 기록했다. 최초 ready 1,180.49초와 계측 종료 시각이 다르므로 이를 독립적인 시간 분해나 절감 가능 시간으로 환산하지 않는다. 범위 index를 개선한 뒤 남은 쓰기 비용을 연산·단계·동시 호출로 분해한다. 용량 장부를 함께 확인하며 SF-02 전체와 대형 프로젝트 용량 목표의 완료로 확대하지 않는다.

| ID | 확정 범위 | 완료 기준 |
| --- | --- | --- |
| JW-01 | live mutation/commit 계측 | 경로 없는 고정 연산별 준비·append·durable flush·index 적용·전체 호출 시간, 성공/실패와 기록 바이트를 집계한다. mutation gate 대기와 flush 시작/끝의 다른 mutation 대기 호출 수를 고정 histogram으로 기록한다. no-op/거부될 호출도 대기 표본에 포함될 수 있음을 명시한다. 과거 누락은 unknown이다. Core/schema/fake 기반 적합성 시험과 현재 문서를 맞춘다. |
| JW-02 | 고정 합성 쓰기와 계측 비용 | 새 디렉터리만 쓰는 개발용 WriteProbe로 단일 쓰기 1,024회, 4개 동시 작성자 각 256회, metadata 1,024회의 고정 작은 입력을 실행한다. 각 journal 16MiB 이하, 실행 묶음 총 256MiB 이하로 제한한다. 공통 probe와 기준/계측 Core·Runtime hash를 고정하고 각 3회 순서를 바꿔 비용·출력 hash·재연결·파일 할당을 비교한다. |
| JW-03 | 새 작은 Unity의 첫 준비와 반복 수정 | 기존 작은 fixture와 격리 NTFS 시험 디스크에서 새 소유 snapshot/delta를 만든다. 첫 Editor import, 명시적 compile·실제 로드 코드·에셋, 한 번의 후속 코드 수정, 정상 stop과 닫힌 journal 재연결을 확인한다. 각 단계의 같은 provider epoch 차이를 사용하고 누적 gate 대기를 벽시계와 합산하지 않는다. |
| JW-04 | 판단·독립 결과·회수 | 최초 준비·후속 검증·결과 보존/회수 시간과 worker/Library/Git/증거 파일 할당·공유 VSS를 기록한다. 새 시작의 원본 파일/ref/타 snapshot 기준을 잡아 보존을 확인한다. 독립 결과를 확보한 뒤 소유 프로세스·mount·Git 등록·delta·snapshot을 회수하고 VHD 분리 뒤 결과를 다시 검증한다. 실제 관측으로 후속 group commit 실험의 타당성과 필요한 내구성 관문을 결정한다. |

이번 단위는 기존 acknowledged write마다 flush하는 정책과 저널 형식을 유지하는 계측·원인 판별이다. flush 시작/끝의 대기 호출은 batch로 합칠 수 있는 durable transaction 수가 아니며, 그 수로 성능 개선률을 계산하지 않는다. 잠금 밖에서 쓰기 성공을 조기 반환하거나 새 queue/daemon을 넣지 않는다. 쓰기 묶음의 구현은 별도 범위로 두며, 채택하려면 성공 응답 이전의 durable sequence, 실패 전파·재연결·seal·종료와 bounded 대기 검증이 필요하다.

기존 WI 저널은 회수됐으므로 새 실행 기준을 수집한다. Library 전체 복사, 저널 되감기, 원본 hydration, 대형 PV 확대, 첫 렌더 원인 수정, 제품 기본 모드·Coordination 연결은 포함하지 않는다. 과거 첫 PNG 빈 화면과 stop timeout을 새 실행의 결과와 구분한다.

## 실행 판정

JW-01~04는 [실행 기록](../verification/live-journal-write-cost.md)의 정의한 범위를 `scoped-complete-with-limitations`로 마감했다. 자동 303 통과·선택적 ReFS 1 skip, 합성 18회와 새 작은 Unity의 첫 준비·후속 코드 수정·정상 stop·동일 journal 재연결·독립 결과·원본/ref/타 snapshot 보존·회수·VHD 분리 뒤 결과 검증을 확인했다. 첫 PNG와 후속 reload 뒤 첫 PNG의 빈 화면, 작은 표본과 순간 물리 peak unknown은 남는다.

Windows 변경 callback이 Runtime 밖에서 직렬화되므로 Runtime에 동시 flush 묶음만 추가하는 구현은 선정하지 않았다. 후속 후보를 검토하려면 먼저 callback의 복합 검사 원자성·실제 대기·요청 완료 경계를 정의해야 하며, 기존 durable sequence/실패 전파/재연결/seal/종료 계약이 선행 관문이다. 이번 계측 결과로 SF-02 전체, Library 용량 목표 또는 대형 프로젝트 성능을 완료 처리하지 않는다.
