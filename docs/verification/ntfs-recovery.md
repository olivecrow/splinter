# NTFS 복구·지원 경계 검증

기준일: 2026-09-22. [NR 계획](../plans/ntfs-recovery-and-support.md)의 실행 기록이다. **NR-01/03/05/07 부분 구현·자동 검증, 원격 VSS 읽기·회수 복구와 5개 파일 API 거부, ARM Windows/x64 Unity 단일 작업공간 회귀·회수 통과**이며 NR 전체 완료가 아니다.

## 새 live VSS 후보

이후의 수집 경쟁 조건·status 비용·공통 mount 접근 수정은 [별도 검증 기록](README.md)을 따른다. 아래 LV·원격 Unity 수치를 그 수정 후보의 실환경 통과로 승계하지 않는다.

[LV-01~08 기록](ntfs-live-vss-identity.md)은 캡처/live identity와 현재 읽기 위치 분리, 새 연결 세대의 비관리자 Observe/재개을 검증한다. 새 후보는 자동 180 통과·0 실패·선택적 ReFS 1 skip, 실제 VSS/Git 재연결·4KiB 범위 쓰기·원본 시점 격리, Windows x64 Unity 검증 3건·재연결 후 loaded-code·독립 결과·최종 회수를 통과했다. 77개 원본 파일과 기존 snapshot 3개를 보존했고 소유 자원은 0이다. 다음 절의 139개 통과와 원격 ARM 시험은 이전 후보 기록이며 새 후보의 ARM·실제 재부팅·전원·공간 장애는 NotRun이다.

## 로컬 코드와 자동 시험

`development`의 기존 NI 후보 위에 아래 계약을 연결했다.

| 경계 | 확인한 내용 | 상태 |
| --- | --- | --- |
| snapshot 관측 | `available`/`missing`/`unknown`, unknown boolean 생략, 경로 파일 바꿔치기는 identity 불일치로 보존 | 자동 Passed |
| 복구 응답 | ready/observe/preserve/resume-ensure/resume-release/released와 안정된 이유·다음 명령, CLI/schema 일치 | 자동 Passed |
| 같은 identity | 일반 관측 불가 동안 ensure/회수 준비 거부, snapshot 재생성·delta 회수 없음, 관측 복구 뒤 같은 기준 재개 | fake Passed |
| 상태 읽기 | 불완전 JSON·고아 임시 파일·잠긴 JSON을 신규 작업으로 취급하지 않고 파일 보존 | 실제 파일 I/O Passed |
| 상태 쓰기 | 잠긴 저장 대상의 `StateWriteIncomplete`, 이전 기록·임시 파일 보존 | 실제 파일 I/O Passed |
| provider 기록 | 관측 불가도 같은 probe로 `snapshot-unknown` 기록, phase/availability 모순을 schema로 거부 | 자동 Passed |
| 저널 손상 | checksum이 일치해도 잘못된 JSON·누락 entry·경로 이탈·범위 오류는 `DeltaCorrupt`, 손상 identity와 저널 원문 보존 | 실제 저널 파일 I/O Passed |
| 저널 프로세스 종료 | 별도 자식의 64KiB 쓰기 완료를 외부 ACK 파일에 flush한 뒤 실제 강제 종료, 1/8/32 ACK 재연결 및 두 번째 재생 hash 일치 | 실제 프로세스 / fake 불변 트리 3건 Passed |
| 작업 중 파일 속성 | reparse/EFS/압축/희소의 create/overwrite/속성 callback 거부, 기존 바이트·속성·sequence·저널 hash 불변. 일반 속성과 변경 없음 sentinel 재연결 | callback/실제 저널 Passed |
| 마지막 회수 관측 | 같은 부팅의 정확한 해제 관측만 unknown 해소, 다른 부팅·identity·조회 실패는 보존, 최종 회수 재개도 unknown이면 완료 차단 | 영수증/fake Passed |
| 전체 회귀 | locked restore → Release build → 자동 시험, 총 140개 중 139 통과·0 실패·선택적 ReFS 1 skip. 경고/오류 0 | Passed |

이전 NR-07 후보 증거는 `artifacts/ntfs-recovery/nr07-release-observation-20260921/`의 `final-build.trx`, `source-manifest.json`에 있다. 앞선 128개 시험 후보는 `nr01305-20260921/`, 112개 후보는 `nr01-20260921/`에 따로 보존한다. `RecoveryContractTests`, `SnapshotWorkspaceCoordinatorTests`, `VssReleaseObservationTests`, `ContractAndCliTests`, `SnapshotOverlayTests`, `OverlayFileAttributeTests`가 추가 계약을 검증한다. 기존 journal/Git/Windows 프로세스 시험도 같은 후보에서 실행했다. 파일 잠금에 의한 저장 실패는 실제 볼륨 고갈의 증거가 아니며 callback 직접 호출은 실제 mount의 파일 API 시험이 아니다.

추가 프로세스 시험은 `artifacts/ntfs-recovery/nr03-process-20260921/`의 개발용 harness와 `trials-2/result.json`에 보존한다. 제품의 동일 `JournalDeltaStore` 바이너리를 사용하되 기준 트리는 결정적 fake다. 최종 ACK sequence 1/8/32가 모두 유지됐고, 재생된 sequence는 1/9/33이었다. ACK를 받기 전 완료된 마지막 레코드가 남을 수 있다는 허용 범위와 일치한다. 초기 payload 복사와 읽기 cache는 0이다. 첫 실행은 종료 직후 공유 위반 32로 중단되어 `trials/`와 실패 설명을 보존했다. 후속 harness는 소유 프로세스 종료 확인 뒤 공유 위반만 최대 5초 재관측하며, 통과한 3건에서 해당 재시도는 0회였다. 이 시험은 WinFsp/VSS·mmap flush·저널 내부 특정 명령어 중단·재부팅/전원 손실의 증거가 아니다.

현재 snapshot probe는 root 메타데이터와 source 볼륨 접근을 관측한다. 재부팅 후 VSS ID·provider·volume의 동일성을 재검증한 증거는 아직 없다. 모든 부작용 경계, 정상 재부팅/강제 전원 종료, 실제 공간 고갈/offline, ACL/EFS·입력 특성의 전체 표와 부분 준비 회수는 NotRun이다. 아래 실제 mount의 5개 API 거부와 VIVOBOOK 단일 Unity 작업공간 시험을 이 전체 범위와 구분한다.

## VIVOBOOK 준비

사용자가 지정한 원격 작업의 빈 `splinter-test-vivobook`을 사용한다. 원격 하위 `splinter-unity`에 Git bundle로 fixture를 복사했다. 원격이 기대 revision `46e7f4dbdb219e4afa602f3598a3de9ff4070fae`, bundle SHA-256, 추적 파일 77개, clean `development` 일치를 각각 확인했다. 원본의 `Assets`/`Packages`/`ProjectSettings`/`.meta`와 Git 이력을 보존하며 `Library`/`Temp`/`Logs`는 복제하지 않았다. 이는 시험 환경 최초 준비이며 제품 workspace 복제량 계측에 합산하지 않는다.

사용자가 제공한 환경은 ARM Windows 11, RAM 32GB와 용량 342GB다. 이 용량 표현을 실제 여유 공간으로 대신하지 않고 원격에서 측정했다. 12:34 UTC의 시스템 NTFS 볼륨 여유는 336.618309GiB다. 프로젝트 내부 `.tools/dotnet`에 .NET SDK 10.0.112 ARM64 ZIP을 공식 SHA-512와 대조해 준비했다. **시험 Editor는 기존 x64 Unity 6000.3.22f1**이다. 사용자가 선택한 기존 설치를 유지하며 ARM64 Editor를 추가하지 않는다. 이 fixture의 canonical x64 Editor 첫 임포트는 아래 준비 절에 기록하며 제품 Unity workspace 검증과 구분한다.

원격 Codex의 최근 작업 본문이 이쪽 조회에서 비어 있어 지정 파일만 제공하는 한시적 로컬 전송 서버를 사용했다. 결과는 최소 bool/오류 코드 JSON으로 직접 수신했다. 계정명·전체 경로가 포함된 보고서는 전송하지 않으며 사용자에게 중계나 수동 복사를 요구하지 않는다. 증거는 `artifacts/remote-vivobook/transfer/vivobook-report-*.json`에 보관한다. 이 전송 서버는 제품 기능이 아니다.

사용자가 승인한 현재 제품 소스 162개와 파일별 manifest를 원격에 전달했다. ZIP SHA-256은 `3d3efb9239d55a18a8a622ba76af1335efc2c4252eaa92915f51dbb83a74005c`이고 시험 대상 소스 aggregate는 `8422f019575a3a2bb48a239f9220f2e3ba51bb34079d9982563dc8f5b0e8ad55`다. ARM64 .NET의 locked restore·Release build·자동 시험은 **총 128개, 127 통과·0 실패·선택적 ReFS 1 skip**이다. 전송 hash, 소스 불변 및 최종 TRX hash를 확인했다. `WindowsProcessTests`는 PATH로 시작한 PowerShell의 `$PSHOME`을 atomic 파일로 직접 받아 실행 파일을 확인하므로 설치 경로를 고정하거나 외부 `Process.MainModule` 조회에 의존하지 않는다.

원격 작업의 직접 사용자 승인 후 **WinFsp `2.2.26215 Beta4` Core·사용자 런타임 설치가 완료**됐다. MSI exit code 0, 재부팅 불필요, ARM64 파일과 서명 검증, launcher 실행을 확인했다. 아래 별도 시험에서 실제 커널 mount와 파일 I/O도 확인했다. 설치 성공과 실행 시험은 별도 증거이며 기존 x64 Unity 파일과 패키지 선언은 유지됐다.

12:34 UTC의 같은 시스템 볼륨 VSS diff-area는 상한 19.027344GiB, 사용 16.620625GiB, 할당 16.971958GiB, 상한 여유 2.406719GiB로 capture의 8GiB 여유 조건에 부족했다. 사용자가 승인한 증가 전용 계획은 `artifacts/remote-vivobook/vivobook-vss-budget-plan.json`에 있다. 부모의 전달 승인을 거부한 뒤 원격의 직접 사용자 입력으로 승인 경계를 통과했으며, **13:20 UTC 보고에서 상한 32GiB 적용과 기존 snapshot 4개 보존을 확인**했다. 상한 축소·기존 snapshot 삭제·자동 재부팅·물리 공간 고갈을 하지 않았다.

원격 fixture에는 coordination `0.3.0`과 Coplay `10.2.0` 선언/lock이 일치한다. 기존 Unity MCP provider 설정과 Python/uv 준비를 재사용했고 canonical x64 Editor의 첫 임포트·HTTP 연결·coordination 준비를 완료했다. 대화형 사용자의 Git 소유권 검사는 정확한 fixture에 한정한 프로세스 설정으로 읽기 조회에 성공했다. 전역 Git 안전 경로 완화나 패키지 업그레이드를 하지 않았다. **제품 Unity 단일 정상 회귀는 아래 기록에서 통과했으며, 원격 PC를 쓰는 A/B·재부팅·장애 복구 및 나머지 파일 지원 경계 시험은 아직 남아 있다.**

사용량 제한 해제 후 13:05 UTC에 같은 원격 작업을 재개했다. 13:07 UTC 무렵 명시 host 조회가 일시 실패했지만 host 자동 탐색으로 같은 실행 중 작업과 새 보고를 확인했다. PC 전원이나 원격 실행 중단의 증거는 아니다.

실제 제품 workspace의 첫 시도는 Git preflight에서 `GitCommandFailed`로 중단됐다. 제품 Git 어댑터가 외부 `GIT_*` 변수를 제거하므로 단독 조회에 쓰인 프로세스 범위 trust 설정이 상속되지 않았다. 13:20 UTC 보고에서 원본 index 불변과 소유 snapshot/mount 0을 확인했다. 이후 **동일 fixture에서 제품 Git 방어와 사용자 전역 Git 설정 변경 없이 비관리자 생성에 성공**했다. 새 fixture나 중복 smoke는 만들지 않았다. 첫 실패를 VSS·mount 실패로 집계하지 않는다.

## VIVOBOOK 제품 workspace 읽기와 회수

일반 제품 CLI의 `workspace ensure --storage ntfs-snapshot`으로 실제 VSS와 WinFsp를 연결했다. `vivobook-report-1789997092011864500.json`은 비관리자 runtime의 ready, capture/mount 성공, 최초 payload 복사와 read cache 0을 기록한다. 이 단계는 앞 절의 설치 또는 아래 fake 트리 mount 시험과 별개다.

`vivobook-report-1789997324666452000.json`에서 추적 파일 **77개 전체의 바이트 일치**, 읽기 후 payload 불변·read cache 0, 원본 index 불변을 확인했다. source를 편집하거나 Unity를 실행하지 않았다. 일반 release는 변경 없는 기준 commit을 독립 결과로 보존했으며 validation 성공을 만든 것이 아니다.

`vivobook-report-1789997930391652500.json`은 실제 회수 후 **소유 snapshot/mount/provider/delta/핸들/메모리 0**, 기존 snapshot 4개 보존, 원래 Git worktree 등록 복원과 독립 결과 보존을 기록한다. 중간 UAC 취소는 해결됐다. 다만 삭제된 장치 경로의 Win32 21을 정상 CLI가 `SnapshotObservationUnknown`으로 판단해 workspace는 `releasing`에 남았다. 이 시점의 물리 자원 회수와 제품 상태 전환 실패를 각각 보존했다. 아래 수정 후보의 재개 시험에서 최종 상태 전환도 완료했다.

`vivobook-report-1789998203275918300.json`에서 해제 성공 영수증의 snapshot/owner 일치와 helper의 부재 관측을 확인했다. 같은 비관리자 문맥의 현재 VSS 목록 조회는 HRESULT `0x80041014`로 실패했다. 수정 후보는 정확한 helper의 부재 관측을 현재 부팅 시각과 대조하고, 이전 영수증이면 읽기 전용 Observe helper로 갱신한다. 일반 unknown은 유지하며 최종 회수 단계에서만 이 관측 갱신 경로를 허용한다. fake·영수증 시험은 현재 부팅/다른 부팅/조회 실패/다른 owner·snapshot·provider/재사용된 경로와 최종 unknown 차단을 구분한다. 이 정책 시험은 실제 재부팅 시험이 아니다.

`vivobook-report-1789999527420230800.json`은 수정 후보의 **동일 workspace 회수 재개 통과** 기록이다. 전달 ZIP SHA-256은 `c8b20aa2e13c4cbad89049d5f78ab856c7e989c9c635c055daef03fedd28a250`, 시험 소스 aggregate는 `78d285261a1a5899f137c39e1193eb3dc0c6e00d1d4523870bdd709c7af7140e`다. 원격 ZIP/파일 hash 검증과 locked restore/build 이후 총 140개 중 139 통과·0 실패·ReFS 1 skip, 경고/오류 0을 확인했다. Observe helper는 원래 release 영수증을 변경하지 않았고 소유 snapshot·현재 부팅과 일치하는 부재 관측만 갱신했다. 이후 정상 비관리자 `workspace release/status`의 exit code가 모두 0이고 phase는 `released`, 이전 오류는 해소됐다. workspace JSON을 수동 수정하거나 새 workspace로 바꾸지 않았다.

최종 관측은 소유 snapshot/mount/provider/delta/process/핸들/working set/private bytes 0, 기존 snapshot 4개 보존, 원본 HEAD/index/77개 파일·원래 Git worktree 등록·독립 결과 ref/tree 보존이다. Unity는 이 시험에서 실행하지 않았다. 읽기 단계는 앞선 후보의 실제 snapshot을 사용했고, 수정 후보는 그 지속 상태를 이어 회수했다. 이 호환 재개 증거를 새 후보의 새 capture·Unity·재부팅 전체 시험으로 확대하지 않는다.

## VIVOBOOK x64 Unity 초기 준비

`vivobook-report-1790000221236042200.json`에서 기존 x64 Unity `6000.3.22f1`을 비관리자로 한 개만 실행하고 canonical fixture의 첫 임포트를 **179.423초**에 완료했다. 180초 준비 예산 안이며 로그의 컴파일 오류 행은 0이다. 추적 source/HEAD, package manifest/lock과 staged 내용은 유지됐다. Git index의 stat/cache 갱신으로 바이트 hash는 달라졌으므로 index 바이트 불변으로 기록하지 않는다.

첫 임포트의 Library는 논리 크기 **1,837,943,930바이트**, 파일 **32,041개**다. 이는 빈 원격 fixture의 최초 준비 비용이며 제품 workspace의 최초 payload 복사·읽기 hydration 계측이 아니다. 새 VSS나 새 provider, ARM64 Editor를 설치하지 않았다.

초기에는 저장된 HTTP endpoint가 듣고 있지 않아 MCP initialize가 `UnityMcpEndpointConnectionFailed`로 실패했다. 이후 `vivobook-report-1790000999923943400.json`에서 원격 native UI로 기존 Start Server 버튼을 눌렀고 실제 HTTP 연결과 첫 coordination status가 성공했다. 해당 관측의 agent/claim 수는 0이며 active editor-access lease는 없다. 사용자 버튼 조작이나 새 provider 설치를 요구하지 않았다.

`vivobook-report-1790001786937894400.json`의 Unity 준비 최종 결과는 성공·정리 성공, Console 오류 0·경고 1, 마지막 준비 확인 653ms다. 원격 작업은 경고를 연결 전 발생한 60초 대기 경고로 확인했고 `Session Active`도 관측했다. 초기 임포트 시간과 이 확인 시간을 합치거나 제품 workspace 성능으로 취급하지 않는다. 상세 instance/lease 자료는 원격에 보존하며 승인된 최소 결과만 수신했다. Play/Test와 제품 Unity workspace 코드 실행은 별도 시험이다.

## NR-05 실제 mount의 파일 API 거부

동일 제품 후보의 `JournalDeltaStore`/`WinFspOverlayFileSystem`을 사용하여 ARM64 프로세스의 실제 WinFsp mount에서 5개 API를 시험했다. 불변 기준은 결정적인 fake 트리이며 VSS와 Unity 원본은 사용하지 않았다. 로컬 계획은 `artifacts/ntfs-recovery/nr05-mount-api-20260921/plan.json`, 원시 harness·API별 결과는 원격 `artifacts/nr05-kernel/`에 있다.

| 요청 | 일반 NTFS 양성 대조 | 실제 mount |
| --- | --- | --- |
| `CreateFile` named data stream | 성공 | 거부 |
| `CreateHardLinkW` | 성공 | 거부 |
| `FSCTL_SET_SPARSE` | 성공 | 거부 |
| `FSCTL_SET_COMPRESSION` | 성공 | 거부 |
| `FSCTL_SET_REPARSE_POINT` mount-point buffer | 성공 | 거부 |

최종 최소 보고 `vivobook-report-1790001786937894400.json`은 **5 통과·0 실패·0 NotRun, 327ms, 정리 성공**이다. 원격 작업은 거부 전후 파일 내용·메타데이터·저널 보존, 일반 쓰기·rename·delete 정상 동작과 mount·프로세스·핸들 회수를 확인했다. 최초 107ms 실행은 harness의 저널 읽기 공유 설정 오류로 API 시험 전에 중단됐고 5개 모두 NotRun이었다. 첫 실패와 자료는 보존했으며 harness만 수정한 재실행 결과를 통과로 기록했다.

이 증거는 지원하지 않는 API의 거부 경계다. 원본 preflight·VSS 입력 특성·EFS·ACL·긴 경로·모든 reparse 종류 또는 일반 NTFS 특성 지원으로 확대하지 않는다. 상세 전송의 승인 범위를 넘지 않도록 개별 Win32 코드와 원시 경로/로그는 원격 자료에 남겼다.

## ARM Windows의 제품 Unity 단일 작업공간

기존 x64 Unity `6000.3.22f1`, 원격 fixture revision `46e7f4dbdb219e4afa602f3598a3de9ff4070fae`와 위 `78d28526…` 제품 후보로 일반 CLI의 새 `ntfs-snapshot` 작업을 시험한다. 계획과 최소 보고 연결은 `artifacts/ntfs-recovery/nr08-vivobook-unity-20260922/plan.json`에 있다. 원시 CLI·snapshot·process·Git·Unity 증거는 원격에 보존한다.

첫 캡처 요청은 시작 전에 취소됐다. `vivobook-report-1790002929162919600.json`에서 대기 0·정리 성공·Unity 시험 1 NotRun을 확인했다. 사용자에게 UAC 창이 보이지 않았다는 응답도 받았으므로 이전 대기 보고를 실제 창 표시 증거로 사용하지 않는다. 동일 작업의 재시도는 `vivobook-report-1790003097919600000.json`에서 **캡처 2,895ms·성공·대기 0**으로 확인했다.

단일 작업은 기본 probe 값 13 확인 → 반환된 workspace의 probe만 변경 → 새 generation의 값 20과 현재 fingerprint의 validation 확인 → 독립 checkpoint → 원본 통합 없이 release 순서로 요청했다. `vivobook-report-1790003606358959700.json`은 검증 2 통과 후 checkpointing 단계의 중간 보고다. 최종 workspace 보고 `vivobook-report-1790004051972464600.json`에서 **2 통과·0 실패·0 NotRun, 전체 성공, workspace_released, 남은 소유 작업공간 자원 0, 정리 성공**을 확인했다. 원시 실행 값·generation·fingerprint·결과 ref/manifest는 원격 증거에 남으며 부모에는 승인된 최소 결과만 전송했다.

`vivobook-report-1790004706952742900.json`은 시험과 canonical 준비 세션 정리를 모두 완료한 최종 보고다. 검증 2 통과·0 실패·0 NotRun, 소유 workspace 자원 0, workspace 정리 성공, canonical Editor/MCP 세션의 정상 종료 성공과 대기 0을 확인했다. 원본 Editor/MCP 보존을 workspace 회수 뒤 먼저 확인한 다음 이번 준비에서 직접 실행한 세션을 종료했다. 중간 정상 종료 지연을 최종 회수 실패로 남기지 않으며, 강제 전원 종료 시험으로 집계하지 않는다.

| 최종 시간 보고 | 결과 |
| --- | --- |
| 작업 Editor 최초 Ready | 127.085초 |
| 기본 상태 검증 | 23.878초 |
| 수정 상태 검증 | 25.329초 |

각 시간은 원격 단계별 보고이며 전체 작업 시간이나 시작 SLA가 아니다. canonical 최초 임포트 179.423초와도 구분한다. 원격은 독립 결과와 기존 snapshot 4개 보존을 확인했다. 부모의 한시적 전송 서버도 PID·시작 시각·실행 파일 대조 뒤 종료했고 `artifacts/remote-vivobook/transfer/server-final-cleanup.json`에 남겼다. **이번 원격 시험 세션은 끝났으며 실행 중인 시험을 위해 VIVOBOOK을 계속 켜 둘 필요는 없다.** 프로젝트·Library·결과·원시 증거와 승인된 설치·VSS 설정은 후속 시험용으로 보존한다.

이 시험은 단일 작업의 정상 스크립트 회귀다. ARM Windows의 A/B 동시 실행·재부팅 뒤 loaded-code·MVID·장애 복구, 일반 에셋·Play/Test 지원을 통과로 승계하지 않는다.

## ARM64 실제 WinFsp와 합성 입력

`vivobook-report-1789996442204143400.json`은 **실제 WinFsp mount + fake 불변 트리** 시험 결과다. 메모리에 만든 결정적인 입력과 제품 `JournalDeltaStore`/`WinFspOverlayFileSystem`을 조합한 별도 개발용 harness를 비관리자로 실행했다. Unity fixture와 VSS는 사용하지 않았으며 제품 snapshot workspace 생성의 대체 경로가 아니다.

| 관측 | 결과 |
| --- | --- |
| 실행 | ARM64 .NET, 비관리자, 1,370ms, 통과 |
| 합성 기준 | 32,768바이트 전체 읽기 일치, 기준 트리 바이트 불변 |
| 저장 계약 | 최초 payload 복사 0, read cache 0, 읽기로 생긴 payload 0 |
| 부분 쓰기 | 4,096바이트 쓰기·flush·읽기 일치, payload 증가 4,096바이트 |
| 파일 작업 | 새 시험 파일 create·rename·delete 통과, 최종 payload 총 4,131바이트 |
| 오류와 회수 | callback 오류 0, unmount 후 열린 핸들/보유 context 0, 프로세스 종료, 실제 mount 부재, 소유 provider 메모리 0 |
| 보존 | 원래 제품 소스/Unity fixture 보존, 소유 snapshot 0, VSS quota 미변경 |

이는 ARM64 WinFsp 기본 파일 I/O의 증거다. VSS 시점 격리·제품 workspace/Git 수명주기·프로세스 중단 지속성·특수 파일/ACL·Unity 실행은 이 시험에서 검증하지 않았다. NR 항목 전체를 Passed로 올리지 않는다.

[WinFsp ARM64 안내](https://winfsp.dev/doc/WinFsp-on-ARM64/)는 ARM64 지원과 공통 설치 패키지, ARM64 OS에서 x64 사용자 프로세스의 사용을 설명한다. Editor 프로세스 아키텍처와 커널 드라이버 아키텍처를 구분한다. 이 머신의 조합은 ARM Windows 11 + x64 Unity로 기록하며 실행 증거 없이 검증 완료로 표시하지 않는다. .NET SDK는 [Microsoft 10.0 release metadata](https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/10.0/releases.json)의 10.0.112 ARM64 ZIP과 SHA-512를 기준으로 프로젝트 내부에 준비한다.
