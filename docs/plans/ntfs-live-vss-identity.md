# NTFS 후속 구현 범위: 살아 있는 VSS 식별과 같은 작업 재개

계획일: 2026-09-22. 상태: **LV-01~08의 정의된 자동/실환경 검증과 회수 완료**. 상위 계획은 [NTFS 내부 알파 2차](ntfs-recovery-and-support.md)다. 이번 구현 단위는 NR-01의 live VSS identity와 이에 필요한 NR-07 회수·NR-08 정상 Unity 회귀에 한정한다. 이 문서의 완료는 NR-01 전체나 NR-01~08 전체 완료를 뜻하지 않는다.

## 제품 목표와 현재 도달점

[기획서](../../plan.html#section-1)의 목표는 AI가 Windows/NTFS + Git + Unity에서 독립 작업공간의 준비·병렬 편집·실행·검증·결과 통합·회수를 운영하는 것이다. 원본 위치와 파일시스템을 유지하고, 원본을 계속 편집할 수 있어야 한다. 작업은 시작 시점의 불변 스냅샷을 읽으며 최초 전체 복제·읽기 hydration·파일 전체 copy-up 없이 변경 범위만 저장한다. CLI와 기계용 계약이 기본이고 Core는 Windows·Git·Unity·에이전트 구현에 의존하지 않는다.

공개 v0에는 에셋 저작·실제 Play·EditMode/PlayMode 테스트, Codex Runner·DAG·자원 예약·설치와 공개 48개 시나리오까지 필요하다. 현재 도달점은 **간단한 Unity 스크립트 병렬 편집을 위한 NTFS 내부 알파**다.

| 구분 | 확인한 완료 범위 | 남아 있는 범위 |
| --- | --- | --- |
| 공통 기반 | JSON/schema, fake, atomic JSON·operation lock, ensure/status/checkpoint/integrate/release, Unity start/refresh/stop | 상주 Host/DB, 제품 agent runner·DAG 실행·설치 |
| NI-01~08 | 제품 CLI의 NTFS 생성·범위 저장·원본/A/B 스크립트 격리·재연결·독립 결과·dirty 원본 통합 차단·소유 VSS까지 회수 | 일반 프로젝트 지원과 모든 장애 조건을 포함한 완료가 아님 |
| NR-01/03/05/07 | available/missing/unknown·복구 행동, 상태/저널 손상 보존, 일부 파일 속성 거부, 현재 부팅의 정확한 VSS 해제 관측 | 살아 있는 VSS 재식별, 모든 준비/결과/회수 중단 경계, 파일·ACL·경로 표 |
| ARM Windows/x64 Unity | 기존 6000.3.22f1의 단일 제품 workspace에서 스크립트 검증 2건·독립 결과·회수, 시험 세션 종료 | ARM A/B 동시 실행·재부팅 후 Unity·실제 전원/공간 장애 |
| 별도 증거 | SO-01~06 실험, 기존 checkout·선택적 ReFS 알파 | SO/ReFS 성공을 NI/NR/공개 시나리오에 승계하지 않음 |

이번 계획 검토에서 확인한 기준은 다음과 같다.

- 제품은 clean `development`, `591d53fe5dc66e632eb92cdb8e108df7497b0fb6`에서 시작했다. 보관된 [소스 manifest](../../artifacts/ntfs-recovery/nr07-release-observation-20260921/source-manifest.json)의 121개 파일 hash가 현재 파일과 모두 일치했다. aggregate는 `78d285261a1a5899f137c39e1193eb3dc0c6e00d1d4523870bdd709c7af7140e`다.
- 같은 기록의 [final-build.trx](../../artifacts/ntfs-recovery/nr07-release-observation-20260921/final-build.trx)는 총 140개 중 139 통과·0 실패·선택적 ReFS 1개 미실행이다. 이번 계획 작업에서 빌드·제품 시험을 재실행한 수치가 아니다.
- 형제 Unity fixture는 clean `development`, `46e7f4dbdb219e4afa602f3598a3de9ff4070fae`, `6000.3.22f1`이다. 원격의 [최종 보고](../../artifacts/remote-vivobook/transfer/vivobook-report-1790004706952742900.json)는 검증 2건·남은 소유 workspace 자원 0·세션 정리 완료를 기록한다. 원격 현재 OS 상태를 새로 관측한 결과가 아니다.
- 실행 범위의 원본은 [NI 기록](../verification/ntfs-workspace-integration.md)과 [NR 기록](../verification/ntfs-recovery.md)이다. LV 당시에는 제품 WorkspaceBridge의 Ready heartbeat가 함수 값·domain generation·MVID·PID를 보고했다. 현재 제품 heartbeat는 fixture를 호출하지 않으며 새 loaded-code 검증에는 공통 Qualification fixture의 요청 ID가 일치하는 `inspect` 응답을 사용한다. 과거 LV 영수증과 현재 호출 계약을 구분한다.

## 계획 당시 경계와 변경 책임

| 계획 당시 코드 | 확인한 한계 | 이번 변경 책임 |
| --- | --- | --- |
| [VssSnapshotProvider](../../src/Splinter.Platform.Windows/VssSnapshotProvider.cs)의 ObserveAsync | released가 아닌 영수증은 주로 경로 관측을 반환한다. 현재 부팅 증거는 마지막 해제 관측에 사용한다 | 살아 있는 정확한 VSS 객체의 식별 정보와 캡처 기록을 대조 |
| [VSS helper](../../scripts/vss-snapshot.ps1) | 캡처 영수증에 native provider는 있지만 원본 volume identity·상대 경로가 별도 고정되지 않는다. 관측에 저장하는 snapshot은 기존 영수증 값이다 | 캡처 정보와 실제 조회한 객체의 값을 분리해 저장 |
| [SnapshotAvailabilityProbe](../../src/Splinter.Platform.Windows/SnapshotAvailabilityProbe.cs), [VssSnapshotTree](../../src/Splinter.Platform.Windows/VssSnapshotTree.cs) | 저장한 root 경로 접근 가능성을 관측·읽기에 사용한다 | 검증된 현재 읽기 위치만 사용하며 경로의 존재를 identity 증거로 사용하지 않음 |
| [JournalDeltaStore](../../src/Splinter.Runtime/JournalDeltaStore.cs), backend·mount request | RootPath를 포함한 SnapshotReference/OverlayIdentity를 대조한다 | 현재 읽기 위치의 변경이 기존 delta identity·저널 변경으로 이어지지 않도록 분리 |

Microsoft는 snapshot ID, provider ID, 원본 볼륨과 읽기용 device object를 별도 속성으로 정의한다. 이를 근거로 **불변 식별 정보와 현재 접근 위치를 분리**한다. 이 API 정의 자체는 이 제품의 재부팅 성공 증거가 아니다. [VSS_SNAPSHOT_PROP](https://learn.microsoft.com/en-us/windows/win32/api/vss/ns-vss-vss_snapshot_prop), [Win32_ShadowCopy](https://learn.microsoft.com/en-us/previous-versions/windows/desktop/legacy/aa394428%28v%3Dvs.85%29).

## 확정한 구현 범위

### 1. 캡처 정보·현재 관측·읽기 위치를 분리한다

- 캡처 영수증에 버전, 소유 작업, snapshot ID, native provider ID, 원본 volume identity, 볼륨 내 프로젝트 상대 경로와 현재의 persistent/differential/no-auto-release 조건을 고정한다. 드라이브 문자나 디렉터리 존재만으로 볼륨을 식별하지 않는다.
- 실제 조회 응답에는 조회한 ID/provider/volume/device 경로, 현재 boot, 관측 시각과 요청/연결 세대를 담는다. 저장된 snapshot 객체를 그대로 복사해 live identity 검증으로 표시하지 않는다.
- 기존 SnapshotReference와 delta identity는 불변으로 유지하고 현재 읽기 위치를 별도 접근 정보로 전달한다. 새 위치는 검증된 device root와 고정한 상대 경로에서만 구성한다. 경로 탈출·일반 원본 경로·다른 snapshot으로의 전환을 거부한다.
- VSS/CIM 전용 필드는 Windows 어댑터가 소유한다. Core에는 공급자 중립적인 식별·관측·접근 계약만 필요한 만큼 추가한다. 기존 저널 형식 변경이나 전체 payload 이관은 목표가 아니다.

### 2. 비관리자 CLI에서 같은 작업을 재개한다

- 정상 진입점은 기존 ensure/status/checkpoint/release다. 관측 권한이 없으면 정확한 `-Action Observe` helper와 원래 명령의 동일 인자 재시도를 반환한다. 이 helper는 VSS·원본·설정을 변경하지 않고 소유 관측 기록만 저장한다. CLI/Unity를 상시 관리자 프로세스로 바꾸지 않는다.
- 새 연결 세대에서 최신 식별 확인을 요구한다. 이전 boot, 다른 owner/snapshot/요청, 실패한 최신 조회, 손상·부분 기록을 가진 증거는 사용할 수 없다. 같은 부팅의 오래된 available 기록만으로 재연결을 허용하지 않는다.
- 동일 관측 요청의 재시도는 중복 캡처·delta·Git 등록·provider를 만들지 않는다. 정상 status 반복 조회가 매번 승격을 요구하지 않도록 연결 세대 내 재사용과 무효화 조건을 계약/fake로 고정한다. 파일 I/O마다 CIM 조회를 추가하지 않는다.
- 기존 provider를 실행 중인 채로 다른 읽기 경로에 전환하지 않는다. 현재 소유 프로세스·연결 세대를 확인하고, 재연결할 때만 검증한 접근 정보를 전달한다. 경로 소실/관측 실패 때는 보존 상태로 전환한다.

### 3. 판정과 기존 기록의 호환성을 고정한다

| 관측 결과 | 제품 행동 |
| --- | --- |
| 정확한 identity 일치 + 읽기 가능 | 같은 key/snapshot/delta로 실제 재개. 검증된 현재 경로가 달라도 기존 불변 기록은 유지 |
| 조회 성공 + 정확한 ID의 부재 확인 | missing. 새 캡처로 대체하지 않으며 결과·저널을 보존 |
| 권한/timeout/offline/boot 조회 실패 | unknown. 누락 값을 false/0으로 채우지 않고 관측 복구 행동 반환 |
| 같은 경로의 다른 ID, 다른 provider/volume/owner | identity mismatch. 읽기·재연결·파괴적 회수 차단 |
| legacy 영수증의 필드 부족 | 원문을 유지하고 원래 ID의 새 조회와 기존 소유권·캡처 정보로 입증되는 항목만 별도 재검증 기록에 남김. 현재 원본 경로에서 추정하거나 캡처 당시 기록으로 위장하지 않음. 입증 불가하면 preserve |
| 마지막 회수의 부재 | 기존 독립 결과/ref/tree/seal 및 소유 자원 회수 조건까지 충족해야 released. available/unknown/mismatch에서는 성공을 발행하지 않음 |

기존 `SnapshotObservationUnknown`, `SnapshotIdentityMismatch`, `SnapshotMissing`, `SnapshotPrivilegeRequired`와 recovery/nextActions를 우선 재사용한다. 추가 오류·필드가 필요하면 입력/출력 schema·capabilities·fake·CLI 적합성 시험과 함께 정의한다. 새 사용자 명령이나 별도 repair CLI는 이번 목표에 넣지 않는다.

## 실행 순서와 산출물

| 순서 | 구현·검증 작업 | 다음 단계 진입 조건 |
| --- | --- | --- |
| 1 | Core/Windows 책임과 영수증 버전·legacy 처리·관측 수명을 먼저 고정하고 fake/schema 시험 작성 | 일치·불일치·unknown·missing·stale의 응답과 원본/저널 보존 판정이 자동 시험으로 구분됨 |
| 2 | VSS helper/provider에 캡처·live 조회 대조와 비관리자 재시도 연결 | 반환 JSON이 실제 조회 객체에 근거하며 Observe로 캡처/삭제/설정 변경이 발생하지 않음 |
| 3 | NTFS backend, VssSnapshotTree, WinFsp 요청/실행 세대에 현재 접근 정보 연결 | 경로 변경 fake 재개, 기존 delta/결과 identity 유지, 오래된 요청·중복 호출 차단 |
| 4 | `pwsh ./scripts/build.ps1` 후 같은 후보의 실제 VSS/WinFsp/Git CLI 재개·회수와 단일 Unity 회귀 | 아래 LV 행렬의 필수 자동/실환경 증거, 독립 결과와 최종 자원 회수 확보 |
| 5 | schema·현재 계약·NR 기록·추적표·문서 지도/AGENTS·변경 이력 동기화 | 실행/미실행 경계를 명시하고 후속 중단·재부팅 시험으로 인계 가능한 후보 확보 |

우선 조사할 파일은 위 코드 표와 `src/Splinter.Core/SnapshotOverlay.cs`, `src/Splinter.Core/SnapshotWorkspaces.cs`, `src/Splinter.Runtime/SnapshotWorkspaces.cs`, `src/Splinter.Platform.Windows/NtfsSnapshotWorkspaceBackend.cs`, `WinFspOverlayClient.cs`·`WinFspOverlayServer.cs`·`WinFspOverlayMountProvider.cs`, `contracts/v1`, 기존 복구/CLI/snapshot 시험이다. 모든 파일의 수정이나 새 모듈 추가를 요구하는 목록은 아니다.

## 이 단위의 완료 행렬

LV는 NR의 하위 시험 식별자다. 아래에 정한 수준의 시험은 **모두 Passed**이며 [실행 기록](../verification/ntfs-live-vss-identity.md)에 새 후보의 180개 자동 통과·실제 VSS/Git 재연결·단일 Unity 검증 3건·최종 회수를 연결했다. fake 경로 변경을 실제 OS 경로 재배정/재부팅으로 집계하지 않는다.

| ID / 상위 NR | 필수 사례 | 필요한 증거 |
| --- | --- | --- |
| LV-01 / NR-01 | 캡처 identity와 실제 조회 identity 구분, 일치 시 재개 | fake·직렬화/schema·Windows 어댑터 시험 및 실제 helper 대조 |
| LV-02 / NR-01 | 같은 경로/다른 ID, 다른 provider/volume/owner, 상대 경로 탈출 | 자동 거부 및 영수증·delta·원본 불변 |
| LV-03 / NR-01 | 같은 ID/새 읽기 경로, 기존 delta의 부분 쓰기 유지 | fake/어댑터 입력 시험으로 실제 읽기 재개·hash/sequence·identity 불변. 이를 실제 OS 경로 재배정/재부팅 증거로 보고하지 않음 |
| LV-04 / NR-01 | 이전 boot·stale 세대·조회 실패·offline·없는 ID·손상 관측 | fake에서 unknown/missing/mismatch 구분, 관측 실패 뒤 오래된 성공 재사용 금지 |
| LV-05 / NR-01 | legacy 재검증 성공과 입증 불가 보존, 같은 key 동시 재개, 관측 기록 저장 중단 | 원문 보존·중복 자원 없음·재시도 행동·부분 기록을 성공으로 읽지 않음 |
| LV-06 / NR-01 | 같은 부팅의 실제 제품 workspace 생성→부분 쓰기/flush→소유 앱·provider 종료→같은 key 재개 | 비관리자 CLI + 정확한 helper, 같은 snapshot/delta, 이전 기준 바이트·승인 쓰기 hash/sequence 유지. 원본 변경 후에도 기존 시점 유지 |
| LV-07 / NR-07 | 실제 독립 checkpoint→release와 기존 마지막 VSS 해제 관측 회귀 | result ref/tree/manifest/seal, 소유 프로세스/핸들/메모리 0, mount/Git 등록/delta/소유 VSS 부재, 기존 snapshot·원본 보존 |
| LV-08 / NR-08 | 같은 후보의 단일 Unity 작업에서 기본/변경 코드 검증·재연결 후 loaded-code·결과·회수 | 정확한 6000.3.22f1, probe 값·PID/generation·현재 fingerprint/validation, 원본 Editor/MCP 보존. 재부팅·A/B 회귀와 구분 |

실제 LV-06은 작고 소유권이 명확한 Git fixture에서 먼저 수행한다. 최초 payload·읽기 hydration 0과 4KiB 부분 쓰기의 범위 저장도 회귀 확인한다. 관측 실패 사례의 Windows API 결과는 주입 시험과 실제 장애를 구분한다. 경로 재배정·offline·재부팅을 이번 실환경 시험에서 강제로 만들지 않는다.

실환경 최소 관문은 접근 가능한 검증 환경 한 곳의 VSS/WinFsp/Git와 단일 Unity다. VIVOBOOK을 사용하면 기존 ARM Windows + x64 Unity 조합·fixture·설정·증거를 재사용하고 준비·결과 교환은 에이전트가 담당한다. 사용하지 않은 아키텍처는 새 후보에서 NotRun으로 남긴다. 계획 수립 이후 실제 실행에서는 로컬 Windows x64와 기존 설치를 사용했다. 새 후보의 ARM Windows 시험은 NotRun이다. 제품 브리지의 기존 MVID 보고를 활용하며 fixture 자체 계측을 확장하지 않는다. 재부팅 후 loaded-code 검증은 NR-08에 남긴다.

새 증거는 `artifacts/ntfs-recovery/<runId>/`에 source manifest·환경·명령/응답·identity/관측·hash·결과·자원 회수를 저장한다. workspace home은 짧게 유지한다. 실제 실행할 때 현재 자원·공간·프로세스 소유권을 다시 관측하며 이전 보고의 자원 0을 현재 상태로 간주하지 않는다. 후속 Splinter 검증은 독립 Editor를 경로·PID·시작 시각으로 식별하며 제거한 Unity Agent Coordination을 다시 설치하거나 사용하지 않는다.

**완료 조건:** LV-01~08에 정의한 수준의 자동/실환경 증거가 같은 후보에 연결되고, 문서와 실제 응답이 일치하며, 독립 결과와 원본을 보존한 채 소유 자원 회수가 확인돼야 한다. fake만 통과하거나 실제 재개 대신 preserve만 반환하면 이 구현 단위는 미완료다. 실행 환경이 없으면 해당 사례를 NotRun과 다음 조건으로 남긴다.

## 다음 작업으로 남기는 범위

이번에는 live 식별·재연결에 직접 관계된 기록 경계만 검증한다. VSS capture 반환 ID 유실, Git/index/Ready, 결과 commit/ref/manifest/seal, 전체 회수 단계의 모든 중단 위치는 NR-02/03/07에 남긴다. 파일 특성·ACL·긴 경로 표의 나머지는 NR-05에 남긴다.

그 뒤 **나머지 중단/파일 경계 → 격리 환경의 정상 재부팅·강제 전원 종료·물리 공간 고갈/offline(NR-04/06) → ARM A/B 및 실제 재부팅 후 Unity를 포함한 NR-08 최종 후보** 순서로 진행한다. 대상 장비·볼륨·복귀 절차·외부 증거 보존과 기존 승인 범위를 확인한 실행 계획을 먼저 준비한다. 이번 범위 확정이 재부팅·전원 제어·새 시험 볼륨 설정을 실행한 것이나 추가 승인이라는 뜻은 아니다.

NR 이후에는 자체 제작 에셋의 저작/Play/테스트, Git dirty/LFS 입력·통합 후보 검증, 대표 프로젝트 성능, 공개 운영을 순서대로 확장한다. 구매 에셋·Unity/패키지 업그레이드·daemon/DB 교체·저널 compaction·다른 OS·게시/릴리스는 이번 단위에 포함하지 않는다.
