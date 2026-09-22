# Splinter

AI 에이전트가 작업별로 독립 개발 환경을 확보하고 수정·실행·검증·결과 회수까지 운영하는 로컬 프로그램이다. 첫 지원 목표는 **기존 Windows/NTFS + Git + Unity**이며, CLI가 기본 인터페이스다. 원본 이동·최초 전체 복제 없이 작업 시작 시점의 스냅샷과 작업별 변경분을 사용한다. 원본은 병렬 작업 중에도 계속 수정할 수 있어야 한다.

주 사용자는 **AI 에이전트**다. 사람은 개발 목표와 최초 운영 정책을 정하고, 반복적인 작업공간·프로세스·검증·정리는 프로그램과 에이전트가 처리하도록 설계한다.

## 현재 상태

일반 `workspace ensure --storage ntfs-snapshot`에 VSS 스냅샷·WinFsp 범위 저널·Git 등록·독립 결과 보존·회수를 연결했다. [NTFS 사용법](docs/ntfs-workspaces.md)과 [NI 검증 기록](docs/verification/ntfs-workspace-integration.md)을 따른다. 실제 NTFS 생성·저장량·두 Unity Editor의 격리·재연결·실패 보호를 검증했다. 작업 프로세스·mount·Git 등록·delta·소유 VSS까지 회수를 완료했다.

별도 [SO 실행 기록](docs/verification/snapshot-overlay.md)에는 SO-01~06의 합성 파일·실제 Git/Unity·장애·회수 증거가 있다. 이 실험의 최초 payload 복사·읽기 hydration 0과 원본/A/B 격리는 제품 NI 통과로 승계하지 않는다. **ReFS 경로는 최초 전체 기준본 준비를 요구하므로 위 저장소 요구사항을 충족하지 않으며**, 선택적 경로로 유지한다. NTFS 실패의 이관·전체 복사 fallback이 아니다.

완료한 구현 단위는 [NTFS workspace 1차 통합](docs/plans/ntfs-workspace-integration.md)이다. NI-01~08의 1차 내부 알파 범위를 검증했다. 실제 원본의 미커밋 변경이 있으면 통합을 거부하고 HEAD·인덱스·변경 내용·독립 결과를 보존하는 것까지 확인했다. 일반 에셋·재부팅·공개 제품 전체 지원은 후속 범위다. 기존 Unity fixture와 합성 파일을 사용하고 구매 에셋은 요구하지 않는다.

상위 작업은 [NTFS 내부 알파 2차](docs/plans/ntfs-recovery-and-support.md)의 NR-01~08이다. 공통 복구·상태/저널 보존·파일 속성 거부·VSS 해제 관측과 [live VSS 식별·재연결](docs/verification/ntfs-live-vss-identity.md)을 구현했다. 새 로컬 후보의 자동 시험은 180 통과·0 실패·선택적 ReFS 1 skip이다. 이전 VIVOBOOK 후보의 139개 자동 통과·ARM Windows/x64 Unity 단일 작업·5개 파일 API 거부는 [NR 기록](docs/verification/ntfs-recovery.md)에 별도 보존한다.

[살아 있는 VSS 식별과 같은 작업 재개](docs/plans/ntfs-live-vss-identity.md)의 LV-01~08을 정한 시험 수준에서 완료했다. 캡처 identity·현재 읽기 위치 분리, 비관리자 Observe/재개, legacy 보존과 실제 VSS/Git 재연결·범위 쓰기·단일 Unity 검증 3건·독립 결과·최종 회수를 확인했다. 다음은 나머지 중단·파일 경계와 격리 환경의 재부팅·전원·공간 장애, ARM A/B다. NR 전체나 공개 제품 지원 완료가 아니다.

현재는 **간단한 Unity 스크립트 병렬 편집을 위한 내부 알파**다. .NET 10 CLI가 atomic JSON 상태와 operation lock을 사용한다. NTFS 작업은 불변 기준과 범위 저널을, 선택적 ReFS 작업은 예열 기준본과 블록 복제를 사용한다. 무-checkout Git index와 작업별 Windows 프로세스 그룹으로 소스·Editor·자식 수명을 관리한다. 기존 환경별 증거는 [자원 검증](docs/verification/storage-and-resources.md), 두 작업자의 일반 checkout 증거는 [알파 검증](docs/verification/internal-alpha.md)에 있다. daemon/SQLite Host는 미구현이다.

제품과 테스트 프로젝트는 별도 Git 저장소다. 형제 `../splinter-unity`는 Unity `6000.3.22f1` 프로젝트이며 submodule이 아니다. Library와 생성 파일은 그 저장소의 Git 추적에서 제외한다.

`main`은 출시용, `development`는 개발용 브랜치다. 일반 개발은 `development`에서 진행한다. Unity 테스트 저장소의 브랜치와 시험 상태는 에이전트가 필요에 맞게 관리한다. 자세한 기준은 [저장소와 브랜치 운영](docs/foundation.md#저장소와-브랜치-운영)을 따른다.

## 진단과 NTFS 진입점

.NET SDK `10.0.112` 호환 패치, Git, PowerShell 7.2 이상이 필요하다. 저장소 루트에서:

```powershell
pwsh ./scripts/build.ps1
pwsh ./scripts/splinter.ps1 capabilities
pwsh ./scripts/splinter.ps1 schema workspace.ensure
pwsh ./scripts/splinter.ps1 doctor --project ../splinter-unity
pwsh ./scripts/splinter.ps1 workspace ensure --project ../splinter-unity --key <task-key> --storage ntfs-snapshot --base development --home <NTFS-home>
```

CLI는 기본적으로 비대화형이며 stdout에 JSON 응답 하나를 출력한다. `--help`도 명령 계약을 JSON으로 반환한다. NTFS 캡처/해제에 권한이 부족하면 정확한 관리자 helper와 재시도 행동을 반환한다. [NTFS 절차](docs/ntfs-workspaces.md)를 먼저 따른다. ReFS를 명시적으로 사용할 때만 [기준본 준비](docs/storage-and-resources.md)가 필요하다. 진단 성공은 실행 준비 완료를 뜻하지 않는다. 작업은 **반환된 작업공간 경로에서만** 편집한다. 대화나 CLI 종료만으로 Editor와 작업공간을 회수하지 않는다.

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
