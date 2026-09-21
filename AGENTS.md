# Splinter 작업 지침

- 먼저 `README.md`와 `docs/README.md`를 읽고 해당 작업의 문서만 이어서 읽는다.
- `main`은 출시용, `development`는 개발용 브랜치다. 일반 개발과 통합은 `development`를 기준으로 수행한다.
- 주 사용자는 AI 에이전트다. 기계용 계약, 적은 호출 수, 명시적 상태·복구 행동, 재시작 후 지속성을 기준으로 설계한다.
- 저장소의 필수 요구사항은 NTFS 원본 위치·파일시스템 유지, 최초 전체 데이터 복제와 읽기 hydration 금지, 큰 파일의 범위별 변경 저장이다. 원본은 작업 중 계속 수정 가능하며 각 작업은 시작 시점의 불변 스냅샷을 참조한다. 작업 기간 전체의 원본 쓰기 차단을 대체안으로 삼지 않는다.
- NTFS 실험의 SO-01~06 fixture 증거는 `docs/verification/snapshot-overlay.md`에 있다. `docs/snapshot-overlay.md`의 `snapshot-spike`와 준비·측정·장애·보존·회수 스크립트는 독립 실험이며 제품 workspace backend가 아니다. 다음 단계는 `docs/plans/ntfs-snapshot-overlay-spike.md`의 통합 조건이다. 기존 ReFS 경로는 선택적 백엔드의 증거로 유지하고 필수 준비·이관·전체 복사 fallback으로 사용하지 않는다.
- `plan.html`은 제품 요구사항의 원본이다. `docs/plans/`는 목표 구조와 남은 작업이며, 실제 구현은 `docs/foundation.md`를 확인한다.
- 작업 요청이 계획 수립이면 계획과 문서를 작성한다. 구현·설치·Git 초기화·외부 프로젝트 변경을 계획 수립에 포함시키지 않는다.
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

현재 제품 workspace 내부 알파는 Runtime/Git/Unity에 `Splinter.Platform.Windows`의 ReFS 블록 복제와 작업별 프로세스 감시를 연결한다. 에이전트는 `scripts/splinter.ps1`을 사용한다. 재개·통합은 `docs/alpha-workspaces.md`, 기준본/CoW/메모리 회수는 `docs/storage-and-resources.md`를 따른다. 최초 전체 복제 금지는 별도 NTFS 실험에서 검증했고 제품 workspace에는 미통합이다. 기존 ReFS 경로의 정상 동작을 SO 관문 통과로 집계하지 않는다. 큰 파일의 반복 복사를 기본값으로 만들거나 실패를 전체 복사로 대체하지 않는다. release는 소유 프로세스와 메모리 0, 실제 경로·Git 등록 회수를 확인해야 한다. `scripts/setup-dev-drive.ps1`은 기본 read-only 계획이며 `-Create`는 승인된 새 가상디스크의 관리자 설정용이다. 실제 검증은 `docs/verification/`와 추적표의 AA/SR/SO 항목을 따르고 공개 시나리오 전체 통과로 확대하지 않는다.
