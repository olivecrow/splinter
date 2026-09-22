# NTFS snapshot 작업공간

`workspace ensure --storage ntfs-snapshot`은 기존 workspace 수명주기에 VSS 기준과 WinFsp 범위 저널을 연결한다. baseline 준비나 원본 이관은 필요하지 않다. **일반 제품 CLI로 실제 NTFS/Git/Unity 생성·편집·검증·재연결·독립 결과 보존과 회수 단계를 실행했다.** 최종 VSS 해제 상태와 지원 한계는 [NI 검증 기록](verification/ntfs-workspace-integration.md)을 따른다. 독립 SO 실험의 성공을 제품 통합의 성공으로 합산하지 않는다.

## 입력과 권한 경계

- Windows의 기존 NTFS, 설치된 WinFsp, 저장된 clean Git repository root를 대상으로 한다. 첫 Unity 검증 대상은 별도 `splinter-unity` fixture다. 원본의 현재 HEAD와 `--base`가 같아야 하며 `.git` 디렉터리가 원본 안에 있어야 한다.
- dirty/staged/untracked 변경, skip-worktree/assume-unchanged, submodule/symlink, LFS·사용자 filter·working-tree-encoding은 거부한다. 필터는 checkpoint와 결과 보존 시에도 검사한다. 원본을 stash/reset하거나 임의로 커밋하지 않는다.
- 원본과 불변 스냅샷의 메타데이터를 검사하여 ADS, hardlink, reparse, EFS, 압축·희소 파일을 `SnapshotFileUnsupported`로 거부한다. `.git`은 기준 트리에서 제외한다. 이 검사는 파일 payload를 읽거나 복제하지 않는다. 개별 ACL 변경은 지원하지 않으며 overlay가 원본의 모든 ACL 의미를 재현한다고 주장하지 않는다.
- reparse/EFS/압축/희소 속성은 기준 트리의 지연 읽기와 WinFsp create/overwrite/속성 변경에도 같은 규칙으로 거부한다. overwrite는 기존 길이를 줄이기 전에 검사한다. 거부된 callback은 바이트·메타데이터·저널 sequence를 유지하며 OS에는 `STATUS_NOT_SUPPORTED`를 반환한다. ARM64 실제 mount에서는 ADS·hardlink·희소·압축·mount-point reparse 요청 5개의 거부를 일반 NTFS 양성 대조와 함께 확인했다. 불변 입력은 fake이며 VSS 입력·EFS·ACL·특수 FSCTL의 전체 검증은 남아 있다. 실행 범위는 [NR 기록](verification/ntfs-recovery.md)을 따른다.
- 캡처 직전 호출자는 원본 Unity가 저장된 상태이고 import/compile/Play 중이 아닌지 확인한다. 캡처는 application writer 없는 filesystem crash consistency다. 미저장 메모리를 포함하지 않는다. 캡처 후에는 원본을 계속 수정할 수 있다.
- `--home`은 짧은 경로를 사용한다. NI의 깊은 시험 경로에서는 Unity 패키지 DLL 전체 경로가 270자일 때 초기 로드 예외가 관측됐다. 스크립트 검증과 실행은 통과했지만 긴 패키지 경로와 일반 에셋 로드를 지원으로 확정하지 않는다. 초기 임포트 시간의 SLA도 없다.
- 생성/재연결에는 상태 볼륨의 여유 2 GiB, 새 캡처에는 기존 VSS diff-area 상한의 여유 8 GiB가 필요하다. 설치·볼륨·상한 설정은 자동 변경하지 않는다. 프로젝트 밖 쓰기도 VSS 공간을 사용한다.

VSS capture/release와 정확한 live ID를 확인하는 Observe helper에는 관리자 권한이 필요하다. 일반 CLI는 자동 승격하지 않고 `SnapshotPrivilegeRequired`와 정확한 helper 경로·argument list, 그 뒤 동일 인자로 재시도할 workspace ensure, unity start 또는 workspace release를 `nextActions`에 반환한다. 승인된 helper만 관리자 권한으로 실행한 뒤 원래 명령을 다시 호출한다. Unity와 일반 CLI를 관리자 계정으로 계속 실행하는 구조가 아니다. helper의 `operation-result.json`에는 성공 또는 진단이 남는다.

`SnapshotPrivilegeRequired` 응답은 helper 실행이 필요하다는 뜻이며 UAC 창 표시나 snapshot 생성의 증거가 아니다. 외부 실행자가 승격을 요청했다면 그 소유 프로세스의 종료 상태와 `capture-intent.json`·`capture-return.json`·`snapshot.json`·`operation-result.json`을 대조한다. 사용자에게 창이 보이지 않으면 대기 상태만으로 확인 버튼을 반복 요청하지 않는다. 기존 helper가 아직 실행 중이거나 캡처 부작용을 판정할 수 없을 때는 중복 helper/새 캡처를 시작하지 않고 기존 기록을 보존한다.

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

NTFS refresh는 incremental compilation을 요청한다. 일반 제품의 전체 대상 소스 fingerprint를 요청 ID·컴파일 callback·domain reload와 연결한다. 검증 뒤 소스나 `.meta`가 바뀌면 이전 성공은 무효다. `Accepted`는 접수이며 `workspace.validation.success`와 현재 fingerprint 일치가 완료 조건이다. 변경 없는 incremental 요청에서 reload가 없으면 성공을 발행하지 않는다. 현재 실제 실행 관문은 스크립트 변경 후 검증이며 일반 에셋·패키지 검증은 별도다. fixture의 `snapshot-probe.json`은 현재 로드된 코드의 값·PID·generation을 추가로 기록하며 전체 소스 검증을 대체하지 않는다. fixture 자체 보고에는 MVID가 없다. 제품 WorkspaceBridge의 Ready heartbeat는 같은 파일에 현재 로드된 함수의 값·domain generation·assemblyMvid·PID를 추가 보고한다.

일반 status는 앱/provider와 새 검증 응답을 조회하며 매번 프로젝트 파일을 읽지 않는다. 대기 중이던 검증 응답이 도착하면 소스를 한 번 읽어 fingerprint를 대조하고 결과를 저장한다. `data.sourceVerification=verified`인 응답에는 `source`와 `validationCurrent`가 있으며, 검증 성공은 `workspace.validation.success=true`와 `validationCurrent=true`를 함께 확인한다. `not-checked` 응답에는 두 필드를 생략한다. 이때 남아 있는 `workspace.validation`은 이전 증거이며 현재 파일 상태의 보증이 아니다. 현재 소스를 명시적으로 확인하려면 `workspace status --id <id> --verify-source`를 사용한다. checkpoint/integrate의 사전 확인은 항상 소스를 다시 읽는다.

checkpoint는 앱과 자식의 종료를 확인한 뒤 검증한 소스만 커밋하고 `refs/splinter/snapshot-results/<id>`와 `workspace.snapshot.result`를 남긴다. manifest에는 snapshot ID, revision, fingerprint, 결과 ref, 저널 sequence/길이/SHA-256과 `collectionProtocol=stable-journal-v1`이 있다. 소스 검사와 결과 ref 보존 전후의 seal이 같을 때만 manifest를 발행한다. 결과 객체가 Git 저장소 안에 있는지 검증한다. 결과 커밋과 기록은 회수 뒤에도 남는다. 새로운 편집에는 새 key를 사용한다.

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

WinFsp의 디렉터리 mount root 자체는 reparse point다. 상태 저장소는 이 경로가 연결돼 있어도 기록을 읽을 수 있으며, Git/Unity는 Core의 `WorkspaceMountAccess.ObservedRoot`로 같은 접근 규칙을 사용한다. backend 이름 대신 snapshot 가용성·mount/provider 생존·소유권과 workspace ID·snapshot·절대 root 경로의 일치를 요구한다. 원본·상위 경로·작업 트리 내부의 일반 링크는 계속 거부한다. 이 공통 규칙이 새 backend의 제품 지원을 뜻하지는 않는다.

provider가 끝나면 앱/자식이 남지 않았음을 확인한 후 같은 ensure로 기존 snapshot/delta에 재연결한다. 실행 intent만 있고 영수증을 확인할 수 없거나 이전 state와 새 실행 세대가 다르면 중복 실행 대신 `OverlayStartUncertain`으로 보존한다. 재부팅·전원 손실 복구는 미검증이다. 캡처와 실제 파일 내용의 불일치 등으로 준비를 완료할 수 없는 작업의 자동 폐기는 제공하지 않는다. `SnapshotPreparationIncomplete` 상태와 해당 영수증을 보존해 조사한다.

NTFS 응답의 `data.recovery`는 `disposition`, `reasonCode`, `nextCommand`를 반환한다. `ready`는 기존 다음 작업을 진행할 수 있음을, `resume-ensure`/`resume-release`는 같은 identity의 해당 단계를 재개할 수 있음을 나타낸다. `observe`는 관측 복구 후 status를 다시 조회하고, `preserve`는 데이터·영수증을 보존하며 원인을 해결해야 한다. `released`의 다음 행동은 list다. 실제 호출 인자는 `nextActions`에 있으며 `data.nextCommand`와 같은 명령을 가리킨다. 복구 분류 자체가 자원 삭제 승인은 아니며 각 명령에서 소유권·결과·회수 조건을 다시 검사한다.

스냅샷 접근 실패는 `SnapshotObservationUnknown`이다. `snapshotAvailability`와 `snapshotStorage.availability`는 `available`/`missing`/`unknown` 중 하나이며 unknown에서는 기존 boolean 필드를 생략한다. 일반 ensure와 회수 전 단계는 관측 복구까지 보존한다. 관측한 부재와 접근 오류를 구분하며, 경로가 파일로 바뀌면 `SnapshotIdentityMismatch`로 보존한다. 정확한 ID의 성공한 부재 조회만 missing으로 판정하며 읽기 경로의 소실만으로 VSS 부재를 주장하지 않는다. 새 연결의 live identity 대조는 아래 계약을 따른다.

마지막 `release-snapshot` 단계에서는 독립 결과를 다시 검증하고 앱/provider·mount·Git 등록·delta 회수를 확인한 뒤 정확한 VSS helper의 관측을 갱신할 수 있다. 삭제된 장치 경로가 Win32 21을 반환해도 오류만으로 부재로 바꾸지 않는다. 같은 owner와 snapshot의 `released` 영수증, helper가 VSS ID의 부재를 확인한 `observation.json`, 현재 OS의 `LastBootUpTime` 일치가 있어야 모호한 경로 관측을 해소한다. 부팅 정보가 없거나 달라진 이전 영수증은 삭제 없는 `-Action Observe` 관리자 helper와 동일 release 재시도를 반환한다. CLI 자체를 관리자 실행하지 않는다. 부팅 정보 조회 실패는 unknown으로 남고, 회수 완료를 저장할 때도 모든 자원 부재를 검사한다. 이미 해제된 장치 경로가 다른 디렉터리로 재사용되면 identity 불일치로 보존한다.

provider의 `state.json`도 검증된 접근 관측을 사용하며 관측 불가를 `snapshot-unknown`으로 기록한다. 완결 저널 프레임의 checksum·sequence·JSON·경로/범위 메타데이터가 잘못됐거나 identity JSON이 손상되면 `DeltaCorrupt`로 실패하고 파일을 그대로 보존한다. 불완전한 마지막 프레임만 기존 계약대로 재생에서 제거하며 완결 손상을 잘라내지 않는다.

손상된 workspace JSON은 `StateCorrupt`, 기존 control/workspace 경로 옆의 누락된 JSON은 `StateRecoveryRequired`, 읽을 수 없는 JSON은 `StateReadUnavailable`이다. 같은 key로 새 작업을 만들지 않는다. 상태 기록 실패는 `StateWriteIncomplete`이며 이전 기록과 임시 파일을 보존한다. 작업 실패 기록마저 저장할 수 없으면 원래 오류 코드를 details에 포함한다. 이 계약은 파일 잠금·중단된 임시 파일로 검증했고 실제 디스크 고갈/전원 중단 검증은 남아 있다.

## 살아 있는 VSS 식별과 읽기 위치

`snapshot/snapshot.json` version 2의 `captureIdentity`는 원래 snapshot ID·system provider ID·volume GUID 경로·볼륨 내 상대 경로·캡처 시각·persistent/differential/no-auto-release 조건을 고정한다. `verification-request.json`은 owner·원래 ID·요청 세대·현재 boot를, `live-observation.json`은 실제 조회 객체의 identity와 현재 device root·조회 성공/실패를 기록한다. 현재 읽기 위치는 검증된 device root와 고정 상대 경로로만 만든 `SnapshotReadAccess`다. `SnapshotReference.RootPath`, overlay identity와 기존 저널은 캡처 당시 그대로 유지한다. available 응답의 `snapshotStorage.readAccess`는 snapshotId·rootPath·verificationId를 제공한다.

새 provider 연결은 이전 실행이 소비한 verification ID와 다른 현재 부팅의 관측을 요구한다. `SnapshotVerificationRequired`의 재개 명령은 동일 ensure다. 관리자 Observe 뒤 같은 요청 재시도는 준비된 관측을 재사용하며 중복 캡처·등록·provider를 만들지 않는다. Observe는 VSS·원본·볼륨 설정을 변경하지 않고 소유 관측 기록만 저장한다. 정상 status는 같은 연결의 검증을 재사용한다. provider는 주기적으로 관측과 현재 읽기 경로를 확인하며 파일 I/O마다 CIM을 호출하지 않는다. 실행 중 다른 접근 정보로 변경되면 거부하고 새 연결에서만 읽기 경로를 바꾼다. 외부 관리자의 VSS 삭제·대체와 모든 동시 경쟁 조건을 검증했다는 뜻은 아니다.

version 1은 원래 ID를 새로 조회해 기존 owner/provider/시각/조건과 대조한 경우에만 `verified-identity.json`을 별도로 기록한다. provenance는 `legacy-revalidated`이며 원래 영수증을 변경하거나 새 필드를 캡처 당시 증거라고 표시하지 않는다. version 2의 누락된 captureIdentity·지원하지 않는 버전은 손상으로 보존한다. 다른 owner/ID/provider/volume/상대 경로는 `SnapshotIdentityMismatch`, 손상 JSON은 `SnapshotObservationCorrupt`, 중단된 `.new`/관측 요청 `.tmp`는 `SnapshotObservationIncomplete`다. 파일이나 델타를 지우거나 이전 성공으로 대체하지 않는다. 새 boot·연결 세대, 최신 조회 실패, 손상·부분 기록과 접근 불가는 이전 성공의 재사용을 막는다. 실제 재부팅·강제 전원 종료는 별도 미검증 관문이다.

## 회수와 관측

회수 순서는 앱/자식 종료 → 소유 브리지 제거 → 수집 전 flush/seal → 현재 source 검사와 독립 결과 ref 보존 → 수집 후 flush/seal 대조 → manifest 저장 → unmount와 provider 종료 → seal 재대조 → Git 등록 회수 → delta 회수 → 마지막 의존성이 없는 소유 VSS ID 해제다. 브리지 제거는 합법적인 저널 변경이므로 수집 구간 전에 마친다. 관리 캐시 밖의 ignored 파일, 미보존 변경, 다른 의존 작업이 있으면 중단한다. 수집 중이나 이후의 쓰기로 seal이 달라지면 `DeltaChangedAfterCollection`로 중단하고 기존 manifest·저널·snapshot을 보존한다. delta 회수 중 응답이 유실되면 기존 삭제 intent와 남은 파일을 검증하고 이어 간다. VSS 해제 helper는 정확한 소유 ID만 받으며 전역 삭제를 하지 않는다.

snapshot이 소실된 뒤에는 `stable-journal-v1`로 수집한 독립 결과가 있고 Git ref/객체와 **저널 전체 seal이 동일한 경우에만** 회수할 수 있다. 사용할 수 없는 원본 트리를 다시 읽거나 수정하지 않고 unmount 후 seal을 비교한다. 독립 결과가 없거나 수집 뒤 쓰기가 있으면 결과와 저널을 보존하고 실패한다. 열린 핸들이 남은 unmount는 `OverlayStopPending`이며 핸들을 닫은 뒤 같은 release를 재시도한다.

이 필드가 없는 이전 manifest도 읽을 수 있지만 회수의 증거로 바로 사용하지 않는다. 원래 snapshot과 mount가 살아 있고 아직 회수를 시작하지 않았다면 checkpoint 또는 release가 새 절차로 다시 수집한다. snapshot 소실 또는 이미 시작한 `release-*` 단계에서는 `SnapshotResultRevalidationRequired`와 `preserve`를 반환하고 남은 자원을 보존한다. 기록의 필드나 단계를 수동 수정해 통과시키는 복구는 제공하지 않는다. 소스·저널 전체 해시는 수집 경계에서 여전히 읽으며 증분 해시 최적화나 읽기 비용 0을 주장하지 않는다.

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
| `SnapshotResultRevalidationRequired` | 이전 수집 증거로 회수를 재개하지 않고 남은 자원·기록 보존. 원래 기준을 사용할 수 있는 수집 전 상태에서만 재수집 |

## 구현과 검증 경계

Core의 `ISnapshotWorkspaceBackend`와 `ISnapshotSourceWorkspaceProvider`는 Windows나 Git API를 노출하지 않는다. Runtime은 단계·잠금·결과/의존성 정책, Git 어댑터는 등록·index·결과 ref, Windows 어댑터는 VSS helper·특수 파일 검사·WinFsp·저널 관측/회수를 맡는다. CLI가 이들을 조립하며 Host는 읽기 전용 in-process 계층을 유지한다.

기존 [ReFS 기준본](storage-and-resources.md)은 선택적 경로다. NTFS 실패를 ReFS 이관·전체 checkout·파일 전체 copy-up으로 바꾸지 않는다. 자동 시험의 fake/Git/Windows 메타데이터 증거와 실제 mount/Unity 실행 근거는 NI 기록에서 구분한다. [NR-01~08 계획](plans/ntfs-recovery-and-support.md)의 공통 복구 응답과 상태 파일 보존은 부분 구현·자동 검증했고, 실제 재부팅·공간 장애·파일 지원/거부는 [NR 기록](verification/ntfs-recovery.md)의 미완료 조건이다. 특수 파일 지원 확대, 일반 에셋/패키지, 대형 프로젝트 성능과 공개 제품 전체는 이후 범위로 남긴다. 구매 에셋은 NI 및 다음 NR 시험에 필요하지 않다.
