# Splinter 작업 지침

- 먼저 `README.md`와 `docs/README.md`를 읽고 해당 작업의 문서만 이어서 읽는다.
- `main`은 출시용, `development`는 개발용 브랜치다. 일반 개발과 통합은 `development`를 기준으로 수행한다.
- 주 사용자는 AI 에이전트다. 기계용 계약, 적은 호출 수, 명시적 상태·복구 행동, 재시작 후 지속성을 기준으로 설계한다.
- 일반 `workspace status`는 새 pending 검증 응답을 조정할 때만 소스를 읽는다. 현재 소스 확인은 `--verify-source`로 요청하고 `sourceVerification`·`validationCurrent`와 validation 성공을 함께 판단한다. `stable-journal-v1` 결과만 회수에 사용하며 수집 전후·unmount 후 seal 불일치는 보존한다. Git/Unity의 mount root 허용은 Core `WorkspaceMountAccess`를 공유한다. 현재 수정 증거는 `docs/verification/workspace-review-fixes.md`다.
- 다음 실행 단위는 `docs/plans/ntfs-recovery-and-support.md`의 제한된 실제 AI A/B·비용 관문 PV-01/02다. NR 장애·파일 경계는 지원 확대의 필수 관문으로 유지하되, 그 전체 완료를 제한된 fixture의 작업 효용 확인보다 앞세우지 않는다. 선택적 ReFS 신규 확장·daemon/DB 전환은 이 검증의 선행 구현으로 추가하지 않는다.
- 저장소의 필수 요구사항은 NTFS 원본 위치·파일시스템 유지, 최초 전체 데이터 복제와 읽기 hydration 금지, 큰 파일의 범위별 변경 저장이다. 원본은 작업 중 계속 수정 가능하며 각 작업은 시작 시점의 불변 스냅샷을 참조한다. 작업 기간 전체의 원본 쓰기 차단을 대체안으로 삼지 않는다.
- NTFS 실험의 SO-01~06 fixture 증거는 `docs/verification/snapshot-overlay.md`에 있다. `docs/snapshot-overlay.md`의 `snapshot-spike`와 준비·측정·장애·보존·회수 스크립트는 독립 실험이며 제품 workspace backend가 아니다. 제품 NTFS backend의 현재 계약은 `docs/ntfs-workspaces.md`, 실제 NI 진행은 `docs/verification/ntfs-workspace-integration.md`다. 기존 ReFS 경로는 선택적 백엔드의 증거로 유지하고 필수 준비·이관·전체 복사 fallback으로 사용하지 않는다.
- `plan.html`은 제품 요구사항의 원본이다. `docs/plans/`는 목표 구조와 남은 작업이며, 실제 구현은 `docs/foundation.md`를 확인한다.
- NI-01~08의 1차 NTFS workspace 통합은 완료했다. 현재 작업 범위는 `docs/plans/ntfs-recovery-and-support.md`와 검증 추적표의 NR-01~08이다. NR-01/03/05/07의 복구 응답·unknown 관측·상태/저널 손상 보존·파일 속성 거부·현재 부팅의 VSS 해제 관측을 부분 구현했고 실행 범위는 `docs/verification/ntfs-recovery.md`를 따른다. VIVOBOOK의 빈 `splinter-test-vivobook`에 fixture 복사를 완료했다. 기존 x64 Unity 6000.3.22f1을 유지하며 ARM64 Editor를 추가하지 않는다. ARM Windows의 제품 Unity 단일 workspace는 스크립트 검증 2건·결과 보존·회수를 통과했다. 재부팅·물리 공간 고갈·강제 전원 종료와 ARM Windows의 A/B 동시 회귀는 아직 완료하지 않았다. 원격 canonical x64 Editor의 첫 임포트·기존 MCP 연결·Console 준비는 완료했고 fake 입력의 실제 mount에서 파일 API 5개 거부를 확인했다. 이번 원격 workspace와 canonical Editor/MCP 세션은 모두 정리했고 fixture·결과·설정은 보존했다. live VSS identity 하위 단위는 완료했으며 다음은 나머지 중단·파일 경계와 재부팅·장애 관문이다. 원격 준비와 결과 교환은 두 작업 사이에서 수행하며, 사용자에게 메시지 전달이나 수동 fixture 복사를 다시 요구하지 않는다.
- 작업 요청이 계획 수립이면 계획과 문서를 작성한다. 구현·설치·Git 초기화·외부 프로젝트 변경을 계획 수립에 포함시키지 않는다.
- `docs/plans/ntfs-live-vss-identity.md`의 LV-01~08은 정의한 자동/실환경 수준에서 완료했다. 현재 계약은 `docs/ntfs-workspaces.md`, 새 후보의 180개 자동 통과·실제 VSS/Git 재연결·Windows x64 Unity 검증 3건·독립 결과·소유 자원 회수는 `docs/verification/ntfs-live-vss-identity.md`를 따른다. 사용자 재시작 뒤 canonical Editor/MCP를 보존했다. 이를 NR 전체 완료로 표시하지 않고 나머지 중단·파일 경계, 실제 재부팅·전원·공간 장애와 ARM A/B는 상위 NR 계획에 남긴다.
- 공통 계약과 fake 적합성 시험부터 연결한다. Core에 Unity·Win32·Git·Codex 전용 API를 넣지 않는다. 현재 Host는 읽기 전용 in-process 조합 계층이며 daemon이나 DB writer가 아니다.
- 검증 진입점은 `pwsh ./scripts/build.ps1`이다. 의존성 변경 시 중앙 버전과 해당 `packages.lock.json`을 함께 갱신하고 locked restore로 확인한다.
- 실제 Git 워크트리 시험은 `scripts/test-worktree-lifecycle.ps1 -Repository <경로>`를 사용한다. 명시 저장소의 Git 등록을 수정하는 개발용 시험이며 제품의 workspace API로 취급하지 않는다.
- 형제 `../splinter-unity`는 독립 Git/Unity fixture다. 해당 저장소의 AGENTS.md도 읽고, Unity 실행 없이 수행한 Git 시험을 Editor·CoW 검증으로 보고하지 않는다.
- Unity fixture의 브랜치·워크트리·시험용 커밋과 필요한 변경·충돌 상태는 에이전트가 테스트 목적에 맞게 자율적으로 구성하고 관리한다. 재사용할 기본 설정은 그 저장소의 `development`에 유지한다.
- worktree 생성·회수와 원본 적용은 지정 어댑터를 통해 수행하는 제품 계약으로 설계한다. CoW를 보안 샌드박스라고 표현하지 않는다.
- 접수·완료, 작업자 종료·검증 성공, fixture 시험·실환경 지원을 구분한다. 실제 실행하지 않은 시험을 통과로 기록하지 않는다.
- 원본과 다른 작업자의 변경을 보존한다. 임대 만료·타임아웃·디스크 압박만으로 유일한 결과를 삭제하는 동작을 만들지 않는다.
- 계약을 바꾸면 입력·출력 schema, fake, 적합성 시험, 관련 문서·검증 추적표를 함께 갱신한다.
- 새 모듈이나 실행 가능한 명령이 생기면 이 파일·문서 지도·capabilities의 현재 상태를 함께 갱신한다.
- NTFS 실험 코드는 `docs/snapshot-overlay.md`, 실제 진행 상태는 `docs/verification/snapshot-overlay.md`를 따른다. `scripts/setup-snapshot-overlay.ps1`와 `scripts/set-snapshot-test-budget.ps1`은 기본 계획이며 쓰기 모드는 별도 승인된 관리자 작업이다. `scripts/vss-snapshot.ps1`와 `overlay-serve`는 제품 workspace API와 구별한다. `measurement-v1-invalid`의 물리 할당 수치는 사용하지 않는다.

<!-- project-docs-bootstrap:start -->
# Project Documentation Maintenance

- After changing code, behavior, architecture, or workflows, update the relevant project documents before reporting completion.
- Use `README.md` as the project overview and documentation entrypoint.
- Use `docs/README.md` as the documentation map for feature and subsystem documents.
- Keep feature documents under `docs/` as current-state snapshots only: structure, behavior, entrypoints, data flow, and constraints that are true now.
- Do not store historical notes, old intent, or CHANGE LOG rows inside feature documents.
- Store feature CHANGE LOG rows separately under `docs/_change_logs/`, with one JSON object per line.
- After each feature change, append a CHANGE LOG row with the project-docs-bootstrap `manage_feature_changelog.py` script, then prune old rows when the configured limit is exceeded.
- Before final reporting after documentation edits, validate UTF-8 encoding with the project-docs-bootstrap `check_utf8_docs.py` script.
<!-- project-docs-bootstrap:end -->

현재 제품 workspace 내부 알파는 Runtime/Git/Unity에 `ntfs-snapshot` backend와 선택적 ReFS 블록 복제, 작업별 프로세스 감시를 연결한다. 에이전트는 `scripts/splinter.ps1`을 사용한다. NTFS 생성·권한 경계·독립 결과·회수는 `docs/ntfs-workspaces.md`, 공통 Unity 절차는 `docs/alpha-workspaces.md`, ReFS 기준본과 프로세스 자원은 `docs/storage-and-resources.md`를 따른다. NI-01~08의 1차 내부 알파 fixture/자동 증거와 실제 dirty 원본 통합 차단을 확인했고 모든 소유 시험 프로세스·mount·delta·snapshot을 회수했다. fixture의 검증 범위와 일반 지원의 경계는 NI 기록을 따른다. SO 또는 ReFS 성공을 NI 통과로 집계하지 않는다. 긴 Unity 패키지 경로의 초기 로딩 오류가 관측됐으므로 짧은 home을 사용하고 범용 패키지/에셋 지원으로 확대하지 않는다. 최초 전체 복제·읽기 hydration·파일 전체 copy-up fallback을 만들지 않는다. release는 독립 결과, 소유 프로세스·메모리·핸들 0, 실제 mount·Git 등록·delta·마지막 소유 snapshot 회수를 확인해야 한다. unknown 관측을 0으로 취급하지 않는다. `scripts/setup-dev-drive.ps1`은 기본 read-only 계획이며 `-Create`는 승인된 새 가상디스크의 관리자 설정용이다. 실제 검증은 `docs/verification/`와 AA/SR/SO/NI 추적표를 따르고 공개 시나리오 전체 통과로 확대하지 않는다.
