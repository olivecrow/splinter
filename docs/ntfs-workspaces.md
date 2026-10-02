# NTFS snapshot 작업공간

현재 `--source none`의 일반 폴더 경로와 v2 상태/서비스 패키지는 [VCS 없는 작업공간](vcs-independent-workspaces.md)을 따른다. 아래 Git 경로와 과거 native 자격을 일반 폴더 지원으로 승계하지 않는다.

[명시적 폐기](workspace-discard.md)는 Core/Runtime·Windows/Git provider·CLI를 연결했다. 해당 계약을 지원하는 제한 서비스가 필요하며 로컬 Editor 없는 dirty A/B의 실제 폐기 자격은 [폐기 검증](verification/workspace-discard.md)을 따른다. 아래 보존형 release는 독립 결과 gate를 계속 요구한다.

`workspace ensure --storage ntfs-snapshot`은 기존 workspace 수명주기에 VSS 기준과 WinFsp 범위 저널을 연결한다. baseline 준비나 원본 이관은 필요하지 않다. **일반 제품 CLI로 실제 NTFS/Git/Unity 생성·편집·검증·재연결·독립 결과 보존과 회수 단계를 실행했다.** 최종 VSS 해제 상태와 지원 한계는 [NI 검증 기록](verification/ntfs-workspace-integration.md)을 따른다. 독립 SO 실험의 성공을 제품 통합의 성공으로 합산하지 않는다.

## 입력과 권한 경계

아래 Git의 clean/HEAD/관리 등록 조건은 `--source git` 경로다. 일반 폴더는 [VCS 독립 계약](vcs-independent-workspaces.md), Plastic은 [관리 정보/등록 호환 계약](plastic-compatibility.md)을 따르며 공통 NTFS/VSS/WinFsp·소유 자원·범위 저장·회수 guard를 공유한다.

- Windows의 기존 NTFS, 설치된 WinFsp, 저장된 clean Git repository root를 대상으로 한다. 첫 Unity 검증 대상은 별도 `splinter-unity` fixture다. 원본의 현재 HEAD와 `--base`가 같아야 하며 `.git` 디렉터리가 원본 안에 있어야 한다.
- dirty/staged/untracked 변경, skip-worktree/assume-unchanged, submodule/symlink, LFS·사용자 filter·working-tree-encoding은 거부한다. 필터는 checkpoint와 결과 보존 시에도 검사한다. 원본을 stash/reset하거나 임의로 커밋하지 않는다.
- 원본과 불변 스냅샷의 메타데이터를 검사하여 ADS, hardlink, reparse, EFS, 압축·희소 파일을 `SnapshotFileUnsupported`로 거부한다. `.git`은 기준 트리에서 제외한다. 이 검사는 파일 payload를 읽거나 복제하지 않는다. 개별 ACL 변경은 지원하지 않으며 overlay가 원본의 모든 ACL 의미를 재현한다고 주장하지 않는다.
- reparse/EFS/압축/희소 속성은 기준 트리의 지연 읽기와 WinFsp create/overwrite/속성 변경에도 같은 규칙으로 거부한다. overwrite는 기존 길이를 줄이기 전에 검사한다. 거부된 callback은 바이트·메타데이터·저널 sequence를 유지하며 OS에는 `STATUS_NOT_SUPPORTED`를 반환한다. ARM64 실제 mount에서는 ADS·hardlink·희소·압축·mount-point reparse 요청 5개의 거부를 일반 NTFS 양성 대조와 함께 확인했다. 불변 입력은 fake이며 VSS 입력·EFS·ACL·특수 FSCTL의 전체 검증은 남아 있다. 실행 범위는 [NR 기록](verification/ntfs-recovery.md)을 따른다.
- 캡처 직전 호출자는 원본 Unity가 저장된 상태이고 import/compile/Play 중이 아닌지 확인한다. 캡처는 application writer 없는 filesystem crash consistency다. 미저장 메모리를 포함하지 않는다. 캡처 후에는 원본을 계속 수정할 수 있다.
- `--home`은 짧은 경로를 사용한다. NI의 깊은 시험 경로에서는 Unity 패키지 DLL 전체 경로가 270자일 때 초기 로드 예외가 관측됐다. 스크립트 검증과 실행은 통과했지만 긴 패키지 경로와 일반 에셋 로드를 지원으로 확정하지 않는다. 초기 임포트 시간의 SLA도 없다.
- 생성/재연결에는 상태 볼륨의 여유 2 GiB, 새 캡처에는 기존 VSS diff-area 상한의 여유 8 GiB가 필요하다. 설치·볼륨·상한 설정은 자동 변경하지 않는다. 프로젝트 밖 쓰기도 VSS 공간을 사용한다.

VSS Capture/Observe/Release는 [Windows 저장소 권한 서비스](storage-privilege-service.md)가 처리한다. 최초 설치와 사용자·프로젝트·볼륨·home 등록은 관리자 구성이고, 정상 CLI·Git·Unity는 일반 사용자 권한이다. 서비스가 없으면 `StorageServiceMissing`, 정지했으면 `StorageServiceUnavailable`, 미등록 범위면 `StorageScopeDenied`를 반환한다. 자동 승격이나 기존 helper로의 fallback은 없다.

서비스 코드와 실제 실행 자격은 구분한다. 최초 설치 후 정상 반복 작업의 추가 UAC 0 판정은 [UA 검증](verification/storage-privilege-service.md)을 따른다. 개발용 관리자 세션이나 fake 시험을 실제 서비스 설치·반복 사용의 완료로 표시하지 않는다.

수동 진단용 `scripts/vss-snapshot.ps1`은 별도로 승인된 관리자 작업에만 사용한다. UAC 요청 자체를 창 표시나 snapshot 생성의 증거로 삼지 않는다. 실제 소유 프로세스 종료와 `capture-intent.json`·`capture-return.json`·`snapshot.json`·`operation-result.json`을 대조한다. 창이 보이지 않거나 결과가 불명확하면 중복 helper/새 capture를 시작하지 않고 기존 기록을 보존한다.

WinFsp 디렉터리 열거는 루트 이외의 디렉터리에 실제 디렉터리 정보와 함께 `.`·`..` 항목 및 marker 기반 재개 순서를 제공한다. 빈 하위 디렉터리도 Win32 `FindFirstFileW(<dir>\*)`로 열거할 수 있다. 이 항목은 overlay의 파일이나 저널 기록이 아니다. [작은 Unity 실환경 회귀](verification/journal-compaction-qualification-and-cost.md)는 수정 전 빈 폴더 열거 실패와 수정 후 성공을 대조했다.

## 일반 CLI 흐름

등록한 로컬 fixture/home에서 쓰기 중단을 재현하는 [제품 개발 시험](product-write-recovery.md)은 일반 provider를 유지하고 외부 client 경계에서 정확한 소유 provider만 종료한다. 같은 ensure·새 현재 VSS read proof·독립 결과/release를 대조하며, 일반 workspace 사용 중 자동으로 gate를 켜거나 실패 작업을 회수하지 않는다.

아래 `splinter`는 제품 저장소에서 `pwsh ./scripts/splinter.ps1`이다. 빌드는 `pwsh ./scripts/build.ps1`로 한다. `--home`은 원본 프로젝트 밖의 같은 지속 상태 경로를 모든 작업자가 공유한다.

```text
splinter workspace ensure --project <원본 Unity 저장소> --key <고유한 작업 key> --base development --storage ntfs-snapshot --home <상태 경로>
splinter workspace status --id <workspace.id> --home <상태 경로>
splinter unity start --id <id> --home <상태 경로>
```

첫 ensure 전에 `storage service status`와 설치 범위를 확인한다. 서비스 설치/구성 후 **같은 입력**으로 ensure를 재호출한다. `--editor`를 주면 준비 후 Editor 실행도 요청한다. Editor의 준비는 `application.ready`로 확인하고, 편집은 반환된 `workspace.path` 안에서만 한다. 작업 key는 프로젝트와 함께 identity를 결정하며 입력 hash, snapshot, delta, 등록, 실행 영수증은 `home`에 남는다. `--max-journal-bytes`는 기본 16 GiB, 최소 4096, 최대 1 TiB이며 작업 생성 뒤 변경할 수 없다.

공통 WinFsp mount/reconnect는 실행 중인 정확한 provider의 실제 mount와 최신 `mounted` 상태를 최대 2분 기다린다. `starting`/`replaying`은 준비 완료가 아니며 한도 초과나 취소로 provider·저널을 삭제하지 않는다. 같은 요청 재시도는 그 provider를 이어서 확인한다. 저널은 64KiB 수명 한정 버퍼로 읽되 모든 frame 검증을 유지하며 새 replay 단계 계수의 의미는 [계측 계약](source-verification.md#계측의-의미)을 따른다. 이것이 새 Unity 실행 모드나 기본 방식 전환을 뜻하지 않는다.

```text
splinter workspace status --id <id> --home <상태 경로>
splinter workspace compact --id <id> --home <상태 경로>
splinter workspace checkpoint --id <id> --message <결과 설명> --home <상태 경로>
splinter workspace release --id <id> --home <상태 경로>
```

Editor의 refresh·컴파일·Play/테스트 등은 반환 workspace 경로를 선택한 외부 MCP·Unity CLI에서 수행한다. 제품 bridge는 liveness·진행 상태와 정상 종료만 처리하고 프로젝트 함수를 실행하거나 합격을 판정하지 않는다. checkpoint는 컴파일 성공 없는 소스도 안정된 독립 결과로 보존한다. 실제 게임/코드 검증 증거는 선택한 도구에서 확인한다.

`workspace compact`는 **최종 결과 수집 전**의 연결된 NTFS 작업공간 하나에서 명시적으로 실행한다. 소유 Editor와 자식 프로세스가 종료되고 mount의 열린 핸들이 0이며 snapshot 가용성과 소유권이 확인돼야 한다. 같은 workspace의 다른 수명주기 명령은 `compact-*` 단계 동안 거부한다. provider를 멈춘 뒤 unmount 전후 저널 seal을 대조하고, 원래 snapshot·작업 ID·Git 등록을 그대로 사용해 새 저널 세대를 검증·발행·재연결한다. 정리 뒤 source 내용과 seal을 다시 확인하며 프로젝트 검증은 에이전트가 필요에 맞게 수행한다. 이미 완료한 세대의 같은 명령 재시도는 seal이 같을 때 읽기 전용 결과를 반환하며, 이후 새 쓰기가 있으면 다시 명시적으로 압축할 수 있다. `checkpoint`를 중간 압축용으로 사용하지 않는다.

선택적 `--max-estimated-journal-overlap-bytes <양의 정수>`는 기존 저널의 논리 길이와 후보 저널의 사전 계산 논리 길이의 합계를 제한한다. 지정하지 않으면 기존 정리 동작을 유지한다. 합계가 예산을 초과하면 새 후보 파일·세대 게시 전에 `CompactionOverlapBudgetExceeded`와 두 길이·합계를 반환한다. 그때 provider는 정지된 `resume-compaction` 상태이므로 같은 ID/home의 `workspace compact`로 명시적으로 재시도한다. 인자를 생략한 재시도는 저장된 예산을 유지하고, 값을 명시해야 바뀐다. 이미 후보가 게시된 `compact-connect` 단계에서는 새 예산이 복구를 막지 않는다. 성공하면 pending 예산을 지운다. 이 옵션은 **예상 저널 파일 길이의 중첩량**만 제한하며 NTFS 실제 할당, control/Git/Library, VSS diff area 또는 작업공간 전체 물리 peak의 상한이 아니다. 실제 판단에는 [읽기 전용 공간 장부](source-verification.md#계측의-의미)를 함께 사용한다.

예산 검사는 provider 정지 뒤 기존 저널 재생·seal·상태 hash·후보 scan을 수행하므로 거부에도 읽기 시간과 Editor 정지가 필요하다. 중단된 이전 시도에 이미 부분 후보 파일이 있으면 낮은 예산의 거부도 그 파일을 삭제하지 않는다. 예산은 앞으로 작성할 후보를 검사하며 이미 쓰인 공간을 자동 회수하지 않는다. 원본 저널·intent·기존 부분 후보의 소유와 상태를 확인한 뒤 같은 작업의 명시적 재시도로 진행한다.

압축은 살아 있는 범위를 chunk 단위로 다시 기록하고 같은 경로의 연속된 무효 쓰기를 하나의 sequence 구간으로 묶는다. 범위/무효 쓰기 메타데이터와 payload의 합계가 기존 frame보다 커지면 기존 frame을 유지한다. 개별 skip 표현이 더 크면 같은 경로의 묶음이 더 작을 때만 변환한다. 따라서 일반 정리는 후보 저널의 논리 길이를 늘리지 않지만, 더 작은 표현을 위해 일부 무효 payload를 남길 수 있으며 `obsoletePayloadBytes=0`을 완료 조건으로 삼지 않는다. 사전 예산 계산과 후보 작성은 같은 선택 규칙을 사용한다. [크기 회귀 검증](verification/journal-compaction-size.md)의 합성 입력과 실제 저널 할당 비교를 따른다.

whiteout·rename·truncate·`.meta`와 변경 경로를 재생 대조하며 파일 전체를 copy-up하거나 기준 파일을 읽어 채우지 않는다. 새 후보는 같은 디렉터리에 flush하고 checksum·전체 재생 상태를 확인한 뒤 원자적으로 게시한다. `delta.journal.previous`는 새 세대의 mount 재연결과 seal이 확인될 때까지 남긴다. `compaction-intent.json`과 후보/이전 세대의 소유·hash가 맞지 않거나 내용이 손상되면 파일을 보존하고 `preserve`로 중단한다. 준비·게시·재연결 중 끊기면 같은 ID/home의 `workspace compact`로 `resume-compaction`을 진행한다. 자동 sweep이나 다른 workspace·snapshot 회수와 연결하지 않는다.

`snapshot.compaction`에는 attempt, 전후 sequence/길이/SHA-256 seal, 전후 NTFS 파일 할당과 두 저널 세대가 함께 있던 전환 지점의 할당 합계를 기록한다. 새 시도의 `cost`는 `stopwatchFrequency`·`totalTicks`와 stage별 `elapsedTicks`·`readBytes`·`writtenBytes`·`flushCount`를 제공한다. stage에는 원본 replay/seal/상태 hash, 후보 scan/write/flush/replay/seal, 세대 게시·검증, provider 재연결·이전 세대 정리가 들어간다. `totalTicks`는 오프라인 저널 정리 호출만 재며 재연결·이전 세대 정리는 별도 stage다. 이미 게시된 세대를 확인만 하는 재시도에는 `cost`가 없을 수 있고, 누락은 0 비용이 아니다. 두 세대 할당 합계와 stage 표본은 control/identity/Git/Library를 포함한 전체 작업공간 peak가 아니다. 후보를 쓸 여유와 재연결의 2 GiB 하한이 없거나 계산한 후보가 journal budget을 넘으면 기존 세대를 보존한다. VSS 사용량은 볼륨 공유 관측이며 작업별 절감으로 귀속하지 않는다. 현재 구현은 범위 재기록이며 일반 목적 payload 압축이나 쓰기마다 수행하는 flush 정책 변경이 아니다. [정리 1차](verification/journal-compaction.md), [Unity 재개·공간 비용](verification/journal-compaction-qualification-and-cost.md), [중첩량 예산](ntfs-workspaces.md)을 따른다.

일반 status는 소유 앱/provider를 관측하고 프로젝트 내용을 읽지 않는다. `--verify-source`의 `sourceVerification=verified` 응답에는 현재 `source`가 있으며 `not-checked`에는 이를 생략한다. source fingerprint는 저장 내용 검사이고 컴파일/테스트 성공이 아니다. checkpoint/integrate는 현재 소스와 독립 결과를 다시 확인한다.

checkpoint는 앱과 자식의 종료를 확인한 뒤 검증한 소스만 커밋하고 `refs/splinter/snapshot-results/<id>`와 `workspace.snapshot.result`를 남긴다. manifest에는 snapshot ID, revision, fingerprint, 결과 ref, 저널 sequence/길이/SHA-256과 `collectionProtocol=stable-journal-v1`이 있다. 소스 검사와 결과 ref 보존 전후의 seal이 같을 때만 manifest를 발행한다. 결과 객체가 Git 저장소 안에 있는지 검증한다. 결과 커밋과 기록은 회수 뒤에도 남는다. 새로운 편집에는 새 key를 사용한다.

원본 반영이 필요할 때만 `workspace integrate --id <id> --target development --home <상태 경로>`를 호출한다. 원본은 요청한 브랜치의 clean 상태여야 한다. 충돌 시 이 명령이 시작한 merge만 중단하고 결과를 보존한다. **독립 결과를 보존한 NTFS 작업은 원본 통합 없이 회수할 수 있다.** 실제 NTFS fixture에서 원본 README에 미커밋 변경을 넣고 통합을 요청하면 `GitDirtyWorktree`로 거부하며 원본 HEAD·인덱스·변경 내용과 결과 ref를 보존하는 것을 확인했다. 실패한 검증이나 미커밋 변경은 자동 삭제하지 않는다. 원본 통합·Editor 조작의 공통 절차는 [알파 작업공간](alpha-workspaces.md)을 따른다.

## 지속 상태와 재개

workspace의 Git 명령에는 10분 제한과 호출자 취소가 적용된다. 큰 에셋의 초기 index 확인도 파일 내용을 읽을 수 있다. index 파일이 이미 있어도 중단된 attach의 재시도는 stat cache refresh를 완료한다. `GitCommandTimeout` 뒤에는 상태와 남은 잠금을 조사해야 하며 제품은 `index.lock`을 자동 삭제하지 않는다. 진단용 Git inspector의 10초 제한과 별도다.

```text
<home>/records/<id>/workspace.json         identity, 단계, 이전 증거, 결과 manifest
<home>/records/<id>/events.jsonl           작업 intent와 완료·실패
<home>/records/<id>/snapshot/              소유 VSS 영수증, helper, 입력 검사, 해제 허가
<home>/records/<id>/git-overlay.json       no-checkout 등록과 전용 .git pointer
<home>/records/<id>/overlay/               provider 요청·실행·flush/stop 관측
<home>/records/<id>/delta/                 identity.json, delta.journal; 압축 중에는 compaction-intent.json, delta.journal.compacting/previous
<home>/records/<id>/delta-reclaim.json     정확한 독립 결과와 저널의 삭제 intent
<home>/workspaces/<id>/                   WinFsp가 제공하는 작업 트리
```

준비 순서는 요청 저장 → 소스 checkpoint 확인 → 캡처 intent/영수증 → no-checkout Git 등록 → mount → 독립 index 연결 → 캡처 내용과 checkpoint 비교다. 비교에 실패하면 Ready로 승격하지 않는다. 부분 등록은 원래 경로/branch/owner 영수증과 Git 관리 영역을 대조한다. 같은 key 재시도는 같은 캡처를 사용하고, 원래 캡처가 사라졌다고 현재 원본으로 대체하지 않는다. CLI 종료만으로 Editor/provider를 종료하거나 작업을 회수하지 않는다.

WinFsp의 디렉터리 mount root 자체는 reparse point다. 상태 저장소는 이 경로가 연결돼 있어도 기록을 읽을 수 있으며, Git/Unity는 Core의 `WorkspaceMountAccess.ObservedRoot`로 같은 접근 규칙을 사용한다. backend 이름 대신 snapshot 가용성·mount/provider 생존·소유권과 workspace ID·snapshot·절대 root 경로의 일치를 요구한다. 원본·상위 경로·작업 트리 내부의 일반 링크는 계속 거부한다. 이 공통 규칙이 새 backend의 제품 지원을 뜻하지는 않는다.

provider가 끝나면 앱/자식이 남지 않았음을 확인한 후 같은 ensure로 기존 snapshot/delta에 재연결한다. 실행 intent만 있고 영수증을 확인할 수 없거나 이전 state와 새 실행 세대가 다르면 중복 실행 대신 `OverlayStartUncertain`으로 보존한다. 재부팅·전원 손실 복구는 미검증이다. 캡처와 실제 파일 내용의 불일치 등으로 준비를 완료할 수 없는 작업의 자동 폐기는 제공하지 않는다. `SnapshotPreparationIncomplete` 상태와 해당 영수증을 보존해 조사한다.

새 NTFS 작업은 provider 실행 전에 검증된 snapshot에 대한 빈 delta identity를 게시한다. 기존 identity가 있으면 caller에서 저널을 다시 재생하지 않고 provider가 기존 저널을 검증한다. private `overlay-serve`는 정확한 launch generation을 필수 인자로 받으며 token·workspace·generation이 durable intent와 일치하는지 검사한 뒤 자신의 PID·시작 시각·generation 영수증을 게시한다. parent CLI가 프로세스 생성 직후 종료돼도 같은 ensure는 이 자식 영수증을 대조해 기존 provider를 관측한다. intent만 있고 실행 여부를 확인할 수 없는 간격은 계속 보존/차단하며 추측 재실행하지 않는다.

## 동일한 private 범위의 반복 쓰기

현재 쓰기 범위 전체가 이미 승인된 private 저널 범위에 있고 바이트가 같으면 payload를 다시 쓰지 않는다. 기존 저널을 최대 64KiB 버퍼로 비교하고 기존 `metadata` frame에 수정 시각·현재 경로·새 sequence를 기록해 개별 `Flush(true)` 뒤 응답한다. 빈 쓰기의 기존 no-op과 다르며 소스 변경 경로와 승인 쓰기 경계는 유지한다.

같은 private 범위에 일부 다른 바이트가 있으면 첫 차이부터 마지막 차이까지의 연속 범위 하나를 저장한다. 앞뒤의 일치 바이트를 제외해 payload를 4KiB 이상 줄일 수 있을 때만 적용하며, 중간의 일치 바이트는 그 범위에 포함한다. 전체 요청의 길이·수정 시각·경로 sequence를 유지하고 기존 `write` frame 하나와 개별 `Flush(true)`로 승인한다. 작은 절감에는 기존 전체 요청을 저장해 불필요한 extent 분할을 피한다.

원본만 참조하는 범위·zero-fill hole·파일 길이 확장은 요청한 payload를 기존 방식으로 저장한다. 첫 신규 파일 쓰기도 그대로다. 비교를 위해 snapshot payload를 읽거나 hydration하지 않으며 작업/파일 간 dedup·공유 payload cache는 없다. 같은 내용에도 metadata는 누적되므로 무한 반복의 저장량 0을 보장하지 않는다. 정상 쓰기에서도 저널 비교 읽기가 추가될 수 있다. [동일 쓰기 검증](verification/duplicate-private-writes.md)과 [부분 덮어쓰기·버퍼·시작 검증](verification/resource-efficiency-followup.md)은 고정 입력의 실제 파일 할당과 Unity 전체 효율을 구별한다.

WinFsp Read/Write callback은 요청 길이와 최대 전송 크기 중 작은 크기로 버퍼를 빌린다. 빈 요청은 최소 대여 버퍼를 사용하며 기존 빈 I/O 의미를 유지한다. 큰 요청의 chunk 크기·callback 직렬화·승인/flush 경계는 그대로이고 성공/실패 뒤 버퍼를 반환한다. 대여 크기와 누적 관리 할당은 Editor·시스템 RAM과 구별한다.

## 개발용 준비 중단 시험

`preparation-probe --input <JSON>`은 일반 `workspace ensure`의 NTFS 준비 경계에만 명시적 gate를 넣는다. Unity Editor를 시작하거나 자원을 자동 회수하지 않는다. 입력은 `preparation-probe-request` version 1, 출력 signal은 `preparation-probe-signal` schema다. capability의 `developmentPreparationProbe`에서 현재 진입점과 schema를 확인한다. signal은 원본/workspace 밖의 새 경로에만 flush 후 게시하며 boundary·workspace/request digest·PID/시작 시각·snapshot/step을 연결한다. 같은 PID/시작 시각을 확인한 외부 harness가 CLI만 종료한다. `.continue`를 명시적으로 만들면 gate를 통과하고 5분 deadline은 기존 작업을 보존한 취소다.

```powershell
# 기본값은 읽기 전용 실행 계획이다. 실제 실행에는 별도 새 evidence 경로와 -Execute를 지정한다.
pwsh ./scripts/test-preparation-recovery.ps1 -Project <등록된 clean NTFS Git fixture> -HomePath <등록된 home> -BaseReference <현재 HEAD의 ref> -OutputPath <새 evidence 경로>
```

`-Execute`는 요청 기록·소스 확인·capture·Git 등록·delta·provider·mount·index·Ready·응답 경계의 CLI를 종료하고 같은 입력의 일반 ensure 두 개를 실행한다. 잠금의 `WorkspaceBusy`는 첫 호출이 끝난 뒤 같은 요청으로 다시 확인한다. 응답 유실 재호출·입력 충돌 거부·독립 결과 보존과 명시적 release까지 시험하며, 실패하면 해당 작업과 evidence를 남긴다. 서비스 설치·grant·볼륨/VSS 예산·전역 Git 설정은 바꾸지 않는다. fixture 소유자 차이는 명시한 source에 한정한 자식 프로세스의 Git 설정으로 처리한다. 경계별 실제 자격과 적용 불가능한 조합은 [준비 복구 검증](verification/preparation-recovery.md)을 따른다.

NTFS 응답의 `data.recovery`는 `disposition`, `reasonCode`, `nextCommand`를 반환한다. `ready`는 기존 다음 작업을 진행할 수 있음을, `resume-ensure`/`resume-compaction`/`resume-release`는 같은 identity의 해당 단계를 재개할 수 있음을 나타낸다. `observe`는 관측 복구 후 status를 다시 조회하고, `preserve`는 데이터·영수증을 보존하며 원인을 해결해야 한다. `released`의 다음 행동은 list다. 실제 호출 인자는 `nextActions`에 있으며 `data.nextCommand`와 같은 명령을 가리킨다. 복구 분류 자체가 자원 삭제 승인은 아니며 각 명령에서 소유권·결과·회수 조건을 다시 검사한다.

스냅샷 접근 실패는 `SnapshotObservationUnknown`이다. `snapshotAvailability`와 `snapshotStorage.availability`는 `available`/`missing`/`unknown` 중 하나이며 unknown에서는 기존 boolean 필드를 생략한다. 일반 ensure와 회수 전 단계는 관측 복구까지 보존한다. 관측한 부재와 접근 오류를 구분하며, 경로가 파일로 바뀌면 `SnapshotIdentityMismatch`로 보존한다. 정확한 ID의 성공한 부재 조회만 missing으로 판정하며 읽기 경로의 소실만으로 VSS 부재를 주장하지 않는다. 새 연결의 live identity 대조는 아래 계약을 따른다.

마지막 `release-snapshot` 단계에서는 독립 결과를 다시 검증하고 앱/provider·mount·Git 등록·delta 회수를 확인한 뒤 설치된 권한 서비스를 통해 정확한 VSS 관측을 갱신할 수 있다. 삭제된 장치 경로가 Win32 21을 반환해도 오류만으로 부재로 바꾸지 않는다. 같은 owner와 snapshot의 `released` 영수증, helper가 VSS ID의 부재를 확인한 `observation.json`, 현재 OS의 `LastBootUpTime` 일치가 있어야 모호한 경로 관측을 해소한다. 현재 부팅 정보와 다른 이전 영수증은 삭제 없는 서비스 `Observe`로 갱신한다. 부팅 정보를 확인할 수 없으면 상태를 보존한다. CLI 자체를 관리자 실행하지 않는다. 부팅 정보 조회 실패는 unknown으로 남고, 회수 완료를 저장할 때도 모든 자원 부재를 검사한다. 이미 해제된 장치 경로가 다른 디렉터리로 재사용되면 identity 불일치로 보존한다.

provider의 `state.json`도 검증된 접근 관측을 사용하며 관측 불가를 `snapshot-unknown`으로 기록한다. 완결 저널 프레임의 checksum·sequence·JSON·경로/범위 메타데이터가 잘못됐거나 identity JSON이 손상되면 `DeltaCorrupt`로 실패하고 파일을 그대로 보존한다. 불완전한 마지막 프레임만 기존 계약대로 재생에서 제거하며 완결 손상을 잘라내지 않는다.

provider 제어 JSON과 workspace 상태는 같은 디렉터리의 임시 파일을 완성·flush한 뒤 기존 파일을 `File.Replace`로 교체한다. 최초 게시만 덮어쓰기 없는 move를 사용하고 독자는 delete sharing을 허용한다. VSS 영수증도 같은 방식이다. Windows provider IPC는 일시적 공유 오류 32/33/1175에서 같은 임시 파일로 최대 1초 재시도한다. 읽기 전용 속성 변경·목적 파일 선삭제·내용 덮어쓰기는 하지 않는다. 지속 실패의 임시 파일은 보존하고 내부 `state.json`에 `errorHResult`, `errorStackTrace`, `publicationPath`, `publicationTemporaryPath`를 남긴다. 외부 workspace 응답 schema나 저널 flush/seal 계약은 바뀌지 않는다. [재현·회귀 증거](verification/storage-recovery.md)를 따른다.

Windows 제어 파일 reader는 `File.Exists`로 읽기 가능 여부를 판정하지 않고 경로 guard와 공유 삭제를 허용하는 open을 수행한다. 교체 중의 파일/디렉터리 부재와 공유 오류 32/33에는 단조 시간 기준 최대 1초, 최대 25ms 간격으로 같은 경로를 다시 연다. deadline 뒤에도 실제 부재이면 기존 missing 계약을 반환하며 지속 공유 오류는 예외로 남긴다. 파일 부재는 자원 0의 증거가 아니며 caller의 launch/owner·mount 검사가 계속 필요하다. 매번 경로 guard를 적용하고 JSON 손상·접근 거부·reparse·owner 불일치는 재시도로 덮지 않는다. provider의 선택적 stop/flush/source 요청은 없을 때 다음 poll에서 다시 읽으므로 부재 대기를 생략한다. [IPC 경합 근거](verification/control-file-read-races.md)는 실제 Windows 파일 시험과 합성 client 시험을 구분한다.

손상된 workspace JSON은 `StateCorrupt`, 기존 control/workspace 경로 옆의 누락된 JSON은 `StateRecoveryRequired`, 읽을 수 없는 JSON은 `StateReadUnavailable`이다. 같은 key로 새 작업을 만들지 않는다. 상태 기록 실패는 `StateWriteIncomplete`이며 남아 있는 기록과 임시 파일을 보존한다. 작업 실패 기록마저 저장할 수 없으면 원래 오류 코드를 details에 포함한다. 이 계약은 파일 잠금·중단된 임시 파일로 검증했고 실제 디스크 고갈/전원 중단 검증은 남아 있다.

`StateWriteIncomplete`의 현재 details는 `urn:splinter:v1:state-write-failure-details`를 따른다. `id`·`action`·`diagnostic`이며 실패 기록마저 저장하지 못하면 `operationError`와 중첩 `details`에 같은 계약을 유지한다. 진단은 마지막 시도 `stage`(prepare-record/validate-target/open-temporary/write-temporary/flush-temporary/close-temporary/publish-state/append-event), `exceptionType`·signed `hResult`, `statePublication`을 제공한다. 게시 전은 `not-attempted`, 게시 호출 실패는 교체 여부 `unknown`, 게시 완료 뒤 이벤트 기록 실패는 `completed`다. completed도 명령 성공이 아니며 작업을 보존하고 status로 같은 identity를 관측한다. 새 상태가 보이는 경우에도 응답을 잃은 이전 작업의 성공으로 자동 승계하지 않는다.

Windows에서 FACILITY_WIN32 HRESULT를 확인한 경우만 `nativeErrorDomain=win32`와 `nativeErrorCode`를 제공한다. 미확인 native 오류는 필드를 생략하며 0으로 추측하지 않는다. 원본 예외 message·stack·임시/목적 파일 경로는 진단에 포함하지 않는다. 진단은 atomic 게시·flush·retry·임시 파일 보존 정책을 바꾸지 않으며 validation project ledger의 `ValidationStateWriteIncomplete`는 이 workspace 계약의 범위가 아니다. [진단·실제 파일 시험과 한계](verification/state-write-diagnostics.md)를 따른다.

## 살아 있는 VSS 식별과 읽기 위치

`snapshot/snapshot.json` version 2의 `captureIdentity`는 원래 snapshot ID·system provider ID·volume GUID 경로·볼륨 내 상대 경로·캡처 시각·persistent/differential/no-auto-release 조건을 고정한다. `verification-request.json`은 owner·원래 ID·요청 세대·현재 boot를, `live-observation.json`은 실제 조회 객체의 identity와 현재 device root·조회 성공/실패를 기록한다. 현재 읽기 위치는 검증된 device root와 고정 상대 경로로만 만든 `SnapshotReadAccess`다. `SnapshotReference.RootPath`, overlay identity와 기존 저널은 캡처 당시 그대로 유지한다. available 응답의 `snapshotStorage.readAccess`는 snapshotId·rootPath·verificationId를 제공한다.

새 provider 연결은 이전 실행이 소비한 verification ID와 다른 현재 부팅의 관측을 요구한다. `SnapshotVerificationRequired`의 재개 명령은 동일 ensure다. 서비스 Observe 뒤 같은 요청 재시도는 준비된 관측을 재사용하며 중복 캡처·등록·provider를 만들지 않는다. Observe는 VSS·원본·볼륨 설정을 변경하지 않고 소유 관측 기록만 저장한다. 정상 status는 같은 연결의 검증을 재사용한다. provider는 주기적으로 관측과 현재 읽기 경로를 확인하며 파일 I/O마다 CIM을 호출하지 않는다. 실행 중 다른 접근 정보로 변경되면 거부하고 새 연결에서만 읽기 경로를 바꾼다. 외부 관리자의 VSS 삭제·대체와 모든 동시 경쟁 조건을 검증했다는 뜻은 아니다.

현재 VSS 영수증은 version 2와 captureIdentity를 요구한다. 지원하지 않는 버전·누락된 capture identity는 거부하고 원본 영수증·델타를 보존하며 자동 이관하지 않는다. 다른 owner/ID/provider/volume/상대 경로는 `SnapshotIdentityMismatch`, 손상 JSON은 `SnapshotObservationCorrupt`, 중단된 게시 파일은 `SnapshotObservationIncomplete`다. 최신 조회 실패·새 boot·접근 불가는 이전 성공으로 대체하지 않는다. 실제 재부팅·강제 전원 종료는 별도 미검증 관문이다.

## 지연 회수와 명시적 재시도

`workspace.snapshot.reclamation`은 `snapshotId`, `attemptId`, `disposition`(`deferred`/`released`), `observedAt`, `blockingSnapshotIds`, 선택적 `sharedStorage`를 제공한다. 독립 결과가 검증되고 앱/provider·메모리·핸들 0, mount/등록/delta 회수가 확인된 뒤에만 대기 상태를 저장한다. `snapshot.step`은 `release-snapshot`, workspace phase는 `reclaim-pending`이다. 결과 ref는 유지하며 작업을 다시 mount하거나 새 snapshot으로 바꾸지 않는다.

대기하는 `workspace release`는 `Accepted`, `terminal=false`, `retryDisposition=requires-user-action`, `recovery.disposition=deferred-reclamation`과 같은 `id/home`의 release를 반환한다. 자동 예약이나 폴링이 아니다. `status/list`는 저장된 대기와 마지막 관측을 보여 주며, 조회 성공을 자원 회수 완료로 읽지 않는다. 차단한 오래된 snapshot의 소유 작업이 별도로 명시적으로 회수된 뒤 같은 release를 재시도한다. 타 작업이나 비소유 snapshot은 대신 삭제하지 않는다. 일반 사용자 재시도는 같은 workspace의 설치된 권한 서비스를 호출하며 전체 CLI/Editor를 승격하지 않는다.

`release-request.json`과 `release-deferred.json`은 전체 snapshot reference·owner·현재 boot·요청 ID·시각에 묶인다. helper 완료 후 새 CLI는 아직 소비하지 않은 대기 결과를 회수하고, 이미 저장한 시도의 명시적 재시도는 새 요청을 발행한다. helper는 매번 최신 snapshot 목록과 삭제 차단 조건을 확인한다. 서로 다른 Splinter home의 삭제는 전역 release mutex로 직렬화하지만 외부 백업 프로그램의 동시 삭제를 직렬화하지는 못한다. 손상/불일치 증거는 `SnapshotReclamationInvalid` 또는 기존 관측 오류로 보존한다. 자원 unknown, 결과 ref 소실, 잔존 delta는 재시도에서도 회수를 막는다.

`sharedStorage`는 `volumeId/observedAt/usedBytes/allocatedBytes/limitBytes`와 `scope=source-volume-shared`, `workspaceAttribution=unknown`을 제공한다. 수치는 마지막 helper가 관측한 볼륨 전체 VSS 값이다. 같은 볼륨의 작업끼리 합산하거나 해당 작업의 귀속 비용/삭제 시 반환 바이트로 표시하면 안 된다. 관측 실패 시 숫자는 생략하고 `SnapshotStorageUnknown`을 남긴다. 일반 status는 이 값을 새로 측정하지 않는다. released는 snapshot ID와 소유 자원의 부재를 뜻하며 VSS 할당량이나 OS 캐시가 즉시 0이 된다는 보장은 아니다.

범위와 증거는 [지연 회수 계획](ntfs-workspaces.md)과 [검증 기록](verification/deferred-snapshot-reclamation.md)을 따른다. 자동 sweep, 공유 기준 수명주기와 임의 순서의 즉시 물리 회수는 제공하지 않는다.

## 회수와 관측

개발용 [source view/Library cache](validation-workflow.md)의 미완료 게시·후보·소스 세대·열린 cache는 해당 VSS의 의존성이다. 제품 release/discard는 이들이 남으면 `SnapshotInUse`로 차단하며 연쇄 삭제하지 않는다. Editor 없는 worker도 제품 `workspace checkpoint`로 보존한다.


회수 순서는 앱/자식 종료 → 소유 브리지 제거 → 수집 전 flush/seal → 현재 source 검사와 독립 결과 ref 보존 → 수집 후 flush/seal 대조 → manifest 저장 → unmount와 provider 종료 → seal 재대조 → Git 등록 회수 → delta 회수 → 마지막 의존성이 없는 소유 VSS ID 해제다. 브리지 제거는 합법적인 저널 변경이므로 수집 구간 전에 마친다. 관리 캐시 밖의 ignored 파일, 미보존 변경, 다른 의존 작업이 있으면 중단한다. 수집 중이나 이후의 쓰기로 seal이 달라지면 `DeltaChangedAfterCollection`로 중단하고 기존 manifest·저널·snapshot을 보존한다. delta 회수 중 응답이 유실되면 기존 삭제 intent와 남은 파일을 검증하고 이어 간다. VSS 해제 helper는 정확한 소유 ID만 받으며 전역 삭제를 하지 않는다. 다만 정확한 ID 삭제 중에도 provider가 오래된 다른 snapshot을 제거한 [실패 기록](verification/storage-foundation.md)이 있다. 현재는 같은 볼륨의 다른 오래된/동일 시각/시각 불명 snapshot 또는 볼륨 정보 불명이 있으면 `SnapshotReleaseIsolationRequired`로 삭제 전에 차단한다. `release-isolation.json`에 전후 ID와 삭제 시도 여부를 기록하고 다른 ID 소실은 `SnapshotCollateralLoss`다. 기존 snapshot을 지워 우회하지 않는다. 차단과 오래된 순서의 회수는 [격리된 소유 합성 snapshot](verification/storage-recovery.md)에서 실제 검증했으며 provider 전체·일반 비소유 백업 공존의 보존 보장은 아니다. 더 오래된 snapshot이 사용 중이면 나중에 끝난 작업도 마지막 snapshot 회수 단계의 `reclaim-pending`으로 남는다. 결과 보존과 회수 대기를 구분하고 같은 작업의 명시적 release 재시도로 이어간다. 이 상태는 완전한 release 성공이 아니다.

snapshot이 소실된 뒤에는 `stable-journal-v1`로 수집한 독립 결과가 있고 Git ref/객체와 **저널 전체 seal이 동일한 경우에만** 회수할 수 있다. 사용할 수 없는 원본 트리를 다시 읽거나 수정하지 않고 unmount 후 seal을 비교한다. 독립 결과가 없거나 수집 뒤 쓰기가 있으면 결과와 저널을 보존하고 실패한다. 열린 핸들이 남은 unmount는 `OverlayStopPending`이며 핸들을 닫은 뒤 같은 release를 재시도한다.

결과 manifest의 `collectionProtocol`은 필수이며 `stable-journal-v1`만 허용한다. 누락·지원하지 않는 값은 `StateCorrupt`로 거부하고 manifest·저널·snapshot을 보존한다. 현재 checkpoint가 Git commit 뒤 수집 전에 중단되어 아직 결과 manifest가 없는 경우에는 같은 작업의 checkpoint를 재시도할 수 있다. 이미 게시된 잘못된 manifest를 자동 재수집하거나 필드를 수동 수정하는 이관은 제공하지 않는다. 소스 검사는 [변경 기반 검증](source-verification.md)을 사용하고 초기·감사·provider 재연결 시 전체 읽기를 수행한다. 저널 전체 seal과 Git 객체 생성의 읽기는 수집 경계에 남으며 `source-inspections.jsonl`·`storage-reads.jsonl`에 계측한다.

`workspace.snapshot.observation`은 snapshot/mount/provider/delta 존재, 소유권, 핸들·working set·private bytes, host free space, 저널 잔여량을 제공한다. 누락된 자원 값은 unknown이며 0으로 읽으면 안 된다. `storage`의 생성 payload·읽기 cache·저널 payload/metadata·sequence는 provider 계측이다. 저널 길이는 실제 NTFS file-data allocation이나 VSS 사용량이 아니다. `snapshotStorage.preservedBytes/allocatedBytes`는 현재 일반 호출에서 관측하지 못하므로 생략한다. 관리자 helper의 별도 볼륨 관측과 실제 allocation 계측이 필요하다.

`failureCode`에는 snapshot 소실·압박·provider 실패 또는 마지막 provider I/O 실패 코드가 표시된다. `DeltaFull`이면 기존 데이터는 남으며 현재 작업의 quota를 자동 확장하거나 데이터를 지우지 않는다. 성공한 release에는 앱/자식/provider와 소유 메모리·핸들 0, mount·delta·snapshot 부재를 재관측한 값이 남는다. `storage`의 누적 쓰기량은 마지막 실행의 이력이며 회수 뒤 남은 실제 공간을 뜻하지 않는다. released 조회는 마지막 회수 영수증을 반환한다. OS standby/cache RAM을 비우는 계약은 아니다.

| 오류 | 재개 행동 |
| --- | --- |
| `StorageServiceMissing` / `StorageServiceUnavailable` / `StorageScopeDenied` | `storage service status`와 설치된 grant를 확인하고 명시적 설치/구성 후 같은 workspace 명령 재시도 |
| `StorageOperationUncertain` / `StorageRequestPending` | 보호된 기존 intent를 유지하고 같은 workspace·요청으로 재시도. 새 capture나 타 작업 회수로 우회하지 않음 |
| `RequestKeyConflict` | 저장된 입력으로 재호출하거나 별도 작업에 새 key 사용 |
| `SnapshotMissing`, `SnapshotIdentityMismatch` | 새 원본으로 대체하지 않음. 소실된 snapshot은 기존 독립 결과와 저널 seal이 일치할 때만 release 가능 |
| `SnapshotFileUnsupported`, `SnapshotFilterUnsupported` | 지원하지 않는 입력을 별도 검증 대상으로 유지 |
| `SnapshotHeadroomRequired`, `DeltaFull` | 기존 결과를 보존하고 공간/한도와 helper 관측 확인. 자동 삭제·전체 복사 없음 |
| `CompactionClientsRemain`, `CompactionProviderNotStopped` | 소유 Editor·프로세스와 mount 핸들 관측을 해결한 뒤 같은 `workspace compact` 재시도 |
| `CompactionHeadroomRequired` | 여유 공간을 확보한 뒤 같은 ID/home으로 정리 재시도. snapshot이나 이전 저널을 먼저 삭제하지 않음 |
| `CompactionGenerationMismatch`, `CompactionStateMismatch`, `CompactionCandidateMismatch`, `CompactionIntentCorrupt`, `CompactionIntentMismatch` | Core 공통 보존 정책. 실패 응답은 `requires-user-action`과 같은 ID/절대 home의 `workspace.status`, 지속 recovery는 `preserve`. 세대·intent·후보를 보존하고 원인을 확인 |
| `OverlayClientsRemain`, `ProcessCleanupIncomplete` | 소유 앱/자식 종료 상태 확인 후 재호출 |
| `OverlayStartUncertain`, `LaunchUncertain` | 정확한 실행 intent·PID/시작 시각·generation 조사. 중복 실행/강제 회수 없음 |
| `UncollectedChanges`, `UnownedIgnoredFiles`, `DeltaChangedAfterCollection` | 유일한 변경을 보존하고 검증/결과 정합성 확인 |
| `SnapshotResultInvalid` | 이전 수집 증거로 회수를 재개하지 않고 남은 자원·기록 보존. 원래 기준을 사용할 수 있는 수집 전 상태에서만 재수집 |

## 구현과 검증 경계

Core의 `ISnapshotWorkspaceBackend`와 `ISnapshotSourceWorkspaceProvider`는 Windows나 Git API를 노출하지 않는다. Runtime은 단계·잠금·결과/의존성 정책, Git 어댑터는 등록·index·결과 ref, Windows 어댑터는 VSS helper·특수 파일 검사·WinFsp·저널 관측/회수를 맡는다. CLI가 이들을 조립하며 Host는 읽기 전용 in-process 계층을 유지한다.

기존 [ReFS 기준본](storage-and-resources.md)은 선택적 경로다. NTFS 실패를 ReFS 이관·전체 checkout·파일 전체 copy-up으로 바꾸지 않는다. 자동 시험의 fake/Git/Windows 메타데이터 증거와 실제 mount/Unity 실행 근거는 NI 기록에서 구분한다. [NR-01~08 계획](plans/ntfs-recovery-and-support.md)의 공통 복구 응답과 상태 파일 보존은 부분 구현·자동 검증했고, 실제 재부팅·공간 장애·파일 지원/거부는 [NR 기록](verification/ntfs-recovery.md)의 미완료 조건이다. 특수 파일 지원 확대, 일반 에셋/패키지, 대형 프로젝트 성능과 공개 제품 전체는 이후 범위로 남긴다. 구매 에셋은 NI 및 다음 NR 시험에 필요하지 않다.
