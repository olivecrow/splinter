# Plastic 사용 환경의 작업공간

[English](../plastic-compatibility.md)

과거 VSS C:/T: A/B·회수·도구 자격은 [PC 기록](verification/plastic-compatibility.md)과 [VR-01](verification/vss-and-mcp-remediation.md)에 보존한다. 현재 [VSS 폐기](plans/vss-retirement.md) 뒤 이 경로는 실행하지 않는다.

Splinter가 병렬 작업공간을 제공한다. Unity VCS/Plastic SCM 연동은 원본의 관리 정보·등록·잠금을 격리하고 추가 checkout/복제 비용을 만들지 않기 위한 호환 경계다. 현재 후보의 자동/실제 자격은 [검증 기록](verification/plastic-compatibility.md), 이번 범위는 [계획](plans/plastic-compatibility.md)이 소유한다.

현재 `plastic-snapshot`은 기록된 changeset/revision을 참조하며 [프로젝트 기준본](plastic-project-basis.md)과 [PPB 검증](verification/plastic-project-basis.md)을 따른다. VSS 서비스와 grant는 필요하지 않다.

## 선택과 입력

`--source plastic`을 최초 요청에 명시한다. 현재 상태 v2에 project/key/id/home·source/application·basis·digest를 고정한다. `plastic`의 기본 application은 `unity`, 명시적 기록 changeset이 필요하며 NTFS task만 지원한다. `--application none`은 Editor 없는 입력의 공통 수명주기에 사용한다. 다른 source로 fallback하지 않는다.

```powershell
pwsh ./scripts/splinter.ps1 doctor --project <Plastic 원본> --source plastic --application unity
pwsh ./scripts/splinter.ps1 schema workspace.ensure
pwsh ./scripts/splinter.ps1 workspace ensure --project <Plastic 원본> --key <같은 작업 key> --source plastic --application unity --base <changeset@repository@server> --storage plastic-snapshot --home <짧은 home>
```

로컬 `cm` 버전·정확한 workspace root/GUID·regular/static 유형과 관리 정보 hash를 고정한다. reader는 필요한 source를 포함하는 명시적 고정 changeset/revision을 읽는다. 자동 checkout/check-in이나 캡처 시점의 변경 가능한 원본 fallback은 없다. 이후 원본 파일 변경은 허용한다.

원본은 Plastic workspace의 정확한 root여야 한다. partial/Gluon·dynamic/plasticfs·혼합/중첩 VCS·다른 workspace를 상속하는 경로는 거부한다. home은 원본 밖이며 **모든 Plastic 등록과 겹치지 않고 Git/Plastic 등 관리 정보를 상속하지 않는 위치**여야 한다. 제품 저장소 아래 `.local`은 그 저장소의 Git을 상속하므로 Plastic worker home으로 사용하지 않는다. 임의 등록을 삭제·이동해서 조건을 맞추지 않는다. Windows/NTFS·WinFsp·지원하는 일반 파일·공간 조건은 [NTFS 계약](ntfs-workspaces.md)을 유지한다.

## 관리 정보와 결과

원본 `.plastic`을 worker의 기준 트리에서 제외한다. `.plastic`·`.git`·`.hg`·`.svn` root는 overlay identity의 `deniedTopLevel`로 읽기/열거와 생성·rename 대상에서 차단한다. 이 정책은 같은 delta의 재연결·재생에 고정된다. Git 경로의 원본 `.git` 제외와 private linked worktree pointer 생성은 별도 기존 정책을 유지한다.

A/B는 **Plastic에 등록되지 않은 파일 작업공간**이다. 원본 Plastic 등록·서버 잠금·사용자 설정·자격 증명을 공유/변경하지 않으며 작업별 Plastic checkout·wktree/index/cache 복제도 만들지 않는다. 명시적 소스 검사·수집 전에는 local workspace 목록과 home/worker의 관리 영역 부재를 확인한다. 관측 실패·손상·유형 미지원은 보존 후 거부한다. 임의 외부 도구에 대한 보안 sandbox나 동시 전역 VCS 등록 변경의 배타 잠금을 제공하는 것은 아니다.

`checkpoint`는 VCS commit/check-in과 무관한 파일 manifest와 실제 독립 기준/결과 payload를 보존한다. 명시적 수집 전에는 payload를 hydrate/복제하지 않는다. 수집에서 필요한 payload 저장은 생성/읽기·delta 비용과 구분한다. 결과와 폐기의 원본 관리 identity는 `plastic/admission.json`·`plastic/snapshot.json`, 공통 파일 소유/기준/결과는 `files/`에 남는다. 해당 schema는 `urn:splinter:v1:plastic-source-admission`·`plastic-source-receipt`·`file-source-manifest`다.

Release/discard는 [공통 파일 결과](vcs-independent-workspaces.md)와 [폐기](workspace-discard.md)의 intent·seal·독립 결과·원본/peer·소유 자원 보호를 유지한다. 일반 사용자 provider가 자기 기준본 참조와 읽기 임대만 회수하며 서비스·helper나 관리자 실행으로 fallback하지 않는다.

과거 볼륨 snapshot 삭제 순서에 따른 `reclaim-pending`·`discard-reclaim-pending`과 T: 한정 회수는 [PC 검증](verification/plastic-compatibility.md)의 역사적 근거다. 기록된 프로젝트 기준본의 회수 자격으로 승계하지 않는다.

후속 [Unity CLI 인계 검증](verification/unity-cli-handoff.md)은 같은 등록 T: 입력의 새 A/B에서 목록에 빠진 worker의 exact-path command·loaded 코드·A 정상 종료 뒤 B 작업과 두 결과/회수를 확인했다. CLI 목록 발견을 수정한 것은 아니며 MCP·다른 입력/버전의 연결을 자동 승계하지 않는다. 실제 사용은 [정확한 대상 선택 절차](alpha-workspaces.md#unity-cli의-정확한-대상-선택)를 따른다.

구형 `ntfs-snapshot` Plastic의 원본 반영·Plastic check-in/update/merge·새 VCS 등록은 제공하지 않는다. 기록된 `plastic-snapshot`의 로컬 파일 전체/선택 반영은 [별도 보호 경계](plastic-original-apply.md)를 따른다. Editor의 refresh·컴파일·Play/Test/빌드와 외부 도구 연결은 AI/기존 도구의 책임이며 Splinter의 readiness/checkpoint는 그 성공을 주장하지 않는다.
