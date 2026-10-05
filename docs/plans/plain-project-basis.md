# VCS 없는 프로젝트 단위 기준본

2026-10-05 사용자가 미커밋 변경 커밋 뒤 VCS 없는 프로젝트의 작업을 요청했다. 이 단위는 기존 `source=none`의 VSS 수명주기 재검증이 아니라 **VSS 없이 프로젝트 기준을 고정하고 원본·A/B의 변경 범위만 저장하는 경로**다. 기존 Git/Plastic/일반 폴더 결과와 과거 검증을 보존한다. HTML은 만들지 않는다.

## 승인한 원본 접근 계약

기존 MCP/VSS 변경은 제품 `ae0dee3`, fixture `b09fa58`로 커밋했다. 현재 None 경로는 [기존 계약](../vcs-independent-workspaces.md)의 VSS snapshot에 의존한다. `.git`이 없다는 사실만으로 불변 원본 내용이 생기지 않으며 원본의 직접 쓰기를 현재 WinFsp 작업 mount가 관측하지 않는다.

[AGENTS.md](../../AGENTS.md)의 “원본 NTFS 위치·파일시스템 유지”와 “최초 전체 데이터 복제·읽기 hydration·파일 전체 copy-up fallback 금지”를 함께 유지해야 한다. [이전 Library 예외](git-library-reference.md)는 Library에만 적용된다. 사용자가 2026-10-05 **A: VCS 없는 원본 프로젝트도 관리형 mount 허용**을 승인했다. 아래 B는 구현하지 않는 대안이며 현재 제품 계약은 [project-snapshot](../plain-project-basis.md)이다.

| 선택 | 원본과 기준본 | 필요한 프로그램 작업 |
| --- | --- | --- |
| A: VCS 없는 원본 프로젝트도 관리형 mount 허용, 권장 | 원본 Editor를 정상 종료한 뒤 기존 데이터 트리를 같은 NTFS 볼륨에서 소유 기준 영역으로 rename한다. 원본의 기존 경로에는 writable WinFsp view를 제공한다. 원본·A/B가 불변 데이터를 공유하고 각 범위 저널에 쓴다. 원본 경로는 같지만 활성 경로의 파일시스템은 WinFsp로 바뀐다 | 기존 OwnedDirectory tree·range journal·managed restore를 재사용하는 프로젝트 수명주기. 원본 view의 내용과 마지막 NTFS 복원을 명시적으로 관리. Library도 같은 불변 기준/각 작업 private 범위로 연결 |
| B: 원본 소스의 직접 NTFS 접근을 유지 | 원본 쓰기·paging I/O·truncate·rename/delete 전에 필요한 이전 내용과 metadata를 보존한다 | 별도 minifilter 등 쓰기 선행 보존 계층의 설계·WDK/서명/개발 환경 확인부터 진행. 기존 사용자 모드 WinFsp에 이미 구현됐다고 표시하지 않으며 드라이버를 자동 설치하지 않음 |

A는 현재 Library 승인에서 원본 소스까지 예외를 확대하는 제품 결정이다. 데이터 전체 복사나 ReFS 이관을 뜻하지 않는다. B는 현재 NTFS 원본 계약을 유지하지만 새로운 kernel 경계가 생긴다. A를 선택하더라도 미지원 특수 파일·실제 OS 장애의 일반 자격을 제공하지 않는다.

## 검증 입력: 기존 Unity 파일 재사용

원본 `C:/Users/olive/Desktop/Development/splinter-unity`의 `.git`을 삭제하거나 이름을 바꾸지 않는다. 현재 Editor·브랜치·결과 refs와 공유 8080 서버를 보존한다. Unity Hub로 새 프로젝트를 생성하지 않고 기존 파일의 선택 집합으로 VCS 없는 시험 입력을 준비한다.

- 입력 revision: `b09fa58e9d40a560d4fe1af7a213f8ac23e8e3b1`. 실제 working 파일의 hash/길이를 `artifacts/plain-project-basis-20261005/input-review.json`에 고정했다.
- 선택: SampleScene와 대응 meta, Assets/Settings와 directory meta, 기존 Alpha A/B·SnapshotProbe와 meta, Packages manifest/lock, ProjectSettings. **51개·157,794 logical bytes**다.
- 제외: `.git`/다른 VCS 관리 정보, Library/Temp/Logs/Obj/UserSettings, 전체 Medieval village 팩. 작은 선택 입력의 자격이며 전체 원본 팩과의 동등성은 주장하지 않는다.
- 실행 경로: `T:/vcs-none-project-20261005`, 같은 볼륨의 별도 home. 선택 파일 준비·첫 import·원본/A/B를 실행했으며 VSS 요청은 없다. 실행 전에 T: 존재/NTFS·경로 미점유·VCS 조상/하위 부재·home 분리를 확인한다.
- 시험 폴더 준비의 한 번 파일 복사 비용은 별도로 기록한다. 제품의 초기 기준본/작업공간 payload 복사 0과 혼동하지 않는다. A/B 생성 시 Git/Plastic 접근·VSS 요청·전체 복사/read hydration이 없어야 한다.
- Unity는 기존 `6000.3.22f1`, MCP는 고정 Coplay 패치를 사용한다. 원본 fixture의 Library를 임의로 공유하거나 복제하지 않는다. 작은 입력을 최초 import한 뒤 생긴 Library를 해당 None 원본·A/B의 기준으로 사용한다. 새로운 Unity 버전·패키지 업그레이드·서버/전역 설정 변경은 포함하지 않는다.

## 확정한 구현 단위와 종료 기준

원본 view와 각 task의 source·Library 변경을 분리한다. Core에는 공통 기준 identity·불변 tree·의존성 계약만 두고 Windows에 rename/mount/restore를 둔다. 현재 제품 명령은 `project prepare/status/restore`와 `workspace ensure --storage project-snapshot --source none --project-basis <id>`다. 소유 계약/비용/자격은 [검증 문서](../verification/plain-project-basis.md)에 한 번만 기록한다.

| ID | 포함할 작업 | 종료 기준 |
| --- | --- | --- |
| NP-01 | 공통 프로젝트 기준·원본 view 계약과 fake 적합성 | 고정 id/owner/manifest·동일 요청·원본 수정 중 A/B 기준 유지·기준 손상/missing/owner 불일치의 보존 거부. 다른 VCS/기존 결과 guard 유지 |
| NP-02 | Windows prepare/status/restore 수명주기 | 정상 원본 Editor stop·같은 볼륨 rename·초기 payload 복사 0·원본 경로 writable view·reader/dependent guard. 중단 뒤 같은 요청 복구와 마지막 원본 범위/metadata 복원. 다른 작업/공유 자원 보존 |
| NP-03 | `source=none`의 프로젝트 기준본과 Runtime/CLI | VCS/VSS 없는 reader 선택·WinFsp/SJR2 연결·고정 request/digest·현재 상태/schema/capabilities/스킬 갱신. 기존 `ntfs-snapshot` 의미/기존 기록을 변경하거나 자동 이관하지 않음 |
| NP-04 | checkpoint·전체/선택 반영·release/discard | 독립 파일 결과·seal·기준/결과/현재 원본 비교와 고정 intent. 지원 application/대상·원본 Editor lock/owner와 조건부 게시 경계를 명시. A 회수 중 B 지속·다른 원본 변경/충돌 보호·독립 결과와 원본 복원 확인 |
| NP-05 | 작은 실제 None 원본/A/B 한 묶음 | 먼저 Editor 없는 실제 파일 I/O·동일 요청/범위 저장/정리 경계를 확인한다. 그 뒤 고정 Unity 입력의 원본·A/B를 exact MCP project/PID/start로 선택하고 리소스·씬·현재 작업 상태 테스트를 수행한다. A 전체/B 선택 반영 또는 충돌 보존·회수·최종 원본 복원을 확인. 긴 명령은 기존 job 방식이며 쓰기 timeout 자동 반복 없음 |
| NP-06 | 비용·회귀·문서와 마감 | 파일 할당/저널·이동한 기존 payload·시험 입력 준비·독립 결과 비용을 구분. 최초 전체 복사/read hydration/전체 파일 copy-up/VSS 호출 0. 필수 build·변경 계약/fake/실제 파일 시험·UTF-8/diff와 현재 문서/자격 표 갱신 |

각 workspace의 Library 쓰기도 private 범위로 저장한다. 이동한 기존 source/Library payload를 새로운 전체 복제 비용으로 합산하지 않는다. 초기 검증 hash 읽기의 I/O는 0이라고 주장하지 않는다. 실제 RAM 절감은 사용자가 이미 합격 조건에서 제외했으며 이 단위에서 재도입하지 않는다.

원본 변경·task 변경·checkpoint·원본 게시·최종 restore는 서로 다른 intent다. peer가 있거나 미완료 apply/restore가 있으면 기준 회수를 차단한다. release는 독립 결과를 보존하고 discard는 명시적 요청으로 수행한다. 같은 요청의 실패 자원과 증거를 보존하며 새 key/recapture/삭제로 실패를 숨기지 않는다.

## 제외와 증거 상태

NP의 현재 상태와 실패/재시도·미실행 경계는 [검증 추적](../verification/plain-project-basis.md)을 따른다. 승인한 A 계약으로 구현·파일/Unity 검증과 전체 회귀를 확인했으며 서비스 설치·VCS 등록·전역 MCP 설정을 변경하지 않았다. 검증 원본의 정상 종료 때 새로 관측한 외부 Coplay 공유 서버 종료 결함과 같은 버전/포트 복구는 NP 검증의 별도 제한으로 남긴다. 기존 M0/PS/ME/SP/GL/PPB/AP의 통과를 새로운 None reader의 자격으로 승계하지 않는다. 새 전체 장애 행렬·대형 팩·ARM·ReFS fallback·자동 Git init·VSS 재도입·전용 installer·Codex binding·게임 검증 API와 이전 Editor 종료 원인 조사는 포함하지 않는다.

기술 판단은 [VSS의 volume snapshot](https://learn.microsoft.com/en-us/windows/win32/vss/shadow-copies-and-shadow-copy-sets), [ReFS 전용 block cloning](https://learn.microsoft.com/en-us/windows/win32/fileio/block-cloning), [oplock의 충돌/break 계약](https://learn.microsoft.com/en-us/windows/win32/fileio/types-of-opportunistic-locks), [minifilter의 pre-operation 경계](https://learn.microsoft.com/en-us/windows-hardware/drivers/ifs/writing-preoperation-callback-routines)를 참고했다. 이 문서에서 A/B 경로의 제품 구성과 비용에 대한 설명은 현재 코드/요구를 대조한 설계 판단이며 해당 공식 문서가 Splinter 구현을 검증한 것은 아니다.
