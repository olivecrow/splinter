# Splinter

AI 에이전트가 Unity처럼 독립 실행이 제약되는 환경에서 병렬 작업공간을 효율적으로 만들고, 이어가며, 원할 때 폐기하도록 돕는 로컬 도구다. 첫 지원 목표는 **Windows/NTFS + Git + Unity**다. 원본 이동·최초 전체 복제·읽기 hydration 없이 불변 snapshot과 작업별 범위 변경분을 사용하며, 원본은 작업 중 계속 수정할 수 있다.

각 대화가 필요할 때만 자신의 작업공간을 사용한다. 같은 작업은 여러 대화와 CLI 실행에 걸쳐 유지하며, 대화나 Editor 종료는 반영·회수·폐기의 승인이 아니다.

등록된 작은 Windows x64 NTFS/Git/Unity 환경의 **첫 한정 실사용 버전 M0를 마감했다**. 제공물은 기존 개발 CLI·소스 스킬과 [AI 시작 안내](docs/agent-start.md)이며, [납품 판정](docs/verification/m0-delivery.md)은 scoped-complete-with-limitations다. [대표 A/B](docs/verification/representative-agent-workflow.md)의 오류/수정·같은 작업 재개·보존/통합/회수·실패 작업 폐기와 비용, 현재 복구 계약의 회귀를 확인했다. 전역 설치·자동 대화 연결·전체 native/OS 장애 보장·대형 지원은 후속이고 폐기에는 해당 계약을 지원하는 제한 서비스가 필요하다. 범용 Unity 저작/Play/Test Runner·AI Runner/DAG·상주 DB는 제품 범위에서 제외한다.

## 현재 제공하는 기능

- .NET CLI, atomic JSON 상태와 작업 잠금, 공통 Core 계약 및 fake 적합성 시험.
- 저장소에 포함한 [AI 사용 스킬](skills/splinter/SKILL.md): 필요성 판단·반환 경로 사용·같은 요청 재개·명시적 종료·실패 처리. 현재 CLI와 대화의 작업 식별자를 사용한다.
- [명시적 폐기](docs/workspace-discard.md)의 고정 intent·중단 재개와 Git/Windows provider·`workspace discard`. 실제 서비스 적용/폐기 자격은 별도로 표시한다.
- `ntfs-snapshot` workspace의 VSS·WinFsp 범위 저널, Git 등록, 작업별 프로세스 그룹과 Unity 검증, 독립 결과 보존과 명시적 회수.
- 변경 기반 소스 검증, 명시적 저널 정리와 선택적 중첩량 예산, snapshot 지연 회수.
- VSS 전용 Windows 권한 서비스와 명시적 설치 도구. 실제 설치·반복 UAC 0의 자격은 [UA 검증](docs/verification/storage-privilege-service.md)을 따른다.
- 선택적 ReFS 블록 복제 backend. 최초 기준본 복사가 필요하므로 NTFS의 전체 복제 금지 요구를 충족하는 대체 경로는 아니다.
- 불변 후보의 같은 요청 게시 재시도·등록 증거에 따른 Git 포인터 분리·공통 Library 검증 view와 replay/write probe는 개발 도구다. 제품 기본 모드나 자동 조정 기능이 아니다.
- [개발용 준비 중단 probe/harness](docs/ntfs-workspaces.md#개발용-준비-중단-시험)는 정확한 CLI 종료 뒤 같은 요청의 준비 재개·독립 결과·회수를 시험한다. 일반 workspace 명령에는 gate를 활성화하지 않는다.
- [제품 검증 workflow](docs/validation-workflow.md)의 `validation` CLI: 실제 Windows/Git/Unity 어댑터, 독립 VSS 후보·불변 snapshot Library와 공통 cache·소스 세대, 정지 cache 정리·독립 결과와 snapshot 회수 의존성 검사. [작은 cold/기존 Library 실행](docs/verification/product-validation-unelevated.md)에서 재사용·정리·재증가와 최종 회수를 확인했으며 기본 모드 자동 선택은 없다.

현재 지원은 작은 fixture의 내부 알파 범위다. 등록한 프로젝트/home에서 제한 서비스와 일반 권한 Unity A/B/A·Library 재사용/정리·재연결·독립 결과/회수 및 추가 UAC 관측 0을 확인했다. 일반 에셋·파일 지원, 실제 재부팅·전원·공간 장애, ARM A/B와 대형 프로젝트의 용량 목표는 미완료다. 제품 전체 설치·대화 자동 연결은 미구현이며 실제 discard 자격은 [폐기 검증](docs/verification/workspace-discard.md)을 따른다. daemon/DB는 제품 범위에서 제외한다. 현재 계약은 [기반 구조](docs/foundation.md), 실행 중 복구와 지원 경계는 [검증 안내](docs/verification/README.md)를 따른다.

## 실행

.NET SDK `10.0.112` 호환 패치, Git, PowerShell 7.2 이상이 필요하다. 저장소 루트에서 실행한다.

```powershell
pwsh ./scripts/build.ps1
pwsh ./scripts/splinter.ps1 capabilities
pwsh ./scripts/splinter.ps1 schema workspace.ensure
pwsh ./scripts/splinter.ps1 doctor --project ../splinter-unity
pwsh ./scripts/splinter.ps1 workspace ensure --project ../splinter-unity --key <task-key> --storage ntfs-snapshot --base development --home <NTFS-home>
```

CLI는 stdout에 JSON 응답 하나를 반환한다. VSS 작업은 [제한 권한 서비스](docs/storage-privilege-service.md)를 사용하며, 서비스 부재나 미등록 범위는 설치/구성 필요 상태를 반환한다. [NTFS 사용법](docs/ntfs-workspaces.md)을 먼저 읽고 반환된 작업공간 경로에서 편집한다. 일반 CLI·Git·Unity 전체를 관리자 권한으로 실행하지 않는다.

각 대화의 에이전트는 [시작 안내](docs/agent-start.md)와 [스킬 원본](skills/splinter/SKILL.md)을 읽고 시작한다. 프로젝트 정책에 지정한 CLI·등록 project/home을 사용하고, 응답의 `data.workspace.path`에서 편집한다. 같은 작업은 key/id/home·기준·request digest를 대화나 인계에 남겨 같은 ensure 입력으로 재개한다. 현재 경로에서 독립 실행 없이 작업할 수 있으면 새 workspace를 만들지 않는다.

`main`은 출시용, `development`는 개발용이다. 형제 `../splinter-unity`는 별도 Git 저장소이며 submodule이 아니다. 실제 Git 등록 시험은 `pwsh ./scripts/test-worktree-lifecycle.ps1 -Repository ../splinter-unity`로 실행한다. 이 시험은 Unity Editor·CoW 시험과 구별한다.

## 문서

- [문서 지도](docs/README.md): 현재 기능별 계약과 사용법.
- [제품 요구사항](plan.html): 제품이 달성해야 할 목표.
- [구현 계획](docs/plans/implementation-plan.md): 목표 구조와 남은 작업.
- [저장공간 우선 설계](docs/plans/storage-priority-and-editor-modes.md): 용량·준비·재개 비용과 Editor 실행 방식의 판단 기준.
- [검증 추적표](docs/plans/validation-matrix.md): 첫 버전 M0와 장기 지원·확장 카탈로그.

배포 전에는 현재 데이터 계약만 지원한다. 지원하지 않는 형식은 원본과 결과를 보존한 채 명시적으로 거부하며 자동 이관하지 않는다. 변경 이력은 Git에서 확인한다.
