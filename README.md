# Splinter

AI 에이전트가 작업별로 독립 개발 환경을 확보하고 수정·실행·검증·결과 회수까지 운영하는 로컬 프로그램이다. 첫 지원 목표는 **기존 Windows/NTFS + Git + Unity**이며, CLI가 기본 인터페이스다. 원본 이동·최초 전체 복제 없이 작업 시작 시점의 스냅샷과 작업별 변경분을 사용한다. 원본은 병렬 작업 중에도 계속 수정할 수 있어야 한다.

주 사용자는 **AI 에이전트**다. 사람은 개발 목표와 최초 운영 정책을 정하고, 반복적인 작업공간·프로세스·검증·정리는 프로그램과 에이전트가 처리하도록 설계한다.

## 현재 상태

NTFS의 VSS 스냅샷과 WinFsp 범위 저장은 [독립 저장소 실험](docs/snapshot-overlay.md)으로 구현했다. [SO 실행 기록](docs/verification/snapshot-overlay.md)에 SO-01~06의 합성 파일·실제 Git/Unity·장애 재개·결과 보존·회수 증거가 있다. 최초 payload 복사와 읽기 hydration은 0이며 원본/A/B가 서로 다른 코드를 실행했다. 제품 workspace backend 통합과 재부팅 복구는 미검증이다.

**현재 ReFS 내부 알파는 위 저장소 요구사항을 아직 충족하지 않는다.** 최초 전체 기준본 준비를 요구하며 약 297MiB는 준비 이후 작업공간 하나의 증가량이다. 다음 작업은 [NTFS 실험의 제품 통합 조건](docs/plans/ntfs-snapshot-overlay-spike.md)을 따른다. ReFS는 선택적 최적화 후보이며 필수 설치·원본 이관 경로가 아니다.

현재는 **간단한 Unity 스크립트 병렬 편집을 위한 내부 알파**다. .NET 10 CLI가 atomic JSON 상태와 operation lock을 사용한다. 저장소 절감을 위해 예열 기준본·ReFS CoW·무-checkout Git index를 연결하고, 작업별 Windows 프로세스 그룹으로 Editor와 자식의 수명을 관리한다. 코드와 실제 환경별 검증 범위는 [저장소·자원 관리](docs/storage-and-resources.md)와 [자원 검증](docs/verification/storage-and-resources.md)을 따른다. 기존 두 작업자의 일반 checkout 편집·컴파일·재개 증거는 [알파 검증](docs/verification/internal-alpha.md)에 있다. daemon/SQLite Host는 미구현이다.

제품과 테스트 프로젝트는 별도 Git 저장소다. 형제 `../splinter-unity`는 Unity `6000.3.22f1` 프로젝트이며 submodule이 아니다. Library와 생성 파일은 그 저장소의 Git 추적에서 제외한다.

`main`은 출시용, `development`는 개발용 브랜치다. 일반 개발은 `development`에서 진행한다. Unity 테스트 저장소의 브랜치와 시험 상태는 에이전트가 필요에 맞게 관리한다. 자세한 기준은 [저장소와 브랜치 운영](docs/foundation.md#저장소와-브랜치-운영)을 따른다.

## 현재 ReFS 알파의 진단 명령

.NET SDK `10.0.112` 호환 패치, Git, PowerShell 7.2 이상이 필요하다. 저장소 루트에서:

```powershell
pwsh ./scripts/build.ps1
pwsh ./scripts/splinter.ps1 capabilities
pwsh ./scripts/splinter.ps1 schema workspace.ensure
pwsh ./scripts/splinter.ps1 doctor --project ../splinter-unity
pwsh ./scripts/splinter.ps1 storage probe --path <ReFS-home>
pwsh ./scripts/splinter.ps1 baseline prepare --project ../splinter-unity --base development --home <ReFS-home>
```

CLI는 기본적으로 비대화형이며 stdout에 JSON 응답 하나를 출력한다. `--help`도 명령 계약을 JSON으로 반환한다. 기준본은 한 번 검증·봉인한 뒤 작업마다 `workspace ensure --storage refs-cow --baseline <id>`로 재사용한다. [기준본 준비 절차](docs/storage-and-resources.md)를 먼저 따른다. 진단 성공은 실행 준비 완료를 뜻하지 않는다. 작업은 **반환된 작업공간 경로에서만** 편집한다. 재개·검증·통합·회수는 [알파 작업공간 사용법](docs/alpha-workspaces.md)을 따른다. 대화나 CLI 종료만으로 Editor와 작업공간을 회수하지 않는다.

Git 수명주기 시험은 최초 커밋이 있는 저장소에 실행한다.

```powershell
pwsh ./scripts/test-worktree-lifecycle.ps1 -Repository ../splinter-unity
```

이 개발용 스크립트는 실제 worktree 두 개의 등록·변경 격리·회수를 검사한다. 일반 Git checkout을 사용하며 Unity Editor와 CoW는 실행하지 않는다. 생성되는 증거는 `artifacts/`에 보관한다.

## 읽는 순서

1. [현재 기반](docs/foundation.md): 구현된 모듈·명령·계약·검증 방법과 제한.
2. [원본 기획서](plan.html): 제품 요구사항과 첫 공개 버전의 범위.
3. [구현 계획](docs/plans/implementation-plan.md): 목표 구조와 단계별 구현 작업.
4. [검증 추적표](docs/plans/validation-matrix.md): 기획서의 필수 시나리오 48개와 추가 기반 검증의 담당 단계·증거.
5. [문서 지도](docs/README.md): 기능별 문서 진입점.

계획 문서는 `docs/plans/`에 두며, 구현된 기능의 현재 계약과 구별한다. 지원 여부는 해당 환경에서 확보한 실행 증거를 근거로 기록한다.
