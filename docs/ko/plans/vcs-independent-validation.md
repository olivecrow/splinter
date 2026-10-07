# 일반 폴더의 실제 A/B 실행 계획

[English](../../plans/vcs-independent-validation.md)

[구현 범위](implementation-plan.md#마감한-선행-개발-단위-vcs-없는-기본-수명주기)의 실제 관문을 고정한 실행 계획이다. 사용자 관리자 Apply·A/B 승인으로 실행했고 [검증 기록](../verification/vcs-independent-workspaces.md)의 **scoped-complete-with-limitations**로 마감했다. 실제 경로 결함 수정 뒤 locked restore/Release는 795개 중 794개 통과·당시 제외 시험 1개 Skipped다. 한 묶음의 후보/실패/동일 요청 복구는 검증 기록이 소유하며 전체 장애/설치 자격으로 확대하지 않는다.

## 고정 입력과 변경할 머신 구성

| 항목 | 검토할 값 |
| --- | --- |
| 새 소스 | `T:\vcs-none-20261002` — 기존 Git fixture를 변경하지 않고 만든 4개 파일의 작은 Unity 입력. `.git`·Plastic 관리 정보 없음 |
| 고정 key / 요청 | A `vcs-none-a-20261002`, B `vcs-none-b-20261002`; `source=none`, `application=unity`, `base=snapshot`, `storage=ntfs-snapshot`, journal budget 16 GiB |
| 새 home | `C:\Users\olive\Desktop\Development\splinter\.local\vcs1002` — 원본 밖의 새 NTFS home |
| Unity / 도구 | 설치된 `6000.3.22f1`, Unity CLI `1.0.0-beta.5`, fixture Pipeline `0.7.0-exp.1`. 필요한 외부 명령은 설치된 CLI의 help/schema로 확인하며 모든 실행에 정확한 project path 사용 |
| 서비스 | 기존 `SplinterStorageAuthority` 업데이트·정상 재시작. 일반 workspace/Unity/Git는 비승격 상태 유지 |
| 패키지 | 첫 실행 `artifacts/vcs-independent/package-20261002-01` — manifest SHA-256 `82eee942d8732f57afd8ee419dd283ee32289f573ff64783ce102bd87074f21d`. 회수 경로 결함 수정 뒤 `package-20261002-02` — `57a91e572c7be16e61a3a6f6f2901bfc9c8e3d18c6297ffd26f3f88edfcd2571`. 각 명시 파일 14개, 두 계획/Apply/실패 증거 유지 |
| grant | `artifacts/vcs-independent/grants.json` — 기존 3개 유지, 새 source/home 1개 추가. 네 범위의 현 디렉터리/볼륨 ID를 읽어 대조함 |
| 읽기 전용 계획 | `artifacts/vcs-independent/service-update-plan.json` — `Action=Update`, `Apply=false` 검증 성공. 설치 경로 `%ProgramFiles%/Splinter/StorageAuthority`, 기존 보호 ledger `%ProgramData%/SplinterStorageAuthority` 보존 |
| 보존 대상 | `protected-processes.json`의 기존 Editor·import worker·서비스 관측. 원본 `splinter-unity`의 Editor/파일·기존 `T:\ua-source`·기존 workspaces/results/snapshots를 시험 대상으로 사용하거나 종료/삭제하지 않음 |
| 공간 관측 | 준비 시 C: 여유 284,184,903,680 bytes, T: 여유 15,174,967,296 bytes. 실제 capture 직전 상태/볼륨/VSS 여유 재확인. VSS 귀속/전체 물리 peak는 unknown 유지 |

현재 서비스의 보호된 구성/ledger와 새 패키지를 관리자 Apply가 다시 대조한다. 알려지지 않은 grant가 더 있거나 identity가 바뀌면 기존 구성을 보존하고 거부한다. 기존 상태 v1은 새 v2 workspace로 이관하지 않는다. 설치·grant·볼륨 설정을 일반 CLI의 실패 fallback으로 변경하지 않는다.

승인받은 관리자 작업은 다음 업데이트다. 기본 설치 도구의 검사·원자적 파일 게시·기존 grant/보호 상태 보존·SCM 재시작 절차를 사용한다. 별도 패키지와 실패 증거는 보존하며 설치 파일의 자동 backup을 주장하지 않는다.

```powershell
pwsh -NoProfile -File ./scripts/setup-storage-service.ps1 -Action Update -PackagePath ./artifacts/vcs-independent/package-20261002-01 -GrantFile ./artifacts/vcs-independent/grants.json -Apply
```

## 한 묶음의 실행과 종료

1. 서비스 적용 뒤 상태/비승격 CLI·새 grant를 확인한다. 새 fixture만 소유한 Unity 과정으로 최초 import를 준비하고 정상 종료한다. 실제 capture 전 source manifest와 private/generated 경계를 고정한다. seed hash는 `fixture-seed.json`이며 import 후 기준과 혼동하지 않는다.
2. `--source none --application unity --base snapshot --storage ntfs-snapshot`의 A/B를 서로 다른 고정 key로 생성한다. 반환 project path·id·digest·snapshot·기준을 보존하고 새로운 CLI에서 같은 요청을 재개한다. 원본 marker를 이후 수정해 A/B가 불변 기준을 유지하는지 확인한다.
3. 각 소유 Editor를 정확한 project path·PID·시작 시각으로 확인한다. A의 소스를 수정해 기존 외부 도구로 필요한 내용/컴파일 관측을 수집하고 B는 실패한 소스를 보존한다. peer·원본·공유 서버를 종료/재선택하지 않는다. Splinter의 liveness를 컴파일 성공으로 읽지 않는다.
4. A의 정상 stop·checkpoint·안정 seal·기준/결과의 실제 payload를 확인한다. A release 중 B가 계속 자신의 경로에서 작동하는지 확인하고 회수 뒤 원본 변경과 독립된 결과 내용을 검증한다. snapshot 회수가 지연되면 `reclaim-pending`과 실제 남은 자원을 그대로 기록한다.
5. B는 정상 stop 후 명시적 intent/digest의 discard를 실행하고 같은 intent 재시도·결과를 요구하지 않는 회수·원본/A 결과 보호·실제 mount/delta/소유 자원 0을 확인한다. 마지막 snapshot 회수는 기존 정확한 ID·의존성·격리 guard를 따른다.
6. journal의 초기 payload 복제·hydration·copy-up 0 및 범위 쓰기/승인 flush 관측과 명시적 결과 보존의 논리/실제 할당 비용을 구분한다. 전체 물리 peak·공유 VSS를 임의로 0이나 workspace 귀속으로 표시하지 않는다. 결과/실패/제약을 VCS 추적표에 반영한다.

사용 가능한 환경에서 이 한 묶음과 변경한 부작용 경계만 검증한다. 권한/소유권/손상/격리 guard에 막히면 해당 evidence와 자원을 보존한다. 자동 다른 source mode·새 key recapture·비소유 snapshot 삭제·새 전체 harness/행렬·대형/원격/OS/ARM 시험으로 확대하지 않는다. 일반 폴더 원본 apply·Plastic 사용 환경의 호환 처리는 후속이다.
