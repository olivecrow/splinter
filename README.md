# Splinter

AI 에이전트가 작업별로 독립 개발 환경을 확보하고 수정·실행·검증·결과 회수까지 운영하는 로컬 프로그램이다. 첫 지원 목표는 Windows + ReFS + Git + Unity이며, CLI가 기본 인터페이스다.

주 사용자는 **AI 에이전트**다. 사람은 개발 목표와 최초 운영 정책을 정하고, 반복적인 작업공간·프로세스·검증·정리는 프로그램과 에이전트가 처리하도록 설계한다.

## 현재 상태

현재는 기반 구현 단계다. .NET 10 solution, JSON 계약, 명령 탐색, Git/Unity 메타데이터 진단, 계획의 스키마·의존성 검증과 자동 시험을 실행할 수 있다. 지속 Host·실제 작업 실행·ReFS CoW·Unity 브리지는 다음 단계다.

제품과 테스트 프로젝트는 별도 Git 저장소다. 형제 `../splinter-unity`는 Unity `6000.3.22f1` 프로젝트이며 submodule이 아니다. Library와 생성 파일은 그 저장소의 Git 추적에서 제외한다.

`main`은 출시용, `development`는 개발용 브랜치다. 일반 개발은 `development`에서 진행한다. Unity 테스트 저장소의 브랜치와 시험 상태는 에이전트가 필요에 맞게 관리한다. 자세한 기준은 [저장소와 브랜치 운영](docs/foundation.md#저장소와-브랜치-운영)을 따른다.

## 시작

.NET SDK `10.0.112` 호환 패치, Git, PowerShell 7.2 이상이 필요하다. 저장소 루트에서:

```powershell
pwsh ./scripts/build.ps1
dotnet ./src/Splinter.Cli/bin/Release/net10.0/splinter.dll capabilities
dotnet ./src/Splinter.Cli/bin/Release/net10.0/splinter.dll schema plan.validate
dotnet ./src/Splinter.Cli/bin/Release/net10.0/splinter.dll doctor --project ../splinter-unity
dotnet ./src/Splinter.Cli/bin/Release/net10.0/splinter.dll plan validate --input ./contracts/examples/parallel-plan.json
```

CLI는 기본적으로 비대화형이며 stdout에 JSON 응답 하나를 출력한다. `--help`도 명령 계약을 JSON으로 반환한다. `--human`은 들여쓰기 옵션이다. 진단과 계획 검증의 성공은 실제 작업 실행 준비 완료를 뜻하지 않는다.

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
