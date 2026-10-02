# VCS 없는 NTFS 작업공간

제품 CLI는 `--source git|none`으로 소스 공급자를 선택한다. 생략하면 기존 Git 경로다. Git 오류·손상·unknown을 일반 폴더로 해석하지 않는다. `none`은 VCS 관리 정보가 없는 일반 폴더의 NTFS task만 허용한다. 원본과 조상/하위 경로의 `.git`·`.plastic`·`.hg`·`.svn`, 지원하지 않는 파일 유형은 보존 후 거부한다. Plastic 전용 연동은 아직 제공하지 않는다. 실제 지원 상태는 [검증 기록](verification/vcs-independent-workspaces.md)이 소유한다.

## 입력과 같은 작업 재개

```powershell
pwsh ./scripts/splinter.ps1 schema workspace.ensure
pwsh ./scripts/splinter.ps1 doctor --project <등록된 일반 폴더> --source none
pwsh ./scripts/splinter.ps1 workspace ensure --project <등록된 일반 폴더> --key <고정 key> --source none --storage ntfs-snapshot --home <등록된 home>
```

`none`의 기본 basis는 `snapshot`, application은 `none`이다. Unity 파일·Editor·라이선스 없이 생성/편집/재개/결과 보존/회수/폐기를 요청할 수 있다. Unity 소스를 사용할 때는 최초 요청에 `--application unity`를 고정하고 필요한 시점에만 `--editor` 또는 `unity start`를 요청한다. application `none`의 모든 경로는 일반 데이터이며 `Library`·IDE 파일을 임의로 제외하지 않는다.

소스/application 종류·basis·storage·budget은 request digest와 상태에 고정된다. 같은 project/key/id/home과 입력을 재사용한다. 상태는 현재 v2만 읽으며 이전 v1/선택 필드가 없는 상태를 자동 이관하지 않는다. 원본/작업/서비스 영수증을 보존한다. 새 상태를 읽는 현재 제한 서비스 패키지와 정확한 project/home grant가 필요하다. 설치/업데이트는 [기존 설치 절차](storage-privilege-service.md#설치와-발견)의 별도 관리자 Apply다.

## 기준·검사·결과

생성은 소스 메타데이터를 검사하고 기존 VSS·WinFsp·범위 저널을 사용한다. 전체 payload 복제·읽기 hydration·파일 전체 copy-up fallback은 없다. `Splinter.SourceControl.Files`는 검증된 불변 snapshot 경로에서 일반 파일/디렉터리의 경로·유형·길이·SHA-256·mtime·속성을 읽어 기준 manifest를 고정한다. 이후 원본 변경이나 재연결은 기준을 재계산하지 않는다. 공통 revision 필드는 공급자의 opaque identity이며 `none`에서는 manifest의 `sha256:` identity다. Git branch/commit을 요구하거나 내부 `git init`을 실행하지 않는다.

현재 일반 폴더의 최초 기준/명시적 내용 검사는 전체 읽기다. mtime만으로 cache를 신뢰하지 않는다. manifest는 최대 32,768개 항목·경로 UTF-8 합계 1 MiB·JSON 16 MiB이며 더 큰 입력은 거부한다. 일반 status는 기존처럼 자원만 관측한다.

`workspace checkpoint --id <id> --message <설명>`는 소유 application을 정상 종료하고 안정된 결과를 보존한다. 작업별 control의 `files/baseline.json`과 `result.json`, owner/collection 영수증, `objects/<sha256>`에 기준과 결과의 **실제 payload**를 저장한다. 동일 내용은 그 작업 안에서 재사용한다. 기준 전체와 결과 전체를 명시적 수집 시 보존하므로 이 비용은 private 범위 저널과 별개다. `resultStorage`는 기준/결과 논리 바이트·고유 payload 바이트/개수를 반환한다. 실제 할당·물리 peak·VSS 귀속은 별도로 관측한다.

모든 파일·hash·owner를 확인하고 전후 `stable-journal-v1` seal이 같을 때만 독립 결과를 게시한다. 결과 확인에는 원본/작업 mount/VSS를 사용하지 않는다. 같은 collection의 응답 유실은 이미 게시한 내용과 고정 임시 파일을 확인해 재개하며, 다른 intent·부분/손상 파일은 보존 후 거부한다. 결과를 자동 덮어쓰거나 sweep하지 않는다. checkpoint는 컴파일/테스트 합격 증거를 만들지 않는다.

## 반영·회수·폐기

VCS 없는 `workspace integrate`는 상태 변경·Editor stop·원본 쓰기 전에 `SourceIntegrationUnsupported`로 거부한다. 기준/결과/최신 원본 비교를 사용하는 안전한 apply는 [후속 계획](plans/implementation-plan.md#이후-순서와-원본-반영-원칙)이다.

release는 실제 독립 payload 검증, 정상 stop·소유 자원 0·unmount 후 seal·delta 부재·snapshot 의존성/격리 guard를 유지한다. 제한 서비스도 같은 고정 owner/basis/result와 내용을 확인한다. 회수 지연/unknown은 완료나 0이 아니다. 명시적 discard는 별도 intent·seal·완료된 file-source 영수증과 기존 별도 authorization을 사용하며 이미 보존한 독립 결과를 지우지 않는다. 같은 intent 재시도만 허용하고 원본·peer·공유 자원을 삭제하지 않는다.
