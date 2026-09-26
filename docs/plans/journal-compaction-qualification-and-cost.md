# SF-02 저널 정리의 Unity 관문과 전체 공간 비용 검증

상태: **범위 확정, 구현·실행 NotRun**. 우선순위는 대형 Unity 프로젝트의 작업공간 용량 부담 확인이다. 이 계획은 [명시적 저널 정리 1차](journal-compaction-first-milestone.md)의 남은 검증만 좁혀 다룬다. [기존 실행](../verification/journal-compaction.md)은 합성 저널 할당을 454,656B에서 49,152B로, 작은 Unity 저널 할당을 2,347,147,264B에서 1,721,651,200B로 줄였다. 둘 다 같은 `durable sequence`를 유지했다. Unity에서는 `Library/ScriptAssemblies`의 `FileNotFoundException`으로 브리지 준비와 스크립트 검증이 실패했고, 전체 작업공간 최고 물리 할당과 저널 단계별 비용은 측정하지 못했다. SF-02 전체는 계속 Partial이다.

## 이번 실행의 범위

| 단위 | 실행과 남길 증거 | 완료 조건 |
| --- | --- | --- |
| QJ-01 Unity 로드 장애 규명 | 기존 작은 Unity 6000.3.22f1 fixture를 짧은 경로의 격리 NTFS 제품 mount에서 사용한다. 정리 전 첫 로드와 정리 후 재개 로드를 같은 버전·패키지·작업 ID·snapshot으로 대조한다. Editor 로그의 첫 `ScriptAssemblies` 예외, 해당 경로의 파일 API 결과, 브리지 로그·heartbeat, 재연결 상태와 실행/종료 시간을 보존한다. 필요할 때만 동일 fixture의 일반 NTFS 경로를 대조군으로 사용한다. | Unity/패키지 입력 문제와 제품 mount·파일 API 문제를 구분할 근거를 확보한다. 제품 결함으로 좁혀지면 원인 경계의 최소 수정과 대응 fake/실제 회귀를 수행한다. 원인이 남거나 제품 밖이면 실패 상태·재현 자료를 보존하고 Unity 관문을 통과로 쓰지 않는다. |
| QJ-02 작업공간 공간 장부 | 한 합성 반복 덮어쓰기와 한 작은 Unity 작업에서 생성 직전, 첫 import 종료 또는 시간 초과, 편집, 정리 시작·후보 생성·발행·이전 세대 제거, 재개, 독립 결과, release 후의 논리 크기와 NTFS 실제 할당을 기록한다. control, delta와 임시 세대, Git 객체·결과, 로그·임시 파일, Unity `Library`의 작업별 쓰기를 겹치지 않게 분류한다. 원본 `Library` 기준량은 제외하고, mount의 논리 view를 물리 사본으로 중복 합산하지 않는다. | 정리 전후 최종 저널 할당, 임시 두 세대의 겹침, **원본을 제외한 소유 추가 물리 공간의 관측 최고치**를 각각 제시한다. 관측 간 숨은 최고치의 가능성과 측정 간격을 밝힌다. 공유 볼륨 VSS Used/Allocated는 별도 시계열로만 보이고 작업별 절감액·총합에 귀속하지 않는다. 누락 항목은 unknown으로 둔다. |
| QJ-03 정리 비용 분해 | 같은 입력의 반복 구간에서 저널 scan/read, 살아 있는 범위 후보 쓰기, flush, replay, seal, provider 재연결 시간을 분리한다. 기존 쓰기별 `Flush(true)`의 호출 수·지연은 관측만 한다. 계측은 읽기 전용·개발용으로 두고 새 자동 정리 정책을 연결하지 않는다. | 단계별 시간·읽기/쓰기 바이트·flush 횟수와 전체 경과를 동일 실행 ID로 연결한다. 관측 불가 비용을 0으로 기록하거나 재연결 replay만으로 정리 전체 비용을 대체하지 않는다. |
| QJ-04 재개·결과·회수 | QJ-01이 성공하면 정리된 동일 snapshot에서 새 스크립트 편집 후 `application.ready`, 실제 로드된 코드와 현재 source validation을 확인한다. 독립 Git 결과와 `stable-journal-v1` seal을 확인하고 release한다. QJ-01이 실패해도 독립 결과·소유 자원 장부와 실패 증거를 보존한다. | 정상 경로에서 제품 `unity stop`과 결과 수집·release를 통과하고, 소유 Editor 프로세스·핸들·mount·delta·마지막 소유 VSS가 0인지 확인한다. 운영자가 정확한 소유 PID를 종료해야 하면 그 개입을 기록하고 정상 종료 Passed로 대체하지 않는다. |

## 실행 순서와 자원

1. 기존 [SF-02 실행 기록](../verification/journal-compaction.md)의 입력·예외·할당 수치를 기준선으로 고정한다. `scripts/measure-workspace-storage.ps1`의 범주를 재사용하되 stage 표본만으로 순간 최고치를 확정하지 않는다. 필요한 경계 계측만 추가한다.
2. 기존 소유 격리 VHDX와 작은 Unity fixture의 현재 상태·소유권·여유 공간을 읽기 전용으로 확인한 뒤 합성과 Unity에 각각 새 소유 ID를 사용한다. 이미 종료한 작업공간과 PV 대용량 비교군은 재개하지 않는다. Unity Editor는 한 번에 하나만 실행하며 Splinter S에 Coordination을 설치·연결하지 않는다.
3. QJ-01의 원인을 먼저 좁히고, QJ-02/03은 Unity 관문 결과와 무관하게 확보 가능한 범위까지 측정한다. 제품 경계의 수정이 필요하면 재현 조건과 보존 계약을 확인한 뒤 해당 파일·계약·시험만 변경한다. 별개 원인의 대규모 Unity/WinFsp 재설계가 필요하면 후속 범위로 분리한다.
4. QJ-04 후 기존과 새 결과 ref·원본 Git 상태를 대조한다. 소유 리소스와 격리 VHD 종료 영수증을 남긴다. `reclaim-pending`은 release 완료로 바꾸지 않고 같은 ID의 명시적 재시도만 한다.

기존 x64 Unity fixture와 합성 데이터로 충분하다. 구매 에셋, 100GB 파일, C/S/Q 재비교, ARM Editor 추가는 요구하지 않는다. 제한 관리자 helper는 소유 ID와 Capture/Observe/Release 작업을 고정한 개발 검증에만 사용하고, 기존 snapshot 삭제로 차단을 우회하지 않는다.

## 판정과 후속 경계

- **Unity 관문 Passed**: 동일 snapshot 재개 후 브리지 `application.ready`, 현재 소스와 로드된 코드의 일치, 정상 제품 stop, 독립 결과·seal·release를 모두 확인한 경우에만 준다. 로드 화면 또는 `ProjectInfo` 시간만으로 대체하지 않는다.
- **공간 장부 Passed**: 같은 실행의 정리 전후 저널 할당과 소유 추가 물리 공간의 관측 최고치·구성 항목, 임시 세대 겹침, 공유 VSS 별도 관측을 제출하고 누락·표본 한계를 명시한 경우에만 준다. 최종 저널 감소가 전체 추가 공간 감소나 대형 프로젝트의 이익을 뜻하지 않는다.
- **비용 장부 Passed**: 저널 단계별 직접 계측과 총 경과가 연결되고 실제 실행의 단위·환경·입력 hash가 남은 경우에만 준다. 이 장부는 flush 정책의 성능 개선이나 내구성 변경 통과가 아니다.
- 세 장부를 놓고 동일 입력의 정리 전후 **소유 추가 공간의 최종값과 관측 최고치**를 따로 비교한다. 최고치가 줄지 않거나 공유 VSS·누락 항목 때문에 결론을 낼 수 없으면 저장공간 목표의 달성으로 보고하지 않고 원인을 다음 설계 입력으로 남긴다.
- 하나라도 실패하면 해당 관문을 Failed/Partial/NotRun으로 유지하고 증거와 독립 결과를 보존한다. 기존 [공통 저장소 실행](../verification/storage-foundation.md)의 FailedPreservation과 SF-02 전체 Partial은 자동으로 바뀌지 않는다. 단일 Editor SF-03~06도 NotRun이다.

이번 범위는 쓰기별 flush 정책 변경, 자동 compact/sweep, 단일 Editor 후보 전환, 설치·binding·discard, 일반 비소유 VSS 공존, 물리 공간 고갈·재부팅·전원 장애, 일반 Unity 에셋 지원을 포함하지 않는다. 저널 내구성/flush 정책은 공간 장부와 Unity 관문을 판정한 뒤 별도로 설계한다. 실제 실행 판정은 새 검증 기록에만 추가하며 이 계획을 통과 증거로 쓰지 않는다.
