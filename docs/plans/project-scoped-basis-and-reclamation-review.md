# VCS 프로젝트 단위 기준본 전환 계획

사용자 확정 범위: 프로젝트 하나의 작업에 볼륨 전체 VSS를 만드는 단위 불일치를 해결한다. 우선 Git·Plastic에 이미 기록된 프로젝트 버전을 기준본으로 재사용하고, VCS 없는 프로젝트의 새 기준본 방식은 후순위로 둔다. 프로젝트 밖 변경의 비용이나 기존 snapshot 회수 문제는 이 전환의 동기가 될 수 있으나 이번 첫 문제의 정의를 대신하지 않는다.

원본 NTFS 위치·파일시스템 유지, 최초 전체 복제·읽기 hydration·파일 전체 copy-up fallback 금지, 작업별 범위 저장, 원본 계속 편집 가능이라는 [제품 요구](../../plan.html)를 유지한다. 기존 `source=none` 기능과 이전 VSS 작업·독립 결과·pending 영수증을 삭제하거나 자동 이관하지 않는다. PS/ME/SP/M0의 기존 한정 마감을 새 backend의 통과로 승계하지 않는다.

## 최초 전체 복제에 대한 정정

최초 전체 복제는 snapshot의 필수 조건이 아니다. 변경되지 않은 데이터는 기존 저장소를 참조하고, 원본을 변경하기 **전**에 기준에 필요한 이전 범위를 보존하면 copy-on-write 기준본을 구성할 수 있다. 덮어쓴 후 새 파일을 복사하거나 파일 감시 알림만 처리하면 이미 사라진 이전 바이트를 보호할 수 없다. 작은 원본 변경 때문에 파일 전체를 복사하는 방식은 현재 범위 저장 요구와 구분한다.

이번 VCS 경로에서는 기존 commit/changeset이 이전 내용을 이미 소유하므로 원본 쓰기 가로채기를 첫 구현의 필수 조건으로 삼지 않는다. 먼저 정확한 불변 VCS 객체/revision을 참조한다. 살아 있는 원본 파일을 읽는 최적화는 동일 버전임을 읽기 중에도 입증할 수 있을 때만 별도로 검토하며, mtime/길이만으로 판정하거나 mutable source를 기준본 fallback으로 쓰지 않는다. VCS 없는 원본의 선행 범위 보존·kernel filter·초기 seed 비용은 후순위이며 이번 전환을 막는 필수 연구로 넣지 않는다.

## 이번 구조와 입력 경계

작업공간은 **고정한 프로젝트 버전 + 작업별 범위 저널 + 별도로 관리하는 생성 캐시**로 구성한다. Git의 commit/tree/blob, Plastic의 changeset/file revision은 어댑터에 두고 Core에는 opaque 기준 식별과 불변 tree의 계약만 둔다.

| 경계 | 구현 목표 |
| --- | --- |
| 프로젝트 범위 | 프로젝트 root/필요한 subtree의 파일 목록·불변 내용 ID만 고정한다. C: 같은 볼륨 snapshot이나 다른 프로젝트의 내용 capture를 요청하지 않는다 |
| Git | 이미 로컬에 있는 tree/blob을 공유해 읽고 소유 기준 참조를 보존한다. 전체 checkout·clone·read hydration 없이 범위 저널에 연결한다. branch 이동·원본 파일 수정이 고정 기준을 바꾸지 않아야 한다 |
| Plastic | 명시적 저장소·고정 changeset·각 revision을 참조한다. 등록된 새 checkout/worker VCS 관리 정보를 만들지 않고 내용은 일반 사용자 권한으로 읽는다. 서버 전송·CLI 내부 cache·임시 저장의 비용/부작용을 별도로 확인한다 |
| 미커밋/미등록 소스 | 기록된 기준본에 없는 필요한 소스나 일치하지 않는 원본 내용을 조용히 누락하지 않는다. 첫 한정 경로에서 거부하거나 기록된 버전으로 시작한다는 명시적 입력 의미를 제공한다. 자동 commit/check-in/update는 하지 않는다 |
| 생성 데이터 | Unity Library 등 VCS 밖 데이터는 commit에 있다고 가정하지 않는다. 새 경로의 private 생성 범위와 사용 비용을 분리하고, 원본 Library를 mutable 공유하거나 VSS로 몰래 캡처하지 않는다. 기존 warm Library 재사용과 비용 동등성은 새 경로의 별도 자격이다 |
| 기존 수명주기 | 같은 project/key/id/home·기준/digest·owner, 개별 durable flush, seal, 독립 결과, 원본/peer/target/dirty guard와 명시적 회수/폐기를 유지한다 |

기록된 버전의 선택과 현재 폴더 전체의 시점 캡처는 다른 입력 의미다. commit/changeset만으로 ignored/private 파일까지 보존됐다고 표시하지 않는다. Git 변환/CRLF·LFS/filter·submodule/특수 파일과 Plastic 변환/부분 workspace의 미지원 거부를 유지하며 새 방식에서 필요한 자격을 별도로 확인한다. 결과를 원본/VCS 없이 독립 보존할 때 필요한 payload 저장은 생성/읽기 비용과 구분한다.

현재 제품 CLI의 `ntfs-snapshot`은 VSS 기반이다. Git 전용 `git-snapshot`은 [현재 계약](../git-project-basis.md)으로 연결했고 [원본 Library 참조](../git-library-reference.md)와 실제 Unity/MCP도 한정 마감했다. Plastic 전용 `plastic-snapshot`도 [현재 계약](../plastic-project-basis.md)의 fixed changeset/blob reader와 공통 Library에 연결했다. 현재 자동·실제 자격은 [PPB 검증](../verification/plastic-project-basis.md)이 소유한다. 실제 RAM 절감은 보장하지 않으며 사용자가 Plastic 단계의 합격 조건에서 제외했다. 기존 명령의 기준본이나 과거 VSS 상태를 바꾸지 않는다.

## 구현 순서와 종료 기준

2026-10-04 후속 승인: 우선 Git 경로를 구현한다. 이번 실행 질문은 `git-snapshot`의 기록된 commit 읽기·원본/branch 변경 중 기준 유지·범위 저널·동일 요청 재개·소유 ref 보호·독립 결과·회수다. Plastic reader와 VCS 없는 전환, Unity Editor/Library 재사용·MCP·전체 native 장애 행렬은 이 구현의 자격으로 확대하지 않는다.

고정 실제 입력은 새 소유 `.local/gb1004/source`의 regular Git 저장소다. 1MiB binary·작은 text/ignore 파일과 ignored Library 표본을 만들고 `core.autocrlf=false`로 기록한다. 같은 `.local/gb1004/home`에서 Editor 없는 A/B 한 묶음만 생성한다. 원본 변경/후속 commit, A의 4KiB 변경·재개·명시적 정리·독립 결과/release 중 B의 읽기/쓰기 지속, B의 독립 checkpoint 뒤 명시적 discard를 확인한다. 자기 fixture의 Git refs/등록·journal/mount/소유 provider만 회수하며 원본 fixture·독립 결과·metadata 영수증과 evidence는 보존한다. 설치·grant·볼륨 설정·과거 VSS 회수는 실행하지 않는다. 실패하면 같은 key/id/home을 보존하고 원인을 고쳐 재개한다. 종료는 소유 ref/등록/delta/mount/provider 부재와 두 독립 결과 확인이며 전체 물리 peak·Unity RAM 절감은 판정하지 않는다.

| ID | 포함할 단위 | 종료 기준 | 현재 상태 |
| --- | --- | --- | --- |
| VP-01 | 공통 기준본 계약·fake 적합성 | 프로젝트 범위·불변 identity·bounded read·소유 기준 참조·missing/unknown 거부·같은 요청 복구를 정의. 원본 수정/기준 이동/손상·다른 owner에 대한 보호 확인 | Git 공통 계약/fake·보호 자동 시험 Passed |
| VP-02 | Git 기준본 reader·소유 참조 | 정확한 project tree/blob만 사용. 전체 checkout/초기 payload 복제·읽기 disk cache 없음, 원본/branch 변경 뒤 동일 바이트, 객체 소실 보존 거부. 원본 index/branch·peer/결과 refs 보호 | Git reader/소유 ref 연결 및 작은 Editor 없는 A/B 확인. 후속 Unity/Library는 GL 검증으로 한정 마감, 대형/일반 RAM 미자격 유지 |
| VP-03 | Plastic 기준본 reader·고정 identity | 정확한 changeset/revision의 목록·내용 읽기. 공급자는 새 Plastic 등록/check-in 없음. 필요한 소스의 포함 여부·내용 변환·서버/cache 비용·실패/응답 유실을 확인하고 원본/peer를 보호 | fixed reader·전체 cs 2 길이/hash 확인·자동 보호 시험 통과. 승인된 정확한 입력 check-in과 제품의 읽기 전용 계약을 구분하며 PPB 검증을 따름 |
| VP-04 | Windows overlay·Runtime/CLI 연결 | WinFsp·범위 저널을 공통 불변 reader와 연결. 생성/재개/읽기/정리/수집/회수에 새 VSS 호출 0. 현재 input/output schema·fake·capabilities·기능/AI 문서를 함께 갱신 | Git과 Plastic 연결·schema/fake/필수 회귀 완료. Plastic 실제 수명주기는 PPB 검증의 상태를 따름 |
| VP-05 | 한정 실제 입력·A/B와 비용 판정 | VCS별 필요한 작은 Editor 없는 한 A/B에서 원본 변경 중 기준 유지, 변경량 저장, 같은 요청 재개, A 종료 중 B 지속, 독립 결과/전용 자원 회수 확인. 파일 할당·전송/cache·소유 메모리·잔여 shared 데이터 분리 | Git A/B와 별도 PPB Plastic Unity A/B의 기준/Library 격리·재개·peer 지속·독립 결과/회수·원본 복원/비용 확인. MCP 응답·release/test 실행 겹침 제한과 외부 cache/전체 peak unknown 유지 |

Git reader부터 연결한 뒤 Plastic reader를 같은 계약에 연결한다. 실제 workspace 실행은 입력과 질문·종료 기준을 고정한 해당 단위에서 수행한다. Unity Editor·Library 재사용·MCP·Play/Test/build 전체를 VP-05에 자동 포함하지 않는다. 일반 VCS 없는 폴더의 전환, 원본 쓰기 kernel driver, 새 installer/service/grant/볼륨 설정, 서버 이력 변경, 전체 장애/ARM/대형 행렬은 이번 범위에서 제외한다.

Git의 VP-01/02·VP-04/05 부분은 [전용 검증](../verification/git-project-basis.md)의 자동 919 Passed/1 선택적 NotRun과 고정 실제 Editor 없는 A/B로 scoped-complete-with-limitations 마감했다. 이후 Git Unity/Library는 [GL 검증](../verification/git-library-reference.md)에서 별도 한정 마감했다. Plastic VP-03과 해당 연결/실제 입력도 [PPB 범위](plastic-project-basis.md)의 자동 961 Passed/1 선택적 skip과 고정 Unity A/B로 [한정 마감](../verification/plastic-project-basis.md)했다. 대형/전체 peak 자격과 VCS 없는 전환은 유지하며 RAM 절감은 Plastic 단계의 합격 조건이 아니다. 새 질문 없이 같은 A/B나 전체 장애 행렬을 자동 반복하지 않는다.

## 고정 VCS 내용의 사전 확인

2026-10-04의 [읽기 전용 probe](../../artifacts/vcs-project-basis-20261004/probe.py)와 [기록](../../artifacts/vcs-project-basis-20261004/basis-probe.json)은 전체 checkout·새 VSS·원본 쓰기 없이 기존 VCS에 프로젝트 버전의 목록과 내용을 얻을 수 있는지 확인했다. 내용 표본은 각각 `ProjectSettings/ProjectVersion.txt` 85B를 스트림으로 읽고 hash만 보존했다. manifest JSON에는 식별자/파일 목록을 저장했으며 파일 payload는 저장하지 않았다. CLI 내부 cache 쓰기는 계측하지 않았다. 생성/읽기의 제품 hydration 0이나 provider/Unity 지원을 증명한 시험은 아니다.

| 입력 | 고정 기준 | 목록 | 내용 표본/제한 |
| --- | --- | --- | --- |
| Git `splinter-unity` | commit `6a54974a9c655031aeb2be17fb7622fc5d641c52` | 1,602파일·논리 2,804,769,314B | `cat-file`로 고정 blob의 85B 읽기. 내용 전체를 export/checkout하지 않음 |
| Plastic 기존 시험 저장소 | changeset 1 | 26파일·논리 70,004B | `cm ls --tree`·`cm cat`로 읽기. **Assets가 기록돼 있지 않아 현재 Unity 폴더 전체와 동등한 기준으로 사용할 수 없음** |

사전 확인 중 PowerShell의 미인용 Git revision 표현은 quote 후 다시 조회했고, 설치된 Plastic 11.0.16.9915의 `ls` format에서 `{tab}`이 거부돼 같은 고정 버전의 XML 출력으로 확인했다. 제한 실행의 `getworkspacefrompath` 접근 실패도 있었으며 일반 사용자 권한의 서버 버전 읽기로 전환했다. workspace 등록/파일 권한을 변경하지 않았다. probe는 read-only 명령만 호출하며 이 명령의 timeout만으로 변경 요청을 반복하지 않는다.

현재 Plastic 시험 입력의 private Assets를 자동 check-in하거나 별도 전체 복제해 조건을 맞추지 않는다. 첫 실제 Plastic 실행에는 필요한 소스가 저장된 명시적 changeset을 선택하고 현재 원본·기준의 차이를 먼저 확인해야 한다. 이 목록/내용 접근의 성공을 실제 A/B 자격으로 확대하지 않는다.

공식 근거는 [Git 객체](https://git-scm.com/book/en/v2/Git-Internals-Git-Objects)·[cat-file](https://git-scm.com/docs/git-cat-file), [Plastic list](https://docs.unity.com/en-us/unity-version-control/uvcs-cli/list)·[getfile](https://docs.unity.com/en-us/unity-version-control/uvcs-cli/getfile), [Unity Asset Database](https://docs.unity3d.com/6000.0/Documentation/Manual/asset-database-contents.html)다. 커밋/changeset의 내용 접근 가능성과 Splinter adapter의 구현/비용 자격을 구분한다.

## 별도 보존하는 두 번째 문제

사용자가 이번에는 첫 번째 문제만 선택했으므로 기존 snapshot의 독립 회수 제약은 자동으로 다시 실행하지 않는다. [회수 재현/보호](../verification/storage-recovery.md)·[기존 C: Plastic pending](../verification/plastic-compatibility.md)이 소유 근거다. 새 VCS 기준본을 구현해도 과거 VSS가 자동으로 안전해지거나 회수된 것으로 표시하지 않는다. 현재 격리 guard, 같은 id/home/intent 재시도, 비소유/다른 작업 snapshot 보호를 유지한다.
