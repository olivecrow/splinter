# 문서 지도

[Plastic 전체/선택 원본 반영](plastic-original-apply.md)의 현재 계약, [고정 범위](plans/plastic-original-apply.md), [자격](verification/plastic-original-apply.md)을 구분한다.

현재 구현은 아래 기능 문서에서, 축소한 후속 책임은 계획 문서에서 확인한다. 계획은 프로그램 구현·AI 사용 절차·필요 시 지원 검증·구현 제외를 구분한다. 검증 기록은 지정한 입력·후보의 근거이며 현재 코드의 자동 통과 선언이나 미완성 기능의 수가 아니다.

## 현재 계약과 사용법

| 문서 | 소유하는 내용 |
| --- | --- |
| [현재 기반](foundation.md) | 모듈·의존성·명령·빌드와 시험 |
| [Unity MCP 연결·응답 복구](unity-mcp-response-recovery.md) | 외부 Coplay 고정 패치의 준비·HTTP 대상·Codex 설정 조회 검사·Editor 종료 뒤 남은 연결 확인/필요한 복구·긴 코드 job·reload 상세 결과와 한정 자격 |
| [개발용 CLI](development-cli.md) | 기본 CLI에서 분리한 ReFS/baseline·diagnostic checkout·저장소 view·spike/probe의 진입점 |
| [한정 버전 AI 시작 안내](agent-start.md) | 개발 CLI의 전제·진입·정확한 경로/같은 작업 재개·명시적 종료·실패의 다음 행동 |
| [최소 AI 사용 스킬](../skills/splinter/SKILL.md) | 저장소 지침 원본·직접/독립 작업 판단·CLI 발견·반환 경로·같은 작업 재개·명시적 종료/실패 처리. 설치/자동 binding은 없음 |
| [VCS 없는 작업공간](vcs-independent-workspaces.md) | 명시적 소스/application 선택·파일 기준/결과·현재 형식·회수와 실제 검증의 구분 |
| [VCS 없는 프로젝트 기준본](plain-project-basis.md), [범위](plans/plain-project-basis.md), [검증](verification/plain-project-basis.md) | 승인한 whole-project 관리형 원본·VSS 없는 None source/Library 공통 기준·원본/A/B private 범위·전체/선택 게시·task 참조 회수와 마지막 일반 NTFS 복원. 파일/실제 Unity·비용/미자격을 구분 |
| [Plastic 사용 환경](plastic-compatibility.md), [호환 계획](plans/plastic-compatibility.md), [C: 실행 계획](plans/plastic-validation.md), [추가 T: 회수 계획](plans/plastic-reclamation-validation.md), [검증](verification/plastic-compatibility.md) | Splinter A/B의 관리 정보/등록 격리·무 checkout 파일 작업공간·독립 결과·T: 마지막 회수의 한정 마감과 C: 보류/외부 도구 발견 제한 |
| [일반 폴더 원본 반영](plain-folder-original-apply.md) | 읽기 전용 계획·고정 대상/intent·조건부 Windows 게시·중단/회수와 비용 경계 |
| [원본 반영 검증](verification/plain-folder-original-apply.md) | Core/fake·실제 일반 권한 NTFS 파일 시험과 작은 Editor 없는 제품 A/B 한정 마감·계측 |
| [VCS 독립 검증](verification/vcs-independent-workspaces.md) | 자동 파일/fake·Git 회귀와 실제 A/B·서비스 업데이트·동일 요청 회수 복구의 한정 판정/비용 |
| [일반 폴더 실제 실행 계획](plans/vcs-independent-validation.md) | 고정 fixture·서비스 업데이트/grant의 검토 가능한 계획·한 A/B 묶음과 중단 조건 |
| [NTFS 작업공간](ntfs-workspaces.md) | VSS identity·범위 저널·Git·권한·제어 파일 게시/읽기·복구·결과 보존·회수 |
| [Plastic 프로젝트 기준본](plastic-project-basis.md), [검증](verification/plastic-project-basis.md) | fixed changeset/revision·VSS 없는 범위 저장·공통 원본 Library·독립 파일 결과와 소유 회수. 구현/reader·native·실제 Unity 자격을 구분 |
| [Git 프로젝트 기준본](git-project-basis.md), [검증](verification/git-project-basis.md) | `git-snapshot`의 fixed Git commit/tree/blob·VSS 없는 WinFsp 범위 저장·소유 ref/fence·독립 결과/회수. ignored/Library 기준 재사용과 후속 Unity 자격을 구분 |
| [Git 원본 Library 참조](git-library-reference.md), [검증](verification/git-library-reference.md) | 명시적 Library prepare/status/restore·원본 관리형 mount·Git 소스와 불변 Library graft·작업 의존성·파일 이동/범위 복원과 MCP/RAM 자격 |
| [Git Unity 실제 실행 범위](plans/git-unity-validation.md), [검증](verification/git-unity-validation.md) | 현재 원본 Git/Unity의 새 기준 A/B·cold Library·exact HTTP MCP·리소스/씬/현재 상태 시험과 소유 회수. 준비 병목/동일 요청 복구와 이전 VSS 자격을 구분 |
| [명시적 폐기](workspace-discard.md) | Core/Runtime intent·Git/Windows provider·CLI·서비스 별도 authorization·중단 재개 |
| [Windows 저장소 권한 서비스](storage-privilege-service.md) | 설치 범위·고정 VSS IPC·사용자/경로/소유권·보호된 재시도 상태 |
| [Unity 알파 작업공간](alpha-workspaces.md) | 외부 도구로 편집·검증, 같은 작업 재개·독립 결과·통합과 Editor 수명 |
| [Unity CLI A/B 인계 검증](verification/unity-cli-handoff.md) | 정확한 반환 경로의 catalog/loaded 코드·A stop/release 뒤 B 후속 작업·두 독립 결과/회수의 작은 T: 한정 자격과 목록 누락/타임아웃 제한 |
| [저장소와 자원](storage-and-resources.md) | 선택적 ReFS 기준본·프로세스 그룹·자원 관측 |
| [실행 파일 묶음의 메모리 검증](verification/runner-bundle-memory.md) | 동일 빌드의 디스크 재사용·스트림 준비의 관리 할당·손상/소실 거부·동시 게시와 실제 프로세스 회귀 |
| [저널 정리 크기 회귀](verification/journal-compaction-size.md) | 작은 덮어쓰기의 메타데이터 증가 재현·더 작은 frame 선택·같은 입력의 NTFS 할당 비교 |
| [동일 private 쓰기](verification/duplicate-private-writes.md) | 승인된 private 범위의 중복 payload 억제·메타데이터/flush 유지·NTFS 할당과 작은 WinFsp client 검증 |
| [작은 Unity 동일 쓰기 공간 비교](verification/unity-duplicate-private-writes.md) | 고정 Runtime의 warm 편집/재개·Library/저널 분류·실제 파일 할당·동일 독립 결과와 회수·작은 관측 차이의 한계 |
| [부분 쓰기·버퍼·시작 개선](verification/resource-efficiency-followup.md) | private 덮어쓰기의 일치 앞뒤 재사용·요청 크기 버퍼·시작/소스 검증 분리와 고정 입력·로컬 Unity 자격 |
| [Library payload 압축 후보 선정](verification/library-block-compression-feasibility.md) | UH 비용 재계산·현재 Bee 4개 입력의 읽기 전용 block 모델/복원·다음 codec 범위 선정. 실제 저널/물리 절감 미자격 |
| [범위 payload codec](workspace-payload-codec.md), [검증](verification/workspace-payload-codec.md) | 공통 block API·raw/Brotli binary 표현·손상 거부·부분 읽기와 bounded buffer. 현재 [journal 연결](journal-payload-compression.md)의 block 부품; standalone 표본과 제품 자격을 구분 |
| [소스 검증](source-verification.md) | 변경 경로·hash cache·status freshness·현재 계측 형식 |
| [불변 후보와 검증 view](validation-view.md) | 개발용 후보 게시·같은 요청 중단 복구·검증된 Git 포인터 분리·고정 view·공통 Library |
| [개발용 소스 view 수명주기](validation-workflow.md) | 개발 CLI의 view·snapshot Library·정지 cache 정리·독립 결과·회수 의존성. Unity 검증은 외부 도구 책임 |
| [Unity 미리보기](unity-render-qualification.md) | fixture의 요청별 PNG·픽셀·해시·실패/복구 조건 |
| [Snapshot overlay 시험](snapshot-overlay.md) | 개발용 준비·mount·파일 API·장애·결과·회수 스크립트 |
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
| [Unity 실제 C/S 비교 범위](plans/unity-live-comparison.md), [검증](verification/unity-live-comparison.md), [HTML](../reports/unity-live-comparison-20261003.html) | Splinter 없는 Codex worktree와 제품 workspace의 L/A/B·실제 코드/에셋·파일 할당/메모리·0.7 실패/승인한 0.8·격리/재개/지속. 당시 구형 home 기록의 마지막 회수 Partial과 승인된 후속 해소를 구분 |
| [Unity 병렬 편집 확정 범위](plans/unity-parallel-editor-validation.md), [검증](verification/unity-parallel-editor-validation.md), [기존 S HTML](../reports/unity-parallel-editor-validation-20261003.html) | S와 Splinter를 사용하지 않은 C A/B의 MCP·PNG/importer·씬·현재 파일 테스트·전체/선택 반영·정리. 후속 실제 MCP 테스트 겹침·reload 결과 보존/재연결·C checkout 전체 회수와 파일/VSS/RAM 표본. 초기 실패/비용·전체 peak unknown·과거 Plastic pending 유지. 후속은 텍스트 기록 |

## 남은 설계와 검증

| 문서 | 용도 |
| --- | --- |
| [제품 요구사항](../plan.html) | 제품이 달성할 목표 |
| [구현 계획](plans/implementation-plan.md) | 프로그램/에이전트 책임·VCS 무관한 제품 요구·다시 확정한 일반 폴더 기본 수명주기와 원본 반영·외부 VCS 호환 처리의 후속 순서·마감한 M0 |
| [한정 마감: 일반 폴더 원본 반영](plans/plain-folder-original-apply.md) | 명시적 대상·기준/결과/현재 원본 비교·조건부 게시·동일 intent/부분 수행 복구·Editor 없는 NTFS A/B 한 묶음과 제외/종료 기준. 구현·일반 권한 파일 검증과 작은 Editor 없는 제품 A/B의 자격을 구분 |
| [원본 반영 제품 A/B 준비](plans/plain-folder-original-apply-validation.md) | 고정 작은 입력·seed·서비스/등록·승인한 관리자 Apply와 한 실행 묶음의 경계 |
| [검증 추적표](plans/validation-matrix.md) | M0와 기존 ID의 실제 증거 상태·후속 책임 분류·보존한 48개 카탈로그 |
| [NTFS 복구·지원](plans/ntfs-recovery-and-support.md) | NR-01/02 준비 복구·NR-03 공통 저널/로컬 kernel/제품 client 경계와 재부팅·전원·공간·파일 관문의 완료 기준 |
| [저장공간 우선 설계](plans/storage-priority-and-editor-modes.md) | SF-02~06·비용 판단과 WF-09~12/LB-01~05의 완료 범위·재검증 기준 |
| [VCS 프로젝트 단위 기준본 전환](plans/project-scoped-basis-and-reclamation-review.md) | Git·Plastic의 고정 버전 재사용·프로젝트 범위·기존 범위 저널 연결의 VP-01~05. Git/Plastic은 Library·고정 실제 Unity까지 한정 마감하고 VCS 없는 새 기준본은 별도 승인한 NP 관리형 원본 계약을 따름. 사전 CLI 접근과 실제 제품 자격을 구분 |
| [Plastic 프로젝트 기준본·Library 범위](plans/plastic-project-basis.md) | fixed changeset reader·VSS 없는 범위 저장·공통 Library와 실제 Unity A/B·MCP/리소스/씬/현재 상태 tests·독립 결과/회수/원본 복원의 PPB-01~06 한정 마감. 초기 준비/복원·MCP 응답/실행 겹침 제한과 원본 반영/기존 VSS/비VCS 후속을 구분 |
| [Git 원본 Library 참조·MCP/RAM](plans/git-library-reference.md) | 승인한 Library 전용 관리형 mount·원본/peer 복구와 GL-01~05 한정 마감. 실제 RAM 절감 미확인·MCP 실패 상세 소실·준비/복원 비용은 검증에 유지 |
| [대화별 설치·사용](plans/session-workflow-and-installation.md) | AI의 설치 연결/인계/도구 선택/종료 절차·외부 도구의 최소 경계·[후속 Unity CLI A/B 인계 범위](plans/session-workflow-and-installation.md#후속-범위-unity-cli의-ab-인계)·SW 증거 분류 |
| [저장소 권한 서비스](plans/storage-privilege-service.md) | 반복 UAC 없는 제한 서비스의 UA-01~04 |
| [병렬 비교 방법](plans/parallel-workflow-qualification.md) | C/S/Q 입력·작업·비용 비교를 재사용할 때 |

변경 이력은 Git에서 확인한다. 완료한 단계별 계획을 다시 유지하지 않는다. 후속 성능·지원 판단에 필요한 실측은 verification에 보존하며 README·AGENTS에 복제하지 않는다.

첫 버전의 실행 범위는 M0로 확정하고 NR/SF/SW 전체 미완료를 첫 버전 완료의 분모로 사용하지 않는다. [제품 범위 정리 검증](verification/workspace-product-scope.md)은 삭제한 DAG/Unity 검증 진입점·기본/개발 CLI 분리와 실제 자동 회귀의 경계를 소유한다.
