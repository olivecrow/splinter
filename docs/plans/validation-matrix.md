# 구현 계획 검증 추적표

상태: **원본 48개와 FD-01~10의 전체 통과 조건은 모두 NotRun**. 내부 알파의 실제 두 GUI Editor·재시작·compile/reload 검증은 아래 AA 항목, ReFS·기준본·자원 회수는 SR 항목으로 별도 집계한다. 이 좁은 범위의 성공을 공개 제품 시나리오 전체의 통과로 확대하지 않는다.

기준은 [기획서 14절](../../plan.html#section-14)의 필수 48개 시나리오다. 구현 순서는 [구현 계획](implementation-plan.md)의 P0~P7을 따른다. P0의 작은 실험 성공만으로 아래 시나리오 전체가 통과한 것으로 기록하지 않는다.

현재 시험 경계와 재현 명령은 [현재 기반](../foundation.md#빌드와-시험)에 있다. .NET 시험은 `artifacts/test-results/`의 TRX, 실제 저장소 Git 시험은 `artifacts/worktree-smoke/<runId>/result.json`에 결과를 남긴다. JSON/DAG/inspector 및 일반 checkout worktree 시험으로 CLI-01·GT-01·EX-01 등의 전체 조건을 통과했다고 보고하지 않는다. Unity/CoW 성공 경로의 실제 근거는 SR에 한정하며 지속 Host 시나리오는 미실행이다.

## 결과와 증거 규칙

- 상태는 `Passed`, `Failed`, `NotRun`, `Unsupported` 중 하나로 기록한다. 필수 지원 조합의 `NotRun`/`Unsupported`는 공개 통과로 집계하지 않는다.
- 각 실행은 run ID, 도구 commit, OS/볼륨/Git/Unity/패키지/Codex 버전, fixture/source hash, 시작/종료 manifest, 명령·이벤트, 결과 파일 hash를 기록한다.
- fixture 성공, 합성 대형 데이터 성공, 실제 프로젝트 성공을 구분한다. 단계별 통과 후에는 변경된 경계와 최종 통합 후보의 필수 시험을 다시 실행한다.
- 아래의 ‘단계’는 구현 책임과 주 검증 단계다. P7은 공개 대상 조합에서 전체 증거를 검토하는 단계다.

## 원본 필수 시나리오 48개

| ID | 검증 대상 | 단계 | 반드시 남길 증거 |
| --- | --- | --- | --- |
| FS-01 | 원본/A/B 독립 수정·삭제·재생성·rename | P0 S1 → P2 | 시작/종료 내용 hash, 다른 사본 불변 |
| FS-02 | 대형·작은·0바이트·경계 크기 파일 | P0 S1 → P2 | 내용/길이, 초기 payload·snapshot 보존·delta·메타데이터 할당, 실제 볼륨 조건 |
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
| GT-02 | NTFS 원본 + 스냅샷·변경분 root | P0 S1 → P2 | Git 조회/결과/회수, 원본 HEAD/staged 보존 |
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

## 내부 알파 검증

환경과 실제 실행 증거는 [알파 검증 보고서](../verification/internal-alpha.md)를 따른다. `git-checkout`, 작업별 새 Library, Unity `6000.3.22f1`, Coplay `10.2.0`, Windows 로컬에서 검증했다.

| ID | 검증 대상 | 상태 | 실제 근거 |
| --- | --- | --- | --- |
| AA-01 | Luna low 작업자가 필요 시 독립 GUI Editor 두 개 확보 | Passed | 별도 ID·경로·Git index·Library·PID로 동시 실행 |
| AA-02 | A의 소스 변경·컴파일 실패가 B와 원본에 전파되지 않음 | Passed | A CS0103 중 B/원본 HEAD·assembly hash·수정 시각 유지 |
| AA-03 | A 오류 중 B가 새 수정 내용을 컴파일 | Passed | A 오류가 남은 시점 B의 새 fingerprint validation 성공 |
| AA-04 | 다른 대화와 CLI에서 동일 작업 재개 | Passed | 동일 project/key의 ensure가 같은 작업공간·살아 있는 PID 반환 |
| AA-05 | Editor stop/start 후 같은 소스로 재검증 | Passed | 새 PID, 동일 workspace와 소스, 양쪽 validation 성공 |
| AA-06 | 검증된 결과를 development에 순차 통합 | Passed | A/B 결과 commit이 통합 HEAD의 조상이며 원본 checkout 유지 |
| AA-07 | 완료 시 명시적 Editor 종료와 worktree 회수 | Passed | 작업 PID 종료, 두 경로와 Git 등록 제거, 결과 branch/기록 유지 |
| AA-08 | 통합한 원본 Editor 새로고침·동작 확인 | Passed | 포커스·refresh·domain reload 후 원본 어셈블리에서 Increment(3)=4, Double(3)=6 |
| AA-09 | 긴 경로·중단된 회수·변경된 잔여 파일 보존 | Passed | 실제 A 부분 회수 재개, B 정상 회수, 46개 자동 시험에 경계 포함 |
| AA-10 | 작업 Editor 종료가 원본 Coplay 서버를 종료하지 않음 | Passed | 정상 원본 서버를 둔 별도 작업의 start/stop/release 전후 PID 유지, 이후 원본 MCP 호출 성공 |

## 저장소·프로세스 내부 알파 검증

2026-09-21의 [자원 검증 보고서](../verification/storage-and-resources.md)와 [기계용 증거](../verification/storage-and-resources.evidence.json)를 따른다. 실제 ReFS는 승인된 S: Dev Drive, Unity는 동일 fixture의 `6000.3.22f1`이다. SR은 아래 경계만 집계하며 FS/GT/WS/FD 전체 조건을 대신하지 않는다.

| ID | 검증 대상 | 상태 | 증거 범위 |
| --- | --- | --- | --- |
| SR-01 | 루트보다 오래 사는 자식의 종료와 원본 보존 | Passed | 실제 Windows Job Object·64MiB 자식·sentinel |
| SR-02 | 프로세스 잔류·소유권 불명 시 회수 차단 | Passed | fake Runtime과 상태/파일 검사 |
| SR-03 | CoW 불가 시 전체 복사 fallback 금지 | Passed | NTFS probe, Git 생성 전 거부 |
| SR-04 | 무-checkout·독립 index·변경 소스만 checkout | Passed | 실제 Git + fake clone |
| SR-05 | 검증된 기준본·변경 감지·소비자 보호 | Passed | fake 경계와 실제 활성 기준본 삭제 거부 |
| SR-06 | 실제 512MiB clone·중첩 clone·쓰기 격리 | Passed | ReFS extent API, 내용 hash와 볼륨 할당 표본 |
| SR-07 | CoW Library를 사용한 GUI Editor 두 개 | Passed | 실제 A/B 동시 실행·변경 격리·각각 compile 성공 |
| SR-08 | Unity 자식·감시·worktree 전체 회수 | Passed | 관측한 40개 소유 프로세스 종료, 자원 0, 경로·등록 제거, 원본 MCP 호출 |
| SR-09 | 다음 소스 커밋의 캐시 재사용·봉인·GC | Passed | 실제 소스 한 파일 변경, refs-cow 준비·검증, 새 캐시 삭제·기존 캐시 재사용 |
| SR-10 | CLI가 살아 있는 Editor의 출력 핸들을 기다리지 않음 | Passed | 실제 CLI 응답 및 Windows 익명 pipe 회귀 시험 |

## 공개 시 필요한 결과 묶음

최종 보고서는 48개 필수 시나리오 및 추가 기반 시험의 실제 상태, 실패 이유, 증거 링크, 검증한 환경 조합을 포함한다. CoW 성능, Unity 준비 시간, 자동 발견성과 사용자 개입 횟수는 별도 측정표로 제공한다. 코드가 있는 기능과 실제 지원이 검증된 기능을 구분한다.

## NTFS 스냅샷·변경분 필수 관문

전체 조건과 의도는 [SO 계획](ntfs-snapshot-overlay-spike.md)을 따른다. SR의 ReFS 기준본 시험과 별개이며 기존 Passed를 승계하지 않는다.

| ID | 조건 | 상태 |
| --- | --- | --- |
| SO-01 | NTFS 원본 이동·최초 전체 복제 없이 생성 | PassedFixture: VSS·초기 payload 0·볼륨 계수·실제 독립 Git HEAD/index |
| SO-02 | 전체 읽기에 따른 작업별 영구 복제 없음 | PassedFixture: 반복 hash·delta allocation 불변·read cache 0 |
| SO-03 | 큰 파일 일부 쓰기의 범위별 저장 | PassedFixture: 3개 크기 4KiB·mmap·truncate·rename·replace·저널 비용 |
| SO-04 | 원본 계속 수정 + A/B 시작 시점 스냅샷 유지 | PassedFixture: t0/t1 분리·원본/A/B 코드 33/103/203 실행 |
| SO-05 | 실제 Unity mmap·잠금·알림·컴파일과 증가량 설명 | PassedFixture: 추가 컴파일 104/204·세대/MVID·잠금 32/33·알림·Library/RAM/할당 |
| SO-06 | 기준/변경분 재연결·결과 보존·소유 자원 회수 | PassedFixture: provider/저널/quota/snapshot 소실·독립 결과·소유 자원 0. 재부팅/전원/모든 명령 경계 NotRun |

SO의 실제 원시 증거와 측정 제한은 [실행 기록](../verification/snapshot-overlay.md)을 따른다.
