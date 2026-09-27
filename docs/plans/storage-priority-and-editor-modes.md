# 저장공간 우선 설계와 Editor 실행 방식

대형 Unity 프로젝트의 작업공간 용량과 생성·편집 준비·재개 시간을 함께 판단하고 Editor 동시성은 그 뒤에 둔다. 현재 제품은 작업별 독립 Editor 방식이다. 단일 Editor 순차 검증은 후보이며 기본 모드 채택은 미확정이다.

## 남은 범위

| 단위 | 현재 상태 | 남은 완료 조건 |
| --- | --- | --- |
| SF-01 변경 기반 소스 검증 | 정의한 소스 검증 범위 Passed | 일반 파일/입력 지원은 NR 검증과 별도 |
| SF-02 journal·저장량 | Partial | Library 유효 데이터의 절대량, 정리 중 전체 물리 peak, 재개 후 재증가를 포함한 용량 목표 |
| SF-03 단일 Library 검증 | 개발용 작은 A/B/A 하위 범위 완료, 전체 Partial | 제품 연결·임대 복구·검증 세대와 source 반환 병합·큐/Coordination 계약 |
| SF-04 작업 재개·독립 결과 | NotRun | 후보 검증 후 계속 편집, VSS 소실·새 CLI·외부 원본 변경에서도 결과 보존 |
| SF-05 대형 입력 적합성 | NotRun | 파일 수·에셋·LFS/filter·패키지 조건과 NR 장애 관문 |
| SF-06 기본 방식 선택 | NotRun | 같은 입력의 신규/재개/누적 편집/정리 비용 비교 후 채택 여부 결정 |

개발용 후보와 현재 계약은 [validation view](../validation-view.md)를 따른다. 완료된 작은 시험과 제한은 [검증 안내](../verification/README.md)에서 확인하며 새 대형 PV를 자동 시작하지 않는다.

## 다음 판단에 필요한 근거

- [BQ 저장량](../verification/journal-budget-and-growth-qualification.md): 작은 fixture 유효 payload의 약 99.5%가 Library다. 대체 기록 정리만으로 용량 목표를 달성할 수 없다.
- [VC](../verification/immutable-validation-view.md): 후보 게시 약 1.3초와 반복 replay 21~29초·Editor ready 75~84초·정상 stop 31~33초를 구분한다. 게시 시간만으로 작업 준비 시간을 대표하지 않는다.
- [JW](../verification/live-journal-write-cost.md): 첫 import commit 내부 848.16초 중 flush 828.38초를 관측했다. Windows callback의 선행 직렬화 때문에 Runtime 단독 group flush는 미선정이다. callback 원자성·대기/완료 경계·acknowledged write 내구성을 별도로 검토해야 한다.
- [WI](../verification/cumulative-write-range-index.md), [RI](../verification/replay-index-cost.md), [RU](../verification/unity-replay-comparison.md): 같은 닫힌 저널·합성·누적 Editor 재개를 구별한다. 합성 개선이나 warm cache 소수 표본을 전체 Editor 개선·Library 물리 절감으로 환산하지 않는다.
- [VSS 보호 근거](../verification/storage-recovery.md): 정확한 ID 삭제만으로 타 snapshot 보존이 보장되지 않는다. 현재 격리 guard와 명시적 지연 회수 계약을 유지한다.

## 공통 저장소와 Editor 정책

작업자는 Editor 없이 독립 소스를 편집하고 불변 후보를 게시할 수 있어야 한다. 검증 view는 같은 경로의 단일 Library를 사용하되 정상 Editor stop·mount 부재 뒤 재시작하며 replay·재시작·큐 대기를 반복 비용에 포함한다. 사용자 원본 Editor를 별도로 열면 총 Editor 수는 둘이다. 원본 Editor 자체를 공유하려면 짧은 임대 동안 미저장 상태·후보 적용·원복을 보호하는 별도 계약이 필요하다. 작업 기간 전체의 원본 쓰기를 막지 않는다.

전체 논리 파일 트리와 GUID/.meta·패키지 의존성을 유지하며 범위 읽기와 실제 변경만 저장한다. 현재 씬이 참조하는 파일만 선택하는 방식이나 파일 전체 복사로 대체하지 않는다. 도구가 전체 파일을 다시 쓰면 그 쓰기는 누적되므로 정리·독립 Git 결과의 materialization 비용까지 포함한다.

첫 비교 지표는 **원본을 제외한 총 추가 물리 공간과 최고치**다. worker delta, 검증 view/Library, Git·LFS·독립 결과, 임시 파일, 공유 VSS를 분리한다. VSS는 볼륨 공유 관측이며 workspace 비용으로 귀속하거나 중복 합산하지 않는다. 논리 길이·누적 관리 할당·working set은 물리 공간과 다르고 unknown을 0으로 바꾸지 않는다.

판단 순서는 원본·결과 보존과 입력 적합성 → 전체 복제 없는 공간 사용 → 반복 편집/후보 전환의 실용성 → 메모리 → Editor 동시성이다. 독립 Editor는 병렬 실행 이익이 추가 비용을 감당하는 경우의 선택지로 평가한다.
