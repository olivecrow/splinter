# ReFS 시험 자원 정리와 다음 작업의 시작 상태

2026-09-21 사용자가 문서 전제 수정과 임시 자원 정리를 요청한 뒤 수행했다. 제품 코드는 `f0239bb`, Unity fixture의 `development`는 `66820470038c85319bc4d47d1ff2859cab82e928`다. 정리는 새 NTFS 저장소 구현/시험의 통과 증거가 아니다.

## 확인과 회수

- 제품·fixture의 Git worktree 목록에는 각각 원래 checkout 하나만 있었다. 새로 삭제할 linked worktree는 없었다.
- ReFS 시험의 준비/A/B/증분 준비 기록 네 개가 모두 `released`이고 `workspaces`가 비어 있음을 확인했다. 소비 프로세스가 없는 상태에서 `baseline release --id 51f7ee1d3abfc7c859cee60b --home S:/splinter-resource-alpha`가 `Succeeded`를 반환했다.
- 생성 영수증의 정확한 VHDX 경로·disk 4·S: 매핑·ReFS/볼륨 ID를 확인했다. 알려진 시험 디렉터리만 있고 baseline tree와 살아 있는 시험 소비자가 없는지 확인했다. 기존 볼륨을 포맷하거나 임의 디스크를 분리하지 않았다.
- Windows 관리자 권한으로 시험 VHDX를 분리하고 삭제했다. 삭제한 파일의 길이는 **9,835,642,880 bytes(약 9.16GiB)**다. 동적 디스크의 최대 크기 64GiB를 실제 회수량으로 보고하지 않는다. 분리와 파일 부재를 확인했으며 이는 OS 전체 여유 공간의 통제된 성능 측정은 아니다.
- 이전 Git checkout 알파의 기록 세 개도 모두 `released`이며 실행 경로가 비어 있음을 확인했다. 해당 임시 home과 사용이 끝난 일회 도우미·index·시험용 임시 파일을 정리했다.

## 보존한 것

원본 Unity 프로젝트·Library·Editor/MCP, Git의 결과 ref/커밋과 기존 개발 브랜치를 보존한다. 제품 코드·시험 코드·재현 스크립트는 유지한다. 설치된 도구와 .NET/NuGet 의존성 캐시는 불필요한 전체 재설치를 피하기 위해 유지한다.

작은 실행 결과와 로그는 `artifacts/resource-alpha/reclaimed-state/` 및 `artifacts/alpha-20260921/reclaimed-state/`에 보존한다. VHDX 생성 영수증, 종료/소유권 기록, 기준본 메타데이터, ReFS 시험 결과와 `cleanup.json`이 포함된다. 원본 기록의 토큰/로컬 경로를 공개 문서에 옮기지 않는다. 추적된 기존 SR/AA 증거의 실행 당시 사실은 변경하지 않는다.

## 다음 세션

S: 시험 디스크와 ReFS 기준본은 더 이상 존재하지 않는다. 새 작업이 이를 재생성하거나 cold baseline을 다시 만드는 것을 기본으로 삼지 않는다. [NTFS 스냅샷·변경분 계획](../plans/ntfs-snapshot-overlay-spike.md)의 SO-01~06을 수행한다.

별도 Codex worktree에서는 `../splinter-unity`가 실제 fixture가 아닐 수 있다. 실제 fixture는 `C:/Users/olive/Desktop/Development/splinter-unity`이며 그 저장소 지침과 상태를 확인한 뒤 사용한다. 실제 1TB 사용자 프로젝트는 시험 대상으로 자동 선택하지 않는다.
