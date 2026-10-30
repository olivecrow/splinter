# VCS 없는 NTFS 작업공간

현재 task 상태는 **v3**, 관리형 basis는 v2다. [반복 원본 반영](repeated-original-apply.md)이 실행 중 수집·불변 결과 버전·경로별 반영 이력·실행 중 원본의 정확한 외부 협력을 소유한다. [RI 자격](verification/repeated-original-apply.md)은 과거 종료형 Editor 근거와 구분하며 이전 task는 이관 없이 보존·거부한다.

[English](../vcs-independent-workspaces.md)

현재 `--source none`은 [프로젝트 기준본](plain-project-basis.md)의 `project prepare` → `workspace ensure --storage project-snapshot --source none --project-basis <id>`다. 승인한 원본 관리형 view·같은 NTFS 볼륨 이동·source/Library 불변 공유와 private 저널을 사용한다. application none/unity의 전체·선택 원본 게시와 task 참조 회수 뒤 명시적 NTFS 복원을 연결한다. [NP 검증](verification/plain-project-basis.md)과 [VSS 폐기 결정](plans/vss-retirement.md)을 따른다. 구형 상태 자동 이관과 소스 fallback은 없다.

병렬 격리·범위 저장·기본 수명주기는 Splinter가 담당한다. 제품 목표는 외부 VCS 유무와 무관하게 이를 제공하고, VCS를 사용할 때도 관리 정보·등록·잠금·공유 기준과 추가 저장/메모리 비용이 작업공간의 격리·효율·원본/peer 보호를 해치지 않도록 처리하는 것이다. source 공급자는 이 호환 경계와 기준/결과 보존을 담당한다.

제품 CLI는 `--source git|none|plastic`으로 소스 공급자를 선택한다. 생략하면 기존 Git 경로다. Git 오류·손상·unknown을 일반 폴더로 해석하지 않는다. `none`은 VCS 관리 정보가 없는 일반 폴더의 NTFS task만 허용한다. 원본과 조상/하위 경로의 `.git`·`.plastic`·`.hg`·`.svn`, 지원하지 않는 파일 유형은 보존 후 거부한다. Plastic 사용 환경의 관리 정보/등록 격리는 [별도 계약](plastic-compatibility.md)을 따른다. 실제 지원 상태는 [검증 기록](verification/vcs-independent-workspaces.md)이 소유한다.

## 입력과 같은 작업 재개

```powershell
pwsh ./scripts/splinter.ps1 schema workspace.ensure
pwsh ./scripts/splinter.ps1 doctor --project <준비한 일반 폴더> --source none
pwsh ./scripts/splinter.ps1 workspace ensure --project <준비한 일반 폴더> --key <고정 key> --source none --storage project-snapshot --project-basis <prepared-id> --home <home>
```

`none`의 기본 basis는 `snapshot`, application은 `none`이다. Unity 파일·Editor·라이선스 없이 생성/편집/재개/결과 보존/회수/폐기를 요청할 수 있다. Unity 소스를 사용할 때는 최초 요청에 `--application unity`를 고정하고 필요한 시점에만 `--editor` 또는 `unity start`를 요청한다. application `none`의 모든 경로는 일반 데이터이며 `Library`·IDE 파일을 임의로 제외하지 않는다.

source/application·basis·storage·budget을 request digest와 v3 task 상태에 고정하고 같은 project/key/id/home으로 재개한다. 원본·작업 영수증과 구형 상태는 보존한다. 서비스·grant는 필요하지 않으며 이전 버전이나 VSS 상태는 자동 이관하지 않는다.

## 기준·검사·결과

생성은 소스 메타데이터를 검사하고 관리형 프로젝트 기준본·WinFsp·범위 저널을 사용한다. 전체 payload 복제·읽기 hydration·파일 전체 copy-up fallback은 없다. `Splinter.SourceControl.Files`는 검증된 불변 snapshot 경로에서 일반 파일/디렉터리의 경로·유형·길이·SHA-256·mtime·속성을 읽어 기준 manifest를 고정한다. 이후 원본 변경이나 재연결은 기준을 재계산하지 않는다. 공통 revision 필드는 공급자의 opaque identity이며 `none`에서는 manifest의 `sha256:` identity다. Git branch/commit을 요구하거나 내부 `git init`을 실행하지 않는다.

현재 일반 폴더의 최초 기준/명시적 내용 검사는 전체 읽기다. mtime만으로 cache를 신뢰하지 않는다. manifest는 최대 32,768개 항목·경로 UTF-8 합계 1 MiB·JSON 16 MiB이며 더 큰 입력은 거부한다. 일반 status는 기존처럼 자원만 관측한다.

`workspace checkpoint --id <id> --request-key <key> --message <설명>`는 task/Editor를 유지하며 불변 소스 결과 하나를 보존한다. control에 `files/generation-basis.json`·`files/generations/<결과-id>/result.json`·결과 영수증·내용 기반 `objects/<sha256>`를 남긴다. 전체 독립 basis/result payload는 명시적 수집에서만 보존하고 같은 task의 모든 버전에서 내용을 재사용한다. `resultStorage`는 기준/최신 결과 논리 바이트와 보존 고유 payload 바이트/개수를 구분하며 물리 할당·peak와 별개다. 같은 키는 최초 결과를 검증·반환한다. 영속 metadata/범위 pin이 mutation 경계 밖의 고정 읽기를 보호하며 최종 종료/unmount는 전체 seal을 유지한다. [현재 결과 계약](repeated-original-apply.md)을 따른다.

payload 보존의 각 단계는 고유 hash 객체만 처리한다. 독립 결과를 검사하는 한 호출 안에서는 기준/결과 manifest의 객체 집합을 합쳐 동일 객체를 한 번 hash한다. 동일 hash의 선언 길이가 다르면 객체 읽기 전에 거부한다. 게시 전·게시 후 및 이후 회수 검사는 각각 다시 검증하며 이전 성공을 신뢰하는 지속 cache를 두지 않는다. manifest의 모든 경로/메타데이터·identity/owner·collection 검사는 유지한다. [최적화 회귀](verification/code-quality-optimization.md)는 공유 객체의 읽기 수와 이후 손상 거부를 구분한다.

파일 결과 수집은 mutation 경계 밖에서 고정 소스 manifest 하나를 scan하며 최종 종료는 전체 전후 seal을 유지한다. 로컬 전체 payload 보존과 독립 object 검증은 최대 8 worker로 처리하며 실패·취소에도 반환 전에 drain한다. 기존·pending·이후 호출의 object는 다시 증명하고 object별 영속 flush 및 모든 결과/소유 검사를 유지한다. 동일 입력 Windows 실측과 한계는 [RT-01–04](verification/result-collection-throughput.md)를 따른다.

전체 파일 scan은 제한된 목록을 먼저 열거하고 None은 최대 8 worker로 hash하며 실패/취소에도 drain한다. Plastic mount scan은 reader가 revision 증명/stream 하나를 유지하므로 직렬로 남긴다. 같은 최초 editorless ensure 안에서는 최초 scan과 metadata/목록 재검사 각각의 전후에 완전한 provider 세대를 검증한 경우만 전체 검사를 한 번 재사용한다. owner·basis·mount·policy·identity·Plastic 등록 격리는 계속 검사하며 tracker 없음/audit는 전체 scan을 사용한다. 1회성 증명은 영속화하지 않고 Editor 시작/provider 재시작 뒤 재사용하지 않는다. 준비·관측 최적화와 별도 시간 한계는 [PR-01–04](verification/preparation-throughput.md)가 소유한다.

## 반영·회수·폐기

`workspace integrate --target original --result <id> --plan`은 고정 checkpoint·기준/결과·현재 원본을 비교한다. `project-snapshot`은 application none/unity의 전체·선택 반영을 지원하며 원본 mount identity·정확한 실행 중 Editor 협력 또는 정지 reservation·명시적 intent/digest를 요구한다. [원본 반영](plain-folder-original-apply.md)과 [프로젝트 기준본](plain-project-basis.md)의 보호 및 한정 자격을 따른다.

Release는 실제 독립 payload·seal 검증, 정상 종료, 소유 자원 0, unmount 후 seal, delta 부재와 기준본 의존성 보호를 유지한다. unknown은 0이나 완료가 아니다. 명시적 discard는 같은 intent·seal과 완료된 파일 소스 영수증으로 자기 자원만 회수하며 이전 독립 결과·원본·peer를 보존한다.
