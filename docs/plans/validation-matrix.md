# 검증 추적표

현재 기능의 제한과 보존 근거는 [검증 안내](../verification/README.md)를 따른다. 이 표는 공개 요구와 남은 검증의 소유자이며 실행하지 않은 조건을 통과로 올리지 않는다.

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
| UX-01 | 명시 호출·점유 시 암묵 사용·불필요 시 직접 작업 | P3 → P7 | 사용/사용/미사용 증거, direct의 새 workspace 0, Codex 별도 worktree 0 (SW-03/04) |
| UX-02 | 프로젝트용/전역용 설치·Codex 연결·권한·설정 | P3 → P7 | 요청 범위 설치·실제 지침 발견, 반복 설치 멱등·미등록 프로젝트 불변 (SW-01/02) |
| UX-03 | 대화별 binding·여러 차례 수정·중단/중복 요청 재개 | P1 → P3 | 같은 key/workspace·기준/변경분 유지, 경로/권한 검증·stale 작성자 거부 (SW-04/06) |
| UX-04 | 독립된 두 대화 A/B의 병렬 작업 | P3 → P6 | 별도 조정 AI 없이 A 오류 중 B 진전, A 먼저 완료해도 B 계속 작업 (SW-05/07) |
| UX-05 | 사용자 의도에 따른 반영·보존·폐기와 회수 | P6 | 최신 원본 검증·통합/보존/명시적 discard 구분, 오류 작업 폐기·중단 재개·타 작업 보존 (SW-07~10) |
| UX-06 | 업데이트·도구 제거·대화 종료 | P7 | 기존 설정·자격증명·미완료 결과 유지, 대화 종료의 자동 통합/삭제 없음 (SW-02/10) |
| CLI-01 | JSON/JSONL·오류·schema | P1 | 전체 stdout 파싱, 접수/완료 구분, descriptor/schema 적합성 |
| CLI-02 | 비대화형·권한 부족 | P1 → P3 | 입력/모달 대기 없음, 필요한 행동 반환, sandbox 유지 |
| CLI-03 | 이벤트 재접속·관찰 취소 | P1 → P3 | cursor 재생/중복 제거, watch 종료 후 작업 지속 |
| CLI-04 | 현재 대화의 경로·권한·세대 바인딩 | P0 S3 → P3/P6 | 실제 파일 도구/명령/Editor 경로 일치, 잘못된 작업·세대 거부, 종료와 검증 구분 |
| GT-01 | 실제 worktree + CoW | P0 S1 → P2 | Git 등록·HEAD/index, .git 미복제, 전체 파일 재전개 없음 |
| GT-02 | NTFS 원본 + 스냅샷·변경분 root | P0 S1 → P2 | Git 조회/결과/회수, 원본 HEAD/staged 보존 |
| GT-03 | staged/unstaged/신규 에셋 snapshot | P2 | 일관된 S, .meta 포함, 원본 무변경, 상속 delta 중복 없음 |
| GT-04 | LFS·CRLF·attributes·filters | P0 S1 일부 → P2 | 실제 checkout 내용과 index, 누락 LFS 차단, 반복 다운로드 없음 |
| GT-05 | 생성/회수 단계별 중단 | P2 | 저널/Git/파일 재조정, 미수집 결과·다른 worktree 보존 |
| GT-06 | 외부 생성/이동/삭제·볼륨 분리 | P2 | attach/관찰/차단 구별, offline 보존, 관리 밖 경로 무삭제 |
| GT-07 | linked worktree·하위 Unity·공유 정책 | P2 | common dir/상대 경로, branch 충돌 거부, hook/config 보존 |
| GT-08 | Editor·작업·결과의 수명과 폐기 | P3 → P6 | Editor 정지 후 작업 유지, 정상 회수 시 독립 결과 유지, 명시 폐기 시 전용 결과만 제거 |
| EX-01 | Unity/Windows/Git/Codex 없는 Core | P1 | 비Git fake VCS·fake 앱·대화 연결로 수명주기, Runner 없이 검증 |
| EX-02 | 버전/capability 불일치 | P1 → P3 | 사전 차단, 거짓 Ready/자동 호환 실행 없음 |

## 기반 구현에서 추가할 회귀 시험

원본 48개를 구체화하는 추가 시험이다. 수를 늘리기 위한 API별 모방 테스트 대신 실제 데이터 손실·중복 실행·허위 성공의 경계를 검증한다.

| ID | 검증 대상 | 단계 | 통과 조건 |
| --- | --- | --- | --- |
| FD-01 | 두 CLI의 동일 작업 동시 접수 | P1 | 공통 상태 저장소·잠금, 같은 요청의 외부 부작용 1개. Host 선택 시 시작 중복도 검증 |
| FD-02 | 같은 requestKey에 다른 입력, 회수 후 키 재사용 | P1 | 명확한 충돌/기존 결과, 보존기간 내 재실행 없음 |
| FD-03 | lease 만료 후에도 살아 있는 작성자 | P1/P3 | 소유권 확인 전 새 작성자 미배정, stale 세대 명령 거부 |
| FD-04 | 선택적 DAG의 선행 결과 입력 반영 | 확장 선택 시 | B의 입력 snapshot에 A 결과 포함, 오래된 사본 미사용 |
| FD-05 | 검증 중 외부 파일 변경과 결과 재사용 | P3/P5 | 증거 stale, 새 revision 검증 전 통합 성공 차단 |
| FD-06 | CLI/관리 프로세스 중단 동안 소유 자식 관측 | P3 | supervisor 기록·작업 binding 유지, 정확한 재접속, 중복 앱/provider 없음 |
| FD-07 | 선택적 DB migration·일관된 백업·복원 | DB 전환 시 | 진행 중 operation과 requestKey/결과 참조 보존 |
| FD-08 | cleanup 계획 후 경로/소유권 변경 | P2/P6 | digest/정규 경로/볼륨/reparse 재검사로 잘못된 삭제 거부 |
| FD-09 | 통합 검증 후 원본 HEAD 또는 dirty 상태 변경 | P6 | 적용 거부, 후보 branch·검증 증거 보존 |
| FD-10 | 손실된 callback·이전 run XML·누락 artifact | P5 | 실제 완료/새 증거를 확인할 수 없으면 성공 차단 |

## NTFS 지원 확대 관문

| ID | 검증 대상 | 상태 | 완료 조건·제한 |
| --- | --- | --- | --- |
| NR-01 | 복구 상태·identity·영속 기록·기계용 응답 | InProgress: 공통 응답·상태 보존과 LV live identity/재개 통과 | unknown/소실/경로 불일치 구분, 복구 분류/schema/fake, 손상 JSON·잠긴 기록·고아 임시 파일 보존. VSS live identity·연결 세대는 정의한 LV 범위에서 확인. 전체 중단 단계의 오류/행동은 남음 |
| NR-02 | 준비 단계의 실제 중단과 중복 요청 | NotRun | capture/반환 ID·Git 등록·delta 생성·mount·index·Ready의 부작용 전후 중단, 같은 key 동시 재개에 중복 자원 없음 |
| NR-03 | 저널·flush·승인 쓰기의 지속성 | InProgress: 손상 보존 자동 통과, fake 기준의 실제 journal 자식 종료·ACK 보존 3건 통과 | 정상/불완전 tail/완전 레코드 손상 구분, 승인 쓰기와 flushed mmap 보존, rename/replace/truncate, 초기 payload/hydration 0과 범위 할당. LV의 실제 WinFsp/VSS 4KiB 쓰기·flush·정상 provider 재연결 통과. 강제 중단 지점은 남음 |
| NR-04 | 정상 재부팅·강제 전원 종료 후 같은 작업 | NotRun | Ready/미수집 변경·checkpoint 완료·회수 도중의 전후 boot 식별, 같은 snapshot/delta/key, 새 원본과 시점 격리. 유효 기준은 실제 재개, 소실/손상은 보존/차단 |
| NR-05 | 파일 특성·ACL·경로 지원/거부 | InProgress: 속성 callback 자동 통과, ARM64 실제 WinFsp 기본 I/O·5개 API 거부·회수 통과 | fake 불변 트리의 실제 mount에서 ADS/hardlink/희소/압축/mount-point reparse 거부와 일반 NTFS 양성 대조 확인. VSS 입력·EFS·ACL 전제·다른 reparse 종류·한글/공백/긴 경로는 별도 검증 |
| NR-06 | 실제 공간 고갈·offline·snapshot 소실 | NotRun | VSS·delta/상태·Git 결과 공간을 나눈 격리 볼륨 시험, quota와 구분, unknown 보존, 늦은 쓰기·미보존 결과 보호, 다른 작업·기존 복원 지점 보존 |
| NR-07 | 부분 준비·결과·회수의 재조정 | InProgress: 마지막 VSS 해제 관측 자동 통과, 원격 동일 workspace의 released 복구·소유 자원0, LV 독립 결과·최종 회수 확인 | 사용자 쓰기 없는 부분 준비 회수 조건, commit/ref/manifest/seal 중단, unmount/Git/delta/VSS 해제 응답 유실, 결과 독립성과 실제 자원 0/부재 |
| NR-08 | 최종 후보·Unity 회귀·문서 | InProgress: 이전 ARM/x64 단일 검증 2건, 새 LV Windows x64 단일 검증 3건·재연결·결과·회수 통과 | 원격 workspace 소유 자원0과 canonical Editor/MCP 세션 정상 종료 확인. 같은 후보의 단일 정상 회귀이며 ARM Windows 원본/A/B·재부팅 후 loaded-code·fingerprint·validation·dirty 통합 차단과 전체 NR 완료는 남음 |

결과 보존 결함·공통 계약을 먼저 수정하고 아래 PV-01/02의 제한된 작업·비용 관문에서 확보한 증거와 선택 범위 마감 판정을 보존한다. NR-01~03/05/07의 나머지 계약·프로세스 시험과 NR-04/06 격리 환경 준비를 병행하고 NR-08 최종 지원 회귀로 이어 간다. 실제 장애 경계 목록과 데이터 지속성 oracle은 계획에 정의한다. 새 원시 증거는 `artifacts/ntfs-recovery/<runId>/`에 보존하고 workspace home은 짧은 별도 경로를 사용한다.

## SF-03 공통 수명주기 적합성

아래는 [공통 계약·지속 상태](../validation-workflow.md)의 자동 시험 추적이다. 실제 VSS·WinFsp·Unity의 제품 연결이나 공개 48개 시나리오의 추가 통과로 집계하지 않는다. `ValidationWorkflowTests`는 새 coordinator/provider와 합성 파일 영수증을 사용한다.

| ID | 대상과 증거 | 상태 |
| --- | --- | --- |
| VW-01 | 입력·응답·상태 schema, current-only 거부, CLI capability의 `contract-and-fake` 경계 | Passed, 자동/fake 범위 |
| VW-02 | 후보 불변·후속 worker 수정, project/request ID 분리, 동시 동일 요청, intent/observed/completed 중단과 provider 응답 유실 | Passed, 자동/fake 범위 |
| VW-03 | A/B/A의 소스 세대 분리·동일 세대 재개, 다른 snapshot/cache·stale owner·busy/unknown 차단 | Passed, 자동/fake 범위 |
| VW-04 | 후보 seal·generation별 검증 증거, 잘못된 증거 거부, worker 성공 자동 승계 없음 | Passed, 자동/fake 범위 |
| VW-05 | 실패 검증 출력의 독립 보존, source freeze, 의존성이 있는 후보/미보존 결과/손상 결과의 회수 거부, 응답 유실 재시도 | Passed, 자동/fake 범위 |
| VW-06 | 초기/다음 flushed 상태 재개, read-only 관측, 부분·중복·누락·구버전·역행·거짓 release 상태의 보존 거부, 후보/view 기록 누락의 의존성 오인 차단 | Passed, 자동/fake 범위 |

2026-09-28의 `development` 작업 트리에서 `pwsh -NoProfile -File ./scripts/build.ps1`의 locked restore·Release 빌드와 전체 시험을 실행했다. [최종 TRX](../../artifacts/test-results/sf03-workflow-20260928.trx)는 388 Passed, 0 Failed, 1 Skipped이며, 이 중 `ValidationWorkflowTests` 64건과 capability/schema 경계 1건이 이번 범위다. 건너뛴 1건은 `SPLINTER_TEST_REFS_ROOT`가 필요한 선택적 실제 ReFS 시험이다. [실행 manifest](../../artifacts/test-results/sf03-workflow-20260928.manifest.json)는 기준 commit, 변경 코드/schema의 hash와 결과 hash를 고정한다.

실제 provider 연결·관리 파일 분리의 VSS/Unity 실행·snapshot/cache 공동 수명은 아래 WF 추적으로 구분하며 [남은 관문](storage-priority-and-editor-modes.md#sf-03-제품-연결과-남은-관문)을 따른다.

## SF-03 관리 파일 분리 적합성

개발용 후보 게시의 [현재 계약](../validation-view.md#등록-증거가-있는-관리-파일-분리)을 검증한다. 실제 임시 Git 등록과 합성 불변 트리를 사용하며 제품 VSS/WinFsp/Unity 연결과 구분한다. 형제 Unity fixture, 실제 snapshot, 설치와 외부 프로젝트는 이 자동 시험의 자원이 아니다.

| ID | 대상과 완료 기준 | 상태 |
| --- | --- | --- |
| VG-01 | 등록 owner·경로·역참조·HEAD·포인터/lock 검증, 원본·다른 worktree 무변경 | Passed, 임시 실제 Git 범위 |
| VG-02 | 검증된 전용 포인터만 후보에서 분리, 소스 rename/삭제/희소 범위 보존, hydration 0 | Passed, 합성 불변 트리/저널 |
| VG-03 | 변조·잘못된 snapshot/owner·디렉터리·관리 경계 rename·cache 쓰기의 비파괴 거부 | Passed, 자동 시험 |
| VG-04 | 후속 worker 편집과 후보 불변, 새 source 세대에서 원래 Git 포인터 노출 없음, 손상/중단 후보 보존 | Passed, 합성 불변 트리/저널 |
| VG-05 | 현재 후보 형식과 필수 증거, 별도 개발용 입력·출력 schema/capability, 전체 회귀 | Passed, 자동 시험 |

2026-09-28, 기준 commit `6bc433b` 이후 작업 트리의 `pwsh -NoProfile -File ./scripts/build.ps1`에서 locked restore·Release 빌드와 전체 420 Passed, 0 Failed, 선택적 실제 ReFS 1 Skipped를 확인했다. 새 projection 시험 31건과 입력/capability 시험 1건을 포함한다. [최종 TRX](../../artifacts/test-results/vg-projection-20260928.trx)와 [코드·결과 hash manifest](../../artifacts/test-results/vg-projection-20260928.manifest.json)를 보존한다. 이 시험은 실제 VSS/WinFsp/Unity 게시 증거가 아니며 현재 제품 연결은 WF로 별도 판정한다. SF-03 전체는 Partial이다.

## SF-03 후보 게시 복구 적합성

[개발용 게시 복구 계약](../validation-view.md#같은-요청의-게시-복구)의 범위는 합성 불변 트리·실제 임시 파일 I/O와 예외 주입이다. 실제 프로세스 강제 종료·재부팅·전원/물리 공간 장애 또는 제품 VSS/WinFsp/Unity 검증으로 집계하지 않는다.

| ID | 대상과 완료 기준 | 상태 |
| --- | --- | --- |
| VP-01 | intent/identity/저널/receipt flush·게시의 9개 경계, 같은 요청 재전송·동일 receipt | Passed, 예외 주입/파일 I/O |
| VP-02 | 부분 header/metadata/payload/checksum prefix 확인 후 추가만 수행, 손상 prefix 비파괴 거부 | Passed, 합성 저널 |
| VP-03 | 완전한 저널 뒤 worker 후속 편집/활성 writer와 분리, 미완성 게시의 원본 세대 변경 거부, 원본 payload 읽기 0 | Passed, 합성 불변 트리/저널 |
| VP-04 | 동시 게시 잠금, 다른 요청/경로/owner/예산/projection, 부분·중복·누락·손상·출처 불명 파일 보존 | Passed, 자동 시험 |
| VP-05 | 고정 요청 입력·intent·응답 schema/capability와 strict export/projection/view 전체 회귀 | Passed, 자동 시험 |

2026-09-28, 기준 commit `786d264` 이후 작업 트리에서 `pwsh -NoProfile -File ./scripts/build.ps1`의 locked restore·Release 빌드와 전체 483 Passed, 0 Failed, 선택적 실제 ReFS 1 Skipped를 확인했다. `CandidatePublicationTests` 61건과 고정 요청 누락을 실행 전에 거부하는 CLI 시험 2건을 포함한다. [최종 TRX](../../artifacts/test-results/vp-publication-20260928.trx)와 [코드·결과 hash manifest](../../artifacts/test-results/vp-publication-20260928.manifest.json)를 보존한다.

SF-03 전체는 Partial이다. 실제 workflow provider 연결·공동 snapshot 수명·VSS/Unity의 게시 복구는 이 VP 시험의 증거에 포함하지 않으며 아래 WF에서 별도 판정한다.

## SF-03 제품 workflow 연결

`validation` CLI와 Windows/Git/Unity 조합의 WF-01~05를 **scoped-complete-with-limitations**로 판정한다. VW/VG/VP 또는 이전 VC의 성공을 승계하지 않았다. 범위·완료 조건은 [확정 범위](storage-priority-and-editor-modes.md#sf-03-제품-연결과-남은-관문), 실행 증거와 실패·복구 제한은 [WF 기록](../verification/product-validation-workflow.md)을 따른다.

| ID | 검증 대상 | 상태 |
| --- | --- | --- |
| WF-01 | 실제 독립 VSS worker A/B의 Git 포인터 분리·게시·동일 요청 재전송 | Passed, 작은 fixture |
| WF-02 | 같은 경로·cache의 A/B/A 정상 stop/remount/start·실제 코드/에셋·세대별 출력 | Passed, 중단 복구 후 최종 A/B/A. 새 cold 무중단 자격은 없음 |
| WF-03 | source sequence에 묶인 실제 Unity validation, 실패/변경 증거 보존 | Passed, 실제 source 변경 거부·새 검증 복구·요청별 PNG |
| WF-04 | 후보·소스 독립 Git 결과, worker 후속 편집 보존, VSS 의존성 차단과 소유 자원 회수 | Passed, 독립 ref 8개·소유 자원/VSS 0·VHD 분리 후 재검증 |
| WF-05 | 실제 CLI 중단/응답 유실의 동일 owner/request 복구와 원본/기존 ref 보존 | Passed, publish intent 뒤 실제 CLI 종료·같은 요청 복구/재전송. 전체 명령 경계·재부팅은 제외 |

WF-01~05 후보의 2026-09-28 locked restore·Release 빌드·전체 자동 시험은 511 Passed, 0 Failed, 선택적 실제 ReFS 1 Skipped다. [TRX](../../artifacts/test-results/wf-product-20260928.trx)와 [코드·실행 파일·실제 증거 manifest](../../artifacts/storage-foundation/wf-20260928/verification-manifest.json)를 보존한다. 해당 formatter의 기록 재검증과 실제 VSS/Unity 실행의 시점 차이는 WF 기록의 제한을 따른다.

## SF-03 새 cache 실행과 비용

최종 후보에서 두 빈 cache의 A/B/A와 전체 비용·독립 결과·회수를 실행하여 WF-06~08을 **scoped-complete-with-limitations**로 판정한다. 각 세대는 렌더 준비의 정상 종료 후 재시작하여 검증하며, 추가 시작/종료와 앞선 두 실패 시도의 비용을 포함한다. [실행 근거와 제한](../verification/product-validation-cold-cost.md)을 따르며 기존 WF-01~05의 결과나 formatter 재직렬화 검사를 승계하지 않았다. 공개 48개 시나리오의 추가 통과로도 집계하지 않는다.

| ID | 검증 대상 | 상태 |
| --- | --- | --- |
| WF-06 | 같은 최종 바이너리로 독립된 빈 cache 두 차례의 실제 A/B/A, 정상 전환·코드/에셋/PNG·세대 격리와 실행 중 원시 응답 schema | Passed, 같은 최종 후보의 두 빈 cache A/B/A. 세대별 준비/검증 Editor 각 1회, schema·세대 격리·PNG 확인. t2의 Editor 시작 전 준비 요청 재시도 제한 유지 |
| WF-07 | 두 trial의 준비→보존→회수 전체 추가 파일 할당·관측 최고치·VSS 공유 관측·단계 시간·cache 재증가와 계측 한계 | Passed, 소유 파일 할당·관측 최고치·단계 시간·실패/재시작 비용. 순간 물리 peak와 공유 VSS workspace 귀속은 미확정 |
| WF-08 | 새 실행 결과의 독립 Git 보존·회수 후 내용/hash, 원본/기존 refs/비소유 snapshot 보존과 소유 자원 0/부재 | Passed, 정상 결과 14개와 앞선 결과 2개의 독립 보존·VHD 분리 후 재검증·소유 자원 0/부재 |

최종 locked restore·Release 빌드와 자동 시험은 515 Passed, 0 Failed, 선택적 실제 ReFS 1 Skipped다. [최종 TRX](../../artifacts/storage-foundation/wf-cold-20260928/final-candidate.trx)와 [최종 후보 manifest](../../artifacts/storage-foundation/wf-cold-20260928/final-candidate-manifest.json)를 보존한다.

## Windows IPC 상태 읽기 경합

[확정 범위와 완료 조건](storage-priority-and-editor-modes.md#이번-범위-windows-ipc-상태-읽기-경합)을 **scoped-complete-with-limitations**로 판정한다. [실행 근거](../verification/control-file-read-races.md)에 따르며 WF의 실제 cold 반복이나 공개 48개 시나리오의 추가 통과로 집계하지 않는다.

| ID | 검증 대상 | 상태 |
| --- | --- | --- |
| IPC-01 | 수정 전 Windows 동시 게시/읽기 재현과 과거 실패 비교 | Passed, 3,000회 게시에서 guard/open의 부재·공유 오류 재현. 과거 t2의 정확한 발생 지점은 미확정 |
| IPC-02 | 유한 읽기 복구와 지속 실패·취소·손상·소유권 보존 | Passed, 최대 1초 guard/open 재시도·선택적 요청 polling·추가 회귀 12개 |
| IPC-03 | 수정 후 실제 Windows 경합·합성 종료 관측/같은 요청·전체 빌드 | Passed, 3,000회 게시/22,831회 읽기의 예외·null·손상 0. 전체 527 Passed/0 Failed/1 Skipped. 실제 WinFsp/VSS/Unity 재자격은 NotRun |

## 제품화와 비용 관문

- SF-02/03은 Partial, SF-04~06은 NotRun이다. 완료 기준은 [저장공간·Editor 계획](storage-priority-and-editor-modes.md)을 따른다.
- SF-03의 공통 수명주기 계약은 위 VW 자동 적합성으로 추적한다. 실제 제품 VSS·WinFsp·Unity 연결은 WF의 현재 후보 증거로 따로 판정한다.
- SW-01~10 설치·binding·명시적 discard와 UA-01~04 권한 서비스는 NotRun이다. 각각의 소유 계획에서 검증한다.
- PV-01/02의 사용자 선택 범위는 scoped-complete-with-limitations다. 전체 PV-02 Passed로 표시하지 않고 S4 입력 실패·소수 표본·raw 입력 동등성 unknown을 유지한다. [비교 방법](parallel-workflow-qualification.md)과 [실측](../verification/parallel-workflow.md)은 후속 비교의 입력을 고정하기 위해 보존한다.
- 현재 코드의 계약 변경은 필수 형식 생성·읽기, 지원하지 않는 형식의 비파괴 거부, 같은 버전의 중단 재시도를 함께 검증한다. 자동 시험과 실제 VSS/Unity 실행을 구별한다.
