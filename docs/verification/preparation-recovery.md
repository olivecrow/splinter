# NTFS 준비 단계의 CLI 중단 복구

NR-01/02의 `workspace ensure` 준비 단계는 **scoped-complete-with-limitations**다. 같은 최종 후보의 26개 실제 CLI 종료 경계에서 동일 요청·동시 재호출의 Ready, 독립 결과와 정상 회수, 원본/기존 refs/비소유 snapshot 보존을 확인했다. [최종 감사](../../artifacts/ntfs-recovery/nr02-20260930e/final-audit.json)가 정확한 경계별 workspace/snapshot/result identity와 판정을 고정한다. 상태 게시 실패 1건의 원인 미확정과 명시적 복구 기록은 아래 제한으로 유지한다.

최종 후보는 같은 binary hash의 [d 실행 8개](../../artifacts/ntfs-recovery/nr02-20260930d/results.json)와 [e 실행 18개](../../artifacts/ntfs-recovery/nr02-20260930e/results.json)를 연결한다. d의 완료 경계 8개 뒤 한 상태 게시 실패로 harness가 중단돼 e에서 해당 경계부터 남은 18개를 새 요청으로 재실행했다. 실패한 작업의 원래 요청 재개/회수와 새 경계 실행은 구분한다.

## 후보와 실행 조건

- 기준은 `development @ d9825ba` 이후 작업 트리다. [바이너리 hash](../../artifacts/ntfs-recovery/nr02-20260930d/candidate.json)는 실제 실행한 Release DLL/JSON을 고정한다.
- [코드/schema/harness·설치 환경 manifest](../../artifacts/ntfs-recovery/nr02-20260930d/code-and-environment.json)와 e의 동일 binary hash를 대조했다. 실행 중 구현과 설치된 권한 서비스 DLL/JSON은 변경되지 않았다.
- 제품 CLI는 일반 사용자이고 [서비스 상태](../../artifacts/ntfs-recovery/nr02-20260930d/service.stdout.json)의 `callerElevated=false`, protocol 1, 설치된 `SplinterStorageAuthority=running`을 확인했다. 기존 grant와 서비스 설치를 재사용한다.
- clean NTFS/Git source는 `T:\ua-source`, base는 `codex/library-20260929`, revision은 `f71e4e244ba9b085aa0fc439336e4c60ee3d5d5d`다. home은 제품 저장소의 `.local/wl29`다. Unity Editor는 시작하지 않는다.
- source의 Library를 새로 만들거나 복제하지 않는다. 기존 VHD·VSS 예산·서비스/grant·원본 소유자·전역 Git 설정을 변경하지 않는다. fixture의 Git 소유자 차이는 자식 전용 `XDG_CONFIG_HOME`의 정확한 source 예외로 처리한다.
- [필수 빌드 결과](../../artifacts/ntfs-recovery/nr02-20260930d/final-full.trx)는 locked restore·Release 빌드 뒤 615 Passed / 0 Failed / 1 optional ReFS Skipped다. 새 공통 준비 시험은 19개 실제 자식 종료/fake 복구와 subprocess/schema 2개다. fake는 VSS·WinFsp 성공으로 집계하지 않는다.

## 실행 방법과 판정

각 boundary는 새 key를 사용한다. 개발용 `preparation-probe`가 정확한 경계에서 flush한 signal을 게시하면 harness가 PID와 시작 시각을 대조하고 해당 CLI만 종료한다. provider나 권한 서비스는 강제 종료하지 않는다. 각 디렉터리의 `request.json`·`signal.json`·중단 당시 영속 기록·일반 ensure 응답을 함께 읽는다.

새 일반 ensure CLI 두 개를 동시에 시작한다. 잠금의 5초 `WorkspaceBusy`는 먼저 끝난 준비 뒤 같은 요청으로 다시 실행하며 성공으로 덮지 않고 최초 실패 응답도 보존한다. 두 Ready 응답의 workspace/snapshot/provider identity가 같아야 한다. 응답 boundary에서는 일반 완료 응답을 사용하지 않은 뒤 같은 요청을 다시 호출한다. 각 boundary의 base가 다른 재호출은 `RequestKeyConflict`인지 확인한다. 이후 일반 `workspace release`로 독립 Git 결과를 보존하고 실제 자원 부재를 판정한다.

| 준비 경계 | 실제 CLI 종료 지점 |
| --- | --- |
| 요청과 소스 | `request.before-record`, `request.after-record`, `source.before-effect`, `source.after-effect`, `source.after-record` — 5개 확인 |
| capture | `capture.before-effect`, `capture.after-effect`, `capture.after-record` — 3개 확인 |
| Git 등록 | `registration.before-effect`, `git-registration.after-effect`, `git-registration.after-record`, `registration.after-effect`, `registration.after-record` — 5개 확인. 첫 게시 실패와 fresh-key 재실행 구분 |
| delta | `delta.before-effect`, `delta.after-record` — 2개 확인 |
| provider | `provider.before-effect`, `provider.after-effect`, `provider.after-record` — 3개 확인. 중단 전 intent와 재개된 provider의 generation/PID/시작 시각 일치 |
| mount | `mount.before-effect`, `mount.after-effect`, `mount.after-record` — 3개 확인 |
| 독립 index와 Ready | `attach.before-effect`, `attach.after-effect`, `ready.before-record`, `ready.after-record` — 4개 확인 |
| 응답 | `response.before-publish` — 1개 확인. 완료 응답 유실 재호출도 같은 identity |

capture의 after-effect는 제한 서비스가 capture 영수증을 반환했지만 Runtime이 snapshot ID를 아직 저장하지 않은 지점이다. native VSS 호출 내부나 helper의 capture-return 게시 내부를 강제 종료한 시험이 아니다. Git의 after-effect는 실제 `worktree add --no-checkout` 뒤 등록 영수증 전이며 after-record는 영수증 뒤 pointer-only 준비 디렉터리 정리 전이다. delta identity 게시 자체가 생성 영수증이므로 별도 after-effect/after-record 간격을 만들지 않는다. index는 attach 전후를 시험하며 Git 명령 내부의 index.lock 중단을 포함하지 않는다. Ready는 외부 부작용이 없는 상태 게시 전후다.

provider before-effect는 durable launch intent 이전이다. intent 게시 뒤 OS 프로세스 생성 여부가 불명확한 짧은 간격은 `OverlayStartUncertain`으로 보존하는 기존 거부 계약을 유지한다. after-effect는 프로세스 생성 후 parent receipt 전이다. child가 token·workspace·generation이 같은 intent를 확인하고 자신의 PID/시작 시각 영수증을 게시하므로 정상 동일 ensure가 살아 있는 해당 provider를 이어서 확인한다. 다른 owner/generation·손상 기록·snapshot unknown을 추측 재실행하지 않는다. 해당 거부 회귀는 `OverlayMountObservationTests`, `OverlayControlReadTests`, `OverlayReadinessTests`와 기존 상태/identity 시험의 자동 범위다.

`OverlayLaunchReceiptTests`는 실제 임시 파일에서 누락·손상 intent, 다른 owner/token/generation, 빈 generation 6개를 실행한다. native mount 전에 자식 실행을 거부하고 기존 intent/state/receipt 바이트를 보존하며 delta/mount를 생성하지 않아야 한다. 권한 확인 전 실패 상태를 새로 게시하는 것도 허용하지 않는다. 이 자동 시험은 실제 provider 정상 시작 자격과 구분한다.

## 보존과 해석 제한

원본 HEAD·index hash·tracked 파일 hash·dirty 상태·이전 refs·기존 worktree 등록은 실행 전후 대조한다. 새 시험 branch와 독립 result ref는 보존할 출력이다. ignored Library 전체의 물리 할당 변화나 workspace별 VSS 공간 귀속은 이 시험의 oracle이 아니다.

최종 감사는 d 시작 전 원본과 e 종료 뒤 원본을 대조해 모두 동일함을 확인했다. 각 trial은 `initialPayloadCopiedBytes=0`, `readCacheBytes=0`, 하나의 소유 snapshot과 기존 비소유 snapshot 3개의 목록을 확인했다. provider 생성 뒤 중단 사례는 부모가 게시하기 전 intent의 generation과 자식 영수증·Ready mount의 PID/시작 시각이 그대로 이어졌다. 새 capture/provider로 바꿔 통과한 것이 아니다.

각 release는 `released`, 소유 provider/mount/delta/snapshot 부재와 handles/working set/private bytes 0을 요구한다. `release-isolation.json`의 전후 목록과 `missingOtherIds`로 비소유 snapshot 보존을 대조한다. unknown·reclaim-pending은 완료가 아니다. 예외 주입이나 JSON 수동 편집으로 대신하지 않는다.

26개 경계의 독립 ref와 `stable-journal-v1` 결과를 모두 검증했고 실제 runner 조회는 0개였다. 실패/중단 후보와 원래 요청으로 복구한 추가 작업도 결과 보존 뒤 `released`다. 영속 영수증·실패 임시 파일·결과 refs는 의도적으로 남긴 증거이며 자원 부재와 구분한다.

이 후보는 Unity 코드/에셋 저작·Play·ARM A/B·재부팅·전원·물리 공간 장애, 쓰기/flush/결과/회수의 나머지 중단 경계를 실행하지 않았다. NR-01/07 전체나 NR 지원 전체의 완료로 확대하지 않는다.

provider 시작은 제품 validation view도 공유한다. 이전 WF/LB의 실제 Unity·Library 기록은 각 기록의 고정 후보에 대한 근거이며 이번 후보의 validation/Library 재자격으로 자동 승계하지 않는다. 이번 실행의 `ValidationView=null` 준비 경계와 fake workflow 회귀만 현재 후보로 확인한다.

## 보존한 실패와 재검증 조건

착수 전 `d9825ba` 기준 전체 시험은 587 Passed / 1 Failed / 1 optional ReFS Skipped였다. `WindowsProcessTests.SupervisorSurvivesClientAndCleansDescendantThatOutlivesRoot`가 `TestDirectory.Dispose`의 임시 `Splinter.Core.dll` 삭제 접근 거부로 실패했고 같은 바이너리 단독 재실행은 1 Passed였다. 원인은 미확정이며 해당 시험을 수정하지 않았다. 이번 최종 전체 시험에서 통과한 사실과 구분한다. [기준 실패/단독 재실행](../../artifacts/ntfs-recovery/scope-20260930/)을 보존한다.

실환경 harness 준비 중 Git 함수 재귀·safe.directory 경로 표기·제품 자식이 제거하는 `GIT_*` 설정 때문에 실패한 `nr02-20260930`, `nr02-20260930a`, `nr02-20260930b` evidence를 보존한다. 마지막 실패는 capture 전 새 작업 기록만 남겼다. 정확한 원래 요청으로 작업 `82034cc66236f1251c2dbe56`을 재개하고 독립 결과/정상 회수한 응답은 [중단 후보 회수](../../artifacts/ntfs-recovery/nr02-20260930c/owned-cleanup/)에 있다. 이 실패를 boundary Passed로 집계하지 않는다.

첫 구현 후보의 실제 시험은 권한 확인 전 실패 기록 게시를 보강하기 위해 15번째 작업에서 중단했다. [15개 작업의 독립 결과/회수](../../artifacts/ntfs-recovery/nr02-20260930c/stopped-run-cleanup.json)와 첫 후보의 609 Passed / 0 Failed / 1 optional ReFS Skipped를 보존한다. 완료 경계 14개와 진행 중이던 작업의 정상 회수를 구분한다. 첫 후보의 부분 통과를 최종 후보의 통과로 승계하지 않고 26개 경계를 모두 새로 실행한다.

최종 후보 d의 `registration.before-effect`에서 첫 새 CLI가 `snapshot-ensured` 게시 중 `StateWriteIncomplete`를 반환했다. `ready` 기록은 이미 남았고 실패 임시 파일은 보존됐다. 동시에 시작한 CLI는 최초 `WorkspaceBusy` 뒤 같은 요청으로 동일 snapshot/provider의 Ready에 도달했다. 원래 요청의 명시적 status/ensure/release 응답과 실패 임시 파일은 [해당 trial](../../artifacts/ntfs-recovery/nr02-20260930d/registration.before-effect/)에 있다. 오류의 하위 HResult는 기존 기계용 응답에 없으므로 공유 오류·접근 거부·공간 원인을 확정하지 않으며 Runtime retry 정책을 추측 변경하지 않는다. 원인 미확정의 게시 실패를 정상 통과로 덮지 않고 실제 재개/독립 결과/회수와 fresh-key 재실행을 별도로 판정한다.

준비 순서·capture/Git identity·delta 초기화·provider 실행/receipt·schema·잠금 계약을 변경하면 새 후보의 해당 경계를 재실행한다. 이전 binary hash의 실제 통과를 새 후보에 자동 승계하지 않는다.
