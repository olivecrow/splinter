# 작은 Unity 재개 비교 실행

[English](../../verification/unity-replay-comparison.md)

`ru-20260927`의 [RU-01~04](unity-replay-comparison.md)를 **scoped-complete-with-limitations**로 마감했다. 제품 기준은 `2cfcad07069e48cde64a3fa9cbfd70a1b546db5e`다. 같은 저널 재연결 6회·실제 Editor 재개 6회, 독립 결과와 원본 보존, 모든 소유 자원 회수와 VHD 분리 후 결과 읽기를 확인했다. 같은 저널 mount 중앙값은 33.84초 → 23.70초였지만, 누적 입력의 실제 Editor 전체 재개 개선은 입증하지 못했다.

## 입력과 비용 경계

기존 격리 NTFS VHD와 `V:\sf-compaction-20260926\unity`를 재사용했다. 원본 HEAD는 `58f81b5bc6ff8a940d8a90872bd3fed858a54cc1`, 추적 파일은 90개, 보호할 기존 ref는 19개이며 원본에는 Library가 없다. 새 소유 snapshot과 `V:\ru27`의 worker·불변 후보·고정 view·공통 cache 하나만 사용했다. canonical 프로젝트, 대형 PV, Coordination과 Codex worktree는 사용하지 않았다.

비교 대상은 기존 `d48b3b0`과 개선 `2cfcad0`의 Core/Runtime이다. 주변 CLI/Git/Unity/Windows 어셈블리는 공통으로 두고 두 실행 묶음의 41개 파일 중 `Splinter.Core.dll`, `Splinter.Runtime.dll`만 다른 것을 hash로 확인했다. 서로 다른 두 커밋 전체를 각각 새로 빌드한 비교는 아니다.

| 실행 묶음 | 파일 수 | 논리 B | 복사 초 | Runtime SHA-256 |
| --- | ---: | ---: | ---: | --- |
| baseline | 41 | 3,137,191 | 1.0750 | `F44D4FFB2FE057EBE3305B3823CCB4A919C1EBE0831B30FA19BCA23D8A70D2FD` |
| current | 41 | 3,140,263 | 0.1786 | `4948624B8F45FB57AA08CF1EF14303C14875D4B19E60658223EE2C1AB91A6B89` |

프로젝트·Library 전체 복사, hydration, journal 되감기는 하지 않았다. worker 최초 mount는 2.09초, 작은 후보 export는 0.86초였다. 표의 작은 바이너리 복사·후보 게시를 실제 Editor 작업 준비 시간으로 대체하지 않는다. 두 묶음은 측정 증거 디렉터리 안에 있으므로 증거 할당과 중복 합산하지 않는다.

제품 코드 변경은 없으며 기존 RI의 최종 빌드·locked restore·자동 **300 통과·0 실패·당시 제외 시험 1 skip** 증거를 재사용했다. 이번 실행 전 제품 소스 134개의 hash가 해당 검증 manifest와 같은 것을 확인했다. 이 수치를 이번에 다시 실행한 자동 시험으로 표시하지 않는다.

후보는 `Calculate(3) = 23`, `Target.png.meta`의 `maxTextureSize = 1024`, 별도 `CandidateA.txt`와 `.meta`다. 실제 Editor의 로드된 코드·1024×1024 텍스처·mesh 3개와 동일 후보 파일을 검사한다. 단순 파일 내용 검사와 실제 Unity 실행 증거를 구분한다.

## 첫 준비와 운영 재시도

첫 `ready` heartbeat는 Editor 시작 호출부터 **1,495.67초**였다. 뒤이은 명시적 refresh는 접수됐지만 55초의 수집 한도를 넘겼다. 같은 request ID/fingerprint의 성공 응답이 refresh부터 **68.87초**, 시작부터 **1,565.16초**에 도착했으며 새 refresh를 만들지 않고 그 응답을 수집했다. 따라서 최초 ready 관측을 실제 코드·에셋 검증 완료 시각으로 대체하지 않는다.

첫 미리보기 PNG는 하늘/배경만 보였고 같은 Editor의 두 번째 PNG에는 모델이 표시됐다. 코드와 텍스처 검사는 둘 다 통과했다. 첫 렌더의 원인은 미확정이며 VR-03의 Partial과 기존 제한을 유지한다.

첫 정상 stop 호출은 **32.12초** 뒤 `EditorStopTimeout`이었다. 로그를 확인하는 동안 같은 Editor가 정상 quit을 마쳤으며, 같은 stop API 재시도 **0.80초**에서 소유 application 프로세스·working set·private bytes 0을 확인했다. 최초 호출 시작부터 재시도 반환까지는 **61.24초**로, 검사와 호출 간 지연을 포함한다. 강제 종료는 하지 않았다. 실패 응답을 성공 응답으로 덮어쓰지 않았다.

초기 Git 준비에서는 mount 뒤 `git-prepare`를 호출해 `UnsafePath`로 거부됐다. 정상 unmount → prepare → remount → attach로 순서를 바로잡았고 거부와 재시도 증거를 모두 남겼다. 해당 준비와 검증 수집·stop 재시도는 운영 비용이며 반복 표본에서 삭제하거나 첫 준비 성공만으로 숨기지 않는다.

## 같은 닫힌 저널의 재연결

정상 종료한 cache journal **2,269,880,373B·331,093 frames**와 source journal **9,442,536B**를 고정했다. 순서는 1회차 baseline/current, 2회차 current/baseline, 3회차 baseline/current다. 매번 새 provider의 PID/시작 시각·최신 mounted·실제 mount를 확인하고 후보 파일을 읽은 뒤 정상 unmount했다. 6회 모두 두 journal hash가 전후 같았고 I/O 오류·열린 handle·남은 provider는 0이었다.

| 구현 | cache replay 초, 1/2/3회 | replay 중앙값 초 | mount CLI 초, 1/2/3회 | mount 중앙값 초 |
| --- | --- | ---: | --- | ---: |
| baseline | 29.73 / 32.01 / 36.52 | 32.01 | 31.65 / 33.84 / 38.47 | 33.84 |
| current | 26.17 / 22.22 / 19.89 | 22.22 | 28.08 / 23.70 / 21.44 | 23.70 |

이 입력에서 replay 중앙값은 약 30.6%, mount CLI 중앙값은 약 30.0% 감소했다. 모든 run 전후 hash 읽기와 반복 재생으로 OS cache가 warm이며 cache를 비우지 않았다. CPU/전력/JIT/GC 상태를 고정하지 않은 3회 표본이다. 과거 VR/RI와 실행 환경·입력을 동일하게 맞춘 비교가 아니며 일반 Unity 개선률로 확대하지 않는다.

6회 모두 cache 디렉터리 파일 할당 **2,269,884,968B**, source 디렉터리 **9,446,008B**로 같았다. Runtime의 prefix 재사용은 디스크 공간 절감 기능이 아니며 관리 메모리 할당·파일 할당·작업 전체 재개 시간을 서로 대체하지 않는다. 고정 입력 hash는 cache `ed73d6ac9ee5a797c10acf96af45423b79f9d4504521f14d0c0b3da448c20dc5`, source `69df69560bf04b8f67fa2d804125ab0d6faadc849801e91673de37007889d7f2`다.

index 적용 중앙값은 27.56초 → 17.61초였다. current의 연산별 중앙값은 rename 9.28초(4,032 frames), write 8.17초(195,268 frames)이며 나머지 create/delete/metadata/resize는 각각 0.062/0.001/0.048/0.058초다. 각 run의 frame/index tick 합계는 전체와 일치했다. 연산별 중앙값의 합을 전체 중앙값과 동일시하지 않으며 baseline의 없는 연산별 계수는 unknown이다. 남은 비용을 rename 접두사 할당만으로 설명하지 않는다.

## 실제 Editor 재개와 파일 할당

각 버전 3회 모두 현재 요청의 compile 성공, 로드 코드 `23`, 1024×1024 텍스처와 mesh 3개를 확인했다. 같은 후보·cache를 쓰지만 Editor가 journal에 기록하므로 입력은 누적된다. 이전 실행의 journal을 복원하거나 별도 Library로 복제하지 않았다. 아래 표는 실제 실행 순서다. ready는 최신 heartbeat, 검증 완료는 명시적 compile 응답과 코드/에셋 검사까지이며 stop은 한도 초과 뒤 최종 회수 확인까지 포함한다.

| 순서 / 구현 | mount 초 | Editor 시작→ready 초 | mount 시작→ready 초 | mount 시작→검증 완료 초 | stop·재확인 초 |
| --- | ---: | ---: | ---: | ---: | ---: |
| 1 / baseline-1 | 30.78 | 126.17 | 157.03 | 211.45 | 41.74 |
| 2 / current-1 | 35.48 | 131.37 | 166.89 | 369.71 | 67.50 |
| 3 / current-2 | 39.56 | 109.28 | 148.87 | 188.17 | 37.45 |
| 4 / baseline-2 | 40.86 | 74.79 | 115.68 | 146.65 | 36.74 |
| 5 / baseline-3 | 46.29 | 71.30 | 117.62 | 148.36 | 36.34 |
| 6 / current-3 | 47.70 | 71.51 | 119.23 | 150.21 | 36.11 |

baseline/current 중앙값은 mount 40.86/39.56초, mount 시작→ready **117.62/148.87초**, mount 시작→검증 완료 **148.36/188.17초**, stop·재확인 36.74/37.45초다. 이 순차 실행은 입력이 다르고 초기화 편차도 크므로 RI 변경의 전체 Editor 재개 개선이나 회귀를 분리해 입증하지 못한다. 같은 닫힌 저널의 약 30% 단축을 전체 재개에 적용하지 않는다.

current-1에서 `EditorFocusRequired` 3건을 보존했다. 소유 PID/시작 시각과 실제 반환된 Unity 창을 대조한 뒤 창을 활성화하고 네 번째 refresh가 접수됐다. 같은 Editor·journal·최초 ready 기록을 이어 썼으며 369.71초에는 포커스 진단/복구와 계측 스크립트 재개 지연도 포함한다. 다른 5회에는 focus 재시도가 없었다.

6회 모두 최초 stop 호출은 31.72~32.24초 뒤 `EditorStopTimeout`이었다. 같은 소유 Editor의 자연 종료를 기다린 뒤 같은 stop API로 재확인해 각각 프로세스·메모리 0을 확인했다. cold를 포함한 7회에서 강제 종료는 없었지만, 무개입 단일 호출 종료 성공으로 표시하지 않는다. 최종 unmount는 각 1.07~1.71초였다.

cache journal은 cold 종료 뒤 **2,269,880,373B**에서 반복 6회 뒤 **2,376,470,766B**로 **106,590,393B** 늘었다. 회차별 증가는 실행 순서대로 17,873,042 / 18,317,816 / 17,881,115 / 17,519,575 / 17,531,165 / 17,467,680B다. source journal은 9,442,536B에서 9,616,656B가 됐다. 모두 논리 길이이며 파일 할당과 구분한다.

current의 write index 적용은 고정 입력 중앙값 8.17초에서 첫 재시작 입력 16.15초, 두 번째 입력 22.36초로 증가했다. 반복 Editor가 남기는 기록과 index 적용 비용을 함께 조사할 근거이며, 경로별 payload 크기만으로 CPU 병목을 특정하지 않는다. 용량/재개 비용을 줄이는 후속 후보는 누적 write의 범위 index 비용이며 flush 변경·자동 정리·새 제품 모드를 이번에 구현하지 않았다.

## 파일 할당 장부

`FILE_STANDARD_INFO.AllocationSize`로 다음 디렉터리의 파일 할당을 관측했다. 약 10초 간격과 단계별 표본 **366개**의 모든 root 관측이 complete였으나 순간 최고치를 보장하지 않는다. Library 쓰기는 cache journal 안에 있으므로 별도 Library 비용으로 더하지 않는다.

| 시점 | cache 할당 B | source 할당 B | 소유 workspace 파일 합계 B |
| --- | ---: | ---: | ---: |
| cold 종료 | 2,269,884,968 | 9,446,008 | 2,282,061,120 |
| baseline-1 종료 | 2,287,755,816 | 9,474,680 | 2,299,666,080 |
| current-1 종료 | 2,306,073,128 | 9,503,352 | 2,318,012,064 |
| current-2 종료 | 2,323,956,264 | 9,532,024 | 2,335,923,872 |
| baseline-2 종료 | 2,341,474,856 | 9,560,696 | 2,353,467,040 |
| baseline-3 종료 | 2,359,005,736 | 9,589,368 | 2,371,026,592 |
| current-3 종료 / 결과 보존 | 2,376,475,176 | 9,618,040 | 2,388,524,704 |

소유 workspace 파일 할당의 관측 최고는 **2,388,524,704B(약 2.389GB)**였다. 결과 보존 시의 구성은 worker 8,752B, 후보 12,848B, source 9,618,040B, cache 2,376,475,176B, control 2,372,560B, snapshot 제어 파일 37,328B다. 원본 Library는 처음부터 없었고 생성하지 않았다.

공통 Git 파일 할당은 생성 전 6,117,288B → 결과 보존 뒤 6,122,568B로 **5,280B** 증가했다. Git 등록을 유지하는 중간 시점에는 6,130,080B였다. 독립 결과 branch는 이 공통 Git에 남으며 C:의 별도 bundle/검증 저장소는 측정 증거 root에 들어간다. bundle의 논리 길이는 6,000,213B이고 export·fetch는 1.19초였다.

결과 보존 뒤 표본의 증거 root는 23,950,000B다. 이 표본의 workspace + Git 기준 대비 증가분 + 증거 파일 할당 합계는 **2,412,479,984B**였다. 바이너리 2개 묶음과 독립 결과를 여기에 다시 더하지 않는다. 이후 control/receipt 보존 복사·계측 결과와 문서 검사 자료는 별도 후속 관측에 포함되므로 이 값을 최종 증거 디렉터리 크기나 실제 전역 peak로 쓰지 않는다.

격리 V:의 공유 VSS는 생성 전 사용/할당 0/0B, import 도중 27,000,832/100,663,296B, 회수 전 27,131,904/100,663,296B였다. 이는 볼륨 전체 관측이며 파일 할당 합계에 workspace별 비용으로 귀속해 더하지 않는다. 별도 C:의 기존 VSS도 관측하되 이 workspace 비용으로 계산하지 않는다. 파일 할당에는 MFT·디렉터리 메타데이터·OS cache·root 밖 Unity 전역 cache/로그·미관측 peak가 빠져 있다. 기존 VHD의 backing 파일 크기 감소나 호스트 디스크 회수율은 검증하지 않는다.

최종 닫힌 저널은 읽기 전용 진단으로 모든 checksum·sequence와 byte 합계를 확인했다. 이 진단은 비교 실행 뒤 수행했으며 cache 분석에 약 7분이 들었다. 시작 시각은 프로세스 조회의 초 단위 관측이므로 정밀 wall-clock 계측과 구분하고 Editor 재개 수치에 합산하지 않는다.

| 저널 | frames | 논리 길이 B | 유효 payload B | 대체 payload B | 메타데이터/frame B |
| --- | ---: | ---: | ---: | ---: | ---: |
| cache | 407,225 | 2,376,470,766 | 1,828,055,921 | 359,787,187 | 188,627,658 |
| source | 2,570 | 9,616,656 | 8,438,867 | 76,681 | 1,101,108 |

두 저널의 유효 payload 합계 1,836,494,788B 중 Library는 1,827,981,842B(약 99.54%)였다. 대체 payload를 즉시 회수 가능한 물리 공간이나 정리 중 peak 감소로 환산하지 않는다. 실제 journal은 회수했으며 최종 hash와 분해 결과를 보존했다.

## 결과 보존과 회수

독립 결과는 **`cd968022c81020ee7975188115de1c8019859062`**이며 `codex/ru27/view-a1`과 `refs/splinter/snapshot-results/ru27-view`에 보존했다. 결과는 의도한 4개 파일만 포함하고 임시 Unity bridge는 제외했다. C:의 `independent-result.bundle`과 외부 object alternates가 없는 `independent-result.git`에서도 같은 revision·코드·메타데이터·추가 파일을 확인했다. bundle SHA-256은 `491fffe36b1e40627287c223d3861345d00656cb608941f7479fb761154468ec`다.

보존용 remount 54.21초, 결과 수집 2.50초, unmount 1.55초, Git 등록 해제 1.58초, 별도 bundle export/fetch 1.19초를 각각 기록했다. 마지막의 긴 replay도 완료 비용에 포함되므로 작은 후보 게시 시간만으로 전체 작업 비용을 대표하지 않는다.

첫 회수 감사는 보존 API가 새로 만든 `refs/splinter/snapshot-results/ru27-view`를 기존 ref 변경으로 판단해 삭제 전에 중단했다. 차이는 해당 소유 결과 ref 1개뿐이고 기존 19개 ref의 변경/소실은 없었다. 정확한 ref 이름과 결과 revision을 별도로 확인하도록 시험 감사의 허용 목록을 보완한 뒤 원래 19개 ref 전체를 다시 비교했다. 원본이나 제품 guard를 바꿔 통과시키지 않았다.

최종 감사에서 원본 HEAD·clean 상태·추적 파일 90개의 raw hash·기존 ref 19개가 같았다. 16개 provider 종료 기록과 cold 포함 7개 Editor 종료 기록에서 소유 PID/시작 시각, 실제 mount 부재·open handle/I/O 오류 0, application 프로세스·working set·private bytes 0을 확인했다. 측정 helper 3개도 종료를 확인하고 소유 경로·reparse 부재를 검사한 뒤 데이터만 회수했다. 데이터 삭제 구간은 0.61초였으며 전체 감사·보존·VSS 해제 시간이 아니다.

소유 snapshot `{529A8CED-493B-48C4-A2CC-039DFCDEC294}` 해제 뒤 V:의 snapshot 0·VSS 사용/할당 0/0B, 기존 C: snapshot `{78AD7813-DF73-4845-8513-F14CC6E87331}` 생존을 확인했다. 이는 다른 볼륨의 기존 ID 보존 증거이며 일반 비소유 백업 공존이나 외부 동시 삭제 보장으로 확대하지 않는다. 원본과 모든 기존 ref, 독립 결과의 Git fsck도 다시 통과했다.

회수 뒤 소유 workspace 디렉터리 파일 할당은 **0B**, 원본 Library는 계속 부재, 공통 Git 증가분은 5,280B였다. 해당 시점의 보존 증거 root는 **26,439,696B**이며 독립 결과·실행 묶음·닫힌 제어 파일·측정 자료를 포함한다. 이후 작은 요약/검사 출력은 이 표본 밖이다. V: 여유 공간은 생성 전 16,952,979,456B, snapshot 회수 뒤 16,952,954,880B였으며 전체 물리 회수율로 해석하지 않는다.

마지막으로 제한 관리자 세션이 VHD를 분리하고 종료했다. `Get-DiskImage.Attached = false`, V: 부재와 해당 관리자 PID 종료를 확인했다. 디스크가 없는 상태에서도 C: 독립 저장소의 revision·내용·fsck와 bundle hash가 일치했다. 최종 상태는 owned mount/application/provider/Git 등록/delta/snapshot 모두 0이며 결과·측정 증거와 기존 VHD 파일 자체는 보존했다.

## 판정과 증거

| ID | 판정 | 범위와 제한 |
| --- | --- | --- |
| RU-01 | Passed | 원본/hash/ref·작은 실행 묶음·공통 주변 계층 확인. RI 자동 300/0/1은 같은 소스의 이전 증거 재사용 |
| RU-02 | Passed, 제한 유지 | 강제 종료 없는 cold import·동일 요청의 지연 compile·실제 코드/에셋. 첫 PNG 빈 화면과 stop timeout 재시도 유지 |
| RU-03 | Passed, 제한된 관측 | 같은 저널 mount 3회씩, 누적 입력 Editor 재개 3회씩과 파일 할당. 전체 Editor 개선률·정밀 global peak 미입증 |
| RU-04 | Passed | 독립 결과·bundle/저장소·원본/기존 ref/타 볼륨 snapshot 보존·소유 자원 회수·VHD 분리 뒤 결과 검증 |

전체 SF-03은 Partial, VR-03의 첫 렌더 원인은 미해결이다. 정상 quit 뒤 stop 재확인이 매번 필요했던 제한, focus 복구 1개 표본과 입력 누적·warm OS cache·소수 표본을 유지한다.

원시 증거는 `artifacts/storage-foundation/ru-20260927/`에 보존한다. `code-and-scope.json`, `binary-manifests.json`, `protected-source.json`, `cold-observation-complete.json`, `cold-stop-completion.json`, `replay-comparison.json`, `editor-comparison.json`, `space-*.json`, `measurement-summary.json`, `*-final-accounting.json`, `pre-reclamation-audit.json`, `independent-result.json`, `closeout.json`, `final-environment-check.json`이 주 근거다. 모든 최초 거부/재시도 응답·Editor 로그·미리보기·고정 관리자 요청/응답과 계측 스크립트도 남겼다. 최종 요약은 `summarize.py`가 보존된 관측에서 계산한다. flush 정책·기본 모드·Coordination 제품 연결·대형 PV는 이 검증 범위 밖이다.
