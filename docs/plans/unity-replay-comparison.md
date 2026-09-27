# 작은 Unity의 기존/개선 재개 비교

2026-09-27 사용자 요청으로 확정했다. RI 커밋 `2cfcad0`의 실제 작은 Unity 재개 시간·파일 할당과 독립 결과·회수를 확인한다. 기존 격리 NTFS 디스크와 작은 fixture를 재사용하며 canonical 프로젝트와 마감한 대형 PV는 변경하지 않는다.

| ID | 범위 | 완료 기준 |
| --- | --- | --- |
| RU-01 | 동일 host의 기존/개선 Core·Runtime과 시험 기준 준비 | `d48b3b0`/`2cfcad0` Runtime 계보·binary hash, 원본/기존 snapshot/파일 할당 기준, 작은 실행 묶음 복사 시간·용량을 보존한다. |
| RU-02 | 작은 Unity의 공통 Library 최초 준비 | 새 소유 snapshot·worker·불변 후보·고정 view에서 정상 import·compile·실제 코드/에셋을 확인한다. 첫 준비 비용은 반복 재개에서 분리한다. |
| RU-03 | 반복 재개 시간·파일 할당 비교 | Editor가 닫힌 동일 source/cache journal의 기존/개선 mount 재개를 각각 3회 순서를 바꿔 비교한다. 실제 Editor 재시작도 두 버전에서 반복하며, Editor가 쓴 누적 journal의 입력 차이·준비/검증/stop 시간과 단계별 할당을 공개한다. |
| RU-04 | 독립 결과·원본 보존·소유 자원 회수 | 정상 Editor stop, 독립 Git 결과와 내용 검증, mount·프로세스·Git 등록·delta·소유 snapshot 회수, 회수 뒤 결과 읽기·기존 원본/타 snapshot 보존·시험 디스크 분리를 확인한다. |

Editor 재시작은 공통 cache에 쓰므로 같은 닫힌 journal의 mount 비교와 별도로 해석한다. full Library 복사·journal 되감기로 같은 입력을 가장하지 않는다. warm OS cache·소수 표본을 일반 개선률로 확대하지 않는다. flush 정책, 기본 모드/Coordination, 첫 렌더 원인 수정과 대형 PV는 범위 밖이다. 현재 판정은 [RU 실행 기록](../verification/unity-replay-comparison.md)을 따른다.

정의한 범위는 `scoped-complete-with-limitations`로 마감했다. 같은 저널 mount는 각각 3회, 실제 Editor 재개도 각각 3회 완료했고 독립 결과·원본/기존 ref/타 볼륨 snapshot 보존·소유 자원 회수·VHD 분리 후 결과 읽기를 확인했다. 같은 입력의 mount 중앙값은 33.84→23.70초였지만 누적 입력의 전체 Editor 개선은 입증하지 못했다. cold compile 응답 수집 지연, focus 복구, 7개 Editor의 stop timeout 재시도와 첫 PNG 제한은 실행 기록에 유지한다. 다음 개선 후보는 누적 write의 범위 index 비용이며 자동 구현 범위로 확대하지 않는다.
