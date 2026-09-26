# 불변 후보·고정 검증 view 검증

2026-09-27, `vc-20260926`. [VC-01~04](../plans/immutable-validation-view-first-milestone.md)의 합성 및 기존 작은 Unity A/B/A 실행을 **scoped-complete-with-limitations**로 마감했다. 불변 후보 게시, 같은 Library의 코드·에셋 원복, 독립 결과와 소유 자원 회수를 확인했다. 첫 import의 장애·강제 종료/수정 후 재개, 첫 A의 빈 미리보기, 긴 cache replay의 mount 응답 재확인, 최초 Git 할당 미계측을 포함한다. SF-03 전체는 Partial이며 제품 모드 채택·대형 Unity 적합성은 완료하지 않았다.

원시 증거는 `artifacts/storage-foundation/vc-20260926/`의 `closeout.json`, `cost-summary.json`, `results-verification.json`, `pre-reclamation-audit.json`, 개별 CLI 응답·timing·Unity 로그·검증 응답·저널 장부·파일 할당 관측에 보존한다. 일부 제어 증거에는 로컬 IPC 토큰이 있으므로 공개 산출물로 사용하지 않는다.

## 범위와 입력

- 시작 기준: `development`의 `4ffed3f` 이후 변경. 이전 BQ 작업은 해당 커밋으로 보존했다.
- 기존 격리 16GiB NTFS VHD와 작은 Unity `V:\sf-compaction-20260926\unity`, 기준 `58f81b5bc6ff8a940d8a90872bd3fed858a54cc1`, Unity 6000.3.22f1.
- 원본 Library 없음. 새 snapshot 한 개, Editor 없는 worker A/B, 고정 `V:\vc26\view`와 공통 캐시 한 개. 큰 PV·canonical Editor·Coordination은 사용하지 않았다.
- 기존 Anvil 모델·텍스처 fixture를 사용했으며 새 에셋 구매나 전체 원본 복사는 하지 않았다. snapshot은 `{7DEE6BAD-3B2F-4429-BC99-416548BB4C71}` 하나, 세대별 `source-a1`/`source-b`/`source-a2`와 공통 `cache`를 사용했다.
- A: 코드 `Calculate(3)=23`와 texture max 1024, A 전용 text/.meta. B: 코드값 33과 texture max 2048. 다시 A로 돌아갈 때 실제 23/1024와 A 파일을 확인했다.
- 후보 A 게시 후 worker를 다시 편집해 코드값 24의 독립 Git 결과 `ab36d65f7f3534985b18e822e1ff00e9dd2bb492`를 보존했다. 후보 seal은 변하지 않았고 Unity A는 계속 23을 로드했다.
- 원본에 소유 임시 파일을 만들고 view에 나타나지 않는 것을 확인한 뒤 해당 파일만 삭제했다. 작업 기간의 원본 쓰기 차단을 사용하지 않았다. 최종 원본 HEAD·clean 상태·코드와 .meta hash·Library 부재를 다시 확인했다.

## 관문별 판정

| 관문 | 판정과 증거 |
| --- | --- |
| VC-01 | Passed, 정의한 합성·작은 fixture: 원본 payload 읽기 0의 후보 export, worker 후속 편집과 후보 불변, read-only seal, 동시 writer·손상·미완료 게시 거부 |
| VC-02 | Passed, 정의한 합성·fixture: source/cache 분리, 단일 writer, 세대별 source 출력 보존, 다른 후보로 기존 source 재사용 거부, private extent 이동의 5개 중단 지점 복구 |
| VC-03 | Passed, 정의한 loaded-code·asset A/B/A와 보존/회수. 첫 A import는 수정·운영 개입 후 성공했다. 첫 A의 빈 렌더 미리보기는 미해결 제한이며 A/B/A 전체 렌더 통과로 집계하지 않는다 |
| VC-04 | 계측 범위 완료, 공간·시간 장부는 제한 포함: 아래 실제 수치와 unknown을 보존한다. 정상 cold import 시간, 생성 직전 Git 추가 할당, 미관측 peak와 동등 입력 비교군의 절감률은 미확인이다 |

최종 `pwsh ./scripts/build.ps1`은 **282 통과·0 실패·선택적 ReFS 1 skip**이며 build warning/error는 0이다. `build-transfer-retry.log`, `tests-282-passed.trx`를 따른다. 합성은 추가·삭제·rename·truncate·.meta, 후보 뒤 재편집, 캐시 재개, 독립 source 출력, 1GiB sparse 파일의 유효 4KiB만 이동, 5개 중단 지점에서 같은 세대 복구와 잘못된 세대 차단, 열린 file ID의 replace·왕복을 확인한다. 후보/선택 schema와 변경 소스의 Git attach, 남은 index.lock 보존도 포함한다. 이 합성 복구는 실제 재부팅·전원·공간 장애를 대체하지 않는다.

## Unity 결과와 운영 개입

| 관측 | A1, 수정 후 재개 | B | A2 복귀 |
| --- | --- | --- | --- |
| 실제 코드값 | 23 | 33 | 23 |
| texture width/height/max | 1024/1024/1024 | 2048/2048/2048 | 1024/1024/1024 |
| Assembly MVID | `fa902747-0138-48ef-b4e0-274669b3b57a` | `136fff76-8dfd-430f-aa3d-35d10b735e45` | A1과 동일 |
| A 전용 파일 | 있음 | 없음 | 있음 |
| 명시적 refresh | 성공, 오류 0 | 성공, 오류 0 | 성공, 오류 0 |
| 모델 mesh/vertices/triangles | 3/3001/2441 | 3/3001/2441 | 3/3001/2441 |
| Scene mesh 참조 누락 | 0 | 0 | 0 |
| 저장한 렌더 미리보기 | 빈 하늘/바닥, 원인 미확정 | 모델 보임 | 모델 보임 |

세 응답 모두 texture GUID `60e4466064901d24bbb38040780c6bed`를 유지했다. refresh의 접수와 완료를 구분하고 각 `requestId`·fingerprint·성공/오류 0을 맞췄다. A1/A2의 fingerprint도 동일하다. A1 inspect는 refresh 접수 뒤 완료 확인 전에 실행됐고, A2 inspect는 완료 확인 뒤 실행됐다. 이 차이만으로 빈 미리보기의 원인을 확정하지 않는다. 최초 A 화면은 통과로 재분류하지 않았으며 원본 PNG를 보존했다.

첫 A import에서는 Unity가 새 임시 .meta를 Temp에서 source로 옮길 때 `ViewPartitionBoundary`가 발생했다. 당시 구현은 모든 경계 이동을 거부했다. 정상 `unity-stop`은 35.957초 뒤 timeout했고 창 닫기도 완료되지 않아, 정확한 PID·시작 시각·프로젝트와 후보 seal을 확인한 소유 Editor만 강제 종료했다. 기존 candidate·source·cache·snapshot은 유지했다. 원본 범위가 없는 private 파일의 유효 extent만 전달하는 지속 intent와 동일 세대 복구를 구현한 뒤 자동 시험을 통과하고 같은 Library로 재개했다. 복구 후 A1/B/A2의 관측 I/O 실패는 0이며 private 이동은 각각 5건/792B, 4건/236B, 4건/236B였다. 최초 실패 로그와 강제 종료 기록은 삭제하지 않았다.

B와 A2의 mount CLI는 `OverlayStartUncertain`을 반환했다. cache replay는 각각 29.249초·21.107초였고 같은 provider가 이후 mounted임을 확인했다. provider를 중복 생성하거나 snapshot을 다시 캡처하지 않았다. 이 재확인 없이 최초 mount 반환을 준비 완료로 취급하면 안 된다. 회수 전 계측 스크립트의 Git 명령 이름 충돌은 데이터 회수 전에 실패했고 수정 후 재실행했다. native 관측에 필요한 프로세스 권한 재시도도 제품 성능의 개선으로 집계하지 않는다.

## 시간

| 단계 | 실제 관측 |
| --- | ---: |
| 준비된 snapshot의 worker A mount | 1.6734초 |
| worker B mount | 1.6403초 |
| 기존 worker A 재개 | 1.5973초 |
| A 후보 게시 | 1.3049초 |
| B 후보 게시 | 1.2941초 |
| VSS helper 내부 캡처 | 0.355초, 초기 payload 복사 0B |
| 첫 A launch → 최초 ready, 실패·수정·개입 포함 | 2,323.351초 |
| A 복구 launch → ready | 544.985초 |
| B / A2 launch → ready | 84.120 / 75.008초 |
| A1 복구 / B / A2의 cache replay | 7.428 / 29.249 / 21.107초 |
| A1 / B / A2 refresh 호출 시작 → 완료 | 32.203 / 25.464 / 25.001초 |
| A1 / B / A2 정상 Editor stop | 30.863 / 31.642 / 33.065초 |
| A1 / B / A2 독립 결과 보존 | 2.389 / 2.236 / 2.316초 |
| A1 / B / A2 unmount | 1.572 / 1.486 / 1.423초 |
| A1 / B / A2 Git 등록 회수 | 1.952 / 1.776 / 1.800초 |
| A1 stop 시작 → B ready | 180.643초 |
| B stop 시작 → A2 ready | 400.352초 |

A/B 후보 저널은 각각 8,010B/2,863B다. 내부 저널 준비·검증 시간은 0.0613초/0.0589초이며 receipt 게시와 CLI 기동을 포함하는 전체 시간은 위 표를 사용한다. 별도 snapshot 캡처·Git 등록 시간은 각 영수증에 남는다. 이 값은 작은 실험 worker의 값으로, 제품 `workspace ensure`나 대형 원본 전체 검증 시간을 대표하지 않는다.

마지막 두 값은 측정·도구 호출 사이의 운영 대기까지 포함한 실제 경과 시간이다. 자동 전환 latency나 두 방향의 순수 성능 차이로 해석하지 않는다. `cost-summary.json`에 각 시작 시각과 개별 호출 시간이 있어 비용을 분리할 수 있다. ready는 1초 polling의 소유 heartbeat이며 validation 성공과 별개다. 최초 A는 깨끗한 cold import 성능 표본이 아니다. 준비된 snapshot의 작은 worker mount나 내부 후보 export만으로 제품 `workspace ensure`의 전체 준비 시간을 대표하지 않는다. 최초 소스 검증·권한 준비·전체 CLI 운영 지연은 별도다.

## 저장량

아래는 A2 정상 종료·결과 보존·unmount·Git 등록 회수 뒤 같은 시점의 파일 장부다. `FILE_STANDARD_INFO.AllocationSize`로 읽은 파일 할당이며 경로별 payload 비율을 물리 공간으로 환산하지 않았다.

| 저장 위치 | 논리 파일 길이 B | 파일 할당 B |
| --- | ---: | ---: |
| worker A | 12,259 | 12,848 |
| worker B | 3,417 | 4,656 |
| 두 후보, receipt 포함 | 13,955 | 21,600 |
| 검증 source A1 | 9,461,409 | 9,462,392 |
| 검증 source B | 9,425,736 | 9,429,624 |
| 검증 source A2 | 9,425,782 | 9,429,624 |
| 공통 cache | 2,309,202,812 | 2,309,206,568 |
| 제어 기록 | 5,696,274 | 5,801,392 |
| snapshot 영수증·helper | 26,802 | 37,328 |
| 공유 Git, 보존 결과 포함 | 6,056,835 | 6,112,144 |
| 원본 Library | 0 | 0 |

A/B 후보 저널 자체는 8,010B/2,863B다. 공통 cache 저널은 2,309,202,263B이며 유효 payload 1,829,089,908B, 대체 payload 312,771,103B, 메타데이터/프레임 167,341,252B다. 현재 유효 경로 기준 Library는 1,829,015,829B, 그 밖의 cache는 74,079B다. source 저널의 각 유효 payload는 8,438,867/8,437,864/8,437,910B이며 대부분 Unity가 생성한 `.csproj`다. 이 값은 논리 범위 장부이며 파일 할당의 하위 배분값이 아니다.

소유 worker·candidate·source·cache·control·snapshot 파일 할당의 **관측 최고는 2,345,400,736B**다. 같은 관측 시각의 측정 증거 할당은 별도 4,826,392B였다. 10초 간격과 단계별 관측 365개 중 364개에서 소유 root 관측이 완전했고 회수와 겹친 1개는 불완전했다. 최초 표본은 worker/후보 생성 뒤·첫 import 중이므로 생성 이전 Git 기준은 unknown이다. 최종 Git 전체 할당을 이번 실행의 순수 추가 비용으로 표시하지 않는다. 마지막 회수 직후 원시 증거·닫힌 제어/영수증 파일의 할당 관측은 11,146,120B이며 이후 문서/요약 파일 추가는 이 시점에 포함되지 않는다.

격리 볼륨 VSS의 첫/마지막 데이터 단계 관측은 used 25,051,136/27,131,904B, allocated 둘 다 100,663,296B였다. snapshot 회수 뒤 0/0을 확인했다. 이 값은 볼륨 공유 관측이며 workspace별로 귀속하거나 파일 할당과 중복 합산하지 않는다. 파일 할당은 MFT·디렉터리·커널 캐시를 포함하지 않고 표본 사이 peak를 놓칠 수 있다. 보존한 VHD 파일의 host 물리 할당 감소도 주장하지 않는다. 같은 입력의 독립 Library 비교군을 이번에 실행하지 않았으므로 절감률·속도 배수는 계산하지 않는다.

## 독립 결과·보존·회수

| 결과 branch | revision | 내용 |
| --- | --- | --- |
| `codex/vc26/worker-a` | `ab36d65f7f3534985b18e822e1ff00e9dd2bb492` | 후보 게시 후 후속 코드값 24 |
| `codex/vc26/view-a1` | `48ec1758a02e2a791329b83e86e50e6b9f99c2f0` | A 코드값 23와 생성 source |
| `codex/vc26/view-b` | `9b75f64c7ae54f5981dfd2f2145f74b9472316ce` | B 코드값 33, A 전용 파일 없음 |
| `codex/vc26/view-a2` | `51db3be32bf32d1c129e736e56d1bcfe5ced22f5` | A 복귀 코드값 23와 별도 생성 source |

공통 실험 결과 ref는 다음 수집 때 갱신되므로 각각의 branch/revision을 보존했다. snapshot 삭제 전후 `git fsck --full --no-reflogs`, branch identity와 코드·.meta·A 파일 내용, 원본 HEAD/clean/hash를 확인했다. 결과는 기존 격리 VHD의 Git에 남기고 VHD를 분리했다. 원본 적용이나 worker에 Unity 생성 source를 병합하지 않았다.

소유 application 그룹의 process/working set/private bytes 0, provider 종료·open handle 0·mount 부재, Git 등록 제거를 확인한 뒤 정확한 `V:\vc26`의 trial 하위 경로만 회수했다. 종료한 provider PID가 별도 MCP Python 프로세스에 재사용된 사례는 시작 시각으로 구분해 해당 프로세스를 보존했다. 소유 snapshot만 마지막에 해제했고 기존 C 볼륨 snapshot `{78AD7813-DF73-4845-8513-F14CC6E87331}`은 유지했다. 관리자 제한 세션·측정기 종료와 V: 부재도 확인했다. unknown을 0으로 치환하지 않았다.

## 판단과 다음 경계

작업자와 불변 후보에 Library를 복제하지 않고 하나의 Library를 A/B/A에서 재사용하는 최소 경로는 동작했다. 그러나 2.31GB 저널의 21~29초 replay, 75~84초 Editor 준비, 31~33초 종료가 반복 작업 시간을 지배한다. 정상 cold import와 첫 A 렌더는 아직 확인이 필요하다. 따라서 기본 모드 전환이나 대형 PV 재개 근거로 삼지 않는다.

다음 제한된 작업 후보는 **정상 첫 import/첫 렌더 재현과 같은 cache의 반복 재연결 계측**이다. 긴 replay의 준비 응답을 실제 mounted 시점과 맞추고, 저널 읽기/검증·index 재구성·flush·Editor 준비 중 어느 구간을 줄일지 구분해야 한다. 이 후속 작업은 아직 실행하지 않았다. 원본 전체 복사·읽기 hydration·flush 정책 변경·온라인 정리·일반 임대 중단 복구·source 반환 병합·큐/Coordination 연결은 이번 성과에 포함하지 않는다.
