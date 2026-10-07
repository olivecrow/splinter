# Splinter

**AI 에이전트의 병렬 개발을 위한 효율적인 독립 작업공간.**

[English](README.md)

Splinter는 AI 에이전트가 Unity 프로젝트나 일반 프로젝트 폴더에서 독립적으로 작업할 수 있도록 돕는 로컬 작업공간·저장 도구입니다. 각 작업에는 별도의 쓰기 가능한 프로젝트 경로를 제공합니다. 변경하지 않은 파일은 고정한 프로젝트 기준본에서 읽고, 편집한 내용은 작업별 비공개 바이트 범위로 저장합니다. 여러 작업을 동시에 진행하면서 원본도 계속 수정할 수 있고, 결과의 보존과 반영은 명시적으로 수행합니다.

**현재 상태:** Windows와 NTFS를 대상으로 하는 내부 알파입니다. 작은 Git·Unity Version Control / Plastic SCM·VCS 없는 프로젝트에서 범위를 정해 검증했습니다. 실제 입력과 지원 한계는 [검증 안내](docs/ko/verification/README.md)를 참고하세요.

## Splinter가 필요한 이유

병렬 개발에서 작업마다 프로젝트 전체와 생성 데이터를 복제하고 독립 애플리케이션을 실행하면 비용이 커집니다. Unity에서는 소스만 분리해도 에셋과 `Library`, 여러 Editor의 부담이 남습니다.

Splinter는 작업이 재사용할 데이터와 변경할 데이터를 분리합니다. 범위 저널을 사용하므로 최초 프로젝트 전체 복제, 읽은 파일을 디스크에 복제하는 캐시, 일부 수정 때문에 기준 파일 전체를 복사하는 처리를 피합니다. 명시적으로 준비한 Library 기준본을 연결하면 변경하지 않은 생성 데이터도 참조하고, 각 작업의 쓰기는 별도로 저장할 수 있습니다.

기본 사용 흐름은 간단합니다. 독립 실행이 필요할 때 작업공간을 만들고, 여러 대화에 걸쳐 같은 작업을 이어간 뒤, 명시적으로 보존·반영·폐기합니다. 에이전트는 Splinter가 반환한 경로에서 기존 파일 도구·Git 도구·애플리케이션 도구로 작업합니다.

## 실제 측정한 저장 공간과 시간의 트레이드 오프

같은 Unity 프로젝트에서 **실제 Codex 앱 워크트리 두 개와 Splinter 작업공간 두 개**를 비교했습니다. 입력은 추적 파일 1,602개, 소스 약 2.8 GB, Unity 6000.3.22f1입니다. 각 방식에서 독립 Editor 두 개를 실행하고 동일한 A/B 코드 변경을 수행했습니다. Codex는 워크트리마다 새 Library를 생성했고, Splinter는 `git-snapshot`, 기존 원본 Library 기준본, 기본 SJL4 저널 최적화를 사용했습니다.

소스 작업은 의도적으로 작게 제한했습니다. A는 C# 파일 하나에서 `value + 1`을 `value + 2`로, B는 다른 파일에서 `value + value`를 `value + value + value`로 바꿨습니다. 결과마다 변경한 줄은 한 줄입니다. 아래 보존·회수 구간은 편집·컴파일·Editor 종료 이후부터이며, 이 작은 결과를 보존하고 자원을 회수하는 부가 비용입니다. **측정한 지연은 이 입력에서 대화형 개발의 실사용을 가로막는 성능 문제입니다.**

![Unity 작업공간 두 개의 추가 파일 할당: Codex 10.52 GiB, Splinter 0.14 GiB](docs/images/unity-workspace-storage.png)

두 변경의 컴파일을 확인한 시점의 추가 파일 할당은 **Codex 10.52 GiB, Splinter 0.14 GiB로, 98.65% 감소**했습니다. 작업 파일과 소유 Git 등록을 포함하고, Splinter의 공유 runner와 Library 메타데이터는 한 번만 합산했습니다. 기존 원본 소스·Library 본문·공유 Git 객체는 제외했습니다. 특정 시점의 실측값이며 디스크 최대 사용량은 아닙니다.

**최적화 후 같은 Unity 프로젝트를 다시 측정했습니다.** 초기 Editor 준비는 **29분 13초 → 17분 22초**, 한 줄 수정 후 컴파일은 **9분 14초 → 1분 17초**, A/B checkpoint·release는 **2시간 36분 7초 → 7분 1초**였습니다. 현재 A/B checkpoint는 각각 83.5초·81.6초, release는 130.6초·125.6초입니다.

![같은 Unity 입력의 이전·현재 시간과 과거 Codex 비교](docs/images/unity-lifecycle-remeasurement.png)

Library 준비·복원을 모두 포함한 이번 관측 구간은 **68분 28초**이며, 마지막 Library 복원만 **23분 25초**가 걸렸습니다. 종료 명령 두 개의 2분 시간 제한 뒤 같은 Editor의 정상 종료를 관측해 계속했습니다. 이전 보존 구간에는 중첩·복구·동시 진단이 있었고 현재는 A→B 순차 실행이므로, 순수 최적화 배율로 해석하지 않습니다. 프로파일 부하를 포함한 한 번의 실측이며, Codex와 위 저장 공간 수치는 과거 비교군입니다. [정확한 비용·프로파일·실패·복원 검증](docs/ko/verification/unity-lifecycle-remeasurement.md)과 [데이터](reports/benchmarks/unity-lifecycle-remeasurement-20261007.json)를 참고하세요.

[후속 읽기·복원 최적화](docs/ko/verification/lifecycle-read-optimization.md)는 제한된 Git reader·디렉터리 메타데이터 재사용, 느린 기준 읽기의 잠금 분리와 완전히 변경 없는 Library의 검증된 디렉터리 이동을 적용했습니다. 최신 작은 NTFS 비교(260개 파일, 약 16MiB)는 고정 기록 대기 제거를 포함해 평균 **4.21초 → 0.52초**입니다. 이 값은 fixture 결과이며 위 Unity 전체 시간은 추가 변경 전의 기록입니다.

<details>
<summary>이전 시간 측정과 최적화 과정</summary>

![같은 Unity 작업의 시간 비용: Editor 준비 및 코드 변경 후 컴파일 준비](docs/images/unity-workspace-time.png)

| A/B 두 작업의 관측 시간 | Codex 워크트리 | Splinter + Library 기준본 | Splinter 추가 시간 |
| --- | ---: | ---: | ---: |
| 작업공간 생성 후 Editor 시작부터 컴파일 준비까지 | 14분 47초 | 29분 13초 | +14분 26초 |
| 코드 변경·refresh부터 컴파일 준비까지 | 19초 | 9분 14초 | +8분 55초 |

**이번 실행에서는 공간 절감과 함께 상당한 시간 비용이 발생했습니다.** Splinter는 별도로 최초 Library 기준본 준비에 4분 33초, 작업공간 두 개의 순차 생성에 12분 46초가 걸렸습니다. 이 준비 비용은 Editor 그래프에 포함하지 않았습니다. Codex 대화 생성의 전체 완료 시간은 확인할 수 없어 제외했습니다.

**결과 보존도 느렸습니다. Editor 종료 후 A의 checkpoint만 95분 23초가 걸렸으며 Editor 그래프의 범위 밖입니다.** 전체 Git attribute 검사의 반복과 store 잠금 안의 journal 파일 순회로 추가 metadata 비용이 드러났습니다. 이후 같은 50개 경로를 읽기 전용으로 조회했을 때 일반 NTFS는 0.23초, Splinter는 12.83초가 걸렸습니다. [지연 원인과 수명주기 비용](docs/ko/verification/readme-unity-comparison.md#보존-단계-지연-원인)은 이 결과와 앞선 Unity 시간을 구분합니다. 같은 경계의 Codex 전체 보존 시간은 측정하지 못했습니다.

[후속 프로파일링](docs/ko/verification/checkpoint-release-profiling.md)은 해당 기록과 작은 Editor 없는 task를 사용했습니다. checkpoint는 소스 검사를 일곱 번 반복하고, release는 보존한 결과를 재수집하며, 저널 회수에는 파일 fence의 제곱 비례 검색이 있었습니다. 이 진단에서 원래 제품을 최적화하지 않았습니다.

[이후 수명주기 최적화](docs/ko/verification/checkpoint-release-optimization.md)로 Git 속성 검사를 묶고, 응답만을 위한 소스 검사를 줄이며, 변경 없는 seal 결과를 재사용하고 회수 fence를 인덱스로 찾습니다. 같은 작은 Editor 없는 workload에서 checkpoint는 **42.30초 → 31.88초 (24.65%)**, release는 **20.66초 → 14.54초 (29.59%)**로 개선됐습니다. 작은 fixture의 측정이며 후속 실제 Unity 재측정은 UR-01–04에서 별도로 검증합니다.

[추가 최적화 CO-05–08](docs/ko/verification/checkpoint-release-optimization.md#최종-제품-측정-co-07)는 checkpoint/보존 검증을 결합하고 반복 inventory와 고정 요청 대기를 줄였습니다. 같은 작은 workload의 새 비교에서 앞서 최적화한 배포본 대비 checkpoint **28.66초 → 14.09초 (50.84% 감소)**, release **13.68초 → 9.66초 (29.39% 감소)**였습니다. 아래 대형 Unity 그래프는 과거 측정을 그대로 보여줍니다.

![Splinter의 별도 수명주기 비용(분): Library 준비 4.5, 작업공간 생성 12.8, 기록한 A/B 보존·회수 156.1, Library 복원 24.6](docs/images/unity-workspace-lifecycle.png)

기록한 보존·회수 순서는 **2시간 36분 7초**가 걸렸습니다. B 작업의 겹침, 보존한 `WorkspaceBusy` orchestration 거부·복구, 동시 진단을 포함합니다. 위 A checkpoint도 포함하므로 구간을 중복 합산하지 않습니다. 최종 Library 복원에는 **24분 33초**가 걸렸습니다. Splinter 두 결과를 보존하고 소유 자원을 회수했으며, 원본 Library 복원과 원본 소스 1,602개 전부의 바이트 불변을 확인했습니다. 수명주기 막대는 기록한 Splinter 실행 순서이며, 같은 경계의 Codex 전체 비용은 확인하지 못했습니다.

Windows/NTFS에서 방식별 한 묶음만 측정했고, OS 캐시를 비우지 않은 채 Codex 다음 Splinter 순서로 실행했습니다. 선택한 코드의 컴파일·에셋 로딩·렌더링을 확인했으며 Play Mode·테스트·게임 빌드는 실행하지 않았습니다. 일반적인 속도·RAM·최대 저장 공간 절감을 입증하는 결과는 아닙니다. [측정 방법·정확한 수치·실패·정리의 검증 범위](docs/ko/verification/readme-unity-comparison.md), [측정 데이터](reports/benchmarks/unity-workspace-comparison-20261007.json), [그래프 생성 스크립트](scripts/plot-unity-workspace-comparison.py)를 참고하세요.

</details>

## 동작 원리

**기준본**은 작업을 시작할 때 고정한 데이터입니다. **비공개 저널**은 해당 작업이 바꾼 바이트 범위와 파일 메타데이터를 기록합니다. 두 데이터를 조합한 프로젝트 트리를 쓰기 가능한 WinFsp 파일시스템으로 제공합니다.

```text
고정한 프로젝트 기준본
  + 작업 A의 비공개 변경분  ->  작업공간 A
  + 작업 B의 비공개 변경분  ->  작업공간 B
```

A의 편집은 B의 파일이나 원본을 자동으로 변경하지 않습니다. checkpoint는 안정된 소스를 독립 결과로 보존하고, integrate는 선택한 소스 어댑터의 보호 조건에 따라 그 결과를 반영합니다. release는 결과를 유지하며 해당 작업이 소유한 자원을 회수합니다. discard는 별도의 명시적 폐기입니다.

작업 중에도 원본을 사용할 수 있습니다. 선택적인 관리형 Library 또는 VCS 없는 프로젝트 기준본은 기존 데이터를 같은 NTFS 볼륨에서 복사 없이 이동하고, 원래 경로에 쓰기 가능한 view를 제공합니다. 최초 전환과 최종 복원에는 원본 Editor의 저장과 정상 종료가 필요합니다. 복원은 원본의 현재 내용을 일반 NTFS 디렉터리로 되돌립니다.

## 주요 기능

- **작업별 독립 공간.** 쓰기 가능한 경로·변경분·작업 상태·소유 애플리케이션 프로세스를 분리하고, 원본과 다른 작업을 보호합니다.
- **변경량에 따른 저장.** 불변 데이터 참조, 제한된 크기의 raw/Brotli payload, 저널 정리를 제공합니다. 새 작업은 현재 변경분만 유지하는 저널 최적화를 기본 사용하며, 명시적 정리로 기준본과 다시 같아진 범위도 제거할 수 있습니다.
- **대화 이후에도 이어지는 작업.** 고정한 작업 식별자·원자적 JSON 상태·같은 요청의 복구를 통해 중단이나 응답 유실 뒤 작업을 이어갑니다.
- **독립 결과와 보호 조건을 갖춘 반영.** 소스 checkpoint, Git 결과 통합, 지원하는 일반 폴더·기록된 Plastic 경로의 전체/선택 파일 반영을 제공합니다. 대상 변경이나 충돌은 거부하고 명시적으로 해결하도록 합니다.
- **명시적 자원 소유권.** Unity Editor 시작/종료, provider·mount 추적, 의존성 보호, 자기 자원의 회수·폐기를 관리합니다.
- **에이전트를 위한 인터페이스.** stdout의 단일 JSON 응답, capabilities·명령 schema·구조화된 오류와 저장소에 포함한 [에이전트 스킬](skills/splinter/SKILL.md)을 제공합니다.

Splinter는 작업공간 저장과 자원 수명을 담당합니다. 편집·refresh·컴파일·Play Mode·테스트·빌드·결과 판정은 에이전트와 선택한 도구가 수행합니다. checkpoint는 소스의 안정성을 확인하며, 컴파일·테스트 결과는 외부 도구에서 확인합니다. MCP와 Unity CLI는 에이전트가 필요에 따라 선택하는 외부 연결입니다.

## 저장 방식

소스 종류는 `git`, `plastic`, `none`으로 명시합니다. 실제 프로젝트에 맞는 저장 방식을 선택하고 생성 전에 해당 경로의 전제를 확인하세요.

| 저장 방식 | 프로젝트 기준본 | 준비 조건 |
| --- | --- | --- |
| [`git-snapshot`](docs/ko/git-project-basis.md) | 고정한 로컬 Git commit·tree·blob | 요청 commit과 일치하는 지원 가능한 clean 저장소, WinFsp. VSS 서비스는 필요하지 않습니다. |
| [`plastic-snapshot`](docs/ko/plastic-project-basis.md) | 기록된 고정 Plastic changeset·파일 revision | 검증한 설치 버전의 Plastic client를 통한 읽기 접근, WinFsp. 새 Plastic 작업공간 등록이나 VSS 서비스는 필요하지 않습니다. |
| [`project-snapshot`](docs/ko/plain-project-basis.md) | 생성 데이터를 포함한 VCS 없는 프로젝트의 관리형 기준본 | 명시적 `project prepare`, 같은 NTFS 볼륨 내 이동, 원본 경로의 쓰기 가능한 view. VSS 서비스는 필요하지 않습니다. |
| [`ntfs-snapshot`](docs/ko/ntfs-workspaces.md) | Git·Plastic·VCS 없는 프로젝트의 VSS snapshot | WinFsp, 정확한 project/home identity가 등록된 제한 권한 서비스. |

Git·Plastic 작업에는 명시적으로 준비한 [Library 기준본](docs/ko/git-library-reference.md)을 연결할 수 있습니다. Git commit 기준본은 커밋한 원시 바이트만 포함하고 ignored 파일은 제외합니다. checkout filter·LFS·특수 파일은 해당 경로의 입력 제한을 확인해야 합니다. 기록된 Plastic reader는 현재 client `11.0.16.9915`가 필요합니다.

## 시작하기

현재 진입점은 소스 checkout과 PowerShell CLI wrapper입니다.

### 요구 환경

- 위 작업공간 저장 경로를 위한 Windows와 NTFS.
- PowerShell 7.2 이상.
- .NET SDK `10.0.112` 또는 [global.json](global.json)이 허용하는 호환 패치.
- mount 작업공간을 위한 [WinFsp](https://github.com/winfsp/winfsp). .NET binding은 `2.2.26215`로 고정되어 있습니다.
- Git 소스에는 Git, Plastic 소스에는 검증한 설치 버전의 Plastic client. Unity는 Unity 프로젝트·Editor를 사용할 때 필요합니다.
- `ntfs-snapshot` 경로에만 [제한 권한 서비스](docs/ko/storage-privilege-service.md)와 등록된 접근 범위.

Splinter 저장소 루트에서 실행합니다.

```powershell
# 고정 의존성 restore, Release 빌드, 자동 시험.
pwsh ./scripts/build.ps1

# 새 명령을 실행하기 전에 인터페이스를 확인합니다.
pwsh ./scripts/splinter.ps1 capabilities
pwsh ./scripts/splinter.ps1 schema workspace.ensure
```

첫 Git 작업공간은 Editor 없이 시작할 수 있습니다. 지원 조건을 만족하는 clean 저장소에서 현재 HEAD와 선택한 기준이 일치해야 합니다. 아래 값은 실제 프로젝트·기존 기준 branch·원본과 VCS 관리 트리 밖의 NTFS home으로 바꾸세요.

```powershell
$project = 'C:/Projects/MyProject'
$workspaceHome = 'C:/Splinter/MyProject'
$taskKey = 'feature-a'
$base = 'development'

pwsh ./scripts/splinter.ps1 doctor --project $project

$reply = (& pwsh ./scripts/splinter.ps1 workspace ensure `
    --project $project --key $taskKey --base $base `
    --source git --application none --storage git-snapshot `
    --home $workspaceHome) | ConvertFrom-Json
$reply
```

`status`, `error`, `nextActions`를 확인하세요. 요청 성공과 실제 준비 상태를 확인한 뒤 **`data.workspace.path`**를 편집 디렉터리와 파일·애플리케이션 도구의 대상으로 사용합니다. `doctor`는 메타데이터 진단이며 실제 작업공간 실행을 보증하지 않습니다.

Unity 작업은 최초 요청에 `--application unity`를 지정하고 [AI 시작 안내](docs/ko/agent-start.md)와 [Unity 사용 절차](docs/ko/alpha-workspaces.md)를 따릅니다. 일반 CLI·Git·Unity는 일반 사용자 권한으로 실행합니다. 서비스 설치와 기준본 준비에는 각각 명시적 준비 절차가 있습니다.

### 작업을 이어가고 마무리하기

project·home·key·workspace ID·storage·base·request digest를 대화나 인계에 보존하세요. 같은 `workspace ensure` 입력으로 재개하고, 한 작업공간 경로에는 한 작성자만 둡니다. 새 명령은 `schema <command>`를 먼저 읽고 저장한 ID와 home을 사용합니다.

| 의도 | 명령과 동작 |
| --- | --- |
| 나중에 계속 작업 | 작업공간을 유지합니다. 소유 Editor만 종료하려면 `unity stop`을 사용합니다. |
| 결과 보존 | `workspace checkpoint`로 안정된 소스를 독립 결과로 수집합니다. |
| 결과 반영 | `workspace integrate`가 소스별 대상·충돌·소유권 보호 조건을 확인합니다. 일반 폴더·기록된 Plastic의 반영에는 명시적 계획과 고정 intent가 필요합니다. |
| 결과를 유지하며 작업 자원 회수 | `workspace release`는 독립 결과를 확인하고 자기 자원만 회수합니다. |
| 작업 폐기 | `workspace discard`는 정확한 작업과 고정 폐기 intent를 요구합니다. |

대화나 Editor를 닫는 것은 반영·회수·폐기의 승인이 아닙니다. 정확한 수명주기와 복구 조건은 [AI 안내](docs/ko/agent-start.md), [폐기 계약](docs/ko/workspace-discard.md), [용어집](docs/ko/glossary.md)을 참고하세요.

## 지원 범위와 한계

현재 구현과 실제 검증은 작은 프로젝트의 Windows/NTFS 알파 사용을 대상으로 합니다. 대형 프로젝트·macOS/Linux 어댑터·포괄적인 ARM 지원·재부팅/전원 손실/일반 OS 장애 보장은 이 자격에 포함되지 않습니다. [검증 안내](docs/ko/verification/README.md)는 자동 계약 시험과 실제 파일시스템·Unity 실행을 구분합니다.

변경하지 않은 데이터를 공유해 저장 구조의 중복을 줄입니다. 각 Unity Editor의 메모리와 import 비용은 여전히 존재하며, 현재 근거로 전체 시스템 RAM이나 디스크 순간 최대 사용량의 일반적인 절감을 주장하지 않습니다. 지원하지 않는 파일 유형과 메타데이터는 저장 경로별 문서에 명시합니다.

Splinter는 CLI와 에이전트 지침을 제공합니다. 전용 설치·대화 자동 연결·작업 스케줄링·자동 모드 선택·원본 Editor 공유·AI 실행 플랫폼은 현재 구현 범위 밖입니다. 작업공간 격리는 저장과 자원 소유권을 보호하며, 신뢰할 수 없는 코드를 실행하는 OS 보안 sandbox는 아닙니다. 지원하지 않거나 손상된 상태는 보존하고 명시적으로 거부하며 자동 이관하지 않습니다.

## 문서와 개발

[한국어 문서 지도](docs/ko/README.md)에서 기능별 상세 문서로 이동할 수 있습니다. 기능·아키텍처·계획·검증 문서의 기본 언어는 영어이며, 각 문서의 언어 링크로 전환할 수 있습니다. [영문 문서 지도](docs/README.md)도 제공합니다.

| 문서 | 용도 |
| --- | --- |
| [AI 시작 안내](docs/ko/agent-start.md)와 [에이전트 스킬](skills/splinter/SKILL.md) | 준비 조건·반환 경로·같은 작업 재개·명시적 종료 |
| [용어집](docs/ko/glossary.md) | 기준본·비공개 범위·seal·intent·수명주기 용어 |
| [현재 기반](docs/ko/foundation.md) | 모듈·계약·구현 구조 |
| [제품 요구사항](plan.ko.html)과 [구현 계획](docs/ko/plans/implementation-plan.md) | 제품 목표·책임 경계·후속 작업 |
| [검증 안내](docs/ko/verification/README.md) | 실제 근거·검증 환경·지원 한계 |
| [개발용 CLI](docs/ko/development-cli.md) | 진단 checkout·view·probe |
| [서드파티 고지](THIRD_PARTY_NOTICES.md) | 의존성 버전·출처·라이선스 조건 |

코드는 공통 계약·수명주기(`Core`, `Runtime`, `Protocol`)와 소스 어댑터·Unity 애플리케이션 정책·Windows 저장/프로세스 구현을 분리합니다. `src/`는 제품, `tests/`는 자동 시험, `scripts/`는 명령 wrapper와 검증 도구, `tools/`는 개발 전용 유틸리티입니다.

일반 개발과 통합은 `development`, 출시는 `main`을 사용합니다. 필수 검증은 `pwsh ./scripts/build.ps1`입니다. 의존성을 변경하면 중앙 버전과 해당 lockfile을 함께 갱신합니다. 선택적인 형제 `../splinter-unity`는 별도의 Git/Unity 시험 프로젝트이며 submodule이나 CLI 발견의 필수 조건이 아닙니다.

## 크레딧

Splinter는 다음 프로젝트와 기여자들의 작업을 사용합니다.

- **[WinFsp](https://github.com/winfsp/winfsp)**: 쓰기 가능한 파일시스템 view와 .NET binding. **WinFsp - Windows File System Proxy, Copyright (C) Bill Zissimopoulos.** GPLv3와 자유·오픈소스 소프트웨어의 linking에 대한 조건부 예외를 적용합니다.
- **[.NET](https://github.com/dotnet/runtime)**·**[System.CommandLine](https://github.com/dotnet/command-line-api)**: 런타임과 CLI 기반. Brotli 압축은 .NET의 `System.IO.Compression` API를 통해 사용합니다.
- **[json-everything](https://github.com/json-everything/json-everything)**: schema 검증과 JSON 처리를 위한 JsonSchema.Net·JsonPointer.Net·Json.More.Net. 소스는 MIT이며, 고정한 NuGet 바이너리에는 별도의 Open Source Maintenance Fee 약정이 포함되어 있습니다.
- **[Humanizer](https://github.com/Humanizr/Humanizer)**: JSON 패키지가 간접 참조하는 MIT 런타임 의존성.
- **[xUnit.net](https://github.com/xunit/xunit)**·**[Microsoft.Testing.Platform](https://github.com/microsoft/testfx)**: 자동 시험과 TRX 보고. 각각 Apache 2.0·MIT를 적용합니다.
- **[Matplotlib](https://matplotlib.org/)**: 선택적인 문서용 스크립트로 기록된 비교 그래프를 렌더링합니다.

설치된 Git·PowerShell·Unity·Plastic 도구에는 각 도구의 라이선스가 적용됩니다. 직접·간접 패키지의 정확한 버전, 원본 라이선스 링크, 표기 조건과 재배포 시 고려사항은 [서드파티 고지](THIRD_PARTY_NOTICES.md)에 정리했습니다.
