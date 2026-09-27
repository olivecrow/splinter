# Live journal 쓰기 비용 검증

`jw-20260927`은 [JW-01~04 범위](../write-profiling.md)를 **scoped-complete-with-limitations**로 마감한 실행 기록이다. 자동·고정 합성 18회와 새 작은 Unity의 첫 import·후속 수정·닫힌 journal 재연결·독립 결과·원본 보존·회수·VHD 분리 뒤 결과 검증을 완료했다. 이번 변경은 계측이며 acknowledged write의 개별 durable flush, 저널 형식과 범위 index 동작을 유지한다. Runtime에만 동시 group flush를 추가하는 후속 구현은 선정하지 않았다.

## 계측 계약과 자동 시험

`liveJournalWrites`는 `create/write/resize/metadata/rename/delete`의 최대 6개 연산별 commit 진입·durable commit·실패, payload/저널 바이트, 준비·append·flush·index 적용·전체 호출 시간을 집계한다. 중첩을 제외한 mutation gate 호출/대기와 flush 시작/끝의 다른 대기 mutation 호출 수를 기록한다. histogram의 5칸은 0, 1, 2~3, 4~7, 8개 이상이다. 관측 배열은 복사본이고 과거 누락/null은 unknown이다. no-op와 commit 이전 거부는 mutation 호출에는 포함될 수 있으나 commit 시도에는 포함되지 않는다. 명시적 Flush와 tail 복구 flush는 전체 flush 계수에만 들어간다.

표준 `pwsh ./scripts/build.ps1`의 locked restore·Release 빌드는 경고/오류 0, 자동 **303 통과·0 실패·선택적 ReFS 1 skip**이다. 여섯 연산·no-op·사전 거부·명시적 Flush 구분, 반환 배열 격리, 예산 거부 뒤 journal hash와 재연결, 두 schema의 일치와 음수 histogram 거부를 확인했다. `evidence/build-instrumentation.log`와 `evidence/tests-303-passed.trx`가 해당 실행의 증거다.

## 고정 합성 쓰기 18회

기준 구현은 WI 완료 커밋 `091a5fc`, 후보는 live 계측을 추가한 구현이다. 공통 WriteProbe 실행 묶음 9개 파일 중 Core/Runtime DLL만 다르다(`evidence/probe-binary-alignment.json`). 공통 probe DLL SHA-256은 `7A2202890048EC8ECDD426592A818C4B1C0042046B58E124B1832CC9AC5B7E55`이고 각 DLL과 실제 Unity CLI 묶음의 hash는 `evidence/binary-manifest.json`에 보존한다.

| DLL | 기준 SHA-256 | 계측 SHA-256 |
| --- | --- | --- |
| Core | `1FEE11489294A4EA2892117E6DCEFC4768686D8D99215B1A1D200160381865A5` | `2D13960701DA865FDCFAEFB3125CD701E920B72F3CD83661124DD36EAEA1A9C9` |
| Runtime | `03C9E464FB03E25006A092D55B192EACA846FAA27484C16385DB3519585039B0` | `27369D4DB5B532F7093CAEA47763CF7C6F3DC6FDEDD9CC4345742D25518D76CF` |

순차 1,024×4KiB, 동시 4명×256×4KiB, metadata 1,024회 프로필을 구현마다 세 번씩 새 journal에서 실행했다. 회차별 순서는 기준/계측, 계측/기준, 기준/계측이다. 모두 독립 기대 byte/hash·metadata, live 출력과 읽기 전용 replay, replay 전후 입력 hash 보존을 통과했다. 구현 사이의 `contentHash`가 같고 journal hash는 각 실행의 ID·시각이 달라 개별 입력 보존에만 사용한다.

| 입력 | 기준 시간 초 최소 / 중앙 / 최대 | 계측 시간 초 최소 / 중앙 / 최대 | 누적 관리 할당 B 중앙값 기준 / 계측 | peak working set B 중앙값 기준 / 계측 |
| --- | ---: | ---: | ---: | ---: |
| serial-1024 | 2.3823 / 2.3839 / 2.7678 | 2.2635 / 2.4874 / 2.9379 | 1,227,072 / 1,227,472 | 63,868,928 / 64,606,208 |
| parallel-4x256 | 2.1870 / 2.1950 / 2.2684 | 2.1841 / 2.1856 / 2.2135 | 1,295,232 / 1,295,632 | 64,647,168 / 65,703,936 |
| metadata-1024 | 2.1803 / 2.1816 / 2.1937 | 2.1736 / 2.1774 / 2.1860 | 1,106,448 / 1,106,848 | 51,568,640 / 52,678,656 |

시간은 파일 생성 뒤 호출 loop/thread 시작부터이며 최종 명시적 Flush·출력 검증·replay는 제외한다. working set은 replay 검증 뒤까지의 프로세스 최고 관측값이라 같은 구간이 아니다. warm OS cache·소수 표본이고 시간 범위가 겹쳐 계측 오버헤드의 속도 개선/회귀를 확정하지 않는다. 누적 관리 할당은 동시 메모리나 디스크 비용이 아니다.

계측 세 번을 합친 flush 시작/끝 histogram은 serial과 metadata에서 각각 `[3075,0,0,0,0]`, parallel에서는 둘 다 `[140,192,2752,0,0]`이며 표본 최대 대기자는 3명이다. parallel의 mutation 대기 누적 중앙값 5.9884초는 loop 벽시계 중앙값 2.1856초보다 크다. 여러 대기자의 시간이 겹치므로 합산할 수 없고, 대기 호출은 후속 no-op/거부도 포함할 수 있어 곧바로 batch 크기가 되지 않는다.

각 journal의 논리 길이는 serial 4,604,690B, parallel 4,604,225B, metadata 351,460B로 두 구현에서 같다. 합성 전체 36개 파일의 논리 길이/파일 할당은 **57,369,006/57,416,352B**여서 실행 묶음 256MiB 한도 안이다. 각 journal도 16MiB 이하다. 기준/계측 probe 묶음의 파일 할당은 610,648/622,936B, CLI 묶음은 3,220,144/3,236,528B다. 이 값은 `FILE_STANDARD_INFO` 관측이며 MFT·OS cache·VHD backing 공간이 아니다.

## 실제 작은 Unity

기존 격리 NTFS VHD와 작은 fixture에서 새 `jw27-small-unity` 소유 snapshot/delta를 만들었다. 시작 원본은 clean HEAD `58f81b5bc6ff8a940d8a90872bd3fed858a54cc1`, 추적 파일 90개, 기존 ref 23개, 원본 Library 없음이다. V:의 기존 snapshot과 VSS 사용/할당은 0/0/0이며 C:의 기존 snapshot은 별도 보존 대상이다. 과거 WI의 21개 ref나 journal을 새 실행의 기준으로 재사용하지 않는다.

개발용 `snapshot-spike`의 mount/Git/Unity 경로에서 `Calculate(3)=23`, 텍스처 import 상한 1024를 준비했고 후속 코드는 24로 바꿨다. 제품 workspace 모드 전체 지원 증거로 확대하지 않는다. 최초 payload 복사는 0B이고 Library 전체 복사나 hydration을 사용하지 않는다. 제한 관리자 세션은 기존 격리 VHD와 고정된 소유 snapshot 한 개의 capture/observe/release/detach만 처리한다.

### 최초 준비·후속 수정·정상 종료

새 snapshot의 첫 Editor 시작→ready는 **1,186.16초**다. 같은 PID `59580`의 첫 명시적 refresh 시작→현재 fingerprint의 compile 성공은 **36.72초**였다. 로드 코드 23, 텍스처 1024×1024와 mesh 3개를 확인했다. 첫 PNG는 빈 화면이고 같은 Editor의 두 번째 PNG에는 모델이 렌더됐다. 후속 코드 수정 시작→현재 fingerprint의 compile 성공은 **32.32초**였으며 다른 assembly MVID에서 코드 24와 같은 에셋을 확인했다. 후속 domain reload 뒤의 첫 PNG도 빈 화면이다. 첫 렌더 원인은 미확정이며, 후속 코드 24의 첫 PNG를 렌더 성공으로 표시하지 않는다.

Editor는 강제 종료나 stop 재시도 없이 **29.76초**에 정상 종료됐다. 소유 active process·working set·private bytes 0을 확인한 뒤 provider를 1.62초에 unmount했다. 닫힌 journal은 **2,284,196,270B**, **334,185 frames/sequence**, SHA-256 `B8AC380B5964A26215C9EB7D462E58DB01F567E0704C37F37F5FBED9E0B2A0AC`다. 첫 준비와 후속 수정은 누적 입력이고 다른 구현의 속도 비교가 아니다.

`live-samples.jsonl`은 약 5초 간격 282개 표본이며 계측 읽기 오류는 0이다. 아래 값은 같은 provider epoch `4d9d8d8de25f43a1b99603057cc63ea6`의 단계 경계 차이다. 최초 준비의 provider 관측 시각은 19:41:21.0216~20:01:07.5490(KST)이고 CLI 시작과 heartbeat ready 시각은 각각 약 0.39초·0.02초 차이다. 6개 root의 할당 계측과 공유 VSS 조회·OS background 동작이 함께 있으므로 격리된 성능 기준이나 과거 WI와의 개선률로 사용하지 않는다.

| 관측 구간 | durable commits | journal 증가 B | 준비 초 | append 초 | commit flush 초 | index 적용 초 | 전체 commit 초 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 첫 import 관측 | 320,482 | 2,263,014,996 | 6.467 | 3.451 | 828.378 | 8.197 | 848.157 |
| 첫 ready→첫 검증 관측 | 1,314 | 6,060,259 | 0.038 | 0.018 | 12.610 | 0.104 | 12.778 |
| 첫 검사·대기 | 33 | 75,049 | 0.001 | 0.0002 | 0.348 | 0.009 | 0.358 |
| 후속 수정 관측 | 1,228 | 6,850,706 | 0.026 | 0.018 | 2.733 | 0.035 | 2.818 |
| 후속 검사·종료 관측 | 11,120 | 8,188,208 | 0.111 | 0.010 | 23.850 | 0.040 | 24.055 |

명시적 Flush 등을 포함한 첫 import의 전체 flush는 320,548회·828.497초이고 표의 commit flush와 다르다. 후속 수정의 provider 관측 구간은 20:03:04.4872~20:03:56.0952이며 실제 compile 완료 20:03:37.0102 뒤의 관찰 지연도 포함한다. 마지막 행도 검사·명령 준비·종료 전체 구간이므로 Editor quit만의 비용으로 환산하지 않는다. 전체 commit에는 계수 갱신 등 표의 네 단계 밖의 소량 비용도 있다. 호출별 대기 시간은 이 시간들과 합산하지 않는다.

| 첫 import 연산 | durable commits | prepare 초 | append 초 | flush 초 | index 적용 초 | 전체 commit 초 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| create | 35,301 | 0.613 | 0.016 | 93.277 | 0.099 | 94.185 |
| delete | 557 | 0.018 | 0.0004 | 1.338 | 0.003 | 1.362 |
| metadata | 62,082 | 0.946 | 0.024 | 160.769 | 0.101 | 162.149 |
| rename | 3,966 | 0.114 | 0.002 | 9.071 | 7.197 | 16.406 |
| resize | 33,605 | 0.537 | 0.014 | 86.467 | 0.164 | 87.353 |
| write | 184,971 | 4.240 | 3.394 | 477.456 | 0.633 | 486.703 |

첫 import commit 내부에서 flush는 약 97.7%다. 이는 **계측한 commit 내부의 비중**이며 전체 Editor 시간의 절감 가능 비율이 아니다. create·metadata·resize의 flush 합계도 340.51초이므로 payload 기록 이외의 내구성 비용을 분리할 필요가 있다. 최초 mutation gate 337,410회·누적 대기 2.7798초, Editor 전체 관측의 누적 대기 2.8072초다. 모든 실제 연산의 flush 전후 대기 histogram은 0명 칸만 증가했고 commit 내부 실패와 I/O 실패는 0이었다.

### 닫힌 journal 재연결과 저장량

같은 snapshot과 닫힌 journal의 재연결은 mount **5.77초**, 내부 replay **4.79초**였다. 신규 commit 0, 334,185 frames, 코드 24와 텍스처 설정 hash 일치, 정상 unmount를 확인했다. 재연결 전후와 결과 수집 뒤 journal 길이/hash가 동일하다. 이 단계에는 Editor를 다시 실행하지 않았으므로 Editor 재개 속도 증거로 표시하지 않는다.

정지 저널의 읽기 전용 장부는 모든 frame checksum·sequence와 위 hash를 확인했다. 유효 payload **1,838,007,144B**, 대체 payload **289,531,653B**, metadata/frame **156,657,473B**이며 합계가 journal 길이와 같다. 현재 경로 기준 유효 Library payload는 **1,828,459,443B(약 99.5%)**다. 이 값들은 논리 바이트이고 물리 할당과 다르다. 대체 payload를 전부 회수할 수 있다고 보장하지 않으며 이 실행에서는 저널 정리를 하지 않았다.

| 단계 | 소유 delta/control/snapshot 파일 할당 B | 공통 Git 파일 할당 B |
| --- | ---: | ---: |
| snapshot 전 | 20,480 | 6,124,208 |
| Editor 시작 전 | 816,800 | 6,137,000 |
| 첫 ready 후 표본 | 2,265,472,920 | 6,137,000 |
| 후속 검증 후 표본 | 2,278,515,184 | 6,137,000 |
| 결과 보존·등록 해제 후 | **2,286,170,992** | 6,125,848 |
| 소유 자원 회수 후 | 0 | 6,125,848 |

모든 단계의 6개 root가 complete이며 초기 delta를 포함했다. 표본별 최고 **약 2.286GB**는 순간 peak, MFT·디렉터리 메타데이터·OS cache·전역 Unity cache 또는 VHD backing 파일의 감소를 측정한 값이 아니다. 첫 임포트의 Library 쓰기는 delta에 이미 들어 있어 별도 합산하지 않는다. 공통 Git은 모든 기존 결과를 포함하며 이번 실행 전후 차이는 1,640B다. 이를 새 결과의 독립적인 물리 비용으로 단정하지 않는다.

격리 V:의 공유 VSS 사용/할당은 snapshot 전 **0/0B**, 회수 직전 **27,164,672/100,663,296B**, 회수 뒤 **0/0B**였다. 볼륨 전체 값이므로 소유 파일 할당에 더하지 않는다. 독립 bundle 논리 길이는 **5,999,971B**이고 C: 검증 증거 root의 회수 뒤 파일 할당은 **81,183,072B**였다. 합성 입력·두 probe/CLI 묶음·독립 bundle/bare 저장소·제어 증거가 이 root에 포함되며 중복 합산하지 않는다. 마지막 표본 뒤의 최종 검사·요약 파일은 이 수치 밖이다.

### 독립 결과와 회수

결과 revision **`b0f8b865f9d39c69154cc025accd2e789fe69f32`**은 `codex/jw27/unity`와 `refs/splinter/snapshot-results/jw27-unity`에 남겼다. 바뀐 파일은 `Assets/SplinterSnapshotProbe.cs`와 `Assets/SplinterQualification/Target.png.meta` 2개이고 임시 Unity bridge는 제외됐다. 보존 mount 5.78초, 결과 수집 2.32초, unmount 0.66초, Git 등록 해제 1.12초, C: bundle 생성/별도 저장소 fetch는 0.87초였다. 관찰·장부·명령 사이 대기는 위 API 시간에 포함되지 않는다.

C:의 bundle과 외부 alternates 없는 bare 저장소에서 같은 revision·코드 24·텍스처 설정·`git fsck`를 확인했다. 원본 clean HEAD·추적 파일 90개 hash·기존 ref 23개·원본 Library 부재를 회수 전과 snapshot 제거 뒤에 다시 확인했다. 소유 provider 3개와 Editor 1개의 PID/시작 시각을 기준으로 종료와 열린 handle·I/O 오류 0, mount·Git 등록 부재를 확인했다. 증거를 복사한 뒤 소유 delta/control 회수는 0.18초였고 소유 snapshot과 빈 영수증 경로도 회수했다. 기존 C: snapshot `{78AD7813-DF73-4845-8513-F14CC6E87331}`는 유지됐다.

마지막에는 `Get-DiskImage.Attached=false`, V: 부재, 제한 관리자 PID 종료를 확인하고 C: 독립 결과의 revision·내용·bundle hash·`git fsck`를 다시 검증했다. 원본 fixture·기존 ref·새 결과 ref·기존 VHD·과거 실행 증거는 보존한다. 새 결과 bundle SHA-256은 `AD22FD06F63913574C9D842E7436DE45B2006BC3AB69A235139D8AC9D43C862D`다.

## 쓰기 동시성의 경계와 후속 판단

`WinFspOverlayFileSystem`의 `mutationGate`가 Create·Overwrite·Cleanup·Write·SetBasicInfo·SetFileSize·Rename의 변경 callback 전체를 직렬화한다. Runtime 앞에서 이미 직렬화된 Unity 요청과 직접 Runtime을 호출하는 parallel 합성의 조건은 다르다. 현재 Runtime 표본으로 어댑터/OS의 대기량을 알아낼 수 없으며, Runtime 대기 0을 전체 쓰기 수요가 없다는 의미로 해석하지 않는다.

따라서 **Runtime에 flush 묶음만 추가하는 후속 구현은 선정하지 않는다**. 별도 동시성 설계가 필요하다면 먼저 callback의 append offset·constrained I/O·resize·후속 파일 정보 조회가 지키는 복합 검사의 원자성, queue 대기 위치와 요청 완료 경계를 검토해야 한다. 성공 응답 이전 durable sequence, 실패 전파·재연결·seal·종료, bounded 대기의 관문을 그 이후에 둔다. 이번 범위에서 잠금을 제거하거나 flush 정책을 바꾸지 않는다.

## 판정과 증거

| ID | 판정 | 제한 |
| --- | --- | --- |
| JW-01 | Passed | 고정 연산 계수·schema·예산 거부 |
| JW-02 | Passed | 고정 합성 18회, 직접 Runtime 호출, warm cache·3회 소수 표본 |
| JW-03 | Passed, 제한된 관측 | 최초 import·현재 compile/로드 코드·후속 수정·정상 stop·동일 journal 재연결. 첫 PNG와 후속 reload 뒤 첫 PNG는 빈 화면, 첫 렌더 원인 미확정 |
| JW-04 | Passed, 제한된 관측 | 독립 결과·원본 90개 파일/기존 ref 23개/타 snapshot 보존·회수·VHD 분리 확인. 순간 peak·어댑터/OS 대기 unknown, Runtime 단독 group flush 미선정 |

원시 증거 root는 `artifacts/storage-foundation/jw-20260927/`다. 합성·binary hash·자동 TRX는 그 아래 `evidence/`에 있고, Unity 단계 관측은 `live-samples.jsonl`·`state-*.json`·`live-phase-summary.json`, 파일 할당은 `space-*.json`, 공유 VSS는 시각이 있는 `response-*.json`으로 분리한다. 전체 판정은 `qualification-summary.json`, 보존/회수는 `independent-result.json`·`pre-reclamation-audit.json`·`source-and-result-after-release.json`·`closeout.json`·`final-environment-check.json`과 독립 bundle/저장소를 따른다. SF-02 전체와 SF-03은 Partial이며 첫 렌더 원인·Library 물리 절감·대형 PV·기본 모드 채택으로 확대하지 않는다.
