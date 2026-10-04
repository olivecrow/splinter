# Plastic 사용 환경의 작업공간

Splinter가 병렬 작업공간을 제공한다. Unity VCS/Plastic SCM 연동은 원본의 관리 정보·등록·잠금을 격리하고 추가 checkout/복제 비용을 만들지 않기 위한 호환 경계다. 현재 후보의 자동/실제 자격은 [검증 기록](verification/plastic-compatibility.md), 이번 범위는 [계획](plans/plastic-compatibility.md)이 소유한다.

기록된 changeset을 참조하는 별도 `plastic-snapshot`은 [프로젝트 기준본](plastic-project-basis.md)과 [현재 검증](verification/plastic-project-basis.md)을 따른다. 아래 기본 snapshot 선택·서비스·VSS 회수 자격은 기존 `ntfs-snapshot`이다.

## 선택과 입력

`--source plastic`을 최초 요청에 명시한다. 현재 상태 v2에 project/key/id/home·source/application·basis·digest를 고정한다. `plastic`의 기본 application은 `unity`, basis는 `snapshot`이며 NTFS task만 지원한다. `--application none`은 Editor 없는 입력의 공통 수명주기에 사용한다. 다른 source로 fallback하지 않는다.

```powershell
pwsh ./scripts/splinter.ps1 doctor --project <Plastic 원본> --source plastic --application unity
pwsh ./scripts/splinter.ps1 schema workspace.ensure
pwsh ./scripts/splinter.ps1 workspace ensure --project <Plastic 원본> --key <같은 작업 key> --source plastic --application unity --base snapshot --storage ntfs-snapshot --home <등록한 짧은 home>
```

로컬 `cm`의 버전·정확한 workspace root/GUID·`regular/static` 유형을 읽기 전용으로 확인한다. root `.plastic/plastic.workspace`의 제한된 내용 hash와 소유 요청을 admission에 고정하고, 실제 캡처의 검증된 읽기 위치에서 같은 관리 identity를 확인한 뒤 파일 기준을 확정한다. 임의 changeset/branch를 checkout해 기준을 만들지 않으며 실제 캡처 시점의 파일이 기준이다. 원본의 파일 변경은 이후에도 허용한다.

원본은 Plastic workspace의 정확한 root여야 한다. partial/Gluon·dynamic/plasticfs·혼합/중첩 VCS·다른 workspace를 상속하는 경로는 거부한다. home은 원본 밖이며 **모든 Plastic 등록과 겹치지 않고 Git/Plastic 등 관리 정보를 상속하지 않는 위치**여야 한다. 제품 저장소 아래 `.local`은 그 저장소의 Git을 상속하므로 Plastic worker home으로 사용하지 않는다. 임의 등록을 삭제·이동해서 조건을 맞추지 않는다. Windows/NTFS·WinFsp·등록된 제한 서비스·지원하는 일반 파일·공간 조건은 [NTFS 계약](ntfs-workspaces.md)을 유지한다.

## 관리 정보와 결과

원본 `.plastic`을 worker의 기준 트리에서 제외한다. `.plastic`·`.git`·`.hg`·`.svn` root는 overlay identity의 `deniedTopLevel`로 읽기/열거와 생성·rename 대상에서 차단한다. 이 정책은 같은 delta의 재연결·재생에 고정된다. Git 경로의 원본 `.git` 제외와 private linked worktree pointer 생성은 별도 기존 정책을 유지한다.

A/B는 **Plastic에 등록되지 않은 파일 작업공간**이다. 원본 Plastic 등록·서버 잠금·사용자 설정·자격 증명을 공유/변경하지 않으며 작업별 Plastic checkout·wktree/index/cache 복제도 만들지 않는다. 명시적 소스 검사·수집 전에는 local workspace 목록과 home/worker의 관리 영역 부재를 확인한다. 관측 실패·손상·유형 미지원은 보존 후 거부한다. 임의 외부 도구에 대한 보안 sandbox나 동시 전역 VCS 등록 변경의 배타 잠금을 제공하는 것은 아니다.

`checkpoint`는 VCS commit/check-in과 무관한 파일 manifest와 실제 독립 기준/결과 payload를 보존한다. 명시적 수집 전에는 payload를 hydrate/복제하지 않는다. 수집에서 필요한 payload 저장은 생성/읽기·delta 비용과 구분한다. 결과와 폐기의 원본 관리 identity는 `plastic/admission.json`·`plastic/snapshot.json`, 공통 파일 소유/기준/결과는 `files/`에 남는다. 해당 schema는 `urn:splinter:v1:plastic-source-admission`·`plastic-source-receipt`·`file-source-manifest`다.

release/discard는 [공통 파일 결과](vcs-independent-workspaces.md)·[명시적 폐기](workspace-discard.md)의 intent·seal·독립 결과·원본/peer·소유 자원 회수 guard를 유지한다. 권한 서비스는 요청에 묶인 receipts와 payload만 검증하며 SYSTEM 권한으로 `cm`·원본 Plastic 관리 정보·사용자 인증 설정을 사용하지 않는다. 최신 서비스 패키지가 필요하며 일반 CLI는 자동 elevation/helper fallback을 하지 않는다.

비소유 snapshot과 공유 source-volume storage를 보호해 회수가 보류될 수 있다. A release는 `reclaim-pending`, 명시적 discard는 `discard-reclaim-pending`의 Accepted/nonterminal 응답과 같은 intent의 후속 행동을 반환하며 완료로 해석하지 않는다. 별도 작은 T: A/B의 마지막 snapshot 회수는 한정 확인했으며 선행 C: 보류·Unity CLI의 A/B 발견 실패와 구분한다. 정확한 입력/자격은 [PC 검증](verification/plastic-compatibility.md)을 따른다.

후속 [Unity CLI 인계 검증](verification/unity-cli-handoff.md)은 같은 등록 T: 입력의 새 A/B에서 목록에 빠진 worker의 exact-path command·loaded 코드·A 정상 종료 뒤 B 작업과 두 결과/회수를 확인했다. CLI 목록 발견을 수정한 것은 아니며 MCP·다른 입력/버전의 연결을 자동 승계하지 않는다. 실제 사용은 [정확한 대상 선택 절차](alpha-workspaces.md#unity-cli의-정확한-대상-선택)를 따른다.

원본 반영·Plastic check-in/update/merge·새 VCS 등록은 제공하지 않는다. `workspace integrate`는 상태 변경·Editor 종료·원본 쓰기 전에 거부한다. 독립 결과는 보존할 수 있으며 원본 반영은 별도 후속이다. Editor의 refresh·컴파일·Play/Test/빌드와 외부 도구 연결은 AI/기존 도구의 책임이며 Splinter의 readiness/checkpoint는 그 성공을 주장하지 않는다.
