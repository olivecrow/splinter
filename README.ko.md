# Splinter

**Windows에서 병렬 개발을 위한 독립 작업공간.**

[English](README.md)

Splinter는 작업마다 쓰기 가능한 프로젝트 경로를 제공합니다. 각 작업은 고정된 프로젝트 기준본을 공유하고 변경된 바이트 범위만 따로 저장합니다. 원본은 계속 편집할 수 있고, 대화가 바뀌어도 같은 작업공간에서 이어서 작업할 수 있습니다. Unity에서는 작업공간별로 Editor를 실행할 수 있습니다.

## 동작 방식

WinFsp 마운트는 공통 기준본과 작업별 변경을 하나의 프로젝트 트리로 보여 줍니다. 프로젝트 전체를 복제하거나 읽은 파일을 디스크에 따로 쌓지 않으며, 파일의 일부만 바꿀 때 파일 전체를 복사하지 않습니다. Unity 작업은 미리 준비한 `Library` 기준본에서 변경되지 않은 데이터도 공유할 수 있습니다.

1. **작업 시작 또는 재개.** 고정된 작업 키를 사용하고 Splinter가 반환한 경로에서 편집합니다. Unity에서는 필요할 때 해당 작업공간의 Editor를 실행합니다.
2. **체크포인트.** 특정 시점의 소스 결과를 보존하고 같은 작업공간에서 편집을 이어갑니다. 컴파일, Play Mode, 테스트, 빌드에는 기존 도구를 사용합니다.
3. **작업 종료.** 검토한 결과를 원본에 반영하거나, 결과를 남긴 채 작업공간 자원을 회수하거나, 작업을 폐기합니다. 반영할 때는 현재 원본과 충돌하는 변경이 있는지 확인합니다.

## 프로젝트 소스

| 소스 | 저장 모드 | 기준본 |
| --- | --- | --- |
| [Git](docs/git-project-basis.md) | `git-snapshot` | 지원 조건을 충족하는 깨끗한 저장소의 고정 로컬 커밋 |
| [Unity Version Control / Plastic SCM](docs/plastic-project-basis.md) | `plastic-snapshot` | 지원되는 설치 클라이언트로 읽는 기록된 변경 집합 |
| [버전 관리가 없는 폴더](docs/plain-project-basis.md) | `project-snapshot` | 원본 NTFS 볼륨에서 준비한 관리형 기준본 |

Git과 Plastic 작업에는 [별도로 준비한 Unity Library](docs/git-library-reference.md)를 연결할 수 있습니다. 관리형 프로젝트와 Library를 준비할 때는 같은 NTFS 볼륨 안에서 기존 데이터를 옮기고, 원래 경로에서 쓸 수 있는 뷰를 제공합니다. Unity 프로젝트를 준비하거나 최종 복원할 때는 원본 Editor에서 저장한 뒤 정상 종료해야 합니다.

## 빠른 시작

Windows와 NTFS, WinFsp, PowerShell 7.2 이상, [global.json에 지정된 .NET SDK](global.json)가 필요합니다. Git, Plastic, Unity는 프로젝트에 따라 설치합니다.

Splinter 소스 체크아웃에서 CLI를 빌드하고 작업공간 스키마를 확인합니다. 아래 예시는 깨끗한 Git 저장소의 Unity 프로젝트를 사용합니다. 작업공간 홈은 원본 프로젝트 바깥으로 지정합니다.

```powershell
pwsh ./scripts/build.ps1
pwsh ./scripts/splinter.ps1 capabilities
pwsh ./scripts/splinter.ps1 schema workspace.ensure

$project = 'C:/projects/MyGame'
$workspaceHome = 'C:/sp/mygame'
$base = git -C $project rev-parse HEAD

pwsh ./scripts/splinter.ps1 workspace ensure --project $project --home $workspaceHome `
    --key lighting-pass --source git --application unity --storage git-snapshot --base $base
```

`workspace ensure`가 성공하면 `data.workspace.path`를 파일 편집과 도구 실행에 사용합니다. 작업을 재개할 때는 프로젝트, 홈, 키, 기준 리비전을 그대로 사용합니다. [시작 및 종료 안내](docs/agent-start.md), [Unity Editor 사용법](docs/alpha-workspaces.md), [에이전트 인계 절차](skills/splinter/SKILL.md)를 참고하세요.

## 현재 상태

Splinter는 Windows/NTFS용 연구 알파입니다. 검증은 특정 소규모 작업을 대상으로 했으며, 대형 프로젝트의 전체 작업 흐름, 일반적인 성능·메모리 개선, OS 장애 시 내구성은 검증 범위 밖입니다. Unity Editor마다 메모리와 임포트 비용이 따로 듭니다. 작업공간 격리는 프로젝트 저장소와 자원 소유권에 한정됩니다. [검증 범위](docs/verification/README.md)

## 문서와 라이선스

- [문서 목차](docs/README.md)에서 저장소, 원본 반영, 복구, 개발 도구를 찾아볼 수 있습니다.
- [기술 구조](docs/ko/foundation.md)에서 구성요소, 데이터 형식, 소유권 경계를 설명합니다.
- [제품 요구사항](plan.html)에서 사용 흐름과 프로젝트 범위를 정의합니다.

Splinter의 코드와 문서는 [MIT 라이선스](LICENSE)를 따릅니다. 의존성 버전과 라이선스 조건은 [외부 구성요소 고지](THIRD_PARTY_NOTICES.md)에 정리되어 있습니다.

WinFsp - Windows File System Proxy, Copyright (C) Bill Zissimopoulos. [WinFsp 소스](https://github.com/winfsp/winfsp)
