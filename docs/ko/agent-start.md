# 한정 버전 AI 시작 안내

[English](../agent-start.md)

제품 진입점은 저장소 checkout의 `scripts/splinter.ps1`과 [스킬 원본](../../skills/splinter/SKILL.md)이다. view·probe는 [별도 개발 CLI](development-cli.md)의 경로다. 용어는 [용어집](glossary.md)을 참고한다. 독립 실행이 필요한 지속 작업에만 workspace를 사용하며 현재 경로에서 안전하게 작업할 수 있으면 direct로 작업한다.

## 저장 경로별 시작점

최초 요청의 source/application/storage와 기준을 고정하고 해당 경로의 문서·schema를 읽는다. 한 경로의 서비스 전제나 실제 실행 자격을 다른 경로로 옮겨 적용하지 않는다.

| 저장 경로 | 전제와 사용법 | 실행 근거와 제한 |
| --- | --- | --- |
| `ntfs-snapshot`, source git/none/plastic | [NTFS 계약](ntfs-workspaces.md)과 정확한 서비스/grant·WinFsp가 필요하다. 소스별 조건은 [일반 폴더](vcs-independent-workspaces.md)와 [Plastic 호환](plastic-compatibility.md)을 따른다. | [M0](verification/m0-delivery.md), [일반 폴더](verification/vcs-independent-workspaces.md), [Plastic](verification/plastic-compatibility.md)의 후보별 한정 자격. 일반 폴더의 Editor 없는 원본 반영은 [별도 계약](plain-folder-original-apply.md)의 계획/intent·현재 서비스 guard를 확인한다. |
| `git-snapshot`, source git | [Git 기준본](git-project-basis.md)의 고정 commit을 사용하고 ignored/private 소스는 제외한다. WinFsp가 필요하며 VSS service/grant는 불필요하다. | [reader/Editor 없는 Git](verification/git-project-basis.md), [cold Unity A/B](verification/git-unity-validation.md), [원본 Library/warm Unity](verification/git-library-reference.md)를 구분한다. 실제 Unity 기록은 지정한 후보·입력의 한정 자격이며 대형·ARM·OS 전체 지원을 뜻하지 않는다. |
| `plastic-snapshot`, source plastic | [Plastic 기준본](plastic-project-basis.md)의 고정 changeset/revision과 일반 사용자 cm 접근·WinFsp를 사용한다. VSS service/grant는 불필요하다. | [기준본 검증](verification/plastic-project-basis.md)과 [전체/선택 원본 반영](plastic-original-apply.md)을 확인한다. 원본 반영은 정상 원본 Editor 종료와 별도 고정 계획/intent를 요구하며 서버 이력은 변경하지 않는다. |
| `project-snapshot`, source none | [프로젝트 기준본](plain-project-basis.md)의 명시적으로 승인한 원본 관리형 mount를 사용한다. WinFsp가 필요하며 VSS service/grant는 불필요하다. | [NP 검증](verification/plain-project-basis.md)의 파일·작은 Unity 자격을 확인한다. Library만의 전환 승인으로 전체 원본 mount를 허용하지 않는다. |

Git/Plastic의 원본 Library 재사용은 [별도 Library 전환](git-library-reference.md)의 승인·정상 원본 Editor 종료·반환 basis id를 요구하며 `--library-basis`에 고정한다. `project-snapshot`은 `project prepare --application none|unity`로 source/Library를 같은 NTFS 볼륨에서 복사 없이 이동하고 원본 경로에 writable view를 제공한다. 반환 basis id를 `workspace ensure --source none --storage project-snapshot --project-basis <id>`에 같은 application/home과 함께 고정한다. task 회수 뒤 모든 dependent/reader가 끝나면 명시적 `project restore`로 일반 NTFS 원본을 복원한다.

## NTFS/VSS Git·Unity 경로의 전제와 예시

아래 전제와 생성 예시는 `source=git`, `application=unity`, `storage=ntfs-snapshot` 경로다. 다른 경로는 위 표의 소유 문서를 사용한다.

| 항목 | 확인할 조건 |
| --- | --- |
| 제품 | 기존 개발 checkout의 `scripts/splinter.ps1`과 Release build. 빌드가 없으면 에이전트가 `pwsh ./scripts/build.ps1`로 준비. .NET SDK `10.0.112` 호환 패치·Git·PowerShell 7.2 이상 |
| 원본 | 기존 NTFS의 clean Git repository root, `.git` 디렉터리가 내부에 있고 원본 HEAD와 요청 base가 같음. 캡처 직전 Unity의 변경은 저장돼 있고 import/compile/Play 중이 아니어야 함 |
| 입력 | dirty/staged/untracked·submodule/symlink·LFS/filter/working-tree-encoding은 거부. ADS/hardlink/reparse/EFS/압축/희소 입력과 개별 ACL 의미는 지원하지 않음. 상세 검사 경계는 [NTFS 계약](ntfs-workspaces.md#입력과-권한-경계) |
| 시스템 | 기존 WinFsp와 현재 discard 계약을 지원하는 `SplinterStorageAuthority` 서비스. 사용자·원본·볼륨·home의 정확한 identity가 이미 등록돼 있어야 함 |
| home/공간 | 같은 프로젝트의 모든 작업은 원본 밖의 같은 짧은 NTFS home 사용. 생성/재연결의 상태 볼륨 여유 2 GiB, 새 capture의 기존 VSS diff-area 상한 여유 8 GiB 필요. 구성은 자동 변경하지 않음 |
| Editor | 현재 한정 실행은 Unity `6000.3.22f1`의 작은 fixture·기존 warm Library·독립 Editor 경로. 다른 입력/버전·대형 비용·ARM A/B·OS 장애 보장으로 확대하지 않음 |

일반 CLI·Git·Unity는 일반 사용자 권한이다. 서비스 설치/업데이트·grant 변경이 필요하면 [서비스 설치 계획](storage-privilege-service.md#설치와-발견)을 준비하며, 관리자 Apply는 별도 승인 범위다. 일반 제품 설치·전역 PATH·스킬 자동 발견/대화 binding은 제공하지 않는다.

## 발견과 생성

제품·project/home/base는 프로젝트 정책이나 같은 작업의 인계에서 가져온다. 아래 문자열을 실제 승인된 값으로 채운다. 새 home/key로 실패를 우회하지 않는다.

```powershell
$cli = '<제품 checkout 절대 경로>/scripts/splinter.ps1'
$project = '<등록된 원본 절대 경로>'
$workspaceHome = '<등록된 공통 home 절대 경로>'
$base = '<원본 HEAD와 일치하는 base ref>'
$key = '<이 작업의 고정 key>'

pwsh -NoProfile -File $cli capabilities
pwsh -NoProfile -File $cli schema workspace.ensure
pwsh -NoProfile -File $cli storage service status
pwsh -NoProfile -File $cli doctor --project $project
```

`doctor`는 메타데이터 진단이고 서비스 status는 SCM 관측이다. 성공해도 grant·VSS 여유·Editor 준비·전체 실행 자격을 보증하지 않는다. `doctor.data.readyForExecution=false`와 `checksNotRun`은 실제 실행 미검사와 구분한다. Git 소유권/환경 오류가 나면 정확한 원본·기존 정책을 확인하며 전역 설정이나 소스를 자동 변경하지 않는다.

```powershell
$reply = (& pwsh -NoProfile -File $cli workspace ensure --project $project --key $key --base $base --storage ntfs-snapshot --home $workspaceHome) | ConvertFrom-Json
$reply
```

응답의 `status`·`terminal`·`error`·`retryDisposition`·`nextActions`를 읽는다. `Accepted`는 진행 중이고 실패는 usable workspace가 아니다. ensure의 Succeeded와 실제 준비 상태를 확인한 뒤 `data.workspace.path`를 편집/Git/도구의 working directory와 절대 파일 대상으로 사용한다. 원본 경로로 대체하지 않는다. 도구 권한이 그 경로를 허용하지 않으면 접근 실패와 작업을 보존한다.

대화/인계에는 `$cli/$project/$workspaceHome/$base/$key`, ID·반환 root/control·`requestDigest`·`baseRevision`·snapshot과 명시한 budget 옵션을 남긴다. 재개는 **같은 ensure 입력**으로 하고 ID/root/digest/기준/snapshot이 같은지 확인한다. 임의 파일 편집의 대화별 배타 잠금은 없으므로 한 작업 경로에는 한 작성자만 둔다.

독립 Git 작업은 에이전트가 서로 다른 고정 key와 동일한 project/home/base/Library 기준으로 `workspace ensure` 두 개를 겹쳐 실행할 수 있다. Library는 먼저 한 번 준비하고 반환 작업 경로마다 작성자를 한 명만 둔다. 각 응답을 따로 확인하고 둘 다 성공해야 A/B 전체가 준비된 것으로 판단한다. 실패·응답 유실 시 해당 작업을 보존하고 반환 next action을 확인한 뒤 같은 identity로 재개하며, 타임아웃만으로 쓰기를 중복 실행하지 않는다. 한쪽 실패는 peer 회수의 승인이 아니다. 기존 명령을 조율하는 에이전트 절차이며 제품 큐나 자동 모드가 아니다. [한정 병렬 준비 검증](verification/time-memory-optimization.md)은 작은 Editor 없는 A/B이며 전체 Unity 준비 시간은 측정하지 않는다.

## 편집·Editor·종료

MCP·Unity CLI는 사용자가 설치한 도구 중 에이전트가 필요할 때 선택하는 선택적 외부 도구다. 없어도 Splinter의 저장 격리·기준본·Library/미변경 파일 참조·범위 변경·독립 결과·안전한 원본 반영과 소유 자원 수명 기능은 성립한다. 특정 도구 이름·버전·내부 API는 제품 전제나 gate가 아니다. 도구 선택·연결·refresh·컴파일·Play/Test/build·캡처와 결과 판단은 에이전트가 맡는다. 외부 도구 설치·다운그레이드·수정·별도 수정본 생성/운영은 Splinter 범위에서 제외한다. 선택한 도구의 실제 project/PID/start를 반환 workspace/소유 Editor와 대조한다. 목록 누락만으로 Editor 부재를 판단하지 않고 현재 도구의 도움말/schema를 읽는다. Splinter 브리지는 수명 관측·정상 종료만 담당한다. [대상 선택](alpha-workspaces.md#unity-cli의-정확한-대상-선택)을 참고한다.

필요한 명령의 `schema <command>`를 먼저 읽고 저장한 ID/home을 사용한다. Editor가 필요할 때만 `unity start`; 준비는 status의 실제 `application.ready`로 확인한다. `ready`는 실행 준비 상태다. 편집 뒤 refresh·컴파일·테스트는 선택한 외부 도구로 수행하고 그 결과를 AI가 판정한다. Splinter 응답은 컴파일·테스트 성공을 제공하지 않는다. 일반 status는 반복 전체 소스 검사를 하지 않으며 현재 확인에는 `--verify-source`, 손상 cache 감사에는 `--audit-source`를 구분한다.

`unity start`와 `ensure --editor`의 시작 후 응답도 실행 관측을 사용한다. `sourceVerification=not-checked`이며 현재 소스 검증은 별도로 요청한다. 시작 뒤 import 중 소스 검사 실패를 실행 실패로 혼동하지 않는다.

외부 도구를 사용 중인 Editor 하나가 종료되면 에이전트가 다음 외부 요청 전에 남은 대상의 연결과 identity를 확인한다. 외부 패키지가 공유 서버를 종료하는 문제와 그 복구는 해당 도구/에이전트 절차의 책임이다. 연결 실패는 Splinter 결과 보존/회수의 gate가 아니며 서버·패키지·전역 설정을 자동 변경하지 않는다. [선택적 연결 확인](unity-mcp-response-recovery.md#editor-종료-후-남은-mcp-연결-확인)을 따른다.

| 명시적 의도 | 사용할 경로 |
| --- | --- |
| 나중에 계속 작업 | 같은 workspace 유지. 필요한 경우 `unity stop --id <id> --home <home>`으로 Editor만 정상 종료 |
| 결과 보존 뒤 자원 회수 | 필요한 외부 검증 → 범위 저널 작업은 `workspace checkpoint --id <id> --message <설명> --release --home <home>`. 보존과 회수를 함께 명시적으로 승인하며 원본 통합은 필수가 아님. 별도 checkpoint와 release도 유지 |
| 원본 반영 뒤 자원 회수 | checkpoint → 요청한 깨끗한 현재 원본 branch에 `workspace integrate --id <id> --target <branch> --home <home>` → 명시적 release. 원본 Editor의 통합 후 검증은 별도이며 push/배포 승인이 아님 |
| 실패/미커밋 작업 폐기 | `workspace discard --id <id> --intent-key <고정 intent> --expected-request-digest <저장 digest> --home <home>`. 성공 검증/checkpoint 불필요; 정확한 대상과 기존 폐기 승인을 사용 |

checkpoint는 최종 수집 경계다. 반복 편집/재개는 그 전에 수행한다. Editor가 없거나 컴파일에 실패한 작업도 같은 `workspace checkpoint`로 독립 보존할 수 있다. checkpoint는 소스/저널의 안정성을 확인하며 컴파일 성공을 부여하지 않는다. 선택적 Library/view 저장소 경로는 [개발용 CLI](development-cli.md)로 분리한다. 후보/cache 의존성은 따로 정리하며 연쇄 삭제하지 않는다. 대화/Editor 종료·timeout·임대 만료·디스크 압박은 통합/회수/폐기 의도가 아니다.

## 실패와 완료 판독

| 응답/조건 | 다음 행동 |
| --- | --- |
| 검증 실패·SourceChanged·dirty 원본 | 변경을 보존하고 소스를 수정/현재 검증. 원본 변경을 없애려고 stash/reset하거나 결과를 버리지 않음 |
| DeltaChangedAfterCollection | revision/delta/snapshot 보존. 정확한 소유 Editor 종료·자원/핸들 0과 저널 안정 확인 뒤 같은 ID/home의 checkpoint/release 명시적 재시도 |
| 상태 손상·소유권/실행 unknown·중단된 부작용 | 구조화된 오류와 evidence 보존, 제공한 다음 행동/같은 요청의 회복 조건 확인. JSON·잠금·임시 파일 수동 삭제나 추측 강제 종료로 우회하지 않음 |
| reclaim-pending·discard-reclaim-pending | 논리 종료와 물리 회수 대기 구분. 같은 작업/home의 release 또는 같은 discard intent 재시도만 허용; 다른 snapshot sweep 금지 |
| released·discarded | 실제 mount/Git/delta·정확한 snapshot 부재와 소유 자원 0을 확인한 종료. 기록/로그·독립 결과는 남음. unknown은 0이 아니며 새 편집에는 새 작업 key 사용 |

첫 정상 stop 뒤 지연 Library lock 쓰기가 발생할 수 있다. 외부 도구의 연결·동작 실패는 해당 도구의 근거로 처리한다. 준비/소유 메모리/재시도 비용이 있어 짧은 수정에는 direct를 우선한다. 지원 경로의 승인 쓰기 손실·원본/peer 훼손이 관측되면 해당 경로를 중단하고 먼저 수정한다. 실제 재부팅/전원/물리 공간 장애·현재 바이너리의 모든 native 중단 경계는 보장하지 않는다. 비용과 미확정 게시 오류의 소유 근거는 [대표 사용](verification/representative-agent-workflow.md), 이후 재검증 조건은 [납품 검증](verification/m0-delivery.md)을 따른다.
