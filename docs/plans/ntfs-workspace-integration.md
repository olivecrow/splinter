# NTFS workspace 1차 통합 작업 범위

확정일: 2026-09-21. 상태: **NI-01~08의 1차 내부 알파 구현·fixture 검증·회수 완료**. 이 문서는 승인된 작업 범위와 완료 기준이다. 현재 구현 계약은 [NTFS 작업공간](../ntfs-workspaces.md), 실행 결과와 남은 관문은 [NI 검증 기록](../verification/ntfs-workspace-integration.md)을 따른다. 아래 출발점 표는 범위 확정 당시 조회 기록이다.

목표는 **독립 NTFS 실험을 일반 workspace 명령에 연결하여 생성 → 편집·컴파일 → 같은 작업 재개 → 독립 결과 보존 → 안전한 회수를 완주하는 내부 알파**다. [기획서 15.1절](../../plan.html#section-15-1)의 우선순위 1·2를 중심으로, 이 흐름에 필요한 제한된 복구와 소스 검증을 포함한다. 공개 제품 전체 지원과 성능 목표는 [전체 구현 계획](implementation-plan.md)에 남긴다.

## 확인한 출발점

| 항목 | 확인 결과와 근거 |
| --- | --- |
| 제품 저장소 | `development`, `17cee45190c9ac6800fd1737321ea4286a8696df`. 검토 시작 시 미커밋 변경 없음 |
| Unity fixture | 별도 저장소 `../splinter-unity`, `development`, `46e7f4dbdb219e4afa602f3598a3de9ff4070fae`. 검토 시작 시 미커밋 변경 없음. Unity `6000.3.22f1`, URP `17.3.0` |
| 기존 제품 알파 | atomic JSON·작업 잠금, Git worktree, Editor 실행·컴파일·재개, checkpoint/integrate/release 구현. [AA](../verification/internal-alpha.md)와 [SR](../verification/storage-and-resources.md)에서 실제 범위를 구분 |
| NTFS 실험 | VSS 불변 기준, WinFsp mount, 범위 저널, 독립 Git index, 원본/A/B 실행, 장애·보존·회수의 [SO-01~06 fixture 증거](../verification/snapshot-overlay.md) 확보 |
| 착수 당시 제품과의 간격 | `workspace-ensure-request.schema.json`과 `WorkspaceCoordinator`는 `refs-cow`/`git-checkout`만 허용. CLI가 ReFS 공급자를 조립하며 snapshot/delta/mount ports는 제품 coordinator에 미연결 |
| 추가 통합 경계 | VSS capture/observe/release는 관리자 스크립트 경로다. `OverlayExperimentCommands`의 Unity fingerprint는 `SplinterSnapshotProbe.cs` 한 파일이며 일반 제품 validation을 대신하지 못함 |
| 기존 자동 시험 | NTFS 실험 worktree의 2026-09-21 TRX는 69개 중 68 passed, 0 failed, 선택적 ReFS 1개 not executed. 이번에 다시 실행하지 않음 |

원시 SO 증거와 최신 TRX는 현재 로컬의 `C:/Users/olive/.codex/worktrees/3918/splinter/artifacts/`에 있다. 일반 checkout의 `artifacts/test-results/`에는 이전 ReFS 실행의 56개 결과가 있으므로 혼합하지 않는다. 검토에서 실제 A/B probe의 104/204, 범위 쓰기 계측, Unity 회수 영수증의 process/memory/handle/delta 0과 mount 부재, 소유 VSS 삭제·기존 지점 보존을 대조했다. 이는 당시 실행 기록의 확인이며 현재 OS 상태를 새로 시험한 결과가 아니다. 후속 구현은 자기 run 디렉터리와 검증 보고서를 남긴다.

기존 SO 데이터·mount·delta·snapshot과 ReFS 시험 VHDX는 회수된 상태로 기록되어 있다. 이전 요청 파일을 살아 있는 작업공간으로 재사용하거나 ReFS 기준본 재생성을 전제로 시작하지 않는다.

## 이번에 구현할 범위

### 1. 공통 계약과 지속 상태

- 기존 `ISourceSnapshotProvider`, `IWorkspaceDeltaStore`, `IOverlayMountProvider`와 workspace 수명주기의 책임을 먼저 정리하고 fake 적합성 시험을 연결한다. Core에 VSS·WinFsp·Git·Unity API를 넣지 않는다.
- workspace ID, 정규화한 요청과 입력 hash, snapshot identity, delta identity/저널 세대, Git 등록, 앱/제공자 process lease, 결과 manifest를 지속 상태에서 연결한다. 부작용 전 intent와 실행 후 관측을 기록한다.
- 같은 project/key와 같은 입력의 재호출은 같은 작업으로 돌아온다. 다른 입력은 충돌이며 회수된 key의 재호출은 새 작업을 만들지 않는다. CLI 종료·응답 유실만으로 새 캡처·Git 등록·Editor를 중복 생성하지 않는다.
- 현재 atomic JSON과 잠금을 보강한다. 이 작업을 위해 Host를 daemon/SQLite writer로 바꾸지 않는다. 기존 ReFS 기록은 읽을 수 있게 유지하고 NTFS 필수 identity가 빠진 기록은 보존 상태로 명시 실패한다.

### 2. 일반 CLI에서 NTFS 생성·조회·재개

- `workspace ensure`에 storage 값 `ntfs-snapshot`을 추가한다. 현재 코드·입력/출력 schema·명령 descriptor·capabilities에 연결했으며 실제 통합 qualification은 NI 기록을 따른다. baseline 준비 없이 NTFS 원본에서 생성하며, 기존 명시적 ReFS/진단 모드는 별도 유지한다.
- 최초 지원 입력은 로컬 NTFS의 repository-root Unity fixture, clean checkpoint, Git LFS·추가 사용자 filter가 없는 검증 조합이다. 시작 시 Git revision과 snapshot 안의 소스가 같은 checkpoint임을 확인하고 준비 중 불일치는 Ready 전에 차단한다. 원본 stash/reset/임의 commit으로 전제를 만들지 않는다.
- 새 작업은 그 작업을 위해 확인한 캡처 시점의 기준을 사용한다. 기존 key 재개는 같은 snapshot/delta를 사용한다. 기준 공유는 동일 캡처를 명시적으로 참조하는 경우로 제한하고, 같은 Git SHA만으로 오래된 Library snapshot을 새 작업에 재사용하지 않는다.
- 캡처 순간의 저장·import idle 확인 범위와 시간을 기록한다. 이후 원본은 계속 수정 가능하며, 작업 기간 전체의 쓰기 차단이나 Editor 종료를 요구하지 않는다. 미저장 메모리 상태는 지원 범위에 포함하지 않는다.
- VSS의 권한 있는 capture/observe/release를 Windows 어댑터와 지속 영수증으로 연결한다. 일반 CLI와 Unity 전체를 관리자 권한으로 실행하는 구조를 만들지 않는다. 권한 경계가 준비되지 않았으면 JSON의 안정된 코드와 `nextActions`로 중단하고 재개 지점을 남긴다. 무인 설치·상시 권한 서비스는 후속 범위다.
- Git은 지정 어댑터로 no-checkout 등록·독립 index·전용 `.git` pointer를 연결한다. 읽기는 불변 snapshot+delta, 쓰기는 변경 범위와 메타데이터에 한정한다. 최초 전체 payload 복사·읽기 hydration·파일 전체 copy-up·원본 fallback은 금지한다.
- `workspace list/status`와 기존 `unity start/refresh/stop`에서 snapshot 가용성, mount/제공자 상태, 앱 상태, 복구 행동을 함께 판단할 수 있게 한다. 작업별 임시 요청 JSON을 사람이 조립하는 절차는 제품 정상 흐름에서 제거한다.

### 3. 검증·독립 결과·회수

- Unity의 기존 제품 소스 fingerprint·요청 ID·컴파일/리로드 세대 계약을 NTFS 경로에도 연결한다. probe 한 파일만으로 전체 workspace 성공을 발행하지 않는다. 검증 뒤 소스 변경, 오래된 callback/assembly, 누락된 자원 관측은 성공으로 처리하지 않는다.
- 작업 overlay의 검증된 ephemeral PID/port만 whiteout하고 원본 helper·MCP·Library를 보존한다. 작업의 실제 새 코드 실행 증거와 원본/A/B의 독립 진행을 확인한다.
- checkpoint는 검증한 소스와 `.meta`를 Git result ref/commit과 manifest로 독립 보존한다. 도구 브리지·재생 가능한 캐시는 소스 결과와 구분하고, 미분류 파일·Git에 보존되지 않은 사용자 결과는 조용히 폐기하지 않는다. 결과 불완전 시 delta와 snapshot을 유지한다.
- 검증 성공과 실패 결과 보존을 구분한다. 실패하거나 검증 전인 변경도 유일한 결과인 동안 유지하며, 단순 보존을 validation 성공으로 승격하지 않는다.
- 원본 반영은 명시적인 `workspace integrate`로 수행한다. 기존 로컬 통합 계약을 연결하고, 대상 변경·dirty 상태·충돌은 보존/차단한다. 독립 결과가 검증되면 원본 통합을 강제하지 않고 workspace 회수를 허용한다. 자동 통합 후보 생성·재검증은 후속 범위다.
- 회수는 앱/자식 종료 확인 → 결과의 최신성·독립성 확인 → flush/unmount와 제공자 종료 → 소유 Git 등록 회수 → delta 회수 → 마지막 의존성이 없는 소유 snapshot 해제의 사후조건으로 관리한다. 부분 완료를 다시 관측해 재개한다.
- snapshot 의존성은 지속 기록과 실제 상태를 대조한다. 다른 작업이나 미보존 결과가 남으면 기준을 해제하지 않는다. 결과 ref/manifest·작은 복구 기록은 회수 후에도 남긴다.

### 4. 공간·실패 보호와 계측

- 기존 VSS/delta 공간과 한도를 확인해 새 작업의 수용 여부를 판단하고, 압박·snapshot 소실·delta 한도 초과를 명시 상태로 반환한다. 프로젝트 밖 쓰기가 VSS에 영향을 주는 한계를 표시한다. 압박·lease 만료·timeout만으로 유일한 결과나 기존 복원 지점을 삭제하지 않는다.
- VSS 상한·드라이버·볼륨 설정을 자동 변경하지 않는다. 설정 변경이 필요하면 실제 필요한 조치와 재개 가능한 상태를 반환한다. 관측되지 않는 사용량은 `unknown`으로 구분하며 0으로 보고하지 않는다.
- 이번에 추가하는 외부 부작용 단계의 전후 중단·응답 유실·중복 요청을 fake로 검증한다. 실제 fixture에서는 CLI 재실행, 제공자 종료·같은 기준 재연결, quota disk-full, snapshot 소실 시 보존, 부분 회수를 대표 사례로 확인한다. 모든 Windows I/O 경계의 강제 중단은 후속 관문이다.
- 초기 payload, 읽기 cache, 저널 payload/metadata, 실제 file-data allocation, 볼륨/VSS 사용량, 결과 저장량, 소유 메모리와 단계 시간을 구분한다. Unity Library 재작성·전체 소스 검증 비용도 기록한다. 알파 통합 단계에서 시작 시간 SLA나 대형 프로젝트 성능을 약속하지 않는다.
- 현재 검증하지 않은 ADS/hardlink/reparse/EFS/개별 ACL 변경, dirty/LFS 입력 등은 지원으로 노출하지 않고 지원 검사/안정된 오류로 경계를 둔다. 필요한 preflight 거부는 이번 범위에 포함하며 해당 기능의 구현은 후속 범위다.

## 완료 기준과 실행 순서

상태는 [검증 추적표](validation-matrix.md)의 **NI-01~08**로 별도 관리한다. SO 통과를 NI로 자동 승계하지 않는다.

1. **NI-01~02:** Core/fake·지속 상태·schema를 먼저 연결한다. 두 CLI의 같은 요청, 입력 충돌, 응답 유실과 부분 완료에서 작업·snapshot·등록·프로세스가 중복되지 않아야 한다.
2. **NI-03:** 일반 workspace CLI를 통해 비희소·비압축 16MiB/256MiB/1GiB 파일의 생성·반복 읽기·같은 4KiB 쓰기를 측정한다. 전체 payload 복사·hydration 0과 변경 범위에 따른 저장량을 다시 확인한다.
3. **NI-04~05:** 기존 Unity fixture의 clean 기준에서 A/B를 만들고 원본도 계속 수정한다. A 컴파일 실패 중 B가 진행하며, 고친 뒤 실제 로드된 새 코드와 정확한 fingerprint를 확인한다. 새 CLI와 Editor stop/start 후 동일 작업을 재개한다.
4. **NI-06~07:** 성공 결과·미보존 변경·유효하지 않은 snapshot·quota·부분 실행을 구분해 결과 보호와 회수 재시도를 검증한다. 회수 후 앱/자식/제공자 process·소유 메모리·핸들 0, mount·경로·Git 등록·delta 부재와 마지막 소유 snapshot 해제를 관측한다. 다른 작업과 원본·기존 복원 지점은 보존한다.
5. **NI-08:** `pwsh ./scripts/build.ps1`의 locked restore·Release build·자동 시험과 실제 NTFS/Git/Unity 증거를 함께 남긴다. 선택적 ReFS 미실행과 실제 Windows/Unity 시험을 각각 표기한다. schema·capabilities·fake·현재 계약·사용법·검증 문서를 동기화하고 UTF-8을 확인한다.

납품물은 제품 코드/계약/시험, CLI만으로 재현하는 절차, 단계별 자원·저장량 보고, 정상/실패/회수 원시 증거와 작은 추적 보고서다. 필수 NI 항목의 실행 증거가 없으면 구현 완료와 검증 대기를 구분하며 이 내부 알파 통합 전체를 완료로 표시하지 않는다.

## 후속 작업으로 남기는 범위

- 재부팅 후 같은 VSS identity 재연결, 전원 손실, 실제 host 볼륨 고갈, 모든 mount/flush/보존/회수 명령 경계의 실제 장애 행렬.
- 일반 dirty/staged/untracked 원본의 상속, LFS·사용자 filter, linked-worktree 원본·하위 Unity 프로젝트, 특수 파일 특성의 지원 확대.
- 에셋·패키지 변경의 일반 검증과 긴 Unity 패키지 경로 호환성, 씬/프리팹 저작, Play, Unity Test Framework 제품 명령. 이번 깊은 home에서 초기 패키지 DLL 로딩 오류가 관측됐으며 스크립트 컴파일 성공으로 일반 패키지 지원을 대신하지 않는다.
- 초기 대량 import·metadata 탐색 최적화, 저널 compaction, 대형 실프로젝트 성능 보장.
- daemon/SQLite Host, 설치·업데이트·제거, Codex Runner, DAG/자원 예약, 자동 통합 후보 검증, 다른 OS와 공개 48개 전체 통과.

이 중 다음 작업은 [NTFS 내부 알파 2차: 복구와 파일 지원 경계](ntfs-recovery-and-support.md)의 NR-01~08로 구체화했다. NR-01의 공통 복구 계약을 부분 구현했고 VIVOBOOK에 fixture를 복사해 필수 도구를 준비 중이다. 실제 재부팅·공간 장애는 아직 미실행이며 [NR 기록](../verification/ntfs-recovery.md)을 따른다. 나머지는 [NTFS 제품 통합의 남은 조건](ntfs-snapshot-overlay-spike.md#제품-통합-전에-남은-조건)과 공개 로드맵에 유지한다. 1차 통합이 끝나도 일반 NTFS/Unity 지원 완료로 표시하지 않는다.

## 테스트 자원

**이번 범위에 구매 에셋은 필요하지 않다.** 기존 `splinter-unity`와 재현 스크립트가 만드는 비희소 합성 파일로 저장·수명주기·실제 컴파일 경계를 검사한다. 난수 바이너리는 저장소 시험용이며 Unity 임포트 성능의 대체물이 아니다.

후속 에셋 호환성/실프로젝트 성능 단계에서 필요하면 텍스처·모델·오디오 등 어떤 종류와 규모가 필요한지 먼저 정해 사용자 보유 에셋을 요청한다. 지금 임의 구매·다운로드·임포트를 전제하지 않는다. 유료 에셋을 사용할 경우 공개 fixture와 별도의 비공개 시험 자원으로 관리한다.
