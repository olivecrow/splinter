# NTFS snapshot 작업공간

`workspace ensure --storage ntfs-snapshot`은 기존 workspace 수명주기에 VSS 기준과 WinFsp 범위 저널을 연결한다. baseline 준비나 원본 이관은 필요하지 않다. **일반 제품 CLI로 실제 NTFS/Git/Unity 생성·편집·검증·재연결·독립 결과 보존과 회수 단계를 실행했다.** 최종 VSS 해제 상태와 지원 한계는 [NI 검증 기록](verification/ntfs-workspace-integration.md)을 따른다. 독립 SO 실험의 성공을 제품 통합의 성공으로 합산하지 않는다.

## 입력과 권한 경계

- Windows의 기존 NTFS, 설치된 WinFsp, 저장된 clean Git repository root를 대상으로 한다. 첫 Unity 검증 대상은 별도 `splinter-unity` fixture다. 원본의 현재 HEAD와 `--base`가 같아야 하며 `.git` 디렉터리가 원본 안에 있어야 한다.
- dirty/staged/untracked 변경, skip-worktree/assume-unchanged, submodule/symlink, LFS·사용자 filter·working-tree-encoding은 거부한다. 필터는 checkpoint와 결과 보존 시에도 검사한다. 원본을 stash/reset하거나 임의로 커밋하지 않는다.
- 원본과 불변 스냅샷의 메타데이터를 검사하여 ADS, hardlink, reparse, EFS, 압축·희소 파일을 `SnapshotFileUnsupported`로 거부한다. `.git`은 기준 트리에서 제외한다. 이 검사는 파일 payload를 읽거나 복제하지 않는다. 개별 ACL 변경은 지원하지 않으며 overlay가 원본의 모든 ACL 의미를 재현한다고 주장하지 않는다.
- 캡처 직전 호출자는 원본 Unity가 저장된 상태이고 import/compile/Play 중이 아닌지 확인한다. 캡처는 application writer 없는 filesystem crash consistency다. 미저장 메모리를 포함하지 않는다. 캡처 후에는 원본을 계속 수정할 수 있다.
- `--home`은 짧은 경로를 사용한다. NI의 깊은 시험 경로에서는 Unity 패키지 DLL 전체 경로가 270자일 때 초기 로드 예외가 관측됐다. 스크립트 검증과 실행은 통과했지만 긴 패키지 경로와 일반 에셋 로드를 지원으로 확정하지 않는다. 초기 임포트 시간의 SLA도 없다.
- 생성/재연결에는 상태 볼륨의 여유 2 GiB, 새 캡처에는 기존 VSS diff-area 상한의 여유 8 GiB가 필요하다. 설치·볼륨·상한 설정은 자동 변경하지 않는다. 프로젝트 밖 쓰기도 VSS 공간을 사용한다.

VSS capture/release만 관리자 권한이 필요하다. 일반 CLI는 자동 승격하지 않고 `SnapshotPrivilegeRequired`와 정확한 helper 경로·argument list, 그 뒤 재시도할 workspace 명령을 `nextActions`에 반환한다. 승인된 helper만 관리자 권한으로 실행한 뒤 원래 명령을 다시 호출한다. Unity와 일반 CLI를 관리자 계정으로 계속 실행하는 구조가 아니다. helper의 `operation-result.json`에는 성공 또는 진단이 남는다.

## 일반 CLI 흐름

아래 `splinter`는 제품 저장소에서 `pwsh ./scripts/splinter.ps1`이다. 빌드는 `pwsh ./scripts/build.ps1`로 한다. `--home`은 원본 프로젝트 밖의 같은 지속 상태 경로를 모든 작업자가 공유한다.

```text
splinter workspace ensure --project <원본 Unity 저장소> --key <고유한 작업 key> --base development --storage ntfs-snapshot --home <상태 경로>
splinter workspace status --id <workspace.id> --home <상태 경로>
splinter unity start --id <id> --home <상태 경로>
```

첫 ensure는 권한 경계에서 멈출 수 있다. 반환된 helper 실행 후 **같은 입력**으로 ensure를 재호출한다. `--editor`를 주면 준비 후 Editor 실행도 요청한다. Editor의 준비는 `application.ready`로 확인하고, 편집은 반환된 `workspace.path` 안에서만 한다. 작업 key는 프로젝트와 함께 identity를 결정하며 입력 hash, snapshot, delta, 등록, 실행 영수증은 `home`에 남는다. `--max-journal-bytes`는 기본 16 GiB, 최소 4096, 최대 1 TiB이며 작업 생성 뒤 변경할 수 없다.

```text
splinter unity refresh --id <id> --home <상태 경로>
splinter workspace status --id <id> --home <상태 경로>
splinter workspace checkpoint --id <id> --message <결과 설명> --home <상태 경로>
splinter workspace release --id <id> --home <상태 경로>
```

NTFS refresh는 incremental compilation을 요청한다. 일반 제품의 전체 대상 소스 fingerprint를 요청 ID·컴파일 callback·domain reload와 연결한다. 검증 뒤 소스나 `.meta`가 바뀌면 이전 성공은 무효다. `Accepted`는 접수이며 `workspace.validation.success`와 현재 fingerprint 일치가 완료 조건이다. 변경 없는 incremental 요청에서 reload가 없으면 성공을 발행하지 않는다. 현재 실제 실행 관문은 스크립트 변경 후 검증이며 일반 에셋·패키지 검증은 별도다. fixture의 `snapshot-probe.json`은 현재 로드된 코드의 값·MVID·domain 세대를 추가로 기록하며 전체 소스 검증을 대체하지 않는다.

checkpoint는 앱과 자식의 종료를 확인한 뒤 검증한 소스만 커밋하고 `refs/splinter/snapshot-results/<id>`와 `workspace.snapshot.result`를 남긴다. manifest에는 snapshot ID, revision, fingerprint, 결과 ref, 저널 sequence/길이/SHA-256이 있다. 결과 객체가 Git 저장소 안에 있는지 검증한다. 결과 커밋과 기록은 회수 뒤에도 남는다. 새로운 편집에는 새 key를 사용한다.

원본 반영이 필요할 때만 `workspace integrate --id <id> --target development --home <상태 경로>`를 호출한다. 원본은 요청한 브랜치의 clean 상태여야 한다. 충돌 시 이 명령이 시작한 merge만 중단하고 결과를 보존한다. **독립 결과를 보존한 NTFS 작업은 원본 통합 없이 회수할 수 있다.** 실제 NTFS fixture에서 원본 README에 미커밋 변경을 넣고 통합을 요청하면 `GitDirtyWorktree`로 거부하며 원본 HEAD·인덱스·변경 내용과 결과 ref를 보존하는 것을 확인했다. 실패한 검증이나 미커밋 변경은 자동 삭제하지 않는다. 원본 통합·Editor 조작의 공통 절차는 [알파 작업공간](alpha-workspaces.md)을 따른다.

## 지속 상태와 재개

```text
<home>/records/<id>/workspace.json         identity, 단계, validation, 결과 manifest
<home>/records/<id>/events.jsonl           작업 intent와 완료·실패
<home>/records/<id>/snapshot/              소유 VSS 영수증, helper, 입력 검사, 해제 허가
<home>/records/<id>/git-overlay.json       no-checkout 등록과 전용 .git pointer
<home>/records/<id>/overlay/               provider 요청·실행·flush/stop 관측
<home>/records/<id>/delta/                 identity.json, delta.journal
<home>/records/<id>/delta-reclaim.json     정확한 독립 결과와 저널의 삭제 intent
<home>/workspaces/<id>/                   WinFsp가 제공하는 작업 트리
```

준비 순서는 요청 저장 → 소스 checkpoint 확인 → 캡처 intent/영수증 → no-checkout Git 등록 → mount → 독립 index 연결 → 캡처 내용과 checkpoint 비교다. 비교에 실패하면 Ready로 승격하지 않는다. 부분 등록은 원래 경로/branch/owner 영수증과 Git 관리 영역을 대조한다. 같은 key 재시도는 같은 캡처를 사용하고, 원래 캡처가 사라졌다고 현재 원본으로 대체하지 않는다. CLI 종료만으로 Editor/provider를 종료하거나 작업을 회수하지 않는다.

WinFsp의 디렉터리 mount root 자체는 reparse point다. 상태 저장소는 이 경로가 연결돼 있어도 기록을 읽을 수 있으며, Git/Unity는 backend가 현재 소유권·snapshot·mount identity를 확인한 root만 허용한다. 원본·상위 경로·작업 트리 내부의 일반 링크는 계속 거부한다.

provider가 끝나면 앱/자식이 남지 않았음을 확인한 후 같은 ensure로 기존 snapshot/delta에 재연결한다. 실행 intent만 있고 영수증을 확인할 수 없거나 이전 state와 새 실행 세대가 다르면 중복 실행 대신 `OverlayStartUncertain`으로 보존한다. 재부팅·전원 손실 복구는 미검증이다. 캡처와 실제 파일 내용의 불일치 등으로 준비를 완료할 수 없는 작업의 자동 폐기는 제공하지 않는다. `SnapshotPreparationIncomplete` 상태와 해당 영수증을 보존해 조사한다.

## 회수와 관측

회수 순서는 앱/자식 종료 → 현재 source와 독립 결과 확인 → 소유 브리지 제거 → 저널 flush/seal → unmount와 provider 종료 → Git 등록 회수 → delta 회수 → 마지막 의존성이 없는 소유 VSS ID 해제다. 관리 캐시 밖의 ignored 파일, 미보존 변경, 다른 의존 작업이 있으면 중단한다. 중간 쓰기로 저널 seal이 바뀌어도 삭제하지 않는다. delta 회수 중 응답이 유실되면 기존 삭제 intent와 남은 파일을 검증하고 이어 간다. VSS 해제 helper는 정확한 소유 ID만 받으며 전역 삭제를 하지 않는다.

snapshot이 소실된 뒤에는 기존 checkpoint의 독립 결과가 있고 Git ref/객체와 **저널 전체 seal이 동일한 경우에만** 회수할 수 있다. 사용할 수 없는 원본 트리를 다시 읽거나 수정하지 않고 unmount 후 seal을 비교한다. 독립 결과가 없거나 수집 뒤 쓰기가 있으면 결과와 저널을 보존하고 실패한다. 열린 핸들이 남은 unmount는 `OverlayStopPending`이며 핸들을 닫은 뒤 같은 release를 재시도한다.

`workspace.snapshot.observation`은 snapshot/mount/provider/delta 존재, 소유권, 핸들·working set·private bytes, host free space, 저널 잔여량을 제공한다. 누락된 자원 값은 unknown이며 0으로 읽으면 안 된다. `storage`의 생성 payload·읽기 cache·저널 payload/metadata·sequence는 provider 계측이다. 저널 길이는 실제 NTFS file-data allocation이나 VSS 사용량이 아니다. `snapshotStorage.preservedBytes/allocatedBytes`는 현재 일반 호출에서 관측하지 못하므로 생략한다. 관리자 helper의 별도 볼륨 관측과 실제 allocation 계측이 필요하다.

`failureCode`에는 snapshot 소실·압박·provider 실패 또는 마지막 provider I/O 실패 코드가 표시된다. `DeltaFull`이면 기존 데이터는 남으며 현재 작업의 quota를 자동 확장하거나 데이터를 지우지 않는다. 성공한 release에는 앱/자식/provider와 소유 메모리·핸들 0, mount·delta·snapshot 부재를 재관측한 값이 남는다. `storage`의 누적 쓰기량은 마지막 실행의 이력이며 회수 뒤 남은 실제 공간을 뜻하지 않는다. released 조회는 마지막 회수 영수증을 반환한다. OS standby/cache RAM을 비우는 계약은 아니다.

| 오류 | 재개 행동 |
| --- | --- |
| `SnapshotPrivilegeRequired` | `nextActions`의 VSS helper만 승인된 관리자 권한으로 실행한 뒤 같은 workspace 명령 재시도 |
| `RequestKeyConflict` | 저장된 입력으로 재호출하거나 별도 작업에 새 key 사용 |
| `SnapshotMissing`, `SnapshotIdentityMismatch` | 새 원본으로 대체하지 않음. 소실된 snapshot은 기존 독립 결과와 저널 seal이 일치할 때만 release 가능 |
| `SnapshotFileUnsupported`, `SnapshotFilterUnsupported` | 지원하지 않는 입력을 별도 검증 대상으로 유지 |
| `SnapshotHeadroomRequired`, `DeltaFull` | 기존 결과를 보존하고 공간/한도와 helper 관측 확인. 자동 삭제·전체 복사 없음 |
| `OverlayClientsRemain`, `ProcessCleanupIncomplete` | 소유 앱/자식 종료 상태 확인 후 재호출 |
| `OverlayStartUncertain`, `LaunchUncertain` | 정확한 실행 intent·PID/시작 시각·generation 조사. 중복 실행/강제 회수 없음 |
| `UncollectedChanges`, `UnownedIgnoredFiles`, `DeltaChangedAfterCollection` | 유일한 변경을 보존하고 검증/결과 정합성 확인 |

## 구현과 검증 경계

Core의 `ISnapshotWorkspaceBackend`와 `ISnapshotSourceWorkspaceProvider`는 Windows나 Git API를 노출하지 않는다. Runtime은 단계·잠금·결과/의존성 정책, Git 어댑터는 등록·index·결과 ref, Windows 어댑터는 VSS helper·특수 파일 검사·WinFsp·저널 관측/회수를 맡는다. CLI가 이들을 조립하며 Host는 읽기 전용 in-process 계층을 유지한다.

기존 [ReFS 기준본](storage-and-resources.md)은 선택적 경로다. NTFS 실패를 ReFS 이관·전체 checkout·파일 전체 copy-up으로 바꾸지 않는다. 자동 시험의 fake/Git/Windows 메타데이터 증거와 실제 mount/Unity 실행 근거는 NI 기록에서 구분한다. 재부팅, 특수 파일 지원 확대, 긴 경로·일반 에셋/패키지, 대형 프로젝트 성능과 공개 제품 전체는 [후속 범위](plans/ntfs-workspace-integration.md)에 남긴다. 구매 에셋은 이번 NI 시험에 필요하지 않다.
