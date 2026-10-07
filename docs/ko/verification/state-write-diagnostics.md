# Workspace 상태 쓰기 실패 진단 검증

[English](../../verification/state-write-diagnostics.md)

기준일: 2026-10-01. [NR-01의 확정 범위](../plans/ntfs-recovery-and-support.md#확정-범위-nr-01-상태-쓰기-실패-진단)와 [현재 workspace 계약](../ntfs-workspaces.md)의 진단 단위다. `10eaad7` 뒤 최종 후보는 **scoped-complete-with-limitations**다. NR-01 전체는 **InProgress**이며 실제 VSS/WinFsp/Unity·서비스/grant/설치·VIVOBOOK·재부팅/전원/물리 공간 장애는 이번 후보에서 **NotRun**이다.

`artifacts/ntfs-recovery/state-write-diagnostics-20261001/`의 `qualified-source.json`·`qualified-candidate.json`·`final-audit.json`은 소스 247개·관리 바이너리/JSON 20개의 후보를 고정한다. 소스 hash 불변, 기존 설치 서비스/WinFsp 파일 hash 불변과 형제 fixture clean을 확인했다. `qualified-build.log`·`qualified-full.trx`는 locked restore·Release build 경고/오류 0, **715개 중 714 Passed / 0 Failed / 당시 제외 시험 1 Skipped**를 소유한다. UTF-8·diff 검사도 통과했다.

| 관문 | 수 | 결과와 한계 |
| --- | --- | --- |
| 공통 데이터/응답 schema | 9 | 8개 단계와 게시 상태의 적합성·모순 거부, native 오류 쌍/Win32 facility·누락 진단·추가 path 필드 거부. 합성 응답 Passed |
| 실제 로컬 파일 I/O | 5 | 교체 공유 오류 32·읽기 전용 교체 거부 5·이벤트 append 공유 오류 32, record 경로 파일 blocker·목적 디렉터리 보존. Windows x64 Passed |
| 원래 작업 오류와 실패 기록의 중첩 진단 | 1 | fake source의 `GitCommandFailed`와 실제 잠긴 상태 파일의 stage/native 오류를 함께 유지. 잠금 해제 후 같은 fake 작업 재개/회수 Passed |
| 일반 CLI와 새 소유 diagnostic Git fixture | 1 | 정상 ensure → 실제 release-intent 교체 잠금 → JSON 진단/기존 기록·Git 등록/완결 tmp 확인 → 같은 ensure/release. 원본 HEAD/index/tracked 입력·tmp 유지, 최종 released Passed |

새 시험 **16개 모두 Passed**다. 교체가 거부된 실제 파일 시험은 이전 내용과 완결 임시 JSON을 유지하고 새 동일 작업 Save 뒤에도 실패 tmp를 삭제하거나 자동 승계하지 않았다. 이벤트 append 차단은 새 JSON이 이미 보이는 `statePublication=completed`와 실패 응답을 함께 확인했다. 이벤트 로그는 접근 복구 전 불변이며 같은 Save로 기록을 재개했다. 게시 오류 응답은 교체 성공 여부를 `unknown`으로 유지한다. 접근 복구는 시험이 연 자신의 핸들 종료이며 제품의 속성 변경 우회나 자동 retry가 아니다. 모든 입력은 새 소유 임시 디렉터리이고 기존 등록 source/home·비소유 snapshot을 사용하지 않았다.

실제 open/write/flush/close 내부 장애·부분 event append·VSS 준비 단계 재발은 **NotRun**이다. 합성 단계 응답의 통과를 이 실제 장애 관문이나 이전 kernel/VSS/Unity 후보의 자격으로 확대하지 않는다. diagnostic Git 시험은 NTFS snapshot/Unity 검증이 아니며 validation project ledger의 `ValidationStateWriteIncomplete`도 이번 단위에 포함하지 않는다.

## 보존한 시험 실패

`first-build.log`·`candidate-source.json`은 xUnit2031 분석기로 시험 전 실패한 최초 후보를 보존한다. Assert.Single의 필터 인수 overload로 수정했다. 그 뒤 `final-build.log`·`failed-full.trx`·`final-source.json`·`failed-test-candidate.json`·`failed-binaries/`는 **712 Passed / 1 Failed / 1 Skipped**를 보존한다. CLI fixture가 NTFS 전용 준비 observer를 diagnostic Git 모드에 연결해 실패 주입이 작동하지 않았으며, 기대한 실패 대신 정상 성공을 반환했다. 제품 결함의 해결로 판정하지 않는다. 후속 시험은 일반 release의 상태 파일 잠금과 별도 fake 원래 오류 보존을 사용했고 제품 observer/저장·게시·retry/flush 정책은 변경하지 않았다. 최종 전체 시험은 수정한 새 후보로 다시 실행했다.

[NR-02의 상태 게시 실패](preparation-recovery.md#보존한-실패와-재검증-조건)와 [NR-03 제품 시험의 상태 게시 실패](ntfs-recovery.md#보존한-실패와-재검증-조건)는 원래 후보·응답·임시 파일을 보존한다. 그 응답에는 하위 오류와 stage가 없어 원인은 **unknown**이다. 새 진단 추가로 과거 오류를 공유 위반·접근 거부·공간 부족으로 소급 확정하지 않는다. 동일 실제 작업에서 재발하면 새 진단·원래 요청·단계의 기록을 먼저 수집하며 retry/flush 정책을 추측 변경하지 않는다.
