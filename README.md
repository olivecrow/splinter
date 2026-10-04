# Splinter

AI 에이전트가 Unity처럼 독립 실행이 제약되는 환경에서 병렬 작업공간을 효율적으로 만들고, 이어가며, 원할 때 폐기하도록 돕는 로컬 도구다. 현재 한정 실행 환경은 **Windows/NTFS + Unity의 작은 Git·Plastic 프로젝트 또는 VCS 없는 폴더**다. 원본 이동·최초 전체 복제·읽기 hydration 없이 불변 snapshot과 작업별 범위 변경분을 사용하며, 원본은 작업 중 계속 수정할 수 있다.

제품 요구는 **외부 VCS가 있건 없건 Splinter 자체가 병렬 작업을 제공하고, Git·Unity VCS/Plastic SCM 등을 사용할 때 저장 공간·메모리·병렬 작업에 문제가 생기지 않도록 처리하는 것**이다. VCS별 처리는 Splinter 작업공간의 격리·효율·원본/peer 보호를 위한 호환 책임이다. 현재 CLI는 `--source git|none|plastic`을 선택하고 일반 폴더의 파일 기준·독립 payload 보존·회수/폐기를 제공한다. 작은 Windows x64 NTFS/Unity A/B의 한 묶음을 실행했으며 [현재 계약](docs/vcs-independent-workspaces.md)과 [후보·한정 검증 상태](docs/verification/vcs-independent-workspaces.md)를 구분한다. [일반 폴더의 안전한 원본 반영](docs/plain-folder-original-apply.md)을 명시적 계획/intent와 Windows 파일 게시로 연결했으며, [현재 검증](docs/verification/plain-folder-original-apply.md)의 작은 Editor 없는 실제 제품 A/B를 scoped-complete-with-limitations로 마감했다. Plastic 사용 환경은 [관리 정보/등록 격리와 독립 파일 결과](docs/plastic-compatibility.md)를 연결하고 [별도 T: A/B 검증](docs/verification/plastic-compatibility.md#별도-t-입력의-회수-자격)의 마지막 snapshot 회수까지 한정 마감했다. 선행 C: 회수 보류와 외부 도구 발견 제한은 유지한다.

각 대화가 필요할 때만 자신의 작업공간을 사용한다. 같은 작업은 여러 대화와 CLI 실행에 걸쳐 유지하며, 대화나 Editor 종료는 반영·회수·폐기의 승인이 아니다.

[Unity CLI의 정확한 A/B 인계](docs/verification/unity-cli-handoff.md)는 작은 T: Plastic 입력에서 편집·loaded 결과·A 정상 종료 뒤 B 후속 작업·두 독립 결과/회수까지 한정 확인했다. 설치된 CLI의 worker 목록 누락과 첫 eval 타임아웃은 유지하며, [반환 경로와 실제 대상/PID를 확인하는 절차](docs/alpha-workspaces.md#unity-cli의-정확한-대상-선택)를 사용한다. MCP·다른 입력/버전·Play/Test/build의 자격으로 확대하지 않는다.

등록된 작은 Windows x64 NTFS/Git/Unity 환경의 **첫 한정 실사용 버전 M0를 마감했다**. 제공물은 기존 개발 CLI·소스 스킬과 [AI 시작 안내](docs/agent-start.md)이며, [납품 판정](docs/verification/m0-delivery.md)은 scoped-complete-with-limitations다. [대표 A/B](docs/verification/representative-agent-workflow.md)의 오류/수정·같은 작업 재개·보존/통합/회수·실패 작업 폐기와 비용, 현재 복구 계약의 회귀를 확인했다. 폐기에는 해당 계약을 지원하는 제한 서비스가 필요하다. 후속 책임은 [축소한 구현 계획](docs/plans/implementation-plan.md#프로그램과-에이전트의-책임)에서 프로그램 구현·AI 사용 절차·필요 시 지원 검증으로 구분한다.

Splinter는 작업공간과 소유 Editor의 수명·범위 저장·독립 결과·안전한 반영/회수/폐기를 담당한다. 설치 연결·대화 인계·도구/모드 선택·충돌 해결·게임 검증은 AI와 기존 Git·MCP·Unity CLI가 수행한다. 전용 installer·Codex 자동 binding·큐/자동 모드·일반 임대 인수·원본 Editor 공유·범용 Unity 작업 API·AI Runner/DAG·상주 DB는 구현 범위에서 제외한다. 현재 외부 도구 인계는 [도구 선택·연결과 peer 보호의 한정 범위](docs/plans/session-workflow-and-installation.md#외부-도구-인계의-한정-범위)를 먼저 확인해야 한다.

## 현재 제공하는 기능

- .NET CLI, atomic JSON 상태와 작업 잠금, 공통 Core 계약 및 fake 적합성 시험.
- 저장소에 포함한 [AI 사용 스킬](skills/splinter/SKILL.md): 필요성 판단·반환 경로 사용·같은 요청 재개·명시적 종료·실패 처리. 현재 CLI와 대화의 작업 식별자를 사용한다.
- [명시적 폐기](docs/workspace-discard.md)의 고정 intent·중단 재개와 Git/Windows provider·`workspace discard`. 실제 서비스 적용/폐기 자격은 별도로 표시한다.
- `ntfs-snapshot` workspace의 VSS·WinFsp 범위 저널, 선택한 Git/파일 기준, 작업별 프로세스 그룹과 Editor start/stop, 독립 결과 보존과 명시적 회수.
- [Git 프로젝트 기준본](docs/git-project-basis.md)의 `git-snapshot`: 기록된 commit/tree/blob을 VSS·전체 checkout·읽기 disk cache 없이 기존 범위 저널에 연결한다. ignored 소스는 제외하며 Library는 [명시적 원본 참조](docs/git-library-reference.md)로 연결할 수 있다. 실제 자격은 [별도 검증](docs/verification/git-project-basis.md)을 따른다.
- [Plastic 프로젝트 기준본](docs/plastic-project-basis.md)의 `plastic-snapshot`: 고정 changeset/revision을 VSS·checkout·payload disk cache 없이 읽고 공통 Library 참조를 연결한다. [전체/선택 원본 반영](docs/plastic-original-apply.md)은 고정 계획/intent와 조건부 NTFS 게시를 사용하며 [실제 자격](docs/verification/plastic-original-apply.md)을 별도로 확인한다.
- [현재 압축 저널](docs/journal-payload-compression.md): bounded raw/Brotli 범위·overlay v2·candidate v3. 실제 자격은 [별도 검증](docs/verification/journal-payload-compression.md)을 따른다.
- 변경 기반 소스 검증, 명시적 저널 정리와 선택적 중첩량 예산, snapshot 지연 회수.
- VSS 전용 Windows 권한 서비스와 명시적 설치 도구. 실제 설치·반복 UAC 0의 자격은 [UA 검증](docs/verification/storage-privilege-service.md)을 따른다.
- `checkpoint`는 현재 소스를 독립 결과로 보존한다. 컴파일·Play·테스트·빌드·캡처와 합격 판단은 AI가 MCP·Unity CLI 등으로 수행하며 Splinter는 그 성공을 판정하지 않는다.
- 선택적 ReFS/baseline·diagnostic checkout·후보/공통 Library view·spike/probe는 [개발용 CLI](docs/development-cli.md)의 별도 경로다. 기본 CLI의 명령 발견·작업 생성에서 제외한다. view는 저장소 수명과 회수 의존성만 관리한다.

현재 지원은 작은 fixture의 내부 알파 범위다. 기존 M0·WF/LB 실행 기록은 당시 후보의 근거이며, 이번 컴파일 기능 제거·개발 CLI 분리 후보의 native 실행 통과로 승계하지 않는다. 등록한 프로젝트/home에서 제한 서비스와 일반 권한 Unity A/B/A·Library 재사용/정리·재연결·독립 결과/회수 및 추가 UAC 관측 0을 확인했다. 일반 파일/에셋 I/O·OS 장애·ARM·대형 용량의 미자격은 유지하고 실제 사용할 입력/환경에서 필요한 경계만 선택한다. 전용 제품 설치·대화 자동 연결은 현재 제공하지 않으며 구현 제외 항목을 미완성 개발 목록으로 세지 않는다. 실제 discard 자격은 [폐기 검증](docs/verification/workspace-discard.md), 현재 계약은 [기반 구조](docs/foundation.md), 지원 경계는 [검증 안내](docs/verification/README.md)를 따른다.

## 실행

.NET SDK `10.0.112` 호환 패치, Git 경로에서만 Git, PowerShell 7.2 이상이 필요하다. 저장소 루트에서 실행한다.

```powershell
pwsh ./scripts/build.ps1
pwsh ./scripts/splinter.ps1 capabilities
pwsh ./scripts/splinter.ps1 schema workspace.ensure
pwsh ./scripts/splinter.ps1 doctor --project ../splinter-unity
pwsh ./scripts/splinter.ps1 workspace ensure --project ../splinter-unity --key <task-key> --storage ntfs-snapshot --base development --home <NTFS-home>
```

CLI는 stdout에 JSON 응답 하나를 반환한다. VSS 작업은 [제한 권한 서비스](docs/storage-privilege-service.md)를 사용하며, 서비스 부재나 미등록 범위는 설치/구성 필요 상태를 반환한다. [NTFS 사용법](docs/ntfs-workspaces.md)을 먼저 읽고 반환된 작업공간 경로에서 편집한다. 일반 CLI·Git·Unity 전체를 관리자 권한으로 실행하지 않는다.

각 대화의 에이전트는 [시작 안내](docs/agent-start.md)와 [스킬 원본](skills/splinter/SKILL.md)을 읽고 시작한다. 프로젝트 정책에 지정한 CLI·등록 project/home을 사용하고, 응답의 `data.workspace.path`에서 편집한다. 같은 작업은 key/id/home·기준·request digest를 대화나 인계에 남겨 같은 ensure 입력으로 재개한다. 현재 경로에서 독립 실행 없이 작업할 수 있으면 새 workspace를 만들지 않는다.

`main`은 출시용, `development`는 개발용이다. 형제 `../splinter-unity`는 별도 Git 저장소이며 submodule이 아니다. 실제 Git 등록 시험은 `pwsh ./scripts/test-worktree-lifecycle.ps1 -Repository ../splinter-unity`로 실행한다. 이 시험은 Unity Editor·CoW 시험과 구별한다.

## 문서

- [문서 지도](docs/README.md): 현재 기능별 계약과 사용법.
- [제품 요구사항](plan.html): 제품이 달성해야 할 목표.
- [구현 계획](docs/plans/implementation-plan.md): 프로그램/에이전트 책임과 축소한 후속 작업·종료 기준.
- [저장공간 우선 설계](docs/plans/storage-priority-and-editor-modes.md): 용량·준비·재개 비용과 Editor 실행 방식의 판단 기준.
- [VCS 프로젝트 단위 기준본 전환 계획](docs/plans/project-scoped-basis-and-reclamation-review.md): Git·Plastic 버전 재사용과 범위 저널 연결의 확정 범위. VCS 없는 새 방식은 후순위이며 Git 경로는 별도 `git-snapshot`으로 연결했다.
- [물리 저장 단계 마감](docs/verification/physical-storage-completion.md): 현재 NTFS 알파의 최종 A/B·정리·독립 결과/회수·잔여 비용을 한정 확인했다.
- [메모리 단계 마감](docs/verification/memory-completion.md): 압축 쓰기/부분 읽기 buffer·같은 실제 저널·provider/Editor 비용과 최종 A/B 회수를 한정 확인했다.
- [병렬 안정성의 선택 단위 마감](docs/verification/stable-parallel-completion.md): 잠금 경로 오류와 실제 A checkpoint/release 중 B 조회·재개·64개 쓰기/독립 결과/회수를 확인했다. Editor 없는 경계이며 기존 Unity 자격과 구분한다.
- [검증 추적표](docs/plans/validation-matrix.md): 첫 버전 M0와 장기 지원·확장 카탈로그.

배포 전에는 현재 데이터 계약만 지원한다. 지원하지 않는 형식은 원본과 결과를 보존한 채 명시적으로 거부하며 자동 이관하지 않는다. 변경 이력은 Git에서 확인한다.
