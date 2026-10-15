# Bee 내부 native backing 검증

[English](../../verification/bee-internal-native.md)

**현재 상태: 폐기 / 제거 완료.** 시간·저장 기준 실패 뒤 사용자가 이 후보와 이전 junction 방식의 제거를 요청했다. 현재 코드에서 `tools/Splinter.BeeProbe`와 `scripts/measure-bee-prototype.py`를 제거했으며 구현은 `93a7395`에 보존한다. 아래 당시 결과·수치·자격 경계와 원시 보고서는 변경하지 않은 역사적 근거다. 현재 명령·선택적 backend·후속 구현 계획이 아니다. 제품 제한 저널과 성공한 도구는 유지한다.

**2026-10-15: 소형 정확성 gate 통과, 전체 성능 gate 실패.** 이 문서는 [원자적 Bee 저장 설계](../plans/bee-native-storage-design.md)의 BN-01–05를 소유한다. 제품 제한 저널은 `d0ac0a2`로 커밋했다. 이번 후보는 선택한 단일 볼륨 구조를 `tools/Splinter.BeeProbe`에 구현하며, 일반 제품 작업공간에서 native Bee 저장을 활성화하지 않는다. 이전 junction 실험과 원자 rename 실패는 [SX 기록](structural-throughput-prototypes.md)에 보존한다.

새 구조는 실제 Bee 컴파일과 소스/결과 보존을 완료했지만, 측정 구간 평균 합계가 **11.184초에서 18.883초로 증가(+68.8%)**하고 최종 할당도 **0.918MiB에서 2.128MiB로 증가(2.318배)**했다. 이 후보를 제품에 채택할 근거는 없다. [정확한 수치·hash·provenance](../../../reports/benchmarks/bee-internal-native-20261015.json)는 성공한 4개 실행과 실패 pilot을 구분해 보존한다.

## 근거 상태

| ID | 근거 | 상태 |
| --- | --- | --- |
| BN-01 | 단일 namespace, 파일 identity, 상속 데이터의 복사 없는 범위 쓰기, 소스 승격과 원본/peer 보호 | 아래 소형 mount/직접 store 경계에서 통과 |
| BN-02 | durable 소스, native flush, 정확한 owner/inventory, 동일 owner 프로세스 복구 | 중단 경계 10개와 소유권 guard 통과 |
| BN-03 | 데이터 mapping·실행 이미지, 일반 cache drain, 정상 종료 | `native-safety-06` 통과; 살아 있는 mapping/image의 stop 거부 |
| BN-04 | 실제 cold/unchanged/incremental Bee 빌드, 상수 `42 → 43`, 직렬 ABBA 시간/저장/RAM | 정확성 통과; 전체 성능 수용 기준 실패 |
| BN-05 | 필수 locked restore, Release 빌드/전체 회귀, 양쪽 언어 문서 | 명시한 개발 후보 범위에서 통과 |

## 구현한 후보

하나의 WinFsp 프로젝트 볼륨이 모든 논리 이름과 파일 identity를 소유한다. 새로 생성한 `Library/Bee` 파일만 비공개 opaque NTFS payload를 받으며, Bee junction이나 native client 경로를 노출하지 않는다. 제한 저널은 namespace의 권위와 소스 데이터 저장을 유지한다. 상속 Bee 파일의 읽기와 부분 쓰기는 불변 기준 범위와 private 저널 범위를 계속 사용한다. 상속 데이터를 읽거나 일부 덮어써도 native 전체 복사를 만들지 않는다.

native 생성 payload는 일반 NTFS buffering을 사용한다. 명시적 flush는 모든 native payload와 저널에 도달하며, 소스 쓰기는 mutation별 durable ACK를 유지한다. 새 생성 파일을 소스로 옮기면 애플리케이션이 쓴 bytes를 같은 identity의 durable 저널로 먼저 가져와 검증한 뒤 소스 경로를 게시한다. 이미 열린 handle을 통한 후속 쓰기도 저널로 전환된다. Bee로 이동한 소스 파일은 계속 저널을 사용한다. 이는 재생성 가능한 데이터의 내구성 tradeoff이며, 동일 내구성 비교나 소스 ACK 완화가 아니다.

소유 root/조상, opaque payload identity, 정책, association과 현재 버전 복구 intent를 검증한다. unknown 파일, 다른 owner, 지원하지 않는 pending 전환, hardlink는 보존하고 거부한다. native payload에는 별도 byte 예산이 있으며 저널 observation은 명시적으로 저널만 계산한다. native control 파일과 payload를 따로 집계한다. 승격한 native payload는 회수까지 소유 이력 입력으로 남으므로 durable 소스 저널 bytes와 일시적으로 중복될 수 있다.

## Namespace와 소스 안전성: BN-01

최종 실제 mount fixture는 `artifacts/bee-internal-20261015/native-safety-06/`다. 검사 행 15개와 정상 종료가 통과했으며 열린 handle과 retained context는 모두 0이다. 상대/절대 경로의 새 이름 이동과 replacement를 native `MoveFileExW`의 copy fallback 없이 검사했다. 게시 뒤 소스 file index를 유지한다. 디렉터리와 대소문자 전용 rename도 identity와 내용을 유지한다.

실제 replacement는 destination을 먼저 닫고, 실제 디렉터리 이동은 하위 handle을 먼저 닫아 검증한 일반 Windows 연산 조건을 따른다. 교체된 기존 native/상속 identity와 이동한 하위 identity의 보존은 명시적 release까지 직접 delta 계약으로 별도 확인한다. POSIX replacement/unlink 의미를 주장하지 않는다. delete-pending 이름은 마지막 handle이 닫힐 때까지 보일 수 있다.

64MiB 상속 sparse 파일을 읽고 offset 17,825,915에 4KiB를 덮어썼다. native payload/파일 증가와 최초 payload 복사는 모두 0이며, 이 압축 가능한 patch와 metadata의 저널 증가는 760B다. 새 생성 파일로 상속 이름을 교체할 때 상속 bytes를 복사하지 않는다. 0/짧은/여러 chunk/offset 쓰기, 0으로 채운 hole, truncate/re-extend와 timestamp도 확인했다.

소스 전환은 identity와 기존 handle을 통한 쓰기를 보존한다. 독립적으로 내보낸 27-byte 결과 두 개를 unmount 뒤 확인하고, 기준/native 입력 모두를 소유 exclusive handle로 읽기 거부한 상태에서도 다시 읽어 검증했다. 원본 fixture hash와 별도 peer sentinel은 변하지 않았다. 이는 독립 peer sentinel을 둔 소형 fixture이며 새 제품 A/B 수명주기나 Git/Plastic 원본 반영 자격이 아니다.

이 후보에서는 아직 승격하지 않은 native 하위를 가진 디렉터리를 소스 영역으로 옮길 수 없다. 직접 store 요청은 namespace 게시 전에 거부하고 내용을 유지한다. 이 명시적 제한 때문에 제품 파일시스템을 투명하게 대체한다고 주장할 수 없다.

## 복구와 소유권: BN-02

`artifacts/bee-internal-20261015/recovery-02/recovery-result.json`은 정확히 소유한 자식 프로세스의 중단 **10/10** 통과를 기록한다. 생성 경계 2개, native payload flush, namespace rename, 소스 승격 경계 4개, payload 회수 경계 2개다. 같은 identity로 재연결해 ACK한 durable 소스 sentinel을 보존하고 연산을 완료한 뒤 두 번째 reopen도 확인한다. 승격 소스의 독립 export는 store를 닫은 뒤에도 읽히고 원본 입력 bytes는 유지된다.

guard는 unknown 파일, 다른 저널 owner, 불일치 pending owner, 다른 native file identity의 pending association, 관계없는 동일 owner pending 연산과 hardlink를 보존·거부한다. 활성 native root는 교체할 수 없다. pending 문서는 의미상 owner/identity와 허용한 전환을 검증한 뒤에만 교체하며, checksum이 맞는다는 이유만으로 기존 세대를 덮어쓰지 않는다. stopped proof는 정확한 inventory, payload identity/내용과 owner/association/promotion control 내용을 검증한다.

이 검증은 실제 파일 store 자식을 종료하며 mount provider나 Editor를 강제 종료하지 않는다. 이 복구 fixture의 reparse 주입은 명시적으로 **NotRun**이다. OS crash, 전원 장애, 적대적인 동시 변경이나 모든 파일시스템 공격 행렬의 자격이 아니다.

## Cache와 mapping 수명: BN-03

후보는 `FlushAndPurgeOnCleanup`을 유지한다. 개발 전용 stop barrier는 정확한 mount root/볼륨을 확인하고 자기 root flush handle만 허용한 상태에서 새 open을 막은 뒤 kernel cache를 flush한다. 현재 연결된 retained identity에는 WinFsp cache 무효화 알림을 보낸다. 삭제된 identity를 재사용된 경로로 알리지 않는다. 성공하려면 실제 마지막 `Close` callback과 handle/context 0이 여전히 필요하며, counter 초기화나 강제 unmount를 하지 않는다.

실제 mount 검사는 쓰기 가능한 데이터 mapping을 유지한 채 원래 파일 handle을 닫는다. stop은 거부되며 그 후에도 mapping을 읽고 쓸 수 있다. 적재한 실행 이미지도 stop을 거부하고 계속 읽힌다. 이미지를 unload한 뒤에는 같은 일반 barrier가 정상 drain하고 출력 파일을 보존한다. 이 통과 검사에는 파일명별 reopen이나 삭제를 사용하지 않는다. 임의의 외부 mapping 부재를 증명한 것은 아니다.

최초 `bee-pilot-01`은 실제 Bee 빌드 3개를 마쳤지만 열린 handle 0, retained context 1인 상태에서 정상 종료가 timeout됐다. provider와 출력을 보존했다. 별도 기록한 복구는 정확한 `1900b0aE.dag_derived` 파일을 write-data/read-attributes 접근으로 열었다가 닫았으며, 내용·metadata·보호 inventory는 바꾸지 않았다. 이후 provider는 정상 종료하고 독립 소스 결과를 보존했다. 이는 과거 실패 실행의 복구이며, 마지막 provider 영수증의 `passed`가 실패한 측정 sample을 성공 cohort로 바꾸지 않는다. 현재 측정 스크립트에는 이 파일명별 정리가 없다.

## 실제 Bee 측정: BN-04

고정 입력은 보존된 Unity 6000.3.22f1의 3개 assembly fixture다. 패키지, Editor, IL post-processing, Play는 없다. 스크립트는 고정 입력/runtime hash를 확인하고 설치된 graph generator와 Bee/Csc backend를 외부 도구 수정 없이 실행한다. 각 새 view는 생성 출력이 없는 상태에서 graph 1회와 cold, unchanged, 한 소스 변경 빌드를 수행한다. 컴파일된 `RuntimeSentinel.Value`는 `42`, `42`, `43`이어야 한다. state/digest 파일은 같은 가상 볼륨에 게시되고 임시 게시 파일이 남지 않아야 하며, native rename telemetry는 논리 identity를 유지해야 한다.

최종 `artifacts/bee-internal-20261015/bee-abba-01/` 비교는 모든 빌드·시험이 끝난 뒤 bounded-overlay / internal-native / internal-native / bounded-overlay를 직렬 실행했다. 4개 모두 컴파일 상수, 정규화 graph/payload 일치, 고정 입력/runtime 무변경, 독립 소스 결과, 정상 unmount와 handle/context 0 gate를 통과했다. 각 native 실행은 논리 identity를 유지하며 state와 digest cache를 각각 3회 게시했다. 앞선 실패 pilot은 정확성/진단 근거에만 사용한다.

방식별 새 view 2개의 평균이다.

| 측정 구간 | 현재 제한 overlay | 내부 native Bee |
| --- | ---: | ---: |
| Provider 준비 | 0.344초 | 0.419초 |
| Graph 생성 | 1.207초 | 0.720초 |
| 생성 출력이 없는 cold 컴파일 | 7.063초 | 11.746초 |
| 변경 없는 빌드 | 0.422초 | 0.416초 |
| 소스 1개 변경 빌드 | 1.359초 | 1.441초 |
| Assembly 상수 검사 | 0.488초 | 0.565초 |
| 입력 게시와 소스 편집 | 0.070초 | 0.120초 |
| 정상 완료 | 0.231초 | 3.455초 |
| **측정 구간 합계** | **11.184초** | **18.883초** |
| **Provider 시작 → 정상 종료 경과 시간** | **11.573초** | **20.911초** |

구간 합계는 **68.8%**, 경과 시간은 **80.7%** 늘었다. native cold 컴파일은 **66.3%** 늘었고, 변경 없는 빌드와 incremental 작업에서도 큰 이점은 나타나지 않았다. graph 평균은 40.3% 줄었지만 첫 overlay graph는 1.688초, 나중 overlay graph는 0.727초이며 native의 0.730초·0.710초와 가깝다. OS·compiler-server cache를 초기화하지 않았고 고정 순서에서 방식별 sample은 2개다. “Cold”는 해당 view에 생성 출력이 없다는 뜻이며 컴퓨터 cache가 비었다는 뜻이 아니다. graph 비율을 독립적인 인과 효과나 전체 작업 이득으로 읽지 않는다.

| 직렬 실행 | 방식 | Graph | Cold 컴파일 | 구간 합계 | 경과 시간 |
| --- | --- | ---: | ---: | ---: | ---: |
| 0 | 제한 overlay | 1.688초 | 7.822초 | 12.900초 | 13.289초 |
| 1 | 내부 native | 0.730초 | 14.389초 | 23.606초 | 25.834초 |
| 2 | 내부 native | 0.710초 | 9.103초 | 14.160초 | 15.988초 |
| 3 | 제한 overlay | 0.727초 | 6.303초 | 9.468초 | 9.857초 |

native의 두 cold 컴파일과 전체 수명주기가 모두 overlay의 두 실행보다 느리다. 이 후보의 한정 성능 gate를 거부하기에는 충분하지만 모든 Unity 작업에 대한 통계적 주장은 아니다.

아래 provider 완료 하위 구간은 위의 정상 완료에 포함되며 다시 더하면 안 된다.

| Provider 하위 구간 평균 | 제한 overlay | 내부 native |
| --- | ---: | ---: |
| Fence, kernel flush, context drain | 0.014초 | 0.731초 |
| Drain 뒤 명시적 store flush | 0.003초 | 0.743초 |
| Seal, 원본 검사, 독립 보존 | 0.049초 | 0.186초 |
| Unmount, stopped reopen/proof, 정리 | 0.026초 | 1.628초 |

완료 경과 시간에는 요청 polling, 최종 영수증 게시와 프로세스 종료도 들어간다. native 실행은 payload 178개를 유지하며 payload flush 356회를 기록한다. root kernel flush와 drain 뒤 명시적 flush가 각각 payload 집합을 순회한다. 두 native 실행의 누적 native flush 시간은 각각 2.257초와 0.655초이며, 이 counter는 완료 하위 구간과 겹친다. dirty-only flush 최적화나 절감 효과는 측정하지 않았다. 새 파일 생성에는 durable JSON control 3회 게시, 빈 payload flush, 저널 namespace metadata도 추가된다. 이 control 게시 비용을 따로 프로파일링하지 않았으므로 cold 컴파일 증가의 정량적 원인이 아니라 구조적 후속 가설로 남긴다.

| 최종 자원/프로세스 관찰 최댓값의 평균 | 제한 overlay | 내부 native |
| --- | ---: | ---: |
| 최종 일반 파일 NTFS 할당 | 0.918MiB | 2.128MiB |
| Provider 표본 최대 working set | 62.723MiB | 65.084MiB |
| Provider 표본 최대 private bytes | 29.992MiB | 36.053MiB |
| 직접 client 표본 최대 working set | 46.213MiB | 45.859MiB |

할당은 **131.8%** 증가했다. native는 실행마다 backing 파일 358개(payload 178개, association record 178개, owner/lock control 2개)와 저널 파일 3개를 사용한다. delta만 보면 평균 할당이 200KiB지만 전체 평균은 2,231,384B다. 작아진 저널만 제시하면 저장 공간 증가가 숨겨진다. 단계 sample의 할당 최댓값은 overlay가 958,464B·1,052,672B, native가 3,681,368B·3,210,328B다. 선택한 경계에서의 관찰값이며 peak가 아니다.

최종 할당은 소유 delta와 native 디렉터리를 재귀 순회하며 `GetFileInformationByHandleEx(FileStandardInfo).AllocationSize`로 측정한다. 하위 저널 object와 native control 파일도 포함한다. 공통 fixture, 도구, 독립 소스 export, 영수증, evidence 복사와 파일시스템 디렉터리 metadata는 제외한다. 중간 할당값은 단계 경계 sample이며 임시 peak가 아니다. native payload의 논리 길이도 물리 할당과 다르다.

RAM은 provider와 직접 실행한 client를 약 100ms 간격으로 표본 관찰한다. compiler 하위 프로세스, NTFS/system cache와 컴퓨터 전체 메모리는 **NotMeasured**다. 프로세스별 관찰 최댓값을 동시 프로세스 트리 peak로 읽지 않는다. 메모리 표본 비용은 client 시간에 들어가며, 단계 할당 순회와 evidence 수집은 이름 붙인 연산 합계에서 빠지고 수명주기 경과 시간에는 포함된다.

## 회귀와 남은 경계: BN-05

최종 필수 `pwsh -NoProfile -File ./scripts/build.ps1`는 locked restore와 Release 컴파일 뒤 **1,691 통과 / 0 실패 / 조건부 skip 24개(전체 1,715개)**를 **12분 18.103초**에 완료했고 exit code는 0이다. 원시 로그는 `artifacts/bee-internal-20261015/build-final.log`다. native 안전성/복구는 별도 개발 probe 근거이며 이 제품 시험 수에 자동 포함하지 않는다. 최종 cohort는 소스 커밋과 별도로 실행 파일/runtime identity를 기록한다.

이 후보는 일반 제품 provider/CLI/seal/compact/회수 수명주기에 앱 정책, native backing inventory, 소스 분류와 복구를 연결하지 않는다. 원본 Library 표현과 복원도 바꾸지 않는다. 전체 Unity Editor 작업, ILPP, Editor를 통한 Play/Test/build, 대형 프로젝트 처리량, 전체 메모리/peak 저장 공간과 순차 Editor 손익분기는 **NotRun**이다. 서비스, UAC, 전역 설정, 외부 도구나 원본 Unity 프로젝트 변경은 없다. README의 과거 전체 작업 시간을 이 컴포넌트 수치로 대체하지 않는다.
