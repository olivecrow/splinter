# 명시적 폐기의 공통 계약

현재 제공 범위는 Core 계약·Runtime 수명주기·fake 적합성이다. 제품 CLI와 실제 Git/Windows/VSS 폐기 provider는 연결하지 않았다. `capabilities.unavailable`의 `workspace.discard`는 유지되며 `workspace discard`는 실행 명령이 아니다. 현재 제품의 보존형 [release](ntfs-workspaces.md#회수와-관측)는 독립 결과와 seal을 계속 요구한다.

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

workspace·candidate·source generation·cache 의존성이 있으면 정지 전에 차단하며 정확한 의존성과 확인할 다음 행동을 반환한다. snapshot 회수 직전에도 확인한다. 다른 작업·원본 Editor·공유 view/cache를 자동 종료하지 않는다.

`discard-reclaim-pending`은 source/delta 정리 뒤 snapshot 회수를 기다리는 상태다. 회수 영수증과 blocker를 유지하며 같은 intent로 snapshot 단계만 재시도한다. `discarded`는 정확한 snapshot/delta/mount/provider 부재와 소유 자원 0을 확인한 완료 영수증이다. unknown은 0이나 완료가 아니다. 대화·Editor 종료·timeout·공간 압박은 폐기 의도가 아니다.

## 실제 provider 연결의 남은 경계

`IWorkspaceDiscardProvider`가 없으면 `WorkspaceDiscardUnavailable`을 반환하고 intent 작성·Editor 정지·삭제를 수행하지 않는다. 실제 provider는 Git 등록과 작업 branch의 동일 owner/revision 확인·중단 재조정, Windows delta의 별도 삭제 intent·잔여 파일 확인, VSS 서비스의 **명시적 폐기 authorization**을 구현해야 한다. `resultsPreserved=true`를 꾸미거나 기존 release의 독립 결과 gate를 제거해 연결하지 않는다. 권한 서비스 설치/업데이트와 실제 폐기의 자격은 별도 실행 범위다.

이번 범위의 실행 근거는 [공통 폐기 검증](verification/workspace-discard.md), 제품 완료 기준은 [M0](plans/validation-matrix.md#첫-실사용-버전-m0)를 따른다.
