# Splinter 첫 실사용 버전 계획

**등록된 작은 Windows x64 NTFS/Git/Unity 환경의 첫 한정 실사용 버전 M0를 마감했다.** [납품 판정](../verification/m0-delivery.md)은 scoped-complete-with-limitations이며 [AI 시작 안내](../agent-start.md)에서 기존 개발 CLI·소스 스킬의 전제와 다음 행동을 찾는다. 대표 A/B 사용·비용과 현재 복구 계약을 확인했고 과거 native의 재사용 한계를 고정했다. 남은 NR/SF/SW 전체를 M0의 진척 분모로 다시 넣지 않는다. 후속 범위는 실제 사용할 프로젝트/입력 또는 설치 필요가 정해졌을 때 그 요구에 맞춰 선택한다.

## 목적과 완료 경계

Splinter는 **Unity처럼 프로젝트·Editor 점유로 독립 작업이 제약되는 환경에서 AI 에이전트가 병렬 작업공간을 효율적으로 만들고, 이어가며, 원할 때 폐기하는 도구**다. 작업 내용의 의미 판단·코드 작성·게임 기능 검증은 해당 대화의 AI와 기존 개발 도구가 담당한다.

첫 실사용 버전은 아래 M0의 한정된 환경과 사용 흐름을 완성한다. 범용 Unity 저작·테스트 플랫폼이나 모든 NTFS 장애 조건의 자격을 완료 기준에 넣지 않는다. 제품 요구는 [plan.html](../../plan.html), 현재 구현은 [foundation](../foundation.md), 실행 상태는 [검증 추적표](validation-matrix.md)가 소유한다.

```text
필요성 판단 → 독립 작업 생성/같은 작업 재개 → 편집·필요한 Editor 사용
                                            → 계속 보존
                                            → 결과 보존/명시적 반영 후 회수
                                            → 명시적 폐기
```

대화·Editor 종료, timeout·임대 만료·디스크 압박은 반영이나 폐기 의도가 아니다. A를 끝내도 B의 기준·변경분·Editor와 원본은 유지한다. 별도 조정 AI, 작업 그래프 제출과 하위 에이전트 Runner는 제공하지 않는다.

## 첫 버전의 지원 조건

| 항목 | 고정할 범위 |
| --- | --- |
| 환경 | Windows x64, 로컬 NTFS, Git, 등록된 project/home과 기존 제한 VSS 서비스. Unity fixture의 정확한 패치 `6000.3.22f1`부터 실제 실행을 확인 |
| 입력 | 검증된 clean Git 입력과 일반 파일·디렉터리. unsupported 파일 특성·LFS/filter·dirty 입력은 현재 거부 정책 유지. 주 사용 프로젝트가 이를 필요로 하면 별도 최소 지원 범위를 먼저 정함 |
| 저장 | 원본 NTFS 위치/파일시스템 유지, 원본 계속 편집 가능, 최초 전체 복제·읽기 hydration·파일 전체 copy-up fallback 금지, 변경 범위 저장 |
| 실행 | 독립 소스 편집과 검증을 구분. 필요한 Editor만 실행하며 기존 독립 Editor 또는 명시적 validation view를 선택. 자동 모드 선택·큐·원본 Editor 공유는 없음 |
| AI 진입 | 개발 배포물/기존 CLI와 최소 사용 지침, 안정된 task key·workspace ID·절대 경로·동일 home. 전역 마지막 작업 추측이나 특정 Codex 세션 API를 필수로 하지 않음 |
| 보장 | 지원 경로의 원본/타 작업 격리, 승인된 쓰기 보존, 정확한 소유 자원 처리, 같은 버전의 정상 재개와 요청 재전송. OS 장애 전체 보장·보안 샌드박스는 선언하지 않음 |

현재 구현을 지원 완료로 승격하는 표가 아니다. 새 후보의 실행 자격과 지원 한계를 함께 기록한다. 선택적 ReFS와 diagnostic Git checkout은 NTFS 정상 경로의 fallback이 아니며 첫 버전의 지원 확대 대상에서 제외한다.

## 제품 작업 순서

| 순서 | 산출물 | 완료 기준과 수정 한도 |
| --- | --- | --- |
| 1 | 명시적 `discard` 계약·구현: 연결/로컬 단위 마감 | dirty 작업·같은 intent 재개/중복·peer 보호·실제 회수 확인. Unity 컴파일 실패/Editor stop은 3의 대표 흐름에 포함하며 이 때문에 별도 장애 행렬을 열지 않음 |
| 2 | 최소 AI 작업 연결·사용 지침: Editor 없는 단위 마감 | [스킬 원본](../../skills/splinter/SKILL.md)과 실제 explicit/direct·반환 경로 편집·새 CLI의 같은 key/id/home 재개·폐기 확인. Editor/대표 대화 흐름은 3에서 확인하며 프로젝트/전역 설치·자동 binding은 후속 |
| 3 | A/B의 대표 사용 흐름: 한정 마감 | [대표 사용](../verification/representative-agent-workflow.md)의 오류/수정·같은 작업 재개·dirty guard·보존/통합/release와 실패 작업 discard. 첫 수집/포커스 거부와 같은 작업 재시도 제한 포함 |
| 4 | 공간·시간의 채택 판단: 한정 마감 | 같은 작은 warm 입력의 관측 파일 할당·준비/편집/종료 비용으로 지속 독립 작업에 한정 채택. VSS 귀속·물리 peak·대형 효율 unknown, 새 비교/반복 관문 없음 |
| 5 | 한정 버전 납품: 마감 | [시작 안내](../agent-start.md)·[현재 복구 근거/납품 판정](../verification/m0-delivery.md). 기존 개발 CLI·등록 서비스 전제의 한정 버전이며 일반 설치·대형/범용 공개 지원으로 확대하지 않음 |

discard의 상세 의도·공유 자원·중단 조건은 [대화별 작업 계획](session-workflow-and-installation.md#완료보존폐기)이 소유한다. 첫 구현은 정확한 worker workspace를 대상으로 한다. 사용 중인 candidate/source/cache 의존성은 명시적으로 차단하고 해당 의존성 정리 행동을 반환한다. 원본 Editor·타 작업·공유 view를 자동 종료하거나 연쇄 삭제하지 않는다. snapshot 회수가 지연되면 논리적 작업 종료와 물리 회수 대기를 구분하며 숨은 백업이나 회수 완료로 표시하지 않는다.

### 한정 납품 경계와 후속 선택

M0는 기존 개발 checkout의 CLI·소스 스킬과 등록된 서비스/driver 환경을 제공물로 삼는다. [대표 사용/비용](../verification/representative-agent-workflow.md)은 실행·실패·같은 작업 재시도를, [납품 판정](../verification/m0-delivery.md)은 현재 회귀·과거 native의 재사용 한계와 제외할 장애 조건을 소유한다. 일반 installer·자동 binding·외부 공개/배포를 완료한 것으로 읽지 않는다.

후속 범위는 실제 사용자 요구로 고른다. 새 프로젝트/입력이면 해당 입력·등록·mount 경계, 설치가 필요하면 프로젝트/사용자 범위 발견·배포물 연결, 관측 비용이 실용성을 막으면 그 입력의 병목을 대상으로 한다. 각 범위의 질문·지원 조건·종료 기준을 먼저 고정한다. 필요가 정해지기 전에 새 harness·전체 장애 행렬·VIVOBOOK·대형 PV·p95 반복·일반 설치/자동 binding을 자동 개시하지 않는다. 지원 경로의 승인 쓰기 손실·원본/peer 훼손이 새로 확인되면 그 결함을 먼저 수정한다.

## 유지할 구현과 제거 경계

| 판단 | 현재 경계 |
| --- | --- |
| 유지 | Core ports/schema, atomic 상태·잠금, Git linked worktree, VSS/WinFsp 범위 저널·승인 쓰기 flush·결과 seal, process supervisor·소유권·회수 guard |
| 유지 | Unity start/refresh/stop과 소스 세대 확인. 기존 Library snapshot/cache와 명시적 validation은 저장 비용과 단일 Editor 사용에 직접 연결됨 |
| 유지하되 추가 확장 중지 | ReFS alpha, 개발 spike/probe·복구 harness. 현재 회귀와 재현 근거로 사용하며 제품 정상 경로로 확대하거나 새 시험 행렬을 계속 추가하지 않음 |
| 제품에 두지 않음 | DAG 검사/제출, 에이전트 실행·스케줄러, daemon/DB 설계, 범용 Unity 저작/Play/Test Runner API. 필요가 생기면 새로운 목적·범위를 검토 |

모듈 명칭에 `runner`가 있더라도 OS 자식 프로세스를 감독하는 내부 보조 프로그램은 작업 분할용 AI Runner와 다르다. 실제 제품 경로와 실패 재현에 사용되는 기능을 이름만 보고 삭제하지 않는다. 상태·기록 형식을 임의로 바꾸거나 구버전 migration/fallback을 추가하지 않는다. 원시 evidence, 독립 결과 refs, snapshot과 다른 작업은 정리 대상이 아니다.

## 첫 버전 검증 M0

실행 상태는 [M0 추적표](validation-matrix.md#첫-실사용-버전-m0) 한 곳에 둔다. 기존 NI/LV/WF/LB/NR의 하위 근거를 참조하되 새 사용 흐름 전체의 통과로 자동 승계하지 않는다.

| ID | 첫 버전의 완료 조건 |
| --- | --- |
| M0-01 | 정확한 project/home에서 A/B 생성, 최초 전체 payload 복제와 hydration 없이 작은 쓰기의 범위 저장. 원본 위치·내용과 peer 격리 |
| M0-02 | 실제 AI가 올바른 경로/Editor를 사용하며 CLI/대화 재개에 같은 task key·기준·변경분 유지. 불필요한 상황에는 새 작업 0 |
| M0-03 | 오류가 난 A를 수정하거나 보존하는 동안 B 진행. 정확한 Unity 코드 변경/컴파일 증거와 한 실행 경로의 재개 |
| M0-04 | 검증 성공·checkpoint 없는 작업의 명시적 폐기, 중복 폐기·중단 후 같은 intent 재개. 원본/타 작업/공유 의존성 보호 |
| M0-05 | 명시적 결과 보존·반영·회수 구분. A 종료 뒤 B 지속, dirty/변경된 원본에는 적용 거부와 결과 보존 |
| M0-06 | 지원 경로의 요청/응답 유실·소유 프로세스 중단·손상/unknown에서 허위 성공·중복 부작용·잘못된 삭제 없음. 기존 내구성 계약 유지 |
| M0-07 | 동일 경로에서 소유 Editor/provider/mount/Git/delta와 마지막 snapshot의 회수 확인. shared/reclaim-pending은 별도 보고, unknown은 0이 아님 |
| M0-08 | 고정 입력의 생성·전체 편집 준비·재개·반복 변경·종료 시간과 추가 저장 비용으로 채택/제한 판단. 관측하지 못한 물리 peak·공유 VSS 귀속은 unknown |

## 검증량과 종료 규칙

- `pwsh ./scripts/build.ps1`의 locked restore·Release·기존 자동 시험은 필수다. 폐기/삭제·승인 쓰기·원본/peer 보호 시험을 줄여 진도를 만들지 않는다.
- 계약 변경에는 schema·fake·적합성을 함께 수정한다. 실제 시험은 **바뀐 부작용 경계 또는 재현 결함**에 맞춰 선택하고 첫 버전 후보의 대표 A/B 흐름 한 묶음으로 끝낸다. 문서/명령 발견 변경 때문에 과거 Unity/VSS 시험 전부를 재실행하지 않는다.
- 실패는 보존하고 동일 원인의 한정된 재현·수정·회귀를 수행한다. 성공하면 다음 M0로 이동한다. 새 harness·전체 경계 조합·반복 수 확대에는 답할 질문과 종료 조건을 먼저 적는다.
- 비용 관측은 고정 입력의 최초·기존 작업 재개와 반복 편집을 구분한다. 측정 배수·절감률·시간 SLA를 미리 약속하지 않는다. 기존 C/S/Q 대형 PV는 자동 재개하지 않으며 p95/20회 반복은 첫 버전 관문이 아니다.
- 손실된 승인 쓰기·원본 수정·타 작업 종료/삭제가 지원 경로에서 재현되면 해당 사용 경로를 차단하고 먼저 수정한다. 과거 실패는 원인이 밝혀지기 전 unknown을 유지한다.

## 별도 지원 확대

[NR](ntfs-recovery-and-support.md)의 재부팅·전원·물리 공간 고갈·ARM A/B·일반 파일 조합, [SF](storage-priority-and-editor-modes.md)의 대형 용량·큐/자동 모드와 [SW](session-workflow-and-installation.md)의 프로젝트/전역 설치·자동 binding·업데이트/제거는 후속이다. `NotRun`/`Partial`을 Passed로 바꾸지 않으며 미실행의 지원 보장을 홍보하지 않는다. 일반 파일과 에셋이 mount 경계를 정상 통과해야 한다는 요구는 유지하지만 에셋 저작 API와 Play/Test Runner 구현 자체는 제품의 필수 범위에서 제외한다.

이전 P0~P7·F 단계와 48개 목록은 첫 버전의 진척 분모가 아니다. 장기 시나리오 ID·실패·재현 조건은 [검증 추적표](validation-matrix.md)에 보존하고 M0의 한정된 완료와 구별한다. 다른 OS/VCS/앱 확장을 위해 Core API를 미리 늘리지 않으며 현재 공급자 중립 ports와 fake 경계를 유지한다.
