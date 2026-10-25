# 영속 flush와 종료 처리량 실험

[English](../../verification/durable-flush-throughput.md)

**DF-01–05, 2026-10-24: 제품 최적화 채택 없이 검토 단위를 마쳤다.** [범위](../plans/durable-flush-throughput.md)는 큰 시간 절감을 우선하여 flush/종료 후보를 구현하라는 사용자 요청을 따른다. 기준은 `663c400`이다. bounded checkpoint RAM 후보를 구현·시험했지만 성능 기준을 충족하지 못해 제거했다. 현재 제품 소스/시험은 기준과 같다. [데이터셋](../../../reports/benchmarks/durable-flush-throughput-20261024.json)에 개별 측정·runtime/소스 hash·고정 입력 근거를 남긴다. `artifacts/durable-flush-20261024/`의 원시 자료·고정 runtime·제외한 소스는 제품 backend나 새 CLI 진입점이 아니다.

## DF-01 — 개별 쓰기 대안

각 NTFS probe는 새 소유 임시 파일에 같은 576-byte payload를 2,048번 쓰고 매번 선택한 완료 연산을 기다린 뒤 닫고 모든 기록을 대조한다. 표의 각 방식은 정방향/역방향으로 2회 측정했다. 생성·쓰기·완료·닫기는 시간에 포함하고 내용 재검사는 제외한다. 최초 네 방식 비교는 별도 보존한다. 아래 확장 비교와 data-sync 비교는 각각 자체 기준 측정을 사용한다.

| 비교 | 방식 | 평균 초 | 논리적 파일 길이 | 판단 |
| --- | --- | ---: | ---: | --- |
| 확장 | Buffered `Flush(true)` | 4.365 | 1,179,648 B | 현재 방식 유지 |
| 확장 | 선할당 + `Flush(true)` | 4.347 | 1,179,648 B | 큰 절감 없음 |
| 확장 | Write-through + `Flush(true)` | 4.360 | 1,179,648 B | 큰 절감 없음 |
| 확장 | Write-through + 관리 buffer만 배출 | 0.141 | 1,179,648 B | storage-cache 내구성 동등성을 입증하지 못해 미채택 |
| 확장 | Unbuffered write-through, 4,096-byte slot | 0.116 | 8,388,608 B | 파일 길이 7.11배·다른 표현·동등 전원 장애 자격 부재로 미채택 |
| Data-sync | Buffered `Flush(true)` | 4.360 | 1,179,648 B | 별도 동일 입력 기준 |
| Data-sync | `NtFlushBuffersFileEx(FILE_DATA_SYNC_ONLY)` | 4.345 | 1,179,648 B | 큰 절감 없음 |

Data-sync flag도 저장 장치 cache를 동기화하고 데이터 조회에 필요한 metadata를 보존하되 가능한 시각 갱신을 줄인다([Microsoft 계약](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/ntifs/nf-ntifs-ntflushbuffersfileex)). 작은 probe에서만 평가했고 제품 adapter에는 추가하지 않았다. 빠른 write-through-only 완료는 명시적 flush가 불필요하다는 증거가 아니다. 하드웨어/드라이버의 FUA 처리가 storage-cache flush와 다를 수 있다([Microsoft 설명](https://devblogs.microsoft.com/oldnewthing/20170510-00/?p=95505/)). 정상 내용 재검사는 갑작스러운 전원 장애 시험이 아니다. 디스크 설정·설치 도구·서비스·원본 프로젝트·기존 프로세스를 변경하지 않았다.

## DF-02–03 — bounded checkpoint 준비

후보는 크기 계산에서 만든 직렬화 metadata와 압축 payload를 보관해 상태 출력에 재사용했다. checkpoint별 내용/부가 비용 64MiB·65,536-entry 예산을 두고, 초과하면 준비 frame을 버리고 기존 streaming 두 번째 pass를 사용했다. 16MiB/4,096-frame 한도·형식·변경별 flush/ACK·독립 논리/물리 증명·후보/게시 뒤 두 재생·소유권·실패 writer/복구를 유지했다.

닫힌 합성 입력 하나에는 큰 private payload 20MiB와 16,000 × 112-byte private 쓰기가 들어 있다. 각 측정 프로세스는 같은 정지 입력을 시간 밖에서 복사하고 재접속한 뒤, 10,305 × 112-byte 범위를 덮어쓰고 재접속 후 수정 범위 전체와 durable sequence를 확인한다. 모든 실행은 자동 checkpoint 3회·commit 10,305회·계측 flush 10,314회를 수행한다. 불변 원본 payload를 저널로 복사하지 않는다. 작은 쓰기가 이어지는 후반부 모형이며 실제 Unity callback이나 파일 분포의 기록 재생은 아니다.

Pilot의 전체 쓰기는 **24.463초→24.224초**, **0.239초 / 1.0%**만 줄었다. Checkpoint 시간은 2.538초→2.263초였다. 다음 최종 비교 평균에 pilot을 섞지 않는다.

| 최종 순서 | 버전 | 쓰기 구간, 초 | Checkpoint, 초 | 누적 flush, 초 |
| --- | --- | ---: | ---: | ---: |
| 1 | 변경 전 | 25.409 | 2.540 | 22.620 |
| 2 | RAM 후보 | 35.567 | 2.610 | 33.022 |
| 3 | RAM 후보 | 39.830 | 2.872 | 37.374 |
| 4 | 변경 전 | 24.340 | 2.492 | 21.581 |

최종 평균은 **기준 24.874초 / 후보 37.699초**다. 증가분 대부분이 durable flush 내부에서 계측됐으며 원인을 코드의 인과적 회귀로 분리하지는 못했다. 다만 요청한 큰 절감 기준을 충족하지 못한다. Checkpoint와 flush 시간은 겹치므로 더하지 않는다. 관리 누적 할당은 458,898,640 B→421,494,808 B로 줄었지만 peak RAM이 아니며 실패한 시간 최적화를 유지할 근거로 삼지 않는다. 측정 중 빌드/probe는 겹치지 않았고 OS cache는 초기화하지 않았다.

후보와 전용 시험/probe는 추적 소스에서 제거했다. 고정 소스/바이너리·원시 결과만 보존하며 선택적 RAM/native-I/O 모드를 추가하지 않았다. 새 제품 속도 개선 주장은 없다.

## DF-04–05 — 검증과 남은 경계

실험 후보의 bounded 저널·준비 예산·쓰기 계측 집중 시험은 11.855초에 **41개 통과·실패 0·skip 0**이었다. 0/작은/유지 가능한 준비 예산, 압축/raw 내용, rename/metadata, 열린 삭제 identity, 개별 flush와 기존 bounded 중단/복구 사례를 포함한다. 해당 파일 기반 동작의 자격이며 일반 OS 장애 경계가 아니다. 최초 제한 환경 빌드 진단과 모든 probe 로그를 보존한다.

최종 필수 `pwsh -NoProfile -File scripts/build.ps1`은 locked restore·경고/오류 0개의 Release 빌드와 **1,580개 통과 / 실패 0 / 조건부 skip 26(전체 1,606)**을 555.570초에 통과했다. 최종 Runtime SHA-256은 `2daec23a795c8a156220fcd1ba4d3b02d0deb26ce777b513ef367b9b1b1eb526`으로 고정 기준과 같다. 제품 소스와 시험 변경은 제거했으며 판단·측정·문서 탐색 변경만 남긴다. 모든 실행 뒤에도 고정 입력의 전체 파일 길이와 SHA-256이 일치했다.

Group commit은 구현하지 않았다. WinFsp mutation gate 앞의 실제 대기 요청이 아직 미측정이고 동기 호출자는 묶을 요청을 제공하지 않을 수 있다. 각 성공 응답의 내구성을 유지하는 별도 큐/순서/가시성/실패 계약이 필요하다. 저장된 frame 한도 상향·조기 ACK·강제 종료·이른 unmount/Library 복원·폐기한 native Bee backend로 후보 실패를 우회하지 않았다. 새 Unity 종료/전체 수명주기·원본 반영·전원 장애/장치 자격·전체 peak RAM은 **NotRun**이다. 기존 LU-01과 미해결 과거 디버거 진단의 자격은 유지한다.
