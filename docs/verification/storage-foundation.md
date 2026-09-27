# 공통 저장소 1차 구현·검증

실행일: 2026-09-25, Windows x64/NTFS, Unity 6000.3.22f1. **실행 단위 전체 판정은 `FailedPreservation`이다.** 파일 단위 변경 검증과 저장 계측은 구현했고 아래의 제한된 시험을 통과했지만, 시험 전 VSS 5개의 보존 조건을 충족하지 못했다. 독립 결과와 원본 소스는 보존됐으며 시험 소유 자원은 회수됐다. [최종 영수증](../../artifacts/storage-foundation/sf-20260925/final-closeout.json)을 따른다.

2026-09-26의 [후속 원인 재현·보호 시험](storage-recovery.md)에서 제어 JSON 게시 충돌을 수정하고, 격리 VSS에서도 최신/중간 삭제의 연쇄 소실을 재현했다. 비강제 직접 API도 같은 결과였고, 실제 제품 guard의 위험 요청 차단·오래된 순서 회수는 정의한 조건을 통과했다. 이 후속 결과는 아래 과거 실패를 통과로 바꾸지 않는다.

| 범위 | 판정 | 근거와 남은 조건 |
| --- | --- | --- |
| SF-01 파일 단위 소스 검증 | Passed: 정의한 소스 검증 범위 | 실제 반복 읽기·독립 oracle·같은 크기/mtime 검출, 자동 손상/경쟁/정책 변경, Unity loaded-code 연결 |
| SF-02 계측·저장 불변조건 | Partial | 초기 복사/읽기 hydration 0, 범위·반복 쓰기와 저널 잔존량·flush·재생·seal 계측. 압축·flush 정책 변경·압축 중단 복구는 NotRun |
| 실환경 보존·회수 | FailedPreservation | 소유 5개 workspace 회수와 결과 보존은 확인. 비소유 기존 VSS 5개가 B 해제 중 소실됨 |
| 새 VSS 해제 차단 | PassedAutomated / PassedDefinedLive | 9개 fake CIM 시험과 2026-09-26 격리 시험. 소유 sentinel의 위험 요청 2건 차단·12개 순차 회수. 일반 비소유 백업 공존은 미검증 |
| SF-03~06, NR/SW 전체 | NotRun 또는 기존 부분 상태 유지 | 단일 Editor·대형 PV 재측정·설치/binding/discard·재부팅/전원/물리 공간 고갈·ARM A/B를 실행하지 않음 |

## 기존 VSS 보존 실패와 현재 제한

사용자 승인으로 C:의 VSS 한도를 39,985,348,608B에서 68,719,476,736B(64GiB)로 늘렸다. [설정 직후 영수증](../../artifacts/storage-foundation/sf-20260925/vss-budget/vss-budget.json)은 기존 5개 ID의 보존을 확인했다. 한도를 축소하지 않았고 현재도 64GiB다.

그러나 23:24:56 KST의 B 해제 시각에 Windows가 `volsnap 95`를 5회 기록했고, [최종 VSS 조회](../../artifacts/storage-foundation/sf-20260925/final-vss.json)에서는 기존 5개와 소유 시험 snapshot 모두 없었다. [Windows 이벤트](../../artifacts/storage-foundation/sf-20260925/volsnap-events.json)의 메시지는 나중에 생성되어 삭제 대상으로 표시된 복사본을 삭제하기 위해 가장 오래된 복사본을 삭제했다는 내용이다. 소실 ID는 다음과 같다.

- `{E82CEB2E-AC2B-4FDF-ABE9-E6E4041FB724}`
- `{8D150A60-DBB3-4A18-A509-DF3E7648C50C}`
- `{1B6D14D7-E5FA-4951-B3B6-9E62F96F194D}`
- `{08B97BC9-DA94-483B-8536-CCC700DA0613}`
- `{EF908FCF-69D0-4BAD-837C-6B7792255316}`

당시 helper의 `Remove-CimInstance` 입력은 B의 정확한 ID `{E01D2739-E626-4B9E-82EA-951EDB93B04A}`였다. 전역 삭제·한도 축소 호출은 없었지만, 정확한 ID만 삭제한다는 구현으로 provider의 다른 snapshot 보존까지 보장할 수 없었다. 이벤트의 삭제 연쇄와 시각은 확인됐고 내부 provider의 전체 원인은 확정하지 않았다. 삭제된 기존 snapshot을 복원한 증거는 없다. Microsoft도 [삭제된 shadow copy의 복구 불가](https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/vssadmin-delete-shadows)를 명시한다. 이 손실이나 VSS 할당 감소를 공간 절감 성과로 계산하지 않는다.

현재 helper는 같은 볼륨에 다른 오래된/동일 시각/시각 불명 snapshot이 있으면 `SnapshotReleaseIsolationRequired`로 삭제 전에 차단한다. 볼륨 정보 불명도 차단하며 `release-isolation.json`에 대상·관측 ID·차단 ID·삭제 시도 여부를 남긴다. 허용된 정확한 ID 삭제 뒤에도 다른 ID가 사라지면 `SnapshotCollateralLoss`로 중단한다. 차단을 통과하려고 기존 snapshot을 삭제하지 않는다. A가 먼저 생성된 작은 A/B의 A→B 순서와 달리, 오래된 비소유 snapshot이 있는 볼륨의 회수는 보존 상태로 남을 수 있다.

이 보완은 fake CIM에서 삭제 호출 0, 정확한 ID만 삭제, 관측 실패 보존, 다른 ID 소실 탐지를 확인했다. 후속 [격리 볼륨 시험](storage-recovery.md)에서도 위험 요청 차단과 오래된 순서의 실제 회수를 확인했다. OS의 모든 자동 삭제나 동시 생성 경쟁을 방지하는 검증은 아니다. 다음은 늦게 끝난 작업의 결과 보존과 snapshot 회수 시점을 분리하는 계약이며, 차단을 해제하거나 임의 순서의 즉시 회수를 지원한다고 표시하지 않는다.

## 후보와 자동 시험

기준 커밋은 `development`의 `30ce3075bb377e968931a6bc53dde77c4065673f`이며 미커밋 후보로 실행했다. 관련 없는 기존 계획 변경은 유지했고 commit/push는 하지 않았다.

- 변경 전 기준: [baseline TRX](../../artifacts/storage-foundation/sf-20260925/baseline.trx), 207개 중 206 통과·0 실패·선택적 ReFS 1 skip.
- 실제 소스 비용·최종 Unity 수집 후보: [live manifest](../../artifacts/storage-foundation/sf-20260925/live-candidate-manifest.json), [live TRX](../../artifacts/storage-foundation/sf-20260925/live-candidate.trx), 218개 중 217 통과·0 실패·1 skip. 범위 저장·초기 Unity 단계의 이전 후보와 재개·수정 과정도 원시 기록에 보존한다.
- 2026-09-25 마감 당시 VSS 차단 후보: [candidate manifest](../../artifacts/storage-foundation/sf-20260925/candidate-manifest.json), [최종 build](../../artifacts/storage-foundation/sf-20260925/implementation-build-14.log), [TRX](../../artifacts/storage-foundation/sf-20260925/candidate.trx), 227개 중 226 통과·0 실패·1 skip. locked restore·Release build·자동 시험을 수행했다. 당시 추가 guard 후보의 실제 VSS 실행은 하지 않았으며 새 후보의 후속 검증은 [별도 기록](storage-recovery.md)을 따른다.

자동 시험은 동일 크기/mtime의 dirty 판정과 실제 커밋 내용, 변경 없는 큰 파일을 열기 금지로 잠근 상태의 재사용, 독립 raw oracle, source set/attributes/EOL 변화, filter 거부, `.meta`의 validation 무효화, 경로 변경·provider 세대·손상·관측 unknown·경쟁, 정확한 audit 복구 명령과 기존 수집 보존 회귀를 포함한다.

## 반복 소스 읽기와 초기 비용

시험 소스는 무작위 16/256/1,024MiB 파일, 10B 코드, 30B attributes의 5파일이며 총 raw 길이는 1,358,954,536B다. 별도 fixture 준비와 Git 객체 작성은 제품의 최초 payload 복제로 집계하지 않는다. 동일 크기/mtime을 유지한 코드 내용 변경 뒤 명시적 검증을 3회 수행했다.

| 관측 | 변경 전 | 변경 후 |
| --- | --- | --- |
| 명령 경과 시간 3회 | 41.306 / 33.636 / 36.097초 | 3.618 / 3.624 / 3.697초 |
| provider 경로 읽기 증가 | 각 1,359,348,024B | 1,252 / 1,134 / 1,134B |
| raw payload 재해시 | 매회 전체 소스 | 10 / 0 / 0B |
| canonical Git hash 입력 | 별도 계수 없음 | 10 / 0 / 0B |
| 같은 크기/mtime 코드의 dirty 판정 | 잘못된 clean | `hasChanges=true`, `changedPaths=[code.cs]` |

원시 증거는 [변경 전](../../artifacts/storage-foundation/sf-20260925/baseline-source-cost.json), [변경 후](../../artifacts/storage-foundation/sf-20260925/candidate-final-source-cost.json), [독립 Python 전체 읽기 oracle](../../artifacts/storage-foundation/sf-20260925/candidate-final-oracle.json)이다. 모든 raw fingerprint는 `179d39064f8a0b9a0a6b55d3e92e57c5adbbc259bf877333824269bdf1e4641d`로 일치했다. 후속 0B는 소스 payload 재해시이며 attributes/control 읽기와 Git 메타데이터 열거까지 0이라는 뜻은 아니다. [미수집 변경의 release 거부](../../artifacts/storage-foundation/sf-20260925/same-mtime-release-refusal.json)도 실제 확인했다.

최초 확인 비용은 줄지 않았다. 캡처 helper 이후 동일 ensure는 변경 전 91.581초, 최종 소스 후보의 새 작업은 95.856초였다. 새 작업의 첫 manifest 작성에는 raw와 Git hash 입력 각각 1,358,954,536B, 57.424초, manifest 1,470B가 들었다. 응답 직전 두 번째 검사가 0B여도 [초기 inspection 기록](../../artifacts/storage-foundation/sf-20260925/retained-records/final-cold/source-inspections.jsonl)에 전체 비용을 남긴다. 명시적 감사·provider 재시작도 전체 읽기를 요구한다. 중간 후보의 느린 audit 기록을 최종 후보 최초 비용으로 대체하지 않았다.

표본은 각 3회이고 배경 Unity 활동·OS cache가 통제된 벤치마크가 아니다. 시간은 관측값이며 일반 배속 보장이 아니다. 독립 oracle은 다른 읽기 크기를 사용하므로 그 실행 시간을 제품 최초 읽기의 성능으로 비교하지 않는다.

## 범위 저장·누적 저널

[실제 mount 범위 시험](../../artifacts/storage-foundation/sf-20260925/range-exercise.json)과 `allocation-range-*.json`은 논리 payload·저널 길이·FILE_STANDARD_INFO 할당을 구분한다.

| 단계 | 누적 payload B | 저널 논리 B | delta 파일 할당 B |
| --- | ---: | ---: | ---: |
| 쓰기 전 / 반복 읽기 후 | 68 / 68 | 2,419 / 2,419 | 4,096 / 4,096 |
| 16MiB에 4KiB 쓰기 | 4,164 | 6,937 | 8,192 |
| 256MiB에 4KiB 쓰기 | 8,260 | 11,459 | 16,384 |
| 1,024MiB에 4KiB 쓰기 | 12,356 | 15,985 | 16,384 |
| 같은 4KiB에 100회 추가 쓰기 | 421,956 | 468,695 | 471,040 |
| 16MiB 전체 재쓰기 | 17,199,172 | 17,252,743 | 17,256,448 |
| truncate / rename / delete 후 | 17,199,172 | 17,253,157 / 17,253,580 / 17,253,991 | 각 17,256,448 |

생성 payload 복사와 read cache는 0이다. 100회 쓰기는 payload 409,600B를 추가했고 601.717ms, commit +100, flush +200(호출자의 flush 포함)이었다. 삭제·truncate는 이전 저널 공간을 회수하지 않는다. 기존 내구성·flush 정책은 유지했다.

운영자가 provider를 중지한 뒤 같은 snapshot/delta로 다시 연결해 acknowledged 범위를 재확인했다. [재생 바이트 확인](../../artifacts/storage-foundation/sf-20260925/range-replay-bytes.json)과 원본 해시 일치를 보존했다. 이는 제어된 재연결이며 전원 중단·재부팅·실제 디스크 고갈 시험이 아니다.

seal/회수 검증의 전체 저널 읽기도 남는다. 보존된 `storage-reads.jsonl` 합계는 합성 후보 6회 204,305,838B, Unity A 7회 1,041,877,059B, B 8회 2,088,153,166B다. 재시도 포함 누적값이며 B의 provider 재시작 전후 epoch 계수를 단순 차감하지 않는다. [전체 장부](../../artifacts/storage-foundation/sf-20260925/final-closeout.json)에 원시 기록 경로와 시간을 연결했다.

## 추가 할당 장부의 범위

최종 소스 후보의 새 합성 workspace는 생성 직전 대비 control/delta +68,288B, runner +631,128B, 공유 Git +1,032B였다. 같은 단계의 VSS 볼륨 used는 +84,656,128B, allocated는 +423,968,768B였으나 다른 작업·원본 쓰기를 포함하므로 해당 workspace 비용으로 귀속하지 않는다. 이전 후보를 여러 번 실행한 home의 runner 전체를 새 workspace 비용으로 계산하지 않았다.

Unity의 원본 Library 1,874,456,304B는 기준선으로 제외한다. 회수 뒤에도 control 기록 887,000B, runner 1,254,064B, 공유 Git의 기준선 대비 +18,360B가 남았다. 독립 결과·기록 보존 비용이며 delta/Editor 잔존으로 오해하지 않는다. Library 쓰기는 delta 안에 포함돼 다시 더하지 않는다.

`unity-both-edited` 표본은 A checkpoint 시작과 겹쳤으므로 정지된 동일 종료점의 비교에 사용하지 않는다. 모든 표본은 디렉터리별 순차 관측으로 원자적 순간 합계가 아니며 MFT·커널 cache·표본 사이 최고치·계측 root 밖 로그/임시 파일은 완전히 계측하지 못했다. `totalAttributableBytes`와 총 절감률은 null로 남긴다. 특히 기존 VSS 소실 후의 볼륨 사용량 감소는 정상 회수나 절감 근거가 아니다.

## 실제 Unity A/B와 회수

기존 작은 fixture의 `2e85e3fe1b3be036cb826749b59159c0ff2737da`에서 준비한 별도 `C:\sf25\u`를 사용했다. 소스는 90파일·6,235,231B이고 모델/텍스처/씬을 포함한다. 원본 기준 HEAD는 `58f81b5bc6ff8a940d8a90872bd3fed858a54cc1`이다. 대형 Medieval pack·C/Q 비교군·Coordination은 사용하지 않았다.

| 작업 | 실제 loaded-code 값 | 최종 독립 결과 |
| --- | --- | --- |
| A `657cf6764c59f9cea7b132de` | 13 → 23 → 새 CLI 재개 후 24 | `3dfb5fe0bb185038b1c45810d1d461ca8e47d2e2` |
| B `0d0eb2cd8851a4f98ead1b54` | 13 → 33 → A 회수 뒤 편집 34 | `7fcbf2d1b91b432056d53655723d96d22e7b2498` |

각 값에 정확한 PID·작업 경로·domain generation·assembly MVID와 현재 validation fingerprint를 연결했다. A 회수 뒤 B가 아직 checkpoint되지 않은 상태에서 편집을 계속한 다음 최종 수집했다. 독립 ref의 실제 코드도 A +21, B +31로 다시 확인했다. [dirty 원본 통합 거부](../../artifacts/storage-foundation/sf-20260925/dirty-original-guard.json)는 HEAD·index·dirty 내용·독립 ref를 보존했고 시험용 dirty 내용만 원래 bytes로 복원했다.

B의 첫 release는 `OverlayFlushUncertain`으로 실패했다. provider의 `UnauthorizedAccessException`과 완성된 flush 결과 임시 파일을 보존했으나 정확한 발생 지점/원인은 확정하지 않았다. 같은 key의 ensure, 원래 VSS ID의 Observe, 저널 재생으로 연결한 뒤 동일 결과/fingerprint를 확인해 회수를 재개했다. 재생은 261,019,985B였고 source rebuild는 raw/Git 각각 6,235,231B였다. 이 운영 복구를 무개입 성공으로 기록하지 않는다.

기준 합성·변경 합성·최종 초기 비용·Unity A/B의 5개 workspace는 모두 released이며 독립 ref는 읽을 수 있다. 제품 영수증에서 소유 프로세스/메모리/핸들 0, mount·delta·소유 snapshot 부재를 확인했다. [OS 종료 확인](../../artifacts/storage-foundation/sf-20260925/process-closeout.json)은 소유 Editor/provider 및 계측 helper의 종료를 확인한다. 두 시험 원본은 clean이고 worktree 등록은 원본 하나씩이다. 형제 canonical Unity 저장소도 변경하지 않았다. **이 소유 자원 회수 성공과 기존 비소유 VSS 보존 실패는 별개 판정이다.**

현재 계약은 [변경 기반 소스 검증](../source-verification.md), 남은 범위는 [공통 저장소 1차](../plans/storage-priority-and-editor-modes.md)와 [NR 지원 관문](../plans/ntfs-recovery-and-support.md)을 따른다.
