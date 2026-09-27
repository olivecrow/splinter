# Splinter

AI 에이전트가 작업별로 독립 개발 환경을 확보하고 수정·실행·검증·결과 보존·회수를 수행하는 로컬 프로그램이다. 첫 지원 목표는 **Windows/NTFS + Git + Unity**다. 원본 이동·최초 전체 복제·읽기 hydration 없이 불변 snapshot과 작업별 범위 변경분을 사용하며, 원본은 작업 중 계속 수정할 수 있다.

각 대화가 필요할 때만 자신의 작업공간을 사용한다. 같은 작업은 여러 대화와 CLI 실행에 걸쳐 유지하며, 대화나 Editor 종료는 반영·회수·폐기의 승인이 아니다.

## 현재 제공하는 기능

- .NET CLI, atomic JSON 상태와 작업 잠금, 공통 Core 계약 및 fake 적합성 시험.
- `ntfs-snapshot` workspace의 VSS·WinFsp 범위 저널, Git 등록, 작업별 프로세스 그룹과 Unity 검증, 독립 결과 보존과 명시적 회수.
- 변경 기반 소스 검증, 명시적 저널 정리와 선택적 중첩량 예산, snapshot 지연 회수.
- 선택적 ReFS 블록 복제 backend. 최초 기준본 복사가 필요하므로 NTFS의 전체 복제 금지 요구를 충족하는 대체 경로는 아니다.
- 불변 후보·공통 Library 검증 view와 replay/write probe는 개발 도구다. 제품 기본 모드나 자동 조정 기능이 아니다.

현재 지원은 작은 fixture의 내부 알파 범위다. 일반 에셋·파일 지원, 실제 재부팅·전원·공간 장애, ARM A/B와 대형 프로젝트의 용량 목표는 미완료다. 설치·대화 자동 연결·명시적 discard·권한 서비스·daemon/DB는 미구현이다. 현재 계약은 [기반 구조](docs/foundation.md), 지원 경계와 다음 검증은 [검증 안내](docs/verification/README.md)를 따른다.

## 실행

.NET SDK `10.0.112` 호환 패치, Git, PowerShell 7.2 이상이 필요하다. 저장소 루트에서 실행한다.

```powershell
pwsh ./scripts/build.ps1
pwsh ./scripts/splinter.ps1 capabilities
pwsh ./scripts/splinter.ps1 schema workspace.ensure
pwsh ./scripts/splinter.ps1 doctor --project ../splinter-unity
pwsh ./scripts/splinter.ps1 workspace ensure --project ../splinter-unity --key <task-key> --storage ntfs-snapshot --base development --home <NTFS-home>
```

CLI는 stdout에 JSON 응답 하나를 반환한다. 권한이 필요한 VSS 작업은 정확한 helper와 같은 작업의 재시도 절차를 반환한다. [NTFS 사용법](docs/ntfs-workspaces.md)을 먼저 읽고 반환된 작업공간 경로에서 편집한다. 일반 CLI·Git·Unity 전체를 관리자 권한으로 실행하지 않는다.

`main`은 출시용, `development`는 개발용이다. 형제 `../splinter-unity`는 별도 Git 저장소이며 submodule이 아니다. 실제 Git 등록 시험은 `pwsh ./scripts/test-worktree-lifecycle.ps1 -Repository ../splinter-unity`로 실행한다. 이 시험은 Unity Editor·CoW 시험과 구별한다.

## 문서

- [문서 지도](docs/README.md): 현재 기능별 계약과 사용법.
- [제품 요구사항](plan.html): 제품이 달성해야 할 목표.
- [구현 계획](docs/plans/implementation-plan.md): 목표 구조와 남은 작업.
- [저장공간 우선 설계](docs/plans/storage-priority-and-editor-modes.md): 용량·준비·재개 비용과 Editor 실행 방식의 판단 기준.
- [검증 추적표](docs/plans/validation-matrix.md): 필수 시나리오와 미완료 관문.

배포 전에는 현재 데이터 계약만 지원한다. 지원하지 않는 형식은 원본과 결과를 보존한 채 명시적으로 거부하며 자동 이관하지 않는다. 변경 이력은 Git에서 확인한다.
