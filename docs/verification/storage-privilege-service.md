# 반복 UAC 제거 검증

**등록된 Windows x64 프로젝트/home의 정상 반복 UAC 제거를 확인했다.** 2026-09-29에 제한 서비스 설치 후 일반 사용자 CLI로 실제 NTFS/Git A/B 생성·같은 snapshot 재연결·독립 결과 보존·회수를 실행했다. UA-01~04의 아래 범위는 Passed이며, 제품 전체 설치·일반 Unity·ARM·NR 장애 지원의 완료를 뜻하지 않는다.

## 판정과 실행 근거

원시 근거는 `artifacts/storage-authority/ua-20260928/`에 있다. [종합 결과](../../artifacts/storage-authority/ua-20260928/qualification-20260929.json)는 각 workspace의 snapshot/mount/delta 부재와 소유 프로세스·메모리·핸들 0, 외부 snapshot 보존, CLI 권한과 서비스 상태를 묶는다.

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
