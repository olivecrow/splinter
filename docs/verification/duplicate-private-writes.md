# 동일 private 쓰기의 저장량과 복구

실행일: 2026-10-02, Windows x64/C: NTFS. 이 기록의 범위는 기존 private 저널로 완전히 덮인 요청의 바이트가 같을 때 payload를 재사용하는 것이다. `metadata` frame·개별 durable flush를 사용하며 Core port·CLI/schema·저널 버전은 유지했다. 당시 원본만 참조하는 범위·hole·확장·다른 바이트는 기존 payload 쓰기였다. 이후의 부분 변경 앞뒤 재사용은 [별도 검증](resource-efficiency-followup.md)을 따른다. [현재 계약](../ntfs-workspaces.md#동일한-private-범위의-반복-쓰기)과 [계측 의미](../source-verification.md#live-write-비용)를 따른다.

## 동일 입력의 실제 NTFS 할당

기준 `93080be`의 Runtime과 수정한 Runtime 바이너리를 고정했다. 새 소유 디렉터리 각각에서 파일 하나의 동일 64KiB 범위를 32회 썼다. 요청 내용은 고정이고 snapshot payload를 읽으면 실패하는 합성 불변 트리다. ID·시각에 따른 저널 hash 차이는 내용 동등성으로 해석하지 않으며 현재 파일·재생 내용과 metadata·sequence를 대조했다. [수정 전](../../artifacts/storage-foundation/duplicate-write-20261001-235827-526/before.json)과 [수정 후](../../artifacts/storage-foundation/duplicate-write-20261001-235827-526/after.json)를 비교한다.

| 관측 | 기존 Runtime | 수정 Runtime |
| --- | ---: | ---: |
| 요청한 누적 바이트 | 2,097,152 | 2,097,152 |
| 기록한 payload B | 2,097,152 | 65,536 |
| 저널 논리 B | 2,110,307 | 78,784 |
| 실제 NTFS 저널 파일 할당 B | 2,113,536 | 81,920 |
| durable sequence / commit 수 | 33 / 33 | 33 / 33 |
| durable flush 수 | 34 | 34 |
| 비교 때문에 추가한 저널 읽기 B | 0 | 2,031,616 |

이 반복 입력의 파일 할당은 **2,031,616B, 약 96.1% 감소**했다. 요청한 바이트가 사라진 것이 아니라 처음 승인된 payload를 재사용하고 31개의 새 metadata commit을 남겼다. 마지막 명시적 flush까지 포함한 수는 동일하다. 현재 내용 hash는 두 실행 모두 `4b640d85ab3ba30fd02c9fc9db4a8928f416322ad27022ea58a65aaee68a4df2`이고 snapshot 읽기·최초 payload 복사·read cache는 0이다. 파일 할당은 `FILE_STANDARD_INFO.AllocationSize`이며 디렉터리/MFT·VSS·Git·전체 작업공간 peak를 포함하지 않는다.

## 작은 실제 WinFsp client

[수정 Runtime의 로컬 mount](../../artifacts/storage-foundation/duplicate-write-20261001-235827-526/native-after.json)에서 같은 입력을 `FileStream.Write`·`Flush(true)` 32회로 실행했다. 클라이언트 읽기와 unmount 후 재생 내용·metadata·sequence가 일치했다. sequence 33, payload 65,536B, journal 78,784B, 파일 할당 81,920B이며 소유 핸들/retained context 0·정상 mount 부재와 callback 오류 0을 확인했다. client의 명시적 flush가 포함돼 총 flush는 66회다. 일반 사용자 권한(`callerElevated=false`)으로 기존 WinFsp만 사용했다.

이것은 **실제 WinFsp/fake 불변 기준의 한정 쓰기 실행**이다. VSS·제품 workspace CLI/Git·Unity·VIVOBOOK·mmap·강제 종료·전원/재부팅·물리 공간 장애는 실행하지 않았다. 이전 Unity/Library 결과를 새 Runtime의 자격으로 승계하지 않는다. 같은 바이트의 비중과 비교 읽기 비용은 실제 프로젝트에서 미측정이며 위 96.1%를 Library나 제품 전체 절감률로 사용하지 않는다.

## 자동 회귀와 완료 기준

`JournalDuplicateWriteTests` 8건 중 기존 코드에서 4건이 실패했다. 수정한 8건은 통과했다. 여러 private extent를 가로지른 일치·재연결 후 부분 범위 재사용, metadata·경로 sequence·flush, base/hole/확장/바이트 불일치, 작은 예산의 동일 쓰기 허용과 실제 새 payload 거부를 확인한다. metadata header/flush 뒤의 예외 주입은 기존 payload 보존과 각각의 tail/완결 frame 복구를 검사한다. 실제 프로세스/전원 종료로 읽지 않는다. [수정 전 TRX](../../artifacts/storage-foundation/duplicate-write-20261001-235827-526/before-tests.trx)와 [수정 후 TRX](../../artifacts/storage-foundation/duplicate-write-20261001-235827-526/after-tests.trx)를 보존한다.

첫 전체 회귀의 cache 정리 시험 8건은 동일 payload 32회가 무효 기록을 만든다는 입력 가정에서 실패했다([TRX](../../artifacts/storage-foundation/duplicate-write-20261001-235827-526/automated-first.trx), [진단](../../artifacts/storage-foundation/duplicate-write-20261001-235827-526/first-run-failure.json)). 그 fixture의 첫 바이트만 매회 바꿔 실제 무효 payload를 만들고 최종 기대 내용 42·8개 중단 경계·동일 요청 복구·범위/예산·보존 검증 조건을 유지했다.

필수 `pwsh ./scripts/build.ps1`의 최종 실행은 locked restore·Release 빌드 경고/오류 0, 자동 시험 **765건 중 764 통과·0 실패·당시 제외 시험 1 skip**이다. [build 로그](../../artifacts/storage-foundation/duplicate-write-20261001-235827-526/build.log), [최종 TRX](../../artifacts/storage-foundation/duplicate-write-20261001-235827-526/automated.trx), [현재 source/binary hash와 판정](../../artifacts/storage-foundation/duplicate-write-20261001-235827-526/result.json)을 보존했다. 동일 쓰기·observer·정리/재개·cache 정리의 현재 회귀도 통과했다.

이 단위는 **scoped-complete-with-limitations**다. 완료 조건은 위 공간·개별 flush·내용/metadata·예산·복구와 기존 자동 회귀 통과다. SF-02 전체의 Partial·대형/일반 Unity 전체 절감 unknown은 유지한다.

[별도 작은 Unity warm 비교](unity-duplicate-private-writes.md)는 실제 제품 CLI/VSS/WinFsp/Editor에서 같은 편집·재개·결과 보존/release를 확인한다. 그 입력의 종료 직전 저널 할당 차이는 작으며, 이 합성 단위의 96.1%나 일반 Unity/대형 절감률을 승계하지 않는다.
