# 수명주기 읽기·복원 최적화

[English](../../verification/lifecycle-read-optimization.md)

이 문서는 [UR-01–04 실제 Unity 프로파일](unity-lifecycle-remeasurement.md) 이후 구현한 **LO-01–08**을 소유한다. LO-01–05의 비교 기준은 UR에서 사용한 고정 `38b6803` 배포본이며 `0e707ed`로 커밋했다. 아래 LO-06–08은 `0e707ed`를 기준으로 한다. 현재 후보는 데이터의 소스·바이너리 hash로 식별한다. 원시 근거는 ignored `artifacts/lifecycle-read-optimization-20261007/`에 보존한다.

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

디렉터리 전체 복원 모드나 시작한 intent의 이관은 추가하지 않았다. 전체 디렉터리 rename에는 별도의 복구·identity 작업이 필요하다.

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
