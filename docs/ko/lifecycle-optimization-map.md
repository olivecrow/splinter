# 최적화 에이전트를 위한 Splinter 수명주기 지도

[English](../lifecycle-optimization-map.md) · [기계 판독 색인](../lifecycle-optimization-map.json) · [문서 지도](README.md)

**코드 기준:** 기준 `aff285f0da769eeaba0300d448f027ceeaa4a5bb` 이후 커밋 `b51f18fb6d4515a149975bbe6b3bf334804e1217`의 반복 원본 반영 소스, `development`, 2026-10-30 확인. JSON이 현재 소스 hash/symbol을 기록한다. 현재 실행 중 수집·반복 게시의 자격은 [RI](verification/repeated-original-apply.md)가 소유한다. 기존 MX05/MX08A 채택·완료한 MX10 및 BO/PR/RT/BD 성능은 정확한 과거 후보의 근거이며 새 수명주기의 자격으로 승계하지 않는다.

**AI 읽기 순서:** §§1–3에서 identity·경로를 파악하고, §4에서 단계별 동작, §§5–7에서 직렬화·비용·복구, §8에서 실제 코드를 확인한다. 계약 수정 전에는 연결된 기능 소유 문서를 읽는다. 명령 사용 절차 원본은 [Splinter 스킬](../../skills/splinter/SKILL.md)이다.

## 1. 흐름을 결정하는 규칙

- Splinter는 쓰기 가능한 격리 프로젝트, 고정 기준본, 변경 범위 저장, 소유 Editor 수명, 독립 결과, 명시적 반영·회수를 제공한다. 코드 편집·refresh·컴파일·Play/Test/build·판단은 에이전트와 선택한 설치 도구가 수행한다.
- `ready`/heartbeat는 실행 준비 관측이다. 컴파일·동작 성공 증명이 아니다. `workspace checkpoint`는 소스를 보존하며, 컴파일 실패 자체가 보존을 금지하지 않는다.
- 같은 작업은 같은 `project/key/id/home/base/requestDigest/storage/source/application/basis-id`로 재개한다. 대화 변경·Editor 종료·타임아웃은 새 작업 생성이나 삭제 승인이 아니다.
- 최초 프로젝트 전체 복제·읽기 시 디스크 hydration·파일 전체 copy-up은 금지한다. **명시적 독립 결과 보존과 원본 파일 게시**에는 별도 payload 복사 비용이 있다.
- 새 일반 Windows provider 기본값은 raw **bounded SJR2**다. 기존 저장된 SJL4/append 선택은 유지한다. `JournalDeltaStore` 생성자의 단독 기본값과 provider 기본값을 혼동하지 않는다.
- **B10 내부 저널 checkpoint**, **C20 작업공간 결과 checkpoint**, **R10 작업공간 release**, **F10 원본 Library/project restore**는 트리거·출력·비용이 다른 작업이다.
- `released`/`discarded`는 해당 작업 자원의 종료다. 보존 결과 삭제·이미 반영한 원본의 되돌림·공유 기준본의 복원까지 뜻하지 않는다.

## 2. 작업공간 하나의 제어 흐름

```mermaid
flowchart TD
  S00["S00 직접 작업 / 격리 작업 선택"] --> Need{"격리가 필요한가?"}
  Need -->|아니오| Direct["기존 프로젝트 편집; Splinter 수명주기 없음"]
  Need -->|예| P10["P10 필요한 공유 기준본 준비 또는 정확한 기존 기준본 재사용"]
  P10 --> P20["P20 작업 요청 영속화 / 작업 잠금"]
  P20 --> P30["P30 소스 사전 검사 / 기준본 capture / 등록"]
  P30 --> P40["P40 기준 증명 / 저널 replay / mount"]
  P40 --> P50["P50 소스 attach / 깨끗한 baseline 증명 / ready"]
  P50 --> E10["E10 선택적 소유 Editor 시작"]
  E10 --> E20["E20 에이전트 편집 / refresh / 컴파일 / 검증"]
  E20 -->|같은 작업 지속| E20
  E20 --> Finish{"명시적 종료 의도"}
  Finish -->|최종 보존 또는 반영| C10["C10 소유 Editor 정상 종료 / 자식 자원 drain"]
  C10 --> C20["C20 고정된 독립 결과 버전"]
  E20 -->|저장; 중간 수집| C20
  C20 --> Apply{"반영 요청?"}
  Apply -->|Git 브랜치| A10["A10 원본 Git merge / guard"]
  Apply -->|Git·None·Plastic 원본 파일| A20["A20 고정 원본 변경 계획 / 검토"]
  A20 --> A30["A30 조건부 파일 게시 / 영수증"]
  Apply -->|보존만| R10["R10 소유 작업공간 자원 release"]
  A10 --> R10
  A30 -->|최종 회수 요청| R10
  A30 -->|중간; 외부 refresh/재로드| E20
  C20 -->|보존 후 지속| E20
  Finish -->|폐기| D10["D10 정확한 폐기 intent / 종료 / seal / 회수"]
  R10 --> F10["F10 모든 dependent 종료 뒤 명시적 공유 기준본 복원"]
  D10 --> F10
```

Editor 없는 작업은 `P50 → E20`으로 이동한다. E20 안에서 Editor 종료·재시작과 C20 반복 수집이 가능하다. 중간 C20은 C10·종료 seal을 생략하며 최종 종료는 이를 유지한다. 반영이 필요 없는 `checkpoint --release`는 같은 작업 명령에서 C10/C20/R10을 잇는다. 반영 후 회수는 checkpoint → integrate → release다. 관리형 공유 기준본이 없으면 F10은 생략하며, peer가 사용 중이면 F10만 남겨 둔다.

## 3. 데이터와 소유 관계

```mermaid
flowchart LR
  Git["고정 Git commit/tree/blob + 소유 ref"] --> Basis["불변 소스 reader"]
  Plastic["고정 Plastic changeset/revision"] --> Basis
  Project["이동한 관리형 프로젝트 기준본"] --> Basis
  Library["선택적 이동 Library 기준본"] --> Graft["고정 소스 + Library graft"]
  Basis --> Graft
  Graft --> VA["작업 A WinFsp 프로젝트"]
  DA["A private raw 저널"] --> VA
  Graft --> VB["작업 B WinFsp 프로젝트"]
  DB["B private raw 저널"] --> VB
  Library --> Original["원본 관리형 Library view"]
  Project --> OriginalProject["원본 관리형 프로젝트 view"]
  DO["원본 private 저널"] --> Original
  DO --> OriginalProject
  VA --> Result["명시적 독립 소스 결과"]
  Result --> Apply["명시적 원본 반영"]
```

원본 view의 두 연결은 Library/전체 프로젝트라는 **대안 구성**이다. 두 구성이 한 저널을 공유한다는 뜻이 아니다. 작업들은 고정 기준본을 읽으며 이후 원본 수정이 baseline에 자동 추가되지 않는다. 일반 Git/Plastic 원본 소스는 기존 경로를 유지한다. 관리형 전체 프로젝트는 승인된 writable view가 기존 원본 경로를 대신한다. 작업 Library 변경은 private이며 소스 결과에서 제외된다. 통합으로 원본 Library에 합쳐지지 않는다.

| 입력 경로 | 고정 소스 | 생성 데이터 | 반영 경로 | 최종 공통 정리 |
| --- | --- | --- | --- | --- |
| `git-snapshot`, `source=git` | 정확한 local commit/tree/blob, 최초 clean root/HEAD, 소유 기준 ref | 명시적 `library prepare` / `--library-basis` 선택; 없으면 생성 파일을 private 저장 | 원본이 요청 브랜치를 이미 checkout했고 clean일 때 Git merge | 작업 기준/ref·선택적 Library 의존성 해제; 미사용 Library 명시적 복원 |
| `plastic-snapshot`, `source=plastic` | 명시적 recorded changeset, regular/static 원본 identity, private revision reader | 같은 선택적 Library 수명주기 | `target=original`, 검토한 plan + intent/digest, 선택적 정확한 경로 | 작업 receipt/reader·Library 의존성 해제; 서버 check-in·등록 변경 없음 |
| `project-snapshot`, `source=none` | 필수 사전 준비 프로젝트 기준본; 같은 볼륨에서 복사 없이 이동 | 관리형 프로젝트 안에 포함; 별도 Library graft 없음 | `target=original`, 검토한 plan + intent/digest, 선택적 정확한 경로 | 작업은 자기 의존성만 해제; 명시적 project restore가 공통 payload 회수 |

`application=none`에는 Editor 단계가 없다. `application=unity`는 생성/bridge 경로와 Unity 정책을 제공한다. 폐기된 VSS/ReFS와 개발용 `git-checkout`은 이 제품 지도 범위가 아니다.

```text
<home>/locks/                                  명령 잠금
<home>/records/<task-id>/workspace.json         요청·phase·기준·결과·intent·관측
<home>/records/<task-id>/snapshot/              기준 receipt / read-access identity / 소스 등록 근거
<home>/records/<task-id>/overlay/               provider 요청·launch·상태·제어·진단 파일
<home>/records/<task-id>/delta/                 identity·raw 저널·checkpoint/회수 근거
<home>/records/<task-id>/files/                 None/Plastic manifest와 독립 내용 객체
<home>/records/<task-id>/{launch-*,process-*,heartbeat.json,editor.log,...}
<home>/workspaces/<task-id>/                    반환 writable mount; 최초 전체 복사본 아님
<home>/{library-bases,project-bases}/<basis-id>/ 상태·이동 base·manifest/proof·원본 delta/mount,
                                               dependents·reader lock·복원 plan/cursor/receipt
<home>/runners/                                소유 provider/process helper 바이너리
<original>/.git/                               Git 객체·작업 브랜치·기준/결과 ref·linked 등록
```

이 개요로 삭제 경로를 합성하지 말고 실제 반환 경로를 사용한다. 작업 payload 회수 뒤에도 record·evidence·결과 ref/객체·완료 영수증은 보통 남는다.

## 4. 단계별 계약

<a id="s00"></a>
### S00 — 선택과 재개

에이전트 책임이며 작업 선택 시 수행한다. 현재 capabilities/schema와 저장된 작업 identity를 읽는다. 요청되었거나 독립 실행이 필요할 때 격리하고 매 편집마다 새 작업을 만들지 않는다. `workspace list`는 저장된 목록이며 live 소유권·Editor idle 증명이 아니다. `workspace status`는 실행 상태를 관측하고, 소스 verify/audit는 명시적 추가 작업이다. [시작 안내](agent-start.md), [세션 흐름](plans/session-workflow-and-installation.md)을 따른다.

<a id="p10"></a>
### P10 — 필요한 공유 Library/project 기준본 준비

진입점: `library prepare` / `project prepare` → `ManagedDirectoryBasis.PrepareAsync` [K04]. `workspace ensure`와 별개의 명시적 원본 전환이다.

1. 기준 잠금 아래 project/key/application/compatibility/id를 고정한다. 같은 기존 NTFS 볼륨·분리된 home·지원하는 일반 경로를 요구한다. 전체 프로젝트 준비는 VCS 관리 영역도 거부한다.
2. 정확한 원본 Editor를 저장·정상 종료한다. 제품 정책은 종료/lock 조건을 검사하며 다른 작업 Editor를 자동 조작하지 않는다.
3. 준비 intent를 게시한다. 내용·메타데이터를 열거/hash하고 manifest/proof·reader/dependent 보호를 만든다. 적격 Git+Unity Library 준비는 원본 소스 bytes와 Git blob을 대조하고 일치한 timestamp도 고정한다.
4. 원본 Library/소스 디렉터리를 소유 기준 경로로 이동한다. 전체 payload 복사가 아니다. 중단된 이동은 같은 intent로 조정하며 양쪽 경로가 있으면 추측하지 않는다.
5. 이동한 내용을 증명/admit하고 원본 private 저널과 writable 원본 view를 열어 `ready`를 저장한다.

비용은 전체 입력 scan/hash·파일별 handle/metadata·증명 게시·원본 provider 시작이다. capture/admission/변경 없는 복원에 최대 8개 파일 worker를 사용한다. rename만이 준비 비용은 아니다. 다른 작업은 정확한 ready 기준을 재사용하지만 의존성/admission 비용은 남는다. [Library](git-library-reference.md), [관리형 프로젝트](plain-project-basis.md)가 계약을 소유한다.

<a id="p20"></a>
### P20 — 작업 요청 영속화

`WorkspaceCoordinator.EnsureAsync` [K01], CLI 조합 [K00]. source/application/storage/base를 정규화하고 home/경로 분리·요청 key를 검사한다. project+key에서 task ID, 고정 매개변수에서 request digest를 만든다. 작업 잠금 아래 v2 `create-intent`를 만들거나 재개의 고정 필드를 대조한다. 다르면 거부하며 released/discarded ID로 새 작업을 만들지 않는다. `--editor`는 같은 요청의 선택적 동작이다. 미완료 원본 반영/폐기는 무관한 수명주기 작업을 막는다.

비용은 local lock·경로 검사·영속 JSON 게시다. 잠금은 이 명령의 범위이며 대화 전체의 외부 파일 편집을 배타적으로 잠그지 않는다. 다른 ID는 작업 잠금을 공유하지 않지만 후속 일부 구간의 프로젝트/기준 잠금은 공유한다.

<a id="p30"></a>
### P30 — 사전 검사, 기준 capture, 소스 등록

`EnsureSnapshotAsync` [K02]는 부수 효과 전후에 `source-checked → capture-intent → captured → registration-intent → registered`를 영속화한다.

1. 프로젝트 잠금 아래 preflight로 base revision과 Git/Plastic/plain-project 전제를 확인한다.
2. provider별 receipt를 만든다. Git은 고정 commit/tree의 소유 reachability ref, Plastic은 recorded revision identity, project는 정확한 공통 기준 의존성을 고정한다. 선택적 Library attach도 dependent receipt를 만든다.
3. 프로젝트 잠금 아래 소스 등록을 준비한다. Git은 payload 전체 checkout 없이 소유 linked Git 관리 영역을 만든다. File/Plastic은 소유 receipt와 관리 root 격리를 유지한다.

Git 객체/catalog 검사·Plastic client/server revision 접근·기준 manifest admission·등록 metadata 등 비용이 다르다. 여기의 snapshot capture라는 이름은 VSS가 아니다. 코드 [K02, K05, K06, K19], 계약 [Git](git-project-basis.md)·[Plastic](plastic-project-basis.md)·[project](plain-project-basis.md).

<a id="p40"></a>
### P40 — 저장소 연결과 replay

`ConnectSnapshotAsync` → `NtfsSnapshotWorkspaceBackend.VerifyConnectionAsync/ConnectAsync` → WinFsp mount/client/server [K02, K05, K07].

1. 정확한 기준/read-access/owner·선택적 Library identity를 검사한다. 같은 read-access의 live provider만 재사용한다. 소유 앱 client가 남아 있으면 사라진 provider를 재연결하지 않는다.
2. token에 결속된 overlay 요청을 영속화하고 정확한 소유 provider를 시작/조정한다. 기준 reader 수명을 확보하고 source+Library graft, 디렉터리/저널 소유권을 확인한다.
3. 저장된 저널 모드를 열고 해당 버전의 중단된 generation을 복구한다. frame 검증/replay로 metadata·범위 index를 재구성한다. 새 일반 provider는 bounded SJR2를 선택한다.
4. **8개 dispatch thread**로 WinFsp를 mount한다. 정확한 provider PID/start/token·mount 경로·기준 가용성을 게시/관측한 뒤 연결 완료를 선언한다.

비용은 process launch·필요한 전체 admission/proof·replay·handle·cache 초기화다. 재사용은 완료가 증명된 효과만 생략한다. 새 snapshot이나 mutable 원본 복사를 통한 복구가 아니다. 소스 status/provider ready 시각은 컴파일 준비 시각과 다르다.

제어 관측 [PR-01](verification/preparation-throughput.md): 일반 intent/receipt 읽기는 게시 유예를 유지하면서 겹치고 모두 drain하며, 없는 state도 대기한다. Start의 배타 `client.lock` 안에서는 경쟁 start를 배제하고 launch 기록을 한 번 관측해 재사용하며 부재 대기를 생략한다. 기존 intent의 자식은 receipt를 나중에 게시할 수 있으므로 그 intent의 receipt 부재는 unknown으로 보존하고 같은 요청으로 재관측한다. 이전 launch가 있으면 state 유예는 유지한다. Project task는 release/discard authorization 전까지만 없는 release receipt의 유예를 생략한다. capture/release·dependent attach는 기존 게시 배제 잠금을 사용한다. 존재하는 기록·공유 오류·소유/세대 검사·미해결 launch 보호는 유지한다.

<a id="p50"></a>
### P50 — attach, baseline 증명, ready 반환

`SnapshotSources.AttachAsync/EstablishBaselineAsync`와 clean 검사가 마지막 단계다 [K02]. Git [K06]은 소유 관리 정보를 연결하고 새 index에 `read-tree`를 수행하며 반드시 refresh한다. **새로운 변경 없는 index**에서만 1MiB 이상 파일의 첫 바이트 선행 읽기를 refresh와 겹친다. 이 읽기는 정상 전체 blob 증명을 유발한다. 실패해도 양쪽을 drain하며 기존 index 재개에는 적용하지 않는다. File/Plastic baseline은 고정 소스 manifest를 scan하고 관리/VCS 경로를 제외한다.

clean 소스 revision이 base와 같아야 `Attached=true`, `ready`, `snapshot-ensured`를 게시한다. 연산 한정 최초 검사 증명을 editorless 응답에 재사용할 수 있지만 영구적인 무변경 증명이 아니다. `ensure --editor`는 E10 뒤 실행 관측을 반환하며 추가 전체 소스 검사를 했다고 주장하지 않는다.

None/recorded Plastic [K19]의 전체 소스 scan은 제한된 목록을 먼저 확정한다. None은 최대 **8 worker**로 hash하며 실패/취소에도 모두 join한다. Plastic mount scan은 단일 revision 증명/stream의 반복 퇴출을 막기 위해 직렬을 유지한다. 최초 scan은 같은 ensure 응답에서 **한 번만** 쓰는 증명을 만들 수 있다. 전체 scan과 metadata-only 재검사 각각의 전후에 완전한 source-change barrier가 같아야 하며 owner/basis/mount/request/policy·목록·metadata·epoch·history도 일치해야 한다. Plastic 등록 격리도 다시 검사한다. tracker 없음/audit는 전체 검사이며 명령이나 provider 재시작을 넘어 증명을 재사용하지 않는다. [PR-02](verification/preparation-throughput.md) 참조.

주 비용은 Git index/stat/hash 순회·최초 전체 객체 decode·소스 manifest scan·metadata 연산이다. 현재 Git reader는 두 decoder 임대, 4MiB/4,096-blob proof, 256MiB/4,096-chunk RAM payload LRU, 최대 128MiB 최초 proof 선행 window를 쓴다. 각각 tree별 제한이며 process 전체 RAM 제한이 아니다. 디스크 hydration은 없다. [WC](verification/workspace-creation-throughput.md) 참조.

BO-04 진단 [K27]은 실행 프로세스에 `SPLINTER_GIT_PREPARATION_DIAGNOSTICS=1`이 있어야 활성화된다. provider의 `gitReads`는 decode/proof bytes·시간, reader 대기, cache miss, 용량 퇴출과 사용 전 read-ahead 퇴출을 기록한다. 진단 attach는 효과 전에 새 attempt ID와 `running`을 `control/git-preparation.json`에 게시하고, 끝나면 `succeeded`/`failed`/`cancelled`와 verification/read-tree/warm/refresh/final-head-status/admission의 겹치는 제한 구간을 최선 노력으로 게시한다. 최초 marker 게시 실패 시 해당 진단 시도를 시작하지 않으며 최종 게시 실패는 완료를 주장하지 않고 `running`을 남긴다. 기본 동작에는 새 파일이 없고 상세 관측은 null이다. 관측은 내용 증명이 아니므로 attempt/task identity와 outcome을 대조한 뒤 해석한다. reader/cache 한도·전체 증명·실패 drain은 유지한다. 추가 reader/window 예산을 고르기 전에 미사용 퇴출과 대기를 계측하며 겹친 counter를 attach 경과 시간에 합산하지 않는다.

<a id="e10"></a>
### E10 — 소유 Editor 시작

`StartCoreAsync` → `UnityWorkspaceProvider.StartAsync` → process supervisor [K01, K08, K09]. live mount와 미수집 결과 상태를 확인하고 관측된 소유 running Editor만 재사용한다. `ProjectVersion.txt`에서 설치 Unity를 선택한다. 명시적 Library 참조의 최초 시작에서만 적격 직속 `Library/Bee/*.dag`를 무효화하고 1회 receipt·나머지 cache는 보존한다. 제품 소유 process-address만 reset한다. 소유 bridge/asmdef를 검증·재사용하거나 기록하고 launch intent/token·PID/start/process-group receipt를 만든다.

Unity가 import·assembly 작업·domain reload·scene/Editor 시작을 수행한다. bridge heartbeat 주기는 0.5초, provider freshness는 15초다. `ready`는 보고된 phase를 요구하지만 오류 없는 컴파일이나 검증 성공 증명은 아니다. 에이전트가 필요한 loaded code/compile 결과를 별도로 확인한다. launch 반환·process 생존·첫 heartbeat·컴파일 완료는 다른 계측 경계다.

BO-05는 별도의 읽기 전용 [시작 근거 수집 스크립트](development-cli.md) [K28]를 제공한다. 에이전트는 editorless ensure 뒤 `before-start`, 준비 완료를 독립 검증한 뒤 회수 전에 `live`를 수집할 수 있다. 정확한 task/mount/provider/Editor identity를 확인하고 선택한 Bee 산출물·로그·민감한 필드를 제거한 provider 관측을 새 근거 디렉터리에 제한 복사한다. 기본 한도는 2,048개 파일, 파일당 32MiB, 총 128MiB이며 초과·파일 변경·소유권 실패 시 성공 marker 없이 부분 근거를 보존한다. 파일별 수집은 원자적 Library snapshot·컴파일 증명·새 Unity 성능 자격이 아니다. before/live 구간은 호출자가 정한 구간이며 자동 시작 stopwatch가 아니므로 수집 비용을 별도 기록한다. 스크립트는 Unity 시작/종료나 profiler 인수 주입을 하지 않는다.

<a id="e20"></a>
### E20 — 코드·Editor 작업 반복

에이전트가 **`data.workspace.path`**를 일반 파일 도구로 편집한다. 설치된 MCP/Unity CLI 등을 필요에 따라 선택하고 정확한 project/PID/start를 대조한다. 필요 시 명시적으로 refresh하고 compile/import/reload 완료와 결과를 확인한다. 포트는 바뀔 수 있고 목록 누락은 Editor 부재가 아니다. 쓰기 timeout을 근거로 자동 재실행하지 않는다.

소스·asset·Library I/O는 아래 I10/W10/B10을 통과한다. 도구 실행·Unity 컴파일/시험·게임 정답 판단은 제품 밖이다. C20 이전에는 같은 task에서 여러 edit/compile·Editor stop/start를 반복할 수 있다. 매 I/O의 내구성을 바꾸지 않으면서 준비/최종화 비용을 여러 작업에 분산한다. 소스 격리가 Unity heap/graphics memory 자동 공유를 뜻하지 않는다.

<a id="i10"></a>
### I10 — 모든 단계 안의 읽기 경로

```mermaid
flowchart TD
  Read["WinFsp Read: 제한된 요청 버퍼"] --> Resolve["저널 gate: 파일 / 범위 index 해석"]
  Resolve --> Private["private extent: raw 또는 현재 encoded payload 검증"]
  Resolve --> Base["미변경 extent: 불변 소스 / Library reader"]
  Resolve --> Hole["hole / truncate 뒤 확장 범위: 0"]
  Base --> GitRead["Git: 최초 전체 proof; 후속 proof/cache/decoder 읽기"]
  Base --> FileRead["소유 디렉터리 / recorded Plastic reader"]
  Private --> Finish["요청 bytes 조합 / 수명·무결성 확인"]
  GitRead --> Finish
  FileRead --> Finish
  Hole --> Finish
```

`JournalDeltaStore.Read` [K10]는 private/base/hole 구간을 해석한다. reader 수명이 고정되고 provider가 concurrent read를 지원할 때만 불변 읽기를 저널 gate 밖에서 수행할 수 있다. 완료 시 poison/길이/기준 상태를 다시 확인하며 close는 진행 중 읽기를 drain한다. private read와 admission/replay에도 각 검증이 있다. cache hit가 필수 identity/proof 생략을 허용하지 않는다. 호출 bytes·전체 객체 decode bytes·실제 디스크 bytes를 구분한다. native/managed 요청 버퍼 제한도 별개다.

<a id="w10"></a>
### W10 — 범위 쓰기, metadata, 명시적 flush

```mermaid
flowchart TD
  Callback["동시 WinFsp Write callback"] --> Queue["mutation gate 앞 FIFO ticket"]
  Queue --> Leader["호출자 하나가 선도; live native buffer에서 제한 chunk 복사"]
  Leader --> Gate["mutation gate + journal gate; 쓰기 순서 유지"]
  Gate --> Compare["전체 private 범위라면 현재 bytes 대조 / 재사용"]
  Compare --> Frame["metadata + raw 변경 envelope + checksum"]
  Frame --> Limit{"suffix 여유?"}
  Limit -->|없음| B10["B10 pending flush / 현재 private 상태 checkpoint"]
  B10 --> Append["frame append / private 메모리 index 갱신"]
  Limit -->|있음| Append
  Append --> Durable["묶음 성공 전에 공유 영속 flush"]
  Durable --> Ack["callback 완료 / native buffer 소유 해제"]
  Explicit["명시적 Flush callback"] --> Drain["접수 ticket drain / local 저널 확인"]
  Drain --> Clean{"같은 handle + 길이가 이미 영속화됐는가?"}
  Clean -->|SJR2 예| Reuse["영속 증명 재사용"]
  Clean -->|아니오 또는 SJL4| Flush["물리 flush"]
```

callback [K11], queue [K12], store [K10], 순서 보장 batch [K13]. 한 batch 호출은 최대 **64개 범위 쓰기 / 16MiB**, provider 전송 chunk는 최대 **1MiB**다. 큰 callback은 여러 batch로 나뉘고 모두 끝난 뒤 완료한다. B10 경계를 넘으면 batch 안에도 추가 물리 barrier가 생기며 SJL4는 쓰기별 barrier를 유지하므로 batch당 물리 flush 한 번을 보장하지 않는다. append/constrained 길이에는 앞선 batch 쓰기를 반영한다. 중단된 waiter도 callback이 끝날 때까지 native buffer를 보존한다. background writer·수집 sleep·조기 ACK는 없다.

완전히 private인 덮어쓰기는 현재 범위를 대조한다. 동일 bytes면 새 payload 없이 metadata/timestamp 변경만 기록한다. 일부만 다르면 양끝 절약량이 합계 **4KiB** 이상일 때 단일 연속 envelope를 저장한다. base/hole/확장은 일반 요청 payload를 사용한다. 폐기한 양방향 끝 탐색 후보는 없다.

frame에는 제한된 직렬화 mutation metadata·raw payload·checksum이 있다. 단일 mutation은 flush 후 메모리 apply/반환한다. batch는 외부 접근이 막힌 gate 아래 순서대로 apply하고 flush 뒤 ACK한다. 실패하면 ACK되지 않은 prefix가 남을 수 있어 불확실한 상태를 poison하고 replay를 요구한다. batch는 rollback transaction이 아니다. create/rename/resize/metadata/delete는 여전히 별도 직렬 연산이며 이 FIFO는 모든 mutation이 아닌 범위 Write callback을 모은다. 채택한 MX08A는 단일 Overwrite의 resize/metadata를 `SetLengthAndMetadata`로 결합하지만 혼합 callback batch 후보 MX08B는 제거했다.

`Flush()`가 매번 현재 상태 전체를 직렬화하는 것은 아니다. SJR2는 local 검사 뒤 **같은 열린 저널의 변경 없는 길이와 성공한 영속 flush**를 재사용할 수 있다. 신규/재연결/교체 handle은 자체 barrier가 필요하다. SJL4 capsule commit/게시·명시적 flush는 별도 경계를 유지한다. [RW/WP](raw-write-batching.md), [bounded 저널](bounded-journal.md) 참조.

BO-03 `writeAdmission` [K26]은 provider 시작 시 `SPLINTER_WRITE_DIAGNOSTICS=1`이 없으면 꺼져 있다. 활성화해도 ingress I/O나 payload 보관 없이 고정 ring에 callback 32개·batch 16개·store call 16개만 보관하며 덮어쓴 표본 수와 경로 256자 한도를 기록한다. 원래 요청/effective/staged/변경 payload bytes, queue/outstanding 최대치, mutation gate 대기, FIFO batch/chunk/분할 이유, drain, 효과 완료와 callback 반환/실패를 구분한다. monotonic ticks/frequency로 구간을 저비용 Runtime flush/commit 차분과 연결한다. store-call 구간에는 동시 관리 flush가 포함될 수 있고 제한된 최근 표본으로 모든 과거 callback을 복원할 수는 없다. 순서·영속성·poison·drain·native buffer 수명은 그대로이며 진단이 background writer를 만들거나 순차 caller를 묶을 수 있게 하지는 않는다.

<a id="b10"></a>
### B10 — 내부 bounded 저널 checkpoint

다음 frame이 **16MiB suffix / 4,096-frame** 또는 남은 전체 저널 예산을 넘으면 mutation 안에서 자동 실행한다 [K14, K15]. 작업 전체 저널 기본 예산은 **16GiB**다. suffix 제한은 현재 상태 prefix·임시 generation·전체 RAM 제한이 아니다.

1. pending batch를 flush한다. live range와 raw candidate 크기를 길이로 계산한다. 크기 계산용 payload 읽기/할당은 없다.
2. 전체 저널 예산과 candidate 크기 외에 **2GiB** 디스크 여유를 확인한다. 현재 저널 seal·논리 상태 증명을 만들고 generation intent를 영속 게시한다.
3. 현재 private 상태 전체 prefix와 checksum을 candidate에 쓰고 flush한다. candidate를 reopen/replay하여 논리 상태를 대조한다. 원본 generation을 다시 검사하고 prepared intent를 게시한다.
4. 이전 generation을 보존한 채 current를 교체한다. 게시 상태를 reopen/replay/재증명하고 index/counter를 갱신한다. 정확한 이전 generation/intent를 검증·회수한다.
5. 트리거 mutation과 정상 영속 ACK를 이어 간다.

store 연산 안의 동기 작업이므로 단일 write latency가 튈 수 있다. append suffix가 작아도 전체 private 상태 복사·증명이 지배할 수 있다. Unity 종료·소스 결과 생성·작업 삭제 승인이 아니다. 현재 형식 encoded 입력을 읽을 수 있지만 새 상태 출력은 raw다. 증분 상태 저장은 아직 구현하지 않았으며 형식/복구 설계가 필요하다.

<a id="c10"></a>
### C10 — 소유 Editor 정상 종료와 자원 drain

최종 checkpoint/release/discard가 필요에 따라 호출하며 명시적 `unity stop`도 있다 [K08, K09, K20]. token/request에 결속된 정상 종료 요청을 게시한다. bridge는 compile/import 중 기다리고 Play·dirty scene이면 거부한다. 허용될 때만 `EditorApplication.Exit(0)`을 호출한다. 자동 저장이나 외부 도구 관리는 하지 않는다.

호출자는 최대 **2분**, **250ms** 간격으로 기다리며 정확한 process group/자식의 종료도 요구한다. timeout/거부는 상태 보존이며 강제 종료·삭제 승인이 아니다. 최종 소스 수집에는 ownership이 검증된 **active process·Working Set·Private Bytes 모두 0**이 필요하다. Editor PID 부재만으로 충분하지 않다. 최종 종료는 정상 정지 후 안정된 seal 구간 전에 검증된 소유 bridge만 제거한다.

<a id="c20"></a>
### C20 — 작업을 지속하는 불변 소스 결과

`workspace checkpoint --request-key <key>` [K32–K34]는 task 잠금 아래 key/message별 결과 하나를 고정하고 Editor를 유지한다. 경로·ID·기준본·Library를 유지하며 이후 편집·재시작을 허용한다. 이전 키는 최초 결과를 검증·반환하며 미완료 수집/반영은 새 요청 전에 복구한다.

callback이 접수된 쓰기를 drain하고 mutation gate에서 영속화한 뒤 소스 namespace·metadata·범위 참조·backing identity를 고정한다. 보호 intent를 먼저 영속화하고 내부 NTFS alias를 만든다. 이 경계에서 basis scan·payload hash·전체 프로젝트 복사·전체 payload RAM 적재는 하지 않는다. 복구는 writer replay/회수 전에 보호를 복원한다. bounded 교체와 SJL4 retained-prefix truncate는 alias를 보존하며 영수증에 결합된 private-prefix 복사는 소스 copy-up과 구분한다.

```mermaid
flowchart TD
  Intent["Runtime: 정확한 수집 요청 영속화"] --> Freeze["callback/mutation gate: flush; metadata/범위; 영속 보호"]
  Freeze --> Continue["mutation gate 해제; 후속 쓰기 지속"]
  Continue --> Scan["고정된 captured tree와 basis scan"]
  Scan --> Route{"독립 payload 저장소"}
  Route -->|Git| Git["독립 Git ref/객체; 결과 blob stream"]
  Route -->|None / recorded Plastic| Files["결과 버전 간 내용 기반 전체 파일 객체 재사용"]
  Git --> Proof["독립 결과 버전/객체 검증"]
  Files --> Proof
  Proof --> Commit["결과 ID/순번/key/내용 identity 저장; ready"]
  Commit --> Unpin["materialized 영수증 게시; 정확한 backing alias 제거"]
  Files -.->|실패 / 취소| Drain["worker drain; 정확한 intent/보호 보존"]
  Unpin --> Next["E20 지속 또는 고정 결과 게시"]
```

적격 local payload/proof는 최대 8 worker이며 recorded Plastic revision 획득은 직렬이다. 파일 결과는 모든 버전의 내용 재사용과 함께 서로 다른 basis/result 전체 payload를 보존한다. Git은 독립 ref/객체를 유지하며 전체 소스의 중복 파일 저장소를 만들지 않는다. scan·독립 증명·영속 게시는 mutation gate 밖의 실제 비용이다. 실패/응답 유실은 고정 수집을 재개하며 나중 편집을 다시 수집하지 않는다.

최종 R10은 해당 task Editor만 정상 종료하고 bridge 제거·전체 pre-seal 뒤 같은 수집 엔진으로 종료 중 마지막 쓰기까지 포함하고 전체 post-seal 일치를 요구한다. 회수 전에 과거 결과도 모두 검증하며 모든 결과 버전·반영 영수증을 남긴다. `checkpoint --release` 재시도는 요청한 결과를 반환하고 최종 종료 결과도 보존한다. 컴파일 성공을 주장하지 않는다. 이 경로는 [RI](verification/repeated-original-apply.md)가 소유하며 RT 한 번 scan 시간은 과거 정지형 수집 구현의 근거다.

<a id="a10"></a>
### A10 — 명시적 Git 브랜치 merge

branch 대상은 `--target original` 파일 게시와 별도다. `IntegrateGenerationAsync`가 선택한 독립 Git 결과를 검증하고 기존 clean-target merge 엔진에 revision을 전달한다 [K18, K34]. 현재 작업의 후속 편집·Editor는 유지한다. 원본은 요청한 깨끗한 branch를 이미 checkout해야 한다. 저장소 잠금·소유 merge abort·결과 보호를 유지한다. push는 포함하지 않으며 원본 Editor/도구 조정은 외부 책임이다.

<a id="a20"></a>
### A20 — Git/None/Plastic 원본 파일 반영 계획

`workspace integrate --target original --result <id> --plan` [K16, K19, K34–K35]은 새 요청에 최신 독립 결과를 선택한다. 최초에는 불변 basis를 사용하고 이후 **경로별 마지막 반영 상태 / 고정 결과 / 현재 원본**을 대조한다. 선택한 경로만 삭제 기록·Unity `.meta` closure와 함께 진행하며 수집 뒤 작업 편집은 고정 결과를 무효화하지 않는다. target identity·이전 영수증 digest·결과·소스 관리 identity·선택·실행 중 원본 협력을 계획 digest에 결합한다.

관리형 project의 정확한 mount/basis 보호를 유지한다. Git은 읽기 전용 관리 identity/잠금 보호와 HEAD/index/branch를 유지한다. Plastic은 client/등록/repository/loaded revision/selector/root·영향 lock을 검증하고 서버를 변경하지 않는다. 실행 중 Unity 원본에는 정확한 외부 저장/import 유예 허가와 실제 실행 파일/PID/시작/프로젝트 대조·lockfile 보호가 필요하다. 게시 뒤 에이전트가 새 dirty 검사·refresh/재로드·로드 값/컴파일을 확인한다. 계획을 검토하며 충돌은 보존·거부한다. [반복 반영](repeated-original-apply.md) 참조.

<a id="a30"></a>
### A30 — 고정 파일 계획 게시

같은 결과·`intent-key`·`expected-plan-digest`·선택·실행 중 협력 identity·task/home을 전달한다. 최초 intent 전에 계획을 재확인하고 intent/progress를 영속화한다. `WindowsOriginalFilePublisher.Publish` [K17]가 전체 write set과 NTFS identity/parent handle을 확인하고, 소유 sibling stage·receipt 생성 → 독립 payload 읽기/hash → stage 쓰기/flush → 기존 entry 조건부 retire·새 entry 게시 → 단계별 progress 게시 → 소유 retired/stage 검증·회수를 수행한다.

**파일별 복구 가능한 게시이며 모든 파일의 단일 원자 transaction이 아니다.** 부분 효과는 같은 영속 intent 아래 남는다. 소유 불명/교체된 stage는 보존한다. 미완료 intent는 release/discard/새 수집을 막는다. 같은 완료 요청 반복은 재게시하지 않는다. Library·VCS 이력/등록/lock·선택하지 않은 소스는 반영하지 않는다. 반영된 원본 검증은 에이전트/도구의 별도 작업이다.

<a id="r10"></a>
### R10 — 결과를 보존하고 작업공간 회수

`ReleaseSnapshotWorkspaceAsync` [K02, K32]는 모든 보존 결과를 검증하고 해당 task만 정상 종료하며 C20으로 최종 결과를 seal한다. 전체 전후·unmount seal을 확인하고 모든 결과 버전/영수증을 남긴다. 저장된 결과나 재시도는 이전 메모리 증명을 승계하지 않는다.

영속 순서: **release-unmount → release-registration → release-delta → release-snapshot → released**.

1. dependent candidate/source/cache view를 거부한다. 소유 앱을 정지하고 독립 결과·stable seal을 검증/재사용한다.
2. provider를 unmount한다. post-unmount seal이 결과와 같고 소유 provider/handle/memory가 0이며 mount root가 없어야 한다.
3. 프로젝트 잠금 아래 소유 소스 등록만 제거한다. 독립 Git 결과 ref/객체와 파일 결과 객체는 남긴다.
4. 영속 회수 기록의 정확한 저널 inventory를 회수한다. 부분 삭제는 이 기록으로 재개한다.
5. 기준 잠금 아래 의존성을 재확인하고 작업 기준/ref·선택적 Library 의존성을 해제한다. project는 자기 참조만 해제한다. 정확한 부재와 자원 0을 관측한 뒤 `released`다.

물리 회수가 남으면 `reclaim-pending`이지 성공 완료가 아니다. 같은 task/home/result를 유지한다. peer 연쇄 정리나 control/evidence 삭제로 우회하지 않는다.

<a id="d10"></a>
### D10 — 결과 생성 없는 폐기

```mermaid
flowchart TD
  Intent["정확한 task + discard intent + expected request digest"] --> Pre["상충 apply/release/compact·dependent view 없음; owner 증명"]
  Pre --> Stop["stop 단계 영속화 / 소유 Editor 정상 종료 / 전체 seal"]
  Stop --> Unmount["seal 대조 / unmount / seal 대조 / 자원 0"]
  Unmount --> Source["프로젝트 잠금 아래 소유 소스 등록 폐기"]
  Source --> Delta["정확한 저널 회수 / 영속 부분 삭제 복구"]
  Delta --> Authorize["delta/mount 부재·자원 0 검증을 영속 게시"]
  Authorize --> Basis["의존성 재확인 / 작업 기준·참조 회수"]
  Basis --> End{"회수 완료 관측?"}
  End -->|예| Done["discarded; intent/receipt/evidence 보존"]
  End -->|지연| Pending["discard-reclaim-pending; 같은 intent로 재시도"]
```

`WorkspaceDiscard.DiscardAsync`, `WindowsWorkspaceDiscardProvider` [K03, K21]. **stop → unmount → source → delta → snapshot → complete**를 영속화한다. 컴파일 성공·checkpoint·독립 결과가 필요하지 않다. 정상 종료·전체 seal 대조·의존성 보호·정확한 소유권·mount/delta 부재·소유 자원 0은 여전히 필요하다. 따라서 폐기에도 종료·hash·파일 삭제 비용이 든다. 이미 반영한 원본을 rollback하거나 기존 독립 결과를 지우거나 Git GC를 수행하지 않는다.

<a id="f10"></a>
### F10 — 공유 원본 기준본을 별도로 복원

`library restore` / `project restore` [K04, K22, K23, K25, K29, K30, K31]. 모든 dependent/reader가 끝나고 정확한 원본 Editor를 저장·종료한 뒤 명시적으로 수행한다. 원본 managed provider를 종료하고 mount/provider 부재를 증명한다. exclusive reader 보호를 잡아 `restoring`과 고정 seal/plan/cursor를 사용한다.

- **최종 상태가 정확히 같은 Library**: SJL4/bounded SJR2, 시작된 파일 plan 없음. 전체 최종 metadata·경로·retained 내용이 고정 manifest와 같고 private range가 없을 때만 양수 durable sequence도 허용한다. 재시도를 포함해 같은 고정 핸들의 plan bytes/hash와 sealed journal replay를 이 일치 조건에 결속한다. no-replace 디렉터리 rename → 전체 NTFS identity/content/membership 재증명 → metadata 복원 → receipt 검증·정확한 sealed 저널 회수. stopped journal reader를 최종 proof/metadata 동안 유지하고 reclamation inventory로 인계한다. 전체 project 복원의 fast path는 아니다.
- **일부 Library 변경(BO-01)**: 디렉터리 집합과 surviving 파일 경로/길이가 같고 private range·새 파일 없이 metadata 변경·기존 leaf 삭제만 허용한다. 전체 identity·metadata patch·정렬된 삭제 목록·정확한 journal seal을 별도 plan에 고정한다. root를 한 번 rename한 뒤 survivor 전체와 남은 삭제 leaf를 reopen·증명한다. 삭제 leaf를 exact handle로 receipt root의 지정 staging 이름으로 옮기고 최종 membership 확인·metadata 적용·plan에 결속된 verified 및 reclaim receipt 게시 뒤에만 staged 데이터를 삭제한다. 같은 핸들의 plan bytes/hash와 최종 효과를 sealed journal replay에 대조하고 survivor·plan·journal 보호를 staging 삭제까지 유지한다. staging 재개 시 reclaim receipt가 있을 때만 leaf 부재를 허용한다. 저널은 기존 exact inventory로 회수한다.
- **같은 디렉터리 구조의 변경 Library (MX05 root-delta v1)**: root를 한 번 옮기고 stable 파일은 그대로 둔다. source-backed 파일 경로/spelling을 유지하며 private range·truncate/확장/zero hole·새 leaf·삭제 leaf를 처리한다. 고정 plan/intent/seal, 파일별 write/complete·신규 identity receipt, 전체 증명·metadata와 plan-bound staged reclaim을 유지한다.
- **구조가 바뀐 Library (MX05 topology v1)**: parent mapping으로 설명되지 않는 최소 unit을 고정한다. 깊은 split child stage → 얕은 final/new-directory 설치 → 파일 이동 → private range/새 leaf → 최종 proof/metadata → 승인된 staged 삭제 순서다. Root 효과 전 cursor 0, topology 효과별 cursor와 새 directory identity receipt를 영속 게시한다. 재개는 k/k+1의 실제 inventory를 증명한다.
- **그 외**: 일반 v2 파일별 plan. retained 파일 이동·private range/truncation/hole/metadata 반영·새 파일 durable creation receipt·최종 content/identity 검증을 수행한다. **128개 파일**마다 및 마지막에 completion cursor를 영속 게시한다. range data flush와 최초/최종/새 파일 receipt는 유지한다. 재개 시 마지막 durable cursor 이후 효과를 다시 검증한다.

기존 unchanged/general/sparse/root-delta/topology plan은 원래 경로로 재개하며 orphan/conflict·foreign/교체 항목·변경 내용/seal은 보존·거부한다. 신규 선택 순서는 unchanged → sparse → root-delta → 적격·선호 topology → general이다. 새 두 경로는 원본 Unity Library의 SJL4/bounded SJR2만 지원하며 whole-project·append SJR2는 general이다. Topology 선호는 `T < F && O + N <= ceil(F / ManagedDirectoryRestore.CursorBatchSize)`이고 공유 상수는 `128`이다. F는 생존 source 파일, T는 root 포함 보존 이동, O는 topology 연산, N은 새 directory receipt다. 새 파일은 F를 늘리지 않으며 기존 intent에는 이 비용 조건을 재적용하지 않는다. 구조적 비용 screen일 뿐 시간 보장이 아니다.

Inventory projection은 실제 type/path/ID/hash 증명이 아니다. 원본·receipt forest map을 따로 고정하고 최대 8 worker가 fullhash를 수행하며 오류/취소도 join 뒤 dispose한다. 원본 root는 caller가 소유하고 deleted directory DELETE 권한과 최종 TreePins/현재 membership/metadata/staged reclaim 보호를 유지한다. Exact-parent no-replace 새 파일/디렉터리 생성·topology 자식 stage/install과 현재 타입/identity/path 검사는 빈 부모의 attrs-only reparse 전환을 containment하며 보편적인 전환 차단을 주장하지 않는다. 새 파일의 identity receipt 전 중단은 보존·거부한다. canonical plain `.GUID.tmp`는 무시·보존하지만 authority로 입양하거나 삭제하지 않는다. [BO 검증](verification/lifecycle-bottlenecks.md)은 과거 sparse 근거를 소유하고 [MX05 검증](verification/remaining-lifecycle-optimization.md)은 현재 후보별 결과를 소유한다. 최종 일반 NTFS 성능/자원 판단과 전체 gate 뒤 A 및 guard 적용 B2를 채택했으며 실제 Unity 전체 자격은 추가하지 않는다.

허용된 원본 편집과 명시적 소스 반영을 포함한 **원본 view의 현재 내용**을 복원한다. 작업 Library를 원본에 합치는 것이 아니다. 최종 검증 뒤 이동 공통 payload/원본 저널/provider 자원을 회수하되 state·manifest·plan·receipt·독립 결과는 보존한다. `status`만으로 live mount를 증명하지 않는다. peer가 필요하면 관리형 상태를 유지하고 F10 미완료를 명시한다.

## 5. 직렬화·겹침·영속 경계

| 경계 | 현재 동작 | 최적화 시 의미 |
| --- | --- | --- |
| 작업 잠금 | ID별 수명주기 명령 전체; 다른 ID는 진행 가능 | 같은 ID의 명령 병렬화·전역 편집 배타 잠금으로 해석하지 않음 |
| 프로젝트 잠금 | 소스 preflight/등록·반영·소스 등록 제거/폐기 | 독립 작업도 대기 가능; lock wait 별도 기록 |
| 기준 gate/reader lease/dependent | 공유 준비/attach/복원·기준 수명 보호 | 복원은 실제 의존성 종료를 기다림; 검사 삭제는 가속 방법이 아님 |
| WinFsp | 8 dispatch thread, mutation gate 앞 범위 쓰기 FIFO | 동시 도착은 barrier 감소 가능; 완전 순차 caller의 다음 요청을 미리 묶을 수 없음 |
| 저널 mutation gate | metadata/range 순서·checksum·append·checkpoint·flush | 저널별 frame 준비/append는 현재 병렬화하지 않음 |
| 읽기 수명 | 지원하는 불변 읽기 겹침, private/상태 수집 gate, close drain | 폐기한 flush/read overlap 후보는 활성화하지 않음 |
| Git reader | 두 decoder 임대·제한 proof/payload cache | 다른 객체 decode는 겹칠 수 있으나 최초 전체 증명 유지 |
| 관리형 파일 | capture/admission/변경 없는 복원 및 topology forest proof 최대 8 worker, 고정 map·실패 join/drain | 이미 병렬화됨; 추가 concurrency는 device 경합·전체 RAM 계측 필요 |
| 파일 소스 / 독립 결과 | None 전체 소스 hash·로컬 payload/증명은 단계별 최대 8 worker와 반환 전 drain, Plastic mount 소스 scan 직렬, C20 scan 1회, 최초 editorless 증명 1회성, Plastic revision 획득 직렬 | 기준/결과 전체 내용과 이후 호출의 새 증명 비용은 남음 |
| 영속 상태 | workspace/intent/receipt 원자 게시·영속 경계; 저널 checksum/flush | heartbeat/liveness는 영속 소스 결과 commit이 아님 |
| 종료 | Unity import/compile/자식 대기, retained handle이면 unmount 불가 | Editor exit·child exit·handle drain·unmount·회수를 분리 |

작업 하나의 앱 정지·독립 보존·필수 post-unmount seal은 선후 의존성이 있다. 독립 A/B 구간은 겹칠 수 있지만 공유 준비/복원은 한 번 센다. 명령 dispatch·AI 추론·외부 도구 대기를 저장소 비용에 숨기지 않는다.

## 6. 최적화 계측 계약

향후 evidence에 이 단계 ID를 사용한다. **이 목록만으로 새 Unity 실험을 실행하지 않는다.** 승인된 입력·질문·자원 제한·완료/복구 조건부터 고정한다.

| 단계 | 시작 → 종료 경계 | counter / 교란 요인 |
| --- | --- | --- |
| P10 | prepare 요청 → 원본 managed view ready 증명 | 전체 scan/hash bytes·파일·worker; 원본 정지는 별도; 공통 비용 |
| P20–P50 | ensure dispatch → mounted clean ready 반환 | 잠금 대기·preflight/capture/register/mount/attach·Git cold proof/cache·replay bytes |
| E10 | start dispatch → launch receipt → 첫 heartbeat → 외부 검증 compiled baseline | Unity 버전·소스/Library bytes/mtime·실제 loaded assembly·OS/cache |
| E20 | edit 완료 → refresh → compile/reload 완료 → 검증 완료 | 정확한 patch/workload·package/import·polling/연결 지연; AI 추론 별도 |
| W10 | callback 접수 → gate 획득 → 비교/준비 → append → flush → ACK | 연산 종류·payload/frame bytes·batch 분포·flush 횟수/시간·waiter; metadata 별도 |
| B10 | checkpoint trigger → 게시/replay 안정화 → 해당 write ACK | live prefix/suffix bytes/frame·크기 추정·seal/상태 증명·출력/replay·임시 중복 |
| C10 | stop 요청 → Editor exit → child/resource 0 | busy/Play/dirty scene 거부·종료 코드 배치·helper·retained 파일/image handle |
| C20 | 요청 → mutation 대기/고정 → 독립 payload/proof → 결과 게시/unpin | callback drain·mutation pause·소스 scan/hash·보존 payload; 최종 종료는 전체 seal 포함 |
| A10/A20/A30 | plan 별도; execute 요청 → integrated 완료 | target/선택·stage bytes·merge·파일별 flush/receipt·충돌/재시도 |
| R10/D10 | release/discard 요청 → 정확한 자원 부재·완료 receipt | seal 읽기·unmount·등록 제거·inventory 삭제·task ref 제거; pending은 끝 아님 |
| F10 | 명시적 restore → 일반 원본 검증 + 공통 자원 회수 | unchanged/sparse/root-delta/topology/general 경로, root/unit/leaf 이동, topology별 cursor·새 directory receipt와 general 128-file batch 구분, 전체 proof bytes·range flush·metadata·정확한 reclaim |

run별 원시 시작/종료 시각과 hash를 보존하고 정상 처리와 진단/복구를 분리한다. 같은 소스 bytes만으로 같은 Unity 입력이 되지 않는다. Library 내용/mtime·loaded assembly·Unity/package·Editor 시작 이력도 다를 수 있다. 양쪽 후보를 같은 입력에서 새로 측정하고 과거 구성 요소 절감률을 더해 전체 비율을 예측하지 않는다. 겹친 구간은 한 번 세며 중첩 단계 counter는 단순 합산하지 않는다.

### 기존 근거와 다음 질문

| 질문 / 단계 | 기록된 후보에서 확인한 근거 | 다음 유용한 관측; 구현 약속 아님 |
| --- | --- | --- |
| 중복 명시적 flush, W10 | [WP](verification/write-path-throughput.md): 2,048 writes+명시적 flush 8.114789 → 4.374668초; 공급된 16-write batch 0.518767 → 0.299169초 | 실제 앱 explicit flush·쓰기 도착 분포; 일반 쓰기 내구성 유지 |
| CPU 준비 / flush, W10 | WP 최종 2,048 small write: 준비 0.033705초, 물리 flush 4.317677초 | thread·binary metadata 추가 전 실제 연산 구성 확인 |
| 상태 크기 / 전체 checkpoint, B10 | WP 크기 계산 0.277765 → 0.049482초; checkpoint workload 전체 1.030469 → 0.993840초; 변경 후 checkpoint 자체 0.707720초 | 전체 상태 쓰기·증명·replay·게시를 귀속; 증분 표현 미구현 |
| 큰 private overwrite 비교 | WP 후보 읽기는 감소했지만 평균 1.838353 → 1.882652초 | 후보 제거됨; 읽기 bytes만으로 복구하지 않음 |
| 생성, P10/P50 | [WC](verification/workspace-creation-throughput.md): Library 준비 후 같은 입력 editorless Git 생성 73.953 → 51.915초 | 새 승인 입력의 최초 proof/index/manifest; 일반 대형 이점 아님 |
| Unity 수명주기 하나 | [LU](verification/unity-single-lifecycle.md): 359.5 → 300.0초; 준비/생성/복원 일관된 50.6초 절감 | 과거 결합 후보; 소스 반영 미실행; WP/RW 이득을 이 합계에 귀속 불가 |
| 정상 Editor 종료, C10 | [SD](verification/unity-shutdown-diagnosis.md): 같은 method 수의 단일/64개 class 17.34초 / 1.60초 | 정확한 종료 workload/loaded 상태; 일반 method 수 증가 법칙 아님 |
| 파일 결과 보존, C20 | [RT-03](verification/result-collection-throughput.md): 실제 editorless None checkpoint **98.429 → 58.944초**, **39.485초 / 40.1%** 절감; 한 번 scan과 제한 병렬 payload 작업을 묶어 측정 | baseline/result 전체 구체화·반복 object 증명은 유지; 개별 인과 효과·Unity/실제 Plastic 시간은 미확인 |
| metadata mutation, W10 | 범위 Write를 묶고 별도 metadata callback commit 유지 | 더 넓은 ordered commit 계약 전에 create/rename/resize/metadata/delete flush 집계 |
| release / discard | discard는 C20 보존을 생략하지만 stop/seal/reclaim 필요 | 같은 상태에서 단계별 비교; 무료 폐기나 보편 절감 가정 없음 |

RT-03은 같은 **2.125GiB·2,112개 파일**의 소스 내용·수정 시각과 고정 편집으로 전 → 후 → 후 → 전 순서를 사용했다. P20–P50 ensure + E20 고정 편집 + C20 checkpoint + 별도 R10 release 합계는 **203.305 → 159.906초**, 관측 절감은 **43.399초 / 21.3%**다. checkpoint 명령 시간에는 명령 외곽 비용도 포함되며 내부 C20만의 타이머는 아니다. 공통 P10 준비 **9.031초**와 F10 복원 **12.018초**는 합계 밖에서 각각 한 번 수행했다. Editor·원본 반영·폐기 시간은 측정하지 않았다. 버전별 두 번·큰 절대 시간 편차·OS 캐시를 비우지 않은 조건의 한계를 유지하며 [개별 실행과 소스 hash](../../reports/benchmarks/result-collection-throughput-20261026.json)를 따른다.

PR-03은 새로운 동일 **2.125 GiB / 2,112파일** 입력의 ABBA 순서로 후속 준비/관측 변경을 별도 비교했다. Ensure **65.258 → 20.650초**, checkpoint **38.916 → 24.524초**, 별도 release **35.071 → 9.578초**, 같은 작업공간 단계 합계는 **139.304 → 54.790초**로 **84.515초 / 60.7%** 줄었다. 공통 prepare **8.598초**와 restore **11.965초**는 각각 한 번 실행해 합계에서 제외했다. 모든 소스/결과 identity와 소유 회수가 일치했다. 결합한 변경의 관측이며 개별 인과 시간이나 Unity/반영/폐기 측정이 아니다. 한계는 [PR 검증](verification/preparation-throughput.md)이 소유한다. 남은 명령 비용은 20.650초 / 24.524초 / 9.578초이며 전체 admission·독립 payload 구체화·이후 호출의 새 증명을 유지한다.

이 입력에서 남은 실측 비용은 **P20–P50 생성 65.599초**, **checkpoint 58.944초**, **별도 R10 release 35.326초**다. 후속 계측은 생성의 admission/baseline scan, 전체 결과 구체화/증명, release의 소스/seal/object 재검증 비용을 나누어야 한다. 이 수치는 조사 위치를 알려주며 증명 생략을 승인하거나 미구현 후보의 절감량을 입증하지 않는다.

폐기/무이득 후보는 유지하지 않는다: [DF](verification/durable-flush-throughput.md) RAM checkpoint·I/O 대안, [LT](verification/lifecycle-throughput.md) flush/read 겹침, native Bee, VSS, ReFS. 조기 ACK·필수 flush 생략은 다른 내구성 정책이지 동등 최적화가 아니다. 이 지도는 backend·worker pool·명령·정책 switch를 추가하지 않는다.

BO-01–05는 F10 sparse 복원 구현과 W10/P50 진단 구간 분리, E10 근거 수집을 연결한다. 진단 OFF/ON 비용·일반 NTFS 복원 비교·과거 RR-01 Unity 관측은 별도 근거다. 이전 표로 BO의 전체 Unity 절감이나 새 반영/폐기 시간을 추정하지 않으며 현재 자격은 [BO 계획](plans/lifecycle-bottlenecks.md)·[검증](verification/lifecycle-bottlenecks.md)을 따른다.

MX10 최종 비교는 반영 **338.51645460→326.31757210초**, 폐기 **281.90245510→286.53377360초**다. 단일 전체 stopwatch이며 각 경로 전후 **1회**라 큰 전체 이점·인과 효과를 확정하지 않는다. 모두 원본 Library sequence **0**의 `unchanged-root`여서 MX05 A/B2 변경 복원을 선택하지 않았다. 현재 큰 구간은 준비 약 **40초**, ensure **49–52초**, 초기 ready **98–101초**, 최종 반영 **90.57529460초**/폐기 **44.63291410초**, 복원 **22–24초**다. 최종 gate **1,966 통과 / 0 실패 / 34 조건부 skip**과 MX08A 채택 근거·제한은 [MX10](verification/remaining-lifecycle-optimization.md#mx-10-final-closeout)이 소유하며 새 시험을 승인하지 않는다.

## 7. 완료·실패 해석

| 상태 / 관측 | 올바른 해석과 후속 |
| --- | --- |
| `ready`, Editor stopped | 결과 수집 전 재사용 가능한 작업; identity/basis 유지 |
| Editor running, `ready=false` | starting/busy/unresponsive 관측; log·정확한 ownership 확인; 컴파일 판정 아님 |
| `collected` | 독립 소스 결과 존재; 정상 start/edit 수명주기 종료; 반영/보존/release |
| `integrated` | target 반영 완료; 작업공간/기준 정리가 남을 수 있음 |
| `reclaim-pending` / `discard-reclaim-pending` | 작업 제거가 끝나도 기준 회수는 남을 수 있음; 같은 request/intent 재개 |
| `released` / `discarded` | 관측된 task 자원 부재·소유 자원 0; 독립 결과/evidence 보존; F10은 남을 수 있음 |
| 원본 apply 미완료 | 같은 intent/digest/선택만 허용; release/discard/새 수집 차단 |
| 파일 수집 실패 또는 seal 변경 | 접수한 object worker를 drain하고 journal·고정 intent·부분/게시 object를 보존. `result.json`만으로 task revision/result를 확정하거나 회수하지 않으며 같은 identity로 재개 |
| append 실패 / poison | 저널 보존·reopen/replay; 불확실한 작업 성공 ACK 없음; 새 task로 우회하지 않음 |
| 기준/소스/owner 불일치·미지원 버전 | 보존·거부; 자동 이관·mutable 원본 fallback·수동 control 삭제 없음 |
| timeout·dirty 원본/scene·공유 의존성 | 실제 조건 해결; 강제 kill/reset/discard/peer 정리 승인을 추론하지 않음 |

보고에는 task/basis ID·home, 현재 result/target, 정확한 명령 결과, 실제 Editor/provider/handle/dependency 관측, 보존 evidence, 공유 restore 미완료 여부를 쓴다. unknown counter를 0으로 대체하지 않는다.

## 8. 코드 진입점 색인

JSON은 같은 단계 ID·edge·source symbol·코드 기준 행 힌트를 기록한다. 코드가 바뀌면 `rg`로 symbol을 찾는다. 이 문서는 최적화 탐색 지도이며 기능 계약과 자격은 연결된 소유 문서가 유지한다.

| Ref | 파일 / symbol |
| --- | --- |
| K00 | [WorkspaceCommands](../../src/Splinter.Cli/WorkspaceCommands.cs): `Add`, Runtime 조합, 명령 분기; [GeneratedDirectoryCommands](../../src/Splinter.Cli/GeneratedDirectoryCommands.cs) |
| K01 | [WorkspaceCoordinator](../../src/Splinter.Runtime/WorkspaceCoordinator.cs): `EnsureAsync`, `StartCoreAsync`, `CheckpointAsync`, `IntegrateAsync`, `ReleaseAsync` |
| K02 | [SnapshotWorkspaces](../../src/Splinter.Runtime/SnapshotWorkspaces.cs): `EnsureSnapshotAsync`, `ConnectSnapshotAsync`, `CheckpointSnapshotResultAsync`, `ReleaseSnapshotWorkspaceAsync`, `ReuseSnapshotResultAsync` |
| K03 | [WorkspaceDiscard](../../src/Splinter.Runtime/WorkspaceDiscard.cs): `DiscardAsync`, seal/dependency/자원 0 guard |
| K04 | [ManagedDirectoryBasis](../../src/Splinter.Platform.Windows/ManagedDirectoryBasis.cs): `PrepareAsync`, `AttachAsync`, `DetachAsync`, `RestoreAsync`; [OwnedDirectorySnapshot](../../src/Splinter.Platform.Windows/OwnedDirectorySnapshot.cs): worker/proof |
| K05 | [NtfsSnapshotWorkspaceBackend](../../src/Splinter.Platform.Windows/NtfsSnapshotWorkspaceBackend.cs): capture/connect/seal/unmount/delta/기준 회수; [ProjectDirectorySnapshotProvider](../../src/Splinter.Platform.Windows/ProjectDirectorySnapshotProvider.cs): 공통 project 참조와 release 관측 |
| K06 | [GitSnapshotWorkspaces](../../src/Splinter.SourceControl.Git/GitSnapshotWorkspaces.cs), [GitOverlayRegistration](../../src/Splinter.SourceControl.Git/GitOverlayRegistration.cs), [GitObjectSnapshotTree](../../src/Splinter.SourceControl.Git/GitObjectSnapshotTree.cs): 고정 기준·attach/proof/선행 읽기·보존 |
| K07 | [WinFspOverlayMountProvider](../../src/Splinter.Platform.Windows/WinFspOverlayMountProvider.cs), [WinFspOverlayClient](../../src/Splinter.Platform.Windows/WinFspOverlayClient.cs), [WinFspOverlayServer](../../src/Splinter.Platform.Windows/WinFspOverlayServer.cs): 소유 provider/control/replay/mount |
| K08 | [UnityWorkspaceProvider](../../src/Splinter.Application.Unity/UnityWorkspaceProvider.cs): start/infrastructure/관측/정상 종료 |
| K09 | [WindowsProcessWorkspaceProvider](../../src/Splinter.Platform.Windows/WindowsProcessWorkspaceProvider.cs): 소유 launch/group/exit 증명 |
| K10 | [JournalDeltaStore](../../src/Splinter.Runtime/JournalDeltaStore.cs): `Read`, `Write`, `FindPrivateChanges`, `Commit`, `Flush`, replay/index; [JournalDeltaCandidates](../../src/Splinter.Runtime/JournalDeltaCandidates.cs): `Seal` |
| K11 | [WinFspOverlayFileSystem](../../src/Splinter.Platform.Windows/WinFspOverlayFileSystem.cs): read/write/flush/metadata/cleanup/close callback |
| K12 | [WinFspWriteBatch](../../src/Splinter.Platform.Windows/WinFspWriteBatch.cs): `SubmitWrite`, `ExecuteWriteBatch`, `DrainAcceptedWrites` |
| K13 | [JournalWriteBatch](../../src/Splinter.Runtime/JournalWriteBatch.cs): `WriteBatch`, `FlushPendingWrites`; [WorkspaceWriteBatch](../../src/Splinter.Core/WorkspaceWriteBatch.cs): 제한 |
| K14 | [JournalDeltaBounded](../../src/Splinter.Runtime/JournalDeltaBounded.cs): 용량/checkpoint/generation 복구 |
| K15 | [JournalStateFrames](../../src/Splinter.Runtime/JournalStateFrames.cs): raw 크기 계산/출력/replay |
| K16 | [WorkspaceOriginalApply](../../src/Splinter.Runtime/WorkspaceOriginalApply.cs): plan/apply 진입 guard |
| K17 | [WindowsOriginalFilePublisher](../../src/Splinter.Platform.Windows/WindowsOriginalFilePublisher.cs): NTFS 조건부 게시·receipt, `MoveOwnedEntry`, `CreateOwnedFile`, `CreateOwnedDirectory` |
| K18 | [GitWorkspaceProvider](../../src/Splinter.SourceControl.Git/GitWorkspaceProvider.cs): 소스 checkpoint·target/dirty guard·merge |
| K19 | [FileWorkspaceProvider](../../src/Splinter.SourceControl.Files/FileWorkspaceProvider.cs), [FileRecordedBasis](../../src/Splinter.SourceControl.Files/FileRecordedBasis.cs), [FileOriginalApply](../../src/Splinter.SourceControl.Files/FileOriginalApply.cs), [PlasticWorkspaceProvider](../../src/Splinter.SourceControl.Files/PlasticWorkspaceProvider.cs): manifest/전체 결과 payload/원본 plan·intent; [FileInspectionProof](../../src/Splinter.SourceControl.Files/FileInspectionProof.cs): 세대에 결속된 1회성 응답 증명 |
| K20 | [WorkspaceBridge](../../unity/Splinter.Editor/Editor/WorkspaceBridge.cs): heartbeat/busy/dirty scene/Play/정상 exit |
| K21 | [WindowsWorkspaceDiscardProvider](../../src/Splinter.Workflows.Windows/WindowsWorkspaceDiscardProvider.cs): 정확한 소유 delta/기준/소스 폐기 |
| K22 | [ManagedDirectoryRestore](../../src/Splinter.Platform.Windows/ManagedDirectoryRestore.cs): `ExecuteAsync`, 공유 `CursorBatchSize=128`, 신규 route 선택·기존 intent 재개·일반 v2 file/range/cursor/final proof |
| K23 | [ManagedUnchangedDirectoryRestore](../../src/Splinter.Platform.Windows/ManagedUnchangedDirectoryRestore.cs): 변경 없는 적격 Library rename/proof/회수 |
| K24 | [WorkspaceStore](../../src/Splinter.Runtime/WorkspaceStore.cs), [ProductState](../../src/Splinter.Workflows.Windows/ViewLifecycleState.cs): identity/경로/잠금/영속 record 게시 |
| K25 | [ManagedSparseDirectoryRestore](../../src/Splinter.Platform.Windows/ManagedSparseDirectoryRestore.cs): `Eligible`, `CreateAsync`, `ResumeAsync`, `OpenJournalEffects`, staged leaf 회수; [ManagedRestoreJournalProof](../../src/Splinter.Platform.Windows/ManagedRestoreJournalProof.cs): 고정 manifest metadata replay; [ManagedRestoreFileAccess](../../src/Splinter.Platform.Windows/ManagedRestoreFileAccess.cs): exact handle 이동/삭제, `MoveRelative`, `CreateFile`, `CreateDirectory`, `PinDirectoryAgainstDataWrites` |
| K26 | [WinFspWriteTelemetry](../../src/Splinter.Platform.Windows/WinFspWriteTelemetry.cs): `WriteTelemetry`, `ObserveWriteAdmission`, 제한 구간 관측; [JournalWriteAccounting](../../src/Splinter.Runtime/JournalWriteAccounting.cs), [SourceViewDeltaStore](../../src/Splinter.Runtime/SourceViewDeltaStore.cs): 저비용 Runtime 구간 counter |
| K27 | [GitReadObservation](../../src/Splinter.SourceControl.Git/GitReadObservation.cs): `ObserveReads`; [GitPreparationObservation](../../src/Splinter.SourceControl.Git/GitPreparationObservation.cs): `Run`, `Measure`, `Observe`; [WorkspaceSnapshotBasis](../../src/Splinter.Platform.Windows/WorkspaceSnapshotBasis.cs): `ObserveGitReads` |
| K28 | [capture-unity-startup.ps1](../../scripts/capture-unity-startup.ps1), [UnityStartupCapture](../../scripts/internal/UnityStartupCapture.cs): 제한 개발 근거, 소유권 확인, before/live 연결, 로그 순서 분석 |
| K29 | [ManagedRootDeltaDirectoryRestore](../../src/Splinter.Platform.Windows/ManagedRootDeltaDirectoryRestore.cs): `Eligible`, `CreateAsync`, `ResumeAsync` — 고정 구조 root-delta v1 |
| K30 | [ManagedTopologyDirectoryRestore](../../src/Splinter.Platform.Windows/ManagedTopologyDirectoryRestore.cs): `Eligible`, `PreferFresh`, `CreateAsync`, `ResumeAsync`; [ManagedRestoreTopology](../../src/Splinter.Platform.Windows/ManagedRestoreTopology.cs): `Build`, `Inventory`, `ForestPins` |
| K31 | [ManagedRestoreControlFiles](../../src/Splinter.Platform.Windows/ManagedRestoreControlFiles.cs): `IsPublicationTemporary`; [WindowsDirectoryTreeProof](../../src/Splinter.Platform.Windows/WindowsDirectoryTreeProof.cs): `PinDirectory`; [WindowsOwnedDirectoryLease](../../src/Splinter.Platform.Windows/WindowsOwnedDirectoryLease.cs): `TransferToRestore` |
| K32 | [WorkspaceGenerations](../../src/Splinter.Core/WorkspaceGenerations.cs): `WorkspaceResultGeneration`; [WorkspaceGenerations](../../src/Splinter.Runtime/WorkspaceGenerations.cs): `CaptureGenerationCoreAsync`; [JournalSourceCapture](../../src/Splinter.Runtime/JournalSourceCapture.cs): `CaptureSource` |
| K33 | [NtfsSourceGenerations](../../src/Splinter.Platform.Windows/NtfsSourceGenerations.cs): `CaptureSourceAsync`; [WinFspSourceCapture](../../src/Splinter.Platform.Windows/WinFspSourceCapture.cs): `CaptureSource`; [WindowsJournalFilePins](../../src/Splinter.Platform.Windows/WindowsJournalFilePins.cs): `RecoverCopies` |
| K34 | [FileSourceGenerations](../../src/Splinter.SourceControl.Files/FileSourceGenerations.cs): `PreserveGenerationAsync`; [GitGenerationIntegration](../../src/Splinter.SourceControl.Git/GitGenerationIntegration.cs): `IntegrateGenerationAsync`; [GitOriginalPublicationGuard](../../src/Splinter.SourceControl.Git/GitOriginalPublicationGuard.cs): `GitOriginalPublicationGuard` |
| K35 | [UnityProjectPolicy](../../src/Splinter.Application.Unity/UnityProjectPolicy.cs): `AcquireOriginal`; [WindowsOriginalEditorIdentityVerifier](../../src/Splinter.Platform.Windows/WindowsOriginalEditorIdentityVerifier.cs): `Acquire` |

유지보수: 단계 순서·책임·내구성·모드/기본값·결과 구체화·회수 의존성이 바뀌면 영문·한국어·JSON을 함께 갱신한다. 과거 시간은 검증 ID에 결속하고 이 스냅샷을 현재 구현으로 사용하기 전에 변경된 코드를 확인한다.

[BD-01](verification/unity-lifecycle-bottleneck-diagnosis.md)은 이 커밋의 실제 GUI 진단 하나를 추가한다. 정상 종료·폐기·정확한 Library 복원까지 391.3초이며 명시적 캡처/서버 관측 29.7초를 포함한다. 전후 비교가 아니다. 전체 decoder 대기가 0.000837초여서 캐시 우회 후보를 제거했다. W10 순차 영속 쓰기·E10 초기화·P50 payload 보존/재해석은 별도 후보이며 겹치는 누적 시간을 절감량으로 더하지 않는다. 원본 반영과 새 최적화 GUI 비교는 미자격이다.

[GR-01–03](verification/git-payload-retention.md)은 Git payload cache 1.5GiB/32,768-entry 후보를 시험했다. 동일 입력의 실제 생성 평균은 **50.661 → 48.550초(2.111초 / 4.2%)**였다. 큰 절감 기준에 미달해 후보를 제거하고 **256MiB/4,096개를 유지**한다. Editor 없는 생성 근거이며 Unity 전체 수명주기 개선은 아니다. [고정 범위](plans/git-payload-retention.md) 참조.

MX05는 A와 게시 수 제한 guard를 적용한 B2를 채택했다. F10 일반 NTFS 복원은 고정 구조 46.05306965→14.73430160초(31.31876805초 / 68.00582087%), bulk 45.36571795→14.74108300초(30.62463495초 / 67.50611769%)였고 leaf는 general 경로에서 5.02044795→5.00588275초로 사실상 같았다. 전체 gate는 1,941개 통과·실패 0개·skip 33개다. 전체 시험 process의 관측 OS peak working set 평균 증가는 26,806,272 / 40,529,920 bytes이며 setup/assert와 표본 한계를 포함해 이 입력에서 합당하다고 판단했다. 과거 A/B1의 서로 다른 후보 실측·guard 산술·구성 요소 절감을 현재 전체 Unity 개선으로 승계하지 않는다. [MX 프로그램](plans/remaining-lifecycle-optimization.md)·[후보별 검증](verification/remaining-lifecycle-optimization.md)·[추적 보고서](../../reports/benchmarks/remaining-lifecycle-optimization-20261028.json)를 함께 읽는다.

RI-01–08 한정 자격은 현재 Windows cm 11.0.16.10484 private reader의 raw/BOM·segmented binary 읽기와 세 입력의 고정 Unity 원본/A/B 순서를 포함한다. 색인의 소스 hash가 이 후보를 고정하며 과거 성능 수치를 이 후보에 승계하지 않는다.
