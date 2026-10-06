# 저장 view 계약 검증

현재 [개발 CLI](../development-cli.md)는 `view.execute`와 `view-lifecycle-request`/`view-lifecycle-data`를 사용한다. 공통 후보·소스 세대·cache·독립 결과 계약은 `view-workflow-request`/`view-workflow-response`/`view-workflow-state`, 불변 후보는 `immutable-candidate`다. 입력/schema 식별자는 CLI `schema view.execute`와 `capabilities.viewWorkflowContract`에서 확인한다.

| 경계 | 현재 확인 방법 | 자격 |
| --- | --- | --- |
| workspace 상태 | `WorkspaceCoordinatorTests.UnrecognizedStateFieldsArePreservedAndRejected`: null/값이 있는 알 수 없는 필드의 읽기 거부와 파일 bytes 보존 | 자동/fake |
| 후보·소스 세대·cache 수명 | `ViewWorkflowTests`, `ViewLifecycleStateTests`, `ImmutableCandidateTests`, `ViewCacheMaintenanceTests`: 같은 요청 재전송·부분 게시/응답 유실·A/B/A 소스 분리·cache 기준·정지 정리·소유 자원 guard | 자동/fake 및 일반 파일 |
| 입력/응답/ledger 구조 | `ViewWorkflowTests.EveryRequestResponseAndDurableStateMatchesItsStrictSchema`, `UnsupportedGenerationFieldsCannotBeDroppedDuringResume`: 현재 schema와 Runtime의 미지원 필드 거부·재개/회수 차단·원본 bytes 보존 | 자동/fake |
| 독립 결과·회수 | `GitSourceResultTests`, `JournalReclamationTests`, `SnapshotWorkspaceCoordinatorTests`, `WorkspaceDiscardTests`: raw Git 결과·원본 index/dirty/target·seal·peer·cache/snapshot 의존성·응답 유실 재개 | 자동/fake 및 임시 실제 Git/파일 |
| CLI 후속 관측 | `AcceptedDevelopmentStartProvidesAnExecutableStatusPayload`: 준비 중 `start`의 Accepted 응답과 실제 `view --input`용 status JSON | 자동/fake |

`ViewWorkflowCoordinator`와 `WindowsViewProvider`는 저장 수명을 관리한다. `SourceViewDeltaStore`는 세대별 source journal과 공통 cache journal을 분리하고, `CacheSnapshotTree`는 고정 cache 기준을 읽는다. `ImmutableCandidate`는 snapshot에 의존하는 불변 범위 후보다. `GitSourceResult`의 독립 결과는 컴파일·Play/Test/build의 성공을 나타내지 않는다.

요청 오류는 `ViewRequestInvalid`/`ViewContractUnsupported`, 소유권·자원 오류는 `ViewOwnerMismatch`/`ViewResourcesBusy`/`ViewResourcesUnknown`, receipt/상태 오류는 `ViewReceiptInvalid`/`ViewReceiptMismatch`/`ViewStateCorrupt`다. `ViewStateRecoveryRequired`는 같은 입력의 명시적 재시도를 요구한다. `ViewSourceChanged`/`ViewResultInvalid`는 원래 파일·결과를 보존하며, 열린 소스 세대/cache가 있으면 `ViewDependencyActive` 또는 workspace의 `SnapshotInUse`로 회수를 차단한다. 응답의 보존/재시도 분류는 `WorkspaceFailurePolicy`가 소유한다.

공통 ledger의 `validation-workflows`, provider의 `validation-product`, mount의 `validation-mounts`, Git 결과의 `refs/splinter/validation-results`는 현재 사용 중인 저장 identity다. 해당 조회 경로·작업 ID/잠금 key를 유지하여 workspace와 제한 서비스의 의존성 조회 및 이미 보존한 독립 결과 보호를 연결한다. 현재 parser는 지원하지 않는 필드/요청/손상 상태를 보존하고 거부한다. 변환·추가 reader·자동 복구 fallback은 제공하지 않는다.

2026-10-06, `development` 계열 기준 `dbd24a1`의 격리 워크트리 `C:\Users\olive\.codex\worktrees\8e66\splinter`, 브랜치 `codex/remove-self-validation`에서 `pwsh ./scripts/build.ps1`을 실행했다. locked restore·Release 빌드는 **경고 0·오류 0**, 전체 자동 회귀는 **1,064 Passed, 0 Failed, 9 Skipped**다. 외부 실행/자식 진입점용 조건부 시험 9건은 해당 환경 변수를 설정하지 않아 실행하지 않았다. [빌드 로그](../../artifacts/storage-view-contract/20261006/build.log), [최종 TRX](../../artifacts/storage-view-contract/20261006/final.trx), [소스·바이너리·결과 hash manifest](../../artifacts/storage-view-contract/20261006/manifest.json)를 확인한다. 최초 계약 기대값 불일치 3건은 새 계약으로 수정했고 최초 실패 TRX와 로그도 같은 폴더에 보존했다.

실제 개발 CLI의 [capabilities](../../artifacts/storage-view-contract/20261006/capabilities.json)와 [view schema](../../artifacts/storage-view-contract/20261006/view-schema.json)는 새 식별자·현재 6개 공통 작업을 반환했다. 64개 schema의 참조 누락은 0이며 현재 view 문서 링크도 확인했다. 이 단위는 새 VSS/WinFsp/Unity A/B·서비스 설치·외부 도구 연결·전체 native 장애 행렬을 실행하지 않았다. 설치된 서비스·실행 중 Editor/서버·기존 workspace/result/snapshot은 변경하지 않았다. 실제 입력의 지원 경계는 [검증 안내](README.md)와 각 기능의 실행 기록을 따른다.

같은 날 최신 `development`의 저널 최적화(`62910ad`)를 병합한 코드 후보 `7744ee62accc75293cd721be7052d073ccdc9a8e`에서 필수 `pwsh ./scripts/build.ps1`을 다시 실행했다. locked restore·Release 빌드는 **경고 0·오류 0**, 자동 회귀는 **1,161 Passed, 0 Failed, 10 Skipped**다. 조건부 외부 실행/자식 진입점 9건과 추가된 WinFsp evidence 진입점 1건은 실행하지 않았다. 64개 schema의 참조 누락은 0이다. [통합 빌드 로그](../../artifacts/storage-view-contract/20261006/integration-build.log), [통합 TRX](../../artifacts/storage-view-contract/20261006/integration.trx), [통합 revision·결과·hash manifest](../../artifacts/storage-view-contract/20261006/integration-manifest.json)를 별도로 보존했다. 기존 실패·성공 자료도 유지하며, 워크트리 정리 전에 전체 증거 파일을 원본 저장소의 같은 상대 경로에 복사하고 SHA256을 대조한다.
