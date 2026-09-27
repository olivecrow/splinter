# 첫 준비·재연결 시간 검증

실행 ID는 `vr-20260927`이며 [VR-01~04](../validation-view.md)를 `scoped-complete-with-limitations`로 마감했다. 시작 제품 기준은 `5c5d94f`이고 그 위의 VR 후보를 검증했다. 정상 첫 import와 반복 재연결·보존/회수는 확인했지만 첫 렌더는 빈 화면이어서 VR-03 전체는 Partial이다. SF-03 전체나 일반 Unity 지원을 Passed로 올리지 않는다.

기존 격리 NTFS VHD의 작은 Unity `V:\sf-compaction-20260926\unity`와 새 소유 `V:\vr27`을 사용한다. snapshot은 `{FFE06A10-BF98-4846-B91B-C573B1D53E2A}`, 원본 기준은 `58f81b5bc6ff8a940d8a90872bd3fed858a54cc1`이다. 원본 Library와 새 cache가 없는 상태를 확인했다. worker 생성 전 공유 Git 파일 할당 6,112,144B를 포함한 기준을 보존했다.

원시 증거는 `artifacts/storage-foundation/vr-20260927/`다. 이전 바이너리 bundle과 manifest, 새 binary 및 시험 결과, 개별 timing, source·candidate hash, 파일 할당 표본과 VSS 볼륨 관측을 분리한다. 이전 VC의 실패·복구·빈 미리보기를 이 기록으로 덮어쓰지 않는다. 요약 수치는 `measurement-summary.json`, 최종 소유 자원과 분리는 `closeout.json`·`final-environment-check.json`을 따른다.

## 판정과 자동 회귀

| 관문 | 판정 | 확보한 증거와 한계 |
| --- | --- | --- |
| VR-01 | Passed, 정의한 범위 | 64KiB journal 읽기 버퍼, 모든 frame checksum·metadata·sequence 검증, 읽기/검증/index 계수. 경계 손상·미완료 tail·과거 null/unknown 호환을 검증했다. |
| VR-02 | Passed, 정의한 범위 | 같은 PID/시작 시각의 최신 mounted 상태와 실제 mount를 기다린다. 실패·세대 불일치·한도·취소 자동 시험과 실제 새 구현의 재연결 3건 모두 성공. 기존 구현의 3건은 준비 전에 `OverlayStartUncertain`을 반환했다. |
| VR-03 | Partial | 빈 cache의 첫 import·명시적 compile 완료·실제 코드/에셋 확인·정상 stop은 통과. `EditorFocusRequired` 뒤 같은 Editor에 명시적 refresh 재시도 1회가 필요했다. 첫 PNG는 배경만 표시됐고 같은 Editor의 두 번째 PNG는 모델을 표시했다. 원인은 미확정이다. |
| VR-04 | Passed, 정의한 범위 | 같은 닫힌 source/cache 저널의 기존/새 구현 각각 3회, hash 보존·실제 소스 읽기·I/O 실패 0. 독립 Git 결과·원본 보존·파일 할당·공유 VSS 관측·최종 소유 자원 회수를 확인했다. |

`pwsh ./scripts/build.ps1`의 locked restore·빌드·자동 시험은 **296 통과·0 실패·선택적 ReFS 1 skip**이다. `build-contract-final.log`와 `tests-296-passed.trx`에 저장했다. `JournalReplayTests`, `OverlayReadinessTests`와 실제 관측 형식의 `overlay-observation` schema 적합성 시험을 추가했다. 일반 response와 개발용 overlay schema 모두 새 nullable 계수를 허용한다. 이전 기록에 계수가 없으면 unknown이다.

첫 실행의 sandbox restore 제한과 writer가 열린 상태에서 hash를 읽던 새 시험 코드 오류는 `build.log`·`build-retry.log`에 남겼으며, 시험의 writer를 닫고 재실행했다. 최종 성공 증거를 실패 실행에 소급하지 않는다. cold Editor가 사용한 Runtime/Core/Windows binary와 최종 비교 binary의 hash는 같으며 마지막 schema 회귀 추가로 `Splinter.Protocol.dll`만 바뀌었다. `code-under-test-final.json`과 두 current manifest로 구분한다.

## 실제 준비와 첫 화면

worker mount 1.116초, 불변 후보 게시 0.731초, 첫 view mount 1.835초를 관측했다. 앞의 두 값은 이전 binary의 직접 CLI, view mount는 PowerShell wrapper를 포함하므로 이 짧은 단계 사이를 속도 개선으로 비교하지 않는다. 후보 journal은 8,038B이며 최초 payload 복제와 read cache는 0이다.

Unity `6000.3.22f1` 시작부터 첫 ready heartbeat까지 **1,027.510초(17분 7.5초)**였다. 강제 종료·cache 교체·import 복구 없이 완료했다. 별도 refresh는 첫 창 활성화 실패를 보존하고 같은 Editor에 재시도해 request ID `e8cd93c7e52740288fa1d3980de694c4`와 fingerprint가 일치하는 `success=true`, errors 0 완료를 확인했다. ready만으로 검증 완료를 대신하지 않았다.

실제 로드 값은 원본 13에서 후보 **23**, texture `1024×1024`와 maxTextureSize 1024, mesh 3개·3,001 vertices·2,441 triangles였다. 첫/두 번째 관측의 PID 2312, Assembly MVID `cc254943-165a-4955-859a-49002423133b`, 에셋 dependency hash가 같았다. 첫 `view-a1-preview.png`는 배경만, `view-a1-second-preview.png`는 모델을 표시함을 이미지로 확인했다. 이 사이 코드·에셋을 바꾸거나 Editor를 재시작하지 않았다. 지속적인 에셋 누락은 재현되지 않았지만 shader 비동기 준비 등 특정 원인을 증명한 것은 아니다. 최초 렌더 성공은 미달이다.

compile 완료 무렵 cache 관측에는 flush 319,572회·누적 726.068초가 기록됐다. 이는 해당 관측 시점의 누적 함수 시간으로, 첫 ready 벽시계 시간과 동일한 구간의 독립 분해가 아니다. acknowledged write flush 정책은 바꾸지 않았다. 정상 Editor stop은 **30.081초**, 그 뒤 unmount는 1.658초였다. 이번 변경으로 최초 import의 쓰기 비용이 줄었다고 주장하지 않는다.

## 동일 저널의 재연결 비교

정상 stop/unmount 후 cache `2,268,054,588B`·330,665 frame, source `9,442,536B`를 고정했다. 동일 mount·control·요청·snapshot을 사용해 `baseline → current` 순서로 3쌍을 실행했고 양쪽 모두 직접 `dotnet ... snapshot-spike`로 호출했다. Editor를 시작하거나 cache를 복사하지 않았다. 각 회 실제 코드/texture 설정/후보 파일을 읽고 unmount한 다음 양쪽 journal hash가 불변임을 확인했다.

| 회차 | 기존 cache replay | 새 cache replay | 기존 실제 mount 준비 관측 | 새 실제 mount 준비 관측 |
| --- | ---: | ---: | ---: | ---: |
| 1 | 23.025초 | 17.736초 | 24.336초 | 18.837초 |
| 2 | 19.032초 | 14.901초 | 19.972초 | 15.878초 |
| 3 | 19.464초 | 14.691초 | 20.480초 | 15.749초 |
| 중앙값 | **19.464초** | **14.901초** | **20.480초** | **15.878초** |

이 관측에서 replay 중앙값은 **23.44%**, 실제 mount 준비 중앙값은 **22.47%** 감소했다. 기존 CLI는 16.71~16.99초에 `OverlayStartUncertain`을 반환했고 측정기가 같은 provider의 준비를 추가 관측했다. 새 CLI는 15.73~18.82초에 실제 mounted로 성공했다. timeout 반환 시간을 실제 작업 가능 시간으로 비교하지 않는다.

새 구현은 매번 34,608 read 호출로 동일한 journal 전체를 읽었다. 중앙값의 읽기 0.613초·검증 2.342초·index 적용 11.748초로, 남은 시간의 대부분은 index 구성이다. OS cache를 비우지 않았고 매 회 전후 hash 읽기가 있으며 순서도 고정됐다. 3개 소수 표본·따뜻한 OS cache의 반복 관측일 뿐 물리 디스크 읽기량, 임의 시스템의 성능 개선률, 전체 Unity 재시작 시간의 증거가 아니다.

## 저장량·독립 결과·회수

파일 할당 표본 185개에서 소유 파일의 관측 최고는 **2,280,951,448B(약 2.281GB)**였다. 표본 자신·마지막 보고서 작성 이후 증가, MFT·OS cache·순간 peak의 상한은 포함하지 않는다.

| 최종 보존 직전 소유 경로 | 파일 할당 |
| --- | ---: |
| worker | 8,752B |
| immutable candidate | 12,848B |
| source generation | 9,446,008B |
| 공통 cache | 2,268,058,152B |
| control | 3,388,360B |
| snapshot receipt | 37,328B |

공유 Git 파일 할당은 **6,112,144 → 6,117,288B**, 증가 5,144B였다. 원본 Library는 생성하지 않았다. 회수 직후 측정 증거의 파일 할당은 10,617,136B로 별도이며 후속 문서·요약 작성으로 늘 수 있다. Library를 journal과 이중 합산하지 않는다. 격리 VSS 볼륨은 회수 전 used 27,099,136B·allocated 100,663,296B, 마지막 소유 snapshot 회수 뒤 0이었다. 이는 공유 볼륨 관측이며 개별 workspace 비용으로 귀속하지 않는다.

읽기 전용 checksum 장부의 cache 유효 payload는 1,829,199,588B·대체 payload 283,706,123B, source 유효 payload는 8,438,867B·대체 payload 733B다. 논리 장부를 물리 할당으로 환산하지 않는다. 이 범위에서는 저널 정리를 실행하지 않았다.

독립 결과는 fixture Git의 `codex/vr27/view-a1`, **`fc16f38bb21188c92d52f43fb5471ed7f2f23828`**에 보존했다. 코드 값 23·texture 설정 1024·CandidateA 파일, 결과 ref·객체 `git fsck`를 VSS 회수 전후 모두 확인했다. 원본 HEAD·development·clean 상태와 코드/texture meta hash는 시작값과 같았다.

정상 stop의 소유 프로세스·working/private bytes 0, worker/첫 view/6개 비교/최종 보존 provider의 PID·시작 시각별 종료와 open handles 0, mount·Git 등록 해제 후 delta를 회수했다. snapshot `{FFE06A10-BF98-4846-B91B-C573B1D53E2A}`만 해제했고 기존 다른 볼륨의 `{78AD7813-DF73-4845-8513-F14CC6E87331}`은 유지했다. 제한 환경에서 읽지 못한 helper 상태는 invalid/unknown으로 정정하고 실제 종료를 다시 확인했다. 보조 프로세스 4개 종료와 VHD 분리를 확인했으며 기존 VHD 파일과 그 안의 원본·독립 Git 결과는 보존했다. VHD 파일의 host 물리 크기 축소를 뜻하지 않는다.

## 남은 범위

첫 렌더의 초기화 원인, index 구성의 세부 병목, 최초 import의 쓰기 비용이 남아 있다. 다음 시간 개선 후보는 계측상 큰 index 적용 단계를 먼저 분해하는 제한된 작업이며 구현·실행은 NotRun이다. flush 묶음 변경·온라인 정리·새 기본 모드·큐/Coordination·source 반환 병합·대형 PV 재개는 수행하지 않았다.
