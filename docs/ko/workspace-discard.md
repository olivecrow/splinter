# 명시적 workspace 폐기

[English](../workspace-discard.md)

현재 `--source none`의 일반 폴더 경로와 v2 상태/서비스 패키지는 [VCS 없는 작업공간](vcs-independent-workspaces.md)을 따른다. 아래 Git 경로와 과거 native 자격을 일반 폴더 지원으로 승계하지 않는다.

`workspace discard`는 준비가 완료된 NTFS task worker를 성공 검증/checkpoint 없이 명시적으로 폐기한다. Core/Runtime과 실제 Git/Windows provider·CLI를 연결했다. 실제 VSS 폐기에는 이 계약을 지원하는 권한 서비스 패키지가 필요하며 서비스 업데이트는 별도 관리자 작업이다. [폐기 검증](verification/workspace-discard.md)은 자동 권한 경계·로컬 Editor 없는 VSS A/B·같은 intent 재개/중복을, [대표 사용](verification/representative-agent-workflow.md)은 Unity 컴파일 실패·미커밋 worker의 정상 stop/실제 폐기를 다룬다. native 강제 중단과 일반 지원은 별도 미실행 범위다.

```powershell
pwsh ./scripts/splinter.ps1 workspace status --id <id> --home <home>
pwsh ./scripts/splinter.ps1 workspace discard --id <id> --intent-key <stable-discard-key> --expected-request-digest <requestDigest> --home <home>
```

대상은 status/list의 ID·project/path와 `requestDigest`로 확인한다. 중단/지연은 같은 key/digest/id/home으로 재시도한다. `Succeeded`/`discarded`는 정확한 소유 자원 부재, `Accepted`/`discard-reclaim-pending`은 작업 payload 제거 뒤 공유 VSS blocker를 기다리는 상태다. 기존 보존형 [release](ntfs-workspaces.md#회수와-관측)는 독립 결과·seal을 계속 요구한다.

## 입력과 소유권

`WorkspaceCoordinator.DiscardAsync`는 `WorkspaceDiscardRequest(id, intentKey, expectedRequestDigest)`를 받는다. `intentKey`는 명시적 폐기 요청의 고정 키, `expectedRequestDigest`는 대상 workspace의 기존 요청 digest다. 둘을 대조한 뒤 workspace 잠금 안에서 intent를 저장한다. 지원 범위는 준비가 완료된 NTFS task worker이며 불완전 준비·기준본·다른 storage·기존 release/compaction 진행은 거부한다.

intent는 workspace ID·project/path·요청 digest·branch/base·현재 source revision·전체 snapshot tuple을 고정한다. 성공 검증·checkpoint·독립 결과를 새로 만들지 않는다. 이미 별도로 보존한 독립 결과 ref와 공유 Git 객체는 유지하며 그 결과의 폐기는 현재 요청 범위에 포함하지 않는다.

입력은 `urn:splinter:v1:workspace-discard-request`, 지속 상태와 응답의 선택적 `workspace.discard` 필드는 `urn:splinter:v1:workspace-discard-state`를 따른다. missing/손상/다른 owner·stage·snapshot은 보존 후 거부한다. 현재 버전만 지원하며 이전 intent를 자동 이관하거나 새 intent로 대체하지 않는다.

## 단계와 중단 재개

```text
공유 의존성 확인 → 읽기 전용 source 소유권 확인 → intent 저장
stop → seal 고정 → unmount → source 등록/작업 branch → delta → snapshot → complete
```

Runtime은 앱/자식의 소유권과 프로세스·메모리 0을 요구한다. 고정 seal과 unmount 전후를 비교하며 새 쓰기는 `DiscardDeltaChanged`로 보존한다. provider/mount/핸들·메모리 0과 실제 mount 경로 부재를 확인한 뒤 source/delta 제거를 요청한다. 삭제 provider는 고정 source revision·정확한 등록/파일 allowlist·독립 결과 보호를 검증하고 부분 삭제 intent를 게시한 뒤 같은 요청을 재조정해야 한다. Runtime의 다음 단계 저장이 유실돼도 이전 외부 효과를 중복 생성하지 않아야 한다.

폐기 진행 중에는 ensure/Editor 시작·refresh·stop/checkpoint/integrate/release/compact로 다른 수명주기를 시작하지 않는다. status는 소스 파일을 읽거나 검증 응답을 성공으로 승계하지 않고 관측과 폐기 stage를 반환한다. 같은 intent key/digest의 `DiscardAsync`만 남은 단계를 이어 간다.

workspace·candidate·source generation·cache 의존성이 있으면 정지 전에 차단하며 정확한 의존성과 확인할 다음 행동을 반환한다. snapshot 회수 직전에도 확인한다. 이때 최신 mount/provider·delta 부재와 소유 자원 0 관측을 durable workspace 상태에 게시한 뒤 별도 서비스에 요청한다. 상태 게시 실패는 회수 요청 전에 중단한다. 다른 작업·원본 Editor·공유 view/cache를 자동 종료하지 않는다.

`discard-reclaim-pending`은 source/delta 정리 뒤 snapshot 회수를 기다리는 상태다. 회수 영수증과 blocker를 유지하며 같은 intent로 snapshot 단계만 재시도한다. `discarded`는 정확한 snapshot/delta/mount/provider 부재와 소유 자원 0을 확인한 완료 영수증이다. unknown은 0이나 완료가 아니다. 대화·Editor 종료·timeout·공간 압박은 폐기 의도가 아니다.

## 실제 provider와 권한 경계

`WindowsWorkspaceDiscardProvider`는 일반 권한 Git·delta 처리를 조합한다. Git은 생성된 `splinter/<id>` branch와 정확한 owner lock/등록 receipt를 확인하고, `git-discard.json` intent를 게시한 뒤 등록을 제거한다. 고정 revision의 compare-and-delete와 `--no-deref`로 task branch만 제거하며 독립 결과 ref·공유 객체·원본과 peer를 보존한다. 응답 유실 뒤 등록/branch가 이미 없어도 동일 intent만 완료한다. 재생성되거나 변경된 branch는 보존 후 거부한다.

Windows delta는 owner intent와 `discard-delta-reclaim.json`에 정확한 디렉터리·seal·identity hash를 기록한다. writer를 배제하는 journal fence에서 hash/길이를 확인하고 `delta.journal`·`identity.json`만 삭제한다. 예상 밖 파일·identity/journal 변경은 거부하며 부분 삭제 후 같은 기록으로 나머지 파일만 처리한다. 현재 계약은 진행 중인 preparation/compaction의 강제 정리를 지원하지 않는다.

VSS는 기존 고정 `Release` IPC operation에 별도 `release.discard` authorization을 전달한다. 서비스는 인증된 사용자·설치 scope·보호된 capture tuple, durable discard state/intent/seal, 실제 Git 등록/branch·mount/delta 부재, 소유 자원 0·공유 의존성·독점 reader fence를 대조한다. 보호된 `discard-authorized.json`을 helper에 전달하며 `resultsPreserved=true`를 만들지 않는다. live identity·정확한 snapshot ID·기존 삭제 격리 guard와 지연 회수 영수증을 유지한다. 이전 서비스에 호환 fallback이나 자동 승격을 추가하지 않는다.

제품 완료 기준은 [M0](plans/validation-matrix.md#첫-실사용-버전-m0)를 따른다. provider를 별도로 조합하지 않은 .NET 호출은 `WorkspaceDiscardUnavailable`로 intent/정지 전에 거부한다.
