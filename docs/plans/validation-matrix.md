# 구현 계획 검증 추적표

작성일: 2026-09-20. 상태: **아래 원본 48개와 FD-01~10의 전체 통과 조건은 모두 NotRun**. 일부 기반 코드의 자동 시험은 별도로 존재하며 전체 제품 시나리오의 통과로 집계하지 않는다.

기준은 [기획서 14절](../../plan.html#section-14)의 필수 48개 시나리오다. 구현 순서는 [구현 계획](implementation-plan.md)의 P0~P7을 따른다. P0의 작은 실험 성공만으로 아래 시나리오 전체가 통과한 것으로 기록하지 않는다.

현재 시험 경계와 재현 명령은 [현재 기반](../foundation.md#빌드와-시험)에 있다. .NET 시험은 `artifacts/test-results/`의 TRX, 실제 저장소 Git 시험은 `artifacts/worktree-smoke/<runId>/result.json`에 결과를 남긴다. JSON/DAG/inspector 및 일반 checkout worktree 시험으로 CLI-01·GT-01·EX-01 등의 전체 조건을 통과했다고 보고하지 않는다. Unity/CoW/지속 Host 시나리오는 아직 실행하지 않았다.

## 결과와 증거 규칙

- 상태는 `Passed`, `Failed`, `NotRun`, `Unsupported` 중 하나로 기록한다. 필수 지원 조합의 `NotRun`/`Unsupported`는 공개 통과로 집계하지 않는다.
- 각 실행은 run ID, 도구 commit, OS/볼륨/Git/Unity/패키지/Codex 버전, fixture/source hash, 시작/종료 manifest, 명령·이벤트, 결과 파일 hash를 기록한다.
- fixture 성공, 합성 대형 데이터 성공, 실제 프로젝트 성공을 구분한다. 단계별 통과 후에는 변경된 경계와 최종 통합 후보의 필수 시험을 다시 실행한다.
- 아래의 ‘단계’는 구현 책임과 주 검증 단계다. P7은 공개 대상 조합에서 전체 증거를 검토하는 단계다.

## 원본 필수 시나리오 48개

| ID | 검증 대상 | 단계 | 반드시 남길 증거 |
| --- | --- | --- | --- |
| FS-01 | 원본/A/B 독립 수정·삭제·재생성·rename | P0 S1 → P2 | 시작/종료 내용 hash, 다른 사본 불변 |
| FS-02 | 대형·작은·0바이트·경계 크기 파일 | P0 S1 → P2 | 내용/길이, cloneBytes/copiedBytes, 실제 볼륨 조건 |
| FS-03 | mmap·자식 쓰기·원자적 교체 | P2 | 쓰기 경로별 다른 사본 불변 |
| FS-04 | 링크 탈출·긴/한글/공백/대소문자 경로 | P2 | 지원 동작 또는 사전 오류, 외부 경로 무변경 |
| FS-05 | 복제 중 종료·디스크 부족 | P2 | Ready 미발행, 재조정 후 상태와 남은 자원 |
| WS-01 | GUI Editor 두 개와 잘못된 인스턴스 요청 | P0 S4 → P3 | 서로 다른 경로/PID/시작 시각/세션, 교차 명령 거부 |
| WS-02 | 사람의 원본 Editor와 작업 A/B 공존 | P3 | 원본 소스 보존, 독립 compile/import 진행 |
| CP-01 | 수정한 코드의 실제 실행 값 | P0 S2 → P3 | source/compiled revision, 새 값의 테스트/Play 관찰 |
| CP-02 | 문법 오류 삽입 후 수정 | P0 S2 → P3 | 구조화된 오류, 실행 차단, 수정 후 정상 재개 |
| CP-03 | 깨진 프로젝트의 최초 시작 | P0 S2 → P3 | 브리지 부재 진단·외부 수정·재시작 회복 |
| CP-04 | asmdef·define·패키지 변경 | P3 | 해당 상태의 compile/reload와 새 코드 검증 |
| AS-01 | 새 ScriptableObject 타입과 에셋 | P0 S2 최소 → P4 | 새 타입 사용, 저장 후 재개방 값 |
| AS-02 | 새 컴포넌트의 프리팹/씬 | P4 | 타입·참조 정상, Missing Script 없음, 실제 Play |
| AS-03 | 텍스처 임포트·설정·이동 | P4 | 기대 설정, GUID와 .meta/참조 보존 |
| AS-04 | postprocessor의 추가 생성 | P4 | 후속 import/compile 이벤트, 최종 revision 일치 |
| AS-05 | 에셋 배치 중 예외 | P4 | import 잠금 복원, 다음 명령 정상 실행 |
| PL-01 | 지정 게임 씬의 실제 Play | P0 S2 → P4 | 씬·진입 이벤트·프레임·ready 조건 각각 확인 |
| PL-02 | pause/step/resume/exit 반복 | P4 | 프레임/상태 전이, 중복 구독·장치 없음 |
| PL-03 | domain reload on/off | P4 | 설정별 연결·초기화·코드 변경 리로드 복구 |
| PL-04 | 그래픽 캡처·실제 입력 경로 | P4 | 세션 귀속 이미지의 디코딩/크기, 입력에 따른 상태 변화 |
| TS-01 | 새 EditMode/PlayMode 테스트 | P0 S2 → P3 최소 → P5 | 실제 발견 목록과 선택/실행 manifest·결과 |
| TS-02 | assertion·빈 필터·컴파일 오류 | P0 S2 → P5 | 각각 실패/NoTestsMatched/CompilationFailed, 허위 성공 없음 |
| TS-03 | 테스트 중 reload·취소·Editor 종료 | P5 | 재실행 없음, Interrupted/취소 증거, 다음 실행 정상 |
| IS-01 | 동일 회사/제품명의 외부 상태 충돌 | P6 | 저장 격리 훅 또는 해당 단계 직렬화·미격리 공개 |
| RC-01 | 명령 적용 직후 응답 유실 | P1 fake → P4/P6 실제 | 사후조건 판정, 에셋 중복/덮어쓰기 없음 |
| RC-02 | Host 재시작·lease 만료·삭제 | P1 fake → P3/P6 실제 | 작업·결과 보존, 다른 Editor 미종료 |
| MG-01 | 동일 소스/에셋의 두 결과 | P6 | 충돌, 해결 후 최신 통합 후보 revision과 검증 |
| PK-01 | 도구 overlay 제거 | P2 분리 → P6 통합 | 도구 항목만 제거, 사용자 패키지·테스트·.meta 보존 |
| UX-01 | 도구 이름 없는 자연어 요청 | P3 → P7 | 새 Codex 세션에서 발견→준비→계획, 수동 연결 0회 |
| UX-02 | prerequisites·권한·기존 설정 | P3 → P7 | 일회 설정/승인 기록, 반복 승인 없음, 숨은 전체 복사 없음 |
| UX-03 | 재실행·중복 요청·Host 시작 | P1 → P3 | 동일 요청의 operation/workspace/agent 수 1개 |
| UX-04 | 실제 작업자 A/B 자동 실행 | P3 → P6 | 독립 CWD/세션, A 오류 중 B Play/테스트 진전 |
| UX-05 | 자동 완료·결과·정리 | P6 | 자연어 요청→검증 branch/보고서→worktree/Library 회수 |
| UX-06 | 업데이트·제거·세션 중단 | P7 | 기존 설정/자격증명·미회수 결과 보존, 활성 protocol 유지 |
| CLI-01 | JSON/JSONL·오류·schema | P1 | 전체 stdout 파싱, 접수/완료 구분, descriptor/schema 적합성 |
| CLI-02 | 비대화형·권한 부족 | P1 → P3 | 입력/모달 대기 없음, 필요한 행동 반환, sandbox 유지 |
| CLI-03 | 이벤트 재접속·관찰 취소 | P1 → P3 | cursor 재생/중복 제거, watch 종료 후 작업 지속 |
| CLI-04 | 예산·CWD·권한·실행 세대 | P0 S3 → P3/P6 | 제한 적용, 오염 경로/세대 거부, 작업자 종료와 검증 구별 |
| GT-01 | 실제 worktree + CoW | P0 S1 → P2 | Git 등록·HEAD/index, .git 미복제, 전체 파일 재전개 없음 |
| GT-02 | NTFS common dir + ReFS root | P0 S1 → P2 | Git 조회/결과/회수, 원본 HEAD/staged 보존 |
| GT-03 | staged/unstaged/신규 에셋 snapshot | P2 | 일관된 S, .meta 포함, 원본 무변경, 상속 delta 중복 없음 |
| GT-04 | LFS·CRLF·attributes·filters | P0 S1 일부 → P2 | 실제 checkout 내용과 index, 누락 LFS 차단, 반복 다운로드 없음 |
| GT-05 | 생성/회수 단계별 중단 | P2 | 저널/Git/파일 재조정, 미수집 결과·다른 worktree 보존 |
| GT-06 | 외부 생성/이동/삭제·볼륨 분리 | P2 | attach/관찰/차단 구별, offline 보존, 관리 밖 경로 무삭제 |
| GT-07 | linked worktree·하위 Unity·공유 정책 | P2 | common dir/상대 경로, branch 충돌 거부, hook/config 보존 |
| GT-08 | Editor/worktree/결과의 별도 수명 | P3 → P6 | Editor 정지 후 사본 유지, 회수 후 결과 유지, 원본 강제 변경 없음 |
| EX-01 | Unity/Windows/Git 없는 Core | P1 | 비Git fake VCS·fake 앱·Runner로 동일 CLI 수명주기 |
| EX-02 | 버전/capability 불일치 | P1 → P3 | 사전 차단, 거짓 Ready/자동 호환 실행 없음 |

## 기반 구현에서 추가할 회귀 시험

원본 48개를 구체화하는 추가 시험이다. 수를 늘리기 위한 API별 모방 테스트 대신 실제 데이터 손실·중복 실행·허위 성공의 경계를 검증한다.

| ID | 검증 대상 | 단계 | 통과 조건 |
| --- | --- | --- | --- |
| FD-01 | 두 CLI의 동시 Host 시작과 동일 요청 접수 | P1 | Host writer 1개, operation/외부 부작용 1개 |
| FD-02 | 같은 requestKey에 다른 입력, 회수 후 키 재사용 | P1 | 명확한 충돌/기존 결과, 보존기간 내 재실행 없음 |
| FD-03 | lease 만료 후에도 살아 있는 작성자 | P1/P3 | 소유권 확인 전 새 작성자 미배정, stale 세대 명령 거부 |
| FD-04 | DAG 선행 결과가 후행 작업 입력에 반영 | P1/P6 | B의 입력 snapshot에 A 결과 포함, 오래된 사본 미사용 |
| FD-05 | 검증 중 외부 파일 변경과 결과 재사용 | P3/P5 | 증거 stale, 새 revision 검증 전 통합 성공 차단 |
| FD-06 | Host 중단 동안 자식 stdout/종료 결과 | P3 | supervisor 기록 유지, 정확한 세션 재접속, 중복 작업자 없음 |
| FD-07 | DB migration·일관된 백업·복원 | P1/P7 | 진행 중 operation과 requestKey/결과 참조 보존 |
| FD-08 | cleanup 계획 후 경로/소유권 변경 | P2/P6 | digest/정규 경로/볼륨/reparse 재검사로 잘못된 삭제 거부 |
| FD-09 | 통합 검증 후 원본 HEAD 또는 dirty 상태 변경 | P6 | 적용 거부, 후보 branch·검증 증거 보존 |
| FD-10 | 손실된 callback·이전 run XML·누락 artifact | P5 | 실제 완료/새 증거를 확인할 수 없으면 성공 차단 |

## 공개 시 필요한 결과 묶음

최종 보고서는 48개 필수 시나리오 및 추가 기반 시험의 실제 상태, 실패 이유, 증거 링크, 검증한 환경 조합을 포함한다. CoW 성능, Unity 준비 시간, 자동 발견성과 사용자 개입 횟수는 별도 측정표로 제공한다. 코드가 있는 기능과 실제 지원이 검증된 기능을 구분한다.
