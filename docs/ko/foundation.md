# 현재 기반

[English](../foundation.md)

기준본·변경분·seal·intent와 종료 명령의 차이는 [용어집](glossary.md)을 참고한다.

MCP·Unity CLI는 사용자가 설치한 도구 중 에이전트가 필요할 때 선택하는 선택적 외부 도구다. 없어도 Splinter의 저장 격리·기준본·Library/미변경 파일 참조·범위 변경·독립 결과·안전한 원본 반영과 소유 자원 수명 기능은 성립한다. 특정 도구 이름·버전·내부 API는 제품 전제나 gate가 아니다. 도구 선택·연결·refresh·컴파일·Play/Test/build·캡처와 결과 판단은 에이전트가 맡는다. 외부 도구 설치·다운그레이드·수정·별도 수정본 생성/운영은 Splinter 범위에서 제외한다. [외부 도구 경계](unity-mcp-response-recovery.md)와 [결합 제거 검증](verification/external-tool-boundaries.md)을 따른다.

Splinter의 제품 경로는 **NTFS 작업공간 생성/재개 → 에이전트의 외부 도구 작업 → checkpoint → 선택적 integrate → 명시적 release/discard**다. 작업공간·소유 Editor·범위 저장·독립 결과·회수 안전성을 관리한다. 컴파일·테스트·게임 합격은 에이전트와 MCP·Unity CLI 등 외부 도구의 책임이다.

외부 도구를 사용하는 경우 [정확한 대상 선택](alpha-workspaces.md#unity-cli의-정확한-대상-선택)을 수행한다. 과거 [UH](verification/unity-cli-handoff.md)·MCP 실행은 당시 입력/후보의 근거이며 현재 제품의 도구 버전 요구나 컴파일 gate가 아니다. Splinter 브리지는 외부 패키지의 종료 callback·서버·run-state를 조작하지 않는다.

제품 시작점은 [AI 안내](agent-start.md)와 `scripts/splinter.ps1`이다. 진단·공통 Library view·spike/probe는 [개발 CLI](development-cli.md)로 분리했다. 기존 verification은 해당 후보/입력의 근거이며 변경된 계약의 native 자격을 자동 부여하지 않는다.

병렬 격리와 범위 저장은 Splinter의 공통 저장소 계층이 제공한다. 소스 공급자는 선택한 VCS 관리 정보와 Splinter 작업의 기준·결과·회수 사이의 안전 경계를 담당한다. 제품 CLI는 Git·VCS 없는 일반 폴더·Plastic 사용 환경를 명시적으로 선택한다. 소스/application 종류와 opaque 기준/결과 identity를 v2 작업 상태에 고정하고, 일반 폴더는 불변 파일 manifest·작업별 독립 payload를 사용한다. 실패 시 다른 소스로 fallback하지 않는다. 현재 제약과 실제 자격은 [VCS 없는 작업공간](vcs-independent-workspaces.md)을 따른다. Git 원격 등록은 불필요하며 Plastic 사용 환경의 관리 정보/등록 격리와 독립 파일 결과는 [현재 계약](plastic-compatibility.md)을 따르며 [실제 자격](verification/plastic-compatibility.md)을 별도로 확인한다. 일반 폴더의 editorless [원본 apply](plain-folder-original-apply.md)는 계획/intent·조건부 Windows 게시를 제공하며 실제 제품 자격은 작은 Editor 없는 NTFS A/B 한 묶음에 한정한다.

## 구조와 의존성

JSON object/array 내부의 중복 속성명 순회는 Runtime의 `JsonPropertyValidation`이 공통으로 소유한다. 이름은 Ordinal로 비교하며 크기·깊이·버전·역직렬화 옵션과 오류 코드는 각 입력/상태 경계에서 유지한다. 개발용 view 응답은 `start`의 Accepted 판단에만 임시 JSON 검사를 사용하며 다른 action의 결과는 최종 응답 출력에서 직렬화한다.

[OS와 앱 확장 경계](extensibility-boundaries.md)는 snapshot identity에 고정한 경로 정책·선택적 Unix 권한 metadata와 앱별 프로젝트 정책을 소유한다. Unity의 원본 잠금/동반 파일/Library descriptor는 Unity 어댑터에 있고 Files·Windows는 명시적으로 주입한 정책을 사용한다. 관리형 basis 상태·준비 intent는 앱/디렉터리 identity를 명시한 v2만 지원한다. 다른 OS·앱의 실제 어댑터는 없으며 자동 적합성과 실제 지원은 [검증](verification/extensibility-boundaries.md)에서 구분한다.

Plastic 내용 reader는 Files에 내장된 고정 client adapter를 일반 사용자 PowerShell 자식에서 실행하고 private stdio frame만 읽는다. 공개 API listener나 새로운 CLI 명령은 제공하지 않는다. Windows `cm 11.0.16.9915`·PowerShell 7 전제와 helper의 bounded segment는 [현재 계약](plastic-project-basis.md), 자동/한정 native 읽기는 [보안 경계 검증](verification/security-boundaries.md)을 따른다. 작업공간의 저장 격리는 악성 Git hook·Unity 코드·인증된 MCP 코드 실행에 대한 OS 보안 sandbox가 아니다.

[VCS 없는 프로젝트 기준본](plain-project-basis.md)은 Windows `ManagedDirectoryBasis(projectBasis:true)`의 원본 prepare/restore와 `ProjectDirectorySnapshotProvider`의 task-owned reference를 사용한다. 공통 `IProjectSourceAccess`는 원본 읽기/게시 동안 정확한 기준·mount·필요한 Editor 종료 lease를 보유한다. Files는 기존 독립 결과/3자 반영을 재사용하고 Windows publisher는 검증된 원본 view만 제한적으로 허용한다. task 회수는 peer 공통 payload를 삭제하지 않으며 마지막 restore가 기준 수명을 소유한다. Core에 Win32/Unity API를 추가하지 않는다.

Library/프로젝트의 공통 restore는 v2 계획·기존 파일 identity·신규 생성 영수증과 Windows 핸들 보호를 사용한다. Runtime의 저널 자식 색인은 생성/이동/삭제/재생에서 갱신하며 디렉터리 조회의 전체 경로 검색을 피한다. Git의 `GitProgressReadStream`은 정확한 소유 reader의 blocking read만 30초로 제한하며 진행 중인 전체 object 검증을 중단하지 않는다. 오류의 응답/복구 분류는 Core `WorkspaceFailurePolicy`가 공통으로 소유한다. 각 계약·자동/실제 자격은 기능별 문서를 따른다.

| 프로젝트 | 현재 책임 | 참조 |
| --- | --- | --- |
| `Splinter.Core` | workspace/storage/source/application/process ports·소유권·결과·실패 계약 | .NET 기본 라이브러리 |
| `Splinter.Protocol` | 응답 DTO·제품/개발 명령 카탈로그·내장 JSON Schema | Core, JsonSchema.Net |
| `Splinter.Host` | 읽기 전용 발견/진단 | Core, Protocol |
| `Splinter.Runtime` | 작업공간 수명·atomic JSON 상태/잠금·범위 저널과 후보/cache 수명 | Core |
| `Splinter.SourceControl.Files` | 일반 폴더/Plastic 파일 기준·독립 payload·Plastic 읽기 전용 호환 경계·고정 소스 라우팅·폐기 영수증 | Core, Runtime |
| `Splinter.SourceControl.Git` | fixed commit/tree/blob reader·소유 기준 ref/fence·linked worktree/index·현재 소스 hash·독립 결과·guard가 있는 merge/회수 | Core |
| `Splinter.Application.Unity` | 정확한 버전/프로젝트의 Editor 실행·준비 관측·정상 종료·소유 bridge | Core |
| `Splinter.Platform.Windows` | VSS/Git 불변 reader 조합·WinFsp·변경 범위 저널·제한 권한 서비스·Job Object | Core, Runtime, Files, Git, WinFsp.Net; Win32 |
| `Splinter.Workflows.Windows` | 개발용 불변 source view·공통 Library cache·독립 결과와 회수 의존성 | Runtime와 Git/Unity/Windows |
| `Splinter.Cli` | 제품 명령 파싱·어댑터 조합·JSON/종료 코드; 개발 진입점의 공통 조합 | Host, Runtime, Git/Unity/Windows/workflow, System.CommandLine |
| `tools/Splinter.Development` | 별도 개발 명령 진입점 `splinter-dev` | CLI |
| `unity/Splinter.Editor` | liveness·import/compile 진행 관측·미저장 씬/Play 보호와 정상 종료 | Unity Editor API; 소유 workspace에만 포함 |
| `Splinter.Tests`, `tests/Splinter.UnityBridgeHarness` | 계약/fake·실제 Git·Windows 자식·bridge 소스 적합성 | xUnit v3, MTP; bridge에는 Unity API stub 사용 |
| `tools/Splinter.ReplayProbe`, `tools/Splinter.WriteProbe` | 고정 합성 입력의 읽기/쓰기 비용과 정확성 계측 | Runtime; 개발 도구 |

CLI는 짧은 프로세스다. Host는 읽기 전용 in-process 계층이며 daemon/DB writer가 아니다. Runtime은 부작용 전 intent와 같은 요청 영수증을 durable 상태에 게시한다. 공급자 전용 API는 어댑터에 있고 Core에는 Unity/Win32/Git/Codex API를 넣지 않는다.

다음 행동의 명령 ID와 인자는 Runtime의 `WorkspaceActions`가 함께 구성하고 CLI가 응답에 투영한다. 사람이 읽는 명령 문자열을 다시 파싱하지 않는다. 같은 ensure의 project/key/base/storage/source/application·Library 또는 project basis, 원본 반영의 target/intent/digest/선택 경로를 유지하며 CLI 응답과 실패 복구가 같은 ensure 인자를 사용한다. 기존 schema와 durable 상태 형식은 그대로다. 변경한 검색·객체 검증·압축 순회의 자동 회귀와 측정 한계는 [최적화 검증](verification/code-quality-optimization.md)을 따른다.

Runtime의 파일 잠금은 workspace ID별 명령을 직렬화하며 프로젝트/공유 기준/snapshot 잠금은 필요한 기존 부작용 경계를 보호한다. 확인된 sharing/lock violation만 한정 대기한다. 잠금 경로·접근·다른 I/O 실패는 `StateReadUnavailable`로 즉시 거부하며 점유나 자동 복구로 해석하지 않는다. 현재 동시 CLI/파일 I/O의 한정 자격은 [SP 검증](verification/stable-parallel-completion.md)을 따른다.

[읽기 전용 저장 장부](source-verification.md#계측의-의미)는 현재 SJR2의 stored/decoded·논리 유효 범위·참조 블록 전체 비용과 정확한 task/shared 파일 할당을 구별한다. 물리 저장 개발의 종료 경계와 최종 후보 자격은 [PS 범위](plans/physical-storage-completion.md)·[실행 검증](verification/physical-storage-completion.md)을 따른다. 제품 replay·seal과 VSS 소유권/회수 계약을 계측기로 대체하지 않는다.

workspace 상태와 Windows 제어 IPC는 같은 디렉터리에 flush한 임시 파일을 `File.Replace` 또는 최초 덮어쓰기 없는 move로 게시한다. reader의 delete sharing, 한정 공유 오류 재시도, unknown/손상/소유권 오류 보존은 [NTFS 계약](ntfs-workspaces.md)을 따른다. 상태 쓰기 실패 단계/HResult·게시 완료 여부는 [진단 계약](verification/state-write-diagnostics.md)에 있다.

[현재 압축 저널](journal-payload-compression.md)은 overlay v2, OFF SJR2·candidate v3의 raw/Brotli 범위를 사용한다. [최적화 ON](latest-only-journal.md)은 SJL4/root v4 capsule로 현재 private payload·metadata만 유지하며 mutation마다 한 durable flush·게시·이전 payload/metadata 회수 뒤 성공을 반환한다. 실제 제품 연결 자격은 [JP](verification/journal-payload-compression.md), 내부 buffer와 비용/회수는 [ME](verification/memory-completion.md), 새 현재본 표현은 [LJ](verification/latest-journal-write-optimization.md)로 구분한다. 압축 준비는 scratch 하나와 선택 결과 배열을 사용하며 작은/빈 compressed read는 chunk와 요청분 staging으로 전체 stream 검증 뒤 게시한다. 범위 저널은 승인 쓰기의 개별 durable flush·전후/unmount seal을 유지한다. 동일 private 바이트와 일치 앞뒤의 작은 변경 범위를 재사용하며 최초 전체 복제·읽기 hydration·전체 파일 copy-up fallback은 없다. source fingerprint cache는 snapshot/provider 세대·변경 이력에 묶이고 초기/감사/재연결은 전체 내용을 확인한다. [소스 검증](source-verification.md)은 저장 내용 검사이며 프로젝트 합격 판정이 아니다.

private Mutation의 기본값 JSON은 생략하고 명시적 정리/후보에서는 더 작은 현재 메타데이터를 선택한다. 입력/응답·SJR2 의미를 바꾸거나 저장된 저널을 읽기 중 변환하지 않는다. 실제 Unity 저널의 오프라인 파일 할당과 새 후보 native 실행의 구분은 [JM 검증](verification/journal-metadata-storage.md)을 따른다.

[Checkpoint/release 최적화](verification/checkpoint-release-optimization.md)는 안정된 seal 안에서 Git checkpoint/보존을 결합하고 SJL4 inventory 검색을 인덱스로 바꾸며 새 회수의 seal 검증을 통합한다. 실행 중 writer 계수는 실제 파일 감사와 구분해 표시하고 poisoned 계수는 provider를 종료하지 않고 unknown으로 남긴다. 요청 파일 알림으로 provider를 깨우되 1초 heartbeat polling과 요청 identity 검사는 유지한다. 기존 독립 결과·내용 hash·소유권·중단 복구 검사를 유지한다.

소유 process supervisor와 PID/시작 시각/토큰·자식 자원 0, 실제 mount/Git/delta·snapshot 회수 관측은 제품 핵심이다. [명시적 discard](workspace-discard.md)는 별도 durable intent/authorization을 사용한다. `release`는 독립 결과를 남기며 shared/reclaim-pending·unknown을 회수 완료로 표시하지 않는다. 열린 개발용 candidate/source/cache도 snapshot 의존성으로 계속 보호한다.

workspace 상태는 저장·소스·소유 자원·독립 결과와 반영/회수 intent를 기록한다. view ledger는 후보 게시·소스 세대·cache 의존성과 요청별 receipt를 기록한다. 지원하지 않는 필드·형식과 손상된 상태는 보존하고 명시적으로 거부한다. 컴파일·Play/Test/build와 게임 합격 판단은 에이전트와 외부 도구가 수행한다.

개발용 저장 계약은 Core의 `ViewWorkflow*`·`ImmutableCandidate`·`SourceViewSelection`, Runtime의 `ViewWorkflowCoordinator`·`SourceViewDeltaStore`·`CacheSnapshotTree`, Windows의 `ViewLifecycle`·`WindowsViewProvider`, Git의 `GitSourceResult`가 소유한다. 현재 schema와 회귀 근거는 [저장 view 계약 검증](verification/storage-view-contract.md)을 따른다.

## 에이전트용 명령 계약

개발 checkout의 실행 접두사는 `pwsh ./scripts/splinter.ps1`이다. stdout은 JSON 응답 하나이며 acceptance·성공·회수 대기·실패를 구분한다. 첫 사용 전에 `schema <command>`를 읽고 반환된 `data.workspace.path`를 편집/외부 도구의 기준으로 사용한다.

| 제품 명령 | 책임 |
| --- | --- |
| `capabilities`, `schema`, `doctor` | 현재 제품 계약 발견·입력/출력·읽기 전용 진단 |
| `workspace ensure/list/status` | NTFS 생성/같은 요청 재개·저장 목록·소유 자원 관측 |
| `workspace status --verify-source/--audit-source` | 현재 내용 hash 확인/전체 감사 |
| `workspace checkpoint` | 소유 Editor 정상 stop·안정된 소스 checkpoint·독립 결과. 컴파일/테스트 성공 불필요 |
| `workspace integrate` | Git의 정확한 결과·깨끗한 branch merge 또는 일반 폴더/recorded Plastic의 `--target original --plan`/고정 intent·digest·선택 경로 조건부 NTFS 게시 |
| `workspace compact` | 정상 stop/unmount·seal·명시적 예산으로 private 저널 정리. [최적화 ON](latest-only-journal.md)은 이후 현재본만 저장·선택 유지, 새 작업 기본 ON·기존 저장 선택 유지. 명시적 compact의 [기준본 복귀](journal-base-reversion.md)도 포함 |
| `workspace release/discard` | 독립 결과 보존 뒤 회수/명시적 결과 폐기. peer와 공유 의존성 보호 |
| `library prepare/status/restore` | 명시적으로 허용된 원본 Library의 같은 볼륨 이동·원본/A/B 범위 저장·마지막 일반 NTFS 복원. [계약](git-library-reference.md) |
| `project prepare/status/restore` | 명시적으로 허용된 VCS 없는 전체 원본의 같은 NTFS 볼륨 이동·source/Library 공통 기준·원본/A/B 범위 저장·마지막 일반 NTFS 복원. [계약](plain-project-basis.md) |
| `unity start/stop` | 해당 workspace Editor의 수명. 준비/종료는 상태 관측으로 확인 |
| `storage service status` | 기존 제한 VSS 서비스의 읽기 전용 상태 |

개발 계약은 `scripts/splinter-dev.ps1 capabilities/schema`에서 발견한다. `view`는 불변 후보·소스 세대·Library cache·독립 결과의 수명을 관리한다. spike/probe의 별도 입력과 실행 조건은 [개발 CLI](development-cli.md)를 따른다.

### 진단의 의미

`doctor`·저장된 list·Editor ready·source fingerprint·checkpoint 성공은 각각 자신의 경계만 증명한다. 외부 도구 연결·게임 동작·컴파일/테스트 합격을 뜻하지 않는다. 외부 도구의 정확한 A/B projectRoot·공유 서버 공존은 별도 사용 자격이다.

### 제공하지 않는 작업 그래프

AI Runner·DAG/plan 제출·daemon/DB·큐/자동 모드·Codex 자동 binding·전용 installer·범용 Unity 저작/Play/Test API는 제공하지 않는다. 설치 연결·대화 인계·작업 순서·검증 판단·충돌 해결은 에이전트 절차다.

## 저장소와 브랜치 운영

`development`가 개발/통합 기준이고 `main`은 출시용이다. 형제 `../splinter-unity`는 독립 Git/Unity fixture이며 해당 지침을 별도로 따른다. 문서나 소스 변경이 기존 결과 refs·snapshot·다른 작업의 정리 승인은 아니다.

## 빌드와 시험

```powershell
pwsh ./scripts/build.ps1
```

locked restore·Release·자동 계약/fake/Git/Windows 회귀를 수행한다. 의존성은 중앙 버전과 lockfile을 유지한다. 자동 시험을 native VSS/Unity·ARM/OS 장애 자격으로 승계하지 않는다. 원시 근거와 지원 제한은 [검증 안내](verification/README.md)에 보존한다.

외부 복구·성능 계측·보존한 계측의 복구 진입점과 자식 전용 `Child`·`ChildWriter`·`ChildProvider`·`ChildClient`·`ChildReader`·`ClientActor`·`ChildEnsure`는 지정된 실행 환경 변수가 없으면 Skipped다. 해당 스크립트나 부모 시험이 환경 변수를 전달할 때 같은 진입점이 실행된다. 기본 자동 실행에서도 부모 시험이 호출하는 합성 저널·준비 중단 회귀는 실행하며, 외부 kernel·제품 쓰기 복구의 실행 자격과 구분한다. 동작·metadata·guard 시험에는 작은 입력을 쓰고, 큰 파일 재사용·chunk 경계·저장 비용을 검증하는 시험에서만 필요한 큰 입력을 명시한다.

Windows 전용 파일 공유·native 계약 시험은 다른 OS에서 Skipped다. 단독 고정 응답·오류 매핑·계측 시험이 같은 동작을 반복하면 실제 생성·재개·거부·재생 시험에 고유 assertion을 합친다. 변경 없는 중간 부모 아래의 rename·요청 digest·개별 durable flush·원본/독립 결과 보호처럼 회귀를 막는 검증은 함께 유지한다.

### 실제 저장소의 Git 수명주기 시험

`pwsh ./scripts/test-worktree-lifecycle.ps1 -Repository <경로>`는 명시한 저장소의 worktree 등록을 수정하는 개발 시험이다. Unity/CoW 자격과 구별하며 기존 작업·원본·refs를 보존한다.

## 다음 구현 경계

[구현 계획](plans/implementation-plan.md)을 따른다. VCS 중립 계약·일반 폴더의 기본 수명주기·기존 Git 사용 환경의 보호 경계는 작은 A/B 한 묶음의 제한을 명시해 마감했다. [일반 폴더의 안전한 원본 반영](plain-folder-original-apply.md)의 구현·자동 파일 시험과 작은 Editor 없는 제품 A/B 한정 마감을 구분한다. [Plastic 호환 계약](plastic-compatibility.md)은 별도로 연결하고 작은 T: A/B의 마지막 회수까지 한정 마감했다. 선행 C: 회수 보류·외부 CLI 발견 실패는 [PC 검증](verification/plastic-compatibility.md)을 따른다. recorded Plastic의 [전체/선택 원본 반영](plastic-original-apply.md)은 [별도 한정 자격](verification/plastic-original-apply.md)을 따른다. 외부 도구의 전체 인계 자격은 필요한 입력에서 별도로 확인한다. 저장/메모리는 실제 관측 병목만 고른다. 마감한 Git 기반 M0·대형 PV·전체 장애 카탈로그를 자동 재개하지 않는다.

Plastic의 `plastic-snapshot`은 `Splinter.SourceControl.Files`의 고정 revision reader와 기존 Windows overlay/공통 Library dependency를 연결한다. 현재 계약과 자격은 [프로젝트 기준본](plastic-project-basis.md)·[검증](verification/plastic-project-basis.md)을 따른다. 원본 반영은 위의 별도 계약을 따르며 새 Plastic 등록/check-in·서버 pin은 제공하지 않는다.
