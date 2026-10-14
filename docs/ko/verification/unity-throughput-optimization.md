# Unity 처리 시간 최적화

[English](../../verification/unity-throughput-optimization.md)

이 기록은 [MW-01](unity-maze-workflow.md)과 [구현 범위](../plans/unity-throughput-optimization.md)의 후속이다. TP-01–04는 유지한다. 실험한 kernel cache 후보는 실제 Unity 미로 작업의 정상 회수에 실패하여 철회했다. 작은 fixture 통과와 일부 단계 개선을 최종 제품의 자격이나 전체 Unity 시간상 이점으로 읽지 않는다.

| ID | 변경 | 현재 근거 |
| --- | --- | --- |
| TP-01 | Git 설정과 경로별 속성 해석, version 3 소스 manifest | 대상 시험 62개 통과. 미변경 1MiB 파일의 읽기를 잠근 상태에서 새 경로 73개의 720바이트만 해시한다. 전역 설정과 영향받는 경로의 속성은 변환 hash를 계속 무효화한다. 미지원 버전은 감사에서도 원문 보존·거부한다. |
| TP-02 | 최초 Git 허용에서 처음 검증한 불변 내용 요약 재사용 | 최초 summary·proof 재사용·decode·소스 변경 계약의 대상 시험46개가 통과했다. 필수 index refresh 뒤 최초 검사는 파일4개를 재사용하여 payload를 읽지 않고 감사는1,048,608바이트를 모두 읽는다. 최종 Attach 순서와 변경 경로 회귀는 별도 후보 검증에서 통과했다. |
| TP-03 | commit 범위 capsule 핸들/footer 재사용과 같은 핸들 회수 | 준비 pin, 손상, 게시, 복구, 읽기 동시성을 포함한 대상 시험 82개 통과. 준비 중 Windows 핸들 최대64개를 유지하고 다른 플랫폼/상한 초과는 새로 읽는다. 게시 전에 pin을 닫는다. |
| TP-04 | 압축 private 바이트를 고정한 뒤 mutation 잠금 밖에서 해제 | 첫 대상 실행 54개 통과, 선택 비용 probe 두 개 미실행. 세대 보존, 동시 회수, poison writer, 종료 배출 통과. 후속 동시 읽기/복구 시험35개가 통과했으며64fragment초과 회귀도 포함한다. |
| TP-05 | Windows 파일 cache 유지 실험 | **철회.** 작은 native/helper·Editor 없는 CLI 검증은 통과했지만 실제 Unity에서는 정상 Editor 종료 뒤 각 작업의 연결된 Burst 실행 이미지 문맥11개가 남아 `OverlayStopPending`을 반환했다. 제품은 cleanup purge를 유지하며 실험 helper·시험은 제품 코드에서 제거하고 원시 근거로 보존한다. |
| TP-06 | 같은 다단계 미로 작업과 단일 공유 Editor 비교 | 후보의 기능 생성·저장·통합 Play 검증은 통과했지만 정상 Splinter 완료는 실패했다. 같은 ID 복구 후 원본을 복원했으며 복구와 처리 시간 자격을 구분한다. 공유 실행도 수정된 같은 장면 인계를 사용한다. |

## 작은 비용 측정

고정한 실행 파일마다 두 번, 전/후/후/전 순서의 평균이다. 각 실행은 개별 flush하는 mutation768개, 현재 파일195개/289,879바이트를 유지하며 전체 내용과 재접속 seal 검증을 통과했다.

| 쓰기 fixture | 이전 | 이후 |
| --- | ---: | ---: |
| 전체 파일 덮어쓰기 | 3.766s | 3.661s |
| metadata 교체 | 3.553s | 3.359s |
| page-prefix 덮어쓰기 | 5.141s | 5.216s |
| 합계 | 12.460s | 12.236s |

준비 부분은32–50%, 누적 managed 할당은65–72% 줄었지만 전체 시간 차이는 약1.8%이며 적은 관찰에서 page-prefix는 오히려 증가했다. 큰 작업시간 개선으로 읽지 않는다. 원시 자료는 `artifacts/unity-throughput-optimization-20261015/storage-probe-summary.json`과 전후 writer 표본 네 개다.

별도 eager-shard prototype은 물리 파일2,048개를128/32개로 줄이고 전수 읽기 검증을 빠르게 했다. 하지만 mutation마다 private 바이트를16–64배 재작성하며 측정한 쓰기는 느려졌다. 여러 shard의 원자성이나 제품 복구를 구현하지 않았으며 제품에 선택하지 않는다.

일반 권한 native cache prototype(`.local/wc15b/result.json`)은 작은 합성 기준본을 여덟 번 반복 읽었다. cleanup purge 평균0.1571s, cache 유지0.0374s이며 basis reader 바이트는16,384,000→9,043,968이다. 유지만 하면 정상 unmount 후 context5개가 남았다. 별도 cache 유지+notification 실행은11.3ms에 context0까지 배출하고 unmount 전후 seal을 유지했다. 모든 실행에서 root `FlushFileBuffers`가 성공했다. Unity 결과나 모든 dirty/mapped 쓰기 경계의 증명은 아니다.

## 유지하는 보장과 RAM 비용

저널 형식, 최종 물리 바이트 검사, 독립 결과, 소유권과 mutation별 durable flush/ACK를 유지한다. TP-04는 **읽기당** rented encoded buffer 최대4MiB와64fragment에 기존 fallback scratch를 더하며 동시 읽기 수만큼 증가한다. 기존64MiB decoded cache를 별도 cache 잠금으로 사용하고 현재 encoded 바이트 검사를 유지하며 close 전에 active 읽기를 배출한다. 전체 프로세스 RAM 상한은 아니다.

Windows kernel cache는 OS가 관리하며 제품의 볼륨별 바이트 상한이 없다. 철회한 실험은 정확한 volume flush·admission fence·실제 context 배출을 요구했으며 시간 초과나 Editor 종료만으로 충분하지 않았다. 제품은 기존 cleanup purge 정책을 유지한다. 아래 native 측정은 철회한 후보에만 해당한다.

추가 동시 읽기/복구 검사 35개가 모두 통과했으며 64개 fragment 초과 시 즉시 복사 경로도 확인했다. `.local/wc15c/result.json`의 helper 네이티브 네 trial 및 단위 시험 두 개는 native 세대 identity·공개 WinFsp raw notification API·실제 mapped view의 종료 거부와 재쓰기를 검증한다. `OverlayContextsRemain` 뒤에도 같은 view의 쓰기/flush가 가능하고 view 종료 후 61.4ms에 최종 drain되어 unmount 전후 context 0·독립 재오픈 내용/seal 동일을 확인했다. 다른 시험과 병렬 실행했으므로 이 실행의 읽기 시간은 성능 근거로 사용하지 않는다. 긴 이름·짝이 없는 surrogate도 UTF-16 code unit을 그대로 보존하며 일반 notification batch는 16KiB, 긴 단일 record는 최대 64,016바이트다. 실제 제품 CLI 자격은 아래 작은 A/B로 한정한다.

## 작은 실제 제품 A/B

`artifacts/unity-throughput-optimization-20261015/current/result.json`은 고정한 후보의 새 Editor 없는 Git 프로젝트를 기록한다. payload64파일·관리 Library128파일에서 A/B는 최초 소스68파일을 재사용하고 payload를 재해시하지 않는다. A의 flush-response와 B의 source-response 게시를 막아도 같은 live provider/mount를 유지하며, 주입한 빈 응답 디렉터리만 제거하면 같은 request ID가 완료된다. sticky state는 지우지 않았다. 병렬 `checkpoint --release`는 서로 독립적인 Git 결과·상대 소스 제외·released 상태·mount/provider/delta 부재·핸들/working set/private bytes0을 확인했다. Library 복원 뒤 모든 원본 파일 hash·HEAD/clean 상태·원본 worktree1개를 대조했다. 전체 회귀와 병렬 실행했으므로 시간은 정확성 기록일 뿐 성능 비교로 인용하지 않는다.

## 실제 Unity 실패와 안전한 복구

`artifacts/unity-throughput-maze-20261015/candidate.json`으로 고정한 후보는 TP-01–05를 포함하며 최종 TP-01–04 제품과 다르다. 소유 Editor 두 개는 정상 코드0으로 종료했다. 통합 완료는 먼저 Editor 기한에 도달한 뒤 같은 ID 재시도에서 `OverlayStopPending`을 반환했다. 정확한 프로세스의 읽기 전용 heap 관측에서 작업마다 Burst runtime DLL2개와 JIT DLL9개, 총11개의 연결된 문맥이 확인됐으며 삭제된 문맥은0이다. root flush와 이름 notification으로 실행 이미지가 배출되지 않았다. 위 Editor 없는 CLI 검증은 이 실제 실패를 포괄하지 못했다.

작은 image fixture에서 기존 파일을 `FILE_WRITE_DATA` 권한으로 열면 사용 중인 실행 이미지는 sharing error32로 거부하고 사용이 끝난 뒤 실제 쓰기 없이 성공함을 확인했다. 설치된 WinFsp 구현은 이 open 경로에서 `MmFlushImageSection(MmFlushForWrite)`를 호출한다([공식 소스](https://github.com/winfsp/winfsp/blob/fde790d/src/sys/create.c)). 복구는 정확한 소유 root를 고정하고 확인한 기존 DLL만 쓰지 않고 열며, root를 놓기 전 DLL 내용·metadata와 전체 delta inventory를 대조했다. A는 delta20,015파일/50,349,918바이트·sequence49,331, B는17,464파일/41,627,284바이트·sequence45,242를 유지했다. 이후 두 provider가 자연 unmount하며 핸들과 문맥0에 도달했다. 프로세스 강제 종료·계수 변경·파일 덮어쓰기·전역 cache purge는 하지 않았다.

같은 workspace ID가 이후 `released`에 도달하고 독립 결과를 보존했으며 원본 Library를 복원했다. 원본 소스와 Library 전체 해시, 기존 ref/worktree 등록, 임시 로컬 Git 설정을 대조했다. 복구 근거와 철회한 소스 snapshot은 같은 artifact 디렉터리에 보존한다. 후보 실패까지의 시간이나 진단·복구 시간을 성공한 전체 작업 시간으로 취급하지 않는다.

## TP-06 시간 비교

[데이터셋](../../../reports/benchmarks/unity-throughput-maze-20261015.json). 같은 커밋의2.80GB 소스와 바이트를 대조한42,422파일/3,142,530,954바이트 Library로 두 방식을 시작했다. A는 미로 생성기·장면을 만들고 저장하며 B는 컨트롤러·입력·스테이지 로직을 만든다. 누적6단계와 통합 미로3개의 실제 Play 결과는 동등하다. AI 작성·입력 inventory·증거 복사는 제외하고 완료한 준비·정상 종료·보존·원본 정리는 포함한다.

| 단계 | Splinter 실험 후보 | 단일 공유 Editor |
| --- | ---: | ---: |
| Library 준비 | 69.7s | 불필요 |
| A/B 작업공간 생성 | 105.5s | 14.3s |
| 초기 Editor 준비 | 272.7s | 24.1s |
| 기능 작업6단계 | 355.3s, 병렬 | 136.2s, 순차 |
| 통합·Play 검증 | 218.9s | 113.3s |
| 정상 전체 작업 | **없음 — 정상 회수 실패** | **313.2s (5분13.2초)** |

Splinter의 완료된 단계만 **1,022.1s (17분2.1초)**이며 아직 완료·복원 이전이다. 성공한 전체 시간이 아닌 소계다. 실패는1,296.1s에 반환됐다. 진단 뒤 같은 ID 완료의 A/B 경과 시간은43.2s, Library 복원은52.0s이지만 이를 가상의 중단 없는 전체 시간에 끼워 넣지 않는다. 일부 단계/직렬 전체의 비율3.26을 정상 전체 속도 비율로 읽지 않는다.

MW-01의 과거 Splinter 단계와 비교하면 작업공간 생성은169.9s→105.5s이고 초기 Editor 준비는267.8s→272.7s다. 기능 단계는382.9s→355.3s, 통합은239.8s→218.9s다. 과거 Library는 파일6개가 적고35개 파일이 달라 알고리즘별 인과 효과가 아닌 관찰 비교다. MW-01의 공유 전체787.5s에는 모달 문제가 포함되므로 정상 기준선으로 사용하지 않는다.

측정한 후보는 시간상 수용 기준을 충족하지 못했다. 기능 작업 단계만 보아도 Editor 순차 사용보다 느리다. 최종 코드는 TP-01–04를 유지하고 TP-05를 철회했으며 새로운 전체 Unity 시간상 이점을 주장하지 않는다. 대형 반복 재실행이나 일반적인 긴 작업 손익분기를 추론하지 않는다. OS cache 초기화나 통계적 반복 없이 순서를 고정한 한 묶음이다.

## 회귀 검증

`pwsh -NoProfile -File ./scripts/build.ps1`의 locked restore·Release 빌드가 성공했으며 전체1,646개 중1,621개 통과·실패0·조건부 외부 시험25개 미실행이다. 마지막 Git Attach 순서 조정 뒤에는 후보와 DLL hash가 같은 별도 출력에서37개 회귀를 추가로 통과했고 위 실제 CLI A/B도 그 후보를 사용했다. 전체 gate가 이 마지막 순서 조정 이전이라는 경계를 보존한다. `artifacts/unity-throughput-optimization-20261015/validation-summary.json`과 full-regression.trx가 원시 근거다.

cache 유지 철회 뒤 최종 별도 Release 빌드는 경고·오류0이며 대상 시험35개를 추가 통과했다. 작은 실제 `final-purge` A/B에서 최초 재해시0·응답 게시 실패 보존·병렬 완료·독립 결과·원본과 자원 전체 복원을 다시 확인했다. `artifacts/unity-throughput-optimization-20261015/final-validation.json`이 최종 소스/실행 파일과 gate 경계를 고정한다. 작은 실행의 원시 scope 문자열은 이전 cache 문구를 상속했지만 이 최종 실행은 고정한 코드대로 cleanup purge를 사용한다. 시험과 겹친 시간은 성능 근거가 아니다. 최종 TP-01–04 제품의 새로운 성공한 전체 Unity 시간 자격은 없다.

기본 `scripts/splinter.ps1`의 Release 출력도 철회 뒤 다시 빌드했으며 제품 DLL10개의 hash가 최종 native 검증 출력과 일치한다(`default-entrypoint-proof.json`). 배열 형태의 측정 기록과 응답 객체를 구분해 입력한 실제 응답 schema 검사도 통과했다.
