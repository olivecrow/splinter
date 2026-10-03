# 검증 안내와 보존 근거

[Unity 실제 C/S 비교](unity-live-comparison.md)와 [HTML 보고서](../../reports/unity-live-comparison-20261003.html)는 Splinter를 전혀 사용하지 않은 Codex 앱 worktree와 제품 workspace의 로컬 L + A/B를 실제 실행했다. 지정 소유 파일 할당·독립 Editor/provider 메모리·격리/같은 작업 재개·B 지속은 확인했다. 전체 물리/VSS 귀속·최소 RAM은 unknown이며, 같은 home의 과거 기록 필수 필드 누락으로 마지막 snapshot 회수는 Partial다. 이전 SF/PV의 전체 지원 상태를 변경하지 않는다.

[Unity 병렬 편집 검증](unity-parallel-editor-validation.md)과 [HTML](../../reports/unity-parallel-editor-validation-20261003.html)은 S A/B의 호출별 MCP 대상·텍스처/importer·씬 저장/재로드·현재 상태 EditMode 테스트·A 전체/B 선택 반영·원본 Unity 확인과 완전 회수를 scoped-complete-with-limitations로 마감한다. MCP reload 뒤 명시적 재연결, 보호 검사 거부와 같은 작업의 명시적 release 재시도를 기록했다. 사용자가 승인한 73개 구형 기록의 hash 보존 이동 후 앞선 비교 A/B의 마지막 snapshot도 회수됐으며, 최초 비교의 실패/비용 기록은 유지한다. C worktree의 이번 다섯 기능 항목은 NotRun이다.

[물리 저장 단계 최종 검증](physical-storage-completion.md)은 최신 JM Runtime의 고정 T: A/B·초기 복제/읽기 hydration 0·같은 작업·소유 Editor·정리·독립 결과/회수·설명 가능한 잔여 파일 할당과 현재 장부/회귀를 scoped-complete-with-limitations로 마감했다. SF/NR의 별도 지원 확대·C: 회수 보류와 공유 VSS/전체 순간 peak unknown은 유지한다.

[메모리 단계 최종 검증](memory-completion.md)은 현재 Runtime의 압축 쓰기/부분·빈 read buffer·바이트/미게시/재생 의미·같은 실제 저널과 새 제품 A/B의 소유 비용/독립 결과/회수를 scoped-complete-with-limitations로 마감했다. 전체 RAM·native codec/순간 peak unknown을 유지한다.

[병렬 안정성의 선택 단위](stable-parallel-completion.md)는 잠금 경로 오류의 잘못된 busy 안내 수정, 실제 A checkpoint/release 중 B 조회/같은 요청·64개 쓰기와 독립 결과/회수를 scoped-complete-with-limitations로 마감했다. Editor 없는 CLI·파일 I/O 경계이며 전역 VSS 목록은 unknown, 기존 Unity/PV/대형/OS 자격은 승계하지 않는다.

일반 폴더 원본 반영의 현재 후보는 [별도 검증](plain-folder-original-apply.md)을 따른다. 일반 권한 파일 시험과 작은 Editor 없는 실제 제품 VSS·WinFsp A/B 한정 마감을 구분한다.

현재 기본 CLI는 NTFS workspace 수명과 Editor start/stop을 제공한다. 자체 refresh·컴파일/포커스 처리·내부 성공 gate는 제거했고 결과 보존은 checkpoint로 통일했다. 선택적 단일 view/Library cache·ReFS·probe는 [개발용 CLI](../development-cli.md)로 분리했다. 아래 기존 M0/WF/LB·시나리오 상태는 당시 후보의 증거이며 이번 후보의 native 실행 통과로 자동 승계하지 않는다. 현재 자동 회귀의 한도는 [범위 검증](workspace-product-scope.md)을 따른다.

## 현재 변경의 검증

[저널 기본값 메타데이터](journal-metadata-storage.md)는 현재 SJR2 의미·개별 flush를 유지하는 구현과 실제 Unity A/B의 보존된 저널을 사용한 오프라인 NTFS 전후 비교를 소유한다. 기존 payload 정리 효과·추가 직렬화 비용·새 native 수명주기의 미실행과 전체 workspace/RAM 미확인을 구분한다.

[압축 저널 제품 연결](journal-payload-compression.md)은 overlay v2/SJR2·candidate v3의 현재 회귀·작은 T: Plastic A/B·동일 입력 journal raw 대조의 실제 NTFS 할당·두 독립 결과/회수를 scoped-complete-with-limitations로 마감했다. 전체 공간/RAM·대형/OS 자격과 기존 C: 보류를 유지하며 독립 codec의 논리 표본/이전 native 자격을 승계하지 않는다.

[Unity CLI A/B 인계](unity-cli-handoff.md)는 기존 CLI/Pipeline·작은 T: Plastic 입력에서 정확한 반환 경로의 catalog/loaded 편집·A stop/release 뒤 B 후속 컴파일·두 독립 결과/마지막 snapshot 회수와 원본 보호를 scoped-complete-with-limitations로 마감했다. 프로그램 변경 없이 사용 절차를 보완했으며 목록 누락·첫 eval 타임아웃·MCP/다른 입력/Play/Test/build 미자격은 유지한다.

[Plastic 호환](plastic-compatibility.md)은 regular/static 원본의 관리 정보/등록 격리·독립 파일 결과의 자동 적합성과 현재 후보의 실제 A/B를 별도로 추적한다. 작은 별도 T: A/B의 마지막 snapshot 회수까지 scoped-complete-with-limitations로 마감했으며 선행 C: 보류·외부 CLI worker 발견 실패/전체 인계 미자격은 유지한다.

[VCS 독립 수명주기](vcs-independent-workspaces.md)는 일반 폴더의 자동 파일/fake·Git 회귀와 실제 VSS/Unity A/B·수정 후 동일 요청 회수의 scoped-complete-with-limitations를 구분한다. 후보별 실행·목록 누락·공간·미실행 경계를 유지한다. 새 상태는 v2이며 이전 상태를 자동 이관하지 않는다.

[한정 M0 납품 판정](m0-delivery.md)은 기존 개발 CLI·소스 스킬·[AI 시작 안내](../agent-start.md)의 한정 버전을 scoped-complete-with-limitations로 마감한다. 현재 복구 회귀와 과거 native의 source hash/변경 경계를 대조했고 현재 전체 native 중단 통과로 승계하지 않는다. NR/SF/SW 전체 미완료는 후속 지원 확대이며 M0의 진척 분모가 아니다.

[대표 AI A/B 사용·비용](representative-agent-workflow.md)은 독립 Editor 오류/수정·같은 작업 재개·독립 결과/dirty guard/통합·A release 중 B 지속·현재 컴파일 실패 B discard와 실제 회수를 한정 마감했다. 첫 seal/포커스 거부·같은 작업 재시도와 분 단위 준비·메모리 비용을 포함한다. 물리 peak/VSS 귀속은 unknown, native 강제 중단·OS 장애·ARM·대형 입력은 해당 실행에 포함하지 않았다.

[명시적 폐기 검증](workspace-discard.md)은 자동 자격과 승인한 서비스 Update·로컬 Editor 없는 dirty A/B의 실제 VSS 폐기·같은 요청 재개·peer 지속을 구분한다. 그 실행 자체는 scoped-complete-with-limitations이며 현재 M0의 대표 Unity 자격과 구별한다. SW-09 전체 Partial·강제 중단/SW-10 미실행은 유지한다.

[최소 AI 작업 연결](agent-workflow.md)은 스킬 원본 직접 읽기·CLI 발견·반환 절대 경로 편집·새 CLI 동일 작업 재개·explicit/direct와 명시적 폐기를 확인했다. Editor 없는 단위는 scoped-complete-with-limitations로 마감하며 SW-03/06 전체 Partial이다. 현재 Editor 자격은 위 대표 실행을 따르고 자동 스킬 발견/설치/binding·실제 busy/unknown 점유와 새/동시 대화로 확대하지 않는다.

표준 진입점은 `pwsh ./scripts/build.ps1`이다. locked restore, Release 빌드, 자동 시험을 수행하며 결과는 `artifacts/test-results/`에 기록한다. 선택적 ReFS는 `SPLINTER_TEST_REFS_ROOT`를 지정한 경우에만 실행한다. 현재 계약은 VSS v2 capture identity, 완전한 overlay 검증 tuple, `stable-journal-v1` 결과, 필수 계측 필드를 요구한다. 지원하지 않는 형식은 자동 이관하지 않고 보존 후 거부한다.

자동 시험은 현재 형식의 생성·읽기, 불완전한 입력 거부, checkpoint 중단 후 재시도, 소유권 불명·snapshot 소실·응답 유실에서의 보존을 다룬다. 개발 스크립트의 요청 생성 시험은 합성 영수증과 파일만 사용한다. 실제 VSS·WinFsp mount·Unity 실행을 대신하지 않는다.

## 지원 경계와 남은 관문

첫 실사용 버전의 완료 기준은 [M0](../plans/validation-matrix.md#첫-실사용-버전-m0)다. 아래 NR/SF/SW·48개 카탈로그는 기존 지원 근거와 미완료 조건을 보존한다. 전체 지원 확대를 M0의 완료 분모로 삼지 않으며, 기존 하위 성공을 새 대표 A/B 흐름의 통과로 승계하지 않는다. [제품 범위 정리 검증](workspace-product-scope.md)은 제외한 작업 그래프의 발견/거부 계약과 자동 회귀를 다룬다.

| 영역 | 확인한 범위 | 남은 작업·해석 제한 |
| --- | --- | --- |
| NI/LV 제품 NTFS | 작은 fixture의 VSS·Git·Windows x64 Unity·재연결·독립 결과·회수 | NR 전체 지원은 미완료. 실제 재부팅·전원·물리 공간 장애와 모든 명령 경계는 별도 |
| NR | 상태/저널 손상 보호, 파일 속성 거부, 현재 boot의 live identity, ARM 단일 workspace와 [준비 CLI 중단 26개](preparation-recovery.md) | NR-02는 scoped-complete-with-limitations. 게시 실패 원인 미확정·명시적 재개/회수와 fresh-key 재실행 구분. 나머지 파일·에셋, 쓰기/결과/회수 중단, ARM A/B와 OS 장애는 미완료 |
| NR-03 공통 저널 | [fake 불변 기준의 실제 자식 종료 58개](ntfs-recovery.md#nr-03-공통-저널-경계의-실제-프로세스-종료), 승인 prefix·flush 이후 transaction·tail·두 번째 재생 | 공통 단위 scoped-complete-with-limitations. 실제 WinFsp callback/mmap·VSS·동시 reader/writer·OS 장애는 미실행이며 NR-03 전체 InProgress |
| NR-03 로컬 kernel | [별도 후보의 실제 WinFsp callback/mmap/새 provider 19개](ntfs-recovery.md#nr-03-로컬-winfsp-callbackmmap새-provider-연결), 승인 데이터·동일 경로 재연결·미리 열린 다른 파일 Read·자원 회수 | 로컬 단위 scoped-complete-with-limitations. 최초 reader 종료 타임아웃·새 open/close 동시 응답 미자격 보존. 실제 VSS server/CLI·ARM·OS 장애 NotRun, NR-03 전체 InProgress |
| NR-03 제품 client | [별도 후보의 실제 VSS/CLI 외부 경계 13개](ntfs-recovery.md#nr-03-제품-vsscli의-클라이언트-경계), 같은 snapshot/delta/Git 등록·새 read proof/provider·독립 결과·일반 회수 | 제품 외부 경계 단위 scoped-complete-with-limitations. 준비 상태 게시 원인 unknown·Git text fixture 실패 보존. VSS 내부 callback/OS flush·동시 I/O·Unity/ARM·재부팅/전원/공간 NotRun, NR-03 전체 InProgress |
| NR-01 상태 쓰기 진단 | [실패 단계·게시 상태·기계용 오류와 실제 파일 시험 16개](state-write-diagnostics.md) | 진단 단위 scoped-complete-with-limitations. 실제 Windows 파일 I/O와 새 소유 diagnostic Git CLI·공통/schema/fake 범위. 이전 NR-02/03 상태 게시 실패 원인은 unknown, NR-01 전체 InProgress. VSS/WinFsp·전원/공간 관문의 자격과 구분 |
| PV-01/02 | 대표 fixture와 사용자 선택 비교 범위는 scoped-complete-with-limitations | PV-02 전체 Passed 아님. S4 FailedInputQualification, 소수 표본, 전체 팩 raw 입력 동등성 unknown |
| SF-01/02 | 변경 기반 소스 검증, 명시적 정리·중첩 예산·계측의 하위 범위 | 공통 저장소 최초 실행의 FailedPreservation은 유지. SF-02 전체 Partial, 대형 용량 목표 미달성 |
| SF-03/VC/VR | 개발용 작은 A/B/A, 정상 cold import와 재연결 | SF-03 전체 Partial·VR-03 Partial 유지. 큐/일반 임대 인수는 구현 제외, source 반환 병합은 AI 절차. 현재 제품 연결은 WF로 구분 |
| SF-03/VW | [공통 수명주기](../validation-workflow.md)의 지속 상태·영수증 재조정·fake 적합성 | 합성 파일/영수증이며 실제 VSS·WinFsp·Unity 지원 증거가 아님. 자동 실행 상태는 VW, 제품 연결 자격은 WF를 따름 |
| SF-03/VG | [등록 증거가 있는 관리 파일 분리](../validation-view.md#등록-증거가-있는-관리-파일-분리)의 실제 임시 Git 등록·합성 불변 트리 적합성 | 후보 v2 게시의 자동 시험 범위. 실제 provider 실행은 WF의 현재 후보 증거로 별도 판정 |
| SF-03/VP | [같은 요청 게시 복구](../validation-view.md#같은-요청의-게시-복구)의 영구 intent·부분 저널 prefix·완료 receipt 재조정 | 합성 트리·실제 임시 파일·예외 주입 범위. 실제 CLI 중단은 WF, 재부팅·전원/공간 장애는 NR의 별도 관문 |
| SF-03/WF | [제품 연결·복구](product-validation-workflow.md), [두 빈 cache](product-validation-cold-cost.md), [현재 후보의 일반 권한 cold/기존 Library](product-validation-unelevated.md)와 독립 결과·회수 | scoped-complete-with-limitations. 작은 fixture·준비/검증 재시작·포커스 재시도와 heartbeat 오류 기록의 제한 유지. 대형 용량·일반 cold 안정성·NR 전체를 증명하지 않음 |
| LB-01~05 | [실제 Library snapshot 재사용·정리·재증가와 회수](product-validation-unelevated.md) | 이번 Library 수명주기 범위 scoped-complete-with-limitations. 전체 순간 물리 peak·VSS 귀속은 unknown이며 SF-02/03 전체는 Partial |
| RI/RU/WI | 합성 또는 동일 닫힌 저널의 제한된 replay 비교 | warm cache·소수 표본. 누적 Editor 전체 개선·Library 물리 절감으로 환산하지 않음 |
| JW/FR | 고정 쓰기 계측과 작은 URP fixture의 요청별 유효 PNG 검사 | Windows 선행 직렬화·내구성 경계 유지. Unity 내부 첫 draw 원인 미확정, group flush 미선정 |
| SW-09 폐기 | [로컬 dirty A/B의 서비스 적용·동일 요청 재개](workspace-discard.md)와 [대표 Unity 실패 worker stop/폐기](representative-agent-workflow.md) | 하위 실행 scoped-complete-with-limitations, SW-09 전체 Partial. native 강제 중단·SW-10 미실행 |
| M0, SW-03/06 | [최소 AI 지침·direct](agent-workflow.md)·[대표 Editor 사용/종료·비용](representative-agent-workflow.md)와 [현재 복구 회귀/한정 납품](m0-delivery.md) | 한정 M0 마감·현재 모든 native/OS 장애 통과 아님. SW-03/06 전체 Partial, busy/unknown 점유·새/동시 대화·자동 설치/binding은 미실행 |
| SF-04~06, SW-01/02/04/05/07/08/10 | 보존한 시나리오·후속 책임 분류 | NotRun 유지. 대형/특수 지원은 필요 시 검증, 모드/설치/인계는 AI 절차, 자동 모드/전용 installer/binding은 구현 제외. 전체 대화/Editor 흐름은 미자격 |
| UA-01~04 | [제한 권한 서비스](storage-privilege-service.md)의 실제 설치·일반 CLI A/B 추가 UAC 0·회수·서비스 재시작/응답 유실 | 등록된 Windows x64 NTFS/Git fixture 범위 Passed. 일반 Unity·ARM·실제 전원/재부팅과 native VSS 호출 도중 강제 종료로 확대하지 않음 |

새 후보에서 필요한 실제 환경 시험을 실행하기 전에는 아래 기록을 현재 후보의 통과로 승계하지 않는다. GUI Unity·VSS·격리 디스크 시험은 정의된 범위와 소유 자원을 확인한 뒤 실행한다. 현재 원본·다른 작업·기존 결과 refs와 비소유 snapshot을 보존한다.

[WF-09~12와 LB-01~05](product-validation-unelevated.md)는 해당 기록이 고정한 후보의 실제 실행으로 마감했다. [NR-02](preparation-recovery.md)는 별도 최종 후보의 준비 경계이며 이전 Unity/Library 자격을 자동 승계하지 않는다. 실행 범위와 재검증 기준은 [저장공간 우선 계획](../plans/storage-priority-and-editor-modes.md#보존한-한정-범위-일반-권한-unity-검증-흐름), 남은 일반 지원·대형 용량·설치 관문은 [추적표](../plans/validation-matrix.md)가 소유한다.

## 후속 작업에 필요한 기록

아래 자료는 변경 이력이 아니라 재현 조건·실패 근거·비교 기준으로 보존한다. 각 파일의 후보·입력·날짜와 제한을 함께 읽는다. 원시 artifacts는 로컬에 있을 수 있으며 문서 정리로 지우지 않는다.

| 기록 | 보존 이유 |
| --- | --- |
| [한정 M0 납품](m0-delivery.md) | 현재 복구 계약 회귀·기존 native 소스 대조/재사용 한계와 시작 명령의 실제 관측, 개발 CLI 전제의 한정 완료 |
| [대표 AI A/B 사용·비용](representative-agent-workflow.md) | 작은 warm 입력의 오류 격리·동일 작업 재개·독립 결과/dirty guard/통합·실패 폐기, 종료 뒤 Library lock 쓰기/포커스 거부·같은 작업 재시도와 시간/파일 할당 제한 |
| [제품 범위 정리](workspace-product-scope.md) | 작업 그래프 제외의 명령/schema 발견·오류 응답 계약, 현재 자동 시험과 실환경 자격의 구분 |
| [제한 권한 서비스](storage-privilege-service.md) | 보호된 상태·IPC 경계·최초 설치·반복 UAC·서비스 중단/응답 유실을 구분한 완료 판정 |
| [VSS 보존 실패](storage-foundation.md), [삭제 차단과 복구](storage-recovery.md), [지연 회수](deferred-snapshot-reclamation.md) | exact-ID 삭제의 타 snapshot 영향, 격리 guard, 같은 작업의 명시적 재시도와 회수 순서 |
| [NTFS 제품 통합](ntfs-workspace-integration.md), [live identity](ntfs-live-vss-identity.md), [NR 복구](ntfs-recovery.md) | 실제 Windows/ARM 조합과 미완료 장애·파일 관문, 환경별 재현 조건 |
| [준비 CLI 중단 복구](preparation-recovery.md) | 같은 최종 후보의 실제 경계 26개·동시 재개·자식 실행 세대·독립 결과/원본 보존·회수, 게시 실패 원인 미확정과 명시적 복구 |
| [병렬 비교](parallel-workflow.md) | C/S/Q의 동일 입력 조건·결과 refs·Library/큐/전환 비용, S4 입력 실패와 표본 한계 |
| [초기 overlay 시험](snapshot-overlay.md), [ReFS·프로세스](storage-and-resources.md) | 파일 API·소유권·선택적 backend의 제한된 실환경 증거. NTFS 전체 지원으로 합산하지 않음 |
| [저널 정리](journal-compaction.md), [Unity와 정리 비용](journal-compaction-qualification-and-cost.md), [예산과 재증가](journal-budget-and-growth-qualification.md) | 중단 복구, 두 세대 겹침·전체 할당 peak와 Library 유효량, 재개 후 재증가 |
| [저널 정리 크기 회귀](journal-compaction-size.md) | 작은 덮어쓰기가 오히려 저널을 키우는 조건·기존 frame 유지·동일 합성 입력의 NTFS 파일 할당 비교 |
| [동일 private 쓰기](duplicate-private-writes.md) | byte 일치·전체 private 범위의 중복 저장 억제·metadata/flush·예산/복구·작은 로컬 WinFsp client 완료 |
| [작은 Unity 동일 쓰기 비교](unity-duplicate-private-writes.md) | 실제 제품 CLI/VSS/WinFsp/Unity의 warm 편집·재시작 한 쌍·저널/Library·동일 결과/회수와 관측 차이 한계 |
| [부분 쓰기·버퍼·시작 개선](resource-efficiency-followup.md) | private 일치 앞뒤 재사용·요청 크기 버퍼의 고정 입력 대조·로컬 시작/재시작·현재 검증/보존/회수 |
| [Library payload 압축 후보 선정](library-block-compression-feasibility.md) | 최근 UH 누적 비용·Bee 4개 파일/24개 조합의 정확한 복원·크기 모델. 다음 bounded codec 후보이며 제품/물리 공간 절감은 미자격 |
| [독립 범위 codec](workspace-payload-codec.md) | Core/fake·raw/Brotli·손상/trailing 거부·부분 읽기/임대 반환·동시 caller와 실제 Bee 표현 대조. product journal 미연결 |
| [공통 Library A/B/A](immutable-validation-view.md), [준비와 replay](validation-view-readiness-and-replay.md) | 불변 후보와 검증 세대의 분리, 정상 stop/remount/restart 비용·최초 import 제한 |
| [Library 수명주기와 일반 권한 실행](product-validation-unelevated.md) | 고정 snapshot Library·범위 쓰기·정리/재증가·cold 비교·15개 독립 결과와 회수·실패/복구 한계 |
| [제품 validation workflow](product-validation-workflow.md) | 실제 CLI 연결, 서로 다른 VSS와 한 view/cache, 코드·에셋·PNG·세대 분리, CLI 중단 복구, 독립 결과와 VHD 분리 후 검증 |
| [새 cache 반복과 전체 비용](product-validation-cold-cost.md) | 최종 후보의 두 빈 cache A/B/A, 2분 정상 종료 대기, 준비/검증 재시작 비용, 원시 응답 schema·실패 포함 할당·16개 독립 결과·소유 자원 회수 |
| [제어 파일 읽기 경합](control-file-read-races.md) | Windows 동시 Replace/guard/open의 부재·공유 오류, 유한 읽기 복구와 지속 실패·손상·owner 보존. 실제 WinFsp/VSS/Unity 재자격과 구분 |
| [Replay index](replay-index-cost.md), [실제 Unity 비교](unity-replay-comparison.md), [범위 index](cumulative-write-range-index.md) | 고정 입력·후보 hash·단계별 replay와 실제 Editor 비용을 비교하는 기준 |
| [Live 쓰기](live-journal-write-cost.md) | flush 내부 비용, callback 직렬화·대기/완료 경계와 향후 내구성 판단 |
| [첫 미리보기](first-render-qualification.md) | 첫 draw 미확정 원인, cold/reload/재시작과 실패 주입, PNG 해시 기반 판독 기준 |

시나리오의 실행 상태와 후속 책임 분류는 [검증 추적표](../plans/validation-matrix.md), 축소한 작업/종료 기준은 [구현 계획](../plans/implementation-plan.md), 비용 판단은 [저장공간 우선 계획](../plans/storage-priority-and-editor-modes.md)을 따른다. 구현 제외를 Passed로 바꾸지 않고 전체 NotRun을 개발 backlog로 읽지 않는다. 완료 단계별 계획과 반복 CHANGE LOG는 유지하지 않는다.
