# 안정적인 병렬 작업의 직접 검증

[SP-01~04 확정 범위](../plans/stable-parallel-completion.md)를 **scoped-complete-with-limitations로 마감했다.** 현재 Windows x64 NTFS 알파의 잠금 오류 분류와 Editor 없는 제품 A/B의 동시 CLI·파일 I/O 경계다. 기존 Unity/외부 도구 인계·PV 비교의 자격은 해당 후보/검증에 유지하며 이번 통과로 승계하지 않는다.

## SP-01~02: 결함과 공통 회귀

기준 `1dd9f12`에서 실제 잠금 디렉터리를 제거한 시험은 5초 대기 뒤 `WorkspaceBusy`를 반환했다. 경로 문제를 안전한 점유 재시도로 잘못 안내한 것이다. 잠금 위치가 기존 디렉터리인 시험은 원시 `UnauthorizedAccessException`이었다. 수정 전의 두 실패와 세 점유 시험 통과를 `artifacts/stable-parallel-completion-20261003/lock-before.trx`에 보존했다.

Runtime은 확인된 sharing/lock violation HRESULT만 기존 100ms/5초 대기로 처리한다. 다른 I/O·접근 실패는 기존 `StateReadUnavailable`/`requires-user-action`으로 즉시 거부한다. 파일/권한을 수정하거나 새 잠금 디렉터리로 우회하지 않는다. 같은 작업의 점유·취소·소유 핸들 보존·닫힌 뒤 재사용, 다른 key의 독립 잠금, A의 정상 stop 대기 중 B의 status/같은 ensure 완료를 확인했다. 집중 시험은 18 passed, 새 회귀는 6건이다. 서로 다른 key의 생성 수만 대조하던 기존 시험에 실제 완료 순서의 경계를 보완했다.

Core/공급자 API·fake 계약·입력/출력 schema·상태 v2·overlay v2/SJR2/candidate v3는 유지한다. 이미 제공하던 오류 코드의 내부 분류 수정이다. 프로젝트 등록/반영·기준·공유 snapshot 잠금과 보호된 서비스 writer의 직렬화를 줄이지 않는다. 임의 파일 편집의 대화별 배타 잠금·큐·자동 binding·임대 인수는 제공하지 않는다.

## SP-03: 실제 겹침

현재 Runtime SHA-256은 `b769a1fe899c982416d62f84325021c282313bf7930bad9c1db96b831c9a13ea`다. 기존 원본 `T:\plastic-fixture-20261002`, home `T:\pt1002`, Plastic/Unity/snapshot/NTFS와 17,179,869,184B journal budget을 사용했다. 모든 CLI 호출은 일반 사용자 토큰이다. 원시 명령/응답·UTC 시작/종료·쓰기별 hash는 `artifacts/stable-parallel-completion-20261003/`에 있다.

| 작업 | key / id | 독립 결과 |
| --- | --- | --- |
| A | `sp-t-a-20261003` / `a7c28dec5242ed3d77bfe043` | `sha256:2a6395c243727eee88b30e21581e5da20415ecaf0524170276156b01f6b560c7`, 63 files / 376,595 logical B |
| B | `sp-t-b-20261003` / `26f2f8a586f5f8d60d376237` | `sha256:9bb3f661fb35c41cb58485989b524107075ac14afc9de4de9d2e28911dcd233a`, 127 files / 377,633 logical B |

생성 직후 initial payload/read cache/private payload/sequence는 각각 0이었다. 각 반환 경로에 다른 128KiB source payload를 명시적으로 쓰고 B writer와 A 명령을 별도 프로세스로 실행했다. 같은 원본의 기준은 62 files / 245,523 logical B, fingerprint `sha256:8a58a18cac8d41149bafa58e886dcd25b78fd8162ea7cd5b19e9533fef0c9e0b`다.

| A가 진행 중인 구간 | A 명령 ms | 구간 안에 완료된 B 쓰기 | B status / 같은 ensure ms |
| --- | ---: | ---: | ---: |
| checkpoint | 9,187 | 32 | 2,285 / 3,391 |
| release | 12,714 | 32 | 2,070 / 3,174 |

B의 조회·재개는 각 A 명령이 끝나기 전에 완료했다. 64개 쓰기의 mounted hash와 B 독립 result manifest/object hash를 모두 확인했다. A 결과에는 B marker가 없다. B의 id/path/key/digest/base/snapshot은 재개 전후 같고, A 회수 뒤 B payload를 다시 바꾸어 독립 수집했다. 한 번의 실제 시간 관측이며 동시 속도 향상률·SLA나 모든 명령 조합의 자격은 아니다. Editor·MCP/Unity CLI·원본 통합·discard·강제 중단을 이번에 실행하지 않았다.

## SP-04: 보호·회수·회귀와 한계

두 결과의 baseline/result 모든 파일 payload를 회수 뒤 길이/hash로 검증했다. 둘 다 `released`, 제품의 snapshot observation은 `missing`이며 소유 provider/앱 group process·핸들·private/working set은 0, mount/delta/path는 부재다. 원본/관리 65개 파일과 이전 records/results 944개 파일의 bytes/hash, Plastic 등록 5개, 원본 Unity PID 68624·85184·52688의 시작 시각을 보존했다. 기존 C: 보류 작업에는 release/discard 요청을 보내지 않았다.

추가 전역 `Win32_ShadowCopy` 목록 조회는 일반 사용자 토큰에서 초기화 실패했다. 실패 log를 보존했고 권한·서비스/등록·볼륨을 변경하지 않았다. 전역 목록/기존 보류 snapshot의 새 live 관측은 unknown이다. 이번 두 소유 snapshot의 부재는 각 제품 release의 검증된 관측이며 전체 볼륨/VSS가 0이라는 주장과 구별한다. 과거 C: 보류·SF Partial/NotRun·대형/ARM/OS/다른 도구 미자격은 유지한다.

필수 `pwsh ./scripts/build.ps1`의 locked restore·Release build·906 passed/0 failed/1 optional ReFS skip과 실제 JSON 응답 20건의 현재 `ContractCatalog` schema 검증을 완료했다. 문서 UTF-8·로컬 링크·diff와 최종 source/binary/evidence 연결은 `completion-final.json`에 보존한다. 이번 선택 단위는 마감하며 새 병렬 작업은 실제 진행 차단/보존 결함·새 입력/지원 요구의 질문과 종료 조건에서만 연다. 새 Editor 준비의 몇 분 비용만으로 큐·새 최적화/전체 비교를 자동 시작하지 않는다.
