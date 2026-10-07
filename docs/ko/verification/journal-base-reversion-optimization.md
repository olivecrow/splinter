# 다수 파일 기준본 복귀 시간 최적화 검증

[English](../../verification/journal-base-reversion-optimization.md)

2026-10-06 Windows/NTFS. 기준 구현 `25f4fa1`과 같은 브랜치 `codex/journal-base-reversion-optimization`의 후속 후보를 비교했다. [고정 범위](../plans/journal-base-reversion-optimization.md)와 [현재 계약](../journal-base-reversion.md)을 따른다. 이전 [실제 A/B·7경계 기록](journal-base-reversion.md)은 이전 바이너리의 자격이며 이번 후보의 Unity/WinFsp 실행으로 승계하지 않는다. 기본 OFF를 유지한다.

증거는 ignored `artifacts/journal-base-reversion-optimization-20261006/`에 보존했다. `before-runner/`와 `before-binaries.json`은 이전 바이너리, `after-binaries.json`은 측정한 최적화 바이너리다. 입력/PNG 목록·각 18표본의 `result.json`·`before.trx`/`after.trx`·`analyze.ps1`/`summary.json`·원본 hash/ref 확인을 함께 보존한다.

## 입력과 시간 경계

`splinter-unity`의 고정 commit `b09fa58e9d40a560d4fe1af7a213f8ac23e8e3b1`, 서로 다른 blob의 실제 Medieval PNG 32개, 합계 **47,854,812 bytes**, 파일별 1,155,235~1,862,386 bytes다. 기준 프로젝트 전체가 2.8GB라는 이전 조사와 실제 변경한 47.85MB 에셋을 구분한다. 합성 큰 파일로 대체하지 않았다.

128KiB 범위를 64KiB부터 256KiB 간격으로 교차 저장한다. 각 범위의 B→C→base를 여러 순환 반복하고 최종 저장을 추가한다. 혼합 입력은 파일별 전체 복귀/반쪽 복귀/변경 유지를 섞는다. `small-no-reversion`은 최종 모든 범위가 다른 바이트다. payload 쓰기 수는 최종 저장을 포함하며 별도 파일 metadata 쓰기는 제외한다.

| 입력 | 파일×범위×순환 | payload 쓰기 수 |
| --- | --- | ---: |
| small-mixed | 8×2×6 | 304 |
| many-mixed | 32×3×8 | 2,400 |
| small-no-reversion | 8×2×6 | 304 |

전후 OFF/ON 각각 3회, OFF/ON→ON/OFF→OFF/ON 순서다. 실제 NTFS의 독립 stopped SJR2 저널, 고정 Git provider/ref/lease, 정리마다 새 reader를 사용했다. 쓰기에는 durable flush와 metadata/변경 관측을 포함한다. 정리는 reader 열기·정리·reader 종료를, 재생은 새 reader 열기·journal 재생·usage/seal 관측·종료를 포함한다. 초기 fixture range 준비·전체 파일 내용 검증·마지막 `CompleteCompaction`은 위 시간에서 제외한다. Unity Editor/import·live WinFsp 부하·원본 Library·새 A/B를 실행하지 않았다.

OS cache는 비우지 않았고 cold/warm을 분리하지 못했다. 이전 바이너리 측정 중 일부 구간에는 빌드/선택 회귀가 병행됐다. 전후 쓰기 p95와 전체 시간이 크게 변한 구간을 기록하며, 쓰기 속도 변화를 이 최적화의 효과로 주장하지 않는다. 아래 정리 시간·해독량·보존 의미를 주 근거로 삼는다.

## 결과

3표본 중앙값, ON의 같은 기능 전후다. 최종 payload 선택·기준 비교 bytes·제거 bytes·NTFS 최종 할당은 전후 모두 동일했다.

| 입력 | 정리 이전→최적화 | 정리 단축 | Git 해독 bytes 이전→최적화 | 누적 관리 할당 이전→최적화 |
| --- | ---: | ---: | ---: | ---: |
| small-mixed | 13,816.60→2,836.54ms | 79.47% | 30,402,066→13,528,767 | 67,391,696→13,811,264 |
| many-mixed | 82,294.24→8,073.58ms | 90.19% | 222,165,084→70,923,484 | 402,936,352→56,304,880 |
| small-no-reversion | 21,300.23→2,730.16ms | 87.18% | 24,960,382→13,528,767 | 100,284,464→13,818,088 |

`base-range-compare` 자체의 중앙값은 각각 12,463.86→1,377.03ms, 79,215.46→5,353.02ms, 18,794.57→1,308.28ms다. Git 해독은 reader의 논리 해독 counter이며 OS 물리 I/O가 아니다. 누적 관리 할당은 `GC.GetTotalAllocatedBytes` 차이이며 상주 RAM/heap/시스템 peak가 아니다. ON 이전/이후만 비교한 관리 할당 감소는 각각 79.51%, 86.03%, 86.22%다.

| 입력 | 최적화 OFF/ON 최종 NTFS journal 할당 | ON 절감 | 쓰기+정리+재생 ON 이전→최적화 중앙값 |
| --- | ---: | ---: | ---: |
| small-mixed | 2,273,280 / 1,093,632 bytes | 51.89% | 15,745.46→4,878.68ms |
| many-mixed | 13,918,208 / 7,426,048 bytes | 46.65% | 89,771.57→15,535.11ms |
| small-no-reversion | 2,273,280 / 2,273,280 bytes | 0% | 25,786.66→4,647.98ms |

전체 측정 시간은 각 표본의 쓰기+정리+재생 합계의 중앙값이며 위의 개별 stage 중앙값을 더한 값이 아니다. `many-mixed` 최적화 표본 전체 시간은 OFF **10,582.43~32,547.17ms**, ON **14,943.59~32,538.56ms**로 변동했다. 해당 쓰기 stage는 OFF 6,656.52~28,768.00ms, ON 5,887.39~23,312.00ms였다. 최적화된 정리 ON 7,851.7~8,485.2ms의 단축과 별개로 쓰기/전체 변동의 원인은 확정하지 않았다.

기본 ON 조건에 미달한다. 최적화 후 작은 혼합 입력의 전체 OFF 3,409.53ms / ON 4,878.68ms, 복귀 없는 입력 OFF 3,257.44ms / ON 4,647.98ms로 약 43% 회귀했다. 후자는 공간 절감도 없다. 32파일 전체 비교의 변동을 유리한 기본값 근거로 사용하지 않는다. 이전 압축 반복 입력의 할당 개선 0%도 새 통과로 바꾸지 않는다. **기본 OFF 유지**, 명시적 ON의 비용을 줄인 결과다.

작업량이 늘 때 고정 49%를 더하는 구조는 아니다. 원래는 교차 저장된 private extent 순서가 같은 Git blob을 다시 증명하고, 부분 읽기를 버리는 전환마다 최대 1초 대기를 누적했다. 지금은 BasePath/논리 순으로 비교한 뒤 후보의 물리 순서를 복원하고, 정확한 소유 stdout을 먼저 닫아 unread suffix의 대기를 취소한다. 비용은 저널 이력 스캔·현재 비교 private bytes·기준 blob 전체 증명과 범위 재해독·range 정렬에 따라 달라진다. 변경 파일 수·살아 있는 범위·blob 크기와 같은 변수가 다른 입력의 비례식을 이 3표본으로 확정하지 않는다.

## 보존과 자격

36표본 모두 전체 파일 streaming SHA-256·entry/ID·metadata·변경 목록/sequence가 동일 기대값을 만족했다. 새 자동 시험은 교차/역순 범위와 rename의 비교·후보 재생 순서, 다른 파일에서 비교가 실패했을 때 기존 ACK bytes/저널/seal 보존, 6개 partial reader의 전환/종료를 확인한다. 기존 손상 blob/chunk·packed object·ref/lease 보호·30초 무진행과 진행하는 읽기·7개 합성 실제 자식 종료 복구를 유지한다. 선택 시험 31/31 통과 뒤 실패 미게시 시험을 추가했다. 최종 `pwsh scripts/build.ps1`의 locked restore·전체 빌드는 경고/오류 0, 전체 시험 **1,088개 중 1,079 통과·0 실패·9 선택 시험 skipped**다. `build-final.log`·`full-final.trx`를 보존했고 최종 Runtime/Git DLL hash는 측정한 최적화 바이너리와 일치한다.

원본 사용 PNG 32개 SHA-256·HEAD·clean status와 기존 ref **92개**가 그대로다. 기준 owner `jr-opt-20261006`의 ref만 같은 receipt/lease 확인 뒤 release했고 `ownership-after.json`의 차이는 비어 있다. 기존 원본/Library·Editor·서비스/ACL/설치·비소유 ref·Git GC를 변경하지 않았다. 일반 reader 최적화는 Git 읽기 경로에 적용되며 기준본 복귀 선택은 계속 명시적 ON이다.

전체 작업공간 물리 peak·공유 저장 귀속·provider/Editor RAM·native codec RAM·OS 물리 I/O는 unknown이다. 새 native WinFsp 중단·Unity 저장/import/Play/Test/build·다른 provider/OS/전체 장애 행렬은 NotRun이다. 이전 최종 A/B를 다시 실행하거나 그 자격을 이 후보로 승계하지 않는다.

기본값의 후속 결정: 이 기록의 OFF는 당시 후보의 선택이다. 현재 SJL4 쓰기 경로와 새 작업의 기본 ON 결정은 [현재 계약](../latest-only-journal.md) 및 [후속 검증](latest-journal-write-optimization.md)을 따른다. 당시 입력·수치·미자격은 그대로 보존한다.
