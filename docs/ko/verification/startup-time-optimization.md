# 준비 시간: 독립 읽기와 이어 쓰기 메타데이터

[English](../../verification/startup-time-optimization.md)

**ST-01–04**는 [UC-01](unity-checkpoint-release-remeasurement.md)에서 확인한 준비 경로의 저장 작업을 줄인다. 당시 실제 Unity의 최초 준비는 848.3379초였고 A 로그의 Scan은 448.330154초, CompileScripts는 250.873981초였다. 이는 Unity 단계별 관측이며 순수 컴파일러 CPU 시간이 아니다. 이번 후보의 새 실제 Unity 준비 시간은 **NotRun**이다.

## 독립적인 불변 기준 읽기 — ST-01

`ISnapshotTree.SupportsConcurrentReads`는 수명 동안 고정하는 명시적 선택이며 기본값은 false다. Git은 decoder/proof 상태를 내부에서 동기화하고, 소유 NTFS 디렉터리는 불변 인덱스와 위치 독립적인 고정 핸들을 사용한다. 두 공급자가 이를 켜며 graft는 양쪽이 모두 지원할 때만 켠다. 실제 제품의 두 소유권 wrapper도 이를 전달한다. 따라서 Git 읽기가 막혀 있어도 Library 읽기가 진행할 수 있다. 지원을 선언하지 않은 공급자는 직렬 기준 gate를 유지한다.

동시 기준 I/O 전에 저널 gate 안에서 private bytes와 불변 좌표를 고정한다. 진입·완료 시 poison/길이/가용성 검사와 모든 close 호출이 진행 중 읽기 종료를 기다리는 조건은 유지한다. 시험은 실제 제품 wrapper와 고정 핸들의 NTFS Library, 원본/private 바이트, 보수적 직렬 fallback, 동시 변경·종료, 읽기/가용성/poison 실패를 다룬다. 독립적인 진행을 증명하며 Unity의 448초 스캔 절감률을 측정한 것은 아니다. Git의 두 세션, 보관 chunk 256KiB와 전체 blob/chunk 검증은 유지하며 보관 범위 밖 역방향 읽기는 여전히 decoder를 다시 시작할 수 있다.

## 확정된 이어 쓰기 메타데이터 재사용 — ST-02

현재 SJL4/root v4와 초기화 v1에는 **`NodeFormat=2`**가 필수다. 노드는 **SJN2**, 불변 32-extent 메타데이터 페이지는 일반 SJB1 블록 안의 **SJP1**을 사용한다. 없거나 이전 형식의 식별자는 초기화 게시나 pending 복구·회수 전에 거부하며 비어 있는 이전 root도 포함한다. 이전 SJN1 상태는 이관하지 않고 보존한다. overlay/workspace v2, OFF SJR2, 독립 candidate v3는 유지한다.

실제 이어 쓰기는 새로 완성된 페이지를 최대 하나 게시하고 미완성 뒷부분은 inline으로 유지한다. 앞선 페이지의 전체 extent/hash 배열이나 payload를 다시 쓰지 않고 참조한다. 페이지는 사용자 private payload가 아닌 metadata로 집계한다. 재연결에서 소유권, 정확한 참조, hash, 전체 범위 구성과 object inventory를 검사한다. 각 mutation은 여전히 한 번 durable flush한 뒤 게시·회수·ACK한다.

페이지로 고정한 앞부분을 변경하면 일치하지 않는 뒷부분의 페이지를 회수하고 해당 노드의 live paging을 지속적으로 끈다. 기존 capsule 회수는 영향받은 페이지 capsule의 남은 append payload를 한 번 옮길 수 있으므로 임의 덮어쓰기의 복사 0을 주장하지 않는다. 다시 paging하지 않아 반복적인 페이지/payload 축적을 막는다. 남은 앞부분은 계속 재사용하고 명시적인 stopped compact는 독립 페이지 seed를 다시 만들 수 있다. 범위 payload, 열린 context가 있는 삭제, 마지막 unlinked 회수, 후보 export, OFF/ON compact와 게시 중단 복구의 guard를 유지한다. snapshot 읽기·파일 전체 copy-up·새 읽기 cache는 추가하지 않는다.

## 같은 소유 Unity 기반 파일 재사용 — ST-03

같은 workspace를 시작할 때 기존 bridge의 소유자, 내용 hash, 허용된 하위 항목과 일반 경로를 모두 확인한다. 내장 bridge 내용이 같으면 C# 파일과 marker를 다시 쓰지 않는다. 파일/디렉터리 시각과 Unity가 만든 `.meta` 파일도 유지한다. 내용이 다른 검증된 소유 코드는 기존 갱신 경로를 사용하고 소유권/내용 변경·예상 밖 항목·reparse 경로는 거부한다. launch token과 제어 기록은 새로 만든다. 같은 workspace 재시작의 불필요한 무효화를 줄이며 최초 컴파일 제거를 뜻하지 않는다.

원본 소스 시각은 대입하지 않는다. Git-clean 입력도 줄바꿈 변환에 의해 커밋의 raw bytes와 다를 수 있고 기존 Library 영수증은 안정된 소스 metadata와 raw blob identity를 연결하지 않는다. 안전한 시각 재사용에는 별도의 durable 내용/metadata 근거 계약이 필요하다. Bee 입력/cache와 외부 도구는 변경하지 않았다.

## 한정 측정과 검증 — ST-04

[데이터셋](../../../reports/benchmarks/startup-time-optimization-20261008.json)에 소스/바이너리 hash와 네 표본을 고정했다. 동일한 고정 JS probe 실행 파일과 일반 Release Core/Runtime 어셈블리를 사용하여 로컬 NTFS에서 관련 없는 private 파일 128개를 준비한 뒤 한 파일에 4KiB를 1,024번 이어 쓴다. 준비와 마지막 바이트/다른 파일/seal/재연결 검사는 타이머 밖이다. cache를 비우지 않고 before1 → after1 → after2 → before2 순서로 실행했으며 전체 시험과 성능 측정은 겹치지 않는다. 기준에는 직전 JS 메모리 수정이 이미 포함되어 있다.

| 항목 | 변경 전 | 현재 | 관측 감소 |
| --- | ---: | ---: | ---: |
| 이어 쓰기 시간, 표본 1 / 2 | 7.9540 / 7.9697초 | 5.9865 / 6.4040초 | — |
| 이어 쓰기 시간, 평균 | 7.9619초 | 6.1953초 | 22.19% |
| 논리 put bytes | 123,240,924 | 9,872,201 | 91.99% |
| 각 append의 durable flush 수 | 1,024 | 1,024 | 유지 |

모든 표본에서 정확한 payload, 관련 없는 파일의 불변성, 현재 seal과 읽기 전용 재연결을 확인했다. 논리 put 계수는 물리 디스크 I/O가 아니며 storage 내부에서 옮긴 object는 제외한다. 이어 쓰기 회귀 시험이 각 append capsule에 새 사용자 payload가 정확히 하나만 있는지 별도로 확인한다. 현재 journal bytes는 4,953,346 → 4,894,541이다. 고정된 warm 이어 쓰기 입력의 결과이며 Unity 시작 절감률이나 임의 덮어쓰기의 자격이 아니다. metadata 기록량이 줄어도 mutation마다 필요한 고정 비용은 상당 부분 남는다.

최종 `pwsh -NoProfile -File ./scripts/build.ps1`은 locked restore·Release 빌드에서 **경고/오류 0개**, 시험 **1,315개 통과·실패 0개·조건부 skip 19개**였다. 신규 38개는 독립 읽기 10개, 소유 bridge 재사용 10개, metadata 페이지/현재 형식 거부 18개다. 기존 큰 footer 손상 검사는 paging을 의도적으로 끄는 입력으로 유지했다. 새 게시 중단 시험에서 이전 root 핸들이 자기 backup의 축소를 막는 문제를 발견해, writer/object guard를 유지한 채 해당 핸들을 닫고 복구한 뒤 현재 root를 다시 열도록 수정했다. 최초 실패 근거도 보존했다. 시험과 겹친 빌드 시도는 테스트 DLL 잠금으로 실패했으며 최종 빌드/시험은 단독 완료한 뒤 성능을 측정했다.

원시 실행/빌드 로그와 probe는 ignored `artifacts/startup-time-20261008/`에 보존한다. 관련 없는 기존 변경 27개는 그대로이며 형제 Unity 저장소도 clean이다. 영어/한국어 계약과 문서 지도를 함께 갱신했다. 관리자/UAC 작업·설치 서비스 변경·원본 Unity 편집·전체 Unity 재실행은 없었다. 새 실제 Editor/컴파일 시간, 전체 RAM/디스크 peak, 설치 서비스 호환성과 OS 전원 장애 자격은 **NotRun**이다. 이전 형식 저널은 자동 업그레이드하지 않고 보존·거부한다.
