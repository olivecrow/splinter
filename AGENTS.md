# Splinter 작업 지침

- 먼저 `README.md`와 `docs/README.md`를 읽고 해당 작업의 문서만 이어서 읽는다. `plan.html`은 제품 요구사항, `docs/plans/`는 목표와 남은 작업, `docs/foundation.md`는 현재 구현이다.
- `main`은 출시용, `development`는 개발용이다. 일반 개발과 통합은 `development`를 기준으로 한다. 원본과 다른 작업자의 미커밋 변경을 보존한다.
- 주 사용자는 각 대화의 AI 에이전트다. `docs/plans/session-workflow-and-installation.md`의 필요 시 사용 → 같은 작업 지속 → 명시적 반영/보존/폐기를 기본 사용 모델로 삼는다. 별도 조정 AI·Runner·DAG는 필수가 아니다.
- 사용자가 병렬 작업을 명시하거나 다른 작업의 점유로 독립 실행이 필요할 때 Splinter workspace를 사용한다. 불필요하면 직접 작업하며, Splinter 병렬 경로에 Codex 별도 worktree를 중첩하지 않는다. 제품 내부 Git linked worktree 계약은 유지한다.
- 대화·응답·Editor 종료, 임대 만료·타임아웃·디스크 압박은 통합이나 삭제의 승인이 아니다. release는 독립 결과를 보존한다. 명시적 discard·설치·자동 binding·권한 서비스는 미구현이다.
- 필수 저장소 요구는 원본 NTFS 위치·파일시스템 유지, 최초 전체 데이터 복제·읽기 hydration·파일 전체 copy-up fallback 금지, 범위 변경 저장이다. 원본은 작업 중 계속 수정 가능해야 한다. ReFS는 선택적 backend이며 NTFS의 이관 fallback이 아니다.
- Core에 Unity·Win32·Git·Codex 전용 API를 넣지 않는다. 공통 계약과 fake 적합성 시험부터 연결한다. Host는 읽기 전용 in-process 조합 계층이며 daemon/DB writer가 아니다.
- 에이전트 진입점은 `scripts/splinter.ps1`, 필수 검증은 `pwsh ./scripts/build.ps1`이다. 의존성 변경 시 중앙 버전·해당 lockfile을 함께 갱신하고 locked restore로 확인한다.
- 현재 버전 계약 하나만 지원한다. 구버전 자동 이관·호환 fallback을 추가하지 않는다. 지원하지 않거나 손상된 상태는 보존 후 명시적으로 거부한다. 같은 버전에서의 중단·응답 유실·재시작 복구는 유지한다.
- 계약 변경 시 입력·출력 schema, fake, 적합성 시험, 해당 기능 문서·검증 추적표를 함께 갱신한다. 새 모듈이나 실행 명령은 이 파일·문서 지도·capabilities에 현재 상태를 반영한다.

## 현재 기능의 소유 문서

- NTFS 생성·권한·회수: `docs/ntfs-workspaces.md`. Unity 절차: `docs/alpha-workspaces.md`. ReFS·프로세스 소유권: `docs/storage-and-resources.md`.
- 소스 검증: `docs/source-verification.md`. 일반 status는 새 pending 검증 응답을 조정할 때만 소스를 검사한다. 현재 검사 `--verify-source`, 전체 감사/손상 cache 재작성 `--audit-source`를 구분한다. 초기·감사·provider 재연결은 전체 읽기이며 크기·mtime만 신뢰하지 않는다. Git/Unity mount 허용은 Core `WorkspaceMountAccess`를 공유한다.
- 결과 수집은 `stable-journal-v1`의 전후·unmount 후 seal 일치를 요구한다. release는 독립 결과·소유 프로세스/메모리/핸들 0·실제 mount/Git 등록/delta/마지막 소유 snapshot 회수를 확인한다. unknown을 0으로 읽지 않는다.
- `reclaim-pending`은 결과 보존 뒤 snapshot 회수를 기다리는 상태다. 같은 id/home의 명시적 release 재시도만 허용하며 타 작업·비소유 snapshot 삭제나 자동 sweep으로 우회하지 않는다. 정확한 ID 삭제에도 다른 snapshot이 영향을 받을 수 있으므로 현재 격리 guard를 유지한다.
- Runtime·Windows IPC는 같은 디렉터리에서 flush한 임시 파일을 `File.Replace` 또는 최초 덮어쓰기 없는 move로 게시한다. reader는 delete sharing을 허용한다. 공유 위반을 먼저 삭제하거나 속성을 바꾸어 우회하지 않는다. 저널 acknowledged-write flush 정책과 구별한다.
- `scripts/measure-workspace-storage.ps1`은 읽기 전용 계측이다. 논리 payload·파일 할당·관리 메모리 누적 할당·working set·공유 VSS를 구별한다. 공유 VSS는 workspace별로 귀속하거나 중복 합산하지 않는다.
- `docs/snapshot-overlay.md`의 spike와 `scripts/snapshot-overlay-request.ps1`의 공통 요청 생성은 개발용이다. 제품과 같은 현재 VSS 읽기 검증 tuple을 사용한다. `setup-snapshot-overlay.ps1`, `set-snapshot-test-budget.ps1`, `setup-dev-drive.ps1`은 기본 read-only 계획이며 쓰기 모드는 별도 승인된 관리자 작업이다.
- 개발용 `candidate-export`·등록 증거가 필요한 `candidate-source-export`·`validationView`는 `docs/validation-view.md`를 따른다. 후보 현재 형식은 v2이며 이전 후보를 자동 이관하지 않는다. 게시에는 고정 `publication.requestKey`가 필요하며 같은 intent·원본 seal의 부분 저널만 정확한 prefix 검증 후 이어 쓴다. intent 없는 목적지·부분 JSON·다른 요청·손상 파일은 보존 후 거부한다. 관리 파일 분리는 정확한 owner/snapshot/내용만 검증해 새 후보에 적용하고 worker/Git 등록은 보존한다. worker 편집·불변 후보·세대별 검증 소스를 구분하며 전환은 정상 Editor stop·mount 부재 뒤 같은 경로에서 재시작한다. hot switch·생성 source 병합·큐/Coordination·제품 기본 모드 연결은 미구현이다.
- 제품 연결 전 `ValidationWorkflowCoordinator`/`ValidationWorkflowStore`와 `IValidationWorkflowProvider`는 `docs/validation-workflow.md`의 공통 계약·fake 범위다. 실제 provider·CLI는 연결하지 않았으며 후보/소스 세대의 결과를 worker validation이나 snapshot 독립 결과로 자동 승계하지 않는다.
- `tools/Splinter.ReplayProbe`와 `tools/Splinter.WriteProbe`는 고정 합성 입력의 개발 도구다. 연산별 계측은 `docs/replay-profiling.md`·`docs/write-profiling.md`를 따른다. 실제 Windows mutation gate가 쓰기를 직렬화하므로 Runtime 대기 0은 OS 대기 0이 아니다. group flush는 callback 원자성·응답 경계·내구성 검토 없이는 채택하지 않는다.

## 검증과 후속 작업

- 지원 상태와 보존할 비교 근거는 `docs/verification/README.md`, 미완료 검증은 `docs/plans/validation-matrix.md`를 따른다. 작은 fixture의 성공을 일반 지원으로 확대하지 않는다. 실행하지 않은 시험은 NotRun이며 이전 후보의 결과를 현재 후보의 통과로 표시하지 않는다.
- 대형 프로젝트의 추가 물리 공간·생성·편집 준비·재개 시간을 함께 판단하고 Editor 동시성은 그 뒤에 둔다. SF-02/03은 Partial, SF-04~06은 NotRun이다. 단일 Editor 제품 연결과 기본 모드 선택은 미완료다. 마감된 대형 PV를 자동 재개하지 않는다.
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
