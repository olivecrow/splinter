# 실제 Unity 병렬 편집 검증

사용자가 요청한 MCP 대상 선택, 리소스와 씬 편집, 현재 작업 상태의 테스트, 전체/부분 원본 반영과 회수를 한 번의 작은 실제 A/B 묶음으로 확인한다. 앞선 [저장·메모리 비교](unity-live-comparison.md)의 미실행 항목을 이번 실행의 통과로 소급하지 않는다.

## 고정 범위와 종료 기준

Git 원본 `T:\ua-source`, 등록 home `.local/wl29`, Unity `6000.3.22f1`, 승인한 Pipeline `0.8.0-exp.1`, 기존 Coplay MCP `v10.2.0`과 Test Framework `1.6.0`을 사용한다. 로컬 L과 Splinter A/B는 독립 Editor다. Codex worktree 비교군의 앞선 실측은 유지하며 이번 S 기능 검증과 구분한다. 새 설치·grant·버전·원격·전체 장애 행렬은 포함하지 않는다.

| ID | 실제 작업 | 완료 증거 |
| --- | --- | --- |
| UP-01 | 소유 L/A/B의 기존 MCP bridge 연결과 호출별 A/B 지정 | 실제 project/PID, discovery identity, 교차 순서와 동시 호출의 서로 다른 응답, 원본/peer 보호. 연결 실패는 CLI 성공으로 대체하지 않음 |
| UP-02 | 같은 상대 경로의 작은 PNG 생성·수정과 importer 설정을 A/B에서 다르게 변경 | 서로 다른 픽셀·크기·GUID/파일 hash와 실제 imported 객체, 상대 작업·원본 hash 불변 |
| UP-03 | 같은 상대 경로의 씬 생성·저장·다른 transform/리소스 연결·재로드 | Camera/Light, 작업별 marker·transform·참조, 저장된 씬 재로드의 값과 교차 오염 부재 |
| UP-04 | 작업별 EditMode 테스트 작성·동시 실행, 한쪽 상태 변경 뒤 실패와 수정 후 성공 | 해당 workspace의 실제 상태와 실행 project/PID, 서로 다른 테스트 결과. 의도한 실패/복구와 peer 지속을 보존 |
| UP-05 | A 전체 결과를 시험용 원본 branch로 통합하고 B의 선택한 독립 리소스만 Git으로 반영 | stable seal/독립 result ref, 전체 diff와 선택 경로 목록, 제외한 B 씬/marker 불변, 로컬 Editor 실제 load/테스트 |
| UP-06 | A 회수 중 B 작업 지속, B 결과 보존과 마지막 회수, 로컬 시험 Editor 정상 종료 | exact 소유 Editor/provider/mount/Git 등록/delta/snapshot 부재, result ref 존재, 원본 기준 복원과 다른 작업 보존 |

입력은 `Assets/ParallelQualification` 아래 작은 PNG·씬·marker·Editor 테스트로 한정한다. 부분 반영은 제품의 새 부분 merge API가 아니라 독립 결과의 명시적 경로를 적용하는 AI/Git 절차다. GUID/meta와 참조 의존성까지 선택하고 불필요한 B 변경이 원본에 들어오지 않는지 검사한다. 시험 결과는 별도 `codex/` branch/ref에 보존하고 원본의 기존 branch/내용을 복원한다.

## 선행 회수 차단과 명시적 보존 계획

앞선 A/B는 `.local/wl29`의 73개 종료된 구형 기록 때문에 마지막 snapshot 회수가 실패했다. 읽기 전용 조사에서 72개 `released`·1개 `discarded`, 모두 회수 disposition `released`, workspace/delta 부재를 확인했다. 이 기록은 현재 필수 필드가 없어 거부되며 자동 이관하지 않는다.

실행 전 별도 승인할 구체적 조치는 73개 terminal 기록 디렉터리를 내용/hash 그대로 `.local/wl29-archive-20261003-uv`에 보존 이동하고, 앞선 A/B의 동일 id/home release를 재시도하는 것이다. 독립 결과 refs·서비스 ledger·다른 snapshot은 변경하지 않는다. 전체 파일 manifest와 검토용 실행 스크립트는 `artifacts/unity-parallel-validation-20261003/`에 둔다. 승인되지 않으면 이 조치는 실행하지 않으며 기존 차단을 보존한다. 회수가 다른 snapshot guard에 막히면 그 상태를 기록하고 삭제 우회하지 않는다.

새 A/B는 이 선행 경계가 해소된 기존 등록 home에서만 생성한다. 각 bridge는 정확한 시험 소유 Editor에서 시작하고 공유 서버/다른 Editor를 중지하지 않는다. 호출별 대상 선택이 실제 설치된 MCP client/schema에서 작동하지 않으면 그 제한을 먼저 기록하고, CLI를 사용한 나머지 편집/테스트는 MCP 자격과 구분한다.

검증 문서와 HTML은 실제 실행·실패·미실행과 비용을 표시한다. 작은 입력의 통과를 대형 프로젝트·PlayMode/build·전원 장애·다른 버전의 지원으로 확대하지 않는다.

## Codex worktree 후속 고정 범위

사용자의 남은 검증 실행 요청에 따라 C 비교군도 같은 다섯 기능을 한 번의 작은 A/B로 확인한다. 기존 앱 생성 worktree `1a8c/splinter-unity`와 `90dd/splinter-unity`의 종료된 작업을 보존하고, 별도 `codex/uc-up-*` 시험 branch에서 기준 `23896dfacba2d90d0c49f826444b178fdbe82d2b`를 사용한다. C에서는 Splinter 명령·provider·서비스·mount·소유권 helper와 Coordination을 사용하지 않는다. 일반 Git, 기존 MCP, Unity CLI와 OS 관측만 사용한다.

입력·Editor/패키지 버전은 S와 동일하며 새 `Assets/ParallelQualification`의 PNG·씬·marker·세 개 EditMode 테스트로 한정한다. 호출별 실제 project/PID 확인, A/B 서로 다른 값, B만 변경해 실패/복구, A 전체 및 B PNG/meta 두 경로 선택 반영, A 종료/Library 정리 중 B 지속, 시험 원본에서 실제 load/테스트, 정상 종료와 원본·기존 branch/파일 복원을 종료 기준으로 삼는다. 기존 미커밋 raw 바이트와 결과 commits, 외부 Editor/MCP 설정은 보존한다.

앱이 primary checkout의 archive를 거부했던 기존 제한은 강제 삭제로 우회하지 않는다. 소유 Editor와 생성 cache 정리는 실제 확인하고, 전체 앱 checkout 제거가 불가능하면 그 제한을 남긴 채 범위를 마감한다. 쓰기 타임아웃은 관측 없이 반복하지 않으며 새 설치·버전·장애 행렬·대형/PlayMode/build 시험은 포함하지 않는다. 새 원시 증거는 `artifacts/unity-codex-parallel-validation-20261003/`, 결과는 검증 문서와 대화의 짧은 텍스트에 남긴다. HTML은 추가하지 않는다.

## 한정 종료

사용자는 위 73개 기록의 보존 이동과 앞선 같은 A/B의 release 재시도를 명시적으로 승인했고, hash/독립 결과를 유지한 채 회수 차단을 해소했다. S A/B의 UP-01~06은 실제 검증과 기존 HTML의 실패·명시적 재시도 기록으로 한정 마감했다. 후속 C A/B도 위 고정 범위의 MCP·리소스·씬·현재 상태 테스트·전체/선택 반영과 Editor/cache 정리를 실제 확인했다. 각 방식의 반영 결과를 로컬 Unity에서 불러오고 테스트한 뒤 원본·이전 작업의 raw 파일/branch/결과를 보존·복원했다.

C의 첫 MCP 테스트 초기화/결과 유실, reload 후 수동 bridge 재연결, MCP runner 순차 처리와 앱 primary checkout 전체 회수 Partial은 [실제 검증](../verification/unity-parallel-editor-validation.md)이 소유한다. 새 제품 코드·서비스·설치·버전·전체 장애 행렬 확대는 포함하지 않는다. 후속 C 결과는 텍스트와 검증 문서에만 추가하며 기존 HTML은 S까지의 기록으로 유지한다. 저장·메모리 전체 peak/귀속 unknown과 범위 밖 NotRun은 바꾸지 않는다.
