# NTFS 내부 알파 2차: 복구와 파일 지원 경계

계획일: 2026-09-21. 상태: **NR-01/03/05/07 부분 구현·자동 검증, 원격 VSS workspace 읽기·소유 자원 회수와 마지막 상태 전환 복구 확인**. 현재 실행 범위는 [NR 검증 기록](../verification/ntfs-recovery.md)을 따른다. 사용자가 제공한 VIVOBOOK은 ARM Windows 11, RAM 32GB이며 지정한 `splinter-test-vivobook` 프로젝트를 사용한다. 기존 Unity fixture의 동일 revision·77개 파일 복사와 이전 후보의 로컬·원격 자동 시험 각각 139개 통과를 확인했다. 기존 x64 Unity 6000.3.22f1을 유지하며 ARM64 Editor를 추가하지 않는다. 합성 입력의 실제 WinFsp 기본 I/O·파일 API 5개 거부·회수와 VSS 상한 32GiB 증가를 확인했고 실제 제품 workspace에서 77개 파일 읽기도 통과했다. 읽기 시험의 소유 자원 0과 기존 snapshot 4개 보존을 확인했다. 삭제된 장치 경로 관측 때문에 releasing에 남는 결함을 수정했고 같은 workspace의 비관리자 release/status가 released까지 완료됐다. canonical x64 Unity의 첫 임포트는 180초 안에 완료했고 기존 MCP 연결과 Console 준비도 완료했다. ARM Windows/x64 Unity의 단일 제품 workspace에서 스크립트 검증 2건·독립 결과 보존·회수를 통과했고 남은 소유 workspace 자원은 0이다. canonical Editor/MCP 세션과 임시 전송 서버도 정리해 이번 원격 실행을 완료했다. A/B 동시 회귀·재부팅·공간 고갈·전원 중단은 남은 관문이며 후속 장애 계획에 따라 시험 PC를 다시 사용한다.

목표는 **중단되거나 재부팅된 NTFS 작업을 같은 불변 기준으로 재개하고, 재개할 수 없는 경우에도 결과와 소유권을 보존하며 다음 행동을 명확히 반환하는 것**이다. [기획서 15.1절](../../plan.html#section-15-1)의 복구·파일 지원 관문을 이번 단위로 구체화한다. 제한된 fixture의 실제 A/B 작업과 비용 관측은 NR 전체 완료를 기다리지 않고 아래 관문으로 먼저 확인한다. 일반 프로젝트·에셋·Play·테스트의 지원 확대 조건은 유지한다.

2026-09-22의 [live VSS 식별·재개 단위](ntfs-live-vss-identity.md)는 LV-01~08의 정의된 수준에서 완료했다. 새 로컬 후보의 자동 180 통과, 실제 VSS/Git 재연결·범위 쓰기와 단일 Unity 검증 3건·결과·회수는 [LV 기록](../verification/ntfs-live-vss-identity.md)을 따른다. NR-01 전체나 NR-01~08 전체 완료는 아니며 나머지 중단·파일 경계, 정상 재부팅·강제 전원 종료·물리 공간 고갈·ARM A/B를 이어 간다.

## 출발점과 우선순위의 근거

2026-09-25의 [공통 저장소 실행](../verification/storage-foundation.md)에서 정확한 소유 ID의 VSS 해제 중 기존 비소유 snapshot 5개가 소실됐다. 해당 실행은 FailedPreservation이며 기존 원격 보존 성공을 이 환경의 보장으로 확대하지 않는다. NR-06/07의 후속 우선 관문에 격리 볼륨의 다른 snapshot 전후 ID 보존·해제 순서·연쇄 삭제 탐지를 포함한다. [후속 시험](../verification/storage-recovery.md)에서 격리된 소유 snapshot의 삭제 연쇄를 재현했고, 현재 guard의 위험 요청 차단·오래된 순서 회수는 실제 통과했다. 일반 비소유 백업 공존은 미검증이다. 시스템 볼륨의 기존 snapshot 삭제로 우회하지 않는다. B와 같은 flush 결과 게시 오류는 정상 polling reader와 Move(overwrite)의 충돌로 재현해 수정했으며 당시 핸들 소유 프로세스는 미확정이다. 다음은 지연 회수 상태·재시도·잔존 공간 비용의 계약이다.

아래 표는 2차 전체 계획을 처음 수립한 NI 기준이다. 이전 `591d53f` 후보와 121개 소스 hash·139개 통과 기록의 대조는 [후속 계획의 현재 도달점](ntfs-live-vss-identity.md#제품-목표와-현재-도달점)에 있다.

| 항목 | 이번 계획에서 확인한 근거 | 계획에 미치는 영향 |
| --- | --- | --- |
| 제품 checkout | `development`, `2bae9c461a86f74880ab10da473101b3ca68f12b`, 검토 시작 시 clean | 이 후보를 회귀 기준으로 삼고 기존 NI/SO 자원을 재생성하지 않는다 |
| 기존 실행 증거 | [NI-01~08](../verification/ntfs-workspace-integration.md)의 생성·범위 저장·원본/A/B 스크립트·재연결·독립 결과·dirty 원본 통합 차단·회수 완료 | 이미 증명한 정상 수명주기 위에 복구를 보강한다 |
| 자동 시험 | 기존 `final-build.trx`: 총 98, passed 97, failed 0, 선택적 ReFS 1 skip. 기록된 소스 117개를 현재 파일 hash와 대조해 모두 일치 | 이번 계획에서 빌드나 실행 시험을 다시 했다는 뜻은 아니다 |
| Unity fixture | 형제 `splinter-unity`, clean `development`, `46e7f4dbdb219e4afa602f3598a3de9ff4070fae`; Unity `6000.3.22f1`, URP `17.3.0`, UTF `1.6.0` | 버전 업그레이드 없이 기존 스크립트 fixture를 회귀에 사용한다 |
| 아직 없는 증거 | OS 재부팅·강제 전원 종료·물리 공간 고갈·모든 외부 부작용 경계의 실제 중단 | provider 종료 재연결과 quota 시험을 이 증거로 대신하지 않는다 |
| 파일·경로 제한 | ADS/hardlink/reparse/EFS/압축/희소 입력 거부, ACL 재현 미지원. NI에서 270자 패키지 DLL 경로의 초기 오류 관측 | 새 파일 형식을 지원하기 전에 검사·거부·경로 진단을 확정한다 |

근거 파일은 `artifacts/ntfs-workspace/ni-20260921/evidence/source-manifest.json`과 `final-build.trx`다. 이번 조회는 파일·Git·기록을 읽은 결과이며 현재 mount, Editor, VSS의 OS 상태를 새로 검증한 결과가 아니다.

결과 손실과 회수 경계의 결함을 먼저 수정한다. 동시에 저장소 장애표만 확장하다가 실제 병렬 작업의 이익과 운영 비용 확인이 늦어지지 않도록, 제한된 기존 fixture의 아래 PV 관문에서 확보한 작업·비용 증거와 사용자 선택 범위 마감 판정을 보존한다. 일반 프로젝트를 투입하거나 재부팅·전원·공간 장애 지원을 선언하려면 해당 NR 관문을 통과해야 한다.

## 제한된 병렬 작업과 비용 관문

선택 범위를 마감한 PV의 상세 정의는 [독립 대화 A/B의 NTFS 작업과 비용 검증](parallel-workflow-qualification.md)에 보존한다. checkpoint 전 여러 차례 편집·재개, A 선행 통합/회수와 B 지속, 현재 integrate 이후 원본 Unity 확인, 실제 모델·텍스처를 포함한 Codex 기본 worktree 직접 비교와 필요한 결함 수정까지다. PV-01의 대표 fixture 수명주기는 복구·운영 개입을 포함해 완료했고 PV-02 사용자 선택 범위는 `scoped-complete-with-limitations`로 마감했다. S4 `FailedInputQualification`·소수 표본·과거 전체 팩 raw 동등성 unknown을 유지하며 PV-02 전체 Passed로 올리지 않는다. 아래 NR 전체 범위와 구별한다.

PV-01의 출발점은 현재 지원하는 짧은 home·clean 저장소 루트·C# 스크립트 변경이다. 서로 독립된 대화 A/B의 AI가 제품 CLI로 자기 workspace를 운영한다. PV-02는 실제 모델·텍스처의 임포트와 제한된 변경을 추가하고, 별도 Codex managed worktree 대화 2개의 같은 작업과 비교한다. 이 비교군은 Splinter에 중첩하지 않는다. 별도 조정 AI나 하위 작업자 자동 실행을 전제하지 않는다. 여러 차례 수정 요청, 새 CLI/대화 재개, A 선행 통합·회수 후 B 지속 작업을 확인한다. 아직 없는 Codex Runner·daemon·DB·DAG를 이 검증의 선행 구현으로 만들지 않는다. 실패한 작업의 유일한 결과는 보존한다.

| ID | PV 실행 단위 | 완료 조건 |
| --- | --- | --- |
| PV-01 | 독립 대화 A/B의 여러 차례 작업 → A 오류 중 B 검증 지속 → 같은 작업 재접속 → A 먼저 통합·회수 → B 계속 작업 후 완료 | 작업별 요청·소스 fingerprint·loaded-code·결과 ref를 연결. 원본 편집의 지속과 원본 Editor/MCP 보존, 다른 작업 영향 없음, 소유 자원 회수, 대화별 연결·명령 수·수동 운영 개입 수를 기록. 사용자의 수정/종료 지시는 정상 사용으로 분리. 기존 NI/LV 시험을 새 실행으로 합산하지 않음 |
| PV-02 | 실제 Codex 기본 worktree와 같은 모델·텍스처 포함 Unity 프로젝트의 병렬 2작업 직접 비교 | 새 작업과 기존 작업 재개의 Library 조건, 코드 수정·지정 에셋 재임포트·완료/회수의 시간/읽기/물리 저장/메모리/개입 수, 동일 결과와 보존 증거. 기본·대표 프로필을 실제 실행하며 초기 payload 복사/읽기 hydration 0은 Splinter 필수 계약으로 유지. 합성 파일 시험은 별도 보조 근거 |

현재 PV-01은 대표 fixture에서 복구·운영 개입을 포함한 전체 수명주기와 마지막 소유 자원 회수를 확인했다. **PV-02 사용자 선택 실행 범위는 `scoped-complete-with-limitations`로 마감했으며 PV-02 전체 Passed나 일반 지원 확정은 아니다.** S4 입력 실패·소수 표본·과거 전체 팩 raw 동등성 unknown, 실제 보존·회수·원본 복원은 [실행 기록](../verification/parallel-workflow.md)과 [최종 마감 영수증](../../artifacts/parallel-workflow/pv-20260923/final-closeout.json)을 따른다. 설치·자동 binding·반영 없는 폐기는 [대화별 작업 계획](session-workflow-and-installation.md)의 SW-01~10에서 구현 후 검증한다. 기존 CLI로 가능한 PV는 그 전체 구현을 기다리지 않으며, 현재 없는 discard를 강제 삭제나 실패한 release로 대체하지 않는다. 자동 시험은 실제 AI A/B나 대형 프로젝트 성능 측정을 대체하지 않는다. 결과에 따라 사용자 작업 루프의 가장 큰 비용부터 개선한다. 선택적 ReFS는 기존 회귀만 유지하고 신규 기능을 늘리지 않는다. 저널 compaction·해시 캐시·상주 Host는 실제 병목과 필요 조건을 확인한 뒤 별도 범위로 결정한다.

## 상위 NR 계획의 전체 범위

### 1. 공통 복구 계약과 기계용 응답

- 기존 `workspace ensure/status/checkpoint/release`를 정상 진입점으로 유지한다. 에이전트가 영수증 JSON을 직접 고치거나 내부 `overlay-serve`를 수동 실행해야만 복구되는 절차를 정상 경로로 만들지 않는다.
- 작업·snapshot·delta·Git 등록·결과·프로세스의 identity와 실행 세대를 대조한다. PID만 같거나 경로만 존재한다는 이유로 소유권과 같은 기준을 인정하지 않는다. 재부팅 전후에는 VSS ID와 현재 공급자·볼륨·기준 경로를 다시 확인한다.
- 관측 가능한 재개, 관측/권한 대기, 지원 불가, 영수증/데이터 손상, 소실된 기준, 회수 진행을 구분하고 안정된 오류와 `nextActions`로 연결한다. 관측 불가를 부재나 0으로 변환하지 않는다. 구체 schema와 오류 이름은 NR-01에서 기존 계약과 맞춰 고정한다.
- 같은 snapshot과 정상 저널·소유권을 확인할 수 있는 사례는 실제 재개까지 성공해야 한다. 기준 소실·손상·실행 여부 불명확 사례의 보존/차단을 정상 재개의 대체 통과 조건으로 사용하지 않는다.
- 부작용 전 intent, 실행 결과, 후속 상태 저장이 각각 어디까지 남았는지 판정한다. 실행 여부가 불명확하면 새 snapshot·provider·worktree를 만들지 않고 조사 가능한 보존 상태를 반환한다.
- 현재 atomic JSON·파일 잠금 구조를 보강한다. 필요한 상태 버전 변경은 기존 NI 기록의 읽기와 보존 경로를 포함한다. Core에는 OS/Git/Unity API를 넣지 않으며 daemon·SQLite 전환은 이번 범위에 넣지 않는다.

기존 공통 계약과 자동 시험은 재사용하고 이번 행렬에서 부족한 상태·장애 사례만 추가한다. 먼저 새로운 상태 체계를 전면 도입하거나 정상 경로를 다시 작성하는 작업으로 확대하지 않는다.

VIVOBOOK의 이전 후보는 삭제된 정확한 snapshot의 현재 부팅 내 부재를 확인했다. 새 로컬 후보는 살아 있는 VSS ID·native provider·volume identity 대조, 불변 identity/현재 읽기 위치 분리, 비관리자 재개·legacy 보존까지 구현·검증했다. 캡처와 관측의 현재 계약은 [NTFS 작업공간](../ntfs-workspaces.md), 새 증거는 [LV 기록](../verification/ntfs-live-vss-identity.md)에 있다. 아래의 나머지 중단·파일 조건과 NR-04/06/08은 남아 있다.

현재 `SplinterSnapshotProbe` 자체 callback은 값·generation·PID를 기록하고 assembly MVID는 기록하지 않는다. 제품 WorkspaceBridge의 Ready heartbeat는 현재 로드된 값·generation·assemblyMvid·PID를 보고한다. NR-08과 PV에서는 실제 필드·관측 시각·변경한 함수를 먼저 확인하고 부족한 계측만 보완한다. 이전 callback이나 준비되지 않은 필드를 최신 loaded-code 증거로 보고하지 않는다.

### 2. 명령 경계와 데이터 지속성

아래 경계마다 **실행 전, 부작용 완료 후 영수증/상태 저장 전, 상태 저장 후 응답 전**을 구분한다. 모든 조합은 fake 적합성 시험으로 먼저 고정하고, 실제 제품 어댑터에서는 표에 열거한 경계의 프로세스 종료·응답 유실을 재현한다. 커널 내부의 모든 명령어 위치를 시험했다고 주장하지 않는다.

| 경계 집합 | 실제 중단 지점과 대조 자료 |
| --- | --- |
| 준비 | VSS capture와 반환 ID 저장, Git 등록, delta identity 생성, provider 시작·mount 관측, 독립 index 연결, Ready 저장 |
| 쓰기 | 저널 header/payload/checksum 쓰기와 flush, rename/replace/truncate, mmap의 명시적 flush, 동시 읽기와 writer 재연결 |
| 결과 | 소스 고정·앱 종료, 결과 commit/ref, manifest와 전체 저널 seal 저장. 결과 ref만 있거나 manifest만 있는 경우 포함 |
| 회수 | 앱/자식 종료, flush/seal, unmount/provider 종료, Git 등록 해제, delta 삭제 intent/부분 삭제, VSS 해제와 released 기록 |

- 저널에서 승인 완료된 쓰기와 flush 전 mmap/아직 응답하지 않은 쓰기를 구분해 oracle을 만든다. 완료된 레코드의 checksum·sequence 불일치는 보존/차단하고, 불완전한 마지막 레코드의 처리 규칙은 먼저 계약으로 고정한다. 손상을 조용히 정상 데이터로 취급하지 않는다.
- 정상 재부팅, provider/CLI 강제 종료, VM 강제 전원 종료를 별도 사례로 기록한다. 정상 종료나 hypervisor의 저장·일시 중지는 강제 전원 종료 증거가 아니다. VM 시험의 결과를 실제 하드웨어 전원 장애 전체 보장으로 확대하지 않는다.
- 재부팅 뒤 원본을 변경하고 기존 key가 여전히 이전 snapshot/delta 내용을 반환하는지 확인한다. 새 작업의 새 캡처와 기존 작업의 재개를 구분한다. 원본을 장기간 잠그지 않는다.
- 준비 실패로 Ready에 도달하지 못한 작업의 복구도 포함한다. 사용자 쓰기 없음과 소유 자원을 입증할 수 있을 때의 재개/회수 경로를 정한다. 입증할 수 없으면 영수증·delta·snapshot을 보존한다. 포괄적인 강제 삭제 옵션은 만들지 않는다.

### 3. 물리 공간 부족과 결과 보존

- 현재 quota `DeltaFull`과 구분하여 격리된 시험 볼륨의 실제 공간을 소진한다. 원본/VSS 보존 공간, delta·상태 저장 공간, 결과 Git 객체 공간을 각각 따로 시험한다.
- 상태 저장 자체가 실패할 때도 성공 응답을 발행하지 않고, 공간이 회복된 뒤 마지막으로 확인된 영수증·저널·결과 객체로 조정한다. 압박만으로 유일한 결과나 소유하지 않은 snapshot을 삭제하지 않는다.
- snapshot이 사라진 경우 미보존 변경, 보존 완료 결과, 보존 뒤 늦은 쓰기를 나눈다. 결과 ref/tree/manifest와 seal을 확인한 경우에만 기존 회수 계약에 따라 진행한다.
- 일시적으로 offline인 볼륨, 접근 거부, 삭제 확인을 구분한다. 경로가 안 보이는 것만으로 결과/등록을 정리하지 않는다. 다시 연결한 볼륨이 같은 identity인지 확인한다.
- 회수 완료는 앱/자식/provider와 소유 메모리·핸들 0, 실제 mount·Git 등록·delta·마지막 소유 VSS 부재, 독립 결과 및 다른 작업 보존으로 판정한다. 재부팅 전 캐시된 영수증만으로 완료를 발행하지 않는다.

### 4. 파일 지원표와 짧은 경로

이번 목표는 일반 NTFS 특성 전부의 구현이 아니라 **현재 허용하는 조합과 거부하는 조합을 실행 증거로 고정하는 것**이다. 아래는 목표 정책이며 현재 지원 완료 목록이 아니다.

| 조합 | 이번 목표 | 검증할 경계 |
| --- | --- | --- |
| 일반 파일·디렉터리, 0바이트·경계 크기·큰 파일 | 현재 동작 유지 | 범위 쓰기·mmap flush·rename/replace/truncate, 재연결 후 길이/hash와 초기 payload/hydration 0 |
| ADS, hardlink, 일반 symlink/junction/reparse, EFS, 압축·희소 | 지원 확대 없이 명시적 거부 | 원본 preflight, 캡처한 기준, 작업 중 생성/속성 변경 시도. 다른 경로와 기존 바이트 불변 |
| 원본 파일별 ACL·소유자·명시적 deny/상속 차이 | 허용 ACL 전제와 미지원 조합을 정의하고 위반 시 차단 | 현재 overlay의 단일 보안 descriptor와 차이를 조사. ACL을 조용히 평탄화하고 원본 의미를 재현했다고 표시하지 않음 |
| WinFsp mount root와 일반 링크 | 관측한 소유 mount root만 허용 | 내부 링크·상위 경로 바꿔치기·회수 직전 소유권 변경 거부 |
| 한글·공백·대소문자·긴 경로 | 검증한 짧은 home 조합과 진단 정책 확정 | 기존 244자/270자 관측을 재현하고 실제 실패 경로를 기록. 보편적인 단일 길이 제한이나 OS 설정 변경으로 해결됐다고 간주하지 않음 |
| dirty/staged/untracked 입력, LFS·추가 filter, linked-worktree 원본·하위 Unity 프로젝트 | 현재 미지원 경계 유지 | 안정된 사전 거부와 원본 보존만 회귀. 상속·필터 실행 지원 구현은 다음 단위 |

새 원본에서 사전 거부할 수 있는 경우 캡처·Git 등록·Editor 시작 전에 중단한다. 캡처한 기준에서 뒤늦게 발견한 경우에는 부분 준비 복구 계약을 따른다. 파일 지원표는 OS/WinFsp/파일 특성별 실제 실행 조합을 명시하며 CoW를 보안 샌드박스로 표현하지 않는다.

## 구현 단위와 담당 경계

검증 상태의 원본은 [NR 추적표](validation-matrix.md#ntfs-내부-알파-2차-복구와-파일-지원-경계)다. 아래 파일은 우선 조사·변경할 기존 경계이며 모든 파일을 수정하라는 뜻은 아니다.

| 순서 / ID | 작업과 주요 파일 | 완료 조건 |
| --- | --- | --- |
| 1 / NR-01 | `Core/SnapshotWorkspaces.cs`, `Core/SnapshotOverlay.cs`, `Runtime/WorkspaceStore.cs`, `contracts/v1`, Protocol, fake | 복구 상태·identity·지속성·오류/nextActions와 중단 지점 목록 고정. 오래된 기록을 손실 없이 읽거나 명시 보존 |
| 2 / NR-02 | `Runtime/SnapshotWorkspaces.cs`, Windows VSS/backend/mount, Git snapshot/registration, `scripts/vss-snapshot.ps1` | 준비의 각 외부 부작용 전후 실제 중단·같은 key 동시 재호출에 중복 캡처/등록/provider 없음 |
| 3 / NR-03 | `Runtime/JournalDeltaStore.cs`, Windows WinFsp server/filesystem, 저널·프로세스 시험 | 승인된 쓰기 유지, 불완전 tail·손상 구분, flush 중단 후 재개/보존. 범위 저장 회귀 통과 |
| 4 / NR-05 | `Windows/SnapshotInputChecks.cs`, WinFsp 파일 작업, Git preflight, doctor/CLI 진단 | 허용/거부 파일·경로·ACL 표의 실제 증거. 원본·외부 경로 보존 |
| 5 / NR-07 | Runtime release/result, `Git/GitSnapshotWorkspaces.cs`, Windows backend, WorkspaceStore | 부분 준비·부분 결과·부분 회수 조정, 독립 결과 검증, 마지막 의존성 보호와 실제 자원 회수 |
| 6 / NR-04 | 위 구현과 격리 Windows 시험 harness | Ready/미수집 변경, checkpoint 완료, 회수 도중의 정상 재부팅·강제 전원 종료를 구분. 유효한 같은 snapshot/delta는 실제 재개, 소실/손상은 근거 있는 보존 상태 |
| 7 / NR-06 | 공간 관측·admission·저널/상태/결과 실패 처리와 격리 볼륨 harness | quota와 별개인 실제 공간 고갈·offline·snapshot 소실 시험, 다른 작업/기존 복원 지점/유일한 결과 보존 |
| 8 / NR-08 | build 진입점, 일반 CLI의 실제 Unity 회귀, 현재 계약·검증 문서 | 같은 최종 후보의 자동/실환경 증거와 완전한 회수. 재부팅 뒤 실제 Unity 재검증 포함 |

파일 표의 모듈 약칭은 `src/Splinter.<모듈>/`을 뜻한다. Windows는 `Splinter.Platform.Windows`, Git은 `Splinter.SourceControl.Git`이다. 새 개발용 harness는 제품 workspace 명령과 구분하고 기본값을 계획/관측으로 둔다. 실패 주입을 일반 사용자의 정상 실행에서 활성화하지 않는다. 실행 가능한 스크립트를 추가할 때 AGENTS·문서 지도·capabilities의 현재 상태도 함께 갱신한다.

작업은 **결과 보존 결함·계약·자동 시험 → 제한된 PV-01/02 실행 → NR의 실제 중단·파일 경계·재부팅·공간 장애 → 최종 지원 회귀**로 진행한다. 격리 환경 준비와 NR의 독립 계약 시험은 병행할 수 있다. PV 통과가 NR 미실행을 해소하지 않으며, 실제 관문을 통과하기 전에 지원 범위를 완료로 올리지 않는다. 일정은 환경 준비와 첫 장애 실행 비용을 확인한 뒤 정한다.

## 시험 환경과 필요한 자원

**NR 장애·파일 경계 시험에는 외부 에셋 구매·다운로드가 필요하지 않다.** 기존 Unity 스크립트와 직접 생성한 비희소·비압축 16MiB/256MiB/1GiB 파일, 작은 파일 트리·파일 특성별 fixture를 사용한다. 별도 PV-02에는 사용 조건을 확인한 실제 모델·텍스처 묶음을 준비하며 구성·규모·예산은 [직접 비교 계획](parallel-workflow-qualification.md)에 따른다. 유료 구매는 전제하지 않는다. 대형 난수 파일은 저장소 시험용이며 실제 에셋 임포트 성능의 증거로 사용하지 않는다.

사용자가 제공한 **VIVOBOOK 테스트 PC**를 다음 조건으로 준비한다. 준비와 두 작업 사이의 결과 전달은 에이전트가 담당한다. Unity fixture는 `splinter-test-vivobook/splinter-unity`, 제품 소스는 그 형제 `splinter`로 전달한다. 최초 fixture/Library 준비와 제품 workspace 생성 비용을 구분한다.

| 자원 | 준비 조건과 용도 |
| --- | --- |
| 격리 Windows | NTFS/VSS 시스템 공급자/WinFsp를 시험할 수 있고 관리자 helper·정상 재부팅·강제 전원 종료를 통제할 수 있어야 함. Windows 에디션/build·드라이버 버전은 실제 조회 후 기록 |
| 시험 볼륨 | 시스템 및 중요한 데이터와 분리한 소유 NTFS 데이터 볼륨. 원본/VSS와 delta·상태/결과의 압박을 따로 만들고, 증거는 압박 대상 밖에 보존 |
| 공간 예산 | 생성 시 현재 제품의 상태 볼륨 여유 2GiB와 VSS 상한 여유 8GiB를 충족. 실제 데이터·VSS 증가·반복 횟수를 측정해 용량 결정. 시험 볼륨만 채우며 VM 호스트 볼륨을 고갈시키지 않음 |
| 복구 제어 | VM이면 게스트 밖에서 중단 시점과 강제 전원 종료를 제어하고 증거 회수 가능. 테스트 PC이면 해당 장비만 재부팅·전원 제어할 수 있는 일정과 복구 수단 필요 |
| Unity 최종 회귀 | 정확한 `6000.3.22f1`과 현재 fixture 패키지, GUI Editor를 실행할 수 있는 환경·라이선스. 원본/A/B 동시 실행 자원 확인. NI의 A/B working set 표본만 합쳐도 약 9GiB였으므로 OS·원본 Editor·import 여유를 별도로 산정 |
| 짧은 home | 예: 시험 상태 볼륨의 `S:\s`. 긴 증거 폴더를 workspace home으로 사용하지 않음. 경로·볼륨명은 환경 확인 뒤 정함 |

환경 착수 시 필요한 정보는 Windows 버전, VM/PC 접근 방법, 사용 가능한 RAM/디스크, 전원 제어 가능 범위다. 이미 있는 도구는 재사용하고 신규 설치·볼륨 설정·전원 조작은 실제 대상과 변경 내용을 정한 실행 작업에서 처리한다. 준비 가능 응답을 설치나 현재 PC 재부팅을 수행했다는 의미로 기록하지 않는다.

별도 머신의 fixture·Library 최초 준비는 시험 환경 구축 비용으로 따로 기록한다. 그 준비 비용을 workspace 최초 생성량에서 숨기거나 제품의 작업별 전체 복제 fallback으로 사용하지 않는다.

저장소 장애 시험은 Unity 없이 먼저 할 수 있다. 그러나 그 결과와 기존 호스트의 일반 Unity 재시작을 합쳐 **재부팅 후 Unity 지원**으로 집계하지 않는다. NR-08에는 실제 재부팅한 지원 환경에서 저장된 작업의 새 PID/domain/MVID와 loaded-code, fingerprint·validation 일치를 남긴다. 해당 환경이 없으면 그 하위 사례는 NotRun이고 NR 전체 완료도 보류한다.

## 실행 증거와 완료 판정

- 새 run은 `artifacts/ntfs-recovery/<runId>/`에 계획, 환경/소스 manifest, 명령·응답, 중단 경계, 시작/종료 상태, 원시 hash·자원 계측, 결과와 회수 기록을 남긴다. 짧은 workspace home과 증거 경로를 분리한다.
- 재부팅/전원 시험은 전후 boot 식별·시간, workspace/key와 요청 hash, VSS/delta identity, 마지막으로 외부에서 확인한 쓰기·seal, Git ref/tree, 새 프로세스 시작 시각을 연결한다. 증거를 대상 VM 밖에 회수한 뒤 다음 trial로 초기화한다.
- 장애 전후 원본 HEAD·index·staged/unstaged/untracked 내용, 비소유 worktree·snapshot, 다른 작업의 hash를 대조한다. 모든 장애 시험은 자기 소유 fixture·프로세스·볼륨만 대상으로 한다.
- 저장 계약 회귀는 최초 payload·읽기 hydration 0, 같은 4KiB 쓰기의 payload 증가, metadata/할당 단위·VSS·Git 저장량을 분리한다. 읽기 I/O나 Unity의 실제 Library 재작성 비용을 0으로 보고하지 않는다.
- 최종 변경 후보에 `pwsh ./scripts/build.ps1`을 실행하고 같은 source hash의 실환경 시험을 연결한다. 선택적 ReFS 미실행과 Windows·Unity 미실행을 각각 표기한다. 변경된 입력/출력 schema, fake, 적합성 시험, 사용법·지원표·검증 추적표와 UTF-8을 확인한다.
- NR-01~08의 필수 하위 사례에 실행 근거가 모두 있어야 **이번 내부 알파 2차 완료**다. 미지원 파일의 안전한 거부 시험은 그 거부 계약의 Passed이며 해당 파일 지원의 Passed가 아니다. 환경 미비·미실행·실패는 각각 이유와 다음 조건을 남긴다.
- NR 완료도 공개 48개 시나리오 전체나 물리 장치 전원 장애 일반 지원을 뜻하지 않는다. 다음 기능 단계의 착수 기준을 확보한 것이다.

## 이후 순서와 이번에 넣지 않는 범위

| 다음 순서 | 목표와 최소 완료 흐름 | 자원 |
| --- | --- | --- |
| 3차: 일반 Unity 작업 루프 | 직접 만든 ScriptableObject/프리팹/씬/텍스처, asmdef·로컬 시험 패키지 변경 → 정확한 compile/import → 저장·재개방 → 실제 Play → EditMode/PlayMode 테스트 → 독립 결과·회수. 실패·0개 선택·취소·stale 증거 구분 | 기존 fixture를 계획된 시험 소스로 확장. 처음에는 자체 생성 에셋으로 충분 |
| Git 입력·통합 확대 | staged/unstaged/untracked와 `.meta` 상속, LFS/attributes, linked-worktree·하위 프로젝트, clean 통합 성공·두 NTFS 결과 충돌·최신 통합 후보 재검증 | 별도 Git fixture와 자체 생성 LFS 바이너리. 현재 dirty 원본 거부 증거를 상속 지원으로 바꾸지 않음 |
| 대표 에셋·성능 확대 | PV-02의 모델·텍스처 직접 비교 이후 더 다양한 에셋/오디오·실사용 프로젝트의 초기 import·반복 변경·metadata/저널 비용으로 확대. 측정한 병목부터 개선 | 필요 시 사용자 보유 2D/3D·오디오 또는 대표 프로젝트 일부를 종류·규모·허용 범위를 지정해 요청. 유료/개인 데이터는 공개 fixture와 분리 |
| 공개 운영 | 프로젝트용/전역용 설치·업데이트, 현재 Codex 대화 연결·재개, 필요 시 사용·미사용, 통합 후보·명시적 폐기와 공개 48개/SW 검증 | 독립 대화 시험·공유 자원 정책. Runner/DAG·daemon/DB는 선택적 후속 |

daemon/DB 교체, 임의 에셋 구매·수집, 범용 특수 파일 지원, LFS/dirty 입력 구현, 저널 compaction과 성능 SLA, 다른 OS, 원격 게시·릴리스는 이번 NR 단위에 포함하지 않는다. 공개 v0의 저작·Play·테스트 요구사항은 그대로 유지하며 후속 공개 루프에서 반드시 닫는다.

PV-02의 추가 비교군은 Codex worktree 코드 병렬 작업과 Unity Agent Coordination 단일 Editor 순차 검증이다. 큐 대기·후보 전달·공용 Library와 두 소스 checkout의 비용을 별도로 측정하며 Splinter 실행에는 Coordination을 사용하지 않는다. 상세 조건과 실제 실행 여부는 [PV 범위](parallel-workflow-qualification.md)와 실행 기록을 따른다.
