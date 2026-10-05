# Plastic 프로젝트 기준본

`--source plastic --storage plastic-snapshot --base <changeset@repository@server>`는 기록된 프로젝트 버전을 WinFsp 범위 저널에 연결한다. VSS·새 checkout/Plastic 등록·최초 payload 복제·읽기 disk hydration을 사용하지 않는다. 원본은 기존 NTFS 위치에서 계속 편집할 수 있다. 기존 `ntfs-snapshot`은 캡처 시점의 파일 기준이며 이 별도 경로로 자동 이관하지 않는다.

## 입력과 관리 경계

잘못된 고정 revision 입력은 `PlasticBasisRequestInvalid`/`correct-request`다. 이미 고정한 목록·receipt·revision 내용의 손상이나 변경은 `PlasticBasisInvalid`로 구분하며 `requires-user-action`·workspace `preserve`/`workspace.status`를 반환한다. 동일 task/basis·저널을 보존하고 새 기준 선택이나 다른 VCS로 우회하지 않는다.

원본은 정확한 regular/static Plastic workspace root다. 명시한 원본 local root의 controlled-directory XML과 지정한 changeset 목록의 canonical repository/server·repository ID를 대조한다. 명시적 numeric changeset·root revision/GUID·파일 revision/길이/hash·목록 digest와 owner를 `snapshot/plastic-basis.json`에 고정한다. branch 최신값·mutable 원본·다른 VCS로 fallback하지 않는다. working tree의 dirty/private 파일은 기록된 기준에 포함된 것으로 표시하지 않는다. Unity 입력에는 기록된 Assets·Packages/manifest.json·ProjectSettings/ProjectVersion.txt가 있어야 한다.

```powershell
pwsh ./scripts/splinter.ps1 schema workspace.ensure
pwsh ./scripts/splinter.ps1 workspace ensure --project <original> --key <stable-key> --source plastic --application unity --storage plastic-snapshot --base <changeset@repository@server> --home <same-volume-short-home> --library-basis <prepared-id>
```

home은 모든 VCS 관리/Plastic 등록을 상속하거나 겹치지 않는다. VSS service/grant는 이 기록된 기준 경로의 전제가 아니다. 설치된 WinFsp·PowerShell 7과 일반 사용자 `cm`의 저장소 읽기 접근이 필요하다. 내용 reader는 `%ProgramFiles%/PlasticSCM5/client`의 검증된 `11.0.16.9915` 라이브러리를 사용하며 다른 버전은 `PlasticBasisUnsupported`로 보존·거부한다. 원본 `.plastic` 등 관리 root를 worker에서 제외하고 쓰기·rename도 차단한다. worker는 등록하지 않는 파일 작업공간이며 Plastic update/check-in/merge·인증/서버 설정을 제공하지 않는다. 전체/선택 로컬 반영은 [별도 계획/intent 계약](plastic-original-apply.md)을 따른다.

기록된 tree의 모든 깊이에서 VCS 관리 entry를 거부한다. 실행 중 도구가 없는 nested `.git`/`.plastic` 경로를 조회하는 것은 ordinary not-found이며, 이 조회를 허용해 관리 entry의 기록이나 worker의 최상위 관리 root 쓰기를 허용하지 않는다.

## 읽기와 비용

고정 목록은 `cm ls --tree` XML이다. 내용은 tree가 소유한 일반 사용자 PowerShell 자식의 private stdin/stdout으로 읽는다. `PlasticRevisionBridge`는 고정한 client 라이브러리의 revision segment reader와 decoder만 사용하며 HTTP listener·변경 API·payload 파일을 만들지 않는다. `cat --raw`의 binary/BOM 변환이나 과거 인증 없는 `cm api`로 fallback하지 않는다. helper는 자기 프로세스에만 유지하고 tree 종료 때 stdin을 닫은 뒤 남아 있을 때만 자기 자식을 종료한다.

helper의 decoded scratch는 4MiB이며 반환 frame은 최대 64KiB다. owner는 같은 tree의 한 stream만 받으며 body에 120초 제한을 둔다. 실패·크기 초과·중단된 body는 해당 자식을 종료하여 다음 revision과 섞이지 않게 한다. 파일 내용의 처음 요청은 전체 revision 길이와 서버의 MD5 내용 식별을 확인하고 요청분만 staging한다. 이후 요청은 검증한 SHA-256 chunk proof와 대조한다. 오류/손상/불완전 stream은 요청 buffer에 게시하지 않는다. MD5는 Plastic의 기존 내용 식별이며 새 보안 인증/서명 기능이 아니다. 라이브러리 adapter는 내부 고정 버전 경계이며 vendor의 안정된 공개 SDK로 표시하지 않는다.

하나의 revision에 최대 4MiB proof·64KiB chunk와 최대 1MiB 요청 staging을 유지하며 파일 상한은 8GiB다. 순차 읽기는 열린 stream/chunk를 재사용한다. 이전 위치나 다른 파일로 돌아가면 같은 소유 helper의 새 stream과 prefix 읽기 또는 전체 증명이 필요할 수 있다. 전체 payload를 메모리/disk cache에 저장해 우회하지 않는다. 외부 client의 cache·전송·native 메모리는 이 관리 buffer 한도와 다르며 별도 계측한다.

`snapshotAvailable`은 소유한 고정 manifest의 유효성을 뜻한다. tree를 열 때 identity/digest와 전체 metadata를 검증하고 읽기 핸들로 manifest의 쓰기·교체를 차단한다. metadata callback마다 같은 manifest를 다시 읽고 hash하지 않으며, 경로 보호와 reader fence는 유지한다. 서버의 매 revision 접근은 내용 읽기에서 별도로 확인한다. 서버 권한/내용 소실·장애 때 local manifest만으로 읽기 성공을 주장하지 않는다. reader lease가 열린 동안 회수를 차단하며 release는 로컬 owner/intent만 종료한다. 서버 이력·공유 cache를 삭제하거나 revision 삭제를 방지하는 서버 pin을 만들지 않는다.

## Library·결과·회수

Git 경로와 같은 [원본 Library 수명주기](git-library-reference.md)를 사용한다. `library prepare`로 원본 Library만 같은 볼륨의 불변 base로 이동하고 기존 경로에 원본 overlay를 제공한 뒤 반환 id를 `--library-basis`에 고정한다. 원본/A/B 변경은 각각의 저널이다. 초기/최종 전환에는 원본 Editor의 정상 종료가 필요하고 Splinter가 원본 Editor나 공유 MCP 서버를 자동 종료하지 않는다.

독립 결과는 기존 파일 manifest와 `files/objects`에 저장한다. 생성/일반 읽기에는 payload를 저장하지 않으며 명시적 checkpoint의 결과 수집에서 기준과 결과의 실제 payload를 보존한다. 결과는 서버·원본·Library basis가 없어도 검증할 수 있다. 이 결과 보존 비용은 작업 delta와 구분한다.

release/discard는 seal·정상 stop/unmount·소유 provider/handle/process/delta 부재를 확인한다. 소스 기준의 로컬 release intent를 완료한 뒤만 해당 작업의 Library dependent를 제거한다. 기준 회수 뒤 dependent 제거에 실패해도 같은 요청으로 이어간다. 마지막 peer 회수 후 `library restore`로 원본 최종 범위만 반영하고 일반 NTFS Library를 복원한다. 다른 작업·기존 VSS pending·원본 등록과 독립 결과는 보존한다.

현재 reader의 자동·고정 binary/BOM 읽기 자격은 [보안 경계 검증](verification/security-boundaries.md)을 따른다. 과거 [PPB 검증](verification/plastic-project-basis.md)과 [PPB 계획](plans/plastic-project-basis.md)의 실제 Unity A/B는 당시 API reader의 근거이며 새 reader의 Unity·대형 segmented 입력 통과로 승계하지 않는다.
