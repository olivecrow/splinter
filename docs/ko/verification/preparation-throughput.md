# 준비·관측 처리량 검증

[English](../../verification/preparation-throughput.md) · [범위](../plans/preparation-throughput.md) · [데이터](../../../reports/benchmarks/preparation-throughput-20261026.json)

**PR-01–04, 2026-10-26.** 기준 `448f029a31a7c6f29d14d868c5e5f9e11aff5b19`; 채택 소스·assembly hash는 데이터에 고정한다. 실제 Windows **None / project-snapshot / application none** 비교이며 Unity Editor 전체나 실제 Plastic 서버 벤치마크가 아니다.

## PR-01 — 게시 근거가 있는 부재 대기만 제거

`ReadAsync` 기본값은 없는 파일에 1초 게시 유예를 유지한다. 공유 위반은 재시도하고 해소되지 않으면 실패한다. 완전한 손상 JSON·접근 오류·reparse/소유 불일치도 실패다.

- `StartAsync`는 `client.lock`으로 경쟁 start의 새 intent/receipt 게시를 배제한다. 기존 intent의 자식은 receipt를 뒤늦게 게시할 수 있으므로 부재를 관측하면 unknown으로 자식을 보존하고 같은 요청의 재시도로 일치하는 receipt를 다시 확인한다. 이 기록들을 한 번 읽어 관측/조정에 재사용하고 잠금 안에서 부재 대기를 생략한다. 둘 다 없어도 state를 읽고, 이전 intent/receipt가 있으면 state 게시 유예를 유지한다. 미해결 intent는 시작을 차단하며 일치하는 receipt 없는 live process를 인수하지 않는다.
- 일반 관측은 intent/receipt 읽기를 겹치고 둘 다 join한 뒤 해석한다. state의 정상 유예와 실패/취소 drain을 유지한다. timeout으로 provider 부재를 추정하지 않는다.
- Project task는 존재하는 release receipt를 읽고 identity를 검사한다. release/discard authorization 전에는 부재 대기를 하지 않고, authorization 뒤에는 게시 유예를 유지한다. 공통 기준 소유권·read lease·실제 가용성 검사는 계속 수행한다.
- Task 잠금의 capture/release, basis gate의 dependent attach는 자신이 게시할 기록의 부재 유예를 생략한다. flush·ACK·최종 seal·회수 전제는 제거하지 않았다.

## PR-02 — 제한 병렬 전체 scan과 1회성 응답 증명

전체 파일 scan은 제한된 목록을 먼저 열거하고 None은 최대 8 worker로 다른 파일들을 hash한다. Plastic mount 소스 scan은 직렬을 유지한다. 단일 revision reader에서 파일 읽기를 겹치면 전체 증명을 반복 폐기/재구축하기 때문이다. 길이·수정 시각·속성이 열거된 entry와 일치해야 한다. 기존 manifest/metadata 형식과 ordinal 정렬을 유지한다. 실패/취소는 active worker를 모두 join한 뒤 전파하거나 회수한다.

None/recorded Plastic은 최초 ensure용 기존 `ISourceInspectionProofProvider`를 구현한다. 전체 scan 전후의 권위 있는 source-change barrier와 request/source/application/path/base/result/snapshot/mount/policy를 1회성 증명에 묶는다. 재검증은 owner/basis를 다시 확인하고 새 barrier 두 번 사이에서 전체 metadata/목록을 scan한다. epoch/sequence/history가 같은 경우만 이전 소스 결과를 반환한다. mtime을 보존한 같은 길이 쓰기·재시작·목록/metadata/policy/identity 변경·불완전한 history는 거부한다. Plastic 등록 격리도 재검사한다. tracker 없음/audit는 전체 검사이며 이후 명령은 새로 검사하고 Editor 시작 시 응답 증명을 버린다. 영속 cache·공개 CLI/schema·저장 형식 변경은 없다.

실측 전 집중 시험 **98/98** 통과. 잠긴 파일의 payload를 다시 읽지 않는 재사용·1회 소비·fallback 전체 읽기·변경/재시작/metadata/목록/policy/identity 거부·두 scan의 barrier·불완전한 history·Plastic 격리·최초 시작의 부재·미해결 launch·기존 제어 파일/회수 회귀를 확인했다. 최종 집중 시험에는 authorization이 없어도 존재하는 release receipt와 소유 검사를 수행하는 시험을 포함한다. 전체 gate에는 잠긴 관측 도중 자식이 receipt를 게시하는 경우도 추가했다. 첫 호출은 unknown으로 보존하고 같은 요청의 재시도가 재시작 없이 receipt를 확인한다. 시간 시험은 기존 비병렬 collection을 사용한다.

## PR-03 — 같은 입력의 실제 ABBA 비교

**2,281,701,376바이트 / 2.125 GiB / 2,112파일**: 고유 raw 파일 64 × 32 MiB와 2,048 × 64 KiB. 매 작업 전후 SHA-256·길이·수정 시각이 일치한다. 동일 8바이트 덮어쓰기와 64 KiB 추가에 고정 mtime을 쓴다. 모든 실행 동안 공통 project 기준본과 원본 provider 하나를 유지하고, 각 task는 선택한 고정 실행 파일과 SDK **10.0.114**, bounded raw SJR2를 사용한다. build/tests·Editor를 겹치지 않고 OS cache를 강제 비우지 않았다.

| 단계 | 전 평균 (초) | 후 평균 (초) | 절감 (초) | 감소율 |
| --- | ---: | ---: | ---: | ---: |
| Ensure | 65.258 | 20.650 | 44.608 | 68.4% |
| 고정 private 편집 | 0.059 | 0.037 | 0.022 | 의미 있는 변화 없음 |
| Checkpoint | 38.916 | 24.524 | 14.392 | 37.0% |
| 별도 release | 35.071 | 9.578 | 25.492 | 72.7% |
| 위 작업공간 단계 합계 | 139.304 | 54.790 | 84.515 | 60.7% |

| 실행 순서 | Ensure (초) | Checkpoint (초) | Release (초) |
| --- | ---: | ---: | ---: |
| 1 전 | 65.392 | 43.103 | 35.098 |
| 2 후 | 20.657 | 14.528 | 9.544 |
| 3 후 | 20.644 | 34.520 | 9.613 |
| 4 전 | 65.124 | 34.730 | 35.043 |

인접한 ensure 비교도 각각 **44.735초**, **44.480초** 줄었다. 사전 기준인 ensure 평균 20% 및 10초 절감을 충족한다. 제어 대기·scan 병렬화·응답 증명 재사용을 함께 측정했으므로 각각의 인과 절감을 분리하지 않는다. 명령 시간에는 관측/상태 처리도 포함된다. 편집의 작은 차이는 의미 있는 처리량 회귀/개선이 아니다.

4개 baseline identity와 4개 result identity가 각각 같다. 독립 결과마다 **2,114개 고유 object / 2,315,321,344바이트**를 보존하며 release 뒤 필요한 모든 payload를 읽고 hash 검증했다. 네 task의 mount/delta/provider를 회수했다. 공통 prepare **8.598초**와 최종 restore **11.965초**는 각각 한 번 실행하여 합계에서 제외한다. 원본 내용/mtime을 유지하고 일반 NTFS 경로로 복원했다. 입력 생성·입력/결과 증명·사람/에이전트 대기도 제외한다. 결과·receipt·원시 근거를 보존하며 임시 소스/결과에는 약 13 GiB를 예산으로 잡았다.

남은 명령 비용은 ensure **20.650초**, 전체 결과 수집 **24.524초**, 별도 release **9.578초**다. 전체 소스/admission 증명·독립 baseline/result 구체화·이후 호출의 결과 재검증을 유지한다. 내부 비용을 개별 계측하지는 않았다. 추가 결과 증명 재사용은 고정 object identity/수명이 필요하고, 증분 bounded-journal checkpoint와 복원 재설계는 별도 복구 작업이 필요하다. 이번 변경은 그 보호를 제거하지 않는다.

로컬 NTFS 디스크 하나에서 각 2회 관측했다. 일반 배수·Unity 시작/컴파일/Play/Test/build·실제 Plastic 서버·원본 반영/폐기 시간·peak RAM·전원/장치 장애 자격은 없다. 과거 RT/LU는 별도 역사적 비교이며 이번 데이터의 추가 표본이 아니다.

최종 checkpoint 표본은 후 **14.528초·34.520초**, 전 **43.103초·34.730초**로 편차가 크다. 뒤쪽 인접 비교는 거의 같으므로 평균은 이 입력의 관측이며 보장된 수집 절감률이 아니다. 이 편차의 개별 원인은 계측하지 않았다.

예비 후보와 통과한 전체 gate는 `artifacts/preparation-throughput-20261026/`에 보존한다. 최종 검토에서 Plastic의 단일 revision 증명 반복 퇴출을 막기 위해 mount scan을 직렬로 유지했다. 최종 실행 파일들을 다시 고정하고 `artifacts/preparation-throughput-final-20261026/`에서 같은 입력의 ABBA 전체를 새로 측정했다. 모든 최종 평균에서 예비 측정을 제외한다. 비교별 약 13 GiB, 예비 소스/결과 보존까지 약 26 GiB를 예산으로 잡았다.

## PR-04 — 저장소 gate와 근거

필수 `pwsh -NoProfile -File scripts/build.ps1` 통과: locked restore/build, **전체 1,646 / 통과 1,618 / 실패 0 / 건너뜀 28**, 명령 경과 **550.621초**. 원시 log·gate metadata·복사한 TRX는 `artifacts/preparation-throughput-final-20261026/`에 보존한다. 실측 뒤 제품 소스 변경은 없다. 중간 시험 fixture nullability 컴파일 오류 두 번은 집중 시험 통과 전에 수정했으며 측정 실행이 아니다. 최초 sandbox 기준 build는 SDK sentinel 쓰기가 거부됐고, 실제 비교용 두 실행 파일은 동일한 정상 로컬 환경에서 성공적으로 빌드했다.

원시 근거 `artifacts/preparation-throughput-final-20261026/`: 고정 실행 파일/hash·schema·protocol·입력 manifest·측정/집계 script·네 명령/closure·공통 prepare/restore·집중/전체 gate log·최종 closure. 데이터에 근거와 소스/assembly hash를 기록한다. 양 언어 계약·문서 지도·수명주기 지도/색인을 함께 갱신한다.

문서 검증에서 소스 hash **39개**·symbol 위치 **132개**·로컬 Markdown 링크 **514개**·단계 **19개**와 언어별 **6개 도식**의 같은 topology를 확인했다. 최종 소스/근거 hash와 현재 제품 실행 파일 전체가 최종 실측 후보와 일치한다. 데이터에 gate 수치와 log/TRX hash를 기록한다.
