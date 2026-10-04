# 한정 버전 AI 시작 안내

`--storage git-snapshot --source git`은 [Git 프로젝트 기준본](git-project-basis.md)의 새 경로다. 기록된 commit만 사용하며 VSS 서비스/grant 없이 기존 WinFsp에서 실행한다. ignored/private/Library는 포함하지 않고 생성 데이터는 private다. 아래 등록·VSS·warm Library 전제는 `ntfs-snapshot`의 기존 자격이며 새 Git 경로로 승계하지 않는다. 실제 Editor 없는 Git 자격과 Unity/대형 NotRun은 [별도 검증](verification/git-project-basis.md)을 따른다.

`--source plastic`의 regular/static 원본과 등록하지 않는 파일 작업공간은 [Plastic 계약](plastic-compatibility.md)을 따른다. 관리 정보/등록을 상속하지 않는 짧은 home·현재 서비스와 [별도 실제 자격](verification/plastic-compatibility.md)을 확인한다. 원본 반영은 거부한다.

현재 `--source none`의 일반 폴더 경로와 v2 상태/서비스 패키지는 [VCS 없는 작업공간](vcs-independent-workspaces.md)을 따른다. Editor 없는 원본 반영은 [계획/intent 사용법](plain-folder-original-apply.md)의 정확한 digest·최신 서비스 guard와 작은 Editor 없는 제품 A/B의 한정 자격을 확인한다. 아래 Git 경로와 과거 native 자격을 일반 폴더 지원으로 승계하지 않는다.

현재 제공물은 **등록된 Windows x64 NTFS/Git/Unity 환경의 개발 CLI와 저장소 사용 스킬**이다. 에이전트는 [스킬 원본](../skills/splinter/SKILL.md)을 먼저 읽고, 독립 실행이 필요한 지속 작업에만 workspace를 사용한다. 현재 경로에서 안전하게 작업할 수 있으면 direct로 작업한다. 실행 자격과 M0 완료 판정은 [한정 납품 검증](verification/m0-delivery.md)이 소유한다.

## 시작 전 전제

| 항목 | 확인할 조건 |
| --- | --- |
| 제품 | 기존 개발 checkout의 `scripts/splinter.ps1`과 Release build. 빌드가 없으면 에이전트가 `pwsh ./scripts/build.ps1`로 준비. .NET SDK `10.0.112` 호환 패치·Git·PowerShell 7.2 이상 |
| 원본 | 기존 NTFS의 clean Git repository root, `.git` 디렉터리가 내부에 있고 원본 HEAD와 요청 base가 같음. 캡처 직전 Unity의 변경은 저장돼 있고 import/compile/Play 중이 아니어야 함 |
| 입력 | dirty/staged/untracked·submodule/symlink·LFS/filter/working-tree-encoding은 거부. ADS/hardlink/reparse/EFS/압축/희소 입력과 개별 ACL 의미는 지원하지 않음. 상세 검사 경계는 [NTFS 계약](ntfs-workspaces.md#입력과-권한-경계) |
| 시스템 | 기존 WinFsp와 현재 discard 계약을 지원하는 `SplinterStorageAuthority` 서비스. 사용자·원본·볼륨·home의 정확한 identity가 이미 등록돼 있어야 함 |
| home/공간 | 같은 프로젝트의 모든 작업은 원본 밖의 같은 짧은 NTFS home 사용. 생성/재연결의 상태 볼륨 여유 2 GiB, 새 capture의 기존 VSS diff-area 상한 여유 8 GiB 필요. 구성은 자동 변경하지 않음 |
| Editor | 현재 한정 실행은 Unity `6000.3.22f1`의 작은 fixture·기존 warm Library·독립 Editor 경로. 다른 입력/버전·대형 비용·ARM A/B·OS 장애 보장으로 확대하지 않음 |

일반 CLI·Git·Unity는 일반 사용자 권한이다. 서비스 설치/업데이트·grant 변경이 필요하면 [서비스 설치 계획](storage-privilege-service.md#설치와-발견)을 준비하며, 관리자 Apply는 별도 승인 범위다. 이번 안내는 일반 제품 설치·전역 PATH·스킬 자동 발견/대화 binding을 제공하지 않는다.

## 발견과 생성

제품·project/home/base는 프로젝트 정책이나 같은 작업의 인계에서 가져온다. 아래 문자열을 실제 승인된 값으로 채운다. 새 home/key로 실패를 우회하지 않는다.

```powershell
$cli = '<제품 checkout 절대 경로>/scripts/splinter.ps1'
$project = '<등록된 원본 절대 경로>'
$home = '<등록된 공통 home 절대 경로>'
$base = '<원본 HEAD와 일치하는 base ref>'
$key = '<이 작업의 고정 key>'

pwsh -NoProfile -File $cli capabilities
pwsh -NoProfile -File $cli schema workspace.ensure
pwsh -NoProfile -File $cli storage service status
pwsh -NoProfile -File $cli doctor --project $project
```

`doctor`는 메타데이터 진단이고 서비스 status는 SCM 관측이다. 성공해도 grant·VSS 여유·Editor 준비·전체 실행 자격을 보증하지 않는다. `doctor.data.readyForExecution=false`와 `checksNotRun`은 실제 실행 미검사와 구분한다. Git 소유권/환경 오류가 나면 정확한 원본·기존 정책을 확인하며 전역 설정이나 소스를 자동 변경하지 않는다.

```powershell
$reply = (& pwsh -NoProfile -File $cli workspace ensure --project $project --key $key --base $base --storage ntfs-snapshot --home $home) | ConvertFrom-Json
$reply
```

응답의 `status`·`terminal`·`error`·`retryDisposition`·`nextActions`를 읽는다. `Accepted`는 진행 중이고 실패는 usable workspace가 아니다. ensure의 Succeeded와 실제 준비 상태를 확인한 뒤 `data.workspace.path`를 편집/Git/도구의 working directory와 절대 파일 대상으로 사용한다. 원본 경로로 대체하지 않는다. 도구 권한이 그 경로를 허용하지 않으면 접근 실패와 작업을 보존한다.

대화/인계에는 `$cli/$project/$home/$base/$key`, ID·반환 root/control·`requestDigest`·`baseRevision`·snapshot과 명시한 budget 옵션을 남긴다. 재개는 **같은 ensure 입력**으로 하고 ID/root/digest/기준/snapshot이 같은지 확인한다. 임의 파일 편집의 대화별 배타 잠금은 없으므로 한 작업 경로에는 한 작성자만 둔다.

## 편집·Editor·종료

작업 내용·도구/모드 선택·충돌 해결·게임 검증은 AI와 기존 파일/Git·MCP·Unity CLI의 책임이다. 외부 도구가 선택한 projectRoot는 반환 workspace와 일치해야 한다. Unity CLI의 [정확한 대상 선택 절차](alpha-workspaces.md#unity-cli의-정확한-대상-선택)와 [작은 T: A/B 자격](verification/unity-cli-handoff.md)을 확인한다. 해당 버전에서 status/목록이 worker를 누락해도 같은 반환 경로의 command는 연결되므로 누락을 Editor 부재로 판정하지 않는다. 명령의 실제 target/project/PID를 대조하며 reload 뒤 포트를 고정하지 않는다. MCP·다른 입력의 자격은 별도로 확인해야 한다. 현재 Coplay 브리지는 수명 관측·정상 종료·peer 보호를 담당하며 자체 컴파일 요청이나 성공 판정을 제공하지 않는다. 연결 범위는 [AI 사용 계획](plans/session-workflow-and-installation.md#외부-도구-인계의-한정-범위)을 따른다.

필요한 명령의 `schema <command>`를 먼저 읽고 저장한 ID/home을 사용한다. Editor가 필요할 때만 `unity start`; 준비는 status의 실제 `application.ready`로 확인한다. `ready`는 실행 준비 상태다. 편집 뒤 refresh·컴파일·테스트는 선택한 외부 도구로 수행하고 그 결과를 AI가 판정한다. Splinter 응답은 컴파일·테스트 성공을 제공하지 않는다. 일반 status는 반복 전체 소스 검사를 하지 않으며 현재 확인에는 `--verify-source`, 손상 cache 감사에는 `--audit-source`를 구분한다.

`unity start`와 `ensure --editor`의 시작 후 응답도 실행 관측을 사용한다. `sourceVerification=not-checked`이며 현재 소스 검증은 별도로 요청한다. 시작 뒤 import 중 소스 검사 실패를 실행 실패로 혼동하지 않는다.

| 명시적 의도 | 사용할 경로 |
| --- | --- |
| 나중에 계속 작업 | 같은 workspace 유지. 필요한 경우 `unity stop --id <id> --home <home>`으로 Editor만 정상 종료 |
| 결과 보존 뒤 자원 회수 | 필요한 외부 검증 → `workspace checkpoint --id <id> --message <설명> --home <home>` → 명시적 `workspace release`. 원본 통합은 필수가 아님 |
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
