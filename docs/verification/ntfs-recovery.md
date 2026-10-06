# NTFS 복구·지원 경계 검증

기준일: 2026-10-01. [NR 계획](../plans/ntfs-recovery-and-support.md)의 실행 기록이다. **NR-01/03/05/07 부분 구현·자동 검증, 공통 저널의 실제 자식 종료 58개, 별도 로컬 WinFsp callback/mmap/새 provider 연결 19개, 제품 VSS·CLI의 외부 client 경계 13개**를 확인했다. 각 실행 단위는 아래 별도 후보로 구분한다. 원격 VSS 읽기·회수 복구와 5개 파일 API 거부, ARM Windows/x64 Unity 단일 작업공간 근거는 각 이전 후보의 기록이며 NR 전체 완료가 아니다.

## 새 live VSS 후보

이후의 수집 경쟁 조건·status 비용·공통 mount 접근 수정은 [별도 검증 기록](README.md)을 따른다. 아래 LV·원격 Unity 수치를 그 수정 후보의 실환경 통과로 승계하지 않는다.

[LV-01~08 기록](ntfs-live-vss-identity.md)은 캡처/live identity와 현재 읽기 위치 분리, 새 연결 세대의 비관리자 Observe/재개을 검증한다. 새 후보는 자동 180 통과·0 실패·당시 제외 시험 1 skip, 실제 VSS/Git 재연결·4KiB 범위 쓰기·원본 시점 격리, Windows x64 Unity 검증 3건·재연결 후 loaded-code·독립 결과·최종 회수를 통과했다. 77개 원본 파일과 기존 snapshot 3개를 보존했고 소유 자원은 0이다. 다음 절의 139개 통과와 원격 ARM 시험은 이전 후보 기록이며 새 후보의 ARM·실제 재부팅·전원·공간 장애는 NotRun이다.

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
| 전체 회귀 | locked restore → Release build → 자동 시험, 총 140개 중 139 통과·0 실패·당시 제외 시험 1 skip. 경고/오류 0 | Passed |

이전 NR-07 후보 증거는 `artifacts/ntfs-recovery/nr07-release-observation-20260921/`의 `final-build.trx`, `source-manifest.json`에 있다. 앞선 128개 시험 후보는 `nr01305-20260921/`, 112개 후보는 `nr01-20260921/`에 따로 보존한다. `RecoveryContractTests`, `SnapshotWorkspaceCoordinatorTests`, `VssReleaseObservationTests`, `ContractAndCliTests`, `SnapshotOverlayTests`, `OverlayFileAttributeTests`가 추가 계약을 검증한다. 기존 journal/Git/Windows 프로세스 시험도 같은 후보에서 실행했다. 파일 잠금에 의한 저장 실패는 실제 볼륨 고갈의 증거가 아니며 callback 직접 호출은 실제 mount의 파일 API 시험이 아니다.

추가 프로세스 시험은 `artifacts/ntfs-recovery/nr03-process-20260921/`의 개발용 harness와 `trials-2/result.json`에 보존한다. 제품의 동일 `JournalDeltaStore` 바이너리를 사용하되 기준 트리는 결정적 fake다. 최종 ACK sequence 1/8/32가 모두 유지됐고, 재생된 sequence는 1/9/33이었다. ACK를 받기 전 완료된 마지막 레코드가 남을 수 있다는 허용 범위와 일치한다. 초기 payload 복사와 읽기 cache는 0이다. 첫 실행은 종료 직후 공유 위반 32로 중단되어 `trials/`와 실패 설명을 보존했다. 후속 harness는 소유 프로세스 종료 확인 뒤 공유 위반만 최대 5초 재관측하며, 통과한 3건에서 해당 재시도는 0회였다. 이 시험은 WinFsp/VSS·mmap flush·저널 내부 특정 명령어 중단·재부팅/전원 손실의 증거가 아니다.

현재 snapshot probe는 root 메타데이터와 source 볼륨 접근을 관측한다. 재부팅 후 VSS ID·provider·volume의 동일성을 재검증한 증거는 아직 없다. [NR-02 준비 경계](preparation-recovery.md)와 아래 공통 저널·로컬 kernel 경계를 제외한 나머지 중단 관문, 정상 재부팅/강제 전원 종료, 실제 공간 고갈/offline, ACL/EFS·입력 특성의 전체 표와 부분 준비 직접 회수는 미완료다. 아래 실제 mount의 5개 API 거부와 VIVOBOOK 단일 Unity 작업공간 시험을 이 전체 범위와 구분한다.

## NR-03 공통 저널 경계의 실제 프로세스 종료

`artifacts/ntfs-recovery/nr03-journal-20260930b/`은 `108b594` 이후의 NR-03 공통 observer/개발 harness와 checksum이 정상인 sequence 오류 회귀를 포함한 최종 후보다. `source-and-environment.json`과 `final-audit.json`에서 소스 229개·바이너리 5개의 불변 hash, 일반 권한 caller, 실제 종료 58개와 남은 소유 자식 0을 확인했다. Runtime SHA-256은 `e5ca90d81539e575ba992d1044760cc1151a59d8628df2b9ec390d30fead27e1`이다. 기준은 결정적 fake 불변 트리이며 VSS·WinFsp·Unity를 실행하지 않았다. 형제 fixture는 clean을 유지했다.

[개발 진입점](../journal-recovery.md)의 기본 계획이 파일을 만들지 않음을 자동 시험과 별도 계획 실행으로 확인했다. `-Execute`의 최종 행렬은 **58 Passed / 0 Failed**다. 각 trial에 request·nonce/PID/시작 시각이 일치하는 signal·실제 종료 관측, 승인 prefix·최종 외부 ACK, 재생 전 journal 원문, child stdout/stderr와 결과를 남겼다. `result.json`은 아래 정확한 실행 목록의 요약이다.

| 연산 | 실제 종료 수 | 확인한 내용 |
| --- | --- | --- |
| write | 11 | append 전/header 후/metadata 후/payload 절반 후/전체 후/checksum 후/flush 전·후/apply 후/반환 후 ACK 전·후 |
| rename / replace / truncate / create / delete | 각각 9, 합계 45 | payload 없는 같은 경계, namespace·길이·내용의 전부 적용 또는 부재 |
| 명시적 Flush | 2 | flush 전·후 종료 뒤 이미 승인된 prefix 유지 |

58건 모두 승인 sequence 4를 보존했다. 34건은 sequence 4, durable flush 이후의 24건은 sequence 5로 재생했다. 마지막 외부 ACK가 실제 게시된 6건은 모두 새 내용이 남았다. 아직 반환하지 않은 완전 transaction은 생존할 수 있다는 oracle을 유지하되, 실제 이번 flush 전 사례에서는 새 transaction이 남지 않았다. 이 관측을 모든 미승인 쓰기가 사라진다는 보장으로 해석하지 않는다.

write의 payload 절반 후는 33,129바이트, payload 전체/checksum 후/flush 전의 3건은 각각 65,897바이트의 미완성 tail만 제거했다. 58건 모두 승인 prefix의 원문이 유지되고 재생 후 파일은 종료 시점 원문의 정확한 prefix였다. 두 번째 재생은 namespace·내용 hash와 journal 원문이 바뀌지 않았다. 초기 payload 복사·read cache·base payload 읽기는 0이며 합성 private 저널에 한정한 계측이다. 완전 frame의 checksum·sequence·JSON 손상 보존은 같은 후보의 기존 자동 회귀로 확인했고 손상 frame을 정상 tail로 절단하지 않는다.

최종 `pwsh ./scripts/build.ps1`은 locked restore·Release build 경고/오류 0, **총 684개 중 683 Passed / 0 Failed / 당시 제외 시험 1 Skipped**다. `final-full.trx`와 `../nr03-journal-20260930-preflight/sequence-final-build.log`가 소유하며 UTF-8·diff 검사를 통과했다. 최종 행렬의 종료 후 공유 재관측은 58건 모두 0회다. 앞선 `nr03-journal-20260930/`의 58개 성공과 682개 자동 통과도 원문을 보존한다. Runtime은 같고 sequence 손상 직접 회귀 추가로 시험 바이너리·소스가 달라져 최종 행렬을 새 디렉터리에서 다시 실행했다.

첫 전체 시험은 681개 중 679 Passed / 1 Failed / 1 Skipped였다. `replace / commit.after-apply`에서 정확한 자식 종료 확인 직후 journal open의 Win32 공유 위반 32로 replay 전에 실패했다. `../nr03-journal-20260930-preflight/first-full.trx`와 `failure.json`에 당시 바이너리 hash·실패를 보존했다. harness의 post-exit guard/open에만 공유 위반 32/33의 최대 5초 재관측을 추가했고, 접근 거부·부재·손상·replay 오류는 재시도하지 않는다. 첫 실패를 제품 데이터 손상이나 flush 정책 결함의 해결로 표시하지 않는다.

판정은 **공통 저널 단위 scoped-complete-with-limitations**, 전체 NR-03은 **InProgress**다. 부분 header/checksum 바이트별·OS flush 호출 내부·replay의 tail 절단/flush 도중 강제 종료, 실제 WinFsp callback 응답/재연결, 명시적 mmap flush, 동시 reader/writer, VSS·재부팅·전원·물리 공간 장애는 이 공통 후보에서 NotRun이다. 아래 별도 kernel 후보와 이전 LV·원격 실행이나 NR-02 후보의 실제 자격을 이 후보로 자동 승계하지 않는다.

## NR-03 로컬 WinFsp callback·mmap·새 provider 연결

`artifacts/ntfs-recovery/nr03-kernel-20260930/`은 공통 저널의 로컬 commit `ab9c94e` 뒤의 개발 observer·harness·schema를 포함한 최종 후보다. Windows x64, .NET 10.0.12, 기존 WinFsp 설치에서 제품 `JournalDeltaStore`·`WinFspOverlayFileSystem`과 결정적 fake 불변 트리를 실제 mount에 연결했다. 실제 VSS server/CLI·Git·Unity·VIVOBOOK은 실행하지 않았다. `source-and-environment.json`, `candidate.json`, `final-audit.json`은 소스 238개·관리 바이너리 6개의 동일 hash와 비관리자 caller를 확인한다. Windows 어댑터 SHA-256은 `501c232538f54d9479cb1390a5ace80d046b5314d2f8b66f92e2d1b75e74dc43`이다.

[개발 진입점](../kernel-recovery.md)의 최종 전체 행렬은 **19 Passed / 0 Failed**다. trial별 request·signal·외부 prefix/ACK·정확한 종료 PID/시작 시각·원문 journal·재연결 요청/ready·정상 stopped·stdout/stderr·schema 검증 result를 보존했다. callback 부작용 후는 성공 응답 전에 provider를 종료하며 외부 ACK와 구분한다.

| 연산 | 실제 종료 수 | 확인한 내용 |
| --- | --- | --- |
| write / rename / replace / truncate / 명시적 flush | 각각 3, 합계 15 | callback 전·부작용 후 응답 전·클라이언트 ACK 후, 승인 prefix와 완료한 새 내용 보존 |
| mmap | 4 | view flush 전·파일 flush callback 전/후·view/file flush 완료 뒤 외부 ACK, 페이지 내용 oracle |
| 같은 경로의 새 provider | 위 19건 각각 | 같은 delta/identity 재생·실제 kernel 읽기·beta 새 4KiB 쓰기/flush·정상 unmount |
| 다른 파일의 동시 Read | write 부작용 후 1건 | gate 전에 열린 beta의 4KiB 읽기 응답/hash를 gate 중 게시, Close/reader 종료는 provider 종료 뒤 확인 |

19건 모두 승인 prefix를 보존하고 첫/두 번째 재생에서 journal 원문이 바뀌지 않았다. after-effect/외부 ACK 뒤는 완전한 새 내용을 요구했다. mmap view flush 전의 이번 관측은 새 페이지 부재였고 파일 flush callback 전/후에는 페이지가 남았다. 아직 승인하지 않은 페이지는 생존 또는 부재를 허용하며 이 관측을 보장으로 확대하지 않는다. mmap ACK는 view flush와 파일 `Flush(true)`가 모두 끝난 뒤만 게시한다. 초기 payload 복사·read cache는 모두 0이고, 정상 기준 읽기의 `baseReadBytes`는 4,096~65,536바이트로 별도 기록했다.

최종 `pwsh ./scripts/build.ps1`은 locked restore·Release build 경고/오류 0, **총 695개 중 694 Passed / 0 Failed / 당시 제외 시험 1 Skipped**다. `final-full.trx`·`final-build.log`를 보존했다. 공통 callback fake 5개 연산·19개 요청 조합/schema·기본 계획의 경로 미생성과 기존 회귀를 포함한다. UTF-8·diff 검사도 통과했다. 최종 19건은 새 provider의 핸들/retained context 0·mount 부재·소유 자식 0을 확인했다. 최종 감사는 smoke/실패/preflight/final의 41개 mount 관측과 204개 PID/시작 시각 증거를 대조하고, 남은 kernel child actor 0·모든 delta 보존·형제 fixture clean을 확인했다.

최초 `nr03-kernel-preflight-20260930/`은 1건 통과 뒤 write after-effect의 reader 프로세스 종료를 기다리다가 15초 deadline으로 실패했다. 당시 `File.ReadAllBytes`는 open/read/dispose를 구분하지 않아 실제 대기 단계·하위 원인은 **unknown**이다. 후보 hash·signal·prefix·delta·stdout/stderr·`failure.json`·인접 `.log`를 보존했다. 기존 `Cleanup`이 읽기 전용 핸들도 mutation gate를 잡는다는 코드 근거에 따라 후속 harness는 미리 열린 Read 응답과 Close/프로세스 종료를 나눴다. 제품 잠금·flush 정책은 변경하지 않았다. `nr03-kernel-preflight2-20260930/`의 19건 성공도 보존하며, 이후 최종 capability 문구를 포함한 전체 build/새 행렬이 위 최종 근거다. 첫 실패를 새 open/close 동시 완료나 제품 데이터 손실 결함 해결로 표시하지 않는다.

판정은 **로컬 kernel 단위 scoped-complete-with-limitations**, 전체 NR-03은 **InProgress**다. 새 open/close의 gate 중 응답·같은 파일의 동시 쓰기 원자성·실제 VSS server/제품 CLI의 중단/재연결·OS flush 내부·replay 중 종료·재부팅·전원·물리 공간 장애·ARM/VIVOBOOK은 이번 후보에서 **NotRun**이다. 이전 공통 저널/LV/Unity/원격 후보의 실환경 자격을 자동 승계하지 않는다. VIVOBOOK 시험이 필요하면 실행 전에 사용자에게 알린다.

## NR-03 제품 VSS·CLI의 클라이언트 경계

최종 PNG 후보의 실제 전체 행렬은 **13 Passed / 0 Failed**, 제품 외부 client 단위는 **scoped-complete-with-limitations**다. `artifacts/ntfs-recovery/nr03-product-png-20261001/`의 `candidate.json`·`fixture-attributes.json`·`frozen-source.json`·`final-audit.json`이 소스 244개·관리 바이너리 20개의 불변 hash를 확인한다. 같은 경로의 `environment.json`은 설치된 제한 권한 서비스와 WinFsp 파일의 불변 hash를 소유하며 설치/grant·볼륨/예산은 변경하지 않았다. CLI caller는 비관리자다. 최종 `pwsh ./scripts/build.ps1`은 **699개 중 698 Passed / 0 Failed / 당시 제외 시험 1 Skipped**, 경고/오류 0이며 `final-full.trx`·`final-build.log`를 보존한다.

[개발 진입점](../product-write-recovery.md)은 기존 등록된 clean `T:/ua-source @ f71e4e244ba9b085aa0fc439336e4c60ee3d5d5d`·base `codex/library-20260929`와 제품 `.local/wl29`를 사용한다. 실제 기준은 VSS이며 일반 제품 ensure/server/WinFsp/Git 조합이다. fake 기준을 쓰거나 provider에 observer를 연결하지 않는다. 소스 FBX와 명시적 binary PNG의 합계 129,189바이트 oracle는 시험 evidence 비용이고 제품 초기 복제량이 아니다. 서비스가 조회한 capture·release-isolation으로 기존 snapshot ID 3개를 대조한다. 일반 사용자의 전역 VSS 직접 조회는 access denied/initialization failure로 **unknown**이며 0으로 표시하지 않는다.

| 연산 | 실제 종료 수 | 확인한 내용 |
| --- | --- | --- |
| write / rename / replace / truncate / 명시적 file Flush | 각각 2, 합계 10 | client 연산 전·외부 ACK 후, 승인 prefix와 완료한 내용·namespace 보존 |
| write | 1 | Write 반환 뒤 명시적 file Flush 전에 종료해도 새 4KiB 내용 보존 |
| mmap | 2 | view Flush 전·view/file Flush와 외부 ACK 후의 페이지 oracle |
| 동일 요청 재연결·명시적 보존/회수 | 위 13건 각각 | 같은 snapshot/delta/Git 등록·새 현재 읽기 검증/provider, 반복 ensure 불변·새 쓰기·독립 ref/blob·최종 자원 0 |

13건 모두 정확한 provider token/workspace/snapshot/generation/PID/시작 시각의 실제 종료와 mount 부재 뒤 동일 ensure로 재개했다. 첫 재생과 두 번째 ensure에서 journal 원문이 유지되고 두 번째 ensure는 같은 새 provider를 사용했다. beta의 새 4KiB 쓰기/flush 뒤 일반 `validation preserve-worker`의 독립 Git ref/blob/namespace를 검증했으며 `stable-journal-v1`로 수집했다. Unity validation 성공은 부여하지 않는다. mmap view Flush 전의 이번 관측은 새 페이지 부재였고 view/file Flush 완료 ACK 후에는 새 페이지가 남았다. 미승인 페이지는 완전 생존 또는 부재를 허용하며 이 관측을 일반 보장으로 확대하지 않는다. 13건의 제품 초기 payload 복사와 read cache는 모두 0이다.

일반 release는 모든 13건의 snapshot/mount/provider 부재, 핸들·working set·private 메모리 0, mount/Git 등록 디렉터리/delta 부재와 정확한 소유 프로세스 종료를 확인했다. 최종 감사는 아래 실패/부분 실행을 포함한 **21개 소유 workspace·59개 provider/client PID/시작 시각 증거**를 대조했다. 모든 독립 결과 refs를 보존하고 소유 자식 0, 기존 비소유 snapshot 3개 보존, 첫 smoke 전부터 마지막 실행 후까지 원본 HEAD/index/tracked hash·기존 refs/worktree 불변과 형제 fixture clean을 확인했다. 원시 journal·현재 후보·실패 증거는 삭제하지 않았다.

### 보존한 실패와 재검증 조건

- `nr03-product-smoke-20261001/`의 처음 write-before-effect는 product 재연결 뒤 harness `File.ReadAllBytes`의 공유 위반으로 실패했다. 제한 환경 build가 exit 1로 끝났는데 실행 전에 확인하지 않아 수정 전 후보를 실행한 순서 오류였다. 실행한 DLL/JSON과 `first-full.trx`를 보존했다. 계측에 ReadWrite/Delete sharing과 IPC의 같은 경로 guard/open 재관측만 추가했으며 제품 잠금/flush 정책은 그대로다. 같은 workspace `ceec9769dcfadfac31d7fa35`의 prefix·기준 바이트를 확인하고 일반 preserve-worker/release로 독립 ref `ee65032673fe7c09d71ae126b8cddd3056f073b9`와 전체 회수를 완료했다. 첫 실패를 통과로 바꾸지 않는다.
- inputactions 후보 `nr03-product-20261001/`은 write 전/ACK 후·rename 전 3건을 마친 뒤 다음 초기 ensure의 `captured` 상태 저장에서 `StateWriteIncomplete`를 반환했다. mutation 경계는 **NotRun**이었다. workspace `0ec42f18abf0c388465584b2`, snapshot `{F9A02240-C144-4982-B01A-525346E10BDD}`와 완전 tmp JSON·실패 응답·status를 보존했다. 같은 요청 ensure로 같은 snapshot의 Ready를 회복하고 편집 없는 독립 결과/release를 완료했다. 이전 NR-02 게시 실패와 같은 오류 코드지만 하위 HResult/원인은 여전히 **unknown**이며 retry 정책을 추측 변경하지 않았다.
- 같은 inputactions 후보의 `nr03-product-20261001b/`은 새 rename ACK 후와 replace 전 2건을 마쳤다. replace ACK 후는 원문 VSS/저널/재연결 oracle를 통과했지만 독립 Git blob의 raw-byte hash가 달라 실패했다. FBX를 `*.inputactions text eol=lf`로 선언한 경로에 옮긴 fixture였다. `normalization-evidence.json`은 105,120바이트에서 CRLF의 CR 정확히 3개를 제거한 105,117바이트가 Git blob hash와 일치함을 입증한다. 원문·canonical Git blob·raw journal·실행 DLL/JSON을 보존하고 workspace `56a39a5c8a16ba8ef897dbb9`의 동일 결과/ref/release를 완료했다. 원문은 독립 `independent-raw-beta.bin`으로도 남는다. Git 선언 정책을 변경하거나 제품 정규화 결함을 수정했다고 표시하지 않는다.

PNG 후보는 `Assets/TutorialInfo/Icons/URP.png`의 `text: unset`·`binary: set`을 실제 확인한 다른 바이너리/입력이다. 이전 후보의 5개 완료 경계를 승계하지 않고 13개 모두 새로 실행했다. 전체 NR-03은 **InProgress**이며 현재 후보의 실제 VSS 내부 callback/OS flush 중단·replay 중 종료·동시 writer/read open/close·Unity/ARM·재부팅·전원·물리 공간 장애는 **NotRun**이다. 준비 단계 `StateWriteIncomplete`의 하위 원인도 unknown으로 유지한다. VIVOBOOK은 사용하지 않았으며 시험이 필요하면 실행 전에 사용자에게 알린다.

## VIVOBOOK 준비

사용자가 지정한 원격 작업의 빈 `splinter-test-vivobook`을 사용한다. 원격 하위 `splinter-unity`에 Git bundle로 fixture를 복사했다. 원격이 기대 revision `46e7f4dbdb219e4afa602f3598a3de9ff4070fae`, bundle SHA-256, 추적 파일 77개, clean `development` 일치를 각각 확인했다. 원본의 `Assets`/`Packages`/`ProjectSettings`/`.meta`와 Git 이력을 보존하며 `Library`/`Temp`/`Logs`는 복제하지 않았다. 이는 시험 환경 최초 준비이며 제품 workspace 복제량 계측에 합산하지 않는다.

사용자가 제공한 환경은 ARM Windows 11, RAM 32GB와 용량 342GB다. 이 용량 표현을 실제 여유 공간으로 대신하지 않고 원격에서 측정했다. 12:34 UTC의 시스템 NTFS 볼륨 여유는 336.618309GiB다. 프로젝트 내부 `.tools/dotnet`에 .NET SDK 10.0.112 ARM64 ZIP을 공식 SHA-512와 대조해 준비했다. **시험 Editor는 기존 x64 Unity 6000.3.22f1**이다. 사용자가 선택한 기존 설치를 유지하며 ARM64 Editor를 추가하지 않는다. 이 fixture의 canonical x64 Editor 첫 임포트는 아래 준비 절에 기록하며 제품 Unity workspace 검증과 구분한다.

원격 Codex의 최근 작업 본문이 이쪽 조회에서 비어 있어 지정 파일만 제공하는 한시적 로컬 전송 서버를 사용했다. 결과는 최소 bool/오류 코드 JSON으로 직접 수신했다. 계정명·전체 경로가 포함된 보고서는 전송하지 않으며 사용자에게 중계나 수동 복사를 요구하지 않는다. 증거는 `artifacts/remote-vivobook/transfer/vivobook-report-*.json`에 보관한다. 이 전송 서버는 제품 기능이 아니다.

사용자가 승인한 현재 제품 소스 162개와 파일별 manifest를 원격에 전달했다. ZIP SHA-256은 `3d3efb9239d55a18a8a622ba76af1335efc2c4252eaa92915f51dbb83a74005c`이고 시험 대상 소스 aggregate는 `8422f019575a3a2bb48a239f9220f2e3ba51bb34079d9982563dc8f5b0e8ad55`다. ARM64 .NET의 locked restore·Release build·자동 시험은 **총 128개, 127 통과·0 실패·당시 제외 시험 1 skip**이다. 전송 hash, 소스 불변 및 최종 TRX hash를 확인했다. `WindowsProcessTests`는 PATH로 시작한 PowerShell의 `$PSHOME`을 atomic 파일로 직접 받아 실행 파일을 확인하므로 설치 경로를 고정하거나 외부 `Process.MainModule` 조회에 의존하지 않는다.

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
