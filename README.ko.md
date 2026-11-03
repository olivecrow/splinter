# Splinter

**변경하지 않은 데이터는 공유하고 작업별 변경은 격리하는 AI 병렬 개발 작업공간.**

[English](README.md) · [한국어 기술 설명서](docs/ko/foundation.md)

Splinter는 Windows/NTFS에서 Unity 프로젝트와 일반 폴더를 위한 로컬 작업공간 도구입니다. 각 작업은 고정 기준본과 자기 변경 범위를 결합한 writable WinFsp 경로를 사용합니다. 최초 전체 복제, 읽은 파일의 디스크 hydration, 파일 전체 copy-up 없이 여러 작업을 독립적으로 수행하며 원본도 계속 편집할 수 있습니다.

현재 연구용 alpha이며 대형 입력·전체 수명주기·속도/RAM·OS 장애 자격은 미완료입니다. 과거 작은 Git/None/Plastic/Unity 검증을 현재 후보의 일반 성능이나 지원 보장으로 확대하지 않습니다. 자세한 범위는 [영문 검증 안내](docs/verification/README.md)를 참조하세요.

## 사용 흐름

필요할 때 작업공간 생성 → 같은 작업 지속 → 불변 결과 수집 → 명시적 반영·보존·폐기.

반환된 프로젝트 경로에서 기존 파일·Git·Unity 도구를 사용합니다. 중간 checkpoint 뒤에도 같은 작업을 계속할 수 있으며, release는 독립 결과를 보존합니다. 원본 반영은 결과와 계획·intent·현재 대상 검증을 요구합니다. 대화 종료·시간 초과는 반영이나 삭제의 승인이 아닙니다.

## 저장 경로

| 경로 | 기준본 |
| --- | --- |
| `git-snapshot` | 고정 로컬 Git commit/tree/blob |
| `plastic-snapshot` | 고정 Plastic changeset/revision |
| `project-snapshot` | VCS 없는 프로젝트의 명시적 관리형 기준본 |

관리형 프로젝트/Library 준비는 같은 NTFS 볼륨에서 기존 데이터를 이동하고 원본 경로에 writable view를 제공합니다. 최초 전환과 최종 복원은 정확한 원본 Editor의 저장·정상 종료를 요구합니다. 현재 task/basis/overlay는 v4/v3/v3, metadata는 SPM2입니다. 새 payload는 raw이며 과거 압축 payload와 미지원 형식은 변환하지 않고 보존·거부합니다.

## 시작하기

Windows/NTFS, PowerShell 7.2+, [global.json](global.json)의 .NET SDK와 WinFsp가 필요합니다. Git/Plastic/Unity는 해당 입력에만 필요합니다. 현재는 소스 체크아웃의 PowerShell CLI를 사용합니다.

- [시작 안내](docs/agent-start.md)와 [에이전트 skill](skills/splinter/SKILL.md)에서 setup·schema·같은 작업 인계를 확인하세요.
- `pwsh ./scripts/build.ps1`은 locked restore·Release build·자동 회귀 검증 진입점입니다.
- 제품 CLI는 `scripts/splinter.ps1`, 개발 진단은 `scripts/splinter-dev.ps1`입니다.
- [문서 지도](docs/README.md), [기술 구조](docs/foundation.md), [라이선스](LICENSE), [외부 구성요소 고지](THIRD_PARTY_NOTICES.md)를 참조하세요.

Splinter는 저장·프로세스·자원 소유권을 관리합니다. refresh·컴파일·Play/Test/build·도구 연결·게임 합격 판단은 에이전트와 외부 도구의 책임입니다. 전용 설치기·자동 대화 binding·스케줄러·공유 원본 Editor 조율·AI Runner/DAG·daemon/DB는 제공하지 않습니다. 작업공간 격리는 악성 코드용 OS 보안 sandbox가 아닙니다.

문서는 영어를 기본으로 하며 한국어는 이 README와 기술 설명서만 유지합니다. 과거 실험 보고서와 중복 검증 문서는 Git 이력에서 확인할 수 있습니다.

Splinter 코드와 문서는 MIT 라이선스를 따릅니다. WinFsp와 .NET을 포함한 외부 구성요소의 저작권·라이선스는 [고지 문서](THIRD_PARTY_NOTICES.md)를 참조하세요.
