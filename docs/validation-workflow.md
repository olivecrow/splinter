# 검증 후보의 공통 수명주기 계약

SF-03 제품 연결의 Core/Runtime 계약과 fake 적합성 구현이다. 현재 진입점은 `ValidationWorkflowCoordinator.ExecuteAsync`이며 CLI 명령·Windows/Git/Unity provider는 연결되지 않았다. `capabilities.validationWorkflowContract`는 `stage=contract-and-fake`, `cliAvailable=false`, `integratedWorkspaceMode=false`를 반환한다. 개발용 실제 mount 실험은 [validation view](validation-view.md)와 별개다.

## 식별과 책임

| 객체 | 고정하는 내용 |
| --- | --- |
| 작업자 | project/workspace ID, owner ID·양의 generation, 원래 snapshot tuple, 게시할 source seal |
| 불변 후보 | 요청에서 결정된 candidate ID, 원래 작업자 identity·source seal, 별도 candidate seal·provider receipt |
| 검증 view/cache | view/cache ID, owner generation, 원래 snapshot, application compatibility·partition key·예산 |
| 검증 소스 세대 | 선택 요청에서 결정된 generation ID, 해당 후보와 cache identity. A/B/A는 세 소스 세대이며 같은 세대 재개와 구별 |
| 검증 증거 | candidate ID·seal과 source generation·output seal, 성공 여부·완료 시각 |
| 독립 결과 | 정확한 candidate 또는 source-generation artifact·seal, 읽을 수 있는 결과 reference·content hash, snapshot 독립성 |

Core의 `IValidationWorkflowProvider`는 플랫폼 중립 port다. Runtime은 순서·소유권·지속 상태·의존성을 담당한다. provider는 현재 소유자 확인, writer fencing, 원자적 영수증, 정확한 seal의 결과 수집·독립성 검증과 전용 artifact 회수를 담당한다. Runtime의 명령 잠금은 외부 파일 편집을 막지 않으므로 provider가 부작용 안에서도 소유권·자원·seal을 재검사해야 한다.

관리 경로 분리는 `source-only-v1`을 확인한 provider만 허용한다. [개발용 `candidate-source-export`](validation-view.md#등록-증거가-있는-관리-파일-분리)는 실제 등록 증거가 있는 포인터만 별도 후보에서 분리한다. 기존 `candidate-export`의 `.git` 거부를 완화하지 않으며 이 도구가 현재 공통 workflow provider로 연결된 것은 아니다. cache의 원래 snapshot·view·설정이 다르면 재사용을 거부하고 원본 전체 복제나 기준 재작성으로 대체하지 않는다.

## 요청과 응답

`ValidationWorkflowRequest`는 현재 `version=1`, operation, project ID, 안정된 request key, owner를 요구한다. ID는 경로가 아닌 1~128자의 영문·숫자·`._:-` 식별자다. 각 operation은 아래 payload만 받으며 같은 request key의 다른 입력은 거부한다.

| operation | payload | 동작·응답 |
| --- | --- | --- |
| `publish` | worker identity | 원본 snapshot 관측·application/writer/handle 0·mount 부재 확인 후 source-only 후보 게시. intent와 정확한 영수증으로 중복 생성 방지 |
| `select` | candidate ID, cache identity | 자원이 정지한 view에 새 소스 세대 선택. 기존 세대와 cache는 보존. 선택 완료는 실제 mount·Editor ready·검증 성공이 아님 |
| `resume` | generation ID | 현재 선택된 동일 세대의 영수증과 정지 자원 확인. 이전 A를 재사용하려면 새 select로 별도 세대를 만들며, 실제 remount는 후속 adapter 책임 |
| `observe-validation` | generation ID | provider 증거를 해당 후보 seal·소스 세대에만 기록. 실패 증거도 보존하며 worker의 기존 validation 필드에는 쓰지 않음 |
| `preserve` | target kind/ID | candidate 또는 정지한 source 세대의 seal을 고정하고 독립 결과 수집·검증. 보존된 source 세대는 재개/재검증하지 않고 새 세대를 선택 |
| `release` | target kind/ID | 독립 결과 재검증 후 정확한 전용 artifact만 회수. 후보에 남은 소스 세대가 있으면 거부. cache·snapshot·worker를 삭제하지 않음 |

후보 게시 후 worker는 추가 편집할 수 있다. 과거 후보의 성공을 최신 worker의 성공으로 올리지 않는다. Unity가 생성한 source 출력은 별도 artifact로 보존하고 자동 병합하지 않는다. 성공한 컴파일은 결과 보존의 필수 조건이 아니며, 실패한 검증의 출력도 보존할 수 있다.

재전송의 `replayed=true` 응답은 같은 요청의 과거 영수증이다. 현재 source나 선택 세대가 같다는 새 관측이 아니며, 이전 select를 재전송해도 최신 선택을 되돌리지 않는다. 소유자 generation은 완료 요청 재전송에서도 다시 확인한다. `InspectAsync`는 현재 지속 상태를 읽고 `SnapshotDependenciesAsync`는 남은 후보·소스 세대·미완료 게시 intent를 반환한다. 이 의존성 조회는 기존 제품의 snapshot release에 아직 연결되지 않았다.

입력·성공 응답·지속 상태 schema는 `urn:splinter:v1:validation-workflow-request`, `validation-workflow-response`, `validation-workflow-state`다. 공통 정의는 `validation-workflow-contract`에 있다. `ContractCatalog`에서 조회·검증할 수 있고 capability가 ID를 노출한다. 구현된 CLI command 목록이나 `schema <command>`에 실행 가능한 명령으로 추가하지 않는다.

## 중단과 상태 보존

상태는 지정한 store 아래 `validation-workflows/<project-hash>/state.json`에 보관한다. 실행 중인 명령은 provider 호출을 포함해 같은 project의 잠금으로 직렬화한다. 중단 뒤 남은 pending operation은 충돌하는 worker/candidate/view/cache만 막으며 다른 worker의 독립 게시를 막지 않는다. 충돌 오류의 `blockingRequests`는 재개할 원래 입력을 반환한다. FIFO·공정성·일반 임대 인수는 구현하지 않는다.

부작용 전에 intent를 flush하고 게시한다. provider 응답 유실 뒤에는 먼저 정확한 기존 영수증을 찾으며, `null`은 확인된 부재만 뜻한다. 관측 불확실·손상은 오류로 보존한다. provider의 publish/select/preserve/release는 같은 intent의 부분 실행과 응답 유실에 멱등이어야 한다. source 보존은 외부 수집 전에 frozen seal을 한 번 더 저장한다.

상태 저장은 같은 디렉터리의 `state.json.pending`을 flush한 뒤 `File.Replace` 또는 최초 덮어쓰기 없는 move로 게시한다. reader는 delete sharing을 허용한다. read-only inspect는 pending을 게시하지 않는다. 완전한 현재 형식·다음 revision이며 기존 intent·소유권·결과를 보존하는 pending만 명시적 project 명령에서 게시할 수 있다. 외부 부작용의 재시도는 원래 request key·payload를 따른다. 부분 JSON·중복 필드·누락/null·알 수 없는 필드·버전·뒤로 가는 상태·고아 준비는 보존 후 거부하며 삭제나 구버전 이관으로 우회하지 않는다.

동일 버전의 명령 중단·응답 유실 복구를 다루지만, 실제 재부팅·전원·물리 공간 장애를 검증한 것은 아니다. 어댑터가 아직 없는 기능, 다른 cache identity, 오래된 owner, 활성/unknown 자원, 잘못된 증거, 미보존 결과와 의존성은 각각 구별된 오류를 반환한다. 기계용 실패 응답의 재시도 분류는 공통 `WorkspaceFailurePolicy`를 따른다.

## 검증과 지원 경계

`ValidationWorkflowTests`는 실제 임시 JSON 상태, 파일 영수증과 합성 payload를 사용하는 fake 적합성 시험이다. 재생성한 coordinator/provider, 게시·선택·보존·회수의 중단 경계, 응답 유실, A/B/A와 후속 worker 수정, 잘못된 owner/cache/증거, 독립 결과·의존성·손상 보존을 검사한다. `ContractAndCliTests`는 capability의 비활성 경계와 schema 발견을 확인한다. 표준 실행은 `pwsh ./scripts/build.ps1`이며 추적은 [검증 표](plans/validation-matrix.md#sf-03-공통-수명주기-적합성)를 따른다.

SF-03 전체는 Partial, 실제 제품 provider 연결은 NotRun이다. 개발용 VC 성공이나 이 fake 성공을 제품 VSS·WinFsp·Unity A/B/A의 통과로 합산하지 않는다. 실제 어댑터 연결, snapshot 공동 수명, 생성 source 반환 병합, 큐/Coordination, 원본 Editor 공유와 기본 모드 선택은 [남은 계획](plans/storage-priority-and-editor-modes.md)에 있다.
