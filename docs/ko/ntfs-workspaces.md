# NTFS 프로젝트 작업공간

현재 task 상태는 **v3**, 관리형 basis는 v2다. [반복 원본 반영](repeated-original-apply.md)이 실행 중 수집·불변 결과 버전·경로별 반영 이력·실행 중 원본의 정확한 외부 협력을 소유한다. [RI 자격](verification/repeated-original-apply.md)은 과거 종료형 Editor 근거와 구분하며 이전 task는 이관 없이 보존·거부한다.

[English](../ntfs-workspaces.md)

현재 제품은 `git-snapshot`, `plastic-snapshot`, `project-snapshot` 기준본과 WinFsp 범위 저널을 조합한다. [Git](git-project-basis.md), [Plastic](plastic-project-basis.md), [VCS 없는 프로젝트](plain-project-basis.md)의 입력·준비 조건을 각각 따른다. `ntfs-snapshot`·VSS·전용 서비스는 [폐기](plans/vss-retirement.md)했으며 구형 상태를 보존·거부한다. 아래 공통 저널·경로·결과·복구 보호는 유지한다.

보존과 회수가 모두 승인되고 중간 통합이 없으면 `workspace checkpoint --release`를 사용한다. 소유 Editor 정상 종료·독립 결과·seal·회수 보호를 유지하며 checkpoint는 컴파일 성공을 주장하지 않는다. [LF-01–05](verification/lifecycle-finalization-optimization.md)를 따른다.

## 입력과 권한 경계

아래 Git의 clean/HEAD/관리 등록 조건은 `--source git` 경로다. 일반 폴더는 [VCS 독립 계약](vcs-independent-workspaces.md), Plastic은 [관리 정보/등록 호환 계약](plastic-compatibility.md)을 따르며 공통 NTFS/WinFsp·소유 자원·범위 저장·회수 guard를 공유한다.

- Windows의 기존 NTFS, 설치된 WinFsp, 저장된 clean Git repository root를 대상으로 한다. 첫 Unity 검증 대상은 별도 `splinter-unity` fixture다. 원본의 현재 HEAD와 `--base`가 같아야 하며 `.git` 디렉터리가 원본 안에 있어야 한다.
- dirty/staged/untracked 변경, skip-worktree/assume-unchanged, submodule/symlink, LFS·사용자 filter·working-tree-encoding은 거부한다. 필터는 checkpoint와 결과 보존 시에도 검사한다. 원본을 stash/reset하거나 임의로 커밋하지 않는다.
- 원본과 불변 스냅샷의 메타데이터를 검사하여 ADS, hardlink, reparse, EFS, 압축·희소 파일을 `SnapshotFileUnsupported`로 거부한다. `.git`은 기준 트리에서 제외한다. 이 검사는 파일 payload를 읽거나 복제하지 않는다. 개별 ACL 변경은 지원하지 않으며 overlay가 원본의 모든 ACL 의미를 재현한다고 주장하지 않는다.
- reparse/EFS/압축/희소 속성은 기준 트리의 지연 읽기와 WinFsp create/overwrite/속성 변경에도 같은 규칙으로 거부한다. overwrite는 기존 길이를 줄이기 전에 검사한다. 거부된 callback은 바이트·메타데이터·저널 sequence를 유지하며 OS에는 `STATUS_NOT_SUPPORTED`를 반환한다. ARM64 실제 mount에서는 ADS·hardlink·희소·압축·mount-point reparse 요청 5개의 거부를 일반 NTFS 양성 대조와 함께 확인했다. 불변 입력은 fake이며 VSS 입력·EFS·ACL·특수 FSCTL의 전체 검증은 남아 있다. 실행 범위는 [NR 기록](verification/ntfs-recovery.md)을 따른다.
- 캡처 직전 호출자는 원본 Unity가 저장된 상태이고 import/compile/Play 중이 아닌지 확인한다. 캡처는 application writer 없는 filesystem crash consistency다. 미저장 메모리를 포함하지 않는다. 캡처 후에는 원본을 계속 수정할 수 있다.
- `--home`은 짧은 경로를 사용한다. NI의 깊은 시험 경로에서는 Unity 패키지 DLL 전체 경로가 270자일 때 초기 로드 예외가 관측됐다. 스크립트 검증과 실행은 통과했지만 긴 패키지 경로와 일반 에셋 로드를 지원으로 확정하지 않는다. 초기 임포트 시간의 SLA도 없다.

WinFsp 디렉터리 열거는 루트 이외의 디렉터리에 실제 디렉터리 정보와 함께 `.`·`..` 항목 및 marker 기반 재개 순서를 제공한다. 빈 하위 디렉터리도 Win32 `FindFirstFileW(<dir>\*)`로 열거할 수 있다. 이 항목은 overlay의 파일이나 저널 기록이 아니다. [작은 Unity 실환경 회귀](verification/journal-compaction-qualification-and-cost.md)는 수정 전 빈 폴더 열거 실패와 수정 후 성공을 대조했다.

## 일반 CLI 흐름

명시한 clean Git fixture/home에서 쓰기 중단을 재현하는 [제품 개발 시험](product-write-recovery.md)은 일반 provider를 유지하고 외부 client 경계에서 정확한 소유 provider만 종료한다. 같은 ensure·현재 불변 기준본 read proof·독립 결과/release를 대조하며, 일반 workspace 사용 중 자동으로 gate를 켜거나 실패 작업을 회수하지 않는다.

아래 `splinter`는 제품 저장소에서 `pwsh ./scripts/splinter.ps1`이다. 빌드는 `pwsh ./scripts/build.ps1`로 한다. `--home`은 원본 프로젝트 밖의 같은 지속 상태 경로를 모든 작업자가 공유한다.

```text
splinter workspace ensure --project <원본 Unity 저장소> --key <고유한 작업 key> --base development --storage git-snapshot --home <상태 경로>
splinter workspace status --id <workspace.id> --home <상태 경로>
splinter unity start --id <id> --home <상태 경로>
```

공통 WinFsp mount/reconnect는 실행 중인 정확한 provider의 실제 mount와 최신 `mounted` 상태를 최대 2분 기다린다. `starting`/`replaying`은 준비 완료가 아니며 한도 초과나 취소로 provider·저널을 삭제하지 않는다. 같은 요청 재시도는 그 provider를 이어서 확인한다. 저널은 64KiB 수명 한정 버퍼로 읽되 모든 frame 검증을 유지하며 새 replay 단계 계수의 의미는 [계측 계약](source-verification.md#계측의-의미)을 따른다. 이것이 새 Unity 실행 모드나 기본 방식 전환을 뜻하지 않는다.

```text
splinter workspace status --id <id> --home <상태 경로>
splinter workspace compact --id <id> --home <상태 경로>
splinter workspace checkpoint --id <id> --request-key <capture-key> --message <결과 설명> --home <상태 경로>
splinter workspace release --id <id> --home <상태 경로>
```

Editor의 refresh·컴파일·Play/테스트 등은 반환 workspace 경로를 선택한 외부 MCP·Unity CLI에서 수행한다. 제품 bridge는 liveness·진행 상태와 정상 종료만 처리하고 프로젝트 함수를 실행하거나 합격을 판정하지 않는다. checkpoint는 컴파일 성공 없는 소스도 안정된 독립 결과로 보존한다. 실제 게임/코드 검증 증거는 선택한 도구에서 확인한다.

`workspace compact`는 **최초 소스 결과 수집 전**의 연결된 NTFS 작업공간 하나에서 명시적으로 실행한다. 소유 Editor와 자식 프로세스가 종료되고 mount의 열린 핸들이 0이며 snapshot 가용성과 소유권이 확인돼야 한다. 같은 workspace의 다른 수명주기 명령은 `compact-*` 단계 동안 거부한다. provider를 멈춘 뒤 unmount 전후 저널 seal을 대조하고, 원래 snapshot·작업 ID·Git 등록을 그대로 사용해 새 저널 세대를 검증·발행·재연결한다. 정리 뒤 source 내용과 seal을 다시 확인하며 프로젝트 검증은 에이전트가 필요에 맞게 수행한다. 이미 완료한 세대의 같은 명령 재시도는 seal이 같을 때 읽기 전용 결과를 반환하며, 이후 새 쓰기가 있으면 다시 명시적으로 압축할 수 있다. `checkpoint`를 중간 압축용으로 사용하지 않는다.

`--optimize-journal true`와 `--revert-base-ranges true`는 [현재본 저널 ON](latest-only-journal.md)을 선택하며 이후 writer/reconnect도 완료한 현재 private payload·metadata만 유지한다. 명시적 정리에서만 고정 기준과 같은 private 범위를 제거한다. 새 일반 Windows provider 기본 [bounded](bounded-journal.md)·기존 작업과 옵션 생략 시 저장된 선택 유지·원래/기대 구조 hash·metadata/identity 보존과 v3 compaction intent는 [기준본 복귀 계약](journal-base-reversion.md)을 따른다. OFF의 기준 읽기 0과 작은 frame fallback을 ON으로 승계하지 않는다.

선택적 `--max-estimated-journal-overlap-bytes <양의 정수>`는 기존 저널의 논리 길이와 후보 저널의 사전 계산 논리 길이의 합계를 제한한다. 지정하지 않으면 기존 정리 동작을 유지한다. 합계가 예산을 초과하면 새 후보 파일·세대 게시 전에 `CompactionOverlapBudgetExceeded`와 두 길이·합계를 반환한다. 그때 provider는 정지된 `resume-compaction` 상태이므로 같은 ID/home의 `workspace compact`로 명시적으로 재시도한다. 인자를 생략한 재시도는 저장된 예산을 유지하고, 값을 명시해야 바뀐다. 이미 후보가 게시된 `compact-connect` 단계에서는 새 예산이 복구를 막지 않는다. 성공하면 pending 예산을 지운다. 이 옵션은 **예상 저널 파일 길이의 중첩량**만 제한하며 NTFS 실제 할당, control/Git/Library, VSS diff area 또는 작업공간 전체 물리 peak의 상한이 아니다. 실제 판단에는 [읽기 전용 공간 장부](source-verification.md#계측의-의미)를 함께 사용한다.

예산 검사는 provider 정지 뒤 기존 저널 재생·seal·상태 hash·후보 scan을 수행하므로 거부에도 읽기 시간과 Editor 정지가 필요하다. 중단된 이전 시도에 이미 부분 후보 파일이 있으면 낮은 예산의 거부도 그 파일을 삭제하지 않는다. 예산은 앞으로 작성할 후보를 검사하며 이미 쓰인 공간을 자동 회수하지 않는다. 원본 저널·intent·기존 부분 후보의 소유와 상태를 확인한 뒤 같은 작업의 명시적 재시도로 진행한다.

OFF의 일반 SJR2 정리는 살아 있는 범위를 chunk 단위로 다시 기록하고 같은 경로의 연속된 무효 쓰기를 하나의 sequence 구간으로 묶는다. 범위/무효 쓰기 메타데이터와 payload의 합계가 기존 frame보다 커지면 기존 frame을 유지한다. 개별 skip 표현이 더 크면 같은 경로의 묶음이 더 작을 때만 변환한다. 이 경로는 후보 저널의 논리 길이를 늘리지 않지만 더 작은 표현을 위해 일부 무효 payload를 남길 수 있다. ON은 이전 payload를 회수하며, ON→OFF 전환/독립 candidate는 현재 상태 초기화 prefix로 표현해 metadata 비용이 더 커질 수 있다. 사전 예산 계산과 후보 작성은 각 경로에서 같은 규칙을 사용한다. 기존 [크기 회귀 검증](verification/journal-compaction-size.md)은 당시 일반 SJR2 경로에 한정한다.

whiteout·rename·truncate·`.meta`와 변경 경로를 재생 대조하며 파일 전체를 copy-up하거나 기준 파일을 읽어 채우지 않는다. 새 후보는 같은 디렉터리에 flush하고 checksum·전체 재생 상태를 확인한 뒤 원자적으로 게시한다. `delta.journal.previous`는 새 세대의 mount 재연결과 seal이 확인될 때까지 남긴다. `compaction-intent.json`과 후보/이전 세대의 소유·hash가 맞지 않거나 내용이 손상되면 파일을 보존하고 `preserve`로 중단한다. 준비·게시·재연결 중 끊기면 같은 ID/home의 `workspace compact`로 `resume-compaction`을 진행한다. 자동 sweep이나 다른 workspace·snapshot 회수와 연결하지 않는다.

`snapshot.compaction`에는 attempt, 전후 sequence/길이/SHA-256 seal, 전후 NTFS 파일 할당과 두 저널 세대가 함께 있던 전환 지점의 할당 합계를 기록한다. 새 시도의 `cost`는 `stopwatchFrequency`·`totalTicks`와 stage별 `elapsedTicks`·`readBytes`·`writtenBytes`·`flushCount`를 제공한다. stage에는 원본 replay/seal/상태 hash, 후보 scan/write/flush/replay/seal, 세대 게시·검증, provider 재연결·이전 세대 정리가 들어간다. `totalTicks`는 오프라인 저널 정리 호출만 재며 재연결·이전 세대 정리는 별도 stage다. 이미 게시된 세대를 확인만 하는 재시도에는 `cost`가 없을 수 있고, 누락은 0 비용이 아니다. 두 세대 할당 합계와 stage 표본은 control/identity/Git/Library를 포함한 전체 작업공간 peak가 아니다. 후보를 쓸 여유와 재연결의 2 GiB 하한이 없거나 계산한 후보가 journal budget을 넘으면 기존 세대를 보존한다. VSS 사용량은 볼륨 공유 관측이며 작업별 절감으로 귀속하지 않는다. 현재 구현은 범위 재기록이며 일반 목적 payload 압축이나 쓰기마다 수행하는 flush 정책 변경이 아니다. [정리 1차](verification/journal-compaction.md), [Unity 재개·공간 비용](verification/journal-compaction-qualification-and-cost.md), [중첩량 예산](ntfs-workspaces.md)을 따른다.

일반 status는 소유 앱/provider를 관측하고 프로젝트 내용을 읽지 않는다. `--verify-source`의 `sourceVerification=verified` 응답에는 현재 `source`가 있으며 `not-checked`에는 이를 생략한다. source fingerprint는 저장 내용 검사이고 컴파일/테스트 성공이 아니다. checkpoint/integrate는 현재 소스와 독립 결과를 다시 확인한다.

checkpoint는 실행 중 Editor를 유지하며 안정된 요청 키로 소스 metadata/범위·영속 backing pin을 고정한다. payload 읽기·hash·독립 Git/파일 결과 보존은 mutation gate 밖에서 수행한다. 같은 task에서 이후 편집·Editor 재시작을 허용한다. 불변 ID·버전별 영수증/ref를 사용하며 단일 task 결과를 덮어쓰지 않는다. 최종 stop/bridge 제거·전체 전후 seal·unmount 증명은 같은 결과 엔진을 사용한다. 경로별 반영 상태·HEAD/index를 유지하는 Git 원본 파일 게시·실행 중 원본 허가·정확한 재시도는 [반복 원본 반영](repeated-original-apply.md)을 따른다. 과거 checkpoint/소스 검사 시간은 당시 정지형 엔진 후보의 근거다.

실행 중 volume/heartbeat는 배타적 writer 계수(`journalBytesBasis=writer-accounting`), 명시적/최종 관측은 현재 파일을 검사한다. poisoned writer는 저장량 unknown과 살아 있는 소유권/핸들을 보존하고 reconnect를 요구한다. 파일 알림은 source/flush/stop 요청을 깨울 뿐 승인하지 않는다.

SJL4 재생/inventory는 checksum·identity·소유권 검사를 생략하지 않고 backup 세대와 payload 경로를 인덱스로 찾는다. 새 SJL4 회수는 소유 요청과 stopped 바이트/sequence 관측을 검사한 뒤 바깥 intent에 저장된 예상 seal을 고정한다. 기존 내부 reclaimer가 검증된 inventory 생성과 파일 삭제 전에 전체 seal을 비교한다. 바깥 intent만으로는 삭제할 수 없다. OFF/현재 저널 부재 경로는 바깥 전체 seal을 유지하고, 중단 복구도 동일 intent/inventory/fence 검사를 유지한다. 형식과 개별 durable flush/ACK 의미는 바뀌지 않는다.

원본 파일은 검토된 계획/intent/digest와 필요한 실행 중 원본 허가로 `workspace integrate --target original --result <id>`를 사용한다. `--target development` 같은 명시적 branch merge는 별도 기능이다. 원본은 요청한 브랜치의 clean 상태여야 한다. 충돌 시 이 명령이 시작한 merge만 중단하고 결과를 보존한다. **독립 결과를 보존한 NTFS 작업은 원본 통합 없이 회수할 수 있다.** 실제 NTFS fixture에서 원본 README에 미커밋 변경을 넣고 통합을 요청하면 `GitDirtyWorktree`로 거부하며 원본 HEAD·인덱스·변경 내용과 결과 ref를 보존하는 것을 확인했다. 실패한 검증이나 미커밋 변경은 자동 삭제하지 않는다. 원본 통합·Editor 조작의 공통 절차는 [알파 작업공간](alpha-workspaces.md)을 따른다.

## 지속 상태와 재개

workspace의 Git 명령에는 10분 제한과 호출자 취소가 적용된다. 큰 에셋의 초기 index 확인도 파일 내용을 읽을 수 있다. index 파일이 이미 있어도 중단된 attach의 재시도는 stat cache refresh를 완료한다. `GitCommandTimeout` 뒤에는 상태와 남은 잠금을 조사해야 하며 제품은 `index.lock`을 자동 삭제하지 않는다. 진단용 Git inspector의 10초 제한과 별도다.

```text
<home>/records/<id>/workspace.json         identity, 단계, 이전 증거, 결과 manifest
<home>/records/<id>/events.jsonl           작업 intent와 완료·실패
<home>/records/<id>/snapshot/              소유 프로젝트 기준본 영수증과 해제 허가
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

읽기는 저널 gate 안에서 파일 메타데이터·private 바이트·덮이지 않은 불변 기준 범위를 고정한 뒤, 별도 직렬 reader gate에서 기준 범위를 읽는다. 느린 기준 I/O 중 조회·변경이 진행되며 덮어쓰기·truncate·rename·unlink와 겹쳐도 읽기는 고정한 파일 상태를 반환한다. private payload 접근·회수는 저널 gate 안에 유지한다. 완료 때 writer poison·저널 길이·기준 가용성을 재검사한다. 종료는 새 호출을 거부하고 모든 동시 종료 호출이 진행 중 읽기를 기다린 뒤 저널·소유 fence를 닫는다. disk payload cache나 별도 파일 복제는 만들지 않는다. 일반 저널 경로에 적용하며 개발용 source view는 외부 gate를 유지한다. [LO-06–08 검증](verification/lifecycle-read-optimization.md)을 따른다.

디렉터리 목록은 정확한 기준 경로가 일치할 때만 불변 자식 메타데이터를 재사용한다. private·이름 변경·삭제와 포함/제외/금지 정책의 우선순위를 유지하고 예상 밖 경로는 정확한 `Stat` 조회를 수행하며 목록 뒤 기준본을 다시 검사한다. cache 추가나 timestamp 변경 없이 중복 자식 조회를 제거한다. [LO-10](verification/lifecycle-read-optimization.md)이 실제 NTFS 작업량 비교를 기록한다.

## 동일한 private 범위의 반복 쓰기

일반 파일 rename은 해당 node만 이름 변경하며 전체 경로/whiteout을 검색하지 않는다. 디렉터리 rename은 경계 `/`를 포함한 접두사로 알려진 하위 경로와 whiteout을 이동한다. 재생에는 변경되지 않은 부모 node 없이 하위 파일만 존재할 수 있으므로 이 경우 직접 자식 목록 index만으로 대체하지 않는다. 모든 이전 이름을 제거한 뒤 새 이름을 등록하며 대소문자 변경·열린 파일 ID·lazy 기준 자식을 유지한다.

일반 읽기와 sealed private 복원 읽기는 같은 이진 검색으로 첫 겹치는 extent를 찾고 필요한 범위만 순회한다. 기존 `ReadRangeIndex`에는 두 종류의 호출·검색 probe·방문 extent가 포함된다. 복원은 exact seal과 private coverage를 요구하며 hole을 원본 읽기로 대체하지 않는다.

현재 쓰기 범위 전체가 이미 승인된 private 저널 범위에 있고 바이트가 같으면 payload를 다시 쓰지 않는다. raw 저널은 최대 64KiB 버퍼로 비교하고 encoded extent는 [전체 블록을 한 번 순회](journal-payload-compression.md)한다. 기존 `metadata` frame에 수정 시각·현재 경로·새 sequence를 기록해 개별 `Flush(true)` 뒤 응답한다. 빈 쓰기의 기존 no-op과 다르며 소스 변경 경로와 승인 쓰기 경계는 유지한다.

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

기준/복원 보호 오류 `ProjectBasisMismatch`, `GeneratedBasisChanged`, `GeneratedRestoreConflict`, `RestoreSealMismatch`, `PlasticBasisInvalid`, `GitBasisReadTimeout`은 Core 공통 보존 정책을 사용한다. 실패 응답은 `requires-user-action`, durable workspace recovery는 `preserve`/`workspace.status`다. 같은 기준/id/home·영수증·저널을 보존하고 원인을 확인하며 입력 변경이나 새 작업으로 우회하지 않는다. 고정 Plastic revision의 잘못된 입력은 별도 `PlasticBasisRequestInvalid`/`correct-request`로 구분한다.

저널의 디렉터리 조회는 부모별 자식 identity 색인에서 해당 폴더만 정렬해 반환한다. 기준의 lazy entry와 생성·삭제·교체·디렉터리 이동·재생에서 같은 색인을 갱신한다. 조회마다 다른 폴더의 전체 경로를 순회하지 않으며 기존 metadata 상한·mutation gate·개별 flush/ACK·SJR2 형식은 유지한다. 색인은 bounded 관리 metadata 비용이며 실제 Unity 준비 시간이나 전체 RAM 절감을 보장하지 않는다.

스냅샷 접근 실패는 `SnapshotObservationUnknown`이다. `snapshotAvailability`와 `snapshotStorage.availability`는 `available`/`missing`/`unknown` 중 하나이며 unknown에서는 기존 boolean 필드를 생략한다. 일반 ensure와 회수 전 단계는 관측 복구까지 보존한다. 관측한 부재와 접근 오류를 구분하며, 경로가 파일로 바뀌면 `SnapshotIdentityMismatch`로 보존한다. 정확한 ID의 성공한 부재 조회만 missing으로 판정하며 읽기 경로의 소실만으로 VSS 부재를 주장하지 않는다. 새 연결의 live identity 대조는 아래 계약을 따른다.

provider의 `state.json`도 검증된 접근 관측을 사용하며 관측 불가를 `snapshot-unknown`으로 기록한다. 완결 저널 프레임의 checksum·sequence·JSON·경로/범위 메타데이터가 잘못됐거나 identity JSON이 손상되면 `DeltaCorrupt`로 실패하고 파일을 그대로 보존한다. 불완전한 마지막 프레임만 기존 계약대로 재생에서 제거하며 완결 손상을 잘라내지 않는다.

provider 제어 JSON과 workspace 상태는 같은 디렉터리의 임시 파일을 완성·flush한 뒤 기존 파일을 `File.Replace`로 교체한다. 최초 게시만 덮어쓰기 없는 move를 사용하고 독자는 delete sharing을 허용한다. Windows provider IPC는 일시적 공유 오류 32/33/1175에서 같은 임시 파일로 최대 1초 재시도한다. 읽기 전용 속성 변경·목적 파일 선삭제·내용 덮어쓰기는 하지 않는다. 지속 실패의 임시 파일은 보존하고 내부 `state.json`에 `errorHResult`, `errorStackTrace`, `publicationPath`, `publicationTemporaryPath`를 남긴다. 외부 workspace 응답 schema나 저널 flush/seal 계약은 바뀌지 않는다. [재현·회귀 증거](verification/storage-recovery.md)를 따른다.

Windows 제어 파일 reader는 `File.Exists`로 읽기 가능 여부를 판정하지 않고 경로 guard와 공유 삭제를 허용하는 open을 수행한다. 교체 중의 파일/디렉터리 부재와 공유 오류 32/33에는 단조 시간 기준 최대 1초, 최대 25ms 간격으로 같은 경로를 다시 연다. deadline 뒤에도 실제 부재이면 기존 missing 계약을 반환하며 지속 공유 오류는 예외로 남긴다. 파일 부재는 자원 0의 증거가 아니며 caller의 launch/owner·mount 검사가 계속 필요하다. 매번 경로 guard를 적용하고 JSON 손상·접근 거부·reparse·owner 불일치는 재시도로 덮지 않는다. provider의 선택적 stop/flush/source 요청은 없을 때 다음 poll에서 다시 읽으므로 부재 대기를 생략한다. [IPC 경합 근거](verification/control-file-read-races.md)는 실제 Windows 파일 시험과 합성 client 시험을 구분한다.

손상된 workspace JSON은 `StateCorrupt`, 기존 control/workspace 경로 옆의 누락된 JSON은 `StateRecoveryRequired`, 읽을 수 없는 JSON은 `StateReadUnavailable`이다. 같은 key로 새 작업을 만들지 않는다. 상태 기록 실패는 `StateWriteIncomplete`이며 남아 있는 기록과 임시 파일을 보존한다. 작업 실패 기록마저 저장할 수 없으면 원래 오류 코드를 details에 포함한다. 이 계약은 파일 잠금·중단된 임시 파일로 검증했고 실제 디스크 고갈/전원 중단 검증은 남아 있다.

짧은 CLI 명령은 workspace ID의 파일 잠금을 수명 전체에 유지한다. Windows의 확인된 sharing/lock violation만 100ms 간격으로 최대 5초 기다린 뒤 `WorkspaceBusy`(`safe-to-retry`)를 반환한다. 잠금 경로의 누락·접근 거부·다른 I/O 오류는 즉시 `StateReadUnavailable`(`requires-user-action`)로 거부하며 잠금 파일/디렉터리를 삭제·재작성하지 않는다. 서로 다른 workspace ID의 일반 조회·재개는 독립 잠금을 사용한다. 프로젝트의 등록/반영과 공유 snapshot/기준 보호에는 별도 기존 잠금이 적용된다. 임의 파일 편집의 대화별 배타 잠금이나 서비스 mutation의 병렬화를 제공하지 않는다. 선택한 동시 진행 자격은 [SP 검증](verification/stable-parallel-completion.md)을 따른다.

`StateWriteIncomplete`의 현재 details는 `urn:splinter:v1:state-write-failure-details`를 따른다. `id`·`action`·`diagnostic`이며 실패 기록마저 저장하지 못하면 `operationError`와 중첩 `details`에 같은 계약을 유지한다. 진단은 마지막 시도 `stage`(prepare-record/validate-target/open-temporary/write-temporary/flush-temporary/close-temporary/publish-state/append-event), `exceptionType`·signed `hResult`, `statePublication`을 제공한다. 게시 전은 `not-attempted`, 게시 호출 실패는 교체 여부 `unknown`, 게시 완료 뒤 이벤트 기록 실패는 `completed`다. completed도 명령 성공이 아니며 작업을 보존하고 status로 같은 identity를 관측한다. 새 상태가 보이는 경우에도 응답을 잃은 이전 작업의 성공으로 자동 승계하지 않는다.

Windows에서 FACILITY_WIN32 HRESULT를 확인한 경우만 `nativeErrorDomain=win32`와 `nativeErrorCode`를 제공한다. 미확인 native 오류는 필드를 생략하며 0으로 추측하지 않는다. 원본 예외 message·stack·임시/목적 파일 경로는 진단에 포함하지 않는다. 진단은 atomic 게시·flush·retry·임시 파일 보존 정책을 바꾸지 않으며 validation project ledger의 `ValidationStateWriteIncomplete`는 이 workspace 계약의 범위가 아니다. [진단·실제 파일 시험과 한계](verification/state-write-diagnostics.md)를 따른다.

## 회수와 관측

개발용 [source view/Library cache](view-workflow.md)의 미완료 게시·후보·소스 세대·열린 cache는 해당 불변 기준본의 의존성이다. 제품 release/discard는 이들이 남으면 `SnapshotInUse`로 차단하며 연쇄 삭제하지 않는다. Editor 없는 worker도 제품 `workspace checkpoint`로 보존한다.

회수 순서는 앱/자식 종료 → 소유 브리지 제거 → 수집 전 flush/seal → 현재 source 검사·독립 결과 → 수집 후 seal 대조 → manifest 저장 → unmount/provider 종료 → seal 재대조 → 소유 등록·delta → 소유 기준본 참조 회수다. 미보존 변경·다른 의존성이 있으면 중단한다. 수집 뒤 seal 변경은 `DeltaChangedAfterCollection`로 보존하고 응답 유실은 같은 intent와 남은 inventory로 재개한다. 원본/peer/공유 기준본을 임의 삭제하지 않는다.

snapshot이 소실된 뒤에는 `stable-journal-v1`로 수집한 독립 결과가 있고 Git ref/객체와 **저널 전체 seal이 동일한 경우에만** 회수할 수 있다. 사용할 수 없는 원본 트리를 다시 읽거나 수정하지 않고 unmount 후 seal을 비교한다. 독립 결과가 없거나 수집 뒤 쓰기가 있으면 결과와 저널을 보존하고 실패한다. 열린 핸들이 남은 unmount는 `OverlayStopPending`이며 핸들을 닫은 뒤 같은 release를 재시도한다.

최종 종료의 `workspace.snapshot.result`는 `collectionProtocol=stable-journal-v1`을 요구하며 누락/미지원 값은 보존·거부한다. 중간 수집은 대신 불변 ID·source sequence·capture digest를 포함한 `results`를 게시하고 이후 편집을 허용한다. 수집 응답 유실은 고정 요청 키로만 복구하며 손상된 게시 기록을 조용히 재수집·이관하지 않는다. 전체 소스 검사·고정 tree/object 증명·최종 전후/unmount seal을 구분하며 [현재 결과 소유 경계](repeated-original-apply.md)를 따른다.

`workspace.snapshot.observation`은 snapshot/mount/provider/delta·소유권·핸들·working set/private bytes·host 여유·저널 잔여량을 관측한다. 누락 값은 unknown이며 0이 아니다. provider의 누적 저널 수치는 실제 NTFS 할당·전체 peak·회수 후 잔여 공간이 아니다. 공간 보고는 경로별 실제 allocation과 독립 결과·공유 기준본 비용을 구분한다.

`failureCode`에는 snapshot 소실·압박·provider 실패 또는 마지막 provider I/O 실패 코드가 표시된다. `DeltaFull`이면 기존 데이터는 남으며 현재 작업의 quota를 자동 확장하거나 데이터를 지우지 않는다. 성공한 release에는 앱/자식/provider와 소유 메모리·핸들 0, mount·delta·snapshot 부재를 재관측한 값이 남는다. `storage`의 누적 쓰기량은 마지막 실행의 이력이며 회수 뒤 남은 실제 공간을 뜻하지 않는다. released 조회는 마지막 회수 영수증을 반환한다. OS standby/cache RAM을 비우는 계약은 아니다.

| 오류 | 재개 행동 |
| --- | --- |
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

Core의 `ISnapshotWorkspaceBackend`와 `ISnapshotSourceWorkspaceProvider`는 Windows나 Git API를 노출하지 않는다. Runtime은 단계·잠금·결과/의존성 정책, Git 어댑터는 등록·index·결과 ref, Windows 어댑터는 불변 reader 조합·특수 파일 검사·WinFsp·저널 관측/회수를 맡는다. CLI가 이들을 조립하며 Host는 읽기 전용 in-process 계층을 유지한다.

NTFS 실패를 전체 checkout·파일 전체 copy-up으로 바꾸지 않는다. 자동 시험의 fake/Git/Windows 메타데이터 증거와 실제 mount/Unity 실행 근거는 NI 기록에서 구분한다. [NR-01~08 계획](plans/ntfs-recovery-and-support.md)의 공통 복구 응답과 상태 파일 보존은 부분 구현·자동 검증했고, 실제 재부팅·공간 장애·파일 지원/거부는 [NR 기록](verification/ntfs-recovery.md)의 미완료 조건이다. 특수 파일 지원 확대, 일반 에셋/패키지, 대형 프로젝트 성능과 공개 제품 전체는 이후 범위로 남긴다. 구매 에셋은 NI 및 다음 NR 시험에 필요하지 않다.


과거 VSS 읽기·삭제 순서의 근거는 [기술 결정](plans/vss-retirement.md)과 원래 검증 기록을 따른다.
