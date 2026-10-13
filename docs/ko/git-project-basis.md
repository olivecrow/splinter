# Git 프로젝트 기준본

[English](../git-project-basis.md)

`--storage git-snapshot --source git`은 기록된 프로젝트 commit의 tree/blob을 기준본으로 사용한다. 원본은 기존 NTFS 위치에 남고 작업 중 수정하거나 branch를 진행할 수 있다. 생성·읽기·재개·정리·회수에 VSS나 저장소 권한 서비스를 호출하지 않는다. 기존 `ntfs-snapshot`은 VSS 경로이며 자동 변환하지 않는다.

```powershell
pwsh ./scripts/splinter.ps1 schema workspace.ensure
pwsh ./scripts/splinter.ps1 workspace ensure --project <Git-root> --key <task-key> --source git --application none --base development --storage git-snapshot --home <NTFS-home>
```

반환된 `data.workspace.path`에서 편집하며 project/key/id/home·storage·base/digest를 같은 요청의 재개에 유지한다. 첫 경로는 clean checkout HEAD와 요청 commit이 일치하는 repository root를 요구한다. `.git`이 원본 내부의 일반 디렉터리여야 하며 local objects·regular files만 사용한다. dirty/staged/untracked·skip-worktree/assume-unchanged·submodule/symlink·LFS/filter/working-tree-encoding·부분 clone/shallow/alternates는 거부한다. raw blob을 노출하므로 `core.autocrlf`는 false 또는 input만 허용하고 checkout 변환을 실행하지 않는다. 현재 폴더 전체의 시점 capture라는 의미로 사용하지 않는다.

## 소유권과 저장

Git commit/tree/blob의 stream 검증은 전체 작업의 30초 제한 대신 각 blocking read의 30초 무진행 제한을 적용한다. 부분 읽기가 계속 완료되면 전체 검증 시간이 30초를 넘어도 진행하며 hashing/idle 시간에는 타이머를 적용하지 않는다. 무진행이면 그 stream의 정확한 소유 reader만 종료하고 `GitBasisReadTimeout`으로 기준·저널을 보존한다. 실패 시 요청 buffer 미게시·proof 폐기·고정 기준 보호는 유지하며 쓰기를 자동 반복하지 않는다. metadata 명령 자체의 기존 제한과 8GiB blob/proof 상한은 유지한다.

`GitObjectSnapshotProvider`는 정확한 commit/tree와 owner를 `git-basis.json`에 고정하고 `refs/splinter/git-bases/<id>`를 compare-and-create한다. intent 게시 뒤 effect/응답 유실은 같은 요청으로 조정한다. 완성한 ref의 소실/이동·symbolic ref·손상 영수증은 보존 후 거부하며 새 기준을 만들지 않는다. 첫 기준 선택 뒤 mutable source·index·branch를 다시 기준으로 읽지 않는다.

`GitObjectSnapshotTree`는 fixed tree의 metadata와 raw blob을 `git cat-file`로 읽는다. commit/root tree의 object hash와 한 batch stream에서 읽은 모든 subtree hash를 확인한다. blob의 첫 읽기는 전체 object hash를 검증하면서 64KiB 단위 SHA-256 proof를 만든 뒤 요청분만 반환한다. 후속 읽기는 proof에 맞는 chunk만 요청분 staging에 게시한다. tree별 LRU가 정확한 object ID와 고정 길이를 키로 최대 4,096개 blob의 SHA-256 chunk proof를 합계 4MiB 안에 유지한다. 검증 중인 proof도 같은 예산에 포함해 할당 전에 퇴출하고, 전체 blob/hash/framing 검증이 끝나야 새 proof를 게시한다.

별도의 tree별 RAM LRU가 **최대 4,096개 chunk의 payload를 합계 16MiB**까지 유지한다. 첫 요청에서 유용한 chunk를 포함해 요청 범위와 겹치는 완전한 chunk를 보관하며, 해독한 앞부분 전체나 blob 전체를 캐시용으로 복사하지 않는다. 첫 proof를 만드는 동안 보관한 chunk도 즉시 같은 한도에 포함하며 전체 proof 게시 전에는 재사용할 수 없다. proof가 게시되지 못하면 해당 chunk를 제거한다. cache hit는 고정 object identity/길이를 확인하고 보관된 bytes의 hash를 chunk proof와 대조한 뒤 임대한 reader buffer로 복사한다. 할당 전에 퇴출하며, reader 세션이 다른 object로 재배정된 뒤에도 proof가 유효하면 캐시 payload를 재사용할 수 있다.

object별 reader 세션 두 개는 기존대로 decoder 위치와 검증된 64KiB payload chunk 두 개씩을 유지한다(관리 reader buffer 합계 256KiB). 교차 순방향 읽기는 해당 decoder를 이어 쓰고, reader buffer나 RAM LRU에 남은 범위의 역방향 읽기는 해독하지 않는다. 두 곳에 없는 범위는 stream을 다시 시작해 앞부분을 재검증할 수 있다. reader 세션 LRU 퇴출은 부분 decoder만 취소하며, 끝까지 소비한 batch 프로세스는 다른 object에 재사용한다. proof 퇴출은 해당 object의 캐시 payload와 세션도 회수하며 오류·close는 모든 세션·proof·캐시 payload를 버린다. 소유 Git reader 프로세스는 최대 두 개이며, 동시 입장한 요청 두 개 각각의 staging은 최대 1MiB이고 blob 상한은 8GiB다. cache hit에도 기준본 health·실패한 peer로 인한 게시 차단·close/drain 검사를 적용한다.

**16MiB cache·256KiB reader buffer·4MiB proof·요청 staging 두 개는 서로 별도의 tree별 한도**이며 전체 프로세스 RAM 상한이 아니다. cache metadata·고정 tree 목록·packed object의 Git 내부 메모리·OS/프로세스 비용은 추가다. payload를 disk cache/checkout하지 않으며, `readCacheBytes = 0`은 디스크 read hydration이 없다는 뜻이지 RAM 캐시가 없다는 뜻이 아니다. 이미 검증해 보관한 bytes는 이후 object store가 변해도 고정 내용 그대로이며, hit는 object store의 현재 손상을 새로 감사하지 않는다. 캐시에 없는 읽기는 실제로 읽은 bytes를 고정 proof와 대조하고 불일치 시 요청분 전체를 게시하지 않고 거부한다. 빈/EOF 요청은 0바이트를 반환한다. Windows 경로의 case 충돌·ADS/예약 이름·특수 entry는 거부한다. Git 밖의 mutable 파일을 fallback으로 읽지 않는다. 한정 cache 검증과 계측은 [현재 시간/RAM 최적화 검증](verification/time-memory-optimization.md)을 따르며, [이전 reader 세션 근거](verification/lifecycle-read-optimization.md)는 기록 당시 후보와 한계를 유지한다.

서로 다른 blob은 두 독립 임대에서 decoding/hash를 수행하며 같은 blob 별칭은 직렬화한다. 공통 gate는 입장·LRU·최종 게시만 보호하고 진행 중 proof 예약도 예산에 포함한다. 오류는 다른 진행 중 읽기의 후속 게시를 막고 모두 drain한 뒤 reset하며, 동시 Dispose는 실제 종료를 기다린다. 정확한 blob header를 한정 span으로 읽고 missing/짧은 header를 즉시 거부한다. Library에 고정한 [내용 검증 소스 시각](git-library-reference.md)은 같은 경로/blob/길이에만 적용하며 원본을 다시 읽지 않는다. [SU-01–05 검증](verification/startup-profile-optimization.md)을 따른다.

부분 reader를 퇴출·재시작하거나 close할 때 정확한 소유 stdout 파이프를 먼저 닫아 unread suffix의 쓰기 대기를 취소한다. 이어 stdin을 닫고 기존 1초 종료 한도/소유 자식 종료와 stderr 관측을 유지한다. 요청 bytes는 이미 전체 blob 또는 chunk proof로 검증한 뒤에만 반환하며 취소한 suffix를 읽기 성공으로 판정하지 않는다. 이 시간 최적화는 [별도 다수 파일 비교](plans/journal-base-reversion-optimization.md)의 범위이며 이전 실제 Unity/WinFsp 자격을 자동 승계하지 않는다.

WinFsp와 기존 overlay v2/SJR2 범위 저널을 사용한다. 최초 기준 payload 복제·read hydration·파일 전체 copy-up 없이 private 변경 범위만 저장한다. Git linked worktree는 기존 no-checkout 등록/index 계약을 사용하며 `.git` pointer는 private 저널에 쓰는 관리 데이터다. 원본 index/branch와 peer 등록은 수정하지 않는다. baseline reachability ref는 결과 branch와 별도다.

## 결과와 회수

기존 checkpoint·source guard·전후/unmount seal·독립 결과·통합 target/dirty guard를 유지한다. release는 exact 독립 결과를 확인하고 소유 application/provider/handle/mount/등록/delta가 없어진 뒤 기준 ref를 compare-and-delete한다. reader fence가 열려 있거나 ref가 변했으면 삭제하지 않는다. 명시적 discard도 같은 identity/intent·자원 0 경계를 거친다. 회수 응답이 유실되면 같은 release/discard intent만 이어간다. 원본/peer/독립 결과 refs·공유 객체는 보존하며 Git GC를 실행하지 않는다.

## 입력과 검증 한계

ignored/private 파일과 Unity Library는 commit 기준본에 포함되지 않는다. 생성 데이터는 workspace 안에서 새 private 데이터로 생성되며, Library의 private 쓰기에도 같은 범위 저널·압축이 적용된다. 원본 Library는 [명시적 별도 기준](git-library-reference.md)의 `library prepare`와 `workspace ensure --library-basis <id>`로 재사용한다. 원본 Library 전환/복원에는 정상 Editor 종료와 사용자 허용이 필요하다. 실제 Unity/HTTP MCP·리소스/씬·현재 상태 테스트의 한정 실행과 cold 비용은 [Git Unity 검증](verification/git-unity-validation.md)을 따른다. warm 경로의 실제 준비·MCP·RAM 비용은 [별도 검증](verification/git-library-reference.md)에서 판정한다. Plastic과 VCS 없는 프로젝트는 각각 [Plastic 기준본](plastic-project-basis.md)과 [관리형 프로젝트 기준본](plain-project-basis.md)의 별도 reader·수명 계약을 사용하며 Git 자격을 승계하지 않는다. 이 경로는 다른 VSS 작업의 snapshot을 회수하지 않으며 개발용 VSS view/cache를 Git 기준본의 지원 경로로 사용하지 않는다.

현재 계약은 `workspace.ensure`·`overlay-serve-request`·`git-project-basis-receipt`와 응답 schema, Core snapshot ports와 `WorkspaceStorage.HasRangeJournal`, Windows `WorkspaceSnapshotBasis` 조합으로 연결한다. runner에는 Git DLL이 추가되어 현재 8개 파일의 내용 키를 사용하며 이전 bundle은 유지한다. [검증](verification/git-project-basis.md)은 자동/fake 경계와 실제 Editor 없는 A/B를 구분한다.
