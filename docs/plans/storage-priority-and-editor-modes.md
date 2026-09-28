# 저장공간 우선 설계와 Editor 실행 방식

대형 Unity 프로젝트의 작업공간 용량과 생성·편집 준비·재개 시간을 함께 판단하고 Editor 동시성은 그 뒤에 둔다. 제품은 작업별 독립 Editor와 명시적 `validation` 단일 Editor 검증을 제공한다. 기본 모드 자동 선택은 미확정이다.

## 남은 범위

| 단위 | 현재 상태 | 남은 완료 조건 |
| --- | --- | --- |
| SF-01 변경 기반 소스 검증 | 정의한 소스 검증 범위 Passed | 일반 파일/입력 지원은 NR 검증과 별도 |
| SF-02 journal·저장량 | Partial | Library 유효 데이터의 절대량, 정리 중 전체 물리 peak, 재개 후 재증가를 포함한 용량 목표 |
| SF-03 단일 Library 검증 | 제품 CLI/provider와 작은 실제 A/B/A 연결 완료, WF 제한 유지. 전체 Partial | 일반 임대 복구·source 반환 병합·큐/Coordination 계약과 반복 비용 판단 |
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

## SF-03 제품 연결과 남은 관문

제품 `validation` CLI와 실제 Windows/Git/Unity provider, 서로 다른 VSS의 A/B/A와 같은 경로/캐시, worker 게시의 쓰기 차단, 독립 Git 결과, 제품 snapshot 회수 의존성 검사는 [WF 실행](../verification/product-validation-workflow.md)에서 선택 범위 완료로 판정했다. 기존 첫 cold import 중단·복구 기록은 유지한다. 최종 후보의 두 빈 cache 반복·원시 응답 schema·전체 비용과 보존/회수는 [WF-06~08](../verification/product-validation-cold-cost.md)의 별도 근거로 판정한다.


공통 수명주기와 실제 provider·CLI는 [검증 후보 수명주기](../validation-workflow.md)를 따른다. 연결 자격은 [WF 추적표](validation-matrix.md#sf-03-제품-workflow-연결)에서 아래 범위를 판정한다.

- 등록된 Git 포인터의 분리·worker 쓰기 차단·같은 owner/request 게시 복구를 실제 VSS에서 확인한다. 기존 `ExportCandidate`의 `.git` 거부와 사용자 파일 보호는 유지한다.
- 독립 A/B snapshot에 의존하는 candidate/source 세대와 view 소유의 빈 cache를 구분한다. 동일 프로젝트·compatibility·partition·예산·owner일 때 다른 snapshot 후보로 전환하며 최초 전체 복제·읽기 hydration을 사용하지 않는다.
- 독립 Git 결과의 객체 존재와 제품 snapshot 의존성 차단을 확인한다. 실제 CLI 중단과 응답 유실을 시험하며 fake나 예외 주입의 성공을 실제 프로세스 결과로 승계하지 않는다.
- 최종 후보의 작은 실제 A/B/A에서 정상 stop·mount 부재·같은 경로 재시작, 실제 코드/에셋·결과 독립성·원본/타 작업 보존·소유 자원 회수를 확인한다. 전체 추가 파일 할당과 최고치, replay·준비·재개 시간을 구분한다.

SF-03 전체는 Partial이다. FIFO/Coordination, 일반 임대 인수, 생성 source 반환 병합, 원본 Editor 공유, 기본 모드 선택, group flush, 대형 PV 재개, NR 전체 시험과 설치·binding·discard·권한 서비스는 별도 후속이다. 현재 계약의 같은 요청 중단·응답 유실·재시작 복구는 계속 유지한다.

## 다음 범위 판단

[WF-06~08](../verification/product-validation-cold-cost.md)은 최종 후보의 두 빈 cache A/B/A·소유 파일 할당/시간·독립 결과/회수 근거를 제공한다. 렌더 준비에서 발생하는 설정 저장을 마친 뒤 같은 세대를 다시 시작해 검증하므로 이 추가 Editor 시작/종료도 반복 비용에 포함한다. 첫 두 시도의 실패와 관측 사이 순간 물리 최고치·공유 VSS 귀속의 한계를 유지한다.

남은 우선순위는 Library 유효 데이터의 절대량과 재증가, 일반 파일/에셋·중단 경계의 보존, 같은 입력의 신규/재개/반복 비용이다. t2의 첫 Editor 시작 전 정상 unmount 직후 상태 읽기 실패는 같은 준비 요청 재시도로 복구됐지만 정확한 원인은 미확정이며, 무오류 준비 경로는 후속 검증이 필요하다. 기본 모드 판단에는 이 근거가 더 필요하며 작은 fixture 통과를 대형 공간 목표 달성으로 확대하지 않는다. 일반 임대 복구·source 반환 병합·큐/Coordination, 새 저장소 backend·flush 정책 변경, NR 전체 장애 시험, 설치·binding·discard·권한 서비스와 대형 PV 재개는 별도 범위다.

## 공통 저장소와 Editor 정책

작업자는 Editor 없이 독립 소스를 편집하고 불변 후보를 게시할 수 있어야 한다. 검증 view는 같은 경로의 단일 Library를 사용하되 정상 Editor stop·mount 부재 뒤 재시작하며 replay·재시작·큐 대기를 반복 비용에 포함한다. 사용자 원본 Editor를 별도로 열면 총 Editor 수는 둘이다. 원본 Editor 자체를 공유하려면 짧은 임대 동안 미저장 상태·후보 적용·원복을 보호하는 별도 계약이 필요하다. 작업 기간 전체의 원본 쓰기를 막지 않는다.

전체 논리 파일 트리와 GUID/.meta·패키지 의존성을 유지하며 범위 읽기와 실제 변경만 저장한다. 현재 씬이 참조하는 파일만 선택하는 방식이나 파일 전체 복사로 대체하지 않는다. 도구가 전체 파일을 다시 쓰면 그 쓰기는 누적되므로 정리·독립 Git 결과의 materialization 비용까지 포함한다.

첫 비교 지표는 **원본을 제외한 총 추가 물리 공간과 최고치**다. worker delta, 검증 view/Library, Git·LFS·독립 결과, 임시 파일, 공유 VSS를 분리한다. VSS는 볼륨 공유 관측이며 workspace 비용으로 귀속하거나 중복 합산하지 않는다. 논리 길이·누적 관리 할당·working set은 물리 공간과 다르고 unknown을 0으로 바꾸지 않는다.

판단 순서는 원본·결과 보존과 입력 적합성 → 전체 복제 없는 공간 사용 → 반복 편집/후보 전환의 실용성 → 메모리 → Editor 동시성이다. 독립 Editor는 병렬 실행 이익이 추가 비용을 감당하는 경우의 선택지로 평가한다.
