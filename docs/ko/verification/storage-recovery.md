# 저장소 실패 원인 재현과 보호 검증

[English](../../verification/storage-recovery.md)

실행일: 2026-09-26, Windows x64/NTFS. [환경 기록](../../../artifacts/storage-foundation/recovery-20260926/environment.json)은 OS 10.0.26200.0, volsnap 10.0.26100.8875, vssapi 10.0.26100.9278이다. [전날 실행](storage-foundation.md)의 `FailedPreservation`은 그대로 유지한다. 이번 범위는 제어 JSON 게시 실패의 수정과 VSS 연쇄 삭제의 격리 재현·삭제 보호 검증이다. Unity·대형 PV 비교·저널 압축·flush 정책 변경은 실행하지 않았다.

## 제어 파일 접근 거부

실제 `WindowsProcessWorkspaceProvider.WriteAsync`를 호출해 기존 B의 실패와 같은 `UnauthorizedAccessException`, `0x80070005`, 완성된 임시 JSON 잔존을 재현했다. 목적 파일을 `FileShare.ReadWrite | FileShare.Delete`로 연 정상 독자가 있는 동안에도 `File.Move(temporary, path, true)`는 실패했다. [수정 전](../../../artifacts/storage-foundation/recovery-20260926/ipc-before.json), [교체 API 대조](../../../artifacts/storage-foundation/recovery-20260926/ipc-replace.json), [수정 후 제품 함수](../../../artifacts/storage-foundation/recovery-20260926/ipc-after.json)를 비교한다.

| 조건 | 기존 Move(overwrite) | 수정 후 Replace |
| --- | --- | --- |
| 독자 없음 | 성공 | 성공 |
| delete sharing을 허용한 독자 | 접근 거부 5 | 성공, 기존 독자는 이전 JSON·새 독자는 새 JSON |
| delete sharing을 허용하지 않은 독자 | 접근 거부 5 | 공유 위반 32, 기존 JSON·완성된 임시 파일 보존 |
| 읽기 전용 목적 파일 | 접근 거부 5 | 접근 거부 5, 속성·이전 내용 보존 |
| 이전 파일 삭제 후 독자 핸들만 남음 | 성공 | 성공 |

Runtime의 `AtomicFilePublication`은 완성·flush한 같은 디렉터리의 임시 파일을 기존 파일에는 `File.Replace`, 최초 게시에는 덮어쓰기 없는 `File.Move`로 게시한다. 제어 IPC와 workspace/baseline 저장이 사용하고 VSS PowerShell 영수증에도 같은 동작을 적용했다. 먼저 삭제하거나 보이는 파일에 복사하는 우회, 접근 속성 변경, 무제한 재시도는 추가하지 않았다. 실패 파일은 복구를 위해 남긴다. provider 실패 기록에는 HRESULT·stack·게시 경로·임시 경로를 추가했다. 저널의 acknowledged-write·flush/seal 계약은 바꾸지 않았다.

자동 회귀는 열린 정상 독자와 **100회 게시**, 공유 위반 후 보존·재시도, 읽기 전용 파일, 실제 WorkspaceStore 저장, 실제 PowerShell `Save-Json`을 확인한다. 최초 시험에서 발견한 PowerShell의 `$null`→빈 문자열 바인딩은 `NullString.Value`로 수정했다. 실패한 [첫 빌드](../../../artifacts/storage-foundation/recovery-20260926/build-01.log)도 보존한다.

이로써 제품 자체의 polling reader와 게시 방식의 충돌은 재현·수정했다. 전날 오류 순간의 핸들 소유자는 기록되지 않았으므로 **과거 단일 사건의 프로세스까지 특정한 것은 아니다**. 이번에는 실제 Unity/WinFsp 장기 동시 실행을 반복하지 않았다.

## VSS 연쇄 삭제

새 동적 16GiB NTFS VHDX의 `V:`에 합성 파일만 만들었다. 시작 시 모든 볼륨의 VSS 목록이 비어 있음을 확인하고, 매 생성·삭제 직전에 시험 볼륨 GUID와 기록된 소유 ID 외의 snapshot이 있으면 중단했다. C:의 64GiB 한도와 원본 Unity/Git에는 쓰지 않았다. 이 볼륨은 재현용이며 제품의 원본 이관 요구사항이나 별도 드라이브 준비 요건이 아니다.

| 입력·삭제 경로 | 최신부터 | 오래된 순서 | 중간부터 |
| --- | --- | --- | --- |
| 4시점 × 4MiB, CIM | 다른 ID 소실 0 | 0 | 0 |
| 12시점 × 128MiB, CIM | 가장 오래된 다른 ID 1개 소실 | 0 | 가장 오래된 다른 ID 1개 소실 |
| 12시점 × 128MiB, 직접 VSS API, `bForceDelete=false` | 가장 오래된 다른 ID 1개 소실 | 0 | 가장 오래된 다른 ID 1개 소실 |

각 시점에 동일 경로를 새로운 무작위 바이트로 다시 썼다. 삭제 후 남은 각 snapshot의 파일을 직접 해시해 해당 시점의 값과 비교했다. 증거는 [작은 순서 시험](../../../artifacts/storage-foundation/recovery-20260926/matrix-result.json), [누적 변경/CIM](../../../artifacts/storage-foundation/recovery-20260926/churn-result.json), [비강제 직접 API](../../../artifacts/storage-foundation/recovery-20260926/nativefalse-result.json)이다. CIM 시험의 중간 순서는 시험 스크립트의 인덱스 계산 오류로 삭제 전에 멈췄고, 기록한 같은 12개 ID에서 재개했다. [오류 기록](../../../artifacts/storage-foundation/recovery-20260926/churn-harness-error.json)을 보존한다.

다른 ID가 사라진 네 번 모두 `volsnap 95`가 기록됐다. CIM 시험의 관측 최고 VSS UsedSpace는 6,701,056B로 4GiB 한도에 훨씬 못 미쳤다. 이 수치는 VSS가 보고한 diff-area 사용량이며, 합성 파일 전체 물리 비용이나 숨은 지연 회수량의 측정값이 아니다. 이 재현에서는 용량 한도 도달이 필요 조건이 아니었다.

직접 API도 `S_OK`, `deleted=1`, `notDeleted=GUID_NULL`을 반환한 직후 대상 외의 오래된 ID가 사라졌다. [실험 C++](../../../artifacts/storage-foundation/recovery-20260926/delete-lab-snapshot.cpp)는 이번 시험 볼륨 GUID 이외의 삭제를 거부하며 제품에 포함하지 않았다. Microsoft의 [DeleteSnapshots 계약](https://learn.microsoft.com/en-us/windows/win32/api/vsbackup/nf-vsbackup-ivssbackupcomponents-deletesnapshots)은 강제 여부와 반환 개수를 설명하지만, 이 실측처럼 다른 ID 보존은 전후 목록으로 따로 확인해야 한다. WMI 대신 직접 API를 사용하거나 강제 옵션만 끄는 것은 이 결함의 해결책이 아니었다.

확인한 결함은 **뒤쪽 snapshot의 개별 삭제를 안전한 독립 자원 회수로 취급한 제품 가정**이다. Windows provider가 나중에 삭제 표시된 복사본을 처리하면서 오래된 복사본까지 제거하는 동작을 동일 OS의 격리 볼륨에서 재현했다. 드라이버 내부의 정확한 임계값·전체 발동 조건은 확정하지 않았다. 4시점 시험의 성공을 안전한 상한으로 사용하지 않는다.

## 실제 보호 함수와 남은 제품 제약

실제 제품 helper의 `Invoke-OwnedSnapshotRemoval`과 `Save-Json`을 그대로 불러 12시점 × 128MiB 조건을 검증했다. 최신·중간 삭제 요청 2건은 `SnapshotReleaseIsolationRequired`, `deletionAttempted=false`였고, 전후 12개 ID와 각 시점의 파일 해시가 유지됐다. 이후 오래된 순서의 12개 회수는 대상 외 소실 0·생존 파일 hash 불일치 0·volsnap 이벤트 0으로 완료됐다. [실제 보호 시험](../../../artifacts/storage-foundation/recovery-20260926/guard-result.json)의 판정은 **PassedDefinedLive**다. 모든 sentinel은 이 시험에서 만든 소유 합성 snapshot이며 일반 백업 프로그램의 비소유 snapshot과 동시 작동한 시험은 아니다.

이 보호는 더 오래된/동일 시각/시각 불명 snapshot이 있는 동안 뒤쪽 snapshot을 삭제하지 않는다. 오래된 작업이 여전히 실행 중이면 나중에 끝난 작업의 snapshot은 남을 수 있다. 기존 snapshot이나 타 작업을 대신 삭제해 이 제약을 우회하지 않는다. `release`가 snapshot까지 회수했다고 성공으로 표시해서도 안 된다.

따라서 원본 위치 유지·전체 복제 없음·범위 저장·변경 파일만 검증하는 기능 개발이 불가능해진 것은 아니다. 다만 **임의 완료 순서와 즉시 물리 회수를 동일한 동작으로 보장할 수 없다**. 후속 저장소 설계는 독립 결과 보존과 snapshot의 삭제 가능 시점을 분리하고, 오래된 활성 작업·비소유 snapshot 때문에 남은 공간을 관측해야 한다. 지연 회수의 상태/재시도/후보별 공간 비용을 정한 뒤 SF-02 압축과 SF-03 Editor 후보 연결을 진행한다. 자동 지연 회수 큐나 공유 기준 수명주기는 이번 구현에 포함하지 않았다.

## 후보와 마감

기준은 `development`의 `30ce3075bb377e968931a6bc53dde77c4065673f`이고 미커밋 후보다. [후보 manifest](../../../artifacts/storage-foundation/recovery-20260926/candidate-manifest.json)에 구현 입력 138개와 빌드 DLL 해시를 남겼다. [최종 빌드](../../../artifacts/storage-foundation/recovery-20260926/build-02.log)는 locked restore·Release build·전체 자동 시험 **232개 중 231 통과·0 실패·당시 제외 시험 1 skip**이며 [TRX](../../../artifacts/storage-foundation/recovery-20260926/candidate.trx)를 보존한다.

시험 자원 마감은 [최종 영수증](../../../artifacts/storage-foundation/recovery-20260926/final-closeout.json)을 따른다. 합성 snapshot 96개 중 재현 과정에서 연쇄 삭제된 4개를 포함해 남은 snapshot은 0이다. 보호 시험의 생존 파일 hash 대조는 90건 모두 일치했다. [최종 VSS 조회](../../../artifacts/storage-foundation/recovery-20260926/lab-probe.json)에서 C:/V:의 UsedSpace·AllocatedSpace는 0이고 C: 한도는 64GiB다. [시험 VHD 분리](../../../artifacts/storage-foundation/recovery-20260926/detached.json)를 완료했고 합성 파일을 담은 VHDX와 원시 증거는 보존했다. 원본 Unity fixture는 clean이고 수정하지 않았다. 기존 작업 변경을 유지했으며 commit/push는 하지 않았다. 이전 `FailedPreservation`, SF-02 Partial, SF-03~06 NotRun 및 NR/SW 지원 경계는 이 재현 시험으로 승격하지 않는다.
