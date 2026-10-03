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

## 한정 종료

사용자는 위 73개 기록의 보존 이동과 앞선 같은 A/B의 release 재시도를 명시적으로 승인했고, hash/독립 결과를 유지한 채 회수 차단을 해소했다. 새 S A/B의 UP-01~06은 [실제 검증](../verification/unity-parallel-editor-validation.md)과 [HTML](../../reports/unity-parallel-editor-validation-20261003.html)의 조건 및 실패·명시적 재시도 기록으로 한정 마감한다. A 전체와 B 선택 리소스를 시험 원본 Unity에서 불러오고 현재 상태 테스트를 실행한 뒤, 독립 결과를 보존하고 모든 소유 작업 공간을 회수했다. 기존 원본·다른 작업을 복원/보호했으며 새 제품 코드·서비스·설치·버전 확대는 포함하지 않는다. C의 이번 다섯 기능 항목은 NotRun이며 앞선 실제 저장·메모리 비교의 자격과 구별한다.
