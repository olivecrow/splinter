# Splinter 첫 실사용 버전 계획

**등록된 작은 Windows x64 NTFS/Git/Unity 환경의 첫 한정 실사용 버전 M0를 마감했다.** [납품 판정](../verification/m0-delivery.md)은 scoped-complete-with-limitations이며 [AI 시작 안내](../agent-start.md)에서 기존 개발 CLI·소스 스킬의 전제와 다음 행동을 찾는다. 후속은 작업공간 관리에 필요한 프로그램 구현, AI 사용 절차, 필요 시 지원 검증으로 구분한다. NR/SF/SW 전체를 미완성 프로그램 기능이나 M0의 진척 분모로 다시 넣지 않는다.

## 목적과 완료 경계

Splinter는 **Unity처럼 프로젝트·Editor 점유로 독립 작업이 제약되는 환경에서 AI 에이전트가 병렬 작업공간을 효율적으로 만들고, 이어가며, 원할 때 폐기하는 도구**다. 작업 내용의 의미 판단·코드 작성·게임 기능 검증은 해당 대화의 AI와 기존 개발 도구가 담당한다.

**외부 VCS가 있건 없건 Splinter는 병렬 작업을 제공하고, Git·Unity VCS/Plastic SCM 등을 사용할 때 저장 공간·메모리·병렬 작업에 문제가 생기지 않도록 처리해야 한다.** 병렬 격리·기본 수명주기는 Splinter의 기능이며 VCS별 처리는 이를 안전하고 효율적으로 유지하는 호환 책임이다. 마감한 M0는 Git 전제다. 일반 폴더 기본 수명주기와 기존 Git 사용 환경의 보호 경계를 연결하고 작은 실제 A/B 한 묶음을 마감했다. 일반 폴더의 작은 Editor 없는 원본 반영도 한정 마감했다. Plastic 호환 계약과 별도 작은 T: A/B의 마지막 snapshot 회수까지 한정 마감했으며 [PC 검증](../verification/plastic-compatibility.md)의 선행 C: 회수 보류·외부 도구 발견 실패를 유지한다.

첫 실사용 버전은 아래 M0의 한정된 환경과 사용 흐름을 완성한다. 범용 Unity 저작·테스트 플랫폼이나 모든 NTFS 장애 조건의 자격을 완료 기준에 넣지 않는다. 제품 요구는 [plan.html](../../plan.html), 현재 구현은 [foundation](../foundation.md), 실행 상태는 [검증 추적표](validation-matrix.md)가 소유한다.

```text
필요성 판단 → 독립 작업 생성/같은 작업 재개 → 편집·필요한 Editor 사용
                                            → 계속 보존
                                            → 결과 보존/명시적 반영 후 회수
                                            → 명시적 폐기
```

대화·Editor 종료, timeout·임대 만료·디스크 압박은 반영이나 폐기 의도가 아니다. A를 끝내도 B의 기준·변경분·Editor와 원본은 유지한다. 별도 조정 AI, 작업 그래프 제출과 하위 에이전트 Runner는 제공하지 않는다.

## 프로그램과 에이전트의 책임

Splinter는 작업공간의 생성·유지·재개·독립 결과·명시적 반영/회수/폐기와 소유 Editor의 수명을 담당한다. AI가 현재 CLI·파일 도구·Git·MCP·Unity CLI로 수행할 수 있는 정책과 작업 절차는 프로그램 기능으로 추가하지 않는다.

| 영역 | Splinter에 남길 책임 | AI와 기존 도구의 책임 |
| --- | --- | --- |
| 작업 연결 | 안정된 key/id/home·기준·반환 경로, 같은 요청 재개·중복/잘못된 대상 거부 | 필요성 판단, 인계 기록·재조회, 한 경로의 작성자 조정. Codex 자동 binding/fork/전역 마지막 작업 추측은 구현 제외 |
| Editor | 정확한 프로젝트의 실행·관찰·정지, 소유권·자원 회수, 외부 도구와의 공존 | MCP·Unity CLI의 대상 선택, 편집·refresh·컴파일 분석·Play·테스트·빌드·캡처. 이 명령의 복제나 범용 중계 API는 구현 제외 |
| 결과와 통합 | 안정된 seal·독립 결과·최신 target/dirty guard·통합 직렬화·원본/peer 보호 | 검증할 내용·합격 판단, Git 충돌 해결·통합 전략, Unity 생성 소스의 검토/반환 병합, 통합 후 게임 검증 |
| 저장과 실행 방식 | 범위 저장·Library/cache 수명·명시적 정리·비용/상태 관측 | 독립 Editor 또는 기존 validation 경로 선택, 작업 순서와 동시 Editor 수 판단. 자동 모드·큐/스케줄러·원본 Editor 공유·일반 임대 인수는 구현 제외 |
| 외부 VCS 공존 | 관리 정보·등록·잠금·공유 기준의 격리, 원본/peer 보호, 추가 복제·인덱스/cache·결과 저장·메모리 비용의 제한과 관측 | 기존 VCS 도구로 수행하는 일반 이력 관리·check-in·충돌 해결 정책. 호환 완료는 Splinter의 실제 병렬 작업/회수와 효율로 판단 |
| 설치와 발견 | 실행물·현재 계약·진단, 제한 서비스/driver의 명시적 구성 도구 | 요청 범위의 배포물 확보·빌드/의존성 확인·스킬 연결·업데이트/제거. 전용 프로젝트/전역 installer와 Codex 설정 관리 framework는 구현 제외 |

AI의 판단으로 대체할 수 없는 승인 쓰기 지속성·소유권·원본/peer/공유 자원 보호는 프로그램의 책임이다. CLI 잠금이 임의 파일 쓰기까지 배타 제어한다고 주장하지 않는다. 소유권·seal·독립 결과·회수·target guard와 실패 기록을 유지한다. 프로젝트의 컴파일·테스트 합격 판단은 AI와 외부 도구로 넘긴다.

## 남길 후속 작업과 종료 기준

현재 Windows/NTFS 알파의 **물리 저장 개발 단계는 [PS-01~05](physical-storage-completion.md), 메모리 단계는 [ME-01~05](memory-completion.md)로 한정 마감했다.** 다음 일반 개발 우선순위는 안정적인 병렬 작업이다. SF/NR의 별도 지원 확대와 공유 VSS/순간 peak·전체 RAM 귀속 unknown을 미완성 공간/메모리 기능 수로 읽거나 자동 재실행하지 않는다.

| 구분 | 남길 작업 | 종료 기준과 한도 |
| --- | --- | --- |
| 일반 폴더 원본 반영: 한정 마감 | [VCS 없는 안전한 원본 반영](plain-folder-original-apply.md)의 명시적 대상·3자 내용 비교·조건부 게시·동일 intent/부분 수행 복구 | [AP-01~06](validation-matrix.md#vcs-없는-원본-반영)과 Editor 없는 작은 NTFS A/B 한 묶음. 원본/peer·독립 결과·효율과 기존 Git 회귀 유지. 자동 파일/중단 주입 시험과 AP-05 제품 A/B 한정 자격을 구분. [검증 판정](../verification/plain-folder-original-apply.md)은 scoped-complete-with-limitations |
| VCS 독립성: 이번 단위 마감 | [VCS 없는 기본 수명주기](#마감한-선행-개발-단위-vcs-없는-기본-수명주기)의 공통 계약·일반 폴더 경로·기존 Git 사용 환경의 보호 경계와 회귀 | [한정 A/B 판정](../verification/vcs-independent-workspaces.md)의 scoped-complete-with-limitations. 원본/peer·범위 저장·회수 guard 유지. 원본 반영 자격은 [별도 AP](../verification/plain-folder-original-apply.md), Plastic 호환 자격은 별도 PC |
| Unity CLI 인계: 한정 마감 | [Unity CLI의 A/B 인계](session-workflow-and-installation.md#후속-범위-unity-cli의-ab-인계)의 기존 CLI/Pipeline·작은 T: Plastic 입력에서 정확한 대상 선택·loaded 편집·정상 종료 공존·독립 결과/회수 | [UH 검증](../verification/unity-cli-handoff.md)의 scoped-complete-with-limitations. 프로그램 변경 없이 반환 경로의 command로 연결. 목록 누락/첫 eval 타임아웃·MCP/다른 입력/Play/Test/build 미자격 유지. 기존 Git 후보의 별도 전체 인계는 실행하지 않음 |
| 결과 보존과 작업 검증의 경계: 구현 완료 | 자체 refresh/컴파일·포커스 처리와 내부 컴파일 gate·중복 preserve-worker를 제거 | 외부 검증 뒤 기본 checkpoint 사용. 컴파일 성공 주장 없이 안정된 seal·독립 결과·target guard 유지. 현재 자동 회귀 범위는 [범위 검증](../verification/workspace-product-scope.md), 새 native 인계 자격은 도구별 선택 |
| 저장·메모리: 압축 연결 한정 마감 | [UH 비용/Bee 후보 선정](../verification/library-block-compression-feasibility.md)과 [독립 codec](../verification/workspace-payload-codec.md) 뒤 [압축 저널 연결](journal-payload-compression.md)의 frame/fake·범위/재생·예산/결과를 연결 | SJR2/overlay v2/candidate v3와 [JP 검증](../verification/journal-payload-compression.md)의 동일 입력 NTFS 파일 할당·작은 T: A/B·독립 결과/회수·필수 회귀를 scoped-complete-with-limitations로 마감. 전체 workspace 공간/RAM·대형/OS 자격은 미측정이며 새 backend·flush/큐·전체 비교를 자동 선행하지 않음 |
| AI 사용 절차 | 발견·생성/재개·외부 도구 선택·보존/반영/폐기의 스킬/예시 보완 | 에이전트가 기존 schema와 반환 경로·동일 식별자로 수행 가능. 문서로 가능한 동작에 새 프로그램 기능을 요구하지 않음 |
| 물리 저장: 기본값 메타데이터 한정 마감 | [JM 확정 범위](journal-metadata-storage.md)의 현재 SJR2 기본값 생략·명시적 정리/후보의 더 작은 표현 | [JM 직접 근거](../verification/journal-metadata-storage.md)의 고정 실제 저널 전후 파일 할당·내용/sequence·추가 관리 할당·필수 회귀로 scoped-complete-with-limitations 마감. 기존 정리의 대체 payload 제거와 구분하며 새 Unity/VSS·전체 workspace/RAM 자격은 주장하지 않음 |
| 물리 저장 단계: 한정 마감 | [PS 확정 범위](physical-storage-completion.md)의 현재 SJR2 장부·정확한 task 파일 할당·최종 제품 A/B·정리/독립 결과/회수 | [PS 직접 근거](../verification/physical-storage-completion.md)의 13개 계측 시험·884개 필수 회귀·최신 JM 후보의 실제 A/B와 두 소유 snapshot 회수·설명 가능한 잔여 비용. 일반 후속 공간 목록은 닫고 메모리로 이동. 대형/OS/ARM·VSS 귀속/전체 순간 peak와 C: 보류는 별도 유지 |
| 메모리 단계: 한정 마감 | [ME 확정 범위](memory-completion.md)의 압축 쓰기 중복 buffer·부분/빈 read scratch·같은 실제 입력 의미·소유 provider/Editor/시스템 비용·최종 A/B 회수 | [ME 직접 근거](../verification/memory-completion.md)의 lease 감소·바이트/실패 미게시·900개 필수 회귀·현재 후보의 독립 결과/두 snapshot 회수. 전체 RAM·native codec/순간 peak unknown과 별도 지원 상태를 유지하고 다음 일반 개발은 안정적인 병렬 작업 |
| 필요 시 지원 검증 | 사용할 입력/환경의 mount·Git·중단/회수 경계와 재현된 안전 결함만 선택 | 지원 조건·후보·질문·종료 기준을 먼저 고정. OS 장애 전체·ARM·LFS/filter·모든 파일/패키지·48개 카탈로그는 일괄 필수 작업이 아님 |

Unity CLI exact-path 인계는 위 작은 입력의 한정 자격을 따르며 MCP·다른 입력/버전의 정상 연결은 미자격이다. 현재 worker는 Coplay 서버를 시작하지 않는 전용 브리지 경로이며 원본 서버 종료 방어가 있다. checkpoint/integrate는 컴파일·테스트 성공 판정을 하지 않는다. 연결 조건과 유지하는 목록/타임아웃 제약은 [대화별 사용 계획](session-workflow-and-installation.md#외부-도구-인계의-한정-범위)이 소유한다. 현재 명령과 기본/개발 경로는 [현재 기반](../foundation.md)과 [개발 CLI](../development-cli.md)를 따른다. 과거 M0/WF/LB의 native 실행 자격은 이번 변경 후보로 자동 승계하지 않는다.

## 한정 마감한 원본 반영

2026-10-02에 확정한 **VCS 없는 일반 폴더의 안전한 원본 반영**을 scoped-complete-with-limitations로 마감했다. 정확한 독립 결과의 변경 경로만 원본에 반영하며 원본의 이후 수정·B의 편집·보존 결과를 유지하는 제품 경계를 작은 Editor 없는 A/B 한 묶음에서 확인했다. 포함/제외·조건부 게시와 중단 복구·비용·종료 기준은 [확정 범위](plain-folder-original-apply.md)가 소유한다.

첫 실제 자격은 `source=none`·`application=none`의 작은 Windows x64/NTFS A/B 한 묶음이다. [AP 추적표](validation-matrix.md#vcs-없는-원본-반영)의 AP-01~06과 [실행 근거](../verification/plain-folder-original-apply.md)를 한정 마감했다. Plastic 호환의 자격은 별도 PC를 따르며 Unity 전체 인계/원본 Editor 반영 자격은 후속이다. 이 AP 판정을 승계하지 않는다.

## 마감한 선행 개발 단위: VCS 없는 기본 수명주기

2026-10-02에 추가한 사용자 요구로 앞선 Unity CLI 인계 단위의 우선순위를 조정했다. **일반 폴더의 생성·같은 작업 재개·독립 편집/Editor·결과 보존·회수·폐기를 Git 없이 완성하고, Git을 사용할 때도 Splinter의 격리·효율·원본/peer 보호를 유지하는 것**이 이번 확정 범위다. 공통 계약·파일 소스 공급자·CLI/Windows 연결과 승인된 서비스 업데이트·실제 A/B를 실행해 한정 범위로 마감했다. 현재 상태 v2와 `--source git|none`·`--application unity|none`의 계약은 [기능 문서](../vcs-independent-workspaces.md), 후보별 자동 회귀·실제 실행·수정 후 동일 요청 재개와 제한은 [검증 기록](../verification/vcs-independent-workspaces.md)을 따른다. 실행 상태와 관문은 [VCS 추적표](validation-matrix.md#vcs-없는-기본-수명주기)가 소유한다.

### 현재 의존성과 목표 책임

범위 재확정 당시 CLI는 Git provider와 Git discard provider를 항상 조합했고 Core와 결과 보존도 commit/revision 중심이었다. 현재 구현은 `SourceKind`·`ApplicationKind`와 opaque 기준/결과 identity, 공급자 checkpoint·독립 보존·폐기를 구분한다. 공통 저장소 계층이 병렬 격리를 담당하고 VCS별 공급자는 관리 정보·기준·결과·회수의 호환 경계를 담당한다. 기존 Git linked worktree와 commit/ref 처리는 현재 경로의 구현 선택이며 로컬 `.git`·HEAD/base·clean·branch/ref/object guard를 유지한다. 새 일반 폴더 경로의 실제 자격은 별도 검증하며 Git 원격 등록은 필수 조건이 아니다.

| 외부 VCS 사용 환경 | Splinter가 공통으로 제공할 목표 | Splinter의 환경별 보호 책임과 현재 상태 |
| --- | --- | --- |
| Git | 생성·재개·독립 Editor·내용 검사·독립 결과·회수·폐기 | 기존 commit/ref·linked worktree·통합/dirty guard로 관리 정보를 보호. 공유 객체/등록과 작업별 변경분·결과 비용을 구분하고 회수 시 원본/peer를 유지. 현재 한정 자격과 회귀를 보존 |
| VCS 없음 | 동일 | 불변 파일 기준·내용 hash·독립 결과의 기본 수명주기를 한정 마감. 외부 VCS 설치/초기화 불필요. 안전한 원본 반영은 별도 AP 단위의 자동 파일 시험과 작은 Editor 없는 제품 A/B 한정 자격을 구분 |
| Unity VCS / Plastic SCM | 동일 | 선행 일반 폴더 단위에서는 제외. 현재 [호환 계약](../plastic-compatibility.md)의 관리 정보/등록 보호·독립 파일 결과를 별도 구현했고 [PC](../verification/plastic-compatibility.md)의 한정 실제 자격과 회수/인계 제한을 따름 |

병렬 편집·결과 보존과 원본 통합을 분리한다. VCS가 없어도 독립 작업과 결과 보관이 가능해야 하며 Git commit/check-in 성공을 전제하지 않는다. 원본에 적용할 때는 작업 시작 기준·보존 결과·최신 원본을 비교해 이후 원본 변경을 보호한다. 단순 덮어쓰기·자동 `git init`·사용자에게 Git 등록을 요구하는 방식으로 대체하지 않는다.

### 이번에 포함할 구현

- Core/fake부터 기준 식별·변경 내용 검사·독립 결과 보존·명시적 원본 반영의 경계를 정리한다. 파일 기준과 VCS revision을 구분하고 Git SHA·branch·commit을 공통 수명주기의 필수 값으로 요구하지 않는다. 실제 port/필드/명령 이름은 계약 설계에서 확정하며 이 문서의 요구를 현재 schema로 안내하지 않는다.
- Runtime·CLI/Host·Windows의 source 선택/doctor·결과/폐기 조합에 일반 폴더 경로를 연결한다. VCS 선택을 요청/상태/digest에 고정하고 같은 key/id/home·기준/내용으로 재개한다. Git 실패·손상·관측 unknown을 VCS 없음으로 fallback하지 않는다. 첫 일반 폴더 경로는 VCS 관리 정보가 없는 입력으로 한정하며 Git/Plastic 관리 영역을 일반 소스처럼 복제하거나 같은 관리 상태를 여러 작업에 공유하지 않는다. Git 경로의 기존 검사·refs·원본/peer 보호는 유지한다.
- 일반 폴더의 기준은 정확한 불변 snapshot에서 지원하는 경로·파일 유형·내용 hash로 식별한다. Git clean/untracked 기준을 일반 폴더에 강제하지 않으며 metadata·mtime만으로 내용 일치를 선언하지 않는다. 임시/Editor 관리 파일의 소유 범위를 명시하고 원본의 이후 수정과 A/B의 기준/변경분을 격리한다.
- `checkpoint`는 컴파일 성공·VCS commit과 무관한 독립 결과를 제공한다. manifest뿐 아니라 재구성에 필요한 실제 내용과 불변 기준을 소유해 원본 수정·workspace/delta/snapshot 회수 뒤에도 결과를 읽고 검증할 수 있어야 한다. 원본 경로나 회수할 VSS에 의존한 결과를 독립이라고 표시하지 않는다. 명시적 결과 보존에서 필요한 payload·기준 저장 비용은 별도 계측하며 생성/읽기 hydration·전체 파일 copy-up의 금지 경계를 우회하지 않는다.
- 결과 보존형 release와 결과를 요구하지 않는 명시적 discard를 일반 폴더에 연결한다. 정확한 intent·내용/owner·정상 stop/unmount·seal·소유 자원/공유 의존성 guard·같은 요청 중단 재개를 유지한다. VCS 없는 source 정리를 Git worktree/branch 삭제로 위장하지 않는다.
- 기존 Git 경로는 같은 공통 계약으로 동작하도록 맞추고 locked restore·자동 회귀를 유지한다. 입력/출력 schema·fake·적합성·capabilities·기능 문서·AI 스킬/시작 안내·검증표를 함께 갱신한다. 형식 변경이 필요하면 현재 형식을 명시하고 기존 기록/결과를 보존·거부하며 자동 migration이나 다중 버전 fallback은 추가하지 않는다.

### 실행 관문과 완료 기준

실제 시험의 질문은 **Git 없는 작은 NTFS 폴더에서 A/B가 원본 전체 복제 없이 독립 작업하고, 같은 작업을 재개하며, A의 독립 결과/회수와 B의 명시적 폐기를 안전하게 끝낼 수 있는가**다. 새 전체 장애 harness를 만들지 않고 변경한 계약의 자동 적합성과 작은 대표 A/B 한 묶음으로 판정한다.

1. `.git` 없는 일반 폴더에서 해당 제품 명령의 Git 호출/필수 설치 의존성 0을 확인한다. 원본을 몰래 Git 저장소로 만들지 않는다. 생성·읽기의 전체 payload 복제/hydration 0·작은 쓰기의 범위 저장과 원본 위치/내용 보호를 확인한다.
2. A/B의 반환 경로와 서로 다른 key/ID·기준·변경분을 확인하고 새 CLI의 동일 요청 재개를 수행한다. 원본을 이후 수정해도 A/B의 고정 기준이 바뀌지 않아야 한다. A/B 소유 Editor의 경로·PID/시작 시각과 필요한 외부 도구의 실제 결과를 대조한다. Splinter 자체 컴파일 기능은 복원하지 않는다.
3. A의 정상 stop·독립 checkpoint·명시적 release 후 B의 편집/Editor가 지속되고 A 결과가 원본/VSS 없이 검증돼야 한다. B는 미커밋/검증 성공 없는 변경을 명시적으로 discard한다. 결과 수집 전후/unmount seal·독립 결과·원본/peer 보호와 정확한 소유 자원 회수를 확인하며 unknown/reclaim-pending은 별도로 보고한다.
4. 공통 계약에서 필요한 응답 유실·같은 intent 재개·손상/잘못된 대상 거부는 변경 경계의 fake/자동 회귀로 확인한다. 기존 Git 회귀를 통과시키고 Git 없는 결과의 원본 반영은 아직 실행 불가로 명시하며 부작용 전에 거부한다. 이전 Git native/M0 근거를 일반 폴더의 통과로 승계하지 않는다.

입력은 Windows x64·로컬 NTFS·지원하는 일반 파일이다. Editor 없는 기본 작업에는 Unity 프로젝트 메타데이터·Editor·라이선스를 요구하지 않으며 이 거부/발견 경계는 공통 적합성으로 확인했다. 실제 소유 Editor 관문에는 정확한 Unity `6000.3.22f1`의 작은 VCS 없는 fixture를 사용했다. 새 fixture·정확한 project/home·base 내용·기존 서비스 등록/여유 공간을 실행 전에 고정했고 기존 Git fixture의 `.git`을 변경하지 않았다. [native 계획](vcs-independent-validation.md)의 관리자 Apply는 사용자 별도 승인을 받아 실행했다. 이후 다른 입력/구성의 설치·외부 프로젝트 변경은 이번 마감 범위에 자동 포함하지 않는다.

선행 마감 범위는 **원본 반영의 VCS 중립 계약/차단까지**였다. 실제 apply는 아래 별도 AP 단위이며 선행 통과를 승계하지 않는다. 선행 단위에는 실제 apply가 포함되지 않았다. 자동 충돌 병합·Plastic 관리 정보/서버/checkout·check-in·전용 adapter·독립 Unity CLI 전체 공존 검증·대형/원격/OS 장애·개발용 view 확장은 AP 단위에도 포함하지 않았다. Plastic 호환 adapter는 아래 별도 PC 범위를 따른다. 지원 경로의 쓰기 손실·원본/peer 훼손은 우선 수정하고, 나머지는 선택한 단위의 성공/실패·제약을 기록한 뒤 마감한다.

### 이후 순서와 원본 반영 원칙

| 순서 | 개발 단위 | 완료 경계 |
| --- | --- | --- |
| 1: 이번 마감 | VCS 중립 계약·일반 폴더 기본 수명주기·기존 Git 사용 환경의 보호 경계 | 실제 A/B와 독립 결과/회수·Git 회귀의 한정 판정. 원본 반영 실행·Plastic 사용 환경의 자격은 아직 주장하지 않음 |
| 2: 한정 마감 | [VCS 없는 안전한 원본 반영](plain-folder-original-apply.md)과 기존 Git 보호 경계 유지 | 명시적 대상·3자 내용 비교·조건부 게시·동일 intent/부분 수행 복구, Editor 없는 작은 NTFS A/B 한 묶음과 AP-01~06. 자동 내용 병합은 AI/기존 도구 책임 |
| 3: 한정 마감 | [Unity VCS / Plastic SCM 사용 환경의 병렬 격리·효율 호환 처리](plastic-compatibility.md) | 계약/844개 자동 회귀·별도 T: A/B의 생성·원본 지속 편집·같은 요청·Editor·독립 결과·peer/관리 정보 보호·A release/B discard/마지막 snapshot 회수를 확인. 선행 C: 보류와 외부 CLI worker 발견 실패 유지. 비용/후속 한계는 PC가 소유하며 일반 지원/전체 인계 통과로 확대하지 않음 |

원본 반영은 전체 원본을 작업 시작 상태로 되돌리지 않는다. 변경할 경로가 기준과 같을 때 적용하고, 이미 결과와 같으면 동일 intent의 재조정 근거로 확인하며, 원본과 작업이 서로 다르게 변경한 경로·새 파일/삭제/유형 충돌은 보존·거부한다. 영향 없는 원본 변경은 유지한다. 실제 게시·부분 적용 복구의 범위는 [한정 범위](plain-folder-original-apply.md)에 고정했다. 현재 입력/출력 schema와 게시·복구 계약은 [기능 문서](../plain-folder-original-apply.md)를 따른다. AP-05 제품 A/B와 AP-06 비용/회귀를 한정 마감했으며 [실행 근거와 제한](../verification/plain-folder-original-apply.md)을 따른다. native 강제 중단·Unity/Plastic·대형/OS 지원으로 승계하지 않는다. VCS 명령/API는 Splinter의 소유권·격리·기준/결과·회수 경계를 구현하는 데 필요한 범위로 사용한다. 일반 VCS 충돌 해결·check-in 정책은 기존 도구/AI 절차다. 호환 작업의 완료 기준은 Splinter의 안전성과 효율이며 VCS 기능 수나 check-in 성공 횟수가 아니다.

## 첫 버전의 지원 조건

아래는 마감한 Git 기반 M0의 조건이다. 일반 폴더의 한정 자격은 별도 [VCS 검증](../verification/vcs-independent-workspaces.md), Plastic은 별도 [PC 검증](../verification/plastic-compatibility.md)을 따른다. 서로의 native 자격을 승계하지 않는다.

| 항목 | 고정할 범위 |
| --- | --- |
| 환경 | Windows x64, 로컬 NTFS, Git, 등록된 project/home과 기존 제한 VSS 서비스. Unity fixture의 정확한 패치 `6000.3.22f1`부터 실제 실행을 확인 |
| 입력 | 검증된 clean Git 입력과 일반 파일·디렉터리. unsupported 파일 특성·LFS/filter·dirty 입력은 현재 거부 정책 유지. 주 사용 프로젝트가 이를 필요로 하면 별도 최소 지원 범위를 먼저 정함 |
| 저장 | 원본 NTFS 위치/파일시스템 유지, 원본 계속 편집 가능, 최초 전체 복제·읽기 hydration·파일 전체 copy-up fallback 금지, 변경 범위 저장 |
| 실행 | 독립 소스 편집과 검증을 구분. 필요한 Editor만 실행하며 기본 독립 Editor 또는 필요 시 개발용 저장소 view를 선택. 자동 모드 선택·큐·원본 Editor 공유는 없음 |
| AI 진입 | 개발 배포물/기존 CLI와 최소 사용 지침, 안정된 task key·workspace ID·절대 경로·동일 home. 전역 마지막 작업 추측이나 특정 Codex 세션 API를 필수로 하지 않음 |
| 보장 | 지원 경로의 원본/타 작업 격리, 승인된 쓰기 보존, 정확한 소유 자원 처리, 같은 버전의 정상 재개와 요청 재전송. OS 장애 전체 보장·보안 샌드박스는 선언하지 않음 |

현재 구현을 지원 완료로 승격하는 표가 아니다. 새 후보의 실행 자격과 지원 한계를 함께 기록한다. 선택적 ReFS와 diagnostic Git checkout은 NTFS 정상 경로의 fallback이 아니며 첫 버전의 지원 확대 대상에서 제외한다.

## 제품 작업 순서

| 순서 | 산출물 | 완료 기준과 수정 한도 |
| --- | --- | --- |
| 1 | 명시적 `discard` 계약·구현: 연결/로컬 단위 마감 | dirty 작업·같은 intent 재개/중복·peer 보호·실제 회수 확인. Unity 컴파일 실패/Editor stop은 3의 대표 흐름에 포함하며 이 때문에 별도 장애 행렬을 열지 않음 |
| 2 | 최소 AI 작업 연결·사용 지침: Editor 없는 단위 마감 | [스킬 원본](../../skills/splinter/SKILL.md)과 실제 explicit/direct·반환 경로 편집·새 CLI의 같은 key/id/home 재개·폐기 확인. 설치 연결은 AI 절차이며 전용 installer·자동 binding은 구현 제외 |
| 3 | A/B의 대표 사용 흐름: 한정 마감 | [대표 사용](../verification/representative-agent-workflow.md)의 오류/수정·같은 작업 재개·dirty guard·보존/통합/release와 실패 작업 discard. 첫 수집/포커스 거부와 같은 작업 재시도 제한 포함 |
| 4 | 공간·시간의 채택 판단: 한정 마감 | 같은 작은 warm 입력의 관측 파일 할당·준비/편집/종료 비용으로 지속 독립 작업에 한정 채택. VSS 귀속·물리 peak·대형 효율 unknown, 새 비교/반복 관문 없음 |
| 5 | 한정 버전 납품: 마감 | [시작 안내](../agent-start.md)·[현재 복구 근거/납품 판정](../verification/m0-delivery.md). 기존 개발 CLI·등록 서비스 전제의 한정 버전이며 일반 설치·대형/범용 공개 지원으로 확대하지 않음 |

discard의 상세 의도·공유 자원·중단 조건은 [대화별 작업 계획](session-workflow-and-installation.md#완료보존폐기)이 소유한다. 첫 구현은 정확한 worker workspace를 대상으로 한다. 사용 중인 candidate/source/cache 의존성은 명시적으로 차단하고 해당 의존성 정리 행동을 반환한다. 원본 Editor·타 작업·공유 view를 자동 종료하거나 연쇄 삭제하지 않는다. snapshot 회수가 지연되면 논리적 작업 종료와 물리 회수 대기를 구분하며 숨은 백업이나 회수 완료로 표시하지 않는다.

### 한정 납품 경계와 후속 선택

M0는 기존 개발 checkout의 CLI·소스 스킬과 등록된 서비스/driver 환경을 제공물로 삼는다. [대표 사용/비용](../verification/representative-agent-workflow.md)은 실행·실패·같은 작업 재시도를, [납품 판정](../verification/m0-delivery.md)은 현재 회귀·과거 native의 재사용 한계와 제외할 장애 조건을 소유한다. 일반 installer·자동 binding·외부 공개/배포를 완료한 것으로 읽지 않는다.

후속은 위 책임 표와 종료 기준 안에서 실제 사용자 요구로 고른다. 새 프로젝트/입력이면 해당 입력·등록·mount 경계, 설치가 필요하면 AI 사용 절차와 기존 구성 도구, 관측 비용이 실용성을 막으면 그 입력의 병목을 대상으로 한다. 필요가 정해지기 전에 새 harness·전체 장애 행렬·VIVOBOOK·대형 PV·p95 반복을 개시하지 않는다. 지원 경로의 승인 쓰기 손실·원본/peer 훼손이 새로 확인되면 그 결함을 먼저 수정한다.

개발 순서는 **물리 공간 절감 → 메모리 절감 → 안정적인 병렬 작업**이며 현재 물리 저장/메모리 단계는 PS/ME의 한정 범위로 마감했다. 다음 일반 후속은 안정적인 병렬 작업이다. [저장공간 우선 계획](storage-priority-and-editor-modes.md#후속-개발-우선순위)이 준비 시간의 허용 기준과 판단 방법을 소유한다. 몇 분 이내의 새 Editor 준비는 허용하며 AI의 계획/분석과 병행할 수 있으므로 준비 시간 단축을 높은 우선순위에 두지 않는다. 기존 개발 범위와 마감한 M0 판정은 유지한다.

## 유지할 구현과 제거 경계

| 판단 | 현재 경계 |
| --- | --- |
| 유지 | Core ports/schema, atomic 상태·잠금, Git linked worktree, VSS/WinFsp 범위 저널·승인 쓰기 flush·결과 seal, process supervisor·소유권·회수 guard |
| 유지 | Unity start/status/stop의 실행·자원 관측과 Coplay peer 보호. 프로젝트 검증은 외부 도구·AI의 책임 |
| 제거 | 자체 Unity refresh·컴파일 이벤트/도메인 reload 증거·포커스 재시도·내부 컴파일 성공 gate와 중복 preserve-worker |
| 개발 경로로 분리·확장 중지 | ReFS/baseline·diagnostic checkout·단일 view/Library cache·spike/probe. `splinter-dev.ps1`로만 생성/선택하고 저장소 보호 회귀와 재현 근거로 유지. 기본 제품 발견에서 제외 |
| 구현 범위에서 제외 | DAG/AI Runner·daemon/DB·범용 Unity 저작/Play/Test Runner, 전용 installer·Codex 자동 binding, 자동 모드·큐·일반 임대 인수·원본 Editor 공유, 충돌 해결/생성 소스 반환 병합·게임 검증 플랫폼 |

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
- 실패는 보존하고 동일 원인의 한정된 재현·수정·회귀를 수행한다. 성공하면 선택한 후속 단위를 마감한다. 마감한 M0를 자동 재개하지 않는다. 새 harness·전체 경계 조합·반복 수 확대에는 답할 질문과 종료 조건을 먼저 적는다.
- 비용 관측은 고정 입력의 최초·기존 작업 재개와 반복 편집을 구분한다. 측정 배수·절감률·시간 SLA를 미리 약속하지 않는다. 기존 C/S/Q 대형 PV는 자동 재개하지 않으며 p95/20회 반복은 첫 버전 관문이 아니다.
- 손실된 승인 쓰기·원본 수정·타 작업 종료/삭제가 지원 경로에서 재현되면 해당 사용 경로를 차단하고 먼저 수정한다. 과거 실패는 원인이 밝혀지기 전 unknown을 유지한다.

## 별도 지원 확대

[NR](ntfs-recovery-and-support.md)의 OS 장애·ARM·파일 조합과 [SF](storage-priority-and-editor-modes.md)의 대형 용량은 필요 시 지원 검증이다. [SW](session-workflow-and-installation.md)의 설치·대화 인계·도구 선택·게임 검증은 AI 사용 절차로 다룬다. 자동 binding·큐/모드 선택 등 구현 제외 항목을 미완성 개발 목록에 남기지 않는다. 기존 `NotRun`/`Partial`·실패/unknown은 증거 상태로 보존하며 Passed나 Unsupported로 재표기하지 않는다. 일반 에셋 I/O의 mount 정확성은 제품 책임이고 에셋 저작·Play/Test 명령 구현은 외부 도구의 책임이다.

이전 P0~P7·F 단계와 48개 목록은 첫 버전의 진척 분모가 아니다. 장기 시나리오 ID·실패·재현 조건은 [검증 추적표](validation-matrix.md)에 보존하고 M0의 한정된 완료와 구별한다. 외부 VCS 호환은 위 선택한 개발 단위의 공통 계약/fake와 실제 입력만 연결한다. 다른 VCS·OS·앱의 확장을 선행 구현하지 않는다.
