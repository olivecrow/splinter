# Windows 저장소 권한 서비스

현재 `--source none`의 일반 폴더 경로와 v2 상태/서비스 패키지는 [VCS 없는 작업공간](vcs-independent-workspaces.md)을 따른다. 아래 Git 경로와 과거 native 자격을 일반 폴더 지원으로 승계하지 않는다.

`VssSnapshotProvider`는 일반 사용자 CLI에서 `SplinterStorageAuthority` Windows 서비스에 고정 `Capture`·`Observe`·`Release` 요청을 보낸다. CLI·Git·Unity는 일반 사용자 권한을 유지한다. Host는 기존 읽기 전용 in-process 조합 계층이며 이 서비스가 Host나 작업 배정 daemon을 대신하지 않는다. 현재 실제 설치·반복 실행 자격은 [UA 검증](verification/storage-privilege-service.md)을 따른다.

## 설치와 발견

서비스 실행 파일은 `%ProgramFiles%/Splinter/StorageAuthority`, 소유권·요청·VSS 영수증은 `%ProgramData%/SplinterStorageAuthority`에 둔다. 상태는 SYSTEM/Administrators만 수정·열람하며 실행 파일은 일반 사용자에게 읽기·실행만 허용한다. 서비스는 SCM이 LocalSystem으로 실행한다. 설치된 PowerShell 7과 패키지에 고정된 VSS 스크립트만 실행한다.

- `pwsh scripts/package-storage-service.ps1 -OutputPath <새 패키지 경로>`: Release 패키지와 파일별 SHA-256 목록을 만든다. 기존 패키지를 덮어쓰지 않는다.
- 패키지의 `Splinter.Platform.Windows.exe storage-grant <프로젝트 절대 경로> <home 절대 경로> <사용자 SID>`: 기존 디렉터리의 파일 ID·볼륨 ID를 읽는 설치용 진입점이다. 출력 grant 객체들을 JSON 배열로 저장한다. 프로젝트와 home을 생성하거나 권한을 변경하지 않는다.
- `pwsh scripts/setup-storage-service.ps1 -PackagePath <패키지> -GrantFile <grant 배열 JSON>`: 기본값은 읽기 전용 설치 계획이다. 검토한 요청에 `-Apply`를 붙인 관리자 실행만 머신 구성을 변경한다.
- `pwsh scripts/splinter.ps1 storage service status`: SCM 상태·설치 경로·현재 CLI 승격 여부를 읽는다. VSS를 만들거나 권한 상승을 요청하지 않는다.

최초 설치·권한 범위 구성과 명시적 `-Action Update`·`-Action Remove`는 관리자 작업이다. 이미 등록한 사용자·프로젝트·볼륨·home의 정상 workspace 명령에는 권한 상승 코드가 없다. 등록되지 않은 프로젝트/home은 `StorageScopeDenied`로 거부한다. 경로 문자열이 같아도 디렉터리를 교체했거나 볼륨이 바뀌면 기존 grant로 실행하지 않는다.

설치 계획은 관리자 권한을 요청하기 전에 입력을 검사한다. grant는 한 개여도 JSON 배열이며 필드 이름·SID·볼륨/파일 ID 형식·기존 canonical 디렉터리와 중복 범위를 검사한다. 패키지의 필수 실행 파일·DLL·runtime 설정·스크립트와 hash, 중복 파일 이름을 확인한다. 중복 JSON 속성·알 수 없는 필드·대소문자가 다른 필드는 거부한다. 계획 검사는 패키지 안의 실행 파일을 실행하거나 시스템에 파일을 설치하지 않는다. 실제 grant의 파일·볼륨 identity 대조와 실행 성공은 설치 후 서비스 검증이 계속 필요하다.

업데이트는 같은 프로토콜의 기존 grant와 보호된 상태를 보존한다. 제거는 활성 capture나 미완료 요청이 있으면 거부한다. 서비스 등록을 제거해도 패키지·소유권 기록·독립 결과를 자동 삭제하지 않는다. 구버전 자동 이관이나 기존 수동 helper 영수증의 자동 인수는 없다.

## 요청 경계

IPC는 로컬 named pipe `Splinter.StorageAuthority.v1`이다. ACL은 등록된 사용자 SID와 SYSTEM만 허용하고 Network SID는 거부한다. 등록 사용자는 클라이언트 읽기·쓰기만 가능하며 서버 instance 생성 권한은 없다. 클라이언트는 SCM의 LocalSystem 계정·고정 실행 경로·독립 프로세스 구성과 pipe 서버 PID를 확인한다. 서비스는 클라이언트를 impersonate해 실제 SID를 얻으며 요청 본문의 사용자 이름은 신뢰하지 않는다. 첫 pipe instance만 생성하므로 다른 서버의 같은 이름을 공유하지 않는다.

사용자 프로젝트·home·workspace의 읽기는 요청자의 토큰으로 한다. 서비스는 reparse·경로 대체를 거부하고 디렉터리 ID를 등록값과 대조한다. 조상 경로와 검사한 기록은 작업 동안 rename/delete가 안 되는 핸들로 유지한다. 원본 내부의 일반 파일 편집은 계속 가능하다.

요청은 현재 버전·고정 operation·owner·프로젝트·home·snapshot ID·boot에 묶인 검증/회수 ID만 받는다. 임의 코드·명령·실행 파일·관리자 쓰기 목적지는 없다. 중복/알 수 없는 JSON 필드·필수 필드 누락·크기 초과를 거부한다. 입력/출력은 `storage-authority-request.schema.json`과 `storage-authority-response.schema.json`이 소유한다.

## 보존과 재시도

서비스는 자신이 캡처한 snapshot만 소유권 기록에 연결한다. 다른 workspace의 snapshot ID나 사용자 파일에서 가져온 capture 영수증으로 권한을 얻을 수 없다. 서비스의 영수증은 보호된 디렉터리에만 쓰며, 응답의 고정 영수증 이름을 workspace 폴더에 저장하는 주체는 일반 CLI다.

같은 요청 ID의 입력이 바뀌면 `StorageRequestConflict`, 다른 미완료 요청이 있으면 `StorageRequestPending`이다. 완료 응답 유실 뒤 재시도는 저장된 응답을 반환한다. 중단된 요청은 같은 intent로 재개하며 이미 캡처한 snapshot을 새로 만들지 않는다. 보호된 상태의 완전한 `.pending` JSON은 재시작 시 flush 후 게시하고, 손상/불완전한 파일은 보존 후 거부한다. native capture 결과가 불명확하면 `CaptureReconcileRequired`로 보존한다.

일반 CLI는 성공한 응답의 검증 영수증을 먼저 게시하고 `snapshot.json`을 마지막에 게시한다. 비종결 실패는 이 완료 표지를 갱신하지 않는다. 응답 저장 중 종료해도 기존 capture/release 요청을 재시도한다. Runtime의 `capture-intent` 단계와 서비스의 소유 영수증이 남아 있으면 아직 workspace에 반환 ID를 게시하지 못한 capture도 읽기 검증을 재개할 수 있다.

명시적 workspace 폐기는 같은 `Release` operation의 별도 `release.discard` 계약이다. 서비스는 고정 intent/seal·완료 Git 폐기 receipt와 실제 task branch/등록 부재·mount/delta 부재·소유 자원 0·공유 의존성·reader fence를 확인한다. 보호된 `discard-authorized.json`만 게시하고 결과 보존을 주장하지 않는다. 현재 CLI와 일치하는 서비스 패키지를 명시적으로 업데이트한 뒤 사용할 수 있으며 실제 자격은 [폐기 검증](verification/workspace-discard.md)을 따른다.

보존형 회수는 Runtime의 `stable-journal-v1` 결과 검증·Git 객체 완전성·unmount 후 seal 일치를 거친다. 서비스는 durable `release-snapshot` 단계, 독립 결과와 고정 Git refs, 소유 프로세스·메모리·핸들 0, mount/delta 부재, 다른 workspace와 validation 의존성, 독점 reader fence를 추가 확인한다. 기존 VSS live identity·삭제 격리 guard와 `reclaim-pending`을 유지한다. 시간이 지났다는 이유로 snapshot을 삭제하지 않는다.

서비스 부재·정지·버전 불일치는 설치/복구 필요 상태다. 자동 `RunAs`, 관리자 CLI, 이전 helper로 우회하는 fallback은 없다. `scripts/vss-snapshot.ps1`은 별도로 승인한 개발 진단·수동 복구에만 직접 사용하며 정상 workspace 경로의 대체 권한 방식으로 자동 선택하지 않는다.
