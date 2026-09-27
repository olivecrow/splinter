# 누적 write 범위 index 비용의 한정 개선

2026-09-27 범위 확정 후 WI-01~05를 **scoped-complete-with-limitations**로 실행했다. 이 문서는 확정 범위와 완료 기준이며 실제 판정은 [WI 실행](../verification/cumulative-write-range-index.md)을 따른다. [RU 실행](../verification/unity-replay-comparison.md)의 같은 저널 mount는 빨라졌지만, Editor가 저널을 늘린 실제 전체 재개 개선은 입증되지 않았다. 닫힌 cache 저널 331,093 frames 중 write는 195,268 frames였고, 개선 구현의 write index 적용은 고정 입력 중앙값 8.17초에서 후속 입력 16.15·22.36초로 늘었다. cache journal도 Editor 6회 뒤 106,590,393B 증가했다. 이는 누적 write를 조사할 근거이지, `ReplaceRange`가 원인이라는 증거는 아니다. RU의 실제 저널은 회수됐으므로 그 입력을 재사용할 수 없다.

이번 단위는 **SF-02의 replay/재개 시간 하위 범위**다. 용량이 프로젝트의 1순위이므로 새 계측·개선의 파일 할당과 임시 공간을 함께 측정하되, 메모리 index 변경만으로 Library 약 1.83GB의 유효 payload가 줄었다고 주장하지 않는다. 첫 준비와 반복 준비, replay와 Editor ready, 논리 저널 길이와 실제 파일 할당을 분리한다.

| ID | 구현·검증 범위 | 완료 기준 |
| --- | --- | --- |
| WI-01 | write 범위 index 비용 분해 | replay의 write frames·파일별 extent 수 분포·범위 탐색/목록 복사/정렬 비용과 작업량을 경로명을 노출하지 않는 유한한 집계로 계측한다. 기존 `replayIndexOperations`의 write 시간·frame 합계와 맞추고 계측 오버헤드를 별도 비교한다. 과거에 없는 계수는 `unknown`이다. 응답 계약이 바뀌면 Core/schema/fake/시험/현재 문서를 함께 갱신한다. |
| WI-02 | 고정 합성 입력과 원인 판별 | 기존 개발용 `Splinter.ReplayProbe`에 1KiB payload로 같은 파일 순차 쓰기 4,096·16,384회, 같은 범위 반복 덮어쓰기 16,384회, 128개 파일 분산 쓰기 총 16,384회의 고정 입력만 더한다. 입력당 저널 32MiB 이하, 새 입력 합계 128MiB 이하로 제한한다. 독립 기대 내용·state hash·checksum/sequence·입력 hash를 확인하고, frame 수와 파일별 extent 수 중 어느 쪽에 비용이 따라가는지 구분한다. |
| WI-03 | 확인된 병목 하나의 최소 수정 | 목록 전체 복사·정렬이 유의미한 비용으로 확인될 때에만 `ReplaceRange`의 정렬된 extent에서 영향 구간을 찾고 국소적으로 교체하는 변경 하나를 적용한다. 근거가 없거나 개선이 불안정하면 알고리즘 변경을 보류하고 WI-03을 `NotRun` 또는 기각으로 기록한다. 기존 extent 분할·순서·journal offset과 상한 거부, live write와 replay의 동일 결과를 유지한다. |
| WI-04 | 동일 입력 비교와 제품 경로 확인 | 동일 probe 바이너리·공통 주변 DLL을 사용해 기준 `2cfcad0`의 무계측/계측 구현과 후보를 구분하고 실행 묶음의 파일 hash를 기록한다. 봉인한 합성 입력에서 각 3회 순서를 바꿔 replay·index 단계·누적 관리 할당·peak working set·파일 할당을 비교한다. 별도로 기존 작은 Unity와 격리 NTFS 시험 디스크에서 새 소유 snapshot/cache를 준비하고, 정상 Editor 쓰기로 만든 **새 닫힌 저널**의 hash를 고정한 뒤 기준/후보 mount를 각각 3회 비교한다. 후보로 실제 Editor를 한 번 재개해 compile 완료·로드 코드·에셋을 확인한다. Editor가 쓰는 실행은 동일 입력 비교에서 제외한다. |
| WI-05 | 결과 보존·비용 장부·마감 | 최초 준비, 합성 생성, 반복 mount/replay, 실제 Editor ready/검증/stop, 결과 보존/회수 시간을 따로 기록한다. worker·cache/source 저널·Git·증거의 논리량과 관측 파일 할당, 볼륨 공유 VSS의 사용/할당을 별도로 기록하고 누락은 `unknown`으로 남긴다. 공유 VSS를 workspace 파일 할당에 귀속하거나 중복 합산하지 않는다. 독립 Git 결과와 원본·기존 ref·타 snapshot을 확인한 뒤 소유 Editor/provider/mount/Git 등록/delta/snapshot만 회수하고 디스크 분리 뒤 결과를 다시 읽는다. 최종 build·자동 시험·문서/UTF-8 검사와 판정을 기록한다. |

## 실행 순서와 판정

1. 작은 fixture·격리 디스크·canonical 상태, 기존 결과 ref와 타 snapshot, 여유 공간·소유권을 **읽기 전용으로 먼저** 확인한다. RU의 90개 파일/19개 ref는 당시 기준이므로 이번 시작 기준을 새로 기록한다. 기존 결과나 타 snapshot을 재설정하지 않는다.
2. WI-01/02로 병목을 확인한 뒤 WI-03의 단일 변경을 결정한다. 후보가 동일 상태·손상 거부·내구성 회귀를 통과하지 못하면 유지하지 않는다. append/겹침/중간 분할/인접 범위/hole/truncate/축소 후 확장/compacted fragment/extent 상한과 실패 후 재연결을 검증한다. 저널 형식, checksum·sequence·seal, acknowledged write의 flush 정책은 그대로 둔다.
3. WI-04의 합성 비교는 warm OS cache·소수 표본이라고 표시한다. 실제 Unity 비교는 같은 닫힌 입력의 mount/replay 효과만 판정한다. 후보 Editor 재개는 실제 제품 회귀 증거이지만 기준/후보의 전체 Editor 준비 시간 개선률로 환산하지 않는다.
4. 동일 입력에서 상태와 입력 hash가 같고, 계측 오버헤드를 뺀 범위 index 시간의 3회 중앙값이 합성과 실제 닫힌 Unity 저널에서 모두 줄며 전체 mount 시간·파일 할당의 퇴행이 확인되지 않을 때에만 최적화를 채택한다. 각 원시값·범위를 공개하고 표본 산포보다 작은 차이는 개선 주장으로 쓰지 않는다. 개선이 없으면 후보 변경을 되돌리고 원인·비용·미해결 조건을 기록하는 것으로 이 조사 단위를 마친다. 실제 Editor 검증이나 보존/회수가 실패하면 해당 WI를 Passed로 올리지 않는다.

기존 `append`/`overlap`/`file-renames` probe 입력과 RI 결과는 보존하며, 합성 수치를 실제 Unity의 용량 또는 전체 재개 개선으로 환산하지 않는다. 새 첫 import를 반복 비용에 섞지 않고, 실제 저널을 맞추려고 Library를 전체 복사하거나 되감지 않는다. 원본은 계속 수정 가능해야 하고 최초 전체 복제·읽기 hydration·파일 전체 copy-up은 금지한다. 보존 결과는 snapshot 해제 뒤에도 읽혀야 한다.

첫 PNG 빈 화면의 원인 수정, live write flush 묶음, 온라인 저널 정리, 지속 디스크 index, SF-03 제품 큐/Coordination·기본 모드, NR/SW 지원 확대와 대형 PV 재실행은 이번 범위 밖이다. WI 결과로 SF-02 전체나 SF-03~06을 Passed로 바꾸지 않는다. 실제 실행 기록은 [WI 검증](../verification/cumulative-write-range-index.md)과 원시 증거에 남겼다.
