# 제품 validation workflow 검증

[English](../../verification/product-validation-workflow.md)

[제품 CLI와 실제 provider](../view-workflow.md)의 WF-01~05를 **선택 범위 완료·제한 유지 (`scoped-complete-with-limitations`)**로 판정한다. 독립 VSS worker A/B에서 게시한 후보를 같은 Unity view/cache로 A/B/A 검증하고, 후보·소스 세대·worker 결과를 독립 Git 객체로 보존한 뒤 소유 자원을 회수했다. 첫 cold import의 중단과 복구를 포함한 작은 fixture 실행이며 SF-03 전체, 대형 용량 목표나 cold 무중단 안정성의 통과가 아니다.

원시 증거는 [`artifacts/storage-foundation/wf-20260928/`](../../../artifacts/storage-foundation/wf-20260928), 최종 코드는 기준 `c1e3cc4` 이후의 이 문서와 함께 커밋된 작업이다. 파일·실행 파일·결과 hash는 [검증 manifest](../../../artifacts/storage-foundation/wf-20260928/verification-manifest.json), 전체 자동 시험은 [최종 TRX](../../../artifacts/test-results/wf-product-20260928.trx)에 고정한다. 실행 중 수정 전의 실패와 최종 실행을 섞어 이전 성공을 승계하지 않는다.

## 입력과 실제 실행 경계

- Windows x64 Unity `6000.3.22f1`, 작은 URP fixture, 기존 격리 NTFS VHD의 `V:/sf-compaction-20260926/unity`를 사용했다. 원본 HEAD는 `58f81b5bc6ff8a940d8a90872bd3fed858a54cc1`, 추적 파일 90개·보호 ref 29개이며 최초 원본 Library는 없었다.
- home과 독립 결과 bundle은 C:에 두었다. `workspace ensure`와 `validation` 제품 명령으로 작업했으며 S 경로에 Codex worktree나 Unity Agent Coordination을 추가하지 않았다.
- 사용자 승인으로 다시 연 관리자 helper는 두 고정 worker의 capture/observe/release와 해당 VHD 분리만 처리했다. 일반 CLI·Git·Unity는 비관리자로 실행했다. helper의 13개 요청과 종료는 `session-*.json`, `response-*.json`에 남아 있다.
- A worker `34e1837b50676e1afe7269b9`의 snapshot은 `{080F1F21-9E08-4630-BE98-7B1F65D9C7C5}`, B `8eed731485f92952225b7636`은 `{C79E3EDC-9DE0-4F67-A92F-EE688496B221}`이다. 하나의 snapshot을 A/B로 가장하지 않았다.
- view `unity-view`, cache `7fafcaf0d0eab504c3e762b7`, mount `home/validation-mounts/db2fbef9a6296ae0e1c17abf`를 유지했다. 세 번의 선택은 서로 다른 source generation이다. 현재 probe/render helper는 각 검증 source에만 넣었고 원본·불변 후보를 바꾸지 않았다(`instrumentation-*.json`).

canonical fixture의 짧은 PNG 경로 수정은 `splinter-unity`의 `development` 커밋 `6a54974`다. 실제 세대에서 컴파일한 render 파일과 같은 내용을 반영했고 canonical Editor는 실행하지 않았다.

## 실제 기능 판정

| 관문 | 관측과 증거 |
| --- | --- |
| WF-01 게시 | 정확한 Git 등록을 검증해 `.git`만 분리한 source-only 후보 A/B를 게시했다. A journal은 8,410 bytes다. 게시 후 A worker를 코드 24로 바꿔도 후보는 23이고 같은 게시 요청의 재전송은 원래 receipt를 반환했다(`candidate-*.json`, `publish-a-lost-response-replay-1.json`, `live-isolation.json`). |
| WF-02 전환 | 최종 A1/B/A2에서 같은 mount/cache와 정상 stop/unmount/restart, 로드된 코드·에셋·세대별 생성 파일을 확인했다. A2에는 A1/B에서 만든 source 출력이 없다(`*-started.json`, `ready-*.json`, `qualified-*.json`). |
| WF-03 증거 | 실제 refresh/stop 뒤 candidate seal·source generation·output seal에 성공을 기록했다. B의 검증 뒤 사용자 source 변경은 `ValidationSourceChanged`로 거부하고, 같은 pending observe 요청은 재시작·새 검증 후에만 성공했다(`observe-b-changed.json`, `observe-b-recovered-1.json`). |
| WF-04 보존/회수 | 의존 후보/소스가 있는 A의 release는 `SnapshotInUse`였다. 실행 중 B view의 proof를 worker 재연결로 바꾸려 하면 `SnapshotReadersActive`, 기존 proof hash 일치·mount 유지·I/O 실패 0이었다. 독립 결과 8개, cache·worker·VSS 회수와 VHD 분리 뒤 객체/내용 재검증을 완료했다. |
| WF-05 복구 | 별도 publish CLI PID 56460을 durable workflow intent 뒤 응답 전에 실제 종료했다. 같은 owner/request로 candidate를 복구하고 다시 재전송해 중복 없는 receipt를 확인했다(`interrupted-publish-process.json`, `interrupted-publish-ledger.json`, `recover-a-interrupted-1.json`, `replay-a-interrupted-1.json`). |

원본에는 소유 sentinel을 실제 기록해 계속 쓰기 가능함을 확인했고 worker/후보에 나타나지 않았다. 해당 sentinel만 정리한 뒤 원본 90개 파일의 SHA-256·HEAD·보호 ref 29개가 그대로이고 Git status가 clean임을 확인했다(`source-before.json`, `source-after.json`). 원본의 Git 객체 저장소에 결과 ref를 추가했으며 원본 checkout/index를 결과로 교체하지 않았다.

### 최종 코드·에셋·PNG

| 선택 | Editor PID | `Calculate(3)` | texture max | CandidateA 파일 | 첫 유효 pass 전경 픽셀 |
| --- | ---: | ---: | ---: | --- | ---: |
| A1 (`a1-stable`) | 52096 | 23 | 1024 | 있음 | `[9430]` |
| B (`b-final`) | 66692 | 33 | 2048 | 없음 | `[9457]` |
| A2 (`a2`) | 53252 | 23 | 1024 | 있음 | `[9430]` |

세 조건 모두 mesh 3개·vertex 3,001개·triangle 2,441개, 누락 mesh 0, 씬 복원을 확인했다. 요청 ID가 반영된 PNG 경로·SHA-256·640×480 크기·pass별 픽셀을 다시 검사했고 실제 이미지를 열어 모델을 확인했다. 모두 최대 2회 안의 첫 pass가 유효했다. A1/A2 SHA-256은 `5138d38f72aaa13739b4fb0f302b262db6d4338ce4dde9d5854a591e699e72a2`, B는 `df94cefbc5f0d2fa6b46b878badb8848bc0221d66130778347349725a47b632d`다. `png-verified-*.json`과 `preview-*.png`를 보존한다.

A1에는 같은 세대에서 진단 중 만든 `Validationa1*.txt`가 남고 B에는 `Validationb.txt`·`Validationbfinal.txt`, A2에는 `Validationa2.txt`만 있다. 재시도 중 생성한 파일을 삭제해 깨끗한 첫 실행으로 꾸미지 않았다. worker A의 후속 24를 후보 A의 검증 성공으로 승격하지 않았으며 `preserve-worker` 응답은 `validationClaimed=false`다.

## 비용과 저장량

| 측정 | 관측값 | 해석 |
| --- | --- | --- |
| A/B ensure | 각 14.28초 | 이미 승인된 helper receipt 뒤 제품 CLI 시간 |
| A/B publish | 2.62 / 2.55초 | source-only 작은 후보. Editor 준비와 별도 |
| 최종 A1/B/A2 프로세스 시작→관측 ready | 57.58 / 47.26 / 75.38초 | recovered/warm cache, 소수 표본. A1/B의 진단 재시작을 포함하므로 신규 비용 비교 아님 |
| 정상 stop | 8.68 / 8.62 / 9.15초 | 최종 세대의 명시적 종료 |
| source 독립 보존 | 7.64 / 6.10 / 6.00초 | 후보/worker 보존 시간과 구분 |
| view 관련 파일 할당 | A1 2,339,465,032 → B 2,494,604,320 → A2 2,639,363,600 bytes | cache·세대 source·제어 파일 합계. 논리 payload나 working set이 아님 |
| 회수 후 view 관련 파일 할당 | 882,704 bytes | 영수증·로그 등 보존 metadata. active source/cache journal은 0 |

`storage-*-stopped.json`은 여섯 root를 구분하며 Git 공통 저장소와 원본 Library를 별도 기록한다. A2의 view 관련 관측치는 약 2.46 GiB다. 이는 관측 시점의 최대이며 실행 중 모든 임시 파일의 순간 peak가 아니다. 중단 때 mount 밖으로 보존한 `stranded-a1-files`와 독립 bundle/저장소, 원시 시험 artifacts도 home의 active 저장량 합계에 포함하지 않는다. 전체 추가 물리 공간 목표 달성을 주장하지 않는다.

일반 계측의 VSS 관측은 unknown이며 workspace에 귀속하지 않는다. 별도 관리자 probe에서 V: shared VSS는 중간 used 1,277,952 / allocated 33,980,416 bytes, 최종 모두 0이었다. C:의 VSS 저장량과 snapshot은 이 작업에 배분하거나 합산하지 않았다. NTFS 원본 이관, 최초 전체 데이터 복사·읽기 hydration·파일 전체 copy-up fallback을 추가하지 않았다.

## 실패·복구와 남는 제한

1. 최초 start의 mount parent 생성과 WinFsp 소유권 관측 누락을 수정했다. 이어 첫 cold import에서 provider 상태 파일의 `File.Replace`가 `0x80070497`로 실패했고 unmount 후 Unity가 종료됐다. 시험 reader 일부의 delete sharing 누락도 확인했다. reader는 공유 삭제를 허용하도록 고쳤고 Windows IPC는 **동일한 flush된 임시 파일**에만 32/33/1175 오류를 최대 1초 재시도한다. 먼저 지우거나 속성을 바꾸는 우회는 없다. 원인 하나를 확정하거나 새 cold의 무중단 성공으로 판정하지 않는다.
2. unmount 뒤 Unity가 일반 디렉터리에 남긴 Library/Logs/Packages는 소유 프로세스가 0임을 확인한 뒤 정확한 mount 경로 전체를 `stranded-a1-files`로 보존했다. manifest와 interrupted Editor 로그를 남겼고 같은 후보/세대/cache로 재개했다.
3. 원본 `58f81b5`의 구형 미리보기는 현재 FR 계약을 충족하지 못했다. 현재 canonical probe를 검증 source에만 주입했다. 긴 control 경로에서 Mono 임시 PNG 경로 한계도 재현하여 fixture의 최종 이름을 `preview-<요청 SHA>.png`, 같은 디렉터리 임시 이름을 독립 GUID로 짧게 했다. 실패 응답과 이전 PNG를 보존했다.
4. Unity의 사라진 `~UnityDirMonSyncFile~<32 hex>~`까지 일반 source 변경으로 세어 정상 refresh가 거부됐다. 후보·현재 source에 모두 없고 Assets/ProjectSettings 직속인 정확한 이름만 sequence에서 제외한다. 전체 journal/seal은 그대로 남고 일반 사용자 파일 변경은 최종 B에서 실제로 거부됐다.
5. 최초 source release의 잠긴 journal 디렉터리 rename이 Windows에서 거부됐다. seal을 검증한 journal handle을 유지하며 정확한 파일을 unlink하고, durable 회수 intent와 metadata hash로 재시도하는 방식으로 수정했다. 같은 release 요청 재개와 세 source·세 candidate·cache의 실제 회수가 성공했다.
6. 실행 중 작성된 응답 20개는 Accepted의 후속 명령 또는 status의 필수 null 필드가 빠져 schema 검사에 실패했다. 원시 응답은 그대로 보존한다. 최종 formatter로 실제 결과 57개를 재직렬화한 응답과 status 후속 payload 15개, 실제 입력 50개는 모두 통과했다(`schema-audit-final.json`). 이는 **직렬화 재검증**이며 VSS/Unity 재실행으로 세지 않는다. 최종 바이너리의 VHD 분리 후 실제 status 응답도 schema에 적합하다.

최종 `pwsh -NoProfile -File scripts/build.ps1`은 locked restore·Release 빌드 **경고/오류 0**, 자동 시험 **511 Passed / 0 Failed / 1 Skipped**다. skip은 현재 폐기한 backend의 당시 실행 조건에 따른 기록이다. serializer 수정 전 런타임의 최종 A/B/A, 이후 회수 수정의 실제 실행, 최종 serializer 자동/기록 재검증의 경계를 구분한다. 새 cold 반복, 모든 명령 경계별 중단, 실제 재부팅·전원·물리 공간 장애, 일반 에셋·대형 프로젝트·ARM A/B는 이번 통과에 포함하지 않는다.

## 독립 결과와 회수

`independent-results.json`의 ref 8개를 C:의 `independent-results.bundle`과 bare 저장소에 보존했다. bundle SHA-256은 `521abd4e6b25b75324b0de2840fe27e429b2f35f68a064a0ca248feb66d992c3`다. VHD 분리 후에도 commit·tree·blob 누락 없이 각각의 코드/texture/CandidateA와 세대별 출력, 여섯 validation 결과의 commit SHA-256을 검증했다.

| 결과 | ref 끝 ID | 검증 코드 |
| --- | --- | ---: |
| source A1 | `23b692554774a2717d752ee4` | 23 |
| source B | `003f01e68336b69cb2f1a53d` | 33 |
| source A2 | `654741e9950e2174ecdf6ad9` | 23 |
| candidate A | `3e66cf70e3b4776c1b08dfa2` | 23 |
| candidate B | `7f69a4baf776ed9fda80e9c9` | 33 |
| candidate A 후속 | `d98090f6834345971d32039d` | 24 |
| worker A | `34e1837b50676e1afe7269b9` | 24 |
| worker B | `8eed731485f92952225b7636` | 33 |

위 여섯 결과의 prefix는 `refs/splinter/validation-results/`, 두 worker는 `refs/splinter/snapshot-results/`다. `closeout-after-detach.json`, `response-final-space.json`, `final-environment-confirmed.json`이 최종 근거다. 소유 앱/provider 프로세스·메모리·handle·mount·Git worktree 등록·source/cache/worker delta·V: snapshot은 모두 0/부재이고, 원본 저장소의 worktree 등록 하나는 유지했다. 기존 C: snapshot `{78AD7813-DF73-4845-8513-F14CC6E87331}`과 시험 도중 외부에서 생긴 다른 C: snapshot도 보존했다. 시험 VHD는 삭제하지 않고 분리했으며 관리자 helper도 종료했다.

제품 기본 모드 선택, 일반 임대 인수, 생성 source 반환 병합, FIFO/Coordination, 설치·binding·discard·권한 서비스는 [별도 계획](../plans/storage-priority-and-editor-modes.md)에 남는다. 이번에 연결한 제품 CLI·VSS·Unity 경로를 다시 미구현 후속으로 분류하지 않는다.
