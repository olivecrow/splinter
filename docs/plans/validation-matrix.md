# 검증 추적표

[외부 도구 결합 제거 검증](../verification/external-tool-boundaries.md)의 ET-01~04는 외부 도구 없이 성립하는 제품 경계를 소유한다. 아래 MCP·Unity CLI·고정 패치의 과거 자격은 현재 필수 설치/버전/성공 gate가 아니다. 새 Unity A/B·도구 행렬을 자동 재실행하지 않는다.

## VCS 없는 프로젝트 기준본

사용자 승인한 whole-project 원본 mount와 VSS 없는 `project-snapshot`의 NP-01~06은 [확정 범위](plain-project-basis.md)·[후보별 검증](../verification/plain-project-basis.md)을 따른다. 기존 AP/GL/PPB의 성공을 새 경로로 승계하지 않으며 새 전체 OS/대형/ARM 행렬은 열지 않는다.

## 기존 C: 회수와 MCP 응답 복구의 현재 판정

[VR/MR 직접 근거](../verification/vss-and-mcp-remediation.md)로 기존 C: Plastic A release/B discard의 소유 회수와 독립 결과·최신 DirectX 보존을 확인했다. 고정 Coplay 패치의 35초 한 번 실행·중복 방지·reload 상세 결과와 Codex 기본 HTTP MCP의 정확한 원본/패치/현재 상태도 확인해 두 문제를 한정 마감했다. 아래 이전 단계의 C: 보류·MCP 응답 실패는 당시 후보의 근거이며 현재 보류 상태가 아니다. 그 단계의 다른 NotRun/Partial을 자동으로 통과 처리하지 않는다. 이전 Editor 종료의 원인 조사는 사용자 지시로 후속 작업에서 제외했다.

## Recorded Plastic 원본 반영의 선택 단위 마감

[POA 범위](plastic-original-apply.md)와 [후보별 직접 근거](../verification/plastic-original-apply.md)는 전체/선택 로컬 게시·원본/peer 보호·회수와 Unity import/원본 복원을 한정 마감한다. 실제 게시 후보와 마지막 불완전 asset/meta 삭제 거부 추가의 자동 자격을 구분한다. 실제 서버 잠금 생성·최종 후보의 새 재게시 묶음·OS 장애·기존 PPB MCP 테스트/release 실행 중첩 미확인은 유지한다. 구형 VSS Plastic와 과거 C: 보류를 해소한 것으로 읽지 않는다.

## 병렬 안정성의 선택 단위 마감

[SP 확정 범위](stable-parallel-completion.md)와 [현재 후보 직접 근거](../verification/stable-parallel-completion.md)로 Editor 없는 CLI·파일 I/O 경계를 **scoped-complete-with-limitations** 마감했다. 이전 Unity/도구/대형/OS 자격과 C: 보류·전역 VSS unknown을 유지한다.

| ID | 대상 | 판정 |
| --- | --- | --- |
| SP-01 | 잠금 오류 재현·분류 | Passed, 수정 전 2개 실제 경로 오류 실패 보존·확인된 sharing/lock violation만 대기·다른 I/O/접근 오류는 즉시 StateReadUnavailable |
| SP-02 | 동일/다른 작업의 명령 경계 | Passed, 점유/취소/핸들 보존/재사용·A 정상 stop 대기 중 B status/같은 ensure 완료. 집중 18건·새 회귀 6건 |
| SP-03 | 실제 A/B 명령·쓰기 겹침 | Passed with limitations, A checkpoint/release가 진행 중일 때 B 조회/같은 요청·각 32개 쓰기 완료와 hash·A 결과 격리·B 후속 편집/독립 결과 |
| SP-04 | 보호·회수·회귀·종료 | Passed with limitations, 소유 두 snapshot missing/자원 0·65개 원본/944개 이전 records 파일·등록/기존 Editor 보호·906 passed/0 failed/1 optional skip·응답 20건 schema. 전역 VSS CIM 조회 실패 unknown |

## 메모리 단계 마감

[ME 확정 범위](memory-completion.md)와 [현재 후보 직접 근거](../verification/memory-completion.md)로 **현재 Windows/NTFS 알파의 메모리 개발을 scoped-complete-with-limitations 마감했다.** 다음 일반 개발은 안정적인 병렬 작업이다. lease·누적 할당/heap·process/group·시스템 공유 값을 구분하며 전체 RAM/native codec/순간 peak unknown과 이전 지원 상태를 유지한다.

| ID | 대상 | 판정 |
| --- | --- | --- |
| ME-01 | 고정 실제 입력·분리된 비용 | Passed with limitations, actual A/B hash·같은 probe/별도 child process·source 증거 보존. pool 잔류/heap와 native/전체 시스템 RAM 귀속 구분 |
| ME-02 | journal 중복 압축 buffer | Passed, 기본 max 쓰기 lease 3MiB→1MiB·raw/선택/bytes·budget/개별 flush/ACK 유지 |
| ME-03 | 부분/빈 읽기 scratch | Passed, 최대 block 7B/4KiB lease 1MiB→32KiB·빈 16KiB·실제 replay 최대 lease 2,162,688→1,130,496B·전체 stream 검증/실패 미게시/임대 반환 |
| ME-04 | 최종 현재 후보 A/B·소유 비용/회수 | Passed with limitations, T: 한 묶음·loaded PID/ticks/MVID·A release 중 B 후속 변경·독립 파일 결과/두 snapshot missing·원본/기존 records/등록/프로세스 보호. 첫 발견/읽기 timeout 제한 유지 |
| ME-05 | 필수 회귀·schema·문서/종료 | Passed, 900 passed/0 failed/1 optional skip·실제 응답 schema·UTF-8/링크/diff. 최종 계수는 ME completion 증거 |

## 물리 저장 단계 마감

[PS 확정 범위](physical-storage-completion.md)와 [직접 근거](../verification/physical-storage-completion.md)로 **현재 Windows/NTFS 알파의 물리 저장 개발을 scoped-complete-with-limitations 마감했다.** 후속 메모리는 위 별도 ME 판정을 따른다. 기존 M0/JP/JM·SF/NR의 후보/지원 상태를 승계하거나 변경하지 않는다.

| ID | 대상 | 판정 |
| --- | --- | --- |
| PS-01 | stopped SJR2의 stored/decoded·논리 live·전체 참조 block 장부 | Passed, 독립/손상/writer 10건·기존 Runtime usage와 현재 seal/계수 대조. Brotli 내용 복원·비ASCII 제외 |
| PS-02 | 정확한 task·공유/원본/evidence 파일 할당 | Passed, 실제 Windows 합계/중복 root/누락/기존 출력/hardlink unknown 3건 |
| PS-03 | 최신 후보 실제 A/B·생성/읽기/같은 요청·소유 Editor·정리·독립 결과/회수·B 지속 | Passed with limitations, 작은 T: 입력 한 묶음·두 정확한 snapshot missing·소유 자원 0·원본/기존 records/등록/프로세스 보존 |
| PS-04 | 정리/두 세대 비용·회수 뒤 잔여량 | Passed with limitations, 두 task 1,048,304 allocated B·shared runner와 이전 자료/evidence 분리. VSS 귀속·전체 물리 순간 peak unknown |
| PS-05 | 필수 자동 회귀·schema·문서 | Passed, 884 passed/0 failed/1 optional skip·Python 13 passed·응답 schema 35건·UTF-8/링크/diff |

SF-02/03 전체 Partial·SF-04~06 NotRun·대형 PV/다른 입력/ARM/OS·실제 디스크 고갈·기존 C: 회수 보류는 그대로다. 이들은 이번 공간 구현 단계의 필수 후속 목록이 아니다.

## Unity CLI A/B 인계

[고정 실행 범위](session-workflow-and-installation.md#후속-범위-unity-cli의-ab-인계)와 [UH-01~04 근거/비용/제한](../verification/unity-cli-handoff.md)을 따른다. 기존 CLI/Pipeline·작은 T: Plastic 입력의 exact-path 작업·정상 종료 공존·독립 결과/회수를 scoped-complete-with-limitations로 마감했다. 목록 발견 누락·초기 읽기 eval 타임아웃·MCP/다른 입력/Play/Test/build 미자격은 유지한다. 과거 M0/PC의 실패를 삭제하거나 이 단위를 자동 재실행하지 않는다.

## Plastic 사용 환경의 병렬 작업

범위는 [호환 계획](plastic-compatibility.md)·[C: 실행 계획](plastic-validation.md)·[추가 T: 회수 계획](plastic-reclamation-validation.md), 현재 후보의 근거는 [검증 기록](../verification/plastic-compatibility.md)을 따른다. 작은 T: A/B의 마지막 snapshot 회수까지 scoped-complete-with-limitations로 마감했다. C: 보류 상태와 기존 Git/none/AP 자격을 승계하지 않는다.

| ID | 검증 대상 | 상태 |
| --- | --- | --- |
| PC-01 | 고정 source 선택·regular/static identity·home/등록 겹침·손상/unknown 비파괴 거부 | Passed, 자동 적합성과 고정 실제 client/root/GUID·같은 요청 A/B. 실제 unknown/손상 전체 장애 자격으로 확대하지 않음 |
| PC-02 | 관리 root 읽기/열거/생성/rename 차단·재연결 고정·기존 Git pointer 회귀 | Passed, 자동 적합성·실제 생성/rename 5회 AccessDenied와 journal 불변·cm worker 등록 없음 |
| PC-03 | 실제 A/B·원본 계속 편집·같은 요청·소유 Editor·관리 정보/등록 보호 | Passed with limitations, 정확한 A/B/원본 Editor·파일 격리·기존 원본/등록 보호. PC 당시 CLI worker 발견 STATUS_NO_INSTANCES/전체 인계 미실행. 후속 exact-path 인계는 별도 UH 자격을 따르며 목록 발견 제한은 유지 |
| PC-04 | A 독립 결과/release·B 지속/dirty discard·원본/peer/공유 자원 보호 | Passed with limitations, 별도 T: A released/B discarded·독립 bytes/hash·B 41회 지속 쓰기·snapshot/소유 자원 0·기존 C: 6개 snapshot/보호 기록 보존. 선행 C: 입력의 회수 Partial·보류 상태는 유지 |
| PC-05 | 생성/읽기/관리 복제 비용·delta/결과/소유 메모리·필수 기존 회귀 | Passed with limitations, 생성 초기 payload/readCache 0·단계 비용/소유 메모리/독립 결과 기록·844개 필수 회귀 통과. T: Library 기준선 complete·최종 VSS used/allocated 0. 전체 물리 peak/VSS 귀속·선행 잠긴 C: Library 기준선 unknown |

현재 기본 CLI는 NTFS workspace 수명과 Editor start/stop을 제공한다. 자체 refresh·컴파일/포커스 처리·내부 성공 gate는 제거했고 결과 보존은 checkpoint로 통일했다. 선택적 단일 view/Library cache·probe는 [개발용 CLI](../development-cli.md)로 분리했다. 아래 기존 M0/WF/LB·시나리오 상태는 당시 후보의 증거이며 이번 후보의 native 실행 통과로 자동 승계하지 않는다. 현재 자동 회귀의 한도는 [범위 검증](../verification/workspace-product-scope.md)을 따른다.

현재 기능의 제한과 보존 근거는 [검증 안내](../verification/README.md)를 따른다. M0는 한정 마감했고 기존 시나리오의 실행 상태를 보존한다. 각 행의 후속 책임은 프로그램 구현·AI 사용 절차·필요 시 지원 검증·구현 제외로 구분한다. 미실행을 Passed로 올리거나 전체 카탈로그를 미완성 프로그램 기능/M0 완료의 분모로 사용하지 않는다.

## 첫 실사용 버전 M0

범위·수정 한도·종료 규칙은 [첫 버전 계획](implementation-plan.md)이 소유한다. 한정 M0는 [납품 판정](../verification/m0-delivery.md)의 **scoped-complete-with-limitations**로 마감했다. 아래 Passed는 명시한 작은 등록 fixture·독립 Editor와 현재 자동 계약/파일 시험의 조건이며 범용 지원이나 현재 바이너리의 모든 native 중단 통과를 뜻하지 않는다. 장기 NI/NR/SF/SW의 미완료는 유지한다.

| ID | 대표 흐름 | 상태 | 재사용 근거와 남은 확인 |
| --- | --- | --- | --- |
| M0-01 | NTFS A/B 생성·범위 저장·원본/peer 격리 | Passed | [대표 사용](../verification/representative-agent-workflow.md)의 작은 warm fixture·독립 Editor 경로 한정. 최초 payload 복제 0·원본/peer 보호·회수. 일반 파일/대형 입력 지원으로 확대하지 않음 |
| M0-02 | 최소 AI 지침·같은 작업/경로 재개·direct 미사용 | Passed | [최소 AI 실행](../verification/agent-workflow.md)의 direct/지침과 [대표 사용](../verification/representative-agent-workflow.md)의 실제 Editor·새 CLI 동일 A 재개. 설치/인계는 AI 절차, 자동 binding/임의 파일의 대화별 배타 제어는 구현 제외. 새/동시 대화 전체 증거는 별도 |
| M0-03 | 여러 차례 수정·오류 격리·정확한 Editor 검증 | Passed | [대표 사용](../verification/representative-agent-workflow.md): A CS0103 → 수정 113, B 213/214/215의 현재 검증·loaded assembly, 같은 작업 유지. focus 거부/재시도와 게시 오류 로그 제한 포함 |
| M0-04 | 실패/미커밋 작업의 명시적 폐기·중복/중단 재개 | Passed | [로컬 폐기](../verification/workspace-discard.md)의 동일 intent 재개/중복과 [대표 사용](../verification/representative-agent-workflow.md)의 B 현재 컴파일 실패·checkpoint 없음·정상 stop/실제 회수. native 강제 중단/SW-10은 별도 NotRun |
| M0-05 | 결과 보존/반영/회수·A 종료 뒤 B 지속 | Passed | [대표 사용](../verification/representative-agent-workflow.md)의 독립 결과·dirty 원본 거부/보존·clean 시험 branch 통합·A release 중 B 수정. 첫 seal 거부 뒤 같은 A 재수집 성공, 원본 Editor의 통합 후 게임 검증은 별도 |
| M0-06 | 지원 경로의 응답 유실/프로세스 중단·손상/unknown | Passed | [현재 복구 계약 판정](../verification/m0-delivery.md#현재-후보의-m0-06-근거): 현재 자동 회귀의 동일 요청 재개·실제 자식/파일 오류·unknown/손상 보존과 대표 seal 거부/동일 A 재수집. 과거 native의 변경된 조정/authority 경계는 승계하지 않음. 현재 모든 native 강제 중단·OS 장애 NotRun, NR 전체 InProgress 유지 |
| M0-07 | 소유 자원 회수·공유/reclaim-pending의 정확한 보고 | Passed | [대표 사용](../verification/representative-agent-workflow.md)의 A release/B discard: 소유 Editor/provider·mount/Git/delta·정확한 VSS 부재, 자원 0. 공유 지연 blocker는 이번 실행에 없음. 전역 VSS inventory는 unknown |
| M0-08 | 대표 입력의 생성/준비/재개/반복 편집/종료 비용 | Passed | [대표 사용](../verification/representative-agent-workflow.md)의 동일 실행 비용·제약 판단. 관측 추가 파일 할당 최대 약 335 MiB·종료 뒤 약 1.77 MiB. 분 단위 준비·큰 Editor 메모리·재시도 포함, 물리 peak/VSS 귀속 unknown |

기존 자동 회귀는 유지한다. 실제 시험은 변경된 부작용 경계·재현 결함과 최종 대표 A/B 한 묶음에 맞춘다. 기존 결과의 후보/입력 제한을 표시하면서 재사용하고 새로운 후보 전체 Passed로 자동 승계하지 않는다.

| 장기 카탈로그 | 유지할 프로그램 요구 | AI 절차·제외·선택 검증 |
| --- | --- | --- |
| FS/GT/EX/CLI | 지원 입력의 격리·범위 저장·Git/owner·JSON·재개·회수·안전 거부 | dirty/LFS/filter·attach·파일 특성/OS 조합 확대 |
| WS/CP | 정확한 작업 경로와 소유 Editor·소스 세대·현재 gate·오류 격리 | 외부 Unity 도구 사용/컴파일 분석은 AI. 실제 필요한 package/define/버전만 선택 검증 |
| AS/PL/TS/IS | 기존 도구의 일반 에셋 I/O가 지원 mount 경계를 통과하며 미격리 외부 상태는 제한으로 표시 | Splinter 자체 저작/Play/캡처/입력/Test Runner·외부 namespace API는 제품 필수 범위에서 제외 |
| UX/RC/MG/PK | 현재 명령의 지속 상태/identity·명시적 종료·독립 결과·target guard·원본/peer 보존 | 설치/인계·충돌 해결·게임 검증은 AI. 전용 installer·자동 binding·통합 검증 플랫폼은 구현 제외 |
| NR/SF/SW/FD | 지원 입력의 안전/회수·범위 저장/Library 수명·필요한 비용 관측 | OS/ARM/대형은 필요 시 검증. 모드 선택/설치/업데이트는 AI, 큐·자동 모드·일반 임대 인수는 구현 제외 |

## 축소한 후속의 책임 분류

실제 작업과 종료 기준은 [구현 계획](implementation-plan.md#남길-후속-작업과-종료-기준)이 소유한다. 이 표는 기존 ID의 상태를 변경하지 않고 후속 책임만 분류한다.

| 대상 | 분류 | 남길 범위 |
| --- | --- | --- |
| VCS 없는 원본 반영: 한정 마감 | 실제 프로그램 기능·안전 계약 | [확정 범위](plain-folder-original-apply.md)의 변경 경로 반영·원본/peer 보호·동일 intent/부분 수행 복구. AP-01~04는 자동 계약/실제 파일 시험 한정 Passed, AP-05/06은 작은 Editor 없는 제품 A/B·계측/회귀 한정 Passed. Editor 없는 작은 NTFS A/B 한 묶음으로 한정 |
| VCS 독립성과 외부 VCS 공존 | Splinter 자체의 병렬 작업·격리·효율 책임 | [VCS 없는 기본 수명주기](implementation-plan.md#마감한-선행-개발-단위-vcs-없는-기본-수명주기)는 이번 한정 단위를 마감. 일반 폴더·Git 사용 환경의 보호 경계·후속 Plastic 환경의 호환 자격을 아래에서 구분. 완료 기준은 Splinter의 병렬 작업/회수·원본/peer 보호와 저장 공간/메모리 비용이며 기존 M0의 미실행 항목 추가로 취급하지 않음 |
| 외부 MCP/Unity CLI 인계 | AI 절차·필요 시 최소 프로그램 연결/사용 검증 | [Unity CLI의 A/B 인계](session-workflow-and-installation.md#후속-범위-unity-cli의-ab-인계)는 [UH-01~04](../verification/unity-cli-handoff.md)의 작은 T: Plastic exact-path 작업·loaded 코드·A stop/release 뒤 B 지속·두 결과/회수로 한정 마감. 목록 누락·초기 eval 타임아웃 유지. MCP·기존 Git 후보/다른 입력·Play/Test/build는 NotRun. 기존 M0에서 승계하지 않음 |
| SF-02/03 | 프로그램의 저장·수명 유지/관측 병목 개선 | 기존 Library/후보/세대·정리·의존성 회수. 큐/일반 임대 인수/원본 공유는 제외, 생성 소스 병합은 AI |
| SF-04/05·NR | 필요 시 지원 검증·안전 결함 수정 | 사용할 입력/환경의 파일·Git·재개/회수 경계. 전체 대형/OS/ARM 행렬 자동 실행 없음 |
| SF-06·SW-01/02/03/06 | AI 사용 절차 | 모드 선택·설치 연결·필요성 판단·인계/재조회. 자동 모드·전용 installer·Codex binding은 구현 제외 |
| SW-04~10·MG/CP/AS/PL/TS | 작업공간 보호는 프로그램, 작업 수행/판단은 AI·외부 도구 | 현재 owner/중복·원본/peer·독립 결과·통합/폐기 guard 유지. 충돌 해결/게임 검증 판단과 범용 Unity 저작/컴파일/Test API는 추가하지 않음 |

범위에서 제외한 기능의 NotRun/Partial/실패를 Passed/Unsupported로 바꾸지 않는다. 현재 프로그램의 gate·schema·자동 시험도 이 문서 변경으로 바꾸지 않는다.

## VCS 없는 기본 수명주기

이 요구는 새 프로그램 기능/계약의 개발 단위이며 기존 Git 기반 M0·48개 카탈로그와 구분한다. 범위·순서·수정 한도는 [다시 확정한 계획](implementation-plan.md#마감한-선행-개발-단위-vcs-없는-기본-수명주기)이 소유한다. 자동 file/fake·기존 Git 회귀와 승인된 실제 일반 폴더 VSS/Unity A/B 한 묶음을 **scoped-complete-with-limitations**로 마감했다. [현재 검증](../verification/vcs-independent-workspaces.md)이 첫 후보의 생성/Editor/checkpoint, 경로 결함 수정 후보의 같은 release/discard 재개와 미실행 경계를 구분한다. [고정 native/서비스 계획](vcs-independent-validation.md)을 따랐으며 새 후보 전체 A/B 재실행·일반 지원으로 자동 승계하지 않는다.

| ID | 완료 관문 | 상태 | 필요한 근거 |
| --- | --- | --- | --- |
| VCS-01 | VCS 중립 계약·일반 폴더 발견/생성 | Passed (자동·native 한정) | `.git` 없는 등록 일반 폴더·명시 mode/기준으로 실제 생성. Git 호출 부재/손상 fallback 거부는 실제 adapter의 자동 적합성과 코드 연결로 확인. application none의 별도 native는 NotRun |
| VCS-02 | A/B 격리·원본 지속 수정·같은 작업 재개 | Passed (자동·native 한정) | 서로 다른 반환 경로/key/ID, 새 CLI의 동일 A 요청 id/digest/basis/snapshot 유지. 원본 이후 수정에도 A/B의 basis 유지 |
| VCS-03 | 기존 NTFS 효율 계약 | Passed (자동·native 한정) | 초기 payload/journal/read cache 0, A의 5바이트 범위 쓰기와 ACK flush/seal 회귀. Unity journal·명시적 basis/result payload·파일 할당을 별도 관측. 전체 물리 peak/VSS 귀속 unknown |
| VCS-04 | 실제 소유 Editor·독립 결과 | Passed (한 묶음·수정 후 회수) | 정확한 A/B 경로·PID/시작 시각·외부 CLI loaded code/성공 A·실패 B, Git 없는 checkpoint. 회수 뒤 실제 49개 payload 항목/26개 object·기준/결과 내용 검증. 후보별 단계는 검증 기록에 고정 |
| VCS-05 | release/discard·peer/공유 자원 보호 | Passed (한 묶음·동일 intent 복구) | A release의 경로 결함 수정·같은 요청 재개. A unmount/delta 제거 뒤 B 응답 지속, dirty B의 snapshot 대기와 동일 intent/중복 폐기. 최종 두 작업 자원/mount/delta/snapshot 부재, 원본/A 결과 유지 |
| VCS-06 | 기존 Git·실패/형식 회귀 | Passed (자동 한정) | 최종 locked restore/Release·기존 Git와 변경한 schema/fake 적합성, VCS 없는 원본 반영 미구현의 부작용 전 거부, 손상/변경 요청 보존 |

선행 VCS-01~06의 원본 반영 미구현 거부 근거는 해당 후보에 유지하며, 원본 반영 실행의 현재 자격은 아래 AP가 소유한다. Plastic SCM 사용 환경은 위 PC의 별도 구현/실제 자격을 따른다. 작은 별도 T: A/B의 생성·격리·독립 결과/peer 보호와 회수까지 한정 마감했으며 선행 C: 비소유 VSS 보호의 보류와 외부 CLI 전체 인계 미자격은 유지한다. 일반 폴더의 성공을 Plastic 환경의 통과로 표시하지 않는다. Git 사용 환경에서 Splinter의 기존 자격은 기존 표에 유지하고 공급자 선택을 분리한 후보의 회귀와 구별한다.

## VCS 없는 원본 반영

현재 범위·입력·제외·검증량은 [원본 반영 계획](plain-folder-original-apply.md), 계약은 [현재 기능](../plain-folder-original-apply.md), 후보별 근거는 [한정 검증](../verification/plain-folder-original-apply.md)이 소유한다. 자동 계약/실제 파일 시험과 작은 Editor 없는 AP-05 제품 A/B의 자격을 구분해 scoped-complete-with-limitations로 마감했다. 기존 VCS-01~06·Git M0의 통과를 이 기능으로 승계하지 않는다.

| ID | 완료 관문 | 상태 | 필요한 근거 |
| --- | --- | --- | --- |
| AP-01 | 명시적 원본·독립 결과·반영 계획/실행 계약 | Passed (자동 계약/실제 파일 한정) | ID/digest·basis/result·원본 identity/guard·고정 intent, 잘못된 대상/지원 외 입력의 쓰기 전 거부. Git branch/설치 의존성 없음 |
| AP-02 | 변경 경로만 조건부 반영 | Passed (자동 계약/실제 파일 한정) | 수정/생성/삭제·디렉터리/유형 경계와 실제 파일 게시·동시 원본 writer/교체 보호. 무관한 원본 변경 유지 |
| AP-03 | 충돌·손상·unknown 보존 | Passed (자동 계약/실제 파일 한정) | 전체 preflight의 충돌 시 원본 쓰기 0, 부분 반영 뒤 새 충돌의 완료/미완료 기록, 원본·결과·peer 보존과 경로 탈출 거부 |
| AP-04 | 같은 intent·중단/응답 유실 재개 | Passed (자동 계약/실제 파일 한정) | 게시 전 intent·경로별 영수증, 확인한 완료 경로의 중복 쓰기 없음, 변경 intent/target guard·unknown의 추측 덮어쓰기/성공 없음, 미해결 intent/임시 자원의 회수 완료 차단 |
| AP-05 | 실제 Editor 없는 NTFS A/B | Passed (작은 한 묶음) | 동일 A 재개·3경로 반영·A release 중 B 91회 쓰기/후속 편집·B 충돌 거부와 전후 원본 manifest 일치·독립 결과 hash·양쪽 mount/delta/snapshot/소유 자원 부재. 기존 snapshot 4개 보존. native 강제 중단·Unity 자격 승계 없음 |
| AP-06 | 추가 비용·기존 회귀·문서 | Passed (고정 계측/회귀 한정) | 8/32MiB publisher stage/관리 할당과 실제 제품 결과/journal/runner·provider 시점 메모리·일반 CLI 시간/권한 관측. locked restore/Release 826 Passed·응답 24개 schema 유효·지침 일치. 전체 물리 peak·workspace별 VSS 귀속·제품 stage 순간 peak는 unknown |

## 결과와 증거 규칙

- 상태는 `Passed`, `Failed`, `NotRun`, `Unsupported` 중 하나로 기록한다. 필수 지원 조합의 `NotRun`/`Unsupported`는 공개 통과로 집계하지 않는다.
- 각 실행은 run ID, 도구 commit, OS/볼륨/Git/Unity/패키지/Codex 버전, fixture/source hash, 시작/종료 manifest, 명령·이벤트, 결과 파일 hash를 기록한다.
- fixture 성공, 합성 대형 데이터 성공, 실제 프로젝트 성공을 구분한다. 변경된 경계와 첫 버전 후보의 대표 흐름을 검증하고 무관한 변경에 전체 과거 실환경 재실행을 요구하지 않는다.
- 기존 표의 P0~P7은 원래 책임/증거를 찾는 역사적 분류이며 현재 실행 순서가 아니다. M0는 마감했고 후속은 위 분류와 구현 계획의 선택한 단위를 따른다.

## 장기 지원·확장 시나리오 카탈로그 48개

기존 ID·원래 시나리오와 실패/재현 근거를 보존한다. P0~P7·범용 Unity 기능·설치·binding의 원래 문구는 현재 프로그램 개발 요구가 아니다. AI 사용 절차와 구현 제외 항목을 위 표로 분류하고 mount/Git/보존에 필요한 하위 경계만 선택한다. 미실행과 과거 실패는 그대로 유지한다.

| ID | 검증 대상 | 단계 | 반드시 남길 증거 |
| --- | --- | --- | --- |
| FS-01 | 원본/A/B 독립 수정·삭제·재생성·rename | P0 S1 → P2 | 시작/종료 내용 hash, 다른 사본 불변 |
| FS-02 | 대형·작은·0바이트·경계 크기 파일 | P0 S1 → P2 | 내용/길이, 초기 payload·snapshot 보존·delta·메타데이터 할당, 실제 볼륨 조건 |
| FS-03 | mmap·자식 쓰기·원자적 교체 | P2 | 쓰기 경로별 다른 사본 불변 |
| FS-04 | 링크 탈출·긴/한글/공백/대소문자 경로 | P2 | 지원 동작 또는 사전 오류, 외부 경로 무변경 |
| FS-05 | 복제 중 종료·디스크 부족 | P2 | Ready 미발행, 재조정 후 상태와 남은 자원 |
| WS-01 | GUI Editor 두 개와 잘못된 인스턴스 요청 | P0 S4 → P3 | 서로 다른 경로/PID/시작 시각/세션, 교차 명령 거부 |
| WS-02 | 사람의 원본 Editor와 작업 A/B 공존 | P3 | 원본 소스 보존, 독립 compile/import 진행 |
| CP-01 | 수정한 코드의 실제 실행 값 | P0 S2 → P3 | source/compiled revision, 새 값의 테스트/Play 관찰 |
| CP-02 | 문법 오류 삽입 후 수정 | P0 S2 → P3 | 구조화된 오류, 실행 차단, 수정 후 정상 재개 |
| CP-03 | 깨진 프로젝트의 최초 시작 | P0 S2 → P3 | 브리지 부재 진단·외부 수정·재시작 회복 |
| CP-04 | asmdef·define·패키지 변경 | P3 | 해당 상태의 compile/reload와 새 코드 검증 |
| AS-01 | 새 ScriptableObject 타입과 에셋 | P0 S2 최소 → P4 | 새 타입 사용, 저장 후 재개방 값 |
| AS-02 | 새 컴포넌트의 프리팹/씬 | P4 | 타입·참조 정상, Missing Script 없음, 실제 Play |
| AS-03 | 텍스처 임포트·설정·이동 | P4 | 기대 설정, GUID와 .meta/참조 보존 |
| AS-04 | postprocessor의 추가 생성 | P4 | 후속 import/compile 이벤트, 최종 revision 일치 |
| AS-05 | 에셋 배치 중 예외 | P4 | import 잠금 복원, 다음 명령 정상 실행 |
| PL-01 | 지정 게임 씬의 실제 Play | P0 S2 → P4 | 씬·진입 이벤트·프레임·ready 조건 각각 확인 |
| PL-02 | pause/step/resume/exit 반복 | P4 | 프레임/상태 전이, 중복 구독·장치 없음 |
| PL-03 | domain reload on/off | P4 | 설정별 연결·초기화·코드 변경 리로드 복구 |
| PL-04 | 그래픽 캡처·실제 입력 경로 | P4 | 세션 귀속 이미지의 디코딩/크기, 입력에 따른 상태 변화 |
| TS-01 | 새 EditMode/PlayMode 테스트 | P0 S2 → P3 최소 → P5 | 실제 발견 목록과 선택/실행 manifest·결과 |
| TS-02 | assertion·빈 필터·컴파일 오류 | P0 S2 → P5 | 각각 실패/NoTestsMatched/CompilationFailed, 허위 성공 없음 |
| TS-03 | 테스트 중 reload·취소·Editor 종료 | P5 | 재실행 없음, Interrupted/취소 증거, 다음 실행 정상 |
| IS-01 | 동일 회사/제품명의 외부 상태 충돌 | P6 | 저장 격리 훅 또는 해당 단계 직렬화·미격리 공개 |
| RC-01 | 명령 적용 직후 응답 유실 | P1 fake → P4/P6 실제 | 사후조건 판정, 에셋 중복/덮어쓰기 없음 |
| RC-02 | Host 재시작·lease 만료·삭제 | P1 fake → P3/P6 실제 | 작업·결과 보존, 다른 Editor 미종료 |
| MG-01 | 동일 소스/에셋의 두 결과 | P6 | 충돌, 해결 후 최신 통합 후보 revision과 검증 |
| PK-01 | 도구 overlay 제거 | P2 분리 → P6 통합 | 도구 항목만 제거, 사용자 패키지·테스트·.meta 보존 |
| UX-01 | 명시 호출·점유 시 암묵 사용·불필요 시 직접 작업 | P3 → P7 | 사용/사용/미사용 증거, direct의 새 workspace 0, Codex 별도 worktree 0 (SW-03/04) |
| UX-02 | 프로젝트용/전역용 설치·Codex 연결·권한·설정 | P3 → P7 | 요청 범위 설치·실제 지침 발견, 반복 설치 멱등·미등록 프로젝트 불변 (SW-01/02) |
| UX-03 | 대화별 binding·여러 차례 수정·중단/중복 요청 재개 | P1 → P3 | 같은 key/workspace·기준/변경분 유지, 경로/권한 검증·stale 작성자 거부 (SW-04/06) |
| UX-04 | 독립된 두 대화 A/B의 병렬 작업 | P3 → P6 | 별도 조정 AI 없이 A 오류 중 B 진전, A 먼저 완료해도 B 계속 작업 (SW-05/07) |
| UX-05 | 사용자 의도에 따른 반영·보존·폐기와 회수 | P6 | 최신 원본 검증·통합/보존/명시적 discard 구분, 오류 작업 폐기·중단 재개·타 작업 보존 (SW-07~10) |
| UX-06 | 업데이트·도구 제거·대화 종료 | P7 | 기존 설정·자격증명·미완료 결과 유지, 대화 종료의 자동 통합/삭제 없음 (SW-02/10) |
| CLI-01 | JSON/JSONL·오류·schema | P1 | 전체 stdout 파싱, 접수/완료 구분, descriptor/schema 적합성 |
| CLI-02 | 비대화형·권한 부족 | P1 → P3 | 입력/모달 대기 없음, 필요한 행동 반환, sandbox 유지 |
| CLI-03 | 이벤트 재접속·관찰 취소 | P1 → P3 | cursor 재생/중복 제거, watch 종료 후 작업 지속 |
| CLI-04 | 현재 대화의 경로·권한·세대 바인딩 | P0 S3 → P3/P6 | 실제 파일 도구/명령/Editor 경로 일치, 잘못된 작업·세대 거부, 종료와 검증 구분 |
| GT-01 | 실제 worktree + CoW | P0 S1 → P2 | Git 등록·HEAD/index, .git 미복제, 전체 파일 재전개 없음 |
| GT-02 | NTFS 원본 + 스냅샷·변경분 root | P0 S1 → P2 | Git 조회/결과/회수, 원본 HEAD/staged 보존 |
| GT-03 | staged/unstaged/신규 에셋 snapshot | P2 | 일관된 S, .meta 포함, 원본 무변경, 상속 delta 중복 없음 |
| GT-04 | LFS·CRLF·attributes·filters | P0 S1 일부 → P2 | 실제 checkout 내용과 index, 누락 LFS 차단, 반복 다운로드 없음 |
| GT-05 | 생성/회수 단계별 중단 | P2 | 저널/Git/파일 재조정, 미수집 결과·다른 worktree 보존 |
| GT-06 | 외부 생성/이동/삭제·볼륨 분리 | P2 | attach/관찰/차단 구별, offline 보존, 관리 밖 경로 무삭제 |
| GT-07 | linked worktree·하위 Unity·공유 정책 | P2 | common dir/상대 경로, branch 충돌 거부, hook/config 보존 |
| GT-08 | Editor·작업·결과의 수명과 폐기 | P3 → P6 | Editor 정지 후 작업 유지, 정상 회수 시 독립 결과 유지, 명시 폐기 시 전용 결과만 제거 |
| EX-01 | Unity/Windows/Git/Codex 없는 Core | P1 | 비Git fake VCS·fake 앱·대화 연결로 수명주기, Runner 없이 검증 |
| EX-02 | 버전/capability 불일치 | P1 → P3 | 사전 차단, 거짓 Ready/자동 호환 실행 없음 |

## 기반 구현에서 추가할 회귀 시험

원본 48개를 구체화하는 추가 시험이다. 수를 늘리기 위한 API별 모방 테스트 대신 실제 데이터 손실·중복 실행·허위 성공의 경계를 검증한다.

| ID | 검증 대상 | 단계 | 통과 조건 |
| --- | --- | --- | --- |
| FD-01 | 두 CLI의 동일 작업 동시 접수 | P1 | 공통 상태 저장소·잠금, 같은 요청의 외부 부작용 1개. Host 선택 시 시작 중복도 검증 |
| FD-02 | 같은 requestKey에 다른 입력, 회수 후 키 재사용 | P1 | 명확한 충돌/기존 결과, 보존기간 내 재실행 없음 |
| FD-03 | lease 만료 후에도 살아 있는 작성자 | P1/P3 | 소유권 확인 전 새 작성자 미배정, stale 세대 명령 거부 |
| FD-04 | 선택적 DAG의 선행 결과 입력 반영 | 확장 선택 시 | B의 입력 snapshot에 A 결과 포함, 오래된 사본 미사용 |
| FD-05 | 검증 중 외부 파일 변경과 결과 재사용 | P3/P5 | 증거 stale, 새 revision 검증 전 통합 성공 차단 |
| FD-06 | CLI/관리 프로세스 중단 동안 소유 자식 관측 | P3 | supervisor 기록·작업 binding 유지, 정확한 재접속, 중복 앱/provider 없음 |
| FD-07 | 선택적 DB migration·일관된 백업·복원 | DB 전환 시 | 진행 중 operation과 requestKey/결과 참조 보존 |
| FD-08 | cleanup 계획 후 경로/소유권 변경 | P2/P6 | digest/정규 경로/볼륨/reparse 재검사로 잘못된 삭제 거부 |
| FD-09 | 통합 검증 후 원본 HEAD 또는 dirty 상태 변경 | P6 | 적용 거부, 후보 branch·검증 증거 보존 |
| FD-10 | 손실된 callback·이전 run XML·누락 artifact | P5 | 실제 완료/새 증거를 확인할 수 없으면 성공 차단 |

## NTFS 지원 확대 관문

확정 단위와 완료 기준은 [NR-01/02 준비 단계 중단 복구](ntfs-recovery-and-support.md#확정-범위-nr-0102-준비-단계-중단-복구)가 소유한다. 실제 후보와 실행 목록은 [준비 복구 검증](../verification/preparation-recovery.md)을 따른다. NR-07은 해당 단위의 사용자 쓰기 없는 부분 준비 재조정에만 연결하고 나머지 중단 관문은 유지한다.

NR-03은 [공통 저널 58개](../verification/ntfs-recovery.md#nr-03-공통-저널-경계의-실제-프로세스-종료), [로컬 kernel 19개](../verification/ntfs-recovery.md#nr-03-로컬-winfsp-callbackmmap새-provider-연결), [제품 VSS·CLI의 client 경계](../verification/ntfs-recovery.md#nr-03-제품-vsscli의-클라이언트-경계)를 별도 범위·후보로 추적한다. 후보별 실행 자격을 서로 승계하지 않으며 NR-03 전체나 OS 장애 완료로 확대하지 않는다.

별도 로컬 kernel 단위는 [실제 WinFsp callback/mmap/새 provider 19개](../verification/ntfs-recovery.md#nr-03-로컬-winfsp-callbackmmap새-provider-연결)로 scoped-complete-with-limitations다. fake 불변 기준이며 실제 VSS server/CLI·ARM·전원/공간 장애로 확대하지 않는다. 최초 reader 종료 타임아웃과 미리 열린 Read 응답/Close 구분을 보존한다.

| ID | 검증 대상 | 상태 | 완료 조건·제한 |
| --- | --- | --- | --- |
| NR-01 | 복구 상태·identity·영속 기록·기계용 응답 | InProgress: 공통 응답·상태 보존/LV와 준비 port/schema/fake·자식 launch receipt 거부, 상태 쓰기 진단 단위 scoped-complete-with-limitations | unknown/소실/경로 불일치 구분, 손상 JSON·잠긴 기록·고아 tmp 보존. [진단 16개](../verification/state-write-diagnostics.md)는 stage·게시 상태·native 오류·원래 작업 오류와 실제 파일/diagnostic Git CLI 재개/회수, 후보 714 Passed / 0 Failed / 1 Skipped. 이전 실제 준비 상태 게시 실패의 원인은 unknown이고 전체 중단 단계의 오류/행동은 남음 |
| NR-02 | 준비 단계의 실제 중단과 중복 요청 | scoped-complete-with-limitations: 같은 최종 후보의 실제 CLI 종료 26개·동시 같은 요청 Ready·독립 결과/회수 | [정확한 경계와 최종 감사](../verification/preparation-recovery.md). 최초 payload/read cache 0, 원본/기존 refs/비소유 snapshot 3개 보존, 소유 runner 0. 게시 실패 1건의 명시적 동일 identity 복구와 fresh-key 재실행을 구분. native capture 내부·intent 뒤 실행 여부 unknown 간격·OS 장애는 이 자격에 포함하지 않음 |
| NR-03 | 저널·flush·승인 쓰기의 지속성 | InProgress: 공통 저널 58개·별도 로컬 kernel 19개·제품 VSS/CLI client 13개 각각 scoped-complete-with-limitations | 제품 후보는 같은 snapshot/delta/Git 등록·새 provider/read proof·반복 ensure 불변·독립 결과·일반 release 자원 0, 698 Passed / 0 Failed / 1 Skipped. 이전 kernel의 reader 타임아웃, 제품 준비 StateWriteIncomplete 원인 unknown·Git text fixture 실패 보존. VSS 내부 callback·같은 파일 동시 쓰기·새 open/close 동시 완료·flush 내부·replay 중 종료·OS 장애·ARM은 미실행. 이전 공통/kernel/LV 후보와 구분 |
| NR-04 | 정상 재부팅·강제 전원 종료 후 같은 작업 | NotRun | Ready/미수집 변경·checkpoint 완료·회수 도중의 전후 boot 식별, 같은 snapshot/delta/key, 새 원본과 시점 격리. 유효 기준은 실제 재개, 소실/손상은 보존/차단 |
| NR-05 | 파일 특성·ACL·경로 지원/거부 | InProgress: 속성 callback 자동 통과, ARM64 실제 WinFsp 기본 I/O·5개 API 거부·회수 통과 | fake 불변 트리의 실제 mount에서 ADS/hardlink/희소/압축/mount-point reparse 거부와 일반 NTFS 양성 대조 확인. VSS 입력·EFS·ACL 전제·다른 reparse 종류·한글/공백/긴 경로는 별도 검증 |
| NR-06 | 실제 공간 고갈·offline·snapshot 소실 | NotRun | VSS·delta/상태·Git 결과 공간을 나눈 격리 볼륨 시험, quota와 구분, unknown 보존, 늦은 쓰기·미보존 결과 보호, 다른 작업·기존 복원 지점 보존 |
| NR-07 | 부분 준비·결과·회수의 재조정 | InProgress: 마지막 VSS 해제/LV, 원격 released 복구와 NR-02의 사용자 쓰기 없는 준비 재개 후 독립 결과/최종 회수 | 준비를 Ready로 재개한 뒤 정상 회수한 범위. 부분 준비 직접 release와 commit/ref/manifest/seal 중단, unmount/Git/delta/VSS 해제 응답 유실은 남음. 영수증과 실패 임시 파일은 보존 |
| NR-08 | 최종 후보·Unity 회귀·문서 | InProgress: 이전 ARM/x64 단일 검증 2건, 새 LV Windows x64 단일 검증 3건·재연결·결과·회수 통과 | 원격 workspace 소유 자원0과 canonical Editor/MCP 세션 정상 종료 확인. 같은 후보의 단일 정상 회귀이며 ARM Windows 원본/A/B·재부팅 후 loaded-code·fingerprint·validation·dirty 통합 차단과 전체 NR 완료는 남음 |

결과 보존 결함·공통 계약과 기존 PV의 제한/마감 판정은 유지한다. 폐기·최소 AI 연결·대표 A/B·비용 판단은 한정 M0로 마감했다. NR 전체·OS/공간·ARM은 필요 시 지원 검증이며 안전 결함·변경 경계·실제 사용할 입력에 필요한 하위 단위만 선택한다. 새 원시 증거는 `artifacts/ntfs-recovery/<runId>/`에 보존하고 home은 짧은 별도 경로를 사용한다.

## SF-03 공통 수명주기 적합성

아래는 [공통 계약·지속 상태](../validation-workflow.md)의 자동 시험 추적이다. 실제 VSS·WinFsp·Unity의 제품 연결이나 공개 48개 시나리오의 추가 통과로 집계하지 않는다. `ValidationWorkflowTests`는 새 coordinator/provider와 합성 파일 영수증을 사용한다.

| ID | 대상과 증거 | 상태 |
| --- | --- | --- |
| VW-01 | 입력·응답·상태 schema, current-only 거부, CLI capability의 `contract-and-fake` 경계 | Passed, 자동/fake 범위 |
| VW-02 | 후보 불변·후속 worker 수정, project/request ID 분리, 동시 동일 요청, intent/observed/completed 중단과 provider 응답 유실 | Passed, 자동/fake 범위 |
| VW-03 | A/B/A의 소스 세대 분리·동일 세대 재개, 다른 snapshot/cache·stale owner·busy/unknown 차단 | Passed, 자동/fake 범위 |
| VW-04 | 후보 seal·generation별 검증 증거, 잘못된 증거 거부, worker 성공 자동 승계 없음 | Passed, 자동/fake 범위 |
| VW-05 | 실패 검증 출력의 독립 보존, source freeze, 의존성이 있는 후보/미보존 결과/손상 결과의 회수 거부, 응답 유실 재시도 | Passed, 자동/fake 범위 |
| VW-06 | 초기/다음 flushed 상태 재개, read-only 관측, 부분·중복·누락·구버전·역행·거짓 release 상태의 보존 거부, 후보/view 기록 누락의 의존성 오인 차단 | Passed, 자동/fake 범위 |

2026-09-28의 `development` 작업 트리에서 `pwsh -NoProfile -File ./scripts/build.ps1`의 locked restore·Release 빌드와 전체 시험을 실행했다. [최종 TRX](../../artifacts/test-results/sf03-workflow-20260928.trx)는 388 Passed, 0 Failed, 1 Skipped이며, 이 중 `ValidationWorkflowTests` 64건과 capability/schema 경계 1건이 이번 범위다. 건너뛴 1건은 현재 폐기한 backend의 당시 시험이다. [실행 manifest](../../artifacts/test-results/sf03-workflow-20260928.manifest.json)는 기준 commit, 변경 코드/schema의 hash와 결과 hash를 고정한다.

실제 provider 연결·관리 파일 분리의 VSS/Unity 실행·snapshot/cache 공동 수명은 아래 WF 추적으로 구분하며 [남은 관문](storage-priority-and-editor-modes.md#sf-03-제품-연결과-남은-관문)을 따른다.

## SF-03 관리 파일 분리 적합성

개발용 후보 게시의 [현재 계약](../validation-view.md#등록-증거가-있는-관리-파일-분리)을 검증한다. 실제 임시 Git 등록과 합성 불변 트리를 사용하며 제품 VSS/WinFsp/Unity 연결과 구분한다. 형제 Unity fixture, 실제 snapshot, 설치와 외부 프로젝트는 이 자동 시험의 자원이 아니다.

| ID | 대상과 완료 기준 | 상태 |
| --- | --- | --- |
| VG-01 | 등록 owner·경로·역참조·HEAD·포인터/lock 검증, 원본·다른 worktree 무변경 | Passed, 임시 실제 Git 범위 |
| VG-02 | 검증된 전용 포인터만 후보에서 분리, 소스 rename/삭제/희소 범위 보존, hydration 0 | Passed, 합성 불변 트리/저널 |
| VG-03 | 변조·잘못된 snapshot/owner·디렉터리·관리 경계 rename·cache 쓰기의 비파괴 거부 | Passed, 자동 시험 |
| VG-04 | 후속 worker 편집과 후보 불변, 새 source 세대에서 원래 Git 포인터 노출 없음, 손상/중단 후보 보존 | Passed, 합성 불변 트리/저널 |
| VG-05 | 현재 후보 형식과 필수 증거, 별도 개발용 입력·출력 schema/capability, 전체 회귀 | Passed, 자동 시험 |

2026-09-28, 기준 commit `6bc433b` 이후 작업 트리의 `pwsh -NoProfile -File ./scripts/build.ps1`에서 locked restore·Release 빌드와 전체 420 Passed, 0 Failed, 당시 제외 시험 1 Skipped를 확인했다. 새 projection 시험 31건과 입력/capability 시험 1건을 포함한다. [최종 TRX](../../artifacts/test-results/vg-projection-20260928.trx)와 [코드·결과 hash manifest](../../artifacts/test-results/vg-projection-20260928.manifest.json)를 보존한다. 이 시험은 실제 VSS/WinFsp/Unity 게시 증거가 아니며 현재 제품 연결은 WF로 별도 판정한다. SF-03 전체는 Partial이다.

## SF-03 후보 게시 복구 적합성

[개발용 게시 복구 계약](../validation-view.md#같은-요청의-게시-복구)의 범위는 합성 불변 트리·실제 임시 파일 I/O와 예외 주입이다. 실제 프로세스 강제 종료·재부팅·전원/물리 공간 장애 또는 제품 VSS/WinFsp/Unity 검증으로 집계하지 않는다.

| ID | 대상과 완료 기준 | 상태 |
| --- | --- | --- |
| VP-01 | intent/identity/저널/receipt flush·게시의 9개 경계, 같은 요청 재전송·동일 receipt | Passed, 예외 주입/파일 I/O |
| VP-02 | 부분 header/metadata/payload/checksum prefix 확인 후 추가만 수행, 손상 prefix 비파괴 거부 | Passed, 합성 저널 |
| VP-03 | 완전한 저널 뒤 worker 후속 편집/활성 writer와 분리, 미완성 게시의 원본 세대 변경 거부, 원본 payload 읽기 0 | Passed, 합성 불변 트리/저널 |
| VP-04 | 동시 게시 잠금, 다른 요청/경로/owner/예산/projection, 부분·중복·누락·손상·출처 불명 파일 보존 | Passed, 자동 시험 |
| VP-05 | 고정 요청 입력·intent·응답 schema/capability와 strict export/projection/view 전체 회귀 | Passed, 자동 시험 |

2026-09-28, 기준 commit `786d264` 이후 작업 트리에서 `pwsh -NoProfile -File ./scripts/build.ps1`의 locked restore·Release 빌드와 전체 483 Passed, 0 Failed, 당시 제외 시험 1 Skipped를 확인했다. `CandidatePublicationTests` 61건과 고정 요청 누락을 실행 전에 거부하는 CLI 시험 2건을 포함한다. [최종 TRX](../../artifacts/test-results/vp-publication-20260928.trx)와 [코드·결과 hash manifest](../../artifacts/test-results/vp-publication-20260928.manifest.json)를 보존한다.

SF-03 전체는 Partial이다. 실제 workflow provider 연결·공동 snapshot 수명·VSS/Unity의 게시 복구는 이 VP 시험의 증거에 포함하지 않으며 아래 WF에서 별도 판정한다.

## SF-03 제품 workflow 연결

`validation` CLI와 Windows/Git/Unity 조합의 WF-01~05를 **scoped-complete-with-limitations**로 판정한다. VW/VG/VP 또는 이전 VC의 성공을 승계하지 않았다. 범위·완료 조건은 [확정 범위](storage-priority-and-editor-modes.md#sf-03-제품-연결과-남은-관문), 실행 증거와 실패·복구 제한은 [WF 기록](../verification/product-validation-workflow.md)을 따른다.

| ID | 검증 대상 | 상태 |
| --- | --- | --- |
| WF-01 | 실제 독립 VSS worker A/B의 Git 포인터 분리·게시·동일 요청 재전송 | Passed, 작은 fixture |
| WF-02 | 같은 경로·cache의 A/B/A 정상 stop/remount/start·실제 코드/에셋·세대별 출력 | Passed, 중단 복구 후 최종 A/B/A. 새 cold 무중단 자격은 없음 |
| WF-03 | source sequence에 묶인 실제 Unity validation, 실패/변경 증거 보존 | Passed, 실제 source 변경 거부·새 검증 복구·요청별 PNG |
| WF-04 | 후보·소스 독립 Git 결과, worker 후속 편집 보존, VSS 의존성 차단과 소유 자원 회수 | Passed, 독립 ref 8개·소유 자원/VSS 0·VHD 분리 후 재검증 |
| WF-05 | 실제 CLI 중단/응답 유실의 동일 owner/request 복구와 원본/기존 ref 보존 | Passed, publish intent 뒤 실제 CLI 종료·같은 요청 복구/재전송. 전체 명령 경계·재부팅은 제외 |

WF-01~05 후보의 2026-09-28 locked restore·Release 빌드·전체 자동 시험은 511 Passed, 0 Failed, 당시 제외 시험 1 Skipped다. [TRX](../../artifacts/test-results/wf-product-20260928.trx)와 [코드·실행 파일·실제 증거 manifest](../../artifacts/storage-foundation/wf-20260928/verification-manifest.json)를 보존한다. 해당 formatter의 기록 재검증과 실제 VSS/Unity 실행의 시점 차이는 WF 기록의 제한을 따른다.

## SF-03 새 cache 실행과 비용

최종 후보에서 두 빈 cache의 A/B/A와 전체 비용·독립 결과·회수를 실행하여 WF-06~08을 **scoped-complete-with-limitations**로 판정한다. 각 세대는 렌더 준비의 정상 종료 후 재시작하여 검증하며, 추가 시작/종료와 앞선 두 실패 시도의 비용을 포함한다. [실행 근거와 제한](../verification/product-validation-cold-cost.md)을 따르며 기존 WF-01~05의 결과나 formatter 재직렬화 검사를 승계하지 않았다. 공개 48개 시나리오의 추가 통과로도 집계하지 않는다.

| ID | 검증 대상 | 상태 |
| --- | --- | --- |
| WF-06 | 같은 최종 바이너리로 독립된 빈 cache 두 차례의 실제 A/B/A, 정상 전환·코드/에셋/PNG·세대 격리와 실행 중 원시 응답 schema | Passed, 같은 최종 후보의 두 빈 cache A/B/A. 세대별 준비/검증 Editor 각 1회, schema·세대 격리·PNG 확인. t2의 Editor 시작 전 준비 요청 재시도 제한 유지 |
| WF-07 | 두 trial의 준비→보존→회수 전체 추가 파일 할당·관측 최고치·VSS 공유 관측·단계 시간·cache 재증가와 계측 한계 | Passed, 소유 파일 할당·관측 최고치·단계 시간·실패/재시작 비용. 순간 물리 peak와 공유 VSS workspace 귀속은 미확정 |
| WF-08 | 새 실행 결과의 독립 Git 보존·회수 후 내용/hash, 원본/기존 refs/비소유 snapshot 보존과 소유 자원 0/부재 | Passed, 정상 결과 14개와 앞선 결과 2개의 독립 보존·VHD 분리 후 재검증·소유 자원 0/부재 |

최종 locked restore·Release 빌드와 자동 시험은 515 Passed, 0 Failed, 당시 제외 시험 1 Skipped다. [최종 TRX](../../artifacts/storage-foundation/wf-cold-20260928/final-candidate.trx)와 [최종 후보 manifest](../../artifacts/storage-foundation/wf-cold-20260928/final-candidate-manifest.json)를 보존한다.

## Windows IPC 상태 읽기 경합

실제 Windows 파일 교체/읽기와 합성 overlay client의 IPC-01~03을 **scoped-complete-with-limitations**로 판정한다. [실행 근거와 제한](../verification/control-file-read-races.md)에 따르며 WF의 실제 cold 반복이나 공개 48개 시나리오의 추가 통과로 집계하지 않는다.

| ID | 검증 대상 | 상태 |
| --- | --- | --- |
| IPC-01 | 수정 전 Windows 동시 게시/읽기 재현과 과거 실패 비교 | Passed, 3,000회 게시에서 guard/open의 부재·공유 오류 재현. 과거 t2의 정확한 발생 지점은 미확정 |
| IPC-02 | 유한 읽기 복구와 지속 실패·취소·손상·소유권 보존 | Passed, 최대 1초 guard/open 재시도·선택적 요청 polling·추가 회귀 12개 |
| IPC-03 | 수정 후 실제 Windows 경합·합성 종료 관측/같은 요청·전체 빌드 | Passed, 3,000회 게시/22,831회 읽기의 예외·null·손상 0. 해당 후보 전체 527 Passed/0 Failed/1 Skipped. 실제 제품 재자격은 WF-09~12를 따른다 |

## 일반 권한 Unity 검증 흐름 재검증

`development @ 183118b`에 Library 변경을 적용한 고정 후보에서 권한 서비스·IPC와 실제 Unity workflow를 함께 실행했다. [원시 근거와 제한](../verification/product-validation-unelevated.md)에 따라 WF-09~12를 작은 fixture의 `scoped-complete-with-limitations`로 마감한다. 기존 WF-01~08·IPC·UA의 성공을 승계하거나 공개 48개 시나리오의 추가 통과로 집계하지 않는다.

| ID | 검증할 경계 | 현재 상태 |
| --- | --- | --- |
| WF-09 | 최종 제품/설치 서비스 identity와 실제 Unity 흐름의 일반 권한·반복 추가 UAC 0 | Passed, 일반 CLI 187회·추가 UAC 관측 0. 최초 관리자 구성과 포커스 재시도는 별도 |
| WF-10 | 작은 worker A/B·빈 cache 하나의 A/B/A, 실제 코드/에셋·세대별 검증·원시 schema | Passed, cold A/B/A·warm A/B/A/정리 후 A, raw schema 530건. 첫 draw와 heartbeat 로그 제한 유지 |
| WF-11 | 실제 prepare-worker 정상 unmount/동일 요청, 새 CLI의 같은 작업 재연결과 후속 편집 격리 | Passed, 같은 요청·같은 workspace 재연결과 게시 후 편집 격리. 일반 강제 종료/응답 유실 관문으로 확대하지 않음 |
| WF-12 | 독립 결과·원본/타 작업 보존, 의존성 순서에 따른 최종 회수·전체 할당/시간/실패 비용 | Passed, 독립 결과 15개·소유 자원 회수·원본/기존 snapshot 보존. 전체 순간 물리 peak와 공유 VSS 귀속 unknown |

회수 후 reclaim-pending이나 소유 자원 unknown은 남지 않았다. 측정의 unknown은 위 범위와 분리해 유지한다. 자세한 완료 기준과 제외 범위는 [계획](storage-priority-and-editor-modes.md#보존한-한정-범위-일반-권한-unity-검증-흐름), 관측·실패·복구 근거는 [검증 문서](../verification/product-validation-unelevated.md)가 소유한다.

## Library 저장 수명주기

[이번 단계의 다섯 완료 기준](storage-priority-and-editor-modes.md#이번-단계의-library-완료-기준)은 [실제 cold/기존 Library 실행](../verification/product-validation-unelevated.md)과 자동 적합성을 함께 근거로 `scoped-complete-with-limitations`다. 대형 프로젝트 전체 지원을 뜻하지 않는다.

| ID | 검증할 경계 | 현재 상태 |
| --- | --- | --- |
| LB-01 | 기존 Library 불변 snapshot 읽기·범위 쓰기, 초기 복제/읽기 hydration/전체 파일 copy-up 없음 | Passed, 실제 snapshot 직접 읽기·초기 private journal 0B와 범위 쓰기 자동 적합성 |
| LB-02 | A/B/A·새 CLI의 동일 cache 기준, 원본/작업 격리·기준 회수 차단 | Passed, 실제 네 세대의 고정 기준·원본 Library 변경 격리와 cache 의존 회수 거부 |
| LB-03 | 정지 cache 정리·겹침량 예산·내용 동등성·같은 요청 중단 복구 | Passed, 실제 정리/같은 요청/restart/재증가. 8개 중단 경계와 작은 예산 거부는 자동 시험 범위 |
| LB-04 | 같은 소스의 cold/기존 Library 비교·전체 추가 할당·준비/재개/정리 비용 | Passed with limitations, 같은 입력의 저장량·시간·정리 겹침과 재증가. 전체 순간 물리 peak·VSS 귀속은 unknown |
| LB-05 | artifact→cache→worker 독립 결과와 최종 회수·현재 계약 및 안내 | Passed, 실제 독립 결과·최종 소유 자원 회수와 588개 자동 시험·현재 문서 |

## 제품화와 비용 관문

아래 Partial/NotRun은 증거 상태이며 미완성 프로그램 기능의 수가 아니다. FIFO/Coordination·일반 임대 인수·원본 공유·자동 모드·전용 installer·자동 binding은 구현 제외다. source 반환 병합·설치/인계·도구 선택·게임 검증은 AI 절차다. 기존 코드 제거/명령 발견의 자격은 [제품 범위 정리 검증](../verification/workspace-product-scope.md)에 보존하며 이번 계획 축소를 새 실행/코드 제거 증거로 읽지 않는다.

- SF-02/03은 Partial, SF-04~06은 NotRun이다. 완료 기준은 [저장공간·Editor 계획](storage-priority-and-editor-modes.md)을 따른다.
- [Bee payload 후보 선정](../verification/library-block-compression-feasibility.md)과 [독립 codec](../verification/workspace-payload-codec.md)은 선행 분석/부품 단위다. 이후 [JP 제품 연결](../verification/journal-payload-compression.md)의 작은 T: A/B·동일 journal raw 대조의 NTFS 할당을 한정 마감했다. 이 하위 자격을 전체 SF-02·workspace 공간/RAM 절감으로 올리지 않는다.
- SF-03의 공통 수명주기 계약은 위 VW 자동 적합성으로 추적한다. 실제 제품 VSS·WinFsp·Unity 연결은 WF의 현재 후보 증거로 따로 판정한다.
- SW-01/02/04/05/07/08/10 NotRun·SW-03/06/09 전체 Partial은 유지한다. [최소 AI 실행](../verification/agent-workflow.md)·[로컬 폐기](../verification/workspace-discard.md)·[대표 A/B](../verification/representative-agent-workflow.md)의 하위 자격을 구분한다. 설치/인계의 AI 절차와 busy/unknown·새/동시 대화·native 중단의 미실행을 보존하며 구현 제외 기능을 완료로 표시하지 않는다. [UA](../verification/storage-privilege-service.md)의 제한 서비스 구성/반복 사용은 전용 제품 installer와 구별한다.
- PV-01/02의 사용자 선택 범위는 scoped-complete-with-limitations다. 전체 PV-02 Passed로 표시하지 않고 S4 입력 실패·소수 표본·raw 입력 동등성 unknown을 유지한다. [비교 방법](parallel-workflow-qualification.md)과 [실측](../verification/parallel-workflow.md)은 후속 비교의 입력을 고정하기 위해 보존한다.
- 현재 코드의 계약 변경은 필수 형식 생성·읽기, 지원하지 않는 형식의 비파괴 거부, 같은 버전의 중단 재시도를 함께 검증한다. 자동 시험과 실제 VSS/Unity 실행을 구별한다.

## 현재 압축 저널 제품 연결

[JP-01~06](../verification/journal-payload-compression.md)은 [확정 범위](journal-payload-compression.md)의 공통 frame/fragment·현재 단일 형식·내용/복구/정리·작은 T: 실제 A/B·동일 journal raw 대조의 NTFS 할당/메모리 표본·필수 회귀를 scoped-complete-with-limitations로 마감했다. 이전 standalone·M0/UH를 새 형식의 근거로 승계하지 않으며 SF 전체 Partial·전체 peak/VSS 귀속/native RAM unknown과 기존 C: 보류를 유지한다.

## 저널 기본값의 물리 저장 개선

[JM 확정 범위](journal-metadata-storage.md)의 직접 근거는 [JM 검증](../verification/journal-metadata-storage.md)이 소유한다. 현재 SJR2 기본값 표현의 개선이며 새 형식/자동 이관·새 native A/B를 포함하지 않는다.

| ID | 경계 | 상태 |
| --- | --- | --- |
| JM-01 | literal 기본값 명시/생략·비기본값·raw/encoded·개별 flush | Passed, 자동/fake |
| JM-02 | 명시적 정리/후보의 더 작은 metadata·내용/sequence·예산 | Passed, focused 자동/실제 저널 오프라인 |
| JM-03 | 고정 A/B 전후 NTFS 할당·이전 세대 회수·기준 읽기/입력 변경 0 | Passed with limitations, 새 Unity/VSS 수명주기 NotRun |
| JM-04 | 필수 전체 build·현재 계약/문서·UTF-8/diff | Passed, 884 passed·0 failed·1 skip. UTF-8/로컬 링크/diff 확인 |

JM-01~04는 scoped-complete-with-limitations로 마감했다. 실제 저널의 오프라인 파일 할당 감소를 전체 workspace 공간·메모리·새 native 지원으로 확대하지 않는다.
