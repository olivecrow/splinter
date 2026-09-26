# SF-02 명시적 저널 정리의 중첩량 예산

별도 [BQ-01~05 제품 검증과 반복 저장량 판단](journal-budget-and-growth-qualification.md)은 [정의한 범위에서 완료](../verification/journal-budget-and-growth-qualification.md)했다. 실제 VSS/WinFsp 제품 경로의 낮은 예산 거부·저장 예산 유지·같은 작업 재시도와 작은 Unity의 수정 3회·후속 저장량을 확인했다. 아래 PB의 당시 자동 범위와 BQ의 실환경 증거를 구분한다.

상태: **PB-01~04의 정의한 자동 범위 완료; 당시 미실행한 제품 경로는 후속 BQ의 제한된 실환경 증거를 참조**. [실행 판정](../verification/journal-compaction-peak-admission.md)을 따른다. [QJ 공간 계측](../verification/journal-compaction-qualification-and-cost.md)에서 작은 Unity 작업은 정리 직전 저널 할당 1,941,647,360B, 새 후보 1,646,452,736B가 동시에 존재했다. 정리 중 소유 추가 파일 할당의 관측 최고치는 3,590,531,672B였고 재개 작업 후 다시 증가했다. 이 범위는 큰 Unity 지원이나 용량 목표 달성을 선언하기 전에, 호출자가 명시한 저널 두 세대의 허용 중첩량을 정리 후보 작성 전에 적용한다.

| ID | 범위와 완료 조건 |
| --- | --- |
| PB-01 계약 | 기존 `workspace compact`에 선택적 `--max-estimated-journal-overlap-bytes <양의 정수>`를 연결한다. 값이 없으면 기존 정리 동작을 유지한다. 입력 schema, Core port, Runtime, Windows backend, CLI와 현재 계약 문서를 함께 갱신한다. 값은 작업 생성 때 고정되는 journal 크기 상한과 별개다. |
| PB-02 게시 전 계산 | provider가 정지한 뒤 현재 저널의 논리 길이와 살아 있는 범위로 정확히 계산한 후보의 예상 논리 길이를 더한다. 합계가 지정 예산을 초과하면 후보 파일·새 게시를 만들기 전에 `CompactionOverlapBudgetExceeded`와 구성 바이트를 반환한다. 예산 확인 전 checksum/seal·상태 hash를 검증하고, 실패 시 이전 저널·sequence·snapshot·Git 결과를 보존한다. 이 값은 저널 파일 길이의 예상 중첩량이며 NTFS 할당·MFT·control·Git·VSS를 포함한 물리 peak 상한이 아니다. |
| PB-03 재시도 | 중단 상태와 최초 예산을 저장한다. 같은 ID/home의 재시도에서 예산 인자를 생략하면 저장된 예산을 유지하며, 명시한 새 값으로만 변경한다. 후보가 이미 발행된 뒤에는 더 낮은 예산으로 완료/복구를 가로막지 않는다. 성공한 정리 뒤 pending 예산을 지운다. 예산 거부 후 작업공간이 `resume-compaction` 상태라면 다른 작업을 시작하지 않고 같은 작업의 명시적 재시도를 요구한다. |
| PB-04 검증 | 합성 반복 쓰기의 저널에 낮은 예산을 적용해 후보·게시 없음과 원본 저널 seal/sequence 불변을 확인하고, 큰 예산의 동일 작업 재시도로 내용·seal·회수를 확인한다. fake coordinator와 응답 schema 적합성 시험, 전체 `pwsh ./scripts/build.ps1`을 실행한다. 실제 Unity/VSS는 이 PB 단위에서 실행하지 않았고 후속 BQ로 분리했다. 대용량 PV를 다시 실행하지 않는다. |

예산은 사용자가 해당 호출에 설정한다. 절감률이 낮다는 이유만으로 기존 명시적 정리 동작을 자동 거부하지 않는다. 예산 거부는 저장 공간 부족의 일반 해결책이나 자동 sweep이 아니며, 물리 할당과 공유 VSS의 실측을 대체하지 않는다. `stable-journal-v1` 결과 보존, 같은 snapshot 재개, `reclaim-pending`, 기존 [SF 실행의 FailedPreservation](../verification/storage-foundation.md)과 SF-02 전체 Partial 판정을 유지한다. flush 정책 변경, 단일 Editor, 새 서비스·설치·binding/discard는 별도 후속이다.
