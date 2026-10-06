# Git 소스·원본 Library 참조와 MCP/RAM 검증

2026-10-04 사용자 요청은 Git 프로젝트의 Library도 원본 데이터를 참조하고 각 병렬 작업의 변경은 따로 저장하며, 실제 A/B의 MCP 연결과 RAM 절감 여부를 확인하는 것이다. 기존 cold Git A/B의 [검증](../verification/git-unity-validation.md)은 비교 근거이며 새 warm 경로의 통과로 승계하지 않는다. HTML은 작성하지 않는다.

## 고정 요구와 발견한 경계

- Git 소스는 현재 fixed commit/tree/blob과 source/target/dirty/peer guard를 유지한다.
- 기존 원본 Library를 기준으로 읽되 초기 전체 복사·읽기 hydration·파일 전체 copy-up·VSS 재도입으로 우회하지 않는다. 각 작업의 Library 쓰기는 별도 범위 저널이다.
- 원본이 계속 수정돼도 시작 세대의 Library 바이트가 유지돼야 한다. mtime/길이·감시 알림·하드링크만으로 불변성을 주장하지 않는다.
- 현재 WinFsp는 mount를 통해 오는 쓰기만 처리한다. 원본의 직접 NTFS 쓰기와 mmap 덮어쓰기는 현재 provider를 통과하지 않는다. Git ignored Library에는 이전 내용을 복원할 객체가 없다.
- [oplock](https://learn.microsoft.com/en-us/windows/win32/fileio/types-of-opportunistic-locks)은 접근/쓰기 충돌 통지이며 변경 범위의 이전 내용 저장 구현을 대신하지 않는다.

## 검토 가능한 두 실행 경로

### A: 원본 Library만 관리형 mount로 연결

사용자가 **Library만 관리형 mount 허용**으로 이 경로를 승인했다. 제품/소스 프로젝트 root·Assets·Packages·ProjectSettings는 같은 NTFS 위치에 남는다. 원본 Library의 접근 경로도 `<project>/Library`를 유지하지만 해당 디렉터리는 WinFsp가 제공하게 된다. 이 Library의 파일시스템/물리 배치 변경만 기존 요구의 예외다.

1. 정확한 원본 Editor/PID·시작 시각·dirty scene·MCP/서버 소유권을 확인한다. 최초 연결에는 원본 Editor의 저장/정상 종료가 필요하다. dirty 변경을 자동 폐기하지 않는다.
2. 원본 Library를 같은 NTFS 볼륨의 소유 home 아래 고정 base 디렉터리로 **rename**한다. 데이터 전체 복사 없이 최초 바이트를 보존한다. 예시 시험 home은 `.local/glr1004/home`, base는 `library-bases/<id>/base`다. 원본 source 경로의 이동은 없다.
3. 원본의 기존 Library 경로에 별도 writable overlay를 연결한다. 원본 Editor도 이 경로를 통해 변경 범위만 쓰므로 A/B의 고정 base를 덮어쓰지 않는다. A/B는 같은 불변 Library 기준+각 private delta를 사용한다. 모든 owner·기준·호환 키와 의존성은 durable receipt로 유지한다.
4. 첫 이동/mount·중단 재개·동일 요청·basis reader 보호를 먼저 fake/작은 실제 폴더로 검증한다. 사용자 원본의 첫 전환은 이 검토된 구현이 준비된 뒤 수행한다.
5. 마지막 peer 회수 후 원본의 최종 변경 범위를 기준에 반영하고 정상 NTFS Library로 복원한다. 다른 작업/원본 변경·미완료 게시 intent가 있으면 보존하고 복원을 거부한다. 새 전체 Library materialization/파일 전체 copy-up으로 복원하지 않는다.

이 경로는 원본 Library를 수정 가능한 관리형 경로로 만드는 새로운 수명주기다. 기존 `git-snapshot`의 mutable-source fallback이나 원본 mount 숨기기로 구현하지 않는다. 원본 Editor의 첫 정상 종료와 cache 위치/파일시스템 변경을 기존 읽기 권한으로 해석하지 않는다.

### B: 원본 Library의 NTFS 위치/파일시스템을 완전히 유지

원본 NTFS의 쓰기·paging I/O·truncate·rename/delete 전에 필요한 이전 범위/메타데이터를 보존하는 별도 파일시스템 필터가 필요하다. WDK·드라이버 서명/설치·paging I/O와 전원/중단 보존은 기존 일반 사용자 WinFsp 코드의 연결 작업으로 포함하지 않는다. 필요한 개발/설치 조건과 한정 시험을 먼저 확인하며 현재 PC에 드라이버를 자동 설치하지 않는다.

## 구현과 검증의 종료 기준

경로 선택 뒤 공통 불변 생성 데이터 기준·조합 tree·작업별 range store·owner/dependency/release/discard를 연결한다. 현재 schema/fake/적합성·capabilities/기능 문서·AGENTS를 함께 갱신하며 구형 상태를 자동 이관하지 않는다.

| ID | 질문 | 종료 증거 |
| --- | --- | --- |
| GL-01 | 원본 Library 기준을 복제 없이 안전하게 참조하는가 | Git commit+Library 세대의 정확한 identity·원본 후속 범위 쓰기 중 기존 A/B 바이트 유지·payload 복사/read hydration 0·private 쓰기 분리 |
| GL-02 | 원본/peer와 기준의 수명이 보호되는가 | 같은 요청 복구·basis reader/dependent guard·A 회수 중 B 지속·마지막 회수와 원본 Library 복원 또는 필터 분리·독립 결과/원본 refs 유지 |
| GL-03 | 각 Editor의 MCP가 reload 뒤에도 정확한가 | 호출별 실제 project/PID/start 확인·사전 refresh/reload 완료와 재발견·유실 job 확인·쓰기 무조건 반복 없음·전체 결과를 외부 저장 |
| GL-04 | 실제 RAM 비용이 줄었는가 | 동일 source/Unity/package/scene/task의 비교·idle/작업 구간별 Editor/provider/reader·private/공유 resident/PSS 추정·프로세스 identity fencing. 미절감/unknown을 그대로 보고 |
| GL-05 | 최종 결과와 소유 자원이 보존/회수되는가 | 독립 결과·정확한 A/B editor/provider/mount/등록/delta/basis refs 부재·원본 Editor/공유 MCP/기존 refs/config 보호·필수 build/UTF-8 |

RAM 측정에서는 Private Bytes·working set·공유 page 귀속과 전체 순간 peak를 구별한다. Library 파일 참조만으로 Unity 관리 heap/scene/graphics 메모리가 공유된다고 주장하지 않는다. 새 읽기 전용 process 계측은 원본에 대한 쓰기/정지 없이 준비할 수 있다. 실제 A/B·원본 Library 전환은 선택한 경로의 계약과 복구 구현 뒤 진행한다.

## 종료 판정

승인한 A 경로의 구현·GL-01~05와 한 실제 Git/Unity A/B를 [검증](../verification/git-library-reference.md)의 scoped-complete-with-limitations로 마감했다. 초기 payload 복사/read cache 0·공통 기준/각 범위 쓰기·MCP exact/reload/리소스/씬/current-state tests·독립 결과/소유 회수와 원본 NTFS 복원·재열기를 확인했다. 실제 RAM은 cold와 비슷해 절감을 확인하지 못했고, 준비/복원 비용·MCP 실패 상세 소실·전체 peak/공유 귀속 unknown은 후속 판단에 남긴다. 이 결과를 일반 RAM 절감이나 추가 VCS/환경 지원으로 확대하지 않는다.
