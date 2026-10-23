# 반복 UAC 제거 검증

[English](../../verification/storage-privilege-service.md)

**등록된 Windows x64 프로젝트/home의 정상 반복 UAC 제거를 확인했다.** 2026-09-29에 제한 서비스 설치 후 일반 사용자 CLI로 실제 NTFS/Git A/B 생성·같은 snapshot 재연결·독립 결과 보존·회수를 실행했다. UA-01~04의 아래 범위는 Passed이며, 제품 전체 설치·일반 Unity·ARM·NR 장애 지원의 완료를 뜻하지 않는다.

## 판정과 실행 근거

원시 근거는 `artifacts/storage-authority/ua-20260928/`에 있다. [종합 결과](../../../artifacts/storage-authority/ua-20260928/qualification-20260929.json)는 각 workspace의 snapshot/mount/delta 부재와 소유 프로세스·메모리·핸들 0, 외부 snapshot 보존, CLI 권한과 서비스 상태를 묶는다.

| 단위 | 확인한 범위 | 실제 근거 |
| --- | --- | --- |
| UA-01 설치/수명 | 보호 경로의 LocalSystem 자동 시작 서비스 설치, 동일 패키지 업데이트에서 활성 A/B 보존, 활성 snapshot이 있으면 제거 거부 후 서비스 재시작 | `bootstrap-result-02.json`, `management-update-active-01-02.json`, `management-remove-active-01-02.json`, `status-final.json` |
| UA-02 요청 경계 | SCM의 고정 경로/계정/PID와 pipe peer 일치, 버전·임의 operation·알 수 없는 필드·미등록 home·타 작업 snapshot·조기 회수 거부, 일반 사용자에게 실행 파일/상태 쓰기와 같은 pipe 서버 생성 거부 | `pipe-boundaries.json` 6종, `installed-protection.json`; 디렉터리 교체·다른 SID·JSON 경계는 아래 자동 시험 |
| UA-03 반복 사용 | 일반 권한 A/B 생성, A provider 정상 정지 후 같은 key/ID/snapshot 재연결, 별도 편집 결과 보존, B 먼저 회수 시 A를 차단자로 유지, A 회수 뒤 B 명시 재시도 성공 | `ensure-a-02.json`, `ensure-b-01.json`, `reconnect-a-01.json`, `preserve-*-01.json`, `release-b-first.json`, `release-a-01.json`, `release-b-retry.json` |
| UA-04 중단/복구 | 서비스 정지 시 `StorageServiceUnavailable`을 반환하고 자동 elevation 없이 같은 작업 재개, Observe 응답 본문을 읽지 않고 연결 종료한 뒤 서비스 재시작·동일 요청 재전송, 잘못된 버전 거부 | `reconnect-service-stopped.json`, `lost-observe-drop.json`, `management-restart-01-02.json`, `lost-observe-replay-02.json`, `response-replay-proof.json` |

일반 CLI 실행 **13회 모두 `callerElevated=false`**였다. 실행 중 50ms 간격의 `consent.exe` 표본은 0건이며, 정상 provider 코드에는 자동 elevation/helper fallback이 없다. 최초 설치와 승인된 서비스 관리 시험은 별도 관리자 세션에서 수행했다. 그 세션은 `management-ended-02.json`의 `finish`로 종료됐고 최종 서비스는 일반 CLI에서 `running`으로 발견된다. 정상 VSS 요청은 이 시험 세션이 아니라 설치된 SYSTEM 서비스가 처리했다.

응답 유실 시험의 재전송 SHA-256 `0999868d784ec1500393dc060754728d6be9287f51854378585ebfb40fbbec8d`는 재시작 전 보호된 완료 응답 파일과 같다. 서비스 PID가 바뀌어도 기존 응답을 재사용했다. 첫 probe 재전송은 PowerShell의 자동 날짜 변환으로 입력 offset이 달라져 `StorageRequestConflict`가 났다. 원래 문자열을 보존한 동일 입력에서 통과했으며 잘못된 첫 요청 근거도 남겼다.

## 결과와 보존

| 작업 | workspace ID | 독립 Git 결과 | 최종 상태 |
| --- | --- | --- | --- |
| A | `ade04e0b2900b1ca585a91b4` | `754f024901f6b283749ee84fb7ab638e5908ada8` | released; 소유 snapshot/mount/delta 및 프로세스·메모리·핸들 0 |
| B | `21b16811c316fdfe5008655a` | `c0813e817db67b4851502c2186c0934d82a5f72a` | reclaim-pending 뒤 명시 재시도로 released; 같은 자원 0 |

회수 뒤 `refs/splinter/snapshot-results/<id>`에서 `independent-result-a`와 `independent-result-b`를 실제 읽었다. 원본 HEAD `4a8c6b3150a2c8ef4d18567ad47e1649d74c378a`와 깨끗한 checkout은 유지됐고 Git worktree 등록은 원본 하나로 돌아왔다. 기존 C: snapshot 두 ID `{78AD7813-DF73-4845-8513-F14CC6E87331}`, `{15182498-4A5B-4E95-B85F-FBE2EC57AE15}`는 전후 모두 존재한다.

Unity Editor를 시작하지 않았으므로 `workspace checkpoint`의 `ValidationRequired`는 유지했다. 편집 결과는 기존 제품 `validation preserve-worker`로 보존했고 `validationClaimed=false`다. Unity 컴파일 성공을 만들어 회수 조건을 우회하지 않았다.

## 재현 범위와 남은 한계

- 설치 패키지는 `package-03`, manifest SHA-256은 `98195261bdd01cfcb6fe940a52f09301bbbfba62f15078c4b51d5515d646fff8`이다. installer SHA-256은 `806e3de182a9b84b8c884fdc1d6da21e04d4ae83e53642bd5f4a3dc32c1433c3`다. 기준은 `development @ e75b4a5` 이후 미커밋 후보이며 실행 중 제품 코드는 바꾸지 않았다.
- 새 16GiB 동적 VHDX `ua-isolated.vhdx`만 정확한 새 RAW disk identity 확인 후 T:로 포맷했다. 그 볼륨에만 seed snapshot을 생성해 diff area를 10GiB로 늘리고 해당 seed ID를 회수했다. 기존 C: 설정과 다른 snapshot은 변경하지 않았다. fixture와 독립 Git 결과를 보존하기 위해 T:와 VHDX는 유지한다. 이것은 workspace mount/delta 회수와 별개다.
- 설치 grant는 `T:\ua-source` + 이 근거 디렉터리의 `home`, 형제 `splinter-unity` + `unity-home`의 정확한 identity다. 실제 A/B는 첫 grant에서만 실행했다. 형제 저장소에는 코드·Editor 작업을 하지 않았다. 새 프로젝트/home 등록·서비스 업데이트/제거는 관리자 구성 작업이며 일반 반복 workspace 명령에 포함하지 않는다.
- 시험 source 디렉터리는 관리자 설치가 생성해 Git의 소유권 거부가 발생했다. 별도 `git-test-settings/git/config`의 정확한 `safe.directory=T:/ua-source`를 시험 프로세스의 `XDG_CONFIG_HOME`으로만 적용했다. 사용자 전역 Git 설정과 제품의 GIT 환경 격리 코드는 변경하지 않았다.
- 실제 서비스 정지/재시작과 완료 응답 유실은 위 범위로 실행했다. native VSS capture 도중 강제 종료는 실제 실행하지 않았고 해당 중단/불명확 capture 보존은 자동 시험 근거다. 재부팅·전원·물리 공간 장애, 일반 파일·Unity·ARM A/B는 기존 NR/WF 지원 경계를 유지한다.
- 앞선 최초 UAC 요청은 취소됐고 사용자는 직접 연결 모니터의 검은 화면을 보고했다. 사용자 복귀와 설치/시험 명시 승인 후의 재시도는 관리자 프로세스·설치 결과로 성공을 확인했다. UAC/secure desktop·그래픽 설정은 변경하지 않았으며 앞선 표시 문제의 근본 원인은 미확정이다.

## 자동 검증

`pwsh ./scripts/build.ps1`의 locked restore·Release 빌드와 **568 통과·0 실패·1 시험 생략**이 현재 제품 후보의 근거다. `StorageAuthorityTests`·`StorageAuthorityPipeTests` 31개와 `StorageServiceSetupTests` 9개를 포함한다. fake capture 중단/응답 유실/동일 요청 복구, 완전한 pending 게시 재개·손상 보존, 실제 Windows 파일 교체 차단·일반 파일 쓰기·임시 pipe SID/ACL, 읽기 전용 설치 입력 거부를 검증했다.

최종 빌드 로그는 `build-setup-preflight.log`, 전체 결과는 `artifacts/test-results/Splinter.Tests_net10.0_x64.trx`다. 앞선 `build-final.log`, `authority-final.trx`, `setup-preflight.trx`도 보존한다. 기존 파일 교체 경합 시험은 중간에 한 번 30초 제한에 걸렸으나 시험/기한을 바꾸지 않은 별도 재실행과 최종 전체 실행에서 통과했다. 원인이 해결됐다고 단정하지 않는다.

현재 서비스 계약은 [기능 문서](../storage-privilege-service.md), 완료 범위는 [UA 계획](../plans/storage-privilege-service.md)을 따른다.

<a id="ua-05-idle-path-handles"></a>
## UA-05: 대기 중 경로 핸들 수명, 2026-10-23

범위: 설치·상태·PowerShell 조상 핸들을 서비스 전체 수명에서 시작/요청 범위로 줄이고, 기존 쓰기/삭제 공유 차단·reparse/타입 검사·요청자 토큰의 프로젝트/home 권한 검증을 유지한다. 모든 조상의 identity와 설정값을 보관하고 요청마다 핸들을 다시 확보해 identity·설정·보호 디렉터리 ACL을 재검증한 뒤 응답 전에 모든 경로/증거 핸들을 닫는다. 시작 실패·admission 거부·연산 오류·취소와 한정된 서비스 오류 기록에서도 해제한다. 프로토콜·grant·보호 영수증 형식·snapshot 회수 조건은 바꾸지 않았다.

`StorageAuthorityLifetimeTests` 11개 사례는 실제 파이프 서버와 Windows 핸들, 독립 디렉터리 fixture, 합성 보호 ACL 판정과 fake VSS 부수 효과를 사용한다. 대기/시작·미완성 입력·성공 응답/재전송·요청 거부·실패/취소 연산·서비스 취소를 다룬다. 요청 중 조상 쓰기·디렉터리 이름 변경·junction 전환은 계속 차단한다. 요청 사이에는 fixture 조상을 CBS와 같은 접근 `0x12019F`, 공유 7, `OPEN_EXISTING`으로 열며 파일을 변경하지 않는다. leaf identity가 같은 대기 중 조상 교체, 디렉터리/PowerShell/설정 파일 교체, 설정 내용 변경, ACL 변경과 reparse 대체는 거부한다. 독립 조상은 다른 프로세스의 C: 핸들이 회귀를 가리는 일을 피하기 위한 것이며 실제 C: 근거는 아래와 구분한다.

설치된 구형 서비스의 정확한 LocalSystem PID **4968**을 승인된 정상 중지로 종료하자 C: 디렉터리 접근은 **32(`ERROR_SHARING_VIOLATION`)에서 0(성공)**으로 바뀌었다. 확인 시 자식 프로세스와 미완료 `active.json` 요청은 없었다. 보호 ledger 파일 **1,721개**의 경로/내용 hash는 그대로였으며 snapshot/자원 삭제·Windows Update 설치·DISM/SFC·재부팅은 실행하지 않았다. 이 서비스 중지로 관측된 루트 접근 충돌이 해소됨을 입증하며 Windows Update나 구성요소 복구 성공을 뜻하지 않는다. 근거는 `artifacts/storage-authority-idle-20261023/stop-result.json`과 고정된 중지 스크립트다.

최종 `pwsh ./scripts/build.ps1`의 locked restore·Release 빌드와 **1,702 통과·0 실패·24 생략(총 1,726개)**을 확인했으며 새 사례 11개가 모두 포함된다. 근거는 동일 디렉터리의 `required-build-final.log`와 `authority-lifetime-final.trx`다. 최초 실행에서 드러난 시험 자체의 JSON 예외 하위 타입·루프 정상 종료 기대를 수정한 뒤 전체 실행이 통과했다.

동일 근거 디렉터리의 `package/`에 새 서비스 패키지 16개 파일을 준비했다. manifest SHA-256은 `b7753ca6040a2c603867b73d636b3339b1fa2db095c1b7ddc7c35d4a27c7303e`다. 표준 Update 읽기 전용 사전검사는 T:가 연결되지 않아 보존 중인 grant를 거부했다(`update-plan.log`). T:는 UA 시험용 16GiB VHDX였으며 이후 기록된 다른 Splinter 시험에도 사용했다. `Get-DiskImage`로 `Attached=false`를 확인했다. grant 삭제/재작성·볼륨 재연결·새 설치 적용은 하지 않았다.

이후 사용자가 모든 점유 정리를 요청했다. 최초 UAC 취소 후 명시적으로 재승인받아 관리자 확인을 수행했으며 **소유 기록 102개 모두 released, 활성 요청 0, 미회수 소유 기록 0, 현재 소유 snapshot 0, Splinter 실행 프로세스·예약 작업 0**이었다. 표준 `setup-storage-service.ps1 -Action Remove -Apply`로 구형 서비스 등록을 제거했고 SCM은 **1060(서비스 없음)**을 반환했다. ledger 파일 1,721개와 기존 비소유 C: snapshot 3개는 그대로 보존됐다. 시험 프로세스 종료 뒤 **C:, D:, E:, Program Files, ProgramData, 서비스 설치/상태 디렉터리, PowerShell 7**의 native 디렉터리 접근이 모두 성공했다(8개 경로, 오류 0). 근거는 `cleanup-result.json`, `service-remove.log`, `cleanup-summary.json`이다.

당시 정리 시점의 archive 제외 `.local` 기록에는 **종료된 workspace 91개(released 88, discarded 3)**와 **restored 프로젝트/Library 기준본 38개**가 있었다. 확인한 두 원본 프로젝트와 각 Library는 reparse point 없는 일반 디렉터리였다. 결과·시험 VHDX·과거 영수증·실행되지 않는 설치 파일은 보존했으며 공용 WinFsp 설치는 제거하지 않았다. 위 조사 범위에서 남은 활성 Splinter 점유가 관측되지 않았다. 이후 별도 승인으로 아래 설치를 수행해 서비스 없음 상태가 바뀌었으며, 과거 UA-01~04 자격을 이번 후보의 통과로 승계하지 않는다.

### 실제 설치 서비스 후속, 2026-10-23

이후 사용자가 수정 서비스 설치·실제 서비스 검증·성공 후 커밋을 승인했다. UAC 취소 1회 후 재승인받아 정확한 기존 VHDX/NTFS 볼륨을 T:로 잠시 다시 연결했다. 표준 Install 사전검사/Apply는 기존 **grant 7개**와 과거 보호 소유 기록을 유지했으며 **설치 파일 16개 hash**가 위 패키지와 일치했다. 새 프로젝트/home 권한은 추가하지 않았다. 일반 CLI는 `callerElevated=false`, 서비스 PID **12548**은 LocalSystem/Automatic이었다.

작은 Editor 없는 작업 하나가 기존 `T:\plain-apply-20261002` / `.local\ap1002`, `source=none`, `application=none`, `ntfs-snapshot`, 16MiB 저널 예산, key `ua05-installed-idle-20261023`을 사용했다. workspace는 **`d6b31c017bd9062ef57223de`**, 요청 digest는 `e2df5a04116b61cb16937948`, 생성 snapshot은 **`{7CD684FE-7FF2-4C29-8388-B86C5DFB9A05}`**다. 반환 작업공간에만 기록한 58바이트 파일은 `checkpoint --release` 뒤 독립 결과 `sha256:98580480db6a631f67260abf5e187cd17256590203b83a3e2b9d110537caa265`로 보존됐다. 원본 3개 파일/29바이트의 hash는 그대로였다. 최종 작업은 `released`이며 mount/delta/provider/snapshot이 없고 소유 open handle은 0이다.

첫 checkpoint/release는 결과를 보존하고 provider를 종료했으나 현재의 엄격한 상태 검사가 과거 완료된 peer 기록 2개의 폐기된 필드를 거부했다(`StateCorrupt`). 보호된 released 영수증·현재 snapshot/mount/delta 부재·독립 payload hash를 확인한 뒤 정확한 기록을 내용 변경 없이 `.local/ap1002/archive/ua05-preexisting-records/<id>`로 옮겼다. A `42fdead74de65116b2669858`의 33개 파일과 B `78b313a0f90b0261a72742e9`의 31개 파일은 모든 상대 경로/내용 hash가 같았다. 같은 새 작업 ID/home/checkpoint 요청으로 재개해 회수를 완료했다. 제품 코드가 구형 필드를 이관·무시하거나 회수 guard를 완화하지 않았다. 최초 실패와 보관 manifest도 근거 디렉터리에 남겼다.

| 설치 서비스 관측 시점 | 경로 × 표본 | 결과 |
| --- | --- | --- |
| 최초 서비스 대기 | 9 × 3 | 모두 오류 0 |
| 실제 Capture/Observe 후, workspace provider 실행 중 | 9 × 3 | 모두 오류 0 |
| checkpoint/release 후, 서비스 계속 실행 중 | 9 × 3 | 모두 오류 0 |
| 명시적 서비스 재시작 후, PID **23076** | 9 × 3 | 모두 오류 0 |
| T: 분리 후 최종 대기 | 8 × 3 | 모두 오류 0 |

native 접근 **132회 모두** `0x12019F`, 공유 7, `OPEN_EXISTING`으로 열고 닫기만 했다. 경로는 C:, D:, E:, Program Files, ProgramData, 서비스 설치/상태, PowerShell 7과 연결 중인 T:다. 최종 서비스는 **Running/Automatic/LocalSystem**이며 정확한 시험 VHDX는 다시 분리됐다. 과거 보호 소유 기록과 이번 설치 시작 시 존재한 C: snapshot 4개는 그대로였고 새 작업의 T: snapshot만 회수했다. Splinter provider/workflow 프로세스는 남지 않았으며 서비스 자체는 계속 실행 중이다.

근거 `artifacts/storage-authority-idle-20261023/`: `install-plan.json`, `install-apply.json`, `installed-verification.json`, 다섯 `installed-*.json` 표본, `native-ensure.json`, `native-checkpoint-release.json`, `native-checkpoint-release-retry.json`, `workspace-finished.json`, `archived-legacy-records.json`, `installed-summary.json`. 작은 Windows x64 NTFS fixture의 실제 대기/정상 작업 완료/서비스 재시작 동작으로 한정한다. OS 재부팅·native VSS 도중 강제 중단·Windows Update 재시도·DISM/SFC는 **NotRun**이다.
