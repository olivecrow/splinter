# Plastic 프로젝트 기준본

[English](../plastic-project-basis.md)

`--source plastic --storage plastic-snapshot --base <changeset@repository@server>`는 기록된 프로젝트 버전을 WinFsp 범위 저널에 연결한다. VSS·새 checkout/Plastic 등록·최초 payload 복제·읽기 disk hydration을 사용하지 않는다. 원본은 기존 NTFS 위치에서 계속 편집할 수 있다. 구형 `ntfs-snapshot`은 폐기했으며 자동 이관 없이 보존·거부한다.

## 입력과 관리 경계

잘못된 고정 revision 입력은 `PlasticBasisRequestInvalid`/`correct-request`다. 이미 고정한 목록·receipt·revision 내용의 손상이나 변경은 `PlasticBasisInvalid`로 구분하며 `requires-user-action`·workspace `preserve`/`workspace.status`를 반환한다. 동일 task/basis·저널을 보존하고 새 기준 선택이나 다른 VCS로 우회하지 않는다.

원본은 정확한 regular/static Plastic workspace root다. 명시한 원본 local root의 controlled-directory XML과 지정한 changeset 목록의 canonical repository/server·repository ID를 대조한다. 명시적 numeric changeset·root revision/GUID·파일 revision/길이/hash·목록 digest와 owner를 `snapshot/plastic-basis.json`에 고정한다. branch 최신값·mutable 원본·다른 VCS로 fallback하지 않는다. working tree의 dirty/private 파일은 기록된 기준에 포함된 것으로 표시하지 않는다. Unity 입력에는 기록된 Assets·Packages/manifest.json·ProjectSettings/ProjectVersion.txt가 있어야 한다.

```powershell
pwsh ./scripts/splinter.ps1 schema workspace.ensure
pwsh ./scripts/splinter.ps1 workspace ensure --project <original> --key <stable-key> --source plastic --application unity --storage plastic-snapshot --base <changeset@repository@server> --home <same-volume-short-home> --library-basis <prepared-id>
```

home은 모든 VCS 관리/Plastic 등록을 상속하거나 겹치지 않는다. VSS service/grant는 이 기록된 기준 경로의 전제가 아니다. 설치된 WinFsp·PowerShell 7과 일반 사용자 `cm`의 저장소 읽기 접근이 필요하다. 내용 reader는 `%ProgramFiles%/PlasticSCM5/client`의 검증된 `11.0.16.10484` 라이브러리를 사용하며 다른 버전은 `PlasticBasisUnsupported`로 보존·거부한다. 원본 `.plastic` 등 관리 root를 worker에서 제외하고 쓰기·rename도 차단한다. worker는 등록하지 않는 파일 작업공간이며 Plastic update/check-in/merge·인증/서버 설정을 제공하지 않는다. 전체/선택 로컬 반영은 [별도 계획/intent 계약](plastic-original-apply.md)을 따른다.

기록된 tree의 모든 깊이에서 VCS 관리 entry를 거부한다. 실행 중 도구가 없는 nested `.git`/`.plastic` 경로를 조회하는 것은 ordinary not-found이며, 이 조회를 허용해 관리 entry의 기록이나 worker의 최상위 관리 root 쓰기를 허용하지 않는다.

## 읽기와 비용

고정 목록은 `cm ls --tree` XML을 순차 읽어 불변 페이지 catalog로 저장한다. 내용은 tree가 소유한 일반 사용자 PowerShell 자식의 private stdin/stdout으로 읽는다. `PlasticRevisionBridge`는 고정한 client 라이브러리의 정확한 revision reader와 decoder만 사용하며 HTTP listener·변경 API·payload 파일을 만들지 않는다. adapter 소스도 길이가 늘어나는 명령줄 대신 private pipe로 전달한다. `cat --raw`의 binary/BOM 변환이나 과거 인증 없는 `cm api`로 fallback하지 않는다. helper는 자기 프로세스에만 유지하고 tree 종료 때 stdin을 닫은 뒤 남아 있을 때만 자기 자식을 종료한다.

helper의 decoded scratch는 4MiB이며 반환 frame은 최대 64KiB다. owner는 같은 tree의 한 stream만 받으며 각 blocking frame/body 읽기에 120초 제한을 두고 caller의 hash 처리 중에는 해제한다. 실패·크기 초과·중단된 body는 해당 자식을 종료하여 다음 revision과 섞이지 않게 한다. 파일 내용의 처음 요청은 전체 revision 길이와 서버의 MD5 내용 식별을 확인하고 요청분만 staging한다. 이후 요청은 검증한 SHA-256 chunk proof와 대조한다. 오류/손상/불완전 stream은 요청 buffer에 게시하지 않는다. MD5는 Plastic의 기존 내용 식별이며 새 보안 인증/서명 기능이 아니다. 라이브러리 adapter는 내부 고정 버전 경계이며 vendor의 안정된 공개 SDK로 표시하지 않는다.

하나의 revision에 디스크 기반 SHA-256 chunk proof metadata·64KiB chunk와 최대 1MiB 요청 staging을 유지하며 파일 크기·segment 개수의 총량 상한은 없다. 순차 읽기는 열린 stream/chunk를 재사용한다. 작은 파일은 같은 직렬 helper에서 검증된 client의 `GetSmallFilesData`로 최대 64개 exact revision ID·decoded payload 합계 4MiB를 선행 읽는다. 반환 ID·segment·마지막 표시·길이·MD5를 모두 요청 묶음과 대조하고 누락·중복·외부 record는 노출 전에 전체 묶음을 거부한다. tree의 기존 전체 MD5·영속 SHA-256 chunk proof도 유지한다. helper는 해당 bounded 묶음만 유지하며 더 큰 파일은 기존 segment 읽기를 사용한다. 원본 파일 대체나 프로젝트 hydration이 아니다. 외부 client packet buffer·payload 묶음·native handle·전체 private memory는 metadata cache와 별도로 계측한다.

SCAL 후보의 task/basis/overlay 형식은 v4/v3/v3이며 이전 상태를 자동 이관하지 않는다. 자격 검증은 [진행 중](verification/scalable-storage.md)이다. 읽기 전용 고정 revision 64개 프로토콜 검증은 전체 수명주기·Unity 성능과 구분하고 과거 RI/PPB 측정을 새 후보에 승계하지 않는다.

`snapshotAvailable`은 소유한 고정 manifest의 유효성을 뜻한다. tree를 열 때 identity/digest와 전체 metadata를 검증하고 읽기 핸들로 manifest의 쓰기·교체를 차단한다. metadata callback마다 같은 manifest를 다시 읽고 hash하지 않으며, 경로 보호와 reader fence는 유지한다. 서버의 매 revision 접근은 내용 읽기에서 별도로 확인한다. 서버 권한/내용 소실·장애 때 local manifest만으로 읽기 성공을 주장하지 않는다. reader lease가 열린 동안 회수를 차단하며 release는 로컬 owner/intent만 종료한다. 서버 이력·공유 cache를 삭제하거나 revision 삭제를 방지하는 서버 pin을 만들지 않는다.

## Library·결과·회수

Git 경로와 같은 [원본 Library 수명주기](git-library-reference.md)를 사용한다. `library prepare`로 원본 Library만 같은 볼륨의 불변 base로 이동하고 기존 경로에 원본 overlay를 제공한 뒤 반환 id를 `--library-basis`에 고정한다. 원본/A/B 변경은 각각의 저널이다. 초기/최종 전환에는 원본 Editor의 정상 종료가 필요하고 Splinter가 원본 Editor나 공유 MCP 서버를 자동 종료하지 않는다.

독립 결과는 기존 파일 manifest와 `files/objects`에 저장한다. 생성/일반 읽기에는 payload를 저장하지 않으며 명시적 checkpoint의 결과 수집에서 기준과 결과의 실제 payload를 보존한다. 결과는 서버·원본·Library basis가 없어도 검증할 수 있다. 이 결과 보존 비용은 작업 delta와 구분한다.

지속 작업의 `checkpoint --request-key`는 [공통 불변 결과 엔진](repeated-original-apply.md)을 사용한다. 짧은 mutation 경계에서 소스를 고정하고 경계 밖에서 scan/payload를 보존하며 최종 종료는 전체 seal을 유지한다. 동시 읽기를 지원하는 tree의 로컬 payload/증명은 최대 8 worker를 쓰고 recorded revision 획득은 직렬로 유지한다. 실패·취소 시 진행 중 작업을 모두 drain하고 새 object는 여전히 게시 전에 각각 영속 flush한다. [RT-01–04](verification/result-collection-throughput.md)는 과거 수집 처리량 자격이며 실제 시간 측정은 None 입력이므로 Plastic 서버 성능 근거는 아니다.

release/discard는 seal·정상 stop/unmount·소유 provider/handle/process/delta 부재를 확인한다. 소스 기준의 로컬 release intent를 완료한 뒤만 해당 작업의 Library dependent를 제거한다. 기준 회수 뒤 dependent 제거에 실패해도 같은 요청으로 이어간다. 마지막 peer 회수 후 `library restore`로 원본 최종 범위만 반영하고 일반 NTFS Library를 복원한다. 다른 작업·기존 VSS pending·원본 등록과 독립 결과는 보존한다.

[보안 경계 검증](verification/security-boundaries.md)은 private reader 자격을 기록하며 [RI-07](verification/repeated-original-apply.md)은 현재 client의 segmented/BOM 읽기와 실제 Unity 원본/A/B 순서를 소유한다. 과거 [PPB 검증](verification/plastic-project-basis.md)과 [PPB 계획](plans/plastic-project-basis.md)은 당시 API reader의 근거로 유지하고 다른 후보의 자격으로 승계하지 않는다.
