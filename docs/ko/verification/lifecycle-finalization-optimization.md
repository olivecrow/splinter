# 최종화와 압축 해제 데이터 재사용

[English](../../verification/lifecycle-finalization-optimization.md)

커밋 `07bc2de`의 후속 실제 Unity 실행은 [FU-01](unity-finalization-rerun.md)에 별도로 기록한다. 병렬 통합 종료와 새 공유 Editor 비교를 포함하며 아래 LF의 한정 비용은 원래 범위를 유지한다.

이 문서는 [RC-01](unity-current-comparison.md)의 한정 후속 LF-01–05를 소유한다. RC-01의 직렬 A/B checkpoint/release는 199.5초였다. 소스 검사는 1,602개 내용 해시를 모두 재사용해도 회당 약 9초였고, 약 36 MB 저널마다 약 16,500개 파일이 있었다. 이 관측은 아래 변경의 근거이며 이번 후보의 시간은 아니다.

## 범위와 유지하는 경계

- **LF-01 — 소스 경로 확인:** 존재/유형/속성의 별도 조회를 한 번의 속성 조회로 바꾼다. identity를 확인한 mount 안에서 신뢰할 수 있는 저널 세대 전후 검사가 있을 때만 검사 연산에 한정한 유한 상위 경로 cache를 사용한다. leaf와 mount 밖 경로는 매번 확인한다. HEAD/index, 구성원, attributes/config, 소스 변경 이력, reparse 거부와 실패 경계는 유지한다. 명령 간 경로 증명은 저장하지 않는다.
- **LF-02 — capsule 읽기:** 형식 확인, frame 검증, block 위치가 같은 열린 파일을 사용한다. 임시 block descriptor(최대 131,072개, 구조체 데이터 약 3 MiB와 별도 collection overhead)를 세대 순으로 재생하여 index를 위해 capsule을 다시 열지 않는다. 예산을 넘으면 기존 block 위치 읽기를 수행한다. 완전/prefix checksum, identity, 세대와 최종 inventory는 유지하며 이전 seal/cache hash로 현재 물리 바이트 검사를 대체하지 않는다. 디스크 형식은 바꾸지 않는다.
- **LF-03 — 명시적 통합 종료:** 범위 저널 작업의 `workspace checkpoint --release`는 같은 task 잠금 아래 결과 보존부터 회수까지 이어 수행한다. 이번 호출에서 성공한 checkpoint의 post-seal만 바로 다음 release의 pre-seal을 대신한다. 새 소스 검사, 그 뒤 seal, 독립 결과 확인, unmount 후 seal과 회수 guard는 유지한다. 전체 seal 한 번, 중복 stop/인프라 제거와 중간 응답 관측을 줄이며 원본 통합은 하지 않는다.
- **LF-04 — 압축 해제 visitor cache:** 압축 payload 비교, 논리 hash와 compact visitor가 읽기와 같은 store별 64 MiB / 4,096-entry RAM cache를 사용한다. 매 hit에 현재 encoded bytes를 다시 읽고 검증한다. miss는 전체 decode 완료 뒤 buffer를 게시하며 작은 예산/초과 block은 유한 streaming을 유지한다. 읽기/decode/visitor 실패는 cache를 비우며 poison과 close의 회수, 각 mutation의 durable flush/ACK를 유지한다. cache 예산은 전체 프로세스 RAM이 아니다.

LF-03은 저장된 workflow/state 형식을 바꾸지 않는다. 기존 checkpoint나 재시도는 이전 호출의 메모리 증명을 재사용하지 않는다. `release-*`의 재시도는 기존 release를 재개하고 `released`는 멱등 완료다. `reclaim-pending`은 반환된 명시적 복구 동작을 계속 요구한다. 소스/저널 변경, 미완료 원본 반영, view 의존성, 다른 소유권과 불확실한 자원은 작업을 보존한다. 범위 저널이 아닌 checkout 모드의 `--release`는 변경 전에 거부한다.

checkpoint는 최종 수집 경계다. 반복 편집과 외부 검증 중에는 같은 workspace/Editor를 유지하며, 보존과 회수 모두 승인된 경우 통합 종료를 선택한다. 원본 통합이 필요하면 checkpoint → integrate → release를 따로 사용한다. checkpoint가 소유 Editor를 정상 종료하므로 별도 정지 상태 관측이 필요하지 않으면 직전 `unity stop`은 불필요하다.

## 검증

`pwsh -NoProfile -File ./scripts/build.ps1`은 locked restore와 경고/오류 0개 빌드 뒤 **1,571개 통과, 실패 0개, 조건부 건너뜀 24개**로 완료했다(총 1,595개, 7분 21.499초). 새 회귀는 cache hit의 현재 바이트 손상, visitor callback 전 잘못된 압축 데이터, visitor 실패 뒤 cache 비우기, descriptor 예산 fallback, 완전/보존 prefix 혼합 복구, checkpoint와 release 사이 소스/저널 변경, unmount 중 쓰기, 독립 결과 소실, 비소유/소유 프로세스 guard와 같은 intent의 release 복구를 포함한다. 첫 시험에서 새 시험 도구의 결함 두 가지(nullable callback 계수와 fixture writer를 닫기 전 hash 조회)를 발견했으며 수정 뒤 전체 시험을 통과했다.

후보는 `37cfa01` 위의 작업 트리 구현이며 고정한 CLI SHA-256은 `ed04b022b6a7ff0f696909161f270a5b775c8ce7d85f714ad9873441a0ea6d57`이다. 비교 기준 제품 코드는 `7630970`이다. [데이터셋](../../../reports/benchmarks/lifecycle-finalization-optimization-20261014.json)에 소스/DLL hash, 원시 근거 참조, 관측과 경계를 기록한다.

### 한정 구성요소 비용

| 시험 | 이전 | 이후 | 감소 | 측정 경계 |
| --- | ---: | ---: | ---: | --- |
| LF-01 경로 검사 | 2,727.34 ms | 707.47 ms | 74.1% | 1,602개 파일, 합성 mount/저널 허용 조건의 일반 NTFS 교대 5회 중앙값. 전체 소스 검사는 아님 |
| LF-02 정지 저널 생성자 | 400.35 ms | 311.32 ms | 22.2% | 같은 논리 파일 512개 / 물리 파일 515개 / 992,579 bytes, warm 3회 중앙값 |
| LF-02 전체 물리 seal | 418.48 ms | 329.81 ms | 21.2% | 같은 정지 fixture의 전체 manifest/seal 동일성. 현재 바이트 검사는 유지 |
| LF-04 반복 decoded visitor | 148.41 ms | 14.70 ms | 90.1% | 압축된 1 MiB block 8개를 각각 16회 방문. cache OFF/ON 각 2회 중앙값 |

LF-01의 metadata 조회는 회당 76,896회에서 19,348회로 줄었다. LF-02 seal의 관리 메모리 할당은 50,583,360에서 11,363,216 bytes로 줄었다(77.5%). 이는 누적 관리 할당이며 상주 RAM이 아니다. 첫 JIT 관측은 보존하되 warm 중앙값에서 제외하고 앞선 sandbox 측정은 진단 근거로만 남긴다. LF-04는 decoded payload 8 MiB(cache 계수 8,392,704 bytes)를 유지하여 압축 해제를 128회에서 8회로 줄이고 120회 hit를 기록했다. 기존 store 예산 64 MiB / 4,096개 항목은 유지한다. 이 감소율을 전체 checkpoint나 Unity 작업에 적용하지 않는다.

### LF-05 — 작은 실제 제품 종료

범위를 한정한 새로운 Git/WinFsp A/B 세 묶음에 같은 커밋 소스 바이트(추적 파일 68개, 그중 payload 64 × 1 KiB)와 Library 128 × 1 KiB를 사용했다. 각 작업은 자기 결과 텍스트 파일을 추가하고 private Library 128개 파일에서 각각 한 바이트를 바꾼다. Unity 정책을 선택하지만 Editor는 실행하지 않는다. 모든 묶음의 작업공간 생성은 직렬이며 마지막 종료의 실행 순서만 다르다.

| A/B 결과 보존과 회수 | 경과 시간 | 이전 대비 감소 |
| --- | ---: | ---: |
| 이전: checkpoint/release 분리, A 뒤 B | 65.771 s | — |
| 후보: `checkpoint --release`, A 뒤 B | 59.898 s | 8.9% |
| 후보: `checkpoint --release`, A/B 동시 | 37.740 s | 42.6% |

계측은 CLI 시작과 양쪽의 보존/회수를 포함한다. 준비, 생성, 편집, 결과 검증 조회와 Library 복원은 제외하며 해당 명령 시간은 데이터셋에 별도 기록한다. 방식마다 한 묶음이며 코드 변경들과 마지막 행의 동시 실행을 함께 비교한다. 특정 cache의 인과적 절감률을 분리한 시험은 아니다. 동시 실행 시 개별 종료는 30.278 s / 29.614 s에서 37.730 s / 36.506 s로 늘지만 양쪽을 기다리는 전체 시간은 짧아진다. 독립 A/B 명령의 동시 실행은 에이전트 절차이며 제품 scheduler가 아니다.

작업 여섯 개는 모두 `released`로 완료했다. 독립 Git 결과는 자기 변경을 포함하고 peer 변경은 제외하며, 직렬 묶음에서 A 회수 뒤 B 읽기도 유지했다. 마지막 관측의 mount/provider/delta는 부재이고 소유 handle/working set/private bytes는 0이다. Library 복원을 완료하고 원본 파일 hash·HEAD·Git clean 상태가 일치하며 Git worktree 등록에는 원본만 남았다. **실제 CLI 응답 20개** 모두 현재 응답 schema를 통과했다. fixture 데이터와 원시 근거는 보존했으며 무관한 프로젝트·프로세스·VSS snapshot·서비스는 변경하지 않았다.

근거 범위는 자동 계약/손상/복구 시험, 같은 작은 입력의 NTFS capsule 비용, decoded visitor 비용과 작은 Editor 없는 제품 A/B다. 이 시험으로 전체 Unity 초기화/컴파일, callback 귀속, Play/Test/build, OS 장애, 전체 시스템 RAM, 대형 프로젝트 scaling을 입증하지 않는다. 사용자의 원본 Unity 프로젝트와 설치된 서비스는 변경하지 않았다. README의 RC-01/SE-01 시간은 당시 관측으로 유지하며 이번 후보의 측정으로 바꾸지 않는다.

## 남은 구조 작업

이번 변경으로 임의의 새 작업이 실행 중 Editor의 프로젝트 상태를 인수하지 않는다. 자동 Editor pool/reset/rebase, private 읽기의 수명 잠금 밖 이동, 새 저장 형식으로 capsule 묶기와 release의 새 소스 검사 제거는 별도 상태/수명 설계가 필요하다. cache로 현재 손상을 숨기거나 mtime을 내용 증명으로 삼지 않는다. RC-01의 약 87초 초기화 callback 구간은 원인을 변경하기 전에 함수별 귀속을 확인해야 한다.
