# 병렬 Unity 스크립트 알파

> 공통 Unity 작업 흐름과 기존 ReFS 예시를 설명한다. 일반 CLI에 연결한 `ntfs-snapshot`의 생성·권한·독립 결과·회수 차이는 [NTFS 작업공간](ntfs-workspaces.md)을 따른다. 실제 VSS/Unity 관문은 [NI 검증](verification/ntfs-workspace-integration.md)에 별도로 기록하며 기존 ReFS 성공으로 대체하지 않는다.

AI 작업자는 Editor가 필요한 시점에 이 CLI로 자신의 프로젝트와 Editor를 확보한다. 작업 도중 대화나 CLI가 끝나도 작업공간과 Editor는 남는다. 같은 `--home`, `--project`, `--key`로 다시 접속한다. 완료 후 명시적으로 결과를 보존하고 필요한 통합과 회수를 수행한다.

현재 범위는 Windows의 간단한 C# 스크립트 편집과 컴파일이다. NTFS에는 [불변 snapshot 모드](ntfs-workspaces.md)를, 선택적 ReFS에는 [예열 기준본과 CoW](storage-and-resources.md)를 사용한다. 명시적인 `git-checkout`은 새 Library를 만드는 진단 모드다. 원본은 최초 커밋이 있고 깨끗한 Git 저장소 루트의 Unity 프로젝트여야 한다. 해당 Unity 패치가 Hub 기본 경로에 설치되어 있어야 하며 다른 위치는 `SPLINTER_UNITY_EDITOR`로 지정한다. 실제 환경별 지원 증거는 [자원 검증](verification/storage-and-resources.md)을 확인한다.

## 에이전트의 기본 흐름

빌드 후 저장소 루트에서 `pwsh ./scripts/splinter.ps1`을 실행 접두사로 쓴다. 아래 예시의 `splinter`를 이 접두사로 바꾼다. `--home`은 모든 작업자가 공유하는 지속 상태 디렉터리이며 대상 Unity 저장소 **밖**에 둔다. 생략하면 `SPLINTER_HOME`, 없으면 사용자 LocalApplicationData/Splinter다.

```text
splinter workspace ensure --project <Unity 저장소> --key <안정된 작업 이름> --storage refs-cow --baseline <봉인된 기준본 ID> --base <기준본 커밋> --editor --home <ReFS 상태 경로>
splinter workspace status --id <응답의 workspace.id> --home <상태 경로>
```

`data.application.ready=true`가 될 때까지 10초 정도 간격을 두고 status를 조회한다. 최초 기준본의 cold 준비는 패키지 해석과 새 Library 임포트에 수 분이 걸릴 수 있다. CoW 작업도 경로 변경과 소유 브리지 추가에 따른 갱신이 필요하다. `running=true`와 로그 진행이 유지되면 짧은 대기만으로 실패로 판정하지 않는다. **반환된 `data.workspace.path`에서만** 수정한다. 원본 경로나 다른 작업자의 경로를 수정하지 않는다. Editor가 필요하지 않으면 `--editor`를 생략하고 나중에 `unity start --id`로 연다.

fixture의 Unity `6000.3.22f1`은 C# 9 문법을 사용한다. 예를 들어 file-scoped namespace 대신 `namespace Example { ... }`를 쓴다. 다른 Unity 패치의 실행 검증은 별도다.

```text
splinter unity refresh --id <id> --home <상태 경로>
splinter workspace status --id <id> --home <상태 경로>
```

refresh는 대상 Editor를 활성화하고 명시적 AssetDatabase refresh와 스크립트 컴파일을 요청한다. ReFS/진단 모드는 clean compilation, NTFS는 incremental compilation을 사용한다. `Accepted`는 접수다. status는 새 검증 응답이 도착했을 때 소스를 한 번 읽어 확인한다. 이 응답의 `sourceVerification=verified`, `workspace.validation.success=true`, `validationCurrent=true`가 검증 성공의 조건이다. 오류는 `workspace.validation.errors`에 있다. 수정 후 refresh를 다시 실행한다. Unity가 새 `.meta`를 만들면 fingerprint가 바뀌어 첫 검증이 거부될 수 있으므로, 파일이 안정된 뒤 재검증한다. Play와 저장하지 않은 씬은 검증·종료를 차단한다.

작업을 다음 대화에서 이어가려면 같은 ensure 또는 `workspace list --project`와 status를 사용한다. `unity stop`은 Editor만 종료한다. 작업이 끝났을 때:

```text
splinter workspace checkpoint --id <id> --message <결과 설명> --home <상태 경로>
splinter workspace integrate --id <id> --target development --home <상태 경로>
splinter workspace release --id <id> --home <상태 경로>
```

checkpoint는 검증한 소스만 커밋한다. NTFS는 앱/자식 종료를 먼저 확인하고 독립 result ref/manifest까지 남기며 원본 통합 없이 release할 수 있다. integrate는 작업 Editor를 종료하고 원본의 현재 브랜치에 merge한다. 원본이 요청한 브랜치에 있고 깨끗해야 한다. 충돌은 이 명령이 시작한 merge만 중단하고 결과 worktree를 남긴다. 여러 결과의 통합은 저장소별로 직렬화한다. 통합한 원본은 원본 Editor에서 별도로 새로고침·검증한다. release는 결과가 회수된 깨끗한 worktree만 삭제하며 기록·로그·결과 브랜치는 유지한다. 자동 타임아웃 삭제는 없다.

release 완료에는 Editor뿐 아니라 자식과 감시 프로세스의 종료 증거가 필요하다. `workspace.releasedResources`의 소유 프로세스 수·working set·private bytes가 모두 0이며 실제 worktree 경로도 제거되어야 한다. 재사용 기준본은 별도 디스크 캐시로 유지되며 `baseline release`로 명시적으로 회수한다.

Windows Git 호출에는 `core.longpaths=true`를 명령별로 적용한다. 사용자나 저장소 설정은 바꾸지 않는다. Git이 등록만 해제하고 폴더 일부를 남긴 경우에도 같은 release를 재시도할 수 있다. 저장된 회수 intent, 결과 브랜치와 통합 포함 여부를 확인하고, 남은 소스 파일은 Git blob과 일치해야 삭제한다. 관리 캐시 밖의 알 수 없는 파일이나 변경된 소스, reparse point는 보존하고 실패를 반환한다. 이 복구는 손상되거나 임의로 옮긴 작업공간 전체의 자동 복원을 뜻하지 않는다.

## 상태와 복구

| 관측 | 행동 |
| --- | --- |
| `Accepted`, `editor-starting`, `validating` | status 조회. 아직 성공으로 기록하지 않는다. |
| `validation-failed`, `ValidationRequired`, `SourceChanged` | 오류와 현재 소스 확인 → 수정 → refresh → status. |
| `EditorFocusRequired` | Windows가 활성화를 거부했다. 해당 프로젝트 Editor를 활성화한 뒤 refresh 재시도. |
| `WorkspaceBusy`, `EditorNotReady` | 같은 명령 또는 status 재조회. |
| `UncollectedChanges`, `GitDirtyWorktree` | 변경을 보존하고 검증·checkpoint를 완료한다. ReFS/진단 경로는 integrate도 필요하다. |
| `IntegrationConflict` | 원본과 결과를 보존해 충돌을 해결한 새 작업을 진행한다. |
| `LaunchUncertain`, `EditorStopTimeout`, `ProcessOwnershipUnknown` | `controlPath/editor.log`와 해당 PID/시작 시각을 조사한다. 강제 종료·강제 회수하지 않는다. |
| `released` | 종료된 작업이다. 새 편집에는 새 key를 쓴다. |

`workspace list`는 저장된 상태이며 일반 status는 프로세스와 새 검증 응답을 관측한다. 새 결과가 없는 반복 조회는 프로젝트 파일을 읽지 않고 `sourceVerification=not-checked`를 반환하며 `source`와 `validationCurrent`를 생략한다. 저장된 validation을 현재 파일의 검증으로 해석하지 않는다. 현재 파일까지 다시 확인하려면 `workspace status --id <id> --verify-source --home <상태 경로>`를 쓴다. checkpoint/integrate는 항상 현재 소스를 확인한다. JSON 응답에 프로세스 제어 토큰을 노출하지 않는다. 권한이 필요한 환경에서는 대상 저장소와 상태 경로에 대한 정상 파일·프로세스 권한으로 실행한다.

## 구현 경계

`Splinter.Runtime`은 짧게 실행되는 명령 조정 계층이다. `WorkspaceStore`가 atomic JSON 교체와 프로세스 간 파일 잠금으로 상태를 유지한다. intent를 저장한 뒤 어댑터 부작용을 실행한다. daemon·SQLite는 없다. 생성 직후의 불확실한 프로세스 실행은 자동 중복 실행 대신 보존 오류를 반환한다.

Git 어댑터는 독립 worktree/index를 사용하고 Unity 어댑터는 별도 프로젝트·Library·프로세스·제어 디렉터리를 사용한다. PID·시작 시각·실행 파일·토큰·프로젝트 경로를 확인한다. `Assets/__Splinter__`의 소유 브리지는 컴파일 이벤트와 domain reload를 관측한다. 해당 경로는 사용자 편집 대상이 아니며 결과 커밋에서 제외한다.

Coplay `10.2.0`은 HTTP 서버 소유 정보를 전역 EditorPrefs에서 읽어 작업 Editor 종료가 원본 서버를 종료할 수 있다. 소유 브리지는 해당 버전의 `McpEditorShutdownCleanup.OnEditorQuitting` 콜백을 **작업 프로세스 안에서만** 해제한다. 패키지 파일과 전역 설정은 수정하지 않는다. 작업 Editor에서는 Coplay 서버를 시작하지 않고 Splinter의 전용 브리지를 사용한다. 이 호환 처리는 최초 초기화와 domain reload마다 적용되며 heartbeat의 `coplayShutdownGuard`에 기록한다. 다른 패키지 버전의 호환성은 별도 검증 대상이다.

Editor가 종료하면서 기존 에셋의 직렬화 형식을 갱신하면 파일은 보존되고 기존 검증 hash가 무효화된다. 바뀐 파일을 검토한 뒤 다시 검증한다. 이번 fixture의 Mobile URP 에셋이 이 경로에 해당한다. CLI 실행 접두사는 .NET 도구 상태 경로를 제품 저장소 기준 절대 경로로 정규화하여 Unity 작업공간에 도구 sentinel 파일을 생성하지 않게 한다.

사용자 전역 EditorPrefs, 라이선스, CPU/GPU, 외부 서비스는 공유된다. 보안 샌드박스가 아니며 다른 작업공간 접근을 OS 수준에서 금지하지 않는다. 씬·프리팹 편집, Play, 테스트 실행, 빌드, DAG 실행과 자동 에이전트 시작은 미구현이다. 저장소와 프로세스 그룹의 지원 범위·실제 증거는 [저장소·자원 관리](storage-and-resources.md)를 따른다. 각 대화의 에이전트가 CLI를 직접 사용할 수 있고 별도 조정 AI는 명령의 전제조건이 아니다. 하나의 대상 저장소에는 하나의 공통 `--home`을 사용한다. 제품의 설치·사용 여부 판단·Codex 대화 자동 연결과 반영 없는 discard는 아직 구현하지 않았다. [대화별 작업과 설치 계획](plans/session-workflow-and-installation.md)을 현재 실행 가능한 기능으로 해석하지 않는다.

## 변경 기반 검사와 보존 제한

NTFS의 현재 소스 확인은 `workspace status --verify-source`, 전체 감사와 거부된 manifest 보존/재작성은 `--audit-source`다. `sourceVerification=verified`·`validationCurrent=true`·실제 validation 성공을 함께 확인한다. 파일 hash 재사용은 snapshot/provider epoch/소스 변경 이력에 묶이며 초기·감사·provider 재연결 비용은 남는다. [현재 계약](source-verification.md)을 따른다.

VSS의 정확한 ID 해제 중 기존 다른 snapshot이 소실된 [실행 기록](verification/storage-foundation.md)이 있다. 오래된/시각 불명 동일 볼륨 snapshot이 있으면 현재 helper는 회수를 차단하고 독립 결과를 보존한다. 기존 snapshot을 삭제해 우회하지 않는다. 격리 볼륨에서의 실제 보존 확인 전에는 이 알파를 기존 복구 지점의 보존이 검증된 기능으로 취급하지 않는다.
