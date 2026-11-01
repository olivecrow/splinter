# 현재 기반

[ST-29–35](verification/scalable-storage-throughput-followup.md#st-2935-자원과-동시성-후속)는 Runtime `StorageResources`·`BoundedParallel`을 추가한다. 수요 기반 프로세스별 최대 4GiB 작업 목표·제한된 hash 중첩 실행·삽입/변경 색인 배치·목록 metadata 재사용·정렬 파일 비교·디스크 여유에 따른 병렬 staging을 연결한다. 메모리를 미리 확보하지 않으며 실제 RSS와 여러 provider 합산을 4GiB로 강제 제한하지 않는다. 현재 형식·capabilities·내구성/소유 보호는 유지한다.

[ST-01–23](verification/scalable-storage-throughput-followup.md)은 Core `IWorkspaceMetadataBatch` port·Windows metadata callback 수집·Runtime 취소 가능한 `PayloadHash`·Files `FileRawRangePreparation`을 추가한다. 내부 처리 경계이며 현재 영속/wire 버전과 전체 내구성/소유 proof를 유지한다. 시간과 자격 범위는 명시적으로 한정한다.

[ST-24–28](verification/scalable-storage-throughput-followup.md#st-2428-구조적-후속-2026-11-01)은 Runtime `PagedMetadataBatch`·고정 작업 안의 제한된 native 내용 증명 재사용·동일 path import 재사용·Git의 정렬 이전 catalog 병합을 추가한다. 저널 ACK/root 내구성과 현재 형식은 유지하며 작은 국지 검사와 metadata 비용 감소만 확인했다.

대규모 저장 후보는 task v4·관리형 basis v3·제품 overlay v3를 사용한다. 현재 표현과 진행 중인 SCAL-01–08 자격은 [대규모 저장](scalable-storage.md)이 소유한다. [반복 원본 반영](repeated-original-apply.md)의 불변 수집·명시적 결과·경로별 이력·외부 도구의 실행 중 Editor 협력은 유지한다. 과거 [RI](verification/repeated-original-apply.md)·MX·GCO 근거는 당시 task v3/basis v2 후보에 결속한다. 이전 상태는 보존·거부하고 일치하는 바이너리를 사용하며 자동 이관·회수하지 않는다.

[English](../foundation.md)

[GCO-01–04](verification/git-collection-optimization.md)가 현재 Git 수집 최적화의 한정 자격을 소유한다. 정확한 lease 아래 로컬 ref/receipt 검사·게시 시 전체 재검증을 하는 범위 reader와 전체 seal·재시도 의미를 유지하는 새 결합 checkpoint/release의 수집 한 번 공유다. 해당 GCO 후보는 wire schema·영속 버전을 유지했으며 별도 SCAL 후보가 현재 형식을 변경한다.

[수명주기 병목 후속](plans/lifecycle-bottlenecks.md)은 최종 전체 일치·metadata 변경·기존 leaf 삭제의 Library 디렉터리 복원과 선택적 쓰기/Git 관측·읽기 전용 시작 캡처를 설명한다. 미지원 내용/구조 변경은 일반 파일별 복원을 유지한다. [BO 검증](verification/lifecycle-bottlenecks.md)은 구성 요소 증거와 실제 Unity 수명주기 자격을 구분한다.

기준본·변경분·seal·intent와 종료 명령의 차이는 [용어집](glossary.md)을 참고한다.

MCP·Unity CLI는 사용자가 설치한 도구 중 에이전트가 필요할 때 선택하는 선택적 외부 도구다. 없어도 Splinter의 저장 격리·기준본·Library/미변경 파일 참조·범위 변경·독립 결과·안전한 원본 반영과 소유 자원 수명 기능은 성립한다. 특정 도구 이름·버전·내부 API는 제품 전제나 gate가 아니다. 도구 선택·연결·refresh·컴파일·Play/Test/build·캡처와 결과 판단은 에이전트가 맡는다. 외부 도구 설치·다운그레이드·수정·별도 수정본 생성/운영은 Splinter 범위에서 제외한다. [외부 도구 경계](unity-mcp-response-recovery.md)와 [결합 제거 검증](verification/external-tool-boundaries.md)을 따른다.

Splinter의 제품 경로는 **NTFS 작업공간 생성/재개 → 에이전트의 외부 도구 작업 → checkpoint → 선택적 integrate → 명시적 release/discard**다. 작업공간·소유 Editor·범위 저장·독립 결과·회수 안전성을 관리한다. 컴파일·테스트·게임 합격은 에이전트와 MCP·Unity CLI 등 외부 도구의 책임이다.

외부 도구를 사용하는 경우 [정확한 대상 선택](alpha-workspaces.md#unity-cli의-정확한-대상-선택)을 수행한다. 과거 [UH](verification/unity-cli-handoff.md)·MCP 실행은 당시 입력/후보의 근거이며 현재 제품의 도구 버전 요구나 컴파일 gate가 아니다. Splinter 브리지는 외부 패키지의 종료 callback·서버·run-state를 조작하지 않는다.

제품 시작점은 [AI 안내](agent-start.md)와 `scripts/splinter.ps1`이다. 진단·공통 Library view·spike/probe는 [개발 CLI](development-cli.md)로 분리했다. 기존 verification은 해당 후보/입력의 근거이며 변경된 계약의 native 자격을 자동 부여하지 않는다.

병렬 격리와 범위 저장은 Splinter의 공통 저장소 계층이 제공한다. 소스 공급자는 선택한 VCS 관리 정보와 Splinter 작업의 기준·결과·회수 사이의 안전 경계를 담당한다. 제품 CLI는 Git·VCS 없는 일반 폴더·Plastic 사용 환경를 명시적으로 선택한다. 소스/application 종류와 opaque 기준/결과 identity를 task v4 후보 상태에 고정하고, 일반 폴더는 불변 파일 manifest·작업별 독립 payload를 사용한다. 실패 시 다른 소스로 fallback하지 않는다. 현재 제약과 실제 자격은 [VCS 없는 작업공간](vcs-independent-workspaces.md)을 따른다. Git 원격 등록은 불필요하며 Plastic 사용 환경의 관리 정보/등록 격리와 독립 파일 결과는 [현재 계약](plastic-compatibility.md)을 따르며 [실제 자격](verification/plastic-compatibility.md)을 별도로 확인한다. 일반 폴더의 editorless [원본 apply](plain-folder-original-apply.md)는 계획/intent·조건부 Windows 게시를 제공하며 실제 제품 자격은 작은 Editor 없는 NTFS A/B 한 묶음에 한정한다.

## 대규모 저장 후보의 구현 소유

새 바이너리/페이지 표현과 자격은 [SCAL](scalable-storage.md)이 소유한다. `PagedMetadataStore`·`PagedSnapshotCatalog`·`PagedPathInterner`·디스크 정렬/cursor가 이름/경로/범위 인덱스를 제공한다. segment 저널은 root/보호를 고정한 뒤 경계 밖에서 결과를 보존한다. 같은 순번의 물리 채택/회수에도 논리 accepted-source seal을 유지하며 native identity/hash/page/inventory 증명을 계속 요구한다.

Core의 선택적 `ISnapshotAvailabilityScope`는 이미 열린 불변 reader에 정확히 결합한 관측을 제공한다. Windows Plastic은 native 디렉터리 identity·쓰기/삭제를 차단한 manifest/catalog 파일 핸들을 고정한 뒤 전체 접수 증명을 수행한다. Heartbeat는 물리 결합을 확인하며 일반 관측·`VerifyReadBoundary`의 전체 manifest 증명은 유지한다. 자격을 가진 scope가 없는 Git/None reader의 기존 검사는 유지한다. Native 결합 비용은 자원 정책 캐시(과거 R19 후보는 32MiB)와 별도다. 진행 중 live-index 복사는 독립 segment 정리보다 먼저 복구하고 이전 순서에서 이미 확정된 변경은 증명한 빈 header만 shadow에서 대조한다. Git 데이터 record 명령은 취소/drain·record 검사를 유지하며 정상적인 무출력 전체 tree 처리를 허용하고 object protocol의 blocking-read 보호는 유지한다. R19 focused 검증은 187행 중 186개 통과이며 actor 목록 1행은 필수 부모가 실행했다. 최종 SCAL 전체 gate는 남아 있다.

Offline overlay v3 재생은 읽기 전용 소스 인덱스를 보존하고 같은 논리 identity·별도 주소 범위의 독립 소유 scratch에서 새 COW 페이지를 해석한다. 소스에 재생 metadata를 append하거나 graph 전체를 복사하지 않는다. Windows는 native pin·정확한 부모에 대한 scratch 디렉터리 원자 생성·extended 경로 native 열기를 제공한다. 접수된 restore cursor는 마지막 reader가 닫힐 때까지 소스·소유 기준본·journal backing·scratch를 보존하며 생성 간격·충돌은 deferred-reclamation 범위 `read-only-replay`에서 조회할 수 있다. Offline v3 `ObserveChanges`는 `SourceObservationUnavailable`로 거부하고 다시 결합할 root는 정확한 소유 live provider에서 얻으며 개발용 overlay v2와 구분한다. R14 focused 실행은 34개 중 33개, R17 결합 focused 실행은 107개 중 106개 통과이며 각각 subprocess 전용 actor 목록 skip 1개였다. 별도 Git 실험 정리는 그 뒤 기존 restore plan/cursor를 재개해 일반 Library·과거 결과 6개를 검증하고 불확실한 전체 인덱스를 소유권 채택·정리 없이 그대로 보존했다. 최종 SCAL gate는 미완료다. 정확한 자격·잔여 할당 범위는 [대규모 저장](scalable-storage.md)을 따른다.

`OwnedContentStore`·`OwnedRangeContentStore`·`ResultFileContentStore`·수집 채택 인덱스가 불변 전체/chunk/slice 내용을 공유한다. 수정 가능한 원본으로 대체하지 않는다. Git commit/tree/ref 구체화는 명시적 branch 반영으로 미루며 일반 원본 파일 게시는 HEAD/index/branch를 유지한다. Windows의 페이지 Library 준비/복원은 아래 작은 입력의 최적화 복원 경로와 구분한다.

조합 파일 descriptor codec v3는 정확한 소유 raw 기준과 보호된 외부 Git 기준을 구분한다. 최초 non-Git generation-basis 게시에서는 파일 전체가 일치하는 raw chunk를 공유할 수 있고 새 private chunk는 전체가 일치하는 기존 whole 객체를 공유할 수 있다. 전체 hash·길이·소유권 증명과 참조 보존을 요구한다. 기존 고정 header를 이관/재지정하거나 부분 일치로 대체하지 않으며 두 pool의 이전 복사본은 남을 수 있다. 경계와 미완료 자격은 [대규모 저장](scalable-storage.md)이 소유한다.

`IWorkspaceMetadataMaintenance`·Runtime `MetadataMaintenance`/`PagedMetadataCollection`·Files `FileMetadataMaintenance`·CLI `WorkspaceMaintenanceCommands`가 workspace/shared/content/range 일곱 범위의 같은 요청 graph 회수를 담당한다. 활성 anchor·보존 세대/검토/진행 root·reader를 삭제 전에 보호한다. 같은 키 복구·native 생성 영수증·보호 잔여 페이지·완료한 과거 요청의 영수증 반환이 계약이다. `workspace read`·`workspace maintain`으로 노출하며 DB/daemon을 추가하지 않는다. metadata 캐시는 `StorageResources`에 따라 프로세스 공통 최대 2.5GiB를 계상하고 전체 대상/입력 native fence·payload/전체 private memory 비용은 별도다. 필수 SCAL gate는 진행 중이다.

Metadata container reader는 캐시된 페이지 hit의 최근 접근 갱신까지 포함한 자원 정책에 따른 명시적 16–64-handle LRU를 사용한다. Scope/container를 직접 찾는 캐시 무효화의 새 partition/list 색인도 자원 정책의 캐시 예산에 계상하며 쓰기를 배제하는 핸들만 페이지 캐시를 허용한다. Live 인덱스 회수는 접수한 cursor가 게시를 막으면 비싼 shadow 준비·재생 전에 유예하고 정확한 미완료 intent·최종 root/reader 보호를 유지한다. 유예나 강제 호출이 회수 완료를 뜻하지 않는다. 이 보호는 R17 focused 실행에서 검증했으며 보존 중인 100,000항목 provider 업그레이드·측정은 남아 있고 실측 속도 개선을 주장하지 않는다.

## 구조와 의존성

[결과 구현 소유 경계](repeated-original-apply.md#구현-소유-경계)가 Core 결과/허가·Runtime 수집/이력/최종 종료·Windows backing alias/native Editor identity·Files 결과·Git 결과/관리 guard를 연결한다. 현재 지속 task 경로가 최종 전용 checkpoint 의미를 대체하며 과거 수집/성능 기록은 당시 후보에 결속한다.

JSON object/array 내부의 중복 속성명 순회는 Runtime의 `JsonPropertyValidation`이 공통으로 소유한다. 이름은 Ordinal로 비교하며 크기·깊이·버전·역직렬화 옵션과 오류 코드는 각 입력/상태 경계에서 유지한다. 개발용 view 응답은 `start`의 Accepted 판단에만 임시 JSON 검사를 사용하며 다른 action의 결과는 최종 응답 출력에서 직렬화한다.

[OS와 앱 확장 경계](extensibility-boundaries.md)는 snapshot identity에 고정한 경로 정책·선택적 Unix 권한 metadata와 앱별 프로젝트 정책을 소유한다. Unity의 원본 잠금/동반 파일/Library descriptor는 Unity 어댑터에 있고 Files·Windows는 명시적으로 주입한 정책을 사용한다. 현재 관리형 basis 상태·준비 intent는 앱/디렉터리 identity·페이지 inventory를 가진 v3이며 이전 basis 상태는 보존·거부한다. 다른 OS·앱의 실제 어댑터는 없으며 자동 적합성과 실제 지원은 [검증](verification/extensibility-boundaries.md)에서 구분한다.

Plastic 내용 reader는 Files에 내장된 고정 client adapter를 일반 사용자 PowerShell 자식에서 실행하고 private stdio frame만 읽는다. 공개 API listener나 새로운 CLI 명령은 제공하지 않는다. Windows `cm 11.0.16.10484`·PowerShell 7 전제와 helper의 bounded segment는 [현재 계약](plastic-project-basis.md), 자동/한정 native 읽기는 [보안 경계 검증](verification/security-boundaries.md)을 따른다. 작업공간의 저장 격리는 악성 Git hook·Unity 코드·인증된 MCP 코드 실행에 대한 OS 보안 sandbox가 아니다.

[VCS 없는 프로젝트 기준본](plain-project-basis.md)은 Windows `ManagedDirectoryBasis(projectBasis:true)`의 원본 prepare/restore와 `ProjectDirectorySnapshotProvider`의 task-owned reference를 사용한다. 공통 `IProjectSourceAccess`는 원본 읽기/게시 동안 정확한 기준·mount·필요한 Editor 종료 lease를 보유한다. Files는 기존 독립 결과/3자 반영을 재사용하고 Windows publisher는 검증된 원본 view만 제한적으로 허용한다. task 회수는 peer 공통 payload를 삭제하지 않으며 마지막 restore가 기준 수명을 소유한다. Core에 Win32/Unity API를 추가하지 않는다.

Library/프로젝트의 공통 restore는 기존 파일 identity·신규 생성 영수증과 Windows 핸들 보호를 사용한다. 일반 파일별 계획은 v2, 변경 없는 디렉터리 계획은 v2, sparse·root-delta·topology 계획은 각각 v1이다. 새 root-delta/topology 경로는 SJL4 또는 bounded SJR2인 원본 Unity Library에만 한정하며 전체 프로젝트·append SJR2는 일반 경로를 유지한다. 기존 계획을 먼저 재개하고, 새 요청은 unchanged → sparse → root-delta → 적격이면서 선호 조건을 만족하는 topology → general 순서다. Topology의 신규 선택만 보존 이동 수와 제어 게시 수를 일반 경로의 공통 128-file cursor 예산과 비교하며 기존 intent 복구에는 비용 조건을 다시 부과하지 않는다. Runtime의 저널 자식 색인은 생성/이동/삭제/재생에서 갱신하며 디렉터리 조회의 전체 경로 검색을 피한다. Git의 `GitProgressReadStream`은 정확한 소유 reader의 blocking read만 30초로 제한하며 진행 중인 전체 object 검증을 중단하지 않는다. 오류의 응답/복구 분류는 Core `WorkspaceFailurePolicy`가 공통으로 소유한다. 각 계약·자동/실제 자격은 기능별 문서를 따른다.

MX05의 root 이동·private 범위 반영은 전체 identity/내용/구성원 증명과 최종 metadata·소유 회수를 유지한다. Topology의 별도 원본/receipt forest map을 고정한 뒤 최대 8개 hash worker를 실행하고 실패·취소도 join한 뒤 핸들을 닫는다. Exact-parent native 새 파일/디렉터리 생성과 topology 자식 stage/install과 현재 타입/경로 검사는 속성 쓰기만의 reparse 전환에 대한 containment를 제공하며 모든 reparse 전환을 막는다는 주장은 하지 않는다. 새 파일의 identity 영수증 전 중단은 보존·거부하고, canonical stem의 plain `.GUID.tmp`는 무시·보존할 뿐 권한으로 채택하거나 삭제하지 않는다. [Library 계약](git-library-reference.md)·[MX05 검증](verification/remaining-lifecycle-optimization.md)이 상세 경계와 후보별 근거를 소유하며 최종 A와 guard 적용 B2는 일반 NTFS 구성 요소 비교·전체 gate 뒤 채택했다. 실제 Unity 전체 비교는 별도 후속이다.

| 프로젝트 | 현재 책임 | 참조 |
| --- | --- | --- |
| `Splinter.Core` | workspace/storage/source/application/process ports·소유권·결과·실패 계약 | .NET 기본 라이브러리 |
| `Splinter.Protocol` | 응답 DTO·제품/개발 명령 카탈로그·내장 JSON Schema | Core, JsonSchema.Net |
| `Splinter.Host` | 읽기 전용 발견/진단 | Core, Protocol |
| `Splinter.Runtime` | 작업공간 수명·atomic JSON 상태/잠금·범위 저널과 후보/cache 수명 | Core |
| `Splinter.SourceControl.Files` | 일반 폴더/Plastic 파일 기준·독립 payload·Plastic 읽기 전용 호환 경계·고정 소스 라우팅·폐기 영수증 | Core, Runtime |
| `Splinter.SourceControl.Git` | fixed commit/tree/blob reader·소유 기준 ref/fence·순차 페이지 소스 해석·보존 파일/범위 결과·필요 시 명시적 branch 구체화 | Core, Runtime |
| `Splinter.Application.Unity` | 정확한 버전/프로젝트의 Editor 실행·준비 관측·정상 종료·소유 bridge | Core |
| `Splinter.Platform.Windows` | Git/Plastic/project 불변 reader 조합·WinFsp·변경 범위 저널·Job Object | Core, Runtime, Files, Git, WinFsp.Net; Win32 |
| `Splinter.Workflows.Windows` | 개발용 불변 source view·공통 Library cache·독립 결과와 회수 의존성 | Runtime와 Git/Unity/Windows |
| `Splinter.Cli` | 제품 명령 파싱·어댑터 조합·JSON/종료 코드; 개발 진입점의 공통 조합 | Host, Runtime, Git/Unity/Windows/workflow, System.CommandLine |
| `tools/Splinter.Development` | 별도 개발 명령 진입점 `splinter-dev` | CLI |
| `unity/Splinter.Editor` | liveness·import/compile 진행 관측·미저장 씬/Play 보호와 정상 종료 | Unity Editor API; 소유 workspace에만 포함 |
| `Splinter.Tests`, `tests/Splinter.UnityBridgeHarness` | 계약/fake·실제 Git·Windows 자식·bridge 소스 적합성 | xUnit v3, MTP; bridge에는 Unity API stub 사용 |
| `tools/Splinter.ReplayProbe`, `tools/Splinter.WriteProbe` | 고정 합성 입력의 읽기/쓰기 비용과 정확성 계측 | Runtime; 개발 도구 |
| `tools/Splinter.BoundedLogProbe` | 제한 범위 로그 [실험](plans/structural-throughput-prototypes.md)과 [제품 저널 계측](verification/bounded-journal.md). 폐기한 Bee 도구는 제거했으며 코드는 `93a7395`에 보존 | Runtime; 개발 도구 |

CLI는 짧은 프로세스다. Host는 읽기 전용 in-process 계층이며 daemon/DB writer가 아니다. Runtime은 부작용 전 intent와 같은 요청 영수증을 durable 상태에 게시한다. 공급자 전용 API는 어댑터에 있고 Core에는 Unity/Win32/Git/Codex API를 넣지 않는다.

다음 행동의 명령 ID와 인자는 Runtime의 `WorkspaceActions`가 함께 구성하고 CLI가 응답에 투영한다. 사람이 읽는 명령 문자열을 다시 파싱하지 않는다. 같은 ensure의 project/key/base/storage/source/application·Library 또는 project basis, 원본 반영의 target/intent/digest/선택 경로를 유지하며 CLI 응답과 실패 복구가 같은 ensure 인자를 사용한다. 기존 schema와 durable 상태 형식은 그대로다. 변경한 검색·객체 검증·압축 순회의 자동 회귀와 측정 한계는 [최적화 검증](verification/code-quality-optimization.md)을 따른다.

Runtime의 파일 잠금은 workspace ID별 명령을 직렬화하며 프로젝트/공유 기준/snapshot 잠금은 필요한 기존 부작용 경계를 보호한다. 확인된 sharing/lock violation만 한정 대기한다. 잠금 경로·접근·다른 I/O 실패는 `StateReadUnavailable`로 즉시 거부하며 점유나 자동 복구로 해석하지 않는다. 현재 동시 CLI/파일 I/O의 한정 자격은 [SP 검증](verification/stable-parallel-completion.md)을 따른다.

[읽기 전용 저장 장부](source-verification.md#계측의-의미)는 현재 SJR2의 stored/decoded·논리 유효 범위·참조 블록 전체 비용과 정확한 task/shared 파일 할당을 구별한다. 물리 저장 개발의 종료 경계와 최종 후보 자격은 [PS 범위](plans/physical-storage-completion.md)·[실행 검증](verification/physical-storage-completion.md)을 따른다. 제품 replay·seal과 기준본 소유권/회수 계약을 계측기로 대체하지 않는다.

[TP-01–06](verification/unity-throughput-optimization.md)은 source manifest v3의 경로별 Git 해석·검증한 불변 내용의 최초 1회 허용·제한된 capsule 준비 및 private 읽기 snapshot을 추가한다. 전체 내용 증명·개별 durable 쓰기 승인·독립 결과 seal은 유지한다. flush나 응답 게시가 실패하면 mount된 provider를 계속 유지한다. 실험한 Windows kernel cache 유지 경로는 실제 Unity의 정상 회수에 실패하여 철회했으며 cleanup purge를 유지한다. 구성 요소 검증은 단일 공유 Editor 대비 전체 시간상 이점을 입증하지 않는다.

workspace 상태와 Windows 제어 IPC는 같은 디렉터리에 flush한 임시 파일을 `File.Replace` 또는 최초 덮어쓰기 없는 move로 게시한다. reader의 delete sharing, 한정 공유 오류 재시도, unknown/손상/소유권 오류 보존은 [NTFS 계약](ntfs-workspaces.md)을 따른다. 상태 쓰기 실패 단계/HResult·게시 완료 여부는 [진단 계약](verification/state-write-diagnostics.md)에 있다.

[Raw 쓰기 계약](raw-write-batching.md)은 제품 압축 writer를 모두 폐기하고 기존 현재 형식 payload의 검증 reader를 유지한다. 해당 RW 후보는 overlay v2/SJR2/SJL4·개발 candidate v3를 유지했다. 현재 SCAL 제품 overlay v3는 SJS1 segment raw frame·페이지 metadata를 사용하며 이전 형식과 구분한다. `IWorkspaceWriteBatch`는 크기가 제한된 순서 보장 SJR2 쓰기에 flush를 공유하고 영속 완료 뒤 성공한다. WinFsp는 mutation gate 전에 대기 쓰기를 모은다. 단일 쓰기와 SJL4 capsule 게시 경계는 유지한다. Checksum·checkpoint 증명·source/result/unmount seal·최초 전체 복제/읽기 hydration/전체 파일 copy-up 금지·원본/peer 보호를 유지한다. 현재 변경은 [RW 검증](verification/raw-write-batching.md)이 소유하며 JP/ME/LJ는 이전 한정 근거다.

[WP 검증](verification/write-path-throughput.md)은 이미 영속화한 같은 핸들의 변경 없음이 증명된 명시적 SJR2 flush 재사용과 payload 읽기 없는 raw 상태 크기 계산을 소유한다. 실제 변경·접수된 쓰기 drain·신규/교체 핸들·전체 checkpoint 증명·SJL4 게시 경계는 유지한다. 덮어쓰기 양끝 탐색 후보는 전체 시간 기준을 충족하지 못해 제거했다.

private Mutation의 기본값 JSON은 생략하고 명시적 정리/후보에서는 더 작은 현재 메타데이터를 선택한다. 입력/응답·SJR2 의미를 바꾸거나 저장된 저널을 읽기 중 변환하지 않는다. 실제 Unity 저널의 오프라인 파일 할당과 새 후보 native 실행의 구분은 [JM 검증](verification/journal-metadata-storage.md)을 따른다.

[Checkpoint/release 최적화](verification/checkpoint-release-optimization.md)는 안정된 seal 안에서 Git checkpoint/보존을 결합하고 SJL4 inventory 검색을 인덱스로 바꾸며 새 회수의 seal 검증을 통합한다. 실행 중 writer 계수는 실제 파일 감사와 구분해 표시하고 poisoned 계수는 provider를 종료하지 않고 unknown으로 남긴다. 요청 파일 알림으로 provider를 깨우되 1초 heartbeat polling과 요청 identity 검사는 유지한다. 기존 독립 결과·내용 hash·소유권·중단 복구 검사를 유지한다.

소유 process supervisor와 PID/시작 시각/토큰·자식 자원 0, 실제 mount/Git/delta·snapshot 회수 관측은 제품 핵심이다. [명시적 discard](workspace-discard.md)는 별도 durable intent/authorization을 사용한다. `release`는 독립 결과를 남기며 shared/reclaim-pending·unknown을 회수 완료로 표시하지 않는다. 열린 개발용 candidate/source/cache도 snapshot 의존성으로 계속 보호한다.

workspace 상태는 저장·소스·소유 자원·독립 결과와 반영/회수 intent를 기록한다. view ledger는 후보 게시·소스 세대·cache 의존성과 요청별 receipt를 기록한다. 지원하지 않는 필드·형식과 손상된 상태는 보존하고 명시적으로 거부한다. 컴파일·Play/Test/build와 게임 합격 판단은 에이전트와 외부 도구가 수행한다.

개발용 저장 계약은 Core의 `ViewWorkflow*`·`ImmutableCandidate`·`SourceViewSelection`, Runtime의 `ViewWorkflowCoordinator`·`SourceViewDeltaStore`·`CacheSnapshotTree`, Windows의 `ViewLifecycle`·`WindowsViewProvider`, Git의 `GitSourceResult`가 소유한다. 현재 schema와 회귀 근거는 [저장 view 계약 검증](verification/storage-view-contract.md)을 따른다.

## 에이전트용 명령 계약

[작업공간 생성 처리량](verification/workspace-creation-throughput.md)은 Git 전체 blob proof마다 최대 128MiB의 앞쪽 window를 공통 256MiB/4,096-chunk RAM LRU 안에 보관한다. 최초 index attach는 큰 파일의 첫 바이트 읽기 하나를 필수 index refresh와 겹치고 성공·실패·취소에서 양쪽 종료를 기다린다. 전체 object/chunk 증명·두 decoder 임대·index lock 보호·복구 시 refresh를 유지한다. 기존 index와 변경한 candidate에는 이 힌트를 사용하지 않는다. 명령·저장 형식은 바뀌지 않으며 Editor 없는 생성 자격은 앞선 LU-01 전체 Unity 수명주기와 별개다.

[단일 작업공간 수명주기 처리량 변경](verification/lifecycle-throughput.md)은 관리형 Library capture/admission·변경 없는 디렉터리 복원에 고정한 조상 증명·최대 8개 worker·전체 내용/identity 검증·실패 drain을 사용한다. 절감 폭이 작은 저널 경합 개선은 제외했으며 저널 게시·checkpoint·seal·close는 기존 동작을 유지한다. 새 명령·schema·저장 형식 버전은 없다. 별도 [LU-01](verification/unity-single-lifecycle.md)의 동일 입력 실제 Unity 폐기 수명주기는 359.5 → 300.0초이며 준비·복원에서 일관된 50.6초 절감과 시작 편차를 구분한다. 양쪽 공통 bounded 폐기 경로 수정은 모드별 inventory·namespace 증명·같은 intent 복구를 유지한다.

개발 checkout의 실행 접두사는 `pwsh ./scripts/splinter.ps1`이다. stdout은 JSON 응답 하나이며 acceptance·성공·회수 대기·실패를 구분한다. 첫 사용 전에 `schema <command>`를 읽고 반환된 `data.workspace.path`를 편집/외부 도구의 기준으로 사용한다.

| 제품 명령 | 책임 |
| --- | --- |
| `capabilities`, `schema`, `doctor` | 현재 제품 계약 발견·입력/출력·읽기 전용 진단 |
| `workspace ensure/list/status` | NTFS 생성/같은 요청 재개·저장 목록·소유 자원 관측 |
| `workspace status --verify-source/--audit-source` | 현재 내용 hash 확인/전체 감사 |
| `workspace checkpoint` | 소유 Editor 정상 stop·안정된 소스 checkpoint·독립 결과. 컴파일/테스트 성공 불필요 |
| `workspace integrate` | Git의 정확한 결과·깨끗한 branch merge 또는 일반 폴더/recorded Plastic의 `--target original --plan`/고정 intent·digest·선택 경로 조건부 NTFS 게시 |
| `workspace compact` | 정상 stop/unmount·seal·명시적 예산으로 private 저널 정리. [최적화 ON](latest-only-journal.md)은 이후 현재본만 저장·선택 유지, 새 일반 Windows 작업은 bounded 기본·옵션 생략은 저장된 모드 유지·명시적 true/false는 ON/append 선택. 명시적 compact의 [기준본 복귀](journal-base-reversion.md)도 포함 |
| `workspace release/discard` | 독립 결과 보존 뒤 회수/명시적 결과 폐기. peer와 공유 의존성 보호 |
| `library prepare/status/restore` | 명시적으로 허용된 원본 Library의 같은 볼륨 이동·원본/A/B 범위 저장·마지막 일반 NTFS 복원. [계약](git-library-reference.md) |
| `project prepare/status/restore` | 명시적으로 허용된 VCS 없는 전체 원본의 같은 NTFS 볼륨 이동·source/Library 공통 기준·원본/A/B 범위 저장·마지막 일반 NTFS 복원. [계약](plain-project-basis.md) |
| `unity start/stop` | 해당 workspace Editor의 수명. 준비/종료는 상태 관측으로 확인 |

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

Windows 전용 파일 공유·native 계약 시험은 다른 OS에서 Skipped다. 단독 고정 응답·오류 매핑·계측 시험이 같은 동작을 반복하면 실제 생성·재개·거부·재생 시험에 고유 assertion을 합친다. 변경 없는 중간 부모 아래의 rename·요청 digest·단일/공유 flush 뒤 영속 ACK·원본/독립 결과 보호처럼 회귀를 막는 검증은 함께 유지한다.

### 실제 저장소의 Git 수명주기 시험

`pwsh ./scripts/test-worktree-lifecycle.ps1 -Repository <경로>`는 명시한 저장소의 worktree 등록을 수정하는 개발 시험이다. Unity/CoW 자격과 구별하며 기존 작업·원본·refs를 보존한다.

## 다음 구현 경계

[구현 계획](plans/implementation-plan.md)을 따른다. VCS 중립 계약·일반 폴더의 기본 수명주기·기존 Git 사용 환경의 보호 경계는 작은 A/B 한 묶음의 제한을 명시해 마감했다. [일반 폴더의 안전한 원본 반영](plain-folder-original-apply.md)의 구현·자동 파일 시험과 작은 Editor 없는 제품 A/B 한정 마감을 구분한다. [Plastic 호환 계약](plastic-compatibility.md)은 별도로 연결하고 작은 T: A/B의 마지막 회수까지 한정 마감했다. 선행 C: 회수 보류·외부 CLI 발견 실패는 [PC 검증](verification/plastic-compatibility.md)을 따른다. recorded Plastic의 [전체/선택 원본 반영](plastic-original-apply.md)은 [별도 한정 자격](verification/plastic-original-apply.md)을 따른다. 외부 도구의 전체 인계 자격은 필요한 입력에서 별도로 확인한다. 저장/메모리는 실제 관측 병목만 고른다. 마감한 Git 기반 M0·대형 PV·전체 장애 카탈로그를 자동 재개하지 않는다.

Plastic의 `plastic-snapshot`은 `Splinter.SourceControl.Files`의 고정 revision reader와 기존 Windows overlay/공통 Library dependency를 연결한다. 현재 계약과 자격은 [프로젝트 기준본](plastic-project-basis.md)·[검증](verification/plastic-project-basis.md)을 따른다. 원본 반영은 위의 별도 계약을 따르며 새 Plastic 등록/check-in·서버 pin은 제공하지 않는다.

[VSS 폐기 결정](plans/vss-retirement.md): 서비스·IPC·전용 provider/스크립트/schema를 제거했다. 공통 경로·저널·결과·소유 수명은 유지한다.
