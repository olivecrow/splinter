# 스냅샷 지연 회수 검증

실행 범위는 [지연 회수 1차](../plans/deferred-snapshot-reclamation.md)다. 기준 커밋은 `79f77ab`이고 후보는 development의 작업 중 변경이다. 증거 루트는 `artifacts/storage-foundation/deferred-20260926/`다.

정의한 지연 회수의 자동 계약/fake/실제 helper 함수와 실제 격리 NTFS/Git 경로를 검증했다. Unity 실행이나 NR 전체 지원으로 확대하지 않는다.

첫 표준 빌드의 자동 시험에서 252개 중 248 통과·3 실패·선택적 ReFS 1 skip을 확인했다. 3건은 새 descriptor의 retry 값이 기존 enum을 벗어난 2건과 CLI 테스트 fixture의 필수 requestDigest 누락 1건이다. descriptor는 기존 `idempotent-release`를 유지하고 fixture를 수정했다. [수정 후 빌드](../../artifacts/storage-foundation/deferred-20260926/build-03.log)는 252개 중 251 통과·0 실패·선택적 ReFS 1 skip이다. 샌드박스 내 locked restore는 진단 없이 종료되어 동일 표준 빌드를 일반 권한 환경에서 실행했다.

## 실제 NTFS/Git 수명주기

기존 소유 `isolated-ntfs.vhdx`를 다시 연결해 `V:\sf-deferred-20260926`에 작은 새 Git fixture를 만들었다. V:의 VSS 한도만 10GiB로 정했고 C:의 64GiB는 유지했다. 두 제품 workspace의 create/ensure, 상태 조회, release는 비관리자 CLI로 수행했다. Unity Editor를 실행하거나 checkpoint validation을 꾸미지 않았으며 source 변경은 fixture Git 커밋으로 보존한 뒤 release의 독립 결과 수집을 검증했다.

| 항목 | 결과와 증거 |
| --- | --- |
| 서로 다른 시작 시점 | [A](../../artifacts/storage-foundation/deferred-20260926/ensure-a.json), [B](../../artifacts/storage-foundation/deferred-20260926/ensure-b.json)의 서로 다른 VSS ID와 원래 source baseline 확인 |
| B 먼저 회수 | [응답](../../artifacts/storage-foundation/deferred-20260926/release-b-deferred.json)은 `Accepted`, `terminal=false`, `reclaim-pending`. A ID가 blocker이며 B 결과·Git 등록/delta/mount 정리와 snapshot 잔존을 구분 |
| 새 CLI의 상태/목록 | [status](../../artifacts/storage-foundation/deferred-20260926/status-b-pending.json)·[list](../../artifacts/storage-foundation/deferred-20260926/list-pending.json)가 같은 attempt와 대기를 유지. 조회로 삭제/재시도 없음 |
| A의 계속 작업 | B 대기 후 A baseline 읽기와 `result-A` 편집, [현재 source 검증](../../artifacts/storage-foundation/deferred-20260926/status-a-after-b.json)에서 변경 검출 |
| 명시적 회수 순서 | [A release](../../artifacts/storage-foundation/deferred-20260926/release-a.json) 뒤 [B release 재시도](../../artifacts/storage-foundation/deferred-20260926/release-b-retry.json)로 두 작업 `released`. 소유 프로세스/메모리/핸들 0, snapshot/mount/delta 부재 |
| 독립 결과/원본 | Git result ref에서 `result-A`/`result-B` 읽기, source HEAD·marker hash·clean 상태 유지, source 하나만 Git worktree 등록에 남음 |

판정은 [live-result.json](../../artifacts/storage-foundation/deferred-20260926/live-result.json)의 **PassedDefinedLive**다. B 대기 시 source volume 전체의 VSS used는 **7,815,168B**, allocated는 **35,012,608B**, limit은 **10,737,418,240B**였다. 이는 B에 귀속되는 비용이나 B 삭제 시 반환 바이트가 아니다. workspace 귀속은 unknown이다. 최종 [관리자 관측](../../artifacts/storage-foundation/deferred-20260926/probe-probe.json)은 모든 snapshot 0, C:/V:의 VSS UsedSpace/AllocatedSpace 0이다. 실제 NTFS 전체 파일 할당이나 RAM cache가 모두 0이라는 주장은 아니다.

## 권한 세션과 운영 마감

사용자가 반복 UAC의 불편을 지적한 뒤 남은 B 차단·A 해제·B 재시도를 **한 번 승인한 제한 관리자 세션**에서 처리했다. 정확한 두 소유 snapshot ID·helper hash·허용 Release 동작과 15분 만료를 고정했고 일반 CLI는 계속 비관리자였다. 마지막 probe/detach를 포함해 [4건을 처리하고 세션이 종료](../../artifacts/storage-foundation/deferred-20260926/release-session-stopped.json)됐다. [VHD 분리](../../artifacts/storage-foundation/deferred-20260926/detached.json) 후 fixture·독립 Git 결과·영수증은 VHD와 artifacts에 보존했다. UAC 설정 변경이나 제품 서비스 설치는 하지 않았다. 반복 UAC 없는 제품은 [UA 계획](../plans/storage-privilege-service.md)의 NotRun으로 남는다.

시험 harness에서 연결 전 V: 경로 조합, PowerShell 함수/alias 이름 충돌을 수정했다. 제품 snapshot 생성 전의 준비 오류였고 다른 snapshot을 삭제하거나 기존 source를 변경하는 복구를 하지 않았다. 해당 오류 파일도 artifacts에 남겼다.

## 후보와 제한

실환경 후보의 입력 141개 및 DLL hash는 [live-candidate-manifest.json](../../artifacts/storage-foundation/deferred-20260926/live-candidate-manifest.json)에 있다. 실환경 완료 후 helper 증거의 `deletionAttempted` 필드 누락을 false로 해석하지 않고 unknown으로 거부하도록 보강했고 회귀를 추가했다. 이 마지막 잘못된 입력 처리는 자동 시험으로 검증하며 실제 VSS 시험을 다시 실행했다고 표시하지 않는다. 최종 후보 hash·자동 시험은 별도 manifest/TRX로 보존한다.

이번 결과는 과거 SF 실행의 FailedPreservation을 소급 변경하지 않는다. SF-02 전체 Partial, SF-03~06 NotRun, 일반 비소유 백업 공존·외부 동시 삭제·실제 재부팅/전원/공간 장애·ARM A/B와 SW/UA 관문은 유지한다. 자동 sweep·다른 workspace 연쇄 회수·임의 완료 순서의 즉시 물리 회수는 제공하지 않는다.

최종 [표준 빌드](../../artifacts/storage-foundation/deferred-20260926/build-04.log)는 locked restore·Release build·자동 시험 **253개 중 252 통과·0 실패·선택적 ReFS 1 skip**이다. [최종 후보](../../artifacts/storage-foundation/deferred-20260926/candidate-manifest.json)와 [TRX](../../artifacts/storage-foundation/deferred-20260926/candidate.trx), [마감 영수증](../../artifacts/storage-foundation/deferred-20260926/final-closeout.json)에 범위와 후보 차이를 남겼다. 이 작업에서 commit/push는 하지 않았다.
