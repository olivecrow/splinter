# Checkpoint와 release 최적화

[English](../../verification/checkpoint-release-optimization.md)

이 문서는 [LP 프로파일링](checkpoint-release-profiling.md) 이후 2026-10-07의 한정 최적화 **CO-01–08**을 소유한다. 첫 [측정과 후보 hash](../../../reports/benchmarks/checkpoint-release-optimization-20261007.json)는 `74518fd`를 기준으로 한 변경 전 일반 배포본과 CO-01–04 최적화 배포본을 구분한다. 아래 CO-05–08은 그 최적화 배포본과 후속 후보를 비교한다. 이전 Unity 시간은 과거 근거로 유지한다.

후속 실제 Unity 입력의 비용·프로파일·회수는 [UR-01–04](unity-lifecycle-remeasurement.md)에 별도로 기록한다. 아래 CO 기록의 대형 Unity NotRun은 해당 소형 측정 당시의 자격이다.

## 변경과 보호 경계

- **CO-01:** 경로 50개마다 실행하던 Git 속성 프로세스를 UTF-8 NUL stdin, 256 KiB를 목표로 한 묶음으로 바꾼다. 현재/cached filter·encoding 검사, 전후 해석 digest 바이트, HEAD/index/경로 목록·provider 세대 검사를 유지한다. 1,602개 경로가 한 입력 묶음에 들어가면 전체 검사당 속성 프로세스 수는 132에서 4로 줄어든다. 코드에서 도출한 횟수이며 새 대형 프로젝트 시간 측정이 아니다.
- 범위 저널의 첫 checkpoint는 최초 검사와 최종 응답만을 위한 소스 검사를 생략한다. 자원 준비 상태와 소유 앱 종료가 계속 필요하며, 소스 adapter가 checkpoint를 검증하고 seal을 갖춘 독립 수집을 수행한다. 기존 revision 재요청은 현재 소스를 계속 검사한다. 최종 응답은 정확히 `sourceVerification=not-checked`이고 `source`를 생략하며, 검증한 결과 revision/fingerprint/seal은 `workspace.snapshot.result`에 남는다.
- 변경 없는 release는 저장한 pre-seal과 정확히 일치하고 새 clean revision/fingerprint 검사 한 번과 post-seal 불변을 확인해야 stable 독립 결과를 재사용한다. 독립 ref/object 검사와 unmount 후 seal을 유지한다. 생성 데이터/infrastructure가 바뀌면 전체 수집을 수행하며, 소스 불일치·결과 손상·동시 쓰기는 작업을 보존한다.
- **CO-04:** 이미 연 회수 fence 경로를 HashSet으로 검색하며 `WorkspaceStore.SamePath`와 같은 전체 경로 정규화·OS 대소문자 비교를 사용한다. 파일 존재 검사·모든 핸들·inventory 검사 두 번·삭제 전 hash·내구성 receipt·같은 intent 복구를 유지한다. 검색 비용은 제곱 비례에서 기대 선형으로 줄어든다. 저널 형식·flush/ACK 정책·inventory 관측 cache는 변경하지 않았다.

## 한정 전후 근거

**CO-03:** 같은 소스 commit에서 일반 배포본마다 순서대로 한 번 실행했다. **tracked 파일 64개 / 5,510 B**, **합성 Library 파일 256개 / 65,536 B**, `git-snapshot`, Unity 디렉터리 정책과 기본 SJL4를 사용한다. 각 task는 소스 한 줄과 private Library 파일 128개의 한 바이트를 바꾼다. Unity Editor나 실제 에셋 import는 실행하지 않는다. 소유 home은 별도이고 원본 소스/Library 바이트는 동일하며 변경 후 측정 전에 빌드와 전체 시험을 마쳤다. OS cache 초기화나 추가 프로파일링 scope가 없다. 프로세스 시작과 최대 100 ms driver polling을 포함하며 명령별 240초 관측 한도에서 강제 종료/재시도하지 않는다.

| 작업 | 변경 전, s | 변경 후, s | 감소 | 소스 검사 전 → 후 |
| --- | ---: | ---: | ---: | ---: |
| Checkpoint | 42.3045 | 31.8774 | 24.65% | 7 → 5 |
| Release | 20.6558 | 14.5439 | 29.59% | 2 → 1 |
| 합계 | 62.9604 | 46.4213 | 26.27% | 9 → 6 |

소스 검사 합계는 checkpoint 33.8403 → 24.2087초, release 10.0437 → 4.7185초다. checkpoint는 외부 seal 측정 2회, release는 4회를 유지한다. 이 내부 시간은 명령 합계에 포함된다. 후보 간 결과 소스 fingerprint가 일치한다.

CO-04 단일 실행 microprobe는 32개 파일 warmup 후 같은 실제 NTFS 한 바이트 파일 1,024개와 열린 stream에서 이전 검색, 인덱스 검색 순서로 측정했다. 인덱스 생성을 포함해 **760.3767 → 63.2675 ms**다. 비교는 **524,800회**에서 **인덱스 검색 1,024회**와 인덱스 삽입 1,024회로 바뀐다. 전체 release 시간이 아닌 검색 근거이며 순서/cache/JIT 변동으로 과거 Unity 지연에 외삽하지 않는다.

## 회귀·종결·제한

**CO-02:** `pwsh ./scripts/build.ps1`의 locked restore, Release 빌드 **경고 0 / 오류 0**, 시험 **통과 1,160 / 실패 0 / 건너뜀 19**를 확인했다. 추가한 26개 case는 byte 한도를 넘는 NUL/Unicode 묶음과 literal argv 동등성, 현재/cached 미지원 속성, 응답 freshness, 이용 불가/중단된 release 준비 상태, 소스/revision/fingerprint/결과 변경, 검증 중 쓰기, 변경 저널 전체 수집, 다수 파일 중단 회수·외부 파일 보존·Windows writer fence를 다룬다. 기존 재수집 중단 시험은 보존 중 쓰기를 주입하기 전에 저널을 명시적으로 바꾸도록 갱신했다. 실제 성공한 전후 CLI 응답 10개도 별도의 단일 시험에서 기존 응답 schema validator를 통과했다.

변경 전 최초 Library 준비는 filesystem sandbox에서 provider 준비 전에 실패했다. 같은 identity/request를 일반 사용자 권한으로 성공적으로 재개했으며 준비 거부는 보존하고 checkpoint/release 합계에서 제외한다. 빌드 스크립트도 sandbox 내 restore 실패 뒤 일반 사용자 실행이 필요했다.

두 task 모두 `released`에 도달하고 소스 파일 하나의 독립 결과를 보존했으며 소유 mount/provider/handle/memory/delta 부재를 관측했다. 실행마다 원본 소스 hash 64개·Library hash 256개·clean HEAD·소유 basis ref 회수를 확인하고 Library를 일반 NTFS로 복원했다. 기존 Unity 프로젝트와 이전 결과는 수정하지 않았다. 원시 요청/응답·배포본 복사·receipt·시험 근거·계측 script는 Git 제외 `artifacts/lifecycle-optimization-20261007/`과 `.local/lifecycle-optimization-20261007/`에 보존한다.

후보마다 작은 workload 한 번이며 median이나 SLA가 아니다. **새 대형 Unity 수명 시간, Editor/import/compile/Play/Test/build, VSS, OS 중단, 전체 disk peak와 RAM 귀속은 NotRun/unknown이다.** CO-01–04 당시에는 소스 파일별 metadata 검사·seal/hash 순회·전체 보존 시 ignored tree 순회·주기적 저널 inventory 관측·provider 요청 polling이 남았다. 아래 CO-05–08은 그중 일부 비용을 추가로 줄인다. 과거 Unity 보존/release 2h 36m을 이 측정으로 최적화 완료나 해결로 바꾸지 않는다.

현재 후보의 새 native 일반 폴더/Plastic·변경 저널 fallback·중단·peer 실행도 NotRun이다. 해당 자동/fake 회귀와 이전 native 자격은 구분한다.

## 후속: 반복 작업 제거 (CO-05–08)

[후속 데이터셋](../../../reports/benchmarks/checkpoint-release-followup-20261007.json)은 CO-01–04 최적화 배포본 그대로의 새 기준 측정, 중간 후보, 최종 후보를 기록한다. 앞의 비용은 다른 실행과 합산하지 않고 보존한다. 이 문서의 전체 소유 범위는 CO-01–08이다.

**CO-05 — checkpoint 트랜잭션:** Git checkpoint와 독립 ref 보존은 Runtime 전후 seal 안에서 연산 한정 소스 증명을 공유한다. 일반 편집의 전체 소스 검사를 다섯 번에서 두 번으로 줄였다. staging/pinning의 provider 세대·경로 변경 이력·HEAD/index/경로 구성·attributes/config 해석·관리 index 제외·정확한 commit tree 검사는 유지한다. 누락된 추적 경로가 있으면 삭제된 `.gitattributes` 해석을 포함한 staged 전체 검사를 유지한다. pin 실패·응답 유실·seal 변경은 이전 성공 manifest를 보존하며 같은 task로 재시도할 수 있다. literal 관리 root는 Git ignored-file 순회에서 제외하고 wildcard 정책과 소유하지 않은 ignored 파일은 기존 검사를 유지한다. 지속 증명이나 상태 형식 이관은 추가하지 않는다.

**CO-06 — 저널과 요청 비용:** volume 크기 callback은 배타적 writer의 바이트 계수를 읽고 heartbeat는 writer 계수임을 명시한다. reconnect에서는 실제 파일로 계수를 다시 만든다. 명시적 관측·stopped 상태·seal은 실제 파일을 검사하며 poisoned writer는 activity를 unknown으로 반환하고 살아 있는 소유권/handle을 유지한다. SJL4 중복 세대와 payload 경로 검색은 set/dictionary로 바꾸고 완전한 checksum·재생·identity·seal 내용은 유지한다. 새 SJL4 회수는 소유 intent에 저장된 예상 seal을 고정하고 내부 reclaimer가 삭제 전 전체 새 seal을 검사하므로 중복된 바깥 seal을 생략한다. OFF/현재 저널 부재·중단 복구의 기존 검사는 유지한다. source/flush/stop 게시 시 provider를 즉시 깨우되 1초 heartbeat와 알림 누락/사용 불가 시 polling을 유지한다. 알림 자체는 요청 승인이 아니다. mutation별 durable flush/ACK는 바뀌지 않는다.

### 오프라인 저널 비용

크기마다 stopped SJL4 입력 하나를 한 번 생성하고 별도 .NET 프로세스로 변경 전→후 순서로 측정했다. private 파일마다 1바이트이며 물리 파일명/길이/SHA-256 목록과 전체 seal은 그대로였다. 중간 저장 후보의 측정이며 후속 source router 변경은 이 저널 코드를 바꾸지 않지만 최종 배포본의 별도 microbenchmark는 실행하지 않았다. 생성자와 seal은 별도 측정이며 seal에는 생성자가 한 번 더 포함된다. 16회 조회 열은 기존 실제 파일 `Bytes` getter와 `CommittedBytes` 비교이며 전체 callback이나 명령 시간이 아니다.

| private 파일 / 보존 backup 파일 | 읽기 전용 생성자, ms 전 → 후 | 생성자 포함 전체 seal, ms 전 → 후 | 바이트 계수 16회 조회, ms 전 → 후 |
| --- | ---: | ---: | ---: |
| 512 / 511 | 593.9489 → 535.8092 | 612.3981 → 537.9753 | 274.4681 → 0.1719 |
| 2,048 / 2,047 | 2,418.8014 → 2,068.0361 | 2,178.6386 → 2,033.5423 | 1,081.2229 → 0.1642 |

cache/JIT 영향이 포함될 수 있는 순서 고정 단일 관측이다. 반복 inventory 제거는 실행 중 바이트 조회 비용을 크게 줄이지만 전체 hash/재생 비용은 남는다. 작은 생성자/seal 개선으로 대형 프로젝트의 확장 성능이나 Unity 시간을 추정하지 않는다.

### 최종 제품 측정 (CO-07)

같은 새 fixture는 **추적 파일 64개 / 5,510 B**, **합성 Library 파일 256개 / 65,536 B**이며 `git-snapshot`·Unity 디렉터리 정책·기본 SJL4를 사용한다. 후보마다 같은 소스 한 줄과 private Library 파일 128개의 1바이트를 바꾼다. Editor는 실행하지 않는다. 후보마다 별도 소유 home에서 순서 고정 한 번씩 실행하며 cache 초기화나 profiler 주입은 없다. CLI 관측은 강제 종료/재시도 없이 명령당 120초로 한정하고 프로세스 시작·최대 100 ms driver polling을 포함한다. 최종 일반 배포본 실행 전에 전체 시험을 완료했다.

| 작업 | 앞서 최적화한 배포본, s | 요청 wake 없는 중간 후보, s | 최종, s | 앞서 최적화한 배포본 대비 감소 |
| --- | ---: | ---: | ---: | ---: |
| Checkpoint | 28.6560 | 21.1214 | 14.0870 | 50.84% |
| Release | 13.6807 | 11.8697 | 9.6596 | 29.39% |

Checkpoint/release 합계는 **42.3367 → 23.7466초 (43.91%)**다. checkpoint 전체 소스 검사는 5회에서 2회로 줄고 release는 1회를 유지했다. 별도 준비/복원 비용은 Library prepare 7.7513 → 7.6478초, ensure 27.9540 → 24.6339초, Library restore 9.3588 → 8.7554초다. 중간 측정은 보존하되 최종 명령 시간에 더하지 않는다. wakeup 중간 후보는 checkpoint 17.9085초·release 9.9637초였고 소스 검사 4회를 통해 CLI source router의 capability 전달 누락을 발견했다. 최종 후보는 선택한 provider가 지원하는 연산만 전달하며 기존 None/Plastic 경로를 유지한다. 모든 후보의 소스 결과 fingerprint가 일치한다.

### 회귀와 종결 (CO-08)

`pwsh -NoProfile -File ./scripts/build.ps1`의 locked restore, Release 빌드 **경고 0 / 오류 0**, 시험 **통과 1,203 / 실패 0 / 건너뜀 19**를 확인했다. 실제 CLI 응답 20개는 별도의 단일 시험에서 기존 응답 schema validator를 통과했다. 추가 검증은 소스 재쓰기/복귀·rename·provider 세대/unknown·config/attributes·commit hook의 tree 변경·literal/wildcard ignored 정책·source router capability 선택·pin 뒤 실패와 동일 task/no-op 재시도·이전 manifest가 있는 응답 유실·seal 중첩 변경·계수/reconnect/poison·중복 저널 세대·실패/부분 회수·알림 게시/교체·알림 사용 불가 시 polling을 포함한다. 기존 중단 harness에서 신호 게시 순간 발생한 공유 오류는 기존 delete-sharing/한정 IPC reader를 사용하도록 수정했고, 저널 내구성 동작을 바꾸지 않고 최종 시험을 통과했다.

네 task 모두 기대한 소스 파일 하나의 독립 결과와 함께 `released`에 도달하고 mount/delta 부재·소유 process/memory/handle 0을 관측했다. 원본 소스/Library hash와 clean HEAD를 전부 확인하고 소유 basis ref를 회수했으며 fixture Library를 일반 NTFS로 복원했다. **관리자 권한·UAC·서비스 설치/갱신·기존 Unity 프로젝트 변경은 사용하지 않았다.** 원시 script·요청/응답·고정 배포본·종결 근거는 Git 제외 `artifacts/lifecycle-followup-20261007/`, `.local/lifecycle-followup-20261007/`, `.local/lco7/`에 보존한다.

작은 Editor 없는 입력의 한정 단일 관측이다. 과거 대형 Unity의 2h 36m 구간은 **재실행하지 않았고**, 파일별 소스 metadata·전체 seal/hash·독립 객체 graph 검사의 비용도 남는다. 새 Unity import/compile/Play/Test/build·native 일반 폴더/Plastic/peer/중단 행렬·VSS·전체 disk peak·RAM 귀속은 **NotRun/unknown**이다. 자동/fake 보호 시험으로 이전 native 자격을 현재 후보에 승계하지 않는다.
