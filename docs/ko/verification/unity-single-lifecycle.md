# 작업공간 하나의 Unity 전체 수명주기 비교

[English](../../verification/unity-single-lifecycle.md)

**LU-01, 2026-10-24:** [요청한 비교](../plans/lifecycle-throughput.md)를 변경 전 → 후 → 후 → 전 순서로, 매회 실제 Unity 작업공간 하나만 실행해 완료했다. 전체 평균은 **359.5초 → 300.0초**, **59.4초·16.5% 단축**이다. 이 중 Library 준비·작업공간 생성·복원에서 반복 확인된 차이는 **50.6초**다. Editor 시작의 평균 차이는 편차가 커 이번 변경의 인과 효과로 확정하지 않는다.

[데이터셋](../../../reports/benchmarks/unity-single-lifecycle-20261024.json)은 개별 시간·고정 assembly hash·소스 증명·관측 counter·마감 영수증을 담는다. 원시 근거와 외부 계측기는 `artifacts/unity-single-lifecycle-20261024/`에 있다. A/B Editor 중첩이나 공유 Editor 비교는 포함하지 않는다.

## 고정 조건과 시간 계산

- Windows/NTFS, Ryzen 5 5600X(6코어·논리 프로세서 12개), RAM 64GB, Unity **6000.3.22f1**. 제품 `unity start`로 GUI Editor를 시작하고 제품의 소유 정상 종료 경로를 사용했다.
- 기존 MCP 제외 실제 에셋 fixture 커밋 `fd611140aa0c22d66782c19b007f1a68c00af2a6`의 전용 실험 clone을 사용했다. 매회 전후 **소스 1,602개·2,804,769,932바이트**, **Library 42,038개·3,105,800,655바이트**와 모든 파일의 수정 시각이 일치한다. PID/cache 파일 제외는 없다.
- 네 번 모두 초기 로드 assembly MVID는 `e188b8a8-3bef-44f9-b3ac-161e371ee53c`다. 작업공간의 초기 소스 내용 증명 1,602개 hash가 독립적으로 해시한 입력과 일치한다. 현재 기본 bounded SJR2를 사용하며 제외한 LT-01 읽기/flush 후보는 없다.
- 기준본은 고정한 `be761bb`에 **아래의 공통 bounded 폐기 정확성 수정**을 더했다. 후보는 여기에 LT-02 Library worker 변경을 더한다. 따라서 전혀 수정하지 않은 출시 실행 파일과의 비교는 아니다.
- `library prepare`부터 `library restore`까지 잰다. 식 하나를 고쳐 `Increment(3)`을 4에서 5로 바꾼다. 명시적 refresh·컴파일 callback·새 generation/MVID·현재 PID/path·ready 상태로 컴파일을 증명하고 선택 모델·텍스처·씬 검사와 실제 렌더를 수행한다. 네 PNG의 SHA-256은 모두 `df94cefbc5f0d2fa6b46b878badb8848bc0221d66130778347349725a47b632d`이며 실제 모델 표시도 확인했다.
- 완료 분기는 **명시적 폐기**다. 소유 Editor 정상 종료·소스 등록/delta/기준 참조 회수 뒤 원본 Library를 복원한다. 원본 소스 반영이나 독립 결과 checkpoint/release 시간은 LU-01에서 측정하지 않았다.
- 실험 clone/backup 준비와 전체 입력/마감 hash 검사는 측정 밖이다. 구간 안의 captured-manifest 일치 확인은 매회 약 **0.15–0.16초**를 별도 제외했다. 나머지 계산 잔차는 **0.006–0.009초**다. Editor 시작 명령은 readiness에, 종료 milestone은 폐기에 포함되어 중복 합산하지 않는다. 측정 중 빌드·시험은 실행하지 않았다.

## 결과

| 단계 | 변경 전 평균, 초 | 변경 후 평균, 초 | 절감, 초 |
| --- | ---: | ---: | ---: |
| Library 준비 | 60.9 | 43.8 | 17.1 |
| 작업공간 생성 | 81.7 | 74.3 | 7.4 |
| Editor 최초 준비 완료 | 105.6 | 95.8 | 9.7, 편차 있음 |
| 코드 변경부터 컴파일 확인 | 18.8 | 19.4 | -0.6 |
| 선택 에셋·씬·렌더 검사 | 1.1 | 1.2 | -0.1 |
| 정상 종료를 포함한 폐기 | 43.7 | 43.9 | -0.2 |
| 원본 Library 복원 | 47.7 | 21.6 | 26.1 |
| **전체 계산 시간** | **359.5** | **300.0** | **59.4** |

변경 전 전체는 **372.9 / 346.0초**, 변경 후는 **297.9 / 302.1초**다. Library 준비는 61.0 / 60.7초 대 43.9 / 43.7초, 작업공간 생성은 81.9 / 81.5초 대 74.3 / 74.3초, 복원은 47.2 / 48.1초 대 21.4 / 21.8초다. 복원은 **54.7%** 줄었다. 세 구간의 합은 일관되게 **50.6초** 짧다. 반면 시작 범위는 **변경 전 91.8–119.3초**, **변경 후 93.5–98.2초**로 겹치며 마지막 기준본이 후보 두 번보다 빨랐다. 전체 16.5%는 이 한정 시리즈의 관측이며 일반적이거나 완전히 분리한 인과 절감률이 아니다.

## 남아 있는 큰 비용

| 우선순위 | 현재 근거 | 다음 검토 경계 |
| --- | --- | --- |
| Editor 초기화·저장 처리 | 후보 readiness **93.5–98.2초**. 최초 refresh **80.7–85.2초** 안에 script compile **33.0–33.9초**, domain reload **39.3–42.2초**가 있고, 그중 InitializeOnLoad는 **23.5–27.2초**다. | 비싼 초기 읽기/쓰기와 callback부터 식별하고 cache·알고리즘을 고른다. 중첩 시간을 합산하지 않는다. Library worker 증가로 Unity callback 자체가 병렬화되지는 않는다. |
| Git 연결·최초 소스 검증 | `attach-intent → ready`는 네 번 모두 **48.3–48.6초**다. Editor 시작 전에 immutable basis를 약 **2.805GB** 읽었다. `GitOverlayRegistration.AttachAsync`가 index refresh를 수행하며, 뒤의 소스 응답 검사는 이미 증명한 hash 1,602개를 재사용하고 약 5초가 걸린다. | index refresh·전체 내용 경로를 계측하고, 전체 내용/세대/소유 증명을 유지하는 한정 병렬 검증·더 큰 immutable RAM cache를 검토한다. 48초 전체를 한 함수에 귀속한 것은 아니다. |
| 시작·사용·종료 안의 durable 쓰기 | 후보 provider는 약 **26,400회 flush**, 누적 durable flush **54.3–60.8초**를 기록한다. 주로 약 **24,000회 write mutation**이다. | 위 수명주기 구간과 겹치는 비용이다. CPU·스레드만 늘려 내구성 I/O 지연이 없어지지는 않는다. 개별 durable ACK 생략·폐기한 native Bee 복원·LT-01 합성 결과의 Unity 승계는 하지 않는다. |
| 정상 종료 | 폐기 시작 뒤 정확한 프로세스 종료 관측은 **34.6–34.8초**이며, 폐기 전체 **43.7–43.9초** 안에 포함된다. | 필요 시 Unity 소유 종료 처리를 별도로 식별한다. 새 디버거 진단은 하지 않았으며 종료·핸들 guard를 유지한다. 소스 metadata capture 약 5.7초, 개별 seal 읽기 약 0.04–0.06초는 더 작은 비용이므로 우선순위가 낮다. |

작업공간 생성과 Editor 준비 완료 구간의 합계는 후보 전체 300초 중 약 **170초**다. Provider counter는 게시·poll 지연이 있는 누적 관측이다. lock/mutation wait는 겹치는 스레드 시간이며 추가 벽시계 시간이나 그대로 회수할 수 있는 절감량이 아니다. 같은 실행 파일을 쓴 첫 기준본의 flush 비용이 마지막 기준본보다 컸다는 점도 시작 편차를 뒷받침한다.

## 진단 보정과 실제 폐기 결함 수정

첫 `00-before` 진단은 새 checkout의 소스 수정 시각과 이전 Library를 함께 사용했다. 최초 scan **277.7초**, readiness **469.9초**였으며 전부 제외한다. 본 비교 전에 내용이 일치하는 이전 입력과 고정 Alpha A/B 시각 기록에서 소스 시각을 복원하고 Library 내용·시각은 그대로 고정했다. 본 비교의 초기 scan은 **0.91–1.03초**다. 바이트 일치만으로 warm cache 조건이 유지되지 않았음을 보여주며 추가 제품 최적화 성과가 아니다.

관측기가 GUI `renderPipelineBefore`를 batch 기준의 `uninitialized`와 비교한 문제도 있었다. 실제 컴파일·렌더는 성공했고 올바른 기존 GUI 기준과 일치했다. 응답·실패 기록을 보존하며 제품 guard를 완화하지 않았다.

이어 실제 폐기에서 별도 제품 결함이 드러났다. 정상 종료·소스 제거 뒤 `JournalReclamation`이 bounded 저장소를 append 회수로 보내 `delta.journal.bounded.lock`을 알 수 없는 파일로 거부했다. task는 `discard-delta`에서 보존됐다. 기존 `BoundedJournalReclamation`을 선택하도록 수정해 정확한 seal/inventory·pending 게시·writer fence·Windows namespace 증명·같은 intent 복구를 유지한다. 모드 영수증 충돌도 거부한다. 수정 뒤 **같은 task/id/home/intent/digest**로 회수를 **2.8초**에 완료하고 진단 원본 Library 전체를 복원했다. 거부를 우회하기 위해 파일을 수동 삭제하지 않았다.

시간 비교 양쪽에 같은 수정을 넣었다. 이전 어댑터 회귀 행렬은 append/latest만 다뤘으나 bounded 성공·journal/guard 삭제·pending intent·외부 파일·변경 journal·unknown 자원까지 확장했다. 어댑터 집중 시험 **20/20**, 기존 inventory/회수 시험 **53/53**이 통과했으며 모드 충돌 2개 사례를 전체 gate에 추가했다. 최종 필수 `pwsh -NoProfile -File scripts/build.ps1`의 locked restore·Release 빌드는 **경고 0 / 오류 0**, 시험은 **1,568 통과 / 실패 0 / 조건부 건너뜀 25 (합계 1,593)**이며 시험 시간은 8분 40.5초다. 전체 시험은 실측 묶음이 끝난 뒤 실행했다.

네 번의 측정 Editor는 모두 종료 코드 0으로 정상 종료했다. 소유 provider·mount·private delta·task 등록은 없고 매회 원본 Library 복원과 소스/Library/refs/설정 일치를 확인했다. canonical `splinter-unity`는 `b09fa58e9d40a560d4fe1af7a213f8ac23e8e3b1`에서 clean이며 과거 디버거 시도나 비소유 자원을 회수·재판정하지 않았다.

마지막 소스·Library 일치와 이전 fixture의 동일 소스 커밋·Library 백업 보존을 확인한 뒤 전용 실험 clone `.local/lu24-source`를 삭제했다. 이 정리는 제품 시간 밖이다. 고정 실행 파일·원시 근거·task 영수증은 유지하며 `fixture-cleanup-preflight.json`·`fixture-cleanup.json`에 정확한 대상과 완료를 기록한다.

**한계:** 한 장비·버전별 2회이고 OS cache를 지우지 않았다. 작은 코드 변경 하나와 선택 에셋 검증을 일반 게임 동작·대형 코드 변경·Play/Test/build·원본 반영·변경된 원본 Library 복원·cold storage·전체 disk/RAM peak·외부 도구 자격으로 확대하지 않는다. 이번 변경은 준비·복원의 유의미한 단축이며 Editor 전체를 몇 배 빠르게 만든 결과는 아니다.
