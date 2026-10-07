# 수명주기 읽기·복원 최적화

[English](../../verification/lifecycle-read-optimization.md)

이 문서는 [UR-01–04 실제 Unity 프로파일](unity-lifecycle-remeasurement.md) 이후 구현한 **LO-01–11**을 소유한다. LO-01–05의 비교 기준은 UR에서 사용한 고정 `38b6803` 배포본이며 `0e707ed`로 커밋했다. 아래 LO-06–08은 `0e707ed`를 기준으로 한다. 현재 후보는 데이터의 소스·바이너리 hash로 식별한다. 원시 근거는 ignored `artifacts/lifecycle-read-optimization-20261007/`에 보존한다.

## 제한된 Git proof 재사용 — LO-01

`GitObjectSnapshotTree`는 tree별 정확한 object ID·길이의 chunk proof를 LRU로 유지한다. **합계 4MiB, 최대 4,096개 항목**이며 생성 중인 proof까지 포함해 할당 전에 퇴출한다. miss는 전체 Git blob hash와 framing을 검증한 뒤에만 proof와 요청 바이트를 게시한다. hit는 다시 읽은 chunk를 proof와 대조한다. 당시 후보의 blob 전환은 부분 reader를 취소하고 payload chunk 하나를 무효화한다. 오류·close는 전체 proof를 버린다. 요청 staging, 64KiB chunk, 무진행 timeout, disk hydration 금지와 제품의 8GiB blob 상한은 유지한다.

실제 loose/packed Git 시험은 1MiB A/B/A 입력을 번갈아 읽는다. A 복귀에서 decode한 양은 **65,536바이트**이며 이전 전체 blob 증명 경로의 1,048,576바이트와 구분된다. 이는 한정 바이트 계수 근거이며 Unity 벽시계 시간이 아니다. alias 경로는 proof 하나를 공유한다. 작은 내부 시험 한도로 byte·항목 수 퇴출, MRU, 할당 전 예산 검사와 과대 proof의 decode·출력 거부를 확인한다. 파일 전환 뒤 캐시된 blob을 손상시키면 호출자의 전체 버퍼를 그대로 두고 캐시 전체를 무효화한다.

LO-01에는 reader 세션 pool과 기준본 읽기 중 저널 gate 해제가 없었다. 아래 LO-06–08 후속에서 추가했다. 보존 chunk 범위 밖의 읽기는 여전히 앞부분을 decode할 수 있으며 두 변경 모두 Git decode 비용 전체를 없애지는 않는다.

## Seal 읽기와 경로 검사 — LO-02

SJL4 capsule 검증 중 읽는 정확한 바이트를 composite seal에 함께 반영하며 물리 파일명 ordinal 순서와 기존 root/이름/길이/내용 framing을 유지한다. capsule/prefix/seed 검증, 현재 generation, 마지막 head/구성원/pending 검사와 독립된 전후·unmount 후 seal 연산은 그대로다. 이전 seal을 재사용하지 않는다. 독립 raw-file hash 시험은 빈 root와 여러 파일·보존 prefix 세대를 포함하며 payload 손상을 계속 거부한다. 한 번 고정한 길이로 전체 읽기와 마지막 길이 검사를 수행한다.

`EnsurePlainPath`는 디렉터리 존재·파일 존재·속성을 따로 조회하던 대신 조상마다 속성을 한 번 조회한다. 자식이 없어도 부모를 계속 확인하고 reparse와 접근 오류를 거부한다. Windows 복원은 절대 경로로 정규화하고 reparse를 따라가지 않는 핸들로 root부터 부모를 고정·검증해 반복문 안에서 전체 조상을 다시 순회하지 않는다. 문자열 경로 캐시로 identity 보호를 대체하지 않는다.

회수는 별도 inventory·hash·intent·삭제 직전 guard를 유지한다. 이 검사를 제거하거나 sharing·조건부 삭제 계약을 바꾸는 작업은 이번 구현 밖이다.

## 변경 없는 파일 복원과 durable cursor — LO-03

계획은 전체 검증한 basis가 여전히 핸들로 보호되고, private 범위가 없으며 base/최종/manifest 길이가 같을 때만 manifest의 최종·보존 hash를 재사용한다. 실행은 저장된 물리 identity와 전체 내용을 새로 검증하고 쓰기·삭제를 막는 같은 핸들로 이동·메타데이터 적용까지 수행한다. read-only를 포함한 변경 없음·rename·메타데이터 변경에 적용한다. 새 변경 없는 경로는 payload 순회를 약 **6회에서 2회**로 줄인다. basis 생성 검증과 실행 증명이 남는다. 이는 코드 경로 횟수이며 전체 Library I/O 비율의 실측은 아니다.

변경 파일의 범위 재생과 개별 durable data flush는 유지한다. 재시도 파일이 이미 최종 hash와 일치하면 같은 배타 output 핸들의 중복 hash만 생략한다. read-only 해제 전과 다시 연 뒤의 보존 바이트 검사는 별도로 유지한다.

완료 cursor는 기존 계획·cursor 형식으로 **128개마다**와 마지막 파일에서 전진한다. 초기·최종 기록, 생성 영수증과 payload flush는 개별 내구성을 유지한다. 묶음 게시 실패 시 완료 효과 128개가 기록되지 않을 수 있으며 진행 중 효과와 함께 재개 때 identity·내용을 다시 확인한다. 새 260파일 시험은 127/128/129개 효과 뒤, cursor 게시 전후의 중단, 동일 바이트 교체, read-only 바이트 변경과 같은 핸들의 쓰기·삭제 차단을 검증한다. 127/128/129파일의 정상 실행은 전진 게시 1/1/2회를 관측한다.

LO-03은 파일별 복원이다. 아래 LO-09는 변경 없는 Library의 별도 디렉터리 복원 모드를 추가하며, 시작한 v2 파일 계획은 전환하지 않는다.

## 일반 배포본의 한정 비용 — LO-04

| 후보 | 1회, 초 | 2회, 초 | 평균, 초 |
| --- | ---: | ---: | ---: |
| 38b6803 | 8.1917 | 8.1461 | 8.1689 |
| 현재 | 4.2431 | 4.2286 | 4.2359 |

이 작은 입력의 평균 복원 시간은 **48.15% 감소**했다. 현재 후보는 실행마다 cursor 전진 게시 3회와 같은 내용·소유 회수를 확인했다. 이전 코드는 파일별 260회 게시하며 해당 배포본에는 게시 observer가 없다. 실행 순서는 이전 1 → 현재 1 → 현재 2 → 이전 2다. [정확한 관측·후보 hash](../../../reports/benchmarks/lifecycle-read-optimization-20261007.json)를 보존한다.

복원 비교는 이전·현재 일반 assembly에 같은 기존 소유 파일 fixture를 사용한다. 로컬 NTFS의 변경 없는 파일 260개 × 65,536바이트와 SJL4다. fixture 준비와 마지막 독립 바이트·회수 검사는 타이머 밖이다. 파일 이동, 계획, 검증, 진행 게시와 회수는 포함한다. Unity·서비스·UAC·원본 프로젝트 변경은 없다. 데이터에 실행 순서와 개별 관측을 유지하며 이 작은 warm 비교의 비율을 UR의 23분 복원에 그대로 적용하지 않는다.

## 검증과 한계 — LO-05

LO-01–05의 `pwsh ./scripts/build.ps1` 실행은 **통과 1,224개, 실패 0개, 조건부 제외 19개**, 빌드 경고·오류 0개다. 새 회귀 20개는 Git proof 6개, 복원·cursor 11개, seal 형식·손상 3개다. 기존 packed/loose·역방향·취소·무진행 읽기, 파일 identity·retained-byte·변경 범위 복구, 저널 중단·회수 회귀도 같은 전체 실행에 포함된다.

기존 미커밋 작업과 앞선 UR 근거·schema 수정은 별도로 유지한다. README의 Unity 시간과 98.65% 할당 절감은 기록한 후보에 속한다. 새 Unity A/B·Play/테스트/빌드·저장 할당·RAM/전체 디스크 peak·OS 전원 장애 행렬은 실행하지 않았다. 현재 변경은 자동 손상·복구 회귀와 한정 실제 NTFS/Git 근거를 가지며 새 대형 Unity 시간으로 주장하지 않는다.


## 불변 읽기와 저널 메타데이터 분리 — LO-06

일반 `JournalDeltaStore`는 기존 gate 안에서 불변 파일 메타데이터를 고정하고 private 바이트를 복사하며 덮이지 않은 기준 범위를 기록한다. 이후 별도 직렬 reader gate에서 불변 범위만 읽는다. 별도 payload 복제/cache는 없다. private 바이트는 이미 호출자의 요청 버퍼에 있고 범위 목록은 기존 extent index로 한정된다. 동시 덮어쓰기·truncate·rename·마지막 unlink가 이전 private capsule을 회수해도 고정된 읽기 바이트는 바뀌지 않는다. 기준 읽기는 계속 직렬이며 Git decoder를 무제한 병렬 생성하지 않는다.

완료 시 writer poison·저널 길이·기준 가용성을 재검사한다. 읽기 실패도 수명 점유를 반드시 반납한다. 종료는 flush/accounting/restore를 포함한 새 호출을 거부하고, 모든 동시 종료 호출이 진행 중 읽기를 기다린 뒤 저널 핸들·소유 fence를 닫는다. 소유자는 여전히 store 다음에 snapshot/lease를 닫는다. `baseReadBytes`는 원자적 계수이며 공통 `lockWaitTicks`에는 별도 reader gate·OS/Git 대기가 포함되지 않는다.

결정적 시험 8개는 SJR2/SJL4의 기준 읽기를 막은 상태에서 조회·변경 완료, 변경 전 private 바이트 보존, truncate/rename/unlink, 동시 종료 두 건 중 읽기 성공·실패, writer 배타 fence 유지, poison 쓰기, payload 읽기 직렬화와 중첩 불변 candidate를 검증한다. 기준을 명시적으로 막은 동안의 진행성을 증명하며 native WinFsp 시간이나 Unity 처리량 시험은 아니다. 개발용 `SourceViewDeltaStore`의 외부 gate는 유지한다.

## 제한된 Git reader 세션 — LO-07

object별 LRU 세션 두 개가 decoder 위치와 검증된 64KiB chunk 두 개씩을 유지한다. 관리 payload buffer는 이전 64KiB에서 **256KiB**가 되며 소유 `cat-file` reader는 최대 두 개다. 세 번째 object는 가장 오래된 세션에 재할당한다. 끝까지 소비한 batch reader는 재사용하고, 부분 reader는 정확한 stdout을 닫고 소유 자식을 종료한 뒤 교체한다. 교차 읽기의 decoder 재생성과 작은 파일마다 새 프로세스를 만드는 비용을 함께 막는다. proof 퇴출도 해당 세션을 회수하며 오류·close는 한 cleanup이 실패하더라도 두 세션과 전체 proof를 정리한다.

생성 중 proof를 포함한 4MiB/4,096개 한도, 최초 게시 전 전체 object hash/framing, chunk proof, 전체 요청 staging, 무진행 timeout과 disk hydration 금지는 유지한다. 이전에 검증해 보존한 바이트는 이후 object store가 변경돼도 다시 읽지 않고 재사용할 수 있으며 여전히 고정 object와 일치한다. 매 hit의 최신 손상 감사는 아니다. 보존 chunk 두 개보다 이전 범위와 퇴출된 부분 세션은 여전히 재시작·prefix 해독이 필요할 수 있다. Git 자식/pipe/내부 packed-object 메모리는 관리 buffer 한도와 별도이며 전체 프로세스 RAM은 측정하지 않았다.

실제 Git 시험 8개는 loose/packed 교차·역방향 읽기, 오래된 범위 재시작, 여러 chunk 요청 staging, 세션/proof 퇴출, alias 재사용, 손상 재읽기의 부분 게시 거부, 모든 소유 자식 종료와 작은 blob 12개의 파일별 프로세스 생성 방지를 검증한다.

## 고정 입력 비교와 검증 — LO-08

고정 `0e707ed`와 현재 일반 assembly가 같은 1MiB blob 두 개를 실제 loose/packed object store에서 읽는다. 최초 전체 proof는 **두 후보 모두 2,097,152바이트**를 읽는다. A/B 4회차에서 0·65,536·131,072·196,608 위치의 16바이트씩을 요청하고, 이후 두 파일의 131,072 위치를 역방향으로 읽는다. 바이트/PID 관측과 후보 hash는 [데이터](../../../reports/benchmarks/read-concurrency-20261008.json), ignored 원시 근거는 `artifacts/read-concurrency-20261008/`에 보존한다.

| 지표: loose와 packed에서 동일 | 0e707ed | 현재 |
| --- | ---: | ---: |
| 전체 proof를 제외한 순방향 회차의 해독 바이트 | 1,310,720 | 524,288 |
| proof + 순방향 회차까지 관측한 개별 payload reader PID | 8 | 2 |
| 가까운 역방향 읽기의 추가 해독 바이트 | 393,216 | 0 |
| 가까운 역방향 읽기의 추가 reader 생성 | 2 | 0 |
| 동시 활성 payload reader 최대 | 1 | 2 |

이 패턴에서 순방향 해독량은 **60%**, 관측 reader 생성은 **75% 감소**했다. 매 실행에서 반환 바이트와 관측한 모든 reader PID의 종료를 검증한다. 최초 proof/준비를 제외한 warm 순방향 회차는 loose 0.6057428 → 0.003358초, packed 0.5899848 → 0.0031412초였다. 후보/store별 한 번씩, cache 제거 없이 이전 loose → 이전 packed → 현재 loose → 현재 packed 순서다. 이 작은 warm 시간을 실제 Unity 시작 절감률로 추정하지 않는다.

새 Unity/Editor/컴파일 프로파일, native WinFsp 동시성, 전체 프로젝트 RAM과 OS 장애 자격은 **NotRun**이다. 이전 초기 준비 **17분 22초**는 UR의 당시 후보 값으로 유지한다. 원본 Unity 프로젝트·설치 서비스·UAC 설정은 변경하지 않았다.

최종 `pwsh -NoProfile -File ./scripts/build.ps1`: locked restore 통과, **통과 1,240개·실패 0개·조건부 제외 19개**, **빌드 경고·오류 0개**다. 새 회귀 16개는 집중 실행 38개에서도 통과했다. 소스와 측정 Git 바이너리 hash가 최종 빌드와 일치한다.

## 변경 없는 Library 디렉터리 복원 — LO-09

새 빠른 경로는 원본 **SJL4** 저널의 durable sequence가 0이고 모든 논리 항목이 고정 manifest와 같은 Unity `Library`에 한정한다. 경로/base 경로·길이·종류·속성·모든 timestamp·UnixMode와 private 범위/삭제 부재를 대조한다. 최종 내용이 다시 같아져도 실제 변경 기록이 있으면 일반 경로다. 다른 저널 형식·관리형 전체 프로젝트 기준본·시작한 v2 파일 계획도 기존 경로를 유지한다. CLI 옵션·관리자 권한·서비스 변경은 필요 없다.

별도의 현재 **v1 디렉터리 계획**은 basis, 원본/delta 경로, overlay identity hash, seal과 root/모든 디렉터리/파일의 NTFS identity를 고정한다. 원본 manifest는 일반 manifest/proof 대조 외에 `Basis.ManifestHash`와도 같아야 한다. 최초 전체 hash 검증을 마친 basis 핸들을 유지한 채 계획을 durable 게시한다. 정확한 root와 양쪽 조상 경로를 핸들로 보호하며 같은 볼륨에서 대상 덮어쓰기 없이 한 번 이동한다. Windows 디렉터리 rename은 하위 파일 핸들이 닫혀 있어야 한다([Microsoft rename 규칙](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/ntifs/ns-ntifs-_file_rename_information)). 그 간격 뒤 모든 하위 항목을 다시 열어 identity·전체 내용·정확한 구성원을 검증한 다음에만 메타데이터 복원과 회수로 진행한다. 변경·교체·외부 항목은 이동한 원본 경로와 저널에 보존하고 실패한다. 기준본 경로에 다시 나타난 디렉터리를 재귀 삭제하지 않는다.

최종 파일/디렉터리 핸들은 메타데이터 적용과 소유 delta 회수까지 유지한다. 원본 identity 파일은 쓰기·삭제를 막고, SJL4의 정확한 파일별 회수 inventory가 durable 게시·검증된 intent 경계에서 보호를 넘긴다. v1 완료 검증 영수증은 계획 hash에 결합하며, 중단된 회수는 고정 inventory를 사용한다. 매 재시도는 최종 identity·내용을 새로 증명하고 계획한 메타데이터를 다시 적용한다. 이동 전 재개는 원본 내용도 이동 전에 재검증한다. 누락/모호한 root, manifest/identity/seal 변경, 충돌하는 모드와 고아 cursor/영수증은 보존·거부한다. intent 이관은 없다. 신규 복원은 변경 없는 payload를 여전히 두 번 읽으며 중단 재시도는 전체 읽기를 추가할 수 있다. payload cache·복사·hash 생략·data 내구성 완화·전원 장애 보장은 추가하지 않는다.

선택적 초기 파일 계획/cursor 읽기는 기존 직렬 복원 임대 안에서 부재를 즉시 반환하고 읽은 cursor를 재사용한다. 이전 신규 일반 복원은 약 **1초의 게시 대기를 세 번** 수행했다. 공유 오류 재시도와 손상 기록 거부는 유지하고, 필수 프로세스 간 게시 reader의 대기는 바꾸지 않는다.

새 시험 26개는 빈/비지 않은 Library, root/파일 메타데이터·identity, 핸들 유지 중 쓰기·삭제 거부, 부분/최종 delta 회수를 포함한 중단 경계 7개, rename 간격의 변경, 동일 내용 파일/디렉터리 교체, 외부 항목, 기준본 재생성, seal/identity/manifest-proof 동시 변경, 재시도 메타데이터 복원, 일반 경로 선택과 고아 cursor 거부를 검증한다. 기존 일반 복원/cursor 시험은 디렉터리 timestamp 변경을 넣어 파일별 경로의 범위를 유지한다.

## 불변 디렉터리 목록 메타데이터 재사용 — LO-10

`JournalDeltaStore.List`는 불변 목록이 반환한 `SnapshotEntry`의 경로가 일반 조회로 계산한 기준 경로와 정확히 같을 때만 그 메타데이터를 재사용한다. private 항목·삭제·이름을 바꾼 부모의 기준 좌표·제외/포함/금지 root와 경로 정책의 우선순위를 유지하며, 예상 밖 목록 경로는 기존의 정확한 경로 `Stat` 조회를 수행한다. 목록 처리 뒤 기준본 가용성을 다시 검사한다. 영속 cache·timestamp 변경·저널 또는 payload guard 완화는 추가하지 않는다.

Editor 없는 고정 NTFS 비교는 각 64바이트의 소유 파일 256개를 사용하고, 일반 `5b70e61` assembly와 새 Runtime assembly를 차례로 실행했다. Core/Windows assembly는 고정했다. 첫 목록의 중복 자식 `Stat` 호출은 **256 → 0**, `List`는 양쪽 모두 한 번이다. 반복 목록의 자식 Stat은 양쪽 모두 0이다. 반환한 모든 메타데이터를 불변 manifest와 대조하고 표본 payload 내용도 확인했다. 프레임 메타데이터 hash는 양쪽 모두 `1ebb6ff2c1e53b76839988a87e58e4fb93290136547fac9a29506143c44660d2`다. warm 단일 관측의 첫 목록은 0.012483 → 0.0040691초이며, 이는 작업량 계측이지 Unity 속도 개선 추정이 아니다. 원시 기록·계측기·바이너리 hash는 ignored `artifacts/startup-listing-20261008/`에 보존한다.

새 시험 7개가 실패/제외 없이 통과했다. 두 저널 형식, 이름 변경·수정/삭제/신규 항목, 정책 제외, 예상 밖 목록 경로, 목록 뒤 기준본 소실, 실제 소유 NTFS 핸들과 내용을 확인한다. 좁은 빌드는 경고/오류 0개다. 통합 전체 검증은 아래 LO-11에 기록한다.

기존 UR Editor 로그는 이전 후보 기록이다. `Scan`은 536.446218초, `CompileScripts`는 342.981118초이며, 첫 Bee backend는 539개를 평가하고 0개를 갱신한 뒤 60.86초에 DAG 재작성으로 돌아갔다. 그래프 생성과 다음 backend는 반올림된 2분00초·2분28초로 기록됐다. 로그는 `Library/Bee/*-inputdata.json` timestamp 변경을 보고하지만 새 workspace 경로가 생성 입력을 바꿀 수 있으므로 불필요한 재작성이라고 단정할 수 없다. Git 기준본은 현재 캡처 시각을 파일 메타데이터로 사용한다. 내용/identity를 증명하는 새 메타데이터 계약 없이 원본 timestamp를 복사하거나 Unity 쓰기를 억제하고 Bee cache를 바꾸는 작업은 포함하지 않는다. Unity 컴파일러 CPU 시간, 최신 후보 초기 refresh, 실제 WinFsp scan 시간은 **NotRun**이다.

## 한정 비교와 통합 검증 — LO-11

[관측값·소스·바이너리 hash](../../../reports/benchmarks/unchanged-library-startup-20261008.json)는 `5b70e61` 이전 후보와 현재 후보를 고정한다. 같은 기존 fixture 바이너리를 사용해 NTFS의 파일 260개 × 65,536바이트와 변경 없는 원본 SJL4 저널을 비교했다. 준비와 독립 최종 내용/회수 검증은 timer 밖, 계획·전체 내용 검증·메타데이터·이동·회수는 안이다. cache 제거 없이 이전 1 → 현재 1 → 보조 일반 경로 1 → 보조 일반 경로 2 → 현재 2 → 이전 2 순서로 실행했다.

| 복원 입력/경로 | 1회, 초 | 2회, 초 | 평균, 초 |
| --- | ---: | ---: | ---: |
| 5b70e61, 변경 없는 Library, 파일별 경로 | 4.2121 | 4.2077 | 4.2099 |
| 현재, 변경 없는 Library, 디렉터리 경로 | 0.5141 | 0.5168 | 0.5155 |
| 현재, 부모 rename, 일반 파일별 경로 | 1.2516 | 1.2610 | 1.2563 |

동일한 변경 없는 입력은 **이 작은 비교에서 87.76% 빨라졌다**. 파일 이동 260회가 디렉터리 이동 1회로 바뀌며 파일 cursor는 없다. 약 3초의 선택적 기록 고정 대기 제거 효과도 포함하므로 디렉터리 rename만의 절감률이 아니다. 보조 표본은 payload를 유지하며 부모 디렉터리 이름과 read-only 파일 하나의 메타데이터를 바꿨다. 일반 경로와 이동 260회의 유지 확인이며 동일 입력의 인과 비교가 아니다. 원시 첫 이벤트 시각은 이전 첫 파일 이동 3.439초, 현재 계획 게시 0.302초·디렉터리 이동 0.316초를 구분한다.

집중 검증은 **통과 44개·실패 0개·제외 0개**다(새 디렉터리 26개, 새 목록 7개, 기존 일반 복원 11개). 최종 `pwsh -NoProfile -File ./scripts/build.ps1`: locked restore 통과, **통과 1,273개·실패 0개·조건부 제외 19개**, **빌드 경고·오류 0개**다. 측정 바이너리 hash가 최종 빌드와 일치하고 기존 다른 작업의 미커밋 파일 27개는 변경되지 않았다. 이 후보의 대형 Unity 초기 Editor/컴파일 시간과 원본 Library 42,420개 파일 복원은 **NotRun**이며 과거 UR 수치는 바꾸지 않는다. 원본 Unity 데이터·관리자/UAC 설정·설치 서비스·Defender 예외·외부 도구 cache를 변경하지 않았다.
