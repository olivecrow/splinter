# 수명주기 병목 후속 검증

[English](../../verification/lifecycle-bottlenecks.md) · [계획과 경계](../plans/lifecycle-bottlenecks.md)

## 후보와 범위

기준 커밋 `daa3a1646e44395690f00a282d5b44770ca89066`, Windows x64, 2026-10-27과 benchmark 보고서의 소스 hash를 사용한다. BO-01–05는 RR-01 뒤 선택한 한정 최적화를 구현한다. RR-01의 428.6초 / 102.0초는 과거 판단 근거이며 이번 후보의 전후 결과가 아니다.

| ID | 증거 |
| --- | --- |
| BO-01 | 양수 sequence의 정확한 최종 일치, metadata-only·기존 leaf 삭제 복원, 고정 계획과 sealed 저널 효과 대조, root 이동, 전체 내용/identity/구성원 증명, 삭제 staging과 정확한 회수. 신규·재개에 중단/외부 데이터 시험을 연결한다. |
| BO-02 | 고정한 기존 제품과 후보가 같은 10,000 × 1KiB 일반 NTFS 파일에서 read-only leaf 하나를 삭제한다. 직렬 전/후/후/전이며 제품 검증·회수를 포함한 전체 restore 호출을 측정한다. fixture 생성과 시험의 독립 확인은 타이머 밖이다. |
| BO-03 | 선택적·제한된 callback/batch/store-call 관측. 순차·동시 OFF/ON/ON/OFF 비용 fixture는 실제 영속 저널 쓰기와 내용 확인을 유지한다. native WinFsp mount·Unity 실행 자격은 아니다. |
| BO-04 | Git read/proof/cache/wait counter, 제어한 attach 구간 중첩, 실패/drain·시도 identity 시험. 기존 reader/cache 상한을 유지한다. |
| BO-05 | identity·제한·reparse/변경 거부·before/live 연계를 포함한 일반 파일 native 캡처 시험 21개 통과. PowerShell capabilities·helper 로드 통과. 실제 Unity 시작 캡처는 **NotRun**이다. |

## 구성 요소 실측

[기계 판독 보고서](../../../reports/benchmarks/lifecycle-bottlenecks-20261027.json)는 네 번의 실행, 같은 논리 입력 hash, 정확한 assembly/source hash와 두 진단 비용 군을 보존한다.

| 복원 실행 | 변경 전 | 변경 후 |
| --- | ---: | ---: |
| 첫 관측 | 44.772525초 | 2.557986초 |
| 두 번째 관측 | 44.868515초 | 2.353533초 |
| 평균 | **44.820520초** | **2.455759초** |

BO-02의 복원 시간은 **42.364760초 / 94.5%** 줄었다. 기존 실행은 각각 생존 파일 9,999개를 이동했다. 후보는 root 1개·삭제 leaf 1개만 이동하고 생존 파일별 이동은 0회다. 최종 9,999개 내용·생성/수정 시각·속성, 정확한 구성원, 삭제 leaf 부재, basis/delta 회수와 staging 부재를 독립 확인했다. 입력 hash는 접근 시각과 디렉터리 metadata도 고정한다. 복원 중 관리 누적 할당은 324,601,436→183,017,848바이트로 줄었으며 peak RAM 값은 아니다.

| BO-03 실제 영속 callback fixture | 진단 OFF 평균 | 진단 ON 평균 |
| --- | ---: | ---: |
| 순차 쓰기 512회 | 1.114871초 | 1.084679초 |
| 호출자 8개의 쓰기 512회 | 0.259829초 | 0.252060초 |

작은 OFF/ON 차이를 속도 개선으로 읽지 않는다. 두 쌍의 관측에서는 이 fixture의 큰 시간 오버헤드가 없었고 할당은 각각 130,416·150,552바이트 늘었다. 쓰기마다 변경 내용 112바이트이며 순차 호출은 flush 512회, 동시 호출은 기존 batching을 사용한다. 새 관측 비용을 측정했으며 batching 알고리즘의 새 개선 결과는 아니다. 실제 mount Unity의 계측 비용은 unknown이다.

## 재현과 검증

원시 증거는 `artifacts/lifecycle-feasibility-implementation-20261027/`와 `artifacts/optimization-feasibility-20261027/`에 보존한다. 구현 전 `git archive`한 baseline에는 동일 benchmark 시험만 추가했다. 제품 assembly와 논리 입력 hash로 후보를 구별한다. 추적하는 benchmark 보고서는 관측과 소스 hash를 보존한다.

필수 검증은 `pwsh ./scripts/build.ps1`이다. 명시적 비용 시험은 기본 skip이다. `SparseDirectoryRestoreCostTests`에는 `SPLINTER_SPARSE_RESTORE_COST`, `WinFspWriteTelemetryTests.FixedSequentialAndConcurrentTelemetryCostEvidence`에는 `SPLINTER_WRITE_TELEMETRY_COST_OUTPUT`을 새 JSON 경로로 설정한다. 다른 시험/build와 경합하지 않게 비용 군을 직렬 실행한다. 두 명령 모두 Unity를 시작하지 않는다.

첫 전체 검증에서는 일반 경로 회수 fixture 두 개가 계획 생성 전 고아 영수증을 주입해 새 경로 보호가 더 일찍 거부했다. fixture는 실제 일반 복원을 검증 뒤 중단한 다음 다른 모드의 영수증을 주입하도록 수정했다. 다른 저널 모드 회수를 거부하는 기존 assertion과 복원 파일 검증을 유지한다. 별도 sandbox의 Git 초기화·junction 생성 실패는 환경 권한 오류였으며 일반 사용자 재시험과 함께 로그를 보존한다. 겹친 build/test의 DLL 복사 충돌은 후속 소스의 검증 통과로 계산하지 않는다.

최종 변경 경계 시험은 복원·회수·Git 시도 **146/146개**를 12.866초에 통과했다(`final-fixes-focused.trx`). Git reader/cache 대상 검증은 47개 통과·선택적 1개 skip, 쓰기 admission은 45개 통과·선택적 2개 skip이다. 이후 명시적 비용 실행에서 쓰기 오버헤드 시험도 통과했다. 대상 시험은 필수 전체 검증을 대체하지 않는다.

최종 필수 `pwsh ./scripts/build.ps1`은 locked restore·컴파일 경고/오류 0개와 **통과 1,724 / 실패 0 / 조건부 skip 30**, 총 1,754개를 9분 08.601초에 통과했다. 원시는 위 수명주기 증거 디렉터리의 `final-full-build.log`·`final-full-build.trx`다. 측정한 후보 assembly hash 두 개는 이 최종 빌드와 일치한다. 양 언어 Markdown 링크, 소스 색인 hash 50개, benchmark 소스 hash, 단계 ID 19개, UTF-8과 `git diff --check`를 확인했다. 코드 기반 지도는 언어별 도식 6개를 유지한다.

## 해석과 남은 비용

복원 최적화는 생존 파일별 이동과 진행 상태 작업을 제거한다. 최초·이동 후 전체 내용 증명, identity/구성원 검사, metadata 적용, 정확한 저널 회수는 유지한다. 새 파일·디렉터리 집합 변경·경로 이동·길이/private range 변경·시작한 일반 계획은 기존 파일별 경로다. metadata-only 재시도의 동작은 계획으로 한정한다.

상세 쓰기·Git 계측은 기본 OFF다. 진단을 켜는 것은 원인 구분용이며 새 고속 모드가 아니다. callback/effective/changed bytes를 구별하고 누적 대기·동시 처리 ticks를 전체 스톱워치로 읽지 않는다. 캡처는 제한된 기존 자료와 정확한 프로세스 identity를 보존하며 Unity profiler를 켜거나 호출자 구간을 Editor 시작 시간으로 주장하지 않는다.

새 실제 Unity 수명주기·원본 반영·폐기·Play/Test/build·외부 도구 지원·OS 전원 장애·peak RAM 자격은 실행하지 않았다. 남은 큰 Editor 시작·쓰기/flush 비용은 별도로 고정한 실제 작업에서 새 관측을 확보해야 한다. 구성 요소 절감 비율을 과거 전체 시간 차이에 그대로 적용하지 않는다.
