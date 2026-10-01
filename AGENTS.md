# Splinter 작업 지침

- 먼저 `README.md`와 `docs/README.md`를 읽고 해당 작업의 문서만 이어서 읽는다. `plan.html`은 제품 요구사항, `docs/plans/`는 목표와 남은 작업, `docs/foundation.md`는 현재 구현이다.
- `main`은 출시용, `development`는 개발용이다. 일반 개발과 통합은 `development`를 기준으로 한다. 원본과 다른 작업자의 미커밋 변경을 보존한다.
- 주 사용자는 각 대화의 AI 에이전트다. `docs/plans/session-workflow-and-installation.md`의 필요 시 사용 → 같은 작업 지속 → 명시적 반영/보존/폐기를 기본 사용 모델로 삼는다. 첫 실사용 버전은 `docs/plans/implementation-plan.md`의 M0이며 대표 A/B 사용·비용 단위 뒤 다음 범위는 한정 버전 납품 정리다. 별도 조정 AI·Runner·DAG·daemon/DB·범용 Unity 저작/Play/Test Runner는 제품 범위에서 제외한다.
- 각 대화의 CLI 사용 지침 원본은 `skills/splinter/SKILL.md`다. 새 명령의 schema를 읽고 반환 `data.workspace.path`를 파일 도구·working directory로 사용한다. 같은 project/key/id/home·base/digest를 인계에 보존하며 자동 Codex binding·전역 설치·임의 파일 편집의 대화별 배타 잠금을 구현한 것으로 읽지 않는다.
- 사용자가 병렬 작업을 명시하거나 다른 작업의 점유로 독립 실행이 필요할 때 Splinter workspace를 사용한다. 불필요하면 직접 작업하며, Splinter 병렬 경로에 Codex 별도 worktree를 중첩하지 않는다. 제품 내부 Git linked worktree 계약은 유지한다.
- 대화·응답·Editor 종료, 임대 만료·타임아웃·디스크 압박은 통합이나 삭제의 승인이 아니다. release는 독립 결과를 보존한다. 명시적 discard는 NTFS task의 provider/CLI를 연결했으며 실제 자격은 폐기 검증을 따른다. 일반 제품 설치·자동 binding은 미구현이다. VSS 전용 권한 서비스의 실제 자격은 UA 추적표를 따른다.
- 필수 저장소 요구는 원본 NTFS 위치·파일시스템 유지, 최초 전체 데이터 복제·읽기 hydration·파일 전체 copy-up fallback 금지, 범위 변경 저장이다. 원본은 작업 중 계속 수정 가능해야 한다. ReFS는 선택적 backend이며 NTFS의 이관 fallback이 아니다.
- Core에 Unity·Win32·Git·Codex 전용 API를 넣지 않는다. 공통 계약과 fake 적합성 시험부터 연결한다. Host는 읽기 전용 in-process 조합 계층이며 daemon/DB writer가 아니다.
- 에이전트 진입점은 `scripts/splinter.ps1`, 필수 검증은 `pwsh ./scripts/build.ps1`이다. 의존성 변경 시 중앙 버전·해당 lockfile을 함께 갱신하고 locked restore로 확인한다.
- 현재 버전 계약 하나만 지원한다. 구버전 자동 이관·호환 fallback을 추가하지 않는다. 지원하지 않거나 손상된 상태는 보존 후 명시적으로 거부한다. 같은 버전에서의 중단·응답 유실·재시작 복구는 유지한다.
- 계약 변경 시 입력·출력 schema, fake, 적합성 시험, 해당 기능 문서·검증 추적표를 함께 갱신한다. 새 모듈이나 실행 명령은 이 파일·문서 지도·capabilities에 현재 상태를 반영한다.

## 현재 기능의 소유 문서

- 명시적 폐기는 `docs/workspace-discard.md`를 따른다. Core/Runtime·schema·Git/Windows provider·CLI와 제한 서비스의 별도 authorization을 연결했다. 회수 요청 전에 최신 delta/mount 부재·소유 자원 0 관측을 durable 상태에 게시한다. provider 없이는 intent·정지 전 거부하며 결과 보존으로 위장하거나 기존 release gate를 약화하지 않는다. 실제 Editor 없는 폐기·최소 AI·대표 Unity A/B의 한정 자격은 `docs/verification/workspace-discard.md`·`agent-workflow.md`·`representative-agent-workflow.md`를 따른다. 대표 사용은 오류 격리·같은 작업 재개·독립 결과/dirty guard/통합·A release 중 B 지속·실패 B discard와 비용 판단을 포함한다. 첫 seal/포커스 거부 뒤 같은 작업 재시도를 기록했고 전체 물리 peak/VSS 귀속은 unknown이다. native 강제 중단·일반 설치/binding·대형/M0/SW 전체로 승계하지 않는다. 다음은 기존 근거를 대조한 한정 납품 정리이며 새 장애 행렬을 자동 반복하지 않는다.

- NTFS 생성·권한·회수: `docs/ntfs-workspaces.md`. Unity 절차: `docs/alpha-workspaces.md`. ReFS·프로세스 소유권: `docs/storage-and-resources.md`.
- 제한 권한 서비스: `docs/storage-privilege-service.md`. `storage service status`는 읽기 전용이다. `package-storage-service.ps1`은 새 패키지 작성, `setup-storage-service.ps1`은 기본 read-only 계획이며 Apply는 명시적 관리자 설치/업데이트/제거다. 설치용 `storage-grant`는 고정 경로 identity만 읽는다. 일반 workspace 명령은 자동 elevation/helper fallback을 하지 않는다. 보호된 ledger와 다른 작업의 snapshot을 보존한다.
- 소스 검증: `docs/source-verification.md`. 일반 status는 새 pending 검증 응답을 조정할 때만 소스를 검사한다. 현재 검사 `--verify-source`, 전체 감사/손상 cache 재작성 `--audit-source`를 구분한다. 초기·감사·provider 재연결은 전체 읽기이며 크기·mtime만 신뢰하지 않는다. Git/Unity mount 허용은 Core `WorkspaceMountAccess`를 공유한다.
- 결과 수집은 `stable-journal-v1`의 전후·unmount 후 seal 일치를 요구한다. release는 독립 결과·소유 프로세스/메모리/핸들 0·실제 mount/Git 등록/delta/마지막 소유 snapshot 회수를 확인한다. unknown을 0으로 읽지 않는다.
- `reclaim-pending`은 결과 보존 뒤 snapshot 회수를 기다리는 상태다. 같은 id/home의 명시적 release 재시도만 허용하며 타 작업·비소유 snapshot 삭제나 자동 sweep으로 우회하지 않는다. 정확한 ID 삭제에도 다른 snapshot이 영향을 받을 수 있으므로 현재 격리 guard를 유지한다.
- Runtime·Windows IPC는 같은 디렉터리에서 flush한 임시 파일을 `File.Replace` 또는 최초 덮어쓰기 없는 move로 게시한다. reader와 시험 계측도 delete sharing을 허용한다. Windows IPC의 게시 공유 오류는 같은 임시 파일로, 관측 reader의 일시적 부재/공유 오류는 같은 경로의 guard/open으로 최대 1초 재시도한다. 선택적 stop/flush/source 요청 polling만 부재를 즉시 반환한다. 목적 파일 삭제·속성 변경으로 우회하지 않으며 JSON 손상·접근 거부·소유권 불일치는 재시도로 숨기지 않는다. 저널 acknowledged-write flush 정책과 구별한다.
- workspace `StateWriteIncomplete`는 현재 details schema의 마지막 시도 stage·HResult와 게시 미시도/unknown/완료를 유지한다. 게시 뒤 event append 실패도 명령 실패다. 확인한 Windows native 오류만 제공하고 이전 실패의 원인을 추측하거나 진단 추가를 retry/flush 정책 변경으로 읽지 않는다. 소유 문서와 검증은 `docs/ntfs-workspaces.md`·`docs/verification/state-write-diagnostics.md`다.
- `scripts/measure-workspace-storage.ps1`은 읽기 전용 계측이다. 논리 payload·파일 할당·관리 메모리 누적 할당·working set·공유 VSS를 구별한다. 공유 VSS는 workspace별로 귀속하거나 중복 합산하지 않는다.
- `docs/snapshot-overlay.md`의 spike와 `scripts/snapshot-overlay-request.ps1`의 공통 요청 생성은 개발용이다. 제품과 같은 현재 VSS 읽기 검증 tuple을 사용한다. `setup-snapshot-overlay.ps1`, `set-snapshot-test-budget.ps1`, `setup-dev-drive.ps1`은 기본 read-only 계획이며 쓰기 모드는 별도 승인된 관리자 작업이다.
- 개발용 `candidate-export`·등록 증거가 필요한 `candidate-source-export`·`validationView`는 `docs/validation-view.md`를 따른다. 후보 현재 형식은 v2이며 이전 후보를 자동 이관하지 않는다. 게시에는 고정 `publication.requestKey`가 필요하며 같은 intent·원본 seal의 부분 저널만 정확한 prefix 검증 후 이어 쓴다. intent 없는 목적지·부분 JSON·다른 요청·손상 파일은 보존 후 거부한다. 관리 파일 분리는 정확한 owner/snapshot/내용만 검증해 새 후보에 적용하고 worker/Git 등록은 보존한다. worker 편집·불변 후보·세대별 검증 소스를 구분하며 전환은 정상 Editor stop·mount 부재 뒤 같은 경로에서 재시작한다. hot switch·생성 source 병합·큐/Coordination·제품 기본 모드 자동 선택은 미구현이다.
- 제품 `validation` CLI와 `Splinter.Workflows.Windows`는 `docs/validation-workflow.md`를 따른다. 공통 coordinator/provider 계약에 실제 VSS·WinFsp·Git·Unity를 연결하며 단일 view/cache와 세대별 source를 분리한다. 명시적 보존 뒤 artifact·cache·worker 순서로 회수하며 후보/소스 검증을 worker의 성공으로 자동 승계하지 않는다. 작은 실제 실행·cold 반복의 자격과 추가 준비/검증 재시작·비용 한계는 WF 검증을 따른다.
- 제품 Library cache는 첫 후보의 불변 snapshot을 고정해 읽고 private 범위만 저장한다. 후보를 바꿔도 기준 snapshot을 바꾸지 않으며 cache close가 끝나기 전 그 snapshot의 회수를 막는다. `validation`의 `compact-cache` JSON action은 정상 stop/unmount 뒤 명시적 겹침량 예산과 같은 요청 복구로 대체된 범위만 정리한다. 작은 실제 Unity의 공간·정리·재증가·회수 자격과 제한은 `docs/verification/product-validation-unelevated.md`의 LB/WF 범위를 따른다.
- `tools/Splinter.ReplayProbe`와 `tools/Splinter.WriteProbe`는 고정 합성 입력의 개발 도구다. 연산별 계측은 `docs/replay-profiling.md`·`docs/write-profiling.md`를 따른다. 실제 Windows mutation gate가 쓰기를 직렬화하므로 Runtime 대기 0은 OS 대기 0이 아니다. group flush는 callback 원자성·응답 경계·내구성 검토 없이는 채택하지 않는다.
- 개발용 `preparation-probe --input`과 `scripts/test-preparation-recovery.ps1`은 `docs/ntfs-workspaces.md`의 준비 중단 절차를 따른다. 스크립트 기본값은 읽기 전용 계획이며 `-Execute`만 등록된 clean fixture의 정확한 CLI PID/시작 시각을 확인해 종료하고 같은 요청의 일반 ensure·결과 보존·release를 실행한다. 실패 작업/evidence를 보존하며 Unity·설치·grant·볼륨 변경이나 unknown의 추측 재시작을 포함하지 않는다. 실제 자격은 `docs/verification/preparation-recovery.md`를 따른다.

## 검증과 후속 작업

- `scripts/test-product-write-recovery.ps1`은 `docs/product-write-recovery.md`의 등록된 로컬 fixture/home·기존 제한 서비스를 사용하는 13개 외부 client 경계 개발 시험이다. 기본 읽기 전용 계획, `-Execute`는 새 evidence/작업 key와 정확한 소유 provider 종료·동일 ensure 재연결·editorless 명시적 보존/release다. 일반 provider에는 observer를 연결하지 않고 실패 작업/delta/snapshot을 보존한다. 설치/grant/볼륨/Unity·VIVOBOOK은 포함하지 않으며 원격 시험은 실행 전에 알린다.

- `scripts/test-kernel-recovery.ps1`은 `docs/kernel-recovery.md`의 로컬 실제 WinFsp/fake 불변 기준 개발 시험이다. 기본 읽기 전용 계획이며 `-Execute`만 새 evidence/mount home에 19개 callback·mmap·새 provider 관문을 실행한다. 자기 자식의 정확한 PID/시작 시각만 종료하고 delta/증거를 보존한다. VSS·제품 CLI·Unity·ARM 자격으로 승계하지 않으며 VIVOBOOK 시험이 필요하면 실행 전에 사용자에게 알린다.

- `scripts/test-journal-recovery.ps1`은 `docs/journal-recovery.md`의 합성 저널/실제 자식 종료 개발 시험이다. 기본값은 읽기 전용 계획, `-Execute`는 새 증거 디렉터리에만 58개 고정 경계의 ACK·tail·재생 hash를 남긴다. 명시적 공통 observer는 일반 provider에 연결하지 않으며 실제 WinFsp/VSS/mmap/전원 자격으로 확대하지 않는다.

- 지원 상태와 보존할 비교 근거는 `docs/verification/README.md`, 미완료 검증은 `docs/plans/validation-matrix.md`를 따른다. 작은 fixture의 성공을 일반 지원으로 확대하지 않는다. 실행하지 않은 시험은 NotRun이며 이전 후보의 결과를 현재 후보의 통과로 표시하지 않는다.
- M0와 장기 NR/SF/SW·48개 카탈로그를 구분한다. 기존 자동 회귀·승인 쓰기·원본/peer/공유 자원 보호는 유지한다. 실제 시험은 변경된 부작용 경계·재현 결함·최종 대표 A/B 한 묶음에 맞춘다. 새 harness·전체 행렬·반복 확대에는 질문과 종료 조건을 먼저 확정하며 지원 경로의 안전 결함을 제외하면 폐기/최소 AI 연결보다 앞세우지 않는다. `plan validate`·DAG schema는 제공하지 않는다.
- 대형 프로젝트의 추가 물리 공간·생성·편집 준비·재개 시간을 함께 판단하고 Editor 동시성은 그 뒤에 둔다. SF-02/03은 Partial, SF-04~06은 NotRun이다. 단일 Editor 제품 연결의 현재 실행 자격은 WF 추적표를 따른다. 기본 모드 선택은 미완료다. 마감된 대형 PV를 자동 재개하지 않는다.
- PV의 S와 Codex 독립 Editor C에는 Unity Agent Coordination을 사용하지 않는다. Q 비교군만 Codex worktree와 Coordination의 단일 Editor를 사용한다. C/S/Q의 Library·큐 대기·후보 전환 비용을 구분한다. S4 입력 실패와 전체 팩 raw 입력 동등성 unknown을 유지한다.
- 남은 NR 관문은 명령 경계별 중단, 일반 파일/에셋, 실제 재부팅·전원·물리 공간 장애, ARM A/B다. VIVOBOOK fixture와 x64 Unity `6000.3.22f1`을 유지하며 ARM64 Editor를 임의로 추가하지 않는다. 원격 준비·증거 교환은 에이전트가 수행한다.
- fixture 미리보기는 `docs/unity-render-qualification.md`를 따른다. 한 요청 최대 2회에서 첫 유효 PNG를 채택하고 요청 경로·해시·pass별 픽셀을 확인한다. Unity 내부 첫 draw 원인은 미확정이다. 제품 validation·대형 프로젝트 지원과 구별한다.
- 실제 Git 수명주기 시험은 `scripts/test-worktree-lifecycle.ps1 -Repository <경로>`다. 명시한 저장소의 등록을 수정하는 개발 시험이며 Unity·CoW 검증이 아니다.
- 형제 `../splinter-unity`는 독립 Git/Unity fixture다. 해당 AGENTS.md를 읽고 기본 설정은 그 저장소의 `development`에 유지한다. 시험용 브랜치·worktree·커밋·충돌은 목적에 맞게 관리하되 원본·다른 작업·기존 결과 refs를 보존한다.
- 계획 요청에는 계획·문서만 작성한다. 구현·설치·외부 프로젝트 변경을 자동으로 포함하지 않는다. 정상 반복 UAC 제거는 `docs/plans/storage-privilege-service.md`를 따르며 UAC를 끄거나 임의 실행 서비스를 설치하지 않는다.

## 문서 관리

- 코드·계약·구조·워크플로 변경 시 해당 현재 문서를 함께 갱신한다. README는 개요, docs/README는 지도다.
- 기능 문서는 현재 구조·동작·진입점·제약만 설명한다. 완료 작업 나열과 변경 이력은 Git에 맡기며 별도 CHANGE LOG를 생성하지 않는다.
- 향후 판단에 필요한 미해결 요구사항·실패 근거·재현 조건·비교 기준은 plans 또는 verification의 소유 문서에 한 번만 보존한다. 원시 artifacts·독립 결과·snapshot·다른 작업은 문서 정리 대상으로 삭제하지 않는다.
- 문서 수정 후 `C:/Users/olive/.codex/skills/project-docs-bootstrap/scripts/check_utf8_docs.py --project-root <경로>`로 UTF-8을 확인한다.
