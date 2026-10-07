# 개발용 소스 view와 Library cache 수명

[English](../view-workflow.md)

별도 [개발 CLI](development-cli.md)의 `view`는 후보·소스 세대·공통 Library·독립 결과의 저장 수명만 관리한다. 자체 refresh·컴파일·검증 수집/판정은 제공하지 않으며 작업 내용의 검증은 외부 도구와 에이전트가 맡는다. worker 보존은 제품 `workspace checkpoint`로 수행한다. 저장 경로/현재 형식과 기존 기록을 자동 이관하거나 삭제하지 않는다.

`pwsh ./scripts/splinter-dev.ps1 view --input <request.json> --home <state-root>`는 기존 NTFS worker의 불변 후보와 단일 소스 view를 연결한다. 명령 계약은 `schema view.execute`에서 확인한다. `capabilities.viewWorkflowContract`는 `stage=development-storage-view`, `cliAvailable=true`, `integratedWorkspaceMode=true`다. 실제 실행 자격은 [검증 추적표](plans/validation-matrix.md)와 [Library·일반 권한 실행의 제한](verification/product-validation-unelevated.md)을 따른다.

`Splinter.Workflows.Windows`는 Runtime coordinator와 Windows VSS/WinFsp, Git, Unity를 조합한다. Core에는 플랫폼 API가 없고 Host는 읽기 전용이다. 직접 worker Editor를 사용하는 기존 `workspace`/`unity` 명령과 선택적으로 함께 쓰며, 기본 모드 자동 선택·큐·Coordination·소유권 자동 인수는 제공하지 않는다.

## 실행 입력

현재 입력은 `version=1`, `action`, 절대 프로젝트 경로 `project`, `owner:{id,generation}`, 안정된 `requestKey`를 요구한다. ID는 경로가 아닌 1~128자의 영문·숫자·`._:-` 식별자다. owner generation은 양수이며 최초 명시적 사용으로 해당 worker/view에 고정된다. 같은 request key는 같은 전체 입력으로만 재전송한다. `--home`도 계속 유지한다.

```json
{
  "version": 1,
  "action": "prepare-worker",
  "project": "C:/projects/game",
  "owner": {"id": "conversation-a", "generation": 1},
  "requestKey": "prepare-a-1",
  "workspaceId": "<workspace ensure가 반환한 ID>"
}
```

worker는 `workspace ensure --storage ntfs-snapshot`으로 만든다. Editor 없이 소스를 편집하고 준비·게시한다. Library 등 관리 캐시의 변경 이력이 있는 worker는 source-only 게시 적합성을 충족하지 못하면 거부한다. 데이터 생략이나 전체 파일 복제로 우회하지 않는다.

`start`가 실행 중이며 application ready를 기다리면 `Accepted`, `terminal=false`다. `nextActions[].arguments.request`의 status JSON을 별도 파일로 저장하고 `pwsh ./scripts/splinter-dev.ps1 view --input <파일> --home <arguments.home>`으로 관측한다. 원래 요청 파일을 덮어쓰지 않는다. status ledger의 아직 없는 결과·frozen seal·응답은 필수 null 필드로 유지한다.

| action | 추가 입력 | 결과와 다음 단계 |
| --- | --- | --- |
| `prepare-worker` | `workspaceId` | 소유 Editor 정상 stop, bridge 제거, flush/unmount와 seal 확인. 게시할 worker identity 반환 |
| `publish` | `workspaceId` | 정지 worker의 원본 snapshot·Git 등록·source seal과 읽기 전용 저널 fence 확인 후 candidate 반환 |
| `select` | `viewId`, `candidateId`, 선택적 `maxJournalBytes` | 정지 view에 새 source generation 선택. 기본 예산 16 GiB, 같은 view에서 변경 불가 |
| `start` | `generationId` | 현재 세대를 같은 view 경로에 mount하고 정확한 Unity 버전 시작. 응답의 application ready는 별도 관측 |
| `status` | 선택적 `viewId` | workflow ledger와 실제 앱/provider/mount/handle 자원 관측 |
| `stop` | `generationId` | 정상 stop·bridge 제거·flush/unmount. timeout은 회수 완료가 아님 |
| `resume` | `generationId` | 현재 미보존 세대와 정지 자원의 영수증 확인. 실제 실행은 `start` |
| `preserve` | `target:{kind,id}` | `candidate` 또는 `source-generation`의 정확한 seal을 고정하고 독립 Git 결과 보존 |
| `release` | 같은 target | 독립 결과 재검증 후 그 artifact의 journal만 회수. 후보에 의존하는 source 세대가 있으면 거부 |
| `close-view` | `viewId` | 모든 소스 세대의 보존·회수와 정지 자원 확인 뒤 해당 cache 회수. 닫힌 view는 재사용하지 않음 |
| `compact-cache` | `viewId`, 양수 `maxEstimatedJournalOverlapBytes` | 정지 cache의 대체된 범위 정리. 같은 요청 중단 복구·내용 동등성·정리 전후 private payload와 파일 할당 영수증 반환 |

A/B/A 전환은 A stop → B select/start → B stop → A의 새 select/start다. 세 번의 선택은 세 개의 독립 source generation이다. 이전 A의 재전송은 현재 선택을 되돌리지 않는다. 게시 뒤 worker의 후속 편집은 `workspace ensure`로 같은 worker를 다시 연결해 이어가며 후보를 바꾸지 않는다. view의 실행을 최신 worker의 합격으로 올리거나 Unity가 생성한 source를 자동으로 되돌려 병합하지 않는다.

같은 VSS의 실행 중 provider들은 확인된 read access와 OS read fence를 공유한다. 재연결이 새 검증 proof를 요구할 때 다른 provider가 읽고 있으면 `SnapshotReadersActive`로 기존 proof를 보존한다. 해당 snapshot을 사용하는 worker/view를 정상 stop/unmount한 뒤 같은 명령을 재시도한다. 한 provider의 재연결이 다른 Editor의 읽기 근거를 바꾸지 않으며, 종료된 프로세스의 fence는 OS가 해제한다.

Windows provider 상태의 게시/읽기 경합은 [NTFS 제어 파일 reader](ntfs-workspaces.md)의 최대 1초 복구를 따른다. 지속 공유 오류·손상·소유권 불일치는 상태와 저널을 보존하고 실패한다. 원인을 해결한 뒤 같은 owner/request를 재호출하며 `prepare-worker`의 seal·정상 unmount·소유권 조건을 생략하지 않는다.

## 후보·캐시·결과의 소유권

| 객체 | 고정하는 identity |
| --- | --- |
| worker | project/workspace, owner generation, 원래 VSS tuple, 게시할 source seal |
| candidate | 공통 workflow operation에서 결정한 ID, 원래 worker·source seal, 별도 candidate seal, 원자적 게시 intent/receipt |
| view/cache | view/cache ID, owner generation, 첫 후보 worker의 불변 Library snapshot과 worker ID, Unity/bridge compatibility, partition·예산 |
| source generation | 선택 요청 ID, 해당 candidate와 공통 cache. 이전 출력 journal을 보존 |
| 독립 결과 | frozen artifact/seal, Git result ref, commit 객체의 SHA-256, snapshot 없이 읽는 객체 집합 |

등록 증거가 있는 `.git` 포인터만 후보에서 분리한다. `GitOverlayRegistration.QualifyCandidateSourceAsync`와 [같은 요청 게시 복구](source-view.md#같은-요청의-게시-복구)를 공통 operation ID에 연결한다. worker 명령 잠금과 실제 read-only journal handle을 함께 사용한다. `.git` 외의 사용자 데이터는 임의로 분리하지 않는다.

cache는 `Library/Temp/Logs/UserSettings/obj/.vs/.idea`의 단일 writer다. 첫 선택의 worker snapshot을 `SnapshotBacked=true`인 cache 기준으로 고정한다. 기존 Library는 해당 snapshot에서 직접 읽고 새 쓰기는 private 범위로 저장한다. 최초 Library 전체 복제·읽기 hydration·전체 파일 copy-up이 없다. 원본에 Library가 없으면 실제 cold import가 필요하며, 그 뒤 원본의 Library 생성·변경은 이미 고정한 cache 기준에 들어오지 않는다.

후보를 B로 전환해도 cache의 기준은 A의 원래 snapshot이다. 두 snapshot의 owner·읽기 proof·OS read fence와 가용성을 각각 확인하며 하나라도 불명확하면 정상 mount로 보고하지 않는다. 사라진 기준을 새 snapshot이나 빈 cache로 교체하지 않는다. 다른 VSS의 후보도 동일 프로젝트·Unity/bridge compatibility·partition·예산·view owner일 때 같은 cache를 쓴다. 개발용 `SourceViewSelection.CacheBasis`는 `empty` 또는 `snapshot`을 명시하고, 개발용 view는 snapshot과 cache 전용 `IncludedTopLevel` identity를 전달한다.

`compact-cache`는 Editor/provider/handle/mount의 소유 자원 0을 확인한 뒤에만 실행한다. 예산은 이전 journal과 새 journal의 예상 논리 겹침량이며 실제 볼륨 최고치가 아니다. 현재 유효 private 범위·대체된 payload·frame metadata를 구분하고, 유효 Library를 삭제하지 않는다. [저널 크기 선택](ntfs-workspaces.md#일반-cli-흐름)은 작은 덮어쓰기의 범위 메타데이터가 더 커지면 기존 frame을 남기므로 일부 대체된 payload가 남을 수 있다. 불변 기준의 payload는 읽거나 새 journal로 복사하지 않는다. 기준 없는 임의 초기화와 자동 정리는 제공하지 않는다.

정리 intent·후보 flush·게시·관측·이전 journal 회수·완료 영수증 각 경계에서 중단하면 같은 request key·cache·예산으로 재호출한다. 진행 중 다른 정리나 view start/select/close는 거부한다. 작은 예산의 사전 거부는 cache를 잠긴 상태로 남기지 않는다. 완료 응답 재전송은 원래 영수증을 반환하며 이후의 cache 쓰기를 다시 정리하지 않는다. `status.cacheMaintenance`에서 진행 영수증을 읽을 수 있다. 물리 할당은 Windows에서 확인한 journal 두 세대의 값이며 VSS·Git·임시 파일은 별도로 측정한다.

`GitSourceResult`는 원본 index나 checkout을 수정하지 않는다. frozen source의 논리 트리를 열거하고 변경 경로의 payload만 읽어 raw Git blob/tree/commit 객체를 만든다. 원래 base revision의 미변경 blob은 재사용한다. 필터·checkout 변환을 실행하지 않고 bridge/cache를 제외하며 `refs/splinter/validation-results/<operation-id>`와 객체 존재를 검증한다. 이는 최종 결과의 독립 materialization 비용이며 candidate 게시의 전체 복제 금지와 구분한다.

보존된 source는 고정되어 다시 실행할 수 없다. 새 select를 사용한다. artifact release는 seal을 재확인하고 writer를 거부하는 handle로 journal을 잠근 뒤, 정확한 경로·seal·metadata hash의 회수 intent를 저장하고 그 journal만 unlink한다. 잠금을 닫은 뒤 hash가 같은 알려진 metadata만 제거한다. Windows에서 잠긴 자식 파일을 포함한 디렉터리의 rename에 의존하지 않는다. 중단 후 새 파일·바뀐 metadata·다른 seal은 보존하며, 회수 intent와 독립 ref는 남긴다. cache는 `close-view`로만, worker snapshot은 기존 `workspace release`로만 회수한다.

제품 `workspace release`는 준비된 미완료 intent·candidate·source generation 또는 열린 snapshot cache가 남으면 `SnapshotInUse`로 중단한다. source generation을 모두 회수해도 `close-view`의 영수증과 공통 ledger가 완료되기 전에는 cache 기준 snapshot을 회수할 수 없다. 원본 VSS가 사라진 것으로 추측하거나 snapshot 공동 비용을 0으로 읽지 않는다. 의존 artifact 보존/회수 → cache close → 현재 worker의 `workspace checkpoint` → `workspace release` 순서를 지키며, 기존 정확한 ID·격리 guard·`reclaim-pending` 규칙을 유지한다.

## 지속 상태와 재시도

공통 ledger는 `validation-workflows/<project-hash>/state.json`, 실제 binding·세대·후보·cache·앱/WinFsp control·영수증은 `validation-product/<project-id>/`에 있다. mount는 home의 별도 `validation-mounts/` 경로다. 일반 owner 변경이나 오래된 형식의 자동 이관은 없다.

동일 프로젝트의 view 명령을 직렬화하고, worker 준비/게시에는 기존 workspace operation lock도 사용한다. 공통 ledger는 부작용 전 intent와 보존 전 frozen seal을 flush한다. 같은 디렉터리 임시 파일을 flush하고 `File.Replace` 또는 최초 덮어쓰기 없는 move로 게시한다. reader는 delete sharing을 허용한다. 불완전한/지원하지 않는 JSON과 알 수 없는 파일은 보존 후 거부한다.

중단 뒤에는 같은 명령·키·입력으로 재시도한다. 공통 coordinator는 먼저 기존 provider 영수증을 찾고, 게시의 부분 prefix·완전 저널·유실 receipt는 원래 source seal로만 재조정한다. 완전한 현재 형식의 다음 ledger revision만 명시적 재시도로 게시하며 read-only inspect가 pending을 자동 게시하지 않는다. `replayed=true`는 과거 응답이며 현재 실행 상태의 새 관측이 아니다.

입력과 출력은 `view-lifecycle-request`/`view-lifecycle-data`, 공통 request/response/state는 `view-workflow-*` schema를 사용한다. `ViewWorkflowTests`는 fake 적합성과 CLI 응답, `ImmutableCandidateTests`는 별도 snapshot/cache 저널, `GitSourceResultTests`는 실제 Git 객체·원본 index 보존, `JournalReclamationTests`는 실제 파일 잠금과 회수 중단 복구, `SnapshotWorkspaceCoordinatorTests`는 제품 회수 의존성 연결을 검증한다. 현재 자동 회귀와 실제 실행의 한계는 [저장 view 계약 검증](verification/storage-view-contract.md)을 따른다. 재부팅·전원·물리 공간 장애·일반 에셋·대형 프로젝트는 미자격이며, 생성 source 병합과 모드 선택은 AI 절차다.
