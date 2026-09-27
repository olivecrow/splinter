# 누적 write 범위 index 검증

`wi-20260927`은 [WI-01~05 범위](cumulative-write-range-index.md)를 **scoped-complete-with-limitations**로 마감한 실행 기록이다. 기준 구현은 `2cfcad0`의 무계측 Runtime, 계측 구현은 전체 목록 복사·정렬을 유지한 Runtime, 최종 후보는 정렬된 extent의 영향 구간만 교체하는 Runtime이다. 고정 합성·새 작은 Unity 저널·실제 Editor·독립 결과와 회수까지 확인했다. 전체 Editor 재개 개선과 Library 물리 절감은 입증하지 않았다.

## 합성 원인 판별과 회귀

기존 `append`/`overlap`/`file-renames`의 동일 입력 journal/state hash와 출력 검증이 기준/최종 후보에서 일치했다(`evidence/existing-profile-final-regression.json`). 추가한 네 고정 입력도 checksum·sequence·독립 기대 파일 hash·state/input hash가 세 구현에서 일치했다. 새 journal 길이는 순차 4,096회 5,662,179B, 순차 16,384회 22,676,107B, 같은 범위 덮어쓰기 22,517,584B, 128개 파일 분산 22,714,867B이며 합계 73,570,737B다. 각 입력은 32MiB 상한 안에 있다.

각 입력을 기준/계측/최종 후보 순서를 회차마다 바꿔 세 번씩 별도 프로세스에서 replay했다. 전체 입력 hash를 실행 전에 읽으므로 warm OS cache, 3회 소수 표본이다. 아래 값은 각 구현의 **중앙값**이며 `write`는 `replayIndexOperations.write.applyTicks`다. 표본별 원시값과 바이너리 hash는 `artifacts/storage-foundation/wi-20260927/evidence/final-qualified-*.json`에 보존한다. `write-compacted` 계수 분리 이전 예비 비교도 별도 `qualified-*` 파일로 보존했다.

| 입력 | 구현 | replay 초 | write 초 | 누적 관리 할당 MiB | peak working set MiB |
| --- | --- | ---: | ---: | ---: | ---: |
| 한 파일 순차 4,096 | 기준 / 계측 / 후보 | 0.545 / 0.565 / 0.061 | 0.480 / 0.497 / 0.003 | 71.5 / 71.5 / 7.4 | 54.4 / 55.3 / 44.2 |
| 한 파일 순차 16,384 | 기준 / 계측 / 후보 | 3.032 / 3.011 / 0.196 | 2.892 / 2.873 / 0.011 | 1,053.3 / 1,053.3 / 28.4 | 64.4 / 64.7 / 59.1 |
| 같은 범위 덮어쓰기 16,384 | 기준 / 계측 / 후보 | 0.196 / 0.197 / 0.197 | 0.011 / 0.012 / 0.013 | 29.1 / 29.1 / 28.5 | 58.6 / 58.7 / 58.5 |
| 128개 파일 분산 16,384 | 기준 / 계측 / 후보 | 0.243 / 0.245 / 0.202 | 0.052 / 0.055 / 0.011 | 37.5 / 37.5 / 28.8 | 60.1 / 59.9 / 59.2 |

계측 구현에서 한 파일 순차 16,384회의 `write` 중앙값 2.873초 중 전체 정렬은 2.299초, 목록 순회·복사는 0.566초였다. 직전 extent 수 합계는 134,209,536개, 최대 16,383개였고 순차 입력에서 같은 수만큼 방문·복사했다. 후보는 동일 분포에서 방문 16,383개, 복사 0개다. 같은 frame 수의 반복 덮어쓰기는 목록 길이가 작아 `write` 약 0.01초다. 그러므로 비용은 frame 수 자체보다 한 파일에 누적된 extent 목록을 매번 전체 처리하는 방식에 따른다. 별도 계측 오버헤드는 무계측/계측 3회 범위를 함께 공개하며 중앙값 차이가 표본 산포보다 작을 때 개선/회귀로 판정하지 않는다.

후보는 append 경로에서 끝에 추가하고, 그 외에는 정렬된 extent의 겹침 구간을 이진 탐색해 국소 교체한다. 기존 왼쪽·오른쪽 fragment와 journal offset을 유지하고, list 용량을 먼저 확보한 뒤 변경한다. 저널 형식, frame checksum·sequence·seal, acknowledged write flush는 바꾸지 않았다. 임의 겹침·hole·truncate·확장·재연결의 byte/hash와 기존 journal 회귀, `replayWriteRangeIndex` schema의 음수 거부·이전 계수 누락 호환을 자동 시험에 추가했다. 최종 후보는 `write-compacted`를 일반 `write` 계수에서 제외한다. 최종 표준 `pwsh ./scripts/build.ps1`은 locked restore·Release 빌드 경고/오류 0, 자동 **301 통과·0 실패·선택적 ReFS 1 skip**이며 TRX는 `evidence/standard-build-tests-capability-final.trx`에 보존했다.

네 합성 입력 디렉터리의 논리 크기/FILE_STANDARD_INFO 파일 할당은 73,596,387/73,618,960B, 기준/계측/최종 후보 probe 실행 묶음의 파일 할당은 각 356,696B, 기준/후보 Unity CLI 묶음은 각 3,220,144B였다. 분리 전 예비 후보 probe 묶음도 별도로 남겼다. 누적 관리 할당은 시간 동안의 총량이고 동시 working set이나 디스크 절감량이 아니다. 특히 합성 16,384회 순차 입력의 관리 할당 감소를 Unity Library 물리 절감으로 환산하지 않는다.

## 실제 작은 Unity와 회수

실제 입력은 이전 RU 저널을 재사용하지 않고, 기존 격리 NTFS VHD의 작은 Unity fixture에서 새 `wi27` 소유 snapshot으로 만든다. 시작 원본은 clean HEAD `58f81b5bc6ff8a940d8a90872bd3fed858a54cc1`, 추적 파일 90개, 기존 ref 21개, 원본 Library 없음이다. 시험 볼륨 기존 snapshot은 0개이며 C:의 기존 snapshot은 별도 보존 대상이다. 코드의 `Calculate(3)`을 23으로, 텍스처 import 상한을 1024로 바꾼 작업을 제품 `snapshot-spike`의 mount/Git/Unity 경로에서 검증한다. baseline/current 실행 묶음의 주변 CLI/Git/Unity/Windows 파일은 같고 Core/Runtime만 다르다. 최초 Git prepare의 샌드박스 거부와 권한을 갖춘 동일 요청 재시도는 별도 원시 증거로 남긴다.

기준/최종 후보의 41개 파일 실행 묶음에서 **Core/Runtime DLL만 다름**을 hash로 확인했다. 최종 후보 Runtime SHA-256은 `03C9E464FB03E25006A092D55B192EACA846FAA27484C16385DB3519585039B0`이다. 최초 Editor 준비는 후보의 일반 write 계수 분리 직전 DLL로 시작했으며, 정상 stop 뒤 최종 DLL을 고정했다. 아래 닫힌 저널 비교와 후보 Editor 재개는 모두 최종 DLL을 사용한다. 초기 `git-prepare`의 샌드박스 거부는 파일/저장소 문제가 아니었고, 권한을 갖춘 동일 요청으로 성공했다. 거부 응답과 재시도는 별도로 보존했다.

### 최초 준비와 같은 저널 비교

새 snapshot의 최초 payload 복사는 0B다. 작은 Unity의 첫 Editor 시작→`ready`는 **1,180.49초**, 정상 stop은 30.59초였다. 첫 import 동안 별도의 빌드/자동 시험도 실행돼 이 준비 시간은 격리된 성능 기준이 아니다. 같은 PID의 로드 코드 `Calculate(3)=23`을 확인했다. stop/unmount 뒤 새 저널 **2,272,161,094B**, **331,992 frames**를 SHA-256 `2A30E9F76E1F5C2FEB1AD9783EA05489CEEBD6D1DA4EAD6664C8D49998E4FA0F`로 봉인했다. 여기에는 일반 `write` 196,118 frames가 있다.

같은 닫힌 저널의 순서는 1회 기준/후보, 2회 후보/기준, 3회 기준/후보다. 매번 새 provider PID·시작 시각·최신 mounted 상태와 실제 mount를 확인하고, 코드·텍스처 설정을 읽은 뒤 정상 unmount했다. 여섯 번 모두 입력 길이/hash가 같고 I/O 오류·열린 handle·남은 mount/provider는 0이었다.

| 구현 | mount CLI 초, 1/2/3회 | 중앙값 초 | replay 초, 1/2/3회 | 중앙값 초 | write index 초, 1/2/3회 | 중앙값 초 |
| --- | --- | ---: | --- | ---: | --- | ---: |
| 기준 | 11.67 / 10.87 / 10.96 | 10.96 | 10.56 / 10.03 / 10.07 | 10.07 | 5.73 / 5.75 / 5.59 | 5.73 |
| 최종 후보 | 5.90 / 5.62 / 5.60 | 5.62 | 4.80 / 4.76 / 4.71 | 4.76 | 0.142 / 0.131 / 0.125 | 0.131 |

이 입력에서 mount 중앙값은 약 48.8%, replay 중앙값은 약 52.7% 감소했다. 최종 후보의 `replayWriteRangeIndex.replaceCalls`는 `write` 196,118 frames와 같고, 직전 extent 수 합계 334,711,337개·최대 10,805개, 방문 1,490,638개·목록 이동 113,168,556개였다. 국소 교체에서도 목록 이동이 0은 아니지만 전체 복사·정렬이 없어졌다. 비교는 hash를 매번 읽는 warm OS cache·고정 순서·3회 소수 표본이다. 전체 Editor 준비 시간 개선률로 환산하지 않는다.

### 실제 Editor 재개와 비용 장부

최종 후보로 한 번 재개한 Editor는 mount **5.87초**, 시작→`ready` **63.23초**, mount 시작→`ready` **78.38초**, mount 시작→현재 요청의 compile 성공 **119.42초**였다. 로드된 코드 23, 1024×1024 텍스처·mesh 3개와 동일 PID를 확인했다. 첫 PNG는 빈 화면이고 같은 Editor의 두 번째 PNG는 모델을 렌더했다. 원인은 미확정이다. 첫 정상 stop은 **31.58초**에 `EditorStopTimeout`을 반환했지만 로그상 종료가 진행됐고 강제 종료 없이 같은 PID가 사라졌다. 같은 stop API 재확인에서 소유 프로세스·working set·private bytes 0, unmount **1.50초**였다. 첫 호출 시작부터 재확인 완료까지 **84.56초**에는 관찰·재시도 사이 지연도 들어 있다.

Editor 재개는 journal을 **17,278,621B** 늘려 최종 **2,289,439,715B**가 됐다. 따라서 이 입력은 위 6회 동일 입력 비교에 넣지 않았다. 단계별 `FILE_STANDARD_INFO` 관측에서 닫힌 비교 6회의 delta 파일 할당은 모두 **2,272,162,344B**로 같았다. 후보 Editor 종료/결과 보존 시 소유 delta·control·snapshot 합계 관측 최고는 **2,291,852,832B**다. 순간 최고치, MFT·디렉터리 메타데이터, OS cache, VHD backing 파일 감소는 미계측이다. 최초 `before-cold` 스크립트가 delta root를 빠뜨린 표본은 보존하고 초기 delta 할당을 `unknown`으로 둔다. 수정한 이후 단계별 표본은 6개 root 모두 complete다.

격리 V:의 공유 VSS는 snapshot 전 사용/할당 **0/0B**, 회수 직전 **27,197,440/100,663,296B**, 회수 뒤 **0/0B**였다. 볼륨 전체 값이며 소유 workspace의 파일 할당에 더하지 않는다. 공통 Git의 파일 할당은 최초 **6,135,360B**, 결과 수집/등록 해제 뒤 **6,124,208B**로 관측돼 새 결과 ref만의 물리 비용은 분리되지 않는다. 독립 bundle 논리 길이는 **5,999,972B**다. `space-after-release.json`의 C: 검증 증거 root는 **98,303,776B**의 파일 할당을 관측했으며 이후 검사 로그·문서 수정은 이 표본 밖이다. 합성 입력과 실행 묶음은 증거 root에 포함되므로 중복 합산하지 않는다.

### 독립 결과와 최종 판정

결과 revision **`1a840215f3de41a25eeb05a2027b3bd081a1f954`**은 `codex/wi27/unity`와 `refs/splinter/snapshot-results/wi27-unity`에 보존했다. 바뀐 파일은 코드와 텍스처 `.meta` 2개뿐이고 임시 Unity bridge는 제외됐다. C:의 bundle·외부 alternates 없는 bare 저장소에서 같은 revision·내용·`git fsck`를 확인했다. 결과 수집 뒤 원본 HEAD·추적 파일 90개의 hash·clean 상태·기존 ref 21개·원본 Library 부재가 유지됐다. 소유 provider 9개 종료 기록, Editor 2개 종료, Git 등록·mount·delta·snapshot 0을 확인하고 소유 경로만 회수했다. 기존 C: snapshot `{78AD7813-DF73-4845-8513-F14CC6E87331}`도 유지됐다. VHD 분리 뒤 `Get-DiskImage.Attached=false`, V: 부재, 제한 관리자 PID 종료와 C: 결과 revision/bundle hash/fsck 일치를 다시 확인했다.

| ID | 판정 | 제한 |
| --- | --- | --- |
| WI-01 | Passed | 과거 계수 누락은 unknown, `write-compacted`는 일반 write 계수에서 제외 |
| WI-02 | Passed | 고정 합성 입력·warm cache·3회 소수 표본 |
| WI-03 | Passed | 국소 범위 교체 한 가지 변경, flush/저널 형식 불변 |
| WI-04 | Passed, 제한된 관측 | 같은 새 닫힌 저널의 mount 각 3회·후보 실제 Editor 1회; 전체 Editor 개선 미입증, 첫 PNG 빈 화면·stop 재확인 |
| WI-05 | Passed, 제한된 관측 | 독립 결과·원본/ref 보존·회수·VHD 분리 확인; 순간 물리 peak와 초기 delta 할당 일부 unknown |

원시 증거는 `artifacts/storage-foundation/wi-20260927/evidence/`의 합성 비교·바이너리 hash·최종 TRX와 같은 WI root의 `fixed-replay-comparison.json`, `space-*.json`, `closed-unity-input.json`, `candidate-editor-*.json`, `independent-result.json`, `pre-reclamation-audit.json`, `source-and-result-after-release.json`, `closeout.json`, `final-environment-check.json`, 독립 bundle/저장소다. SF-02 전체와 대형 PV/기본 모드, 첫 렌더 원인, Library 용량 목표는 이 결과로 완료 처리하지 않는다.
