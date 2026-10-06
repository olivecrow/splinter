# SF-02 예산 제품 경로와 반복 저장량 검증

실행일: 2026-09-26. 상태: **BQ-01~05 정의한 범위 완료, scoped-complete-with-limitations**. [확정 범위](journal-budget-and-growth-qualification.md)를 따르며 증거는 `artifacts/storage-foundation/bq-20260926/`에 보존한다. 제품 후보는 `c56d065`, Windows x64, 기존 격리 16GiB NTFS VHD와 작은 Unity `6000.3.22f1` 입력이다. 기존 원본·결과는 재사용/보존하고 새 home `V:\bq26\s`, `V:\bq26\u`와 새 작업 ID를 사용했다. 다음 공간 절감 후보는 **SF-03의 불변 후보·고정 검증 view·단일 Library A/B/A 최소 범위**로 선정했으며 아직 실행하지 않았다.

| 관문 | 현재 판정 |
| --- | --- |
| BQ-01 | Passed: 제품 locked restore·Release 빌드·자동 267 통과·0 실패·당시 제외 시험 1 skip. 읽기 전용 장부 6건 통과. 합성/Unity 생성 직전 Git 공간 기준과 후보 hash 확보 |
| BQ-02 | Passed: 실제 제품 mount의 예산 초과·인자 생략 재시도 거부, 저널 hash/sequence·snapshot 보존과 후보 없음, 명시적 정확한 중첩량 재시도·내용·같은 snapshot 재개 확인 |
| BQ-03 | Passed, 운영 재시도 포함: 첫 로드·정리·같은 snapshot 재개·총 3회 수정의 실제 값/MVID/현재 validation·정상 stop 확인 |
| BQ-04 | Passed, 정의한 논리 분류·별도 파일 할당 장부: 정리 전후·최종 수집 seal 일치, 재증가 분해·동시 표본 최고치·공유 VSS 확인. MFT/전역 cache·표본 사이 peak는 unknown |
| BQ-05 | Passed, 아래 보존 경로의 제한 포함: 두 독립 결과·원본 hash/HEAD/index·비소유 C: snapshot 보존, 소유 자원 회수·VHD 분리·제한 관리자 종료, SF-03 한 후보 선정 |

합성 workspace ID는 `e61be1640b78a9922e741b4e`, Unity ID는 `4ba126abe0feb6b7ea7c76b9`다. 관리자 세션은 이 두 ID의 고정 helper 해시·정확한 디스크/볼륨만 처리하며, 비소유 snapshot 목록과 공유 VSS 시계열을 별도 기록한다.

계측은 제품이 replay·seal을 확인한 저널을 정지 경계에서 읽는다. 개발용 Python 장부는 각 프레임 checksum·전체 hash를 확인하고 범위 덮어쓰기·truncate·rename/delete·정리 형식의 논리 바이트를 분류한다. 제품 replay를 대체하지 않는다. ASCII 경로만 판정하며 비ASCII는 명시적으로 거부한다. 경로별 물리 할당을 추정하지 않고, 대체 payload 전체가 실제 compaction 절감량이라고 가정하지 않는다.

정리 직후 장부를 위한 추가 정상 provider unmount는 기존 `snapshot-spike`의 `unmount`만 사용하는 **개발 계측 개입**이다. workspace 생성·compact·재개·회수는 제품 명령으로 확인하며 이 개입을 제품의 새 명령이나 무개입 실행 증거로 표시하지 않는다. 추가 unmount/ensure·권한 관측 비용을 직접 compact 비용과 구분한다.

합성 fixture에는 Unity validation이 없어 제품 checkpoint가 `ValidationRequired`, 미수집 release가 `UncollectedChanges`로 거부됐다. 변경 파일만 fixture의 workspace Git 브랜치에 커밋한 뒤 제품 release로 독립 결과를 보존했다. 이 경로에 앱 검증 성공을 부여하지 않는다. Unity 작업은 실제 로드 코드와 현재 validation 이후 최종 checkpoint 한 번을 사용했다.

BQ의 이 제한된 실행 완료를 SF-02 전체 Passed나 대형 프로젝트 용량 목표 달성으로 해석하지 않는다. 기존 FailedPreservation, SF-02 전체 Partial과 SF-03~06 NotRun을 유지한다.

## 후보와 합성 예산 경로

`candidate-manifest.json`에 제품 소스·계약·시험 hash를 고정했고 `candidate.trx`와 `build.log`에 locked restore·Release 빌드 및 267 통과/0 실패/당시 제외 시험 1 skip을 남겼다. 이번 코드 변경은 개발용 읽기 전용 계측 두 스크립트와 그 회귀 시험에 한정한다. Python 장부 6건은 `accounting-tests-final.log`를 따른다. 제품 Runtime·Core·CLI는 변경하지 않았다.

`original-baselines.json`은 합성 원본 `2aa147df53037cc31eaa2acbe04ab7330e66b49d`와 작은 Unity 원본 `58f81b5bc6ff8a940d8a90872bd3fed858a54cc1`의 HEAD·index·추적 파일 원시 hash를 고정한다. 생성 전 Git common 할당은 두 입력 모두 측정했다. 새로운 대형 에셋이나 원본 Library를 만들지 않았다.

합성은 1MiB 파일의 offset 8192에서 같은 4096B를 100회 썼다. 제품 `compact`의 예산 1B와 새 CLI의 인자 생략 재시도가 모두 `CompactionOverlapBudgetExceeded`로 거부됐다. 기존 저널 453,147B와 후보 예상 5,825B의 합은 458,972B다. 거부 전후 저널 SHA256 `9411853b2dada69fb9f93c005657857b4fc131ac95c874e44345feaffab22ef1`, sequence 102, snapshot ID가 같고 후보·intent·이전 세대가 없었다. 저장된 예산 1B와 `resume-compaction`을 유지했다.

정확히 458,972B를 명시한 같은 작업의 재시도는 성공했다. 정리 뒤 저널은 5,825B, 유효 payload는 4,177B(모델 4,096B와 Git pointer 81B), 대체 payload는 0B다. 정리 전 대체 payload는 405,504B였다. 정지 경계의 hash·sequence가 제품 compaction seal과 일치하며 같은 snapshot에서 mount 내용과 소스를 확인했다. 물리 파일 장부의 저널 할당은 458,752B → 8,192B, 소유 추가 파일 합계는 1,232,944B → 795,504B였다. 제품 receipt의 다른 관측 시점 할당은 별도로 남기고 혼합하지 않는다.

합성 독립 결과 `54d2f05ff69a3b7374dc240a516cd653d176790d`를 snapshot 회수 후 읽었고 결과 1MiB의 SHA256은 `f1afe330db09bcb82c41720c5dccf4a6fce92ffa599bd227b017e35fb07a8623`다. 원본 HEAD/index/파일 hash는 보존됐다. 기존 QJ 결과와 같은 모델 blob을 재사용했으므로 **새 Git blob 물질화 비용 시험은 아니다**. release 뒤 기록·로그·Git 결과의 추가 파일 할당 787,960B는 보존하고 mount·delta·프로세스·핸들·소유 snapshot은 회수했다. 상세 증거는 `source-budget-preservation.json`, `journal-source-*.json`, `source-closeout.json`이다.

## 작은 Unity의 실제 로드와 재개

| 단계 | `Calculate(3)` | assembly MVID | Editor PID | 검증 |
| --- | --- | --- | --- | --- |
| 최초 준비 | 13 | `078269ea-7b40-49d7-87f1-69956d6b0403` | 29404 | ready·실제 probe·현재 소스 확인 |
| 수정 1 | 14 | `b06def67-f6a1-4a99-90df-9a479805c5e1` | 29404 | 현재 source/validation 성공 |
| 같은 snapshot 재개 | 14 | `b06def67-f6a1-4a99-90df-9a479805c5e1` | 29056 | 새 generation·같은 코드 확인 |
| 수정 2 | 15 | `3935ece1-9c95-4bc5-a24c-87c9804be976` | 29056 | 현재 source/validation 성공 |
| 수정 3 | 16 | `7336e7c7-8d14-4313-b571-9f048f6a6952` | 29056 | 현재 source/validation 성공 |

수정은 `Assets/SplinterSnapshotProbe.cs` 한 파일의 상수 변경 총 3회다. 각각의 `unity-editN-change.json`, `unity-editN-validated.json`, `unity-editN-probe.json`에 원시 source hash·fingerprint·generation·MVID·PID·실제 경로를 연결한다. 정리 전후 정상 제품 stop은 소유 프로세스와 메모리 0을 확인했다.

첫 시작과 재개 시작은 `SourceChanged`를 반환했지만 소유 Editor는 실행 중이었다. 중복 시작하지 않고 status·로그를 관측해 ready가 된 뒤 현재 소스를 검증했다. 세 번째 refresh의 첫 `EditorFocusRequired`는 코드 추가 수정 없이 같은 제품 refresh 재시도로 접수·검증했다. 이 응답 및 개발용 측정 unmount/ensure를 포함한 **운영 개입이 있는 실행**이며 완전 무개입 경로로 주장하지 않는다.

Unity 정리의 저장된 예산은 5,368,709,120B였다. 권한 Observe 후 인자를 생략한 재시도가 성공했고 pending 예산을 지웠다. 저널은 sequence 333,222를 보존하며 2,279,539,161B → 1,969,637,634B로 줄었다. 읽기 전용 분석 전후 hash가 제품 compaction seal과 같았다. 유효 payload 1,838,626,847B는 유지됐고 대체 payload 284,667,979B는 0B로 줄었다. 나머지 감소는 프레임·메타데이터 감소다. 정리 전 유효 payload 중 현재 `Library` 경로는 1,829,045,448B(약 99.5%)다.

직접 compact 재시도 CLI는 97.086초다. receipt의 핵심 정리 구간은 55.554초이고 그 밖의 provider 재연결 14.240초·이전 세대 정리 8.868초가 별도 stage다. 핵심 구간의 source replay 17.096초·candidate write 5.338초·candidate replay 11.732초 등을 모두 원시 receipt에 남겼다. 추가 측정 unmount/ensure, 별도 Python 분석(정리 직후 37.083초), 관리자 관측·대기는 직접 compact 시간에 합치지 않는다. flush 정책은 변경하지 않았다.

첫 start 요청부터 ready 표본까지 1,366.904초, 재개 start 요청부터 ready 표본까지 76.284초였다. 30초 status polling에 따른 관측 지연을 포함한다. 첫 준비의 flush 320,947회·누적 1,029.603초와 재개 provider epoch의 flush 32,610회·68.938초는 서로 다른 구간이며 wall time에 더하지 않는다. 최초 payload 복제와 읽기 cache는 계속 0B다. `unity-timing-summary.json`을 따른다. 첫 Editor 로그는 다음 시작 때 덮어써졌으므로 보존된 CLI 시각을 준비 시간의 기준으로 사용하고, 재개 로그는 `unity-resume-editor.log`에 남겼다.

최종 장부 측정 뒤 추가 ensure에서는 저널 replay 22.564초가 시작 대기 구간(150 × 100ms, 관측 호출 시간 별도)을 넘어 `OverlayStartUncertain`이 발생했다. 이어진 status의 `SourceObservationUncertain`도 기록했다. 정확한 기존 provider가 mounted인 것을 확인하고 같은 ID/home의 ensure를 재시도해 새 provider/snapshot 생성 없이 회복했다. 현재 validation은 유지됐다. `unity-reconnect-recovery.json`과 해당 CLI 응답을 따르며 이 재연결 대기 제한을 해결했다고 주장하지 않는다.

## 저장량 분해와 파일 할당

아래는 원본 snapshot에서 읽는 데이터를 제외한 **저널 내부 논리 바이트**다. 경로군은 현재 경로 기준이며 rename 이전 쓰기 경로군별 누적량은 원시 `groups`에 별도로 있다. 직접 `Packages` 경로에 남은 overlay payload는 0B다. `Library/PackageCache`는 `Library`로 센다.

| 정지 경계 | 전체 저널 | 유효 payload | 대체/삭제 payload | 프레임·메타데이터 | 미분류 |
| --- | ---: | ---: | ---: | ---: | ---: |
| 정리 전 | 2,279,539,161 | 1,838,626,847 | 284,667,979 | 156,244,335 | 0 |
| 정리 직후 | 1,969,637,634 | 1,838,626,847 | 0 | 131,010,787 | 0 |
| 재개·수정 2/3 뒤 최종 수집 | 2,005,159,072 | 1,838,813,568 | 21,381,329 | 144,964,175 | 0 |

최종 유효 payload는 `Assets` 14,764B, `Packages` 0B, `ProjectSettings` 957B, `Library` 1,829,225,426B, 기타 9,572,421B다. 정리 이후 저널 재증가 35,521,438B는 유효 payload +186,721B, 대체 payload +21,381,329B, 프레임·메타데이터 +13,953,388B로 설명된다. 이 논리 값을 경로별 물리 할당으로 환산하지 않는다. 대체 payload 전체를 다음 정리의 확정 절감량으로도 표시하지 않는다.

아래 파일 할당은 같은 표본 시각의 `control-and-delta + runners + (Git common − 생성 직전 Git common 6,081,264B)`다. journal/candidate/previous는 control 합계의 하위 항목이므로 다시 더하지 않는다. 원본 Library는 없었고 mount view 내부 Library를 별도 파일 복사로 합산하지 않았다.

| 단계 | 저널 파일 할당(B) | 소유 추가 파일 합계(B) |
| --- | ---: | ---: |
| 첫 Editor ready | 2,264,924,160 | 2,266,916,024 |
| 정리 직전 stop 뒤 | 2,279,604,224 | 2,281,510,960 |
| 정리 중 관측 최고 | 현재 1,969,639,424 + 이전 2,279,542,784 | **4,251,098,528** |
| 정리 뒤 | 1,969,639,424 | 1,971,555,168 |
| 같은 snapshot Editor 재개 | 1,976,172,544 | 1,977,039,032 |
| 수정 2 검증 뒤 | 1,989,410,816 | 1,990,286,040 |
| 수정 3 검증 뒤 | 1,999,437,824 | 2,000,325,336 |
| 최종 stop 뒤 | 2,005,204,992 | 2,006,096,736 |
| 독립 결과 수집 뒤 | 2,005,159,936 | 2,006,064,896 |
| release 뒤 보존 기록/결과 | 0 | 889,400 |

주기 표본 251건(각 계측 뒤 10초 대기)과 명명한 경계 12건, 총 263건의 파일 장부는 모두 complete다. 최고치는 `space-unity-0165.json`(13:15:27.500 UTC)의 같은 시각 합계이며 표본 사이의 절대 peak가 아니다. NTFS 할당은 시점별 값이므로 stop/unmount 전후의 예약 할당 차이를 payload 절감으로 해석하지 않는다. 정리 직후부터 최종 수집까지 소유 추가 파일 증가는 34,509,728B다. `unity-space-summary.json`에 모든 원시 표본 경로를 남겼다.

실제 중첩한 두 저널 파일의 receipt 할당은 4,249,182,208B, 두 논리 길이 합은 4,249,176,795B였다. 5GiB 예산은 후자만 검사한다. 같은 시각 control/runner/Git 비용이나 볼륨 공유 VSS를 포함한 상한이 아니다. VSS는 별도 `response-vss-*.json`에 기록했으며 Unity 준비 뒤 관측은 Used 27,082,752B / Allocated 100,663,296B, 정리 뒤는 27,148,288B / 100,663,296B, 마지막 소유 snapshot 회수 뒤는 0B / 0B였다. `vss-overlap`라는 요청명은 마지막 응답 시각이 이미 이전 세대 회수 뒤이므로 VSS의 중첩 순간 표본으로 쓰지 않는다.

release 시점의 별도 계측 증거 파일 531개·할당 2,477,928B는 측정 overhead로 한 번만 표시하고 제품 합계에서 제외한다. 이후 문서·최종 요약 작성 비용까지 고정하는 수치는 아니다. NTFS 디렉터리/MFT, 공유 Unity 전역 cache 변화, OS cache와 표본 사이 peak는 미계측이다. 따라서 전체 물리 공간의 총량·절감률·상한과 대형 프로젝트 적합성을 선언하지 않는다.

## 결과 보존·회수와 다음 선택

Unity 최종 checkpoint는 `0a5876455cda92ab569f6c3585047715ae9fa4a5`를 `refs/splinter/snapshot-results/4ba126abe0feb6b7ea7c76b9`에 보존했다. 분석한 최종 sequence 365,826과 hash `1718f184735bf1ab4a647a9ac3fc8a41514ad50bdb07360dac1f3e27709f8834`가 checkpoint seal과 일치했다. release는 소유 bridge 제거에 따른 8개 메타데이터 기록을 추가한 뒤 sequence 365,834로 다시 봉인했다. 두 seal을 같은 것으로 표시하지 않는다.

snapshot 삭제 뒤 Git 결과의 probe 소스 1,533B(SHA256 `d301792f35c8fbfbae355dc729b0a1575bddfeb405513b1d3958dc19ac43f3d6`)를 읽고 `value + 13`과 최종 검증 fingerprint의 일치를 확인했다. Git 정규화된 결과 blob과 원본 파일의 원시 hash 검사는 별도다. 원본 HEAD·index·모든 추적 파일 hash·clean 상태가 유지됐고 mount·Git 등록·delta·provider·Editor/자식·핸들이 사라졌다. `unity-closeout.json`, `source-closeout.json`의 보존 검사를 따른다.

최종 V: 소유 snapshot은 0개이며 기존 C: snapshot `{78AD7813-DF73-4845-8513-F14CC6E87331}`은 유지됐다. 이를 같은 볼륨의 일반 비소유 백업 공존 검증으로 확대하지 않는다. 공간 샘플러와 고정 소유 ID 관리자 세션을 정상 종료했고 VHD `Attached=false`, `V:\` 없음과 해당 프로세스 종료를 `host-closeout.json`으로 확인했다. 기존 VHD 파일·fixture·Git 독립 결과·기록은 보존했다.

**다음 후보는 SF-03의 불변 후보·고정 검증 view·단일 Library A/B/A 최소 단위 하나다.** 마지막 단계에서 유효 Library payload 1.829GB는 대체 payload 21.4MB보다 훨씬 크고, 대체 기록을 모두 정리해도 유효 데이터 1.839GB는 남는다. 재증가분만 보면 대체/메타데이터가 주원인이지만, 용량 목표에서 더 큰 고정 비용은 작업마다 필요한 Library다. 이 실행은 한 작업이므로 단일 Library가 여러 후보 사이에서 실제로 절감하는 양은 아직 입증하지 않았다.

후속 범위는 작은 기존 fixture에서 A/B/A 불변 소스 후보·고정 검증 경로·검증 Editor 1개/Library 1개를 정의하고, 전환/원복의 코드·에셋·`.meta`·캐시 정합성과 원본·각 결과 보존, 추가 할당을 확인하는 것이다. 작업자 Editor 없이 소스를 편집하는 경우를 포함하되 기존 원본 Editor 공유·Coordination 제품 연결·기본 모드 채택은 자동 확정하지 않는다. 이 기록의 후보 선정 단계에서는 구현·실행이 NotRun이었다. 이후 제품의 작은 Library 구현·실행 자격은 [WF/LB 검증](product-validation-unelevated.md)으로 구분한다. flush 정책·온라인 정리·대용량 PV 재개·NR 전체 지원으로 범위를 넓히지 않았다.
