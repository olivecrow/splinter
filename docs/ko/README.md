# 문서 지도

[English](../README.md)

[영문 소개](../../README.md) · [한국어 소개](../../README.ko.md) · [서드파티 고지](../../THIRD_PARTY_NOTICES.md)

기능·아키텍처·계획·검증 문서의 기본 언어는 영어입니다. 이 폴더는 한국어 버전이며 각 문서의 언어 링크로 전환할 수 있습니다. 원시 검증 자료와 과거 HTML 보고서는 기록된 내용과 언어를 보존합니다.

[Plastic 전체/선택 원본 반영](plastic-original-apply.md)의 현재 계약, [고정 범위](plans/plastic-original-apply.md), [자격](verification/plastic-original-apply.md)을 구분한다.

현재 구현은 아래 기능 문서에서, 후속 책임은 계획 문서에서 확인한다. 계획은 프로그램 구현·AI 사용 절차·필요 시 지원 검증·구현 제외를 구분한다. 검증 기록은 지정한 입력·후보의 근거이며 현재 코드의 자동 통과 선언이나 미완성 기능의 수가 아니다. 용어가 낯설면 [용어집](glossary.md)에서 의미와 서로의 차이를 먼저 확인한다.

## 현재 계약과 사용법

| 문서 | 소유하는 내용 |
| --- | --- |
| [최종화와 압축 해제 데이터 재사용](verification/lifecycle-finalization-optimization.md) | LF-01–05: 유한 경로/capsule RAM 재사용, 명시적 checkpoint와 release 통합, payload visitor cache, guard 회귀와 작은 fixture 비용. |
| [현재 Unity 병렬 작업·저장 비용 비교](verification/unity-current-comparison.md) | RC-01: 같은 입력의 일반 Codex/Git 워크트리 819.7s와 Splinter 935.2s, 현재 파일 할당, 동일 병합 결과, 측정 제외 구간·빈 Codex 폴더 잔여 한계. |
| [병렬 코드 작성과 공유 Unity Editor](verification/unity-shared-editor.md) | SE-01: Editor 하나가 독립 A/B 후보를 검증하는 계측 작업 합계 102.5s, B 대기 16.0s. 입력/실행 파일 대조 후 RC-01 Splinter를 재사용하며 Editor 대기열과 코드 병렬화·원본 복구·긴 작업 손익분기 미측정을 구분한다. |
| [시간·RAM 최적화](verification/time-memory-optimization.md) | Library 검증 범위 고정, 제한된 Git/저널 RAM cache, 독립 ensure 병렬 실행, 보호 회귀와 작은 입력 비용. 전체 Unity 시간은 후보별 [UF-01](verification/unity-final-lifecycle.md)·후속 [UT-01](verification/unity-time-memory.md)과 구분한다. |
| [용어집](glossary.md) | 작업/기준/저장·식별자·보존/반영/회수·응답과 검증 상태의 쉬운 설명과 소유 문서 |
| [현재 기반](foundation.md) | 모듈·의존성·명령·빌드와 시험 |
| [OS와 앱 확장 경계](extensibility-boundaries.md), [검증](verification/extensibility-boundaries.md) | 불변 경로 정책·Unix 권한 metadata·앱별 원본/동반 파일/생성 디렉터리 정책, 실제 다른 OS·앱 미구현과 합성 시험의 구분 |
| [보안 경계 검증](verification/security-boundaries.md) | Plastic private reader·서비스 상속 ACL·보호된 회수 잠금·짧은 IPC 수신의 자동/한정 읽기와 미실행 배포 경계, 과거 외부 MCP 인증 기록 |
| [코드 최적화 회귀](verification/code-quality-optimization.md) | 구조화한 다음 행동·파일 rename/복원 검색·고유 객체 검사·원본 계획 조상 집합·압축 블록 순회의 자동 검증과 실측 한계 |
| [선택적 외부 Unity 도구](unity-mcp-response-recovery.md), [검증](verification/external-tool-boundaries.md) | 에이전트의 도구 선택·대상/요청 확인·필요 시 연결 복구와 제품 결합 제거. 외부 패치 제작/운영·버전 gate는 제외 |
| [개발용 CLI](development-cli.md) | 기본 CLI에서 분리한 diagnostic checkout·저장소 view·spike/probe의 진입점 |
| [저장 view 계약 검증](verification/storage-view-contract.md) | 현재 후보·소스 세대·Library cache·독립 결과 계약과 schema/미지원 필드·같은 요청 복구·소유 회수의 자동 회귀 |
| [폐기한 ReFS 기준본 경로](plans/refs-retirement.md) | 대형 프로젝트의 최초 전체 기준본 준비 부담으로 폐기한 기능과 과거 근거의 경계 |
| [한정 버전 AI 시작 안내](agent-start.md) | 기본 CLI의 저장 경로별 전제·진입·정확한 경로/같은 작업 재개·명시적 종료·실패의 다음 행동 |
| [최소 AI 사용 스킬](../../skills/splinter/SKILL.md) | 저장소 지침 원본·직접/독립 작업 판단·CLI 발견·반환 경로·같은 작업 재개·명시적 종료/실패 처리. 설치/자동 binding은 없음 |
| [VCS 없는 작업공간](vcs-independent-workspaces.md) | 명시적 소스/application 선택·파일 기준/결과·현재 형식·회수와 실제 검증의 구분 |
| [VCS 없는 프로젝트 기준본](plain-project-basis.md), [범위](plans/plain-project-basis.md), [검증](verification/plain-project-basis.md) | 승인한 whole-project 관리형 원본·VSS 없는 None source/Library 공통 기준·원본/A/B private 범위·전체/선택 게시·task 참조 회수와 마지막 일반 NTFS 복원. 파일/실제 Unity·비용/미자격을 구분 |
| [Plastic 사용 환경](plastic-compatibility.md), [호환 범위](plans/plastic-compatibility.md), [검증](verification/plastic-compatibility.md), [C: 후속 회수](verification/vss-and-mcp-remediation.md#vr-01-정확한-기존-c-plastic-회수) | 관리 정보/등록 격리·무 checkout 파일 작업공간·독립 결과와 회수. C: 회수는 종결했으며 외부 도구 발견 제한은 유지 |
| [일반 폴더 원본 반영](plain-folder-original-apply.md) | 읽기 전용 계획·고정 대상/intent·조건부 Windows 게시·중단/회수와 비용 경계 |
| [원본 반영 검증](verification/plain-folder-original-apply.md) | Core/fake·실제 일반 권한 NTFS 파일 시험과 작은 Editor 없는 제품 A/B 한정 마감·계측 |
| [VCS 독립 검증](verification/vcs-independent-workspaces.md) | 자동 파일/fake·Git 회귀와 실제 A/B·서비스 업데이트·동일 요청 회수 복구의 한정 판정/비용 |
| [일반 폴더 실제 실행 계획](plans/vcs-independent-validation.md) | 고정 fixture·서비스 업데이트/grant의 검토 가능한 계획·한 A/B 묶음과 중단 조건 |
| [NTFS 작업공간](ntfs-workspaces.md) | VSS identity·범위 저널·Git·권한·제어 파일 게시/읽기·복구·결과 보존·회수 |
| [Plastic 프로젝트 기준본](plastic-project-basis.md), [검증](verification/plastic-project-basis.md) | fixed changeset/revision·VSS 없는 범위 저장·공통 원본 Library·독립 파일 결과와 소유 회수. 구현/reader·native·실제 Unity 자격을 구분 |
| [Git 프로젝트 기준본](git-project-basis.md), [reader/Editor 없는 검증](verification/git-project-basis.md), [Unity 검증](verification/git-unity-validation.md) | `git-snapshot`의 fixed Git commit/tree/blob·VSS 없는 WinFsp 범위 저장·소유 ref/fence·독립 결과/회수. ignored/Library 기준 재사용과 후보별 Unity 자격을 구분 |
| [Git 원본 Library 참조](git-library-reference.md), [검증](verification/git-library-reference.md) | 명시적 Library prepare/status/restore·원본 관리형 mount·Git 소스와 불변 Library graft·작업 의존성·파일 이동/범위 복원과 MCP/RAM 자격 |
| [Git Unity 실제 실행 범위](plans/git-unity-validation.md), [검증](verification/git-unity-validation.md) | 현재 원본 Git/Unity의 새 기준 A/B·cold Library·exact HTTP MCP·리소스/씬/현재 상태 시험과 소유 회수. 준비 병목/동일 요청 복구와 이전 VSS 자격을 구분 |
| [명시적 폐기](workspace-discard.md) | Core/Runtime intent·Git/Windows provider·CLI·서비스 별도 authorization·중단 재개 |
| [Windows 저장소 권한 서비스](storage-privilege-service.md) | 설치 범위·고정 VSS IPC·사용자/경로/소유권·보호된 재시도 상태 |
| [Unity 알파 작업공간](alpha-workspaces.md) | 외부 도구로 편집·검증, 같은 작업 재개·독립 결과·통합과 Editor 수명 |
| [Unity CLI A/B 인계 검증](verification/unity-cli-handoff.md) | 정확한 반환 경로의 catalog/loaded 코드·A stop/release 뒤 B 후속 작업·두 독립 결과/회수의 작은 T: 한정 자격과 목록 누락/타임아웃 제한 |
| [저장소와 자원](storage-and-resources.md) | 프로세스 그룹·자원 관측 |
| [실행 파일 묶음의 메모리 검증](verification/runner-bundle-memory.md) | 동일 빌드의 디스크 재사용·스트림 준비의 관리 할당·손상/소실 거부·동시 게시와 실제 프로세스 회귀 |
| [쓰기 경로 최적화](plans/latest-journal-write-optimization.md)·[검증](verification/latest-journal-write-optimization.md) | SJL4 payload/metadata capsule·mutation마다 한 durable flush·이전 현재본 회수·동일 PNG 반복 입력의 전후 비교 |
| [현재본 저널 ON](latest-only-journal.md)·[현재 범위](plans/latest-journal-write-optimization.md)·[검증](verification/latest-journal-write-optimization.md) | SJL4 현재 범위·단일 capsule 기록/개별 mutation durable flush/게시/회수·새 작업 기본 ON·기존 OFF/ON 유지·생성 중단 복구·v3 compact intent·실제 PNG 반복 저장 및 필요한 WinFsp/프로세스 경계 |
| [기준본 복귀 정리](journal-base-reversion.md), [이전 범위](plans/journal-base-reversion.md), [당시 검증](verification/journal-base-reversion.md), [이전 시간 최적화](plans/journal-base-reversion-optimization.md)·[당시 계측](verification/journal-base-reversion-optimization.md) | 명시적 compact의 기준 동일 범위 제거·원래/기대 hash·과거 v2 intent/append 구현의 OFF/ON 및 강제 중단/peer 근거 |
| [저널 정리 크기 회귀](verification/journal-compaction-size.md) | 작은 덮어쓰기의 메타데이터 증가 재현·더 작은 frame 선택·같은 입력의 NTFS 할당 비교 |
| [동일 private 쓰기](verification/duplicate-private-writes.md) | 승인된 private 범위의 중복 payload 억제·메타데이터/flush 유지·NTFS 할당과 작은 WinFsp client 검증 |
| [작은 Unity 동일 쓰기 공간 비교](verification/unity-duplicate-private-writes.md) | 고정 Runtime의 warm 편집/재개·Library/저널 분류·실제 파일 할당·동일 독립 결과와 회수·작은 관측 차이의 한계 |
| [부분 쓰기·버퍼·시작 개선](verification/resource-efficiency-followup.md) | private 덮어쓰기의 일치 앞뒤 재사용·요청 크기 버퍼·시작/소스 검증 분리와 고정 입력·로컬 Unity 자격 |
| [Library payload 압축 후보 선정](verification/library-block-compression-feasibility.md) | UH 비용 재계산·현재 Bee 4개 입력의 읽기 전용 block 모델/복원·다음 codec 범위 선정. 실제 저널/물리 절감 미자격 |
| [범위 payload codec](workspace-payload-codec.md), [검증](verification/workspace-payload-codec.md) | 공통 block API·raw/Brotli binary 표현·손상 거부·부분 읽기와 bounded buffer. 현재 [journal 연결](journal-payload-compression.md)의 block 부품; standalone 표본과 제품 자격을 구분 |
| [소스 검증](source-verification.md) | 변경 경로·hash cache·status freshness·현재 계측 형식 |
| [불변 소스 후보와 고정 view](source-view.md) | 개발용 후보 게시·같은 요청 중단 복구·검증된 Git 포인터 분리·고정 view·공통 Library |
| [개발용 소스 view 수명주기](view-workflow.md) | 개발 CLI의 view·snapshot Library·정지 cache 정리·독립 결과·회수 의존성. Unity 검증은 외부 도구 책임 |
| [Unity 미리보기](unity-render-qualification.md) | fixture의 요청별 PNG·픽셀·해시·실패/복구 조건 |
| [Snapshot overlay 시험](snapshot-overlay.md) | 개발용 준비·mount·파일 API·장애·결과·회수 스크립트와 내부 계측/I/O 모듈 |
| [준비 중단 절차](ntfs-workspaces.md#개발용-준비-중단-시험), [준비 복구 검증](verification/preparation-recovery.md) | 개발용 gate/harness와 같은 요청의 실제 CLI 종료·재개·원본 보존·회수 자격 |
| [Replay probe](replay-profiling.md) | 고정 합성 입력의 replay 비용 |
| [Write probe](write-profiling.md) | 고정 합성 쓰기·flush·대기 비용 |
| [저널 중단 시험](journal-recovery.md) | 합성 불변 기준의 58개 실제 자식 종료·외부 ACK·불완전 tail·재생 관문 |
| [로컬 kernel 복구 시험](kernel-recovery.md) | 실제 WinFsp/fake 기준의 callback·mmap·동시 reader·같은 경로 재연결 개발 관문 |
| [제품 쓰기 복구 시험](product-write-recovery.md) | 일반 VSS/WinFsp/Git CLI의 외부 client 경계·동일 작업 재연결·독립 보존/회수 개발 관문 |
| [검증 안내](verification/README.md) | 지원 경계, 검증 진입점, 보존한 실측 근거 |
| [최소 AI 작업 연결 검증](verification/agent-workflow.md) | source skill 직접 사용·반환 경로 편집·새 CLI 재개·direct/명시적 폐기 한정 실행 |
| [대표 AI A/B 사용·비용 검증](verification/representative-agent-workflow.md) | 독립 Editor의 오류/수정·동일 작업 재개·보존/통합/release와 실패 작업 discard·단계별 파일 할당/시간·재시도 제한 |
| [한정 M0 납품 판정](verification/m0-delivery.md) | 현재 복구 회귀·기존 native 소스 대조와 재사용 한계·실제 시작 명령 확인·한정 완료와 지원 확대의 구분 |
| [압축 저널](journal-payload-compression.md), [범위](plans/journal-payload-compression.md), [검증](verification/journal-payload-compression.md) | 저장/복원 길이·논리 fragment·SJR2/overlay v2/candidate v3·정리/복구·실제 A/B와 파일 할당의 한정 연결 |
| [상태 쓰기 진단 검증](verification/state-write-diagnostics.md) | workspace 저장 단계·게시 완료 여부·실제 파일 잠금과 기계용 오류 계약의 자격 |
| [저널 기본값 저장 범위](plans/journal-metadata-storage.md), [검증](verification/journal-metadata-storage.md) | 현재 SJR2 기본값 생략·명시적 정리/후보의 더 작은 메타데이터 선택·고정 실제 저널의 오프라인 NTFS 할당과 추가 비용. 새 native 수명주기 자격은 구분 |
| [물리 저장 단계 최종 범위](plans/physical-storage-completion.md), [검증](verification/physical-storage-completion.md) | 현재 SJR2 장부·정확한 task 파일 할당·최종 제품 A/B·정리/독립 결과/회수와 물리 저장 단계의 종료 경계 |
| [메모리 단계 최종 범위](plans/memory-completion.md), [검증](verification/memory-completion.md) | 압축 쓰기 중복 buffer·부분/빈 읽기 scratch·같은 실제 저널 대조와 소유 provider/Editor·공유 시스템 비용/회수의 종료 경계 |
| [병렬 안정성의 확정 범위](plans/stable-parallel-completion.md), [검증](verification/stable-parallel-completion.md) | 잠금 오류의 점유 오인 수정·실제 A 수집/회수와 B 조회/재개·파일 쓰기 겹침·독립 결과/소유 회수. Editor 없는 경계와 기존 자격 구분 |
| [이전 Unity 실측 시간(UF)](verification/unity-final-lifecycle.md), [데이터](../../reports/benchmarks/unity-final-lifecycle-20261008.json) | UF-01: `116fd44` 전체 1,083.0초·Editor 준비 216.1초·보존/회수 172.0초·Library 복원 126.0초, 최초와 같은 편집·입력 및 정상 종료/원본/소유 회수 검증. |
| [과거 Codex/공간 비교 기준 (RB)](verification/readme-unity-comparison.md), [데이터](../../reports/benchmarks/unity-workspace-comparison-20261007.json) | 실제 Codex 앱 워크트리와 당시 기본 ON Splinter·원본 Library 기준본의 동일 A/B 편집, 추가 파일 할당, 별도 준비·Editor 비용, 한정 컴파일·에셋 검증 |
| [Checkpoint/release 프로파일링](verification/checkpoint-release-profiling.md), [데이터](../../reports/benchmarks/checkpoint-release-profile-20261007.json) | 기존 Unity 단계 귀속·작은 Editor 없는 task; 반복 검증/재수집·저널 inventory·제곱 비례 fence 검색; 제품 최적화 없는 진단 복사본 |
| [Checkpoint/release 최적화](verification/checkpoint-release-optimization.md), [첫 데이터](../../reports/benchmarks/checkpoint-release-optimization-20261007.json), [후속 데이터](../../reports/benchmarks/checkpoint-release-followup-20261007.json) | CO-01–08: Git 묶음/통합 보존·seal 결과 재사용·fence/inventory 인덱스·구분한 live 계수·요청 wake; 작은 일반 배포본 비용·guard 회귀·소유 회수 |
| [Unity 수명주기 재측정](verification/unity-lifecycle-remeasurement.md) | 과거 UR-01–04: 같은 실제 입력의 최적화 후 A/B 시간·제한된 프로파일, 종료 시간 제한과 같은 작업 지속, 독립 결과·원본 복원, 남은 병목 |
| [수명주기 읽기·복원 최적화](verification/lifecycle-read-optimization.md) | LO-01–11: 제한된 Git proof·reader 세션 재사용과 기준 읽기 gate 분리, SJL4 검증·seal 읽기 통합, 경로 조회·변경 없는 파일 복원·cursor 묶음·전체 Library 복원·불변 목록 재사용, 한정 NTFS 비용과 손상·복구 회귀 |
| [과거 checkpoint/release 측정 (UC)](verification/unity-checkpoint-release-remeasurement.md) | UC-01: 커밋한 최적화 후보의 같은 실제 Unity A/B 입력, checkpoint/release 304.9초·Library 복원 180.9초, 종료 시간 제한과 결과·원본·자원 검증. |
| [시작 단계 저널 최적화](verification/journal-startup-optimization.md) | JS-01–04: 회수된 metadata 수명·파일별 object 인덱스·bounded footer 비교, 작은 이어 쓰기 비용과 손상·재연결 회귀. 결합 후보의 실제 준비는 UP-01 참조. |
| [준비 시간 최적화](verification/startup-time-optimization.md) | ST-01–04: 독립 불변 읽기, SJN2 이어 쓰기 metadata 페이지, 동일 bridge 재사용과 한정 경과 시간·guard 검증. 실제 준비는 UP-01 참조. |
| [실제 Unity 준비 프로파일](verification/unity-startup-profile.md) | 과거 UP-01: 최초 준비 766.9초, 거의 그대로인 약 447초 스캔, Git reader 대기·Bee 출력 비용, 정확한 입력·소유 회수 확인과 후속 우선순위 분석. |
| [준비 프로파일 후속 최적화](verification/startup-profile-optimization.md) | SU-01–05: 두 Git decoder의 독립 진행, 고정 저널 경로, Cleanup 잠금 제거, bridge assembly 분리와 내용으로 증명한 소스 시각; 한정 비용과 회귀 검증. |
| [SU 이후 Unity 준비 재측정](verification/unity-startup-remeasurement.md) | 과거 US-01: 준비 349.8초·스캔 16초·컴파일 준비 210초, 시작 판독 오류 복구와 서로 다른 runtime, 남은 ILPP/Library/소스 증명/게시 비용 및 원본 복원 확인. |
| [추가 준비 최적화](verification/startup-additional-optimization.md) | SA-01–05: Library 수명 고정·로컬 Close·단일 핸들 게시·단일 읽기 Git hash/ensure 증명·첫 시작 Bee 그래프 무효화, 한정 검증과 합친 후보의 실제 Unity 시간은 UF-01에 별도 기록. |
| [종료와 준비 단계 비용](verification/lifecycle-cost-optimization.md) | LC-01–03: 작은 쓰기 메타데이터 제한·root 전용 client 초기화·seal/회수 검사 재사용·작은 NTFS 비용/복구 근거, 합친 후보의 실제 Unity 시간은 UF-01에 별도 기록. |
| [Unity 실제 C/S 비교 범위](plans/unity-live-comparison.md), [검증](verification/unity-live-comparison.md), [HTML](../../reports/unity-live-comparison-20261003.html) | Codex worktree와 제품 workspace의 코드/에셋·파일 할당/메모리·격리/재개·회수 비교. 입력/버전과 실패·후속 복구의 근거는 소유 검증에서 구분 |
| [Unity 병렬 편집 확정 범위](plans/unity-parallel-editor-validation.md), [검증](verification/unity-parallel-editor-validation.md), [S HTML](../../reports/unity-parallel-editor-validation-20261003.html) | C/S A/B의 정확한 MCP 대상·리소스/씬/테스트·원본 반영·소유 회수와 비용 비교. HTML은 S까지이며 후속 실행과 제한은 텍스트 검증이 소유 |

## 설계 범위와 검증 기준

| 문서 | 용도 |
| --- | --- |
| [제품 요구사항](../../plan.ko.html) | 제품이 달성할 목표 |
| [구현 계획](plans/implementation-plan.md) | 프로그램/에이전트 책임·VCS 무관한 제품 요구·다시 확정한 일반 폴더 기본 수명주기와 원본 반영·외부 VCS 호환 처리의 후속 순서·마감한 M0 |
| [한정 마감: 일반 폴더 원본 반영](plans/plain-folder-original-apply.md) | 명시적 대상·기준/결과/현재 원본 비교·조건부 게시·동일 intent/부분 수행 복구·Editor 없는 NTFS A/B 한 묶음과 제외/종료 기준. 구현·일반 권한 파일 검증과 작은 Editor 없는 제품 A/B의 자격을 구분 |
| [원본 반영 제품 A/B 준비](plans/plain-folder-original-apply-validation.md) | 고정 작은 입력·seed·서비스/등록·승인한 관리자 Apply와 한 실행 묶음의 경계 |
| [검증 추적표](plans/validation-matrix.md) | M0와 기존 ID의 실제 증거 상태·후속 책임 분류·보존한 48개 카탈로그 |
| [NTFS 복구·지원](plans/ntfs-recovery-and-support.md) | NR-01/02 준비 복구·NR-03 공통 저널/로컬 kernel/제품 client 경계와 재부팅·전원·공간·파일 관문의 완료 기준 |
| [저장공간 우선 설계](plans/storage-priority-and-editor-modes.md) | SF-02~06·비용 판단과 WF-09~12/LB-01~05의 완료 범위·재검증 기준 |
| [VCS 프로젝트 단위 기준본 전환](plans/project-scoped-basis-and-reclamation-review.md) | Git·Plastic의 고정 버전 재사용·프로젝트 범위·기존 범위 저널 연결의 VP-01~05. Git/Plastic은 Library·고정 실제 Unity까지 한정 마감하고 VCS 없는 새 기준본은 별도 승인한 NP 관리형 원본 계약을 따름. 사전 CLI 접근과 실제 제품 자격을 구분 |
| [Plastic 프로젝트 기준본·Library 범위](plans/plastic-project-basis.md) | fixed changeset reader·VSS 없는 범위 저장·공통 Library와 실제 Unity A/B·MCP/리소스/씬/현재 상태 tests·독립 결과/회수/원본 복원의 PPB-01~06 한정 마감. 초기 준비/복원·MCP 응답/실행 겹침 제한과 원본 반영/기존 VSS/비VCS 후속을 구분 |
| [Git 원본 Library 참조·MCP/RAM](plans/git-library-reference.md) | 승인한 Library 전용 관리형 mount·원본/peer 복구와 GL-01~05 한정 마감. 실제 RAM 절감 미확인·MCP 실패 상세 소실·준비/복원 비용은 검증에 유지 |
| [대화별 설치·사용](plans/session-workflow-and-installation.md) | AI의 설치 연결/인계/선택적 도구 사용/종료 절차·제품과 외부 도구의 책임·[과거 Unity CLI A/B 인계 범위](plans/session-workflow-and-installation.md#후속-범위-unity-cli의-ab-인계)·SW 증거 분류 |
| [저장소 권한 서비스](plans/storage-privilege-service.md) | 반복 UAC 없는 제한 서비스의 UA-01~04 |
| [병렬 비교 방법](plans/parallel-workflow-qualification.md) | C/S/Q 입력·작업·비용 비교를 재사용할 때 |

현재 기능 문서는 구조·동작·진입점·제약만 설명한다. 검증과 계획에서 보존할 과거 기록은 지원 범위를 입증하는 후보/입력/환경·결과, 미해결 제한·재현 조건, 복구·소유권 판단과 비교에 필요한 근거다. 같은 근거는 소유 문서에 한 번만 두고 지도에서는 연결한다. 후속 판단에 쓰이지 않는 완료 순서·작업 일지·중복 시험 숫자·해결된 상태의 반복 요약은 제거하며 단순 변경 이력은 Git에서 확인한다. 완료한 계획에 남은 승인 범위·재현 조건·제외 조건이 필요하면 유지하되 현재 미완료 작업으로 표시하지 않는다. 원시 artifacts·독립 결과·snapshot은 문서 정리 대상으로 삭제하지 않는다.

첫 버전의 실행 범위는 M0로 확정하고 NR/SF/SW 전체 미완료를 첫 버전 완료의 분모로 사용하지 않는다. [제품 범위 정리 검증](verification/workspace-product-scope.md)은 삭제한 DAG/Unity 검증 진입점·기본/개발 CLI 분리와 실제 자동 회귀의 경계를 소유한다.
