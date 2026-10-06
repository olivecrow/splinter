# Splinter 용어집

CLI 응답과 기능 문서를 읽을 때 필요한 용어를 설명한다. 각 항목의 링크는 정확한 사용 조건을 소유하는 문서다. 명령 입력은 실행 전에 `schema <command>`로 확인한다.

## 작업과 식별자

| 용어 | 쉬운 설명과 구분 | 자세한 계약 |
| --- | --- | --- |
| 원본 / project | 작업의 출발점이자 명시적으로 결과를 반영할 대상 프로젝트. 작업 중에도 원본을 계속 수정할 수 있다. | [작업공간](vcs-independent-workspaces.md) |
| task / 작업 | 같은 목적과 고정 key로 이어가는 논리적인 작업. 대화나 CLI 프로세스가 끝나도 작업이 자동 종료되지는 않는다. | [AI 시작 안내](agent-start.md) |
| workspace / 작업공간 | 특정 작업의 파일을 편집하는 독립 경로와 그 상태·소유 자원. 응답의 `data.workspace.path`를 사용한다. | [현재 기반](foundation.md) |
| worker / 작업자 | 편집하는 작업공간을 가리키는 말. 개발용 view 문서에서는 후보를 내보내는 소스 작업공간을 뜻한다. AI 자체나 background thread의 이름이 아니다. | [개발용 view](view-workflow.md) |
| peer / 다른 작업 | 같은 프로젝트나 기준을 사용하는 다른 작업공간. A의 회수는 B의 파일·Editor·결과를 정리할 권한이 아니다. | [회수와 관측](ntfs-workspaces.md#회수와-관측) |
| home | 같은 프로젝트의 작업 상태·작업 경로·공유 실행 파일을 두는 기준 디렉터리. 재개와 회수에서도 같은 home을 지정한다. | [AI 시작 안내](agent-start.md) |
| key / request key | 같은 논리 작업이나 요청을 다시 찾기 위한 고정 이름. workspace의 `--key`, 게시의 `requestKey`, 폐기의 `intentKey`는 각각의 명령에서 사용하는 키다. 같은 키로 다른 입력을 보내면 새 요청으로 바뀌지 않는다. | [AI 시작 안내](agent-start.md), [후보 게시](source-view.md#같은-요청의-게시-복구) |
| ID / id | Splinter가 작업공간·기준본 등 특정 객체에 부여한 식별자. 사용자가 정하는 작업 key와 구분하며 status/list로 정확한 대상을 확인한다. | [명시적 폐기](workspace-discard.md) |
| owner / 소유자 | 자원이나 영수증이 어느 작업에 속하는지 나타내는 identity. 개발용 workflow의 generation은 같은 소유자의 세대를 구분한다. | [개발용 view](view-workflow.md) |
| digest / fingerprint / hash | 입력이나 내용을 요약한 값. `requestDigest`는 고정 작업 입력, 계획 digest는 반영 계획, fingerprint는 해당 계약의 내용 검증에 사용한다. 서로 대체하지 않는다. | [원본 반영](plain-folder-original-apply.md), [소스 검증](source-verification.md) |
| intent / 수행 의도 | 반영·폐기·복원처럼 중간에 끊길 수 있는 작업의 대상과 입력을 먼저 고정한 기록. 중단 후에는 같은 intent로 이어가며 파일을 지우거나 새 키로 우회하지 않는다. | [원본 반영](plain-folder-original-apply.md), [폐기](workspace-discard.md) |
| receipt / 영수증 | 생성·등록·게시·회수 등 특정 수행에서 확인한 대상과 소유권을 남기는 기록. 기록의 존재만으로 현재 mount나 프로세스가 살아 있음을 뜻하지 않는다. | [프로젝트 기준본](plain-project-basis.md), [회수와 관측](ntfs-workspaces.md#회수와-관측) |

## 기준과 범위 저장

| 용어 | 쉬운 설명과 구분 | 자세한 계약 |
| --- | --- | --- |
| source / 소스 종류 | `git`, `plastic`, `none` 중 선택한 소스 관리 경계. 어떤 VCS의 기준·등록·관리 정보를 보호할지 정하며 application과 별개다. | [VCS 없는 작업공간](vcs-independent-workspaces.md) |
| application / 앱 종류 | 프로젝트를 사용하는 앱의 정책. 현재 `unity`와 `none`이며 Editor 잠금·동반 파일·생성 디렉터리 처리에 쓰인다. | [확장 경계](extensibility-boundaries.md) |
| storage / 저장 모드 | 기준 데이터를 읽고 변경분을 저장하는 방식. 아래 저장 모드 표를 참고한다. source나 application의 다른 이름이 아니다. | [현재 기반](foundation.md) |
| base / baseRevision | `--base`는 요청하는 기준 선택값, `baseRevision`은 선택한 고정 버전의 identity다. Git commit이나 Plastic revision 등 의미는 소스 계약에 따른다. | [Git 기준본](git-project-basis.md), [Plastic 기준본](plastic-project-basis.md) |
| snapshot / 스냅샷 | 변경되지 않는 기준을 읽기 위한 참조와 identity. VSS뿐 아니라 고정 Git object·Plastic revision·관리형 디렉터리 기준도 포함한다. 항상 프로젝트 전체 복사본을 뜻하지 않는다. | [Git 기준본](git-project-basis.md), [Plastic 기준본](plastic-project-basis.md), [프로젝트 기준본](plain-project-basis.md) |
| basis / 기준본 | 여러 작업에서 참조하는 관리형 기준과 수명 기록. Library basis는 Library만, project basis는 VCS 없는 프로젝트와 생성 데이터를 다룬다. task 회수와 마지막 basis 복원은 별도다. | [Library 기준](git-library-reference.md), [프로젝트 기준](plain-project-basis.md) |
| overlay / 겹쳐 보이는 파일 트리 | 불변 기준 위에 그 작업의 변경분을 적용해 보이는 파일 트리. 기준을 복사하거나 직접 수정하지 않고 작업별 내용을 보여 준다. | [NTFS 작업공간](ntfs-workspaces.md) |
| delta / 변경분 | 작업에서 변경한 바이트 범위와 파일 길이·이름·삭제 등의 정보. 변경 파일 전체 복사본과 같은 뜻이 아니다. | [NTFS 작업공간](ntfs-workspaces.md) |
| private / 작업 전용 | 원본·다른 작업과 공유하지 않는 변경 범위나 생성 데이터. 암호화나 악성 코드 실행 격리를 뜻하지 않는다. | [현재 기반](foundation.md) |
| journal / 저널 | 변경 연산과 필요한 payload를 순서대로 저장한 기록. 다시 읽어 작업의 파일 상태를 복원한다. | [압축 저널](journal-payload-compression.md) |
| SJR2 / overlay v2 / candidate v3 | 현재 저널·overlay·후보의 형식 이름과 버전. workspace 상태 v2와는 별도 계약이며 이전 형식을 자동 이관하지 않는다. | [압축 저널](journal-payload-compression.md) |
| payload / extent / fragment / block | payload는 저장한 실제 바이트, extent는 파일의 적용 범위, fragment는 그 범위가 참조하는 부분, block은 압축·복원 단위다. 유효 범위가 작아도 참조 block 전체가 공간을 차지할 수 있다. | [저널 계측](source-verification.md#계측의-의미), [codec](workspace-payload-codec.md) |
| whiteout / 삭제 표시 | 기준에 있는 경로를 특정 작업에서 보이지 않게 하는 기록. 해당 원본 파일을 삭제하지 않는다. | [overlay 실험](snapshot-overlay.md) |
| graft / 기준 연결 | 소스 기준의 특정 경로에 별도 생성 디렉터리 기준을 연결하는 것. 예를 들어 Git 기준에 명시적으로 준비한 Library 기준을 연결한다. | [Library 참조](git-library-reference.md) |
| hydration / 읽기 cache | 기준에서 읽은 내용을 작업 디스크에 내려받아 채우는 방식. 현재 제품 범위 저장은 읽기 disk hydration을 사용하지 않는다. | [Git 기준본](git-project-basis.md), [Plastic 기준본](plastic-project-basis.md) |
| copy-up | 기준 파일을 수정하려고 파일 전체를 작업 전용 사본으로 옮기는 방식. 현재 제품은 전체 파일 copy-up fallback을 사용하지 않는다. 명시적 원본 반영의 파일 stage 비용과 구분한다. | [원본 반영](plain-folder-original-apply.md) |
| mount / provider | mount는 파일 시스템으로 연결한 경로, provider는 그 연결이나 기준 읽기를 제공하는 구현·프로세스다. 소스 공급자도 provider라고 부르므로 해당 문서의 대상과 함께 읽는다. | [NTFS 작업공간](ntfs-workspaces.md) |
| managed view / 관리형 원본 | 승인한 Library·프로젝트 데이터를 같은 NTFS 볼륨에서 이동한 뒤 기존 원본 경로에 제공하는 writable mount. 단순 폴더와 다르며 마지막 명시적 restore로 일반 NTFS를 복원한다. | [Library 참조](git-library-reference.md), [프로젝트 기준본](plain-project-basis.md) |

| 제품 저장 모드 | 기준을 읽는 방식 |
| --- | --- |
| `ntfs-snapshot` | VSS로 고정한 NTFS 기준. 제한 권한 서비스와 등록된 범위가 필요하다. |
| `git-snapshot` | 고정 local Git commit/tree/blob을 읽는다. VSS service/grant가 필요하지 않다. |
| `plastic-snapshot` | 기록된 Plastic changeset/revision을 읽는다. VSS service/grant가 필요하지 않다. |
| `project-snapshot` | 승인한 VCS 없는 관리형 프로젝트 기준을 읽는다. VSS service/grant가 필요하지 않다. |

`git-checkout`, view·probe는 [개발용 CLI](development-cli.md)의 경로다. 제품 NTFS 실패의 자동 fallback으로 사용하지 않는다.

## 보존·반영·회수

| 용어 | 쉬운 설명과 구분 | 자세한 계약 |
| --- | --- | --- |
| checkpoint | 소유 Editor의 정상 종료와 안정된 소스 확인 뒤 결과를 보존하는 명령. 컴파일·테스트 성공을 뜻하지 않는다. | [AI 시작 안내](agent-start.md#편집editor종료) |
| independent result / 독립 결과 | 기존 snapshot이나 작업 mount를 회수해도 보존되는 소스 결과. Git 결과와 파일 payload 결과의 표현은 서로 다르다. | [NTFS 작업공간](ntfs-workspaces.md), [VCS 없는 작업공간](vcs-independent-workspaces.md) |
| seal / 내용 고정 증거 | 특정 시점의 저널 sequence·길이·hash 등으로 이후 변경 여부를 대조하는 값. 컴파일 합격이나 사용자 승인 표시가 아니다. | [소스 검증](source-verification.md), [후보](source-view.md) |
| candidate / 후보 | 정지 worker의 유효 변경분을 별도 게시한 개발용 입력. 게시 후 worker 편집과 분리되지만 여전히 원본 snapshot에 의존하므로 독립 결과와 다르다. | [불변 후보](source-view.md) |
| view / source generation / cache | 개발용 view는 선택한 후보로 실행하는 고정 경로다. source generation은 선택별 소스 세대, cache는 같은 view에서 유지하는 생성 데이터 영역이다. 기본 제품 작업 모드가 아니다. | [개발용 view](view-workflow.md) |
| integrate / apply / 원본 반영 | checkpoint 결과를 명시적 대상에 반영한다. Git branch 반영과 일반 폴더·recorded Plastic의 `--target original` 조건부 파일 게시를 구분한다. | [현재 기반](foundation.md), [파일 반영](plain-folder-original-apply.md), [Plastic 반영](plastic-original-apply.md) |
| release / 자원 회수 | 독립 결과를 보존한 뒤 해당 작업의 소유 자원을 회수한다. 원본 반영이 필수는 아니며 결과 폐기와 다르다. | [회수와 관측](ntfs-workspaces.md#회수와-관측) |
| discard / 작업 폐기 | 성공 checkpoint 없이 해당 작업의 작업 데이터를 명시적으로 버리고 소유 자원을 회수한다. 이미 원본에 반영한 내용을 되돌리는 명령은 아니다. | [명시적 폐기](workspace-discard.md) |
| restore / 원본 복원 | 모든 dependent/reader를 종료한 뒤 Library 또는 project basis의 현재 원본 내용을 일반 NTFS로 돌려놓는 명령. 처음 준비한 시점으로 원본 편집을 되돌리는 뜻이 아니다. | [Library 참조](git-library-reference.md), [프로젝트 기준본](plain-project-basis.md) |
| compact / 저널 정리 | 정상 stop/unmount와 seal·예산 확인 후 대체된 저널 범위를 명시적으로 정리한다. 작업 폐기나 일반 파일 삭제가 아니다. | [압축 저널](journal-payload-compression.md) |
| reclaim-pending / discard-reclaim-pending | 각각 보존 또는 폐기 경로에서 물리 자원 회수를 기다리는 상태. 종료 완료로 읽지 않고 같은 작업·같은 요청으로 명시적으로 재시도한다. | [AI 시작 안내](agent-start.md#실패와-완료-판독) |
| lease / fence / dependency | lease는 작업 동안 보유하는 접근 수명, fence는 읽기·쓰기·회수의 동시 충돌을 막는 경계, dependency는 기준을 아직 사용하는 객체 관계다. 만료·타임아웃은 다른 작업 삭제나 일반 소유권 인수의 승인이 아니다. | [개발용 view](view-workflow.md), [폐기](workspace-discard.md) |
| grant / authorization | grant는 제한 서비스에 등록한 사용자·project·volume·home 범위다. authorization은 특정 capture/release/discard 요청에서 대조하는 권한과 소유 증거다. 사용자 의도와 대상 검사를 대체하지 않는다. | [권한 서비스](storage-privilege-service.md) |

예를 들어 A 작업의 결과만 남기고 끝내려면 checkpoint → release를 사용한다. 원본에 반영하려면 checkpoint → 명시적 integrate → release다. 실패한 A 작업을 버리려면 discard를 사용한다. project basis를 함께 쓰는 B가 남아 있으면 A 회수 뒤에도 project restore를 진행할 수 없다.

## 응답과 검증 상태

| 용어 | 쉬운 설명과 구분 | 자세한 계약 |
| --- | --- | --- |
| Accepted / Succeeded / Failed | 각각 요청 접수·명령 성공·명령 실패다. Accepted는 완료가 아니며 Succeeded도 게임 기능이나 테스트 합격을 뜻하지 않는다. `terminal`, `error`, `nextActions`와 실제 준비·자원 관측을 함께 읽는다. | [AI 시작 안내](agent-start.md) |
| fake / 합성 시험 | 실제 OS·VCS·Editor 대신 제어 가능한 구현과 입력으로 공통 계약을 시험한다. 실제 WinFsp·VSS·Unity 지원 증거로 바꾸어 읽지 않는다. | [검증 안내](verification/README.md) |
| native / 실제 환경 시험 | 실제 OS API·파일 시스템·프로세스 등을 사용한 시험. native 파일 시험도 별도 Unity 실행이나 OS 장애 전체의 자격을 주지는 않는다. | [검증 안내](verification/README.md) |
| Passed / PassedFixture | 지정한 시험 또는 fixture가 정의한 조건을 통과했다는 판정. 모든 입력·현재 변경 후보·다른 OS의 일반 지원 선언이 아니다. | [검증 추적표](plans/validation-matrix.md) |
| scoped-complete-with-limitations | 미리 정한 범위는 끝냈으나 문서에 적은 제약·미측정·미실행 범위가 남은 상태. | [검증 안내](verification/README.md) |
| Partial / InProgress / NotRun / unknown | 각각 일부 확인·진행 중·미실행·관측하거나 판단할 수 없음. NotRun은 실패가 아니고 unknown은 0이나 성공이 아니다. | [검증 추적표](plans/validation-matrix.md) |
| M0 / NR·SF·SW / VC·WF·LB 등 | 계획·검증의 범위를 찾는 추적 ID. M0는 첫 한정 실사용 범위, 나머지는 각 소유 문서의 검증 단위다. 명령 이름이나 남은 구현 기능 개수가 아니다. | [구현 계획](plans/implementation-plan.md), [검증 추적표](plans/validation-matrix.md) |

검증 기록은 명시한 후보·입력·환경에만 적용한다. 과거 통과와 현재 변경에서 아직 실행하지 않은 경계는 별도로 표시한다.
