# 구현 계획 검증 추적표

| ID | FR 첫 미리보기 검사 | 상태 |
| --- | --- | --- |
| FR-01 | cold 첫 PNG 재현과 API/셰이더/연속 draw의 원인 분리 | Passed, 작은 fixture. 진단 PNG의 중간 육안 판독 정정, Unity 내부 첫 draw 원인 미확정 |
| FR-02 | 단일 요청의 유효 PNG·요청별 경로/hash·최대 2회·명시 실패 | Passed, cold pass별 `[0,9457]`과 실제 이미지·기존 씬 복원 확인 |
| FR-03 | 새 cold/reload/정상 재시작 첫 요청과 실패 주입/복구 | Passed, 첫 요청 3건·빈 화면/누락 mesh/출력 충돌과 정상 복구 3건 |
| FR-04 | 독립 결과·원본/ref/타 snapshot·소유 자원 회수·VHD 분리 | Passed, 독립 결과 2개·원본 90개 파일/기존 ref 25개 보존·소유 자원 0·분리 후 결과 재검증 |

[FR 계획](first-render-qualification.md)의 범위는 [실행 기록](../verification/first-render-qualification.md)을 근거로 `scoped-complete-with-limitations`다. 공통 fixture의 화면 성공 계약이며 제품 validation API·Unity 내부 원인 해결·Library 공간 절감으로 확대하지 않는다. 과거 빈 PNG와 VR-03 Partial는 유지한다. 표준 제품 빌드는 303 통과·0 실패·선택적 ReFS 1 skip이다.

| ID | JW 최초 준비 쓰기 비용 | 상태 |
| --- | --- | --- |
| JW-01 | 고정 연산별 commit 비용·mutation 대기, nullable 계약·fake 기반 회귀 | Passed, 자동 303 통과·0 실패·선택적 ReFS 1 skip |
| JW-02 | 고정 live WriteProbe 3종·기준/계측 각 3회·내용/replay·파일 할당 | Passed, 공통 probe·18회 출력/replay/hash 보존; 작은 표본 |
| JW-03 | 새 작은 Unity 첫 준비·후속 수정·실제 로드·닫힌 저널 재연결 | Passed, 정의한 관측. 첫 PNG와 후속 reload 뒤 첫 PNG는 빈 화면 |
| JW-04 | 단계별 비용·독립 결과·원본/ref/타 snapshot·소유 자원 회수 | Passed, 원본 90개 파일/기존 ref 23개·타 snapshot·독립 결과 보존, VHD 분리 확인. 순간 peak/외부 gate 대기 unknown |

범위와 완료 기준은 [JW 계획](live-journal-write-cost.md), 실제 판정은 [JW 실행 기록](../verification/live-journal-write-cost.md)을 따른다. 선택한 범위는 `scoped-complete-with-limitations`다. Windows callback의 선행 직렬화 때문에 Runtime 단독 group flush는 미선정이며 대기 호출 수를 group commit 크기나 성능 개선률로 환산하지 않는다.

[WI-01~05](cumulative-write-range-index.md)는 [누적 write 범위 index의 합성·새 작은 Unity 실행](../verification/cumulative-write-range-index.md)을 `scoped-complete-with-limitations`로 마감했다. 표준 빌드의 자동 시험은 301 통과·0 실패·선택적 ReFS 1 skip이다. 같은 닫힌 저널 mount 개선과 전체 Editor 재개·Library 물리 절감을 구분한다.

| 하위 관문 | 범위 | 판정 |
| --- | --- | --- |
| WI-01 | 일반 write의 직전 extent 분포·탐색/이동/정렬 비용, nullable 계약 | Passed, 연산/frame 합계·압축 frame 분리·기존 계수 unknown 확인 |
| WI-02 | 고정 합성 4종의 병목 판별과 입력/상태/hash | Passed, 세 구현×각 3회, warm cache·소수 표본 제한 |
| WI-03 | 정렬된 extent의 영향 구간 국소 교체 | Passed, 겹침·hole·truncate·압축 fragment 재연결과 자동 회귀 |
| WI-04 | 새 작은 Unity 닫힌 저널 mount 각 3회·후보 Editor 1회 | Passed, 정의한 관측. 전체 Editor 개선 미입증, 첫 PNG 빈 화면·stop 재확인 유지 |
| WI-05 | 최초/반복 시간·파일 할당/VSS 분리·독립 결과/회수 | Passed, 원본 90개 파일/기존 ref 21개 보존·VHD 분리 뒤 결과 검증. 최초 delta 표본 일부·순간 peak unknown |

[RU-01~04](unity-replay-comparison.md)는 [작은 Unity의 기존/개선 비교](../verification/unity-replay-comparison.md)를 `scoped-complete-with-limitations`로 마감했다. 제품 코드 변경 없이 같은 RI 소스의 자동 300/0/1 증거를 재사용했다.

| 하위 관문 | 범위 | 판정 |
| --- | --- | --- |
| RU-01 | 공통 주변 계층·Core/Runtime 비교와 원본/할당 기준 | Passed, 두 작은 실행 묶음의 hash·복사 비용과 원본 기준 확인 |
| RU-02 | 빈 cache의 첫 준비·실제 코드/에셋 | Passed, 지연 compile 수집·첫 PNG 빈 화면·정상 quit 뒤 stop 재확인 제한 유지 |
| RU-03 | 동일 journal mount와 누적 입력 Editor 각 3회 | Passed, 정의한 관측. 전체 Editor 개선률 미입증·focus 복구/종료 재시도 포함 |
| RU-04 | 독립 결과·원본/기존 ref/타 볼륨 snapshot·회수 | Passed, 소유 자원 0·VHD 분리 후 독립 결과 fsck/내용 확인 |

[RI-01~04](replay-index-cost.md)는 [연산별 index 계측·rename 할당 개선과 offline 합성 비교](../verification/replay-index-cost.md)의 정의한 범위다. 당시 자동 시험은 300 통과·0 실패·선택적 ReFS 1 skip이었다. 실제 Unity 재연결의 후속 관측은 RU 기록을 따르며 RI의 합성 관측과 구분한다.

| 하위 관문 | 범위 | 판정 |
| --- | --- | --- |
| RI-01 | 최대 9개 연산의 replay 횟수·index 시간과 nullable 계약 | Passed, 자동·합성 관측의 합계와 신규 쓰기 제외 확인 |
| RI-02 | 범위 쓰기·겹침/resize·다수 파일 rename의 제한된 입력 | Passed, 같은 봉인 입력·독립 기대 내용과 hash·파일 할당 확인 |
| RI-03 | rename당 경로 접두사 한 번 생성 | Passed, 경계 형제·lazy child·whiteout·비ASCII/대소문자 경로와 재생 회귀 |
| RI-04 | 기존/계측/개선의 반복 비교와 검증 | Passed, warm OS cache·소수 표본의 합성 범위. 실제 Unity·첫 렌더·대형 PV로 확대하지 않음 |

[VR-01~04](validation-view-readiness-and-replay.md)는 [작은 Unity 실행](../verification/validation-view-readiness-and-replay.md)을 제한을 포함해 마감했다. 당시 자동 시험은 296 통과·0 실패·선택적 ReFS 1 skip이다.

| 하위 관문 | 범위 | 판정 |
| --- | --- | --- |
| VR-01 | 64KiB replay 읽기·단계별 계수·손상/미완료 tail 보존 | Passed, 정의한 자동/작은 fixture |
| VR-02 | 같은 launch의 실제 mount·최신 mounted 준비 응답 | Passed, 정의한 자동 및 실제 재연결 3건 |
| VR-03 | 빈 cache 첫 import·compile·실제 코드/에셋·첫 렌더 | Partial. 정상 import/stop 완료, focus 재시도 1회. 첫 PNG는 빈 화면, 동일 Editor 두 번째 PNG는 모델 표시, 원인 미확정 |
| VR-04 | 동일 저널 기존/새 3회씩 비교·파일 할당·독립 결과·회수 | Passed, warm OS cache·고정 순서의 제한된 관측. 원본·기존 다른 snapshot 보존과 소유 자원 회수 완료 |

[VC-01~04](immutable-validation-view-first-milestone.md)는 [정의한 합성·작은 Unity 범위](../verification/immutable-validation-view.md)를 `scoped-complete-with-limitations`로 마감했다. SF-03 전체는 Partial이다.

| 하위 관문 | 범위 | 판정 |
| --- | --- | --- |
| VC-01 | 유효 범위 후보·worker 후속 편집·불변 seal | Passed, 정의한 자동/작은 fixture |
| VC-02 | 공통 cache·세대별 source·private extent 이동 복구 | Passed, 정의한 자동/작은 fixture. 중단 5개 지점은 합성 |
| VC-03 | 실제 코드·에셋 A/B/A·독립 결과·원본 보존·회수 | Passed, 첫 import 수정·운영 개입 포함. 첫 A 렌더 미리보기는 빈 화면으로 전체 렌더 통과 아님 |
| VC-04 | 단계별 시간·논리 저널·파일 할당·공유 VSS 장부 | 정의한 계측 완료. 정상 cold import·생성 직전 Git 할당·미관측 peak·절감률은 미확인 |

[BQ-01~05](journal-budget-and-growth-qualification.md)의 예산 제품 경로와 반복 저장량 판단을 [정의한 범위에서 완료](../verification/journal-budget-and-growth-qualification.md)했다. 운영 재시도·합성 결과 보존 경로·공간 미계측 제한과 기존 SF/NR/PV의 별도 판정을 유지한다.

| 하위 관문 | 범위 | 상태 |
| --- | --- | --- |
| BQ-01 | 현재 후보·입력·생성 직전 공간 기준과 자동 회귀 | Passed, 정의한 BQ 범위·제한은 실행 기록 참조 |
| BQ-02 | 실제 NTFS mount의 중첩량 예산 거부·저장된 예산·명시적 동일 작업 재시도 | Passed, 정의한 BQ 범위·제한은 실행 기록 참조 |
| BQ-03 | 작은 Unity의 정리·동일 snapshot 재개·추가 편집과 loaded-code | Passed, 정의한 BQ 범위·제한은 실행 기록 참조 |
| BQ-04 | 반복 작업의 논리 유효/대체 기록 분류와 별도 물리 공간 장부 | Passed, 정의한 BQ 범위·제한은 실행 기록 참조 |
| BQ-05 | 독립 결과·원본 보존·소유 자원 회수와 다음 공간 절감 작업 선택 | Passed, 정의한 BQ 범위·제한은 실행 기록 참조 |

[SF-01~06](storage-priority-and-editor-modes.md#후속-검증과-선택-기준) 중 SF-01의 정의한 파일 단위 소스 검증은 Passed, SF-02 계측·명시적 저널 정리 하위 범위와 SF-03 전체는 Partial이며 SF-04~06은 NotRun이다. [실제 실행](../verification/storage-foundation.md)에서 기존 비소유 VSS 5개가 해제 중 소실돼 공통 저장소 1차 전체는 **FailedPreservation**이다. 소유 결과 보존·자원 회수와 이 실패를 구분한다.

새 VSS 해제 차단은 자동 9개 사례와 [격리 볼륨의 정의한 실제 시험](../verification/storage-recovery.md)을 통과했다. 최신·중간 요청 2건 차단과 오래된 순서 12개 회수에서 다른 ID·생존 파일을 보존했다. sentinel은 모두 소유 시험 snapshot이며 일반 비소유 백업·외부 동시 삭제·재부팅 지원으로 확대하지 않는다. 제어 파일 게시 충돌도 재현·수정했고 [지연 회수 검증](../verification/deferred-snapshot-reclamation.md)의 당시 자동 시험은 252 통과·0 실패·선택적 ReFS 1 skip이다. 임의 순서의 즉시 회수·자동 지연 회수는 미구현이다. [SF-02 명시적 저널 정리](journal-compaction-first-milestone.md)는 대체 범위의 제품 명령·세대 복구를 구현했고 [격리 실행](../verification/journal-compaction.md)에서 실제 할당 감소를 측정했다. [QJ-01~04 검증](../verification/journal-compaction-qualification-and-cost.md)은 빈 하위 폴더의 WinFsp 열거 수정과 작은 Unity 재개·로드 코드·정상 stop/결과/회수, 공간 peak·직접 정리 비용을 기록했다. 합성 생성 직전 Git 할당은 unknown이고 정리 중 최고 공간과 재개 후 재증가로 대형 프로젝트 용량 목표는 미달성이다. [PB 중첩량 예산](../verification/journal-compaction-peak-admission.md)은 자동·로컬 저널에서 후보 작성 전 거부와 같은 작업 재시도를 확인했고 후속 BQ의 실제 제품 VSS/Unity 경로는 운영 재시도를 포함해 검증했다. 쓰기별 flush 정책 변경과 재부팅·전원·공간 장애의 전체 지원은 미완료다. 기존 PV 제한과 NR/SW 관문을 유지한다.

상태: **개정 5판의 공개 48개와 필수 FD·SW-01~10의 전체 통과 조건은 모두 NotRun**. FD-04/07은 각각 DAG/DB 확장을 선택할 때 적용하는 조건부 시험이다. 내부 알파의 실제 두 GUI Editor·재시작·compile/reload 검증은 아래 AA 항목, ReFS·기준본·자원 회수는 SR 항목으로 별도 집계한다. 이 좁은 범위의 성공을 공개 제품 시나리오 전체의 통과로 확대하지 않는다.

설치·필요 시 사용·독립 대화·명시적 완료/폐기의 세부 관문은 [SW-01~10](session-workflow-and-installation.md#사용-흐름-검증-sw-0110)이다. 기존 실행 증거의 범위를 유지하며 개정된 UX 조건을 소급 통과로 표시하지 않는다. 기준은 [기획서 14절](../../plan.html#section-14)의 필수 48개 시나리오다. 구현 순서는 [구현 계획](implementation-plan.md)의 P0~P7을 따른다. P0의 작은 실험 성공만으로 아래 시나리오 전체가 통과한 것으로 기록하지 않는다.

현재 시험 경계와 재현 명령은 [현재 기반](../foundation.md#빌드와-시험)에 있다. .NET 시험은 `artifacts/test-results/`의 TRX, 실제 저장소 Git 시험은 `artifacts/worktree-smoke/<runId>/result.json`에 결과를 남긴다. JSON/DAG/inspector 및 일반 checkout worktree 시험으로 CLI-01·GT-01·EX-01 등의 전체 조건을 통과했다고 보고하지 않는다. Unity/CoW 성공 경로의 실제 근거는 SR/SO/NI에서 저장소 방식과 fixture 범위를 구분하며 지속 Host 시나리오는 미실행이다.

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

최종 보고서는 개정 5판의 48개 필수 시나리오·SW 하위 사례·적용 가능한 기반 시험의 실제 상태, 실패 이유, 증거 링크, 검증한 환경 조합을 포함한다. CoW 성능, Unity 준비 시간, 자동 발견성과 사용자 개입 횟수는 별도 측정표로 제공한다. 코드가 있는 기능과 실제 지원이 검증된 기능을 구분한다.

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

## NTFS workspace 1차 통합 검증

[이번 작업 범위](ntfs-workspace-integration.md)의 완료 기준이다. **일반 제품 CLI에서 실제 NTFS/Git/Unity·범위 저장·재연결·실패 보호를 검증하고 소유 VSS까지 자원 회수를 완료했다.** 실제 dirty 원본 통합 차단까지 확인해 NI-01~08의 이번 fixture 범위를 완료했다. 상세 근거는 [NI 기록](../verification/ntfs-workspace-integration.md)을 따른다. SO, AA/SR, 공개 48개와 별도 집계한다.

| ID | 검증 대상 | 상태 | 반드시 남길 증거 |
| --- | --- | --- | --- |
| NI-01 | 공통 계약·지속 상태·기계용 CLI | Passed: 자동/fixture | snapshot/delta/mount fake, schema/capabilities, 권한·미지원 입력 nextActions, 실제 capture→Ready |
| NI-02 | 같은 요청·중복 CLI·부분 실행 재개 | Passed: 자동/대표 재개 | fake의 동일 요청·충돌·단계별 응답 유실, 실제 새 CLI/제공자 재연결과 released key 동시 재호출·입력 충돌. 모든 실제 I/O 경계는 후속 |
| NI-03 | 제품 CLI의 NTFS 저장 계약 | Passed: 합성 fixture | 최초 payload·읽기 hydration 0, 비희소 3개 크기의 동일 4KiB 쓰기, 실제 할당·저널·VSS·Git 비용 구분 |
| NI-04 | 원본/A/B 독립 편집·Unity 검증 | Passed: 스크립트 fixture | 원본 33, A 오류 중 B 203, 최종 A 104/B 204, 다른 .meta 변경 시 validation 무효, 원본/MCP 보존. 긴 패키지 경로 제한 별도 |
| NI-05 | 새 CLI·Editor·제공자 재연결 | Passed: 대표 fixture | 동일 snapshot/delta/fingerprint, provider 종료 후 재연결, Editor 새 PID/domain의 실제 코드. 재부팅 NotRun |
| NI-06 | 결과 독립성·snapshot 의존성·공간 보호 | Passed: 자동/fixture | result ref/tree/seal 독립성, 미보존 결과 거부, quota/소실 보호, 실제 dirty 원본 통합 시 GitDirtyWorktree와 HEAD/index/diff/result ref 보존 |
| NI-07 | 실제 회수·부분 회수 재시도 | Passed: fixture | 열린 핸들 실패 후 재개, 앱/자식/제공자·메모리·핸들 0, mount/경로/Git/delta/소유 VSS 부재, 독립 결과·원본·기존 복원 지점 보존 |
| NI-08 | 최종 통합 후보 검증·증거·문서 | Passed: 이번 내부 알파 범위 | build.ps1 97 passed/0 failed/1 ReFS skip, 같은 source hash의 실제 run·추가 통합 차단·회수 증거, schema·fake·현재 계약/사용법·UTF-8 동기화 |

## NTFS 내부 알파 2차: 복구와 파일 지원 경계

[현재 작업 계획](ntfs-recovery-and-support.md)의 NR-01~08이다. **NR-01/03/05/07을 부분 구현했고 이전 후보의 로컬·VIVOBOOK 자동 시험 각각 139개 통과, 합성 입력의 실제 WinFsp 기본 I/O·파일 API 5개 거부·회수, 제품 VSS workspace의 정상 생성·77개 파일 읽기를 확인했다. 읽기 시험의 소유 자원은 모두 회수됐다. 마지막 상태가 releasing에 남는 결함도 수정본의 동일 workspace 재개로 released까지 복구했다. ARM Windows/x64 Unity의 단일 제품 workspace는 스크립트 검증 2건·독립 결과·회수를 통과했다. ARM A/B·재부팅 후 Unity·나머지 파일 특성·장애 시험은 별도 관문이다.** 실행 근거와 미실행 조건은 [NR 기록](../verification/ntfs-recovery.md)을 따른다. NI/SO/AA/SR의 성공을 NR 통과로 승계하지 않는다.

2026-09-22의 [live VSS 식별과 재개](ntfs-live-vss-identity.md) LV-01~08은 정의한 자동/실환경 수준에서 Passed다. [LV 기록](../verification/ntfs-live-vss-identity.md)은 새 로컬 후보의 180개 자동 통과, 실제 VSS 재연결·범위 쓰기, Windows x64 Unity 검증 3건과 최종 회수를 연결한다. 아래 NR 행은 더 넓은 남은 관문 때문에 InProgress/NotRun을 유지한다.

| ID | 검증 대상 | 상태 | 반드시 남길 증거 |
| --- | --- | --- | --- |
| NR-01 | 복구 상태·identity·영속 기록·기계용 응답 | InProgress: 공통 응답·상태 보존과 LV live identity/재개 통과 | unknown/소실/경로 불일치 구분, 복구 분류/schema/fake, 손상 JSON·잠긴 기록·고아 임시 파일 보존. VSS live identity·legacy·연결 세대는 LV 완료. 전체 중단 단계의 오류/행동은 남음 |
| NR-02 | 준비 단계의 실제 중단과 중복 요청 | NotRun | capture/반환 ID·Git 등록·delta 생성·mount·index·Ready의 부작용 전후 중단, 같은 key 동시 재개에 중복 자원 없음 |
| NR-03 | 저널·flush·승인 쓰기의 지속성 | InProgress: 손상 보존 자동 통과, fake 기준의 실제 journal 자식 종료·ACK 보존 3건 통과 | 정상/불완전 tail/완전 레코드 손상 구분, 승인 쓰기와 flushed mmap 보존, rename/replace/truncate, 초기 payload/hydration 0과 범위 할당. LV의 실제 WinFsp/VSS 4KiB 쓰기·flush·정상 provider 재연결 통과. 강제 중단 지점은 남음 |
| NR-04 | 정상 재부팅·강제 전원 종료 후 같은 작업 | NotRun | Ready/미수집 변경·checkpoint 완료·회수 도중의 전후 boot 식별, 같은 snapshot/delta/key, 새 원본과 시점 격리. 유효 기준은 실제 재개, 소실/손상은 보존/차단 |
| NR-05 | 파일 특성·ACL·경로 지원/거부 | InProgress: 속성 callback 자동 통과, ARM64 실제 WinFsp 기본 I/O·5개 API 거부·회수 통과 | fake 불변 트리의 실제 mount에서 ADS/hardlink/희소/압축/mount-point reparse 거부와 일반 NTFS 양성 대조 확인. VSS 입력·EFS·ACL 전제·다른 reparse 종류·한글/공백/긴 경로는 별도 검증 |
| NR-06 | 실제 공간 고갈·offline·snapshot 소실 | NotRun | VSS·delta/상태·Git 결과 공간을 나눈 격리 볼륨 시험, quota와 구분, unknown 보존, 늦은 쓰기·미보존 결과 보호, 다른 작업·기존 복원 지점 보존 |
| NR-07 | 부분 준비·결과·회수의 재조정 | InProgress: 마지막 VSS 해제 관측 자동 통과, 원격 동일 workspace의 released 복구·소유 자원0, LV 독립 결과·최종 회수 확인 | 사용자 쓰기 없는 부분 준비 회수 조건, commit/ref/manifest/seal 중단, unmount/Git/delta/VSS 해제 응답 유실, 결과 독립성과 실제 자원 0/부재 |
| NR-08 | 최종 후보·Unity 회귀·문서 | InProgress: 이전 ARM/x64 단일 검증 2건, 새 LV Windows x64 단일 검증 3건·재연결·결과·회수 통과 | 원격 workspace 소유 자원0과 canonical Editor/MCP 세션 정상 종료 확인. 같은 후보의 단일 정상 회귀이며 ARM Windows 원본/A/B·재부팅 후 loaded-code·fingerprint·validation·dirty 통합 차단과 전체 NR 완료는 남음 |

결과 보존 결함·공통 계약을 먼저 수정하고 아래 PV-01/02의 제한된 작업·비용 관문에서 확보한 증거와 선택 범위 마감 판정을 보존한다. NR-01~03/05/07의 나머지 계약·프로세스 시험과 NR-04/06 격리 환경 준비를 병행하고 NR-08 최종 지원 회귀로 이어 간다. 실제 장애 경계 목록과 데이터 지속성 oracle은 계획에 정의한다. 새 원시 증거는 `artifacts/ntfs-recovery/<runId>/`에 보존하고 workspace home은 짧은 별도 경로를 사용한다.

수집 전후 저널 동일성·이전 manifest 재검증/보존은 NR-01/07, 공통 mount 허용 규칙은 NR-05의 자동 회귀를 보강한다. 일반 status의 소스 읽기 생략과 기계용 freshness 응답은 CLI 계약/Git fixture로 검증한다. 실행 증거는 [검토 후 수정 검증](../verification/workspace-review-fixes.md)이며 NR 행의 전체 완료 상태는 올리지 않는다.

## 제한된 병렬 작업과 비용 검증

[확정한 PV 작업 범위](parallel-workflow-qualification.md)에 실행 순서·계측·완료 기준을 정의한다. PV-02는 실제 Codex 기본 worktree C·Coordination 단일 Editor Q·Splinter S와 모델·텍스처를 포함한 대표 프로필을 필수로 한다. PV-01의 S1 대표 수명주기는 복구·운영 개입을 포함해 독립 결과 보존과 마지막 소유 VSS 회수까지 완료했다. C1/S1/Q1과 후속 반복의 실제 증거는 [실행 기록](../verification/parallel-workflow.md)을 따른다. PV-02의 사용자 선택 반복·작은 프로필·합성 저장소 범위는 `scoped-complete-with-limitations`로 마감했다. S4 `FailedInputQualification`, 소수 표본, 과거 S1~S3 전체 팩 raw 동등성 unknown을 유지하며 PV-02 전체 Passed는 아니다. 실제 하위 판정·소유 자원 회수·원본 복원·합성 종료는 [최종 마감 영수증](../../artifacts/parallel-workflow/pv-20260923/final-closeout.json)에 연결한다. 이 증거를 설치/자동 binding/discard나 NR 전체의 통과로 합산하지 않는다. checkpoint는 각 작업의 최종 수집 때만 수행하고, 통합 후 원본 Unity 검증을 사전 통합 후보 검증과 구별한다.

| ID | 제한된 제품 효용 검증 | 상태 | 완료 증거 |
| --- | --- | --- | --- |
| PV-01 | 독립 대화 A/B의 여러 차례 작업·오류 격리·재개·A 선행 통합/회수·B 지속 | Passed — 정의한 대표 fixture·복구 포함 | S1의 대화별 요청·fingerprint·loaded-code·독립 결과, A 완전 회수 후 B 추가 편집, 최종 원본 검증과 소유 자원 회수를 연결. Git 결함 수정·수집 거부 처리·권한 재시도는 운영 개입으로 보존. 무개입 성공·자동 binding·NR 일반 지원과 구별 |
| PV-02 | 실제 Codex worktree의 독립 Editor C·Coordination 단일 Editor Q와 Splinter S 비교 | scoped-complete-with-limitations | 실제 수행한 C/S/Q 하위 비교·결과 보존·소유 자원 회수·원본 복원은 [실행 기록](../verification/parallel-workflow.md)과 [최종 마감 영수증](../../artifacts/parallel-workflow/pv-20260923/final-closeout.json)에 연결. S4는 FailedInputQualification; 대표 핵심 C2/C3·S2/S3·Q2/Q3 각각 2회, 작은 프로필은 방식별 1회 관측. 과거 S1~S3 전체 팩 raw 입력 동등성 unknown과 Library·캐시/종료점 차이를 유지하며 PV-02 전체 Passed가 아님. Splinter 최초 복제/읽기 hydration 0의 필수 계약은 유지하고 합성 파일은 별도 보조 근거로 해석 |

미지원 특성의 안전한 거부는 거부 계약의 Passed이며 그 특성 지원의 Passed가 아니다. VM 강제 전원 종료를 모든 물리 장치 장애 지원으로 확대하지 않는다. Unity 없는 재부팅 시험과 다른 머신의 Editor stop/start를 합쳐 재부팅 후 Unity 지원으로 집계하지 않는다. 필수 하위 사례가 남으면 NR 전체 완료로 표시하지 않는다.
